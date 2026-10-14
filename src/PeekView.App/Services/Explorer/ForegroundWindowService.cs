using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PeekView.App.Services.Explorer;

public sealed class ForegroundWindowService
{
    public bool IsFileExplorerForeground()
    {
        IntPtr foregroundWindow = GetForegroundWindow();

        if (foregroundWindow == IntPtr.Zero)
            return false;

        _ = GetWindowThreadProcessId(
            foregroundWindow,
            out uint processId);

        if (processId == 0)
            return false;

        try
        {
            using Process process =
                Process.GetProcessById((int)processId);

            return string.Equals(
                process.ProcessName,
                "explorer",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);
}