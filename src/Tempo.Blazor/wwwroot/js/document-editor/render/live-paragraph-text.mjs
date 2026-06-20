// Phase D — render/live-paragraph-text.mjs
// `createSetLiveParagraphText({document, asText})` → `setLiveParagraphText(node, text)`
// — fast-path text writer for the live-typing DOM patch. Sets the node's text
// content directly; when the text is empty it inserts a `<br data-caret-placeholder>`
// so the paragraph keeps a caret landing spot. Returns false for a null node,
// true otherwise.

export function createSetLiveParagraphText(options) {
    const opts = options || {};
    if (!opts.document || typeof opts.document.createElement !== 'function') {
        throw new TypeError(
            'createSetLiveParagraphText requires options.document (with createElement)');
    }
    if (typeof opts.asText !== 'function') {
        throw new TypeError(
            'createSetLiveParagraphText requires options.asText (function)');
    }
    const { document: doc, asText } = opts;

    return function setLiveParagraphText(node, text) {
        if (!node) return false;
        const value = asText(text);
        if (value.length === 0) {
            const placeholder = doc.createElement('br');
            placeholder.setAttribute('data-caret-placeholder', 'true');
            node.replaceChildren(placeholder);
        } else {
            node.textContent = value;
        }
        return true;
    };
}
