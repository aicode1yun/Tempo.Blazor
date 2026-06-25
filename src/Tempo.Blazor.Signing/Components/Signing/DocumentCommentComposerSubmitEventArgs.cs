using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Payload emitted by the document comment composer when text is submitted.</summary>
/// <param name="Body">Plain text body.</param>
/// <param name="Mentions">Mentions detected in the body.</param>
public sealed record DocumentCommentComposerSubmitEventArgs(
    string Body,
    IReadOnlyList<DocumentCommentMention> Mentions);
