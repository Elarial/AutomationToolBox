using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AutomationTools.Services;
using System.Windows;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// ViewModel for the Port Tester tool. Handles IP and port parsing and concurrent TCP connection testing.
    /// </summary>
    public partial class PortTesterViewModel : ViewModelBase, IToolViewModel
    {
        /// <summary>
        /// Initializes a new instance of the PortTesterViewModel class.
        /// </summary>
        public PortTesterViewModel()
        {
            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));
        }

        public string Name => TranslationSource.Instance["PortTesterName"] ?? "Port Tester";
        
        public string IconKind => "LanConnect"; // Material Design Icon

        public double DefaultWidth => 650;
        public double DefaultHeight => 450;

        event System.EventHandler<double>? IToolViewModel.WidthChangedRequest { add { } remove { } }

        /// <summary>
        /// Cleans up resources when the tool is closed.
        /// </summary>
        public void Cleanup()
        {
            if (IsRunning)
            {
                StopScan();
            }
        }

        [ObservableProperty]
        private string _inputTargetIp = string.Empty;

        [ObservableProperty]
        private string _inputTargetPorts = "80, 443, 502";

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private int _totalProgress;

        [ObservableProperty]
        private int _maxProgress = 1;

        [ObservableProperty]
        private bool _isInputValid = true;

        [ObservableProperty]
        private string _dynamicWarning = string.Empty;

        public ObservableCollection<PortScanTarget> Results { get; } = new ObservableCollection<PortScanTarget>();

        private CancellationTokenSource? _cancellationTokenSource;

        partial void OnInputTargetIpChanged(string value)
        {
            ValidateInput();
            StartScanCommand.NotifyCanExecuteChanged();
        }

        partial void OnInputTargetPortsChanged(string value)
        {
            ValidateInput();
            StartScanCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Validates the input IP and port strings and updates the UI accordingly.
        /// </summary>
        private void ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(InputTargetIp) || string.IsNullOrWhiteSpace(InputTargetPorts))
            {
                IsInputValid = true;
                DynamicWarning = string.Empty;
                return;
            }

            var ips = ParseIpRange(InputTargetIp);
            var ports = ParsePorts(InputTargetPorts);

            if (ips.Count == 0)
            {
                IsInputValid = false;
                DynamicWarning = TranslationSource.Instance["InvalidIpFormatWarning"] ?? "Invalid IP format. Examples: 192.168.1.1, 192.168.1.1-50";
            }
            else if (ports.Count == 0)
            {
                IsInputValid = false;
                DynamicWarning = TranslationSource.Instance["InvalidPortFormatWarning"] ?? "Invalid port format. Examples: 80, 443, 502-505";
            }
            else if (ips.Count * ports.Count > 10000)
            {
                IsInputValid = false;
                DynamicWarning = "Maximum allowed scan limit is 10,000 combinations.";
            }
            else
            {
                IsInputValid = true;
                DynamicWarning = string.Empty;
            }
        }

        /// <summary>
        /// Starts the port scanning process asynchronously.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartScan))]
        private async Task StartScanAsync()
        {
            var ipsToScan = ParseIpRange(InputTargetIp);
            var portsToScan = ParsePorts(InputTargetPorts);

            if (ipsToScan.Count == 0 || portsToScan.Count == 0 || (ipsToScan.Count * portsToScan.Count) > 10000)
                return;

            Results.Clear();
            var flattenedTasksList = new List<Func<Task>>();

            foreach (var ip in ipsToScan)
            {
                var target = new PortScanTarget { IpAddress = ip };
                foreach (var port in portsToScan)
                {
                    var portResult = new PortResult { PortNumber = port };
                    target.Ports.Add(portResult);

                    flattenedTasksList.Add(async () => await ScanPortAsync(target.IpAddress, portResult));
                }
                Results.Add(target);
            }

            IsRunning = true;
            MaxProgress = flattenedTasksList.Count;
            TotalProgress = 0;
            StartScanCommand.NotifyCanExecuteChanged();
            StopScanCommand.NotifyCanExecuteChanged();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            var semaphore = new SemaphoreSlim(20);
            var tasks = new List<Task>();

            try
            {
                foreach (var scanFunc in flattenedTasksList)
                {
                    await semaphore.WaitAsync(token);

                    if (token.IsCancellationRequested)
                    {
                        semaphore.Release();
                        break;
                    }

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            if (token.IsCancellationRequested) return;
                            await scanFunc();
                        }
                        finally
                        {
                            Application.Current.Dispatcher.Invoke(() => TotalProgress++);
                            semaphore.Release();
                        }
                    }, token));
                }

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Clean cancellation
            }
            finally
            {
                IsRunning = false;
                StartScanCommand.NotifyCanExecuteChanged();
                StopScanCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Scans a single port on a specific IP asynchronously.
        /// </summary>
        private async Task ScanPortAsync(string ip, PortResult result)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, result.PortNumber);
                
                // 1500ms timeout
                if (await Task.WhenAny(connectTask, Task.Delay(1500)) == connectTask)
                {
                    // Connected (or connection refused exception thrown)
                    await connectTask; // Unwrap potential exceptions
                    stopwatch.Stop();
                    result.Status = PortStatus.Success;
                    result.RoundtripTime = stopwatch.ElapsedMilliseconds;
                }
                else
                {
                    // Timeout
                    stopwatch.Stop();
                    result.Status = PortStatus.Failed;
                    result.ErrorMessage = "Timeout";
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Status = PortStatus.Failed;
                result.ErrorMessage = (ex is SocketException socketEx) ? socketEx.SocketErrorCode.ToString() : "Failed";
            }
        }

        /// <summary>
        /// Determines whether a new scan can be started.
        /// </summary>
        private bool CanStartScan() => !IsRunning && !string.IsNullOrWhiteSpace(InputTargetIp) && !string.IsNullOrWhiteSpace(InputTargetPorts);

        /// <summary>
        /// Stops the currently running port scan.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void StopScan()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Parses a string representing an IP range into a list of IP addresses.
        /// </summary>
        private List<string> ParseIpRange(string input)
        {
            var ips = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return ips;

            input = input.Trim();
            var parts = input.Split('-');

            if (parts.Length == 1)
            {
                if (IPAddress.TryParse(parts[0].Trim(), out _))
                    ips.Add(parts[0].Trim());
            }
            else if (parts.Length == 2)
            {
                string startStr = parts[0].Trim();
                string endStr = parts[1].Trim();

                if (!IPAddress.TryParse(startStr, out IPAddress? startIp)) return ips;
                
                IPAddress? endIp;
                if (byte.TryParse(endStr, out byte endOctet))
                {
                    var startBytes = startIp.GetAddressBytes();
                    startBytes[3] = endOctet;
                    endIp = new IPAddress(startBytes);
                }
                else if (!IPAddress.TryParse(endStr, out endIp)) return ips;

                uint startUint = IpToUint(startIp);
                uint endUint = IpToUint(endIp);

                if (startUint <= endUint)
                {
                    uint maxCount = Math.Min(endUint - startUint + 1, 10000);
                    for (uint i = 0; i < maxCount; i++)
                    {
                        ips.Add(UintToIp(startUint + i).ToString());
                    }
                }
            }
            return ips;
        }

        /// <summary>
        /// Parses a string representing a list of ports or port ranges into a list of integers.
        /// </summary>
        private List<int> ParsePorts(string input)
        {
            var ports = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(input)) return new List<int>();

            var groups = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var g in groups)
            {
                var p = g.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                if (p.Contains("-"))
                {
                    var range = p.Split('-');
                    if (range.Length == 2 && int.TryParse(range[0].Trim(), out int start) && int.TryParse(range[1].Trim(), out int end))
                    {
                        if (start > 0 && end <= 65535 && start <= end)
                        {
                            for (int i = start; i <= end; i++) ports.Add(i);
                        }
                    }
                }
                else
                {
                    if (int.TryParse(p, out int port) && port > 0 && port <= 65535)
                    {
                        ports.Add(port);
                    }
                }
            }

            var list = ports.ToList();
            list.Sort();
            return list;
        }

        /// <summary>
        /// Converts an IPAddress object to an unsigned integer.
        /// </summary>
        private static uint IpToUint(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        /// <summary>
        /// Converts an unsigned integer to an IPAddress object.
        /// </summary>
        private static IPAddress UintToIp(uint ip)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)((ip >> 24) & 0xFF);
            bytes[1] = (byte)((ip >> 16) & 0xFF);
            bytes[2] = (byte)((ip >> 8) & 0xFF);
            bytes[3] = (byte)(ip & 0xFF);
            return new IPAddress(bytes);
        }
    }
}
