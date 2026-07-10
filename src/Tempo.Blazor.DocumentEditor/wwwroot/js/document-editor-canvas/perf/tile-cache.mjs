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

// Perf plan N6.5: the signature is a rolling hash over the same per-command fingerprint fields the
// old implementation joined into one giant string — building that string allocated O(page text)
// per page on EVERY paint, including 100% cache hits. Two independent 32-bit accumulators plus the
// command count keep the collision odds negligible for a paint cache.
export function pageSignature(displayList, pageIndex) {
    const normalizedPageIndex = Number(pageIndex) || 0;
    let hashA = 5381;
    let hashB = 52711;
    let count = 0;

    const mixString = (value) => {
        const text = String(value ?? '');
        for (let index = 0; index < text.length; index += 1) {
            const code = text.charCodeAt(index);
            hashA = (((hashA << 5) + hashA) + code) | 0;
            hashB = (((hashB << 7) - hashB) ^ code) | 0;
        }
        // Field separator so adjacent fields cannot alias (["ab", "c"] vs ["a", "bc"]).
        hashA = (((hashA << 5) + hashA) + 31) | 0;
        hashB = (((hashB << 7) - hashB) ^ 31) | 0;
    };
    const mixNumber = (value) => {
        mixString(Math.round(Number(value || 0) * 100) / 100);
    };

    for (const command of displayList?.commands || []) {
        if (Number(command?.pageIndex || 0) !== normalizedPageIndex || !isContentCacheCommand(command)) {
            continue;
        }

        count += 1;
        mixString(command.type);
        mixString(command.id);
        mixString(command.blockId);
        mixNumber(command.x);
        mixNumber(command.y);
        mixNumber(command.width);
        mixNumber(command.height);
        mixString(command.text || '');
        mixString(command.fill || '');
        mixString(command.stroke || '');
        // Rotation/flip do not change the axis-aligned x/y/width/height, so without them a rotate (or flip)
        // yielded an identical signature and the page was never repainted (the bitmap stayed upright).
        mixNumber(command.rotation);
        mixString(command.flipHorizontal === true ? 'fh' : '');
        mixString(command.flipVertical === true ? 'fv' : '');
    }

    return `${count}:${hashA >>> 0}:${hashB >>> 0}`;
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
