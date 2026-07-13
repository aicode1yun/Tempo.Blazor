using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>
/// Verifies the accessible sort state (aria-sort) TmDataTable exposes on sortable column headers,
/// so assistive tech announces ascending/descending/none as the user cycles sorting.
/// </summary>
public class TmDataTableAriaSortTests : LocalizationTestBase
{
    private sealed record SortPerson(string Name, int Age);

    private static List<SortPerson> People =>
    [
        new("Charlie", 30),
        new("Alice",   25),
        new("Bob",     35),
    ];

    private IRenderedComponent<TmDataTable<SortPerson>> RenderWithSortableNameColumn()
        => RenderComponent<TmDataTable<SortPerson>>(p =>
        {
            p.Add(c => c.Items, People);
            p.AddChildContent(b =>
            {
                b.OpenComponent<TmDataTableColumn<SortPerson>>(0);
                b.AddAttribute(1, "Title", "Name");
                b.AddAttribute(2, "PropertyName", "Name");
                b.AddAttribute(3, "Sortable", true);
                b.AddAttribute(4, "Field", (Func<SortPerson, object?>)(x => x.Name));
                b.CloseComponent();
            });
        });

    [Fact]
    public void AriaSort_IsNone_BeforeSorting()
    {
        var cut = RenderWithSortableNameColumn();

        cut.Find("th[data-sortable='true']").GetAttribute("aria-sort").Should().Be("none");
    }

    [Fact]
    public void AriaSort_IsAscending_AfterFirstClick()
    {
        var cut = RenderWithSortableNameColumn();

        cut.Find("th[data-sortable='true']").Click();

        cut.Find("th[data-sortable='true']").GetAttribute("aria-sort").Should().Be("ascending");
    }

    [Fact]
    public void AriaSort_IsDescending_AfterSecondClick()
    {
        var cut = RenderWithSortableNameColumn();

        var header = cut.Find("th[data-sortable='true']");
        header.Click();
        header.Click();

        cut.Find("th[data-sortable='true']").GetAttribute("aria-sort").Should().Be("descending");
    }
}
