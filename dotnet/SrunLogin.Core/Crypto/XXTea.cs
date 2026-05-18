using System.Text;

namespace SrunLogin.Crypto;

/// <summary>
/// XXTEA 加密算法实现（严格遵循 Python 原版逻辑）
/// </summary>
public static class XXTea
{
    private const uint Delta = 0x9E3779B9;

    /// <summary>
    /// 加密字符串
    /// </summary>
    public static string Encrypt(string plaintext, string key)
    {
        var v = StrToUints(plaintext, addLength: true);
        var k = StrToUints(key, addLength: false);

        while (k.Count < 4)
            k.Add(0);

        int n = v.Count - 1;
        if (n < 1)
            return string.Empty;

        uint z = v[n];
        uint y = v[0];
        uint d = 0;

        int q = 6 + 52 / (n + 1);
        while (q > 0)
        {
            q--;
            d = (d + Delta) & 0xFFFFFFFF;
            uint e = (d >> 2) & 3;

            for (int p = 0; p < n; p++)
            {
                y = v[p + 1];
                
                // 严格按照 Python 原版的计算顺序（分步计算，避免 C# 运算符优先级差异）
                uint m = ((z & 0xFFFFFFFF) >> 5) ^ ((y << 2) & 0xFFFFFFFF);
                m += (((y & 0xFFFFFFFF) >> 3) ^ ((z << 4) & 0xFFFFFFFF)) ^ (d ^ y);
                m += (k[(p & 3) ^ (int)e] ^ z) & 0xFFFFFFFF;
                
                v[p] = (v[p] + m) & 0xFFFFFFFF;
                z = v[p];
            }

            y = v[0];
            
            uint m2 = ((z & 0xFFFFFFFF) >> 5) ^ ((y << 2) & 0xFFFFFFFF);
            m2 += (((y & 0xFFFFFFFF) >> 3) ^ ((z << 4) & 0xFFFFFFFF)) ^ (d ^ y);
            m2 += (k[(n & 3) ^ (int)e] ^ z) & 0xFFFFFFFF;
            
            v[n] = (v[n] + m2) & 0xFFFFFFFF;
            z = v[n];
        }

        return UintsToStr(v);
    }

    private static List<uint> StrToUints(string s, bool addLength)
    {
        var v = new List<uint>();
        int n = s.Length;

        for (int i = 0; i < n; i += 4)
        {
            uint val = (uint)(i < n ? s[i] : 0);
            val |= (uint)((i + 1 < n ? s[i + 1] : 0) << 8);
            val |= (uint)((i + 2 < n ? s[i + 2] : 0) << 16);
            val |= (uint)((i + 3 < n ? s[i + 3] : 0) << 24);
            v.Add(val);
        }

        if (addLength)
            v.Add((uint)n);

        return v;
    }

    private static string UintsToStr(List<uint> v)
    {
        var chars = new StringBuilder();
        foreach (var num in v)
        {
            chars.Append((char)(num & 0xff));
            chars.Append((char)((num >> 8) & 0xff));
            chars.Append((char)((num >> 16) & 0xff));
            chars.Append((char)((num >> 24) & 0xff));
        }
        return chars.ToString();
    }
}