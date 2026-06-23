import { createFontMetricsService } from '../../document-editor/layout/font-metrics.mjs';
import { createParagraphLayoutEngineFactory } from '../../document-editor/layout/paragraph-engine.mjs';
import { normalizeParagraphAlignment } from '../../document-editor/layout/paragraph-alignment.mjs';

const DEFAULT_TEXT_STYLE = Object.freeze({
    fontFamily: 'Aptos, Arial, sans-serif',
    fontSize: 14,
    color: '#0f172a',
    fontWeight: '400',
    fontStyle: 'normal',
});

export function layoutDrawingTextLines(textBody, contentWidth, metrics = null) {
    const body = normalizeTextboxBody(textBody);
    if (!body.paragraphs.length) {
        return [];
    }

    const measurement = ensureMetrics(metrics);
    const engine = createParagraphLayoutEngineFactory({
        findBlock(_, blockId) {
            return body.paragraphs.find(paragraph => paragraph.id === blockId) || null;
        },
    })(measurement, {
        minReadableWidth: 12,
        lineGap: 0,
    });

    const width = Math.max(1, Number(contentWidth || 0) || 1);
    const layoutWidth = body.wrapText === false ? Math.max(width, 100_000) : width;
    const lines = [];
    let cursorY = 0;
    let textOffset = 0;
    for (let paragraphIndex = 0; paragraphIndex < body.paragraphs.length; paragraphIndex += 1) {
        const paragraph = body.paragraphs[paragraphIndex];
        const layout = engine.layoutParagraph(paragraph, {
            x: 0,
            y: cursorY,
            width: layoutWidth,
            minReadableWidth: 12,
            lineGap: 0,
        });
        const paragraphLines = materializeTextboxLines(layout, width, paragraph);
        let lineOffset = 0;
        for (let lineIndex = 0; lineIndex < paragraphLines.length; lineIndex += 1) {
            const line = paragraphLines[lineIndex];
            const lineText = String(line.text || '');
            lines.push({
                ...line,
                paragraphIndex,
                lineIndex,
                textStart: textOffset + lineOffset,
                textEnd: textOffset + lineOffset + lineText.length,
            });
            lineOffset += lineText.length;
        }

        cursorY = paragraphLines.length > 0
            ? Math.max(...paragraphLines.map(line => line.y + line.lineHeight)) + paragraph.spacingAfter
            : cursorY + paragraph.defaultLineHeight + paragraph.spacingAfter;
        const paragraphText = paragraph.content?.runs?.map(run => String(run?.text || '')).join('') || '';
        textOffset += paragraphText.length + (paragraphIndex < body.paragraphs.length - 1 ? 1 : 0);
    }

    return lines.length > 0 ? lines : [{
        text: '',
        style: { ...DEFAULT_TEXT_STYLE },
        fontSize: DEFAULT_TEXT_STYLE.fontSize,
        lineHeight: DEFAULT_TEXT_STYLE.fontSize * 1.22,
        alignment: 'left',
        x: 0,
        y: 0,
        width,
    }];
}

export function drawingTextLayoutHeight(lines) {
    if (!Array.isArray(lines) || lines.length === 0) {
        return 0;
    }

    return Math.max(...lines.map(line => (Number(line.y || 0) || 0) + Math.max(1, Number(line.lineHeight || 0) || 1)));
}

function materializeTextboxLines(layout, contentWidth, paragraph) {
    const layoutLines = Array.isArray(layout?.lines) ? layout.lines : [];
    if (!layoutLines.length) {
        return [{
            text: '',
            style: paragraph.defaultStyle,
            fontSize: paragraph.defaultFontSize,
            lineHeight: paragraph.defaultLineHeight,
            alignment: paragraph.alignment,
            x: 0,
            y: 0,
            width: contentWidth,
        }];
    }

    return layoutLines.map(line => {
        const segments = Array.isArray(line.segments) ? line.segments : [];
        const firstSegment = segments.find(segment => segment.type !== 'space') || segments[0] || {};
        const style = normalizeTextboxStyle(firstSegment.style || paragraph.defaultStyle);
        const rect = line.rect || {};
        return {
            text: segments.map(segment => String(segment.text || '')).join('').trimEnd(),
            style,
            fontSize: Math.max(8, Number(style.fontSize || paragraph.defaultFontSize || 14) || 14),
            lineHeight: Math.max(1, Number(rect.height || paragraph.defaultLineHeight || 17.08) || 17.08),
            alignment: normalizeParagraphAlignment(line.alignment ?? paragraph.alignment),
            x: Math.max(0, Number(rect.x || 0) || 0),
            y: Math.max(0, Number(rect.y || 0) || 0),
            width: contentWidth,
            segments: segments.map(segment => ({
                text: String(segment.text || ''),
                x: Math.max(0, Number(segment.rect?.x || 0) || 0),
                y: Math.max(0, Number(segment.rect?.y || 0) || 0),
                width: Math.max(0, Number(segment.rect?.width || 0) || 0),
                height: Math.max(1, Number(segment.rect?.height || rect.height || 1) || 1),
                style: normalizeTextboxStyle(segment.style || style),
            })),
        };
    });
}

function normalizeTextboxBody(textBody) {
    const source = textBody && typeof textBody === 'object' ? textBody : {};
    const paragraphs = Array.isArray(source.paragraphs) ? source.paragraphs : [];
    return {
        wrapText: source.wrapText !== false,
        paragraphs: paragraphs.map((paragraph, index) => {
            const style = normalizeTextboxStyle(paragraph?.style || {});
            const fontSize = Math.max(8, Number(style.fontSize || 14) || 14);
            const lineHeight = Math.max(fontSize * 1.22, Number(style.lineHeight || 0) || 0);
            const id = String(paragraph?.id || `textbox-paragraph-${index}`);
            return {
                id,
                type: 'paragraph',
                alignment: normalizeParagraphAlignment(paragraph?.alignment),
                spacingAfter: Math.max(0, Number(paragraph?.spacingAfter || 0) || 0),
                defaultFontSize: fontSize,
                defaultLineHeight: lineHeight,
                defaultStyle: style,
                style: {},
                content: {
                    alignment: normalizeParagraphAlignment(paragraph?.alignment),
                    runs: [{
                        id: `${id}-run-0`,
                        type: 'text',
                        kind: 'text',
                        text: String(paragraph?.text || ''),
                        marks: [],
                        style,
                    }],
                },
            };
        }),
    };
}

function normalizeTextboxStyle(style) {
    const source = style && typeof style === 'object' ? style : {};
    const fontWeight = source.bold === true
        ? '700'
        : String(source.fontWeight || DEFAULT_TEXT_STYLE.fontWeight);
    const fontStyle = source.italic === true
        ? 'italic'
        : String(source.fontStyle || DEFAULT_TEXT_STYLE.fontStyle);
    return {
        fontFamily: String(source.fontFamily || DEFAULT_TEXT_STYLE.fontFamily),
        fontSize: Math.max(8, Number(source.fontSize || DEFAULT_TEXT_STYLE.fontSize) || DEFAULT_TEXT_STYLE.fontSize),
        color: String(source.color || DEFAULT_TEXT_STYLE.color),
        fontWeight,
        fontStyle,
        bold: fontWeight === '700',
        italic: fontStyle === 'italic',
        lineHeight: Number(source.lineHeight || 0) || null,
    };
}

function ensureMetrics(metrics) {
    if (metrics && typeof metrics.measureText === 'function') {
        if (typeof metrics.getStats === 'function') {
            return metrics;
        }

        return Object.assign(Object.create(metrics), {
            getStats() {
                return {};
            },
        });
    }

    return createFontMetricsService();
}
