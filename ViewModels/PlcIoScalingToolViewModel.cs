using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using AutomationTools.Services;

namespace AutomationTools.ViewModels
{
    public class PlcPreset
    {
        public string Name { get; set; } = "";
        public double Min { get; set; }
        public double Max { get; set; }
        public bool IsCustom { get; set; }
    }

    public class SignalType
    {
        public string Name { get; set; } = "";
        public double Min { get; set; }
        public double Max { get; set; }
        public string Unit { get; set; } = "";
    }

    public partial class PlcIoScalingToolViewModel : ViewModelBase, IToolViewModel
    {
        public string Name => TranslationSource.Instance["PlcIoScalingToolName"] ?? "PLC I/O Scaling";
        public string IconKind => "SwapVertical";
        public double DefaultWidth => 400;
        public double DefaultHeight => 750;

        public event EventHandler<double>? WidthChangedRequest { add { } remove { } }
        public void Cleanup() { }

        public ObservableCollection<PlcPreset> PlcPresets { get; } = new();
        public ObservableCollection<SignalType> SignalTypes { get; } = new();

        [ObservableProperty]
        private PlcPreset _selectedPlcPreset;

        [ObservableProperty]
        private SignalType _selectedSignalType;

        [ObservableProperty]
        private double _plcMin;

        [ObservableProperty]
        private double _plcMax;

        [ObservableProperty]
        private double _engMin = 0;

        [ObservableProperty]
        private double _engMax = 100;

        [ObservableProperty]
        private string _rawValueText = "";

        [ObservableProperty]
        private string _signalValueText = "";

        [ObservableProperty]
        private string _engValueText = "";

        [ObservableProperty]
        private string _precisionText = "";

        [ObservableProperty]
        private bool _isOutOfRange;

        [ObservableProperty]
        private string _outOfRangeWarning = "";

        private bool _isUpdating = false;

        public PlcIoScalingToolViewModel()
        {
            PlcPresets.Add(new PlcPreset { Name = "12-bit (0-4095)", Min = 0, Max = 4095 });
            PlcPresets.Add(new PlcPreset { Name = "16-bit (0-65535)", Min = 0, Max = 65535 });
            PlcPresets.Add(new PlcPreset { Name = "Custom", Min = 0, Max = 65535, IsCustom = true });

            SignalTypes.Add(new SignalType { Name = "4-20 mA", Min = 4, Max = 20, Unit = "mA" });
            SignalTypes.Add(new SignalType { Name = "0-20 mA", Min = 0, Max = 20, Unit = "mA" });
            SignalTypes.Add(new SignalType { Name = "0-10 V", Min = 0, Max = 10, Unit = "V" });
            SignalTypes.Add(new SignalType { Name = "-10 to +10 V", Min = -10, Max = 10, Unit = "V" });

            SelectedSignalType = SignalTypes[0];
            SelectedPlcPreset = PlcPresets[2];

            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));
        }

        partial void OnSelectedPlcPresetChanged(PlcPreset value)
        {
            if (value != null && !value.IsCustom)
            {
                PlcMin = value.Min;
                PlcMax = value.Max;
            }
            UpdatePrecision();
            RecalculateFromRaw();
        }

        partial void OnSelectedSignalTypeChanged(SignalType value)
        {
            UpdatePrecision();
            RecalculateFromRaw();
        }

        partial void OnPlcMinChanged(double value) { UpdatePrecision(); RecalculateFromRaw(); }
        partial void OnPlcMaxChanged(double value) { UpdatePrecision(); RecalculateFromRaw(); }
        partial void OnEngMinChanged(double value) { UpdatePrecision(); RecalculateFromRaw(); }
        partial void OnEngMaxChanged(double value) { UpdatePrecision(); RecalculateFromRaw(); }

        partial void OnRawValueTextChanged(string value)
        {
            if (_isUpdating || SelectedSignalType == null) return;
            if (double.TryParse(value, out double raw))
            {
                _isUpdating = true;
                double t = (raw - PlcMin) / (PlcMax - PlcMin);
                double sig = SelectedSignalType.Min + t * (SelectedSignalType.Max - SelectedSignalType.Min);
                double eng = EngMin + t * (EngMax - EngMin);

                SignalValueText = sig.ToString("0.000");
                EngValueText = eng.ToString("0.00");
                CheckBounds(sig);
                _isUpdating = false;
            }
        }

        partial void OnSignalValueTextChanged(string value)
        {
            if (_isUpdating || SelectedSignalType == null) return;
            if (double.TryParse(value, out double sig))
            {
                _isUpdating = true;
                double t = (sig - SelectedSignalType.Min) / (SelectedSignalType.Max - SelectedSignalType.Min);
                double raw = PlcMin + t * (PlcMax - PlcMin);
                double eng = EngMin + t * (EngMax - EngMin);

                RawValueText = Math.Round(raw).ToString();
                EngValueText = eng.ToString("0.00");
                CheckBounds(sig);
                _isUpdating = false;
            }
        }

        partial void OnEngValueTextChanged(string value)
        {
            if (_isUpdating || SelectedSignalType == null) return;
            if (double.TryParse(value, out double eng))
            {
                _isUpdating = true;
                double t = (eng - EngMin) / (EngMax - EngMin);
                double raw = PlcMin + t * (PlcMax - PlcMin);
                double sig = SelectedSignalType.Min + t * (SelectedSignalType.Max - SelectedSignalType.Min);

                RawValueText = Math.Round(raw).ToString();
                SignalValueText = sig.ToString("0.000");
                CheckBounds(sig);
                _isUpdating = false;
            }
        }

        private void RecalculateFromRaw()
        {
            OnRawValueTextChanged(RawValueText);
        }

        private void UpdatePrecision()
        {
            if (SelectedSignalType == null || PlcMax == PlcMin) return;

            double t = 1.0 / Math.Abs(PlcMax - PlcMin);
            double sigStep = t * Math.Abs(SelectedSignalType.Max - SelectedSignalType.Min);
            double engStep = t * Math.Abs(EngMax - EngMin);

            string sigUnitStr = SelectedSignalType.Unit;
            if (sigStep < 0.1 && SelectedSignalType.Unit == "V")
            {
                sigStep *= 1000;
                sigUnitStr = "mV";
            }
            else if (sigStep < 0.01 && SelectedSignalType.Unit == "mA")
            {
                sigStep *= 1000;
                sigUnitStr = "uA";
            }

            PrecisionText = $"1 bit = {sigStep:0.###} {sigUnitStr} = {engStep:0.#####} Eng. Units";
        }

        private void CheckBounds(double signal)
        {
            if (SelectedSignalType == null) return;
            if (SelectedSignalType.Name == "4-20 mA" && signal < 3.6)
            {
                IsOutOfRange = true;
                OutOfRangeWarning = "Wire Break Condition (< 3.6 mA)";
            }
            else if (signal < SelectedSignalType.Min)
            {
                IsOutOfRange = true;
                OutOfRangeWarning = "Under range";
            }
            else if (signal > SelectedSignalType.Max * 1.05) // 5% overrange allowance
            {
                IsOutOfRange = true;
                OutOfRangeWarning = "Over range / Fault";
            }
            else
            {
                IsOutOfRange = false;
                OutOfRangeWarning = "";
            }
        }
    }
}
