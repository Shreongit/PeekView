using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PeekView.App.Models;

namespace PeekView.App.Services.Preview;

public sealed class PdfPreviewProvider : IPreviewProvider
{
    public bool CanPreview(string filePath)
    {
        return string.Equals(
            Path.GetExtension(filePath),
            ".pdf",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var session = new PdfiumDocumentSession(filePath);

            cancellationToken.ThrowIfCancellationRequested();

            if (session.PageCount <= 0)
            {
                return Task.FromResult(
                    PreviewResult.Failure("This PDF contains no pages."));
            }

            // Render only the first page for the initial preview.
            // The viewer will request subsequent pages as needed.
            var firstPage = session.RenderPage(
                pageIndex: 0,
                targetWidth: 1200,
                targetHeight: 1600);

            return Task.FromResult(
                PreviewResult.ImageSuccess(filePath, firstPage));
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
                or System.Runtime.InteropServices.ExternalException)
        {
            return Task.FromResult(
                PreviewResult.Failure(
                    $"Unable to render this PDF: {ex.Message}"));
        }
    }
}