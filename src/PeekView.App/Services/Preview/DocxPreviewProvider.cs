
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Docnet.Core;
using Docnet.Core.Models;
using PeekView.App.Models;

namespace PeekView.App.Services.Preview;

public sealed class DocxPreviewProvider : IPreviewProvider
{
    private readonly OfficePdfConverter _converter = new();

    public bool CanPreview(string filePath)
    {
        return string.Equals(
            Path.GetExtension(filePath),
            ".docx",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        string? convertedPdf = null;

        try
        {
            convertedPdf = await _converter.ConvertDocxToPdfAsync(
                filePath,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            using var document = DocLib.Instance.GetDocReader(
                convertedPdf,
                new PageDimensions(1600, 2000));

            if (document.GetPageCount() == 0)
            {
                return PreviewResult.Failure(
                    "The Word document contains no pages.");
            }

            using var page = document.GetPageReader(0);

            var bytes = page.GetImage();
            var width = page.GetPageWidth();
            var height = page.GetPageHeight();

            cancellationToken.ThrowIfCancellationRequested();

            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                width,
                height,
                96,
                96,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                bytes,
                width * 4);

            bitmap.Freeze();

            return PreviewResult.ImageSuccess(filePath, bitmap);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException)
        {
            return PreviewResult.Failure(
                $"Unable to render this Word document: {ex.Message}");
        }
        finally
        {
            if (convertedPdf is not null)
            {
                try
                {
                    var directory = Path.GetDirectoryName(convertedPdf);

                    if (directory is not null &&
                        Directory.Exists(directory))
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch (IOException)
                {
                    // Temporary PDF cleanup can be retried later.
                }
                catch (UnauthorizedAccessException)
                {
                    // Temporary PDF cleanup can be retried later.
                }
            }
        }
    }
}
