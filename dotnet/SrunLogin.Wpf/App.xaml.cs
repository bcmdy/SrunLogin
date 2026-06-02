using System.Threading;
using System.Windows;

namespace SrunLogin.Wpf;

public partial class App
{
    private const string MutexName = "SrunLogin_SingleInstance_{B4E5F2A1-8C3D-4E6B-9F0A-1C2D3E4F5A6B}";
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("校园网认证工具已在运行，请勿重复启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
