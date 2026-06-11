import { fontStringFromStyle } from '../../document-editor/layout/font-metrics.mjs';
import { applyPresetGeometryPath, buildPresetGeometryPath } from '../objects/geometry-preset.mjs';
import { buildDrawingChartLayout } from '../objects/chart-layout.mjs';
import { CANVAS_RENDER_LAYERS } from './layers.mjs';

const imageCache = new Map();

export function paintDisplayList(layers, displayList, options = {}) {
    const commands = Array.isArray(displayList?.commands) ? displayList.commands : [];
    const summary = {
        paintedCommandCount: 0,
        textRunCount: 0,
        mathEquationCount: 0,
        contentControlCount: 0,
        diagnosticCount: 0,
    };

    // Optional safety-net clip: confine body content to the page body so a mislaid-out run can
    // never bleed into the margins. Applied lazily per layer context and released at the end.
    const clipRect = isValidClipRect(options.clipRect) ? options.clipRect : null;
    const clippedContexts = clipRect ? new Set() : null;

    for (const command of commands) {
        const canvas = layers instanceof Map ? layers.get(command.layer) : layers?.[command.layer];
        const context = canvas?.getContext?.('2d');
        if (!context) {
            continue;
        }

        if (clipRect && !clippedContexts.has(context) && typeof context.clip === 'function' && typeof context.save === 'function') {
            context.save();
            context.beginPath?.();
            context.rect?.(clipRect.x, clipRect.y, clipRect.width, clipRect.height);
            context.clip();
            clippedContexts.add(context);
        }

        if (paintCommand(context, command, options)) {
            summary.paintedCommandCount++;
            if (command.type === 'textRun' || command.type === 'listLabel' || command.type === 'lineNumber') {
                summary.textRunCount++;
            }

            if (command.type === 'mathEquation') {
                summary.mathEquationCount++;
            }

            if (command.type === 'formControl') {
                summary.contentControlCount++;
            }

            if (command.layer === CANVAS_RENDER_LAYERS.diagnostics) {
                summary.diagnosticCount++;
            }
        }
    }

    if (clippedContexts) {
        for (const context of clippedContexts) {
            context.restore?.();
        }
    }

    return summary;
}

function isValidClipRect(rect) {
    return rect
        && Number.isFinite(Number(rect.x))
        && Number.isFinite(Number(rect.y))
        && Number(rect.width) > 0
        && Number(rect.height) > 0;
}

export function paintCommand(context, command, options = {}) {
    switch (command?.type) {
        case 'pageFill':
        case 'bodyArea':
            fillRect(context, command);
            return true;
        case 'pageBorder':
        case 'marginGuide':
        case 'columnSeparator':
            strokeRect(context, command);
            return true;
        case 'watermarkText':
            paintWatermarkText(context, command);
            return true;
        case 'watermarkImage':
            paintWatermarkImage(context, command);
            return true;
        case 'tableBox':
        case 'drawingRun':
        case 'headerFooterFrame':
            fillRect(context, command);
            strokeRect(context, command);
            return true;
        case 'imageObject':
            paintImageObject(context, command);
            return true;
        case 'drawingShape':
            if (command.metadataOnly === true) {
                return false;
            }

            paintDrawingShape(context, command);
            return true;
        case 'drawingShapeEffect':
        case 'drawingShapeFill':
        case 'drawingShapeStroke':
            paintDrawingShapePart(context, command, command.paintPart || command.type.replace('drawingShape', '').toLowerCase());
            return true;
        case 'drawingLine':
            paintDrawingLine(context, command);
            return true;
        case 'drawingChart':
            paintDrawingChart(context, command);
            return true;
        case 'imageCaption':
        case 'drawingText':
            paintTextRun(context, command);
            return true;
        case 'tabLeader':
            paintTabLeader(context, command);
            return true;
        case 'tableCell':
            fillRect(context, command);
            strokeRect(context, command);
            return true;
        case 'textRun':
        case 'field':
        case 'formControl':
        case 'listLabel':
        case 'lineNumber':
        case 'noteMarker':
            if (command.type === 'formControl') {
                paintFormControl(context, command);
            } else {
                paintTextRun(context, command);
            }
            return true;
        case 'mathEquation':
            paintMathEquation(context, command);
            return true;
        case 'glyphRun':
        case 'paragraphBox':
            return options.paintLayoutArtifacts === true ? paintLayoutArtifact(context, command) : false;
        case 'commentAnchor':
        case 'revisionAnchor':
            return paintAnnotation(context, command);
        case 'diagnosticOverlay':
        case 'debugBounds':
        case 'noteSeparator':
            strokeRect(context, command);
            return true;
        default:
            return false;
    }
}

function paintTabLeader(context, command) {
    const x = Number(command.x || 0) || 0;
    const y = Number(command.y || 0) || 0;
    const width = Math.max(0, Number(command.width || 0) || 0);
    const height = Math.max(1, Number(command.height || 14) || 14);
    const baseline = Number(command.baseline || 0) || y + height * 0.78;
    const color = command.style?.color || command.style?.foreground || '#334155';
    const leader = String(command.leader || 'dots').toLowerCase();

    context.save?.();
    context.strokeStyle = color;
    context.fillStyle = color;
    context.lineWidth = Math.max(1, Number(command.style?.fontSize || 11) / 15);

    if (leader === 'bar') {
        const barX = x + width;
        context.beginPath?.();
        context.moveTo?.(barX, y + 2);
        context.lineTo?.(barX, y + height - 2);
        context.stroke?.();
        context.restore?.();
        return;
    }

    const lineY = leader === 'underline'
        ? baseline + 2
        : baseline - Math.max(2, height * 0.18);
    if (leader === 'dash') {
        context.setLineDash?.([5, 4]);
        context.beginPath?.();
        context.moveTo?.(x, lineY);
        context.lineTo?.(x + width, lineY);
        context.stroke?.();
    } else if (leader === 'underline') {
        context.beginPath?.();
        context.moveTo?.(x, lineY);
        context.lineTo?.(x + width, lineY);
        context.stroke?.();
    } else {
        const radius = 1.1;
        for (let dotX = x + 3; dotX < x + width - 1; dotX += 6) {
            context.beginPath?.();
            context.arc?.(dotX, lineY, radius, 0, Math.PI * 2);
            context.fill?.();
        }
    }

    context.restore?.();
}

function paintDrawingShape(context, command) {
    paintDrawingShapePart(context, command, 'effect');
    paintDrawingShapePart(context, command, 'fill');
    paintDrawingShapePart(context, command, 'stroke');
}

function paintDrawingShapePart(context, command, part) {
    const rect = commandRect(command);
    const shape = command.shape || {};
    const fill = shape.fill || {};
    const stroke = shape.stroke || {};
    const normalizedPart = String(part || 'fill').toLowerCase();

    context.save?.();
    applyObjectTransform(context, rect, command);
    beginShapePath(context, String(shape.preset || 'rectangle'), rect, shape.adjustments);

    if (normalizedPart === 'effect') {
        if (shape.shadow && String(fill.type || 'solid').toLowerCase() !== 'none') {
            applyShadow(context, shape.shadow);
            context.fillStyle = createDrawingFillStyle(context, fill, rect);
            context.fill?.();
            clearShadow(context);
        }

        context.restore?.();
        return;
    }

    if (normalizedPart === 'fill' && String(fill.type || 'solid').toLowerCase() !== 'none') {
        context.fillStyle = createDrawingFillStyle(context, fill, rect);
        context.fill?.();
        context.restore?.();
        return;
    }

    if (normalizedPart === 'stroke' && Number(stroke.width ?? 1.5) > 0) {
        applyStroke(context, stroke);
        context.stroke?.();
    }

    context.restore?.();
}

function paintDrawingLine(context, command) {
    const rect = commandRect(command);
    const stroke = command.shape?.stroke || {};
    const points = Array.isArray(command.connector?.points) && command.connector.points.length >= 2
        ? command.connector.points
        : null;
    context.save?.();
    if (!points) {
        applyObjectTransform(context, rect, command);
    }

    applyStroke(context, stroke);
    context.beginPath?.();
    if (points) {
        context.moveTo?.(Number(points[0].x || 0) || 0, Number(points[0].y || 0) || 0);
        for (const point of points.slice(1)) {
            context.lineTo?.(Number(point.x || 0) || 0, Number(point.y || 0) || 0);
        }
    } else {
        context.moveTo?.(rect.x, rect.y + rect.height / 2);
        context.lineTo?.(rect.x + rect.width, rect.y + rect.height / 2);
    }

    context.stroke?.();
    const start = points?.[0] || { x: rect.x, y: rect.y + rect.height / 2 };
    const second = points?.[1] || { x: rect.x + rect.width, y: rect.y + rect.height / 2 };
    const end = points?.at(-1) || { x: rect.x + rect.width, y: rect.y + rect.height / 2 };
    const beforeEnd = points?.at(-2) || { x: rect.x, y: rect.y + rect.height / 2 };
    paintArrowHead(context, beforeEnd.x, beforeEnd.y, end.x, end.y, stroke.endArrow);
    paintArrowHead(context, second.x, second.y, start.x, start.y, stroke.startArrow);
    context.restore?.();
}

function paintDrawingChart(context, command) {
    const rect = commandRect(command);
    const chart = command.chart || {};
    const layout = command.chartLayout || buildDrawingChartLayout(chart, rect);
    const title = String(layout.title ?? chart.title ?? '');
    const plot = layout.plotRect || {
        x: rect.x + 28,
        y: rect.y + (title ? 24 : 10),
        width: Math.max(1, rect.width - 46),
        height: Math.max(1, rect.height - (title ? 52 : 36)),
    };

    context.save?.();
    applyObjectTransform(context, rect, command);
    context.fillStyle = '#ffffff';
    context.strokeStyle = '#cbd5e1';
    context.lineWidth = 1;
    roundRectPath(context, rect.x, rect.y, rect.width, rect.height, 6);
    context.fill?.();
    context.stroke?.();

    if (title) {
        context.font = '600 13px Aptos, Arial, sans-serif';
        context.fillStyle = '#0f172a';
        context.fillText(title, rect.x + 12, rect.y + 17, Math.max(1, rect.width - 24));
    }

    paintChartGridAndLabels(context, layout);

    const type = String(layout.type || chart.type || 'bar').replace(/[\s_-]/g, '').toLowerCase();
    if (type === 'pie' || type === 'donut') {
        paintPieChart(context, layout);
    } else if (type === 'line' || type === 'area' || type === 'scatter') {
        paintLineAreaScatterChart(context, layout, type);
    } else {
        paintBarChart(context, layout);
    }

    paintChartLegend(context, layout);
    context.restore?.();
}

function paintChartGridAndLabels(context, layout) {
    const plot = layout.plotRect;
    if (!plot) {
        return;
    }

    context.strokeStyle = '#e2e8f0';
    context.lineWidth = 1;
    context.beginPath?.();
    context.moveTo?.(plot.x, plot.y + plot.height);
    context.lineTo?.(plot.x + plot.width, plot.y + plot.height);
    context.moveTo?.(plot.x, plot.y);
    context.lineTo?.(plot.x, plot.y + plot.height);
    context.stroke?.();

    context.font = '10px Aptos, Arial, sans-serif';
    context.fillStyle = '#64748b';
    for (const label of layout.categoryLabels || []) {
        context.textAlign = 'center';
        context.fillText(String(label.text || ''), label.x, label.y, 54);
    }
    context.textAlign = 'start';
}

function paintBarChart(context, layout) {
    for (const series of layout.seriesLayouts || []) {
        context.fillStyle = series.color || '#2563eb';
        for (const bar of series.bars || []) {
            context.fillRect(bar.x, bar.y, bar.width, bar.height);
        }
    }
}

function paintLineAreaScatterChart(context, layout, type) {
    for (const series of layout.seriesLayouts || []) {
        const points = Array.isArray(series.points) ? series.points : [];
        if (points.length === 0) {
            continue;
        }

        context.strokeStyle = series.color || '#2563eb';
        context.fillStyle = series.color || '#2563eb';
        if (type === 'area') {
            context.globalAlpha = 0.18;
            context.beginPath?.();
            context.moveTo?.(points[0].x, series.baselineY);
            for (const point of points) {
                context.lineTo?.(point.x, point.y);
            }
            context.lineTo?.(points.at(-1).x, series.baselineY);
            context.closePath?.();
            context.fill?.();
            context.globalAlpha = 1;
        }

        if (type !== 'scatter') {
            context.lineWidth = 2;
            context.beginPath?.();
            points.forEach((point, index) => {
                if (index === 0) {
                    context.moveTo?.(point.x, point.y);
                } else {
                    context.lineTo?.(point.x, point.y);
                }
            });
            context.stroke?.();
        }

        for (const point of points) {
            context.beginPath?.();
            context.arc?.(point.x, point.y, type === 'scatter' ? 3.5 : 2.4, 0, Math.PI * 2);
            context.fill?.();
        }
    }
}

function paintPieChart(context, layout) {
    const series = (layout.seriesLayouts || [])[0];
    if (!series) {
        return;
    }

    for (const slice of series.slices || []) {
        context.fillStyle = slice.color || '#2563eb';
        context.beginPath?.();
        context.moveTo?.(series.center.x, series.center.y);
        context.arc?.(series.center.x, series.center.y, series.radius, slice.startAngle, slice.endAngle);
        context.closePath?.();
        context.fill?.();
    }

    if (series.innerRadius > 0) {
        context.fillStyle = '#ffffff';
        context.beginPath?.();
        context.arc?.(series.center.x, series.center.y, series.innerRadius, 0, Math.PI * 2);
        context.fill?.();
    }
}

function paintChartLegend(context, layout) {
    const items = layout.legendItems || [];
    if (items.length === 0) {
        return;
    }

    context.font = '10px Aptos, Arial, sans-serif';
    context.textAlign = 'start';
    for (const item of items) {
        context.fillStyle = item.color || '#2563eb';
        context.fillRect(item.x, item.y + 3, 8, 8);
        context.fillStyle = '#475569';
        context.fillText(String(item.name || ''), item.x + 12, item.y + 11, Math.max(12, item.width - 12));
    }
}

function paintImageObject(context, command) {
    const x = Number(command.x || 0) || 0;
    const y = Number(command.y || 0) || 0;
    const width = Math.max(1, Number(command.width || 0) || 1);
    const height = Math.max(1, Number(command.height || 0) || 1);
    const url = String(command.url || '');
    const image = url ? resolveCachedImage(context, url) : null;
    const ready = image?.complete === true && image.naturalWidth > 0;

    context.save?.();
    // Rotate/flip the image about its centre (same transform watermarks use) so the bitmap, border and
    // alt-warning dot all turn together — previously paintImageObject ignored command.rotation entirely.
    applyObjectTransform(context, { x, y, width, height }, command);
    if (ready) {
        // Draw the real bitmap edge-to-edge — no grey fill beneath it (that placeholder is only for the
        // not-yet-decoded state). The image.onload hook (resolveCachedImage) repaints once the bitmap arrives.
        context.drawImage?.(image, x, y, width, height);
    } else {
        context.fillStyle = command.fill || 'rgba(226, 232, 240, 0.48)';
        context.fillRect(x, y, width, height);
    }

    context.strokeStyle = command.stroke || '#94a3b8';
    context.lineWidth = Math.max(0.5, Number(command.lineWidth) || 1);
    context.strokeRect(x, y, width, height);
    if (!command.altText && command.isDecorative !== true) {
        context.fillStyle = 'rgba(245, 158, 11, 0.92)';
        context.beginPath?.();
        context.arc?.(x + width - 10, y + 10, 4, 0, Math.PI * 2);
        context.fill?.();
    }

    context.restore?.();
}

function paintWatermarkText(context, command) {
    const style = command.style || {};
    const text = String(command.text || '');
    if (!text) {
        return;
    }

    context.save?.();
    context.globalAlpha = Math.max(0, Math.min(1, Number(command.opacity ?? 0.16) || 0.16));
    context.translate?.(Number(command.x || 0) || 0, Number(command.y || 0) || 0);
    context.rotate?.((Number(command.rotation || 0) || 0) * Math.PI / 180);
    context.font = fontStringFromStyle(style);
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillStyle = style.textColor || style.color || 'rgba(71, 85, 105, 0.52)';
    context.fillText?.(text, 0, 0, Math.max(1, Number(command.width || 1) || 1));
    context.restore?.();
}

function paintWatermarkImage(context, command) {
    const rect = commandRect(command);
    const url = String(command.imageUrl || command.url || '');
    if (!url) {
        return;
    }

    context.save?.();
    context.globalAlpha = Math.max(0, Math.min(1, Number(command.opacity ?? 0.16) || 0.16));
    applyObjectTransform(context, rect, command);
    const image = resolveCachedImage(context, url);
    if (image?.complete && image.naturalWidth > 0) {
        context.drawImage?.(image, rect.x, rect.y, rect.width, rect.height);
    }
    context.restore?.();
}

function commandRect(command) {
    return {
        x: Number(command.x || 0) || 0,
        y: Number(command.y || 0) || 0,
        width: Math.max(1, Number(command.width || 0) || 1),
        height: Math.max(1, Number(command.height || 0) || 1),
    };
}

function applyObjectTransform(context, rect, command) {
    const degrees = Number(command?.rotation || 0) || 0;
    const flipHorizontal = command?.flipHorizontal === true || command?.flipH === true;
    const flipVertical = command?.flipVertical === true || command?.flipV === true;
    if (Math.abs(degrees) < 0.001 && !flipHorizontal && !flipVertical) {
        return;
    }

    context.translate?.(rect.x + rect.width / 2, rect.y + rect.height / 2);
    if (Math.abs(degrees) >= 0.001) {
        context.rotate?.(degrees * Math.PI / 180);
    }

    if (flipHorizontal || flipVertical) {
        context.scale?.(flipHorizontal ? -1 : 1, flipVertical ? -1 : 1);
    }

    context.translate?.(-(rect.x + rect.width / 2), -(rect.y + rect.height / 2));
}

function applyStroke(context, stroke) {
    context.strokeStyle = colorWithOpacity(stroke.color || '#64748b', stroke.opacity);
    context.lineWidth = Math.max(0.5, Number(stroke.width ?? 1.5) || 1.5);
    context.lineCap = ['butt', 'round', 'square'].includes(String(stroke.lineCap || '').toLowerCase()) ? stroke.lineCap : 'round';
    context.lineJoin = ['miter', 'round', 'bevel'].includes(String(stroke.lineJoin || '').toLowerCase()) ? stroke.lineJoin : 'round';
    context.setLineDash?.(dashArray(stroke.dash));
}

function applyShadow(context, shadow) {
    if (!shadow) {
        return;
    }

    context.shadowColor = shadow.color || 'rgba(15, 23, 42, 0.22)';
    context.shadowBlur = Math.max(0, Number(shadow.blur ?? shadow.Blur ?? 6) || 0);
    context.shadowOffsetX = Number(shadow.offsetX ?? shadow.OffsetX ?? 0) || 0;
    context.shadowOffsetY = Number(shadow.offsetY ?? shadow.OffsetY ?? 2) || 0;
}

function createDrawingFillStyle(context, fill, rect) {
    const type = String(fill.type || 'solid').replace(/[\s_-]/g, '').toLowerCase();
    const primary = colorWithOpacity(fill.color || '#ffffff', fill.opacity);
    const secondarySource = fill.secondaryColor || fill.SecondaryColor;
    if (type !== 'lineargradient' || !secondarySource || typeof context.createLinearGradient !== 'function') {
        return primary;
    }

    const angle = (Number(fill.angle ?? fill.Angle ?? 0) || 0) * Math.PI / 180;
    const cx = rect.x + rect.width / 2;
    const cy = rect.y + rect.height / 2;
    const dx = Math.cos(angle) * rect.width / 2;
    const dy = Math.sin(angle) * rect.height / 2;
    const gradient = context.createLinearGradient(cx - dx, cy - dy, cx + dx, cy + dy);
    gradient.addColorStop?.(0, primary);
    gradient.addColorStop?.(1, colorWithOpacity(secondarySource, fill.opacity));
    return gradient;
}

function clearShadow(context) {
    context.shadowColor = 'rgba(0, 0, 0, 0)';
    context.shadowBlur = 0;
    context.shadowOffsetX = 0;
    context.shadowOffsetY = 0;
}

function beginShapePath(context, preset, rect, adjustments) {
    applyPresetGeometryPath(context, buildPresetGeometryPath(preset, rect, adjustments));
}

function roundRectPath(context, x, y, width, height, radius) {
    const r = Math.max(0, Math.min(radius, width / 2, height / 2));
    context.beginPath?.();
    context.moveTo?.(x + r, y);
    context.lineTo?.(x + width - r, y);
    context.quadraticCurveTo?.(x + width, y, x + width, y + r);
    context.lineTo?.(x + width, y + height - r);
    context.quadraticCurveTo?.(x + width, y + height, x + width - r, y + height);
    context.lineTo?.(x + r, y + height);
    context.quadraticCurveTo?.(x, y + height, x, y + height - r);
    context.lineTo?.(x, y + r);
    context.quadraticCurveTo?.(x, y, x + r, y);
    context.closePath?.();
}

function paintArrowHead(context, fromX, fromY, toX, toY, arrow) {
    const normalized = String(arrow || '').replace(/[\s_-]/g, '').toLowerCase();
    if (!normalized || normalized === 'none') {
        return;
    }

    const angle = Math.atan2(toY - fromY, toX - fromX);
    const length = 10;
    context.save?.();
    context.beginPath?.();
    context.moveTo?.(toX, toY);
    context.lineTo?.(toX - Math.cos(angle - Math.PI / 6) * length, toY - Math.sin(angle - Math.PI / 6) * length);
    context.lineTo?.(toX - Math.cos(angle + Math.PI / 6) * length, toY - Math.sin(angle + Math.PI / 6) * length);
    context.closePath?.();
    context.fillStyle = context.strokeStyle;
    context.fill?.();
    context.restore?.();
}

function dashArray(dash) {
    const normalized = String(dash || 'solid').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'dash') return [8, 5];
    if (normalized === 'dot') return [2, 4];
    if (normalized === 'dashdot') return [8, 4, 2, 4];
    return [];
}

function colorWithOpacity(color, opacity) {
    const value = String(color || '#000000');
    const alpha = Math.max(0, Math.min(1, Number(opacity ?? 1) || 0));
    if (alpha >= 0.999 || value.startsWith('rgba(') || value.startsWith('hsla(')) {
        return value;
    }

    if (/^#[0-9a-f]{6}$/i.test(value)) {
        const red = parseInt(value.slice(1, 3), 16);
        const green = parseInt(value.slice(3, 5), 16);
        const blue = parseInt(value.slice(5, 7), 16);
        return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
    }

    return value;
}

function resolveCachedImage(context, url) {
    if (imageCache.has(url)) {
        return imageCache.get(url);
    }

    const canvas = context?.canvas || null;
    const view = canvas?.ownerDocument?.defaultView || globalThis;
    if (typeof view.Image !== 'function') {
        return null;
    }

    const image = new view.Image();
    image.decoding = 'async';
    image.onload = () => {
        const repaint = context?.canvas?.__tmCanvasRepaint;
        if (typeof repaint === 'function') {
            repaint();
        }
    };
    image.src = url;
    imageCache.set(url, image);
    return image;
}

function fillRect(context, command) {
    context.save?.();
    context.fillStyle = command.fill || 'rgba(0, 0, 0, 0)';
    context.fillRect(command.x, command.y, command.width, command.height);
    context.restore?.();
}

function strokeRect(context, command) {
    context.save?.();
    context.strokeStyle = command.stroke || '#94a3b8';
    context.lineWidth = Math.max(0.5, Number(command.lineWidth) || 1);
    if (Array.isArray(command.dash)) {
        context.setLineDash?.(command.dash);
    }

    context.strokeRect(command.x, command.y, command.width, command.height);
    context.setLineDash?.([]);
    context.restore?.();
}

function paintTextRun(context, command) {
    const style = command.style || {};
    const baseline = Number(command.baseline) || Number(command.y) || 0;
    const x = Number(command.x) || 0;
    const text = String(command.text || '');
    const lineHeight = Math.max(1, Number(command.height) || Number(style.fontSize) * 1.25 || 16);
    const top = baseline - lineHeight * 0.78;

    context.save?.();
    context.font = fontStringFromStyle(style);
    context.textBaseline = 'alphabetic';
    if ('fontKerning' in context) {
        context.fontKerning = style.kerning === false || String(style.kerning).toLowerCase() === 'false' ? 'none' : 'normal';
    }
    if (style.backgroundColor) {
        context.fillStyle = style.backgroundColor;
        context.fillRect(x, top, command.width, lineHeight);
    }

    context.fillStyle = style.color || '#111827';
    paintAdvancedText(context, text, alignedTextX(context, command, text, x), baseline, style);

    if (Array.isArray(style.decorations) && style.decorations.length > 0) {
        paintDecorations(context, command, style, baseline, lineHeight);
    }

    context.restore?.();
}

function paintFormControl(context, command) {
    const style = command.style || {};
    const renderState = command.renderState || {};
    const baseline = Number(command.baseline) || Number(command.y) || 0;
    const text = String(command.text || '');
    const x = Number(command.x || 0) || 0;
    const height = Math.max(16, Number(command.height || 0) || Number(style.fontSize || 14) * 1.25);
    const width = Math.max(18, Number(command.width || 0) || 18);
    const isPlaceholder = command.isPlaceholder === true || renderState.placeholder === true;

    if (renderState.showChrome !== true) {
        context.save?.();
        context.font = fontStringFromStyle({
            ...style,
            fontStyle: isPlaceholder ? 'italic' : style.fontStyle,
        });
        context.textBaseline = 'alphabetic';
        context.fillStyle = renderState.typography?.color || (isPlaceholder ? '#64748b' : (style.color || '#111827'));
        paintAdvancedText(context, text, alignedTextX(context, command, text, x), baseline, style);
        context.restore?.();
        return;
    }

    const top = baseline - height * 0.78;
    const paddingX = Math.max(3, Math.min(8, height * 0.22));
    const invalid = command.validation && command.validation.valid === false;
    const locked = command.isLocked === true;
    const chrome = renderState.chrome || {};

    context.save?.();
    roundRectPath(context, x - paddingX, top - 1, width + paddingX * 2, height + 2, 4);
    context.fillStyle = chrome.fill || (invalid
        ? 'rgba(254, 226, 226, 0.82)'
        : locked ? 'rgba(226, 232, 240, 0.68)' : 'rgba(239, 246, 255, 0.74)');
    context.fill?.();
    context.strokeStyle = chrome.stroke || (invalid
        ? 'rgba(220, 38, 38, 0.8)'
        : locked ? 'rgba(100, 116, 139, 0.7)' : 'rgba(37, 99, 235, 0.68)');
    context.lineWidth = invalid ? 1.25 : 1;
    if (isPlaceholder) {
        context.setLineDash?.(Array.isArray(chrome.dash) && chrome.dash.length > 0 ? chrome.dash : [3, 3]);
    }
    context.stroke?.();
    context.setLineDash?.([]);

    if (renderState.showTag === true) {
        paintFormControlDesignTag(context, command, renderState, top, x - paddingX, height);
    }

    context.font = fontStringFromStyle({
        ...style,
        fontStyle: isPlaceholder ? 'italic' : style.fontStyle,
    });
    context.textBaseline = 'alphabetic';
    context.fillStyle = renderState.typography?.color || (isPlaceholder ? '#64748b' : (style.color || '#111827'));
    paintAdvancedText(context, text, alignedTextX(context, command, text, x), baseline, style);
    context.restore?.();
}

function paintFormControlDesignTag(context, command, renderState, top, x, height) {
    const tag = String(renderState.tagLabel || command.designTag || '').trim();
    if (!tag) {
        return;
    }

    const labelFontSize = Math.max(8, Math.min(11, (Number(command.style?.fontSize || 11) || 11) * 0.78));
    const labelPaddingX = Math.max(4, labelFontSize * 0.45);
    const labelHeight = Math.max(14, labelFontSize + 5);
    const labelY = Math.max(0, top - labelHeight + 1);

    context.save?.();
    context.font = `${labelFontSize}px sans-serif`;
    const labelWidth = Math.max(24, context.measureText?.(tag)?.width || tag.length * labelFontSize * 0.55) + labelPaddingX * 2;
    roundRectPath(context, x, labelY, labelWidth, labelHeight, Math.min(4, height * 0.22));
    context.fillStyle = renderState.chrome?.labelFill || 'rgba(30, 64, 175, 0.96)';
    context.fill?.();
    context.textBaseline = 'middle';
    context.fillStyle = renderState.chrome?.labelText || '#ffffff';
    paintAdvancedText(context, tag, x + labelPaddingX, labelY + labelHeight / 2, { fontSize: labelFontSize });
    context.restore?.();
}

function paintMathEquation(context, command) {
    const layout = command.mathLayout || null;
    if (!layout) {
        paintTextRun(context, command);
        return;
    }

    const style = command.style || {};
    context.save?.();
    context.fillStyle = style.color || '#111827';
    context.strokeStyle = style.color || '#111827';
    context.lineWidth = Math.max(1, (Number(style.fontSize) || 16) * 0.055);
    paintMathBox(context, layout, Number(command.x || 0) || 0, Number(command.y || 0) || 0, style);
    context.restore?.();
}

function paintMathBox(context, box, originX, originY, inheritedStyle) {
    if (!box) {
        return;
    }

    const x = originX + (Number(box.x || 0) || 0);
    const y = originY + (Number(box.y || 0) || 0);
    switch (box.type) {
        case 'run':
            paintMathRun(context, box, x, y, inheritedStyle);
            return;
        case 'fraction':
            paintMathChildren(context, box, x, y, inheritedStyle);
            paintMathRule(context, x, y + Number(box.ruleY || 0), Math.max(1, Number(box.ruleWidth || box.width) || 1), inheritedStyle);
            return;
        case 'radical':
            paintRadicalSign(context, box, x, y, inheritedStyle);
            paintMathChildren(context, box, x, y, inheritedStyle);
            return;
        case 'bar':
            paintMathRule(context, x, y + Number(box.ruleY || 0), Math.max(1, Number(box.ruleWidth || box.width) || 1), inheritedStyle);
            paintMathChildren(context, box, x, y, inheritedStyle);
            return;
        case 'matrix':
            paintMatrixBrackets(context, box, x, y, inheritedStyle);
            paintMathChildren(context, box, x, y, inheritedStyle);
            return;
        case 'borderBox':
            paintMathBorderBox(context, box, x, y, inheritedStyle);
            paintMathChildren(context, box, x, y, inheritedStyle);
            return;
        default:
            paintMathChildren(context, box, x, y, inheritedStyle);
    }
}

function paintMathChildren(context, box, x, y, inheritedStyle) {
    for (const child of Array.isArray(box.children) ? box.children : []) {
        paintMathBox(context, child, x, y, inheritedStyle);
    }
}

function paintMathRun(context, box, x, y, inheritedStyle) {
    const style = { ...(inheritedStyle || {}), ...(box.style || {}) };
    context.font = fontStringFromStyle(style);
    context.textBaseline = 'alphabetic';
    context.fillStyle = style.color || inheritedStyle?.color || '#111827';
    context.fillText(String(box.text || ''), x, y + Number(box.ascent || 0));
}

function paintMathRule(context, x, y, width, style) {
    context.save?.();
    context.strokeStyle = style?.color || '#111827';
    context.lineWidth = Math.max(1, (Number(style?.fontSize) || 16) * 0.055);
    context.beginPath?.();
    context.moveTo?.(x, Math.round(y) + 0.5);
    context.lineTo?.(x + width, Math.round(y) + 0.5);
    context.stroke?.();
    context.restore?.();
}

function paintMathBorderBox(context, box, x, y, style) {
    context.save?.();
    context.strokeStyle = style?.color || '#111827';
    context.lineWidth = Math.max(1, (Number(style?.fontSize) || 16) * 0.045);
    context.strokeRect?.(
        Math.round(x) + 0.5,
        Math.round(y) + 0.5,
        Math.max(1, Number(box.width || 0) || 1),
        Math.max(1, Number(box.height || 0) || 1));
    context.restore?.();
}

function paintRadicalSign(context, box, x, y, style) {
    const degreeWidth = Number(box.degreeWidth || 0) || 0;
    const symbolWidth = Math.max(8, Number(box.symbolWidth || 12) || 12);
    const left = x + degreeWidth;
    const top = y + Math.max(1, Number(box.height || 0) * 0.12);
    const bottom = y + Math.max(1, Number(box.height || 0) * 0.78);
    const mid = y + Math.max(1, Number(box.height || 0) * 0.56);
    const overbarY = top + 1;
    context.save?.();
    context.strokeStyle = style?.color || '#111827';
    context.lineWidth = Math.max(1, (Number(style?.fontSize) || 16) * 0.07);
    context.beginPath?.();
    context.moveTo?.(left + symbolWidth * 0.08, mid);
    context.lineTo?.(left + symbolWidth * 0.28, bottom);
    context.lineTo?.(left + symbolWidth * 0.55, overbarY);
    context.lineTo?.(x + Math.max(1, Number(box.width || 0) || 1), overbarY);
    context.stroke?.();
    context.restore?.();
}

function paintMatrixBrackets(context, box, x, y, style) {
    const height = Math.max(1, Number(box.height || 0) || 1);
    const width = Math.max(1, Number(box.width || 0) || 1);
    const inset = Math.max(3, (Number(style?.fontSize) || 16) * 0.18);
    context.save?.();
    context.strokeStyle = style?.color || '#111827';
    context.lineWidth = Math.max(1, (Number(style?.fontSize) || 16) * 0.055);
    context.beginPath?.();
    context.moveTo?.(x + inset, y);
    context.lineTo?.(x, y);
    context.lineTo?.(x, y + height);
    context.lineTo?.(x + inset, y + height);
    context.moveTo?.(x + width - inset, y);
    context.lineTo?.(x + width, y);
    context.lineTo?.(x + width, y + height);
    context.lineTo?.(x + width - inset, y + height);
    context.stroke?.();
    context.restore?.();
}

function alignedTextX(context, command, text, x) {
    const align = String(command.align || '').toLowerCase();
    if (align !== 'center' && align !== 'right' && align !== 'end') {
        return x;
    }

    const textWidth = Number(context.measureText?.(text)?.width) || 0;
    const width = Math.max(0, Number(command.width || 0) || 0);
    if (align === 'center') {
        return x + Math.max(0, (width - textWidth) / 2);
    }

    return x + Math.max(0, width - textWidth);
}

function paintAdvancedText(context, text, x, baseline, style) {
    const scale = Math.max(0.1, Number(style.characterScale || 1) || 1);
    const spacing = Number(style.letterSpacing || 0) || 0;
    if (Math.abs(scale - 1) < 0.0001 && Math.abs(spacing) < 0.0001) {
        context.fillText(text, x, baseline);
        return;
    }

    if (Math.abs(spacing) < 0.0001) {
        context.save?.();
        context.translate?.(x, 0);
        context.scale?.(scale, 1);
        context.fillText(text, 0, baseline);
        context.restore?.();
        return;
    }

    let cursor = x;
    const glyphs = Array.from(text);
    glyphs.forEach((glyph, index) => {
        context.save?.();
        context.translate?.(cursor, 0);
        context.scale?.(scale, 1);
        context.fillText(glyph, 0, baseline);
        context.restore?.();
        cursor += (Number(context.measureText?.(glyph)?.width) || 0) * scale;
        if (index < glyphs.length - 1) {
            cursor += spacing;
        }
    });
}

function paintDecorations(context, command, style, baseline, lineHeight) {
    const x = Number(command.x) || 0;
    const width = Math.max(1, Number(command.width) || 1);
    context.strokeStyle = style.color || '#111827';
    context.lineWidth = Math.max(1, Math.round((Number(style.fontSize) || 16) / 14));
    for (const decoration of style.decorations) {
        const positions = decoration === 'double-line-through'
            ? [
                baseline - lineHeight * 0.38,
                baseline - lineHeight * 0.27,
            ]
            : [
                decoration === 'line-through'
                    ? baseline - lineHeight * 0.33
                    : baseline + Math.max(1, (Number(style.fontSize) || 16) * 0.08),
            ];
        for (const y of positions) {
            context.beginPath?.();
            context.moveTo?.(x, Math.round(y) + 0.5);
            context.lineTo?.(x + width, Math.round(y) + 0.5);
            context.stroke?.();
        }
    }
}

function paintLayoutArtifact(context, command) {
    context.save?.();
    context.strokeStyle = 'rgba(14, 165, 233, 0.4)';
    context.lineWidth = 1;
    context.strokeRect(command.x, command.y, command.width, command.height);
    context.restore?.();
    return true;
}

function paintAnnotation(context, command) {
    context.save?.();
    context.strokeStyle = command.type === 'revisionAnchor' ? '#f59e0b' : '#2563eb';
    context.lineWidth = 2;
    context.beginPath?.();
    context.moveTo?.(command.x, command.y + command.height + 3);
    context.lineTo?.(command.x + command.width, command.y + command.height + 3);
    context.stroke?.();
    context.restore?.();
    return true;
}
