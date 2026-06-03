# SrunLogin 校园网认证工具

用于 Dr.COM/SRun 认证系统的 .NET 桌面登录工具。

## 项目结构

```text
SrunLogin/
└── dotnet/
    ├── SrunLogin.Core/        # 认证协议、加密、模型和工具类
    ├── SrunLogin.Core.Tests/  # Core 迁移对照测试
    ├── SrunLogin.Wpf/         # WPF 主线 GUI
    ├── build.ps1              # WPF 发布脚本
    └── 修改建议.md            # 整改记录
```

## 运行

WPF 版复用 `SrunLogin.Core` 作为认证核心。目标框架为 .NET 8 LTS，需要安装 .NET 8 Windows Desktop Runtime。

```powershell
dotnet run --project dotnet\SrunLogin.Wpf\SrunLogin.Wpf.csproj
```

发布单文件可执行程序：

```powershell
.\dotnet\build.ps1
```

发布输出位于 `dotnet\publish\`。配置文件 `config.json` 和日志文件 `app.log` 会写入 exe 同级目录。

如果校园网认证网关使用自签名 HTTPS 证书，可在 WPF 版“高级功能”中勾选“允许自签名 HTTPS 证书”。该选项默认关闭。

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
