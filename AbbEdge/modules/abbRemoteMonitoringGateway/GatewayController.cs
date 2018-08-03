    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices; 
    using System.Runtime.Serialization;
    using System.Text;
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
        public DeviceModel  GatewayDeviceConfig {get; private set;}

        // Collection of telemetry data to send
        public Dictionary<string, object> Telemetry { get; set; } = new Dictionary<string, object>();
        
        private bool IsDeviceInfoUpdated = false;

        private Mutex telemetryMutex = new Mutex(false, "Telemetry");
        
        /// <summary>
        /// Creates a new instance of a DeviceModel.
        /// </summary>
        private GatewayController(GatewayModel  gatewayDeviceConfig)
        {
            this.GatewayConfig = gatewayDeviceConfig;
            this.GatewayDeviceConfig = new DeviceModel();
            this.GatewayDeviceConfig.DeviceProperties.HubEnabledState = gatewayDeviceConfig.ReportEnabledState;
            this.GatewayDeviceConfig.DeviceProperties.CreatedTime = DateTime.UtcNow.ToString();
        }
        /**
        Initialize and Send DeviceInfo message
         */
        public static async Task<GatewayController> Start( ModuleClient ioTHubModuleClient, Twin moduleTwin, CancellationToken cancelToken){
            
                if (moduleTwin == null || moduleTwin.Properties == null)
                {
                    throw new ArgumentOutOfRangeException("No module Twin desired properties provided.");
                }
                GatewayModel  gatewayDeviceConfig = CreateGatewayModel(moduleTwin.Properties.Desired);

                GatewayController gatewayHandle = new GatewayController(gatewayDeviceConfig);
                          
                DeviceProperties gatewayProperties = gatewayHandle.GatewayDeviceConfig.DeviceProperties;                 
                gatewayProperties.DeviceID = moduleTwin.ModuleId;   
                             
                // This is the place you can specify the metadata for your device. The below fields are not mandatory.
                
                gatewayProperties.UpdatedTime = DateTime.UtcNow.ToString();
                gatewayProperties.FirmwareVersion = "1.0";
                gatewayProperties.InstalledRAM = "Unknown";
                gatewayProperties.Manufacturer = "Unknown";
                gatewayProperties.ModelNumber = "Unknown";
                gatewayProperties.Platform = RuntimeInformation.OSDescription;
                gatewayProperties.Processor = Enum.GetName(typeof(Architecture),RuntimeInformation.OSArchitecture);
                gatewayProperties.SerialNumber = "Unknown";

                // Create send task               
                await Task.Factory.StartNew(async()=> {
                    while (true)
                    {

                        if (gatewayHandle.GatewayConfig.ReportEnabledState)
                        {
                            
                                
                                    bool hasMutex = false;
                                    try{ 
                                        hasMutex = gatewayHandle.telemetryMutex.WaitOne(gatewayHandle.GatewayConfig.ReportingInterval);
                                        if (hasMutex){
                                            if (gatewayHandle.Telemetry.Count > 0){// Send current telemetry data            
                                                gatewayHandle.SendData(ioTHubModuleClient, gatewayHandle.Telemetry);
                                                gatewayHandle.Telemetry.Clear();
                                            }
                                            if (gatewayHandle.IsDeviceInfoUpdated){// Send DeviceInfo structure  
                                                gatewayHandle.SendData(ioTHubModuleClient, gatewayHandle.GatewayDeviceConfig);
                                                gatewayHandle.IsDeviceInfoUpdated = false;
                                            }
                                        }else{
                                            Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", gatewayHandle.GatewayConfig.ReportingInterval);
                                        }
                                    }finally{
                                            if (hasMutex)
                                            {
                                               gatewayHandle.telemetryMutex.ReleaseMutex();
                                            }
                                    }
                                                                                        
                        }
                        await Task.Delay(gatewayHandle.GatewayConfig.ReportingInterval);

                        if (cancelToken.IsCancellationRequested)
                        {
                            // Cancel was called
                            Console.WriteLine("Sending task canceled");
                            break;
                        }
                    }
                }, cancelToken);

                return gatewayHandle;
        }

      public static GatewayModel CreateGatewayModel(TwinCollection settings){   

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
            
             bool hasMutex = false;
            try{ 
                hasMutex = this.telemetryMutex.WaitOne(this.GatewayConfig.ReportingInterval);
                if (hasMutex){
                    this.GatewayConfig.ReportEnabledState = config.ReportEnabledState;
                    this.GatewayConfig.ReportingInterval = config.ReportingInterval;
                    
                    // Clear DeviceInfo, it will be updated soon  
                    // TODO: Copy telemetry matched metadata from existing GatewayDeviceConfig
                    this.GatewayDeviceConfig.Telemetry.Clear();
                        
                }else{
                    Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", this.GatewayConfig.ReportingInterval);
                }
            }finally{
                    if (hasMutex)
                    {
                        this.telemetryMutex.ReleaseMutex();
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


        public async Task ProcessAbbDriveProfileTelemetry(List<SignalTelemetry> telemetryData){
            await Task.Run(()=>{
                bool hasMutex = false;

                try{
                    hasMutex = this.telemetryMutex.WaitOne(this.GatewayConfig.ReportingInterval);
                    if (hasMutex){
                            foreach(SignalTelemetry signalData in telemetryData){
                                
                                    TelemetryFormat telemetryMetaData = GetMetadataForTelemetry(signalData);
                                    if (telemetryMetaData != null){
                                        
                                        object value = null;
                                        // convert telemetry value based on specified data type
                                        if (telemetryMetaData.Type.Equals("double")){
                                            value = double.Parse(signalData.Value);
                                        }else if (telemetryMetaData.Type.Equals("int")){
                                            value = int.Parse(signalData.Value);
                                        }else{
                                            // Use string as a default value type                    
                                            value = signalData.Value;
                                        }
                                        
                                        if (this.Telemetry.ContainsKey(signalData.Name)){
                                             this.Telemetry[signalData.Name] = value;
                                        }else{
                                            this.Telemetry.Add(signalData.Name, value); 
                                        }

                                                                   
                                    }
                        }
                    }
                    else{
                        throw new InvalidOperationException(String.Format("Can't handle telemetry. Timeout after {0} ms. ", this.GatewayConfig.ReportingInterval)); 
                    }
                }
                finally{
                    if (hasMutex){
                        this.telemetryMutex.ReleaseMutex();
                    }
                }
            });
        }

        /**
         Return true if 
        */
            private TelemetryFormat GetMetadataForTelemetry(SignalTelemetry signalData){

                if (this.GatewayConfig.ReportedTelemetry.ContainsKey(signalData.Name)){
                    TelemetryMetadataModel gwConfigMetadata = this.GatewayConfig.ReportedTelemetry.GetValueOrDefault(signalData.Name);
                    
                    // Construct metadata
                    TelemetryFormat telemetryMetaData = new TelemetryFormat(){
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
                    Console.WriteLine("Signal {0} is not configured for reporting at the module Twin ");
                    return null;
                }
                
            }
    }
    

    public class GatewayModel
    {            
        public int ReportingInterval {get; set;}
        public bool ReportEnabledState {get; set;}
        
 //       public Dictionary<string, Command> Commands;        
        public Dictionary<string, TelemetryMetadataModel> ReportedTelemetry;
  
    }

    public class TelemetryMetadataModel{
        public string DisplayName {get; set;}
    }


    public class SignalTelemetry{
        public string Name { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; }
        public string ValueUnit { get; set; }
    }          
}
