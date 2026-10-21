using System.ComponentModel;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// Represents a data source that can provide continuous value updates to a chart.
    /// </summary>
    public interface IChartDataSource : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets the name of the series to display in the chart legend.
        /// </summary>
        string SeriesName { get; }

        /// <summary>
        /// Parses the current state of the data source into a double value for the chart.
        /// </summary>
        /// <returns>The double value, or double.NaN if parsing fails.</returns>
        double GetChartValue();
    }
}
