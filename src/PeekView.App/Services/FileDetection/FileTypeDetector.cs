using System;
using System.IO;
using System.Collections.Generic;

namespace PeekView.App.Services.FileDetection;

public enum PreviewFileType
{
    Unsupported,
    Image,
    Pdf,
    Text
}

public sealed class FileTypeDetector
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".gif",
            ".tif",
            ".tiff",
            ".webp"
        };

    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".md",
            ".markdown",
            ".json",
            ".xml",
            ".yaml",
            ".yml",
            ".csv",
            ".log",
            ".ini",
            ".toml",
            ".html",
            ".css",
            ".js",
            ".ts",
            ".jsx",
            ".tsx",
            ".py",
            ".cs",
            ".java",
            ".cpp",
            ".h",
            ".hpp",
            ".c",
            ".sql",
            ".ps1",
            ".sh",
            ".bat",
            ".cmd",
            ".gitignore",
            ".editorconfig"
        };

    public PreviewFileType Detect(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string extension = Path.GetExtension(filePath);

        if (ImageExtensions.Contains(extension))
            return PreviewFileType.Image;

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return PreviewFileType.Pdf;

        if (TextExtensions.Contains(extension))
            return PreviewFileType.Text;

        return PreviewFileType.Unsupported;
    }
}