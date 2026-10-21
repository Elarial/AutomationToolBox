using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace AutomationTools.Services
{
    /// <summary>
    /// Provides dynamic translation binding source for WPF UI.
    /// Usage in XAML: Text="{Binding [Key], Source={x:Static services:TranslationSource.Instance}}"
    /// </summary>
    public class TranslationSource : INotifyPropertyChanged
    {
        private static readonly TranslationSource instance = new TranslationSource();
        public static TranslationSource Instance => instance;

        private readonly ResourceManager resManager;
        private CultureInfo currentCulture;

        private TranslationSource()
        {
            resManager = new ResourceManager("AutomationTools.Resources.Locales.Strings", Assembly.GetExecutingAssembly());
            currentCulture = CultureInfo.CurrentUICulture;
        }

        public string this[string key] => resManager.GetString(key, currentCulture) ?? $"[{key}]";

        public CultureInfo CurrentCulture
        {
            get => currentCulture;
            set
            {
                if (currentCulture != value)
                {
                    currentCulture = value;
                    CultureInfo.CurrentCulture = value;
                    CultureInfo.CurrentUICulture = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
