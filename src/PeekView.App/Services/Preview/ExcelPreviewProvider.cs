using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExcelDataReader;
using PeekView.App.Models;

namespace PeekView.App.Services.Preview;

public sealed class ExcelPreviewProvider : IPreviewProvider
{
    private const int MaxRowsPerSheet = 100;
    private const int MaxColumnsPerSheet = 20;
    private const int MaxCharacters = 100_000;

    public bool CanPreview(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);
    }

    public Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var stream = File.Open(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = ExcelReaderFactory.CreateReader(stream);

            var output = new StringBuilder();
            var sheetIndex = 0;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                sheetIndex++;

                if (sheetIndex > 1)
                {
                    output.AppendLine();
                    output.AppendLine();
                }

                output.AppendLine($"=== {reader.Name} ===");
                output.AppendLine();

                var rowCount = 0;

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    rowCount++;

                    if (rowCount > MaxRowsPerSheet)
                    {
                        output.AppendLine("[Additional rows omitted]");
                        break;
                    }

                    var cellCount = Math.Min(
                        reader.FieldCount,
                        MaxColumnsPerSheet);

                    for (var column = 0; column < cellCount; column++)
                    {
                        if (column > 0)
                        {
                            output.Append(" | ");
                        }

                        var value = reader.GetValue(column);
                        output.Append(value?.ToString() ?? string.Empty);
                    }

                    if (reader.FieldCount > MaxColumnsPerSheet)
                    {
                        output.Append(" | ...");
                    }

                    output.AppendLine();

                    if (output.Length >= MaxCharacters)
                    {
                        output.AppendLine();
                        output.AppendLine("[Preview truncated]");
                        break;
                    }
                }

                if (output.Length >= MaxCharacters)
                {
                    break;
                }
            }
            while (reader.NextResult());

            var content = output.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                content = "This workbook contains no readable cell values.";
            }

            if (content.Length > MaxCharacters)
            {
                content = content[..MaxCharacters]
                    + Environment.NewLine
                    + "[Preview truncated]";
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                PreviewResult.TextSuccess(filePath, content));
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
            or DataException)
        {
            return Task.FromResult(
                PreviewResult.Failure(
                    $"Unable to preview this spreadsheet: {ex.Message}"));
        }
    }
}