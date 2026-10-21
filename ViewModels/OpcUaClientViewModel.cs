#pragma warning disable CS0618 // Suppress obsolete warnings for synchronous OPC UA methods (Browse, Write, Close) until full async refactoring

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using AutomationTools.Services;

namespace AutomationTools.ViewModels
{
    public partial class OpcUaClientViewModel : ViewModelBase, IToolViewModel
    {
        public string Name => TranslationSource.Instance["OpcUaClientToolName"] ?? "OPC UA Client";
        public string IconKind => "LanConnect";
        public double DefaultWidth => 800;
        public double DefaultHeight => 600;

        public event EventHandler<double>? WidthChangedRequest { add { } remove { } }

        [ObservableProperty]
        private string _endpointUrl = "opc.tcp://localhost:4840";

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _connectionStatus = TranslationSource.Instance["OpcUaDisconnected"] ?? "Disconnected";

        [ObservableProperty]
        private string _connectionError = string.Empty;

        public ObservableCollection<OpcNodeItem> ServerNodes { get; } = new();
        public ObservableCollection<OpcWatchItem> Watchlist { get; } = new();

        private Session? _session;
        private Subscription? _subscription;
        private ApplicationConfiguration? _appConfig;

        public OpcUaClientViewModel()
        {
            InitializeOpcUa();
        }

        private void InitializeOpcUa()
        {
            try
            {
                _appConfig = new ApplicationConfiguration()
                {
                    ApplicationName = "AutomationTools OPC UA Client",
                    ApplicationUri = "urn:localhost:AutomationTools:OPCUAClient",
                    ProductUri = "urn:AutomationTools",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%LocalAppData%\AutomationTools\pki\own", SubjectName = "AutomationTools" },
                        TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%LocalAppData%\AutomationTools\pki\issuers" },
                        TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%LocalAppData%\AutomationTools\pki\trusted" },
                        RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%LocalAppData%\AutomationTools\pki\rejected" },
                        AutoAcceptUntrustedCertificates = true,
                        RejectSHA1SignedCertificates = false,
                        MinimumCertificateKeySize = 1024
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                    TraceConfiguration = new TraceConfiguration()
                };

                Task.Run(() => _appConfig.Validate(ApplicationType.Client)).Wait();
                if (_appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    _appConfig.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
                }
            }
            catch (Exception ex)
            {
                ConnectionError = "Init Error: " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (IsConnected)
            {
                Disconnect();
                return;
            }

            try
            {
                ConnectionError = string.Empty;
                ConnectionStatus = TranslationSource.Instance["OpcUaConnecting"] ?? "Connecting...";

                var endpointDescription = new EndpointDescription(EndpointUrl)
                {
                    SecurityMode = MessageSecurityMode.None,
                    SecurityPolicyUri = SecurityPolicies.None
                };
                var endpointConfiguration = EndpointConfiguration.Create(_appConfig);
                var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                _session = await Session.Create(_appConfig, endpoint, false, "AutomationToolsSession", 60000, new UserIdentity(new AnonymousIdentityToken()), null);

                IsConnected = true;
                ConnectionStatus = TranslationSource.Instance["OpcUaConnected"] ?? "Connected";

                _subscription = new Subscription(_session.DefaultSubscription) { PublishingInterval = 1000 };
                _session.AddSubscription(_subscription);
                _subscription.Create();

                PopulateRootNodes();
                RestoreWatchlistSubscriptions();
            }
            catch (Exception ex)
            {
                ConnectionError = ex.Message;
                ConnectionStatus = TranslationSource.Instance["OpcUaConnectionFailed"] ?? "Connection Failed";
                Disconnect();
            }
        }

        private void Disconnect()
        {
            if (_session != null)
            {
                try
                {
                    _session.Close();
                    _session.Dispose();
                }
                catch { }
                _session = null;
            }
            IsConnected = false;
            ConnectionStatus = TranslationSource.Instance["OpcUaDisconnected"] ?? "Disconnected";
            ServerNodes.Clear();
        }

        private void PopulateRootNodes()
        {
            ServerNodes.Clear();
            var objectsNode = new OpcNodeItem { DisplayName = "Objects", OpcNodeId = ObjectIds.ObjectsFolder, NodeIdString = ObjectIds.ObjectsFolder.ToString() };
            ServerNodes.Add(objectsNode);
        }

        [RelayCommand]
        private void ExpandNode(OpcNodeItem node)
        {
            if (node.IsLoaded || _session == null || !IsConnected) return;

            node.Children.Clear();
            try
            {
                var browseDescription = new BrowseDescription
                {
                    NodeId = node.OpcNodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method),
                    ResultMask = (uint)BrowseResultMask.All
                };

                _session.Browse(null, null, node.OpcNodeId, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Unspecified, out byte[] continuationPoint, out ReferenceDescriptionCollection references);

                if (references != null)
                {
                    foreach (var rd in references)
                    {
                        var expandedNodeId = ExpandedNodeId.ToNodeId(rd.NodeId, _session.NamespaceUris);
                        var child = new OpcNodeItem
                        {
                            DisplayName = rd.DisplayName.Text,
                            OpcNodeId = expandedNodeId,
                            NodeIdString = expandedNodeId.ToString()
                        };

                        if (rd.NodeClass == NodeClass.Variable)
                        {
                            child.Children.Clear(); // No expander for variables
                        }

                        node.Children.Add(child);
                    }
                }
                node.IsLoaded = true;
            }
            catch (Exception ex)
            {
                node.Children.Add(new OpcNodeItem { DisplayName = "Error: " + ex.Message });
            }
        }

        [RelayCommand]
        private void AddToWatchlist(OpcNodeItem node)
        {
            if (node == null || node.OpcNodeId == null) return;
            if (Watchlist.Any(w => w.OpcNodeId == node.OpcNodeId)) return;

            var watchItem = new OpcWatchItem
            {
                DisplayName = node.DisplayName,
                OpcNodeId = node.OpcNodeId,
                NodeIdString = node.NodeIdString
            };

            Watchlist.Add(watchItem);
            SubscribeToItem(watchItem);
        }

        [RelayCommand]
        private void RemoveFromWatchlist(OpcWatchItem item)
        {
            if (item != null)
            {
                UnsubscribeFromItem(item);
                Watchlist.Remove(item);
            }
        }

        private void RestoreWatchlistSubscriptions()
        {
            if (_session == null || _subscription == null) return;
            foreach (var item in Watchlist)
            {
                SubscribeToItem(item);
            }
        }

        private void SubscribeToItem(OpcWatchItem item)
        {
            if (_subscription == null || item.OpcNodeId == null) return;

            var monitoredItem = new MonitoredItem(_subscription.DefaultItem)
            {
                DisplayName = item.DisplayName,
                StartNodeId = item.OpcNodeId,
                Handle = item
            };

            monitoredItem.Notification += OnMonitoredItemNotification;
            _subscription.AddItem(monitoredItem);
            _subscription.ApplyChanges();
        }

        private void UnsubscribeFromItem(OpcWatchItem item)
        {
            if (_subscription == null) return;
            var monitoredItem = _subscription.MonitoredItems.FirstOrDefault(m => m.Handle == item);
            if (monitoredItem != null)
            {
                _subscription.RemoveItem(monitoredItem);
                _subscription.ApplyChanges();
            }
        }

        private void OnMonitoredItemNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            if (monitoredItem.Handle is OpcWatchItem watchItem && e.NotificationValue is MonitoredItemNotification notification)
            {
                var value = notification.Value;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    watchItem.Value = value.Value?.ToString() ?? "null";
                    watchItem.DataType = value.Value?.GetType().Name ?? "Unknown";
                    watchItem.Timestamp = value.SourceTimestamp.ToLocalTime().ToString("HH:mm:ss.fff");
                });
            }
        }

        [RelayCommand]
        private void WriteValue(OpcWatchItem item)
        {
            if (_session == null || !IsConnected || string.IsNullOrWhiteSpace(item.WriteInput)) return;

            try
            {
                // Simple auto-conversion based on the detected DataType
                object valueToWrite = item.WriteInput;
                if (item.DataType == "Boolean" && bool.TryParse(item.WriteInput, out bool b)) valueToWrite = b;
                else if (item.DataType == "Int16" && short.TryParse(item.WriteInput, out short s)) valueToWrite = s;
                else if (item.DataType == "Int32" && int.TryParse(item.WriteInput, out int i)) valueToWrite = i;
                else if (item.DataType == "Int64" && long.TryParse(item.WriteInput, out long l)) valueToWrite = l;
                else if (item.DataType == "Double" && double.TryParse(item.WriteInput, out double d)) valueToWrite = d;
                else if (item.DataType == "Single" && float.TryParse(item.WriteInput, out float f)) valueToWrite = f;

                var writeValue = new WriteValue
                {
                    NodeId = item.OpcNodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(valueToWrite))
                };

                _session.Write(null, new WriteValueCollection { writeValue }, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos);

                if (StatusCode.IsBad(results[0]))
                {
                    ConnectionError = $"Write failed: {results[0]}";
                }
                else
                {
                    item.WriteInput = ""; // Clear on success
                    ConnectionError = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ConnectionError = $"Write error: {ex.Message}";
            }
        }

        public void Cleanup()
        {
            Disconnect();
        }
    }
}
