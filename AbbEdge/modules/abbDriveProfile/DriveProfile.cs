namespace abbDriveProfile
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Client;
    using DynamicExpresso;
    using Modbus.Slaves;
    using Newtonsoft.Json;

    /** This class contain handle for the module logic and all signal processors */
    class ModuleMessageHandler{


        private  ModuleMessageHandler(){}
        private List<SignalProcessor> SignalProcessorList = new List<SignalProcessor>();
        public static ModuleMessageHandler CreateFromConfig(DriveProfileConfig config)
        {

            if (config == null || config.SignalConfigs == null)
                    return null;
                    
            ModuleMessageHandler moduleHandle = new ModuleMessageHandler();        

            foreach(string keyName in config.SignalConfigs.Keys){
                DriveProfileSignalConfig signalConfig = config.SignalConfigs[keyName];
                SignalProcessor signalProcessor = new SignalProcessor(keyName, signalConfig);
                moduleHandle.SignalProcessorList.Add(signalProcessor);
            }

            return  moduleHandle;

        }

        public List<SignalTelemetry> ProcessModBusMessage(string ModbusMessageJson){

                List<SignalTelemetry> processingResult = new List<SignalTelemetry>();
                foreach(SignalProcessor processor in SignalProcessorList){
                      
                     SignalTelemetry telemetry = processor.ProcessSignal(ModbusMessageJson);
                     if (telemetry != null)
                            processingResult.Add(telemetry);   
                }
            
            Console.WriteLine(" signals processed: {0}", processingResult.Count);

            return processingResult;
        }
    }

    public class SignalTelemetry{
        public string Name { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; }
        public string ValueUnit { get; set; }
    }

    /** Handler modbus message and calculate signal */
    class SignalProcessor{

        /** Supported signal value type */
        public const string ValueTypeDouble = "Double";
       /* public const string Integer = "Integer";
        public const string String = "String";*/
    
        public DriveProfileSignalConfig config {get; private set;}
        private string[] ParameterNames = null;
        private Interpreter FunctionProcessor = null;
        public string SignalName {get; private set;}
        
        internal SignalProcessor(string signalName, DriveProfileSignalConfig config){
            this.SignalName = signalName;
            this.config = config;

           this.ParameterNames = ExtractParameters(config.ValueFormula);
            if (this.ParameterNames == null || this.ParameterNames.Length == 0)
            {
                Console.WriteLine("No parameters detected in expression {0}. ValueFormat format error.\n Each variable must correspond modbus operation displayname and start with $");
            }
            else
            {
                
                FunctionProcessor = new Interpreter();                
                Console.WriteLine("Function for signal {0} compiled sucessfully", this.SignalName);                
            }            
        }

        /// <summary>
        /// Check if we have all parameters in the message
        /// </summary>
        /// <param name="ModbusMessageJson"></param>
        /// <returns></returns>
        private bool IsRequiredParametersExist(string ModbusMessageJson)
        {
            int score = 0;
            foreach (string param in ParameterNames)
            {
                if (ModbusMessageJson.Contains(param))
                {
                    score++;
                }
                else
                {
                    return false;
                }
            }

            return score == ParameterNames.Length;
        }

        public SignalTelemetry ProcessSignal(string ModbusMessageJson)
        {
            /// Check if we have all parameter values for calculations and ready for calculations
            if (ParameterNames == null || FunctionProcessor == null
                || String.IsNullOrEmpty(ModbusMessageJson))
            {
                Console.WriteLine("Modbus message doesn't contain data");
                return null;
            }
            if (!IsRequiredParametersExist(ModbusMessageJson))
            {
                Console.WriteLine("Modbus message doesn't have all required parameters: {0} to calculate {1} ", ModbusMessageJson, this.SignalName);
                return null;
            }

            ModbusOutMessage message = JsonConvert.DeserializeObject<ModbusOutMessage>(ModbusMessageJson);
            if (message == null || message.Content == null)
            {
                Console.WriteLine("Can't read modbus message {0}.\n ModbusOutContent deserialization failed", ModbusMessageJson);
            }
            else
            {
                // Go throught all content sections
                foreach (ModbusOutContent content in message.Content)
                {
                    int[] paramVal = ExtractParamValues(content);
                    if (paramVal == null || paramVal.Length != this.ParameterNames.Length)
                    {
                        Console.WriteLine("Error extract param values for the message {0} to calculate {1} ", ModbusMessageJson, this.SignalName);
                        return null;
                    }
                    else
                    {
                         Parameter[] Parameters = new Parameter[paramVal.Length];
                        for (int i = 0; i < paramVal.Length; i++)
                        {
                            Parameters[i] = new Parameter(ParameterNames[i], typeof(int), paramVal[i]);
                            Console.WriteLine("{0} : {1}", this.ParameterNames[i], paramVal[i]);
                        }
                       
                         object result = FunctionProcessor.Eval(this.config.ValueFormula, Parameters);

                        if (result != null)
                        {

                            Console.WriteLine("Result: {0}", result);
                            return new SignalTelemetry { Name = this.SignalName, ValueType = this.config.ValueType, 
                                            Value = ValueAsString(result,this.config), ValueUnit = this.config.ValueUnit };
                        }
                    }
                }
                 
                    Console.WriteLine("Result calculation is empty for signal {0}", this.SignalName);
                
            }

            //  processor.ProcessSignalAsDouble(modbusMessage);
            // TODO Calculate signal value
            // TODO: support different return
            /*
            if (ValueTypeDouble.Equals(this.config.ValueType, StringComparison.InvariantCultureIgnoreCase))
            {            }
            else{                    Console.WriteLine($"Unknown signal type: {this.config.ValueType}");                }*/

                return null;
        }
         
        private static string ValueAsString(object value, DriveProfileSignalConfig signalConfig){
            if (value == null)
                throw new ArgumentNullException(String.Format("Calculation value is null for formula {0}", signalConfig.ValueFormula));

            try{
            if (signalConfig.ValueType.Equals("Double", StringComparison.InvariantCultureIgnoreCase))
                  return ((double) value).ToString("#.###");
                        
            }catch(Exception ex){
                Console.WriteLine(String.Format("Error {0} casting formula {1} result as {2}", ex.Message, signalConfig.ValueFormula, signalConfig.ValueType));
            }

            return value.ToString();
        }


        private int[] ExtractParamValues(ModbusOutContent config)
        {
            int[] paramVal = new int[this.ParameterNames.Length];
            for(int i=0; i < paramVal.Length; i++)
            {
                foreach(var m in config.Data)
                {
                    ModbusOutValue modbusVal = m.Values.Find(v => v.DisplayName.Equals(this.ParameterNames[i]));
                    if (modbusVal != null && modbusVal.Value != null)
                    {
                        paramVal[i] = int.Parse(modbusVal.Value);
                        break;
                    }
                }
            }
            return paramVal;
        }

        private static string[] ExtractParameters(string valueFormula)
        {
            // All Alphanumeric started wit $ trated as parameters
            MatchCollection matches = Regex.Matches(valueFormula, "[a-zA-Z]+");
            if (matches.Count > 0)
            {
                string[] retVal = new string[matches.Count];
                for (int i = 0; i < matches.Count; i++)
                {
                    retVal[i] = matches[i].ToString();
                }               
                Console.WriteLine("Found {0} parameters in expression {1}", matches.Count, valueFormula);                
                return retVal;
            }
            else
            {
                return null;
            }
        }
    }

    class DriveProfileConfig{
       internal const string DriveProfileConfigSection = "SignalConfigs";
       /**  File name for storing module stiings on the edge */
       //internal const string DriveProfileConfigFileName = "abb-drive-profile-config.json";
        
        /** Configuration of drive signals (usually actuals) */
       public Dictionary <string, DriveProfileSignalConfig> SignalConfigs;


        public DriveProfileConfig(){}
        public DriveProfileConfig(Dictionary <string, DriveProfileSignalConfig> SignalConfigs){
            this.SignalConfigs = SignalConfigs;
        }

    }


    class DriveProfileSignalConfig{         
            /** Formula for calculating actual value based on modBus registers setting */
            public string ValueFormula {get; set;}
            public string ValueType {get; set;}            
            public string ValueUnit {get;set;}
    }
}