import { mathToAccessibleText, normalizeMathRun } from '../math/math-model.mjs';

export function createAccessibilityMirror(options = {}) {
    const doc = options.document;
    if (!doc || typeof doc.createElement !== 'function') {
        throw new Error('CanvasDocumentEngine accessibility mirror requires a DOM-like document.');
    }

    const root = doc.createElement('div');
    root.className = 'tm-document-canvas-a11y-mirror';
    root.setAttribute('data-testid', 'document-canvas-a11y-mirror');
    root.setAttribute('id', options.id || 'document-canvas-a11y-mirror');
    root.setAttribute('role', 'document');
    if (options.ariaLabel) {
        root.setAttribute('aria-label', options.ariaLabel);
    }
    root.style.position = 'absolute';
    root.style.width = '1px';
    root.style.height = '1px';
    root.style.overflow = 'hidden';
    root.style.clipPath = 'inset(50%)';
    root.style.whiteSpace = 'nowrap';

    function update(model) {
        root.replaceChildren?.();
        if (!root.replaceChildren) {
            while (root.firstChild) {
                root.removeChild(root.firstChild);
            }
        }

        const blocks = readingOrderBlocks(model);
        root.setAttribute('data-canvas-a11y-block-count', String(blocks.length));
        root.setAttribute('data-canvas-a11y-comment-count', String(Array.isArray(model?.comments) ? model.comments.length : 0));
        root.setAttribute('data-canvas-a11y-revision-count', String(Array.isArray(model?.revisions) ? model.revisions.length : 0));

        for (const block of blocks) {
            root.appendChild(renderBlock(block));
        }
    }

    function renderBlock(block) {
        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'table') {
            return renderTable(block);
        }

        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'image') {
            return renderDrawingFigure(block?.content?.image || {}, block?.id || '');
        }

        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'contentcontrol') {
            const nestedBlocks = Array.isArray(block?.content?.contentControl?.blocks)
                ? block.content.contentControl.blocks
                : [];
            if (nestedBlocks.length > 0) {
                return renderContentControlGroup(block, nestedBlocks);
            }
        }

        const headingLevel = getHeadingLevel(block);
        const node = doc.createElement(headingLevel ? `h${headingLevel}` : 'p');
        node.setAttribute('data-block-id', block?.id || '');
        node.setAttribute('data-canvas-a11y-block-type', String(block?.type || block?.content?.type || 'paragraph').toLowerCase());
        if (headingLevel) {
            node.setAttribute('role', 'heading');
            node.setAttribute('aria-level', String(headingLevel));
        }

        applyDirection(node, block);
        applyBlockAnnotations(node, block);
        appendInlineContent(node, block);
        return node;
    }

    function renderContentControlGroup(block, nestedBlocks) {
        const group = doc.createElement('div');
        const control = block?.content?.contentControl?.control || {};
        group.setAttribute('data-block-id', block?.id || '');
        group.setAttribute('data-canvas-a11y-block-type', 'contentcontrol');
        group.setAttribute('data-canvas-a11y-content-control', 'true');
        group.setAttribute('data-control-id', String(control?.controlId || control?.id || ''));
        group.setAttribute('data-control-kind', String(control?.kind || '').toLowerCase());
        group.setAttribute('role', 'group');
        applyDirection(group, block);
        applyBlockAnnotations(group, block);
        for (const nestedBlock of nestedBlocks) {
            group.appendChild(renderBlock(nestedBlock));
        }

        return group;
    }

    function appendInlineContent(node, block) {
        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        for (const run of runs) {
            if (String(run?.type || '').toLowerCase() === 'drawing' && run.drawing) {
                node.appendChild(renderDrawingFigure(run.drawing, block?.id || '', run?.id || ''));
            } else if (String(run?.type || '').toLowerCase() === 'math' || run?.math) {
                node.appendChild(renderMathRun(run, block?.id || ''));
            } else if (String(run?.type || '').toLowerCase() === 'field' && run.field) {
                appendText(node, String(run.field.displayText ?? run.field.DisplayText ?? ''));
            } else {
                const text = run && run.text != null ? String(run.text) : '';
                const revision = findRevisionMark(run);
                const comment = findCommentMark(run);
                if (revision || comment || directionOf(run)) {
                    const span = doc.createElement('span');
                    if (run?.id) {
                        span.setAttribute('data-run-id', run.id);
                    }

                    if (revision) {
                        span.setAttribute('data-canvas-a11y-revision-id', revision.revisionId || '');
                        span.setAttribute('data-canvas-a11y-revision-kind', String(revision.value || revision.kind || '').toLowerCase());
                    }

                    if (comment) {
                        span.setAttribute('data-canvas-a11y-comment-id', comment.commentId || '');
                    }

                    applyDirection(span, run);
                    appendText(span, text);
                    node.appendChild(span);
                } else {
                    appendText(node, text);
                }
            }
        }
    }

    function renderMathRun(run, blockId) {
        const math = normalizeMathRun(run);
        const text = math.altText || mathToAccessibleText(math) || '□';
        const span = doc.createElement('span');
        span.setAttribute('data-block-id', blockId);
        if (run?.id) {
            span.setAttribute('data-run-id', run.id);
        }

        span.setAttribute('data-canvas-a11y-math', 'true');
        span.setAttribute('data-math-id', math.mathId || '');
        span.setAttribute('role', 'math');
        span.setAttribute('aria-label', text);
        span.textContent = text;
        return span;
    }

    function appendText(node, text) {
        if (typeof doc.createTextNode === 'function') {
            node.appendChild(doc.createTextNode(text));
            return;
        }

        node.textContent = `${node.textContent || ''}${text}`;
    }

    function renderDrawingFigure(image, blockId, runId = '') {
        const figure = doc.createElement('figure');
        figure.setAttribute('data-block-id', blockId);
        figure.setAttribute('data-run-id', runId);
        figure.setAttribute('data-canvas-a11y-image', 'true');
        figure.setAttribute('data-canvas-a11y-drawing', 'true');
        figure.setAttribute('data-drawing-kind', drawingKindName(image));
        figure.setAttribute('role', 'img');
        const img = doc.createElement('img');
        const url = String(image?.url ?? image?.Url ?? '');
        const altText = String(image?.altText ?? image?.AltText ?? '');
        const caption = String(image?.caption ?? image?.Caption ?? '');
        const decorative = image?.isDecorative === true || image?.IsDecorative === true;
        if (url) {
            img.setAttribute('src', url);
        }

        img.setAttribute('alt', decorative ? '' : altText);
        figure.setAttribute('aria-label', decorative ? '' : drawingAccessibleName(image, altText, caption));
        figure.setAttribute('data-canvas-a11y-alt-warning', String(!decorative && !altText.trim()));
        figure.appendChild(img);
        if (caption) {
            const captionNode = doc.createElement('figcaption');
            captionNode.textContent = caption;
            figure.appendChild(captionNode);
        }

        return figure;
    }

    function drawingAccessibleName(image, altText, caption) {
        const explicit = String(altText || '').trim();
        if (explicit) {
            return explicit;
        }

        const captionText = String(caption || '').trim();
        if (captionText) {
            return captionText;
        }

        const title = String(image?.name ?? image?.Name ?? image?.title ?? image?.Title ?? '').trim();
        if (title) {
            return title;
        }

        const kind = drawingKindName(image);
        return kind === 'image' ? 'Image' : `Drawing ${kind}`;
    }

    function drawingKindName(image) {
        const value = image?.kind ?? image?.Kind;
        if (typeof value === 'number') {
            return ['image', 'shape', 'textBox', 'line', 'connector', 'chart', 'group'][Math.max(0, Math.min(6, Math.trunc(value)))] || 'image';
        }

        const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
        if (normalized === 'textbox') return 'textBox';
        if (normalized === 'line') return 'line';
        if (normalized === 'connector') return 'connector';
        if (normalized === 'chart') return 'chart';
        if (normalized === 'group') return 'group';
        if (normalized === 'shape') return 'shape';
        return 'image';
    }

    function renderTable(block) {
        const table = doc.createElement('table');
        table.setAttribute('data-block-id', block?.id || '');
        table.setAttribute('data-canvas-a11y-table', 'true');
        table.setAttribute('role', 'table');
        applyDirection(table, block);
        applyBlockAnnotations(table, block);
        const body = doc.createElement('tbody');
        const rows = Array.isArray(block?.content?.table?.rows) ? block.content.table.rows : [];
        table.setAttribute('aria-rowcount', String(rows.length));
        const colCount = rows.reduce((max, row) => Math.max(max, Array.isArray(row?.cells) ? row.cells.length : 0), 0);
        table.setAttribute('aria-colcount', String(colCount));
        for (const row of rows) {
            const rowNode = doc.createElement('tr');
            rowNode.setAttribute('role', 'row');
            for (const cell of Array.isArray(row?.cells) ? row.cells : []) {
                const cellNode = doc.createElement(cell?.isHeader === true ? 'th' : 'td');
                cellNode.setAttribute('data-cell-id', cell?.id || '');
                cellNode.setAttribute('role', cell?.isHeader === true ? 'columnheader' : 'cell');
                const columnSpan = Math.max(1, Number(cell?.columnSpan || 1) || 1);
                const rowSpan = Math.max(1, Number(cell?.rowSpan || 1) || 1);
                if (columnSpan > 1) {
                    cellNode.setAttribute('colspan', String(columnSpan));
                }

                if (rowSpan > 1) {
                    cellNode.setAttribute('rowspan', String(rowSpan));
                }

                const blocks = Array.isArray(cell?.blocks) ? cell.blocks : [];
                for (const nestedBlock of blocks) {
                    cellNode.appendChild(renderBlock(nestedBlock));
                }

                if (blocks.length === 0) {
                    cellNode.textContent = '';
                }

                rowNode.appendChild(cellNode);
            }

            body.appendChild(rowNode);
        }

        table.appendChild(body);
        return table;
    }

    return {
        root,
        update,
    };
}

export function readingOrderBlocks(model) {
    const blocks = Array.isArray(model?.body?.blocks) ? model.body.blocks.slice() : [];
    return blocks.sort((a, b) => {
        const ao = Number(a?.order ?? a?.Order);
        const bo = Number(b?.order ?? b?.Order);
        if (Number.isFinite(ao) && Number.isFinite(bo) && ao !== bo) {
            return ao - bo;
        }

        return 0;
    });
}

function getHeadingLevel(block) {
    if (String(block?.type || block?.content?.type || '').toLowerCase() !== 'heading') {
        return 0;
    }

    const level = Number(block?.content?.headingLevel ?? block?.content?.level ?? block?.headingLevel ?? block?.level ?? 1);
    return Math.max(1, Math.min(6, Number.isFinite(level) ? Math.round(level) : 1));
}

function applyDirection(node, source) {
    const direction = directionOf(source);
    if (direction) {
        node.setAttribute('dir', direction);
    }
}

function directionOf(source) {
    const value = String(source?.direction ?? source?.Direction ?? source?.textDirection ?? source?.TextDirection ?? source?.content?.direction ?? '').toLowerCase();
    if (value === 'rtl' || value === 'ltr' || value === 'auto') {
        return value;
    }

    return '';
}

function applyBlockAnnotations(node, block) {
    const comments = collectMarks(block, mark => String(mark?.type || '').toLowerCase() === 'commentanchor');
    const revisions = collectMarks(block, mark => String(mark?.type || '').toLowerCase() === 'revision');
    if (comments.length > 0) {
        node.setAttribute('data-canvas-a11y-comment-ids', comments.map(mark => mark.commentAnchor?.commentId || mark.commentId || '').filter(Boolean).join(' '));
    }

    if (revisions.length > 0) {
        node.setAttribute('data-canvas-a11y-revision-ids', revisions.map(mark => mark.revisionId || '').filter(Boolean).join(' '));
    }
}

function collectMarks(block, predicate) {
    const marks = [];
    for (const run of Array.isArray(block?.content?.runs) ? block.content.runs : []) {
        for (const mark of Array.isArray(run?.marks) ? run.marks : []) {
            if (predicate(mark)) {
                marks.push(mark);
            }
        }
    }

    return marks;
}

function findRevisionMark(run) {
    return (Array.isArray(run?.marks) ? run.marks : []).find(mark => String(mark?.type || '').toLowerCase() === 'revision') || null;
}

function findCommentMark(run) {
    const mark = (Array.isArray(run?.marks) ? run.marks : []).find(item => String(item?.type || '').toLowerCase() === 'commentanchor') || null;
    if (!mark) {
        return null;
    }

    return {
        commentId: mark.commentAnchor?.commentId || mark.commentId || '',
    };
}
