using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for Word/Google Docs style UX polish.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase12E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task RecoveryTextSelection_UxHighlightToolbarAndColorPopoverStayReadable()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await SelectPhraseAsync(page, "recovery-comment-paragraph", "This paragraph");
        await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var polish = await page.EvaluateAsync<TextSelectionPolishProbe>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-mini-toolbar"]');
                const selection = window.getSelection();
                const selectionRect = selection && selection.rangeCount ? selection.getRangeAt(0).getBoundingClientRect() : null;
                const toolbarRect = toolbar?.getBoundingClientRect?.();
                const style = getComputedStyle(document.querySelector('[data-testid="document-wysiwyg-host"]'));
                return {
                    selectedText: selection?.toString() || '',
                    selectionColor: style.getPropertyValue('--tm-color-primary').trim(),
                    toolbarVisible: !!toolbar && toolbarRect.width > 1 && toolbarRect.height > 1,
                    toolbarOverlapsSelection: intersects(toolbarRect, selectionRect),
                    toolbarTransition: getComputedStyle(toolbar).animationDuration || getComputedStyle(toolbar).transitionDuration || '',
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight
                };

                function intersects(a, b) {
                    if (!a || !b) return false;
                    return a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
                }
            }
            """);

        StringAssert.Contains(polish.SelectedText, "This paragraph");
        Assert.IsTrue(polish.ToolbarVisible, "The floating toolbar must be visible.");
        Assert.IsFalse(polish.ToolbarOverlapsSelection, "The floating toolbar must not cover selected text.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(polish.SelectionColor), "Selection color token must resolve.");
        Assert.IsTrue(
            polish.ToolbarTransition.Contains("ms", StringComparison.OrdinalIgnoreCase)
            || polish.ToolbarTransition.Contains('s'),
            $"The floating toolbar should animate in, actual duration was '{polish.ToolbarTransition}'.");

        await page.Locator("[data-testid='document-mini-text-color'] .tm-color-picker-trigger").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-mini-text-color'] .tm-color-picker-dropdown")).ToBeVisibleAsync();
        var dropdown = await page.EvaluateAsync<RectProbe>(
            """
            () => {
                const rect = document.querySelector('[data-testid="document-mini-text-color"] .tm-color-picker-dropdown')?.getBoundingClientRect();
                return rect ? { x: rect.x, y: rect.y, width: rect.width, height: rect.height } : { x: 0, y: 0, width: 0, height: 0 };
            }
            """);
        Assert.IsTrue(dropdown.Width > 1 && dropdown.Height > 1, "Color popover must have visible geometry.");
        Assert.IsTrue(dropdown.X >= 0 && dropdown.X + dropdown.Width <= polish.ViewportWidth + 0.5, "Color popover must stay inside horizontal viewport bounds.");
        Assert.IsTrue(dropdown.Y >= 0 && dropdown.Y + dropdown.Height <= polish.ViewportHeight + 0.5, "Color popover must stay inside vertical viewport bounds.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryTextSelection_UxHighlightToolbarAndColorPopoverStayReadable));
    }

    [TestMethod]
    public async Task RecoveryCommentsAndRevisions_UseDistinctReadableStates()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var styles = await page.EvaluateAsync<MarkerPolishProbe>(
            """
            () => {
                const comment = document.querySelector('.tm-document-inline--comment-anchor[data-comment-id="recovery-comment-visible"]');
                const insertion = document.querySelector('.tm-wysiwyg-revision[data-revision-id="recovery-revision-insertion"]');
                const deletion = document.querySelector('.tm-wysiwyg-revision[data-revision-id="recovery-revision-deletion"]');
                return {
                    commentText: comment?.textContent || '',
                    commentBackground: getComputedStyle(comment).backgroundColor,
                    insertionText: insertion?.textContent || '',
                    insertionDecoration: getComputedStyle(insertion).textDecorationLine,
                    insertionBackground: getComputedStyle(insertion).backgroundColor,
                    deletionText: deletion?.textContent || '',
                    deletionDecoration: getComputedStyle(deletion).textDecorationLine,
                    deletionBackground: getComputedStyle(deletion).backgroundColor
                };
            }
            """);

        Assert.AreEqual("visible comment anchor", styles.CommentText);
        Assert.AreNotEqual("rgba(0, 0, 0, 0)", styles.CommentBackground, "Comment highlight must be visible.");
        Assert.AreEqual("inserted recovery clause", styles.InsertionText);
        StringAssert.Contains(styles.InsertionDecoration, "underline");
        Assert.AreNotEqual(styles.InsertionBackground, styles.CommentBackground, "Revision insertion must not look like a comment.");
        Assert.AreEqual("deleted recovery clause", styles.DeletionText);
        StringAssert.Contains(styles.DeletionDecoration, "line-through");
        Assert.AreNotEqual(styles.DeletionBackground, styles.InsertionBackground, "Deletion and insertion must be visually distinct.");

        await page.GetByTestId("document-side-panel-tab-revisions").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-revision-item").First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-revision-item").First).ToContainTextAsync("Recovery Reviewer");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryCommentsAndRevisions_UseDistinctReadableStates));
    }

    [TestMethod]
    public async Task RecoveryImageUx_UsesCompactIconSegmentsAndDocumentStyleHandles()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var image = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='recovery-left-wrap-image']").First;
        await image.ScrollIntoViewIfNeededAsync();
        await image.ClickAsync();

        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var probe = await page.EvaluateAsync<ImagePolishProbe>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-image-wrap-panel"]');
                const inspector = document.querySelector('[data-testid="document-image-inspector"]');
                const wrapButtons = Array.from(document.querySelectorAll('[data-testid="document-image-wrap-panel"] [data-human-testid="document-image-wrap-button"]'));
                const handle = document.querySelector('[data-testid^="document-wysiwyg-object-resize-handle-"], .tm-wysiwyg-image__resize-handle');
                const handleStyle = handle ? getComputedStyle(handle) : null;
                const toolbarRect = toolbar?.getBoundingClientRect?.();
                const inspectorRect = inspector?.getBoundingClientRect?.();
                return {
                    wrapButtonCount: wrapButtons.length,
                    allWrapButtonsHaveIcon: wrapButtons.every(button => !!button.querySelector('.tm-icon, svg')),
                    toolbarWidth: toolbarRect?.width || 0,
                    inspectorHeight: inspectorRect?.height || 0,
                    viewportHeight: window.innerHeight,
                    handleBorderRadius: handleStyle?.borderRadius || '',
                    handleBorderWidth: handleStyle?.borderWidth || ''
                };
            }
            """);

        Assert.IsTrue(probe.WrapButtonCount >= 5, "Wrap controls must be a complete segmented set.");
        Assert.IsTrue(probe.AllWrapButtonsHaveIcon, "Wrap controls must use icons, not only text labels.");
        Assert.IsTrue(probe.ToolbarWidth <= 384, $"Image toolbar should stay compact, got {probe.ToolbarWidth}px.");
        Assert.IsTrue(probe.InspectorHeight <= probe.ViewportHeight, "Image properties panel must fit into the viewport.");
        Assert.IsTrue(probe.HandleBorderWidth.StartsWith("2", StringComparison.Ordinal), $"Resize handles should have a clear document-editor border, got '{probe.HandleBorderWidth}'.");
        Assert.IsTrue(probe.HandleBorderRadius is "2px" or "3px" or "4px", $"Resize handles should be small rounded squares, got '{probe.HandleBorderRadius}'.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryImageUx_UsesCompactIconSegmentsAndDocumentStyleHandles));
    }

    private static Task SelectPhraseAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync(
            """
            ({ blockId, phrase }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = host?.querySelector(`[data-block-id="${CSS.escape(blockId)}"]`);
                if (!block) throw new Error(`Block ${blockId} was not found.`);
                block.scrollIntoView({ block: 'center', inline: 'nearest' });
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                const nodes = [];
                let text = '';
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    nodes.push({ node, start: text.length, end: text.length + node.nodeValue.length });
                    text += node.nodeValue;
                }
                const start = text.indexOf(phrase);
                if (start < 0) throw new Error(`Phrase ${phrase} was not found.`);
                const end = start + phrase.length;
                const from = nodes.find(item => start >= item.start && start <= item.end);
                const to = nodes.find(item => end >= item.start && end <= item.end);
                const range = document.createRange();
                range.setStart(from.node, start - from.start);
                range.setEnd(to.node, end - to.start);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                block.dispatchEvent(new Event('selectionchange', { bubbles: true }));
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            new { blockId, phrase });

    private sealed class TextSelectionPolishProbe
    {
        [JsonPropertyName("selectedText")] public string SelectedText { get; set; } = string.Empty;
        [JsonPropertyName("selectionColor")] public string SelectionColor { get; set; } = string.Empty;
        [JsonPropertyName("toolbarVisible")] public bool ToolbarVisible { get; set; }
        [JsonPropertyName("toolbarOverlapsSelection")] public bool ToolbarOverlapsSelection { get; set; }
        [JsonPropertyName("toolbarTransition")] public string ToolbarTransition { get; set; } = string.Empty;
        [JsonPropertyName("viewportWidth")] public double ViewportWidth { get; set; }
        [JsonPropertyName("viewportHeight")] public double ViewportHeight { get; set; }
    }

    private sealed class MarkerPolishProbe
    {
        [JsonPropertyName("commentText")] public string CommentText { get; set; } = string.Empty;
        [JsonPropertyName("commentBackground")] public string CommentBackground { get; set; } = string.Empty;
        [JsonPropertyName("insertionText")] public string InsertionText { get; set; } = string.Empty;
        [JsonPropertyName("insertionDecoration")] public string InsertionDecoration { get; set; } = string.Empty;
        [JsonPropertyName("insertionBackground")] public string InsertionBackground { get; set; } = string.Empty;
        [JsonPropertyName("deletionText")] public string DeletionText { get; set; } = string.Empty;
        [JsonPropertyName("deletionDecoration")] public string DeletionDecoration { get; set; } = string.Empty;
        [JsonPropertyName("deletionBackground")] public string DeletionBackground { get; set; } = string.Empty;
    }

    private sealed class ImagePolishProbe
    {
        [JsonPropertyName("wrapButtonCount")] public int WrapButtonCount { get; set; }
        [JsonPropertyName("allWrapButtonsHaveIcon")] public bool AllWrapButtonsHaveIcon { get; set; }
        [JsonPropertyName("toolbarWidth")] public double ToolbarWidth { get; set; }
        [JsonPropertyName("inspectorHeight")] public double InspectorHeight { get; set; }
        [JsonPropertyName("viewportHeight")] public double ViewportHeight { get; set; }
        [JsonPropertyName("handleBorderRadius")] public string HandleBorderRadius { get; set; } = string.Empty;
        [JsonPropertyName("handleBorderWidth")] public string HandleBorderWidth { get; set; } = string.Empty;
    }

    private sealed class RectProbe
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
    }
}
