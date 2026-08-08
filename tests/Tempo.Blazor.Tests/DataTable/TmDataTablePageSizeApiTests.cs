using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>
/// Public so NSubstitute can proxy <c>IDataTableDataProvider&lt;SizedPerson&gt;</c>.
/// </summary>
public record SizedPerson(string Name, int Index);

/// <summary>
/// Covers the public page-size surface — <c>ChangePageSizeAsync</c> and the controlled
/// <c>PageSize</c> parameter.
/// <para>
/// <c>DefaultPageSize</c> is read once, in <c>OnInitializedAsync</c>, and the only member that could
/// change the size afterwards was private. A host with its own page-size control therefore had no way
/// to resize a mounted table and had to remount it through <c>@key</c>, which throws away scroll
/// position, focus and expanded rows along with the page size.
/// </para>
/// </summary>
public class TmDataTablePageSizeApiTests : LocalizationTestBase
{
    private static List<SizedPerson> MakePeople(int count, string prefix = "Person") =>
        Enumerable.Range(1, count).Select(i => new SizedPerson($"{prefix} {i}", i)).ToList();

    private IRenderedComponent<TmDataTable<SizedPerson>> RenderTable(
        IReadOnlyList<SizedPerson> items,
        Action<ComponentParameterCollectionBuilder<TmDataTable<SizedPerson>>>? extra = null)
        => Render<TmDataTable<SizedPerson>>(p =>
        {
            p.Add(c => c.Items, items);
            p.Add(c => c.DefaultPageSize, 10);
            p.AddChildContent<TmDataTableColumn<SizedPerson>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.PropertyName, "Name")
                .Add(c => c.Sortable, true)
                .Add(c => c.Field, (Func<SizedPerson, object?>)(x => x.Name)));
            extra?.Invoke(p);
        });

    private static IReadOnlyList<string> Names(IRenderedComponent<TmDataTable<SizedPerson>> cut)
        => cut.FindAll("tbody tr").Select(r => r.QuerySelector("td")!.TextContent.Trim()).ToList();

    private static IDataTableDataProvider<SizedPerson> MakeProvider()
    {
        var provider = Substitute.For<IDataTableDataProvider<SizedPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var q = ci.Arg<DataTableQuery>();
                    return Task.FromResult(new PagedResult<SizedPerson>
                    {
                        Items = MakePeople(q.PageSize),
                        TotalCount = 500,
                        Page = q.Page,
                        PageSize = q.PageSize,
                    });
                });
        return provider;
    }

    // ── ChangePageSizeAsync ───────────────────────────────────────

    [Fact]
    public async Task ChangePageSizeAsync_IsPublic_AndResizesAMountedTable()
    {
        var cut = RenderTable(MakePeople(50));
        Names(cut).Should().HaveCount(10);

        await cut.InvokeAsync(() => cut.Instance.ChangePageSizeAsync(25));

        Names(cut).Should().HaveCount(25);
    }

    [Fact]
    public async Task ChangePageSizeAsync_ReturnsToPageOne()
    {
        var cut = RenderTable(MakePeople(50));
        cut.Find("[data-testid='pagination-page-3']").Click();
        Names(cut)[0].Should().Be("Person 21");

        await cut.InvokeAsync(() => cut.Instance.ChangePageSizeAsync(25));

        Names(cut)[0].Should().Be("Person 1");
        cut.Find("[aria-current='page']").GetAttribute("data-testid").Should().Be("pagination-page-1");
    }

    [Fact]
    public async Task ChangePageSizeAsync_KeepsTableStateThatARemountWouldHaveLost()
    {
        // The point of the API: the sort the user applied survives, because the table is resized in place.
        // Two clicks = descending, which is the only direction that differs from the unsorted order here.
        var cut = RenderTable(MakePeople(50));
        cut.Find("th[data-sortable='true']").Click();
        cut.Find("th[data-sortable='true']").Click();
        Names(cut)[0].Should().Be("Person 9", "guard: the table really is sorted descending now");

        await cut.InvokeAsync(() => cut.Instance.ChangePageSizeAsync(25));

        Names(cut)[0].Should().Be("Person 9", "the sort applied by the user must survive a resize");
        Names(cut).Should().HaveCount(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ChangePageSizeAsync_RejectsANonPositiveSize(int size)
    {
        var cut = RenderTable(MakePeople(50));

        var act = () => cut.InvokeAsync(() => cut.Instance.ChangePageSizeAsync(size));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ChangePageSizeAsync_RequeriesTheProviderWithTheNewSizeOnPageOne()
    {
        var provider = MakeProvider();
        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.DefaultPageSize, 10));
        await cut.InvokeAsync(() => { });
        cut.Find("[data-testid='pagination-page-3']").Click();
        provider.ClearReceivedCalls();

        await cut.InvokeAsync(() => cut.Instance.ChangePageSizeAsync(50));

        await provider.Received(1).GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.PageSize == 50 && q.Page == 1),
            Arg.Any<CancellationToken>());
    }

    // ── PageSize parameter (controlled) ───────────────────────────

    [Fact]
    public void PageSize_SuppliedAtMount_WinsOverDefaultPageSize()
    {
        var cut = RenderTable(MakePeople(50), p => p.Add(c => c.PageSize, 25));

        Names(cut).Should().HaveCount(25);
    }

    [Fact]
    public void PageSize_Null_LeavesDefaultPageSizeInCharge()
    {
        // Non-breaking guard: the released DefaultPageSize behaviour is untouched when PageSize is not used.
        var cut = RenderTable(MakePeople(50));

        Names(cut).Should().HaveCount(10);
    }

    [Fact]
    public void PageSize_ChangedAfterMount_ResizesTheTableAndReturnsToPageOne()
    {
        var cut = RenderTable(MakePeople(50), p => p.Add(c => c.PageSize, 10));
        cut.Find("[data-testid='pagination-page-3']").Click();

        cut.Render(p => p.Add(c => c.PageSize, 25));

        Names(cut).Should().HaveCount(25);
        Names(cut)[0].Should().Be("Person 1");
    }

    [Fact]
    public void PageSize_ChangedAfterMount_KeepsTableStateThatARemountWouldHaveLost()
    {
        var cut = RenderTable(MakePeople(50), p => p.Add(c => c.PageSize, 10));
        cut.Find("th[data-sortable='true']").Click();
        cut.Find("th[data-sortable='true']").Click();
        Names(cut)[0].Should().Be("Person 9", "guard: the table really is sorted descending now");

        cut.Render(p => p.Add(c => c.PageSize, 25));

        Names(cut)[0].Should().Be("Person 9", "the sort applied by the user must survive a resize");
        Names(cut).Should().HaveCount(25);
    }

    [Fact]
    public async Task PageSize_ChangedAfterMount_RequeriesTheProvider()
    {
        var provider = MakeProvider();
        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, 10));
        await cut.InvokeAsync(() => { });
        provider.ClearReceivedCalls();

        cut.Render(p => p.Add(c => c.PageSize, 50));

        await provider.Received(1).GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.PageSize == 50 && q.Page == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PageSize_SuppliedAtMount_ReachesTheProvidersVeryFirstQuery()
    {
        var provider = MakeProvider();
        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.DefaultPageSize, 10)
            .Add(c => c.PageSize, 50));
        await cut.InvokeAsync(() => { });

        await provider.Received(1).GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.PageSize == 50),
            Arg.Any<CancellationToken>());
        await provider.DidNotReceive().GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PageSize_SetToTheSameValueAgain_DoesNotResetThePage()
    {
        // A parent re-render must not silently drag the user back to page one.
        var cut = RenderTable(MakePeople(50), p => p.Add(c => c.PageSize, 10));
        cut.Find("[data-testid='pagination-page-3']").Click();

        cut.Render(p => p.Add(c => c.PageSize, 10));

        Names(cut)[0].Should().Be("Person 21");
    }

    // ── PageSizeChanged (two-way binding) ─────────────────────────

    [Fact]
    public void PageSizeChanged_FiresWhenTheUserPicksInTheBuiltInDropdown()
    {
        int? captured = null;
        var cut = RenderTable(MakePeople(50), p => p
            .Add(c => c.PageSize, 10)
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => captured = v)));

        cut.Find("[data-testid='pagination-page-size']").Change("25");

        captured.Should().Be(25);
    }

    [Fact]
    public async Task PageSizeChanged_FiresForTheImperativeCallToo()
    {
        int? captured = null;
        var cut = RenderTable(MakePeople(50), p => p
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => captured = v)));

        await cut.InvokeAsync(() => cut.Instance.ChangePageSizeAsync(25));

        captured.Should().Be(25);
    }

    [Fact]
    public async Task BoundPageSize_ChangedFromTheDropdown_QueriesTheProviderOnlyOnce()
    {
        // The bound value comes straight back in as a parameter; that round-trip must not re-query.
        var provider = MakeProvider();
        var pageSize = 10;
        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, pageSize)
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => pageSize = v)));
        await cut.InvokeAsync(() => { });
        provider.ClearReceivedCalls();

        cut.Find("[data-testid='pagination-page-size']").Change("25");
        cut.Render(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, pageSize)
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => pageSize = v)));

        pageSize.Should().Be(25);
        await provider.Received(1).GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PageSizeChanged_DoesNotEchoBackAHostDrivenChange()
    {
        // The host already knows the value it just set; echoing it back would loop through its own setter.
        // Server-side, because the reload that follows the parameter change is what would do the echoing.
        var provider = MakeProvider();
        var calls = new List<int>();
        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, 10)
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => calls.Add(v))));
        await cut.InvokeAsync(() => { });

        cut.Render(p => p.Add(c => c.PageSize, 25));

        calls.Should().BeEmpty();
    }

    [Fact]
    public async Task PageSizeChanged_FiresWhenTheProviderAnswersWithADifferentPageSize()
    {
        // A provider that caps the page size wins over the requested value, so a bound host has to hear it.
        var provider = Substitute.For<IDataTableDataProvider<SizedPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PagedResult<SizedPerson>
                {
                    Items = MakePeople(20),
                    TotalCount = 500,
                    Page = 1,
                    PageSize = 20, // capped: the table asked for 100
                }));

        var calls = new List<int>();
        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.DefaultPageSize, 100)
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => calls.Add(v))));
        await cut.InvokeAsync(() => { });

        calls.Should().Equal(20);
    }

    [Fact]
    public async Task AppliedSavedView_WithAPageSize_ResizesTheTableAndReportsIt()
    {
        var view = new DataTableView
        {
            Id = "v1",
            Name = "Wide",
            Scope = ViewScope.Personal,
            CreatedBy = "u1",
            PageSize = 25,
        };
        var viewProvider = Substitute.For<IDataTableViewProvider>();
        viewProvider.GetViewsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IEnumerable<DataTableView>>([view]));

        var calls = new List<int>();
        var cut = RenderTable(MakePeople(50), p => p
            .Add(c => c.ViewProvider, viewProvider)
            .Add(c => c.ViewContext, "ctx")
            .Add(c => c.CurrentUserId, "u1")
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, v => calls.Add(v))));
        await cut.InvokeAsync(() => { });

        cut.Find(".tm-view-manager-toggle").Click();
        cut.Find(".tm-view-item").Click();

        Names(cut).Should().HaveCount(25);
        calls.Should().Equal(25);
    }

    // ── Controlled mode without a binding ─────────────────────────

    [Fact]
    public void ControlledPageSize_WithoutABinding_ReSyncsAfterTheBuiltInDropdownChangedIt()
    {
        // The host owns the value but supplied no PageSizeChanged, so the dropdown's change can never reach
        // it. Rather than let the two drift apart silently, the next parameter set snaps back to the host.
        var cut = RenderTable(MakePeople(50), p => p.Add(c => c.PageSize, 10));
        cut.Find("[data-testid='pagination-page-size']").Change("25");
        Names(cut).Should().HaveCount(25, "guard: the dropdown did change the size locally");

        cut.Render(p => p.Add(c => c.PageSize, 10));

        Names(cut).Should().HaveCount(10);
    }

    [Fact]
    public void UncontrolledPageSize_KeepsTheDropdownsChoiceAcrossAParentRender()
    {
        // The mirror image: with no PageSize parameter the dropdown owns the value and a parent re-render
        // must not undo the user's choice.
        var cut = RenderTable(MakePeople(50));
        cut.Find("[data-testid='pagination-page-size']").Change("25");

        cut.Render(p => p.Add(c => c.Items, MakePeople(50)));

        Names(cut).Should().HaveCount(25);
    }

    [Fact]
    public async Task ProviderImposedPageSize_DoesNotMakeEveryParentRenderRequery()
    {
        // A provider that caps the size answers the re-synced query with the same cap, so treating it like a
        // drift would cost one query and one jump back to page one per parent render.
        var provider = Substitute.For<IDataTableDataProvider<SizedPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PagedResult<SizedPerson>
                {
                    Items = MakePeople(20),
                    TotalCount = 500,
                    Page = 1,
                    PageSize = 20, // capped: the table asked for 100
                }));

        var cut = Render<TmDataTable<SizedPerson>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, 100));
        await cut.InvokeAsync(() => { });
        provider.ClearReceivedCalls();

        cut.Render(p => p.Add(c => c.PageSize, 100));

        await provider.DidNotReceive().GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>());
    }

    // ── Out-of-order provider responses ───────────────────────────

    [Fact]
    public async Task ASupersededProviderResponse_DoesNotOverwriteTheNewerOne()
    {
        // Every user gesture starts a load without awaiting the one in flight. Before the generation guard
        // the result that arrived last won, whatever it was — so a slow page-size query could land after a
        // faster one and leave the table showing a size nobody asked for.
        var slow = new TaskCompletionSource<PagedResult<SizedPerson>>();
        var fast = new TaskCompletionSource<PagedResult<SizedPerson>>();
        var pending = new Queue<TaskCompletionSource<PagedResult<SizedPerson>>>([slow, fast]);

        var provider = Substitute.For<IDataTableDataProvider<SizedPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
                .Returns(_ => pending.Count > 0
                    ? pending.Dequeue().Task
                    : Task.FromResult(new PagedResult<SizedPerson> { Items = [], TotalCount = 0, Page = 1, PageSize = 10 }));

        var cut = RenderTable([], p => p.Add(c => c.DataProvider, provider));

        // Second query is issued while the first is still in flight, then they complete out of order.
        Task? second = null;
        await cut.InvokeAsync(() => { second = cut.Instance.ChangePageSizeAsync(50); });
        fast.SetResult(new PagedResult<SizedPerson> { Items = MakePeople(50), TotalCount = 500, Page = 1, PageSize = 50 });
        slow.SetResult(new PagedResult<SizedPerson> { Items = MakePeople(25, "Stale"), TotalCount = 500, Page = 1, PageSize = 25 });
        await second!;
        await cut.InvokeAsync(() => { });

        Names(cut).Should().HaveCount(50, "the newer query's result must win");
        Names(cut).Should().NotContain(n => n.StartsWith("Stale"));
    }
}
