using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportsPageTests : ReportServerWebTestBase
{
    [Fact]
    public void ReportsPage_RendersExplorerFromTypedClient_AndNavigatesToViewerDeepLink()
    {
        SignIn();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<ReportsPage>();

        cut.Find("[data-testid='f12-explorer-page']").TextContent.Should().Contain("Report explorer");

        // The folder tree is built from the flat folder DTOs returned by the typed client.
        cut.Find("[data-testid='tm-report-folder-/Finance']").Click();
        cut.Find("[data-testid='tm-report-explorer-grid']").TextContent.Should().Contain("Sales Register");

        cut.Find("[data-testid='tm-report-open-sales-register']").Click();

        navigation.Uri.Should().EndWith("/reports/Finance/sales-register");
    }

    [Fact]
    public void ReportsPage_CreateFolder_CallsTypedClient_AndShowsNewFolder()
    {
        SignIn();
        var cut = RenderComponent<ReportsPage>();

        cut.Find("[data-testid='tm-report-folder-/Finance']").Click();
        cut.Find("[data-testid='tm-report-new-folder-name']").Input("Month End");
        cut.Find("[data-testid='tm-report-folder-create'] button").Click();

        cut.Find("[data-testid='tm-report-folder-/Finance/Month End']").Should().NotBeNull();
    }
}
