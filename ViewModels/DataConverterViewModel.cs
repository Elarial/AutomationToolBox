using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using AutomationTools.Services;

namespace AutomationTools.ViewModels
{
    public enum DataFormat
    {
        Binary,
        Decimal,
        Hexadecimal,
        Float32
    }

    /// <summary>
    /// ViewModel for the Data Converter tool. Handles converting values between different representations (Decimal, Hex, Float32, Binary).
    /// </summary>
    public partial class DataConverterViewModel : ViewModelBase, IToolViewModel
    {
        /// <summary>
        /// Initializes a new instance of the DataConverterViewModel class.
        /// </summary>
        public DataConverterViewModel()
        {
            TranslationSource.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(Name));
        }

        public string Name => TranslationSource.Instance["DataConverterToolName"] ?? "Data Converter";
        
        public string IconKind => "SwapHorizontal";

        public double DefaultWidth => 650;
        public double DefaultHeight => 400;

        event System.EventHandler<double>? IToolViewModel.WidthChangedRequest { add { } remove { } }

        /// <summary>
        /// Cleans up resources when the tool is closed.
        /// </summary>
        public void Cleanup()
        {
            // No background tasks to cleanup
        }

        public Array AvailableFormats => Enum.GetValues(typeof(DataFormat));

        [ObservableProperty]
        private DataFormat _selectedInputFormat = DataFormat.Decimal;

        [ObservableProperty]
        private string _inputValue = string.Empty;

        [ObservableProperty]
        private string _binaryOutput = string.Empty;

        [ObservableProperty]
        private string _decimalOutput = string.Empty;

        [ObservableProperty]
        private string _hexOutput = string.Empty;

        [ObservableProperty]
        private string _floatOutput = string.Empty;

        [ObservableProperty]
        private string _warningMessage = string.Empty;

        private int _warningCounter = 0;

        /// <summary>
        /// Displays a warning message temporarily.
        /// </summary>
        public async void ShowWarning(string message)
        {
            WarningMessage = message;
            int current = ++_warningCounter;
            await Task.Delay(2500);
            if (current == _warningCounter)
            {
                WarningMessage = string.Empty;
            }
        }

        partial void OnSelectedInputFormatChanged(DataFormat value)
        {
            InputValue = string.Empty;
            ClearOutputs();
            WarningMessage = string.Empty;
        }

        partial void OnInputValueChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-")
            {
                ClearOutputs();
                return;
            }

            ConvertData(value, SelectedInputFormat);
        }

        /// <summary>
        /// Clears all the output properties.
        /// </summary>
        private void ClearOutputs()
        {
            BinaryOutput = string.Empty;
            DecimalOutput = string.Empty;
            HexOutput = string.Empty;
            FloatOutput = string.Empty;
        }

        /// <summary>
        /// Converts the input string from the specified format into all other supported formats.
        /// </summary>
        private void ConvertData(string input, DataFormat format)
        {
            try
            {
                uint rawBits = 0;

                switch (format)
                {
                    case DataFormat.Binary:
                        rawBits = Convert.ToUInt32(input, 2);
                        break;
                    case DataFormat.Decimal:
                        if (int.TryParse(input, out int decVal))
                        {
                            rawBits = (uint)decVal;
                        }
                        else
                        {
                            rawBits = Convert.ToUInt32(input, 10);
                        }
                        break;
                    case DataFormat.Hexadecimal:
                        rawBits = Convert.ToUInt32(input, 16);
                        break;
                    case DataFormat.Float32:
                        float fVal = Convert.ToSingle(input, System.Globalization.CultureInfo.InvariantCulture);
                        byte[] bytes = BitConverter.GetBytes(fVal);
                        rawBits = BitConverter.ToUInt32(bytes, 0);
                        break;
                }

                BinaryOutput = Convert.ToString(rawBits, 2).PadLeft(32, '0');
                
                DecimalOutput = ((int)rawBits).ToString();
                
                HexOutput = rawBits.ToString("X8");
                
                byte[] floatBytes = BitConverter.GetBytes(rawBits);
                float parsedFloat = BitConverter.ToSingle(floatBytes, 0);
                FloatOutput = parsedFloat.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                BinaryOutput = "Invalid";
                DecimalOutput = "Invalid";
                HexOutput = "Invalid";
                FloatOutput = "Invalid";
            }
        }
    }
}
