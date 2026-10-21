namespace AutomationTools.ViewModels
{
    /// <summary>
    /// Defines the contract for an automation tool that can be hosted in the workspace.
    /// </summary>
    public interface IToolViewModel
    {
        /// <summary>
        /// Gets the localized name of the tool.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the icon identifier for the tool (e.g., Material Design Icon kind).
        /// </summary>
        string IconKind { get; }

        /// <summary>
        /// Gets the default width of the tool's card when spawned.
        /// </summary>
        double DefaultWidth { get; }

        /// <summary>
        /// Gets the default height of the tool's card when spawned.
        /// </summary>
        double DefaultHeight { get; }

        /// <summary>
        /// Event invoked to request a relative change in the wrapper's width.
        /// </summary>
        event System.EventHandler<double> WidthChangedRequest;

        /// <summary>
        /// Cleans up any background tasks or connections when the tool is closed.
        /// </summary>
        void Cleanup();
    }
}
