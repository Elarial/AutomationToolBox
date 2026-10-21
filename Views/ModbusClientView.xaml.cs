using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutomationTools.ViewModels;

namespace AutomationTools.Views
{
    /// <summary>
    /// Interaction logic for ModbusClientView. A tool to connect to and interact with Modbus TCP servers.
    /// </summary>
    public partial class ModbusClientView : UserControl
    {
        private Point _startPoint;

        /// <summary>
        /// Initializes a new instance of the ModbusClientView class.
        /// </summary>
        public ModbusClientView()
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
                    if (dataGrid.SelectedItem is ModbusRegister register)
                    {
                        DragDrop.DoDragDrop(dataGrid, register, DragDropEffects.Copy);
                    }
                }
            }
        }
    }
}
