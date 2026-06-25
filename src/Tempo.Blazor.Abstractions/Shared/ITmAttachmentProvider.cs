namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Provider contract for linking file assets to Tempo entities.</summary>
public interface ITmAttachmentProvider : ITmCapabilityProvider<TmAttachmentProviderCapabilities>
{
    /// <summary>Operations this provider supports.</summary>
    new TmAttachmentProviderCapabilities Capabilities { get; }

    /// <summary>Gets attachments linked to an entity.</summary>
    /// <param name="entityRef">Entity to load attachments for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmAttachment>> GetForEntityAsync(
        TmEntityRef entityRef,
        CancellationToken cancellationToken = default);

    /// <summary>Adds or updates an attachment link.</summary>
    /// <param name="attachment">Attachment link to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmAttachment> AddAsync(
        TmAttachment attachment,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an attachment link from an entity.</summary>
    /// <param name="entityRef">Entity that owns the attachment link.</param>
    /// <param name="attachmentId">Attachment id to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(
        TmEntityRef entityRef,
        string attachmentId,
        CancellationToken cancellationToken = default);
}
