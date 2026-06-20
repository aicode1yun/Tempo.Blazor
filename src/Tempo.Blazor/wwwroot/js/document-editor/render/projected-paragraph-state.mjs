// Phase D — render/projected-paragraph-state.mjs
// `restoreWysiwygProjectedParagraph(paragraph)` — reverts a paragraph element that
//   was projected for text-exclusion reflow back to its original HTML + clears the
//   projection class/attributes/inline styles. Returns false when the element was
//   not projected (so callers can no-op).
// `createShouldProjectWysiwygParagraph({blockText})` →
//   `shouldProjectWysiwygParagraph(paragraph, block)` — true only for non-empty
//   paragraph blocks whose DOM has no marker/comment/revision inline children (those
//   can't be safely re-projected).

export function restoreWysiwygProjectedParagraph(paragraph) {
    if (!paragraph || paragraph.getAttribute('data-wysiwyg-projected-layout') !== 'true') {
        return false;
    }
    if (paragraph.__tmWysiwygOriginalHtml !== undefined) {
        paragraph.innerHTML = paragraph.__tmWysiwygOriginalHtml;
    }
    paragraph.classList.remove('tm-wysiwyg-block--projected-layout');
    paragraph.removeAttribute('data-wysiwyg-projected-layout');
    paragraph.removeAttribute('data-wysiwyg-layout-signature');
    paragraph.style.position = '';
    paragraph.style.minHeight = '';
    paragraph.style.height = '';
    paragraph.style.whiteSpace = '';
    return true;
}

export function createShouldProjectWysiwygParagraph(options) {
    const opts = options || {};
    if (typeof opts.blockText !== 'function') {
        throw new TypeError(
            'createShouldProjectWysiwygParagraph requires options.blockText (function)');
    }
    const { blockText } = opts;
    return function shouldProjectWysiwygParagraph(paragraph, block) {
        if (!paragraph || !block || block.type !== 'paragraph') return false;
        if (!blockText(block).trim()) return false;
        if (paragraph.querySelector(
            '.tm-wysiwyg-marker, .tm-document-inline--comment-anchor, .tm-document-inline--revision')) {
            return false;
        }
        return true;
    };
}
