const MATHML_NAMESPACE = 'http://www.w3.org/1998/Math/MathML';

export function parseMathMLToContent(mathML) {
    const documentNode = parseXmlDocument(mathML);
    if (!documentNode) {
        return { elements: [] };
    }

    const root = localName(documentNode.name) === 'math'
        ? documentNode
        : firstElement(documentNode) || documentNode;
    return contentFromNodes(localName(root.name) === 'math' ? root.children : [root]);
}

export function mathContentToMathML(content, options = {}) {
    const body = contentToMathML(content || { elements: [] });
    const display = normalizeDisplayMode(options.displayMode ?? options.DisplayMode);
    if (options.includeRoot === false) {
        return body;
    }

    return `<math xmlns="${MATHML_NAMESPACE}" display="${display === 'display' ? 'block' : 'inline'}">${body}</math>`;
}

function contentFromNodes(nodes) {
    const elements = [];
    for (const node of nodes || []) {
        if (node.type === 'text') {
            const text = normalizeText(node.text);
            if (text) {
                elements.push(runElement(text, 'normal'));
            }
            continue;
        }

        const converted = elementFromNode(node);
        if (!converted) {
            continue;
        }

        if (Array.isArray(converted.elements)) {
            elements.push(...converted.elements);
        } else {
            elements.push(converted);
        }
    }

    return { elements };
}

function elementFromNode(node) {
    const name = localName(node.name);
    switch (name) {
        case 'math':
        case 'mrow':
        case 'semantics':
        case 'mstyle':
        case 'mpadded':
        case 'mphantom':
            return contentFromNodes(node.children);
        case 'annotation':
        case 'annotation-xml':
            return null;
        case 'mi':
            return runElement(textContent(node), identifierStyle(textContent(node)));
        case 'mn':
        case 'mtext':
            return runElement(textContent(node), 'normal');
        case 'mo':
            return runElement(textContent(node), operatorStyle(textContent(node)));
        case 'mfrac':
            return {
                type: 'fraction',
                fractionType: fractionType(node.attrs),
                numerator: contentFromChild(node, 0),
                denominator: contentFromChild(node, 1),
            };
        case 'msqrt':
            return {
                type: 'radical',
                radicand: contentFromNodes(node.children),
            };
        case 'mroot':
            return {
                type: 'radical',
                radicand: contentFromChild(node, 0),
                degree: contentFromChild(node, 1),
            };
        case 'msup':
            return {
                type: 'sup',
                base: contentFromChild(node, 0),
                superscript: contentFromChild(node, 1),
            };
        case 'msub':
            return {
                type: 'sub',
                base: contentFromChild(node, 0),
                subscript: contentFromChild(node, 1),
            };
        case 'msubsup':
            return {
                type: 'subSup',
                base: contentFromChild(node, 0),
                subscript: contentFromChild(node, 1),
                superscript: contentFromChild(node, 2),
            };
        case 'mmultiscripts':
            return multiscriptsFromNode(node);
        case 'munderover':
            return underOverFromNode(node, true, true);
        case 'munder':
            return underOverFromNode(node, true, false);
        case 'mover':
            return underOverFromNode(node, false, true);
        case 'mfenced':
            return {
                type: 'delimiter',
                open: node.attrs.open || '(',
                close: node.attrs.close || ')',
                separator: node.attrs.separators || ',',
                content: contentFromNodes(node.children),
            };
        case 'mtable':
            return {
                type: 'matrix',
                rows: elementChildren(node).map(row => ({
                    cells: elementChildren(row).map(cell => contentFromNodes(cell.children)),
                })),
            };
        case 'menclose':
            return encloseFromNode(node);
        default:
            return contentFromNodes(node.children);
    }
}

function underOverFromNode(node, hasUnder, hasOver) {
    const children = elementChildren(node);
    const base = children[0] || null;
    const under = hasUnder ? children[1] || null : null;
    const over = hasOver ? children[hasUnder ? 2 : 1] || null : null;
    const baseText = textContent(base);
    const overText = textContent(over);

    if (hasOver && node.attrs.accent === 'true') {
        if (overText === '¯' || overText === '‾') {
            return { type: 'bar', position: 'over', base: contentFromNode(base) };
        }

        return { type: 'accent', accent: overText || '̂', base: contentFromNode(base) };
    }

    if (hasUnder && node.attrs.accentunder === 'true') {
        return { type: 'bar', position: 'under', base: contentFromNode(base) };
    }

    if (isNaryOperator(baseText)) {
        return {
            type: 'nary',
            operator: baseText,
            lowerLimit: under ? contentFromNode(under) : null,
            upperLimit: over ? contentFromNode(over) : null,
            base: { elements: [] },
        };
    }

    if (baseText === 'lim') {
        return {
            type: 'limit',
            base: contentFromNode(base),
            lowerLimit: under ? contentFromNode(under) : null,
            upperLimit: over ? contentFromNode(over) : null,
            content: { elements: [] },
        };
    }

    return {
        type: 'limit',
        base: contentFromNode(base),
        lowerLimit: under ? contentFromNode(under) : null,
        upperLimit: over ? contentFromNode(over) : null,
        content: { elements: [] },
    };
}

function multiscriptsFromNode(node) {
    const children = elementChildren(node);
    const preIndex = children.findIndex(child => localName(child.name) === 'mprescripts');
    if (preIndex >= 0) {
        return {
            type: 'preSubSup',
            base: contentFromNode(children[0]),
            subscript: contentFromNode(children[preIndex + 1]),
            superscript: contentFromNode(children[preIndex + 2]),
        };
    }

    return {
        type: 'subSup',
        base: contentFromNode(children[0]),
        subscript: contentFromNode(children[1]),
        superscript: contentFromNode(children[2]),
    };
}

function encloseFromNode(node) {
    const notation = String(node.attrs.notation || 'box').toLowerCase();
    if (notation.includes('top')) {
        return { type: 'bar', position: 'over', base: contentFromNodes(node.children) };
    }

    if (notation.includes('bottom')) {
        return { type: 'bar', position: 'under', base: contentFromNodes(node.children) };
    }

    return { type: 'borderBox', content: contentFromNodes(node.children) };
}

function contentToMathML(content) {
    return (content?.elements || []).map(elementToMathML).join('');
}

function elementToMathML(element) {
    switch (element?.type) {
        case 'fraction':
            return `<mfrac${fractionAttributes(element)}>${slotToMathML(element.numerator)}${slotToMathML(element.denominator)}</mfrac>`;
        case 'radical':
            return element.degree
                ? `<mroot>${slotToMathML(element.radicand)}${slotToMathML(element.degree)}</mroot>`
                : `<msqrt>${contentToMathML(element.radicand)}</msqrt>`;
        case 'sup':
            return `<msup>${slotToMathML(element.base)}${slotToMathML(element.superscript)}</msup>`;
        case 'sub':
            return `<msub>${slotToMathML(element.base)}${slotToMathML(element.subscript)}</msub>`;
        case 'subSup':
            return `<msubsup>${slotToMathML(element.base)}${slotToMathML(element.subscript)}${slotToMathML(element.superscript)}</msubsup>`;
        case 'preSubSup':
            return `<mmultiscripts>${slotToMathML(element.base)}<mprescripts/>${slotToMathML(element.subscript)}${slotToMathML(element.superscript)}</mmultiscripts>`;
        case 'nary':
            return naryToMathML(element);
        case 'delimiter':
            return `<mrow><mo>${escapeXml(element.open || '(')}</mo>${contentToMathML(element.content)}<mo>${escapeXml(element.close || ')')}</mo></mrow>`;
        case 'function':
            return `<mrow>${slotToMathML(element.functionName)}${slotToMathML(element.base || element.content)}</mrow>`;
        case 'accent':
            return `<mover accent="true">${slotToMathML(element.base || element.content)}<mo>${escapeXml(element.accent || element.text || '̂')}</mo></mover>`;
        case 'bar':
            return String(element.position || 'over').toLowerCase() === 'under'
                ? `<munder accentunder="true">${slotToMathML(element.base || element.content)}<mo>_</mo></munder>`
                : `<mover accent="true">${slotToMathML(element.base || element.content)}<mo>¯</mo></mover>`;
        case 'groupChar':
            return String(element.position || 'over').toLowerCase() === 'under'
                ? `<munder accentunder="true">${slotToMathML(element.base || element.content)}<mo>${escapeXml(element.accent || element.text || '⏟')}</mo></munder>`
                : `<mover accent="true">${slotToMathML(element.base || element.content)}<mo>${escapeXml(element.accent || element.text || '⏞')}</mo></mover>`;
        case 'limit':
            return limitToMathML(element);
        case 'matrix':
            return `<mtable>${(element.rows || []).map(row => `<mtr>${(row.cells || []).map(cell => `<mtd>${contentToMathML(cell)}</mtd>`).join('')}</mtr>`).join('')}</mtable>`;
        case 'box':
            return `<mrow>${contentToMathML(element.content || element.base)}</mrow>`;
        case 'borderBox':
            return `<menclose notation="box">${contentToMathML(element.content || element.base)}</menclose>`;
        case 'run':
        default:
            return tokenToMathML(element);
    }
}

function naryToMathML(element) {
    const operator = `<mo>${escapeXml(element.operator || '∑')}</mo>`;
    const lower = element.lowerLimit ? slotToMathML(element.lowerLimit) : null;
    const upper = element.upperLimit ? slotToMathML(element.upperLimit) : null;
    const base = element.base ? slotToMathML(element.base) : '';
    if (lower && upper) {
        return `<mrow><munderover>${operator}${lower}${upper}</munderover>${base}</mrow>`;
    }

    if (lower) {
        return `<mrow><munder>${operator}${lower}</munder>${base}</mrow>`;
    }

    if (upper) {
        return `<mrow><mover>${operator}${upper}</mover>${base}</mrow>`;
    }

    return `<mrow>${operator}${base}</mrow>`;
}

function limitToMathML(element) {
    const base = slotToMathML(element.base || { elements: [{ type: 'run', text: 'lim', style: 'normal' }] });
    const lower = element.lowerLimit ? slotToMathML(element.lowerLimit) : null;
    const upper = element.upperLimit ? slotToMathML(element.upperLimit) : null;
    const expression = element.content ? slotToMathML(element.content) : '';
    if (lower && upper) {
        return `<mrow><munderover>${base}${lower}${upper}</munderover>${expression}</mrow>`;
    }

    if (lower) {
        return `<mrow><munder>${base}${lower}</munder>${expression}</mrow>`;
    }

    if (upper) {
        return `<mrow><mover>${base}${upper}</mover>${expression}</mrow>`;
    }

    return `<mrow>${base}${expression}</mrow>`;
}

function tokenToMathML(element) {
    const text = String(element?.text ?? '');
    const escaped = escapeXml(text);
    if (/^[0-9]+([.,][0-9]+)?$/.test(text)) {
        return `<mn>${escaped}</mn>`;
    }

    if (String(element?.style || '').toLowerCase() === 'italic' && /^[a-zA-Zα-ωΑ-Ω]+$/u.test(text)) {
        return `<mi>${escaped}</mi>`;
    }

    if (/^[+\-=*/()[\]{}<>≤≥≠≈→←∑∏∫∞,.;:|]+$/u.test(text)) {
        return `<mo>${escaped}</mo>`;
    }

    return `<mtext>${escaped}</mtext>`;
}

function slotToMathML(content) {
    return `<mrow>${contentToMathML(content || { elements: [] })}</mrow>`;
}

function fractionAttributes(element) {
    if (String(element.fractionType || '').toLowerCase() === 'nobar') {
        return ' linethickness="0"';
    }

    if (String(element.fractionType || '').toLowerCase() === 'skewed') {
        return ' bevelled="true"';
    }

    return '';
}

function fractionType(attrs) {
    if (String(attrs.bevelled || '').toLowerCase() === 'true') {
        return 'skewed';
    }

    if (String(attrs.linethickness || '').trim() === '0') {
        return 'noBar';
    }

    return 'bar';
}

function contentFromNode(node) {
    return node ? contentFromNodes([node]) : { elements: [] };
}

function contentFromChild(node, index) {
    return contentFromNode(elementChildren(node)[index]);
}

function elementChildren(node) {
    return (node?.children || []).filter(child => child.type === 'element');
}

function firstElement(node) {
    if (node?.type === 'element') {
        return node;
    }

    for (const child of node?.children || []) {
        const found = firstElement(child);
        if (found) {
            return found;
        }
    }

    return null;
}

function textContent(node) {
    if (!node) {
        return '';
    }

    if (node.type === 'text') {
        return normalizeText(node.text);
    }

    return normalizeText((node.children || []).map(textContent).join(''));
}

function normalizeText(value) {
    return String(value || '').replace(/\s+/g, ' ').trim();
}

function runElement(text, style) {
    return { type: 'run', text, style };
}

function identifierStyle(text) {
    return /^[a-zA-Zα-ωΑ-Ω]$/u.test(String(text || '')) ? 'italic' : 'normal';
}

function operatorStyle(text) {
    return /^[a-zA-Zα-ωΑ-Ω]+$/u.test(String(text || '')) ? 'italic' : 'normal';
}

function isNaryOperator(text) {
    return ['∑', '∏', '∫', '∮', 'Σ', 'Π'].includes(String(text || '').trim());
}

function normalizeDisplayMode(value) {
    if (typeof value === 'number') {
        return value === 1 ? 'display' : 'inline';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    return normalized === 'display' || normalized === 'block' ? 'display' : 'inline';
}

function parseXmlDocument(xml) {
    const source = String(xml || '').trim();
    if (!source) {
        return null;
    }

    if (typeof DOMParser !== 'undefined') {
        const parsed = new DOMParser().parseFromString(source, 'application/xml');
        if (parsed.querySelector('parsererror')) {
            return null;
        }

        return domNodeToTree(parsed.documentElement);
    }

    return parseXmlFallback(source);
}

function domNodeToTree(node) {
    if (node.nodeType === 3) {
        return { type: 'text', text: node.nodeValue || '' };
    }

    return {
        type: 'element',
        name: node.nodeName,
        attrs: Object.fromEntries(Array.from(node.attributes || []).map(attribute => [localName(attribute.name), attribute.value])),
        children: Array.from(node.childNodes || []).map(domNodeToTree).filter(Boolean),
    };
}

function parseXmlFallback(xml) {
    const root = { type: 'element', name: 'root', attrs: {}, children: [] };
    const stack = [root];
    const tokens = xml
        .replace(/<\?xml[\s\S]*?\?>/g, '')
        .replace(/<!--[\s\S]*?-->/g, '')
        .match(/<[^>]+>|[^<]+/g) || [];

    for (const token of tokens) {
        if (token.startsWith('</')) {
            if (stack.length > 1) {
                stack.pop();
            }
            continue;
        }

        if (token.startsWith('<')) {
            if (/^<!/.test(token)) {
                continue;
            }

            const selfClosing = /\/>$/.test(token);
            const inner = token.slice(1, token.length - (selfClosing ? 2 : 1)).trim();
            const space = inner.search(/\s/);
            const name = space < 0 ? inner : inner.slice(0, space);
            const attrs = parseAttributes(space < 0 ? '' : inner.slice(space + 1));
            const node = { type: 'element', name, attrs, children: [] };
            stack[stack.length - 1].children.push(node);
            if (!selfClosing && localName(name) !== 'mprescripts') {
                stack.push(node);
            }
            continue;
        }

        stack[stack.length - 1].children.push({ type: 'text', text: decodeEntities(token) });
    }

    return firstElement(root);
}

function parseAttributes(source) {
    const attrs = {};
    for (const match of source.matchAll(/([:\w-]+)\s*=\s*("([^"]*)"|'([^']*)')/g)) {
        attrs[localName(match[1])] = decodeEntities(match[3] ?? match[4] ?? '');
    }

    return attrs;
}

function localName(name) {
    return String(name || '').split(':').pop();
}

function escapeXml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;');
}

function decodeEntities(value) {
    return String(value || '')
        .replaceAll('&lt;', '<')
        .replaceAll('&gt;', '>')
        .replaceAll('&quot;', '"')
        .replaceAll('&apos;', "'")
        .replaceAll('&amp;', '&');
}
