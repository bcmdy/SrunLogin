using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using SrunLogin.Crypto;
using SrunLogin.Models;

namespace SrunLogin.Services;

/// <summary>
/// Srun 门户认证服务
/// </summary>
public class SrunPortal
{
    private readonly string _authUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _domain;
    private string? _acId;
    private string? _ip;
    private readonly CookieContainer _cookieContainer = new();

    private static readonly Random Random = new();
    private readonly string[] _acIdCandidates = ["143", "2", "3", "5", "10", "15", "20", "100"];

    public SrunPortal(string authUrl, string username, string password, string? acId = null, string? ip = null, string domain = "")
    {
        _authUrl = authUrl.TrimEnd('/');
        _username = username;
        _password = password;
        _domain = domain;
        _acId = acId;
        _ip = ip;
    }

    private string UsernameWithDomain => _username + _domain;

    public string? GetDetectedIp() => _ip;
    public string? GetDetectedAcId() => _acId;

    public async Task DetectInfoAsync()
    {
        Console.WriteLine($"[诊断] 开始探测 IP/AC_ID...");

        if (!string.IsNullOrEmpty(_acId) && !string.IsNullOrEmpty(_ip))
        {
            Console.WriteLine($"[诊断] 已提供 IP={_ip}, AC_ID={_acId}");
            return;
        }

        // 1. 访问首页
        try
        {
            var (html, finalUrl) = await FetchHtmlAsync("/");
            Console.WriteLine($"[诊断] 首页最终 URL: {finalUrl}");

            var acIdFromHtml = ExtractAcId(html);
            if (!string.IsNullOrEmpty(acIdFromHtml))
                _acId = _acId ?? acIdFromHtml;

            if (string.IsNullOrEmpty(_ip))
            {
                var ipFromHtml = ExtractIp(html);
                if (!string.IsNullOrEmpty(ipFromHtml))
                {
                    _ip = ipFromHtml;
                    Console.WriteLine($"[诊断] 从首页 HTML 提取 IP: {_ip}");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[诊断] 首页探测失败: {e.Message}");
        }

        // 2. ac_detect 接口
        if (string.IsNullOrEmpty(_acId) || _acId == "1")
        {
            try
            {
                Console.WriteLine("[诊断] 尝试 ac_detect 接口获取真实 ac_id...");
                var data = await GetJsonAsync("/v1/srun_portal_detect");
                Console.WriteLine($"[诊断] ac_detect 返回: {JsonSerializer.Serialize(data)}");

                string? redirect = null, pcUrl = null, mobileUrl = null;

                if (data.TryGetProperty("Redirect", out var r)) redirect = r.GetString();
                if (data.TryGetProperty("Pc", out var p)) pcUrl = p.GetString();
                if (data.TryGetProperty("Mobile", out var m)) mobileUrl = m.GetString();

                var targetUrl = redirect ?? pcUrl ?? mobileUrl;
                if (!string.IsNullOrEmpty(targetUrl))
                {
                    var match = Regex.Match(targetUrl, @"[?&]ac_id=(\d+)");
                    if (match.Success)
                    {
                        _acId = match.Groups[1].Value;
                        Console.WriteLine($"[诊断] 从 ac_detect 重定向 URL 获取 ac_id: {_acId}");
                    }
                }

                if ((string.IsNullOrEmpty(_acId) || _acId == "1") && data.TryGetProperty("ac_id", out var acid) && acid.ValueKind == JsonValueKind.Number)
                {
                    _acId = acid.GetInt32().ToString();
                    Console.WriteLine($"[诊断] 从 ac_detect 数据获取 ac_id: {_acId}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[诊断] ac_detect 失败: {e.Message}");
            }
        }

        // 3. srun_portal_pc
        if (string.IsNullOrEmpty(_acId) || _acId == "1")
        {
            try
            {
                Console.WriteLine("[诊断] 尝试访问 srun_portal_pc 获取真实 ac_id...");
                var (html, finalUrl) = await FetchHtmlAsync("/srun_portal_pc");
                Console.WriteLine($"[诊断] srun_portal_pc 最终 URL: {finalUrl}");

                var match = Regex.Match(finalUrl, @"[?&]ac_id=(\d+)");
                if (match.Success)
                {
                    _acId = match.Groups[1].Value;
                    Console.WriteLine($"[诊断] 从 srun_portal_pc URL 提取 ac_id: {_acId}");
                }

                var acIdFromHtml = ExtractAcId(html);
                if (!string.IsNullOrEmpty(acIdFromHtml) && acIdFromHtml != "1")
                    _acId = acIdFromHtml;

                if (string.IsNullOrEmpty(_ip))
                {
                    var ipFromHtml = ExtractIp(html);
                    if (!string.IsNullOrEmpty(ipFromHtml))
                    {
                        _ip = ipFromHtml;
                        Console.WriteLine($"[诊断] 从 srun_portal_pc HTML 提取 IP: {_ip}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[诊断] srun_portal_pc 探测失败: {e.Message}");
            }
        }

        // 4. 尝试候选 ac_id
        if (string.IsNullOrEmpty(_acId) || _acId == "1")
        {
            Console.WriteLine($"[诊断] 尝试候选 ac_id 列表: {string.Join(", ", _acIdCandidates)}");
            foreach (var testAcId in _acIdCandidates)
            {
                try
                {
                    var oldAcId = _acId;
                    _acId = testAcId;
                    var data = await GetChallengeAsync();
                    _acId = oldAcId;

                    if (data.Error == "ok" && !string.IsNullOrEmpty(data.Challenge))
                    {
                        Console.WriteLine($"[诊断] ac_id={testAcId} 的 get_challenge 成功");
                        _acId = testAcId;
                        break;
                    }
                }
                catch
                {
                    Console.WriteLine($"[诊断] ac_id={testAcId} 测试失败");
                }
            }
        }

        // 5. rad_user_info 获取 IP
        if (string.IsNullOrEmpty(_ip))
        {
            try
            {
                Console.WriteLine("[诊断] 尝试 JSONP 模式 rad_user_info 获取 IP...");
                var data = await GetJsonAsync("/cgi-bin/rad_user_info", jsonp: true);
                if (data.TryGetProperty("client_ip", out var clientIp) && clientIp.ValueKind == JsonValueKind.String)
                    _ip = clientIp.GetString();
                if (string.IsNullOrEmpty(_ip) && data.TryGetProperty("online_ip", out var onlineIp) && onlineIp.ValueKind == JsonValueKind.String)
                    _ip = onlineIp.GetString();
                if (!string.IsNullOrEmpty(_ip))
                    Console.WriteLine($"[诊断] 从 JSONP rad_user_info 获取 IP: {_ip}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[诊断] JSONP rad_user_info 失败: {e.Message}");
            }
        }

        // 6. 最终校验
        if (string.IsNullOrEmpty(_ip))
            throw new InvalidOperationException("无法自动获取本机 IP，请手动指定 --ip");

        if (string.IsNullOrEmpty(_acId))
        {
            Console.WriteLine("[警告] 无法自动获取 ac_id，使用默认值 1");
            _acId = "1";
        }

        Console.WriteLine($"[诊断] 探测结果: IP={_ip}, AC_ID={_acId}");
    }

    private async Task<(string Html, string FinalUrl)> FetchHtmlAsync(string path)
    {
        var handler = new HttpClientHandler { CookieContainer = _cookieContainer };
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        var response = await client.GetAsync(_authUrl + path);
        var html = await response.Content.ReadAsStringAsync();
        return (html, response.RequestMessage?.RequestUri?.ToString() ?? _authUrl + path);
    }

    private async Task<JsonElement> GetJsonAsync(string path, bool jsonp = false)
    {
        var query = new Dictionary<string, string>();
        if (jsonp)
        {
            query["callback"] = $"jQuery{Random.Next(100000000, 999999999)}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
            query["_"] = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
        }

        var url = _authUrl + path;
        if (query.Count > 0)
            url += "?" + string.Join("&", query.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));

        var handler = new HttpClientHandler { CookieContainer = _cookieContainer };
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
        client.DefaultRequestHeaders.Add("Accept", GetAccept());
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        client.DefaultRequestHeaders.Add("Referer", $"{_authUrl}/srun_portal_pc?ac_id={_acId}&theme=pro");

        var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        return ParseResponse(text);
    }

    private JsonElement ParseResponse(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("空响应");

        if (text == "ok")
            return JsonDocument.Parse("{\"error\":\"ok\"}").RootElement;

        if (text == "not_online_error")
            return JsonDocument.Parse("{\"error\":\"not_online_error\"}").RootElement;

        if (text == "login_error")
            return JsonDocument.Parse("{\"error\":\"login_error\"}").RootElement;

        if (text.StartsWith("challenge="))
        {
            var challenge = text.Split('=', 1)[1];
            return JsonDocument.Parse($"{{\"error\":\"ok\",\"challenge\":\"{challenge}\"}}").RootElement;
        }

        // JSONP
        var jsonpMatch = Regex.Match(text, @"jQuery\d+_\d+\((\{.*\})\)\s*;?\s*$", RegexOptions.Singleline);
        if (jsonpMatch.Success)
            text = jsonpMatch.Groups[1].Value;

        try
        {
            return JsonDocument.Parse(text).RootElement;
        }
        catch
        {
            // CSV 格式
            if (text.Contains(',') && !text.StartsWith('<'))
            {
                var parts = text.Split(',');
                return JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    error = "ok",
                    user_name = parts.Length > 0 ? parts[0].Trim() : null,
                    online_ip = parts.Length > 8 ? parts[8].Trim() : null,
                    sum_bytes = parts.Length > 6 ? (long.TryParse(parts[6].Trim(), out var sb) ? sb : 0) : 0,
                    sum_seconds = parts.Length > 4 ? (long.TryParse(parts[4].Trim(), out var ss) ? ss : 0) : 0
                })).RootElement;
            }

            throw new InvalidOperationException($"无法解析响应: {text[..Math.Min(200, text.Length)]}");
        }
    }

    private async Task<ChallengeResult> GetChallengeAsync()
    {
        var query = $"?username={HttpUtility.UrlEncode(UsernameWithDomain)}&ip={_ip}";
        var data = await GetJsonAsync("/cgi-bin/get_challenge" + query, jsonp: true);

        if (data.TryGetProperty("challenge", out var challenge))
            return new ChallengeResult { Error = "ok", Challenge = challenge.GetString() };

        if (data.TryGetProperty("error", out var err) && err.GetString() == "ok")
        {
            // 尝试无 callback 模式
            var data2 = await GetJsonAsync("/cgi-bin/get_challenge" + query, jsonp: false);
            if (data2.TryGetProperty("challenge", out var challenge2))
                return new ChallengeResult { Error = "ok", Challenge = challenge2.GetString() };
            return new ChallengeResult { Error = "ok", Challenge = "" };
        }

        throw new InvalidOperationException($"获取 challenge 失败: {data}");
    }

    public async Task<LoginResult> LoginAsync()
    {
        Console.WriteLine($"[登录] 账号: {_username}, IP: {_ip}, AC_ID: {_acId}");

        var challenge = await GetChallengeAsync();
        var token = challenge.Challenge ?? "";
        Console.WriteLine($"[登录] 获取 token: {(token.Length > 8 ? token[..8] : token)}...");

        var queryParams = new Dictionary<string, string>
        {
            ["action"] = "login",
            ["username"] = UsernameWithDomain,
            ["password"] = "",
            ["os"] = "Windows 10",
            ["name"] = "Windows",
            ["double_stack"] = "0",
            ["chksum"] = "",
            ["info"] = "",
            ["ac_id"] = _acId!,
            ["ip"] = _ip!,
            ["n"] = "200",
            ["type"] = "1"
        };

        if (!string.IsNullOrEmpty(token))
        {
            // 加密密码
            var hmd5 = ComputeHmacMd5(token, _password);

            // 加密用户信息
            var infoObj = new
            {
                username = UsernameWithDomain,
                password = _password,
                ip = _ip,
                acid = _acId,
                enc_ver = "srun_bx1"
            };
            var infoStr = JsonSerializer.Serialize(infoObj);
            var encrypted = XXTea.Encrypt(infoStr, token);
            var i = "{SRBX1}" + SrunBase64.Encode(Encoding.Latin1.GetBytes(encrypted));

            // 计算签名
            var chkstr = token + UsernameWithDomain + token + hmd5 + token + _acId + token + _ip +
                         token + "200" + token + "1" + token + i;
            var chksum = ComputeSha1(chkstr);

            queryParams["password"] = "{MD5}" + hmd5;
            queryParams["chksum"] = chksum;
            queryParams["info"] = i;
        }
        else
        {
            Console.WriteLine("[登录] 使用老版本明文密码模式");
            queryParams["password"] = _password;
        }

        var url = _authUrl + "/cgi-bin/srun_portal";
        var query = string.Join("&", queryParams.Select(kv =>
            $"callback={HttpUtility.UrlEncode("jQuery" + Random.Next(100000000, 999999999) + "_" + DateTimeOffset.Now.ToUnixTimeMilliseconds())}" +
            $"&{kv.Key}={HttpUtility.UrlEncode(kv.Value)}" +
            $"&_={DateTimeOffset.Now.ToUnixTimeMilliseconds()}"));

        var handler = new HttpClientHandler { CookieContainer = _cookieContainer };
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
        client.DefaultRequestHeaders.Add("Accept", GetAccept());
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        client.DefaultRequestHeaders.Add("Referer", $"{_authUrl}/srun_portal_pc?ac_id={_acId}&theme=pro");

        // JSONP 请求需要特殊处理
        var jsonpCallback = $"jQuery{Random.Next(100000000, 999999999)}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
        var fullUrl = url + $"?callback={HttpUtility.UrlEncode(jsonpCallback)}&{string.Join("&", queryParams.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"))}&_={DateTimeOffset.Now.ToUnixTimeMilliseconds()}";

        var response = await client.GetAsync(fullUrl);
        var text = await response.Content.ReadAsStringAsync();

        try
        {
            var jsonpMatch = Regex.Match(text, @"jQuery\d+_\d+\((\{.*\})\)\s*;?\s*$", RegexOptions.Singleline);
            if (jsonpMatch.Success)
                text = jsonpMatch.Groups[1].Value;

            return JsonSerializer.Deserialize<LoginResult>(text) ?? new LoginResult { Error = "error" };
        }
        catch
        {
            return new LoginResult { Error = "error", ErrorMsg = text };
        }
    }

    public async Task<UserInfo> GetUserInfoAsync()
    {
        var data = await GetJsonAsync("/cgi-bin/rad_user_info", jsonp: true);

        var userInfo = new UserInfo();
        if (data.TryGetProperty("error", out var e)) userInfo.Error = e.GetString();
        if (data.TryGetProperty("user_name", out var u)) userInfo.UserName = u.GetString();
        if (data.TryGetProperty("online_ip", out var o)) userInfo.OnlineIp = o.GetString();
        if (data.TryGetProperty("client_ip", out var c)) userInfo.ClientIp = c.GetString();
        if (data.TryGetProperty("user_mac", out var m)) userInfo.UserMac = m.GetString();
        if (data.TryGetProperty("sum_bytes", out var sb)) userInfo.SumBytes = sb.GetInt64();
        if (data.TryGetProperty("sum_seconds", out var ss)) userInfo.SumSeconds = ss.GetInt64();
        if (data.TryGetProperty("user_balance", out var ub)) userInfo.UserBalance = ub.GetDecimal();

        return userInfo;
    }

    public async Task<DateTime?> GetExpireTimeAsync()
    {
        try
        {
            var data = await GetJsonAsync("/v1/srun_portal_expire_time");
            if (data.TryGetProperty("code", out var code) && code.GetInt32() == 0 &&
                data.TryGetProperty("data", out var ts) && ts.GetInt64() is var timestamp && timestamp > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[诊断] 获取到期时间失败: {e.Message}");
        }
        return null;
    }

    public async Task<LoginResult> LogoutAsync()
    {
        var data = await GetJsonAsync($"/cgi-bin/srun_portal?action=logout&username={HttpUtility.UrlEncode(UsernameWithDomain)}&ip={_ip}&ac_id={_acId}", jsonp: true);

        try
        {
            return JsonSerializer.Deserialize<LoginResult>(data.GetRawText()) ?? new LoginResult { Error = "ok" };
        }
        catch
        {
            return new LoginResult { Error = data.TryGetProperty("error", out var e) ? e.GetString() : "ok" };
        }
    }

    private string ExtractAcId(string html)
    {
        var patterns = new[]
        {
            @"<input[^>]*id=[""']ac_id[""'][^>]*value=[""'](\d+)[""']",
            @"<input[^>]*value=[""'](\d+)[""'][^>]*id=[""']ac_id[""']",
            @"var\s+ac_id\s*=\s*['""](\d+)['""]",
            @"var\s+acid\s*=\s*['""]?(\d+)['""]?",
            @"[""']?ac_id[""']?\s*:\s*[""']?(\d+)[""']?",
            @"[?&]ac_id=(\d+)",
            @"index_(\d+)\.html",
            @"srun_portal_pc\?ac_id=(\d+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Value != "1")
                return match.Groups[1].Value;
        }
        return null!;
    }

    private string ExtractIp(string html)
    {
        var patterns = new[]
        {
            @"<input[^>]*id=[""']ip[""'][^>]*value=[""']([\d.]+)[""']",
            @"ip\s*[:=]\s*[""']([\d.]+)[""']",
            @"userip\s*[:=]\s*[""']([\d.]+)[""']",
            @"client_ip\s*[:=]\s*[""']([\d.]+)[""']",
            @"online_ip\s*[:=]\s*[""']([\d.]+)[""']",
            @"""ip""\s*:\s*""([\d.]+)""",
            @"""client_ip""\s*:\s*""([\d.]+)""",
            @"var\s+ip\s*=\s*[""']?([\d.]+)[""']?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }
        return null!;
    }

    private static string ComputeHmacMd5(string key, string data)
    {
        using var hmac = new HMACMD5(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLower();
    }

    private static string ComputeSha1(string data)
    {
        using var sha1 = SHA1.Create();
        return BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLower();
    }

    private static string GetUserAgent() =>
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36 Edg/135.0.0.0";

    private static string GetAccept() =>
        "text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01";
}