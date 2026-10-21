using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AutomationTools.ViewModels
{
    public enum PortStatus
    {
        Pending,
        Success,
        Failed
    }

    /// <summary>
    /// Represents the result of a scan for a specific port.
    /// </summary>
    public partial class PortResult : ObservableObject
    {
        [ObservableProperty]
        private int _portNumber;

        [ObservableProperty]
        private PortStatus _status = PortStatus.Pending;

        [ObservableProperty]
        private long _roundtripTime;

        [ObservableProperty]
        private string _errorMessage = string.Empty;
    }

    /// <summary>
    /// Represents a target IP address and its associated scanned ports.
    /// </summary>
    public partial class PortScanTarget : ObservableObject
    {
        [ObservableProperty]
        private string _ipAddress = string.Empty;

        public ObservableCollection<PortResult> Ports { get; } = new ObservableCollection<PortResult>();
    }
}
