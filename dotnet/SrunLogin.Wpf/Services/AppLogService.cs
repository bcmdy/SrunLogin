using System.IO;

namespace SrunLogin.Wpf.Services;

public sealed class AppLogService
{
    private readonly object _lock = new();
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "app.log");

    public void Write(string text)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text}{Environment.NewLine}";

        lock (_lock)
        {
            File.AppendAllText(_logPath, line);
        }
    }
}

