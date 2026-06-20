// Phase D — input/command-id.mjs
// `normalizeCommandId(input)` — canonicalises a raw command input (string id,
// keyboard shortcut event, or command payload object) to a single canonical
// camel-case id (`bold`, `italic`, `fontFamily`, etc.).
//
// Accepts:
//   - String: direct command id (e.g. `'bold'`, `'set-font-family'`)
//   - Object: `{commandId|CommandId|id|Id|name|Name}` field
//   - Keyboard event: `{ctrlKey|metaKey, key}` → maps Ctrl+B/I/U to bold/italic/underline
//
// Strips common namespace prefixes (`format.`, `paragraph.`, `toggle-`) and
// resolves alias chains (`strike` ← `strikethrough`, `set-font-color` ← `textColor`, etc.).

const ALIASES = Object.freeze({
    'bold': 'bold',
    'toggle-bold': 'bold',
    'italic': 'italic',
    'toggle-italic': 'italic',
    'underline': 'underline',
    'toggle-underline': 'underline',
    'strike': 'strike',
    'strikethrough': 'strike',
    'font-family': 'fontFamily',
    'fontfamily': 'fontFamily',
    'set-font-family': 'fontFamily',
    'setfontfamily': 'fontFamily',
    'font-size': 'fontSize',
    'fontsize': 'fontSize',
    'set-font-size': 'fontSize',
    'setfontsize': 'fontSize',
    'text-color': 'textColor',
    'textcolor': 'textColor',
    'set-text-color': 'textColor',
    'settextcolor': 'textColor',
    'font-color': 'textColor',
    'fontcolor': 'textColor',
    'foreground-color': 'textColor',
    'foregroundcolor': 'textColor',
    'background-color': 'backgroundColor',
    'backgroundcolor': 'backgroundColor',
    'highlight': 'backgroundColor',
    'highlight-color': 'backgroundColor',
    'highlightcolor': 'backgroundColor',
    'set-highlight-color': 'backgroundColor',
    'sethighlightcolor': 'backgroundColor',
    'link': 'link',
    'remove-link': 'removeLink',
    'removelink': 'removeLink',
    'clear-formatting': 'clearFormatting',
    'clearformatting': 'clearFormatting',
    'remove-formatting': 'clearFormatting',
    'removeformatting': 'clearFormatting',
    'alignment': 'alignment',
    'align': 'alignment',
    'paragraph-alignment': 'alignment',
    'paragraphalignment': 'alignment',
    'set-paragraph-alignment': 'alignment',
    'setparagraphalignment': 'alignment',
    'line-spacing': 'lineSpacing',
    'linespacing': 'lineSpacing',
    'set-line-spacing': 'lineSpacing',
    'setlinespacing': 'lineSpacing',
    'spacing-before': 'spacingBefore',
    'spacingbefore': 'spacingBefore',
    'set-spacing-before': 'spacingBefore',
    'setspacingbefore': 'spacingBefore',
    'spacing-after': 'spacingAfter',
    'spacingafter': 'spacingAfter',
    'set-spacing-after': 'spacingAfter',
    'setspacingafter': 'spacingAfter',
    'list': 'list',
    'bullet-list': 'list',
    'bulletlist': 'list',
    'toggle-bullet-list': 'list',
    'togglebulletlist': 'list',
    'numbered-list': 'list',
    'numberedlist': 'list',
    'toggle-numbered-list': 'list',
    'togglenumberedlist': 'list',
    'indent': 'indent',
    'increase-indent': 'indent',
    'increaseindent': 'indent',
    'outdent': 'outdent',
    'decrease-indent': 'outdent',
    'decreaseindent': 'outdent',
    'insert-table': 'insertTable',
    'inserttable': 'insertTable',
    'insert-row-above': 'insertRowAbove',
    'insertrowabove': 'insertRowAbove',
    'insert-row-below': 'insertRowBelow',
    'insertrowbelow': 'insertRowBelow',
    'insert-column-left': 'insertColumnLeft',
    'insertcolumnleft': 'insertColumnLeft',
    'insert-column-right': 'insertColumnRight',
    'insertcolumnright': 'insertColumnRight',
    'delete-row': 'deleteRow',
    'deleterow': 'deleteRow',
    'delete-column': 'deleteColumn',
    'deletecolumn': 'deleteColumn',
    'merge-cells': 'mergeCells',
    'mergecells': 'mergeCells',
    'split-cell': 'splitCell',
    'splitcell': 'splitCell',
    'cell-background': 'cellBackground',
    'cellbackground': 'cellBackground',
    'cell-border': 'cellBorder',
    'cellborder': 'cellBorder',
    'resize-table': 'resizeTable',
    'resizetable': 'resizeTable',
});

export function normalizeCommandId(input) {
    let value = input || {};
    if (typeof value === 'string') value = { commandId: value };
    let key = String(
        value.commandId || value.CommandId
        || value.id || value.Id
        || value.name || value.Name
        || '').trim();
    if (!key && (value.ctrlKey || value.CtrlKey || value.metaKey || value.MetaKey)) {
        const shortcutKey = String(value.key || value.Key || '').toLowerCase();
        if (shortcutKey === 'b') key = 'bold';
        if (shortcutKey === 'i') key = 'italic';
        if (shortcutKey === 'u') key = 'underline';
    }
    const normalized = key
        .replace(/^format[.\-_:]/i, '')
        .replace(/^paragraph[.\-_:]/i, '')
        .replace(/^toggle[.\-_:]?/i, '')
        .replace(/[\s_.:-]+/g, '-')
        .toLowerCase();
    return ALIASES[normalized] || normalized;
}
