using PeekView.App.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PeekView.App.Services.Preview;

public sealed class PreviewService
{
    private readonly PreviewProviderFactory _factory;

    public PreviewService(PreviewProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public async Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PreviewResult.Failure(
                "No file path was provided.");
        }

        var provider = _factory.GetProvider(filePath);

        if (provider is null)
        {
            return PreviewResult.Failure(
                "This file type is not supported yet.");
        }

        return await provider.PreviewAsync(
            filePath,
            cancellationToken);
    }
}