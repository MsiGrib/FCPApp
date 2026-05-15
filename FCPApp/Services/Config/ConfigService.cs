using FCPApp.Models;
using System;
using System.IO;
using System.Linq;
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

    public static AppConfig? Load()
    {
        if (!File.Exists(ConfigPath)) return null;

        try
        {
            var json = File.ReadAllText(ConfigPath);

            var newConfig = JsonSerializer.Deserialize<AppConfig>(json);
            if (newConfig?.Profiles?.Count > 0)
            {
                newConfig.EnsureDefaultProfile();
                return newConfig;
            }

            var legacyConfig = JsonSerializer.Deserialize<LegacyFolderConfig>(json);
            if (legacyConfig != null) return MigrateFromLegacy(legacyConfig);

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Load config: {ex.Message}");
            try
            {
                File.Delete(ConfigPath);
            }
            catch { }

            return null;
        }
    }

    private static AppConfig MigrateFromLegacy(LegacyFolderConfig legacy)
    {
        var newConfig = new AppConfig();
        var defaultProfile = new Profile
        {
            Name = "Default (Migrated)",
            RootPath = legacy.RootPath,
            SelectedFolderPaths = legacy.SelectedFolderPaths,
            SkipAllErrors = legacy.SkipAllErrors,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        newConfig.Profiles.Add(defaultProfile);
        newConfig.CurrentProfileId = defaultProfile.Id;

        Save(newConfig);

        Console.WriteLine("[CONFIG] Migrated from legacy format");
        return newConfig;
    }

    public static void Save(AppConfig config)
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

    public static Profile CreateProfile(string name)
    {
        return new Profile
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static bool AddProfile(AppConfig config, Profile profile)
    {
        if (config.Profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
            return false;

        config.Profiles.Add(profile);
        if (config.CurrentProfileId == null)
            config.CurrentProfileId = profile.Id;

        Save(config);

        return true;
    }

    public static bool DeleteProfile(AppConfig config, string profileId)
    {
        var profile = config.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return false;

        if (config.Profiles.Count <= 1) return false;

        config.Profiles.Remove(profile);

        if (config.CurrentProfileId == profileId)
            config.CurrentProfileId = config.Profiles.First().Id;

        Save(config);

        return true;
    }

    public static bool RenameProfile(AppConfig config, string profileId, string newName)
    {
        var profile = config.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return false;

        if (config.Profiles.Any(p => p.Id != profileId &&
            p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            return false;

        var index = config.Profiles.IndexOf(profile);
        if (index == -1) return false;

        var updatedProfile = profile with
        {
            Name = newName,
            UpdatedAt = DateTime.UtcNow
        };

        config.Profiles[index] = updatedProfile;
        Save(config);

        return true;
    }

    public static bool SwitchProfile(AppConfig config, string profileId)
    {
        if (!config.Profiles.Any(p => p.Id == profileId)) return false;

        config.CurrentProfileId = profileId;
        Save(config);

        return true;
    }

    public static void UpdateCurrentProfile(AppConfig config, Action<Profile> updateAction)
    {
        var current = config.CurrentProfile;
        if (current == null) return;

        var index = config.Profiles.IndexOf(current);
        if (index == -1) return;

        updateAction(current);

        config.Profiles[index] = current with { UpdatedAt = DateTime.UtcNow };

        Save(config);
    }
}