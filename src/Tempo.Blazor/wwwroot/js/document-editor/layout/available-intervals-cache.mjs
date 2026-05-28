// Phase D — layout/available-intervals-cache.mjs
// LRU-ish cache around `createTextExclusionManager().getAvailableIntervals(…)` so the
// expensive polygon scan / interval merge does not re-run every line layout call.
//
// `availableIntervalsCacheNumber(value)` — quantize a number to 3 decimal places so
//   tiny floating-point drift doesn't bust cache keys.
// `createAvailableIntervalsCacheKey(lineY, lineHeight, body, exclusions, minWidth,
//   scopeOptions)` — JSON-stringified canonical signature (scope, body rect,
//   each exclusion's rect/polygon/distances/scopeKey/wrapMode/wrapSide/version).
// `createAvailableIntervalsCacheStats()` — fresh counters record.
// `ensureAvailableIntervalsCacheStats(exclusions)` — sticks stats onto the
//   exclusions array as a hidden property (returns a fresh record when the host
//   array can't accept a property, e.g. frozen arrays).
// `getAvailableIntervalsCacheStats(exclusions)` — read-only clone of the stats.
// `ensureAvailableIntervalsCache(exclusions)` — hidden Map on the exclusions array.
// `resetAvailableIntervalsCache(exclusions, reason)` — clears the cache and bumps
//   the `cacheClears` counter with `reason`.
// `getAvailableIntervals(y, height, bodyFrame, exclusions, minReadableWidth,
//   scopeOptions)` — public entry. Cache size cap = 256 entries.

import { asArray, asText, clone } from '../core/helpers.mjs';
import { rectFromGeometry } from '../objects/geometry.mjs';
import { normalizeWrapModeName, normalizeWrapSideName } from '../objects/wrap-modes.mjs';
import { createTextExclusionScopeDescriptor } from './text-exclusion-scope.mjs';
import { createTextExclusionManager } from './text-exclusion-manager.mjs';

export function availableIntervalsCacheNumber(value) {
    const number = Number(value || 0) || 0;
    return Math.round(number * 1000) / 1000;
}

export function createAvailableIntervalsCacheKey(
    lineY, lineHeight, body, exclusions, minWidth, scopeOptions) {
    const scopeDescriptor = createTextExclusionScopeDescriptor(scopeOptions || {});
    const signature = asArray(exclusions).map(function (exclusion) {
        const rect = rectFromGeometry(exclusion && (exclusion.rect || exclusion.Rect));
        const polygon = asArray(exclusion && (exclusion.polygon || exclusion.Polygon))
            .map(function (point) {
                return [
                    availableIntervalsCacheNumber(point && (point.x ?? point.X)),
                    availableIntervalsCacheNumber(point && (point.y ?? point.Y)),
                ];
            });
        return {
            allowOverlap: !!(exclusion
                && (exclusion.allowOverlap === true || exclusion.AllowOverlap === true)),
            objectId: asText((exclusion && (exclusion.objectId || exclusion.ObjectId)) || ''),
            blockId: asText((exclusion && (exclusion.blockId || exclusion.BlockId)) || ''),
            scopeKey: asText((exclusion && (exclusion.scopeKey || exclusion.ScopeKey)) || ''),
            wrapMode: normalizeWrapModeName(
                exclusion && (exclusion.wrapMode || exclusion.WrapMode)),
            wrapSide: normalizeWrapSideName(
                exclusion && (exclusion.wrapSide || exclusion.WrapSide)),
            polygonVersion: asText(
                (exclusion && (exclusion.polygonVersion || exclusion.PolygonVersion
                    || exclusion.wrapContourVersion || exclusion.WrapContourVersion)) || ''),
            distances: [
                availableIntervalsCacheNumber(
                    exclusion && (exclusion.distanceLeft ?? exclusion.DistanceLeft)),
                availableIntervalsCacheNumber(
                    exclusion && (exclusion.distanceRight ?? exclusion.DistanceRight)),
                availableIntervalsCacheNumber(
                    exclusion && (exclusion.distanceTop ?? exclusion.DistanceTop)),
                availableIntervalsCacheNumber(
                    exclusion && (exclusion.distanceBottom ?? exclusion.DistanceBottom)),
            ],
            rect: [
                availableIntervalsCacheNumber(rect.x),
                availableIntervalsCacheNumber(rect.y),
                availableIntervalsCacheNumber(rect.width),
                availableIntervalsCacheNumber(rect.height),
            ],
            polygon,
        };
    });
    return JSON.stringify({
        y: availableIntervalsCacheNumber(lineY),
        height: availableIntervalsCacheNumber(lineHeight),
        minWidth: availableIntervalsCacheNumber(minWidth),
        body: [
            availableIntervalsCacheNumber(body.x),
            availableIntervalsCacheNumber(body.y),
            availableIntervalsCacheNumber(body.width),
            availableIntervalsCacheNumber(body.height),
        ],
        scopeKey: scopeDescriptor.enabled === true ? scopeDescriptor.scopeKey : '',
        exclusions: signature,
    });
}

export function createAvailableIntervalsCacheStats() {
    return {
        calls: 0,
        cacheHits: 0,
        cacheMisses: 0,
        cacheClears: 0,
        cacheEntries: 0,
        managerBuilds: 0,
        lineResolveCount: 0,
        exclusionScanCount: 0,
        blockedGeometryComputeCount: 0,
        polygonComputationCount: 0,
        lastCacheKey: '',
        lastCacheEvent: '',
    };
}

export function ensureAvailableIntervalsCacheStats(exclusions) {
    if (!Array.isArray(exclusions)) return createAvailableIntervalsCacheStats();
    let stats = exclusions.__tmAvailableIntervalsCacheStats;
    if (stats) return stats;
    stats = createAvailableIntervalsCacheStats();
    try {
        Object.defineProperty(exclusions, '__tmAvailableIntervalsCacheStats', {
            value: stats,
            configurable: true,
            enumerable: false,
            writable: true,
        });
    } catch {
        return stats;
    }
    return stats;
}

export function getAvailableIntervalsCacheStats(exclusions) {
    const stats = Array.isArray(exclusions) && exclusions.__tmAvailableIntervalsCacheStats
        ? exclusions.__tmAvailableIntervalsCacheStats
        : createAvailableIntervalsCacheStats();
    stats.cacheEntries = Array.isArray(exclusions) && exclusions.__tmAvailableIntervalsCache
        ? exclusions.__tmAvailableIntervalsCache.size
        : Number(stats.cacheEntries || 0);
    return clone(stats);
}

export function ensureAvailableIntervalsCache(exclusions) {
    if (!Array.isArray(exclusions)) return null;
    let cache = exclusions.__tmAvailableIntervalsCache;
    if (cache && typeof cache.get === 'function' && typeof cache.set === 'function') return cache;
    cache = new Map();
    try {
        Object.defineProperty(exclusions, '__tmAvailableIntervalsCache', {
            value: cache,
            configurable: true,
            enumerable: false,
            writable: true,
        });
        return cache;
    } catch {
        return null;
    }
}

export function resetAvailableIntervalsCache(exclusions, reason) {
    if (!Array.isArray(exclusions)) return getAvailableIntervalsCacheStats(exclusions);
    const cache = ensureAvailableIntervalsCache(exclusions);
    const stats = ensureAvailableIntervalsCacheStats(exclusions);
    if (cache) cache.clear();
    stats.cacheClears++;
    stats.cacheEntries = 0;
    stats.lastCacheEvent = asText(reason || 'manual-reset');
    return getAvailableIntervalsCacheStats(exclusions);
}

export function getAvailableIntervals(
    y, height, bodyFrame, exclusions, minReadableWidth, scopeOptions) {
    const lineY = Number(y || 0);
    const lineHeight = Math.max(1, Number(height || 1) || 1);
    const body = rectFromGeometry(bodyFrame || { x: 0, y: 0, width: 640, height: 900 });
    const minWidth = Math.max(1, Number(minReadableWidth || 48) || 48);
    const cache = ensureAvailableIntervalsCache(exclusions);
    const stats = ensureAvailableIntervalsCacheStats(exclusions);
    stats.calls++;
    const managerOptions = Object.assign(
        { minReadableWidth: minWidth, intervalCacheStats: stats },
        scopeOptions || {});
    const cacheKey = cache
        ? createAvailableIntervalsCacheKey(
            lineY, lineHeight, body, exclusions, minWidth, managerOptions)
        : '';
    stats.lastCacheKey = cacheKey;
    if (cache && cache.has(cacheKey)) {
        stats.cacheHits++;
        stats.cacheEntries = cache.size;
        stats.lastCacheEvent = 'hit';
        return clone(cache.get(cacheKey));
    }
    stats.cacheMisses++;
    stats.lastCacheEvent = 'miss';
    const available = createTextExclusionManager(exclusions, body, managerOptions)
        .getAvailableIntervals(lineY, lineHeight, minWidth);
    if (cache) {
        if (cache.size > 256) {
            cache.clear();
            stats.cacheClears++;
        }
        cache.set(cacheKey, clone(available));
        stats.cacheEntries = cache.size;
    }
    return available;
}
