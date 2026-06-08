export function createTileCache(options = {}) {
    const maxEntries = Math.max(1, Number(options.maxEntries || 160) || 160);
    const entries = new Map();
    const stats = {
        hits: 0,
        misses: 0,
        invalidations: 0,
        evictions: 0,
        commits: 0,
    };

    function shouldRepaint(pageIndex, displayList, options = {}) {
        const key = String(pageIndex);
        const signature = pageSignature(displayList, pageIndex);
        const existing = entries.get(key);
        const dirty = options.force === true || normalizePageIndexes(options.dirtyPageIndexes).has(Number(pageIndex));
        if (!dirty && existing?.signature === signature) {
            touch(key, existing);
            stats.hits += 1;
            return { repaint: false, signature, reason: 'cache-hit' };
        }

        stats.misses += 1;
        return { repaint: true, signature, reason: dirty ? 'dirty-page' : 'signature-changed' };
    }

    function commitPage(pageIndex, signature, metadata = {}) {
        const key = String(pageIndex);
        entries.set(key, {
            pageIndex: Number(pageIndex) || 0,
            signature: String(signature || ''),
            committedAt: now(),
            metadata: { ...metadata },
        });
        stats.commits += 1;
        enforceLimit();
    }

    function invalidate(pageIndexes = null) {
        if (pageIndexes == null) {
            const count = entries.size;
            entries.clear();
            stats.invalidations += count;
            return count;
        }

        let count = 0;
        for (const pageIndex of normalizePageIndexes(pageIndexes)) {
            if (entries.delete(String(pageIndex))) {
                count += 1;
            }
        }

        stats.invalidations += count;
        return count;
    }

    function snapshot() {
        return {
            ...stats,
            entryCount: entries.size,
            maxEntries,
            pages: Array.from(entries.values()).map(entry => ({
                pageIndex: entry.pageIndex,
                signature: entry.signature,
                committedAt: entry.committedAt,
            })),
        };
    }

    function touch(key, entry) {
        entries.delete(key);
        entries.set(key, entry);
    }

    function enforceLimit() {
        while (entries.size > maxEntries) {
            const oldest = entries.keys().next().value;
            entries.delete(oldest);
            stats.evictions += 1;
        }
    }

    return {
        shouldRepaint,
        commitPage,
        invalidate,
        snapshot,
    };
}

export function pageSignature(displayList, pageIndex) {
    const normalizedPageIndex = Number(pageIndex) || 0;
    return (displayList?.commands || [])
        .filter(command => Number(command?.pageIndex || 0) === normalizedPageIndex)
        .filter(command => isContentCacheCommand(command))
        .map(command => [
            command.type,
            command.id,
            command.blockId,
            Math.round(Number(command.x || 0) * 100) / 100,
            Math.round(Number(command.y || 0) * 100) / 100,
            Math.round(Number(command.width || 0) * 100) / 100,
            Math.round(Number(command.height || 0) * 100) / 100,
            command.text || '',
            command.fill || '',
            command.stroke || '',
        ].join(':'))
        .join('|');
}

function isContentCacheCommand(command) {
    const layer = String(command?.layer || '');
    return layer !== 'selection'
        && layer !== 'selection-caret'
        && layer !== 'diagnostics'
        && command?.type !== 'selectionRect'
        && command?.type !== 'caret';
}

function normalizePageIndexes(value) {
    const source = value == null
        ? []
        : value instanceof Set
            ? Array.from(value)
            : Array.isArray(value)
                ? value
                : [value];
    return new Set(source.map(item => Number(item) || 0));
}

function now() {
    return Number(globalThis.performance?.now?.() || Date.now()) || 0;
}
