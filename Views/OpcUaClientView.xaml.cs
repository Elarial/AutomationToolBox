using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutomationTools.ViewModels;

namespace AutomationTools.Views
{
    public partial class OpcUaClientView : UserControl
    {
        private Point _startPoint;

        public OpcUaClientView()
        {
            InitializeComponent();
        }

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is DataGrid dataGrid)
            {
                Point position = e.GetPosition(null);
                if (System.Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (dataGrid.SelectedItem is OpcWatchItem watchItem)
                    {
                        DragDrop.DoDragDrop(dataGrid, watchItem, DragDropEffects.Copy);
                    }
                }
            }
        }
    }
}
