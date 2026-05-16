using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for JS-owned comment anchors and comment panel bridge.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeCommentTests : DocumentEditorE2ETestBase
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
    public async Task Phase10_SelectTextAddCommentCreatesRuntimeAnchorAndHighlight()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase10-comment-{DateTimeOffset.UtcNow:HHmmssfff}";

        var selectedText = await SelectFirstVisibleTextRangeAsync(page, start: 4, length: 7);
        Assert.IsFalse(string.IsNullOrWhiteSpace(selectedText), "The test should select text before adding a comment.");

        await AddCommentFromCurrentSelectionAsync(page, marker);

        await Assertions.Expect(page.Locator("[data-testid='document-comment-thread']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor").Filter(new() { HasText = selectedText }).First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        var comment = await ReadCommentAnchorAsync(page, marker);
        Assert.IsNotNull(comment, "The JS runtime snapshot should contain the newly created comment.");
        Assert.AreEqual(1, comment!.Type);
        Assert.IsFalse(comment.IsOrphaned);
        Assert.AreEqual(4, comment.StartOffset);
        Assert.AreEqual(11, comment.EndOffset);
    }

    [TestMethod]
    public async Task Phase10_InsertBeforeCommentKeepsHighlightOnOriginalText()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase10-shift-{DateTimeOffset.UtcNow:HHmmssfff}";
        var prefix = $"pre{DateTimeOffset.UtcNow:HHmmssfff}-";

        var selectedText = await SelectFirstVisibleTextRangeAsync(page, start: 6, length: 8);
        await AddCommentFromCurrentSelectionAsync(page, marker);

        var before = await ReadCommentAnchorAsync(page, marker);
        Assert.IsNotNull(before, "The comment anchor should exist before typing.");

        await PlaceCaretAtFirstVisibleBlockOffsetAsync(page, offset: 0);
        await page.Keyboard.TypeAsync(prefix);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor").Filter(new() { HasText = selectedText }).First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        var highlightedText = await ReadCommentHighlightTextAsync(page, before!.Id);
        Assert.AreEqual(selectedText, highlightedText);

        var after = await ReadCommentAnchorAsync(page, marker);
        Assert.IsNotNull(after, "The comment anchor should remain in the JS runtime snapshot after typing before it.");
        Assert.AreEqual(before.StartOffset + prefix.Length, after!.StartOffset);
        Assert.AreEqual(before.EndOffset + prefix.Length, after.EndOffset);
        Assert.IsFalse(after.IsOrphaned);
    }

    [TestMethod]
    public async Task Phase10_ClickCommentPanelScrollsRuntimeAnchor()
    {
        var page = _page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase10-scroll-{DateTimeOffset.UtcNow:HHmmssfff}";

        await SelectFirstVisibleTextRangeAsync(page, start: 3, length: 6);
        await AddCommentFromCurrentSelectionAsync(page, marker);

        await page.EvaluateAsync(
            """
            () => document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-document-inline--comment-anchor--selected')
                .forEach(node => node.classList.remove('tm-document-inline--comment-anchor--selected'))
            """);

        await page.Locator("[data-testid='document-comment-thread']").Filter(new() { HasText = marker })
            .Locator("[data-testid='document-comment-thread-select']")
            .ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor--selected").First)
            .ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    private static async Task AddCommentFromCurrentSelectionAsync(IPage page, string marker)
    {
        await page.Locator("[data-testid='document-ribbon-tab-review']").ClickAsync();
        await page.Locator("[data-testid='document-add-comment']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-new-composer']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-comment-input']").FillAsync(marker);
        await page.Locator("[data-testid='document-comment-submit']").ClickAsync();
    }

    private static Task<string> SelectFirstVisibleTextRangeAsync(IPage page, int start, int length)
    {
        return page.EvaluateAsync<string>(
            """
            ({ start, length }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const isVisible = el => {
                    if (!el || el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual')) return false;
                    const rect = el.getBoundingClientRect();
                    const style = getComputedStyle(el);
                    return rect.width > 0
                        && rect.height > 0
                        && style.visibility !== 'hidden'
                        && style.display !== 'none';
                };
                const block = Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body p.tm-wysiwyg-block[data-block-id]') || []).find(isVisible)
                    || Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body [data-block-id]') || []).find(isVisible);
                if (!block) throw new Error('Visible text block was not found.');

                const resolve = absoluteOffset => {
                    const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                    let current = 0;
                    let node;
                    while ((node = walker.nextNode())) {
                        const text = node.textContent || '';
                        if (absoluteOffset <= current + text.length) {
                            return { node, offset: Math.max(0, Math.min(absoluteOffset - current, text.length)) };
                        }
                        current += text.length;
                    }
                    return null;
                };

                const textLength = block.textContent.length;
                const rangeStart = Math.max(0, Math.min(start, textLength));
                const rangeEnd = Math.max(rangeStart, Math.min(start + length, textLength));
                const startPos = resolve(rangeStart);
                const endPos = resolve(rangeEnd);
                if (!startPos || !endPos) throw new Error('Selectable text range was not found.');

                const range = document.createRange();
                range.setStart(startPos.node, startPos.offset);
                range.setEnd(endPos.node, endPos.offset);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
                return range.toString();
            }
            """,
            new { start, length });
    }

    private static Task PlaceCaretAtFirstVisibleBlockOffsetAsync(IPage page, int offset)
    {
        return page.EvaluateAsync(
            """
            offset => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const isVisible = el => {
                    if (!el || el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual')) return false;
                    const rect = el.getBoundingClientRect();
                    const style = getComputedStyle(el);
                    return rect.width > 0
                        && rect.height > 0
                        && style.visibility !== 'hidden'
                        && style.display !== 'none';
                };
                const block = Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body p.tm-wysiwyg-block[data-block-id]') || []).find(isVisible)
                    || Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body [data-block-id]') || []).find(isVisible);
                if (!block) throw new Error('Visible text block was not found.');

                const resolve = absoluteOffset => {
                    const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                    let current = 0;
                    let node;
                    while ((node = walker.nextNode())) {
                        const text = node.textContent || '';
                        if (absoluteOffset <= current + text.length) {
                            return { node, offset: Math.max(0, Math.min(absoluteOffset - current, text.length)) };
                        }
                        current += text.length;
                    }
                    const fallback = block.appendChild(document.createTextNode(''));
                    return { node: fallback, offset: 0 };
                };

                const pos = resolve(Math.max(0, offset));
                const range = document.createRange();
                range.setStart(pos.node, pos.offset);
                range.collapse(true);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            offset);
    }

    private static Task<string> ReadCommentHighlightTextAsync(IPage page, string commentId)
    {
        return page.EvaluateAsync<string>(
            """
            commentId => {
                const highlight = document.querySelector(`[data-testid="document-wysiwyg-host"] .tm-document-inline--comment-anchor[data-comment-id="${CSS.escape(commentId)}"]`);
                return highlight?.textContent || '';
            }
            """,
            commentId);
    }

    private static Task<CommentAnchorSnapshot?> ReadCommentAnchorAsync(IPage page, string marker)
    {
        return page.EvaluateAsync<CommentAnchorSnapshot?>(
            """
            marker => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = raw ? JSON.parse(raw) : {};
                const comments = snapshot.Document?.Comments || snapshot.document?.comments || [];
                const comment = comments.find(item => {
                    const entries = item.Entries || item.entries || [];
                    return entries.some(entry => String(entry.Text || entry.text || '').includes(marker));
                });
                if (!comment) return null;
                const anchor = comment.Anchor || comment.anchor || {};
                return {
                    id: comment.Id || comment.id || '',
                    type: anchor.Type ?? anchor.type,
                    blockId: anchor.BlockId || anchor.blockId || '',
                    startOffset: anchor.StartOffset ?? anchor.startOffset,
                    endOffset: anchor.EndOffset ?? anchor.endOffset,
                    isOrphaned: !!(anchor.IsOrphaned ?? anchor.isOrphaned)
                };
            }
            """,
            marker);
    }

    public sealed class CommentAnchorSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public int Type { get; set; }

        public string BlockId { get; set; } = string.Empty;

        public int StartOffset { get; set; }

        public int EndOffset { get; set; }

        public bool IsOrphaned { get; set; }
    }
}
