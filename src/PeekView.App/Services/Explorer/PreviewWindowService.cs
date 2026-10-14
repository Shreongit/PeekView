using System;
using System.Windows;

namespace PeekView.App.Services.Explorer;

public sealed class PreviewWindowService
{
    private Window? _previewWindow;

    public bool IsPreviewOpen =>
        _previewWindow is { IsVisible: true };

    public void Show(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        Close();

        _previewWindow = window;

        _previewWindow.Closed += (_, _) =>
        {
            _previewWindow = null;
        };

        _previewWindow.Show();
        _previewWindow.Activate();
    }

    public void Close()
    {
        if (_previewWindow is null)
            return;

        Window window = _previewWindow;
        _previewWindow = null;

        window.Close();
    }
}