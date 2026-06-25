using Tempo.Blazor.Reporting.Components;
using Tempo.Blazor.Reporting.Models;
using Tempo.Blazor.Reporting.Tests.Fixtures;

namespace Tempo.Blazor.Reporting.Tests.Components;

public sealed class TmReportExplorerTests : ReportingComponentTestBase
{
    [Fact]
    public void Explorer_RendersFolderTreeGridAndSearchesReports()
    {
        var cut = RenderComponent<TmReportExplorer>(parameters => parameters
            .Add(component => component.RootFolder, FolderTree())
            .Add(component => component.CurrentFolderPath, "/finance")
            .Add(component => component.Reports, Reports()));

        cut.Find("[data-testid='tm-report-explorer-grid']").TextContent.Should().Contain("Invoice Aging");
        cut.Find("[data-testid='tm-report-folder-/operations']").TextContent.Should().Contain("Operations");

        cut.Find("[data-testid='tm-report-explorer-search']").Input("sales");

        cut.Find("[data-testid='tm-report-explorer-grid']").TextContent.Should().Contain("Sales Register");
        cut.Find("[data-testid='tm-report-explorer-grid']").TextContent.Should().NotContain("Invoice Aging");
    }

    [Fact]
    public void Explorer_SwitchesToListViewAndRaisesOpenReport()
    {
        ReportExplorerReportItem? opened = null;
        var cut = RenderComponent<TmReportExplorer>(parameters => parameters
            .Add(component => component.RootFolder, FolderTree())
            .Add(component => component.CurrentFolderPath, "/finance")
            .Add(component => component.Reports, Reports())
            .Add(component => component.ReportOpened, item => opened = item));

        cut.Find("button[aria-label='List view']").Click();

        cut.Find("[data-testid='tm-report-explorer-list']").TextContent.Should().Contain("Sales Register");
        cut.Find("[data-testid='tm-report-open-sales-register']").Click();

        opened.Should().NotBeNull();
        opened!.Id.Should().Be("sales-register");
    }

    [Fact]
    public void Explorer_RaisesFolderCreateAndMoveActions()
    {
        ReportExplorerCreateFolderRequest? created = null;
        ReportExplorerMoveReportRequest? moved = null;
        var cut = RenderComponent<TmReportExplorer>(parameters => parameters
            .Add(component => component.RootFolder, FolderTree())
            .Add(component => component.CurrentFolderPath, "/finance")
            .Add(component => component.Reports, Reports())
            .Add(component => component.AllowFolderManagement, true)
            .Add(component => component.CreateFolderRequested, request => created = request)
            .Add(component => component.ReportMoveRequested, request => moved = request));

        cut.Find("[data-testid='tm-report-new-folder-name']").Input("Archive");
        cut.Find("button[aria-label='Create folder']").Click();

        created.Should().BeEquivalentTo(new ReportExplorerCreateFolderRequest("/finance", "Archive"));

        cut.Find("button[aria-label='Actions for Invoice Aging']").Click();
        cut.Find("[data-testid='tm-report-move-target-invoice-aging']").Change("/operations");
        cut.Find("[data-testid='tm-report-move-invoice-aging']").Click();

        moved.Should().BeEquivalentTo(new ReportExplorerMoveReportRequest("invoice-aging", "/operations"));
    }

    private static ReportExplorerFolder FolderTree()
        => new(
            "/",
            "Reports",
            [
                new ReportExplorerFolder("/finance", "Finance"),
                new ReportExplorerFolder("/operations", "Operations"),
            ]);

    private static IReadOnlyList<ReportExplorerReportItem> Reports()
        =>
        [
            new ReportExplorerReportItem(
                "invoice-aging",
                "Invoice Aging",
                "/reports/finance/invoice-aging",
                "/finance",
                "Open receivables by due date",
                "Finance",
                DateTimeOffset.Parse("2026-06-12T08:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
                7,
                "data:image/png;base64,iVBORw0KGgo=",
                ["Finance", "AR"]),
            new ReportExplorerReportItem(
                "sales-register",
                "Sales Register",
                "/reports/finance/sales-register",
                "/finance",
                "Sales orders and payment status",
                "Sales Ops",
                DateTimeOffset.Parse("2026-06-14T10:15:00Z", System.Globalization.CultureInfo.InvariantCulture),
                12,
                "data:image/png;base64,iVBORw0KGgo=",
                ["Sales"]),
        ];
}
