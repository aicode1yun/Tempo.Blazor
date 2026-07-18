import { decorationsFromMarks, normalizeLayoutSegmentStyle } from '../../document-editor/layout/segment-style.mjs';
import { uppercasePreservingLength } from '../../document-editor/core/text-transform.mjs';
import { resolveBlockStyleFormatting } from '../styles/style-resolver.mjs';
import { mathToAccessibleText, normalizeMathRun } from '../math/math-model.mjs';
import { contentControlDisplayText } from '../controls/sdt-model.mjs';

export const POINTS_TO_CSS_PIXELS = 96 / 72;
export const DEFAULT_BODY_FONT_SIZE_PT = 11;
export const DEFAULT_BODY_FONT_FAMILY = 'Aptos, Arial, sans-serif';

export function createCanvasRunText(run) {
    const type = String(run?.type || 'text');
    if (type === 'field') {
        return String(run?.field?.displayText || run?.field?.fallbackText || '');
    }

    if (type === 'token') {
        return String(run?.token?.displayName || run?.token?.fallbackText || run?.text || '');
    }

    if (type === 'noteReference') {
        return String(run?.noteReference?.displayMarker || '');
    }

    if (type === 'math') {
        const math = normalizeMathRun(run);
        return math.altText || mathToAccessibleText(math) || '□';
    }

    if (type === 'contentControl') {
        return contentControlDisplayText(run?.contentControl?.control || run?.contentControl || run);
    }

    return String(run?.text || '');
}

export function createCanvasRunDisplayText(run) {
    const text = createCanvasRunText(run);
    const marks = Array.isArray(run?.marks) ? run.marks : [];
    if (marks.some(mark => normalizeMarkType(mark?.type) === 'allcaps' || normalizeMarkType(mark?.type) === 'smallcaps')) {
        return uppercasePreservingLength(text);
    }

    return text;
}

export function createCanvasRunStyle(model, block, run) {
    const theme = model?.theme || {};
    const marks = Array.isArray(run?.marks) ? run.marks : [];
    const blockType = String(block?.type || block?.content?.type || '').toLowerCase();
    const resolved = resolveBlockStyleFormatting(model, block);
    const characterFormat = resolved.characterFormat || {};
    const headingLevel = blockType === 'heading'
        ? Math.max(1, Number(block?.content?.headingLevel || block?.content?.outlineLevel || 1) || 1)
        : null;
    const baseFontSize = pointsToCssPixels(theme.bodyFontSize || theme.BodyFontSize || DEFAULT_BODY_FONT_SIZE_PT);
    const fontSizePt = readNumber(characterFormat, 'fontSize', null);
    const fontSize = fontSizePt
        ? pointsToCssPixels(fontSizePt)
        : headingLevel ? baseFontSize * headingScale(headingLevel) : baseFontSize;
    const base = {
        fontFamily: readString(characterFormat, 'fontFamily', null) || theme.bodyFontFamily || theme.BodyFontFamily || DEFAULT_BODY_FONT_FAMILY,
        fontSize,
        fontWeight: readString(characterFormat, 'fontWeight', null) || (headingLevel ? '700' : '400'),
        fontStyle: readString(characterFormat, 'fontStyle', null) || (blockType === 'quote' ? 'italic' : 'normal'),
        color: readString(characterFormat, 'color', null) || theme.bodyTextColor || theme.BodyTextColor || theme.textColor || theme.TextColor || '#111827',
        backgroundColor: readString(characterFormat, 'backgroundColor', null),
        baselineShift: 0,
        characterScale: 1,
        fontVariantCaps: 'normal',
        kerning: true,
        letterSpacing: 0,
    };

    let verticalScript = '';
    for (const mark of marks) {
        const type = normalizeMarkType(mark?.type);
        if (type === 'bold') {
            base.fontWeight = '700';
        } else if (type === 'italic') {
            base.fontStyle = 'italic';
        } else if (type === 'textcolor' && mark.value) {
            base.color = String(mark.value);
        } else if (type === 'highlight' && mark.value) {
            base.backgroundColor = String(mark.value);
        } else if (type === 'redaction') {
            // Redaction bar: black background + black glyphs hides the content on screen; exports
            // additionally DESTROY the text (DocumentRedactionService / snapshot redactedRunIds).
            base.backgroundColor = '#000000';
            base.color = '#000000';
        } else if (type === 'fontfamily' && mark.value) {
            base.fontFamily = String(mark.value);
        } else if (type === 'fontsize' && mark.value) {
            base.fontSize = pointsToCssPixels(Number(mark.value) || DEFAULT_BODY_FONT_SIZE_PT);
        } else if (type === 'superscript') {
            verticalScript = 'superscript';
        } else if (type === 'subscript') {
            verticalScript = 'subscript';
        } else if (type === 'smallcaps') {
            base.fontVariantCaps = 'small-caps';
        } else if (type === 'allcaps') {
            base.textTransform = 'uppercase';
        } else if (type === 'characterspacing') {
            base.letterSpacing = readMarkNumber(mark, 0, -12, 36);
        } else if (type === 'characterscale') {
            base.characterScale = readMarkNumber(mark, 100, 33, 300) / 100;
        } else if (type === 'kerning') {
            base.kerning = String(mark.value || '').toLowerCase() !== 'false';
        }
    }

    if (verticalScript === 'superscript') {
        const originalSize = base.fontSize;
        base.fontSize = originalSize * 0.65;
        base.baselineShift = -originalSize * 0.34;
    } else if (verticalScript === 'subscript') {
        const originalSize = base.fontSize;
        base.fontSize = originalSize * 0.65;
        base.baselineShift = originalSize * 0.22;
    }

    const style = normalizeLayoutSegmentStyle(base);
    return {
        ...style,
        decorations: decorationsFromMarks(marks),
    };
}

export function orderedCanvasBlocks(model) {
    const body = model?.body || model?.Body || {};
    const blocks = Array.isArray(body.blocks || body.Blocks) && (body.blocks || body.Blocks).length > 0
        ? (body.blocks || body.Blocks)
        : Array.isArray(model?.sections || model?.Sections)
            ? (model.sections || model.Sections).flatMap(section => Array.isArray(section.blocks || section.Blocks) ? (section.blocks || section.Blocks) : [])
            : [];

    return flattenContentControlBlocks(blocks)
        .slice()
        .sort((left, right) => {
            const order = (Number(left?.order ?? left?.Order) || 0) - (Number(right?.order ?? right?.Order) || 0);
            const leftId = left?.id ?? left?.Id ?? '';
            const rightId = right?.id ?? right?.Id ?? '';
            return order !== 0 ? order : String(leftId).localeCompare(String(rightId));
        });
}

function flattenContentControlBlocks(blocks) {
    const result = [];
    for (const block of Array.isArray(blocks) ? blocks : []) {
        if (!block) {
            continue;
        }

        const type = String(block.type || block.content?.type || '').replace(/[\s_-]/g, '').toLowerCase();
        const nested = block.content?.contentControl?.blocks || block.contentControl?.blocks || [];
        if (type === 'contentcontrol' && Array.isArray(nested) && nested.length > 0) {
            result.push(...flattenContentControlBlocks(nested));
            continue;
        }

        result.push(block);
    }

    return result;
}

export function paragraphIndent(block, key) {
    const properties = block?.paragraphProperties || {};
    return pointsToCssPixels(properties[key] ?? properties[pascalCase(key)] ?? 0);
}

export function pointsToCssPixels(value) {
    return (Number(value) || 0) * POINTS_TO_CSS_PIXELS;
}

export function normalizeMarkType(type) {
    return String(type || '').replace(/[\s_-]/g, '').toLowerCase();
}

export function normalizeCanvasAlignment(value) {
    if (typeof value === 'number') {
        return ['left', 'center', 'right', 'justify'][Math.max(0, Math.min(3, Math.trunc(value)))] || 'left';
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === 'center' || normalized === 'middle') {
        return 'center';
    }

    if (normalized === 'right' || normalized === 'end') {
        return 'right';
    }

    if (normalized === 'justify' || normalized === 'justified' || normalized === 'block') {
        return 'justify';
    }

    return 'left';
}

function pascalCase(value) {
    return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}

function readNumber(source, key, fallback) {
    const value = source?.[key] ?? source?.[pascalCase(key)];
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? number : fallback;
}

function readString(source, key, fallback) {
    const value = source?.[key] ?? source?.[pascalCase(key)];
    const text = value == null ? '' : String(value).trim();
    return text.length > 0 ? text : fallback;
}

function readMarkNumber(mark, fallback, min, max) {
    const parsed = Number(String(mark?.value ?? '').replace(/(pt|px|%)$/iu, '').trim());
    if (!Number.isFinite(parsed)) {
        return fallback;
    }

    return Math.max(min, Math.min(max, parsed));
}

function headingScale(level) {
    if (level <= 1) {
        return 1.9;
    }

    if (level === 2) {
        return 1.55;
    }

    if (level === 3) {
        return 1.3;
    }

    return 1.12;
}
