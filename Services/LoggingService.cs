using System.Text;

namespace WinBridge.Services;

public sealed class LoggingService
{
    private readonly string _logDirectory;
    private readonly object _sync = new();

    public LoggingService(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinBridge", "Logs");
        Directory.CreateDirectory(_logDirectory);
        Rotate();
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_sync)
                File.AppendAllText(Path.Combine(_logDirectory, $"winbridge-{DateTime.Now:yyyyMMdd}.log"),
                    $"{DateTimeOffset.Now:O} [{level}] {Sanitize(message)}{Environment.NewLine}", Encoding.UTF8);
        }
        catch { /* Logging must never terminate the app. */ }
    }

    private void Rotate()
    {
        try
        {
            foreach (var file in new DirectoryInfo(_logDirectory).GetFiles("winbridge-*.log")
                         .OrderByDescending(f => f.CreationTimeUtc).Skip(7))
                file.Delete();
        }
        catch { }
    }

    private static string Sanitize(string value) =>
        value.Replace(Environment.UserName, "[user]", StringComparison.OrdinalIgnoreCase);
}
