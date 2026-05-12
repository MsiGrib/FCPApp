using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FCPApp;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin(args);
                return;
            }
        }
        else WarnAboutPermissions();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    #region Admin helpers

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

            return (bool?)isInRole?.Invoke(principal, new[] { adminValue }) == true;
        }
        catch
        {
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

            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var exeWithoutDll = exePath.Replace(".dll", ".exe");
                if (File.Exists(exeWithoutDll)) exePath = exeWithoutDll;
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

                Process.Start(startInfo);
                Console.WriteLine("✅ Administrator rights requested (Windows)...");

                return;
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Console.WriteLine("⚠️ Launch cancelled by user");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error requesting rights: {ex.Message}");
        }

        Console.WriteLine("⚠️ Failed to obtain administrator rights.");
        Console.WriteLine("Try: Right-click the shortcut → Run as administrator");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        Environment.Exit(1);
    }

    private static void WarnAboutPermissions()
    {
        if (!IsRunningAsRoot())
        {
            Console.WriteLine("⚠️ On Linux, deleting system folders may require root privileges.");
            Console.WriteLine("   If you get 'No rights' errors, run: sudo dotnet FCPApp.dll");
            Console.WriteLine("   Press Enter to continue without permissions...");
            Console.ReadLine();
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
}