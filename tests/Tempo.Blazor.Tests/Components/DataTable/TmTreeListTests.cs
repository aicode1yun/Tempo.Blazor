using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DataTable;

/// <summary>TDD tests for TmTreeList&lt;TItem&gt;.</summary>
public class TmTreeListTests : LocalizationTestBase
{
    private record TreeEmp(int Id, string Name, int? ManagerId);
    private static List<TreeEmp> MakeData() =>
    [
        new(1, "CEO", null),
        new(2, "Alice", 1),
        new(3, "Bob", 1),
        new(4, "Charlie", 2),
        new(5, "Diana", 2),
        new(6, "Eve", 3),
    ];

    [Fact]
    public void TmTreeList_Renders_Table()
    {
        var data = MakeData();
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.Find(".tm-tree-list").Should().NotBeNull();
    }

    [Fact]
    public void TmTreeList_Renders_Rows_For_Roots_Only_When_Collapsed()
    {
        var data = MakeData();
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        // Only root "CEO" visible initially (collapsed by default)
        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(1);
        cut.Markup.Should().Contain("CEO");
    }

    [Fact]
    public void TmTreeList_Expand_Root_Shows_Children()
    {
        var data = MakeData();
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        // Click expand on CEO
        var toggle = cut.Find(".tm-tree-list-toggle");
        toggle.Click();

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(3); // CEO + Alice + Bob
    }

    [Fact]
    public void TmTreeList_Collapse_Root_Hides_Children()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var rowsBefore = cut.FindAll(".tm-tree-list-row");
        rowsBefore.Count.Should().Be(3);

        // Click collapse on CEO
        var toggle = cut.Find(".tm-tree-list-toggle");
        toggle.Click();

        var rowsAfter = cut.FindAll(".tm-tree-list-row");
        rowsAfter.Count.Should().Be(1);
    }

    [Fact]
    public void TmTreeList_Indentation_Increases_With_Level()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1, 2 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(5); // CEO + Alice + Bob + Charlie + Diana

        // Check padding-left on expand cells
        var expands = cut.FindAll(".tm-tree-list-expand");
        expands[0].GetAttribute("style")!.Should().Contain("padding-left: 0.0rem");      // CEO
        expands[1].GetAttribute("style")!.Should().Contain("padding-left: 1.5rem");   // Alice
        expands[2].GetAttribute("style")!.Should().Contain("padding-left: 3.0rem");   // Charlie
        expands[3].GetAttribute("style")!.Should().Contain("padding-left: 3.0rem");     // Diana
    }

    [Fact]
    public void TmTreeList_Leaf_Has_No_Toggle()
    {
        var data = new List<TreeEmp> { new(1, "CEO", null) };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.FindAll(".tm-tree-list-toggle").Should().BeEmpty();
    }

    [Fact]
    public void TmTreeList_Parent_Has_Toggle()
    {
        var data = MakeData();
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.FindAll(".tm-tree-list-toggle").Count.Should().Be(1); // CEO
    }

    [Fact]
    public void TmTreeList_Selectable_Row_Click_Fires_OnRowSelect()
    {
        var data = MakeData();
        TreeEmp? selected = null;
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Selectable, true)
            .Add(c => c.OnRowSelect, (TreeEmp item) => selected = item)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var row = cut.Find(".tm-tree-list-row");
        row.Click();

        selected.Should().NotBeNull();
        selected!.Name.Should().Be("CEO");
    }

    [Fact]
    public void TmTreeList_Selectable_Adds_Selected_Class()
    {
        var data = MakeData();
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Selectable, true)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var row = cut.Find(".tm-tree-list-row");
        row.Click();

        row.ClassList.Should().Contain("tm-tree-list-row--selected");
    }

    [Fact]
    public void TmTreeList_Empty_Data_Shows_NoData_Message()
    {
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, new List<TreeEmp>())
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.Find(".tm-tree-list-cell--empty").TextContent.Should().Contain("No data");
    }

    [Fact]
    public void TmTreeList_SortBy_Orders_Siblings_Ascending()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.SortBy, x => x.Name)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(3); // CEO + Alice + Bob

        // Alice should come before Bob alphabetically
        rows[1].TextContent.Should().Contain("Alice");
        rows[2].TextContent.Should().Contain("Bob");
    }

    [Fact]
    public void TmTreeList_SortBy_Orders_Siblings_Descending()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.SortBy, x => x.Name)
            .Add(c => c.SortDescending, true)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(3);

        // Bob should come before Alice when descending
        rows[1].TextContent.Should().Contain("Bob");
        rows[2].TextContent.Should().Contain("Alice");
    }

    [Fact]
    public void TmTreeList_Filter_Shows_Matching_Rows_And_Ancestors()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.Filter, x => x.Name.Contains("Charlie"))
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(3); // CEO + Alice + Charlie (ancestors of Charlie)

        rows[0].TextContent.Should().Contain("CEO");
        rows[1].TextContent.Should().Contain("Alice");
        rows[2].TextContent.Should().Contain("Charlie");
    }

    [Fact]
    public void TmTreeList_Filter_NoMatch_Shows_Empty()
    {
        var data = MakeData();
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.Filter, x => x.Name.Contains("Zebra"))
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.Find(".tm-tree-list-cell--empty").TextContent.Should().Contain("No data");
    }

    // ── Inline editing (TL-13) ───────────────────────────────────

    [Fact]
    public void TmTreeList_InlineEditing_DoubleClick_EntersEditMode()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.Editable, true)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> {
                new() { Title = "Name", Field = x => x.Name, Editable = true,
                        EditTemplate = item => builder => builder.AddMarkupContent(0, $"<input class=\"edit-name\" value=\"{item.Name}\" />") }
            }));

        var row = cut.FindAll(".tm-tree-list-row")[1]; // Alice
        row.DoubleClick();

        cut.Find(".edit-name").Should().NotBeNull();
        cut.Find(".edit-name").GetAttribute("value")!.Should().Be("Alice");
    }

    [Fact]
    public async Task TmTreeList_InlineEditing_Blur_CommitsEdit()
    {
        TreeEmp? edited = null;
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.Editable, true)
            .Add(c => c.OnRowEdit, (TreeEmp item) => edited = item)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> {
                new() { Title = "Name", Field = x => x.Name, Editable = true,
                        EditTemplate = item => builder => builder.AddMarkupContent(0, "<input class=\"edit-name\" />") }
            }));

        var row = cut.FindAll(".tm-tree-list-row")[1]; // Alice
        row.DoubleClick();

        var editCell = cut.Find(".tm-tree-list-edit-cell");
        editCell.FocusOut();

        edited.Should().NotBeNull();
        edited!.Name.Should().Be("Alice");
    }

    [Fact]
    public void TmTreeList_InlineEditing_Escape_CancelsEdit()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.Editable, true)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> {
                new() { Title = "Name", Field = x => x.Name, Editable = true,
                        EditTemplate = item => builder => builder.AddMarkupContent(0, "<input class=\"edit-name\" />") }
            }));

        var row = cut.FindAll(".tm-tree-list-row")[1]; // Alice
        row.DoubleClick();

        cut.Find(".edit-name").Should().NotBeNull();

        var editCell = cut.Find(".tm-tree-list-edit-cell");
        editCell.KeyDown("Escape");

        cut.FindAll(".edit-name").Should().BeEmpty();
    }

    // ── Pagination (TL-14) ───────────────────────────────────────

    [Fact]
    public void TmTreeList_Pagination_ShowsFirstPageOnly()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.PageSize, 2)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(2); // CEO + Alice (first page)
    }

    [Fact]
    public void TmTreeList_Pagination_NextPage_ShowsNextRows()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.PageSize, 2)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var nextBtn = cut.Find(".tm-pagination-next");
        nextBtn.Click();

        var rows = cut.FindAll(".tm-tree-list-row");
        rows.Count.Should().Be(1); // Bob (second page, only 1 item left)
        rows[0].TextContent.Should().Contain("Bob");
    }

    [Fact]
    public void TmTreeList_Pagination_InfoText_ShowsCount()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.PageSize, 2)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        var info = cut.Find(".tm-pagination-info").TextContent;
        info.Should().Contain("1");
        info.Should().Contain("2");
        info.Should().Contain("3"); // total visible = 3 (CEO+Alice+Bob)
    }

    // ── Drag-to-select (TL-15) ───────────────────────────────────

    [Fact]
    public void TmTreeList_MultiSelect_CtrlClick_TogglesSelection()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.OnSelectionChange, (IReadOnlySet<object> ids) => { })
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.FindAll(".tm-tree-list-row")[0].Click(new MouseEventArgs { CtrlKey = true }); // CEO
        cut.FindAll(".tm-tree-list-row")[1].Click(new MouseEventArgs { CtrlKey = true }); // Alice

        cut.FindAll(".tm-tree-list-row")[0].ClassList.Should().Contain("tm-tree-list-row--selected");
        cut.FindAll(".tm-tree-list-row")[1].ClassList.Should().Contain("tm-tree-list-row--selected");

        // Toggle CEO off
        cut.FindAll(".tm-tree-list-row")[0].Click(new MouseEventArgs { CtrlKey = true });
        cut.FindAll(".tm-tree-list-row")[0].ClassList.Should().NotContain("tm-tree-list-row--selected");
        cut.FindAll(".tm-tree-list-row")[1].ClassList.Should().Contain("tm-tree-list-row--selected");
    }

    [Fact]
    public void TmTreeList_MultiSelect_ShiftClick_SelectsRange()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.FindAll(".tm-tree-list-row")[0].Click();                     // CEO (anchor)
        cut.FindAll(".tm-tree-list-row")[2].Click(new MouseEventArgs { ShiftKey = true }); // Bob (range)

        cut.FindAll(".tm-tree-list-row")[0].ClassList.Should().Contain("tm-tree-list-row--selected");
        cut.FindAll(".tm-tree-list-row")[1].ClassList.Should().Contain("tm-tree-list-row--selected");
        cut.FindAll(".tm-tree-list-row")[2].ClassList.Should().Contain("tm-tree-list-row--selected");
    }

    [Fact]
    public void TmTreeList_MultiSelect_DragToSelect_SelectsRange()
    {
        var data = MakeData();
        var expanded = new HashSet<object> { 1 };
        var cut = Render<TmTreeList<TreeEmp>>(p => p
            .Add(c => c.Items, data)
            .Add(c => c.IdSelector, x => x.Id)
            .Add(c => c.ParentIdSelector, x => x.ManagerId)
            .Add(c => c.ExpandedIds, expanded)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.Columns, new List<TmTreeListColumn<TreeEmp>> { new() { Title = "Name", Field = x => x.Name } }));

        cut.FindAll(".tm-tree-list-row")[0].MouseDown();   // CEO
        cut.FindAll(".tm-tree-list-row")[2].TriggerEvent("onmouseenter", new MouseEventArgs()); // Bob
        cut.FindAll(".tm-tree-list-row")[2].MouseUp();     // Bob

        cut.FindAll(".tm-tree-list-row")[0].ClassList.Should().Contain("tm-tree-list-row--selected");
        cut.FindAll(".tm-tree-list-row")[1].ClassList.Should().Contain("tm-tree-list-row--selected");
        cut.FindAll(".tm-tree-list-row")[2].ClassList.Should().Contain("tm-tree-list-row--selected");
    }
}
