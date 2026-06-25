// Phase D — input/command-classifiers.mjs
// Pure classifiers used by the command-dispatcher pipeline. They map between
// toolbar command ids and the underlying mark types, and discriminate which
// commands apply to inline runs vs. paragraph attributes.
//
//   - `commandSource(input)` — extracts the originating surface (toolbar/keyboard/api)
//     from a command payload; defaults to 'api' for null/string inputs.
//   - `inlineCommandTypes()` — list of command ids that produce inline marks.
//   - `paragraphCommandTypes()` — list of command ids that change paragraph attrs.
//   - `markMatchesCommand(mark, id)` — true when `mark`'s normalised type is the
//     mark that the toolbar command would produce. Handles aliases (strike vs
//     strikethrough, backgroundColor vs highlight, etc.).

import { markType } from '../core/marks.mjs';

export function commandSource(input) {
    if (!input || typeof input === 'string') return 'api';
    return String(input.surface || input.Surface
        || input.source || input.Source
        || 'api');
}

export function inlineCommandTypes() {
    return [
        'bold',
        'italic',
        'underline',
        'strike',
        'fontFamily',
        'fontSize',
        'textColor',
        'backgroundColor',
        'link',
    ];
}

export function paragraphCommandTypes() {
    return [
        'alignment',
        'lineSpacing',
        'spacingBefore',
        'spacingAfter',
        'list',
        'indent',
        'outdent',
    ];
}

export function markMatchesCommand(mark, id) {
    const type = markType(mark);
    if (id === 'bold') return type === 'bold';
    if (id === 'italic') return type === 'italic';
    if (id === 'underline') return type === 'underline';
    if (id === 'strike') return type === 'strike' || type === 'strikethrough';
    if (id === 'fontFamily') return type === 'fontfamily';
    if (id === 'fontSize') return type === 'fontsize';
    if (id === 'textColor') {
        return type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor';
    }
    if (id === 'backgroundColor') return type === 'backgroundcolor' || type === 'highlight';
    if (id === 'link') return type === 'link';
    return false;
}
