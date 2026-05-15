using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests for Tempo-specific block types embedded in the Notion editor:
/// Diagram (order 30 on Page1) and Wireframe (order 31 on Page1).
/// </summary>
[TestClass]
public class NotionTempoBlocksE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        using var http = new HttpClient();
        try { await http.PostAsync("https://localhost:5100/api/notion/reset", null); }
        catch { /* ignore if API unavailable or cert untrusted */ }

        var context = await CreateContextAsync();
        var page    = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    private async Task<ILocator> ScrollToDiagramBlockAsync(IPage page)
    {
        var block = page.Locator(".tm-notion-diagram-block").First;
        await block.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await block.ScrollIntoViewIfNeededAsync();
        await page.WaitForTimeoutAsync(500);
        return block;
    }

    private async Task<ILocator> ScrollToWireframeBlockAsync(IPage page)
    {
        var block = page.Locator(".tm-notion-wireframe-block").First;
        await block.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await block.ScrollIntoViewIfNeededAsync();
        await page.WaitForTimeoutAsync(500);
        return block;
    }

    /// <summary>
    /// Clicks the "Create Diagram" button and waits until the modal footer (save
    /// button) is visible, meaning the editor finished loading.
    /// </summary>
    private async Task<ILocator> OpenDiagramModalAsync(IPage page, ILocator diagramBlock)
    {
        var createBtn = diagramBlock.Locator(".tm-notion-media-upload-zone--diagram").First;
        await createBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await createBtn.ClickAsync();

        var modal = page.Locator(".tm-notion-diagram-edit-modal").First;
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // Wait for the save button — proves the modal finished loading
        var saveBtn = modal.Locator(".tm-notion-diagram-edit-modal__btn--primary").First;
        await saveBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        return modal;
    }

    /// <summary>
    /// Clicks the "Create Wireframe" button and waits until the modal save button
    /// is visible.
    /// </summary>
    private async Task<ILocator> OpenWireframeModalAsync(IPage page, ILocator wireframeBlock)
    {
        var createBtn = wireframeBlock.Locator(".tm-notion-media-upload-zone--wireframe").First;
        await createBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await createBtn.ClickAsync();

        var modal = page.Locator(".tm-notion-wireframe-edit-modal").First;
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var saveBtn = modal.Locator(".tm-notion-wireframe-edit-modal__btn--primary").First;
        await saveBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        return modal;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Diagram tests
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("An empty Diagram block shows the 'Create Diagram' upload-zone button")]
    public async Task DiagramBlock_Empty_ShowsCreateButton()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToDiagramBlockAsync(page);

        var createBtn = block.Locator(".tm-notion-media-upload-zone--diagram").First;
        await createBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await createBtn.IsVisibleAsync(), "Create Diagram button should be visible on an empty Diagram block");

        await TakeScreenshotAsync(page, "tempo_diagram_empty");
    }

    [TestMethod]
    [Description("Clicking 'Create Diagram' opens the diagram editor modal")]
    public async Task DiagramBlock_Create_OpensEditorModal()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToDiagramBlockAsync(page);
        var modal = await OpenDiagramModalAsync(page, block);

        Assert.IsTrue(await modal.IsVisibleAsync(), "Diagram edit modal should be visible after clicking Create");

        await TakeScreenshotAsync(page, "tempo_diagram_modal_open");
    }

    [TestMethod]
    [Description("Clicking the Discard button in the Diagram modal closes it")]
    public async Task DiagramBlock_Modal_CancelButton_Closes()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToDiagramBlockAsync(page);
        var modal = await OpenDiagramModalAsync(page, block);

        // Discard is the 2nd button in the footer
        var discardBtn = modal.Locator(".tm-notion-diagram-edit-modal__btn:not(.tm-notion-diagram-edit-modal__btn--primary)").First;
        await discardBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 3000 });
        await discardBtn.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        Assert.IsFalse(await modal.IsVisibleAsync(), "Diagram edit modal should be closed after clicking Discard");

        await TakeScreenshotAsync(page, "tempo_diagram_modal_discarded");
    }

    [TestMethod]
    [Description("Clicking Save in the Diagram modal closes it and shows the block figure")]
    public async Task DiagramBlock_Modal_SaveButton_ClosesAndShowsPreview()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToDiagramBlockAsync(page);
        var modal = await OpenDiagramModalAsync(page, block);

        var saveBtn = modal.Locator(".tm-notion-diagram-edit-modal__btn--primary").First;
        await saveBtn.ClickAsync();

        // Modal should disappear
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });
        Assert.IsFalse(await modal.IsVisibleAsync(), "Diagram edit modal should close after Save");

        // Block figure should now be visible
        await block.ScrollIntoViewIfNeededAsync();
        var figure = block.Locator(".tm-notion-diagram-block__figure").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.IsTrue(await figure.IsVisibleAsync(), "Diagram block figure should be visible after saving");

        await TakeScreenshotAsync(page, "tempo_diagram_saved");
    }

    [TestMethod]
    [Description("Clicking the Edit button on a saved Diagram block reopens the modal")]
    public async Task DiagramBlock_Edit_OpensSameModal()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToDiagramBlockAsync(page);

        // 1. Create & save to get the block into "has document" state
        var modal   = await OpenDiagramModalAsync(page, block);
        var saveBtn = modal.Locator(".tm-notion-diagram-edit-modal__btn--primary").First;
        await saveBtn.ClickAsync();
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        // 2. Hover the figure preview so the overlay appears, then click Edit
        await block.ScrollIntoViewIfNeededAsync();
        var figure = block.Locator(".tm-notion-diagram-block__figure").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        await figure.HoverAsync();

        var editBtn = block.Locator(".tm-notion-diagram-block__edit-btn").First;
        await editBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await editBtn.ClickAsync();

        // 3. Modal should reopen
        var modal2 = page.Locator(".tm-notion-diagram-edit-modal").First;
        await modal2.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await modal2.IsVisibleAsync(), "Diagram edit modal should reopen when clicking Edit on a saved diagram");

        await TakeScreenshotAsync(page, "tempo_diagram_edit_reopen");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Wireframe tests
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("An empty Wireframe block shows the 'Create Wireframe' upload-zone button")]
    public async Task WireframeBlock_Empty_ShowsCreateButton()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToWireframeBlockAsync(page);

        var createBtn = block.Locator(".tm-notion-media-upload-zone--wireframe").First;
        await createBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await createBtn.IsVisibleAsync(), "Create Wireframe button should be visible on an empty Wireframe block");

        await TakeScreenshotAsync(page, "tempo_wireframe_empty");
    }

    [TestMethod]
    [Description("Clicking 'Create Wireframe' opens the wireframe editor modal")]
    public async Task WireframeBlock_Create_OpensEditorModal()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToWireframeBlockAsync(page);
        var modal = await OpenWireframeModalAsync(page, block);

        Assert.IsTrue(await modal.IsVisibleAsync(), "Wireframe edit modal should be visible after clicking Create");

        await TakeScreenshotAsync(page, "tempo_wireframe_modal_open");
    }

    [TestMethod]
    [Description("Clicking the Discard button in the Wireframe modal closes it")]
    public async Task WireframeBlock_Modal_CancelButton_Closes()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToWireframeBlockAsync(page);
        var modal = await OpenWireframeModalAsync(page, block);

        var discardBtn = modal.Locator(".tm-notion-wireframe-edit-modal__btn:not(.tm-notion-wireframe-edit-modal__btn--primary)").First;
        await discardBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 3000 });
        await discardBtn.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        Assert.IsFalse(await modal.IsVisibleAsync(), "Wireframe edit modal should be closed after clicking Discard");

        await TakeScreenshotAsync(page, "tempo_wireframe_modal_discarded");
    }

    [TestMethod]
    [Description("Clicking Save in the Wireframe modal closes it and shows the block figure")]
    public async Task WireframeBlock_Modal_SaveButton_ClosesAndShowsPreview()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToWireframeBlockAsync(page);
        var modal = await OpenWireframeModalAsync(page, block);

        var saveBtn = modal.Locator(".tm-notion-wireframe-edit-modal__btn--primary").First;
        await saveBtn.ClickAsync();

        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });
        Assert.IsFalse(await modal.IsVisibleAsync(), "Wireframe edit modal should close after Save");

        await block.ScrollIntoViewIfNeededAsync();
        var figure = block.Locator(".tm-notion-wireframe-block__figure").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.IsTrue(await figure.IsVisibleAsync(), "Wireframe block figure should be visible after saving");

        await TakeScreenshotAsync(page, "tempo_wireframe_saved");
    }

    [TestMethod]
    [Description("Clicking the Edit button on a saved Wireframe block reopens the modal")]
    public async Task WireframeBlock_Edit_OpensSameModal()
    {
        var page  = await OpenNotionEditorAsync();
        var block = await ScrollToWireframeBlockAsync(page);

        // 1. Create & save
        var modal   = await OpenWireframeModalAsync(page, block);
        var saveBtn = modal.Locator(".tm-notion-wireframe-edit-modal__btn--primary").First;
        await saveBtn.ClickAsync();
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        // 2. Hover & click Edit
        await block.ScrollIntoViewIfNeededAsync();
        var figure = block.Locator(".tm-notion-wireframe-block__figure").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        await figure.HoverAsync();

        var editBtn = block.Locator(".tm-notion-wireframe-block__edit-btn").First;
        await editBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await editBtn.ClickAsync();

        // 3. Modal should reopen
        var modal2 = page.Locator(".tm-notion-wireframe-edit-modal").First;
        await modal2.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await modal2.IsVisibleAsync(), "Wireframe edit modal should reopen when clicking Edit on a saved wireframe");

        await TakeScreenshotAsync(page, "tempo_wireframe_edit_reopen");
    }
}
