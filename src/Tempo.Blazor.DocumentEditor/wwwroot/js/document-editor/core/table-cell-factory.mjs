// Phase D — core/table-cell-factory.mjs
// `createEmptyTableCellFactory({importBlock})` factory → `createEmptyTableCell(tableId, rowIndex, columnIndex)`
// — builds a fresh table cell shape containing one empty paragraph block. Used by
// the table operations when inserting rows/columns.
//
// Cell id pattern: `<tableId>-r<rowIndex>-c<columnIndex>`. Paragraph id: `<cellId>-p`.
// Inline run id: `<cellId>-r`.

export function createEmptyTableCellFactory(options) {
    const opts = options || {};
    if (typeof opts.importBlock !== 'function') {
        throw new TypeError(
            'createEmptyTableCellFactory requires options.importBlock (function)');
    }
    const { importBlock } = opts;

    return function createEmptyTableCell(tableId, rowIndex, columnIndex) {
        const cellId = tableId + '-r' + rowIndex + '-c' + columnIndex;
        return {
            id: cellId,
            type: 'tableCell',
            rowSpan: 1,
            colSpan: 1,
            width: null,
            height: null,
            style: {},
            blocks: [importBlock({
                Id: cellId + '-p',
                Type: 'Paragraph',
                Content: { Inlines: [{ Id: cellId + '-r', Text: '' }] },
            }, cellId + '-block')],
        };
    };
}
