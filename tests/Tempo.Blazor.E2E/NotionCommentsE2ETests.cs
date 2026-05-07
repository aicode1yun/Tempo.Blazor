using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests covering all comment features in the Notion editor:
/// block comments, page-level comments, and text-anchor (inline) comments.
/// </summary>
[TestClass]
public class NotionCommentsE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Setup
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Block Comments
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Open block comment panel from block context menu")]
    public async Task BlockComment_OpenPanel()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);

        var panel = page.Locator(".tm-nbcp").First;
        Assert.IsTrue(await panel.IsVisibleAsync(), "Block comment panel should be visible");
        await TakeScreenshotAsync(page, "block_comment_open");
    }

    [TestMethod]
    [Description("Add a new block comment")]
    public async Task BlockComment_AddComment()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);
        await AddBlockCommentAsync(page, "Test block comment");

        var entryText = page.Locator(".tm-nbcp__entry-text").Filter(new() { HasText = "Test block comment" }).First;
        await entryText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await entryText.IsVisibleAsync(), "New block comment should appear in thread");
        await TakeScreenshotAsync(page, "block_comment_add");
    }

    [TestMethod]
    [Description("Reply to an existing block comment")]
    public async Task BlockComment_Reply()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);
        await AddBlockCommentAsync(page, "Initial comment");
        await AddBlockCommentAsync(page, "Reply text");

        var replyText = page.Locator(".tm-nbcp__entry-text").Filter(new() { HasText = "Reply text" }).First;
        await replyText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await replyText.IsVisibleAsync(), "Reply should appear in thread");
        await TakeScreenshotAsync(page, "block_comment_reply");
    }

    [TestMethod]
    [Description("Resolve and unresolve a block comment")]
    public async Task BlockComment_ResolveUnresolve()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);
        await AddBlockCommentAsync(page, "Comment to resolve");

        // Resolve
        var resolveBtn = page.Locator(".tm-nbcp__resolve-btn").Filter(new() { HasText = "Resolve" }).First;
        await resolveBtn.ClickAsync();

        var resolvedBanner = page.Locator(".tm-nbcp__resolved-banner").First;
        await resolvedBanner.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await resolvedBanner.IsVisibleAsync(), "Resolved banner should appear");

        // Unresolve
        var unresolveBtn = page.Locator(".tm-nbcp__resolve-btn--unresolve").First;
        await unresolveBtn.ClickAsync();

        var textarea = page.Locator(".tm-nbcp__reply-input").First;
        await textarea.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await textarea.IsVisibleAsync(), "Reply input should re-appear after unresolve");
        await TakeScreenshotAsync(page, "block_comment_resolve_unresolve");
    }

    [TestMethod]
    [Description("Edit a block comment entry")]
    public async Task BlockComment_Edit()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);
        await AddBlockCommentAsync(page, "Comment to edit");

        var editBtn = page.Locator(".tm-nbcp__entry-action").Filter(new() { HasText = "Edit" }).First;
        await editBtn.ClickAsync();

        var editInput = page.Locator(".tm-nbcp__edit-input").First;
        await editInput.FillAsync("Edited comment");

        var saveBtn = page.Locator(".tm-nbcp__edit-save").First;
        await saveBtn.ClickAsync();

        var editedText = page.Locator(".tm-nbcp__entry-text").Filter(new() { HasText = "Edited comment" }).First;
        await editedText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await editedText.IsVisibleAsync(), "Edited text should be displayed");
        await TakeScreenshotAsync(page, "block_comment_edit");
    }

    [TestMethod]
    [Description("Delete a block comment entry with confirm dialog")]
    public async Task BlockComment_Delete()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);
        await AddBlockCommentAsync(page, "Comment to delete");

        var entryBefore = page.Locator(".tm-nbcp__entry-text").Filter(new() { HasText = "Comment to delete" }).First;
        Assert.IsTrue(await entryBefore.IsVisibleAsync());

        var deleteBtn = page.Locator(".tm-nbcp__entry-action--danger").First;
        await deleteBtn.ClickAsync();

        var okBtn = page.Locator(".tm-dialog-btn-ok").First;
        await okBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await okBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var entryAfter = page.Locator(".tm-nbcp__entry-text").Filter(new() { HasText = "Comment to delete" });
        Assert.IsTrue(await entryAfter.CountAsync() == 0, "Deleted comment should disappear");
        await TakeScreenshotAsync(page, "block_comment_delete");
    }

    [TestMethod]
    [Description("Close block comment panel")]
    public async Task BlockComment_ClosePanel()
    {
        var page = await OpenNotionEditorAsync();
        await OpenBlockCommentPanelAsync(page);

        var closeBtn = page.Locator(".tm-nbcp__close-btn").First;
        await closeBtn.ClickAsync();

        var panel = page.Locator(".tm-nbcp").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
        Assert.IsFalse(await panel.IsVisibleAsync(), "Panel should be closed");
        await TakeScreenshotAsync(page, "block_comment_close");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Page Comments
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Page comment panel shows unresolved count badge")]
    public async Task PageComment_ShowsBadge()
    {
        var page = await OpenNotionEditorAsync();
        var badge = page.Locator(".tm-npcp__badge").First;
        await badge.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var count = await badge.TextContentAsync();
        Assert.IsFalse(string.IsNullOrEmpty(count), "Badge should show unresolved count");
        await TakeScreenshotAsync(page, "page_comment_badge");
    }

    [TestMethod]
    [Description("Expand page comment panel and add new comment")]
    public async Task PageComment_AddComment()
    {
        var page = await OpenNotionEditorAsync();
        await ExpandPageCommentPanelAsync(page);

        var textarea = page.Locator(".tm-npcp__new-comment .tm-npcp__reply-input").First;
        await textarea.FillAsync("New page comment");

        var sendBtn = page.Locator(".tm-npcp__reply-send").First;
        await sendBtn.ClickAsync();

        var entryText = page.Locator(".tm-npcp__entry-text").Filter(new() { HasText = "New page comment" }).First;
        await entryText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await entryText.IsVisibleAsync(), "New page comment should appear");
        await TakeScreenshotAsync(page, "page_comment_add");
    }

    [TestMethod]
    [Description("Reply to a page comment thread")]
    public async Task PageComment_Reply()
    {
        var page = await OpenNotionEditorAsync();
        await ExpandPageCommentPanelAsync(page);

        // Click reply trigger on first thread
        var replyTrigger = page.Locator(".tm-npcp__reply-trigger").First;
        await replyTrigger.ClickAsync();

        var textarea = page.Locator(".tm-npcp__thread-reply .tm-npcp__reply-input").First;
        await textarea.FillAsync("Page reply");

        var sendBtn = page.Locator(".tm-npcp__thread-reply .tm-npcp__reply-send").First;
        await sendBtn.ClickAsync();

        var replyText = page.Locator(".tm-npcp__entry-text").Filter(new() { HasText = "Page reply" }).First;
        await replyText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await replyText.IsVisibleAsync(), "Reply should appear in thread");
        await TakeScreenshotAsync(page, "page_comment_reply");
    }

    [TestMethod]
    [Description("Resolve and unresolve a page comment")]
    public async Task PageComment_ResolveUnresolve()
    {
        var page = await OpenNotionEditorAsync();
        await ExpandPageCommentPanelAsync(page);
        await AddPageCommentAsync(page, "Comment to resolve");

        // Find the thread containing our new comment and resolve it
        var ourThread = page.Locator(".tm-npcp__thread").Filter(new() { HasText = "Comment to resolve" }).First;
        var resolveBtn = ourThread.Locator(".tm-npcp__thread-action").Filter(new() { HasText = "Resolve" }).First;
        await resolveBtn.ClickAsync();

        // Wait for the thread to gain resolved styling
        await ourThread.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        var threadClass = await ourThread.EvaluateAsync<string>("el => el.className");
        Assert.IsTrue(threadClass.Contains("tm-npcp__thread--resolved"), "Thread should be marked resolved");

        // Unresolve the same thread
        var unresolveBtn = ourThread.Locator(".tm-npcp__thread-action").Filter(new() { HasText = "Re-open" }).First;
        await unresolveBtn.ClickAsync();

        // Verify resolved class is removed
        await page.WaitForTimeoutAsync(500);
        threadClass = await ourThread.EvaluateAsync<string>("el => el.className");
        Assert.IsFalse(threadClass.Contains("tm-npcp__thread--resolved"), "Thread should no longer be resolved after unresolve");
        await TakeScreenshotAsync(page, "page_comment_resolve_unresolve");
    }

    [TestMethod]
    [Description("Edit a page comment entry")]
    public async Task PageComment_Edit()
    {
        var page = await OpenNotionEditorAsync();
        await ExpandPageCommentPanelAsync(page);
        // Pre-seeded comments have CanEdit=false; add a new one first.
        await AddPageCommentAsync(page, "Page comment to edit");

        var editBtn = page.Locator(".tm-npcp__entry-action").Filter(new() { HasText = "Edit" }).First;
        await editBtn.ClickAsync();

        var editInput = page.Locator(".tm-npcp__edit-input").First;
        await editInput.FillAsync("Edited page comment");

        var saveBtn = page.Locator(".tm-npcp__edit-save").First;
        await saveBtn.ClickAsync();

        var editedText = page.Locator(".tm-npcp__entry-text").Filter(new() { HasText = "Edited page comment" }).First;
        await editedText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await editedText.IsVisibleAsync(), "Edited page comment should be displayed");
        await TakeScreenshotAsync(page, "page_comment_edit");
    }

    [TestMethod]
    [Description("Delete a page comment entry with confirm dialog")]
    public async Task PageComment_Delete()
    {
        var page = await OpenNotionEditorAsync();
        await ExpandPageCommentPanelAsync(page);
        await AddPageCommentAsync(page, "Page comment to delete");

        var entryBefore = page.Locator(".tm-npcp__entry-text").Filter(new() { HasText = "Page comment to delete" }).First;
        Assert.IsTrue(await entryBefore.IsVisibleAsync());

        var deleteBtn = page.Locator(".tm-npcp__entry-action--danger").First;
        await deleteBtn.ClickAsync();

        var okBtn = page.Locator(".tm-dialog-btn-ok").First;
        await okBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await okBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var entryAfter = page.Locator(".tm-npcp__entry-text").Filter(new() { HasText = "Page comment to delete" });
        Assert.IsTrue(await entryAfter.CountAsync() == 0, "Deleted page comment should disappear");
        await TakeScreenshotAsync(page, "page_comment_delete");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Text / Inline Comments
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Select text and open text comment panel from inline toolbar")]
    public async Task TextComment_OpenPanel()
    {
        var page = await OpenNotionEditorAsync();
        await OpenTextCommentPanelAsync(page);

        var panel = page.Locator(".tm-ntcp").First;
        Assert.IsTrue(await panel.IsVisibleAsync(), "Text comment panel should be visible");
        await TakeScreenshotAsync(page, "text_comment_open");
    }

    [TestMethod]
    [Description("Add a comment to selected text")]
    public async Task TextComment_AddComment()
    {
        var page = await OpenNotionEditorAsync();
        await OpenTextCommentPanelAsync(page);
        await AddTextCommentAsync(page, "Text anchor comment");

        var entryText = page.Locator(".tm-ntcp__entry-text").Filter(new() { HasText = "Text anchor comment" }).First;
        await entryText.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await entryText.IsVisibleAsync(), "Text comment should appear");
        await TakeScreenshotAsync(page, "text_comment_add");
    }

    [TestMethod]
    [Description("Resolve a text comment")]
    public async Task TextComment_Resolve()
    {
        var page = await OpenNotionEditorAsync();
        await OpenTextCommentPanelAsync(page);
        await AddTextCommentAsync(page, "Comment to resolve");

        var resolveBtn = page.Locator(".tm-ntcp__resolve-btn").Filter(new() { HasText = "Resolve" }).First;
        await resolveBtn.ClickAsync();

        // Text comment panel closes automatically after resolve
        var panel = page.Locator(".tm-ntcp").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
        Assert.IsFalse(await panel.IsVisibleAsync(), "Text comment panel should close after resolve");
        await TakeScreenshotAsync(page, "text_comment_resolve");
    }

    [TestMethod]
    [Description("Delete a text comment entry with confirm dialog")]
    public async Task TextComment_Delete()
    {
        var page = await OpenNotionEditorAsync();
        await OpenTextCommentPanelAsync(page);
        await AddTextCommentAsync(page, "Text comment to delete");

        var entryBefore = page.Locator(".tm-ntcp__entry-text").Filter(new() { HasText = "Text comment to delete" }).First;
        Assert.IsTrue(await entryBefore.IsVisibleAsync());

        var deleteBtn = page.Locator(".tm-ntcp__entry-action--danger").First;
        await deleteBtn.ClickAsync();

        var okBtn = page.Locator(".tm-dialog-btn-ok").First;
        await okBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await okBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var entryAfter = page.Locator(".tm-ntcp__entry-text").Filter(new() { HasText = "Text comment to delete" });
        Assert.IsTrue(await entryAfter.CountAsync() == 0, "Deleted text comment should disappear");
        await TakeScreenshotAsync(page, "text_comment_delete");
    }

    [TestMethod]
    [Description("Close text comment panel")]
    public async Task TextComment_ClosePanel()
    {
        var page = await OpenNotionEditorAsync();
        await OpenTextCommentPanelAsync(page);

        var closeBtn = page.Locator(".tm-ntcp__close-btn").First;
        await closeBtn.ClickAsync();

        var panel = page.Locator(".tm-ntcp").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
        Assert.IsFalse(await panel.IsVisibleAsync(), "Text comment panel should be closed");
        await TakeScreenshotAsync(page, "text_comment_close");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task OpenBlockCommentPanelAsync(IPage page)
    {
        var firstBlock = page.Locator("[data-notion-block]").First;
        await firstBlock.HoverAsync();
        var menuBtn = firstBlock.Locator(".tm-notion-handle__btn").Last;
        await menuBtn.ClickAsync();
        var commentBtn = page.Locator(".tm-notion-ctx__item:has-text('Comment')").First;
        await commentBtn.ClickAsync();
        await page.Locator(".tm-nbcp").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    private async Task AddBlockCommentAsync(IPage page, string text)
    {
        var textarea = page.Locator(".tm-nbcp__reply-input").First;
        await textarea.FillAsync(text);
        var sendBtn = page.Locator(".tm-nbcp__reply-send").First;
        await sendBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);
    }

    private async Task ExpandPageCommentPanelAsync(IPage page)
    {
        var toggle = page.Locator(".tm-npcp__toggle").First;
        await toggle.ClickAsync();
        await page.Locator(".tm-npcp__body").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    private async Task AddPageCommentAsync(IPage page, string text)
    {
        var textarea = page.Locator(".tm-npcp__new-comment .tm-npcp__reply-input").First;
        await textarea.FillAsync(text);
        var sendBtn = page.Locator(".tm-npcp__reply-send").First;
        await sendBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);
    }

    private async Task OpenTextCommentPanelAsync(IPage page)
    {
        var firstEditable = page.Locator(".tm-notion-editable").First;
        await firstEditable.ClickAsync();
        await firstEditable.EvaluateAsync("el => { el.focus(); document.execCommand('selectAll', false, null); }");
        var toolbar = page.Locator(".tm-notion-inline-toolbar").First;
        await toolbar.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var commentBtn = toolbar.Locator("button[title*='Comment'], button[title*='Komentář']").First;
        await commentBtn.ClickAsync();
        await page.Locator(".tm-ntcp").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    private async Task AddTextCommentAsync(IPage page, string text)
    {
        var textarea = page.Locator(".tm-ntcp__reply-input").First;
        await textarea.FillAsync(text);
        var sendBtn = page.Locator(".tm-ntcp__reply-send").First;
        await sendBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);
    }
}
