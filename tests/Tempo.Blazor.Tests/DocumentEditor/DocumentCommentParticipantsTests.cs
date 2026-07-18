using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 8: per-participant comment colors. DocumentCommentParticipants derives the distinct
/// participant list (first-appearance order) from comment threads, assigns each a stable palette
/// color index, and flags external (client) participants for the KLIENT badge and legend.
/// </summary>
public sealed class DocumentCommentParticipantsTests
{
    [Fact]
    public void FromComments_AssignsColorIndexesInFirstAppearanceOrder()
    {
        var comments = new List<DocumentComment>
        {
            Thread(("anna", "Anna", false), ("client", "Klient Novák", true)),
            Thread(("bob", "Bob", false), ("anna", "Anna", false))
        };

        var participants = DocumentCommentParticipants.FromComments(comments);

        participants.Should().HaveCount(3);
        participants[0].AuthorId.Should().Be("anna");
        participants[0].ColorIndex.Should().Be(0);
        participants[1].AuthorId.Should().Be("client");
        participants[1].ColorIndex.Should().Be(1);
        participants[1].IsExternal.Should().BeTrue();
        participants[2].AuthorId.Should().Be("bob");
        participants[2].ColorIndex.Should().Be(2);
    }

    [Fact]
    public void FromComments_WrapsColorIndexesAroundThePalette()
    {
        var comments = Enumerable.Range(0, DocumentCommentParticipants.PaletteSize + 2)
            .Select(index => Thread(($"author-{index}", $"Author {index}", false)))
            .ToList();

        var participants = DocumentCommentParticipants.FromComments(comments);

        participants[DocumentCommentParticipants.PaletteSize].ColorIndex.Should().Be(0);
        participants[DocumentCommentParticipants.PaletteSize + 1].ColorIndex.Should().Be(1);
    }

    [Fact]
    public void FromComments_ParticipantIsExternalWhenAnyEntryIsExternal()
    {
        var comments = new List<DocumentComment>
        {
            Thread(("mixed", "Mixed", false)),
            Thread(("mixed", "Mixed", true))
        };

        var participants = DocumentCommentParticipants.FromComments(comments);

        participants.Should().ContainSingle().Which.IsExternal.Should().BeTrue();
    }

    [Fact]
    public void ColorIndexFor_ReturnsAssignedIndexAndZeroForUnknownAuthors()
    {
        var participants = DocumentCommentParticipants.FromComments(
        [
            Thread(("anna", "Anna", false), ("client", "Klient", true))
        ]);

        DocumentCommentParticipants.ColorIndexFor(participants, "client").Should().Be(1);
        DocumentCommentParticipants.ColorIndexFor(participants, "unknown").Should().Be(0);
        DocumentCommentParticipants.ColorIndexFor(participants, null).Should().Be(0);
    }

    [Fact]
    public void FromComments_SkipsEntriesWithoutAuthorId()
    {
        var comments = new List<DocumentComment>
        {
            Thread(("", "Anonymous", false), ("anna", "Anna", false))
        };

        var participants = DocumentCommentParticipants.FromComments(comments);

        participants.Should().ContainSingle().Which.AuthorId.Should().Be("anna");
    }

    private static DocumentComment Thread(params (string Id, string Name, bool External)[] authors)
        => new()
        {
            Entries = authors.Select(author => new DocumentCommentEntry
            {
                Author = new DocumentEditorAuthor { Id = author.Id, DisplayName = author.Name },
                IsExternalAuthor = author.External,
                Text = "entry"
            }).ToList()
        };
}
