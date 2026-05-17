# SrunLogin 校园网认证工具

多语言实现的校园网登录工具，支持 Dr.COM/SRun 认证系统。

## 项目结构

```
SrunLogin/
├── python/           # Python 原版实现
│   ├── login.py
│   ├── login.bat
│   ├── getinfo.bat
│   ├── loginout.bat
│   └── README.md
└── dotnet/           # .NET 重构版
    ├── SrunLogin.csproj
    ├── Program.cs
    ├── Crypto/           # 加密算法
    ├── Models/           # 数据模型
    ├── Services/         # 认证服务
    └── Utils/            # 工具类
```

## 当前实现

### Python 版

纯 Python 标准库实现，无需安装任何第三方库。

```bash
python python/login.py login -u 账号 -p 密码 --url http://网关
```

### .NET 版

.NET 8+ 控制台应用，零外部依赖。

```bash
dotnet run --project dotnet -- login -u 账号 -p 密码 --url http://网关
```

或编译后运行：

```bash
dotnet publish dotnet -c Release -o ./publish
./publish/SrunLogin login -u 账号 -p 密码 --url http://网关
```

## 功能

- 自动检测 IP 和 AC_ID
- 登录/登出校园网
- 查询在线状态（流量、时长、余额）
- 查询账户到期时间
- 诊断输出方便排查问题

## 协议

MIT License