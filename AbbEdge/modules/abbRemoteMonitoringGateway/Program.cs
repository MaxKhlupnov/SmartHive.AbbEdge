namespace abbRemoteMonitoringGateway
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
   

    class Program
    {
        static int counter;
        private static CancellationTokenSource cts;
        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

             // Read TemperatureThreshold from Module Twin Desired Properties
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            var moduleTwinCollection = moduleTwin.Properties.Desired;
            try {
                await DoTwinUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);
            } catch(ArgumentOutOfRangeException e) {
                Console.WriteLine($"Error setting desired  properties: {e.Message}"); 
            }
           
            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, ioTHubModuleClient);
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
             var userContextValues  = userContext as Tuple<ModuleClient, GatewayController>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }
                ModuleClient ioTHubModuleClient = userContextValues.Item1;
                GatewayController gatewayHandle = userContextValues.Item2;

            int counterValue = Interlocked.Increment(ref counter);
            

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
              //  await Task.Run(() => {
                 List<SignalTelemetry> telemetryData = JsonConvert.DeserializeObject<List<SignalTelemetry>>(messageString);                
                 if (telemetryData.Count > 0){
                    await gatewayHandle.ProcessAbbDriveProfileTelemetry(telemetryData);
                 } 
               /// }); 
                
            }else{                
                throw new InvalidOperationException("Error: message body is empty"); 
                
            }
            return MessageResponse.Completed;
        }

        /// <summary>
        /// Callback to handle Twin desired properties updates�
        /// </summary>
        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            ModuleClient ioTHubModuleClient = userContext as ModuleClient;

             try
            {
                // stop all activities while updating configuration
                await ioTHubModuleClient.SetInputMessageHandlerAsync(
                "gatewayInput",
                DummyCallBack,
                null);

                await DoTwinUpdate(desiredProperties, ioTHubModuleClient);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            
        }
               /// <summary>
        /// A dummy callback does nothing
        /// </summary>
        /// <param name="message"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        static async Task<MessageResponse> DummyCallBack(Message message, object userContext)
        {
            await Task.Delay(TimeSpan.FromSeconds(0));
            return MessageResponse.Abandoned;
        }


         /// <summary>
        /// Update Start from module Twin. 
        /// </summary>
        static async Task  DoTwinUpdate(TwinCollection desiredProperties, ModuleClient ioTHubModuleClient)
        {                                             
            string  serializedStr = JsonConvert.SerializeObject(desiredProperties);
            Console.WriteLine("Updating desired properties:");
            Console.WriteLine(serializedStr);

            if (string.IsNullOrEmpty(serializedStr))
            {
                Console.WriteLine("No configuration provided for the module Twin.");
            }
            else if (!desiredProperties.Contains(GatewayController.GatewayConfigSection))
            {
                Console.WriteLine("No Remote Monitoring Gateway configuration section defined in Twin desired properties or local settings");
                Console.WriteLine("Configuration must contain required section " + GatewayController.GatewayConfigSection);
            }
            else
            {
                Console.WriteLine("Attempt to parse configuration");
                GatewayModel config = JsonConvert.DeserializeObject<GatewayModel>(serializedStr);                
                                               
                if (config != null)
                {
                    GatewayController controller = new GatewayController(config);
                    var userContext = new Tuple<ModuleClient, GatewayController>(ioTHubModuleClient, controller);
                    await GatewayController.Start(userContext, cts.Token);
                    
                    
                    // Register callback to be called when a message is received by the module
                    await ioTHubModuleClient.SetInputMessageHandlerAsync(
                    "gatewayInput",
                    PipeMessage,
                    userContext);

                    Console.WriteLine("Remote Monitoring Gateway module was loaded sucessfully and listining for driveProfile messages.");
                }
                else
                {
                    Console.WriteLine("Error creating drive profile message handler. Message processing stopped.");
                }

            }

        }
    }    
}
