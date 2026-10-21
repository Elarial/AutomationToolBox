using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// Wraps an IToolViewModel to provide workspace-specific properties like position and size.
    /// </summary>
    public partial class ToolWrapperViewModel : ViewModelBase
    {
        private readonly Action<ToolWrapperViewModel> _closeAction;
        private readonly Action<ToolWrapperViewModel> _focusAction;

        public IToolViewModel Content { get; }

        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        [ObservableProperty]
        private double _width;

        [ObservableProperty]
        private double _height;

        [ObservableProperty]
        private int _zIndex;

        /// <summary>
        /// Initializes a new instance of the ToolWrapperViewModel class.
        /// </summary>
        public ToolWrapperViewModel(IToolViewModel content, Action<ToolWrapperViewModel> closeAction, Action<ToolWrapperViewModel> focusAction)
        {
            Content = content;
            _closeAction = closeAction;
            _focusAction = focusAction;

            Width = content.DefaultWidth;
            Height = content.DefaultHeight;

            content.WidthChangedRequest += (s, delta) => Width += delta;
        }

        /// <summary>
        /// Closes the tool by invoking the provided close action.
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            _closeAction?.Invoke(this);
        }

        /// <summary>
        /// Focuses the tool by invoking the provided focus action.
        /// </summary>
        [RelayCommand]
        private void Focus()
        {
            _focusAction?.Invoke(this);
        }
    }
}
