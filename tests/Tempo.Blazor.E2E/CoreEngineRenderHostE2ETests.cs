using System.Globalization;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Phase R.4.1 browser gate for the new model-owned core engine.
/// Loads the bundled modules (<c>document-editor.dist.js</c>) via a static harness
/// page and drives <c>coreEngine.createRenderHost</c> in a real browser to verify:
///   (a) the headless layout → positioned-DOM pipeline renders a multi-page document,
///   (b) page virtualization paints only visible pages,
///   (c) R.4.0 font metrics match the browser's real text measurement (&lt; 1px),
///   (d) first-paint is recorded.
///
/// Requires the running WASM demo on https://localhost:7106. Rebuild the bundle with
/// <c>npm run build:document-editor</c> before running so the harness loads the
/// current modules.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CoreEngineRenderHostE2ETests : WasmTestBase
{
    private const string HarnessUrl = "/core-engine-harness.html";

    private async Task<IPage> OpenHarnessAsync(int width = 1280, int height = 900)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}{HarnessUrl}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 60000,
        });
        // Wait for the IIFE bundle global + coreEngine to be present.
        await page.WaitForFunctionAsync(
            "() => window.__coreEngineHarness && window.__coreEngineHarness.ready === true",
            null,
            new PageWaitForFunctionOptions { Timeout = 30000 });
        return page;
    }

    [TestMethod]
    public async Task R41_RenderHost_RendersMultiPageDocument_AndVirtualizes()
    {
        var page = await OpenHarnessAsync();

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');

            // Build a realistic 100-paragraph document.
            const blocks = [];
            for (let i = 0; i < 100; i++) {
                blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph',
                    runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph ' + i + ' — the quick brown fox jumps over the lazy dog.' }] } });
            }
            const model = { documentId: 'r41-100p', body: { blocks } };

            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 });

            const t0 = performance.now();
            const res = host.render();
            const firstPaintMs = performance.now() - t0;

            const countAttr = (attr) => document.querySelectorAll('[' + attr + ']').length;
            const usingRealMetrics = M.layout.fontMetrics.createFontMetricsService().isUsingRealMetrics();

            // Virtualize to the first viewport and re-render.
            host.setViewport({ scrollTop: 0, height: 1200, overscanPages: 0 });
            host.render();
            const visiblePages = (host.getSnapshot().layout.visiblePageIndices || []);
            const paintedBlocksVirtual = countAttr('data-render-block-id');

            // Back to full render.
            host.setViewport(null);
            host.render();
            const paintedBlocksFull = countAttr('data-render-block-id');

            return JSON.stringify({
                ok: res.ok,
                layoutPages: res.layout.pages.length,
                layoutBlocks: res.layout.blocks.length,
                paintedBlocksFull,
                paintedBlocksVirtual,
                visiblePageCount: visiblePages.length,
                firstPaintMs,
                usingRealMetrics,
            });
        }");

        var result = JsonDocument.Parse(json.GetString()!).RootElement;

        result.GetProperty("ok").GetBoolean().Should().BeTrue("the pipeline should render successfully");
        result.GetProperty("layoutPages").GetInt32().Should().BeGreaterThan(1, "100 paragraphs span multiple pages");
        result.GetProperty("layoutBlocks").GetInt32().Should().Be(100);
        result.GetProperty("paintedBlocksFull").GetInt32().Should().Be(100, "all blocks painted without a viewport");
        result.GetProperty("usingRealMetrics").GetBoolean().Should().BeTrue("the browser provides a real canvas → real font metrics");

        var visiblePageCount = result.GetProperty("visiblePageCount").GetInt32();
        var layoutPages = result.GetProperty("layoutPages").GetInt32();
        visiblePageCount.Should().BeInRange(1, layoutPages - 1, "virtualization should paint a strict subset of pages");
        result.GetProperty("paintedBlocksVirtual").GetInt32().Should()
            .BeLessThan(100, "virtualized render paints fewer blocks than the full document");

        var firstPaintMs = result.GetProperty("firstPaintMs").GetDouble();
        TestContext.WriteLine($"R.4.1 first-paint (100p): {firstPaintMs.ToString("F1", CultureInfo.InvariantCulture)} ms");
        firstPaintMs.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task R42_OffScreenInput_TypingRoutesToModel_NoVisibleContentEditable()
    {
        var page = await OpenHarnessAsync();

        // Build a single-empty-paragraph document + attach the off-screen input surface.
        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r42', body: { blocks: [
                { id: 'b1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: '' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'b1', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");

        // Type with the REAL keyboard → routes through beforeinput → edit-model → re-render.
        await page.Keyboard.TypeAsync("Hello");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("World");

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            const segText = Array.from(document.querySelectorAll('[data-render-block-id]'))
                .map((b) => b.textContent).filter((t) => t && t.length);
            // No visible contenteditable surface anywhere (the capture element is a <textarea>).
            const contentEditableCount = document.querySelectorAll('[contenteditable=""true""]').length;
            const captureTag = host.getInputSurface().element.tagName;
            return JSON.stringify({
                blockCount: host.getSnapshot().layout.blocks.length,
                segText,
                caret: host.getCaret(),
                contentEditableCount,
                captureTag,
            });
        }");

        var r = JsonDocument.Parse(json.GetString()!).RootElement;
        r.GetProperty("contentEditableCount").GetInt32().Should().Be(0,
            "the new engine must NOT use a visible contenteditable surface");
        r.GetProperty("captureTag").GetString().Should().Be("TEXTAREA",
            "keystrokes are captured by an off-screen textarea");
        r.GetProperty("blockCount").GetInt32().Should().Be(2, "Enter split the paragraph into two blocks");

        var segments = r.GetProperty("segText").EnumerateArray().Select(e => e.GetString()).ToList();
        var allText = string.Concat(segments);
        allText.Should().Contain("Hello", "typed text appears in the rendered DOM");
        allText.Should().Contain("World", "text typed after Enter appears in the second block");
        TestContext.WriteLine("R.4.2 rendered text segments: " + string.Join(" | ", segments));
    }

    [TestMethod]
    public async Task R43_Caret_ClickPlacesCaret_ArrowsMove_ShiftSelects()
    {
        var page = await OpenHarnessAsync();

        // Build a one-line document and attach input. Return the client coords to click
        // for caret offset 5 (just after "Hello"), computed from the layout caret stop.
        var setupJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r43', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Hello world example' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render();
            window.__host = host;

            const layout = host.getLayout();
            const stop = M.coreEngine.hitTest.caretStopAt(layout, { blockId: 'p1', offset: 5 });
            const section = root.querySelector('.tm-render-page[data-render-page-index=""0""]')
                || root.querySelector('[data-render-page-index=""0""]');
            const sr = section.getBoundingClientRect();
            const pr = layout.pages[0].rect;
            return JSON.stringify({
                clientX: sr.left + (stop.rect.x - pr.x),
                clientY: sr.top + (stop.rect.y - pr.y) + (stop.rect.height / 2),
            });
        }");
        var setup = JsonDocument.Parse(setupJson.GetString()!).RootElement;
        var clickX = (float)setup.GetProperty("clientX").GetDouble();
        var clickY = (float)setup.GetProperty("clientY").GetDouble();

        // Real mouse click → pointerdown → hit-test → caret at offset 5.
        await page.Mouse.ClickAsync(clickX, clickY);

        var afterClick = await page.EvaluateAsync<int>("() => window.__host.getCaret().offset");
        afterClick.Should().BeInRange(4, 6, "clicking just after 'Hello' places the caret near offset 5");

        // Caret element exists + visible.
        var caretVisible = await page.EvaluateAsync<bool>(@"() => {
            const c = window.__host.getCaretElement();
            return !!c && c.style.display !== 'none';
        }");
        caretVisible.Should().BeTrue("a blinking caret element is painted at the caret position");

        // ArrowRight moves the caret forward.
        await page.Keyboard.PressAsync("ArrowRight");
        var afterArrow = await page.EvaluateAsync<int>("() => window.__host.getCaret().offset");
        afterArrow.Should().Be(afterClick + 1, "ArrowRight advances the caret by one");

        // Shift+ArrowRight extends the selection → at least one selection rect painted.
        await page.Keyboard.PressAsync("Shift+ArrowRight");
        var selJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            const range = host.getSelectionRange();
            return JSON.stringify({
                selRectCount: host.getSelectionElements().length,
                anchorOffset: range.anchor.offset,
                focusOffset: range.focus.offset,
            });
        }");
        var sel = JsonDocument.Parse(selJson.GetString()!).RootElement;
        sel.GetProperty("selRectCount").GetInt32().Should().BeGreaterThan(0, "shift+arrow paints a selection rectangle");
        (sel.GetProperty("focusOffset").GetInt32() - sel.GetProperty("anchorOffset").GetInt32())
            .Should().Be(1, "the selection spans exactly one character");
        TestContext.WriteLine($"R.4.3 click→caret {afterClick}, arrow→{afterArrow}, selRects {sel.GetProperty("selRectCount").GetInt32()}");
    }

    [TestMethod]
    public async Task R44_ImeComposition_PreviewUnderlines_CommitsFinalText()
    {
        var page = await OpenHarnessAsync();

        // Build "Hi", place the caret at the end (offset 2), attach the off-screen input,
        // then drive a CJK IME sequence with REAL CompositionEvents dispatched on the
        // capture textarea in the live browser: start → updates (preview replaces, never
        // accumulates) → end (commit). We read the rendered DOM (not just the model) so
        // this verifies the full compositionstart/update/end → model → re-layout → paint
        // path, including the painted pre-edit underline.
        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r44', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Hi' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 2 }).attachInput();
            host.render();
            host.focusInput();

            const ta = host.getInputSurface().element;
            const fire = (type, data) => ta.dispatchEvent(new CompositionEvent(type, { data: data == null ? '' : data, bubbles: true, cancelable: true }));
            const domText = () => Array.from(document.querySelectorAll('[data-render-block-id]')).map((b) => b.textContent).join('');
            const underlines = () => document.querySelectorAll('[data-testid=""core-engine-composition-underline""]').length;

            fire('compositionstart');
            const afterStart = { composing: host.isComposing(), compText: host.getComposition().text, start: host.getComposition().start };

            fire('compositionupdate', 'か');
            const u1 = { dom: domText(), caret: host.getCaret().offset, compText: host.getComposition().text };
            fire('compositionupdate', 'かん');
            const u2 = { dom: domText(), caret: host.getCaret().offset };
            fire('compositionupdate', '感'); // IME collapses 2 kana into 1 kanji → preview shrinks
            const u3 = { dom: domText(), caret: host.getCaret().offset, compText: host.getComposition().text, underlines: underlines(), composing: host.isComposing() };

            fire('compositionend', '感じ'); // commit the candidate
            const end = { dom: domText(), caret: host.getCaret().offset, composition: host.getComposition(), underlines: underlines(), composing: host.isComposing() };

            // No visible contenteditable anywhere; capture stays a textarea.
            const contentEditableCount = document.querySelectorAll('[contenteditable=""true""]').length;
            return JSON.stringify({ afterStart, u1, u2, u3, end, contentEditableCount, captureTag: ta.tagName });
        }");

        var r = JsonDocument.Parse(json.GetString()!).RootElement;

        // compositionstart begins an empty preview at the caret.
        var afterStart = r.GetProperty("afterStart");
        afterStart.GetProperty("composing").GetBoolean().Should().BeTrue("compositionstart marks the host composing");
        afterStart.GetProperty("compText").GetString().Should().Be("");
        afterStart.GetProperty("start").GetInt32().Should().Be(2, "composition begins at the caret offset");

        // Live preview is in the rendered DOM and the caret follows it.
        var u1 = r.GetProperty("u1");
        u1.GetProperty("dom").GetString().Should().Be("Hiか", "first preview char is rendered");
        u1.GetProperty("caret").GetInt32().Should().Be(3, "caret sits at the end of the preview");
        u1.GetProperty("compText").GetString().Should().Be("か");
        r.GetProperty("u2").GetProperty("dom").GetString().Should().Be("Hiかん");

        // The preview REPLACES (does not accumulate) and is underlined while composing.
        var u3 = r.GetProperty("u3");
        u3.GetProperty("dom").GetString().Should().Be("Hi感", "preview replaced, not 'Hiかん感'");
        u3.GetProperty("compText").GetString().Should().Be("感");
        u3.GetProperty("composing").GetBoolean().Should().BeTrue();
        u3.GetProperty("underlines").GetInt32().Should().BeGreaterThan(0, "a pre-edit underline is painted under the composing text");

        // compositionend commits the final candidate as one edit; composition cleared.
        var end = r.GetProperty("end");
        end.GetProperty("dom").GetString().Should().Be("Hi感じ", "the committed candidate replaces the preview");
        end.GetProperty("caret").GetInt32().Should().Be(4, "caret after the committed text");
        end.GetProperty("composing").GetBoolean().Should().BeFalse("composition is finished");
        end.GetProperty("composition").ValueKind.Should().Be(JsonValueKind.Null, "composition state cleared on end");
        end.GetProperty("underlines").GetInt32().Should().Be(0, "the pre-edit underline is removed after commit");

        r.GetProperty("contentEditableCount").GetInt32().Should().Be(0, "the IME flow uses no visible contenteditable");
        r.GetProperty("captureTag").GetString().Should().Be("TEXTAREA");
        TestContext.WriteLine($"R.4.4 IME: preview か→かん→感 (replace), commit→{end.GetProperty("dom").GetString()}, caret {end.GetProperty("caret").GetInt32()}");
    }

    [TestMethod]
    public async Task R45_Bidi_RtlRendersVisually_AndGraphemeCaretSkipsEmoji()
    {
        var page = await OpenHarnessAsync();

        // --- Part A: RTL (Hebrew + Arabic) renders in visual order with mirrored caret
        // geometry. Computed with the browser's REAL font metrics + real bidi pass.
        var rtlJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const HE = 'שלום'; // שלום
            const AR = 'عربي'; // عربي
            const model = { documentId: 'r45', body: { blocks: [
                { id: 'he', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'rhe', kind: 'text', text: HE }] } },
                { id: 'ar', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'rar', kind: 'text', text: AR }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'he', offset: 0 });
            host.render();

            const L = host.getLayout();
            const heBlk = L.blocks.find((b) => b.blockId === 'he');
            const stopX = (o) => heBlk.caretStops.filter((s) => s.offset === o)[0].rect.x;

            const rtlSegs = document.querySelectorAll('.tm-render-segment[dir=""rtl""]').length;
            const renderedText = Array.from(document.querySelectorAll('[data-render-block-id]')).map((b) => b.textContent).join('|');
            return JSON.stringify({
                rtlSegs,
                renderedText,
                heX0: stopX(0),
                heX4: stopX(4),
                baseDirHe: M.layout.bidi.baseDirection(HE),
                baseDirAr: M.layout.bidi.baseDirection(AR),
                // combining-mark grapheme boundary in the real browser Intl.Segmenter
                combiningNext: M.layout.grapheme.nextGraphemeBoundary('éfg', 0),
            });
        }");
        var rtl = JsonDocument.Parse(rtlJson.GetString()!).RootElement;

        rtl.GetProperty("rtlSegs").GetInt32().Should().BeGreaterThan(0, "RTL segments are tagged dir=rtl so the browser shapes them");
        rtl.GetProperty("renderedText").GetString().Should().Contain("שלום", "Hebrew text is rendered");
        rtl.GetProperty("baseDirHe").GetString().Should().Be("rtl");
        rtl.GetProperty("baseDirAr").GetString().Should().Be("rtl");
        rtl.GetProperty("heX0").GetDouble().Should().BeGreaterThan(rtl.GetProperty("heX4").GetDouble(),
            "RTL: the caret at logical offset 0 sits to the RIGHT of the caret at the end");
        rtl.GetProperty("combiningNext").GetInt32().Should().Be(2, "base letter + combining acute is one grapheme cluster");

        // --- Part B: grapheme caret movement over an emoji, driven by the REAL keyboard.
        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r45b', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'a👍b' }] } }, // a👍b
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");

        await page.Keyboard.PressAsync("ArrowRight");
        var afterFirst = await page.EvaluateAsync<int>("() => window.__host.getCaret().offset");
        afterFirst.Should().Be(1, "ArrowRight from 0 lands after 'a'");

        await page.Keyboard.PressAsync("ArrowRight");
        var afterEmoji = await page.EvaluateAsync<int>("() => window.__host.getCaret().offset");
        afterEmoji.Should().Be(3, "ArrowRight skips the whole 2-code-unit emoji (grapheme step), not landing at offset 2");

        await page.Keyboard.PressAsync("ArrowLeft");
        var afterBack = await page.EvaluateAsync<int>("() => window.__host.getCaret().offset");
        afterBack.Should().Be(1, "ArrowLeft skips the emoji back to offset 1");

        TestContext.WriteLine($"R.4.5 RTL caret x0={rtl.GetProperty("heX0").GetDouble():F1} > xEnd={rtl.GetProperty("heX4").GetDouble():F1}; emoji caret 0→1→3→1; rtlSegs={rtl.GetProperty("rtlSegs").GetInt32()}");
    }

    [TestMethod]
    public async Task R46a_InlineMarks_BoldToggles_AndAlignmentApplies_InBrowser()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46a', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Hello world' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");

        // Select 'Hello' with the REAL keyboard (Shift+ArrowRight ×5 → routes through the
        // off-screen input surface → caret extension).
        for (int i = 0; i < 5; i++) await page.Keyboard.PressAsync("Shift+ArrowRight");
        var selOffsets = await page.EvaluateAsync<string>(@"() => { const r = window.__host.getSelectionRange(); return r.anchor.offset + '-' + r.focus.offset; }");
        selOffsets.Should().Be("0-5", "Shift+ArrowRight ×5 selects 'Hello'");

        // Apply bold (the API a toolbar button will call) and read the BROWSER's computed style.
        var boldJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.toggleMark('bold');
            const helloSpan = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => (s.textContent || '').trim() === 'Hello');
            const weight = helloSpan ? getComputedStyle(helloSpan).fontWeight : null;
            const worldSpan = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /world/.test(s.textContent || ''));
            const worldWeight = worldSpan ? getComputedStyle(worldSpan).fontWeight : null;
            return JSON.stringify({ active: host.isMarkActive('bold'), weight, worldWeight, helloText: helloSpan ? helloSpan.textContent : null });
        }");
        var bold = JsonDocument.Parse(boldJson.GetString()!).RootElement;
        bold.GetProperty("active").GetBoolean().Should().BeTrue("host reports bold active for the selection");
        bold.GetProperty("helloText").GetString().Should().Be("Hello", "the bolded run is its own segment");
        bold.GetProperty("weight").GetString().Should().BeOneOf("700", "bold", "the 'Hello' segment renders bold (computed font-weight)");
        bold.GetProperty("worldWeight").GetString().Should().BeOneOf("400", "normal", "the rest stays normal weight");

        // Toggle bold off again.
        var offWeight = await page.EvaluateAsync<string>(@"() => {
            window.__host.toggleMark('bold');
            const helloSpan = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /Hello/.test(s.textContent || ''));
            return getComputedStyle(helloSpan).fontWeight;
        }");
        offWeight.Should().BeOneOf("400", "normal", "bold toggled back off");

        // Paragraph alignment applies and survives a render.
        var alignJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.setAlignment('center');
            const blk = host.getSnapshot().model.body.blocks[0];
            return JSON.stringify({ alignment: blk.content.alignment });
        }");
        var align = JsonDocument.Parse(alignJson.GetString()!).RootElement;
        align.GetProperty("alignment").GetString().Should().Be("center", "alignment set on the paragraph");

        TestContext.WriteLine($"R.4.6a bold weight={bold.GetProperty("weight").GetString()}, off={offWeight}, align=center");
    }

    [TestMethod]
    public async Task R46d_FloatingImage_RendersWraps_ClickSelects_HandleDragResizes()
    {
        var page = await OpenHarnessAsync();

        // Build a long paragraph, attach input, render. (1×1 transparent PNG data URI so the
        // <img> loads without a network round-trip.)
        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46d', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 't', kind: 'text',
                    text: 'The quick brown fox jumps over the lazy dog and keeps running across the field for a long time and even longer to force several wrapped lines beside the floating image box.' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
            window.__img = host.insertImage({
                url: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
                width: 160, height: 120, wrapMode: 'square', alt: 'pic',
            });
            host.clearObjectSelection(); // start deselected so we can test click-to-select
        }");

        // The floating figure + <img> render, and the body text wraps to the right of it.
        var renderJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const fig = document.querySelector('[data-testid=""core-engine-object""]');
            const img = document.querySelector('[data-testid=""core-engine-object-img""]');
            const segs = window.__host.getLayout().blocks[0].segments;
            const firstSegX = Math.round(segs.find((s) => /The/.test(s.text || '')).rect.x);
            return JSON.stringify({ hasFigure: !!fig, hasImg: !!img, firstSegX, handlesBefore: document.querySelectorAll('[data-resize-handle]').length });
        }");
        var rj = JsonDocument.Parse(renderJson.GetString()!).RootElement;
        rj.GetProperty("hasFigure").GetBoolean().Should().BeTrue("the floating image renders as a figure");
        rj.GetProperty("hasImg").GetBoolean().Should().BeTrue("the figure contains an <img>");
        rj.GetProperty("firstSegX").GetInt32().Should().BeGreaterThanOrEqualTo(160, "body text wraps to the right of the 160px image");
        rj.GetProperty("handlesBefore").GetInt32().Should().Be(0, "no handles while deselected");

        // REAL click on the image → selects it → 8 resize handles appear.
        var figBox = await page.Locator("[data-testid='core-engine-object']").BoundingBoxAsync();
        figBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(figBox!.X + figBox.Width / 2, figBox.Y + figBox.Height / 2);
        var afterClick = await page.EvaluateAsync<JsonElement>(@"() => JSON.stringify({ selected: window.__host.getSelectedObjectId(), handles: document.querySelectorAll('[data-resize-handle]').length })");
        var ac = JsonDocument.Parse(afterClick.GetString()!).RootElement;
        ac.GetProperty("selected").GetString().Should().NotBeNull("clicking the image selects it");
        ac.GetProperty("handles").GetInt32().Should().Be(8, "selection shows 8 resize handles");

        // REAL drag of the SE handle → image grows → text wraps further right.
        var widthBefore = await page.EvaluateAsync<double>("() => window.__host.getObjects()[0].rect.width");
        var firstSegBefore = await page.EvaluateAsync<int>("() => Math.round(window.__host.getLayout().blocks[0].segments.find(s => /The/.test(s.text||'')).rect.x)");
        var seBox = await page.Locator("[data-testid='core-engine-resize-handle-se']").BoundingBoxAsync();
        seBox.Should().NotBeNull();
        await page.Mouse.MoveAsync(seBox!.X + seBox.Width / 2, seBox.Y + seBox.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(seBox.X + seBox.Width / 2 + 80, seBox.Y + seBox.Height / 2 + 60, new MouseMoveOptions { Steps = 5 });
        await page.Mouse.UpAsync();

        var afterResize = await page.EvaluateAsync<JsonElement>(@"() => JSON.stringify({ width: window.__host.getObjects()[0].rect.width, firstSegX: Math.round(window.__host.getLayout().blocks[0].segments.find(s => /The/.test(s.text||'')).rect.x) })");
        var ar = JsonDocument.Parse(afterResize.GetString()!).RootElement;
        ar.GetProperty("width").GetDouble().Should().BeGreaterThan(widthBefore, "dragging the SE handle widens the image");
        ar.GetProperty("firstSegX").GetInt32().Should().BeGreaterThan(firstSegBefore, "the wider image pushes wrapped text further right");

        TestContext.WriteLine($"R.4.6d wrap firstSegX {rj.GetProperty("firstSegX").GetInt32()}, width {widthBefore}→{ar.GetProperty("width").GetDouble()}, segX {firstSegBefore}→{ar.GetProperty("firstSegX").GetInt32()}");
    }

    [TestMethod]
    public async Task R46i_UndoRedo_RealKeyboard_RevertsTypingAndFormatting()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46i', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: '' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");
        var text = () => page.EvaluateAsync<string>(@"() => { const b = document.querySelector('[data-render-block-id]'); return b ? (b.textContent || '') : ''; }");

        // Type with the REAL keyboard → one coalesced typing run.
        await page.Keyboard.TypeAsync("Hello");
        (await text()).Should().Be("Hello");
        var depth = await page.EvaluateAsync<int>("() => window.__host.getHistoryDepth().undo");
        depth.Should().Be(1, "the whole typing run is a single undo step");

        // REAL Ctrl+Z → reverts the entire run.
        await page.Keyboard.PressAsync("Control+z");
        (await text()).Should().Be("", "Ctrl+Z undoes the typing run");

        // REAL Ctrl+Y → redoes it.
        await page.Keyboard.PressAsync("Control+y");
        (await text()).Should().Be("Hello", "Ctrl+Y redoes the typing run");

        // REAL Ctrl+Shift+Z also redoes (after an undo).
        await page.Keyboard.PressAsync("Control+z");
        (await text()).Should().Be("");
        await page.Keyboard.PressAsync("Control+Shift+Z");
        (await text()).Should().Be("Hello", "Ctrl+Shift+Z redoes too");

        // Formatting is undoable through the same real Ctrl+Z.
        var boldState = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.setSelection({ blockId: 'p1', offset: 0 });
            for (let i = 0; i < 5; i++) host.moveCaret('ArrowRight', true);
            host.toggleMark('bold');
            return JSON.stringify({ activeAfter: host.isMarkActive('bold') });
        }");
        JsonDocument.Parse(boldState.GetString()!).RootElement.GetProperty("activeAfter").GetBoolean()
            .Should().BeTrue("bold applied to the selection");
        await page.Keyboard.PressAsync("Control+z");
        var activeAfterUndo = await page.EvaluateAsync<bool>(@"() => {
            const host = window.__host;
            host.setSelection({ blockId: 'p1', offset: 0 });
            for (let i = 0; i < 5; i++) host.moveCaret('ArrowRight', true);
            return host.isMarkActive('bold');
        }");
        activeAfterUndo.Should().BeFalse("Ctrl+Z reverts the bold mark (whole-model snapshot undo)");

        TestContext.WriteLine("R.4.6i undo/redo: type→Ctrl+Z→''→Ctrl+Y→'Hello'; bold→Ctrl+Z→unbold");
    }

    [TestMethod]
    public async Task R46b_ParagraphStyles_HeadingRendersLarger_AndOutline_Undoable()
    {
        var page = await OpenHarnessAsync();

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46b', body: { blocks: [
                { id: 'h', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'My Heading' }] } },
                { id: 'p', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r2', kind: 'text', text: 'Body paragraph.' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'h', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;

            const headSpan = () => Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /Heading/.test(s.textContent || ''));
            const sizeBefore = parseFloat(getComputedStyle(headSpan()).fontSize);

            host.setParagraphStyle('Heading1');
            const sizeAfter = parseFloat(getComputedStyle(headSpan()).fontSize);
            const outline = host.getOutline();
            return JSON.stringify({ sizeBefore, sizeAfter, style: host.getParagraphStyle(), outlineLen: outline.length, outlineText: outline[0] && outline[0].text, outlineLevel: outline[0] && outline[0].level });
        }");
        var r = JsonDocument.Parse(json.GetString()!).RootElement;
        var before = r.GetProperty("sizeBefore").GetDouble();
        var after = r.GetProperty("sizeAfter").GetDouble();
        after.Should().BeGreaterThan(before, "applying Heading 1 enlarges the rendered text");
        after.Should().BeApproximately(32, 1.0, "Heading 1 renders at ~32px");
        r.GetProperty("style").GetString().Should().Be("Heading1");
        r.GetProperty("outlineLen").GetInt32().Should().Be(1, "the heading appears in the document outline");
        r.GetProperty("outlineText").GetString().Should().Be("My Heading");
        r.GetProperty("outlineLevel").GetInt32().Should().Be(1);

        // REAL Ctrl+Z reverts the style.
        await page.Keyboard.PressAsync("Control+z");
        var afterUndo = await page.EvaluateAsync<JsonElement>(@"() => {
            const headSpan = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /Heading/.test(s.textContent || ''));
            return JSON.stringify({ size: parseFloat(getComputedStyle(headSpan).fontSize), style: window.__host.getParagraphStyle(), outlineLen: window.__host.getOutline().length });
        }");
        var au = JsonDocument.Parse(afterUndo.GetString()!).RootElement;
        au.GetProperty("size").GetDouble().Should().BeApproximately(before, 1.0, "Ctrl+Z restores the original size");
        au.GetProperty("style").GetString().Should().Be("Normal");
        au.GetProperty("outlineLen").GetInt32().Should().Be(0, "outline empty after undo");

        TestContext.WriteLine($"R.4.6b heading size {before}→{after}px, outline=1, undo→{au.GetProperty("size").GetDouble()}px");
    }

    [TestMethod]
    public async Task R46c_Table_InsertRendersGrid_TypeInCell_AddRowColumn_Undo()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46c', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: 'Intro' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 5 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
            window.__t = host.insertTable({ rows: 2, cols: 2 });
        }");

        // The table renders as a real grid (1 table container + 4 cell boxes).
        var renderJson = await page.EvaluateAsync<JsonElement>(@"() => JSON.stringify({
            tables: document.querySelectorAll('.tm-render-table').length,
            cells: document.querySelectorAll('.tm-render-table-cell').length,
            info: window.__host.getTableInfo(),
            caretBlock: window.__host.getCaret().blockId,
        })");
        var rj = JsonDocument.Parse(renderJson.GetString()!).RootElement;
        rj.GetProperty("tables").GetInt32().Should().Be(1, "the table renders");
        rj.GetProperty("cells").GetInt32().Should().Be(4, "2×2 = four cell boxes render");
        rj.GetProperty("info").GetProperty("rows").GetInt32().Should().Be(2);
        rj.GetProperty("info").GetProperty("cols").GetInt32().Should().Be(2);
        rj.GetProperty("caretBlock").GetString().Should().EndWith("-r0-c0-p", "caret drops into the first cell");

        // REAL keyboard typing goes into the focused cell.
        await page.Keyboard.TypeAsync("Cell");
        var cellText = await page.EvaluateAsync<string>(@"() => {
            const id = window.__t.tableId + '-r0-c0-p';
            const blk = document.querySelector('[data-render-block-id=""' + window.__t.tableId + '""]');
            return blk ? (blk.textContent || '') : '';
        }");
        cellText.Should().Contain("Cell", "typed text renders in the cell");

        // Structure ops + undo.
        var afterOps = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.addTableRow();
            host.addTableColumn();
            const grown = host.getTableInfo();
            host.undo(); // undo column
            host.undo(); // undo row
            const back = host.getTableInfo();
            return JSON.stringify({ grownRows: grown.rows, grownCols: grown.cols, backRows: back.rows, backCols: back.cols, cellsNow: document.querySelectorAll('.tm-render-table-cell').length });
        }");
        var ao = JsonDocument.Parse(afterOps.GetString()!).RootElement;
        ao.GetProperty("grownRows").GetInt32().Should().Be(3, "addTableRow grows to 3 rows");
        ao.GetProperty("grownCols").GetInt32().Should().Be(3, "addTableColumn grows to 3 cols");
        ao.GetProperty("backRows").GetInt32().Should().Be(2, "undo reverts the row");
        ao.GetProperty("backCols").GetInt32().Should().Be(2, "undo reverts the column");
        ao.GetProperty("cellsNow").GetInt32().Should().Be(4, "back to 4 rendered cells after undo");

        TestContext.WriteLine($"R.4.6c table 2x2 rendered (4 cells), typed in cell, grew to 3x3, undo→2x2");
    }

    [TestMethod]
    public async Task R46h_Hyperlink_AppliesToSelection_RendersHrefAndStyle_Undoable()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46h', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: 'Visit our site' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 10 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");

        // Select 'site' (offsets 10..14) with the REAL keyboard, then apply a link.
        for (int i = 0; i < 4; i++) await page.Keyboard.PressAsync("Shift+ArrowRight");
        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.applyLink('https://tempo.dev');
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => (s.textContent || '').trim() === 'site');
            const cs = span ? getComputedStyle(span) : null;
            return JSON.stringify({
                href: host.getLinkHref(),
                dataHref: span ? span.getAttribute('data-href') : null,
                role: span ? span.getAttribute('role') : null,
                underline: cs ? (cs.textDecorationLine || cs.textDecoration || '') : '',
                color: cs ? cs.color : '',
            });
        }");
        var r = JsonDocument.Parse(json.GetString()!).RootElement;
        r.GetProperty("href").GetString().Should().Be("https://tempo.dev", "the link href is set on the selection");
        r.GetProperty("dataHref").GetString().Should().Be("https://tempo.dev", "the rendered span carries data-href (real DOM → middle-click / a11y)");
        r.GetProperty("role").GetString().Should().Be("link", "the link span has role=link");
        r.GetProperty("underline").GetString().Should().Contain("underline", "links render underlined");
        r.GetProperty("color").GetString().Should().Be("rgb(5, 99, 193)", "links render in hyperlink blue");

        // Undo removes the link entirely.
        await page.Keyboard.PressAsync("Control+z");
        var afterUndo = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => (s.textContent || '').trim() === 'site');
            return JSON.stringify({ href: host.getLinkHref(), dataHref: span ? span.getAttribute('data-href') : null });
        }");
        var au = JsonDocument.Parse(afterUndo.GetString()!).RootElement;
        au.GetProperty("href").ValueKind.Should().Be(JsonValueKind.Null, "undo removes the link href");
        (au.GetProperty("dataHref").ValueKind == JsonValueKind.Null || au.GetProperty("dataHref").GetString() == null)
            .Should().BeTrue("the data-href attribute is gone after undo");

        TestContext.WriteLine("R.4.6h hyperlink: applied to 'site' → data-href + underline + blue; Ctrl+Z removes it");
    }

    [TestMethod]
    public async Task R46fr_FindReplace_HighlightsMatches_NavigatesAndReplacesAll()
    {
        var page = await OpenHarnessAsync();

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46fr', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: 'alpha beta alpha gamma alpha' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;

            const count = host.find('alpha');
            const matchEls = document.querySelectorAll('[data-testid=""core-engine-find-match""]').length;
            const currentEls = document.querySelectorAll('[data-testid=""core-engine-find-current""]').length;
            const idx0 = host.getFindState().index;
            host.findNext();
            const idx1 = host.getFindState().index;
            return JSON.stringify({ count, matchEls, currentEls, idx0, idx1 });
        }");
        var r = JsonDocument.Parse(json.GetString()!).RootElement;
        r.GetProperty("count").GetInt32().Should().Be(3, "three occurrences of 'alpha'");
        r.GetProperty("currentEls").GetInt32().Should().Be(1, "the active match is highlighted distinctly");
        r.GetProperty("matchEls").GetInt32().Should().Be(2, "the other two matches are highlighted");
        r.GetProperty("idx0").GetInt32().Should().Be(0);
        r.GetProperty("idx1").GetInt32().Should().Be(1, "findNext advances the active match");

        // Replace all + undo (single step), reading the rendered text.
        var text = () => page.EvaluateAsync<string>(@"() => { const b = document.querySelector('[data-render-block-id]'); return b ? (b.textContent || '') : ''; }");
        var replaced = await page.EvaluateAsync<int>("() => window.__host.replaceAll('alpha', 'X')");
        replaced.Should().Be(3);
        (await text()).Should().Be("X beta X gamma X", "all matches replaced in the rendered DOM");

        await page.Keyboard.PressAsync("Control+z");
        (await text()).Should().Be("alpha beta alpha gamma alpha", "replaceAll undoes in one step");

        TestContext.WriteLine("R.4.6h-2 find/replace: 3 highlights (1 current), findNext→idx1, replaceAll→'X beta X gamma X', Ctrl+Z reverts");
    }

    [TestMethod]
    public async Task R46f_TrackChanges_InsertUnderlined_DeleteStruck_AcceptResolves()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46f', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: 'Hello' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 5 }).attachInput();
            host.render();
            host.focusInput();
            host.setTrackChanges(true);
            window.__host = host;
        }");

        // Type tracked text with the REAL keyboard.
        await page.Keyboard.TypeAsync(" World");
        var ins = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /World/.test(s.textContent || ''));
            const cs = span ? getComputedStyle(span) : null;
            const text = document.querySelector('[data-render-block-id]').textContent || '';
            return JSON.stringify({
                text,
                revisions: host.getRevisions().length,
                kind: host.getRevisions()[0] && host.getRevisions()[0].kind,
                underline: cs ? (cs.textDecorationLine || cs.textDecoration || '') : '',
                color: cs ? cs.color : '',
            });
        }");
        var i = JsonDocument.Parse(ins.GetString()!).RootElement;
        i.GetProperty("text").GetString().Should().Be("Hello World", "tracked text is inserted into the model");
        i.GetProperty("revisions").GetInt32().Should().BeGreaterThan(0, "the insertion is tracked as a revision");
        i.GetProperty("kind").GetString().Should().Be("insertion");
        i.GetProperty("underline").GetString().Should().Contain("underline", "a tracked insertion renders underlined");
        i.GetProperty("color").GetString().Should().Be("rgb(27, 127, 59)", "tracked insertions render in the insertion color");

        // Accept all → text stays, marks resolved (no more underline, no revisions).
        var accepted = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.acceptAllRevisions();
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /World/.test(s.textContent || ''));
            const cs = span ? getComputedStyle(span) : null;
            return JSON.stringify({
                text: document.querySelector('[data-render-block-id]').textContent || '',
                hasRevisions: host.hasRevisions(),
                underline: cs ? (cs.textDecorationLine || cs.textDecoration || '') : '',
            });
        }");
        var a = JsonDocument.Parse(accepted.GetString()!).RootElement;
        a.GetProperty("text").GetString().Should().Be("Hello World", "accepting keeps the inserted text");
        a.GetProperty("hasRevisions").GetBoolean().Should().BeFalse("no revisions remain after accept");
        a.GetProperty("underline").GetString().Should().NotContain("underline", "accepted text is no longer marked");

        TestContext.WriteLine("R.4.6f track changes: tracked ' World' underlined+green, acceptAll → clean 'Hello World'");
    }

    [TestMethod]
    public async Task R46g_Comments_AnchorHighlights_ResolveClears_InBrowser()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r46g', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: 'Review this sentence' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");

        // Select 'Review' with the REAL keyboard and add a comment.
        for (int i = 0; i < 6; i++) await page.Keyboard.PressAsync("Shift+ArrowRight");
        var added = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            const id = host.addComment('Please rephrase', 'Alex');
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /Review/.test(s.textContent || ''));
            const cs = span ? getComputedStyle(span) : null;
            const comments = host.getComments();
            return JSON.stringify({
                id,
                count: comments.length,
                anchorText: comments[0] && comments[0].anchorText,
                author: comments[0] && comments[0].author,
                bg: cs ? cs.backgroundColor : '',
            });
        }");
        var a = JsonDocument.Parse(added.GetString()!).RootElement;
        a.GetProperty("id").GetString().Should().NotBeNullOrEmpty("addComment returns an id");
        a.GetProperty("count").GetInt32().Should().Be(1);
        a.GetProperty("anchorText").GetString().Should().Be("Review", "the comment is anchored to the selection");
        a.GetProperty("author").GetString().Should().Be("Alex");
        a.GetProperty("bg").GetString().Should().NotBe("rgba(0, 0, 0, 0)", "the commented text is highlighted");

        // Resolve → highlight clears, record stays as resolved.
        var resolved = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            host.resolveComment(host.getComments()[0].id);
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => /Review/.test(s.textContent || ''));
            const cs = span ? getComputedStyle(span) : null;
            return JSON.stringify({ resolved: host.getComments()[0].resolved, bg: cs ? cs.backgroundColor : '' });
        }");
        var rr = JsonDocument.Parse(resolved.GetString()!).RootElement;
        rr.GetProperty("resolved").GetBoolean().Should().BeTrue("the comment is marked resolved");
        rr.GetProperty("bg").GetString().Should().Be("rgba(0, 0, 0, 0)", "resolving clears the highlight");

        TestContext.WriteLine("R.4.6g comments: anchored 'Review' highlighted (Alex), resolve clears highlight");
    }

    [TestMethod]
    public async Task R46e_HeaderFooter_PageNumberField_ResolvesPerPage_InBrowser()
    {
        var page = await OpenHarnessAsync();

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const hf = M.coreEngine.headerFooter;
            const root = document.getElementById('harness-root');
            const blocks = [];
            for (let i = 0; i < 60; i++) {
                blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph',
                    runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph ' + i + ' lorem ipsum dolor sit amet consectetur adipiscing elit.' }] } });
            }
            const model = { documentId: 'r46e', body: { blocks } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 });
            host.render();
            host.setFooter([hf.textRun('Page '), hf.pageNumberField(), hf.textRun(' of '), hf.pageCountField()]);
            host.setHeader('Confidential');

            const footers = Array.from(document.querySelectorAll('[data-testid=""document-page-footer""]'))
                .map((el) => ({ page: el.getAttribute('data-render-page-index'), text: (el.textContent || '').trim() }))
                .sort((a, b) => Number(a.page) - Number(b.page));
            const headers = Array.from(document.querySelectorAll('[data-testid=""document-page-header""]'))
                .map((el) => (el.textContent || '').trim());
            return JSON.stringify({ pages: host.getLayout().pages.length, footers, headerHasText: headers.some((t) => /Confidential/.test(t)) });
        }");
        var r = JsonDocument.Parse(json.GetString()!).RootElement;
        var pages = r.GetProperty("pages").GetInt32();
        pages.Should().BeGreaterThan(1, "the document spans multiple pages");
        var footers = r.GetProperty("footers").EnumerateArray().ToList();
        footers.Count.Should().Be(pages, "a footer renders on every page");
        footers[0].GetProperty("text").GetString().Should().Be($"Page 1 of {pages}", "page 1 footer field resolves to 1");
        footers[1].GetProperty("text").GetString().Should().Be($"Page 2 of {pages}", "page 2 footer field resolves to 2");
        r.GetProperty("headerHasText").GetBoolean().Should().BeTrue("the header text renders");

        TestContext.WriteLine($"R.4.6e header/footer: {pages} pages, footer 'Page N of {pages}' per page, header 'Confidential'");
    }

    [TestMethod]
    public async Task R46i2_LayoutCache_ReusesAcrossScrollRenders_AndIsFaster()
    {
        var page = await OpenHarnessAsync();

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const blocks = [];
            for (let i = 0; i < 150; i++) {
                blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph',
                    runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph ' + i + ' — the quick brown fox jumps over the lazy dog repeatedly to fill the line.' }] } });
            }
            const model = { documentId: 'r46i2', body: { blocks } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 });

            const t0 = performance.now();
            host.render();
            const coldMs = performance.now() - t0;
            const computesAfterCold = host.getLayoutComputeCount();

            // 5 scroll re-renders (no model change) — should hit the layout cache.
            let scrollMs = 0;
            for (let i = 0; i < 5; i++) {
                host.setViewport({ scrollTop: i * 700, height: 900, overscanPages: 1 });
                const s = performance.now();
                host.render();
                scrollMs += performance.now() - s;
            }
            const computesAfterScroll = host.getLayoutComputeCount();
            const avgScrollMs = scrollMs / 5;

            return JSON.stringify({ coldMs, computesAfterCold, computesAfterScroll, avgScrollMs, pages: host.getLayout().pages.length });
        }");
        var r = JsonDocument.Parse(json.GetString()!).RootElement;

        r.GetProperty("computesAfterCold").GetInt32().Should().Be(1, "the cold first render lays out once");
        r.GetProperty("computesAfterScroll").GetInt32().Should().Be(1, "5 scroll re-renders all reuse the cached layout (no relayout)");
        r.GetProperty("pages").GetInt32().Should().BeGreaterThan(1, "150 paragraphs span multiple pages");

        var coldMs = r.GetProperty("coldMs").GetDouble();
        var avgScrollMs = r.GetProperty("avgScrollMs").GetDouble();
        avgScrollMs.Should().BeLessThan(coldMs, "a cached scroll re-render is faster than the cold full-layout paint");
        TestContext.WriteLine($"R.4.6i-2 layout cache: cold {coldMs.ToString("F1", CultureInfo.InvariantCulture)}ms, " +
            $"avg cached scroll re-render {avgScrollMs.ToString("F1", CultureInfo.InvariantCulture)}ms (1 layout compute for 6 renders)");
    }

    [TestMethod]
    public async Task R47_Accessibility_DocumentRole_HeadingRoles_AndLiveRegion()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r47', body: { blocks: [
                { id: 'h', type: 'paragraph', content: { type: 'paragraph', styleName: 'Heading1', headingLevel: 1, runs: [{ id: 'rh', kind: 'text', text: 'Chapter Title' }] } },
                { id: 'p', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'rp', kind: 'text', text: 'Body paragraph text.' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({
                doc: document,
                ariaLabel: 'My Document',
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            host.mount(root).setModel(model).setSelection({ blockId: 'h', offset: 0 }).attachInput();
            host.render();
            host.focusInput();
            window.__host = host;
        }");

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const host = window.__host;
            const root = document.getElementById('harness-root');
            const headingEl = root.querySelector('[role=""heading""]');
            const ta = host.getInputSurface().element;
            return JSON.stringify({
                rootRole: root.getAttribute('role'),
                rootLabel: root.getAttribute('aria-label'),
                rootRoleDesc: root.getAttribute('aria-roledescription'),
                headingRole: headingEl ? headingEl.getAttribute('role') : null,
                headingLevel: headingEl ? headingEl.getAttribute('aria-level') : null,
                headingText: headingEl ? (headingEl.textContent || '') : null,
                textboxRole: ta.getAttribute('role'),
                multiline: ta.getAttribute('aria-multiline'),
                ariaHidden: ta.getAttribute('aria-hidden'),
                liveRole: host.getLiveRegionElement() ? host.getLiveRegionElement().getAttribute('aria-live') : null,
                liveText: host.getLiveRegionText(),
            });
        }");
        var r = JsonDocument.Parse(json.GetString()!).RootElement;

        r.GetProperty("rootRole").GetString().Should().Be("document", "the editor root exposes a document role");
        r.GetProperty("rootLabel").GetString().Should().Be("My Document");
        r.GetProperty("rootRoleDesc").GetString().Should().Be("rich text editor");
        r.GetProperty("headingRole").GetString().Should().Be("heading", "a heading paragraph exposes role=heading");
        r.GetProperty("headingLevel").GetString().Should().Be("1", "with the correct aria-level");
        r.GetProperty("headingText").GetString().Should().Contain("Chapter", "the heading text is real DOM text a screen reader can read");
        r.GetProperty("textboxRole").GetString().Should().Be("textbox", "the input surface is an accessible multiline textbox");
        r.GetProperty("multiline").GetString().Should().Be("true");
        (r.GetProperty("ariaHidden").ValueKind == JsonValueKind.Null || r.GetProperty("ariaHidden").GetString() != "true")
            .Should().BeTrue("the textbox is not aria-hidden");
        r.GetProperty("liveRole").GetString().Should().Be("polite", "there is a polite live region");
        r.GetProperty("liveText").GetString().Should().Be("Heading level 1, Chapter Title", "the live region announces the caret's heading context");

        // Moving the caret to the body paragraph updates the announcement.
        await page.Keyboard.PressAsync("ArrowDown");
        var liveAfter = await page.EvaluateAsync<string>("() => window.__host.getLiveRegionText()");
        liveAfter.Should().Be("Body paragraph text.", "the live region updates as the caret moves into the paragraph");

        TestContext.WriteLine("R.4.7 a11y: root=document, heading role+level, accessible textbox, live region announces caret context");
    }

    [TestMethod]
    public async Task R48_CoreEditorBridge_MountsTypesCommandsAndTracksDirty_InBrowser()
    {
        var page = await OpenHarnessAsync();

        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const model = { documentId: 'r48', version: 0, body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: 'Hello world' }] } },
            ] } };
            window.__editor = M.coreEngine.createCoreEditor({
                root, doc: document, model, ariaLabel: 'Bridge Doc',
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
                autoFocus: true,
            });
        }");

        // Type with the REAL keyboard at the end → goes through the facade's attached input.
        await page.EvaluateAsync("() => window.__editor.getHost().setSelection({ blockId: 'p1', offset: 11 })");
        await page.Keyboard.TypeAsync("!");
        var afterType = await page.EvaluateAsync<JsonElement>(@"() => JSON.stringify({
            dirty: window.__editor.isDirty(),
            text: document.querySelector('[data-render-block-id]').textContent,
        })");
        var at = JsonDocument.Parse(afterType.GetString()!).RootElement;
        at.GetProperty("text").GetString().Should().Be("Hello world!", "typing routes through the bridge into the model + DOM");
        at.GetProperty("dirty").GetBoolean().Should().BeTrue("editing marks the editor dirty");

        // Select "Hello" (offsets 0..5) and run the bold command id; verify computed style + queryCommand.
        var boldJson = await page.EvaluateAsync<JsonElement>(@"() => {
            const ed = window.__editor;
            const host = ed.getHost();
            host.setSelection({ blockId: 'p1', offset: 0 });
            for (let i = 0; i < 5; i++) host.moveCaret('ArrowRight', true);
            ed.execCommand('bold');
            const span = Array.from(document.querySelectorAll('.tm-render-segment')).find((s) => (s.textContent || '').trim() === 'Hello');
            return JSON.stringify({
                pressed: ed.queryCommand('bold'),
                weight: span ? getComputedStyle(span).fontWeight : null,
            });
        }");
        var b = JsonDocument.Parse(boldJson.GetString()!).RootElement;
        b.GetProperty("pressed").GetBoolean().Should().BeTrue("queryCommand reflects the toggled bold state");
        b.GetProperty("weight").GetString().Should().BeOneOf("700", "bold", "execCommand('bold') renders bold");

        // markSaved clears dirty; undo via the facade works.
        var saved = await page.EvaluateAsync<JsonElement>(@"() => {
            const ed = window.__editor;
            ed.markSaved();
            const dirtyAfterSave = ed.isDirty();
            ed.execCommand('undo');
            return JSON.stringify({ dirtyAfterSave, pressedAfterUndo: ed.queryCommand('bold'), dirtyAfterUndo: ed.isDirty() });
        }");
        var s = JsonDocument.Parse(saved.GetString()!).RootElement;
        s.GetProperty("dirtyAfterSave").GetBoolean().Should().BeFalse("markSaved clears the dirty flag");
        s.GetProperty("pressedAfterUndo").GetBoolean().Should().BeFalse("undo via the facade reverts bold");

        TestContext.WriteLine("R.4.8 bridge facade: mount+type→dirty, execCommand(bold)+queryCommand, markSaved clears dirty, undo");
    }

    [TestMethod]
    public async Task R40_FontMetrics_MatchBrowserTextMeasurement_WithinOnePixel()
    {
        var page = await OpenHarnessAsync();

        var json = await page.EvaluateAsync<JsonElement>(@"() => {
            const M = window.tmDocumentEditorModules;
            const svc = M.layout.fontMetrics.createFontMetricsService({ doc: document });

            // A measuring DOM span with the same font as the metrics request.
            const span = document.createElement('span');
            span.style.position = 'absolute';
            span.style.visibility = 'hidden';
            span.style.whiteSpace = 'pre';
            span.style.font = '16px Arial';
            document.body.appendChild(span);

            const cases = [
                { text: 'Hello world', fontFamily: 'Arial', fontSize: 16 },
                { text: 'The quick brown fox jumps over the lazy dog', fontFamily: 'Arial', fontSize: 16 },
                { text: 'WWWWWWMMMMMM', fontFamily: 'Arial', fontSize: 24 },
                { text: 'iiilll.,;:', fontFamily: 'Arial', fontSize: 12 },
            ];
            const results = cases.map((c) => {
                span.style.font = c.fontSize + 'px ' + c.fontFamily;
                span.textContent = c.text;
                const domWidth = span.getBoundingClientRect().width;
                const metricWidth = svc.measureRun(c).width;
                return { text: c.text, fontSize: c.fontSize, domWidth, metricWidth, diff: Math.abs(domWidth - metricWidth) };
            });
            document.body.removeChild(span);
            return JSON.stringify({ usingReal: svc.isUsingRealMetrics(), results });
        }");

        var parsed = JsonDocument.Parse(json.GetString()!).RootElement;
        parsed.GetProperty("usingReal").GetBoolean().Should().BeTrue("real canvas metrics should be active in the browser");

        foreach (var r in parsed.GetProperty("results").EnumerateArray())
        {
            var text = r.GetProperty("text").GetString();
            var domWidth = r.GetProperty("domWidth").GetDouble();
            var metricWidth = r.GetProperty("metricWidth").GetDouble();
            var diff = r.GetProperty("diff").GetDouble();
            TestContext.WriteLine($"\"{text}\": dom={domWidth:F2} metric={metricWidth:F2} diff={diff:F3}");
            diff.Should().BeLessThan(1.0,
                $"font-metrics width for \"{text}\" should match the browser's real measurement within 1px");
        }
    }

    // R.4.8 3c — core-engine perf parity: first-paint + typing latency at 30/100/500 paragraphs,
    // measured with wall-clock performance.now() (the same Playwright TypeAsync methodology as the
    // legacy DocumentEditorPhaseABCPerformanceE2ETests, so the numbers are directionally comparable).
    [TestMethod]
    public async Task R64_PerfParity_CoreEngine_FirstPaintAndTypingLatency()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);

        foreach (var n in new[] { 30, 100, 500 })
        {
            // Build an N-paragraph document, mount, and measure first-paint (synchronous
            // layout + render of the cold document).
            var firstPaint = await page.EvaluateAsync<double>(@"(n) => {
                if (window.__host) { try { window.__host.destroy(); } catch (e) { /* */ } }
                const M = window.tmDocumentEditorModules;
                const root = document.getElementById('harness-root');
                const blocks = [];
                for (let i = 0; i < n; i++) {
                    blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Performance paragraph ' + i + ' contents.' }] } });
                }
                const model = { documentId: 'perf', body: { blocks } };
                const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
                host.mount(root).setModel(model).setSelection({ blockId: 'p5', offset: 0 }).attachInput();
                const t0 = performance.now();
                host.render();
                const fp = performance.now() - t0;
                host.focusInput();
                window.__host = host;
                return fp;
            }", n);

            // Type a few characters at the caret (p5) and measure wall-clock per character until
            // the DOM reflects them. (10 chars keeps even the 500p case under the timeout — the
            // engine currently re-lays-out + re-renders the WHOLE document on every keystroke.)
            const int chars = 10;
            await page.EvaluateAsync("() => { window.__t0 = performance.now(); }");
            await page.Keyboard.TypeAsync(new string('x', chars), new() { Delay = 0 });
            await page.WaitForFunctionAsync(
                "() => { const b = document.querySelector('[data-render-block-id=\"p5\"]'); return b && (b.textContent || '').indexOf('xxxxxxxxxx') !== -1; }",
                null, new PageWaitForFunctionOptions { Timeout = 60000 });
            var typingMs = await page.EvaluateAsync<double>("() => performance.now() - window.__t0");

            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "R.4.8 3c CORE perf [{0}p]: first-paint={1:F1}ms | typing={2:F1}ms/char ({3} chars, {4:F0}ms total)",
                n, firstPaint, typingMs / chars, chars, typingMs));

            // Recording test — generous bounds so all three counts are captured. The real gap
            // (typing latency vs legacy) is reported, not gated, here.
            firstPaint.Should().BeLessThan(n <= 100 ? 2500 : 10000,
                $"core-engine cold first-paint for {n} paragraphs should stay within a sane bound");
            typingMs.Should().BeLessThan(55000, $"typing {chars} chars into a {n}-paragraph doc should at least complete");
        }
    }

    // R.4.8 3c — TRUE per-keystroke main-thread cost (no Playwright round-trip). We dispatch a
    // synthetic `beforeinput` straight at the off-screen input surface and time the synchronous
    // handler (edit-model mutation + computeLayout + full re-render). This is the work that blocks
    // the frame — the number that must drop under ~16ms (and stay flat vs doc size) for a
    // Word/Google-Docs feel. (Legacy does ~0 here because contenteditable types natively.)
    [TestMethod]
    public async Task R65_PerfParity_CoreEngine_TrueSingleKeystrokeMainThreadCost()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);

        foreach (var n in new[] { 30, 100, 500 })
        {
            var json = await page.EvaluateAsync<string>(@"(n) => {
                if (window.__host) { try { window.__host.destroy(); } catch (e) { /* */ } }
                const M = window.tmDocumentEditorModules;
                const root = document.getElementById('harness-root');
                const blocks = [];
                for (let i = 0; i < n; i++) {
                    blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Performance paragraph ' + i + ' contents.' }] } });
                }
                const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
                host.mount(root).setModel({ documentId: 'perf', body: { blocks } }).setSelection({ blockId: 'p5', offset: 0 }).attachInput();
                host.render();
                host.focusInput();
                window.__host = host;

                const surface = host.getInputSurface().element;
                const fire = () => {
                    const ev = new InputEvent('beforeinput', { inputType: 'insertText', data: 'x', bubbles: true, cancelable: true });
                    const t0 = performance.now();
                    surface.dispatchEvent(ev); // onBeforeInput → insertText → edit + render, all synchronous
                    return performance.now() - t0;
                };

                for (let w = 0; w < 5; w++) fire();      // warm up (JIT, caches)
                const samples = [];
                for (let k = 0; k < 25; k++) samples.push(fire());
                samples.sort((a, b) => a - b);
                const mean = samples.reduce((s, v) => s + v, 0) / samples.length;
                const p50 = samples[Math.floor(samples.length * 0.5)];
                const p95 = samples[Math.floor(samples.length * 0.95)];
                return JSON.stringify({ mean, p50, p95, min: samples[0], max: samples[samples.length - 1] });
            }", n);

            var r = JsonDocument.Parse(json).RootElement;
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "R.4.8 3c CORE true keystroke main-thread cost [{0}p]: mean={1:F2}ms p50={2:F2}ms p95={3:F2}ms (min={4:F2} max={5:F2})",
                n, r.GetProperty("mean").GetDouble(), r.GetProperty("p50").GetDouble(), r.GetProperty("p95").GetDouble(),
                r.GetProperty("min").GetDouble(), r.GetProperty("max").GetDouble()));

            // R.4.9.9 perf gate — the median keystroke must fit in one 60Hz frame (16.7ms) AND stay
            // flat across document size (O(1) incremental render). Median is used (robust to CI GC spikes).
            r.GetProperty("p50").GetDouble().Should().BeLessThan(16.0,
                $"R.4.9 — median keystroke at {n}p must fit in a frame budget (incremental render-on-edit)");
        }
    }

    // R.4.9.3 (de-risk) — does `engine.layoutParagraph(block, frameFromCache)` reproduce EXACTLY
    // what `layoutDocument` produced for that block? If yes, single-block incremental re-layout is
    // sound. Compares segment text+x+width and height for an UNEDITED block.
    [TestMethod]
    public async Task R67_IncrementalLayout_LayoutParagraphReproducesCachedBlock()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root');
            const blocks = [];
            for (let i = 0; i < 30; i++) {
                blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph ' + i + ' with quite a few words so that the line is long enough to wrap across two lines in the page body for sure.' }] } });
            }
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
            host.mount(root).setModel({ documentId: 'fr', body: { blocks } });
            host.render();
            const layout = host.getLayout();
            const model = host.getSnapshot().model;
            const engine = host.getEngine();
            const sig = (bl) => (bl.segments || []).map(s => (s.text || '') + '@' + Math.round((s.rect && s.rect.x) || 0) + ':' + Math.round((s.rect && s.rect.width) || 0)).join('|');
            const out = [];
            ['p3', 'p10', 'p20'].forEach((id) => {
                const cached = layout.blocks.find(b => b.blockId === id);
                const blk = model.body.blocks.find(b => b.id === id);
                const fresh = engine.layoutParagraph(blk, { x: cached.rect.x, y: cached.rect.y, width: cached.rect.width });
                out.push({ id, cachedLines: (cached.lines || []).length, freshLines: (fresh.lines || []).length,
                    cachedH: Math.round(cached.rect.height), freshH: Math.round(fresh.rect.height),
                    match: sig(cached) === sig(fresh), cachedSig: sig(cached).slice(0, 80), freshSig: sig(fresh).slice(0, 80) });
            });
            return JSON.stringify(out);
        }");

        var arr = JsonDocument.Parse(json).RootElement;
        foreach (var e in arr.EnumerateArray())
        {
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "R.4.9.3 frame-probe [{0}]: match={1} lines {2}/{3} height {4}/{5}\n  cached: {6}\n  fresh:  {7}",
                e.GetProperty("id").GetString(), e.GetProperty("match").GetBoolean(),
                e.GetProperty("cachedLines").GetInt32(), e.GetProperty("freshLines").GetInt32(),
                e.GetProperty("cachedH").GetInt32(), e.GetProperty("freshH").GetInt32(),
                e.GetProperty("cachedSig").GetString(), e.GetProperty("freshSig").GetString()));
        }
        foreach (var e in arr.EnumerateArray())
        {
            e.GetProperty("match").GetBoolean().Should().BeTrue(
                $"layoutParagraph({e.GetProperty("id").GetString()}) must reproduce the cached block layout for incremental re-layout to be sound");
        }
    }

    // R.4.9.8 — golden correctness gate: an incremental keystroke must produce a layout that is
    // byte-identical to a FULL layout of the same resulting model. Also asserts the incremental
    // fast path is actually taken for a plain keystroke, and that a structural edit (Enter) falls
    // back to full render.
    [TestMethod]
    public async Task R68_IncrementalLayout_GoldenMatchesFullRender()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const mkBlocks = () => { const b = []; for (let i = 0; i < 30; i++) b.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph ' + i + ' content here.' }] } }); return b; };
            const segSig = (bl) => (bl.segments || []).map(s => (s.text || '') + '@' + Math.round((s.rect && s.rect.x) || 0) + ',' + Math.round((s.rect && s.rect.y) || 0) + ':' + Math.round((s.rect && s.rect.width) || 0)).join('|');
            const laySig = (lay) => (lay.blocks || []).map(bl => bl.blockId + '#y' + Math.round((bl.rect && bl.rect.y) || 0) + 'h' + Math.round((bl.rect && bl.rect.height) || 0) + '[' + segSig(bl) + ']').join('||');
            const ensureRoot = (id) => { let r = document.getElementById(id); if (!r) { r = document.createElement('div'); r.id = id; r.style.position = 'relative'; document.body.appendChild(r); } r.innerHTML = ''; return r; };

            // Host A — real keystroke through the incremental path.
            const rootA = ensureRoot('harness-root');
            const A = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            A.mount(rootA).setModel({ documentId: 'a', body: { blocks: mkBlocks() } }).setSelection({ blockId: 'p10', offset: 0 }).attachInput();
            A.render();
            A.focusInput();
            const surface = A.getInputSurface().element;
            surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'X', bubbles: true, cancelable: true }));
            const usedIncremental = !!A.getLastRenderTimings().incremental;
            const bail = A.getLastIncrementalBail();
            const aModel = JSON.parse(JSON.stringify(A.getSnapshot().model));
            const aSig = laySig(A.getLayout());
            const p10text = (A.getLayout().blocks.find(b => b.blockId === 'p10').segments || []).map(s => s.text).join('');

            // Host B — full layout of the SAME resulting model (the golden reference).
            const rootB = ensureRoot('golden-root');
            const B = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            B.mount(rootB).setModel(aModel);
            B.render();
            const bSig = laySig(B.getLayout());

            // Host C — a structural edit (Enter) must fall back to a full render.
            const rootC = ensureRoot('struct-root');
            const C = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            C.mount(rootC).setModel({ documentId: 'c', body: { blocks: mkBlocks() } }).setSelection({ blockId: 'p10', offset: 3 }).attachInput();
            C.render(); C.focusInput();
            C.getInputSurface().element.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertParagraph', bubbles: true, cancelable: true }));
            const enterIncremental = !!C.getLastRenderTimings().incremental;

            let firstDiff = null;
            for (let k = 0; k < Math.max(aSig.length, bSig.length); k++) { if (aSig[k] !== bSig[k]) { firstDiff = { pos: k, a: aSig.slice(k, k + 70), b: bSig.slice(k, k + 70) }; break; } }
            return JSON.stringify({ usedIncremental, bail, match: aSig === bSig, p10text, enterIncremental, firstDiff });
        }");

        var r = JsonDocument.Parse(json).RootElement;
        TestContext.WriteLine($"R.4.9.8 golden: usedIncremental={r.GetProperty("usedIncremental").GetBoolean()}, bail={(r.TryGetProperty("bail", out var bv) ? bv.ToString() : "?")}, match={r.GetProperty("match").GetBoolean()}, p10='{r.GetProperty("p10text").GetString()}', enterIncremental={r.GetProperty("enterIncremental").GetBoolean()}");
        if (r.TryGetProperty("firstDiff", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            TestContext.WriteLine($"  firstDiff@{d.GetProperty("pos").GetInt32()}:\n  A: {d.GetProperty("a").GetString()}\n  B: {d.GetProperty("b").GetString()}");
        }

        r.GetProperty("usedIncremental").GetBoolean().Should().BeTrue("a plain keystroke must take the incremental fast path");
        r.GetProperty("p10text").GetString().Should().StartWith("X", "the typed character is in the re-laid-out block");
        r.GetProperty("match").GetBoolean().Should().BeTrue("the incremental layout must be byte-identical to a full render of the same model");
        r.GetProperty("enterIncremental").GetBoolean().Should().BeFalse("a structural edit (Enter) must fall back to a full render");
    }

    // R.4.9.3b — profile the INCREMENTAL per-keystroke path: where does the remaining O(N) go
    // (viewLayout vs cheap snapshot vs renderer fragment rebuild)?
    [TestMethod]
    public async Task R69_PerfProfile_IncrementalPathBreakdown()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        foreach (var n in new[] { 30, 100, 500 })
        {
            var json = await page.EvaluateAsync<string>(@"(n) => {
                if (window.__host) { try { window.__host.destroy(); } catch (e) { /* */ } }
                const M = window.tmDocumentEditorModules;
                const root = document.getElementById('harness-root');
                const blocks = [];
                for (let i = 0; i < n; i++) blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Performance paragraph ' + i + ' contents.' }] } });
                const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
                host.mount(root).setModel({ documentId: 'perf', body: { blocks } }).setSelection({ blockId: 'p5', offset: 0 }).attachInput();
                host.render(); host.focusInput();
                const surface = host.getInputSurface().element;
                const fire = () => surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'x', bubbles: true, cancelable: true }));
                for (let w = 0; w < 5; w++) fire();
                const acc = {}; const K = 15;
                for (let k = 0; k < K; k++) { fire(); const t = host.getLastRenderTimings(); for (const key in t) if (typeof t[key] === 'number') acc[key] = (acc[key] || 0) + t[key]; }
                for (const key in acc) acc[key] = acc[key] / K;
                acc.incremental = !!host.getLastRenderTimings().incremental;
                return JSON.stringify(acc);
            }", n);
            var t = JsonDocument.Parse(json).RootElement;
            double G(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "R.4.9.3b incremental [{0}p]: total={1:F1}ms = viewLayout {2:F1} + snapshot {3:F1} + renderer {4:F1} + overlays {5:F1} (incremental={6})",
                n, G("totalMs"), G("viewLayoutMs"), G("snapshotMs"), G("rendererMs"), G("overlaysMs"),
                t.TryGetProperty("incremental", out var iv) && iv.GetBoolean()));
        }
    }

    // R.4.9.4 — golden gate for the Y-REFLOW path: a keystroke that wraps a line (height change)
    // must (a) take the incremental reflow path (not fall back), (b) push the following blocks down,
    // and (c) produce a layout byte-identical to a full render of the same model.
    [TestMethod]
    public async Task R70_IncrementalReflow_WrapKeystroke_GoldenAndShiftsFollowingBlocks()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            // 30 short paragraphs fit on ONE page → a same-page reflow (no repagination).
            const mkBlocks = () => { const b = []; for (let i = 0; i < 30; i++) b.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Para ' + i + '.' }] } }); return b; };
            const segSig = (bl) => (bl.segments || []).map(s => (s.text || '') + '@' + Math.round((s.rect && s.rect.x) || 0) + ',' + Math.round((s.rect && s.rect.y) || 0)).join('|');
            const laySig = (lay) => (lay.blocks || []).map(bl => bl.blockId + '#y' + Math.round((bl.rect && bl.rect.y) || 0) + 'h' + Math.round((bl.rect && bl.rect.height) || 0) + '[' + segSig(bl) + ']').join('||');
            const ensureRoot = (id) => { let r = document.getElementById(id); if (!r) { r = document.createElement('div'); r.id = id; r.style.position = 'relative'; document.body.appendChild(r); } r.innerHTML = ''; return r; };

            const rootA = ensureRoot('harness-root');
            const A = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            A.mount(rootA).setModel({ documentId: 'a', body: { blocks: mkBlocks() } }).setSelection({ blockId: 'p10', offset: 6 }).attachInput();
            A.render(); A.focusInput();
            const surface = A.getInputSurface().element;
            const p10 = () => A.getLayout().blocks.find(b => b.blockId === 'p10');
            const p11y = () => Math.round((A.getLayout().blocks.find(b => b.blockId === 'p11').rect || {}).y || 0);

            const startLines = (p10().lines || []).length;
            const p11yBefore = p11y();
            // Type a long word one char at a time until p10 wraps to a new line.
            let wrapWasIncremental = null, wrapBail = null, lines = startLines, typed = 0;
            while (lines <= startLines && typed < 200) {
                surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'x', bubbles: true, cancelable: true }));
                typed++;
                lines = (p10().lines || []).length;
                if (lines > startLines) { wrapWasIncremental = !!A.getLastRenderTimings().incremental; wrapBail = A.getLastIncrementalBail(); }
            }
            const p11yAfter = p11y();
            const aModel = JSON.parse(JSON.stringify(A.getSnapshot().model));
            const aSig = laySig(A.getLayout());

            // Golden — full render of the resulting model.
            const rootB = ensureRoot('golden-root');
            const B = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            B.mount(rootB).setModel(aModel); B.render();
            const bSig = laySig(B.getLayout());

            let firstDiff = null;
            for (let k = 0; k < Math.max(aSig.length, bSig.length); k++) { if (aSig[k] !== bSig[k]) { firstDiff = { pos: k, a: aSig.slice(k, k + 70), b: bSig.slice(k, k + 70) }; break; } }
            return JSON.stringify({ startLines, endLines: lines, typed, wrapWasIncremental, wrapBail, p11yBefore, p11yAfter, match: aSig === bSig, firstDiff });
        }");

        var r = JsonDocument.Parse(json).RootElement;
        TestContext.WriteLine($"R.4.9.4 reflow: lines {r.GetProperty("startLines").GetInt32()}→{r.GetProperty("endLines").GetInt32()} after {r.GetProperty("typed").GetInt32()} chars, wrapIncremental={r.GetProperty("wrapWasIncremental")}, wrapBail={r.GetProperty("wrapBail")}, p11.y {r.GetProperty("p11yBefore").GetInt32()}→{r.GetProperty("p11yAfter").GetInt32()}, match={r.GetProperty("match").GetBoolean()}");
        if (r.TryGetProperty("firstDiff", out var d) && d.ValueKind == JsonValueKind.Object)
            TestContext.WriteLine($"  firstDiff@{d.GetProperty("pos").GetInt32()}: A={d.GetProperty("a").GetString()} | B={d.GetProperty("b").GetString()}");

        r.GetProperty("endLines").GetInt32().Should().BeGreaterThan(r.GetProperty("startLines").GetInt32(), "the typing wrapped p10 onto another line");
        r.GetProperty("wrapWasIncremental").GetBoolean().Should().BeTrue("the wrap keystroke must take the incremental Y-reflow path, not a full render");
        r.GetProperty("p11yAfter").GetInt32().Should().BeGreaterThan(r.GetProperty("p11yBefore").GetInt32(), "the following block (p11) shifted down by the reflow");
        r.GetProperty("match").GetBoolean().Should().BeTrue("the reflowed layout must be byte-identical to a full render of the same model");
    }

    // R.4.9.10 — cross-page repagination golden gate: growing a block at the top of a multi-page
    // document pushes later blocks ACROSS a page boundary. The incremental path must repaginate
    // (reuse cached block layouts, reassign pages) and produce a layout byte-identical to a full
    // render — without falling back to a full layoutDocument.
    [TestMethod]
    public async Task R71_IncrementalRepagination_CrossPage_GoldenMatchesFullRender()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            // 80 short paragraphs → multiple pages.
            const mkBlocks = () => { const b = []; for (let i = 0; i < 80; i++) b.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Para ' + i + '.' }] } }); return b; };
            const segSig = (bl) => (bl.segments || []).map(s => (s.text || '') + '@' + Math.round((s.rect && s.rect.x) || 0) + ',' + Math.round((s.rect && s.rect.y) || 0)).join('|');
            const laySig = (lay) => (lay.blocks || []).map(bl => bl.blockId + '#p' + (bl.pageIndex || 0) + 'y' + Math.round((bl.rect && bl.rect.y) || 0) + 'h' + Math.round((bl.rect && bl.rect.height) || 0) + '[' + segSig(bl) + ']').join('||');
            const ensureRoot = (id) => { let r = document.getElementById(id); if (!r) { r = document.createElement('div'); r.id = id; r.style.position = 'relative'; document.body.appendChild(r); } r.innerHTML = ''; return r; };
            const page0count = (host) => host.getLayout().blocks.filter(b => (b.pageIndex || 0) === 0).length;
            const pageCount = (host) => host.getLayout().pages.length;

            const rootA = ensureRoot('harness-root');
            const A = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            A.mount(rootA).setModel({ documentId: 'a', body: { blocks: mkBlocks() } }).setSelection({ blockId: 'p0', offset: 6 }).attachInput();
            A.render(); A.focusInput();
            const surface = A.getInputSurface().element;
            const startPages = pageCount(A), startP0 = page0count(A);

            // Type a long run into p0 → it grows several lines → tail of page 0 crosses onto page 1.
            let incremental = 0, fallback = 0;
            for (let k = 0; k < 250; k++) {
                surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'x', bubbles: true, cancelable: true }));
                if (A.getLastRenderTimings().incremental) incremental++; else fallback++;
            }
            const endPages = pageCount(A), endP0 = page0count(A);
            const aModel = JSON.parse(JSON.stringify(A.getSnapshot().model));
            const aSig = laySig(A.getLayout());

            const rootB = ensureRoot('golden-root');
            const B = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            B.mount(rootB).setModel(aModel); B.render();
            const bSig = laySig(B.getLayout());

            let firstDiff = null;
            for (let k = 0; k < Math.max(aSig.length, bSig.length); k++) { if (aSig[k] !== bSig[k]) { firstDiff = { pos: k, a: aSig.slice(k, k + 80), b: bSig.slice(k, k + 80) }; break; } }
            return JSON.stringify({ startPages, endPages, startP0, endP0, incremental, fallback, match: aSig === bSig, firstDiff });
        }");

        var r = JsonDocument.Parse(json).RootElement;
        TestContext.WriteLine($"R.4.9.10 cross-page: pages {r.GetProperty("startPages").GetInt32()}→{r.GetProperty("endPages").GetInt32()}, page0 blocks {r.GetProperty("startP0").GetInt32()}→{r.GetProperty("endP0").GetInt32()}, incremental={r.GetProperty("incremental").GetInt32()} fallback={r.GetProperty("fallback").GetInt32()}, match={r.GetProperty("match").GetBoolean()}");
        if (r.TryGetProperty("firstDiff", out var d) && d.ValueKind == JsonValueKind.Object)
            TestContext.WriteLine($"  firstDiff@{d.GetProperty("pos").GetInt32()}: A={d.GetProperty("a").GetString()} | B={d.GetProperty("b").GetString()}");

        r.GetProperty("endP0").GetInt32().Should().BeLessThan(r.GetProperty("startP0").GetInt32(), "growing p0 pushed blocks off page 0 (a page boundary was crossed)");
        r.GetProperty("incremental").GetInt32().Should().BeGreaterThan(200, "the vast majority of keystrokes (incl. repagination) stay on the incremental path");
        r.GetProperty("match").GetBoolean().Should().BeTrue("the repaginated layout must be byte-identical to a full render of the same model");
    }

    // R.4.9.8 — golden gate for the tricky block kinds on the incremental path: a LIST item (the
    // re-laid-out block must keep its computed marker + indent) and a BIDI/RTL block (direction +
    // mirrored caret geometry). Both edits must take the incremental path AND match a full render.
    [TestMethod]
    public async Task R72_IncrementalEdit_ListAndBidiBlocks_GoldenMatchesFullRender()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const mkBlocks = () => [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Intro.' }] } },
                { id: 'l1', type: 'paragraph', content: { type: 'paragraph', listType: 'ordered', level: 0, runs: [{ id: 'lr1', kind: 'text', text: 'First item' }] } },
                { id: 'l2', type: 'paragraph', content: { type: 'paragraph', listType: 'ordered', level: 0, runs: [{ id: 'lr2', kind: 'text', text: 'Second item' }] } },
                { id: 'heb', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'hr', kind: 'text', text: 'שלום עולם' }] } },
                { id: 'p3', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r3', kind: 'text', text: 'Outro.' }] } },
            ];
            const segSig = (bl) => (bl.segments || []).map(s => (s.text || '') + '@' + Math.round((s.rect && s.rect.x) || 0) + ':' + (s.direction || 'ltr')).join('|');
            const mk = (bl) => bl.listMarker ? ('M:' + bl.listMarker.text + '@' + Math.round(bl.listMarker.localX)) : '';
            const laySig = (lay) => (lay.blocks || []).map(bl => bl.blockId + '#h' + Math.round((bl.rect && bl.rect.height) || 0) + mk(bl) + '[' + segSig(bl) + ']').join('||');
            const ensureRoot = (id) => { let r = document.getElementById(id); if (!r) { r = document.createElement('div'); r.id = id; r.style.position = 'relative'; document.body.appendChild(r); } r.innerHTML = ''; return r; };

            const A = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            A.mount(ensureRoot('harness-root')).setModel({ documentId: 'a', body: { blocks: mkBlocks() } }).setSelection({ blockId: 'l2', offset: 0 }).attachInput();
            A.render(); A.focusInput();
            const surface = A.getInputSurface().element;
            const fire = () => surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'Z', bubbles: true, cancelable: true }));
            // Edit the second LIST item.
            fire();
            const listIncremental = !!A.getLastRenderTimings().incremental;
            const l2 = A.getLayout().blocks.find(b => b.blockId === 'l2');
            const l2marker = l2.listMarker ? l2.listMarker.text : null;
            // Edit the HEBREW (RTL) block.
            A.setSelection({ blockId: 'heb', offset: 0 });
            fire();
            const bidiIncremental = !!A.getLastRenderTimings().incremental;
            const heb = A.getLayout().blocks.find(b => b.blockId === 'heb');
            const hebDir = (heb.segments || []).map(s => s.direction || 'ltr').join(',');

            const aModel = JSON.parse(JSON.stringify(A.getSnapshot().model));
            const aSig = laySig(A.getLayout());
            const B = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            B.mount(ensureRoot('golden-root')).setModel(aModel); B.render();
            const bSig = laySig(B.getLayout());
            let firstDiff = null;
            for (let k = 0; k < Math.max(aSig.length, bSig.length); k++) { if (aSig[k] !== bSig[k]) { firstDiff = { pos: k, a: aSig.slice(k, k + 80), b: bSig.slice(k, k + 80) }; break; } }
            return JSON.stringify({ listIncremental, l2marker, bidiIncremental, hebDir, match: aSig === bSig, firstDiff });
        }");

        var r = JsonDocument.Parse(json).RootElement;
        TestContext.WriteLine($"R.4.9.8 list+bidi: listIncremental={r.GetProperty("listIncremental").GetBoolean()} l2marker={r.GetProperty("l2marker")}, bidiIncremental={r.GetProperty("bidiIncremental").GetBoolean()} hebDir={r.GetProperty("hebDir").GetString()}, match={r.GetProperty("match").GetBoolean()}");
        if (r.TryGetProperty("firstDiff", out var d) && d.ValueKind == JsonValueKind.Object)
            TestContext.WriteLine($"  firstDiff@{d.GetProperty("pos").GetInt32()}: A={d.GetProperty("a").GetString()} | B={d.GetProperty("b").GetString()}");

        r.GetProperty("listIncremental").GetBoolean().Should().BeTrue("editing a list item takes the incremental path");
        r.GetProperty("l2marker").GetString().Should().Be("2.", "the re-laid-out list item keeps its computed ordered marker");
        r.GetProperty("bidiIncremental").GetBoolean().Should().BeTrue("editing an RTL block takes the incremental path");
        r.GetProperty("hebDir").GetString().Should().Contain("rtl", "the RTL block keeps its bidi direction after the incremental edit");
        r.GetProperty("match").GetBoolean().Should().BeTrue("incremental list + bidi edits must be byte-identical to a full render");
    }

    // R.4.8 follow-up — inline-image resize probe: does resizing an INLINE image update its painted
    // object width? (Floating images already resize; this checks the inline path.)
    [TestMethod]
    public async Task R73_InlineImageResize_Probe()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'i', body: { blocks: [{ id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Hello world' }] } }] } }).setSelection({ blockId: 'p0', offset: 5 }).attachInput();
            host.render();
            const r = host.insertImage({ url: 'pic.png', width: 240, height: 160 }); // inline (default wrapMode)
            host.render();
            const objs0 = host.getObjects();
            const wBefore = objs0.length ? Math.round(objs0[0].rect.width) : -1;
            host.resizeSelectedObject(360, 240);
            const info = host.getSelectedObjectInfo();
            const objs1 = host.getObjects();
            const wAfter = objs1.length ? Math.round(objs1[0].rect.width) : -1;
            return JSON.stringify({ objCount: objs1.length, wBefore, wAfter, modelW: info ? info.width : -1 });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        TestContext.WriteLine($"R.4.8 inline-resize probe: objCount={r.GetProperty("objCount").GetInt32()}, rect.width {r.GetProperty("wBefore").GetInt32()}→{r.GetProperty("wAfter").GetInt32()}, model.width={r.GetProperty("modelW").GetDouble()}");
    }

    // R.4.9.1 — profiling probe: where does the per-keystroke time go? Breaks the synchronous
    // render() down into layoutDocument / bidi / list / viewLayout / snapshot / renderer /
    // overlays, so we know whether the O(N) cost is in LAYOUT (engine.layoutDocument) or the DOM
    // patch (renderer). Confirms the target for the incremental work.
    [TestMethod]
    public async Task R66_PerfProfile_PerKeystrokeBreakdown()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);

        foreach (var n in new[] { 30, 100, 500 })
        {
            var json = await page.EvaluateAsync<string>(@"(n) => {
                if (window.__host) { try { window.__host.destroy(); } catch (e) { /* */ } }
                const M = window.tmDocumentEditorModules;
                const root = document.getElementById('harness-root');
                const blocks = [];
                for (let i = 0; i < n; i++) {
                    blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Performance paragraph ' + i + ' contents.' }] } });
                }
                const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
                host.mount(root).setModel({ documentId: 'perf', body: { blocks } }).setSelection({ blockId: 'p5', offset: 0 }).attachInput();
                host.render();
                host.focusInput();
                window.__host = host;

                const surface = host.getInputSurface().element;
                const fire = () => surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'x', bubbles: true, cancelable: true }));
                for (let w = 0; w < 5; w++) fire();
                // Average the breakdown over several keystrokes.
                const acc = {};
                const K = 15;
                for (let k = 0; k < K; k++) {
                    fire();
                    const t = host.getLastRenderTimings();
                    for (const key in t) { if (typeof t[key] === 'number') acc[key] = (acc[key] || 0) + t[key]; }
                }
                for (const key in acc) acc[key] = acc[key] / K;
                return JSON.stringify(acc);
            }", n);

            var t = JsonDocument.Parse(json).RootElement;
            double G(string k) => t.TryGetProperty(k, out var v) ? v.GetDouble() : 0;
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "R.4.9.1 profile [{0}p]: total={1:F1}ms = layoutDocument {2:F1} + bidi {3:F1} + list {4:F1} | viewLayout {5:F1} | snapshot {6:F1} | renderer {7:F1} | overlays {8:F1}",
                n, G("totalMs"), G("layoutDocumentMs"), G("bidiMs"), G("listMs"), G("viewLayoutMs"), G("snapshotMs"), G("rendererMs"), G("overlaysMs")));
        }
    }

    // R.5.1 — DATA-LOSS BLOKER gate. Loads the exact JS-model shape CoreEngineModelConverter emits for
    // tables, a standalone image (paragraph + drawing run + imageBlock flag + __docSource preserve), and a
    // page break. Proves the LIVE engine (a) renders all three and (b) preserves the converter's extra
    // properties through the very read-back path the bridge uses (getSnapshot().model) — even after an edit.
    // Without this, opening a .docx with a table/image and saving through the core engine would lose them.
    [TestMethod]
    public async Task R75_Converter_TableImagePageBreak_RenderAndSurviveModelReadback()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const cell = (id, text) => ({ id: id, type: 'tableCell', rowSpan: 1, colSpan: 1, style: {}, blocks: [{ id: id + '-p', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: id + '-r', kind: 'text', text: text }] } }] });
            const model = { documentId: 'rt', version: 1, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'p0-r0', kind: 'text', text: 'Intro' }] } },
                { id: 't', type: 'table', content: { type: 'table', rows: [
                    { id: 't-row0', cells: [cell('c00', 'H1'), cell('c01', 'H2')] },
                    { id: 't-row1', cells: [cell('c10', 'B1'), cell('c11', 'B2')] },
                ], tableLayout: { width: 480, alignment: 'Center', backgroundColor: null, cellPadding: null } } },
                { id: 'i', type: 'paragraph', imageBlock: true, content: { type: 'paragraph', runs: [
                    { id: 'i-run', kind: 'drawing', objectId: 'i-obj', url: 'pic.png', __docSource: '{\""source\"":1,\""url\"":\""pic.png\"",\""assetId\"":\""asset-1\""}',
                      layout: { wrapMode: 'square', width: 120, height: 90, altText: 'alt', caption: 'cap', zIndex: 0, horizontalPosition: { align: 'Left', offset: 0, relativeTo: 'Page' }, verticalPosition: { align: 'Top', offset: 0, relativeTo: 'Page' } } },
                ] } },
                { id: 'pb', type: 'pageBreak' },
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'p1-r0', kind: 'text', text: 'After' }] } },
            ] } };
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 5 }).attachInput();
            host.render();

            // --- DOM: did the engine actually render the structural blocks? ---
            const text = root.textContent || '';
            const cellsRendered = ['H1', 'H2', 'B1', 'B2'].every(t => text.indexOf(t) !== -1);
            const img = root.querySelector('figure[data-object-id] img, figure img, img');
            const imgRendered = !!img && (img.getAttribute('src') || '').indexOf('pic.png') !== -1;
            const pageSections = root.querySelectorAll('[data-page-index], .tm-render-page, [data-render-page]').length;

            // --- read back through the bridge's exact path (getSnapshot().model) ---
            const readModel = () => { const snap = host.getSnapshot(); return (snap && snap.model) || null; };
            const findBlock = (m, id) => (m && m.body && m.body.blocks || []).find(b => b.id === id) || null;
            const probe = (m) => {
                const i = findBlock(m, 'i'); const t = findBlock(m, 't'); const pb = findBlock(m, 'pb');
                const iRun = i && i.content && (i.content.runs || [])[0];
                return {
                    imageBlockFlag: !!(i && i.imageBlock),
                    imageDrawingKind: iRun && iRun.kind,
                    imageDocSource: !!(iRun && typeof iRun.__docSource === 'string' && iRun.__docSource.length > 0),
                    tableType: t && t.type,
                    tableLayoutWidth: t && t.content && t.content.tableLayout && t.content.tableLayout.width,
                    cellText: t && t.content && t.content.rows && t.content.rows[1] && t.content.rows[1].cells[1] && t.content.rows[1].cells[1].blocks[0].content.runs[0].text,
                    pageBreakType: pb && pb.type,
                    blockCount: (m && m.body && m.body.blocks || []).length,
                };
            };
            const before = probe(readModel());

            // --- edit (type a char) then re-read: structural blocks must still be intact ---
            const surface = host.getInputSurface().element;
            surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'X', bubbles: true, cancelable: true }));
            const after = probe(readModel());

            return JSON.stringify({ cellsRendered, imgRendered, pageSections, before, after });
        }");

        var r = JsonDocument.Parse(json).RootElement;
        // (a) the engine renders the structural blocks loaded from the converter shape.
        Assert.IsTrue(r.GetProperty("cellsRendered").GetBoolean(), "table cells should render");
        Assert.IsTrue(r.GetProperty("imgRendered").GetBoolean(), "the image should render as a figure/img with the url");
        Assert.IsTrue(r.GetProperty("pageSections").GetInt32() >= 2, "the page break should split content onto a second page");

        // (b) the converter's extra props survive the bridge read-back path — BEFORE and AFTER an edit.
        foreach (var key in new[] { "before", "after" })
        {
            var p = r.GetProperty(key);
            Assert.IsTrue(p.GetProperty("imageBlockFlag").GetBoolean(), $"[{key}] imageBlock flag must survive read-back");
            Assert.AreEqual("drawing", p.GetProperty("imageDrawingKind").GetString(), $"[{key}] image drawing run preserved");
            Assert.IsTrue(p.GetProperty("imageDocSource").GetBoolean(), $"[{key}] __docSource preserve channel must survive");
            Assert.AreEqual("table", p.GetProperty("tableType").GetString(), $"[{key}] table block preserved");
            Assert.AreEqual(480d, p.GetProperty("tableLayoutWidth").GetDouble(), $"[{key}] tableLayout preserved");
            Assert.AreEqual("B2", p.GetProperty("cellText").GetString(), $"[{key}] nested cell text preserved");
            Assert.AreEqual("pageBreak", p.GetProperty("pageBreakType").GetString(), $"[{key}] page break preserved");
            Assert.AreEqual(5, p.GetProperty("blockCount").GetInt32(), $"[{key}] no blocks dropped");
        }
        TestContext.WriteLine("R.5.1 data-loss gate: table+image+pageBreak render AND survive getSnapshot().model read-back before+after an edit (no data loss).");
    }

    // R.5.2 — rich clipboard. Copy a formatted range (bold run) to a real browser DataTransfer,
    // then paste it elsewhere; the pasted text AND its bold mark must survive (internal fragment).
    [TestMethod]
    public async Task R76_Clipboard_CopyFormattedRange_PasteKeepsMarks()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'cb', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [
                    { id: 'r0', kind: 'text', text: 'Hello ' },
                    { id: 'r1', kind: 'text', text: 'bold', marks: [{ type: 'bold' }] },
                    { id: 'r2', kind: 'text', text: ' world' },
                ] } },
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r3', kind: 'text', text: 'Target' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 6 }).attachInput();
            host.render();
            // Select the bold word 'bold' (offset 6..10).
            for (let i = 0; i < 4; i++) host.moveCaret('ArrowRight', true);
            const dt = new DataTransfer();
            const copied = host.copyToClipboard(dt);
            const plain = dt.getData('text/plain');
            // Paste at the end of p1.
            host.setSelection({ blockId: 'p1', offset: 6 });
            const pasted = host.pasteFromClipboard(dt, false);
            const m = host.getSnapshot().model;
            const p1 = m.body.blocks.find(b => b.id === 'p1');
            const p1text = p1.content.runs.map(r => r.text || '').join('');
            const hasBold = p1.content.runs.some(r => (r.text === 'bold') && (r.marks || []).some(mk => mk.type === 'bold'));
            return JSON.stringify({ copied, plain, pasted, p1text, hasBold });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("copied").GetBoolean(), "copy should succeed for a non-collapsed selection");
        Assert.AreEqual("bold", r.GetProperty("plain").GetString(), "plain text on the clipboard");
        Assert.IsTrue(r.GetProperty("pasted").GetBoolean(), "paste should succeed");
        Assert.AreEqual("Targetbold", r.GetProperty("p1text").GetString(), "pasted text appended");
        Assert.IsTrue(r.GetProperty("hasBold").GetBoolean(), "the pasted run keeps its bold mark (internal fragment)");
        TestContext.WriteLine("R.5.2 clipboard: copy formatted range → DataTransfer → paste keeps text + bold mark.");
    }

    // R.5.4 — Ctrl/Cmd+click on a hyperlink opens it (window.open with the href).
    [TestMethod]
    public async Task R77_Hyperlink_CtrlClick_OpensHref()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'lnk', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [
                    { id: 'r0', kind: 'text', text: 'visit ' },
                    { id: 'r1', kind: 'text', text: 'site', marks: [{ type: 'link', value: 'https://example.com/x' }] },
                ] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const span = root.querySelector('[data-href]');
            const hrefAttr = span ? span.getAttribute('data-href') : null;
            let opened = null; const orig = window.open; window.open = function (u) { opened = u; return null; };
            const rect = span.getBoundingClientRect();
            span.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, ctrlKey: true, clientX: rect.left + 2, clientY: rect.top + 2 }));
            window.open = orig;
            return JSON.stringify({ hrefAttr, opened });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual("https://example.com/x", r.GetProperty("hrefAttr").GetString(), "link renders with data-href");
        Assert.AreEqual("https://example.com/x", r.GetProperty("opened").GetString(), "Ctrl+click opens the href via window.open");
        TestContext.WriteLine("R.5.4 hyperlink: Ctrl+click on data-href → window.open(href).");
    }

    // R.5.5 — bookmarks: add a named anchor on a range (renders data-bookmark), navigate back to it.
    [TestMethod]
    public async Task R78_Bookmark_AddRendersAnchor_GoToMovesCaret()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'bm', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Hello world here' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 6 }).attachInput();
            host.render();
            // Select 'world' (offset 6..11) and bookmark it.
            for (let i = 0; i < 5; i++) host.moveCaret('ArrowRight', true);
            const added = host.addBookmark('spot');
            host.render();
            const anchorEl = root.querySelector('[data-bookmark=""spot""]');
            const list = host.listBookmarks();
            // Move the caret to the start, then navigate to the bookmark.
            host.setSelection({ blockId: 'p0', offset: 0 });
            const went = host.goToBookmark('spot');
            const caret = host.getCaret();
            return JSON.stringify({ added, anchorText: anchorEl ? anchorEl.textContent : null, listLen: list.length, listName: list[0] && list[0].name, listOffset: list[0] && list[0].offset, went, caretOffset: caret.offset });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("added").GetBoolean(), "addBookmark succeeds on a selection");
        Assert.AreEqual("world", r.GetProperty("anchorText").GetString(), "bookmark renders a data-bookmark span over the range");
        Assert.AreEqual(1, r.GetProperty("listLen").GetInt32());
        Assert.AreEqual("spot", r.GetProperty("listName").GetString());
        Assert.AreEqual(6, r.GetProperty("listOffset").GetInt32(), "bookmark recorded at offset 6");
        Assert.IsTrue(r.GetProperty("went").GetBoolean(), "goToBookmark finds it");
        Assert.AreEqual(6, r.GetProperty("caretOffset").GetInt32(), "navigation moves the caret to the bookmark");
        TestContext.WriteLine("R.5.5 bookmark: add over 'world' → data-bookmark anchor; goToBookmark returns caret to offset 6.");
    }

    // R.5.3 — autosave seam: the engine fires a DEBOUNCED onChange after edits (drives C# autosave).
    // Several rapid keystrokes coalesce into a single fire; loading a doc does not fire.
    [TestMethod]
    public async Task R79_OnChange_DebouncedAfterEdits_DrivesAutosave()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"async () => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            let fires = 0;
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps, changeDebounceMs: 50, onChange: function () { fires++; } });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'as', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Start' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 5 }).attachInput();
            host.render();
            host.focusInput();
            const firesAfterLoad = fires; // loading must NOT fire
            const surface = host.getInputSurface().element;
            for (let i = 0; i < 3; i++) surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'x', bubbles: true, cancelable: true }));
            const firesImmediately = fires; // still debouncing → 0
            await new Promise(function (r) { setTimeout(r, 140); });
            return JSON.stringify({ firesAfterLoad, firesImmediately, firesAfterDebounce: fires });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(0, r.GetProperty("firesAfterLoad").GetInt32(), "loading a document is not a change");
        Assert.AreEqual(0, r.GetProperty("firesImmediately").GetInt32(), "onChange is debounced — no synchronous fire");
        Assert.AreEqual(1, r.GetProperty("firesAfterDebounce").GetInt32(), "3 rapid keystrokes coalesce into exactly one onChange");
        TestContext.WriteLine("R.5.3 autosave seam: debounced onChange fires once after a burst of edits (not on load).");
    }

    // R.5.6 — mouse selection gestures: double-click selects a word, triple-click selects the
    // paragraph, and a mousedown-drag extends the selection.
    [TestMethod]
    public async Task R81_Mouse_DoubleClickWord_TripleClickParagraph_DragSelects()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'ms', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Hello world foo' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const selText = () => { const r = host.getSelectionRange(); if (!r || !r.anchor) return ''; if (r.anchor.blockId !== r.focus.blockId) return '<multi>'; const m = host.getSnapshot().model; const b = m.body.blocks.find(x => x.id === r.focus.blockId); const t = b.content.runs.map(rn => rn.text || '').join(''); const s = Math.min(r.anchor.offset, r.focus.offset), e = Math.max(r.anchor.offset, r.focus.offset); return t.slice(s, e); };
            const blk = root.querySelector('[data-render-block-id=""p0""]');
            const rect = blk.getBoundingClientRect();
            const midX = rect.left + rect.width * 0.5, midY = rect.top + rect.height * 0.5;
            const fire = (detail, x, y) => blk.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: detail, clientX: x, clientY: y }));

            fire(2, midX, midY); // double-click → word
            const word = selText();
            fire(3, midX, midY); // triple-click → paragraph
            const para = selText();

            // Drag-select: mousedown at left, move to right, mouseup.
            fire(1, rect.left + 4, midY);
            document.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, clientX: rect.left + rect.width * 0.6, clientY: midY }));
            document.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, clientX: rect.left + rect.width * 0.6, clientY: midY }));
            const dragLen = selText().length;
            return JSON.stringify({ word, para, dragLen });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        var word = r.GetProperty("word").GetString();
        Assert.IsTrue(word!.Length > 0 && !word.Contains(' '), $"double-click selects a single word (got '{word}')");
        Assert.AreEqual("Hello world foo", r.GetProperty("para").GetString(), "triple-click selects the whole paragraph");
        Assert.IsTrue(r.GetProperty("dragLen").GetInt32() > 0, "drag extends a selection");
        TestContext.WriteLine($"R.5.6 mouse: double-click→'{word}', triple-click→paragraph, drag→{r.GetProperty("dragLen").GetInt32()} chars.");
    }

    // R.5.7 — PageDown/PageUp jump the caret by ~a viewport of lines.
    [TestMethod]
    public async Task R82_PageUpPageDown_JumpsManyLines()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 400, marginTop: 36, marginBottom: 36, marginLeft: 72, marginRight: 72 };
            const blocks = [];
            for (let i = 0; i < 60; i++) blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Line ' + i }] } });
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'pg', body: { blocks } }).setViewport({ height: 400 }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const startBlock = host.getCaret().blockId;
            host.moveCaret('PageDown', false);
            const afterDown = host.getCaret().blockId;
            host.moveCaret('PageDown', false);
            const afterDown2 = host.getCaret().blockId;
            host.moveCaret('PageUp', false);
            const afterUp = host.getCaret().blockId;
            const idx = id => Number(String(id).replace('p', ''));
            return JSON.stringify({ start: idx(startBlock), afterDown: idx(afterDown), afterDown2: idx(afterDown2), afterUp: idx(afterUp) });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        int start = r.GetProperty("start").GetInt32(), down = r.GetProperty("afterDown").GetInt32(), down2 = r.GetProperty("afterDown2").GetInt32(), up = r.GetProperty("afterUp").GetInt32();
        Assert.IsTrue(down - start >= 5, $"PageDown jumps several lines (start {start} → {down})");
        Assert.IsTrue(down2 > down, "second PageDown advances further");
        Assert.IsTrue(up < down2, "PageUp moves back up");
        TestContext.WriteLine($"R.5.7 paging: lines {start} → PageDown {down} → {down2} → PageUp {up}.");
    }

    // R.5.8 — pending format: toggling bold on a collapsed caret bolds the NEXT typed character.
    [TestMethod]
    public async Task R83_PendingFormat_CollapsedBold_AppliesToNextChar()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'pf', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'ab' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 2 }).attachInput();
            host.render(); host.focusInput();
            const toggled = host.toggleMark('bold');       // collapsed → pending bold
            const pressed = host.getFormattingState().bold; // toolbar reflects pending
            const surface = host.getInputSurface().element;
            surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'X', bubbles: true, cancelable: true }));
            const m = host.getSnapshot().model;
            const runs = m.body.blocks[0].content.runs;
            const xRun = runs.find(r => (r.text || '').indexOf('X') !== -1);
            const xBold = !!(xRun && (xRun.marks || []).some(mk => mk.type === 'bold'));
            // After typing, pending is consumed: the next char is NOT bold.
            surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'Y', bubbles: true, cancelable: true }));
            const m2 = host.getSnapshot().model;
            const yRun = m2.body.blocks[0].content.runs.find(r => (r.text || '') === 'Y' || ((r.text||'').indexOf('Y')!==-1 && !(r.marks||[]).some(mk=>mk.type==='bold')));
            const yBold = !!(yRun && (yRun.marks || []).some(mk => mk.type === 'bold'));
            return JSON.stringify({ toggled, pressed, xBold, yBold });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("toggled").GetBoolean(), "toggleMark on collapsed caret returns handled (pending)");
        Assert.IsTrue(r.GetProperty("pressed").GetBoolean(), "toolbar shows bold pressed after pending toggle");
        Assert.IsTrue(r.GetProperty("xBold").GetBoolean(), "the next typed character is bold");
        Assert.IsFalse(r.GetProperty("yBold").GetBoolean(), "pending is consumed — the following character is not bold");
        TestContext.WriteLine("R.5.8 pending format: collapsed bold → next char bold, then consumed.");
    }

    // R.5.10 — inline image visual resize: resizing updates the painted figure width.
    [TestMethod]
    public async Task R84_InlineImage_VisualResize_FigureWidthUpdates()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'im', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'See ' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 4 }).attachInput();
            host.render();
            host.insertImage({ url: 'pic.png', width: 120, height: 90 }); // inline (default)
            host.render();
            const fig0 = root.querySelector('figure[data-object-id]');
            const w0 = fig0 ? Math.round(parseFloat(fig0.style.width)) : -1;
            host.resizeSelectedObject(240, 180);
            host.render();
            const fig1 = root.querySelector('figure[data-object-id]');
            const w1 = fig1 ? Math.round(parseFloat(fig1.style.width)) : -1;
            return JSON.stringify({ w0, w1 });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(120, r.GetProperty("w0").GetInt32(), "inline image renders at its initial width");
        Assert.AreEqual(240, r.GetProperty("w1").GetInt32(), "resizing the inline image updates the painted figure width");
        TestContext.WriteLine($"R.5.10 inline resize: figure width {r.GetProperty("w0").GetInt32()} → {r.GetProperty("w1").GetInt32()}.");
    }

    // R.5.9 — advanced table editing: Tab navigates cells (and appends a row past the end);
    // delete row/column and horizontal merge mutate the structure.
    [TestMethod]
    public async Task R85_Table_TabNav_DeleteRowCol_MergeRight()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'tb', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Intro' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 5 }).attachInput();
            host.render();
            host.insertTable({ rows: 2, cols: 2 });
            host.render();
            const info0 = host.getTableInfo();
            const cell00 = host.getCaret().blockId; // caret dropped into first cell
            // Tab moves to the next cell.
            const t1 = host.tableTab(false); const afterTab1 = host.getCaret().blockId;
            const t2 = host.tableTab(false); const afterTab2 = host.getCaret().blockId;
            // Shift+Tab moves back.
            host.tableTab(true); const afterShiftTab = host.getCaret().blockId;
            // Add a column then delete it.
            host.insertColumnRight(); const colsAfterAdd = host.getTableInfo().cols;
            host.deleteTableColumn(); const colsAfterDel = host.getTableInfo().cols;
            // Add a row then delete it.
            host.insertRowBelow(); const rowsAfterAdd = host.getTableInfo().rows;
            host.deleteTableRow(); const rowsAfterDel = host.getTableInfo().rows;
            // Position the caret in the first cell (col 0), then merge it with the cell to its right → colSpan 2.
            const tbl0 = host.getSnapshot().model.body.blocks.find(b => b.type === 'table');
            host.setSelection({ blockId: tbl0.content.rows[0].cells[0].blocks[0].id, offset: 0 });
            const merged = host.mergeCellRight();
            const m = host.getSnapshot().model;
            const table = m.body.blocks.find(b => b.type === 'table');
            const firstRowCells = table.content.rows[0].cells.length;
            const firstCellSpan = table.content.rows[0].cells[0].colSpan;
            return JSON.stringify({ rows0: info0.rows, cols0: info0.cols, distinctTab: (cell00 !== afterTab1 && afterTab1 !== afterTab2), backToFirst: afterShiftTab === afterTab1, colsAfterAdd, colsAfterDel, rowsAfterAdd, rowsAfterDel, merged, firstRowCells, firstCellSpan });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(2, r.GetProperty("rows0").GetInt32());
        Assert.AreEqual(2, r.GetProperty("cols0").GetInt32());
        Assert.IsTrue(r.GetProperty("distinctTab").GetBoolean(), "Tab moves through distinct cells");
        Assert.IsTrue(r.GetProperty("backToFirst").GetBoolean(), "Shift+Tab moves back a cell");
        Assert.AreEqual(3, r.GetProperty("colsAfterAdd").GetInt32(), "insert column → 3 cols");
        Assert.AreEqual(2, r.GetProperty("colsAfterDel").GetInt32(), "delete column → back to 2");
        Assert.AreEqual(3, r.GetProperty("rowsAfterAdd").GetInt32(), "insert row → 3 rows");
        Assert.AreEqual(2, r.GetProperty("rowsAfterDel").GetInt32(), "delete row → back to 2");
        Assert.IsTrue(r.GetProperty("merged").GetBoolean(), "merge right succeeds");
        Assert.AreEqual(1, r.GetProperty("firstRowCells").GetInt32(), "merged row has one cell");
        Assert.AreEqual(2, r.GetProperty("firstCellSpan").GetInt32(), "merged cell spans 2 columns");
        TestContext.WriteLine("R.5.9 tables: Tab nav + insert/delete row+col + horizontal merge (colSpan 2).");
    }

    // R.5.14 — regex find + replace with back-references.
    [TestMethod]
    public async Task R86_FindReplace_Regex_BackReferences()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'rx', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Due 2026-05-31 and 2025-01-02.' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const found = host.find('(\\d{4})-(\\d{2})-(\\d{2})', { regex: true });
            const replaced = host.replaceAll('(\\d{4})-(\\d{2})-(\\d{2})', '$3/$2/$1', { regex: true });
            const text = host.getSnapshot().model.body.blocks[0].content.runs.map(r => r.text || '').join('');
            return JSON.stringify({ found, replaced, text });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(2, r.GetProperty("found").GetInt32(), "regex finds both dates");
        Assert.AreEqual(2, r.GetProperty("replaced").GetInt32(), "regex replaces both");
        Assert.AreEqual("Due 31/05/2026 and 02/01/2025.", r.GetProperty("text").GetString(), "back-references reorder the date parts");
        TestContext.WriteLine("R.5.14 regex: (yyyy)-(mm)-(dd) → $3/$2/$1 over two matches.");
    }

    // R.5.16 — an RTL (Hebrew) paragraph with no explicit alignment is right-aligned.
    [TestMethod]
    public async Task R87_Bidi_RtlParagraph_RightAligned()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'rtl', body: { blocks: [
                { id: 'en', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'e0', kind: 'text', text: 'Hello' }] } },
                { id: 'he', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'h0', kind: 'text', text: 'שלום' }] } },
                { id: 'rr', type: 'paragraph', content: { type: 'paragraph', alignment: 'right', runs: [{ id: 'rr0', kind: 'text', text: 'Right' }] } },
            ] } }).setSelection({ blockId: 'en', offset: 0 }).attachInput();
            host.render();
            const segLeft = (id) => { const seg = root.querySelector('[data-render-block-id=\""' + id + '\""] .tm-render-segment'); return seg ? Math.round(seg.getBoundingClientRect().left) : -1; };
            return JSON.stringify({ en: segLeft('en'), he: segLeft('he'), rr: segLeft('rr') });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        int en = r.GetProperty("en").GetInt32(), he = r.GetProperty("he").GetInt32(), rr = r.GetProperty("rr").GetInt32();
        Assert.IsTrue(en >= 0 && he >= 0, "both segments rendered");
        Assert.IsTrue(he > en + 100, $"RTL paragraph is pushed to the right margin (en left {en}, he left {he})");
        Assert.IsTrue(rr > en + 100, $"explicit right-alignment also shifts the rendered text (en {en}, rr {rr})");
        TestContext.WriteLine($"R.5.16 RTL: LTR {en}px, RTL {he}px, explicit-right {rr}px (alignment shift now reaches the rendered segments).");
    }

    // R.5.15 — Table of Contents generation + outline navigation (goToHeading + TOC-entry click).
    [TestMethod]
    public async Task R88_Outline_TocGeneration_AndNavigation()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'toc', body: { blocks: [
                { id: 'h1', type: 'paragraph', content: { type: 'paragraph', headingLevel: 1, styleName: 'Heading1', runs: [{ id: 'h1r', kind: 'text', text: 'Chapter One' }] } },
                { id: 'b1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'b1r', kind: 'text', text: 'Body of one.' }] } },
                { id: 'h2', type: 'paragraph', content: { type: 'paragraph', headingLevel: 2, styleName: 'Heading2', runs: [{ id: 'h2r', kind: 'text', text: 'Section Two' }] } },
                { id: 'b2', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'b2r', kind: 'text', text: 'Body of two.' }] } },
            ] } }).setSelection({ blockId: 'b2', offset: 0 }).attachInput();
            host.render();
            const outline = host.getOutline();
            const n = host.insertTableOfContents();
            const m = host.getSnapshot().model;
            const tocBlocks = m.body.blocks.filter(b => b.content && b.content.toc);
            // goToHeading API.
            host.goToHeading('h1'); const afterGoto = host.getCaret().blockId;
            // Click the second TOC entry → navigate to its heading (h2).
            const tocEl = root.querySelector('[data-render-block-id=\""' + tocBlocks[1].id + '\""]');
            const rect = tocEl.getBoundingClientRect();
            tocEl.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: 1, clientX: rect.left + 4, clientY: rect.top + rect.height / 2 }));
            const afterClick = host.getCaret().blockId;
            return JSON.stringify({ outlineLen: outline.length, n, tocCount: tocBlocks.length, target0: tocBlocks[0].content.tocTargetBlockId, afterGoto, afterClick });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(2, r.GetProperty("outlineLen").GetInt32(), "outline has 2 headings");
        Assert.AreEqual(2, r.GetProperty("n").GetInt32(), "TOC inserted 2 entries");
        Assert.AreEqual(2, r.GetProperty("tocCount").GetInt32());
        Assert.AreEqual("h1", r.GetProperty("target0").GetString(), "first TOC entry targets the first heading");
        Assert.AreEqual("h1", r.GetProperty("afterGoto").GetString(), "goToHeading moves the caret");
        Assert.AreEqual("h2", r.GetProperty("afterClick").GetString(), "clicking a TOC entry navigates to its heading");
        TestContext.WriteLine("R.5.15 outline: TOC generated (2 entries); goToHeading + TOC-entry click navigate.");
    }

    // R.5.11 — track-changes depth: per-revision accept/reject + review modes (markup/final/original).
    [TestMethod]
    public async Task R89_TrackChanges_PerRevision_AndReviewModes()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'tc', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [
                    { id: 'r0', kind: 'text', text: 'keep ' },
                    { id: 'r1', kind: 'text', text: 'added', marks: [{ type: 'insertion', value: 'rev1' }] },
                    { id: 'r2', kind: 'text', text: ' ' },
                    { id: 'r3', kind: 'text', text: 'gone', marks: [{ type: 'deletion', value: 'rev2' }] },
                    { id: 'r4', kind: 'text', text: ' end' },
                ] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const txt = () => (root.querySelector('[data-render-block-id=\""p0\""]').textContent || '').replace(/\s+/g, ' ').trim();
            const revs0 = host.getRevisions();
            const markup = txt();
            host.setReviewMode('final'); const finalText = txt();
            host.setReviewMode('original'); const originalText = txt();
            host.setReviewMode('markup');
            // Per-revision: accept rev1 (insertion → keep 'added'), accept rev2 (deletion → drop 'gone').
            host.acceptRevision('rev1');
            host.acceptRevision('rev2');
            const revs1 = host.getRevisions();
            const finalModelText = host.getSnapshot().model.body.blocks[0].content.runs.map(r => r.text || '').join('');
            return JSON.stringify({ revCount: revs0.length, revIds: revs0.map(r => r.id), markup, finalText, originalText, revsAfter: revs1.length, finalModelText });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(2, r.GetProperty("revCount").GetInt32(), "two revisions listed");
        Assert.AreEqual("keep added gone end", r.GetProperty("markup").GetString(), "markup shows both");
        Assert.AreEqual("keep added end", r.GetProperty("finalText").GetString(), "final view drops the deletion");
        Assert.AreEqual("keep gone end", r.GetProperty("originalText").GetString(), "original view drops the insertion");
        Assert.AreEqual(0, r.GetProperty("revsAfter").GetInt32(), "after accepting both, no revisions remain");
        Assert.AreEqual("keep added  end", r.GetProperty("finalModelText").GetString(), "accept rev1 kept 'added', accept rev2 dropped 'gone' (its surrounding spaces remain)");
        TestContext.WriteLine("R.5.11 track-changes: review modes (markup/final/original) + per-revision accept.");
    }

    // R.5.12 — comments: reply threads, author read-back, anchor position + navigation.
    [TestMethod]
    public async Task R90_Comments_ThreadsAuthorAndNavigation()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'cm', comments: [{ id: 'c1', author: 'Alice', text: 'Check this', resolved: false }], body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [
                    { id: 'r0', kind: 'text', text: 'Hello ' },
                    { id: 'r1', kind: 'text', text: 'world', marks: [{ type: 'comment', value: 'c1' }] },
                ] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const replied = host.replyToComment('c1', 'Looks good', 'Bob');
            const comments = host.getComments();
            const c = comments[0];
            host.goToComment('c1');
            const caret = host.getCaret();
            return JSON.stringify({ replied, author: c.author, anchorText: c.anchorText, anchorBlockId: c.anchorBlockId, anchorOffset: c.anchorOffset, replyCount: c.replies.length, replyAuthor: c.replies[0] && c.replies[0].author, caretBlock: caret.blockId, caretOffset: caret.offset });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("replied").GetBoolean());
        Assert.AreEqual("Alice", r.GetProperty("author").GetString(), "author read-back");
        Assert.AreEqual("world", r.GetProperty("anchorText").GetString());
        Assert.AreEqual("p0", r.GetProperty("anchorBlockId").GetString(), "anchor block for the rail");
        Assert.AreEqual(6, r.GetProperty("anchorOffset").GetInt32(), "anchor offset for the rail");
        Assert.AreEqual(1, r.GetProperty("replyCount").GetInt32(), "thread has the reply");
        Assert.AreEqual("Bob", r.GetProperty("replyAuthor").GetString());
        Assert.AreEqual("p0", r.GetProperty("caretBlock").GetString());
        Assert.AreEqual(6, r.GetProperty("caretOffset").GetInt32(), "goToComment moves to the anchor");
        TestContext.WriteLine("R.5.12 comments: reply thread + author read-back + anchor pos + goToComment.");
    }

    // R.5.13 — headers: a date field renders, and first-page vs primary scopes differ per page.
    [TestMethod]
    public async Task R91_HeaderFooter_DateField_AndFirstPageScope()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 600, marginTop: 48, marginBottom: 48, marginLeft: 72, marginRight: 72 };
            const blocks = [];
            for (let i = 0; i < 40; i++) blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph number ' + i + ' with enough text to fill the page.' }] } });
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'hf', body: { blocks } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            // First-page header = a date field; primary header (other pages) = plain text.
            host.setHeader([{ kind: 'field', fieldType: 'date', text: '2026-05-31' }], 'first');
            host.setHeader('Regular Header', 'primary');
            host.render();
            const all = root.textContent || '';
            const pageCount = root.querySelectorAll('[data-render-page-index]').length;
            const m = host.getSnapshot().model;
            const scopes = (m.headers || []).map(h => h.scope).sort();
            const headerText = (h) => (h && h.blocks && h.blocks[0].content.runs.map(r => r.text || '').join('')) || '';
            const firstRegion = (m.headers || []).find(h => h.scope === 'FirstPage');
            const primaryRegion = (m.headers || []).find(h => h.scope === 'Primary');
            return JSON.stringify({ pageCount, hasDate: all.indexOf('2026-05-31') !== -1, scopes, firstText: headerText(firstRegion), primaryText: headerText(primaryRegion) });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("pageCount").GetInt32() >= 2, "document spans multiple pages");
        Assert.IsTrue(r.GetProperty("hasDate").GetBoolean(), "the date field renders (first-page header)");
        var scopes = r.GetProperty("scopes").EnumerateArray().Select(x => x.GetString()).ToArray();
        CollectionAssert.AreEquivalent(new[] { "FirstPage", "Primary" }, scopes, "both header scopes stored");
        Assert.AreEqual("2026-05-31", r.GetProperty("firstText").GetString(), "first-page header carries the date");
        Assert.AreEqual("Regular Header", r.GetProperty("primaryText").GetString(), "primary header carries the regular text");
        TestContext.WriteLine("R.5.13 headers: date field renders + first/primary scopes stored distinctly.");
    }

    // R.5.17 — first-paint budget: a budgeted first render lays out only the first N blocks (fast),
    // then completes the full layout on idle. Verified against a 500-block document.
    [TestMethod]
    public async Task R92_FirstPaint_BudgetedLayout_ThenCompletesOnIdle()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"async () => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const make = () => { const blocks = []; for (let i = 0; i < 500; i++) blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Document paragraph number ' + i + ' with a reasonable amount of body text.' }] } }); return { documentId: 'big', body: { blocks } }; };
            const wrap = document.getElementById('harness-root'); wrap.innerHTML = '<div id=\""rb\""></div><div id=\""rf\""></div>';

            // Budgeted first paint (40 blocks).
            const hostB = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps, firstPaintMaxBlocks: 40 });
            hostB.mount(document.getElementById('rb')).setModel(make()).attachInput();
            const b0 = performance.now(); hostB.render(); const b1 = performance.now();
            const partial = hostB.getLayout();

            // Full layout (no budget) for comparison.
            const hostF = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            hostF.mount(document.getElementById('rf')).setModel(make()).attachInput();
            const f0 = performance.now(); hostF.render(); const f1 = performance.now();
            const full = hostF.getLayout();

            // Wait for the budgeted host to finish its full layout on idle.
            await new Promise(function (res) { setTimeout(res, 450); });
            const afterIdle = hostB.getLayout();
            hostF.destroy(); hostB.destroy();
            return JSON.stringify({
                budgetMs: Math.round(b1 - b0), fullMs: Math.round(f1 - f0),
                partialComplete: partial.complete, partialBlocks: partial.laidOutBlockCount,
                fullComplete: full.complete, fullBlocks: full.laidOutBlockCount,
                idleComplete: afterIdle.complete, idleBlocks: afterIdle.laidOutBlockCount,
            });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        int budgetMs = r.GetProperty("budgetMs").GetInt32(), fullMs = r.GetProperty("fullMs").GetInt32();
        Assert.IsFalse(r.GetProperty("partialComplete").GetBoolean(), "budgeted first paint is partial");
        Assert.IsTrue(r.GetProperty("partialBlocks").GetInt32() <= 40, "first paint laid out at most the budget");
        Assert.IsTrue(r.GetProperty("fullComplete").GetBoolean() && r.GetProperty("fullBlocks").GetInt32() == 500, "unbudgeted lays out everything");
        Assert.IsTrue(budgetMs < fullMs, $"budgeted first paint is faster than full ({budgetMs}ms < {fullMs}ms)");
        Assert.IsTrue(r.GetProperty("idleComplete").GetBoolean() && r.GetProperty("idleBlocks").GetInt32() == 500, "the full layout completes on idle");
        TestContext.WriteLine($"R.5.17 first-paint: budgeted {budgetMs}ms (40 blocks) vs full {fullMs}ms (500 blocks); idle completed all 500.");
    }

    // R.5.20 — accessibility: high-contrast (forced-colors) styles for caret + overlays, and
    // per-word / per-character live-region announcing.
    [TestMethod]
    public async Task R93_Accessibility_HighContrast_AndAnnounceGranularity()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'a11y', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Hello world' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();

            const caretStyle = document.getElementById('tm-core-caret-blink-style');
            const hcCaret = !!(caretStyle && caretStyle.textContent.indexOf('forced-colors') !== -1 && caretStyle.textContent.indexOf('CanvasText') !== -1);

            // Per-word announce: caret inside 'world'.
            host.setAnnounceGranularity('word');
            host.setSelection({ blockId: 'p0', offset: 6 });
            host.moveCaret('ArrowRight', false); // → offset 7, inside 'world'
            const wordAnnounce = host.getLiveRegionText();

            // Per-character announce.
            host.setAnnounceGranularity('character');
            host.moveCaret('ArrowRight', false); // → offset 8, crossed 'o'
            const charAnnounce = host.getLiveRegionText();

            // Paragraph (default).
            host.setAnnounceGranularity('paragraph');
            host.moveCaret('ArrowRight', false);
            const paraAnnounce = host.getLiveRegionText();

            // Selection overlay high-contrast style (created when a selection rect paints).
            host.setSelection({ blockId: 'p0', offset: 0 });
            for (let i = 0; i < 5; i++) host.moveCaret('ArrowRight', true);
            const overlayStyle = document.getElementById('tm-core-overlay-hc-style');
            const hcOverlay = !!(overlayStyle && overlayStyle.textContent.indexOf('Highlight') !== -1);
            return JSON.stringify({ hcCaret, wordAnnounce, charAnnounce, paraAnnounce, hcOverlay });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("hcCaret").GetBoolean(), "caret has a forced-colors high-contrast rule");
        Assert.AreEqual("world", r.GetProperty("wordAnnounce").GetString(), "word granularity announces the word at the caret");
        Assert.AreEqual("o", r.GetProperty("charAnnounce").GetString(), "character granularity announces the crossed grapheme");
        Assert.AreEqual("Hello world", r.GetProperty("paraAnnounce").GetString(), "paragraph granularity announces the full context");
        Assert.IsTrue(r.GetProperty("hcOverlay").GetBoolean(), "selection overlay has a forced-colors high-contrast rule");
        TestContext.WriteLine("R.5.20 a11y: high-contrast caret+overlay + word/character/paragraph announcing.");
    }

    // R.5.23 — view subsystems: zoom (CSS scale + zoom-aware hit-test), runtime page-settings
    // re-layout, and a print hook that injects the print stylesheet.
    [TestMethod]
    public async Task R94_ViewSubsystems_Zoom_PageSettings_Print()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const blocks = [];
            for (let i = 0; i < 30; i++) blocks.push({ id: 'p' + i, type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r' + i, kind: 'text', text: 'Paragraph ' + i + ' with a good amount of body text to fill space.' }] } });
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'view', body: { blocks } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();

            // Zoom: CSS transform applied + getZoom + hit-test still places a caret.
            const z = host.setZoom(2);
            const transform = root.style.transform;
            const sec = root.querySelector('[data-render-page-index]');
            const sr = sec.getBoundingClientRect();
            host.placeCaretFromClient(sr.left + 4, sr.top + 6, false, sec); // near the very start at 2x zoom
            const caretAfterZoom = host.getCaret();

            // Runtime page settings: smaller page → more pages.
            const before = host.getLayout().pages.length;
            host.setPageSettings({ width: 420, height: 320, marginTop: 24, marginBottom: 24, marginLeft: 24, marginRight: 24 });
            const after = host.getLayout().pages.length;

            // Print hook injects the print stylesheet (window.print stubbed so it doesn't block).
            const origPrint = window.print; let printed = false; window.print = function () { printed = true; };
            const printResult = host.print();
            window.print = origPrint;
            const printStyle = document.getElementById('tm-core-print-style');
            return JSON.stringify({ z, transform, caretBlock: caretAfterZoom.blockId, beforePages: before, afterPages: after, printed, printResult, printStyleHidesCaret: !!(printStyle && printStyle.textContent.indexOf('tm-core-caret') !== -1) });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(2d, r.GetProperty("z").GetDouble(), "setZoom returns the clamped factor");
        StringAssert.Contains(r.GetProperty("transform").GetString(), "scale(2)", "the render root is CSS-scaled");
        Assert.AreEqual("p0", r.GetProperty("caretBlock").GetString(), "hit-testing is zoom-aware (click near origin lands at the start)");
        Assert.IsTrue(r.GetProperty("afterPages").GetInt32() > r.GetProperty("beforePages").GetInt32(), "smaller page settings re-paginate into more pages");
        Assert.IsTrue(r.GetProperty("printed").GetBoolean() && r.GetProperty("printResult").GetBoolean(), "print() calls window.print");
        Assert.IsTrue(r.GetProperty("printStyleHidesCaret").GetBoolean(), "the print stylesheet hides editing overlays");
        TestContext.WriteLine($"R.5.23 view: zoom scale(2) + zoom-aware hit-test; page-settings {r.GetProperty("beforePages").GetInt32()}→{r.GetProperty("afterPages").GetInt32()} pages; print hook.");
    }

    // R.5.11 (depth) — format revisions + cross-block tracked delete (paragraph-mark deletion).
    [TestMethod]
    public async Task R95_TrackChanges_FormatRevision_AndCrossBlockDelete()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'tc2', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Hello world' }] } },
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Second line' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render(); host.focusInput();
            host.setTrackChanges(true);
            const p0bold = () => { const r = host.getSnapshot().model.body.blocks.find(b => b.id === 'p0'); return r && r.content.runs.some(rn => (rn.text || '').indexOf('Hello') !== -1 && (rn.marks || []).some(m => m.type === 'bold')); };

            // --- format revision: bold 'Hello' while tracking → tracked formatting change ---
            host.setSelection({ blockId: 'p0', offset: 0 });
            for (let i = 0; i < 5; i++) host.moveCaret('ArrowRight', true);
            host.toggleMark('bold');
            const revsFmt = host.getRevisions();
            const fmtRev = revsFmt.find(r => r.kind === 'format');
            const boldAfterApply = p0bold();
            host.rejectAllRevisions(); // reject → the tracked bold is removed
            const boldAfterReject = p0bold();

            // --- cross-block tracked delete: Backspace at the start of p1 marks the break ---
            host.setSelection({ blockId: 'p1', offset: 0 });
            const surface = host.getInputSurface().element;
            surface.dispatchEvent(new KeyboardEvent('keydown', { key: 'Backspace', bubbles: true, cancelable: true }));
            const blocksAfterDel = host.getSnapshot().model.body.blocks.length;
            const revsDel = host.getRevisions();
            const hasParaDel = revsDel.some(r => r.kind === 'paragraphDeletion');
            host.acceptAllRevisions(); // accept → the paragraphs merge
            const m = host.getSnapshot().model;
            const blocksAfterAccept = m.body.blocks.length;
            const mergedText = m.body.blocks[0].content.runs.map(r => r.text || '').join('');
            return JSON.stringify({ fmtKind: fmtRev && fmtRev.kind, fmtFormat: fmtRev && fmtRev.format, boldAfterApply, boldAfterReject, blocksAfterDel, hasParaDel, blocksAfterAccept, mergedText });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual("format", r.GetProperty("fmtKind").GetString(), "a tracked formatting change is listed as a format revision");
        Assert.AreEqual("bold", r.GetProperty("fmtFormat").GetString(), "the format revision records which mark changed");
        Assert.IsTrue(r.GetProperty("boldAfterApply").GetBoolean(), "bold is applied while tracking");
        Assert.IsFalse(r.GetProperty("boldAfterReject").GetBoolean(), "rejecting the format revision removes the bold");
        Assert.AreEqual(2, r.GetProperty("blocksAfterDel").GetInt32(), "cross-block delete keeps paragraphs separate (marked)");
        Assert.IsTrue(r.GetProperty("hasParaDel").GetBoolean(), "the paragraph-break deletion is a revision");
        Assert.AreEqual(1, r.GetProperty("blocksAfterAccept").GetInt32(), "accepting merges the paragraphs");
        Assert.AreEqual("Hello worldSecond line", r.GetProperty("mergedText").GetString(), "merged content is contiguous");
        TestContext.WriteLine("R.5.11 depth: format revision (apply/reject bold) + cross-block tracked delete (mark → accept merges).");
    }

    // R.5.9 (depth) — column resize, cell-range selection + format, and vertical (rowSpan) merge.
    [TestMethod]
    public async Task R96_Table_ColumnResize_CellRangeSelect_AndVerticalMerge()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            // A pre-filled 3×3 table (cells carry text so formatting is observable).
            const cell = (id, text) => ({ id: id, type: 'tableCell', rowSpan: 1, colSpan: 1, style: {}, blocks: [{ id: id + '-p', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: id + '-r', kind: 'text', text: text }] } }] });
            const rows = [];
            for (let ri = 0; ri < 3; ri++) { const cells = []; for (let ci = 0; ci < 3; ci++) cells.push(cell('c' + ri + ci, 'R' + ri + 'C' + ci)); rows.push({ id: 'row' + ri, cells: cells }); }
            host.mount(root).setModel({ documentId: 'tbl9', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Intro' }] } },
                { id: 't', type: 'table', content: { type: 'table', rows: rows } },
            ] } }).setSelection({ blockId: 'p0', offset: 5 }).attachInput();
            host.render();
            const para = (t, r, c) => t.content.rows[r].cells[c].blocks[0].id;
            let m = host.getSnapshot().model; let tbl = m.body.blocks.find(b => b.type === 'table');

            const tableLayout = () => host.getLayout().blocks.find(b => b.type === 'table');
            // 1. Column resize — set column 0 wide.
            host.setSelection({ blockId: para(tbl, 0, 0), offset: 0 });
            host.setColumnWidth(0, 400);
            let TL = tableLayout();
            const col0w = Math.round((TL.columns.find(c => c.index === 0) || {}).width || 0);
            const col1w = Math.round((TL.columns.find(c => c.index === 1) || {}).width || 0);

            // 2. Cell-range selection (0,0)..(1,1) → 4 cells, format bold across them.
            host.selectCellRange(para(tbl, 0, 0), para(tbl, 1, 1));
            const cellSelCount = host.getCellSelection().length;
            host.formatCellSelection('bold');
            m = host.getSnapshot().model; tbl = m.body.blocks.find(b => b.type === 'table');
            const cell11bold = tbl.content.rows[1].cells[1].blocks[0].content.runs.some(r => (r.marks || []).some(mk => mk.type === 'bold'));
            const cell22bold = tbl.content.rows[2].cells[2].blocks[0].content.runs.some(r => (r.marks || []).some(mk => mk.type === 'bold'));
            host.clearCellSelection();

            // 3. Vertical merge: merge cell (0,2) downward → rowSpan 2; row 1 loses a cell, layout covers the column.
            host.setSelection({ blockId: para(tbl, 0, 2), offset: 0 });
            const mergedDown = host.mergeCellDown();
            m = host.getSnapshot().model; tbl = m.body.blocks.find(b => b.type === 'table');
            const cell02span = tbl.content.rows[0].cells[2].rowSpan;
            const row1cellCount = tbl.content.rows[1].cells.length;
            TL = tableLayout();
            const cell02 = TL.cells.find(cl => cl.rowIndex === 0 && cl.columnIndex === 2);
            const cell02h = Math.round(cell02.rect.height);
            const row0h = Math.round((TL.rows.find(r => r.rowIndex === 0) || {}).height || 0);
            return JSON.stringify({ col0w, col1w, cellSelCount, cell11bold, cell22bold, mergedDown, cell02span, row1cellCount, cell02h, row0h });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("col0w").GetInt32() > r.GetProperty("col1w").GetInt32() + 50, "column resize widened column 0");
        Assert.AreEqual(4, r.GetProperty("cellSelCount").GetInt32(), "cell range (0,0)..(1,1) covers 4 cells");
        Assert.IsTrue(r.GetProperty("cell11bold").GetBoolean(), "formatting applies inside the selected cells");
        Assert.IsFalse(r.GetProperty("cell22bold").GetBoolean(), "cells outside the range are untouched");
        Assert.IsTrue(r.GetProperty("mergedDown").GetBoolean(), "vertical merge succeeds");
        Assert.AreEqual(2, r.GetProperty("cell02span").GetInt32(), "merged cell spans 2 rows");
        Assert.AreEqual(2, r.GetProperty("row1cellCount").GetInt32(), "the row below loses its covered cell");
        Assert.IsTrue(r.GetProperty("cell02h").GetInt32() > r.GetProperty("row0h").GetInt32(), "the rowSpan cell is taller than a single row");
        TestContext.WriteLine("R.5.9 depth: column resize + cell-range select/format + vertical (rowSpan) merge.");
    }

    // R.5.15 — named-style inheritance registry: define styles (with basedOn), apply them, and
    // edits to a base style propagate to derived styles' paragraphs.
    [TestMethod]
    public async Task R97_NamedStyleRegistry_InheritanceAndPropagation()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'sty', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Callout text' }] } },
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Derived text' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const fs = (id) => host.getSnapshot().model.body.blocks.find(b => b.id === id).content.style.fontSize;
            const wt = (id) => host.getSnapshot().model.body.blocks.find(b => b.id === id).content.style.fontWeight;

            // Define 'Callout' (basedOn Normal, larger) and apply to p0.
            host.defineStyle('Callout', { basedOn: 'Normal', label: 'Callout', style: { fontSize: 20 } });
            host.setSelection({ blockId: 'p0', offset: 0 });
            host.setParagraphStyle('Callout');
            const p0fs1 = fs('p0');

            // Define 'BigCallout' derived from Callout (adds weight) and apply to p1.
            host.defineStyle('BigCallout', { basedOn: 'Callout', style: { fontWeight: '700' } });
            host.setSelection({ blockId: 'p1', offset: 0 });
            host.setParagraphStyle('BigCallout');
            const p1fs1 = fs('p1'); const p1wt = wt('p1');

            // Edit the BASE 'Callout' fontSize → both the Callout paragraph and the derived one update.
            host.defineStyle('Callout', { basedOn: 'Normal', label: 'Callout', style: { fontSize: 30 } });
            const p0fs2 = fs('p0'); const p1fs2 = fs('p1');
            return JSON.stringify({ p0fs1, p1fs1, p1wt, p0fs2, p1fs2 });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(20d, r.GetProperty("p0fs1").GetDouble(), "applied style sets the paragraph's font size");
        Assert.AreEqual(20d, r.GetProperty("p1fs1").GetDouble(), "derived style inherits the base font size");
        Assert.AreEqual("700", r.GetProperty("p1wt").GetString(), "derived style adds its own property");
        Assert.AreEqual(30d, r.GetProperty("p0fs2").GetDouble(), "editing the base style updates its paragraph");
        Assert.AreEqual(30d, r.GetProperty("p1fs2").GetDouble(), "editing the base style propagates to derived-style paragraphs");
        TestContext.WriteLine("R.5.15 styles: basedOn inheritance + edits to a base propagate to derived-style paragraphs.");
    }

    // R.5.13 — header click-to-edit: clicking a header region places the caret in it so typing edits it.
    [TestMethod]
    public async Task R98_Header_ClickToEdit_RoutesTypingIntoTheHeader()
    {
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const ps = { width: 794, height: 1123, marginTop: 90, marginBottom: 72, marginLeft: 72, marginRight: 72, headerHeight: 48 };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps });
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            host.mount(root).setModel({ documentId: 'hdr', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Body paragraph.' }] } },
            ] } }).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            host.setHeader('Header text');
            host.render();
            const m0 = host.getSnapshot().model;
            const hBlockId = m0.headers[0].blocks[0].id;
            const hEl = root.querySelector('[data-render-region=\""Header\""] [data-render-block-id]');
            const rect = hEl.getBoundingClientRect();
            hEl.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: 1, clientX: rect.left + 5, clientY: rect.top + 5 }));
            const caretBlock = host.getCaret().blockId;
            const surface = host.getInputSurface().element;
            surface.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: '!', bubbles: true, cancelable: true }));
            const m1 = host.getSnapshot().model;
            const headerText = m1.headers[0].blocks[0].content.runs.map(r => r.text || '').join('');
            const bodyText = m1.body.blocks[0].content.runs.map(r => r.text || '').join('');
            return JSON.stringify({ hBlockId, caretBlock, headerText, bodyText });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(r.GetProperty("hBlockId").GetString(), r.GetProperty("caretBlock").GetString(), "clicking the header places the caret in the header block");
        Assert.AreEqual("Header text!", r.GetProperty("headerText").GetString(), "typing edits the header content");
        Assert.AreEqual("Body paragraph.", r.GetProperty("bodyText").GetString(), "the body is untouched");
        TestContext.WriteLine("R.5.13 header click-to-edit: click header → caret in header block → typing edits it.");
    }

    [TestMethod]
    public async Task R99_Comments_FacadeThread_Reply_Resolve_Reopen_Remove_AndNavigate()
    {
        // R.5.12 — the comment sidebar drives the engine through the createCoreEditor facade
        // (execCommand). Prove the facade routes reply / resolve / reopen / remove + navigation,
        // and that getComments() exposes the thread + anchor for the C# rail.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r99', version: 0, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Intro line.' }] } },
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Review this sentence.' }] } },
            ] } };
            const ed = M.coreEngine.createCoreEditor({
                root, doc: document, model, ariaLabel: 'Comments Doc',
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            const host = ed.getHost();

            // Select 'Review' (offsets 0..6) in p1 and add a comment through the facade.
            host.setSelection({ blockId: 'p1', offset: 0 });
            for (let i = 0; i < 6; i++) host.moveCaret('ArrowRight', true);
            ed.execCommand('comment', { text: 'Please rephrase', author: 'Alex' });
            const id = ed.getComments()[0].id;

            // Reply through the facade → thread grows.
            ed.execCommand('replycomment', { id, text: 'Agree, will fix.', author: 'Sam' });
            const afterReply = ed.getComments()[0];

            // Navigate: move the caret away, then gotocomment returns it to the anchor.
            host.setSelection({ blockId: 'p0', offset: 0 });
            ed.execCommand('gotocomment', { id });
            const caretAfterGoto = host.getCaret();

            // Resolve → record marked resolved; reopen → back to open.
            ed.execCommand('resolvecomment', { id });
            const resolvedFlag = ed.getComments()[0].resolved;
            ed.execCommand('reopencomment', { id });
            const reopenedFlag = ed.getComments()[0].resolved;

            // Remove → comment gone.
            ed.execCommand('removecomment', { id });
            const countAfterRemove = ed.getComments().length;

            return JSON.stringify({
                anchorText: afterReply.anchorText,
                author: afterReply.author,
                replyCount: (afterReply.replies || []).length,
                replyAuthor: (afterReply.replies || [])[0] && (afterReply.replies || [])[0].author,
                replyText: (afterReply.replies || [])[0] && (afterReply.replies || [])[0].text,
                anchorBlockId: afterReply.anchorBlockId,
                caretBlockAfterGoto: caretAfterGoto.blockId,
                caretOffsetAfterGoto: caretAfterGoto.offset,
                resolvedFlag, reopenedFlag, countAfterRemove,
            });
        }");
        var c = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual("Review", c.GetProperty("anchorText").GetString(), "the comment is anchored to the selected word");
        Assert.AreEqual("Alex", c.GetProperty("author").GetString());
        Assert.AreEqual(1, c.GetProperty("replyCount").GetInt32(), "replycomment appends to the thread");
        Assert.AreEqual("Sam", c.GetProperty("replyAuthor").GetString());
        Assert.AreEqual("Agree, will fix.", c.GetProperty("replyText").GetString());
        Assert.AreEqual("p1", c.GetProperty("anchorBlockId").GetString(), "getComments exposes the anchor block for navigation");
        Assert.AreEqual("p1", c.GetProperty("caretBlockAfterGoto").GetString(), "gotocomment returns the caret to the comment's block");
        Assert.AreEqual(0, c.GetProperty("caretOffsetAfterGoto").GetInt32(), "gotocomment lands at the anchor start");
        Assert.IsTrue(c.GetProperty("resolvedFlag").GetBoolean(), "resolvecomment marks the thread resolved");
        Assert.IsFalse(c.GetProperty("reopenedFlag").GetBoolean(), "reopencomment re-opens the thread");
        Assert.AreEqual(0, c.GetProperty("countAfterRemove").GetInt32(), "removecomment deletes the comment");
        TestContext.WriteLine("R.5.12 comment facade: comment→reply→navigate→resolve→reopen→remove, thread+anchor exposed for the rail.");
    }

    [TestMethod]
    public async Task R100_ContextMenu_DetectsContextAndFiresCallback()
    {
        // R.5.23 — the context menu reads what's under the pointer (selection / link / image /
        // comment / table cell) and a real right-click fires onContextMenu with that info.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r100', version: 0, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [
                    { id: 'r0', kind: 'text', text: 'Visit ' },
                    { id: 'r1', kind: 'text', text: 'our site', marks: [{ type: 'link', value: 'https://example.com' }] },
                    { id: 'r2', kind: 'text', text: ' today.' },
                ] } },
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r3', kind: 'text', text: 'Plain paragraph.' }] } },
            ] } };
            window.__ctx = [];
            const ed = M.coreEngine.createCoreEditor({
                root, doc: document, model, ariaLabel: 'Context Doc',
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
                onContextMenu: function (info, x, y) { window.__ctx.push({ info, x, y }); },
            });
            window.__ed = ed;
            const host = ed.getHost();

            // (a) Real right-click on the rendered link segment → callback fires with link href.
            const segText = () => Array.from(document.querySelectorAll('.tm-render-segment')).map(s => s.textContent);
            const linkSpan = Array.from(document.querySelectorAll('.tm-render-segment')).find(s => /site/.test(s.textContent || ''));
            const lr = linkSpan.getBoundingClientRect();
            linkSpan.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: lr.left + 3, clientY: lr.top + 5 }));

            // (b) Right-click on plain text → callback fires, no link.
            const plainSpan = Array.from(document.querySelectorAll('.tm-render-segment')).find(s => /Plain/.test(s.textContent || ''));
            const pr = plainSpan.getBoundingClientRect();
            plainSpan.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: pr.left + 3, clientY: pr.top + 5 }));

            const events = window.__ctx;
            return JSON.stringify({
                segments: segText(),
                exposed: typeof host.getContextAt === 'function' && typeof ed.getContextAt === 'function',
                eventCount: events.length,
                linkHref: events[0] && events[0].info.link,
                linkBlock: events[0] && events[0].info.blockId,
                plainLink: events[1] && events[1].info.link,
                plainBlock: events[1] && events[1].info.blockId,
                plainCaretMoved: host.getCaret().blockId,
            });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.IsTrue(r.GetProperty("exposed").GetBoolean(), "getContextAt is exposed on host + facade");
        Assert.AreEqual(2, r.GetProperty("eventCount").GetInt32(), "both right-clicks fire onContextMenu");
        Assert.AreEqual("https://example.com", r.GetProperty("linkHref").GetString(), "right-click on a link reports its href");
        Assert.AreEqual("p0", r.GetProperty("linkBlock").GetString());
        Assert.IsTrue(r.GetProperty("plainLink").ValueKind == JsonValueKind.Null, "right-click on plain text reports no link");
        Assert.AreEqual("p1", r.GetProperty("plainBlock").GetString(), "context resolves the right block");
        Assert.AreEqual("p1", r.GetProperty("plainCaretMoved").GetString(), "right-click outside a selection moves the caret to the clicked block");
        TestContext.WriteLine("R.5.23a context menu: getContextAt(link/plain) + real right-click fires onContextMenu, caret follows.");
    }

    [TestMethod]
    public async Task R101_SpellCheck_SquigglesMisspelling_SuggestsAndReplaces()
    {
        // R.5.23c — installing a checker paints red squiggles under flagged words, surfaces the
        // misspelling + suggestions via getContextAt, and replaceRange applies the fix.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r101', version: 0, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'This sentance is fine.' }] } },
            ] } };
            const ed = M.coreEngine.createCoreEditor({
                root, doc: document, model, ariaLabel: 'Spell Doc',
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            const host = ed.getHost();

            const before = document.querySelectorAll('[data-testid=""core-engine-spell-underline""]').length;

            // Install a flagged-word checker with a suggestion for 'sentance'.
            ed.execCommand('setspellcheck', { flagged: ['sentance'], suggestions: { sentance: ['sentence', 'sentience'] } });
            const after = document.querySelectorAll('[data-testid=""core-engine-spell-underline""]').length;

            // Context at the misspelled word (offset within 'sentance', which starts at 5).
            host.setSelection({ blockId: 'p0', offset: 8 });
            const ms = host.misspellingAt('p0', 8);

            // Apply the first suggestion via replaceRange.
            const ok = ed.execCommand('replacerange', { blockId: 'p0', start: ms.start, end: ms.end, text: ms.suggestions[0] });
            const fixedText = host.getSnapshot().model.body.blocks[0].content.runs.map(x => x.text || '').join('');
            const afterFix = document.querySelectorAll('[data-testid=""core-engine-spell-underline""]').length;

            return JSON.stringify({
                before, after,
                word: ms && ms.word, sStart: ms && ms.start, sEnd: ms && ms.end,
                suggestion: ms && ms.suggestions && ms.suggestions[0],
                ok, fixedText, afterFix,
            });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(0, r.GetProperty("before").GetInt32(), "no squiggles before a checker is installed");
        Assert.IsTrue(r.GetProperty("after").GetInt32() >= 1, "installing a checker paints a squiggle under the flagged word");
        Assert.AreEqual("sentance", r.GetProperty("word").GetString(), "the misspelling is detected at the pointer");
        Assert.AreEqual(5, r.GetProperty("sStart").GetInt32());
        Assert.AreEqual(13, r.GetProperty("sEnd").GetInt32());
        Assert.AreEqual("sentence", r.GetProperty("suggestion").GetString(), "the suggestion is surfaced");
        Assert.IsTrue(r.GetProperty("ok").GetBoolean(), "replaceRange applies the suggestion");
        Assert.AreEqual("This sentence is fine.", r.GetProperty("fixedText").GetString(), "the word is corrected");
        Assert.AreEqual(0, r.GetProperty("afterFix").GetInt32(), "the squiggle clears once the word is fixed");
        TestContext.WriteLine("R.5.23c spellcheck: squiggle painted, misspelling+suggestion via getContextAt, replaceRange fixes it, squiggle clears.");
    }

    [TestMethod]
    public async Task R102_DragDrop_MovesSelectedTextToDropPoint()
    {
        // R.5.23b — mousedown inside a selection + drag past the threshold relocates the text to
        // the drop caret on mouseup (Word/GDocs gesture), with a drop-caret indicator mid-drag.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r102', version: 0, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Apple Banana Cherry' }] } },
            ] } };
            const ed = M.coreEngine.createCoreEditor({
                root, doc: document, model, ariaLabel: 'Drag Doc',
                pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 },
            });
            const host = ed.getHost();

            // Select 'Banana ' (offsets 6..13, includes the trailing space).
            host.setSelection({ blockId: 'p0', offset: 6 });
            for (let i = 0; i < 7; i++) host.moveCaret('ArrowRight', true);
            const selText = (function () { const r = host.getSelectionRange(); return r.anchor.offset + '-' + r.focus.offset; })();

            const segs = () => Array.from(document.querySelectorAll('.tm-render-segment'));
            const banana = segs().find(s => /Banana/.test(s.textContent || ''));
            const first = segs().find(s => /Apple/.test(s.textContent || '')) || segs()[0];
            const br = banana.getBoundingClientRect();
            const fr = first.getBoundingClientRect();

            // mousedown inside the selection (on 'Banana').
            banana.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, clientX: br.left + br.width / 2, clientY: br.top + br.height / 2 }));
            const dropCaretBeforeMove = document.querySelectorAll('[data-testid=""core-engine-drop-caret""]').length;

            // drag to the very start of the line (drop offset 0) — past the move threshold.
            document.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true, clientX: fr.left + 1, clientY: fr.top + fr.height / 2 }));
            const dropCaretDuringMove = document.querySelectorAll('[data-testid=""core-engine-drop-caret""]').length;

            // drop.
            document.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, clientX: fr.left + 1, clientY: fr.top + fr.height / 2 }));
            const dropCaretAfter = document.querySelectorAll('[data-testid=""core-engine-drop-caret""]').length;
            const text = host.getSnapshot().model.body.blocks[0].content.runs.map(x => x.text || '').join('');

            return JSON.stringify({ selText, dropCaretBeforeMove, dropCaretDuringMove, dropCaretAfter, text, undoable: host.canUndo() });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual("6-13", r.GetProperty("selText").GetString(), "the 'Banana ' span (with trailing space) is selected");
        Assert.AreEqual(0, r.GetProperty("dropCaretBeforeMove").GetInt32(), "no drop caret until the drag passes the threshold");
        Assert.IsTrue(r.GetProperty("dropCaretDuringMove").GetInt32() >= 1, "a drop-caret indicator shows during the drag");
        Assert.AreEqual(0, r.GetProperty("dropCaretAfter").GetInt32(), "the drop caret clears on drop");
        Assert.AreEqual("Banana Apple Cherry", r.GetProperty("text").GetString(), "the dragged text is relocated to the drop point");
        Assert.IsTrue(r.GetProperty("undoable").GetBoolean(), "the move is a single undoable step");
        TestContext.WriteLine("R.5.23b drag-drop: select 'Banana ' → drag to start → 'Banana Apple Cherry', drop-caret shown mid-drag, undoable.");
    }

    [TestMethod]
    public async Task R103_Sections_NextPageBreak_AppliesLandscapeGeometry()
    {
        // R.5.23d — a section break starts a fresh page with that section's page settings
        // (landscape here): the new page is wider and stacks below the portrait page.
        var page = await OpenHarnessAsync(width: 1600, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const portrait = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const landscape = { width: 1123, height: 794, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const model = {
                documentId: 'r103', version: 0,
                sections: [{ startBlockId: 'p1', pageSettings: landscape }],
                body: { blocks: [
                    { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Portrait section.' }] } },
                    { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', kind: 'text', text: 'Landscape section.' }] } },
                ] },
            };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: portrait });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();

            const pages = host.getLayout().pages;
            const pageEls = Array.from(document.querySelectorAll('.tm-render-page'));
            const widthOf = (i) => pageEls[i] ? Math.round(parseFloat(getComputedStyle(pageEls[i]).width)) : -1;
            // which page is each block on?
            const blockPage = (id) => { const b = host.getLayout().blocks.find(x => x.blockId === id); return b ? b.pageIndex : -1; };
            return JSON.stringify({
                pageCount: pages.length,
                p0w: Math.round(pages[0].rect.width),
                p1w: pages[1] ? Math.round(pages[1].rect.width) : -1,
                p1y: pages[1] ? Math.round(pages[1].rect.y) : -1,
                p0bottom: Math.round(pages[0].rect.y + pages[0].rect.height),
                domCount: pageEls.length,
                domP0w: widthOf(0),
                domP1w: widthOf(1),
                p0Block: blockPage('p0'),
                p1Block: blockPage('p1'),
            });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual(2, r.GetProperty("pageCount").GetInt32(), "the section break creates a second page");
        Assert.AreEqual(794, r.GetProperty("p0w").GetInt32(), "page 1 keeps the portrait width");
        Assert.AreEqual(1123, r.GetProperty("p1w").GetInt32(), "page 2 takes the landscape width");
        Assert.IsTrue(r.GetProperty("p1y").GetInt32() >= r.GetProperty("p0bottom").GetInt32(), "the landscape page stacks below the portrait page (cumulative height)");
        Assert.AreEqual(2, r.GetProperty("domCount").GetInt32(), "both pages render in the DOM");
        Assert.AreEqual(794, r.GetProperty("domP0w").GetInt32(), "the portrait page renders at 794px");
        Assert.AreEqual(1123, r.GetProperty("domP1w").GetInt32(), "the landscape page renders at 1123px");
        Assert.AreEqual(0, r.GetProperty("p0Block").GetInt32(), "the portrait paragraph is on page 1");
        Assert.AreEqual(1, r.GetProperty("p1Block").GetInt32(), "the landscape paragraph is on page 2");
        TestContext.WriteLine("R.5.23d sections: section break → page 2 landscape (1123px) stacked below portrait page 1 (794px), in layout + DOM.");
    }

    [TestMethod]
    public async Task R104_Collaboration_TwoEnginesConverge_ViaOtTransform()
    {
        // R.5.18/R.5.22 — two live engine instances ('clients') type concurrently; each emits a
        // text operation, exchanges it through the OT transform, applies it as a remote op, and
        // both models CONVERGE to identical text. The collab substrate, proven without a server.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const { transformOperation } = M.coreEngine.operations;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const rootA = document.createElement('div'); const rootB = document.createElement('div');
            root.appendChild(rootA); root.appendChild(rootB);
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const seed = () => ({ documentId: 'collab', version: 0, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Hello World' }] } },
            ] } });

            const aOps = []; const bOps = [];
            const hostA = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps, onOperation: (op) => aOps.push(op) });
            const hostB = M.coreEngine.createRenderHost({ doc: document, pageSettings: ps, onOperation: (op) => bOps.push(op) });
            hostA.mount(rootA).setModel(seed()).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            hostB.mount(rootB).setModel(seed()).setSelection({ blockId: 'p0', offset: 11 }).attachInput();
            hostA.render(); hostB.render();

            const textOf = (host) => host.getSnapshot().model.body.blocks[0].content.runs.map(x => x.text || '').join('');

            // Client A types 'X' at the start; Client B types 'Z' at the end — concurrently.
            hostA.setSelection({ blockId: 'p0', offset: 0 }); hostA.focusInput();
            hostA.getInputSurface().element.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'X', bubbles: true, cancelable: true }));
            hostB.setSelection({ blockId: 'p0', offset: 11 }); hostB.focusInput();
            hostB.getInputSurface().element.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: 'Z', bubbles: true, cancelable: true }));

            const aLocalText = textOf(hostA);  // 'XHello World'
            const bLocalText = textOf(hostB);  // 'Hello WorldZ'
            const opA = aOps[0]; const opB = bOps[0];

            // Exchange through OT: A applies B's op rebased past A's; B applies A's op rebased past B's.
            transformOperation(opB, opA, 'right').forEach((o) => hostA.applyRemoteOperation(o));
            transformOperation(opA, opB, 'left').forEach((o) => hostB.applyRemoteOperation(o));

            // Presence: A shows B's caret (remote cursor with a name flag).
            hostA.setRemoteCursors([{ id: 'b', blockId: 'p0', offset: 3, color: '#16a34a', label: 'Bob' }]);
            const remoteCarets = rootA.querySelectorAll('[data-testid=""core-engine-remote-caret""]');
            const remoteLabel = remoteCarets[0] ? (remoteCarets[0].querySelector('.tm-core-remote-caret-label') || {}).textContent : null;

            return JSON.stringify({
                opA, opB, aLocalText, bLocalText,
                aFinal: textOf(hostA), bFinal: textOf(hostB),
                aLogLen: hostA.getOperationLog().length,
                remoteCaretCount: remoteCarets.length,
                remoteLabel,
            });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        Assert.AreEqual("insert", r.GetProperty("opA").GetProperty("type").GetString(), "client A emits an insert op");
        Assert.AreEqual(0, r.GetProperty("opA").GetProperty("offset").GetInt32());
        Assert.AreEqual("XHello World", r.GetProperty("aLocalText").GetString(), "A's local edit applies");
        Assert.AreEqual("Hello WorldZ", r.GetProperty("bLocalText").GetString(), "B's local edit applies");
        var aFinal = r.GetProperty("aFinal").GetString();
        var bFinal = r.GetProperty("bFinal").GetString();
        Assert.AreEqual(aFinal, bFinal, "after exchanging ops through the OT transform, both engines CONVERGE");
        Assert.AreEqual("XHello WorldZ", aFinal, "the converged document carries both concurrent edits");
        Assert.IsTrue(r.GetProperty("aLogLen").GetInt32() >= 1, "the operation journal records the local edit");
        Assert.AreEqual(1, r.GetProperty("remoteCaretCount").GetInt32(), "the collaborator's remote caret paints (presence)");
        Assert.AreEqual("Bob", r.GetProperty("remoteLabel").GetString(), "the remote caret carries the collaborator's name flag");
        TestContext.WriteLine($"R.5.18/R.5.22 collab: two engines typed concurrently → OT transform → converged to '{aFinal}'; remote presence caret painted.");
    }

    [TestMethod]
    public async Task R105_Collaboration_RelayTransport_TwoEditorsConverge_WithRealTyping()
    {
        // R.5.22 — the END-TO-END relay path: two real editors connected via editor.connectCollab
        // to a (simulated) sequencer/relay like the SignalR hub. Real keystrokes auto-broadcast as
        // ops; each editor transforms-to-head + applies; both CONVERGE. Mirrors the production loop.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const rootA = document.createElement('div'); const rootB = document.createElement('div');
            root.appendChild(rootA); root.appendChild(rootB);
            const ps = { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 };
            const seed = () => ({ documentId: 'relay', version: 0, body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'Shared' }] } },
            ] } });
            const edA = M.coreEngine.createCoreEditor({ root: rootA, doc: document, model: seed(), pageSettings: ps });
            const edB = M.coreEngine.createCoreEditor({ root: rootB, doc: document, model: seed(), pageSettings: ps });

            // A simple total-order relay sequencer (what the SignalR hub provides).
            let seq = 0; const handles = {};
            function relaySubmit(msg) {
                const change = { ops: msg.ops, base: msg.base, sequence: ++seq, clientId: msg.clientId };
                handles.A.receiveServerChange(change); // delivered to ALL in commit order
                handles.B.receiveServerChange(change);
            }
            handles.A = edA.connectCollab({ clientId: 'A', send: relaySubmit });
            handles.B = edB.connectCollab({ clientId: 'B', send: relaySubmit });

            const textOf = (ed) => ed.getModel().body.blocks[0].content.runs.map(x => x.text || '').join('');
            const typeAt = (ed, host, off, ch) => { host.setSelection({ blockId: 'p0', offset: off }); host.focusInput();
                host.getInputSurface().element.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: ch, bubbles: true, cancelable: true })); };

            // Concurrent-ish real typing: A prepends, B appends, alternating.
            typeAt(edA, edA.getHost(), 0, 'X');
            typeAt(edB, edB.getHost(), 6, 'Z');
            typeAt(edA, edA.getHost(), 0, 'Y');
            typeAt(edB, edB.getHost(), 8, 'W');

            return JSON.stringify({ a: textOf(edA), b: textOf(edB), seq });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        var a = r.GetProperty("a").GetString();
        var b = r.GetProperty("b").GetString();
        Assert.AreEqual(a, b, "both editors converge to identical text over the relay transport");
        Assert.AreEqual(10, a!.Length, "converged text = 6 base chars + 4 inserted chars (no loss)");
        Assert.IsTrue(a.Contains('X') && a.Contains('Y') && a.Contains('Z') && a.Contains('W'), "every collaborator's edit is present in the converged document");
        Assert.AreEqual(4, r.GetProperty("seq").GetInt32(), "the relay sequenced all four changes");
        TestContext.WriteLine($"R.5.22 relay transport: two editors typed over a relay → converged to '{a}' (4 sequenced changes).");
    }

    [TestMethod]
    public async Task R108_ReadingOrder_BidiSegments_AreLogicalInDom_VisualInLayout()
    {
        // R.5.20 — accessibility reading order for bidi/RTL: segments are laid out in VISUAL
        // order (RTL runs reorder right-to-left), but the DOM is emitted in LOGICAL order so a
        // screen reader reads reading order. Two adjacent Hebrew runs: visually run2 sits LEFT
        // of run1, yet the DOM order (data-model-start) is ascending [0, 3].
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r108', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [
                    { id: 'r0', kind: 'text', text: 'אבג' },
                    { id: 'r1', kind: 'text', text: 'דהו' },
                ] } },
            ] } };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const segs = Array.from(document.querySelectorAll('.tm-render-paragraph [data-render-block-id=""p0""] .tm-render-segment, [data-render-block-id=""p0""].tm-render-paragraph .tm-render-segment, [data-render-block-id=""p0""] .tm-render-segment'));
            const list = segs.map(s => ({ start: Number(s.getAttribute('data-model-start')), left: Math.round(parseFloat(s.style.left) || 0), dir: s.getAttribute('dir') || 'ltr', text: s.textContent }));
            return JSON.stringify({ count: list.length, list });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        var list = r.GetProperty("list");
        // At least the two Hebrew runs render as two segments.
        Assert.IsTrue(list.GetArrayLength() >= 2, "the two RTL runs render as separate segments");
        var starts = new List<int>();
        var lefts = new List<int>();
        var anyRtl = false;
        foreach (var seg in list.EnumerateArray())
        {
            starts.Add(seg.GetProperty("start").GetInt32());
            lefts.Add(seg.GetProperty("left").GetInt32());
            if (seg.GetProperty("dir").GetString() == "rtl") anyRtl = true;
        }
        // DOM order is logical (ascending model start).
        var sorted = starts.OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(sorted, starts, "DOM segment order is logical (ascending model start) for the screen reader");
        Assert.IsTrue(anyRtl, "the RTL runs are tagged dir=rtl so the browser shapes them right-to-left");
        // Visual is still RTL: the logically-first segment (start 0) sits to the RIGHT of the next.
        Assert.IsTrue(lefts[0] > lefts[1], "visually the logical-first RTL run is placed to the right (visual order preserved)");
        TestContext.WriteLine($"R.5.20 bidi reading-order: DOM starts={string.Join(",", starts)} (logical), lefts={string.Join(",", lefts)} (visual RTL).");
    }

    [TestMethod]
    public async Task R109_CaretStops_FollowShapedAdvances_NotLinearInterpolation()
    {
        // R.5.16 — caret stops within a word come from the measured SHAPED prefix advance, not a
        // uniform width/length split. In a real proportional font 'W' is much wider than 'i', so
        // the W-gaps must exceed the i-gaps (linear interpolation would make them all equal).
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r109', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: 'WWii' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const stops = host.getLayout().caretStops
                .filter(s => s.blockId === 'p0')
                .sort((a, b) => a.offset - b.offset)
                .map(s => Math.round((s.rect.x) * 100) / 100);
            // gaps between consecutive caret stops: W, W, i, i
            const gaps = [];
            for (let i = 1; i < stops.length; i++) gaps.push(Math.round((stops[i] - stops[i - 1]) * 100) / 100);
            return JSON.stringify({ stops, gaps });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        var gaps = r.GetProperty("gaps").EnumerateArray().Select(g => g.GetDouble()).ToList();
        gaps.Count.Should().Be(4, "WWii has 4 inter-caret gaps");
        var wGap = (gaps[0] + gaps[1]) / 2.0;
        var iGap = (gaps[2] + gaps[3]) / 2.0;
        wGap.Should().BeGreaterThan(iGap * 1.3, "the 'W' advances are measurably wider than the 'i' advances (shaping-aware, not interpolated)");
        // Linear interpolation would make all four gaps equal; assert they are NOT.
        gaps.Distinct().Count().Should().BeGreaterThan(1, "caret gaps are non-uniform (real measured advances, not width/length interpolation)");
        TestContext.WriteLine($"R.5.16 shaped caret advances: gaps W≈{wGap:0.0}px vs i≈{iGap:0.0}px (non-uniform = measured, not interpolated).");
    }

    [TestMethod]
    public async Task R110_BidiReWrap_RtlParagraphWrapsAndEachLineIsReordered()
    {
        // R.5.19 — an RTL paragraph that WRAPS across several lines in a narrow column: each
        // wrapped line is independently bidi-reordered (RTL), so on every line the logically-
        // first segment sits to the RIGHT of the logically-later ones.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        var json = await page.EvaluateAsync<string>(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            // Many Hebrew words → forced to wrap in the narrow body.
            const words = []; for (let i = 0; i < 14; i++) words.push('שלום' + i);
            const model = { documentId: 'r110', body: { blocks: [
                { id: 'p0', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r0', kind: 'text', text: words.join(' ') }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 320, height: 900, marginTop: 36, marginBottom: 36, marginLeft: 36, marginRight: 36 } });
            host.mount(root).setModel(model).setSelection({ blockId: 'p0', offset: 0 }).attachInput();
            host.render();
            const segs = host.getLayout().blocks.find(b => b.blockId === 'p0').segments
                .filter(s => (s.text || '').trim().length > 0);
            // group by line (rounded y)
            const byLine = {};
            segs.forEach(s => { const y = Math.round(s.rect.y); (byLine[y] = byLine[y] || []).push({ start: Number(s.start), x: Math.round(s.rect.x), dir: s.direction || 'ltr', text: s.text }); });
            const lines = Object.keys(byLine).map(Number).sort((a, b) => a - b).map(y => byLine[y]);
            // For each line: is the logical-first segment to the RIGHT of the logical-last? (RTL)
            const lineChecks = lines.map(segsOnLine => {
                if (segsOnLine.length < 2) return { n: segsOnLine.length, rtlOrdered: true, allRtl: segsOnLine.every(s => s.dir === 'rtl') };
                const sorted = segsOnLine.slice().sort((a, b) => a.start - b.start);
                const first = sorted[0]; const last = sorted[sorted.length - 1];
                return { n: segsOnLine.length, rtlOrdered: first.x > last.x, allRtl: segsOnLine.every(s => s.dir === 'rtl') };
            });
            return JSON.stringify({ lineCount: lines.length, lineChecks });
        }");
        var r = JsonDocument.Parse(json).RootElement;
        r.GetProperty("lineCount").GetInt32().Should().BeGreaterThan(1, "the long RTL paragraph wraps into multiple lines in the narrow column");
        var checks = r.GetProperty("lineChecks");
        var multiSegLines = 0;
        foreach (var c in checks.EnumerateArray())
        {
            c.GetProperty("allRtl").GetBoolean().Should().BeTrue("every segment on every wrapped line is RTL");
            if (c.GetProperty("n").GetInt32() >= 2)
            {
                multiSegLines++;
                c.GetProperty("rtlOrdered").GetBoolean().Should().BeTrue("on each wrapped line the logical-first RTL segment is placed rightmost (re-wrap reorders per line)");
            }
        }
        multiSegLines.Should().BeGreaterThan(0, "at least one wrapped line has multiple segments to prove per-line reordering");
        TestContext.WriteLine($"R.5.19 bidi re-wrap: RTL paragraph wrapped into {r.GetProperty("lineCount").GetInt32()} lines, each reordered RTL ({multiSegLines} multi-segment lines verified).");
    }

    [TestMethod]
    public async Task R111_OperationLogUndo_RealKeyboard_RevertsRunThenSnapshotForMarks()
    {
        // R.5.18 — operation-log undo end-to-end with the REAL keyboard: a typing run reverts as
        // ONE op-log step (replaying inverse ops, no whole-model snapshot) and redo restores it;
        // a subsequent BOLD edit undoes via snapshot. Hybrid op-log + snapshot in one stack.
        var page = await OpenHarnessAsync(width: 1440, height: 900);
        await page.EvaluateAsync(@"() => {
            const M = window.tmDocumentEditorModules;
            const root = document.getElementById('harness-root'); root.innerHTML = '';
            const model = { documentId: 'r111', body: { blocks: [
                { id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r', kind: 'text', text: '' }] } },
            ] } };
            const host = M.coreEngine.createRenderHost({ doc: document, pageSettings: { width: 794, height: 1123, marginTop: 72, marginBottom: 72, marginLeft: 72, marginRight: 72 } });
            host.mount(root).setModel(model).setSelection({ blockId: 'p1', offset: 0 }).attachInput();
            host.render(); host.focusInput();
            window.__host = host;
        }");
        Func<Task<string>> text = () => page.EvaluateAsync<string>(@"() => window.tmDocumentEditorModules.coreEngine ? (function(){ const b = window.__host.getSnapshot().model.body.blocks[0]; return b.content.runs.map(r => r.text || '').join(''); })() : ''");

        // Real keyboard typing run.
        await page.Keyboard.TypeAsync("Hello", new KeyboardTypeOptions { Delay = 20 });
        (await text()).Should().Be("Hello");
        var depth = await page.EvaluateAsync<int>("() => window.__host.getHistoryDepth().undo");
        depth.Should().Be(1, "the whole typing run is one op-log undo step");

        // Ctrl+Z reverts the entire run (op-log inverse replay); caret back to start.
        await page.Keyboard.PressAsync("Control+z");
        (await text()).Should().Be("", "op-log undo reverts the whole typed run");
        var caretAfterUndo = await page.EvaluateAsync<int>("() => window.__host.getCaret().offset");
        caretAfterUndo.Should().Be(0, "caret restored to the run's start");

        // Redo restores it.
        await page.Keyboard.PressAsync("Control+y");
        (await text()).Should().Be("Hello", "redo replays the run forward");

        // Now select + bold (snapshot undo), then undo reverts the mark (not the text).
        await page.EvaluateAsync(@"() => { const h = window.__host; h.setSelection({ blockId: 'p1', offset: 0 }); for (let i = 0; i < 5; i++) h.moveCaret('ArrowRight', true); h.toggleMark('bold'); }");
        var boldOn = await page.EvaluateAsync<bool>("() => window.__host.isMarkActive('bold')");
        boldOn.Should().BeTrue("bold applied to the selection");
        await page.Keyboard.PressAsync("Control+z");
        var boldAfterUndo = await page.EvaluateAsync<bool>("() => window.__host.isMarkActive('bold')");
        boldAfterUndo.Should().BeFalse("snapshot undo reverts the bold mark");
        (await text()).Should().Be("Hello", "the text is unchanged by the mark undo");

        TestContext.WriteLine("R.5.18 op-log undo (real keyboard): typing run reverts/redos as one op-log step; bold reverts via snapshot — hybrid stack.");
    }
}
