// Phase D — render/render-snapshot.mjs
// `flattenLayoutSegments(layout)` — concatenates `block.segments` across all
//   `layout.blocks` into a single array (preserves order).
// `stableChecksum(value)` — deterministic FNV-like 32-bit hash over the value
//   stringified via `sortObject`-stable JSON. Returns `<8-hex>-<length>`.
// `createRenderSnapshot(model, layout, selection, options?)` — bundles the model,
//   layout and selection into an immutable snapshot used by the atomic renderer.
//   Computes `checksum` + a derived `fingerprint` (checksum + block/segment counts)
//   so the renderer can short-circuit identical frames. `affectedScopes` falls back
//   to `layout.debug.invalidatedScopes` when the caller does not supply it.

import { asArray, sortObject } from '../core/helpers.mjs';

export function flattenLayoutSegments(layout) {
    const result = [];
    asArray(layout && layout.blocks).forEach(function (block) {
        asArray(block.segments).forEach(function (segment) {
            result.push(segment);
        });
    });
    return result;
}

export function stableChecksum(value) {
    const text = JSON.stringify(sortObject(value || {}));
    let hash = 2166136261;
    for (let i = 0; i < text.length; i++) {
        hash ^= text.charCodeAt(i);
        hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
    }
    return ('00000000' + (hash >>> 0).toString(16)).slice(-8) + '-' + text.length;
}

export function createRenderSnapshot(model, layout, selection, options) {
    const opts = options || {};
    const blocks = asArray(layout && layout.blocks);
    const segments = flattenLayoutSegments(layout);
    const affectedScopes = asArray(
        opts.affectedScopes || opts.AffectedScopes
        || (layout && layout.debug && layout.debug.invalidatedScopes));
    const rawModelVersion = opts.modelVersion ?? opts.ModelVersion
        ?? (model && (model.version ?? model.Version)) ?? 1;
    const rawLayoutVersion = opts.layoutVersion ?? opts.LayoutVersion
        ?? (layout && layout.layoutVersion) ?? 1;
    const rawSelectionVersion = opts.selectionVersion ?? opts.SelectionVersion
        ?? (selection && selection.version) ?? 1;
    const modelVersion = Number(rawModelVersion) || 1;
    const layoutVersionValue = Number(rawLayoutVersion) || 1;
    const selectionVersion = Number(rawSelectionVersion) || 1;
    // R.4.9.3b — incremental path: skip the O(total segments) flatten + checksum. The atomic
    // renderer never reads `segments`/`checksum`; it diffs per block (B1/B2) on its own, so a
    // cheap monotonic fingerprint is sufficient. This keeps the per-keystroke snapshot O(1).
    if (opts.cheap === true) {
        // Plain by-reference object — NO `sortObject` (it deep-clones the whole layout+model = O(N),
        // the actual per-keystroke bottleneck). The renderer reads layout/model/selection read-only.
        return {
            ok: true,
            modelVersion,
            layoutVersion: layoutVersionValue,
            selectionVersion,
            affectedScopes,
            checksum: '',
            fingerprint: 'inc-' + modelVersion + '-' + selectionVersion + '-' + (opts.dirtyBlockId || ''),
            model,
            layout,
            selection: selection || null,
            debug: { blockCount: blocks.length, incremental: true },
        };
    }
    const fingerprintSource = {
        documentId: model && model.documentId,
        modelVersion,
        layoutVersion: layoutVersionValue,
        selectionVersion,
        affectedScopes,
        blockIds: blocks.map(function (block) { return block.blockId; }),
        segmentIds: segments.map(function (segment) {
            return segment.id + ':' + segment.start + ':' + segment.end;
        }),
    };
    const checksum = stableChecksum(fingerprintSource);
    return sortObject({
        ok: true,
        modelVersion,
        layoutVersion: layoutVersionValue,
        selectionVersion,
        affectedScopes,
        checksum,
        fingerprint: checksum + '-' + blocks.length + '-' + segments.length,
        model,
        layout,
        selection: selection || null,
        debug: {
            blockCount: blocks.length,
            segmentCount: segments.length,
            affectedScopes,
            checksum,
        },
    });
}
