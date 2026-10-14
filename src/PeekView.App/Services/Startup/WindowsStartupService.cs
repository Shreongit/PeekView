using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace PeekView.App.Services.Startup;

public sealed class WindowsStartupService
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string AppName = "PeekView";

    public bool IsEnabled()
    {
        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);

        string? value = key?.GetValue(AppName) as string;

        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key =
            Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (!enabled)
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            return;
        }

        string executablePath = ResolveExecutablePath();

        key.SetValue(
            AppName,
            $"\"{executablePath}\"",
            RegistryValueKind.String);
    }

    private static string ResolveExecutablePath()
    {
        // For a packaged or published app, this should be PeekView.App.exe.
        string? processPath = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(processPath) &&
            Path.GetFileNameWithoutExtension(processPath)
                .Equals("PeekView.App", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(processPath);
        }

        // During development, Environment.ProcessPath may point to dotnet.exe.
        // Prefer the application's compiled executable beside the assembly.
        string assemblyDirectory =
            AppContext.BaseDirectory;

        string publishedExecutable =
            Path.Combine(assemblyDirectory, "PeekView.App.exe");

        if (File.Exists(publishedExecutable))
            return Path.GetFullPath(publishedExecutable);

        throw new InvalidOperationException(
            "Could not locate PeekView.App.exe. Publish the application " +
            "before enabling automatic Windows startup.");
    }
}