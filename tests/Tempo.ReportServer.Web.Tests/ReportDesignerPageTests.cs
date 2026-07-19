using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportDesignerPageTests : ReportServerWebTestBase
{
    [Fact]
    public void DesignerPage_RendersDesignerForCatalogReport()
    {
        SignIn();

        var cut = RenderComponent<ReportDesignerPage>(parameters => parameters
            .Add(component => component.ReportId, "sales-register"));

        cut.Find("[data-testid='f13-designer-page']").TextContent.Should().Contain("Sales Register");
        cut.Find("[data-testid='tm-report-designer']").Should().NotBeNull();
        cut.Find("[data-testid='nav-designer']").ClassList.Should().Contain("rs-nav__item--active");
    }

    [Fact]
    public async Task DesignerPage_ShowsTheRealCreatedReport_NotADifferentDemoReport()
    {
        // F1 regression: the designer must render the ACTUAL report's definition (its name/bands from
        // _resolved.DefinitionJson), never fall back to a different demo report (e.g. "Sales Register").
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        var created = await fake.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "northwind",
            FolderId = "folder-finance",
            Name = "E2E Ledger",
            DefinitionJson = "{\"schemaVersion\":1,\"name\":\"E2E Ledger\"}",
        });

        var cut = RenderComponent<ReportDesignerPage>(parameters => parameters
            .Add(component => component.ReportId, created.ReportId));

        cut.Find("[data-testid='f13-designer-page']").TextContent.Should().Contain("E2E Ledger");
        cut.Find("[data-testid='f13-designer-page']").TextContent.Should().NotContain("Sales Register");
    }

    [Fact]
    public void DesignerPage_RendersGracefulNotFound_ForUnknownReport()
    {
        // F2b: an unknown report id resolves to a 404 (KeyNotFoundException in the client); the page shows
        // its graceful not-found state instead of an unhandled exception.
        SignIn();

        var cut = RenderComponent<ReportDesignerPage>(parameters => parameters
            .Add(component => component.ReportId, "no-such-report"));

        cut.Find("[data-testid='designer-not-found']").Should().NotBeNull();
    }
}
