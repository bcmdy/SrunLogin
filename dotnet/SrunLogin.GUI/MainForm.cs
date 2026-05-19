using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using SrunLogin.Crypto;
using SrunLogin.Models;
using SrunLogin.Services;
using System.Reflection;

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

    private Panel? _advancedPanel;
    private bool _isAdvancedExpanded = false;
    private const int ExpandedWidth = 200;

    private System.Windows.Forms.Timer? _loopTimer;
    private CheckBox? _chkLoopEnable = null!;
    private TextBox? _txtLoopInterval = null!;
    private TextBox? _txtLoopTimeout = null!;
    private Label? _lblLoopStatus = null!;
    private TextBox? _txtPingHost = null!;
    private bool _isLoopRunning = false;
    private int _lastCheckTime = 0;

    // ===== 托盘图标相关 =====
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _trayMenu = null!;
    private bool _allowClose = false;

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

    /// <summary>
    /// 从程序集嵌入资源加载图标
    /// </summary>
    private static Icon? LoadEmbeddedIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 尝试查找常见的资源名称格式
        var resourceNames = assembly.GetManifestResourceNames();

        // 优先查找 .ico 结尾的资源
        var iconResource = resourceNames.FirstOrDefault(n =>
            n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));

        if (iconResource == null)
            return null;

        try
        {
            using var stream = assembly.GetManifestResourceStream(iconResource);
            if (stream == null)
                return null;
            return new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    public MainForm()
    {
        this.AutoScaleMode = AutoScaleMode.None;
        InitializeComponent();
        InitializeTrayIcon();
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

    /// <summary>
    /// 初始化托盘图标和右键菜单
    /// </summary>
    private void InitializeTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();

        // 打开界面
        var menuShow = new ToolStripMenuItem("打开界面", null, (s, e) => ShowFromTray());
        // 登录
        var menuLogin = new ToolStripMenuItem("登录", null, (s, e) => TrayLogin());
        // 登出
        var menuLogout = new ToolStripMenuItem("登出", null, (s, e) => TrayLogout());
        // 分隔线
        var separator = new ToolStripSeparator();
        // 退出
        var menuExit = new ToolStripMenuItem("退出", null, (s, e) => { _allowClose = true; Application.Exit(); });

        _trayMenu.Items.AddRange(new ToolStripItem[]
        {
            menuShow,
            menuLogin,
            menuLogout,
            separator,
            menuExit
        });

        _notifyIcon = new NotifyIcon
        {
            Text = "校园网认证工具",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };

        // 加载嵌入资源图标
        _notifyIcon.Icon = LoadEmbeddedIcon() ?? SystemIcons.Application;

        // 双击打开
        _notifyIcon.DoubleClick += (s, e) => ShowFromTray();
    }

    /// <summary>
    /// 从托盘恢复窗口
    /// </summary>
    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    /// <summary>
    /// 托盘菜单执行登录
    /// </summary>
    private void TrayLogin()
    {
        if (InvokeRequired)
        {
            Invoke(TrayLogin);
            return;
        }

        ShowFromTray();
        BtnLogin_Click(null, EventArgs.Empty);
    }

    /// <summary>
    /// 托盘菜单执行登出
    /// </summary>
    private void TrayLogout()
    {
        if (InvokeRequired)
        {
            Invoke(TrayLogout);
            return;
        }

        ShowFromTray();
        BtnLogout_Click(null, EventArgs.Empty);
    }

    /// <summary>
    /// 最小化时隐藏到托盘
    /// </summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            _notifyIcon.ShowBalloonTip(2000, "校园网认证工具", "已最小化到托盘", ToolTipIcon.Info);
        }
    }

    /// <summary>
    /// 关闭时最小化到托盘而非退出（除非从托盘菜单选择退出）
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            Hide();
            _notifyIcon.ShowBalloonTip(2000, "校园网认证工具", "程序已最小化到托盘，右键图标可操作", ToolTipIcon.Info);
            return;
        }

        _notifyIcon?.Dispose();
        base.OnFormClosing(e);
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

        // ===== 新增：设置窗口图标 =====
        Icon = LoadEmbeddedIcon() ?? SystemIcons.Application;

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

        // 高级功能按钮（竖着的侧边栏按钮，位于窗口右侧）
        var btnAdvanced = new Label
        {
            Text = "▶\n高\n级\n功\n能\n▶",
            Location = new Point(590, 200),
            Size = new Size(35, 180),
            BackColor = ColorPrimary,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        btnAdvanced.Click += (s, e) =>
        {
            _isAdvancedExpanded = !_isAdvancedExpanded;
            Size = new Size(_isAdvancedExpanded ? 840 : 640, Height);
            _advancedPanel!.Visible = _isAdvancedExpanded;
            btnAdvanced.Text = _isAdvancedExpanded ? "◀\n高\n级\n功\n能\n◀" : "▶\n高\n级\n功\n能\n▶";
        };

        // 高级功能面板（右侧折叠区域）
        _advancedPanel = new Panel
        {
            Location = new Point(620, 0),
            Size = new Size(ExpandedWidth, 680),
            BackColor = Color.FromArgb(230, 235, 240),
            Visible = false
        };

        var lblAdvancedTitle = new Label
        {
            Text = "高级功能",
            Location = new Point(10, 15),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = ColorText
        };
        _advancedPanel.Controls.Add(lblAdvancedTitle);

        // 循环检测登陆功能
        int advY = 50;

        var lblLoopTitle = new Label
        {
            Text = "循环检测登陆",
            Location = new Point(10, advY),
            Size = new Size(180, 20),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = ColorText
        };
        _advancedPanel.Controls.Add(lblLoopTitle);
        advY += 30;

        _chkLoopEnable = new CheckBox
        {
            Text = "启用循环检测",
            Location = new Point(10, advY),
            Size = new Size(150, 20),
            FlatStyle = FlatStyle.Flat,
            ForeColor = ColorText,
            Font = new Font("Segoe UI", 9F)
        };
        _chkLoopEnable.CheckedChanged += (s, e) =>
        {
            if (_chkSaveConfig!.Checked)
                SaveConfig();

            if (_chkLoopEnable!.Checked)
            {
                StartLoopDetection();
            }
            else
            {
                StopLoopDetection();
            }
        };
        _advancedPanel.Controls.Add(_chkLoopEnable);
        advY += 28;

        var lblInterval = new Label
        {
            Text = "检测间隔(秒):",
            Location = new Point(10, advY),
            Size = new Size(100, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F)
        };
        _advancedPanel.Controls.Add(lblInterval);

        _txtLoopInterval = new TextBox
        {
            Location = new Point(10, advY + 22),
            Size = new Size(80, 24),
            Text = "10",
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.White
        };
        _advancedPanel.Controls.Add(_txtLoopInterval);
        advY += 52;

        var lblTimeout = new Label
        {
            Text = "网络超时(秒):",
            Location = new Point(10, advY),
            Size = new Size(100, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F)
        };
        _advancedPanel.Controls.Add(lblTimeout);

        _txtLoopTimeout = new TextBox
        {
            Location = new Point(10, advY + 22),
            Size = new Size(80, 24),
            Text = "3",
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.White
        };
        _advancedPanel.Controls.Add(_txtLoopTimeout);
        advY += 52;

        var lblPingHost = new Label
        {
            Text = "Ping主机:",
            Location = new Point(10, advY),
            Size = new Size(100, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F)
        };
        _advancedPanel.Controls.Add(lblPingHost);

        _txtPingHost = new TextBox
        {
            Location = new Point(10, advY + 22),
            Size = new Size(150, 24),
            Text = "www.baidu.com",
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.White
        };
        _advancedPanel.Controls.Add(_txtPingHost);
        advY += 52;

        _lblLoopStatus = new Label
        {
            Text = "状态: 未启动",
            Location = new Point(10, advY),
            Size = new Size(180, 20),
            ForeColor = ColorLabel,
            Font = new Font("Segoe UI", 9F)
        };
        _advancedPanel.Controls.Add(_lblLoopStatus);

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
            lblTitle, line, btnAdvanced,
            lblUrl, _txtUrl, lblUser, _txtUsername, lblPwd, _txtPassword, _chkShowPassword,
            lblIp, _txtIp, lblAcId, _txtAcId, lblDomain, _txtDomain, _chkSaveConfig, _chkAutoLogin,
            _btnLogin, _btnInfo, _btnLogout, btnHelp,
            lblOutput, outputPanel,
            _advancedPanel
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

                    if (config.Loop != null)
                    {
                        _chkLoopEnable!.Checked = config.Loop.Enable;
                        _txtLoopInterval!.Text = config.Loop.Interval.ToString();
                        _txtLoopTimeout!.Text = config.Loop.Timeout.ToString();
                        _txtPingHost!.Text = config.Loop.PingHost;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[配置] 加载失败: {ex.Message}");
        }
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
                AutoLogin = _chkAutoLogin.Checked,
                Loop = new LoopConfig
                {
                    Enable = _chkLoopEnable!.Checked,
                    Interval = int.TryParse(_txtLoopInterval!.Text, out var interval) ? interval : 60,
                    Timeout = int.TryParse(_txtLoopTimeout!.Text, out var timeout) ? timeout : 5,
                    PingHost = _txtPingHost!.Text
                }
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
        catch (Exception ex)
        {
            Log($"[配置] 删除失败：{ex.Message}");
        }
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

    private CancellationTokenSource? _cancellationTokenSource;

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

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

            var portal = new SrunPortal(url, username, password, acId, ip, domain);
            portal.DebugLog = LogDebug;

            // 如果用户没有提供 IP/AC_ID，先自动检测
            if (!userProvidedIp && !userProvidedAcId)
            {
                await portal.DetectInfoAsync(_cancellationTokenSource.Token);
                ip = portal.GetDetectedIp();
                acId = portal.GetDetectedAcId();
                Log($"自动检测结果 - IP: {ip}, AC_ID: {acId}");
            }

            var result = await portal.LoginAsync(_cancellationTokenSource.Token);
            Log($"结果: {result.Error}");

            // 如果用户提供了信息但登录失败，尝试自动检测后重试
            if (!result.IsSuccess && (userProvidedIp || userProvidedAcId))
            {
                Log("使用用户指定参数登录失败，尝试自动检测...");
                await portal.DetectInfoAsync(_cancellationTokenSource.Token);
                ip = portal.GetDetectedIp();
                acId = portal.GetDetectedAcId();
                Log($"自动检测结果 - IP: {ip}, AC_ID: {acId}");
                result = await portal.LoginAsync(_cancellationTokenSource.Token);
                Log($"结果: {result.Error}");
            }

            if (result.IsSuccess)
            {
                // 登录成功仅输出日志，不弹窗
                Log("[登录] 登录成功");
                await QueryStatus(portal);
            }
            else
            {
                var error = result.ErrorMsg ?? result.Message ?? result.Ecode ?? "未知错误";
                MessageBox.Show($"登录失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"错误：{error}");
            }
        }
        catch (OperationCanceledException)
        {
            Log("[登录] 已取消");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log($"异常：{ex.Message}");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
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
            MessageBox.Show($"查询失败：{ex.Message}", "错误");
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

    private static readonly object _logLock = new();
    private static StreamWriter? _logWriter;

    private void EnsureLogWriter()
    {
        lock (_logLock)
        {
            if (_logWriter == null || _logWriter.BaseStream.Position > 4096)
            {
                _logWriter?.Dispose();
                _logWriter = new StreamWriter(LogPath, append: true) { AutoFlush = true };
            }
        }
    }

    private void Log(string text, LogLevel level = LogLevel.Info)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] {text}\n";

        try
        {
            EnsureLogWriter();
            lock (_logLock)
            {
                _logWriter?.Write(logLine);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"日志写入失败: {ex.Message}");
        }

        if (level == LogLevel.Info)
        {
            AppendOutput(text + "\n");
        }
    }

    private void LogDebug(string text)
    {
        Log(text, LogLevel.Debug);
    }

    private void StartLoopDetection()
    {
        int interval = 60;
        if (!int.TryParse(_txtLoopInterval!.Text, out interval) || interval < 5)
        {
            interval = 60;
        }

        if (_loopTimer != null)
        {
            _loopTimer.Stop();
            _loopTimer.Dispose();
        }

        _loopTimer = new System.Windows.Forms.Timer();
        _loopTimer.Interval = interval * 1000;
        _loopTimer.Tick += async (s, e) =>
        {
            await PerformLoopCheck();
        };
        _loopTimer.Start();
        _isLoopRunning = true;
        _lastCheckTime = Environment.TickCount;

        _lblLoopStatus!.Text = $"状态: 检测中 ({interval}秒)";
        Log("[循环检测] 已启动");
    }

    private void StopLoopDetection()
    {
        if (_loopTimer != null)
        {
            _loopTimer.Stop();
            _loopTimer.Dispose();
            _loopTimer = null;
        }
        _isLoopRunning = false;
        _lblLoopStatus!.Text = "状态: 已停止";
        Log("[循环检测] 已停止");
    }

    private async Task PerformLoopCheck()
    {
        if (!_isLoopRunning) return;

        try
        {
            // 检查网络连通性
            int timeout = 5;
            if (!int.TryParse(_txtLoopTimeout!.Text, out timeout) || timeout < 1)
            {
                timeout = 5;
            }

            bool isOnline = await CheckNetworkOnline();
            int loopInterval = int.TryParse(_txtLoopInterval!.Text, out int i) && i >= 5 ? i : 60;

            if (!isOnline)
            {
                _lblLoopStatus!.Text = $"状态: 网络离线，尝试登录...";
                Log("[循环检测] 网络离线，尝试自动登录");

                var username = GetActualText(_txtUsername, "");
                var password = GetActualText(_txtPassword, "");

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    var url = GetActualText(_txtUrl, "http://10.0.0.1");
                    var domain = GetActualText(_txtDomain, "");

                    var portal = new SrunPortal(url, username, password, null, null, domain);
                    portal.DebugLog = LogDebug;

                    await portal.DetectInfoAsync();
                    var result = await portal.LoginAsync();

                    if (result.IsSuccess)
                    {
                        _lblLoopStatus!.Text = $"状态: 登录成功";
                        Log("[循环检测] 登录成功");
                    }
                    else
                    {
                        var error = result.ErrorMsg ?? result.Error ?? "未知错误";
                        _lblLoopStatus!.Text = $"状态: 登录失败";
                        Log($"[循环检测] 登录失败: {error}");
                    }
                }
            }
            else
            {
                _lblLoopStatus!.Text = $"状态: 在线 ({loopInterval}秒)";
            }
        }
        catch (Exception ex)
        {
            _lblLoopStatus!.Text = $"状态: 异常";
            Log($"[循环检测] 异常: {ex.Message}");
        }
    }

    private async Task<bool> CheckNetworkOnline()
    {
        try
        {
            int timeout = 3;
            int.TryParse(_txtLoopTimeout!.Text, out timeout);
            string host = string.IsNullOrWhiteSpace(_txtPingHost!.Text) ? "www.baidu.com" : _txtPingHost.Text;

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeout * 1000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private string FormatFlow(long bytes)
    {
        if (bytes == 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double val = bytes;
        int idx = 0;
        while (val >= 1024 && idx < units.Length - 1) { val /= 1024; idx++; }
        return $"{val:F2} {units[idx]}";
    }

    private string FormatTime(long seconds)
    {
        if (seconds == 0) return "0 秒";
        long s = seconds;
        var parts = new List<string>();
        long d = s / 86400;
        s %= 86400;
        long h = s / 3600;
        s %= 3600;
        long m = s / 60;
        s %= 60;
        if (d > 0) parts.Add($"{d}天");
        if (h > 0) parts.Add($"{h}小时");
        if (m > 0) parts.Add($"{m}分");
        if (s > 0 || parts.Count == 0) parts.Add($"{s}秒");
        return string.Join("", parts);
    }
}