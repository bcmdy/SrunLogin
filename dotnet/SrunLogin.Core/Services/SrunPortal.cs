using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SrunLogin.Crypto;
using SrunLogin.Models;

namespace SrunLogin.Services;

/// <summary>
/// Srun 门户认证服务（参数顺序严格保持与 Python 原版一致）
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
    // private readonly CookieContainer _cookieContainer = new();
    private HttpClient _httpClient;

    private static readonly Random Random = new();
    private readonly string[] _acIdCandidates = ["143", "2", "3", "5", "10", "15", "20", "100"];

    public Action<string>? DebugLog { get; set; }

    public SrunPortal(string authUrl, string username, string password, string? acId = null, string? ip = null, string domain = "")
    {
        _authUrl = authUrl.TrimEnd('/');
        _username = username;
        _password = password;
        _domain = domain;
        _acId = acId;
        _ip = ip;

        // 共享 HttpClient 和 CookieContainer，保持会话一致性
        var handler = new HttpClientHandler { CookieContainer = _cookieContainer };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
        _httpClient.DefaultRequestHeaders.Add("Accept", GetAccept());
        _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
    }

    private void LogDebug(string message)
    {
        DebugLog?.Invoke(message);
    }

    private string UsernameWithDomain => _username + _domain;

    public string? GetDetectedIp() => _ip;
    public string? GetDetectedAcId() => _acId;

    public async Task DetectInfoAsync(CancellationToken cancellationToken = default)
    {
        LogDebug($"[诊断] 开始探测 IP/AC_ID...");

        if (!string.IsNullOrEmpty(_acId) && !string.IsNullOrEmpty(_ip))
        {
            LogDebug($"[诊断] 已提供 IP={_ip}, AC_ID={_acId}");
            return;
        }

        // 1. 访问首页
        try
        {
            var (html, finalUrl) = await FetchHtmlAsync("/", cancellationToken);
            LogDebug($"[诊断] 首页最终 URL: {finalUrl}");

            var acIdFromHtml = ExtractAcId(html);
            if (!string.IsNullOrEmpty(acIdFromHtml))
                _acId = _acId ?? acIdFromHtml;

            if (string.IsNullOrEmpty(_ip))
            {
                var ipFromHtml = ExtractIp(html);
                if (!string.IsNullOrEmpty(ipFromHtml))
                {
                    _ip = ipFromHtml;
                    LogDebug($"[诊断] 从首页 HTML 提取 IP: {_ip}");
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            LogDebug($"[诊断] 首页探测失败: {e.Message}");
        }

        // 2. ac_detect 接口
        if (string.IsNullOrEmpty(_acId) || _acId == "1")
        {
            try
            {
                LogDebug("[诊断] 尝试 ac_detect 接口获取真实 ac_id...");
                var data = await GetJsonAsync("/v1/srun_portal_detect", cancellationToken: cancellationToken);
                LogDebug($"[诊断] ac_detect 返回: {JsonSerializer.Serialize(data)}");

                string? redirect = null, pcUrl = null, mobileUrl = null;

                if (data.TryGetProperty("Redirect", out var r) && r.ValueKind == JsonValueKind.String) redirect = r.GetString();
                if (data.TryGetProperty("Pc", out var p) && p.ValueKind == JsonValueKind.String) pcUrl = p.GetString();
                if (data.TryGetProperty("Mobile", out var m) && m.ValueKind == JsonValueKind.String) mobileUrl = m.GetString();

                var targetUrl = redirect ?? pcUrl ?? mobileUrl;
                if (!string.IsNullOrEmpty(targetUrl))
                {
                    var match = Regex.Match(targetUrl, @"[?&]ac_id=(\d+)");
                    if (match.Success)
                    {
                        _acId = match.Groups[1].Value;
                        LogDebug($"[诊断] 从 ac_detect 重定向 URL 获取 ac_id: {_acId}");
                    }
                }

                if ((string.IsNullOrEmpty(_acId) || _acId == "1") && data.TryGetProperty("ac_id", out var acid) && acid.ValueKind == JsonValueKind.Number)
                {
                    _acId = acid.GetInt32().ToString();
                    LogDebug($"[诊断] 从 ac_detect 数据获取 ac_id: {_acId}");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                LogDebug($"[诊断] ac_detect 失败: {e.Message}");
            }
        }

        // 3. srun_portal_pc
        if (string.IsNullOrEmpty(_acId) || _acId == "1")
        {
            try
            {
                LogDebug("[诊断] 尝试访问 srun_portal_pc 获取真实 ac_id...");
                var (html, finalUrl) = await FetchHtmlAsync("/srun_portal_pc", cancellationToken);
                LogDebug($"[诊断] srun_portal_pc 最终 URL: {finalUrl}");

                var match = Regex.Match(finalUrl, @"[?&]ac_id=(\d+)");
                if (match.Success)
                {
                    _acId = match.Groups[1].Value;
                    LogDebug($"[诊断] 从 srun_portal_pc URL 提取 ac_id: {_acId}");
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
                        LogDebug($"[诊断] 从 srun_portal_pc HTML 提取 IP: {_ip}");
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                LogDebug($"[诊断] srun_portal_pc 探测失败: {e.Message}");
            }
        }

        // 4. 尝试候选 ac_id
        if (string.IsNullOrEmpty(_acId) || _acId == "1")
        {
            LogDebug($"[诊断] 尝试候选 ac_id 列表: {string.Join(", ", _acIdCandidates)}");
            foreach (var testAcId in _acIdCandidates)
            {
                try
                {
                    var oldAcId = _acId;
                    _acId = testAcId;
                    var data = await GetChallengeAsync(cancellationToken);
                    _acId = oldAcId;

                    if (data.Error == "ok" && !string.IsNullOrEmpty(data.Challenge))
                    {
                        LogDebug($"[诊断] ac_id={testAcId} 的 get_challenge 成功");
                        _acId = testAcId;
                        break;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    LogDebug($"[诊断] ac_id={testAcId} 测试失败: {e.Message}");
                }
            }
        }

        // 5. rad_user_info 获取 IP
        if (string.IsNullOrEmpty(_ip))
        {
            try
            {
                LogDebug("[诊断] 尝试 JSONP 模式 rad_user_info 获取 IP...");
                var data = await GetJsonAsync("/cgi-bin/rad_user_info", null, true, cancellationToken);
                if (data.TryGetProperty("client_ip", out var clientIp) && clientIp.ValueKind == JsonValueKind.String)
                    _ip = clientIp.GetString();
                if (string.IsNullOrEmpty(_ip) && data.TryGetProperty("online_ip", out var onlineIp) && onlineIp.ValueKind == JsonValueKind.String)
                    _ip = onlineIp.GetString();
                if (!string.IsNullOrEmpty(_ip))
                    LogDebug($"[诊断] 从 JSONP rad_user_info 获取 IP: {_ip}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                LogDebug($"[诊断] JSONP rad_user_info 失败: {e.Message}");
            }
        }

        // 6. 最终校验
        if (string.IsNullOrEmpty(_ip))
            throw new InvalidOperationException("无法自动获取本机 IP，请手动指定 --ip");

        if (string.IsNullOrEmpty(_acId))
        {
            LogDebug("[警告] 无法自动获取 ac_id，使用默认值 1");
            _acId = "1";
        }

        LogDebug($"[诊断] 探测结果: IP={_ip}, AC_ID={_acId}");
    }

    private async Task<(string Html, string FinalUrl)> FetchHtmlAsync(string path, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _authUrl + path);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return (html, response.RequestMessage?.RequestUri?.ToString() ?? _authUrl + path);
    }

    /// <summary>
    /// 核心修复：使用有序参数列表，确保参数顺序与 Python urllib.parse.urlencode 完全一致
    /// </summary>
    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var sb = new StringBuilder();
        foreach (var kv in parameters)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kv.Value));
        }
        return sb.ToString();
    }

    /// <summary>
    /// 统一 GET 请求方法。使用 List<KeyValuePair> 保持参数顺序。
    /// </summary>
    private async Task<JsonElement> GetJsonAsync(string path, List<KeyValuePair<string, string>>? parameters = null, bool jsonp = false, CancellationToken cancellationToken = default)
    {
        var orderedParams = new List<KeyValuePair<string, string>>();
        if (parameters != null)
            orderedParams.AddRange(parameters);

        if (jsonp)
        {
            var callback = $"jQuery{Random.Next(100000000, 999999999)}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
            orderedParams.Add(new KeyValuePair<string, string>("callback", callback));
            orderedParams.Add(new KeyValuePair<string, string>("_", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString()));
        }

        var url = _authUrl + path;
        if (orderedParams.Count > 0)
            url += "?" + BuildQueryString(orderedParams);

        LogDebug($"[诊断] 请求: {url[..Math.Min(130, url.Length)]}...");

        // 更新 Referer（先移除再添加，避免重复）
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", $"{_authUrl}/srun_portal_pc?ac_id={_acId}&theme=pro");

        var text = await _httpClient.GetStringAsync(url, cancellationToken);
        LogDebug($"[诊断] 响应: {text[..Math.Min(200, text.Length)]}");
        return ParseResponse(text);
    }

    private static string? SafeGetString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

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

        if (text == "bad_request_parameters")
            return JsonDocument.Parse("{\"error\":\"bad_request_parameters\"}").RootElement;

        if (text.StartsWith("challenge="))
        {
            // 修复：Split('=', 1) 在 C# 中只会返回 1 个元素，必须用 2
            var challenge = text.Split('=', 2)[1];
            return JsonDocument.Parse($"{{\"error\":\"ok\",\"challenge\":\"{challenge}\"}}").RootElement;
        }

        // 尝试直接 JSON 解析
        try
        {
            return JsonDocument.Parse(text).RootElement;
        }
        catch { }

        // JSONP: jQuery123({...});  使用通用正则匹配，与 Python 一致
        var jsonpMatch = Regex.Match(text, @"[^(]*\((.*)\)\s*;?\s*$", RegexOptions.Singleline);
        if (jsonpMatch.Success)
        {
            try
            {
                return JsonDocument.Parse(jsonpMatch.Groups[1].Value).RootElement;
            }
            catch { }
        }

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

    private async Task<ChallengeResult> GetChallengeAsync(CancellationToken cancellationToken = default)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("username", UsernameWithDomain),
            new("ip", _ip ?? "0.0.0.0")
        };

        var data = await GetJsonAsync("/cgi-bin/get_challenge", parameters, jsonp: true, cancellationToken);

        if (data.TryGetProperty("challenge", out var challenge))
            return new ChallengeResult { Error = "ok", Challenge = SafeGetString(challenge) };

        if (data.TryGetProperty("error", out var err) && SafeGetString(err) == "ok")
        {
            // 尝试无 callback 模式
            var data2 = await GetJsonAsync("/cgi-bin/get_challenge", parameters, jsonp: false, cancellationToken);
            if (data2.TryGetProperty("challenge", out var challenge2))
                return new ChallengeResult { Error = "ok", Challenge = SafeGetString(challenge2) };
            return new ChallengeResult { Error = "ok", Challenge = "" };
        }

        throw new InvalidOperationException($"获取 challenge 失败: {data}");
    }
    public async Task<LoginResult> LoginAsync(CancellationToken cancellationToken = default)
    {
        LogDebug($"[登录] 账号: {_username}, IP: {_ip}, AC_ID: {_acId}");

        var challenge = await GetChallengeAsync(cancellationToken);
        var token = challenge.Challenge ?? "";
        LogDebug($"[登录] 获取 token: {(token.Length > 8 ? token[..8] : token)}...");

        // 严格按照 Python 原版的参数顺序（服务器对此极其敏感）
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("action", "login"),
            new("username", UsernameWithDomain),
            new("password", ""),
            new("os", "Windows 10"),
            new("name", "Windows"),
            new("double_stack", "0"),
            new("chksum", ""),
            new("info", ""),
            new("ac_id", _acId!),
            new("ip", _ip!),
            new("n", "200"),
            new("type", "1")
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

            // 按顺序替换占位值
            parameters[2] = new KeyValuePair<string, string>("password", "{MD5}" + hmd5);
            parameters[6] = new KeyValuePair<string, string>("chksum", chksum);
            parameters[7] = new KeyValuePair<string, string>("info", i);
        }
        else
        {
            LogDebug("[登录] 使用老版本明文密码模式");
            parameters[2] = new KeyValuePair<string, string>("password", _password);
        }

        var result = await GetJsonAsync("/cgi-bin/srun_portal", parameters, true, cancellationToken);
        return ParseLoginResult(result);
    }

    public async Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetJsonAsync("/cgi-bin/rad_user_info", null, true, cancellationToken);

        var userInfo = new UserInfo();
        if (data.TryGetProperty("error", out var e)) userInfo.Error = SafeGetString(e);
        if (data.TryGetProperty("user_name", out var u)) userInfo.UserName = SafeGetString(u);
        if (data.TryGetProperty("online_ip", out var o)) userInfo.OnlineIp = SafeGetString(o);
        if (data.TryGetProperty("client_ip", out var c)) userInfo.ClientIp = SafeGetString(c);
        if (data.TryGetProperty("user_mac", out var m)) userInfo.UserMac = SafeGetString(m);
        if (data.TryGetProperty("sum_bytes", out var sb) && sb.ValueKind == JsonValueKind.Number) userInfo.SumBytes = sb.GetInt64();
        if (data.TryGetProperty("sum_seconds", out var ss) && ss.ValueKind == JsonValueKind.Number) userInfo.SumSeconds = ss.GetInt64();
        if (data.TryGetProperty("user_balance", out var ub) && ub.ValueKind == JsonValueKind.Number) userInfo.UserBalance = ub.GetDecimal();

        return userInfo;
    }

    public async Task<DateTime?> GetExpireTimeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await GetJsonAsync("/v1/srun_portal_expire_time", null, false, cancellationToken);
            if (data.TryGetProperty("code", out var code) && code.GetInt32() == 0 &&
                data.TryGetProperty("data", out var ts) && ts.GetInt64() is var timestamp && timestamp > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            LogDebug($"[诊断] 获取到期时间失败: {e.Message}");
        }
        return null;
    }

    public async Task<LoginResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("action", "logout"),
            new("username", UsernameWithDomain),
            new("ip", _ip!),
            new("ac_id", _acId!)
        };

        var data = await GetJsonAsync("/cgi-bin/srun_portal", parameters, jsonp: true, cancellationToken);
        return ParseLoginResult(data);
    }

    private static LoginResult ParseLoginResult(JsonElement data)
    {
        try
        {
            return JsonSerializer.Deserialize<LoginResult>(data.GetRawText()) ?? new LoginResult { Error = "error" };
        }
        catch
        {
            var result = new LoginResult { Error = "error" };
            if (data.TryGetProperty("error", out var e)) result.Error = SafeGetString(e);
            if (data.TryGetProperty("error_msg", out var em)) result.ErrorMsg = SafeGetString(em);
            if (data.TryGetProperty("ecode", out var ec)) result.Ecode = SafeGetString(ec);
            if (data.TryGetProperty("message", out var msg)) result.Message = SafeGetString(msg);
            if (data.TryGetProperty("suc_msg", out var sm)) result.SucMsg = SafeGetString(sm);
            if (data.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number) result.Code = c.GetInt32();
            return result;
        }
    }

    private string ExtractAcId(string html)
    {
        var candidates = new List<string>();
        var patterns = new[]
        {
            @"<<input[^>]*id=[""']ac_id[""'][^>]*value=[""'](\d+)[""']",
            @"<<input[^>]*value=[""'](\d+)[""'][^>]*id=[""']ac_id[""']",
            @"var\s+ac_id\s*=\s*['""](\d+)['""]",
            @"var\s+acid\s*=\s*['""]?(\d+)['""]?",
            @"[""']?ac_id[""']?\s*:\s*[""']?(\d+)[""']?",
            @"[""']?acid[""']?\s*:\s*[""']?(\d+)[""']?",
            @"[?&]ac_id=(\d+)",
            @"index_(\d+)\.html",
            @"srun_portal_pc\?ac_id=(\d+)"
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
            {
                if (match.Success)
                    candidates.Add(match.Groups[1].Value);
            }
        }

        if (candidates.Count == 0)
            return null!;

        // 统计频率，优先选择非 "1" 的值（与 Python Counter 逻辑一致）
        var counts = candidates.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        LogDebug($"[诊断] HTML 中发现 ac_id 候选: {JsonSerializer.Serialize(counts)}");

        var nonOne = counts.Where(c => c.Key != "1").ToList();
        if (nonOne.Count > 0)
        {
            var best = nonOne.OrderByDescending(c => c.Value).First().Key;
            LogDebug($"[诊断] 选择非默认 ac_id: {best}");
            return best;
        }
        return counts.OrderByDescending(c => c.Value).First().Key;
    }

    private string ExtractIp(string html)
    {
        var patterns = new[]
        {
            @"<<input[^>]*id=[""']ip[""'][^>]*value=[""']([\d.]+)[""']",
            @"<<input[^>]*value=[""']([\d.]+)[""'][^>]*id=[""']ip[""']",
            @"ip\s*[:=]\s*[""']([\d.]+)[""']",
            @"userip\s*[:=]\s*[""']([\d.]+)[""']",
            @"client_ip\s*[:=]\s*[""']([\d.]+)[""']",
            @"online_ip\s*[:=]\s*[""']([\d.]+)[""']",
            @"""ip""\s*:\s*""([\d.]+)""",
            @"""client_ip""\s*:\s*""([\d.]+)""",
            @"""online_ip""\s*:\s*""([\d.]+)""",
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