// Fáze 23 (code review N2): memoizovaný per-model index bloků. Selection-state čtečky
// (content control + signing field) dělaly plný DFS dokumentu po KAŽDÉM settled editu, přestože
// interop kontrakt slibuje O(focused block). Model je copy-on-write (edit vymění referenci),
// takže WeakMap klíčovaná objektem modelu platí po celý jeho život — stejný vzor jako
// rawBlocksById v layout enginu. Pozn.: tabulkové buňky mohou být mutovány in-place
// (nález N6), ale identita/id bloku se tam nemění, index tedy zůstává platný.
//
// Entry: { block, nestedInControl, headerFooterId, headerFooterScope }
//   - body/table bloky:            nestedInControl=false, headerFooterId=''
//   - bloky uvnitř content controlu: nestedInControl=true,  headerFooterId=''
//   - header/footer bloky:          headerFooterId + surový scope
// Konzumenti filtrují dle svého historického pokrytí (content control nehledá v header/footer,
// signing field nehledá uvnitř block-scope content controlů) — první výskyt id vyhrává,
// v pořadí původního DFS.

const indexByModel = new WeakMap();

export function getBlockIndex(model) {
    if (!model || typeof model !== 'object') {
        return EMPTY_INDEX;
    }

    let index = indexByModel.get(model);
    if (!index) {
        index = buildIndex(model);
        indexByModel.set(model, index);
    }

    return index;
}

const EMPTY_INDEX = new Map();

function buildIndex(model) {
    const index = new Map();
    // Stejné pořadí procházení jako původní DFS v content-control-selection.mjs.
    const stack = Array.isArray(model?.body?.blocks)
        ? model.body.blocks.map(block => ({ block, nestedInControl: false })).reverse()
        : [];
    while (stack.length > 0) {
        const frame = stack.pop();
        const block = frame?.block;
        if (!block) {
            continue;
        }

        const id = String(block?.id || '');
        if (id && !index.has(id)) {
            index.set(id, {
                block,
                nestedInControl: frame.nestedInControl === true,
                headerFooterId: '',
                headerFooterScope: null,
            });
        }

        const rows = block?.content?.table?.rows;
        if (Array.isArray(rows)) {
            for (let rowIndex = rows.length - 1; rowIndex >= 0; rowIndex -= 1) {
                for (const cell of [...(rows[rowIndex]?.cells || [])].reverse()) {
                    for (const nested of [...(cell?.blocks || [])].reverse()) {
                        stack.push({ block: nested, nestedInControl: frame.nestedInControl === true });
                    }
                }
            }
        }

        const nestedControlBlocks = block?.content?.contentControl?.blocks;
        if (Array.isArray(nestedControlBlocks)) {
            for (let nestedIndex = nestedControlBlocks.length - 1; nestedIndex >= 0; nestedIndex -= 1) {
                stack.push({ block: nestedControlBlocks[nestedIndex], nestedInControl: true });
            }
        }
    }

    for (const headerFooter of Array.isArray(model?.headersFooters) ? model.headersFooters : []) {
        for (const block of Array.isArray(headerFooter?.blocks) ? headerFooter.blocks : []) {
            const id = String(block?.id || '');
            if (id && !index.has(id)) {
                index.set(id, {
                    block,
                    nestedInControl: false,
                    headerFooterId: String(headerFooter.id || ''),
                    headerFooterScope: headerFooter.scope ?? headerFooter.Scope ?? null,
                });
            }
        }
    }

    return index;
}
