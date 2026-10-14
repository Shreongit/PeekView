using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PeekView.App.Services.Explorer;

public sealed class QuickLookCoordinator : IDisposable
{
    private readonly SpacebarHookService _keyboardHook;
    private readonly ExplorerSelectionService _selectionService;
    private readonly ForegroundWindowService _foregroundService;
    private readonly MainWindow _previewWindow;

    private int _isProcessing;
    private bool _disposed;

    public QuickLookCoordinator(MainWindow previewWindow)
    {
        ArgumentNullException.ThrowIfNull(previewWindow);

        _previewWindow = previewWindow;
        _keyboardHook = new SpacebarHookService();
        _selectionService = new ExplorerSelectionService();
        _foregroundService = new ForegroundWindowService();

        _keyboardHook.SpacePressed += OnSpacePressed;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _keyboardHook.Start();
    }

    private void OnSpacePressed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _isProcessing, 1) != 0)
            return;

        try
        {
            Application.Current.Dispatcher.BeginInvoke(
                new Action(() => _ = HandleSpacePressedAsync()));
        }
        catch
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    private async Task HandleSpacePressedAsync()
    {
        try
        {
            if (!_foregroundService.IsFileExplorerForeground())
                return;

            string? selectedPath = _selectionService.GetSelectedFilePath();

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                MessageBox.Show(
                    _selectionService.LastDiagnostic ??
                    "No selected file path was returned.",
                    "PeekView — Selection Diagnostic",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!File.Exists(selectedPath))
            {
                MessageBox.Show(
                    $"The selection service returned a path, but the file doesn't exist:\n\n" +
                    selectedPath,
                    "PeekView — Path Diagnostic",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!_previewWindow.IsVisible)
                _previewWindow.Show();

            _previewWindow.WindowState = WindowState.Normal;
            _previewWindow.Activate();

            await _previewWindow.LoadPreviewAsync(selectedPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Quick Look failed:\n{ex}",
                "PeekView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _keyboardHook.SpacePressed -= OnSpacePressed;
        _keyboardHook.Dispose();
        _disposed = true;
    }
}