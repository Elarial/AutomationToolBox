using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutomationTools.ViewModels;
using AutomationTools.Services;

namespace AutomationTools.Views
{
    /// <summary>
    /// Interaction logic for DataConverterView. Provides conversion between different data formats (Hex, Decimal, Float, Binary).
    /// </summary>
    public partial class DataConverterView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the DataConverterView class.
        /// </summary>
        public DataConverterView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Validates text input before it is applied to the TextBox.
        /// </summary>
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DataContext is DataConverterViewModel vm)
            {
                if (!IsAllowed(e.Text, vm.SelectedInputFormat))
                {
                    e.Handled = true;
                    vm.ShowWarning(TranslationSource.Instance["InvalidCharWarning"] ?? "Invalid character!");
                }
            }
        }

        /// <summary>
        /// Validates text when pasting into the TextBox.
        /// </summary>
        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (DataContext is DataConverterViewModel vm)
                {
                    if (!IsAllowed(text, vm.SelectedInputFormat))
                    {
                        e.CancelCommand();
                        vm.ShowWarning(TranslationSource.Instance["InvalidCharWarning"] ?? "Invalid character!");
                    }
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>
        /// Determines if the provided text is allowed for the specified data format.
        /// </summary>
        private bool IsAllowed(string text, AutomationTools.ViewModels.DataFormat format)
        {
            switch (format)
            {
                case AutomationTools.ViewModels.DataFormat.Binary:
                    return Regex.IsMatch(text, "^[01]+$");
                case AutomationTools.ViewModels.DataFormat.Decimal:
                    // Allow digits and a minus sign at the beginning
                    return Regex.IsMatch(text, "^[-0-9]+$");
                case AutomationTools.ViewModels.DataFormat.Hexadecimal:
                    return Regex.IsMatch(text, "^[0-9A-Fa-f]+$");
                case AutomationTools.ViewModels.DataFormat.Float32:
                    // Allow digits, minus sign, dot, and 'e'/'E' for scientific notation
                    return Regex.IsMatch(text, "^[-0-9.eE]+$");
                default:
                    return false;
            }
        }
    }
}
