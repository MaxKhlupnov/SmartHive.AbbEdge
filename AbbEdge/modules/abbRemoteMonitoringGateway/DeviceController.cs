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
     class DeviceController {    

        private DeviceModel deviceModel = null;
        
        DeviceController (DeviceModel model){
            deviceModel = model;
        }
       /// <summary>
        /// Iterate through each Modbus session to poll data 
        /// </summary>
        /// <param name="userContext"></param>
        /// <returns></returns>
      /*  async Task Start(GatewayController gatewayHandle)
        {
                  // Create send task                              
                    while (GatewayController.IsRun)
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
                    }                                    
        }*/
     }
}