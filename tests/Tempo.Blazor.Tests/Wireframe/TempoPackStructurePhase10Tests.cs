using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class TempoPackStructurePhase10Tests
{
    private static readonly string[] StructureTypes =
    [
        "TmCard",
        "TmStatCard",
        "TmBadge",
        "TmChip",
        "TmChipGroup",
        "TmFilterChip",
        "TmDivider",
        "TmText",
        "TmAccordion",
        "TmAccordionItem",
        "TmEmptyState",
        "TmQRCode",
        "TmBarcode",
        "TmChangeDiff",
        "TmMultiViewList",
        "TmDataTable",
        "TmPagination",
        "TmBulkActionBar",
        "TmColumnFilter",
        "TmColumnPicker",
        "TmViewManager",
        "TmTabs",
        "TmTabPanel",
        "TmBreadcrumbs",
        "TmMenu",
        "TmContextMenu",
        "TmContextMenuItem",
        "TmBottomNavigation",
        "TmSection",
        "TmSidebar",
        "TmTopBar",
        "TmDrawer",
        "TmSplitter",
        "TmStackLayout",
        "TmDockManager",
        "TmCommandPalette",
        "TmKeyboardShortcutsHelp",
        "TmToolbar",
        "TmToolbarButton",
        "TmToolbarDivider"
    ];

    public static TheoryData<string> StructureTypeData
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in StructureTypes)
                data.Add(type);
            return data;
        }
    }

    public static TheoryData<string, (string Key, object? Value)[], string[]> RenderCases
    {
        get
        {
            var data = new TheoryData<string, (string Key, object? Value)[], string[]>();
            Add(data, "TmBadge", [("label", "Paid"), ("variant", "success")], "Paid");
            Add(data, "TmChip", [("label", "Overdue"), ("variant", "danger")], "Overdue");
            Add(data, "TmChipGroup", [("chips", new[] { "Alpha", "Beta", "Gamma" })], "Alpha", "Beta");
            Add(data, "TmFilterChip", [("label", "Open"), ("active", true)], "Open");
            Add(data, "TmDivider", [], "<line");
            Add(data, "TmText", [("text", "Hello structure")], "Hello structure");
            Add(data, "TmAccordion", [("items", new[] { "Details", "History", "Files" })], "Details", "History");
            Add(data, "TmAccordionItem", [("title", "Advanced"), ("expanded", true)], "Advanced", "Content area");
            Add(data, "TmEmptyState", [("title", "No invoices"), ("actionLabel", "Create")], "No invoices", "Create");
            Add(data, "TmQRCode", [("value", "INV-001")], "INV-001");
            Add(data, "TmBarcode", [("value", "1234567890")], "1234567890");
            Add(data, "TmChangeDiff", [("oldValue", "Draft"), ("newValue", "Approved")], "- Draft", "+ Approved");
            Add(data, "TmMultiViewList", [("title", "Invoices")], "Invoices");
            Add(data, "TmPagination", [("totalPages", 4), ("currentPage", 2)], "2");
            Add(data, "TmBulkActionBar", [("selectedCount", 7)], "7", "selected", "Delete");
            Add(data, "TmColumnFilter", [("columnName", "Customer"), ("filterType", "text")], "Customer", "text");
            Add(data, "TmColumnPicker", [("columns", new[] { "Customer", "Total", "Status" })], "Customer", "Total");
            Add(data, "TmViewManager", [("viewName", "My view")], "My view", "Save");
            Add(data, "TmTabPanel", [("label", "Details panel")], "Details panel");
            Add(data, "TmBreadcrumbs", [("items", new[] { "Home", "Invoices", "Detail" })], "Home", "Invoices");
            Add(data, "TmMenu", [("items", new[] { "Dashboard", "Projects", "Settings" })], "Dashboard", "Projects");
            Add(data, "TmContextMenu", [("items", new[] { "Edit", "Duplicate", "Delete" })], "Edit", "Delete");
            Add(data, "TmContextMenuItem", [("text", "Remove"), ("danger", true)], "Remove");
            Add(data, "TmBottomNavigation", [("items", new[] { "Home", "Search", "Inbox", "Profile" })], "Home", "Inbox");
            Add(data, "TmSidebar", [("items", new[] { "Dashboard", "Users", "Reports" })], "Dashboard", "Users");
            Add(data, "TmTopBar", [("title", "Tempo")], "Tempo", "Search");
            Add(data, "TmDrawer", [("title", "Filters")], "Filters");
            Add(data, "TmSplitter", [("pane1Label", "Preview"), ("pane2Label", "Details")], "Preview", "Details");
            Add(data, "TmStackLayout", [("items", 3)], "Item 1", "Item 3");
            Add(data, "TmDockManager", [], "Tab 1", "Canvas", "Output");
            Add(data, "TmCommandPalette", [("placeholder", "Run command")], "Run command");
            Add(data, "TmKeyboardShortcutsHelp", [("shortcuts", new[] { "Ctrl+S Save", "Ctrl+K Command" })], "Keyboard shortcuts", "Ctrl+S Save");
            Add(data, "TmToolbarButton", [("label", "Refresh"), ("icon", "refresh-cw")], "Refresh");
            Add(data, "TmToolbarDivider", [], "<line");
            return data;
        }
    }

    [Fact]
    public void StructureTypeList_CoversPlanScope()
    {
        StructureTypes.Should().HaveCount(40);
        StructureTypes.Should().Contain(["TmCard", "TmDataTable", "TmTabs", "TmSection", "TmToolbar"]);
        StructureTypes.Should().NotContain("TmKanbanBoard");
    }

    [Fact]
    public async Task TmCard_RendersBoundTitleAndShape_FromTempoPack()
    {
        var svg = await RenderAsync(
            "TmCard",
            ("title", "Faktury"),
            ("showHeader", true),
            ("showFooter", true));

        svg.Should().Contain(">Faktury<");
        svg.Should().Contain("<rect");
        svg.Should().Contain(">Save<");
        svg.Should().NotContain(">TmCard<");
    }

    [Fact]
    public async Task TmStatCard_RendersValueAndTrendMarker()
    {
        var svg = await RenderAsync(
            "TmStatCard",
            ("title", "Revenue"),
            ("value", "12 450"),
            ("trend", "up"));

        svg.Should().Contain(">12 450<");
        svg.Should().Contain("<path");
        svg.Should().Contain("#16a34a");
        svg.Should().NotContain(">TmStatCard<");
    }

    [Fact]
    public async Task TmDataTable_RendersHeaderColumnsAndRepeatedRows()
    {
        var svg = await RenderAsync(
            "TmDataTable",
            ("title", "Invoices"),
            ("columns", new[] { "Invoice", "Customer", "Total", "Status" }),
            ("rows", 4));

        using var _ = new AssertionScope();
        svg.Should().Contain(">Invoice<");
        svg.Should().Contain(">Customer<");
        svg.Split("<line").Length.Should().BeGreaterThanOrEqualTo(6);
        svg.Should().Contain("<rect");
        svg.Should().NotContain(">TmDataTable<");
    }

    [Fact]
    public async Task TmTabs_RendersActiveUnderline()
    {
        var svg = await RenderAsync(
            "TmTabs",
            ("tabs", new[] { "Overview", "Customers", "Revenue" }),
            ("activeTab", 1));

        svg.Should().Contain(">Customers<");
        svg.Should().Contain("#3b82f6");
        svg.Should().NotContain(">TmTabs<");
    }

    [Fact]
    public async Task TmSection_RendersTitleAndBodyChrome()
    {
        var svg = await RenderAsync("TmSection", ("title", "Details"));

        svg.Should().Contain(">Details<");
        svg.Should().Contain("<line");
        svg.Should().Contain("<rect");
        svg.Should().NotContain(">TmSection<");
    }

    [Fact]
    public async Task TmToolbar_RendersTitleAndActions()
    {
        var svg = await RenderAsync("TmToolbar", ("title", "Invoices"), ("sticky", true));

        svg.Should().Contain(">Invoices<");
        svg.Should().Contain(">Action<");
        svg.Should().Contain(">Cancel<");
        svg.Should().NotContain(">TmToolbar<");
    }

    [Theory]
    [MemberData(nameof(RenderCases))]
    public async Task StructureComponents_RenderBoundContentAndExpectedAffordances(
        string type,
        (string Key, object? Value)[] props,
        string[] expectedFragments)
    {
        var svg = await RenderAsync(type, props);

        using var _ = new AssertionScope(type);
        foreach (var fragment in expectedFragments)
            svg.Should().Contain(fragment);
        svg.Should().NotContain("Missing component");
        svg.Should().NotContainEquivalentOf("<script");
        svg.Should().NotContainEquivalentOf("<foreignObject");
    }

    [Theory]
    [MemberData(nameof(StructureTypeData))]
    public void StructureDefinitions_PreserveSchemaMetadata_AndComeFromTempoPack(string type)
    {
        var registry = Registry();
        var def = registry.GetDef(type);
        var schema = new BuiltInComponentSchemas().GetSchemas().Single(s => s.Type == type);

        using var _ = new AssertionScope(type);
        def.Should().NotBeNull();
        def!.PackId.Should().Be("tempo");
        def.NativeType.Should().BeNull();
        def.Category.Should().Be(schema.Category);
        def.DisplayName.Should().Be(schema.DisplayName);
        def.DefaultWidth.Should().Be(schema.DefaultWidth);
        def.DefaultHeight.Should().Be(schema.DefaultHeight);
        def.Props.Select(p => p.Name).Should().Equal(schema.Props.Select(p => p.Name));
        def.SizePresets.Should().BeEquivalentTo(schema.SizePresets);
    }

    [Fact]
    public void Registry_WithPack_CoversAllBuiltInSchemas()
    {
        var registry = Registry();
        var missing = new BuiltInComponentSchemas()
            .GetSchemas()
            .Select(schema => schema.Type)
            .Where(type => registry.GetDef(type) is null)
            .ToArray();

        missing.Should().BeEmpty();
    }

    private static void Add(
        TheoryData<string, (string Key, object? Value)[], string[]> data,
        string type,
        (string Key, object? Value)[] props,
        params string[] fragments)
        => data.Add(type, props, fragments);

    private static async Task<string> RenderAsync(
        string type,
        params (string Key, object? Value)[] props)
    {
        var def = Registry().GetDef(type) ?? throw new InvalidOperationException($"Missing definition for {type}.");
        var page = new WireframePage { Id = "page", Name = "Golden", Width = Math.Max(640, def.DefaultWidth + 80), Height = Math.Max(360, def.DefaultHeight + 80) };
        var element = new WireframeElement
        {
            Id = "sut",
            Type = type,
            X = 40,
            Y = 40,
            W = def.DefaultWidth,
            H = def.DefaultHeight
        };
        foreach (var (key, value) in props)
            element.SetProp(key, value);

        page.Elements.Add(element);
        return await Renderer().RenderPageAsync(page);
    }

    private static WireframeSvgRenderer Renderer()
        => new(Registry(), Services());

    private static WireframeComponentRegistry Registry()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());
        return registry;
    }

    private static IServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
