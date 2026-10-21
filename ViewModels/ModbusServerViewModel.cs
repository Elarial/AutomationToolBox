using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentModbus;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using AutomationTools.Services;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// ViewModel for the Modbus Server tool. Handles running a local Modbus TCP server and simulating data.
    /// </summary>
    public partial class ModbusServerViewModel : ViewModelBase, IToolViewModel
    {
        private static int _nextPort = 501; // 502 for the first actual tool
        private ModbusTcpServer? _server;
        private bool _isUpdatingFromServer;

        /// <summary>
        /// Initializes a new instance of the ModbusServerViewModel class.
        /// </summary>
        public ModbusServerViewModel()
        {
            Port = _nextPort++;
            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));
            UpdateGrid();
        }

        public string Name => TranslationSource.Instance["ModbusServerName"] ?? "Modbus Server";
        public string IconKind => "ServerNetwork";
        public double DefaultWidth => 700;
        public double DefaultHeight => 800;

        public event System.EventHandler<double>? WidthChangedRequest;

        /// <summary>
        /// Cleans up resources and stops the server when the tool is closed.
        /// </summary>
        public void Cleanup()
        {
            if (IsRunning)
            {
                StopServer();
            }
        }

        [ObservableProperty]
        private int _port = 502;

        [ObservableProperty]
        private byte _slaveId = 1;

        public Array DisplayModes => Enum.GetValues(typeof(ModbusDisplayMode));
        public Array WordOrders => Enum.GetValues(typeof(ModbusDataFormatter.WordOrder));
        public Array NumericFormats => Enum.GetValues(typeof(ModbusDataFormatter.NumericFormat));

        [ObservableProperty]
        private ModbusDisplayMode _selectedDisplayMode = ModbusDisplayMode.Word16;

        [ObservableProperty]
        private ModbusDataFormatter.WordOrder _selectedWordOrder = ModbusDataFormatter.WordOrder.BigEndian;

        [ObservableProperty]
        private ModbusDataFormatter.NumericFormat _selectedNumericFormat = ModbusDataFormatter.NumericFormat.Decimal;

        partial void OnSelectedDisplayModeChanged(ModbusDisplayMode value) => UpdateGrid();
        partial void OnSelectedWordOrderChanged(ModbusDataFormatter.WordOrder value) => UpdateGrid();
        partial void OnSelectedNumericFormatChanged(ModbusDataFormatter.NumericFormat value) => UpdateGrid();

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private ModbusRegisterType _selectedRegisterType = ModbusRegisterType.HoldingRegister;

        [ObservableProperty]
        private int _startAddress = 0;

        [ObservableProperty]
        private int _registerCount = 10;

        public ObservableCollection<ModbusRegister> Registers { get; } = new ObservableCollection<ModbusRegister>();

        [ObservableProperty]
        private bool _isDebugPanelOpen;

        partial void OnIsDebugPanelOpenChanged(bool value)
        {
            WidthChangedRequest?.Invoke(this, value ? 280 : -280);
        }

        public ObservableCollection<ModbusLogMessage> Logs { get; } = new ObservableCollection<ModbusLogMessage>();

        /// <summary>
        /// Toggles the visibility of the debug panel.
        /// </summary>
        [RelayCommand]
        private void ToggleDebugPanel() => IsDebugPanelOpen = !IsDebugPanelOpen;

        /// <summary>
        /// Clears the communication logs.
        /// </summary>
        [RelayCommand]
        private void ClearLogs() => Logs.Clear();

        /// <summary>
        /// Logs a Modbus frame to the debug panel.
        /// </summary>
        private void LogFrame(bool isSent, byte unit, byte fCode, byte[] data)
        {
            int len = 2 + data.Length;
            byte[] frame = new byte[6 + len];
            frame[4] = (byte)(len >> 8); frame[5] = (byte)len;
            frame[6] = unit;
            frame[7] = fCode;
            Array.Copy(data, 0, frame, 8, data.Length);
            string hex = BitConverter.ToString(frame).Replace("-", " ");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                Logs.Add(new ModbusLogMessage { IsSent = isSent, Payload = hex });
                if (Logs.Count > 100) Logs.RemoveAt(0);
            });
        }

        public Array RegisterTypes => Enum.GetValues(typeof(ModbusRegisterType));

        partial void OnSelectedRegisterTypeChanged(ModbusRegisterType value) => UpdateGrid();
        partial void OnStartAddressChanged(int value) => UpdateGrid();
        partial void OnRegisterCountChanged(int value) => UpdateGrid();

        /// <summary>
        /// Updates the displayed registers grid based on the current configuration.
        /// </summary>
        private void UpdateGrid()
        {
            if (!IsRunning)
            {
                Registers.Clear();
                return;
            }

            if (RegisterCount < 1) RegisterCount = 1;
            if (RegisterCount > 1000) RegisterCount = 1000; // Limit rendering
            if (StartAddress < 0) StartAddress = 0;

            var newRegisters = new System.Collections.Generic.List<ModbusRegister>();

            if (SelectedRegisterType == ModbusRegisterType.HoldingRegister || SelectedRegisterType == ModbusRegisterType.InputRegister)
            {
                short[] dummyMemory = new short[RegisterCount];
                if (_server != null)
                {
                    var span = SelectedRegisterType == ModbusRegisterType.HoldingRegister ? _server.GetHoldingRegisters(SlaveId) : _server.GetInputRegisters(SlaveId);
                    for (int i = 0; i < RegisterCount; i++)
                    {
                        if (StartAddress + i < span.Length) dummyMemory[i] = span[StartAddress + i];
                    }
                }

                var formatted = ModbusDataFormatter.FormatRegisters(dummyMemory, StartAddress, SelectedDisplayMode, SelectedWordOrder, SelectedNumericFormat);
                foreach (var reg in formatted)
                {
                    reg.Type = SelectedRegisterType;
                    newRegisters.Add(reg);
                }
            }
            else
            {
                for (int i = 0; i < RegisterCount; i++)
                {
                    var reg = new ModbusRegister { Address = StartAddress + i, Type = SelectedRegisterType };
                    if (_server != null)
                    {
                        reg.Value = ReadMemory(reg.Address, reg.Type);
                    }
                    newRegisters.Add(reg);
                }
            }

            UpdateRegisters(newRegisters, SelectedRegisterType, StartAddress);
        }

        private void UpdateRegisters(System.Collections.Generic.IEnumerable<ModbusRegister> newRegisters, ModbusRegisterType type, int startAddress)
        {
            var newList = System.Linq.Enumerable.ToList(newRegisters);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Registers.Count != newList.Count || Registers.Count == 0 || Registers[0].Type != type || Registers[0].Address != startAddress)
                {
                    foreach (var r in Registers) r.PropertyChanged -= OnRegisterPropertyChanged;
                    Registers.Clear();
                    foreach (var reg in newList)
                    {
                        reg.Type = type;
                        reg.PropertyChanged += OnRegisterPropertyChanged;
                        Registers.Add(reg);
                    }
                }
                else
                {
                    for (int i = 0; i < newList.Count; i++)
                    {
                        if (Registers[i].Value != newList[i].Value)
                        {
                            Registers[i].PropertyChanged -= OnRegisterPropertyChanged;
                            Registers[i].Value = newList[i].Value;
                            Registers[i].PropertyChanged += OnRegisterPropertyChanged;
                        }
                    }
                }
            });
        }

        private void OnRegisterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_isUpdatingFromServer && e.PropertyName == nameof(ModbusRegister.Value) && sender is ModbusRegister reg)
            {
                WriteMemory(reg.Address, reg.Type, reg.Value);
            }
        }

        /// <summary>
        /// Reads the value of a specific register from the server's memory.
        /// </summary>
        private string ReadMemory(int address, ModbusRegisterType type)
        {
            if (_server == null) return "0";
            try
            {
                switch (type)
                {
                    case ModbusRegisterType.HoldingRegister: return _server.GetHoldingRegisters(SlaveId)[address].ToString();
                    case ModbusRegisterType.InputRegister: return _server.GetInputRegisters(SlaveId)[address].ToString();
                    case ModbusRegisterType.Coil: 
                        byte cb = _server.GetCoils(SlaveId)[address / 8];
                        return ((cb & (1 << (address % 8))) != 0) ? "1" : "0";
                    case ModbusRegisterType.DiscreteInput:
                        byte db = _server.GetDiscreteInputs(SlaveId)[address / 8];
                        return ((db & (1 << (address % 8))) != 0) ? "1" : "0";
                    default: return "0";
                }
            }
            catch { return "0"; }
        }

        /// <summary>
        /// Writes a value to a specific register in the server's memory.
        /// </summary>
        private void WriteMemory(int address, ModbusRegisterType type, string valueStr)
        {
            if (_server == null) return;
            try
            {
                if (type == ModbusRegisterType.HoldingRegister || type == ModbusRegisterType.InputRegister)
                {
                    if (!ModbusDataFormatter.ParseUserInput(valueStr, SelectedDisplayMode, SelectedWordOrder, SelectedNumericFormat, out short[] shorts)) return;

                    var span = type == ModbusRegisterType.HoldingRegister ? _server.GetHoldingRegisters(SlaveId) : _server.GetInputRegisters(SlaveId);
                    
                    for (int i = 0; i < shorts.Length; i++)
                    {
                        if (address + i < span.Length)
                        {
                            span[address + i] = shorts[i];
                        }
                    }
                }
                else
                {
                    bool val = valueStr == "1" || valueStr.ToLower() == "true";
                    var span = type == ModbusRegisterType.Coil ? _server.GetCoils(SlaveId) : _server.GetDiscreteInputs(SlaveId);
                    int byteIdx = address / 8;
                    int bitIdx = address % 8;
                    byte b = span[byteIdx];
                    if (val) b |= (byte)(1 << bitIdx);
                    else b &= (byte)~(1 << bitIdx);
                    span[byteIdx] = b;
                }
            }
            catch { }
        }

        /// <summary>
        /// Starts the Modbus TCP server.
        /// </summary>
        [RelayCommand]
        private void StartServer()
        {
            if (IsRunning) return;
            try
            {
                _server = new FluentModbus.ModbusTcpServer();
                // We add the specific SlaveId to answer correctly.
                _server.AddUnit(SlaveId);
                _server.EnableRaisingEvents = true;
                
                var defaultValidator = _server.RequestValidator;
                _server.RequestValidator = (unit, fCode, addr, qty) =>
                {
                    try
                    {
                        if (IsDebugPanelOpen)
                        {
                            LogFrame(false, unit, (byte)fCode, new byte[] { (byte)(addr >> 8), (byte)addr, (byte)(qty >> 8), (byte)qty });
                        }
                        
                        // Force validation to pass unit check by passing 0 (FluentModbus default)
                        var result = defaultValidator != null ? defaultValidator(0, fCode, addr, qty) : ModbusExceptionCode.OK;
                        
                        if (IsDebugPanelOpen)
                        {
                            SynthesizeTxResponse(unit, (byte)fCode, addr, qty, result);
                        }
                        return result;
                    }
                    catch
                    {
                        return (FluentModbus.ModbusExceptionCode)4; // Slave/Server Device Failure
                    }
                };

                _server.RegistersChanged += (s, e) => System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshGridValues);
                _server.CoilsChanged += (s, e) => System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshGridValues);
                _server.Start(new IPEndPoint(IPAddress.Any, Port));
                IsRunning = true;
                StructuredPopulateCommand.NotifyCanExecuteChanged();
                RandomPopulateCommand.NotifyCanExecuteChanged();
                ClearPopulateCommand.NotifyCanExecuteChanged();
                UpdateGrid(); 
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error Starting Server");
            }
        }

        /// <summary>
        /// Stops the Modbus TCP server.
        /// </summary>
        [RelayCommand]
        private void StopServer()
        {
            if (!IsRunning) return;
            _server?.Stop();
            _server = null;
            IsRunning = false;
            StructuredPopulateCommand.NotifyCanExecuteChanged();
            RandomPopulateCommand.NotifyCanExecuteChanged();
            ClearPopulateCommand.NotifyCanExecuteChanged();
            UpdateGrid();
        }

        /// <summary>
        /// Populates the registers with structured sequential data.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void StructuredPopulate()
        {
            foreach (var reg in Registers)
            {
                reg.Value = (reg.Type == ModbusRegisterType.Coil || reg.Type == ModbusRegisterType.DiscreteInput) 
                    ? ((reg.Address % 2 == 0) ? "1" : "0") 
                    : (reg.Address % 32768).ToString();
            }
        }

        /// <summary>
        /// Populates the registers with random data.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void RandomPopulate()
        {
            var rand = new Random();
            foreach (var reg in Registers)
            {
                reg.Value = (reg.Type == ModbusRegisterType.Coil || reg.Type == ModbusRegisterType.DiscreteInput) 
                    ? rand.Next(2).ToString() 
                    : rand.Next(short.MinValue, short.MaxValue + 1).ToString();
            }
        }

        /// <summary>
        /// Refreshes the grid values with the latest data from the server's memory.
        /// </summary>
        private void RefreshGridValues()
        {
            if (_server == null) return;
            _isUpdatingFromServer = true;

            if (SelectedRegisterType == ModbusRegisterType.HoldingRegister || SelectedRegisterType == ModbusRegisterType.InputRegister)
            {
                short[] dummyMemory = new short[RegisterCount];
                var span = SelectedRegisterType == ModbusRegisterType.HoldingRegister ? _server.GetHoldingRegisters(SlaveId) : _server.GetInputRegisters(SlaveId);
                for (int i = 0; i < RegisterCount; i++)
                {
                    if (StartAddress + i < span.Length) dummyMemory[i] = span[StartAddress + i];
                }

                var formatted = ModbusDataFormatter.FormatRegisters(dummyMemory, StartAddress, SelectedDisplayMode, SelectedWordOrder, SelectedNumericFormat);
                
                int idx = 0;
                foreach (var fReg in formatted)
                {
                    if (idx < Registers.Count)
                    {
                        if (Registers[idx].Value != fReg.Value)
                            Registers[idx].Value = fReg.Value;
                    }
                    idx++;
                }
            }
            else
            {
                foreach(var reg in Registers)
                {
                    string newVal = ReadMemory(reg.Address, reg.Type);
                    if (reg.Value != newVal)
                    {
                        reg.Value = newVal;
                    }
                }
            }

            _isUpdatingFromServer = false;
        }

        /// <summary>
        /// Clears all populated register values, setting them to zero.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void ClearPopulate()
        {
            foreach (var reg in Registers)
            {
                reg.Value = "0";
            }
        }

        /// <summary>
        /// Synthesizes and logs a Modbus transmission response for debugging.
        /// </summary>
        private void SynthesizeTxResponse(byte unit, byte fCode, ushort addr, ushort qty, FluentModbus.ModbusExceptionCode exceptionCode)
        {
            if (exceptionCode != FluentModbus.ModbusExceptionCode.OK)
            {
                LogFrame(true, unit, (byte)(fCode | 0x80), new byte[] { (byte)exceptionCode });
                return;
            }

            var payload = new System.Collections.Generic.List<byte>();
            
            if (fCode == 1 || fCode == 2)
            {
                byte byteCount = (byte)((qty + 7) / 8);
                payload.Add(byteCount);
                
                var span = fCode == 1 ? _server!.GetCoils(SlaveId) : _server!.GetDiscreteInputs(SlaveId);
                for (int i = 0; i < byteCount; i++)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int index = addr + (i * 8) + bit;
                        if (index < addr + qty)
                        {
                            int spanByteIdx = index / 8;
                            int spanBitIdx = index % 8;
                            if (spanByteIdx < span.Length)
                            {
                                if ((span[spanByteIdx] & (1 << spanBitIdx)) != 0)
                                {
                                    b |= (byte)(1 << bit);
                                }
                            }
                        }
                    }
                    payload.Add(b);
                }
                LogFrame(true, unit, fCode, payload.ToArray());
            }
            else if (fCode == 3 || fCode == 4)
            {
                byte byteCount = (byte)(qty * 2);
                payload.Add(byteCount);
                
                var span = fCode == 3 ? _server!.GetHoldingRegisters(SlaveId) : _server!.GetInputRegisters(SlaveId);
                for (int i = 0; i < qty; i++)
                {
                    int index = addr + i;
                    if (index < span.Length)
                    {
                        short val = span[index];
                        payload.Add((byte)(val >> 8));
                        payload.Add((byte)(val & 0xFF));
                    }
                    else
                    {
                        payload.Add(0);
                        payload.Add(0);
                    }
                }
                LogFrame(true, unit, fCode, payload.ToArray());
            }
            else if (fCode == 5 || fCode == 6 || fCode == 15 || fCode == 16)
            {
                payload.Add((byte)(addr >> 8));
                payload.Add((byte)(addr & 0xFF));
                payload.Add((byte)(qty >> 8));
                payload.Add((byte)(qty & 0xFF));
                LogFrame(true, unit, fCode, payload.ToArray());
            }
        }
    }
}
