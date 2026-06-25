export const CONTENT_CONTROL_KINDS = Object.freeze({
    plainText: 'plainText',
    richText: 'richText',
    comboBox: 'comboBox',
    dropDown: 'dropDown',
    date: 'date',
    checkbox: 'checkbox',
    picture: 'picture',
    repeatingSection: 'repeatingSection',
});

export const CONTENT_CONTROL_SCOPES = Object.freeze({
    inline: 'inline',
    block: 'block',
});

export function normalizeContentControlRun(run = {}) {
    const source = objectOrEmpty(run);
    const payload = objectOrEmpty(source.contentControl ?? source.ContentControl);
    const control = normalizeContentControl(payload.control ?? payload.Control ?? source.control ?? source.Control, {
        fallbackId: source.id ?? source.Id,
        scope: CONTENT_CONTROL_SCOPES.inline,
    });
    return {
        control,
        runs: Array.isArray(payload.runs) ? payload.runs : Array.isArray(payload.Runs) ? payload.Runs : [],
    };
}

export function normalizeContentControlBlock(block = {}) {
    const source = objectOrEmpty(block);
    const payload = objectOrEmpty(source.contentControl ?? source.ContentControl);
    return {
        control: normalizeContentControl(payload.control ?? payload.Control ?? source.control ?? source.Control, {
            fallbackId: source.id ?? source.Id,
            scope: CONTENT_CONTROL_SCOPES.block,
        }),
        blocks: Array.isArray(payload.blocks) ? payload.blocks : Array.isArray(payload.Blocks) ? payload.Blocks : [],
    };
}

export function normalizeContentControl(input = {}, options = {}) {
    const source = objectOrEmpty(input);
    const kind = normalizeKind(source.kind ?? source.Kind);
    const value = normalizeValue(source.value ?? source.Value, kind);
    const items = normalizeItems(source.items ?? source.Items);
    const fallbackId = options.fallbackId == null ? 'sdt' : String(options.fallbackId);
    return {
        controlId: stringValue(source.controlId ?? source.ControlId ?? fallbackId, fallbackId),
        kind,
        scope: normalizeScope(source.scope ?? source.Scope ?? options.scope),
        alias: nullableText(source.alias ?? source.Alias),
        tag: nullableText(source.tag ?? source.Tag),
        placeholderText: nullableText(source.placeholderText ?? source.PlaceholderText),
        isRequired: Boolean(source.isRequired ?? source.IsRequired),
        lockContent: Boolean(source.lockContent ?? source.LockContent),
        lockDeletion: Boolean(source.lockDeletion ?? source.LockDeletion),
        formatMask: nullableText(source.formatMask ?? source.FormatMask),
        value,
        items,
        metadata: objectOrEmpty(source.metadata ?? source.Metadata),
        displayText: contentControlDisplayText({ kind, value, items, placeholderText: source.placeholderText ?? source.PlaceholderText }),
        isPlaceholder: isEmptyValue(kind, value),
        validation: validateContentControl({ kind, value, items, isRequired: Boolean(source.isRequired ?? source.IsRequired) }),
    };
}

export function contentControlDisplayText(control = {}) {
    const kind = normalizeKind(control.kind ?? control.Kind);
    const value = normalizeValue(control.value ?? control.Value, kind);
    const placeholder = nullableText(control.placeholderText ?? control.PlaceholderText) || '';
    if (kind === CONTENT_CONTROL_KINDS.checkbox) {
        return value.checked === true ? '☑' : '☐';
    }

    if (kind === CONTENT_CONTROL_KINDS.dropDown || kind === CONTENT_CONTROL_KINDS.comboBox) {
        const selected = value.selectedValue || value.text || '';
        if (!selected) return placeholder;
        const item = normalizeItems(control.items ?? control.Items).find(candidate => candidate.value === selected);
        return item?.displayText || selected;
    }

    if (kind === CONTENT_CONTROL_KINDS.date) {
        return value.dateIso || value.text || placeholder;
    }

    if (kind === CONTENT_CONTROL_KINDS.picture) {
        return value.assetId || placeholder;
    }

    return value.text || placeholder;
}

export function setContentControlValue(control = {}, nextValue = {}) {
    const normalized = normalizeContentControl(control);
    if (normalized.lockContent) {
        return {
            changed: false,
            reason: 'locked',
            control: normalized,
        };
    }

    const value = normalizeValue(nextValue, normalized.kind);
    const next = normalizeContentControl({
        ...normalized,
        value,
    });
    return {
        changed: JSON.stringify(normalized.value) !== JSON.stringify(next.value),
        reason: '',
        control: next,
    };
}

export function validateContentControl(control = {}) {
    const kind = normalizeKind(control.kind ?? control.Kind);
    const value = normalizeValue(control.value ?? control.Value, kind);
    const required = Boolean(control.isRequired ?? control.IsRequired);
    const items = normalizeItems(control.items ?? control.Items);
    const empty = isEmptyValue(kind, value);
    if (required && empty) {
        return { valid: false, reason: 'required' };
    }

    if ((kind === CONTENT_CONTROL_KINDS.dropDown || kind === CONTENT_CONTROL_KINDS.comboBox)
        && value.selectedValue
        && items.length > 0
        && !items.some(item => item.value === value.selectedValue)) {
        return { valid: false, reason: 'unknownOption' };
    }

    return { valid: true, reason: '' };
}

function normalizeKind(value) {
    const text = String(value ?? '').replace(/[\s_-]/g, '').toLowerCase();
    if (text === 'richtext') return CONTENT_CONTROL_KINDS.richText;
    if (text === 'combobox') return CONTENT_CONTROL_KINDS.comboBox;
    if (text === 'dropdown' || text === 'dropdownlist') return CONTENT_CONTROL_KINDS.dropDown;
    if (text === 'date' || text === 'datepicker') return CONTENT_CONTROL_KINDS.date;
    if (text === 'checkbox' || text === 'check') return CONTENT_CONTROL_KINDS.checkbox;
    if (text === 'picture' || text === 'image') return CONTENT_CONTROL_KINDS.picture;
    if (text === 'repeatingsection' || text === 'repeating') return CONTENT_CONTROL_KINDS.repeatingSection;
    return CONTENT_CONTROL_KINDS.plainText;
}

function normalizeScope(value) {
    const text = String(value ?? '').replace(/[\s_-]/g, '').toLowerCase();
    return text === 'block' ? CONTENT_CONTROL_SCOPES.block : CONTENT_CONTROL_SCOPES.inline;
}

function normalizeValue(input, kind) {
    const source = objectOrEmpty(input);
    if (kind === CONTENT_CONTROL_KINDS.checkbox) {
        return {
            text: nullableText(source.text ?? source.Text),
            selectedValue: nullableText(source.selectedValue ?? source.SelectedValue),
            checked: Boolean(source.checked ?? source.Checked),
            dateIso: nullableText(source.dateIso ?? source.DateIso),
            assetId: nullableText(source.assetId ?? source.AssetId),
        };
    }

    return {
        text: nullableText(source.text ?? source.Text),
        selectedValue: nullableText(source.selectedValue ?? source.SelectedValue),
        checked: source.checked ?? source.Checked ?? null,
        dateIso: nullableText(source.dateIso ?? source.DateIso),
        assetId: nullableText(source.assetId ?? source.AssetId),
    };
}

function normalizeItems(input) {
    const items = Array.isArray(input) ? input : [];
    return items.map((item, index) => {
        const source = objectOrEmpty(item);
        const value = stringValue(source.value ?? source.Value, `item-${index + 1}`);
        return {
            displayText: stringValue(source.displayText ?? source.DisplayText ?? value, value),
            value,
        };
    });
}

function isEmptyValue(kind, value) {
    if (kind === CONTENT_CONTROL_KINDS.checkbox) {
        return value.checked !== true;
    }

    if (kind === CONTENT_CONTROL_KINDS.dropDown || kind === CONTENT_CONTROL_KINDS.comboBox) {
        return !value.selectedValue && !value.text;
    }

    if (kind === CONTENT_CONTROL_KINDS.date) {
        return !value.dateIso && !value.text;
    }

    if (kind === CONTENT_CONTROL_KINDS.picture) {
        return !value.assetId;
    }

    return !value.text;
}

function objectOrEmpty(value) {
    return value && typeof value === 'object' && !Array.isArray(value) ? value : {};
}

function nullableText(value) {
    return value === null || value === undefined ? null : String(value);
}

function stringValue(value, fallback) {
    const text = nullableText(value);
    return text && text.trim() ? text : fallback;
}
