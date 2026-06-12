using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Comprehensive coverage of placing/arranging content in the email editor canvas — the
/// real HTML5 drag&amp;drop path the user exercised in the bug report (blocks could not be
/// dropped into the canvas). Drives genuine <c>dragstart</c>/<c>dragover</c>/<c>drop</c> events
/// (the drop zones only render after <c>dragstart</c> flips the editor into drag mode), plus the
/// click-to-add, reorder, move-between-columns, delete and undo/redo affordances.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EmailEditorDragDropE2ETests : EmailTemplateE2ETestBase
{
    private async Task<IPage> OpenEditorAsync(Guid templateId)
    {
        var page = await OpenAsync($"/email-templates/edit/{templateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });
        await page.WaitForSelectorAsync("[data-tm-canvas-doc]", new() { Timeout = 15000 });
        return page;
    }

    /// <summary>An empty column's persistent drop target — the spot the user aimed at.</summary>
    private const string EmptyDropSelector = "[data-tm-drop-empty]";

    /// <summary>
    /// Drives an HTML5 drag: dispatch <c>dragstart</c> on the source, then wait for the editor to
    /// enter drag mode (the drag payload is registered, drop zones light up) before dispatching the
    /// drop on the target. The wait mirrors a real human drag (where the payload is always set long
    /// before the slow mouse release) and avoids the automation-only race where a synthetic drop
    /// could otherwise beat the dragstart round-trip. Position-faithful pixel-miss reproduction is
    /// not possible via Playwright (its drag always lands on a valid target), so these tests verify
    /// the place/arrange pipeline + that the drop targets exist and are hit; the human hit-area fix
    /// is verified visually.
    /// </summary>
    private static async Task Html5DragAsync(IPage page, string sourceSelector, string targetSelector)
    {
        var source = page.Locator(sourceSelector).First;
        await source.ScrollIntoViewIfNeededAsync();

        var startDt = await page.EvaluateHandleAsync("() => new DataTransfer()");
        await source.DispatchEventAsync("dragstart", new Dictionary<string, object> { ["dataTransfer"] = startDt });

        // Wait until the drag payload is registered (drop targets become active).
        await page.WaitForSelectorAsync(targetSelector, new() { Timeout = 10000 });

        var target = page.Locator(targetSelector).First;
        var dt = await page.EvaluateHandleAsync("() => new DataTransfer()");
        await target.DispatchEventAsync("dragenter", new Dictionary<string, object> { ["dataTransfer"] = dt });
        await target.DispatchEventAsync("dragover", new Dictionary<string, object> { ["dataTransfer"] = dt });
        await target.DispatchEventAsync("drop", new Dictionary<string, object> { ["dataTransfer"] = dt });
        await source.DispatchEventAsync("dragend", new Dictionary<string, object> { ["dataTransfer"] = dt });
    }

    private static Task<int> BlockCountAsync(IPage page)
        => page.Locator("[data-tm-block-id]").CountAsync();

    // ── The reported bug: drop a toolbox block into the canvas ────────────────────────────────

    [TestMethod]
    public async Task Drop_TextBlockFromToolbox_IntoEmptyColumn_AddsBlock()
    {
        var page = await OpenEditorAsync(NewsletterTemplateId);
        await page.WaitForSelectorAsync(EmptyDropSelector, new() { Timeout = 15000 });
        var before = await BlockCountAsync(page);

        // Newsletter seed has an empty multi-column section — the exact spot the user aimed at.
        await Html5DragAsync(page, "[data-tm-block='text']", EmptyDropSelector);

        await page.WaitForFunctionAsync("n => document.querySelectorAll('[data-tm-block-id]').length > n", before,
            new() { Timeout = 15000 });
        (await BlockCountAsync(page)).Should().BeGreaterThan(before);
    }

    [TestMethod]
    public async Task DragMode_EmptyColumnsExposeLargeStableDropTargets()
    {
        // The fix: empty columns keep a large drop target while dragging (previously they collapsed
        // to an ~8px strip, which is why content could not be placed). Verify the targets are tall.
        var page = await OpenEditorAsync(NewsletterTemplateId);
        await page.WaitForSelectorAsync(EmptyDropSelector, new() { Timeout = 15000 });

        var source = page.Locator("[data-tm-block='text']").First;
        var dt = await page.EvaluateHandleAsync("() => new DataTransfer()");
        await source.DispatchEventAsync("dragstart", new Dictionary<string, object> { ["dataTransfer"] = dt });
        await page.WaitForSelectorAsync($"{EmptyDropSelector}.is-drop-active", new() { Timeout = 10000 });

        var minHeight = await page.Locator(EmptyDropSelector).First.EvaluateAsync<double>(
            "el => el.getBoundingClientRect().height");
        minHeight.Should().BeGreaterThan(40, "the empty-column drop target stays large during a drag");

        await SaveNamedScreenshotAsync(page, "12-drag-dropzones.png");
        await source.DispatchEventAsync("dragend", new Dictionary<string, object> { ["dataTransfer"] = dt });
    }

    [TestMethod]
    public async Task Drop_ButtonBlockFromToolbox_AddsBlock()
    {
        var page = await OpenEditorAsync(WelcomeTemplateId);
        var before = await BlockCountAsync(page);

        // Welcome seed is a single column with blocks — drop onto an insert zone (appears on drag).
        await Html5DragAsync(page, "[data-tm-block='button']", "[data-tm-drop-col]");

        await page.WaitForFunctionAsync("n => document.querySelectorAll('[data-tm-block-id]').length > n", before,
            new() { Timeout = 15000 });
    }

    // ── Click-to-add still works ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ClickToAdd_AppendsBlockToColumn()
    {
        var page = await OpenEditorAsync(WelcomeTemplateId);
        var before = await BlockCountAsync(page);

        await page.Locator("[data-tm-block='image']").First.ClickAsync();

        await page.WaitForFunctionAsync("n => document.querySelectorAll('[data-tm-block-id]').length > n", before,
            new() { Timeout = 15000 });
    }

    // ── Reorder an existing block within a column ─────────────────────────────────────────────

    [TestMethod]
    public async Task Reorder_MoveBlockDown_ChangesOrder()
    {
        var page = await OpenEditorAsync(WelcomeTemplateId);
        var firstId = await page.Locator("[data-tm-block-id]").First.GetAttributeAsync("data-tm-block-id");

        // Select the first block, then use its move-down action.
        await page.Locator($"[data-tm-block-id='{firstId}']").ClickAsync();
        await page.Locator($"[data-tm-block-id='{firstId}'] [data-tm-block-action='down']").ClickAsync();

        await page.WaitForFunctionAsync(
            "id => document.querySelector('[data-tm-block-id]').getAttribute('data-tm-block-id') !== id",
            firstId, new() { Timeout = 15000 });
    }

    // ── Delete + undo/redo ────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteBlock_ThenUndoRedo_RestoresAndReRemoves()
    {
        var page = await OpenEditorAsync(WelcomeTemplateId);
        var before = await BlockCountAsync(page);

        await page.Locator("[data-tm-block-id]").First.ClickAsync();
        await page.Locator("[data-tm-block-action='delete']").First.ClickAsync();
        await page.WaitForFunctionAsync("n => document.querySelectorAll('[data-tm-block-id]').length < n", before,
            new() { Timeout = 15000 });

        await page.Locator("[data-tm-undo]").ClickAsync();
        await page.WaitForFunctionAsync("n => document.querySelectorAll('[data-tm-block-id]').length === n", before,
            new() { Timeout = 15000 });

        await page.Locator("[data-tm-redo]").ClickAsync();
        await page.WaitForFunctionAsync("n => document.querySelectorAll('[data-tm-block-id]').length < n", before,
            new() { Timeout = 15000 });
    }
}
