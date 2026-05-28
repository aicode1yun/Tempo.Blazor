// Phase D — objects/horizontal-position.mjs
// Horizontal position enum (Left=0, Center=1, Right=2) for floating object alignment.
// Pure helpers.

export function normalizeHorizontalPositionName(value) {
    if (value === 0) return 'Left';
    if (value === 1) return 'Center';
    if (value === 2) return 'Right';
    const raw = String(value || '').replace(/[\s_-]+/g, '').toLowerCase();
    if (raw === 'center' || raw === 'centre' || raw === 'middle') return 'Center';
    if (raw === 'right' || raw === 'end') return 'Right';
    return 'Left';
}

export function horizontalPositionToValue(value) {
    const normalized = normalizeHorizontalPositionName(value);
    return normalized === 'Center' ? 1 : (normalized === 'Right' ? 2 : 0);
}
