
using PeekView.App.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeekView.App.Services.Preview;

public sealed class TextPreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".markdown", ".json", ".xml", ".xaml" ,".axaml", ".resx",
            ".yaml", ".yml", ".csv", ".log", ".ini", ".toml",
            ".html", ".css", ".js", ".ts", ".jsx", ".tsx",
            ".py", ".cs", ".java", ".cpp", ".h", ".hpp",
            ".c", ".sql", ".ps1", ".sh", ".bat", ".cmd",
            ".gitignore", ".editorconfig"
        };

    private const int MaxPreviewBytes = 1_000_000;

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
                    "The text file does not exist.");
            }

            if (fileInfo.Length > MaxPreviewBytes)
            {
                return PreviewResult.Failure(
                    "The file is too large to preview (maximum 1 MB).");
            }

            string content = await File.ReadAllTextAsync(
                filePath,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return PreviewResult.TextSuccess(filePath, content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException ||
            ex is System.Security.SecurityException)
        {
            return PreviewResult.Failure(
                $"Unable to read text file: {ex.Message}");
        }
    }
}
