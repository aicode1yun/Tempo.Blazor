// Phase R.5.2 — core-engine/clipboard.mjs
// Rich copy / cut / paste for the model-owned engine. The off-screen input surface stays
// empty, so the browser's native copy/paste of the textarea is useless — instead we
// serialize the selected MODEL range to the clipboard (plain + HTML + an internal
// fragment) and parse pasted content back into model "lines" (one entry per paragraph,
// each an array of `{ text, marks }` runs). Internal paste is lossless for formatting;
// external HTML (Word / Google Docs) is mapped best-effort; plain text splits on newlines.
//
//   serializeRange(model, orderedRange) → { text, html, internal, lines }
//   parseClipboard(getData, opts?, doc?) → lines        (getData(mime) → string)
//   parsePlainText(text) / parseHtml(html, doc) → lines
//   INTERNAL_MIME — the custom clipboard type carrying the internal fragment.

export const INTERNAL_MIME = 'application/x-tempo-doc';

const BLOCK_TAGS = new Set(['p', 'div', 'li', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'blockquote', 'tr', 'section', 'article']);

function escapeHtml(s) {
    return String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function topLevelBlocks(model) { return (model && model.body && Array.isArray(model.body.blocks)) ? model.body.blocks : []; }
function blockIndex(model, id) { return topLevelBlocks(model).findIndex(function (b) { return b && b.id === id; }); }
function runText(run) { return run && typeof run.text === 'string' ? run.text : ''; }
function cloneMarks(marks) { return Array.isArray(marks) ? marks.map(function (m) { return Object.assign({}, m); }) : []; }

function blockTextLen(block) {
    const runs = (block && block.content && Array.isArray(block.content.runs)) ? block.content.runs : [];
    let n = 0;
    for (let i = 0; i < runs.length; i++) n += runText(runs[i]).length;
    return n;
}

// Text runs (with marks) overlapping [s, e) of a paragraph block.
function runsInRange(block, s, e) {
    const out = [];
    const runs = (block && block.content && Array.isArray(block.content.runs)) ? block.content.runs : [];
    let off = 0;
    for (let i = 0; i < runs.length; i++) {
        const t = runText(runs[i]);
        const len = t.length;
        if (len === 0) continue;
        const rs = Math.max(s, off), re = Math.min(e, off + len);
        if (re > rs) out.push({ text: t.slice(rs - off, re - off), marks: cloneMarks(runs[i].marks) });
        off += len;
    }
    return out;
}

// Orders a {anchor, focus} selection into {start, end} in document order.
export function orderRange(model, range) {
    const a = range.anchor, f = range.focus;
    const ai = blockIndex(model, a.blockId), fi = blockIndex(model, f.blockId);
    if (ai < fi || (ai === fi && (a.offset || 0) <= (f.offset || 0))) return { start: a, end: f };
    return { start: f, end: a };
}

export function rangeIsCollapsed(range) {
    return !range || (range.anchor && range.focus && range.anchor.blockId === range.focus.blockId && (range.anchor.offset || 0) === (range.focus.offset || 0));
}

// model + orderedRange → array of lines, each an array of { text, marks }.
export function collectLines(model, ordered) {
    const blocks = topLevelBlocks(model);
    const si = blockIndex(model, ordered.start.blockId);
    const ei = blockIndex(model, ordered.end.blockId);
    if (si < 0 || ei < 0) return [];
    const lines = [];
    for (let i = si; i <= ei; i++) {
        const b = blocks[i];
        const len = blockTextLen(b);
        const s = i === si ? (ordered.start.offset || 0) : 0;
        const e = i === ei ? (ordered.end.offset || 0) : len;
        lines.push(runsInRange(b, s, e));
    }
    return lines;
}

function runToHtml(run) {
    let inner = escapeHtml(run.text);
    const styles = [];
    let href = null;
    (run.marks || []).forEach(function (m) {
        const type = String((m && m.type) || '').toLowerCase();
        if (type === 'bold') inner = '<strong>' + inner + '</strong>';
        else if (type === 'italic') inner = '<em>' + inner + '</em>';
        else if (type === 'underline') styles.push('text-decoration:underline');
        else if (type === 'strikethrough' || type === 'strike') styles.push('text-decoration:line-through');
        else if (type === 'highlight' && m.value) styles.push('background-color:' + m.value);
        else if (type === 'textcolor' && m.value) styles.push('color:' + m.value);
        else if (type === 'fontfamily' && m.value) styles.push('font-family:' + m.value);
        else if (type === 'fontsize' && m.value) styles.push('font-size:' + m.value);
        else if (type === 'link' && m.value) href = m.value;
    });
    if (styles.length) inner = '<span style="' + escapeHtml(styles.join(';')) + '">' + inner + '</span>';
    if (href) inner = '<a href="' + escapeHtml(href) + '">' + inner + '</a>';
    return inner;
}

export function linesToText(lines) {
    return lines.map(function (line) { return line.map(function (r) { return r.text; }).join(''); }).join('\n');
}

export function linesToHtml(lines) {
    return lines.map(function (line) { return '<p>' + line.map(runToHtml).join('') + '</p>'; }).join('');
}

export function serializeRange(model, ordered) {
    const lines = collectLines(model, ordered);
    return { lines: lines, text: linesToText(lines), html: linesToHtml(lines), internal: JSON.stringify({ v: 1, lines: lines }) };
}

export function parsePlainText(text) {
    return String(text == null ? '' : text).split(/\r\n|\r|\n/).map(function (line) {
        return line.length ? [{ text: line, marks: [] }] : [];
    });
}

function marksForElement(tag, el, parentMarks) {
    const marks = parentMarks.slice();
    function add(m) { if (!marks.some(function (x) { return x.type === m.type && x.value === m.value; })) marks.push(m); }
    if (tag === 'b' || tag === 'strong') add({ type: 'bold' });
    if (tag === 'i' || tag === 'em') add({ type: 'italic' });
    if (tag === 'u') add({ type: 'underline' });
    if (tag === 's' || tag === 'strike' || tag === 'del') add({ type: 'strikethrough' });
    if (tag === 'a') { const href = el.getAttribute && el.getAttribute('href'); if (href) add({ type: 'link', value: href }); }
    // Inline style (Word / Google Docs export as styled spans).
    const style = (el.getAttribute && el.getAttribute('style')) || '';
    if (/font-weight\s*:\s*(bold|[6-9]00)/i.test(style)) add({ type: 'bold' });
    if (/font-style\s*:\s*italic/i.test(style)) add({ type: 'italic' });
    if (/text-decoration[^;]*underline/i.test(style)) add({ type: 'underline' });
    if (/text-decoration[^;]*line-through/i.test(style)) add({ type: 'strikethrough' });
    const color = /(?:^|;)\s*color\s*:\s*([^;]+)/i.exec(style);
    if (color) add({ type: 'textcolor', value: color[1].trim() });
    return marks;
}

export function parseHtml(html, domDoc) {
    const d = domDoc || globalThis.document;
    if (!d || typeof d.createElement !== 'function') return parsePlainText(stripTags(html));
    const container = d.createElement('div');
    container.innerHTML = sanitizeHtml(String(html == null ? '' : html));

    const lines = [];
    let current = [];
    function flush() { lines.push(current); current = []; }
    function walk(node, marks) {
        const kids = node.childNodes ? Array.prototype.slice.call(node.childNodes) : [];
        for (let i = 0; i < kids.length; i++) {
            const child = kids[i];
            if (child.nodeType === 3) {
                const t = String(child.textContent || '').replace(/\s+/g, ' ');
                if (t && t !== ' ') current.push({ text: t, marks: marks.slice() });
            } else if (child.nodeType === 1) {
                const tag = String(child.tagName || '').toLowerCase();
                if (tag === 'br') { flush(); continue; }
                const childMarks = marksForElement(tag, child, marks);
                if (BLOCK_TAGS.has(tag)) {
                    if (current.length) flush();
                    walk(child, childMarks);
                    if (current.length) flush();
                } else {
                    walk(child, childMarks);
                }
            }
        }
    }
    walk(container, []);
    if (current.length) flush();

    const nonEmpty = lines.filter(function (l) { return l.length; });
    if (nonEmpty.length) return nonEmpty;
    const text = String(container.textContent || '').trim();
    return text ? [[{ text: text, marks: [] }]] : [];
}

function stripTags(html) { return String(html == null ? '' : html).replace(/<[^>]*>/g, ''); }

// Removes script/style/comment nodes (their text must never become content). Attribute-level
// sanitisation is unnecessary because we only ever read tagName / href / style and emit our
// own model — pasted HTML is never re-inserted into the DOM.
function sanitizeHtml(html) {
    return html
        .replace(/<!--[\s\S]*?-->/g, '')
        .replace(/<\s*(script|style)[^>]*>[\s\S]*?<\s*\/\s*\1\s*>/gi, '');
}

function safeGet(getData, mime) {
    try { const v = getData(mime); return v == null ? '' : String(v); } catch (e) { return ''; }
}

export function parseClipboard(getData, opts, domDoc) {
    const o = opts || {};
    if (!o.plain) {
        const internal = safeGet(getData, INTERNAL_MIME);
        if (internal) {
            try { const parsed = JSON.parse(internal); if (parsed && Array.isArray(parsed.lines)) return parsed.lines; } catch (e) { /* fall through */ }
        }
        const html = safeGet(getData, 'text/html');
        if (html) { const lines = parseHtml(html, domDoc); if (lines.length) return lines; }
    }
    const text = safeGet(getData, 'text/plain');
    return text ? parsePlainText(text) : [];
}
