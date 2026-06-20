export function createRecalcInfo(options = {}) {
    const idleScheduler = typeof options.scheduleIdle === 'function'
        ? options.scheduleIdle
        : defaultScheduleIdle;
    const blockOrder = new Map();
    let dirtyBlockIds = new Set();
    let firstDirtyBlockIndex = -1;
    let lastFirstDirtyBlockIndex = -1;
    let structural = false;
    let idleReconciliationQueued = false;
    let idleReconciliationCount = 0;

    function updateBlockOrder(model) {
        blockOrder.clear();
        editableBlocks(model).forEach((block, index) => {
            const id = String(block?.id || block?.Id || '');
            if (id) {
                blockOrder.set(id, index);
            }
        });
        recomputeFirstDirty();
        return snapshot();
    }

    function markDirty(blockIds, options = {}) {
        const ids = normalizeIds(blockIds);
        for (const id of ids) {
            dirtyBlockIds.add(id);
        }

        structural = structural || options.structural === true;
        recomputeFirstDirty();
        return snapshot();
    }

    function clearDirty() {
        dirtyBlockIds = new Set();
        firstDirtyBlockIndex = -1;
        structural = false;
        return snapshot();
    }

    function immediateRenderOptions(change = {}) {
        const ids = normalizeIds(change.dirtyBlockIds || change.blockIds || Array.from(dirtyBlockIds));
        const changeStructural = change.structural === true || structural === true;
        return {
            dirtyBlockIds: ids,
            structural: changeStructural,
            firstDirtyBlockIndex: firstDirtyBlockIndexFor(ids),
            incremental: ids.length > 0,
        };
    }

    function queueIdleReconciliation(callback) {
        if (idleReconciliationQueued) {
            return false;
        }

        idleReconciliationQueued = true;
        idleScheduler(() => {
            idleReconciliationQueued = false;
            idleReconciliationCount += 1;
            clearDirty();
            if (typeof callback === 'function') {
                callback(snapshot());
            }
        });
        return true;
    }

    function snapshot() {
        return {
            dirtyBlockIds: Array.from(dirtyBlockIds),
            dirtyBlockCount: dirtyBlockIds.size,
            firstDirtyBlockIndex,
            lastFirstDirtyBlockIndex,
            structural,
            idleReconciliationQueued,
            idleReconciliationCount,
            blockCount: blockOrder.size,
        };
    }

    function recomputeFirstDirty() {
        firstDirtyBlockIndex = firstDirtyBlockIndexFor(Array.from(dirtyBlockIds));
        if (firstDirtyBlockIndex >= 0) {
            lastFirstDirtyBlockIndex = firstDirtyBlockIndex;
        }
    }

    function firstDirtyBlockIndexFor(ids) {
        const indexes = normalizeIds(ids)
            .map(id => blockOrder.has(id) ? blockOrder.get(id) : Number.POSITIVE_INFINITY)
            .filter(index => Number.isFinite(index));
        return indexes.length === 0 ? -1 : Math.min(...indexes);
    }

    return {
        updateBlockOrder,
        markDirty,
        clearDirty,
        immediateRenderOptions,
        queueIdleReconciliation,
        snapshot,
    };
}

export function dirtyPagesFromDisplayList(displayList, dirtyBlockIds, options = {}) {
    const ids = new Set(normalizeIds(dirtyBlockIds));
    if (ids.size === 0) {
        return [];
    }

    const pageIndexes = new Set();
    for (const command of displayList?.commands || []) {
        if (ids.has(String(command?.blockId || ''))) {
            pageIndexes.add(Number(command?.pageIndex || 0) || 0);
        }
    }

    if (options.structural === true && pageIndexes.size > 0) {
        const first = Math.min(...pageIndexes);
        return (displayList?.pages || [])
            .map(page => Number(page?.index || 0) || 0)
            .filter(pageIndex => pageIndex >= first);
    }

    return Array.from(pageIndexes).sort((left, right) => left - right);
}

function editableBlocks(model) {
    if (Array.isArray(model?.body?.blocks)) {
        return model.body.blocks;
    }

    if (Array.isArray(model?.Body?.Blocks)) {
        return model.Body.Blocks;
    }

    if (Array.isArray(model?.sections)) {
        return model.sections.flatMap(section => Array.isArray(section?.blocks) ? section.blocks : []);
    }

    if (Array.isArray(model?.Sections)) {
        return model.Sections.flatMap(section => Array.isArray(section?.Blocks) ? section.Blocks : []);
    }

    return [];
}

function normalizeIds(value) {
    const source = Array.isArray(value) ? value : value == null ? [] : [value];
    return Array.from(new Set(source.map(item => String(item || '').trim()).filter(Boolean)));
}

function defaultScheduleIdle(callback) {
    const win = globalThis.window || globalThis;
    if (typeof win.requestIdleCallback === 'function') {
        win.requestIdleCallback(callback, { timeout: 250 });
        return;
    }

    setTimeout(callback, 16);
}
