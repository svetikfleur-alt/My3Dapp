using Avalonia.Controls;
using Avalonia.Threading;
using My3DApp.AvaloniaApp.Services;
using My3DApp.AvaloniaApp.ViewModels;

namespace My3DApp.AvaloniaApp;

public partial class MainWindow : Window
{
    private ViewportService? _viewport;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var host = this.FindControl<Border>("ViewportHost");
        if (host is null) return;

        try
        {
            _viewport = new ViewportService(host);
            await _viewport.InitializeAsync();
            vm.ViewportService = _viewport;
            vm.IsViewportLoading = false;
            RuntimeLog.Info("MainWindow", "Viewport initialized.");
        }
        catch (Exception ex)
        {
            RuntimeLog.Error("MainWindow", "Failed to initialize viewport.", ex);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewport?.Dispose();
        RuntimeLog.Info("MainWindow", "Window closing.");
    }
}
