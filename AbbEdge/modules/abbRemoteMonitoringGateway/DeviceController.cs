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

using abbRemoteMonitoringGateway.Models;

namespace abbRemoteMonitoringGateway
{
     class DeviceController {    
        
        /** 
            Collection of telemetry data to send
        */
         public Dictionary<string, object> Telemetry {get; set;} = null;  
         /**
          #endregionList of metadata to initialize MessageSchema
          */
         private List<string> TelemetryFieldsToInit = null;

        private bool ReportValueUnits = false;

        private const string  cValueUnitsSuffix = "_unit";
        /** 
         Device model for IoT Hub device twin
        */
        private DeviceModel deviceModel = null;        
        /**
          Device client to talk with Device Twin
         */
        private DeviceClient controllerClient = null;

        private DateTimeOffset DeviceTwinReportedTime = new DateTime(2000,1,1);

        public bool ReportEnabledState {get; set;} = true;               

        private DeviceController (string deviceId){
           
              this.deviceModel = new DeviceModel(){
                   Id = deviceId,
                   Created = DateTime.Now
              };

              this.Telemetry = new Dictionary<string, object> ();
        }


        public void SetTelemetry(SignalTelemetry telemetry){
               
            lock(this.Telemetry){
                if (this.Telemetry.ContainsKey(telemetry.Name)){
                    this.Telemetry[telemetry.Name] =  GetSignalValue(telemetry);
                }else{
                    this.Telemetry.Add(telemetry.Name, GetSignalValue(telemetry)); 
                }

                // Add telemetry value units if requested
                if (this.ReportValueUnits){
                    string unitsKey = telemetry.Name + cValueUnitsSuffix;

                    if (this.Telemetry.ContainsKey(unitsKey)){
                        this.Telemetry[unitsKey] =  telemetry.ValueUnit;
                    }else{
                        this.Telemetry.Add(unitsKey, telemetry.ValueUnit); 
                    }
                }
            }

            try{
                if (this.TelemetryFieldsToInit != null && TelemetryFieldsToInit.Contains(telemetry.Name) 
                        && this.deviceModel.Telemetry != null && this.deviceModel.Telemetry.Count > 0){
                    lock(TelemetryFieldsToInit){
                      DeviceModel.DeviceModelMessage devMsgSchema = this.deviceModel.Telemetry[0];
                      DeviceModel.DeviceModelMessageSchemaType schemaTypeField;
                        if (!devMsgSchema.MessageSchema.Fields.TryGetValue(telemetry.Name, out schemaTypeField)){
                             // if this field not exist
                                schemaTypeField = (DeviceModel.DeviceModelMessageSchemaType) Enum.Parse(typeof(DeviceModel.DeviceModelMessageSchemaType), telemetry.ValueType);
                                devMsgSchema.MessageSchema.Fields.Add(telemetry.Name, schemaTypeField);
                                if (this.ReportValueUnits){
                                    devMsgSchema.MessageSchema.Fields.Add(telemetry.Name+cValueUnitsSuffix, DeviceModel.DeviceModelMessageSchemaType.Text);
                                }

                            devMsgSchema.MessageTemplate = ConstructMessageTemplate(devMsgSchema.MessageSchema.Fields);
                            TelemetryFieldsToInit.Remove(telemetry.Name);
                            this.deviceModel.Modified = DateTimeOffset.Now;
                        };
                                
                    }
                }
            }catch(Exception ex){
                Console.WriteLine($"Error {ex.Message} building device {telemetry.HwId} telemetry schema for the field {telemetry.Name} typeof {telemetry.ValueType} {ex.StackTrace}");                 
            }
        }


            private string ConstructMessageTemplate(IDictionary<string, DeviceModel.DeviceModelMessageSchemaType> fields){
                if (fields == null || fields.Count == 0)
                    return string.Empty;

                    StringBuilder sb = new StringBuilder(100).Append('{');
                        int fieldNumber = 0;
                        foreach(string field in fields.Keys){
                            fieldNumber ++;
                            if (!field.EndsWith(cValueUnitsSuffix)){                            
                                // Add only measurements and skip _unites telemetry
                                sb.Append($"\"{field}\":$").Append('{').Append(field).Append('}');
                                if (this.ReportValueUnits){
                                    sb.Append($",\"{field}{cValueUnitsSuffix}\":$").Append('{').Append(field).Append(cValueUnitsSuffix).Append('}');
                                }
                            }
                            if (fieldNumber == fields.Keys.Count){
                                sb.Append('}');
                            }
                        }

                    return sb.ToString();
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

       /// <summary>
        /// Initialize Device Controller IoT Hub connection and device Twin properties
        /// </summary>
        /// <param name="userContext"></param>
        /// <returns></returns>
        internal static async Task<DeviceController> Init(string HwId, GatewayController gatewayController)
        {
          
          DeviceController controller = new DeviceController(HwId);
          //read current module connection string from enviroemt settings
          // see  for details https://docs.microsoft.com/en-us/azure/iot-edge/module-development#connecting-to-iot-edge-hub-from-a-module
                if (string.IsNullOrEmpty(HwId)){
                    Console.WriteLine("Error creating controller. HwId is NULL or empty");
                    return null;
                }
                
                DeviceConfig deviceConfig = gatewayController.GetConfigForHwId(HwId);

                if (string.IsNullOrEmpty(deviceConfig.ConnectionString)){
                    Console.WriteLine($"ConnectionString string for device {HwId} or empty");
                    return null;
                }

                try{
                    
                    TransportType tt = TransportType.Mqtt_Tcp_Only;
                    try{
                        tt = (TransportType) Enum.Parse(typeof(TransportType), deviceConfig.Protocol, true);
                        
                    }catch(Exception){
                        Console.WriteLine($"Error parsing protocol {deviceConfig.Protocol} as TransportType enum");
                    }

                    Console.WriteLine($"Selected {tt} as device client transport");

                    controller.controllerClient = DeviceClient.CreateFromConnectionString(deviceConfig.ConnectionString, tt);                    
                    controller.controllerClient.SetConnectionStatusChangesHandler(controller.ConnectionStatusChangesHandler);                    
                    
                    // Update device model                    
                    controller.deviceModel.Properties = deviceConfig.Properties;
                    controller.deviceModel.Protocol = Enum.GetName(typeof(TransportType), tt);
                    controller.deviceModel.Type = deviceConfig.Type;
                    //Construct DeviceModel
                    try{
                        DeviceType deviceSchema=  gatewayController.GetDeviceType(deviceConfig.Type);  
                        
                        controller.TelemetryFieldsToInit = new List<string>(deviceSchema.TelemetryFields.Split(','));
                        
                        controller.ReportValueUnits = deviceSchema.ReportValueUnits;

                        DeviceModel.DeviceModelMessage devMsgSchema = new DeviceModel.DeviceModelMessage();
                            devMsgSchema.MessageSchema = new DeviceModel.DeviceModelMessageSchema();
                            devMsgSchema.MessageSchema.Name = deviceConfig.Type.ToLowerInvariant() + ";v1";
                            devMsgSchema.MessageSchema.Format = DeviceModel.DeviceModelMessageSchemaFormat.JSON;
                            devMsgSchema.MessageSchema.Fields = new Dictionary<string,DeviceModel.DeviceModelMessageSchemaType>();
                            devMsgSchema.Interval = TimeSpan.FromMilliseconds(gatewayController.GatewayConfig.ReportingInterval);


                        controller.deviceModel.Telemetry.Add(devMsgSchema);
                        
                    }catch(Exception ex){
                         Console.WriteLine($"Device {HwId} message schema initialization error {ex.Message} {ex.StackTrace}");
                    }
                    
                        controller.ReportEnabledState = gatewayController.GatewayConfig.ReportEnabledState;
                        await controller.Run(gatewayController.GatewayConfig.ReportingInterval);

                return controller;

                }catch(Exception ex){
                       Console.WriteLine($"Error {ex.Message} starting deviceClient for {HwId}"); 
                       Console.WriteLine(ex.StackTrace);
                       return null;
                }                 
        }

          private void ConnectionStatusChangesHandler(ConnectionStatus status, ConnectionStatusChangeReason reason){

                Console.WriteLine($"Connection status  changed {status} {reason} at {DateTime.Now}");
          }

       private async Task SendTelemetry()
        {
               if (this.controllerClient == null)
                {
                    Console.WriteLine("Connection To IoT Hub is not established. Cannot send message now");
                    return;
                }

            if (this.Telemetry == null || this.Telemetry.Count == 0){
                    Console.WriteLine("No new telemetry to sent");
                    return;
            }

            string serializedStr = string.Empty;
            lock(this.Telemetry){                    
                    serializedStr = JsonConvert.SerializeObject(this.Telemetry);                                    
                    this.Telemetry.Clear();                                               
            }
            byte [] messageBytes = Encoding.ASCII.GetBytes(serializedStr);
             Message  pipeMessage =  new Message(messageBytes);
            pipeMessage.Properties.Add("content-type", "application/rm-gw-json");
            
            await this.controllerClient.SendEventAsync(pipeMessage).ConfigureAwait(false);
            
            Console.WriteLine($"Sent HwId {this.deviceModel.Id} message: {serializedStr}");  

                   
        }        

        /* Loop for sending telemetry*/            
        private async Task Run(int ReportingInterval){
            // Create send task               
                await Task.Factory.StartNew(async()=> {

                    while (this.ReportEnabledState)
                    {
                        try{
                            await this.controllerClient.OpenAsync().ConfigureAwait(false);;

                            await this.SendTelemetry().ConfigureAwait(false);;
                            // Update device twin if changed
                            if (this.deviceModel.Telemetry != null && this.deviceModel.Telemetry.Count > 0 &&
                                        this.DeviceTwinReportedTime < this.deviceModel.Modified ){
                                // Update Twin BTW 
                                                                    
                                    string serializedStr = JsonConvert.SerializeObject(this.deviceModel);                                     
                                    Console.WriteLine($"Updating device twin  {serializedStr}");
                                 
                                   TwinCollection reportedProperties = JsonConvert.DeserializeObject<TwinCollection>(serializedStr);                         
                                    
                                    await this.controllerClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);;                                                                   
                                    this.DeviceTwinReportedTime = DateTimeOffset.Now;

                            await this.controllerClient.CloseAsync().ConfigureAwait(false);;
                            }
                        }
                        catch(Exception ex){
                            Console.WriteLine($"Send telemetry {this.deviceModel.Id} error {ex.Message} {ex.StackTrace}");
                        }
                       await Task.Delay(ReportingInterval);
                    }
                
                Console.WriteLine($"Sending task canceled for {this.deviceModel.Id}");

                }).ConfigureAwait(false);
            
        }

     }
}