using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for Google Docs engine header/footer rendering and editing.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase3E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody()
    {
        var defaultPage = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var defaultConsole = GetMandatoryDocumentEditorConsoleCapture(defaultPage);

        await Assertions.Expect(defaultPage.GetByTestId("document-page-header").First)
            .ToContainTextAsync("Tempo Legal - Service agreement");
        await Assertions.Expect(defaultPage.GetByTestId("document-page-footer").First)
            .ToContainTextAsync("Confidential - Page 1");
        var defaultGeometry = await ReadHeaderFooterGeometryAsync(defaultPage);
        AssertHeaderFooterGeometry(defaultGeometry, nameof(DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody));
        await AssertNoDocumentEditorConsoleErrorsAsync(defaultPage, defaultConsole, nameof(DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody));

        var recoveryPage = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var recoveryConsole = GetMandatoryDocumentEditorConsoleCapture(recoveryPage);

        await Assertions.Expect(recoveryPage.GetByTestId("document-page-header").First)
            .ToContainTextAsync("Recovery Primary Header");
        await Assertions.Expect(recoveryPage.GetByTestId("document-page-footer").First)
            .ToContainTextAsync("Recovery Primary Footer - Page 1");
        var recoveryGeometry = await ReadHeaderFooterGeometryAsync(recoveryPage);
        AssertHeaderFooterGeometry(recoveryGeometry, nameof(DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody));
        await AssertNoDocumentEditorConsoleErrorsAsync(recoveryPage, recoveryConsole, nameof(DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody));
    }

    [TestMethod]
    public async Task HeaderEditing_RoutesTypingToHeaderAndReturnsFocusToBody()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string headerBlockId = "contract-header-primary-block";
        const string bodyBlockId = "contract-intro";

        var originalHeader = await ReadDocumentEditorBlockTextAsync(page, headerBlockId);
        await page.GetByTestId("document-page-header").First.DblClickAsync();
        await ClickDocumentEditorBlockOffsetAsync(page, headerBlockId, originalHeader.Length);
        await page.Keyboard.TypeAsync(" EDIT", new() { Delay = 0 });

        var editedHeader = await ReadDocumentEditorBlockTextAsync(page, headerBlockId);
        Assert.AreEqual(originalHeader + " EDIT", editedHeader);
        Assert.IsFalse((await ReadDocumentEditorBlockTextAsync(page, bodyBlockId)).Contains(" EDIT", StringComparison.Ordinal),
            "Header typing must not mutate the body paragraph.");
        var headerProbe = await CaptureStrictFrameProbeAsync(page, "header-edit");
        Assert.AreEqual(headerBlockId, headerProbe.Selection.BlockId);
        Assert.AreEqual("Header", await page.GetByTestId("document-wysiwyg-host").GetAttributeAsync("data-active-region"));

        await ClickDocumentEditorBlockOffsetAsync(page, bodyBlockId, 0);
        var bodyProbe = await CaptureStrictFrameProbeAsync(page, "body-after-header");
        Assert.AreEqual(bodyBlockId, bodyProbe.Selection.BlockId);
        Assert.AreEqual("Body", await page.GetByTestId("document-wysiwyg-host").GetAttributeAsync("data-active-region"));
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(HeaderEditing_RoutesTypingToHeaderAndReturnsFocusToBody));
    }

    [TestMethod]
    public async Task FooterEditing_SavesReloadsAndDoesNotStealBodySelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        const string footerBlockId = "contract-footer-primary-block";
        const string bodyBlockId = "contract-intro";
        const string marker = " footer-persist";

        var originalFooter = await ReadDocumentEditorBlockTextAsync(page, footerBlockId);
        await page.GetByTestId("document-page-footer").First.DblClickAsync();
        await ClickDocumentEditorBlockOffsetAsync(page, footerBlockId, originalFooter.Length);
        await page.Keyboard.TypeAsync(marker, new() { Delay = 0 });
        await Assertions.Expect(page.GetByTestId("document-page-footer").First)
            .ToContainTextAsync(originalFooter + marker);
        Assert.AreEqual(
            originalFooter + marker,
            await ReadRuntimeFooterBlockTextAsync(page, footerBlockId),
            "The JS runtime snapshot must include footer edits before the provider save boundary is crossed.");

        await ClickDocumentEditorBlockOffsetAsync(page, bodyBlockId, 0);
        var bodyProbeBeforeSave = await CaptureStrictFrameProbeAsync(page, "body-before-save");
        Assert.AreEqual(bodyBlockId, bodyProbeBeforeSave.Selection.BlockId);
        Assert.AreEqual("Body", await page.GetByTestId("document-wysiwyg-host").GetAttributeAsync("data-active-region"));
        Assert.AreEqual(
            originalFooter + marker,
            await ReadRuntimeFooterBlockTextAsync(page, footerBlockId),
            "Moving the caret back to the body must not discard footer edits from the runtime snapshot.");

        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);

        await Assertions.Expect(page.GetByTestId("document-page-footer").First)
            .ToContainTextAsync(originalFooter + marker);
        await ClickDocumentEditorBlockOffsetAsync(page, bodyBlockId, 0);
        var bodyProbeAfterReload = await CaptureStrictFrameProbeAsync(page, "body-after-reload");
        Assert.AreEqual(bodyBlockId, bodyProbeAfterReload.Selection.BlockId);
        Assert.AreEqual("Body", await page.GetByTestId("document-wysiwyg-host").GetAttributeAsync("data-active-region"));
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(FooterEditing_SavesReloadsAndDoesNotStealBodySelection));
    }

    private static Task<HeaderFooterGeometryProbe> ReadHeaderFooterGeometryAsync(IPage page)
        => page.EvaluateAsync<HeaderFooterGeometryProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const header = host?.querySelector('[data-testid="document-page-header"]');
                const body = host?.querySelector('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body');
                const footer = host?.querySelector('[data-testid="document-page-footer"]');
                const firstBodyText = body?.querySelector('p.tm-wysiwyg-block[data-block-id]');
                return {
                    header: rect(header),
                    body: rect(body),
                    footer: rect(footer),
                    firstBodyText: rect(firstBodyText)
                };

                function rect(node) {
                    const r = node?.getBoundingClientRect?.();
                    return r ? { x: r.x, y: r.y, width: r.width, height: r.height } : { x: 0, y: 0, width: 0, height: 0 };
                }
            }
            """);

    private static Task<string> ReadRuntimeFooterBlockTextAsync(IPage page, string blockId)
        => page.EvaluateAsync<string>(
            """
            ({ blockId }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id');
                const raw = instanceId && window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = raw ? JSON.parse(raw) : null;
                const doc = snapshot?.Document || snapshot?.document || {};
                const headersFooters = doc.HeadersFooters || doc.headersFooters || [];
                for (const region of headersFooters) {
                    const blocks = region.Blocks || region.blocks || [];
                    const block = blocks.find(item => (item.Id || item.id) === blockId);
                    if (!block) continue;
                    const content = block.Content || block.content || {};
                    const inlines = content.Inlines || content.inlines || [];
                    return inlines.map(inline => inline.Text ?? inline.text ?? inline.FallbackText ?? inline.fallbackText ?? '').join('');
                }

                return '';
            }
            """,
            new { blockId });

    private static void AssertHeaderFooterGeometry(HeaderFooterGeometryProbe geometry, string scenario)
    {
        Assert.IsTrue(geometry.Header.Height > 1, $"{scenario}: header must have a visible rect.");
        Assert.IsTrue(geometry.Footer.Height > 1, $"{scenario}: footer must have a visible rect.");
        Assert.IsTrue(geometry.Body.Height > 1, $"{scenario}: body must have a visible rect.");
        Assert.IsTrue(geometry.Header.Y + geometry.Header.Height <= geometry.Body.Y + 1,
            $"{scenario}: header must be above body.");
        Assert.IsTrue(geometry.Footer.Y >= geometry.Body.Y + geometry.Body.Height - 1,
            $"{scenario}: footer must be below body.");
        Assert.IsTrue(geometry.FirstBodyText.Y >= geometry.Body.Y,
            $"{scenario}: body text must stay inside body region.");
        Assert.IsTrue(geometry.FirstBodyText.Y >= geometry.Header.Y + geometry.Header.Height - 1,
            $"{scenario}: body text must not overlap header.");
        Assert.IsTrue(geometry.FirstBodyText.Y + geometry.FirstBodyText.Height <= geometry.Footer.Y + 1,
            $"{scenario}: body text must not overlap footer.");
    }

    private sealed class HeaderFooterGeometryProbe
    {
        [JsonPropertyName("header")] public DocumentEditorRectProbe Header { get; set; } = new();
        [JsonPropertyName("body")] public DocumentEditorRectProbe Body { get; set; } = new();
        [JsonPropertyName("footer")] public DocumentEditorRectProbe Footer { get; set; } = new();
        [JsonPropertyName("firstBodyText")] public DocumentEditorRectProbe FirstBodyText { get; set; } = new();
    }
}
