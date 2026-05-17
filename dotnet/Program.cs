using SrunLogin.Models;
using SrunLogin.Services;
using SrunLogin.Utils;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var action = args[0].ToLower();

        // 解析通用参数
        string? url = null, username = null, password = null, ip = null, acId = null, domain = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    url = args[++i];
                    break;
                case "-u" or "--username" when i + 1 < args.Length:
                    username = args[++i];
                    break;
                case "-p" or "--password" when i + 1 < args.Length:
                    password = args[++i];
                    break;
                case "--ip" when i + 1 < args.Length:
                    ip = args[++i];
                    break;
                case "--ac-id" when i + 1 < args.Length:
                    acId = args[++i];
                    break;
                case "--domain" when i + 1 < args.Length:
                    domain = args[++i];
                    break;
                case "-h" or "--help":
                    PrintUsage();
                    return 0;
            }
        }

        // 验证必需参数
        if (string.IsNullOrEmpty(username))
        {
            Console.WriteLine("错误: 必须提供用户名 (-u/--username)");
            return 1;
        }

        if (string.IsNullOrEmpty(url))
        {
            url = "http://10.0.0.1";
        }

        try
        {
            return action switch
            {
                "login" => await HandleLogin(url, username, password, ip, acId, domain),
                "info" => await HandleInfo(url, username, ip, acId, domain),
                "logout" => await HandleLogout(url, username, ip, acId, domain),
                _ => HandleUnknown(action)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[致命错误] {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> HandleLogin(string url, string username, string? password, string? ip, string? acId, string? domain)
    {
        if (string.IsNullOrEmpty(password))
        {
            Console.WriteLine("错误: login 操作需要提供密码 (-p/--password)");
            return 1;
        }

        var portal = new SrunPortal(url, username, password, acId, ip, domain ?? "");
        await portal.DetectInfoAsync();
        var result = await portal.LoginAsync();

        UserInfo? info = null;
        DateTime? expire = null;

        try { info = await portal.GetUserInfoAsync(); }
        catch { }

        try { expire = await portal.GetExpireTimeAsync(); }
        catch { }

        ShowResult(result, info, expire);
        return result.IsSuccess ? 0 : 1;
    }

    static async Task<int> HandleInfo(string url, string username, string? ip, string? acId, string? domain)
    {
        var portal = new SrunPortal(url, username, "", acId, ip, domain ?? "");
        await portal.DetectInfoAsync();
        var info = await portal.GetUserInfoAsync();

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("【用户在线信息】");
        Console.WriteLine($"账号: {info.UserName ?? "N/A"}");
        Console.WriteLine($"IP: {info.OnlineIp ?? info.ClientIp ?? "N/A"}");
        Console.WriteLine($"MAC: {info.UserMac ?? "N/A"}");

        if (info.SumBytes > 0)
            Console.WriteLine($"已用流量: {Formatters.FormatFlow(info.SumBytes)}");
        if (info.SumSeconds > 0)
            Console.WriteLine($"已用时长: {Formatters.FormatTime(info.SumSeconds)}");
        if (info.UserBalance.HasValue)
            Console.WriteLine($"余额: ¥{info.UserBalance:F2}");

        return 0;
    }

    static async Task<int> HandleLogout(string url, string username, string? ip, string? acId, string? domain)
    {
        var portal = new SrunPortal(url, username, "", acId, ip, domain ?? "");
        await portal.DetectInfoAsync();
        var result = await portal.LogoutAsync();

        Console.WriteLine("\n" + new string('=', 50));
        if (result.IsSuccess || result.Error == "ok")
        {
            Console.WriteLine("[✓] 注销成功");
        }
        else
        {
            Console.WriteLine($"[✗] 注销失败: {result.ErrorMsg ?? result.Message ?? "未知错误"}");
        }

        return result.IsSuccess ? 0 : 1;
    }

    static int HandleUnknown(string action)
    {
        Console.WriteLine($"错误: 未知操作 '{action}'");
        PrintUsage();
        return 1;
    }

    static void ShowResult(LoginResult result, UserInfo? info, DateTime? expire)
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("【认证服务器原始响应】");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        if (result.IsSuccess && result.SucMsg == "ip_already_online_error")
        {
            Console.WriteLine("\n[!] 当前 IP 已在线，无需重复登录");
            Console.WriteLine($"IP: {result.OnlineIp ?? "N/A"}");
            return;
        }

        if (!result.IsSuccess)
        {
            Console.WriteLine("\n[✗] 登录失败");
            Console.WriteLine($"错误: {result.ErrorMsg ?? result.Message ?? result.Ecode ?? "未知"}");

            switch (result.Ecode)
            {
                case "E2901":
                    Console.WriteLine("提示: 账号或密码错误");
                    break;
                case "E2620":
                    Console.WriteLine("提示: 在线设备数量超限");
                    break;
            }

            if (result.ErrorMsg == "ip_already_online_error")
                Console.WriteLine("提示: 当前 IP 已在线");
            else if (result.Error == "auth_info_error")
                Console.WriteLine("提示: 认证信息加密错误（可能是 ac_id 不正确）");
            else if (result.Error == "login_error" && (result.ErrorMsg?.Contains("nas", StringComparison.OrdinalIgnoreCase) ?? false))
                Console.WriteLine("提示: NAS 类型未找到，可能是 ac_id 不正确");

            return;
        }

        Console.WriteLine("\n[✓] 登录成功");

        if (info != null && info.Error != "not_online_error")
        {
            Console.WriteLine($"{"账号:",-12} {info.UserName ?? "N/A"}");
            Console.WriteLine($"{"IP:",-12} {info.OnlineIp ?? info.ClientIp ?? "N/A"}");
            Console.WriteLine($"{"MAC:",-12} {info.UserMac ?? "N/A"}");

            if (info.SumBytes > 0)
                Console.WriteLine($"{"已用流量:",-12} {Formatters.FormatFlow(info.SumBytes)}");
            if (info.SumSeconds > 0)
                Console.WriteLine($"{"已用时长:",-12} {Formatters.FormatTime(info.SumSeconds)}");
            if (info.UserBalance.HasValue)
                Console.WriteLine($"{"余额:",-12} ¥{info.UserBalance:F2}");
        }

        if (expire.HasValue)
            Console.WriteLine($"{"到期时间:",-12} {expire.Value:yyyy-MM-dd HH:mm:ss}");
    }

    static void PrintUsage()
    {
        Console.WriteLine("""
            Srun 校园网认证工具 (.NET 版)
            ==============================

            用法:
              SrunLogin <操作> [选项]

            操作:
              login   登录校园网
              info    查询在线状态
              logout  登出校园网

            选项:
              --url <地址>      认证服务器地址 (默认: http://10.0.0.1)
              -u, --username <账号>  用户名 (必需)
              -p, --password <密码>  密码 (login 操作必需)
              --ip <IP>         指定 IP 地址 (可选，自动检测)
              --ac-id <ID>      AC ID (可选，自动检测)
              --domain <域>     域后缀 (可选)
              -h, --help        显示帮助

            示例:
              SrunLogin login -u 账号 -p 密码 --url http://1.1.1.1
              SrunLogin info -u 账号 --url http://1.1.1.1
              SrunLogin logout -u 账号 --url http://1.1.1.1
            """);
    }
}