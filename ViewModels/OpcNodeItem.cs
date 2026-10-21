using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Opc.Ua;

namespace AutomationTools.ViewModels
{
    public partial class OpcNodeItem : ObservableObject
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _nodeIdString = string.Empty;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isLoaded;

        public NodeId? OpcNodeId { get; set; }

        public ObservableCollection<OpcNodeItem> Children { get; } = new();

        public OpcNodeItem()
        {
            // Add a dummy child so the expander shows up
            Children.Add(new OpcNodeItem { DisplayName = "Loading..." });
        }
    }
}
