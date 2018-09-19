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
    using abbRemoteMonitoringGateway.Models;

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

        public DeviceType GetDeviceType (string Type){
            DeviceType returnValue = null;
                if (this.GatewayConfig.DeviceTypes.TryGetValue(Type, out returnValue)){
                    return returnValue;
                }else{
                    throw new ArgumentException($"Can't find definition in Twin for type {Type}");
                }
        }

        internal static async Task<GatewayController> Make(GatewayModel  gatewayDeviceConfig){

                if (gatewayDeviceConfig ==null || gatewayDeviceConfig.DownstreamDevices == null || gatewayDeviceConfig.DownstreamDevices.Count == 0)
                    throw new ArgumentException("Gateway config in the module twin contain no devices under DownstreamDevices section");
                
                    GatewayController controller = new GatewayController(gatewayDeviceConfig);
                    InstallCert();

                    await controller.LoadConfig(gatewayDeviceConfig);
                                       
                    return controller;
        }

        private async Task  LoadConfig(GatewayModel  gatewayDeviceConfig){
                 foreach(string HwId in gatewayDeviceConfig.DownstreamDevices.Keys){
                        DeviceConfig deviceConfig = GetConfigForHwId(HwId);
                        if (deviceConfig != null){

                        await Task.Run( async() => {
                                DeviceController deviceController = await DeviceController.Init(HwId,this);     
                                if (deviceController != null) {                        
                                    this.DeviceHandlers.Add(HwId, deviceController);
                                }else{
                                    Console.WriteLine($"Error creating Device Controller for {HwId}");
                                }

                         });
                        }else{
                            Console.WriteLine("Error reading  config for Device {0} in from module twin DownstreamDevices section", deviceConfig);
                        }

                    }
        } 


        public DeviceConfig GetConfigForHwId(string HwId){
             DeviceConfig deviceConfig = null;
             if (this.GatewayConfig.DownstreamDevices.TryGetValue(HwId, out deviceConfig))
                return deviceConfig;
             else 
                return null;
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
        public async Task UpdateGatewayModel(GatewayModel config){            
                  
                 await this.LoadConfig(config);                                      
        }

/*       private async void SendData(ModuleClient ioTHubModuleClient, object data)
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
        }   */     


        private void ProcessTelemetryForHwId(string HwId, SignalTelemetry[] telemetryData){
                
                if (telemetryData == null || telemetryData.Length == 0){
                    Console.WriteLine(@"Warning: No telemetry in a batch for {HwId}");
                }

                DeviceController controller = null;
                if (DeviceHandlers.TryGetValue(HwId, out controller)){
                    
                       
                        foreach(SignalTelemetry signalData in telemetryData){                    
                            controller.SetTelemetry(signalData);                                                                                                       
                        }                                                                                                                                                                       
                                           
                }else{
                       Console.WriteLine($"Warning: Can't find handler for deviceId wasn't configured for {HwId}");
                }
        }
        
 

        public async Task ProcessAbbDriveProfileTelemetry(List<SignalTelemetry> telemetryData){
            await Task.Run(()=>{                
                 IEnumerable<IGrouping<string,SignalTelemetry>> telemetryByHw = telemetryData.GroupBy(t => t.HwId);                
                 foreach(IGrouping<string,SignalTelemetry> hwTelemetry in telemetryByHw){
                       this.ProcessTelemetryForHwId(hwTelemetry.Key,  hwTelemetry.ToArray<SignalTelemetry>());
                 }

            });
        }
    }
    

    public class GatewayModel
    {            
        public bool ReportEnabledState {get; set;}
         public int ReportingInterval {get; set;}
         public string ReportedTelemetry {get; set;}

         public Dictionary<string, DeviceType> DeviceTypes {get; set;}
  
 //       public Dictionary<string, Command> Commands;        
        public Dictionary<string, DeviceConfig> DownstreamDevices;
  
    }
    
    public class DeviceConfig{
        public string ConnectionString {get; set;}
        public string Type {get; set;}
        public string Protocol { get; set; }

        public Dictionary<string, object> Properties { get; set; }
    }


    public class DeviceType{            
            public string TelemetryFields {get; set;}
            public bool ReportValueUnits {get; set;} = false;
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
