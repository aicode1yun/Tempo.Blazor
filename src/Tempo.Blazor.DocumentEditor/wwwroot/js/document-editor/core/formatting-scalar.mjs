// Phase D — core/formatting-scalar.mjs
// `formattingScalarValue(formatting, commandId, fallback)` — single-cell lookup
// against a formatting state object. Returns `'mixed'` when the selection contains
// multiple values for the command, the resolved value otherwise, falling back to
// the supplied default when no value is known.
//
// Formatting state shape (matches the engine's runtime payload):
//   { inline: { mixed: { commandId: bool } }, commandValues: { commandId: value } }

export function formattingScalarValue(formatting, commandId, fallback) {
    const inline = formatting && formatting.inline || {};
    const mixed = inline.mixed || {};
    const commandValues = formatting && formatting.commandValues || {};
    if (mixed[commandId] === true) return 'mixed';
    const value = commandValues[commandId];
    return value === undefined || value === null ? fallback : value;
}
