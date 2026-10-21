using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AutomationTools.ViewModels;

namespace AutomationTools.Views
{
    /// <summary>
    /// Interaction logic for ToolContainerView. Provides dragging and resizing logic for tools in the workspace.
    /// </summary>
    public partial class ToolContainerView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the ToolContainerView class.
        /// </summary>
        public ToolContainerView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Finds the first visual tree parent of the specified type.
        /// </summary>
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        /// <summary>
        /// Handles the DragDelta event of the TitleBar to move the tool within its canvas.
        /// </summary>
        private void TitleBar_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is ToolWrapperViewModel vm)
            {
                double newX = vm.X + e.HorizontalChange;
                double newY = vm.Y + e.VerticalChange;

                var canvas = FindParent<Canvas>(this);
                if (canvas != null)
                {
                    if (newX < 0) newX = 0;
                    if (newX + vm.Width > canvas.ActualWidth) newX = Math.Max(0, canvas.ActualWidth - vm.Width);

                    if (newY < 0) newY = 0;
                    if (newY + vm.Height > canvas.ActualHeight) newY = Math.Max(0, canvas.ActualHeight - vm.Height);
                }
                else
                {
                    if (newX < 0) newX = 0;
                    if (newY < 0) newY = 0;
                }

                vm.X = newX;
                vm.Y = newY;
            }
        }

        /// <summary>
        /// Handles the DragDelta event of the Resize thumb to resize the tool.
        /// </summary>
        private void Resize_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is ToolWrapperViewModel vm)
            {
                double newWidth = Math.Max(200, vm.Width + e.HorizontalChange);
                double newHeight = Math.Max(100, vm.Height + e.VerticalChange);

                var canvas = FindParent<Canvas>(this);
                if (canvas != null)
                {
                    if (vm.X + newWidth > canvas.ActualWidth)
                        newWidth = Math.Max(200, canvas.ActualWidth - vm.X);

                    if (vm.Y + newHeight > canvas.ActualHeight)
                        newHeight = Math.Max(100, canvas.ActualHeight - vm.Y);
                }

                vm.Width = newWidth;
                vm.Height = newHeight;
            }
        }

        /// <summary>
        /// Focuses the tool when it is clicked.
        /// </summary>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ToolWrapperViewModel vm)
            {
                vm.FocusCommand?.Execute(null);
            }
        }
    }
}
