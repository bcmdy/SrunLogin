# SrunLogin 校园网认证工具

多语言实现的校园网登录工具，支持 Dr.COM/SRun 认证系统。

## 项目结构

```text
SrunLogin/
├── python/                 # Python 原版脚本
│   ├── login.py
│   ├── login.bat
│   ├── getinfo.bat
│   ├── loginout.bat
│   └── README.md
└── dotnet/
    ├── SrunLogin.Core/     # 认证协议、加密、模型和工具类
    ├── SrunLogin.GUI/      # WinForms 旧版 GUI
    ├── SrunLogin.Wpf/      # WPF 新版 GUI
    └── build.ps1           # WPF 发布脚本
```

## 当前实现

### Python 版

纯 Python 标准库实现，无需安装第三方库。

```bash
python python/login.py login -u 账号 -p 密码 --url http://网关
```

### .NET WPF 版

新版桌面 GUI 使用 WPF 实现，并复用 `SrunLogin.Core` 作为认证核心。目标框架为 .NET 8 LTS，需要安装 .NET 8 Windows Desktop Runtime。

```powershell
dotnet run --project dotnet\SrunLogin.Wpf\SrunLogin.Wpf.csproj
```

发布单文件可执行程序：

```powershell
.\dotnet\build.ps1
```

发布输出位于 `dotnet\publish\`。配置文件 `config.json` 和日志文件 `app.log` 会写入 exe 同级目录。

### .NET WinForms 版

`dotnet\SrunLogin.GUI` 保留为旧版 GUI，主要用于过渡和对照。

```powershell
dotnet run --project dotnet\SrunLogin.GUI\SrunLogin.GUI.csproj
```

## 测试

Core 迁移对照测试不依赖外部测试框架，使用普通 console 项目执行。测试覆盖自定义 Base64、XXTEA、紧凑 JSON、HMAC-MD5、查询字符串顺序和响应解析。

```powershell
dotnet run --project dotnet\SrunLogin.Core.Tests\SrunLogin.Core.Tests.csproj
```

## 功能

- 自动检测 IP 和 AC_ID
- 登录和注销校园网
- 查询在线状态（流量、时长、余额）
- 查询账户到期时间
- 托盘最小化与右键操作
- 循环检测网络状态并尝试自动登录
- 诊断日志输出，便于排查认证问题

## 协议

MIT License
