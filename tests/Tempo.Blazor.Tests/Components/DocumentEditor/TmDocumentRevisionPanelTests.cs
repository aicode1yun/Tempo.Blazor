using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentRevisionPanelTests : LocalizationTestBase
{
    [Fact]
    public void Panel_RendersOnlyPendingRevisionCount()
    {
        var revisions = new[]
        {
            CreateRevision("revision-1", DocumentRevisionAction.Pending),
            CreateRevision("revision-2", DocumentRevisionAction.Accepted),
            CreateRevision("revision-3", DocumentRevisionAction.Pending)
        };

        var cut = RenderComponent<TmDocumentRevisionPanel>(parameters => parameters
            .Add(p => p.Revisions, revisions)
            .Add(p => p.CanReview, true));

        cut.FindAll("[data-testid='document-revision-item']").Should().HaveCount(2);
    }

    [Fact]
    public void Panel_DisablesAcceptRejectButtonsWhileReviewActionIsBusy()
    {
        var cut = RenderComponent<TmDocumentRevisionPanel>(parameters => parameters
            .Add(p => p.Revisions, new[] { CreateRevision("revision-1", DocumentRevisionAction.Pending) })
            .Add(p => p.CanReview, true)
            .Add(p => p.IsBusy, true));

        cut.Find("[data-testid='document-revision-accept']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-revision-reject']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Panel_FiltersPendingRevisionsByAuthorAndType()
    {
        var cut = RenderComponent<TmDocumentRevisionPanel>(parameters => parameters
            .Add(p => p.Revisions, new[]
            {
                CreateRevision("revision-1", DocumentRevisionAction.Pending, "author-1", DocumentRevisionType.Insertion),
                CreateRevision("revision-2", DocumentRevisionAction.Pending, "author-2", DocumentRevisionType.Deletion),
                CreateRevision("revision-3", DocumentRevisionAction.Pending, "author-1", DocumentRevisionType.Deletion)
            })
            .Add(p => p.CanReview, true));

        cut.Find("[data-testid='document-revision-filter-author']").Change("author-1");
        cut.Find("[data-testid='document-revision-filter-type']").Change(DocumentRevisionType.Deletion.ToString());

        cut.FindAll("[data-testid='document-revision-item']").Should().HaveCount(1);
        cut.Markup.Should().Contain("revision-3");
    }

    [Fact]
    public void Panel_RaisesAcceptAllWithCurrentFilter()
    {
        DocumentRevisionFilter? filter = null;
        var cut = RenderComponent<TmDocumentRevisionPanel>(parameters => parameters
            .Add(p => p.Revisions, new[]
            {
                CreateRevision("revision-1", DocumentRevisionAction.Pending, "author-1", DocumentRevisionType.Insertion),
                CreateRevision("revision-2", DocumentRevisionAction.Pending, "author-2", DocumentRevisionType.Deletion)
            })
            .Add(p => p.CanReview, true)
            .Add(p => p.OnAcceptAllRevisions, value => filter = value));

        cut.Find("[data-testid='document-revision-filter-author']").Change("author-2");
        cut.Find("[data-testid='document-revision-accept-all']").Click();

        filter.Should().NotBeNull();
        filter!.AuthorId.Should().Be("author-2");
    }

    private static DocumentRevision CreateRevision(
        string id,
        DocumentRevisionAction action,
        string authorId = "reviewer-1",
        DocumentRevisionType type = DocumentRevisionType.Insertion)
        => new()
        {
            Id = id,
            Type = type,
            Action = action,
            Author = new DocumentRevisionAuthor { Id = authorId, DisplayName = authorId },
            Range = new DocumentRevisionRange { BlockId = "block-1" },
            PayloadJson = "Inserted"
        };
}
