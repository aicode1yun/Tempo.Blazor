using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 15 page navigation and WYSIWYG surface UX.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase15PageUxE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase15_PageNavigator_NavigatesToSecondPageAfterPageBreak()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await PlaceCaretAtEndOfBodyAsync(page);
        await ExecuteRuntimeCommandAsync(page, "insertPageBreak", new { });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-page-break']"))
            .ToHaveCountAsync(1, new() { Timeout = 5000 });

        await page.Locator("[data-testid='document-side-panel-tab-pages']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-page-navigator']")).ToBeVisibleAsync();
        var secondPage = page.Locator("[data-testid='document-page-navigator-item']").Nth(1);
        await Assertions.Expect(secondPage).ToBeVisibleAsync(new() { Timeout = 5000 });

        await secondPage.ClickAsync();

        await Assertions.Expect(secondPage).ToHaveAttributeAsync("aria-current", "page", new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase15_PageBreak_CanBeSelectedAndDeletedWithKeyboard()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await PlaceCaretAtEndOfBodyAsync(page);
        await ExecuteRuntimeCommandAsync(page, "insertPageBreak", new { });
        var breakHandle = page.Locator("[data-testid='document-wysiwyg-page-break']").First;
        await Assertions.Expect(breakHandle).ToBeVisibleAsync(new() { Timeout = 5000 });

        await breakHandle.ClickAsync();
        await Assertions.Expect(breakHandle).ToHaveClassAsync(new Regex("tm-wysiwyg-page-break--selected"), new() { Timeout = 5000 });
        await page.Keyboard.PressAsync("Delete");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-page-break']"))
            .ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase15_NonPrintingCharactersToggle_ShowsParagraphAndSpaceMarks()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await EditorTypeAsync(page, " phase15 marks");
        await page.Locator("[data-testid='document-ribbon-tab-view']").ClickAsync();
        await page.Locator("[data-testid='document-toggle-nonprinting']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']"))
            .ToHaveClassAsync(new Regex("tm-wysiwyg--show-nonprinting"), new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-nonprinting-text']").First)
            .ToContainTextAsync("·", new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-nonprinting-text']").Filter(new() { HasText = "¶" }).First)
            .ToContainTextAsync("¶", new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase15_EmptyBodyTableCellHeaderAndFooter_DoNotCollapseAndAcceptTyping()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await LoadEmptyRegionsDocumentAsync(page);
        var body = page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__body--empty").First;
        var header = page.Locator("[data-testid='document-wysiwyg-header'].tm-wysiwyg-page__header--empty").First;
        var footer = page.Locator("[data-testid='document-wysiwyg-footer'].tm-wysiwyg-page__footer--empty").First;
        await Assertions.Expect(body).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(header).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(footer).ToBeVisibleAsync(new() { Timeout = 5000 });
        Assert.IsTrue(await RegionsHaveStableHeightAsync(page), "Empty body/header/footer regions should keep a visible editable footprint.");

        var bodyMarker = $"phase15-body-{DateTimeOffset.UtcNow:HHmmssfff}";
        await body.ClickAsync(new() { Position = new() { X = 16, Y = 16 } });
        await page.Keyboard.InsertTextAsync(bodyMarker);
        await Assertions.Expect(body).ToContainTextAsync(bodyMarker, new() { Timeout = 5000 });

        var headerMarker = $"phase15-header-{DateTimeOffset.UtcNow:HHmmssfff}";
        await ActivateHeaderFooterRegionAsync(page, "header");
        await page.Keyboard.InsertTextAsync(headerMarker);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-header']").First)
            .ToContainTextAsync(headerMarker, new() { Timeout = 5000 });

        var footerMarker = $"phase15-footer-{DateTimeOffset.UtcNow:HHmmssfff}";
        await ActivateHeaderFooterRegionAsync(page, "footer");
        await page.Keyboard.InsertTextAsync(footerMarker);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-footer']").First)
            .ToContainTextAsync(footerMarker, new() { Timeout = 5000 });

        await LoadEmptyTableDocumentAsync(page);
        var cell = page.Locator("[data-testid='document-wysiwyg-host'] td.tm-wysiwyg-table-cell--empty").First;
        await Assertions.Expect(cell).ToBeVisibleAsync(new() { Timeout = 5000 });
        Assert.IsTrue(await CellHasStableHeightAsync(page), "Empty table cells should keep a visible editable footprint.");
        var cellMarker = $"phase15-cell-{DateTimeOffset.UtcNow:HHmmssfff}";
        await cell.ClickAsync(new() { Position = new() { X = 12, Y = 12 } });
        await page.Keyboard.InsertTextAsync(cellMarker);
        await Assertions.Expect(cell).ToContainTextAsync(cellMarker, new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase15_PageOverflowWarningAction_InsertsPageBreak()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await LoadOverflowDocumentAsync(page);
        var warning = page.Locator("[data-testid='document-page-overflow-warning']").First;
        await Assertions.Expect(warning).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-page-overflow-insert-page-break']").First)
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        await PlaceCaretAtEndOfBodyAsync(page);
        await page.Locator("[data-testid='document-page-overflow-insert-page-break']").First.ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-page-break']"))
            .ToHaveCountAsync(1, new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase15_OutlinePanel_HighlightsActiveHeadingAfterScroll()
    {
        var original = await LoadDemoDocumentAsync("contract-demo");
        Assert.IsNotNull(original?.Document);

        try
        {
            await SaveDemoDocumentAsync(CreateOutlineSyncDocument(original.Document));

            var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
            await page.Locator("[data-testid='document-side-panel-tab-outline']").ClickAsync();

            await page.WaitForFunctionAsync(
                """
                () => document.querySelectorAll('[data-testid="document-outline-item"][data-block-id]').length >= 4
                """,
                new PageWaitForFunctionOptions { Timeout = 10000 });
            var targetBlockId = await page.Locator("[data-testid='document-outline-item'][data-block-id]").Last
                .GetAttributeAsync("data-block-id");
            Assert.IsFalse(string.IsNullOrWhiteSpace(targetBlockId), "The prepared document should expose outline heading targets.");

            await ScrollHeadingIntoActivePositionAsync(page, targetBlockId!);

            await Assertions.Expect(page.Locator($"[data-testid='document-outline-item'][data-block-id='{targetBlockId}']"))
                .ToHaveAttributeAsync("data-active", "true", new() { Timeout = 10000 });
        }
        finally
        {
            if (original?.Document is not null)
            {
                await SaveDemoDocumentAsync(original.Document);
            }
        }
    }

    private static Task ExecuteRuntimeCommandAsync(IPage page, string command, object payload)
    {
        return page.EvaluateAsync(
            """
            ({ command, payload }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, command, payload);
            }
            """,
            new { command, payload });
    }

    private static Task LoadEmptyRegionsDocumentAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const doc = snapshot.Document || snapshot.document;
                doc.Blocks = [];
                doc.blocks = [];
                const headersFooters = doc.HeadersFooters || doc.headersFooters || [];
                headersFooters.forEach(hf => {
                    hf.Blocks = [];
                    hf.blocks = [];
                });
                window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
            }
            """);
    }

    private static Task LoadEmptyTableDocumentAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const doc = snapshot.Document || snapshot.document;
                doc.Blocks = [{
                    Id: 'phase15-empty-table',
                    Type: 4,
                    Content: {
                        Rows: [{
                            Id: 'phase15-empty-row',
                            Cells: [{ Id: 'phase15-empty-cell', Blocks: [] }]
                        }]
                    }
                }];
                doc.blocks = doc.Blocks;
                window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
            }
            """);
    }

    private static Task LoadOverflowDocumentAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const doc = snapshot.Document || snapshot.document;
                doc.Blocks = Array.from({ length: 90 }, (_, index) => ({
                    Id: `phase15-overflow-${index}`,
                    Type: 0,
                    Content: {
                        Inlines: [{
                            Id: `phase15-overflow-inline-${index}`,
                            Text: `Phase 15 overflow paragraph ${index + 1}. This paragraph intentionally fills the page so the overflow affordance becomes visible.`
                        }]
                    }
                }));
                doc.blocks = doc.Blocks;
                window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
            }
            """);
    }

    private static Task<bool> RegionsHaveStableHeightAsync(IPage page)
    {
        return page.EvaluateAsync<bool>(
            """
            () => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body--empty');
                const header = document.querySelector('[data-testid="document-wysiwyg-header"].tm-wysiwyg-page__header--empty');
                const footer = document.querySelector('[data-testid="document-wysiwyg-footer"].tm-wysiwyg-page__footer--empty');
                return [body, header, footer].every(element => {
                    const rect = element?.getBoundingClientRect();
                    return rect && rect.width > 0 && rect.height >= 16;
                });
            }
            """);
    }

    private static Task<bool> CellHasStableHeightAsync(IPage page)
    {
        return page.EvaluateAsync<bool>(
            """
            () => {
                const cell = document.querySelector('[data-testid="document-wysiwyg-host"] td.tm-wysiwyg-table-cell--empty');
                const rect = cell?.getBoundingClientRect();
                return !!rect && rect.width > 0 && rect.height >= 16;
            }
            """);
    }

    private static Task ActivateHeaderFooterRegionAsync(IPage page, string region)
    {
        return page.EvaluateAsync(
            """
            region => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const selector = region === 'footer'
                    ? '.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__footer[contenteditable="true"]'
                    : '.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__header[contenteditable="true"]';
                const target = Array.from(host?.querySelectorAll(selector) || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                if (!target) throw new Error(`${region} region was not found.`);
                target.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
                target.focus({ preventScroll: true });
                const range = document.createRange();
                range.selectNodeContents(target);
                range.collapse(false);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            region);
    }

    private static Task ScrollHeadingIntoActivePositionAsync(IPage page, string blockId)
    {
        return page.EvaluateAsync(
            """
            blockId => new Promise(resolve => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const heading = host?.querySelector(`[data-block-id="${CSS.escape(blockId)}"]`);
                if (heading) {
                    const rootRect = host.getBoundingClientRect();
                    const headingRect = heading.getBoundingClientRect();
                    const rootCanScroll = host.scrollHeight > host.clientHeight + 2;
                    const scrollContainer = rootCanScroll ? host : findScrollContainer(host);
                    const viewportHeight = window.innerHeight || 900;
                    const threshold = rootCanScroll
                        ? rootRect.top + Math.min(Math.max(rootRect.height * 0.2, 96), 240)
                        : Math.min(Math.max(viewportHeight * 0.2, 96), 240);
                    const delta = headingRect.top - threshold + 4;
                    if (rootCanScroll) {
                        host.scrollTop += delta;
                    } else if (scrollContainer) {
                        scrollContainer.scrollTop += delta;
                        window.scrollTo(0, window.scrollY + delta);
                    } else {
                        window.scrollBy(0, delta);
                    }
                }
                requestAnimationFrame(() => {
                    host?.dispatchEvent(new Event('scroll', { bubbles: true }));
                    document.dispatchEvent(new Event('scroll', { bubbles: true }));
                    window.dispatchEvent(new Event('scroll'));
                    requestAnimationFrame(resolve);
                });

                function findScrollContainer(element) {
                    for (let node = element?.parentElement; node; node = node.parentElement) {
                        const style = getComputedStyle(node);
                        if (node.scrollHeight > node.clientHeight + 2 && /(auto|scroll|overlay)/.test(style.overflowY)) {
                            return node;
                        }
                    }
                    if (document.documentElement.scrollHeight > document.documentElement.clientHeight + 2) {
                        return document.documentElement;
                    }

                    return document.scrollingElement || document.documentElement;
                }
            })
            """,
            blockId);
    }

    private static async Task<DocumentEditorLoadResult?> LoadDemoDocumentAsync(string documentId)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        return await http.GetFromJsonAsync<DocumentEditorLoadResult>($"api/document-editor/{Uri.EscapeDataString(documentId)}");
    }

    private static async Task SaveDemoDocumentAsync(DocumentEditorDocument document)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.PutAsJsonAsync(
            $"api/document-editor/{Uri.EscapeDataString(document.DocumentId)}",
            new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            });
        response.EnsureSuccessStatusCode();
    }

    private static DocumentEditorDocument CreateOutlineSyncDocument(DocumentEditorDocument original)
    {
        var document = DocumentEditorDocument.Empty(original.DocumentId);
        document.Metadata = new DocumentEditorMetadata
        {
            Title = "Phase 15 outline sync",
            Status = DocumentEditorStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };
        document.PageSettings = original.PageSettings;
        document.Theme = original.Theme;

        document.Blocks =
        [
            CreateHeading("phase15-outline-heading-1", 1, "Phase 15 heading 1", 0),
            ..CreateParagraphs("phase15-outline-intro", 10, 1),
            CreateHeading("phase15-outline-heading-2", 2, "Phase 15 heading 2", 20),
            ..CreateParagraphs("phase15-outline-middle", 12, 21),
            CreateHeading("phase15-outline-heading-3", 2, "Phase 15 heading 3", 40),
            ..CreateParagraphs("phase15-outline-late", 12, 41),
            CreateHeading("phase15-outline-heading-4", 3, "Phase 15 heading 4", 60),
            ..CreateParagraphs("phase15-outline-end", 4, 61)
        ];

        return document;
    }

    private static DocumentBlock CreateHeading(string id, int level, string text, double order)
    {
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Heading,
            Order = order,
            Content = new HeadingBlockContent
            {
                Level = level,
                Inlines = [new TextRun { Id = $"{id}-inline", Text = text }]
            }
        };
    }

    private static DocumentBlock[] CreateParagraphs(string idPrefix, int count, double firstOrder)
    {
        return Enumerable.Range(0, count)
            .Select(index => new DocumentBlock
            {
                Id = $"{idPrefix}-{index}",
                Type = DocumentBlockType.Paragraph,
                Order = firstOrder + index,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Id = $"{idPrefix}-{index}-inline",
                            Text = "Phase 15 filler paragraph for outline scroll synchronization."
                        }
                    ]
                }
            })
            .ToArray();
    }

    private static Task PlaceCaretAtEndOfBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const body = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]'))
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 0
                            && rect.height > 0
                            && style.display !== 'none'
                            && style.visibility !== 'hidden'
                            && !element.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                if (!body) throw new Error('Editable body was not found.');

                const blocks = Array.from(body.children)
                    .filter(block =>
                        block.matches('p[data-block-id], h1[data-block-id], h2[data-block-id], h3[data-block-id], h4[data-block-id], h5[data-block-id], h6[data-block-id], blockquote[data-block-id], li[data-block-id]')
                        && block.textContent.trim().length > 0);
                const target = blocks.at(-1) || body;
                target.closest('[contenteditable="true"]')?.focus();
                const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
                let last = null;
                while (walker.nextNode()) {
                    if ((walker.currentNode.textContent || '').trim().length > 0) last = walker.currentNode;
                }
                const range = document.createRange();
                if (last) {
                    range.setStart(last, last.textContent.length);
                } else {
                    range.selectNodeContents(body);
                    range.collapse(false);
                }
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """);
    }
}
