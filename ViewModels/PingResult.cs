using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationTools.ViewModels
{
    public enum PingStatus
    {
        Pending,
        Success,
        Failed
    }

    /// <summary>
    /// Represents the result of an ICMP ping request to a specific IP address.
    /// </summary>
    public partial class PingResult : ObservableObject
    {
        [ObservableProperty]
        private string _ipAddress = string.Empty;

        [ObservableProperty]
        private PingStatus _status = PingStatus.Pending;

        [ObservableProperty]
        private long _roundtripTime;

        [ObservableProperty]
        private string _errorMessage = string.Empty;
    }
}
