using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using AutomationTools.Services;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// ViewModel for the Live Charting Tool.
    /// </summary>
    public partial class ChartingToolViewModel : ViewModelBase, IToolViewModel
    {
        private readonly System.Collections.Generic.Dictionary<IChartDataSource, ObservableCollection<LiveChartsCore.Defaults.ObservablePoint>> _seriesData = new();
        private readonly DateTime _startTime = DateTime.Now;
        private readonly SKColor[] _colors = new[]
        {
            SKColors.DodgerBlue,
            SKColors.Orange,
            SKColors.Purple,
            SKColors.MediumSeaGreen,
            SKColors.Crimson,
            SKColors.Gold,
            SKColors.DeepPink,
            SKColors.Cyan
        };

        public ChartingToolViewModel()
        {
            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));

            Series = new ObservableCollection<ISeries>();
            
            XAxes = new[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Labeler = value => 
                    {
                        if (double.IsNaN(value)) return "";
                        return _startTime.AddSeconds(value).ToString("HH:mm:ss");
                    },
                    LabelsRotation = 45,
                    TextSize = 12,
                    UnitWidth = 1,
                    MinStep = 1
                }
            };
        }

        public string Name => TranslationSource.Instance["ChartingToolName"] ?? "Live Charting";
        public string IconKind => "ChartLine";
        public double DefaultWidth => 600;
        public double DefaultHeight => 450;

        event EventHandler<double>? IToolViewModel.WidthChangedRequest { add { } remove { } }

        public void Cleanup()
        {
            foreach (var source in _seriesData.Keys)
            {
                source.PropertyChanged -= Source_PropertyChanged;
            }
            _seriesData.Clear();
            Series.Clear();
        }

        public ObservableCollection<ISeries> Series { get; }
        
        public LiveChartsCore.SkiaSharpView.Axis[] XAxes { get; }

        public void AddSeries(IChartDataSource source)
        {
            if (_seriesData.ContainsKey(source)) return; // Already tracking

            var values = new ObservableCollection<LiveChartsCore.Defaults.ObservablePoint>();
            _seriesData[source] = values;

            var color = _colors[Series.Count % _colors.Length];
            var paint = new SolidColorPaint(color) { StrokeThickness = 3 };

            var lineSeries = new LineSeries<LiveChartsCore.Defaults.ObservablePoint>
            {
                Values = values,
                Name = source.SeriesName,
                Fill = null,
                GeometrySize = 10,
                LineSmoothness = 0.2,
                Stroke = paint,
                GeometryStroke = paint,
                GeometryFill = new SolidColorPaint(color)
            };

            Series.Add(lineSeries);

            // Add initial point
            AddPoint(source);

            source.PropertyChanged += Source_PropertyChanged;
        }

        private void Source_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is IChartDataSource source)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => AddPoint(source));
            }
        }

        private void AddPoint(IChartDataSource source)
        {
            if (!_seriesData.TryGetValue(source, out var values)) return;

            double val = source.GetChartValue();
            if (!double.IsNaN(val))
            {
                double x = (DateTime.Now - _startTime).TotalSeconds;
                values.Add(new LiveChartsCore.Defaults.ObservablePoint(x, val));
                if (values.Count > 100)
                {
                    values.RemoveAt(0);
                }
            }
        }

        [RelayCommand]
        private void ClearChart()
        {
            foreach (var source in _seriesData.Keys)
            {
                source.PropertyChanged -= Source_PropertyChanged;
            }
            _seriesData.Clear();
            Series.Clear();
        }
    }
}
