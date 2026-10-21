using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using AutomationTools.Models.Persistence;
using System.Linq;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// Represents a workspace that contains and manages a collection of active tools.
    /// </summary>
    public partial class WorkspaceViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _name;

        public ObservableCollection<ToolWrapperViewModel> ActiveTools { get; } = new();

        private int _highestZIndex = 0;

        /// <summary>
        /// Initializes a new instance of the WorkspaceViewModel class.
        /// </summary>
        public WorkspaceViewModel(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Adds a new instance of the specified tool template to the workspace.
        /// </summary>
        public void AddTool(IToolViewModel templateTool)
        {
            if (templateTool == null) return;
            
            var newTool = (IToolViewModel)System.Activator.CreateInstance(templateTool.GetType())!;
            
            var wrapper = new ToolWrapperViewModel(newTool, CloseTool, FocusTool)
            {
                X = 50 + (ActiveTools.Count * 30),
                Y = 50 + (ActiveTools.Count * 30),
                ZIndex = ++_highestZIndex
            };

            ActiveTools.Add(wrapper);
        }

        /// <summary>
        /// Closes the specified tool and removes it from the workspace.
        /// </summary>
        private void CloseTool(ToolWrapperViewModel wrapper)
        {
            wrapper.Content.Cleanup();
            ActiveTools.Remove(wrapper);
        }

        /// <summary>
        /// Brings the specified tool to the front by updating its Z-index.
        /// </summary>
        private void FocusTool(ToolWrapperViewModel wrapper)
        {
            wrapper.ZIndex = ++_highestZIndex;
        }

        /// <summary>
        /// Cleans up and closes all tools in the workspace.
        /// </summary>
        public void CloseWorkspace()
        {
            foreach (var wrapper in ActiveTools)
            {
                wrapper.Content.Cleanup();
            }
            ActiveTools.Clear();
        }

        public WorkspaceDto ToDto()
        {
            var wsDto = new WorkspaceDto { Name = this.Name };
            foreach (var tool in ActiveTools)
            {
                ToolStateDto? dto = null;
                if (tool.Content is PortTesterViewModel pt)
                {
                    dto = new PortTesterStateDto { TargetIp = pt.InputTargetIp, TargetPorts = pt.InputTargetPorts };
                }
                else if (tool.Content is PingToolViewModel p)
                {
                    dto = new PingToolStateDto { Target = p.InputTarget };
                }
                else if (tool.Content is ModbusServerViewModel ms)
                {
                    dto = new ModbusServerStateDto {
                        Port = ms.Port, SlaveId = ms.SlaveId, DisplayMode = ms.SelectedDisplayMode, WordOrder = ms.SelectedWordOrder, NumericFormat = ms.SelectedNumericFormat, RegisterType = ms.SelectedRegisterType, StartAddress = ms.StartAddress, RegisterCount = ms.RegisterCount
                    };
                }
                else if (tool.Content is ModbusClientViewModel mc)
                {
                    dto = new ModbusClientStateDto {
                        IpAddress = mc.IpAddress, Port = mc.Port, SlaveId = mc.SlaveId, DisplayMode = mc.SelectedDisplayMode, WordOrder = mc.SelectedWordOrder, NumericFormat = mc.SelectedNumericFormat, RegisterType = mc.SelectedRegisterType, StartAddress = mc.StartAddress, RegisterCount = mc.RegisterCount, PollingDelay = mc.PollingDelay
                    };
                }
                else if (tool.Content is DataConverterViewModel dc)
                {
                    dto = new DataConverterStateDto {
                        SelectedInputFormat = dc.SelectedInputFormat, InputValue = dc.InputValue
                    };
                }
                else if (tool.Content is ChartingToolViewModel ct)
                {
                    dto = new ChartingToolStateDto();
                }
                else if (tool.Content is PlcIoScalingToolViewModel plc)
                {
                    dto = new PlcIoScalingToolStateDto
                    {
                        SelectedPresetName = plc.SelectedPlcPreset?.Name ?? "",
                        SelectedSignalName = plc.SelectedSignalType?.Name ?? "",
                        PlcMin = plc.PlcMin,
                        PlcMax = plc.PlcMax,
                        EngMin = plc.EngMin,
                        EngMax = plc.EngMax
                    };
                }
                else if (tool.Content is OpcUaClientViewModel opc)
                {
                    dto = new OpcUaClientStateDto
                    {
                        EndpointUrl = opc.EndpointUrl,
                        Watchlist = opc.Watchlist.Select(w => new OpcWatchItemDto { DisplayName = w.DisplayName, NodeIdString = w.NodeIdString }).ToList()
                    };
                }

                if (dto != null)
                {
                    dto.X = tool.X;
                    dto.Y = tool.Y;
                    dto.Width = tool.Width;
                    dto.Height = tool.Height;
                    dto.ZIndex = tool.ZIndex;
                    wsDto.Tools.Add(dto);
                }
            }
            return wsDto;
        }

        public void LoadFromDto(WorkspaceDto dto)
        {
            ActiveTools.Clear();
            _highestZIndex = 0;

            foreach (var toolDto in dto.Tools)
            {
                IToolViewModel? content = null;
                if (toolDto is PortTesterStateDto pt)
                {
                    content = new PortTesterViewModel { InputTargetIp = pt.TargetIp, InputTargetPorts = pt.TargetPorts };
                }
                else if (toolDto is PingToolStateDto ping)
                {
                    content = new PingToolViewModel { InputTarget = ping.Target };
                }
                else if (toolDto is ModbusServerStateDto ms)
                {
                    content = new ModbusServerViewModel { Port = ms.Port, SlaveId = ms.SlaveId, SelectedDisplayMode = ms.DisplayMode, SelectedWordOrder = ms.WordOrder, SelectedNumericFormat = ms.NumericFormat, SelectedRegisterType = ms.RegisterType, StartAddress = ms.StartAddress, RegisterCount = ms.RegisterCount };
                }
                else if (toolDto is ModbusClientStateDto mc)
                {
                    content = new ModbusClientViewModel { IpAddress = mc.IpAddress, Port = mc.Port, SlaveId = mc.SlaveId, SelectedDisplayMode = mc.DisplayMode, SelectedWordOrder = mc.WordOrder, SelectedNumericFormat = mc.NumericFormat, SelectedRegisterType = mc.RegisterType, StartAddress = mc.StartAddress, RegisterCount = mc.RegisterCount, PollingDelay = mc.PollingDelay };
                }
                else if (toolDto is DataConverterStateDto dc)
                {
                    content = new DataConverterViewModel { SelectedInputFormat = dc.SelectedInputFormat, InputValue = dc.InputValue };
                }
                else if (toolDto is ChartingToolStateDto ct)
                {
                    content = new ChartingToolViewModel();
                }
                else if (toolDto is PlcIoScalingToolStateDto plcDto)
                {
                    var plcViewModel = new PlcIoScalingToolViewModel();
                    
                    var signal = plcViewModel.SignalTypes.FirstOrDefault(s => s.Name == plcDto.SelectedSignalName);
                    if (signal != null) plcViewModel.SelectedSignalType = signal;

                    var preset = plcViewModel.PlcPresets.FirstOrDefault(p => p.Name == plcDto.SelectedPresetName);
                    if (preset != null) plcViewModel.SelectedPlcPreset = preset;

                    plcViewModel.PlcMin = plcDto.PlcMin;
                    plcViewModel.PlcMax = plcDto.PlcMax;
                    plcViewModel.EngMin = plcDto.EngMin;
                    plcViewModel.EngMax = plcDto.EngMax;
                    
                    content = plcViewModel;
                }
                else if (toolDto is OpcUaClientStateDto opcDto)
                {
                    var opcViewModel = new OpcUaClientViewModel { EndpointUrl = opcDto.EndpointUrl };
                    foreach (var watchDto in opcDto.Watchlist)
                    {
                        try
                        {
                            opcViewModel.Watchlist.Add(new OpcWatchItem
                            {
                                DisplayName = watchDto.DisplayName,
                                NodeIdString = watchDto.NodeIdString,
                                OpcNodeId = Opc.Ua.NodeId.Parse(watchDto.NodeIdString)
                            });
                        }
                        catch { }
                    }
                    content = opcViewModel;
                }
                
                if (content != null)
                {
                    var wrapper = new ToolWrapperViewModel(content, CloseTool, FocusTool)
                    {
                        X = toolDto.X,
                        Y = toolDto.Y,
                        Width = toolDto.Width,
                        Height = toolDto.Height,
                        ZIndex = toolDto.ZIndex
                    };
                    ActiveTools.Add(wrapper);
                    if (toolDto.ZIndex > _highestZIndex)
                    {
                        _highestZIndex = toolDto.ZIndex;
                    }
                }
            }
        }
    }
}
