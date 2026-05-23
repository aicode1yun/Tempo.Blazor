using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Final recovery regression suite that binds P0/P1 behavior to human-visible UI.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase13E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Recovery_HeaderFooter_VisibleEditableAndPersistent()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string footerBlockId = "contract-footer-primary-block";
        const string marker = " p0";

        await Assertions.Expect(page.GetByTestId("document-page-header").First).ToContainTextAsync("Tempo Legal - Service agreement");
        var original = await ReadDocumentEditorBlockTextAsync(page, footerBlockId);
        await page.GetByTestId("document-page-footer").First.DblClickAsync();
        await ClickDocumentEditorBlockOffsetAsync(page, footerBlockId, original.Length);
        await page.Keyboard.TypeAsync(marker, new() { Delay = 0 });
        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Saved", new() { Timeout = 10000 });
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);

        await Assertions.Expect(page.GetByTestId("document-page-footer").First).ToContainTextAsync(original + marker);
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_HeaderFooter_VisibleEditableAndPersistent));
    }

    [TestMethod]
    public async Task Recovery_Comments_MarkersPanelBidirectionalSync()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var marker = page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor[data-comment-id='recovery-comment-visible']").First;
        await marker.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-thread'][data-comment-id='recovery-comment-visible']").First)
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected|active"), new() { Timeout = 5000 });

        await page.GetByTestId("document-comment-thread-select").First.ClickAsync();
        await Assertions.Expect(marker).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected|active"), new() { Timeout = 5000 });

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_Comments_MarkersPanelBidirectionalSync));
    }

    [TestMethod]
    public async Task Recovery_Revisions_MarkersPanelAcceptRejectSync()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await page.GetByTestId("document-side-panel-tab-revisions").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item'][data-revision-id='recovery-revision-insertion']").First)
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        await page.Locator("[data-testid='document-revision-item'][data-revision-id='recovery-revision-insertion'] [data-testid='document-revision-accept']").First.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision[data-revision-id='recovery-revision-insertion']"))
            .ToHaveCountAsync(0, new() { Timeout = 5000 });

        await page.Locator("[data-testid='document-revision-item'][data-revision-id='recovery-revision-deletion'] [data-testid='document-revision-reject']").First.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision[data-revision-id='recovery-revision-deletion']"))
            .ToHaveCountAsync(0, new() { Timeout = 5000 });

        var text = await ReadEditorPlainTextAsync(page);
        StringAssert.Contains(text, "inserted recovery clause");
        StringAssert.Contains(text, "deleted recovery clause");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_Revisions_MarkersPanelAcceptRejectSync));
    }

    [TestMethod]
    public async Task Recovery_TextSelection_ShowsFloatingToolbarAndAppliesFormatting()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await SelectPhraseAsync(page, "recovery-comment-paragraph", "This paragraph");
        await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await page.GetByTestId("document-mini-bold").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-mini-bold")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });

        var selected = await page.EvaluateAsync<string>("() => window.getSelection()?.toString() || ''");
        StringAssert.Contains(selected, "This paragraph");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_TextSelection_ShowsFloatingToolbarAndAppliesFormatting));
    }

    [TestMethod]
    public async Task Recovery_ImageSelection_ShowsToolbarAndPropertiesPanel()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickImageAsync(page, "recovery-provider-image");
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-inspector-asset-info")).ToContainTextAsync("contract-evidence-asset");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_ImageSelection_ShowsToolbarAndPropertiesPanel));
    }

    [TestMethod]
    public async Task Recovery_ImageProperties_AllFieldsApplyWithDebounce()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickImageAsync(page, "recovery-url-image");
        await page.GetByTestId("document-image-inspector-caption").FillAsync("P0 caption");
        await page.GetByTestId("document-image-inspector-width").FillAsync("188");
        await page.GetByTestId("document-image-inspector-link").FillAsync("/document-editor-evidence.svg?phase13=1");
        await page.WaitForTimeoutAsync(450);

        var model = await ReadActiveImageModelAsync(page, "recovery-url-image");
        Assert.AreEqual("P0 caption", model.Caption);
        Assert.AreEqual(188, Math.Round(model.Width));
        Assert.AreEqual("/document-editor-evidence.svg?phase13=1", model.Url);

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_ImageProperties_AllFieldsApplyWithDebounce));
    }

    [TestMethod]
    public async Task Recovery_SpaceAndEnter_AppearImmediately()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string blockId = "recovery-selection-paragraph";

        var original = await ReadDocumentEditorBlockTextAsync(page, blockId);
        await ClickDocumentEditorBlockOffsetAsync(page, blockId, 0);
        await page.Keyboard.PressAsync("A");
        await page.Keyboard.PressAsync("Space");
        Assert.IsTrue((await ReadDocumentEditorBlockTextAsync(page, blockId)).StartsWith("A " + original, StringComparison.Ordinal));

        await page.Keyboard.PressAsync("Enter");
        var paragraphs = await ReadParagraphsAsync(page);
        var index = Array.FindIndex(paragraphs, paragraph => paragraph.Id == blockId);
        Assert.IsTrue(index >= 0 && index + 1 < paragraphs.Length, "Enter must create a following paragraph immediately.");
        await page.Keyboard.PressAsync("B");
        paragraphs = await ReadParagraphsAsync(page);
        Assert.IsTrue(paragraphs[index + 1].Text.StartsWith("B", StringComparison.Ordinal), "Typing after Enter must land in the inserted paragraph.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_SpaceAndEnter_AppearImmediately));
    }

    [TestMethod]
    public async Task Recovery_FastTyping_IsNotBatchedIntoLargeChunks()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string blockId = "recovery-selection-paragraph";

        await ClickDocumentEditorBlockOffsetAsync(page, blockId, 0);
        await ClearRenderStatsAsync(page);
        var original = await ReadDocumentEditorBlockTextAsync(page, blockId);
        var typed = string.Empty;
        foreach (var ch in "phase13fasttyping")
        {
            await page.Keyboard.TypeAsync(ch.ToString(), new() { Delay = 0 });
            typed += ch;
            if (typed.Length % 4 == 0)
            {
                Assert.IsTrue((await ReadDocumentEditorBlockTextAsync(page, blockId)).StartsWith(typed + original, StringComparison.Ordinal));
            }
        }

        var stats = await ReadRenderStatsAsync(page);
        Assert.AreEqual(0, stats.FullRenderCount, "Fast typing must not use full document render.");
        Assert.IsTrue(stats.InputDomApplyCount >= typed.Length, "Every typed key should have an immediate DOM apply.");
        Assert.IsTrue(stats.MaxTypingBatchSize <= 1, $"Typing must not arrive in large visual chunks, got max batch {stats.MaxTypingBatchSize}.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_FastTyping_IsNotBatchedIntoLargeChunks));
    }

    [TestMethod]
    public async Task Recovery_DefaultDemo_NoConsoleErrorsAfterReload()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_DefaultDemo_NoConsoleErrorsAfterReload));
    }

    [TestMethod]
    public async Task Recovery_P1RegressionSuite_MarkersPopoversSourceUiSideTabsTableAndMobileSmoke()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await Assertions.Expect(page.GetByTestId("document-page-header").First).ToContainTextAsync("Recovery Primary Header");

        await ClickDocumentEditorBlockOffsetAsync(page, "recovery-comment-paragraph", 0);
        await page.Keyboard.TypeAsync("before ", new() { Delay = 0 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor[data-comment-id='recovery-comment-visible']").First)
            .ToContainTextAsync("visible comment anchor");

        await SelectPhraseAsync(page, "recovery-comment-paragraph", "before");
        await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await page.Locator("[data-testid='document-mini-highlight'] .tm-color-picker-trigger").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-mini-highlight'] .tm-color-picker-dropdown")).ToBeVisibleAsync();

        await page.GetByTestId("document-side-panel-tab-properties").ClickAsync();
        await page.GetByTestId("document-side-panel-tab-comments").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-comment-thread").First).ToBeVisibleAsync();

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);
        await ClickImageAsync(page, "recovery-url-image");
        await page.GetByTestId("document-side-panel-tab-properties").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-image-inspector-link")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await ClickImageAsync(page, "recovery-provider-image");
        await page.GetByTestId("document-side-panel-tab-properties").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-image-inspector-link")).ToHaveCountAsync(0, new() { Timeout = 5000 });

        var cell = page.Locator("[data-testid='document-wysiwyg-host'] [data-block-id='recovery-table-under-images'] td[data-cell-id]").First;
        await cell.ScrollIntoViewIfNeededAsync();
        await cell.ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-table-selection-properties-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToHaveCountAsync(0);

        await page.SetViewportSizeAsync(390, 780);
        await Assertions.Expect(page.GetByTestId("document-page-header").First).ToContainTextAsync("Recovery Primary Header");
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-properties")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_P1RegressionSuite_MarkersPopoversSourceUiSideTabsTableAndMobileSmoke));
    }

    private static async Task ClickImageAsync(IPage page, string blockId)
    {
        var image = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{blockId}']").First;
        await image.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(image).ToBeVisibleAsync(new() { Timeout = 5000 });
        await image.ClickAsync();
    }

    private static Task SelectPhraseAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync(
            """
            ({ blockId, phrase }) => {
                const block = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${CSS.escape(blockId)}"]`);
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
                if (start < 0) throw new Error(`Phrase ${phrase} was not found in ${text}.`);
                const end = start + phrase.length;
                const from = nodes.find(item => start >= item.start && start <= item.end);
                const to = nodes.find(item => end >= item.start && end <= item.end);
                const range = document.createRange();
                range.setStart(from.node, start - from.start);
                range.setEnd(to.node, end - to.start);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            new { blockId, phrase });

    private static Task<ParagraphProbe[]> ReadParagraphsAsync(IPage page)
        => page.EvaluateAsync<ParagraphProbe[]>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-block[data-block-id]'))
                .filter(node => node.tagName.toLowerCase() === 'p')
                .map(node => ({ id: node.getAttribute('data-block-id') || '', text: node.textContent || '' }))
            """);

    private static Task<ImageModelProbe> ReadActiveImageModelAsync(IPage page, string blockId)
        => page.EvaluateAsync<ImageModelProbe>(
            """
            ({ blockId }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id');
                const raw = instanceId && window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = raw ? JSON.parse(raw) : null;
                const documentModel = snapshot?.Document || snapshot?.document || snapshot;
                const block = documentModel?.Blocks?.find(item => item.Id === blockId || item.id === blockId);
                const content = block?.Content || block?.content || {};
                const size = content.Size || content.size || {};
                const transform = content.Layout?.Transform || content.layout?.transform || {};
                return {
                    caption: content.Caption || content.caption || '',
                    url: content.Url || content.url || '',
                    width: Number(transform.Width ?? transform.width ?? size.Width ?? size.width ?? 0)
                };
            }
            """,
            new { blockId });

    private static Task ClearRenderStatsAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorEngine?.clearDebugMetrics?.(instanceId);
            }
            """);

    private static Task<RenderStatsProbe> ReadRenderStatsAsync(IPage page)
        => page.EvaluateAsync<RenderStatsProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id');
                const stats = instanceId ? window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {} : {};
                return {
                    fullRenderCount: Number(stats.FullRenderCount ?? stats.fullRenderCount ?? 0),
                    inputDomApplyCount: Number(stats.InputDomApplyCount ?? stats.inputDomApplyCount ?? 0),
                    maxTypingBatchSize: Number(stats.MaxTypingBatchSize ?? stats.maxTypingBatchSize ?? 0)
                };
            }
            """);

    private sealed class ParagraphProbe
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private sealed class ImageModelProbe
    {
        [JsonPropertyName("caption")] public string Caption { get; set; } = string.Empty;
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
        [JsonPropertyName("width")] public double Width { get; set; }
    }

    private sealed class RenderStatsProbe
    {
        [JsonPropertyName("fullRenderCount")] public int FullRenderCount { get; set; }
        [JsonPropertyName("inputDomApplyCount")] public int InputDomApplyCount { get; set; }
        [JsonPropertyName("maxTypingBatchSize")] public int MaxTypingBatchSize { get; set; }
    }
}
