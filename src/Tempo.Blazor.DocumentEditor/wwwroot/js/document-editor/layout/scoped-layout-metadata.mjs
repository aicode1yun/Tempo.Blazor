// Phase D — layout/scoped-layout-metadata.mjs
// `createScopedLayoutMetadataDecorator({createBlockIndexContext})` factory →
//   `decorateScopedLayoutMetadata(layout, context)` — mutates `layout` in place,
//   stamping `region/headerFooterId/tableId/cellId/columnIndex/pageIndex` from the
//   block-index `context` onto the layout root and every nested line/range/segment/
//   caret stop/baseline/inline object/exclusion. `null` layout is treated as `{}`
//   so callers can chain unconditionally. Returns the mutated `layout` object.

import { asArray } from '../core/helpers.mjs';

export function createScopedLayoutMetadataDecorator(options) {
    const opts = options || {};
    if (typeof opts.createBlockIndexContext !== 'function') {
        throw new TypeError(
            'createScopedLayoutMetadataDecorator requires options.createBlockIndexContext (function)');
    }
    const { createBlockIndexContext } = opts;

    return function decorateScopedLayoutMetadata(layout, context) {
        const scoped = layout || {};
        const ctx = createBlockIndexContext(context);
        const pageIndex = Number(
            ctx.pageIndex ?? ctx.PageIndex ?? scoped.pageIndex ?? 0) || 0;
        scoped.region = ctx.region;
        scoped.headerFooterId = ctx.headerFooterId || null;
        scoped.tableId = ctx.tableId || null;
        scoped.cellId = ctx.cellId || null;
        scoped.columnIndex = ctx.columnIndex ?? null;
        scoped.pageIndex = pageIndex;

        function apply(item) {
            if (!item) return;
            item.region = ctx.region;
            item.headerFooterId = ctx.headerFooterId || null;
            item.tableId = ctx.tableId || null;
            item.cellId = ctx.cellId || null;
            item.columnIndex = ctx.columnIndex ?? null;
            item.pageIndex = pageIndex;
        }

        asArray(scoped.lines).forEach(function (line) {
            apply(line);
            asArray(line.availableIntervals).forEach(apply);
            asArray(line.segments).forEach(apply);
            asArray(line.inlineObjects).forEach(apply);
        });
        asArray(scoped.segments).forEach(apply);
        asArray(scoped.inlineObjects).forEach(apply);
        asArray(scoped.caretStops).forEach(apply);
        asArray(scoped.baselines).forEach(apply);
        asArray(scoped.objects).forEach(apply);
        asArray(scoped.exclusions).forEach(apply);
        return scoped;
    };
}
