namespace abbDriveProfile
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    class Program
    {
        // Drive profile configuration section starting tag in the module Twin JSON
        
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

            Console.WriteLine("Drive profile module - Received message");
            int counterValue = Interlocked.Increment(ref counter);

            var userContextValues = userContext as Tuple<ModuleClient, ModuleMessageHandler>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }

            var moduleClient = userContextValues.Item1;            
            var messageHandler = userContextValues.Item2;

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
               
                List<SignalTelemetry> telemetryData = messageHandler.ProcessModBusMessage(messageString);
                if (telemetryData.Count > 0){
                    string serializedStr = JsonConvert.SerializeObject(telemetryData);
                    messageBytes = Encoding.ASCII.GetBytes(serializedStr);

                    Message  pipeMessage =  new Message(messageBytes);
                    pipeMessage .Properties.Add("content-type", "application/edge-modbus-json");
                 
                    await moduleClient.SendEventAsync("driveProfileOutput", pipeMessage);
                    Console.WriteLine("Telemetry message sent");
                }
            }
            else
            {
                throw new InvalidOperationException("Error: Message Body is empty");
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
                "driveProfileInput",
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
            else if (!desiredProperties.Contains(DriveProfileConfig.DriveProfileConfigSection))
            {
                Console.WriteLine("No driverProfile configuration defined in Twin desired properties or local settings");
                Console.WriteLine("Configuration must contain required section " + DriveProfileConfig.DriveProfileConfigSection);
            }
            else
            {
                Console.WriteLine("Attempt to parse configuration");
                DriveProfileConfig config = JsonConvert.DeserializeObject<DriveProfileConfig>(serializedStr);


                ModuleMessageHandler moduleHandle = ModuleMessageHandler.CreateFromConfig(config);

                if (moduleHandle != null)
                {
                    var userContext = new Tuple<ModuleClient, ModuleMessageHandler>(ioTHubModuleClient, moduleHandle);
                    // Register callback to be called when a message is received by the module
                    await ioTHubModuleClient.SetInputMessageHandlerAsync(
                    "driveProfileInput",
                    PipeMessage,
                    userContext);
                    Console.WriteLine("DriverProfile module was loaded sucessfully and listining for modbus messages.");
                }
                else
                {
                    Console.WriteLine("Error creating modbus message handler. Message processing stopped.");
                }

            }

        }
    }
}

