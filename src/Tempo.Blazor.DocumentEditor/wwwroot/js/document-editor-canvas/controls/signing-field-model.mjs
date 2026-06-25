// Signing field run model (plan S2). A signing field is a new canvas run type — an atomic inline box
// that a signer later fills (signature, initials, date, …). It is position-agnostic: the same
// normalized field lives identically in a body block or a header/footer block. The field `uuid` is
// the stable key used to group the field's per-page layout occurrences into its signing areas
// (body field → 1 area; header/footer field → one area per page it renders on).

// String keys mirroring the C# `SigningFieldType` enum (camelCase), EXCLUDING the static
// overlay-only `heading`/`strikethrough` (plan decision #5 — those are authored as plain text).
export const SIGNING_FIELD_TYPES = Object.freeze({
    text: 'text',
    signature: 'signature',
    initials: 'initials',
    date: 'date',
    dateNow: 'dateNow',
    number: 'number',
    image: 'image',
    file: 'file',
    select: 'select',
    checkbox: 'checkbox',
    multiple: 'multiple',
    radio: 'radio',
    cells: 'cells',
    stamp: 'stamp',
    payment: 'payment',
    phone: 'phone',
    verification: 'verification',
    kba: 'kba',
});

// Default inline box per field type (CSS px / document units). Signature/initials/stamp reserve a
// larger box than text; a checkbox/radio is the most compact. Used when the payload omits a size.
const DEFAULT_BOX = Object.freeze({ width: 140, height: 24 });
const BOX_BY_TYPE = Object.freeze({
    signature: { width: 180, height: 44 },
    initials: { width: 96, height: 44 },
    stamp: { width: 120, height: 64 },
    image: { width: 120, height: 80 },
    checkbox: { width: 18, height: 18 },
    radio: { width: 18, height: 18 },
    cells: { width: 180, height: 28 },
});

// Fallback palette used to colour a signing field whose role is unknown or has no colour, so a field
// is always visually attributable to a signer even before roles are fully configured.
export const SIGNING_ROLE_PALETTE = Object.freeze([
    '#2563eb', '#16a34a', '#db2777', '#d97706', '#7c3aed', '#0891b2', '#dc2626', '#4b5563',
]);

// Resolves the display colour for a signing field's role from the engine's signing roles, falling
// back to a deterministic palette colour keyed by the role identifier (or field) so it stays stable.
export function resolveSigningRoleColor(submitterUuid, signingRoles, fallbackKey = '') {
    const id = String(submitterUuid ?? '').trim();
    const roles = Array.isArray(signingRoles) ? signingRoles : [];
    const match = roles.find(role => String(role?.uuid ?? role?.Uuid ?? '') === id && id !== '');
    const color = match ? String(match.color ?? match.Color ?? '').trim() : '';
    if (/^#[0-9a-f]{6}$/i.test(color)) {
        return color;
    }

    const key = id || String(fallbackKey ?? '');
    let hash = 0;
    for (let index = 0; index < key.length; index += 1) {
        hash = (hash * 31 + key.charCodeAt(index)) | 0;
    }

    return SIGNING_ROLE_PALETTE[Math.abs(hash) % SIGNING_ROLE_PALETTE.length];
}

let signingFieldSequence = 0;

export function normalizeSigningFieldType(value) {
    const key = camelCaseKey(value);
    return Object.prototype.hasOwnProperty.call(SIGNING_FIELD_TYPES, key)
        ? SIGNING_FIELD_TYPES[key]
        : SIGNING_FIELD_TYPES.text;
}

export function defaultSigningFieldBox(fieldType) {
    return BOX_BY_TYPE[normalizeSigningFieldType(fieldType)] || DEFAULT_BOX;
}

export function normalizeSigningFieldRun(run = {}) {
    const source = objectOrEmpty(run);
    const payload = objectOrEmpty(source.signingField ?? source.SigningField ?? source);
    const fieldType = normalizeSigningFieldType(payload.fieldType ?? payload.FieldType ?? payload.type ?? payload.Type);
    const box = defaultSigningFieldBox(fieldType);
    return {
        uuid: stringValue(payload.uuid ?? payload.Uuid ?? source.id ?? source.Id, generateUuid()),
        fieldType,
        submitterUuid: stringValue(payload.submitterUuid ?? payload.SubmitterUuid, ''),
        required: Boolean(payload.required ?? payload.Required),
        label: stringValue(payload.label ?? payload.Label, ''),
        boxWidth: positiveNumber(payload.boxWidth ?? payload.BoxWidth, box.width),
        boxHeight: positiveNumber(payload.boxHeight ?? payload.BoxHeight, box.height),
        options: normalizeOptions(payload.options ?? payload.Options),
    };
}

// Builds a normalized canvas run wrapping a signing field, ready to insert at a caret.
export function createSigningFieldRun(input = {}) {
    const field = normalizeSigningFieldRun({ signingField: input });
    return {
        id: stringValue(input.runId ?? input.RunId, `signing-run-${++signingFieldSequence}`),
        type: 'signingField',
        text: '',
        marks: [],
        signingField: field,
    };
}

function normalizeOptions(value) {
    if (!Array.isArray(value)) {
        return [];
    }

    return value
        .map(option => {
            const source = objectOrEmpty(option);
            return {
                value: stringValue(source.value ?? source.Value, ''),
                label: stringValue(source.label ?? source.Label, ''),
            };
        })
        .filter(option => option.value !== '' || option.label !== '');
}

function camelCaseKey(value) {
    const text = String(value ?? '').trim();
    if (!text) {
        return '';
    }

    return text.charAt(0).toLowerCase() + text.slice(1);
}

function objectOrEmpty(value) {
    return value && typeof value === 'object' && !Array.isArray(value) ? value : {};
}

function stringValue(value, fallback) {
    if (value == null) {
        return fallback;
    }

    const text = String(value);
    return text.length === 0 && fallback !== '' ? fallback : text;
}

function positiveNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? number : fallback;
}

function generateUuid() {
    const cryptoRef = globalThis.crypto;
    if (cryptoRef && typeof cryptoRef.randomUUID === 'function') {
        return cryptoRef.randomUUID().replace(/-/g, '');
    }

    return `signing-${Date.now().toString(36)}-${(++signingFieldSequence).toString(36)}`;
}
