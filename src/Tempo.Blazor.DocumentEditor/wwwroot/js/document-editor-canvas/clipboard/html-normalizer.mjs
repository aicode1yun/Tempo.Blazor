export const INTERNAL_CLIPBOARD_MIME = 'application/x-tempo-document-fragment+json';

const BLOCK_TAGS = new Set(['p', 'div', 'li', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6']);
const UNSAFE_CONTENT = /<(script|style|iframe|object|embed|svg|math)\b[^>]*>[\s\S]*?<\/\1>/gi;
const TAG_OR_TEXT = /<[^>]+>|[^<]+/g;

export function normalizeClipboardHtml(html, plainText = '') {
    const sourceHtml = String(html || '');
    const sanitized = stripUnsafeContent(sourceHtml);
    const blocks = [];
    let cursor = 0;
    const tablePattern = /<table\b[^>]*>[\s\S]*?<\/table>/gi;
    let match;
    while ((match = tablePattern.exec(sanitized)) !== null) {
        blocks.push(...parseFlowHtml(sanitized.slice(cursor, match.index)));
        const table = parseTable(match[0], blocks.length);
        if (table) {
            blocks.push(table);
        }
        cursor = match.index + match[0].length;
    }

    blocks.push(...parseFlowHtml(sanitized.slice(cursor)));
    const finalBlocks = blocks.filter(block => block.type === 'table' || blockText(block).trim().length > 0);
    if (finalBlocks.length === 0 && String(plainText || '').trim().length > 0) {
        return createPlainTextFragment(plainText);
    }

    return createFragment({
        source: detectHtmlSource(sourceHtml),
        blocks: finalBlocks.length > 0 ? finalBlocks : createPlainTextFragment(stripTags(sourceHtml)).blocks,
        rawHtml: sourceHtml,
    });
}

export function createPlainTextFragment(text) {
    const lines = String(text || '').replace(/\r\n?/g, '\n').split('\n');
    const blocks = lines.length === 0
        ? [createTextBlock('paragraph', '', 0)]
        : lines.map((line, index) => createTextBlock('paragraph', line, index));
    return createFragment({ source: 'plainText', blocks, rawHtml: '' });
}

export function createUrlFragment(url, selectedText = '') {
    const href = sanitizeUrl(url);
    if (!href) {
        return createPlainTextFragment(url);
    }

    const text = String(selectedText || href);
    const block = createTextBlock('paragraph', '', 0);
    block.content.runs = [createTextRun(text, [
        {
            type: 'link',
            value: href,
            link: {
                href,
                target: '_blank',
                rel: 'noopener noreferrer',
            },
            preserve: {},
        },
    ], 'url-run-1')];
    return createFragment({ source: 'url', blocks: [block], rawHtml: '' });
}

export function isSingleUrl(value) {
    const text = String(value || '').trim();
    return !!sanitizeUrl(text) && !/[\r\n\t ]/.test(text.replace(/^https?:\/\//i, '').replace(/^mailto:/i, ''));
}

export function serializeInternalFragment(fragment) {
    return JSON.stringify(normalizeFragment(fragment));
}

export function parseInternalFragment(value) {
    try {
        const fragment = JSON.parse(String(value || ''));
        return normalizeFragment(fragment);
    } catch {
        return null;
    }
}

export function fragmentToPlainText(fragment) {
    return normalizeFragment(fragment).blocks.map(blockText).join('\n');
}

export function fragmentToHtml(fragment) {
    return normalizeFragment(fragment).blocks.map(block => {
        if (block.type === 'table') {
            const rows = block.content?.table?.rows || [];
            const body = rows.map(row => `<tr>${(row.cells || []).map(cell => `<td>${escapeHtml((cell.blocks || []).map(blockText).join('\n'))}</td>`).join('')}</tr>`).join('');
            return `<table>${body}</table>`;
        }

        const tag = block.type === 'heading'
            ? `h${Math.max(1, Math.min(6, Number(block.content?.headingLevel || 1) || 1))}`
            : block.type === 'list'
                ? 'li'
                : 'p';
        const html = (block.content?.runs || []).map(runToHtml).join('');
        return `<${tag}>${html}</${tag}>`;
    }).join('');
}

export function sanitizeUrl(value) {
    const text = String(value || '').trim();
    if (!text) {
        return '';
    }

    try {
        const url = new URL(text, 'https://tempo.invalid');
        if (url.protocol === 'http:' || url.protocol === 'https:' || url.protocol === 'mailto:') {
            return text;
        }
    } catch {
        return '';
    }

    return '';
}

export function normalizeFragment(fragment) {
    const source = typeof fragment?.source === 'string' && fragment.source ? fragment.source : 'unknown';
    const blocks = Array.isArray(fragment?.blocks)
        ? fragment.blocks.map((block, index) => normalizeBlock(block, index)).filter(Boolean)
        : [];
    return {
        schemaVersion: 1,
        source,
        blocks,
        plainText: blocks.map(blockText).join('\n'),
        html: fragment?.html || '',
        rawHtml: fragment?.rawHtml || '',
        warnings: Array.isArray(fragment?.warnings) ? fragment.warnings.slice() : [],
    };
}

function parseFlowHtml(html) {
    const source = stripUnsafeContent(html);
    const blocks = [];
    const markFrames = [];
    const listStack = [];
    let current = null;
    let token;

    while ((token = TAG_OR_TEXT.exec(source)) !== null) {
        const value = token[0];
        if (!value.startsWith('<')) {
            appendText(value);
            continue;
        }

        const tag = parseTag(value);
        if (!tag) {
            continue;
        }

        if (tag.closing) {
            closeTag(tag.name);
            continue;
        }

        if (tag.name === 'br') {
            appendText('\n');
            continue;
        }

        if (tag.name === 'ul' || tag.name === 'ol') {
            listStack.push(tag.name);
        }

        if (BLOCK_TAGS.has(tag.name)) {
            closeCurrent();
            current = createBlockForTag(tag.name, blocks.length, listStack.at(-1) === 'ol');
        }

        const marks = marksForTag(tag.name, tag.attributes);
        markFrames.push({ tag: tag.name, marks });
    }

    closeCurrent();
    return blocks;

    function appendText(raw) {
        const text = decodeEntities(raw).replace(/\s+/g, ' ');
        if (!text) {
            return;
        }

        if (!current) {
            current = createTextBlock('paragraph', '', blocks.length);
        }

        current.content.runs.push(createTextRun(text, activeMarks(), `${current.id}-run-${current.content.runs.length + 1}`));
    }

    function closeTag(tagName) {
        if (BLOCK_TAGS.has(tagName)) {
            closeCurrent();
        }

        if (tagName === 'ul' || tagName === 'ol') {
            listStack.pop();
        }

        const index = findLastIndex(markFrames, frame => frame.tag === tagName);
        if (index >= 0) {
            markFrames.splice(index, 1);
        }
    }

    function closeCurrent() {
        if (!current) {
            return;
        }

        current.content.runs = compactRuns(current.content.runs, current.id);
        if (blockText(current).trim().length > 0) {
            blocks.push(current);
        }

        current = null;
    }

    function activeMarks() {
        const byType = new Map();
        for (const frame of markFrames) {
            for (const mark of frame.marks) {
                byType.set(mark.type, clone(mark));
            }
        }

        return Array.from(byType.values());
    }
}

function parseTable(html, index) {
    const rows = [];
    const rowPattern = /<tr\b[^>]*>([\s\S]*?)<\/tr>/gi;
    let rowMatch;
    while ((rowMatch = rowPattern.exec(stripUnsafeContent(html))) !== null) {
        const cells = [];
        const cellPattern = /<t[dh]\b[^>]*>([\s\S]*?)<\/t[dh]>/gi;
        let cellMatch;
        while ((cellMatch = cellPattern.exec(rowMatch[1])) !== null) {
            const text = normalizeWhitespace(stripTags(cellMatch[1]));
            cells.push({
                id: `clipboard-table-${index}-cell-${rows.length + 1}-${cells.length + 1}`,
                blocks: [createTextBlock('paragraph', text, 0)],
                columnSpan: 1,
                rowSpan: 1,
                preserve: {},
            });
        }

        if (cells.length > 0) {
            rows.push({ id: `clipboard-table-${index}-row-${rows.length + 1}`, cells, preserve: {} });
        }
    }

    if (rows.length === 0) {
        return null;
    }

    return {
        id: `clipboard-table-${index + 1}`,
        sectionId: null,
        type: 'table',
        order: (index + 1) * 10,
        paragraphProperties: {},
        content: {
            type: 'table',
            runs: [],
            table: { rows, layout: {} },
        },
        preserve: {},
    };
}

function createBlockForTag(tag, index, ordered) {
    if (tag.startsWith('h')) {
        const level = Math.max(1, Math.min(6, Number(tag.slice(1)) || 1));
        const block = createTextBlock('heading', '', index);
        block.content.headingLevel = level;
        block.content.styleName = `Heading ${level}`;
        block.content.outlineLevel = level;
        return block;
    }

    if (tag === 'li') {
        const block = createTextBlock('list', '', index);
        block.content.list = { ordered, indentLevel: 0, startNumber: 1 };
        return block;
    }

    return createTextBlock('paragraph', '', index);
}

function createTextBlock(type, text, index) {
    const id = `clipboard-${type}-${index + 1}`;
    return {
        id,
        sectionId: null,
        type,
        order: (index + 1) * 10,
        paragraphProperties: {},
        content: {
            type,
            runs: text ? [createTextRun(text, [], `${id}-run-1`)] : [],
            headingLevel: null,
            styleId: null,
            styleName: null,
            outlineLevel: null,
            list: null,
            table: null,
            image: null,
            pageBreak: null,
        },
        preserve: {},
    };
}

function createTextRun(text, marks = [], id = 'clipboard-run') {
    return {
        id,
        type: 'text',
        text: String(text || ''),
        marks: marks.map(clone),
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        preserve: {},
    };
}

function marksForTag(tag, attributes) {
    const marks = [];
    if (tag === 'b' || tag === 'strong') marks.push({ type: 'bold', preserve: {} });
    if (tag === 'i' || tag === 'em') marks.push({ type: 'italic', preserve: {} });
    if (tag === 'u') marks.push({ type: 'underline', preserve: {} });
    if (tag === 'a') {
        const href = sanitizeUrl(attributes.href);
        if (href) {
            marks.push({ type: 'link', value: href, link: { href, target: '_blank', rel: 'noopener noreferrer' }, preserve: {} });
        }
    }

    const style = parseStyle(attributes.style);
    if (style.bold) marks.push({ type: 'bold', preserve: {} });
    if (style.italic) marks.push({ type: 'italic', preserve: {} });
    if (style.underline) marks.push({ type: 'underline', preserve: {} });
    if (style.color) marks.push({ type: 'textColor', value: style.color, preserve: {} });
    if (style.highlight) marks.push({ type: 'highlight', value: style.highlight, preserve: {} });
    return marks;
}

function parseTag(token) {
    const match = /^<\s*(\/)?\s*([a-zA-Z0-9]+)([^>]*)>$/i.exec(token);
    if (!match) {
        return null;
    }

    const name = match[2].toLowerCase();
    return {
        name,
        closing: !!match[1],
        attributes: parseAttributes(match[3] || ''),
    };
}

function parseAttributes(raw) {
    const attributes = {};
    const pattern = /([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*("([^"]*)"|'([^']*)'|([^\s"'>]+))/g;
    let match;
    while ((match = pattern.exec(raw)) !== null) {
        const name = match[1].toLowerCase();
        if (name.startsWith('on')) {
            continue;
        }

        attributes[name] = decodeEntities(match[3] ?? match[4] ?? match[5] ?? '');
    }

    return attributes;
}

function parseStyle(raw) {
    const result = {};
    for (const declaration of String(raw || '').split(';')) {
        const [name, value] = declaration.split(':').map(part => String(part || '').trim());
        const normalized = value && sanitizeCssValue(value);
        if (!name || !normalized) {
            continue;
        }

        const key = name.toLowerCase();
        if (key === 'font-weight' && (/bold/i.test(normalized) || Number(normalized) >= 600)) result.bold = true;
        if (key === 'font-style' && /italic/i.test(normalized)) result.italic = true;
        if (key === 'text-decoration' && /underline/i.test(normalized)) result.underline = true;
        if (key === 'color') result.color = normalized;
        if (key === 'background-color') result.highlight = normalized;
    }

    return result;
}

function normalizeBlock(block, index) {
    if (!block || typeof block !== 'object') {
        return null;
    }

    if (block.type === 'table' || block.type === 'image') {
        return clone(block);
    }

    const type = ['paragraph', 'heading', 'list', 'quote'].includes(block.type) ? block.type : 'paragraph';
    const copy = createTextBlock(type, '', index);
    copy.id = String(block.id || copy.id);
    copy.sectionId = block.sectionId ?? null;
    copy.order = Number(block.order || (index + 1) * 10) || (index + 1) * 10;
    copy.paragraphProperties = block.paragraphProperties && typeof block.paragraphProperties === 'object' ? clone(block.paragraphProperties) : {};
    copy.content = { ...copy.content, ...(block.content && typeof block.content === 'object' ? clone(block.content) : {}) };
    copy.content.type = type;
    copy.content.runs = Array.isArray(block.content?.runs)
        ? compactRuns(block.content.runs.map((run, runIndex) => normalizeRun(run, `${copy.id}-run-${runIndex + 1}`)), copy.id)
        : [createTextRun('', [], `${copy.id}-empty-run`)];
    return copy;
}

function normalizeRun(run, fallbackId) {
    if (String(run?.type || '').toLowerCase() === 'drawing' && run?.drawing && typeof run.drawing === 'object') {
        return {
            ...clone(run),
            id: String(run?.id || fallbackId),
            type: 'drawing',
            text: '',
            marks: Array.isArray(run?.marks) ? run.marks.map(mark => ({ ...clone(mark), type: String(mark?.type || '') })) : [],
            drawing: clone(run.drawing),
            preserve: run?.preserve && typeof run.preserve === 'object' ? clone(run.preserve) : {},
        };
    }

    return {
        id: String(run?.id || fallbackId),
        type: 'text',
        text: String(run?.text || ''),
        marks: Array.isArray(run?.marks) ? run.marks.map(mark => ({ ...clone(mark), type: String(mark?.type || '') })) : [],
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        preserve: {},
    };
}

function runToHtml(run) {
    if (String(run?.type || '').toLowerCase() === 'drawing' && run?.drawing) {
        const label = drawingPlainText(run);
        return `<span data-tempo-drawing="true">${escapeHtml(label)}</span>`;
    }

    let value = escapeHtml(run?.text || '');
    for (const mark of run?.marks || []) {
        if (mark.type === 'bold') value = `<strong>${value}</strong>`;
        if (mark.type === 'italic') value = `<em>${value}</em>`;
        if (mark.type === 'underline') value = `<u>${value}</u>`;
        if (mark.type === 'textColor' && mark.value) value = `<span style="color:${escapeHtml(mark.value)}">${value}</span>`;
        if (mark.type === 'highlight' && mark.value) value = `<span style="background-color:${escapeHtml(mark.value)}">${value}</span>`;
        if (mark.type === 'link') {
            const href = sanitizeUrl(mark.link?.href || mark.value);
            if (href) value = `<a href="${escapeHtml(href)}">${value}</a>`;
        }
    }

    return value;
}

function compactRuns(runs, blockId) {
    const compacted = [];
    for (const run of runs.filter(Boolean)) {
        if (String(run?.type || '').toLowerCase() === 'drawing' && run.drawing) {
            compacted.push(clone(run));
            continue;
        }

        if (!run.text) {
            continue;
        }

        const previous = compacted.at(-1);
        if (previous && JSON.stringify(previous.marks || []) === JSON.stringify(run.marks || [])) {
            previous.text += run.text;
        } else {
            compacted.push(clone(run));
        }
    }

    return compacted.length > 0 ? compacted : [createTextRun('', [], `${blockId}-empty-run`)];
}

function createFragment(input) {
    const fragment = normalizeFragment({
        schemaVersion: 1,
        source: input.source,
        blocks: input.blocks,
        rawHtml: input.rawHtml,
        warnings: [],
    });
    fragment.html = fragmentToHtml(fragment);
    return fragment;
}

function detectHtmlSource(html) {
    const value = String(html || '');
    if (/urn:schemas-microsoft-com:office|mso-|class="?Mso/i.test(value)) return 'word';
    if (/docs-internal-guid|id="?docs-internal-guid|googleusercontent/i.test(value)) return 'googleDocs';
    if (/google-sheets-html-origin|docs-internal-guid/i.test(value) && /<table/i.test(value)) return 'googleSheets';
    return 'html';
}

function blockText(block) {
    if (block.type === 'table') {
        return (block.content?.table?.rows || [])
            .map(row => (row.cells || []).map(cell => (cell.blocks || []).map(blockText).join('\n')).join('\t'))
            .join('\n');
    }

    return (block.content?.runs || []).map(run =>
        String(run?.type || '').toLowerCase() === 'drawing'
            ? drawingPlainText(run)
            : String(run?.text || '')).join('');
}

function drawingPlainText(run) {
    const drawing = run?.drawing || {};
    const textBody = drawing.textBody || drawing.TextBody || {};
    const paragraphs = Array.isArray(textBody.paragraphs ?? textBody.Paragraphs)
        ? (textBody.paragraphs ?? textBody.Paragraphs)
        : [];
    const text = paragraphs.map(paragraph => String(paragraph?.text ?? paragraph?.Text ?? '')).filter(Boolean).join(' ');
    if (text) {
        return text;
    }

    return String(drawing.altText ?? drawing.AltText ?? drawing.caption ?? drawing.Caption ?? drawing.objectId ?? drawing.ObjectId ?? 'Drawing object');
}

function stripUnsafeContent(html) {
    return String(html || '').replace(UNSAFE_CONTENT, '').replace(/<!--[\s\S]*?-->/g, '');
}

function stripTags(html) {
    return decodeEntities(stripUnsafeContent(html).replace(/<br\s*\/?>/gi, '\n').replace(/<[^>]*>/g, ' '));
}

function normalizeWhitespace(value) {
    return decodeEntities(String(value || '')).replace(/\s+/g, ' ').trim();
}

function decodeEntities(value) {
    return String(value || '')
        .replace(/&nbsp;/gi, ' ')
        .replace(/&lt;/gi, '<')
        .replace(/&gt;/gi, '>')
        .replace(/&quot;/gi, '"')
        .replace(/&#39;/gi, "'")
        .replace(/&amp;/gi, '&');
}

function escapeHtml(value) {
    return String(value || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function sanitizeCssValue(value) {
    const text = String(value || '').trim();
    return /expression|javascript:|url\s*\(/i.test(text) ? '' : text.replace(/[<>"']/g, '');
}

function findLastIndex(items, predicate) {
    for (let index = items.length - 1; index >= 0; index -= 1) {
        if (predicate(items[index])) {
            return index;
        }
    }

    return -1;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
