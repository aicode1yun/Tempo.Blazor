// Phase D — render/engine-table-html.mjs
// `createRenderEngineTableHtml({escapeHtml, asArray, textFromRuns})` →
//   `renderEngineTableHtml(block)` — server-style HTML for a table block:
//   `<table>` → `<tr data-row-id>` → `<td data-cell-id data-column-index
//   colspan rowspan role=gridcell>` carrying optional inline style
//   (background / border / padding) and paragraph children rendered as
//   `<p data-block-id>` with their text content escaped.

export function createRenderEngineTableHtml(options) {
    const opts = options || {};
    for (const key of ['escapeHtml', 'asArray', 'textFromRuns']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createRenderEngineTableHtml requires options.${key} (function)`);
        }
    }
    const { escapeHtml, asArray, textFromRuns } = opts;

    return function renderEngineTableHtml(block) {
        const rows = asArray(block && block.content && block.content.rows);
        const html = [
            '<table class="tm-wysiwyg-block tm-wysiwyg-table" data-block-id="'
            + escapeHtml(block.id)
            + '" role="table" aria-label="Table"><tbody>',
        ];
        rows.forEach(function (row) {
            html.push('<tr data-row-id="' + escapeHtml(row.id) + '" role="row">');
            asArray(row.cells).forEach(function (cell, cellIndex) {
                const style = [];
                if (cell.style && cell.style.background) {
                    style.push('background:' + escapeHtml(cell.style.background));
                }
                if (cell.style && cell.style.border) {
                    style.push('border:' + escapeHtml(cell.style.border));
                }
                if (cell.style && cell.style.padding !== undefined) {
                    style.push('padding:' + Number(cell.style.padding || 0) + 'px');
                }
                html.push('<td data-cell-id="' + escapeHtml(cell.id)
                    + '" data-column-index="' + escapeHtml(cellIndex)
                    + '" colspan="' + escapeHtml(cell.colSpan || 1)
                    + '" rowspan="' + escapeHtml(cell.rowSpan || 1)
                    + '" role="gridcell" tabindex="-1" aria-label="Table cell '
                    + (cellIndex + 1) + '"'
                    + (style.length ? ' style="' + style.join(';') + '"' : '')
                    + '>');
                asArray(cell.blocks).forEach(function (child) {
                    if (child.type === 'paragraph') {
                        html.push('<p class="tm-wysiwyg-block" data-block-id="'
                            + escapeHtml(child.id) + '">'
                            + escapeHtml(textFromRuns(child.content && child.content.runs))
                            + '</p>');
                    }
                });
                html.push('</td>');
            });
            html.push('</tr>');
        });
        html.push('</tbody></table>');
        return html.join('');
    };
}
