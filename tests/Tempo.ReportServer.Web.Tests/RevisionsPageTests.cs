using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class RevisionsPageTests : ReportServerWebTestBase
{
    [Fact]
    public void RevisionsPage_ListsRevisionsFromTypedClient_WithCurrentMarker()
    {
        SignIn();
        var cut = RenderComponent<RevisionsPage>();

        var table = cut.Find("[data-testid='revisions-table']");
        table.TextContent.Should().Contain("Sales Register");
        table.TextContent.Should().Contain("Added IncludeClosed parameter.");

        // The report's latest revision (rev-sr-2) is current, so its rollback button is disabled.
        cut.Find("[data-testid='rollback-rev-sr-2']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='rollback-rev-sr-1']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void RevisionsPage_Rollback_CallsTypedClient_AndAddsNewLatestRevision()
    {
        SignIn();
        var cut = RenderComponent<RevisionsPage>();

        cut.Find("[data-testid='rollback-rev-sr-1']").Click();

        // The rollback creates a new latest revision with the default rollback note.
        cut.Find("[data-testid='revisions-table']").TextContent.Should().Contain("Rollback to revision 1");
    }
}
