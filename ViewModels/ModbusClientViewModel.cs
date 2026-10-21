using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentModbus;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading.Tasks;
using AutomationTools.Services;
using System.Windows;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// ViewModel for the Modbus Client tool. Handles connecting to Modbus TCP servers and reading/writing data.
    /// </summary>
    public partial class ModbusClientViewModel : ViewModelBase, IToolViewModel
    {
        private ModbusTcpClient? _client;

        /// <summary>
        /// Initializes a new instance of the ModbusClientViewModel class.
        /// </summary>
        public ModbusClientViewModel()
        {
            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));
        }

        public string Name => TranslationSource.Instance["ModbusClientName"] ?? "Modbus Client";
        public string IconKind => "LanConnect";
        public double DefaultWidth => 700;
        public double DefaultHeight => 800;

        public event System.EventHandler<double>? WidthChangedRequest;

        /// <summary>
        /// Cleans up resources and disconnects the client when the tool is closed.
        /// </summary>
        public void Cleanup()
        {
            if (IsConnected)
            {
                DisconnectClient();
            }
        }

        [ObservableProperty]
        private string _ipAddress = "127.0.0.1";

        [ObservableProperty]
        private int _port = 502;

        [ObservableProperty]
        private byte _slaveId = 1;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private ModbusRegisterType _selectedRegisterType = ModbusRegisterType.HoldingRegister;

        public Array DisplayModes => Enum.GetValues(typeof(ModbusDisplayMode));
        public Array WordOrders => Enum.GetValues(typeof(ModbusDataFormatter.WordOrder));
        public Array NumericFormats => Enum.GetValues(typeof(ModbusDataFormatter.NumericFormat));

        [ObservableProperty]
        private ModbusDisplayMode _selectedDisplayMode = ModbusDisplayMode.Word16;

        [ObservableProperty]
        private ModbusDataFormatter.WordOrder _selectedWordOrder = ModbusDataFormatter.WordOrder.BigEndian;

        [ObservableProperty]
        private ModbusDataFormatter.NumericFormat _selectedNumericFormat = ModbusDataFormatter.NumericFormat.Decimal;

        partial void OnSelectedDisplayModeChanged(ModbusDisplayMode value)
        {
            if (IsConnected)
            {
                _ = ReadDataAsync();
            }
        }

        partial void OnSelectedWordOrderChanged(ModbusDataFormatter.WordOrder value)
        {
            if (IsConnected)
            {
                _ = ReadDataAsync();
            }
        }

        partial void OnSelectedNumericFormatChanged(ModbusDataFormatter.NumericFormat value)
        {
            if (IsConnected)
            {
                _ = ReadDataAsync();
            }
        }

        [ObservableProperty]
        private int _startAddress = 0;

        [ObservableProperty]
        private int _registerCount = 10;

        public ObservableCollection<ModbusRegister> Registers { get; } = new ObservableCollection<ModbusRegister>();

        [ObservableProperty]
        private bool _isPolling;

        partial void OnIsPollingChanged(bool value)
        {
            ReadDataCommand.NotifyCanExecuteChanged();
            if (value)
            {
                _ = PollDataAsync();
            }
        }

        [ObservableProperty]
        private int _pollingDelay = 1000;

        /// <summary>
        /// Polls data continuously from the server while connected and polling is enabled.
        /// </summary>
        private async Task PollDataAsync()
        {
            while (IsPolling && IsConnected)
            {
                await ReadDataAsync();
                await Task.Delay(PollingDelay);
            }
            IsPolling = false;
        }

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
            int len = 2 + data.Length; // unitid(1) + fcode(1) + data(n)
            byte[] frame = new byte[6 + len];
            frame[4] = (byte)(len >> 8); frame[5] = (byte)len;
            frame[6] = unit;
            frame[7] = fCode;
            Array.Copy(data, 0, frame, 8, data.Length);
            string hex = BitConverter.ToString(frame).Replace("-", " ");
            Application.Current.Dispatcher.InvokeAsync(() => 
            {
                Logs.Add(new ModbusLogMessage { IsSent = isSent, Payload = hex });
                if (Logs.Count > 100) Logs.RemoveAt(0);
            });
        }
        public Array RegisterTypes => Enum.GetValues(typeof(ModbusRegisterType));

        partial void OnSelectedRegisterTypeChanged(ModbusRegisterType value)
        {
            if (IsConnected)
            {
                _ = ReadDataAsync();
            }
        }

        /// <summary>
        /// Attempts to connect to the specified Modbus TCP server asynchronously.
        /// </summary>
        [RelayCommand]
        private async Task ConnectClientAsync()
        {
            if (IsConnected) return;

            try
            {
                if (!IPAddress.TryParse(IpAddress, out var ip))
                {
                    MessageBox.Show(TranslationSource.Instance["InvalidIpAddress"], TranslationSource.Instance["ConnectionError"] ?? "Error");
                    return;
                }

                _client = new ModbusTcpClient();
                
                // Wrap synchronous Connect in Task.Run to keep UI responsive
                await Task.Run(() => _client.Connect(new IPEndPoint(ip, Port)));
                
                IsConnected = true;
                ReadDataCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _client = null;
                MessageBox.Show(ex.Message, TranslationSource.Instance["ConnectionError"] ?? "Connection Error");
            }
        }

        /// <summary>
        /// Disconnects from the current Modbus TCP server.
        /// </summary>
        [RelayCommand]
        private void DisconnectClient()
        {
            if (!IsConnected) return;

            IsPolling = false;
            _client?.Disconnect();
            _client = null;
            IsConnected = false;
            ReadDataCommand.NotifyCanExecuteChanged();
        }

        public bool CanReadData => IsConnected && !IsPolling;

        /// <summary>
        /// Reads data from the Modbus TCP server based on current configuration asynchronously.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanReadData))]
        private async Task ReadDataAsync()
        {
            if (_client == null || !IsConnected) return;

            try
            {
                int start = StartAddress < 0 ? 0 : StartAddress;
                int count = RegisterCount < 1 ? 1 : RegisterCount;
                if (count > 125) count = 125;

                // Wrap synchronous Read in Task.Run to keep UI responsive
                await Task.Run(() =>
                {
                    var type = SelectedRegisterType;
                    byte fCode = type switch { ModbusRegisterType.Coil => 1, ModbusRegisterType.DiscreteInput => 2, ModbusRegisterType.HoldingRegister => 3, ModbusRegisterType.InputRegister => 4, _ => 3 };
                    if (IsDebugPanelOpen) LogFrame(true, SlaveId, fCode, new byte[] { (byte)(start >> 8), (byte)start, (byte)(count >> 8), (byte)count });
                    
                    if (type == ModbusRegisterType.HoldingRegister)
                    {
                        var dataSpan = _client.ReadHoldingRegisters<short>(SlaveId, start, count);
                        var data = dataSpan.ToArray();
                        if (IsDebugPanelOpen) { byte[] bytes = new byte[1 + data.Length * 2]; bytes[0] = (byte)(data.Length * 2); Buffer.BlockCopy(data, 0, bytes, 1, bytes.Length - 1); LogFrame(false, SlaveId, fCode, bytes); }

                        var formatted = ModbusDataFormatter.FormatRegisters(dataSpan, start, SelectedDisplayMode, SelectedWordOrder, SelectedNumericFormat);
                        UpdateRegisters(formatted, type, start);
                    }
                    else if (type == ModbusRegisterType.InputRegister)
                    {
                        var dataSpan = _client.ReadInputRegisters<short>(SlaveId, start, count);
                        var data = dataSpan.ToArray();
                        if (IsDebugPanelOpen) { byte[] bytes = new byte[1 + data.Length * 2]; bytes[0] = (byte)(data.Length * 2); Buffer.BlockCopy(data, 0, bytes, 1, bytes.Length - 1); LogFrame(false, SlaveId, fCode, bytes); }
                        
                        var formatted = ModbusDataFormatter.FormatRegisters(dataSpan, start, SelectedDisplayMode, SelectedWordOrder, SelectedNumericFormat);
                        UpdateRegisters(formatted, type, start);
                    }
                    else if (type == ModbusRegisterType.Coil)
                    {
                        var dataSpan = _client.ReadCoils(SlaveId, start, count);
                        var data = dataSpan.ToArray();
                        if (IsDebugPanelOpen) { byte[] bytes = new byte[1 + data.Length]; bytes[0] = (byte)data.Length; Buffer.BlockCopy(data, 0, bytes, 1, data.Length); LogFrame(false, SlaveId, fCode, bytes); }
                        
                        var formatted = new System.Collections.Generic.List<ModbusRegister>();
                        for (int i = 0; i < count; i++)
                        {
                            int byteIdx = i / 8;
                            int bitIdx = i % 8;
                            bool val = (data[byteIdx] & (1 << bitIdx)) != 0;
                            formatted.Add(new ModbusRegister { Address = start + i, Value = val ? "1" : "0" });
                        }
                        UpdateRegisters(formatted, type, start);
                    }
                    else if (type == ModbusRegisterType.DiscreteInput)
                    {
                        var dataSpan = _client.ReadDiscreteInputs(SlaveId, start, count);
                        var data = dataSpan.ToArray();
                        if (IsDebugPanelOpen) { byte[] bytes = new byte[1 + data.Length]; bytes[0] = (byte)data.Length; Buffer.BlockCopy(data, 0, bytes, 1, data.Length); LogFrame(false, SlaveId, fCode, bytes); }
                        
                        var formatted = new System.Collections.Generic.List<ModbusRegister>();
                        for (int i = 0; i < count; i++)
                        {
                            int byteIdx = i / 8;
                            int bitIdx = i % 8;
                            bool val = (data[byteIdx] & (1 << bitIdx)) != 0;
                            formatted.Add(new ModbusRegister { Address = start + i, Value = val ? "1" : "0" });
                        }
                        UpdateRegisters(formatted, type, start);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{TranslationSource.Instance["ReadError"]}\n{ex.Message}", "Read Error");
            }
        }
        private void UpdateRegisters(System.Collections.Generic.IEnumerable<ModbusRegister> newRegisters, ModbusRegisterType type, int startAddress)
        {
            var newList = System.Linq.Enumerable.ToList(newRegisters);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Registers.Count != newList.Count || Registers.Count == 0 || Registers[0].Type != type || Registers[0].Address != startAddress)
                {
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

        /// <summary>
        /// Handles changes to register values, writing them back to the server if applicable.
        /// </summary>
        private async void OnRegisterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ModbusRegister.Value) || sender is not ModbusRegister reg) return;
            if (_client == null || !IsConnected) return;

            string newVal = reg.Value;

            if (reg.Type == ModbusRegisterType.InputRegister || reg.Type == ModbusRegisterType.DiscreteInput)
            {
                MessageBox.Show(TranslationSource.Instance["ModbusClientReadOnlyError"], "Error");
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    reg.PropertyChanged -= OnRegisterPropertyChanged;
                    reg.Value = "0";
                    reg.PropertyChanged += OnRegisterPropertyChanged;
                });
                return;
            }

            if (reg.Type == ModbusRegisterType.Coil)
            {
                if (newVal != "0" && newVal != "1")
                {
                    MessageBox.Show(TranslationSource.Instance["ModbusClientCoilError"], "Error");
                    _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        reg.PropertyChanged -= OnRegisterPropertyChanged;
                        reg.Value = "0";
                        reg.PropertyChanged += OnRegisterPropertyChanged;
                    });
                    return;
                }
            }
            if (reg.Type == ModbusRegisterType.HoldingRegister)
            {
                if (!ModbusDataFormatter.ParseUserInput(newVal, SelectedDisplayMode, SelectedWordOrder, SelectedNumericFormat, out short[] shorts))
                {
                    MessageBox.Show(TranslationSource.Instance["ModbusClientHoldingError"] ?? "Invalid Format", "Error");
                    _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        reg.PropertyChanged -= OnRegisterPropertyChanged;
                        reg.Value = "0";
                        reg.PropertyChanged += OnRegisterPropertyChanged;
                    });
                    return;
                }

                try
                {
                    await Task.Run(() =>
                    {
                        if (shorts.Length == 1)
                        {
                            short v = shorts[0];
                            if (IsDebugPanelOpen) LogFrame(true, SlaveId, 6, new byte[] { (byte)(reg.Address >> 8), (byte)reg.Address, (byte)(v >> 8), (byte)v });
                            _client.WriteSingleRegister(SlaveId, reg.Address, v);
                            if (IsDebugPanelOpen) LogFrame(false, SlaveId, 6, new byte[] { (byte)(reg.Address >> 8), (byte)reg.Address, (byte)(v >> 8), (byte)v });
                        }
                        else if (shorts.Length > 1)
                        {
                            if (IsDebugPanelOpen) LogFrame(true, SlaveId, 16, new byte[] { (byte)(reg.Address >> 8), (byte)reg.Address, 0, (byte)shorts.Length, (byte)(shorts.Length*2) });
                            _client.WriteMultipleRegisters(SlaveId, reg.Address, shorts);
                            if (IsDebugPanelOpen) LogFrame(false, SlaveId, 16, new byte[] { (byte)(reg.Address >> 8), (byte)reg.Address, 0, (byte)shorts.Length });
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{TranslationSource.Instance["ModbusClientWriteFailed"]} {ex.Message}", "Error");
                }
            }
            else if (reg.Type == ModbusRegisterType.Coil)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        bool isTrue = newVal == "1";
                        if (IsDebugPanelOpen) LogFrame(true, SlaveId, 5, new byte[] { (byte)(reg.Address >> 8), (byte)reg.Address, (byte)(isTrue ? 0xFF : 0x00), 0x00 });
                        _client.WriteSingleCoil(SlaveId, reg.Address, isTrue);
                        if (IsDebugPanelOpen) LogFrame(false, SlaveId, 5, new byte[] { (byte)(reg.Address >> 8), (byte)reg.Address, (byte)(isTrue ? 0xFF : 0x00), 0x00 });
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{TranslationSource.Instance["ModbusClientWriteFailed"]} {ex.Message}", "Error");
                }
            }

        }
    }
}
