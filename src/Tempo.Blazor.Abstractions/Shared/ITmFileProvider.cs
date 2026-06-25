namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Provider contract for blob or asset storage independent of entity attachment links.</summary>
public interface ITmFileProvider : ITmCapabilityProvider<TmFileProviderCapabilities>
{
    /// <summary>Operations this provider supports.</summary>
    new TmFileProviderCapabilities Capabilities { get; }

    /// <summary>Uploads a complete file stream.</summary>
    /// <param name="request">Upload metadata.</param>
    /// <param name="content">Readable file stream.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmFileUploadResult> UploadAsync(
        TmFileUploadRequest request,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves an asset id to an access URL or ticket.</summary>
    /// <param name="request">Resolve metadata.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmFileResolveResult> ResolveAsync(
        TmFileResolveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a provider-managed asset.</summary>
    /// <param name="assetId">Asset id to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task DeleteAsync(
        string assetId,
        CancellationToken cancellationToken = default);
}
