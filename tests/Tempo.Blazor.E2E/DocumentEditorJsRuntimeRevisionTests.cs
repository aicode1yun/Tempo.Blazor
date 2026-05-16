using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for JS-owned track changes and revision review.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeRevisionTests : DocumentEditorE2ETestBase
{
    private IPage? _page;

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (_page is not null)
        {
            await _page.Context.CloseAsync();
            _page = null;
        }
    }

    [TestMethod]
    public async Task Phase9_TrackChangesTypingCreatesRuntimeRevisionPanelItem()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase9-insert-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EnableTrackChangesAsync(page);
        await EditorTypeAsync(page, marker);

        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision--insert").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync();

        var snapshotHasRevision = await PageHasPendingRevisionAsync(page, marker);
        Assert.IsTrue(snapshotHasRevision, "The JS canonical snapshot should contain the pending insertion revision.");
    }

    [TestMethod]
    public async Task Phase9_TrackChangesDeleteKeepsDeletedTextAsRedStrike()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await EnableTrackChangesAsync(page);
        await SelectFirstTextRunRangeAsync(page, length: 7);
        await page.Keyboard.PressAsync("Backspace");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision--delete").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").First)
            .ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task Phase9_TrackChangesEnterKeepsStructuralRevisionPanelItem()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase9-enter-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EnableTrackChangesAsync(page);
        await EditorTypeAsync(page, marker);
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = "SplitBlock" }).First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase9_TrackChangesPasteCreatesSingleInsertionRevision()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase9-paste-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EnableTrackChangesAsync(page);
        await EditorPastePlainTextAsync(page, marker);

        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision--insert").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task Phase9_AcceptInsertionIsUndoableAndRestoresPendingRevision()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase9-accept-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EnableTrackChangesAsync(page);
        await EditorTypeAsync(page, marker);
        var item = page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }).First;
        await Assertions.Expect(item).ToBeVisibleAsync(new() { Timeout = 10000 });

        await item.Locator("[data-testid='document-revision-accept']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToHaveCountAsync(0, new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision--insert").Filter(new() { HasText = marker }))
            .ToHaveCountAsync(0);

        await EditorPressUndoAsync(page);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision--insert").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase9_RejectInsertionRemovesInsertedText()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase9-reject-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EnableTrackChangesAsync(page);
        await EditorTypeAsync(page, marker);
        var item = page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }).First;
        await Assertions.Expect(item).ToBeVisibleAsync(new() { Timeout = 10000 });

        await item.Locator("[data-testid='document-revision-reject']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToHaveCountAsync(0, new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']").Filter(new() { HasText = marker }))
            .ToHaveCountAsync(0);
    }

    private static async Task EnableTrackChangesAsync(IPage page)
    {
        await page.Locator("[data-testid='document-ribbon-tab-review']").ClickAsync();
        var button = page.Locator("[data-testid='document-track-changes']");
        if (await button.GetAttributeAsync("aria-pressed") != "true")
        {
            await button.ClickAsync();
        }
    }

    private static Task SelectFirstTextRunRangeAsync(IPage page, int length)
    {
        return page.EvaluateAsync(
            """
            length => {
                const inline = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-block[data-block-id] [data-inline-id]');
                const text = inline && Array.from(inline.childNodes).find(node => node.nodeType === Node.TEXT_NODE && (node.textContent || '').length >= length);
                if (!text) throw new Error('No selectable text run found.');
                const range = document.createRange();
                range.setStart(text, 0);
                range.setEnd(text, length);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
            }
            """,
            length);
    }

    private static async Task EditorPastePlainTextAsync(IPage page, string text)
    {
        var body = await WaitForWysiwygBodyAsync(page);
        var textBlock = body.Locator(".tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)").First;
        await textBlock.ClickAsync(new() { Position = new() { X = 12, Y = 12 } });
        await page.EvaluateAsync(
            """
            text => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable]');
                const event = new Event('paste', { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'clipboardData', {
                    value: {
                        getData: type => type === 'text/plain' ? text : '',
                        items: [],
                        files: []
                    }
                });
                body.dispatchEvent(event);
            }
            """,
            text);
    }

    private static Task<bool> PageHasPendingRevisionAsync(IPage page, string marker)
    {
        return page.EvaluateAsync<bool>(
            """
            marker => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = raw ? JSON.parse(raw) : {};
                const revisions = snapshot.Document?.Revisions || snapshot.document?.revisions || [];
                return revisions.some(revision => {
                    const action = revision.Action ?? revision.action;
                    const payload = String(revision.PayloadJson ?? revision.payloadJson ?? '');
                    return action === 0 && payload.includes(marker);
                });
            }
            """,
            marker);
    }
}
