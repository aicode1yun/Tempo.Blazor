// Phase D — layout/wrap-snapshot-intervals.mjs
// Test/inspection helpers for the wrap-layout snapshot. Shape-compatible with the
// runtime's `normalizeManagerInterval`, kept as a separate function in the IIFE so
// that the snapshot output never accidentally leaks runtime-only fields.
//
// `normalizeWrapSnapshotInterval(interval, atY, lineHeight, extra)` — canonical
//   {x,y,width,height,...extra} interval shape; clamps width≥0, height≥1.
// `collectBlockedIntervalsForWrapSnapshot(exclusions, atY, lineHeight, body, minWidth)`
//   — for each exclusion: walks `blockedIntervalsForExclusionGeometry`, stitches
//   objectId/blockId/wrapMode/wrapSide via the `extra` slot, sorts left→right
//   then by width ascending.

import { asArray, sortObject } from '../core/helpers.mjs';
import { normalizeWrapModeName, normalizeWrapSideName } from '../objects/wrap-modes.mjs';
import { blockedIntervalsForExclusionGeometry } from './blocked-intervals.mjs';

export function normalizeWrapSnapshotInterval(interval, atY, lineHeight, extra) {
    const rawY = interval ? (interval.y ?? interval.Y) : atY;
    return sortObject(Object.assign({
        x: Number((interval && (interval.x ?? interval.X)) || 0) || 0,
        y: Number(rawY ?? atY) || 0,
        width: Math.max(0,
            Number((interval && (interval.width ?? interval.Width)) || 0) || 0),
        height: Math.max(1,
            Number((interval && (interval.height ?? interval.Height)) || lineHeight)
            || lineHeight),
    }, extra || {}));
}

export function collectBlockedIntervalsForWrapSnapshot(
    exclusions, atY, lineHeight, body, minWidth) {
    const blocked = [];
    asArray(exclusions).forEach(function (exclusion) {
        blockedIntervalsForExclusionGeometry(exclusion, atY, lineHeight, body, minWidth)
            .forEach(function (interval) {
                blocked.push(normalizeWrapSnapshotInterval(interval, atY, lineHeight, {
                    objectId: (exclusion && (exclusion.objectId || exclusion.ObjectId)) || '',
                    blockId: (exclusion && (exclusion.blockId || exclusion.BlockId)) || '',
                    wrapMode: normalizeWrapModeName(
                        exclusion && (exclusion.wrapMode || exclusion.WrapMode)),
                    wrapSide: normalizeWrapSideName(
                        exclusion && (exclusion.wrapSide || exclusion.WrapSide)),
                }));
            });
    });
    return blocked.sort(function (a, b) {
        return a.x - b.x || a.width - b.width;
    });
}
