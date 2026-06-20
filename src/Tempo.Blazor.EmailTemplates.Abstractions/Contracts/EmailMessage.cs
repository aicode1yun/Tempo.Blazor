namespace Tempo.Blazor.EmailTemplates.Abstractions.Contracts;

/// <summary>A fully rendered email ready to be delivered.</summary>
/// <param name="From">The sender address (when the host does not impose a fixed one).</param>
/// <param name="To">The primary recipients.</param>
/// <param name="Cc">The carbon-copy recipients.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Html">The HTML body.</param>
/// <param name="Text">The plain-text alternative body.</param>
public sealed record EmailMessage(
    string? From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Html,
    string Text);
