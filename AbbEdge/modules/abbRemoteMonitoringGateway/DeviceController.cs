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
        /***
          If we can send telemetry to the cloud
         */
        public bool ReportEnabledState {get; set;} = true;
        /**
          Sent Telemetry to the cloud interval in ms
        */
        public int ReportingInterval {get; set;} = 1000;
        /** 
            Collection of telemetry data to send
        */
        public Dictionary<string, object> Telemetry {get; set;} = null;

        /** 
         Device model for IoT Hub device twin
        */
        private DeviceModel deviceModel = null;        
        /**
          Device client to talk with Device Twin
         */
        private DeviceClient controllerClient = null;

        // private bool IsDeviceInfoUpdated = false;

        public Mutex telemetryMutex {get; set; }

        private CancellationToken cancelToken;

        private DeviceController (string deviceId){
           
              this.deviceModel = new DeviceModel(){
                   Id = deviceId,
                   Created = DateTime.Now
              };
              this.Telemetry = new Dictionary<string, object>();
              this.telemetryMutex  = new Mutex(false, deviceId);
        }



       /// <summary>
        /// Initialize Device Controller IoT Hub connection and device Twin properties
        /// </summary>
        /// <param name="userContext"></param>
        /// <returns></returns>
        internal static async Task<DeviceController> Init(string HwId, DeviceConfig deviceConfig, CancellationToken cancelToken)
        {
          
          DeviceController controller = new DeviceController(HwId);
          //read current module connection string from enviroemt settings
          // see  for details https://docs.microsoft.com/en-us/azure/iot-edge/module-development#connecting-to-iot-edge-hub-from-a-module
                if (string.IsNullOrEmpty(HwId)){
                    Console.WriteLine("Error creating controller. HwId is NULL or empty");
                    return null;
                }
               
                if (string.IsNullOrEmpty(deviceConfig.ConnectionString)){
                    Console.WriteLine($"ConnectionString string for device {HwId} or empty");
                    return null;
                }

                try{
                    
                    controller.controllerClient = DeviceClient.CreateFromConnectionString(deviceConfig.ConnectionString);
                    await controller.controllerClient.OpenAsync();
                    controller.cancelToken = cancelToken;
                  //  await controller.Run();

                return controller;

                }catch(Exception ex){
                       Console.WriteLine($"Error {ex.Message} starting deviceClient for {HwId}"); 
                       Console.WriteLine(ex.StackTrace);
                       return null;
                }                 
        }

  /*      private async Task Run(){

            // Create send task               
                await Task.Factory.StartNew(async()=> {

                    while (!cancelToken.IsCancellationRequested)
                    {

                        if (this.ReportEnabledState)
                        {
                            
                                
                                    bool hasMutex = false;
                                    try{ 
                                        hasMutex = this.telemetryMutex.WaitOne(this.ReportingInterval);
                                        if (hasMutex){
                                            if (this.Telemetry.Count > 0){// Send current telemetry data            
                                                await this.SendData();
                                                this.Telemetry.Clear();
                                            }
                                          
                                        }else{
                                            Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", this.ReportingInterval);
                                        }
                                    }catch(Exception ex){
                                            Console.WriteLine("Error upload data: {0}, {1}", ex.Message, ex.StackTrace);
                                    }
                                    finally{
                                            if (hasMutex)
                                            {
                                               this.telemetryMutex.ReleaseMutex();
                                            }
                                    }
                                                                                        
                        }
                        await Task.Delay(this.ReportingInterval);
                }
                
                Console.WriteLine($"Sending task canceled for {this.deviceModel.Id}");

                }, cancelToken);
            
        }*/


       public async Task SendTelemetry()
        {
               if (this.controllerClient == null)
                {
                    Console.WriteLine("Connection To IoT Hub is not established. Cannot send message now");
                    return;
                }

                string serializedStr = JsonConvert.SerializeObject(this.Telemetry);                
                byte [] messageBytes = Encoding.ASCII.GetBytes(serializedStr);

                    Message  pipeMessage =  new Message(messageBytes);
                    pipeMessage.Properties.Add("content-type", "application/rm-gw-json");
                 
                    await this.controllerClient.SendEventAsync(pipeMessage);                            
                    Console.WriteLine($"Sent HwId {this.deviceModel.Id} message: {serializedStr}");                        
        }        

     }
}