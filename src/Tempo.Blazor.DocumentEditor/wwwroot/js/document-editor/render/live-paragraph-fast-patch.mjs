// Phase D — render/live-paragraph-fast-patch.mjs
// Gatekeepers for the live-typing DOM fast path. When a paragraph contains only
// plain text (no marks/comments/revisions/fields/inline objects) and the live DOM
// node has no projected/marker children, the runtime can patch the text node in
// place instead of re-rendering the whole paragraph.
//
// `liveTextNodeCanUseFastPatch(node)` — pure DOM predicate. False when the node is
//   a projected-layout container or contains any marker/segment/inline element that
//   the fast path cannot safely preserve.
// `createTextBlockHasOnlyPlainTextRuns({isEditableTextBlock, isDrawingRunSource,
//   asArray, asText, readCommentIdsFromRun, readRevisionIdsFromRun})` →
//   `textBlockHasOnlyPlainTextRuns(block)` — true when every run is plain text with
//   no marks/comments/revisions/field type/key.

const FAST_PATCH_BLOCKING_SELECTOR = [
    '.tm-wysiwyg-layout-segment--projected',
    '.tm-wysiwyg-marker',
    '.tm-document-inline--comment-anchor',
    '.tm-document-inline--revision',
    '.tm-wysiwyg-revision',
    '.tm-wysiwyg-inline-drawing',
    '.tm-wysiwyg-drawing-anchor',
    '[data-inline-id]',
    '[data-node-id]',
    '[data-inline-break]',
    '[data-caret-placeholder]',
].join(',');

export function liveTextNodeCanUseFastPatch(node) {
    if (!node
        || (node.getAttribute
            && node.getAttribute('data-wysiwyg-projected-layout') === 'true')) {
        return false;
    }
    if (node.querySelector && node.querySelector(FAST_PATCH_BLOCKING_SELECTOR)) {
        return false;
    }
    return true;
}

export function createTextBlockHasOnlyPlainTextRuns(options) {
    const opts = options || {};
    for (const key of ['isEditableTextBlock', 'isDrawingRunSource', 'asArray',
        'asText', 'readCommentIdsFromRun', 'readRevisionIdsFromRun']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createTextBlockHasOnlyPlainTextRuns requires options.${key} (function)`);
        }
    }
    const {
        isEditableTextBlock,
        isDrawingRunSource,
        asArray,
        asText,
        readCommentIdsFromRun,
        readRevisionIdsFromRun,
    } = opts;

    return function textBlockHasOnlyPlainTextRuns(block) {
        if (!isEditableTextBlock(block)) return false;
        return asArray(block.content && block.content.runs).every(function (run) {
            return !!run
                && (run.kind === 'text' || !run.kind)
                && !isDrawingRunSource(run)
                && asArray(run.marks || run.Marks).length === 0
                && readCommentIdsFromRun(run).length === 0
                && readRevisionIdsFromRun(run).length === 0
                && !asText(run.fieldType || run.FieldType)
                && !asText(run.key || run.Key);
        });
    };
}
