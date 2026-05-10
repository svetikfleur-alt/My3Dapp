using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using My3DApp.AvaloniaApp.Services;
using My3DApp.AvaloniaApp.ViewModels;

namespace My3DApp.AvaloniaApp;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            RuntimeLog.Info("App", "My3DApp started successfully.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
