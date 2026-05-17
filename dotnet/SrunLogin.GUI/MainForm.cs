using System.Net;
using System.Text.Json;
using SrunLogin.Crypto;
using SrunLogin.Models;
using SrunLogin.Services;

namespace SrunLogin.GUI;

public partial class MainForm : Form
{
    private TextBox _txtUrl = null!;
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private TextBox _txtIp = null!;
    private TextBox _txtAcId = null!;
    private TextBox _txtDomain = null!;
    private Button _btnLogin = null!;
    private Button _btnLogout = null!;
    private Button _btnInfo = null!;
    private TextBox _txtOutput = null!;
    private CheckBox _chkShowPassword = null!;
    private CheckBox _chkAutoDetect = null!;
    private CheckBox _chkSaveConfig = null!;

    private string? _lastAcId;
    private string? _lastIp;

    private readonly string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SrunLogin",
        "config.json"
    );

    public MainForm()
    {
        InitializeComponent();
        LoadConfig();
    }

    private void InitializeComponent()
    {
        Text = "校园网认证工具";
        Size = new Size(550, 550);
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
        _txtUrl = CreatePlaceholderTextBox(115, 58, 350, 22, "http://10.0.0.1");

        // 用户名
        var lblUser = new Label { Text = "用户名:", Location = new Point(20, 90), Size = new Size(90, 20) };
        _txtUsername = CreatePlaceholderTextBox(115, 88, 200, 22, "请输入用户名");

        // 密码
        var lblPwd = new Label { Text = "密码:", Location = new Point(20, 120), Size = new Size(90, 20) };
        _txtPassword = CreatePlaceholderTextBox(115, 118, 200, 22, "请输入密码");
        _txtPassword.UseSystemPasswordChar = true;

        _chkShowPassword = new CheckBox
        {
            Text = "显示",
            Location = new Point(320, 118),
            Size = new Size(60, 20),
            FlatStyle = FlatStyle.Flat
        };
        _chkShowPassword.CheckedChanged += (s, e) =>
        {
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
        };

        // IP地址
        var lblIp = new Label { Text = "IP地址:", Location = new Point(20, 150), Size = new Size(90, 20) };
        _txtIp = CreatePlaceholderTextBox(115, 148, 150, 22, "自动检测");

        _chkAutoDetect = new CheckBox
        {
            Text = "自动检测",
            Location = new Point(270, 148),
            Size = new Size(90, 20),
            Checked = true,
            FlatStyle = FlatStyle.Flat
        };
        _chkAutoDetect.CheckedChanged += (s, e) => _txtIp.Enabled = !_chkAutoDetect.Checked;

        // AC_ID
        var lblAcId = new Label { Text = "AC ID:", Location = new Point(20, 180), Size = new Size(90, 20) };
        _txtAcId = CreatePlaceholderTextBox(115, 178, 100, 22, "留空自动检测");

        var lblAcIdNote = new Label { Text = "(留空自动检测)", Location = new Point(220, 180), Size = new Size(100, 20), ForeColor = Color.Gray };

        // 域
        var lblDomain = new Label { Text = "域:", Location = new Point(20, 210), Size = new Size(90, 20) };
        _txtDomain = CreatePlaceholderTextBox(115, 208, 150, 22, "@edu.cn");

        // 保存配置
        _chkSaveConfig = new CheckBox
        {
            Text = "保存配置",
            Location = new Point(20, 248),
            Size = new Size(90, 20),
            FlatStyle = FlatStyle.Flat
        };
        _chkSaveConfig.CheckedChanged += (s, e) =>
        {
            if (_chkSaveConfig.Checked)
                SaveConfig();
            else
                DeleteConfig();
        };

        // 按钮
        _btnLogin = new Button
        {
            Text = "登录",
            Location = new Point(115, 240),
            Size = new Size(85, 32),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogin.Click += BtnLogin_Click;

        _btnInfo = new Button
        {
            Text = "查询状态",
            Location = new Point(210, 240),
            Size = new Size(95, 32),
            BackColor = Color.FromArgb(0, 150, 136),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnInfo.Click += BtnInfo_Click;

        _btnLogout = new Button
        {
            Text = "登出",
            Location = new Point(315, 240),
            Size = new Size(85, 32),
            BackColor = Color.FromArgb(244, 67, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogout.Click += BtnLogout_Click;

        // 输出
        var lblOutput = new Label { Text = "输出:", Location = new Point(20, 290), Size = new Size(100, 20) };
        _txtOutput = new TextBox
        {
            Location = new Point(20, 310),
            Size = new Size(500, 145),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Font = new Font("Consolas", 9)
        };

        Controls.AddRange(new Control[]
        {
            lblTitle, lblUrl, _txtUrl, lblUser, _txtUsername, lblPwd, _txtPassword,
            _chkShowPassword, lblIp, _txtIp, _chkAutoDetect, lblAcId, _txtAcId,
            lblAcIdNote, lblDomain, _txtDomain, _chkSaveConfig,
            _btnLogin, _btnInfo, _btnLogout, lblOutput, _txtOutput
        });
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
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
                    _chkAutoDetect.Checked = config.AutoDetectIp;
                    if (!string.IsNullOrEmpty(config.AcId))
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
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var config = new Config
            {
                Url = GetActualText(_txtUrl, "http://10.0.0.1"),
                Username = GetActualText(_txtUsername),
                Password = GetActualText(_txtPassword, ""),
                Domain = GetActualText(_txtDomain, ""),
                AutoDetectIp = _chkAutoDetect.Checked,
                AcId = _txtAcId.Text == "留空自动检测" ? "" : _txtAcId.Text
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
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
            if (File.Exists(_configPath))
                File.Delete(_configPath);
            AppendOutput("[配置] 已删除\n");
        }
        catch { }
    }

    private class Config
    {
        public string? Url { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Domain { get; set; }
        public bool AutoDetectIp { get; set; } = true;
        public string? AcId { get; set; }
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
        var ip = _chkAutoDetect.Checked ? null : GetActualText(_txtIp, "");
        var acId = string.IsNullOrWhiteSpace(_txtAcId.Text) || _txtAcId.Text == "留空自动检测" ? null : _txtAcId.Text.Trim();
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
        var acId = string.IsNullOrWhiteSpace(_txtAcId.Text) || _txtAcId.Text == "留空自动检测" ? null : _txtAcId.Text.Trim();
        var ip = _chkAutoDetect.Checked ? null : GetActualText(_txtIp, "");
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
        var acId = string.IsNullOrWhiteSpace(_txtAcId.Text) || _txtAcId.Text == "留空自动检测" ? null : _txtAcId.Text.Trim();
        var ip = _chkAutoDetect.Checked ? (_lastIp ?? GetActualText(_txtIp, "")) : GetActualText(_txtIp, "");
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