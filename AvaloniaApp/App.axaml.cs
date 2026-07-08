using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using My3DApp.AvaloniaApp.Services;
using My3DApp.AvaloniaApp.ViewModels;

namespace My3DApp.AvaloniaApp;

public enum StudioThemeMode
{
    Light,
    Dark
}

public sealed class App : Avalonia.Application
{
    private static readonly Uri BaseUri = new("avares://My3DApp/");
    private static readonly Uri LightThemeUri = new("avares://My3DApp/AvaloniaApp/Themes/Studio.Light.axaml");
    private static readonly Uri DarkThemeUri = new("avares://My3DApp/AvaloniaApp/Themes/Studio.Dark.axaml");

    private StyleInclude? _activeStudioThemeStyle;

    public StudioThemeMode CurrentStudioTheme { get; private set; } = StudioThemeMode.Light;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        SetStudioTheme(CurrentStudioTheme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
        {
            RuntimeLog.Write("Avalonia", "Unhandled UI thread exception.", eventArgs.Exception);
            eventArgs.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // One application, one window: the real My3DApp shell hosts the native
            // OCCT viewport. No demo shell, no second composition root.
            desktop.MainWindow = new MainWindow
            {
                DataContext = new StudioShellViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void SetStudioTheme(StudioThemeMode mode)
    {
        if (_activeStudioThemeStyle is not null && CurrentStudioTheme == mode)
        {
            return;
        }

        if (_activeStudioThemeStyle is not null)
        {
            Styles.Remove(_activeStudioThemeStyle);
            _activeStudioThemeStyle = null;
        }

        var selectedThemeUri = mode == StudioThemeMode.Dark ? DarkThemeUri : LightThemeUri;
        _activeStudioThemeStyle = new StyleInclude(BaseUri)
        {
            Source = selectedThemeUri
        };
        Styles.Add(_activeStudioThemeStyle);

        CurrentStudioTheme = mode;
        RequestedThemeVariant = mode == StudioThemeMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
