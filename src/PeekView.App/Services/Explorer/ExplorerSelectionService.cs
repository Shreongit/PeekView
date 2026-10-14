using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PeekView.App.Services.Explorer;

public sealed class ExplorerSelectionService
{
    public string? LastDiagnostic { get; private set; }

    public string? GetSelectedFilePath()
    {
        object? shellObject = null;
        object? windowsObject = null;

        try
        {
            IntPtr foregroundHwnd = GetForegroundWindow();

            if (foregroundHwnd == IntPtr.Zero)
            {
                LastDiagnostic = "No foreground window was found.";
                return null;
            }

            Type? shellType = Type.GetTypeFromProgID("Shell.Application");

            if (shellType is null)
            {
                LastDiagnostic = "Shell.Application COM type is unavailable.";
                return null;
            }

            shellObject = Activator.CreateInstance(shellType);

            if (shellObject is null)
            {
                LastDiagnostic = "Could not create Shell.Application.";
                return null;
            }

            dynamic shell = shellObject;
            windowsObject = shell.Windows();

            if (windowsObject is null)
            {
                LastDiagnostic = "Explorer returned no window collection.";
                return null;
            }

            dynamic windows = windowsObject;
            int count = (int)windows.Count;
            string? matchingWindowError = null;

            for (int i = 0; i < count; i++)
            {
                object? windowObject = null;
                object? documentObject = null;
                object? itemsObject = null;
                object? itemObject = null;

                try
                {
                    windowObject = windows.Item(i);

                    if (windowObject is null)
                        continue;

                    dynamic window = windowObject;
                    int hwnd = Convert.ToInt32(window.HWND);

                    if (unchecked((IntPtr)hwnd) != foregroundHwnd)
                        continue;

                    string windowPath =
                        Convert.ToString(window.FullName) ?? string.Empty;

                    if (!windowPath.EndsWith(
                            "explorer.exe",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        LastDiagnostic =
                            "The foreground shell window is not File Explorer.";
                        return null;
                    }

                    documentObject = window.Document;

                    if (documentObject is null)
                    {
                        LastDiagnostic =
                            "The foreground Explorer window has no document.";
                        return null;
                    }

                    dynamic document = documentObject;
                    itemsObject = document.SelectedItems();

                    if (itemsObject is null)
                    {
                        LastDiagnostic =
                            "Explorer returned no selected-items collection.";
                        return null;
                    }

                    dynamic items = itemsObject;
                    int selectedCount = (int)items.Count;

                    if (selectedCount != 1)
                    {
                        LastDiagnostic =
                            $"Select exactly one file. Selected items: {selectedCount}.";
                        return null;
                    }

                    itemObject = items.Item(0);

                    if (itemObject is null)
                    {
                        LastDiagnostic =
                            "Explorer returned a null selected item.";
                        return null;
                    }

                    dynamic item = itemObject;
                    string path = Convert.ToString(item.Path) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        LastDiagnostic =
                            "Explorer returned an empty item path.";
                        return null;
                    }

                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        LastDiagnostic =
                            $"The selected path is neither a file nor a directory:\n{path}";
                        return null;
                    }

                    LastDiagnostic = "Selection retrieved successfully.";
                    return path;
                }
                catch (Exception ex)
                {
                    matchingWindowError = ex.Message;
                }
                finally
                {
                    ReleaseComObject(itemObject);
                    ReleaseComObject(itemsObject);
                    ReleaseComObject(documentObject);
                    ReleaseComObject(windowObject);
                }
            }

            LastDiagnostic = matchingWindowError is not null
                ? $"Explorer COM error: {matchingWindowError}"
                : $"No matching foreground Explorer window found among {count} shell windows.";

            return null;
        }
        catch (Exception ex)
        {
            LastDiagnostic = $"Selection lookup failed: {ex.Message}";
            return null;
        }
        finally
        {
            ReleaseComObject(windowsObject);
            ReleaseComObject(shellObject);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try
            {
                Marshal.ReleaseComObject(value);
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}