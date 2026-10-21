using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using AutomationTools.Services;
using System.Windows;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// ViewModel for the Ping Tool. Handles parsing target IP ranges and executing concurrent ping requests.
    /// </summary>
    public partial class PingToolViewModel : ViewModelBase, IToolViewModel
    {
        /// <summary>
        /// Initializes a new instance of the PingToolViewModel class.
        /// </summary>
        public PingToolViewModel()
        {
            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));
        }

        public string Name => TranslationSource.Instance["PingToolName"] ?? "Network Ping";
        
        public string IconKind => "LanPending"; // Material Design Icon

        public double DefaultWidth => 600;
        public double DefaultHeight => 450;

        event System.EventHandler<double>? IToolViewModel.WidthChangedRequest { add { } remove { } }

        /// <summary>
        /// Cleans up resources when the tool is closed.
        /// </summary>
        public void Cleanup()
        {
            if (IsRunning)
            {
                StopPing();
            }
        }

        [ObservableProperty]
        private string _inputTarget = string.Empty;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private int _totalProgress;

        [ObservableProperty]
        private int _maxProgress = 1;

        public ObservableCollection<PingResult> Results { get; } = new ObservableCollection<PingResult>();

        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Starts the ping scanning process asynchronously.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartPing))]
        private async Task StartPingAsync()
        {
            var ipsToPing = ParseIpRange(InputTarget);

            if (ipsToPing.Count == 0)
            {
                MessageBox.Show(TranslationSource.Instance["InvalidIpFormatWarning"] ?? "Invalid IP format. Examples: 192.168.1.1, 192.168.1.1-50, 10.0.0.1-10.0.0.254", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ipsToPing.Count > 10000)
            {
                MessageBox.Show("Maximum allowed range is 10,000 IPs.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Results.Clear();
            foreach (var ip in ipsToPing)
            {
                Results.Add(new PingResult { IpAddress = ip });
            }

            IsRunning = true;
            MaxProgress = ipsToPing.Count;
            TotalProgress = 0;
            StartPingCommand.NotifyCanExecuteChanged();
            StopPingCommand.NotifyCanExecuteChanged();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Maximum of 20 parallel ping requests
            var semaphore = new SemaphoreSlim(20);
            var tasks = new List<Task>();

            try
            {
                foreach (var result in Results)
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
                            using var pingSender = new Ping();
                            var reply = await pingSender.SendPingAsync(result.IpAddress, 3000);
                            
                            if (token.IsCancellationRequested) return;

                            if (reply.Status == IPStatus.Success)
                            {
                                result.Status = PingStatus.Success;
                                result.RoundtripTime = reply.RoundtripTime;
                            }
                            else
                            {
                                result.Status = PingStatus.Failed;
                                result.ErrorMessage = reply.Status.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Status = PingStatus.Failed;
                            result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
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
                // Cancellation safely handled
            }
            finally
            {
                IsRunning = false;
                StartPingCommand.NotifyCanExecuteChanged();
                StopPingCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Determines whether a new ping scan can be started.
        /// </summary>
        private bool CanStartPing() => !IsRunning && !string.IsNullOrWhiteSpace(InputTarget);

        /// <summary>
        /// Stops the currently running ping scan.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void StopPing()
        {
            _cancellationTokenSource?.Cancel();
        }

        [ObservableProperty]
        private bool _isInputValid = true;

        [ObservableProperty]
        private string _dynamicWarning = string.Empty;

        partial void OnInputTargetChanged(string value)
        {
            StartPingCommand.NotifyCanExecuteChanged();
            ValidateInput(value);
        }

        /// <summary>
        /// Validates the input IP range string and updates the UI accordingly.
        /// </summary>
        private void ValidateInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsInputValid = true;
                DynamicWarning = string.Empty;
                return;
            }

            var ips = ParseIpRange(value);
            if (ips.Count == 0)
            {
                IsInputValid = false;
                DynamicWarning = TranslationSource.Instance["InvalidIpFormatWarning"] ?? "Invalid IP format. Examples: 192.168.1.1, 192.168.1.1-50";
            }
            else if (ips.Count > 10000)
            {
                IsInputValid = false;
                DynamicWarning = "Maximum allowed range is 10,000 IPs.";
            }
            else
            {
                IsInputValid = true;
                DynamicWarning = string.Empty;
            }
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
                else if (!IPAddress.TryParse(endStr, out endIp))
                {
                    return ips;
                }

                uint startUint = IpToUint(startIp);
                uint endUint = IpToUint(endIp);

                if (startUint <= endUint)
                {
                    uint maxCount = Math.Min(endUint - startUint + 1, 10000); // hard cap
                    for (uint i = 0; i < maxCount; i++)
                    {
                        ips.Add(UintToIp(startUint + i).ToString());
                    }
                }
            }
            return ips;
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
