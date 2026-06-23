// Phase D — runtime/strict-performance-helpers.mjs
// Small helpers used across the strict-mode performance pipeline:
//   - `strictPerformanceNow()` — millisecond timestamp from `performance.now()` if
//     available (browser), else `Date.now()` fallback for Node/headless tests.
//   - `normalizePerformanceRegion(value)` — coerces region aliases to one of the
//     canonical labels (Body/Header/Footer/TableCell/Image/Document).
//   - `activeRegionForSelection(selection)` — reads region from a selection snapshot.
//   - `activeRegionForInstance(inst)` — reads region from `.selection.region` or
//     `.activeFocusRegion`, defaulting to Body.
//   - `ensureStrictPerformanceStats(inst, createStats)` — lazy-initialises `inst.performanceStats`
//     using the supplied factory; returns the stats object.

import { asText } from '../core/helpers.mjs';

export function strictPerformanceNow() {
    if (typeof globalThis !== 'undefined'
        && globalThis.performance
        && typeof globalThis.performance.now === 'function') {
        return globalThis.performance.now();
    }
    return Date.now();
}

export function normalizePerformanceRegion(value) {
    const raw = asText(value || '').trim().toLowerCase();
    if (raw === 'header' || raw === 'headers') return 'Header';
    if (raw === 'footer' || raw === 'footers') return 'Footer';
    if (raw === 'tablecell' || raw === 'table-cell' || raw === 'cell') return 'TableCell';
    if (raw === 'image' || raw === 'object') return 'Image';
    if (raw === 'document') return 'Document';
    return 'Body';
}

export function activeRegionForSelection(selection) {
    const snapshot = selection || {};
    return normalizePerformanceRegion(
        snapshot.region || snapshot.Region
        || snapshot.activeRegion || snapshot.ActiveRegion
        || 'Body');
}

export function activeRegionForInstance(inst) {
    if (!inst) return 'Body';
    return normalizePerformanceRegion(
        inst.selection && (inst.selection.region || inst.selection.Region)
        || inst.activeFocusRegion
        || 'Body');
}

export function ensureStrictPerformanceStats(inst, createStats) {
    if (!inst) return null;
    if (!inst.performanceStats) {
        inst.performanceStats = typeof createStats === 'function' ? createStats() : {};
    }
    return inst.performanceStats;
}
