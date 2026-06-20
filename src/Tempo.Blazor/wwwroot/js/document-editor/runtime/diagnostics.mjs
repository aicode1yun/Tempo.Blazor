// Phase D — runtime/diagnostics.mjs
// Per-instance diagnostics state + timeline recorder. Used by the watchdog, the
// strict-mode performance pipeline, and the in-browser debug overlay.
//
// `createDiagnosticsState()` returns the canonical shape with empty arrays / zero
// versions / no last-valid snapshots.
// `ensureDiagnostics(inst)` lazy-initialises `inst.diagnostics` and repairs missing
// array fields (defensive against partial deserialisation).
// `recordTimeline(inst, kind, detail?)` appends a timestamped entry; trims to 300.

import { clone, sortObject } from '../core/helpers.mjs';

export const DIAGNOSTICS_TIMELINE_LIMIT = 300;

export function createDiagnosticsState() {
    return {
        modelVersion: 0,
        layoutVersion: 0,
        renderVersion: 0,
        selectionVersion: 0,
        timeline: [],
        lastErrors: [],
        watchdogFailures: [],
        debugWarnings: [],
        lastValidRenderHtml: '',
        lastValidLayout: null,
        lastValidSelection: null,
        lastValidSnapshot: null,
        forceLayoutFailure: false,
        forceRenderFailure: false,
        forceSelectionFailure: false,
    };
}

export function ensureDiagnostics(inst) {
    if (!inst) return null;
    if (!inst.diagnostics) inst.diagnostics = createDiagnosticsState();
    if (!Array.isArray(inst.diagnostics.timeline)) inst.diagnostics.timeline = [];
    if (!Array.isArray(inst.diagnostics.lastErrors)) inst.diagnostics.lastErrors = [];
    if (!Array.isArray(inst.diagnostics.watchdogFailures)) inst.diagnostics.watchdogFailures = [];
    if (!Array.isArray(inst.diagnostics.debugWarnings)) inst.diagnostics.debugWarnings = [];
    return inst.diagnostics;
}

export function recordTimeline(inst, kind, detail) {
    if (!inst) return null;
    const diagnostics = ensureDiagnostics(inst);
    const entry = sortObject({
        index: diagnostics.timeline.length + 1,
        kind: kind,
        detail: clone(detail || {}),
        at: Date.now(),
    });
    diagnostics.timeline.push(entry);
    if (diagnostics.timeline.length > DIAGNOSTICS_TIMELINE_LIMIT) {
        diagnostics.timeline.splice(
            0, diagnostics.timeline.length - DIAGNOSTICS_TIMELINE_LIMIT);
    }
    return entry;
}

export const DIAGNOSTICS_ERROR_LIMIT = 20;
export const DIAGNOSTICS_WATCHDOG_FAILURE_LIMIT = 20;

// Records an engine error against `inst.diagnostics.lastErrors` (trimmed to 20)
// AND mirrors it onto the timeline as an `error-recovery` entry. Sets `inst.lastError`
// to the code so the toolbar/status surface can read it.
export function recordDiagnosticError(inst, code, error, detail) {
    if (!inst) return null;
    const diagnostics = ensureDiagnostics(inst);
    const entry = sortObject({
        code: code || 'engine-error',
        message: String(error && error.message || error || code || 'engine-error'),
        detail: clone(detail || {}),
        at: Date.now(),
    });
    diagnostics.lastErrors.push(entry);
    if (diagnostics.lastErrors.length > DIAGNOSTICS_ERROR_LIMIT) {
        diagnostics.lastErrors.splice(
            0, diagnostics.lastErrors.length - DIAGNOSTICS_ERROR_LIMIT);
    }
    inst.lastError = entry.code;
    recordTimeline(inst, 'error-recovery', entry);
    return entry;
}

// Records a watchdog failure (extends `recordDiagnosticError`). Also pushes a
// `watchdog-recovery-active` debug warning when the failure count reaches 2,
// and updates the `data-debug-warning` attribute on `inst.root` so CSS hooks
// can surface a visible warning marker.
export function recordWatchdogFailure(inst, kind, error, detail) {
    if (!inst) return null;
    const diagnostics = ensureDiagnostics(inst);
    const entry = recordDiagnosticError(inst, kind + '-failure', error, detail);
    diagnostics.watchdogFailures.push(sortObject(Object.assign({}, entry, { kind: kind })));
    if (diagnostics.watchdogFailures.length > DIAGNOSTICS_WATCHDOG_FAILURE_LIMIT) {
        diagnostics.watchdogFailures.splice(
            0, diagnostics.watchdogFailures.length - DIAGNOSTICS_WATCHDOG_FAILURE_LIMIT);
    }
    if (diagnostics.watchdogFailures.length >= 2
        && diagnostics.debugWarnings.indexOf('watchdog-recovery-active') < 0) {
        diagnostics.debugWarnings.push('watchdog-recovery-active');
    }
    if (inst.root && typeof inst.root.toggleAttribute === 'function') {
        inst.root.toggleAttribute('data-debug-warning', diagnostics.debugWarnings.length > 0);
    }
    return entry;
}
