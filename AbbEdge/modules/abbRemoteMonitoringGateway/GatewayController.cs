    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices; 
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    


namespace abbRemoteMonitoringGateway
{
    class GatewayController {
         internal const string GatewayConfigSection = "GatewayConfig";

        public DeviceModel  GatewayDeviceConfig {get; private set;
        }
        /// <summary>
        /// Creates a new instance of a DeviceModel.
        /// </summary>
        public GatewayController(DeviceModel  gatewayDeviceConfig)
        {
            this.GatewayDeviceConfig = gatewayDeviceConfig;   
                
        }
        /**
        Initialize and Send DeviceInfo message
         */
        public static async Task Start(object userContext){
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
                gatewayProperties.CreatedTime = DateTime.UtcNow.ToString();
                gatewayProperties.UpdatedTime = DateTime.UtcNow.ToString();
                gatewayProperties.FirmwareVersion = "1.0";
                gatewayProperties.InstalledRAM = "Unknown";
                gatewayProperties.Manufacturer = "Unknown";
                gatewayProperties.ModelNumber = "Unknown";
                gatewayProperties.Platform = RuntimeInformation.OSDescription;
                gatewayProperties.Processor = Enum.GetName(typeof(Architecture),RuntimeInformation.OSArchitecture);
                gatewayProperties.SerialNumber = "Unknown";


        }

      
      
    }
}
