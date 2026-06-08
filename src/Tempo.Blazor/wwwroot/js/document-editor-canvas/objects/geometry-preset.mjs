const DEFAULT_ROUND_RECT_RADIUS = 0.16;
const DEFAULT_ARROW_HEAD = 0.32;

export function buildPresetGeometryPath(preset, rect, adjustments = {}) {
    const box = normalizeRect(rect);
    const name = normalizePresetName(preset);
    const adjust = normalizeAdjustments(adjustments);

    switch (name) {
        case 'ellipse':
        case 'oval':
            return [ellipse(box)];
        case 'roundrect':
        case 'roundrectangle':
        case 'roundedrectangle':
            return roundRectangle(box, clamp01(adjust.radius ?? adjust.roundRadius ?? DEFAULT_ROUND_RECT_RADIUS));
        case 'triangle':
            return polygon([
                point(box, 0.5, 0),
                point(box, 1, 1),
                point(box, 0, 1),
            ]);
        case 'righttriangle':
            return polygon([
                point(box, 0, 0),
                point(box, 1, 1),
                point(box, 0, 1),
            ]);
        case 'diamond':
            return polygon([
                point(box, 0.5, 0),
                point(box, 1, 0.5),
                point(box, 0.5, 1),
                point(box, 0, 0.5),
            ]);
        case 'pentagon':
            return regularPolygon(box, 5, -Math.PI / 2);
        case 'hexagon':
            return regularPolygon(box, 6, Math.PI / 6);
        case 'star':
        case 'star5':
            return starPolygon(box, 5, adjust.innerRatio ?? 0.45);
        case 'star6':
            return starPolygon(box, 6, adjust.innerRatio ?? 0.48);
        case 'leftarrow':
            return horizontalArrow(box, 'left', adjust.arrowhead ?? DEFAULT_ARROW_HEAD);
        case 'uparrow':
            return verticalArrow(box, 'up', adjust.arrowhead ?? DEFAULT_ARROW_HEAD);
        case 'downarrow':
            return verticalArrow(box, 'down', adjust.arrowhead ?? DEFAULT_ARROW_HEAD);
        case 'rightarrow':
            return horizontalArrow(box, 'right', adjust.arrowhead ?? DEFAULT_ARROW_HEAD);
        case 'callout':
        case 'rectcallout':
            return callout(box, adjust);
        case 'line':
            return [
                { command: 'moveTo', x: box.x, y: box.y + box.height / 2 },
                { command: 'lineTo', x: box.x + box.width, y: box.y + box.height / 2 },
            ];
        case 'bentconnector':
        case 'elbowconnector':
        case 'connector':
            return [
                { command: 'moveTo', x: box.x, y: box.y + box.height / 2 },
                { command: 'lineTo', x: box.x + box.width / 2, y: box.y + box.height / 2 },
                { command: 'lineTo', x: box.x + box.width / 2, y: box.y + box.height },
                { command: 'lineTo', x: box.x + box.width, y: box.y + box.height },
            ];
        case 'rectangle':
        case 'rect':
        default:
            return rectangle(box);
    }
}

export function buildPresetStretchGuides(preset, rect, adjustments = {}) {
    const box = normalizeRect(rect);
    const name = normalizePresetName(preset);
    const adjust = normalizeAdjustments(adjustments);
    const right = box.x + box.width;
    const bottom = box.y + box.height;

    switch (name) {
        case 'leftarrow':
        case 'rightarrow': {
            const head = Math.max(0.18, Math.min(0.72, Number(adjust.arrowhead) || DEFAULT_ARROW_HEAD));
            const shaftHalf = 0.17;
            const headBoundary = name === 'leftarrow'
                ? box.x + box.width * head
                : box.x + box.width * (1 - head);
            const shaftLeft = name === 'leftarrow' ? headBoundary : box.x;
            const shaftRight = name === 'leftarrow' ? right : headBoundary;
            const shaftTop = box.y + box.height * (0.5 - shaftHalf);
            const shaftBottom = box.y + box.height * (0.5 + shaftHalf);
            return [
                xGuide('headBoundary', headBoundary, box.y, bottom),
                yGuide('shaftTop', shaftTop, shaftLeft, shaftRight),
                yGuide('shaftBottom', shaftBottom, shaftLeft, shaftRight),
                rectGuide('shaftRect', shaftLeft, shaftTop, shaftRight - shaftLeft, shaftBottom - shaftTop),
            ];
        }
        case 'uparrow':
        case 'downarrow': {
            const head = Math.max(0.18, Math.min(0.72, Number(adjust.arrowhead) || DEFAULT_ARROW_HEAD));
            const shaftHalf = 0.17;
            const headBoundary = name === 'uparrow'
                ? box.y + box.height * head
                : box.y + box.height * (1 - head);
            const shaftTop = name === 'uparrow' ? headBoundary : box.y;
            const shaftBottom = name === 'uparrow' ? bottom : headBoundary;
            const shaftLeft = box.x + box.width * (0.5 - shaftHalf);
            const shaftRight = box.x + box.width * (0.5 + shaftHalf);
            return [
                yGuide('headBoundary', headBoundary, box.x, right),
                xGuide('shaftLeft', shaftLeft, shaftTop, shaftBottom),
                xGuide('shaftRight', shaftRight, shaftTop, shaftBottom),
                rectGuide('shaftRect', shaftLeft, shaftTop, shaftRight - shaftLeft, shaftBottom - shaftTop),
            ];
        }
        case 'callout':
        case 'rectcallout': {
            const tailX = Math.max(0.12, Math.min(0.88, Number(adjust.tailx) || 0.72));
            const tailY = Math.max(0.18, Math.min(1.2, Number(adjust.taily) || 1.12));
            const textBottom = box.y + box.height * 0.78;
            const tailPointX = box.x + box.width * tailX;
            const tailPointY = box.y + box.height * tailY;
            const tailLeft = box.x + box.width * Math.max(0.14, tailX - 0.12);
            const tailRight = box.x + box.width * Math.min(0.86, tailX + 0.09);
            return [
                rectGuide('textRect', box.x, box.y, box.width, textBottom - box.y),
                yGuide('textBottom', textBottom, box.x, right),
                xGuide('tailPointX', tailPointX, textBottom, tailPointY),
                yGuide('tailPointY', tailPointY, tailLeft, tailRight),
            ];
        }
        default:
            return [];
    }
}

export function applyPresetGeometryPath(context, path) {
    if (!context || !Array.isArray(path) || path.length === 0) {
        return false;
    }

    context.beginPath?.();
    for (const segment of path) {
        switch (segment.command) {
            case 'moveTo':
                context.moveTo?.(segment.x, segment.y);
                break;
            case 'lineTo':
                context.lineTo?.(segment.x, segment.y);
                break;
            case 'quadraticCurveTo':
                context.quadraticCurveTo?.(segment.cpx, segment.cpy, segment.x, segment.y);
                break;
            case 'ellipse':
                context.ellipse?.(segment.x, segment.y, segment.radiusX, segment.radiusY, segment.rotation || 0, segment.startAngle || 0, segment.endAngle || Math.PI * 2);
                break;
            case 'closePath':
                context.closePath?.();
                break;
        }
    }

    return true;
}

export function normalizePresetName(preset) {
    return String(preset || 'rectangle').replace(/[\s_-]/g, '').toLowerCase();
}

function normalizeRect(rect) {
    return {
        x: Number(rect?.x || 0) || 0,
        y: Number(rect?.y || 0) || 0,
        width: Math.max(1, Number(rect?.width || 0) || 1),
        height: Math.max(1, Number(rect?.height || 0) || 1),
    };
}

function normalizeAdjustments(adjustments) {
    const source = adjustments && typeof adjustments === 'object' ? adjustments : {};
    const result = {};
    for (const [key, value] of Object.entries(source)) {
        result[String(key).replace(/[\s_-]/g, '').toLowerCase()] = Number(value);
    }

    return result;
}

function rectangle(box) {
    return polygon([
        point(box, 0, 0),
        point(box, 1, 0),
        point(box, 1, 1),
        point(box, 0, 1),
    ]);
}

function roundRectangle(box, radiusRatio) {
    const radius = Math.min(box.width, box.height) * Math.max(0, Math.min(0.5, radiusRatio));
    const right = box.x + box.width;
    const bottom = box.y + box.height;
    return [
        { command: 'moveTo', x: box.x + radius, y: box.y },
        { command: 'lineTo', x: right - radius, y: box.y },
        { command: 'quadraticCurveTo', cpx: right, cpy: box.y, x: right, y: box.y + radius },
        { command: 'lineTo', x: right, y: bottom - radius },
        { command: 'quadraticCurveTo', cpx: right, cpy: bottom, x: right - radius, y: bottom },
        { command: 'lineTo', x: box.x + radius, y: bottom },
        { command: 'quadraticCurveTo', cpx: box.x, cpy: bottom, x: box.x, y: bottom - radius },
        { command: 'lineTo', x: box.x, y: box.y + radius },
        { command: 'quadraticCurveTo', cpx: box.x, cpy: box.y, x: box.x + radius, y: box.y },
        { command: 'closePath' },
    ];
}

function ellipse(box) {
    return {
        command: 'ellipse',
        x: box.x + box.width / 2,
        y: box.y + box.height / 2,
        radiusX: box.width / 2,
        radiusY: box.height / 2,
        rotation: 0,
        startAngle: 0,
        endAngle: Math.PI * 2,
    };
}

function polygon(points) {
    return [
        { command: 'moveTo', ...points[0] },
        ...points.slice(1).map(item => ({ command: 'lineTo', ...item })),
        { command: 'closePath' },
    ];
}

function regularPolygon(box, sides, offset) {
    const cx = box.x + box.width / 2;
    const cy = box.y + box.height / 2;
    const rx = box.width / 2;
    const ry = box.height / 2;
    const points = [];
    for (let index = 0; index < sides; index += 1) {
        const angle = offset + index * Math.PI * 2 / sides;
        points.push({ x: cx + Math.cos(angle) * rx, y: cy + Math.sin(angle) * ry });
    }

    return polygon(points);
}

function starPolygon(box, points, innerRatio) {
    const cx = box.x + box.width / 2;
    const cy = box.y + box.height / 2;
    const rx = box.width / 2;
    const ry = box.height / 2;
    const inner = Math.max(0.1, Math.min(0.9, Number(innerRatio) || 0.45));
    const result = [];
    for (let index = 0; index < points * 2; index += 1) {
        const outer = index % 2 === 0;
        const angle = -Math.PI / 2 + index * Math.PI / points;
        result.push({
            x: cx + Math.cos(angle) * rx * (outer ? 1 : inner),
            y: cy + Math.sin(angle) * ry * (outer ? 1 : inner),
        });
    }

    return polygon(result);
}

function horizontalArrow(box, direction, arrowHeadRatio) {
    const head = Math.max(0.18, Math.min(0.72, Number(arrowHeadRatio) || DEFAULT_ARROW_HEAD));
    const shaft = 0.34;
    const points = direction === 'left'
        ? [
            point(box, 1, 0.5 - shaft / 2),
            point(box, head, 0.5 - shaft / 2),
            point(box, head, 0),
            point(box, 0, 0.5),
            point(box, head, 1),
            point(box, head, 0.5 + shaft / 2),
            point(box, 1, 0.5 + shaft / 2),
        ]
        : [
            point(box, 0, 0.5 - shaft / 2),
            point(box, 1 - head, 0.5 - shaft / 2),
            point(box, 1 - head, 0),
            point(box, 1, 0.5),
            point(box, 1 - head, 1),
            point(box, 1 - head, 0.5 + shaft / 2),
            point(box, 0, 0.5 + shaft / 2),
        ];
    return polygon(points);
}

function verticalArrow(box, direction, arrowHeadRatio) {
    const head = Math.max(0.18, Math.min(0.72, Number(arrowHeadRatio) || DEFAULT_ARROW_HEAD));
    const shaft = 0.34;
    const points = direction === 'up'
        ? [
            point(box, 0.5, 0),
            point(box, 1, head),
            point(box, 0.5 + shaft / 2, head),
            point(box, 0.5 + shaft / 2, 1),
            point(box, 0.5 - shaft / 2, 1),
            point(box, 0.5 - shaft / 2, head),
            point(box, 0, head),
        ]
        : [
            point(box, 0.5 - shaft / 2, 0),
            point(box, 0.5 + shaft / 2, 0),
            point(box, 0.5 + shaft / 2, 1 - head),
            point(box, 1, 1 - head),
            point(box, 0.5, 1),
            point(box, 0, 1 - head),
            point(box, 0.5 - shaft / 2, 1 - head),
        ];
    return polygon(points);
}

function callout(box, adjustments) {
    const tailX = Math.max(0.12, Math.min(0.88, Number(adjustments.tailx) || 0.72));
    const tailY = Math.max(0.18, Math.min(1.2, Number(adjustments.taily) || 1.12));
    return [
        { command: 'moveTo', ...point(box, 0, 0) },
        { command: 'lineTo', ...point(box, 1, 0) },
        { command: 'lineTo', ...point(box, 1, 0.78) },
        { command: 'lineTo', ...point(box, Math.min(0.86, tailX + 0.09), 0.78) },
        { command: 'lineTo', ...point(box, tailX, tailY) },
        { command: 'lineTo', ...point(box, Math.max(0.14, tailX - 0.12), 0.78) },
        { command: 'lineTo', ...point(box, 0, 0.78) },
        { command: 'closePath' },
    ];
}

function point(box, x, y) {
    return {
        x: box.x + box.width * x,
        y: box.y + box.height * y,
    };
}

function xGuide(name, position, min, max) {
    return {
        name,
        axis: 'x',
        position: round(position),
        min: round(Math.min(min, max)),
        max: round(Math.max(min, max)),
    };
}

function yGuide(name, position, min, max) {
    return {
        name,
        axis: 'y',
        position: round(position),
        min: round(Math.min(min, max)),
        max: round(Math.max(min, max)),
    };
}

function rectGuide(name, x, y, width, height) {
    return {
        name,
        axis: 'rect',
        x: round(x),
        y: round(y),
        width: round(Math.max(0, width)),
        height: round(Math.max(0, height)),
    };
}

function round(value) {
    const number = Number(value);
    return Number.isFinite(number) ? Math.round(number * 1000) / 1000 : 0;
}

function clamp01(value) {
    return Math.max(0, Math.min(1, Number(value) || 0));
}
