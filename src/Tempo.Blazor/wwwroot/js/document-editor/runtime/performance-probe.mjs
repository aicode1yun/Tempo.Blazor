// Phase D — runtime/performance-probe.mjs
// `createPerformanceProbe({ getEngineMetrics?, now?, getElementPrototype? })` →
//   `{ startCapture, stopCapture, isCapturing, clearAll, noteJsInteropCall,
//      getActiveCaptures }` — the global JS-side performance capture API that
//   `window.tmDocumentEditorPerformance` exposes.
//
// Each capture records forced-reflow count (by temporarily patching
// `Element.prototype.getBoundingClientRect/getClientRects`) and engine metric
// deltas. The reflow patch is installed on the first `startCapture` and removed
// when all captures are stopped (`_maybeUninstallReflowPatch`).
//
// Injected deps (all optional with sensible defaults):
//   `getEngineMetrics(instanceId)` — returns engine debug metrics object.
//   `now()` — high-res timestamp; defaults to `performance.now()` or `Date.now()`.
//   `getElementPrototype()` — returns `Element.prototype` for patching; defaults to
//     `globalThis.Element?.prototype`. Inject a stub for Node.js tests.

const METRIC_KEYS = [
    'KeyDownCount', 'BeforeInputCount', 'InputDomApplyCount',
    'FullRenderCount', 'PartialRenderCount', 'RenderSwapCount', 'FullRenderSwapCount',
    'ModelCommitCount', 'BlazorInteropCallCount', 'BlazorCallbackDuringTypingCount',
    'LayoutPassCount', 'LayoutPassTotalMs', 'RenderPassCount', 'RenderPassTotalMs',
    'InputOperationCount', 'InputOperationTotalMs',
    'TypingLatencyCount', 'TypingLatencyTotalMs',
];

export function createPerformanceProbe(options) {
    const opts = options || {};
    const getEngineMetrics = typeof opts.getEngineMetrics === 'function'
        ? opts.getEngineMetrics
        : (instanceId) => {
            const engine = globalThis.tmDocumentEditorEngine;
            if (!engine || typeof engine.getDebugMetrics !== 'function') return null;
            try { return engine.getDebugMetrics(instanceId) || null; } catch { return null; }
        };
    const now = typeof opts.now === 'function'
        ? opts.now
        : () => {
            if (typeof performance !== 'undefined' && typeof performance.now === 'function') return performance.now();
            return Date.now();
        };
    const getElementPrototype = typeof opts.getElementPrototype === 'function'
        ? opts.getElementPrototype
        : () => globalThis.Element && globalThis.Element.prototype;

    const captures = new Map();
    let reflowCount = 0;
    let jsInteropCount = 0;
    let rectPatched = false;
    let originalGetBoundingClientRect = null;
    let originalGetClientRects = null;

    function shallowClone(value) {
        if (!value || typeof value !== 'object') return value;
        const copy = {};
        Object.keys(value).forEach(function (key) { copy[key] = value[key]; });
        return copy;
    }

    function diffNumber(after, before, key) {
        const a = Number((after && after[key]) || 0) || 0;
        const b = Number((before && before[key]) || 0) || 0;
        return Math.max(0, a - b);
    }

    function ensureReflowPatchInstalled() {
        if (rectPatched) return;
        const proto = getElementPrototype();
        if (!proto) return;
        originalGetBoundingClientRect = proto.getBoundingClientRect;
        originalGetClientRects = proto.getClientRects;
        if (typeof originalGetBoundingClientRect !== 'function') {
            originalGetBoundingClientRect = null;
            return;
        }
        rectPatched = true;
        proto.getBoundingClientRect = function () {
            reflowCount++;
            return originalGetBoundingClientRect.apply(this, arguments);
        };
        if (typeof originalGetClientRects === 'function') {
            proto.getClientRects = function () {
                reflowCount++;
                return originalGetClientRects.apply(this, arguments);
            };
        }
    }

    function maybeUninstallReflowPatch() {
        if (!rectPatched || captures.size > 0) return;
        const proto = getElementPrototype();
        if (proto) {
            if (originalGetBoundingClientRect) proto.getBoundingClientRect = originalGetBoundingClientRect;
            if (originalGetClientRects) proto.getClientRects = originalGetClientRects;
        }
        originalGetBoundingClientRect = null;
        originalGetClientRects = null;
        rectPatched = false;
    }

    function startCapture(instanceId, label) {
        const id = String(instanceId || '');
        if (!id) throw new Error('createPerformanceProbe.startCapture: instanceId is required.');
        ensureReflowPatchInstalled();
        const capture = {
            instanceId: id,
            label: String(label || 'capture'),
            startedAt: now(),
            reflowStart: reflowCount,
            interopStart: jsInteropCount,
            beforeMetrics: shallowClone(getEngineMetrics(id)),
        };
        captures.set(id, capture);
        return { ok: true, label: capture.label, instanceId: id };
    }

    function stopCapture(instanceId) {
        const id = String(instanceId || '');
        const capture = captures.get(id);
        if (!capture) return null;
        captures.delete(id);
        const elapsedMs = Math.max(0, now() - capture.startedAt);
        const after = getEngineMetrics(id);
        const before = capture.beforeMetrics;
        const report = {
            InstanceId: id,
            Label: capture.label,
            ElapsedMs: elapsedMs,
            ForcedReflowCount: Math.max(0, reflowCount - capture.reflowStart),
            JsInteropCallCount: Math.max(0, jsInteropCount - capture.interopStart),
        };
        METRIC_KEYS.forEach(function (key) {
            report[key] = diffNumber(after, before, key);
        });
        report.MaxTypingBatchSize = Number((after && after.MaxTypingBatchSize) || 0) || 0;
        report.ActiveRegion = (after && after.ActiveRegion) || 'Body';
        maybeUninstallReflowPatch();
        return report;
    }

    function isCapturing(instanceId) {
        return captures.has(String(instanceId || ''));
    }

    function clearAll() {
        captures.clear();
        reflowCount = 0;
        jsInteropCount = 0;
        maybeUninstallReflowPatch();
    }

    function noteJsInteropCall(count) {
        jsInteropCount += Math.max(0, Number(count || 1) || 0);
    }

    function getActiveCaptures() {
        const ids = [];
        captures.forEach(function (_value, key) { ids.push(key); });
        return ids;
    }

    return {
        startCapture,
        stopCapture,
        isCapturing,
        clearAll,
        noteJsInteropCall,
        getActiveCaptures,
    };
}
