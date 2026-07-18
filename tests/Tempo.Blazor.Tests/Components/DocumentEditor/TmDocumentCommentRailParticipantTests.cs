using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Phase 8: per-participant comment colors and the client badge. The comment rail derives the
/// participant palette from its threads, renders a legend (color chip + name + CLIENT badge for
/// external participants) and colors each thread/entry by its author's palette index.
/// </summary>
public class TmDocumentCommentRailParticipantTests : LocalizationTestBase
{
    [Fact]
    public void Rail_RendersParticipantLegendWithClientBadge()
    {
        var cut = RenderComponent<TmDocumentCommentRail>(parameters =>
            parameters.Add(p => p.Comments, BuildComments())
                      .Add(p => p.CanComment, true));

        var legend = cut.Find("[data-testid='document-comment-legend']");
        var items = cut.FindAll("[data-testid='document-comment-legend-item']");
        items.Should().HaveCount(2);
        items[0].TextContent.Should().Contain("Anna Author");
        items[1].TextContent.Should().Contain("Klient Novák");
        items[1].QuerySelector("[data-testid='document-comment-client-badge']")!
            .TextContent.Trim().Should().Be("CLIENT");
        items[0].QuerySelector("[data-testid='document-comment-client-badge']").Should().BeNull();

        // Color chips carry the participant palette classes.
        items[0].QuerySelector(".tm-document-comment-legend__chip--participant-0").Should().NotBeNull();
        items[1].QuerySelector(".tm-document-comment-legend__chip--participant-1").Should().NotBeNull();
        legend.TextContent.Should().Contain("Participants");
    }

    [Fact]
    public void Threads_AreColoredByTheirAuthorsPaletteIndex()
    {
        var cut = RenderComponent<TmDocumentCommentRail>(parameters =>
            parameters.Add(p => p.Comments, BuildComments())
                      .Add(p => p.CanComment, true));

        var threads = cut.FindAll(".tm-document-comment-thread");
        threads.Should().HaveCount(2);
        threads[0].ClassList.Should().Contain("tm-document-comment-thread--participant-0");
        threads[1].ClassList.Should().Contain("tm-document-comment-thread--participant-1");
    }

    [Fact]
    public void ExternalEntry_ShowsClientBadgeInsteadOfPlainExternalLabel()
    {
        var cut = RenderComponent<TmDocumentCommentRail>(parameters =>
            parameters.Add(p => p.Comments, BuildComments())
                      .Add(p => p.CanComment, true));

        var externalEntry = cut.FindAll(".tm-document-comment-entry--external");
        externalEntry.Should().NotBeEmpty();
        externalEntry[0].QuerySelector(".tm-document-comment-entry__external")!
            .TextContent.Trim().Should().Be("CLIENT");
    }

    [Fact]
    public void Rail_WithoutComments_RendersNoLegend()
    {
        var cut = RenderComponent<TmDocumentCommentRail>(parameters =>
            parameters.Add(p => p.Comments, new List<DocumentComment>())
                      .Add(p => p.CanComment, true));

        cut.FindAll("[data-testid='document-comment-legend']").Should().BeEmpty();
    }

    private static List<DocumentComment> BuildComments() =>
    [
        new DocumentComment
        {
            Id = "thread-1",
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "anna", DisplayName = "Anna Author" },
                    Text = "Internal note"
                }
            ]
        },
        new DocumentComment
        {
            Id = "thread-2",
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "client-1", DisplayName = "Klient Novák" },
                    IsExternalAuthor = true,
                    Text = "Client feedback"
                }
            ]
        }
    ];
}
