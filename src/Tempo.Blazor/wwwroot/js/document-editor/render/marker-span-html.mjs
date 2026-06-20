// Phase D — render/marker-span-html.mjs
// HTML span builders for inline markers (comment / revision / search). All three
// emit `aria-current` plus marker `data-testid`/`data-marker-id` attributes the
// editor's render tests + assistive tech rely on.
//
// `createRenderCommentSpanHtml({escapeHtml, asText})` →
//   `renderCommentSpanHtml(inst, commentId, text, status?, innerHtml?)`
//   • status `'resolved'` adds the `--resolved` modifier; active comment id (from
//     `inst.activeCommentId`) toggles `--selected` / `--comment-active`.
// `createRenderRevisionSpanHtml({revisionById, readRevisionMarkerType, escapeHtml,
//   asText})` → `renderRevisionSpanHtml(inst, revisionId, text, marker?, innerHtml?)`
//   • emits an inner `<span data-testid="document-wysiwyg-revision-<kind>">` so
//     legacy tests can still find the marker; `kind` falls out of
//     `marker.type || readRevisionMarkerType(revision)` (insert/delete/format).
// `createRenderSearchSpanHtml({escapeHtml, asText})` →
//   `renderSearchSpanHtml(inst, marker, text, innerHtml?)` for search hit
//   highlights; `marker.active` toggles `--active` modifiers.

function commonDeps(opts, label, names) {
    for (const key of names) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`${label} requires options.${key} (function)`);
        }
    }
}

export function createRenderCommentSpanHtml(options) {
    const opts = options || {};
    commonDeps(opts, 'createRenderCommentSpanHtml', ['escapeHtml', 'asText']);
    const { escapeHtml, asText } = opts;

    return function renderCommentSpanHtml(inst, commentId, text, status, innerHtml) {
        const id = asText(commentId);
        const active = id && inst && inst.activeCommentId === id;
        const classes = [
            'tm-document-inline',
            'tm-document-inline--comment-anchor',
            'tm-wysiwyg-marker',
            'tm-wysiwyg-marker--comment',
        ];
        if (status === 'resolved') {
            classes.push('tm-document-inline--comment-anchor--resolved');
        }
        if (active) {
            classes.push(
                'tm-document-inline--comment-anchor--selected',
                'tm-wysiwyg-marker--comment-active');
        }
        const content = innerHtml !== undefined ? innerHtml : escapeHtml(text);
        return '<span class="' + classes.join(' ')
            + '" data-testid="document-comment-marker"'
            + ' data-comment-id="' + escapeHtml(id) + '"'
            + ' data-marker-id="comment:' + escapeHtml(id) + '"'
            + ' data-comment-status="' + escapeHtml(status || 'open') + '"'
            + ' aria-current="' + (active ? 'true' : 'false') + '">'
            + content + '</span>';
    };
}

export function createRenderRevisionSpanHtml(options) {
    const opts = options || {};
    commonDeps(opts, 'createRenderRevisionSpanHtml',
        ['revisionById', 'readRevisionMarkerType', 'escapeHtml', 'asText']);
    const { revisionById, readRevisionMarkerType, escapeHtml, asText } = opts;

    return function renderRevisionSpanHtml(inst, revisionId, text, marker, innerHtml) {
        const id = asText(revisionId);
        const revision = revisionById(inst && inst.model, id);
        const markerType = (marker && marker.type) || readRevisionMarkerType(revision);
        const typeClass = markerType === 'revisionDeletion'
            ? 'delete'
            : (markerType === 'revisionFormat' ? 'format' : 'insert');
        const active = id && inst && inst.activeRevisionId === id;
        const classes = [
            'tm-document-inline',
            'tm-document-inline--revision',
            'tm-document-inline--revision-' + typeClass,
            'tm-wysiwyg-marker',
            'tm-wysiwyg-marker--revision',
            'tm-wysiwyg-marker--' + markerType,
            'tm-wysiwyg-revision',
            'tm-wysiwyg-revision--' + typeClass,
        ];
        if (active) {
            classes.push('tm-wysiwyg-revision--selected', 'tm-wysiwyg-marker--revision-active');
        }
        const contentHtml = innerHtml !== undefined ? innerHtml : escapeHtml(text);
        const legacyTestId = typeClass === 'delete'
            ? 'document-wysiwyg-revision-delete'
            : (typeClass === 'format'
                ? 'document-wysiwyg-revision-format'
                : 'document-wysiwyg-revision-insert');
        return '<span class="' + classes.join(' ')
            + '" data-testid="document-revision-marker"'
            + ' data-revision-id="' + escapeHtml(id) + '"'
            + ' data-marker-id="revision:' + escapeHtml(id) + '"'
            + ' data-revision-type="' + escapeHtml(markerType) + '"'
            + ' aria-current="' + (active ? 'true' : 'false') + '">'
            + '<span data-testid="' + legacyTestId
            + '" data-revision-id="' + escapeHtml(id) + '">'
            + contentHtml + '</span></span>';
    };
}

export function createRenderSearchSpanHtml(options) {
    const opts = options || {};
    commonDeps(opts, 'createRenderSearchSpanHtml', ['escapeHtml', 'asText']);
    const { escapeHtml, asText } = opts;

    return function renderSearchSpanHtml(inst, marker, text, innerHtml) {
        const id = asText((marker && (marker.id || marker.Id
            || marker.targetId || marker.TargetId)) || 'search');
        const active = !!(marker && (marker.active === true || marker.Active === true));
        const classes = [
            'tm-wysiwyg-marker',
            'tm-wysiwyg-marker--search',
            'tm-wysiwyg-search-match',
        ];
        if (active) {
            classes.push('tm-wysiwyg-marker--search-active', 'tm-wysiwyg-search-match--active');
        }
        const content = innerHtml !== undefined ? innerHtml : escapeHtml(text);
        return '<span class="' + classes.join(' ')
            + '" data-testid="document-search-marker"'
            + ' data-marker-id="search:' + escapeHtml(id) + '"'
            + ' data-search-marker-id="' + escapeHtml(id) + '"'
            + ' data-marker-kind="search"'
            + ' aria-current="' + (active ? 'true' : 'false') + '">'
            + content + '</span>';
    };
}
