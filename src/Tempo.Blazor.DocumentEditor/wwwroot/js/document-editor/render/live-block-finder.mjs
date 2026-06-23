// Phase D — render/live-block-finder.mjs
// DOM-walking helpers that find the live block element(s) for a given blockId in
// the rendered editor. Used by the typing fast-path + selection-restore paths.
//
// `findLiveTextBlockElement(inst, blockId)` — single match (first), skips block-level
//   image/table figures that share the same blockId attribute.
// `findLiveTextBlockElements(inst, blockId, selection?)` — all matches filtered by
//   selection's region (Body/Header/Footer) + optional headerFooterId.
// `findLiveTextBlockElementForContext(inst, blockId, context, selection?)` — picks the
//   match whose pageIndex/region/headerFooterId match the supplied layout context.
// `liveBlockElementMatchesSelection(node, selection)` — predicate used by the two above.
// `liveBlockContextFromElement(node)` — extracts pageIndex/region/headerFooterId from
//   the node's ancestors (closest data-page-index / data-render-region / data-hf-id).

import { asText } from '../core/helpers.mjs';
import { cssEscape } from './css-escape.mjs';

const BLOCK_SELECTOR_PREFIX = '.tm-wysiwyg-block[data-block-id="';
const BLOCK_SELECTOR_SUFFIX = '"]';
const NON_TEXT_BLOCK_SELECTOR = 'figure, table, .tm-wysiwyg-image, .tm-wysiwyg-table';

export function findLiveTextBlockElement(inst, blockId) {
    if (!inst || !inst.root || !blockId) return null;
    const selector = BLOCK_SELECTOR_PREFIX + cssEscape(blockId) + BLOCK_SELECTOR_SUFFIX;
    const node = inst.root.querySelector(selector);
    if (!node) return null;
    if (typeof node.matches === 'function' && node.matches(NON_TEXT_BLOCK_SELECTOR)) {
        return null;
    }
    return node;
}

export function liveBlockElementMatchesSelection(node, selection) {
    if (!node || !selection) return true;
    const region = asText(selection.region || selection.Region || '');
    const headerFooterId = selection.headerFooterId || selection.HeaderFooterId || null;
    if (region) {
        const regionNode = typeof node.closest === 'function'
            ? node.closest('[data-render-region]')
            : null;
        const nodeRegion = regionNode && regionNode.getAttribute('data-render-region') || 'Body';
        if (nodeRegion !== region) return false;
        if ((region === 'Header' || region === 'Footer') && headerFooterId) {
            const nodeHeaderFooterId = regionNode && regionNode.getAttribute('data-hf-id') || null;
            if (nodeHeaderFooterId && nodeHeaderFooterId !== headerFooterId) return false;
        }
    }
    return true;
}

export function findLiveTextBlockElements(inst, blockId, selection) {
    if (!inst || !inst.root || !blockId
        || typeof inst.root.querySelectorAll !== 'function') {
        return [];
    }
    const selector = BLOCK_SELECTOR_PREFIX + cssEscape(blockId) + BLOCK_SELECTOR_SUFFIX;
    return Array.from(inst.root.querySelectorAll(selector)).filter(function (node) {
        if (!node) return false;
        if (typeof node.matches === 'function' && node.matches(NON_TEXT_BLOCK_SELECTOR)) {
            return false;
        }
        return liveBlockElementMatchesSelection(node, selection);
    });
}

export function liveBlockContextFromElement(node) {
    if (!node || typeof node.closest !== 'function') return null;
    const page = node.closest('.tm-wysiwyg-page[data-page-index]');
    const regionNode = node.closest('[data-render-region]');
    return {
        pageIndex: page && page.getAttribute('data-page-index') || null,
        region: regionNode && regionNode.getAttribute('data-render-region') || 'Body',
        headerFooterId: regionNode && regionNode.getAttribute('data-hf-id') || null,
    };
}

export function findLiveTextBlockElementForContext(inst, blockId, context, selection) {
    const nodes = findLiveTextBlockElements(inst, blockId, selection);
    if (!nodes.length) return null;
    if (context) {
        const match = nodes.find(function (node) {
            const candidate = liveBlockContextFromElement(node);
            return candidate
                && (context.pageIndex === null || candidate.pageIndex === context.pageIndex)
                && (!context.region || candidate.region === context.region)
                && (!context.headerFooterId || candidate.headerFooterId === context.headerFooterId);
        });
        if (match) return match;
    }
    return nodes[0] || null;
}
