import { contentControlDisplayText, normalizeContentControl, validateContentControl } from './sdt-model.mjs';

const FORM_MODE = 'form';
const DESIGN_MODE = 'design';

export function normalizeContentControlRenderMode(mode) {
    const normalized = String(mode || '').replace(/[\s_-]/g, '').toLowerCase();
    return normalized === DESIGN_MODE || normalized === 'designmode'
        ? DESIGN_MODE
        : FORM_MODE;
}

export function buildContentControlRenderState(control = {}, options = {}) {
    const normalized = normalizeContentControl(control);
    const mode = normalizeContentControlRenderMode(options.mode || options.contentControlRenderMode);
    const validation = validateContentControl(normalized);
    const text = contentControlDisplayText(normalized);
    const placeholder = normalized.isPlaceholder === true;
    const locked = normalized.lockContent === true;
    const invalid = validation.valid === false;
    const tagLabel = firstNonEmpty(normalized.alias, normalized.tag, normalized.controlId);

    return {
        mode,
        text,
        tagLabel,
        showChrome: mode === DESIGN_MODE,
        showTag: mode === DESIGN_MODE && tagLabel.length > 0,
        placeholder,
        locked,
        invalid,
        validation,
        typography: {
            color: placeholder ? '#64748b' : null,
            fontStyle: placeholder ? 'italic' : null,
        },
        chrome: {
            fill: invalid
                ? 'rgba(254, 226, 226, 0.82)'
                : locked ? 'rgba(226, 232, 240, 0.68)' : 'rgba(239, 246, 255, 0.74)',
            stroke: invalid
                ? 'rgba(220, 38, 38, 0.8)'
                : locked ? 'rgba(100, 116, 139, 0.7)' : 'rgba(37, 99, 235, 0.68)',
            labelFill: invalid ? 'rgba(153, 27, 27, 0.96)' : 'rgba(30, 64, 175, 0.96)',
            labelText: '#ffffff',
            dash: placeholder ? [3, 3] : [],
        },
    };
}

function firstNonEmpty(...values) {
    return values.map(value => String(value || '').trim()).find(Boolean) || '';
}
