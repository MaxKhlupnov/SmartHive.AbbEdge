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
         internal const string GatewayConfigSection = "GatewayConfig";

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
        public GatewayController(GatewayModel  gatewayDeviceConfig)
        {
            this.GatewayConfig = gatewayDeviceConfig;
            this.GatewayDeviceConfig = new DeviceModel();
            this.GatewayDeviceConfig.DeviceProperties.HubEnabledState = gatewayDeviceConfig.HubEnabledState;
            this.GatewayDeviceConfig.DeviceProperties.CreatedTime = DateTime.UtcNow.ToString();
        }
        /**
        Initialize and Send DeviceInfo message
         */
        public static async Task Start(object userContext, CancellationToken cancelToken){
            var userContextValues  = userContext as Tuple<ModuleClient, GatewayController>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }
                ModuleClient ioTHubModuleClient = userContextValues.Item1;
                GatewayController gatewayHandle = userContextValues.Item2;
                Twin twin = await ioTHubModuleClient.GetTwinAsync();                

                DeviceProperties gatewayProperties = gatewayHandle.GatewayDeviceConfig.DeviceProperties;                 
                gatewayProperties.DeviceID = twin.ModuleId;   
                             
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

                        if (gatewayHandle.GatewayConfig.HubEnabledState)
                        {
                            
                                if (gatewayHandle.Telemetry.Count > 0){// Send current telemetry data
                                    bool hasMutex = false;
                                    try{ 
                                        hasMutex = gatewayHandle.telemetryMutex.WaitOne(gatewayHandle.GatewayConfig.PoolingInterval);
                                        if (hasMutex){
                                            
                                            gatewayHandle.SendData(ioTHubModuleClient, gatewayHandle.Telemetry);
                                            gatewayHandle.Telemetry.Clear();
                                        }else{
                                            Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", gatewayHandle.GatewayConfig.PoolingInterval);
                                        }
                                    }finally{
                                            if (hasMutex)
                                            {
                                               gatewayHandle.telemetryMutex.ReleaseMutex();
                                            }
                                    }
                                }
                            
                            if (gatewayHandle.IsDeviceInfoUpdated){
                                gatewayHandle.SendData(ioTHubModuleClient, gatewayHandle.GatewayDeviceConfig);
                            }
                        }
                        await Task.Delay(gatewayHandle.GatewayConfig.PoolingInterval);

                        if (cancelToken.IsCancellationRequested)
                        {
                            // Cancel was called
                            Console.WriteLine("Sending task canceled");
                            break;
                        }
                    }
                }, cancelToken);
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
                    hasMutex = this.telemetryMutex.WaitOne(this.GatewayConfig.PoolingInterval);
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
                                        this.Telemetry.Add(signalData.Name, value);                        
                                    }
                        }
                    }
                    else{
                        throw new InvalidOperationException(String.Format("Can't handle telemetry. Timeout after {0} ms. ", this.GatewayConfig.PoolingInterval)); 
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

                if (this.GatewayConfig.Telemetry.ContainsKey(signalData.Name)){
                    TelemetryFormat gwConfigMetadata = this.GatewayConfig.Telemetry.GetValueOrDefault(signalData.Name);
                    
                    // Construct metadata
                    TelemetryFormat telemetryMetaData = new TelemetryFormat(){
                        Name = signalData.Name,
                        DisplayName = gwConfigMetadata.DisplayName,
                        Type  = signalData.ValueType.ToLowerInvariant()
                    };
                    // Check if we have metadata description in monitoring device
                    if (!this.GatewayDeviceConfig.Telemetry.Contains(telemetryMetaData)){
                        this.GatewayDeviceConfig.Telemetry.Add(telemetryMetaData);
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
        public int PoolingInterval = 2000;
        public bool HubEnabledState = true;        
        public Dictionary<string, Command> Commands;        
        public Dictionary<string, TelemetryFormat> Telemetry;
    }


    public class SignalTelemetry{
        public string Name { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; }
        public string ValueUnit { get; set; }
    }          
}
