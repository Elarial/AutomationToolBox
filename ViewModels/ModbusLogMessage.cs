using System;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// Represents a log entry for Modbus network traffic (TX/RX frames).
    /// </summary>
    public class ModbusLogMessage
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsSent { get; set; }
        public string Direction => IsSent ? "TX" : "RX";
        public string Payload { get; set; } = string.Empty;
    }
}
