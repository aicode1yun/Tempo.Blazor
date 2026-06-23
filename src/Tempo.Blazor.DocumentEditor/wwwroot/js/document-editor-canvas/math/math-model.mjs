import { parseMathMLToContent } from './mathml-adapter.mjs';

export function normalizeMathRun(run) {
    const math = run?.math || run?.Math || {};
    const mathML = math.mathML ?? math.MathML ?? null;
    const content = math.content ?? math.Content;
    return {
        mathId: String(math.mathId ?? math.MathId ?? run?.id ?? ''),
        displayMode: normalizeDisplayMode(math.displayMode ?? math.DisplayMode),
        content: content == null && mathML ? parseMathMLToContent(mathML) : normalizeMathContent(content),
        altText: String(math.altText ?? math.AltText ?? ''),
        mathML,
        ommlXml: math.ommlXml ?? math.OmmlXml ?? null,
        metadata: math.metadata ?? math.Metadata ?? {},
    };
}

export function normalizeMathContent(content) {
    const elements = Array.isArray(content?.elements ?? content?.Elements) ? (content.elements ?? content.Elements) : [];
    return {
        elements: elements.map(normalizeMathElement),
    };
}

export function normalizeMathElement(element) {
    const source = element && typeof element === 'object' ? element : {};
    return {
        type: normalizeElementType(source.type ?? source.Type),
        text: source.text ?? source.Text ?? null,
        style: String(source.style ?? source.Style ?? 'italic'),
        fractionType: String(source.fractionType ?? source.FractionType ?? 'bar'),
        open: source.open ?? source.Open ?? null,
        close: source.close ?? source.Close ?? null,
        separator: source.separator ?? source.Separator ?? null,
        operator: source.operator ?? source.Operator ?? null,
        limitsAboveBelow: (source.limitsAboveBelow ?? source.LimitsAboveBelow ?? true) !== false,
        accent: source.accent ?? source.Accent ?? null,
        position: source.position ?? source.Position ?? null,
        base: optionalContent(source.base ?? source.Base),
        numerator: optionalContent(source.numerator ?? source.Numerator),
        denominator: optionalContent(source.denominator ?? source.Denominator),
        radicand: optionalContent(source.radicand ?? source.Radicand),
        degree: optionalContent(source.degree ?? source.Degree),
        superscript: optionalContent(source.superscript ?? source.Superscript),
        subscript: optionalContent(source.subscript ?? source.Subscript),
        lowerLimit: optionalContent(source.lowerLimit ?? source.LowerLimit),
        upperLimit: optionalContent(source.upperLimit ?? source.UpperLimit),
        functionName: optionalContent(source.functionName ?? source.FunctionName),
        content: optionalContent(source.content ?? source.Content),
        rows: normalizeRows(source.rows ?? source.Rows),
        metadata: source.metadata ?? source.Metadata ?? {},
    };
}

export function mathToAccessibleText(mathOrContent) {
    const content = mathOrContent?.content?.elements ? mathOrContent.content : mathOrContent;
    return contentText(normalizeMathContent(content));
}

export function createMathContentFromLinear(input) {
    const text = String(input ?? '').trim();
    if (!text) {
        return { elements: [{ type: 'run', text: '□', style: 'normal' }] };
    }

    const fraction = splitTopLevel(text, '/');
    if (fraction) {
        return {
            elements: [{
                type: 'fraction',
                numerator: createMathContentFromLinear(fraction.left),
                denominator: createMathContentFromLinear(fraction.right),
                fractionType: 'bar',
            }],
        };
    }

    const sup = splitTopLevel(text, '^');
    if (sup) {
        return {
            elements: [{
                type: 'sup',
                base: createMathContentFromLinear(sup.left),
                superscript: createMathContentFromLinear(sup.right),
            }],
        };
    }

    const sub = splitTopLevel(text, '_');
    if (sub) {
        return {
            elements: [{
                type: 'sub',
                base: createMathContentFromLinear(sub.left),
                subscript: createMathContentFromLinear(sub.right),
            }],
        };
    }

    if (/^sqrt\((.*)\)$/i.test(text)) {
        return {
            elements: [{
                type: 'radical',
                radicand: createMathContentFromLinear(text.match(/^sqrt\((.*)\)$/i)[1]),
            }],
        };
    }

    if (text === '\\sum' || text === 'sum') {
        return {
            elements: [{
                type: 'nary',
                operator: '∑',
                lowerLimit: { elements: [{ type: 'run', text: 'i=1', style: 'italic' }] },
                upperLimit: { elements: [{ type: 'run', text: 'n', style: 'italic' }] },
                base: { elements: [{ type: 'run', text: 'i', style: 'italic' }] },
            }],
        };
    }

    if (text === '\\prod' || text === 'prod') {
        return {
            elements: [{
                type: 'nary',
                operator: '∏',
                lowerLimit: { elements: [{ type: 'run', text: 'i=1', style: 'italic' }] },
                upperLimit: { elements: [{ type: 'run', text: 'n', style: 'italic' }] },
                base: { elements: [{ type: 'run', text: 'i', style: 'italic' }] },
            }],
        };
    }

    if (text === '\\lim' || text === 'lim') {
        return {
            elements: [{
                type: 'limit',
                base: { elements: [{ type: 'run', text: 'lim', style: 'normal' }] },
                lowerLimit: { elements: [{ type: 'run', text: 'x→0', style: 'italic' }] },
                content: { elements: [{ type: 'run', text: 'f(x)', style: 'italic' }] },
            }],
        };
    }

    return {
        elements: expandSymbols(text).split(/(\s+|[+\-=()])/).filter(Boolean).map(part => ({
            type: 'run',
            text: part,
            style: /^[a-zA-Zα-ωΑ-Ω]+$/.test(part) ? 'italic' : 'normal',
        })),
    };
}

function contentText(content) {
    return (content?.elements || []).map(elementText).join('');
}

function elementText(element) {
    switch (element.type) {
        case 'fraction':
            return `(${contentText(element.numerator)})/(${contentText(element.denominator)})`;
        case 'radical':
            return `sqrt(${contentText(element.radicand)})`;
        case 'sup':
            return `${contentText(element.base)}^${contentText(element.superscript)}`;
        case 'sub':
            return `${contentText(element.base)}_${contentText(element.subscript)}`;
        case 'subSup':
            return `${contentText(element.base)}_${contentText(element.subscript)}^${contentText(element.superscript)}`;
        case 'preSubSup':
            return `_${contentText(element.subscript)}^${contentText(element.superscript)}${contentText(element.base)}`;
        case 'nary':
            return `${element.operator || '∑'}_${contentText(element.lowerLimit)}^${contentText(element.upperLimit)} ${contentText(element.base)}`;
        case 'matrix':
            return `[${element.rows.map(row => row.cells.map(contentText).join(',')).join(';')}]`;
        case 'delimiter':
            return `${element.open || '('}${contentText(element.content)}${element.close || ')'}`;
        case 'function':
            return `${contentText(element.functionName)}(${contentText(element.base || element.content)})`;
        case 'accent':
            return `${contentText(element.base || element.content)} ${element.accent || element.text || 'accent'}`;
        case 'bar':
            return `${String(element.position || 'over').toLowerCase() === 'under' ? 'underbar' : 'overbar'}(${contentText(element.base || element.content)})`;
        case 'groupChar':
            return `${String(element.position || 'over').toLowerCase() === 'under' ? 'undergroup' : 'overgroup'}(${contentText(element.base || element.content)})`;
        case 'limit':
            return `${contentText(element.base)}_${contentText(element.lowerLimit)}^${contentText(element.upperLimit)} ${contentText(element.content)}`;
        case 'box':
        case 'borderBox':
            return contentText(element.content || element.base);
        default:
            return String(element.text || '');
    }
}

function normalizeDisplayMode(value) {
    if (typeof value === 'number') {
        return value === 1 ? 'display' : 'inline';
    }

    return String(value || '').replace(/[\s_-]/g, '').toLowerCase() === 'display' ? 'display' : 'inline';
}

function normalizeElementType(value) {
    const normalized = String(value || 'run').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'text') return 'run';
    if (normalized === 'subsup') return 'subSup';
    if (normalized === 'presubsup') return 'preSubSup';
    if (normalized === 'groupchar') return 'groupChar';
    if (normalized === 'borderbox') return 'borderBox';
    if (['run', 'fraction', 'radical', 'sup', 'sub', 'subSup', 'preSubSup', 'nary', 'delimiter', 'function', 'accent', 'bar', 'groupChar', 'limit', 'matrix', 'box', 'borderBox'].includes(normalized)) {
        return normalized;
    }

    return 'run';
}

function optionalContent(value) {
    return value == null ? null : normalizeMathContent(value);
}

function normalizeRows(rows) {
    return (Array.isArray(rows) ? rows : []).map(row => ({
        cells: (Array.isArray(row?.cells ?? row?.Cells) ? (row.cells ?? row.Cells) : []).map(normalizeMathContent),
    }));
}

function splitTopLevel(text, operator) {
    let depth = 0;
    for (let index = 0; index < text.length; index += 1) {
        const char = text[index];
        if (char === '(') depth += 1;
        if (char === ')') depth -= 1;
        if (char === operator && depth === 0 && index > 0 && index < text.length - 1) {
            return { left: text.slice(0, index), right: text.slice(index + 1) };
        }
    }

    return null;
}

function expandSymbols(text) {
    return String(text)
        .replaceAll('\\alpha', 'α')
        .replaceAll('\\beta', 'β')
        .replaceAll('\\gamma', 'γ')
        .replaceAll('\\Delta', 'Δ')
        .replaceAll('\\delta', 'δ')
        .replaceAll('\\theta', 'θ')
        .replaceAll('\\lambda', 'λ')
        .replaceAll('\\mu', 'μ')
        .replaceAll('\\sigma', 'σ')
        .replaceAll('\\omega', 'ω')
        .replaceAll('\\pi', 'π')
        .replaceAll('\\sum', '∑')
        .replaceAll('\\prod', '∏')
        .replaceAll('\\int', '∫')
        .replaceAll('\\infty', '∞')
        .replaceAll('\\leq', '≤')
        .replaceAll('\\geq', '≥')
        .replaceAll('\\neq', '≠')
        .replaceAll('\\rightarrow', '→')
        .replaceAll('\\leftarrow', '←');
}
