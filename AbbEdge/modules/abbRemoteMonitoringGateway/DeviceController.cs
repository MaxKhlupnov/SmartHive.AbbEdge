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
        private Dictionary<string, object> Telemetry = null;

        /** 
         Device model for IoT Hub device twin
        */
        private DeviceModel deviceModel = null;        
        /**
          Device client to talk with Device Twin
         */
        private DeviceClient controllerClient = null;

        // private bool IsDeviceInfoUpdated = false;

        private Mutex telemetryMutex = new Mutex(false, "Telemetry");

        

        private DeviceController (string deviceId){
           
              this.deviceModel = new DeviceModel(){
                   Id = deviceId,
                   Created = DateTime.Now
              };
              this.Telemetry = new Dictionary<string, object>();

        }


        public void SetTelemetry(string TelemetryName, object TelemetryValue){
            bool hasMutex = false;
            try{ 
                hasMutex = this.telemetryMutex.WaitOne(this.ReportingInterval);
                if (hasMutex){
                    if (this.Telemetry.ContainsKey(TelemetryName)){
                        this.Telemetry[TelemetryName] = TelemetryValue;
                    }else{
                        this.Telemetry.Add(TelemetryName, TelemetryValue); 
                    }
                }else{
                      Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", this.ReportingInterval);
                  }
            }finally{
                    if (hasMutex)
                    {
                        this.telemetryMutex.ReleaseMutex();
                    }
            }                    
        }

        public void ClearTelemetry(){
            bool hasMutex = false;
            try{ 
                hasMutex = this.telemetryMutex.WaitOne(this.ReportingInterval);
                if (hasMutex){
                    this.Telemetry.Clear();
                }else{
                      Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", this.ReportingInterval);
                  }
            }finally{
                    if (hasMutex)
                    {
                        this.telemetryMutex.ReleaseMutex();
                    }
            }       
        }

       /// <summary>
        /// Initialize Device Controller IoT Hub connection and device Twin properties
        /// </summary>
        /// <param name="userContext"></param>
        /// <returns></returns>
        internal static async Task<DeviceController> Init(string deviceId, DeviceConfig deviceConfig, CancellationToken cancelToken)
        {
          
          DeviceController controller = new DeviceController(deviceId);
          //read current module connection string from enviroemt settings
          // see  for details https://docs.microsoft.com/en-us/azure/iot-edge/module-development#connecting-to-iot-edge-hub-from-a-module

                string connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");
                
                controller.controllerClient = DeviceClient.CreateFromConnectionString(connectionString, deviceId);
                await controller.controllerClient.OpenAsync();

                return controller;
                  // Create send task                              
                   /*  while (GatewayController.IsRun)
                    {                                                                                                                                   
                         if (gatewayHandle.IsDeviceInfoUpdated){// Send DeviceInfo structure into module twin
                                        string  serializedStr = JsonConvert.SerializeObject(gatewayHandle.GatewayDeviceConfig); 
                                        TwinCollection reported = JsonConvert.DeserializeObject<TwinCollection>(serializedStr);
                                        //await ioTHubModuleClient.UpdateReportedPropertiesAsync(reported);                                                    
                                        gatewayHandle.IsDeviceInfoUpdated = false;                                                                                       
                          }else{
                                Console.WriteLine("Error. Can't get mutext for telemetry data for {0} ms. Timeout!", gatewayHandle.GatewayConfig.ReportingInterval);
                          }
                    if (GatewayController.IsRun)
                    {
                        break;
                    }
                    await Task.Delay(gatewayHandle.GatewayConfig.ReportingInterval);
                    } */                       
        }
     }
}