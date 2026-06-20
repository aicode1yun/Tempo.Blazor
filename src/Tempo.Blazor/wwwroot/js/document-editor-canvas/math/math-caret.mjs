import { createMathContentFromLinear, mathToAccessibleText, normalizeMathContent, normalizeMathRun } from './math-model.mjs';

export function collectMathSlots(mathOrRun, options = {}) {
    const math = normalizeMathLike(mathOrRun);
    const slots = [];
    collectContentSlots(normalizeMathContent(math.content), [], slots, math.mathId || '');

    if (options.includeRoot === true || slots.length === 0) {
        slots.unshift(createSlot(math.mathId || '', [], 'equation', math.content));
    }

    return slots;
}

export function moveMathSlot(mathOrRun, slotPath, direction = 'next') {
    const slots = collectMathSlots(mathOrRun);
    if (slots.length === 0) {
        return { slot: null, index: -1, slots };
    }

    const currentPath = normalizePath(slotPath);
    const currentIndex = Math.max(0, slots.findIndex(slot => samePath(slot.path, currentPath)));
    const delta = compact(direction) === 'previous' || compact(direction) === 'prev' ? -1 : 1;
    const index = (currentIndex + delta + slots.length) % slots.length;
    return { slot: slots[index], index, slots };
}

export function createMathSlotRange(mathOrRun, anchorPath, focusPath) {
    const slots = collectMathSlots(mathOrRun).filter(slot => slot.path.length > 0);
    const anchor = slotForPathOrNearest(slots, anchorPath);
    const focus = slotForPathOrNearest(slots, focusPath);
    if (!anchor || !focus) {
        return { anchor: null, focus: null, selectedSlots: [], structuralPath: [] };
    }

    const anchorIndex = slots.indexOf(anchor);
    const focusIndex = slots.indexOf(focus);
    const start = Math.min(anchorIndex, focusIndex);
    const end = Math.max(anchorIndex, focusIndex);
    const selectedSlots = slots.slice(start, end + 1);
    return {
        anchor,
        focus,
        selectedSlots,
        structuralPath: commonStructuralPath(selectedSlots.map(slot => slot.path)),
        isReversed: focusIndex < anchorIndex,
    };
}

export function insertTextInMathSlot(mathOrRun, slotPath, text, options = {}) {
    const math = cloneMathForEdit(mathOrRun);
    const path = normalizePath(slotPath);
    const content = getContentAtPath(math.content, path);
    const nextContent = insertTextIntoContent(content, String(text ?? ''), options.offset ?? options.Offset);
    math.content = setContentAtPath(math.content, path, nextContent.content);
    return finalizeMathEdit(math, path, nextContent.offset);
}

export function deleteTextInMathSlot(mathOrRun, slotPath, options = {}) {
    const math = cloneMathForEdit(mathOrRun);
    const path = normalizePath(slotPath);
    const content = getContentAtPath(math.content, path);
    const direction = compact(options.direction ?? options.Direction) === 'forward' ? 'forward' : 'backward';
    const offset = clampOffset(options.offset ?? options.Offset, contentTextLength(content));
    const structural = deleteStructureAtSlotBoundary(math.content, path, {
        direction,
        offset,
        content,
    });
    if (structural.changed) {
        math.content = structural.content;
        return finalizeMathEdit(math, structural.slotPath, structural.offset);
    }

    const nextContent = deleteTextFromContent(content, offset, direction);
    math.content = setContentAtPath(math.content, path, nextContent.content);
    return finalizeMathEdit(math, path, nextContent.offset);
}

export function replaceContentInMathSlot(mathOrRun, slotPath, contentOrLinear, options = {}) {
    const math = cloneMathForEdit(mathOrRun);
    const path = normalizePath(slotPath);
    const nextContent = contentOrLinear?.elements
        ? normalizeMathContent(contentOrLinear)
        : createMathContentFromLinear(String(contentOrLinear ?? ''));
    math.content = setContentAtPath(math.content, path, nextContent);
    return finalizeMathEdit(
        math,
        path,
        options.offset ?? options.Offset ?? contentTextLength(nextContent));
}

export function addMathMatrixRow(mathOrRun, matrixPath, options = {}) {
    const math = cloneMathForEdit(mathOrRun);
    const path = normalizePath(matrixPath);
    const matrix = getElementAtPath(math.content, path);
    if (!matrix || matrix.type !== 'matrix') {
        return finalizeMathEdit(math, path, 0, false);
    }

    const rows = Array.isArray(matrix.rows) ? matrix.rows.map(row => ({
        cells: Array.isArray(row?.cells) ? row.cells.map(cell => normalizeMathContent(cell)) : [],
    })) : [];
    const columnCount = Math.max(1, ...rows.map(row => row.cells.length), Number(options.columns ?? options.Columns ?? 0) || 0);
    const values = Array.isArray(options.values || options.Values) ? (options.values || options.Values) : [];
    const newRow = {
        cells: Array.from({ length: columnCount }, (_, columnIndex) => contentFromMatrixValue(values[columnIndex] ?? '')),
    };
    const afterRowIndex = clampIndex(options.afterRowIndex ?? options.AfterRowIndex, rows.length - 1);
    rows.splice(afterRowIndex + 1, 0, newRow);
    matrix.rows = rows.map(row => ({
        cells: Array.from({ length: columnCount }, (_, columnIndex) => normalizeMathContent(row.cells[columnIndex] || { elements: [] })),
    }));
    math.content = setElementAtPath(math.content, path, matrix);
    return finalizeMathEdit(math, [...path, 'rows', afterRowIndex + 1, 'cells', 0], 0);
}

export function addMathMatrixColumn(mathOrRun, matrixPath, options = {}) {
    const math = cloneMathForEdit(mathOrRun);
    const path = normalizePath(matrixPath);
    const matrix = getElementAtPath(math.content, path);
    if (!matrix || matrix.type !== 'matrix') {
        return finalizeMathEdit(math, path, 0, false);
    }

    const rows = Array.isArray(matrix.rows) ? matrix.rows.map(row => ({
        cells: Array.isArray(row?.cells) ? row.cells.map(cell => normalizeMathContent(cell)) : [],
    })) : [];
    const columnCount = Math.max(1, ...rows.map(row => row.cells.length));
    const afterColumnIndex = clampIndex(options.afterColumnIndex ?? options.AfterColumnIndex, columnCount - 1);
    const values = Array.isArray(options.values || options.Values) ? (options.values || options.Values) : [];
    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        while (rows[rowIndex].cells.length < columnCount) {
            rows[rowIndex].cells.push(normalizeMathContent({ elements: [] }));
        }
        rows[rowIndex].cells.splice(afterColumnIndex + 1, 0, contentFromMatrixValue(values[rowIndex] ?? ''));
    }

    matrix.rows = rows;
    math.content = setElementAtPath(math.content, path, matrix);
    return finalizeMathEdit(math, [...path, 'rows', 0, 'cells', afterColumnIndex + 1], 0);
}

export function mathCaretRectForSlot(mathLayout, slotPath, offset = 0) {
    const path = normalizePath(slotPath);
    const box = boxForSlotPath(mathLayout, path);
    const contentWidth = Math.max(1, Number(box.node?.width) || 1);
    const textLength = Math.max(1, Number(box.textLength) || 1);
    const ratio = Math.max(0, Math.min(1, (Number(offset) || 0) / textLength));
    return {
        x: box.x + contentWidth * ratio,
        y: box.y,
        width: 1,
        height: Math.max(8, Number(box.node?.height) || Number(mathLayout?.height) || 12),
    };
}

export function mathSlotRectForSlot(mathLayout, slotPath) {
    const path = normalizePath(slotPath);
    const box = boxForSlotPath(mathLayout, path);
    return {
        x: box.x,
        y: box.y,
        width: Math.max(1, Number(box.node?.width) || 1),
        height: Math.max(8, Number(box.node?.height) || Number(mathLayout?.height) || 12),
    };
}

export function mathSlotAtPoint(mathLayout, x, y, options = {}) {
    if (!mathLayout) {
        return null;
    }

    const hitSlop = Math.max(0, Number(options.hitSlop ?? options.HitSlop ?? 3) || 0);
    const slots = collectMathSlots(mathLayout, { includeRoot: true });
    let best = null;
    for (const [slotIndex, slot] of slots.entries()) {
        const rect = mathSlotRectForSlot(mathLayout, slot.path);
        if (!pointInRect(x, y, rect, hitSlop)) {
            continue;
        }

        const centerX = rect.x + rect.width / 2;
        const centerY = rect.y + rect.height / 2;
        const score = Math.abs(Number(x || 0) - centerX) + Math.abs(Number(y || 0) - centerY) + (slot.path.length === 0 ? 4096 : 0);
        const priority = slot.path.length + slotIndex / 1000;
        if (!best || score < best.score - 0.000001 || (Math.abs(score - best.score) <= 0.000001 && priority > best.priority)) {
            best = {
                ...slot,
                rect,
                offset: offsetForPoint(rect, slot.textLength, x),
                score,
                priority,
            };
        }
    }

    if (best) {
        const { score, priority, ...slot } = best;
        return slot;
    }

    const rootRect = {
        x: 0,
        y: 0,
        width: Math.max(1, Number(mathLayout.width) || 1),
        height: Math.max(8, Number(mathLayout.height) || 12),
    };
    if (pointInRect(x, y, rootRect, hitSlop)) {
        const rootSlot = slots.find(slot => slot.path.length === 0) || collectMathSlots(mathLayout, { includeRoot: true })[0] || null;
        return rootSlot ? {
            ...rootSlot,
            rect: rootRect,
            offset: offsetForPoint(rootRect, rootSlot.textLength, x),
        } : null;
    }

    return null;
}

function collectContentSlots(content, path, slots, mathId) {
    const normalized = normalizeMathContent(content);
    normalized.elements.forEach((element, elementIndex) => {
        const elementPath = [...path, 'elements', elementIndex];
        collectElementSlots(element, elementPath, slots, mathId);
    });
}

function collectElementSlots(element, path, slots, mathId) {
    switch (String(element?.type || '').toLowerCase()) {
        case 'fraction':
            pushContentSlot(slots, mathId, [...path, 'numerator'], 'numerator', element.numerator);
            pushContentSlot(slots, mathId, [...path, 'denominator'], 'denominator', element.denominator);
            break;
        case 'radical':
            if (element.degree) {
                pushContentSlot(slots, mathId, [...path, 'degree'], 'degree', element.degree);
            }
            pushContentSlot(slots, mathId, [...path, 'radicand'], 'radicand', element.radicand);
            break;
        case 'sup':
            pushContentSlot(slots, mathId, [...path, 'base'], 'base', element.base);
            pushContentSlot(slots, mathId, [...path, 'superscript'], 'superscript', element.superscript);
            break;
        case 'sub':
            pushContentSlot(slots, mathId, [...path, 'base'], 'base', element.base);
            pushContentSlot(slots, mathId, [...path, 'subscript'], 'subscript', element.subscript);
            break;
        case 'subsup':
        case 'presubsup':
            pushContentSlot(slots, mathId, [...path, 'base'], 'base', element.base);
            pushContentSlot(slots, mathId, [...path, 'subscript'], 'subscript', element.subscript);
            pushContentSlot(slots, mathId, [...path, 'superscript'], 'superscript', element.superscript);
            break;
        case 'nary':
            if (element.lowerLimit) {
                pushContentSlot(slots, mathId, [...path, 'lowerLimit'], 'lower limit', element.lowerLimit);
            }
            if (element.upperLimit) {
                pushContentSlot(slots, mathId, [...path, 'upperLimit'], 'upper limit', element.upperLimit);
            }
            pushContentSlot(slots, mathId, [...path, 'base'], 'expression', element.base);
            break;
        case 'limit':
            pushContentSlot(slots, mathId, [...path, 'base'], 'limit operator', element.base);
            if (element.lowerLimit) {
                pushContentSlot(slots, mathId, [...path, 'lowerLimit'], 'lower limit', element.lowerLimit);
            }
            if (element.upperLimit) {
                pushContentSlot(slots, mathId, [...path, 'upperLimit'], 'upper limit', element.upperLimit);
            }
            pushContentSlot(slots, mathId, [...path, 'content'], 'expression', element.content);
            break;
        case 'delimiter':
        case 'box':
        case 'borderbox':
            pushContentSlot(slots, mathId, [...path, 'content'], 'content', element.content || element.base);
            break;
        case 'function':
            if (element.functionName) {
                pushContentSlot(slots, mathId, [...path, 'functionName'], 'function name', element.functionName);
            }
            pushContentSlot(slots, mathId, [...path, 'base'], 'argument', element.base || element.content);
            break;
        case 'accent':
        case 'bar':
        case 'groupchar':
            pushContentSlot(slots, mathId, [...path, 'base'], 'base', element.base || element.content);
            break;
        case 'matrix':
            collectMatrixSlots(element, path, slots, mathId);
            break;
        default:
            break;
    }
}

function collectMatrixSlots(element, path, slots, mathId) {
    const rows = Array.isArray(element.rows) ? element.rows : [];
    rows.forEach((row, rowIndex) => {
        const cells = Array.isArray(row?.cells) ? row.cells : [];
        cells.forEach((cell, columnIndex) => {
            pushContentSlot(
                slots,
                mathId,
                [...path, 'rows', rowIndex, 'cells', columnIndex],
                `cell ${rowIndex + 1}, ${columnIndex + 1}`,
                cell);
        });
    });
}

function pushContentSlot(slots, mathId, path, slotName, content) {
    const normalized = normalizeMathContent(content || { elements: [] });
    slots.push(createSlot(mathId, path, slotName, normalized));
    collectContentSlots(normalized, path, slots, mathId);
}

function createSlot(mathId, path, slotName, content) {
    const normalized = normalizeMathContent(content || { elements: [] });
    return {
        mathId,
        path: path.slice(),
        pathKey: path.join('.'),
        slotName,
        label: slotName,
        text: mathToAccessibleText(normalized),
        textLength: mathToAccessibleText(normalized).length,
    };
}

function slotForPathOrNearest(slots, pathValue) {
    const path = normalizePath(pathValue);
    return slots.find(slot => samePath(slot.path, path)) || slots[0] || null;
}

function commonStructuralPath(paths) {
    if (!Array.isArray(paths) || paths.length === 0) {
        return [];
    }

    const first = normalizePath(paths[0]);
    let length = first.length;
    for (const pathValue of paths.slice(1)) {
        const path = normalizePath(pathValue);
        length = Math.min(length, path.length);
        for (let index = 0; index < length; index += 1) {
            if (first[index] !== path[index]) {
                length = index;
                break;
            }
        }
    }

    const common = first.slice(0, length);
    const elementStart = lastElementPathStart(common);
    return elementStart >= 0 ? common.slice(0, elementStart + 2) : [];
}

function insertTextIntoContent(content, text, offsetValue) {
    const normalized = normalizeMathContent(content);
    const insertText = String(text ?? '');
    if (!insertText) {
        return { content: normalized, offset: clampOffset(offsetValue, contentTextLength(normalized)) };
    }

    const elements = normalized.elements.map(element => clone(element));
    if (elements.length === 0) {
        elements.push({ type: 'run', text: '' });
    }

    const offset = clampOffset(offsetValue, contentTextLength({ elements }));
    let cursor = 0;
    for (let index = 0; index < elements.length; index += 1) {
        const element = elements[index];
        const type = String(element?.type || '').toLowerCase();
        const length = type === 'run' ? String(element.text ?? '').length : elementTextLength(element);
        if (type === 'run' && offset <= cursor + length) {
            const local = Math.max(0, Math.min(length, offset - cursor));
            element.text = `${String(element.text ?? '').slice(0, local)}${insertText}${String(element.text ?? '').slice(local)}`;
            return { content: normalizeMathContent({ elements }), offset: offset + insertText.length };
        }

        if (offset <= cursor + length) {
            const insertIndex = offset <= cursor + length / 2 ? index : index + 1;
            elements.splice(insertIndex, 0, { type: 'run', text: insertText });
            return { content: normalizeMathContent({ elements }), offset: offset + insertText.length };
        }

        cursor += length;
    }

    elements.push({ type: 'run', text: insertText });
    return { content: normalizeMathContent({ elements }), offset: cursor + insertText.length };
}

function deleteTextFromContent(content, offsetValue, directionValue) {
    const normalized = normalizeMathContent(content);
    const elements = normalized.elements.map(element => clone(element));
    const textLength = contentTextLength({ elements });
    const direction = compact(directionValue) === 'forward' ? 'forward' : 'backward';
    const offset = clampOffset(offsetValue, textLength);
    if (elements.length === 0 || textLength === 0) {
        return { content: normalized, offset: 0 };
    }

    let cursor = 0;
    for (let index = 0; index < elements.length; index += 1) {
        const element = elements[index];
        const type = String(element?.type || '').toLowerCase();
        const length = type === 'run' ? String(element.text ?? '').length : elementTextLength(element);
        const end = cursor + length;
        if (direction === 'backward' && offset > cursor && offset <= end) {
            if (type === 'run') {
                const local = Math.max(1, Math.min(length, offset - cursor));
                element.text = `${String(element.text ?? '').slice(0, local - 1)}${String(element.text ?? '').slice(local)}`;
                pruneEmptyRun(elements, index);
                return { content: normalizeMathContent({ elements }), offset: offset - 1 };
            }

            elements.splice(index, 1);
            return { content: normalizeMathContent({ elements }), offset: cursor };
        }

        if (direction === 'forward' && offset >= cursor && offset < end) {
            if (type === 'run') {
                const local = Math.max(0, Math.min(length - 1, offset - cursor));
                element.text = `${String(element.text ?? '').slice(0, local)}${String(element.text ?? '').slice(local + 1)}`;
                pruneEmptyRun(elements, index);
                return { content: normalizeMathContent({ elements }), offset };
            }

            elements.splice(index, 1);
            return { content: normalizeMathContent({ elements }), offset: cursor };
        }

        cursor = end;
    }

    return { content: normalizeMathContent({ elements }), offset };
}

function deleteStructureAtSlotBoundary(rootContent, slotPath, options = {}) {
    const path = normalizePath(slotPath);
    if (path.length < 3) {
        return { changed: false };
    }

    const direction = compact(options.direction) === 'forward' ? 'forward' : 'backward';
    const content = normalizeMathContent(options.content || getContentAtPath(rootContent, path));
    const offset = clampOffset(options.offset, contentTextLength(content));
    if ((direction === 'backward' && offset !== 0) || (direction === 'forward' && offset !== contentTextLength(content))) {
        return { changed: false };
    }

    const elementPathStart = lastElementPathStart(path);
    if (elementPathStart < 0) {
        return { changed: false };
    }

    const parentContentPath = path.slice(0, elementPathStart);
    const elementIndex = Number(path[elementPathStart + 1]);
    if (!Number.isInteger(elementIndex) || elementIndex < 0) {
        return { changed: false };
    }

    const parentContent = getContentAtPath(rootContent, parentContentPath);
    const elements = Array.isArray(parentContent.elements) ? parentContent.elements.map(clone) : [];
    if (elementIndex >= elements.length) {
        return { changed: false };
    }

    const replacement = normalizeMathContent(content);
    const beforeOffset = contentTextLength({ elements: elements.slice(0, elementIndex) });
    const replacementElements = replacement.elements.map(clone);
    elements.splice(elementIndex, 1, ...replacementElements);
    const nextParent = normalizeMathContent({ ...parentContent, elements });
    return {
        changed: true,
        content: setContentAtPath(rootContent, parentContentPath, nextParent),
        slotPath: parentContentPath,
        offset: direction === 'forward'
            ? beforeOffset + contentTextLength(replacement)
            : beforeOffset,
    };
}

function lastElementPathStart(path) {
    for (let index = path.length - 2; index >= 0; index -= 1) {
        if (path[index] === 'elements' && Number.isInteger(Number(path[index + 1]))) {
            return index;
        }
    }

    return -1;
}

function finalizeMathEdit(math, slotPath, offset, changed = true) {
    const normalized = normalizeMathRun({ id: math.mathId || '', math: {
        ...math,
        content: normalizeMathContent(math.content),
        mathML: changed ? null : math.mathML,
        ommlXml: changed ? null : math.ommlXml,
        altText: '',
    } });
    normalized.altText = mathToAccessibleText(normalized);
    const slot = collectMathSlots(normalized, { includeRoot: true }).find(item => samePath(item.path, normalizePath(slotPath)))
        || createSlot(normalized.mathId || '', normalizePath(slotPath), 'equation', normalized.content);
    return {
        changed,
        math: normalized,
        slot,
        offset: clampOffset(offset, slot.textLength),
    };
}

function normalizeMathLike(value) {
    if (value?.content?.elements) {
        return normalizeMathRun({ id: value.mathId || value.id || '', math: value });
    }

    if (value?.elements) {
        return normalizeMathRun({ id: '', math: { content: value } });
    }

    return normalizeMathRun(value || {});
}

function cloneMathForEdit(value) {
    return normalizeMathLike(clone(value));
}

function getContentAtPath(content, pathValue) {
    const path = normalizePath(pathValue);
    if (path.length === 0) {
        return normalizeMathContent(content);
    }

    return normalizeMathContent(getAtPath(content, path) || { elements: [] });
}

function setContentAtPath(content, pathValue, nextContent) {
    const path = normalizePath(pathValue);
    const root = clone(normalizeMathContent(content));
    if (path.length === 0) {
        return normalizeMathContent(nextContent);
    }

    setAtPath(root, path, normalizeMathContent(nextContent));
    return normalizeMathContent(root);
}

function getElementAtPath(content, pathValue) {
    const value = getAtPath(normalizeMathContent(content), normalizePath(pathValue));
    return value && typeof value === 'object' ? clone(value) : null;
}

function setElementAtPath(content, pathValue, element) {
    const root = clone(normalizeMathContent(content));
    setAtPath(root, normalizePath(pathValue), clone(element));
    return normalizeMathContent(root);
}

function getAtPath(root, path) {
    let current = root;
    for (const segment of path) {
        if (current == null) {
            return null;
        }

        current = current[segment];
    }

    return current;
}

function setAtPath(root, path, value) {
    let current = root;
    for (let index = 0; index < path.length - 1; index += 1) {
        current = current[path[index]];
        if (current == null) {
            return false;
        }
    }

    current[path.at(-1)] = value;
    return true;
}

function normalizePath(value) {
    if (Array.isArray(value)) {
        return value.map(segment => numericOrString(segment));
    }

    if (typeof value === 'string') {
        const trimmed = value.trim();
        if (!trimmed) {
            return [];
        }

        try {
            const parsed = JSON.parse(trimmed);
            if (Array.isArray(parsed)) {
                return parsed.map(segment => numericOrString(segment));
            }
        } catch {
            return trimmed.split(/[./]/).filter(Boolean).map(segment => numericOrString(segment));
        }
    }

    return [];
}

function numericOrString(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.trunc(value));
    }

    const text = String(value ?? '');
    return /^\d+$/.test(text) ? Number(text) : text;
}

function samePath(left, right) {
    const a = normalizePath(left);
    const b = normalizePath(right);
    return a.length === b.length && a.every((segment, index) => segment === b[index]);
}

function contentFromMatrixValue(value) {
    if (value?.elements) {
        return normalizeMathContent(value);
    }

    return createMathContentFromLinear(String(value ?? ''));
}

function contentTextLength(content) {
    return mathToAccessibleText(normalizeMathContent(content)).length;
}

function elementTextLength(element) {
    return mathToAccessibleText({ elements: [element] }).length;
}

function clampOffset(value, length) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return Math.max(0, Number(length) || 0);
    }

    return Math.max(0, Math.min(Math.max(0, Number(length) || 0), Math.trunc(parsed)));
}

function pointInRect(x, y, rect, hitSlop = 0) {
    const left = Number(rect?.x || 0) - hitSlop;
    const top = Number(rect?.y || 0) - hitSlop;
    const right = (Number(rect?.x || 0) || 0) + Math.max(1, Number(rect?.width || 0) || 1) + hitSlop;
    const bottom = (Number(rect?.y || 0) || 0) + Math.max(1, Number(rect?.height || 0) || 1) + hitSlop;
    return Number(x || 0) >= left
        && Number(x || 0) <= right
        && Number(y || 0) >= top
        && Number(y || 0) <= bottom;
}

function offsetForPoint(rect, textLength, x) {
    const length = Math.max(0, Number(textLength || 0) || 0);
    if (length === 0) {
        return 0;
    }

    const width = Math.max(1, Number(rect?.width || 0) || 1);
    const ratio = Math.max(0, Math.min(1, ((Number(x || 0) || 0) - (Number(rect?.x || 0) || 0)) / width));
    return Math.max(0, Math.min(length, Math.round(length * ratio)));
}

function clampIndex(value, max) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return Math.max(-1, Number(max) || -1);
    }

    return Math.max(-1, Math.min(Math.max(-1, Number(max) || -1), Math.trunc(parsed)));
}

function pruneEmptyRun(elements, index) {
    const element = elements[index];
    if (String(element?.type || '').toLowerCase() === 'run' && String(element.text ?? '').length === 0 && elements.length > 1) {
        elements.splice(index, 1);
    }
}

function boxForSlotPath(layout, path) {
    let node = layout;
    let x = 0;
    let y = 0;
    for (let index = 0; index < path.length; index += 1) {
        const segment = path[index];
        if (segment !== 'elements') {
            continue;
        }

        const elementIndex = Number(path[index + 1]) || 0;
        const elementBox = node?.children?.[elementIndex] || null;
        if (!elementBox) {
            break;
        }

        x += Number(elementBox.x) || 0;
        y += Number(elementBox.y) || 0;
        node = elementBox;
        index += 1;

        const slotName = path[index + 1];
        if (typeof slotName === 'string') {
            const child = childBoxForSlot(node, slotName, path.slice(index + 2));
            if (child) {
                x += Number(child.x) || 0;
                y += Number(child.y) || 0;
                node = child;
            }
        }
    }

    return { node: node || layout, x, y, textLength: Math.max(1, Math.round((Number(node?.width) || 1) / 6)) };
}

function childBoxForSlot(node, slotName, restPath) {
    const type = String(node?.type || '').toLowerCase();
    if (!Array.isArray(node?.children)) {
        return null;
    }

    if (type === 'fraction') {
        return slotName === 'denominator' ? node.children[1] : node.children[0];
    }

    if (type === 'radical') {
        return slotName === 'degree' && node.children.length > 1 ? node.children[0] : node.children.at(-1);
    }

    if (type === 'sup' || type === 'sub' || type === 'subsup') {
        if (slotName === 'base') {
            return node.children[0];
        }

        if (slotName === 'superscript') {
            return node.children.find(child => child !== node.children[0] && child.y <= node.children[0].y) || node.children[1];
        }

        return node.children.find(child => child !== node.children[0] && child.y >= node.children[0].y) || node.children.at(-1);
    }

    if (type === 'nary') {
        if (slotName === 'base') {
            return node.children.at(-1) || null;
        }

        if (slotName === 'upperLimit') {
            return node.children.find(child => child.type === 'content' && child.y <= 0) || node.children[0] || null;
        }

        if (slotName === 'lowerLimit') {
            return node.children.find(child => child.type === 'content' && child !== node.children.at(-1) && child.y > 0) || null;
        }
    }

    if (type === 'matrix' && slotName === 'rows') {
        const rowIndex = Number(restPath[0]) || 0;
        const columnIndex = Number(restPath[2]) || 0;
        const columnCount = Math.max(1, Math.round(Math.sqrt(node.children.length)));
        return node.children[rowIndex * columnCount + columnIndex] || node.children[0];
    }

    return node.children.find(child => child.type === 'content') || node.children.at(-1) || null;
}

function compact(value) {
    return String(value == null ? '' : value).replace(/[\s_-]/g, '').toLowerCase();
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
