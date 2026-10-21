using System.Windows;
using System.Windows.Input;
using AutomationTools.ViewModels;

namespace AutomationTools;

/// <summary>
/// Interaction logic for the main application window. Handles custom chrome, background generation, and window state.
/// </summary>
public partial class MainWindow : Window
{
    private bool _isClosing = false;

    /// <summary>
    /// Initializes a new instance of the MainWindow class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        GenerateNoiseBackground();
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.LoadStateAsync();
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing) return;

        if (DataContext is MainViewModel vm)
        {
            e.Cancel = true;
            _isClosing = true;
            await vm.SaveStateAsync();
            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// Generates a subtle noise pattern and applies it as the window's background brush.
    /// </summary>
    private void GenerateNoiseBackground()
    {
        int width = 128;
        int height = 128;
        var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        
        var pixels = new uint[width * height];
        System.Random rand = new System.Random();
        
        for (int i = 0; i < pixels.Length; i++)
        {
            byte intensity = (byte)rand.Next(200, 255);
            byte alpha = (byte)rand.Next(0, 30); // Very subtle noise
            pixels[i] = (uint)((alpha << 24) | (intensity << 16) | (intensity << 8) | intensity);
        }
        
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        
        var brush = new System.Windows.Media.ImageBrush(bitmap)
        {
            TileMode = System.Windows.Media.TileMode.Tile,
            Viewport = new Rect(0, 0, width, height),
            ViewportUnits = System.Windows.Media.BrushMappingMode.Absolute
        };
        
        NoiseBackground.Background = brush;
    }

    /// <summary>
    /// Enables dragging the window by clicking and dragging on its background.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Minimizes the application window.
    /// </summary>
    private void btnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Toggles the maximization state of the application window.
    /// </summary>
    private void btnMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Closes the application.
    /// </summary>
    private void btnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}