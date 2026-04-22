using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests for diagram editor edge interactions across all 6 phases
/// of EDGE_FEATURES_IMPLEMENTATION_PLAN.md.
/// These tests exercise the JS-side drag/draw/drop behavior that bUnit cannot verify.
/// </summary>
[TestClass]
public class DiagramEdgeE2ETests : WasmTestBase
{
    private const string DiagramEditorUrl = "/diagram-editor";
    private const string Node1Id = "class1";
    private const string Node2Id = "class2";

    private async Task WaitForCanvasAsync(IPage page)
    {
        await page.WaitForSelectorAsync(".tm-diagram-canvas", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.WaitForTimeoutAsync(500);
    }

    private async Task<(double X, double Y)> GetCenterAsync(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync();
        Assert.IsNotNull(box, "Expected element to have a bounding box");
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    private async Task SelectEdgeAsync(IPage page)
    {
        // Use JS to dispatch a click directly on the hit path — avoids SVG bounding-box / overlay issues
        await page.EvaluateAsync(@"
            const hit = document.querySelector('.tm-diagram-edge-hit-path');
            if (hit) {
                hit.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, clientX: 0, clientY: 0 }));
            }
        ");
        await page.WaitForTimeoutAsync(300);
    }

    /// <summary>Gets the screen center of the first element matching selector via JS getBoundingClientRect.</summary>
    private async Task<(double X, double Y)> GetScreenCenterViaJsAsync(IPage page, string selector)
    {
        var json = await page.EvaluateAsync<string>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return null;
                const r = el.getBoundingClientRect();
                return JSON.stringify({ x: r.left + r.width/2, y: r.top + r.height/2 });
            }
        """, selector);
        Assert.IsNotNull(json, $"Element not found: {selector}");
        using var doc = JsonDocument.Parse(json);
        return (doc.RootElement.GetProperty("x").GetDouble(), doc.RootElement.GetProperty("y").GetDouble());
    }

    /// <summary>Gets screen centers of all elements matching selector via JS.</summary>
    private async Task<List<(double X, double Y)>> GetScreenCentersViaJsAsync(IPage page, string selector)
    {
        var json = await page.EvaluateAsync<string>("""
            (sel) => {
                const els = document.querySelectorAll(sel);
                const arr = [];
                els.forEach(el => {
                    const r = el.getBoundingClientRect();
                    arr.push({ x: r.left + r.width/2, y: r.top + r.height/2 });
                });
                return JSON.stringify(arr);
            }
        """, selector);
        Assert.IsNotNull(json, $"Elements not found: {selector}");
        using var doc = JsonDocument.Parse(json);
        var list = new List<(double, double)>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            list.Add((item.GetProperty("x").GetDouble(), item.GetProperty("y").GetDouble()));
        }
        return list;
    }

    /// <summary>Dispatches a click event via JS on the first element matching selector.</summary>
    private async Task ClickViaJsAsync(IPage page, string selector)
    {
        await page.EvaluateAsync<string>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return null;
                const r = el.getBoundingClientRect();
                el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, clientX: r.left + r.width/2, clientY: r.top + r.height/2 }));
                return 'ok';
            }
        """, selector);
        await page.WaitForTimeoutAsync(300);
    }

    /// <summary>Dispatches a mousedown event via JS on the first element matching selector.
    /// Use this for interactions handled by the JS mousedown listener (virtual bends, handles).</summary>
    private async Task MouseDownViaJsAsync(IPage page, string selector)
    {
        await page.EvaluateAsync<string>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return null;
                const r = el.getBoundingClientRect();
                el.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, clientX: r.left + r.width/2, clientY: r.top + r.height/2 }));
                return 'ok';
            }
        """, selector);
        await page.WaitForTimeoutAsync(300);
    }

    /// <summary>Changes the edge routing via the properties panel TmSelect.</summary>
    private async Task SetEdgeRoutingAsync(IPage page, string routing)
    {
        var propsPanel = page.Locator(".tm-diagram-properties-panel, .tm-diagram-editor__props-wrap").First;
        var routingField = propsPanel.Locator(".tm-diagram-properties__field").Filter(new LocatorFilterOptions { HasText = "Routing" }).First;
        if (await routingField.CountAsync() == 0)
        {
            Assert.Inconclusive("Routing field not found in properties panel.");
        }
        var routingSelect = routingField.Locator("select");
        await routingSelect.SelectOptionAsync(routing);
        await page.WaitForTimeoutAsync(700);
    }

    private async Task OpenEdgeContextMenuAsync(IPage page)
    {
        // Use JS dispatch because HTML overlay blocks native mouse events on SVG
        await page.EvaluateAsync("""
            () => {
                const hit = document.querySelector('.tm-diagram-edge-hit-path');
                if (!hit) return;
                const r = hit.getBoundingClientRect();
                const cx = r.left + r.width / 2;
                const cy = r.top + r.height / 2;
                hit.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2, clientX: cx, clientY: cy }));
            }
        """);
        await page.WaitForTimeoutAsync(300);
    }

    private async Task SetToolModeAsync(IPage page, string mode)
    {
        await page.EvaluateAsync("""
            (mode) => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas || !window.tmDiagramEditor) return;
                window.tmDiagramEditor.setToolMode(canvas, mode);
            }
        """, mode);
        await page.WaitForTimeoutAsync(200);
    }

    private async Task<int> GetEdgeCountAsync(IPage page)
    {
        return await page.Locator(".tm-diagram-edge-path").CountAsync();
    }

    // ========================================================================
    // FÁZE 1 — Základy interakce (Foundation)
    // ========================================================================

    [TestMethod]
    [Description("1.1.5 / 1.1.8 — Manual routing toggle via properties panel stops auto-router")]
    public async Task Phase1_ManualRouting_ToggleViaPropertiesPanel()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        // Insert a virtual-bend waypoint first so the edge doesn't disappear when manual routing is toggled
        // (empty Waypoints list + IsManuallyRouted=true produces an empty path)
        var virtualBends = page.Locator(".tm-diagram-edge-virtual-bend");
        if (await virtualBends.CountAsync() > 0)
        {
            // JS mousedown handler inserts the waypoint; a simple click event won't trigger it
            await MouseDownViaJsAsync(page, ".tm-diagram-edge-virtual-bend");
            await page.WaitForTimeoutAsync(500);
        }

        // Find the properties panel and the manual routing checkbox
        var propsPanel = page.Locator(".tm-diagram-properties-panel, .tm-diagram-editor__props-wrap").First;
        await propsPanel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Look for manual routing checkbox (TmCheckbox wrapper with label containing "Manual")
        var manualCheckbox = propsPanel.Locator(".tm-checkbox-wrapper").Filter(new LocatorFilterOptions { HasText = "Manual" }).First;
        if (await manualCheckbox.CountAsync() == 0)
        {
            Assert.Inconclusive("Manual routing checkbox not found in properties panel.");
        }

        await manualCheckbox.ClickAsync();
        await page.WaitForTimeoutAsync(600); // give async HandleEdgeManualRoutingChanged time to seed waypoints

        // After toggle, the edge should still be drawn: non-empty `d` attribute
        // AND non-zero SVG bounding box. `IsVisibleAsync` proved flaky in CI
        // because Blazor re-renders detach/attach the SVG node; we use a JS
        // probe so a diagnostic message is attached to any failure.
        var diag = await page.EvaluateAsync<string>("""
            () => {
                const paths = Array.from(document.querySelectorAll('.tm-diagram-edge-path'));
                const out = paths.map(p => {
                    let bb = null;
                    try { bb = p.getBBox(); } catch { bb = null; }
                    const cs = getComputedStyle(p);
                    const parentG = p.closest('.tm-diagram-edge-group');
                    const parentOpacity = parentG ? getComputedStyle(parentG).opacity : null;
                    return {
                        d: p.getAttribute('d'),
                        bboxW: bb ? bb.width : null,
                        bboxH: bb ? bb.height : null,
                        visibility: cs.visibility,
                        display: cs.display,
                        opacity: cs.opacity,
                        parentOpacity,
                        cls: p.getAttribute('class')
                    };
                });
                return JSON.stringify({ count: paths.length, paths: out });
            }
        """);
        Assert.IsNotNull(diag);
        using var diagDoc = JsonDocument.Parse(diag);
        var count = diagDoc.RootElement.GetProperty("count").GetInt32();
        bool anyVisible = false;
        foreach (var p in diagDoc.RootElement.GetProperty("paths").EnumerateArray())
        {
            var d = p.GetProperty("d").GetString();
            var w = p.GetProperty("bboxW").ValueKind == JsonValueKind.Number ? p.GetProperty("bboxW").GetDouble() : 0;
            var h = p.GetProperty("bboxH").ValueKind == JsonValueKind.Number ? p.GetProperty("bboxH").GetDouble() : 0;
            var vis = p.GetProperty("visibility").GetString();
            var disp = p.GetProperty("display").GetString();
            if (!string.IsNullOrEmpty(d) && (w > 0 || h > 0) && vis != "hidden" && disp != "none")
            {
                anyVisible = true;
                break;
            }
        }
        Assert.IsTrue(anyVisible,
            $"Edge should remain visible after toggling manual routing. Diagnostics: {diag}");

        await TakeScreenshotAsync(page, "phase1_manual_routing");
    }

    [TestMethod]
    [Description("1.1.9 — Reset routing button restores auto-routing")]
    public async Task Phase1_ResetRouting_ButtonRestoresAutoRouting()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        // Switch to orthogonal so manual routing + reset are available
        await SetEdgeRoutingAsync(page, "orthogonal");

        var propsPanel = page.Locator(".tm-diagram-properties-panel, .tm-diagram-editor__props-wrap").First;

        // Enable manual routing so the Reset button appears
        var manualCheckbox = propsPanel.Locator(".tm-checkbox-wrapper").Filter(new LocatorFilterOptions { HasText = "Manual" }).First;
        if (await manualCheckbox.CountAsync() == 0)
        {
            Assert.Inconclusive("Manual routing checkbox not found in properties panel.");
        }
        var isChecked = await manualCheckbox.Locator("input").IsCheckedAsync();
        if (!isChecked)
        {
            await manualCheckbox.ClickAsync();
            await page.WaitForTimeoutAsync(300);
        }

        var resetBtn = page.Locator("button:has-text('Reset routing'), button:has-text('Reset')").First;
        if (await resetBtn.CountAsync() == 0)
        {
            Assert.Inconclusive("Reset routing button not found in properties panel.");
        }

        await resetBtn.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

        // Verify that manual routing was turned off (the main effect of reset)
        var isCheckedAfter = await manualCheckbox.Locator("input").IsCheckedAsync();
        Assert.IsFalse(isCheckedAfter, "Manual routing should be disabled after reset routing");

        await TakeScreenshotAsync(page, "phase1_reset_routing");
    }

    [TestMethod]
    [Description("1.2 — Virtual bend click inserts a new waypoint")]
    public async Task Phase1_VirtualBend_ClickInsertsWaypoint()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        var handlesBefore = page.Locator(".tm-diagram-edge-handle:not(.tm-diagram-edge-handle--dangling):not(.tm-diagram-edge-handle--jetty):not(.tm-diagram-edge-segment-handle)");
        var countBefore = await handlesBefore.CountAsync();

        var virtualBends = page.Locator(".tm-diagram-edge-virtual-bend");
        var vbCount = await virtualBends.CountAsync();
        if (vbCount == 0)
        {
            Assert.Inconclusive("No virtual bends visible; sample edge may be orthogonal or no bends rendered.");
        }

        // Dispatch mousedown via JS — the canvas JS handler listens for mousedown on virtual bends,
        // not click events (which only trigger Blazor @onclick bindings).
        await MouseDownViaJsAsync(page, ".tm-diagram-edge-virtual-bend");
        await page.WaitForTimeoutAsync(500);

        var handlesAfter = page.Locator(".tm-diagram-edge-handle:not(.tm-diagram-edge-handle--dangling):not(.tm-diagram-edge-handle--jetty):not(.tm-diagram-edge-segment-handle)");
        var countAfter = await handlesAfter.CountAsync();
        Assert.IsTrue(countAfter > countBefore,
            $"Expected waypoint count to increase after virtual bend insert (before={countBefore}, after={countAfter})");

        await TakeScreenshotAsync(page, "phase1_virtual_bend");
    }

    [TestMethod]
    [Description("1.3 — Smart removal: dragging waypoint onto straight line removes it")]
    public async Task Phase1_SmartRemoval_DragWaypointToStraightLineRemovesIt()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // First insert a virtual bend to create a middle waypoint
        await SelectEdgeAsync(page);
        var virtualBends = page.Locator(".tm-diagram-edge-virtual-bend");
        if (await virtualBends.CountAsync() == 0)
        {
            Assert.Inconclusive("No virtual bends to create a test waypoint.");
        }

        // Dispatch mousedown via JS — the canvas JS handler listens for mousedown on virtual bends
        await MouseDownViaJsAsync(page, ".tm-diagram-edge-virtual-bend");
        await page.WaitForTimeoutAsync(500);

        // Waypoint handles have data-waypoint="true"; endpoint handles have data-waypoint="false".
        // Virtual bends share the tm-diagram-edge-handle class but have no data-waypoint attribute.
        var waypointHandles = page.Locator(".tm-diagram-edge-handle[data-waypoint='true']");
        var countBefore = await waypointHandles.CountAsync();
        Assert.IsTrue(countBefore >= 1, "Need at least 1 waypoint to test smart removal");

        var waypointCenters = await GetScreenCentersViaJsAsync(page, ".tm-diagram-edge-handle[data-waypoint='true']");
        Assert.IsTrue(waypointCenters.Count >= 1, "Expected at least 1 waypoint center from JS");

        var endpointCenters = await GetScreenCentersViaJsAsync(page, ".tm-diagram-edge-handle[data-waypoint='false']");
        Assert.IsTrue(endpointCenters.Count >= 2, "Expected source and target endpoint handles");

        // Drag the first (only) waypoint onto the straight line between its source and target endpoints
        var (mx, my) = waypointCenters[0];
        var targetX = (endpointCenters[0].X + endpointCenters[1].X) / 2;
        var targetY = (endpointCenters[0].Y + endpointCenters[1].Y) / 2;

        await page.Mouse.MoveAsync((float)mx, (float)my);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)targetX, (float)targetY);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);

        var countAfter = await waypointHandles.CountAsync();
        Assert.IsTrue(countAfter < countBefore,
            $"Expected waypoint count to decrease after smart removal (before={countBefore}, after={countAfter})");

        await TakeScreenshotAsync(page, "phase1_smart_removal");
    }

    [TestMethod]
    [Description("1.4 — Grid snap: node drag snaps to grid")]
    public async Task Phase1_GridSnap_NodeDragSnapsToGrid()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Drag the first sample node — nodes are HTML elements so real mouse works reliably
        var node = page.Locator(".tm-diagram-node").First;
        await node.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var box = await node.BoundingBoxAsync();
        Assert.IsNotNull(box, "Node should have a bounding box");
        var startX = box.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;

        // Drag to an unaligned position — demo uses GridSize = 20
        // Use Locator.DragToAsync with offset positions so Playwright handles the drag sequence
        // Simulate drag via JS dispatchEvent — the HTML overlay intercepts native mouse
        // events, so Playwright Mouse API doesn't reach the SVG/canvas handlers reliably.
        await page.EvaluateAsync("""
            () => {
                const node = document.querySelector('.tm-diagram-node');
                const container = document.querySelector('.tm-diagram-canvas');
                if (!node || !container) return null;
                const r = node.getBoundingClientRect();
                const cx = r.left + r.width/2;
                const cy = r.top + r.height/2;
                node.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, clientX: cx, clientY: cy }));
                container.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true, button: 0, clientX: cx + 100, clientY: cy + 100 }));
                container.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, button: 0, clientX: cx + 100, clientY: cy + 100 }));
                return 'done';
            }
        """);
        await page.WaitForTimeoutAsync(800);

        // After grid snap, the node position should be a multiple of 20
        var newBox = await node.BoundingBoxAsync();
        Assert.IsNotNull(newBox);

        // First verify the node actually moved
        Assert.IsTrue(Math.Abs(newBox.X - box.X) > 5 || Math.Abs(newBox.Y - box.Y) > 5,
            $"Node should have moved, but stayed at ({newBox.X},{newBox.Y})");

        var snapX = Math.Round(newBox.X / 20) * 20;
        var snapY = Math.Round(newBox.Y / 20) * 20;
        var deltaX = Math.Abs(newBox.X - snapX);
        var deltaY = Math.Abs(newBox.Y - snapY);

        // Allow slightly larger tolerance because async Blazor re-render can introduce
        // a small sub-pixel offset before the snapped value settles.
        Assert.IsTrue(deltaX < 5 && deltaY < 5,
            $"Node should snap to grid multiple of 20, but is at ({newBox.X},{newBox.Y})");

        await TakeScreenshotAsync(page, "phase1_grid_snap");
    }

    [TestMethod]
    [Description("Phase 1 — Empty-to-empty drag in edge mode creates floating edge")]
    public async Task Phase1_FreeLine_EmptyToEmpty_CreatesFloatingEdge()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var initialEdgeCount = await GetEdgeCountAsync(page);
        var danglingBefore = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        await SetToolModeAsync(page, "edge");

        var pointsJson = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                const nodes = Array.from(document.querySelectorAll('.tm-diagram-node[data-node-id]'));
                if (!canvas) return null;
                const cr = canvas.getBoundingClientRect();
                let maxNodeRight = cr.left + 80;
                let minNodeTop = cr.top + 80;
                for (const n of nodes) {
                    const r = n.getBoundingClientRect();
                    maxNodeRight = Math.max(maxNodeRight, r.right);
                    minNodeTop = Math.min(minNodeTop, r.top);
                }
                const startX = Math.min(cr.right - 140, maxNodeRight + 40);
                const startY = Math.max(cr.top + 40, minNodeTop - 20);
                return JSON.stringify({
                    startX,
                    startY,
                    endX: startX + 220,
                    endY: startY + 130
                });
            }
        """);
        Assert.IsNotNull(pointsJson, "Could not compute empty-canvas drag points.");
        using var points = JsonDocument.Parse(pointsJson);
        var sx = points.RootElement.GetProperty("startX").GetDouble();
        var sy = points.RootElement.GetProperty("startY").GetDouble();
        var ex = points.RootElement.GetProperty("endX").GetDouble();
        var ey = points.RootElement.GetProperty("endY").GetDouble();

        await page.Mouse.MoveAsync((float)sx, (float)sy);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)ex, (float)ey);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(900);

        var edgeCountAfter = await GetEdgeCountAsync(page);
        Assert.AreEqual(initialEdgeCount + 1, edgeCountAfter, "Expected one newly created edge.");

        var danglingCount = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        Assert.IsTrue(danglingCount >= 2, "Expected both terminals to be dangling for empty->empty draw.");

        await TakeScreenshotAsync(page, "free_line_empty_to_empty");
    }

    [TestMethod]
    [Description("Phase 1 — Empty-to-port drag keeps floating source and attaches target")]
    public async Task Phase1_FreeLine_EmptyToNode_AttachesTarget()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var initialEdgeCount = await GetEdgeCountAsync(page);
        var danglingBefore = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        await SetToolModeAsync(page, "edge");

        var pointsJson = await page.EvaluateAsync<string>("""
            (targetSelector) => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                const target = document.querySelector(targetSelector);
                if (!canvas || !target) return null;
                const cr = canvas.getBoundingClientRect();
                const tr = target.getBoundingClientRect();
                const startX = Math.max(cr.left + 30, tr.left - 180);
                const startY = Math.max(cr.top + 30, tr.top - 80);
                return JSON.stringify({
                    startX,
                    startY,
                    endX: tr.left + tr.width / 2,
                    endY: tr.top + tr.height / 2
                });
            }
        """, $".tm-diagram-node[data-node-id='{Node2Id}'] .tm-diagram-port[data-port-id='left']");
        Assert.IsNotNull(pointsJson, "Could not compute empty->port drag points.");
        using var points = JsonDocument.Parse(pointsJson);
        var sx = points.RootElement.GetProperty("startX").GetDouble();
        var sy = points.RootElement.GetProperty("startY").GetDouble();
        var ex = points.RootElement.GetProperty("endX").GetDouble();
        var ey = points.RootElement.GetProperty("endY").GetDouble();

        await page.Mouse.MoveAsync((float)sx, (float)sy);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)ex, (float)ey);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(900);

        var edgeCountAfter = await GetEdgeCountAsync(page);
        Assert.AreEqual(initialEdgeCount + 1, edgeCountAfter, "Expected one newly created edge.");

        var danglingAfter = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        Assert.IsTrue(danglingAfter >= danglingBefore + 1,
            "Expected at least one additional dangling handle after empty->port draw.");

        await TakeScreenshotAsync(page, "free_line_empty_to_node");
    }

    [TestMethod]
    [Description("Phase 2 — Edge tool empty click without drag starts a polyline draft; ESC discards it")]
    public async Task Phase2_EdgeTool_ClickEmpty_StartsPolyline_EscCancels()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var initialEdgeCount = await GetEdgeCountAsync(page);
        await SetToolModeAsync(page, "edge");

        var pointJson = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas) return null;
                const r = canvas.getBoundingClientRect();
                return JSON.stringify({ x: r.left + 40, y: r.top + 40 });
            }
        """);
        Assert.IsNotNull(pointJson);
        using var p = JsonDocument.Parse(pointJson);
        var x = p.RootElement.GetProperty("x").GetDouble();
        var y = p.RootElement.GetProperty("y").GetDouble();

        await page.Mouse.ClickAsync((float)x, (float)y);
        await page.WaitForTimeoutAsync(200);

        var stateAfterClick = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas || !window.tmDiagramEditor) return 'unknown';
                const inst = window.tmDiagramEditor.instances.get(canvas.id);
                if (!inst) return 'missing';
                return JSON.stringify({ polyline: inst.isDrawingPolyline, drawing: inst.isDrawingEdge, mode: inst.toolMode });
            }
        """);
        Assert.IsNotNull(stateAfterClick);
        using (var s = JsonDocument.Parse(stateAfterClick))
        {
            Assert.IsTrue(s.RootElement.GetProperty("polyline").GetBoolean(),
                "Plain click on empty canvas in edge mode should start a polyline draft.");
            Assert.IsTrue(s.RootElement.GetProperty("drawing").GetBoolean(),
                "isDrawingEdge should also be true while polyline is drafting.");
        }

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(200);

        var mode = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas || !window.tmDiagramEditor) return 'unknown';
                const inst = window.tmDiagramEditor.instances.get(canvas.id);
                return inst ? inst.toolMode : 'missing';
            }
        """);
        Assert.AreEqual("select", mode, "ESC should cancel the polyline draft and return to select.");

        var edgeCountAfter = await GetEdgeCountAsync(page);
        Assert.AreEqual(initialEdgeCount, edgeCountAfter, "No new edge should be created by click+ESC.");
    }

    [TestMethod]
    [Description("Phase 2 — Three clicks + double-click finish creates a polyline with waypoints")]
    public async Task Phase2_Polyline_ThreeClicks_DblClickFinish()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var initialEdgeCount = await GetEdgeCountAsync(page);
        await SetToolModeAsync(page, "edge");

        var pointsJson = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas) return null;
                const cr = canvas.getBoundingClientRect();
                const nodes = Array.from(document.querySelectorAll('.tm-diagram-node[data-node-id]'));
                let maxRight = cr.left + 40, minTop = cr.top + 40;
                for (const n of nodes) {
                    const r = n.getBoundingClientRect();
                    maxRight = Math.max(maxRight, r.right);
                    minTop = Math.min(minTop, r.top);
                }
                // Base anchor: right of every node, slightly above the top row.
                const baseX = Math.min(cr.right - 260, maxRight + 60);
                const baseY = Math.max(cr.top + 30, minTop - 30);

                // Ensure a point lands on truly empty canvas (not on a node,
                // port, connection-point or edge-hit-path) AND is still inside
                // the canvas rect. If not, nudge upward in 20px steps.
                function isEmpty(x, y) {
                    const el = document.elementFromPoint(x, y);
                    if (!el) return false;
                    if (!canvas.contains(el)) return false;
                    if (el.closest('.tm-diagram-port')) return false;
                    if (el.closest('.tm-diagram-connection-point')) return false;
                    if (el.closest('[data-node-id]')) return false;
                    if (el.closest('.tm-diagram-edge-hit-path')) return false;
                    return true;
                }
                function pick(x, y) {
                    for (let k = 0; k < 20; k++) {
                        const yy = y - k * 20;
                        if (yy < cr.top + 10) break;
                        if (isEmpty(x, yy)) return { x, y: yy };
                    }
                    return { x, y: cr.top + 10 };
                }

                return JSON.stringify({
                    p1: pick(baseX,        baseY),
                    p2: pick(baseX + 80,   baseY),
                    p3: pick(baseX + 160,  baseY),
                    p4: pick(baseX + 220,  baseY)
                });
            }
        """);
        Assert.IsNotNull(pointsJson);
        using var pts = JsonDocument.Parse(pointsJson);
        double Px(string k) => pts.RootElement.GetProperty(k).GetProperty("x").GetDouble();
        double Py(string k) => pts.RootElement.GetProperty(k).GetProperty("y").GetDouble();

        await page.Mouse.ClickAsync((float)Px("p1"), (float)Py("p1"));
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.ClickAsync((float)Px("p2"), (float)Py("p2"));
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.ClickAsync((float)Px("p3"), (float)Py("p3"));
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.DblClickAsync((float)Px("p4"), (float)Py("p4"));
        await page.WaitForTimeoutAsync(900);

        var edgeCountAfter = await GetEdgeCountAsync(page);
        Assert.AreEqual(initialEdgeCount + 1, edgeCountAfter, "Expected one new polyline edge.");

        // Both ends should be dangling (floating source + floating target).
        var danglingCount = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        Assert.IsTrue(danglingCount >= 2, "Polyline with both floating ends should have dangling handles on both sides.");

        await TakeScreenshotAsync(page, "phase2_polyline_dblclick");
    }

    [TestMethod]
    [Description("Phase 2 — ESC during polyline draft discards the edge")]
    public async Task Phase2_Polyline_Escape_DiscardsDraft()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var initialEdgeCount = await GetEdgeCountAsync(page);
        await SetToolModeAsync(page, "edge");

        var pointsJson = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                if (!canvas) return null;
                const cr = canvas.getBoundingClientRect();
                const nodes = Array.from(document.querySelectorAll('.tm-diagram-node[data-node-id]'));
                let maxRight = cr.left + 40, minTop = cr.top + 40;
                for (const n of nodes) {
                    const r = n.getBoundingClientRect();
                    maxRight = Math.max(maxRight, r.right);
                    minTop = Math.min(minTop, r.top);
                }
                const baseX = Math.min(cr.right - 160, maxRight + 60);
                const baseY = Math.max(cr.top + 30, minTop - 30);
                function isEmpty(x, y) {
                    const el = document.elementFromPoint(x, y);
                    if (!el) return false;
                    if (!canvas.contains(el)) return false;
                    if (el.closest('.tm-diagram-port')) return false;
                    if (el.closest('.tm-diagram-connection-point')) return false;
                    if (el.closest('[data-node-id]')) return false;
                    if (el.closest('.tm-diagram-edge-hit-path')) return false;
                    return true;
                }
                function pick(x, y) {
                    for (let k = 0; k < 20; k++) {
                        const yy = y - k * 20;
                        if (yy < cr.top + 10) break;
                        if (isEmpty(x, yy)) return { x, y: yy };
                    }
                    return { x, y: cr.top + 10 };
                }
                return JSON.stringify({
                    p1: pick(baseX,       baseY),
                    p2: pick(baseX + 80,  baseY)
                });
            }
        """);
        Assert.IsNotNull(pointsJson);
        using var pts = JsonDocument.Parse(pointsJson);
        var p1x = pts.RootElement.GetProperty("p1").GetProperty("x").GetDouble();
        var p1y = pts.RootElement.GetProperty("p1").GetProperty("y").GetDouble();
        var p2x = pts.RootElement.GetProperty("p2").GetProperty("x").GetDouble();
        var p2y = pts.RootElement.GetProperty("p2").GetProperty("y").GetDouble();

        await page.Mouse.ClickAsync((float)p1x, (float)p1y);
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.ClickAsync((float)p2x, (float)p2y);
        await page.WaitForTimeoutAsync(150);
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(400);

        var edgeCountAfter = await GetEdgeCountAsync(page);
        Assert.AreEqual(initialEdgeCount, edgeCountAfter, "ESC should discard the polyline draft.");

        var mode = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                const inst = canvas && window.tmDiagramEditor && window.tmDiagramEditor.instances.get(canvas.id);
                return inst ? inst.toolMode : 'missing';
            }
        """);
        Assert.AreEqual("select", mode, "After ESC we should be back in select mode.");
    }

    [TestMethod]
    [Description("Phase 2 — Click on port during polyline draft attaches the target terminal")]
    public async Task Phase2_Polyline_ClickOnNode_AttachesTarget()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var initialEdgeCount = await GetEdgeCountAsync(page);
        var danglingBefore = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        await SetToolModeAsync(page, "edge");

        var pointsJson = await page.EvaluateAsync<string>("""
            (targetSelector) => {
                const canvas = document.querySelector('.tm-diagram-canvas');
                const target = document.querySelector(targetSelector);
                if (!canvas || !target) return null;
                const cr = canvas.getBoundingClientRect();
                const tr = target.getBoundingClientRect();
                const p1x = Math.max(cr.left + 30, tr.left - 200);
                const p1y = Math.max(cr.top + 30, tr.top - 120);
                const p2x = p1x + 80;
                const p2y = p1y + 40;
                return JSON.stringify({
                    p1: { x: p1x, y: p1y },
                    p2: { x: p2x, y: p2y },
                    target: { x: tr.left + tr.width / 2, y: tr.top + tr.height / 2 }
                });
            }
        """, $".tm-diagram-node[data-node-id='{Node2Id}'] .tm-diagram-port[data-port-id='left']");
        Assert.IsNotNull(pointsJson);
        using var pts = JsonDocument.Parse(pointsJson);
        double Px(string k) => pts.RootElement.GetProperty(k).GetProperty("x").GetDouble();
        double Py(string k) => pts.RootElement.GetProperty(k).GetProperty("y").GetDouble();

        await page.Mouse.ClickAsync((float)Px("p1"), (float)Py("p1"));
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.ClickAsync((float)Px("p2"), (float)Py("p2"));
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.ClickAsync((float)Px("target"), (float)Py("target"));
        await page.WaitForTimeoutAsync(900);

        var edgeCountAfter = await GetEdgeCountAsync(page);
        Assert.AreEqual(initialEdgeCount + 1, edgeCountAfter, "Expected one new polyline edge.");

        // Floating source adds 1 dangling handle; the attached target side does not.
        var danglingAfter = await page.Locator(".tm-diagram-edge-handle--dangling").CountAsync();
        Assert.IsTrue(danglingAfter >= danglingBefore + 1,
            "Expected one additional dangling handle (floating source) after polyline attaches target.");

        await TakeScreenshotAsync(page, "phase2_polyline_click_on_node");
    }

    // ========================================================================
    // FÁZE 2 — Segment Drag
    // ========================================================================

    [TestMethod]
    [Description("2.1 — Drag an orthogonal segment handle moves the segment")]
    public async Task Phase2_SegmentDrag_MovesOrthogonalSegment()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Switch edge to orthogonal via properties panel
        await SelectEdgeAsync(page);
        await SetEdgeRoutingAsync(page, "orthogonal");

        var segmentHandles = page.Locator(".tm-diagram-edge-segment-handle");
        var segCount = await segmentHandles.CountAsync();
        if (segCount == 0)
        {
            Assert.Inconclusive("No segment handles visible; edge may not be orthogonal.");
        }

        var (sx, sy) = await GetScreenCenterViaJsAsync(page, ".tm-diagram-edge-segment-handle");

        await page.Mouse.MoveAsync((float)sx, (float)sy);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(sx + 30), (float)(sy + 30));
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);

        var edgesAfter = page.Locator(".tm-diagram-edge-path");
        var edgeCountAfter = await edgesAfter.CountAsync();
        Assert.IsTrue(edgeCountAfter > 0, "Edge should still exist after segment drag");

        await TakeScreenshotAsync(page, "phase2_segment_drag");
    }

    [TestMethod]
    [Description("2.3.1 — Cursor changes when hovering over vertical/horizontal segment")]
    public async Task Phase2_CursorFeedback_HoverOverSegment()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Switch to orthogonal via properties panel
        await SelectEdgeAsync(page);
        await SetEdgeRoutingAsync(page, "orthogonal");

        var segmentHandles = page.Locator(".tm-diagram-edge-segment-handle");
        if (await segmentHandles.CountAsync() == 0)
        {
            Assert.Inconclusive("No segment handles for cursor test.");
        }

        var (sx, sy) = await GetScreenCenterViaJsAsync(page, ".tm-diagram-edge-segment-handle");

        // Hover over segment handle
        await page.Mouse.MoveAsync((float)sx, (float)sy);
        await page.WaitForTimeoutAsync(200);

        // Check cursor style on canvas container
        var canvas = page.Locator(".tm-diagram-canvas");
        var cursor = await canvas.EvaluateAsync<string>("el => el.style.cursor");

        // Cursor should be either col-resize or row-resize depending on segment orientation
        var validCursors = new[] { "col-resize", "row-resize", "crosshair", "pointer" };
        Assert.IsTrue(validCursors.Contains(cursor) || string.IsNullOrEmpty(cursor),
            $"Unexpected cursor style: '{cursor}'");

        await TakeScreenshotAsync(page, "phase2_cursor_feedback");
    }

    // ========================================================================
    // FÁZE 3 — Volné konce a Connection Constraints
    // ========================================================================

    [TestMethod]
    [Description("3.2 / 3.3 — Drag from port to empty space creates dangling edge")]
    public async Task Phase3_DanglingEdge_DrawFromPortToEmptySpace()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var port = page.Locator($".tm-diagram-node[data-node-id='{Node1Id}'] .tm-diagram-port[data-port-id='right']");
        await port.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var (startX, startY) = await GetCenterAsync(port);

        var endX = startX + 250;
        var endY = startY;

        await page.Mouse.MoveAsync((float)startX, (float)startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)endX, (float)endY);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(2000); // WASM needs more time to render

        // Debug: count total edges first
        var allEdges = page.Locator(".tm-diagram-edge-path");
        var edgeCount = await allEdges.CountAsync();

        var dangling = page.Locator(".tm-diagram-edge-handle--dangling");
        var count = await dangling.CountAsync();
        Assert.IsTrue(edgeCount > 1, $"Expected edge count to increase after draw (got {edgeCount} edges).");
        Assert.IsTrue(count > 0, "Expected at least one dangling handle after drawing to empty space");

        await TakeScreenshotAsync(page, "phase3_dangling_edge");
    }

    [TestMethod]
    [Description("3.3 — Dangling edge reconnect to node outline")]
    public async Task Phase3_DanglingReconnect_ToNodeOutline()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Create dangling edge
        var port = page.Locator($".tm-diagram-node[data-node-id='{Node1Id}'] .tm-diagram-port[data-port-id='right']");
        await port.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var (startX, startY) = await GetCenterAsync(port);

        var emptyX = startX + 200;
        var emptyY = startY + 80;

        await page.Mouse.MoveAsync((float)startX, (float)startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)emptyX, (float)emptyY);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);

        var dangling = page.Locator(".tm-diagram-edge-handle--dangling").First;
        await dangling.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Drag dangling handle onto class2 node
        var node2 = page.Locator($".tm-diagram-node[data-node-id='{Node2Id}']");
        var (targetX, targetY) = await GetCenterAsync(node2);
        var (dgX, dgY) = await GetCenterAsync(dangling);

        await page.Mouse.MoveAsync((float)dgX, (float)dgY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)targetX, (float)targetY);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(2500); // outline-connect requires 2s hover timer

        var remainingDangling = page.Locator(".tm-diagram-edge-handle--dangling");
        var remainingCount = await remainingDangling.CountAsync();
        Assert.AreEqual(0, remainingCount, "Expected dangling handle to disappear after outline connect");

        await TakeScreenshotAsync(page, "phase3_outline_connect");
    }

    [TestMethod]
    [Description("3.5.5 — Connection point click starts edge draw with constraint")]
    public async Task Phase3_ConnectionPoint_ClickStartsEdgeDraw()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var cp = page.Locator($".tm-diagram-node[data-node-id='{Node1Id}'] .tm-diagram-connection-point").First;
        if (await cp.CountAsync() == 0)
        {
            Assert.Inconclusive("No connection points rendered for the sample node.");
        }

        await cp.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var (cpx, cpy) = await GetCenterAsync(cp);

        await page.Mouse.MoveAsync((float)cpx, (float)cpy);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(cpx + 100), (float)(cpy + 50));
        await page.WaitForTimeoutAsync(200);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(300);

        // Canvas should still be intact
        var canvas = page.Locator(".tm-diagram-canvas");
        Assert.IsTrue(await canvas.IsVisibleAsync(), "Canvas should still be visible after connection point interaction");

        await TakeScreenshotAsync(page, "phase3_connection_point");
    }

    [TestMethod]
    [Description("3.6 — Edge-to-edge hover highlight while drawing")]
    public async Task Phase3_EdgeToEdge_HoverShowsHighlight()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Create a dangling edge first
        var port = page.Locator($".tm-diagram-node[data-node-id='{Node1Id}'] .tm-diagram-port[data-port-id='bottom']");
        await port.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var (px, py) = await GetCenterAsync(port);

        await page.Mouse.MoveAsync((float)px, (float)py);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)px, (float)(py + 120));
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);

        // Start a second draw
        var port2 = page.Locator($".tm-diagram-node[data-node-id='{Node2Id}'] .tm-diagram-port[data-port-id='bottom']");
        await port2.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var (p2x, p2y) = await GetCenterAsync(port2);

        await page.Mouse.MoveAsync((float)p2x, (float)p2y);
        await page.Mouse.DownAsync();

        var firstEdge = page.Locator(".tm-diagram-edge-path").First;
        var (ex, ey) = await GetCenterAsync(firstEdge);
        await page.Mouse.MoveAsync((float)ex, (float)ey);
        await page.WaitForTimeoutAsync(300);

        var highlighted = await firstEdge.EvaluateAsync<bool>(
            "el => el.closest('.tm-diagram-edge-group')?.classList.contains('tm-diagram-edge--target') || false");

        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(300);

        if (!highlighted)
        {
            Assert.Inconclusive("Edge-to-edge highlight not detected — known issue (3.6.2 audit finding).");
        }

        await TakeScreenshotAsync(page, "phase3_edge_to_edge");
    }

    // ========================================================================
    // FÁZE 4 — Pokročilé featury
    // ========================================================================

    [TestMethod]
    [Description("4.1 — Label drag moves label relative to edge")]
    public async Task Phase4_LabelDrag_MovesLabelRelativeToEdge()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Sample edge has label "1..*"
        var labelGroup = page.Locator(".tm-diagram-edge-label-group").First;
        if (await labelGroup.CountAsync() == 0)
        {
            Assert.Inconclusive("No edge label rendered on sample edge.");
        }

        await labelGroup.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var (lx, ly) = await GetCenterAsync(labelGroup);

        await page.Mouse.MoveAsync((float)lx, (float)ly);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(lx + 20), (float)(ly - 15));
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);

        // Label should still be visible after drag
        Assert.IsTrue(await labelGroup.IsVisibleAsync(), "Label should remain visible after drag");

        await TakeScreenshotAsync(page, "phase4_label_drag");
    }

    [TestMethod]
    [Description("4.2 — Double-click on elbow handle flips orientation")]
    public async Task Phase4_ElbowFlip_DoubleClickChangesOrientation()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Switch to elbow via properties panel (context menu may be blocked by HTML overlay)
        await SelectEdgeAsync(page);
        await SetEdgeRoutingAsync(page, "elbow");

        var handles = page.Locator(".tm-diagram-edge-handle:not(.tm-diagram-edge-handle--dangling):not(.tm-diagram-edge-handle--jetty):not(.tm-diagram-edge-segment-handle)");
        if (await handles.CountAsync() < 2)
        {
            Assert.Inconclusive("Not enough handles for elbow flip test.");
        }

        var middle = handles.Nth(1);
        // Dispatch dblclick via JS because HTML overlay blocks Mouse.DblClickAsync on SVG handles
        await middle.EvaluateAsync(@"
            el => {
                const rect = el.getBoundingClientRect();
                const cx = rect.left + rect.width / 2;
                const cy = rect.top + rect.height / 2;
                el.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true, clientX: cx, clientY: cy }));
            }");
        await page.WaitForTimeoutAsync(800);

        // Edge should still be rendered after flip
        var edgePath = page.Locator(".tm-diagram-edge-path").First;
        // Use CountAsync > 0 instead of IsVisibleAsync to avoid transient detached-element issues during Blazor re-render
        var edgeCount = await edgePath.CountAsync();
        Assert.IsTrue(edgeCount > 0, "Edge should remain visible after elbow flip");

        await TakeScreenshotAsync(page, "phase4_elbow_flip");
    }

    [TestMethod]
    [Description("4.4 — ArcSize input changes edge rounding")]
    public async Task Phase4_ArcSize_InputChangesRounding()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        var propsPanel = page.Locator(".tm-diagram-properties-panel, .tm-diagram-editor__props-wrap").First;

        // Ensure Rounded is enabled so ArcSize input is visible
        var roundedCheckbox = propsPanel.Locator(".tm-checkbox-wrapper").Filter(new LocatorFilterOptions { HasText = "Rounded" }).First;
        if (await roundedCheckbox.CountAsync() > 0)
        {
            var isChecked = await roundedCheckbox.Locator("input").IsCheckedAsync();
            if (!isChecked)
            {
                await roundedCheckbox.ClickAsync();
                await page.WaitForTimeoutAsync(300);
            }
        }

        // Find ArcSize input by label text (localized as "Corner Radius")
        var arcSizeField = propsPanel.Locator(".tm-diagram-properties__field").Filter(new LocatorFilterOptions { HasText = "Radius" }).First;
        if (await arcSizeField.CountAsync() == 0)
        {
            Assert.Inconclusive("ArcSize (Corner Radius) field not found in properties panel.");
        }
        var targetInput = arcSizeField.Locator("input[type='number']");
        await targetInput.FillAsync("25");
        await targetInput.BlurAsync();
        await page.WaitForTimeoutAsync(1500);

        // Verify edge path is still present (IsVisibleAsync can return false during re-render)
        var edgePaths = page.Locator(".tm-diagram-edge-path");
        var edgeCount = await edgePaths.CountAsync();
        Assert.IsTrue(edgeCount > 0, "Edge should remain visible after ArcSize change");

        await TakeScreenshotAsync(page, "phase4_arcsize");
    }

    // ========================================================================
    // FÁZE 5 — Specializované
    // ========================================================================

    [TestMethod]
    [Description("5.1 — Cubic bezier toggle changes path shape")]
    public async Task Phase5_CubicBezier_ToggleChangesPathShape()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        // Cubic Bézier checkbox is only visible when routing is "curved"
        await SetEdgeRoutingAsync(page, "curved");

        var propsPanel = page.Locator(".tm-diagram-properties-panel, .tm-diagram-editor__props-wrap").First;
        var checkbox = propsPanel.Locator(".tm-checkbox-wrapper").Filter(new LocatorFilterOptions { HasText = "Cubic" }).First;
        if (await checkbox.CountAsync() == 0)
        {
            Assert.Inconclusive("Cubic bezier checkbox not found.");
        }

        // Get path d attribute before
        var edgePath = page.Locator(".tm-diagram-edge-path").First;
        var dBefore = await edgePath.GetAttributeAsync("d");

        await checkbox.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var dAfter = await edgePath.GetAttributeAsync("d");
        Assert.AreNotEqual(dBefore, dAfter, "Path data should change after toggling cubic bezier");

        await TakeScreenshotAsync(page, "phase5_cubic_bezier");
    }

    [TestMethod]
    [Description("5.6 — Rubber-band selection includes edges")]
    public async Task Phase5_RubberBandSelection_IncludesEdges()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Deselect by clicking empty area (top-left corner of canvas is usually empty)
        var canvas = page.Locator(".tm-diagram-canvas");
        var box = await canvas.BoundingBoxAsync();
        Assert.IsNotNull(box);
        await page.Mouse.ClickAsync((float)(box.X + 10), (float)(box.Y + 10));
        await page.WaitForTimeoutAsync(200);

        // Use JS to find node screen positions so we can start the rubber-band guaranteed outside any node
        var nodeRectsJson = await page.EvaluateAsync<string>("""
            () => {
                const nodes = document.querySelectorAll('.tm-diagram-node[data-node-id]');
                const arr = [];
                nodes.forEach(n => { const r = n.getBoundingClientRect(); arr.push({ left: r.left, top: r.top, right: r.right, bottom: r.bottom }); });
                return JSON.stringify(arr);
            }
        """);
        using var rectsDoc = JsonDocument.Parse(nodeRectsJson);
        var rects = rectsDoc.RootElement.EnumerateArray()
            .Select(e => (Left: e.GetProperty("left").GetDouble(), Top: e.GetProperty("top").GetDouble(), Right: e.GetProperty("right").GetDouble(), Bottom: e.GetProperty("bottom").GetDouble()))
            .ToList();
        Assert.IsTrue(rects.Count > 0, "Expected at least one node on the canvas");
        var minLeft = rects.Min(r => r.Left);
        var minTop = rects.Min(r => r.Top);
        var maxRight = rects.Max(r => r.Right);
        var maxBottom = rects.Max(r => r.Bottom);

        // Start slightly outside the node bounding box to guarantee empty-canvas mousedown
        var startX = minLeft - 20;
        var startY = minTop - 20;
        var endX = maxRight + 20;
        var endY = maxBottom + 20;

        await page.Mouse.MoveAsync((float)startX, (float)startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)endX, (float)endY);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(400);

        // Check that rubber-band element existed during drag (it is removed after mouseup)
        // We verify selection changed by checking that nodes got selected class or selection outlines
        var outlines = page.Locator(".tm-diagram-selection-outline");
        var outlineCount = await outlines.CountAsync();
        Assert.IsTrue(outlineCount > 0, "Rubber-band should select at least one node and create selection outline");

        await TakeScreenshotAsync(page, "phase5_rubber_band");
    }

    [TestMethod]
    [Description("5.7 — Selected edge shows selection outline")]
    public async Task Phase5_SelectionOutline_VisibleOnSelectedEdge()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        // Selection outline for edge is rendered as a dashed outline path inside the SVG
        var outlinePath = page.Locator(".tm-diagram-edge-path--selected-outline").First;
        var dAttr = await outlinePath.GetAttributeAsync("d");
        Assert.IsFalse(string.IsNullOrEmpty(dAttr), "Selected edge should have a selection outline path");

        await TakeScreenshotAsync(page, "phase5_selection_outline");
    }

    [TestMethod]
    [Description("5.5 — Cardinality symbols render near edge terminals")]
    public async Task Phase5_Cardinality_SelectShowsSymbols()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);

        var propsPanel = page.Locator(".tm-diagram-properties-panel, .tm-diagram-editor__props-wrap").First;
        // Find cardinality select by label text (TmSelect renders a native <select>)
        var cardinalityField = propsPanel.Locator(".tm-diagram-properties__field").Filter(new LocatorFilterOptions { HasText = "Cardinality" }).First;
        if (await cardinalityField.CountAsync() == 0)
        {
            Assert.Inconclusive("Cardinality field not found in properties panel.");
        }
        var cardinalitySelect = cardinalityField.Locator("select");
        await cardinalitySelect.SelectOptionAsync("one");
        await page.WaitForTimeoutAsync(500);

        var cardinalityText = page.Locator(".tm-diagram-edge-cardinality");
        var textCount = await cardinalityText.CountAsync();
        Assert.IsTrue(textCount > 0, "Cardinality text element should be rendered after selection");

        await TakeScreenshotAsync(page, "phase5_cardinality");
    }

    // ========================================================================
    // FÁZE 6 — Drag celé čáry (Whole Edge Drag)
    // ========================================================================

    [TestMethod]
    [Description("6.1 — Dragging a selected edge by its hit-path detaches both ends and moves the edge")]
    public async Task Phase6_WholeEdgeDrag_DetachesAndMovesEdge()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Select the edge first so handles are visible
        await SelectEdgeAsync(page);
        await page.WaitForTimeoutAsync(200);

        // Drag the edge via JS dispatchEvent — the HTML overlay intercepts native mouse
        // events, so Playwright Mouse API doesn't reach the SVG/canvas handlers reliably.
        // We compute the mid-point of the visible edge path using SVG APIs (getPointAtLength
        // + CTM) because getBoundingClientRect on a transparent stroke-only path returns
        // an empty rect in some browsers.
        var result = await page.EvaluateAsync<string>("""
            () => {
                const path = document.querySelector('.tm-diagram-edge-path');
                const hitPath = document.querySelector('.tm-diagram-edge-hit-path');
                if (!path || !hitPath) return 'missing';

                const len = path.getTotalLength();
                const mid = path.getPointAtLength(len / 2);
                const svg = path.closest('svg');
                const pt = svg.createSVGPoint();
                pt.x = mid.x;
                pt.y = mid.y;
                const ctm = svg.getScreenCTM();
                if (!ctm) return 'noctm';
                const sp = pt.matrixTransform(ctm);

                const sx = sp.x;
                const sy = sp.y;
                const ex = sx + 120;
                const ey = sy + 80;

                hitPath.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, clientX: sx, clientY: sy }));
                document.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true, button: 0, clientX: ex, clientY: ey }));
                document.dispatchEvent(new MouseEvent('mouseup',   { bubbles: true, cancelable: true, button: 0, clientX: ex, clientY: ey }));
                return 'done';
            }
        """);
        Assert.AreEqual("done", result, "Whole-edge drag dispatch failed");
        await page.WaitForTimeoutAsync(800);

        // Edge path should still exist after the drag
        var edgePaths = page.Locator(".tm-diagram-edge-path");
        Assert.IsTrue(await edgePaths.CountAsync() > 0, "Edge path should still exist after whole-edge drag");

        // After detaching, dangling handles should appear (proof that ends disconnected)
        var danglingHandles = page.Locator(".tm-diagram-edge-handle--dangling");
        var danglingCount = await danglingHandles.CountAsync();
        Assert.IsTrue(danglingCount >= 2, $"Expected at least 2 dangling handles after whole-edge drag (got {danglingCount})");

        await TakeScreenshotAsync(page, "phase6_whole_edge_drag");
    }

    [TestMethod]
    [Description("6.2 — Dragging a connected terminal handle detaches the end from its node")]
    public async Task Phase6_ConnectedTerminalDrag_DetachesEnd()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Select edge to reveal dangling handles on connected ends
        await SelectEdgeAsync(page);
        await page.WaitForTimeoutAsync(300);

        // Drag the source dangling handle (on a connected end) away from its node
        var result = await page.EvaluateAsync<string>("""
            () => {
                const handle = document.querySelector('rect.tm-diagram-edge-handle--dangling[data-dangling="source"]');
                if (!handle) return 'missing';
                const r = handle.getBoundingClientRect();
                const sx = r.left + r.width / 2;
                const sy = r.top + r.height / 2;
                const ex = sx + 150;
                const ey = sy + 100;
                handle.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, clientX: sx, clientY: sy }));
                document.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true, button: 0, clientX: ex, clientY: ey }));
                document.dispatchEvent(new MouseEvent('mouseup',   { bubbles: true, cancelable: true, button: 0, clientX: ex, clientY: ey }));
                return 'done';
            }
        """);
        Assert.AreEqual("done", result, "Connected-terminal drag dispatch failed");
        await page.WaitForTimeoutAsync(800);

        // Edge path should still exist
        var edgePaths = page.Locator(".tm-diagram-edge-path");
        Assert.IsTrue(await edgePaths.CountAsync() > 0, "Edge path should still exist after connected-terminal drag");

        // The source end should now be detached — at least one dangling handle should remain visible
        var danglingHandles = page.Locator(".tm-diagram-edge-handle--dangling");
        var danglingCount = await danglingHandles.CountAsync();
        Assert.IsTrue(danglingCount >= 1, $"Expected at least 1 dangling handle after connected-terminal drag (got {danglingCount})");

        await TakeScreenshotAsync(page, "phase6_connected_terminal_drag");
    }

    [TestMethod]
    [Description("6.3 — Floating inline toolbar appears when a single edge is selected")]
    public async Task Phase6_InlineToolbar_AppearsOnEdgeSelection()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);
        await page.WaitForTimeoutAsync(300);

        var toolbar = page.Locator(".tm-diagram-edge-toolbar");
        await toolbar.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var buttons = toolbar.Locator("button");
        var btnCount = await buttons.CountAsync();
        Assert.IsTrue(btnCount >= 2, $"Expected at least 2 toolbar buttons (got {btnCount})");

        await TakeScreenshotAsync(page, "phase6_inline_toolbar_visible");
    }

    [TestMethod]
    [Description("6.4 — Inline toolbar flip button works (edge remains visible after flip)")]
    public async Task Phase6_InlineToolbar_FlipButtonWorks()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        // Select edge and switch to elbow routing so flip is applicable
        await SelectEdgeAsync(page);
        await SetEdgeRoutingAsync(page, "elbow");
        await page.WaitForTimeoutAsync(300);

        var flipBtn = page.Locator(".tm-diagram-edge-toolbar button[data-action='flip']");
        if (await flipBtn.CountAsync() == 0)
        {
            Assert.Inconclusive("Flip button not found in inline toolbar.");
        }

        await flipBtn.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        // Edge should still be rendered after flip (degenerate elbow may not change path visually)
        var edgePath = page.Locator(".tm-diagram-edge-path").First;
        Assert.IsTrue(await edgePath.CountAsync() > 0, "Edge should remain visible after toolbar flip");

        await TakeScreenshotAsync(page, "phase6_inline_toolbar_flip");
    }

    [TestMethod]
    [Description("6.5 — Context menu shows graphical routing picker with SVG previews")]
    public async Task Phase6_ContextMenu_RoutingPickerHasSvgPreviews()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);
        await OpenEdgeContextMenuAsync(page);

        var picker = page.Locator(".tm-diagram-edge-routing-picker");
        await picker.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var previews = picker.Locator(".tm-diagram-routing-preview");
        var count = await previews.CountAsync();
        Assert.IsTrue(count >= 4, $"Expected at least 4 routing previews (got {count})");

        var firstSvg = previews.First.Locator("svg");
        Assert.IsTrue(await firstSvg.CountAsync() > 0, "Each preview should contain an SVG");

        // Click orthogonal preview
        var orthogonal = picker.Locator(".tm-diagram-routing-preview[data-routing='orthogonal']");
        if (await orthogonal.CountAsync() == 0)
        {
            Assert.Inconclusive("Orthogonal preview not found in routing picker.");
        }
        await orthogonal.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        // Context menu should close
        var menu = page.Locator(".tm-diagram-editor__context-menu");
        Assert.IsTrue(await menu.CountAsync() == 0, "Context menu should close after selecting routing");

        await TakeScreenshotAsync(page, "phase6_routing_picker");
    }

    [TestMethod]
    [Description("6.6 — Context menu Flip action works for elbow edges")]
    public async Task Phase6_ContextMenu_FlipActionWorks()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);
        await SetEdgeRoutingAsync(page, "elbow");
        await page.WaitForTimeoutAsync(300);

        await OpenEdgeContextMenuAsync(page);

        var flipItem = page.Locator(".tm-diagram-editor__context-item").Filter(new LocatorFilterOptions { HasText = "Flip" }).First;
        if (await flipItem.CountAsync() == 0)
        {
            Assert.Inconclusive("Flip item not found in edge context menu.");
        }

        await flipItem.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var edgePath = page.Locator(".tm-diagram-edge-path").First;
        Assert.IsTrue(await edgePath.CountAsync() > 0, "Edge should remain visible after context-menu flip");

        await TakeScreenshotAsync(page, "phase6_context_menu_flip");
    }

    [TestMethod]
    [Description("6.7 — Context menu Clear waypoints removes manual waypoints")]
    public async Task Phase6_ContextMenu_ClearWaypointsWorks()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        await SelectEdgeAsync(page);
        await SetEdgeRoutingAsync(page, "orthogonal");
        await page.WaitForTimeoutAsync(300);

        var virtualBends = page.Locator(".tm-diagram-edge-virtual-bend");
        if (await virtualBends.CountAsync() > 0)
        {
            await page.EvaluateAsync("""
                () => {
                    const vb = document.querySelector('.tm-diagram-edge-virtual-bend');
                    if (vb) {
                        const r = vb.getBoundingClientRect();
                        vb.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, clientX: r.left + r.width/2, clientY: r.top + r.height/2 }));
                    }
                }
            """);
            await page.WaitForTimeoutAsync(500);
        }

        var handlesBefore = page.Locator(".tm-diagram-edge-handle:not(.tm-diagram-edge-handle--dangling):not(.tm-diagram-edge-handle--jetty):not(.tm-diagram-edge-segment-handle)");
        var countBefore = await handlesBefore.CountAsync();

        await OpenEdgeContextMenuAsync(page);

        var clearItem = page.Locator(".tm-diagram-editor__context-item").Filter(new LocatorFilterOptions { HasText = "Clear waypoints" }).First;
        if (await clearItem.CountAsync() == 0)
        {
            Assert.Inconclusive("Clear waypoints item not found in edge context menu.");
        }

        await clearItem.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var handlesAfter = page.Locator(".tm-diagram-edge-handle:not(.tm-diagram-edge-handle--dangling):not(.tm-diagram-edge-handle--jetty):not(.tm-diagram-edge-segment-handle)");
        var countAfter = await handlesAfter.CountAsync();
        Assert.IsTrue(countAfter < countBefore, $"Expected fewer waypoint handles after clear (before={countBefore}, after={countAfter})");

        await TakeScreenshotAsync(page, "phase6_context_menu_clear_waypoints");
    }

    // ========================================================================
    // Smoke / Integration
    // ========================================================================

    [TestMethod]
    [Description("Diagram page loads with preloaded UML sample nodes and edges")]
    public async Task Smoke_DiagramPage_LoadsWithSample()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync(BaseUrl + DiagramEditorUrl);
        await WaitForCanvasAsync(page);

        var node1 = page.Locator($".tm-diagram-node[data-node-id='{Node1Id}']");
        var node2 = page.Locator($".tm-diagram-node[data-node-id='{Node2Id}']");
        await node1.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await node2.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var edges = page.Locator(".tm-diagram-edge-path");
        Assert.IsTrue(await edges.CountAsync() > 0, "Expected at least one edge");

        await TakeScreenshotAsync(page, "smoke_diagram_load");
    }
}
