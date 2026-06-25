// Phase D — core/schema-validation.mjs
// Schema-level filters applied to incoming insertion blocks (paste/clipboard).
//
//   - `schemaAllowsBlockForTest(type, region)` — true when the block type is allowed
//     in the destination region. Page breaks only land in the body; nested tables are
//     rejected inside table cells.
//   - `normalizeInsertionBlocksForSchema(blocks, region)` — applies the predicate, but
//     instead of dropping disallowed tables in table cells, unwraps them to the inner
//     blocks. Image blocks without AltText get an empty AltText defaulted in.
//
// Returns `{ blocks, warnings }` where warnings describe what was rewritten or dropped.

import { asArray, asText, clone } from './helpers.mjs';

export function schemaAllowsBlockForTest(type, region) {
    const normalizedRegion = asText(region).toLowerCase();
    if ((type === 6 || String(type).toLowerCase().indexOf('pagebreak') >= 0)
        && normalizedRegion !== 'body') {
        return false;
    }
    if ((type === 4 || String(type).toLowerCase().indexOf('table') >= 0)
        && normalizedRegion === 'tablecell') {
        return false;
    }
    return true;
}

export function normalizeInsertionBlocksForSchema(blocks, region) {
    const warnings = [];
    const output = [];
    asArray(blocks).forEach(function (block) {
        if (!schemaAllowsBlockForTest(block.Type ?? block.type, region)) {
            if ((block.Type ?? block.type) === 4 && asText(region).toLowerCase() === 'tablecell') {
                asArray(block.Content && block.Content.Rows).forEach(function (row) {
                    asArray(row.Cells).forEach(function (cell) {
                        asArray(cell.Blocks).forEach(function (child) {
                            output.push(clone(child));
                        });
                    });
                });
                warnings.push({ code: 'table-unwrapped-in-table-cell' });
            } else {
                warnings.push({ code: 'block-rejected-by-schema' });
            }
            return;
        }
        const cloned = clone(block);
        if ((cloned.Type ?? cloned.type) === 5
            && cloned.Content && cloned.Content.AltText === undefined) {
            cloned.Content.AltText = '';
            warnings.push({ code: 'image-alt-text-defaulted' });
        }
        output.push(cloned);
    });
    return { blocks: output, warnings: warnings };
}
