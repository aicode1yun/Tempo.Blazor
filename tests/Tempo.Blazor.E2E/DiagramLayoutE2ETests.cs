using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests for diagram editor layout algorithms.
/// Verifies that all layout options correctly reposition nodes without NaN/invalid coordinates.
/// </summary>
[TestClass]
public class DiagramLayoutE2ETests : WasmTestBase
{
    private const string DiagramEditorUrl = "/diagram-editor";

    private async Task<IPage> PrepareDiagramPageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        // Navigate first, then set locale and reload so localStorage is accessible
        await page.GotoAsync($"{BaseUrl}{DiagramEditorUrl}");
        await page.EvaluateAsync("() => localStorage.setItem('tm-demo-culture', 'en')");
        await page.ReloadAsync();
        await WaitForAppReadyAsync(page);

        // Wait for diagram editor to be fully rendered (toolbar + canvas)
        await page.WaitForSelectorAsync(".tm-diagram-editor__toolbar", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15000
        });
        await page.WaitForTimeoutAsync(2000);

        // Load UML sample (creates multiple nodes locally – no HTTP wait needed)
        await page.GetByRole(AriaRole.Button, new() { Name = "Load UML sample" }).ClickAsync();
        await page.WaitForTimeoutAsync(500);

        // Ensure nodes rendered
        await page.WaitForSelectorAsync(".tm-diagram-node", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.WaitForTimeoutAsync(300);

        return page;
    }

    private async Task SelectAllNodesAsync(IPage page)
    {
        // Click first node to focus editor, then select all
        var firstNode = page.Locator(".tm-diagram-node").First;
        await firstNode.ClickAsync();
        await page.Keyboard.PressAsync("Control+a");
        await page.WaitForTimeoutAsync(500);
    }

    private async Task OpenLayoutDropdownAsync(IPage page)
    {
        var trigger = page.Locator(".tm-diagram-editor__toolbar .tm-dropdown-trigger")
            .Filter(new() { HasTextRegex = new Regex("Layout|Rozložit") });
        await trigger.ClickAsync();
        await page.WaitForTimeoutAsync(200);
    }

    private async Task ClickLayoutOptionAsync(IPage page, string optionName)
    {
        var option = page.Locator(".tm-dropdown-item")
            .Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(optionName)}$") });
        await option.ClickAsync();
        await page.WaitForTimeoutAsync(2000); // let layout + render settle
    }

    private async Task<List<(string Id, double X, double Y)>> GetNodeLayoutPositionsAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>("""
            () => {
                const nodes = document.querySelectorAll('.tm-diagram-node');
                const arr = [];
                nodes.forEach(n => {
                    const t = n.style.transform;
                    const m = t.match(/translate\(([-+]?[0-9]*\.?[0-9]+)px,\s*([-+]?[0-9]*\.?[0-9]+)px\)/);
                    if (m) {
                        arr.push({ id: n.getAttribute('data-node-id'), x: parseFloat(m[1]), y: parseFloat(m[2]) });
                    } else {
                        arr.push({ id: n.getAttribute('data-node-id'), x: 0, y: 0 });
                    }
                });
                return JSON.stringify(arr);
            }
        """);
        Assert.IsNotNull(json);
        using var doc = JsonDocument.Parse(json);
        var list = new List<(string Id, double X, double Y)>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            list.Add((item.GetProperty("id").GetString()!, item.GetProperty("x").GetDouble(), item.GetProperty("y").GetDouble()));
        }
        return list;
    }

    private static void AssertAllNodesFinite(List<(string Id, double X, double Y)> positions)
    {
        Assert.IsTrue(positions.Count > 0, "Expected at least one node");
        foreach (var (id, x, y) in positions)
        {
            Assert.IsTrue(double.IsFinite(x), $"Node {id} X must be finite, got {x}");
            Assert.IsTrue(double.IsFinite(y), $"Node {id} Y must be finite, got {y}");
        }
    }

    private static void AssertNodesSpreadOut(List<(string Id, double X, double Y)> positions)
    {
        var xs = positions.Select(p => p.X).Distinct().OrderBy(v => v).ToList();
        var ys = positions.Select(p => p.Y).Distinct().OrderBy(v => v).ToList();
        Assert.IsTrue(xs.Count > 1 || ys.Count > 1, "Layout should spread nodes out; expected more than one distinct X or Y coordinate");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Dagre layouts
    // ═══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DagreTbLayout_SpreadsNodesAndIsFinite()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Hierarchical Top-Down");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);
    }

    [TestMethod]
    public async Task DagreLrLayout_SpreadsNodesAndIsFinite()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Hierarchical Left-Right");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Tree layouts
    // ═══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TreeTbLayout_SpreadsNodesAndIsFinite()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Tree");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);
    }

    [TestMethod]
    public async Task TreeLrLayout_SpreadsNodesAndIsFinite()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Tree Left-Right");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Force layout
    // ═══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ForceLayout_SpreadsNodesAndIsFinite()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Force");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Circle layout
    // ═══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CircleLayout_SpreadsNodesAndIsFinite()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Circle");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Grid layout (the one reported broken in the video)
    // ═══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GridLayout_ArrangesNodesInGrid()
    {
        var page = await PrepareDiagramPageAsync();
        await SelectAllNodesAsync(page);
        await OpenLayoutDropdownAsync(page);
        await ClickLayoutOptionAsync(page, "Grid");

        var positions = await GetNodeLayoutPositionsAsync(page);
        TestContext.WriteLine($"Nodes after layout: {string.Join(", ", positions.Select(p => $"{p.Id}=({p.X:F1},{p.Y:F1})"))}");
        AssertAllNodesFinite(positions);
        AssertNodesSpreadOut(positions);

        // Grid-specific assertion: nodes should align in rows and columns
        // meaning multiple nodes share the same X (different rows) or same Y (different columns)
        var xs = positions.Select(p => Math.Round(p.X, 1)).ToList();
        var ys = positions.Select(p => Math.Round(p.Y, 1)).ToList();

        var distinctX = xs.Distinct().Count();
        var distinctY = ys.Distinct().Count();

        // For N nodes we expect roughly sqrt(N) columns and rows.
        // We just assert that there is more than one distinct X AND more than one distinct Y
        // when there are enough nodes.
        if (positions.Count >= 4)
        {
            Assert.IsTrue(distinctX > 1, $"Grid layout should produce multiple columns, got {distinctX} distinct X values");
            Assert.IsTrue(distinctY > 1, $"Grid layout should produce multiple rows, got {distinctY} distinct Y values");
        }
    }
}
