using System.Collections.Generic;
using System.Text.Json.Serialization;
using AutomationTools.ViewModels;

namespace AutomationTools.Models.Persistence
{
    public class AppStateDto
    {
        public List<WorkspaceDto> Workspaces { get; set; } = new();
        public int SelectedWorkspaceIndex { get; set; }
    }

    public class WorkspaceDto
    {
        public string Name { get; set; } = string.Empty;
        public List<ToolStateDto> Tools { get; set; } = new();
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(PortTesterStateDto), typeDiscriminator: "PortTester")]
    [JsonDerivedType(typeof(PingToolStateDto), typeDiscriminator: "PingTool")]
    [JsonDerivedType(typeof(ModbusServerStateDto), typeDiscriminator: "ModbusServer")]
    [JsonDerivedType(typeof(ModbusClientStateDto), typeDiscriminator: "ModbusClient")]
    [JsonDerivedType(typeof(DataConverterStateDto), typeDiscriminator: "DataConverter")]
    [JsonDerivedType(typeof(ChartingToolStateDto), typeDiscriminator: "ChartingTool")]
    [JsonDerivedType(typeof(PlcIoScalingToolStateDto), typeDiscriminator: "PlcIoScalingTool")]
    [JsonDerivedType(typeof(OpcUaClientStateDto), typeDiscriminator: "OpcUaClient")]
    public abstract class ToolStateDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int ZIndex { get; set; }
    }

    public class PortTesterStateDto : ToolStateDto
    {
        public string TargetIp { get; set; } = string.Empty;
        public string TargetPorts { get; set; } = string.Empty;
    }

    public class PingToolStateDto : ToolStateDto
    {
        public string Target { get; set; } = string.Empty;
    }

    public class ModbusServerStateDto : ToolStateDto
    {
        public int Port { get; set; }
        public byte SlaveId { get; set; }
        public ModbusDisplayMode DisplayMode { get; set; }
        public ModbusDataFormatter.WordOrder WordOrder { get; set; }
        public ModbusDataFormatter.NumericFormat NumericFormat { get; set; }
        public ModbusRegisterType RegisterType { get; set; }
        public int StartAddress { get; set; }
        public int RegisterCount { get; set; }
    }

    public class ModbusClientStateDto : ToolStateDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public byte SlaveId { get; set; }
        public ModbusDisplayMode DisplayMode { get; set; }
        public ModbusDataFormatter.WordOrder WordOrder { get; set; }
        public ModbusDataFormatter.NumericFormat NumericFormat { get; set; }
        public ModbusRegisterType RegisterType { get; set; }
        public int StartAddress { get; set; }
        public int RegisterCount { get; set; }
        public int PollingDelay { get; set; }
    }

    public class DataConverterStateDto : ToolStateDto
    {
        public DataFormat SelectedInputFormat { get; set; }
        public string InputValue { get; set; } = string.Empty;
    }

    public class ChartingToolStateDto : ToolStateDto
    {
    }

    public class PlcIoScalingToolStateDto : ToolStateDto
    {
        public string SelectedPresetName { get; set; } = string.Empty;
        public string SelectedSignalName { get; set; } = string.Empty;
        public double PlcMin { get; set; }
        public double PlcMax { get; set; }
        public double EngMin { get; set; }
        public double EngMax { get; set; }
    }

    public class OpcUaClientStateDto : ToolStateDto
    {
        public string EndpointUrl { get; set; } = string.Empty;
        public List<OpcWatchItemDto> Watchlist { get; set; } = new();
    }

    public class OpcWatchItemDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string NodeIdString { get; set; } = string.Empty;
    }
}
