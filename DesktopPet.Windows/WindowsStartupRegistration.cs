using System.IO;
using System.Reflection;
using System.Security;
using DesktopPet.Core;
using Microsoft.Win32;

namespace DesktopPet.Windows;

internal sealed class WindowsStartupRegistration : IWindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HoodDesktopPet";

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                using var existingKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
                existingKey?.Flush();
                if (existingKey?.GetValue(ValueName) is not null)
                {
                    throw new IOException("The current-user startup registry value could not be removed.");
                }

                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new IOException("The current-user startup registry key could not be opened.");
            var command = GetStartupCommand();
            key.SetValue(ValueName, command, RegistryValueKind.String);
            key.Flush();
            if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.Ordinal))
            {
                throw new IOException("The current-user startup registry value could not be verified.");
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new IOException(
                $"Windows startup registration could not be {(enabled ? "enabled" : "disabled")}: {exception.Message}",
                exception);
        }
    }

    private static string GetStartupCommand()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new IOException("The running Hood executable path could not be determined.");
        }

        if (!string.Equals(Path.GetFileNameWithoutExtension(executablePath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{executablePath}\"";
        }

        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new IOException("The Hood application path could not be determined for Windows startup.");
        }

        return $"\"{executablePath}\" \"{assemblyPath}\"";
    }
}
