// Phase D — render/projected-wysiwyg-line.mjs
// `groupProjectedWysiwygSegmentsByLine(segments)` — buckets projected segments into
//   lines by their rect's y (within 20% of line height), each line's segments
//   sorted left→right and lines sorted top→bottom.
// `createProjectedWysiwygLineRenderer({document, asArray, applySegmentStyleToElement})`
//   → `{renderProjectedWysiwygLine, renderProjectedWysiwygSegment}` — DOM builders
//   for a projected (text-exclusion-reflowed) paragraph line and its segments. The
//   line span absolutely positions itself at `(y - paragraphTop)`, segments flow
//   with `margin-left` gaps to honour their measured x positions.

import { asArray } from '../core/helpers.mjs';

export function groupProjectedWysiwygSegmentsByLine(segments) {
    const lines = [];
    asArray(segments).forEach(function (segment) {
        const rect = (segment && segment.rect) || {};
        const y = Number(rect.y || 0) || 0;
        const height = Math.max(1, Number(rect.height || 0) || 1);
        let line = lines.find(function (candidate) {
            return Math.abs(Number(candidate.y || 0) - y) <= Math.max(1, height * 0.2);
        });
        if (!line) {
            line = { y, height, segments: [] };
            lines.push(line);
        }
        line.height = Math.max(line.height, height);
        line.segments.push(segment);
    });
    lines.forEach(function (line) {
        line.segments.sort(function (a, b) {
            const ar = (a && a.rect) || {};
            const br = (b && b.rect) || {};
            return Number(ar.x || 0) - Number(br.x || 0);
        });
    });
    return lines.sort(function (a, b) { return Number(a.y || 0) - Number(b.y || 0); });
}

export function createProjectedWysiwygLineRenderer(options) {
    const opts = options || {};
    if (!opts.document || typeof opts.document.createElement !== 'function') {
        throw new TypeError(
            'createProjectedWysiwygLineRenderer requires options.document (with createElement)');
    }
    for (const key of ['asArray', 'applySegmentStyleToElement']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createProjectedWysiwygLineRenderer requires options.${key} (function)`);
        }
    }
    const { document: doc, asArray: arr, applySegmentStyleToElement } = opts;

    function renderProjectedWysiwygSegment(segment, fallbackLineHeight) {
        const span = doc.createElement('span');
        const rect = (segment && segment.rect) || {};
        span.className = 'tm-wysiwyg-layout-segment tm-wysiwyg-layout-segment--projected';
        span.setAttribute('data-layout-segment-id', segment.id || '');
        span.setAttribute('data-model-block-id', segment.blockId || '');
        span.setAttribute('data-model-run-id', segment.runId || '');
        span.setAttribute('data-model-start', segment.start ?? 0);
        span.setAttribute('data-model-end', segment.end ?? segment.start ?? 0);
        span.setAttribute('data-layout-height',
            Math.max(1, Number(rect.height || fallbackLineHeight) || fallbackLineHeight));
        if (segment.runId) span.setAttribute('data-inline-id', segment.runId);
        span.style.position = 'relative';
        span.style.width = Math.max(1, Math.round(Number(rect.width || 1) * 100) / 100) + 'px';
        span.style.height = Math.max(1,
            Math.round(Number(rect.height || fallbackLineHeight) * 100) / 100) + 'px';
        span.style.lineHeight = Math.max(1,
            Number(rect.height || fallbackLineHeight) || fallbackLineHeight) + 'px';
        span.style.whiteSpace = 'pre';
        span.style.overflow = 'visible';
        span.style.display = 'inline-block';
        applySegmentStyleToElement(span, segment.style || {}, segment.decorations || []);
        span.appendChild(doc.createTextNode(segment.text || ''));
        return span;
    }

    function renderProjectedWysiwygLine(line, paragraphTop, bodyWidth, fallbackLineHeight) {
        const span = doc.createElement('span');
        const y = Number((line && line.y) || paragraphTop) || paragraphTop;
        const height = Math.max(1,
            Number((line && line.height) || fallbackLineHeight) || fallbackLineHeight);
        span.className = 'tm-wysiwyg-layout-text-line';
        span.style.position = 'absolute';
        span.style.left = '0px';
        span.style.top = Math.round((y - paragraphTop) * 100) / 100 + 'px';
        span.style.width = Math.max(1, Number(bodyWidth || 1) || 1) + 'px';
        span.style.height = height + 'px';
        span.style.lineHeight = height + 'px';
        span.style.whiteSpace = 'pre';
        span.style.overflow = 'visible';
        span.style.display = 'block';
        let cursorX = 0;
        arr(line && line.segments).forEach(function (segment) {
            const rect = (segment && segment.rect) || {};
            const x = Math.max(0, Number(rect.x || 0) || 0);
            const width = Math.max(1, Number(rect.width || 1) || 1);
            const fragment = renderProjectedWysiwygSegment(segment, fallbackLineHeight);
            const gap = Math.max(0, x - cursorX);
            fragment.style.marginLeft = Math.round(gap * 100) / 100 + 'px';
            fragment.style.width = Math.round(width * 100) / 100 + 'px';
            span.appendChild(fragment);
            cursorX = x + width;
        });
        return span;
    }

    return Object.freeze({ renderProjectedWysiwygLine, renderProjectedWysiwygSegment });
}
