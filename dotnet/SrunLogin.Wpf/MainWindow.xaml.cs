using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using SrunLogin.Wpf.ViewModels;

namespace SrunLogin.Wpf;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel;
    private readonly NotifyIcon _notifyIcon;
    private bool _allowClose;
    private bool _passwordInitializing;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = _viewModel;

        _notifyIcon = CreateNotifyIcon();

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _passwordInitializing = true;
        PasswordInput.Password = _viewModel.Password;
        _passwordInitializing = false;

        _viewModel.StartConfiguredLoopDetection();

        if (_viewModel.CanAutoLogin)
            await _viewModel.LoginAsync();
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_passwordInitializing)
            return;

        _viewModel.Password = PasswordInput.Password;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Output))
            return;

        Dispatcher.BeginInvoke(() =>
        {
            OutputBox.CaretIndex = OutputBox.Text.Length;
            OutputBox.ScrollToEnd();
        });
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开界面", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("登录", null, (_, _) => Dispatcher.Invoke(() => _viewModel.LoginCommand.Execute(null)));
        menu.Items.Add("注销", null, (_, _) => Dispatcher.Invoke(() => _viewModel.LogoutCommand.Execute(null)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _allowClose = true;
                Close();
            });
        });

        var notifyIcon = new NotifyIcon
        {
            Text = "校园网认证工具",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = LoadIcon()
        };
        notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        return notifyIcon;
    }

    private static Icon LoadIcon()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return SystemIcons.Application;

        return System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
            return;

        Hide();
        _notifyIcon.ShowBalloonTip(2000, "校园网认证工具", "已最小化到托盘", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(2000, "校园网认证工具", "程序已最小化到托盘，右键图标可操作", ToolTipIcon.Info);
            return;
        }

        _viewModel.Shutdown();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _notifyIcon.Dispose();
        base.OnClosing(e);
    }
}
