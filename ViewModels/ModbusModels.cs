using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationTools.ViewModels
{
    public enum ModbusRegisterType
    {
        HoldingRegister,
        InputRegister,
        Coil,
        DiscreteInput
    }

    public enum ModbusDisplayMode
    {
        Word16,
        Word32,
        Float32,
        Byte8,
        BitField
    }

    /// <summary>
    /// Represents a single Modbus register or coil with its address and formatted string value.
    /// </summary>
    public partial class ModbusRegister : ObservableObject, IChartDataSource
    {
        public int Address { get; set; }

        [ObservableProperty]
        private string _value = "0";

        public ModbusRegisterType Type { get; set; }

        // IChartDataSource implementation
        public string SeriesName => $"{Type} {Address}";

        public double GetChartValue()
        {
            if (double.TryParse(Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;

            if (bool.TryParse(Value, out bool bResult))
                return bResult ? 1.0 : 0.0;

            return double.NaN;
        }
    }
}
