using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>bUnit tests for 3.3–3.5: toolbar buttons derive disabled/aria state from CommandRegistry.</summary>
public class DocumentEditorToolbarCommandStateTests : LocalizationTestBase
{
    // ─── 3.3 Save – registry-driven ──────────────────────────────────────────

    [Fact]
    public void HomeTab_SaveButton_ExistsWithCorrectTestId()
    {
        var registry = BuildRegistry(("save", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-save']").Should().NotBeNull();
    }

    [Fact]
    public void HomeTab_SaveButton_IsDisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("save", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_SaveButton_IsEnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("save", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.3 Undo/Redo – registry-driven ────────────────────────────────────

    [Fact]
    public void HomeTab_UndoButton_ExistsWithCorrectTestId()
    {
        var registry = BuildRegistry(("undo", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-undo']").Should().NotBeNull();
    }

    [Fact]
    public void HomeTab_UndoButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("undo", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-undo']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_UndoButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("undo", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-undo']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void HomeTab_RedoButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("redo", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-redo']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.3 Bold/Italic/Underline – registry-driven ─────────────────────────

    [Fact]
    public void HomeTab_BoldButton_IsDisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("bold", enabled: false, value: "inactive"));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-bold']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_BoldButton_IsEnabledAndAriaFalseWhenInactive()
    {
        var registry = BuildRegistry(("bold", enabled: true, value: "inactive"));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        var bold = cut.Find("[data-testid='document-bold']");
        bold.HasAttribute("disabled").Should().BeFalse();
        bold.GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void HomeTab_BoldButton_AriaIsTrueWhenActive()
    {
        var registry = BuildRegistry(("bold", enabled: true, value: "active"));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-bold']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void HomeTab_BoldButton_AriaIsMixedWhenMixed()
    {
        var registry = BuildRegistry(("bold", enabled: true, value: "mixed"));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-bold']").GetAttribute("aria-pressed").Should().Be("mixed");
    }

    [Fact]
    public void HomeTab_ItalicButton_AriaIsTrueWhenActive()
    {
        var registry = BuildRegistry(("italic", enabled: true, value: "active"));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-italic']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void HomeTab_UnderlineButton_AriaIsTrueWhenActive()
    {
        var registry = BuildRegistry(("underline", enabled: true, value: "active"));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-underline']").GetAttribute("aria-pressed").Should().Be("true");
    }

    // ─── 3.4 Insert tab – registry-driven ────────────────────────────────────

    [Fact]
    public void InsertTab_TableButton_ExistsWithCorrectTestId()
    {
        var registry = BuildRegistry(("insertTable", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-toolbar-table']").Should().NotBeNull();
    }

    [Fact]
    public void InsertTab_TableButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("insertTable", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-toolbar-table']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void InsertTab_TableButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("insertTable", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-toolbar-table']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void InsertTab_ImageButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("insertImage", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry)
            .Add(x => x.CanBrowseImageAssets, true));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-toolbar-image']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void InsertTab_ImageSplitMenu_ShowsUrlUploadAndAssetChoices()
    {
        var registry = BuildRegistry(("insertImage", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry)
            .Add(x => x.CanBrowseImageAssets, true));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();
        cut.Find("[data-testid='document-toolbar-image']").Click();

        cut.Find("[data-testid='document-image-insert-url']").Should().NotBeNull();
        cut.Find("[data-testid='document-image-insert-upload']").Should().NotBeNull();
        cut.Find("[data-testid='document-image-insert-asset']").Should().NotBeNull();
    }

    [Fact]
    public void InsertTab_ImageUploadChoice_DisabledWithoutProvider()
    {
        var registry = BuildRegistry(("insertImage", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry)
            .Add(x => x.CanUploadImages, false));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();
        cut.Find("[data-testid='document-toolbar-image']").Click();

        cut.Find("[data-testid='document-image-insert-upload']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void InsertTab_ImageAssetChoice_HiddenWithoutAssetCapability()
    {
        var registry = BuildRegistry(("insertImage", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry)
            .Add(x => x.CanBrowseImageAssets, false));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();
        cut.Find("[data-testid='document-toolbar-image']").Click();

        cut.FindAll("[data-testid='document-image-insert-asset']").Should().BeEmpty();
    }

    [Fact]
    public void InsertTab_ImageUrlChoice_FiresUrlCallback()
    {
        var registry = BuildRegistry(("insertImage", enabled: true, value: null));
        var called = false;

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry)
            .Add(x => x.OnInsertImageUrl, () => called = true));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();
        cut.Find("[data-testid='document-toolbar-image']").Click();
        cut.Find("[data-testid='document-image-insert-url']").Click();

        called.Should().BeTrue();
    }

    // ─── 3.5 Review tab – TrackChanges registry-driven ───────────────────────

    [Fact]
    public void ReviewTab_TrackChangesButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("trackChanges", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-track-changes']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ReviewTab_TrackChangesButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("trackChanges", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-track-changes']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.3 Font selectors – registry-driven ────────────────────────────────

    [Fact]
    public void HomeTab_FontFamilySelect_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("fontFamily", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-font-family']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_FontFamilySelect_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("fontFamily", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-font-family']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void HomeTab_FontSizeSelect_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("fontSize", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-font-size']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_ClearFormattingButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("clearFormatting", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-clear-formatting']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void HomeTab_ClearFormattingButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("clearFormatting", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-clear-formatting']").HasAttribute("disabled").Should().BeTrue();
    }

    // ─── 3.3 Alignment buttons – registry-driven ─────────────────────────────

    [Fact]
    public void HomeTab_AlignLeftButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("paragraphAlignment", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-align-left']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_AlignCenterButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("paragraphAlignment", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-align-center']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void HomeTab_AlignRightButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("paragraphAlignment", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-align-right']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_AlignJustifyButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("paragraphAlignment", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-align-justify']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.3 Link button – registry-driven ───────────────────────────────────

    [Fact]
    public void HomeTab_LinkButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("link", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-link']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void HomeTab_LinkButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("link", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        cut.Find("[data-testid='document-link']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.4 Insert menu – registry-driven ───────────────────────────────────

    [Fact]
    public void InsertTab_InsertMenuButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("insertMenu", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-insert-menu']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void InsertTab_InsertMenuButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("insertMenu", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-insert-menu']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.5 ReviewDisplayMode – registry-driven ─────────────────────────────

    [Fact]
    public void ReviewTab_ReviewDisplayModeSelect_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("reviewDisplayMode", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-review-display-mode']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ReviewTab_ReviewDisplayModeSelect_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("reviewDisplayMode", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-review-display-mode']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.5 AddComment / Compare – registry-driven ──────────────────────────

    [Fact]
    public void ReviewTab_AddCommentButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("addComment", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-add-comment']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ReviewTab_AddCommentButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("addComment", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-add-comment']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void ReviewTab_CompareButton_DisabledWhenRegistrySaysDisabled()
    {
        var registry = BuildRegistry(("compareDocuments", enabled: false, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-compare-open']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ReviewTab_CompareButton_EnabledWhenRegistrySaysEnabled()
    {
        var registry = BuildRegistry(("compareDocuments", enabled: true, value: null));

        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry)
            .Add(x => x.CanCompareDocuments, true));
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();

        cut.Find("[data-testid='document-compare-open']").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── 3.5 Ruler/Zoom/PageWidth – behavioral regression ────────────────────

    [Fact]
    public void ViewTab_RulerToggle_ExistsAndRespondsToClick()
    {
        var showRuler = true;
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.ShowRuler, showRuler)
            .Add(x => x.ShowRulerChanged, v => showRuler = v));
        cut.Find("[data-testid='document-ribbon-tab-view']").Click();

        cut.Find("[data-testid='document-toggle-ruler']").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("[data-testid='document-toggle-ruler']").Click();

        showRuler.Should().BeFalse();
    }

    [Fact]
    public void ViewTab_ZoomButtons_ExistAndFireCallbacks()
    {
        var zoomPercent = 100;
        var pageWidthRequested = false;
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.ZoomPercent, zoomPercent)
            .Add(x => x.ZoomPercentChanged, v => zoomPercent = v)
            .Add(x => x.OnZoomPageWidth, () => pageWidthRequested = true));
        cut.Find("[data-testid='document-ribbon-tab-view']").Click();

        cut.Find("[data-testid='document-zoom-in']").Click();
        cut.Find("[data-testid='document-zoom-page-width']").Click();

        zoomPercent.Should().Be(110);
        pageWidthRequested.Should().BeTrue();
    }

    // ─── Fallback: no CommandRegistry ─────────────────────────────────────────

    [Fact]
    public void HomeTab_SaveButton_FallsBackToParametersWhenNoRegistry()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.ReadOnly, false)
            .Add(x => x.IsSaving, false));

        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void HomeTab_SaveButton_FallsBackDisabledWhenReadOnlyAndNoRegistry()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(p => p
            .Add(x => x.ReadOnly, true));

        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue();
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static DocumentEditorCommandRegistry BuildRegistry(
        params (string name, bool enabled, string? value)[] commands)
    {
        var registry = new DocumentEditorCommandRegistry();
        foreach (var (name, enabled, value) in commands)
        {
            bool capturedEnabled = enabled;
            string? capturedValue = value;
            registry.Register(new FuncDocumentEditorCommandEntry(
                name, affectsData: true,
                computeEnabled: _ => capturedEnabled,
                computeValue: _ => capturedValue,
                execute: (_, _) => Task.CompletedTask));
        }

        var ctx = new DocumentEditorCommandContext();
        registry.RefreshAllAsync(ctx).GetAwaiter().GetResult();
        return registry;
    }
}
