using PeekView.App.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeekView.App.Services.Preview;

public sealed class FolderPreviewProvider : IPreviewProvider
{
    public bool CanPreview(string filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath)
            && Directory.Exists(filePath);
    }

    public async Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = new DirectoryInfo(filePath);

            if (!directory.Exists)
            {
                return PreviewResult.Failure(
                    "The folder does not exist.");
            }

            DateTime lastModified = directory.LastWriteTime;
            int itemCount = 0;
            long totalSize = 0;

            // Count immediate children for the folder summary.
            foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                itemCount++;
            }

            // Calculate the total size recursively without loading file contents.
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(directory.FullName);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string currentPath = pendingDirectories.Pop();

                IEnumerable<string> files;

                try
                {
                    files = Directory.EnumerateFiles(currentPath);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        totalSize = checked(
                            totalSize + new FileInfo(file).Length);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip files that cannot be inspected.
                    }
                    catch (IOException)
                    {
                        // Skip files that disappear or become unavailable.
                    }
                    catch (OverflowException)
                    {
                        return PreviewResult.Failure(
                            "The folder is too large to calculate its total size.");
                    }
                }

                IEnumerable<string> subdirectories;

                try
                {
                    subdirectories = Directory.EnumerateDirectories(currentPath);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (string subdirectory in subdirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        FileAttributes attributes =
                            File.GetAttributes(subdirectory);

                        // Avoid following directory junctions and symbolic links.
                        if ((attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            pendingDirectories.Push(subdirectory);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directories.
                    }
                    catch (IOException)
                    {
                        // Skip directories that are no longer available.
                    }
                }
            }

            return PreviewResult.FolderSuccess(
                directory.FullName,
                totalSize,
                itemCount,
                lastModified);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return PreviewResult.Failure(
                "Access to this folder was denied.");
        }
        catch (IOException ex)
        {
            return PreviewResult.Failure(
                $"Unable to inspect folder: {ex.Message}");
        }
        catch (Exception ex)
        {
            return PreviewResult.Failure(
                $"Unable to preview folder: {ex.Message}");
        }
    }
}