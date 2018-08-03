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
        public const string InputName = "gatewayInput";
        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            CancellationTokenSource cts = new CancellationTokenSource();
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

            // Read Module Twin Desired Properties
            Console.WriteLine("Reading module Twin from IoT Hub.");
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();

            CancellationTokenSource cts = new CancellationTokenSource();

            // Read Module Twin Desired Properties       
            Console.WriteLine("Starting Gateway controller handler process.");
            GatewayController controller = await GatewayController.Start(ioTHubModuleClient, moduleTwin, cts.Token);
            var userContext = new Tuple<ModuleClient, GatewayController>(ioTHubModuleClient, controller);
            
            // Register callback to be called when a message is received by the module
            Console.WriteLine(String.Format("Registreing call back for  input named {0}.", InputName));
            await ioTHubModuleClient.SetInputMessageHandlerAsync(InputName, PipeMessage,  userContext);           
            
            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, userContext);

            Console.WriteLine("Setting up cancelation for controler handler process.");
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();           
            
            Console.WriteLine("Gateway controller handler Initialization sucessfull.");
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

            if (gatewayHandle.GatewayConfig == null){
                Console.WriteLine("Module configuration is empty. Message processing terminated");
                return MessageResponse.Abandoned;             
            }else if (gatewayHandle.GatewayConfig.ReportedTelemetry == null){
                Console.WriteLine("No telemetry signals configured for remote monitoring. Message processing terminated");
                 return MessageResponse.Abandoned;   
            }

            int counterValue = Interlocked.Increment(ref counter);
            
            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);           

            if (!string.IsNullOrEmpty(messageString))
            {
                 Console.WriteLine($"Parsing message: {counterValue}, Body: [{messageString}]");
              
                 List<SignalTelemetry> telemetryData = JsonConvert.DeserializeObject<List<SignalTelemetry>>(messageString);                
                 if (telemetryData.Count > 0){
                    await gatewayHandle.ProcessAbbDriveProfileTelemetry(telemetryData);
                 }               
                
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
           var userContextValues  = userContext as Tuple<ModuleClient, GatewayController>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }
                ModuleClient ioTHubModuleClient = userContextValues.Item1;
                GatewayController gatewayHandle = userContextValues.Item2;

             try
            {
                // stop all activities while updating configuration
                await ioTHubModuleClient.SetInputMessageHandlerAsync( InputName, DummyCallBack, null);

                GatewayModel updateModel = GatewayController.CreateGatewayModel(desiredProperties);
                 
                 gatewayHandle.UpdateGatewayModel(updateModel);

                // restore message handling
                await ioTHubModuleClient.SetInputMessageHandlerAsync(InputName, PipeMessage,  userContext); 

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
    }    
}
