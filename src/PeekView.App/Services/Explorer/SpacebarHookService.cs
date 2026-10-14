
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace PeekView.App.Services.Explorer;

public sealed class SpacebarHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_SPACE = 0x20;

    private readonly LowLevelKeyboardProc _hookCallback;
    private IntPtr _hookHandle;
    private bool _disposed;

    public event EventHandler? SpacePressed;

    public SpacebarHookService()
    {
        _hookCallback = HookCallback;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookHandle != IntPtr.Zero)
            return;

        using Process process = Process.GetCurrentProcess();
        using ProcessModule? module = process.MainModule;

        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _hookCallback,
            module is null
                ? IntPtr.Zero
                : GetModuleHandle(module.ModuleName),
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Failed to install the keyboard hook.");
        }
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam == (IntPtr)WM_KEYDOWN ||
             wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int virtualKey = Marshal.ReadInt32(lParam);

            if (virtualKey == VK_SPACE)
            {
                SpacePressed?.Invoke(this, EventArgs.Empty);
            }
        }

        return CallNextHookEx(
            _hookHandle,
            nCode,
            wParam,
            lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
