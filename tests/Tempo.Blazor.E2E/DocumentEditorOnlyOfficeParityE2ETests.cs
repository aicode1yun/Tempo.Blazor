using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>ONLYOFFICE-level RED baseline tests for selection, formatting, revisions, comments, and undo.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:HumanWorkflow")]
[TestCategory("DocumentEditor:LayoutVisual")]
[DoNotParallelize]
public sealed class DocumentEditorOnlyOfficeParityE2ETests : DocumentEditorE2ETestBase
{
    private const string DocumentId = "onlyoffice-parity-2026-05-24";
    private const string FormattingBlockId = "onlyoffice-formatting-paragraph";
    private const string FormattingPhrase = "exact target phrase";
    private const string FloatingPositionBlockId = FormattingBlockId;
    private const string FloatingPositionPhrase = FormattingPhrase;
    private const string MixedFormattingBlockId = "onlyoffice-mixed-formatting-paragraph";
    private const string MixedFormattingPhrase = "Bold mixed segment and plain mixed segment";
    private const string CollapsedCaretBlockId = "onlyoffice-collapsed-caret-paragraph";
    private const string TrackChangesBlockId = "onlyoffice-track-changes-paragraph";
    private const string CommentBoundaryBlockId = "onlyoffice-comment-boundary-paragraph";
    private const string CommentBoundaryPhrase = "commented range";

    [TestMethod]
    public async Task OnlyOfficeParity_SeedContainsAllPhase0Scenarios()
    {
        using var response = await LoadOnlyOfficeParityDocumentFromApiAsync();
        var snapshot = GetString(response.RootElement, "JsonSnapshot");
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot), "ONLYOFFICE parity API response must include JsonSnapshot.");

        using var documentJson = JsonDocument.Parse(snapshot!);
        var document = documentJson.RootElement;
        var blocks = GetArray(document, "Blocks").ToArray();
        var comments = GetArray(document, "Comments").ToArray();
        var revisions = GetArray(document, "Revisions").ToArray();

        AssertHasBlock(blocks, "onlyoffice-formatting-paragraph");
        AssertHasBlock(blocks, "onlyoffice-mixed-formatting-paragraph");
        AssertHasBlock(blocks, "onlyoffice-collapsed-caret-paragraph");
        AssertHasBlock(blocks, "onlyoffice-track-changes-paragraph");
        AssertHasBlock(blocks, "onlyoffice-comment-boundary-paragraph");
        AssertHasBlock(blocks, "recovery-comment-paragraph");
        AssertHasBlock(blocks, "recovery-insertion-revision-paragraph");
        AssertHasBlock(blocks, "recovery-deletion-revision-paragraph");
        AssertHasBlock(blocks, "recovery-table-under-images");

        Assert.IsTrue(comments.Any(comment => GetString(comment, "Id") == "onlyoffice-comment-boundary"),
            "Parity seed must contain a comment boundary scenario.");
        Assert.IsTrue(revisions.Any(revision => GetString(revision, "Id") == "recovery-revision-insertion"),
            "Parity seed must retain an existing insertion revision scenario.");
        Assert.IsTrue(revisions.Any(revision => GetString(revision, "Id") == "recovery-revision-deletion"),
            "Parity seed must retain an existing deletion revision scenario.");
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await ClickRibbonCommandAsync(page, "document-bold", selection);

            var target = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsAreBold(target);
            await AssertSelectionStillEqualsAsync(page, selection);
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });
            AssertToolbarStateMatchesTextStyles(await ReadRibbonFormattingStateAsync(page), target);
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            AssertTextRunsAreBold(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase));
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-bold", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonBold_UndoRedoRestoresFormattingAndToolbarState()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await ClickRibbonCommandAsync(page, "document-bold", selection);
            AssertTextRunsAreBold(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase));
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-undo", selection);
            AssertTextRunsAreNormalWeight(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase));
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "false", new() { Timeout = 5000 });
            var undoStateAfterUndo = await ReadUndoStateDebugAsync(page);
            Assert.IsTrue(undoStateAfterUndo.CanRedo,
                $"Runtime redo stack must be available after undo. State: {undoStateAfterUndo.Debug}");
            await Assertions.Expect(page.GetByTestId("document-redo")).ToBeEnabledAsync(new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);

            await ClickRibbonCommandAsync(page, "document-redo", selection);
            AssertTextRunsAreBold(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase));
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonBold_UndoRedoRestoresFormattingAndToolbarState));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-bold-undo-redo", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonFontSize_AppliesToSelectionAndSynchronizesVisibleState()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await ChooseRibbonSelectOptionAsync(page, "document-font-size", "28", selection);

            var target = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsFontSizeNearPt(target, 28);
            await AssertSelectionStillEqualsAsync(page, selection);
            await Assertions.Expect(page.GetByTestId("document-font-size")).ToHaveValueAsync("28", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-undo", selection);
            var afterUndo = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            Assert.IsFalse(afterUndo.TargetStyles.All(style => Math.Abs(style.FontSizePt - 28) <= 1.75),
                $"Undo must remove the 28pt font-size command. Debug: {afterUndo.Debug}");
            await Assertions.Expect(page.GetByTestId("document-redo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-redo", selection);
            AssertTextRunsFontSizeNearPt(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), 28);
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            AssertTextRunsFontSizeNearPt(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), 28);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonFontSize_AppliesToSelectionAndSynchronizesVisibleState));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-font-size", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonTextColor_AppliesToSelectionAndUpdatesSwatch()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string blue = "#2563eb";

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await OpenRibbonColorPickerAsync(page, "document-font-color-trigger", selection);
            await EnterColorHexAsync(page, blue);

            var target = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsColorEquals(target, blue);
            await AssertSelectionStillEqualsAsync(page, selection);
            Assert.AreEqual(blue, (await ReadRibbonFormattingStateAsync(page)).TextColor, "Ribbon font color trigger must show the actual current color.");
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-undo", selection);
            var afterUndo = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            Assert.IsFalse(afterUndo.TargetStyles.All(style => CssColorMatches(style.ColorHex, blue) || CssColorMatches(style.Color, blue)),
                $"Undo must remove the ribbon text color command. Debug: {afterUndo.Debug}");
            await Assertions.Expect(page.GetByTestId("document-redo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-redo", selection);
            AssertTextRunsColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), blue);
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            AssertTextRunsColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), blue);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonTextColor_AppliesToSelectionAndUpdatesSwatch));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-text-color", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonHighlight_AppliesToSelectionAndUpdatesSwatch()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string yellow = "#fde68a";

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await OpenRibbonColorPickerAsync(page, "document-highlight-color-trigger", selection);
            await EnterColorHexAsync(page, yellow);

            var target = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsBackgroundColorEquals(target, yellow);
            await AssertSelectionStillEqualsAsync(page, selection);
            Assert.AreEqual(yellow, (await ReadRibbonFormattingStateAsync(page)).HighlightColor, "Ribbon highlight trigger must show the actual current highlight.");
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-undo", selection);
            var afterUndo = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            Assert.IsFalse(afterUndo.TargetStyles.All(style => CssColorMatches(style.BackgroundColorHex, yellow) || CssColorMatches(style.BackgroundColor, yellow)),
                $"Undo must remove the ribbon highlight command. Debug: {afterUndo.Debug}");
            await Assertions.Expect(page.GetByTestId("document-redo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-redo", selection);
            AssertTextRunsBackgroundColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), yellow);
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            AssertTextRunsBackgroundColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), yellow);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonHighlight_AppliesToSelectionAndUpdatesSwatch));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-highlight", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonHighlightClear_RemovesHighlightAndKeepsSelection()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string yellow = "#fde68a";

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await OpenRibbonColorPickerAsync(page, "document-highlight-color-trigger", selection);
            await EnterColorHexAsync(page, yellow);
            AssertTextRunsBackgroundColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), yellow);

            await OpenRibbonColorPickerAsync(page, "document-highlight-color-trigger", selection);
            await ClearOpenColorPickerAsync(page);

            var cleared = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            Assert.IsTrue(cleared.TargetStyles.All(style => string.IsNullOrWhiteSpace(style.BackgroundColorHex)),
                $"Clearing highlight must remove the target background color. Debug: {cleared.Debug}");
            Assert.AreNotEqual(yellow, (await ReadRibbonFormattingStateAsync(page)).HighlightColor, "Ribbon highlight trigger must not keep the cleared color.");
            await AssertSelectionStillEqualsAsync(page, selection);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonHighlightClear_RemovesHighlightAndKeepsSelection));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-highlight-clear", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_FloatingToolbar_FormatsSelectionAndSynchronizesRibbon()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });
            await ClickFloatingToolbarCommandAsync(page, "document-mini-bold", selection);

            var target = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsAreBold(target);
            await Assertions.Expect(page.GetByTestId("document-mini-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);
            await AssertRibbonAndFloatingStateEqualAsync(page);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_FloatingToolbar_FormatsSelectionAndSynchronizesRibbon));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "floating-toolbar-bold", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_FloatingToolbar_OnlyAppearsForRealTextSelection()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var toolbar = page.GetByTestId("document-mini-toolbar");

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 8);
            await Assertions.Expect(toolbar).ToBeHiddenAsync(new() { Timeout = 5000 });

            var mouseSelection = await SelectTextByMouseAsync(page, FloatingPositionBlockId, FloatingPositionPhrase);
            await Assertions.Expect(toolbar).ToBeVisibleAsync(new() { Timeout = 5000 });
            await ExpectToolbarNearSelectionAsync(toolbar, mouseSelection.Rect);

            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 10);
            await Assertions.Expect(toolbar).ToBeHiddenAsync(new() { Timeout = 5000 });

            var keyboardSelection = await SelectTextByKeyboardAsync(page, FloatingPositionBlockId, FloatingPositionPhrase);
            await Assertions.Expect(toolbar).ToBeVisibleAsync(new() { Timeout = 5000 });
            await ExpectToolbarNearSelectionAsync(toolbar, keyboardSelection.Rect);
            await AssertRibbonAndFloatingStateEqualAsync(page);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_FloatingToolbar_OnlyAppearsForRealTextSelection));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "floating-toolbar-real-selection-only", FloatingPositionBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_FloatingToolbar_StaysNearSelectionAndAvoidsChrome()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var toolbar = page.GetByTestId("document-mini-toolbar");

        try
        {
            var selection = await SelectTextByMouseAsync(page, FloatingPositionBlockId, FloatingPositionPhrase);
            await Assertions.Expect(toolbar).ToBeVisibleAsync(new() { Timeout = 5000 });
            await ExpectToolbarNearSelectionAsync(toolbar, selection.Rect);
            await ExpectToolbarAvoidsRibbonAsync(page, toolbar);
            await ExpectRectInsideViewportAsync(page, toolbar, "floating toolbar");

            var scroll = await ScrollDocumentViewportAsync(page, 160);
            if (Math.Round(scroll.DeltaY) == 0)
            {
                scroll = await ScrollDocumentViewportAsync(page, -160);
            }
            Assert.AreNotEqual(0, Math.Round(scroll.DeltaY), $"Document editor test viewport must be scrollable to validate bubble repositioning. Debug: {scroll.Debug}");
            await WaitForEditorStableAsync(page, "floating toolbar scroll reposition", FloatingPositionBlockId, FloatingPositionPhrase);
            var scrolledSelection = await ReadDocumentEditorSelectionSnapshotAsync(page);
            await ExpectToolbarNearSelectionAsync(toolbar, scrolledSelection.Rect);
            await ExpectRectInsideViewportAsync(page, toolbar, "floating toolbar after scroll");

            await page.SetViewportSizeAsync(1280, 720);
            await WaitForEditorStableAsync(page, "floating toolbar resize reposition", FloatingPositionBlockId, FloatingPositionPhrase);
            var resizedSelection = await ReadDocumentEditorSelectionSnapshotAsync(page);
            await ExpectToolbarNearSelectionAsync(toolbar, resizedSelection.Rect);
            await ExpectToolbarAvoidsRibbonAsync(page, toolbar);
            await ExpectRectInsideViewportAsync(page, toolbar, "floating toolbar after resize");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_FloatingToolbar_StaysNearSelectionAndAvoidsChrome));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "floating-toolbar-position", FloatingPositionBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_FloatingToolbar_FontSizeColorHighlightUndoAndReloadMatchRibbon()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string blue = "#2563eb";
        const string yellow = "#fde68a";

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });

            await ChooseFloatingSelectOptionAsync(page, "document-mini-font-size", "32", selection);
            AssertTextRunsFontSizeNearPt(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), 32);
            await Assertions.Expect(page.GetByTestId("document-font-size")).ToHaveValueAsync("32", new() { Timeout = 5000 });
            await AssertRibbonAndFloatingStateEqualAsync(page);

            await OpenFloatingColorPickerAsync(page, "document-mini-text-color", selection);
            await EnterColorHexAsync(page, blue);
            AssertTextRunsColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), blue);
            Assert.AreEqual(blue, (await ReadRibbonFormattingStateAsync(page)).TextColor, "Ribbon text color must update immediately after a floating toolbar command.");
            await AssertRibbonAndFloatingStateEqualAsync(page);

            await OpenFloatingColorPickerAsync(page, "document-mini-highlight", selection);
            await EnterColorHexAsync(page, yellow);
            AssertTextRunsBackgroundColorEquals(await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase), yellow);
            Assert.AreEqual(yellow, (await ReadRibbonFormattingStateAsync(page)).HighlightColor, "Ribbon highlight must update immediately after a floating toolbar command.");
            await AssertRibbonAndFloatingStateEqualAsync(page);
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-undo", selection);
            var afterUndo = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            Assert.IsFalse(afterUndo.TargetStyles.All(style => string.Equals(style.BackgroundColorHex, yellow, StringComparison.OrdinalIgnoreCase)),
                $"Undo must remove the last floating toolbar highlight operation. Debug: {afterUndo.Debug}");

            selection = await SelectTextByKeyboardAsync(page, FormattingBlockId, FormattingPhrase);
            await OpenFloatingColorPickerAsync(page, "document-mini-highlight", selection);
            await EnterColorHexAsync(page, yellow);
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);

            var reloaded = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsFontSizeNearPt(reloaded, 32);
            AssertTextRunsColorEquals(reloaded, blue);
            AssertTextRunsBackgroundColorEquals(reloaded, yellow);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_FloatingToolbar_FontSizeColorHighlightUndoAndReloadMatchRibbon));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "floating-toolbar-font-size-color-highlight", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_MixedSelection_ShowsMixedStateInRibbonAndFloatingToolbar()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            await SelectTextByMouseAsync(page, MixedFormattingBlockId, MixedFormattingPhrase);
            await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });

            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "mixed", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-mini-bold")).ToHaveAttributeAsync("aria-pressed", "mixed", new() { Timeout = 5000 });

            var ribbon = await ReadRibbonFormattingStateAsync(page);
            var floating = await ReadFloatingFormattingStateAsync(page);
            Assert.IsTrue(ribbon.BoldMixed, $"Ribbon must expose mixed bold state. Ribbon={ribbon.Debug}");
            Assert.IsTrue(floating.BoldMixed, $"Floating toolbar must expose mixed bold state. Floating={floating.Debug}");
            await AssertRibbonAndFloatingStateEqualAsync(page);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_MixedSelection_ShowsMixedStateInRibbonAndFloatingToolbar));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "mixed-selection-toolbar-state", MixedFormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_CaretMove_UpdatesToolbarStateFromRuntimeSelection()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, MixedFormattingBlockId, 4);
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 8);
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "false", new() { Timeout = 5000 });

            var ribbon = await ReadRibbonFormattingStateAsync(page);
            Assert.IsFalse(ribbon.Bold, $"Ribbon bold state must follow the plain caret location. Ribbon={ribbon.Debug}");
            Assert.IsFalse(ribbon.BoldMixed, $"Plain caret must not keep mixed bold state. Ribbon={ribbon.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_CaretMove_UpdatesToolbarStateFromRuntimeSelection));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "caret-runtime-toolbar-state", CollapsedCaretBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonFontSizePointerMiss_DoesNotDestroySelection()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            var point = await ReadPointJustBesideElementAsync(page, "[data-testid='document-font-size']");
            await page.Mouse.ClickAsync((float)point.X, (float)point.Y);
            await WaitForEditorStableAsync(page, "font-size pointer miss", selection.StartBlockId, selection.SelectedText);

            await AssertSelectionStillEqualsAsync(page, selection);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonFontSizePointerMiss_DoesNotDestroySelection));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "font-size-pointer-miss", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonInlineToggleCommands_ApplyTogglePreserveSelectionAndUndo()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await ClickRibbonCommandAsync(page, "document-bold", selection);
            var boldTarget = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsAreBold(boldTarget);
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeEnabledAsync(new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);

            await ClickRibbonCommandAsync(page, "document-bold", selection);
            var unboldTarget = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsAreNormalWeight(unboldTarget);
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "false", new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);

            await ClickRibbonCommandAsync(page, "document-italic", selection);
            var italicTarget = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsFontStyle(italicTarget, "italic");
            await Assertions.Expect(page.GetByTestId("document-italic")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-underline", selection);
            var underlineTarget = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsTextDecorationContains(underlineTarget, "underline");
            await Assertions.Expect(page.GetByTestId("document-underline")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

            await ClickRibbonCommandAsync(page, "document-strikethrough", selection);
            var strikeTarget = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsTextDecorationContains(strikeTarget, "line-through");
            await Assertions.Expect(page.GetByTestId("document-strikethrough")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            AssertSurroundingRunsDoNotHaveInlineDecorations(strikeTarget);
            await AssertSelectionStillEqualsAsync(page, selection);

            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            var reloaded = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsFontStyle(reloaded, "italic");
            AssertTextRunsTextDecorationContains(reloaded, "underline");
            AssertTextRunsTextDecorationContains(reloaded, "line-through");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonInlineToggleCommands_ApplyTogglePreserveSelectionAndUndo));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-inline-toggle-commands", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonFontFamilyAndSize_ApplyOnlyToSelectionAndPersistAfterReload()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string georgia = "Georgia, \"Times New Roman\", serif";

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await ChooseRibbonSelectOptionAsync(page, "document-font-family", georgia, selection);
            await ChooseRibbonSelectOptionAsync(page, "document-font-size", "28", selection);

            var target = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsFontFamilyContains(target, "Georgia");
            AssertTextRunsFontSizeNearPt(target, 28);
            AssertSurroundingRunsDoNotUseFontFamilyOrSize(target, "Georgia", 28);
            await AssertSelectionStillEqualsAsync(page, selection);
            await Assertions.Expect(page.GetByTestId("document-font-family")).ToHaveValueAsync(georgia, new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-font-size")).ToHaveValueAsync("28", new() { Timeout = 5000 });

            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);

            var reloaded = await ReadTextRunComputedStylesAsync(page, FormattingBlockId, FormattingPhrase);
            AssertTextRunsFontFamilyContains(reloaded, "Georgia");
            AssertTextRunsFontSizeNearPt(reloaded, 28);
            await Assertions.Expect(page.GetByTestId("document-undo")).ToBeDisabledAsync(new() { Timeout = 5000 });
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonFontFamilyAndSize_ApplyOnlyToSelectionAndPersistAfterReload));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-font-family-size-selection", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonCollapsedCaretFormatting_AffectsNextTypedText()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "F6Pending";
        const string georgia = "Georgia, \"Times New Roman\", serif";
        const string blue = "#2563eb";
        const string yellow = "#fde68a";

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 10);
            await ChooseRibbonSelectOptionAsync(page, "document-font-family", georgia);
            await ChooseRibbonSelectOptionAsync(page, "document-font-size", "28");
            await OpenRibbonColorPickerAsync(page, "document-font-color-trigger");
            await EnterColorHexAsync(page, blue);
            await OpenRibbonColorPickerAsync(page, "document-highlight-color-trigger");
            await EnterColorHexAsync(page, yellow);
            await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "collapsed caret pending formatting typing", CollapsedCaretBlockId, typed);

            var target = await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed);
            AssertTextRunsFontFamilyContains(target, "Georgia");
            AssertTextRunsFontSizeNearPt(target, 28);
            AssertTextRunsColorEquals(target, blue);
            AssertTextRunsBackgroundColorEquals(target, yellow);
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonCollapsedCaretFormatting_AffectsNextTypedText));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-collapsed-caret-pending-formatting", CollapsedCaretBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_TypingSessionUndo_RemovesWholeSessionAndEnablesRedo()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "phase11 jak se mas";

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 0);
            await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
            await WaitForUndoEnabledAsync(page);

            var afterTyping = await ReadDocumentEditorBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsTrue(afterTyping.Contains(typed, StringComparison.Ordinal),
                $"Typed session should be inserted in normal order. Block text: {afterTyping}");
            var modelAfterTyping = await ReadDocumentEditorModelBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsTrue(modelAfterTyping.Contains(typed, StringComparison.Ordinal),
                $"Typed session must also be present in the JS-owned document model. Model text: {modelAfterTyping}");

            await ClickRibbonCommandAsync(page, "document-undo", expectedSelection: null, requireRuntimeSelectionToken: false);
            await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).Not.ToContainTextAsync(typed, new() { Timeout = 5000 });
            await WaitForRedoEnabledAsync(page);

            var afterUndo = await ReadDocumentEditorBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsFalse(afterUndo.Contains("phase11", StringComparison.Ordinal),
                $"Undo must remove the whole typing session, not only the last character. Block text: {afterUndo}");
            var modelAfterUndo = await ReadDocumentEditorModelBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsFalse(modelAfterUndo.Contains(typed, StringComparison.Ordinal),
                $"Undo through the ribbon must remove the typed session from the model. Model text: {modelAfterUndo}");
            var undoSelection = await ReadDocumentEditorSelectionSnapshotAsync(page);
            Assert.IsTrue(undoSelection.IsCollapsed,
                $"Undo after a typing session should leave a collapsed caret, not an arbitrary range. Selection: {undoSelection.Debug}");

            await page.Keyboard.PressAsync("Control+Y");
            await WaitForEditorStableAsync(page, "keyboard redo typing session", CollapsedCaretBlockId, typed);
            var afterKeyboardRedo = await ReadDocumentEditorBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsTrue(afterKeyboardRedo.Contains(typed, StringComparison.Ordinal),
                $"Ctrl+Y must redo the whole typing session in the visible DOM. Block text: {afterKeyboardRedo}");
            var modelAfterKeyboardRedo = await ReadDocumentEditorModelBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsTrue(modelAfterKeyboardRedo.Contains(typed, StringComparison.Ordinal),
                $"Ctrl+Y must redo the whole typing session in the model. Model text: {modelAfterKeyboardRedo}");

            await page.Keyboard.PressAsync("Control+Z");
            await WaitForEditorStableAsync(page, "keyboard undo typing session", CollapsedCaretBlockId);
            var afterKeyboardUndo = await ReadDocumentEditorBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsFalse(afterKeyboardUndo.Contains(typed, StringComparison.Ordinal),
                $"Ctrl+Z must undo the redone typing session in the visible DOM. Block text: {afterKeyboardUndo}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_TypingSessionUndo_RemovesWholeSessionAndEnablesRedo));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "typing-session-undo", CollapsedCaretBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_TrackChangesTyping_PreservesOrderAndCoalescesInsertion()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "jak se mas";

        try
        {
            await EnableTrackChangesAsync(page);
            await ClickDocumentEditorBlockOffsetAsync(page, TrackChangesBlockId, 0);
            await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "track changes typing", TrackChangesBlockId, typed);

            var blockText = await ReadDocumentEditorBlockTextAsync(page, TrackChangesBlockId);
            Assert.IsTrue(blockText.StartsWith(typed, StringComparison.Ordinal),
                $"Typing with track changes must preserve character order. Expected prefix '{typed}', got '{blockText}'.");

            var revision = await ReadInsertionRevisionProbeAsync(page, TrackChangesBlockId);
            Assert.IsTrue(revision.Text.Contains(typed, StringComparison.Ordinal),
                $"Inserted text must be visible as insertion markup. Revision text was '{revision.Text}'. Debug: {revision.Debug}");
            Assert.IsTrue(revision.FragmentCount <= 2,
                $"Track changes must coalesce typing into a logical insertion, not one fragment per character. FragmentCount={revision.FragmentCount}. Debug: {revision.Debug}");
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            var reloadedRevision = await ReadInsertionRevisionProbeAsync(page, TrackChangesBlockId);
            Assert.IsTrue(reloadedRevision.Text.Contains(typed, StringComparison.Ordinal),
                $"Track insertion must survive save/reload. Revision text was '{reloadedRevision.Text}'. Debug: {reloadedRevision.Debug}");
            Assert.IsTrue(reloadedRevision.FragmentCount <= 2,
                $"Reloaded track insertion must stay coalesced. FragmentCount={reloadedRevision.FragmentCount}. Debug: {reloadedRevision.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_TrackChangesTyping_PreservesOrderAndCoalescesInsertion));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "track-changes-typing", TrackChangesBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_TrackChangesDeletion_PersistsAfterSaveReload()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var deleted = $"phase16-delete-{DateTimeOffset.UtcNow:HHmmssfff}";

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, TrackChangesBlockId, 0);
            await page.Keyboard.TypeAsync($"{deleted} ", new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "track deletion seed text", TrackChangesBlockId, deleted);

            await EnableTrackChangesAsync(page);
            await SelectTextByMouseAsync(page, TrackChangesBlockId, deleted);
            await page.Keyboard.PressAsync("Backspace");
            await WaitForEditorStableAsync(page, "track deletion markup", TrackChangesBlockId, deleted);

            var deletion = await ReadDeletionRevisionProbeAsync(page, TrackChangesBlockId);
            Assert.IsTrue(deletion.Text.Contains(deleted, StringComparison.Ordinal),
                $"Deleted text must stay visible as deletion markup before review. Revision text was '{deletion.Text}'. Debug: {deletion.Debug}");
            Assert.IsTrue(deletion.TextDecorationLine.Contains("line-through", StringComparison.OrdinalIgnoreCase),
                $"Deletion markup must render as struck-through text. Debug: {deletion.Debug}");

            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            var reloadedDeletion = await ReadDeletionRevisionProbeAsync(page, TrackChangesBlockId);
            Assert.IsTrue(reloadedDeletion.Text.Contains(deleted, StringComparison.Ordinal),
                $"Track deletion must survive save/reload. Revision text was '{reloadedDeletion.Text}'. Debug: {reloadedDeletion.Debug}");
            Assert.IsTrue(reloadedDeletion.TextDecorationLine.Contains("line-through", StringComparison.OrdinalIgnoreCase),
                $"Reloaded deletion markup must stay struck-through. Debug: {reloadedDeletion.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_TrackChangesDeletion_PersistsAfterSaveReload));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "track-changes-deletion", TrackChangesBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_ReviewedRevisions_DoNotReturnAfterSaveReload()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var accepted = $"phase16-accept-{DateTimeOffset.UtcNow:HHmmssfff}";
        var rejected = $"phase16-reject-{DateTimeOffset.UtcNow:HHmmssfff}";

        try
        {
            await EnableTrackChangesAsync(page);
            await ClickDocumentEditorBlockOffsetAsync(page, TrackChangesBlockId, 0);
            await page.Keyboard.TypeAsync($"{accepted} ", new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "accepted insertion revision", TrackChangesBlockId, accepted);
            await ReviewFirstRevisionContainingTextAsync(page, accepted, "accept");
            await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).ToContainTextAsync(accepted, new() { Timeout = 5000 });
            await AssertNoPendingRevisionForTextAsync(page, accepted);

            await ClickDocumentEditorBlockOffsetAsync(page, TrackChangesBlockId, 0);
            await page.Keyboard.TypeAsync($"{rejected} ", new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "rejected insertion revision", TrackChangesBlockId, rejected);
            await ReviewFirstRevisionContainingTextAsync(page, rejected, "reject");
            await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).Not.ToContainTextAsync(rejected, new() { Timeout = 5000 });
            await AssertNoPendingRevisionForTextAsync(page, rejected);

            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).ToContainTextAsync(accepted, new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).Not.ToContainTextAsync(rejected, new() { Timeout = 5000 });
            await AssertNoPendingRevisionForTextAsync(page, accepted);
            await AssertNoPendingRevisionForTextAsync(page, rejected);
            Assert.AreEqual(0, await ReadRevisionMarkerCountForTextAsync(page, accepted),
                "Accepted insertion must not reload as pending revision markup.");
            Assert.AreEqual(0, await ReadRevisionMarkerCountForTextAsync(page, rejected),
                "Rejected insertion must not reload as pending revision markup.");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_ReviewedRevisions_DoNotReturnAfterSaveReload));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "reviewed-revisions-reload", TrackChangesBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_CommentBoundary_TypingAfterCommentDoesNotExtendCommentHighlight()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "fff";

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CommentBoundaryBlockId, "Text before ".Length + 2);
            var commentCaretToolbar = await ReadRibbonFormattingStateAsync(page);
            Assert.IsTrue(string.IsNullOrWhiteSpace(commentCaretToolbar.HighlightColor),
                $"Comment annotation highlight must not appear as a normal text highlight command state. State: {commentCaretToolbar.Debug}");

            await ClickAfterPhraseByMouseAsync(page, CommentBoundaryBlockId, CommentBoundaryPhrase);
            await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "typing after comment boundary", CommentBoundaryBlockId, typed);

            var probe = await ReadCommentBoundaryProbeAsync(page, CommentBoundaryBlockId, typed);
            Assert.IsTrue(probe.TypedTextOffset > probe.CommentAnchorOffset,
                $"Typed text should appear immediately after the comment boundary. Debug: {probe.Debug}");
            Assert.IsFalse(probe.TypedTextInsideCommentAnchor,
                $"Text typed after a comment range must not inherit comment anchor highlight. Debug: {probe.Debug}");
            Assert.IsFalse(probe.CommentAnchorText.Contains(typed, StringComparison.Ordinal),
                $"The original comment anchor text must not expand to include newly typed text. Debug: {probe.Debug}");
            await SaveAndReloadOnlyOfficeParityDocumentAsync(page);
            var reloadedProbe = await ReadCommentBoundaryProbeAsync(page, CommentBoundaryBlockId, typed);
            Assert.IsFalse(reloadedProbe.TypedTextInsideCommentAnchor,
                $"Text typed after a comment range must stay outside the comment anchor after save/reload. Debug: {reloadedProbe.Debug}");
            Assert.IsFalse(reloadedProbe.CommentAnchorText.Contains(typed, StringComparison.Ordinal),
                $"Comment anchor text must not expand after save/reload. Debug: {reloadedProbe.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_CommentBoundary_TypingAfterCommentDoesNotExtendCommentHighlight));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "comment-boundary", CommentBoundaryBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_SidePanel_RevisionsReplaceVersionsAndKeepDockedLayout()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            await page.GetByTestId("document-ribbon-tab-view").ClickAsync();
            await page.GetByTestId("document-open-versions").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-side-panel-tab-versions")).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-version-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
            Assert.AreEqual(0, await page.GetByTestId("document-revision-panel").CountAsync(), "Version tab must not render the revision panel at the same time.");

            var versionsLayout = await ReadSidePanelLayoutProbeAsync(page);
            Assert.AreEqual("docked-tabs", versionsLayout.WorkspaceLayout, versionsLayout.Debug);
            Assert.AreEqual("versions", versionsLayout.ActiveWorkspaceTab, versionsLayout.Debug);
            Assert.IsTrue(versionsLayout.DocumentSurfaceNarrowed, $"Docked side panel must narrow the document surface instead of covering it. Debug: {versionsLayout.Debug}");
            Assert.IsTrue(versionsLayout.CloseButtonVisible, $"Docked side panel must expose a clear close button. Debug: {versionsLayout.Debug}");

            await page.GetByTestId("document-ribbon-tab-review").ClickAsync();
            await page.GetByTestId("document-open-revisions").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-side-panel-tab-revisions")).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
            await Assertions.Expect(page.GetByTestId("document-revision-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
            Assert.AreEqual(0, await page.GetByTestId("document-version-panel").CountAsync(), "Revision tab must replace the version panel instead of stacking next to it.");

            var revisionsLayout = await ReadSidePanelLayoutProbeAsync(page);
            Assert.AreEqual("revisions", revisionsLayout.ActiveWorkspaceTab, revisionsLayout.Debug);
            Assert.AreEqual("revisions", revisionsLayout.ActiveSidePanelTab, revisionsLayout.Debug);
            Assert.AreEqual(1, revisionsLayout.VisiblePanelCount, revisionsLayout.Debug);
            Assert.IsTrue(revisionsLayout.DocumentSurfaceNarrowed, $"Revision panel must stay in the layout and preserve document context. Debug: {revisionsLayout.Debug}");

            var focusOrder = await ReadSidePanelFocusOrderAsync(page);
            AssertFocusOrder(focusOrder, "document-side-panel-tab-revisions", "document-side-panel-close");
            AssertFocusOrder(focusOrder, "document-side-panel-close", "document-revision-filter-author");
            AssertFocusOrder(focusOrder, "document-revision-filter-author", "document-revision-filter-type");
            AssertFocusOrder(focusOrder, "document-revision-accept-all", "document-revision-reject-all");

            await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });
            var floatingLayout = await ReadSidePanelLayoutProbeAsync(page);
            Assert.IsTrue(floatingLayout.MiniToolbarVisible, floatingLayout.Debug);
            Assert.IsFalse(floatingLayout.MiniToolbarOverlapsSidePanel,
                $"Floating toolbar must avoid the docked right panel. Debug: {floatingLayout.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_SidePanel_RevisionsReplaceVersionsAndKeepDockedLayout));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "side-panel-docked-layout", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_RibbonTabs_ShowClearModesPreserveSelectionAndTrackChangesToggle()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "phase13 mode ";

        try
        {
            var selection = await SelectTextByMouseAsync(page, FormattingBlockId, FormattingPhrase);
            var homeMode = await ReadRibbonModeProbeAsync(page);
            Assert.AreEqual("home", homeMode.ActiveToolbarTab, homeMode.Debug);
            Assert.AreEqual("home", homeMode.ActivePanelTab, homeMode.Debug);
            Assert.IsTrue(homeMode.HasFormattingTools, $"Home tab must show formatting tools. Debug: {homeMode.Debug}");
            Assert.IsFalse(homeMode.HasReviewTools, $"Home tab must not mix in review tools. Debug: {homeMode.Debug}");

            await page.GetByTestId("document-ribbon-tab-review").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-ribbon-tab-review")).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
            var reviewMode = await ReadRibbonModeProbeAsync(page);
            Assert.AreEqual("review", reviewMode.ActiveToolbarTab, reviewMode.Debug);
            Assert.AreEqual("review", reviewMode.ActivePanelTab, reviewMode.Debug);
            Assert.IsTrue(reviewMode.HasReviewTools, $"Review tab must show review tools. Debug: {reviewMode.Debug}");
            Assert.IsFalse(reviewMode.HasFormattingTools, $"Review tab must hide Home formatting groups. Debug: {reviewMode.Debug}");
            Assert.IsTrue(Math.Abs(reviewMode.ToolbarHeight - homeMode.ToolbarHeight) <= 20,
                $"Switching Home -> Review should not create a visible ribbon layout jump. Home={homeMode.ToolbarHeight}, Review={reviewMode.ToolbarHeight}. Debug: {reviewMode.Debug}");
            await AssertSelectionStillEqualsAsync(page, selection);

            await page.GetByTestId("document-ribbon-tab-home").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-ribbon-tab-home")).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);

            await page.GetByTestId("document-ribbon-tab-home").FocusAsync();
            await page.Keyboard.PressAsync("ArrowRight");
            await Assertions.Expect(page.GetByTestId("document-ribbon-tab-insert")).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
            await page.Keyboard.PressAsync("End");
            await Assertions.Expect(page.GetByTestId("document-ribbon-tab-view")).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });

            await page.GetByTestId("document-ribbon-tab-review").ClickAsync();
            var toggle = page.GetByTestId("document-track-changes");
            await Assertions.Expect(toggle).ToBeVisibleAsync(new() { Timeout = 5000 });
            if (string.Equals(await toggle.GetAttributeAsync("aria-pressed"), "true", StringComparison.OrdinalIgnoreCase))
            {
                await toggle.ClickAsync();
                await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false", new() { Timeout = 5000 });
            }

            var offToggle = await ReadTrackChangesToggleProbeAsync(page);
            Assert.AreEqual("off", offToggle.State, offToggle.Debug);
            Assert.AreEqual("false", offToggle.AriaPressed, offToggle.Debug);
            Assert.IsTrue(offToggle.IsNeutral, $"Disabled track changes toggle must look neutral. Debug: {offToggle.Debug}");

            await toggle.ClickAsync();
            await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await Assertions.Expect(toggle).ToHaveAttributeAsync("data-state", "on", new() { Timeout = 5000 });
            var onToggle = await ReadTrackChangesToggleProbeAsync(page);
            Assert.AreEqual("on", onToggle.State, onToggle.Debug);
            Assert.AreEqual("true", onToggle.AriaPressed, onToggle.Debug);
            Assert.IsTrue(onToggle.IsActive, $"Enabled track changes toggle must expose an active visual state. Debug: {onToggle.Debug}");
            Assert.AreNotEqual(offToggle.BackgroundColor, onToggle.BackgroundColor, $"Track changes on/off must have visibly different backgrounds. Off={offToggle.Debug}; On={onToggle.Debug}");

            await ClickDocumentEditorBlockOffsetAsync(page, TrackChangesBlockId, 0);
            await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "track changes mode typing", TrackChangesBlockId, typed);

            var revision = await ReadInsertionRevisionProbeAsync(page, TrackChangesBlockId);
            Assert.IsTrue(revision.Text.Contains(typed, StringComparison.Ordinal),
                $"Track changes enabled state must immediately affect newly typed text. Revision text was '{revision.Text}'. Debug: {revision.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_RibbonTabs_ShowClearModesPreserveSelectionAndTrackChangesToggle));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "ribbon-tabs-track-changes-mode", FormattingBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_ToolbarKeyboard_TabActivateEscapeAndPaletteArrows()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var typed = $"phase17toolbar{DateTimeOffset.UtcNow:HHmmssfff}";

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 0);
            await page.Keyboard.TypeAsync($"{typed} ", new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "phase 17 toolbar keyboard seed", CollapsedCaretBlockId, typed);
            var selection = await SelectTextByMouseAsync(page, CollapsedCaretBlockId, typed);

            var focusOrder = await ReadToolbarFocusOrderAsync(page);
            AssertFocusOrder(focusOrder, "document-ribbon-tab-home", "document-save");
            AssertFocusOrder(focusOrder, "document-save", "document-font-family");
            AssertFocusOrder(focusOrder, "document-font-family", "document-font-size");
            AssertFocusOrder(focusOrder, "document-font-size", "document-bold");
            AssertFocusOrder(focusOrder, "document-bold", "document-italic");

            await page.GetByTestId("document-ribbon-tab-home").FocusAsync();
            await page.Keyboard.PressAsync("Tab");
            Assert.AreEqual("document-save", await ReadActiveElementTestIdAsync(page), $"Tab from Home must enter the ribbon command strip. Focus order: {string.Join(" > ", focusOrder)}");
            await page.Keyboard.PressAsync("Tab");
            Assert.AreEqual("document-undo", await ReadActiveElementTestIdAsync(page), $"Tab must continue through enabled quick-access commands. Focus order: {string.Join(" > ", focusOrder)}");
            await page.Keyboard.PressAsync("Tab");
            Assert.AreEqual("document-font-family", await ReadActiveElementTestIdAsync(page), $"Disabled redo must be skipped and formatting controls must follow quick access. Focus order: {string.Join(" > ", focusOrder)}");

            await page.GetByTestId("document-bold").FocusAsync();
            await page.Keyboard.PressAsync("Space");
            AssertTextRunsAreBold(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed));
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);

            await page.GetByTestId("document-italic").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            AssertTextRunsFontStyle(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed), "italic");
            await Assertions.Expect(page.GetByTestId("document-italic")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
            await AssertSelectionStillEqualsAsync(page, selection);

            var picker = page.GetByTestId("document-font-color-trigger");
            var trigger = picker.Locator(".tm-color-picker-trigger");
            await trigger.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true", new() { Timeout = 5000 });
            await Assertions.Expect(picker.Locator(".tm-color-picker-dropdown")).ToBeVisibleAsync(new() { Timeout = 5000 });

            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Tab");
            await page.WaitForFunctionAsync("() => !!document.activeElement?.closest?.('.tm-color-palette-swatch')", null, new() { Timeout = 5000 });
            var indexBefore = await ReadFocusedPaletteSwatchIndexAsync(page);
            await page.Keyboard.PressAsync("ArrowRight");
            await page.WaitForFunctionAsync(
                "previous => Number(document.activeElement?.closest?.('.tm-color-palette-swatch')?.getAttribute('data-palette-index') || -1) > Number(previous)",
                indexBefore,
                new() { Timeout = 5000 });
            var indexAfter = await ReadFocusedPaletteSwatchIndexAsync(page);
            Assert.AreEqual(indexBefore + 1, indexAfter, $"ArrowRight must move the palette roving focus. Before={indexBefore}, after={indexAfter}.");

            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false", new() { Timeout = 5000 });
            Assert.AreEqual("document-font-color-trigger", await ReadActiveElementTestIdAsync(page), "Escape must close the color popover and return focus to the trigger.");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_ToolbarKeyboard_TabActivateEscapeAndPaletteArrows));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "phase17-toolbar-keyboard", CollapsedCaretBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_EditorKeyboardShortcuts_FormatUndoRedoAndKeepTrackChangesGrouped()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var typed = $"phase17shortcut{DateTimeOffset.UtcNow:HHmmssfff}";
        var tracked = $"phase17tracked{DateTimeOffset.UtcNow:HHmmssfff}";

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 0);
            await page.Keyboard.TypeAsync($"{typed} ", new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "phase 17 shortcut seed", CollapsedCaretBlockId, typed);
            await SelectTextByMouseAsync(page, CollapsedCaretBlockId, typed);

            await page.Keyboard.PressAsync("Control+B");
            AssertTextRunsAreBold(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed));
            await Assertions.Expect(page.GetByTestId("document-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

            await page.Keyboard.PressAsync("Control+I");
            AssertTextRunsFontStyle(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed), "italic");
            await Assertions.Expect(page.GetByTestId("document-italic")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

            await page.Keyboard.PressAsync("Control+U");
            AssertTextRunsTextDecorationContains(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed), "underline");
            await Assertions.Expect(page.GetByTestId("document-underline")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

            await page.Keyboard.PressAsync("Control+Z");
            await WaitForRedoEnabledAsync(page);
            var afterUndo = await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed);
            Assert.IsFalse(afterUndo.TargetStyles.All(style => style.Underline),
                $"Ctrl+Z must undo the last keyboard formatting command. Debug: {afterUndo.Debug}");

            await page.Keyboard.PressAsync("Control+Y");
            AssertTextRunsTextDecorationContains(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed), "underline");

            await page.Keyboard.PressAsync("Control+Z");
            await WaitForRedoEnabledAsync(page);
            await page.Keyboard.PressAsync("Control+Shift+Z");
            AssertTextRunsTextDecorationContains(await ReadTextRunComputedStylesAsync(page, CollapsedCaretBlockId, typed), "underline");

            await EnableTrackChangesAsync(page);
            await ClickDocumentEditorBlockOffsetAsync(page, TrackChangesBlockId, 0);
            await page.Keyboard.TypeAsync(tracked, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "phase 17 track changes after shortcuts", TrackChangesBlockId, tracked);

            var revision = await ReadInsertionRevisionProbeAsync(page, TrackChangesBlockId);
            Assert.IsTrue(revision.Text.Contains(tracked, StringComparison.Ordinal),
                $"Track changes must keep normal typing order after editor keyboard shortcuts. Revision text was '{revision.Text}'. Debug: {revision.Debug}");
            Assert.IsTrue(revision.FragmentCount <= 2,
                $"Keyboard shortcuts must not break track changes grouping. FragmentCount={revision.FragmentCount}. Debug: {revision.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_EditorKeyboardShortcuts_FormatUndoRedoAndKeepTrackChangesGrouped));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "phase17-editor-shortcuts", CollapsedCaretBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_PerformanceBudget_FastTypingAndHeldKeyStayOnPartialRenderPath()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var fastText = $"phase18fast{DateTimeOffset.UtcNow:HHmmssfff}" + new string('a', 200);

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 0);
            await ClearPerformanceMetricsAsync(page);
            await page.Keyboard.TypeAsync(fastText, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "phase 18 fast typing", CollapsedCaretBlockId, fastText);
            await page.WaitForTimeoutAsync(650);

            var typedText = await ReadDocumentEditorBlockTextAsync(page, CollapsedCaretBlockId);
            Assert.IsTrue(typedText.StartsWith(fastText, StringComparison.Ordinal),
                $"Fast typing must be visible in order. Block text: {typedText}");
            var fastMetrics = await ReadPerformanceMetricsAsync(page);
            Assert.AreEqual(0, fastMetrics.FullRenderCount, $"Fast typing must not full-render the document. Metrics: {fastMetrics}");
            Assert.IsTrue(fastMetrics.PartialRenderCount >= fastText.Length,
                $"Fast typing must account partial/live patches per key. Metrics: {fastMetrics}");
            Assert.IsTrue(fastMetrics.BlazorCallbackDuringTypingCount <= 4,
                $"Typing callbacks to Blazor must be batched, not per key. Metrics: {fastMetrics}");
            AssertHistogramWithinBudget(fastMetrics.KeydownVisibleTextHistogram, minimumSamples: fastText.Length - 2, "keydown -> visible text");

            await ClearPerformanceMetricsAsync(page);
            var held = await HoldKeyAndMeasureBatchesAsync(page, "x", holdMilliseconds: 2000);
            await page.WaitForTimeoutAsync(650);
            var heldMetrics = await ReadPerformanceMetricsAsync(page);

            Assert.AreEqual(0, held.FullRenderCount, $"Held key probe must stay off full render path. Probe full renders: {held.FullRenderCount}");
            Assert.IsTrue(held.MutationBatchCount >= 8,
                $"Held key should paint progressively in multiple DOM mutation batches. Batches: {held.MutationBatchCount}");
            Assert.AreEqual(0, heldMetrics.FullRenderCount, $"Held key runtime metrics must stay off full render path. Metrics: {heldMetrics}");
            Assert.IsTrue(heldMetrics.BlazorCallbackDuringTypingCount <= 4,
                $"Held key Blazor callbacks must remain throttled. Metrics: {heldMetrics}");
            AssertHistogramWithinBudget(heldMetrics.KeydownVisibleTextHistogram, minimumSamples: 8, "held keydown -> visible text");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_PerformanceBudget_FastTypingAndHeldKeyStayOnPartialRenderPath));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "phase18-fast-typing-held-key", CollapsedCaretBlockId);
            throw;
        }
    }

    [TestMethod]
    public async Task OnlyOfficeParity_PerformanceBudget_SpaceEnterFormattingTrackChangesAndMixedMarkupStayMeasured()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var longText = $"phase18format{DateTimeOffset.UtcNow:HHmmssfff}" + new string('f', 160);
        var formatTarget = longText[..48];
        var tracked = $"phase18tracked{DateTimeOffset.UtcNow:HHmmssfff}" + new string('t', 100);

        try
        {
            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 0);
            await page.Keyboard.TypeAsync(longText, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "phase 18 long formatting seed", CollapsedCaretBlockId, longText);

            var selection = await SelectTextRangeForPerformanceAsync(page, CollapsedCaretBlockId, 0, formatTarget.Length);
            await page.WaitForTimeoutAsync(140);
            var selectionMetrics = await ReadPerformanceMetricsAsync(page);
            Assert.IsTrue(selectionMetrics.FormattingStateEventCount > 0,
                $"Selection must publish formatting state. Metrics: {selectionMetrics}");
            Assert.AreEqual(0, selectionMetrics.ToolbarStateLayoutThrashCount,
                $"Toolbar state updates must not synchronously trigger render/layout thrash. Metrics: {selectionMetrics}");
            AssertHistogramWithinBudget(selectionMetrics.SelectionChangeToolbarStateHistogram, minimumSamples: 1, "selection -> toolbar state");

            await ClearPerformanceMetricsAsync(page);
            await ClickRibbonCommandAsync(page, "document-bold", selection);
            var formattingMetrics = await ReadPerformanceMetricsAsync(page);
            Assert.AreEqual(0, formattingMetrics.FullRenderCount, $"Formatting selected text should patch the affected paragraph. Metrics: {formattingMetrics}");
            Assert.IsTrue(formattingMetrics.FormattingCommandPartialRenderCount > 0,
                $"Formatting command must record a partial render. Metrics: {formattingMetrics}");
            CollectionAssert.Contains(formattingMetrics.LastPartialRenderScopeIds, CollapsedCaretBlockId,
                $"Formatting scope must include the edited paragraph. Metrics: {formattingMetrics}");
            AssertHistogramWithinBudget(formattingMetrics.ToolbarCommandVisibleStyleHistogram, minimumSamples: 1, "toolbar command -> visible style");

            await ClickDocumentEditorBlockOffsetAsync(page, CollapsedCaretBlockId, 12);
            await ClearPerformanceMetricsAsync(page);
            await page.Keyboard.PressAsync("Space");
            await page.Keyboard.PressAsync("Enter");
            await page.Keyboard.PressAsync("Z");
            await WaitForEditorStableAsync(page, "phase 18 space enter latency", CollapsedCaretBlockId);
            var spaceEnterMetrics = await ReadPerformanceMetricsAsync(page);
            Assert.AreEqual(0, spaceEnterMetrics.FullRenderCount, $"Space/Enter must stay partial. Metrics: {spaceEnterMetrics}");
            AssertHistogramWithinBudget(spaceEnterMetrics.SpaceVisibleTextHistogram, minimumSamples: 1, "Space -> visible text");
            AssertHistogramWithinBudget(spaceEnterMetrics.EnterVisibleTextHistogram, minimumSamples: 1, "Enter -> visible paragraph");

            var commentStart = "Text before ".Length;
            await SelectTextRangeForPerformanceAsync(page, CommentBoundaryBlockId, commentStart, commentStart + CommentBoundaryPhrase.Length);
            await ClickRibbonCommandAsync(page, "document-bold", expectedSelection: null, requireRuntimeSelectionToken: false);
            await EnableTrackChangesAsync(page);
            await ClickDocumentEditorBlockOffsetAsync(page, CommentBoundaryBlockId, commentStart + CommentBoundaryPhrase.Length);
            await ClearPerformanceMetricsAsync(page);
            await page.Keyboard.TypeAsync(tracked, new() { Delay = 0 });
            await WaitForEditorStableAsync(page, "phase 18 track changes stress", CommentBoundaryBlockId, tracked);
            await page.WaitForTimeoutAsync(650);

            var trackMetrics = await ReadPerformanceMetricsAsync(page);
            Assert.AreEqual(0, trackMetrics.FullRenderCount, $"Track changes typing must stay partial. Metrics: {trackMetrics}");
            AssertHistogramWithinBudget(trackMetrics.KeydownVisibleTextHistogram, minimumSamples: tracked.Length - 2, "track changes typing");
            var revision = await ReadInsertionRevisionProbeAsync(page, CommentBoundaryBlockId);
            Assert.IsTrue(revision.Text.Contains(tracked, StringComparison.Ordinal),
                $"Tracked text must be rendered as one visible insertion. Revision: {revision.Debug}");
            Assert.IsTrue(revision.FragmentCount <= 2,
                $"Track changes stress typing must stay coalesced. FragmentCount={revision.FragmentCount}. Debug: {revision.Debug}");

            var mixed = await ReadMixedMarkupPerformanceProbeAsync(page, CommentBoundaryBlockId);
            Assert.IsTrue(mixed.HasComment, $"Mixed paragraph must retain comment markup. Debug: {mixed.Debug}");
            Assert.IsTrue(mixed.HasRevisionInsert, $"Mixed paragraph must include tracked insertion markup. Debug: {mixed.Debug}");
            Assert.IsTrue(mixed.HasBold, $"Mixed paragraph must retain formatting markup. Debug: {mixed.Debug}");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(OnlyOfficeParity_PerformanceBudget_SpaceEnterFormattingTrackChangesAndMixedMarkupStayMeasured));
        }
        catch
        {
            await SaveOnlyOfficeParityArtifactsAsync(page, console, "phase18-space-enter-formatting-track-mixed", CommentBoundaryBlockId);
            throw;
        }
    }

    private static async Task<JsonDocument> LoadOnlyOfficeParityDocumentFromApiAsync()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.GetAsync($"api/document-editor/documents/{DocumentId}");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task SaveOnlyOfficeParityArtifactsAsync(IPage page, DocumentEditorConsoleCapture console, string behavior, string? targetBlockId = null)
    {
        var artifactPath = await CaptureDocumentEditorDiagnosticArtifactAsync(page, $"onlyoffice_parity_{behavior}", targetBlockId, console);
        if (console.FatalErrors.Count > 0)
        {
            throw new AssertFailedException($"{behavior} emitted document editor console/runtime errors: {string.Join(" | ", console.FatalErrors)}. JSON artifact: {artifactPath}");
        }
    }

    private static async Task SaveAndReloadOnlyOfficeParityDocumentAsync(IPage page)
    {
        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync(new Regex("Saved|Autosaved"), new() { Timeout = 10000 });
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);
    }

    private static Task ClearPerformanceMetricsAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorEngine?.clearDebugMetrics?.(instanceId);
            }
            """);

    private static Task<PerformanceMetricsProbe> ReadPerformanceMetricsAsync(IPage page)
        => page.EvaluateAsync<PerformanceMetricsProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorEngine?.getDebugMetrics?.(instanceId) || {};
            }
            """);

    private static Task<MixedMarkupPerformanceProbe> ReadMixedMarkupPerformanceProbeAsync(IPage page, string blockId)
        => page.EvaluateAsync<MixedMarkupPerformanceProbe>(
            """
            blockId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = CSS.escape(blockId);
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        return rect.width > 1 && rect.height > 1;
                    });
                const comment = !!block?.querySelector('.tm-document-inline--comment-anchor, .tm-wysiwyg-marker--comment');
                const revision = !!block?.querySelector('.tm-wysiwyg-revision--insert, .tm-document-inline--revision-insert, [data-testid="document-wysiwyg-revision-insert"]');
                const bold = Array.from(block?.querySelectorAll('*') || []).some(node => {
                    const style = getComputedStyle(node);
                    return Number.parseInt(style.fontWeight || '0', 10) >= 600 || style.fontWeight === 'bold';
                });
                return {
                    hasComment: comment,
                    hasRevisionInsert: revision,
                    hasBold: bold,
                    debug: JSON.stringify({
                        text: block?.innerText || block?.textContent || '',
                        html: block?.outerHTML?.slice(0, 2400) || ''
                    })
                };
            }
            """,
            blockId);

    private static async Task<DocumentEditorSelectionSnapshot> SelectTextRangeForPerformanceAsync(IPage page, string blockId, int startOffset, int endOffset)
    {
        await page.EvaluateAsync(
            """
            ({ blockId, startOffset, endOffset }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = CSS.escape(blockId);
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        return rect.width > 1 && rect.height > 1;
                    });
                if (!block) throw new Error(`Block '${blockId}' was not found.`);

                const locate = absoluteOffset => {
                    const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                    let current = 0;
                    let node;
                    while ((node = walker.nextNode())) {
                        const length = (node.textContent || '').length;
                        if (absoluteOffset <= current + length) {
                            return { node, offset: Math.max(0, Math.min(absoluteOffset - current, length)) };
                        }
                        current += length;
                    }
                    throw new Error(`Offset ${absoluteOffset} was not found in block '${blockId}'.`);
                };

                const start = locate(startOffset);
                const end = locate(endOffset);
                const range = document.createRange();
                range.setStart(start.node, start.offset);
                range.setEnd(end.node, end.offset);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            new { blockId, startOffset, endOffset });
        await WaitForEditorStableAsync(page, "phase 18 programmatic performance selection", blockId);
        return await ReadDocumentEditorSelectionSnapshotAsync(page);
    }

    private static void AssertHistogramWithinBudget(LatencyHistogramProbe histogram, int minimumSamples, string label)
    {
        Assert.IsTrue(histogram.Count >= minimumSamples,
            $"{label} histogram must contain real samples, not only an average. Count={histogram.Count}, expected>={minimumSamples}, histogram={histogram}");
        Assert.IsTrue(histogram.P95Ms >= 0 && histogram.P95Ms <= histogram.BudgetMs,
            $"{label} exceeded the p95 budget. Histogram={histogram}");
        Assert.IsTrue(histogram.MaxMs >= 0 && histogram.MaxMs <= histogram.BudgetMs * 3,
            $"{label} has a single-sample latency spike beyond the fail-fast guard. Histogram={histogram}");
    }

    private static Task WaitForUndoEnabledAsync(IPage page)
        => page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getUndoState?.(instanceId) || {};
                const button = document.querySelector('[data-testid="document-undo"]');
                return !!(state.CanUndo ?? state.canUndo) && button && !button.disabled;
            }
            """,
            null,
            new() { Timeout = 5000 });

    private static Task WaitForRedoEnabledAsync(IPage page)
        => page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getUndoState?.(instanceId) || {};
                const button = document.querySelector('[data-testid="document-redo"]');
                return !!(state.CanRedo ?? state.canRedo) && button && !button.disabled;
            }
            """,
            null,
            new() { Timeout = 5000 });

    private static Task<UndoStateProbe> ReadUndoStateDebugAsync(IPage page)
        => page.EvaluateAsync<UndoStateProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getUndoState?.(instanceId) || {};
                const debug = window.tmDocumentEditorRuntime?.getDebugUndoStack?.(instanceId) || null;
                const undoButton = document.querySelector('[data-testid="document-undo"]');
                const redoButton = document.querySelector('[data-testid="document-redo"]');
                return {
                    canUndo: !!(state.CanUndo ?? state.canUndo),
                    canRedo: !!(state.CanRedo ?? state.canRedo),
                    undoDepth: Number(state.UndoDepth ?? state.undoDepth ?? 0),
                    redoDepth: Number(state.RedoDepth ?? state.redoDepth ?? 0),
                    undoButtonDisabled: !!undoButton?.disabled,
                    redoButtonDisabled: !!redoButton?.disabled,
                    debug: JSON.stringify({ state, debug })
                };
            }
            """);

    private static void AssertSurroundingRunsDoNotHaveInlineDecorations(DocumentEditorTextRunComputedStyleProbe probe)
    {
        var surrounding = probe.BeforeStyles.Concat(probe.AfterStyles).ToArray();
        Assert.IsTrue(surrounding.Length > 0, $"Expected target formatting to split surrounding text into measurable runs. Debug: {probe.Debug}");
        Assert.IsTrue(surrounding.All(style => !style.Bold && !style.Italic && !style.Underline && !style.Strikethrough),
            $"Formatting command leaked inline decorations outside the target range. Debug: {probe.Debug}");
    }

    private static void AssertSurroundingRunsDoNotUseFontFamilyOrSize(
        DocumentEditorTextRunComputedStyleProbe probe,
        string fontFamily,
        double sizePt)
    {
        var surrounding = probe.BeforeStyles.Concat(probe.AfterStyles).ToArray();
        Assert.IsTrue(surrounding.Length > 0, $"Expected target formatting to split surrounding text into measurable runs. Debug: {probe.Debug}");
        Assert.IsFalse(surrounding.Any(style => style.FontFamily.Contains(fontFamily, StringComparison.OrdinalIgnoreCase)),
            $"Font family command leaked outside the target range. Debug: {probe.Debug}");
        Assert.IsFalse(surrounding.Any(style => Math.Abs(style.FontSizePt - sizePt) <= 1.75),
            $"Font size command leaked outside the target range. Debug: {probe.Debug}");
    }

    private static async Task SelectPhraseByMouseAsync(IPage page, string blockId, string phrase)
    {
        var target = await ReadPhraseMouseTargetAsync(page, blockId, phrase);
        await page.Mouse.MoveAsync((float)target.StartX, (float)target.StartY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)target.EndX, (float)target.EndY, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync(
            "phrase => (window.getSelection()?.toString() || '').includes(phrase)",
            phrase,
            new() { Timeout = 5000 });
    }

    private static async Task ClickAfterPhraseByMouseAsync(IPage page, string blockId, string phrase)
    {
        var target = await ReadPhraseMouseTargetAsync(page, blockId, phrase);
        await page.Mouse.ClickAsync((float)target.AfterX, (float)target.AfterY);
    }

    private static Task<PhraseMouseTarget> ReadPhraseMouseTargetAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync<PhraseMouseTarget>(
            """
            ({ blockId, phrase }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = visibleBlock(host, blockId);
                if (!block) throw new Error(`Could not find visible block '${blockId}'.`);
                block.scrollIntoView({ block: 'center', inline: 'nearest' });

                const nodes = [];
                let text = '';
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue && node.nodeValue.length > 0
                            ? NodeFilter.FILTER_ACCEPT
                            : NodeFilter.FILTER_REJECT;
                    }
                });
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    nodes.push({ node, start: text.length, end: text.length + node.nodeValue.length });
                    text += node.nodeValue;
                }

                const start = text.indexOf(phrase);
                if (start < 0) throw new Error(`Phrase '${phrase}' was not found in '${text}'.`);
                const end = start + phrase.length;
                const startPos = positionAt(start);
                const endPos = positionAt(end);
                const afterPos = positionAt(Math.min(text.length, end + 1));
                const startRect = rectFor(startPos.node, startPos.offset, 1, block);
                const endRect = rectFor(endPos.node, Math.max(0, endPos.offset - 1), 1, block);
                const afterRect = rectFor(afterPos.node, Math.max(0, afterPos.offset - 1), 1, block);
                return {
                    startX: startRect.left + 1,
                    startY: startRect.top + startRect.height / 2,
                    endX: endRect.right - 1,
                    endY: endRect.top + endRect.height / 2,
                    afterX: afterRect.left + Math.max(1, Math.min(4, afterRect.width / 2)),
                    afterY: afterRect.top + afterRect.height / 2,
                    blockText: text
                };

                function visibleBlock(root, id) {
                    const escaped = CSS.escape(id);
                    return Array.from(root?.querySelectorAll(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`) || [])
                        .find(node => {
                            const rect = node.getBoundingClientRect();
                            const style = getComputedStyle(node);
                            return rect.width > 1 && rect.height > 1 && style.display !== 'none' && style.visibility !== 'hidden';
                        });
                }

                function positionAt(offset) {
                    for (const entry of nodes) {
                        if (offset <= entry.end) {
                            return { node: entry.node, offset: Math.max(0, Math.min(entry.node.nodeValue.length, offset - entry.start)) };
                        }
                    }
                    const last = nodes[nodes.length - 1];
                    return { node: last.node, offset: last.node.nodeValue.length };
                }

                function rectFor(node, offset, length, fallback) {
                    const range = document.createRange();
                    const start = Math.max(0, Math.min(node.nodeValue.length, offset));
                    const end = Math.max(start, Math.min(node.nodeValue.length, start + length));
                    range.setStart(node, start);
                    range.setEnd(node, end);
                    return Array.from(range.getClientRects())[0] || fallback.getBoundingClientRect();
                }
            }
            """,
            new { blockId, phrase });

    private static Task<string> ReadNativeSelectedTextAsync(IPage page)
        => page.EvaluateAsync<string>("() => window.getSelection()?.toString() || ''");

    private static Task<ComputedStyleProbe> ReadComputedStyleForPhraseAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync<ComputedStyleProbe>(
            """
            ({ blockId, phrase }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${CSS.escape(blockId)}"], [data-render-block-id="${CSS.escape(blockId)}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        const style = getComputedStyle(node);
                        return rect.width > 1 && rect.height > 1 && style.display !== 'none' && style.visibility !== 'hidden';
                    });
                if (!block) throw new Error(`Could not find block '${blockId}'.`);
                const entries = [];
                let text = '';
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue && node.nodeValue.length > 0
                            ? NodeFilter.FILTER_ACCEPT
                            : NodeFilter.FILTER_REJECT;
                    }
                });
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    entries.push({ node, start: text.length, end: text.length + node.nodeValue.length });
                    text += node.nodeValue;
                }

                const start = text.indexOf(phrase);
                if (start < 0) throw new Error(`Phrase '${phrase}' was not found in '${text}'.`);
                const end = start + phrase.length;
                const styles = entries
                    .filter(entry => entry.end > start && entry.start < end)
                    .map(entry => {
                        const element = entry.node.parentElement;
                        const style = getComputedStyle(element);
                        return {
                            text: entry.node.nodeValue,
                            parentHtml: element?.outerHTML?.slice(0, 500) || '',
                            fontWeight: style.fontWeight,
                            fontStyle: style.fontStyle,
                            textDecorationLine: style.textDecorationLine,
                            fontSize: style.fontSize,
                            fontFamily: style.fontFamily,
                            color: style.color,
                            backgroundColor: style.backgroundColor
                        };
                    });

                return {
                    phrase,
                    blockText: text,
                    nodeCount: styles.length,
                    allFontWeightsBold: styles.length > 0 && styles.every(item => {
                        const numeric = Number.parseInt(item.fontWeight, 10);
                        return item.fontWeight === 'bold' || (Number.isFinite(numeric) && numeric >= 600);
                    }),
                    fontSizes: styles.map(item => item.fontSize),
                    colors: styles.map(item => item.color),
                    backgroundColors: styles.map(item => item.backgroundColor),
                    debug: JSON.stringify({ phrase, blockText: text, styles })
                };
            }
            """,
            new { blockId, phrase });

    private static async Task ApplyRibbonColorAsync(IPage page, string pickerTestId, string color)
    {
        var picker = page.GetByTestId(pickerTestId);
        await picker.Locator(".tm-color-picker-trigger").ClickAsync();
        await Assertions.Expect(picker.Locator(".tm-color-picker-dropdown")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await picker.Locator(".tm-flat-color-picker-hex input").FillAsync(color);
        await picker.Locator(".tm-flat-color-picker-hex input").PressAsync("Tab");
        await picker.Locator(".tm-color-picker-apply").ClickAsync();
    }

    private static Task<string> ReadColorPickerTriggerTextAsync(IPage page, string pickerTestId)
        => page.EvaluateAsync<string>(
            """
            pickerTestId => {
                const picker = document.querySelector(`[data-testid="${CSS.escape(pickerTestId)}"]`);
                return picker?.querySelector('.tm-color-picker-trigger-text')?.textContent?.trim() || '';
            }
            """,
            pickerTestId);

    private static Task<PointProbe> ReadPointJustBesideElementAsync(IPage page, string selector)
        => page.EvaluateAsync<PointProbe>(
            """
            selector => {
                const element = document.querySelector(selector);
                if (!element) throw new Error(`Could not find '${selector}'.`);
                const rect = element.getBoundingClientRect();
                return {
                    x: Math.max(1, rect.left - 4),
                    y: rect.top + rect.height / 2
                };
            }
            """,
            selector);

    private static async Task EnableTrackChangesAsync(IPage page)
    {
        await page.GetByTestId("document-ribbon-tab-review").ClickAsync();
        var button = page.GetByTestId("document-track-changes");
        await Assertions.Expect(button).ToBeVisibleAsync(new() { Timeout = 5000 });
        if (!string.Equals(await button.GetAttributeAsync("aria-pressed"), "true", StringComparison.OrdinalIgnoreCase))
        {
            await button.ClickAsync();
        }

        await Assertions.Expect(button).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
        await page.GetByTestId("document-ribbon-tab-home").ClickAsync();
    }

    private static Task<RevisionProbe> ReadInsertionRevisionProbeAsync(IPage page, string blockId)
        => page.EvaluateAsync<RevisionProbe>(
            """
            blockId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${CSS.escape(blockId)}"], [data-render-block-id="${CSS.escape(blockId)}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        return rect.width > 1 && rect.height > 1;
                    });
                const fragments = Array.from(block?.querySelectorAll('.tm-wysiwyg-revision--insert, .tm-document-inline--revision-insert, [data-testid="document-wysiwyg-revision-insert"]') || []);
                return {
                    text: fragments.map(node => node.textContent || '').join(''),
                    fragmentCount: fragments.length,
                    debug: JSON.stringify({
                        blockText: block?.innerText || block?.textContent || '',
                        fragments: fragments.map(node => ({ text: node.textContent || '', html: node.outerHTML.slice(0, 500) }))
                    })
                };
            }
            """,
            blockId);

    private static Task<RevisionProbe> ReadDeletionRevisionProbeAsync(IPage page, string blockId)
        => page.EvaluateAsync<RevisionProbe>(
            """
            blockId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${CSS.escape(blockId)}"], [data-render-block-id="${CSS.escape(blockId)}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        return rect.width > 1 && rect.height > 1;
                    });
                const fragments = Array.from(block?.querySelectorAll('.tm-wysiwyg-revision--delete, .tm-document-inline--revision-delete, [data-testid="document-wysiwyg-revision-delete"]') || []);
                const firstStyle = fragments[0] ? getComputedStyle(fragments[0]) : {};
                return {
                    text: fragments.map(node => node.textContent || '').join(''),
                    fragmentCount: fragments.length,
                    textDecorationLine: firstStyle.textDecorationLine || '',
                    debug: JSON.stringify({
                        blockText: block?.innerText || block?.textContent || '',
                        fragments: fragments.map(node => ({ text: node.textContent || '', html: node.outerHTML.slice(0, 500) })),
                        textDecorationLine: firstStyle.textDecorationLine || ''
                    })
                };
            }
            """,
            blockId);

    private static async Task ReviewFirstRevisionContainingTextAsync(IPage page, string text, string action)
    {
        await page.GetByTestId("document-ribbon-tab-review").ClickAsync();
        await page.GetByTestId("document-open-revisions").ClickAsync();
        var item = page.GetByTestId("document-revision-item").Filter(new() { HasText = text });
        await Assertions.Expect(item.First).ToBeVisibleAsync(new() { Timeout = 5000 });
        await item.First.Locator($"[data-testid='document-revision-{action}']").ClickAsync();
        await Assertions.Expect(item).ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    private static async Task AssertNoPendingRevisionForTextAsync(IPage page, string text)
    {
        await page.GetByTestId("document-ribbon-tab-review").ClickAsync();
        await page.GetByTestId("document-open-revisions").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-revision-item").Filter(new() { HasText = text }))
            .ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    private static Task<int> ReadRevisionMarkerCountForTextAsync(IPage page, string text)
        => page.EvaluateAsync<int>(
            """
            text => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const isVisible = node => {
                    const rect = node.getBoundingClientRect();
                    const style = getComputedStyle(node);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };
                return Array.from(host?.querySelectorAll('[data-revision-id], .tm-wysiwyg-revision') || [])
                    .filter(node => isVisible(node) && (node.textContent || '').includes(text))
                    .length;
            }
            """,
            text);

    private static Task<CommentBoundaryProbe> ReadCommentBoundaryProbeAsync(IPage page, string blockId, string typed)
        => page.EvaluateAsync<CommentBoundaryProbe>(
            """
            ({ blockId, typed }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${CSS.escape(blockId)}"], [data-render-block-id="${CSS.escape(blockId)}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        return rect.width > 1 && rect.height > 1;
                    });
                if (!block) throw new Error(`Could not find block '${blockId}'.`);
                const commentAnchor = block.querySelector('.tm-document-inline--comment-anchor, [data-comment-id="onlyoffice-comment-boundary"]');
                const typedNodes = [];
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue?.includes(typed) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                    }
                });
                while (walker.nextNode()) {
                    typedNodes.push(walker.currentNode);
                }
                const typedTextInsideCommentAnchor = typedNodes.some(node => !!node.parentElement?.closest('.tm-document-inline--comment-anchor, [data-comment-id="onlyoffice-comment-boundary"]'));
                const blockText = block.innerText || block.textContent || '';
                const commentAnchorText = commentAnchor?.textContent || '';
                return {
                    blockText,
                    commentAnchorText,
                    commentAnchorOffset: blockText.indexOf(commentAnchorText),
                    typedTextOffset: blockText.indexOf(typed),
                    typedTextInsideCommentAnchor,
                    debug: JSON.stringify({
                        blockHtml: block.outerHTML.slice(0, 2000),
                        commentAnchorHtml: commentAnchor?.outerHTML || '',
                        typedNodes: typedNodes.map(node => node.parentElement?.outerHTML?.slice(0, 500) || '')
                    })
                };
            }
            """,
            new { blockId, typed });

    private static Task<RibbonModeProbe> ReadRibbonModeProbeAsync(IPage page)
        => page.EvaluateAsync<RibbonModeProbe>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-toolbar"]');
                const panel = document.querySelector('[data-testid="document-ribbon-panel"]');
                const selectedTab = document.querySelector('[data-testid^="document-ribbon-tab-"][aria-selected="true"]');
                const rect = toolbar?.getBoundingClientRect();
                const has = testId => !!document.querySelector(`[data-testid="${testId}"]`);
                const activeClass = selectedTab?.classList?.contains('tm-document-editor__ribbon-tab--active') || false;
                const hasFormattingTools = has('document-bold')
                    && has('document-font-size')
                    && has('document-font-color-trigger');
                const hasReviewTools = has('document-track-changes')
                    && has('document-review-display-mode')
                    && has('document-open-revisions');

                return {
                    activeToolbarTab: toolbar?.getAttribute('data-active-ribbon-tab') || '',
                    activePanelTab: panel?.getAttribute('data-active-ribbon-tab') || '',
                    selectedTabTestId: selectedTab?.getAttribute('data-testid') || '',
                    selectedTabCurrent: selectedTab?.getAttribute('aria-current') || '',
                    selectedTabActive: activeClass,
                    hasFormattingTools,
                    hasReviewTools,
                    toolbarHeight: rect?.height || 0,
                    debug: JSON.stringify({
                        toolbarTab: toolbar?.getAttribute('data-active-ribbon-tab') || '',
                        panelTab: panel?.getAttribute('data-active-ribbon-tab') || '',
                        selectedTab: selectedTab?.outerHTML || '',
                        panelHtml: panel?.outerHTML?.slice(0, 1600) || '',
                        toolbarRect: rect ? {
                            top: rect.top,
                            left: rect.left,
                            right: rect.right,
                            bottom: rect.bottom,
                            width: rect.width,
                            height: rect.height
                        } : null
                    })
                };
            }
            """);

    private static Task<TrackChangesToggleProbe> ReadTrackChangesToggleProbeAsync(IPage page)
        => page.EvaluateAsync<TrackChangesToggleProbe>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-toolbar"]');
                const button = document.querySelector('[data-testid="document-track-changes"]');
                const style = button ? getComputedStyle(button) : null;
                const state = button?.getAttribute('data-state') || '';
                const className = button?.className || '';
                const isActive = button?.classList?.contains('tm-document-editor__track-toggle--on') === true
                    && button?.classList?.contains('tm-document-editor__ribbon-button--active') === true;
                const isNeutral = state === 'off'
                    && button?.classList?.contains('tm-document-editor__track-toggle--off') === true
                    && button?.classList?.contains('tm-document-editor__track-toggle--on') !== true;

                return {
                    state,
                    toolbarState: toolbar?.getAttribute('data-track-changes-state') || '',
                    ariaPressed: button?.getAttribute('aria-pressed') || '',
                    className,
                    backgroundColor: style?.backgroundColor || '',
                    borderColor: style?.borderColor || '',
                    color: style?.color || '',
                    isActive,
                    isNeutral,
                    debug: JSON.stringify({
                        toolbarState: toolbar?.getAttribute('data-track-changes-state') || '',
                        button: button?.outerHTML || '',
                        backgroundColor: style?.backgroundColor || '',
                        borderColor: style?.borderColor || '',
                        color: style?.color || ''
                    })
                };
            }
            """);

    private static Task<SidePanelLayoutProbe> ReadSidePanelLayoutProbeAsync(IPage page)
        => page.EvaluateAsync<SidePanelLayoutProbe>(
            """
            () => {
                const workspace = document.querySelector('[data-testid="document-editor-workspace"]');
                const surface = document.querySelector('.tm-document-editor__surface');
                const sidePanel = document.querySelector('[data-testid="document-side-panel"]');
                const body = document.querySelector('[data-testid="document-side-panel-body"]');
                const closeButton = document.querySelector('[data-testid="document-side-panel-close"]');
                const miniToolbar = document.querySelector('[data-testid="document-mini-toolbar"]');
                const versionPanelCount = document.querySelectorAll('[data-testid="document-version-panel"]').length;
                const revisionPanelCount = document.querySelectorAll('[data-testid="document-revision-panel"]').length;
                const surfaceRect = rect(surface);
                const panelRect = rect(sidePanel);
                const toolbarRect = rect(miniToolbar);
                const toolbarVisible = isVisible(miniToolbar);
                const panelVisible = isVisible(sidePanel);
                const documentSurfaceNarrowed = panelVisible
                    && surfaceRect.width > 1
                    && panelRect.width > 1
                    && surfaceRect.right <= panelRect.left - 1;
                const toolbarOverlapsPanel = toolbarVisible
                    && panelVisible
                    && intersects(toolbarRect, panelRect);

                return {
                    workspaceState: workspace?.getAttribute('data-side-panel-state') || '',
                    workspaceLayout: workspace?.getAttribute('data-side-panel-layout') || '',
                    activeWorkspaceTab: workspace?.getAttribute('data-active-side-panel-tab') || '',
                    activeSidePanelTab: sidePanel?.getAttribute('data-active-tab') || '',
                    visiblePanelCount: Number(sidePanel?.getAttribute('data-visible-panel-count') || body?.getAttribute('data-visible-panel-count') || 0),
                    sidePanelVisible: panelVisible,
                    closeButtonVisible: isVisible(closeButton),
                    documentSurfaceNarrowed,
                    versionPanelCount,
                    revisionPanelCount,
                    miniToolbarVisible: toolbarVisible,
                    miniToolbarOverlapsSidePanel: toolbarOverlapsPanel,
                    debug: JSON.stringify({
                        workspace: workspace?.outerHTML?.slice(0, 800) || '',
                        sidePanel: sidePanel?.outerHTML?.slice(0, 1200) || '',
                        surfaceRect,
                        panelRect,
                        toolbarRect,
                        versionPanelCount,
                        revisionPanelCount
                    })
                };

                function rect(element) {
                    if (!element) {
                        return { left: 0, top: 0, right: 0, bottom: 0, width: 0, height: 0 };
                    }

                    const value = element.getBoundingClientRect();
                    return {
                        left: value.left,
                        top: value.top,
                        right: value.right,
                        bottom: value.bottom,
                        width: value.width,
                        height: value.height
                    };
                }

                function isVisible(element) {
                    if (!element) return false;
                    const value = element.getBoundingClientRect();
                    const style = getComputedStyle(element);
                    return value.width > 1
                        && value.height > 1
                        && style.display !== 'none'
                        && style.visibility !== 'hidden'
                        && style.opacity !== '0';
                }

                function intersects(a, b) {
                    return a.left < b.right
                        && a.right > b.left
                        && a.top < b.bottom
                        && a.bottom > b.top;
                }
            }
            """);

    private static Task<string[]> ReadSidePanelFocusOrderAsync(IPage page)
        => page.EvaluateAsync<string[]>(
            """
            () => {
                const panel = document.querySelector('[data-testid="document-side-panel"]');
                const selector = [
                    'button:not([disabled])',
                    'select:not([disabled])',
                    'input:not([disabled])',
                    'textarea:not([disabled])',
                    'a[href]',
                    '[tabindex]:not([tabindex="-1"])'
                ].join(',');
                return Array.from(panel?.querySelectorAll(selector) || [])
                    .filter(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 1
                            && rect.height > 1
                            && style.display !== 'none'
                            && style.visibility !== 'hidden';
                    })
                    .map(element => element.getAttribute('data-testid') || element.id || element.getAttribute('aria-label') || element.tagName.toLowerCase());
            }
            """);

    private static Task<string[]> ReadToolbarFocusOrderAsync(IPage page)
        => page.EvaluateAsync<string[]>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-toolbar"]') || document.querySelector('.tm-document-editor__toolbar');
                const selector = [
                    'button:not([disabled])',
                    'select:not([disabled])',
                    'input:not([disabled])',
                    'textarea:not([disabled])',
                    'a[href]',
                    '[tabindex]:not([tabindex="-1"])'
                ].join(',');
                return Array.from(toolbar?.querySelectorAll(selector) || [])
                    .filter(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 1
                            && rect.height > 1
                            && style.display !== 'none'
                            && style.visibility !== 'hidden';
                    })
                    .map(element => element.getAttribute('data-testid')
                        || element.closest('[data-testid]')?.getAttribute('data-testid')
                        || element.id
                        || element.getAttribute('aria-label')
                        || element.tagName.toLowerCase());
            }
            """);

    private static Task<string> ReadActiveElementTestIdAsync(IPage page)
        => page.EvaluateAsync<string>(
            """
            () => document.activeElement?.getAttribute('data-testid')
                || document.activeElement?.closest?.('[data-testid]')?.getAttribute('data-testid')
                || document.activeElement?.id
                || ''
            """);

    private static Task<int> ReadFocusedPaletteSwatchIndexAsync(IPage page)
        => page.EvaluateAsync<int>(
            """
            () => Number(document.activeElement?.closest?.('.tm-color-palette-swatch')?.getAttribute('data-palette-index') || -1)
            """);

    private static void AssertFocusOrder(IReadOnlyList<string> focusOrder, string before, string after)
    {
        var beforeIndex = focusOrder.ToList().IndexOf(before);
        var afterIndex = focusOrder.ToList().IndexOf(after);
        Assert.IsTrue(beforeIndex >= 0, $"Focus order does not contain '{before}'. Order: {string.Join(" > ", focusOrder)}");
        Assert.IsTrue(afterIndex >= 0, $"Focus order does not contain '{after}'. Order: {string.Join(" > ", focusOrder)}");
        Assert.IsTrue(beforeIndex < afterIndex, $"Expected '{before}' before '{after}'. Order: {string.Join(" > ", focusOrder)}");
    }

    private static void AssertSelectedTextContains(string selectedText, string phrase)
    {
        Assert.IsTrue(selectedText.Contains(phrase, StringComparison.Ordinal),
            $"Selection should still contain '{phrase}', but native selection was '{selectedText}'.");
    }

    private static void AssertFontSizeNearPt(ComputedStyleProbe probe, double expectedPt)
    {
        var expectedPx = expectedPt * 96d / 72d;
        var sizes = probe.FontSizes.Select(ParsePx).Where(value => value > 0).ToArray();
        Assert.IsTrue(sizes.Length > 0, $"No computed font sizes found. Debug: {probe.Debug}");
        Assert.IsTrue(sizes.All(size => Math.Abs(size - expectedPx) <= 2.5),
            $"Expected all font sizes near {expectedPt}pt ({expectedPx:0.0}px), got {string.Join(", ", probe.FontSizes)}. Debug: {probe.Debug}");
    }

    private static void AssertColorEquals(ComputedStyleProbe probe, string property, string expectedHex)
    {
        var expectedRgb = HexToRgb(expectedHex);
        var values = property == "backgroundColor" ? probe.BackgroundColors : probe.Colors;
        Assert.IsTrue(values.Length > 0, $"No computed {property} values found. Debug: {probe.Debug}");
        Assert.IsTrue(values.All(value => ColorMatches(value, expectedRgb)),
            $"Expected all {property} values to match {expectedHex}, got {string.Join(", ", values)}. Debug: {probe.Debug}");
    }

    private static double ParsePx(string value)
    {
        var cleaned = value.Trim().Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase);
        return double.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static (int R, int G, int B) HexToRgb(string hex)
    {
        var normalized = hex.TrimStart('#');
        return (
            Convert.ToInt32(normalized[..2], 16),
            Convert.ToInt32(normalized.Substring(2, 2), 16),
            Convert.ToInt32(normalized.Substring(4, 2), 16));
    }

    private static bool ColorMatches(string cssColor, (int R, int G, int B) expected)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches(cssColor, @"\d+")
            .Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return numbers.Length >= 3
            && Math.Abs(numbers[0] - expected.R) <= 2
            && Math.Abs(numbers[1] - expected.G) <= 2
            && Math.Abs(numbers[2] - expected.B) <= 2;
    }

    private static void AssertHasBlock(JsonElement[] blocks, string id)
    {
        Assert.IsTrue(blocks.Any(block => GetString(block, "Id") == id), $"Parity seed must contain block '{id}'.");
    }

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var camel = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
            return element.TryGetProperty(camel, out value);
        }

        value = default;
        return false;
    }

    private static string SanitizeForFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }

    private sealed class PhraseMouseTarget
    {
        [JsonPropertyName("startX")] public double StartX { get; set; }
        [JsonPropertyName("startY")] public double StartY { get; set; }
        [JsonPropertyName("endX")] public double EndX { get; set; }
        [JsonPropertyName("endY")] public double EndY { get; set; }
        [JsonPropertyName("afterX")] public double AfterX { get; set; }
        [JsonPropertyName("afterY")] public double AfterY { get; set; }
        [JsonPropertyName("blockText")] public string BlockText { get; set; } = string.Empty;
    }

    private sealed class PointProbe
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
    }

    private sealed class ComputedStyleProbe
    {
        [JsonPropertyName("phrase")] public string Phrase { get; set; } = string.Empty;
        [JsonPropertyName("blockText")] public string BlockText { get; set; } = string.Empty;
        [JsonPropertyName("nodeCount")] public int NodeCount { get; set; }
        [JsonPropertyName("allFontWeightsBold")] public bool AllFontWeightsBold { get; set; }
        [JsonPropertyName("fontSizes")] public string[] FontSizes { get; set; } = [];
        [JsonPropertyName("colors")] public string[] Colors { get; set; } = [];
        [JsonPropertyName("backgroundColors")] public string[] BackgroundColors { get; set; } = [];
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class RevisionProbe
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("fragmentCount")] public int FragmentCount { get; set; }
        [JsonPropertyName("textDecorationLine")] public string TextDecorationLine { get; set; } = string.Empty;
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class CommentBoundaryProbe
    {
        [JsonPropertyName("blockText")] public string BlockText { get; set; } = string.Empty;
        [JsonPropertyName("commentAnchorText")] public string CommentAnchorText { get; set; } = string.Empty;
        [JsonPropertyName("commentAnchorOffset")] public int CommentAnchorOffset { get; set; }
        [JsonPropertyName("typedTextOffset")] public int TypedTextOffset { get; set; }
        [JsonPropertyName("typedTextInsideCommentAnchor")] public bool TypedTextInsideCommentAnchor { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class UndoStateProbe
    {
        [JsonPropertyName("canUndo")] public bool CanUndo { get; set; }
        [JsonPropertyName("canRedo")] public bool CanRedo { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
        [JsonPropertyName("redoDepth")] public int RedoDepth { get; set; }
        [JsonPropertyName("undoButtonDisabled")] public bool UndoButtonDisabled { get; set; }
        [JsonPropertyName("redoButtonDisabled")] public bool RedoButtonDisabled { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class SidePanelLayoutProbe
    {
        [JsonPropertyName("workspaceState")] public string WorkspaceState { get; set; } = string.Empty;
        [JsonPropertyName("workspaceLayout")] public string WorkspaceLayout { get; set; } = string.Empty;
        [JsonPropertyName("activeWorkspaceTab")] public string ActiveWorkspaceTab { get; set; } = string.Empty;
        [JsonPropertyName("activeSidePanelTab")] public string ActiveSidePanelTab { get; set; } = string.Empty;
        [JsonPropertyName("visiblePanelCount")] public int VisiblePanelCount { get; set; }
        [JsonPropertyName("sidePanelVisible")] public bool SidePanelVisible { get; set; }
        [JsonPropertyName("closeButtonVisible")] public bool CloseButtonVisible { get; set; }
        [JsonPropertyName("documentSurfaceNarrowed")] public bool DocumentSurfaceNarrowed { get; set; }
        [JsonPropertyName("versionPanelCount")] public int VersionPanelCount { get; set; }
        [JsonPropertyName("revisionPanelCount")] public int RevisionPanelCount { get; set; }
        [JsonPropertyName("miniToolbarVisible")] public bool MiniToolbarVisible { get; set; }
        [JsonPropertyName("miniToolbarOverlapsSidePanel")] public bool MiniToolbarOverlapsSidePanel { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class RibbonModeProbe
    {
        [JsonPropertyName("activeToolbarTab")] public string ActiveToolbarTab { get; set; } = string.Empty;
        [JsonPropertyName("activePanelTab")] public string ActivePanelTab { get; set; } = string.Empty;
        [JsonPropertyName("selectedTabTestId")] public string SelectedTabTestId { get; set; } = string.Empty;
        [JsonPropertyName("selectedTabCurrent")] public string SelectedTabCurrent { get; set; } = string.Empty;
        [JsonPropertyName("selectedTabActive")] public bool SelectedTabActive { get; set; }
        [JsonPropertyName("hasFormattingTools")] public bool HasFormattingTools { get; set; }
        [JsonPropertyName("hasReviewTools")] public bool HasReviewTools { get; set; }
        [JsonPropertyName("toolbarHeight")] public double ToolbarHeight { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class TrackChangesToggleProbe
    {
        [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
        [JsonPropertyName("toolbarState")] public string ToolbarState { get; set; } = string.Empty;
        [JsonPropertyName("ariaPressed")] public string AriaPressed { get; set; } = string.Empty;
        [JsonPropertyName("className")] public string ClassName { get; set; } = string.Empty;
        [JsonPropertyName("backgroundColor")] public string BackgroundColor { get; set; } = string.Empty;
        [JsonPropertyName("borderColor")] public string BorderColor { get; set; } = string.Empty;
        [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
        [JsonPropertyName("isActive")] public bool IsActive { get; set; }
        [JsonPropertyName("isNeutral")] public bool IsNeutral { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class LatencyHistogramProbe
    {
        [JsonPropertyName("Count")] public int Count { get; set; }
        [JsonPropertyName("LastMs")] public double LastMs { get; set; }
        [JsonPropertyName("MaxMs")] public double MaxMs { get; set; }
        [JsonPropertyName("P50Ms")] public double P50Ms { get; set; }
        [JsonPropertyName("P95Ms")] public double P95Ms { get; set; }
        [JsonPropertyName("BudgetMs")] public double BudgetMs { get; set; }
        [JsonPropertyName("WithinBudget")] public bool WithinBudget { get; set; }

        public override string ToString()
            => $"count={Count}, p50={P50Ms:0.##}ms, p95={P95Ms:0.##}ms, max={MaxMs:0.##}ms, budget={BudgetMs:0.##}ms";
    }

    private sealed class PerformanceMetricsProbe
    {
        [JsonPropertyName("FullRenderCount")] public int FullRenderCount { get; set; }
        [JsonPropertyName("PartialRenderCount")] public int PartialRenderCount { get; set; }
        [JsonPropertyName("BlazorInteropCallCount")] public int BlazorInteropCallCount { get; set; }
        [JsonPropertyName("BlazorCallbackDuringTypingCount")] public int BlazorCallbackDuringTypingCount { get; set; }
        [JsonPropertyName("FormattingStateEventCount")] public int FormattingStateEventCount { get; set; }
        [JsonPropertyName("FormattingCommandPartialRenderCount")] public int FormattingCommandPartialRenderCount { get; set; }
        [JsonPropertyName("ToolbarStateLayoutThrashCount")] public int ToolbarStateLayoutThrashCount { get; set; }
        [JsonPropertyName("LastPartialRenderScopeIds")] public string[] LastPartialRenderScopeIds { get; set; } = [];
        [JsonPropertyName("KeydownVisibleTextHistogram")] public LatencyHistogramProbe KeydownVisibleTextHistogram { get; set; } = new();
        [JsonPropertyName("SpaceVisibleTextHistogram")] public LatencyHistogramProbe SpaceVisibleTextHistogram { get; set; } = new();
        [JsonPropertyName("EnterVisibleTextHistogram")] public LatencyHistogramProbe EnterVisibleTextHistogram { get; set; } = new();
        [JsonPropertyName("ToolbarCommandVisibleStyleHistogram")] public LatencyHistogramProbe ToolbarCommandVisibleStyleHistogram { get; set; } = new();
        [JsonPropertyName("SelectionChangeToolbarStateHistogram")] public LatencyHistogramProbe SelectionChangeToolbarStateHistogram { get; set; } = new();

        public override string ToString()
            => $"full={FullRenderCount}, partial={PartialRenderCount}, interop={BlazorInteropCallCount}, typingInterop={BlazorCallbackDuringTypingCount}, formattingEvents={FormattingStateEventCount}, formattingPartial={FormattingCommandPartialRenderCount}, toolbarThrash={ToolbarStateLayoutThrashCount}, lastScopes=[{string.Join(",", LastPartialRenderScopeIds)}], key={KeydownVisibleTextHistogram}, space={SpaceVisibleTextHistogram}, enter={EnterVisibleTextHistogram}, toolbar={ToolbarCommandVisibleStyleHistogram}, selection={SelectionChangeToolbarStateHistogram}";
    }

    private sealed class MixedMarkupPerformanceProbe
    {
        [JsonPropertyName("hasComment")] public bool HasComment { get; set; }
        [JsonPropertyName("hasRevisionInsert")] public bool HasRevisionInsert { get; set; }
        [JsonPropertyName("hasBold")] public bool HasBold { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class OnlyOfficeParityDebugArtifact
    {
        [JsonPropertyName("selectedText")] public string SelectedText { get; set; } = string.Empty;
        [JsonPropertyName("activeElement")] public string ActiveElement { get; set; } = string.Empty;
        [JsonPropertyName("toolbarHtml")] public string ToolbarHtml { get; set; } = string.Empty;
        [JsonPropertyName("miniToolbarHtml")] public string MiniToolbarHtml { get; set; } = string.Empty;
        [JsonPropertyName("runtimeState")] public string RuntimeState { get; set; } = string.Empty;
        [JsonPropertyName("formattingState")] public string FormattingState { get; set; } = string.Empty;
        [JsonPropertyName("undoStack")] public string UndoStack { get; set; } = string.Empty;
    }
}
