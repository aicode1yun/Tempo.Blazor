import { normalizeMathContent, normalizeMathRun } from './math-model.mjs';

const SCALE = Object.freeze([1, 0.72, 0.56, 0.48]);
const DEFAULT_FONT = 'Cambria Math, STIX Two Math, Times New Roman, serif';

export function layoutMathRun(math, options = {}) {
    const normalized = normalizeMathRun({ id: math?.mathId || math?.MathId || options.id || '', math });
    const style = options.style || {};
    const fontSize = Math.max(10, Number(style.fontSize || options.fontSize || 18) || 18);
    const metrics = options.metrics || null;
    const content = normalizeMathContent(normalized.content);
    const box = layoutContent(content, {
        metrics,
        fontSize,
        level: 0,
        displayMode: normalized.displayMode || options.displayMode || 'inline',
        fontFamily: style.fontFamily || DEFAULT_FONT,
    });
    return {
        ...box,
        mathId: normalized.mathId,
        displayMode: normalized.displayMode,
        content,
    };
}

export function layoutContent(content, context) {
    const elements = normalizeMathContent(content).elements;
    const boxes = elements.map(element => layoutElement(element, context));
    const width = boxes.reduce((sum, box) => sum + box.width, 0);
    const ascent = Math.max(...boxes.map(box => box.ascent), context.fontSize * scaled(context.level) * 0.78);
    const descent = Math.max(...boxes.map(box => box.descent), context.fontSize * scaled(context.level) * 0.24);
    let cursor = 0;
    boxes.forEach(box => {
        box.x = cursor;
        box.y = ascent - box.ascent;
        cursor += box.width;
    });
    return {
        type: 'content',
        width,
        height: ascent + descent,
        ascent,
        descent,
        children: boxes,
    };
}

export function layoutElement(element, context) {
    switch (element.type) {
        case 'fraction':
            return layoutFraction(element, context);
        case 'radical':
            return layoutRadical(element, context);
        case 'sup':
            return layoutSupSub(element, context, 'sup');
        case 'sub':
            return layoutSupSub(element, context, 'sub');
        case 'subSup':
            return layoutSupSub(element, context, 'subSup');
        case 'preSubSup':
            return layoutPreSubSup(element, context);
        case 'nary':
            return layoutNary(element, context);
        case 'matrix':
            return layoutMatrix(element, context);
        case 'delimiter':
            return layoutDelimiter(element, context);
        case 'function':
            return layoutFunction(element, context);
        case 'accent':
            return layoutAccent(element, context);
        case 'bar':
            return layoutBar(element, context);
        case 'groupChar':
            return layoutGroupChar(element, context);
        case 'limit':
            return layoutLimit(element, context);
        case 'box':
        case 'borderBox':
            return layoutBox(element, context);
        default:
            return layoutRun(element, context);
    }
}

function layoutRun(element, context) {
    const text = String(element.text ?? element.Text ?? '');
    const fontSize = context.fontSize * scaled(context.level);
    const style = {
        fontFamily: context.fontFamily,
        fontSize,
        fontStyle: element.style === 'normal' ? 'normal' : 'italic',
        fontWeight: element.style === 'bold' || element.style === 'boldItalic' ? '700' : '400',
    };
    const measured = measureMathText(context.metrics, text, style);
    const width = Math.max(text ? fontSize * 0.28 : fontSize * 0.6, Number(measured.width) || 0);
    return {
        type: 'run',
        text,
        style,
        width,
        height: fontSize * 1.18,
        ascent: fontSize * 0.78,
        descent: fontSize * 0.24,
        children: [],
    };
}

function layoutFraction(element, context) {
    const childContext = nextContext(context);
    const numerator = layoutContent(element.numerator || { elements: [] }, childContext);
    const denominator = layoutContent(element.denominator || { elements: [] }, childContext);
    const pad = context.fontSize * 0.18;
    const gap = context.fontSize * 0.16;
    const rule = Math.max(1, context.fontSize * 0.055);
    const width = Math.max(numerator.width, denominator.width) + pad * 2;
    const ascent = numerator.height + gap + rule;
    const descent = denominator.height + gap;
    numerator.x = (width - numerator.width) / 2;
    numerator.y = 0;
    denominator.x = (width - denominator.width) / 2;
    denominator.y = ascent + gap;
    return {
        type: 'fraction',
        width,
        height: ascent + descent,
        ascent,
        descent,
        ruleY: ascent,
        ruleWidth: width,
        children: [numerator, denominator],
    };
}

function layoutRadical(element, context) {
    const radicand = layoutContent(element.radicand || { elements: [] }, context);
    const degree = element.degree ? layoutContent(element.degree, nextContext(context, 2)) : null;
    const symbolWidth = context.fontSize * 0.62;
    const clearance = Math.max(2, context.fontSize * 0.12);
    const degreeWidth = degree ? degree.width * 0.75 : 0;
    const width = degreeWidth + symbolWidth + radicand.width + context.fontSize * 0.16;
    const ascent = radicand.ascent + clearance + context.fontSize * 0.18;
    const descent = radicand.descent;
    radicand.x = degreeWidth + symbolWidth;
    radicand.y = ascent - radicand.ascent;
    if (degree) {
        degree.x = 0;
        degree.y = 0;
    }
    return {
        type: 'radical',
        width,
        height: ascent + descent,
        ascent,
        descent,
        symbolWidth,
        degreeWidth,
        children: degree ? [degree, radicand] : [radicand],
    };
}

function layoutSupSub(element, context, kind) {
    const base = layoutContent(element.base || { elements: [] }, context);
    const scriptContext = nextContext(context);
    const sup = kind !== 'sub' && element.superscript ? layoutContent(element.superscript, scriptContext) : null;
    const sub = kind !== 'sup' && element.subscript ? layoutContent(element.subscript, scriptContext) : null;
    const gap = context.fontSize * 0.08;
    const scriptWidth = Math.max(sup?.width || 0, sub?.width || 0);
    const supShift = sup ? Math.max(base.ascent * 0.58, context.fontSize * 0.42) : 0;
    const subShift = sub ? Math.max(base.descent * 0.75, context.fontSize * 0.22) : 0;
    const ascent = Math.max(base.ascent, (sup?.height || 0) + supShift);
    const descent = Math.max(base.descent, (sub?.height || 0) + subShift);
    base.x = 0;
    base.y = ascent - base.ascent;
    const children = [base];
    if (sup) {
        sup.x = base.width + gap;
        sup.y = ascent - supShift - sup.height;
        children.push(sup);
    }
    if (sub) {
        sub.x = base.width + gap;
        sub.y = ascent + subShift - sub.ascent;
        children.push(sub);
    }

    return {
        type: kind,
        width: base.width + gap + scriptWidth,
        height: ascent + descent,
        ascent,
        descent,
        children,
    };
}

function layoutPreSubSup(element, context) {
    const base = layoutContent(element.base || { elements: [] }, context);
    const scriptContext = nextContext(context);
    const sup = element.superscript ? layoutContent(element.superscript, scriptContext) : null;
    const sub = element.subscript ? layoutContent(element.subscript, scriptContext) : null;
    const gap = context.fontSize * 0.08;
    const scriptWidth = Math.max(sup?.width || 0, sub?.width || 0);
    const supShift = sup ? Math.max(base.ascent * 0.58, context.fontSize * 0.42) : 0;
    const subShift = sub ? Math.max(base.descent * 0.75, context.fontSize * 0.22) : 0;
    const ascent = Math.max(base.ascent, (sup?.height || 0) + supShift);
    const descent = Math.max(base.descent, (sub?.height || 0) + subShift);
    const children = [];
    if (sup) {
        sup.x = Math.max(0, scriptWidth - sup.width);
        sup.y = ascent - supShift - sup.height;
        children.push(sup);
    }
    if (sub) {
        sub.x = Math.max(0, scriptWidth - sub.width);
        sub.y = ascent + subShift - sub.ascent;
        children.push(sub);
    }
    base.x = scriptWidth + gap;
    base.y = ascent - base.ascent;
    children.push(base);
    return {
        type: 'preSubSup',
        width: scriptWidth + gap + base.width,
        height: ascent + descent,
        ascent,
        descent,
        children,
    };
}

function layoutNary(element, context) {
    const operator = layoutRun({ type: 'run', text: element.operator || '∑', style: 'normal' }, { ...context, level: Math.max(0, context.level - 1) });
    const base = layoutContent(element.base || { elements: [] }, context);
    const lower = element.lowerLimit ? layoutContent(element.lowerLimit, nextContext(context)) : null;
    const upper = element.upperLimit ? layoutContent(element.upperLimit, nextContext(context)) : null;
    const gap = context.fontSize * 0.12;
    const limitsStacked = element.limitsAboveBelow !== false && String(context.displayMode).toLowerCase() === 'display';
    if (!limitsStacked) {
        operator.x = 0;
        operator.y = Math.max(0, base.ascent - operator.ascent);
        base.x = operator.width + gap;
        base.y = 0;
        return {
            type: 'nary',
            width: operator.width + gap + base.width,
            height: Math.max(operator.height, base.height),
            ascent: Math.max(operator.ascent, base.ascent),
            descent: Math.max(operator.descent, base.descent),
            children: [operator, base],
        };
    }

    const stackWidth = Math.max(operator.width, lower?.width || 0, upper?.width || 0);
    const ascent = (upper?.height || 0) + gap + operator.ascent;
    const descent = operator.descent + gap + (lower?.height || 0);
    const children = [];
    if (upper) {
        upper.x = (stackWidth - upper.width) / 2;
        upper.y = 0;
        children.push(upper);
    }
    operator.x = (stackWidth - operator.width) / 2;
    operator.y = (upper?.height || 0) + gap;
    children.push(operator);
    if (lower) {
        lower.x = (stackWidth - lower.width) / 2;
        lower.y = ascent + operator.descent + gap - lower.ascent;
        children.push(lower);
    }
    base.x = stackWidth + gap;
    base.y = ascent - base.ascent;
    children.push(base);
    return {
        type: 'nary',
        width: stackWidth + gap + base.width,
        height: ascent + descent,
        ascent,
        descent,
        children,
    };
}

function layoutMatrix(element, context) {
    const rows = element.rows || [];
    const cellBoxes = rows.map(row => row.cells.map(cell => layoutContent(cell, context)));
    const columnCount = Math.max(0, ...cellBoxes.map(row => row.length));
    const colWidths = Array.from({ length: columnCount }, (_, column) => Math.max(1, ...cellBoxes.map(row => row[column]?.width || 1)));
    const rowHeights = cellBoxes.map(row => Math.max(1, ...row.map(cell => cell.height)));
    const gapX = context.fontSize * 0.65;
    const gapY = context.fontSize * 0.28;
    const width = colWidths.reduce((sum, width) => sum + width, 0) + Math.max(0, columnCount - 1) * gapX + context.fontSize * 0.7;
    const contentHeight = rowHeights.reduce((sum, height) => sum + height, 0) + Math.max(0, rowHeights.length - 1) * gapY;
    const ascent = contentHeight / 2 + context.fontSize * 0.32;
    const descent = contentHeight - ascent;
    const children = [];
    let y = 0;
    cellBoxes.forEach((row, rowIndex) => {
        let x = context.fontSize * 0.35;
        row.forEach((cell, columnIndex) => {
            cell.x = x + (colWidths[columnIndex] - cell.width) / 2;
            cell.y = y + (rowHeights[rowIndex] - cell.height) / 2;
            children.push(cell);
            x += colWidths[columnIndex] + gapX;
        });
        y += rowHeights[rowIndex] + gapY;
    });
    return { type: 'matrix', width, height: contentHeight, ascent, descent, children };
}

function layoutDelimiter(element, context) {
    const content = layoutContent(element.content || { elements: [] }, context);
    const open = layoutRun({ type: 'run', text: element.open || '(', style: 'normal' }, context);
    const close = layoutRun({ type: 'run', text: element.close || ')', style: 'normal' }, context);
    open.x = 0;
    open.y = Math.max(0, content.ascent - open.ascent);
    content.x = open.width;
    content.y = 0;
    close.x = open.width + content.width;
    close.y = open.y;
    return {
        type: 'delimiter',
        width: open.width + content.width + close.width,
        height: content.height,
        ascent: content.ascent,
        descent: content.descent,
        children: [open, content, close],
    };
}

function layoutFunction(element, context) {
    const name = element.functionName
        ? layoutContent(element.functionName, context)
        : layoutRun({ type: 'run', text: element.text || 'f', style: 'normal' }, context);
    const base = layoutContent(element.base || element.content || { elements: [] }, context);
    const gap = context.fontSize * 0.16;
    name.x = 0;
    name.y = Math.max(0, base.ascent - name.ascent);
    base.x = name.width + gap;
    base.y = 0;
    return {
        type: 'function',
        width: name.width + gap + base.width,
        height: Math.max(name.height, base.height),
        ascent: Math.max(name.ascent, base.ascent),
        descent: Math.max(name.descent, base.descent),
        children: [name, base],
    };
}

function layoutAccent(element, context) {
    const base = layoutContent(element.base || element.content || { elements: [] }, context);
    const accent = layoutRun({ type: 'run', text: element.accent || element.text || '̂', style: 'normal' }, nextContext(context, 1));
    const gap = Math.max(1, context.fontSize * 0.05);
    const width = Math.max(base.width, accent.width);
    accent.x = (width - accent.width) / 2;
    accent.y = 0;
    base.x = (width - base.width) / 2;
    base.y = accent.height + gap;
    return {
        type: 'accent',
        width,
        height: base.height + accent.height + gap,
        ascent: base.ascent + accent.height + gap,
        descent: base.descent,
        children: [accent, base],
    };
}

function layoutBar(element, context) {
    const base = layoutContent(element.base || element.content || { elements: [] }, context);
    const gap = context.fontSize * 0.12;
    const rule = Math.max(1, context.fontSize * 0.05);
    const over = String(element.position || 'over').toLowerCase() !== 'under';
    base.x = 0;
    base.y = over ? gap + rule : 0;
    return {
        type: 'bar',
        width: base.width,
        height: base.height + gap + rule,
        ascent: base.ascent + (over ? gap + rule : 0),
        descent: base.descent + (over ? 0 : gap + rule),
        ruleY: over ? 0 : base.height + gap,
        ruleWidth: base.width,
        children: [base],
    };
}

function layoutGroupChar(element, context) {
    const base = layoutContent(element.base || element.content || { elements: [] }, context);
    const under = String(element.position || 'over').toLowerCase() === 'under';
    const glyph = layoutRun({ type: 'run', text: element.accent || element.text || (under ? '⏟' : '⏞'), style: 'normal' }, nextContext(context, 1));
    const gap = Math.max(1, context.fontSize * 0.08);
    const width = Math.max(base.width, glyph.width);
    base.x = (width - base.width) / 2;
    glyph.x = (width - glyph.width) / 2;
    if (under) {
        base.y = 0;
        glyph.y = base.height + gap;
    } else {
        glyph.y = 0;
        base.y = glyph.height + gap;
    }
    return {
        type: 'groupChar',
        width,
        height: base.height + glyph.height + gap,
        ascent: base.ascent + (under ? 0 : glyph.height + gap),
        descent: base.descent + (under ? glyph.height + gap : 0),
        children: under ? [base, glyph] : [glyph, base],
    };
}

function layoutLimit(element, context) {
    const base = layoutContent(element.base || { elements: [{ type: 'run', text: 'lim', style: 'normal' }] }, context);
    const lower = element.lowerLimit ? layoutContent(element.lowerLimit, nextContext(context)) : null;
    const upper = element.upperLimit ? layoutContent(element.upperLimit, nextContext(context)) : null;
    const expression = element.content ? layoutContent(element.content, context) : null;
    const gap = context.fontSize * 0.12;
    const stackWidth = Math.max(base.width, lower?.width || 0, upper?.width || 0);
    const ascent = (upper?.height || 0) + (upper ? gap : 0) + base.ascent;
    const descent = base.descent + (lower ? gap + lower.height : 0);
    const children = [];
    if (upper) {
        upper.x = (stackWidth - upper.width) / 2;
        upper.y = 0;
        children.push(upper);
    }
    base.x = (stackWidth - base.width) / 2;
    base.y = (upper?.height || 0) + (upper ? gap : 0);
    children.push(base);
    if (lower) {
        lower.x = (stackWidth - lower.width) / 2;
        lower.y = ascent + base.descent + gap - lower.ascent;
        children.push(lower);
    }
    if (expression) {
        expression.x = stackWidth + gap;
        expression.y = ascent - expression.ascent;
        children.push(expression);
    }
    return {
        type: 'limit',
        width: stackWidth + (expression ? gap + expression.width : 0),
        height: Math.max(ascent + descent, expression?.height || 0),
        ascent: Math.max(ascent, expression?.ascent || 0),
        descent: Math.max(descent, expression?.descent || 0),
        children,
    };
}

function layoutBox(element, context) {
    const content = layoutContent(element.content || element.base || { elements: [] }, context);
    const paddingX = context.fontSize * 0.18;
    const paddingY = context.fontSize * 0.12;
    content.x = paddingX;
    content.y = paddingY;
    return {
        type: element.type === 'borderBox' ? 'borderBox' : 'box',
        width: content.width + paddingX * 2,
        height: content.height + paddingY * 2,
        ascent: content.ascent + paddingY,
        descent: content.descent + paddingY,
        children: [content],
        paddingX,
        paddingY,
    };
}

function nextContext(context, add = 1) {
    return { ...context, level: Math.min(3, Math.max(0, Number(context.level || 0) + add)) };
}

function scaled(level) {
    return SCALE[Math.max(0, Math.min(3, Number(level || 0) || 0))];
}

function measureMathText(metrics, text, style) {
    if (typeof metrics?.measureText === 'function') {
        return metrics.measureText(text, style);
    }

    if (typeof metrics?.measureRun === 'function') {
        return metrics.measureRun({ text, ...style });
    }

    const fontSize = Number(style?.fontSize) || 16;
    return { width: String(text || '').length * fontSize * 0.54 };
}
