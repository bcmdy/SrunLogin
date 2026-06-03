using System.IO;

namespace SrunLogin.Wpf.Services;

public sealed class AppLogService
{
    private const long MaxLogBytes = 1024 * 1024;
    private readonly object _lock = new();
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "app.log");
    private readonly string _archivePath = Path.Combine(AppContext.BaseDirectory, "app.1.log");

    public void Write(string text)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text}{Environment.NewLine}";

        lock (_lock)
        {
            RotateIfNeeded(line);
            File.AppendAllText(_logPath, line);
        }
    }

    private void RotateIfNeeded(string pendingLine)
    {
        var dir = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_logPath))
            return;

        var currentSize = new FileInfo(_logPath).Length;
        if (currentSize + pendingLine.Length <= MaxLogBytes)
            return;

        if (File.Exists(_archivePath))
            File.Delete(_archivePath);

        File.Move(_logPath, _archivePath);
    }
}
