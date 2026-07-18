using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>
/// Asynchronous proofing (spell/grammar) provider consumed by <c>TmDocumentEditor</c>. The editor
/// extracts the document plain text, calls <see cref="CheckAsync"/>, and materializes the returned
/// issues into the word-list based <see cref="DocumentProofingOptions"/> that drive the canvas
/// squiggle overlay and the spelling context menu. A reference LanguageTool implementation ships in
/// the <c>Tempo.Blazor.Proofing.LanguageTool</c> package.
/// </summary>
public interface ITempoProofingProvider
{
    /// <summary>Runs a proofing pass over the supplied text snapshot.</summary>
    Task<DocumentProofingCheckResult> CheckAsync(
        DocumentProofingCheckRequest request,
        CancellationToken cancellationToken = default);
}
