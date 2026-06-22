namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Result returned after committing provider-managed draft assets.</summary>
public sealed class TmFileCommitResult
{
    /// <summary>Whether the commit operation succeeded.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Asset ids that were committed.</summary>
    public IReadOnlyList<string> AssetIds { get; set; } = [];

    /// <summary>Error message when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }
}
