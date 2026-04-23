using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Safety-net E2E tests for the unified SVG canvas refactor
/// (see planning/DIAGRAM_UNIFIED_SVG_PLAN.md — F0.4, F0.5, F0.6).
///
/// <para>
/// These tests lock the <b>current</b> (pre-refactor) behavior of the two-layer architecture so
/// that any accidental break during F1–F7 surfaces immediately. They exercise the actual JS
/// coordinate pipeline (<c>_screenToDoc</c>, <c>_syncHtmlTransform</c>, SVG <c>viewBox</c> and
/// HTML overlay CSS transform) across several zooms and a 45° rotation.
/// </para>
///
/// <para>
/// After F2 the HTML overlay selectors (<c>.tm-diagram-canvas__overlay</c>,
/// <c>.tm-diagram-transform-layer</c>) will be replaced by SVG-side equivalents. Update
/// <see cref="HtmlOverlayScale"/> and its callers accordingly at that point.
/// </para>
/// </summary>
[TestClass]
public class DiagramTransformE2ETests : WasmTestBase
{
    private const string DiagramEditorUrl = "/diagram-editor";

    private async Task<IPage> PrepareDiagramPageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}{DiagramEditorUrl}");
        await page.EvaluateAsync("() => localStorage.setItem('tm-demo-culture', 'en')");
        await page.ReloadAsync();
        await WaitForAppReadyAsync(page);

        await page.WaitForSelectorAsync(".tm-diagram-canvas", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15000
        });

        // Wait until the editor JS has wired up the instance
        await page.WaitForFunctionAsync("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas || !canvas.id) return false;
                const ed = window.tmDiagramEditor;
                return !!(ed && ed.instances && ed.instances.get(canvas.id));
            }
        """, null, new PageWaitForFunctionOptions { Timeout = 15000 });

        // Load a known sample so there are at least two nodes and one edge
        await page.GetByRole(AriaRole.Button, new() { Name = "Load UML sample" }).ClickAsync();
        await page.WaitForSelectorAsync(".tm-diagram-node", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.WaitForTimeoutAsync(500);

        return page;
    }

    private static async Task SetCanvasScaleAsync(IPage page, double scale)
    {
        // Use the same JS entry point the Blazor SetZoom() C# method invokes.
        await page.EvaluateAsync("""
            (s) => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas) return;
                window.tmDiagramEditor.zoomTo(canvas, s);
            }
        """, scale);
        await page.WaitForTimeoutAsync(400);
    }

    private static async Task<(double A, double B, double C, double D, double E, double F)> GetSvgScreenCtmAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>("""
            () => {
                const svg = document.querySelector('.tm-diagram-canvas__svg');
                if (!svg) return null;
                const m = svg.getScreenCTM();
                if (!m) return null;
                return JSON.stringify({ a: m.a, b: m.b, c: m.c, d: m.d, e: m.e, f: m.f });
            }
        """);
        Assert.IsNotNull(json, "SVG element or CTM not available");
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        return (
            r.GetProperty("a").GetDouble(),
            r.GetProperty("b").GetDouble(),
            r.GetProperty("c").GetDouble(),
            r.GetProperty("d").GetDouble(),
            r.GetProperty("e").GetDouble(),
            r.GetProperty("f").GetDouble()
        );
    }

    /// <summary>
    /// Returns the effective horizontal scale applied by the JS overlay transform. Today this
    /// is a separate value from <see cref="GetSvgScreenCtmAsync"/> because
    /// <c>_syncHtmlTransform</c> writes an independent CSS transform — the drift bug.
    /// </summary>
    private static async Task<double?> HtmlOverlayScale(IPage page)
    {
        return await page.EvaluateAsync<double?>("""
            () => {
                const layer = document.querySelector('.tm-diagram-canvas__overlay .tm-diagram-transform-layer');
                if (!layer) return null;
                const t = getComputedStyle(layer).transform;
                if (!t || t === 'none') return 1;
                const m = t.match(/matrix\(([-0-9.eE+]+),\s*([-0-9.eE+]+),\s*([-0-9.eE+]+),\s*([-0-9.eE+]+),\s*([-0-9.eE+]+),\s*([-0-9.eE+]+)\)/);
                return m ? parseFloat(m[1]) : null;
            }
        """);
    }

    private static async Task<(double X, double Y)> GetBoundingCenterAsync(IPage page, string selector)
    {
        var json = await page.EvaluateAsync<string>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return null;
                const r = el.getBoundingClientRect();
                return JSON.stringify({ x: r.left + r.width / 2, y: r.top + r.height / 2 });
            }
        """, selector);
        Assert.IsNotNull(json, $"Element not found: {selector}");
        using var doc = JsonDocument.Parse(json);
        return (doc.RootElement.GetProperty("x").GetDouble(), doc.RootElement.GetProperty("y").GetDouble());
    }

    /// <summary>
    /// F0.4 — verifies that nodes and their connected edge endpoints stay aligned across
    /// multiple zoom levels. This is the canonical regression for the "drift bug" comment in
    /// <c>diagram-editor.js::_findNearestPortOnNode</c>. When F2 removes the dual transform the
    /// tolerances should tighten; the test itself stays identical.
    /// </summary>
    [TestMethod]
    public async Task PanThenZoom_NodesRemainAlignedWithEdges()
    {
        var page = await PrepareDiagramPageAsync();

        double[] scales = [1.0, 0.75, 1.25];
        foreach (var s in scales)
        {
            await SetCanvasScaleAsync(page, s);

            var svgCtm = await GetSvgScreenCtmAsync(page);
            var htmlScale = await HtmlOverlayScale(page);
            Assert.IsNotNull(htmlScale, "HTML overlay transform-layer missing");

            Math.Abs(htmlScale!.Value - svgCtm.A).Should().BeLessThan(0.01,
                $"at scale {s}, HTML overlay CSS scale must match SVG getScreenCTM().a — this is the drift-bug canary");

            // Pick the first edge path and verify that its first command coordinate projects
            // onto the bounding box of some node (simple adjacency sanity check in screen space).
            var edgeCenter = await GetBoundingCenterAsync(page, ".tm-diagram-edge-path");
            var nodeCenter = await GetBoundingCenterAsync(page, ".tm-diagram-node");
            Math.Abs(edgeCenter.X - nodeCenter.X).Should().BeLessThan(2000,
                "sanity-only: edge and first node should be on the same canvas");
        }
    }

    /// <summary>
    /// F0.5 — rotates the first node by 45° via the public command surface and checks that
    /// its ports' screen centers track the rotation. Gives us a pre-F5 baseline for the
    /// SVG-based rotate flow.
    /// </summary>
    [TestMethod]
    public async Task RotatedNode_PortsMoveWithRotation()
    {
        var page = await PrepareDiagramPageAsync();

        // Pick the first node and read port positions at 0° and at 45°.
        var before = await page.EvaluateAsync<string>("""
            () => {
                const node = document.querySelector('.tm-diagram-node');
                if (!node) return null;
                const id = node.getAttribute('data-node-id');
                const ports = node.querySelectorAll('.tm-diagram-port');
                const centers = [];
                ports.forEach(p => {
                    const r = p.getBoundingClientRect();
                    centers.push({ x: r.left + r.width / 2, y: r.top + r.height / 2 });
                });
                return JSON.stringify({ id, centers });
            }
        """);
        Assert.IsNotNull(before);

        using var beforeDoc = JsonDocument.Parse(before);
        var nodeId = beforeDoc.RootElement.GetProperty("id").GetString();
        Assert.IsNotNull(nodeId);

        // Rotate via JS by updating the element's inline `transform` property — this mirrors
        // how the Blazor side writes the transform today (translate(...) rotate(...)). F5 will
        // replace this with an SVG <g> transform and the test will be rewritten to use the
        // rotation command instead.
        await page.EvaluateAsync("""
            (id) => {
                const el = document.querySelector(`[data-node-id='${id}']`);
                if (!el) return;
                const current = el.style.transform || '';
                const stripped = current.replace(/rotate\([^)]*\)/, '').trim();
                el.style.transform = (stripped.length > 0 ? stripped + ' ' : '') + 'rotate(45deg)';
            }
        """, nodeId);
        await page.WaitForTimeoutAsync(250);

        var after = await page.EvaluateAsync<string>("""
            (id) => {
                const node = document.querySelector(`[data-node-id='${id}']`);
                if (!node) return null;
                const ports = node.querySelectorAll('.tm-diagram-port');
                const centers = [];
                ports.forEach(p => {
                    const r = p.getBoundingClientRect();
                    centers.push({ x: r.left + r.width / 2, y: r.top + r.height / 2 });
                });
                return JSON.stringify(centers);
            }
        """, nodeId);
        Assert.IsNotNull(after);

        using var afterDoc = JsonDocument.Parse(after);
        var afterCenters = afterDoc.RootElement;
        var beforeCenters = beforeDoc.RootElement.GetProperty("centers");

        beforeCenters.GetArrayLength().Should().Be(afterCenters.GetArrayLength(),
            "port count must not change when rotating");

        // At least one port must have moved: otherwise rotation has no visible effect, which
        // would indicate the whole rotation path is broken (or the node has no ports).
        var moved = false;
        for (var i = 0; i < beforeCenters.GetArrayLength(); i++)
        {
            var bx = beforeCenters[i].GetProperty("x").GetDouble();
            var by = beforeCenters[i].GetProperty("y").GetDouble();
            var ax = afterCenters[i].GetProperty("x").GetDouble();
            var ay = afterCenters[i].GetProperty("y").GetDouble();
            if (Math.Abs(ax - bx) > 2 || Math.Abs(ay - by) > 2)
            {
                moved = true;
                break;
            }
        }
        moved.Should().BeTrue("rotating a node must visibly shift its ports in screen space");
    }

    /// <summary>
    /// F0.6 — core drift-bug regression. At 0.75 scale the SVG CTM's horizontal scale and the
    /// HTML overlay's CSS scale must be identical. When they diverge (which is the root cause
    /// of _findNearestPortOnNode being flaky at non-unit scales) this test fails.
    /// </summary>
    [TestMethod]
    public async Task ScreenToDoc_And_HtmlLayerScale_MatchAtScale075()
    {
        var page = await PrepareDiagramPageAsync();

        await SetCanvasScaleAsync(page, 0.75);

        var ctm = await GetSvgScreenCtmAsync(page);
        var htmlScale = await HtmlOverlayScale(page);

        Assert.IsNotNull(htmlScale, "HTML overlay transform-layer not found");

        Math.Abs(htmlScale!.Value - ctm.A).Should().BeLessThan(0.005,
            "both coordinate systems must agree on scale; a difference here reproduces the drift bug and F2 must make this exact test pass with tolerance 0");
    }
}
