using Avalonia;
using My3DApp.AvaloniaApp;
using My3DApp.AvaloniaApp.Services;

namespace My3DApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            RuntimeLog.Write("Runtime", "Unhandled AppDomain exception.", eventArgs.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            RuntimeLog.Write("Runtime", "Unobserved task exception.", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
