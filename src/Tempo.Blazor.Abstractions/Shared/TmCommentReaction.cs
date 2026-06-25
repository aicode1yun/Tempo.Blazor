namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Reaction metadata stored with a comment entry.</summary>
public sealed class TmCommentReaction
{
    /// <summary>Emoji or compact reaction value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>User ids that applied this reaction.</summary>
    public List<string> UserIds { get; set; } = [];

    /// <summary>Returns true when the reaction has a value.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
}
