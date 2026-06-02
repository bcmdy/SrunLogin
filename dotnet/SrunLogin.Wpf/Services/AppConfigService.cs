using System.IO;
using System.Text.Json;
using SrunLogin.Models;

namespace SrunLogin.Wpf.Services;

public sealed class AppConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public Config? Load()
    {
        if (!File.Exists(ConfigPath))
            return null;

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<Config>(json);
    }

    public void Save(Config config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    public void Delete()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
    }
}

