namespace abbRemoteMonitoringGateway
{
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

      #region Data Contracts for serializing data

    public class DeviceProperties
    {

        internal string DeviceID;


        internal bool HubEnabledState = true;

       
        internal string CreatedTime;


        internal string DeviceState = "normal";

       
        internal string UpdatedTime;

       
        internal string Manufacturer;

       
        internal string ModelNumber;

       
        internal string SerialNumber;

       
        internal string FirmwareVersion;

       
        internal string Platform;

       
        internal string Processor;

       
        internal string InstalledRAM;


        internal double Latitude= 47.6603;


        internal double Longitude= -122.1405;

    }


    public class CommandParameter
    {

        public string Name;


        public string Type;
    }


    public class Command
    {

        public string Name { get; set; }


        public Collection<CommandParameter> Parameters = new Collection<CommandParameter>();
    }


    public class ReceivedMessage
    {

        public string Name { get; set; }

        public string MessageId { get; set; }

        public string CreatedTime { get; set; }

        public Dictionary<string, object> Parameters { get; set; }
    }


    
    public class TelemetryFormat : IEquatable<TelemetryFormat>
    {
       
        public string Name { get; set; }

       
        public string DisplayName { get; set; }

        public string Type { get; set; }

        public bool Equals(TelemetryFormat telemetry)
        {
            return null != telemetry && Name.Equals(telemetry.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TelemetryFormat);
        }

        public override int GetHashCode()
        {            
                return this.Name.GetHashCode();            
        }
    }

   
    public class DeviceModel
    {       
        public DeviceProperties DeviceProperties { get; set; } = new DeviceProperties();

       /* [DataMember]
        internal Collection<Command> Commands = new Collection<Command>();*/

        internal Collection<TelemetryFormat> Telemetry = new Collection<TelemetryFormat>();


        internal bool IsSimulatedDevice = false;

        internal string Version = "1.0";


        internal string ObjectType = "DeviceInfo";
    }
    #endregion
}