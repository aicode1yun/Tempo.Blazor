using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.DataDisplay;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.DataDisplay;

/// <summary>Tests for controlled search state (SearchText / SearchTextChanged) in TmMultiViewList.</summary>
public class TmMultiViewListSearchTests : LocalizationTestBase
{
    public record MvlSearchItem(
        string Title,
        string? SubTitle = null,
        string? StatusLabel = null,
        string? StatusColor = null,
        DateTimeOffset? Date = null,
        string? AvatarUrl = null) : IMultiViewListItem
    {
        public string Id => Title;
        public IReadOnlyList<ITag>? Tags => null;
    }

    private static IReadOnlyList<MvlSearchItem> SampleItems(int count = 3) =>
        Enumerable.Range(1, count)
            .Select(i => new MvlSearchItem($"Item {i}", $"Sub {i}"))
            .ToArray();

    [Fact]
    public void MultiViewList_SearchText_SetExternally_FiltersClientItems()
    {
        var cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.Items, SampleItems())
            .Add(c => c.SearchText, "Item 2"));

        cut.FindAll(".tm-mvl-row").Count.Should().Be(1);
        cut.Find(".tm-mvl-row").TextContent.Should().Contain("Item 2");
    }

    [Fact]
    public void MultiViewList_SearchText_SetExternally_WithShowSearchFalse_FiltersItems()
    {
        var cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.Items, SampleItems())
            .Add(c => c.SearchText, "Item 1")
            .Add(c => c.ShowSearch, false));

        cut.FindAll(".tm-input-search").Should().BeEmpty();
        cut.FindAll(".tm-mvl-row").Count.Should().Be(1);
        cut.Find(".tm-mvl-row").TextContent.Should().Contain("Item 1");
    }

    [Fact]
    public void MultiViewList_SearchTextChanged_Fires_WhenInternalInputChanges()
    {
        string? captured = null;
        var cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.Items, SampleItems())
            .Add(c => c.SearchTextChanged, (string value) => captured = value));

        var input = cut.Find(".tm-input-search");
        input.Input("Item");

        captured.Should().Be("Item");
    }

    [Fact]
    public async Task MultiViewList_SearchText_SetExternally_UsesServerProvider()
    {
        var provider = Substitute.For<IDataTableDataProvider<MvlSearchItem>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PagedResult<MvlSearchItem>
            {
                Items = [new("Server")],
                TotalCount = 1,
                Page = call.Arg<DataTableQuery>().Page,
                PageSize = call.Arg<DataTableQuery>().PageSize
            }));

        var cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ViewContext, "search-test")
            .Add(c => c.SearchText, "server-term"));

        await cut.InvokeAsync(() => cut.Render());

        await provider.Received().GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.SearchText == "server-term"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void MultiViewList_UncontrolledSearch_PersistsAfterParentRerender()
    {
        var cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.Items, SampleItems()));

        var input = cut.Find(".tm-input-search");
        input.Input("Item 2");

        // Simulate an unrelated parent rerender (no SearchText parameter change)
        cut.SetParametersAndRender();

        cut.FindAll(".tm-mvl-row").Count.Should().Be(1);
        cut.Find(".tm-mvl-row").TextContent.Should().Contain("Item 2");
    }

    [Fact]
    public async Task MultiViewList_SearchText_ExternalChange_ServerProvider_ReloadsOnce()
    {
        var provider = Substitute.For<IDataTableDataProvider<MvlSearchItem>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PagedResult<MvlSearchItem>
            {
                Items = [new("Result")],
                TotalCount = 1,
                Page = call.Arg<DataTableQuery>().Page,
                PageSize = call.Arg<DataTableQuery>().PageSize
            }));

        var cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ViewContext, "search-test"));

        await cut.InvokeAsync(() => cut.Render());

        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, "alpha"));
        await cut.InvokeAsync(() => cut.Render());

        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, "beta"));
        await cut.InvokeAsync(() => cut.Render());

        await provider.Received(3).GetDataAsync(
            Arg.Any<DataTableQuery>(),
            Arg.Any<CancellationToken>());

        await provider.Received().GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.SearchText == "alpha"),
            Arg.Any<CancellationToken>());

        await provider.Received().GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.SearchText == "beta"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultiViewList_ControlledSearch_InternalInput_DoesNotReloadTwice()
    {
        var provider = Substitute.For<IDataTableDataProvider<MvlSearchItem>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PagedResult<MvlSearchItem>
            {
                Items = [new("Result")],
                TotalCount = 1,
                Page = call.Arg<DataTableQuery>().Page,
                PageSize = call.Arg<DataTableQuery>().PageSize
            }));

        IRenderedComponent<TmMultiViewList<MvlSearchItem>>? cut = null;
        var searchText = string.Empty;
        cut = RenderComponent<TmMultiViewList<MvlSearchItem>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ViewContext, "search-test")
            .Add(c => c.SearchText, searchText)
            .Add(c => c.SearchTextChanged, (string value) =>
            {
                searchText = value;
                cut!.SetParametersAndRender(pp => pp.Add(c => c.SearchText, value));
            }));

        await cut.InvokeAsync(() => cut.Render());

        var input = cut.Find(".tm-input-search");
        input.Input("typed");
        await cut.InvokeAsync(() => cut.Render());

        // Initial load + one load after internal input; bind rerender must not add a third.
        await provider.Received(2).GetDataAsync(
            Arg.Any<DataTableQuery>(),
            Arg.Any<CancellationToken>());

        await provider.Received(1).GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.SearchText == "typed"),
            Arg.Any<CancellationToken>());
    }
}
