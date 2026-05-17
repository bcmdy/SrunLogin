using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using SrunLogin.Crypto;
using SrunLogin.Models;
using SrunLogin.Services;

namespace SrunLogin.GUI;

public partial class MainForm : Form
{
    [DllImport("user32.dll")]
    private static extern int ShowScrollBar(IntPtr hWnd, int wBar, int bShow);

    private const int SB_VERT = 1;
    private const int SB_HORZ = 0;

    [DllImport("user32.dll")]
    private static extern bool SetScrollInfo(IntPtr hWnd, int fnBar, ref SCROLLINFO lpsi, bool fRedraw);

    [StructLayout(LayoutKind.Sequential)]
    private struct SCROLLINFO
    {
        public int cbSize;
        public int fMask;
        public int nMin;
        public int nMax;
        public int nPage;
        public int nPos;
        public int nTrackPos;
    }

    private const int SIF_RANGE = 0x0001;
    private const int SIF_PAGE = 0x0002;
    private const int SIF_DISABLENOSCROLL = 0x0003;

    private TextBox _txtUrl = null!;
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private TextBox _txtIp = null!;
    private TextBox _txtAcId = null!;
    private TextBox _txtDomain = null!;
    private Button _btnLogin = null!;
    private Button _btnLogout = null!;
    private Button _btnInfo = null!;
    private RichTextBox _txtOutput = null!;
    private CheckBox _chkSaveConfig = null!;
    private CheckBox _chkShowPassword = null!;
    private CheckBox _chkAutoLogin = null!;

    private static readonly Color ColorBg = Color.FromArgb(248, 249, 250);
    private static readonly Color ColorPrimary = Color.FromArgb(0, 120, 215);
    private static readonly Color ColorInfo = Color.FromArgb(0, 150, 136);
    private static readonly Color ColorDanger = Color.FromArgb(244, 67, 54);
    private static readonly Color ColorWarning = Color.FromArgb(255, 193, 7);
    private static readonly Color ColorText = Color.FromArgb(33, 37, 41);
    private static readonly Color ColorLabel = Color.FromArgb(108, 117, 125);
    private static readonly Color ColorBorder = Color.FromArgb(206, 212, 218);

    private string ConfigPath
    {
        get
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "config.json");
        }
    }

    private string LogPath
    {
        get
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "app.log");
        }
    }

    public MainForm()
    {
        this.AutoScaleMode = AutoScaleMode.None;
        InitializeComponent();
        LoadConfig();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ActiveControl = null;

        // 自动登录
        if (_chkAutoLogin.Checked)
        {
            var url = GetActualText(_txtUrl, "");
            var username = GetActualText(_txtUsername, "");
            var password = GetActualText(_txtPassword, "");

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                BtnLogin_Click(null, EventArgs.Empty);
            }
        }
    }

    private void InitializeComponent()
    {
        Text = "校园网认证工具";
        Size = new Size(640, 680);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = ColorBg;
        Font = new Font("Segoe UI", 9F);

        int marginX = 35;
        int startY = 20;

        // 标题
        var lblTitle = new Label
        {
            Text = "校园网登录",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(marginX, startY),
            Size = new Size(250, 32),
            ForeColor = ColorText
        };

        // 分隔线
        var line = new Panel
        {
            Location = new Point(marginX, startY + 36),
            Size = new Size(560, 1),
            BackColor = ColorBorder
        };

        // 表单区域
        int y = startY + 55;
        int lblW = 85;
        int inputX = marginX + lblW + 12;
        int inputW = 565 - lblW - marginX * 2;

        // 网关地址
        var lblUrl = CreateLabel("网关地址", marginX, y);
        _txtUrl = CreateModernTextBox(inputX, y, inputW, "例:http://10.0.0.1");
        y += 46;

        // 用户名
        var lblUser = CreateLabel("用户名", marginX, y);
        _txtUsername = CreateModernTextBox(inputX, y, inputW, "例:学号/工号");
        y += 46;

        // 密码
        var lblPwd = CreateLabel("密码", marginX, y);
        _txtPassword = CreateModernTextBox(inputX, y, 170, "例:密码");
        _txtPassword.UseSystemPasswordChar = true;

        int checkY = y + 2;

        _chkShowPassword = new CheckBox
        {
            Text = "显示",
            Location = new Point(inputX + 190, checkY),
            Size = new Size(50, 20),
            FlatStyle = FlatStyle.Flat,
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 8.5F)
        };
        _chkShowPassword.CheckedChanged += (s, e) =>
        {
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
        };

        _chkSaveConfig = new CheckBox
        {
            Text = "保存配置",
            Location = new Point(inputX + 245, checkY),
            Size = new Size(80, 20),
            FlatStyle = FlatStyle.Flat,
            Checked = true,
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 8.5F)
        };
        _chkSaveConfig.CheckedChanged += (s, e) =>
        {
            if (_chkSaveConfig.Checked) SaveConfig(); else DeleteConfig();
        };

        _chkAutoLogin = new CheckBox
        {
            Text = "自动登录",
            Location = new Point(inputX + 330, checkY),
            Size = new Size(80, 20),
            FlatStyle = FlatStyle.Flat,
            Checked = true,
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 8.5F)
        };
        y += 46;

        // IP & AC ID 同行
        var lblIp = CreateLabel("IP 地址", marginX, y);
        _txtIp = CreateModernTextBox(inputX, y, 170, "留空自动获取");

        var lblAcId = new Label
        {
            Text = "AC ID:",
            Location = new Point(inputX + 190, y + 2),
            Size = new Size(45, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F)
        };
        _txtAcId = CreateModernTextBox(inputX + 240, y, inputW - 240, "留空自动获取");
        y += 46;

        // 域
        var lblDomain = CreateLabel("域", marginX, y);
        _txtDomain = CreateModernTextBox(inputX, y, inputW, "@edu.cn");
        y += 58;

        // 按钮区域 - 等宽均匀分布
        const int btnWidth = 125;
        const int btnHeight = 34;
        const int btnSpacing = 12;
        int totalWidth = btnWidth * 4 + btnSpacing * 3;
        int btnStartX = (ClientSize.Width - totalWidth) / 2;

        _btnLogin = CreateFlatButton("登录", btnStartX, y, btnWidth, btnHeight, ColorPrimary);
        _btnLogin.Click += BtnLogin_Click;

        _btnInfo = CreateFlatButton("查询状态", btnStartX + btnWidth + btnSpacing, y, btnWidth, btnHeight, ColorInfo);
        _btnInfo.Click += BtnInfo_Click;

        _btnLogout = CreateFlatButton("登出", btnStartX + (btnWidth + btnSpacing) * 2, y, btnWidth, btnHeight, ColorDanger);
        _btnLogout.Click += BtnLogout_Click;

        var btnHelp = CreateFlatButton("填写帮助", btnStartX + (btnWidth + btnSpacing) * 3, y, btnWidth, btnHeight, ColorWarning);
        btnHelp.ForeColor = ColorText;
        btnHelp.Click += (s, e) => ShowHelp();
        y += 55;

        // 输出区域
        var lblOutput = new Label
        {
            Text = "输出日志",
            Location = new Point(marginX, y),
            Size = new Size(100, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        _txtOutput = new RichTextBox
        {
            Location = new Point(1, 1),
            Size = new Size(558, 248),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BackColor = Color.White,
            ForeColor = ColorText,
            Font = new Font("Consolas", 9.5F),
            BorderStyle = BorderStyle.None,
            AcceptsTab = true
        };

        var outputPanel = new Panel
        {
            Location = new Point(marginX, y + 22),
            Size = new Size(560, 250),
            BackColor = ColorBorder,
            Padding = new Padding(1)
        };
        outputPanel.Controls.Add(_txtOutput);

        // 强制显示垂直滚动条
        var si = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_DISABLENOSCROLL, nMin = 0, nMax = 100, nPage = 100 };
        SetScrollInfo(_txtOutput.Handle, SB_VERT, ref si, true);
        ShowScrollBar(_txtOutput.Handle, SB_VERT, 1);

        Controls.AddRange(new Control[]
        {
            lblTitle, line,
            lblUrl, _txtUrl, lblUser, _txtUsername, lblPwd, _txtPassword, _chkShowPassword,
            lblIp, _txtIp, lblAcId, _txtAcId, lblDomain, _txtDomain, _chkSaveConfig, _chkAutoLogin,
            _btnLogin, _btnInfo, _btnLogout, btnHelp,
            lblOutput, outputPanel
        });
    }

    private Label CreateLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y + 2),
            Size = new Size(85, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private TextBox CreateModernTextBox(int x, int y, int width, string placeholder)
    {
        var txt = new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 26),
            Tag = placeholder,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F),
            BackColor = ColorBg,
            ForeColor = Color.Gray
        };

        txt.Text = placeholder;

        // 底部线条
        var underline = new Panel
        {
            Location = new Point(x, y + 24),
            Size = new Size(width, 1),
            BackColor = ColorBorder
        };
        Controls.Add(underline);

        txt.GotFocus += (s, e) =>
        {
            if (txt.Text == placeholder)
            {
                txt.Text = "";
                txt.ForeColor = ColorText;
                if (placeholder == "例:密码")
                    txt.UseSystemPasswordChar = true;
            }
            underline.BackColor = ColorPrimary;
            underline.Height = 2;
        };

        txt.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text))
            {
                txt.Text = placeholder;
                txt.ForeColor = Color.Gray;
                if (placeholder == "例:密码")
                    txt.UseSystemPasswordChar = true;
            }
            underline.BackColor = ColorBorder;
            underline.Height = 1;
        };

        return txt;
    }

    private Button CreateFlatButton(string text, int x, int y, int w, int h, Color backColor)
    {
        return new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<Config>(json);
                if (config != null)
                {
                    // 直接设置值，不检查placeholder
                    _txtUrl.Text = config.Url ?? "http://10.0.0.1";
                    _txtUrl.ForeColor = ColorText;

                    _txtUsername.Text = config.Username ?? "";
                    _txtUsername.ForeColor = ColorText;

                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        _txtPassword.Text = config.Password;
                        _txtPassword.ForeColor = ColorText;
                        _txtPassword.UseSystemPasswordChar = true;
                    }

                    _txtDomain.Text = config.Domain ?? "@edu.cn";
                    _txtDomain.ForeColor = ColorText;

                    if (!string.IsNullOrEmpty(config.Ip))
                    {
                        _txtIp.Text = config.Ip;
                        _txtIp.ForeColor = ColorText;
                    }

                    if (!string.IsNullOrEmpty(config.AcId))
                    {
                        _txtAcId.Text = config.AcId;
                        _txtAcId.ForeColor = ColorText;
                    }

                    _chkSaveConfig.Checked = true;
                    _chkAutoLogin.Checked = config.AutoLogin;
                }
            }
        }
        catch { }
    }

    private void SetTextBoxIfPlaceholder(TextBox txt, string value)
    {
        if (txt.Text == txt.Tag?.ToString() || string.IsNullOrWhiteSpace(txt.Text))
        {
            txt.Text = value;
            txt.ForeColor = ColorText;
        }
    }

    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var config = new Config
            {
                Url = GetActualText(_txtUrl, "http://10.0.0.1"),
                Username = GetActualText(_txtUsername),
                Password = GetActualText(_txtPassword, ""),
                Ip = IsPlaceholder(_txtIp) ? "" : _txtIp.Text.Trim(),
                Domain = GetActualText(_txtDomain, ""),
                AcId = IsPlaceholder(_txtAcId) ? "" : _txtAcId.Text.Trim(),
                AutoLogin = _chkAutoLogin.Checked
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
            Log("[配置] 已保存");
        }
        catch (Exception ex)
        {
            Log($"[配置] 保存失败：{ex.Message}");
        }
    }

    private void DeleteConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                File.Delete(ConfigPath);
            Log("[配置] 已删除");
        }
        catch { }
    }

    private class Config
    {
        public string? Url { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Ip { get; set; }
        public string? Domain { get; set; }
        public string? AcId { get; set; }
        public bool AutoLogin { get; set; }
    }

    private void ShowHelp()
    {
        var helpText = @"========== 填写说明 ==========
网关地址：校园网认证服务器URL，通常是 http://10.0.0.1 或 http://192.168.0.1，可在浏览器打开任意网页自动跳转获取
用户名：校园网账号，通常是学号或工号
密码：校园网密码
IP地址：本机在校园网中的IP，留空自动获取
AC ID：认证设备编号，留空自动获取，登录失败可尝试手动指定
域：留空为普通账号，部分校园网支持多域认证，可选：
  @ct         - 中国电信用户
  @cm         - 中国移动用户
  @cu         - 中国联通用户
  @edu.cn     - 教育网/校园网用户
  @student    - 学生账号
  @teacher    - 教师账号

========== 常见问题 ==========
1. 无法获取IP → 手动填写IP地址
2. ac_id错误 → 尝试手动指定AC ID
3. 当前已在线 → 账号已登录成功，无需重复登录
4. 认证加密错误 → AC ID不正确，尝试更换
5. 在线设备超限 → 先登出其他设备
";
        _txtOutput.Text = helpText;
    }

    private static string GetActualText(TextBox txt, string defaultValue = "")
    {
        var placeholder = txt.Tag?.ToString() ?? "";
        if (txt.Text == placeholder)
            return defaultValue;
        return txt.Text.Trim();
    }

    private static bool IsPlaceholder(TextBox txt)
    {
        var placeholder = txt.Tag?.ToString() ?? "";
        return string.IsNullOrWhiteSpace(txt.Text) || txt.Text == placeholder;
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        var url = GetActualText(_txtUrl, "http://10.0.0.1");
        var username = GetActualText(_txtUsername);
        var password = GetActualText(_txtPassword, "");
        var ip = IsPlaceholder(_txtIp) ? null : _txtIp.Text.Trim();
        var acId = IsPlaceholder(_txtAcId) ? null : _txtAcId.Text.Trim();
        var domain = GetActualText(_txtDomain, "");

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("请输入网关地址", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("请输入用户名", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("请输入密码", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_chkSaveConfig.Checked)
            SaveConfig();

        SetButtonsEnabled(false);
        Log("=== 登录尝试 ===");

        // 用户是否手动指定了 IP 或 AC_ID
        bool userProvidedIp = !IsPlaceholder(_txtIp);
        bool userProvidedAcId = !IsPlaceholder(_txtAcId);

        try
        {
            var portal = new SrunPortal(url, username, password, acId, ip, domain);
            portal.DebugLog = LogDebug;

            // 如果用户没有提供 IP/AC_ID，先自动检测
            if (!userProvidedIp && !userProvidedAcId)
            {
                await portal.DetectInfoAsync();
                ip = portal.GetDetectedIp();
                acId = portal.GetDetectedAcId();
                Log($"自动检测结果 - IP: {ip}, AC_ID: {acId}");
            }

            var result = await portal.LoginAsync();
            Log($"结果: {result.Error}");

            // 如果用户提供了信息但登录失败，尝试自动检测后重试
            if (!result.IsSuccess && (userProvidedIp || userProvidedAcId))
            {
                Log("使用用户指定参数登录失败，尝试自动检测...");
                await portal.DetectInfoAsync();
                ip = portal.GetDetectedIp();
                acId = portal.GetDetectedAcId();
                Log($"自动检测结果 - IP: {ip}, AC_ID: {acId}");
                result = await portal.LoginAsync();
                Log($"结果: {result.Error}");
            }

            if (result.IsSuccess)
            {
                MessageBox.Show("登录成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await QueryStatus(portal);
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? result.Ecode ?? "未知错误";
                MessageBox.Show($"登录失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"错误：{error}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log($"异常：{ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void BtnInfo_Click(object? sender, EventArgs e)
    {
        var username = GetActualText(_txtUsername);
        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("请输入用户名", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var url = GetActualText(_txtUrl, "http://10.0.0.1");
        var domain = GetActualText(_txtDomain, "");

        SetButtonsEnabled(false);
        Log("=== 查询状态 ===");

        try
        {
            var portal = new SrunPortal(url, username, "", null, null, domain);
            portal.DebugLog = LogDebug;
            await portal.DetectInfoAsync();
            Log($"检测结果 - IP: {portal.GetDetectedIp()}, AC_ID: {portal.GetDetectedAcId()}");
            await QueryStatus(portal);
        }
        catch (Exception ex)
        {
            Log($"错误：{ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void BtnLogout_Click(object? sender, EventArgs e)
    {
        var username = GetActualText(_txtUsername);
        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("请输入用户名", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var url = GetActualText(_txtUrl, "http://10.0.0.1");
        var domain = GetActualText(_txtDomain, "");

        SetButtonsEnabled(false);
        Log("=== 登出 ===");

        try
        {
            var portal = new SrunPortal(url, username, "", null, null, domain);
            portal.DebugLog = LogDebug;
            await portal.DetectInfoAsync();
            Log($"检测结果 - IP: {portal.GetDetectedIp()}, AC_ID: {portal.GetDetectedAcId()}");
            var result = await portal.LogoutAsync();

            if (result.IsSuccess)
            {
                MessageBox.Show("登出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log("登出成功");
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? "未知错误";
                MessageBox.Show($"登出失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"错误：{error}");
            }
        }
        catch (Exception ex)
        {
            Log($"错误：{ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async Task QueryStatus(SrunPortal portal)
    {
        try
        {
            var info = await portal.GetUserInfoAsync();
            Log("\n--- 用户信息 ---");
            Log($"账号：{info.UserName ?? "N/A"}");
            Log($"IP：{info.OnlineIp ?? info.ClientIp ?? "N/A"}");
            Log($"MAC：{info.UserMac ?? "N/A"}");

            if (info.SumBytes > 0)
                Log($"已用流量：{FormatFlow(info.SumBytes.Value)}");
            if (info.SumSeconds > 0)
                Log($"已用时长：{FormatTime(info.SumSeconds.Value)}");
            if (info.UserBalance.HasValue)
                Log($"余额：{info.UserBalance:F2}");
        }
        catch { }

        try
        {
            var expire = await portal.GetExpireTimeAsync();
            if (expire.HasValue)
                Log($"到期时间：{expire:yyyy-MM-dd HH:mm:ss}");
        }
        catch { }

        Log("");
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _btnLogin.Enabled = enabled;
        _btnInfo.Enabled = enabled;
        _btnLogout.Enabled = enabled;
    }

    private void AppendOutput(string text)
    {
        _txtOutput.AppendText(text);
    }

    private enum LogLevel { Info, Debug }

    private void Log(string text, LogLevel level = LogLevel.Info)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] {text}\n";

        try
        {
            File.AppendAllText(LogPath, logLine);
        }
        catch { }

        if (level == LogLevel.Info)
        {
            AppendOutput(text + "\n");
        }
    }

    private void LogDebug(string text)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] {text}\n";

        try
        {
            File.AppendAllText(LogPath, logLine);
        }
        catch { }
    }

    private static string FormatFlow(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double val = bytes;
        int idx = 0;
        while (val >= 1024 && idx < units.Length - 1) { val /= 1024; idx++; }
        return $"{val:F2} {units[idx]}";
    }

    private static string FormatTime(long seconds)
    {
        var d = seconds / 86400;
        var h = (seconds % 86400) / 3600;
        var m = (seconds % 3600) / 60;
        var s = seconds % 60;
        var parts = new List<string>();
        if (d > 0) parts.Add($"{d}天");
        if (h > 0) parts.Add($"{h}小时");
        if (m > 0) parts.Add($"{m}分");
        if (s > 0 || parts.Count == 0) parts.Add($"{s}秒");
        return string.Join("", parts);
    }
}