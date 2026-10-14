using PeekView.App.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PeekView.App.Services.Preview;

public interface IPreviewProvider
{
    bool CanPreview(string filePath);

    Task<PreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}