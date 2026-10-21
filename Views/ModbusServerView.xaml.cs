using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutomationTools.ViewModels;

namespace AutomationTools.Views
{
    /// <summary>
    /// Interaction logic for ModbusServerView. A tool to simulate a Modbus TCP server.
    /// </summary>
    public partial class ModbusServerView : UserControl
    {
        private Point _startPoint;

        /// <summary>
        /// Initializes a new instance of the ModbusServerView class.
        /// </summary>
        public ModbusServerView()
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
