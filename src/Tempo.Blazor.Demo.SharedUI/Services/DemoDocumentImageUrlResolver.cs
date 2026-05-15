using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>Demo resolver for provider-managed document editor images.</summary>
public class DemoDocumentImageUrlResolver : IDocumentImageUrlResolver, IDocumentImageProvider
{
    /// <summary>Sample asset id used by the document editor demo.</summary>
    public const string ContractAssetId = "contract-evidence-asset";

    /// <summary>Sample exhibit asset id used by the document editor demo.</summary>
    public const string ExhibitAssetId = "exhibit-provider-asset";

    private const string DemoPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAUAAAAC0CAYAAADl5PURAAACsUlEQVR42u3ZzQ2CQBCAUYuwFLuxDjuwBDqgAK9UZfwB9ECC04JGdJd5hzeZ80C+y252+9MMkNHGEQABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEBAAB0CEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAgAIDOD6nGSAjAQQEEEAAAQQQQAABBBBAAAEEEKDiAA4xADISQEAAAQQQQAABBBBAAAEEEEAAAQQQQAB/pWk74MsEUABBAAVQAEEABVAAQQAFUABBAAUQYBUBfMQCkJAAAgIIIIAAAggggAACCCCAAAIIUHEA+xgAGQkgIIAAAliJ8zgDhRNAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBHIE8D5Oc438XFC+0jsigIAACiAggAIICKAAAgIogIAACiAggAIICKAAAgIogIAACiAggAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKICAAAogIIACCAjgGwG8xViT7fGwiLXdCag4gEIHpAig2AECKHSAAIodkDiAPh6Q9hEEQAABPg7gEAtAQgIICCCAAAIIIIAAAggggAACCCCAAAIIIIAAAgjgMpq2g2KIiQAKIAKIAAogAogACiACiAAKIAJIgQG8xgDISAABAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQIC/BrCPBSAhAQQEEEAAAQQQQAABBBBAAAEEEKDiAF5iAGQkgIAAAgggQBIvAt6vRwtbqO0AAAAASUVORK5CYII=";
    private readonly Dictionary<string, string> _assets = new(StringComparer.OrdinalIgnoreCase)
    {
        [ContractAssetId] = DemoPng,
        [ExhibitAssetId] = DemoPng
    };

    /// <inheritdoc />
    public Task<string> ResolveUrlAsync(
        string documentId,
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_assets.TryGetValue(assetId, out var url) ? url : string.Empty);
    }

    /// <inheritdoc />
    public async Task<DocumentImageUploadResult> UploadAsync(
        DocumentImageUploadRequest request,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var assetId = string.IsNullOrWhiteSpace(request.LocalAssetId)
            ? Guid.NewGuid().ToString("N")
            : request.LocalAssetId;
        var dataUrl = $"data:{request.ContentType};base64,{Convert.ToBase64String(memory.ToArray())}";
        _assets[assetId] = dataUrl;
        return new DocumentImageUploadResult
        {
            Success = true,
            AssetId = assetId,
            Url = dataUrl
        };
    }

    /// <inheritdoc />
    public Task<DocumentImageResolveResult> ResolveAsync(
        DocumentImageResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_assets.TryGetValue(request.AssetId, out var url)
            ? new DocumentImageResolveResult { Success = true, Url = url, ContentType = "image/png" }
            : new DocumentImageResolveResult { Success = false, ErrorMessage = "Image asset was not found." });
    }

    /// <inheritdoc />
    public Task DeleteDraftAssetAsync(
        string documentId,
        string assetId,
        CancellationToken cancellationToken = default)
    {
        _assets.Remove(assetId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DocumentImageCommitResult> CommitAssetsAsync(
        string documentId,
        IReadOnlyList<string> assetIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DocumentImageCommitResult
        {
            Success = true,
            AssetIds = [.. assetIds]
        });
    }

    /// <inheritdoc />
    public Task<DocumentImageResolveResult> RefreshUrlAsync(
        DocumentImageResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        return ResolveAsync(request, cancellationToken);
    }
}
