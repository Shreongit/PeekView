using PeekView.App.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PeekView.App.Services.Preview;

public sealed class ImagePreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp",
            ".gif", ".tif", ".tiff", ".webp"
        };

    // Limit decoded width for typical desktop preview usage.
    private const int MaxDecodeWidth = 1920;

    public bool CanPreview(string filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath)
            && SupportedExtensions.Contains(
                Path.GetExtension(filePath));
    }

    public async Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
            {
                return PreviewResult.Failure(
                    "The image file does not exist.");
            }

            const long maxImageBytes = 50 * 1024 * 1024;

            if (fileInfo.Length > maxImageBytes)
            {
                return PreviewResult.Failure(
                    "The image exceeds the 50 MB preview limit.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            BitmapImage bitmap;

            // Open the file directly; avoid a second full-file byte array.
            await using (var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true))
            {
                bitmap = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var decoded = new BitmapImage();
                    decoded.BeginInit();
                    decoded.CacheOption = BitmapCacheOption.OnLoad;
                    decoded.DecodePixelWidth = MaxDecodeWidth;
                    decoded.StreamSource = stream;
                    decoded.EndInit();
                    decoded.Freeze();

                    cancellationToken.ThrowIfCancellationRequested();

                    return decoded;
                }, cancellationToken);
            }

            return PreviewResult.ImageSuccess(filePath, bitmap);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PreviewResult.Failure(
                $"Unable to preview image: {ex.Message}");
        }
    }
}