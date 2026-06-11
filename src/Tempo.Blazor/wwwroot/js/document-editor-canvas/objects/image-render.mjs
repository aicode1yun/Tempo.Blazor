import { normalizeWrapModeName } from '../../document-editor/objects/wrap-modes.mjs';
import { createFontMetricsService } from '../../document-editor/layout/font-metrics.mjs';
import { buildDrawingChartLayout } from './chart-layout.mjs';
import { drawingTextLayoutHeight, layoutDrawingTextLines } from './textbox-layout.mjs';

export const CANVAS_IMAGE_HANDLES = Object.freeze(['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w']);
const DEFAULT_IMAGE_WIDTH = 220;
const DEFAULT_IMAGE_HEIGHT = 124;
const DEFAULT_WRAP_MARGIN = 12;
const DEFAULT_DRAWING_FILL = Object.freeze({ type: 'solid', color: '#ffffff', opacity: 1 });
const DEFAULT_DRAWING_STROKE = Object.freeze({ color: '#64748b', width: 1.5, dash: 'solid', opacity: 1, lineCap: 'round', lineJoin: 'round' });

export function collectCanvasImageObjects(model, pages = []) {
    const blocks = Array.isArray(model?.body?.blocks) ? model.body.blocks : [];
    const page = pages[0] || null;
    const body = page?.body || { x: 72, y: 72, width: 480, height: 680 };
    const objects = [];
    for (let blockIndex = 0; blockIndex < blocks.length; blockIndex += 1) {
        const block = blocks[blockIndex];
        const type = canvasBlockType(block);
        if (type === 'image') {
            objects.push(normalizeCanvasImageObject({
                model,
                block,
                blockIndex,
                body,
                objectRole: 'imageBlock',
            }));
            continue;
        }

        if (isTextLikeBlock(type)) {
            const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
            for (let runIndex = 0; runIndex < runs.length; runIndex += 1) {
                const run = runs[runIndex];
                if (String(run?.type || '').toLowerCase() !== 'drawing' || !run?.drawing) {
                    continue;
                }

                objects.push(normalizeCanvasImageObject({
                    model,
                    block,
                    blockIndex,
                    run,
                    runIndex,
                    body,
                    objectRole: 'drawingRun',
                }));
            }
        }
    }

    return objects.sort((left, right) =>
        (Number(left.zIndex || 0) - Number(right.zIndex || 0))
        || (left.blockIndex - right.blockIndex)
        || (left.runIndex - right.runIndex));
}

export function normalizeCanvasImageObject(context) {
    const model = context?.model || {};
    const block = context?.block || {};
    const run = context?.run || null;
    const body = context?.body || { x: 72, y: 72, width: 480, height: 680 };
    const source = run?.drawing || block?.content?.image || {};
    const layout = source.layout || source.Layout || {};
    const transform = layout.transform || layout.Transform || {};
    const position = layout.position || layout.Position || {};
    const wrap = layout.wrap || layout.Wrap || {};
    const stacking = layout.stacking || layout.Stacking || {};
    const anchor = layout.anchor || layout.Anchor || {};
    const sourceSize = source.size || source.Size || {};
    const naturalSize = source.naturalSize || source.NaturalSize || {};
    const width = Math.max(24, Number(
        transform.width ?? transform.Width
        ?? layout.width ?? layout.Width
        ?? sourceSize.width ?? sourceSize.Width
        ?? naturalSize.width ?? naturalSize.Width
        ?? DEFAULT_IMAGE_WIDTH) || DEFAULT_IMAGE_WIDTH);
    const height = Math.max(24, Number(
        transform.height ?? transform.Height
        ?? layout.height ?? layout.Height
        ?? sourceSize.height ?? sourceSize.Height
        ?? naturalSize.height ?? naturalSize.Height
        ?? DEFAULT_IMAGE_HEIGHT) || DEFAULT_IMAGE_HEIGHT);
    const transformFlip = transform.flip || transform.Flip || {};
    const wrapMode = normalizeWrapModeName(
        layout.wrapMode ?? layout.WrapMode
        ?? wrap.mode ?? wrap.Mode);
    const layoutKind = layoutKindName(layout.kind ?? layout.Kind, wrapMode);
    const isInline = layoutKind === 'Inline' || wrapMode === 'Inline';
    const x = resolveObjectX({
        body,
        width,
        layoutKind,
        position,
        alignment: source.alignment ?? source.Alignment,
        fallbackIndex: Number(context?.blockIndex || 0) || 0,
    });
    const y = Number(position.y ?? position.Y ?? layout.y ?? layout.Y);
    // Preserve absence as '' so legacy/JS-authored objects (no reference frame) keep their
    // historical body-absolute placement, while objects that explicitly declare a frame
    // (e.g. C# wrapped images with VerticalRelativeTo=Paragraph) are honored.
    const verticalRelativeTo = relativePositionName(
        position.verticalRelativeTo ?? position.VerticalRelativeTo
        ?? layout.verticalRelativeTo ?? layout.VerticalRelativeTo,
        '');
    const zIndex = Number(stacking.zIndex ?? stacking.ZIndex ?? layout.zIndex ?? layout.ZIndex ?? 0) || 0;
    const asset = findImageAsset(model, source.assetId ?? source.AssetId);
    const url = resolveImageUrl(model, source);
    const caption = String(source.caption ?? source.Caption ?? asset?.caption ?? asset?.Caption ?? '');
    const altText = String(source.altText ?? source.AltText ?? asset?.altText ?? asset?.AltText ?? '');
    const isDecorative = (source.isDecorative ?? source.IsDecorative ?? false) === true;
    const kind = normalizeDrawingKind(source.kind ?? source.Kind, source);
    const objectId = String(
        source.objectId ?? source.ObjectId
        ?? source.id ?? source.Id
        ?? run?.id ?? run?.Id
        ?? block.id ?? block.Id
        ?? '');

    return {
        id: `${objectId || block.id || 'image'}-${context?.objectRole || 'image'}`,
        objectId,
        blockId: String(block.id || ''),
        runId: String(run?.id || ''),
        role: context?.objectRole || 'imageBlock',
        blockIndex: Number(context?.blockIndex || 0) || 0,
        runIndex: Number(context?.runIndex ?? -1),
        source,
        kind,
        url,
        assetId: String(source.assetId ?? source.AssetId ?? ''),
        altText,
        isDecorative,
        caption,
        linkUrl: String(source.linkUrl ?? source.LinkUrl ?? ''),
        shape: normalizeDrawingShape(source.shape ?? source.Shape, kind),
        textBody: normalizeDrawingTextBody(source.textBody ?? source.TextBody),
        chart: normalizeDrawingChart(source.chart ?? source.Chart),
        group: source.group ?? source.Group ?? null,
        width,
        height,
        naturalWidth: Number(naturalSize.width ?? naturalSize.Width ?? width) || width,
        naturalHeight: Number(naturalSize.height ?? naturalSize.Height ?? height) || height,
        lockAspectRatio: (transform.lockAspectRatio ?? transform.LockAspectRatio ?? sourceSize.lockAspectRatio ?? sourceSize.LockAspectRatio ?? true) !== false,
        rotation: normalizeRotation(transform.rotation ?? transform.Rotation ?? source.shape?.rotation ?? source.Shape?.Rotation ?? 0),
        flipHorizontal: (transformFlip.horizontal ?? transformFlip.Horizontal ?? transform.flipH ?? transform.FlipH ?? false) === true,
        flipVertical: (transformFlip.vertical ?? transformFlip.Vertical ?? transform.flipV ?? transform.FlipV ?? false) === true,
        wrapMode,
        layoutKind,
        isInline,
        isFloating: !isInline,
        wrapMargin: Math.max(0, Number(layout.wrapMargin ?? layout.WrapMargin ?? wrap.margin ?? wrap.Margin ?? DEFAULT_WRAP_MARGIN) || 0),
        distanceLeft: distance(wrap, 'left', layout, DEFAULT_WRAP_MARGIN),
        distanceRight: distance(wrap, 'right', layout, DEFAULT_WRAP_MARGIN),
        distanceTop: distance(wrap, 'top', layout, DEFAULT_WRAP_MARGIN),
        distanceBottom: distance(wrap, 'bottom', layout, DEFAULT_WRAP_MARGIN),
        allowOverlap: (stacking.allowOverlap ?? stacking.AllowOverlap ?? false) === true,
        zIndex,
        x,
        explicitY: Number.isFinite(y) ? y : null,
        verticalRelativeTo,
        anchorBlockId: String(anchor.blockId ?? anchor.BlockId ?? layout.anchorBlockId ?? layout.AnchorBlockId ?? block.id ?? ''),
        anchorOffset: Number(anchor.offset ?? anchor.Offset ?? layout.anchorOffset ?? layout.AnchorOffset ?? 0) || 0,
    };
}

export function resolveImageUrl(model, source) {
    const direct = source?.url ?? source?.Url;
    if (typeof direct === 'string' && direct.trim().length > 0) {
        return direct.trim();
    }

    const asset = findImageAsset(model, source?.assetId ?? source?.AssetId);
    return String(asset?.url ?? asset?.Url ?? '');
}

// B4 caption layout constants (must match the imageCaption display command style: 12px italic).
const CAPTION_FONT_SIZE = 12;
const CAPTION_FONT_FAMILY = 'Aptos, Arial, sans-serif';
const CAPTION_LINE_HEIGHT = 15;
const CAPTION_PADDING_X = 2;
const CAPTION_PADDING_Y = 7;

// Greedy word-wrap of a caption to a maximum pixel width. Falls back to a per-character estimate when no
// font-metrics service is available (so a unit test without metrics still wraps deterministically).
function wrapCaptionLines(text, maxWidth, fontMetrics) {
    const trimmed = String(text || '').trim();
    if (!trimmed) {
        return [];
    }

    const measure = candidate => {
        if (fontMetrics && typeof fontMetrics.measureRun === 'function') {
            const result = fontMetrics.measureRun({ text: candidate, fontSize: CAPTION_FONT_SIZE, fontFamily: CAPTION_FONT_FAMILY, italic: true });
            return Number(result?.width || 0) || 0;
        }

        return candidate.length * CAPTION_FONT_SIZE * 0.5;
    };

    const words = trimmed.split(/\s+/).filter(Boolean);
    const lines = [];
    let current = '';
    for (const word of words) {
        const candidate = current ? `${current} ${word}` : word;
        if (!current || measure(candidate) <= maxWidth) {
            current = candidate;
        } else {
            lines.push(current);
            current = word;
        }
    }

    if (current) {
        lines.push(current);
    }

    return lines;
}

// B4: emit one imageCaption display command per wrapped caption line (the renderer paints single-line text),
// so a long caption no longer overflows to the right and collides with the wrapping body text. Returns the
// advanced sequence counter.
function pushCaptionCommands(commands, imageLayout, object, sequence) {
    if (!imageLayout.captionRect) {
        return sequence;
    }

    const captionLines = Array.isArray(imageLayout.captionLines) && imageLayout.captionLines.length > 0
        ? imageLayout.captionLines
        : [object.caption || ''];
    const style = {
        fontFamily: CAPTION_FONT_FAMILY,
        fontSize: CAPTION_FONT_SIZE,
        color: '#475569',
        fontStyle: 'italic',
        fontWeight: '400',
    };
    let seq = sequence;
    captionLines.forEach((lineText, lineIndex) => {
        const lineY = imageLayout.captionRect.y + lineIndex * CAPTION_LINE_HEIGHT;
        commands.push({
            id: `${imageLayout.objectId || imageLayout.blockId}-caption${lineIndex > 0 ? `-${lineIndex}` : ''}`,
            type: 'imageCaption',
            layer: 'content',
            pageIndex: Number(imageLayout.pageIndex || 0) || 0,
            blockId: imageLayout.blockId || '',
            objectId: imageLayout.objectId || '',
            text: lineText,
            x: imageLayout.captionRect.x,
            y: lineY,
            width: imageLayout.captionRect.width,
            height: CAPTION_LINE_HEIGHT,
            baseline: lineY + 12,
            style,
            sequence: seq++,
        });
    });
    return seq;
}

export function layoutCanvasImageObject(object, context) {
    const page = context?.page || {};
    const body = page.body || { x: 72, y: 72, width: 480, height: 680 };
    const fallbackY = Number(context?.y ?? body.y);
    const y = resolveObjectY(object, page, body, fallbackY);
    const captionX = Math.max(body.x, Math.min(body.x + body.width - object.width, Number(object.x || body.x) || body.x));
    const captionWidth = Math.min(body.width, Math.max(24, object.width));
    // B4: wrap the caption to the image width (it used to be a single un-clipped line that overflowed to the
    // right and collided with the wrapping body text). captionRect grows to fit the wrapped lines.
    const captionLines = object.caption
        ? wrapCaptionLines(String(object.caption), Math.max(8, captionWidth - 2 * CAPTION_PADDING_X), context?.fontMetrics)
        : [];
    const captionHeight = captionLines.length > 0
        ? captionLines.length * CAPTION_LINE_HEIGHT + CAPTION_PADDING_Y
        : 0;
    return {
        id: object.id,
        blockId: object.blockId,
        runId: object.runId,
        objectId: object.objectId,
        type: 'image',
        role: object.role,
        pageIndex: Number(page.index || 0) || 0,
        sequence: Number(context?.sequence || 0) || 0,
        rect: {
            x: captionX,
            y,
            width: captionWidth,
            height: Math.max(24, object.height),
        },
        captionLines,
        captionRect: object.caption ? {
            x: captionX,
            y: y + object.height + 4,
            width: captionWidth,
            height: captionHeight,
        } : null,
        object: {
            ...object,
            pageIndex: Number(page.index || 0) || 0,
        },
        lines: [],
        segments: [],
        caretStops: [],
    };
}

export function imageDisplayCommands(imageLayout, sequenceStart = 0, options = {}) {
    const object = imageLayout.object || {};
    if (object.kind && object.kind !== 'image') {
        return drawingDisplayCommands(imageLayout, sequenceStart, options);
    }

    const commands = [];
    let sequence = sequenceStart;
    // Behind-text objects must paint under the body text (their own dedicated z-band), not on the
    // content layer where ordering depends on paint sequence. page-background sits below content, so
    // the object is guaranteed behind every text run; in-front objects stay on the objects layer.
    const layer = object.wrapMode === 'BehindText' ? 'page-background' : 'objects';
    commands.push({
        id: `${imageLayout.objectId || imageLayout.blockId}-image`,
        type: 'imageObject',
        layer,
        pageIndex: Number(imageLayout.pageIndex || 0) || 0,
        blockId: imageLayout.blockId || '',
        runId: imageLayout.runId || '',
        objectId: imageLayout.objectId || '',
        role: imageLayout.role || 'imageBlock',
        x: imageLayout.rect.x,
        y: imageLayout.rect.y,
        width: imageLayout.rect.width,
        height: imageLayout.rect.height,
        rotation: Number(object.rotation || 0) || 0,
        flipHorizontal: object.flipHorizontal === true,
        flipVertical: object.flipVertical === true,
        url: object.url || '',
        altText: object.altText || '',
        isDecorative: object.isDecorative === true,
        caption: object.caption || '',
        linkUrl: object.linkUrl || '',
        wrapMode: object.wrapMode || 'Inline',
        zIndex: Number(object.zIndex || 0) || 0,
        fill: 'rgba(226, 232, 240, 0.72)',
        stroke: object.altText || object.isDecorative ? '#94a3b8' : '#f59e0b',
        lineWidth: object.altText || object.isDecorative ? 1 : 1.5,
        sequence: sequence++,
    });
    sequence = pushCaptionCommands(commands, imageLayout, object, sequence);

    return commands;
}

export function drawingDisplayCommands(imageLayout, sequenceStart = 0, options = {}) {
    const object = imageLayout.object || {};
    const commands = [];
    let sequence = sequenceStart;
    // Behind-text objects must paint under the body text (their own dedicated z-band), not on the
    // content layer where ordering depends on paint sequence. page-background sits below content, so
    // the object is guaranteed behind every text run; in-front objects stay on the objects layer.
    const layer = object.wrapMode === 'BehindText' ? 'page-background' : 'objects';
    const kind = object.kind || 'shape';
    const shape = normalizeDrawingShape(object.shape, kind);
    const base = {
        id: `${imageLayout.objectId || imageLayout.blockId}-${kind}`,
        type: kind === 'chart' ? 'drawingChart' : (kind === 'line' || kind === 'connector' ? 'drawingLine' : 'drawingShape'),
        layer,
        pageIndex: Number(imageLayout.pageIndex || 0) || 0,
        blockId: imageLayout.blockId || '',
        runId: imageLayout.runId || '',
        objectId: imageLayout.objectId || '',
        role: imageLayout.role || 'drawingRun',
        kind,
        x: imageLayout.rect.x,
        y: imageLayout.rect.y,
        width: imageLayout.rect.width,
        height: imageLayout.rect.height,
        rotation: Number(object.rotation ?? shape.rotation ?? 0) || 0,
        flipHorizontal: object.flipHorizontal === true,
        flipVertical: object.flipVertical === true,
        shape,
        chart: object.chart || null,
        chartLayout: kind === 'chart' ? buildDrawingChartLayout(object.chart || {}, imageLayout.rect) : null,
        connector: kind === 'line' || kind === 'connector'
            ? buildConnectorRoute(imageLayout, options.objectLayouts || [])
            : null,
        wrapMode: object.wrapMode || 'Inline',
        zIndex: Number(object.zIndex || 0) || 0,
        sequence: sequence++,
    };
    if (kind === 'shape' || kind === 'textBox' || kind === 'group') {
        commands.push({ ...base, metadataOnly: true });
        for (const part of drawingShapePaintCommands(base, sequence)) {
            commands.push(part);
            sequence += 1;
        }
    } else {
        commands.push(base);
    }

    const textCommands = drawingTextCommands(imageLayout, object.textBody, sequence, options);
    commands.push(...textCommands);
    sequence += textCommands.length;

    sequence = pushCaptionCommands(commands, imageLayout, object, sequence);

    return commands;
}

function drawingShapePaintCommands(base, sequenceStart) {
    const shape = base.shape || {};
    const commands = [];
    let sequence = sequenceStart;
    if (shape.shadow) {
        commands.push({
            ...base,
            id: `${base.id}-effect`,
            type: 'drawingShapeEffect',
            metadataOnly: false,
            paintPart: 'effect',
            sequence: sequence++,
        });
    }

    if (String(shape.fill?.type || 'solid').toLowerCase() !== 'none') {
        commands.push({
            ...base,
            id: `${base.id}-fill`,
            type: 'drawingShapeFill',
            metadataOnly: false,
            paintPart: 'fill',
            sequence: sequence++,
        });
    }

    if (Number(shape.stroke?.width ?? 1.5) > 0) {
        commands.push({
            ...base,
            id: `${base.id}-stroke`,
            type: 'drawingShapeStroke',
            metadataOnly: false,
            paintPart: 'stroke',
            sequence: sequence++,
        });
    }

    return commands;
}

function buildConnectorRoute(imageLayout, objectLayouts) {
    const object = imageLayout.object || {};
    const shape = object.shape || {};
    const rect = imageLayout.rect || {};
    const start = resolveConnectorEndpoint(shape.startConnection, objectLayouts)
        || normalizedConnectorPoint(shape.points?.[0], rect)
        || { x: rect.x, y: rect.y + rect.height / 2 };
    const end = resolveConnectorEndpoint(shape.endConnection, objectLayouts)
        || normalizedConnectorPoint(shape.points?.[1], rect)
        || { x: rect.x + rect.width, y: rect.y + rect.height / 2 };
    const preset = String(shape.preset || '').replace(/[\s_-]/g, '').toLowerCase();
    const routing = String(shape.routing || shape.Routing || (preset.includes('bent') || preset.includes('elbow') ? 'elbow' : 'straight'))
        .replace(/[\s_-]/g, '')
        .toLowerCase();
    const points = routing === 'elbow' || routing === 'orthogonal'
        ? elbowRoute(start, end)
        : [{ ...start }, { ...end }];
    return {
        routing,
        start,
        end,
        points: points.map(point => ({ x: round(point.x), y: round(point.y) })),
        startConnection: normalizeConnection(shape.startConnection),
        endConnection: normalizeConnection(shape.endConnection),
    };
}

function resolveConnectorEndpoint(connection, objectLayouts) {
    const normalized = normalizeConnection(connection);
    if (!normalized.objectId) {
        return null;
    }

    const layout = (objectLayouts || []).find(item => String(item?.objectId || item?.object?.objectId || '') === normalized.objectId);
    const rect = layout?.rect;
    if (!rect) {
        return null;
    }

    return sitePoint(rect, normalized.site);
}

function normalizeConnection(connection) {
    const source = connection && typeof connection === 'object' ? connection : {};
    return {
        objectId: String(source.objectId ?? source.ObjectId ?? ''),
        site: String(source.site ?? source.Site ?? 'center').replace(/[\s_-]/g, '').toLowerCase(),
    };
}

function sitePoint(rect, site) {
    const x = Number(rect.x || 0) || 0;
    const y = Number(rect.y || 0) || 0;
    const width = Math.max(1, Number(rect.width || 0) || 1);
    const height = Math.max(1, Number(rect.height || 0) || 1);
    switch (site) {
        case 'left':
            return { x, y: y + height / 2 };
        case 'right':
            return { x: x + width, y: y + height / 2 };
        case 'top':
            return { x: x + width / 2, y };
        case 'bottom':
            return { x: x + width / 2, y: y + height };
        default:
            return { x: x + width / 2, y: y + height / 2 };
    }
}

function normalizedConnectorPoint(point, rect) {
    if (!point || typeof point !== 'object') {
        return null;
    }

    const px = Number(point.x ?? point.X);
    const py = Number(point.y ?? point.Y);
    if (!Number.isFinite(px) || !Number.isFinite(py)) {
        return null;
    }

    const x = px >= 0 && px <= 1 ? (Number(rect.x || 0) || 0) + px * Math.max(1, Number(rect.width || 0) || 1) : px;
    const y = py >= 0 && py <= 1 ? (Number(rect.y || 0) || 0) + py * Math.max(1, Number(rect.height || 0) || 1) : py;
    return { x, y };
}

function elbowRoute(start, end) {
    const midX = start.x + (end.x - start.x) / 2;
    return [
        { ...start },
        { x: midX, y: start.y },
        { x: midX, y: end.y },
        { ...end },
    ];
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

    return Math.round(normalized * 1000) / 1000;
}

function drawingTextCommands(imageLayout, textBody, sequenceStart, options = {}) {
    const body = normalizeDrawingTextBody(textBody);
    if (!body.paragraphs.length) {
        return [];
    }

    const commands = [];
    const metrics = options.fontMetrics || createFontMetricsService();
    const left = Number(body.insetLeft || 0) || 0;
    const top = Number(body.insetTop || 0) || 0;
    const right = Number(body.insetRight || 0) || 0;
    const bottom = Number(body.insetBottom || 0) || 0;
    const contentWidth = Math.max(1, imageLayout.rect.width - left - right);
    const contentHeight = Math.max(1, imageLayout.rect.height - top - bottom);
    const lines = layoutDrawingTextLines(body, contentWidth, metrics);
    const totalHeight = drawingTextLayoutHeight(lines);
    const baseY = imageLayout.rect.y + top + verticalTextOffset(body.verticalAlignment, contentHeight, totalHeight);
    let sequence = sequenceStart;
    for (let index = 0; index < lines.length; index += 1) {
        const line = lines[index];
        const style = line.style || {};
        const y = baseY + (Number(line.y || 0) || 0);
        commands.push({
            id: `${imageLayout.objectId || imageLayout.blockId}-drawing-text-${index}`,
            type: 'drawingText',
            layer: 'objects',
            pageIndex: Number(imageLayout.pageIndex || 0) || 0,
            blockId: imageLayout.blockId || '',
            runId: imageLayout.runId || '',
            objectId: imageLayout.objectId || '',
            text: line.text,
            x: imageLayout.rect.x + left,
            y,
            baseline: y + line.lineHeight * 0.78,
            width: contentWidth,
            height: line.lineHeight,
            align: line.alignment,
            paragraphIndex: Number(line.paragraphIndex || 0) || 0,
            lineIndex: Number(line.lineIndex || index) || 0,
            textStart: Math.max(0, Number(line.textStart || 0) || 0),
            textEnd: Math.max(0, Number(line.textEnd ?? line.textStart ?? 0) || 0),
            style: {
                fontFamily: style.fontFamily || 'Aptos, Arial, sans-serif',
                fontSize: line.fontSize,
                color: style.color || '#0f172a',
                fontWeight: style.bold === true ? '700' : '400',
                fontStyle: style.italic === true ? 'italic' : 'normal',
            },
            sequence: sequence++,
        });
        if (y + line.lineHeight > imageLayout.rect.y + imageLayout.rect.height - bottom + 0.1) {
            break;
        }
    }

    return commands;
}

function verticalTextOffset(alignment, contentHeight, totalHeight) {
    const free = Math.max(0, contentHeight - totalHeight);
    const normalized = String(alignment || 'top').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'middle' || normalized === 'center') {
        return free / 2;
    }

    if (normalized === 'bottom' || normalized === 'end') {
        return free;
    }

    return 0;
}

function normalizeDrawingKind(value, source) {
    if (typeof value === 'number') {
        return ['image', 'shape', 'textBox', 'line', 'connector', 'chart', 'group'][Math.max(0, Math.min(6, Math.trunc(value)))] || 'image';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'shape' || normalized === 'autoshape' || normalized === 'freeform') return 'shape';
    if (normalized === 'textbox' || normalized === 'text') return 'textBox';
    if (normalized === 'line') return 'line';
    if (normalized === 'connector') return 'connector';
    if (normalized === 'chart') return 'chart';
    if (normalized === 'group') return 'group';
    if (source?.chart || source?.Chart) return 'chart';
    if (source?.textBody || source?.TextBody) return 'textBox';
    if (source?.shape || source?.Shape) return 'shape';
    return 'image';
}

function normalizeDrawingShape(shape, kind) {
    const source = shape && typeof shape === 'object' ? shape : {};
    const fill = source.fill || source.Fill || DEFAULT_DRAWING_FILL;
    const stroke = source.stroke || source.Stroke || DEFAULT_DRAWING_STROKE;
    return {
        preset: String(source.preset ?? source.Preset ?? defaultPresetForKind(kind) ?? 'rectangle'),
        fill: {
            type: String(fill.type ?? fill.Type ?? DEFAULT_DRAWING_FILL.type),
            color: String(fill.color ?? fill.Color ?? DEFAULT_DRAWING_FILL.color),
            secondaryColor: fill.secondaryColor ?? fill.SecondaryColor ?? null,
            opacity: clamp01(fill.opacity ?? fill.Opacity ?? DEFAULT_DRAWING_FILL.opacity),
            angle: Number(fill.angle ?? fill.Angle ?? 0) || 0,
        },
        stroke: {
            color: String(stroke.color ?? stroke.Color ?? DEFAULT_DRAWING_STROKE.color),
            width: Math.max(0, Number(stroke.width ?? stroke.Width ?? DEFAULT_DRAWING_STROKE.width) || 0),
            dash: String(stroke.dash ?? stroke.Dash ?? DEFAULT_DRAWING_STROKE.dash),
            opacity: clamp01(stroke.opacity ?? stroke.Opacity ?? DEFAULT_DRAWING_STROKE.opacity),
            lineCap: String(stroke.lineCap ?? stroke.LineCap ?? DEFAULT_DRAWING_STROKE.lineCap),
            lineJoin: String(stroke.lineJoin ?? stroke.LineJoin ?? DEFAULT_DRAWING_STROKE.lineJoin),
            startArrow: stroke.startArrow ?? stroke.StartArrow ?? null,
            endArrow: stroke.endArrow ?? stroke.EndArrow ?? null,
        },
        shadow: source.shadow ?? source.Shadow ?? null,
        rotation: Number(source.rotation ?? source.Rotation ?? 0) || 0,
        adjustments: source.adjustments ?? source.Adjustments ?? {},
        points: Array.isArray(source.points ?? source.Points) ? (source.points ?? source.Points) : [],
        startConnection: source.startConnection ?? source.StartConnection ?? null,
        endConnection: source.endConnection ?? source.EndConnection ?? null,
        routing: source.routing ?? source.Routing ?? null,
    };
}

function normalizeDrawingTextBody(textBody) {
    const source = textBody && typeof textBody === 'object' ? textBody : {};
    const paragraphs = Array.isArray(source.paragraphs ?? source.Paragraphs) ? (source.paragraphs ?? source.Paragraphs) : [];
    return {
        paragraphs: paragraphs.map(paragraph => {
            const style = paragraph?.style || paragraph?.Style || {};
            return {
                text: String(paragraph?.text ?? paragraph?.Text ?? ''),
                alignment: String(paragraph?.alignment ?? paragraph?.Alignment ?? 'left'),
                style: {
                    fontFamily: String(style.fontFamily ?? style.FontFamily ?? 'Aptos, Arial, sans-serif'),
                    fontSize: Number(style.fontSize ?? style.FontSize ?? 14) || 14,
                    color: String(style.color ?? style.Color ?? '#0f172a'),
                    bold: style.bold === true || style.Bold === true,
                    italic: style.italic === true || style.Italic === true,
                },
            };
        }),
        insetLeft: Number(source.insetLeft ?? source.InsetLeft ?? 8) || 0,
        insetTop: Number(source.insetTop ?? source.InsetTop ?? 6) || 0,
        insetRight: Number(source.insetRight ?? source.InsetRight ?? 8) || 0,
        insetBottom: Number(source.insetBottom ?? source.InsetBottom ?? 6) || 0,
        verticalAlignment: String(source.verticalAlignment ?? source.VerticalAlignment ?? 'top'),
        wrapText: (source.wrapText ?? source.WrapText ?? true) !== false,
        autoFit: String(source.autoFit ?? source.AutoFit ?? 'none'),
    };
}

function normalizeDrawingChart(chart) {
    const source = chart && typeof chart === 'object' ? chart : null;
    if (!source) {
        return null;
    }

    const series = Array.isArray(source.series ?? source.Series) ? (source.series ?? source.Series) : [];
    return {
        type: String(source.type ?? source.Type ?? 'bar'),
        title: source.title ?? source.Title ?? null,
        categories: Array.isArray(source.categories ?? source.Categories) ? (source.categories ?? source.Categories).map(item => String(item)) : [],
        series: series.map(item => ({
            name: String(item?.name ?? item?.Name ?? ''),
            values: Array.isArray(item?.values ?? item?.Values) ? (item.values ?? item.Values).map(value => Number(value) || 0) : [],
            color: item?.color ?? item?.Color ?? null,
        })),
        showLegend: (source.showLegend ?? source.ShowLegend ?? true) !== false,
        palette: Array.isArray(source.palette ?? source.Palette) ? (source.palette ?? source.Palette).map(item => String(item)) : [],
    };
}

function defaultPresetForKind(kind) {
    if (kind === 'line' || kind === 'connector') return 'line';
    if (kind === 'chart') return 'rectangle';
    return 'rectangle';
}

function clamp01(value) {
    return Math.max(0, Math.min(1, Number(value ?? 1) || 0));
}

function round(value) {
    return Math.round((Number(value) || 0) * 1000) / 1000;
}

export function objectExclusionIntervals(objects, page, y, lineHeight) {
    const body = page?.body || {};
    const rowTop = Number(y || 0) || 0;
    const rowBottom = rowTop + Math.max(1, Number(lineHeight || 16) || 16);
    const exclusions = (objects || [])
        .filter(layout => (layout?.object?.isFloating ?? layout?.isFloating) && shouldExcludeText(layout))
        // Use the axis-aligned bounding box of the (possibly rotated) object so text reserves space for the
        // rotated corners instead of flowing over them.
        .map(layout => ({ layout, aabb: aabbOfRotatedRect(layout.rect || {}, layout.object?.rotation) }))
        .filter(({ layout, aabb }) => {
            const object = layout.object || {};
            const top = aabb.y - Number(object.distanceTop || 0);
            const contentBottom = layout.captionRect
                ? layout.captionRect.y + layout.captionRect.height
                : aabb.y + aabb.height;
            const bottom = contentBottom + Number(object.distanceBottom || 0);
            return rowBottom >= top && rowTop <= bottom;
        })
        .map(({ layout, aabb }) => ({
            x: aabb.x - Number(layout.object?.distanceLeft || 0),
            width: aabb.width + Number(layout.object?.distanceLeft || 0) + Number(layout.object?.distanceRight || 0),
        }))
        .sort((left, right) => left.x - right.x);
    if (exclusions.length === 0) {
        return [{ x: body.x, width: body.width }];
    }

    const intervals = [];
    let cursor = Number(body.x || 0) || 0;
    const right = cursor + Math.max(1, Number(body.width || 1) || 1);
    for (const exclusion of exclusions) {
        const start = Math.max(cursor, Number(exclusion.x || 0) || 0);
        if (start > cursor + 24) {
            intervals.push({ x: cursor, width: start - cursor });
        }

        cursor = Math.max(cursor, start + Math.max(1, Number(exclusion.width || 1) || 1));
    }

    if (cursor < right - 24) {
        intervals.push({ x: cursor, width: right - cursor });
    }

    return intervals.length > 0 ? intervals : [{ x: body.x, width: body.width }];
}

// Axis-aligned bounding box of a rect rotated about its centre. Returns the rect unchanged when not rotated.
export function aabbOfRotatedRect(rect, rotation) {
    const x = Number(rect?.x || 0) || 0;
    const y = Number(rect?.y || 0) || 0;
    const width = Math.max(0, Number(rect?.width || 0) || 0);
    const height = Math.max(0, Number(rect?.height || 0) || 0);
    const degrees = Number(rotation) || 0;
    if (Math.abs(degrees) < 0.001) {
        return { x, y, width, height };
    }

    const rad = degrees * Math.PI / 180;
    const cos = Math.abs(Math.cos(rad));
    const sin = Math.abs(Math.sin(rad));
    const aabbWidth = width * cos + height * sin;
    const aabbHeight = width * sin + height * cos;
    const centerX = x + width / 2;
    const centerY = y + height / 2;
    return { x: centerX - aabbWidth / 2, y: centerY - aabbHeight / 2, width: aabbWidth, height: aabbHeight };
}

export function shouldExcludeText(imageLayout) {
    const mode = imageLayout?.object?.wrapMode || imageLayout?.wrapMode || 'Inline';
    return mode === 'Square' || mode === 'Tight' || mode === 'Through';
}

export function footprintHeight(imageLayout) {
    return imageLayout.rect.height + (imageLayout.captionRect ? imageLayout.captionRect.height + 4 : 0);
}

function findImageAsset(model, assetId) {
    const id = String(assetId || '');
    if (!id) {
        return null;
    }

    return (Array.isArray(model?.assets) ? model.assets : []).find(asset => String(asset?.id ?? asset?.Id ?? '') === id) || null;
}

function resolveObjectX({ body, width, layoutKind, position, alignment, fallbackIndex }) {
    const explicitX = Number(position.x ?? position.X);
    if (Number.isFinite(explicitX)) {
        return body.x + explicitX;
    }

    const align = alignmentName(position.horizontalAlignment ?? position.HorizontalAlignment ?? alignment);
    if (align === 'End' || align === 'Right') {
        return body.x + body.width - width;
    }

    if (align === 'Center' || layoutKind === 'Inline') {
        return body.x + Math.max(0, (body.width - width) / 2);
    }

    return body.x + Math.min(24, Math.max(0, fallbackIndex * 6));
}

// Resolves the top (y) of a positioned object honoring its vertical reference frame.
// Word/OnlyOffice anchor most wrapped images to the PARAGRAPH, where the vertical offset is
// measured from the anchoring paragraph (the current flow position), not the page body top.
// Treating every offset as body-relative pins every paragraph-anchored float (offset 0) to the
// top of the page, stacking them on top of each other and the first lines of text.
function resolveObjectY(object, page, body, fallbackY) {
    const flowY = Number.isFinite(Number(fallbackY)) ? Number(fallbackY) : body.y;
    if (!object.isFloating) {
        return Math.max(body.y, flowY || body.y);
    }

    const hasExplicitY = object.explicitY != null && Number.isFinite(Number(object.explicitY));
    const explicitY = hasExplicitY ? Number(object.explicitY) : 0;
    const relativeTo = relativePositionName(object.verticalRelativeTo, '');

    if (relativeTo === 'page') {
        return (Number(page?.y) || 0) + explicitY;
    }

    if (relativeTo === 'margin') {
        return body.y + explicitY;
    }

    // paragraph / line / character / column: the vertical offset is measured from the anchoring
    // paragraph (the current flow position), so a zero offset keeps the float beside its paragraph
    // (Word/OnlyOffice) instead of pinning every float to the top of the page body. Only objects
    // that actually participate in text flow (reserve/exclude space) track the paragraph; decorative
    // overlays and connectors (in-front / behind text) keep their stored body-absolute offset.
    const flowReference = relativeTo === 'paragraph' || relativeTo === 'line'
        || relativeTo === 'character' || relativeTo === 'column';
    if (flowReference && reservesTextSpace(object)) {
        return Math.max(body.y, flowY + explicitY);
    }

    // No flow reference (legacy / JS-authored objects, decorative overlays): keep the historical
    // body-absolute placement when an explicit offset exists, otherwise flow at the cursor.
    return hasExplicitY ? body.y + explicitY : Math.max(body.y, flowY || body.y);
}

function reservesTextSpace(object) {
    const mode = String(object?.wrapMode || '').toLowerCase();
    return mode === 'square' || mode === 'tight' || mode === 'through' || mode === 'topbottom';
}

function relativePositionName(value, fallback = '') {
    if (typeof value === 'number') {
        return ['page', 'margin', 'column', 'paragraph', 'character', 'line'][Math.max(0, Math.min(5, Math.trunc(value)))] || fallback;
    }

    const normalized = String(value || '').trim().toLowerCase();
    return normalized || fallback;
}

function distance(wrap, side, layout, fallback) {
    const key = `distance${side.charAt(0).toUpperCase()}${side.slice(1)}`;
    const pascal = `Distance${side.charAt(0).toUpperCase()}${side.slice(1)}`;
    const value = wrap?.[key] ?? wrap?.[pascal] ?? layout?.wrapMargin ?? layout?.WrapMargin ?? fallback;
    return Math.max(0, Number(value) || 0);
}

function layoutKindName(value, wrapMode) {
    if (typeof value === 'number') {
        return ['Inline', 'Anchored', 'Fixed'][Math.max(0, Math.min(2, Math.trunc(value)))] || 'Inline';
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === 'anchored') return 'Anchored';
    if (normalized === 'fixed') return 'Fixed';
    return wrapMode === 'Inline' ? 'Inline' : 'Anchored';
}

function alignmentName(value) {
    if (typeof value === 'number') {
        return ['Start', 'Center', 'End'][Math.max(0, Math.min(2, Math.trunc(value)))] || 'Center';
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === 'left' || normalized === 'start') return 'Start';
    if (normalized === 'right' || normalized === 'end') return 'End';
    return 'Center';
}

function canvasBlockType(block) {
    return String(block?.type || block?.content?.type || 'paragraph').replace(/[\s_-]/g, '').toLowerCase();
}

function isTextLikeBlock(type) {
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
}
