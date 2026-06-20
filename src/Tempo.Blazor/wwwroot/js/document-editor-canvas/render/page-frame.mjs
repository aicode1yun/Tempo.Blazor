import { CANVAS_RENDER_LAYERS } from './layers.mjs';
import { buildWatermarkCommands, resolvePageBackground } from './page-background.mjs';

export function buildPageFrameCommands(pageLayout, theme = {}, model = {}) {
    const page = pageLayout || {};
    const body = page.body || { x: 96, y: 96, width: Math.max(1, (page.width || 794) - 192), height: Math.max(1, (page.height || 1123) - 192) };
    const width = Math.max(1, Number(page.width) || 794);
    const height = Math.max(1, Number(page.height) || 1123);
    const background = resolvePageBackground(model, theme);
    const borderFrame = background.border.alignTo === 'margin'
        ? { x: body.x, y: body.y, width: body.width, height: body.height }
        : { x: 0, y: 0, width, height };
    const borderMargin = background.border.margin;

    const commands = [
        {
            id: `page-${page.index || 0}-fill`,
            type: 'pageFill',
            layer: CANVAS_RENDER_LAYERS.pageBackground,
            pageIndex: Number(page.index) || 0,
            x: 0,
            y: 0,
            width,
            height,
            fill: background.pageFill || readThemeValue(theme, 'pageBackgroundPaint', '#ffffff'),
        },
        ...buildWatermarkCommands(page, background),
        {
            id: `page-${page.index || 0}-body`,
            type: 'bodyArea',
            layer: CANVAS_RENDER_LAYERS.pageBackground,
            pageIndex: Number(page.index) || 0,
            x: body.x,
            y: body.y,
            width: body.width,
            height: body.height,
            fill: readThemeValue(theme, 'bodyAreaPaint', 'rgba(248, 250, 252, 0.28)'),
        },
        {
            id: `page-${page.index || 0}-border`,
            type: 'pageBorder',
            layer: CANVAS_RENDER_LAYERS.pageBackground,
            pageIndex: Number(page.index) || 0,
            x: borderFrame.x + borderMargin + 0.5,
            y: borderFrame.y + borderMargin + 0.5,
            width: Math.max(1, borderFrame.width - borderMargin * 2 - 1),
            height: Math.max(1, borderFrame.height - borderMargin * 2 - 1),
            stroke: background.border.color,
            lineWidth: background.border.enabled ? background.border.width : 1,
            dash: background.border.enabled ? background.border.dash : [],
        },
        {
            id: `page-${page.index || 0}-margin`,
            type: 'marginGuide',
            layer: CANVAS_RENDER_LAYERS.pageBackground,
            pageIndex: Number(page.index) || 0,
            x: body.x + 0.5,
            y: body.y + 0.5,
            width: body.width,
            height: body.height,
            stroke: readThemeValue(theme, 'marginGuidePaint', '#e2e8f0'),
            lineWidth: 1,
            dash: [6, 5],
        },
    ];

    if (page.columnSeparatorLine === true && Array.isArray(page.columns) && page.columns.length > 1) {
        for (let index = 0; index < page.columns.length - 1; index += 1) {
            const column = page.columns[index];
            const next = page.columns[index + 1];
            const x = column.x + column.width + (next.x - (column.x + column.width)) / 2;
            commands.push({
                id: `page-${page.index || 0}-column-separator-${index}`,
                type: 'columnSeparator',
                layer: CANVAS_RENDER_LAYERS.pageBackground,
                pageIndex: Number(page.index) || 0,
                x,
                y: body.y,
                width: 0,
                height: body.height,
                stroke: readThemeValue(theme, 'columnSeparatorPaint', '#cbd5e1'),
                lineWidth: 1,
            });
        }
    }

    return commands;
}

function readThemeValue(theme, key, value) {
    return theme && theme[key] ? theme[key] : value;
}
