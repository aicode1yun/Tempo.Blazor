namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Request handed to an <see cref="Services.ITempoProofingProvider"/> for a proofing pass.</summary>
public sealed class DocumentProofingCheckRequest
{
    /// <summary>Plain text of the document (or fragment) to proof.</summary>
    public required string Text { get; init; }

    /// <summary>Optional BCP-47 language tag; falls back to the provider's configured language.</summary>
    public string? Language { get; init; }

    /// <summary>Optional id of the source document, for providers that scope caches per document.</summary>
    public string? DocumentId { get; init; }
}

/// <summary>A single proofing finding reported by an <see cref="Services.ITempoProofingProvider"/>.</summary>
public sealed class DocumentProofingIssue
{
    /// <summary>The flagged word exactly as it appears in the checked text.</summary>
    public required string Word { get; init; }

    /// <summary>Zero-based offset of the finding inside the checked text.</summary>
    public int Offset { get; init; }

    /// <summary>Length of the flagged range inside the checked text.</summary>
    public int Length { get; init; }

    /// <summary>Optional human-readable description of the finding.</summary>
    public string? Message { get; init; }

    /// <summary>Optional provider rule id (e.g. a LanguageTool rule id).</summary>
    public string? RuleId { get; init; }

    /// <summary>Optional provider category id (e.g. TYPOS).</summary>
    public string? CategoryId { get; init; }

    /// <summary>Replacement suggestions ordered by provider confidence.</summary>
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}

/// <summary>Result of a proofing pass over a text snapshot.</summary>
public sealed class DocumentProofingCheckResult
{
    /// <summary>Shared empty result.</summary>
    public static DocumentProofingCheckResult Empty { get; } = new();

    /// <summary>Findings reported for the checked text, in document order.</summary>
    public IReadOnlyList<DocumentProofingIssue> Issues { get; init; } = [];
}
