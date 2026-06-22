namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>User mention metadata stored with a comment entry.</summary>
public sealed class TmCommentMention
{
    /// <summary>Mentioned user snapshot.</summary>
    public TmUserRef User { get; set; } = new();

    /// <summary>Display text captured for the mention.</summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>Optional start offset in the body.</summary>
    public int? StartOffset { get; set; }

    /// <summary>Optional mention length in the body.</summary>
    public int? Length { get; set; }

    /// <summary>Creates mention metadata from a user reference.</summary>
    /// <param name="user">Mentioned user.</param>
    public static TmCommentMention FromUser(TmUserRef user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new TmCommentMention
        {
            User = user,
            DisplayText = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Id : user.DisplayName
        };
    }
}
