namespace My3DApp.AvaloniaApp.Services;

public sealed class AutosaveService : IDisposable
{
    private static readonly string AutosaveFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "My3DApp", "Autosave", "autosave.my3dapp");

    public static string AutosavePath => AutosaveFile;

    public static bool HasAutosave => File.Exists(AutosaveFile);

    public static DateTimeOffset? GetAutosaveTimestamp()
    {
        try
        {
            return HasAutosave ? File.GetLastWriteTimeUtc(AutosaveFile) : null;
        }
        catch
        {
            return null;
        }
    }

    public static void DeleteAutosave()
    {
        try { File.Delete(AutosaveFile); } catch { }
    }

    private readonly Func<bool> _isDirty;
    private readonly Action<string> _saveAction;
    private readonly Action<DateTimeOffset>? _savedCallback;
    private readonly System.Timers.Timer _timer;
    private bool _disposed;

    public AutosaveService(Func<bool> isDirty, Action<string> saveAction, Action<DateTimeOffset>? savedCallback = null, double intervalMs = 120_000)
    {
        _isDirty = isDirty;
        _saveAction = saveAction;
        _savedCallback = savedCallback;
        _timer = new System.Timers.Timer(intervalMs) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isDirty())
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutosaveFile)!);
            _saveAction(AutosaveFile);
            _savedCallback?.Invoke(DateTimeOffset.UtcNow);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
    }
}
