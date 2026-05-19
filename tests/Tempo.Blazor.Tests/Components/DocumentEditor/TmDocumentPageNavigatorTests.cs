using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class TmDocumentPageNavigatorTests : LocalizationTestBase
{
    [Fact]
    public void PageNavigator_RendersPageCountAndItems()
    {
        var cut = RenderComponent<TmDocumentPageNavigator>(parameters => parameters
            .Add(p => p.Metrics, Metrics()));

        cut.Find("[data-testid='document-page-navigator']").Should().NotBeNull();
        cut.Find("[data-testid='document-page-navigator-count']").TextContent.Should().Contain("3 pages");
        cut.FindAll("[data-testid='document-page-navigator-item']").Should().HaveCount(3);
    }

    [Fact]
    public void PageNavigator_ClickPage_InvokesNavigationCallback()
    {
        var navigated = -1;
        var cut = RenderComponent<TmDocumentPageNavigator>(parameters => parameters
            .Add(p => p.Metrics, Metrics())
            .Add(p => p.OnNavigateToPage, EventCallback.Factory.Create<int>(this, page => navigated = page)));

        cut.FindAll("[data-testid='document-page-navigator-item']")[1].Click();

        navigated.Should().Be(1);
    }

    [Fact]
    public void PageNavigator_MarksActivePageAndOverflow()
    {
        var cut = RenderComponent<TmDocumentPageNavigator>(parameters => parameters
            .Add(p => p.Metrics, Metrics())
            .Add(p => p.ActivePageIndex, 2));

        var items = cut.FindAll("[data-testid='document-page-navigator-item']");
        items[2].GetAttribute("aria-current").Should().Be("page");
        cut.Find("[data-testid='document-page-navigator-overflow']").TextContent.Should().Contain("Overflow");
    }

    private static WysiwygPageMetrics Metrics() => new()
    {
        TotalPages = 3,
        RenderedPages = 2,
        VirtualizedPages = 1,
        ActivePageIndex = 0,
        Pages =
        [
            new WysiwygPageMetric { PageIndex = 0, PageNumber = 1, Label = "Page 1", BlockIds = ["h1"] },
            new WysiwygPageMetric { PageIndex = 1, PageNumber = 2, Label = "Page 2", IsVirtual = true },
            new WysiwygPageMetric { PageIndex = 2, PageNumber = 3, Label = "Page 3", HasOverflow = true }
        ]
    };
}
