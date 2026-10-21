using System.Windows;
using System.Windows.Controls;
using AutomationTools.ViewModels;

namespace AutomationTools.Views
{
    public partial class ChartingToolView : UserControl
    {
        public ChartingToolView()
        {
            InitializeComponent();
        }

        private void Chart_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is ChartingToolViewModel vm)
            {
                var formats = e.Data.GetFormats();
                foreach (var format in formats)
                {
                    var data = e.Data.GetData(format);
                    if (data is IChartDataSource source)
                    {
                        vm.AddSeries(source);
                        break;
                    }
                }
            }
        }
    }
}
