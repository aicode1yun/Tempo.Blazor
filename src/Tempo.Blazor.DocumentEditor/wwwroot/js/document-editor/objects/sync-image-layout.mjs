// Phase D — objects/sync-image-layout.mjs
// `syncImageLayoutCase` — fills both PascalCase and camelCase mirror fields on a
// Layout payload so it survives a JS interop round-trip without losing data on
// either side. Also normalises numeric ordinals (Region, Wrap.Mode, …), default
// dimensions, and derives `Kind` from `Wrap.Mode + Anchor.FixedOnPage`.
//
// `applyImageWrapModeToLayout` — apply a new wrap mode to an existing layout,
// adjusting Anchor + Stacking semantics (Inline → MoveWithText=true / no overlap,
// BehindText → ZIndex=-1 / overlap, InFrontOfText → ZIndex=1 / overlap, …).
//
// Pure functions — mutate a clone (not the input).

import { clone, sortObject } from '../core/helpers.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';
import { normalizeWrapModeName } from './wrap-modes.mjs';
import { anchorRegionToValue } from './anchor-region.mjs';
import { normalizeLayoutKindName } from './layout-helpers.mjs';
import { wrapModeToValue } from './wrap-mode-value.mjs';

export function syncImageLayoutCase(layout) {
    const source = layout || {};
    const wrap = source.Wrap || source.wrap || {};
    const position = source.Position || source.position || {};
    const anchor = source.Anchor || source.anchor || {};
    const transform = source.Transform || source.transform || {};
    const size = source.Size || source.size || {};
    const stacking = source.Stacking || source.stacking || {};
    const mode = wrap.Mode ?? wrap.mode ?? 0;
    const modeName = normalizeWrapModeName(mode);

    wrap.Mode = wrapModeToValue(modeName);
    wrap.mode = wrap.Mode;
    wrap.DistanceLeft = Number(wrap.DistanceLeft ?? wrap.distanceLeft ?? 0) || 0;
    wrap.distanceLeft = wrap.DistanceLeft;
    wrap.DistanceRight = Number(wrap.DistanceRight ?? wrap.distanceRight ?? 0) || 0;
    wrap.distanceRight = wrap.DistanceRight;
    wrap.DistanceTop = Number(wrap.DistanceTop ?? wrap.distanceTop ?? 0) || 0;
    wrap.distanceTop = wrap.DistanceTop;
    wrap.DistanceBottom = Number(wrap.DistanceBottom ?? wrap.distanceBottom ?? 0) || 0;
    wrap.distanceBottom = wrap.DistanceBottom;

    position.HorizontalAlignment = position.HorizontalAlignment ?? position.horizontalAlignment ?? 0;
    position.horizontalAlignment = position.HorizontalAlignment;
    position.HorizontalRelativeTo = position.HorizontalRelativeTo ?? position.horizontalRelativeTo ?? 0;
    position.horizontalRelativeTo = position.HorizontalRelativeTo;
    position.VerticalRelativeTo = position.VerticalRelativeTo ?? position.verticalRelativeTo ?? 3;
    position.verticalRelativeTo = position.VerticalRelativeTo;
    position.X = Number(position.X ?? position.x ?? 0) || 0;
    position.x = position.X;
    position.Y = Number(position.Y ?? position.y ?? 0) || 0;
    position.y = position.Y;

    const fixedInput = anchor.FixedOnPage ?? anchor.fixedOnPage ?? false;
    anchor.FixedOnPage = fixedInput === true || fixedInput === 'true';
    const moveInput = anchor.MoveWithText ?? anchor.moveWithText;
    anchor.MoveWithText = anchor.FixedOnPage
        ? false
        : (moveInput === undefined || moveInput === null
            ? true
            : moveInput !== false && moveInput !== 'false');
    anchor.moveWithText = anchor.MoveWithText;
    anchor.fixedOnPage = anchor.FixedOnPage;
    anchor.LockAnchor = (anchor.LockAnchor ?? anchor.lockAnchor ?? false) === true;
    anchor.lockAnchor = anchor.LockAnchor;
    anchor.BlockId = anchor.BlockId ?? anchor.blockId ?? '';
    anchor.blockId = anchor.BlockId;
    anchor.Offset = Number(anchor.Offset ?? anchor.offset ?? 0) || 0;
    anchor.offset = anchor.Offset;
    anchor.InlineIndex = Number(anchor.InlineIndex ?? anchor.inlineIndex ?? -1);
    anchor.inlineIndex = anchor.InlineIndex;
    anchor.Region = anchorRegionToValue(anchor.Region ?? anchor.region ?? 'Body');
    anchor.region = anchor.Region;
    anchor.TableId = anchor.TableId ?? anchor.tableId ?? null;
    anchor.tableId = anchor.TableId;
    anchor.CellId = anchor.CellId ?? anchor.cellId ?? null;
    anchor.cellId = anchor.CellId;
    anchor.ColumnIndex = normalizeTextExclusionColumnIndex(anchor.ColumnIndex ?? anchor.columnIndex);
    anchor.columnIndex = anchor.ColumnIndex;
    anchor.HeaderFooterId = anchor.HeaderFooterId ?? anchor.headerFooterId ?? null;
    anchor.headerFooterId = anchor.HeaderFooterId;

    transform.Width = Number(transform.Width ?? transform.width ?? size.Width ?? size.width ?? 120) || 120;
    transform.width = transform.Width;
    transform.Height = Number(transform.Height ?? transform.height ?? size.Height ?? size.height ?? 80) || 80;
    transform.height = transform.Height;
    transform.LockAspectRatio = (transform.LockAspectRatio ?? transform.lockAspectRatio ?? size.LockAspectRatio ?? size.lockAspectRatio ?? true) !== false;
    transform.lockAspectRatio = transform.LockAspectRatio;
    size.Width = transform.Width;
    size.width = transform.Width;
    size.Height = transform.Height;
    size.height = transform.Height;
    size.LockAspectRatio = transform.LockAspectRatio;
    size.lockAspectRatio = transform.LockAspectRatio;

    stacking.ZIndex = Number(stacking.ZIndex ?? stacking.zIndex ?? 0) || 0;
    stacking.zIndex = stacking.ZIndex;
    stacking.AllowOverlap = (stacking.AllowOverlap ?? stacking.allowOverlap ?? false) === true;
    stacking.allowOverlap = stacking.AllowOverlap;

    const kindName = normalizeLayoutKindName(source.Kind ?? source.kind ?? 0);
    if (kindName === 'Fixed' && modeName !== 'Inline') {
        anchor.FixedOnPage = true;
        anchor.fixedOnPage = true;
        anchor.MoveWithText = false;
        anchor.moveWithText = false;
    } else if (modeName === 'Inline') {
        anchor.FixedOnPage = false;
        anchor.fixedOnPage = false;
        anchor.MoveWithText = true;
        anchor.moveWithText = true;
    }
    source.Kind = modeName === 'Inline' ? 0 : (anchor.FixedOnPage ? 2 : 1);
    source.kind = source.Kind;
    source.Anchor = anchor;
    source.anchor = anchor;
    source.Position = position;
    source.position = position;
    source.Wrap = wrap;
    source.wrap = wrap;
    source.Size = size;
    source.size = size;
    source.Transform = transform;
    source.transform = transform;
    source.Stacking = stacking;
    source.stacking = stacking;
    return sortObject(source);
}

// Apply a new wrap mode to a Layout. Mutates a deep clone of the input, never the
// original. Adjusts Anchor.MoveWithText / FixedOnPage and Stacking.AllowOverlap /
// ZIndex according to the new mode's semantics.
export function applyImageWrapModeToLayout(layout, value, options) {
    const opts = options || {};
    const mode = normalizeWrapModeName(value);
    const next = syncImageLayoutCase(clone(layout || {}));
    const anchor = next.Anchor || next.anchor || {};
    const wrap = next.Wrap || next.wrap || {};
    const stacking = next.Stacking || next.stacking || {};

    const explicitFixed = opts.fixedOnPage !== undefined || opts.FixedOnPage !== undefined;
    let fixedOnPage = explicitFixed
        ? (opts.fixedOnPage ?? opts.FixedOnPage) === true
        : (mode === 'InFrontOfText' && anchor.FixedOnPage === true);

    if (mode === 'Inline') {
        fixedOnPage = false;
        anchor.MoveWithText = true;
        stacking.AllowOverlap = false;
    } else if (mode === 'BehindText') {
        fixedOnPage = false;
        anchor.MoveWithText = true;
        stacking.AllowOverlap = true;
        if (Number(stacking.ZIndex || 0) >= 0) stacking.ZIndex = -1;
    } else if (mode === 'InFrontOfText') {
        anchor.MoveWithText = fixedOnPage !== true;
        stacking.AllowOverlap = true;
        if (Number(stacking.ZIndex || 0) <= 0) stacking.ZIndex = 1;
    } else {
        // Square / Tight / Through / TopBottom: anchored to text flow, no overlap
        fixedOnPage = false;
        anchor.MoveWithText = true;
        stacking.AllowOverlap = false;
        if (Number(stacking.ZIndex || 0) < 0) stacking.ZIndex = 0;
    }

    anchor.FixedOnPage = fixedOnPage === true;
    anchor.fixedOnPage = anchor.FixedOnPage;
    anchor.moveWithText = anchor.MoveWithText;
    wrap.Mode = wrapModeToValue(mode);
    wrap.mode = wrap.Mode;
    stacking.allowOverlap = stacking.AllowOverlap;
    stacking.zIndex = stacking.ZIndex;
    next.Kind = mode === 'Inline' ? 0 : (anchor.FixedOnPage ? 2 : 1);
    next.kind = next.Kind;
    next.Anchor = anchor;
    next.anchor = anchor;
    next.Wrap = wrap;
    next.wrap = wrap;
    next.Stacking = stacking;
    next.stacking = stacking;
    return syncImageLayoutCase(next);
}
