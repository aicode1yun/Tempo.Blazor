// Phase D — input/command-marks.mjs
// Convert user-facing command ids (`bold`/`italic`/`fontSize`/…) to the underlying
// inline mark records that the operations layer applies. Plus a colour normaliser and
// a "clear value" detector for colour pickers.
//
// Pure functions — no closure state, no DOM access.

// Normalise a colour input to a lowercase 6-char hex string. Accepts 3-char shorthand
// hex (`#abc` → `#aabbcc`), 6-char hex, or any other non-empty string (passes through
// trimmed). Empty/null returns null.
export function normalizeCommandColorValue(value) {
    if (value === undefined || value === null) return null;
    const text = String(value).trim();
    if (/^#[0-9a-f]{3}$/i.test(text)) {
        return '#' + text.slice(1).split('').map(part => part + part).join('').toLowerCase();
    }
    if (/^#[0-9a-f]{6}$/i.test(text)) {
        return text.toLowerCase();
    }
    return text || null;
}

// Map a command id + payload to an inline mark record. Returns null when the command
// id isn't a recognised mark command (callers handle that as "this command doesn't
// produce a mark and is processed differently").
export function commandMark(id, payload) {
    const body = payload || {};
    switch (id) {
        case 'bold': return { type: 0 };
        case 'italic': return { type: 1 };
        case 'underline': return { type: 2 };
        case 'strike': return { type: 3 };
        case 'fontFamily':
            return { type: 11, value: body.family || body.Family || body.value || body.Value || null };
        case 'fontSize':
            return { type: 12, value: body.size || body.Size || body.value || body.Value || null };
        case 'textColor':
            return {
                type: 10,
                value: normalizeCommandColorValue(body.color || body.Color || body.value || body.Value || null),
            };
        case 'backgroundColor':
            return {
                type: 9,
                value: normalizeCommandColorValue(body.color || body.Color || body.value || body.Value || null),
            };
        case 'link':
            return {
                type: 6,
                href: body.href || body.Href || body.url || body.Url || '',
                title: body.title || body.Title || null,
            };
        default:
            return null;
    }
}

// True when the colour-style command should remove the mark instead of applying it
// (user picked "no fill" / "automatic"). Only applies to textColor / backgroundColor.
export function isClearValueCommand(id, mark) {
    return (id === 'textColor' || id === 'backgroundColor')
        && mark
        && (mark.value === null || mark.value === undefined || mark.value === '');
}
