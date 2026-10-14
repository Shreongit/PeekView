using System;
using System.IO;
using System.Drawing;
using System.Windows.Media.Imaging;
using PdfiumViewer;
using WpfPixelFormat = System.Windows.Media.PixelFormats;

namespace PeekView.App.Services.Preview;

public sealed class PdfiumDocumentSession : IDisposable
{
    private readonly MemoryStream _documentStream;
    private readonly PdfDocument _document;
    private bool _disposed;

    public int PageCount => _document.PageCount;

    public PdfiumDocumentSession(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _documentStream = new MemoryStream();

        using (var source = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite))
        {
            source.CopyTo(_documentStream);
        }

        _documentStream.Position = 0;

        try
        {
            _document = PdfDocument.Load(_documentStream);
        }
        catch
        {
            _documentStream.Dispose();
            throw;
        }
    }

    public BitmapSource RenderPage(
        int pageIndex,
        int targetWidth,
        int targetHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (pageIndex < 0 || pageIndex >= _document.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetWidth),
                "The target dimensions must be positive.");
        }

        var pageSize = _document.PageSizes[pageIndex];

        double scale = Math.Min(
            targetWidth / pageSize.Width,
            targetHeight / pageSize.Height);

        int renderWidth = Math.Max(
            1,
            (int)Math.Round(pageSize.Width * scale));

        int renderHeight = Math.Max(
            1,
            (int)Math.Round(pageSize.Height * scale));

        using Image renderedImage = _document.Render(
            pageIndex,
            renderWidth,
            renderHeight,
            96,
            96,
            PdfRotation.Rotate0,
            PdfRenderFlags.Annotations |
            PdfRenderFlags.LimitImageCacheSize,
            true);

        using var bitmap = new Bitmap(renderedImage);

        IntPtr hBitmap = bitmap.GetHbitmap();

        try
        {
            var bitmapSource =
                System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze();
            return bitmapSource;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _document.Dispose();
        _documentStream.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}