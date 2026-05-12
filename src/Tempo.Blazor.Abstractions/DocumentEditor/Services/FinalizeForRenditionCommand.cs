using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Finalizes a saved document version into an immutable rendition.</summary>
public class FinalizeForRenditionCommand
{
    private readonly IDocumentRenditionProvider _renditionProvider;

    /// <summary>Creates the command.</summary>
    public FinalizeForRenditionCommand(IDocumentRenditionProvider renditionProvider)
    {
        _renditionProvider = renditionProvider;
    }

    /// <summary>Creates an immutable rendition from a saved document version.</summary>
    public Task<DocumentRenditionResult> ExecuteAsync(
        DocumentRenditionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DocumentVersionId))
        {
            return Task.FromResult(new DocumentRenditionResult
            {
                Success = false,
                ErrorMessage = "A rendition can only be finalized from a saved document version."
            });
        }

        return _renditionProvider.CreateRenditionAsync(request, cancellationToken);
    }
}

/// <summary>Validates whether a signing rendition still matches a source document version.</summary>
public static class DocumentRenditionCompatibility
{
    /// <summary>Returns true when a new rendition is required for the supplied source version.</summary>
    public static bool RequiresNewRendition(DocumentRendition? rendition, DocumentVersion? sourceVersion)
    {
        if (rendition is null || sourceVersion is null)
        {
            return true;
        }

        return !string.Equals(rendition.DocumentVersionId, sourceVersion.Id, StringComparison.Ordinal)
            || !string.Equals(rendition.Hash.SourceSnapshotHash, sourceVersion.Snapshot.Hash, StringComparison.Ordinal);
    }
}
