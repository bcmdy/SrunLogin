using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Windows;
using SrunLogin.Models;
using SrunLogin.Services;
using SrunLogin.Utils;
using SrunLogin.Wpf.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace SrunLogin.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppConfigService _configService = new();
    private readonly AppLogService _logService = new();
    private readonly SemaphoreSlim _loopCheckGate = new(1, 1);
    private CancellationTokenSource? _operationCts;
    private CancellationTokenSource? _loopCts;

    private string _url = "http://10.0.0.1";
    private string _username = "";
    private string _password = "";
    private string _ip = "";
    private string _acId = "";
    private string _domain = "@edu.cn";
    private bool _saveConfig = true;
    private bool _autoLogin = true;
    private bool _loopEnabled;
    private string _loopInterval = "10";
    private string _loopTimeout = "3";
    private string _pingHost = "www.baidu.com";
    private bool _ignoreTlsErrors;
    private string _loopStatus = "状态：未启动";
    private string _output = "";

    public MainViewModel()
    {
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        InfoCommand = new AsyncRelayCommand(QueryInfoAsync);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        HelpCommand = new AsyncRelayCommand(ShowHelpAsync);

        LoadConfig();
    }

    public AsyncRelayCommand LoginCommand { get; }
    public AsyncRelayCommand InfoCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }
    public AsyncRelayCommand HelpCommand { get; }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string Ip
    {
        get => _ip;
        set => SetProperty(ref _ip, value);
    }

    public string AcId
    {
        get => _acId;
        set => SetProperty(ref _acId, value);
    }

    public string Domain
    {
        get => _domain;
        set => SetProperty(ref _domain, value);
    }

    public bool SaveConfig
    {
        get => _saveConfig;
        set
        {
            if (!SetProperty(ref _saveConfig, value))
                return;

            if (value)
                SaveCurrentConfig();
            else
                _configService.Delete();
        }
    }

    public bool AutoLogin
    {
        get => _autoLogin;
        set => SetProperty(ref _autoLogin, value);
    }

    public bool LoopEnabled
    {
        get => _loopEnabled;
        set
        {
            if (!SetProperty(ref _loopEnabled, value))
                return;

            if (value)
                StartLoopDetection();
            else
                StopLoopDetection();

            if (SaveConfig)
                SaveCurrentConfig();
        }
    }

    public string LoopInterval
    {
        get => _loopInterval;
        set => SetProperty(ref _loopInterval, value);
    }

    public string LoopTimeout
    {
        get => _loopTimeout;
        set => SetProperty(ref _loopTimeout, value);
    }

    public string PingHost
    {
        get => _pingHost;
        set => SetProperty(ref _pingHost, value);
    }

    public bool IgnoreTlsErrors
    {
        get => _ignoreTlsErrors;
        set => SetProperty(ref _ignoreTlsErrors, value);
    }

    public string LoopStatus
    {
        get => _loopStatus;
        set => SetProperty(ref _loopStatus, value);
    }

    public string Output
    {
        get => _output;
        set => SetProperty(ref _output, value);
    }

    public bool CanAutoLogin =>
        AutoLogin &&
        !string.IsNullOrWhiteSpace(Url) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);

    public async Task LoginAsync()
    {
        if (!ValidateLogin())
            return;

        _operationCts?.Cancel();
        _operationCts = new CancellationTokenSource();

        if (SaveConfig)
            SaveCurrentConfig();

        Log("=== 登录尝试 ===");

        try
        {
            using var portal = CreatePortal(includePassword: true);
            portal.DebugLog = LogDebug;

            var userProvidedIp = !string.IsNullOrWhiteSpace(Ip);
            var userProvidedAcId = !string.IsNullOrWhiteSpace(AcId);

            if (!userProvidedIp && !userProvidedAcId)
            {
                await portal.DetectInfoAsync(_operationCts.Token);
                Ip = portal.GetDetectedIp() ?? "";
                AcId = portal.GetDetectedAcId() ?? "";
                Log($"自动检测结果 - IP: {Ip}, AC_ID: {AcId}");
            }

            var result = await portal.LoginAsync(_operationCts.Token);
            Log($"结果: {result.Error}");

            if (!result.IsSuccess && (userProvidedIp || userProvidedAcId))
            {
                Log("使用用户指定参数登录失败，尝试自动检测后重试...");
                await portal.DetectInfoAsync(_operationCts.Token);
                Ip = portal.GetDetectedIp() ?? "";
                AcId = portal.GetDetectedAcId() ?? "";
                Log($"自动检测结果 - IP: {Ip}, AC_ID: {AcId}");
                result = await portal.LoginAsync(_operationCts.Token);
                Log($"结果: {result.Error}");
            }

            if (result.IsSuccess)
            {
                Log("[登录] 登录成功");
                await QueryStatusAsync(portal);
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? result.Ecode ?? "未知错误";
                Log($"错误：{error}");
                WpfMessageBox.Show($"登录失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            Log("[登录] 已取消");
        }
        catch (Exception ex)
        {
            Log($"异常：{ex.Message}");
            WpfMessageBox.Show($"错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task QueryInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowValidationErrors(ValidateCommonInputs(requirePassword: false));
            return;
        }

        var errors = ValidateCommonInputs(requirePassword: false);
        if (errors.Count > 0)
        {
            ShowValidationErrors(errors);
            return;
        }

        Log("=== 查询状态 ===");

        try
        {
            using var portal = CreatePortal(includePassword: false);
            portal.DebugLog = LogDebug;
            await portal.DetectInfoAsync();
            Ip = portal.GetDetectedIp() ?? Ip;
            AcId = portal.GetDetectedAcId() ?? AcId;
            Log($"检测结果 - IP: {Ip}, AC_ID: {AcId}");
            await QueryStatusAsync(portal);
        }
        catch (Exception ex)
        {
            Log($"错误：{ex.Message}");
            WpfMessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task LogoutAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowValidationErrors(ValidateCommonInputs(requirePassword: false));
            return;
        }

        var errors = ValidateCommonInputs(requirePassword: false);
        if (errors.Count > 0)
        {
            ShowValidationErrors(errors);
            return;
        }

        Log("=== 注销 ===");

        try
        {
            using var portal = CreatePortal(includePassword: false);
            portal.DebugLog = LogDebug;
            await portal.DetectInfoAsync();
            Ip = portal.GetDetectedIp() ?? Ip;
            AcId = portal.GetDetectedAcId() ?? AcId;
            Log($"检测结果 - IP: {Ip}, AC_ID: {AcId}");

            var result = await portal.LogoutAsync();
            if (result.IsSuccess)
            {
                Log("注销成功");
                WpfMessageBox.Show("注销成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? "未知错误";
                Log($"错误：{error}");
                WpfMessageBox.Show($"注销失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Log($"错误：{ex.Message}");
        }
    }

    public Task ShowHelpAsync()
    {
        var helpText = """
========== 填写说明 ==========
网关地址：校园网认证服务器 URL，通常是 http://10.0.0.1 或 http://192.168.0.1
用户名：校园网账号，通常是学号或工号
密码：校园网密码
IP地址：本机在校园网中的 IP，留空自动获取
AC ID：认证设备编号，留空自动获取，登录失败可尝试手动指定
域：留空为普通账号，部分校园网支持多域认证，可选：
  @ct         - 中国电信用户
  @cm         - 中国移动用户
  @cu         - 中国联通用户
  @edu.cn     - 教育网/校园网用户
  @student    - 学生账号
  @teacher    - 教师账号

========== 常见问题 ==========
1. 无法获取 IP -> 手动填写 IP 地址
2. ac_id 错误 -> 尝试手动指定 AC ID
3. 当前已在线 -> 账号已登录成功，无需重复登录
4. 认证加密错误 -> AC ID 不正确，尝试更换
5. 在线设备超限 -> 先登出其他设备
""";
        Log("");
        Log(helpText);
        return Task.CompletedTask;
    }

    private void LoadConfig()
    {
        try
        {
            var config = _configService.Load();
            if (config == null)
                return;

            Url = config.Url ?? "http://10.0.0.1";
            Username = config.Username ?? "";
            Password = config.Password ?? "";
            Ip = config.Ip ?? "";
            Domain = config.Domain ?? "@edu.cn";
            AcId = config.AcId ?? "";
            AutoLogin = config.AutoLogin;
            IgnoreTlsErrors = config.IgnoreTlsErrors;

            if (config.Loop != null)
            {
                LoopInterval = config.Loop.Interval.ToString();
                LoopTimeout = config.Loop.Timeout.ToString();
                PingHost = config.Loop.PingHost;
                _loopEnabled = config.Loop.Enable;
                OnPropertyChanged(nameof(LoopEnabled));
            }
        }
        catch (Exception ex)
        {
            Log($"[配置] 加载失败: {ex.Message}");
        }
    }

    public void SaveCurrentConfig()
    {
        try
        {
            var config = new Config
            {
                Url = string.IsNullOrWhiteSpace(Url) ? "http://10.0.0.1" : Url.Trim(),
                Username = Username.Trim(),
                Password = Password,
                Ip = Ip.Trim(),
                Domain = Domain.Trim(),
                AcId = AcId.Trim(),
                AutoLogin = AutoLogin,
                IgnoreTlsErrors = IgnoreTlsErrors,
                Loop = new LoopConfig
                {
                    Enable = LoopEnabled,
                    Interval = ParseInt(LoopInterval, 60, 5),
                    Timeout = ParseInt(LoopTimeout, 5, 1),
                    PingHost = string.IsNullOrWhiteSpace(PingHost) ? "www.baidu.com" : PingHost.Trim()
                }
            };

            _configService.Save(config);
            Log("[配置] 已保存");
        }
        catch (Exception ex)
        {
            Log($"[配置] 保存失败：{ex.Message}");
        }
    }

    public void Shutdown()
    {
        StopLoopDetection();
        _operationCts?.Cancel();
        _operationCts?.Dispose();
    }

    public void StartConfiguredLoopDetection()
    {
        if (LoopEnabled)
            StartLoopDetection();
    }

    private bool ValidateLogin()
    {
        var errors = ValidateCommonInputs(requirePassword: true);
        if (errors.Count > 0)
        {
            ShowValidationErrors(errors);
            return false;
        }

        return true;
    }

    private List<string> ValidateCommonInputs(bool requirePassword)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Url))
            errors.Add("请输入网关地址。");
        else if (!Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors.Add("网关地址必须是有效的 HTTP/HTTPS URL。");

        if (string.IsNullOrWhiteSpace(Username))
            errors.Add("请输入用户名。");

        if (requirePassword && string.IsNullOrWhiteSpace(Password))
            errors.Add("请输入密码。");

        if (!string.IsNullOrWhiteSpace(AcId) && !int.TryParse(AcId.Trim(), out _))
            errors.Add("AC ID 必须是数字，或留空自动获取。");

        if (!string.IsNullOrWhiteSpace(Ip) && !IPAddress.TryParse(Ip.Trim(), out _))
            errors.Add("IP 地址格式不正确，或留空自动获取。");

        if (!int.TryParse(LoopInterval, out var interval) || interval < 5)
            errors.Add("检测间隔必须是大于等于 5 的整数秒。");

        if (!int.TryParse(LoopTimeout, out var timeout) || timeout < 1)
            errors.Add("网络超时必须是大于等于 1 的整数秒。");

        if (!IsValidHost(PingHost))
            errors.Add("Ping 主机必须是有效域名或 IP 地址。");

        return errors;
    }

    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;

        host = host.Trim();
        if (IPAddress.TryParse(host, out _))
            return true;

        return Uri.CheckHostName(host) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    private static void ShowValidationErrors(IReadOnlyCollection<string> errors)
    {
        if (errors.Count == 0)
            return;

        WpfMessageBox.Show(string.Join(Environment.NewLine, errors), "输入检查", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private SrunPortal CreatePortal(bool includePassword)
    {
        return new SrunPortal(
            string.IsNullOrWhiteSpace(Url) ? "http://10.0.0.1" : Url.Trim(),
            Username.Trim(),
            includePassword ? Password : "",
            string.IsNullOrWhiteSpace(AcId) ? null : AcId.Trim(),
            string.IsNullOrWhiteSpace(Ip) ? null : Ip.Trim(),
            Domain.Trim(),
            IgnoreTlsErrors);
    }

    private async Task QueryStatusAsync(SrunPortal portal)
    {
        try
        {
            var info = await portal.GetUserInfoAsync();
            Log("");
            Log("--- 用户信息 ---");
            Log($"账号：{info.UserName ?? "N/A"}");
            Log($"IP：{info.OnlineIp ?? info.ClientIp ?? "N/A"}");
            Log($"MAC：{info.UserMac ?? "N/A"}");

            if (info.SumBytes > 0)
                Log($"已用流量：{Formatters.FormatFlow(info.SumBytes)}");
            if (info.SumSeconds > 0)
                Log($"已用时长：{Formatters.FormatTime(info.SumSeconds)}");
            if (info.UserBalance.HasValue)
                Log($"余额：{info.UserBalance:F2}");
        }
        catch (Exception ex)
        {
            Log($"查询用户信息失败: {ex.Message}");
        }

        try
        {
            var expire = await portal.GetExpireTimeAsync();
            if (expire.HasValue)
                Log($"到期时间：{expire:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Log($"查询到期时间失败: {ex.Message}");
        }

        Log("");
    }

    private void StartLoopDetection()
    {
        StopLoopDetection();
        _loopCts = new CancellationTokenSource();
        _ = RunLoopDetectionAsync(_loopCts.Token);
    }

    private void StopLoopDetection()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
        LoopStatus = "状态：已停止";
    }

    private async Task RunLoopDetectionAsync(CancellationToken cancellationToken)
    {
        var interval = ParseInt(LoopInterval, 60, 5);
        var failureCount = 0;
        LoopStatus = $"状态：检测中（{interval}秒）";
        Log("[循环检测] 已启动");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var backoffSeconds = Math.Min(failureCount * interval, interval * 5);
                await Task.Delay(TimeSpan.FromSeconds(interval + backoffSeconds), cancellationToken);
                var success = await PerformLoopCheckAsync(cancellationToken);
                failureCount = success ? 0 : Math.Min(failureCount + 1, 5);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                failureCount = Math.Min(failureCount + 1, 5);
                LoopStatus = "状态：检测异常";
                Log($"[循环检测] 异常: {ex.Message}");
            }
        }

        Log("[循环检测] 已停止");
    }

    private async Task<bool> PerformLoopCheckAsync(CancellationToken cancellationToken)
    {
        if (!await _loopCheckGate.WaitAsync(0, cancellationToken))
        {
            Log("[循环检测] 上一次检查仍在运行，跳过本轮");
            return true;
        }

        try
        {
            var online = await CheckNetworkOnlineAsync(cancellationToken);
            var interval = ParseInt(LoopInterval, 60, 5);

            if (online)
            {
                LoopStatus = $"状态：在线（{interval}秒）";
                return true;
            }

            LoopStatus = "状态：网络离线，尝试登录...";
            Log("[循环检测] 网络离线，尝试自动登录");

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                Log("[循环检测] 缺少用户名或密码，跳过自动登录");
                return false;
            }

            using var portal = CreatePortal(includePassword: true);
            portal.DebugLog = LogDebug;
            await portal.DetectInfoAsync(cancellationToken);
            var result = await portal.LoginAsync(cancellationToken);

            if (result.IsSuccess)
            {
                LoopStatus = "状态：登录成功";
                Log("[循环检测] 登录成功");
                return true;
            }

            var error = result.ErrorMsg ?? result.Error ?? "未知错误";
            LoopStatus = "状态：登录失败";
            Log($"[循环检测] 登录失败: {error}");
            return false;
        }
        finally
        {
            _loopCheckGate.Release();
        }
    }

    private async Task<bool> CheckNetworkOnlineAsync(CancellationToken cancellationToken)
    {
        try
        {
            var timeout = ParseInt(LoopTimeout, 5, 1);
            var host = string.IsNullOrWhiteSpace(PingHost) ? "www.baidu.com" : PingHost.Trim();
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeout * 1000).WaitAsync(cancellationToken);
            return reply.Status == IPStatus.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private void Log(string text)
    {
        var line = text + Environment.NewLine;
        WpfApplication.Current.Dispatcher.Invoke(() => Output += line);
        _logService.Write(text);
    }

    private void LogDebug(string text)
    {
        _logService.Write(text);
    }

    private static int ParseInt(string value, int fallback, int min)
    {
        return int.TryParse(value, out var parsed) && parsed >= min ? parsed : fallback;
    }
}
