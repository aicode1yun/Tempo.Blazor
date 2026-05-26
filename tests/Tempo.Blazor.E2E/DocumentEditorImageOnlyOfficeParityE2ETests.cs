using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>ONLYOFFICE-level RED baseline tests for image wrapping, focus, insertion, drag, and resize behavior.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:Images")]
[TestCategory("DocumentEditor:HumanWorkflow")]
[DoNotParallelize]
public sealed class DocumentEditorImageOnlyOfficeParityE2ETests : DocumentEditorE2ETestBase
{
    private const string DocumentId = "onlyoffice-parity-2026-05-24";
    private const string NormalParagraphId = "onlyoffice-image-normal-paragraph";
    private const string EmptyParagraphId = "onlyoffice-image-empty-paragraph";
    private const string InsertionParagraphId = "onlyoffice-image-insertion-paragraph";
    private const string LeftSquareImageId = "recovery-left-wrap-image";
    private const string LeftSquareAnchorId = "recovery-left-wrap-text";
    private const string RightSquareImageId = "recovery-right-wrap-image";
    private const string RightSquareAnchorId = "recovery-right-wrap-text";
    private const string TopBottomImageId = "recovery-top-bottom-image";
    private const string TinyPngDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [TestMethod]
    public async Task ImageOnlyOfficeParity_SeedContainsPhase0ImageScenariosAndTargetsNewDrawingModel()
    {
        using var response = await LoadOnlyOfficeParityDocumentFromApiAsync();
        var snapshot = GetString(response.RootElement, "JsonSnapshot");
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot), "Image parity API response must include JsonSnapshot.");

        using var documentJson = JsonDocument.Parse(snapshot!);
        var document = documentJson.RootElement;
        var blocks = GetArray(document, "Blocks").ToArray();
        var headersFooters = GetArray(document, "HeadersFooters").ToArray();
        var issues = new List<string>();

        RequireBlock(blocks, NormalParagraphId, issues);
        RequireBlock(blocks, EmptyParagraphId, issues);
        RequireBlock(blocks, InsertionParagraphId, issues);
        RequireBlock(blocks, LeftSquareAnchorId, issues);
        RequireBlock(blocks, RightSquareAnchorId, issues);
        RequireBlock(blocks, TopBottomImageId, issues);
        if (!blocks.Any(IsTableBlock))
        {
            issues.Add("Seed must include a table/cell scenario.");
        }

        if (headersFooters.Length < 2)
        {
            issues.Add("Seed must include header and footer regions.");
        }

        var topLevelImages = blocks.Count(IsImageBlock);
        if (topLevelImages != 0)
        {
            issues.Add($"Target seed must not contain top-level image blocks; found {topLevelImages}.");
        }

        var drawingRuns = CountDrawingRuns(document);
        if (drawingRuns < 4)
        {
            issues.Add($"Target seed must contain inline/floating drawing runs; found {drawingRuns}.");
        }

        var drawingWrapModes = ReadDrawingWrapModes(document).ToArray();
        foreach (var required in new[] { "Inline", "Square", "TopBottom", "BehindText", "InFrontOfText" })
        {
            if (!drawingWrapModes.Contains(required, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add($"Target seed must contain drawing wrap mode '{required}'. Modes: {string.Join(", ", drawingWrapModes)}");
            }
        }

        if (issues.Count > 0)
        {
            Assert.Fail("Image parity seed is still on the old image-block baseline:\n" + string.Join("\n", issues));
        }
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ArrowDownFromTextBeforeSquareImageKeepsTextSelection()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickDocumentEditorBlockOffsetAsync(page, LeftSquareAnchorId, 0);
        await page.Keyboard.PressAsync("ArrowDown");
        await WaitForEditorStableAsync(page, "image parity arrow down around square image", LeftSquareAnchorId);

        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        Assert.AreEqual("Text", diagnostics.SelectionMode, diagnostics.Debug);
        Assert.AreEqual(string.Empty, diagnostics.ActiveImageId, diagnostics.Debug);
        Assert.IsFalse(diagnostics.ImageToolbarVisible, diagnostics.Debug);
        await AssertDocumentEditorHostHasFocusAsync(page);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ArrowDownFromTextBeforeSquareImageKeepsTextSelection));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ArrowUpFromTextAfterSquareImageKeepsTextSelection()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var text = await ReadDocumentEditorBlockTextAsync(page, RightSquareAnchorId);
        await ClickDocumentEditorBlockOffsetAsync(page, RightSquareAnchorId, Math.Max(0, text.Length - 1));
        await page.Keyboard.PressAsync("ArrowUp");
        await WaitForEditorStableAsync(page, "image parity arrow up around square image", RightSquareAnchorId);

        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, RightSquareImageId);
        Assert.AreEqual("Text", diagnostics.SelectionMode, diagnostics.Debug);
        Assert.AreEqual(string.Empty, diagnostics.ActiveImageId, diagnostics.Debug);
        Assert.IsFalse(diagnostics.ImageToolbarVisible, diagnostics.Debug);
        await AssertDocumentEditorHostHasFocusAsync(page);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ArrowUpFromTextAfterSquareImageKeepsTextSelection));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ClickBesideSquareImagePlacesTextCaret()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var anchor = await ReadDocumentEditorImageAnchorAsync(page, LeftSquareImageId);
        var imageRect = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        Assert.IsTrue(imageRect.Width > 0 && imageRect.Height > 0,
            "The square image must be visible before the caret hit-test scenario can run.");

        await page.Mouse.ClickAsync((float)(imageRect.X + imageRect.Width + 28), (float)(imageRect.Y + imageRect.Height / 2));
        await WaitForEditorStableAsync(page, "click beside square image places caret", anchor.AnchorBlockId);

        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        Assert.AreEqual("Text", diagnostics.SelectionMode, diagnostics.Debug);
        Assert.AreEqual(string.Empty, diagnostics.ActiveImageId, diagnostics.Debug);
        Assert.AreEqual(anchor.AnchorBlockId, diagnostics.CaretBlockId, diagnostics.Debug);
        Assert.IsTrue(diagnostics.CaretOffset >= 0, diagnostics.Debug);
        Assert.IsFalse(diagnostics.ImageToolbarVisible, diagnostics.Debug);
        await AssertDocumentEditorHostHasFocusAsync(page);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ClickBesideSquareImagePlacesTextCaret));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_InsertImageAtCaretCreatesDrawingRunNotTopLevelBlock()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var beforeTopLevelImages = await ReadDocumentEditorTopLevelImageBlockCountAsync(page);
        var beforeDrawingRuns = await ReadDocumentEditorDrawingRunsAsync(page);
        var beforeDrawingRunIds = beforeDrawingRuns.Select(run => run.ObjectId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        var beforeTargetDrawingRuns = await ReadDocumentEditorDrawingRunsAsync(page, InsertionParagraphId);
        var beforeText = await ReadDocumentEditorModelBlockTextAsync(page, InsertionParagraphId);

        await ClickDocumentEditorBlockOffsetAsync(page, InsertionParagraphId, "Image insertion before ".Length);
        await page.GetByTestId("document-ribbon-tab-insert").ClickAsync();
        await page.GetByTestId("document-toolbar-image").ClickAsync();
        await page.GetByTestId("document-image-insert-url").ClickAsync();
        await page.GetByTestId("document-wysiwyg-image-url-input").FillAsync(TinyPngDataUrl);
        await page.GetByTestId("document-wysiwyg-image-alt-input").FillAsync("ONLYOFFICE parity inserted drawing");
        await page.GetByTestId("document-wysiwyg-insert-image-url").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-wysiwyg-image-dialog")).ToBeHiddenAsync(new() { Timeout = 10000 });
        await WaitForEditorStableAsync(page, "insert image at caret as drawing run", InsertionParagraphId);

        var afterTopLevelImages = await ReadDocumentEditorTopLevelImageBlockCountAsync(page);
        var afterTargetDrawingRuns = await ReadDocumentEditorDrawingRunsAsync(page, InsertionParagraphId);
        var afterInsertDiagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: DocumentEditorHostSelector);
        var insertedObjectId = afterInsertDiagnostics.ActiveImageId;
        var afterDrawingRunIds = (await ReadDocumentEditorDrawingRunsAsync(page))
            .Select(run => run.ObjectId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var missingOriginalIds = beforeDrawingRunIds.Except(afterDrawingRunIds, StringComparer.Ordinal).ToArray();
        var insertedRun = afterTargetDrawingRuns.FirstOrDefault(run => string.Equals(run.ObjectId, insertedObjectId, StringComparison.Ordinal));
        Assert.AreEqual(beforeTopLevelImages, afterTopLevelImages,
            $"Insert image must not create a new top-level image block. {afterInsertDiagnostics.Debug}");
        Assert.IsFalse(string.IsNullOrWhiteSpace(insertedObjectId),
            $"Insert image must leave the new drawing object selected. {afterInsertDiagnostics.Debug}");
        Assert.IsNotNull(insertedRun,
            $"Insert image must create a drawing run in the caret paragraph. Before target={beforeTargetDrawingRuns.Length}, after target={afterTargetDrawingRuns.Length}, object={insertedObjectId}. {afterInsertDiagnostics.Debug}");
        Assert.AreEqual(InsertionParagraphId, insertedRun!.AnchorBlockId,
            $"Inserted drawing must be anchored to the caret paragraph. {afterInsertDiagnostics.Debug}");
        Assert.AreEqual("ONLYOFFICE parity inserted drawing", insertedRun.AltText,
            "Inserted drawing must preserve alt text from the URL dialog.");
        Assert.AreEqual(0, missingOriginalIds.Length,
            $"Insert image must not remove existing drawing runs. Missing: {string.Join(", ", missingOriginalIds)}");
        Assert.IsTrue(afterTargetDrawingRuns.Length > beforeTargetDrawingRuns.Length,
            $"Target paragraph drawing count must increase. Before={beforeTargetDrawingRuns.Length}, after={afterTargetDrawingRuns.Length}.");
        Assert.AreEqual(beforeText, await ReadDocumentEditorModelBlockTextAsync(page, InsertionParagraphId),
            "Text before and after the inserted drawing must stay in the same paragraph.");

        var inlineFlow = await ReadInlineDrawingFlowProbeAsync(page, InsertionParagraphId, insertedObjectId);
        Assert.IsTrue(inlineFlow.Exists, $"Inserted drawing must render in the live DOM. {inlineFlow.Debug}");
        Assert.IsTrue(inlineFlow.AnchorExists, $"Inserted drawing must have a text-layer anchor. {inlineFlow.Debug}");
        Assert.IsTrue(inlineFlow.InsideTargetParagraph, $"Inserted drawing anchor must stay in the caret paragraph. {inlineFlow.Debug}");
        Assert.IsTrue(inlineFlow.InObjectLayer, $"Inserted drawing must be rendered from the object layer. {inlineFlow.Debug}");
        Assert.AreEqual("absolute", inlineFlow.Position, $"Phase 12 object layer owns editable image positioning. {inlineFlow.Debug}");
        Assert.IsTrue(inlineFlow.Width > 0 && inlineFlow.Height > 0, $"Inline drawing must have a visible size. {inlineFlow.Debug}");
        Assert.IsTrue(inlineFlow.HasTextBefore && inlineFlow.HasTextAfter, $"Inserted drawing must stay between text before and after it. {inlineFlow.Debug}");
        Assert.IsTrue(inlineFlow.SameLineAsAdjacentText, $"Inserted drawing anchor should share the line with adjacent text when there is room. {inlineFlow.Debug}");

        await page.Keyboard.PressAsync("Control+Z");
        await WaitForEditorStableAsync(page, "undo inserted image drawing run", InsertionParagraphId);
        Assert.AreEqual(beforeTopLevelImages, await ReadDocumentEditorTopLevelImageBlockCountAsync(page),
            "Undo must not introduce top-level image blocks.");
        Assert.AreEqual(0, (await ReadDocumentEditorDrawingRunsAsync(page, InsertionParagraphId, insertedObjectId)).Length,
            "Undo must remove the inserted drawing run in one step.");
        Assert.AreEqual(beforeText, await ReadDocumentEditorModelBlockTextAsync(page, InsertionParagraphId),
            "Undo must preserve the original paragraph text.");

        await page.Keyboard.PressAsync("Control+Y");
        await WaitForEditorStableAsync(page, "redo inserted image drawing run", InsertionParagraphId);
        Assert.AreEqual(beforeTopLevelImages, await ReadDocumentEditorTopLevelImageBlockCountAsync(page),
            "Redo must still avoid top-level image blocks.");
        Assert.AreEqual(1, (await ReadDocumentEditorDrawingRunsAsync(page, InsertionParagraphId, insertedObjectId)).Length,
            "Redo must restore the drawing run in one step.");
        Assert.AreEqual(beforeText, await ReadDocumentEditorModelBlockTextAsync(page, InsertionParagraphId),
            "Redo must keep paragraph text around the drawing run.");

        await SaveOnlyOfficeParityDocumentAsync(page);
        await ReloadOnlyOfficeParityDocumentAsync(page);
        Assert.AreEqual(beforeTopLevelImages, await ReadDocumentEditorTopLevelImageBlockCountAsync(page),
            "Save/reload must not migrate the inserted drawing run back to a top-level image block.");
        Assert.AreEqual(1, (await ReadDocumentEditorDrawingRunsAsync(page, InsertionParagraphId, insertedObjectId)).Length,
            "Save/reload must preserve the inserted drawing run.");
        Assert.AreEqual(beforeText, await ReadDocumentEditorModelBlockTextAsync(page, InsertionParagraphId),
            "Save/reload must preserve the paragraph text around the drawing run.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_InsertImageAtCaretCreatesDrawingRunNotTopLevelBlock));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_EmptyWrappedSpaceAcceptsTextCaretAndTyping()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "typed beside empty wrapped image";

        var beforeTopLevelImages = await ReadDocumentEditorTopLevelImageBlockCountAsync(page);
        var targetBlockId = LeftSquareAnchorId;
        var beforeText = await ReadDocumentEditorModelBlockTextAsync(page, targetBlockId);
        var imageRect = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        Assert.IsTrue(imageRect.Width > 0 && imageRect.Height > 0, "The square image must be visible before the wrapped-space typing baseline can run.");
        var clickPoint = await ReadWrappedTextIntervalClickPointAsync(page, LeftSquareImageId, targetBlockId);

        await page.Mouse.ClickAsync((float)clickPoint.X, (float)clickPoint.Y);
        await page.Keyboard.TypeAsync(typed);
        await WaitForEditorStableAsync(page, "type beside empty wrapped image", targetBlockId, typed);

        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        var afterRuns = await ReadDocumentEditorDrawingRunsAsync(page, objectId: LeftSquareImageId);
        Assert.AreEqual("Text", diagnostics.SelectionMode, diagnostics.Debug);
        Assert.AreEqual(targetBlockId, diagnostics.CaretBlockId, diagnostics.Debug);
        Assert.AreEqual(beforeTopLevelImages, await ReadDocumentEditorTopLevelImageBlockCountAsync(page),
            "Typing next to a drawing-only paragraph must not create a top-level image wrapper paragraph.");
        Assert.AreEqual(1, afterRuns.Length,
            $"The original drawing run must stay anchored in the same paragraph. {diagnostics.Debug}");
        Assert.IsTrue((await ReadDocumentEditorModelBlockTextAsync(page, targetBlockId)).Contains(typed, StringComparison.Ordinal),
            diagnostics.Debug);
        Assert.IsTrue(diagnostics.LineIntervals.Length > 0, diagnostics.Debug);

        await page.Keyboard.PressAsync("Control+Z");
        await WaitForEditorStableAsync(page, "undo typing beside empty wrapped image", targetBlockId);
        Assert.AreEqual(beforeText, await ReadDocumentEditorModelBlockTextAsync(page, targetBlockId),
            "Undo must remove only the typed text beside the image.");
        Assert.AreEqual(1, (await ReadDocumentEditorDrawingRunsAsync(page, objectId: LeftSquareImageId)).Length,
            "Undo must not remove the drawing run.");

        await page.Keyboard.PressAsync("Control+Y");
        await WaitForEditorStableAsync(page, "redo typing beside empty wrapped image", targetBlockId, typed);
        Assert.IsTrue((await ReadDocumentEditorModelBlockTextAsync(page, targetBlockId)).Contains(typed, StringComparison.Ordinal),
            "Redo must restore the typed text beside the image.");

        await SaveOnlyOfficeParityDocumentAsync(page);
        await ReloadOnlyOfficeParityDocumentAsync(page);
        Assert.AreEqual(beforeTopLevelImages, await ReadDocumentEditorTopLevelImageBlockCountAsync(page),
            "Save/reload must not create a top-level image wrapper paragraph.");
        Assert.AreEqual(1, (await ReadDocumentEditorDrawingRunsAsync(page, objectId: LeftSquareImageId)).Length,
            "Save/reload must preserve the drawing run anchored to the typed paragraph.");
        Assert.IsTrue((await ReadDocumentEditorModelBlockTextAsync(page, targetBlockId)).Contains(typed, StringComparison.Ordinal),
            "Save/reload must preserve text typed beside the image.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_EmptyWrappedSpaceAcceptsTextCaretAndTyping));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_TypingLeftOfRightSquareImageCreatesTextInSameDrawingParagraph()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string typed = "typed left of right image";

        var beforeTopLevelImages = await ReadDocumentEditorTopLevelImageBlockCountAsync(page);
        var targetBlockId = RightSquareAnchorId;
        var imageRect = await ReadDocumentEditorImageRectAsync(page, RightSquareImageId);
        Assert.IsTrue(imageRect.Width > 0 && imageRect.Height > 0, "The right square image must be visible before the wrapped-space typing baseline can run.");
        var clickPoint = await ReadWrappedTextIntervalClickPointAsync(page, RightSquareImageId, targetBlockId);

        await page.Mouse.ClickAsync((float)clickPoint.X, (float)clickPoint.Y);
        await page.Keyboard.TypeAsync(typed);
        await WaitForEditorStableAsync(page, "type left of right square image", targetBlockId, typed);

        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, RightSquareImageId);
        Assert.AreEqual("Text", diagnostics.SelectionMode, diagnostics.Debug);
        Assert.AreEqual(targetBlockId, diagnostics.CaretBlockId, diagnostics.Debug);
        Assert.AreEqual(beforeTopLevelImages, await ReadDocumentEditorTopLevelImageBlockCountAsync(page),
            "Typing left of a right-positioned image must not create a top-level image wrapper paragraph.");
        Assert.AreEqual(1, (await ReadDocumentEditorDrawingRunsAsync(page, objectId: RightSquareImageId)).Length,
            $"The original right-positioned drawing run must stay anchored. {diagnostics.Debug}");
        Assert.IsTrue((await ReadDocumentEditorModelBlockTextAsync(page, targetBlockId)).Contains(typed, StringComparison.Ordinal),
            diagnostics.Debug);
        Assert.IsTrue(diagnostics.LineIntervals.Length > 0, diagnostics.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_TypingLeftOfRightSquareImageCreatesTextInSameDrawingParagraph));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ImageClickSelectsObjectAndEscapeReturnsCaretToAnchor()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var anchor = await ReadDocumentEditorImageAnchorAsync(page, LeftSquareImageId);

        await ClickImageCenterAsync(page, LeftSquareImageId);
        var selected = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        Assert.AreEqual("Object", selected.SelectionMode, selected.Debug);
        Assert.AreEqual(LeftSquareImageId, selected.ActiveImageId, selected.Debug);

        await page.Keyboard.PressAsync("Escape");
        await WaitForEditorStableAsync(page, "escape image selection returns caret", anchor.AnchorBlockId);

        var afterEscape = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        Assert.AreEqual("Text", afterEscape.SelectionMode, afterEscape.Debug);
        Assert.AreEqual(string.Empty, afterEscape.ActiveImageId, afterEscape.Debug);
        Assert.AreEqual(anchor.AnchorBlockId, afterEscape.CaretBlockId, afterEscape.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ImageClickSelectsObjectAndEscapeReturnsCaretToAnchor));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ImageToolbarDoesNotOverlapReadableText()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickImageCenterAsync(page, LeftSquareImageId);
        await WaitForEditorStableAsync(page, "image toolbar placement", LeftSquareAnchorId);

        var probe = await ReadImageToolbarOverlapProbeAsync(page);
        Assert.IsTrue(probe.ToolbarVisible, probe.Debug);
        Assert.IsFalse(probe.OverlapsReadableText, probe.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ImageToolbarDoesNotOverlapReadableText));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_DragPreviewIsVisibleBeforePointerUpAndDoesNotCommit()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        await ScrollImageIntoViewAsync(page, LeftSquareImageId);

        var rect = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        var before = await ReadImageDragTrackPreviewProbeAsync(page, LeftSquareImageId);
        var startX = rect.X + rect.Width / 2;
        var startY = rect.Y + rect.Height / 2;

        await page.Mouse.MoveAsync((float)startX, (float)startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(startX + 72), (float)(startY + 18), new() { Steps = 8 });
        var preview = await WaitForImageDragTrackPreviewAsync(page, LeftSquareImageId);

        Assert.IsTrue(preview.Active, preview.Debug);
        Assert.IsTrue(Math.Abs(preview.Dx) > 1 || Math.Abs(preview.Dy) > 1, preview.Debug);
        Assert.AreEqual(before.CommandCount, preview.CommandCount,
            $"Pointermove must not commit a document command. {preview.Debug}");
        Assert.AreEqual(before.UndoDepth, preview.UndoDepth,
            $"Pointermove must not push undo entries. {preview.Debug}");

        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(100);
        var afterCancel = await ReadImageDragTrackPreviewProbeAsync(page, LeftSquareImageId);

        Assert.IsFalse(afterCancel.Active, afterCancel.Debug);
        Assert.AreEqual(before.CommandCount, afterCancel.CommandCount,
            $"Escape during drag must cancel preview without committing. {afterCancel.Debug}");
        Assert.AreEqual(before.UndoDepth, afterCancel.UndoDepth,
            $"Escape during drag must not push undo entries. {afterCancel.Debug}");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_DragPreviewIsVisibleBeforePointerUpAndDoesNotCommit));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ResizePreviewIsVisibleBeforePointerUpAndDoesNotCommit()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickImageCenterAsync(page, LeftSquareImageId);
        var before = await ReadImageResizeTrackPreviewProbeAsync(page, LeftSquareImageId);
        var handle = await ReadImageResizeHandleCenterAsync(page, LeftSquareImageId);

        await page.Mouse.MoveAsync((float)handle.X, (float)handle.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(handle.X + 58), (float)(handle.Y + 34), new() { Steps = 10 });
        var preview = await WaitForImageResizeTrackPreviewAsync(page, LeftSquareImageId);

        Assert.IsTrue(preview.Active, preview.Debug);
        Assert.AreEqual("resize", preview.Mode, preview.Debug);
        Assert.IsTrue(preview.PreviewWidth > 0 && preview.PreviewHeight > 0, preview.Debug);
        Assert.IsTrue(preview.BadgeVisible, preview.Debug);
        Assert.IsTrue(preview.BadgeText.Contains('x', StringComparison.Ordinal), preview.Debug);
        Assert.AreEqual(before.CommandCount, preview.CommandCount,
            $"Pointermove resize must not commit a document command. {preview.Debug}");
        Assert.AreEqual(before.UndoDepth, preview.UndoDepth,
            $"Pointermove resize must not push undo entries. {preview.Debug}");

        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(100);
        var afterCancel = await ReadImageResizeTrackPreviewProbeAsync(page, LeftSquareImageId);

        Assert.IsFalse(afterCancel.Active, afterCancel.Debug);
        Assert.AreEqual(before.CommandCount, afterCancel.CommandCount,
            $"Escape during resize must cancel preview without committing. {afterCancel.Debug}");
        Assert.AreEqual(before.UndoDepth, afterCancel.UndoDepth,
            $"Escape during resize must not push undo entries. {afterCancel.Debug}");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ResizePreviewIsVisibleBeforePointerUpAndDoesNotCommit));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ImageToolbarWrapModeUpdatesSelectedDrawingObject()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var before = await ReadDrawingObjectStateAsync(page, LeftSquareImageId);
        Assert.IsTrue(before.Found, before.Debug);

        await ClickImageCenterAsync(page, LeftSquareImageId);
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.GetByTestId("document-image-wrap-tight").ClickAsync();
        var after = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.WrapMode, "Tight", StringComparison.Ordinal),
            "toolbar wrap mode to Tight");

        Assert.AreEqual("Tight", after.WrapMode, after.Debug);
        Assert.AreEqual(LeftSquareImageId, (await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId)).ActiveImageId);

        await page.Keyboard.PressAsync("Control+Z");
        var afterUndo = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.WrapMode, before.WrapMode, StringComparison.Ordinal),
            "undo toolbar wrap mode");
        Assert.AreEqual(before.WrapMode, afterUndo.WrapMode, afterUndo.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ImageToolbarWrapModeUpdatesSelectedDrawingObject));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ImageToolbarWrapModePersistsAfterSaveReload()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickImageCenterAsync(page, LeftSquareImageId);
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.GetByTestId("document-image-wrap-top-bottom").ClickAsync();
        var afterEdit = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.WrapMode, "TopBottom", StringComparison.Ordinal),
            "toolbar wrap mode to TopBottom before save");
        Assert.AreEqual("TopBottom", afterEdit.WrapMode, afterEdit.Debug);
        await Assertions.Expect(page.GetByTestId("document-image-wrap-top-bottom")).ToHaveAttributeAsync("aria-pressed", "true");

        await SaveOnlyOfficeParityDocumentAsync(page);
        await ReloadOnlyOfficeParityDocumentAsync(page);
        var afterReload = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.WrapMode, "TopBottom", StringComparison.Ordinal),
            "persisted toolbar wrap mode after reload");

        Assert.AreEqual("TopBottom", afterReload.WrapMode, afterReload.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ImageToolbarWrapModePersistsAfterSaveReload));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ImageInspectorSizeUpdatesSelectedDrawingObject()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var before = await ReadDrawingObjectStateAsync(page, LeftSquareImageId);
        Assert.IsTrue(before.Found, before.Debug);
        var targetWidth = Math.Round(before.Width + 37);

        await ClickImageCenterAsync(page, LeftSquareImageId);
        await EnsureImageInspectorVisibleAsync(page);
        var widthInput = page.GetByTestId("document-image-inspector-width");
        await widthInput.FillAsync(targetWidth.ToString("0"));
        await widthInput.PressAsync("Tab");

        var after = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => Math.Abs(state.Width - targetWidth) <= 0.5,
            "inspector width change");

        Assert.AreEqual(targetWidth, after.Width, 0.5, after.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ImageInspectorSizeUpdatesSelectedDrawingObject));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ImageInspectorAltTextPersistsOnDrawingObject()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var altText = $"Phase 13 drawing alt {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        await ClickImageCenterAsync(page, LeftSquareImageId);
        await EnsureImageInspectorVisibleAsync(page);
        var altInput = page.GetByTestId("document-image-inspector-alt");
        await altInput.FillAsync(altText);
        await altInput.PressAsync("Tab");

        var afterEdit = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.AltText, altText, StringComparison.Ordinal),
            "inspector alt text change");
        Assert.AreEqual(altText, afterEdit.AltText, afterEdit.Debug);

        await SaveOnlyOfficeParityDocumentAsync(page);
        await ReloadOnlyOfficeParityDocumentAsync(page);
        var afterReload = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.AltText, altText, StringComparison.Ordinal),
            "reload persisted drawing alt text");
        Assert.AreEqual(altText, afterReload.AltText, afterReload.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ImageInspectorAltTextPersistsOnDrawingObject));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_DragImageReanchorsToNearestParagraphAndUndoRestoresAnchor()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var beforeAnchor = await ReadDocumentEditorImageAnchorAsync(page, LeftSquareImageId);

        await DragImageCenterToBlockAsync(page, LeftSquareImageId, NormalParagraphId);
        await WaitForEditorStableAsync(page, "drag image to nearest paragraph", NormalParagraphId);

        var afterDragDiagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        Assert.AreEqual(NormalParagraphId, afterDragDiagnostics.AnchorBlockId,
            $"Drop must reanchor the image to the nearest paragraph. Before={beforeAnchor.AnchorBlockId}, after={afterDragDiagnostics.AnchorBlockId}. {afterDragDiagnostics.Debug}");

        await page.Keyboard.PressAsync("Control+Z");
        await WaitForEditorStableAsync(page, "undo image drag reanchor", beforeAnchor.AnchorBlockId);
        var afterUndo = await ReadDocumentEditorImageAnchorAsync(page, LeftSquareImageId);
        Assert.AreEqual(beforeAnchor.AnchorBlockId, afterUndo.AnchorBlockId,
            $"Undo must restore the original image anchor. Before={beforeAnchor.AnchorBlockId}, after undo={afterUndo.AnchorBlockId}");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_DragImageReanchorsToNearestParagraphAndUndoRestoresAnchor));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_DragImageReanchorPersistsAfterSaveReload()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await DragImageCenterToBlockAsync(page, LeftSquareImageId, NormalParagraphId);
        await WaitForEditorStableAsync(page, "drag image reanchor before save", NormalParagraphId);
        var afterDrag = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.AnchorBlockId, NormalParagraphId, StringComparison.Ordinal),
            "dragged image anchor before save");
        Assert.AreEqual(NormalParagraphId, afterDrag.AnchorBlockId, afterDrag.Debug);

        await SaveOnlyOfficeParityDocumentAsync(page);
        await ReloadOnlyOfficeParityDocumentAsync(page);
        var afterReload = await WaitForDrawingObjectStateAsync(
            page,
            LeftSquareImageId,
            state => string.Equals(state.AnchorBlockId, NormalParagraphId, StringComparison.Ordinal),
            "persisted dragged image anchor after reload");

        Assert.AreEqual(NormalParagraphId, afterReload.AnchorBlockId, afterReload.Debug);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_DragImageReanchorPersistsAfterSaveReload));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_ResizeImageUndoRestoresSizeWithSingleStep()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var before = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);

        await DragImageResizeHandleAsync(page, LeftSquareImageId, 48, 28);
        await WaitForEditorStableAsync(page, "resize image once", LeftSquareAnchorId);
        var resized = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        var resizedDiagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, LeftSquareImageId);
        Assert.IsTrue(resized.Width > before.Width + 8 || resized.Height > before.Height + 8,
            $"Resize must visibly change image size. Before={FormatRect(before)}, after={FormatRect(resized)}. {resizedDiagnostics.Debug}");

        await page.Keyboard.PressAsync("Control+Z");
        await WaitForEditorStableAsync(page, "undo image resize once", LeftSquareAnchorId);
        var afterUndo = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        AssertRectNear(before, afterUndo, 3, "A single undo must restore the original image size.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_ResizeImageUndoRestoresSizeWithSingleStep));
    }

    [TestMethod]
    public async Task ImageOnlyOfficeParity_PerformanceBudget_TypingResizeAndUndoStayResponsive()
    {
        var page = await OpenOnlyOfficeParityDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var typed = $"phase16wrap{DateTimeOffset.UtcNow:HHmmssfff}";

        await ScrollImageIntoViewAsync(page, LeftSquareImageId);
        var clickPoint = await ReadWrappedTextIntervalClickPointAsync(page, LeftSquareImageId, LeftSquareAnchorId);
        await ClearImagePerformanceMetricsAsync(page);
        await page.Mouse.ClickAsync((float)clickPoint.X, (float)clickPoint.Y);
        var typingStarted = DateTimeOffset.UtcNow;
        await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
        await WaitForEditorStableAsync(page, "phase 16 type beside wrapped image", LeftSquareAnchorId, typed);
        var typingElapsed = DateTimeOffset.UtcNow - typingStarted;

        var typingMetrics = await ReadImagePerformanceMetricsAsync(page);
        Assert.AreEqual(0, typingMetrics.FullRenderCount,
            $"Typing next to a wrapped image must stay on the partial JS-owned path. Metrics: {typingMetrics}");
        Assert.IsTrue(typingElapsed < TimeSpan.FromSeconds(8),
            $"Typing next to a wrapped image took too long: {typingElapsed.TotalMilliseconds:0} ms. Metrics: {typingMetrics}");
        if (typingMetrics.InputDomApplyCount > 0 || typingMetrics.InputOperationCount > 0)
        {
            Assert.IsTrue(typingMetrics.PartialRenderCount > 0,
                $"Typing next to a wrapped image must patch visible DOM incrementally. Metrics: {typingMetrics}");
            Assert.IsTrue(typingMetrics.MaxInputLatencyMs < 600,
                $"Typing next to a wrapped image exceeded the visible-input budget. Metrics: {typingMetrics}");
        }

        await ClickImageCenterAsync(page, LeftSquareImageId);
        await ClearImagePerformanceMetricsAsync(page);
        var beforeResize = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        await DragImageResizeHandleAsync(page, LeftSquareImageId, 40, 24);
        await WaitForEditorStableAsync(page, "phase 16 resize wrapped image", LeftSquareAnchorId);

        var resizeMetrics = await WaitForImagePerformanceMetricsAsync(
            page,
            metrics => metrics.ObjectTrackResizeFrameCount > 0 && metrics.ObjectTrackResizeCommitCount > 0,
            "resize wrapped image preview");
        Assert.IsTrue(resizeMetrics.ObjectTrackResizeFrameCount >= 2,
            $"Resize preview must produce tracked frames rather than a silent final jump. Metrics: {resizeMetrics}");
        Assert.AreEqual(1, resizeMetrics.ObjectTrackResizeCommitCount,
            $"Resize must commit once for a single undo step. Metrics: {resizeMetrics}");
        if (resizeMetrics.ImageDragLatencyCount > 0)
        {
            Assert.IsTrue(resizeMetrics.MaxImageDragLatencyMs < 250,
                $"Resize commit exceeded the image operation budget. Metrics: {resizeMetrics}");
        }

        await ClearImagePerformanceMetricsAsync(page);
        var undoStarted = DateTimeOffset.UtcNow;
        await page.Keyboard.PressAsync("Control+Z");
        await WaitForEditorStableAsync(page, "phase 16 undo wrapped image resize", LeftSquareAnchorId);
        var undoElapsed = DateTimeOffset.UtcNow - undoStarted;
        var afterUndo = await ReadDocumentEditorImageRectAsync(page, LeftSquareImageId);
        var undoMetrics = await ReadImagePerformanceMetricsAsync(page);

        AssertRectNear(beforeResize, afterUndo, 3, "Undo after resize must restore the original wrapped image size.");
        Assert.IsTrue(undoElapsed < TimeSpan.FromSeconds(4),
            $"Undo after image resize took too long: {undoElapsed.TotalMilliseconds:0} ms. Metrics: {undoMetrics}");
        Assert.IsTrue(undoMetrics.FullRenderCount <= 1,
            $"Undo after image resize must avoid repeated full render work. Metrics: {undoMetrics}");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(ImageOnlyOfficeParity_PerformanceBudget_TypingResizeAndUndoStayResponsive));
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

    private static async Task SaveOnlyOfficeParityDocumentAsync(IPage page)
    {
        var save = page.GetByTestId("document-save");
        if (!await save.IsVisibleAsync())
        {
            await page.GetByTestId("document-ribbon-tab-home").ClickAsync();
        }

        await page.GetByTestId("document-save").ClickAsync();
        var dirty = page.GetByTestId("document-dirty-status");
        if (await dirty.CountAsync() > 0)
        {
            await Assertions.Expect(dirty).ToBeHiddenAsync(new() { Timeout = 10000 });
        }
    }

    private static async Task ReloadOnlyOfficeParityDocumentAsync(IPage page)
    {
        await page.ReloadAsync(new()
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
    }

    private static async Task EnsureImageInspectorVisibleAsync(IPage page)
    {
        var panel = page.GetByTestId("document-image-properties-panel");
        if (await panel.CountAsync() == 0 || !await panel.IsVisibleAsync())
        {
            var moreOptions = page.GetByTestId("document-image-more-options");
            if (await moreOptions.CountAsync() > 0 && await moreOptions.IsVisibleAsync())
            {
                await moreOptions.ClickAsync();
            }

            var propertiesTab = page.GetByTestId("document-side-panel-tab-properties");
            if (await propertiesTab.CountAsync() > 0)
            {
                await propertiesTab.ClickAsync();
            }
        }

        await Assertions.Expect(panel).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    private static async Task<DrawingObjectStateProbe> WaitForDrawingObjectStateAsync(
        IPage page,
        string objectId,
        Func<DrawingObjectStateProbe, bool> predicate,
        string description)
    {
        DrawingObjectStateProbe? latest = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = await ReadDrawingObjectStateAsync(page, objectId);
            if (predicate(latest))
            {
                return latest;
            }

            await page.WaitForTimeoutAsync(100);
        }

        latest ??= await ReadDrawingObjectStateAsync(page, objectId);
        Assert.Fail($"Timed out waiting for {description}. Latest: {latest.Debug}");
        return latest;
    }

    private static Task<DrawingObjectStateProbe> ReadDrawingObjectStateAsync(IPage page, string objectId)
        => page.EvaluateAsync<DrawingObjectStateProbe>(
            """
            objectId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const documentModel = getRuntimeDocument(instanceId);
                const drawing = findDrawing(documentModel, objectId);
                const layout = drawing?.Layout || drawing?.layout || {};
                const anchor = layout.Anchor || layout.anchor || {};
                const wrap = layout.Wrap || layout.wrap || {};
                const transform = layout.Transform || layout.transform || {};
                const size = drawing?.Size || drawing?.size || {};
                return {
                    found: !!drawing,
                    objectId,
                    anchorBlockId: String(anchor.BlockId ?? anchor.blockId ?? ''),
                    anchorOffset: Number(anchor.Offset ?? anchor.offset ?? -1),
                    anchorRegion: normalizeAnchorRegion(anchor.Region ?? anchor.region ?? ''),
                    anchorTableId: String(anchor.TableId ?? anchor.tableId ?? ''),
                    anchorCellId: String(anchor.CellId ?? anchor.cellId ?? ''),
                    anchorHeaderFooterId: String(anchor.HeaderFooterId ?? anchor.headerFooterId ?? ''),
                    wrapMode: normalizeWrapMode(wrap.Mode ?? wrap.mode ?? layout.WrapMode ?? layout.wrapMode ?? 'Inline'),
                    width: Number(transform.Width ?? transform.width ?? size.Width ?? size.width ?? 0) || 0,
                    height: Number(transform.Height ?? transform.height ?? size.Height ?? size.height ?? 0) || 0,
                    altText: String(drawing?.AltText ?? drawing?.altText ?? ''),
                    debug: JSON.stringify({ objectId, drawing, layout, anchor, wrap, transform, documentAvailable: !!documentModel })
                };

                function getRuntimeDocument(id) {
                    try {
                        const raw = window.tmDocumentEditorRuntime?.getDocumentSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getDocumentSnapshot?.(id)
                            || window.tmDocumentEditorRuntime?.getDocument?.(id)
                            || window.tmDocumentEditorEngine?.getDocument?.(id)
                            || null;
                        const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
                        return parsed?.Document || parsed?.document || parsed?.csharpDocument || parsed || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function findDrawing(document, id) {
                    for (const block of collectDocumentBlocks(document)) {
                        const found = findDrawingInBlock(block, id);
                        if (found) return found;
                    }
                    return null;
                }

                function findDrawingInBlock(block, id) {
                    if (!block || typeof block !== 'object') return null;
                    const content = block.Content || block.content || {};
                    for (const inline of asArray(content.Inlines || content.inlines || content.Runs || content.runs)) {
                        if (!isDrawingRun(inline)) continue;
                        const currentObjectId = String(inline.ObjectId || inline.objectId || inline.Id || inline.id || '');
                        if (currentObjectId === String(id)) return inline;
                    }

                    for (const row of asArray(content.Rows || content.rows)) {
                        for (const cell of asArray(row.Cells || row.cells)) {
                            for (const childBlock of asArray(cell.Blocks || cell.blocks)) {
                                const found = findDrawingInBlock(childBlock, id);
                                if (found) return found;
                            }
                        }
                    }

                    for (const childBlock of asArray(content.Blocks || content.blocks || block.Blocks || block.blocks)) {
                        const found = findDrawingInBlock(childBlock, id);
                        if (found) return found;
                    }

                    return null;
                }

                function collectDocumentBlocks(document) {
                    if (!document || typeof document !== 'object') return [];
                    const blocks = [];
                    appendBlocks(blocks, document.Blocks || document.blocks);
                    appendBlocks(blocks, document.body?.blocks || document.Body?.Blocks);
                    for (const header of [...asArray(document.Headers || document.headers), ...asArray(document.HeadersFooters || document.headersFooters)]) {
                        appendBlocks(blocks, header.Blocks || header.blocks);
                    }
                    for (const footer of asArray(document.Footers || document.footers)) {
                        appendBlocks(blocks, footer.Blocks || footer.blocks);
                    }
                    return blocks;
                }

                function appendBlocks(target, blocks) {
                    for (const block of asArray(blocks)) target.push(block);
                }

                function asArray(value) {
                    return Array.isArray(value) ? value : [];
                }

                function isDrawingRun(node) {
                    if (!node || typeof node !== 'object') return false;
                    const discriminator = node.$type || node.Kind || node.kind || node.Type || node.type || '';
                    return String(discriminator).toLowerCase() === 'drawing'
                        || (!!(node.ObjectId || node.objectId) && !!(node.Layout || node.layout) && (
                            !!(node.Image || node.image || node.Url || node.url || node.AssetId || node.assetId || node.DrawingKind || node.drawingKind)
                            || node.Source !== undefined
                            || node.source !== undefined));
                }

                function normalizeWrapMode(mode) {
                    const raw = String(mode ?? '').trim();
                    if (raw === '0') return 'Inline';
                    if (raw === '1') return 'Square';
                    if (raw === '2') return 'Tight';
                    if (raw === '3') return 'Through';
                    if (raw === '4') return 'TopBottom';
                    if (raw === '5') return 'BehindText';
                    if (raw === '6') return 'InFrontOfText';
                    return raw || 'Inline';
                }

                function normalizeAnchorRegion(region) {
                    const raw = String(region ?? '').trim().toLowerCase();
                    if (raw === '1' || raw === 'header') return 'Header';
                    if (raw === '2' || raw === 'footer') return 'Footer';
                    return 'Body';
                }
            }
            """,
            objectId);

    private static async Task<DocumentEditorPointProbe> ReadWrappedTextIntervalClickPointAsync(
        IPage page,
        string imageId,
        string blockId)
    {
        var intervals = await ReadDocumentEditorLineIntervalsAroundImageAsync(page, imageId);
        var interval = intervals
            .Where(candidate => string.Equals(candidate.BlockId, blockId, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Width)
            .FirstOrDefault();
        Assert.IsNotNull(interval,
            $"Expected a visible wrapped text interval for image '{imageId}' in block '{blockId}'. Intervals: {string.Join(", ", intervals.Select(item => $"{item.BlockId}@{item.X:0.##},{item.Y:0.##} {item.Width:0.##}x{item.Height:0.##}"))}");
        return new DocumentEditorPointProbe
        {
            X = interval!.X + Math.Min(24, Math.Max(4, interval.Width / 3)),
            Y = interval.Y + Math.Max(4, interval.Height / 2)
        };
    }

    private static async Task ClickImageCenterAsync(IPage page, string imageId)
    {
        await ScrollImageIntoViewAsync(page, imageId);
        var rect = await ReadDocumentEditorImageRectAsync(page, imageId);
        await page.Mouse.ClickAsync((float)(rect.X + rect.Width / 2), (float)(rect.Y + rect.Height / 2));
    }

    private static async Task DragImageCenterAsync(IPage page, string imageId, double targetX, double targetY)
    {
        await ScrollImageIntoViewAsync(page, imageId);
        var rect = await ReadDocumentEditorImageRectAsync(page, imageId);
        var startX = rect.X + rect.Width / 2;
        var startY = rect.Y + rect.Height / 2;
        await page.Mouse.MoveAsync((float)startX, (float)startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)targetX, (float)targetY, new() { Steps = 14 });
        await page.Mouse.UpAsync();
    }

    private static async Task DragImageCenterToBlockAsync(IPage page, string imageId, string targetBlockId)
    {
        await ScrollImageIntoViewAsync(page, imageId);
        var target = await ReadBlockCenterWithoutScrollingAsync(page, targetBlockId);
        await DragImageCenterAsync(page, imageId, target.X, target.Y);
    }

    private static async Task DragImageResizeHandleAsync(IPage page, string imageId, double deltaX, double deltaY)
    {
        await ClickImageCenterAsync(page, imageId);
        await page.WaitForTimeoutAsync(50);
        var handle = await ReadImageResizeHandleCenterAsync(page, imageId);

        await page.Mouse.MoveAsync((float)handle.X, (float)handle.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(handle.X + deltaX), (float)(handle.Y + deltaY), new() { Steps = 10 });
        await page.Mouse.UpAsync();
    }

    private static Task<DocumentEditorPointProbe> ReadImageResizeHandleCenterAsync(IPage page, string imageId)
        => page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            imageId => {
                const escaped = CSS.escape(imageId);
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${escaped}"], [data-testid="document-wysiwyg-host"] [data-object-id="${escaped}"]`);
                if (!figure) throw new Error(`Could not find image '${imageId}'.`);
                const overlay = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-testid="document-wysiwyg-object-selection-overlay"][data-object-id="${escaped}"]`);
                const handle = overlay?.querySelector?.('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se')
                    || figure.querySelector('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se, .tm-wysiwyg-image__resize-handle')
                    || figure;
                const rect = handle.getBoundingClientRect();
                return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
            }
            """,
            imageId);

    private static async Task ScrollImageIntoViewAsync(IPage page, string imageId)
    {
        await page.EvaluateAsync(
            """
            imageId => {
                const escaped = CSS.escape(imageId);
                const image = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escaped}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-object-id="${escaped}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${escaped}"]`);
                if (!image) throw new Error(`Could not find image '${imageId}' to scroll into view.`);
                image.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' });
            }
            """,
            imageId);
        await page.WaitForTimeoutAsync(100);
    }

    private static Task<ImageToolbarOverlapProbe> ReadImageToolbarOverlapProbeAsync(IPage page)
        => page.EvaluateAsync<ImageToolbarOverlapProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const toolbar = Array.from(document.querySelectorAll([
                    '[data-testid="document-image-toolbar"]',
                    '[data-human-testid="document-image-toolbar"]',
                    '[data-testid="document-wysiwyg-image-toolbar"]',
                    '[data-testid="document-wysiwyg-object-layout-bubble"]',
                    '.tm-document-editor__image-toolbar',
                    '.tm-wysiwyg-image-toolbar',
                    '.tm-wysiwyg-layout-bubble'
                ].join(','))).find(isVisible);
                const toolbarRect = toolbar ? toRect(toolbar.getBoundingClientRect()) : zeroRect();
                const readableTextRects = collectReadableTextRects(host);
                const overlapping = readableTextRects.filter(rect => overlaps(toolbarRect, rect, 2));
                return {
                    toolbarVisible: !!toolbar,
                    overlapsReadableText: overlapping.length > 0,
                    debug: JSON.stringify({
                        toolbar: toolbar ? describe(toolbar) : null,
                        toolbarRect,
                        overlapping,
                        readableTextRectCount: readableTextRects.length
                    })
                };

                function collectReadableTextRects(root) {
                    const result = [];
                    if (!root) return result;
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                            const parent = node.parentElement;
                            if (!parent || !isVisible(parent)) return NodeFilter.FILTER_REJECT;
                            if (parent.closest([
                                'figure',
                                '[data-testid*="toolbar"]',
                                '.tm-document-editor__ribbon',
                                '.tm-wysiwyg-page__layer--object',
                                '.tm-wysiwyg-page__layer--selection',
                                '.tm-wysiwyg-page__layer--guides',
                                '.tm-wysiwyg-layout-bubble',
                                '[role="menu"]'
                            ].join(','))) return NodeFilter.FILTER_REJECT;
                            return parent.closest('.tm-wysiwyg-block[data-block-id], [data-render-block-id]')
                                ? NodeFilter.FILTER_ACCEPT
                                : NodeFilter.FILTER_REJECT;
                        }
                    });
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width > 0.5 && rect.height > 0.5) result.push(toRect(rect));
                        }
                    }
                    return result;
                }

                function overlaps(a, b, tolerance) {
                    if (!a || !b || a.width <= 0 || a.height <= 0 || b.width <= 0 || b.height <= 0) return false;
                    const t = Number(tolerance || 0);
                    return a.x < b.x + b.width - t
                        && a.x + a.width > b.x + t
                        && a.y < b.y + b.height - t
                        && a.y + a.height > b.y + t;
                }

                function isVisible(node) {
                    if (!node) return false;
                    const style = getComputedStyle(node);
                    const rect = node.getBoundingClientRect();
                    return style.display !== 'none'
                        && style.visibility !== 'hidden'
                        && Number(style.opacity || 1) > 0
                        && rect.width > 0
                        && rect.height > 0;
                }

                function toRect(rect) {
                    return {
                        x: Number(rect?.x ?? rect?.left ?? 0),
                        y: Number(rect?.y ?? rect?.top ?? 0),
                        width: Number(rect?.width ?? 0),
                        height: Number(rect?.height ?? 0)
                    };
                }

                function zeroRect() {
                    return { x: 0, y: 0, width: 0, height: 0 };
                }

                function describe(node) {
                    return {
                        tag: node.tagName,
                        testId: node.getAttribute('data-testid') || '',
                        className: String(node.className || '')
                    };
                }
            }
            """);

    private static async Task<ImageDragTrackPreviewProbe> WaitForImageDragTrackPreviewAsync(IPage page, string imageId)
    {
        ImageDragTrackPreviewProbe? latest = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = await ReadImageDragTrackPreviewProbeAsync(page, imageId);
            if (latest.Active)
            {
                return latest;
            }

            await page.WaitForTimeoutAsync(50);
        }

        latest ??= await ReadImageDragTrackPreviewProbeAsync(page, imageId);
        Assert.Fail($"Timed out waiting for image drag preview. Latest: {latest.Debug}");
        return latest;
    }

    private static async Task<ImageResizeTrackPreviewProbe> WaitForImageResizeTrackPreviewAsync(IPage page, string imageId)
    {
        ImageResizeTrackPreviewProbe? latest = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = await ReadImageResizeTrackPreviewProbeAsync(page, imageId);
            if (latest.Active && latest.PreviewWidth > 0 && latest.PreviewHeight > 0)
            {
                return latest;
            }

            await page.WaitForTimeoutAsync(50);
        }

        latest ??= await ReadImageResizeTrackPreviewProbeAsync(page, imageId);
        Assert.Fail($"Timed out waiting for image resize preview. Latest: {latest.Debug}");
        return latest;
    }

    private static Task<ImageDragTrackPreviewProbe> ReadImageDragTrackPreviewProbeAsync(IPage page, string imageId)
        => page.EvaluateAsync<ImageDragTrackPreviewProbe>(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const engine = window.tmDocumentEditorEngine || window.tmDocumentEditorRuntime;
                const debug = engine?.getDebugSnapshot?.(instanceId) || {};
                const escaped = cssEscape(imageId);
                const item = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escaped}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-object-id="${escaped}"]`);
                const track = debug.imageMoveTrack || debug.ImageMoveTrack || null;
                const trackState = item?.getAttribute?.('data-track-state') || '';
                const dx = Number(item?.getAttribute?.('data-track-dx') || track?.appliedDelta?.x || track?.AppliedDelta?.X || 0) || 0;
                const dy = Number(item?.getAttribute?.('data-track-dy') || track?.appliedDelta?.y || track?.AppliedDelta?.Y || 0) || 0;
                const guideCount = document.querySelectorAll(`[data-testid="document-wysiwyg-object-drag-guide"][data-object-id="${escaped}"]`).length;
                const active = track?.active === true
                    || track?.Active === true
                    || trackState === 'active'
                    || item?.classList?.contains('tm-wysiwyg-object-track--active') === true;
                return {
                    active,
                    predrag: trackState === 'predrag' || track?.stage === 'predrag' || track?.Stage === 'predrag',
                    dx,
                    dy,
                    guideCount,
                    commandCount: Number(debug.commandCount || debug.CommandCount || 0) || 0,
                    undoDepth: Number(debug.undoDepth || debug.UndoDepth || 0) || 0,
                    trackStage: String(track?.stage || track?.Stage || ''),
                    transform: item?.style?.transform || '',
                    debug: JSON.stringify({
                        imageId,
                        instanceId,
                        itemFound: !!item,
                        trackState,
                        active,
                        dx,
                        dy,
                        guideCount,
                        commandCount: debug.commandCount || debug.CommandCount || 0,
                        undoDepth: debug.undoDepth || debug.UndoDepth || 0,
                        track,
                        lastObjectPointerInteraction: debug.lastObjectPointerInteraction || debug.LastObjectPointerInteraction || null,
                        className: String(item?.className || ''),
                        transform: item?.style?.transform || ''
                    })
                };

                function cssEscape(value) {
                    if (window.CSS?.escape) return window.CSS.escape(value);
                    return String(value).replace(/["\\]/g, '\\$&');
                }
            }
            """,
            imageId);

    private static Task<ImageResizeTrackPreviewProbe> ReadImageResizeTrackPreviewProbeAsync(IPage page, string imageId)
        => page.EvaluateAsync<ImageResizeTrackPreviewProbe>(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const engine = window.tmDocumentEditorEngine || window.tmDocumentEditorRuntime;
                const debug = engine?.getDebugSnapshot?.(instanceId) || {};
                const escaped = cssEscape(imageId);
                const item = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escaped}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-object-id="${escaped}"]`);
                const track = debug.imageResizeTrack || debug.ImageResizeTrack || debug.imageMoveTrack || debug.ImageMoveTrack || null;
                const badge = document.querySelector(`[data-testid="document-wysiwyg-image-resize-size-badge"][data-object-id="${escaped}"]`);
                const trackState = item?.getAttribute?.('data-track-state') || '';
                const styleWidth = Number.parseFloat(item?.style?.width || '') || 0;
                const styleHeight = Number.parseFloat(item?.style?.height || '') || 0;
                const previewWidth = Number(track?.previewWidth ?? track?.PreviewWidth ?? styleWidth ?? 0) || styleWidth;
                const previewHeight = Number(track?.previewHeight ?? track?.PreviewHeight ?? styleHeight ?? 0) || styleHeight;
                const active = track?.active === true
                    || track?.Active === true
                    || trackState === 'active'
                    || item?.classList?.contains('tm-wysiwyg-object-track--active') === true;
                const mode = String(track?.mode || track?.Mode || '');
                const badgeVisible = !!badge && getComputedStyle(badge).display !== 'none' && badge.getBoundingClientRect().width > 0;
                return {
                    active,
                    mode,
                    previewWidth,
                    previewHeight,
                    badgeVisible,
                    badgeText: String(badge?.textContent || track?.resizeBadgeText || track?.ResizeBadgeText || ''),
                    commandCount: Number(debug.commandCount || debug.CommandCount || 0) || 0,
                    undoDepth: Number(debug.undoDepth || debug.UndoDepth || 0) || 0,
                    trackStage: String(track?.stage || track?.Stage || ''),
                    debug: JSON.stringify({
                        imageId,
                        instanceId,
                        itemFound: !!item,
                        badgeFound: !!badge,
                        badgeText: badge?.textContent || '',
                        trackState,
                        active,
                        mode,
                        previewWidth,
                        previewHeight,
                        commandCount: debug.commandCount || debug.CommandCount || 0,
                        undoDepth: debug.undoDepth || debug.UndoDepth || 0,
                        track,
                        lastObjectPointerInteraction: debug.lastObjectPointerInteraction || debug.LastObjectPointerInteraction || null,
                        className: String(item?.className || ''),
                        styleWidth: item?.style?.width || '',
                        styleHeight: item?.style?.height || ''
                    })
                };

                function cssEscape(value) {
                    if (window.CSS?.escape) return window.CSS.escape(value);
                    return String(value).replace(/["\\]/g, '\\$&');
                }
            }
            """,
            imageId);

    private static Task<DocumentEditorPointProbe> ReadBlockCenterAsync(IPage page, string blockId)
        => page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            blockId => {
                const escaped = CSS.escape(blockId);
                const block = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${escaped}"], [data-testid="document-wysiwyg-host"] [data-render-block-id="${escaped}"]`);
                if (!block) throw new Error(`Could not find block '${blockId}'.`);
                block.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = block.getBoundingClientRect();
                return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
            }
            """,
            blockId);

    private static Task<DocumentEditorPointProbe> ReadBlockCenterWithoutScrollingAsync(IPage page, string blockId)
        => page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            blockId => {
                const escaped = CSS.escape(blockId);
                const block = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${escaped}"], [data-testid="document-wysiwyg-host"] [data-render-block-id="${escaped}"]`);
                if (!block) throw new Error(`Could not find block '${blockId}'.`);
                const rect = block.getBoundingClientRect();
                return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
            }
            """,
            blockId);

    private static void AssertRectNear(DocumentEditorRectProbe expected, DocumentEditorRectProbe actual, double tolerance, string message)
    {
        if (Math.Abs(expected.Width - actual.Width) > tolerance || Math.Abs(expected.Height - actual.Height) > tolerance)
        {
            Assert.Fail($"{message} Expected={FormatRect(expected)}, actual={FormatRect(actual)}.");
        }
    }

    private static string FormatRect(DocumentEditorRectProbe rect)
        => $"x={rect.X:0.##}, y={rect.Y:0.##}, w={rect.Width:0.##}, h={rect.Height:0.##}";

    private static Task ClearImagePerformanceMetricsAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorEngine?.clearDebugMetrics?.(instanceId);
            }
            """);

    private static Task<ImagePerformanceMetricsProbe> ReadImagePerformanceMetricsAsync(IPage page)
        => page.EvaluateAsync<ImagePerformanceMetricsProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorEngine?.getDebugMetrics?.(instanceId) || {};
            }
            """);

    private static async Task<ImagePerformanceMetricsProbe> WaitForImagePerformanceMetricsAsync(
        IPage page,
        Func<ImagePerformanceMetricsProbe, bool> predicate,
        string behavior)
    {
        ImagePerformanceMetricsProbe? latest = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = await ReadImagePerformanceMetricsAsync(page);
            if (predicate(latest))
            {
                return latest;
            }

            await page.WaitForTimeoutAsync(100);
        }

        latest ??= await ReadImagePerformanceMetricsAsync(page);
        Assert.Fail($"Timed out waiting for performance metrics during {behavior}. Latest: {latest}");
        return latest;
    }

    private static void RequireBlock(IEnumerable<JsonElement> blocks, string id, List<string> issues)
    {
        if (!blocks.Any(block => string.Equals(GetString(block, "Id"), id, StringComparison.Ordinal)))
        {
            issues.Add($"Seed must contain block '{id}'.");
        }
    }

    private static bool IsImageBlock(JsonElement block)
    {
        var type = GetString(block, "Type");
        var content = GetProperty(block, "Content");
        var discriminator = content.HasValue ? GetString(content.Value, "$type") : null;
        return string.Equals(type, "Image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "5", StringComparison.OrdinalIgnoreCase)
            || string.Equals(discriminator, "image", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTableBlock(JsonElement block)
    {
        var type = GetString(block, "Type");
        return string.Equals(type, "Table", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "4", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountDrawingRuns(JsonElement element)
    {
        var count = 0;
        Walk(element, node =>
        {
            if (IsDrawingRun(node))
            {
                count++;
            }
        });
        return count;
    }

    private static IEnumerable<string> ReadDrawingWrapModes(JsonElement element)
    {
        var modes = new List<string>();
        Walk(element, node =>
        {
            if (!IsDrawingRun(node))
            {
                return;
            }

            var layout = GetProperty(node, "Layout") ?? GetProperty(node, "layout");
            var wrap = layout.HasValue ? GetProperty(layout.Value, "Wrap") ?? GetProperty(layout.Value, "wrap") : null;
            var mode = wrap.HasValue ? GetString(wrap.Value, "Mode") ?? GetString(wrap.Value, "mode") : null;
            if (!string.IsNullOrWhiteSpace(mode))
            {
                modes.Add(NormalizeWrapModeName(mode!));
            }
        });
        return modes;
    }

    private static string NormalizeWrapModeName(string mode)
        => mode.Trim() switch
        {
            "0" => "Inline",
            "1" => "Square",
            "2" => "Tight",
            "3" => "Through",
            "4" => "TopBottom",
            "5" => "BehindText",
            "6" => "InFrontOfText",
            var value => value
        };

    private static Task<InlineDrawingFlowProbe> ReadInlineDrawingFlowProbeAsync(IPage page, string blockId, string objectId)
        => page.EvaluateAsync<InlineDrawingFlowProbe>(
            """
            ({ blockId, objectId }) => {
                const escapedBlockId = cssEscape(blockId);
                const escapedObjectId = cssEscape(objectId);
                const block = document.querySelector(`[data-testid="document-wysiwyg-host"] .tm-wysiwyg-block[data-block-id="${escapedBlockId}"]`);
                const anchor = block?.querySelector?.(`[data-object-anchor-id="${escapedObjectId}"]`) || null;
                const image = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escapedObjectId}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-object-id="${escapedObjectId}"]`);
                const rect = image?.getBoundingClientRect?.() || null;
                const anchorRect = anchor?.getBoundingClientRect?.() || rect;
                const style = image ? getComputedStyle(image) : null;
                const beforeTextRect = anchor ? nearestTextRect(block, anchor, 'before') : null;
                const afterTextRect = anchor ? nearestTextRect(block, anchor, 'after') : null;
                const sameLineBefore = !!(beforeTextRect && anchorRect && verticalOverlap(toRect(beforeTextRect), toRect(anchorRect)) > 0.5);
                const sameLineAfter = !!(afterTextRect && anchorRect && verticalOverlap(toRect(afterTextRect), toRect(anchorRect)) > 0.5);
                return {
                    exists: !!image,
                    anchorExists: !!anchor,
                    inObjectLayer: !!(image && image.closest('[data-testid="document-wysiwyg-object-layer"]')),
                    insideTargetParagraph: !!(anchor && anchor.closest('.tm-wysiwyg-block[data-block-id]') === block),
                    display: style?.display || '',
                    position: style?.position || '',
                    width: Number(rect?.width || 0),
                    height: Number(rect?.height || 0),
                    hasTextBefore: !!beforeTextRect,
                    hasTextAfter: !!afterTextRect,
                    sameLineAsAdjacentText: sameLineBefore && sameLineAfter,
                    debug: JSON.stringify({
                        blockId,
                        objectId,
                        blockFound: !!block,
                        imageFound: !!image,
                        anchorFound: !!anchor,
                        inObjectLayer: !!(image && image.closest('[data-testid="document-wysiwyg-object-layer"]')),
                        className: String(image?.className || ''),
                        display: style?.display || '',
                        position: style?.position || '',
                        rect: rect ? toRect(rect) : null,
                        anchorRect: anchorRect ? toRect(anchorRect) : null,
                        beforeTextRect: beforeTextRect ? toRect(beforeTextRect) : null,
                        afterTextRect: afterTextRect ? toRect(afterTextRect) : null,
                        html: block?.innerHTML?.slice(0, 800) || ''
                    })
                };

                function nearestTextRect(root, marker, direction) {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                            if (marker.contains(node)) return NodeFilter.FILTER_REJECT;
                            return NodeFilter.FILTER_ACCEPT;
                        }
                    });
                    const candidates = [];
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const relation = node.compareDocumentPosition(marker);
                        const before = (relation & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
                        const after = (relation & Node.DOCUMENT_POSITION_PRECEDING) !== 0;
                        if (direction === 'before' && !before) continue;
                        if (direction === 'after' && !after) continue;
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        const rects = Array.from(range.getClientRects()).filter(rect => rect.width > 0.5 && rect.height > 0.5);
                        if (rects.length) candidates.push(direction === 'before' ? rects[rects.length - 1] : rects[0]);
                    }
                    return direction === 'before' ? candidates[candidates.length - 1] || null : candidates[0] || null;
                }

                function verticalOverlap(a, b) {
                    return Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                }

                function toRect(rect) {
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }

                function cssEscape(value) {
                    return window.CSS?.escape ? window.CSS.escape(String(value)) : String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                }
            }
            """,
            new { blockId, objectId });

    private static bool IsDrawingRun(JsonElement node)
    {
        var discriminator = GetString(node, "$type")
            ?? GetString(node, "Type")
            ?? GetString(node, "type")
            ?? GetString(node, "Kind")
            ?? GetString(node, "kind");
        return string.Equals(discriminator, "drawing", StringComparison.OrdinalIgnoreCase);
    }

    private static void Walk(JsonElement element, Action<JsonElement> visitor)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            visitor(element);
            foreach (var property in element.EnumerateObject())
            {
                Walk(property.Value, visitor);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, visitor);
            }
        }
    }

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string propertyName)
    {
        var property = GetProperty(element, propertyName);
        return property.HasValue && property.Value.ValueKind == JsonValueKind.Array
            ? property.Value.EnumerateArray()
            : [];
    }

    private static JsonElement? GetProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        var property = GetProperty(element, propertyName);
        if (!property.HasValue)
        {
            return null;
        }

        return property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()
            : property.Value.ToString();
    }

    private sealed class InlineDrawingFlowProbe
    {
        public bool Exists { get; init; }

        public bool AnchorExists { get; init; }

        public bool InObjectLayer { get; init; }

        public bool InsideTargetParagraph { get; init; }

        public string Display { get; init; } = string.Empty;

        public string Position { get; init; } = string.Empty;

        public double Width { get; init; }

        public double Height { get; init; }

        public bool HasTextBefore { get; init; }

        public bool HasTextAfter { get; init; }

        public bool SameLineAsAdjacentText { get; init; }

        public string Debug { get; init; } = string.Empty;
    }

    private sealed class ImageToolbarOverlapProbe
    {
        public bool ToolbarVisible { get; init; }

        public bool OverlapsReadableText { get; init; }

        public string Debug { get; init; } = string.Empty;
    }

    private sealed class ImageDragTrackPreviewProbe
    {
        public bool Active { get; init; }

        public bool Predrag { get; init; }

        public double Dx { get; init; }

        public double Dy { get; init; }

        public int GuideCount { get; init; }

        public int CommandCount { get; init; }

        public int UndoDepth { get; init; }

        public string TrackStage { get; init; } = string.Empty;

        public string Transform { get; init; } = string.Empty;

        public string Debug { get; init; } = string.Empty;
    }

    private sealed class ImageResizeTrackPreviewProbe
    {
        public bool Active { get; init; }

        public string Mode { get; init; } = string.Empty;

        public double PreviewWidth { get; init; }

        public double PreviewHeight { get; init; }

        public bool BadgeVisible { get; init; }

        public string BadgeText { get; init; } = string.Empty;

        public int CommandCount { get; init; }

        public int UndoDepth { get; init; }

        public string TrackStage { get; init; } = string.Empty;

        public string Debug { get; init; } = string.Empty;
    }

    private sealed class DrawingObjectStateProbe
    {
        public bool Found { get; init; }

        public string ObjectId { get; init; } = string.Empty;

        public string AnchorBlockId { get; init; } = string.Empty;

        public double AnchorOffset { get; init; }

        public string AnchorRegion { get; init; } = string.Empty;

        public string AnchorTableId { get; init; } = string.Empty;

        public string AnchorCellId { get; init; } = string.Empty;

        public string AnchorHeaderFooterId { get; init; } = string.Empty;

        public string WrapMode { get; init; } = string.Empty;

        public double Width { get; init; }

        public double Height { get; init; }

        public string AltText { get; init; } = string.Empty;

        public string Debug { get; init; } = string.Empty;
    }

    private sealed class ImagePerformanceMetricsProbe
    {
        public int FullRenderCount { get; init; }

        public int PartialRenderCount { get; init; }

        public int InputDomApplyCount { get; init; }

        public int InputOperationCount { get; init; }

        public double MaxInputLatencyMs { get; init; }

        public int ImageDragLatencyCount { get; init; }

        public double MaxImageDragLatencyMs { get; init; }

        public int ObjectTrackResizeFrameCount { get; init; }

        public int ObjectTrackResizeCommitCount { get; init; }

        public override string ToString()
            => $"full={FullRenderCount}, partial={PartialRenderCount}, domApply={InputDomApplyCount}, ops={InputOperationCount}, maxInput={MaxInputLatencyMs:0.##}, imageOps={ImageDragLatencyCount}, maxImage={MaxImageDragLatencyMs:0.##}, resizeFrames={ObjectTrackResizeFrameCount}, resizeCommits={ObjectTrackResizeCommitCount}";
    }
}
