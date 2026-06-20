// Phase D — input/before-input.mjs
// `normalizeBeforeInput(eventLike)` — pure transform from a browser `beforeinput`
// event into a model-level command record (`{ supported, command, data, inputType, …
// }`). The full input pipeline (which routes commands through history + layout)
// stays in the legacy IIFE; this module covers the pure normalisation step that
// callers (input pipeline, paste handler, command bridge) all share.

import { asText, sortObject } from '../core/helpers.mjs';

// Map of browser inputType → engine command name. Anything not in the map is
// reported as `supported: false` so the caller can fall through to a generic handler
// (or log it). Mirrors the legacy `BEFORE_INPUT_COMMANDS` exactly.
export const BeforeInputCommands = Object.freeze({
    insertText: 'InsertText',
    insertParagraph: 'SplitParagraph',
    insertLineBreak: 'InsertText',
    insertCompositionText: 'InsertCompositionText',
    deleteContentBackward: 'DeleteBackward',
    deleteContentForward: 'DeleteForward',
    deleteWordBackward: 'DeleteBackward',
    deleteWordForward: 'DeleteForward',
    insertFromPaste: 'Paste',
    formatBold: 'ToggleBold',
});

// Normalise a browser-ish `beforeinput` event into the canonical command record.
// Calls `preventDefault()` when present (the engine always handles the input itself).
// Unknown inputTypes return `{ supported: false, log: { code, inputType } }` so the
// caller can log the unsupported case.
export function normalizeBeforeInput(eventLike) {
    const event = eventLike || {};
    const inputType = asText(event.inputType || event.InputType);
    if (typeof event.preventDefault === 'function') event.preventDefault();
    const command = BeforeInputCommands[inputType] || '';
    if (!command) {
        return sortObject({
            supported: false,
            preventDefault: true,
            inputType,
            command: '',
            canonicalSource: 'model-operation',
            log: { code: 'unsupported-beforeinput', inputType },
        });
    }
    return sortObject({
        supported: true,
        preventDefault: true,
        inputType,
        command,
        data: event.data ?? event.Data ?? null,
        canonicalSource: 'model-operation',
        log: null,
    });
}

// Factory wrapper matching the legacy `createBeforeInputNormalizer` shape so call
// sites that wanted a `{ normalize: fn }` object can keep their existing API.
export function createBeforeInputNormalizer() {
    return { normalize: normalizeBeforeInput };
}
