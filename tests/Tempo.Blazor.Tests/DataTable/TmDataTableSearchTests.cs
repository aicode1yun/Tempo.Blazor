using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>Tests for controlled search state (SearchText / SearchTextChanged) in TmDataTable.</summary>
public class TmDataTableSearchTests : LocalizationTestBase
{
    public record SearchPerson(string Name, int Age, string Role);

    private static List<SearchPerson> People =>
    [
        new("Alice", 30, "Admin"),
        new("Bob", 25, "User"),
        new("Carol", 35, "Manager")
    ];

    private static RenderFragment NameColumn => builder =>
    {
        builder.OpenComponent<TmDataTableColumn<SearchPerson>>(0);
        builder.AddAttribute(1, "Title", "Name");
        builder.AddAttribute(2, "Field", (Func<SearchPerson, object>)(x => x.Name));
        builder.CloseComponent();
    };

    [Fact]
    public void TmDataTable_SearchText_SetExternally_FiltersClientItems()
    {
        var cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.Items, People)
            .Add(c => c.SearchText, "Bob")
            .AddChildContent(NameColumn));

        cut.FindAll("tbody tr").Count.Should().Be(1);
        cut.Find("tbody tr").TextContent.Should().Contain("Bob");
    }

    [Fact]
    public void TmDataTable_SearchText_SetExternally_WithShowSearchFalse_FiltersItems()
    {
        var cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.Items, People)
            .Add(c => c.SearchText, "Alice")
            .Add(c => c.ShowSearch, false)
            .AddChildContent(NameColumn));

        cut.FindAll(".tm-input-search").Should().BeEmpty();
        cut.FindAll("tbody tr").Count.Should().Be(1);
        cut.Find("tbody tr").TextContent.Should().Contain("Alice");
    }

    [Fact]
    public void TmDataTable_SearchTextChanged_Fires_WhenInternalInputChanges()
    {
        string? captured = null;
        var cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.Items, People)
            .Add(c => c.SearchTextChanged, (string value) => captured = value)
            .AddChildContent(NameColumn));

        var input = cut.Find(".tm-input-search");
        input.Input("Car");

        captured.Should().Be("Car");
    }

    [Fact]
    public async Task TmDataTable_SearchText_SetExternally_UsesServerProvider()
    {
        var provider = Substitute.For<IDataTableDataProvider<SearchPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PagedResult<SearchPerson>
            {
                Items = [new("Filtered", 1, "X")],
                TotalCount = 1,
                Page = call.Arg<DataTableQuery>().Page,
                PageSize = call.Arg<DataTableQuery>().PageSize
            }));

        var cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ViewContext, "search-test")
            .Add(c => c.SearchText, "server-term")
            .AddChildContent(NameColumn));

        await cut.InvokeAsync(() => cut.Render());

        await provider.Received().GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.SearchText == "server-term"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TmDataTable_UncontrolledSearch_PersistsAfterParentRerender()
    {
        var cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.Items, People)
            .AddChildContent(NameColumn));

        var input = cut.Find(".tm-input-search");
        input.Input("Bob");

        // Simulate an unrelated parent rerender (no SearchText parameter change)
        cut.Render();

        cut.FindAll("tbody tr").Count.Should().Be(1);
        cut.Find("tbody tr").TextContent.Should().Contain("Bob");
    }

    [Fact]
    public async Task TmDataTable_SearchText_ExternalChange_ServerProvider_ReloadsOnce()
    {
        var provider = Substitute.For<IDataTableDataProvider<SearchPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PagedResult<SearchPerson>
            {
                Items = [new("Result", 1, "X")],
                TotalCount = 1,
                Page = call.Arg<DataTableQuery>().Page,
                PageSize = call.Arg<DataTableQuery>().PageSize
            }));

        var cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ViewContext, "search-test")
            .AddChildContent(NameColumn));

        await cut.InvokeAsync(() => cut.Render());

        cut.Render(p => p.Add(c => c.SearchText, "alpha"));
        await cut.InvokeAsync(() => cut.Render());

        cut.Render(p => p.Add(c => c.SearchText, "beta"));
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
    public async Task TmDataTable_ControlledSearch_InternalInput_DoesNotReloadTwice()
    {
        var provider = Substitute.For<IDataTableDataProvider<SearchPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PagedResult<SearchPerson>
            {
                Items = [new("Result", 1, "X")],
                TotalCount = 1,
                Page = call.Arg<DataTableQuery>().Page,
                PageSize = call.Arg<DataTableQuery>().PageSize
            }));

        IRenderedComponent<TmDataTable<SearchPerson>>? cut = null;
        var searchText = string.Empty;
        cut = Render<TmDataTable<SearchPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ViewContext, "search-test")
            .Add(c => c.SearchText, searchText)
            .Add(c => c.SearchTextChanged, (string value) =>
            {
                searchText = value;
                cut!.Render(pp => pp.Add(c => c.SearchText, value));
            })
            .AddChildContent(NameColumn));

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
