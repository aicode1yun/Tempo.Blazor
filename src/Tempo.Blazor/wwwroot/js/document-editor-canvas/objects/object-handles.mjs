export const OBJECT_HANDLE_NAMES = Object.freeze(['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w']);
export const OBJECT_ROTATE_HANDLE_NAME = 'rotate';
export const OBJECT_CONNECTOR_START_HANDLE_NAME = 'connector-start';
export const OBJECT_CONNECTOR_END_HANDLE_NAME = 'connector-end';
export const OBJECT_CONNECTOR_ENDPOINT_HANDLE_NAMES = Object.freeze([
    OBJECT_CONNECTOR_START_HANDLE_NAME,
    OBJECT_CONNECTOR_END_HANDLE_NAME,
]);
const HANDLE_SIZE = 8;
const ROTATE_HANDLE_OFFSET = 24;
const CONNECTOR_HIT_TOLERANCE = 7;

export function imageObjectAtPoint(layout, pageIndex, x, y) {
    const objects = imageBlocks(layout)
        .filter(block => Number(block.pageIndex || 0) === Number(pageIndex || 0))
        .sort((left, right) => (Number(right.object?.zIndex || 0) - Number(left.object?.zIndex || 0)) || (Number(right.sequence || 0) - Number(left.sequence || 0)));
    const connectorHit = objects
        .filter(isConnectorObjectLayout)
        .map(object => ({
            object,
            distance: connectorDistanceToPoint(object, x, y),
        }))
        .filter(item => Number.isFinite(item.distance) && item.distance <= CONNECTOR_HIT_TOLERANCE)
        .sort((left, right) =>
            (left.distance - right.distance)
            || (Number(right.object?.object?.zIndex || 0) - Number(left.object?.object?.zIndex || 0))
            || (Number(right.object?.sequence || 0) - Number(left.object?.sequence || 0)))[0];
    if (connectorHit) {
        return objectSelectionInfo(connectorHit.object);
    }

    for (const object of objects) {
        const rect = object.rect || {};
        if (pointInRect(x, y, rect)) {
            return objectSelectionInfo(object);
        }
    }

    return null;
}

export function objectResizeHandleAt(layout, selection, pageIndex, x, y) {
    const selected = selectedImageBlock(layout, selection);
    if (!selected || Number(selected.pageIndex || 0) !== Number(pageIndex || 0)) {
        return null;
    }

    for (const handle of objectInteractionHandleRects(selected)) {
        if (pointInRect(x, y, handle.rect)) {
            return {
                ...objectSelectionInfo(selected),
                handle: handle.name,
                rect: selected.rect,
            };
        }
    }

    return null;
}

export function objectHandleRects(objectLayout) {
    const rect = objectLayout?.rect || {};
    const x = Number(rect.x || 0) || 0;
    const y = Number(rect.y || 0) || 0;
    const width = Math.max(1, Number(rect.width || 0) || 1);
    const height = Math.max(1, Number(rect.height || 0) || 1);
    const half = HANDLE_SIZE / 2;
    const points = {
        nw: [x, y],
        n: [x + width / 2, y],
        ne: [x + width, y],
        e: [x + width, y + height / 2],
        se: [x + width, y + height],
        s: [x + width / 2, y + height],
        sw: [x, y + height],
        w: [x, y + height / 2],
    };
    return OBJECT_HANDLE_NAMES.map(name => ({
        name,
        rect: {
            x: points[name][0] - half,
            y: points[name][1] - half,
            width: HANDLE_SIZE,
            height: HANDLE_SIZE,
        },
    }));
}

export function objectInteractionHandleRects(objectLayout) {
    const rect = objectLayout?.rect || {};
    const x = Number(rect.x || 0) || 0;
    const y = Number(rect.y || 0) || 0;
    const width = Math.max(1, Number(rect.width || 0) || 1);
    const half = HANDLE_SIZE / 2;
    const connectorHandles = objectConnectorEndpointHandleRects(objectLayout);
    return [
        ...objectHandleRects(objectLayout),
        ...connectorHandles,
        {
            name: OBJECT_ROTATE_HANDLE_NAME,
            rect: {
                x: x + width / 2 - half,
                y: y - ROTATE_HANDLE_OFFSET - half,
                width: HANDLE_SIZE,
                height: HANDLE_SIZE,
            },
        },
    ];
}

export function objectConnectorEndpointHandleRects(objectLayout) {
    if (!isConnectorObjectLayout(objectLayout)) {
        return [];
    }

    const connector = objectLayout?.connector || objectLayout?.object?.connector || {};
    const points = Array.isArray(connector.points) ? connector.points : [];
    const start = normalizePoint(connector.start || points[0]);
    const end = normalizePoint(connector.end || points.at(-1));
    if (!start || !end) {
        return [];
    }

    const half = HANDLE_SIZE / 2;
    return [
        {
            name: OBJECT_CONNECTOR_START_HANDLE_NAME,
            rect: {
                x: start.x - half,
                y: start.y - half,
                width: HANDLE_SIZE,
                height: HANDLE_SIZE,
            },
            point: start,
        },
        {
            name: OBJECT_CONNECTOR_END_HANDLE_NAME,
            rect: {
                x: end.x - half,
                y: end.y - half,
                width: HANDLE_SIZE,
                height: HANDLE_SIZE,
            },
            point: end,
        },
    ];
}

export function isObjectConnectorEndpointHandle(handle) {
    return OBJECT_CONNECTOR_ENDPOINT_HANDLE_NAMES.includes(String(handle || ''));
}

export function rotationFromPointer(startRect, startPointer, currentPointer, startRotation = 0, snap = false) {
    const rect = {
        x: Number(startRect?.x || 0) || 0,
        y: Number(startRect?.y || 0) || 0,
        width: Math.max(1, Number(startRect?.width || 0) || 1),
        height: Math.max(1, Number(startRect?.height || 0) || 1),
    };
    const center = {
        x: rect.x + rect.width / 2,
        y: rect.y + rect.height / 2,
    };
    const startAngle = Math.atan2(
        (Number(startPointer?.y || center.y - ROTATE_HANDLE_OFFSET) || 0) - center.y,
        (Number(startPointer?.x || center.x) || 0) - center.x);
    const currentAngle = Math.atan2(
        (Number(currentPointer?.y || center.y - ROTATE_HANDLE_OFFSET) || 0) - center.y,
        (Number(currentPointer?.x || center.x) || 0) - center.x);
    const delta = (currentAngle - startAngle) * 180 / Math.PI;
    const raw = normalizeRotation((Number(startRotation || 0) || 0) + delta);
    return snap ? Math.round(raw / 15) * 15 : Math.round(raw * 1000) / 1000;
}

export function resizeRectFromHandle(startRect, handle, dx, dy, lockAspectRatio = true) {
    const rect = {
        x: Number(startRect?.x || 0) || 0,
        y: Number(startRect?.y || 0) || 0,
        width: Math.max(24, Number(startRect?.width || 1) || 1),
        height: Math.max(24, Number(startRect?.height || 1) || 1),
    };
    let nextX = rect.x;
    let nextY = rect.y;
    let nextWidth = rect.width;
    let nextHeight = rect.height;
    const name = String(handle || 'se').toLowerCase();
    if (name.includes('e')) nextWidth += dx;
    if (name.includes('s')) nextHeight += dy;
    if (name.includes('w')) {
        nextX += dx;
        nextWidth -= dx;
    }

    if (name.includes('n')) {
        nextY += dy;
        nextHeight -= dy;
    }

    nextWidth = Math.max(24, nextWidth);
    nextHeight = Math.max(24, nextHeight);
    if (lockAspectRatio) {
        const ratio = rect.width / Math.max(1, rect.height);
        if (Math.abs(dx) >= Math.abs(dy)) {
            nextHeight = Math.max(24, nextWidth / ratio);
        } else {
            nextWidth = Math.max(24, nextHeight * ratio);
        }

        if (name.includes('w')) nextX = rect.x + rect.width - nextWidth;
        if (name.includes('n')) nextY = rect.y + rect.height - nextHeight;
    }

    return { x: nextX, y: nextY, width: nextWidth, height: nextHeight };
}

export function moveRect(startRect, dx, dy) {
    return {
        x: (Number(startRect?.x || 0) || 0) + (Number(dx || 0) || 0),
        y: (Number(startRect?.y || 0) || 0) + (Number(dy || 0) || 0),
        width: Math.max(1, Number(startRect?.width || 0) || 1),
        height: Math.max(1, Number(startRect?.height || 0) || 1),
    };
}

function selectedImageBlock(layout, selection) {
    const objectId = String(selection?.object?.objectId || selection?.objectId || '');
    const blockId = String(selection?.object?.blockId || selection?.focus?.blockId || '');
    if (!objectId && !blockId) {
        return null;
    }

    return imageBlocks(layout).find(block =>
        (objectId && String(block?.objectId || block?.object?.objectId || '') === objectId)
        || (blockId && String(block?.blockId || '') === blockId)) || null;
}

function imageBlocks(layout) {
    return (layout?.blocks || []).filter(block => block?.type === 'image' && block?.rect);
}

function objectSelectionInfo(object) {
    return {
        objectId: String(object?.objectId || object?.object?.objectId || ''),
        blockId: String(object?.blockId || ''),
        runId: String(object?.runId || ''),
        role: String(object?.role || object?.object?.role || 'imageBlock'),
        pageIndex: Number(object?.pageIndex || 0) || 0,
        rect: object?.rect || null,
        width: Math.max(1, Number(object?.rect?.width || 0) || 1),
        height: Math.max(1, Number(object?.rect?.height || 0) || 1),
        rotation: Number(object?.object?.rotation ?? object?.rotation ?? 0) || 0,
        flipHorizontal: object?.object?.flipHorizontal === true || object?.flipHorizontal === true,
        flipVertical: object?.object?.flipVertical === true || object?.flipVertical === true,
        wrapMode: object?.object?.wrapMode || 'Inline',
        altText: object?.object?.altText || '',
        caption: object?.object?.caption || '',
        kind: String(object?.object?.kind || object?.kind || ''),
        zIndex: Number(object?.object?.zIndex ?? object?.zIndex ?? 0) || 0,
        connector: cloneConnector(object?.connector || object?.object?.connector || null),
    };
}

function isConnectorObjectLayout(objectLayout) {
    const kind = String(objectLayout?.object?.kind || objectLayout?.kind || '').replace(/[\s_-]/g, '').toLowerCase();
    return kind === 'line' || kind === 'connector';
}

function connectorDistanceToPoint(objectLayout, x, y) {
    const connector = objectLayout?.connector || objectLayout?.object?.connector || {};
    const points = Array.isArray(connector.points)
        ? connector.points.map(normalizePoint).filter(Boolean)
        : [];
    const start = normalizePoint(connector.start);
    const end = normalizePoint(connector.end);
    const route = points.length >= 2
        ? points
        : (start && end ? [start, end] : []);
    if (route.length < 2) {
        return Number.POSITIVE_INFINITY;
    }

    let distance = Number.POSITIVE_INFINITY;
    const point = {
        x: Number(x || 0) || 0,
        y: Number(y || 0) || 0,
    };
    for (let index = 1; index < route.length; index += 1) {
        distance = Math.min(distance, distanceToSegment(point, route[index - 1], route[index]));
    }

    return distance;
}

function distanceToSegment(point, start, end) {
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const lengthSquared = dx * dx + dy * dy;
    if (lengthSquared <= 0) {
        return Math.hypot(point.x - start.x, point.y - start.y);
    }

    const t = Math.max(0, Math.min(1, ((point.x - start.x) * dx + (point.y - start.y) * dy) / lengthSquared));
    const projectedX = start.x + t * dx;
    const projectedY = start.y + t * dy;
    return Math.hypot(point.x - projectedX, point.y - projectedY);
}

function normalizePoint(point) {
    const x = Number(point?.x ?? point?.X);
    const y = Number(point?.y ?? point?.Y);
    if (!Number.isFinite(x) || !Number.isFinite(y)) {
        return null;
    }

    return { x, y };
}

function cloneConnector(connector) {
    if (!connector || typeof connector !== 'object') {
        return null;
    }

    return {
        routing: String(connector.routing || ''),
        start: normalizePoint(connector.start),
        end: normalizePoint(connector.end),
        points: Array.isArray(connector.points) ? connector.points.map(normalizePoint).filter(Boolean) : [],
        startConnection: connector.startConnection ? { ...connector.startConnection } : null,
        endConnection: connector.endConnection ? { ...connector.endConnection } : null,
    };
}

function normalizeRotation(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return 0;
    }

    let normalized = number % 360;
    if (normalized > 180) {
        normalized -= 360;
    } else if (normalized <= -180) {
        normalized += 360;
    }

    return normalized;
}

function pointInRect(x, y, rect) {
    const left = Number(rect?.x || 0) || 0;
    const top = Number(rect?.y || 0) || 0;
    const width = Math.max(1, Number(rect?.width || 0) || 1);
    const height = Math.max(1, Number(rect?.height || 0) || 1);
    return x >= left && x <= left + width && y >= top && y <= top + height;
}
