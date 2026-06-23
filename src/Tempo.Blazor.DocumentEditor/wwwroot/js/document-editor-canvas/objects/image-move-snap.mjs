import { resizeRectFromHandle } from './object-handles.mjs';

export const IMAGE_SNAP_GRID = 8;
export const IMAGE_SNAP_THRESHOLD = 6;

export function snapObjectMoveRect(rect, context = {}) {
    const source = normalizeRect(rect);
    if (context.enabled === false) {
        return snapResult(source, null, null);
    }

    const guides = buildSnapGuides(context);
    const threshold = snapThreshold(context);
    const xSnap = closestSnap([
        { edge: 'left', value: source.x },
        { edge: 'centerX', value: source.x + source.width / 2 },
        { edge: 'right', value: source.x + source.width },
    ], guides.x, context, threshold);
    const ySnap = closestSnap([
        { edge: 'top', value: source.y },
        { edge: 'centerY', value: source.y + source.height / 2 },
        { edge: 'bottom', value: source.y + source.height },
    ], guides.y, context, threshold);

    return snapResult({
        ...source,
        x: source.x + (xSnap?.delta || 0),
        y: source.y + (ySnap?.delta || 0),
    }, xSnap, ySnap);
}

export function snapObjectResizeRect(startRect, handle, dx, dy, lockAspectRatio = true, context = {}) {
    if (context.enabled === false) {
        return snapResult(resizeRectFromHandle(startRect, handle, dx, dy, lockAspectRatio), null, null);
    }

    const start = normalizeRect(startRect);
    const name = String(handle || 'se').toLowerCase();
    const preview = normalizeRect(resizeRectFromHandle(start, name, dx, dy, lockAspectRatio));
    const guides = buildSnapGuides(context);
    const threshold = snapThreshold(context);
    let xSnap = null;
    let ySnap = null;

    if (name.includes('e')) {
        xSnap = closestSnap([{ edge: 'right', value: preview.x + preview.width }], guides.x, context, threshold);
    } else if (name.includes('w')) {
        xSnap = closestSnap([{ edge: 'left', value: preview.x }], guides.x, context, threshold);
    }

    if (name.includes('s')) {
        ySnap = closestSnap([{ edge: 'bottom', value: preview.y + preview.height }], guides.y, context, threshold);
    } else if (name.includes('n')) {
        ySnap = closestSnap([{ edge: 'top', value: preview.y }], guides.y, context, threshold);
    }

    if (lockAspectRatio && xSnap && ySnap && hasHorizontalHandle(name) && hasVerticalHandle(name)) {
        if (xSnap.distance <= ySnap.distance) {
            ySnap = null;
        } else {
            xSnap = null;
        }
    }

    if (lockAspectRatio && hasHorizontalHandle(name) && hasVerticalHandle(name) && (xSnap || ySnap)) {
        return snapResult(lockedAspectResizeFromSnap(start, name, xSnap, ySnap), xSnap, ySnap);
    }

    const snappedDx = adjustedHorizontalDelta(start, name, dx, xSnap);
    const snappedDy = adjustedVerticalDelta(start, name, dy, ySnap);
    return snapResult(
        normalizeRect(resizeRectFromHandle(start, name, snappedDx, snappedDy, lockAspectRatio)),
        xSnap,
        ySnap);
}

function lockedAspectResizeFromSnap(start, handle, xSnap, ySnap) {
    const ratio = start.width / Math.max(1, start.height);
    if (xSnap) {
        const width = Math.max(24, handle.includes('e')
            ? xSnap.guide - start.x
            : start.x + start.width - xSnap.guide);
        const height = Math.max(24, width / ratio);
        return normalizeRect({
            x: handle.includes('w') ? start.x + start.width - width : start.x,
            y: handle.includes('n') ? start.y + start.height - height : start.y,
            width,
            height,
        });
    }

    const height = Math.max(24, handle.includes('s')
        ? ySnap.guide - start.y
        : start.y + start.height - ySnap.guide);
    const width = Math.max(24, height * ratio);
    return normalizeRect({
        x: handle.includes('w') ? start.x + start.width - width : start.x,
        y: handle.includes('n') ? start.y + start.height - height : start.y,
        width,
        height,
    });
}

function adjustedHorizontalDelta(start, handle, fallback, snap) {
    if (!snap) {
        return fallback;
    }

    if (handle.includes('e')) {
        return snap.guide - (start.x + start.width);
    }

    if (handle.includes('w')) {
        return snap.guide - start.x;
    }

    return fallback;
}

function adjustedVerticalDelta(start, handle, fallback, snap) {
    if (!snap) {
        return fallback;
    }

    if (handle.includes('s')) {
        return snap.guide - (start.y + start.height);
    }

    if (handle.includes('n')) {
        return snap.guide - start.y;
    }

    return fallback;
}

function buildSnapGuides(context) {
    const pageIndex = Number(context.pageIndex || 0) || 0;
    const body = context.body || pageBody(context.layout, pageIndex);
    const x = [];
    const y = [];
    if (body) {
        const left = Number(body.x || 0) || 0;
        const top = Number(body.y || 0) || 0;
        const width = Math.max(1, Number(body.width || 0) || 1);
        const height = Math.max(1, Number(body.height || 0) || 1);
        addGuide(x, left, 'body-left');
        addGuide(x, left + width / 2, 'body-center-x');
        addGuide(x, left + width, 'body-right');
        addGuide(y, top, 'body-top');
        addGuide(y, top + height / 2, 'body-center-y');
        addGuide(y, top + height, 'body-bottom');
    }

    const objectId = String(context.objectId || '');
    for (const block of imageBlocks(context.layout)) {
        if (Number(block.pageIndex || 0) !== pageIndex) {
            continue;
        }

        const candidateObjectId = String(block.objectId || block.object?.objectId || '');
        if (objectId && candidateObjectId === objectId) {
            continue;
        }

        const rect = normalizeRect(block.rect);
        addGuide(x, rect.x, 'object-left');
        addGuide(x, rect.x + rect.width / 2, 'object-center-x');
        addGuide(x, rect.x + rect.width, 'object-right');
        addGuide(y, rect.y, 'object-top');
        addGuide(y, rect.y + rect.height / 2, 'object-center-y');
        addGuide(y, rect.y + rect.height, 'object-bottom');
    }

    return { x, y };
}

function closestSnap(candidates, guides, context, threshold) {
    let closest = null;
    for (const candidate of candidates) {
        const guideCandidates = guides.slice();
        const grid = Number(context.gridSize ?? IMAGE_SNAP_GRID);
        if (Number.isFinite(grid) && grid > 0) {
            guideCandidates.push({
                value: Math.round(candidate.value / grid) * grid,
                type: 'grid',
            });
        }

        for (const guide of guideCandidates) {
            const distance = Math.abs(guide.value - candidate.value);
            if (distance > threshold) {
                continue;
            }

            if (!closest || distance < closest.distance) {
                closest = {
                    edge: candidate.edge,
                    guide: guide.value,
                    guideType: guide.type,
                    distance,
                    delta: guide.value - candidate.value,
                };
            }
        }
    }

    return closest;
}

function snapResult(rect, xSnap, ySnap) {
    return {
        rect,
        snapped: !!(xSnap || ySnap),
        x: xSnap,
        y: ySnap,
    };
}

function pageBody(layout, pageIndex) {
    return (layout?.pages || []).find(page => Number(page.index || 0) === pageIndex)?.body || null;
}

function imageBlocks(layout) {
    return (layout?.blocks || []).filter(block => block?.type === 'image' && block?.rect);
}

function addGuide(list, value, type) {
    const numeric = Number(value);
    if (Number.isFinite(numeric)) {
        list.push({ value: numeric, type });
    }
}

function snapThreshold(context) {
    const value = Number(context.threshold ?? IMAGE_SNAP_THRESHOLD);
    return Number.isFinite(value) && value >= 0 ? value : IMAGE_SNAP_THRESHOLD;
}

function normalizeRect(rect) {
    return {
        x: Number(rect?.x || 0) || 0,
        y: Number(rect?.y || 0) || 0,
        width: Math.max(1, Number(rect?.width || 0) || 1),
        height: Math.max(1, Number(rect?.height || 0) || 1),
    };
}

function hasHorizontalHandle(handle) {
    return handle.includes('e') || handle.includes('w');
}

function hasVerticalHandle(handle) {
    return handle.includes('n') || handle.includes('s');
}
