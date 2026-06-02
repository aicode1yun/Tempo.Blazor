// Phase D — core/canonical-document.mjs
// Self-contained canonical-document normaliser used by the runtime serialization
// layer (the Google-Docs migration facade). Data-in / data-out only — no DOM, no
// instance state, no runtime document store.
//
// The module keeps its own small Pascal/camel pair helpers and a deep object sort so
// its output is byte-identical to the legacy IIFE's `tmDocumentEditorRuntime.
// serialization` functions (the deep sort yields a stable key order for diffing).
//
// Public entry points:
//   - `fromCanonicalDocument(document)` → `{version:1, document:<normalised>}`
//   - `toCanonicalDocument(runtimeDocument)` → normalised document (unwraps envelope)
//   - `normalizeCanonicalDocument(document)` → normalised document
//   - `normalizeCanonicalSnapshot(snapshot)` → snapshot with normalised inner document
//   - `roundTripCanonicalDocument(document)` → `to(from(document))`
//   - `diffCanonicalDocuments(expected, actual)` → first structural difference or
//     `{equal:true,...}`
// Plus the per-node normalisers (normalizeInline(s)/Block(s)/…content) for testing.

function hasOwn(value, key) {
    return !!value && Object.prototype.hasOwnProperty.call(value, key);
}

function cloneJson(value) {
    if (value === undefined || value === null) return value;
    return JSON.parse(JSON.stringify(value));
}

function readPair(value, pascalKey, camelKey, fallback) {
    if (hasOwn(value, pascalKey)) return value[pascalKey];
    if (hasOwn(value, camelKey)) return value[camelKey];
    return fallback;
}

function writePair(value, pascalKey, camelKey, propertyValue) {
    if (!value) return;
    if (hasOwn(value, camelKey) && !hasOwn(value, pascalKey)) {
        value[camelKey] = propertyValue;
    } else {
        value[pascalKey] = propertyValue;
    }
}

function ensureArray(value, pascalKey, camelKey) {
    let current = readPair(value, pascalKey, camelKey, []);
    if (!Array.isArray(current)) current = [];
    writePair(value, pascalKey, camelKey, current);
    return current;
}

function ensureString(value, pascalKey, camelKey, fallback) {
    let current = readPair(value, pascalKey, camelKey, fallback || '');
    if (current === undefined || current === null || current === '') current = fallback || '';
    writePair(value, pascalKey, camelKey, String(current));
    return String(current);
}

export function sortObjectDeep(value) {
    if (Array.isArray(value)) {
        return value.map(sortObjectDeep);
    }
    if (!value || typeof value !== 'object') return value;
    const sorted = {};
    Object.keys(value).sort().forEach(function (key) {
        sorted[key] = sortObjectDeep(value[key]);
    });
    return sorted;
}

export function normalizeWrapContourPoints(points) {
    if (!Array.isArray(points)) return [];
    return points.map(function (point) {
        const x = Number(point && (point.X ?? point.x));
        const y = Number(point && (point.Y ?? point.y));
        return {
            X: Math.max(0, Math.min(1, Number.isFinite(x) ? x : 0)),
            Y: Math.max(0, Math.min(1, Number.isFinite(y) ? y : 0)),
        };
    });
}

export function stableNodeId(prefix, path) {
    return 'rt-' + prefix + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

export function getSnapshotDocument(snapshot) {
    if (!snapshot) return {};
    return snapshot.Document || snapshot.document || snapshot;
}

export function setSnapshotDocument(snapshot, document) {
    if (!snapshot) return;
    if (hasOwn(snapshot, 'document') && !hasOwn(snapshot, 'Document')) {
        snapshot.document = document;
    } else {
        snapshot.Document = document;
    }
}

export function looksLikeTextInline(inline) {
    if (!inline) return false;
    if (inline.NoteId !== undefined || inline.noteId !== undefined || inline.NoteType !== undefined || inline.noteType !== undefined) return false;
    if (inline.FieldType !== undefined || inline.fieldType !== undefined || inline.FallbackText !== undefined || inline.fallbackText !== undefined) return false;
    if (inline.Key !== undefined || inline.key !== undefined || inline.TokenType !== undefined || inline.tokenType !== undefined) return false;
    const type = String(inline.$type || inline.Type || inline.type || '').toLowerCase();
    return type === 'text' || type === 'textrun' || type.indexOf('text') >= 0 || hasOwn(inline, 'Text') || hasOwn(inline, 'text');
}

function readInlineText(inline) {
    return String(readPair(inline, 'Text', 'text', ''));
}

function writeInlineText(inline, text) {
    writePair(inline, 'Text', 'text', text);
}

function readInlineMarks(inline) {
    const marks = readPair(inline, 'Marks', 'marks', []);
    return Array.isArray(marks) ? marks : [];
}

function writeInlineMarks(inline, marks) {
    writePair(inline, 'Marks', 'marks', marks);
}

export function normalizeInline(inline, path) {
    const result = inline ? cloneJson(inline) : {};
    ensureString(result, 'Id', 'id', stableNodeId('inline', path));
    const marks = readInlineMarks(result).map(function (mark) { return sortObjectDeep(cloneJson(mark)); });
    writeInlineMarks(result, marks);

    if (result.NoteId !== undefined || result.noteId !== undefined || result.NoteType !== undefined || result.noteType !== undefined) {
        if (!result.$type && !hasOwn(result, 'Type') && !hasOwn(result, 'type')) {
            result.$type = 'noteReference';
        }
        return result;
    }

    if (result.FieldType !== undefined || result.fieldType !== undefined || result.FallbackText !== undefined || result.fallbackText !== undefined) {
        if (!result.$type && !hasOwn(result, 'Type') && !hasOwn(result, 'type')) {
            result.$type = 'field';
        }
        return result;
    }

    if (result.Key !== undefined || result.key !== undefined || result.TokenType !== undefined || result.tokenType !== undefined) {
        if (!result.$type && !hasOwn(result, 'Type') && !hasOwn(result, 'type')) {
            result.$type = 'token';
        }
        return result;
    }

    if (looksLikeTextInline(result)) {
        if (!result.$type && !hasOwn(result, 'Type') && !hasOwn(result, 'type')) {
            result.$type = 'text';
        }
        writeInlineText(result, readInlineText(result));
    }

    return result;
}

export function inlineMergeKey(inline) {
    const clone = cloneJson(inline) || {};
    delete clone.Id;
    delete clone.id;
    delete clone.Text;
    delete clone.text;
    return JSON.stringify(sortObjectDeep(clone));
}

export function canMergeInlineRuns(previous, current) {
    return looksLikeTextInline(previous)
        && looksLikeTextInline(current)
        && inlineMergeKey(previous) === inlineMergeKey(current);
}

function createEmptyTextInline(path) {
    return {
        $type: 'text',
        Id: stableNodeId('inline', path),
        Marks: [],
        Text: '',
    };
}

export function normalizeInlines(inlines, path) {
    const source = Array.isArray(inlines) ? inlines : [];
    const result = [];
    for (let i = 0; i < source.length; i++) {
        const normalized = normalizeInline(source[i], path + '-' + i);
        const previous = result.length > 0 ? result[result.length - 1] : null;
        if (previous && canMergeInlineRuns(previous, normalized)) {
            writeInlineText(previous, readInlineText(previous) + readInlineText(normalized));
        } else {
            result.push(normalized);
        }
    }

    if (result.length === 0) {
        result.push(createEmptyTextInline(path + '-0'));
    }

    return result.map(sortObjectDeep);
}

export function contentKind(content) {
    if (!content) return '';
    const raw = content.$type || content.Type || content.type || '';
    return String(raw).toLowerCase();
}

export function looksLikeParagraphContent(content) {
    return !!content
        && (contentKind(content).indexOf('paragraph') >= 0
            || hasOwn(content, 'Inlines')
            || hasOwn(content, 'inlines'));
}

export function looksLikeTableContent(content) {
    return !!content
        && (contentKind(content).indexOf('table') >= 0
            || hasOwn(content, 'Rows')
            || hasOwn(content, 'rows'));
}

export function looksLikeImageContent(content) {
    return !!content
        && (contentKind(content).indexOf('image') >= 0
            || hasOwn(content, 'Url')
            || hasOwn(content, 'url')
            || hasOwn(content, 'AssetId')
            || hasOwn(content, 'assetId'));
}

export function normalizeParagraphContent(content, path) {
    const result = content ? cloneJson(content) : {};
    if (!result.$type && !hasOwn(result, 'Type') && !hasOwn(result, 'type')) {
        result.$type = 'paragraph';
    }
    const inlines = ensureArray(result, 'Inlines', 'inlines');
    writePair(result, 'Inlines', 'inlines', normalizeInlines(inlines, path + '-inline'));
    return sortObjectDeep(result);
}

export function normalizeTableContent(content, path) {
    const result = content ? cloneJson(content) : {};
    const rows = ensureArray(result, 'Rows', 'rows');
    for (let r = 0; r < rows.length; r++) {
        const row = rows[r] ? cloneJson(rows[r]) : {};
        ensureString(row, 'Id', 'id', stableNodeId('row', path + '-' + r));
        const cells = ensureArray(row, 'Cells', 'cells');
        for (let c = 0; c < cells.length; c++) {
            const cell = cells[c] ? cloneJson(cells[c]) : {};
            ensureString(cell, 'Id', 'id', stableNodeId('cell', path + '-' + r + '-' + c));
            const blocks = ensureArray(cell, 'Blocks', 'blocks');
            writePair(cell, 'Blocks', 'blocks', normalizeBlocks(blocks, path + '-' + r + '-' + c + '-block'));
            cells[c] = sortObjectDeep(cell);
        }
        writePair(row, 'Cells', 'cells', cells);
        rows[r] = sortObjectDeep(row);
    }
    writePair(result, 'Rows', 'rows', rows);
    return sortObjectDeep(result);
}

export function normalizeImageContent(content) {
    const result = content ? cloneJson(content) : {};
    if (!result.$type && !hasOwn(result, 'Type') && !hasOwn(result, 'type')) {
        result.$type = 'image';
    }

    let layout = result.Layout || result.layout || null;
    const legacy = result.FloatingLayout || result.floatingLayout || null;
    if (!layout && legacy) {
        const inline = (legacy.Inline ?? legacy.inline) !== false;
        layout = {
            Kind: inline ? 0 : 1,
            Anchor: {
                MoveWithText: !inline,
                FixedOnPage: false,
                LockAnchor: !!(legacy.LockAnchor ?? legacy.lockAnchor),
            },
            Position: {
                HorizontalRelativeTo: legacy.HorizontalRelativeTo ?? legacy.horizontalRelativeTo ?? 0,
                VerticalRelativeTo: legacy.VerticalRelativeTo ?? legacy.verticalRelativeTo ?? 3,
                X: legacy.X ?? legacy.x ?? 0,
                Y: legacy.Y ?? legacy.y ?? 0,
            },
            Wrap: {
                Mode: legacy.WrapMode ?? legacy.wrapMode ?? 0,
                DistanceLeft: legacy.DistanceLeft ?? legacy.distanceLeft ?? 0,
                DistanceRight: legacy.DistanceRight ?? legacy.distanceRight ?? 0,
                DistanceTop: legacy.DistanceTop ?? legacy.distanceTop ?? 0,
                DistanceBottom: legacy.DistanceBottom ?? legacy.distanceBottom ?? 0,
                WrapContourPoints: normalizeWrapContourPoints(legacy.WrapContourPoints ?? legacy.wrapContourPoints),
            },
            Transform: {},
            Stacking: {
                ZIndex: legacy.ZIndex ?? legacy.zIndex ?? 0,
                AllowOverlap: (legacy.AllowOverlap ?? legacy.allowOverlap) === true
                    || String(legacy.AllowOverlap ?? legacy.allowOverlap ?? '').toLowerCase() === 'true',
            },
        };
        if (legacy.HorizontalPosition != null || legacy.horizontalPosition != null) {
            layout.Position.HorizontalAlignment = legacy.HorizontalPosition ?? legacy.horizontalPosition;
        }
    }

    if (layout) {
        writePair(result, 'Layout', 'layout', sortObjectDeep(layout));
    }

    delete result.FloatingLayout;
    delete result.floatingLayout;
    return sortObjectDeep(result);
}

export function normalizeBlockContent(content, path) {
    if (!content) return normalizeParagraphContent({}, path);
    if (looksLikeTableContent(content)) return normalizeTableContent(content, path);
    if (looksLikeImageContent(content)) return normalizeImageContent(content);
    if (looksLikeParagraphContent(content)) return normalizeParagraphContent(content, path);
    return sortObjectDeep(cloneJson(content));
}

export function normalizeBlock(block, path) {
    const result = block ? cloneJson(block) : {};
    ensureString(result, 'Id', 'id', stableNodeId('block', path));
    const content = readPair(result, 'Content', 'content', null);
    writePair(result, 'Content', 'content', normalizeBlockContent(content, path + '-content'));
    return sortObjectDeep(result);
}

export function normalizeBlocks(blocks, path) {
    const source = Array.isArray(blocks) ? blocks : [];
    return source.map(function (block, index) {
        return normalizeBlock(block, path + '-' + index);
    });
}

export function normalizeHeaderFooter(headerFooter, path) {
    const result = headerFooter ? cloneJson(headerFooter) : {};
    ensureString(result, 'Id', 'id', stableNodeId('header-footer', path));
    const blocks = ensureArray(result, 'Blocks', 'blocks');
    writePair(result, 'Blocks', 'blocks', normalizeBlocks(blocks, path + '-block'));
    return sortObjectDeep(result);
}

export function normalizeCanonicalDocument(document) {
    let result = document ? cloneJson(document) : {};
    if (hasOwn(result, 'document') || hasOwn(result, 'Document')) {
        result = getSnapshotDocument(result);
    }

    writePair(result, 'SchemaVersion', 'schemaVersion', readPair(result, 'SchemaVersion', 'schemaVersion', 1) || 1);
    ensureString(result, 'DocumentId', 'documentId', 'document');
    writePair(result, 'Metadata', 'metadata', readPair(result, 'Metadata', 'metadata', {}) || {});
    writePair(result, 'PageSettings', 'pageSettings', readPair(result, 'PageSettings', 'pageSettings', {}) || {});

    const sections = ensureArray(result, 'Sections', 'sections');
    writePair(result, 'Sections', 'sections', sections.map(function (section, index) {
        const normalized = section ? cloneJson(section) : {};
        ensureString(normalized, 'Id', 'id', stableNodeId('section', index));
        return sortObjectDeep(normalized);
    }));

    const blocks = ensureArray(result, 'Blocks', 'blocks');
    writePair(result, 'Blocks', 'blocks', normalizeBlocks(blocks, 'block'));

    const comments = ensureArray(result, 'Comments', 'comments');
    writePair(result, 'Comments', 'comments', comments.map(function (comment) { return sortObjectDeep(cloneJson(comment)); }));

    const notes = ensureArray(result, 'Notes', 'notes');
    writePair(result, 'Notes', 'notes', notes.map(function (note) { return sortObjectDeep(cloneJson(note)); }));

    const headersFooters = ensureArray(result, 'HeadersFooters', 'headersFooters');
    writePair(result, 'HeadersFooters', 'headersFooters', headersFooters.map(normalizeHeaderFooter));

    const revisions = ensureArray(result, 'Revisions', 'revisions');
    writePair(result, 'Revisions', 'revisions', revisions.map(function (revision) { return sortObjectDeep(cloneJson(revision)); }));

    const assets = ensureArray(result, 'Assets', 'assets');
    writePair(result, 'Assets', 'assets', assets.map(function (asset) { return sortObjectDeep(cloneJson(asset)); }));

    const anchors = ensureArray(result, 'Anchors', 'anchors');
    writePair(result, 'Anchors', 'anchors', anchors.map(function (anchor) { return sortObjectDeep(cloneJson(anchor)); }));

    return sortObjectDeep(result);
}

export function fromCanonicalDocument(document) {
    return sortObjectDeep({
        version: 1,
        document: normalizeCanonicalDocument(document),
    });
}

export function toCanonicalDocument(runtimeDocument) {
    if (!runtimeDocument) return normalizeCanonicalDocument({});
    const document = hasOwn(runtimeDocument, 'document') || hasOwn(runtimeDocument, 'Document')
        ? getSnapshotDocument(runtimeDocument)
        : runtimeDocument;
    return normalizeCanonicalDocument(document);
}

export function normalizeCanonicalSnapshot(snapshot) {
    const result = snapshot ? cloneJson(snapshot) : {};
    const document = getSnapshotDocument(result);
    setSnapshotDocument(result, toCanonicalDocument(document));
    if (!hasOwn(result, 'ProtocolVersion') && !hasOwn(result, 'protocolVersion')) {
        result.ProtocolVersion = 1;
    }
    return sortObjectDeep(result);
}

export function stripRuntimeFields(value) {
    if (Array.isArray(value)) return value.map(stripRuntimeFields);
    if (!value || typeof value !== 'object') return value;

    const result = {};
    Object.keys(value).sort().forEach(function (key) {
        if (key.indexOf('__runtime') === 0 || key.indexOf('_runtime') === 0) return;
        result[key] = stripRuntimeFields(value[key]);
    });
    return result;
}

export function findFirstDifference(expected, actual, path) {
    if (expected === actual) return null;
    if (typeof expected !== typeof actual) {
        return { path: path || '$', expected, actual };
    }
    if (expected === null || actual === null || typeof expected !== 'object') {
        return { path: path || '$', expected, actual };
    }

    const expectedKeys = Array.isArray(expected) ? expected.map(function (_, index) { return index; }) : Object.keys(expected).sort();
    const actualKeys = Array.isArray(actual) ? actual.map(function (_, index) { return index; }) : Object.keys(actual).sort();
    const keys = Array.from(new Set(expectedKeys.concat(actualKeys))).sort(function (a, b) {
        return String(a).localeCompare(String(b), undefined, { numeric: true });
    });

    for (let i = 0; i < keys.length; i++) {
        const key = keys[i];
        if (!hasOwn(expected, key) || !hasOwn(actual, key)) {
            return { path: (path || '$') + '.' + key, expected: expected[key], actual: actual[key] };
        }
        const diff = findFirstDifference(expected[key], actual[key], (path || '$') + '.' + key);
        if (diff) return diff;
    }

    return null;
}

export function diffCanonicalDocuments(expected, actual) {
    const left = stripRuntimeFields(toCanonicalDocument(expected));
    const right = stripRuntimeFields(toCanonicalDocument(actual));
    const diff = findFirstDifference(left, right, '$');
    return diff || { equal: true, path: '$', expected: left, actual: right };
}

export function roundTripCanonicalDocument(document) {
    return toCanonicalDocument(fromCanonicalDocument(document));
}
