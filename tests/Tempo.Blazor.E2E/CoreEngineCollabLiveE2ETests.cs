using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// R.5.22 — live 2-browser realtime collaboration over the SignalR hub. Two tabs open the same
/// document; typing in one converges into the other through the engine's OT control + the
/// existing collaboration transport. Requires BOTH the WASM demo (7106) AND the API hub (5100).
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CoreEngineCollabLiveE2ETests : WasmTestBase
{
    private const string CollabUrl = "/core-engine-collab";

    private static async Task<IPage> OpenConnectedTabAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"https://localhost:7106{CollabUrl}", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        // The engine mounted...
        await page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return el && el.getAttribute('data-core-engine-ready') === 'true'; }",
            null, new PageWaitForFunctionOptions { Timeout = 45000 });
        // ...and collaboration connected to the hub.
        await page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('[data-testid=\"collab-connected\"]'); return el && el.textContent.trim() === 'true'; }",
            null, new PageWaitForFunctionOptions { Timeout = 45000 });
        return page;
    }

    private static Task<string> ParagraphTextAsync(IPage page) => page.EvaluateAsync<string>(
        @"() => { const host = document.querySelector('[data-testid=""document-core-engine-host""]');
                  const blk = host && host.querySelector('[data-render-block-id=""p1""]');
                  return blk ? (blk.textContent || '') : ''; }");

    private static async Task TypeAtEndAsync(IPage page, string text)
    {
        var box = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        box.Should().NotBeNull();
        await page.Mouse.ClickAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync(text, new KeyboardTypeOptions { Delay = 30 });
    }

    [TestMethod]
    public async Task R106_TwoTabs_TypingInOne_ConvergesIntoTheOther_OverSignalR()
    {
        // Two independent WASM app instances (separate contexts) = two collaborators.
        var ctxA = await CreateContextAsync();
        var ctxB = await CreateContextAsync();
        var pageA = await OpenConnectedTabAsync(ctxA);
        var pageB = await OpenConnectedTabAsync(ctxB);

        // Both start from the same seeded document.
        (await ParagraphTextAsync(pageA)).Should().Be("Shared");
        (await ParagraphTextAsync(pageB)).Should().Be("Shared");

        // Tab A types → must appear in Tab B (op broadcast through the hub + applied via OT).
        await TypeAtEndAsync(pageA, "-A");
        await pageB.WaitForFunctionAsync(
            @"() => { const host = document.querySelector('[data-testid=""document-core-engine-host""]');
                      const blk = host && host.querySelector('[data-render-block-id=""p1""]');
                      return blk && /Shared-A/.test(blk.textContent || ''); }",
            null, new PageWaitForFunctionOptions { Timeout = 20000 });

        // Tab B types → must appear back in Tab A (bidirectional).
        await TypeAtEndAsync(pageB, "-B");
        await pageA.WaitForFunctionAsync(
            @"() => { const host = document.querySelector('[data-testid=""document-core-engine-host""]');
                      const blk = host && host.querySelector('[data-render-block-id=""p1""]');
                      return blk && /-B/.test(blk.textContent || '') && /Shared-A/.test(blk.textContent || ''); }",
            null, new PageWaitForFunctionOptions { Timeout = 20000 });

        // Both tabs CONVERGE to identical text.
        var finalA = await ParagraphTextAsync(pageA);
        var finalB = await ParagraphTextAsync(pageB);
        finalA.Should().Be(finalB, "both tabs converge to identical text over the live SignalR transport");
        finalA.Should().Contain("Shared").And.Contain("-A").And.Contain("-B");

        TestContext.WriteLine($"R.5.22 live collab: tab A + tab B converged to '{finalA}' over the SignalR hub.");
    }
}
