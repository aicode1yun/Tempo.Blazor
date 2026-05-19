using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class DocumentEditorToolbarDeclarativeMigrationTests : LocalizationTestBase
{
    [Fact]
    public void BuiltInToolbar_HomeTabMetadataCoversRenderedGroups()
    {
        AssertMetadata(DocumentToolbarTab.Home,
            "save", "undo", "redo",
            "bold", "italic", "underline", "fontFamily", "fontSize", "textColor", "highlightColor", "clearFormatting", "link",
            "alignLeft", "alignCenter", "alignRight", "alignJustify", "lineSpacing", "spacingBefore", "spacingAfter",
            "increaseIndent", "decreaseIndent");
    }

    [Fact]
    public void Toolbar_HomeTabRendersRegistryBackedCommands()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        AssertCommand(cut, "document-save", "save");
        AssertCommand(cut, "document-undo", "undo");
        AssertCommand(cut, "document-redo", "redo");
        AssertCommand(cut, "document-bold", "bold");
        AssertCommand(cut, "document-italic", "italic");
        AssertCommand(cut, "document-underline", "underline");
        AssertCommand(cut, "document-link", "link");
        AssertCommand(cut, "document-clear-formatting", "clearFormatting");
        AssertCommand(cut, "document-align-left", "alignLeft");
        AssertCommand(cut, "document-align-center", "alignCenter");
        AssertCommand(cut, "document-align-right", "alignRight");
        AssertCommand(cut, "document-align-justify", "alignJustify");
        AssertCommand(cut, "document-decrease-indent", "decreaseIndent");
        AssertCommand(cut, "document-increase-indent", "increaseIndent");

        cut.Find("[data-testid='document-font-family']").Should().NotBeNull();
        cut.Find("[data-testid='document-font-size']").Should().NotBeNull();
        cut.Find("[data-testid='document-font-color-trigger']").Should().NotBeNull();
        cut.Find("[data-testid='document-highlight-color-trigger']").Should().NotBeNull();
        cut.Find("[data-testid='document-line-spacing']").Should().NotBeNull();
        cut.Find("[data-testid='document-spacing-before']").Should().NotBeNull();
        cut.Find("[data-testid='document-spacing-after']").Should().NotBeNull();
    }

    [Fact]
    public void BuiltInToolbar_InsertReviewViewAndHeaderFooterMetadataCoversRenderedCommands()
    {
        AssertMetadata(DocumentToolbarTab.Insert, "insertTable", "insertImage", "insertPageBreak");
        AssertMetadata(DocumentToolbarTab.Review,
            "trackChanges", "reviewDisplayMode", "addComment", "openComments", "openRevisions",
            "compareDocuments", "protectDocument", "markEditableRegion");
        AssertMetadata(DocumentToolbarTab.View,
            "showRuler", "zoomPageWidth", "showBlocks", "fullscreen", "viewDocumentJson",
            "viewClipboardHtml", "exportPdf", "importDocx", "exportDocx", "openVersions");
        AssertMetadata(DocumentToolbarTab.HeaderFooter,
            "differentFirstPage", "differentOddEven", "closeHeaderFooter");
    }

    [Fact]
    public void Toolbar_InsertTabRendersRegistryBackedCommands()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        AssertCommand(cut, "document-toolbar-table", "insertTable");
        AssertCommand(cut, "document-toolbar-image", "insertImage");
        AssertCommand(cut, "document-insert-page-break", "insertPageBreak");
    }

    [Fact]
    public void Toolbar_ReviewTabRendersRegistryBackedCommands()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        AssertCommand(cut, "document-track-changes", "trackChanges");
        AssertCommand(cut, "document-add-comment", "addComment");
        AssertCommand(cut, "document-open-comments", "openComments");
        AssertCommand(cut, "document-open-revisions", "openRevisions");
        AssertCommand(cut, "document-compare-open", "compareDocuments");
        AssertCommand(cut, "document-protect-document", "protectDocument");
        AssertCommand(cut, "document-mark-editable-region", "markEditableRegion");
        var reviewMode = cut.Find("[data-testid='document-review-display-mode']");
        reviewMode.Should().NotBeNull();
        reviewMode.TextContent.Should().Contain("Original");
    }

    [Fact]
    public void Toolbar_ViewTabRendersRegistryBackedCommands()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ShowDebugTools, true)
            .Add(p => p.CanExportPdf, true)
            .Add(p => p.CanExportDocx, true));

        cut.Find("[data-testid='document-ribbon-tab-view']").Click();

        AssertCommand(cut, "document-toggle-ruler", "showRuler");
        AssertCommand(cut, "document-zoom-page-width", "zoomPageWidth");
        AssertCommand(cut, "document-show-blocks", "showBlocks");
        AssertCommand(cut, "document-fullscreen", "fullscreen");
        AssertCommand(cut, "document-view-json", "viewDocumentJson");
        AssertCommand(cut, "document-view-clipboard-html", "viewClipboardHtml");
        AssertCommand(cut, "document-open-versions", "openVersions");
    }

    [Fact]
    public void Toolbar_HeaderFooterContextualTabRendersOnlyInHeaderFooterMode()
    {
        var bodyCut = RenderComponent<TmDocumentEditorToolbar>();
        bodyCut.FindAll("[data-testid='document-ribbon-tab-header-footer']").Should().BeEmpty();

        var headerCut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ActiveRegion, "Header"));

        headerCut.Find("[data-testid='document-ribbon-tab-header-footer']")
            .GetAttribute("aria-selected").Should().Be("true");
        AssertCommand(headerCut, "document-header-footer-different-first-page", "differentFirstPage");
        AssertCommand(headerCut, "document-header-footer-different-odd-even", "differentOddEven");
        AssertCommand(headerCut, "document-close-header-footer", "closeHeaderFooter");
    }

    [Fact]
    public void BuiltInToolbar_HeaderFooterItemsRespectContextVisibility()
    {
        var items = DocumentEditorBuiltInToolbar.DefaultItems
            .Where(item => item.Tab == DocumentToolbarTab.HeaderFooter)
            .ToList();

        items.Should().OnlyContain(item =>
            !item.IsVisible(new DocumentToolbarVisibilityContext { IsHeaderFooterMode = false }));
        items.Should().OnlyContain(item =>
            item.IsVisible(new DocumentToolbarVisibilityContext { IsHeaderFooterMode = true }));
    }

    private static void AssertMetadata(DocumentToolbarTab tab, params string[] itemIds)
    {
        var metadata = DocumentEditorBuiltInToolbar.DefaultItems
            .Where(item => item.Tab == tab)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var itemId in itemIds)
        {
            metadata.Should().Contain(itemId);
        }
    }

    private static void AssertCommand(IRenderedComponent<TmDocumentEditorToolbar> cut, string testId, string commandName)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        element.GetAttribute("data-command").Should().Be(commandName);
        DocumentEditorBuiltInToolbar.FindItemByCommandName(commandName)
            .Should().NotBeNull($"'{commandName}' must be present in built-in toolbar metadata");
    }
}
