namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Represents an emoji reaction on a comment entry.</summary>
public interface ICommentReaction
{
    /// <summary>The emoji character (e.g. "👍").</summary>
    string Emoji { get; }

    /// <summary>User IDs who added this reaction.</summary>
    IReadOnlyList<string> UserIds { get; }
}
