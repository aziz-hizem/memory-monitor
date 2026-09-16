using Microsoft.Win32;

namespace MemoryMonitor;

/// <summary>
/// Registers/unregisters the app in the per-user Run key so it starts
/// with Windows. No installer, no scheduled task, no admin rights needed.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MemoryMonitor";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existing &&
               string.Equals(existing.Trim('"'), ExePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{ExePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string ExePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("Could not determine executable path.");
}
