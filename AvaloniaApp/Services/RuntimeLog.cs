using System.Text;

namespace My3DApp.AvaloniaApp.Services;

public static class RuntimeLog
{
    private static readonly object Gate = new();

    public static void Write(string area, string message, Exception? exception = null)
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            var logDirectory = Path.Combine(baseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "runtime-errors.log");

            var builder = new StringBuilder();
            builder.Append('[')
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
                .Append("] ")
                .Append(area)
                .Append(": ")
                .AppendLine(message);

            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            builder.AppendLine(new string('-', 80));

            lock (Gate)
            {
                File.AppendAllText(logPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Never throw from logger.
        }
    }
}
