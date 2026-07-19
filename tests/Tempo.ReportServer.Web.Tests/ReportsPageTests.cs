using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportsPageTests : ReportServerWebTestBase
{
    [Fact]
    public void ReportsPage_RendersExplorerAndNavigatesToViewerDeepLink()
    {
        SignIn();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<ReportsPage>();

        cut.Find("[data-testid='f12-explorer-page']").TextContent.Should().Contain("Report explorer");
        cut.Find("[data-testid='tm-report-explorer-grid']").TextContent.Should().Contain("Sales Register");

        cut.Find("[data-testid='tm-report-open-sales-register']").Click();

        navigation.Uri.Should().Contain("/reports/finance/sales-register?Region=EU");
    }
}
