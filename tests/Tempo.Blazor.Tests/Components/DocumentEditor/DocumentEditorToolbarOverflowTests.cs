using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>bUnit tests for Phase 4: overflow model, More button, overflow menu, and keyboard navigation.</summary>
public class DocumentEditorToolbarOverflowTests : LocalizationTestBase
{
    // ─── 4.1 ToolbarItemPriority values ──────────────────────────────────────

    [Fact]
    public void ToolbarItemPriority_HasPrimarySecondaryAndOverflowOnlyValues()
    {
        var values = Enum.GetValues<ToolbarItemPriority>();

        values.Should().Contain(ToolbarItemPriority.Primary);
        values.Should().Contain(ToolbarItemPriority.Secondary);
        values.Should().Contain(ToolbarItemPriority.OverflowOnly);
    }

    // ─── 4.1 More button in DOM ───────────────────────────────────────────────

    [Fact]
    public void Toolbar_MoreButton_ExistsInDom()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        var btn = cut.Find("[data-testid='document-toolbar-more']");
        btn.Should().NotBeNull();
    }

    [Fact]
    public void Toolbar_MoreButton_IsHiddenByDefault()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        var btn = cut.Find("[data-testid='document-toolbar-more']");
        btn.HasAttribute("hidden").Should().BeTrue("More button must be hidden until JS signals overflow");
    }

    [Fact]
    public async Task Toolbar_MoreButton_IsVisibleWhenJsSignalsOverflow()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        await cut.InvokeAsync(() =>
            cut.Instance.SetOverflowingAsync(true, ["bold"]));

        var btn = cut.Find("[data-testid='document-toolbar-more']");
        btn.HasAttribute("hidden").Should().BeFalse("More button is shown once JS signals overflow");
    }

    [Fact]
    public async Task Toolbar_MoreButton_IsHiddenAgainWhenOverflowClears()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));

        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(false, []));

        var btn = cut.Find("[data-testid='document-toolbar-more']");
        btn.HasAttribute("hidden").Should().BeTrue();
    }

    // ─── 4.1 Overflow menu ────────────────────────────────────────────────────

    [Fact]
    public void Toolbar_OverflowMenu_IsNotRenderedByDefault()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        cut.FindAll("[data-testid='document-toolbar-more-menu']").Should().BeEmpty();
    }

    [Fact]
    public async Task Toolbar_MoreButton_Click_OpensOverflowMenu()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));

        cut.Find("[data-testid='document-toolbar-more']").Click();

        cut.FindAll("[data-testid='document-toolbar-more-menu']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Toolbar_MoreButton_SecondClick_ClosesOverflowMenu()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        cut.Find("[data-testid='document-toolbar-more']").Click();

        cut.FindAll("[data-testid='document-toolbar-more-menu']").Should().BeEmpty();
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_ShowsButtonForEachOverflowedCommand()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold", "italic"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        var menu = cut.Find("[data-testid='document-toolbar-more-menu']");
        menu.QuerySelectorAll("[data-command]").Should().HaveCount(2);
        menu.QuerySelector("[data-command='bold']").Should().NotBeNull();
        menu.QuerySelector("[data-command='italic']").Should().NotBeNull();
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_GroupsCommandsByToolbarGroup()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold", "insertTable", "save"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        var groups = cut.FindAll("[data-testid='document-toolbar-more-group']");

        groups.Select(group => group.GetAttribute("data-group"))
            .Should().Contain(["clipboard", "formatting", "insert"]);
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_SortsCommandsByPriorityBeforeGroupOrder()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["exportDocx", "bold", "viewDocumentJson", "save"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        var commands = cut.FindAll("[data-testid='document-toolbar-more-menu'] [data-command]")
            .Select(button => button.GetAttribute("data-command"))
            .ToList();

        commands.Should().Equal("save", "bold", "exportDocx", "viewDocumentJson");
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_ShowsSearchForLargeCommandSetsAndFilters()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true,
        [
            "save", "undo", "redo", "bold", "italic", "underline", "insertTable", "insertImage"
        ]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        cut.Find("[data-testid='document-toolbar-more-search']").Input("Bold");

        var commands = cut.FindAll("[data-testid='document-toolbar-more-menu'] [data-command]")
            .Select(button => button.GetAttribute("data-command"))
            .ToList();

        commands.Should().Equal("bold");
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_CommandButton_FiresOnBoldCallback()
    {
        var boldFired = false;
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.OnBold, () => { boldFired = true; }));
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        cut.Find("[data-testid='document-toolbar-more-menu'] [data-command='bold']").Click();

        boldFired.Should().BeTrue("clicking bold in overflow menu fires OnBold");
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_CommandButton_FiresOnUndoCallback()
    {
        var undoFired = false;
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.OnUndo, () => { undoFired = true; }));
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["undo"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        cut.Find("[data-testid='document-toolbar-more-menu'] [data-command='undo']").Click();

        undoFired.Should().BeTrue();
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_DisabledCommand_HasDisabledAttribute()
    {
        var registry = BuildRegistry(("bold", enabled: false, value: null));
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        var boldBtn = cut.Find("[data-testid='document-toolbar-more-menu'] [data-command='bold']");
        boldBtn.HasAttribute("disabled").Should().BeTrue("disabled registry state propagates to overflow menu");
    }

    [Fact]
    public async Task Toolbar_OverflowMenu_EnabledCommand_NotDisabled()
    {
        var registry = BuildRegistry(("bold", enabled: true, value: null));
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        var boldBtn = cut.Find("[data-testid='document-toolbar-more-menu'] [data-command='bold']");
        boldBtn.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Toolbar_MoreButton_AriaExpanded_ReflectsMenuState()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold"]));

        var btn = cut.Find("[data-testid='document-toolbar-more']");
        btn.GetAttribute("aria-expanded").Should().Be("false");

        btn.Click();
        btn.GetAttribute("aria-expanded").Should().Be("true");
    }

    // ─── 4.3 Ribbon tab keyboard navigation ──────────────────────────────────

    [Fact]
    public void RibbonTabs_HaveRovingTabindex_ActiveTabIsZero()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        var homeTab = cut.Find("[data-testid='document-ribbon-tab-home']");
        var insertTab = cut.Find("[data-testid='document-ribbon-tab-insert']");

        homeTab.GetAttribute("tabindex").Should().Be("0", "active tab has tabindex 0");
        insertTab.GetAttribute("tabindex").Should().Be("-1", "inactive tabs have tabindex -1");
    }

    [Fact]
    public void RibbonTabs_RovingTabindex_UpdatesWhenTabSelected()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-ribbon-tab-insert']").GetAttribute("tabindex").Should().Be("0");
        cut.Find("[data-testid='document-ribbon-tab-home']").GetAttribute("tabindex").Should().Be("-1");
    }

    // ─── 4.4 Ribbon button stable size ───────────────────────────────────────

    [Fact]
    public void RibbonButton_HasFixedSizeClass_InMarkup()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        var saveBtn = cut.Find("[data-testid='document-save']");
        saveBtn.ClassList.Should().Contain("tm-document-editor__ribbon-button",
            "ribbon buttons must carry the fixed-size CSS class");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DocumentEditorCommandRegistry BuildRegistry(params (string name, bool enabled, string? value)[] commands)
    {
        var registry = new DocumentEditorCommandRegistry();
        foreach (var (name, enabled, value) in commands)
        {
            var capturedEnabled = enabled;
            var capturedValue = value;
            registry.Register(new FuncDocumentEditorCommandEntry(
                name, affectsData: false,
                computeEnabled: _ => capturedEnabled,
                computeValue: _ => capturedValue,
                execute: (_, _) => Task.CompletedTask));
        }

        registry.RefreshAllAsync(new DocumentEditorCommandContext()).GetAwaiter().GetResult();
        return registry;
    }
}
