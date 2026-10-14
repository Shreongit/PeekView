using System;
using System.IO;

namespace PeekView.App.Models;

public sealed class FileItem
{
    public string FullPath { get; }
    public string FileName => Path.GetFileName(FullPath);
    public string Extension => Path.GetExtension(FullPath);
    public long SizeInBytes { get; }
    public DateTime LastModified { get; }

    public FileItem(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        FullPath = Path.GetFullPath(fullPath);

        var fileInfo = new FileInfo(FullPath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "The selected file could not be found.",
                FullPath);
        }

        SizeInBytes = fileInfo.Length;
        LastModified = fileInfo.LastWriteTime;
    }
}