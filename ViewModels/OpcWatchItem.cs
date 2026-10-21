using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Opc.Ua;

namespace AutomationTools.ViewModels
{
    public partial class OpcWatchItem : ObservableObject, IChartDataSource
    {
        [ObservableProperty]
        private string _nodeId = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _nodeIdString = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private string _dataType = string.Empty;

        [ObservableProperty]
        private string _timestamp = string.Empty;

        [ObservableProperty]
        private string _writeInput = string.Empty;

        public NodeId? OpcNodeId { get; set; }

        public string SeriesName => DisplayName;

        public double GetChartValue()
        {
            if (double.TryParse(Value, out double result))
            {
                return result;
            }
            return double.NaN;
        }
    }
}
