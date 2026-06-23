// Phase D — input/autocomplete-trigger.mjs
// Detect autocomplete triggers in text. Recognises `{{...}}` (token), `@...` (mention),
// and `/...` (slash command). Returns null when no trigger is present at the offset.

import { asText } from '../core/helpers.mjs';

// Returns `{ triggerId, marker, markerType, query, startOffset, endOffset }` describing
// the trigger the caret is currently inside, or null if no recognised trigger is at
// `offset` in `text`. The trigger must follow a word boundary (start of string or
// whitespace).
export function detectAutocompleteTriggerText(text, offset) {
    const before = asText(text).slice(0, Number(offset || 0));
    const match = before.match(/(?:^|\s)(\{\{|@|\/)([A-Za-z0-9_-]*)$/);
    if (!match) return null;
    const marker = match[1];
    const query = match[2] || '';
    const triggerId = marker === '{{' ? 'token' : (marker === '@' ? 'mention' : 'slash');
    return {
        triggerId,
        marker,
        markerType: triggerId === 'token' ? 'tokenQuery'
            : (triggerId === 'mention' ? 'tagQuery' : 'slashQuery'),
        query,
        startOffset: before.length - marker.length - query.length,
        endOffset: before.length,
    };
}
