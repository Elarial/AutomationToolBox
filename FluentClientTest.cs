using System;
using System.Net;
using FluentModbus;

namespace AutomationTools
{
    class FluentClientTest
    {
        public static void Test()
        {
            var client = new ModbusTcpClient();
            client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 502));
            
            // Read
            var hr = client.ReadHoldingRegisters<short>(1, 0, 10);
            var ir = client.ReadInputRegisters<short>(1, 0, 10);
            var co = client.ReadCoils(1, 0, 10);
            var di = client.ReadDiscreteInputs(1, 0, 10);

            // Write
            short[] shortArray = { 1, 2, 3 };
            client.WriteMultipleRegisters(1, 0, shortArray);
            
            bool[] boolArray = { true, false, true };
            client.WriteMultipleCoils(1, 0, boolArray);
            
            client.Disconnect();
        }
    }
}
