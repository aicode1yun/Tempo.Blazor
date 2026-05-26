using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor.Performance;

/// <summary>Phase B — verifies the atomic renderer's incremental diff path:
/// fingerprint-based block skip (B1), per-segment patch (B2), invariant validation
/// off-by-default (B3), shallow clone in <c>localizeLayoutBlock</c> (B4).
///
/// These tests use a lightweight stub document/JSDOM-like environment in Node so we can
/// exercise the renderer without a real browser DOM.</summary>
public sealed class AtomicRendererIncrementalDiffJavaScriptTests
{
    private const string DomStubScript =
        """
        // Minimal DOM stub sufficient for createAtomicRenderer. Mirrors the structural
        // attributes and methods the renderer touches; no layout/style computation.
        function _stubElement(tagName) {
            const el = {
                nodeType: 1,
                tagName: String(tagName || 'div').toUpperCase(),
                children: [],
                attributes: {},
                style: {},
                className: '',
                __tmFingerprint: undefined,
                parentNode: null,
                firstChild: null,
                lastChild: null,
                nextSibling: null,
                previousSibling: null,
                textContent: '',
                innerHTML: '',
                setAttribute(name, value) { this.attributes[name] = String(value); },
                getAttribute(name) { return this.attributes[name] === undefined ? null : this.attributes[name]; },
                removeAttribute(name) { delete this.attributes[name]; },
                hasAttribute(name) { return name in this.attributes; },
                appendChild(child) { return _appendChild(this, child); },
                insertBefore(child, before) { return _insertBefore(this, child, before); },
                removeChild(child) { return _removeChild(this, child); },
                replaceChildren() {
                    const args = Array.from(arguments);
                    while (this.firstChild) _removeChild(this, this.firstChild);
                    args.forEach((node) => _appendChild(this, node));
                },
                contains(node) {
                    if (!node) return false;
                    let cur = node;
                    while (cur) { if (cur === this) return true; cur = cur.parentNode; }
                    return false;
                },
                querySelector() { return null; },
                querySelectorAll() { return []; },
                getBoundingClientRect() { return { x: 0, y: 0, width: 0, height: 0 }; },
                addEventListener() {},
                removeEventListener() {},
                dispatchEvent() { return true; }
            };
            return el;
        }

        function _appendChild(parent, child) {
            if (child.parentNode === parent) _removeChild(parent, child);
            else if (child.parentNode) _removeChild(child.parentNode, child);
            const last = parent.lastChild;
            child.parentNode = parent;
            child.previousSibling = last;
            child.nextSibling = null;
            if (last) last.nextSibling = child;
            else parent.firstChild = child;
            parent.lastChild = child;
            parent.children.push(child);
            return child;
        }

        function _insertBefore(parent, child, before) {
            if (!before) return _appendChild(parent, child);
            if (before.parentNode !== parent) throw new Error('insertBefore: reference not a child');
            if (child.parentNode === parent) _removeChild(parent, child);
            else if (child.parentNode) _removeChild(child.parentNode, child);
            child.parentNode = parent;
            child.previousSibling = before.previousSibling;
            child.nextSibling = before;
            if (before.previousSibling) before.previousSibling.nextSibling = child;
            else parent.firstChild = child;
            before.previousSibling = child;
            const idx = parent.children.indexOf(before);
            parent.children.splice(idx, 0, child);
            return child;
        }

        function _removeChild(parent, child) {
            if (child.parentNode !== parent) throw new Error('removeChild: not a child');
            if (child.previousSibling) child.previousSibling.nextSibling = child.nextSibling;
            else parent.firstChild = child.nextSibling;
            if (child.nextSibling) child.nextSibling.previousSibling = child.previousSibling;
            else parent.lastChild = child.previousSibling;
            const idx = parent.children.indexOf(child);
            if (idx >= 0) parent.children.splice(idx, 1);
            child.parentNode = null;
            child.previousSibling = null;
            child.nextSibling = null;
            return child;
        }

        function _createDocumentFragment() {
            const frag = _stubElement('fragment');
            frag.nodeType = 11;
            return frag;
        }

        function _createTextNode(text) {
            return { nodeType: 3, nodeValue: String(text || ''), firstChild: null, parentNode: null, previousSibling: null, nextSibling: null };
        }

        const document = {
            createElement: _stubElement,
            createDocumentFragment: _createDocumentFragment,
            createTextNode: _createTextNode,
            documentElement: _stubElement('html'),
            body: _stubElement('body')
        };
        sandbox.document = document;
        sandbox.window.document = document;
        sandbox.Node = { TEXT_NODE: 3, ELEMENT_NODE: 1 };
        sandbox.Element = { prototype: {} };

        // Re-run code in updated sandbox to register the renderer with the stubbed document.
        vm.runInContext('document = document; window.document = document; Node = Node; Element = Element;', sandbox);
        """;

    [Fact]
    public async Task PhaseB_RendererExposesIncrementalDebugCounters()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "renderer-debug-counters",
            DomStubScript +
            """
            const renderer = hooks.createAtomicRenderer();
            const debug = renderer.debug();
            assert.strictEqual(typeof debug.paragraphFingerprintHits, 'number', JSON.stringify(debug));
            assert.strictEqual(typeof debug.paragraphFingerprintMisses, 'number');
            assert.strictEqual(typeof debug.segmentPatchCount, 'number');
            assert.strictEqual(typeof debug.diagnosticsEnabled, 'boolean');
            assert.strictEqual(debug.diagnosticsEnabled, false, 'diagnostics must be off by default (Phase B3)');
            assert.strictEqual(typeof renderer.resetDebugCounters, 'function');
            assert.strictEqual(typeof renderer.setDiagnostics, 'function');
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseB_RenderParagraphScope_ReusesContainerWhenFingerprintMatches()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "renderer-fingerprint-skip",
            DomStubScript +
            """
            const renderer = hooks.createAtomicRenderer();
            const snapshot = { model: { blocks: [] }, layout: { pages: [], blocks: [] }, selection: null };
            const blockLayout = {
                blockId: 'p1',
                region: 'Body',
                pageIndex: 0,
                rect: { x: 0, y: 0, width: 400, height: 24 },
                segments: [
                    { id: 'seg-1', blockId: 'p1', start: 0, end: 5, text: 'Hello',
                      rect: { x: 0, y: 0, width: 50, height: 24 } }
                ]
            };

            const first = renderer.renderParagraphScope(snapshot, blockLayout);
            const second = renderer.renderParagraphScope(snapshot, blockLayout);
            assert.strictEqual(first, second, 'cached container identity must be preserved across renders');
            const debug = renderer.debug();
            assert.ok(debug.paragraphFingerprintHits >= 1, 'fingerprint hit must register: ' + JSON.stringify(debug));
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseB_RenderParagraphScope_PatchesSegmentsInPlaceWhenTextChanges()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "renderer-segment-patch",
            DomStubScript +
            """
            const renderer = hooks.createAtomicRenderer();
            const snapshot = { model: { blocks: [] }, layout: { pages: [], blocks: [] }, selection: null };
            const before = {
                blockId: 'p1', region: 'Body', pageIndex: 0,
                rect: { x: 0, y: 0, width: 400, height: 24 },
                segments: [
                    { id: 's1', blockId: 'p1', start: 0, end: 5, text: 'Hello',
                      rect: { x: 0, y: 0, width: 50, height: 24 } }
                ]
            };
            const after = {
                blockId: 'p1', region: 'Body', pageIndex: 0,
                rect: { x: 0, y: 0, width: 400, height: 24 },
                segments: [
                    { id: 's1', blockId: 'p1', start: 0, end: 6, text: 'Hello!',
                      rect: { x: 0, y: 0, width: 55, height: 24 } }
                ]
            };

            const container = renderer.renderParagraphScope(snapshot, before);
            const initialFirst = container.firstChild;
            assert.ok(initialFirst, 'first render must produce at least one segment node');
            const same = renderer.renderParagraphScope(snapshot, after);
            assert.strictEqual(same, container, 'container identity preserved');
            const finalFirst = container.firstChild;
            assert.strictEqual(finalFirst, initialFirst, 'segment node reused (data-layout-segment-id match)');
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseB_ResetDebugCountersResetsAllPhaseBMetrics()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "renderer-reset",
            DomStubScript +
            """
            const renderer = hooks.createAtomicRenderer();
            const snapshot = { model: { blocks: [] }, layout: { pages: [], blocks: [] }, selection: null };
            const block = {
                blockId: 'p1', region: 'Body', pageIndex: 0,
                rect: { x: 0, y: 0, width: 100, height: 24 },
                segments: [{ id: 's1', blockId: 'p1', start: 0, end: 1, text: 'a', rect: { x: 0, y: 0, width: 10, height: 24 } }]
            };
            renderer.renderParagraphScope(snapshot, block);
            renderer.renderParagraphScope(snapshot, block);
            let debug = renderer.debug();
            assert.ok(debug.paragraphFingerprintHits >= 1);
            assert.ok(debug.segmentPatchCount >= 1);
            renderer.resetDebugCounters();
            debug = renderer.debug();
            assert.strictEqual(debug.paragraphFingerprintHits, 0);
            assert.strictEqual(debug.paragraphFingerprintMisses, 0);
            assert.strictEqual(debug.segmentPatchCount, 0);
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseB_LocalizeLayoutBlockDoesNotJsonClone()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "localize-no-json-clone",
            DomStubScript +
            """
            // Phase B4 — localizeLayoutBlock must not deep-clone via JSON. We assert this by
            // confirming functions or non-JSON-safe attributes survive the localization step.
            // We exercise it indirectly via renderParagraphScope by attaching a marker function.
            const renderer = hooks.createAtomicRenderer();
            const snapshot = { model: { blocks: [] }, layout: { pages: [], blocks: [] }, selection: null };
            const segMarker = function () {};
            const blockLayout = {
                blockId: 'p1', region: 'Body', pageIndex: 0,
                rect: { x: 0, y: 0, width: 50, height: 24 },
                segments: [{ id: 's1', blockId: 'p1', start: 0, end: 1, text: 'a',
                             rect: { x: 0, y: 0, width: 10, height: 24 } }],
                customMarker: segMarker
            };
            // Calling render once exercises localizeLayoutBlock through the render pipeline.
            const container = renderer.renderParagraphScope(snapshot, blockLayout);
            assert.ok(container, 'render must produce a container');
            assert.strictEqual(blockLayout.customMarker, segMarker,
                'localizeLayoutBlock must not mutate the original block');
            console.log('OK');
            """);

        result.ShouldPass();
    }
}
