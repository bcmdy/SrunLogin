using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SrunLogin.Services;

public static class SrunProtocol
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    public static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var sb = new StringBuilder();
        foreach (var kv in parameters)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(QuotePlus(kv.Key));
            sb.Append('=');
            sb.Append(QuotePlus(kv.Value));
        }
        return sb.ToString();
    }

    private static string QuotePlus(string value)
    {
        return Uri.EscapeDataString(value).Replace("%20", "+");
    }

    public static string BuildLoginInfoJson(string username, string password, string? ip, string? acId)
    {
        var infoObj = new
        {
            username,
            password,
            ip,
            acid = acId,
            enc_ver = "srun_bx1"
        };
        return JsonSerializer.Serialize(infoObj, CompactJsonOptions);
    }

    public static JsonElement ParseResponse(string text)
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
            var challenge = text.Split('=', 2)[1];
            return JsonDocument.Parse($"{{\"error\":\"ok\",\"challenge\":\"{challenge}\"}}").RootElement;
        }

        try
        {
            return JsonDocument.Parse(text).RootElement;
        }
        catch { }

        var jsonpMatch = Regex.Match(text, @"[^(]*\((.*)\)\s*;?\s*$", RegexOptions.Singleline);
        if (jsonpMatch.Success)
        {
            try
            {
                return JsonDocument.Parse(jsonpMatch.Groups[1].Value).RootElement;
            }
            catch { }
        }

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

    public static string? ExtractAcId(string html, Action<string>? logDebug = null)
    {
        var candidates = new List<string>();
        var patterns = new[]
        {
            @"<input[^>]*id=[""']ac_id[""'][^>]*value=[""'](\d+)[""']",
            @"<input[^>]*value=[""'](\d+)[""'][^>]*id=[""']ac_id[""']",
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
            return null;

        var counts = candidates.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        logDebug?.Invoke($"[诊断] HTML 中发现 ac_id 候选: {JsonSerializer.Serialize(counts)}");

        var nonOne = counts.Where(c => c.Key != "1").ToList();
        if (nonOne.Count > 0)
        {
            var best = nonOne.OrderByDescending(c => c.Value).First().Key;
            logDebug?.Invoke($"[诊断] 选择非默认 ac_id: {best}");
            return best;
        }
        return counts.OrderByDescending(c => c.Value).First().Key;
    }

    public static string? ExtractIp(string html)
    {
        var patterns = new[]
        {
            @"<input[^>]*id=[""']ip[""'][^>]*value=[""']([\d.]+)[""']",
            @"<input[^>]*value=[""']([\d.]+)[""'][^>]*id=[""']ip[""']",
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
        return null;
    }

    public static string ComputeHmacMd5(string key, string data)
    {
        using var hmac = new HMACMD5(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLower();
    }

    public static string ComputeSha1(string data)
    {
        using var sha1 = SHA1.Create();
        return BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLower();
    }
}
