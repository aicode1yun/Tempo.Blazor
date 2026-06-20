// Phase D — layout/paragraph-layout-tree.mjs
// Pure helpers used by the paragraph layout engine to build the final tree of
// lines/segments/objects/caret stops.
//
// `paragraphRectFromLines(options, lines)` — bounding rect for a paragraph from
//   its laid-out lines; empty lines fall back to `{x,y,width, height: max(18, lineHeight)}`.
// `createInlineObjectLayoutFromSegment(block, segment, line)` — materialises a
//   `drawing` inline object record from a layout segment (`wrapMode` defaults to
//   `'Inline'`, `createsTextExclusion: false`); requires `normalizeWrapModeName`
//   to be injected (see factory).
// `createInlineObjectLayoutFromSegmentFactory({normalizeWrapModeName})` — factory.
// `firstScopeBlockId(scope)` — affectedScopeIds[0] || scope.blockId || null.
// `findLayoutBlock(layout, blockId)` — looks up a layout block by id; null-safe.
// `createLayoutObjectBlockFactory({normalizeImageObject})` →
//   `layoutObjectBlock(block, options, version)` — caption height heuristic:
//   max(16, min(48, captionLength * 0.6)); placeholder lines/segments arrays;
//   emits `before`/`after` caret stops at the rect boundaries.

import { asArray, clone, sortObject } from '../core/helpers.mjs';

export function paragraphRectFromLines(options, lines) {
    if (!lines.length) {
        return {
            x: options.x,
            y: options.y,
            width: options.width,
            height: Math.max(18, Number(options.lineHeight || 18)),
        };
    }
    const top = lines[0].rect.y;
    const bottom = lines.reduce(function (value, line) {
        return Math.max(value, line.rect.y + line.rect.height);
    }, top);
    return { x: options.x, y: top, width: options.width, height: Math.max(1, bottom - top) };
}

export function createInlineObjectLayoutFromSegmentFactory(options) {
    const opts = options || {};
    if (typeof opts.normalizeWrapModeName !== 'function') {
        throw new TypeError(
            'createInlineObjectLayoutFromSegmentFactory requires options.normalizeWrapModeName (function)');
    }
    const { normalizeWrapModeName } = opts;
    return function createInlineObjectLayoutFromSegment(block, segment, line) {
        const object = segment.object || {};
        const rect = segment.objectRect || segment.rect || {};
        const wrapMode = normalizeWrapModeName(object.wrapMode || 'Inline');
        return sortObject({
            blockId: (block && block.id) || segment.blockId || '',
            runId: segment.runId || null,
            objectId: segment.objectId || object.objectId || '',
            lineId: (line && line.id) || segment.lineId || null,
            inlineObject: true,
            kind: 'drawing',
            wrapMode,
            createsTextExclusion: false,
            rect: {
                x: Number(rect.x || 0) || 0,
                y: Number(rect.y || 0) || 0,
                width: Math.max(1, Number(rect.width || object.width || 1) || 1),
                height: Math.max(1, Number(rect.height || object.height || 1) || 1),
            },
            object: clone(object),
        });
    };
}

export function firstScopeBlockId(scope) {
    return (scope && scope.affectedScopeIds && scope.affectedScopeIds[0])
        || (scope && scope.blockId)
        || null;
}

export function findLayoutBlock(layout, blockId) {
    if (!layout || !blockId) return null;
    return asArray(layout.blocks).find(function (block) {
        return block.blockId === blockId;
    }) || null;
}

export function createLayoutObjectBlockFactory(options) {
    const opts = options || {};
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError(
            'createLayoutObjectBlockFactory requires options.normalizeImageObject (function)');
    }
    const { normalizeImageObject } = opts;
    return function layoutObjectBlock(block, layoutOpts, version) {
        const id = (block && block.id) || 'object';
        const object = block && block.type === 'image' ? normalizeImageObject(block) : null;
        const captionHeight = object && object.caption
            ? Math.max(16, Math.min(48, object.caption.length * 0.6))
            : 0;
        const height = object ? object.height + captionHeight : 80;
        const width = object ? object.width : layoutOpts.width;
        const rect = { x: layoutOpts.x, y: layoutOpts.y, width, height };
        return sortObject({
            ok: true,
            id: 'layout-' + id,
            layoutVersion: version,
            blockId: id,
            type: (block && block.type) || 'object',
            rect,
            lines: [],
            segments: [],
            caretStops: [
                {
                    blockId: id, offset: 0, affinity: 'before',
                    rect: { x: rect.x, y: rect.y, width: 1, height: rect.height },
                    objectBoundary: true,
                },
                {
                    blockId: id, offset: 1, affinity: 'after',
                    rect: { x: rect.x + rect.width, y: rect.y, width: 1, height: rect.height },
                    objectBoundary: true,
                },
            ],
            baselines: [],
            objectId: (block && block.content && block.content.objectId) || id,
        });
    };
}
