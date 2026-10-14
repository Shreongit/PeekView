using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace PeekView.App.Models;

public sealed class PreviewResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }
    public string? FilePath { get; }
    public BitmapSource? Image { get; }
    public IReadOnlyList<BitmapSource> Pages { get; }
    public string? TextContent { get; }

    public bool IsDirectory { get; }
    public long DirectorySizeBytes { get; }
    public int DirectoryItemCount { get; }
    public DateTime? DirectoryLastModified { get; }

    private PreviewResult(
        bool isSuccess,
        string? filePath,
        string? message,
        BitmapSource? image,
        string? textContent,
        IReadOnlyList<BitmapSource>? pages = null,
        bool isDirectory = false,
        long directorySizeBytes = 0,
        int directoryItemCount = 0,
        DateTime? directoryLastModified = null)
    {
        IsSuccess = isSuccess;
        FilePath = filePath;
        Message = message;
        Image = image;
        TextContent = textContent;
        Pages = pages ?? Array.Empty<BitmapSource>();
        IsDirectory = isDirectory;
        DirectorySizeBytes = directorySizeBytes;
        DirectoryItemCount = directoryItemCount;
        DirectoryLastModified = directoryLastModified;
    }

    public static PreviewResult ImageSuccess(
        string filePath,
        BitmapSource image)
    {
        return new PreviewResult(
            true,
            filePath,
            null,
            image,
            null);
    }

    public static PreviewResult PdfSuccess(
        string filePath,
        IReadOnlyList<BitmapSource> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        if (pages.Count == 0)
        {
            return Failure("This PDF contains no pages.");
        }

        return new PreviewResult(
            true,
            filePath,
            null,
            pages[0],
            null,
            pages: pages);
    }

    public static PreviewResult TextSuccess(
        string filePath,
        string content)
    {
        return new PreviewResult(
            true,
            filePath,
            null,
            null,
            content);
    }

    public static PreviewResult FolderSuccess(
        string directoryPath,
        long sizeBytes,
        int itemCount,
        DateTime lastModified)
    {
        return new PreviewResult(
            true,
            directoryPath,
            null,
            null,
            null,
            isDirectory: true,
            directorySizeBytes: sizeBytes,
            directoryItemCount: itemCount,
            directoryLastModified: lastModified);
    }

    public static PreviewResult Success(string filePath)
    {
        return new PreviewResult(
            true,
            filePath,
            null,
            null,
            null);
    }

    public static PreviewResult Failure(string message)
    {
        return new PreviewResult(
            false,
            null,
            message,
            null,
            null);
    }
}