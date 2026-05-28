// Phase D — runtime/watchdog-helpers.mjs
// State machine constants + pure helpers for the runtime watchdog. The watchdog
// instance lifecycle (create/dispose/loadDocument wrap-up) remains in the legacy IIFE
// because it must reach into the `runtime` global (defined in the separate
// `document-editor.js` bootstrap file). These helpers cover everything that's pure.

import { asArray } from '../core/helpers.mjs';

// Watchdog states.
export const WD_READY = 'ready';
export const WD_RECOVERING = 'recovering';
export const WD_RECOVERED = 'recovered';
export const WD_FAILED = 'failed';

// Default tunables. Callers can override via options on the engine instance.
export const WD_DEFAULT_MAX_ATTEMPTS = 3;
export const WD_DEFAULT_BACKOFF_MS = 100;

// Maximum number of events we retain on a watchdog context (older events are dropped
// when the array grows past this).
export const WD_EVENT_HISTORY_LIMIT = 20;

// Compute the backoff for the Nth recovery attempt: `baseBackoffMs * 2^(attempt-1)`,
// clamped to a non-negative integer. `attempt` is 1-based; the first attempt yields
// `baseBackoffMs * 1`.
export function computeWatchdogBackoff(attempt, baseBackoffMs) {
    const base = Math.max(0, Number(baseBackoffMs || WD_DEFAULT_BACKOFF_MS));
    const safeAttempt = Math.max(1, Number(attempt || 1));
    return base * Math.pow(2, safeAttempt - 1);
}

// Deep-clone via JSON (best-effort — falls back to the original on error).
export function cloneWatchdogJson(value) {
    if (value == null) return value;
    try { return JSON.parse(JSON.stringify(value)); } catch { return value; }
}

// Parse a string snapshot (or pass through an object). Empty/null → null.
export function parseWatchdogJson(value) {
    if (value == null || value === '') return null;
    if (typeof value === 'string') {
        try { return JSON.parse(value); } catch { return value; }
    }
    return cloneWatchdogJson(value);
}

// The runtime wraps document snapshots in `{ Document: … }`. These helpers strip /
// re-apply that wrapper consistently.
export function unwrapWatchdogDocumentSnapshot(value) {
    if (!value || typeof value !== 'object') return value || null;
    return value.Document || value.document || value;
}

export function wrapWatchdogDocumentSnapshot(value) {
    if (!value || typeof value !== 'object') return value || null;
    if (value.Document || value.document) return value;
    return { Document: value };
}

// Run `fn`. On any error, return `fallback`. Used everywhere the watchdog inspects
// runtime/instance state where the runtime might not be ready yet.
export function safeCall(fn, fallback) {
    try {
        const value = fn();
        return value === undefined ? fallback : value;
    } catch {
        return fallback;
    }
}

export function watchdogNow() {
    try { return new Date().toISOString(); } catch { return ''; }
}

// Build the event detail record. Mirrors the legacy `_recordWatchdogEvent` exactly
// — both PascalCase and camelCase keys (the .NET side reads PascalCase, the JS side
// uses camelCase, and the wire format wants both).
export function buildWatchdogEventDetail(wd, eventName, source, error, extra) {
    const detail = Object.assign({
        event: eventName || '',
        Event: eventName || '',
        source: source || (wd && wd.lastErrorSource) || '',
        Source: source || (wd && wd.lastErrorSource) || '',
        state: (wd && wd.state) || '',
        State: (wd && wd.state) || '',
        attempt: (wd && wd.attempt) || 0,
        Attempt: (wd && wd.attempt) || 0,
        maxAttempts: (wd && wd.maxAttempts) || WD_DEFAULT_MAX_ATTEMPTS,
        MaxAttempts: (wd && wd.maxAttempts) || WD_DEFAULT_MAX_ATTEMPTS,
        backoffMs: (wd && wd.currentBackoffMs) || 0,
        BackoffMs: (wd && wd.currentBackoffMs) || 0,
        usedSnapshotFallback: !!(wd && wd.usedSnapshotFallback),
        UsedSnapshotFallback: !!(wd && wd.usedSnapshotFallback),
        errorMessage: error && error.message ? String(error.message) : (error ? String(error) : ''),
        ErrorMessage: error && error.message ? String(error.message) : (error ? String(error) : ''),
        timestamp: watchdogNow(),
        Timestamp: watchdogNow(),
    }, extra || {});
    return detail;
}

// Append a detail to the watchdog event log, trimming to WD_EVENT_HISTORY_LIMIT.
// Returns the detail for convenience.
export function recordWatchdogEvent(wd, eventName, source, error, extra) {
    if (!wd) return null;
    const detail = buildWatchdogEventDetail(wd, eventName, source, error, extra);
    wd.lastRecoveryDetail = detail;
    if (!Array.isArray(wd.events)) wd.events = [];
    wd.events.push(detail);
    if (wd.events.length > WD_EVENT_HISTORY_LIMIT) {
        wd.events = wd.events.slice(wd.events.length - WD_EVENT_HISTORY_LIMIT);
    }
    return detail;
}

// Factory for a fresh watchdog context. Used by the runtime wrapper when an engine
// instance is created. `options` may include WatchdogMaxAttempts / WatchdogBackoffMs
// (or camelCase equivalents) from the host config.
export function createWatchdogContext(rootEl, options, dotNetRef) {
    const opts = options || {};
    return {
        state: WD_READY,
        rootEl: rootEl || null,
        options: opts,
        dotNetRef: dotNetRef || null,
        stableSnapshot: null,
        events: [],
        lastRecoveryDetail: null,
        lastErrorSource: '',
        attempt: 0,
        maxAttempts: Number(opts.WatchdogMaxAttempts ?? opts.watchdogMaxAttempts ?? WD_DEFAULT_MAX_ATTEMPTS),
        baseBackoffMs: Number(opts.WatchdogBackoffMs ?? opts.watchdogBackoffMs ?? WD_DEFAULT_BACKOFF_MS),
        currentBackoffMs: 0,
        usedSnapshotFallback: false,
        forceRecoveryFailure: false,
        forceSnapshotFallback: false,
    };
}

// True if the watchdog state is past the point where new commands should be processed.
export function isWatchdogProcessing(wd) {
    return !!(wd && (wd.state === WD_RECOVERING || wd.state === WD_FAILED));
}

// True if the most recent event for this watchdog matches `eventName`. Useful for
// suppressing duplicate notifications.
export function lastEventWas(wd, eventName) {
    const events = (wd && asArray(wd.events)) || [];
    if (events.length === 0) return false;
    const last = events[events.length - 1];
    return last && (last.event === eventName || last.Event === eventName);
}
