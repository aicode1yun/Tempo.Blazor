using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportViewerPageTests : ReportServerWebTestBase
{
    // Folder-qualified path whose LAST segment is the report NAME — the real /resolve contract
    // (ResolveByPathAsync matches the report name within the folder, not the bare id).
    private const string ReportPath = "Finance/Sales Register";

    [Fact]
    public void ViewerPage_FavoriteToggle_AddsThenRemovesViaClient()
    {
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, ReportPath));

        var toggle = cut.Find("[data-testid='favorite-toggle']");
        toggle.GetAttribute("aria-pressed").Should().Be("false");

        toggle.Click();
        fake.AddedFavoriteReportIds.Should().Contain("sales-register");
        cut.Find("[data-testid='favorite-toggle']").GetAttribute("aria-pressed").Should().Be("true");

        cut.Find("[data-testid='favorite-toggle']").Click();
        fake.RemovedFavoriteReportIds.Should().Contain("sales-register");
        cut.Find("[data-testid='favorite-toggle']").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void ViewerPage_FavoriteToggle_ReflectsExistingFavoriteOnOpen()
    {
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        fake.SeedFavorite("sales-register");

        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, ReportPath));

        cut.Find("[data-testid='favorite-toggle']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void ViewerPage_ResolvesIdBasedDeepLinkPath_LikeFavoriteHref()
    {
        // The favorites/explorer deep link is folder-qualified with the report ID in the last segment
        // (e.g. /reports/Finance/sales-register). After the additive id-OR-name resolve fix, opening that
        // exact path resolves the report (round-trips), so a favorite click works end to end.
        SignIn();
        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, "Finance/sales-register"));

        cut.FindAll("[data-testid='report-not-found']").Should().BeEmpty();
        cut.Find("[data-testid='f12-viewer-page']").TextContent.Should().Contain("Sales Register");
        cut.Find("[data-testid='favorite-toggle']").Should().NotBeNull();
    }

    [Fact]
    public async Task ViewerPage_ShowsTheRealCreatedReport_NotADifferentDemoReport()
    {
        // F1 regression: the viewer must reflect the ACTUAL resolved report (name from the real report),
        // never a different demo report's content (e.g. "Sales Register"/"Europe Customer").
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        await fake.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "northwind",
            FolderId = "folder-finance",
            Name = "E2E Ledger",
            DefinitionJson = "{\"schemaVersion\":1,\"name\":\"E2E Ledger\"}",
        });

        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, "Finance/E2E Ledger"));

        cut.Find("[data-testid='f12-viewer-page']").TextContent.Should().Contain("E2E Ledger");
        cut.Find("[data-testid='f12-viewer-page']").TextContent.Should().NotContain("Sales Register");
        cut.FindAll("[data-testid='report-not-found']").Should().BeEmpty();
    }

    [Fact]
    public async Task ViewerPage_ShowsPreviewUnavailable_WhenNoUsableDefinitionForNonDemoReport()
    {
        // A real report with no usable in-process definition previews a clean "unavailable" state rather
        // than another report's content; the Run/API path stays available.
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        await fake.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "northwind",
            FolderId = "folder-finance",
            Name = "Empty Report",
            DefinitionJson = "{}",
        });

        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, "Finance/Empty Report"));

        cut.Find("[data-testid='viewer-preview-unavailable']").Should().NotBeNull();
        cut.Find("[data-testid='f12-viewer-page']").TextContent.Should().NotContain("Sales Register");
    }

    [Fact]
    public void ViewerPage_RendersGracefulNotFound_ForUnknownPath()
    {
        // F2b: a 404 from /resolve (KeyNotFoundException in the client) renders the graceful state.
        SignIn();

        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, "Finance/Missing Report"));

        cut.Find("[data-testid='report-not-found']").Should().NotBeNull();
    }

    [Fact]
    public void ViewerPage_ParameterForm_RendersDeclaredParameters()
    {
        SignIn();
        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, ReportPath));

        cut.Find("[data-testid='viewer-param-form']").Should().NotBeNull();
        cut.Find("[data-testid='param-input-region']").Should().NotBeNull();
        cut.Find("[data-testid='param-input-period']").Should().NotBeNull();
    }

    [Fact]
    public void ViewerPage_Run_SendsFormParameterValueAndSelectedFormatToRender()
    {
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();

        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, ReportPath));

        // Enter a parameter value in the page-local form (the single source of truth) then Run.
        cut.Find("[data-testid='param-input-region']").Change("West");
        cut.Find("[data-testid='run-format']").Change("Pdf");
        cut.Find("[data-testid='run-report']").Click();

        fake.RenderRequests.Should().ContainSingle();
        var request = fake.RenderRequests[0];
        request.ReportId.Should().Be("sales-register");
        request.Format.Should().Be(ReportRenderFormat.Pdf);
        request.Parameters.Should().Contain(parameter => parameter.Name == "region" && parameter.Values.Contains("West"));

        // The run persists a render-run history record surfaced by ListRenderRunsAsync (same call path
        // the real host uses); the viewer reflects the completed run.
        cut.Find("[data-testid='run-report-status']").Should().NotBeNull();
    }

    [Fact]
    public void ViewerPage_ParameterForm_SeedsInitialValuesFromDeepLinkQuery()
    {
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/reports/Finance/Sales%20Register?region=North");

        var cut = RenderComponent<ReportViewerPage>(parameters => parameters.Add(page => page.Path, ReportPath));

        // A shared deep link's parameter is honored: Run without touching the form still sends it.
        cut.Find("[data-testid='run-report']").Click();

        fake.RenderRequests.Should().ContainSingle();
        fake.RenderRequests[0].Parameters.Should().Contain(parameter => parameter.Name == "region" && parameter.Values.Contains("North"));
    }
}
