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

    private static DocumentRevision CreateRevision(string id, DocumentRevisionAction action)
        => new()
        {
            Id = id,
            Type = DocumentRevisionType.Insertion,
            Action = action,
            Author = new DocumentRevisionAuthor { DisplayName = "Reviewer" },
            Range = new DocumentRevisionRange { BlockId = "block-1" },
            PayloadJson = "Inserted"
        };
}
