using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Components.Files;

/// <summary>The outcome of confirming a selection in <see cref="TmDocumentOpenDialog"/>.</summary>
public sealed class DocumentOpenResult
{
    /// <summary>Identifier of the picked document.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Kind of the picked document.</summary>
    public required TempoDocumentKind Kind { get; init; }

    /// <summary>Whether to link to or copy the document.</summary>
    public required DocumentOpenMode Mode { get; init; }

    /// <summary>Display name of the picked document, for convenience.</summary>
    public string? Name { get; init; }
}
