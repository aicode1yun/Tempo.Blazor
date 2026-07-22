using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class FavoritesPageTests : ReportServerWebTestBase
{
    [Fact]
    public void FavoritesPage_ShowsEmptyState_WhenNoFavorites()
    {
        SignIn();
        var cut = Render<FavoritesPage>();

        cut.Find("[data-testid='favorites-empty']").Should().NotBeNull();
        cut.FindAll("[data-testid='favorite-item']").Should().BeEmpty();
    }

    [Fact]
    public void FavoritesPage_RendersFavoritesFromClient_WithReportLinks()
    {
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        fake.SeedFavorite("sales-register");

        var cut = Render<FavoritesPage>();

        var items = cut.FindAll("[data-testid='favorite-item']");
        items.Should().ContainSingle();
        cut.Find("[data-testid='favorites-list']").TextContent.Should().Contain("Sales Register");

        // Favorite link is folder-qualified and identical to the explorer's deep link for that report
        // (ReportServerCatalogMapper.ToReportItem → BuildDeepLink): /reports/{folderSegment}/{reportId}.
        items[0].GetAttribute("href").Should().Be("/reports/Finance/sales-register");

        // The subtitle shows the folder PATH, not the raw folder id.
        cut.Find("[data-testid='favorites-list']").TextContent.Should().Contain("/Finance");
    }
}
