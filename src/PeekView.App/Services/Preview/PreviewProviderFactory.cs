using System;
using System.Collections.Generic;
using System.Linq;

namespace PeekView.App.Services.Preview;

public sealed class PreviewProviderFactory
{
    private readonly IReadOnlyList<IPreviewProvider> _providers;

    public PreviewProviderFactory(
        IEnumerable<IPreviewProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToList();
    }

    public IPreviewProvider? GetProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return _providers.FirstOrDefault(
            provider => provider.CanPreview(filePath));
    }
}