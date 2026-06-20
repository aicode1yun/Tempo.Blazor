namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Spell and proofing options supplied by the host application to the document editor runtime.</summary>
public sealed class DocumentProofingOptions
{
    /// <summary>Whether proofing diagnostics should run in the editor surface.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default language tag used when a block does not provide its own language metadata.</summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>Words that the host proofing provider currently reports as misspelled.</summary>
    public IReadOnlyList<string> FlaggedWords { get; set; } = [];

    /// <summary>Host-supplied spelling suggestions keyed by the original misspelled word.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Suggestions { get; set; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
}
