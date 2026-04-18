using System.Diagnostics;
using Microsoft.Win32;

namespace Tabu.UI.Services;

/// <summary>
/// Manages the application's "launch at Windows startup" registration via the
/// per-user Run registry key. Writes only to HKCU (no admin rights required).
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Tabu";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath)) return;
                key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
            }
            else
            {
                if (key.GetValue(ValueName) is not null)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
        }
        catch
        {
            // Registry access may fail under restricted environments; swallow to keep UI responsive.
        }
    }

    private static string GetExecutablePath()
    {
        // Process.MainModule.FileName returns the actual .exe path even under
        // single-file or framework-dependent deployments.
        try
        {
            var path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(path)) return path;
        }
        catch
        {
            // ignored
        }
        return Environment.ProcessPath ?? string.Empty;
    }
}
