export function createRestrictedEditingRuntime(options = {}) {
    let protectedMode = Boolean(options.protectedMode);
    let markers = normalizeRestrictedMarkers(options.markers || []);

    function update(modelOrOptions = {}) {
        if (Array.isArray(modelOrOptions)) {
            markers = normalizeRestrictedMarkers(modelOrOptions);
            return snapshot();
        }

        protectedMode = isProtected(modelOrOptions);
        markers = normalizeRestrictedMarkers(modelOrOptions.restrictedMarkers || modelOrOptions.RestrictedMarkers || modelOrOptions.markers || []);
        return snapshot();
    }

    function canEdit(selection, model = null) {
        const effectiveProtected = model ? isProtected(model) : protectedMode;
        if (!effectiveProtected) {
            return { allowed: true, reason: '' };
        }

        const effectiveMarkers = model
            ? normalizeRestrictedMarkers(model.restrictedMarkers || model.RestrictedMarkers || [])
            : markers;
        if (effectiveMarkers.length === 0) {
            return { allowed: false, reason: 'documentProtected' };
        }

        const range = normalizeSelection(selection);
        if (!range) {
            return { allowed: false, reason: 'noSelection' };
        }

        const allowed = effectiveMarkers.some(marker => containsRange(marker, range));
        return {
            allowed,
            reason: allowed ? '' : 'outsideEditableRegion',
            markerId: allowed ? effectiveMarkers.find(marker => containsRange(marker, range))?.id || '' : '',
        };
    }

    function snapshot() {
        return {
            protectedMode,
            markerCount: markers.length,
            markers: markers.map(marker => ({ ...marker })),
        };
    }

    return {
        update,
        canEdit,
        snapshot,
    };
}

export function canEditRestrictedSelection(model, selection) {
    return createRestrictedEditingRuntime({ protectedMode: isProtected(model), markers: model?.restrictedMarkers || model?.RestrictedMarkers || [] })
        .canEdit(selection, model);
}

export function canCreateRestrictedSuggestion(model, suggestionOrRange) {
    if (!isProtected(model)) {
        return { allowed: true, reason: '' };
    }

    const markers = normalizeRestrictedMarkers(model?.restrictedMarkers || model?.RestrictedMarkers || []);
    if (markers.length === 0) {
        return { allowed: false, reason: 'documentProtected' };
    }

    const ranges = normalizeSuggestionRanges(suggestionOrRange);
    if (ranges.length === 0) {
        return { allowed: false, reason: 'noSuggestionRange' };
    }

    const denied = ranges.find(range => !markers.some(marker => containsRange(marker, range)));
    if (denied) {
        return {
            allowed: false,
            reason: 'outsideEditableRegion',
            blockId: denied.start.blockId,
        };
    }

    const marker = markers.find(candidate => ranges.some(range => containsRange(candidate, range))) || null;
    return { allowed: true, reason: '', markerId: marker?.id || '' };
}

export function normalizeRestrictedMarkers(markers) {
    return (Array.isArray(markers) ? markers : [])
        .map(marker => ({
            id: String(marker?.id || marker?.Id || ''),
            startBlockId: String(marker?.startBlockId || marker?.StartBlockId || ''),
            startOffset: Math.max(0, Number(marker?.startOffset ?? marker?.StartOffset ?? 0) || 0),
            endBlockId: String(marker?.endBlockId || marker?.EndBlockId || marker?.startBlockId || marker?.StartBlockId || ''),
            endOffset: Math.max(0, Number(marker?.endOffset ?? marker?.EndOffset ?? marker?.startOffset ?? marker?.StartOffset ?? 0) || 0),
            label: String(marker?.label || marker?.Label || ''),
        }))
        .filter(marker => marker.startBlockId && marker.endBlockId);
}

function isProtected(modelOrOptions = {}) {
    return modelOrOptions?.isProtected === true
        || modelOrOptions?.IsProtected === true
        || modelOrOptions?.protectedMode === true;
}

function normalizeSelection(selection) {
    const anchor = selection?.anchor || selection?.Anchor;
    const focus = selection?.focus || selection?.Focus || anchor;
    if (!anchor?.blockId && !anchor?.BlockId) {
        return null;
    }

    const left = {
        blockId: String(anchor.blockId || anchor.BlockId || ''),
        offset: Math.max(0, Number(anchor.offset ?? anchor.Offset ?? 0) || 0),
    };
    const right = {
        blockId: String(focus?.blockId || focus?.BlockId || left.blockId),
        offset: Math.max(0, Number(focus?.offset ?? focus?.Offset ?? left.offset) || 0),
    };
    if (left.blockId === right.blockId && left.offset > right.offset) {
        return { start: right, end: left };
    }

    return { start: left, end: right };
}

function containsRange(marker, range) {
    if (marker.startBlockId !== marker.endBlockId) {
        return range.start.blockId === marker.startBlockId
            && range.end.blockId === marker.endBlockId
            && range.start.offset >= marker.startOffset
            && range.end.offset <= marker.endOffset;
    }

    return range.start.blockId === marker.startBlockId
        && range.end.blockId === marker.endBlockId
        && range.start.offset >= marker.startOffset
        && range.end.offset <= marker.endOffset;
}

function normalizeSuggestionRanges(suggestionOrRange) {
    const source = suggestionOrRange || {};
    const directRange = source.range || source.Range || source;
    const ranges = [];
    const normalizedDirect = normalizeRangeLike(directRange);
    if (normalizedDirect) {
        ranges.push(normalizedDirect);
    }

    const operations = Array.isArray(source.operations || source.Operations)
        ? (source.operations || source.Operations)
        : [];
    for (const operation of operations) {
        const target = operation?.target || operation?.Target || {};
        const blockId = String(target.blockId || target.BlockId || directRange.blockId || directRange.BlockId || '');
        if (!blockId) {
            continue;
        }

        const startOffset = Math.max(0, Number(target.startOffset ?? target.StartOffset ?? target.offset ?? target.Offset ?? directRange.startOffset ?? directRange.StartOffset ?? 0) || 0);
        const length = target.length ?? target.Length;
        const explicitEnd = target.endOffset ?? target.EndOffset ?? directRange.endOffset ?? directRange.EndOffset;
        const endOffset = Math.max(
            startOffset,
            Number(explicitEnd ?? (length == null ? startOffset : startOffset + Number(length))) || startOffset);
        ranges.push({
            start: { blockId, offset: startOffset },
            end: { blockId, offset: endOffset },
        });
    }

    return ranges;
}

function normalizeRangeLike(range) {
    const blockId = String(range?.blockId || range?.BlockId || '');
    if (!blockId) {
        return null;
    }

    const startOffset = Math.max(0, Number(range?.startOffset ?? range?.StartOffset ?? 0) || 0);
    const endOffset = Math.max(startOffset, Number(range?.endOffset ?? range?.EndOffset ?? startOffset) || startOffset);
    return {
        start: { blockId, offset: startOffset },
        end: { blockId, offset: endOffset },
    };
}
