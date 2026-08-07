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
/// Public so NSubstitute can proxy <c>IDataTableDataProvider&lt;ResetPerson&gt;</c>.
/// </summary>
public record ResetPerson(string Name, int Index);

/// <summary>
/// Covers <c>ResetPageAsync</c> — the public way back to page one.
/// <para>
/// A page that owns its filter surface (ToolbarMode=ContentOnly) hands the table an already-narrowed
/// result set, so the table cannot tell a new query from the same list arriving again. It only clamps
/// the current page *down* to the new last page, which means searching while on page 3 leaves the user
/// on page 3 — looking at the tail of the new results, or at an empty-looking list — instead of at the
/// top. The table's own search box resets the page, but a host-owned search never goes through it, and
/// before this there was no public member that did.
/// </para>
/// </summary>
public class TmDataTableResetPageTests : LocalizationTestBase
{
    private static List<ResetPerson> MakePeople(int count, string prefix = "Person") =>
        Enumerable.Range(1, count).Select(i => new ResetPerson($"{prefix} {i}", i)).ToList();

    private IRenderedComponent<TmDataTable<ResetPerson>> RenderTable(IReadOnlyList<ResetPerson> items)
        => Render<TmDataTable<ResetPerson>>(p =>
        {
            p.Add(c => c.Items, items);
            p.Add(c => c.DefaultPageSize, 10);
            p.AddChildContent<TmDataTableColumn<ResetPerson>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.PropertyName, "Name")
                .Add(c => c.Field, (Func<ResetPerson, object?>)(x => x.Name)));
        });

    private static IReadOnlyList<string> Names(IRenderedComponent<TmDataTable<ResetPerson>> cut)
        => cut.FindAll("tbody tr").Select(r => r.QuerySelector("td")!.TextContent.Trim()).ToList();

    [Fact]
    public async Task ResetPageAsync_ReturnsToTheFirstPage()
    {
        var cut = RenderTable(MakePeople(50));
        cut.Find("[data-testid='pagination-page-3']").Click();
        Names(cut)[0].Should().Be("Person 21");

        await cut.InvokeAsync(() => cut.Instance.ResetPageAsync());

        Names(cut)[0].Should().Be("Person 1");
        cut.Find("[aria-current='page']").GetAttribute("data-testid").Should().Be("pagination-page-1");
    }

    [Fact]
    public void WithoutReset_ANarrowedResultSetStrandsTheUserDownThePages()
    {
        // The regression itself, so the fix is measured against the real behaviour and not an assumption:
        // the table clamps down to the new last page, it does not go back to the top.
        var cut = RenderTable(MakePeople(50));
        cut.Find("[data-testid='pagination-page-5']").Click();

        cut.Render(p => p.Add(c => c.Items, MakePeople(22, "Match")));

        Names(cut)[0].Should().Be("Match 21", "the table clamps to the last page rather than to page 1");
    }

    [Fact]
    public async Task ResetPageAsync_AfterTheHostNarrowedTheResults_ShowsTheTopOfThem()
    {
        var cut = RenderTable(MakePeople(50));
        cut.Find("[data-testid='pagination-page-5']").Click();

        cut.Render(p => p.Add(c => c.Items, MakePeople(22, "Match")));
        await cut.InvokeAsync(() => cut.Instance.ResetPageAsync());

        Names(cut)[0].Should().Be("Match 1");
        Names(cut).Should().HaveCount(10);
    }

    [Fact]
    public async Task ResetPageAsync_OnTheFirstPageAlready_ChangesNothing()
    {
        var cut = RenderTable(MakePeople(50));

        await cut.InvokeAsync(() => cut.Instance.ResetPageAsync());

        Names(cut)[0].Should().Be("Person 1");
        Names(cut).Should().HaveCount(10);
    }

    [Fact]
    public async Task ResetPageAsync_RequeriesTheProviderForPageOne()
    {
        var provider = Substitute.For<IDataTableDataProvider<ResetPerson>>();
        provider.GetDataAsync(Arg.Any<DataTableQuery>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PagedResult<ResetPerson>
                {
                    Items = MakePeople(10),
                    TotalCount = 50,
                    Page = 3,
                    PageSize = 10,
                }));

        var cut = Render<TmDataTable<ResetPerson>>(p => p.Add(c => c.DataProvider, provider));
        await cut.InvokeAsync(() => { });
        provider.ClearReceivedCalls();

        await cut.InvokeAsync(() => cut.Instance.ResetPageAsync());

        await provider.Received(1).GetDataAsync(
            Arg.Is<DataTableQuery>(q => q.Page == 1),
            Arg.Any<CancellationToken>());
    }
}
