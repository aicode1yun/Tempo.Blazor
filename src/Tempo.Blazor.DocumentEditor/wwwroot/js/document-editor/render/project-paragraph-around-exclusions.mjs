// Phase D — render/project-paragraph-around-exclusions.mjs
// `createProjectWysiwygParagraphAroundExclusions(deps)` →
//   `projectWysiwygParagraphAroundExclusions(paragraph, block, paragraphTop,
//   bodyWidth, allExclusions, relevantExclusions)` — the WYSIWYG projection
//   orchestrator. Lays the paragraph out via the paragraph engine resolving
//   available intervals around `allExclusions`, tokenizes + reflows the resulting
//   segments, computes the projected paragraph height (extending past overlapping
//   `relevantExclusions`), and — when the projection signature changed — rewrites
//   the paragraph DOM with absolutely-positioned line markers + projected line
//   spans. Returns true when the DOM was rewritten, false when unchanged, and
//   restores the original paragraph when there is nothing to project.
//
// Deps: window?, document, asArray, clone, createParagraphLayoutEngine,
// createTextMeasurementService, getAvailableIntervals,
// splitProjectedWysiwygSegmentsForReflow, reflowProjectedWysiwygSegments,
// restoreWysiwygProjectedParagraph, createWysiwygParagraphProjectionSignature,
// groupProjectedWysiwygSegmentsByLine, renderProjectedWysiwygLine.

const REQUIRED_FNS = [
    'asArray', 'clone', 'createParagraphLayoutEngine', 'createTextMeasurementService',
    'getAvailableIntervals', 'splitProjectedWysiwygSegmentsForReflow',
    'reflowProjectedWysiwygSegments', 'restoreWysiwygProjectedParagraph',
    'createWysiwygParagraphProjectionSignature', 'groupProjectedWysiwygSegmentsByLine',
    'renderProjectedWysiwygLine',
];

export function createProjectWysiwygParagraphAroundExclusions(options) {
    const opts = options || {};
    if (!opts.document || typeof opts.document.createElement !== 'function') {
        throw new TypeError(
            'createProjectWysiwygParagraphAroundExclusions requires options.document');
    }
    for (const key of REQUIRED_FNS) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createProjectWysiwygParagraphAroundExclusions requires options.${key} (function)`);
        }
    }
    const {
        document: doc, asArray, clone, createParagraphLayoutEngine,
        createTextMeasurementService, getAvailableIntervals,
        splitProjectedWysiwygSegmentsForReflow, reflowProjectedWysiwygSegments,
        restoreWysiwygProjectedParagraph, createWysiwygParagraphProjectionSignature,
        groupProjectedWysiwygSegmentsByLine, renderProjectedWysiwygLine,
    } = opts;
    const win = opts.window
        || (typeof window !== 'undefined' ? window : null);

    return function projectWysiwygParagraphAroundExclusions(
        paragraph, block, paragraphTop, bodyWidth, allExclusions, relevantExclusions) {
        const style = win && win.getComputedStyle ? win.getComputedStyle(paragraph) : null;
        const fontSize = style ? (parseFloat(style.fontSize) || 16) : 16;
        const lineHeight = style
            ? (parseFloat(style.lineHeight) || Math.ceil(fontSize * 1.25))
            : Math.ceil(fontSize * 1.25);
        const paragraphInput = clone(block);
        paragraphInput.style = Object.assign({},
            paragraphInput.style || {},
            (paragraphInput.content && paragraphInput.content.style) || {},
            {
                fontFamily: (style && style.fontFamily) || 'Arial',
                fontSize,
                lineHeight,
                fontWeight: (style && style.fontWeight) || '400',
                fontStyle: (style && style.fontStyle) || 'normal',
            });
        const frame = { x: 0, y: 0, width: bodyWidth, height: Math.max(200000, paragraphTop + 2000) };
        const engine = createParagraphLayoutEngine(createTextMeasurementService(),
            { lineGap: 0, minReadableWidth: 36 });
        const layout = engine.layoutParagraph(paragraphInput, {
            x: 0,
            y: paragraphTop,
            width: bodyWidth,
            lineGap: 0,
            minReadableWidth: 36,
            resolveAvailableIntervals(atY, height, minWidth) {
                return getAvailableIntervals(atY, height, frame, allExclusions, minWidth || 36,
                    { pageIndex: 0, region: 'Body' });
            },
        });
        let segments = asArray(layout && layout.segments).filter(function (segment) {
            return segment && segment.inlineObject !== true && segment.text !== undefined;
        });
        if (!segments.length) return restoreWysiwygProjectedParagraph(paragraph);
        segments = splitProjectedWysiwygSegmentsForReflow(segments, paragraphInput.style);
        const projected = reflowProjectedWysiwygSegments(
            segments, paragraphTop, lineHeight, bodyWidth, frame, allExclusions);
        segments = projected.segments;
        const lines = projected.lines.length ? projected.lines : asArray(layout && layout.lines);
        if (!segments.length) return restoreWysiwygProjectedParagraph(paragraph);
        let layoutBottom = Math.max.apply(null, lines.map(function (line) {
            return Number((line && line.rect && line.rect.y) || paragraphTop)
                + Number((line && line.rect && line.rect.height) || lineHeight);
        }).concat(segments.map(function (segment) {
            return Number((segment.rect && segment.rect.y) || paragraphTop)
                + Number((segment.rect && segment.rect.height) || lineHeight);
        })));
        asArray(relevantExclusions).forEach(function (exclusion) {
            const rect = (exclusion && exclusion.rect) || {};
            if (Number(rect.y || 0) < layoutBottom
                && Number(rect.y || 0) + Number(rect.height || 0) > paragraphTop) {
                layoutBottom = Math.max(layoutBottom, Number(rect.y || 0) + Number(rect.height || 0));
            }
        });
        const paragraphHeight = Math.max(lineHeight, layoutBottom - paragraphTop);
        const signature = createWysiwygParagraphProjectionSignature(
            segments, paragraphHeight, paragraphTop);
        if (paragraph.getAttribute('data-wysiwyg-layout-signature') === signature) return false;
        if (paragraph.__tmWysiwygOriginalHtml === undefined
            && paragraph.getAttribute('data-wysiwyg-projected-layout') !== 'true') {
            paragraph.__tmWysiwygOriginalHtml = paragraph.innerHTML;
        }
        paragraph.classList.add('tm-wysiwyg-block--projected-layout');
        paragraph.setAttribute('data-wysiwyg-projected-layout', 'true');
        paragraph.setAttribute('data-wysiwyg-layout-signature', signature);
        paragraph.style.position = 'relative';
        paragraph.style.minHeight = Math.ceil(paragraphHeight) + 'px';
        paragraph.style.height = Math.ceil(paragraphHeight) + 'px';
        paragraph.style.whiteSpace = 'normal';
        paragraph.replaceChildren();
        lines.forEach(function (line) {
            const marker = doc.createElement('span');
            marker.className = 'tm-wysiwyg-layout-line';
            marker.setAttribute('data-layout-line-id', line.id || '');
            marker.setAttribute('aria-hidden', 'true');
            marker.style.left = Math.round(Number((line.rect && line.rect.x) || 0) * 100) / 100 + 'px';
            marker.style.top = Math.round(
                (Number((line.rect && line.rect.y) || paragraphTop) - paragraphTop) * 100) / 100 + 'px';
            marker.style.width = Math.max(1,
                Number((line.rect && line.rect.width) || bodyWidth) || bodyWidth) + 'px';
            marker.style.height = Math.max(1,
                Number((line.rect && line.rect.height) || lineHeight) || lineHeight) + 'px';
            marker.style.pointerEvents = 'none';
            paragraph.appendChild(marker);
        });
        groupProjectedWysiwygSegmentsByLine(segments).forEach(function (line) {
            paragraph.appendChild(renderProjectedWysiwygLine(line, paragraphTop, bodyWidth, lineHeight));
        });
        return true;
    };
}
