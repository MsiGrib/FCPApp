using FCPApp.Models;
using System;
using System.IO;
using System.Text.Json;

namespace FCPApp.Services.Config;

public static class ConfigService
{
    private static readonly string ConfigDir = GetConfigDir();
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static string GetConfigDir()
    {
        try
        {
            var appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create
            );

            if (!string.IsNullOrEmpty(appData))
                return Path.Combine(appData, "FCPApp");
        }
        catch { }

        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfig))
                return Path.Combine(xdgConfig, "FCPApp");

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                return Path.Combine(home, ".config", "FCPApp");
        }

        return Path.Combine(AppContext.BaseDirectory, "config");
    }

    public static FolderConfig? Load()
    {
        if (!File.Exists(ConfigPath)) return null;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<FolderConfig>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Load config: {ex.Message}");
            return null;
        }
    }

    public static void Save(FolderConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var options = new JsonSerializerOptions { WriteIndented = true };

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, options));
            Console.WriteLine($"[CONFIG] Saved to: {ConfigPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Save config: {ex.Message}");
        }
    }
}