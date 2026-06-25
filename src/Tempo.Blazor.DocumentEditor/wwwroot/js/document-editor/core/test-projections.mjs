// Phase D — core/test-projections.mjs
// Test-oriented projections that coerce raw (possibly server-cased / enum-typed)
// block shapes into normalised forms for layout/render test harnesses.
//
// `blockTypeForTest(block)` — maps a block's `Type`/`type` (numeric enum or string)
//   to one of `'image' | 'table' | 'pageBreak' | 'paragraph'` (default paragraph).
// `paragraphRunsForTest(block)` — normalises a paragraph's inlines/runs to
//   `{Id, Text, Start, End, Marks}` with running char offsets; injects a single
//   empty run when the block has none. Falls back across Text/FallbackText/Key.

import { asArray, asText } from './helpers.mjs';

export function blockTypeForTest(block) {
    const type = block && (block.Type ?? block.type);
    if (type === 5 || String(type).toLowerCase().indexOf('image') >= 0) return 'image';
    if (type === 4 || String(type).toLowerCase().indexOf('table') >= 0) return 'table';
    if (type === 6 || String(type).toLowerCase().indexOf('pagebreak') >= 0) return 'pageBreak';
    return 'paragraph';
}

export function paragraphRunsForTest(block) {
    const content = (block && (block.Content || block.content)) || {};
    let runs = asArray(content.Inlines || content.inlines || content.Runs || content.runs);
    if (!runs.length) {
        runs = [{ Id: ((block && block.Id) || (block && block.id) || 'p') + '-empty', Text: '' }];
    }
    let offset = 0;
    return runs.map(function (run, index) {
        const text = asText(run.Text ?? run.text ?? run.FallbackText ?? run.fallbackText
            ?? run.Key ?? run.key ?? '');
        const result = {
            Id: asText(run.Id ?? run.id ?? ('inline-' + index)),
            Text: text,
            Start: offset,
            End: offset + text.length,
            Marks: asArray(run.Marks || run.marks),
        };
        offset += text.length;
        return result;
    });
}
