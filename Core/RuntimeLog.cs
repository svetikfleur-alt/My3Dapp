using System.IO;
using System.Runtime.CompilerServices;

namespace My3DApp.Core;

public static class RuntimeLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "My3DApp", "runtime.log");

    private static readonly object _lock = new();

    static RuntimeLog()
    {
        var dir = Path.GetDirectoryName(LogPath)!;
        Directory.CreateDirectory(dir);
    }

    public static void Write(
        string source,
        string message,
        Exception? ex = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var location = $"{Path.GetFileNameWithoutExtension(file)}:{line} {member}";
        var entry = $"[{timestamp}] [{source}] {message}";
        if (ex != null)
            entry += $"\n  Exception: {ex.GetType().Name}: {ex.Message}\n  Stack: {ex.StackTrace}";
        entry += $"\n  at {location}";

        lock (_lock)
        {
            try { File.AppendAllText(LogPath, entry + "\n"); }
            catch { /* never throw from logging */ }
        }

        System.Diagnostics.Debug.WriteLine(entry);
    }

    public static void Info(string source, string message)                      => Write(source, $"INFO  {message}");
    public static void Warn(string source, string message)                      => Write(source, $"WARN  {message}");
    public static void Error(string source, string message, Exception? ex = null) => Write(source, $"ERROR {message}", ex);
}
