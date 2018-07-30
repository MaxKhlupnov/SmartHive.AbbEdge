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
    [DataContract]
    public class DeviceProperties
    {
        [DataMember]
        internal string DeviceID;

        [DataMember]
        internal bool HubEnabledState = true;

        [DataMember(EmitDefaultValue =false)]
        internal string CreatedTime;

        [DataMember]
        internal string DeviceState = "normal";

        [DataMember(EmitDefaultValue = false)]
        internal string UpdatedTime;

        [DataMember(EmitDefaultValue = false)]
        internal string Manufacturer;

        [DataMember(EmitDefaultValue = false)]
        internal string ModelNumber;

        [DataMember(EmitDefaultValue = false)]
        internal string SerialNumber;

        [DataMember(EmitDefaultValue = false)]
        internal string FirmwareVersion;

        [DataMember(EmitDefaultValue = false)]
        internal string Platform;

        [DataMember(EmitDefaultValue = false)]
        internal string Processor;

        [DataMember(EmitDefaultValue = false)]
        internal string InstalledRAM;

        [DataMember]
        internal double Latitude= 47.6603;

        [DataMember]
        internal double Longitude= -122.1405;

    }

    [DataContract]
    public class CommandParameter
    {
        [DataMember]
        public string Name;

        [DataMember]
        public string Type;
    }

    [DataContract]
    public class Command
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public Collection<CommandParameter> Parameters = new Collection<CommandParameter>();
    }

    [DataContract]
    public class ReceivedMessage
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string MessageId { get; set; }
        [DataMember]
        public string CreatedTime { get; set; }
        [DataMember]
        public Dictionary<string, object> Parameters { get; set; }
    }


    [DataContract]
    public class TelemetryFormat
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string Type { get; set; }
    }

    [DataContract]
    public class DeviceModel
    {
        [DataMember]
        public DeviceProperties DeviceProperties { get; set; } = new DeviceProperties();

        [DataMember]
        internal Collection<Command> Commands = new Collection<Command>();

        [DataMember]
        internal Collection<TelemetryFormat> Telemetry = new Collection<TelemetryFormat>();

        [DataMember]
        internal bool IsSimulatedDevice = false;

        [DataMember]
        internal string Version = "1.0";

        [DataMember]
        internal string ObjectType = "DeviceInfo";
    }
    #endregion
}