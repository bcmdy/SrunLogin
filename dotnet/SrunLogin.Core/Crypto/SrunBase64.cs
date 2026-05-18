using System.Security.Cryptography;
using System.Text;

namespace SrunLogin.Crypto;

/// <summary>
/// Srun 自定义 Base64 编码
/// </summary>
public static class SrunBase64
{
    private const string StdAlpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    private const string CustomAlpha = "LVoJPiCN2R8G90yg+hmFHuacZ1OWMnrsSTXkYpUq/3dlbfKwv6xztjI7DeBE45QA";

    private static readonly Dictionary<char, int> CustomReverse = BuildReverse(CustomAlpha);

    private static Dictionary<char, int> BuildReverse(string alpha)
    {
        var dict = new Dictionary<char, int>();
        for (int i = 0; i < alpha.Length; i++)
            dict[alpha[i]] = i;
        return dict;
    }

    public static string Encode(byte[] data)
    {
        var stdEncoded = Convert.ToBase64String(data);
        var result = new StringBuilder();
        foreach (var c in stdEncoded)
        {
            int idx = StdAlpha.IndexOf(c);
            result.Append(idx >= 0 ? CustomAlpha[idx] : c);
        }
        return result.ToString();
    }

    public static byte[] Decode(string encoded)
    {
        var stdEncoded = new StringBuilder();

        foreach (var c in encoded)
        {
            if (c == '=') continue;
            stdEncoded.Append(CustomReverse.TryGetValue(c, out int idx) ? StdAlpha[idx] : c);
        }

        // 补足 Base64 填充
        while (stdEncoded.Length % 4 != 0)
            stdEncoded.Append('=');

        return Convert.FromBase64String(stdEncoded.ToString());
    }
}