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

    private string? _lastAcId;
    private string? _lastIp;

    private string ConfigPath
    {
        get
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "config.json");
        }
    }

    public MainForm()
    {
        // 禁用DPI缩放
        this.AutoScaleMode = AutoScaleMode.None;

        InitializeComponent();
        LoadConfig();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ActiveControl = null;
    }

    private void InitializeComponent()
    {
        Text = "校园网认证工具";
        Size = new Size(580, 610);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(240, 240, 240);

        var lblTitle = new Label
        {
            Text = "校园网登录",
            Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
            Location = new Point(20, 15),
            Size = new Size(200, 30)
        };

        // 网关地址
        var lblUrl = new Label { Text = "网关地址:", Location = new Point(20, 60), Size = new Size(90, 20) };
        _txtUrl = CreatePlaceholderTextBox(115, 58, 420, 22, "http://10.0.0.1");

        // 用户名
        var lblUser = new Label { Text = "用户名:", Location = new Point(20, 90), Size = new Size(90, 20) };
        _txtUsername = CreatePlaceholderTextBox(115, 88, 420, 22, "请输入用户名");

        // 密码
        var lblPwd = new Label { Text = "密码:", Location = new Point(20, 120), Size = new Size(90, 20) };
        _txtPassword = CreatePlaceholderTextBox(115, 118, 340, 22, "请输入密码");
        _txtPassword.UseSystemPasswordChar = true;

        _chkShowPassword = new CheckBox
        {
            Text = "显示",
            Location = new Point(460, 118),
            Size = new Size(60, 20),
            FlatStyle = FlatStyle.Flat
        };
        _chkShowPassword.CheckedChanged += (s, e) =>
        {
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
        };

        // IP地址
        var lblIp = new Label { Text = "IP地址:", Location = new Point(20, 150), Size = new Size(90, 20) };
        _txtIp = CreatePlaceholderTextBox(115, 148, 150, 22, "留空自动获取");

        // AC_ID
        var lblAcId = new Label { Text = "AC ID:", Location = new Point(280, 150), Size = new Size(55, 20) };
        _txtAcId = CreatePlaceholderTextBox(340, 148, 195, 22, "留空自动获取");

        // 域
        var lblDomain = new Label { Text = "域:", Location = new Point(20, 180), Size = new Size(90, 20) };
        _txtDomain = CreatePlaceholderTextBox(115, 178, 300, 22, "@edu.cn");

        // 保存配置
        _chkSaveConfig = new CheckBox
        {
            Text = "保存配置",
            Location = new Point(415, 178),
            Size = new Size(100, 20),
            FlatStyle = FlatStyle.Flat,
            Checked = true
        };
        _chkSaveConfig.CheckedChanged += (s, e) =>
        {
            if (_chkSaveConfig.Checked)
                SaveConfig();
            else
                DeleteConfig();
        };

        // 按钮（平铺到窗口，相同宽度）
        const int btnWidth = 120;
        const int btnSpacing = 10;
        const int btnStartX = 35;
        _btnLogin = new Button
        {
            Text = "登录",
            Location = new Point(btnStartX, 220),
            Size = new Size(btnWidth, 32),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogin.Click += BtnLogin_Click;

        _btnInfo = new Button
        {
            Text = "查询状态",
            Location = new Point(btnStartX + btnWidth + btnSpacing, 220),
            Size = new Size(btnWidth, 32),
            BackColor = Color.FromArgb(0, 150, 136),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnInfo.Click += BtnInfo_Click;

        _btnLogout = new Button
        {
            Text = "登出",
            Location = new Point(btnStartX + (btnWidth + btnSpacing) * 2, 220),
            Size = new Size(btnWidth, 32),
            BackColor = Color.FromArgb(244, 67, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogout.Click += BtnLogout_Click;

        var btnHelp = new Button
        {
            Text = "填写帮助",
            Location = new Point(btnStartX + (btnWidth + btnSpacing) * 3, 220),
            Size = new Size(btnWidth, 32),
            BackColor = Color.Yellow,
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnHelp.Click += (s, e) => ShowHelp();

        // 输出
        var lblOutput = new Label { Text = "输出:", Location = new Point(20, 260), Size = new Size(100, 20) };
        _txtOutput = new RichTextBox
        {
            Location = new Point(20, 280),
            Size = new Size(520, 290),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            BackColor = Color.White,
            Font = new Font("Consolas", 9),
            AcceptsTab = true
        };
        // 强制显示垂直滚动条
        var si = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_DISABLENOSCROLL, nMin = 0, nMax = 100, nPage = 100 };
        SetScrollInfo(_txtOutput.Handle, SB_VERT, ref si, true);
        ShowScrollBar(_txtOutput.Handle, SB_VERT, 1);

        Controls.AddRange(new Control[]
        {
            lblTitle, lblUrl, _txtUrl, lblUser, _txtUsername, lblPwd, _txtPassword, _chkShowPassword, lblIp, _txtIp, lblAcId, _txtAcId,
            lblDomain, _txtDomain, _chkSaveConfig,
            _btnLogin, _btnInfo, _btnLogout, btnHelp, lblOutput, _txtOutput
        });

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
                    SetTextBoxIfPlaceholder(_txtUrl, config.Url ?? "http://10.0.0.1");
                    SetTextBoxIfPlaceholder(_txtUsername, config.Username ?? "");
                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        SetTextBoxIfPlaceholder(_txtPassword, config.Password);
                    }
                    SetTextBoxIfPlaceholder(_txtDomain, config.Domain ?? "@edu.cn");
                    if (!string.IsNullOrEmpty(config.Ip) && config.Ip != _txtIp.Tag?.ToString())
                    {
                        SetTextBoxIfPlaceholder(_txtIp, config.Ip);
                    }
                    if (!string.IsNullOrEmpty(config.AcId) && config.AcId != _txtAcId.Tag?.ToString())
                    {
                        SetTextBoxIfPlaceholder(_txtAcId, config.AcId);
                    }
                    _chkSaveConfig.Checked = true;
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
            txt.ForeColor = Color.Black;
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
                AcId = IsPlaceholder(_txtAcId) ? "" : _txtAcId.Text.Trim()
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
            AppendOutput("[配置] 已保存\n");
        }
        catch (Exception ex)
        {
            AppendOutput($"[配置] 保存失败：{ex.Message}\n");
        }
    }

    private void DeleteConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                File.Delete(ConfigPath);
            AppendOutput("[配置] 已删除\n");
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

    private static TextBox CreatePlaceholderTextBox(int x, int y, int width, int height, string placeholder)
    {
        var txt = new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            Tag = placeholder
        };

        txt.Text = placeholder;
        txt.ForeColor = Color.Gray;

        txt.GotFocus += (s, e) =>
        {
            if (txt.Text == placeholder)
            {
                txt.Text = "";
                txt.ForeColor = Color.Black;
                if (txt.Tag?.ToString() == "请输入密码")
                    txt.UseSystemPasswordChar = true;
            }
        };

        txt.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text))
            {
                txt.Text = placeholder;
                txt.ForeColor = Color.Gray;
                if (txt.Tag?.ToString() == "请输入密码")
                    txt.UseSystemPasswordChar = true;
            }
        };

        return txt;
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

        // 保存配置
        if (_chkSaveConfig.Checked)
            SaveConfig();

        SetButtonsEnabled(false);
        AppendOutput("=== 登录尝试 ===\n");

        try
        {
            var portal = new SrunPortal(url, username, password, acId, ip, domain);
            await portal.DetectInfoAsync();

            var ipResult = portal.GetDetectedIp();
            var acIdResult = portal.GetDetectedAcId();
            AppendOutput($"检测结果 - IP: {ipResult}, AC_ID: {acIdResult}\n");

            _lastIp = ipResult;
            _lastAcId = acIdResult;

            var result = await portal.LoginAsync();
            AppendOutput($"结果: {result.Error}\n");

            if (result.IsSuccess)
            {
                MessageBox.Show("登录成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await QueryStatus(portal);
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? result.Ecode ?? "未知错误";
                MessageBox.Show($"登录失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendOutput($"错误：{error}\n");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppendOutput($"异常：{ex.Message}\n");
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
        var acId = string.IsNullOrWhiteSpace(_txtAcId.Text) ? null : _txtAcId.Text.Trim();
        var ip = string.IsNullOrWhiteSpace(_txtIp.Text) ? null : _txtIp.Text.Trim();
        var domain = GetActualText(_txtDomain, "");

        SetButtonsEnabled(false);
        AppendOutput("=== 查询状态 ===\n");

        try
        {
            var portal = new SrunPortal(url, username, "", acId, ip, domain);
            await portal.DetectInfoAsync();
            await QueryStatus(portal);
        }
        catch (Exception ex)
        {
            AppendOutput($"错误：{ex.Message}\n");
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
        var acId = string.IsNullOrWhiteSpace(_txtAcId.Text) ? null : _txtAcId.Text.Trim();
        var ip = string.IsNullOrWhiteSpace(_txtIp.Text) ? (_lastIp ?? "") : _txtIp.Text.Trim();
        var domain = GetActualText(_txtDomain, "");

        SetButtonsEnabled(false);
        AppendOutput("=== 登出 ===\n");

        try
        {
            var portal = new SrunPortal(url, username, "", acId, ip, domain);
            await portal.DetectInfoAsync();
            var result = await portal.LogoutAsync();

            if (result.IsSuccess)
            {
                MessageBox.Show("登出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendOutput("登出成功\n");
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? "未知错误";
                MessageBox.Show($"登出失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendOutput($"错误：{error}\n");
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"错误：{ex.Message}\n");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
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

    private async Task QueryStatus(SrunPortal portal)
    {
        try
        {
            var info = await portal.GetUserInfoAsync();
            AppendOutput($"\n--- 用户信息 ---\n");
            AppendOutput($"账号：{info.UserName ?? "N/A"}\n");
            AppendOutput($"IP：{info.OnlineIp ?? info.ClientIp ?? "N/A"}\n");
            AppendOutput($"MAC：{info.UserMac ?? "N/A"}\n");

            if (info.SumBytes > 0)
                AppendOutput($"已用流量：{FormatFlow(info.SumBytes.Value)}\n");
            if (info.SumSeconds > 0)
                AppendOutput($"已用时长：{FormatTime(info.SumSeconds.Value)}\n");
            if (info.UserBalance.HasValue)
                AppendOutput($"余额：{info.UserBalance:F2}\n");
        }
        catch { }

        try
        {
            var expire = await portal.GetExpireTimeAsync();
            if (expire.HasValue)
                AppendOutput($"到期时间：{expire:yyyy-MM-dd HH:mm:ss}\n");
        }
        catch { }

        AppendOutput("\n");
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