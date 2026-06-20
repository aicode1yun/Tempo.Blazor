import { CANVAS_RENDER_LAYERS } from './layers.mjs';

export function resolvePageBackground(model = {}, theme = {}) {
    const options = model.pageBackground || model.PageBackground || {};
    const watermark = options.watermark || options.Watermark || {};
    const border = options.border || options.Border || {};
    return {
        pageFill: firstString(options.color, options.Color, theme.pageBackgroundPaint, '#ffffff'),
        watermark: {
            enabled: watermark.enabled === true || watermark.Enabled === true,
            kind: String(watermark.kind ?? watermark.Kind ?? 'text').toLowerCase(),
            text: firstString(watermark.text, watermark.Text, ''),
            imageUrl: firstString(watermark.imageUrl, watermark.ImageUrl, ''),
            opacity: clampNumber(watermark.opacity ?? watermark.Opacity, 0, 1, 0.16),
            rotation: clampNumber(watermark.rotation ?? watermark.Rotation, -90, 90, -36),
            color: firstString(watermark.color, watermark.Color, 'rgba(71, 85, 105, 0.52)'),
        },
        border: {
            enabled: border.enabled === true || border.Enabled === true,
            color: firstString(border.color, border.Color, theme.pageBorderPaint, '#cbd5e1'),
            width: clampNumber(border.width ?? border.Width, 0.5, 12, 1),
            margin: clampNumber(border.margin ?? border.Margin, 0, 144, 0),
            alignTo: String(border.alignTo ?? border.AlignTo ?? 'page').toLowerCase(),
            dash: Array.isArray(border.dash) ? border.dash : Array.isArray(border.Dash) ? border.Dash : [],
        },
    };
}

export function buildWatermarkCommands(page, background) {
    const watermark = background?.watermark || {};
    if (watermark.enabled !== true) {
        return [];
    }

    const pageIndex = Number(page?.index || 0) || 0;
    const width = Math.max(1, Number(page?.width || 794) || 794);
    const height = Math.max(1, Number(page?.height || 1123) || 1123);
    if (watermark.kind === 'image' && watermark.imageUrl) {
        return [{
            id: `page-${pageIndex}-watermark-image`,
            type: 'watermarkImage',
            layer: CANVAS_RENDER_LAYERS.pageBackground,
            pageIndex,
            x: width * 0.18,
            y: height * 0.26,
            width: width * 0.64,
            height: height * 0.36,
            imageUrl: watermark.imageUrl,
            opacity: watermark.opacity,
            rotation: watermark.rotation,
        }];
    }

    if (!watermark.text) {
        return [];
    }

    return [{
        id: `page-${pageIndex}-watermark-text`,
        type: 'watermarkText',
        layer: CANVAS_RENDER_LAYERS.pageBackground,
        pageIndex,
        text: watermark.text,
        x: width / 2,
        y: height / 2,
        width: width * 0.78,
        height: 96,
        baseline: 0,
        rotation: watermark.rotation,
        opacity: watermark.opacity,
        style: {
            fontFamily: 'Aptos, Arial, sans-serif',
            fontSize: Math.max(32, Math.min(84, width / 9)),
            fontWeight: 700,
            textColor: watermark.color,
            textAlign: 'center',
        },
    }];
}

function firstString(...values) {
    for (const value of values) {
        if (typeof value === 'string' && value.trim()) {
            return value.trim();
        }
    }

    return '';
}

function clampNumber(value, min, max, fallback) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return fallback;
    }

    return Math.max(min, Math.min(max, number));
}
