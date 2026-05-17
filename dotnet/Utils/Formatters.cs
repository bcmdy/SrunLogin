namespace SrunLogin.Utils;

public static class Formatters
{
    public static string FormatFlow(long? bytes, int mode = 1024)
    {
        if (!bytes.HasValue || bytes == 0)
            return "0 B";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double val = bytes.Value;
        int idx = 0;

        while (val >= mode && idx < units.Length - 1)
        {
            val /= mode;
            idx++;
        }

        return $"{val:F2} {units[idx]}";
    }

    public static string FormatTime(long? seconds)
    {
        if (!seconds.HasValue || seconds == 0)
            return "0 秒";

        long s = seconds.Value;
        var parts = new List<string>();

        long d = s / 86400;
        s %= 86400;
        long h = s / 3600;
        s %= 3600;
        long m = s / 60;
        s %= 60;

        if (d > 0) parts.Add($"{d}天");
        if (h > 0) parts.Add($"{h}小时");
        if (m > 0) parts.Add($"{m}分");
        if (s > 0 || parts.Count == 0) parts.Add($"{s}秒");

        return string.Join("", parts);
    }
}