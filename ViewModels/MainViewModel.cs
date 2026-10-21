using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using AutomationTools.Services;
using AutomationTools.Models.Persistence;
using System.Threading.Tasks;
namespace AutomationTools.ViewModels
{
    /// <summary>
    /// The Main ViewModel orchestrating the application workspace.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        public ObservableCollection<IToolViewModel> AvailableTools { get; } = new();

        public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = new();

        [ObservableProperty]
        private WorkspaceViewModel? _selectedWorkspace;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyMessage = string.Empty;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            // Register available tools (acts as templates)
            AvailableTools.Add(new DataConverterViewModel());
            AvailableTools.Add(new PingToolViewModel());
            AvailableTools.Add(new PortTesterViewModel());
            AvailableTools.Add(new ModbusServerViewModel());
            AvailableTools.Add(new ModbusClientViewModel());
            AvailableTools.Add(new ChartingToolViewModel());
            AvailableTools.Add(new PlcIoScalingToolViewModel());
            AvailableTools.Add(new OpcUaClientViewModel());

            // AddWorkspace() is called inside LoadStateAsync instead if loading fails.
        }

        public async Task LoadStateAsync()
        {
            IsBusy = true;
            BusyMessage = TranslationSource.Instance["LoadingMessage"] ?? "Loading workspace...";
            await Task.Delay(500); // Artificial delay for smooth UX

            var svc = new AutomationTools.Services.PersistenceService();
            var state = await svc.LoadAsync();

            if (state != null && state.Workspaces.Count > 0)
            {
                Workspaces.Clear();
                foreach (var wsDto in state.Workspaces)
                {
                    var ws = new WorkspaceViewModel(wsDto.Name);
                    ws.LoadFromDto(wsDto);
                    Workspaces.Add(ws);
                }

                if (state.SelectedWorkspaceIndex >= 0 && state.SelectedWorkspaceIndex < Workspaces.Count)
                {
                    SelectedWorkspace = Workspaces[state.SelectedWorkspaceIndex];
                }
                else
                {
                    SelectedWorkspace = Workspaces[0];
                }
            }
            else
            {
                if (Workspaces.Count == 0)
                {
                    AddWorkspace();
                }
            }

            IsBusy = false;
        }

        public async Task SaveStateAsync()
        {
            IsBusy = true;
            BusyMessage = TranslationSource.Instance["SavingMessage"] ?? "Saving workspace...";
            await Task.Delay(500); // Artificial delay for smooth UX

            var state = new AppStateDto();
            state.SelectedWorkspaceIndex = Workspaces.IndexOf(SelectedWorkspace!);

            foreach (var ws in Workspaces)
            {
                state.Workspaces.Add(ws.ToDto());
            }

            var svc = new AutomationTools.Services.PersistenceService();
            await svc.SaveAsync(state);

            IsBusy = false;
        }

        /// <summary>
        /// Spawns a new tool in the currently selected workspace based on the given template.
        /// </summary>
        [RelayCommand]
        private void SpawnTool(IToolViewModel templateTool)
        {
            SelectedWorkspace?.AddTool(templateTool);
        }

        /// <summary>
        /// Adds a new workspace to the application.
        /// </summary>
        [RelayCommand]
        private void AddWorkspace()
        {
            if (Workspaces.Count >= 10) return;
            
            var ws = new WorkspaceViewModel($"Workspace {Workspaces.Count + 1}");
            Workspaces.Add(ws);
            SelectedWorkspace = ws;
        }

        /// <summary>
        /// Closes the specified workspace and removes it from the application.
        /// </summary>
        [RelayCommand]
        private void CloseWorkspace(WorkspaceViewModel ws)
        {
            if (Workspaces.Count <= 1) return; // Enforce minimum 1

            if (ws != null)
            {
                ws.CloseWorkspace();
                Workspaces.Remove(ws);
                if (SelectedWorkspace == ws)
                {
                    SelectedWorkspace = Workspaces[Workspaces.Count - 1];
                }
            }
        }

        /// <summary>
        /// Changes the active language of the application to the specified culture code.
        /// </summary>
        [RelayCommand]
        private void ChangeLanguage(string cultureCode)
        {
            TranslationSource.Instance.CurrentCulture = new CultureInfo(cultureCode);
        }
    }
}
