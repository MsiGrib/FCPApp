using Avalonia;
using FCPApp.Services.Config;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FCPApp;

public class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FCPApp", "startup.log"
    );

    [STAThread]
    public static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        Log($"[=== {DateTime.Now:HH:mm:ss} ===] App starting...");

        try
        {
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //{
            //    if (!IsRunningAsAdmin())
            //    {
            //        Log("[ADMIN] Not admin, requesting elevation...");
            //        RestartAsAdmin(args);
            //        Log("[ADMIN] RestartAsAdmin called, exiting current process");
            //        return;
            //    }
            //    Log("[ADMIN] Running as admin ✓");
            //}
            //else
            //{
            //    WarnAboutPermissions();
            //}

            Log("[CONFIG] Checking startup status...");
            CheckStartupStatus();

            Log("[AVALONIA] Starting application...");

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            Log("[EXIT] Application shutdown normally");
        }
        catch (Exception ex)
        {
            Log($"[FATAL] Unhandled exception: {ex}");
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine($"📄 Log: {LogPath}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MessageBoxShow("FCPApp Error", $"Failed to start:\n{ex.Message}\n\nLog: {LogPath}");
            }

            Environment.Exit(1);
        }
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Console.WriteLine(line);
        try { File.AppendAllText(LogPath, line + "\n"); } catch { }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        Log("[AVALONIA] Building AppBuilder...");
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .With(new X11PlatformOptions { EnableIme = true });
    }

    public static void CheckStartupStatus()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                bool isEnabled = AppSettingsService.IsStartWithWindowsEnabled();
                Log($"[STARTUP] Auto-start: {(isEnabled ? "✅ ENABLED" : "❌ DISABLED")}");

                var settings = AppSettingsService.Load();
                if (settings.StartWithWindows != isEnabled)
                {
                    Log("[STARTUP] ⚠️ Registry/config mismatch!");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[STARTUP] Error: {ex.Message}");
        }
    }

    #region Admin helpers (без изменений)

    private static bool IsRunningAsAdmin()
    {
        try
        {
            var identityType = Type.GetType("System.Security.Principal.WindowsIdentity, System.Security.Principal.Windows");
            if (identityType == null) return false;

            var getCurrent = identityType.GetMethod("GetCurrent", Type.EmptyTypes);
            var identity = getCurrent?.Invoke(null, null);
            if (identity == null) return false;

            var principalType = Type.GetType("System.Security.Principal.WindowsPrincipal, System.Security.Principal.Windows");
            var constructor = principalType?.GetConstructor(new[] { identityType });
            var principal = constructor?.Invoke(new[] { identity });

            var roleEnum = Type.GetType("System.Security.Principal.WindowsBuiltInRole, System.Security.Principal.Windows");
            var adminValue = Enum.Parse(roleEnum, "Administrator");

            var isInRole = principalType?.GetMethod("IsInRole", new[] { roleEnum });
            var result = (bool?)isInRole?.Invoke(principal, new[] { adminValue }) == true;

            Log($"[ADMIN] IsInRole(Administrator) = {result}");
            return result;
        }
        catch (Exception ex)
        {
            Log($"[ADMIN] Error: {ex.Message}");
            return false;
        }
    }

    private static void RestartAsAdmin(string[] args)
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Environment.ProcessPath
                ?? AppContext.BaseDirectory;

            Log($"[RESTART] Exe path: {exePath}");

            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var exeWithoutDll = exePath.Replace(".dll", ".exe");
                if (File.Exists(exeWithoutDll))
                {
                    exePath = exeWithoutDll;
                    Log($"[RESTART] Using .exe: {exePath}");
                }
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var startInfo = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };

                foreach (var arg in args) startInfo.ArgumentList.Add(arg);

                Log("[RESTART] Starting elevated process...");
                var proc = Process.Start(startInfo);

                if (proc != null)
                {
                    Log($"[RESTART] ✅ Started PID={proc.Id}");
                }
                else
                {
                    Log("[RESTART] ❌ Process.Start returned null");
                }
                return;
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Log("[RESTART] ⚠️ User cancelled UAC prompt");
        }
        catch (Exception ex)
        {
            Log($"[RESTART] ❌ Error: {ex}");
        }

        Console.WriteLine("⚠️ Failed to obtain administrator rights.");
        Console.WriteLine($"📄 Log: {LogPath}");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        Environment.Exit(1);
    }

    private static void WarnAboutPermissions()
    {
        if (!IsRunningAsRoot())
        {
            Log("[PERMS] Not running as root on Unix");
            Console.WriteLine("⚠️ On Linux, run with: sudo dotnet FCPApp.dll");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }
        else
        {
            Log("[PERMS] Running as root ✓");
        }
    }

    private static bool IsRunningAsRoot()
    {
        try
        {
            return Environment.UserName == "root";
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region UI Helpers

    private static void MessageBoxShow(string title, string message)
    {
        try
        {
            var asm = System.Reflection.Assembly.Load("System.Windows.Forms");
            var mbType = asm?.GetType("System.Windows.Forms.MessageBox");
            if (mbType != null)
            {
                var showMethod = mbType.GetMethod("Show", new[] { typeof(string), typeof(string) });
                showMethod?.Invoke(null, new object[] { message, title });
                return;
            }
        }
        catch { }
        Console.WriteLine($"{title}: {message}");
    }

    #endregion
}