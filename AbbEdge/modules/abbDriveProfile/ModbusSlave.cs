namespace Modbus.Slaves
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.IO.Ports;
    using System.Runtime.InteropServices;

    /* Modbus Frame Details
     ----------------------- --------
    |MBAP Header description|Length  |
     ----------------------- --------
    |Transaction Identifier |2 bytes |
     ----------------------- --------
    |Protocol Identifier    |2 bytes |
     ----------------------- --------
    |Length 2bytes          |2 bytes |
     ----------------------- --------
    |Unit Identifier        |1 byte  |
     ----------------------- --------
    |Body                   |variable|
     ----------------------- --------
    */

    
    class ModbusOutContent
    {
        public string HwId { get; set; }
        public List<ModbusOutData> Data { get; set; }
    }

    class ModbusOutData
    {
        public string CorrelationId { get; set; }
        public string SourceTimestamp { get; set; }
        public List<ModbusOutValue> Values { get; set; }
    }
    class ModbusOutValue
    {
        public string DisplayName { get; set; }
        //public string OpName { get; set; }
        public string Address { get; set; }
        public string Value { get; set; }
    }

    class ModbusOutMessage
    {
        public string PublishTimestamp { get; set; }
        public List<ModbusOutContent> Content { get; set; }
    }
}
