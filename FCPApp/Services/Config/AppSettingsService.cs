using FCPApp.Models;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FCPApp.Services.Config;

public static class AppSettingsService
{
    private static readonly string ConfigDir = GetConfigDir();
    private static readonly string SettingsPath = Path.Combine(ConfigDir, "appsettings.json");

    #region Config paths

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

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Load app settings: {ex.Message}");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            settings = settings with
            {
                LastModified = DateTime.UtcNow,
                StartWithWindows = settings.StartWithWindows,
            };

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
            Console.WriteLine($"[SETTINGS] Saved to: {SettingsPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Save app settings: {ex.Message}");
        }
    }

    #endregion

    #region Auto start

    public static void ApplyStartWithWindows(bool enabled)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ApplyStartWithWindows_Windows(enabled);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                ApplyStartWithWindows_Linux(enabled);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                ApplyStartWithWindows_MacOS(enabled);

            Console.WriteLine($"[STARTUP] {(enabled ? "✅ Enabled" : "❌ Disabled")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] ApplyStartWithWindows: {ex.Message}");
        }
    }

    private static void ApplyStartWithWindows_Windows(bool enabled)
    {
        try
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "FCPApp";

            using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath, true))
            {
                if (key == null) return;

                if (enabled)
                {
                    string exePath = GetExecutablePath();
                    if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        var dotnetPath = FindDotnetPath();
                        exePath = $"\"{dotnetPath}\" \"{exePath}\"";
                    }
                    else
                    {
                        exePath = $"\"{exePath}\"";
                    }
                    key.SetValue(appName, exePath, RegistryValueKind.String);
                    Console.WriteLine($"[STARTUP] Registry set: {appName}");
                }
                else
                {
                    if (key.GetValue(appName) != null)
                        key.DeleteValue(appName, false);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] ApplyStartWithWindows_Windows: {ex.Message}");
        }
    }

    private static void ApplyStartWithWindows_Linux(bool enabled)
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home)) return;

        var autostartDir = Path.Combine(home, ".config", "autostart");
        var desktopFile = Path.Combine(autostartDir, "fcpapp.desktop");

        if (enabled)
        {
            Directory.CreateDirectory(autostartDir);
            string exePath = GetExecutablePath();
            string command = exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? $"/usr/bin/dotnet \"{exePath}\"" : $"\"{exePath}\"";

            var desktopContent = $"""
                [Desktop Entry]
                Type=Application
                Name=FCPApp
                Comment=Folder Cleanup Application
                Exec={command}
                Terminal=false
                X-GNOME-Autostart-enabled=true
                Hidden=false
                """;
            File.WriteAllText(desktopFile, desktopContent);
        }
        else if (File.Exists(desktopFile)) File.Delete(desktopFile);
    }

    private static void ApplyStartWithWindows_MacOS(bool enabled)
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home)) return;

        var launchAgentsDir = Path.Combine(home, "Library", "LaunchAgents");
        var plistFile = Path.Combine(launchAgentsDir, "com.fcpapp.startup.plist");

        if (enabled)
        {
            Directory.CreateDirectory(launchAgentsDir);
            string exePath = GetExecutablePath();
            string program = exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? "/usr/local/bin/dotnet" : exePath;

            var plistContent = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key><string>com.fcpapp.startup</string>
                    <key>ProgramArguments</key><array><string>{program}</string></array>
                    <key>RunAtLoad</key><true/>
                    <key>WorkingDirectory</key><string>{Path.GetDirectoryName(exePath) ?? "/"}</string>
                </dict>
                </plist>
                """;
            File.WriteAllText(plistFile, plistContent);
        }
        else if (File.Exists(plistFile)) File.Delete(plistFile);
    }

    public static bool IsStartWithWindowsEnabled()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath, false))
                {
                    if (key == null) return false;

                    var value = key.GetValue("FCPApp");
                    return value != null && !string.IsNullOrEmpty(value.ToString());
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home = Environment.GetEnvironmentVariable("HOME");

                if (string.IsNullOrEmpty(home)) return false;
                return File.Exists(Path.Combine(home, ".config", "autostart", "fcpapp.desktop"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetEnvironmentVariable("HOME");

                if (string.IsNullOrEmpty(home)) return false;
                return File.Exists(Path.Combine(home, "Library", "LaunchAgents", "com.fcpapp.startup.plist"));
            }
        }
        catch { }

        return false;
    }

    #endregion

    #region Helpers

    private static string GetExecutablePath()
        => Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? AppContext.BaseDirectory;

    private static string FindDotnetPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet");
                if (File.Exists(candidate)) return candidate;
            }
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pf = Environment.GetEnvironmentVariable("ProgramFiles");
            var candidate = Path.Combine(pf ?? "", "dotnet", "dotnet.exe");
            if (File.Exists(candidate)) return candidate;
        }
        else
        {
            if (File.Exists("/usr/bin/dotnet")) return "/usr/bin/dotnet";
            if (File.Exists("/usr/local/bin/dotnet")) return "/usr/local/bin/dotnet";
        }

        return "dotnet";
    }

    #endregion
}