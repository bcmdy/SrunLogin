using System.Runtime.InteropServices;
using System.Threading;
using SrunLogin.GUI;

namespace SrunLogin;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        const string mutexName = "SrunLogin_SingleInstance_{B4E5F2A1-8C3D-4E6B-9F0A-1C2D3E4F5A6B}";

        // 尝试创建/获取互斥体
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // 程序已在运行，提示用户并激活现有窗口后退出
            MessageBox.Show("校园网认证工具已在运行，请勿重复启动。", "提示", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ActivateExistingWindow();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());

        // 释放互斥体
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    /// <summary>
    /// 激活已运行的实例窗口（通过窗口标题查找）
    /// </summary>
    private static void ActivateExistingWindow()
    {
        // 使用 Windows API 查找并激活现有窗口
        var hwnd = FindWindow(null, "校园网认证工具");
        if (hwnd != IntPtr.Zero)
        {
            // 如果窗口最小化，恢复它
            if (IsIconic(hwnd))
                ShowWindow(hwnd, SW_RESTORE);

            // 置顶激活
            SetForegroundWindow(hwnd);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
}