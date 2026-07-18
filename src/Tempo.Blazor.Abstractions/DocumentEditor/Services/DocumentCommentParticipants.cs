using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>A distinct comment participant with a stable palette color assignment.</summary>
public sealed class DocumentCommentParticipant
{
    /// <summary>Stable author id.</summary>
    public required string AuthorId { get; init; }

    /// <summary>Displayed participant name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Whether any entry by this participant is flagged as external (client).</summary>
    public bool IsExternal { get; init; }

    /// <summary>Assigned palette color index (0 .. <see cref="DocumentCommentParticipants.PaletteSize"/> - 1).</summary>
    public int ColorIndex { get; init; }
}

/// <summary>
/// Derives the distinct participant list from comment threads for per-participant coloring and
/// the panel legend: participants appear in first-appearance order (threads, then entries) and get
/// a stable color index into the fixed CSS palette; a participant is external when any of their
/// entries is flagged <see cref="DocumentCommentEntry.IsExternalAuthor"/>.
/// </summary>
public static class DocumentCommentParticipants
{
    /// <summary>Number of distinct colors in the comment participant CSS palette.</summary>
    public const int PaletteSize = 8;

    /// <summary>Builds the participant list from comment threads in first-appearance order.</summary>
    public static IReadOnlyList<DocumentCommentParticipant> FromComments(IEnumerable<DocumentComment>? comments)
    {
        var order = new List<string>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var external = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in (comments ?? []).SelectMany(comment => comment.Entries))
        {
            var authorId = entry.Author?.Id;
            if (string.IsNullOrWhiteSpace(authorId))
            {
                continue;
            }

            if (!names.ContainsKey(authorId))
            {
                order.Add(authorId);
                names[authorId] = entry.Author!.DisplayName;
            }

            if (entry.IsExternalAuthor)
            {
                external.Add(authorId);
            }
        }

        return order
            .Select((authorId, index) => new DocumentCommentParticipant
            {
                AuthorId = authorId,
                DisplayName = names[authorId],
                IsExternal = external.Contains(authorId),
                ColorIndex = index % PaletteSize
            })
            .ToList();
    }

    /// <summary>Returns the assigned color index for an author, or 0 when unknown.</summary>
    public static int ColorIndexFor(IReadOnlyList<DocumentCommentParticipant>? participants, string? authorId)
    {
        if (participants is null || string.IsNullOrWhiteSpace(authorId))
        {
            return 0;
        }

        return participants.FirstOrDefault(participant =>
            string.Equals(participant.AuthorId, authorId, StringComparison.Ordinal))?.ColorIndex ?? 0;
    }
}
