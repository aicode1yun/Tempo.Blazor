// Phase D — core/limit-finder.mjs
// `findLimitForBlock(model, blockId)` — walks the document containers to find the
// id of the smallest layout limit that contains the given block. Falls back to
// `'body'` when the block isn't found anywhere.
//
// Layout limits are: `body.id` (or `'body'`), `headers[i].id`, `footers[j].id`,
// and for table cells: `cell.id` (innermost cell wins for nested tables).

import { asArray } from './helpers.mjs';

export function findLimitForBlock(model, blockId) {
    const body = model && model.body;
    if (asArray(body && body.blocks).some(function (block) {
        return block.id === blockId;
    })) {
        return body && body.id || 'body';
    }
    for (let h = 0; h < asArray(model && model.headers).length; h++) {
        if (asArray(model.headers[h].blocks).some(function (block) {
            return block.id === blockId;
        })) {
            return model.headers[h].id;
        }
    }
    for (let f = 0; f < asArray(model && model.footers).length; f++) {
        if (asArray(model.footers[f].blocks).some(function (block) {
            return block.id === blockId;
        })) {
            return model.footers[f].id;
        }
    }
    let found = null;
    function scan(blocks) {
        asArray(blocks).forEach(function (block) {
            if (!block || block.type !== 'table') return;
            asArray(block.content && block.content.rows).forEach(function (row) {
                asArray(row.cells).forEach(function (cell) {
                    if (asArray(cell.blocks).some(function (child) {
                        return child.id === blockId;
                    })) {
                        found = cell.id;
                    }
                    scan(cell.blocks);
                });
            });
        });
    }
    scan(model && model.body && model.body.blocks);
    return found || 'body';
}
