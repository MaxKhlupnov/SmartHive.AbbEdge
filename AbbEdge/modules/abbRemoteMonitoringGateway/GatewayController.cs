    using System;
    using System.IO;
    using System.Collections;
    using System.Collections.Generic;    
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices; 
    using System.Runtime.Serialization;
    using System.Text;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;


namespace abbRemoteMonitoringGateway
{
    class GatewayController {        

        /**
            Module configuration settings comming from Module Twin
        */
        public GatewayModel  GatewayConfig {get; private set;}
        /**
            Gateway device configuration settings reported into remotemonitoring via DeviceInfo message
         */
        private Dictionary<string,DeviceController> DeviceHandlers = null;               
        
        /// <summary>
        /// Creates a new instance of a DeviceModel.
        /// </summary>
        private GatewayController(GatewayModel  gatewayDeviceConfig)
        {
            this.GatewayConfig = gatewayDeviceConfig;
            this.DeviceHandlers = new Dictionary<string,DeviceController>(gatewayDeviceConfig.DownstreamDevices.Count);
        }
 
        internal static async Task<GatewayController> Init(GatewayModel  gatewayDeviceConfig, CancellationToken cancelToken){

                if (gatewayDeviceConfig ==null || gatewayDeviceConfig.DownstreamDevices == null || gatewayDeviceConfig.DownstreamDevices.Count == 0)
                    throw new ArgumentException("Gateway config in the module twin contain no devices under DownstreamDevices section");
                
                    GatewayController controller = new GatewayController(gatewayDeviceConfig);
                    InstallCert();

                    foreach(string HwId in gatewayDeviceConfig.DownstreamDevices.Keys){
                        DeviceConfig deviceConfig = null;
                        if (gatewayDeviceConfig.DownstreamDevices.TryGetValue(HwId, out deviceConfig)){

                        await Task.Run( async() => {
                                DeviceController deviceController = await DeviceController.Init(HwId,deviceConfig, cancelToken);     
                                if (deviceController != null) {                        
                                    controller.DeviceHandlers.Add(HwId, deviceController);
                                }else{
                                    Console.WriteLine($"Error creating Device Controller for {HwId}");
                                }

                         });
                        }else{
                            Console.WriteLine("Error reading  config for Device {0} in from module twin DownstreamDevices section", deviceConfig);
                        }

                    }
                    
                    return controller;
        }


        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        private static void InstallCert()
        {
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                return;
                // throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                 return;
                //throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
        }

      public static GatewayModel CreateGatewayModel(TwinCollection settings){
            
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            string  serializedStr = JsonConvert.SerializeObject(settings);
            if (string.IsNullOrEmpty(serializedStr))
            {
                throw new ArgumentOutOfRangeException("No configuration provided for the module Twin.");
            }          
            else
            {
                Console.WriteLine(String.Format("Attempt to parse configuration JSON: {0}", serializedStr));                
                GatewayModel model = JsonConvert.DeserializeObject<GatewayModel>(serializedStr);
                if (model == null){
                    throw new ArgumentOutOfRangeException("Errorparsing gateway twin settings");
                }else{
                    return model;
                }               
            }
        }        
        public void UpdateGatewayModel(GatewayModel config){
            
                    foreach (DeviceController cntrl in this.DeviceHandlers.Values)
                    {
                        cntrl.ReportEnabledState = config.ReportEnabledState;
                        cntrl.ReportingInterval = config.ReportingInterval;
                        // Clear DeviceInfo, it will be updated soon  
                        // TODO: Copy telemetry matched metadata from existing GatewayDeviceConfig
                        bool hasMutex = false;
                        try{ 
                            hasMutex = cntrl.telemetryMutex.WaitOne(cntrl.ReportingInterval);
                            if (hasMutex){
                                cntrl.Telemetry.Clear();                    
                            }else{
                            Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", cntrl.ReportingInterval);
                        }
                    }finally{
                        if (hasMutex)
                        {
                            cntrl.telemetryMutex.ReleaseMutex();
                        }
                    }         
                    }
        }

        private async void SendData(ModuleClient ioTHubModuleClient, object data)
        {
            try
            {
                 if (ioTHubModuleClient == null)
                {
                    Console.WriteLine("Connection To IoT Hub is not established. Cannot send message now");
                    return;
                }

                string serializedStr = JsonConvert.SerializeObject(data);                
                byte [] messageBytes = Encoding.ASCII.GetBytes(serializedStr);

                    Message  pipeMessage =  new Message(messageBytes);
                    pipeMessage.Properties.Add("content-type", "application/rm-gw-json");
                 
                    await ioTHubModuleClient.SendEventAsync("remoteMonitoringOutput", pipeMessage);                            
                    Console.WriteLine(String.Format("Sent message: {0}", serializedStr));
            }
            catch (System.Exception e)
            {
                Console.WriteLine("Exception while sending message to IoT Hub:\n" + e.Message.ToString());
            }
        }        


        private async Task ProcessTelemetryForHwId(string HwId, SignalTelemetry[] telemetryData){
                
                if (telemetryData == null || telemetryData.Length == 0){
                    Console.WriteLine(@"Warning: No telemetry in a batch for {HwId}");
                }

                DeviceController controller = null;
                if (DeviceHandlers.TryGetValue(HwId, out controller)){
                    
                    Mutex mutex = Mutex.OpenExisting(HwId);
                    bool acquiredMutex;
                    try{ 
                        
                        if (mutex != null){
                            
                            try{
                                acquiredMutex = mutex.WaitOne(TimeSpan.FromSeconds(15));
                            }catch(AbandonedMutexException)
                            {
                                acquiredMutex = true;
                            }

                            try{
                                if (acquiredMutex){
                                    foreach(SignalTelemetry signalData in telemetryData){
                                        object TelemetryValue = GetSignalValue(signalData);
                                        if (controller.Telemetry.ContainsKey(signalData.Name)){
                                            controller.Telemetry[signalData.Name] = TelemetryValue;
                                        }else{
                                            controller.Telemetry.Add(signalData.Name, TelemetryValue); 
                                        }                                                                 
                                            
                                    } 
                                    await controller.SendTelemetry();                                                                                                                                         

                                    controller.Telemetry.Clear();
                                }
                            }finally{
                                if (acquiredMutex)
                                {
                                        mutex.ReleaseMutex();
                                }
                            }                                  
                        }else{
                            Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", controller.ReportingInterval);
                        }
                    }
                    catch(Exception ex){
                            Console.WriteLine($"Device {HwId} telemetry processing error {ex.Message}: {ex.StackTrace}");                    
                    }       

                 

                }else{
                       Console.WriteLine($"Warning: Can't find handler for deviceId wasn't configured for {HwId}");
                }
        }

        private static object GetSignalValue(SignalTelemetry signalData){
                        
                        object value = null;
                        // convert telemetry value based on specified data type
                        if (signalData.ValueType.Equals("double",StringComparison.InvariantCultureIgnoreCase)){
                            value = double.Parse(signalData.Value);
                        }else if (signalData.ValueType.Equals("int",StringComparison.InvariantCultureIgnoreCase)){
                            value = int.Parse(signalData.Value);
                        }else{
                            // Use string as a default value type                    
                            value = signalData.Value;
                        }
                        return value;
        }

        public async Task ProcessAbbDriveProfileTelemetry(List<SignalTelemetry> telemetryData){
            await Task.Run(async ()=>{                
                 IEnumerable<IGrouping<string,SignalTelemetry>> telemetryByHw = telemetryData.GroupBy(t => t.HwId);                
                 foreach(IGrouping<string,SignalTelemetry> hwTelemetry in telemetryByHw){
                      await this.ProcessTelemetryForHwId(hwTelemetry.Key,  hwTelemetry.ToArray<SignalTelemetry>());
                 }

            });
        }

        /**
         Return true if 
        */
            private void GetMetadataForTelemetry(SignalTelemetry signalData){


                    return;
            /*    if (this.GatewayConfig.ReportedTelemetry.ContainsKey(signalData.Name)){
                    TelemetryMetadataModel gwConfigMetadata = this.GatewayConfig.DownstreamDevice.ReportedTelemetry.GetValueOrDefault(signalData.Name);
                    
                    // Construct metadata
                    TelemetrySchema telemetryMetaData = new TelemetrySchema(){
                        Name = signalData.Name,
                        DisplayName = gwConfigMetadata.DisplayName,
                        Type  = signalData.ValueType.ToLowerInvariant()
                    };
                    // Check if we have metadata description in monitoring device
                    if (!this.GatewayDeviceConfig.Telemetry.Contains(telemetryMetaData)){
                        this.GatewayDeviceConfig.Telemetry.Add(telemetryMetaData);
                        Console.WriteLine(String.Format("Telemetry configured for remote monitoring: Name: {0}, Type: {1}, DisplayName: {2}", 
                                                    telemetryMetaData.Name, telemetryMetaData.Type, telemetryMetaData.DisplayName));
                        this.IsDeviceInfoUpdated = true;
                    }
                
                    return telemetryMetaData;
                }else{
                    Console.WriteLine("Signal {0} is not configured for reporting at the module Twin ", signalData.Name);
                    return null;
                }*/
                
            }
    }
    

    public class GatewayModel
    {            
        public bool ReportEnabledState {get; set;}
         public int ReportingInterval {get; set;}
         public string  ReportedTelemetry {get; set;}
  
 //       public Dictionary<string, Command> Commands;        
        public Dictionary<string, DeviceConfig> DownstreamDevices;
  
    }

    public class DeviceConfig{
        public string ConnectionString {get; set;}
        public string Type {get; set;}
        public string Location {get; set;}
        public double Latitiude {get; set;}
        public double Longitude {get; set;}
        public string ReportedTelemetry {get; set;}
    }


    public class SignalTelemetry{
        public string HwId { get; set; }
        public string SourceTimestamp { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; }
        public string ValueUnit { get; set; }
    }          
}
