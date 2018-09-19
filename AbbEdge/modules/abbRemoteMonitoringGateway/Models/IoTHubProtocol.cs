using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace abbRemoteMonitoringGateway.Models
{
     [JsonConverter(typeof(StringEnumConverter))]
    public enum IoTHubProtocol
        {
         
            [EnumMember(Value = "AMQP")]
            AMQP = 1,
            [EnumMember(Value = "MQTT")]
            MQTT = 2,
            [EnumMember(Value = "HTTP")]
            HTTP = 3
        }
 }