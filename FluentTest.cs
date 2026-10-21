using System;
using System.Net;
using FluentModbus;

namespace AutomationTools
{
    class FluentTest
    {
        public static void Test()
        {
            var server = new ModbusTcpServer();
            server.Start(new IPEndPoint(IPAddress.Loopback, 502));
            var hr = server.GetHoldingRegisters(); // Memory<short>
            var ir = server.GetInputRegisters(); // Memory<short>
            var co = server.GetCoils(); // Memory<byte>
            var di = server.GetDiscreteInputs(); // Memory<byte>

            // Coils are bits in bytes.
            byte b = co[0];
            co[0] = (byte)(b | 1);
        }
    }
}
