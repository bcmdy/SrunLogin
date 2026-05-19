using System.Text.Json.Serialization;

namespace SrunLogin.Models;

/// <summary>
/// 登录结果
/// </summary>
public class LoginResult
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_msg")]
    public string? ErrorMsg { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("suc_msg")]
    public string? SucMsg { get; set; }

    [JsonPropertyName("ecode")]
    public string? Ecode { get; set; }

    [JsonPropertyName("online_ip")]
    public string? OnlineIp { get; set; }

    public bool IsSuccess => Error == "ok" || Code == 0;
}

/// <summary>
/// 用户在线信息
/// </summary>
public class UserInfo
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("online_ip")]
    public string? OnlineIp { get; set; }

    [JsonPropertyName("client_ip")]
    public string? ClientIp { get; set; }

    [JsonPropertyName("user_mac")]
    public string? UserMac { get; set; }

    [JsonPropertyName("sum_bytes")]
    public long? SumBytes { get; set; }

    [JsonPropertyName("sum_seconds")]
    public long? SumSeconds { get; set; }

    [JsonPropertyName("user_balance")]
    public decimal? UserBalance { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtraData { get; set; }
}

/// <summary>
/// 到期时间
/// </summary>
public class ExpireTimeResult
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public long? Data { get; set; }
}

/// <summary>
/// Challenge 响应
/// </summary>
public class ChallengeResult
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("challenge")]
    public string? Challenge { get; set; }
}

/// <summary>
/// 应用配置
/// </summary>
public class Config
{
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Ip { get; set; }
    public string? Domain { get; set; }
    public string? AcId { get; set; }
    public bool AutoLogin { get; set; }
    public LoopConfig? Loop { get; set; }
}

/// <summary>
/// 循环检测配置
/// </summary>
public class LoopConfig
{
    public bool Enable { get; set; }
    public int Interval { get; set; } = 10;
    public int Timeout { get; set; } = 3;
    public string PingHost { get; set; } = "www.baidu.com";
}