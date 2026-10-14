
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MiniSoftware;

namespace PeekView.App.Services.Preview;

public sealed class OfficePdfConverter
{
    public Task<string> ConvertDocxToPdfAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        return ConvertToPdfAsync(filePath, cancellationToken);
    }

    public Task<string> ConvertExcelToPdfAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        return ConvertToPdfAsync(filePath, cancellationToken);
    }

    private static async Task<string> ConvertToPdfAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "The document was not found.",
                filePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "PeekView",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outputDirectory);

        var outputPdf = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(filePath) + ".pdf");

        try
        {
            await Task.Run(
                () => MiniPdf.ConvertToPdf(filePath, outputPdf),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(outputPdf) || new FileInfo(outputPdf).Length == 0)
            {
                throw new InvalidOperationException(
                    "The document converter did not produce a valid PDF.");
            }

            return outputPdf;
        }
        catch
        {
            try
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // Leave temporary files for later cleanup if they are locked.
            }
            catch (UnauthorizedAccessException)
            {
                // Leave temporary files for later cleanup if access is denied.
            }

            throw;
        }
    }
}
