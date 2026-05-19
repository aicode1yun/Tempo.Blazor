using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentReviewSummaryTests : LocalizationTestBase
{
    [Fact]
    public void Summary_RendersPendingChangesAndComments()
    {
        var cut = RenderComponent<TmDocumentReviewSummary>(parameters => parameters
            .Add(p => p.PendingRevisionCount, 3)
            .Add(p => p.OpenCommentCount, 2));

        cut.Find("[data-testid='document-review-summary']").TextContent.Should().Contain("3 pending changes, 2 open comments");
    }

    [Fact]
    public void Summary_InvokesPanelActions()
    {
        var openedComments = false;
        var openedRevisions = false;
        var cut = RenderComponent<TmDocumentReviewSummary>(parameters => parameters
            .Add(p => p.PendingRevisionCount, 1)
            .Add(p => p.OpenCommentCount, 1)
            .Add(p => p.OpenComments, () => openedComments = true)
            .Add(p => p.OpenRevisions, () => openedRevisions = true));

        cut.Find("[data-testid='document-review-summary-revisions']").Click();
        cut.Find("[data-testid='document-review-summary-comments']").Click();

        openedRevisions.Should().BeTrue();
        openedComments.Should().BeTrue();
    }
}
