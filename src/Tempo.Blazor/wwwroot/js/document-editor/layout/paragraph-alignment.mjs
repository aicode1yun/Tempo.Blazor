// Phase D — layout/paragraph-alignment.mjs
// `normalizeParagraphAlignment` — pure mapper for paragraph alignment values.
// Accepts numeric (0..3), string ('left'/'center'/'centre'/'right'/'end'/'justify'/'justified'),
// or unknown values (default 'left'). Used by the line breaker and atomic renderer.

export function normalizeParagraphAlignment(value) {
    const normalized = String(value ?? 'left').trim().toLowerCase();
    if (normalized === '1' || normalized === 'center' || normalized === 'centre') return 'center';
    if (normalized === '2' || normalized === 'right' || normalized === 'end') return 'right';
    if (normalized === '3' || normalized === 'justify' || normalized === 'justified') return 'justify';
    return 'left';
}
