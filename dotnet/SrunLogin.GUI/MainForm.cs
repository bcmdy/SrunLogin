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

    private string? _lastAcId;
    private string? _lastIp;

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "SrunLogin - Campus Network Auth";
        Size = new Size(550, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(240, 240, 240);

        var lblTitle = new Label
        {
            Text = "Campus Network Login",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 15),
            Size = new Size(300, 30)
        };

        // URL
        var lblUrl = new Label { Text = "Gateway URL:", Location = new Point(20, 60), Size = new Size(90, 20) };
        _txtUrl = new TextBox { Location = new Point(115, 58), Size = new Size(350, 22), Text = "http://10.0.0.1" };

        // Username
        var lblUser = new Label { Text = "Username:", Location = new Point(20, 90), Size = new Size(90, 20) };
        _txtUsername = new TextBox { Location = new Point(115, 88), Size = new Size(200, 22) };

        // Password
        var lblPwd = new Label { Text = "Password:", Location = new Point(20, 120), Size = new Size(90, 20) };
        _txtPassword = new TextBox { Location = new Point(115, 118), Size = new Size(200, 22), UseSystemPasswordChar = true };

        _chkShowPassword = new CheckBox
        {
            Text = "Show",
            Location = new Point(320, 118),
            Size = new Size(60, 20),
            FlatStyle = FlatStyle.Flat
        };
        _chkShowPassword.CheckedChanged += (s, e) =>
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;

        // IP
        var lblIp = new Label { Text = "IP:", Location = new Point(20, 150), Size = new Size(90, 20) };
        _txtIp = new TextBox { Location = new Point(115, 148), Size = new Size(150, 22), Enabled = false };

        _chkAutoDetect = new CheckBox
        {
            Text = "Auto Detect",
            Location = new Point(270, 148),
            Size = new Size(100, 20),
            Checked = true,
            FlatStyle = FlatStyle.Flat
        };
        _chkAutoDetect.CheckedChanged += (s, e) => _txtIp.Enabled = !_chkAutoDetect.Checked;

        // AC_ID
        var lblAcId = new Label { Text = "AC ID:", Location = new Point(20, 180), Size = new Size(90, 20) };
        _txtAcId = new TextBox { Location = new Point(115, 178), Size = new Size(100, 22), Enabled = false };

        var lblAcIdNote = new Label { Text = "(Auto detect if empty)", Location = new Point(220, 180), Size = new Size(120, 20), ForeColor = Color.Gray };

        // Domain
        var lblDomain = new Label { Text = "Domain:", Location = new Point(20, 210), Size = new Size(90, 20) };
        _txtDomain = new TextBox { Location = new Point(115, 208), Size = new Size(150, 22) };

        // Buttons
        _btnLogin = new Button
        {
            Text = "Login",
            Location = new Point(115, 248),
            Size = new Size(85, 32),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogin.Click += BtnLogin_Click;

        _btnInfo = new Button
        {
            Text = "Query Status",
            Location = new Point(210, 248),
            Size = new Size(95, 32),
            BackColor = Color.FromArgb(0, 150, 136),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnInfo.Click += BtnInfo_Click;

        _btnLogout = new Button
        {
            Text = "Logout",
            Location = new Point(315, 248),
            Size = new Size(85, 32),
            BackColor = Color.FromArgb(244, 67, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogout.Click += BtnLogout_Click;

        // Output
        var lblOutput = new Label { Text = "Output:", Location = new Point(20, 295), Size = new Size(100, 20) };
        _txtOutput = new TextBox
        {
            Location = new Point(20, 315),
            Size = new Size(500, 120),
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
            lblAcIdNote, lblDomain, _txtDomain, _btnLogin, _btnInfo, _btnLogout,
            lblOutput, _txtOutput
        });
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        if (!ValidateInput()) return;
        SetButtonsEnabled(false);
        AppendOutput("=== Login Attempt ===\n");

        try
        {
            var url = _txtUrl.Text.Trim();
            var username = _txtUsername.Text.Trim();
            var password = _txtPassword.Text;
            var ip = _chkAutoDetect.Checked ? null : _txtIp.Text.Trim();
            var acId = string.IsNullOrWhiteSpace(_txtAcId.Text) ? null : _txtAcId.Text.Trim();
            var domain = _txtDomain.Text.Trim();

            var portal = new SrunPortal(url, username, password, acId, ip, domain);
            await portal.DetectInfoAsync();

            var ipResult = portal.GetDetectedIp();
            var acIdResult = portal.GetDetectedAcId();
            AppendOutput($"Detected - IP: {ipResult}, AC_ID: {acIdResult}\n");

            _lastIp = ipResult;
            _lastAcId = acIdResult;

            var result = await portal.LoginAsync();
            AppendOutput($"Result: {result.Error}\n");

            if (result.IsSuccess)
            {
                MessageBox.Show("Login successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await QueryStatus(portal);
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? result.Ecode ?? "Unknown error";
                MessageBox.Show($"Login failed: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendOutput($"Error: {error}\n");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppendOutput($"Exception: {ex.Message}\n");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void BtnInfo_Click(object? sender, EventArgs e)
    {
        if (!ValidateInput()) return;
        SetButtonsEnabled(false);
        AppendOutput("=== Query Status ===\n");

        try
        {
            var portal = new SrunPortal(
                _txtUrl.Text.Trim(),
                _txtUsername.Text.Trim(),
                "",
                string.IsNullOrWhiteSpace(_txtAcId.Text) ? null : _txtAcId.Text.Trim(),
                _chkAutoDetect.Checked ? null : _txtIp.Text.Trim(),
                _txtDomain.Text.Trim()
            );

            await portal.DetectInfoAsync();
            await QueryStatus(portal);
        }
        catch (Exception ex)
        {
            AppendOutput($"Error: {ex.Message}\n");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void BtnLogout_Click(object? sender, EventArgs e)
    {
        if (!ValidateInput()) return;
        SetButtonsEnabled(false);
        AppendOutput("=== Logout ===\n");

        try
        {
            var portal = new SrunPortal(
                _txtUrl.Text.Trim(),
                _txtUsername.Text.Trim(),
                "",
                string.IsNullOrWhiteSpace(_txtAcId.Text) ? null : _txtAcId.Text.Trim(),
                _chkAutoDetect.Checked ? (_lastIp ?? _txtIp.Text.Trim()) : _txtIp.Text.Trim(),
                _txtDomain.Text.Trim()
            );

            await portal.DetectInfoAsync();
            var result = await portal.LogoutAsync();

            if (result.IsSuccess)
            {
                MessageBox.Show("Logout successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendOutput("Logout successful\n");
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? "Unknown error";
                MessageBox.Show($"Logout failed: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendOutput($"Error: {error}\n");
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"Error: {ex.Message}\n");
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
            AppendOutput($"\n--- User Info ---\n");
            AppendOutput($"Username: {info.UserName ?? "N/A"}\n");
            AppendOutput($"IP: {info.OnlineIp ?? info.ClientIp ?? "N/A"}\n");
            AppendOutput($"MAC: {info.UserMac ?? "N/A"}\n");

            if (info.SumBytes > 0)
                AppendOutput($"Traffic: {FormatFlow(info.SumBytes.Value)}\n");
            if (info.SumSeconds > 0)
                AppendOutput($"Duration: {FormatTime(info.SumSeconds.Value)}\n");
            if (info.UserBalance.HasValue)
                AppendOutput($"Balance: {info.UserBalance:F2}\n");
        }
        catch { }

        try
        {
            var expire = await portal.GetExpireTimeAsync();
            if (expire.HasValue)
                AppendOutput($"Expire: {expire:yyyy-MM-dd HH:mm:ss}\n");
        }
        catch { }

        AppendOutput("\n");
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_txtUrl.Text))
        {
            MessageBox.Show("Please enter gateway URL", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(_txtUsername.Text))
        {
            MessageBox.Show("Please enter username", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
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
        if (d > 0) parts.Add($"{d}d");
        if (h > 0) parts.Add($"{h}h");
        if (m > 0) parts.Add($"{m}m");
        if (s > 0 || parts.Count == 0) parts.Add($"{s}s");
        return string.Join("", parts);
    }
}