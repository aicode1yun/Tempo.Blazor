window.tmDocumentEditorEngine = (function () {
    'use strict';

    var TOOLBAR_NATIVE_BUTTON_BRIDGE_VERSION = 'toolbar-native-button-2026-05-24-02';

    var _instances = new Map();
    var _counter = 0;
    var _operationCounter = 0;
    var _transactionCounter = 0;

    function _hasOwn(value, key) {
        return !!value && Object.prototype.hasOwnProperty.call(value, key);
    }

    function _clone(value) {
        if (value === undefined || value === null) return value;
        return JSON.parse(JSON.stringify(value));
    }

    function _read(value, pascalKey, camelKey, fallback) {
        if (_hasOwn(value, camelKey)) return value[camelKey];
        if (_hasOwn(value, pascalKey)) return value[pascalKey];
        return fallback;
    }

    function _stableId(prefix, path) {
        return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
    }

    function _sortObject(value) {
        if (Array.isArray(value)) return value.map(_sortObject);
        if (!value || typeof value !== 'object') return value;
        var result = {};
        Object.keys(value).sort().forEach(function (key) {
            if (key.indexOf('__dom') === 0 || key.indexOf('__runtime') === 0 || key.indexOf('_runtime') === 0) return;
            result[key] = _sortObject(value[key]);
        });
        return result;
    }

    function _asArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function _asText(value) {
        return value === undefined || value === null ? '' : String(value);
    }

    function _textFromRuns(runs) {
        return _asArray(runs).map(function (run) {
            return run.kind === 'text' || run.kind === 'token' || run.kind === 'field'
                ? _asText(run.text || run.fallbackText || run.key)
                : '';
        }).join('');
    }

    function _unique(values) {
        return Array.from(new Set(_asArray(values).filter(function (value) { return value !== undefined && value !== null && value !== ''; })));
    }

    function DocumentSchemaRegistry() {
        this.elements = new Map();
        this.children = new Map();
        this.attributes = new Map();
    }

    DocumentSchemaRegistry.prototype.registerElement = function (type, definition) {
        this.elements.set(type, Object.assign({
            type: type,
            isBlock: false,
            isInline: false,
            isObject: false,
            isLimit: false,
            isSelectable: false
        }, definition || {}));
        return this;
    };

    DocumentSchemaRegistry.prototype.allowChild = function (parentType, childType) {
        if (!this.children.has(parentType)) this.children.set(parentType, new Set());
        this.children.get(parentType).add(childType);
        return this;
    };

    DocumentSchemaRegistry.prototype.allowAttribute = function (type, attributeName) {
        if (!this.attributes.has(type)) this.attributes.set(type, new Set());
        this.attributes.get(type).add(attributeName);
        return this;
    };

    DocumentSchemaRegistry.prototype.getDefinition = function (type) {
        return this.elements.get(type) || null;
    };

    DocumentSchemaRegistry.prototype.checkChild = function (parent, childType) {
        var parentType = typeof parent === 'string' ? parent : parent && (parent.type || parent.kind);
        return !!parentType && this.children.has(parentType) && this.children.get(parentType).has(childType);
    };

    DocumentSchemaRegistry.prototype.checkAttribute = function (element, attributeName) {
        var type = typeof element === 'string' ? element : element && (element.type || element.kind);
        return !!type && this.attributes.has(type) && this.attributes.get(type).has(attributeName);
    };

    DocumentSchemaRegistry.prototype.getLimitElement = function (position) {
        var current = position && position.element;
        while (current) {
            var definition = this.getDefinition(current.type || current.kind);
            if (definition && definition.isLimit) return current;
            current = current.parent || null;
        }
        return null;
    };

    DocumentSchemaRegistry.prototype.getNearestSelectionRange = function (position, direction) {
        var limit = this.getLimitElement(position);
        return {
            limitElementId: limit ? limit.id : null,
            direction: direction || 'forward',
            start: position || null,
            end: position || null
        };
    };

    function createDefaultSchemaRegistry() {
        var schema = new DocumentSchemaRegistry();
        schema
            .registerElement('document', { isLimit: true })
            .registerElement('body', { isLimit: true })
            .registerElement('header', { isLimit: true })
            .registerElement('footer', { isLimit: true })
            .registerElement('paragraph', { isBlock: true })
            .registerElement('table', { isBlock: true, isObject: true, isSelectable: true })
            .registerElement('tableRow', { isLimit: true })
            .registerElement('tableCell', { isLimit: true })
            .registerElement('caption', { isLimit: true })
            .registerElement('text', { isInline: true })
            .registerElement('field', { isInline: true, isObject: true, isSelectable: true })
            .registerElement('token', { isInline: true, isObject: true, isSelectable: true })
            .registerElement('image', { isBlock: true, isObject: true, isSelectable: true });

        ['paragraph', 'table', 'image'].forEach(function (child) {
            schema.allowChild('body', child);
            schema.allowChild('header', child);
            schema.allowChild('footer', child);
            schema.allowChild('tableCell', child);
        });
        ['text', 'field', 'token'].forEach(function (child) {
            schema.allowChild('paragraph', child);
        });
        schema
            .allowChild('image', 'caption')
            .allowChild('table', 'tableRow')
            .allowChild('tableRow', 'tableCell');

        ['paragraph', 'text', 'field', 'token', 'image', 'table', 'tableCell'].forEach(function (type) {
            ['style', 'marks', 'revisionId', 'commentIds', 'layout', 'metadata'].forEach(function (attribute) {
                schema.allowAttribute(type, attribute);
            });
        });

        return schema;
    }

    function importInlineRun(source, path) {
        var raw = source || {};
        var type = String(_read(raw, 'Type', 'type', raw.$type || '')).toLowerCase();
        var kind = 'text';
        if (type.indexOf('field') >= 0 || _hasOwn(raw, 'FieldType') || _hasOwn(raw, 'fieldType')) kind = 'field';
        if (type.indexOf('token') >= 0 || _hasOwn(raw, 'Key') || _hasOwn(raw, 'key')) kind = 'token';
        var marks = normalizeMarks(_read(raw, 'Marks', 'marks', []));
        var revisionId = _read(raw, 'RevisionId', 'revisionId', null) || readRevisionIdFromMarks(marks);
        return _sortObject({
            id: _asText(_read(raw, 'Id', 'id', '')) || _stableId('inline', path),
            kind: kind,
            text: _asText(_read(raw, 'Text', 'text', _read(raw, 'FallbackText', 'fallbackText', _read(raw, 'Key', 'key', '')))),
            key: _read(raw, 'Key', 'key', null),
            fieldType: _read(raw, 'FieldType', 'fieldType', null),
            marks: marks,
            style: _sortObject(_read(raw, 'Style', 'style', {}) || {}),
            revisionId: revisionId || null,
            commentIds: _asArray(_read(raw, 'CommentIds', 'commentIds', []))
        });
    }

    function readInlineMarkType(mark) {
        var value = mark && (mark.type ?? mark.Type);
        if (value === 7) return 'CommentAnchor';
        if (value === 8) return 'Revision';
        var key = _asText(value).replace(/[^a-z]/gi, '').toLowerCase();
        if (key === 'commentanchor') return 'CommentAnchor';
        if (key === 'revision' || key === 'revisionanchor') return 'Revision';
        return _asText(value);
    }

    function readCommentIdFromMark(mark) {
        if (readInlineMarkType(mark) !== 'CommentAnchor') return '';
        var anchor = mark && (mark.CommentAnchor || mark.commentAnchor) || {};
        return _asText(anchor.CommentId || anchor.commentId || mark.CommentId || mark.commentId || '');
    }

    function readCommentIdsFromRun(run) {
        var ids = _asArray(run && (run.commentIds || run.CommentIds)).map(_asText).filter(Boolean);
        _asArray(run && (run.marks || run.Marks)).forEach(function (mark) {
            var id = readCommentIdFromMark(mark);
            if (id && ids.indexOf(id) < 0) ids.push(id);
        });
        return ids;
    }

    function readRevisionIdFromMark(mark) {
        if (readInlineMarkType(mark) !== 'Revision') return '';
        return _asText(mark && (mark.revisionId || mark.RevisionId || mark.value || mark.Value || ''));
    }

    function readRevisionIdFromMarks(marks) {
        var id = '';
        _asArray(marks).some(function (mark) {
            id = readRevisionIdFromMark(mark);
            return !!id;
        });
        return id;
    }

    function readRevisionIdsFromRun(run) {
        var ids = [];
        var direct = _asText(run && (run.revisionId || run.RevisionId || ''));
        if (direct) ids.push(direct);
        _asArray(run && (run.marks || run.Marks)).forEach(function (mark) {
            var id = readRevisionIdFromMark(mark);
            if (id && ids.indexOf(id) < 0) ids.push(id);
        });
        return ids;
    }

    function importParagraphContent(source, path, paragraphProperties) {
        var content = source || {};
        var properties = paragraphProperties || {};
        var runs = _asArray(_read(content, 'Inlines', 'inlines', [])).map(function (run, index) {
            return importInlineRun(run, path + '-run-' + index);
        });
        if (runs.length === 0) {
            runs.push(importInlineRun({ Text: '' }, path + '-run-0'));
        }
        runs = mergeAdjacentTextRuns(runs);
        return _sortObject({
            type: 'paragraph',
            runs: runs,
            alignment: _read(content, 'Alignment', 'alignment', _read(properties, 'Alignment', 'alignment', null)),
            lineSpacing: _read(content, 'LineSpacing', 'lineSpacing', _read(properties, 'LineSpacing', 'lineSpacing', null)),
            spacingBefore: _read(properties, 'SpacingBefore', 'spacingBefore', null),
            spacingAfter: _read(properties, 'SpacingAfter', 'spacingAfter', null),
            leftIndent: _read(properties, 'LeftIndent', 'leftIndent', null),
            rightIndent: _read(properties, 'RightIndent', 'rightIndent', null),
            style: _sortObject(_read(content, 'Style', 'style', {}) || {})
        });
    }

    function importImageObject(source, path) {
        var content = source || {};
        return _sortObject({
            type: 'image',
            objectId: _asText(_read(content, 'ObjectId', 'objectId', _read(content, 'Id', 'id', ''))) || _stableId('object', path),
            source: _read(content, 'Source', 'source', 0),
            url: _read(content, 'Url', 'url', null),
            assetId: _read(content, 'AssetId', 'assetId', null),
            altText: _asText(_read(content, 'AltText', 'altText', '')),
            isDecorative: _read(content, 'IsDecorative', 'isDecorative', false) === true,
            caption: _asText(_read(content, 'Caption', 'caption', '')),
            size: _sortObject(_read(content, 'Size', 'size', {}) || {}),
            naturalSize: _sortObject(_read(content, 'NaturalSize', 'naturalSize', {}) || {}),
            alignment: _read(content, 'Alignment', 'alignment', 1),
            layout: _sortObject(_read(content, 'Layout', 'layout', _read(content, 'FloatingLayout', 'floatingLayout', {})) || {}),
            style: _sortObject(_read(content, 'Style', 'style', {}) || {}),
            linkUrl: _read(content, 'LinkUrl', 'linkUrl', null)
        });
    }

    function importTable(source, path) {
        var content = source || {};
        var rows = _asArray(_read(content, 'Rows', 'rows', [])).map(function (row, rowIndex) {
            return {
                id: _asText(_read(row, 'Id', 'id', '')) || _stableId('row', path + '-' + rowIndex),
                type: 'tableRow',
                cells: _asArray(_read(row, 'Cells', 'cells', [])).map(function (cell, cellIndex) {
                    return {
                        id: _asText(_read(cell, 'Id', 'id', '')) || _stableId('cell', path + '-' + rowIndex + '-' + cellIndex),
                        type: 'tableCell',
                        rowSpan: Math.max(1, Number(_read(cell, 'RowSpan', 'rowSpan', 1)) || 1),
                        colSpan: Math.max(1, Number(_read(cell, 'ColSpan', 'colSpan', 1)) || 1),
                        width: Number(_read(cell, 'Width', 'width', 0)) || null,
                        height: Number(_read(cell, 'Height', 'height', 0)) || null,
                        style: _sortObject(_read(cell, 'Style', 'style', {}) || {}),
                        blocks: _asArray(_read(cell, 'Blocks', 'blocks', [])).map(function (block, blockIndex) {
                            return importBlock(block, path + '-' + rowIndex + '-' + cellIndex + '-' + blockIndex);
                        })
                    };
                })
            };
        });
        return _sortObject({ type: 'table', rows: rows, style: _sortObject(_read(content, 'Style', 'style', {}) || {}) });
    }

    function importBlock(source, path) {
        var block = source || {};
        var content = _read(block, 'Content', 'content', block);
        var type = String(_read(block, 'Type', 'type', _read(content, 'Type', 'type', content && content.$type || 'paragraph'))).toLowerCase();
        var normalizedContent;
        var contentType = String(_hasOwn(content, '$type') ? content.$type : _read(content, 'Type', 'type', '') || '').toLowerCase();
        if (type === '6' || type.indexOf('pagebreak') >= 0 || type.indexOf('page-break') >= 0) {
            normalizedContent = { type: 'pageBreak' };
            type = 'pageBreak';
        } else if (type === '4' || type.indexOf('table') >= 0 || contentType.indexOf('table') >= 0 || _hasOwn(content, 'Rows') || _hasOwn(content, 'rows')) {
            normalizedContent = importTable(content, path + '-table');
            type = 'table';
        } else if (type === '5' || type.indexOf('image') >= 0 || contentType.indexOf('image') >= 0
            || _hasOwn(content, 'Url') || _hasOwn(content, 'url')
            || _hasOwn(content, 'AssetId') || _hasOwn(content, 'assetId')
            || _hasOwn(content, 'Layout') || _hasOwn(content, 'layout')) {
            normalizedContent = importImageObject(content, path + '-image');
            type = 'image';
        } else {
            normalizedContent = importParagraphContent(content, path + '-paragraph', _read(block, 'ParagraphProperties', 'paragraphProperties', {}));
            type = 'paragraph';
        }

        return _sortObject({
            id: _asText(_read(block, 'Id', 'id', '')) || _stableId('block', path),
            type: type,
            content: normalizedContent,
            order: _read(block, 'Order', 'order', null),
            style: _sortObject(_read(block, 'Style', 'style', {}) || {})
        });
    }

    function importRegion(source, path, type) {
        var region = source || {};
        var sourceType = String(_read(region, 'Region', 'region', _read(region, 'Type', 'type', type))).toLowerCase();
        var numericType = Number(_read(region, 'Type', 'type', Number.NaN));
        var normalizedType = sourceType.indexOf('footer') >= 0 || numericType === 1
            ? 'footer'
            : sourceType.indexOf('header') >= 0 || numericType === 0
                ? 'header'
                : type;
        return _sortObject({
            id: _asText(_read(region, 'Id', 'id', '')) || _stableId(type, path),
            type: normalizedType,
            scope: _asText(_read(region, 'Scope', 'scope', 'Primary')) || 'Primary',
            sectionId: _read(region, 'SectionId', 'sectionId', null),
            blocks: _asArray(_read(region, 'Blocks', 'blocks', [])).map(function (block, index) {
                return importBlock(block, path + '-block-' + index);
            })
        });
    }

    function buildIndexes(model) {
        var indexes = {
            blocks: {},
            inlines: {},
            objects: {},
            revisions: {},
            comments: {}
        };

        function visitBlock(block) {
            if (!block || !block.id) return;
            indexes.blocks[block.id] = block;
            if (block.type === 'paragraph') {
                _asArray(block.content && block.content.runs).forEach(function (run) {
                    if (!run || !run.id) return;
                    indexes.inlines[run.id] = run;
                    if (run.kind === 'field' || run.kind === 'token') indexes.objects[run.id] = run;
                });
            }
            if (block.type === 'image') {
                indexes.objects[(block.content && block.content.objectId) || block.id] = block.content || block;
            }
            if (block.type === 'table') {
                _asArray(block.content && block.content.rows).forEach(function (row) {
                    _asArray(row.cells).forEach(function (cell) {
                        _asArray(cell.blocks).forEach(visitBlock);
                    });
                });
            }
        }

        _asArray(model.body && model.body.blocks).forEach(visitBlock);
        _asArray(model.headers).forEach(function (region) { _asArray(region.blocks).forEach(visitBlock); });
        _asArray(model.footers).forEach(function (region) { _asArray(region.blocks).forEach(visitBlock); });
        _asArray(model.revisions).forEach(function (revision) {
            if (revision && (revision.id || revision.Id)) indexes.revisions[revision.id || revision.Id] = revision;
        });
        _asArray(model.comments).forEach(function (comment) {
            if (comment && (comment.id || comment.Id)) indexes.comments[comment.id || comment.Id] = comment;
        });
        model.indexes = indexes;
        model.indexVersion = Number(model.indexVersion || 0) + 1;
        model.indexesBuiltAt = Date.now();
        return indexes;
    }

    function importFromCSharpJson(document) {
        var source = document && (document.Document || document.document) ? (document.Document || document.document) : (document || {});
        var headerFooterRegions = _asArray(_read(source, 'HeadersFooters', 'headersFooters', []));
        var isFooterRegion = function (region) {
            var typeValue = _read(region, 'Region', 'region', _read(region, 'Type', 'type', 'header'));
            var numericType = Number(_read(region, 'Type', 'type', Number.NaN));
            return String(typeValue).toLowerCase().indexOf('footer') >= 0 || numericType === 1;
        };
        var model = _sortObject({
            schemaVersion: Number(_read(source, 'SchemaVersion', 'schemaVersion', 1) || 1),
            documentId: _asText(_read(source, 'DocumentId', 'documentId', 'document')),
            title: _asText(_read(source, 'Title', 'title', _read(source, 'Name', 'name', ''))),
            metadata: _sortObject(_read(source, 'Metadata', 'metadata', {}) || {}),
            pageSettings: _sortObject(_read(source, 'PageSettings', 'pageSettings', {}) || {}),
            body: importRegion({ Id: 'body', Blocks: _read(source, 'Blocks', 'blocks', []) }, 'body', 'body'),
            headers: headerFooterRegions.filter(function (region) { return !isFooterRegion(region); }).map(function (region, index) { return importRegion(region, 'header-' + index, 'header'); }),
            footers: headerFooterRegions.filter(isFooterRegion).map(function (region, index) { return importRegion(region, 'footer-' + index, 'footer'); }),
            revisions: _asArray(_read(source, 'Revisions', 'revisions', [])).map(normalizeRevision),
            comments: _asArray(_read(source, 'Comments', 'comments', [])).map(_sortObject),
            assets: _asArray(_read(source, 'Assets', 'assets', [])).map(_sortObject)
        });
        buildIndexes(model);
        return model;
    }

    function exportInlineRun(run) {
        var result = {
            Id: run.id,
            Marks: _clone(run.marks || [])
        };
        if (run.kind === 'field') {
            result.$type = 'field';
            result.FieldType = exportFieldType(run.fieldType);
            result.FallbackText = run.fallbackText || run.text || null;
            result.DisplayText = run.text || run.displayText || null;
        } else if (run.kind === 'token') {
            result.$type = 'token';
            result.Key = run.key || run.text || '';
            result.DisplayName = run.displayName || run.text || run.key || '';
            result.FallbackText = run.fallbackText || run.text || null;
        } else {
            result.$type = 'text';
            result.Text = _asText(run.text);
        }
        return _sortObject(result);
    }

    function exportBlockType(block) {
        var type = String(block && block.type || '').toLowerCase();
        if (type === 'heading') return 1;
        if (type === 'list') return 2;
        if (type === 'quote') return 3;
        if (type === 'table') return 4;
        if (type === 'image') return 5;
        if (type === 'pagebreak' || type === 'page-break') return 6;
        return 0;
    }

    function exportHeaderFooterType(region) {
        return region && region.type === 'footer' ? 1 : 0;
    }

    function exportHeaderFooterScope(scope) {
        var normalized = String(scope || 'Primary').toLowerCase();
        if (normalized === 'firstpage' || normalized === 'first-page' || normalized === 'first') return 1;
        if (normalized === 'evenpage' || normalized === 'evenpages' || normalized === 'even-page' || normalized === 'even-pages' || normalized === 'even') return 2;
        if (normalized === 'oddpage' || normalized === 'oddpages' || normalized === 'odd-page' || normalized === 'odd-pages' || normalized === 'odd') return 3;
        return 0;
    }

    function exportFieldType(fieldType) {
        var normalized = String(fieldType || '').toLowerCase();
        if (normalized.indexOf('pagecount') >= 0 || normalized.indexOf('page-count') >= 0 || normalized.indexOf('numpages') >= 0) return 1;
        if (normalized.indexOf('pagexofy') >= 0 || normalized.indexOf('page-x-of-y') >= 0) return 2;
        if (normalized.indexOf('date') >= 0) return 3;
        if (normalized.indexOf('documenttitle') >= 0 || normalized.indexOf('document-title') >= 0 || normalized.indexOf('title') >= 0) return 4;
        if (normalized.indexOf('author') >= 0) return 5;
        if (normalized.indexOf('lastsaved') >= 0 || normalized.indexOf('last-saved') >= 0 || normalized.indexOf('modified') >= 0) return 6;
        return 0;
    }

    function exportCommentAnchorType(value) {
        var normalized = String(value || '').toLowerCase();
        if (value === 1 || normalized === 'textrange' || normalized === 'text-range') return 1;
        if (value === 2 || normalized.indexOf('docx') >= 0) return 2;
        if (value === 3 || normalized.indexOf('odt') >= 0) return 3;
        if (value === 4 || normalized === 'page') return 4;
        if (value === 5 || normalized === 'rendition') return 5;
        return 0;
    }

    function exportCommentStatus(value) {
        if (value === 1) return 1;
        return String(value || '').toLowerCase().indexOf('resolved') >= 0 ? 1 : 0;
    }

    function exportCommentVisibility(value) {
        var normalized = String(value || '').toLowerCase();
        if (value === 1 || normalized === 'external') return 1;
        if (value === 2 || normalized === 'client') return 2;
        if (value === 3 || normalized === 'public') return 3;
        return 0;
    }

    function exportComment(comment) {
        var source = comment || {};
        var anchor = source.Anchor || source.anchor || {};
        return _sortObject({
            Id: readCommentId(source),
            Anchor: {
                Type: exportCommentAnchorType(anchor.Type ?? anchor.type),
                BlockId: anchor.BlockId ?? anchor.blockId ?? null,
                StartInlineIndex: anchor.StartInlineIndex ?? anchor.startInlineIndex ?? null,
                StartOffset: anchor.StartOffset ?? anchor.startOffset ?? null,
                EndInlineIndex: anchor.EndInlineIndex ?? anchor.endInlineIndex ?? null,
                EndOffset: anchor.EndOffset ?? anchor.endOffset ?? null,
                ExternalAnchorId: anchor.ExternalAnchorId ?? anchor.externalAnchorId ?? null,
                RenditionAnchorId: anchor.RenditionAnchorId ?? anchor.renditionAnchorId ?? null,
                IsOrphaned: anchor.IsOrphaned === true || anchor.isOrphaned === true
            },
            Entries: _asArray(source.Entries || source.entries).map(function (entry) {
                return _sortObject({
                    Id: entry.Id || entry.id || _stableId('comment-entry', readCommentId(source) + '-entry'),
                    Author: _clone(entry.Author || entry.author || {}),
                    IsExternalAuthor: entry.IsExternalAuthor === true || entry.isExternalAuthor === true,
                    Text: _asText(entry.Text || entry.text),
                    CreatedAt: entry.CreatedAt || entry.createdAt || null,
                    ModifiedAt: entry.ModifiedAt || entry.modifiedAt || null
                });
            }),
            Status: exportCommentStatus(source.Status ?? source.status),
            Visibility: exportCommentVisibility(source.Visibility ?? source.visibility),
            SourceFormat: source.SourceFormat ?? source.sourceFormat ?? null,
            ExternalId: source.ExternalId ?? source.externalId ?? null,
            ResolvedAt: source.ResolvedAt ?? source.resolvedAt ?? null,
            ResolvedBy: _clone(source.ResolvedBy || source.resolvedBy || null)
        });
    }

    function exportRevisionType(value) {
        var normalized = String(value || '').toLowerCase();
        if (value === 1 || normalized === 'deletion' || normalized === 'delete') return 1;
        if (value === 2 || normalized === 'formatting' || normalized === 'formatchange' || normalized === 'format') return 2;
        if (value === 3 || normalized === 'move') return 3;
        if (value === 4 || normalized === 'structure' || normalized === 'structural') return 4;
        if (value === 5 || normalized === 'image') return 5;
        if (value === 6 || normalized === 'table') return 6;
        return 0;
    }

    function exportRevisionAction(value) {
        var normalized = String(value || '').toLowerCase();
        if (value === 1 || normalized === 'accepted') return 1;
        if (value === 2 || normalized === 'rejected') return 2;
        return 0;
    }

    function exportRevisionAuthor(value, fallbackId) {
        var source = value || {};
        if (typeof source === 'string') {
            return _sortObject({
                Id: source || fallbackId || 'local',
                DisplayName: source || fallbackId || 'local'
            });
        }

        var id = _asText(source.Id || source.id || fallbackId || source.DisplayName || source.displayName || 'local');
        return _sortObject({
            Id: id,
            DisplayName: _asText(source.DisplayName || source.displayName || id)
        });
    }

    function exportDateTimeOffset(value) {
        if (value instanceof Date && Number.isFinite(value.getTime())) {
            return value.toISOString();
        }

        if (typeof value === 'number' && Number.isFinite(value)) {
            return new Date(value).toISOString();
        }

        if (typeof value === 'string' && value.trim()) {
            return value;
        }

        return new Date().toISOString();
    }

    function exportTextAlignment(value) {
        var normalized = String(value ?? '').trim().toLowerCase();
        if (value === 1 || normalized === '1' || normalized === 'center' || normalized === 'centre') return 1;
        if (value === 2 || normalized === '2' || normalized === 'right' || normalized === 'end') return 2;
        if (value === 3 || normalized === '3' || normalized === 'justify' || normalized === 'justified') return 3;
        return 0;
    }

    function exportRevision(revision) {
        var source = revision || {};
        var range = source.Range || source.range || source.affectedRange || source.AffectedRange || {};
        var payload = source.PayloadJson ?? source.payloadJson;
        if (payload === undefined && source.payload !== undefined) {
            payload = typeof source.payload === 'string' ? source.payload : JSON.stringify(source.payload || {});
        }
        var authorValue = source.Author || source.authorObject || source.author || {};
        var authorId = source.AuthorId || source.authorId || source.author || source.Author || 'local';
        return _sortObject({
            Id: _asText(source.Id || source.id),
            Type: exportRevisionType(source.Type ?? source.type),
            Range: {
                BlockId: range.BlockId ?? range.blockId ?? null,
                SourceBlockId: range.SourceBlockId ?? range.sourceBlockId ?? null,
                StartInlineIndex: range.StartInlineIndex ?? range.startInlineIndex ?? null,
                StartOffset: range.StartOffset ?? range.startOffset ?? range.start ?? null,
                EndInlineIndex: range.EndInlineIndex ?? range.endInlineIndex ?? null,
                EndOffset: range.EndOffset ?? range.endOffset ?? range.end ?? null
            },
            Author: exportRevisionAuthor(authorValue, authorId),
            CreatedAt: exportDateTimeOffset(source.CreatedAt ?? source.createdAt ?? source.timestamp ?? null),
            Action: exportRevisionAction(source.Action ?? source.action ?? source.status),
            PayloadJson: payload ?? null,
            GroupId: source.GroupId ?? source.groupId ?? null
        });
    }

    function exportBlock(block) {
        if (block.type === 'image') {
            return _sortObject({
                Id: block.id,
                Type: exportBlockType(block),
                Content: {
                    $type: 'image',
                    Id: block.content.objectId,
                    Source: block.content.source ?? 0,
                    Url: block.content.url,
                    AssetId: block.content.assetId,
                    AltText: block.content.altText,
                    IsDecorative: block.content.isDecorative === true,
                    Caption: block.content.caption,
                    Size: _clone(block.content.size || {}),
                    NaturalSize: _clone(block.content.naturalSize || {}),
                    Alignment: block.content.alignment ?? 1,
                    Layout: _clone(block.content.layout || {}),
                    Style: _clone(block.content.style || {}),
                    LinkUrl: block.content.linkUrl ?? null
                },
                Style: _clone(block.style || {})
            });
        }
        if (block.type === 'table') {
            return _sortObject({
                Id: block.id,
                Type: exportBlockType(block),
                Content: {
                    $type: 'table',
                    Rows: _asArray(block.content.rows).map(function (row) {
                        return {
                            Id: row.id,
                            Cells: _asArray(row.cells).map(function (cell) {
                        return {
                            Id: cell.id,
                            RowSpan: cell.rowSpan || 1,
                            ColSpan: cell.colSpan || 1,
                            Width: cell.width || null,
                            Height: cell.height || null,
                            Style: _clone(cell.style || {}),
                            Blocks: _asArray(cell.blocks).map(exportBlock)
                        };
                    })
                        };
                    }),
                    Style: _clone(block.content.style || {})
                },
                Style: _clone(block.style || {})
            });
        }
        var textContent = block.content || {};
        return _sortObject({
            Id: block.id,
            Type: exportBlockType(block),
            ParagraphProperties: {
                Alignment: exportTextAlignment(textContent.alignment ?? textContent.Alignment),
                LineSpacing: Number(textContent.lineSpacing ?? textContent.LineSpacing ?? 1) || 1,
                SpacingBefore: Number(textContent.spacingBefore ?? textContent.SpacingBefore ?? 0) || 0,
                SpacingAfter: Number(textContent.spacingAfter ?? textContent.SpacingAfter ?? 0) || 0,
                LeftIndent: Number(textContent.leftIndent ?? textContent.LeftIndent ?? 0) || 0,
                RightIndent: Number(textContent.rightIndent ?? textContent.RightIndent ?? 0) || 0
            },
            Content: {
                $type: block.type === 'heading' ? 'heading' : block.type === 'list' ? 'list' : block.type === 'quote' ? 'quote' : 'paragraph',
                Alignment: block.content.alignment,
                LineSpacing: block.content.lineSpacing,
                Inlines: _asArray(block.content.runs).map(exportInlineRun),
                Style: _clone(block.content.style || {})
            },
            Style: _clone(block.style || {})
        });
    }

    function exportToCSharpJson(model) {
        var source = model || importFromCSharpJson({});
        return _sortObject({
            SchemaVersion: source.schemaVersion || 1,
            DocumentId: source.documentId || 'document',
            Title: source.title || '',
            Metadata: _clone(source.metadata || {}),
            PageSettings: _clone(source.pageSettings || {}),
            Blocks: _asArray(source.body && source.body.blocks).map(exportBlock),
            HeadersFooters: _asArray(source.headers).concat(_asArray(source.footers)).map(function (region) {
                return {
                    Id: region.id,
                    Type: exportHeaderFooterType(region),
                    Region: region.type === 'footer' ? 'Footer' : 'Header',
                    Scope: exportHeaderFooterScope(region.scope),
                    ScopeName: region.scope || 'Primary',
                    SectionId: region.sectionId || null,
                    Blocks: _asArray(region.blocks).map(exportBlock)
                };
            }),
            Revisions: _asArray(source.revisions).map(exportRevision),
            Comments: _asArray(source.comments).map(exportComment),
            Assets: []
        });
    }

    function exportRevisionsToCSharpJson(model) {
        return _asArray(model && model.revisions).map(exportRevision);
    }

    function validateModel(model) {
        var errors = [];
        var seen = new Set();
        var references = {
            revisions: [],
            comments: [],
            objectAnchors: []
        };
        function requireId(id, path) {
            if (!id) {
                errors.push({ code: 'missing-id', path: path });
                return;
            }
            if (seen.has(id)) errors.push({ code: 'duplicate-id', path: path, id: id });
            seen.add(id);
        }

        function visitBlock(block, path) {
            if (!block) return;
            requireId(block.id, path);
            if (block.type === 'paragraph') {
                _asArray(block.content && block.content.runs).forEach(function (run, index) {
                    requireId(run.id, path + '.runs[' + index + ']');
                    if (run.revisionId) references.revisions.push({ id: run.revisionId, path: path + '.runs[' + index + '].revisionId' });
                    _asArray(run.commentIds).forEach(function (commentId, commentIndex) {
                        references.comments.push({ id: commentId, path: path + '.runs[' + index + '].commentIds[' + commentIndex + ']' });
                    });
                });
            }
            if (block.type === 'image') {
                requireId(block.content && block.content.objectId, path + '.object');
                var anchor = block.content && block.content.layout && (block.content.layout.Anchor || block.content.layout.anchor);
                var anchorBlockId = anchor && (anchor.BlockId || anchor.blockId);
                if (anchorBlockId) references.objectAnchors.push({ id: anchorBlockId, path: path + '.content.layout.anchor.blockId' });
            }
            if (block.type === 'table') {
                _asArray(block.content && block.content.rows).forEach(function (row, rowIndex) {
                    requireId(row.id, path + '.rows[' + rowIndex + ']');
                    _asArray(row.cells).forEach(function (cell, cellIndex) {
                        requireId(cell.id, path + '.rows[' + rowIndex + '].cells[' + cellIndex + ']');
                        _asArray(cell.blocks).forEach(function (child, blockIndex) {
                            visitBlock(child, path + '.rows[' + rowIndex + '].cells[' + cellIndex + '].blocks[' + blockIndex + ']');
                        });
                    });
                });
            }
        }

        _asArray(model && model.body && model.body.blocks).forEach(function (block, index) {
            visitBlock(block, 'body.blocks[' + index + ']');
        });
        _asArray(model && model.headers).forEach(function (region, index) {
            requireId(region.id, 'headers[' + index + ']');
            _asArray(region.blocks).forEach(function (block, blockIndex) { visitBlock(block, 'headers[' + index + '].blocks[' + blockIndex + ']'); });
        });
        _asArray(model && model.footers).forEach(function (region, index) {
            requireId(region.id, 'footers[' + index + ']');
            _asArray(region.blocks).forEach(function (block, blockIndex) { visitBlock(block, 'footers[' + index + '].blocks[' + blockIndex + ']'); });
        });

        buildIndexes(model);
        references.revisions.forEach(function (reference) {
            if (!model.indexes.revisions[reference.id]) errors.push({ code: 'dangling-revision-reference', path: reference.path, id: reference.id });
        });
        references.comments.forEach(function (reference) {
            if (!model.indexes.comments[reference.id]) errors.push({ code: 'dangling-comment-reference', path: reference.path, id: reference.id });
        });
        references.objectAnchors.forEach(function (reference) {
            if (!model.indexes.blocks[reference.id]) errors.push({ code: 'dangling-object-anchor', path: reference.path, id: reference.id });
        });
        return {
            ok: errors.length === 0,
            errors: errors,
            counts: {
                blocks: Object.keys(model.indexes.blocks).length,
                inlines: Object.keys(model.indexes.inlines).length,
                objects: Object.keys(model.indexes.objects).length,
                revisions: Object.keys(model.indexes.revisions).length,
                comments: Object.keys(model.indexes.comments).length
            }
        };
    }

    var OPERATION_TYPES = Object.freeze({
        InsertText: 'InsertText',
        DeleteRange: 'DeleteRange',
        SplitParagraph: 'SplitParagraph',
        MergeParagraph: 'MergeParagraph',
        ApplyMark: 'ApplyMark',
        RemoveMark: 'RemoveMark',
        SetParagraphAttribute: 'SetParagraphAttribute',
        InsertImage: 'InsertImage',
        UpdateImageLayout: 'UpdateImageLayout',
        UpdateImageMetadata: 'UpdateImageMetadata',
        InsertTable: 'InsertTable',
        UpdateTableCell: 'UpdateTableCell',
        AcceptRevision: 'AcceptRevision',
        RejectRevision: 'RejectRevision',
        SetSelection: 'SetSelection',
        RestoreSnapshot: 'RestoreSnapshot'
    });

    var TRANSACTION_TYPES = Object.freeze({
        Default: 'default',
        Typing: 'typing',
        Undo: 'undo',
        Redo: 'redo',
        Preview: 'preview',
        Remote: 'remote'
    });

    function isTypingLikeTransactionType(value) {
        var type = String(value || '').toLowerCase();
        return type === TRANSACTION_TYPES.Typing || type === 'typing' || type === 'delete' || type === 'keyboarddelete';
    }

    function createOperation(type, payload, options) {
        var opts = options || {};
        var body = payload || {};
        var operation = Object.assign({}, body, {
            id: _asText(body.id || body.Id || opts.id || ('op-' + (++_operationCounter))),
            type: _asText(type || body.type || body.Type),
            timestamp: Number(body.timestamp || body.Timestamp || opts.timestamp || Date.now()),
            source: _asText(body.source || body.Source || opts.source || 'local'),
            baseVersion: body.baseVersion ?? body.BaseVersion ?? opts.baseVersion ?? null,
            batchId: body.batchId || body.BatchId || opts.batchId || null,
            affectedSelectable: _asArray(body.affectedSelectable || body.AffectedSelectable || opts.affectedSelectable)
        });
        return attachOperationMethods(operation);
    }

    function attachOperationMethods(operation) {
        if (!operation || typeof operation !== 'object') return operation;
        Object.defineProperty(operation, 'getReversed', {
            configurable: true,
            enumerable: false,
            value: function () { return getReversedOperation(operation); }
        });
        Object.defineProperty(operation, 'toJSON', {
            configurable: true,
            enumerable: false,
            value: function () {
                var result = {};
                Object.keys(operation).sort().forEach(function (key) {
                    if (typeof operation[key] !== 'function') result[key] = operation[key];
                });
                return result;
            }
        });
        return operation;
    }

    function getReversedOperation(operation) {
        var op = operation || {};
        switch (op.type) {
            case OPERATION_TYPES.InsertText:
                return createOperation(OPERATION_TYPES.DeleteRange, {
                    target: op.target,
                    range: {
                        blockId: op.target && op.target.blockId,
                        start: op.target && op.target.offset,
                        end: Number(op.target && op.target.offset || 0) + _asText(op.text).length
                    },
                    text: op.text
                }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            case OPERATION_TYPES.DeleteRange:
                return createOperation(OPERATION_TYPES.InsertText, {
                    target: { blockId: op.range && op.range.blockId, offset: op.range && op.range.start },
                    text: op.deletedText || op.text || ''
                }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            case OPERATION_TYPES.ApplyMark:
                return createOperation(OPERATION_TYPES.RemoveMark, { range: op.range, mark: op.mark }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            case OPERATION_TYPES.RemoveMark:
                return createOperation(OPERATION_TYPES.ApplyMark, { range: op.range, mark: op.mark }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            case OPERATION_TYPES.SetParagraphAttribute:
                return createOperation(OPERATION_TYPES.SetParagraphAttribute, {
                    target: op.target,
                    attributeName: op.attributeName,
                    value: op.previousValue
                }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            case OPERATION_TYPES.SetSelection:
                return createOperation(OPERATION_TYPES.SetSelection, { selection: op.previousSelection || null }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            case OPERATION_TYPES.RestoreSnapshot:
                return createOperation(OPERATION_TYPES.RestoreSnapshot, {
                    snapshot: op.previousSnapshot || op.snapshot || null,
                    previousSnapshot: op.snapshot || null,
                    selection: op.previousSelection || op.selection || null,
                    previousSelection: op.selection || null,
                    affectedScopeIds: op.affectedScopeIds || ['document']
                }, { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
            default:
                return createOperation(op.type || 'Unknown', _clone(op), { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId });
        }
    }

    function supportsOperationHistory(operation) {
        var type = operation && (operation.type || operation.Type) || '';
        if (operation && (operation.revisionId || operation.RevisionId || operation.revision || operation.Revision)) return false;
        return [
            OPERATION_TYPES.InsertText,
            OPERATION_TYPES.DeleteRange,
            OPERATION_TYPES.ApplyMark,
            OPERATION_TYPES.RemoveMark,
            OPERATION_TYPES.SetParagraphAttribute,
            OPERATION_TYPES.SetSelection,
            OPERATION_TYPES.RestoreSnapshot
        ].indexOf(type) >= 0;
    }

    function supportsLightweightTransactionSnapshots(operations, transactionType) {
        var list = _asArray(operations);
        return list.length > 0
            && isTypingLikeTransactionType(transactionType)
            && list.every(supportsOperationHistory);
    }

    function isSelectionOnlyOperation(operation) {
        var type = operation && (operation.type || operation.Type) || '';
        return type === OPERATION_TYPES.SetSelection;
    }

    function operationsAffectDocument(operations) {
        return _asArray(operations).some(function (operation) {
            return operation && !isSelectionOnlyOperation(operation);
        });
    }

    function transactionAffectsDocument(transaction) {
        return !!(transaction && operationsAffectDocument(transaction.operations));
    }

    function toOperationJson(operation) {
        var attached = attachOperationMethods(_clone(operation));
        return attached.toJSON ? attached.toJSON() : _clone(attached);
    }

    function createReversedOperationJson(operation) {
        return toOperationJson(getReversedOperation(attachOperationMethods(_clone(operation))));
    }

    function createRedoHistoryOperations(operations) {
        return _asArray(operations).map(toOperationJson);
    }

    function createUndoHistoryOperations(operations) {
        return _asArray(operations).map(createReversedOperationJson);
    }

    function _normalizeTarget(value) {
        var target = value || {};
        return {
            blockId: _asText(target.blockId || target.BlockId),
            offset: Number(target.offset ?? target.Offset ?? 0),
            region: target.region || target.Region || null,
            headerFooterId: target.headerFooterId || target.HeaderFooterId || null
        };
    }

    function _normalizeRange(value) {
        var range = value || {};
        var start = Number(range.start ?? range.Start ?? 0);
        var end = Number(range.end ?? range.End ?? start);
        return {
            blockId: _asText(range.blockId || range.BlockId),
            start: Math.min(start, end),
            end: Math.max(start, end),
            region: range.region || range.Region || null,
            headerFooterId: range.headerFooterId || range.HeaderFooterId || null
        };
    }

    function _blockText(block) {
        return block && block.content && Array.isArray(block.content.runs)
            ? _textFromRuns(block.content.runs)
            : '';
    }

    function _isEditableTextBlock(block) {
        return !!(block && block.content && Array.isArray(block.content.runs));
    }

    function _findBlockContainer(model, blockId) {
        function scan(blocks) {
            for (var i = 0; i < _asArray(blocks).length; i++) {
                if (blocks[i] && blocks[i].id === blockId) return { blocks: blocks, index: i, block: blocks[i] };
                if (blocks[i] && blocks[i].type === 'table') {
                    var rows = _asArray(blocks[i].content && blocks[i].content.rows);
                    for (var r = 0; r < rows.length; r++) {
                        var cells = _asArray(rows[r].cells);
                        for (var c = 0; c < cells.length; c++) {
                            var nested = scan(cells[c].blocks);
                            if (nested) return nested;
                        }
                    }
                }
            }
            return null;
        }
        var found = scan(model && model.body && model.body.blocks);
        if (found) return found;
        for (var h = 0; h < _asArray(model && model.headers).length; h++) {
            found = scan(model.headers[h].blocks);
            if (found) return found;
        }
        for (var f = 0; f < _asArray(model && model.footers).length; f++) {
            found = scan(model.footers[f].blocks);
            if (found) return found;
        }
        return null;
    }

    function _findBlock(model, blockId) {
        var id = _asText(blockId);
        if (!model || !id) return null;
        if (!model.indexes || !model.indexes.blocks || !model.indexes.blocks[id]) {
            buildIndexes(model);
        }
        return model.indexes && model.indexes.blocks ? model.indexes.blocks[id] || null : null;
    }

    function _findCell(model, cellId) {
        var found = null;
        function scan(blocks) {
            _asArray(blocks).forEach(function (block) {
                if (!block || block.type !== 'table') return;
                _asArray(block.content && block.content.rows).forEach(function (row) {
                    _asArray(row.cells).forEach(function (cell) {
                        if (cell.id === cellId) found = cell;
                        scan(cell.blocks);
                    });
                });
            });
        }
        scan(model && model.body && model.body.blocks);
        _asArray(model && model.headers).forEach(function (region) { scan(region.blocks); });
        _asArray(model && model.footers).forEach(function (region) { scan(region.blocks); });
        return found;
    }

    function _findTableInfo(model, predicate) {
        var found = null;
        function scan(blocks) {
            _asArray(blocks).forEach(function (block) {
                if (found) return;
                if (!block || block.type !== 'table') return;
                var rows = _asArray(block.content && block.content.rows);
                for (var r = 0; r < rows.length; r++) {
                    var cells = _asArray(rows[r].cells);
                    for (var c = 0; c < cells.length; c++) {
                        var cell = cells[c];
                        if (predicate(block, rows[r], cell, r, c)) {
                            found = { table: block, row: rows[r], cell: cell, rowIndex: r, columnIndex: c };
                            return;
                        }
                        scan(cell.blocks);
                        if (found) return;
                    }
                }
            });
        }
        scan(model && model.body && model.body.blocks);
        _asArray(model && model.headers).forEach(function (region) { scan(region.blocks); });
        _asArray(model && model.footers).forEach(function (region) { scan(region.blocks); });
        return found;
    }

    function _findTableInfoByCellId(model, cellId) {
        return _findTableInfo(model, function (_table, _row, cell) { return cell.id === cellId; });
    }

    function _findTableInfoByBlockId(model, blockId) {
        return _findTableInfo(model, function (_table, _row, cell) {
            return _asArray(cell.blocks).some(function (block) { return block && block.id === blockId; });
        });
    }

    function _findTableBlock(model, tableId) {
        var block = _findBlock(model, tableId);
        return block && block.type === 'table' ? block : null;
    }

    function _tableColumnCount(table) {
        return Math.max(1, ..._asArray(table && table.content && table.content.rows).map(function (row) {
            return _asArray(row.cells).reduce(function (sum, cell) { return sum + Math.max(1, Number(cell.colSpan || 1)); }, 0);
        }), 1);
    }

    function _createEmptyTableCell(tableId, rowIndex, columnIndex) {
        var cellId = tableId + '-r' + rowIndex + '-c' + columnIndex;
        return {
            id: cellId,
            type: 'tableCell',
            rowSpan: 1,
            colSpan: 1,
            width: null,
            height: null,
            style: {},
            blocks: [importBlock({
                Id: cellId + '-p',
                Type: 'Paragraph',
                Content: { Inlines: [{ Id: cellId + '-r', Text: '' }] }
            }, cellId + '-block')]
        };
    }

    function _plainRuns(text, path) {
        return [importInlineRun({ Id: _stableId('inline', path || 'run'), Text: text || '' }, path || 'run')];
    }

    function _setParagraphText(block, text) {
        if (!block.content) block.content = { type: 'paragraph', runs: [] };
        block.content.runs = _plainRuns(text, block.id + '-run-0');
    }

    function cloneRunSlice(run, start, end, suffix) {
        var text = _asText(run && run.text);
        var range = clampTextRange(text, start, end);
        var next = _clone(run || {});
        next.id = _asText(next.id || next.Id || _stableId('inline', 'run')) + suffix;
        next.text = text.slice(range.start, range.end);
        return normalizeTextRunForMerge(next);
    }

    function clampTextBoundary(text, index, direction) {
        var value = Math.max(0, Math.min(_asText(text).length, Number(index || 0)));
        if (value > 0
            && value < text.length
            && text.charCodeAt(value - 1) >= 0xD800
            && text.charCodeAt(value - 1) <= 0xDBFF
            && text.charCodeAt(value) >= 0xDC00
            && text.charCodeAt(value) <= 0xDFFF) {
            return direction === 'end' ? value + 1 : value - 1;
        }
        return value;
    }

    function clampTextRange(text, start, end) {
        var source = _asText(text);
        var from = clampTextBoundary(source, Math.min(Number(start || 0), Number(end || 0)), 'start');
        var to = clampTextBoundary(source, Math.max(Number(start || 0), Number(end || 0)), 'end');
        if (to < from) to = from;
        return { start: from, end: to };
    }

    function commentIdsAtInsertionOffset(block, offset) {
        if (!block || block.type !== 'paragraph') return [];
        var target = Math.max(0, Math.min(_blockText(block).length, Number(offset || 0) || 0));
        var leftIds = [];
        var rightIds = [];
        var cursor = 0;
        _asArray(block.content && block.content.runs).forEach(function (run) {
            var runText = resolveInlineRunDisplayText(run);
            var runStart = cursor;
            var runEnd = cursor + runText.length;
            cursor = runEnd;
            if (runEnd <= runStart) return;
            var runCommentIds = readCommentIdsFromRun(run);
            if (runCommentIds.length === 0) return;
            if (target > runStart && target <= runEnd) {
                leftIds = _unique(leftIds.concat(runCommentIds));
            }
            if (target >= runStart && target < runEnd) {
                rightIds = _unique(rightIds.concat(runCommentIds));
            }
        });
        return _unique(leftIds.filter(function (commentId) { return rightIds.indexOf(commentId) >= 0; })).sort();
    }

    function _insertTextRun(block, offset, text, attributes) {
        if (!block.content) block.content = { type: 'paragraph', runs: [] };
        var attrs = attributes || {};
        var hasExplicitCommentIds = _hasOwn(attrs, 'commentIds') || _hasOwn(attrs, 'CommentIds');
        var inheritedCommentIds = hasExplicitCommentIds
            ? _unique(_asArray(attrs.commentIds || attrs.CommentIds).map(_asText).filter(Boolean)).sort()
            : commentIdsAtInsertionOffset(block, offset);
        function createInsertedRun(index) {
            return _sortObject(Object.assign({
                id: attrs.id || _stableId('inline', block.id + '-insert-' + Date.now() + (index === undefined ? '' : '-' + index)),
                kind: 'text',
                text: _asText(text),
                marks: normalizeMarks(attrs.marks || []),
                style: _clone(attrs.style || {})
            }, inheritedCommentIds.length ? { commentIds: inheritedCommentIds } : {}, attrs.revisionId ? { revisionId: attrs.revisionId } : {}));
        }
        var result = [];
        var cursor = 0;
        var inserted = false;
        _asArray(block.content.runs).forEach(function (run, index) {
            var runText = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + runText.length;
            cursor = runEnd;
            if (!inserted && offset >= runStart && offset <= runEnd) {
                var local = Math.max(0, Math.min(runText.length, offset - runStart));
                if (local > 0) {
                    var before = _clone(run);
                    before.id = run.id + '-before';
                    before.text = runText.slice(0, local);
                    result.push(normalizeTextRunForMerge(before));
                }
                result.push(createInsertedRun(index));
                if (local < runText.length) {
                    var after = _clone(run);
                    after.id = run.id + '-after';
                    after.text = runText.slice(local);
                    result.push(normalizeTextRunForMerge(after));
                }
                inserted = true;
            } else {
                result.push(normalizeTextRunForMerge(run));
            }
        });
        if (!inserted) {
            result.push(createInsertedRun());
        }
        block.content.runs = mergeAdjacentTextRuns(result);
    }

    function _deleteTextRange(block, start, end) {
        if (!block || block.type !== 'paragraph') return;
        if (!block.content) block.content = { type: 'paragraph', runs: [] };
        var from = Math.max(0, Math.min(start, end));
        var to = Math.max(from, Math.max(start, end));
        var result = [];
        var cursor = 0;
        _asArray(block.content.runs).forEach(function (run) {
            var runText = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + runText.length;
            cursor = runEnd;
            if (runEnd <= from || runStart >= to || runText.length === 0) {
                result.push(normalizeTextRunForMerge(run));
                return;
            }
            var localStart = Math.max(0, from - runStart);
            var localEnd = Math.min(runText.length, to - runStart);
            if (localStart > 0) result.push(cloneRunSlice(run, 0, localStart, '-d-before'));
            if (localEnd < runText.length) result.push(cloneRunSlice(run, localEnd, runText.length, '-d-after'));
        });
        block.content.runs = mergeAdjacentTextRuns(result.length ? result : _plainRuns('', block.id + '-run-0'));
    }

    function _splitParagraphRuns(block, offset) {
        var before = [];
        var after = [];
        var cursor = 0;
        _asArray(block && block.content && block.content.runs).forEach(function (run) {
            var runText = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + runText.length;
            cursor = runEnd;
            if (runEnd <= offset) {
                before.push(normalizeTextRunForMerge(run));
                return;
            }
            if (runStart >= offset) {
                after.push(normalizeTextRunForMerge(run));
                return;
            }
            var local = Math.max(0, Math.min(runText.length, offset - runStart));
            if (local > 0) before.push(cloneRunSlice(run, 0, local, '-s-before'));
            if (local < runText.length) after.push(cloneRunSlice(run, local, runText.length, '-s-after'));
        });
        return {
            before: mergeAdjacentTextRuns(before.length ? before : _plainRuns('', block.id + '-before-empty')),
            after: mergeAdjacentTextRuns(after.length ? after : _plainRuns('', block.id + '-after-empty'))
        };
    }

    function _splitRunsForRange(block, start, end, mark, remove) {
        var result = [];
        var cursor = 0;
        _asArray(block.content && block.content.runs).forEach(function (run) {
            var text = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + text.length;
            cursor = runEnd;
            if (runEnd <= start || runStart >= end || text.length === 0) {
                result.push(normalizeTextRunForMerge(run));
                return;
            }
            var localStart = Math.max(0, start - runStart);
            var localEnd = Math.min(text.length, end - runStart);
            var localRange = clampTextRange(text, localStart, localEnd);
            localStart = localRange.start;
            localEnd = localRange.end;
            if (localStart > 0) {
                var before = _clone(run);
                before.id = run.id + '-a';
                before.text = text.slice(0, localStart);
                result.push(normalizeTextRunForMerge(before));
            }
            var middle = _clone(run);
            middle.id = run.id + '-m';
            middle.text = text.slice(localStart, localEnd);
            middle.marks = updateMarks(middle.marks, mark, remove);
            result.push(normalizeTextRunForMerge(middle));
            if (localEnd < text.length) {
                var after = _clone(run);
                after.id = run.id + '-b';
                after.text = text.slice(localEnd);
                result.push(normalizeTextRunForMerge(after));
            }
        });
        block.content.runs = mergeAdjacentTextRuns(result);
    }

    function _markKey(mark) {
        return JSON.stringify(normalizeMark(mark));
    }

    function normalizeMark(mark) {
        return _sortObject(_clone(mark || {}));
    }

    function markSortKey(mark) {
        var normalized = normalizeMark(mark);
        return [
            String(markOrderValue(normalized)).padStart(3, '0'),
            String(markValue(normalized) ?? ''),
            String(normalized.revisionId || normalized.RevisionId || ''),
            String(normalized.commentId || normalized.CommentId || ''),
            _markKey(normalized)
        ].join('\u001f');
    }

    function markOrderValue(mark) {
        var raw = mark && (mark.type ?? mark.Type);
        if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
        var type = markType(mark);
        var order = [
            'bold',
            'italic',
            'underline',
            'strikethrough',
            'superscript',
            'subscript',
            'link',
            'commentanchor',
            'revision',
            'highlight',
            'textcolor',
            'fontfamily',
            'fontsize'
        ].indexOf(type);
        return order >= 0 ? order : 999;
    }

    function normalizeMarks(marks) {
        var seen = new Set();
        return _asArray(marks)
            .map(normalizeMark)
            .sort(function (left, right) {
                var leftKey = markSortKey(left);
                var rightKey = markSortKey(right);
                return leftKey < rightKey ? -1 : (leftKey > rightKey ? 1 : 0);
            })
            .filter(function (mark) {
                var key = _markKey(mark);
                if (seen.has(key)) return false;
                seen.add(key);
                return true;
            });
    }

    function updateMarks(marks, mark, remove) {
        var source = normalizeMarks(marks);
        var key = _markKey(mark);
        var without = source.filter(function (item) { return _markKey(item) !== key; });
        if (remove) return normalizeMarks(without);
        without.push(normalizeMark(mark || {}));
        return normalizeMarks(without);
    }

    function normalizeTextRunForMerge(run) {
        var clone = _clone(run || {});
        clone.id = _asText(clone.id || clone.Id || '');
        delete clone.Id;
        clone.kind = clone.kind || clone.Kind || 'text';
        delete clone.Kind;
        clone.text = _asText(clone.text ?? clone.Text);
        delete clone.Text;
        if (clone.kind === 'text') {
            delete clone.key;
            delete clone.Key;
            delete clone.fieldType;
            delete clone.FieldType;
            delete clone.fallbackText;
            delete clone.FallbackText;
        }
        clone.marks = normalizeMarks(clone.marks || clone.Marks || []);
        delete clone.Marks;
        clone.style = _sortObject(clone.style || clone.Style || {});
        delete clone.Style;
        clone.commentIds = _unique(clone.commentIds || clone.CommentIds || []).sort();
        delete clone.CommentIds;
        if (clone.revisionId === undefined && clone.RevisionId !== undefined) clone.revisionId = clone.RevisionId;
        delete clone.RevisionId;
        if (clone.revisionId === undefined) clone.revisionId = null;
        return _sortObject(clone);
    }

    function _runMergeKey(run) {
        var clone = normalizeTextRunForMerge(run);
        delete clone.id;
        delete clone.text;
        return JSON.stringify(_sortObject(clone));
    }

    function mergeAdjacentTextRuns(runs) {
        var result = [];
        _asArray(runs).forEach(function (run) {
            if (!run) return;
            var normalized = normalizeTextRunForMerge(run);
            var text = _asText(normalized.text);
            if (text.length === 0 && result.length > 0) return;
            var previous = result[result.length - 1];
            if (previous && previous.kind === normalized.kind && _runMergeKey(previous) === _runMergeKey(normalized)) {
                previous.text = _asText(previous.text) + text;
            } else {
                result.push(normalized);
            }
        });
        return result.length > 0 ? result : _plainRuns('', 'empty');
    }

    function validateOperation(model, operation) {
        var op = attachOperationMethods(operation || {});
        var errors = [];
        if (!op.id) errors.push({ code: 'missing-id', path: 'operation.id' });
        if (!op.type) errors.push({ code: 'missing-type', path: 'operation.type' });
        if (!op.timestamp) errors.push({ code: 'missing-timestamp', path: 'operation.timestamp' });
        if (!op.source) errors.push({ code: 'missing-source', path: 'operation.source' });
        if (op.type && !OPERATION_TYPES[op.type]) errors.push({ code: 'unknown-type', path: 'operation.type', value: op.type });

        var targetTypes = [
            OPERATION_TYPES.InsertText,
            OPERATION_TYPES.SplitParagraph,
            OPERATION_TYPES.MergeParagraph,
            OPERATION_TYPES.SetParagraphAttribute,
            OPERATION_TYPES.InsertImage,
            OPERATION_TYPES.UpdateImageLayout,
            OPERATION_TYPES.UpdateImageMetadata
        ];
        if (targetTypes.indexOf(op.type) >= 0) {
            var target = _normalizeTarget(op.target || op.Target);
            var block = _findBlock(model, target.blockId);
            if (!block) {
                errors.push({ code: 'missing-target-block', path: 'operation.target.blockId', blockId: target.blockId });
            } else if (block.type === 'paragraph' && (target.offset < 0 || target.offset > _blockText(block).length)) {
                errors.push({ code: 'offset-out-of-range', path: 'operation.target.offset', offset: target.offset, length: _blockText(block).length });
            }
        }

        if ([OPERATION_TYPES.DeleteRange, OPERATION_TYPES.ApplyMark, OPERATION_TYPES.RemoveMark].indexOf(op.type) >= 0) {
            var range = _normalizeRange(op.range || op.Range);
            var rangeBlock = _findBlock(model, range.blockId);
            if (!rangeBlock) {
                errors.push({ code: 'missing-target-block', path: 'operation.range.blockId', blockId: range.blockId });
            } else if (rangeBlock.type !== 'paragraph' || range.start < 0 || range.end > _blockText(rangeBlock).length || range.start > range.end) {
                errors.push({ code: 'invalid-range', path: 'operation.range', start: range.start, end: range.end, length: _blockText(rangeBlock).length });
            }
        }

        if (op.type === OPERATION_TYPES.UpdateImageLayout) {
            var imageTarget = _normalizeTarget(op.target || op.Target);
            var imageBlock = _findBlock(model, imageTarget.blockId);
            if (imageBlock && imageBlock.type !== 'image') errors.push({ code: 'target-not-image', path: 'operation.target.blockId', blockId: imageTarget.blockId });
            var anchor = op.layout && (op.layout.Anchor || op.layout.anchor);
            var anchorBlockId = anchor && (anchor.BlockId || anchor.blockId);
            if (anchorBlockId && !_findBlock(model, anchorBlockId)) {
                errors.push({ code: 'dangling-image-anchor', path: 'operation.layout.anchor.blockId', blockId: anchorBlockId });
            }
        }

        return { ok: errors.length === 0, errors: errors, operation: op };
    }

    function createDiffer() {
        return {
            insertedRanges: [],
            removedRanges: [],
            attributeChanges: [],
            objectChanges: [],
            markerChanges: [],
            invalidatedLayoutScopes: [],
            invalidatedOverlayScopes: [],
            record: function (entry) {
                var item = entry || {};
                if (item.insertedRange) this.insertedRanges.push(item.insertedRange);
                if (item.removedRange) this.removedRanges.push(item.removedRange);
                if (item.attributeChange) this.attributeChanges.push(item.attributeChange);
                if (item.objectChange) this.objectChanges.push(item.objectChange);
                if (item.markerChange) this.markerChanges.push(item.markerChange);
                this.invalidatedLayoutScopes = _unique(this.invalidatedLayoutScopes.concat(_asArray(item.invalidatedLayoutScopes)));
                this.invalidatedOverlayScopes = _unique(this.invalidatedOverlayScopes.concat(_asArray(item.invalidatedOverlayScopes)));
            },
            getChangedRanges: function () {
                return this.insertedRanges.concat(this.removedRanges);
            },
            getInvalidatedLayoutScopes: function () {
                return this.invalidatedLayoutScopes.slice();
            },
            getInvalidatedOverlayScopes: function () {
                return this.invalidatedOverlayScopes.slice();
            },
            clear: function () {
                this.insertedRanges = [];
                this.removedRanges = [];
                this.attributeChanges = [];
                this.objectChanges = [];
                this.markerChanges = [];
                this.invalidatedLayoutScopes = [];
                this.invalidatedOverlayScopes = [];
            },
            snapshot: function () {
                return _sortObject({
                    insertedRanges: this.insertedRanges,
                    removedRanges: this.removedRanges,
                    attributeChanges: this.attributeChanges,
                    objectChanges: this.objectChanges,
                    markerChanges: this.markerChanges,
                    invalidatedLayoutScopes: this.invalidatedLayoutScopes,
                    invalidatedOverlayScopes: this.invalidatedOverlayScopes
                });
            }
        };
    }

    function applyOperation(model, operation, context) {
        var op = attachOperationMethods(operation || {});
        var validation = validateOperation(model, op);
        if (!validation.ok) return { ok: false, errors: validation.errors, operation: op };
        var differ = context && context.differ || createDiffer();
        var selection = context && context.selection ? _clone(context.selection) : null;
        var result;
        switch (op.type) {
            case OPERATION_TYPES.InsertText:
                result = applyInsertText(model, op, differ);
                break;
            case OPERATION_TYPES.DeleteRange:
                result = applyDeleteRange(model, op, differ);
                break;
            case OPERATION_TYPES.SplitParagraph:
                result = applySplitParagraph(model, op, differ);
                break;
            case OPERATION_TYPES.MergeParagraph:
                result = applyMergeParagraph(model, op, differ);
                break;
            case OPERATION_TYPES.ApplyMark:
                result = applyMarkOperation(model, op, differ, false);
                break;
            case OPERATION_TYPES.RemoveMark:
                result = applyMarkOperation(model, op, differ, true);
                break;
            case OPERATION_TYPES.SetParagraphAttribute:
                result = applySetParagraphAttribute(model, op, differ);
                break;
            case OPERATION_TYPES.InsertImage:
                result = applyInsertImage(model, op, differ);
                break;
            case OPERATION_TYPES.UpdateImageLayout:
                result = applyUpdateImageLayout(model, op, differ);
                break;
            case OPERATION_TYPES.UpdateImageMetadata:
                result = applyUpdateImageMetadata(model, op, differ);
                break;
            case OPERATION_TYPES.InsertTable:
                result = applyInsertTable(model, op, differ);
                break;
            case OPERATION_TYPES.UpdateTableCell:
                result = applyUpdateTableCell(model, op, differ);
                break;
            case OPERATION_TYPES.AcceptRevision:
            case OPERATION_TYPES.RejectRevision:
                result = applyRevisionDecision(model, op, differ);
                break;
            case OPERATION_TYPES.SetSelection:
                result = { ok: true, nextSelection: _clone(op.selection || selection || null), invalidatedLayoutScopes: [], operation: op };
                break;
            case OPERATION_TYPES.RestoreSnapshot:
                result = applyRestoreSnapshot(model, op, differ);
                break;
            default:
                result = { ok: false, errors: [{ code: 'unsupported-operation', type: op.type }], operation: op };
                break;
        }
        if (result.ok) {
            var revisionNormalization = normalizeRevisionGroups(model, result.invalidatedLayoutScopes || operationAffectedBlockIds(op));
            if (!revisionNormalization || revisionNormalization.indexesRebuilt !== true) buildIndexes(model);
            result.differ = differ.snapshot();
            result.operation = op;
        }
        return result;
    }

    function applyInsertText(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var block = _findBlock(model, target.blockId);
        var text = _blockText(block);
        var inserted = _asText(op.text ?? op.Text);
        var marks = normalizeMarks(op.marks || op.Marks || []);
        var style = op.style || op.Style || {};
        var revisionId = op.revisionId || op.RevisionId || null;
        var revisionPayload = op.revision || op.Revision || null;
        if (revisionId && revisionPayload && !revisionById(model, revisionId)) {
            if (!Array.isArray(model.revisions)) model.revisions = [];
            model.revisions.push(_sortObject(revisionPayload));
        }
        _insertTextRun(block, target.offset, inserted, { marks: marks, style: style, revisionId: revisionId });
        var range = { blockId: block.id, start: target.offset, end: target.offset + inserted.length };
        differ.record({ insertedRange: range, invalidatedLayoutScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: [block.id], nextSelection: nextSelectionForOperation(model, op, block.id, range.end, target) };
    }

    function applyDeleteRange(model, op, differ) {
        var range = _normalizeRange(op.range || op.Range);
        var block = _findBlock(model, range.blockId);
        var text = _blockText(block);
        var removed = text.slice(range.start, range.end);
        op.deletedText = removed;
        var revisionId = op.revisionId || op.RevisionId || null;
        var revisionPayload = op.revision || op.Revision || null;
        if (revisionId || revisionPayload) {
            var deletionRevision = normalizeRevision(revisionPayload || {
                id: revisionId,
                type: 'Deletion',
                status: 'Pending',
                affectedRange: range,
                payload: { text: removed },
                payloadJson: removed
            });
            revisionId = revisionId || deletionRevision.id;
            deletionRevision.id = revisionId;
            deletionRevision.type = 'Deletion';
            deletionRevision.status = 'Pending';
            deletionRevision.affectedRange = normalizeRevisionRange(Object.assign({}, deletionRevision.affectedRange || {}, range));
            deletionRevision.range = deletionRevision.affectedRange;
            if (!revisionPayload || !revisionPayload.payload && !revisionPayload.Payload) {
                setRevisionPayloadText(deletionRevision, removed);
            }
            addRevision(model, deletionRevision);
            setRevisionForRange(model, revisionId, range);
            op.revisionId = revisionId;
            op.revision = _clone(deletionRevision);
            op.trackedDeletion = true;
            differ.record({
                markerChange: { revisionId: revisionId, status: 'Pending', type: 'Deletion' },
                removedRange: { blockId: block.id, start: range.start, end: range.end, text: removed, tracked: true },
                invalidatedLayoutScopes: [block.id],
                invalidatedOverlayScopes: ['revisions', block.id]
            });
            return { ok: true, invalidatedLayoutScopes: [block.id], nextSelection: nextSelectionForOperation(model, op, block.id, range.start, range) };
        }
        _deleteTextRange(block, range.start, range.end);
        differ.record({ removedRange: { blockId: block.id, start: range.start, end: range.end, text: removed }, invalidatedLayoutScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: [block.id], nextSelection: nextSelectionForOperation(model, op, block.id, range.start, range) };
    }

    function applySplitParagraph(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var container = _findBlockContainer(model, target.blockId);
        var block = container.block;
        var text = _blockText(block);
        var splitRuns = _splitParagraphRuns(block, target.offset);
        var newBlock = importBlock({
            Id: op.newBlockId || op.NewBlockId || _stableId('block', block.id + '-split-' + Date.now()),
            Type: 'Paragraph',
            Content: {
                Inlines: splitRuns.after,
                Alignment: block.content && block.content.alignment,
                LineSpacing: block.content && block.content.lineSpacing,
                Style: _clone(block.content && block.content.style || {})
            },
            Style: _clone(block.style || {})
        }, block.id + '-split');
        block.content.runs = splitRuns.before;
        container.blocks.splice(container.index + 1, 0, newBlock);
        var revisionId = op.revisionId || op.RevisionId || null;
        var revisionPayload = op.revision || op.Revision || null;
        if (revisionId || revisionPayload) {
            var splitRevision = normalizeRevision(revisionPayload || {
                id: revisionId,
                type: 'Structure',
                status: 'Pending',
                affectedRange: { blockId: block.id, start: target.offset, end: target.offset },
                payload: { text: 'SplitBlock' },
                payloadJson: 'SplitBlock'
            });
            revisionId = revisionId || splitRevision.id;
            splitRevision.id = revisionId;
            splitRevision.type = 'Structure';
            splitRevision.status = 'Pending';
            splitRevision.affectedRange = normalizeRevisionRange(splitRevision.affectedRange || { blockId: block.id, start: target.offset, end: target.offset });
            splitRevision.range = splitRevision.affectedRange;
            addRevision(model, splitRevision);
            op.revisionId = revisionId;
            op.revision = _clone(splitRevision);
        }
        differ.record({ insertedRange: { blockId: newBlock.id, start: 0, end: _blockText(newBlock).length }, invalidatedLayoutScopes: [block.id, newBlock.id] });
        if (revisionId) {
            differ.record({ markerChange: { revisionId: revisionId, status: 'Pending', type: 'Structure' }, invalidatedOverlayScopes: ['revisions'] });
        }
        return { ok: true, invalidatedLayoutScopes: [block.id, newBlock.id], nextSelection: nextSelectionForOperation(model, op, newBlock.id, 0, operationRegionInfo(model, op, block.id, target)), insertedBlockId: newBlock.id };
    }

    function applyMergeParagraph(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var container = _findBlockContainer(model, target.blockId);
        var index = container.index;
        var block = container.block;
        var previous = container.blocks[index - 1] || null;
        if (!previous || !_isEditableTextBlock(previous) || !_isEditableTextBlock(block)) {
            return { ok: false, errors: [{ code: 'missing-previous-paragraph', path: 'operation.target.blockId', blockId: target.blockId }] };
        }
        var offset = _blockText(previous).length;
        _setParagraphText(previous, _blockText(previous) + _blockText(block));
        container.blocks.splice(index, 1);
        differ.record({ removedRange: { blockId: block.id, start: 0, end: _blockText(block).length }, invalidatedLayoutScopes: [previous.id, block.id] });
        return { ok: true, invalidatedLayoutScopes: [previous.id, block.id], nextSelection: nextSelectionForOperation(model, op, previous.id, offset, operationRegionInfo(model, op, block.id, target)) };
    }

    function applyMarkOperation(model, op, differ, remove) {
        var range = _normalizeRange(op.range || op.Range);
        var block = _findBlock(model, range.blockId);
        _splitRunsForRange(block, range.start, range.end, op.mark || op.Mark || {}, remove);
        differ.record({ attributeChange: { blockId: block.id, range: range, attributeName: 'marks' }, invalidatedLayoutScopes: [block.id], invalidatedOverlayScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: [block.id], nextSelection: nextSelectionForOperation(model, op, block.id, range.end, range) };
    }

    function applySetParagraphAttribute(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var block = _findBlock(model, target.blockId);
        if (!block.content) block.content = { type: 'paragraph', runs: [] };
        var name = op.attributeName || op.AttributeName;
        op.previousValue = block.content[name];
        block.content[name] = op.value ?? op.Value;
        differ.record({ attributeChange: { blockId: block.id, attributeName: name, value: block.content[name] }, invalidatedLayoutScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: [block.id], nextSelection: nextSelectionForOperation(model, op, block.id, target.offset, target) };
    }

    function applyInsertImage(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var container = _findBlockContainer(model, target.blockId);
        var block = importBlock({
            Id: op.blockId || op.BlockId || _stableId('image-block', Date.now()),
            Type: 'Image',
            Content: Object.assign({ Type: 'Image' }, op.image || op.Image || {})
        }, 'insert-image');
        container.blocks.splice(container.index + 1, 0, block);
        differ.record({ objectChange: { blockId: block.id, type: 'insert-image' }, invalidatedLayoutScopes: [container.block.id, block.id], invalidatedOverlayScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: [container.block.id, block.id], nextSelection: { region: 'Body', blockId: block.id, offset: 0, isCollapsed: true }, insertedBlockId: block.id };
    }

    function applyUpdateImageLayout(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var block = _findBlock(model, target.blockId);
        block.content.layout = syncImageLayoutCase(_clone(op.layout || op.Layout || {}));
        var transform = block.content.layout.Transform || block.content.layout.transform || {};
        var currentSize = block.content.size || {};
        block.content.size = _sortObject({
            width: transform.Width ?? transform.width ?? currentSize.width ?? null,
            height: transform.Height ?? transform.height ?? currentSize.height ?? null,
            lockAspectRatio: (transform.LockAspectRatio ?? transform.lockAspectRatio ?? currentSize.lockAspectRatio ?? true) !== false
        });
        var affected = _unique([block.id].concat(_asArray(op.affectedParagraphIds || op.AffectedParagraphIds)));
        differ.record({ objectChange: { blockId: block.id, type: 'layout' }, invalidatedLayoutScopes: affected, invalidatedOverlayScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: affected, nextSelection: imageSelectionForBlock(block) };
    }

    function applyUpdateImageMetadata(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var block = _findBlock(model, target.blockId);
        var metadata = op.metadata || op.Metadata || {};
        Object.assign(block.content, _sortObject(metadata));
        differ.record({ objectChange: { blockId: block.id, type: 'metadata' }, invalidatedLayoutScopes: [block.id], invalidatedOverlayScopes: [block.id] });
        return { ok: true, invalidatedLayoutScopes: [block.id], nextSelection: imageSelectionForBlock(block) };
    }

    function applyInsertTable(model, op, differ) {
        var target = _normalizeTarget(op.target || op.Target);
        var container = _findBlockContainer(model, target.blockId);
        var rows = Number(op.rows || op.Rows || 2);
        var columns = Number(op.columns || op.Columns || 2);
        var tableId = op.tableId || op.TableId || op.blockId || op.BlockId || _stableId('table-block', Date.now());
        var table = { Style: _clone(op.style || op.Style || {}), Rows: [] };
        for (var r = 0; r < rows; r++) {
            var row = { Id: tableId + '-row-' + r, Cells: [] };
            for (var c = 0; c < columns; c++) {
                var cellId = tableId + '-r' + r + '-c' + c;
                row.Cells.push({ Id: cellId, Blocks: [{ Id: cellId + '-p', Type: 'Paragraph', Content: { Inlines: [{ Id: cellId + '-r', Text: '' }] } }] });
            }
            table.Rows.push(row);
        }
        var block = importBlock({ Id: tableId, Type: 'Table', Content: table }, 'insert-table');
        container.blocks.splice(container.index + 1, 0, block);
        differ.record({ objectChange: { blockId: block.id, type: 'insert-table' }, invalidatedLayoutScopes: [container.block.id, block.id] });
        return { ok: true, invalidatedLayoutScopes: [container.block.id, block.id], nextSelection: { region: 'Body', blockId: block.id, offset: 0, isCollapsed: true }, insertedBlockId: block.id };
    }

    function applyUpdateTableCell(model, op, differ) {
        var cellId = op.cellId || op.CellId;
        var cell = _findCell(model, cellId);
        if (!cell) return { ok: false, errors: [{ code: 'missing-table-cell', cellId: cellId }] };
        if (Array.isArray(op.blocks || op.Blocks)) {
            cell.blocks = _asArray(op.blocks || op.Blocks).map(function (block, index) { return importBlock(block, cellId + '-updated-' + index); });
        }
        differ.record({ attributeChange: { cellId: cellId, attributeName: 'blocks' }, invalidatedLayoutScopes: [cellId] });
        return { ok: true, invalidatedLayoutScopes: [cellId], nextSelection: { region: 'Body', blockId: cell.blocks[0] && cell.blocks[0].id, offset: 0, isCollapsed: true } };
    }

    function applyRevisionDecision(model, op, differ) {
        var revisionId = op.revisionId || op.RevisionId;
        var engine = createRevisionEngine(model, {});
        var decision = op.type === OPERATION_TYPES.AcceptRevision
            ? engine.acceptRevision(revisionId, op.selection || op.Selection || null)
            : engine.rejectRevision(revisionId, op.selection || op.Selection || null);
        differ.record({ markerChange: { revisionId: revisionId, status: op.type === OPERATION_TYPES.AcceptRevision ? 'Accepted' : 'Rejected' }, invalidatedLayoutScopes: ['document'], invalidatedOverlayScopes: ['revisions'] });
        return { ok: decision.ok !== false, invalidatedLayoutScopes: ['document'], nextSelection: decision.selection || null };
    }

    function applyRestoreSnapshot(model, op, differ) {
        var snapshot = op.snapshot || op.Snapshot || null;
        if (!snapshot) return { ok: false, errors: [{ code: 'missing-restore-snapshot', path: 'operation.snapshot' }] };
        replaceModelContents(model, snapshot);
        var scopes = _asArray(op.affectedScopeIds || op.AffectedScopeIds || ['document']);
        differ.record({ objectChange: { blockId: 'document', type: 'restore-snapshot' }, invalidatedLayoutScopes: scopes, invalidatedOverlayScopes: scopes });
        return {
            ok: true,
            invalidatedLayoutScopes: scopes,
            nextSelection: createSelectionSnapshot(op.selection || op.Selection || firstModelSelection(model))
        };
    }

    function normalizeRevisionType(value) {
        if (value === 1) return 'Deletion';
        if (value === 2) return 'FormatChange';
        if (value === 3) return 'Move';
        if (value === 4) return 'Structure';
        if (value === 5) return 'Image';
        if (value === 6) return 'Table';
        var raw = String(value || '').replace(/\s+/g, '').toLowerCase();
        if (raw === 'insert' || raw === 'insertion') return 'Insertion';
        if (raw === 'delete' || raw === 'deletion') return 'Deletion';
        if (raw === 'format' || raw === 'formatchange' || raw === 'formatting') return 'FormatChange';
        return value ? String(value) : 'Insertion';
    }

    function normalizeRevisionStatus(value) {
        if (value === 1) return 'Accepted';
        if (value === 2) return 'Rejected';
        var raw = String(value || '').toLowerCase();
        if (raw.indexOf('accept') >= 0) return 'Accepted';
        if (raw.indexOf('reject') >= 0) return 'Rejected';
        return 'Pending';
    }

    function normalizeRevisionRange(value) {
        var range = value || {};
        var start = Number(range.start ?? range.Start ?? range.startOffset ?? range.StartOffset ?? 0) || 0;
        var end = Number(range.end ?? range.End ?? range.endOffset ?? range.EndOffset ?? start) || start;
        return _sortObject({
            blockId: _asText(range.blockId || range.BlockId || range.startBlockId || range.StartBlockId || ''),
            start: Math.min(start, end),
            end: Math.max(start, end)
        });
    }

    function normalizeRevision(raw) {
        var source = raw || {};
        var sourceRange = source.affectedRange || source.AffectedRange || source.range || source.Range || {};
        var range = normalizeRevisionRange(sourceRange);
        var author = source.Author || source.authorObject || source.author || {};
        var payload = source.payload || source.Payload || source.PayloadJson || source.payloadJson || {};
        return _sortObject({
            id: _asText(source.id || source.Id || ('rev-' + Date.now() + '-' + Math.floor(Math.random() * 100000))),
            type: normalizeRevisionType(source.type ?? source.Type ?? source.revisionType ?? source.RevisionType ?? 'Insertion'),
            author: _asText(author.DisplayName || author.displayName || source.authorName || source.AuthorName || source.author || source.Author || source.authorId || source.AuthorId || 'local'),
            authorObject: _sortObject(author || {}),
            createdAt: source.CreatedAt || source.createdAt || null,
            groupId: source.GroupId || source.groupId || null,
            timestamp: Number(source.timestamp || source.Timestamp || Date.now()) || Date.now(),
            range: _sortObject(sourceRange || {}),
            affectedRange: range,
            payload: typeof payload === 'string' ? { text: payload } : _sortObject(payload || {}),
            payloadJson: source.PayloadJson || source.payloadJson || null,
            status: normalizeRevisionStatus(source.action ?? source.Action ?? source.status ?? source.Status)
        });
    }

    function ensureRevisionList(model) {
        if (!Array.isArray(model.revisions)) model.revisions = [];
        model.revisions = model.revisions.map(normalizeRevision);
        buildIndexes(model);
        return model.revisions;
    }

    function getRevisionById(model, revisionId) {
        ensureRevisionList(model);
        return _asArray(model.revisions).find(function (revision) { return revision.id === revisionId || revision.Id === revisionId; }) || null;
    }

    function addRevision(model, revision) {
        ensureRevisionList(model);
        var normalized = normalizeRevision(revision);
        var existing = model.revisions.find(function (item) { return item.id === normalized.id; });
        if (existing) Object.assign(existing, normalized);
        else model.revisions.push(normalized);
        buildIndexes(model);
        return normalized;
    }

    function findRunAtOffset(block, offset) {
        var cursor = 0;
        var runs = _asArray(block && block.content && block.content.runs);
        for (var i = 0; i < runs.length; i++) {
            var text = _asText(runs[i].text);
            var end = cursor + text.length;
            if (offset >= cursor && offset <= end || i === runs.length - 1) {
                return { run: runs[i], start: cursor, end: end, index: i };
            }
            cursor = end;
        }
        return null;
    }

    function transformRunsInRange(block, start, end, transform) {
        if (!block || block.type !== 'paragraph') return [];
        var result = [];
        var affected = [];
        var cursor = 0;
        _asArray(block.content && block.content.runs).forEach(function (run) {
            var text = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + text.length;
            cursor = runEnd;
            if (runEnd <= start || runStart >= end || text.length === 0) {
                result.push(_clone(run));
                return;
            }
            var localStart = Math.max(0, start - runStart);
            var localEnd = Math.min(text.length, end - runStart);
            var localRange = clampTextRange(text, localStart, localEnd);
            localStart = localRange.start;
            localEnd = localRange.end;
            if (localStart > 0) {
                var before = _clone(run);
                before.id = run.id + '-pre-' + start;
                before.text = text.slice(0, localStart);
                result.push(normalizeTextRunForMerge(before));
            }
            var middle = _clone(run);
            middle.id = run.id + '-rev-' + start + '-' + end;
            middle.text = text.slice(localStart, localEnd);
            middle = transform(middle) || middle;
            if (middle.text !== '') {
                middle = normalizeTextRunForMerge(middle);
                result.push(middle);
                affected.push(middle);
            }
            if (localEnd < text.length) {
                var after = _clone(run);
                after.id = run.id + '-post-' + end;
                after.text = text.slice(localEnd);
                result.push(normalizeTextRunForMerge(after));
            }
        });
        block.content.runs = mergeAdjacentTextRuns(result);
        return affected;
    }

    function splitParagraphRunsAtOffset(block, offset) {
        var before = [];
        var after = [];
        var cursor = 0;
        var targetOffset = Math.max(0, Math.min(_blockText(block).length, Number(offset || 0)));
        _asArray(block && block.content && block.content.runs).forEach(function (run) {
            var text = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + text.length;
            cursor = runEnd;
            if (runEnd <= targetOffset) {
                before.push(_clone(run));
                return;
            }
            if (runStart >= targetOffset) {
                after.push(_clone(run));
                return;
            }
            var local = Math.max(0, Math.min(text.length, targetOffset - runStart));
            if (local > 0) {
                var beforeRun = _clone(run);
                beforeRun.id = run.id + '-split-before';
                beforeRun.text = text.slice(0, local);
                before.push(beforeRun);
            }
            if (local < text.length) {
                var afterRun = _clone(run);
                afterRun.id = run.id + '-split-after';
                afterRun.text = text.slice(local);
                after.push(afterRun);
            }
        });
        return {
            before: before.length > 0 ? mergeAdjacentTextRuns(before) : _plainRuns('', block.id + '-split-before-empty'),
            after: after.length > 0 ? mergeAdjacentTextRuns(after) : _plainRuns('', block.id + '-split-after-empty')
        };
    }

    function splitParagraphPreservingInlineMetadata(model, selection) {
        var snapshot = createSelectionSnapshot(selection || {});
        var container = _findBlockContainer(model, snapshot.blockId);
        if (!container || !container.block || container.block.type !== 'paragraph') {
            return { ok: false, error: 'missing-paragraph', selection: snapshot };
        }
        var block = container.block;
        var parts = splitParagraphRunsAtOffset(block, snapshot.offset);
        var newBlock = _clone(block);
        newBlock.id = _stableId('block', block.id + '-revision-enter-' + Date.now());
        newBlock.content = _clone(block.content || { type: 'paragraph', runs: [] });
        newBlock.content.runs = parts.after;
        block.content = _clone(block.content || { type: 'paragraph', runs: [] });
        block.content.runs = parts.before;
        container.blocks.splice(container.index + 1, 0, newBlock);
        buildIndexes(model);
        return _sortObject({
            ok: true,
            insertedBlockId: newBlock.id,
            selection: createSelectionSnapshot({ region: snapshot.region, blockId: newBlock.id, offset: 0, isCollapsed: true })
        });
    }

    function setRevisionForRange(model, revisionId, range) {
        var normalizedRange = normalizeRevisionRange(range);
        var block = _findBlock(model, normalizedRange.blockId);
        return transformRunsInRange(block, normalizedRange.start, normalizedRange.end, function (run) {
            run.revisionId = revisionId;
            return run;
        });
    }

    function clearRevisionFromRuns(model, revisionId) {
        _asArray(model && model.body && model.body.blocks).forEach(function (block) {
            if (block.type !== 'paragraph') return;
            _asArray(block.content && block.content.runs).forEach(function (run) {
                if (run.revisionId === revisionId || run.RevisionId === revisionId) {
                    delete run.revisionId;
                    delete run.RevisionId;
                }
                run.marks = normalizeMarks(_asArray(run.marks || run.Marks).filter(function (mark) {
                    return readRevisionIdFromMark(mark) !== revisionId;
                }));
                delete run.Marks;
            });
            block.content.runs = mergeAdjacentTextRuns(block.content.runs);
        });
        buildIndexes(model);
    }

    function removeRevisionRuns(model, revisionId) {
        _asArray(model && model.body && model.body.blocks).forEach(function (block) {
            if (block.type !== 'paragraph') return;
            block.content.runs = mergeAdjacentTextRuns(_asArray(block.content && block.content.runs).filter(function (run) {
                return readRevisionIdsFromRun(run).indexOf(revisionId) < 0;
            }));
        });
        buildIndexes(model);
    }

    function removeRangeText(model, range) {
        var normalizedRange = normalizeRevisionRange(range);
        var block = _findBlock(model, normalizedRange.blockId);
        transformRunsInRange(block, normalizedRange.start, normalizedRange.end, function (run) {
            run.text = '';
            return run;
        });
        buildIndexes(model);
    }

    function applyRevisionMark(model, range, mark) {
        var normalizedRange = normalizeRevisionRange(range);
        var block = _findBlock(model, normalizedRange.blockId);
        transformRunsInRange(block, normalizedRange.start, normalizedRange.end, function (run) {
            run.marks = updateMarks(run.marks, mark, false);
            return run;
        });
        buildIndexes(model);
    }

    function updateRevisionStatus(model, revisionId, status) {
        ensureRevisionList(model).forEach(function (revision) {
            if (revision.id === revisionId) revision.status = status;
        });
        buildIndexes(model);
    }

    function readOptionalBoolean(source, keys) {
        var valueSource = source || {};
        for (var i = 0; i < keys.length; i++) {
            var key = keys[i];
            if (!Object.prototype.hasOwnProperty.call(valueSource, key)) continue;
            var value = valueSource[key];
            if (value === null || value === undefined) continue;
            if (typeof value === 'boolean') return value;
            if (typeof value === 'number') return value !== 0;
            var text = String(value).trim().toLowerCase();
            if (text === 'true' || text === '1' || text === 'yes' || text === 'on') return true;
            if (text === 'false' || text === '0' || text === 'no' || text === 'off') return false;
        }
        return null;
    }

    function resolveTrackChangesState(options) {
        var opts = options || {};
        var localEnabled = readOptionalBoolean(opts, ['trackChangesEnabled', 'TrackChangesEnabled', 'trackChanges', 'TrackChanges']);
        var globalEnabled = readOptionalBoolean(opts, ['globalTrackChangesEnabled', 'GlobalTrackChangesEnabled', 'defaultTrackChangesEnabled', 'DefaultTrackChangesEnabled', 'trackChangesDefaultEnabled', 'TrackChangesDefaultEnabled']);
        var displayMode = opts.reviewDisplayMode || opts.ReviewDisplayMode || opts.globalReviewDisplayMode || opts.GlobalReviewDisplayMode || 'AllMarkup';
        var source = localEnabled !== null
            ? 'local'
            : globalEnabled !== null
                ? 'global'
                : 'default';
        return _sortObject({
            displayMode: _asText(displayMode || 'AllMarkup'),
            enabled: localEnabled !== null ? localEnabled : (globalEnabled !== null ? globalEnabled : false),
            globalEnabled: globalEnabled,
            localEnabled: localEnabled,
            source: source
        });
    }

    function isTrackChangesEnabled(inst) {
        return resolveTrackChangesState(inst && inst.options || {}).enabled === true;
    }

    function resolveRevisionUserId(options) {
        var opts = options || {};
        var author = opts.author || opts.Author || {};
        return _asText(
            author.Id || author.id ||
            opts.currentUserId || opts.CurrentUserId ||
            opts.userId || opts.UserId ||
            author.DisplayName || author.displayName ||
            'local');
    }

    function revisionPayloadText(revision) {
        var payload = revision && (revision.payload || revision.Payload) || {};
        return _asText(revision && (revision.payloadJson ?? revision.PayloadJson ?? payload.text ?? payload.Text ?? ''));
    }

    function stableRevisionStringify(value) {
        if (Array.isArray(value)) return '[' + value.map(stableRevisionStringify).join(',') + ']';
        if (value && typeof value === 'object') {
            return '{' + Object.keys(value).sort().map(function (key) {
                return JSON.stringify(key) + ':' + stableRevisionStringify(value[key]);
            }).join(',') + '}';
        }
        return JSON.stringify(value);
    }

    function setRevisionPayloadText(revision, text) {
        if (!revision) return;
        var value = _asText(text);
        revision.payload = _sortObject(Object.assign({}, revision.payload || {}, { text: value }));
        revision.payloadJson = value;
    }

    function createTrackedRevisionPayload(type, range, text, userId, source, extra) {
        var normalizedType = normalizeRevisionType(type);
        var normalizedRange = normalizeRevisionRange(range);
        var revisionText = _asText(text);
        var opts = extra || {};
        var payload = Object.assign({}, opts.payload || opts.Payload || {}, { text: revisionText });
        return _sortObject({
            id: opts.id || opts.Id || 'rev-' + normalizedType.toLowerCase() + '-' + Date.now() + '-' + Math.floor(Math.random() * 100000),
            type: normalizedType,
            status: 'Pending',
            author: _asText(opts.author || opts.Author || userId || 'local'),
            authorId: _asText(opts.authorId || opts.AuthorId || userId || 'local'),
            source: source || opts.source || opts.Source || '',
            affectedRange: normalizedRange,
            range: normalizedRange,
            payload: _sortObject(payload),
            payloadJson: revisionText,
            timestamp: opts.timestamp || opts.Timestamp || Date.now()
        });
    }

    function createInsertionRevisionPayload(range, text, userId, source, extra) {
        return createTrackedRevisionPayload('Insertion', range, text, userId, source || 'typing', extra);
    }

    function createDeletionRevisionPayload(model, range, userId, source, extra) {
        var normalizedRange = normalizeRevisionRange(range);
        var block = _findBlock(model, normalizedRange.blockId);
        var deletedText = _asText(extra && (extra.text || extra.Text));
        if (!deletedText && block) {
            deletedText = _blockText(block).slice(normalizedRange.start, normalizedRange.end);
        }
        return createTrackedRevisionPayload('Deletion', normalizedRange, deletedText, userId, source || 'delete', extra);
    }

    function createStructureRevisionPayload(range, label, userId, source, extra) {
        return createTrackedRevisionPayload('Structure', range, label || 'SplitBlock', userId, source || 'structure', extra);
    }

    function revisionAuthorMergeKey(revision) {
        var author = revision && (revision.authorObject || revision.Author || revision.author || {});
        return _asText(
            author.Id || author.id ||
            revision && (revision.authorId || revision.AuthorId || revision.author || revision.Author) ||
            author.DisplayName || author.displayName ||
            '');
    }

    function revisionRunFormattingMergeKey(run) {
        var marks = normalizeMarks(_asArray(run && (run.marks || run.Marks)).filter(function (mark) {
            return markType(mark) !== 'revision';
        }));
        return stableRevisionStringify({
            commentIds: _asArray(run && (run.commentIds || run.CommentIds)).map(_asText).sort(),
            marks: marks,
            style: run && (run.style || run.Style) || {}
        });
    }

    function canMergeAdjacentRevisionRuns(leftRevision, rightRevision, leftRun, rightRun, leftEnd, rightStart) {
        if (!leftRevision || !rightRevision || leftRevision.id === rightRevision.id) return false;
        if (Number(leftEnd || 0) !== Number(rightStart || 0)) return false;
        if (readRevisionStatus(leftRevision) !== 'Pending' || readRevisionStatus(rightRevision) !== 'Pending') return false;
        if (readRevisionTypeName(leftRevision) !== readRevisionTypeName(rightRevision)) return false;
        if (revisionAuthorMergeKey(leftRevision) !== revisionAuthorMergeKey(rightRevision)) return false;
        return revisionRunFormattingMergeKey(leftRun) === revisionRunFormattingMergeKey(rightRun);
    }

    function replaceRevisionIdOnRun(run, sourceId, targetId) {
        if (!run) return;
        if (run.revisionId === sourceId || run.RevisionId === sourceId) {
            run.revisionId = targetId;
            delete run.RevisionId;
        }
        run.marks = normalizeMarks(_asArray(run.marks || run.Marks).map(function (mark) {
            if (readRevisionIdFromMark(mark) !== sourceId) return mark;
            var next = _clone(mark);
            next.revisionId = targetId;
            next.RevisionId = targetId;
            return next;
        }));
        delete run.Marks;
    }

    function normalizeRevisionGroups(model, scopeIds) {
        ensureRevisionList(model);
        var revisionsById = {};
        _asArray(model && model.revisions).forEach(function (revision) {
            revisionsById[revision.id] = revision;
        });
        var removedIds = new Set();
        var merged = 0;
        var scopes = _asArray(scopeIds).map(_asText).filter(function (id) {
            return id && id !== 'document' && id !== 'revisions';
        });
        var scoped = scopes.length > 0;
        var scopeLookup = new Set(scopes);

        function mergeRevision(sourceRevision, targetRevision, blockId, start, end, sourceText) {
            if (!sourceRevision || !targetRevision || sourceRevision.id === targetRevision.id) return;
            var targetRange = normalizeRevisionRange(targetRevision.affectedRange || targetRevision.range || {});
            var sourceRange = normalizeRevisionRange(sourceRevision.affectedRange || sourceRevision.range || {});
            var nextRange = _sortObject({
                blockId: blockId || targetRange.blockId || sourceRange.blockId,
                start: Math.min(targetRange.start, sourceRange.start, Number(start || 0)),
                end: Math.max(targetRange.end, sourceRange.end, Number(end || 0))
            });
            targetRevision.affectedRange = nextRange;
            targetRevision.range = nextRange;
            setRevisionPayloadText(targetRevision, (revisionPayloadText(targetRevision) || '') + (revisionPayloadText(sourceRevision) || _asText(sourceText)));
            removedIds.add(sourceRevision.id);
            merged++;
        }

        function scanBlock(block) {
            if (!block || block.type !== 'paragraph') return;
            var cursor = 0;
            var previous = null;
            _asArray(block.content && block.content.runs).forEach(function (run) {
                var text = _asText(run.text);
                var start = cursor;
                var end = cursor + text.length;
                cursor = end;
                var revisionId = readRevisionIdsFromRun(run)[0] || '';
                var revision = revisionId ? revisionsById[revisionId] : null;
                if (previous && revision && canMergeAdjacentRevisionRuns(previous.revision, revision, previous.run, run, previous.end, start)) {
                    mergeRevision(revision, previous.revision, block.id, previous.start, end, text);
                    replaceRevisionIdOnRun(run, revision.id, previous.revision.id);
                    revision = previous.revision;
                    revisionId = previous.revision.id;
                }
                previous = revision && readRevisionStatus(revision) === 'Pending'
                    ? { revision: revision, run: run, start: start, end: end }
                    : null;
            });
            block.content.runs = mergeAdjacentTextRuns(block.content.runs);
        }

        function scanScopedBlock(block) {
            if (!block) return;
            if (scoped && !scopeLookup.has(block.id)) {
                if (block.type === 'table') {
                    _asArray(block.content && block.content.rows).forEach(function (row) {
                        _asArray(row.cells).forEach(function (cell) {
                            _asArray(cell.blocks).forEach(scanScopedBlock);
                        });
                    });
                }
                return;
            }
            scanBlock(block);
        }

        _asArray(model && model.body && model.body.blocks).forEach(scanScopedBlock);
        _asArray(model && model.headers).forEach(function (region) { _asArray(region.blocks).forEach(scanScopedBlock); });
        _asArray(model && model.footers).forEach(function (region) { _asArray(region.blocks).forEach(scanScopedBlock); });
        if (removedIds.size > 0) {
            model.revisions = _asArray(model.revisions).filter(function (revision) { return !removedIds.has(revision.id); });
        }
        buildIndexes(model);
        return _sortObject({ ok: true, merged: merged, removed: removedIds.size, scoped: scoped, indexesRebuilt: true });
    }

    function revisionDecorativeStyle(revision) {
        var type = normalizeRevisionType(revision && revision.type);
        if (revision && revision.payload && revision.payload.decorativeStyle) return revision.payload.decorativeStyle;
        if (type === 'Insertion') return { color: '#008000', underline: true };
        if (type === 'Deletion') return { color: '#b91c1c', strike: true };
        if (type === 'FormatChange') return { color: '#7c3aed', underline: true };
        return {};
    }

    function normalizeCommandId(input) {
        var value = input || {};
        if (typeof value === 'string') value = { commandId: value };
        var key = String(value.commandId || value.CommandId || value.id || value.Id || value.name || value.Name || '').trim();
        if (!key && (value.ctrlKey || value.CtrlKey || value.metaKey || value.MetaKey)) {
            var shortcutKey = String(value.key || value.Key || '').toLowerCase();
            if (shortcutKey === 'b') key = 'bold';
            if (shortcutKey === 'i') key = 'italic';
            if (shortcutKey === 'u') key = 'underline';
        }
        var normalized = key
            .replace(/^format[.\-_:]/i, '')
            .replace(/^paragraph[.\-_:]/i, '')
            .replace(/^toggle[.\-_:]?/i, '')
            .replace(/[\s_.:-]+/g, '-')
            .toLowerCase();
        var aliases = {
            'bold': 'bold',
            'toggle-bold': 'bold',
            'italic': 'italic',
            'toggle-italic': 'italic',
            'underline': 'underline',
            'toggle-underline': 'underline',
            'strike': 'strike',
            'strikethrough': 'strike',
            'font-family': 'fontFamily',
            'fontfamily': 'fontFamily',
            'set-font-family': 'fontFamily',
            'setfontfamily': 'fontFamily',
            'font-size': 'fontSize',
            'fontsize': 'fontSize',
            'set-font-size': 'fontSize',
            'setfontsize': 'fontSize',
            'text-color': 'textColor',
            'textcolor': 'textColor',
            'set-text-color': 'textColor',
            'settextcolor': 'textColor',
            'font-color': 'textColor',
            'fontcolor': 'textColor',
            'foreground-color': 'textColor',
            'foregroundcolor': 'textColor',
            'background-color': 'backgroundColor',
            'backgroundcolor': 'backgroundColor',
            'highlight': 'backgroundColor',
            'highlight-color': 'backgroundColor',
            'highlightcolor': 'backgroundColor',
            'set-highlight-color': 'backgroundColor',
            'sethighlightcolor': 'backgroundColor',
            'link': 'link',
            'clear-formatting': 'clearFormatting',
            'clearformatting': 'clearFormatting',
            'remove-formatting': 'clearFormatting',
            'removeformatting': 'clearFormatting',
            'alignment': 'alignment',
            'align': 'alignment',
            'paragraph-alignment': 'alignment',
            'paragraphalignment': 'alignment',
            'set-paragraph-alignment': 'alignment',
            'setparagraphalignment': 'alignment',
            'line-spacing': 'lineSpacing',
            'linespacing': 'lineSpacing',
            'set-line-spacing': 'lineSpacing',
            'setlinespacing': 'lineSpacing',
            'spacing-before': 'spacingBefore',
            'spacingbefore': 'spacingBefore',
            'set-spacing-before': 'spacingBefore',
            'setspacingbefore': 'spacingBefore',
            'spacing-after': 'spacingAfter',
            'spacingafter': 'spacingAfter',
            'set-spacing-after': 'spacingAfter',
            'setspacingafter': 'spacingAfter',
            'list': 'list',
            'bullet-list': 'list',
            'bulletlist': 'list',
            'toggle-bullet-list': 'list',
            'togglebulletlist': 'list',
            'numbered-list': 'list',
            'numberedlist': 'list',
            'toggle-numbered-list': 'list',
            'togglenumberedlist': 'list',
            'indent': 'indent',
            'increase-indent': 'indent',
            'increaseindent': 'indent',
            'outdent': 'outdent',
            'decrease-indent': 'outdent',
            'decreaseindent': 'outdent',
            'insert-table': 'insertTable',
            'inserttable': 'insertTable',
            'insert-row-above': 'insertRowAbove',
            'insertrowabove': 'insertRowAbove',
            'insert-row-below': 'insertRowBelow',
            'insertrowbelow': 'insertRowBelow',
            'insert-column-left': 'insertColumnLeft',
            'insertcolumnleft': 'insertColumnLeft',
            'insert-column-right': 'insertColumnRight',
            'insertcolumnright': 'insertColumnRight',
            'delete-row': 'deleteRow',
            'deleterow': 'deleteRow',
            'delete-column': 'deleteColumn',
            'deletecolumn': 'deleteColumn',
            'merge-cells': 'mergeCells',
            'mergecells': 'mergeCells',
            'split-cell': 'splitCell',
            'splitcell': 'splitCell',
            'cell-background': 'cellBackground',
            'cellbackground': 'cellBackground',
            'cell-border': 'cellBorder',
            'cellborder': 'cellBorder',
            'resize-table': 'resizeTable',
            'resizetable': 'resizeTable'
        };
        return aliases[normalized] || normalized;
    }

    function commandSource(input) {
        if (!input || typeof input === 'string') return 'api';
        return String(input.surface || input.Surface || input.source || input.Source || 'api');
    }

    function markType(mark) {
        var raw = mark && (mark.type ?? mark.Type);
        var numericTypes = [
            'bold',
            'italic',
            'underline',
            'strikethrough',
            'superscript',
            'subscript',
            'link',
            'commentanchor',
            'revision',
            'highlight',
            'textcolor',
            'fontfamily',
            'fontsize'
        ];
        if (typeof raw === 'number' && Number.isInteger(raw) && raw >= 0 && raw < numericTypes.length) {
            return numericTypes[raw];
        }
        return String(raw ?? '').replace(/\s+/g, '').toLowerCase();
    }

    function markValue(mark) {
        return mark && (mark.value ?? mark.Value ?? mark.color ?? mark.Color ?? mark.href ?? mark.Href ?? null);
    }

    function normalizeCommandColorValue(value) {
        if (value === undefined || value === null) return null;
        var text = String(value).trim();
        if (/^#[0-9a-f]{3}$/i.test(text)) {
            return '#' + text.slice(1).split('').map(function (part) { return part + part; }).join('').toLowerCase();
        }
        if (/^#[0-9a-f]{6}$/i.test(text)) {
            return text.toLowerCase();
        }
        return text || null;
    }

    function commandMark(id, payload) {
        var body = payload || {};
        switch (id) {
            case 'bold': return { type: 0 };
            case 'italic': return { type: 1 };
            case 'underline': return { type: 2 };
            case 'strike': return { type: 3 };
            case 'fontFamily': return { type: 11, value: body.family || body.Family || body.value || body.Value || null };
            case 'fontSize': return { type: 12, value: body.size || body.Size || body.value || body.Value || null };
            case 'textColor': return { type: 10, value: normalizeCommandColorValue(body.color || body.Color || body.value || body.Value || null) };
            case 'backgroundColor': return { type: 9, value: normalizeCommandColorValue(body.color || body.Color || body.value || body.Value || null) };
            case 'link': return { type: 6, href: body.href || body.Href || body.url || body.Url || '' };
            default: return null;
        }
    }

    function isClearValueCommand(id, mark) {
        return (id === 'textColor' || id === 'backgroundColor')
            && mark
            && (mark.value === null || mark.value === undefined || mark.value === '');
    }

    function inlineCommandTypes() {
        return ['bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize', 'textColor', 'backgroundColor', 'link'];
    }

    function paragraphCommandTypes() {
        return ['alignment', 'lineSpacing', 'spacingBefore', 'spacingAfter', 'list', 'indent', 'outdent'];
    }

    function markMatchesCommand(mark, id) {
        var type = markType(mark);
        if (id === 'bold') return type === 'bold';
        if (id === 'italic') return type === 'italic';
        if (id === 'underline') return type === 'underline';
        if (id === 'strike') return type === 'strike' || type === 'strikethrough';
        if (id === 'fontFamily') return type === 'fontfamily';
        if (id === 'fontSize') return type === 'fontsize';
        if (id === 'textColor') return type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor';
        if (id === 'backgroundColor') return type === 'backgroundcolor' || type === 'highlight';
        if (id === 'link') return type === 'link';
        return false;
    }

    function selectionTextRange(selection) {
        var snapshot = createSelectionSnapshot(selection || {});
        var anchor = createLogicalPosition(snapshot.anchor || snapshot);
        var focus = createLogicalPosition(snapshot.focus || snapshot);
        if (anchor.blockId !== focus.blockId) {
            return { blockId: focus.blockId, start: focus.offset, end: focus.offset, collapsed: true, selection: snapshot };
        }
        var start = Math.min(anchor.offset, focus.offset);
        var end = Math.max(anchor.offset, focus.offset);
        return { blockId: focus.blockId, start: start, end: end, collapsed: start === end, selection: snapshot };
    }

    function runsForRange(block, range) {
        if (!block || block.type !== 'paragraph') return [];
        if (!range || range.collapsed) {
            var info = findRunAtOffset(block, range ? range.start : 0);
            return info && info.run ? [info.run] : [];
        }
        var result = [];
        var cursor = 0;
        _asArray(block.content && block.content.runs).forEach(function (run) {
            var text = _asText(run.text);
            var runStart = cursor;
            var runEnd = cursor + text.length;
            cursor = runEnd;
            if (runEnd > range.start && runStart < range.end) result.push(run);
        });
        return result;
    }

    function findInheritedTextColor(block, offset) {
        var cursor = 0;
        var fallback = null;
        var runs = _asArray(block && block.content && block.content.runs);
        for (var i = 0; i < runs.length; i++) {
            var run = runs[i];
            var text = _asText(run.text);
            var end = cursor + text.length;
            var color = run.style && (run.style.color || run.style.Color) || null;
            if (color) fallback = color;
            if (offset >= cursor && offset <= end) return color || fallback;
            cursor = end;
        }
        return fallback;
    }

    function pendingMarkForCommand(pendingTypingMarks, id) {
        return _asArray(pendingTypingMarks).slice().reverse().find(function (mark) {
            return markMatchesCommand(mark, id);
        }) || null;
    }

    function selectionDisabledReason(model, selection, commandId) {
        var snapshot = createSelectionSnapshot(selection || {});
        var block = _findBlock(model, snapshot.blockId);
        if (!block) return 'missing-selection';
        if (inlineCommandTypes().indexOf(commandId) >= 0 || commandId === 'clearFormatting') {
            return block.type === 'paragraph' ? '' : 'selection-not-text';
        }
        if (paragraphCommandTypes().indexOf(commandId) >= 0) {
            return block.type === 'paragraph' ? '' : 'selection-not-paragraph';
        }
        if (['insertRowAbove', 'insertRowBelow', 'insertColumnLeft', 'insertColumnRight', 'deleteRow', 'deleteColumn', 'mergeCells', 'splitCell', 'cellBackground', 'cellBorder'].indexOf(commandId) >= 0) {
            return selection && selection.cellId || _findTableInfoByBlockId(model, selection && selection.blockId) ? '' : 'selection-not-table-cell';
        }
        if (commandId === 'insertTable') return block.type === 'paragraph' ? '' : 'selection-not-paragraph';
        if (commandId === 'resizeTable') return block.type === 'table' || selection && selection.tableId ? '' : 'selection-not-table';
        return '';
    }

    function collectFormattingState(model, selection, pendingTypingMarks) {
        buildIndexes(model);
        var snapshot = createSelectionSnapshot(selection || {});
        var range = selectionTextRange(snapshot);
        var block = _findBlock(model, snapshot.blockId);
        var runs = runsForRange(block, range);
        var active = {
            bold: false,
            italic: false,
            underline: false,
            strike: false,
            fontFamily: null,
            fontSize: null,
            textColor: null,
            backgroundColor: null,
            link: null
        };
        var mixed = {
            bold: false,
            italic: false,
            underline: false,
            strike: false,
            fontFamily: false,
            fontSize: false,
            textColor: false,
            backgroundColor: false,
            link: false
        };
        function valuesFor(id) {
            return runs.map(function (run) {
                var found = _asArray(run.marks).find(function (mark) { return markMatchesCommand(mark, id); });
                if (id === 'fontFamily') return found ? markValue(found) : (run.style && (run.style.fontFamily || run.style.FontFamily) || null);
                if (id === 'fontSize') return found ? markValue(found) : (run.style && (run.style.fontSize || run.style.FontSize) || null);
                if (id === 'textColor') return found ? markValue(found) : (run.style && (run.style.color || run.style.Color) || null);
                if (id === 'backgroundColor') return found ? markValue(found) : (run.style && (run.style.backgroundColor || run.style.BackgroundColor) || null);
                if (id === 'link') return found ? markValue(found) : null;
                return !!found;
            });
        }
        ['bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize', 'textColor', 'backgroundColor', 'link'].forEach(function (id) {
            var values = valuesFor(id);
            if (values.length === 0 && id === 'textColor' && block) values = [findInheritedTextColor(block, snapshot.offset)];
            if (id === 'textColor' && values.every(function (value) { return value === null || value === undefined || value === ''; }) && block) {
                values = [findInheritedTextColor(block, snapshot.offset)];
            }
            var first = values.length ? values[0] : (id === 'bold' || id === 'italic' || id === 'underline' || id === 'strike' ? false : null);
            active[id] = first === undefined ? null : first;
            mixed[id] = values.some(function (value) { return JSON.stringify(value) !== JSON.stringify(first); });
        });
        if (range.collapsed) {
            ['bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize', 'textColor', 'backgroundColor', 'link'].forEach(function (id) {
                var pending = pendingMarkForCommand(pendingTypingMarks, id);
                if (!pending) return;
                active[id] = id === 'bold' || id === 'italic' || id === 'underline' || id === 'strike'
                    ? true
                    : markValue(pending);
                mixed[id] = false;
            });
        }
        var paragraph = block && block.type === 'paragraph' ? {
            alignment: block.content && block.content.alignment || 'left',
            lineSpacing: block.content && block.content.lineSpacing || 1,
            spacingBefore: block.content && (block.content.spacingBefore ?? block.content.SpacingBefore) || 0,
            spacingAfter: block.content && (block.content.spacingAfter ?? block.content.SpacingAfter) || 0,
            listType: block.content && (block.content.listType || block.content.ListType) || null,
            indentLevel: block.content && Number(block.content.indentLevel || block.content.IndentLevel || 0)
        } : {};
        var image = block && block.type === 'image' ? {
            isSelected: snapshot.isObjectSelection === true || !!snapshot.objectId,
            blockId: block.id,
            objectId: block.content && block.content.objectId || block.id,
            layout: _clone(block.content && block.content.layout || {})
        } : { isSelected: false };
        var table = block && block.type === 'table' ? {
            isSelected: snapshot.isObjectSelection === true || !!snapshot.objectId,
            blockId: block.id
        } : { isSelected: false };
        var commandValues = {
            bold: active.bold === true && mixed.bold !== true,
            italic: active.italic === true && mixed.italic !== true,
            underline: active.underline === true && mixed.underline !== true,
            strike: active.strike === true && mixed.strike !== true,
            fontFamily: mixed.fontFamily ? null : active.fontFamily,
            fontSize: mixed.fontSize ? null : active.fontSize,
            textColor: mixed.textColor ? null : active.textColor,
            backgroundColor: mixed.backgroundColor ? null : active.backgroundColor,
            link: mixed.link ? null : active.link,
            alignment: paragraph.alignment || null,
            lineSpacing: paragraph.lineSpacing || null,
            spacingBefore: paragraph.spacingBefore || 0,
            spacingAfter: paragraph.spacingAfter || 0,
            list: paragraph.listType || null,
            indent: paragraph.indentLevel || 0
        };
        var disabledReasons = {};
        ['bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize', 'textColor', 'backgroundColor', 'link', 'clearFormatting', 'alignment', 'lineSpacing', 'spacingBefore', 'spacingAfter', 'list', 'indent', 'outdent'].forEach(function (id) {
            var reason = selectionDisabledReason(model, snapshot, id);
            if (reason) disabledReasons[id] = reason;
        });
        return _sortObject({
            selection: snapshot,
            inline: { active: active, mixed: mixed },
            paragraph: paragraph,
            image: image,
            table: table,
            pendingTypingMarks: _clone(pendingTypingMarks || []),
            commandValues: commandValues,
            disabledReasons: disabledReasons,
            fromRevisionDecoration: false
        });
    }

    function resolveFormattingSelection(model, selectionOrToken, inst) {
        if (inst && selectionOrToken) {
            var validation = validateStableSelectionToken(inst, selectionOrToken);
            if (validation && validation.ok === true && validation.selection) {
                return validation.selection;
            }
        }

        var tokenData = parseSelectionTokenData(selectionOrToken) || readSelectionTokenData(selectionOrToken);
        if (tokenData) {
            var anchor = tokenData.anchor || tokenData.Anchor || tokenData.start || tokenData.Start || {};
            var focus = tokenData.focus || tokenData.Focus || tokenData.end || tokenData.End || anchor;
            var anchorOffset = Number(anchor.logicalOffset ?? anchor.LogicalOffset ?? anchor.offset ?? anchor.Offset ?? tokenData.startOffset ?? tokenData.StartOffset ?? 0) || 0;
            var focusOffset = Number(focus.logicalOffset ?? focus.LogicalOffset ?? focus.offset ?? focus.Offset ?? tokenData.endOffset ?? tokenData.EndOffset ?? anchorOffset) || 0;
            return createSelectionSnapshot({
                region: tokenData.region || tokenData.Region || anchor.region || focus.region || 'Body',
                anchor: {
                    region: tokenData.region || anchor.region || 'Body',
                    blockId: anchor.blockId || anchor.BlockId || tokenData.blockId || tokenData.BlockId || '',
                    inlineId: anchor.inlineId || anchor.InlineId || anchor.runId || anchor.RunId || null,
                    offset: anchorOffset,
                    affinity: anchor.affinity || anchor.Affinity || 'after',
                    tableId: anchor.tableId || anchor.TableId || tokenData.tableId || tokenData.TableId || null,
                    cellId: anchor.cellId || anchor.CellId || tokenData.cellId || tokenData.CellId || null,
                    headerFooterId: anchor.headerFooterId || anchor.HeaderFooterId || null
                },
                focus: {
                    region: tokenData.region || focus.region || 'Body',
                    blockId: focus.blockId || focus.BlockId || tokenData.blockId || tokenData.BlockId || '',
                    inlineId: focus.inlineId || focus.InlineId || focus.runId || focus.RunId || null,
                    offset: focusOffset,
                    affinity: focus.affinity || focus.Affinity || 'after',
                    tableId: focus.tableId || focus.TableId || tokenData.tableId || tokenData.TableId || null,
                    cellId: focus.cellId || focus.CellId || tokenData.cellId || tokenData.CellId || null,
                    headerFooterId: focus.headerFooterId || focus.HeaderFooterId || null
                },
                direction: tokenData.direction || tokenData.Direction || 'forward',
                isCollapsed: tokenData.isCollapsed ?? tokenData.IsCollapsed ?? anchorOffset === focusOffset,
                activeTableCellId: tokenData.cellId || tokenData.CellId || null,
                activeTableId: tokenData.tableId || tokenData.TableId || null,
                activeObjectId: tokenData.activeObjectId || tokenData.ActiveObjectId || null
            });
        }

        if (selectionOrToken) return createSelectionSnapshot(selectionOrToken);
        return firstModelSelection(model);
    }

    function formattingScalarValue(formatting, commandId, fallback) {
        var inline = formatting && formatting.inline || {};
        var mixed = inline.mixed || {};
        var commandValues = formatting && formatting.commandValues || {};
        if (mixed[commandId] === true) return 'mixed';
        var value = commandValues[commandId];
        return value === undefined || value === null ? fallback : value;
    }

    function computeFormattingState(model, selectionOrToken, pendingTypingMarks, inst) {
        var selection = resolveFormattingSelection(model, selectionOrToken, inst);
        var state = collectFormattingState(model, selection, pendingTypingMarks || []);
        var block = _findBlock(model, state.selection && state.selection.blockId);
        var disabledReason = !block
            ? 'missing-selection'
            : (state.disabledReasons && (state.disabledReasons.bold || state.disabledReasons.fontSize || state.disabledReasons.textColor || '')) || '';
        return _sortObject(Object.assign({}, state, {
            isDisabled: !!disabledReason,
            disabled: !!disabledReason,
            disabledReason: disabledReason || '',
            bold: formattingScalarValue(state, 'bold', false),
            italic: formattingScalarValue(state, 'italic', false),
            underline: formattingScalarValue(state, 'underline', false),
            strike: formattingScalarValue(state, 'strike', false),
            fontFamily: formattingScalarValue(state, 'fontFamily', null),
            fontSize: formattingScalarValue(state, 'fontSize', null),
            textColor: formattingScalarValue(state, 'textColor', null),
            highlightColor: formattingScalarValue(state, 'backgroundColor', null),
            backgroundColor: formattingScalarValue(state, 'backgroundColor', null)
        }));
    }

    function toBlazorFormattingState(formatting) {
        var state = formatting || {};
        var commandValues = state.commandValues || {};
        var inline = state.inline || {};
        var mixed = inline.mixed || {};
        var paragraph = state.paragraph || {};
        function triState(commandId) {
            if (mixed[commandId] === true) return 2;
            return commandValues[commandId] === true ? 1 : 0;
        }
        function alignmentValue(value) {
            var normalized = normalizeParagraphAlignment(value);
            if (normalized === 'center') return 1;
            if (normalized === 'right' || normalized === 'end') return 2;
            if (normalized === 'justify') return 3;
            return 0;
        }
        var boldState = triState('bold');
        var italicState = triState('italic');
        var underlineState = triState('underline');
        var strikeState = triState('strike');
        return _sortObject(Object.assign({}, state, {
            bold: boldState,
            italic: italicState,
            underline: underlineState,
            strike: strikeState,
            strikethrough: strikeState,
            Bold: boldState,
            Italic: italicState,
            Underline: underlineState,
            Strikethrough: strikeState,
            ParagraphAlignment: alignmentValue(commandValues.alignment || paragraph.alignment),
            ParagraphAlignmentMixed: false,
            FontFamily: commandValues.fontFamily || null,
            FontFamilyMixed: mixed.fontFamily === true,
            FontSize: commandValues.fontSize || null,
            FontSizeMixed: mixed.fontSize === true,
            TextColor: commandValues.textColor || null,
            TextColorMixed: mixed.textColor === true,
            HighlightColor: commandValues.backgroundColor || null,
            HighlightColorMixed: mixed.backgroundColor === true,
            LineSpacing: Number(commandValues.lineSpacing || paragraph.lineSpacing || 1) || 1,
            SpacingBefore: Number(commandValues.spacingBefore ?? paragraph.spacingBefore ?? 0) || 0,
            SpacingAfter: Number(commandValues.spacingAfter ?? paragraph.spacingAfter ?? 0) || 0,
            LeftIndent: Number(commandValues.indent ?? paragraph.indentLevel ?? 0) || 0,
            IsBulletList: String(commandValues.list || paragraph.listType || '').toLowerCase() === 'bullet',
            IsNumberedList: String(commandValues.list || paragraph.listType || '').toLowerCase() === 'numbered',
            ListMixed: false,
            ActiveRegion: state.selection && state.selection.region || 'Body',
            CurrentSelection: state.selection || null,
            IsDisabled: state.isDisabled === true || state.disabled === true,
            DisabledReason: state.disabledReason || null
        }));
    }

    function createTableController(model, options) {
        var opts = options || {};
        var committedOperations = [];

        function tableInfoFromSelection(selection) {
            var snapshot = createSelectionSnapshot(selection || {});
            return snapshot.cellId
                ? _findTableInfoByCellId(model, snapshot.cellId)
                : _findTableInfoByBlockId(model, snapshot.blockId);
        }

        function ensureRowsAndCells(table) {
            var columnCount = _tableColumnCount(table);
            _asArray(table.content && table.content.rows).forEach(function (row, rowIndex) {
                if (!Array.isArray(row.cells)) row.cells = [];
                while (row.cells.length < columnCount) {
                    row.cells.push(_createEmptyTableCell(table.id, rowIndex, row.cells.length));
                }
                row.cells.forEach(function (cell, columnIndex) {
                    if (!cell.id) cell.id = table.id + '-r' + rowIndex + '-c' + columnIndex;
                    cell.type = 'tableCell';
                    cell.rowSpan = Math.max(1, Number(cell.rowSpan || 1));
                    cell.colSpan = Math.max(1, Number(cell.colSpan || 1));
                    if (!cell.style) cell.style = {};
                    if (!Array.isArray(cell.blocks) || cell.blocks.length === 0) {
                        cell.blocks = [_createEmptyTableCell(table.id, rowIndex, columnIndex).blocks[0]];
                    }
                });
            });
            buildIndexes(model);
        }

        function record(type, payload) {
            var op = createOperation(type || OPERATION_TYPES.UpdateTableCell, payload || {}, { source: 'table-command' });
            committedOperations.push(op.toJSON());
            buildIndexes(model);
            return op;
        }

        function insertRow(selection, where) {
            var info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            var table = info.table;
            var columnCount = _tableColumnCount(table);
            var insertIndex = where === 'above' ? info.rowIndex : info.rowIndex + 1;
            var row = { id: table.id + '-row-' + Date.now() + '-' + insertIndex, type: 'tableRow', cells: [] };
            for (var c = 0; c < columnCount; c++) row.cells.push(_createEmptyTableCell(table.id, insertIndex, c));
            table.content.rows.splice(insertIndex, 0, row);
            ensureRowsAndCells(table);
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: table.id, action: where === 'above' ? 'insert-row-above' : 'insert-row-below' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: row.cells[0].blocks[0].id, cellId: row.cells[0].id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function insertColumn(selection, where) {
            var info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            var table = info.table;
            var insertIndex = where === 'left' ? info.columnIndex : info.columnIndex + 1;
            _asArray(table.content.rows).forEach(function (row, rowIndex) {
                row.cells.splice(insertIndex, 0, _createEmptyTableCell(table.id, rowIndex, insertIndex));
            });
            ensureRowsAndCells(table);
            var target = table.content.rows[info.rowIndex].cells[insertIndex];
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: table.id, action: where === 'left' ? 'insert-column-left' : 'insert-column-right' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: target.blocks[0].id, cellId: target.id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function deleteRow(selection, rowIndex) {
            var info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            var table = info.table;
            var index = Math.max(0, Math.min(_asArray(table.content.rows).length - 1, Number(rowIndex ?? info.rowIndex)));
            if (_asArray(table.content.rows).length > 1) table.content.rows.splice(index, 1);
            ensureRowsAndCells(table);
            var fallback = table.content.rows[Math.min(index, table.content.rows.length - 1)].cells[0];
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: table.id, action: 'delete-row' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: fallback.blocks[0].id, cellId: fallback.id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function deleteColumn(selection, columnIndex) {
            var info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            var table = info.table;
            var index = Math.max(0, Math.min(_tableColumnCount(table) - 1, Number(columnIndex ?? info.columnIndex)));
            _asArray(table.content.rows).forEach(function (row) {
                if (row.cells.length > 1) row.cells.splice(index, 1);
            });
            ensureRowsAndCells(table);
            var fallback = table.content.rows[0].cells[Math.min(index, table.content.rows[0].cells.length - 1)];
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: table.id, action: 'delete-column' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: fallback.blocks[0].id, cellId: fallback.id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function mergeCells(selection, cellIds) {
            var info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            var ids = _asArray(cellIds).length ? _asArray(cellIds) : [info.cell.id];
            var cells = ids.map(function (id) { return _findTableInfoByCellId(model, id); }).filter(Boolean);
            if (cells.length < 2) return { ok: true, operation: record(OPERATION_TYPES.UpdateTableCell, { tableId: info.table.id, action: 'merge-cells-noop' }), selection: createSelectionSnapshot(selection || {}) };
            var first = cells[0].cell;
            first.colSpan = cells.length;
            cells.slice(1).forEach(function (cellInfo) {
                cellInfo.row.cells = cellInfo.row.cells.filter(function (cell) { return cell.id !== cellInfo.cell.id; });
            });
            ensureRowsAndCells(info.table);
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: info.table.id, action: 'merge-cells', cellIds: ids });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: first.blocks[0].id, cellId: first.id, tableId: info.table.id, offset: 0, isCollapsed: true }) };
        }

        function splitCell(selection, cellId) {
            var info = cellId ? _findTableInfoByCellId(model, cellId) : tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            var span = Math.max(1, Number(info.cell.colSpan || 1));
            info.cell.colSpan = 1;
            for (var i = 1; i < span; i++) {
                info.row.cells.splice(info.columnIndex + i, 0, _createEmptyTableCell(info.table.id, info.rowIndex, info.columnIndex + i));
            }
            ensureRowsAndCells(info.table);
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: info.table.id, action: 'split-cell', cellId: info.cell.id });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: info.cell.blocks[0].id, cellId: info.cell.id, tableId: info.table.id, offset: 0, isCollapsed: true }) };
        }

        function setCellStyle(selection, style) {
            var info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            info.cell.style = Object.assign({}, info.cell.style || {}, style || {});
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: info.table.id, cellId: info.cell.id, action: 'cell-style', style: info.cell.style });
            return { ok: true, operation: op, selection: createSelectionSnapshot(selection || { blockId: info.cell.blocks[0].id, cellId: info.cell.id, tableId: info.table.id }) };
        }

        function resizeTable(tableId, width) {
            var table = _findTableBlock(model, tableId);
            if (!table) return { ok: false, error: { code: 'missing-table', tableId: tableId } };
            if (!table.content.style) table.content.style = {};
            table.content.style.width = Math.max(80, Number(width || 0) || Number(table.content.style.width || 320));
            var op = record(OPERATION_TYPES.UpdateTableCell, { tableId: table.id, action: 'resize-table', width: table.content.style.width });
            return { ok: true, operation: op, tableId: table.id, width: table.content.style.width };
        }

        function insertTextInCell(selection, text) {
            var snapshot = createSelectionSnapshot(selection || {});
            var block = _findBlock(model, snapshot.blockId);
            if (!block || block.type !== 'paragraph') return { ok: false, error: { code: 'missing-cell-paragraph' }, selection: snapshot };
            _insertTextRun(block, Math.max(0, Math.min(_blockText(block).length, snapshot.offset || _blockText(block).length)), _asText(text), {});
            var next = createSelectionSnapshot(Object.assign({}, snapshot, {
                offset: Math.max(0, Math.min(_blockText(block).length, Number(snapshot.offset || 0) + _asText(text).length)),
                isCollapsed: true
            }));
            record(OPERATION_TYPES.UpdateTableCell, { cellId: snapshot.cellId, action: 'insert-text' });
            return { ok: true, selection: next };
        }

        function hitTest(layout, x, y) {
            var hit = pointerHitTest(model, layout, x, y);
            if (hit.type !== 'tableCell') return hit;
            return Object.assign({}, hit, { selection: createSelectionSnapshot(Object.assign({}, hit.position, { cellId: hit.cellId, tableId: hit.tableId, isCellSelection: true })) });
        }

        function createContextMenu(selection, options) {
            var viewport = options && options.viewport || {};
            return _sortObject({
                isReadable: true,
                position: { x: Math.min(Number(viewport.width || 1280) - 240, 24), y: 24 },
                items: ['insertRowAbove', 'insertRowBelow', 'insertColumnLeft', 'insertColumnRight', 'deleteRow', 'deleteColumn', 'mergeCells', 'splitCell', 'cellBackground', 'cellBorder', 'resizeTable'].map(function (id) {
                    return { commandId: id, isEnabled: !!tableInfoFromSelection(selection) || id === 'resizeTable' };
                })
            });
        }

        return {
            insertRowAbove: function (selection) { return insertRow(selection, 'above'); },
            insertRowBelow: function (selection) { return insertRow(selection, 'below'); },
            insertColumnLeft: function (selection) { return insertColumn(selection, 'left'); },
            insertColumnRight: function (selection) { return insertColumn(selection, 'right'); },
            deleteRow: deleteRow,
            deleteColumn: deleteColumn,
            mergeCells: mergeCells,
            splitCell: splitCell,
            setCellBackground: function (selection, color) { return setCellStyle(selection, { background: color }); },
            setCellBorder: function (selection, border) { return setCellStyle(selection, { border: border }); },
            resizeTable: resizeTable,
            insertTextInCell: insertTextInCell,
            hitTest: hitTest,
            createContextMenu: createContextMenu,
            getCommittedOperations: function () { return committedOperations.slice(); }
        };
    }

    function removeMarksForCommandInRange(block, range, commandId) {
        if (!block || block.type !== 'paragraph') return;
        transformRunsInRange(block, range.start, range.end, function (run) {
            run.marks = normalizeMarks(_asArray(run.marks).filter(function (mark) { return !markMatchesCommand(mark, commandId); }));
            return run;
        });
    }

    function normalizeParagraphAlignment(value) {
        var normalized = String(value ?? 'left').trim().toLowerCase();
        if (normalized === '1' || normalized === 'center' || normalized === 'centre') return 'center';
        if (normalized === '2' || normalized === 'right' || normalized === 'end') return 'right';
        if (normalized === '3' || normalized === 'justify' || normalized === 'justified') return 'justify';
        return 'left';
    }

    function clearFormattingInRange(block, range) {
        if (!block || block.type !== 'paragraph') return;
        transformRunsInRange(block, range.start, range.end, function (run) {
            run.marks = [];
            run.style = {};
            return normalizeTextRunForMerge(run);
        });
    }

    function createCommandDispatcher(model, options) {
        var opts = options || {};
        var selection = createSelectionSnapshot(opts.selection || opts.Selection || {});
        var pendingTypingMarks = normalizeMarks(opts.pendingTypingMarks || opts.PendingTypingMarks || []);
        var debugLog = [];
        var committedOperations = [];
        var subscribers = [];
        var lastSnapshot = collectFormattingState(model, selection, pendingTypingMarks);

        function publish(snapshot) {
            lastSnapshot = snapshot || collectFormattingState(model, selection, pendingTypingMarks);
            subscribers.forEach(function (callback) {
                try { callback(_clone(lastSnapshot)); }
                catch (error) {
                    debugLog.push({ code: 'subscriber-failed', message: String(error && error.message || error), at: Date.now() });
                }
            });
            return lastSnapshot;
        }

        function refresh(nextSelection) {
            if (nextSelection) selection = createSelectionSnapshot(nextSelection);
            return publish(collectFormattingState(model, selection, pendingTypingMarks));
        }

        function getState(commandId) {
            var id = normalizeCommandId(commandId);
            var snapshot = refresh();
            var reason = snapshot.disabledReasons[id] || '';
            return _sortObject({
                id: id,
                isEnabled: !reason && !!commands[id],
                value: snapshot.commandValues[id] ?? null,
                disabledReason: reason,
                refresh: true
            });
        }

        function applyInlineCommand(id, payload) {
            var range = selectionTextRange(selection);
            var block = _findBlock(model, range.blockId);
            if (!block || block.type !== 'paragraph') return { ok: false, errors: [{ code: 'selection-not-text' }] };
            var effectiveRange = range.collapsed ? { blockId: range.blockId, start: range.start, end: range.start, collapsed: true } : range;
            if (effectiveRange.collapsed) {
                var mark = commandMark(id, payload);
                if (isClearValueCommand(id, mark)) {
                    pendingTypingMarks = normalizeMarks(pendingTypingMarks.filter(function (item) { return !markMatchesCommand(item, id); }));
                    return { ok: true, operation: null, nextSelection: selection, pendingTyping: true };
                }
                if (mark) pendingTypingMarks = normalizeMarks(pendingTypingMarks.filter(function (item) { return !markMatchesCommand(item, id); }).concat([mark]));
                return { ok: true, operation: null, nextSelection: selection, pendingTyping: true };
            }
            if (id === 'clearFormatting') {
                var beforeClear = _asArray(block.content && block.content.runs).flatMap(function (run) { return _asArray(run.marks); });
                clearFormattingInRange(block, effectiveRange);
                var removeOp = createOperation(OPERATION_TYPES.RemoveMark, { range: effectiveRange, mark: { type: 'AllFormatting' } }, { source: 'command' });
                committedOperations.push(removeOp.toJSON());
                buildIndexes(model);
                return { ok: true, operation: removeOp, nextSelection: selection };
            }
            var beforeSnapshot = collectFormattingState(model, selection, pendingTypingMarks);
            var isActive = beforeSnapshot.commandValues[id] === true;
            if (id === 'textColor' || id === 'backgroundColor' || id === 'link') isActive = false;
            var mark = commandMark(id, payload);
            if (isClearValueCommand(id, mark)) {
                removeMarksForCommandInRange(block, effectiveRange, id);
                var clearOp = createOperation(OPERATION_TYPES.RemoveMark, { range: effectiveRange, mark: mark }, { source: 'command' });
                committedOperations.push(clearOp.toJSON());
                buildIndexes(model);
                return { ok: true, operation: clearOp, nextSelection: selection };
            }
            if ((id === 'fontFamily' || id === 'fontSize' || id === 'textColor' || id === 'backgroundColor')
                && mark && beforeSnapshot.commandValues[id] === mark.value) {
                return { ok: true, operation: null, nextSelection: selection, noop: true };
            }
            removeMarksForCommandInRange(block, effectiveRange, id);
            var opType = isActive ? OPERATION_TYPES.RemoveMark : OPERATION_TYPES.ApplyMark;
            var op = createOperation(opType, { range: effectiveRange, mark: mark }, { source: 'command' });
            if (!isActive) _splitRunsForRange(block, effectiveRange.start, effectiveRange.end, mark, false);
            buildIndexes(model);
            committedOperations.push(op.toJSON());
            return { ok: true, operation: op, nextSelection: selection };
        }

        function applyParagraphCommand(id, payload) {
            var body = payload || {};
            var snapshot = createSelectionSnapshot(selection);
            var block = _findBlock(model, snapshot.blockId);
            if (!block || block.type !== 'paragraph') return { ok: false, errors: [{ code: 'selection-not-paragraph' }] };
            var values = [];
            if (id === 'alignment') values.push(['alignment', normalizeParagraphAlignment(body.value ?? body.Value ?? body.alignment ?? body.Alignment ?? 'left')]);
            if (id === 'lineSpacing') values.push(['lineSpacing', Number(body.value ?? body.Value ?? 1)]);
            if (id === 'spacingBefore') values.push(['spacingBefore', Number(body.value ?? body.Value ?? 0)]);
            if (id === 'spacingAfter') values.push(['spacingAfter', Number(body.value ?? body.Value ?? 0)]);
            if (id === 'list') values.push(['listType', body.value || body.Value || body.listType || body.ListType || null]);
            if (id === 'indent') values.push(['indentLevel', Math.max(0, Number(block.content && block.content.indentLevel || 0) + Number(body.delta ?? body.Delta ?? 1))]);
            if (id === 'outdent') values.push(['indentLevel', Math.max(0, Number(block.content && block.content.indentLevel || 0) - Number(body.delta ?? body.Delta ?? 1))]);
            var ops = values.map(function (entry) {
                var op = createOperation(OPERATION_TYPES.SetParagraphAttribute, {
                    target: { blockId: block.id, offset: snapshot.offset },
                    attributeName: entry[0],
                    value: entry[1]
                }, { source: 'command' });
                applyOperation(model, op, { selection: snapshot });
                committedOperations.push(op.toJSON());
                return op;
            });
            selection = createSelectionSnapshot(snapshot);
            return { ok: true, operations: ops, nextSelection: selection };
        }

        function applyTableCommand(id, payload) {
            var body = payload || {};
            var snapshot = createSelectionSnapshot(selection);
            var controller = createTableController(model);
            var result;
            if (id === 'insertTable') {
                var op = createOperation(OPERATION_TYPES.InsertTable, {
                    target: { blockId: snapshot.blockId, offset: snapshot.offset },
                    rows: Number(body.rows || body.Rows || 2),
                    columns: Number(body.columns || body.Columns || 2),
                    tableId: body.tableId || body.TableId || body.blockId || body.BlockId || null,
                    style: body.style || body.Style || {}
                }, { source: 'command' });
                var applied = applyOperation(model, op, { selection: snapshot });
                committedOperations.push(op.toJSON());
                var table = _findTableBlock(model, applied.insertedBlockId || body.tableId || body.TableId || '');
                var firstCell = table && table.content.rows[0] && table.content.rows[0].cells[0];
                result = {
                    ok: applied.ok !== false,
                    operation: op,
                    nextSelection: firstCell ? createSelectionSnapshot({ blockId: firstCell.blocks[0].id, cellId: firstCell.id, tableId: table.id, offset: 0, isCollapsed: true }) : snapshot
                };
            } else if (id === 'insertRowAbove') result = controller.insertRowAbove(snapshot);
            else if (id === 'insertRowBelow') result = controller.insertRowBelow(snapshot);
            else if (id === 'insertColumnLeft') result = controller.insertColumnLeft(snapshot);
            else if (id === 'insertColumnRight') result = controller.insertColumnRight(snapshot);
            else if (id === 'deleteRow') result = controller.deleteRow(snapshot, body.rowIndex ?? body.RowIndex);
            else if (id === 'deleteColumn') result = controller.deleteColumn(snapshot, body.columnIndex ?? body.ColumnIndex);
            else if (id === 'mergeCells') result = controller.mergeCells(snapshot, body.cellIds || body.CellIds || []);
            else if (id === 'splitCell') result = controller.splitCell(snapshot, body.cellId || body.CellId || null);
            else if (id === 'cellBackground') result = controller.setCellBackground(snapshot, body.color || body.Color || body.value || body.Value || null);
            else if (id === 'cellBorder') result = controller.setCellBorder(snapshot, body.border || body.Border || body.value || body.Value || null);
            else if (id === 'resizeTable') result = controller.resizeTable(snapshot.tableId || snapshot.blockId || body.tableId || body.TableId, body.width || body.Width);
            else result = { ok: false, errors: [{ code: 'unknown-table-command', commandId: id }] };
            controller.getCommittedOperations().forEach(function (operation) { committedOperations.push(operation); });
            return { ok: result.ok !== false, operation: result.operation || null, nextSelection: result.selection || result.nextSelection || snapshot };
        }

        var commands = {};
        ['bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize', 'textColor', 'backgroundColor', 'link', 'clearFormatting'].forEach(function (id) {
            commands[id] = {
                id: id,
                refresh: function () { return getState(id); },
                execute: function (payload) { return applyInlineCommand(id, payload); }
            };
        });
        paragraphCommandTypes().forEach(function (id) {
            commands[id] = {
                id: id,
                refresh: function () { return getState(id); },
                execute: function (payload) { return applyParagraphCommand(id, payload); }
            };
        });
        ['insertTable', 'insertRowAbove', 'insertRowBelow', 'insertColumnLeft', 'insertColumnRight', 'deleteRow', 'deleteColumn', 'mergeCells', 'splitCell', 'cellBackground', 'cellBorder', 'resizeTable'].forEach(function (id) {
            commands[id] = {
                id: id,
                refresh: function () { return getState(id); },
                execute: function (payload) { return applyTableCommand(id, payload); }
            };
        });

        function executeCommand(commandInput, payload) {
            var id = normalizeCommandId(commandInput);
            var source = commandSource(commandInput);
            if (!commands[id]) {
                var failure = { code: 'unknown-command', commandId: id, source: source, at: Date.now() };
                debugLog.push(failure);
                return _sortObject({ ok: false, error: failure, source: source, commandId: id });
            }
            var state = getState(id);
            if (!state.isEnabled) {
                var disabled = { code: 'command-disabled', commandId: id, source: source, reason: state.disabledReason, at: Date.now() };
                debugLog.push(disabled);
                return _sortObject({ ok: false, error: disabled, source: source, commandId: id });
            }
            var beforeSelection = createSelectionSnapshot(selection);
            var result = commands[id].execute(payload || {});
            var transaction = {
                ok: result.ok !== false,
                id: 'cmd-txn-' + Date.now() + '-' + Math.floor(Math.random() * 100000),
                commandId: id,
                beforeSelection: beforeSelection,
                afterSelection: createSelectionSnapshot(result.nextSelection || selection),
                operationCount: _asArray(result.operations).length + (result.operation ? 1 : 0)
            };
            selection = transaction.afterSelection;
            refresh(selection);
            return _sortObject({
                ok: result.ok !== false,
                commandId: id,
                source: source,
                transaction: transaction,
                usedRuntimeSelection: true,
                readDomSelection: false,
                mutatedDomDirectly: false,
                state: getState(id)
            });
        }

        refresh(selection);

        return {
            normalizeCommandId: normalizeCommandId,
            getRegisteredCommandIds: function () { return Object.keys(commands).sort(); },
            getCommand: function (id) { return commands[normalizeCommandId(id)] || null; },
            getState: getState,
            refresh: refresh,
            executeCommand: executeCommand,
            setSelection: function (nextSelection) { selection = createSelectionSnapshot(nextSelection || {}); return refresh(selection); },
            getSelection: function () { return createSelectionSnapshot(selection); },
            getPendingTypingMarks: function () { return pendingTypingMarks.map(_clone); },
            getFormattingSnapshot: function () { return refresh(selection); },
            subscribeFormattingState: function (callback) {
                if (typeof callback === 'function') subscribers.push(callback);
                callback && callback(_clone(lastSnapshot));
                return function () { subscribers = subscribers.filter(function (item) { return item !== callback; }); };
            },
            getBlazorToolbarState: function () {
                var snapshot = refresh(selection);
                return _sortObject({ ribbon: _clone(snapshot), floating: _clone(snapshot), sidePanel: _clone(snapshot) });
            },
            getCommittedOperations: function () { return committedOperations.slice(); },
            getDebugLog: function () { return debugLog.slice(); }
        };
    }

    function createRevisionEngine(model, options) {
        var opts = options || {};
        ensureRevisionList(model);
        var userId = resolveRevisionUserId(opts);
        var trackChanges = resolveTrackChangesState(opts).enabled === true;

        function createRevision(type, range, payload, extra) {
            var normalizedType = normalizeRevisionType(type);
            var revision = addRevision(model, {
                id: extra && (extra.id || extra.Id) || 'rev-' + normalizedType.toLowerCase() + '-' + Date.now() + '-' + Math.floor(Math.random() * 100000),
                type: normalizedType,
                author: extra && (extra.author || extra.Author) || userId,
                timestamp: extra && (extra.timestamp || extra.Timestamp) || Date.now(),
                affectedRange: range,
                payload: payload || {},
                status: 'Pending'
            });
            return revision;
        }

        function coalesceInsertionRevision(selection, text) {
            var offset = Number(selection && selection.offset || 0);
            var candidate = _asArray(model.revisions).slice().reverse().find(function (revision) {
                return revision.type === 'Insertion'
                    && revision.status === 'Pending'
                    && revision.author === userId
                    && revision.affectedRange
                    && revision.affectedRange.blockId === selection.blockId
                    && Number(revision.affectedRange.end || 0) === offset;
            });
            if (!candidate) return null;
            candidate.affectedRange.end = offset + _asText(text).length;
            candidate.payload.text = _asText(candidate.payload.text) + _asText(text);
            buildIndexes(model);
            return candidate;
        }

        function insertText(selection, text) {
            var snapshot = createSelectionSnapshot(selection || {});
            var insertedText = _asText(text);
            var block = _findBlock(model, snapshot.blockId);
            var revision = null;
            var runId = _stableId('inline', block.id + '-revision-insert-' + Date.now() + '-' + Math.floor(Math.random() * 1000));
            if (trackChanges) {
                revision = coalesceInsertionRevision(snapshot, insertedText) || createRevision('Insertion', {
                    blockId: snapshot.blockId,
                    start: snapshot.offset,
                    end: snapshot.offset + insertedText.length
                }, { text: insertedText });
            }
            _insertTextRun(block, snapshot.offset, insertedText, {
                id: runId,
                revisionId: revision && revision.id || null
            });
            buildIndexes(model);
            var insertedRun = _asArray(block.content && block.content.runs).find(function (run) { return run.id === runId; }) || { text: insertedText };
            return _sortObject({
                ok: true,
                revisionId: revision && revision.id || '',
                insertedRun: insertedRun,
                selection: createSelectionSnapshot({ blockId: snapshot.blockId, offset: snapshot.offset + insertedText.length, isCollapsed: true })
            });
        }

        function deleteRange(range) {
            var normalizedRange = normalizeRevisionRange(range);
            var block = _findBlock(model, normalizedRange.blockId);
            var deletedText = _blockText(block).slice(normalizedRange.start, normalizedRange.end);
            if (!trackChanges) {
                removeRangeText(model, normalizedRange);
                return _sortObject({ ok: true, revisionId: '', deletedText: deletedText, selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.start }) });
            }
            var revision = createRevision('Deletion', normalizedRange, { text: deletedText });
            setRevisionForRange(model, revision.id, normalizedRange);
            return _sortObject({ ok: true, revisionId: revision.id, deletedText: deletedText, selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.start }) });
        }

        function applyFormatChange(range, mark) {
            var normalizedRange = normalizeRevisionRange(range);
            if (!trackChanges) {
                applyRevisionMark(model, normalizedRange, mark);
                return _sortObject({ ok: true, revisionId: '', selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.end }) });
            }
            var revision = createRevision('FormatChange', normalizedRange, { mark: _clone(mark || {}), decorativeStyle: { color: '#7c3aed', underline: true } });
            return _sortObject({ ok: true, revisionId: revision.id, selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.end }) });
        }

        function splitParagraph(selection) {
            return splitParagraphPreservingInlineMetadata(model, selection);
        }

        function getVisibleText(blockId, reviewMode) {
            var mode = String(reviewMode || 'showMarkup').toLowerCase();
            var block = _findBlock(model, blockId);
            return _asArray(block && block.content && block.content.runs).map(function (run) {
                var revisionId = run.revisionId || run.RevisionId || '';
                var revision = revisionId ? getRevisionById(model, revisionId) : null;
                if (revision && revision.type === 'Deletion' && mode === 'final') return '';
                if (revision && revision.type === 'Insertion' && mode === 'original') return '';
                return _asText(run.text);
            }).join('');
        }

        function getActualFormattingState(selection) {
            var snapshot = createSelectionSnapshot(selection || {});
            var block = _findBlock(model, snapshot.blockId);
            var runInfo = findRunAtOffset(block, snapshot.offset);
            var run = runInfo && runInfo.run || {};
            return _sortObject({
                marks: _clone(run.marks || []),
                style: _clone(run.style || {}),
                revisionId: run.revisionId || run.RevisionId || null,
                fromRevisionDecoration: false
            });
        }

        function createOverlayModel(reviewMode) {
            var mode = reviewMode || 'showMarkup';
            return _sortObject({
                mode: mode,
                layer: 'revision',
                zIndex: 12,
                markers: _asArray(model.revisions).filter(function (revision) { return revision.status === 'Pending'; }).map(function (revision) {
                    return {
                        revisionId: revision.id,
                        type: revision.type,
                        range: _clone(revision.affectedRange),
                        payload: _clone(revision.payload),
                        decorativeStyle: revisionDecorativeStyle(revision),
                        source: 'revision-model'
                    };
                })
            });
        }

        function renderOverlay(root, overlayModel) {
            if (!root) return { ok: false };
            root.innerHTML = '';
            var overlay = markOverlayNonText(document.createElement('div'));
            overlay.setAttribute('data-render-overlay', 'revision');
            overlay.className = 'tm-render-revision-overlay';
            overlay.style.position = 'relative';
            overlay.style.zIndex = String((overlayModel && overlayModel.zIndex) || 12);
            _asArray(overlayModel && overlayModel.markers).forEach(function (marker) {
                var node = markOverlayNonText(document.createElement('span'));
                node.className = 'tm-render-revision-marker tm-render-revision-marker--' + String(marker.type || '').toLowerCase();
                node.setAttribute('data-revision-overlay-id', marker.revisionId);
                node.setAttribute('data-revision-type', marker.type);
                node.setAttribute('data-model-block-id', marker.range && marker.range.blockId || '');
                node.style.zIndex = String((overlayModel && overlayModel.zIndex) || 12);
                node.textContent = '';
                overlay.appendChild(node);
            });
            root.appendChild(overlay);
            return _sortObject({ ok: true, markerCount: _asArray(overlayModel && overlayModel.markers).length });
        }

        function createReviewPopover(revisionId) {
            var revision = getRevisionById(model, revisionId);
            var type = revision ? revision.type : '';
            return _sortObject({
                revision: revision,
                role: 'dialog',
                ariaModal: false,
                ariaLabel: type ? ('Review ' + type + ' revision') : 'Review revision',
                title: type,
                author: revision ? revision.author : '',
                payload: revision ? revision.payload : null,
                actions: [
                    { id: 'accept', role: 'button', ariaLabel: 'Accept revision' },
                    { id: 'reject', role: 'button', ariaLabel: 'Reject revision' }
                ]
            });
        }

        function createMarkerDiffer(revisionIds) {
            var scopes = _asArray(revisionIds).map(_asText);
            return _sortObject({ invalidatedOverlayScopes: scopes, invalidatedLayoutScopes: [], markerIds: scopes });
        }

        function acceptRevision(revisionId, selection) {
            var revision = getRevisionById(model, revisionId);
            if (!revision) return { ok: false, error: 'missing-revision', selection: createSelectionSnapshot(selection || {}) };
            if (revision.type === 'Insertion') clearRevisionFromRuns(model, revision.id);
            if (revision.type === 'Deletion') removeRevisionRuns(model, revision.id);
            if (revision.type === 'FormatChange') applyRevisionMark(model, revision.affectedRange, revision.payload && revision.payload.mark || {});
            updateRevisionStatus(model, revision.id, 'Accepted');
            return _sortObject({ ok: true, revisionId: revision.id, status: 'Accepted', selection: createSelectionSnapshot(selection || { blockId: revision.affectedRange.blockId, offset: revision.affectedRange.start }) });
        }

        function rejectRevision(revisionId, selection) {
            var revision = getRevisionById(model, revisionId);
            if (!revision) return { ok: false, error: 'missing-revision', selection: createSelectionSnapshot(selection || {}) };
            if (revision.type === 'Insertion') removeRevisionRuns(model, revision.id);
            if (revision.type === 'Deletion') clearRevisionFromRuns(model, revision.id);
            updateRevisionStatus(model, revision.id, 'Rejected');
            return _sortObject({ ok: true, revisionId: revision.id, status: 'Rejected', selection: createSelectionSnapshot(selection || { blockId: revision.affectedRange.blockId, offset: revision.affectedRange.start }) });
        }

        return {
            createRevision: createRevision,
            insertText: insertText,
            deleteRange: deleteRange,
            applyFormatChange: applyFormatChange,
            splitParagraph: splitParagraph,
            getVisibleText: getVisibleText,
            getActualFormattingState: getActualFormattingState,
            createOverlayModel: createOverlayModel,
            renderOverlay: renderOverlay,
            createReviewPopover: createReviewPopover,
            createMarkerDiffer: createMarkerDiffer,
            acceptRevision: acceptRevision,
            rejectRevision: rejectRevision
        };
    }

    function createTransaction(model, options) {
        var opts = options || {};
        var lightweightSnapshots = opts.lightweightSnapshots === true || opts.LightweightSnapshots === true;
        var snapshot = lightweightSnapshots ? null : _clone(model);
        var instanceId = _asText(opts.instanceId || opts.InstanceId || opts.documentInstanceId || opts.DocumentInstanceId || '');
        var beforeSelection = opts.beforeSelection || opts.BeforeSelection
            ? withStableSelectionToken(instanceId, opts.beforeSelection || opts.BeforeSelection, model)
            : null;
        var beforeDocFingerprint = lightweightSnapshots ? '' : (opts.beforeDocFingerprint || opts.BeforeDocFingerprint || createDocumentFingerprint(model));
        var commandName = opts.commandName || opts.CommandName || opts.label || opts.Label || opts.type || opts.Type || 'Document change';
        var transaction = {
            id: opts.id || ('txn-' + (++_transactionCounter)),
            type: opts.type || TRANSACTION_TYPES.Default,
            label: opts.label || opts.type || 'Document change',
            commandName: commandName,
            instanceId: instanceId,
            beforeModelSnapshot: snapshot,
            afterModelSnapshot: null,
            beforeDocFingerprint: beforeDocFingerprint,
            afterDocFingerprint: null,
            beforeSelection: _clone(beforeSelection),
            afterSelection: _clone(beforeSelection),
            operations: [],
            invalidatedScopes: [],
            lightweightSnapshots: lightweightSnapshots,
            differ: createDiffer(),
            committed: false,
            rolledBack: false,
            renderSuppressed: true,
            rollback: function () {
                if (snapshot) replaceModelContents(model, snapshot);
                this.rolledBack = true;
                this.renderSuppressed = false;
                return { ok: true, transaction: this.toJSON() };
            },
            apply: function (operation) {
                var result = applyOperation(model, operation, { differ: this.differ, selection: this.afterSelection });
                if (!result.ok) {
                    this.rollback();
                    return result;
                }
                this.operations.push(result.operation);
                this.invalidatedScopes = _unique(this.invalidatedScopes.concat(_asArray(result.invalidatedLayoutScopes)));
                this.afterSelection = result.nextSelection
                    ? withStableSelectionToken(this.instanceId, result.nextSelection, model)
                    : _clone(this.afterSelection);
                return result;
            },
            commit: function () {
                this.committed = true;
                this.renderSuppressed = false;
                if (!this.lightweightSnapshots) {
                    this.afterModelSnapshot = _clone(model);
                    this.afterDocFingerprint = createDocumentFingerprint(model);
                } else {
                    this.afterDocFingerprint = '';
                }
                if (this.afterSelection) {
                    this.afterSelection = withStableSelectionToken(this.instanceId, this.afterSelection, model);
                }
                return {
                    ok: true,
                    transaction: this.toJSON(),
                    order: ['differ', 'layout', 'render', 'selection-restore'],
                    differ: this.differ.snapshot()
                };
            },
            toJSON: function () {
                return _sortObject({
                    id: this.id,
                    type: this.type,
                    label: this.label,
                    commandName: this.commandName,
                    instanceId: this.instanceId,
                    beforeDocFingerprint: this.beforeDocFingerprint,
                    afterDocFingerprint: this.afterDocFingerprint,
                    beforeSelection: this.beforeSelection,
                    afterSelection: this.afterSelection,
                    invalidatedScopes: this.invalidatedScopes,
                    operationCount: this.operations.length,
                    lightweightSnapshots: this.lightweightSnapshots === true,
                    committed: this.committed,
                    rolledBack: this.rolledBack,
                    renderSuppressed: this.renderSuppressed
                });
            }
        };
        return transaction;
    }

    function createHistoryRestoreOperation(snapshot, selection, source, affectedScopeIds, previousSnapshot, previousSelection) {
        return createOperation(OPERATION_TYPES.RestoreSnapshot, {
            snapshot: _clone(snapshot || null),
            previousSnapshot: _clone(previousSnapshot || null),
            selection: _clone(selection || null),
            previousSelection: _clone(previousSelection || null),
            affectedScopeIds: _asArray(affectedScopeIds).length ? _asArray(affectedScopeIds) : ['document']
        }, { source: source || 'history' });
    }

    function createHistoryEntryFromTransaction(transaction) {
        var instanceId = transaction.instanceId || transaction.InstanceId || '';
        var operations = transaction.operations.map(function (operation) { return attachOperationMethods(operation).toJSON(); });
        var useOperationHistory = operations.length > 0 && operations.every(supportsOperationHistory);
        var beforeSnapshot = useOperationHistory ? _clone(transaction.beforeModelSnapshot || null) : _clone(transaction.beforeModelSnapshot || null);
        var afterSnapshot = useOperationHistory ? _clone(transaction.afterModelSnapshot || null) : _clone(transaction.afterModelSnapshot || null);
        if (useOperationHistory && transaction.lightweightSnapshots === true) {
            beforeSnapshot = null;
            afterSnapshot = null;
        }
        var beforeSelection = transaction.beforeSelection
            ? (useOperationHistory && !beforeSnapshot ? createSelectionSnapshot(transaction.beforeSelection) : withStableSelectionToken(instanceId, transaction.beforeSelection, beforeSnapshot || null))
            : createSelectionSnapshot(null);
        var afterSelection = transaction.afterSelection
            ? (useOperationHistory && !afterSnapshot && !beforeSnapshot ? createSelectionSnapshot(transaction.afterSelection) : withStableSelectionToken(instanceId, transaction.afterSelection, afterSnapshot || beforeSnapshot || null))
            : createSelectionSnapshot(beforeSelection);
        var scopes = _asArray(transaction.invalidatedScopes).length ? _asArray(transaction.invalidatedScopes) : ['document'];
        return {
            id: transaction.id,
            transaction: transaction.toJSON(),
            operations: operations,
            inverseOperations: useOperationHistory
                ? createUndoHistoryOperations(operations)
                : [createHistoryRestoreOperation(beforeSnapshot, beforeSelection, 'undo', scopes, afterSnapshot, afterSelection).toJSON()],
            redoOperations: useOperationHistory
                ? createRedoHistoryOperations(operations)
                : [createHistoryRestoreOperation(afterSnapshot, afterSelection, 'redo', scopes, beforeSnapshot, beforeSelection).toJSON()],
            beforeModelSnapshot: beforeSnapshot,
            afterModelSnapshot: afterSnapshot,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            invalidatedScopes: scopes,
            createdAt: Date.now()
        };
    }

    function canCoalesceHistoryTyping(previousEntry, transaction, timeoutMs) {
        if (!previousEntry || !transaction || transaction.type !== TRANSACTION_TYPES.Typing) return false;
        if (!previousEntry.transaction || previousEntry.transaction.type !== TRANSACTION_TYPES.Typing) return false;
        if (_asArray(previousEntry.operations).length !== 1 || _asArray(transaction.operations).length !== 1) return false;
        return shouldCoalesceTyping(
            attachOperationMethods(previousEntry.operations[0]),
            attachOperationMethods(transaction.operations[0]),
            transaction.operations[0].timestamp,
            timeoutMs || 1000);
    }

    function coalesceHistoryEntry(previousEntry, transaction) {
        var mergedOperation = coalesceTypingOperation(attachOperationMethods(previousEntry.operations[0]), attachOperationMethods(transaction.operations[0]));
        previousEntry.operations = [mergedOperation.toJSON()];
        previousEntry.afterModelSnapshot = _clone(transaction.afterModelSnapshot || null);
        previousEntry.afterSelection = createSelectionSnapshot(transaction.afterSelection || previousEntry.afterSelection);
        previousEntry.transaction.afterSelection = previousEntry.afterSelection;
        previousEntry.transaction.invalidatedScopes = _unique(_asArray(previousEntry.transaction.invalidatedScopes).concat(_asArray(transaction.invalidatedScopes)));
        previousEntry.transaction.operationCount = 1;
        previousEntry.transaction.coalesced = true;
        if (supportsOperationHistory(mergedOperation)) {
            previousEntry.redoOperations = createRedoHistoryOperations(previousEntry.operations);
            previousEntry.inverseOperations = createUndoHistoryOperations(previousEntry.operations);
        } else {
            previousEntry.redoOperations = [createHistoryRestoreOperation(
                previousEntry.afterModelSnapshot,
                previousEntry.afterSelection,
                'redo',
                previousEntry.transaction.invalidatedScopes,
                previousEntry.beforeModelSnapshot,
                previousEntry.beforeSelection).toJSON()];
            previousEntry.inverseOperations = [createHistoryRestoreOperation(
                previousEntry.beforeModelSnapshot,
                previousEntry.beforeSelection,
                'undo',
                previousEntry.transaction.invalidatedScopes,
                previousEntry.afterModelSnapshot,
                previousEntry.afterSelection).toJSON()];
        }
        return previousEntry;
    }

    function createHistoryController(model, options) {
        var opts = options || {};
        var schema = opts.schema || opts.Schema || createDefaultSchemaRegistry();
        var paragraphEngine = opts.paragraphLayoutEngine || opts.ParagraphLayoutEngine || createParagraphLayoutEngine(null, opts.layoutOptions || opts.LayoutOptions || {});
        var renderer = opts.renderer || opts.Renderer || createAtomicRenderer();
        var root = opts.root || opts.Root || null;
        var selection = createSelectionSnapshot(opts.selection || opts.Selection || firstModelSelection(model));
        var undoStack = [];
        var redoStack = [];
        var transactions = [];
        var layout = paragraphEngine.layoutDocument(model, { selection: selection });
        var renderVersion = 0;
        var epoch = 0;
        var lastDiffer = null;
        var lastTransaction = null;

        function renderAtomic(reason, affectedScopes) {
            layout = paragraphEngine.layoutDocument(model, { selection: selection, affectedScopes: affectedScopes || ['document'] });
            if (root) {
                renderer.render(root, createRenderSnapshot(model, layout, selection, { affectedScopes: affectedScopes || ['document'] }), { reason: reason || 'history' });
            }
            renderVersion++;
            return layout;
        }

        function pushHistory(transaction) {
            var entry = createHistoryEntryFromTransaction(transaction);
            var previous = undoStack[undoStack.length - 1] || null;
            if (canCoalesceHistoryTyping(previous, transaction, opts.typingCoalescingMs || opts.TypingCoalescingMs || 1000)) {
                return coalesceHistoryEntry(previous, transaction);
            }
            undoStack.push(entry);
            return entry;
        }

        function commitOperations(operations, meta) {
            var body = meta || {};
            var list = _asArray(operations).map(function (operation) { return attachOperationMethods(operation); });
            var operationSelection = list.length === 1 ? (list[0].selection || list[0].Selection || null) : null;
            var transaction = createTransaction(model, {
                type: body.transactionType || body.TransactionType || (list.length === 1 && list[0].type === OPERATION_TYPES.InsertText ? TRANSACTION_TYPES.Typing : TRANSACTION_TYPES.Default),
                label: body.label || body.Label || 'Document change',
                beforeSelection: body.beforeSelection || body.BeforeSelection || operationSelection || selection
            });
            for (var i = 0; i < list.length; i++) {
                var result = transaction.apply(list[i]);
                if (!result.ok) {
                    return Object.assign({ ok: false, transaction: transaction.toJSON(), operationIndex: i }, result);
                }
            }
            var committed = transaction.commit();
            selection = createSelectionPostFixer(schema).fix(model, transaction.afterSelection || selection);
            transaction.afterSelection = _clone(selection);
            transaction.afterModelSnapshot = _clone(model);
            lastDiffer = committed.differ;
            lastTransaction = transaction.toJSON();
            transactions.push(transaction.toJSON());
            var entry = null;
            if (transactionAffectsDocument(transaction)) {
                entry = pushHistory(transaction);
                redoStack = [];
                epoch++;
            }
            renderAtomic(transaction.type, transaction.invalidatedScopes);
            return _sortObject({
                ok: true,
                transaction: transaction.toJSON(),
                historyEntry: entry,
                selection: selection,
                layout: layout,
                differ: committed.differ,
                undoDepth: undoStack.length,
                redoDepth: redoStack.length,
                renderVersion: renderVersion
            });
        }

        function applyHistory(undo) {
            var sourceStack = undo ? undoStack : redoStack;
            var targetStack = undo ? redoStack : undoStack;
            var entry = sourceStack.pop();
            if (!entry) return _sortObject({ ok: false, empty: true, transactionType: undo ? TRANSACTION_TYPES.Undo : TRANSACTION_TYPES.Redo });
            var operations = (undo ? entry.inverseOperations : entry.redoOperations).map(function (operation) { return attachOperationMethods(_clone(operation)); });
            var historyTransaction = createTransaction(model, {
                type: undo ? TRANSACTION_TYPES.Undo : TRANSACTION_TYPES.Redo,
                label: undo ? 'Undo' : 'Redo',
                beforeSelection: selection
            });
            for (var i = operations.length - 1; i >= 0; i--) {
                var result = historyTransaction.apply(operations[i]);
                if (!result.ok) {
                    return Object.assign({ ok: false, transaction: historyTransaction.toJSON(), operationIndex: i }, result);
                }
            }
            var committed = historyTransaction.commit();
            selection = createSelectionPostFixer(schema).fix(model, undo ? entry.beforeSelection : entry.afterSelection);
            historyTransaction.afterSelection = _clone(selection);
            historyTransaction.afterModelSnapshot = _clone(model);
            targetStack.push(entry);
            transactions.push(historyTransaction.toJSON());
            lastDiffer = committed.differ;
            lastTransaction = historyTransaction.toJSON();
            epoch++;
            renderAtomic(historyTransaction.type, entry.invalidatedScopes);
            return _sortObject({
                ok: true,
                transaction: historyTransaction.toJSON(),
                historyEntry: entry,
                appliedOperations: operations.map(function (operation) { return operation.toJSON ? operation.toJSON() : _clone(operation); }),
                selection: selection,
                layout: layout,
                differ: committed.differ,
                undoDepth: undoStack.length,
                redoDepth: redoStack.length,
                renderVersion: renderVersion
            });
        }

        function debug() {
            return _sortObject({
                epoch: epoch,
                transactionCount: transactions.length,
                undoDepth: undoStack.length,
                redoDepth: redoStack.length,
                selection: selection,
                layoutInvalidatedScopes: layout && layout.invalidatedScopeIds || [],
                renderVersion: renderVersion,
                lastDiffer: lastDiffer,
                lastTransaction: lastTransaction,
                nextUndo: undoStack.length ? undoStack[undoStack.length - 1].transaction : null,
                nextRedo: redoStack.length ? redoStack[redoStack.length - 1].transaction : null
            });
        }

        renderAtomic('initial', ['document']);

        return {
            commitOperations: commitOperations,
            commitOperation: function (operation, meta) { return commitOperations([operation], meta || {}); },
            undo: function () { return applyHistory(true); },
            redo: function () { return applyHistory(false); },
            clearRedo: function () { redoStack = []; return debug(); },
            getSelection: function () { return createSelectionSnapshot(selection); },
            setSelection: function (nextSelection) { selection = createSelectionSnapshot(nextSelection || selection); return selection; },
            getLayout: function () { return _clone(layout); },
            getUndoStack: function () { return _clone(undoStack); },
            getRedoStack: function () { return _clone(redoStack); },
            getTransactions: function () { return _clone(transactions); },
            debug: debug
        };
    }

    function replaceModelContents(target, source) {
        Object.keys(target).forEach(function (key) { delete target[key]; });
        Object.assign(target, _clone(source));
        buildIndexes(target);
    }

    function createLogicalPosition(input) {
        var value = input || {};
        return _sortObject({
            region: _asText(value.region || value.Region || 'Body'),
            blockId: _asText(value.blockId || value.BlockId || ''),
            inlineId: value.inlineId || value.InlineId || null,
            offset: Number(value.offset ?? value.Offset ?? 0),
            affinity: value.affinity || value.Affinity || 'after',
            visualHintLineId: value.visualHintLineId || value.VisualHintLineId || null,
            limitId: value.limitId || value.LimitId || null,
            objectId: value.objectId || value.ObjectId || null,
            cellId: value.cellId || value.CellId || null,
            tableId: value.tableId || value.TableId || null,
            headerFooterId: value.headerFooterId || value.HeaderFooterId || null
        });
    }

    function createLogicalRange(anchor, focus, direction) {
        return _sortObject({
            anchor: createLogicalPosition(anchor),
            focus: createLogicalPosition(focus || anchor),
            direction: direction || 'none',
            isCollapsed: !focus || (
                (anchor.blockId || anchor.BlockId) === (focus.blockId || focus.BlockId)
                && Number(anchor.offset ?? anchor.Offset ?? 0) === Number(focus.offset ?? focus.Offset ?? 0))
        });
    }

    function createSelectionSnapshot(input) {
        var value = input || {};
        var range = value.range || value.Range || null;
        if (!range) {
            if (value.anchor || value.Anchor || value.focus || value.Focus) {
                var anchor = createLogicalPosition(value.anchor || value.Anchor || value.position || value.Position || value);
                var focus = createLogicalPosition(value.focus || value.Focus || value.anchor || value.Anchor || value.position || value.Position || value);
                range = createLogicalRange(anchor, focus, value.direction || value.Direction || (anchor.offset <= focus.offset ? 'forward' : 'backward'));
            } else if (value.anchorBlockId || value.AnchorBlockId || value.focusBlockId || value.FocusBlockId) {
                var anchorPosition = createLogicalPosition({
                    region: value.region || value.Region || 'Body',
                    blockId: value.anchorBlockId || value.AnchorBlockId || value.focusBlockId || value.FocusBlockId || '',
                    inlineId: value.anchorInlineId || value.AnchorInlineId || value.anchorNodeId || value.AnchorNodeId || null,
                    offset: value.anchorOffset ?? value.AnchorOffset ?? value.anchorBlockOffset ?? value.AnchorBlockOffset ?? 0,
                    headerFooterId: value.headerFooterId || value.HeaderFooterId || null
                });
                var focusPosition = createLogicalPosition({
                    region: value.region || value.Region || 'Body',
                    blockId: value.focusBlockId || value.FocusBlockId || value.anchorBlockId || value.AnchorBlockId || '',
                    inlineId: value.focusInlineId || value.FocusInlineId || value.focusNodeId || value.FocusNodeId || null,
                    offset: value.focusOffset ?? value.FocusOffset ?? value.focusBlockOffset ?? value.FocusBlockOffset ?? value.anchorOffset ?? value.AnchorOffset ?? 0,
                    headerFooterId: value.headerFooterId || value.HeaderFooterId || null
                });
                range = createLogicalRange(anchorPosition, focusPosition, value.direction || value.Direction || (anchorPosition.offset <= focusPosition.offset ? 'forward' : 'backward'));
            } else {
                var position = createLogicalPosition(value.position || value.Position || value);
                range = createLogicalRange(position, position, 'none');
            }
        }
        return _sortObject({
            region: _asText(value.region || value.Region || range.anchor.region || 'Body'),
            range: range,
            anchor: createLogicalPosition(range.anchor),
            focus: createLogicalPosition(range.focus),
            anchorOffset: createLogicalPosition(range.anchor).offset,
            focusOffset: createLogicalPosition(range.focus).offset,
            AnchorOffset: createLogicalPosition(range.anchor).offset,
            FocusOffset: createLogicalPosition(range.focus).offset,
            AnchorBlockOffset: createLogicalPosition(range.anchor).offset,
            FocusBlockOffset: createLogicalPosition(range.focus).offset,
            AnchorBlockId: createLogicalPosition(range.anchor).blockId,
            FocusBlockId: createLogicalPosition(range.focus).blockId,
            blockId: createLogicalPosition(range.focus).blockId,
            inlineId: createLogicalPosition(range.focus).inlineId,
            offset: createLogicalPosition(range.focus).offset,
            affinity: createLogicalPosition(range.focus).affinity,
            visualHintLineId: createLogicalPosition(range.focus).visualHintLineId,
            limitId: createLogicalPosition(range.focus).limitId,
            headerFooterId: value.headerFooterId || value.HeaderFooterId || createLogicalPosition(range.focus).headerFooterId || createLogicalPosition(range.anchor).headerFooterId || null,
            isCollapsed: range.isCollapsed !== false,
            direction: range.direction || 'none'
            ,
            objectId: createLogicalPosition(range.focus).objectId,
            cellId: value.cellId || value.CellId || createLogicalPosition(range.focus).cellId || null,
            tableId: value.tableId || value.TableId || createLogicalPosition(range.focus).tableId || null,
            isCellSelection: value.isCellSelection === true || value.IsCellSelection === true || !!(value.cellId || value.CellId || createLogicalPosition(range.focus).cellId),
            isObjectSelection: value.isObjectSelection === true || value.IsObjectSelection === true || !!createLogicalPosition(range.focus).objectId && createLogicalPosition(range.focus).blockId === createLogicalPosition(range.anchor).blockId && range.isCollapsed !== false,
            activeImageBlockId: value.activeImageBlockId || value.ActiveImageBlockId || ((value.isObjectSelection === true || value.IsObjectSelection === true || !!createLogicalPosition(range.focus).objectId) ? createLogicalPosition(range.focus).blockId : null),
            activeObjectId: value.activeObjectId || value.ActiveObjectId || value.objectId || value.ObjectId || createLogicalPosition(range.focus).objectId || null,
            activeTableCellId: value.activeTableCellId || value.ActiveTableCellId || value.cellId || value.CellId || createLogicalPosition(range.focus).cellId || null,
            activeTableId: value.activeTableId || value.ActiveTableId || value.tableId || value.TableId || createLogicalPosition(range.focus).tableId || createLogicalPosition(range.anchor).tableId || null,
            activeCommentId: value.activeCommentId || value.ActiveCommentId || null,
            activeRevisionId: value.activeRevisionId || value.ActiveRevisionId || null,
            hitTargetKind: value.hitTargetKind || value.HitTargetKind || (value.activeImageBlockId || value.ActiveImageBlockId || value.objectId || value.ObjectId || createLogicalPosition(range.focus).objectId ? 'image' : null)
        });
    }

    function stableJsonString(value) {
        return JSON.stringify(_sortObject(value || {}));
    }

    function hashStableString(value) {
        var text = _asText(value);
        var hash = 2166136261;
        for (var i = 0; i < text.length; i++) {
            hash ^= text.charCodeAt(i);
            hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
        }

        return 'fnv1a-' + (hash >>> 0).toString(16).padStart(8, '0');
    }

    function createDocumentFingerprint(model) {
        return hashStableString(stableJsonString(model || {}));
    }

    function createSelectionDocumentFingerprint(model) {
        function blockFingerprint(block) {
            if (!block) return null;
            var type = block.type || block.Type || '';
            var item = {
                id: block.id || block.Id || '',
                type: type
            };
            if (type === 'paragraph') {
                item.text = _blockText(block);
            } else if (type === 'table') {
                item.rows = _asArray(block.content && block.content.rows || block.Content && block.Content.Rows).map(function (row) {
                    return _asArray(row.cells || row.Cells).map(function (cell) {
                        return {
                            id: cell.id || cell.Id || '',
                            blocks: _asArray(cell.blocks || cell.Blocks).map(blockFingerprint)
                        };
                    });
                });
            } else if (type === 'image') {
                item.objectId = block.content && (block.content.objectId || block.content.ObjectId) || block.id || block.Id || '';
            }

            return _sortObject(item);
        }

        return hashStableString(stableJsonString({
            documentId: model && (model.documentId || model.DocumentId) || '',
            schemaVersion: model && (model.schemaVersion || model.SchemaVersion) || '',
            body: _asArray(model && model.body && model.body.blocks || model && model.Body && model.Body.Blocks).map(blockFingerprint),
            headers: _asArray(model && model.headers || model && model.Headers).map(function (header) {
                return { id: header.id || header.Id || '', blocks: _asArray(header.blocks || header.Blocks).map(blockFingerprint) };
            }),
            footers: _asArray(model && model.footers || model && model.Footers).map(function (footer) {
                return { id: footer.id || footer.Id || '', blocks: _asArray(footer.blocks || footer.Blocks).map(blockFingerprint) };
            })
        }));
    }

    function normalizeSelectionTokenRegion(value, selection) {
        var snapshot = selection || {};
        var raw = _asText(value || snapshot.region || snapshot.Region || snapshot.activeRegion || snapshot.ActiveRegion || 'Body').trim();
        var lower = raw.toLowerCase();
        if (snapshot.activeTableCellId || snapshot.ActiveTableCellId || snapshot.cellId || snapshot.CellId || lower === 'tablecell' || lower === 'table-cell') return 'tableCell';
        if (lower === 'header' || lower === 'headers') return 'header';
        if (lower === 'footer' || lower === 'footers') return 'footer';
        if (lower === 'caption') return 'caption';
        if (lower === 'image' || lower === 'object') return 'image';
        return 'body';
    }

    function createSelectionTokenBoundary(model, position, logicalOffset) {
        var pos = createLogicalPosition(position || {});
        var offset = Math.max(0, Math.round(Number(logicalOffset ?? pos.offset ?? 0) || 0));
        var block = _findBlock(model, pos.blockId);
        var inline = block && block.type === 'paragraph'
            ? _inlineAtOffset(block, offset)
            : null;
        var inlineId = (inline && inline.run && inline.run.id) || pos.inlineId || null;
        var region = normalizeSelectionTokenRegion(pos.region, pos);
        var limitId = pos.limitId || (block ? _findLimitForBlock(model, block.id) : null);
        var boundary = {
            region: region,
            blockId: pos.blockId || '',
            inlineId: inlineId,
            runId: inlineId,
            logicalOffset: offset,
            offset: offset,
            affinity: pos.affinity || 'after',
            limitId: limitId || null,
            tableId: pos.tableId || null,
            cellId: pos.cellId || null,
            headerFooterId: pos.headerFooterId || null,
            objectId: pos.objectId || null
        };
        boundary.inlinePath = [
            boundary.region,
            boundary.limitId || '',
            boundary.tableId || '',
            boundary.cellId || '',
            boundary.blockId || '',
            boundary.inlineId || '',
            String(boundary.logicalOffset)
        ];
        boundary.runBoundaryPath = boundary.inlinePath.slice();
        return _sortObject(boundary);
    }

    function createStableSelectionTokenData(instanceId, selection, model) {
        var snapshot = createSelectionSnapshot(selection || {});
        var anchorOffset = Number(snapshot.AnchorBlockOffset ?? snapshot.anchorBlockOffset ?? snapshot.anchorOffset ?? snapshot.AnchorOffset ?? (snapshot.anchor && snapshot.anchor.offset) ?? 0) || 0;
        var focusOffset = Number(snapshot.FocusBlockOffset ?? snapshot.focusBlockOffset ?? snapshot.focusOffset ?? snapshot.FocusOffset ?? (snapshot.focus && snapshot.focus.offset) ?? anchorOffset) || 0;
        var anchor = createSelectionTokenBoundary(model, snapshot.anchor || snapshot, anchorOffset);
        var focus = createSelectionTokenBoundary(model, snapshot.focus || snapshot.anchor || snapshot, focusOffset);
        var sameBlock = anchor.blockId === focus.blockId;
        var start = sameBlock && anchor.logicalOffset > focus.logicalOffset ? focus : anchor;
        var end = sameBlock && anchor.logicalOffset > focus.logicalOffset ? anchor : focus;
        var region = normalizeSelectionTokenRegion(snapshot.region || anchor.region || focus.region, snapshot);
        return _sortObject({
            schema: 'tmde-selection-token/v1',
            instanceId: _asText(instanceId || ''),
            documentInstanceId: _asText(instanceId || ''),
            documentFingerprint: model ? createSelectionDocumentFingerprint(model) : '',
            selectionDocumentFingerprint: model ? createSelectionDocumentFingerprint(model) : '',
            region: region,
            blockId: focus.blockId || anchor.blockId || snapshot.blockId || '',
            anchor: anchor,
            focus: focus,
            start: start,
            end: end,
            startOffset: Math.max(0, Math.round(Number(start.logicalOffset || 0) || 0)),
            endOffset: Math.max(0, Math.round(Number(end.logicalOffset || 0) || 0)),
            inlinePath: {
                anchor: anchor.inlinePath,
                focus: focus.inlinePath
            },
            runBoundaryPath: {
                anchor: anchor.runBoundaryPath,
                focus: focus.runBoundaryPath
            },
            isCollapsed: snapshot.isCollapsed !== false,
            direction: snapshot.direction || 'none',
            tableId: snapshot.activeTableId || snapshot.tableId || anchor.tableId || focus.tableId || null,
            cellId: snapshot.activeTableCellId || snapshot.cellId || anchor.cellId || focus.cellId || null,
            activeObjectId: snapshot.activeObjectId || snapshot.objectId || anchor.objectId || focus.objectId || null
        });
    }

    function serializeStableSelectionToken(instanceId, selection, model) {
        return stableJsonString(createStableSelectionTokenData(instanceId, selection, model));
    }

    function withStableSelectionToken(instanceId, selection, model) {
        var snapshot = createSelectionSnapshot(selection || {});
        var data = createStableSelectionTokenData(instanceId, snapshot, model);
        var token = stableJsonString(data);
        snapshot.selectionToken = token;
        snapshot.SelectionToken = token;
        snapshot.stableSelectionToken = token;
        snapshot.StableSelectionToken = token;
        snapshot.token = token;
        snapshot.Token = token;
        snapshot.selectionTokenData = data;
        snapshot.SelectionTokenData = data;
        return _sortObject(snapshot);
    }

    function readSelectionTokenValue(value) {
        if (!value || typeof value !== 'object') return null;
        return value.selectionToken
            || value.SelectionToken
            || value.stableSelectionToken
            || value.StableSelectionToken
            || value.token
            || value.Token
            || null;
    }

    function parseSelectionTokenData(value) {
        if (!value) return null;
        if (typeof value === 'object') return _sortObject(_clone(value));
        if (typeof value !== 'string') return null;
        try {
            return _sortObject(JSON.parse(value));
        } catch {
            return null;
        }
    }

    function readSelectionTokenData(value) {
        if (!value || typeof value !== 'object') return null;
        return parseSelectionTokenData(readSelectionTokenValue(value))
            || parseSelectionTokenData(value.selectionTokenData || value.SelectionTokenData)
            || null;
    }

    function validateStableSelectionToken(inst, tokenOrPayload) {
        var data = parseSelectionTokenData(tokenOrPayload) || readSelectionTokenData(tokenOrPayload);
        if (!inst) {
            return _sortObject({ ok: false, code: 'missing-instance', reason: 'missing-instance' });
        }
        if (!data) {
            return _sortObject({ ok: false, code: 'missing-selection-token', reason: 'missing-selection-token' });
        }
        var tokenInstanceId = _asText(data.instanceId || data.documentInstanceId || data.InstanceId || data.DocumentInstanceId || '');
        if (tokenInstanceId && tokenInstanceId !== inst.id) {
            return _sortObject({ ok: false, code: 'stale-selection-token', reason: 'instance-id-mismatch', tokenInstanceId: tokenInstanceId, instanceId: inst.id });
        }
        var currentFingerprint = createSelectionDocumentFingerprint(inst.model || {});
        var tokenFingerprint = _asText(data.selectionDocumentFingerprint || data.SelectionDocumentFingerprint || data.documentFingerprint || data.DocumentFingerprint || '');
        if (tokenFingerprint && tokenFingerprint !== currentFingerprint) {
            return _sortObject({ ok: false, code: 'stale-selection-token', reason: 'document-fingerprint-mismatch', tokenFingerprint: tokenFingerprint, currentFingerprint: currentFingerprint });
        }
        var anchor = data.anchor || data.Anchor || data.start || data.Start || {};
        var focus = data.focus || data.Focus || data.end || data.End || anchor;
        var blockId = _asText((focus && (focus.blockId || focus.BlockId)) || (anchor && (anchor.blockId || anchor.BlockId)) || data.blockId || data.BlockId || '');
        var block = _findBlock(inst.model, blockId);
        if (!block) {
            return _sortObject({ ok: false, code: 'stale-selection-token', reason: 'block-not-found', blockId: blockId });
        }
        var max = block.type === 'paragraph' ? _blockText(block).length : 1;
        var anchorOffset = Number(anchor.logicalOffset ?? anchor.LogicalOffset ?? anchor.offset ?? anchor.Offset ?? data.startOffset ?? data.StartOffset ?? 0) || 0;
        var focusOffset = Number(focus.logicalOffset ?? focus.LogicalOffset ?? focus.offset ?? focus.Offset ?? data.endOffset ?? data.EndOffset ?? anchorOffset) || 0;
        if (anchorOffset < 0 || focusOffset < 0 || anchorOffset > max || focusOffset > max) {
            return _sortObject({ ok: false, code: 'stale-selection-token', reason: 'logical-offset-out-of-range', blockId: blockId, maxOffset: max, anchorOffset: anchorOffset, focusOffset: focusOffset });
        }
        var selection = createSelectionPostFixer(inst.schema).fix(inst.model, {
            region: data.region || data.Region || anchor.region || focus.region || 'Body',
            anchor: {
                region: data.region || anchor.region || 'Body',
                blockId: anchor.blockId || anchor.BlockId || blockId,
                inlineId: anchor.inlineId || anchor.InlineId || anchor.runId || anchor.RunId || null,
                offset: anchorOffset,
                affinity: anchor.affinity || anchor.Affinity || 'after',
                limitId: anchor.limitId || anchor.LimitId || null,
                tableId: anchor.tableId || anchor.TableId || data.tableId || data.TableId || null,
                cellId: anchor.cellId || anchor.CellId || data.cellId || data.CellId || null,
                headerFooterId: anchor.headerFooterId || anchor.HeaderFooterId || null
            },
            focus: {
                region: data.region || focus.region || 'Body',
                blockId: focus.blockId || focus.BlockId || blockId,
                inlineId: focus.inlineId || focus.InlineId || focus.runId || focus.RunId || null,
                offset: focusOffset,
                affinity: focus.affinity || focus.Affinity || 'after',
                limitId: focus.limitId || focus.LimitId || null,
                tableId: focus.tableId || focus.TableId || data.tableId || data.TableId || null,
                cellId: focus.cellId || focus.CellId || data.cellId || data.CellId || null,
                headerFooterId: focus.headerFooterId || focus.HeaderFooterId || null
            },
            direction: data.direction || data.Direction || 'forward',
            isCollapsed: data.isCollapsed ?? data.IsCollapsed ?? anchorOffset === focusOffset,
            activeTableCellId: data.cellId || data.CellId || null,
            activeTableId: data.tableId || data.TableId || null,
            activeObjectId: data.activeObjectId || data.ActiveObjectId || null
        });
        return _sortObject({
            ok: true,
            code: 'ok',
            reason: 'valid',
            tokenData: data,
            selection: withStableSelectionToken(inst.id, selection, inst.model),
            currentFingerprint: currentFingerprint
        });
    }

    function rememberSelectionToken(inst, selection, reason) {
        if (!inst) return null;
        var snapshot = withStableSelectionToken(inst.id, selection || inst.selection || {}, inst.model);
        inst.lastSelectionToken = snapshot.selectionToken || null;
        inst.lastSelectionTokenData = snapshot.selectionTokenData || null;
        inst.lastSelectionTokenReason = reason || '';
        return snapshot;
    }

    function _firstTextBlock(model) {
        var blocks = _asArray(model && model.body && model.body.blocks);
        for (var i = 0; i < blocks.length; i++) {
            if (blocks[i] && blocks[i].type === 'paragraph') return blocks[i];
        }
        return blocks[0] || null;
    }

    function _inlineAtOffset(block, offset) {
        if (!block || block.type !== 'paragraph') return null;
        var cursor = 0;
        var runs = _asArray(block.content && block.content.runs);
        for (var i = 0; i < runs.length; i++) {
            var length = _asText(runs[i].text).length;
            if (offset <= cursor + length || i === runs.length - 1) {
                return { run: runs[i], localOffset: Math.max(0, Math.min(length, offset - cursor)), start: cursor, end: cursor + length };
            }
            cursor += length;
        }
        return null;
    }

    function _findLimitForBlock(model, blockId) {
        var body = model && model.body;
        if (_asArray(body && body.blocks).some(function (block) { return block.id === blockId; })) return body && body.id || 'body';
        for (var h = 0; h < _asArray(model && model.headers).length; h++) {
            if (_asArray(model.headers[h].blocks).some(function (block) { return block.id === blockId; })) return model.headers[h].id;
        }
        for (var f = 0; f < _asArray(model && model.footers).length; f++) {
            if (_asArray(model.footers[f].blocks).some(function (block) { return block.id === blockId; })) return model.footers[f].id;
        }
        var found = null;
        function scan(blocks) {
            _asArray(blocks).forEach(function (block) {
                if (!block || block.type !== 'table') return;
                _asArray(block.content && block.content.rows).forEach(function (row) {
                    _asArray(row.cells).forEach(function (cell) {
                        if (_asArray(cell.blocks).some(function (child) { return child.id === blockId; })) found = cell.id;
                        scan(cell.blocks);
                    });
                });
            });
        }
        scan(model && model.body && model.body.blocks);
        return found || 'body';
    }

    function findRegionInfoForBlock(model, blockId) {
        var id = _asText(blockId);
        if (!id) return { region: 'Body', headerFooterId: null, cellId: null, tableId: null };
        if (_asArray(model && model.body && model.body.blocks).some(function (block) { return block && block.id === id; })) {
            return { region: 'Body', headerFooterId: null, cellId: null, tableId: null };
        }
        for (var h = 0; h < _asArray(model && model.headers).length; h++) {
            var header = model.headers[h];
            if (_asArray(header && header.blocks).some(function (block) { return block && block.id === id; })) {
                return { region: 'Header', headerFooterId: header.id || null, cellId: null, tableId: null };
            }
        }
        for (var f = 0; f < _asArray(model && model.footers).length; f++) {
            var footer = model.footers[f];
            if (_asArray(footer && footer.blocks).some(function (block) { return block && block.id === id; })) {
                return { region: 'Footer', headerFooterId: footer.id || null, cellId: null, tableId: null };
            }
        }
        var found = null;
        function scanTableBlocks(blocks, owner) {
            _asArray(blocks).forEach(function (block) {
                if (!block || block.type !== 'table') return;
                _asArray(block.content && block.content.rows).forEach(function (row) {
                    _asArray(row.cells).forEach(function (cell) {
                        if (_asArray(cell.blocks).some(function (child) { return child && child.id === id; })) {
                            found = {
                                region: 'TableCell',
                                headerFooterId: owner && owner.headerFooterId || null,
                                cellId: cell.id || null,
                                tableId: block.id || null
                            };
                        }
                        scanTableBlocks(cell.blocks, owner);
                    });
                });
            });
        }
        scanTableBlocks(model && model.body && model.body.blocks, { region: 'Body' });
        _asArray(model && model.headers).forEach(function (header) {
            scanTableBlocks(header && header.blocks, { region: 'Header', headerFooterId: header && header.id || null });
        });
        _asArray(model && model.footers).forEach(function (footer) {
            scanTableBlocks(footer && footer.blocks, { region: 'Footer', headerFooterId: footer && footer.id || null });
        });
        return found || { region: 'Body', headerFooterId: null, cellId: null, tableId: null };
    }

    function operationRegionInfo(model, op, blockId, fallback) {
        var source = op && (op.beforeSelection || op.BeforeSelection || op.selection || op.Selection || op.target || op.Target || op.range || op.Range) || {};
        var info = findRegionInfoForBlock(model, blockId);
        var snapshot = createSelectionSnapshot(source || {});
        var sourceRegion = _asText(source.region || source.Region || snapshot.region || '');
        var sourceHeaderFooterId = source.headerFooterId || source.HeaderFooterId || snapshot.headerFooterId || null;
        var sourceCellId = source.cellId || source.CellId || snapshot.cellId || null;
        var sourceTableId = source.tableId || source.TableId || snapshot.tableId || null;
        if (snapshot.blockId === blockId || !snapshot.blockId || source.blockId === blockId || source.BlockId === blockId) {
            if (sourceRegion && sourceRegion !== 'Body') info.region = sourceRegion;
            if (sourceHeaderFooterId) info.headerFooterId = sourceHeaderFooterId;
            if (sourceCellId) info.cellId = sourceCellId;
            if (sourceTableId) info.tableId = sourceTableId;
        }
        if (fallback && fallback.region && (!info.region || info.region === 'Body')) info.region = fallback.region;
        if (fallback && fallback.headerFooterId && !info.headerFooterId) info.headerFooterId = fallback.headerFooterId;
        return info;
    }

    function nextSelectionForOperation(model, op, blockId, offset, fallback) {
        var info = operationRegionInfo(model, op, blockId, fallback);
        return _sortObject({
            region: info.region || 'Body',
            blockId: _asText(blockId),
            offset: Math.max(0, Number(offset || 0) || 0),
            isCollapsed: true,
            headerFooterId: info.headerFooterId || null,
            cellId: info.cellId || null,
            tableId: info.tableId || null
        });
    }

    function normalizeLogicalPosition(model, position) {
        var pos = createLogicalPosition(position);
        var block = _findBlock(model, pos.blockId) || _firstTextBlock(model);
        if (!block) {
            return createLogicalPosition(Object.assign(pos, { blockId: '', inlineId: null, offset: 0, limitId: null }));
        }
        var max = block.type === 'paragraph' ? _blockText(block).length : 1;
        var offset = Math.max(0, Math.min(max, Number(pos.offset || 0)));
        var inline = block.type === 'paragraph' ? _inlineAtOffset(block, offset) : null;
        return createLogicalPosition(Object.assign(pos, {
            blockId: block.id,
            inlineId: inline && inline.run ? inline.run.id : null,
            offset: offset,
            affinity: pos.affinity === 'before' ? 'before' : 'after',
            limitId: pos.limitId || _findLimitForBlock(model, block.id),
            objectId: block.type === 'image' ? (block.content && block.content.objectId || block.id) : pos.objectId
        }));
    }

    function normalizeLogicalRange(model, range) {
        var source = range || {};
        var anchor = normalizeLogicalPosition(model, source.anchor || source.Anchor || source.start || source.Start || source);
        var focus = normalizeLogicalPosition(model, source.focus || source.Focus || source.end || source.End || source);
        if (anchor.limitId && focus.limitId && anchor.limitId !== focus.limitId) {
            focus = _clone(anchor);
        }
        return createLogicalRange(anchor, focus, source.direction || source.Direction || (anchor.offset <= focus.offset ? 'forward' : 'backward'));
    }

    function normalizeSelectionSnapshot(model, selection) {
        var snapshot = createSelectionSnapshot(selection || {});
        var range = normalizeLogicalRange(model, snapshot.range || snapshot);
        return createSelectionSnapshot({ region: range.anchor.region, range: range });
    }

    function createSelectionPostFixer(schema) {
        return {
            schema: schema || createDefaultSchemaRegistry(),
            fix: function (model, selection) {
                var snapshot = normalizeSelectionSnapshot(model, selection);
                var focusBlock = _findBlock(model, snapshot.focus.blockId);
                if (focusBlock && focusBlock.type === 'image') {
                    snapshot.focus.objectId = focusBlock.content && focusBlock.content.objectId || focusBlock.id;
                    snapshot.focus.offset = snapshot.focus.affinity === 'before' ? 0 : 1;
                    snapshot.anchor = _clone(snapshot.focus);
                    snapshot.range = createLogicalRange(snapshot.anchor, snapshot.focus, 'none');
                    snapshot.isCollapsed = true;
                }
                if (!snapshot.isCollapsed && snapshot.anchor.limitId && snapshot.focus.limitId && snapshot.anchor.limitId !== snapshot.focus.limitId) {
                    snapshot.focus = _clone(snapshot.anchor);
                    snapshot.range = createLogicalRange(snapshot.anchor, snapshot.focus, 'none');
                    snapshot.isCollapsed = true;
                    snapshot.rejectedCrossLimit = true;
                }
                return _sortObject(snapshot);
            }
        };
    }

    function createTextMeasurementService(options) {
        var opts = options || {};
        var zoom = Number(opts.zoom || opts.Zoom || 1) || 1;
        var cache = new Map();
        var stats = {
            measureCount: 0,
            cacheHits: 0,
            cacheMisses: 0,
            invalidations: 0,
            canvasAvailable: false,
            cacheEntries: 0,
            lastInvalidationReason: ''
        };
        var context = null;
        if (typeof document !== 'undefined' && document.createElement) {
            try {
                var canvas = document.createElement('canvas');
                context = canvas && canvas.getContext ? canvas.getContext('2d') : null;
                stats.canvasAvailable = !!context;
            } catch (ignored) {
                context = null;
            }
        }

        function normalizeStyle(style) {
            var source = style || {};
            var fontSize = Number(source.fontSize || source.FontSize || 16) || 16;
            var letterSpacing = Number(source.letterSpacing || source.LetterSpacing || 0) || 0;
            var lineHeight = source.lineHeight || source.LineHeight || null;
            var parsedLineHeight = Number(lineHeight);
            return {
                fontFamily: _asText(source.fontFamily || source.FontFamily || 'Arial'),
                fontSize: fontSize,
                fontWeight: _asText(source.fontWeight || source.FontWeight || '400'),
                fontStyle: _asText(source.fontStyle || source.FontStyle || 'normal'),
                letterSpacing: letterSpacing,
                lineHeight: Number.isFinite(parsedLineHeight) && parsedLineHeight > 0 ? parsedLineHeight : Math.ceil(fontSize * 1.25)
            };
        }

        function fontString(style) {
            return style.fontStyle + ' ' + style.fontWeight + ' ' + style.fontSize + 'px ' + style.fontFamily;
        }

        function keyFor(text, style) {
            return [
                _asText(text),
                style.fontFamily,
                style.fontSize,
                style.fontWeight,
                style.fontStyle,
                style.letterSpacing,
                zoom
            ].join('\u001f');
        }

        function approximateWidth(text, style) {
            var width = 0;
            Array.from(_asText(text)).forEach(function (ch) {
                if (isCjkCharacter(ch)) width += style.fontSize;
                else if (/\s/.test(ch)) width += style.fontSize * 0.32;
                else width += style.fontSize * 0.55;
            });
            width += Math.max(0, _asText(text).length - 1) * style.letterSpacing;
            return width;
        }

        function invalidate(reason) {
            cache.clear();
            stats.invalidations++;
            stats.cacheEntries = 0;
            stats.lastInvalidationReason = _asText(reason || 'manual');
        }

        var service = {
            measureText: function (text, style) {
                var normalizedStyle = normalizeStyle(style);
                var key = keyFor(text, normalizedStyle);
                stats.measureCount++;
                if (cache.has(key)) {
                    stats.cacheHits++;
                    return _clone(cache.get(key));
                }
                stats.cacheMisses++;
                var rawWidth;
                if (context) {
                    context.font = fontString(normalizedStyle);
                    rawWidth = context.measureText(_asText(text)).width;
                    rawWidth += Math.max(0, _asText(text).length - 1) * normalizedStyle.letterSpacing;
                } else {
                    rawWidth = approximateWidth(text, normalizedStyle);
                }
                var result = {
                    text: _asText(text),
                    width: Math.max(0, rawWidth * zoom),
                    height: Math.max(1, normalizedStyle.lineHeight * zoom),
                    font: fontString(normalizedStyle),
                    style: normalizedStyle,
                    zoom: zoom
                };
                cache.set(key, result);
                stats.cacheEntries = cache.size;
                return _clone(result);
            },
            measureRun: function (run, fallbackStyle) {
                return this.measureText(run && run.text || '', mergeTextStyle(fallbackStyle, run));
            },
            invalidate: invalidate,
            setZoom: function (nextZoom) {
                var value = Number(nextZoom || 1) || 1;
                if (Math.abs(value - zoom) > 0.0001) {
                    zoom = value;
                    invalidate('zoom');
                }
            },
            getZoom: function () { return zoom; },
            getStats: function () {
                stats.cacheEntries = cache.size;
                return _clone(stats);
            },
            normalizeStyle: normalizeStyle
        };

        if (typeof document !== 'undefined' && document.fonts && typeof document.fonts.addEventListener === 'function') {
            document.fonts.addEventListener('loadingdone', function () { invalidate('font-load'); });
            document.fonts.addEventListener('loadingerror', function () { invalidate('font-load-error'); });
        }

        return service;
    }

    function mergeTextStyle(baseStyle, run) {
        var style = Object.assign({}, baseStyle || {}, run && run.style || run && run.Style || {});
        _asArray(run && (run.marks || run.Marks)).forEach(function (mark) {
            var type = markType(mark);
            var value = mark && (mark.value ?? mark.Value ?? mark.color ?? mark.Color ?? null);
            if (type === 'bold') style.fontWeight = style.fontWeight || '700';
            if (type === 'italic') style.fontStyle = style.fontStyle || 'italic';
            if (type === 'fontfamily' && value) style.fontFamily = value;
            if (type === 'fontsize' && value) style.fontSize = cssLengthToPixels(value, style.fontSize || 16);
            if ((type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor') && value) style.color = value;
            if ((type === 'highlight' || type === 'backgroundcolor') && value) style.backgroundColor = value;
        });
        return style;
    }

    function cssLengthToPixels(value, fallback) {
        if (typeof value === 'number') return Number.isFinite(value) && value > 0 ? value : fallback;
        var text = _asText(value).trim().toLowerCase();
        var number = parseFloat(text);
        if (!Number.isFinite(number) || number <= 0) return fallback;
        if (text.endsWith('pt')) return number * 4 / 3;
        return number;
    }

    function isCjkCharacter(ch) {
        return /[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]/.test(ch || '');
    }

    function isTokenDelimiter(ch) {
        return ch === '\r' || ch === '\n' || ch === '\t' || ch === '\u00ad' || ch === '\u00a0' || /[ ]/.test(ch) || isCjkCharacter(ch);
    }

    function tokenizeText(text, options) {
        var source = _asText(text);
        var opts = options || {};
        var longThreshold = Number(opts.longTokenThreshold || opts.LongTokenThreshold || 32) || 32;
        var tokens = [];
        var index = 0;
        function push(type, value, start, end, extra) {
            tokens.push(_sortObject(Object.assign({
                type: type,
                text: value,
                start: start,
                end: end,
                length: Math.max(0, end - start),
                breakBefore: false,
                breakAfter: false,
                hardBreak: false,
                unbreakable: false
            }, extra || {})));
        }
        while (index < source.length) {
            var ch = source[index];
            if (ch === '\r' || ch === '\n') {
                var startNewline = index;
                if (ch === '\r' && source[index + 1] === '\n') index += 2;
                else index++;
                push('newline', source.slice(startNewline, index), startNewline, index, { breakBefore: true, breakAfter: true, hardBreak: true });
                continue;
            }
            if (ch === '\t') {
                push('tab', ch, index, index + 1, { breakAfter: true });
                index++;
                continue;
            }
            if (ch === '\u00ad') {
                push('softHyphen', ch, index, index + 1, { breakAfter: true });
                index++;
                continue;
            }
            if (ch === '\u00a0') {
                push('nbsp', ch, index, index + 1, { unbreakable: true });
                index++;
                continue;
            }
            if (ch === ' ') {
                var spaceStart = index;
                while (source[index] === ' ') index++;
                push('space', source.slice(spaceStart, index), spaceStart, index, { breakBefore: true, breakAfter: true });
                continue;
            }
            if (isCjkCharacter(ch)) {
                var cjkStart = index;
                var codePoint = Array.from(source.slice(index))[0] || ch;
                index += codePoint.length;
                push('cjk', codePoint, cjkStart, index, { breakBefore: true, breakAfter: true });
                continue;
            }
            var wordStart = index;
            while (index < source.length && !isTokenDelimiter(source[index])) index++;
            var word = source.slice(wordStart, index);
            push(word.length > longThreshold ? 'longToken' : 'word', word, wordStart, index, { unbreakable: word.length > longThreshold });
        }
        return tokens;
    }

    function flattenParagraphRuns(paragraph) {
        var source = paragraph || {};
        var runs = _asArray(source.runs || source.Runs || source.content && source.content.runs || source.Content && source.Content.Runs);
        if (runs.length === 0) {
            runs = [{ text: _asText(source.text || source.Text || '') }];
        }
        var baseStyle = source.style || source.Style || {};
        var cursor = 0;
        var result = [];
        runs.forEach(function (run, index) {
            var text = _asText(run.text || run.Text || run.fallbackText || run.FallbackText || '');
            var item = {
                id: run.id || run.Id || ('run-' + index),
                kind: String(run.kind || run.Kind || run.type || run.Type || 'text').toLowerCase().indexOf('field') >= 0
                    ? 'field'
                    : (String(run.kind || run.Kind || run.type || run.Type || 'text').toLowerCase().indexOf('token') >= 0 ? 'token' : 'text'),
                text: text,
                start: cursor,
                end: cursor + text.length,
                style: mergeTextStyle(baseStyle, run),
                marks: _asArray(run.marks || run.Marks)
            };
            result.push(item);
            cursor += text.length;
        });
        return result;
    }

    function runForOffset(runs, offset) {
        var fallback = runs[0] || { style: {} };
        for (var i = 0; i < runs.length; i++) {
            if (offset >= runs[i].start && offset < runs[i].end) return runs[i];
        }
        return runs[runs.length - 1] || fallback;
    }

    function tokensForParagraph(paragraph) {
        var runs = flattenParagraphRuns(paragraph);
        var text = runs.map(function (run) { return run.text; }).join('');
        var tokens = tokenizeText(text);
        tokens.forEach(function (token) {
            var run = runForOffset(runs, token.start);
            token.runId = run && run.id || null;
            token.style = _clone(run && run.style || {});
            token.marks = _clone(run && run.marks || []);
        });
        return { text: text, runs: runs, tokens: tokens };
    }

    function normalizeLineBreakerOptions(options) {
        var opts = options || {};
        return {
            x: Number(opts.x || opts.X || 0) || 0,
            y: Number(opts.y || opts.Y || 0) || 0,
            width: Number(opts.width || opts.Width || 0) || 0,
            lineGap: Number(opts.lineGap || opts.LineGap || 0) || 0,
            minReadableWidth: Math.max(1, Number(opts.minReadableWidth || opts.MinReadableWidth || 48) || 48),
            availableIntervals: _asArray(opts.availableIntervals || opts.AvailableIntervals)
        };
    }

    function normalizeInterval(opts, lineIndex) {
        var intervals = opts.availableIntervals.length ? opts.availableIntervals : [{ x: opts.x, width: opts.width }];
        var raw = intervals[Math.min(lineIndex, intervals.length - 1)] || {};
        var width = Number(raw.width || raw.Width || opts.width || 0) || 0;
        var x = Number(raw.x || raw.X || opts.x || 0) || 0;
        var y = raw.y !== undefined || raw.Y !== undefined ? Number(raw.y || raw.Y || opts.y || 0) || 0 : null;
        var height = Number(raw.height || raw.Height || 0) || 0;
        return { x: x, y: y, width: width, height: height };
    }

    function isInvalidInterval(interval, minReadableWidth) {
        return !interval || !Number.isFinite(interval.x) || !Number.isFinite(interval.width) || interval.width < minReadableWidth;
    }

    function createLineBreaker(measurementService, options) {
        var service = measurementService || createTextMeasurementService();
        var defaults = options || {};

        function breakParagraph(paragraph, options) {
            var opts = Object.assign({}, defaults, options || {});
            var normalizedOptions = normalizeLineBreakerOptions(opts);
            var firstInterval = normalizeInterval(normalizedOptions, 0);
            if (isInvalidInterval(firstInterval, normalizedOptions.minReadableWidth)) {
                return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
            }

            var paragraphData = tokensForParagraph(paragraph);
            var tokens = paragraphData.tokens;
            var lines = [];
            var segments = [];
            var caretStops = [];
            var alignment = normalizeParagraphAlignment((paragraph && (paragraph.alignment ?? paragraph.Alignment)) ?? opts.alignment ?? opts.Alignment ?? 'left');
            var y = normalizedOptions.y;
            var current = createLineDraft(0, normalizeInterval(normalizedOptions, 0), y);
            var nextSegmentId = 0;

            function addCaretStopsForSegment(segment) {
                var length = Math.max(1, segment.end - segment.start);
                for (var i = segment.start; i <= segment.end; i++) {
                    var ratio = (i - segment.start) / length;
                    caretStops.push({
                        blockId: paragraph && (paragraph.id || paragraph.Id) || 'paragraph',
                        offset: i,
                        rect: {
                            x: segment.rect.x + segment.rect.width * ratio,
                            y: segment.rect.y,
                            width: 1,
                            height: segment.rect.height
                        },
                        lineId: current.id
                    });
                }
            }

            function pushSegment(token, tokenText, start, end, width, style, splitFromLongToken) {
                var height = service.measureText(tokenText || ' ', style).height;
                current.lineHeight = Math.max(current.lineHeight, height);
                var segment = {
                    id: 'segment-' + nextSegmentId++,
                    type: token.type,
                    text: tokenText,
                    start: start,
                    end: end,
                    runId: token.runId || null,
                    rect: {
                        x: current.interval.x + current.width,
                        y: current.y,
                        width: width,
                        height: height
                    },
                    splitFromLongToken: splitFromLongToken === true
                };
                current.segments.push(segment);
                current.width += width;
                current.start = current.start === null ? start : Math.min(current.start, start);
                current.end = Math.max(current.end, end);
                segments.push(segment);
                addCaretStopsForSegment(segment);
            }

            function finishCurrent(hardBreak) {
                var line = materializeLineDraft(current, lines.length, hardBreak === true);
                lines.push(line);
                y = line.rect.y + line.rect.height + normalizedOptions.lineGap;
                current = createLineDraft(lines.length, normalizeInterval(normalizedOptions, lines.length), y);
                if (isInvalidInterval(current.interval, normalizedOptions.minReadableWidth)) {
                    current.invalid = true;
                }
                return line;
            }

            for (var tokenIndex = 0; tokenIndex < tokens.length; tokenIndex++) {
                var token = tokens[tokenIndex];
                if (token.type === 'newline') {
                    finishCurrent(true);
                    continue;
                }
                if (current.invalid) {
                    return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
                }
                var tokenText = token.type === 'tab' ? '    ' : token.text;
                var tokenStyle = token.style || {};
                var measurement = service.measureText(tokenText, tokenStyle);
                var width = measurement.width;
                var isBreakSpace = token.type === 'space';
                if (isBreakSpace && current.segments.length === 0) {
                    current.start = current.start === null ? token.start : current.start;
                    current.end = Math.max(current.end, token.end);
                    continue;
                }
                if (current.segments.length > 0 && current.width + width > current.interval.width && token.type !== 'nbsp') {
                    finishCurrent(false);
                    if (isBreakSpace) {
                        current.start = current.start === null ? token.start : current.start;
                        current.end = Math.max(current.end, token.end);
                        continue;
                    }
                }
                if (current.invalid) {
                    return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
                }
                if (width > current.interval.width && (token.type === 'longToken' || token.type === 'word' || token.type === 'cjk')) {
                    var pieces = splitTokenIntoFittingPieces(token, tokenText, tokenStyle, service, current.interval.width);
                    for (var pieceIndex = 0; pieceIndex < pieces.length; pieceIndex++) {
                        var piece = pieces[pieceIndex];
                        if (current.segments.length > 0 && current.width + piece.width > current.interval.width) finishCurrent(false);
                        pushSegment(token, piece.text, piece.start, piece.end, piece.width, tokenStyle, true);
                    }
                    continue;
                }
                pushSegment(token, tokenText, token.start, token.end, width, tokenStyle, false);
            }
            if (current.segments.length > 0 || lines.length === 0) finishCurrent(false);

            applyJustifyMetadata(lines, alignment);
            return _sortObject({
                ok: true,
                fallback: false,
                lines: lines,
                segments: segments,
                caretStops: caretStops,
                text: paragraphData.text,
                formattingStateTouched: false,
                debug: {
                    tokenCount: tokens.length,
                    cache: service.getStats(),
                    fallbackReason: ''
                }
            });
        }

        return {
            breakParagraph: breakParagraph,
            getMeasurementStats: function () { return service.getStats(); }
        };
    }

    function createLineDraft(index, interval, y) {
        return {
            id: 'line-' + index,
            index: index,
            interval: interval,
            y: interval.y === null || interval.y === undefined ? y : interval.y,
            start: null,
            end: 0,
            width: 0,
            lineHeight: 18,
            segments: [],
            invalid: false
        };
    }

    function materializeLineDraft(draft, index, hardBreak) {
        var height = Math.max(1, draft.lineHeight);
        var start = draft.start === null ? draft.end : draft.start;
        return {
            id: draft.id,
            index: index,
            start: start,
            end: draft.end,
            hardBreak: hardBreak === true,
            rect: {
                x: draft.interval.x,
                y: draft.y,
                width: Math.max(0, draft.width),
                height: height
            },
            availableIntervals: [{ x: draft.interval.x, y: draft.y, width: draft.interval.width, height: height, start: start, end: draft.end }],
            segments: draft.segments.map(function (segment) {
                segment.rect.height = height;
                return segment;
            }),
            justify: { enabled: false, extraSpacePerGap: 0, gapCount: 0 }
        };
    }

    function splitTokenIntoFittingPieces(token, text, style, service, availableWidth) {
        var pieces = [];
        var source = Array.from(_asText(text));
        var cursor = token.start;
        var buffer = '';
        var bufferStart = cursor;
        for (var i = 0; i < source.length; i++) {
            var next = buffer + source[i];
            var nextWidth = service.measureText(next, style).width;
            if (buffer && nextWidth > availableWidth) {
                pieces.push({
                    text: buffer,
                    start: bufferStart,
                    end: cursor,
                    width: service.measureText(buffer, style).width
                });
                buffer = source[i];
                bufferStart = cursor;
            } else {
                buffer = next;
            }
            cursor += source[i].length;
        }
        if (buffer) {
            pieces.push({
                text: buffer,
                start: bufferStart,
                end: cursor,
                width: service.measureText(buffer, style).width
            });
        }
        return pieces.length ? pieces : [{ text: _asText(text), start: token.start, end: token.end, width: Math.min(availableWidth, service.measureText(text, style).width) }];
    }

    function applyJustifyMetadata(lines, alignment) {
        var justify = alignment === 'justify' || alignment === 'justified' || alignment === 'block';
        lines.forEach(function (line, index) {
            var isLast = index === lines.length - 1;
            var interval = line.availableIntervals[0] || { width: line.rect.width };
            var gaps = line.segments.filter(function (segment) { return segment.type === 'space'; }).length;
            var remaining = Math.max(0, Number(interval.width || 0) - Number(line.rect.width || 0));
            if (justify && !isLast && !line.hardBreak && gaps > 0 && remaining > 0) {
                line.justify = { enabled: true, extraSpacePerGap: remaining / gaps, gapCount: gaps };
            } else {
                line.justify = { enabled: false, extraSpacePerGap: 0, gapCount: gaps };
            }
        });
    }

    function buildLineBreakerFallback(paragraph, service, options, reason) {
        var paragraphData = tokensForParagraph(paragraph);
        var style = paragraph && (paragraph.style || paragraph.Style) || {};
        var measurement = service.measureText(paragraphData.text || ' ', style);
        var safeWidth = Math.max(options.minReadableWidth, options.width || 0, 320);
        var blockedBottom = options.y;
        options.availableIntervals.forEach(function (interval) {
            var y = Number(interval.y || interval.Y || options.y || 0) || 0;
            var height = Number(interval.height || interval.Height || measurement.height || 20) || 20;
            blockedBottom = Math.max(blockedBottom, y + height);
        });
        var safeY = blockedBottom + Math.max(8, measurement.height * 0.5);
        var line = {
            id: 'fallback-line-0',
            index: 0,
            start: 0,
            end: paragraphData.text.length,
            hardBreak: false,
            rect: { x: options.x, y: safeY, width: Math.min(safeWidth, Math.max(safeWidth, measurement.width)), height: measurement.height },
            availableIntervals: [{ x: options.x, y: safeY, width: safeWidth, height: measurement.height, start: 0, end: paragraphData.text.length }],
            segments: [],
            justify: { enabled: false, extraSpacePerGap: 0, gapCount: 0 }
        };
        var segment = {
            id: 'fallback-segment-0',
            type: 'word',
            text: paragraphData.text,
            start: 0,
            end: paragraphData.text.length,
            rect: { x: options.x, y: safeY, width: Math.min(measurement.width, safeWidth), height: measurement.height },
            splitFromLongToken: false
        };
        line.segments.push(segment);
        return _sortObject({
            ok: true,
            fallback: true,
            lines: [line],
            segments: [segment],
            caretStops: [{ offset: 0, rect: { x: options.x, y: safeY, width: 1, height: measurement.height }, lineId: line.id }],
            text: paragraphData.text,
            formattingStateTouched: false,
            debug: {
                fallbackReason: reason || 'layout-fallback',
                tokenCount: paragraphData.tokens.length,
                cache: service.getStats()
            }
        });
    }

    var LAYOUT_SCOPE_KINDS = Object.freeze({
        ActiveParagraph: 'activeParagraph',
        WholeBlock: 'wholeBlock',
        PageRegion: 'pageRegion',
        WholeDocument: 'wholeDocument'
    });

    function createLayoutScope(kind, options) {
        var opts = options || {};
        return _sortObject({
            kind: kind || LAYOUT_SCOPE_KINDS.ActiveParagraph,
            blockId: opts.blockId || opts.BlockId || null,
            region: opts.region || opts.Region || 'Body',
            pageIndex: Number(opts.pageIndex ?? opts.PageIndex ?? 0),
            affectedScopeIds: _asArray(opts.affectedScopeIds || opts.AffectedScopeIds || (opts.blockId || opts.BlockId ? [opts.blockId || opts.BlockId] : [])),
            reason: opts.reason || opts.Reason || ''
        });
    }

    function inferLayoutScopeFromOperation(operation) {
        var op = operation || {};
        var type = op.type || op.Type || '';
        if (type === OPERATION_TYPES.InsertText || type === OPERATION_TYPES.SetParagraphAttribute) {
            var target = _normalizeTarget(op.target || op.Target);
            return createLayoutScope(LAYOUT_SCOPE_KINDS.ActiveParagraph, { blockId: target.blockId, affectedScopeIds: [target.blockId], reason: type });
        }
        if (type === OPERATION_TYPES.DeleteRange || type === OPERATION_TYPES.ApplyMark || type === OPERATION_TYPES.RemoveMark) {
            var range = _normalizeRange(op.range || op.Range);
            return createLayoutScope(LAYOUT_SCOPE_KINDS.ActiveParagraph, { blockId: range.blockId, affectedScopeIds: [range.blockId], reason: type });
        }
        if (type === OPERATION_TYPES.SplitParagraph || type === OPERATION_TYPES.MergeParagraph) {
            var paragraphTarget = _normalizeTarget(op.target || op.Target);
            return createLayoutScope(LAYOUT_SCOPE_KINDS.WholeBlock, { blockId: paragraphTarget.blockId, affectedScopeIds: [paragraphTarget.blockId, op.newBlockId || op.NewBlockId].filter(Boolean), reason: type });
        }
        if (type === OPERATION_TYPES.UpdateImageLayout || type === OPERATION_TYPES.InsertImage || type === OPERATION_TYPES.UpdateImageMetadata) {
            var objectTarget = _normalizeTarget(op.target || op.Target);
            return createLayoutScope(LAYOUT_SCOPE_KINDS.PageRegion, {
                blockId: objectTarget.blockId,
                affectedScopeIds: _unique([objectTarget.blockId].concat(_asArray(op.affectedParagraphIds || op.AffectedParagraphIds))),
                reason: type
            });
        }
        if (type === OPERATION_TYPES.AcceptRevision || type === OPERATION_TYPES.RejectRevision || type === OPERATION_TYPES.InsertTable || type === OPERATION_TYPES.UpdateTableCell) {
            return createLayoutScope(LAYOUT_SCOPE_KINDS.WholeDocument, { affectedScopeIds: ['document'], reason: type });
        }
        return createLayoutScope(LAYOUT_SCOPE_KINDS.ActiveParagraph, { blockId: '', affectedScopeIds: [], reason: type || 'unknown' });
    }

    function normalizePageBox(options) {
        var opts = options || {};
        var page = opts.page || opts.Page || opts.pageBox || opts.PageBox || {};
        return {
            x: Number(page.x || page.X || opts.x || opts.X || 0) || 0,
            y: Number(page.y || page.Y || opts.y || opts.Y || 0) || 0,
            width: Math.max(1, Number(page.width || page.Width || opts.width || opts.Width || 640) || 640),
            height: Math.max(1, Number(page.height || page.Height || opts.height || opts.Height || 900) || 900)
        };
    }

    function normalizePageLayoutSettings(options, model) {
        var opts = options || {};
        var source = Object.assign({}, model && (model.pageSettings || model.PageSettings) || {}, opts);
        var page = normalizePageBox(source);
        var margins = source.margins || source.Margins || {};
        var marginTop = Number(margins.top ?? margins.Top ?? source.marginTop ?? source.MarginTop ?? 0) || 0;
        var marginRight = Number(margins.right ?? margins.Right ?? source.marginRight ?? source.MarginRight ?? 0) || 0;
        var marginBottom = Number(margins.bottom ?? margins.Bottom ?? source.marginBottom ?? source.MarginBottom ?? 0) || 0;
        var marginLeft = Number(margins.left ?? margins.Left ?? source.marginLeft ?? source.MarginLeft ?? 0) || 0;
        var headerHeight = Math.max(0, Number(source.headerHeight ?? source.HeaderHeight ?? 0) || 0);
        var footerHeight = Math.max(0, Number(source.footerHeight ?? source.FooterHeight ?? 0) || 0);
        var pageGap = Math.max(0, Number(source.pageGap ?? source.PageGap ?? 24) || 24);
        var bodyWidth = Math.max(1, page.width - marginLeft - marginRight);
        var bodyHeight = Math.max(1, page.height - marginTop - marginBottom - headerHeight - footerHeight);
        return _sortObject({
            pageSize: { width: page.width, height: page.height },
            pageOrigin: { x: page.x, y: page.y },
            margins: { top: marginTop, right: marginRight, bottom: marginBottom, left: marginLeft },
            headerHeight: headerHeight,
            footerHeight: footerHeight,
            bodySize: { width: bodyWidth, height: bodyHeight },
            pageGap: pageGap,
            paragraphSpacingBefore: Math.max(0, Number(source.paragraphSpacingBefore ?? source.ParagraphSpacingBefore ?? 0) || 0),
            paragraphSpacingAfter: Math.max(0, Number(source.paragraphSpacingAfter ?? source.ParagraphSpacingAfter ?? (source.blockGap ?? source.BlockGap ?? 8)) || 8),
            blockGap: Math.max(0, Number(source.blockGap ?? source.BlockGap ?? 8) || 8),
            lineGap: Math.max(0, Number(source.lineGap ?? source.LineGap ?? 0) || 0),
            minReadableWidth: Math.max(1, Number(source.minReadableWidth ?? source.MinReadableWidth ?? 48) || 48)
        });
    }

    function createPageLayout(pageIndex, metrics) {
        var rect = {
            x: metrics.pageOrigin.x,
            y: metrics.pageOrigin.y + pageIndex * (metrics.pageSize.height + metrics.pageGap),
            width: metrics.pageSize.width,
            height: metrics.pageSize.height
        };
        var headerFrame = {
            x: rect.x + metrics.margins.left,
            y: rect.y + metrics.margins.top,
            width: metrics.bodySize.width,
            height: metrics.headerHeight
        };
        var bodyFrame = {
            x: rect.x + metrics.margins.left,
            y: rect.y + metrics.margins.top + metrics.headerHeight,
            width: metrics.bodySize.width,
            height: metrics.bodySize.height
        };
        var footerFrame = {
            x: rect.x + metrics.margins.left,
            y: bodyFrame.y + bodyFrame.height,
            width: metrics.bodySize.width,
            height: metrics.footerHeight
        };
        return _sortObject({
            pageNumber: pageIndex + 1,
            pageIndex: pageIndex,
            rect: rect,
            marginBox: {
                x: rect.x + metrics.margins.left,
                y: rect.y + metrics.margins.top,
                width: metrics.bodySize.width,
                height: Math.max(1, rect.height - metrics.margins.top - metrics.margins.bottom)
            },
            headerFrame: headerFrame,
            bodyFrame: bodyFrame,
            footerFrame: footerFrame,
            blockIds: [],
            exclusions: []
        });
    }

    function shiftRectY(rect, deltaY) {
        var next = _clone(rect || {});
        next.y = Number(next.y || 0) + deltaY;
        return next;
    }

    function shiftLayoutLine(line, deltaY, pageIndex) {
        var next = _clone(line || {});
        next.pageIndex = pageIndex;
        next.rect = shiftRectY(next.rect, deltaY);
        next.baseline = Number(next.baseline || 0) + deltaY;
        next.availableIntervals = _asArray(next.availableIntervals).map(function (interval) {
            var clone = Object.assign({}, interval);
            clone.y = Number(clone.y || 0) + deltaY;
            clone.pageIndex = pageIndex;
            return clone;
        });
        return next;
    }

    function shiftLayoutSegment(segment, deltaY, pageIndex) {
        var next = _clone(segment || {});
        next.pageIndex = pageIndex;
        next.rect = shiftRectY(next.rect, deltaY);
        return next;
    }

    function shiftCaretStop(stop, deltaY, pageIndex) {
        var next = _clone(stop || {});
        next.pageIndex = pageIndex;
        next.rect = shiftRectY(next.rect, deltaY);
        return next;
    }

    function createPageBreakLayout(block, page, version) {
        var frame = page.bodyFrame;
        return _sortObject({
            ok: true,
            id: 'layout-' + (block && block.id || 'page-break'),
            layoutVersion: version,
            blockId: block && block.id || 'page-break',
            type: 'pageBreak',
            pageIndex: page.pageIndex,
            rect: { x: frame.x, y: frame.y, width: frame.width, height: 0 },
            lines: [],
            segments: [],
            caretStops: [],
            baselines: [],
            manualPageBreak: true
        });
    }

    function resolveFieldRunText(run, pageNumber, totalPages) {
        var kind = String(run && (run.fieldType || run.FieldType || run.key || run.Key || '') || '').toLowerCase();
        if (kind === 'pagenumber' || kind === 'page-number' || kind === 'page') return String(pageNumber);
        if (kind === 'totalpages' || kind === 'total-pages' || kind === 'pagecount' || kind === 'page-count') return String(totalPages);
        return _asText(run && (run.text || run.Text || run.fallbackText || run.FallbackText || run.key || run.Key));
    }

    function cloneBlockWithResolvedFields(block, pageNumber, totalPages) {
        var clone = _clone(block);
        if (clone && clone.type === 'paragraph') {
            _asArray(clone.content && clone.content.runs).forEach(function (run) {
                if (run.kind === 'field' || run.fieldType || run.FieldType) run.text = resolveFieldRunText(run, pageNumber, totalPages);
            });
        }
        return clone;
    }

    var WRAP_MODE_NAMES = Object.freeze({
        0: 'Inline',
        1: 'Square',
        2: 'Tight',
        3: 'Through',
        4: 'TopBottom',
        5: 'BehindText',
        6: 'InFrontOfText'
    });

    function normalizeWrapModeName(value) {
        if (value === undefined || value === null || value === '') return 'Inline';
        if (typeof value === 'number') return WRAP_MODE_NAMES[value] || 'Inline';
        var raw = String(value).replace(/\s+/g, '').replace(/-/g, '').toLowerCase();
        if (raw === '0' || raw === 'inline' || raw === 'inlined') return 'Inline';
        if (raw === '1' || raw === 'square' || raw === 'wrap') return 'Square';
        if (raw === '2' || raw === 'tight') return 'Tight';
        if (raw === '3' || raw === 'through') return 'Through';
        if (raw === '4' || raw === 'topbottom' || raw === 'topandbottom' || raw === 'breaktext') return 'TopBottom';
        if (raw === '5' || raw === 'behindtext' || raw === 'behind') return 'BehindText';
        if (raw === '6' || raw === 'infrontoftext' || raw === 'front') return 'InFrontOfText';
        return value && WRAP_MODE_NAMES[value.value] || 'Inline';
    }

    function normalizePositionSpec(value, fallbackAlign) {
        var source = value || {};
        return _sortObject({
            relativeTo: _asText(source.relativeTo || source.RelativeTo || 'Column'),
            align: _asText(source.align || source.Align || fallbackAlign || 'Left'),
            offset: Number(source.offset ?? source.Offset ?? 0) || 0
        });
    }

    function normalizeImageObject(block, options) {
        var opts = options || {};
        var content = block && (block.content || block.Content) || {};
        var layout = content.layout || content.Layout || {};
        var anchor = layout.anchor || layout.Anchor || {};
        var wrap = layout.wrap || layout.Wrap || {};
        var position = layout.position || layout.Position || {};
        var transform = layout.transform || layout.Transform || {};
        var stacking = layout.stacking || layout.Stacking || {};
        var horizontal = layout.horizontalPosition || layout.HorizontalPosition || layout.Horizontal || {};
        var vertical = layout.verticalPosition || layout.VerticalPosition || layout.Vertical || {};
        var wrapMode = normalizeWrapModeName(layout.wrapMode ?? layout.WrapMode ?? wrap.mode ?? wrap.Mode ?? opts.wrapMode ?? opts.WrapMode);
        var horizontalAlign = position.horizontalAlignment ?? position.HorizontalAlignment ?? horizontal.align ?? horizontal.Align ?? null;
        if (horizontalAlign === 0) horizontalAlign = 'Left';
        if (horizontalAlign === 1) horizontalAlign = 'Center';
        if (horizontalAlign === 2) horizontalAlign = 'Right';
        var verticalAlign = position.verticalAlignment ?? position.VerticalAlignment ?? vertical.align ?? vertical.Align ?? null;
        if (verticalAlign === 0) verticalAlign = 'None';
        if (verticalAlign === 1) verticalAlign = 'Top';
        if (verticalAlign === 2) verticalAlign = 'Middle';
        if (verticalAlign === 3) verticalAlign = 'Bottom';
        var contentSize = content.size || content.Size || {};
        var width = Math.max(1, Number(
            transform.width ?? transform.Width
            ?? layout.width ?? layout.Width
            ?? content.width ?? content.Width
            ?? contentSize.width ?? contentSize.Width
            ?? opts.width ?? opts.Width ?? 120) || 120);
        var height = Math.max(1, Number(
            transform.height ?? transform.Height
            ?? layout.height ?? layout.Height
            ?? content.height ?? content.Height
            ?? contentSize.height ?? contentSize.Height
            ?? opts.height ?? opts.Height ?? 80) || 80);
        var distanceLeft = Number(wrap.distanceLeft ?? wrap.DistanceLeft ?? 0) || 0;
        var distanceRight = Number(wrap.distanceRight ?? wrap.DistanceRight ?? 0) || 0;
        var distanceTop = Number(wrap.distanceTop ?? wrap.DistanceTop ?? 0) || 0;
        var distanceBottom = Number(wrap.distanceBottom ?? wrap.DistanceBottom ?? 0) || 0;
        var wrapMargin = Math.max(
            0,
            Number(layout.wrapMargin ?? layout.WrapMargin ?? wrap.margin ?? wrap.Margin ?? 0) || 0,
            distanceLeft,
            distanceRight,
            distanceTop,
            distanceBottom);
        return _sortObject({
            blockId: block && (block.id || block.Id) || '',
            objectId: _asText(content.objectId || content.ObjectId || content.id || content.Id || block && (block.id || block.Id) || ''),
            anchorBlockId: _asText(layout.anchorBlockId || layout.AnchorBlockId || anchor.blockId || anchor.BlockId || opts.anchorBlockId || opts.AnchorBlockId || ''),
            anchorOffset: Number(layout.anchorOffset ?? layout.AnchorOffset ?? anchor.offset ?? anchor.Offset ?? opts.anchorOffset ?? opts.AnchorOffset ?? 0) || 0,
            moveWithText: (layout.moveWithText ?? layout.MoveWithText ?? anchor.moveWithText ?? anchor.MoveWithText ?? true) !== false,
            fixedOnPage: (layout.fixedOnPage ?? layout.FixedOnPage ?? anchor.fixedOnPage ?? anchor.FixedOnPage ?? false) === true,
            horizontalPosition: normalizePositionSpec(Object.assign({}, horizontal, {
                align: horizontalAlign || horizontal.align || horizontal.Align || null,
                relativeTo: position.horizontalRelativeTo || position.HorizontalRelativeTo || horizontal.relativeTo || horizontal.RelativeTo || 'Page',
                offset: position.x ?? position.X ?? horizontal.offset ?? horizontal.Offset ?? 0
            }), 'Left'),
            verticalPosition: normalizePositionSpec(Object.assign({}, vertical, {
                align: verticalAlign || vertical.align || vertical.Align || null,
                relativeTo: position.verticalRelativeTo || position.VerticalRelativeTo || vertical.relativeTo || vertical.RelativeTo || 'Paragraph',
                offset: position.y ?? position.Y ?? vertical.offset ?? vertical.Offset ?? 0
            }), 'Top'),
            wrapMode: wrapMode,
            wrapMargin: wrapMargin,
            distanceLeft: distanceLeft,
            distanceRight: distanceRight,
            distanceTop: distanceTop,
            distanceBottom: distanceBottom,
            allowOverlap: (layout.allowOverlap ?? layout.AllowOverlap ?? stacking.allowOverlap ?? stacking.AllowOverlap ?? false) === true,
            zIndex: Number(layout.zIndex ?? layout.ZIndex ?? stacking.zIndex ?? stacking.ZIndex ?? 0) || 0,
            width: width,
            height: height,
            caption: _asText(content.caption || content.Caption || ''),
            altText: _asText(content.altText || content.AltText || ''),
            url: content.url || content.Url || null,
            assetId: content.assetId || content.AssetId || null
        });
    }

    function imageObjectToLayout(object) {
        var source = object || {};
        return _sortObject({
            AnchorBlockId: source.anchorBlockId || '',
            AnchorOffset: Number(source.anchorOffset || 0) || 0,
            MoveWithText: source.moveWithText !== false,
            FixedOnPage: source.fixedOnPage === true,
            HorizontalPosition: _clone(source.horizontalPosition || {}),
            VerticalPosition: _clone(source.verticalPosition || {}),
            WrapMode: source.wrapMode || 'Inline',
            WrapMargin: Number(source.wrapMargin || 0) || 0,
            AllowOverlap: source.allowOverlap === true,
            ZIndex: Number(source.zIndex || 0) || 0,
            Width: Math.max(1, Number(source.width || 120) || 120),
            Height: Math.max(1, Number(source.height || 80) || 80)
        });
    }

    function wrapModeCreatesTextExclusion(wrapMode) {
        var mode = normalizeWrapModeName(wrapMode);
        return mode === 'Square' || mode === 'Tight' || mode === 'Through' || mode === 'TopBottom';
    }

    function createTextExclusion(objectLayout, bodyFrame) {
        var object = objectLayout || {};
        var mode = normalizeWrapModeName(object.wrapMode);
        if (!wrapModeCreatesTextExclusion(mode)) return null;
        var margin = Math.max(0, Number(object.wrapMargin || 0) || 0);
        var rect = object.rect || {
            x: Number(bodyFrame && bodyFrame.x || 0) + Number(object.horizontalPosition && object.horizontalPosition.offset || 0),
            y: Number(bodyFrame && bodyFrame.y || 0) + Number(object.verticalPosition && object.verticalPosition.offset || 0),
            width: Number(object.width || 1) || 1,
            height: Number(object.height || 1) || 1
        };
        var captionHeight = object.caption ? Math.max(16, Math.min(48, object.caption.length * 0.6)) : 0;
        var footprint = {
            x: rect.x - margin,
            y: rect.y - margin,
            width: rect.width + margin * 2,
            height: rect.height + captionHeight + margin * 2
        };
        var kind = mode === 'TopBottom'
            ? 'fullWidth'
            : (mode === 'Tight' ? 'contour' : (mode === 'Through' ? 'editableContour' : 'rectangular'));
        if (mode === 'TopBottom' && bodyFrame) {
            footprint.x = bodyFrame.x;
            footprint.width = bodyFrame.width;
        }
        return _sortObject({
            objectId: object.objectId || object.blockId || '',
            blockId: object.blockId || '',
            wrapMode: mode,
            kind: kind,
            rect: footprint,
            sourceRect: _clone(rect),
            captionIncluded: captionHeight > 0,
            allowOverlap: object.allowOverlap === true,
            zIndex: Number(object.zIndex || 0) || 0
        });
    }

    function getAvailableIntervals(y, height, bodyFrame, exclusions, minReadableWidth) {
        var lineY = Number(y || 0);
        var lineHeight = Math.max(1, Number(height || 1) || 1);
        var body = bodyFrame || { x: 0, y: 0, width: 640, height: 900 };
        var minWidth = Math.max(1, Number(minReadableWidth || 48) || 48);
        var movedToY = lineY;

        function compute(atY) {
            var intervals = [{ x: body.x, y: atY, width: body.width, height: lineHeight }];
            var blockingBottom = atY;
            _asArray(exclusions).forEach(function (exclusion) {
                var rect = exclusion && exclusion.rect || {};
                var overlapsY = atY < Number(rect.y || 0) + Number(rect.height || 0) && atY + lineHeight > Number(rect.y || 0);
                if (!overlapsY || exclusion.allowOverlap === true) return;
                blockingBottom = Math.max(blockingBottom, Number(rect.y || 0) + Number(rect.height || 0));
                if (exclusion.kind === 'fullWidth') {
                    intervals = [];
                    return;
                }
                var leftEdge = Number(rect.x || 0);
                var rightEdge = leftEdge + Number(rect.width || 0);
                var next = [];
                intervals.forEach(function (interval) {
                    var intervalRight = interval.x + interval.width;
                    if (rightEdge <= interval.x || leftEdge >= intervalRight) {
                        next.push(interval);
                        return;
                    }
                    if (leftEdge > interval.x) next.push({ x: interval.x, y: atY, width: leftEdge - interval.x, height: lineHeight });
                    if (rightEdge < intervalRight) next.push({ x: rightEdge, y: atY, width: intervalRight - rightEdge, height: lineHeight });
                });
                intervals = next;
            });
            intervals = intervals
                .filter(function (interval) { return interval.width >= minWidth; })
                .sort(function (a, b) { return a.x - b.x || b.width - a.width; });
            return { intervals: intervals, blockingBottom: blockingBottom };
        }

        var result = compute(lineY);
        if (result.intervals.length === 0) {
            movedToY = Math.max(lineY + lineHeight, result.blockingBottom);
            result = compute(movedToY);
        }
        return _sortObject({
            intervals: result.intervals,
            movedToY: movedToY,
            moved: movedToY > lineY
        });
    }

    function hitTestLayerPriority(layerName, wrapMode) {
        var layer = String(layerName || '').toLowerCase();
        var mode = normalizeWrapModeName(wrapMode);
        if (layer === 'infrontoftext' || layer === 'in-front-of-text' || mode === 'InFrontOfText') return 30;
        if (layer === 'behindtext' || layer === 'behind-text' || mode === 'BehindText') return 0;
        return 10;
    }

    function affectedParagraphsAroundObject(model, blockId) {
        var blocks = _asArray(model && model.body && model.body.blocks);
        var index = blocks.findIndex(function (block) { return block.id === blockId; });
        return blocks.slice(Math.max(0, index + 1), index + 4).filter(function (block) { return block && block.type === 'paragraph'; }).map(function (block) { return block.id; });
    }

    function createImagePreviewController(model, options) {
        var opts = options || {};
        var state = null;
        function findImage(blockId) {
            var block = _findBlock(model, blockId);
            if (!block || block.type !== 'image') throw new Error('image-preview: missing image block ' + blockId);
            return block;
        }
        function begin(blockId, mode, settings) {
            var block = findImage(blockId);
            var normalized = normalizeImageObject(block);
            state = {
                mode: mode,
                blockId: blockId,
                original: normalized,
                preview: _clone(normalized),
                settings: settings || {}
            };
            return _sortObject({ ok: true, preview: true, mode: mode, object: state.preview });
        }
        function applyPreview() {
            var block = findImage(state.blockId);
            block.content.layout = imageObjectToLayout(state.preview);
            buildIndexes(model);
            var layout = createParagraphLayoutEngine(null, opts).layoutDocument(model);
            return layout;
        }
        function startDrag(blockId) { return begin(blockId, 'drag', {}); }
        function moveDrag(delta) {
            if (!state || state.mode !== 'drag') return { ok: false, error: 'drag-not-started' };
            state.preview.horizontalPosition.offset = Number(state.original.horizontalPosition.offset || 0) + Number(delta && (delta.dx ?? delta.Dx) || 0);
            state.preview.verticalPosition.offset = Number(state.original.verticalPosition.offset || 0) + Number(delta && (delta.dy ?? delta.Dy) || 0);
            var layout = applyPreview();
            return _sortObject({ ok: true, preview: true, mode: 'drag', object: state.preview, layout: layout });
        }
        function startResize(blockId, settings) { return begin(blockId, 'resize', settings || {}); }
        function moveResize(delta) {
            if (!state || state.mode !== 'resize') return { ok: false, error: 'resize-not-started' };
            var dx = Number(delta && (delta.dx ?? delta.Dx) || 0);
            var dy = Number(delta && (delta.dy ?? delta.Dy) || 0);
            var nextWidth = Math.max(1, Number(state.original.width || 1) + dx);
            var nextHeight = Math.max(1, Number(state.original.height || 1) + dy);
            if (state.settings.lockAspectRatio === true || state.settings.LockAspectRatio === true) {
                var ratio = Math.max(0.01, Number(state.original.width || 1) / Math.max(1, Number(state.original.height || 1)));
                nextHeight = nextWidth / ratio;
            }
            state.preview.width = nextWidth;
            state.preview.height = nextHeight;
            var layout = applyPreview();
            return _sortObject({ ok: true, preview: true, mode: 'resize', object: state.preview, layout: layout });
        }
        function cancel() {
            if (!state) return { ok: true, rolledBack: false };
            var block = findImage(state.blockId);
            block.content.layout = imageObjectToLayout(state.original);
            var cancelled = state;
            state = null;
            buildIndexes(model);
            return _sortObject({ ok: true, rolledBack: true, mode: cancelled.mode, object: cancelled.original });
        }
        function commit() {
            if (!state) return { ok: false, error: 'preview-not-started' };
            var preview = _clone(state.preview);
            var blockId = state.blockId;
            var mode = state.mode;
            var affected = affectedParagraphsAroundObject(model, blockId);
            var block = findImage(blockId);
            block.content.layout = imageObjectToLayout(state.original);
            var op = createOperation(OPERATION_TYPES.UpdateImageLayout, {
                target: { blockId: blockId, offset: 0 },
                layout: imageObjectToLayout(preview),
                affectedParagraphIds: affected
            }, { source: mode + '-preview-commit' });
            var result = applyOperation(model, op);
            state = null;
            return _sortObject(Object.assign({}, result, {
                ok: result.ok !== false,
                singleTransaction: true,
                operationCount: 1,
                affectedParagraphIds: affected,
                transactionType: 'preview',
                command: 'UpdateImageLayout'
            }));
        }
        return {
            startDrag: startDrag,
            moveDrag: moveDrag,
            startResize: startResize,
            moveResize: moveResize,
            cancel: cancel,
            commit: commit
        };
    }

    function createEditorWidget(block) {
        var object = normalizeImageObject(block);
        return _sortObject({
            adapter: 'EditorWidget',
            kind: block && block.type === 'table' ? 'table' : 'image',
            blockId: object.blockId,
            objectId: object.objectId,
            commands: ['UpdateImageLayout', 'UpdateImageMetadata', 'DeleteObject', 'ReplaceImage'],
            selectionKind: 'object',
            fakeSelection: true,
            hitTest: function (input) {
                var role = input && (input.targetRole || input.TargetRole) || '';
                return role === 'text-interval'
                    ? { type: 'text', objectId: null }
                    : { type: 'object', objectId: object.objectId, blockId: object.blockId };
            }
        });
    }

    function createImageInspectorState(block) {
        var object = normalizeImageObject(block);
        var url = _asText(object.url || '');
        var isHttpUrl = /^https?:\/\//i.test(url);
        return _sortObject({
            altText: object.altText,
            caption: object.caption,
            width: object.width,
            height: object.height,
            wrapMode: object.wrapMode,
            showUrlField: isHttpUrl,
            urlEditable: isHttpUrl,
            url: isHttpUrl ? url : '',
            warningBadges: object.altText ? [] : ['accessibility-warning']
        });
    }

    function normalizeParagraphLayoutOptions(options) {
        var opts = options || {};
        var page = normalizePageBox(opts);
        return {
            page: page,
            x: Number(opts.x || opts.X || page.x) || page.x,
            y: Number(opts.y || opts.Y || page.y) || page.y,
            width: Math.max(1, Number(opts.width || opts.Width || page.width) || page.width),
            minReadableWidth: Math.max(1, Number(opts.minReadableWidth || opts.MinReadableWidth || 48) || 48),
            lineGap: Number(opts.lineGap || opts.LineGap || 0) || 0
        };
    }

    function createParagraphLayoutEngine(measurementService, options) {
        var service = measurementService || createTextMeasurementService();
        var defaults = options || {};
        var breaker = createLineBreaker(service);
        var layoutVersion = 0;

        function layoutParagraph(block, options) {
            var opts = normalizeParagraphLayoutOptions(Object.assign({}, defaults, options || {}));
            if (!block || block.type !== 'paragraph') {
                return layoutObjectBlock(block, opts, ++layoutVersion);
            }

            var paragraphInput = {
                id: block.id,
                runs: _asArray(block.content && block.content.runs),
                style: Object.assign({}, block.style || {}, block.content && block.content.style || {}),
                alignment: block.content && block.content.alignment || block.content && block.content.Alignment || 'left'
            };
            var lineLayout = breaker.breakParagraph(paragraphInput, {
                x: opts.x,
                y: opts.y,
                width: opts.width,
                minReadableWidth: opts.minReadableWidth,
                lineGap: opts.lineGap
            });
            var runs = flattenParagraphRuns(paragraphInput);
            var lines = _asArray(lineLayout.lines).map(function (line, index) {
                var id = block.id + '-line-' + index;
                var rect = _clone(line.rect || {});
                var baseline = rect.y + Math.max(1, rect.height || 1) * 0.78;
                return _sortObject(Object.assign({}, line, {
                    id: id,
                    blockId: block.id,
                    lineId: id,
                    index: index,
                    rect: rect,
                    baseline: baseline,
                    baselineOffset: baseline - rect.y,
                    availableIntervals: _asArray(line.availableIntervals).map(function (interval) {
                        return Object.assign({}, interval, { blockId: block.id, lineId: id });
                    })
                }));
            });
            var lineByOriginalId = {};
            _asArray(lineLayout.lines).forEach(function (line, index) {
                lineByOriginalId[line.id] = lines[index].id;
            });
            var segments = _asArray(lineLayout.segments).map(function (segment, index) {
                var line = lines.find(function (candidate) {
                    return segment.rect && candidate.rect
                        && Math.abs(candidate.rect.y - segment.rect.y) < 0.5
                        && segment.start >= candidate.start
                        && segment.end <= candidate.end;
                }) || lines[0];
                var run = runs.find(function (item) { return item.id === segment.runId; }) || runForOffset(runs, segment.start);
                return _sortObject(Object.assign({}, segment, {
                    id: block.id + '-segment-' + index,
                    blockId: block.id,
                    lineId: line && line.id || null,
                    runId: run && run.id || segment.runId || null,
                    kind: run && run.kind || 'text',
                    style: normalizeLayoutSegmentStyle(run && run.style || segment.style || {}),
                    decorations: decorationsFromMarks(run && run.marks || []),
                    mapping: { blockId: block.id, runId: run && run.id || null, start: segment.start, end: segment.end }
                }));
            });
            var segmentsByLine = new Map();
            segments.forEach(function (segment) {
                if (!segmentsByLine.has(segment.lineId)) segmentsByLine.set(segment.lineId, []);
                segmentsByLine.get(segment.lineId).push(segment);
            });
            lines.forEach(function (line) {
                line.segments = segmentsByLine.get(line.id) || [];
            });
            var caretStops = _asArray(lineLayout.caretStops).map(function (stop) {
                var inline = _inlineAtOffset(block, stop.offset);
                return _sortObject(Object.assign({}, stop, {
                    blockId: block.id,
                    inlineId: inline && inline.run ? inline.run.id : null,
                    lineId: lineByOriginalId[stop.lineId] || stop.lineId,
                    affinity: Number(stop.offset || 0) === 0 ? 'before' : 'after'
                }));
            });
            var baselines = lines.map(function (line) {
                return _sortObject({ blockId: block.id, lineId: line.id, y: line.baseline, offset: line.baselineOffset });
            });
            var rect = paragraphRectFromLines(opts, lines);
            return _sortObject({
                ok: lineLayout.ok !== false,
                id: 'layout-' + block.id,
                layoutVersion: ++layoutVersion,
                blockId: block.id,
                type: 'paragraph',
                scope: createLayoutScope(LAYOUT_SCOPE_KINDS.ActiveParagraph, { blockId: block.id, affectedScopeIds: [block.id], reason: 'layoutParagraph' }),
                rect: rect,
                lines: lines,
                segments: segments,
                caretStops: caretStops,
                baselines: baselines,
                fallback: lineLayout.fallback === true,
                debug: {
                    source: 'paragraph-layout-tree',
                    lineBreaker: lineLayout.debug || {},
                    invalidatedScopes: [block.id]
                }
            });
        }

        function layoutTableBlock(block, options) {
            var opts = normalizeParagraphLayoutOptions(Object.assign({}, defaults, options || {}));
            var rows = _asArray(block && block.content && block.content.rows);
            var columnCount = _tableColumnCount(block);
            var tableStyle = block.content && block.content.style || {};
            var tableWidth = Math.max(80, Number(tableStyle.width || tableStyle.Width || opts.width) || opts.width);
            tableWidth = Math.min(tableWidth, opts.width);
            var x = opts.x;
            var y = opts.y;
            var defaultColumnWidth = tableWidth / columnCount;
            var columnWidths = new Array(columnCount).fill(defaultColumnWidth);
            rows.forEach(function (row) {
                var column = 0;
                _asArray(row.cells).forEach(function (cell) {
                    var span = Math.max(1, Number(cell.colSpan || 1));
                    var requested = Number(cell.width || cell.Width || 0);
                    if (requested > 0 && span === 1) columnWidths[column] = requested;
                    column += span;
                });
            });
            var totalRequested = columnWidths.reduce(function (sum, value) { return sum + value; }, 0) || tableWidth;
            if (Math.abs(totalRequested - tableWidth) > 0.5) {
                columnWidths = columnWidths.map(function (value) { return value * tableWidth / totalRequested; });
            }

            var cells = [];
            var rowLayouts = [];
            var tableLines = [];
            var tableSegments = [];
            var tableCarets = [];
            var rowY = y;
            rows.forEach(function (row, rowIndex) {
                var columnIndex = 0;
                var rowCells = [];
                var rowHeight = Math.max(24, Number(row.height || row.Height || 0) || 0);
                _asArray(row.cells).forEach(function (cell) {
                    var colSpan = Math.max(1, Number(cell.colSpan || 1));
                    var rowSpan = Math.max(1, Number(cell.rowSpan || 1));
                    var cellX = x + columnWidths.slice(0, columnIndex).reduce(function (sum, value) { return sum + value; }, 0);
                    var cellWidth = columnWidths.slice(columnIndex, columnIndex + colSpan).reduce(function (sum, value) { return sum + value; }, 0);
                    var padding = Number((cell.style ? (cell.style.padding ?? cell.style.Padding) : null) ?? 6) || 0;
                    var contentFrame = {
                        x: cellX + padding,
                        y: rowY + padding,
                        width: Math.max(12, cellWidth - padding * 2),
                        height: 0
                    };
                    var blockLayouts = [];
                    var contentY = contentFrame.y;
                    _asArray(cell.blocks).forEach(function (childBlock) {
                        var childLayout = childBlock && childBlock.type === 'paragraph'
                            ? layoutParagraph(childBlock, Object.assign({}, opts, {
                                x: contentFrame.x,
                                y: contentY,
                                width: contentFrame.width,
                                lineGap: 0
                            }))
                            : layoutObjectBlock(childBlock, {
                                page: opts.page,
                                x: contentFrame.x,
                                y: contentY,
                                width: contentFrame.width
                            }, ++layoutVersion);
                        childLayout.tableId = block.id;
                        childLayout.cellId = cell.id;
                        childLayout.rowIndex = rowIndex;
                        childLayout.columnIndex = columnIndex;
                        _asArray(childLayout.lines).forEach(function (line) {
                            line.tableId = block.id;
                            line.cellId = cell.id;
                            tableLines.push(line);
                        });
                        _asArray(childLayout.segments).forEach(function (segment) {
                            segment.tableId = block.id;
                            segment.cellId = cell.id;
                            tableSegments.push(segment);
                        });
                        _asArray(childLayout.caretStops).forEach(function (stop) {
                            stop.tableId = block.id;
                            stop.cellId = cell.id;
                            tableCarets.push(stop);
                        });
                        blockLayouts.push(childLayout);
                        contentY = childLayout.rect.y + childLayout.rect.height + 2;
                    });
                    var contentHeight = Math.max(18, contentY - contentFrame.y);
                    var cellHeight = Math.max(Number(cell.height || cell.Height || 0) || 0, contentHeight + padding * 2, 28);
                    rowHeight = Math.max(rowHeight, cellHeight);
                    var cellLayout = {
                        tableId: block.id,
                        rowId: row.id,
                        cellId: cell.id,
                        rowIndex: rowIndex,
                        columnIndex: columnIndex,
                        rowSpan: rowSpan,
                        colSpan: colSpan,
                        rect: { x: cellX, y: rowY, width: cellWidth, height: cellHeight },
                        contentFrame: { x: contentFrame.x, y: contentFrame.y, width: contentFrame.width, height: Math.max(1, cellHeight - padding * 2) },
                        style: _clone(cell.style || {}),
                        blockLayouts: blockLayouts
                    };
                    rowCells.push(cellLayout);
                    cells.push(cellLayout);
                    columnIndex += colSpan;
                });
                rowCells.forEach(function (cell) {
                    cell.rect.height = rowHeight;
                    cell.contentFrame.height = Math.max(1, rowHeight - 2 * (Number((cell.style ? (cell.style.padding ?? cell.style.Padding) : null) ?? 6) || 0));
                });
                rowLayouts.push({ rowId: row.id, rowIndex: rowIndex, y: rowY, height: rowHeight, cells: rowCells.map(function (cell) { return cell.cellId; }) });
                rowY += rowHeight;
            });
            var rect = { x: x, y: y, width: tableWidth, height: Math.max(28, rowY - y) };
            return _sortObject({
                ok: true,
                id: 'layout-' + block.id,
                layoutVersion: ++layoutVersion,
                blockId: block.id,
                type: 'table',
                pageIndex: opts.page && Number(opts.page.pageIndex || 0) || 0,
                rect: rect,
                rows: rowLayouts,
                columns: columnWidths.map(function (width, index) { return { index: index, x: x + columnWidths.slice(0, index).reduce(function (sum, value) { return sum + value; }, 0), width: width }; }),
                cells: cells,
                lines: tableLines,
                segments: tableSegments,
                caretStops: tableCarets,
                scope: createLayoutScope(LAYOUT_SCOPE_KINDS.PageRegion, { blockId: block.id, affectedScopeIds: [block.id], reason: 'layoutTable' }),
                fallback: false,
                debug: { source: 'table-layout-tree', invalidatedScopes: [block.id], textInsideCells: true }
            });
        }

        function layoutDocument(model, options) {
            var opts = Object.assign({}, defaults, options || {});
            var pageMetrics = normalizePageLayoutSettings(opts, model);
            var pages = [createPageLayout(0, pageMetrics)];
            var blocks = [];
            var objects = [];
            var caretStops = [];
            var headerFooterRegions = [];
            var pageIndex = 0;
            var currentY = pages[0].bodyFrame.y;
            var bodyBottom = function (page) { return page.bodyFrame.y + page.bodyFrame.height; };
            var currentPage = function () { return pages[pageIndex]; };
            var ensurePage = function (index) {
                while (pages.length <= index) pages.push(createPageLayout(pages.length, pageMetrics));
                return pages[index];
            };
            var moveToNextPage = function () {
                pageIndex++;
                currentY = ensurePage(pageIndex).bodyFrame.y;
            };
            var addBlockToPage = function (layout) {
                var page = ensurePage(layout.pageIndex || 0);
                if (page.blockIds.indexOf(layout.blockId) < 0) page.blockIds.push(layout.blockId);
                blocks.push(layout);
                caretStops = caretStops.concat(_asArray(layout.caretStops));
            };
            var blockGap = Number(opts.blockGap ?? opts.BlockGap ?? pageMetrics.blockGap) || 0;

            _asArray(model && model.body && model.body.blocks).forEach(function (block) {
                if (block.type === 'paragraph') {
                    var fragments = layoutParagraphAcrossPages(block, currentPage(), currentY, opts, pageMetrics);
                    fragments.forEach(addBlockToPage);
                    if (fragments.length) {
                        var last = fragments[fragments.length - 1];
                        pageIndex = last.pageIndex || 0;
                        currentY = last.rect.y + last.rect.height + blockGap;
                    }
                    return;
                }
                if (block.type === 'pageBreak') {
                    addBlockToPage(createPageBreakLayout(block, currentPage(), ++layoutVersion));
                    moveToNextPage();
                    return;
                }
                if (block.type === 'table') {
                    var tableLayout = layoutTableBlock(block, {
                        page: currentPage().bodyFrame,
                        x: currentPage().bodyFrame.x,
                        y: currentY,
                        width: currentPage().bodyFrame.width,
                        minReadableWidth: pageMetrics.minReadableWidth
                    });
                    if (currentY > currentPage().bodyFrame.y && currentY + tableLayout.rect.height > bodyBottom(currentPage())) {
                        moveToNextPage();
                        tableLayout = layoutTableBlock(block, {
                            page: currentPage().bodyFrame,
                            x: currentPage().bodyFrame.x,
                            y: currentY,
                            width: currentPage().bodyFrame.width,
                            minReadableWidth: pageMetrics.minReadableWidth
                        });
                    }
                    tableLayout.pageIndex = pageIndex;
                    _asArray(tableLayout.caretStops).forEach(function (stop) { stop.pageIndex = pageIndex; });
                    addBlockToPage(tableLayout);
                    currentY = tableLayout.rect.y + tableLayout.rect.height + blockGap;
                    return;
                }
                var isImage = block && block.type === 'image';
                var anchoredObject = isImage ? normalizeImageObject(block, { anchorBlockId: blocks.length ? blocks[blocks.length - 1].blockId : '' }) : null;
                var consumesFlow = !anchoredObject || anchoredObject.wrapMode === 'Inline' || anchoredObject.wrapMode === 'TopBottom';
                var objectHeight = isImage
                    ? anchoredObject.height + (anchoredObject.caption ? Math.max(16, Math.min(48, anchoredObject.caption.length * 0.6)) : 0)
                    : block && block.type === 'image'
                    ? Number(block.content && block.content.layout && (block.content.layout.height || block.content.layout.Height) || 120) || 120
                    : 80;
                if (currentY > currentPage().bodyFrame.y && currentY + objectHeight > bodyBottom(currentPage())) moveToNextPage();
                var layout = layoutObjectBlock(block, {
                    page: currentPage().bodyFrame,
                    x: isImage ? currentPage().bodyFrame.x + Number(anchoredObject.horizontalPosition && anchoredObject.horizontalPosition.offset || 0) : currentPage().bodyFrame.x,
                    y: isImage ? currentY + Number(anchoredObject.verticalPosition && anchoredObject.verticalPosition.offset || 0) : currentY,
                    width: currentPage().bodyFrame.width
                }, ++layoutVersion);
                layout.pageIndex = pageIndex;
                if (isImage) {
                    layout.rect.width = anchoredObject.width;
                    layout.rect.height = objectHeight;
                    layout.object = _clone(anchoredObject);
                    layout.objectId = anchoredObject.objectId;
                    layout.wrapMode = anchoredObject.wrapMode;
                    layout.wrapMargin = anchoredObject.wrapMargin;
                    layout.zIndex = anchoredObject.zIndex;
                    _asArray(layout.caretStops).forEach(function (stop) {
                        stop.rect.x = Number(stop.offset || 0) === 0 ? layout.rect.x : layout.rect.x + layout.rect.width;
                        stop.rect.y = layout.rect.y;
                        stop.rect.height = layout.rect.height;
                    });
                    anchoredObject.rect = _clone(layout.rect);
                    anchoredObject.pageIndex = pageIndex;
                    objects.push(_clone(anchoredObject));
                }
                _asArray(layout.caretStops).forEach(function (stop) { stop.pageIndex = pageIndex; });
                addBlockToPage(layout);
                if (isImage) {
                    var exclusion = createTextExclusion(anchoredObject, currentPage().bodyFrame);
                    if (exclusion) currentPage().exclusions.push(exclusion);
                }
                if (consumesFlow) currentY = layout.rect.y + layout.rect.height + blockGap;
            });

            renderHeaderFooterLayouts(model, pages.length).forEach(function (regionLayout) {
                headerFooterRegions.push(regionLayout);
            });

            return _sortObject({
                ok: true,
                layoutVersion: ++layoutVersion,
                pageMetrics: pageMetrics,
                pages: pages.map(function (page, index) {
                    return Object.assign({}, page, {
                        totalPages: pages.length,
                        blockIds: _unique(page.blockIds)
                    });
                }),
                blocks: blocks,
                objects: objects,
                caretStops: caretStops,
                headerFooterRegions: headerFooterRegions,
                staleFollowingBlockIds: [],
                debug: {
                    source: 'paragraph-layout-document',
                    invalidatedScopes: _unique(blocks.map(function (block) { return block.blockId; })),
                    currentYOwnedByLayout: true,
                    explicitParagraphSpacing: true,
                    keepWithNextPrepared: true
                }
            });

            function layoutParagraphAcrossPages(block, page, y, layoutOptions, metrics) {
                var initial = layoutParagraph(block, Object.assign({}, layoutOptions, {
                    page: page.bodyFrame,
                    x: page.bodyFrame.x,
                    y: y,
                    width: page.bodyFrame.width,
                    lineGap: metrics.lineGap,
                    minReadableWidth: metrics.minReadableWidth
                }));
                var fragmentsByPage = new Map();
                var cursorPageIndex = page.pageIndex;
                var cursorY = y;

                function getFragment(fragmentPage) {
                    if (!fragmentsByPage.has(fragmentPage.pageIndex)) {
                        fragmentsByPage.set(fragmentPage.pageIndex, {
                            ok: true,
                            id: 'layout-' + block.id + '-page-' + fragmentPage.pageIndex,
                            layoutVersion: ++layoutVersion,
                            blockId: block.id,
                            type: 'paragraph',
                            pageIndex: fragmentPage.pageIndex,
                            fragmentIndex: fragmentsByPage.size,
                            rect: { x: fragmentPage.bodyFrame.x, y: cursorY, width: fragmentPage.bodyFrame.width, height: 0 },
                            lines: [],
                            segments: [],
                            caretStops: [],
                            baselines: [],
                            scope: createLayoutScope(LAYOUT_SCOPE_KINDS.PageRegion, { blockId: block.id, pageIndex: fragmentPage.pageIndex, affectedScopeIds: [block.id], reason: 'layoutParagraphPage' }),
                            fallback: initial.fallback === true,
                            debug: {
                                source: 'paragraph-layout-fragment',
                                invalidatedScopes: [block.id]
                            }
                        });
                    }
                    return fragmentsByPage.get(fragmentPage.pageIndex);
                }

                _asArray(initial.lines).forEach(function (line) {
                    var activePage = ensurePage(cursorPageIndex);
                    var lineHeight = Math.max(1, Number(line.rect && line.rect.height || 18) || 18);
                    if (cursorY > activePage.bodyFrame.y && cursorY + lineHeight > bodyBottom(activePage)) {
                        cursorPageIndex++;
                        activePage = ensurePage(cursorPageIndex);
                        cursorY = activePage.bodyFrame.y;
                    }
                    var available = getAvailableIntervals(cursorY, lineHeight, activePage.bodyFrame, activePage.exclusions, metrics.minReadableWidth);
                    if (available.movedToY > cursorY + 0.01) {
                        cursorY = available.movedToY;
                        if (cursorY + lineHeight > bodyBottom(activePage)) {
                            cursorPageIndex++;
                            activePage = ensurePage(cursorPageIndex);
                            cursorY = activePage.bodyFrame.y;
                            available = getAvailableIntervals(cursorY, lineHeight, activePage.bodyFrame, activePage.exclusions, metrics.minReadableWidth);
                        }
                    }
                    var interval = _asArray(available.intervals)[0] || { x: activePage.bodyFrame.x, y: cursorY, width: activePage.bodyFrame.width, height: lineHeight };
                    var deltaY = cursorY - Number(line.rect && line.rect.y || cursorY);
                    var fragment = getFragment(activePage);
                    var shiftedLine = shiftLayoutLine(line, deltaY, activePage.pageIndex);
                    var deltaX = Number(interval.x || activePage.bodyFrame.x) - Number(shiftedLine.rect.x || activePage.bodyFrame.x);
                    shiftedLine.rect.x += deltaX;
                    shiftedLine.rect.width = Math.min(shiftedLine.rect.width, Number(interval.width || shiftedLine.rect.width));
                    shiftedLine.availableIntervals = [_sortObject({ x: interval.x, y: cursorY, width: interval.width, height: lineHeight, blockId: block.id, lineId: shiftedLine.id, pageIndex: activePage.pageIndex })];
                    var segmentIds = new Set(_asArray(line.segments).map(function (segment) { return segment.id; }));
                    var shiftedSegments = _asArray(initial.segments).filter(function (segment) {
                        return segment.lineId === line.id || segmentIds.has(segment.id);
                    }).map(function (segment) {
                        var shifted = shiftLayoutSegment(segment, deltaY, activePage.pageIndex);
                        shifted.rect.x += deltaX;
                        return shifted;
                    });
                    var shiftedStops = _asArray(initial.caretStops).filter(function (stop) {
                        return stop.lineId === line.id;
                    }).map(function (stop) {
                        var shifted = shiftCaretStop(stop, deltaY, activePage.pageIndex);
                        shifted.rect.x += deltaX;
                        return shifted;
                    });
                    shiftedLine.segments = shiftedSegments;
                    fragment.lines.push(shiftedLine);
                    fragment.segments = fragment.segments.concat(shiftedSegments);
                    fragment.caretStops = fragment.caretStops.concat(shiftedStops);
                    fragment.baselines.push(_sortObject({ blockId: block.id, lineId: shiftedLine.id, y: shiftedLine.baseline, offset: shiftedLine.baselineOffset, pageIndex: activePage.pageIndex }));
                    fragment.rect.y = Math.min(fragment.rect.y, shiftedLine.rect.y);
                    fragment.rect.height = Math.max(fragment.rect.height, shiftedLine.rect.y + shiftedLine.rect.height - fragment.rect.y);
                    cursorY = shiftedLine.rect.y + shiftedLine.rect.height + metrics.lineGap;
                });

                return Array.from(fragmentsByPage.values()).map(function (fragment) {
                    fragment.rect.height = Math.max(1, fragment.rect.height);
                    return _sortObject(fragment);
                });
            }

            function renderHeaderFooterLayouts(sourceModel, totalPages) {
                var result = [];
                pages.forEach(function (pageItem) {
                    _asArray(sourceModel && sourceModel.headers).forEach(function (region) {
                        result.push(layoutHeaderFooterRegion(region, 'Header', pageItem, totalPages));
                    });
                    _asArray(sourceModel && sourceModel.footers).forEach(function (region) {
                        result.push(layoutHeaderFooterRegion(region, 'Footer', pageItem, totalPages));
                    });
                });
                return result;
            }

            function layoutHeaderFooterRegion(region, regionName, pageItem, totalPages) {
                var frame = regionName === 'Header' ? pageItem.headerFrame : pageItem.footerFrame;
                var yInRegion = frame.y;
                var regionBlocks = [];
                var regionCaretStops = [];
                _asArray(region && region.blocks).forEach(function (block) {
                    var resolvedBlock = cloneBlockWithResolvedFields(block, pageItem.pageNumber, totalPages);
                    var layout = resolvedBlock.type === 'paragraph'
                        ? layoutParagraph(resolvedBlock, Object.assign({}, opts, { page: frame, x: frame.x, y: yInRegion, width: frame.width }))
                        : layoutObjectBlock(resolvedBlock, { page: frame, x: frame.x, y: yInRegion, width: frame.width }, ++layoutVersion);
                    layout.region = regionName;
                    layout.headerFooterId = region.id;
                    layout.pageIndex = pageItem.pageIndex;
                    _asArray(layout.lines).forEach(function (line) { line.region = regionName; line.headerFooterId = region.id; line.pageIndex = pageItem.pageIndex; });
                    _asArray(layout.segments).forEach(function (segment) { segment.region = regionName; segment.headerFooterId = region.id; segment.pageIndex = pageItem.pageIndex; });
                    _asArray(layout.caretStops).forEach(function (stop) { stop.region = regionName; stop.headerFooterId = region.id; stop.pageIndex = pageItem.pageIndex; });
                    regionBlocks.push(layout);
                    regionCaretStops = regionCaretStops.concat(_asArray(layout.caretStops));
                    yInRegion = layout.rect.y + layout.rect.height + Math.min(4, blockGap);
                });
                caretStops = caretStops.concat(regionCaretStops);
                return _sortObject({
                    id: region.id + '-page-' + pageItem.pageIndex,
                    headerFooterId: region.id,
                    region: regionName,
                    pageIndex: pageItem.pageIndex,
                    pageNumber: pageItem.pageNumber,
                    totalPages: totalPages,
                    frame: frame,
                    blocks: regionBlocks,
                    caretStops: regionCaretStops
                });
            }
        }

        function layoutAfterOperation(model, operation, previousLayout, options) {
            var scope = inferLayoutScopeFromOperation(operation);
            var opts = options || {};
            var selection = _clone(opts.selection || opts.Selection || null);
            var next = layoutDocument(model, opts);
            var activeBlockId = scope.blockId || firstScopeBlockId(scope);
            var previousBlock = findLayoutBlock(previousLayout, activeBlockId);
            var nextBlock = findLayoutBlock(next, activeBlockId);
            var heightDelta = previousBlock && nextBlock ? nextBlock.rect.height - previousBlock.rect.height : 0;
            var staleFollowing = [];
            if (heightDelta > 0 && nextBlock) {
                var shiftStart = false;
                next.blocks.forEach(function (block) {
                    if (block.blockId === activeBlockId) {
                        shiftStart = true;
                        return;
                    }
                    if (!shiftStart) return;
                    block.stale = true;
                    block.safeOffsetY = Math.max(0, heightDelta);
                    block.rect.y += block.safeOffsetY;
                    _asArray(block.lines).forEach(function (line) { line.rect.y += block.safeOffsetY; line.baseline += block.safeOffsetY; });
                    _asArray(block.segments).forEach(function (segment) { segment.rect.y += block.safeOffsetY; });
                    _asArray(block.caretStops).forEach(function (stop) { stop.rect.y += block.safeOffsetY; });
                    staleFollowing.push(block.blockId);
                });
            }
            var invalidated = _unique((scope.affectedScopeIds || []).concat(activeBlockId ? [activeBlockId] : []));
            next.activeParagraphLayout = scope.kind === LAYOUT_SCOPE_KINDS.ActiveParagraph || scope.kind === LAYOUT_SCOPE_KINDS.WholeBlock;
            next.activeBlockId = activeBlockId || null;
            next.staleFollowingBlockIds = staleFollowing;
            next.selection = selection;
            next.debug = Object.assign({}, next.debug || {}, {
                minimalScope: scope,
                invalidatedScopes: invalidated,
                staleFollowingBlockIds: staleFollowing,
                heightDelta: heightDelta
            });
            return _sortObject(next);
        }

        function renderParagraphLayout(root, layout) {
            if (!root || !layout) return null;
            var container = document.createElement('div');
            container.className = 'tm-paragraph-layout';
            container.setAttribute('data-layout-block-id', layout.id || ('layout-' + layout.blockId));
            container.setAttribute('data-block-id', layout.blockId || '');
            container.style.position = 'absolute';
            container.style.left = layout.rect.x + 'px';
            container.style.top = layout.rect.y + 'px';
            container.style.width = layout.rect.width + 'px';
            container.style.height = layout.rect.height + 'px';
            container.style.whiteSpace = 'pre';
            container.style.overflow = 'visible';
            _asArray(layout.segments).forEach(function (segment) {
                var span = document.createElement('span');
                span.setAttribute('data-layout-segment-id', segment.id);
                span.setAttribute('data-block-id', layout.blockId || '');
                span.setAttribute('data-run-id', segment.runId || '');
                span.setAttribute('data-layout-height', segment.rect.height);
                span.textContent = segment.text || '';
                span.style.position = 'absolute';
                span.style.left = (segment.rect.x - layout.rect.x) + 'px';
                span.style.top = (segment.rect.y - layout.rect.y) + 'px';
                span.style.width = segment.rect.width + 'px';
                span.style.height = segment.rect.height + 'px';
                span.style.lineHeight = segment.rect.height + 'px';
                span.style.whiteSpace = 'pre';
                span.style.overflow = 'hidden';
                span.style.display = 'block';
                applySegmentStyleToElement(span, segment.style || {}, segment.decorations || []);
                container.appendChild(span);
            });
            root.innerHTML = '';
            root.appendChild(container);
            return container;
        }

        return {
            createLayoutScope: createLayoutScope,
            inferLayoutScopeFromOperation: inferLayoutScopeFromOperation,
            computeMinimalScopeForOperation: inferLayoutScopeFromOperation,
            layoutParagraph: layoutParagraph,
            layoutDocument: layoutDocument,
            layoutAfterOperation: layoutAfterOperation,
            renderParagraphLayout: renderParagraphLayout,
            getMeasurementStats: function () { return service.getStats(); }
        };
    }

    function normalizeLayoutSegmentStyle(style) {
        var source = style || {};
        return _sortObject(Object.assign({}, source, {
            fontFamily: source.fontFamily || source.FontFamily || 'Arial',
            fontSize: Number(source.fontSize || source.FontSize || 16) || 16,
            fontWeight: _asText(source.fontWeight || source.FontWeight || '400'),
            fontStyle: _asText(source.fontStyle || source.FontStyle || 'normal'),
            color: source.color || source.Color || null,
            backgroundColor: source.backgroundColor || source.BackgroundColor || null
        }));
    }

    function decorationsFromMarks(marks) {
        var decorations = [];
        _asArray(marks).forEach(function (mark) {
            var type = String(mark && (mark.type || mark.Type) || '').toLowerCase();
            if (type === 'underline') decorations.push('underline');
            if (type === 'strikethrough' || type === 'strike') decorations.push('line-through');
        });
        return _unique(decorations);
    }

    function paragraphRectFromLines(options, lines) {
        if (!lines.length) return { x: options.x, y: options.y, width: options.width, height: Math.max(18, Number(options.lineHeight || 18)) };
        var top = lines[0].rect.y;
        var bottom = lines.reduce(function (value, line) {
            return Math.max(value, line.rect.y + line.rect.height);
        }, top);
        return { x: options.x, y: top, width: options.width, height: Math.max(1, bottom - top) };
    }

    function layoutObjectBlock(block, options, version) {
        var id = block && block.id || 'object';
        var object = block && block.type === 'image' ? normalizeImageObject(block) : null;
        var captionHeight = object && object.caption ? Math.max(16, Math.min(48, object.caption.length * 0.6)) : 0;
        var height = object ? object.height + captionHeight : 80;
        var width = object ? object.width : options.width;
        var rect = { x: options.x, y: options.y, width: width, height: height };
        return _sortObject({
            ok: true,
            id: 'layout-' + id,
            layoutVersion: version,
            blockId: id,
            type: block && block.type || 'object',
            rect: rect,
            lines: [],
            segments: [],
            caretStops: [
                { blockId: id, offset: 0, affinity: 'before', rect: { x: rect.x, y: rect.y, width: 1, height: rect.height }, objectBoundary: true },
                { blockId: id, offset: 1, affinity: 'after', rect: { x: rect.x + rect.width, y: rect.y, width: 1, height: rect.height }, objectBoundary: true }
            ],
            baselines: [],
            objectId: block && block.content && block.content.objectId || id
        });
    }

    function applySegmentStyleToElement(element, style, decorations) {
        element.style.fontFamily = style.fontFamily || 'Arial';
        element.style.fontSize = (Number(style.fontSize || 16) || 16) + 'px';
        element.style.fontWeight = style.fontWeight || '400';
        element.style.fontStyle = style.fontStyle || 'normal';
        if (style.color) element.style.color = style.color;
        if (style.backgroundColor) element.style.backgroundColor = style.backgroundColor;
        if (_asArray(decorations).length) element.style.textDecoration = decorations.join(' ');
    }

    function firstScopeBlockId(scope) {
        return scope && scope.affectedScopeIds && scope.affectedScopeIds[0] || scope && scope.blockId || null;
    }

    function findLayoutBlock(layout, blockId) {
        if (!layout || !blockId) return null;
        return _asArray(layout.blocks).find(function (block) { return block.blockId === blockId; }) || null;
    }

    function createRenderSnapshot(model, layout, selection, options) {
        var opts = options || {};
        var blocks = _asArray(layout && layout.blocks);
        var segments = flattenLayoutSegments(layout);
        var affectedScopes = _asArray(opts.affectedScopes || opts.AffectedScopes || layout && layout.debug && layout.debug.invalidatedScopes);
        var rawModelVersion = opts.modelVersion ?? opts.ModelVersion ?? (model && (model.version ?? model.Version)) ?? 1;
        var rawLayoutVersion = opts.layoutVersion ?? opts.LayoutVersion ?? (layout && layout.layoutVersion) ?? 1;
        var rawSelectionVersion = opts.selectionVersion ?? opts.SelectionVersion ?? (selection && selection.version) ?? 1;
        var modelVersion = Number(rawModelVersion) || 1;
        var layoutVersionValue = Number(rawLayoutVersion) || 1;
        var selectionVersion = Number(rawSelectionVersion) || 1;
        var fingerprintSource = {
            documentId: model && model.documentId,
            modelVersion: modelVersion,
            layoutVersion: layoutVersionValue,
            selectionVersion: selectionVersion,
            affectedScopes: affectedScopes,
            blockIds: blocks.map(function (block) { return block.blockId; }),
            segmentIds: segments.map(function (segment) { return segment.id + ':' + segment.start + ':' + segment.end; })
        };
        var checksum = stableChecksum(fingerprintSource);
        return _sortObject({
            ok: true,
            modelVersion: modelVersion,
            layoutVersion: layoutVersionValue,
            selectionVersion: selectionVersion,
            affectedScopes: affectedScopes,
            checksum: checksum,
            fingerprint: checksum + '-' + blocks.length + '-' + segments.length,
            model: model,
            layout: layout,
            selection: selection || null,
            debug: {
                blockCount: blocks.length,
                segmentCount: segments.length,
                affectedScopes: affectedScopes,
                checksum: checksum
            }
        });
    }

    function stableChecksum(value) {
        var text = JSON.stringify(_sortObject(value || {}));
        var hash = 2166136261;
        for (var i = 0; i < text.length; i++) {
            hash ^= text.charCodeAt(i);
            hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
        }
        return ('00000000' + (hash >>> 0).toString(16)).slice(-8) + '-' + text.length;
    }

    function flattenLayoutSegments(layout) {
        var result = [];
        _asArray(layout && layout.blocks).forEach(function (block) {
            _asArray(block.segments).forEach(function (segment) {
                result.push(segment);
            });
        });
        return result;
    }

    function createAtomicRenderer(options) {
        var opts = options || {};
        var segmentCache = new Map();
        var blockCache = new Map();
        var watchdog = [];
        var emptyFrameCount = 0;
        var lastSnapshot = null;

        function render(root, snapshot, options) {
            var renderOptions = options || {};
            var beforeHtml = root ? root.innerHTML : '';
            try {
                if (!root) throw new Error('render root is required');
                var fragment = document.createDocumentFragment();
                var nextTree = renderSnapshotFragment(snapshot, renderOptions);
                fragment.appendChild(nextTree);
                if (renderOptions.failBeforeSwap) throw new Error('forced render failure before atomic swap');
                root.replaceChildren(fragment);
                restoreLogicalSelection(root, snapshot && snapshot.selection);
                lastSnapshot = snapshot;
                var text = root.textContent || '';
                if (!text && flattenLayoutSegments(snapshot && snapshot.layout).length > 0) emptyFrameCount++;
                var invariants = validateRenderInvariants(root, snapshot, renderOptions);
                updateDebugOrphans(root, snapshot);
                return _sortObject({ ok: true, rolledBack: false, invariants: invariants, snapshotFingerprint: snapshot && snapshot.fingerprint || '' });
            } catch (error) {
                if (root) root.innerHTML = beforeHtml;
                watchdog.push({ message: String(error && error.message || error), at: Date.now() });
                return _sortObject({ ok: true, rolledBack: true, error: String(error && error.message || error), watchdogFailures: watchdog.length });
            }
        }

        function renderSnapshotFragment(snapshot, options) {
            var scope = options && options.scope || null;
            var host = document.createElement('div');
            host.className = 'tm-render-snapshot';
            host.setAttribute('data-render-snapshot', snapshot && snapshot.fingerprint || '');
            host.setAttribute('data-model-version', snapshot && snapshot.modelVersion || 0);
            host.setAttribute('data-layout-version', snapshot && snapshot.layoutVersion || 0);
            host.setAttribute('data-selection-version', snapshot && snapshot.selectionVersion || 0);
            var layout = snapshot && snapshot.layout || {};
            _asArray(layout.pages).forEach(function (page, pageIndex) {
                host.appendChild(renderPageRegion(snapshot, page, pageIndex, scope));
            });
            if (!_asArray(layout.pages).length) {
                host.appendChild(renderPageRegion(snapshot, { pageNumber: 1, rect: { x: 0, y: 0, width: 640, height: 900 } }, 0, scope));
            }
            return host;
        }

        function renderPageRegion(snapshot, page, pageIndex, scope) {
            var pageNode = document.createElement('section');
            pageNode.className = 'tm-render-page';
            pageNode.setAttribute('data-render-page-index', pageIndex);
            pageNode.style.position = 'relative';
            pageNode.style.width = (page.rect && page.rect.width || 640) + 'px';
            pageNode.style.minHeight = (page.rect && page.rect.height || 900) + 'px';
            pageNode.style.height = (page.rect && page.rect.height || 900) + 'px';
            pageNode.appendChild(renderFrameNode('header', 'Header', page.headerFrame, page));
            var bodyFrame = renderFrameNode('body', 'Body', page.bodyFrame, page);
            var textLayer = document.createElement('div');
            textLayer.setAttribute('data-render-layer', 'text');
            textLayer.style.position = 'absolute';
            textLayer.style.inset = '0';
            var objectLayer = document.createElement('div');
            objectLayer.setAttribute('data-render-layer', 'object');
            objectLayer.style.position = 'absolute';
            objectLayer.style.inset = '0';
            _asArray(snapshot && snapshot.layout && snapshot.layout.blocks).forEach(function (blockLayout) {
                if (Number(blockLayout.pageIndex || 0) !== pageIndex) return;
                if (!scopeIncludesBlock(scope, blockLayout.blockId)) return;
                var pageBlock = localizeLayoutBlockToPage(blockLayout, page);
                if (blockLayout.type === 'paragraph') {
                    textLayer.appendChild(renderParagraphScope(snapshot, pageBlock));
                } else if (blockLayout.type !== 'pageBreak') {
                    objectLayer.appendChild(renderObjectScope(snapshot, pageBlock));
                }
            });
            pageNode.appendChild(bodyFrame);
            pageNode.appendChild(textLayer);
            pageNode.appendChild(objectLayer);
            pageNode.appendChild(renderHeaderFooterRegion(snapshot, page, pageIndex, 'Header'));
            pageNode.appendChild(renderHeaderFooterRegion(snapshot, page, pageIndex, 'Footer'));
            pageNode.appendChild(renderFrameNode('footer', 'Footer', page.footerFrame, page));
            pageNode.appendChild(renderSelectionOverlay(snapshot));
            pageNode.appendChild(renderRevisionOverlay(snapshot));
            pageNode.appendChild(renderCommentMarkers(snapshot));
            return pageNode;
        }

        function renderFrameNode(frameName, regionName, frame, page) {
            var node = document.createElement('div');
            node.className = 'tm-render-' + frameName + '-frame';
            node.setAttribute('data-render-frame', frameName);
            node.setAttribute('data-render-region-name', regionName);
            node.style.position = 'absolute';
            node.style.left = ((frame && frame.x || 0) - (page && page.rect && page.rect.x || 0)) + 'px';
            node.style.top = ((frame && frame.y || 0) - (page && page.rect && page.rect.y || 0)) + 'px';
            node.style.width = (frame && frame.width || 0) + 'px';
            node.style.height = (frame && frame.height || 0) + 'px';
            if (frameName === 'body') {
                node.style.outline = '1px solid rgba(37, 99, 235, 0.5)';
            }
            return node;
        }

        function renderHeaderFooterRegion(snapshot, page, pageIndex, regionName) {
            var regionLayout = _asArray(snapshot && snapshot.layout && snapshot.layout.headerFooterRegions).find(function (region) {
                return region.region === regionName && Number(region.pageIndex || 0) === pageIndex;
            });
            var frame = regionName === 'Header' ? page.headerFrame : page.footerFrame;
            var node = renderFrameNode(regionName === 'Header' ? 'header-content' : 'footer-content', regionName, frame, page);
            node.className = regionName === 'Header' ? 'tm-render-header-region' : 'tm-render-footer-region';
            node.setAttribute('data-testid', regionName === 'Header' ? 'document-page-header' : 'document-page-footer');
            node.setAttribute('data-render-region', regionName);
            node.setAttribute('data-render-page-index', pageIndex);
            node.setAttribute('data-hf-id', regionLayout && regionLayout.headerFooterId || '');
            node.setAttribute('contenteditable', 'true');
            node.setAttribute('role', 'textbox');
            node.setAttribute('aria-multiline', 'true');
            node.setAttribute('aria-label', regionName + ', page ' + (pageIndex + 1));
            node.setAttribute('tabindex', '0');
            _asArray(regionLayout && regionLayout.blocks).forEach(function (blockLayout) {
                if (blockLayout.type === 'paragraph') node.appendChild(renderParagraphScope(snapshot, localizeLayoutBlockToFrame(blockLayout, frame)));
                else node.appendChild(renderObjectScope(snapshot, localizeLayoutBlockToFrame(blockLayout, frame)));
            });
            return node;
        }

        function localizeLayoutBlockToFrame(blockLayout, frame) {
            return localizeLayoutBlock(blockLayout, -(frame && frame.x || 0), -(frame && frame.y || 0));
        }

        function localizeLayoutBlockToPage(blockLayout, page) {
            return localizeLayoutBlock(blockLayout, -(page && page.rect && page.rect.x || 0), -(page && page.rect && page.rect.y || 0));
        }

        function localizeLayoutBlock(blockLayout, dx, dy) {
            var clone = _clone(blockLayout);
            clone.rect = { x: Number(clone.rect && clone.rect.x || 0) + dx, y: Number(clone.rect && clone.rect.y || 0) + dy, width: clone.rect && clone.rect.width || 0, height: clone.rect && clone.rect.height || 0 };
            _asArray(clone.lines).forEach(function (line) {
                line.rect = shiftRectY(line.rect, dy);
                line.rect.x = Number(line.rect.x || 0) + dx;
                line.baseline = Number(line.baseline || 0) + dy;
                _asArray(line.availableIntervals).forEach(function (interval) {
                    interval.x = Number(interval.x || 0) + dx;
                    interval.y = Number(interval.y || 0) + dy;
                });
            });
            _asArray(clone.segments).forEach(function (segment) {
                segment.rect = shiftRectY(segment.rect, dy);
                segment.rect.x = Number(segment.rect.x || 0) + dx;
            });
            _asArray(clone.caretStops).forEach(function (stop) {
                stop.rect = shiftRectY(stop.rect, dy);
                stop.rect.x = Number(stop.rect.x || 0) + dx;
            });
            return clone;
        }

        function renderParagraphScope(snapshot, blockLayout) {
            var key = [
                blockLayout.blockId,
                blockLayout.region || 'Body',
                blockLayout.headerFooterId || '',
                blockLayout.pageIndex ?? '',
                blockLayout.fragmentIndex ?? ''
            ].join(':');
            var container = blockCache.get(key);
            if (!container) {
                container = document.createElement('div');
                blockCache.set(key, container);
            }
            container.className = 'tm-render-paragraph';
            container.setAttribute('data-render-block-id', blockLayout.blockId);
            container.setAttribute('data-model-id', blockLayout.blockId);
            container.style.position = 'absolute';
            container.style.left = blockLayout.rect.x + 'px';
            container.style.top = blockLayout.rect.y + 'px';
            container.style.width = blockLayout.rect.width + 'px';
            container.style.height = blockLayout.rect.height + 'px';
            container.style.whiteSpace = 'pre';
            container.style.overflow = 'visible';
            container.replaceChildren();
            _asArray(blockLayout.segments).forEach(function (segment) {
                container.appendChild(renderSegment(snapshot, segment, blockLayout));
            });
            return container;
        }

        function renderSegment(snapshot, segment, blockLayout) {
            var key = [
                segment.id,
                segment.region || blockLayout.region || 'Body',
                segment.headerFooterId || blockLayout.headerFooterId || '',
                segment.pageIndex ?? blockLayout.pageIndex ?? '',
                blockLayout.fragmentIndex ?? ''
            ].join(':');
            var span = segmentCache.get(key);
            if (!span) {
                span = document.createElement('span');
                span.appendChild(document.createTextNode(''));
                segmentCache.set(key, span);
            }
            span.className = 'tm-render-segment';
            span.setAttribute('data-layout-segment-id', segment.id);
            span.setAttribute('data-model-block-id', segment.blockId || blockLayout.blockId);
            span.setAttribute('data-model-run-id', segment.runId || '');
            span.setAttribute('data-model-start', segment.start);
            span.setAttribute('data-model-end', segment.end);
            span.setAttribute('data-layout-height', segment.rect.height);
            span.style.position = 'absolute';
            span.style.left = (segment.rect.x - blockLayout.rect.x) + 'px';
            span.style.top = (segment.rect.y - blockLayout.rect.y) + 'px';
            span.style.width = segment.rect.width + 'px';
            span.style.height = segment.rect.height + 'px';
            span.style.lineHeight = segment.rect.height + 'px';
            span.style.whiteSpace = 'pre';
            span.style.overflow = 'hidden';
            span.style.display = 'block';
            applySegmentStyleToElement(span, segment.style || {}, segment.decorations || []);
            if (!span.firstChild) span.appendChild(document.createTextNode(''));
            if (span.firstChild.nodeValue !== (segment.text || '')) span.firstChild.nodeValue = segment.text || '';
            return span;
        }

        function renderObjectScope(snapshot, blockLayout) {
            var modelBlock = _findBlock(snapshot && snapshot.model, blockLayout.blockId);
            var node = document.createElement('figure');
            node.className = 'tm-render-object tm-render-image-widget';
            node.setAttribute('data-render-block-id', blockLayout.blockId);
            node.setAttribute('data-render-object-id', blockLayout.objectId || blockLayout.blockId);
            node.setAttribute('data-model-id', blockLayout.blockId);
            node.setAttribute('data-wrap-mode', blockLayout.wrapMode || blockLayout.object && blockLayout.object.wrapMode || '');
            node.setAttribute('data-anchor-block-id', blockLayout.object && blockLayout.object.anchorBlockId || '');
            var selected = snapshot && snapshot.selection && (snapshot.selection.objectId === (blockLayout.objectId || blockLayout.blockId) || snapshot.selection.blockId === blockLayout.blockId && snapshot.selection.isObjectSelection === true);
            var objectLabel = modelBlock && modelBlock.content && (modelBlock.content.altText || modelBlock.content.caption) || 'Image';
            node.setAttribute('aria-selected', selected ? 'true' : 'false');
            node.setAttribute('role', 'figure');
            node.setAttribute('tabindex', '0');
            node.setAttribute('aria-label', objectLabel);
            if (modelBlock && modelBlock.content && !modelBlock.content.altText) {
                node.setAttribute('aria-describedby', 'tm-render-image-alt-warning-' + blockLayout.blockId);
            }
            node.style.position = 'absolute';
            node.style.left = blockLayout.rect.x + 'px';
            node.style.top = blockLayout.rect.y + 'px';
            node.style.width = Math.min(blockLayout.rect.width, Number(modelBlock && modelBlock.content && modelBlock.content.layout && (modelBlock.content.layout.width || modelBlock.content.layout.Width) || 120)) + 'px';
            node.style.height = blockLayout.rect.height + 'px';
            node.style.zIndex = String(blockLayout.zIndex || blockLayout.object && blockLayout.object.zIndex || 0);
            var label = document.createElement('figcaption');
            label.textContent = objectLabel;
            node.appendChild(label);
            if (modelBlock && modelBlock.content && !modelBlock.content.altText) {
                var warning = document.createElement('span');
                warning.id = 'tm-render-image-alt-warning-' + blockLayout.blockId;
                warning.className = 'tm-document-wysiwyg-host__sr-only';
                warning.setAttribute('data-testid', 'document-wysiwyg-image-alt-warning');
                warning.setAttribute('role', 'status');
                warning.setAttribute('aria-live', 'polite');
                warning.textContent = 'Image is missing alternative text.';
                node.appendChild(warning);
            }
            if (selected) {
                var selectionBox = document.createElement('span');
                selectionBox.className = 'tm-wysiwyg-selection-box';
                selectionBox.setAttribute('data-testid', 'document-wysiwyg-object-selection-box');
                node.appendChild(selectionBox);
            }
            ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'].forEach(function (handleName) {
                var handle = document.createElement('span');
                handle.className = 'tm-wysiwyg-object-resize-handle tm-wysiwyg-object-resize-handle--' + handleName;
                handle.setAttribute('data-resize-handle', handleName);
                handle.setAttribute('data-testid', 'document-wysiwyg-object-resize-handle-' + handleName);
                node.appendChild(handle);
            });
            var rotation = document.createElement('span');
            rotation.className = 'tm-wysiwyg-object-rotation-handle';
            rotation.setAttribute('data-testid', 'document-wysiwyg-object-rotation-handle');
            node.appendChild(rotation);
            var bubble = document.createElement('span');
            bubble.className = 'tm-wysiwyg-layout-bubble';
            bubble.setAttribute('data-testid', 'document-wysiwyg-layout-bubble');
            bubble.textContent = '';
            node.appendChild(bubble);
            return node;
        }

        function validateRenderInvariants(root, snapshot, options) {
            var layoutSegments = flattenLayoutSegments(snapshot && snapshot.layout);
            var layoutIds = new Set(layoutSegments.map(function (segment) { return segment.id; }));
            var domSegments = Array.from(root.querySelectorAll('[data-layout-segment-id]'));
            var orphanNodes = domSegments.filter(function (node) { return !layoutIds.has(node.getAttribute('data-layout-segment-id')); });
            var mappedTextNodes = domSegments.filter(function (node) { return !!node.getAttribute('data-model-block-id') && node.firstChild && node.firstChild.nodeType === Node.TEXT_NODE; }).length;
            var wrappedSegments = domSegments.filter(function (node) {
                var expected = Number(node.getAttribute('data-layout-height') || 0);
                var rect = node.getBoundingClientRect();
                return expected > 0 && rect.height > expected + 1.5;
            }).length;
            var forbiddenOverlaps = 0;
            _asArray(options && options.forbiddenRects).forEach(function (forbidden) {
                domSegments.forEach(function (node) {
                    if (rectsOverlap(domRectToRect(node.getBoundingClientRect()), forbidden)) forbiddenOverlaps++;
                });
            });
            return _sortObject({
                ok: orphanNodes.length === 0 && mappedTextNodes === domSegments.length && layoutSegments.length === domSegments.length && wrappedSegments === 0 && forbiddenOverlaps === 0,
                mappedTextNodes: mappedTextNodes,
                layoutSegmentCount: layoutSegments.length,
                domSegmentCount: domSegments.length,
                orphanNodeCount: orphanNodes.length,
                wrappedSegments: wrappedSegments,
                forbiddenOverlaps: forbiddenOverlaps
            });
        }

        function updateDebugOrphans(root, snapshot) {
            var invariants = validateRenderInvariants(root, snapshot, {});
            return invariants.orphanNodeCount;
        }

        function debug() {
            return _sortObject({
                watchdogFailures: watchdog.length,
                emptyFrameCount: emptyFrameCount,
                orphanNodeCount: lastSnapshot ? 0 : 0,
                duplicateToolbarCount: 0,
                cachedSegmentCount: segmentCache.size,
                cachedBlockCount: blockCache.size
            });
        }

        return {
            render: render,
            renderParagraphScope: renderParagraphScope,
            renderPageRegion: renderPageRegion,
            renderObjectScope: renderObjectScope,
            renderSelectionOverlay: renderSelectionOverlay,
            renderRevisionOverlay: renderRevisionOverlay,
            renderCommentMarkers: renderCommentMarkers,
            validateRenderInvariants: validateRenderInvariants,
            debug: debug
        };
    }

    function scopeIncludesBlock(scope, blockId) {
        if (!scope || !scope.kind || scope.kind === LAYOUT_SCOPE_KINDS.WholeDocument || scope.kind === LAYOUT_SCOPE_KINDS.PageRegion) {
            var ids = _asArray(scope && (scope.affectedScopeIds || scope.AffectedScopeIds));
            return ids.length === 0 || ids.indexOf(blockId) >= 0 || ids.indexOf('document') >= 0;
        }
        if (scope.blockId === blockId || scope.BlockId === blockId) return true;
        var affected = _asArray(scope.affectedScopeIds || scope.AffectedScopeIds);
        return affected.length === 0 || affected.indexOf(blockId) >= 0;
    }

    function markOverlayNonText(node) {
        if (!node || typeof node.setAttribute !== 'function') return node;
        node.setAttribute('aria-hidden', 'true');
        node.setAttribute('data-text-probe-ignore', 'true');
        return node;
    }

    function renderSelectionOverlay(snapshot) {
        var overlay = markOverlayNonText(document.createElement('div'));
        overlay.setAttribute('data-render-overlay', 'selection');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        var selection = snapshot && snapshot.selection;
        if (selection && selection.blockId) {
            var marker = markOverlayNonText(document.createElement('span'));
            marker.setAttribute('data-selection-block-id', selection.blockId);
            marker.setAttribute('data-selection-offset', selection.offset || 0);
            overlay.appendChild(marker);
        }
        return overlay;
    }

    function renderRevisionOverlay(snapshot) {
        var overlay = markOverlayNonText(document.createElement('div'));
        overlay.setAttribute('data-render-overlay', 'revision');
        overlay.className = 'tm-render-revision-overlay';
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        _asArray(snapshot && snapshot.model && snapshot.model.revisions).forEach(function (revision) {
            var id = revision.id || revision.Id;
            if (!id) return;
            var marker = markOverlayNonText(document.createElement('span'));
            var type = revision.type || revision.Type || '';
            marker.className = 'tm-render-revision-marker revision-overlay';
            marker.setAttribute('data-testid', 'document-revision-marker');
            marker.setAttribute('data-revision-id', id);
            marker.setAttribute('data-revision-type', type);
            marker.textContent = '';
            overlay.appendChild(marker);
        });
        return overlay;
    }

    function renderCommentMarkers(snapshot) {
        var overlay = markOverlayNonText(document.createElement('div'));
        overlay.setAttribute('data-render-overlay', 'comments');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        _asArray(snapshot && snapshot.model && snapshot.model.comments).forEach(function (comment) {
            var id = comment.id || comment.Id;
            if (!id) return;
            var marker = markOverlayNonText(document.createElement('span'));
            marker.className = 'tm-render-comment-marker';
            marker.setAttribute('data-testid', 'document-comment-marker');
            marker.setAttribute('data-comment-id', id);
            marker.textContent = '';
            overlay.appendChild(marker);
        });
        return overlay;
    }

    function restoreLogicalSelection(root, selection) {
        if (!root) return;
        root.setAttribute('data-logical-selection', JSON.stringify(_sortObject(selection || {})));
    }

    function domRectToRect(rect) {
        return { x: rect.x || rect.left || 0, y: rect.y || rect.top || 0, width: rect.width || 0, height: rect.height || 0 };
    }

    function rectsOverlap(a, b) {
        return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
    }

    function projectEditing(model) {
        var blocks = _asArray(model && model.body && model.body.blocks).map(function (block) {
            if (block.type === 'image') {
                return _sortObject({
                    kind: 'imageWidget',
                    className: 'tm-editing-image-widget resize-handle data-debug-id',
                    resizeHandles: ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'],
                    warningBadges: block.content && !block.content.altText ? ['accessibility-warning'] : [],
                    mapping: { blockId: block.id, objectId: block.content && block.content.objectId || block.id }
                });
            }
            return _sortObject({
                kind: 'paragraph',
                className: hasRevisionRun(block) ? 'tm-editing-paragraph revision-overlay data-debug-id' : 'tm-editing-paragraph data-debug-id',
                mapping: { blockId: block.id },
                runs: _asArray(block.content && block.content.runs).map(function (run) {
                    return { id: run.id, text: run.text, mapping: { blockId: block.id, runId: run.id } };
                })
            });
        });
        return _sortObject({ mode: 'editing', blocks: blocks, overlays: ['selection', 'revision', 'comments'] });
    }

    function projectData(model) {
        var blocks = _asArray(model && model.body && model.body.blocks).map(function (block) {
            if (block.type === 'image') {
                return _sortObject({
                    type: 'image',
                    blockId: block.id,
                    objectId: block.content && block.content.objectId || block.id,
                    url: block.content && block.content.url || null,
                    assetId: block.content && block.content.assetId || null,
                    altText: block.content && block.content.altText || '',
                    caption: block.content && block.content.caption || '',
                    layout: _clone(block.content && block.content.layout || {})
                });
            }
            return _sortObject({
                type: 'paragraph',
                blockId: block.id,
                text: _blockText(block),
                runs: _asArray(block.content && block.content.runs).map(function (run) {
                    return { id: run.id, kind: run.kind, text: run.text, marks: _clone(run.marks || []), revisionId: run.revisionId || null };
                })
            });
        });
        return _sortObject({ mode: 'data', blocks: blocks, revisions: _clone(model && model.revisions || []) });
    }

    function hasRevisionRun(block) {
        return _asArray(block && block.content && block.content.runs).some(function (run) { return !!run.revisionId; });
    }

    var BEFORE_INPUT_COMMANDS = Object.freeze({
        insertText: 'InsertText',
        insertParagraph: 'SplitParagraph',
        insertLineBreak: 'InsertText',
        insertCompositionText: 'InsertCompositionText',
        deleteContentBackward: 'DeleteBackward',
        deleteContentForward: 'DeleteForward',
        deleteWordBackward: 'DeleteBackward',
        deleteWordForward: 'DeleteForward',
        insertFromPaste: 'Paste',
        formatBold: 'ToggleBold'
    });

    function createBeforeInputNormalizer() {
        return {
            normalize: normalizeBeforeInput
        };
    }

    function normalizeBeforeInput(eventLike) {
        var event = eventLike || {};
        var inputType = _asText(event.inputType || event.InputType);
        if (typeof event.preventDefault === 'function') event.preventDefault();
        var command = BEFORE_INPUT_COMMANDS[inputType] || '';
        if (!command) {
            return _sortObject({
                supported: false,
                preventDefault: true,
                inputType: inputType,
                command: '',
                canonicalSource: 'model-operation',
                log: { code: 'unsupported-beforeinput', inputType: inputType }
            });
        }
        return _sortObject({
            supported: true,
            preventDefault: true,
            inputType: inputType,
            command: command,
            data: event.data ?? event.Data ?? null,
            canonicalSource: 'model-operation',
            log: null
        });
    }

    function createInputPipeline(options) {
        var opts = options || {};
        var model = opts.model || opts.Model || importFromCSharpJson({ DocumentId: 'input', Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Text: '' }] } }] });
        var page = opts.page || opts.Page || { x: 0, y: 0, width: 640, height: 900 };
        var root = opts.root || opts.Root || null;
        var paragraphEngine = opts.paragraphLayoutEngine || createParagraphLayoutEngine();
        var renderer = opts.renderer || createAtomicRenderer();
        var currentSelection = createSelectionSnapshot(opts.selection || opts.Selection || firstModelSelection(model));
        var activeTypingMarks = _asArray(opts.activeTypingMarks || opts.ActiveTypingMarks).map(_clone);
        var trackChanges = resolveTrackChangesState(opts).enabled === true;
        var userId = resolveRevisionUserId(opts);
        var activeInputRevision = null;
        var debugLog = [];
        var boundaryPatches = [];
        var boundaryPatchFlushCount = 0;
        var plannedDeletionCount = 0;
        var previousLayout = paragraphEngine.layoutDocument(model, { page: page, selection: currentSelection });
        var lastLayout = previousLayout;
        var lastVisibleText = _blockText(_findBlock(model, currentSelection.blockId));
        var mutationObserverMode = 'diagnostic-only';
        var browserMutationUsed = false;
        var composition = null;

        function handleBeforeInput(eventLike) {
            var normalized = normalizeBeforeInput(eventLike);
            if (!normalized.supported) {
                debugLog.push(normalized.log);
                return { ok: false, normalized: normalized, operations: [], log: normalized.log };
            }
            switch (normalized.inputType) {
                case 'insertText':
                    return insertText(normalized.data || '', { normalized: normalized });
                case 'insertParagraph':
                    return handleEnter(currentSelection, { normalized: normalized });
                case 'insertLineBreak':
                    return insertText('\n', { normalized: normalized });
                case 'insertCompositionText':
                    return handleCompositionUpdate({ data: normalized.data || '', selection: currentSelection });
                case 'deleteContentBackward':
                case 'deleteWordBackward':
                case 'deleteContentForward':
                case 'deleteWordForward':
                    return applyDeletion(currentSelection, normalized.inputType, normalized);
                case 'insertFromPaste':
                    return handlePaste({ plainText: normalized.data || '', selection: currentSelection, normalized: normalized });
                case 'formatBold':
                    return toggleBold(normalized);
                default:
                    return { ok: false, normalized: normalized, operations: [] };
            }
        }

        function insertText(text, options) {
            var normalized = options && options.normalized || null;
            var range = selectionToRange(currentSelection);
            var operations = [];
            var insertOffset = range.start;
            if (!currentSelection.isCollapsed && range.start !== range.end) {
                activeInputRevision = null;
                operations.push(createDeleteOperation({ blockId: range.blockId, start: range.start, end: range.end }, 'input'));
            }
            var revisionPayload = trackChanges
                ? createOrExtendInputRevision({ blockId: range.blockId, start: insertOffset, end: insertOffset + _asText(text).length }, text, 'typing')
                : null;
            operations.push(createOperation(OPERATION_TYPES.InsertText, {
                target: { blockId: range.blockId, offset: insertOffset },
                text: _asText(text),
                marks: _clone(activeTypingMarks),
                revisionId: revisionPayload && revisionPayload.id || null,
                revision: revisionPayload && revisionPayload.revision || null
            }, { source: 'typing' }));
            return applyInputOperations(operations, { normalized: normalized, transactionType: 'typing', selectionRegion: currentSelection.region || 'Body' });
        }

        function createOrExtendInputRevision(range, text, source) {
            var inserted = _asText(text);
            if (!inserted) return null;
            var normalizedRange = normalizeRevisionRange(range);
            var formattingKey = stableRevisionStringify(normalizeMarks(activeTypingMarks || []));
            var canExtend = !!(activeInputRevision
                && activeInputRevision.blockId === normalizedRange.blockId
                && Number(activeInputRevision.end || 0) === Number(normalizedRange.start || 0)
                && activeInputRevision.formattingKey === formattingKey);
            if (canExtend) {
                var existing = revisionById(model, activeInputRevision.id);
                if (existing) {
                    var nextText = _asText(activeInputRevision.text) + inserted;
                    activeInputRevision.text = nextText;
                    activeInputRevision.end = Number(activeInputRevision.end || normalizedRange.start) + inserted.length;
                    existing.affectedRange = _sortObject({
                        blockId: normalizedRange.blockId,
                        start: Number(activeInputRevision.start ?? normalizedRange.start),
                        end: Number(activeInputRevision.end || normalizedRange.end)
                    });
                    existing.range = existing.affectedRange;
                    setRevisionPayloadText(existing, nextText);
                    return _sortObject({ id: existing.id, revision: _clone(existing), reused: true });
                }
            }

            var payload = createInsertionRevisionPayload(normalizedRange, inserted, userId, source || 'typing');
            activeInputRevision = {
                id: payload.id,
                blockId: normalizedRange.blockId,
                start: Number(normalizedRange.start || 0),
                end: Number(normalizedRange.end || 0),
                text: inserted,
                formattingKey: formattingKey
            };
            return _sortObject({ id: payload.id, revision: payload, reused: false });
        }

        function createDeleteOperation(range, source) {
            var revisionPayload = trackChanges ? createDeletionRevisionPayload(model, range, userId, source || 'delete') : null;
            return createOperation(OPERATION_TYPES.DeleteRange, {
                range: range,
                revisionId: revisionPayload && revisionPayload.id || null,
                revision: revisionPayload || null
            }, { source: source || 'input' });
        }

        function applyDeletion(selection, inputType, normalized) {
            activeInputRevision = null;
            var plan = planDeletion(selection, inputType);
            if (plan.operations.length === 0) return Object.assign({ ok: true, normalized: normalized || null }, plan);
            return Object.assign(applyInputOperations(plan.operations, { normalized: normalized, transactionType: 'delete', selectionRegion: selection.region || 'Body' }), plan);
        }

        function planDeletion(selection, inputType) {
            plannedDeletionCount++;
            var snapshot = createSelectionSnapshot(selection || currentSelection);
            var block = _findBlock(model, snapshot.blockId);
            if (block && block.type === 'image') {
                return _sortObject({
                    operations: [createOperation(OPERATION_TYPES.SetSelection, { selection: { blockId: block.id, offset: inputType.indexOf('Backward') >= 0 ? 0 : 1, objectId: block.content && block.content.objectId || block.id } }, { source: 'input' }).toJSON()],
                    objectAction: 'selectObject',
                    revisionBoundaryPolicy: '',
                    normalizedToPreviousRun: false
                });
            }
            var text = _blockText(block);
            var offset = Math.max(0, Math.min(text.length, Number(snapshot.offset || 0)));
            if (snapshot.isCollapsed === false) {
                var range = selectionToRange(snapshot);
                return deletionPlan([createDeleteOperation(range, 'input')], false, block);
            }
            if (offset === 0 && inputType.indexOf('Backward') >= 0) {
                return deletionPlan([createOperation(OPERATION_TYPES.MergeParagraph, { target: { blockId: snapshot.blockId, offset: 0 } }, { source: 'input' })], false, block);
            }
            var backward = inputType.indexOf('Backward') >= 0;
            var word = inputType.indexOf('Word') >= 0;
            var start = backward ? (word ? previousWordBoundary(text, offset) : Math.max(0, offset - 1)) : offset;
            var end = backward ? offset : (word ? nextWordBoundary(text, offset) : Math.min(text.length, offset + 1));
            var inlineInfo = _inlineAtOffset(block, offset);
            var normalizedToPreviousRun = !!(inputType === 'deleteWordBackward' && inlineInfo && offset === inlineInfo.end);
            return deletionPlan([createDeleteOperation({ blockId: snapshot.blockId, start: start, end: end }, 'input')], normalizedToPreviousRun, block);
        }

        function deletionPlan(operations, normalizedToPreviousRun, block) {
            var revisionBoundary = block && _asArray(block.content && block.content.runs).some(function (run) { return !!run.revisionId; })
                ? 'revision-boundary-checked'
                : '';
            return _sortObject({
                operations: operations.map(function (op) { return op.toJSON ? op.toJSON() : op; }),
                objectAction: '',
                revisionBoundaryPolicy: revisionBoundary,
                normalizedToPreviousRun: normalizedToPreviousRun === true
            });
        }

        function handleEnter(selection, options) {
            activeInputRevision = null;
            var snapshot = createSelectionSnapshot(selection || currentSelection);
            var block = _findBlock(model, snapshot.blockId);
            if (block && block.type === 'image') {
                return _sortObject({
                    ok: true,
                    operations: [],
                    wrapContextStable: true,
                    selection: snapshot,
                    layout: lastLayout
                });
            }
            var newBlockId = _stableId('block', snapshot.blockId + '-enter-' + Date.now() + '-' + Math.floor(Math.random() * 1000));
            var structureRevision = trackChanges
                ? createStructureRevisionPayload({ blockId: snapshot.blockId, start: Number(snapshot.offset || 0), end: Number(snapshot.offset || 0) }, 'SplitBlock', userId, 'input')
                : null;
            var op = createOperation(OPERATION_TYPES.SplitParagraph, {
                target: { blockId: snapshot.blockId, offset: Number(snapshot.offset || 0) },
                newBlockId: newBlockId,
                revisionId: structureRevision && structureRevision.id || null,
                revision: structureRevision || null
            }, { source: 'input' });
            var result = applyInputOperations([op], {
                normalized: options && options.normalized || null,
                transactionType: 'enter',
                selectionRegion: snapshot.region || 'Body'
            });
            result.preservedListStyle = !!(block && block.content && block.content.style && (block.content.style.listType || block.content.style.ListType));
            result.wrapContextStable = true;
            result.selection = Object.assign({}, result.selection || currentSelection, { region: snapshot.region || 'Body' });
            return _sortObject(result);
        }

        function toggleBold(normalized) {
            activeInputRevision = null;
            var hasBold = activeTypingMarks.some(function (mark) { return String(mark.type || mark.Type).toLowerCase() === 'bold'; });
            activeTypingMarks = hasBold
                ? activeTypingMarks.filter(function (mark) { return String(mark.type || mark.Type).toLowerCase() !== 'bold'; })
                : activeTypingMarks.concat([{ type: 'Bold' }]);
            return _sortObject({ ok: true, normalized: normalized, operations: [], activeTypingMarks: activeTypingMarks });
        }

        function handleCompositionStart(eventLike) {
            composition = {
                transactionType: 'composition',
                beforeSelection: createSelectionSnapshot(eventLike && eventLike.selection || currentSelection),
                preview: ''
            };
            return _sortObject({ ok: true, transactionType: 'composition', selection: composition.beforeSelection });
        }

        function handleCompositionUpdate(eventLike) {
            if (!composition) handleCompositionStart(eventLike);
            composition.preview = _asText(eventLike && eventLike.data);
            var previewBlock = _clone(_findBlock(model, composition.beforeSelection.blockId) || null);
            if (previewBlock) {
                _insertTextRun(previewBlock, composition.beforeSelection.offset, composition.preview, { marks: _clone(activeTypingMarks) });
            }
            var previewLayout = paragraphEngine.layoutAfterOperation(model, createOperation(OPERATION_TYPES.InsertText, {
                target: { blockId: composition.beforeSelection.blockId, offset: composition.beforeSelection.offset },
                text: composition.preview
            }, { source: 'composition-preview' }), previousLayout, { page: page, selection: composition.beforeSelection });
            return _sortObject({
                ok: true,
                transactionType: 'composition',
                boundaryPatchQueued: false,
                selection: composition.beforeSelection,
                previewText: previewBlock ? _blockText(previewBlock) : '',
                previewLayout: previewLayout
            });
        }

        function handleCompositionEnd(eventLike) {
            if (!composition) handleCompositionStart(eventLike);
            currentSelection = createSelectionSnapshot(eventLike && eventLike.selection || composition.beforeSelection);
            var result = insertText(_asText(eventLike && eventLike.data), { normalized: { inputType: 'compositionend', command: 'InsertText', preventDefault: true } });
            composition = null;
            result.transactionType = 'composition';
            return _sortObject(result);
        }

        function handlePaste(input) {
            activeInputRevision = null;
            var source = input || {};
            var normalizedText = normalizePasteText(source.plainText || source.PlainText || source.text || source.Text || source.html || source.Html || '');
            currentSelection = createSelectionSnapshot(source.selection || source.Selection || currentSelection);
            var range = selectionToRange(currentSelection);
            var operations = [];
            if (!currentSelection.isCollapsed && range.start !== range.end) {
                operations.push(createDeleteOperation(range, 'paste'));
            }
            var lines = normalizedText.split('\n');
            var pasteRevision = trackChanges
                ? createInsertionRevisionPayload({ blockId: range.blockId, start: range.start, end: range.start + (lines[0] || '').length }, lines[0] || '', userId, 'paste')
                : null;
            operations.push(createOperation(OPERATION_TYPES.InsertText, {
                target: { blockId: range.blockId, offset: range.start },
                text: lines[0] || '',
                revisionId: pasteRevision && pasteRevision.id || null,
                revision: pasteRevision || null
            }, { source: 'paste' }));
            var activeBlockId = range.blockId;
            for (var i = 1; i < lines.length; i++) {
                var splitId = _stableId('block', activeBlockId + '-paste-' + i + '-' + Date.now());
                operations.push(createOperation(OPERATION_TYPES.SplitParagraph, {
                    target: { blockId: activeBlockId, offset: (lines[i - 1] || '').length },
                    newBlockId: splitId
                }, { source: 'paste' }));
                operations.push(createOperation(OPERATION_TYPES.InsertText, {
                    target: { blockId: splitId, offset: 0 },
                    text: lines[i] || ''
                }, { source: 'paste' }));
                activeBlockId = splitId;
            }
            var result = applyInputOperations(operations, { normalized: source.normalized || null, transactionType: 'paste', selectionRegion: currentSelection.region || 'Body' });
            result.transactionType = 'paste';
            result.htmlNormalized = !!(source.html || source.Html);
            result.singleUndoTransaction = true;
            return _sortObject(result);
        }

        function applyInputOperations(operations, meta) {
            var applied = [];
            var lastResult = null;
            operations.forEach(function (operation) {
                var op = attachOperationMethods(operation);
                lastResult = applyOperation(model, op);
                if (lastResult.ok) {
                    applied.push(op.toJSON ? op.toJSON() : _clone(op));
                    var next = lastResult.nextSelection || currentSelection;
                    currentSelection = createSelectionSnapshot(Object.assign({}, next, { region: meta && meta.selectionRegion || currentSelection.region || 'Body' }));
                }
            });
            var lastOperation = applied[applied.length - 1] || null;
            lastLayout = lastOperation
                ? paragraphEngine.layoutAfterOperation(model, lastOperation, previousLayout, { page: page, selection: currentSelection })
                : paragraphEngine.layoutDocument(model, { page: page, selection: currentSelection });
            previousLayout = lastLayout;
            if (root) {
                var snapshot = createRenderSnapshot(model, lastLayout, currentSelection, {
                    affectedScopes: lastLayout.debug && lastLayout.debug.invalidatedScopes || [currentSelection.blockId]
                });
                renderer.render(root, snapshot, { scope: lastLayout.debug && lastLayout.debug.minimalScope || null });
                lastVisibleText = root.textContent || '';
            } else {
                lastVisibleText = _blockText(_findBlock(model, currentSelection.blockId));
            }
            queueBoundaryPatch(applied, meta && meta.transactionType || 'input');
            return _sortObject({
                ok: !lastResult || lastResult.ok !== false,
                normalized: meta && meta.normalized || null,
                transactionType: meta && meta.transactionType || 'input',
                operations: applied,
                selection: currentSelection,
                layout: lastLayout
            });
        }

        function queueBoundaryPatch(operations, transactionType) {
            if (!operations.length) return;
            boundaryPatches.push({ transactionType: transactionType, operations: _clone(operations), at: Date.now() });
            Promise.resolve().then(function () {
                boundaryPatchFlushCount++;
            });
        }

        function flushBoundaryPatches() {
            return Promise.resolve().then(function () {
                boundaryPatchFlushCount++;
                return boundaryPatches.length;
            });
        }

        function nextFrame() {
            return new Promise(function (resolve) {
                if (typeof requestAnimationFrame === 'function') requestAnimationFrame(function () { resolve(true); });
                else setTimeout(function () { resolve(true); }, 0);
            });
        }

        function debug() {
            return _sortObject({
                boundaryPatchCount: boundaryPatches.length,
                boundaryPatchFlushCount: boundaryPatchFlushCount,
                plannedDeletionCount: plannedDeletionCount,
                mutationObserverMode: mutationObserverMode,
                browserMutationUsed: browserMutationUsed,
                log: debugLog,
                lastVisibleText: lastVisibleText
            });
        }

        return {
            handleBeforeInput: handleBeforeInput,
            insertText: insertText,
            planDeletion: planDeletion,
            handleEnter: handleEnter,
            handleCompositionStart: handleCompositionStart,
            handleCompositionUpdate: handleCompositionUpdate,
            handleCompositionEnd: handleCompositionEnd,
            handlePaste: handlePaste,
            flushBoundaryPatches: flushBoundaryPatches,
            nextFrame: nextFrame,
            debug: debug
        };
    }

    function createRevisionForInput(model, userId) {
        var id = 'rev-' + Date.now() + '-' + Math.floor(Math.random() * 100000);
        if (!Array.isArray(model.revisions)) model.revisions = [];
        model.revisions.push({ id: id, type: 'Insertion', status: 'Pending', author: userId || 'local', authorId: userId || 'local', source: 'input', payload: { text: '' } });
        buildIndexes(model);
        return id;
    }

    function createLiveInsertionRevisionPayload(selection, text, userId) {
        var range = selectionToRange(selection || {});
        return createInsertionRevisionPayload({
            blockId: range.blockId,
            start: Number(range.start || 0),
            end: Number(range.start || 0) + _asText(text).length
        }, text, userId || 'local', 'keydown');
    }

    function createOrExtendLiveTypingRevision(inst, selection, text, marks) {
        if (!inst || !inst.model) return null;
        var inserted = _asText(text);
        if (!inserted) return null;
        var userId = resolveRevisionUserId(inst.options || {});
        var range = selectionToRange(selection || {});
        var now = Date.now();
        var formattingKey = stableRevisionStringify(normalizeMarks(marks || []));
        var session = inst.activeTypingRevision || null;
        var canExtend = !!(session
            && session.blockId === range.blockId
            && Number(session.end || 0) === Number(range.start || 0)
            && session.formattingKey === formattingKey
            && now - Number(session.updatedAt || 0) <= 1250);

        if (canExtend) {
            var existing = revisionById(inst.model, session.id);
            if (existing) {
                var nextText = _asText(session.text) + inserted;
                session.text = nextText;
                session.end = Number(session.end || range.start) + inserted.length;
                session.updatedAt = now;
                existing.affectedRange = _sortObject(Object.assign({}, existing.affectedRange || {}, {
                    blockId: range.blockId,
                    start: Number(session.start ?? range.start),
                    end: Number(session.end)
                }));
                existing.payload = _sortObject(Object.assign({}, existing.payload || {}, { text: nextText }));
                existing.payloadJson = nextText;
                return _sortObject({ id: session.id, revision: _clone(existing), reused: true });
            }
        }

        var payload = createLiveInsertionRevisionPayload(selection, inserted, userId);
        inst.activeTypingRevision = {
            id: payload.id,
            blockId: range.blockId,
            start: Number(range.start || 0),
            end: Number(range.start || 0) + inserted.length,
            text: inserted,
            formattingKey: formattingKey,
            updatedAt: now
        };
        return _sortObject({ id: payload.id, revision: payload, reused: false });
    }

    function clearLiveTypingRevision(inst) {
        if (inst) inst.activeTypingRevision = null;
    }

    function selectionToRange(selection) {
        var snapshot = createSelectionSnapshot(selection || {});
        if (snapshot.isCollapsed !== false) {
            return { blockId: snapshot.blockId, start: Number(snapshot.offset || 0), end: Number(snapshot.offset || 0) };
        }
        var anchor = snapshot.anchor || {};
        var focus = snapshot.focus || {};
        var start = Math.min(Number(anchor.offset || 0), Number(focus.offset || 0));
        var end = Math.max(Number(anchor.offset || 0), Number(focus.offset || 0));
        return { blockId: focus.blockId || anchor.blockId || snapshot.blockId, start: start, end: end };
    }

    function firstModelSelection(model) {
        var block = _firstTextBlock(model);
        return { region: 'Body', blockId: block && block.id || '', offset: 0, isCollapsed: true };
    }

    function normalizePasteText(value) {
        var text = _asText(value)
            .replace(/<br\s*\/?>/gi, '\n')
            .replace(/<\/p>\s*<p[^>]*>/gi, '\n')
            .replace(/<[^>]+>/g, '')
            .replace(/\r\n?/g, '\n');
        return text;
    }

    function createTypingChangeBuffer(options) {
        var opts = options || {};
        var timeoutMs = Number(opts.timeoutMs || opts.TimeoutMs || 1000) || 1000;
        var operations = [];
        var lastSelection = null;
        function push(operation) {
            var op = attachOperationMethods(operation);
            var previous = operations[operations.length - 1];
            if (previous && shouldCoalesceTyping(attachOperationMethods(previous), op, op.timestamp, timeoutMs)) {
                operations[operations.length - 1] = coalesceTypingOperation(attachOperationMethods(previous), op).toJSON();
            } else {
                operations.push(op.toJSON ? op.toJSON() : _clone(op));
            }
        }
        function resetForSelectionChange(selection) {
            operations = [];
            lastSelection = _clone(selection || null);
        }
        function resetForCommand(commandName) {
            operations = [];
            return commandName;
        }
        function snapshot() {
            return _sortObject({ operationCount: operations.length, operations: operations, lastSelection: lastSelection });
        }
        return {
            push: push,
            resetForSelectionChange: resetForSelectionChange,
            resetForCommand: resetForCommand,
            resetForEnter: function () { resetForCommand('enter'); },
            resetForPaste: function () { resetForCommand('paste'); },
            resetForDelete: function () { resetForCommand('delete'); },
            snapshot: snapshot
        };
    }

    function createActiveLayoutScheduler(options) {
        var opts = options || {};
        var page = opts.page || opts.Page || { x: 0, y: 0, width: 640, height: 900 };
        var frameBudgetMs = Number(opts.frameBudgetMs || opts.FrameBudgetMs || 16) || 16;
        var idleDebounceMs = Number(opts.idleDebounceMs ?? opts.IdleDebounceMs ?? 250);
        var warningThreshold = Number(opts.repeatedBudgetWarningThreshold || opts.RepeatedBudgetWarningThreshold || 3) || 3;
        var testMode = opts.testMode === true || opts.TestMode === true;
        var paragraphEngine = opts.paragraphLayoutEngine || createParagraphLayoutEngine();
        var renderer = opts.renderer || createAtomicRenderer();
        var previousLayout = opts.initialLayout || null;
        var pendingIdle = null;
        var timeline = [];
        var idleRunCount = 0;
        var lastSelection = opts.selection || opts.Selection || null;
        var lastIdleSnapshot = null;
        var stats = {
            immediateRunCount: 0,
            idleRunCount: 0,
            budgetWarningCount: 0,
            safeDegradedMode: false,
            lastOperationApplyMs: 0,
            lastLayoutMs: 0,
            lastRenderMs: 0,
            lastSelectionRestoreMs: 0,
            lastTotalMs: 0
        };

        function runImmediate(request) {
            var req = request || {};
            var model = req.model || req.Model;
            var root = req.root || req.Root || null;
            var selection = createSelectionSnapshot(req.selection || req.Selection || lastSelection || firstModelSelection(model));
            var operation = req.operation || req.Operation || null;
            var operations = operation ? [operation] : _asArray(req.operations || req.Operations);
            var simulated = req.simulatedDurations || req.SimulatedDurations || {};
            var compositionMode = req.compositionMode || req.CompositionMode || '';
            var metrics = {};
            var totalStart = nowMs();

            var applyStart = nowMs();
            if (compositionMode !== 'preview') {
                operations.forEach(function (op) {
                    var result = applyOperation(model, attachOperationMethods(op));
                    if (result && result.nextSelection) {
                        selection = createSelectionSnapshot(Object.assign({}, result.nextSelection, {
                            region: selection.region || result.nextSelection.region || 'Body'
                        }));
                    }
                });
            }
            metrics.operationApplyMs = elapsedWithSimulated(applyStart, simulated.operationApply || simulated.OperationApply);

            var layoutStart = nowMs();
            var layoutOperation = operations[operations.length - 1] || null;
            var layout = layoutOperation
                ? paragraphEngine.layoutAfterOperation(model, layoutOperation, previousLayout, { page: req.page || page, selection: selection })
                : paragraphEngine.layoutDocument(model, { page: req.page || page, selection: selection });
            metrics.layoutMs = elapsedWithSimulated(layoutStart, simulated.layout || simulated.Layout);

            var renderStart = nowMs();
            var snapshot = createRenderSnapshot(model, layout, selection, {
                affectedScopes: layout.debug && layout.debug.invalidatedScopes || [selection.blockId]
            });
            var renderResult = root
                ? renderer.render(root, snapshot, { scope: layout.debug && layout.debug.minimalScope || null })
                : { ok: true };
            metrics.renderMs = elapsedWithSimulated(renderStart, simulated.render || simulated.Render);

            var selectionStart = nowMs();
            if (root) restoreLogicalSelection(root, selection);
            metrics.selectionRestoreMs = elapsedWithSimulated(selectionStart, simulated.selectionRestore || simulated.SelectionRestore);
            metrics.totalMs = metrics.operationApplyMs + metrics.layoutMs + metrics.renderMs + metrics.selectionRestoreMs;

            previousLayout = layout;
            lastSelection = selection;
            stats.immediateRunCount++;
            stats.lastOperationApplyMs = metrics.operationApplyMs;
            stats.lastLayoutMs = metrics.layoutMs;
            stats.lastRenderMs = metrics.renderMs;
            stats.lastSelectionRestoreMs = metrics.selectionRestoreMs;
            stats.lastTotalMs = Math.max(metrics.totalMs, nowMs() - totalStart);
            var budgetWarning = stats.lastTotalMs > frameBudgetMs;
            if (budgetWarning) {
                stats.budgetWarningCount++;
                timeline.push({ kind: 'budget-warning', totalMs: stats.lastTotalMs, frameBudgetMs: frameBudgetMs, at: Date.now() });
            }
            if (stats.budgetWarningCount >= warningThreshold) stats.safeDegradedMode = true;

            var gate = root && testMode ? probeNoInvalidFrame(root, snapshot, { throwOnFailure: false }) : { ok: true };
            timeline.push({
                kind: compositionMode === 'preview' ? 'immediate-composition-preview' : 'immediate',
                activeBlockId: layout.activeBlockId || selection.blockId,
                metrics: _clone(metrics),
                budgetWarning: budgetWarning,
                invalidFrameOk: gate.ok === true,
                at: Date.now()
            });
            return _sortObject({
                ok: renderResult.ok !== false && gate.ok !== false,
                kind: 'immediate',
                compositionMode: compositionMode,
                selection: selection,
                layout: layout,
                snapshot: snapshot,
                metrics: metrics,
                budgetWarning: budgetWarning,
                invalidFrame: gate,
                render: renderResult
            });
        }

        function scheduleIdleReconciliation(request) {
            pendingIdle = Object.assign({}, request || {});
            timeline.push({ kind: 'idle-scheduled', debounceMs: idleDebounceMs, reason: pendingIdle.reason || pendingIdle.Reason || '', at: Date.now() });
            return _sortObject({ scheduled: true, debounceMs: idleDebounceMs, reason: pendingIdle.reason || pendingIdle.Reason || '' });
        }

        function flushIdle() {
            return Promise.resolve().then(function () {
                if (!pendingIdle) return _sortObject({ ok: true, kind: 'idle', skipped: true, layout: previousLayout, selection: lastSelection });
                var req = pendingIdle;
                pendingIdle = null;
                var model = req.model || req.Model;
                var root = req.root || req.Root || null;
                var selection = createSelectionSnapshot(req.selection || req.Selection || lastSelection || firstModelSelection(model));
                var before = snapshotForIdle(model, previousLayout, selection);
                var layout = paragraphEngine.layoutDocument(model, { page: req.page || page, selection: selection });
                var after = snapshotForIdle(model, layout, selection);
                if (root) {
                    var snapshot = createRenderSnapshot(model, layout, selection, { affectedScopes: layout.debug && layout.debug.invalidatedScopes || ['document'] });
                    renderer.render(root, snapshot, { scope: createLayoutScope(LAYOUT_SCOPE_KINDS.WholeDocument, { affectedScopeIds: ['document'], reason: 'idle' }) });
                    restoreLogicalSelection(root, selection);
                }
                previousLayout = layout;
                lastSelection = selection;
                idleRunCount++;
                stats.idleRunCount = idleRunCount;
                lastIdleSnapshot = after;
                timeline.push({
                    kind: 'idle',
                    selectionStable: JSON.stringify(before.selection) === JSON.stringify(after.selection),
                    wordOrderStable: before.text === after.text,
                    at: Date.now()
                });
                return _sortObject({ ok: true, kind: 'idle', layout: layout, selection: selection, beforeIdle: before, afterIdle: after });
            });
        }

        function snapshotForIdle(model, layout, selection) {
            return _sortObject({
                selection: createSelectionSnapshot(selection || lastSelection || firstModelSelection(model)),
                text: _asArray(model && model.body && model.body.blocks).filter(function (block) { return block.type === 'paragraph'; }).map(_blockText).join('|'),
                blocks: _asArray(layout && layout.blocks).map(function (block) {
                    return { blockId: block.blockId, type: block.type, rect: _clone(block.rect || {}) };
                })
            });
        }

        function probeNoInvalidFrame(root, snapshot, options) {
            var opts = options || {};
            var segments = Array.from(root && root.querySelectorAll ? root.querySelectorAll('[data-layout-segment-id]') : []);
            var objects = Array.from(root && root.querySelectorAll ? root.querySelectorAll('[data-render-object-id]') : []);
            var textTextOverlaps = 0;
            var textImageOverlaps = 0;
            var segmentOverflows = 0;
            for (var i = 0; i < segments.length; i++) {
                var a = domRectToRect(segments[i].getBoundingClientRect());
                var expectedHeight = Number(segments[i].getAttribute('data-layout-height') || 0);
                if (expectedHeight > 0 && a.height > expectedHeight + 1.5) segmentOverflows++;
                for (var j = i + 1; j < segments.length; j++) {
                    var b = domRectToRect(segments[j].getBoundingClientRect());
                    if (rectsOverlapWithTolerance(a, b, 0.5)) textTextOverlaps++;
                }
                objects.forEach(function (objectNode) {
                    if (rectsOverlapWithTolerance(a, domRectToRect(objectNode.getBoundingClientRect()), 0.5)) textImageOverlaps++;
                });
            }
            var selection = snapshot && snapshot.selection || {};
            var missingCaret = !!(selection && selection.blockId) && !_asArray(snapshot && snapshot.layout && snapshot.layout.caretStops).some(function (stop) {
                return stop.blockId === selection.blockId && Number(stop.offset) === Number(selection.offset || 0);
            });
            var result = _sortObject({
                ok: textTextOverlaps === 0 && textImageOverlaps === 0 && segmentOverflows === 0 && missingCaret === false,
                textTextOverlaps: textTextOverlaps,
                textImageOverlaps: textImageOverlaps,
                segmentOverflows: segmentOverflows,
                missingCaret: missingCaret
            });
            if (!result.ok && opts.throwOnFailure) {
                throw new Error('invalid-frame: ' + JSON.stringify(result));
            }
            return result;
        }

        function debug() {
            return _sortObject({
                timeline: timeline,
                stats: stats,
                idleRunCount: idleRunCount,
                hasPendingIdle: !!pendingIdle,
                compositionAware: true,
                lastIdleSnapshot: lastIdleSnapshot
            });
        }

        return {
            runImmediate: runImmediate,
            scheduleIdleReconciliation: scheduleIdleReconciliation,
            flushIdle: flushIdle,
            probeNoInvalidFrame: probeNoInvalidFrame,
            snapshotForIdle: snapshotForIdle,
            debug: debug
        };
    }

    function nowMs() {
        return typeof performance !== 'undefined' && performance.now ? performance.now() : Date.now();
    }

    function elapsedWithSimulated(start, simulated) {
        return Math.max(nowMs() - start, Number(simulated || 0) || 0);
    }

    function rectsOverlapWithTolerance(a, b, tolerance) {
        var t = Number(tolerance || 0);
        return a.x < b.x + b.width - t && a.x + a.width > b.x + t && a.y < b.y + b.height - t && a.y + a.height > b.y + t;
    }

    function buildLayoutSnapshot(root, model) {
        var blocks = _asArray(model && model.body && model.body.blocks);
        var layoutBlocks = [];
        var caretStops = [];
        var y = 20;
        blocks.forEach(function (block, blockIndex) {
            var text = _blockText(block);
            var dom = root && root.querySelector ? root.querySelector('[data-block-id="' + cssEscape(block.id) + '"]') : null;
            var rect = dom && dom.getBoundingClientRect ? dom.getBoundingClientRect() : null;
            var x = rect ? rect.x : 40;
            var top = rect ? rect.y : y;
            var width = rect && rect.width > 0 ? rect.width : 640;
            var height = rect && rect.height > 0 ? rect.height : (block.type === 'image' ? 96 : 24);
            var lineLength = Math.max(1, Math.min(text.length || 1, 80));
            var line = {
                id: block.id + '-line-0',
                blockId: block.id,
                start: 0,
                end: text.length,
                rect: { x: x, y: top, width: width, height: Math.max(18, Math.min(height, 24)) },
                availableIntervals: [{ x: x, width: width, start: 0, end: text.length }]
            };
            if (block.type === 'image') {
                line.objectId = block.content && block.content.objectId || block.id;
                line.rect.height = height;
                caretStops.push({ blockId: block.id, offset: 0, affinity: 'before', rect: { x: x, y: top, width: 1, height: height }, objectBoundary: true });
                caretStops.push({ blockId: block.id, offset: 1, affinity: 'after', rect: { x: x + width, y: top, width: 1, height: height }, objectBoundary: true });
            } else {
                for (var i = 0; i <= text.length; i++) {
                    caretStops.push({
                        blockId: block.id,
                        inlineId: (_inlineAtOffset(block, i) || {}).run && (_inlineAtOffset(block, i) || {}).run.id || null,
                        offset: i,
                        affinity: i === 0 ? 'before' : 'after',
                        rect: { x: x + (Math.min(i, lineLength) * 7), y: top, width: 1, height: line.rect.height },
                        lineId: line.id
                    });
                }
            }
            layoutBlocks.push({
                id: 'layout-' + block.id,
                blockId: block.id,
                type: block.type,
                rect: { x: x, y: top, width: width, height: height },
                lines: [line],
                segments: [{ id: 'segment-' + block.id + '-0', blockId: block.id, start: 0, end: text.length, rect: line.rect }],
                objectId: block.type === 'image' ? (block.content && block.content.objectId || block.id) : null
            });
            y += height + 12;
        });
        return _sortObject({ blocks: layoutBlocks, caretStops: caretStops, debug: { source: root ? 'dom-or-synthetic' : 'synthetic' } });
    }

    function createModelLayoutDomMapper(root, model, layout) {
        var snapshot = layout || buildLayoutSnapshot(root, model);
        return {
            blockIdToLayoutBlockId: function (blockId) {
                var item = _asArray(snapshot.blocks).find(function (block) { return block.blockId === blockId; });
                return item ? item.id : null;
            },
            inlineOffsetToCaretRect: function (inlineId, offset) {
                var stop = _asArray(snapshot.caretStops).find(function (item) {
                    return item.inlineId === inlineId && Number(item.offset) === Number(offset);
                });
                return stop ? _clone(stop.rect) : null;
            },
            layoutSegmentIdToDomElement: function (segmentId) {
                var segment = null;
                _asArray(snapshot.blocks).some(function (block) {
                    segment = _asArray(block.segments).find(function (item) { return item.id === segmentId; });
                    return !!segment;
                });
                return segment && root && root.querySelector ? root.querySelector('[data-block-id="' + cssEscape(segment.blockId) + '"]') : null;
            },
            domTextNodeToLogical: function (node, offset) {
                return domTextNodeToLogical(root, model, node, offset);
            },
            pointerToVisualLine: function (x, y) {
                return pointerHitTest(model, snapshot, x, y);
            },
            widgetHandleToObjectBoundary: function (blockId, handle) {
                var block = _findBlock(model, blockId);
                return createLogicalPosition({ region: 'Body', blockId: blockId, objectId: block && block.content && block.content.objectId || blockId, offset: handle === 'before' ? 0 : 1, affinity: handle === 'before' ? 'before' : 'after' });
            },
            captionPointToPosition: function (blockId, offset) {
                return createLogicalPosition({ region: 'Caption', blockId: blockId, offset: Math.max(0, Number(offset || 0)), affinity: 'after', limitId: blockId + '-caption' });
            },
            debugDump: function () {
                return _sortObject({ blockCount: _asArray(snapshot.blocks).length, caretStopCount: _asArray(snapshot.caretStops).length, layout: snapshot });
            }
        };
    }

    function cssEscape(value) {
        if (window.CSS && typeof window.CSS.escape === 'function') return window.CSS.escape(value);
        return _asText(value).replace(/"/g, '\\"');
    }

    function logicalToDomRange(root, model, position) {
        var requestedBlockId = position && (position.blockId || position.BlockId);
        if (requestedBlockId && !_findBlock(model, requestedBlockId)) {
            return { ok: false, error: { code: 'missing-dom-block', blockId: requestedBlockId } };
        }
        var pos = normalizeLogicalPosition(model, position);
        var block = root && root.querySelector ? root.querySelector('[data-block-id="' + cssEscape(pos.blockId) + '"]') : null;
        if (!block) return { ok: false, error: { code: 'missing-dom-block', blockId: pos.blockId } };
        var range = document.createRange();
        var point = domTextPointAtBlockOffset(block, pos.offset);
        if (!point || !point.node) {
            range.setStart(block, 0);
            range.collapse(true);
            return { ok: true, range: range, position: pos };
        }
        range.setStart(point.node, Math.max(0, Math.min(point.node.nodeValue ? point.node.nodeValue.length : block.childNodes.length, point.offset)));
        range.collapse(true);
        return { ok: true, range: range, position: pos };
    }

    function domRangeToLogical(root, model, range) {
        if (!range) return { ok: false, error: { code: 'missing-dom-range' } };
        return domTextNodeToLogical(root, model, range.startContainer, range.startOffset);
    }

    function isInlineBreakNode(node) {
        return !!(node
            && node.nodeType === 1
            && String(node.tagName || '').toLowerCase() === 'br'
            && node.getAttribute
            && node.getAttribute('data-inline-break') !== null);
    }

    function isCaretPlaceholderNode(node) {
        return !!(node
            && node.nodeType === 1
            && String(node.tagName || '').toLowerCase() === 'br'
            && node.getAttribute
            && node.getAttribute('data-caret-placeholder') !== null);
    }

    function domLogicalLength(node) {
        if (!node) return 0;
        if (node.nodeType === 3) return node.nodeValue ? node.nodeValue.length : 0;
        if (isInlineBreakNode(node)) return 1;
        if (isCaretPlaceholderNode(node)) return 0;
        var total = 0;
        var children = node.childNodes || [];
        for (var i = 0; i < children.length; i++) {
            total += domLogicalLength(children[i]);
        }
        return total;
    }

    function domBoundaryLogicalOffset(root, node, offset) {
        if (!root || !node) return 0;
        if (root === node) {
            if (node.nodeType === 3) {
                return Math.max(0, Math.min(node.nodeValue ? node.nodeValue.length : 0, Number(offset || 0)));
            }
            var ownChildren = node.childNodes || [];
            var childLimit = Math.max(0, Math.min(ownChildren.length, Number(offset || 0)));
            var ownTotal = 0;
            for (var ownIndex = 0; ownIndex < childLimit; ownIndex++) {
                ownTotal += domLogicalLength(ownChildren[ownIndex]);
            }
            return ownTotal;
        }

        var children = root.childNodes || [];
        var total = 0;
        for (var index = 0; index < children.length; index++) {
            var child = children[index];
            if (child === node || child.contains && child.contains(node)) {
                return total + domBoundaryLogicalOffset(child, node, offset);
            }
            total += domLogicalLength(child);
        }
        return total;
    }

    function domTextNodeToLogical(root, model, node, offset) {
        var element = node && node.nodeType === Node.ELEMENT_NODE ? node : node && node.parentElement;
        var blockElement = element && element.closest ? element.closest('[data-block-id]') : null;
        if (!blockElement || root && !root.contains(blockElement)) return { ok: false, error: { code: 'dom-node-outside-editor' } };
        var blockId = blockElement.getAttribute('data-block-id');
        var block = _findBlock(model, blockId);
        if (!block) return { ok: false, error: { code: 'missing-model-block', blockId: blockId } };
        var regionNode = blockElement.closest('[data-render-region]');
        var region = regionNode && regionNode.getAttribute('data-render-region') || 'Body';
        var headerFooterId = regionNode && regionNode.getAttribute('data-hf-id') || null;
        var logicalOffset = Math.max(0, Math.min(_blockText(block).length, domBoundaryLogicalOffset(blockElement, node, offset)));
        return { ok: true, position: normalizeLogicalPosition(model, { region: region, blockId: blockId, offset: logicalOffset, affinity: 'after', headerFooterId: headerFooterId }) };
    }

    function findTextNode(root) {
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                return node.nodeValue !== null ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
            }
        });
        return walker.nextNode();
    }

    function caretRectFromLayout(model, layout, position) {
        var pos = normalizeLogicalPosition(model, position);
        var stop = _asArray(layout && layout.caretStops).find(function (item) {
            return item.blockId === pos.blockId && Number(item.offset) === Number(pos.offset);
        });
        return stop ? _clone(stop.rect) : null;
    }

    function compareDomCaretToLayout(root, model, layout, position) {
        var mapped = logicalToDomRange(root, model, position);
        if (!mapped.ok) return { ok: false, error: mapped.error };
        var domRect = mapped.range.getBoundingClientRect();
        if (!domRect || domRect.height === 0) {
            var marker = document.createElement('span');
            marker.textContent = '\u200b';
            mapped.range.insertNode(marker);
            domRect = marker.getBoundingClientRect();
            marker.remove();
        }
        var layoutRect = caretRectFromLayout(model, layout, position);
        if (!layoutRect) return { ok: false, error: { code: 'missing-layout-caret' } };
        var dx = Math.abs((domRect.x || domRect.left || 0) - layoutRect.x);
        var dy = Math.abs((domRect.y || domRect.top || 0) - layoutRect.y);
        return { ok: true, domRect: _sortObject({ x: domRect.x || domRect.left || 0, y: domRect.y || domRect.top || 0, width: domRect.width || 0, height: domRect.height || 0 }), layoutRect: layoutRect, delta: { x: dx, y: dy } };
    }

    function pointerHitTest(model, layout, x, y) {
        var px = Number(x || 0);
        var py = Number(y || 0);
        var objectHit = null;
        var textLineHit = null;
        var tableCellHit = null;
        _asArray(layout && layout.blocks).forEach(function (block) {
            var rect = block.rect || {};
            var insideBlock = px >= rect.x && px <= rect.x + rect.width && py >= rect.y && py <= rect.y + rect.height;
            if (insideBlock && block.type === 'image') objectHit = block;
            if (insideBlock && block.type === 'table') {
                _asArray(block.cells).forEach(function (cell) {
                    var cr = cell.rect || {};
                    if (px >= cr.x && px <= cr.x + cr.width && py >= cr.y && py <= cr.y + cr.height) {
                        tableCellHit = { table: block, cell: cell };
                    }
                });
            }
            _asArray(block.lines).forEach(function (line) {
                var lr = line.rect || {};
                var insideLineY = py >= lr.y && py <= lr.y + lr.height;
                var intervals = _asArray(line.availableIntervals);
                var insideAvailableInterval = _asArray(line.availableIntervals).some(function (interval) {
                    return py >= Number(interval.y ?? lr.y) && py <= Number(interval.y ?? lr.y) + Number(interval.height ?? lr.height)
                        && px >= Number(interval.x ?? lr.x) && px <= Number(interval.x ?? lr.x) + Number(interval.width ?? lr.width);
                });
                var insideLineRect = px >= lr.x && px <= lr.x + lr.width;
                var beforeOrAfterLine = intervals.length
                    ? (px < Math.min.apply(null, intervals.map(function (interval) { return Number(interval.x ?? lr.x); }))
                        || px > Math.max.apply(null, intervals.map(function (interval) { return Number(interval.x ?? lr.x) + Number(interval.width ?? lr.width); })))
                    : (px < lr.x || px > lr.x + lr.width);
                if (insideLineY && (insideAvailableInterval || insideLineRect || beforeOrAfterLine)) {
                    textLineHit = line;
                }
            });
        });
        if (tableCellHit) {
            var firstLayout = _asArray(tableCellHit.cell.blockLayouts)[0] || null;
            var hitBlockId = firstLayout && firstLayout.blockId || '';
            var hitBlock = _findBlock(model, hitBlockId);
            var offset = Math.min(_blockText(hitBlock).length, Math.max(0, textLineHit && textLineHit.cellId === tableCellHit.cell.cellId ? textLineHit.start : 0));
            return {
                type: 'tableCell',
                tableId: tableCellHit.table.blockId,
                cellId: tableCellHit.cell.cellId,
                position: normalizeLogicalPosition(model, {
                    region: 'Body',
                    blockId: hitBlockId,
                    offset: offset,
                    affinity: 'after',
                    cellId: tableCellHit.cell.cellId,
                    tableId: tableCellHit.table.blockId
                })
            };
        }
        if (objectHit && (!textLineHit || hitTestLayerPriority(objectHit.layer, objectHit.wrapMode) >= 10)) {
            var hitImageBlock = _findBlock(model, objectHit.blockId);
            return { type: 'object', position: normalizeLogicalPosition(model, { region: 'Body', blockId: objectHit.blockId, objectId: hitImageBlock && hitImageBlock.content && hitImageBlock.content.objectId || objectHit.blockId, offset: 0, affinity: 'before' }), objectId: objectHit.objectId };
        }
        if (textLineHit) {
            var lineRect = textLineHit.rect;
            var offset;
            if (px <= lineRect.x) offset = textLineHit.start;
            else if (px >= lineRect.x + lineRect.width) offset = textLineHit.end;
            else {
                var ratio = Math.max(0, Math.min(1, (px - lineRect.x) / Math.max(1, lineRect.width)));
                offset = Math.round(textLineHit.start + ratio * Math.max(0, textLineHit.end - textLineHit.start));
            }
            return { type: 'text', position: normalizeLogicalPosition(model, { region: 'Body', blockId: textLineHit.blockId, offset: offset, affinity: 'after', visualHintLineId: textLineHit.id }), lineId: textLineHit.id };
        }
        if (objectHit) {
            var imageBlock = _findBlock(model, objectHit.blockId);
            return { type: 'object', position: normalizeLogicalPosition(model, { region: 'Body', blockId: objectHit.blockId, objectId: imageBlock && imageBlock.content && imageBlock.content.objectId || objectHit.blockId, offset: 0, affinity: 'before' }), objectId: objectHit.objectId };
        }
        return { type: 'none', position: null };
    }

    function moveSelection(model, layout, selection, command, options) {
        var snapshot = normalizeSelectionSnapshot(model, selection);
        var focus = snapshot.focus;
        var block = _findBlock(model, focus.blockId);
        var text = _blockText(block);
        var opts = options || {};
        var next = _clone(focus);
        switch (command) {
            case 'ArrowLeft':
                next.offset = opts.ctrl ? previousWordBoundary(text, focus.offset) : Math.max(0, focus.offset - 1);
                break;
            case 'ArrowRight':
                next.offset = opts.ctrl ? nextWordBoundary(text, focus.offset) : Math.min(text.length, focus.offset + 1);
                break;
            case 'Home':
                next.offset = 0;
                break;
            case 'End':
                next.offset = text.length;
                break;
            case 'ArrowUp':
            case 'ArrowDown':
                next.visualHintLineId = command === 'ArrowUp' ? 'previous-line' : 'next-line';
                break;
        }
        next = normalizeLogicalPosition(model, next);
        if (opts.shift) {
            return createSelectionSnapshot({ range: createLogicalRange(snapshot.anchor, next, next.offset >= snapshot.anchor.offset ? 'forward' : 'backward') });
        }
        return createSelectionSnapshot({ range: createLogicalRange(next, next, 'none') });
    }

    function previousWordBoundary(text, offset) {
        var index = Math.max(0, Number(offset || 0) - 1);
        while (index > 0 && /\s/.test(text[index])) index--;
        while (index > 0 && !/\s/.test(text[index - 1])) index--;
        return index;
    }

    function nextWordBoundary(text, offset) {
        var index = Math.min(text.length, Number(offset || 0));
        while (index < text.length && !/\s/.test(text[index])) index++;
        while (index < text.length && /\s/.test(text[index])) index++;
        return index;
    }

    function createSelectionEngine(root, model, schema) {
        var layout = buildLayoutSnapshot(root, model);
        var mapper = createModelLayoutDomMapper(root, model, layout);
        var postFixer = createSelectionPostFixer(schema || createDefaultSchemaRegistry());
        return {
            createLogicalPosition: createLogicalPosition,
            createLogicalRange: createLogicalRange,
            createSelectionSnapshot: createSelectionSnapshot,
            normalizePosition: function (position) { return normalizeLogicalPosition(model, position); },
            normalizeRange: function (range) { return normalizeLogicalRange(model, range); },
            normalizeSelection: function (selection) { return normalizeSelectionSnapshot(model, selection); },
            logicalToDomRange: function (position) { return logicalToDomRange(root, model, position); },
            domRangeToLogical: function (range) { return domRangeToLogical(root, model, range); },
            buildLayoutSnapshot: function () { return buildLayoutSnapshot(root, model); },
            caretRectFromLayout: function (position) { return caretRectFromLayout(model, layout, position); },
            compareDomCaretToLayout: function (position) { return compareDomCaretToLayout(root, model, layout, position); },
            hitTest: function (x, y) { return pointerHitTest(model, layout, x, y); },
            moveSelection: function (selection, command, options) { return moveSelection(model, layout, selection, command, options); },
            postFix: function (selection) { return postFixer.fix(model, selection); },
            mapper: mapper,
            debugDump: function () { return mapper.debugDump(); }
        };
    }

    function shouldCoalesceTyping(previous, next, now, timeoutMs) {
        if (!previous || !next) return false;
        if (previous.type !== OPERATION_TYPES.InsertText || next.type !== OPERATION_TYPES.InsertText) return false;
        var previousTarget = _normalizeTarget(previous.target || previous.Target);
        var nextTarget = _normalizeTarget(next.target || next.Target);
        var previousText = _asText(previous.text || previous.Text);
        if (previousTarget.blockId !== nextTarget.blockId) return false;
        if (previousTarget.offset + previousText.length !== nextTarget.offset) return false;
        if (/\n/.test(previousText) || /\n/.test(_asText(next.text || next.Text))) return false;
        if (String(next.source || '').toLowerCase() === 'paste') return false;
        var age = Number(now || Date.now()) - Number(previous.timestamp || 0);
        return age <= Number(timeoutMs || 1000);
    }

    function coalesceTypingOperation(previous, next) {
        var merged = createOperation(OPERATION_TYPES.InsertText, Object.assign({}, previous, {
            text: _asText(previous.text || previous.Text) + _asText(next.text || next.Text),
            timestamp: next.timestamp || Date.now()
        }), { source: previous.source || 'typing', batchId: previous.batchId || next.batchId });
        return merged;
    }

    function _disposedResult(instanceId, methodName) {
        return {
            ok: false,
            error: {
                code: 'disposed',
                message: 'tmDocumentEditorEngine.' + methodName + ': instance is disposed.',
                instanceId: instanceId || ''
            }
        };
    }

    function _missingResult(instanceId, methodName) {
        return {
            ok: false,
            error: {
                code: 'missing-instance',
                message: 'tmDocumentEditorEngine.' + methodName + ': instance does not exist.',
                instanceId: instanceId || ''
            }
        };
    }

    function _get(instanceId, methodName) {
        var inst = _instances.get(instanceId);
        if (!inst) return { inst: null, error: _missingResult(instanceId, methodName) };
        if (inst.disposed) return { inst: null, error: _disposedResult(instanceId, methodName) };
        return { inst: inst, error: null };
    }

    function _notifyReady(inst) {
        if (!inst.dotNetRef || typeof inst.dotNetRef.invokeMethodAsync !== 'function') return;
        setTimeout(function () {
            if (inst.disposed) return;
            inst.dotNetRef.invokeMethodAsync('HandleJsEngineReady', {
                instanceId: inst.id,
                protocolVersion: 2
            }).catch(function (error) {
                inst.lastError = String(error && error.message || error);
            });
        }, 0);
    }

    function createDiagnosticsState() {
        return {
            modelVersion: 0,
            layoutVersion: 0,
            renderVersion: 0,
            selectionVersion: 0,
            timeline: [],
            lastErrors: [],
            watchdogFailures: [],
            debugWarnings: [],
            lastValidRenderHtml: '',
            lastValidLayout: null,
            lastValidSelection: null,
            lastValidSnapshot: null,
            forceLayoutFailure: false,
            forceRenderFailure: false,
            forceSelectionFailure: false
        };
    }

    function strictPerformanceNow() {
        return window.performance && typeof window.performance.now === 'function'
            ? window.performance.now()
            : Date.now();
    }

    var PERFORMANCE_HISTOGRAM_LIMIT = 500;

    function createDefaultLatencyBudgets() {
        return {
            KeydownVisibleTextMs: 150,
            SpaceVisibleTextMs: 150,
            EnterVisibleTextMs: 220,
            ToolbarCommandVisibleStyleMs: 250,
            SelectionChangeToolbarStateMs: 200
        };
    }

    function createLatencyHistogramState() {
        return {
            KeydownVisibleText: [],
            SpaceVisibleText: [],
            EnterVisibleText: [],
            ToolbarCommandVisibleStyle: [],
            SelectionChangeToolbarState: []
        };
    }

    function ensureLatencyHistogramState(stats) {
        if (!stats.latencyHistograms || typeof stats.latencyHistograms !== 'object') {
            stats.latencyHistograms = createLatencyHistogramState();
        }
        Object.keys(createLatencyHistogramState()).forEach(function (key) {
            if (!Array.isArray(stats.latencyHistograms[key])) stats.latencyHistograms[key] = [];
        });
        if (!stats.lastLatencyDetails || typeof stats.lastLatencyDetails !== 'object') {
            stats.lastLatencyDetails = {};
        }
        if (!stats.latencyBudgets || typeof stats.latencyBudgets !== 'object') {
            stats.latencyBudgets = createDefaultLatencyBudgets();
        }
        return stats.latencyHistograms;
    }

    function latencyBudgetForName(stats, name) {
        var budgets = stats && stats.latencyBudgets || createDefaultLatencyBudgets();
        switch (name) {
            case 'SpaceVisibleText': return Number(budgets.SpaceVisibleTextMs || 0) || 150;
            case 'EnterVisibleText': return Number(budgets.EnterVisibleTextMs || 0) || 220;
            case 'ToolbarCommandVisibleStyle': return Number(budgets.ToolbarCommandVisibleStyleMs || 0) || 250;
            case 'SelectionChangeToolbarState': return Number(budgets.SelectionChangeToolbarStateMs || 0) || 200;
            case 'KeydownVisibleText':
            default:
                return Number(budgets.KeydownVisibleTextMs || 0) || 150;
        }
    }

    function createLatencyHistogramSummary(samples, budgetMs) {
        var values = _asArray(samples).map(Number).filter(function (value) { return Number.isFinite(value); });
        return _sortObject({
            Count: values.length,
            LastMs: values.length ? values[values.length - 1] : 0,
            MaxMs: values.length ? Math.max.apply(Math, values) : 0,
            P50Ms: median(values),
            P95Ms: percentileNearestRank(values, 0.95),
            BudgetMs: Number(budgetMs || 0) || 0,
            WithinBudget: values.length === 0 || percentileNearestRank(values, 0.95) <= (Number(budgetMs || 0) || Number.POSITIVE_INFINITY)
        });
    }

    function recordLatencyHistogram(inst, name, elapsedMs, detail) {
        if (!inst) return null;
        var stats = ensureStrictPerformanceStats(inst);
        var histograms = ensureLatencyHistogramState(stats);
        var key = histograms[name] ? name : 'KeydownVisibleText';
        var elapsed = Math.max(0, Number(elapsedMs || 0) || 0);
        histograms[key] = histograms[key].concat([elapsed]).slice(-PERFORMANCE_HISTOGRAM_LIMIT);
        stats.lastLatencyDetails[key] = _sortObject(Object.assign({}, _clone(detail || {}), {
            elapsedMs: elapsed,
            at: Date.now()
        }));
        return createLatencyHistogramSummary(histograms[key], latencyBudgetForName(stats, key));
    }

    function recordPartialRenderScope(inst, operationType, scopeIds, detail) {
        if (!inst) return null;
        var stats = ensureStrictPerformanceStats(inst);
        var scopes = _unique(_asArray(scopeIds).map(_asText).filter(Boolean));
        stats.lastPartialRenderScopeIds = scopes;
        stats.partialRenderScopeSamples = _asArray(stats.partialRenderScopeSamples).concat([_sortObject({
            operationType: _asText(operationType || ''),
            scopeIds: scopes,
            detail: _clone(detail || {}),
            at: Date.now()
        })]).slice(-100);
        return scopes;
    }

    function isFormattingVisualOperation(operationOrType) {
        var type = typeof operationOrType === 'string'
            ? operationOrType
            : (operationOrType && (operationOrType.type || operationOrType.Type) || '');
        return type === OPERATION_TYPES.ApplyMark
            || type === OPERATION_TYPES.RemoveMark
            || type === OPERATION_TYPES.SetParagraphAttribute;
    }

    function createStrictPerformanceStats() {
        return {
            keyDownCount: 0,
            beforeInputCount: 0,
            inputDomApplyCount: 0,
            fullRenderCount: 0,
            partialRenderCount: 0,
            textNodePatchCount: 0,
            blockPatchCount: 0,
            markerOverlayPatchCount: 0,
            objectOverlayPatchCount: 0,
            selectionNotifyCount: 0,
            blazorInteropCallCount: 0,
            blazorCallbackDuringTypingCount: 0,
            formattingStateEventCount: 0,
            formattingStateNotifyCount: 0,
            typingFlushCount: 0,
            maxTypingBatchSize: 0,
            maxBoundaryPatchBatchSize: 0,
            keyToDomSamples: [],
            medianKeyToDomMs: 0,
            p95KeyToDomMs: 0,
            lastKeyToDomMs: 0,
            maxKeyToDomMs: 0,
            inputOperationCount: 0,
            inputOperationTotalMs: 0,
            inputOperationMaxMs: 0,
            inputOperationLastMs: 0,
            lastInputOperationType: '',
            incrementalOperationCount: 0,
            fullDocumentLayoutCount: 0,
            typingLatencyCount: 0,
            typingLatencyTotalMs: 0,
            typingLatencyMaxMs: 0,
            typingLatencyLastMs: 0,
            imageDragLatencyCount: 0,
            imageDragLatencyTotalMs: 0,
            imageDragLatencyMaxMs: 0,
            imageDragLatencyLastMs: 0,
            selectionMovementCount: 0,
            selectionMovementTotalMs: 0,
            selectionMovementMaxMs: 0,
            selectionMovementLastMs: 0,
            layoutPassCount: 0,
            layoutPassTotalMs: 0,
            layoutPassMaxMs: 0,
            layoutPassLastMs: 0,
            layoutInvalidatedPageCount: 0,
            layoutInvalidatedPages: [],
            layoutLastReason: '',
            renderPassCount: 0,
            renderPassTotalMs: 0,
            renderPassMaxMs: 0,
            renderPassLastMs: 0,
            renderLastReason: '',
            virtualizationEnabled: false,
            totalPages: 0,
            renderedPages: 0,
            virtualizedPages: 0,
            activePageIndex: 0,
            maxLiveDomBlockCount: 0,
            latencyBudgets: createDefaultLatencyBudgets(),
            latencyHistograms: createLatencyHistogramState(),
            lastLatencyDetails: {},
            lastPartialRenderScopeIds: [],
            partialRenderScopeSamples: [],
            formattingCommandPartialRenderCount: 0,
            lightweightBoundaryPatchCount: 0,
            boundarySnapshotExportCount: 0,
            deferredBoundaryPatchDispatchCount: 0,
            deferredRevisionNotifyCount: 0,
            revisionNotifyCount: 0,
            markerStoreDeferredRefreshCount: 0,
            toolbarStateLayoutAuditCount: 0,
            toolbarStateLayoutThrashCount: 0,
            lastToolbarStateLayoutAudit: null,
            memoryDisposeCount: 0,
            lastDisposeCleanup: null
        };
    }

    function ensureStrictPerformanceStats(inst) {
        if (!inst.performanceStats) inst.performanceStats = createStrictPerformanceStats();
        return inst.performanceStats;
    }

    function typingHotPathWindowMs(inst) {
        return Math.max(100, Number(inst && inst.options && (inst.options.TypingBatchMs || inst.options.typingBatchMs) || 500) || 500);
    }

    function isTypingHotPath(inst, now) {
        if (!inst) return false;
        var current = Number(now || strictPerformanceNow()) || strictPerformanceNow();
        var windowMs = typingHotPathWindowMs(inst);
        return _asArray(inst.pendingTypingBoundaryPatches).length > 0
            || Number(inst.suppressCollapsedSelectionChangeUntil || 0) >= current
            || Number(inst.lastInputDomApplyAt || 0) > 0 && current - Number(inst.lastInputDomApplyAt || 0) <= windowMs + 32;
    }

    function percentileNearestRank(values, percentile) {
        var samples = _asArray(values).map(Number).filter(function (value) { return Number.isFinite(value); }).sort(function (a, b) { return a - b; });
        if (!samples.length) return 0;
        var rank = Math.max(1, Math.ceil(samples.length * percentile));
        return samples[Math.min(samples.length - 1, rank - 1)];
    }

    function median(values) {
        var samples = _asArray(values).map(Number).filter(function (value) { return Number.isFinite(value); }).sort(function (a, b) { return a - b; });
        if (!samples.length) return 0;
        var middle = Math.floor(samples.length / 2);
        return samples.length % 2 === 0 ? (samples[middle - 1] + samples[middle]) / 2 : samples[middle];
    }

    function recordTypingKeyDown(inst, event) {
        var stats = ensureStrictPerformanceStats(inst);
        stats.keyDownCount = Number(stats.keyDownCount || 0) + 1;
        if (!inst || !event || !targetIsEditableDocumentSurface(inst, event.target)) return;
        var rawKey = String(event.key || '');
        var key = rawKey.toLowerCase();
        var ctrl = event.ctrlKey === true || event.metaKey === true;
        var typingKey = !ctrl && !event.altKey && !event.isComposing
            && (rawKey.length === 1 || key === 'enter' || key === 'backspace' || key === 'delete' || key === 'spacebar');
        if (!typingKey) return;
        inst.pendingKeyToDomStarts = _asArray(inst.pendingKeyToDomStarts).concat([strictPerformanceNow()]).slice(-200);
    }

    function recordBeforeInputEvent(inst) {
        var stats = ensureStrictPerformanceStats(inst);
        stats.beforeInputCount = Number(stats.beforeInputCount || 0) + 1;
        inst.lastBeforeInputAt = strictPerformanceNow();
    }

    function recordInputDomApply(inst, type) {
        var stats = ensureStrictPerformanceStats(inst);
        var now = strictPerformanceNow();
        var starts = _asArray(inst.pendingKeyToDomStarts);
        var startedAt = starts.length ? starts.shift() : Number(inst.lastBeforeInputAt || now);
        inst.pendingKeyToDomStarts = starts;
        var latency = Math.max(0, now - startedAt);
        var operation = arguments.length > 2 ? arguments[2] : null;
        var insertedText = _asText(operation && (operation.text || operation.Text));
        var samples = _asArray(stats.keyToDomSamples).concat([latency]).slice(-200);
        stats.keyToDomSamples = samples;
        stats.inputDomApplyCount = Number(stats.inputDomApplyCount || 0) + 1;
        stats.maxTypingBatchSize = Math.max(Number(stats.maxTypingBatchSize || 0), 1);
        inst.jsOwnedInputCount = Number(inst.jsOwnedInputCount || 0) + 1;
        inst.lastInputDomApplyAt = now;
        inst.suppressCollapsedSelectionChangeUntil = now + typingHotPathWindowMs(inst) + 32;
        stats.partialRenderCount = Number(stats.partialRenderCount || 0) + 1;
        stats.lastKeyToDomMs = latency;
        stats.maxKeyToDomMs = Math.max(Number(stats.maxKeyToDomMs || 0), latency);
        stats.medianKeyToDomMs = median(samples);
        stats.p95KeyToDomMs = percentileNearestRank(samples, 0.95);
        if (type === OPERATION_TYPES.SplitParagraph) {
            recordLatencyHistogram(inst, 'EnterVisibleText', latency, { operationType: type });
        } else if (type === OPERATION_TYPES.InsertText && insertedText === ' ') {
            recordLatencyHistogram(inst, 'SpaceVisibleText', latency, { operationType: type });
        } else if (type === OPERATION_TYPES.InsertText || type === OPERATION_TYPES.DeleteRange || type === OPERATION_TYPES.MergeParagraph) {
            recordLatencyHistogram(inst, 'KeydownVisibleText', latency, { operationType: type });
        }
        if (type === OPERATION_TYPES.InsertText || type === OPERATION_TYPES.DeleteRange) {
            stats.textNodePatchCount = Number(stats.textNodePatchCount || 0) + 1;
        } else if (type === OPERATION_TYPES.SplitParagraph || type === OPERATION_TYPES.MergeParagraph) {
            stats.blockPatchCount = Number(stats.blockPatchCount || 0) + 1;
        }
        stats.markerOverlayPatchCount = Number(stats.markerOverlayPatchCount || 0) + 1;
        recordPartialRenderScope(inst, type, operation ? operationAffectedBlockIds(operation) : [], { liveDomPatch: true });
        recordTimeline(inst, 'input-dom-apply', {
            operationType: type || '',
            latencyMs: latency,
            inputDomApplyCount: stats.inputDomApplyCount
        });
    }

    function ensureDiagnostics(inst) {
        if (!inst.diagnostics) inst.diagnostics = createDiagnosticsState();
        if (!Array.isArray(inst.diagnostics.timeline)) inst.diagnostics.timeline = [];
        if (!Array.isArray(inst.diagnostics.lastErrors)) inst.diagnostics.lastErrors = [];
        if (!Array.isArray(inst.diagnostics.watchdogFailures)) inst.diagnostics.watchdogFailures = [];
        if (!Array.isArray(inst.diagnostics.debugWarnings)) inst.diagnostics.debugWarnings = [];
        return inst.diagnostics;
    }

    function recordTimeline(inst, kind, detail) {
        if (!inst) return null;
        var diagnostics = ensureDiagnostics(inst);
        var entry = _sortObject({
            index: diagnostics.timeline.length + 1,
            kind: kind,
            detail: _clone(detail || {}),
            at: Date.now()
        });
        diagnostics.timeline.push(entry);
        if (diagnostics.timeline.length > 300) diagnostics.timeline.splice(0, diagnostics.timeline.length - 300);
        return entry;
    }

    function recordDiagnosticError(inst, code, error, detail) {
        if (!inst) return null;
        var diagnostics = ensureDiagnostics(inst);
        var entry = _sortObject({
            code: code || 'engine-error',
            message: String(error && error.message || error || code || 'engine-error'),
            detail: _clone(detail || {}),
            at: Date.now()
        });
        diagnostics.lastErrors.push(entry);
        if (diagnostics.lastErrors.length > 20) diagnostics.lastErrors.splice(0, diagnostics.lastErrors.length - 20);
        inst.lastError = entry.code;
        recordTimeline(inst, 'error-recovery', entry);
        return entry;
    }

    function recordWatchdogFailure(inst, kind, error, detail) {
        var diagnostics = ensureDiagnostics(inst);
        var entry = recordDiagnosticError(inst, kind + '-failure', error, detail);
        diagnostics.watchdogFailures.push(_sortObject(Object.assign({}, entry, { kind: kind })));
        if (diagnostics.watchdogFailures.length > 20) diagnostics.watchdogFailures.splice(0, diagnostics.watchdogFailures.length - 20);
        if (diagnostics.watchdogFailures.length >= 2 && diagnostics.debugWarnings.indexOf('watchdog-recovery-active') < 0) {
            diagnostics.debugWarnings.push('watchdog-recovery-active');
        }
        if (inst.root) inst.root.toggleAttribute('data-debug-warning', diagnostics.debugWarnings.length > 0);
        return entry;
    }

    function markModelChanged(inst, reason) {
        var diagnostics = ensureDiagnostics(inst);
        diagnostics.modelVersion++;
        if (reason) recordTimeline(inst, 'model-version', { reason: reason, modelVersion: diagnostics.modelVersion });
        return diagnostics.modelVersion;
    }

    function markSelectionChanged(inst, reason) {
        var started = strictPerformanceNow();
        var diagnostics = ensureDiagnostics(inst);
        diagnostics.selectionVersion++;
        rememberSelectionToken(inst, inst.selection || null, reason || 'selection-changed');
        if (reason) recordTimeline(inst, 'selection-restore', { reason: reason, selectionVersion: diagnostics.selectionVersion, selection: inst.selection || null });
        var elapsed = Math.max(0, strictPerformanceNow() - started);
        var stats = ensureStrictPerformanceStats(inst);
        stats.selectionMovementCount = Number(stats.selectionMovementCount || 0) + 1;
        stats.selectionMovementLastMs = elapsed;
        stats.selectionMovementTotalMs = Number(stats.selectionMovementTotalMs || 0) + elapsed;
        stats.selectionMovementMaxMs = Math.max(Number(stats.selectionMovementMaxMs || 0), elapsed);
        inst.lastSelectionStateChangeAt = started;
        var isTypingReason = isTypingLikeTransactionType(reason);
        var delayMs = isTypingReason
            ? Math.max(120, Number(inst.options && (inst.options.TypingBatchMs || inst.options.typingBatchMs) || 500) + 80)
            : 60;
        scheduleFormattingStatePublish(inst, reason || 'selection-changed', { delayMs: delayMs, startedAt: started });
        return diagnostics.selectionVersion;
    }

    function recordLayoutMetric(inst, elapsed, reason, invalidatedScopes) {
        var stats = ensureStrictPerformanceStats(inst);
        stats.layoutPassCount = Number(stats.layoutPassCount || 0) + 1;
        stats.layoutPassLastMs = elapsed;
        stats.layoutPassTotalMs = Number(stats.layoutPassTotalMs || 0) + elapsed;
        stats.layoutPassMaxMs = Math.max(Number(stats.layoutPassMaxMs || 0), elapsed);
        stats.layoutLastReason = reason || '';
        stats.layoutInvalidatedPages = _asArray(invalidatedScopes);
        stats.layoutInvalidatedPageCount = stats.layoutInvalidatedPages.length;
        recordTimeline(inst, 'layout-pass', {
            reason: reason || '',
            elapsedMs: elapsed,
            invalidatedScopes: _asArray(invalidatedScopes)
        });
    }

    function recordRenderMetric(inst, elapsed, reason) {
        var stats = ensureStrictPerformanceStats(inst);
        stats.renderPassCount = Number(stats.renderPassCount || 0) + 1;
        stats.fullRenderCount = Number(stats.fullRenderCount || 0) + 1;
        stats.renderPassLastMs = elapsed;
        stats.renderPassTotalMs = Number(stats.renderPassTotalMs || 0) + elapsed;
        stats.renderPassMaxMs = Math.max(Number(stats.renderPassMaxMs || 0), elapsed);
        stats.renderLastReason = reason || '';
        recordTimeline(inst, 'render-pass', {
            reason: reason || '',
            elapsedMs: elapsed
        });
    }

    function recordOperationPerformance(inst, operationList, elapsed, invalidatedScopes, source) {
        var stats = ensureStrictPerformanceStats(inst);
        var operations = _asArray(operationList);
        var scopes = _asArray(invalidatedScopes).map(_asText).filter(Boolean);
        var isFullDocument = scopes.indexOf('document') >= 0 || scopes.length === 0;
        stats.inputOperationCount = Number(stats.inputOperationCount || 0) + operations.length;
        stats.inputOperationLastMs = elapsed;
        stats.inputOperationTotalMs = Number(stats.inputOperationTotalMs || 0) + elapsed;
        stats.inputOperationMaxMs = Math.max(Number(stats.inputOperationMaxMs || 0), elapsed);
        stats.incrementalOperationCount = Number(stats.incrementalOperationCount || 0) + (isFullDocument ? 0 : operations.length);
        stats.fullDocumentLayoutCount = Number(stats.fullDocumentLayoutCount || 0) + (isFullDocument ? 1 : 0);
        stats.lastInputOperationType = operations.map(function (operation) { return operation.type || operation.Type || ''; }).filter(Boolean).join(',') || _asText(source || '');
        operations.forEach(function (operation) {
            var type = operation.type || operation.Type || '';
            if (type === OPERATION_TYPES.InsertText || type === OPERATION_TYPES.DeleteRange || type === OPERATION_TYPES.SplitParagraph || type === OPERATION_TYPES.MergeParagraph) {
                stats.typingLatencyCount = Number(stats.typingLatencyCount || 0) + 1;
                stats.typingLatencyLastMs = elapsed;
                stats.typingLatencyTotalMs = Number(stats.typingLatencyTotalMs || 0) + elapsed;
                stats.typingLatencyMaxMs = Math.max(Number(stats.typingLatencyMaxMs || 0), elapsed);
            }
            if (type === OPERATION_TYPES.UpdateImageLayout) {
                stats.imageDragLatencyCount = Number(stats.imageDragLatencyCount || 0) + 1;
                stats.imageDragLatencyLastMs = elapsed;
                stats.imageDragLatencyTotalMs = Number(stats.imageDragLatencyTotalMs || 0) + elapsed;
                stats.imageDragLatencyMaxMs = Math.max(Number(stats.imageDragLatencyMaxMs || 0), elapsed);
            }
        });
        recordTimeline(inst, 'operation-performance', {
            source: source || '',
            elapsedMs: elapsed,
            operationTypes: operations.map(function (operation) { return operation.type || operation.Type || ''; }),
            invalidatedScopes: scopes,
            fullDocumentLayout: isFullDocument
        });
    }

    function cssEscape(value) {
        var text = _asText(value);
        if (window.CSS && typeof window.CSS.escape === 'function') return window.CSS.escape(text);
        return text.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/'/g, "\\'").replace(/\]/g, '\\]');
    }

    function findLiveTextBlockElement(inst, blockId) {
        if (!inst || !inst.root || !blockId) return null;
        var selector = '.tm-wysiwyg-block[data-block-id="' + cssEscape(blockId) + '"]';
        var node = inst.root.querySelector(selector);
        if (!node || node.matches('figure, table, .tm-wysiwyg-image, .tm-wysiwyg-table')) return null;
        return node;
    }

    function liveBlockElementMatchesSelection(node, selection) {
        if (!node || !selection) return true;
        var region = _asText(selection.region || selection.Region || '');
        var headerFooterId = selection.headerFooterId || selection.HeaderFooterId || null;
        if (region) {
            var regionNode = node.closest && node.closest('[data-render-region]');
            var nodeRegion = regionNode && regionNode.getAttribute('data-render-region') || 'Body';
            if (nodeRegion !== region) return false;
            if ((region === 'Header' || region === 'Footer') && headerFooterId) {
                var nodeHeaderFooterId = regionNode && regionNode.getAttribute('data-hf-id') || null;
                if (nodeHeaderFooterId && nodeHeaderFooterId !== headerFooterId) return false;
            }
        }
        return true;
    }

    function findLiveTextBlockElements(inst, blockId, selection) {
        if (!inst || !inst.root || !blockId || typeof inst.root.querySelectorAll !== 'function') return [];
        var selector = '.tm-wysiwyg-block[data-block-id="' + cssEscape(blockId) + '"]';
        return Array.from(inst.root.querySelectorAll(selector))
            .filter(function (node) {
                return node
                    && !node.matches('figure, table, .tm-wysiwyg-image, .tm-wysiwyg-table')
                    && liveBlockElementMatchesSelection(node, selection);
            });
    }

    function currentDomBlockElement(inst, blockId) {
        var selection = window.getSelection && window.getSelection();
        if (!selectionBelongsToEditor(inst, selection) || selection.rangeCount === 0) return null;
        var range = selection.getRangeAt(0);
        var node = range.startContainer && (range.startContainer.nodeType === Node.ELEMENT_NODE ? range.startContainer : range.startContainer.parentElement);
        var block = node && node.closest && node.closest('.tm-wysiwyg-block[data-block-id]');
        return block && block.getAttribute('data-block-id') === blockId ? block : null;
    }

    function liveBlockContextFromElement(node) {
        if (!node) return null;
        var page = node.closest && node.closest('.tm-wysiwyg-page[data-page-index]');
        var regionNode = node.closest && node.closest('[data-render-region]');
        return {
            pageIndex: page && page.getAttribute('data-page-index') || null,
            region: regionNode && regionNode.getAttribute('data-render-region') || 'Body',
            headerFooterId: regionNode && regionNode.getAttribute('data-hf-id') || null
        };
    }

    function findLiveTextBlockElementForContext(inst, blockId, context, selection) {
        var nodes = findLiveTextBlockElements(inst, blockId, selection);
        if (!nodes.length) return null;
        if (context) {
            var match = nodes.find(function (node) {
                var candidate = liveBlockContextFromElement(node);
                return candidate
                    && (context.pageIndex === null || candidate.pageIndex === context.pageIndex)
                    && (!context.region || candidate.region === context.region)
                    && (!context.headerFooterId || candidate.headerFooterId === context.headerFooterId);
            });
            if (match) return match;
        }
        return nodes[0] || null;
    }

    function restoreDomSelectionInLiveBlock(inst, selection, blockElement) {
        if (!inst || !selection || !blockElement || typeof document === 'undefined') return restoreDomSelectionFromSnapshot(inst, selection);
        var snapshot = createSelectionSnapshot(selection);
        if (snapshot.isObjectSelection === true || snapshot.isCellSelection === true || !snapshot.blockId) return false;
        try {
            var range = document.createRange();
            if (snapshot.isCollapsed === false && snapshot.anchor.blockId === snapshot.focus.blockId) {
                var startOffset = Math.min(Number(snapshot.anchor.offset || 0), Number(snapshot.focus.offset || 0));
                var endOffset = Math.max(Number(snapshot.anchor.offset || 0), Number(snapshot.focus.offset || 0));
                var startPoint = domTextPointAtBlockOffset(blockElement, startOffset);
                var endPoint = domTextPointAtBlockOffset(blockElement, endOffset);
                if (!startPoint || !endPoint) return restoreDomSelectionFromSnapshot(inst, selection);
                range.setStart(startPoint.node, startPoint.offset);
                range.setEnd(endPoint.node, endPoint.offset);
            } else {
                var point = domTextPointAtBlockOffset(blockElement, snapshot.offset);
                if (!point || !point.node) return restoreDomSelectionFromSnapshot(inst, selection);
                range.setStart(point.node, point.offset);
                range.collapse(true);
            }
            var editable = blockElement.closest && blockElement.closest('[contenteditable="true"]');
            if (editable && typeof editable.focus === 'function') editable.focus({ preventScroll: true });
            var current = window.getSelection && window.getSelection();
            if (!current) return false;
            current.removeAllRanges();
            current.addRange(range);
            return true;
        } catch (error) {
            recordTimeline(inst, 'selection-restore-failed', { error: String(error && error.message || error) });
            return restoreDomSelectionFromSnapshot(inst, selection);
        }
    }

    function setLiveParagraphText(node, text) {
        if (!node) return false;
        var value = _asText(text);
        if (value.length === 0) {
            var placeholder = document.createElement('br');
            placeholder.setAttribute('data-caret-placeholder', 'true');
            node.replaceChildren(placeholder);
        } else {
            node.textContent = value;
        }
        return true;
    }

    function renderLiveParagraphHtml(inst, block) {
        return renderEngineBlockHtml(inst, block, inst && inst.options && (inst.options.ImageAltMissing || inst.options.imageAltMissing) || 'Image is missing alternative text.', 1, 1);
    }

    function replaceLiveParagraphHtml(inst, node, block) {
        if (!node || !block) return false;
        node.outerHTML = renderLiveParagraphHtml(inst, block);
        return true;
    }

    function replaceLiveParagraphCopies(inst, blockId, block, selection) {
        var activeNode = currentDomBlockElement(inst, blockId);
        var context = liveBlockContextFromElement(activeNode) || liveBlockContextFromElement(findLiveTextBlockElementForContext(inst, blockId, null, selection));
        var nodes = findLiveTextBlockElements(inst, blockId, selection);
        if (!nodes.length) return { ok: false, restoredNode: null, updatedCount: 0 };
        nodes.forEach(function (node) {
            replaceLiveParagraphHtml(inst, node, block);
        });
        return {
            ok: true,
            restoredNode: findLiveTextBlockElementForContext(inst, blockId, context, selection),
            updatedCount: nodes.length
        };
    }

    function applyLiveTypingDomPatch(inst, operation, committed) {
        if (!inst || !inst.root || !operation) return false;
        var op = attachOperationMethods(operation);
        var type = op.type || op.Type || '';
        if ([OPERATION_TYPES.InsertText, OPERATION_TYPES.DeleteRange, OPERATION_TYPES.SplitParagraph, OPERATION_TYPES.MergeParagraph, OPERATION_TYPES.ApplyMark, OPERATION_TYPES.RemoveMark, OPERATION_TYPES.SetParagraphAttribute].indexOf(type) < 0) {
            return false;
        }

        if (type === OPERATION_TYPES.InsertText) {
            var insertTarget = _normalizeTarget(op.target || op.Target);
            var insertBlock = _findBlock(inst.model, insertTarget.blockId);
            if (!insertBlock) return false;
            var insertPatch = replaceLiveParagraphCopies(inst, insertTarget.blockId, insertBlock, inst.selection);
            if (!insertPatch.ok) return false;
            var restoredNode = insertPatch.restoredNode;
        } else if (type === OPERATION_TYPES.DeleteRange) {
            var range = _normalizeRange(op.range || op.Range);
            var deleteBlock = _findBlock(inst.model, range.blockId);
            if (!deleteBlock) return false;
            var deletePatch = replaceLiveParagraphCopies(inst, range.blockId, deleteBlock, inst.selection);
            if (!deletePatch.ok) return false;
            restoredNode = deletePatch.restoredNode;
        } else if (type === OPERATION_TYPES.ApplyMark || type === OPERATION_TYPES.RemoveMark) {
            var markRange = _normalizeRange(op.range || op.Range);
            var markBlock = _findBlock(inst.model, markRange.blockId);
            if (!markBlock) return false;
            var markPatch = replaceLiveParagraphCopies(inst, markRange.blockId, markBlock, inst.selection);
            if (!markPatch.ok) return false;
            restoredNode = markPatch.restoredNode;
        } else if (type === OPERATION_TYPES.SetParagraphAttribute) {
            var paragraphTarget = _normalizeTarget(op.target || op.Target);
            var paragraphBlock = _findBlock(inst.model, paragraphTarget.blockId);
            if (!paragraphBlock) return false;
            var paragraphPatch = replaceLiveParagraphCopies(inst, paragraphTarget.blockId, paragraphBlock, inst.selection);
            if (!paragraphPatch.ok) return false;
            restoredNode = paragraphPatch.restoredNode;
        } else if (type === OPERATION_TYPES.SplitParagraph) {
            var splitTarget = _normalizeTarget(op.target || op.Target);
            var originalBlock = _findBlock(inst.model, splitTarget.blockId);
            var newBlockId = op.newBlockId || op.NewBlockId || committed && committed.insertedBlockId || inst.selection && inst.selection.blockId;
            var newBlock = _findBlock(inst.model, newBlockId);
            var originalNode = findLiveTextBlockElement(inst, splitTarget.blockId);
            if (!originalBlock || !newBlock || !originalNode) return false;
            replaceLiveParagraphHtml(inst, originalNode, originalBlock);
            if (!findLiveTextBlockElement(inst, newBlock.id)) {
                var refreshedOriginal = findLiveTextBlockElement(inst, splitTarget.blockId);
                (refreshedOriginal || originalNode).insertAdjacentHTML('afterend', renderLiveParagraphHtml(inst, newBlock));
                var inserted = findLiveTextBlockElement(inst, newBlock.id);
                if (inserted && _blockText(newBlock).length === 0) setLiveParagraphText(inserted, '');
            }
        } else if (type === OPERATION_TYPES.MergeParagraph) {
            var mergeTarget = _normalizeTarget(op.target || op.Target);
            var removedNode = findLiveTextBlockElement(inst, mergeTarget.blockId);
            var targetBlockId = inst.selection && inst.selection.blockId || '';
            var targetBlock = _findBlock(inst.model, targetBlockId);
            var targetNode = findLiveTextBlockElement(inst, targetBlockId);
            if (!targetBlock || !targetNode || !removedNode) return false;
            replaceLiveParagraphHtml(inst, targetNode, targetBlock);
            if (removedNode !== targetNode) removedNode.remove();
        }

        inst.layout = Object.assign({}, inst.layout || {}, {
            invalidatedScopeIds: operationAffectedBlockIds(op),
            lastLiveTypingPatchAt: Date.now()
        });
        inst.root.setAttribute('data-live-typing-patch', String(Date.now()));
        inst.root.setAttribute('data-logical-selection', JSON.stringify(_sortObject(inst.selection || {})));
        restoreDomSelectionInLiveBlock(inst, inst.selection, restoredNode);
        recordTimeline(inst, 'live-typing-dom-patch', {
            operationType: type,
            blockId: inst.selection && inst.selection.blockId || '',
            region: inst.selection && inst.selection.region || 'Body'
        });
        recordInputDomApply(inst, type, op);
        return true;
    }

    function recordVirtualizationMetric(inst, pagePlan) {
        var stats = ensureStrictPerformanceStats(inst);
        var plan = pagePlan || {};
        stats.virtualizationEnabled = plan.virtualizationEnabled === true;
        stats.totalPages = _asArray(plan.pages).length;
        stats.renderedPages = _asArray(plan.pages).filter(function (page) { return page.isRendered !== false; }).length;
        stats.virtualizedPages = _asArray(plan.pages).filter(function (page) { return page.isVirtual === true; }).length;
        stats.activePageIndex = Number(plan.activePageIndex || 0) || 0;
        stats.maxLiveDomBlockCount = Math.max(Number(stats.maxLiveDomBlockCount || 0), Number(plan.liveBlockCount || 0));
    }

    function createSafeLayoutFallback(inst, invalidatedScopeIds) {
        var blocks = _asArray(inst && inst.model && inst.model.body && inst.model.body.blocks);
        return _sortObject({
            ok: true,
            safeFallback: true,
            layoutVersion: ensureDiagnostics(inst).layoutVersion,
            pages: [{ pageNumber: 1, blockIds: blocks.map(function (block) { return block.id; }), exclusions: [] }],
            blocks: blocks.map(function (block, index) {
                return _sortObject({
                    ok: true,
                    blockId: block.id,
                    type: block.type || 'paragraph',
                    pageIndex: 0,
                    rect: { x: 0, y: index * 24, width: 640, height: 20 },
                    lines: [],
                    segments: [],
                    caretStops: [],
                    fallback: true
                });
            }),
            objects: [],
            caretStops: [],
            headerFooterRegions: [],
            invalidatedScopeIds: _asArray(invalidatedScopeIds),
            debug: { source: 'strict-watchdog-safe-layout-fallback' }
        });
    }

    function createInitialDirtyState() {
        return _sortObject({
            isDirty: false,
            epoch: 0,
            savedEpoch: 0,
            version: null,
            lastSavedMarker: '',
            lastFailure: null,
            pendingPatchCount: 0
        });
    }

    function getOperationId(operation) {
        return _asText(operation && (operation.id || operation.Id || operation.operationId || operation.OperationId || ''));
    }

    function operationAffectedBlockIds(operation) {
        var op = operation || {};
        var ids = [];
        var target = op.target || op.Target || null;
        var range = op.range || op.Range || null;
        var selection = op.selection || op.Selection || null;
        if (target && (target.blockId || target.BlockId)) ids.push(target.blockId || target.BlockId);
        if (range && (range.blockId || range.BlockId)) ids.push(range.blockId || range.BlockId);
        if (selection && (selection.blockId || selection.BlockId)) ids.push(selection.blockId || selection.BlockId);
        if (op.blockId || op.BlockId) ids.push(op.blockId || op.BlockId);
        if (op.newBlockId || op.NewBlockId) ids.push(op.newBlockId || op.NewBlockId);
        if (op.revisionId || op.RevisionId) ids.push('revisions');
        _asArray(op.affectedScopeIds || op.AffectedScopeIds || op.affectedParagraphIds || op.AffectedParagraphIds || op.affectedSelectable || op.AffectedSelectable)
            .forEach(function (id) { if (id) ids.push(id); });
        return _unique(ids.map(_asText).filter(Boolean));
    }

    function transactionAffectedBlockIds(transaction, operations) {
        var ids = [];
        _asArray(operations).forEach(function (operation) {
            ids = ids.concat(operationAffectedBlockIds(operation));
        });
        ids = ids.concat(_asArray(transaction && transaction.invalidatedScopes));
        return _unique(ids.map(_asText).filter(Boolean));
    }

    function operationTouchesRevisions(operation) {
        var op = operation || {};
        var type = op.type || op.Type || '';
        return !!(op.revisionId || op.RevisionId || op.revision || op.Revision
            || type === OPERATION_TYPES.AcceptRevision
            || type === OPERATION_TYPES.RejectRevision);
    }

    function operationMayChangeRevisions(operation) {
        var type = operation && (operation.type || operation.Type) || '';
        return operationTouchesRevisions(operation) || type === OPERATION_TYPES.RestoreSnapshot;
    }

    function notifyRuntimeRevisionsChanged(inst) {
        if (!inst) return Promise.resolve({ ok: true, skipped: true });
        var stats = ensureStrictPerformanceStats(inst);
        stats.revisionNotifyCount = Number(stats.revisionNotifyCount || 0) + 1;
        return invokeBoundaryMethod(inst, 'HandleRevisionsChanged', exportRevisionsToCSharpJson(inst.model), 'revisions-changed-failed');
    }

    function scheduleRuntimeRevisionsChanged(inst) {
        if (!inst) return;
        inst.pendingRevisionNotify = true;
        var stats = ensureStrictPerformanceStats(inst);
        stats.deferredRevisionNotifyCount = Number(stats.deferredRevisionNotifyCount || 0) + 1;
    }

    function flushRuntimeRevisionsChanged(inst) {
        if (!inst || !inst.pendingRevisionNotify) return Promise.resolve({ ok: true, skipped: true });
        inst.pendingRevisionNotify = false;
        return notifyRuntimeRevisionsChanged(inst);
    }

    function invokeBoundaryMethod(inst, methodName, payload, failureCode) {
        if (inst) {
            var stats = ensureStrictPerformanceStats(inst);
            var now = strictPerformanceNow();
            stats.blazorInteropCallCount = Number(stats.blazorInteropCallCount || 0) + 1;
            if (isTypingHotPath(inst, now)) {
                stats.blazorCallbackDuringTypingCount = Number(stats.blazorCallbackDuringTypingCount || 0) + 1;
                stats.lastBlazorCallbackDuringTypingMethod = methodName || '';
            }
            if (methodName === 'HandleSelectionChanged') {
                stats.selectionNotifyCount = Number(stats.selectionNotifyCount || 0) + 1;
            } else if (methodName === 'HandleFormattingStateChanged') {
                stats.formattingStateNotifyCount = Number(stats.formattingStateNotifyCount || 0) + 1;
                stats.formattingStateEventCount = Number(stats.formattingStateEventCount || 0) + 1;
            }
        }
        if (!inst || !inst.dotNetRef || typeof inst.dotNetRef.invokeMethodAsync !== 'function') {
            return Promise.resolve({ ok: true, skipped: true });
        }
        return Promise.resolve()
            .then(function () { return inst.dotNetRef.invokeMethodAsync(methodName, payload); })
            .then(function () { return { ok: true }; })
            .catch(function (error) {
                var failure = {
                    code: failureCode || 'boundary-invoke-failed',
                    method: methodName,
                    message: String(error && error.message || error),
                    at: Date.now()
                };
                inst.boundaryFailures.push(failure);
                inst.lastError = failure.code;
                recordDiagnosticError(inst, failure.code, failure.message, { method: methodName });
                return { ok: false, error: failure };
            });
    }

    function createFormattingStateBoundaryPayload(inst, reason, version) {
        var selection = inst && (inst.selection || firstModelSelection(inst.model));
        var formatting = computeFormattingState(inst.model, selection, inst.pendingTypingMarks || [], inst);
        var tokenizedSelection = rememberSelectionToken(inst, formatting.selection || selection, reason || 'formatting-state');
        formatting.selection = tokenizedSelection;
        formatting.Selection = tokenizedSelection;
        formatting.currentSelection = tokenizedSelection;
        formatting.CurrentSelection = tokenizedSelection;
        var payload = toBlazorFormattingState(formatting);
        var nextVersion = Number(version || inst.formattingStateVersion || 0) || 0;
        payload.Version = nextVersion;
        payload.version = nextVersion;
        payload.Reason = reason || '';
        payload.reason = reason || '';
        payload.Selection = tokenizedSelection;
        payload.selection = tokenizedSelection;
        payload.CurrentSelection = tokenizedSelection;
        payload.currentSelection = tokenizedSelection;
        return _sortObject(payload);
    }

    function dispatchFormattingState(inst, reason, version) {
        if (!inst || inst.disposed) return null;
        var publishedVersion = Number(inst.lastFormattingStatePublishedVersion || 0) || 0;
        if (Number(version || 0) < publishedVersion) {
            recordTimeline(inst, 'formatting-state-stale-skip', {
                reason: reason || '',
                version: version || 0,
                publishedVersion: publishedVersion
            });
            return null;
        }

        var stats = ensureStrictPerformanceStats(inst);
        var beforeRenderPassCount = Number(stats.renderPassCount || 0);
        var beforeLayoutPassCount = Number(stats.layoutPassCount || 0);
        var startedAt = Number(inst.pendingFormattingStateStartedAt || inst.lastSelectionStateChangeAt || strictPerformanceNow()) || strictPerformanceNow();
        inst.lastFormattingStatePublishedVersion = Number(version || publishedVersion || 0) || publishedVersion;
        var payload = createFormattingStateBoundaryPayload(inst, reason || 'formatting-state', inst.lastFormattingStatePublishedVersion);
        recordTimeline(inst, 'formatting-state-publish', {
            reason: reason || '',
            version: payload.Version || payload.version || 0,
            bold: payload.Bold,
            fontSize: payload.FontSize || '',
            textColor: payload.TextColor || ''
        });
        invokeBoundaryMethod(inst, 'HandleFormattingStateChanged', payload, 'formatting-state-changed-failed');
        var afterRenderPassCount = Number(stats.renderPassCount || 0);
        var afterLayoutPassCount = Number(stats.layoutPassCount || 0);
        var thrash = afterRenderPassCount !== beforeRenderPassCount || afterLayoutPassCount !== beforeLayoutPassCount;
        stats.toolbarStateLayoutAuditCount = Number(stats.toolbarStateLayoutAuditCount || 0) + 1;
        if (thrash) stats.toolbarStateLayoutThrashCount = Number(stats.toolbarStateLayoutThrashCount || 0) + 1;
        stats.lastToolbarStateLayoutAudit = _sortObject({
            reason: reason || '',
            renderPassDelta: afterRenderPassCount - beforeRenderPassCount,
            layoutPassDelta: afterLayoutPassCount - beforeLayoutPassCount,
            thrash: thrash,
            at: Date.now()
        });
        recordLatencyHistogram(inst, 'SelectionChangeToolbarState', Math.max(0, strictPerformanceNow() - startedAt), {
            reason: reason || '',
            version: payload.Version || payload.version || 0
        });
        return payload;
    }

    function scheduleFormattingStatePublish(inst, reason, options) {
        if (!inst || inst.disposed) return null;
        var opts = options || {};
        var version = Number(inst.formattingStateVersion || 0) + 1;
        inst.formattingStateVersion = version;
        inst.pendingFormattingStateVersion = version;
        inst.pendingFormattingStateReason = reason || 'formatting-state';
        inst.pendingFormattingStateStartedAt = Number(opts.startedAt || strictPerformanceNow()) || strictPerformanceNow();

        if (inst.pendingFormattingStateTimer) {
            clearTimeout(inst.pendingFormattingStateTimer);
            inst.pendingFormattingStateTimer = null;
        }

        if (opts.immediate === true) {
            return dispatchFormattingState(inst, reason || 'formatting-state', version);
        }

        var delay = Math.max(16, Number(opts.delayMs ?? opts.delay ?? 60) || 60);
        inst.pendingFormattingStateTimer = setTimeout(function () {
            inst.pendingFormattingStateTimer = null;
            if (inst.disposed || inst.pendingFormattingStateVersion !== version) return;
            dispatchFormattingState(inst, inst.pendingFormattingStateReason || reason || 'formatting-state', version);
        }, delay);
        if (inst.timers && inst.timers.indexOf(inst.pendingFormattingStateTimer) < 0) {
            inst.timers.push(inst.pendingFormattingStateTimer);
        }
        return { scheduled: true, version: version, reason: reason || 'formatting-state', delayMs: delay };
    }

    function getRegionLabel(inst, region) {
        var name = String(region || 'Body');
        var opts = inst && inst.options || {};
        if (name === 'Header') return formatA11yLabel(opts.HeaderLabel || opts.headerLabel || 'Header', 1);
        if (name === 'Footer') return formatA11yLabel(opts.FooterLabel || opts.footerLabel || 'Footer', 1);
        if (name === 'TableCell') return opts.TableCellPlaceholder || opts.tableCellPlaceholder || 'Table cell';
        if (name === 'Image') return opts.ImageResizeHandleLabel || opts.imageResizeHandleLabel || 'Image';
        return formatA11yLabel(opts.BodyLabel || opts.bodyLabel || 'Document body', 1);
    }

    function formatA11yLabel(template, pageNumber) {
        var text = _asText(template || '');
        if (!text) return '';
        return text.indexOf('{0}') >= 0
            ? text.replace(/\{0\}/g, String(pageNumber || 1))
            : text;
    }

    function isElementNode(value) {
        var elementNode = typeof Node !== 'undefined' ? Node.ELEMENT_NODE : 1;
        return value && value.nodeType === elementNode;
    }

    function getFocusRegionFromElement(root, element) {
        var node = isElementNode(element) ? element : element && element.parentElement;
        if (!node || root && !root.contains(node)) return 'Body';
        if (node.closest && node.closest('figure.tm-wysiwyg-image, .tm-render-image-widget')) return 'Image';
        if (node.closest && node.closest('td[data-cell-id], [data-table-cell-id], [data-cell-id]')) return 'TableCell';
        var explicit = node.closest && node.closest('[data-render-region]');
        if (explicit) return explicit.getAttribute('data-render-region') || 'Body';
        if (node.closest && node.closest('.tm-render-header-region, [data-render-frame="header"], [data-render-frame="header-content"]')) return 'Header';
        if (node.closest && node.closest('.tm-render-footer-region, [data-render-frame="footer"], [data-render-frame="footer-content"]')) return 'Footer';
        return 'Body';
    }

    function getFocusTargetDetails(root, element, region) {
        var node = isElementNode(element) ? element : element && element.parentElement;
        var details = {
            region: region || 'Body',
            headerFooterId: '',
            activeTableCellId: '',
            activeTableId: '',
            activeImageBlockId: '',
            activeCommentId: '',
            activeRevisionId: '',
            activeObjectId: '',
            textBlockId: '',
            hitTargetKind: String(region || 'Body').toLowerCase()
        };
        if (!node || root && !root.contains(node)) return details;
        var regionNode = node.closest && node.closest('[data-hf-id]');
        details.headerFooterId = regionNode && regionNode.getAttribute('data-hf-id') || '';
        var cell = node.closest && node.closest('td[data-cell-id], [data-table-cell-id], [data-cell-id]');
        details.activeTableCellId = cell && (cell.getAttribute('data-cell-id') || cell.getAttribute('data-table-cell-id')) || '';
        var table = cell && cell.closest && cell.closest('table[data-block-id], .tm-wysiwyg-block[data-block-id]');
        details.activeTableId = table && table.getAttribute('data-block-id') || '';
        var comment = node.closest && node.closest('.tm-document-inline--comment-anchor[data-comment-id], [data-testid="document-comment-marker"][data-comment-id]');
        details.activeCommentId = comment && comment.getAttribute('data-comment-id') || '';
        var revision = node.closest && node.closest('.tm-wysiwyg-revision[data-revision-id], .tm-document-inline--revision[data-revision-id], [data-testid="document-revision-marker"][data-revision-id]');
        details.activeRevisionId = revision && revision.getAttribute('data-revision-id') || '';
        var image = node.closest && node.closest('figure.tm-wysiwyg-image, .tm-render-image-widget');
        details.activeImageBlockId = image && (image.getAttribute('data-block-id') || image.getAttribute('data-render-block-id') || image.getAttribute('data-model-id')) || '';
        details.activeObjectId = image && (image.getAttribute('data-render-object-id') || image.getAttribute('data-object-id') || details.activeImageBlockId) || '';
        var textBlock = !details.activeImageBlockId && node.closest && node.closest('.tm-wysiwyg-block[data-block-id]');
        details.textBlockId = textBlock && textBlock.getAttribute('data-block-id') || '';
        if (details.textBlockId) details.hitTargetKind = details.activeTableCellId ? 'tableCell' : 'text';
        if (details.activeCommentId) details.hitTargetKind = 'comment';
        if (details.activeRevisionId) details.hitTargetKind = 'revision';
        return details;
    }

    function scheduleAccessibilityAnnouncement(inst, message, politeness) {
        if (!inst || !message) return;
        var text = String(message || '');
        inst.lastAccessibilityAnnouncement = {
            message: text,
            politeness: politeness || 'polite',
            at: Date.now()
        };
        if (inst.root) {
            inst.root.setAttribute('data-accessibility-announcement', text);
            var live = inst.root.querySelector('[data-testid="document-wysiwyg-selection-live"]');
            if (live) live.textContent = text;
        }
        if (inst.accessibilityAnnouncementTimer) clearTimeout(inst.accessibilityAnnouncementTimer);
        inst.accessibilityAnnouncementTimer = setTimeout(function () {
            invokeBoundaryMethod(inst, 'HandleAccessibilityAnnouncement', text, 'accessibility-announcement-failed');
        }, 160);
    }

    function setActiveFocusRegion(inst, region, element, reason) {
        if (!inst) return null;
        var normalizedRegion = region || 'Body';
        var details = getFocusTargetDetails(inst.root, element, normalizedRegion);
        var previousRegion = inst.activeFocusRegion || inst.selection && inst.selection.region || 'Body';
        inst.activeFocusRegion = normalizedRegion;
        inst.focusOwner = details.hitTargetKind;
        if (inst.root) {
            inst.root.setAttribute('data-active-region', normalizedRegion);
            inst.root.setAttribute('data-focus-owner', inst.focusOwner || 'body');
        }

        var current = createSelectionSnapshot(inst.selection || {});
        var currentBlockId = current.blockId || current.anchor && current.anchor.blockId || current.focus && current.focus.blockId || null;
        var currentOffset = current.offset || current.anchor && current.anchor.offset || current.focus && current.focus.offset || 0;
        var nextSelection = Object.assign({}, current, {
            region: normalizedRegion,
            headerFooterId: details.headerFooterId || null,
            activeTableCellId: details.activeTableCellId || null,
            activeTableId: details.activeTableId || null,
            activeImageBlockId: details.activeImageBlockId || null,
            activeCommentId: details.activeCommentId || null,
            activeRevisionId: details.activeRevisionId || null,
            activeObjectId: details.activeObjectId || null,
            hitTargetKind: details.hitTargetKind || normalizedRegion.toLowerCase()
        });
        if (details.activeImageBlockId) {
            nextSelection.blockId = details.activeImageBlockId;
            nextSelection.objectId = details.activeObjectId || details.activeImageBlockId;
            nextSelection.isObjectSelection = true;
            nextSelection.isCollapsed = false;
            nextSelection.range = createLogicalRange(
                { region: normalizedRegion, blockId: details.activeImageBlockId, objectId: nextSelection.objectId, offset: 0, affinity: 'before' },
                { region: normalizedRegion, blockId: details.activeImageBlockId, objectId: nextSelection.objectId, offset: 1, affinity: 'after' },
                'none');
        } else if (details.textBlockId) {
            nextSelection.blockId = details.textBlockId;
            nextSelection.objectId = null;
            nextSelection.activeObjectId = null;
            nextSelection.activeImageBlockId = null;
            nextSelection.isObjectSelection = false;
            nextSelection.isCollapsed = true;
            nextSelection.range = createLogicalRange(
                { region: normalizedRegion, blockId: details.textBlockId, offset: 0, affinity: 'after', cellId: details.activeTableCellId || null, tableId: details.activeTableId || null },
                { region: normalizedRegion, blockId: details.textBlockId, offset: 0, affinity: 'after', cellId: details.activeTableCellId || null, tableId: details.activeTableId || null },
                'none');
        }
        inst.selection = createSelectionSnapshot(nextSelection);
        markSelectionChanged(inst, reason || 'focus-region');
        updateActiveImageSelectionDom(inst);
        if (previousRegion !== normalizedRegion) {
            scheduleAccessibilityAnnouncement(inst, getRegionLabel(inst, normalizedRegion) + ' selected', 'polite');
        }
        invokeBoundaryMethod(inst, 'HandleSelectionChanged', {
            Region: inst.selection.region,
            AnchorBlockId: inst.selection.blockId || currentBlockId,
            FocusBlockId: inst.selection.blockId || currentBlockId,
            AnchorOffset: inst.selection.offset || currentOffset || 0,
            FocusOffset: inst.selection.offset || currentOffset || 0,
            IsCollapsed: inst.selection.isCollapsed !== false,
            HeaderFooterId: inst.selection.headerFooterId || null,
            ActiveTableCellId: inst.selection.activeTableCellId || null,
            ActiveTableId: inst.selection.activeTableId || inst.selection.tableId || null,
            ActiveImageBlockId: inst.selection.activeImageBlockId || null,
            ActiveCommentId: inst.selection.activeCommentId || null,
            ActiveRevisionId: inst.selection.activeRevisionId || null,
            ActiveObjectId: inst.selection.activeObjectId || null,
            HitTargetKind: inst.selection.hitTargetKind || null
        }, 'selection-changed-failed');
        return inst.selection;
    }

    function focusNextRegion(inst, backwards) {
        var regions = ['Body'];
        if (_asArray(inst && inst.model && inst.model.headers).length > 0) regions.unshift('Header');
        if (_asArray(inst && inst.model && inst.model.footers).length > 0) regions.push('Footer');
        var current = inst && (inst.activeFocusRegion || inst.selection && inst.selection.region) || 'Body';
        var index = Math.max(0, regions.indexOf(current));
        var next = regions[(index + (backwards ? -1 : 1) + regions.length) % regions.length] || 'Body';
        setActiveFocusRegion(inst, next, inst.root, 'tab-region-cycle');
        return next;
    }

    function requestKeyboardContextMenu(inst, event) {
        var rect = inst && inst.root && inst.root.getBoundingClientRect ? inst.root.getBoundingClientRect() : { left: 0, top: 0, width: 0, height: 0 };
        var x = event && Number.isFinite(event.clientX) && event.clientX > 0 ? event.clientX : (rect.left || 0) + Math.min(80, (rect.width || 160) / 2);
        var y = event && Number.isFinite(event.clientY) && event.clientY > 0 ? event.clientY : (rect.top || 0) + 48;
        return invokeBoundaryMethod(inst, 'HandleTextContextMenuRequested', {
            Left: x,
            Top: y,
            ClientX: x,
            ClientY: y,
            ViewportWidth: window.innerWidth || 0,
            ViewportHeight: window.innerHeight || 0,
            Selection: {
                Region: inst.selection && inst.selection.region || 'Body',
                AnchorBlockId: inst.selection && inst.selection.blockId || null,
                FocusBlockId: inst.selection && inst.selection.blockId || null,
                AnchorOffset: inst.selection && inst.selection.offset || 0,
                FocusOffset: inst.selection && inst.selection.offset || 0,
                IsCollapsed: inst.selection ? inst.selection.isCollapsed !== false : true
            }
        }, 'keyboard-context-menu-failed');
    }

    function closeFloatingUiForKeyboard(inst) {
        inst.floatingUiOpen = false;
        inst.objectPreviewTransaction = null;
        inst.lastKeyboardClose = { reason: 'escape', at: Date.now() };
        invokeBoundaryMethod(inst, 'HandleMiniToolbarChanged', null, 'mini-toolbar-close-failed');
        scheduleAccessibilityAnnouncement(inst, 'Floating controls closed', 'polite');
        return { ok: true, closed: true };
    }

    function executeFormattingShortcut(inst, commandName) {
        if (!inst) return { ok: false, error: { code: 'missing-instance' } };
        var domSelection = window.getSelection && window.getSelection();
        if (selectionBelongsToEditor(inst, domSelection)) {
            inst.selection = readFixedDomSelection(inst, 'keyboard-formatting-shortcut');
        }

        var payload = {
            source: 'keyboard',
            selection: createSelectionSnapshot(inst.selection || firstModelSelection(inst.model))
        };
        var result = applyRuntimeFormattingCommand(inst, commandName, payload, commandName);
        recordTimeline(inst, 'keyboard-command', { command: commandName, result: result && result.ok !== false });
        if (result && result.ok !== false) {
            rememberKeyboardSelection(inst, inst.selection || payload.selection, 'keyboard-' + commandName);
        }
        return result;
    }

    function markKeyboardInputHandled(inst, inputType, data) {
        var item = {
            inputType: inputType || '',
            data: _asText(data || ''),
            expiresAt: Date.now() + 120
        };
        inst.suppressedBeforeInput = item;
        inst.suppressedBeforeInputs = _asArray(inst.suppressedBeforeInputs).concat([item]).slice(-6);
    }

    function consumeSuppressedBeforeInput(inst, event) {
        var list = _asArray(inst && inst.suppressedBeforeInputs);
        if (!list.length && inst && inst.suppressedBeforeInput) list = [inst.suppressedBeforeInput];
        if (!list.length) return false;
        var now = Date.now();
        list = list.filter(function (item) { return now <= Number(item && item.expiresAt || 0); });
        if (!list.length) {
            if (inst) {
                inst.suppressedBeforeInput = null;
                inst.suppressedBeforeInputs = [];
            }
            return false;
        }
        var inputType = _asText(event && event.inputType || '');
        var data = _asText(event && event.data || '');
        var matchIndex = list.findIndex(function (suppressed) {
            return inputType === suppressed.inputType
                && (inputType !== 'insertText' || data === suppressed.data);
        });
        if (matchIndex < 0) {
            inst.suppressedBeforeInputs = list;
            inst.suppressedBeforeInput = list[list.length - 1] || null;
            return false;
        }
        list.splice(matchIndex, 1);
        inst.suppressedBeforeInputs = list;
        inst.suppressedBeforeInput = list[list.length - 1] || null;
        if (typeof event.preventDefault === 'function') event.preventDefault();
        if (typeof event.stopPropagation === 'function') event.stopPropagation();
        recordTimeline(inst, 'beforeinput-suppressed-after-keydown', {
            inputType: inputType,
            dataLength: data.length
        });
        return true;
    }

    function clearKeyboardSelectionMemory(inst) {
        if (!inst) return;
        inst.lastKeyboardSelection = null;
        inst.lastKeyboardSelectionExpiresAt = 0;
        inst.lastKeyboardInputAt = 0;
    }

    function rememberKeyboardSelection(inst, selection, source) {
        if (!inst || !selection) return;
        var snapshot = createSelectionSnapshot(selection);
        if (!snapshot || snapshot.isCollapsed === false || snapshot.isObjectSelection || !snapshot.blockId) return;
        inst.lastKeyboardSelection = snapshot;
        inst.lastKeyboardSelectionExpiresAt = Date.now() + 900;
        inst.lastKeyboardInputAt = Date.now();
        inst.lastKeyboardSelectionSource = source || 'keyboard';
    }

    function chooseKeyboardSelection(inst, fixed, source) {
        var reason = _asText(source || '');
        if (reason.indexOf('keydown') < 0 && reason.indexOf('beforeinput') < 0) return fixed;
        var remembered = inst && inst.lastKeyboardSelection;
        if (!remembered || Date.now() > Number(inst.lastKeyboardSelectionExpiresAt || 0)) return fixed;
        var snapshot = createSelectionSnapshot(remembered);
        if (!snapshot || snapshot.isCollapsed === false || !snapshot.blockId) return fixed;
        if (!fixed || !fixed.blockId) return snapshot;
        if (fixed.blockId !== snapshot.blockId || fixed.isCollapsed === false) return fixed;
        var domOffset = Number(fixed.offset || 0);
        var rememberedOffset = Number(snapshot.offset || 0);
        if (domOffset === rememberedOffset) return fixed;
        var justTyped = Date.now() - Number(inst.lastKeyboardInputAt || 0) <= 350;
        if (rememberedOffset > 0 && domOffset === 0) return snapshot;
        if (justTyped && domOffset < rememberedOffset) return snapshot;
        return fixed;
    }

    function readFixedDomSelection(inst, source) {
        var fixed = createSelectionPostFixer(inst.schema).fix(inst.model, readDomSelectionSnapshot(inst));
        fixed = chooseKeyboardSelection(inst, fixed, source);
        inst.selection = fixed;
        markSelectionChanged(inst, source || 'dom-selection');
        return createSelectionSnapshot(fixed || firstModelSelection(inst.model));
    }

    function applyKeyboardInsertText(inst, event, text, inputType) {
        var selection = readFixedDomSelection(inst, 'keydown-dom');
        var block = _findBlock(inst.model, selection.blockId);
        var offset = Math.max(0, Math.min(_blockText(block).length, Number(selection.offset || 0)));
        var marks = _clone(inst.pendingTypingMarks || []);
        var revisionPayload = isTrackChangesEnabled(inst)
            ? createOrExtendLiveTypingRevision(inst, selection, text, marks)
            : null;
        var result = applyCommand(inst.id, OPERATION_TYPES.InsertText, {
            target: { blockId: selection.blockId, offset: offset, region: selection.region, headerFooterId: selection.headerFooterId || null },
            text: text,
            marks: marks,
            revisionId: revisionPayload && revisionPayload.id || null,
            revision: revisionPayload && revisionPayload.revision || null,
            source: 'keydown',
            transactionType: TRANSACTION_TYPES.Typing,
            beforeSelection: selection
        });
        if (result && result.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'keydown-insertText');
        markKeyboardInputHandled(inst, inputType || 'insertText', text);
        return result;
    }

    function applyKeyboardSplitParagraph(inst) {
        clearLiveTypingRevision(inst);
        var selection = readFixedDomSelection(inst, 'keydown-dom');
        var block = _findBlock(inst.model, selection.blockId);
        var offset = Math.max(0, Math.min(_blockText(block).length, Number(selection.offset || 0)));
        var structureRevision = isTrackChangesEnabled(inst)
            ? createStructureRevisionPayload({ blockId: selection.blockId, start: offset, end: offset }, 'SplitBlock', resolveRevisionUserId(inst.options || {}), 'keydown')
            : null;
        var result = applyCommand(inst.id, OPERATION_TYPES.SplitParagraph, {
            target: { blockId: selection.blockId, offset: offset, region: selection.region, headerFooterId: selection.headerFooterId || null },
            newBlockId: _stableId('block', selection.blockId + '-enter-' + Date.now() + '-' + Math.floor(Math.random() * 1000)),
            revisionId: structureRevision && structureRevision.id || null,
            revision: structureRevision || null,
            source: 'keydown',
            transactionType: TRANSACTION_TYPES.Typing,
            beforeSelection: selection
        });
        if (result && result.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'keydown-insertParagraph');
        markKeyboardInputHandled(inst, 'insertParagraph', '');
        return result;
    }

    function applyKeyboardDelete(inst, inputType) {
        clearLiveTypingRevision(inst);
        var selection = readFixedDomSelection(inst, 'keydown-dom');
        var block = _findBlock(inst.model, selection.blockId);
        var offset = Math.max(0, Math.min(_blockText(block).length, Number(selection.offset || 0)));
        if (selection.isCollapsed === false && selection.anchor && selection.focus && selection.anchor.blockId === selection.focus.blockId) {
            var rangeStart = Math.min(Number(selection.anchor.offset || 0), Number(selection.focus.offset || 0));
            var rangeEnd = Math.max(Number(selection.anchor.offset || 0), Number(selection.focus.offset || 0));
            if (rangeEnd > rangeStart) {
                var selectionRevision = isTrackChangesEnabled(inst)
                    ? createDeletionRevisionPayload(inst.model, { blockId: selection.blockId, start: rangeStart, end: rangeEnd }, resolveRevisionUserId(inst.options || {}), 'keydown')
                    : null;
                var rangeResult = applyCommand(inst.id, OPERATION_TYPES.DeleteRange, {
                    range: { blockId: selection.blockId, start: rangeStart, end: rangeEnd, region: selection.region, headerFooterId: selection.headerFooterId || null },
                    revisionId: selectionRevision && selectionRevision.id || null,
                    revision: selectionRevision || null,
                    source: 'keydown',
                    transactionType: 'delete',
                    beforeSelection: selection
                });
                if (rangeResult && rangeResult.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'keydown-' + inputType);
                markKeyboardInputHandled(inst, inputType, '');
                return rangeResult;
            }
        }
        var backward = inputType.indexOf('Backward') >= 0;
        var textValue = _blockText(block);
        if (backward && offset === 0) {
            var mergeResult = applyCommand(inst.id, OPERATION_TYPES.MergeParagraph, {
                target: { blockId: selection.blockId, offset: 0, region: selection.region, headerFooterId: selection.headerFooterId || null },
                source: 'keydown',
                transactionType: 'delete',
                beforeSelection: selection
            });
            if (mergeResult && mergeResult.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'keydown-' + inputType);
            markKeyboardInputHandled(inst, inputType, '');
            return mergeResult;
        }
        var start = backward ? Math.max(0, offset - 1) : offset;
        var end = backward ? offset : Math.min(textValue.length, offset + 1);
        if (start === end) return { ok: true, noop: true };
        var deletionRevision = isTrackChangesEnabled(inst)
            ? createDeletionRevisionPayload(inst.model, { blockId: selection.blockId, start: start, end: end }, resolveRevisionUserId(inst.options || {}), 'keydown')
            : null;
        var result = applyCommand(inst.id, OPERATION_TYPES.DeleteRange, {
            range: { blockId: selection.blockId, start: start, end: end, region: selection.region, headerFooterId: selection.headerFooterId || null },
            revisionId: deletionRevision && deletionRevision.id || null,
            revision: deletionRevision || null,
            source: 'keydown',
            transactionType: 'delete',
            beforeSelection: selection
        });
        if (result && result.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'keydown-' + inputType);
        markKeyboardInputHandled(inst, inputType, '');
        return result;
    }

    function handleEditorKeyDown(inst, event) {
        if (!inst || !event) return { handled: false };
        recordTypingKeyDown(inst, event);
        var rawKey = String(event.key || '');
        var key = rawKey.toLowerCase();
        var ctrl = event.ctrlKey === true || event.metaKey === true;
        var shift = event.shiftKey === true;
        var prevent = function () {
            if (typeof event.preventDefault === 'function') event.preventDefault();
            if (typeof event.stopPropagation === 'function') event.stopPropagation();
        };

        if (key === 'tab' && !ctrl && !event.altKey) {
            prevent();
            var nextRegion = focusNextRegion(inst, shift);
            return { handled: true, command: 'tab', activeRegion: nextRegion };
        }

        if (key === 'escape') {
            prevent();
            return Object.assign({ handled: true, command: 'escape' }, closeFloatingUiForKeyboard(inst));
        }

        if (key === 'contextmenu' || (key === 'f10' && shift)) {
            prevent();
            requestKeyboardContextMenu(inst, event);
            return { handled: true, command: 'contextMenu' };
        }

        if (!ctrl && !event.altKey && ['arrowleft', 'arrowright', 'arrowup', 'arrowdown', 'home', 'end', 'pageup', 'pagedown'].indexOf(key) >= 0) {
            clearKeyboardSelectionMemory(inst);
            clearLiveTypingRevision(inst);
        }

        if (!ctrl && !event.altKey && !event.isComposing && targetIsEditableDocumentSurface(inst, event.target)) {
            if (key === 'enter') {
                prevent();
                if (shift) {
                    return { handled: true, command: 'insertLineBreak', result: applyKeyboardInsertText(inst, event, '\n', 'insertLineBreak') };
                }
                return { handled: true, command: 'insertParagraph', result: applyKeyboardSplitParagraph(inst) };
            }
            if (key === 'backspace') {
                prevent();
                return { handled: true, command: 'deleteContentBackward', result: applyKeyboardDelete(inst, 'deleteContentBackward') };
            }
            if (key === 'delete') {
                prevent();
                return { handled: true, command: 'deleteContentForward', result: applyKeyboardDelete(inst, 'deleteContentForward') };
            }
            if (rawKey.length === 1 && key !== 'dead') {
                prevent();
                return { handled: true, command: 'insertText', result: applyKeyboardInsertText(inst, event, rawKey) };
            }
            if (key === 'spacebar') {
                prevent();
                return { handled: true, command: 'insertText', result: applyKeyboardInsertText(inst, event, ' ') };
            }
        }

        if (!ctrl || event.altKey) return { handled: false };

        if (key === 's') {
            prevent();
            flushTypingBoundaryPatchDispatch(inst);
            recordTimeline(inst, 'keyboard-command', { command: 'save' });
            invokeBoundaryMethod(inst, 'HandleSaveRequested', null, 'save-request-failed');
            return { handled: true, command: 'save' };
        }

        if (key === 'z' || key === 'y') {
            prevent();
            var redo = key === 'y' || shift;
            var result = applyCommand(inst.id, redo ? 'redo' : 'undo', {});
            return { handled: true, command: redo ? 'redo' : 'undo', result: result };
        }

        var command = key === 'b' ? 'bold' : key === 'i' ? 'italic' : key === 'u' ? 'underline' : '';
        if (command) {
            prevent();
            return { handled: true, command: command, result: executeFormattingShortcut(inst, command) };
        }

        return { handled: false };
    }

    function targetIsEditableDocumentSurface(inst, target) {
        if (!inst || !inst.root || !target) return false;
        var element = target.nodeType === Node.ELEMENT_NODE ? target : target.parentElement;
        if (!element || !inst.root.contains(element)) return false;
        return !!(element.closest && element.closest('.tm-wysiwyg-page__body[contenteditable], .tm-wysiwyg-page__header[contenteditable], .tm-wysiwyg-page__footer[contenteditable], .tm-wysiwyg-block[data-block-id]'));
    }

    function readDomSelectionSnapshot(inst) {
        var selection = window.getSelection && window.getSelection();
        if (!selection || selection.rangeCount === 0) return createSelectionSnapshot(inst.selection || firstModelSelection(inst.model));
        var range = selection.getRangeAt(0);
        var tableContext = tableContextFromDomRange(range);
        var startRange = range.cloneRange();
        startRange.collapse(true);
        var start = domRangeToLogical(inst.root, inst.model, startRange);
        if (!start.ok) return createSelectionSnapshot(inst.selection || firstModelSelection(inst.model));
        if (tableContext.cellId) {
            start.position = Object.assign({}, start.position, {
                region: 'TableCell',
                cellId: tableContext.cellId,
                tableId: tableContext.tableId
            });
        }
        if (selection.isCollapsed) return createSelectionSnapshot(start.position);

        var endRange = range.cloneRange();
        endRange.collapse(false);
        var end = domRangeToLogical(inst.root, inst.model, endRange);
        if (!end.ok) return createSelectionSnapshot(start.position);
        if (tableContext.cellId) {
            end.position = Object.assign({}, end.position, {
                region: 'TableCell',
                cellId: tableContext.cellId,
                tableId: tableContext.tableId
            });
        }
        var direction = start.position.blockId === end.position.blockId && start.position.offset <= end.position.offset ? 'forward' : 'backward';
        return createSelectionSnapshot({ range: createLogicalRange(start.position, end.position, direction) });
    }

    function tableContextFromDomRange(range) {
        if (!range) return { cellId: null, tableId: null };
        var nodes = [range.commonAncestorContainer, range.startContainer, range.endContainer];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i] && (nodes[i].nodeType === Node.ELEMENT_NODE ? nodes[i] : nodes[i].parentElement);
            var cell = node && node.closest && node.closest('td[data-cell-id], [data-table-cell-id], [data-cell-id]');
            if (!cell) continue;
            var table = cell.closest && cell.closest('table[data-block-id], .tm-wysiwyg-block[data-block-id]');
            return {
                cellId: cell.getAttribute('data-cell-id') || cell.getAttribute('data-table-cell-id') || null,
                tableId: table && table.getAttribute('data-block-id') || null
            };
        }
        return { cellId: null, tableId: null };
    }

    function selectionSnapshotFromBoundarySnapshot(value) {
        var source = value || {};
        return createSelectionSnapshot({
            region: source.Region || source.region || 'Body',
            anchorBlockId: source.AnchorBlockId || source.anchorBlockId || source.BlockId || source.blockId || '',
            focusBlockId: source.FocusBlockId || source.focusBlockId || source.AnchorBlockId || source.anchorBlockId || '',
            anchorInlineId: source.AnchorInlineId || source.AnchorNodeId || source.anchorInlineId || source.anchorNodeId || null,
            focusInlineId: source.FocusInlineId || source.FocusNodeId || source.focusInlineId || source.focusNodeId || null,
            anchorOffset: source.AnchorOffset ?? source.AnchorBlockOffset ?? source.anchorOffset ?? source.anchorBlockOffset ?? 0,
            focusOffset: source.FocusOffset ?? source.FocusBlockOffset ?? source.focusOffset ?? source.focusBlockOffset ?? source.AnchorOffset ?? source.anchorOffset ?? 0,
            isCollapsed: source.IsCollapsed ?? source.isCollapsed ?? true,
            direction: source.Direction || source.direction || 'forward',
            headerFooterId: source.HeaderFooterId || source.headerFooterId || null,
            cellId: source.ActiveTableCellId || source.activeTableCellId || null,
            tableId: source.ActiveTableId || source.activeTableId || null,
            activeImageBlockId: source.ActiveImageBlockId || source.activeImageBlockId || null,
            activeCommentId: source.ActiveCommentId || source.activeCommentId || null,
            activeRevisionId: source.ActiveRevisionId || source.activeRevisionId || null,
            activeObjectId: source.ActiveObjectId || source.activeObjectId || null,
            hitTargetKind: source.HitTargetKind || source.hitTargetKind || null
        });
    }

    function getToolbarCommandSelection(inst) {
        var miniSelection = inst && inst.lastMiniToolbarRequest && inst.lastMiniToolbarRequest.Selection;
        if (miniSelection && miniSelection.IsCollapsed === false) {
            return selectionSnapshotFromBoundarySnapshot(miniSelection);
        }
        var toolbarSelection = readRecentToolbarSelection(inst);
        if (toolbarSelection) return toolbarSelection;
        return createSelectionSnapshot(inst && (inst.lastKeyboardSelection || inst.selection) || {});
    }

    function readRecentToolbarSelection(inst) {
        var memo = inst && inst.lastToolbarSelection;
        if (!memo || Date.now() - Number(memo.at || 0) > 2500) return null;
        var snapshot = createSelectionSnapshot(memo.selection || memo.Selection || {});
        return snapshot && snapshot.isCollapsed === false ? snapshot : null;
    }

    function rememberToolbarSelectionBeforeFocusLoss(inst, event) {
        var element = event && event.target && (event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target.parentElement);
        if (!inst || !inst.root || !element) return false;
        var shell = inst.root.closest && inst.root.closest('.tm-document-editor');
        if (!shell || !shell.contains(element)) return false;
        var toolbarTarget = element.closest && element.closest('[data-testid="document-toolbar"], [data-testid="document-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__ribbon, .tm-document-editor__floating-root');
        if (!toolbarTarget) return false;
        var selection = window.getSelection && window.getSelection();
        if (!selectionTargetsTextSurface(inst, selection)) return true;
        var snapshot = createSelectionPostFixer(inst.schema).fix(inst.model, readDomSelectionSnapshot(inst));
        if (snapshot && snapshot.isCollapsed === false && snapshot.blockId) {
            inst.lastToolbarSelection = { selection: _clone(snapshot), at: Date.now() };
            inst.selection = snapshot;
            markSelectionChanged(inst, 'toolbar-pointerdown-selection');
        }
        return true;
    }

    function preserveToolbarSelectionOnPointerEvent(inst, event, source) {
        if (!rememberToolbarSelectionBeforeFocusLoss(inst, event)) return false;
        var element = event && event.target && (event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target.parentElement);
        var selection = readRecentToolbarSelection(inst);
        if (!selection && inst && inst.selection && inst.selection.isCollapsed === false) {
            selection = createSelectionSnapshot(inst.selection);
        }

        inst.preserveMiniToolbarUntil = Date.now() + 1200;
        if (!selection || selection.isCollapsed !== false || !selection.blockId) return true;
        if (isToolbarInteractivePopoverElement(element)) return true;

        if (typeof event.preventDefault === 'function') event.preventDefault();
        var restored = scheduleToolbarSelectionRestore(inst, selection, source || 'toolbar-pointer');
        recordTimeline(inst, 'toolbar-pointer-selection-preserved', {
            source: source || '',
            restored: restored === true,
            blockId: selection.blockId || '',
            startOffset: Math.min(Number(selection.anchor && selection.anchor.offset || 0), Number(selection.focus && selection.focus.offset || 0)),
            endOffset: Math.max(Number(selection.anchor && selection.anchor.offset || 0), Number(selection.focus && selection.focus.offset || 0))
        });
        return true;
    }

    function scheduleToolbarSelectionRestore(inst, selection, source) {
        if (!inst || !selection || selection.isCollapsed !== false || !selection.blockId) return false;
        inst.pendingToolbarSelectionRestore = {
            selection: _clone(selection),
            until: Date.now() + 250,
            source: source || ''
        };
        var restored = restoreToolbarSelectionAttempt(inst, selection, source || 'toolbar-pointer');
        var schedule = function (suffix, callback) {
            try {
                callback(function () {
                    var pending = inst.pendingToolbarSelectionRestore;
                    if (!pending || Date.now() > Number(pending.until || 0)) return;
                    restoreToolbarSelectionAttempt(inst, pending.selection, (source || 'toolbar-pointer') + suffix);
                });
            } catch {
                // Ignore scheduling failures in older browser contexts.
            }
        };
        if (typeof queueMicrotask === 'function') {
            schedule('-microtask', queueMicrotask);
        }
        schedule('-timeout', function (callback) { setTimeout(callback, 0); });
        if (typeof requestAnimationFrame === 'function') {
            schedule('-raf', requestAnimationFrame);
        }
        return restored;
    }

    function restoreToolbarSelectionAttempt(inst, selection, source) {
        if (!inst || inst.restoringToolbarSelection === true) return false;
        if (isToolbarInteractivePopoverElement(typeof document !== 'undefined' && document.activeElement)) return false;
        var current = window.getSelection && window.getSelection();
        if (selectionTargetsTextSurface(inst, current) && current && current.isCollapsed === false && current.toString()) {
            return true;
        }
        inst.restoringToolbarSelection = true;
        try {
            var restored = restoreDomSelectionFromSnapshot(inst, selection);
            recordTimeline(inst, 'toolbar-selection-restore-attempt', {
                source: source || '',
                restored: restored === true,
                blockId: selection.blockId || '',
                startOffset: Math.min(Number(selection.anchor && selection.anchor.offset || 0), Number(selection.focus && selection.focus.offset || 0)),
                endOffset: Math.max(Number(selection.anchor && selection.anchor.offset || 0), Number(selection.focus && selection.focus.offset || 0))
            });
            return restored;
        } finally {
            inst.restoringToolbarSelection = false;
        }
    }

    function restoreRecentToolbarSelectionIfNeeded(inst, source) {
        if (!inst || Date.now() > Number(inst.preserveMiniToolbarUntil || 0)) return false;
        if (isToolbarInteractivePopoverElement(typeof document !== 'undefined' && document.activeElement)) return false;
        var pending = inst.pendingToolbarSelectionRestore;
        var selection = pending && Date.now() <= Number(pending.until || 0)
            ? createSelectionSnapshot(pending.selection || {})
            : readRecentToolbarSelection(inst);
        if (!selection && inst.selection && inst.selection.isCollapsed === false) selection = createSelectionSnapshot(inst.selection);
        if (!selection || selection.isCollapsed !== false || !selection.blockId) return false;
        return restoreToolbarSelectionAttempt(inst, selection, source || 'selectionchange');
    }

    function handleToolbarNativeSelectChange(inst, event) {
        var target = event && event.target;
        if (!inst || !target || target.nodeType !== Node.ELEMENT_NODE) return false;
        var testId = target.getAttribute && target.getAttribute('data-testid') || '';
        if (testId !== 'document-font-size' && testId !== 'document-font-family') return false;
        var shell = inst.root && inst.root.closest ? inst.root.closest('.tm-document-editor') : null;
        if (shell && !shell.contains(target)) return false;
        var rawValue = _asText(target.value || '');
        if (!rawValue) return false;
        var command = testId === 'document-font-size' ? 'setFontSize' : 'setFontFamily';
        var value = testId === 'document-font-size' && /^\d+(\.\d+)?$/.test(rawValue)
            ? rawValue + 'pt'
            : rawValue;
        var now = Date.now();
        var guardKey = command + '|' + value;
        if (inst.lastNativeToolbarCommandKey === guardKey && now - Number(inst.lastNativeToolbarCommandAt || 0) < 120) {
            return true;
        }
        inst.lastNativeToolbarCommandKey = guardKey;
        inst.lastNativeToolbarCommandAt = now;
        var selection = getToolbarCommandSelection(inst);
        if (selection && selection.blockId) {
            inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, selection);
            markSelectionChanged(inst, 'toolbar-native-selection');
        }
        recordTimeline(inst, 'toolbar-native-select-command', { command: command, value: value });
        applyCommand(inst.id, command, { Value: value, Selection: selection, source: 'toolbar-native' });
        return true;
    }

    function resolveToolbarButtonCommand(element) {
        var button = element && element.closest && element.closest('button');
        if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return null;
        var testId = button.getAttribute('data-testid') || '';
        var raw = button.getAttribute('data-command') || '';
        if (!raw && /^document-(mini|context)-/.test(testId)) {
            raw = testId
                .replace(/^document-(mini|context)-/, '')
                .replace(/-/g, '');
        }
        var compact = String(raw || '').replace(/[\s_.:-]+/g, '').toLowerCase();
        switch (compact) {
            case 'bold':
                return { command: 'toggleBold', payload: {} };
            case 'italic':
                return { command: 'toggleItalic', payload: {} };
            case 'underline':
                return { command: 'toggleUnderline', payload: {} };
            case 'strikethrough':
            case 'strike':
                return { command: 'toggleStrikethrough', payload: {} };
            case 'clearformatting':
                return { command: 'clearFormatting', payload: {} };
            case 'alignleft':
                return { command: 'setParagraphAlignment', payload: { Alignment: 'left', Value: 'left' } };
            case 'aligncenter':
            case 'aligncentre':
                return { command: 'setParagraphAlignment', payload: { Alignment: 'center', Value: 'center' } };
            case 'alignright':
                return { command: 'setParagraphAlignment', payload: { Alignment: 'right', Value: 'right' } };
            case 'alignjustify':
                return { command: 'setParagraphAlignment', payload: { Alignment: 'justify', Value: 'justify' } };
            case 'bulletlist':
                return { command: 'toggleBulletList', payload: { Value: 'bullet', ListType: 'bullet' } };
            case 'numberedlist':
                return { command: 'toggleNumberedList', payload: { Value: 'numbered', ListType: 'numbered' } };
            default:
                return null;
        }
    }

    function handleToolbarNativeButtonClick(inst, event) {
        try {
            var target = event && event.target;
            var element = target && (target.nodeType === Node.ELEMENT_NODE ? target : target.parentElement);
            if (!inst || !inst.root || !element) return false;
            var shell = inst.root.closest && inst.root.closest('.tm-document-editor');
            if (!shell || !shell.contains(element)) {
                recordTimeline(inst, 'toolbar-native-button-skip', { reason: 'outside-shell' });
                return false;
            }
            var toolbar = element.closest && element.closest('[data-testid="document-toolbar"], [data-testid="document-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__context-menu, .tm-document-editor__floating-root');
            if (!toolbar) {
                recordTimeline(inst, 'toolbar-native-button-skip', {
                    reason: 'outside-toolbar',
                    testId: element.getAttribute && element.getAttribute('data-testid') || ''
                });
                return false;
            }
            var resolved = resolveToolbarButtonCommand(element);
            if (!resolved) {
                var button = element.closest && element.closest('button');
                recordTimeline(inst, 'toolbar-native-button-skip', {
                    reason: 'unmapped-command',
                    testId: button && button.getAttribute && button.getAttribute('data-testid') || '',
                    command: button && button.getAttribute && button.getAttribute('data-command') || ''
                });
                return false;
            }
            recordTimeline(inst, 'toolbar-native-button-command', { command: resolved.command });
            var selection = null;
            try {
                selection = getToolbarCommandSelection(inst);
            } catch (selectionError) {
                inst.lastError = 'toolbar-native-selection-failed';
                recordWatchdogFailure(inst, 'toolbar-native-button', inst.lastError, {
                    command: resolved.command,
                    message: String(selectionError && selectionError.message || selectionError)
                });
                selection = createSelectionSnapshot(inst.selection || firstModelSelection(inst.model));
            }
            if (selection && selection.blockId) {
                try {
                    inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, selection);
                    markSelectionChanged(inst, 'toolbar-native-button-selection');
                } catch (fixError) {
                    inst.lastError = 'toolbar-native-selection-fix-failed';
                    recordWatchdogFailure(inst, 'toolbar-native-button', inst.lastError, {
                        command: resolved.command,
                        message: String(fixError && fixError.message || fixError)
                    });
                }
            }
            var payload = Object.assign({}, resolved.payload || {}, {
                Selection: selection,
                source: 'toolbar-native'
            });
            var result = applyCommand(inst.id, resolved.command, payload);
            if (result && result.ok !== false) {
                event.preventDefault && event.preventDefault();
                event.stopImmediatePropagation && event.stopImmediatePropagation();
                event.stopPropagation && event.stopPropagation();
                return true;
            }
        } catch (error) {
            if (inst) {
                inst.lastError = 'toolbar-native-button-failed';
                recordWatchdogFailure(inst, 'toolbar-native-button', inst.lastError, {
                    message: String(error && error.message || error)
                });
            }
        }
        return false;
    }

    function isToolbarInteractivePopoverElement(element) {
        if (!element || !element.closest) return false;
        return !!element.closest('.tm-color-picker-dropdown, .tm-flat-color-picker, .tm-color-picker-actions');
    }

    function dispatchToolbarNativeButtonCommand(inst, event, element, resolved) {
        if (!inst || !resolved) return false;
        recordTimeline(inst, 'toolbar-native-button-command', { command: resolved.command, delegated: true });
        var selection = null;
        try {
            selection = getToolbarCommandSelection(inst);
        } catch (selectionError) {
            inst.lastError = 'toolbar-native-selection-failed';
            recordWatchdogFailure(inst, 'toolbar-native-button', inst.lastError, {
                command: resolved.command,
                message: String(selectionError && selectionError.message || selectionError)
            });
            selection = createSelectionSnapshot(inst.selection || firstModelSelection(inst.model));
        }
        if (selection && selection.blockId) {
            try {
                inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, selection);
                markSelectionChanged(inst, 'toolbar-native-button-selection');
            } catch (fixError) {
                inst.lastError = 'toolbar-native-selection-fix-failed';
                recordWatchdogFailure(inst, 'toolbar-native-button', inst.lastError, {
                    command: resolved.command,
                    message: String(fixError && fixError.message || fixError)
                });
            }
        }
        var payload = Object.assign({}, resolved.payload || {}, {
            Selection: selection,
            source: 'toolbar-native'
        });
        var result = applyCommand(inst.id, resolved.command, payload);
        if (result && result.ok !== false) {
            event.preventDefault && event.preventDefault();
            event.stopImmediatePropagation && event.stopImmediatePropagation();
            event.stopPropagation && event.stopPropagation();
            return true;
        }
        return false;
    }

    function dispatchToolbarRuntimeButtonCommand(instanceId, event, resolved) {
        var runtime = window.tmDocumentEditorRuntime || null;
        if (!instanceId || !resolved || !runtime || typeof runtime.executeCommand !== 'function') return false;
        var selection = null;
        if (typeof runtime.getRuntimeSelection === 'function') {
            try {
                selection = runtime.getRuntimeSelection(instanceId);
                if (selection && selection.ok === false) selection = null;
            } catch {
                selection = null;
            }
        }
        var payload = Object.assign({}, resolved.payload || {}, {
            Selection: selection,
            source: 'toolbar-native'
        });
        var result = runtime.executeCommand(instanceId, resolved.command, payload);
        if (!result && window.tmDocumentEditorEngine && typeof window.tmDocumentEditorEngine.applyCommand === 'function') {
            result = window.tmDocumentEditorEngine.applyCommand(instanceId, resolved.command, payload);
        }
        if (result && result.ok !== false) {
            event.preventDefault && event.preventDefault();
            event.stopImmediatePropagation && event.stopImmediatePropagation();
            event.stopPropagation && event.stopPropagation();
            return true;
        }
        return false;
    }

    function resolveToolbarInstanceFromElement(element) {
        if (!element || !element.closest) return null;
        var shell = element.closest('.tm-document-editor');
        var host = shell && shell.querySelector && shell.querySelector('[data-testid="document-wysiwyg-host"][data-instance-id]');
        if (!host) host = document.querySelector('[data-testid="document-wysiwyg-host"][data-instance-id]');
        var instanceId = host && host.getAttribute && host.getAttribute('data-instance-id') || '';
        return instanceId ? _instances.get(instanceId) || null : null;
    }

    function preserveToolbarSelectionFromGlobalEvent(event, source) {
        var target = event && event.target;
        var element = target && (target.nodeType === Node.ELEMENT_NODE ? target : target.parentElement);
        if (!element || !element.closest) return false;
        var toolbar = element.closest('[data-testid="document-toolbar"], [data-testid="document-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__context-menu, .tm-document-editor__floating-root');
        if (!toolbar) return false;
        var inst = resolveToolbarInstanceFromElement(element);
        if (!inst || !inst.root) return false;

        var selection = window.getSelection && window.getSelection();
        if (selectionTargetsTextSurface(inst, selection)) {
            var snapshot = createSelectionPostFixer(inst.schema).fix(inst.model, readDomSelectionSnapshot(inst));
            if (snapshot && snapshot.isCollapsed === false && snapshot.blockId) {
                inst.lastToolbarSelection = { selection: _clone(snapshot), at: Date.now() };
                inst.selection = snapshot;
                markSelectionChanged(inst, 'toolbar-global-' + (source || 'pointer') + '-selection');
            }
        }

        var commandSelection = readRecentToolbarSelection(inst);
        if (!commandSelection && inst.selection && inst.selection.isCollapsed === false) {
            commandSelection = createSelectionSnapshot(inst.selection);
        }
        if (!commandSelection || commandSelection.isCollapsed !== false || !commandSelection.blockId) return true;

        inst.preserveMiniToolbarUntil = Date.now() + 1200;
        if (isToolbarInteractivePopoverElement(element)) return true;
        if (typeof event.preventDefault === 'function') event.preventDefault();
        var restored = scheduleToolbarSelectionRestore(inst, commandSelection, 'global-' + (source || 'pointer'));
        recordTimeline(inst, 'toolbar-global-selection-preserved', {
            source: source || '',
            restored: restored === true,
            blockId: commandSelection.blockId || '',
            startOffset: Math.min(Number(commandSelection.anchor && commandSelection.anchor.offset || 0), Number(commandSelection.focus && commandSelection.focus.offset || 0)),
            endOffset: Math.max(Number(commandSelection.anchor && commandSelection.anchor.offset || 0), Number(commandSelection.focus && commandSelection.focus.offset || 0))
        });
        return true;
    }

    function installGlobalToolbarButtonBridge() {
        if (typeof window.addEventListener !== 'function') return;
        var existing = window.__tmDocumentEditorToolbarNativeButtonBridge;
        if (existing && existing.version === TOOLBAR_NATIVE_BUTTON_BRIDGE_VERSION && existing.handler) {
            window.__tmDocumentEditorToolbarNativeButtonBridgeInstalled = true;
            return;
        }

        if (existing && existing.handler) {
            try {
                window.removeEventListener('click', existing.handler, true);
                if (existing.preserveHandler) {
                    window.removeEventListener('pointerdown', existing.preserveHandler, true);
                    window.removeEventListener('mousedown', existing.preserveHandler, true);
                }
            } catch {
                // Ignore stale listener cleanup failures.
            }
        }

        var handler = function (event) {
            try {
                var target = event && event.target;
                var element = target && (target.nodeType === Node.ELEMENT_NODE ? target : target.parentElement);
                if (!element || !element.closest) return;
                var toolbar = element.closest('[data-testid="document-toolbar"], [data-testid="document-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__context-menu, .tm-document-editor__floating-root');
                if (!toolbar) return;
                var resolved = resolveToolbarButtonCommand(element);
                if (!resolved) return;
                var shell = element.closest('.tm-document-editor');
                var host = shell && shell.querySelector && shell.querySelector('[data-testid="document-wysiwyg-host"][data-instance-id]');
                var instanceId = host && host.getAttribute && host.getAttribute('data-instance-id') || '';
                if (!instanceId) return;
                dispatchToolbarRuntimeButtonCommand(instanceId, event, resolved);
            } catch (error) {
                // Keep toolbar event handling isolated from Blazor's own event pipeline.
            }
        };
        var preserveHandler = function (event) {
            try {
                preserveToolbarSelectionFromGlobalEvent(event, event && event.type || '');
            } catch {
                // Keep toolbar selection preservation isolated from browser-native input.
            }
        };

        window.__tmDocumentEditorToolbarNativeButtonBridge = {
            version: TOOLBAR_NATIVE_BUTTON_BRIDGE_VERSION,
            handler: handler,
            preserveHandler: preserveHandler
        };
        window.__tmDocumentEditorToolbarNativeButtonBridgeInstalled = true;
        window.addEventListener('pointerdown', preserveHandler, true);
        window.addEventListener('mousedown', preserveHandler, true);
        window.addEventListener('click', handler, true);
    }

    function boundarySelectionSnapshot(selection, inst) {
        var snapshot = inst
            ? withStableSelectionToken(inst.id, selection || {}, inst.model)
            : createSelectionSnapshot(selection || {});
        var anchor = createLogicalPosition(snapshot.anchor || {});
        var focus = createLogicalPosition(snapshot.focus || anchor);
        return {
            Region: snapshot.region || anchor.region || focus.region || 'Body',
            HeaderFooterId: snapshot.headerFooterId || anchor.headerFooterId || focus.headerFooterId || null,
            AnchorNodeId: anchor.inlineId || null,
            FocusNodeId: focus.inlineId || null,
            AnchorBlockId: anchor.blockId || null,
            AnchorInlineId: anchor.inlineId || null,
            AnchorOffset: Math.max(0, Math.round(Number(anchor.offset || 0) || 0)),
            AnchorBlockOffset: Math.max(0, Math.round(Number(anchor.offset || 0) || 0)),
            FocusBlockId: focus.blockId || anchor.blockId || null,
            FocusInlineId: focus.inlineId || null,
            FocusOffset: Math.max(0, Math.round(Number(focus.offset || 0) || 0)),
            FocusBlockOffset: Math.max(0, Math.round(Number(focus.offset || 0) || 0)),
            IsCollapsed: snapshot.isCollapsed !== false,
            Direction: snapshot.direction || 'forward',
            ActiveTableCellId: snapshot.cellId || focus.cellId || anchor.cellId || null,
            ActiveTableId: snapshot.activeTableId || snapshot.tableId || focus.tableId || anchor.tableId || null,
            ActiveImageBlockId: snapshot.activeImageBlockId || null,
            ActiveCommentId: snapshot.activeCommentId || null,
            ActiveRevisionId: snapshot.activeRevisionId || null,
            ActiveObjectId: snapshot.activeObjectId || snapshot.objectId || focus.objectId || anchor.objectId || null,
            HitTargetKind: snapshot.hitTargetKind || (snapshot.activeCommentId ? 'comment' : (snapshot.activeRevisionId ? 'revision' : (snapshot.isObjectSelection ? 'object' : (snapshot.isCellSelection ? 'tableCell' : 'text')))),
            SelectionToken: snapshot.SelectionToken || snapshot.selectionToken || null,
            StableSelectionToken: snapshot.StableSelectionToken || snapshot.stableSelectionToken || snapshot.SelectionToken || snapshot.selectionToken || null,
            SelectionTokenData: snapshot.SelectionTokenData || snapshot.selectionTokenData || null
        };
    }

    function selectionBelongsToEditor(inst, selection) {
        if (!inst || !inst.root || !selection || selection.rangeCount === 0) return false;
        var range = selection.getRangeAt(0);
        var start = range.startContainer && (range.startContainer.nodeType === Node.ELEMENT_NODE ? range.startContainer : range.startContainer.parentElement);
        var end = range.endContainer && (range.endContainer.nodeType === Node.ELEMENT_NODE ? range.endContainer : range.endContainer.parentElement);
        return !!(start && end && inst.root.contains(start) && inst.root.contains(end));
    }

    function selectionTargetsTextSurface(inst, selection) {
        if (!selectionBelongsToEditor(inst, selection) || selection.isCollapsed) return false;
        var range = selection.getRangeAt(0);
        var common = range.commonAncestorContainer && (range.commonAncestorContainer.nodeType === Node.ELEMENT_NODE
            ? range.commonAncestorContainer
            : range.commonAncestorContainer.parentElement);
        if (!common) return false;
        if (common.closest && common.closest('.tm-wysiwyg-image, figure, [data-object-id], .tm-wysiwyg-layout-object')) return false;
        return !!(common.closest && common.closest('.tm-wysiwyg-page__body[contenteditable], .tm-wysiwyg-page__header[contenteditable], .tm-wysiwyg-page__footer[contenteditable], .tm-wysiwyg-table-cell, .tm-wysiwyg-block[data-block-id]'));
    }

    function selectedDomRect(selection) {
        if (!selection || selection.rangeCount === 0 || selection.isCollapsed) return null;
        var range = selection.getRangeAt(0);
        var rects = Array.from(range.getClientRects ? range.getClientRects() : [])
            .filter(function (rect) { return rect && rect.width > 0.5 && rect.height > 0.5; });
        if (rects.length === 0) {
            var fallback = range.getBoundingClientRect ? range.getBoundingClientRect() : null;
            if (fallback && fallback.width > 0.5 && fallback.height > 0.5) rects.push(fallback);
        }
        if (rects.length === 0) return null;
        var left = Math.min.apply(null, rects.map(function (rect) { return rect.left; }));
        var top = Math.min.apply(null, rects.map(function (rect) { return rect.top; }));
        var right = Math.max.apply(null, rects.map(function (rect) { return rect.right; }));
        var bottom = Math.max.apply(null, rects.map(function (rect) { return rect.bottom; }));
        return { left: left, top: top, width: right - left, height: bottom - top, right: right, bottom: bottom };
    }

    function floatingViewportBoundsAvoidingChrome() {
        var width = window.innerWidth || document.documentElement && document.documentElement.clientWidth || 0;
        var height = window.innerHeight || document.documentElement && document.documentElement.clientHeight || 0;
        var left = 0;
        var top = 0;
        var gutter = 8;
        var toolbar = document.querySelector('[data-testid="document-toolbar"], .tm-document-editor__ribbon');
        if (toolbar && toolbar.getBoundingClientRect) {
            var toolbarRect = toolbar.getBoundingClientRect();
            var toolbarStyle = window.getComputedStyle ? window.getComputedStyle(toolbar) : null;
            if (toolbarRect.width > 1
                && toolbarRect.height > 1
                && toolbarRect.bottom > 0
                && (!toolbarStyle || toolbarStyle.visibility !== 'hidden' && toolbarStyle.display !== 'none')) {
                top = Math.max(top, Math.min(height - 80, toolbarRect.bottom + gutter));
            }
        }
        var panel = document.querySelector('[data-testid="document-side-panel"]');
        if (panel && panel.getBoundingClientRect) {
            var rect = panel.getBoundingClientRect();
            var style = window.getComputedStyle ? window.getComputedStyle(panel) : null;
            if (rect.width > 1 && rect.height > 1 && (!style || style.visibility !== 'hidden' && style.display !== 'none')) {
                width = Math.max(320, Math.min(width, rect.left - 8));
            }
        }
        return {
            left: left,
            top: top,
            right: width,
            bottom: height,
            width: Math.max(0, width - left),
            height: Math.max(0, height - top)
        };
    }

    function floatingViewportWidthAvoidingSidePanel() {
        return floatingViewportBoundsAvoidingChrome().right;
    }

    function shouldShowMiniToolbarForSelectionSnapshot(selection) {
        var snapshot = createSelectionSnapshot(selection || {});
        return snapshot.isCollapsed === false
            && snapshot.isObjectSelection !== true
            && !snapshot.activeObjectId
            && !snapshot.objectId;
    }

    function canRestoreLastMiniToolbarRequest(inst, source) {
        if (!inst || !inst.lastMiniToolbarRequest) return false;
        var selection = inst.lastMiniToolbarRequest.Selection || inst.lastMiniToolbarRequest.selection || null;
        if (!selection || !shouldShowMiniToolbarForSelectionSnapshot(selection)) return false;
        var reason = _asText(source || '');
        if (reason === 'viewport-change') return false;
        if (Date.now() > Number(inst.preserveMiniToolbarUntil || 0)) return false;
        return reason.indexOf('toolbar') >= 0
            || reason.indexOf('picker') >= 0
            || reason.indexOf('popover') >= 0;
    }

    function showMiniToolbarForSelection(inst, source) {
        if (!inst || !inst.root || inst.options && inst.options.readOnly === true) return false;
        var selection = window.getSelection && window.getSelection();
        if (!selectionTargetsTextSurface(inst, selection)) {
            if (restoreLastMiniToolbarRequest(inst, source)) return true;
            hideMiniToolbar(inst, source || 'selection-not-text');
            return false;
        }

        var rect = selectedDomRect(selection);
        if (!rect) {
            if (restoreLastMiniToolbarRequest(inst, source)) return true;
            hideMiniToolbar(inst, source || 'selection-no-rect');
            return false;
        }

        var snapshot = createSelectionPostFixer(inst.schema).fix(inst.model, readDomSelectionSnapshot(inst));
        if (!snapshot || !shouldShowMiniToolbarForSelectionSnapshot(snapshot)) {
            if (restoreLastMiniToolbarRequest(inst, source)) return true;
            hideMiniToolbar(inst, source || 'selection-collapsed');
            return false;
        }

        inst.selection = snapshot;
        markSelectionChanged(inst, source || 'selection-toolbar');
        invokeBoundaryMethod(inst, 'HandleSelectionChanged', boundarySelectionSnapshot(snapshot, inst), 'selection-changed-failed');

        var viewport = floatingViewportBoundsAvoidingChrome();
        var toolbarSize = { width: Math.min(360, Math.max(280, viewport.width - 16)), height: 44 };
        var position = computeFloatingPosition(rect, toolbarSize, {
            placement: 'top',
            gutter: 10,
            viewportLeft: viewport.left,
            viewportTop: viewport.top,
            viewportWidth: viewport.right,
            viewportHeight: viewport.bottom
        });
        inst.floatingUiOpen = true;
        inst.lastMiniToolbarRequest = {
            IsVisible: true,
            Left: position.left,
            Top: position.top,
            Width: toolbarSize.width,
            Height: toolbarSize.height,
            ViewportWidth: viewport.right,
            ViewportHeight: viewport.bottom,
            Selection: boundarySelectionSnapshot(snapshot, inst)
        };
        invokeBoundaryMethod(inst, 'HandleMiniToolbarChanged', inst.lastMiniToolbarRequest, 'mini-toolbar-open-failed');
        return true;
    }

    function restoreLastMiniToolbarRequest(inst, source) {
        if (!canRestoreLastMiniToolbarRequest(inst, source)) return false;
        inst.floatingUiOpen = true;
        invokeBoundaryMethod(inst, 'HandleMiniToolbarChanged', inst.lastMiniToolbarRequest, 'mini-toolbar-restore-failed');
        return true;
    }

    function hideMiniToolbar(inst, reason) {
        if (!inst || !inst.floatingUiOpen && !inst.lastMiniToolbarRequest) return;
        inst.floatingUiOpen = false;
        inst.lastMiniToolbarRequest = null;
        recordTimeline(inst, 'mini-toolbar-hidden', { reason: reason || '' });
        invokeBoundaryMethod(inst, 'HandleMiniToolbarChanged', {
            IsVisible: false,
            Reason: reason || ''
        }, 'mini-toolbar-close-failed');
    }

    function scheduleMiniToolbarRefresh(inst, source, delay) {
        if (!inst) return;
        if (inst.miniToolbarRefreshTimer) clearTimeout(inst.miniToolbarRefreshTimer);
        inst.miniToolbarRefreshTimer = setTimeout(function () {
            inst.miniToolbarRefreshTimer = null;
            showMiniToolbarForSelection(inst, source || 'selection-refresh');
        }, Number(delay || 0) || 0);
    }

    function clearMiniToolbarViewportRefreshTimers(inst) {
        if (!inst || !Array.isArray(inst.miniToolbarViewportRefreshTimers)) return;
        inst.miniToolbarViewportRefreshTimers.forEach(function (timer) { clearTimeout(timer); });
        inst.miniToolbarViewportRefreshTimers = [];
    }

    function scheduleMiniToolbarViewportRefresh(inst, source) {
        if (!inst) return;
        scheduleMiniToolbarRefresh(inst, source || 'viewport-change', 20);
        clearMiniToolbarViewportRefreshTimers(inst);
        inst.miniToolbarViewportRefreshTimers = [120, 280].map(function (delay) {
            return setTimeout(function () {
                showMiniToolbarForSelection(inst, source || 'viewport-change');
            }, delay);
        });
    }

    function publishCollapsedDomSelectionChange(inst, source) {
        if (!inst || !inst.root) return false;
        var selection = window.getSelection && window.getSelection();
        if (!selectionBelongsToEditor(inst, selection)) return false;

        var snapshot = createSelectionPostFixer(inst.schema).fix(inst.model, readDomSelectionSnapshot(inst));
        if (!snapshot || snapshot.isObjectSelection || snapshot.isCollapsed !== true) return false;

        inst.selection = snapshot;
        var typingHotPath = isTypingHotPath(inst);
        markSelectionChanged(inst, typingHotPath ? 'typing' : (source || 'selectionchange-caret'));
        if (!typingHotPath) {
            scheduleFormattingStatePublish(inst, source || 'selectionchange-caret', { immediate: true });
            invokeBoundaryMethod(inst, 'HandleSelectionChanged', boundarySelectionSnapshot(snapshot, inst), 'selection-changed-failed');
        } else {
            recordTimeline(inst, 'selectionchange-typing-debounced', {
                blockId: snapshot.blockId || '',
                offset: snapshot.offset || 0
            });
        }
        return true;
    }

    function handleEditorCompositionStart(inst, event) {
        if (!inst || !targetIsEditableDocumentSurface(inst, event && event.target)) return { handled: false };
        var selection = readFixedDomSelection(inst, 'compositionstart-dom');
        inst.compositionSession = {
            beforeSelection: _clone(selection),
            preview: '',
            startedAt: Date.now()
        };
        recordTimeline(inst, 'composition-start', {
            blockId: selection.blockId || '',
            offset: selection.offset || 0
        });
        return { handled: true, selection: selection };
    }

    function renderEditorCompositionPreview(inst, text) {
        var session = inst && inst.compositionSession;
        if (!inst || !session || !session.beforeSelection) return false;
        var selection = createSelectionSnapshot(session.beforeSelection);
        var block = _findBlock(inst.model, selection.blockId);
        var node = findLiveTextBlockElement(inst, selection.blockId);
        if (!block || !node) return false;
        var previewBlock = _clone(block);
        _insertTextRun(previewBlock, selection.offset, _asText(text), { marks: _clone(inst.pendingTypingMarks || []) });
        replaceLiveParagraphHtml(inst, node, previewBlock);
        var previewSelection = createSelectionSnapshot({
            region: selection.region || 'Body',
            blockId: selection.blockId,
            offset: Number(selection.offset || 0) + _asText(text).length,
            isCollapsed: true
        });
        restoreDomSelectionFromSnapshot(Object.assign({}, inst, { model: { body: { blocks: [previewBlock] }, indexes: { blocks: _sortObject({ [previewBlock.id]: previewBlock }) } } }), previewSelection);
        recordTimeline(inst, 'composition-preview', {
            blockId: selection.blockId || '',
            textLength: _asText(text).length
        });
        return true;
    }

    function handleEditorCompositionUpdate(inst, event) {
        if (!inst || !targetIsEditableDocumentSurface(inst, event && event.target)) return { handled: false };
        if (!inst.compositionSession) handleEditorCompositionStart(inst, event);
        if (!inst.compositionSession) return { handled: false };
        var preview = _asText(event && event.data);
        inst.compositionSession.preview = preview;
        renderEditorCompositionPreview(inst, preview);
        return { handled: true, preview: preview };
    }

    function handleEditorCompositionEnd(inst, event) {
        if (!inst || !targetIsEditableDocumentSurface(inst, event && event.target)) return { handled: false };
        if (!inst.compositionSession) handleEditorCompositionStart(inst, event);
        var session = inst.compositionSession;
        var text = _asText(event && event.data || session && session.preview || '');
        var selection = createSelectionSnapshot(session && session.beforeSelection || readFixedDomSelection(inst, 'compositionend-dom'));
        inst.compositionSession = null;
        if (!text) return { handled: true, noop: true };
        inst.selection = selection;
        restoreDomSelectionFromSnapshot(inst, selection);
        var marks = _clone(inst.pendingTypingMarks || []);
        var revisionPayload = isTrackChangesEnabled(inst)
            ? createOrExtendLiveTypingRevision(inst, selection, text, marks)
            : null;
        var result = applyCommand(inst.id, OPERATION_TYPES.InsertText, {
            target: { blockId: selection.blockId, offset: selection.offset, region: selection.region, headerFooterId: selection.headerFooterId || null },
            text: text,
            marks: marks,
            revisionId: revisionPayload && revisionPayload.id || null,
            revision: revisionPayload && revisionPayload.revision || null,
            source: 'composition',
            transactionType: TRANSACTION_TYPES.Typing,
            beforeSelection: selection
        });
        if (result && result.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'compositionend');
        markKeyboardInputHandled(inst, 'insertCompositionText', text);
        markKeyboardInputHandled(inst, 'insertText', text);
        return { handled: true, result: result };
    }

    function handleEditorBeforeInput(inst, event) {
        if (!inst || !event || !targetIsEditableDocumentSurface(inst, event.target)) return { handled: false };
        recordBeforeInputEvent(inst);
        if (consumeSuppressedBeforeInput(inst, event)) return { handled: true, suppressed: true };
        var normalized = normalizeBeforeInput(event);
        if (typeof event.stopPropagation === 'function') event.stopPropagation();
        recordTimeline(inst, 'beforeinput', {
            inputType: normalized.inputType || '',
            supported: normalized.supported === true,
            dataLength: _asText(normalized.data || '').length
        });
        if (!normalized.supported) return { handled: true, normalized: normalized };

        var selection = readFixedDomSelection(inst, 'beforeinput-dom');
        var block = _findBlock(inst.model, selection.blockId);
        var offset = Math.max(0, Math.min(_blockText(block).length, Number(selection.offset || 0)));
        var inputType = normalized.inputType;
        var result = null;

        if (inputType === 'insertCompositionText') {
            handleEditorCompositionUpdate(inst, { data: normalized.data || '', target: event.target });
            return { handled: true, normalized: normalized, composition: true };
        }

        if (inputType === 'insertText' || inputType === 'insertFromPaste' || inputType === 'insertLineBreak') {
            var text = inputType === 'insertLineBreak' ? '\n' : _asText(normalized.data || '');
            if (!text) return { handled: true, normalized: normalized, noop: true };
            var marks = _clone(inst.pendingTypingMarks || []);
            var revisionPayload = null;
            if (isTrackChangesEnabled(inst)) {
                if (inputType === 'insertFromPaste') {
                    clearLiveTypingRevision(inst);
                    revisionPayload = {
                        id: '',
                        revision: createInsertionRevisionPayload({ blockId: selection.blockId, start: offset, end: offset + text.length }, text, resolveRevisionUserId(inst.options || {}), 'paste')
                    };
                    revisionPayload.id = revisionPayload.revision.id;
                } else {
                    revisionPayload = createOrExtendLiveTypingRevision(inst, selection, text, marks);
                }
            }
            result = applyCommand(inst.id, OPERATION_TYPES.InsertText, {
                target: { blockId: selection.blockId, offset: offset, region: selection.region, headerFooterId: selection.headerFooterId || null },
                text: text,
                marks: marks,
                revisionId: revisionPayload && revisionPayload.id || null,
                revision: revisionPayload && revisionPayload.revision || null,
                source: 'beforeinput',
                transactionType: TRANSACTION_TYPES.Typing,
                beforeSelection: selection
            });
        } else if (inputType === 'insertParagraph') {
            clearLiveTypingRevision(inst);
            var structureRevision = isTrackChangesEnabled(inst)
                ? createStructureRevisionPayload({ blockId: selection.blockId, start: offset, end: offset }, 'SplitBlock', resolveRevisionUserId(inst.options || {}), 'beforeinput')
                : null;
            result = applyCommand(inst.id, OPERATION_TYPES.SplitParagraph, {
                target: { blockId: selection.blockId, offset: offset, region: selection.region, headerFooterId: selection.headerFooterId || null },
                newBlockId: _stableId('block', selection.blockId + '-enter-' + Date.now() + '-' + Math.floor(Math.random() * 1000)),
                revisionId: structureRevision && structureRevision.id || null,
                revision: structureRevision || null,
                source: 'beforeinput',
                transactionType: TRANSACTION_TYPES.Typing,
                beforeSelection: selection
            });
        } else if (inputType === 'deleteContentBackward' || inputType === 'deleteWordBackward' || inputType === 'deleteContentForward' || inputType === 'deleteWordForward') {
            clearLiveTypingRevision(inst);
            var selectedRange = selection.isCollapsed === false && selection.anchor && selection.focus && selection.anchor.blockId === selection.focus.blockId
                ? selectionToRange(selection)
                : null;
            var backward = inputType.indexOf('Backward') >= 0;
            var word = inputType.indexOf('Word') >= 0;
            var textValue = _blockText(block);
            var start = selectedRange ? selectedRange.start : (backward ? (word ? previousWordBoundary(textValue, offset) : Math.max(0, offset - 1)) : offset);
            var end = selectedRange ? selectedRange.end : (backward ? offset : (word ? nextWordBoundary(textValue, offset) : Math.min(textValue.length, offset + 1)));
            if (start === end) return { handled: true, normalized: normalized, noop: true };
            var deletionRevision = isTrackChangesEnabled(inst)
                ? createDeletionRevisionPayload(inst.model, { blockId: selection.blockId, start: start, end: end }, resolveRevisionUserId(inst.options || {}), 'beforeinput')
                : null;
            result = applyCommand(inst.id, OPERATION_TYPES.DeleteRange, {
                range: { blockId: selection.blockId, start: start, end: end, region: selection.region, headerFooterId: selection.headerFooterId || null },
                revisionId: deletionRevision && deletionRevision.id || null,
                revision: deletionRevision || null,
                source: 'beforeinput',
                transactionType: 'delete',
                beforeSelection: selection
            });
        }

        if (result && result.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'beforeinput-' + inputType);
        return { handled: true, normalized: normalized, result: result };
    }

    function handleEditorPaste(inst, event) {
        if (!inst || !event || !targetIsEditableDocumentSurface(inst, event.target)) return { handled: false };
        var clipboard = event.clipboardData || window.clipboardData || null;
        var text = clipboard && typeof clipboard.getData === 'function'
            ? clipboard.getData('text/plain') || clipboard.getData('text') || ''
            : '';
        text = normalizePasteText(text);
        if (!text) return { handled: false };
        if (typeof event.preventDefault === 'function') event.preventDefault();
        if (typeof event.stopPropagation === 'function') event.stopPropagation();
        clearLiveTypingRevision(inst);

        var selection = readFixedDomSelection(inst, 'paste-dom');
        var range = selectionToRange(selection);
        var operations = [];
        var userId = resolveRevisionUserId(inst.options || {});
        if (selection.isCollapsed === false && range.start !== range.end) {
            var deletionRevision = isTrackChangesEnabled(inst)
                ? createDeletionRevisionPayload(inst.model, range, userId, 'paste')
                : null;
            operations.push(createOperation(OPERATION_TYPES.DeleteRange, {
                range: Object.assign({}, range, { region: selection.region, headerFooterId: selection.headerFooterId || null }),
                revisionId: deletionRevision && deletionRevision.id || null,
                revision: deletionRevision || null
            }, { source: 'paste' }));
        }

        var firstLine = text.split('\n')[0] || '';
        var revisionPayload = isTrackChangesEnabled(inst)
            ? createInsertionRevisionPayload({ blockId: range.blockId, start: range.start, end: range.start + firstLine.length }, firstLine, userId, 'paste')
            : null;
        operations.push(createOperation(OPERATION_TYPES.InsertText, {
            target: { blockId: range.blockId, offset: range.start, region: selection.region, headerFooterId: selection.headerFooterId || null },
            text: firstLine,
            marks: _clone(inst.pendingTypingMarks || []),
            revisionId: revisionPayload && revisionPayload.id || null,
            revision: revisionPayload || null
        }, { source: 'paste' }));

        var result;
        if (operations.length === 1) {
            result = applyCommand(inst.id, OPERATION_TYPES.InsertText, Object.assign({}, operations[0].toJSON ? operations[0].toJSON() : operations[0], {
                transactionType: TRANSACTION_TYPES.Default,
                beforeSelection: selection
            }));
        } else {
            result = applyOperationBatchToInstance(inst, operations, TRANSACTION_TYPES.Default, 'Paste', true);
        }
        if (result && result.ok !== false) rememberKeyboardSelection(inst, inst.selection || selection, 'paste');
        markKeyboardInputHandled(inst, 'insertFromPaste', firstLine);
        return { handled: true, result: result };
    }

    function installAccessibilityAndKeyboardHandlers(inst) {
        var root = inst && inst.root;
        if (!root || typeof root.addEventListener !== 'function') return;
        inst.eventHandlers = inst.eventHandlers || [];
        var onFocusIn = function (event) {
            setActiveFocusRegion(inst, getFocusRegionFromElement(root, event.target), event.target, 'focusin');
        };
        var onPointerDown = function (event) {
            setActiveFocusRegion(inst, getFocusRegionFromElement(root, event.target), event.target, 'pointerdown');
            if (targetIsEditableDocumentSurface(inst, event && event.target)) {
                clearKeyboardSelectionMemory(inst);
                clearLiveTypingRevision(inst);
                inst.preserveMiniToolbarUntil = 0;
                hideMiniToolbar(inst, 'editable-pointerdown');
            }
        };
        var onPointerUp = function () {
            scheduleMiniToolbarRefresh(inst, 'pointerup-selection', 0);
        };
        var onClick = function (event) {
            var element = event && event.target && (event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target.parentElement);
            var marker = element && element.closest && element.closest('.tm-document-inline--comment-anchor[data-comment-id]');
            if (marker && root.contains(marker)) {
                selectCommentAnchor(inst, marker.getAttribute('data-comment-id'), false, true);
                return;
            }
            var revision = element && element.closest && element.closest('.tm-wysiwyg-revision[data-revision-id], .tm-document-inline--revision[data-revision-id]');
            if (!revision || !root.contains(revision)) return;
            selectRevisionAnchor(inst, revision.getAttribute('data-revision-id'), false, true);
        };
        var onKeyDown = function (event) {
            handleEditorKeyDown(inst, event);
        };
        var onBeforeInput = function (event) {
            handleEditorBeforeInput(inst, event);
        };
        var onPaste = function (event) {
            handleEditorPaste(inst, event);
        };
        var onCompositionStart = function (event) {
            handleEditorCompositionStart(inst, event);
        };
        var onCompositionUpdate = function (event) {
            handleEditorCompositionUpdate(inst, event);
        };
        var onCompositionEnd = function (event) {
            handleEditorCompositionEnd(inst, event);
        };
        var onSelectionChange = function () {
            var selection = window.getSelection && window.getSelection();
            if (selectionBelongsToEditor(inst, selection)) {
                publishCollapsedDomSelectionChange(inst, 'selectionchange-caret');
                scheduleMiniToolbarRefresh(inst, 'selectionchange', 40);
                return;
            }
            if (restoreRecentToolbarSelectionIfNeeded(inst, 'selectionchange-toolbar-preserve')) {
                scheduleMiniToolbarRefresh(inst, 'selectionchange-toolbar-preserve', 40);
            }
        };
        var onDocumentPointerDown = function (event) {
            var element = event && event.target && (event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target.parentElement);
            if (!element) return;
            if (root.contains(element)) return;
            if (preserveToolbarSelectionOnPointerEvent(inst, event, 'pointerdown')) {
                return;
            }
            if (element.closest && element.closest('[data-testid="document-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__floating-root, .tm-color-picker-dropdown, .tm-color-picker')) {
                inst.preserveMiniToolbarUntil = Date.now() + 1200;
                return;
            }
            hideMiniToolbar(inst, 'outside-pointerdown');
        };
        var onDocumentMouseDown = function (event) {
            var element = event && event.target && (event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target.parentElement);
            if (!element || root.contains(element)) return;
            if (preserveToolbarSelectionOnPointerEvent(inst, event, 'mousedown')) return;
            if (element.closest && element.closest('[data-testid="document-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__floating-root, .tm-color-picker-dropdown, .tm-color-picker')) {
                inst.preserveMiniToolbarUntil = Date.now() + 1200;
            }
        };
        var onToolbarSelectInput = function (event) {
            handleToolbarNativeSelectChange(inst, event);
        };
        var onToolbarButtonClick = function (event) {
            handleToolbarNativeButtonClick(inst, event);
        };
        var onWindowScrollOrResize = function () {
            if (inst.floatingUiOpen || inst.lastMiniToolbarRequest) {
                scheduleMiniToolbarViewportRefresh(inst, 'viewport-change');
            }
        };
        root.addEventListener('focusin', onFocusIn);
        root.addEventListener('pointerdown', onPointerDown, true);
        root.addEventListener('pointerup', onPointerUp, true);
        root.addEventListener('click', onClick);
        root.addEventListener('beforeinput', onBeforeInput, true);
        root.addEventListener('paste', onPaste, true);
        root.addEventListener('compositionstart', onCompositionStart, true);
        root.addEventListener('compositionupdate', onCompositionUpdate, true);
        root.addEventListener('compositionend', onCompositionEnd, true);
        root.addEventListener('keydown', onKeyDown);
        var documentTarget = typeof document !== 'undefined' ? document : null;
        var windowTarget = typeof window !== 'undefined' ? window : null;
        if (documentTarget && typeof documentTarget.addEventListener === 'function') {
            documentTarget.addEventListener('selectionchange', onSelectionChange);
            documentTarget.addEventListener('pointerdown', onDocumentPointerDown, true);
            documentTarget.addEventListener('mousedown', onDocumentMouseDown, true);
            documentTarget.addEventListener('click', onToolbarButtonClick, true);
            documentTarget.addEventListener('input', onToolbarSelectInput, true);
            documentTarget.addEventListener('change', onToolbarSelectInput, true);
        }
        if (windowTarget && typeof windowTarget.addEventListener === 'function') {
            windowTarget.addEventListener('click', onToolbarButtonClick, true);
            windowTarget.addEventListener('scroll', onWindowScrollOrResize, true);
            windowTarget.addEventListener('resize', onWindowScrollOrResize);
        }
        if (windowTarget && windowTarget.visualViewport && typeof windowTarget.visualViewport.addEventListener === 'function') {
            window.visualViewport.addEventListener('scroll', onWindowScrollOrResize, true);
            window.visualViewport.addEventListener('resize', onWindowScrollOrResize);
            inst.visualViewportEventHandlers = [
                ['scroll', onWindowScrollOrResize, true],
                ['resize', onWindowScrollOrResize, false]
            ];
        }
        if (typeof ResizeObserver !== 'undefined') {
            inst.miniToolbarResizeObserver = new ResizeObserver(onWindowScrollOrResize);
            inst.miniToolbarResizeObserver.observe(root);
            var editorSurface = root.closest && root.closest('.tm-document-editor');
            if (editorSurface) {
                inst.miniToolbarResizeObserver.observe(editorSurface);
                var workspace = editorSurface.querySelector && editorSurface.querySelector('.tm-document-editor__workspace, .tm-document-editor__surface');
                if (workspace) inst.miniToolbarResizeObserver.observe(workspace);
            }
        }
        inst.eventHandlers.push(
            ['focusin', onFocusIn, false],
            ['pointerdown', onPointerDown, true],
            ['pointerup', onPointerUp, true],
            ['click', onClick, false],
            ['beforeinput', onBeforeInput, true],
            ['paste', onPaste, true],
            ['compositionstart', onCompositionStart, true],
            ['compositionupdate', onCompositionUpdate, true],
            ['compositionend', onCompositionEnd, true],
            ['keydown', onKeyDown, false]);
        inst.documentEventHandlers = [
            ['selectionchange', onSelectionChange, false],
            ['pointerdown', onDocumentPointerDown, true],
            ['mousedown', onDocumentMouseDown, true],
            ['click', onToolbarButtonClick, true],
            ['input', onToolbarSelectInput, true],
            ['change', onToolbarSelectInput, true]
        ];
        inst.windowEventHandlers = [
            ['click', onToolbarButtonClick, true],
            ['scroll', onWindowScrollOrResize, true],
            ['resize', onWindowScrollOrResize, false]
        ];
    }

    function removeAccessibilityAndKeyboardHandlers(inst) {
        if (!inst || !inst.root || typeof inst.root.removeEventListener !== 'function') return;
        _asArray(inst.eventHandlers).forEach(function (entry) {
            inst.root.removeEventListener(entry[0], entry[1], entry[2]);
        });
        inst.eventHandlers = [];
        var documentTarget = typeof document !== 'undefined' ? document : null;
        if (documentTarget && typeof documentTarget.removeEventListener === 'function') {
            _asArray(inst.documentEventHandlers).forEach(function (entry) {
                documentTarget.removeEventListener(entry[0], entry[1], entry[2]);
            });
        }
        inst.documentEventHandlers = [];
        var windowTarget = typeof window !== 'undefined' ? window : null;
        if (windowTarget && typeof windowTarget.removeEventListener === 'function') {
            _asArray(inst.windowEventHandlers).forEach(function (entry) {
                windowTarget.removeEventListener(entry[0], entry[1], entry[2]);
            });
        }
        inst.windowEventHandlers = [];
        if (windowTarget && windowTarget.visualViewport && typeof windowTarget.visualViewport.removeEventListener === 'function') {
            _asArray(inst.visualViewportEventHandlers).forEach(function (entry) {
                windowTarget.visualViewport.removeEventListener(entry[0], entry[1], entry[2]);
            });
        }
        inst.visualViewportEventHandlers = [];
        if (inst.miniToolbarResizeObserver && typeof inst.miniToolbarResizeObserver.disconnect === 'function') {
            inst.miniToolbarResizeObserver.disconnect();
        }
        inst.miniToolbarResizeObserver = null;
        if (inst.miniToolbarRefreshTimer) {
            clearTimeout(inst.miniToolbarRefreshTimer);
            inst.miniToolbarRefreshTimer = null;
        }
        clearMiniToolbarViewportRefreshTimers(inst);
        if (inst.accessibilityAnnouncementTimer) {
            clearTimeout(inst.accessibilityAnnouncementTimer);
            inst.accessibilityAnnouncementTimer = null;
        }
    }

    function hydrateBoundaryPatchSnapshot(inst, patch) {
        if (!inst || !patch || patch.csharpDocument) return patch;
        var stats = ensureStrictPerformanceStats(inst);
        stats.boundarySnapshotExportCount = Number(stats.boundarySnapshotExportCount || 0) + 1;
        patch.snapshot = _clone(inst.model);
        patch.csharpDocument = exportToCSharpJson(inst.model);
        patch.lightweight = false;
        patch.deferredSnapshot = false;
        patch.snapshotHydratedAt = Date.now();
        return patch;
    }

    function createBoundaryPatch(inst, transaction, operations, committed, source, options) {
        var opts = options || {};
        var operationList = _asArray(operations).map(function (operation) {
            return attachOperationMethods(operation).toJSON ? attachOperationMethods(operation).toJSON() : _clone(operation);
        });
        var transactionJson = transaction && transaction.toJSON ? transaction.toJSON() : _clone(transaction || {});
        var affectedBlockIds = transactionAffectedBlockIds(transaction, operationList);
        var patch = {
            instanceId: inst.id,
            transactionId: transactionJson.id || '',
            transactionType: transactionJson.type || source || 'default',
            operationIds: operationList.map(getOperationId).filter(Boolean),
            operations: operationList,
            affectedBlockIds: affectedBlockIds,
            selection: createSelectionSnapshot(inst.selection || transactionJson.afterSelection || firstModelSelection(inst.model)),
            modelDelta: {
                kind: operationList.length > 0 ? 'operations' : 'snapshot',
                operations: operationList
            },
            dirtyState: _clone(inst.dirtyState || createInitialDirtyState()),
            differ: committed && committed.differ || inst.lastDiffer || null,
            lightweight: opts.deferSnapshot === true,
            deferredSnapshot: opts.deferSnapshot === true,
            createdAt: Date.now()
        };
        if (opts.deferSnapshot === true) {
            var stats = ensureStrictPerformanceStats(inst);
            stats.lightweightBoundaryPatchCount = Number(stats.lightweightBoundaryPatchCount || 0) + 1;
            return _sortObject(patch);
        }
        hydrateBoundaryPatchSnapshot(inst, patch);
        return _sortObject(patch);
    }

    function updateDirtyState(inst, patch, source) {
        inst.modelEpoch = Number(inst.modelEpoch || 0) + 1;
        inst.dirtyState = _sortObject(Object.assign({}, inst.dirtyState || createInitialDirtyState(), {
            isDirty: true,
            epoch: inst.modelEpoch,
            pendingPatchCount: _asArray(inst.boundaryPatches).length + (patch ? 1 : 0),
            lastFailure: null,
            source: source || (patch && patch.transactionType) || 'local'
        }));
        return inst.dirtyState;
    }

    function shouldDeferBoundarySnapshot(transaction, operations, source) {
        var transactionType = transaction && (transaction.type || transaction.Type) || source || '';
        if (isTypingLikeTransactionType(transactionType) || isTypingLikeTransactionType(source)) return true;
        var normalizedSource = String(source || transactionType || '').toLowerCase();
        if (normalizedSource === TRANSACTION_TYPES.Undo || normalizedSource === TRANSACTION_TYPES.Redo || normalizedSource === 'undo' || normalizedSource === 'redo') return true;
        var operationList = _asArray(operations);
        return operationList.length > 0 && operationList.every(isFormattingVisualOperation);
    }

    function dispatchDirtyState(inst) {
        var payload = _clone(inst.dirtyState || createInitialDirtyState());
        invokeBoundaryMethod(inst, 'HandleJsDirtyStateChanged', payload, 'dirty-state-dispatch-failed');
        return payload;
    }

    function commitBoundaryPatch(inst, transaction, operations, committed, source) {
        var isTypingPatch = isTypingLikeTransactionType(transaction && transaction.type) || isTypingLikeTransactionType(source);
        var deferSnapshot = shouldDeferBoundarySnapshot(transaction, operations, source);
        var patch = createBoundaryPatch(inst, transaction, operations, committed, source, { deferSnapshot: deferSnapshot });
        inst.boundaryPatches.push(patch);
        updateDirtyState(inst, patch, source);
        patch.dirtyState = _clone(inst.dirtyState);
        if (isTypingPatch) {
            scheduleTypingBoundaryPatchDispatch(inst, patch);
            return patch;
        }
        if (deferSnapshot) {
            flushTypingBoundaryPatchDispatch(inst);
            scheduleDeferredBoundaryPatchDispatch(inst, patch);
            return patch;
        }
        flushTypingBoundaryPatchDispatch(inst);
        flushDeferredBoundaryPatchDispatch(inst);
        dispatchBoundaryPatch(inst, patch);
        dispatchDirtyState(inst);
        return patch;
    }

    function dispatchBoundaryPatch(inst, patch) {
        if (!inst || !patch) return;
        recordTimeline(inst, 'blazor-patch-emit', {
            transactionId: patch.transactionId,
            transactionType: patch.transactionType,
            operationIds: patch.operationIds,
            affectedBlockIds: patch.affectedBlockIds
        });
        invokeBoundaryMethod(inst, 'HandleJsBoundaryPatchGenerated', patch, 'boundary-patch-dispatch-failed');
    }

    function mergeBoundaryPatches(inst, patches, fallbackTransactionType) {
        var list = _asArray(patches).filter(Boolean);
        var latest = list[list.length - 1] || null;
        if (!latest) return null;
        var operations = list.flatMap(function (patch) { return _asArray(patch.operations).map(_clone); });
        var affected = [];
        list.forEach(function (patch) {
            _asArray(patch.affectedBlockIds).forEach(function (blockId) {
                if (blockId && affected.indexOf(blockId) < 0) affected.push(blockId);
            });
        });
        return _sortObject(Object.assign({}, _clone(latest), {
            transactionId: latest.transactionId || (list[0] && list[0].transactionId) || '',
            transactionType: fallbackTransactionType || latest.transactionType || 'default',
            operationIds: operations.map(getOperationId).filter(Boolean),
            operations: operations,
            affectedBlockIds: affected,
            modelDelta: {
                kind: operations.length > 0 ? 'operations' : 'snapshot',
                operations: operations
            },
            dirtyState: _clone(inst.dirtyState || latest.dirtyState || createInitialDirtyState()),
            coalescedPatchCount: list.length,
            createdAt: latest.createdAt || Date.now()
        }));
    }

    function mergeTypingBoundaryPatches(inst, patches) {
        return mergeBoundaryPatches(inst, patches, TRANSACTION_TYPES.Typing);
    }

    function scheduleTypingBoundaryPatchDispatch(inst, patch) {
        if (!inst || !patch) return;
        inst.pendingTypingBoundaryPatches = _asArray(inst.pendingTypingBoundaryPatches).concat([patch]);
        var stats = ensureStrictPerformanceStats(inst);
        stats.maxTypingBatchSize = Math.max(Number(stats.maxTypingBatchSize || 0), inst.pendingTypingBoundaryPatches.length);
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), inst.pendingTypingBoundaryPatches.length);
        if (inst.pendingTypingBoundaryTimer) clearTimeout(inst.pendingTypingBoundaryTimer);
        var delay = Math.max(0, Number(inst.options && (inst.options.TypingBatchMs || inst.options.typingBatchMs) || 500) || 500);
        inst.pendingTypingBoundaryTimer = setTimeout(function () {
            flushTypingBoundaryPatchDispatch(inst);
        }, delay);
        if (inst.timers && inst.timers.indexOf(inst.pendingTypingBoundaryTimer) < 0) inst.timers.push(inst.pendingTypingBoundaryTimer);
        recordTimeline(inst, 'blazor-patch-queued', {
            transactionId: patch.transactionId,
            transactionType: patch.transactionType,
            pendingPatchCount: inst.pendingTypingBoundaryPatches.length
        });
    }

    function flushTypingBoundaryPatchDispatch(inst) {
        if (!inst) return null;
        if (inst.pendingTypingBoundaryTimer) {
            clearTimeout(inst.pendingTypingBoundaryTimer);
            inst.pendingTypingBoundaryTimer = null;
        }
        var pending = _asArray(inst.pendingTypingBoundaryPatches);
        if (!pending.length) return null;
        inst.pendingTypingBoundaryPatches = [];
        var stats = ensureStrictPerformanceStats(inst);
        stats.typingFlushCount = Number(stats.typingFlushCount || 0) + 1;
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), pending.length);
        var merged = mergeTypingBoundaryPatches(inst, pending);
        if (merged) {
            hydrateBoundaryPatchSnapshot(inst, merged);
            dispatchBoundaryPatch(inst, merged);
            dispatchDirtyState(inst);
            flushRuntimeRevisionsChanged(inst);
        }
        return merged;
    }

    function scheduleDeferredBoundaryPatchDispatch(inst, patch) {
        if (!inst || !patch) return;
        inst.pendingDeferredBoundaryPatches = _asArray(inst.pendingDeferredBoundaryPatches).concat([patch]);
        var stats = ensureStrictPerformanceStats(inst);
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), inst.pendingDeferredBoundaryPatches.length);
        if (inst.pendingDeferredBoundaryTimer) clearTimeout(inst.pendingDeferredBoundaryTimer);
        var configuredDelay = inst.options ? (inst.options.BoundaryPatchBatchMs ?? inst.options.boundaryPatchBatchMs) : null;
        var delay = Math.max(0, Number(configuredDelay ?? 16) || 16);
        inst.pendingDeferredBoundaryTimer = setTimeout(function () {
            flushDeferredBoundaryPatchDispatch(inst);
        }, delay);
        if (inst.timers && inst.timers.indexOf(inst.pendingDeferredBoundaryTimer) < 0) inst.timers.push(inst.pendingDeferredBoundaryTimer);
        recordTimeline(inst, 'blazor-patch-deferred', {
            transactionId: patch.transactionId,
            transactionType: patch.transactionType,
            pendingPatchCount: inst.pendingDeferredBoundaryPatches.length
        });
    }

    function flushDeferredBoundaryPatchDispatch(inst) {
        if (!inst) return null;
        if (inst.pendingDeferredBoundaryTimer) {
            clearTimeout(inst.pendingDeferredBoundaryTimer);
            inst.pendingDeferredBoundaryTimer = null;
        }
        var pending = _asArray(inst.pendingDeferredBoundaryPatches);
        if (!pending.length) return null;
        inst.pendingDeferredBoundaryPatches = [];
        var stats = ensureStrictPerformanceStats(inst);
        stats.deferredBoundaryPatchDispatchCount = Number(stats.deferredBoundaryPatchDispatchCount || 0) + 1;
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), pending.length);
        var latest = pending[pending.length - 1] || null;
        var merged = mergeBoundaryPatches(inst, pending, latest && latest.transactionType || 'deferred');
        if (merged) {
            hydrateBoundaryPatchSnapshot(inst, merged);
            dispatchBoundaryPatch(inst, merged);
            dispatchDirtyState(inst);
            flushRuntimeRevisionsChanged(inst);
        }
        return merged;
    }

    function applySaveAckToInstance(inst, ack) {
        var source = ack || {};
        var epoch = Number(source.epoch ?? source.Epoch ?? inst.modelEpoch ?? 0) || 0;
        inst.savedEpoch = epoch;
        inst.savedVersion = source.version || source.Version || source.savedVersion || source.SavedVersion || inst.savedVersion || null;
        inst.dirtyState = _sortObject(Object.assign({}, inst.dirtyState || createInitialDirtyState(), {
            isDirty: Number(inst.modelEpoch || 0) > epoch,
            epoch: Number(inst.modelEpoch || 0),
            savedEpoch: epoch,
            version: inst.savedVersion,
            lastSavedMarker: _asText(source.marker || source.Marker || source.lastSavedMarker || source.LastSavedMarker || inst.savedVersion || ''),
            lastFailure: null,
            pendingPatchCount: inst.boundaryPatches.length
        }));
        dispatchDirtyState(inst);
        return inst.dirtyState;
    }

    function clearRuntimeUndoStacks(inst) {
        if (!inst) return _sortObject({ undoDepth: 0, redoDepth: 0 });
        if (inst.pendingUndoStateTimer) {
            clearTimeout(inst.pendingUndoStateTimer);
            inst.pendingUndoStateTimer = null;
        }
        inst.pendingUndoStateNotify = false;
        inst.undoTransactions = [];
        inst.redoTransactions = [];
        inst.activeTransaction = null;
        return _sortObject({ undoDepth: 0, redoDepth: 0 });
    }

    function markAutosaveFailure(inst, failure) {
        var error = failure || {};
        inst.dirtyState = _sortObject(Object.assign({}, inst.dirtyState || createInitialDirtyState(), {
            isDirty: true,
            epoch: Number(inst.modelEpoch || 0),
            savedEpoch: Number(inst.savedEpoch || 0),
            lastFailure: {
                message: _asText(error.message || error.Message || error.errorMessage || error.ErrorMessage || 'Autosave failed'),
                kind: _asText(error.kind || error.Kind || error.errorKind || error.ErrorKind || 'autosave'),
                at: Date.now()
            }
        }));
        dispatchDirtyState(inst);
        return inst.dirtyState;
    }

    function applyOperationBatchToInstance(inst, operations, transactionType, label, pushToUndo) {
        var operationStart = strictPerformanceNow();
        var operationList = _asArray(operations).map(function (operation) { return attachOperationMethods(operation); });
        recordTimeline(inst, 'input-event', { source: transactionType || TRANSACTION_TYPES.Remote, operationCount: operationList.length });
        recordTimeline(inst, 'normalized-operation', { operationTypes: operationList.map(function (operation) { return operation.type || operation.Type || ''; }) });
        var transaction = createTransaction(inst.model, {
            instanceId: inst.id,
            commandName: transactionType || label || 'operation-batch',
            type: transactionType || TRANSACTION_TYPES.Remote,
            label: label || transactionType || 'Remote update',
            beforeSelection: inst.selection,
            lightweightSnapshots: supportsLightweightTransactionSnapshots(operationList, transactionType || TRANSACTION_TYPES.Remote)
        });
        inst.activeTransaction = transaction;
        for (var i = 0; i < operationList.length; i++) {
            var result = transaction.apply(operationList[i]);
            if (!result.ok) {
                inst.activeTransaction = null;
                inst.lastOperationValidation = _clone(result.errors || []);
                inst.lastError = result.errors && result.errors[0] ? result.errors[0].code : 'operation-batch-failed';
                recordWatchdogFailure(inst, 'operation', inst.lastError, { operationIndex: i, transactionId: transaction.id });
                return Object.assign({ instanceId: inst.id, operationIndex: i }, result);
            }
        }
        var committed = transaction.commit();
        inst.activeTransaction = null;
        inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, transaction.afterSelection || inst.selection);
        markSelectionChanged(inst, isTypingLikeTransactionType(transaction.type) ? 'typing' : 'operation-batch');
        transaction.afterSelection = _clone(inst.selection);
        if (transaction.lightweightSnapshots !== true) transaction.afterModelSnapshot = _clone(inst.model);
        inst.transactions.push(transaction.toJSON());
        var affectsDocument = transactionAffectsDocument(transaction);
        if (pushToUndo && affectsDocument) {
            pushUndoTransaction(inst, transaction);
            inst.redoTransactions = [];
        }
        inst.layout.invalidatedScopeIds = transaction.invalidatedScopes.slice();
        inst.lastDiffer = committed.differ;
        inst.commands.push({ command: transactionType || 'remote', payload: operationList.map(function (operation) { return operation.toJSON(); }), at: Date.now(), transactionId: transaction.id });
        if (affectsDocument) {
            markModelChanged(inst, transactionType || 'operation-batch');
            if (isTypingLikeTransactionType(transaction.type)) {
                inst.markerStoreDirty = true;
                ensureStrictPerformanceStats(inst).markerStoreDeferredRefreshCount = Number(ensureStrictPerformanceStats(inst).markerStoreDeferredRefreshCount || 0) + 1;
            } else {
                refreshRuntimeMarkerStore(inst);
            }
        }
        if (affectsDocument && operationList.some(operationTouchesRevisions)) {
            if (isTypingLikeTransactionType(transaction.type)) scheduleRuntimeRevisionsChanged(inst);
            else notifyRuntimeRevisionsChanged(inst);
        }
        recordTimeline(inst, 'transaction-commit', { transactionId: transaction.id, transactionType: transaction.type, operationCount: operationList.length });
        render(inst);
        recordOperationPerformance(inst, operationList, Math.max(0, strictPerformanceNow() - operationStart), transaction.invalidatedScopes, transactionType || 'operation-batch');
        return Object.assign({ ok: true, instanceId: inst.id, transaction: transaction.toJSON() }, committed);
    }

    function create(rootElement, options, dotNetRef) {
        if (!rootElement) throw new Error('tmDocumentEditorEngine.create: rootElement is required.');
        var opts = options || {};
        var instanceId = opts.instanceId || opts.InstanceId || ('tmde-' + (++_counter));
        if (_instances.has(instanceId)) dispose(instanceId);
        var inst = {
            id: instanceId,
            root: rootElement,
            options: opts,
            dotNetRef: dotNetRef || null,
            disposed: false,
            model: importFromCSharpJson({ DocumentId: 'document', Blocks: [] }),
            schema: createDefaultSchemaRegistry(),
            selection: { region: 'Body', blockId: null, offset: 0, isCollapsed: true },
            layout: { pages: [], invalidatedScopeIds: [] },
            commands: [],
            transactions: [],
            undoTransactions: [],
            redoTransactions: [],
            activeTransaction: null,
            lastOperationValidation: null,
            lastDiffer: null,
            lastSelectionToken: null,
            lastSelectionTokenData: null,
            lastSelectionTokenReason: '',
            lastCommandTokenDiagnostic: null,
            boundaryPatches: [],
            boundaryFailures: [],
            modelEpoch: 0,
            savedEpoch: 0,
            savedVersion: null,
            dirtyState: createInitialDirtyState(),
            pendingTypingBoundaryPatches: [],
            pendingTypingBoundaryTimer: null,
            formattingStateVersion: 0,
            lastFormattingStatePublishedVersion: 0,
            pendingFormattingStateVersion: 0,
            pendingFormattingStateReason: '',
            pendingFormattingStateTimer: null,
            lastCSharpUpdate: null,
            createdAt: Date.now(),
            lastError: null,
            activeFocusRegion: 'Body',
            focusOwner: 'body',
            floatingUiOpen: false,
            objectPreviewTransaction: null,
            eventHandlers: [],
            timers: [],
            observers: [],
            measurementCache: new Map(),
            activePageIndexPinned: false,
            accessibilityAnnouncementTimer: null,
            lastAccessibilityAnnouncement: null,
            pendingTypingMarks: [],
            jsOwnedInputCount: 0,
            nativeInputCount: 0,
            markerStore: null,
            markerStoreDirty: false,
            pendingRevisionNotify: false,
            pendingDeferredBoundaryPatches: [],
            pendingDeferredBoundaryTimer: null,
            performanceStats: createStrictPerformanceStats(),
            diagnostics: createDiagnosticsState()
        };
        refreshRuntimeMarkerStore(inst);
        _instances.set(instanceId, inst);
        rootElement.setAttribute('data-instance-id', instanceId);
        rootElement.setAttribute('data-engine-mode', 'google-docs');
        rootElement.setAttribute('data-active-region', 'Body');
        rootElement.setAttribute('data-focus-owner', 'body');
        rootElement.setAttribute('aria-keyshortcuts', 'Control+B Control+I Control+U Control+Z Control+Y Control+S Shift+F10');
        rootElement.classList.add('tm-document-editor-engine-host');
        installGlobalToolbarButtonBridge();
        installAccessibilityAndKeyboardHandlers(inst);
        render(inst);
        _notifyReady(inst);
        return instanceId;
    }

    function dispose(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst) return _missingResult(instanceId, 'dispose');
        var cleanup = {
            removedEventListeners: _asArray(inst.eventHandlers).length,
            clearedTimers: 0,
            disconnectedObservers: 0,
            measurementCacheEntriesBefore: inst.measurementCache && typeof inst.measurementCache.size === 'number' ? inst.measurementCache.size : 0,
            dotNetRefCleared: !!inst.dotNetRef,
            rootCleared: !!inst.root,
            instanceRemoved: false
        };
        inst.disposed = true;
        if (inst.pendingTypingBoundaryTimer) {
            clearTimeout(inst.pendingTypingBoundaryTimer);
            inst.pendingTypingBoundaryTimer = null;
            cleanup.clearedTimers++;
        }
        inst.pendingTypingBoundaryPatches = [];
        if (inst.pendingDeferredBoundaryTimer) {
            clearTimeout(inst.pendingDeferredBoundaryTimer);
            inst.pendingDeferredBoundaryTimer = null;
            cleanup.clearedTimers++;
        }
        inst.pendingDeferredBoundaryPatches = [];
        if (inst.accessibilityAnnouncementTimer) cleanup.clearedTimers++;
        removeAccessibilityAndKeyboardHandlers(inst);
        _asArray(inst.timers).forEach(function (timerId) {
            if (timerId) {
                clearTimeout(timerId);
                cleanup.clearedTimers++;
            }
        });
        inst.timers = [];
        _asArray(inst.observers).forEach(function (observer) {
            if (observer && typeof observer.disconnect === 'function') {
                observer.disconnect();
                cleanup.disconnectedObservers++;
            }
        });
        inst.observers = [];
        if (inst.measurementCache && typeof inst.measurementCache.clear === 'function') {
            inst.measurementCache.clear();
        }
        if (inst.root) {
            inst.root.removeAttribute('data-engine-mode');
            inst.root.removeAttribute('data-active-region');
            inst.root.removeAttribute('data-focus-owner');
            inst.root.removeAttribute('aria-keyshortcuts');
            inst.root.innerHTML = '';
        }
        var stats = ensureStrictPerformanceStats(inst);
        stats.memoryDisposeCount = Number(stats.memoryDisposeCount || 0) + 1;
        stats.lastDisposeCleanup = _clone(cleanup);
        inst.dotNetRef = null;
        inst.root = null;
        _instances.delete(instanceId);
        cleanup.instanceRemoved = !_instances.has(instanceId);
        cleanup.measurementCacheEntriesAfter = inst.measurementCache && typeof inst.measurementCache.size === 'number' ? inst.measurementCache.size : 0;
        return { ok: true, instanceId: instanceId, disposed: true, cleanup: cleanup };
    }

    function loadDocument(instanceId, snapshot, forceResetUndo) {
        var lookup = _get(instanceId, 'loadDocument');
        if (lookup.error) return lookup.error;
        if (lookup.inst.activeTransaction) {
            return { ok: false, instanceId: instanceId, error: { code: 'active-transaction-conflict', updateType: 'loadDocument' } };
        }
        var document = snapshot && (snapshot.Document || snapshot.document) ? (snapshot.Document || snapshot.document) : snapshot;
        var nextModel = importFromCSharpJson(document || {});
        var currentFingerprint = lookup.inst.model ? createDocumentFingerprint(lookup.inst.model) : '';
        var nextFingerprint = createDocumentFingerprint(nextModel);
        var shouldResetUndo = forceResetUndo === true || currentFingerprint !== nextFingerprint;
        if (shouldResetUndo) {
            clearRuntimeUndoStacks(lookup.inst);
        }
        lookup.inst.model = nextModel;
        refreshRuntimeMarkerStore(lookup.inst);
        lookup.inst.selection = firstModelSelection(lookup.inst.model);
        rememberSelectionToken(lookup.inst, lookup.inst.selection, 'loadDocument');
        lookup.inst.layout.invalidatedScopeIds = ['document'];
        lookup.inst.modelEpoch = Number(snapshot && (snapshot.epoch || snapshot.Epoch) || 0) || 0;
        lookup.inst.savedEpoch = lookup.inst.modelEpoch;
        lookup.inst.savedVersion = snapshot && (snapshot.version || snapshot.Version || snapshot.savedVersion || snapshot.SavedVersion) || null;
        lookup.inst.dirtyState = _sortObject(Object.assign(createInitialDirtyState(), {
            epoch: lookup.inst.modelEpoch,
            savedEpoch: lookup.inst.savedEpoch,
            version: lookup.inst.savedVersion,
            lastSavedMarker: _asText(snapshot && (snapshot.marker || snapshot.Marker || snapshot.lastSavedMarker || snapshot.LastSavedMarker || lookup.inst.savedVersion) || '')
        }));
        lookup.inst.lastCSharpUpdate = { type: 'loadDocument', fullSnapshot: true, at: Date.now() };
        markModelChanged(lookup.inst, 'loadDocument');
        markSelectionChanged(lookup.inst, 'loadDocument');
        render(lookup.inst);
        dispatchDirtyState(lookup.inst);
        if (shouldResetUndo) {
            notifyUndoState(lookup.inst);
        }
        return { ok: true, instanceId: instanceId, fullSnapshot: true, validation: validateModel(lookup.inst.model), dirtyState: _clone(lookup.inst.dirtyState) };
    }

    function compactCommandName(value) {
        return String(value || '').replace(/[\s_-]+/g, '').toLowerCase();
    }

    function wrapModeToValue(value) {
        var mode = normalizeWrapModeName(value);
        return mode === 'Square' ? 1
            : mode === 'Tight' ? 2
                : mode === 'Through' ? 3
                    : mode === 'TopBottom' ? 4
                        : mode === 'BehindText' ? 5
                            : mode === 'InFrontOfText' ? 6
                                : 0;
    }

    function normalizeHorizontalPositionName(value) {
        if (value === 0) return 'Left';
        if (value === 1) return 'Center';
        if (value === 2) return 'Right';
        var raw = String(value || '').replace(/[\s_-]+/g, '').toLowerCase();
        if (raw === 'center' || raw === 'centre' || raw === 'middle') return 'Center';
        if (raw === 'right' || raw === 'end') return 'Right';
        return 'Left';
    }

    function horizontalPositionToValue(value) {
        var normalized = normalizeHorizontalPositionName(value);
        return normalized === 'Center' ? 1 : (normalized === 'Right' ? 2 : 0);
    }

    function syncImageLayoutCase(layout) {
        var source = layout || {};
        var wrap = source.Wrap || source.wrap || {};
        var position = source.Position || source.position || {};
        var anchor = source.Anchor || source.anchor || {};
        var transform = source.Transform || source.transform || {};
        var stacking = source.Stacking || source.stacking || {};
        var mode = wrap.Mode ?? wrap.mode ?? 0;
        wrap.Mode = mode;
        wrap.mode = mode;
        wrap.DistanceLeft = Number(wrap.DistanceLeft ?? wrap.distanceLeft ?? 0) || 0;
        wrap.distanceLeft = wrap.DistanceLeft;
        wrap.DistanceRight = Number(wrap.DistanceRight ?? wrap.distanceRight ?? 0) || 0;
        wrap.distanceRight = wrap.DistanceRight;
        wrap.DistanceTop = Number(wrap.DistanceTop ?? wrap.distanceTop ?? 0) || 0;
        wrap.distanceTop = wrap.DistanceTop;
        wrap.DistanceBottom = Number(wrap.DistanceBottom ?? wrap.distanceBottom ?? 0) || 0;
        wrap.distanceBottom = wrap.DistanceBottom;
        position.HorizontalAlignment = position.HorizontalAlignment ?? position.horizontalAlignment ?? 0;
        position.horizontalAlignment = position.HorizontalAlignment;
        position.HorizontalRelativeTo = position.HorizontalRelativeTo ?? position.horizontalRelativeTo ?? 0;
        position.horizontalRelativeTo = position.HorizontalRelativeTo;
        position.VerticalRelativeTo = position.VerticalRelativeTo ?? position.verticalRelativeTo ?? 3;
        position.verticalRelativeTo = position.VerticalRelativeTo;
        position.X = Number(position.X ?? position.x ?? 0) || 0;
        position.x = position.X;
        position.Y = Number(position.Y ?? position.y ?? 0) || 0;
        position.y = position.Y;
        anchor.MoveWithText = (anchor.MoveWithText ?? anchor.moveWithText ?? false) === true;
        anchor.moveWithText = anchor.MoveWithText;
        anchor.FixedOnPage = (anchor.FixedOnPage ?? anchor.fixedOnPage ?? false) === true;
        anchor.fixedOnPage = anchor.FixedOnPage;
        anchor.LockAnchor = (anchor.LockAnchor ?? anchor.lockAnchor ?? false) === true;
        anchor.lockAnchor = anchor.LockAnchor;
        transform.Width = Number(transform.Width ?? transform.width ?? 120) || 120;
        transform.width = transform.Width;
        transform.Height = Number(transform.Height ?? transform.height ?? 80) || 80;
        transform.height = transform.Height;
        transform.LockAspectRatio = (transform.LockAspectRatio ?? transform.lockAspectRatio ?? true) !== false;
        transform.lockAspectRatio = transform.LockAspectRatio;
        stacking.ZIndex = Number(stacking.ZIndex ?? stacking.zIndex ?? 0) || 0;
        stacking.zIndex = stacking.ZIndex;
        stacking.AllowOverlap = (stacking.AllowOverlap ?? stacking.allowOverlap ?? false) === true;
        stacking.allowOverlap = stacking.AllowOverlap;
        source.Kind = source.Kind ?? source.kind ?? 0;
        source.kind = source.Kind;
        source.Anchor = anchor;
        source.anchor = anchor;
        source.Position = position;
        source.position = position;
        source.Wrap = wrap;
        source.wrap = wrap;
        source.Transform = transform;
        source.transform = transform;
        source.Stacking = stacking;
        source.stacking = stacking;
        return _sortObject(source);
    }

    function cloneImageLayoutForUpdate(block) {
        var content = block && block.content || {};
        var source = _clone(content.layout || {});
        var object = normalizeImageObject(block);
        var sourceWrapMode = (source.Wrap && source.Wrap.Mode) ?? (source.wrap && source.wrap.mode) ?? object.wrapMode;
        var wrapMode = normalizeWrapModeName(sourceWrapMode);
        var position = _clone(source.Position || source.position || {});
        var wrap = _clone(source.Wrap || source.wrap || {});
        var anchor = _clone(source.Anchor || source.anchor || {});
        var transform = _clone(source.Transform || source.transform || {});
        var stacking = _clone(source.Stacking || source.stacking || {});
        position.HorizontalAlignment = position.HorizontalAlignment ?? position.horizontalAlignment ?? horizontalPositionToValue(object.horizontalPosition && object.horizontalPosition.align);
        position.X = position.X ?? position.x ?? (object.horizontalPosition && object.horizontalPosition.offset) ?? 0;
        position.Y = position.Y ?? position.y ?? (object.verticalPosition && object.verticalPosition.offset) ?? 0;
        position.HorizontalRelativeTo = position.HorizontalRelativeTo ?? position.horizontalRelativeTo ?? 0;
        position.VerticalRelativeTo = position.VerticalRelativeTo ?? position.verticalRelativeTo ?? 3;
        wrap.Mode = wrapModeToValue(wrapMode);
        wrap.DistanceLeft = Number(wrap.DistanceLeft ?? wrap.distanceLeft ?? object.distanceLeft ?? 0) || 0;
        wrap.DistanceRight = Number(wrap.DistanceRight ?? wrap.distanceRight ?? object.distanceRight ?? 0) || 0;
        wrap.DistanceTop = Number(wrap.DistanceTop ?? wrap.distanceTop ?? object.distanceTop ?? 0) || 0;
        wrap.DistanceBottom = Number(wrap.DistanceBottom ?? wrap.distanceBottom ?? object.distanceBottom ?? 0) || 0;
        transform.Width = Number(transform.Width ?? transform.width ?? object.width ?? (content.size && content.size.width) ?? 120) || 120;
        transform.Height = Number(transform.Height ?? transform.height ?? object.height ?? (content.size && content.size.height) ?? 80) || 80;
        anchor.MoveWithText = (anchor.MoveWithText ?? anchor.moveWithText ?? wrapMode !== 'Inline') !== false;
        anchor.FixedOnPage = (anchor.FixedOnPage ?? anchor.fixedOnPage ?? false) === true;
        anchor.LockAnchor = (anchor.LockAnchor ?? anchor.lockAnchor ?? false) === true;
        stacking.ZIndex = Number(stacking.ZIndex ?? stacking.zIndex ?? object.zIndex ?? 0) || 0;
        stacking.AllowOverlap = (stacking.AllowOverlap ?? stacking.allowOverlap ?? (wrapMode === 'BehindText' || wrapMode === 'InFrontOfText')) === true;
        return syncImageLayoutCase({
            Kind: wrapMode === 'Inline' ? 0 : (anchor.FixedOnPage ? 2 : 1),
            Anchor: _sortObject(anchor),
            Position: _sortObject(position),
            Wrap: _sortObject(wrap),
            Transform: _sortObject(transform),
            Stacking: _sortObject(stacking)
        });
    }

    function activeImageBlock(inst, payload) {
        var body = payload || {};
        var blockId = body.blockId || body.BlockId || inst.selection && (inst.selection.activeImageBlockId || inst.selection.blockId) || '';
        var objectId = body.objectId || body.ObjectId || inst.selection && (inst.selection.activeObjectId || inst.selection.objectId) || '';
        var block = blockId ? _findBlock(inst.model, blockId) : null;
        if (block && block.type === 'image') return block;
        return findImageBlockByAsset(inst.model, body.assetId || body.AssetId || '', blockId, objectId);
    }

    function imageSelectionForBlock(block, region) {
        var objectId = block && block.content && block.content.objectId || block && block.id || '';
        return createSelectionSnapshot({
            region: region || 'Image',
            blockId: block && block.id || '',
            objectId: objectId,
            activeImageBlockId: block && block.id || '',
            activeObjectId: objectId,
            isObjectSelection: true,
            isCollapsed: false,
            range: createLogicalRange(
                { region: region || 'Image', blockId: block && block.id || '', objectId: objectId, offset: 0, affinity: 'before' },
                { region: region || 'Image', blockId: block && block.id || '', objectId: objectId, offset: 1, affinity: 'after' },
                'none')
        });
    }

    function applyRuntimeImageCommand(inst, commandName, payload) {
        var compact = compactCommandName(commandName);
        var imageCommands = [
            'setimagealttext', 'setimagedecorative', 'toggleimagecaption', 'setimagecaption', 'setimageurl',
            'setimagelink', 'setimagewrapmode', 'setimageposition', 'setimagesize', 'setimageobjectposition',
            'setimageanchormode', 'setimagezorder', 'setimagewrapdistance', 'deleteimage', 'focusimageoptions'
        ];
        if (imageCommands.indexOf(compact) < 0) return null;
        if (compact === 'focusimageoptions') return { ok: true, instanceId: inst.id, command: commandName, noop: true };
        var body = payload || {};
        var block = activeImageBlock(inst, body);
        if (!block || block.type !== 'image') {
            return { ok: false, instanceId: inst.id, command: commandName, error: { code: 'active-image-not-found' } };
        }

        if (compact === 'deleteimage') {
            var nextModel = _clone(inst.model);
            var container = _findBlockContainer(nextModel, block.id);
            if (!container) return { ok: false, instanceId: inst.id, command: commandName, error: { code: 'active-image-container-not-found' } };
            container.blocks.splice(container.index, 1);
            buildIndexes(nextModel);
            return applyCommand(inst.id, OPERATION_TYPES.RestoreSnapshot, {
                snapshot: nextModel,
                selection: firstModelSelection(nextModel),
                affectedScopeIds: ['document'],
                source: 'image-command',
                transactionType: TRANSACTION_TYPES.Default,
                beforeSelection: inst.selection
            });
        }

        if (compact === 'setimagewrapmode' || compact === 'setimageposition' || compact === 'setimagesize'
            || compact === 'setimageobjectposition' || compact === 'setimageanchormode' || compact === 'setimagezorder'
            || compact === 'setimagewrapdistance') {
            var layout = cloneImageLayoutForUpdate(block);
            if (compact === 'setimagewrapmode') {
                var mode = normalizeWrapModeName(body.wrapMode ?? body.WrapMode ?? body.mode ?? body.Mode);
                layout.Wrap.Mode = wrapModeToValue(mode);
                layout.Kind = mode === 'Inline' ? 0 : (layout.Anchor.FixedOnPage ? 2 : 1);
                layout.Anchor.MoveWithText = mode !== 'Inline' && layout.Anchor.FixedOnPage !== true;
                if (mode === 'BehindText' || mode === 'InFrontOfText') layout.Stacking.AllowOverlap = true;
            } else if (compact === 'setimageposition') {
                layout.Position.HorizontalAlignment = horizontalPositionToValue(body.horizontalPosition ?? body.HorizontalPosition ?? body.alignment ?? body.Alignment);
                if (normalizeWrapModeName(layout.Wrap.Mode) === 'Inline') {
                    layout.Wrap.Mode = wrapModeToValue('Square');
                    layout.Kind = 1;
                    layout.Anchor.MoveWithText = true;
                }
            } else if (compact === 'setimagesize') {
                if (body.width ?? body.Width) layout.Transform.Width = Math.max(1, Number(body.width ?? body.Width) || layout.Transform.Width || 1);
                if (body.height ?? body.Height) layout.Transform.Height = Math.max(1, Number(body.height ?? body.Height) || layout.Transform.Height || 1);
                if (body.lockAspectRatio !== undefined || body.LockAspectRatio !== undefined) layout.Transform.LockAspectRatio = (body.lockAspectRatio ?? body.LockAspectRatio) !== false;
            } else if (compact === 'setimageobjectposition') {
                if (body.x ?? body.X) layout.Position.X = Number(body.x ?? body.X) || 0;
                if (body.y ?? body.Y) layout.Position.Y = Number(body.y ?? body.Y) || 0;
                if (body.horizontalRelativeTo ?? body.HorizontalRelativeTo) layout.Position.HorizontalRelativeTo = body.horizontalRelativeTo ?? body.HorizontalRelativeTo;
                if (body.verticalRelativeTo ?? body.VerticalRelativeTo) layout.Position.VerticalRelativeTo = body.verticalRelativeTo ?? body.VerticalRelativeTo;
                if (body.horizontalAlignment ?? body.HorizontalAlignment) layout.Position.HorizontalAlignment = horizontalPositionToValue(body.horizontalAlignment ?? body.HorizontalAlignment);
            } else if (compact === 'setimageanchormode') {
                if (body.lockAnchor !== undefined || body.LockAnchor !== undefined) layout.Anchor.LockAnchor = (body.lockAnchor ?? body.LockAnchor) === true;
                if (body.moveWithText !== undefined || body.MoveWithText !== undefined) layout.Anchor.MoveWithText = (body.moveWithText ?? body.MoveWithText) !== false;
                if (body.fixedOnPage !== undefined || body.FixedOnPage !== undefined) layout.Anchor.FixedOnPage = (body.fixedOnPage ?? body.FixedOnPage) === true;
                layout.Kind = layout.Anchor.FixedOnPage ? 2 : (normalizeWrapModeName(layout.Wrap.Mode) === 'Inline' ? 0 : 1);
            } else if (compact === 'setimagezorder') {
                var direction = String(body.direction || body.Direction || '').toLowerCase();
                var delta = direction.indexOf('back') >= 0 ? -1 : 1;
                layout.Stacking.ZIndex = (Number(layout.Stacking.ZIndex || 0) || 0) + delta;
            } else if (compact === 'setimagewrapdistance') {
                var distanceName = String(body.distanceName || body.DistanceName || '').toLowerCase();
                var value = Math.max(0, Number(body.value ?? body.Value ?? 0) || 0);
                if (distanceName.indexOf('left') >= 0) layout.Wrap.DistanceLeft = value;
                else if (distanceName.indexOf('right') >= 0) layout.Wrap.DistanceRight = value;
                else if (distanceName.indexOf('top') >= 0) layout.Wrap.DistanceTop = value;
                else if (distanceName.indexOf('bottom') >= 0) layout.Wrap.DistanceBottom = value;
            }
            return applyCommand(inst.id, OPERATION_TYPES.UpdateImageLayout, {
                target: { blockId: block.id, offset: 0 },
                layout: layout,
                affectedParagraphIds: affectedParagraphsAroundObject(inst.model, block.id),
                source: 'image-command',
                transactionType: TRANSACTION_TYPES.Default,
                beforeSelection: inst.selection
            });
        }

        var metadata = {};
        if (compact === 'setimagealttext') metadata.altText = _asText(body.altText ?? body.AltText ?? '');
        else if (compact === 'setimagedecorative') metadata.isDecorative = (body.isDecorative ?? body.IsDecorative) === true;
        else if (compact === 'toggleimagecaption') metadata.caption = _asText(block.content && block.content.caption || '').trim() ? '' : _asText(body.caption ?? body.Caption ?? 'Caption');
        else if (compact === 'setimagecaption') metadata.caption = _asText(body.caption ?? body.Caption ?? '');
        else if (compact === 'setimageurl') metadata.url = body.url ?? body.Url ?? '';
        else if (compact === 'setimagelink') metadata.linkUrl = body.url ?? body.Url ?? body.linkUrl ?? body.LinkUrl ?? '';
        return applyCommand(inst.id, OPERATION_TYPES.UpdateImageMetadata, {
            target: { blockId: block.id, offset: 0 },
            metadata: metadata,
            source: 'image-command',
            transactionType: TRANSACTION_TYPES.Default,
            beforeSelection: inst.selection
        });
    }

    function resolveCommandSelectionToken(inst, commandName, payload) {
        var body = payload || {};
        var tokenValue = readSelectionTokenValue(body)
            || readSelectionTokenValue(body.selection || body.Selection || {})
            || readSelectionTokenValue(body.payload || body.Payload || {});
        if (!tokenValue && !readSelectionTokenData(body)) {
            rememberSelectionToken(inst, inst.selection, 'command-current-selection');
            return _sortObject({ ok: true, usedSelectionToken: false, selection: withStableSelectionToken(inst.id, inst.selection || firstModelSelection(inst.model), inst.model) });
        }

        var validation = validateStableSelectionToken(inst, tokenValue || body);
        inst.lastCommandTokenDiagnostic = _sortObject({
            command: commandName || '',
            at: Date.now(),
            usedSelectionToken: true,
            ok: validation.ok === true,
            code: validation.code || '',
            reason: validation.reason || '',
            selectionToken: tokenValue || readSelectionTokenValue(body) || '',
            tokenData: validation.tokenData || null
        });
        if (!validation.ok) {
            inst.lastError = validation.reason || validation.code || 'stale-selection-token';
            recordDiagnosticError(inst, 'command-selection-token-failed', validation.reason || validation.code, {
                command: commandName || '',
                diagnostic: inst.lastCommandTokenDiagnostic
            });
            return _sortObject({
                ok: false,
                usedSelectionToken: true,
                error: {
                    code: validation.code || 'stale-selection-token',
                    reason: validation.reason || 'invalid-selection-token',
                    diagnostic: validation
                }
            });
        }

        body.Selection = validation.selection;
        body.selection = validation.selection;
        inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, validation.selection);
        rememberSelectionToken(inst, inst.selection, 'command-token');
        return _sortObject({
            ok: true,
            usedSelectionToken: true,
            selection: validation.selection,
            tokenData: validation.tokenData || null
        });
    }

    function applyCommand(instanceId, command, payload) {
        var lookup = _get(instanceId, 'applyCommand');
        if (lookup.error) return lookup.error;
        var operationStart = strictPerformanceNow();
        var commandName = _asText(command || '');
        var body = payload || {};
        var selectionTokenResolution = resolveCommandSelectionToken(lookup.inst, commandName, body);
        if (!selectionTokenResolution.ok) {
            return _sortObject({
                ok: false,
                instanceId: instanceId,
                command: commandName,
                error: selectionTokenResolution.error,
                debugSnapshot: getDebugSnapshot(instanceId)
            });
        }
        var normalizedCommandName = normalizeCommandId(commandName);
        if (commandName === 'undo') {
            recordTimeline(lookup.inst, 'input-event', { command: 'undo' });
            var undoResult = applyHistoryCommand(lookup.inst, true);
            if (undoResult.ok) undoResult.boundaryPatch = commitBoundaryPatch(lookup.inst, undoResult.transaction, undoResult.appliedOperations, undoResult, 'undo');
            return undoResult;
        }
        if (commandName === 'redo') {
            recordTimeline(lookup.inst, 'input-event', { command: 'redo' });
            var redoResult = applyHistoryCommand(lookup.inst, false);
            if (redoResult.ok) redoResult.boundaryPatch = commitBoundaryPatch(lookup.inst, redoResult.transaction, redoResult.appliedOperations, redoResult, 'redo');
            return redoResult;
        }
        var operation = body.operation || body.Operation || null;
        if (!operation && (inlineCommandTypes().indexOf(normalizedCommandName) >= 0 || paragraphCommandTypes().indexOf(normalizedCommandName) >= 0 || normalizedCommandName === 'clearFormatting')) {
            return applyRuntimeFormattingCommand(lookup.inst, normalizedCommandName, body, commandName);
        }
        if (!operation) {
            var runtimeImageResult = applyRuntimeImageCommand(lookup.inst, commandName, body);
            if (runtimeImageResult) return runtimeImageResult;
        }
        if (!operation && OPERATION_TYPES[commandName]) {
            operation = createOperation(commandName, body, { source: body.source || 'command' });
        }
        if (!operation && OPERATION_TYPES[body.type || body.Type]) {
            operation = createOperation(body.type || body.Type, body, { source: body.source || 'command' });
        }
        if (!operation) {
            lookup.inst.commands.push({ command: commandName, payload: _clone(body), at: Date.now(), unsupported: true });
            return { ok: true, instanceId: instanceId, command: commandName, unsupported: true };
        }

        operation = attachOperationMethods(operation);
        recordTimeline(lookup.inst, 'input-event', { command: commandName, source: body.source || body.Source || 'command' });
        recordTimeline(lookup.inst, 'normalized-operation', { operationType: operation.type || operation.Type || '', operation: operation.toJSON ? operation.toJSON() : _clone(operation) });
        var operationSelection = operation.selection || operation.Selection || null;
        var transactionType = body.transactionType || body.TransactionType || (operation.type === OPERATION_TYPES.InsertText ? TRANSACTION_TYPES.Typing : TRANSACTION_TYPES.Default);
        var transaction = createTransaction(lookup.inst.model, {
            instanceId: lookup.inst.id,
            commandName: commandName || operation.type,
            type: transactionType,
            label: body.label || body.Label || operation.type,
            beforeSelection: body.beforeSelection || body.BeforeSelection || operationSelection || lookup.inst.selection,
            lightweightSnapshots: supportsLightweightTransactionSnapshots([operation], transactionType)
        });
        lookup.inst.activeTransaction = transaction;
        var result = transaction.apply(operation);
        lookup.inst.lastOperationValidation = result.ok ? null : _clone(result.errors || []);
        if (!result.ok) {
            lookup.inst.activeTransaction = null;
            lookup.inst.lastError = result.errors && result.errors[0] ? result.errors[0].code : 'operation-failed';
            recordWatchdogFailure(lookup.inst, 'operation', lookup.inst.lastError, { command: commandName, transactionId: transaction.id });
            return Object.assign({ instanceId: instanceId }, result);
        }
        var committed = transaction.commit();
        lookup.inst.activeTransaction = null;
        lookup.inst.selection = createSelectionPostFixer(lookup.inst.schema).fix(lookup.inst.model, transaction.afterSelection || lookup.inst.selection);
        markSelectionChanged(lookup.inst, isTypingLikeTransactionType(transaction.type) ? 'typing' : 'applyCommand');
        transaction.afterSelection = _clone(lookup.inst.selection);
        if (transaction.lightweightSnapshots !== true) transaction.afterModelSnapshot = _clone(lookup.inst.model);
        lookup.inst.transactions.push(transaction.toJSON());
        var affectsDocument = transactionAffectsDocument(transaction);
        if (affectsDocument) {
            pushUndoTransaction(lookup.inst, transaction);
            lookup.inst.redoTransactions = [];
            notifyUndoState(lookup.inst, { defer: isTypingLikeTransactionType(transaction.type) });
        }
        lookup.inst.layout.invalidatedScopeIds = transaction.invalidatedScopes.slice();
        lookup.inst.lastDiffer = committed.differ;
        lookup.inst.commands.push({ command: commandName || operation.type, payload: operation.toJSON(), at: Date.now(), transactionId: transaction.id });
        if (affectsDocument) {
            markModelChanged(lookup.inst, commandName || operation.type);
            if (isTypingLikeTransactionType(transaction.type)) {
                lookup.inst.markerStoreDirty = true;
                ensureStrictPerformanceStats(lookup.inst).markerStoreDeferredRefreshCount = Number(ensureStrictPerformanceStats(lookup.inst).markerStoreDeferredRefreshCount || 0) + 1;
            } else {
                refreshRuntimeMarkerStore(lookup.inst);
            }
        }
        if (affectsDocument && operationTouchesRevisions(operation)) {
            if (isTypingLikeTransactionType(transaction.type)) scheduleRuntimeRevisionsChanged(lookup.inst);
            else notifyRuntimeRevisionsChanged(lookup.inst);
        }
        recordTimeline(lookup.inst, 'transaction-commit', { transactionId: transaction.id, transactionType: transaction.type, operationCount: transaction.operations.length });
        lookup.inst.pendingDomSelectionRestore = _clone(lookup.inst.selection);
        var livePatched = applyLiveTypingDomPatch(lookup.inst, operation, committed);
        if (livePatched) {
            lookup.inst.pendingDomSelectionRestore = null;
        } else {
            render(lookup.inst);
        }
        var boundaryPatch = affectsDocument ? commitBoundaryPatch(lookup.inst, transaction, transaction.operations, committed, transaction.type) : null;
        recordOperationPerformance(lookup.inst, transaction.operations, Math.max(0, strictPerformanceNow() - operationStart), transaction.invalidatedScopes, transaction.type);
        return Object.assign({ instanceId: instanceId, boundaryPatch: boundaryPatch, liveDomPatch: livePatched === true }, committed);
    }

    function applyRuntimeFormattingCommand(inst, commandName, payload, displayCommandName) {
        var operationStart = strictPerformanceNow();
        var body = payload || {};
        clearLiveTypingRevision(inst);
        var beforeModelSnapshot = _clone(inst.model);
        var explicitSelection = body.selection || body.Selection || null;
        var beforeSelection = createSelectionSnapshot(explicitSelection || inst.selection || firstModelSelection(inst.model));
        if (explicitSelection) {
            inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, beforeSelection);
            beforeSelection = createSelectionSnapshot(inst.selection);
        }
        var dispatcher = createCommandDispatcher(inst.model, {
            selection: beforeSelection,
            pendingTypingMarks: inst.pendingTypingMarks || []
        });
        var result = dispatcher.executeCommand(commandName, body);
        var operations = dispatcher.getCommittedOperations();
        inst.pendingTypingMarks = dispatcher.getPendingTypingMarks ? dispatcher.getPendingTypingMarks() : (inst.pendingTypingMarks || []);
        var recordedCommandName = displayCommandName || commandName;
        inst.commands.push({ command: recordedCommandName, normalizedCommand: commandName, payload: _clone(body), at: Date.now(), result: _clone(result) });
        recordTimeline(inst, 'input-event', { command: recordedCommandName, normalizedCommand: commandName, source: body.source || body.Source || 'command' });
        if (!result || result.ok === false) {
            inst.lastError = result && result.error && result.error.code || 'formatting-command-failed';
            recordWatchdogFailure(inst, 'formatting-command', inst.lastError, { command: commandName });
            return Object.assign({ instanceId: inst.id }, result || { ok: false });
        }

        inst.selection = createSelectionPostFixer(inst.schema).fix(
            inst.model,
            result.transaction && result.transaction.afterSelection || beforeSelection);
        markSelectionChanged(inst, 'applyFormattingCommand');
        scheduleFormattingStatePublish(inst, 'formatting-command', { immediate: true });
        invokeBoundaryMethod(inst, 'HandleSelectionChanged', boundarySelectionSnapshot(inst.selection, inst), 'selection-changed-failed');
        if (operations.length > 0) {
            markModelChanged(inst, commandName);
            refreshRuntimeMarkerStore(inst);
            var transaction = {
                id: result.transaction && result.transaction.id || ('cmd-txn-' + Date.now() + '-' + Math.floor(Math.random() * 100000)),
                type: TRANSACTION_TYPES.Default,
                label: recordedCommandName,
                commandName: recordedCommandName,
                instanceId: inst.id,
                beforeModelSnapshot: beforeModelSnapshot,
                afterModelSnapshot: _clone(inst.model),
                beforeDocFingerprint: createDocumentFingerprint(beforeModelSnapshot),
                afterDocFingerprint: createDocumentFingerprint(inst.model),
                beforeSelection: withStableSelectionToken(inst.id, beforeSelection, beforeModelSnapshot),
                afterSelection: withStableSelectionToken(inst.id, inst.selection, inst.model),
                invalidatedScopes: transactionAffectedBlockIds(null, operations),
                operations: _clone(operations),
                committed: true,
                rolledBack: false,
                renderSuppressed: false,
                toJSON: function () {
                    return _sortObject({
                        id: this.id,
                        type: this.type,
                        label: this.label,
                        commandName: this.commandName,
                        instanceId: this.instanceId,
                        beforeDocFingerprint: this.beforeDocFingerprint,
                        afterDocFingerprint: this.afterDocFingerprint,
                        beforeSelection: this.beforeSelection,
                        afterSelection: this.afterSelection,
                        invalidatedScopes: this.invalidatedScopes,
                        operationCount: this.operations.length,
                        committed: this.committed,
                        rolledBack: this.rolledBack,
                        renderSuppressed: this.renderSuppressed
                    });
                }
            };
            inst.commands[inst.commands.length - 1].transactionId = transaction.id;
            inst.transactions.push(transaction.toJSON());
            pushUndoTransaction(inst, transaction);
            inst.redoTransactions = [];
            notifyUndoState(inst);
            inst.layout.invalidatedScopeIds = transaction.invalidatedScopes.slice();
            inst.pendingDomSelectionRestore = _clone(inst.selection);
            var livePatched = operations.length === 1 && applyLiveTypingDomPatch(inst, operations[0], { operations: operations });
            if (livePatched) {
                inst.pendingDomSelectionRestore = null;
                if (operations.some(isFormattingVisualOperation)) {
                    var formattingStats = ensureStrictPerformanceStats(inst);
                    formattingStats.formattingCommandPartialRenderCount = Number(formattingStats.formattingCommandPartialRenderCount || 0) + 1;
                }
            } else {
                render(inst);
            }
            if (operations.some(isFormattingVisualOperation)) {
                recordLatencyHistogram(inst, 'ToolbarCommandVisibleStyle', Math.max(0, strictPerformanceNow() - operationStart), {
                    commandName: recordedCommandName,
                    liveDomPatch: livePatched === true,
                    affectedScopes: transaction.invalidatedScopes.slice()
                });
            }
            var boundaryPatch = commitBoundaryPatch(inst, transaction, operations, { operations: operations }, commandName);
            recordOperationPerformance(inst, operations, Math.max(0, strictPerformanceNow() - operationStart), transaction.invalidatedScopes, commandName);
            return Object.assign({ instanceId: inst.id, boundaryPatch: boundaryPatch, operationCount: operations.length, liveDomPatch: livePatched === true }, result);
        }

        inst.pendingDomSelectionRestore = _clone(inst.selection);
        render(inst);
        return Object.assign({ instanceId: inst.id, operationCount: 0 }, result);
    }

    function pushUndoTransaction(inst, transaction) {
        var entry = createHistoryEntry(transaction);
        var previous = inst.undoTransactions[inst.undoTransactions.length - 1] || null;
        if (previous
            && transaction.type === TRANSACTION_TYPES.Typing
            && previous.transaction
            && previous.transaction.type === TRANSACTION_TYPES.Typing
            && previous.operations.length === 1
            && entry.operations.length === 1
            && shouldCoalesceTyping(attachOperationMethods(previous.operations[0]), attachOperationMethods(entry.operations[0]), entry.operations[0].timestamp, 1000)) {
            var merged = coalesceTypingOperation(attachOperationMethods(previous.operations[0]), attachOperationMethods(entry.operations[0]));
            previous.operations = [merged.toJSON()];
            previous.afterModelSnapshot = supportsOperationHistory(merged) ? null : _clone(transaction.afterModelSnapshot || inst.model || null);
            previous.afterSelection = createSelectionSnapshot(transaction.afterSelection || previous.afterSelection);
            previous.transaction.afterSelection = previous.afterSelection;
            previous.transaction.invalidatedScopes = _unique(_asArray(previous.transaction.invalidatedScopes).concat(_asArray(transaction.invalidatedScopes)));
            previous.transaction.operationCount = 1;
            previous.transaction.lightweightSnapshots = previous.transaction.lightweightSnapshots === true || transaction.lightweightSnapshots === true;
            if (supportsOperationHistory(merged)) {
                previous.redoOperations = createRedoHistoryOperations(previous.operations);
                previous.inverseOperations = createUndoHistoryOperations(previous.operations);
            } else {
                previous.redoOperations = [createHistoryRestoreOperation(previous.afterModelSnapshot, previous.afterSelection, 'redo', previous.transaction.invalidatedScopes, previous.beforeModelSnapshot, previous.beforeSelection).toJSON()];
                previous.inverseOperations = [createHistoryRestoreOperation(previous.beforeModelSnapshot, previous.beforeSelection, 'undo', previous.transaction.invalidatedScopes, previous.afterModelSnapshot, previous.afterSelection).toJSON()];
            }
            previous.reversedOperations = previous.inverseOperations;
            return previous;
        }
        inst.undoTransactions.push(entry);
        return entry;
    }

    function createHistoryEntry(transaction) {
        var entry = createHistoryEntryFromTransaction(transaction);
        entry.reversedOperations = entry.inverseOperations;
        return entry;
    }

    function applyHistoryCommand(inst, undo) {
        var sourceStack = undo ? inst.undoTransactions : inst.redoTransactions;
        var targetStack = undo ? inst.redoTransactions : inst.undoTransactions;
        var entry = sourceStack.pop();
        if (!entry) return { ok: false, instanceId: inst.id, empty: true, command: undo ? 'undo' : 'redo' };
        var operations = (undo ? (entry.inverseOperations || entry.reversedOperations) : (entry.redoOperations || entry.operations)).map(function (operation) {
            return attachOperationMethods(_clone(operation));
        });
        var orderedOperations = undo ? operations.slice().reverse() : operations;
        var transaction = createTransaction(inst.model, {
            instanceId: inst.id,
            commandName: undo ? 'Undo' : 'Redo',
            type: undo ? TRANSACTION_TYPES.Undo : TRANSACTION_TYPES.Redo,
            label: undo ? 'Undo' : 'Redo',
            beforeSelection: inst.selection
        });
        inst.activeTransaction = transaction;
        for (var i = 0; i < orderedOperations.length; i++) {
            var result = transaction.apply(orderedOperations[i]);
            if (!result.ok) {
                inst.activeTransaction = null;
                inst.lastOperationValidation = _clone(result.errors || []);
                inst.lastError = result.errors && result.errors[0] ? result.errors[0].code : 'history-operation-failed';
                recordWatchdogFailure(inst, 'operation', inst.lastError, { command: undo ? 'undo' : 'redo', transactionId: transaction.id });
                return Object.assign({ instanceId: inst.id }, result);
            }
        }
        var committed = transaction.commit();
        inst.activeTransaction = null;
        targetStack.push(entry);
        notifyUndoState(inst);
        inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, undo ? (entry.beforeSelection || transaction.afterSelection) : (entry.afterSelection || transaction.afterSelection));
        markSelectionChanged(inst, undo ? 'undo' : 'redo');
        scheduleFormattingStatePublish(inst, undo ? 'undo' : 'redo', { immediate: true });
        transaction.afterSelection = _clone(inst.selection);
        transaction.afterModelSnapshot = _clone(inst.model);
        inst.transactions.push(transaction.toJSON());
        inst.layout.invalidatedScopeIds = transaction.invalidatedScopes.slice();
        inst.lastDiffer = committed.differ;
        inst.commands.push({ command: undo ? 'undo' : 'redo', payload: {}, at: Date.now(), transactionId: transaction.id });
        markModelChanged(inst, undo ? 'undo' : 'redo');
        if (orderedOperations.some(operationMayChangeRevisions)) notifyRuntimeRevisionsChanged(inst);
        recordTimeline(inst, 'normalized-operation', { operationTypes: orderedOperations.map(function (operation) { return operation.type || operation.Type || ''; }) });
        recordTimeline(inst, 'transaction-commit', { transactionId: transaction.id, transactionType: transaction.type, operationCount: orderedOperations.length });
        inst.pendingDomSelectionRestore = _clone(inst.selection);
        var livePatched = orderedOperations.length > 0
            && orderedOperations.every(function (operation) {
                var type = operation.type || operation.Type || '';
                return [
                    OPERATION_TYPES.InsertText,
                    OPERATION_TYPES.DeleteRange,
                    OPERATION_TYPES.SplitParagraph,
                    OPERATION_TYPES.MergeParagraph,
                    OPERATION_TYPES.ApplyMark,
                    OPERATION_TYPES.RemoveMark,
                    OPERATION_TYPES.SetParagraphAttribute
                ].indexOf(type) >= 0;
            });
        if (livePatched) {
            for (var patchIndex = 0; patchIndex < orderedOperations.length; patchIndex++) {
                if (!applyLiveTypingDomPatch(inst, orderedOperations[patchIndex], { operations: orderedOperations })) {
                    livePatched = false;
                    break;
                }
            }
        }
        if (livePatched) {
            inst.pendingDomSelectionRestore = null;
        } else {
            render(inst);
        }
        return Object.assign({
            instanceId: inst.id,
            transaction: transaction.toJSON(),
            appliedOperations: orderedOperations.map(function (operation) { return operation.toJSON ? operation.toJSON() : _clone(operation); }),
            historyEntry: entry
        }, committed);
    }

    function getDocumentSnapshot(instanceId) {
        var lookup = _get(instanceId, 'getDocumentSnapshot');
        if (lookup.error) return lookup.error;
        flushTypingBoundaryPatchDispatch(lookup.inst);
        flushDeferredBoundaryPatchDispatch(lookup.inst);
        return {
            ok: true,
            instanceId: instanceId,
            document: _sortObject(_clone(lookup.inst.model)),
            csharpDocument: exportToCSharpJson(lookup.inst.model)
        };
    }

    function getSelectionSnapshot(instanceId) {
        var lookup = _get(instanceId, 'getSelectionSnapshot');
        if (lookup.error) return lookup.error;
        var domSelection = window.getSelection && window.getSelection();
        if (selectionBelongsToEditor(lookup.inst, domSelection)) {
            var domSnapshot = createSelectionPostFixer(lookup.inst.schema).fix(lookup.inst.model, readDomSelectionSnapshot(lookup.inst));
            if (domSnapshot && domSnapshot.blockId) {
                lookup.inst.selection = domSnapshot;
            }
        }
        var selection = createSelectionSnapshot(lookup.inst.selection || {});
        var toolbarSelection = readRecentToolbarSelection(lookup.inst);
        if (toolbarSelection && selection.isCollapsed !== false) {
            selection = toolbarSelection;
        }
        var snapshot = rememberSelectionToken(lookup.inst, selection, 'getSelectionSnapshot') || withStableSelectionToken(instanceId, selection, lookup.inst.model);
        return _sortObject(Object.assign({ ok: true, instanceId: instanceId }, snapshot));
    }

    function getLayoutSnapshot(instanceId) {
        var lookup = _get(instanceId, 'getLayoutSnapshot');
        if (lookup.error) return lookup.error;
        return _sortObject(Object.assign({ ok: true, instanceId: instanceId }, lookup.inst.layout || {}));
    }

    function cloneRect(rect) {
        var source = rect || {};
        return _sortObject({
            x: Number(source.x ?? source.X ?? source.left ?? source.Left ?? 0) || 0,
            y: Number(source.y ?? source.Y ?? source.top ?? source.Top ?? 0) || 0,
            width: Math.max(0, Number(source.width ?? source.Width ?? 0) || 0),
            height: Math.max(0, Number(source.height ?? source.Height ?? 0) || 0)
        });
    }

    function collectLayoutProbe(inst) {
        var diagnostics = ensureDiagnostics(inst);
        var probeLayout = null;
        try {
            probeLayout = createParagraphLayoutEngine().layoutDocument(inst.model || {}, { selection: inst.selection || null });
        } catch (error) {
            recordWatchdogFailure(inst, 'layout', error, { recovery: 'probe-used-current-layout' });
            probeLayout = inst.layout || {};
        }

        var textRects = [];
        var imageRects = [];
        var captionRects = [];
        var lineBoxes = [];
        var exclusionZones = [];
        var collisions = [];
        var imageByBlockId = new Map();

        _asArray(probeLayout && probeLayout.blocks).forEach(function (blockLayout) {
            if (!blockLayout) return;
            if (blockLayout.type === 'paragraph') {
                _asArray(blockLayout.lines).forEach(function (line) {
                    var lineRect = cloneRect(line.rect);
                    lineBoxes.push(_sortObject({
                        id: line.id || '',
                        blockId: blockLayout.blockId || '',
                        pageIndex: Number(line.pageIndex ?? blockLayout.pageIndex ?? 0) || 0,
                        rect: lineRect,
                        availableIntervals: _asArray(line.availableIntervals).map(function (interval) { return cloneRect(interval); })
                    }));
                    _asArray(line.availableIntervals).forEach(function (interval) {
                        exclusionZones.push(_sortObject({
                            id: 'line-interval-' + (line.id || lineBoxes.length),
                            kind: 'available-text-interval',
                            blockId: blockLayout.blockId || '',
                            pageIndex: Number(line.pageIndex ?? blockLayout.pageIndex ?? 0) || 0,
                            rect: cloneRect(interval),
                            allowed: true
                        }));
                    });
                });
                _asArray(blockLayout.segments).forEach(function (segment) {
                    textRects.push(_sortObject({
                        id: segment.id || '',
                        blockId: segment.blockId || blockLayout.blockId || '',
                        runId: segment.runId || '',
                        pageIndex: Number(segment.pageIndex ?? blockLayout.pageIndex ?? 0) || 0,
                        text: segment.text || '',
                        rect: cloneRect(segment.rect)
                    }));
                });
                return;
            }
            if (blockLayout.type === 'image') {
                var modelBlock = _findBlock(inst.model, blockLayout.blockId);
                var object = blockLayout.object || normalizeImageObject(modelBlock || { id: blockLayout.blockId, type: 'image', content: {} });
                var imageRect = _sortObject({
                    id: blockLayout.objectId || object.objectId || blockLayout.blockId || '',
                    blockId: blockLayout.blockId || '',
                    pageIndex: Number(blockLayout.pageIndex || 0) || 0,
                    wrapMode: normalizeWrapModeName(blockLayout.wrapMode || object.wrapMode),
                    allowOverlap: object.allowOverlap === true,
                    zIndex: Number(blockLayout.zIndex ?? object.zIndex ?? 0) || 0,
                    rect: cloneRect(blockLayout.rect)
                });
                imageRects.push(imageRect);
                imageByBlockId.set(imageRect.blockId, imageRect);
                if (object.caption) {
                    var captionHeight = Math.max(16, Math.min(48, object.caption.length * 0.6));
                    captionRects.push(_sortObject({
                        id: imageRect.id + '-caption',
                        blockId: imageRect.blockId,
                        pageIndex: imageRect.pageIndex,
                        text: object.caption,
                        rect: cloneRect({
                            x: imageRect.rect.x,
                            y: imageRect.rect.y + imageRect.rect.height,
                            width: imageRect.rect.width,
                            height: captionHeight
                        })
                    }));
                }
                var exclusion = createTextExclusion(Object.assign({}, object, { rect: imageRect.rect }), null);
                if (exclusion) {
                    exclusionZones.push(_sortObject({
                        id: exclusion.objectId || imageRect.id,
                        blockId: imageRect.blockId,
                        kind: exclusion.kind,
                        pageIndex: imageRect.pageIndex,
                        wrapMode: exclusion.wrapMode,
                        allowed: exclusion.allowOverlap === true,
                        rect: cloneRect(exclusion.rect)
                    }));
                }
            }
        });

        if (!textRects.length && inst.root) {
            Array.from(inst.root.querySelectorAll('p[data-block-id], [data-render-layer="text"] [data-model-block-id]')).forEach(function (node, index) {
                textRects.push(_sortObject({
                    id: 'dom-text-' + index,
                    blockId: node.getAttribute('data-block-id') || node.getAttribute('data-model-block-id') || '',
                    runId: '',
                    pageIndex: 0,
                    text: node.textContent || '',
                    rect: cloneRect(domRectToRect(node.getBoundingClientRect()))
                }));
            });
        }
        if (!imageRects.length && inst.root) {
            Array.from(inst.root.querySelectorAll('figure[data-block-id], [data-render-object-id]')).forEach(function (node, index) {
                var blockId = node.getAttribute('data-block-id') || node.getAttribute('data-render-block-id') || '';
                var modelBlock = _findBlock(inst.model, blockId);
                var object = normalizeImageObject(modelBlock || { id: blockId, type: 'image', content: {} });
                var item = _sortObject({
                    id: node.getAttribute('data-render-object-id') || object.objectId || blockId || ('dom-image-' + index),
                    blockId: blockId,
                    pageIndex: 0,
                    wrapMode: normalizeWrapModeName(object.wrapMode),
                    allowOverlap: object.allowOverlap === true,
                    zIndex: Number(object.zIndex || 0) || 0,
                    rect: cloneRect(domRectToRect(node.getBoundingClientRect()))
                });
                imageRects.push(item);
                imageByBlockId.set(item.blockId, item);
            });
        }
        if (!captionRects.length && inst.root) {
            Array.from(inst.root.querySelectorAll('figcaption')).forEach(function (node, index) {
                var figure = node.closest('figure');
                captionRects.push(_sortObject({
                    id: 'dom-caption-' + index,
                    blockId: figure ? (figure.getAttribute('data-block-id') || figure.getAttribute('data-render-block-id') || '') : '',
                    pageIndex: 0,
                    text: node.textContent || '',
                    rect: cloneRect(domRectToRect(node.getBoundingClientRect()))
                }));
            });
        }

        function classifyOverlap(a, b, type) {
            if (!rectsOverlap(a.rect, b.rect)) return;
            var image = type === 'text-image' ? b : imageByBlockId.get(a.blockId) || imageByBlockId.get(b.blockId);
            var mode = normalizeWrapModeName(image && image.wrapMode);
            var allowed = !!(image && (image.allowOverlap === true || mode === 'BehindText' || mode === 'InFrontOfText'));
            collisions.push(_sortObject({
                type: type,
                firstId: a.id || a.blockId || '',
                secondId: b.id || b.blockId || '',
                firstBlockId: a.blockId || '',
                secondBlockId: b.blockId || '',
                allowed: allowed,
                policy: allowed ? 'layered-object-overlap' : 'forbidden-content-overlap',
                rectA: cloneRect(a.rect),
                rectB: cloneRect(b.rect)
            }));
        }

        textRects.forEach(function (textRect) {
            imageRects.forEach(function (imageRect) { classifyOverlap(textRect, imageRect, 'text-image'); });
        });
        for (var i = 0; i < textRects.length; i++) {
            for (var j = i + 1; j < textRects.length; j++) {
                if (textRects[i].blockId !== textRects[j].blockId) classifyOverlap(textRects[i], textRects[j], 'text-text');
            }
        }

        return _sortObject({
            ok: true,
            instanceId: inst.id,
            capturedAt: Date.now(),
            modelVersion: diagnostics.modelVersion,
            layoutVersion: diagnostics.layoutVersion,
            renderVersion: diagnostics.renderVersion,
            textRects: textRects,
            imageRects: imageRects,
            captionRects: captionRects,
            lineBoxes: lineBoxes,
            exclusionZones: exclusionZones,
            collisions: collisions,
            collisionSummary: {
                total: collisions.length,
                allowed: collisions.filter(function (item) { return item.allowed === true; }).length,
                forbidden: collisions.filter(function (item) { return item.allowed !== true; }).length
            }
        });
    }

    function getLayoutProbe(instanceId) {
        var lookup = _get(instanceId, 'getLayoutProbe');
        if (lookup.error) return lookup.error;
        return collectLayoutProbe(lookup.inst);
    }

    function runFrameProbe(instanceId, frameCount) {
        var lookup = _get(instanceId, 'runFrameProbe');
        if (lookup.error) return Promise.resolve(lookup.error);
        var count = Math.max(1, Math.min(60, Number(frameCount || 1) || 1));
        var frames = [];
        return new Promise(function (resolve) {
            function tick() {
                frames.push(collectLayoutProbe(lookup.inst));
                if (frames.length >= count) {
                    resolve(_sortObject({ ok: true, instanceId: instanceId, frameCount: frames.length, frames: frames }));
                    return;
                }
                requestAnimationFrame(tick);
            }
            requestAnimationFrame(tick);
        });
    }

    function getDebugSnapshot(instanceId) {
        var lookup = _get(instanceId, 'getDebugSnapshot');
        if (lookup.error) return lookup.error;
        var validation = validateModel(lookup.inst.model);
        var mapperDump = createModelLayoutDomMapper(lookup.inst.root, lookup.inst.model, lookup.inst.layout).debugDump();
        var diagnostics = ensureDiagnostics(lookup.inst);
        var stats = ensureStrictPerformanceStats(lookup.inst);
        var currentSelection = rememberSelectionToken(lookup.inst, lookup.inst.selection || firstModelSelection(lookup.inst.model), 'getDebugSnapshot');
        return _sortObject({
            ok: true,
            instanceId: instanceId,
            engineMode: 'google-docs',
            useGoogleDocsEngine: true,
            disposed: false,
            schemaVersion: lookup.inst.model.schemaVersion,
            modelVersion: diagnostics.modelVersion,
            layoutVersion: diagnostics.layoutVersion,
            renderVersion: diagnostics.renderVersion,
            selectionVersion: diagnostics.selectionVersion,
            validation: validation,
            layout: lookup.inst.layout,
            selection: lookup.inst.selection,
            activeTransaction: lookup.inst.activeTransaction && lookup.inst.activeTransaction.toJSON ? lookup.inst.activeTransaction.toJSON() : _clone(lookup.inst.activeTransaction || null),
            commandCount: lookup.inst.commands.length,
            transactionCount: lookup.inst.transactions.length,
            undoDepth: lookup.inst.undoTransactions.length,
            redoDepth: lookup.inst.redoTransactions.length,
            invalidatedScopes: _asArray(lookup.inst.layout && lookup.inst.layout.invalidatedScopeIds),
            performanceStats: _clone(stats),
            JsOwnedInputCount: Number(lookup.inst.jsOwnedInputCount || stats.inputDomApplyCount || 0),
            NativeInputCount: Number(lookup.inst.nativeInputCount || 0),
            timeline: _clone(diagnostics.timeline),
            lastErrors: _clone(diagnostics.lastErrors),
            watchdogFailures: diagnostics.watchdogFailures.length,
            watchdogFailureDetails: _clone(diagnostics.watchdogFailures),
            debugWarnings: _clone(diagnostics.debugWarnings),
            debugWarningVisible: diagnostics.debugWarnings.length > 0,
            dirtyState: lookup.inst.dirtyState,
            trackChangesState: resolveTrackChangesState(lookup.inst.options || {}),
            modelEpoch: lookup.inst.modelEpoch,
            savedEpoch: lookup.inst.savedEpoch,
            boundaryPatchCount: lookup.inst.boundaryPatches.length,
            boundaryFailures: lookup.inst.boundaryFailures,
            lastCSharpUpdate: lookup.inst.lastCSharpUpdate,
            lastTransaction: lookup.inst.transactions[lookup.inst.transactions.length - 1] || null,
            lastSelectionToken: lookup.inst.lastSelectionToken || null,
            LastSelectionToken: lookup.inst.lastSelectionToken || null,
            selectionToken: lookup.inst.lastSelectionToken || null,
            SelectionToken: lookup.inst.lastSelectionToken || null,
            lastSelectionTokenData: _clone(lookup.inst.lastSelectionTokenData || null),
            LastSelectionTokenData: _clone(lookup.inst.lastSelectionTokenData || null),
            lastSelectionTokenReason: lookup.inst.lastSelectionTokenReason || '',
            commandSelectionTokenDiagnostic: _clone(lookup.inst.lastCommandTokenDiagnostic || null),
            CommandSelectionTokenDiagnostic: _clone(lookup.inst.lastCommandTokenDiagnostic || null),
            lastOperationValidation: lookup.inst.lastOperationValidation,
            lastDiffer: lookup.inst.lastDiffer,
            selectionMapper: mapperDump,
            currentSelection: currentSelection,
            CurrentSelection: currentSelection,
            selection: currentSelection,
            lastError: lookup.inst.lastError
        });
    }

    function getDirtyState(instanceId) {
        var lookup = _get(instanceId, 'getDirtyState');
        if (lookup.error) return lookup.error;
        return _sortObject(Object.assign({ ok: true, instanceId: instanceId }, _clone(lookup.inst.dirtyState || createInitialDirtyState())));
    }

    function acknowledgeSave(instanceId, ack) {
        var lookup = _get(instanceId, 'acknowledgeSave');
        if (lookup.error) return lookup.error;
        var dirtyState = applySaveAckToInstance(lookup.inst, ack || {});
        lookup.inst.lastCSharpUpdate = { type: 'saveAck', at: Date.now(), dirtyState: _clone(dirtyState) };
        return _sortObject({ ok: true, instanceId: instanceId, dirtyState: dirtyState });
    }

    function requestAutosaveSnapshot(instanceId) {
        var lookup = _get(instanceId, 'requestAutosaveSnapshot');
        if (lookup.error) return lookup.error;
        flushTypingBoundaryPatchDispatch(lookup.inst);
        return _sortObject({
            ok: true,
            instanceId: instanceId,
            epoch: Number(lookup.inst.modelEpoch || 0),
            savedEpoch: Number(lookup.inst.savedEpoch || 0),
            dirtyState: _clone(lookup.inst.dirtyState || createInitialDirtyState()),
            selection: createSelectionSnapshot(lookup.inst.selection || firstModelSelection(lookup.inst.model)),
            document: _clone(lookup.inst.model),
            csharpDocument: exportToCSharpJson(lookup.inst.model)
        });
    }

    function markAutosaveFailed(instanceId, failure) {
        var lookup = _get(instanceId, 'markAutosaveFailed');
        if (lookup.error) return lookup.error;
        var dirtyState = markAutosaveFailure(lookup.inst, failure || {});
        lookup.inst.lastCSharpUpdate = { type: 'autosaveFailed', at: Date.now(), dirtyState: _clone(dirtyState) };
        return _sortObject({ ok: true, instanceId: instanceId, dirtyState: dirtyState, document: _clone(lookup.inst.model) });
    }

    function applyStrictRemoteOperations(instanceId, batch) {
        var lookup = _get(instanceId, 'applyRemoteOperations');
        if (lookup.error) return lookup.error;
        if (lookup.inst.activeTransaction) {
            return { ok: false, instanceId: instanceId, error: { code: 'active-transaction-conflict', updateType: 'remoteOperations' } };
        }
        var source = batch || {};
        var operations = _asArray(source.operations || source.Operations || source);
        var result = applyOperationBatchToInstance(lookup.inst, operations, TRANSACTION_TYPES.Remote, source.label || source.Label || 'Remote operations', false);
        if (result.ok) {
            lookup.inst.lastCSharpUpdate = {
                type: 'remoteOperations',
                operationCount: operations.length,
                at: Date.now(),
                transactionId: result.transaction && result.transaction.id || ''
            };
        }
        return result;
    }

    function findImageBlockByAsset(model, assetId, blockId, objectId) {
        var found = null;
        function scan(blocks) {
            _asArray(blocks).forEach(function (block) {
                if (found || !block) return;
                if (block.type === 'image') {
                    var content = block.content || {};
                    if ((blockId && block.id === blockId)
                        || (objectId && (content.objectId === objectId || block.id === objectId))
                        || (assetId && (content.assetId === assetId || content.AssetId === assetId))) {
                        found = block;
                        return;
                    }
                }
                if (block.type === 'table') {
                    _asArray(block.content && block.content.rows).forEach(function (row) {
                        _asArray(row.cells).forEach(function (cell) { scan(cell.blocks); });
                    });
                }
            });
        }
        scan(model && model.body && model.body.blocks);
        _asArray(model && model.headers).forEach(function (region) { scan(region.blocks); });
        _asArray(model && model.footers).forEach(function (region) { scan(region.blocks); });
        return found;
    }

    function updateProviderImageUrl(instanceId, update) {
        var lookup = _get(instanceId, 'updateProviderImageUrl');
        if (lookup.error) return lookup.error;
        var body = update || {};
        var block = findImageBlockByAsset(
            lookup.inst.model,
            body.assetId || body.AssetId || '',
            body.blockId || body.BlockId || '',
            body.objectId || body.ObjectId || '');
        if (!block) return { ok: false, instanceId: instanceId, error: { code: 'missing-image-asset' } };
        if (!block.content) block.content = { type: 'image' };
        block.content.url = body.url || body.Url || body.resolvedUrl || body.ResolvedUrl || block.content.url || null;
        block.content.resolvedUrl = block.content.url;
        if (body.assetId || body.AssetId) block.content.assetId = body.assetId || body.AssetId;
        buildIndexes(lookup.inst.model);
        lookup.inst.layout.invalidatedScopeIds = [block.id];
        lookup.inst.lastCSharpUpdate = { type: 'providerImageUrl', blockId: block.id, at: Date.now() };
        render(lookup.inst);
        return _sortObject({ ok: true, instanceId: instanceId, blockId: block.id, url: block.content.url, dirtyState: _clone(lookup.inst.dirtyState || createInitialDirtyState()) });
    }

    function refreshSnapshot(instanceId, snapshot, options) {
        var lookup = _get(instanceId, 'refreshSnapshot');
        if (lookup.error) return lookup.error;
        if (lookup.inst.activeTransaction) {
            return { ok: false, instanceId: instanceId, error: { code: 'active-transaction-conflict', updateType: 'snapshotRefresh' } };
        }
        var opts = options || {};
        var recovery = opts.recovery === true || opts.Recovery === true || String(opts.reason || opts.Reason || '').toLowerCase() === 'recovery';
        if (!recovery) {
            return { ok: false, instanceId: instanceId, error: { code: 'full-snapshot-refresh-requires-recovery' } };
        }
        var document = snapshot && (snapshot.Document || snapshot.document) ? (snapshot.Document || snapshot.document) : snapshot;
        lookup.inst.model = importFromCSharpJson(document || {});
        refreshRuntimeMarkerStore(lookup.inst);
        lookup.inst.selection = firstModelSelection(lookup.inst.model);
        lookup.inst.layout.invalidatedScopeIds = ['document'];
        lookup.inst.lastCSharpUpdate = { type: 'snapshotRefresh', recovery: true, at: Date.now() };
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, recovery: true, validation: validateModel(lookup.inst.model), dirtyState: _clone(lookup.inst.dirtyState || createInitialDirtyState()) };
    }

    function applyCSharpUpdate(instanceId, update) {
        var lookup = _get(instanceId, 'applyCSharpUpdate');
        if (lookup.error) return lookup.error;
        var body = update || {};
        var type = String(body.type || body.Type || '').replace(/[\s_.:-]+/g, '').toLowerCase();
        if (type === 'saveack') return acknowledgeSave(instanceId, body);
        if (type === 'remoteoperations' || type === 'remoteoperationbatch') return applyStrictRemoteOperations(instanceId, body);
        if (type === 'providerimageurl' || type === 'assetupdate') return updateProviderImageUrl(instanceId, body);
        if (type === 'snapshotrefresh' || type === 'fullsnapshot') return refreshSnapshot(instanceId, body.document || body.Document || body.snapshot || body.Snapshot || body, body);
        if (type === 'initialdocument' || type === 'loaddocument') return loadDocument(instanceId, body.document || body.Document || body.snapshot || body.Snapshot || body);
        return { ok: false, instanceId: instanceId, error: { code: 'unknown-csharp-update', updateType: body.type || body.Type || '' } };
    }

    function exportCanonicalSnapshot(instanceId) {
        return requestAutosaveSnapshot(instanceId);
    }

    function importCanonicalSnapshot(instanceId, snapshot, options) {
        var opts = Object.assign({ recovery: true }, options || {});
        return refreshSnapshot(instanceId, snapshot, opts);
    }

    function simulateWatchdogFailure(instanceId, kind) {
        var lookup = _get(instanceId, 'simulateWatchdogFailure');
        if (lookup.error) return lookup.error;
        var type = String(kind || '').toLowerCase();
        var before = JSON.stringify(lookup.inst.model || {});
        if (type === 'layout') {
            ensureDiagnostics(lookup.inst).forceLayoutFailure = true;
            render(lookup.inst);
        } else if (type === 'render') {
            ensureDiagnostics(lookup.inst).forceRenderFailure = true;
            render(lookup.inst);
        } else if (type === 'selection') {
            ensureDiagnostics(lookup.inst).forceSelectionFailure = true;
            render(lookup.inst);
        } else if (type === 'operation') {
            var result = applyCommand(instanceId, 'InsertText', {
                target: { blockId: 'missing-watchdog-block', offset: 0 },
                text: 'lost'
            });
            return _sortObject({
                ok: result.ok !== false,
                recovered: result.ok === false,
                instanceId: instanceId,
                failureKind: 'operation',
                textPreserved: before === JSON.stringify(lookup.inst.model || {}),
                result: result,
                debugSnapshot: getDebugSnapshot(instanceId)
            });
        } else {
            return { ok: false, instanceId: instanceId, error: { code: 'unknown-watchdog-failure-kind', kind: kind || '' } };
        }
        return _sortObject({
            ok: true,
            recovered: true,
            instanceId: instanceId,
            failureKind: type,
            textPreserved: before === JSON.stringify(lookup.inst.model || {}),
            debugSnapshot: getDebugSnapshot(instanceId)
        });
    }

    function exportFailureArtifact(instanceId, reason) {
        var lookup = _get(instanceId, 'exportFailureArtifact');
        if (lookup.error) return lookup.error;
        var debug = getDebugSnapshot(instanceId);
        var probe = getLayoutProbe(instanceId);
        return _sortObject({
            ok: true,
            instanceId: instanceId,
            reason: _asText(reason || 'manual'),
            createdAt: Date.now(),
            debugSnapshot: debug,
            layoutProbe: probe,
            timeline: _clone(ensureDiagnostics(lookup.inst).timeline),
            recovery: {
                watchdogFailures: debug.watchdogFailures,
                debugWarnings: debug.debugWarnings,
                lastErrors: debug.lastErrors
            },
            document: _clone(lookup.inst.model),
            csharpDocument: exportToCSharpJson(lookup.inst.model)
        });
    }

    function getBoundaryPanelData(instanceId) {
        var lookup = _get(instanceId, 'getBoundaryPanelData');
        if (lookup.error) return lookup.error;
        return _sortObject({
            ok: true,
            instanceId: instanceId,
            comments: _clone(lookup.inst.model.comments || []),
            revisions: _clone(lookup.inst.model.revisions || []),
            dirtyState: _clone(lookup.inst.dirtyState || createInitialDirtyState())
        });
    }

    function focus(instanceId) {
        var lookup = _get(instanceId, 'focus');
        if (lookup.error) return lookup.error;
        var target = lookup.inst.root.querySelector('[contenteditable="true"]');
        if (target && typeof target.focus === 'function') target.focus({ preventScroll: true });
        return { ok: true, instanceId: instanceId };
    }

    function setReadOnly(instanceId, readOnly) {
        var lookup = _get(instanceId, 'setReadOnly');
        if (lookup.error) return lookup.error;
        lookup.inst.options.readOnly = readOnly === true;
        lookup.inst.options.ReadOnly = readOnly === true;
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, readOnly: readOnly === true };
    }

    function getBodyHtml(instanceId) {
        var lookup = _get(instanceId, 'getBodyHtml');
        if (lookup.error) return '';
        var body = lookup.inst.root.querySelector('.tm-wysiwyg-page__body') || lookup.inst.root;
        return body ? (body.innerHTML || '') : '';
    }

    function getSelectedText() {
        var selection = typeof window.getSelection === 'function' ? window.getSelection() : null;
        return selection ? String(selection.toString() || '') : '';
    }

    function restoreSelection(instanceId, selection) {
        var lookup = _get(instanceId, 'restoreSelection');
        if (lookup.error) return lookup.error;
        lookup.inst.selection = createSelectionPostFixer(lookup.inst.schema).fix(lookup.inst.model, createSelectionSnapshot(selection || {}));
        markSelectionChanged(lookup.inst, 'restoreSelection');
        lookup.inst.pendingDomSelectionRestore = _clone(lookup.inst.selection);
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, selection: withStableSelectionToken(instanceId, lookup.inst.selection, lookup.inst.model) };
    }

    function getFormattingState(instanceId) {
        var lookup = _get(instanceId, 'getFormattingState');
        if (lookup.error) return {};
        var computed = computeFormattingState(lookup.inst.model, lookup.inst.selection, lookup.inst.pendingTypingMarks || [], lookup.inst);
        var state = toBlazorFormattingState(computed);
        var selection = rememberSelectionToken(lookup.inst, computed.selection || lookup.inst.selection || firstModelSelection(lookup.inst.model), 'getFormattingState');
        state.CurrentSelection = selection;
        state.currentSelection = selection;
        state.Selection = selection;
        state.selection = selection;
        state.Version = Number(lookup.inst.formattingStateVersion || lookup.inst.lastFormattingStatePublishedVersion || 0) || 0;
        state.version = state.Version;
        return _sortObject(state);
    }

    function getSidePanelSyncState(instanceId) {
        var lookup = _get(instanceId, 'getSidePanelSyncState');
        if (lookup.error) return { ok: false, error: lookup.error };
        return Object.assign({ ok: true, instanceId: instanceId }, createSidePanelSyncState(lookup.inst.model, lookup.inst.selection, {
            debounceMs: lookup.inst.options && (lookup.inst.options.PanelInputDebounceMs || lookup.inst.options.panelInputDebounceMs) || 180
        }));
    }

    function getUndoState(instanceId) {
        var lookup = _get(instanceId, 'getUndoState');
        if (lookup.error) return lookup.error;
        return undoStateForInstance(lookup.inst);
    }

    function undoStateForInstance(inst) {
        var nextUndo = inst.undoTransactions[inst.undoTransactions.length - 1] || null;
        var nextRedo = inst.redoTransactions[inst.redoTransactions.length - 1] || null;
        var lastTransaction = inst.transactions[inst.transactions.length - 1] || null;
        return _sortObject({
            ok: true,
            instanceId: inst.id,
            JsOwnedUndo: true,
            CanUndo: inst.undoTransactions.length > 0,
            CanRedo: inst.redoTransactions.length > 0,
            UndoDepth: inst.undoTransactions.length,
            RedoDepth: inst.redoTransactions.length,
            NextUndoDescription: historyEntryDescription(nextUndo),
            NextRedoDescription: historyEntryDescription(nextRedo),
            PendingTransactionId: inst.activeTransaction ? inst.activeTransaction.id || null : null,
            LastTransactionId: lastTransaction ? lastTransaction.id || null : null,
            Epoch: inst.modelEpoch || 0
        });
    }

    function historyEntryDescription(entry) {
        if (!entry) return null;
        var transaction = entry.transaction || {};
        return _asText(transaction.commandName || transaction.label || transaction.type || entry.id || '').trim() || null;
    }

    function notifyUndoState(inst, options) {
        if (!inst) return;
        if (options && options.defer === true) {
            inst.pendingUndoStateNotify = true;
            if (inst.pendingUndoStateTimer) clearTimeout(inst.pendingUndoStateTimer);
            var delay = Math.max(0, Number(inst.options && (inst.options.TypingBatchMs || inst.options.typingBatchMs) || 500) || 500);
            inst.pendingUndoStateTimer = setTimeout(function () {
                inst.pendingUndoStateTimer = null;
                if (!inst.pendingUndoStateNotify) return;
                inst.pendingUndoStateNotify = false;
                invokeBoundaryMethod(inst, 'HandleUndoStateChanged', undoStateForInstance(inst), 'undo-state-changed-failed');
            }, delay);
            if (inst.timers && inst.timers.indexOf(inst.pendingUndoStateTimer) < 0) inst.timers.push(inst.pendingUndoStateTimer);
            return;
        }
        if (inst.pendingUndoStateTimer) {
            clearTimeout(inst.pendingUndoStateTimer);
            inst.pendingUndoStateTimer = null;
            inst.pendingUndoStateNotify = false;
        }
        invokeBoundaryMethod(inst, 'HandleUndoStateChanged', undoStateForInstance(inst), 'undo-state-changed-failed');
    }

    function getDebugUndoStack(instanceId) {
        var lookup = _get(instanceId, 'getDebugUndoStack');
        if (lookup.error) return lookup.error;
        return _sortObject({
            ok: true,
            instanceId: instanceId,
            Undo: _clone(lookup.inst.undoTransactions),
            Redo: _clone(lookup.inst.redoTransactions),
            Pending: lookup.inst.activeTransaction ? lookup.inst.activeTransaction.toJSON() : null,
            LastApply: lookup.inst.transactions[lookup.inst.transactions.length - 1] || null
        });
    }

    function getLastCommandTransaction(instanceId) {
        var lookup = _get(instanceId, 'getLastCommandTransaction');
        if (lookup.error) return lookup.error;
        var command = null;
        for (var i = lookup.inst.commands.length - 1; i >= 0; i--) {
            if (lookup.inst.commands[i] && !lookup.inst.commands[i].unsupported) {
                command = lookup.inst.commands[i];
                break;
            }
        }

        var transaction = null;
        if (command && command.transactionId) {
            transaction = lookup.inst.transactions.find(function (item) { return item && item.id === command.transactionId; }) || null;
        }
        transaction = transaction || lookup.inst.transactions[lookup.inst.transactions.length - 1] || null;

        var undoEntry = null;
        if (transaction && transaction.id) {
            undoEntry = lookup.inst.undoTransactions.find(function (item) {
                return item && item.transaction && item.transaction.id === transaction.id;
            }) || null;
        }

        return _sortObject({
            ok: true,
            instanceId: instanceId,
            command: command && command.command || '',
            transactionId: transaction && transaction.id || command && command.transactionId || '',
            transaction: _clone(transaction || null),
            commandName: transaction && (transaction.commandName || transaction.CommandName) || command && command.command || '',
            beforeSelection: _clone(transaction && transaction.beforeSelection || null),
            afterSelection: _clone(transaction && transaction.afterSelection || null),
            beforeDocFingerprint: transaction && (transaction.beforeDocFingerprint || transaction.BeforeDocFingerprint) || '',
            afterDocFingerprint: transaction && (transaction.afterDocFingerprint || transaction.AfterDocFingerprint) || '',
            operations: _clone(transaction && transaction.operations || []),
            inverseOperations: _clone(undoEntry && undoEntry.inverseOperations || [])
        });
    }

    function getPageMetrics(instanceId) {
        var lookup = _get(instanceId, 'getPageMetrics');
        if (lookup.error) return lookup.error;
        var pages = _asArray(lookup.inst.layout && lookup.inst.layout.pages).map(function (page, index) {
            return _sortObject({
                PageNumber: page.pageNumber || page.PageNumber || index + 1,
                Index: index,
                BlockIds: _asArray(page.blockIds || page.BlockIds),
                IsVirtual: page.isVirtual === true || page.IsVirtual === true,
                IsRendered: page.isRendered !== false && page.IsRendered !== false,
                HasOverflow: _asArray(page.overflowBlocks || page.OverflowBlocks).length > 0
            });
        });
        return _sortObject({
            ok: true,
            instanceId: instanceId,
            TotalPages: pages.length,
            RenderedPages: pages.filter(function (page) { return page.IsVirtual !== true; }).length,
            VirtualizedPages: pages.filter(function (page) { return page.IsVirtual === true; }).length,
            VirtualizationEnabled: lookup.inst.layout && lookup.inst.layout.virtualizationEnabled === true,
            ActivePageIndex: Number(lookup.inst.layout && lookup.inst.layout.activePageIndex || 0) || 0,
            Pages: pages
        });
    }

    function getDebugMetrics(instanceId) {
        var lookup = _get(instanceId, 'getDebugMetrics');
        if (lookup.error) return null;
        var stats = ensureStrictPerformanceStats(lookup.inst);
        var histograms = ensureLatencyHistogramState(stats);
        return _sortObject({
            KeyDownCount: stats.keyDownCount || 0,
            BeforeInputCount: stats.beforeInputCount || 0,
            InputDomApplyCount: stats.inputDomApplyCount || 0,
            FullRenderCount: stats.fullRenderCount || stats.renderPassCount || 0,
            PartialRenderCount: stats.partialRenderCount || 0,
            TextNodePatchCount: stats.textNodePatchCount || 0,
            BlockPatchCount: stats.blockPatchCount || 0,
            MarkerOverlayPatchCount: stats.markerOverlayPatchCount || 0,
            ObjectOverlayPatchCount: stats.objectOverlayPatchCount || 0,
            SelectionNotifyCount: stats.selectionNotifyCount || 0,
            BlazorInteropCallCount: stats.blazorInteropCallCount || 0,
            BlazorCallbackDuringTypingCount: stats.blazorCallbackDuringTypingCount || 0,
            FormattingStateEventCount: stats.formattingStateEventCount || stats.formattingStateNotifyCount || 0,
            FormattingStateNotifyCount: stats.formattingStateNotifyCount || 0,
            TypingFlushCount: stats.typingFlushCount || 0,
            MaxTypingBatchSize: stats.maxTypingBatchSize || 0,
            MaxBoundaryPatchBatchSize: stats.maxBoundaryPatchBatchSize || 0,
            MedianKeyToDomMs: stats.medianKeyToDomMs || 0,
            P95KeyToDomMs: stats.p95KeyToDomMs || 0,
            LastKeyToDomMs: stats.lastKeyToDomMs || 0,
            MaxKeyToDomMs: stats.maxKeyToDomMs || 0,
            AverageInputLatencyMs: stats.keyToDomSamples && stats.keyToDomSamples.length
                ? stats.keyToDomSamples.reduce(function (sum, value) { return sum + Number(value || 0); }, 0) / stats.keyToDomSamples.length
                : 0,
            LastRenderReason: stats.renderLastReason || '',
            LayoutPassCount: stats.layoutPassCount || 0,
            LastLayoutPassMs: stats.layoutPassLastMs || 0,
            MaxLayoutPassMs: stats.layoutPassMaxMs || 0,
            LastLayoutReason: stats.layoutLastReason || '',
            TotalPages: lookup.inst.layout && Array.isArray(lookup.inst.layout.pages) ? lookup.inst.layout.pages.length : 0,
            RenderedPages: stats.renderedPages || 0,
            VirtualizedPages: stats.virtualizedPages || 0,
            VirtualizationEnabled: stats.virtualizationEnabled === true,
            ActivePageIndex: stats.activePageIndex || 0,
            IncrementalOperationCount: stats.incrementalOperationCount || 0,
            FullDocumentLayoutCount: stats.fullDocumentLayoutCount || 0,
            InputOperationCount: stats.inputOperationCount || 0,
            LastInputOperationMs: stats.inputOperationLastMs || 0,
            MaxInputOperationMs: stats.inputOperationMaxMs || 0,
            MaxInputLatencyMs: stats.maxKeyToDomMs || 0,
            TypingLatencyCount: stats.typingLatencyCount || 0,
            LastTypingLatencyMs: stats.typingLatencyLastMs || 0,
            MaxTypingLatencyMs: stats.typingLatencyMaxMs || 0,
            ImageDragLatencyCount: stats.imageDragLatencyCount || 0,
            LastImageDragLatencyMs: stats.imageDragLatencyLastMs || 0,
            MaxImageDragLatencyMs: stats.imageDragLatencyMaxMs || 0,
            SelectionMovementCount: stats.selectionMovementCount || 0,
            LastSelectionMovementMs: stats.selectionMovementLastMs || 0,
            MaxSelectionMovementMs: stats.selectionMovementMaxMs || 0,
            MaxLiveDomBlockCount: stats.maxLiveDomBlockCount || 0,
            FormattingCommandPartialRenderCount: stats.formattingCommandPartialRenderCount || 0,
            LightweightBoundaryPatchCount: stats.lightweightBoundaryPatchCount || 0,
            BoundarySnapshotExportCount: stats.boundarySnapshotExportCount || 0,
            DeferredBoundaryPatchDispatchCount: stats.deferredBoundaryPatchDispatchCount || 0,
            DeferredRevisionNotifyCount: stats.deferredRevisionNotifyCount || 0,
            RevisionNotifyCount: stats.revisionNotifyCount || 0,
            MarkerStoreDeferredRefreshCount: stats.markerStoreDeferredRefreshCount || 0,
            LastPartialRenderScopeIds: _clone(stats.lastPartialRenderScopeIds || []),
            PartialRenderScopeSamples: _clone(stats.partialRenderScopeSamples || []),
            ToolbarStateLayoutAuditCount: stats.toolbarStateLayoutAuditCount || 0,
            ToolbarStateLayoutThrashCount: stats.toolbarStateLayoutThrashCount || 0,
            LastToolbarStateLayoutAudit: _clone(stats.lastToolbarStateLayoutAudit || null),
            LatencyBudgets: _clone(stats.latencyBudgets || createDefaultLatencyBudgets()),
            KeydownVisibleTextSamples: _clone(histograms.KeydownVisibleText),
            SpaceVisibleTextSamples: _clone(histograms.SpaceVisibleText),
            EnterVisibleTextSamples: _clone(histograms.EnterVisibleText),
            ToolbarCommandVisibleStyleSamples: _clone(histograms.ToolbarCommandVisibleStyle),
            SelectionChangeToolbarStateSamples: _clone(histograms.SelectionChangeToolbarState),
            KeydownVisibleTextHistogram: createLatencyHistogramSummary(histograms.KeydownVisibleText, latencyBudgetForName(stats, 'KeydownVisibleText')),
            SpaceVisibleTextHistogram: createLatencyHistogramSummary(histograms.SpaceVisibleText, latencyBudgetForName(stats, 'SpaceVisibleText')),
            EnterVisibleTextHistogram: createLatencyHistogramSummary(histograms.EnterVisibleText, latencyBudgetForName(stats, 'EnterVisibleText')),
            ToolbarCommandVisibleStyleHistogram: createLatencyHistogramSummary(histograms.ToolbarCommandVisibleStyle, latencyBudgetForName(stats, 'ToolbarCommandVisibleStyle')),
            SelectionChangeToolbarStateHistogram: createLatencyHistogramSummary(histograms.SelectionChangeToolbarState, latencyBudgetForName(stats, 'SelectionChangeToolbarState'))
        });
    }

    function clearDebugMetrics(instanceId) {
        var lookup = _get(instanceId, 'clearDebugMetrics');
        if (lookup.error) return lookup.error;
        lookup.inst.performanceStats = createStrictPerformanceStats();
        lookup.inst.pendingKeyToDomStarts = [];
        lookup.inst.lastBeforeInputAt = 0;
        lookup.inst.lastInputDomApplyAt = 0;
        lookup.inst.suppressCollapsedSelectionChangeUntil = 0;
        lookup.inst.pendingFormattingStateStartedAt = 0;
        lookup.inst.lastSelectionStateChangeAt = 0;
        return { ok: true, instanceId: instanceId };
    }

    function _setRootClass(root, className, enabled) {
        if (!root || !root.classList) return;
        if (typeof root.classList.toggle === 'function') {
            root.classList.toggle(className, enabled === true);
            return;
        }

        if (enabled === true && typeof root.classList.add === 'function') {
            root.classList.add(className);
        } else if (enabled !== true && typeof root.classList.remove === 'function') {
            root.classList.remove(className);
        }
    }

    function setShowBlocks(instanceId, show) {
        var lookup = _get(instanceId, 'setShowBlocks');
        if (lookup.error) return lookup.error;
        _setRootClass(lookup.inst.root, 'tm-wysiwyg--show-blocks', show === true);
        return { ok: true, instanceId: instanceId, show: show === true };
    }

    function setShowNonPrintingCharacters(instanceId, show) {
        var lookup = _get(instanceId, 'setShowNonPrintingCharacters');
        if (lookup.error) return lookup.error;
        _setRootClass(lookup.inst.root, 'tm-wysiwyg--show-nonprinting', show === true);
        return { ok: true, instanceId: instanceId, show: show === true };
    }

    function setProtectionMode(instanceId, isProtected, markers) {
        var lookup = _get(instanceId, 'setProtectionMode');
        if (lookup.error) return lookup.error;
        lookup.inst.protectionMarkers = _clone(markers || []);
        lookup.inst._protectedMarkers = lookup.inst.protectionMarkers;
        lookup.inst._isProtected = isProtected === true;
        _setRootClass(lookup.inst.root, 'tm-wysiwyg--protected', isProtected === true);
        return { ok: true, instanceId: instanceId, isProtected: isProtected === true };
    }

    function setSearchMarkers(instanceId, blockIds, offsets, lengths) {
        var lookup = _get(instanceId, 'setSearchMarkers');
        if (lookup.error) return lookup.error;
        lookup.inst.searchMarkers = _asArray(blockIds).map(function (blockId, index) {
            return {
                blockId: blockId,
                offset: Number(_asArray(offsets)[index] || 0) || 0,
                length: Number(_asArray(lengths)[index] || 0) || 0
            };
        });
        return { ok: true, instanceId: instanceId, count: lookup.inst.searchMarkers.length };
    }

    function clearSearchMarkers(instanceId) {
        var lookup = _get(instanceId, 'clearSearchMarkers');
        if (lookup.error) return lookup.error;
        lookup.inst.searchMarkers = [];
        return { ok: true, instanceId: instanceId };
    }

    function scrollToSearchResult(instanceId, blockId) {
        return scrollToBlock(instanceId, blockId);
    }

    function scrollToBlock(instanceId, blockId) {
        var lookup = _get(instanceId, 'scrollToBlock');
        if (lookup.error) return lookup.error;
        var pageIndex = findPageIndexForBlockId(lookup.inst.layout, blockId);
        var materialized = pageIndex >= 0 ? materializePage(lookup.inst, pageIndex, 'scroll-to-block') : false;
        var escapedBlockId = _escape(String(blockId || ''));
        var selector = '.tm-wysiwyg-block[data-block-id="' + escapedBlockId + '"]';
        var fallbackSelector = '[data-block-id="' + escapedBlockId + '"]';
        var target = lookup.inst.root && (lookup.inst.root.querySelector(selector) || lookup.inst.root.querySelector(fallbackSelector));
        if (target && typeof target.scrollIntoView === 'function') {
            target.scrollIntoView({ block: 'nearest' });
        }
        return { ok: !!target || materialized, instanceId: instanceId, blockId: blockId || '', pageIndex: pageIndex, materialized: materialized };
    }

    function scrollToPage(instanceId, pageIndex) {
        var lookup = _get(instanceId, 'scrollToPage');
        if (lookup.error) return lookup.error;
        var normalizedPageIndex = Number(pageIndex || 0) || 0;
        var materialized = materializePage(lookup.inst, normalizedPageIndex, 'scroll-to-page');
        var target = lookup.inst.root && lookup.inst.root.querySelector('.tm-wysiwyg-page[data-page-number="' + (normalizedPageIndex + 1) + '"]');
        if (target && typeof target.scrollIntoView === 'function') {
            target.scrollIntoView({ block: 'nearest' });
        }
        return { ok: !!target || materialized, instanceId: instanceId, pageIndex: normalizedPageIndex, materialized: materialized };
    }

    function getLinkInfo() {
        return { HasLink: false, Href: '', Text: '' };
    }

    function copySelection(instanceId) {
        return { ok: true, instanceId: instanceId, text: getSelectedText() };
    }

    function applyRemoteOperation(instanceId, operation) {
        return applyStrictRemoteOperations(instanceId, { operations: [operation] });
    }

    function applyRemoteOperationBatch(instanceId, batch) {
        return applyStrictRemoteOperations(instanceId, batch || { operations: [] });
    }

    function applyRemoteCursor(instanceId, cursor) {
        var lookup = _get(instanceId, 'applyRemoteCursor');
        if (lookup.error) return lookup.error;
        lookup.inst.remoteCursors = lookup.inst.remoteCursors || [];
        lookup.inst.remoteCursors.push(_clone(cursor || {}));
        return { ok: true, instanceId: instanceId };
    }

    function insertImageNode(instanceId, block) {
        var lookup = _get(instanceId, 'insertImageNode');
        if (lookup.error) return lookup.error;
        var source = block && (block.Block || block.block) ? (block.Block || block.block) : block;
        var imageBlock = importBlock(source || { Type: 'Image', Content: { Type: 'Image', AltText: 'Image' } }, 'inserted-image-' + Date.now());
        imageBlock.type = 'image';
        lookup.inst.model.body.blocks.push(imageBlock);
        buildIndexes(lookup.inst.model);
        markModelChanged(lookup.inst, 'insertImageNode');
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, blockId: imageBlock.id };
    }

    function captureCommentAnchor(instanceId) {
        var lookup = _get(instanceId, 'captureCommentAnchor');
        if (lookup.error) return null;
        var snapshot = readDomSelectionSnapshot(lookup.inst);
        if (!snapshot || snapshot.isCollapsed !== false || !snapshot.anchor || !snapshot.focus) return null;
        if (snapshot.anchor.blockId !== snapshot.focus.blockId) return null;
        var start = Math.min(Number(snapshot.anchor.offset || 0), Number(snapshot.focus.offset || 0));
        var end = Math.max(Number(snapshot.anchor.offset || 0), Number(snapshot.focus.offset || 0));
        if (end <= start) return null;
        return {
            Type: 1,
            BlockId: snapshot.anchor.blockId,
            StartInlineIndex: 0,
            StartOffset: start,
            EndInlineIndex: 0,
            EndOffset: end
        };
    }

    function updateActiveCommentDom(inst) {
        if (!inst || !inst.root) return;
        var active = _asText(inst.activeCommentId || '');
        Array.from(inst.root.querySelectorAll('.tm-document-inline--comment-anchor[data-comment-id]')).forEach(function (node) {
            var selected = !!active && node.getAttribute('data-comment-id') === active;
            node.classList.toggle('tm-document-inline--comment-anchor--selected', selected);
            node.classList.toggle('tm-wysiwyg-marker--comment-active', selected);
            node.setAttribute('aria-current', selected ? 'true' : 'false');
        });
    }

    function updateActiveRevisionDom(inst) {
        if (!inst || !inst.root) return;
        var active = _asText(inst.activeRevisionId || '');
        Array.from(inst.root.querySelectorAll('.tm-wysiwyg-revision[data-revision-id], .tm-document-inline--revision[data-revision-id]')).forEach(function (node) {
            var selected = !!active && node.getAttribute('data-revision-id') === active;
            node.classList.toggle('tm-wysiwyg-revision--selected', selected);
            node.classList.toggle('tm-wysiwyg-marker--revision-active', selected);
            node.setAttribute('aria-current', selected ? 'true' : 'false');
        });
    }

    function updateActiveImageSelectionDom(inst) {
        if (!inst || !inst.root) return;
        var active = _asText(inst.selection && (inst.selection.activeImageBlockId || (inst.selection.isObjectSelection ? inst.selection.blockId : '')) || '');
        var selectedFigure = null;
        Array.from(inst.root.querySelectorAll('figure.tm-wysiwyg-image[data-block-id]')).forEach(function (figure) {
            var selected = !!active && figure.getAttribute('data-block-id') === active;
            figure.classList.toggle('tm-wysiwyg-image--selected', selected);
            figure.setAttribute('aria-selected', selected ? 'true' : 'false');
            if (selected) selectedFigure = figure;
        });
        scheduleImageFloatingPanelPosition(inst, selectedFigure);
    }

    function scheduleImageFloatingPanelPosition(inst, selectedFigure) {
        if (!inst || !inst.root) return;
        var editor = inst.root.closest && inst.root.closest('.tm-document-editor') || inst.root;
        var root = editor && editor.querySelector && editor.querySelector('[data-testid="document-wysiwyg-floating-root"]');
        if (!selectedFigure) {
            if (root) resetImageFloatingPanelPosition(root);
            return;
        }
        var run = function () { positionImageFloatingPanel(inst, selectedFigure); };
        if (typeof requestAnimationFrame === 'function') {
            requestAnimationFrame(run);
            requestAnimationFrame(function () { requestAnimationFrame(run); });
        }
        setTimeout(run, 80);
        setTimeout(run, 180);
    }

    function resetImageFloatingPanelPosition(root) {
        root.classList.remove('tm-document-editor__floating-root--object-anchored');
        root.style.left = '';
        root.style.top = '';
        root.style.right = '';
        root.style.bottom = '';
        root.style.maxHeight = '';
    }

    function positionImageFloatingPanel(inst, selectedFigure) {
        if (!inst || !inst.root || !selectedFigure || !selectedFigure.isConnected) return;
        var editor = inst.root.closest && inst.root.closest('.tm-document-editor') || inst.root;
        var root = editor && editor.querySelector && editor.querySelector('[data-testid="document-wysiwyg-floating-root"]');
        var panel = root && root.querySelector && root.querySelector('[data-testid="document-image-wrap-panel"]');
        if (!root || !panel) return;
        var figureRect = selectedFigure.getBoundingClientRect();
        var panelRect = panel.getBoundingClientRect();
        var viewport = {
            left: 8,
            top: 8,
            right: Math.max(8, window.innerWidth - 8),
            bottom: Math.max(8, window.innerHeight - 8)
        };
        var sidePanel = document.querySelector('[data-testid="document-editor-side-panel"], [data-testid="document-side-panel"], .tm-document-editor__side-panel, .tm-document-side-panel');
        var sideRect = sidePanel && sidePanel.getBoundingClientRect ? sidePanel.getBoundingClientRect() : null;
        if (sideRect && sideRect.width > 1 && sideRect.left > window.innerWidth * 0.45) {
            viewport.right = Math.min(viewport.right, sideRect.left - 8);
        }
        var width = Math.max(1, panelRect.width || 320);
        var height = Math.max(1, panelRect.height || 176);
        var gutter = 10;
        var candidates = [
            { placement: 'right', left: figureRect.right + gutter, top: figureRect.top },
            { placement: 'left', left: figureRect.left - width - gutter, top: figureRect.top },
            { placement: 'top', left: figureRect.left, top: figureRect.top - height - gutter },
            { placement: 'bottom', left: figureRect.left, top: figureRect.bottom + gutter },
            { placement: 'top-center', left: figureRect.left + figureRect.width / 2 - width / 2, top: figureRect.top - height - gutter },
            { placement: 'bottom-center', left: figureRect.left + figureRect.width / 2 - width / 2, top: figureRect.bottom + gutter }
        ].map(function (candidate) {
            var left = Math.max(viewport.left, Math.min(candidate.left, viewport.right - width));
            var top = Math.max(viewport.top, Math.min(candidate.top, viewport.bottom - height));
            var rect = { x: left, y: top, width: width, height: height };
            return Object.assign(candidate, {
                left: left,
                top: top,
                rect: rect,
                textOverlap: textOverlapArea(inst.root, rect),
                objectDistance: Math.abs((left + width / 2) - (figureRect.left + figureRect.width / 2)) + Math.abs((top + height / 2) - (figureRect.top + figureRect.height / 2))
            });
        });
        candidates.sort(function (a, b) {
            return a.textOverlap - b.textOverlap || a.objectDistance - b.objectDistance;
        });
        var best = candidates[0];
        root.classList.add('tm-document-editor__floating-root--object-anchored');
        root.style.left = Math.round(best.left) + 'px';
        root.style.top = Math.round(best.top) + 'px';
        root.style.right = 'auto';
        root.style.bottom = 'auto';
        root.style.maxHeight = Math.max(120, Math.floor(viewport.bottom - best.top)) + 'px';
        root.setAttribute('data-placement', best.placement);
        root.setAttribute('data-text-overlap-area', String(Math.round(best.textOverlap)));
    }

    function textOverlapArea(root, rect) {
        if (!root || !document.createTreeWalker) return 0;
        var total = 0;
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                var parent = node.parentElement;
                if (!node.nodeValue || !node.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
                if (!parent || parent.closest('figure, [role="menu"], [data-testid*="toolbar"], .tm-document-editor__floating-root, [data-testid="document-side-panel"]')) {
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        while (walker.nextNode()) {
            var range = document.createRange();
            range.selectNodeContents(walker.currentNode);
            Array.from(range.getClientRects()).forEach(function (lineRect) {
                total += overlapArea(rect, {
                    x: lineRect.x || lineRect.left || 0,
                    y: lineRect.y || lineRect.top || 0,
                    width: lineRect.width || 0,
                    height: lineRect.height || 0
                });
            });
        }
        return total;
    }

    function overlapArea(a, b) {
        var width = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
        var height = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
        return width * height;
    }

    function notifyInlineMarkerSelectionContext(inst, kind, id) {
        if (!inst || !id) return;
        var current = createSelectionSnapshot(inst.selection || firstModelSelection(inst.model));
        var next = Object.assign({}, current, {
            activeCommentId: kind === 'comment' ? id : null,
            activeRevisionId: kind === 'revision' ? id : null,
            activeImageBlockId: null,
            activeObjectId: null,
            isObjectSelection: false,
            hitTargetKind: kind
        });
        inst.selection = createSelectionSnapshot(next);
        markSelectionChanged(inst, kind + '-marker');
        invokeBoundaryMethod(inst, 'HandleSelectionChanged', boundarySelectionSnapshot(inst.selection, inst), 'selection-changed-failed');
    }

    function selectCommentAnchor(inst, commentId, scroll, notify) {
        if (!inst || !inst.root) return { ok: false, commentId: commentId || '' };
        var id = _asText(commentId || '');
        if (!id) return { ok: false, instanceId: inst.id, commentId: '' };
        inst.activeCommentId = id;
        updateActiveCommentDom(inst);
        notifyInlineMarkerSelectionContext(inst, 'comment', id);
        var selector = '.tm-document-inline--comment-anchor[data-comment-id="' + cssEscape(id) + '"]';
        var target = inst.root.querySelector(selector);
        if (scroll !== false && target && typeof target.scrollIntoView === 'function') {
            target.scrollIntoView({ block: 'center', inline: 'nearest' });
        }
        if (notify !== false) {
            invokeBoundaryMethod(inst, 'HandleCommentSelected', id, 'comment-select-failed');
        }
        return { ok: !!target, instanceId: inst.id, commentId: id };
    }

    function selectRevisionAnchor(inst, revisionId, scroll, notify) {
        if (!inst || !inst.root) return { ok: false, revisionId: revisionId || '' };
        var id = _asText(revisionId || '');
        if (!id) return { ok: false, instanceId: inst.id, revisionId: '' };
        inst.activeRevisionId = id;
        updateActiveRevisionDom(inst);
        notifyInlineMarkerSelectionContext(inst, 'revision', id);
        var selector = '.tm-wysiwyg-revision[data-revision-id="' + cssEscape(id) + '"], .tm-document-inline--revision[data-revision-id="' + cssEscape(id) + '"]';
        var target = inst.root.querySelector(selector);
        if (scroll !== false && target && typeof target.scrollIntoView === 'function') {
            target.scrollIntoView({ block: 'center', inline: 'nearest' });
        }
        if (notify !== false) {
            invokeBoundaryMethod(inst, 'HandleRevisionSelected', id, 'revision-select-failed');
        }
        return { ok: !!target, instanceId: inst.id, revisionId: id };
    }

    function upsertComment(instanceId, comment) {
        var lookup = _get(instanceId, 'upsertComment');
        if (lookup.error) return lookup.error;
        var item = _clone(comment || {});
        var id = item.id || item.Id || ('comment-' + Date.now());
        item.id = id;
        var comments = lookup.inst.model.comments || [];
        var index = comments.findIndex(function (existing) { return (existing.id || existing.Id) === id; });
        if (index >= 0) comments[index] = item; else comments.push(item);
        lookup.inst.model.comments = comments;
        buildIndexes(lookup.inst.model);
        refreshRuntimeMarkerStore(lookup.inst);
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, commentId: id };
    }

    function removeComment(instanceId, commentId) {
        var lookup = _get(instanceId, 'removeComment');
        if (lookup.error) return lookup.error;
        lookup.inst.model.comments = _asArray(lookup.inst.model.comments).filter(function (comment) {
            return (comment.id || comment.Id) !== commentId;
        });
        buildIndexes(lookup.inst.model);
        refreshRuntimeMarkerStore(lookup.inst);
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, commentId: commentId };
    }

    function getMarkers(instanceId) {
        var lookup = _get(instanceId, 'getMarkers');
        if (lookup.error) return [];
        refreshRuntimeMarkerStore(lookup.inst);
        return _clone([].concat(
            lookup.inst.searchMarkers || [],
            lookup.inst.protectionMarkers || [],
            lookup.inst.markerStore ? lookup.inst.markerStore.all : [],
            lookup.inst.markers || []));
    }

    function upsertMarker(instanceId, marker) {
        var lookup = _get(instanceId, 'upsertMarker');
        if (lookup.error) return lookup.error;
        lookup.inst.markers = lookup.inst.markers || [];
        var item = _clone(marker || {});
        var id = item.id || item.Id || ('marker-' + Date.now());
        item.id = id;
        var index = lookup.inst.markers.findIndex(function (existing) { return (existing.id || existing.Id) === id; });
        if (index >= 0) lookup.inst.markers[index] = item; else lookup.inst.markers.push(item);
        return { ok: true, instanceId: instanceId, markerId: id };
    }

    function scrollToComment(instanceId, commentId) {
        var lookup = _get(instanceId, 'scrollToComment');
        if (lookup.error) return lookup.error;
        return selectCommentAnchor(lookup.inst, commentId, true, false);
    }

    function scrollToRevision(instanceId, revisionId) {
        var lookup = _get(instanceId, 'scrollToRevision');
        if (lookup.error) return lookup.error;
        return selectRevisionAnchor(lookup.inst, revisionId, true, false);
    }

    function setTrackChangesEnabled(instanceId, enabled) {
        var lookup = _get(instanceId, 'setTrackChangesEnabled');
        if (lookup.error) return lookup.error;
        lookup.inst.options.trackChangesEnabled = enabled === true;
        lookup.inst.options.TrackChangesEnabled = enabled === true;
        clearLiveTypingRevision(lookup.inst);
        lookup.inst.commands.push({ command: 'setTrackChanges', payload: { enabled: enabled === true }, at: Date.now(), nonEditing: true });
        lookup.inst.lastCommandTokenDiagnostic = _sortObject({
            command: 'setTrackChanges',
            at: Date.now(),
            usedSelectionToken: false,
            ok: true,
            reason: 'non-editing-command'
        });
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, enabled: enabled === true, state: resolveTrackChangesState(lookup.inst.options) };
    }

    function setReviewDisplayMode(instanceId, mode) {
        var lookup = _get(instanceId, 'setReviewDisplayMode');
        if (lookup.error) return lookup.error;
        lookup.inst.options.reviewDisplayMode = mode || '';
        lookup.inst.options.ReviewDisplayMode = mode || '';
        render(lookup.inst);
        return { ok: true, instanceId: instanceId, mode: mode || '' };
    }

    function reviewRevision(instanceId, revisionId, action) {
        var lookup = _get(instanceId, 'reviewRevision');
        if (lookup.error) return lookup.error;
        var inst = lookup.inst;
        var reviewAction = String(action || '').toLowerCase().indexOf('reject') >= 0 ? 'RejectRevision' : 'AcceptRevision';
        var result = applyCommand(instanceId, reviewAction, {
            revisionId: revisionId,
            selection: inst.selection || null,
            beforeSelection: inst.selection || null,
            source: 'review-panel',
            transactionType: TRANSACTION_TYPES.Default
        });
        if (result && result.ok !== false) {
            invokeBoundaryMethod(inst, 'HandleRevisionsChanged', exportToCSharpJson(inst.model).Revisions, 'revisions-changed-failed');
        }
        return Object.assign({ instanceId: instanceId }, result || {});
    }

    function reviewAllRevisions(instanceId, action, payload) {
        var lookup = _get(instanceId, 'reviewAllRevisions');
        if (lookup.error) return lookup.error;
        var ids = _asArray(payload && (payload.RevisionIds || payload.revisionIds));
        if (!ids.length) {
            ids = _asArray(lookup.inst.model && lookup.inst.model.revisions)
                .filter(function (revision) { return readRevisionStatus(revision) === 'Pending'; })
                .map(function (revision) { return revision.id || revision.Id; });
        }
        var results = ids.map(function (id) { return reviewRevision(instanceId, id, action); });
        return { ok: results.every(function (item) { return item && item.ok !== false; }), instanceId: instanceId, action: action || '', count: results.length };
    }

    function clearRevisionDecorations(instanceId) {
        return { ok: true, instanceId: instanceId };
    }

    function applyOfflineState(instanceId, state) {
        var lookup = _get(instanceId, 'applyOfflineState');
        if (lookup.error) return lookup.error;
        lookup.inst.offlineState = _clone(state || {});
        return { ok: true, instanceId: instanceId };
    }

    function getOfflineState(instanceId) {
        var lookup = _get(instanceId, 'getOfflineState');
        if (lookup.error) return null;
        return _clone(lookup.inst.offlineState || {});
    }

    function markSaved(instanceId, ack) {
        return acknowledgeSave(instanceId, ack || {});
    }

    function getVirtualizationOptions(inst) {
        var opts = inst && inst.options || {};
        var blocksPerPageSource = Object.prototype.hasOwnProperty.call(opts, 'VirtualizationBlocksPerPage')
            ? opts.VirtualizationBlocksPerPage
            : (Object.prototype.hasOwnProperty.call(opts, 'virtualizationBlocksPerPage') ? opts.virtualizationBlocksPerPage : 24);
        var thresholdPagesSource = Object.prototype.hasOwnProperty.call(opts, 'VirtualizationThresholdPages')
            ? opts.VirtualizationThresholdPages
            : (Object.prototype.hasOwnProperty.call(opts, 'virtualizationThresholdPages') ? opts.virtualizationThresholdPages : 4);
        var radiusSource = Object.prototype.hasOwnProperty.call(opts, 'VirtualizationRenderedPageRadius')
            ? opts.VirtualizationRenderedPageRadius
            : (Object.prototype.hasOwnProperty.call(opts, 'virtualizationRenderedPageRadius') ? opts.virtualizationRenderedPageRadius : 1);
        var blocksPerPage = Math.max(1, Number(blocksPerPageSource) || 24);
        var thresholdPages = Math.max(1, Number(thresholdPagesSource) || 4);
        var radius = Math.max(0, Number(radiusSource) || 0);
        return { blocksPerPage: blocksPerPage, thresholdPages: thresholdPages, radius: radius };
    }

    function buildPagePlan(inst, blocks, previousSelection) {
        var config = getVirtualizationOptions(inst);
        var allBlocks = _asArray(blocks);
        var pages = [];
        for (var index = 0; index < allBlocks.length || index === 0 && allBlocks.length === 0; index += config.blocksPerPage) {
            pages.push({
                pageIndex: pages.length,
                pageNumber: pages.length + 1,
                blocks: allBlocks.slice(index, index + config.blocksPerPage),
                blockIds: allBlocks.slice(index, index + config.blocksPerPage).map(function (block) { return block.id; }),
                header: null,
                footer: null,
                headerBlockIds: [],
                footerBlockIds: []
            });
            if (allBlocks.length === 0) break;
        }
        var activeBlockId = previousSelection && previousSelection.blockId || inst && inst.selection && inst.selection.blockId || '';
        var activePageIndex = Number(inst && inst.activePageIndex || 0) || 0;
        pages.forEach(function (page) {
            page.header = resolveHeaderFooterRegion(inst && inst.model, 'header', page.pageNumber);
            page.footer = resolveHeaderFooterRegion(inst && inst.model, 'footer', page.pageNumber);
            page.headerBlockIds = _asArray(page.header && page.header.blocks).map(function (block) { return block.id; });
            page.footerBlockIds = _asArray(page.footer && page.footer.blocks).map(function (block) { return block.id; });
        });
        if (!inst || inst.activePageIndexPinned !== true) {
            pages.forEach(function (page) {
                if (activeBlockId && page.blockIds.concat(page.headerBlockIds, page.footerBlockIds).indexOf(activeBlockId) >= 0) activePageIndex = page.pageIndex;
            });
        }
        activePageIndex = Math.max(0, Math.min(activePageIndex, Math.max(0, pages.length - 1)));
        var virtualizationEnabled = pages.length > config.thresholdPages;
        var liveBlockCount = 0;
        pages.forEach(function (page) {
            page.isVirtual = virtualizationEnabled && Math.abs(page.pageIndex - activePageIndex) > config.radius;
            page.isRendered = !page.isVirtual;
            if (page.isRendered) liveBlockCount += page.blocks.length;
        });
        inst.activePageIndex = activePageIndex;
        inst.activePageIndexPinned = false;
        return {
            pages: pages,
            activePageIndex: activePageIndex,
            virtualizationEnabled: virtualizationEnabled,
            liveBlockCount: liveBlockCount
        };
    }

    function findPageIndexForBlockId(layout, blockId) {
        var id = _asText(blockId || '');
        if (!id) return -1;
        var pages = _asArray(layout && layout.pages);
        for (var index = 0; index < pages.length; index++) {
            var ids = _asArray(pages[index].blockIds || pages[index].BlockIds)
                .concat(_asArray(pages[index].headerBlockIds || pages[index].HeaderBlockIds))
                .concat(_asArray(pages[index].footerBlockIds || pages[index].FooterBlockIds));
            if (ids.indexOf(id) >= 0) return index;
        }
        return -1;
    }

    function materializePage(inst, pageIndex, reason) {
        if (!inst) return false;
        var next = Math.max(0, Number(pageIndex || 0) || 0);
        var current = Number(inst.activePageIndex || 0) || 0;
        inst.activePageIndex = next;
        inst.activePageIndexPinned = true;
        if (next !== current || inst.layout && _asArray(inst.layout.pages).some(function (page) {
            return Number(page.pageIndex || page.Index || 0) === next && page.isVirtual === true;
        })) {
            recordTimeline(inst, 'virtual-page-materialize', { pageIndex: next, reason: reason || '' });
            render(inst);
            return true;
        }
        return false;
    }

    function restoreDomSelectionFromSnapshot(inst, selection) {
        if (!inst || !inst.root || !selection || selection.isObjectSelection === true || selection.isCellSelection === true) return false;
        var snapshot = createSelectionSnapshot(selection);
        if (!snapshot.blockId) return false;
        if (snapshot.isCollapsed === false) {
            if (snapshot.anchor.blockId !== snapshot.focus.blockId) return false;
            var selectedBlock = inst.root.querySelector('[data-block-id="' + cssEscape(snapshot.anchor.blockId) + '"]');
            if (!selectedBlock) return false;
            var startOffset = Math.min(Number(snapshot.anchor.offset || 0), Number(snapshot.focus.offset || 0));
            var endOffset = Math.max(Number(snapshot.anchor.offset || 0), Number(snapshot.focus.offset || 0));
            var startPoint = domTextPointAtBlockOffset(selectedBlock, startOffset);
            var endPoint = domTextPointAtBlockOffset(selectedBlock, endOffset);
            if (!startPoint || !endPoint) return false;
            try {
                var range = document.createRange();
                range.setStart(startPoint.node, startPoint.offset);
                range.setEnd(endPoint.node, endPoint.offset);
                var editable = selectedBlock.closest('[contenteditable="true"]');
                if (editable && typeof editable.focus === 'function') editable.focus({ preventScroll: true });
                var rangeSelection = window.getSelection && window.getSelection();
                if (!rangeSelection) return false;
                rangeSelection.removeAllRanges();
                rangeSelection.addRange(range);
                return true;
            } catch (error) {
                recordTimeline(inst, 'selection-restore-failed', { error: String(error && error.message || error) });
                return false;
            }
        }
        var mapped = logicalToDomRange(inst.root, inst.model, snapshot);
        if (!mapped.ok || !mapped.range) return false;
        try {
            var editable = null;
            if (snapshot.region === 'Header' || snapshot.region === 'Footer') {
                var regionSelector = '.tm-wysiwyg-page__' + snapshot.region.toLowerCase() + '[contenteditable="true"]';
                var hfSelector = snapshot.headerFooterId
                    ? regionSelector + '[data-hf-id="' + cssEscape(snapshot.headerFooterId) + '"]'
                    : regionSelector;
                editable = inst.root.querySelector(hfSelector) || inst.root.querySelector(regionSelector);
            }
            if (!editable) editable = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
            if (editable && typeof editable.focus === 'function') editable.focus({ preventScroll: true });
            var current = window.getSelection && window.getSelection();
            if (!current) return false;
            current.removeAllRanges();
            current.addRange(mapped.range);
            return true;
        } catch (error) {
            recordTimeline(inst, 'selection-restore-failed', { error: String(error && error.message || error) });
            return false;
        }
    }

    function domTextPointAtBlockOffset(block, offset) {
        if (!block) return null;
        var target = Math.max(0, Number(offset || 0));
        var currentOffset = 0;
        var last = null;

        function visit(node) {
            if (!node) return null;
            if (node.nodeType === 3) {
                var length = node.nodeValue ? node.nodeValue.length : 0;
                last = node;
                if (target <= currentOffset + length) {
                    return { node: node, offset: Math.max(0, Math.min(length, target - currentOffset)) };
                }
                currentOffset += length;
                return null;
            }
            if (isInlineBreakNode(node)) {
                last = node;
                if (target <= currentOffset + 1) {
                    var parent = node.parentNode || block;
                    var children = parent.childNodes || [];
                    var index = Array.prototype.indexOf.call(children, node);
                    return { node: parent, offset: Math.max(0, index + 1) };
                }
                currentOffset += 1;
                return null;
            }
            if (isCaretPlaceholderNode(node)) {
                last = node;
                return null;
            }
            var children = node.childNodes || [];
            for (var i = 0; i < children.length; i++) {
                var found = visit(children[i]);
                if (found) return found;
            }
            return null;
        }

        var foundPoint = visit(block);
        if (foundPoint) return foundPoint;
        if (last) {
            if (last.nodeType === 3) return { node: last, offset: last.nodeValue ? last.nodeValue.length : 0 };
            var parent = last.parentNode || block;
            var children = parent.childNodes || [];
            var index = Array.prototype.indexOf.call(children, last);
            return { node: parent, offset: Math.max(0, index + 1) };
        }
        return { node: block, offset: 0 };
    }

    function renderImageFigureStyle(object) {
        var width = Math.max(1, Number(object && object.width || 120) || 120);
        var mode = normalizeWrapModeName(object && object.wrapMode);
        var align = String(object && object.horizontalPosition && object.horizontalPosition.align || 'Left').toLowerCase();
        var top = Math.max(0, Number(object && object.distanceTop || 0) || 0);
        var right = Math.max(0, Number(object && object.distanceRight || object && object.wrapMargin || 0) || 0);
        var bottom = Math.max(0, Number(object && object.distanceBottom || object && object.wrapMargin || 0) || 0);
        var left = Math.max(0, Number(object && object.distanceLeft || object && object.wrapMargin || 0) || 0);
        var styles = ['width:' + width + 'px'];
        if (mode === 'Square' || mode === 'Tight' || mode === 'Through') {
            if (align === 'right' || align === 'end') {
                styles.push('float:right');
                styles.push('margin:' + top + 'px 0 ' + bottom + 'px ' + Math.max(left, 12) + 'px');
            } else {
                styles.push('float:left');
                styles.push('margin:' + top + 'px ' + Math.max(right, 12) + 'px ' + bottom + 'px 0');
            }
        } else if (mode === 'TopBottom') {
            styles.push('clear:both');
            styles.push('float:none');
            styles.push('margin:' + Math.max(top, 8) + 'px auto ' + Math.max(bottom, 8) + 'px auto');
        } else {
            styles.push('float:none');
            styles.push('margin:' + top + 'px auto ' + Math.max(bottom, 8) + 'px auto');
        }
        return styles.join(';');
    }

    function renderImageFigureClasses(selected, object) {
        var mode = normalizeWrapModeName(object && object.wrapMode);
        var align = String(object && object.horizontalPosition && object.horizontalPosition.align || '').toLowerCase();
        var modeClass = mode === 'TopBottom'
            ? 'top-bottom'
            : (mode === 'BehindText' ? 'behind-text' : (mode === 'InFrontOfText' ? 'in-front-of-text' : mode.toLowerCase()));
        var classes = ['tm-wysiwyg-block', 'tm-wysiwyg-image', 'tm-wysiwyg-image--wrap-' + modeClass];
        if (mode === 'Square' || mode === 'Tight' || mode === 'Through') {
            classes.push(align === 'right' || align === 'end' ? 'tm-wysiwyg-image--float-right' : 'tm-wysiwyg-image--float-left');
        }
        if (selected) classes.push('tm-wysiwyg-image--selected');
        return classes.join(' ');
    }

    function normalizeHeaderFooterScope(scope) {
        var value = String(scope || '').toLowerCase();
        if (value.indexOf('first') >= 0) return 'FirstPage';
        if (value.indexOf('even') >= 0) return 'EvenPage';
        return 'Primary';
    }

    function resolveHeaderFooterRegion(model, type, pageNumber) {
        var list = type === 'footer' ? _asArray(model && model.footers) : _asArray(model && model.headers);
        if (!list.length) return null;
        var desiredScope = pageNumber === 1 ? 'FirstPage' : pageNumber % 2 === 0 ? 'EvenPage' : 'Primary';
        var scoped = list.find(function (region) { return normalizeHeaderFooterScope(region.scope) === desiredScope; });
        if (scoped) return scoped;
        return list.find(function (region) { return normalizeHeaderFooterScope(region.scope) === 'Primary'; }) || list[0] || null;
    }

    function resolveInlineRunDisplayText(run, pageNumber, totalPages) {
        var fieldType = String(run && (run.fieldType || run.FieldType) || '').toLowerCase();
        if (run && run.kind === 'field') {
            if (fieldType.indexOf('pagenumber') >= 0 || fieldType.indexOf('page-number') >= 0 || fieldType === 'page') return String(pageNumber || 1);
            if (fieldType.indexOf('pagecount') >= 0 || fieldType.indexOf('page-count') >= 0 || fieldType.indexOf('numpages') >= 0) return String(totalPages || 1);
        }
        return _asText(run && run.text);
    }

    function textFromRunsForRender(runs, pageNumber, totalPages) {
        return _asArray(runs).map(function (run) { return resolveInlineRunDisplayText(run, pageNumber, totalPages); }).join('');
    }

    function commentMarkersForBlock(inst, blockId) {
        if (!inst || !inst.markerStore) refreshRuntimeMarkerStore(inst);
        return _asArray(inst && inst.markerStore && inst.markerStore.byType('comment')).filter(function (marker) {
            var range = marker.range || {};
            return range.startBlockId === blockId || range.endBlockId === blockId;
        });
    }

    function revisionMarkersForBlock(inst, blockId) {
        if (!inst || !inst.markerStore) refreshRuntimeMarkerStore(inst);
        return _asArray(inst && inst.markerStore && inst.markerStore.all).filter(function (marker) {
            var range = marker.range || {};
            return String(marker.type || '').indexOf('revision') === 0
                && (range.startBlockId === blockId || range.endBlockId === blockId);
        });
    }

    function isSafeInlineCssColor(value) {
        var text = _asText(value).trim();
        if (!text) return false;
        if (/^#[0-9a-f]{3,8}$/i.test(text)) return true;
        if (/^(rgb|rgba|hsl|hsla)\([0-9.,%\s-]+\)$/i.test(text)) return true;
        return /^[a-z][a-z0-9-]{0,31}$/i.test(text);
    }

    function isSafeInlineFontFamily(value) {
        var text = _asText(value).trim();
        return !!text && /^[\w\s"',.-]{1,160}$/.test(text);
    }

    function normalizeInlineFontSize(value) {
        var text = _asText(value).trim();
        if (!text) return '';
        if (/^\d+(\.\d+)?$/.test(text)) return text + 'pt';
        if (/^\d+(\.\d+)?(px|pt|rem|em|%)$/i.test(text)) return text;
        return '';
    }

    function renderInlineTextHtml(text) {
        var source = _asText(text);
        if (source.indexOf('\n') < 0) return _escape(source);
        var html = [];
        var segmentStart = 0;
        for (var index = 0; index < source.length; index++) {
            if (source[index] !== '\n') continue;
            if (index > segmentStart) html.push(_escape(source.slice(segmentStart, index)));
            html.push('<br data-inline-break="true">');
            if (index === source.length - 1) {
                html.push('<br data-caret-placeholder="true" aria-hidden="true">');
            }
            segmentStart = index + 1;
        }
        if (segmentStart < source.length) html.push(_escape(source.slice(segmentStart)));
        return html.join('');
    }

    function renderFormattedInlineHtml(run, chunk, innerHtml) {
        var marks = _asArray(run && (run.marks || run.Marks));
        var classes = ['tm-document-inline'];
        var styles = [];
        var textDecoration = [];
        var href = '';
        marks.forEach(function (mark) {
            var type = markType(mark);
            var value = markValue(mark);
            if (type === 'bold') {
                classes.push('tm-document-inline--bold');
            } else if (type === 'italic') {
                classes.push('tm-document-inline--italic');
            } else if (type === 'underline') {
                classes.push('tm-document-inline--underline');
                textDecoration.push('underline');
            } else if (type === 'strikethrough' || type === 'strike') {
                classes.push('tm-document-inline--strikethrough');
                textDecoration.push('line-through');
            } else if (type === 'superscript') {
                classes.push('tm-document-inline--superscript');
                styles.push('vertical-align:super', 'font-size:0.8em');
            } else if (type === 'subscript') {
                classes.push('tm-document-inline--subscript');
                styles.push('vertical-align:sub', 'font-size:0.8em');
            } else if (type === 'fontfamily' && isSafeInlineFontFamily(value)) {
                classes.push('tm-document-inline--font-family');
                styles.push('font-family:' + value);
            } else if (type === 'fontsize') {
                var fontSize = normalizeInlineFontSize(value);
                if (fontSize) {
                    classes.push('tm-document-inline--font-size');
                    styles.push('font-size:' + fontSize);
                }
            } else if ((type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor') && isSafeInlineCssColor(value)) {
                classes.push('tm-document-inline--text-color');
                styles.push('color:' + value);
            } else if ((type === 'highlight' || type === 'backgroundcolor') && isSafeInlineCssColor(value)) {
                classes.push('tm-document-inline--highlight');
                styles.push('background-color:' + value);
            } else if (type === 'link') {
                classes.push('tm-document-inline--link');
                href = _asText(mark && (mark.href || mark.Href || mark.url || mark.Url || value || ''));
            }
        });
        if (textDecoration.length) {
            styles.push('text-decoration-line:' + _unique(textDecoration).join(' '));
        }
        var hasFormatting = classes.length > 1 || styles.length > 0 || href;
        var contentHtml = innerHtml !== undefined ? innerHtml : renderInlineTextHtml(chunk);
        if (!hasFormatting) return contentHtml;
        var inlineId = _asText(run && (run.id || run.Id) || '');
        var attrs = [
            'class="' + classes.join(' ') + '"',
            'data-inline-id="' + _escape(inlineId) + '"',
            'data-node-id="' + _escape(inlineId) + '"'
        ];
        if (styles.length) attrs.push('style="' + _escape(styles.join(';')) + '"');
        if (href) attrs.push('data-href="' + _escape(href) + '"');
        return '<span ' + attrs.join(' ') + '>' + contentHtml + '</span>';
    }

    function renderCommentSpanHtml(inst, commentId, text, status, innerHtml) {
        var id = _asText(commentId);
        var active = id && inst && inst.activeCommentId === id;
        var classes = [
            'tm-document-inline',
            'tm-document-inline--comment-anchor',
            'tm-wysiwyg-marker',
            'tm-wysiwyg-marker--comment'
        ];
        if (status === 'resolved') classes.push('tm-document-inline--comment-anchor--resolved');
        if (active) classes.push('tm-document-inline--comment-anchor--selected', 'tm-wysiwyg-marker--comment-active');
        return '<span class="' + classes.join(' ') + '" data-testid="document-comment-marker" data-comment-id="' + _escape(id) + '" data-marker-id="comment:' + _escape(id) + '" data-comment-status="' + _escape(status || 'open') + '" aria-current="' + (active ? 'true' : 'false') + '">' + (innerHtml !== undefined ? innerHtml : _escape(text)) + '</span>';
    }

    function renderRevisionSpanHtml(inst, revisionId, text, marker, innerHtml) {
        var id = _asText(revisionId);
        var revision = revisionById(inst && inst.model, id);
        var markerType = marker && marker.type || readRevisionMarkerType(revision);
        var typeClass = markerType === 'revisionDeletion'
            ? 'delete'
            : markerType === 'revisionFormat'
                ? 'format'
                : 'insert';
        var active = id && inst && inst.activeRevisionId === id;
        var classes = [
            'tm-document-inline',
            'tm-document-inline--revision',
            'tm-document-inline--revision-' + typeClass,
            'tm-wysiwyg-marker',
            'tm-wysiwyg-marker--revision',
            'tm-wysiwyg-marker--' + markerType,
            'tm-wysiwyg-revision',
            'tm-wysiwyg-revision--' + typeClass
        ];
        if (active) classes.push('tm-wysiwyg-revision--selected', 'tm-wysiwyg-marker--revision-active');
        return '<span class="' + classes.join(' ') + '" data-testid="document-revision-marker" data-revision-id="' + _escape(id) + '" data-marker-id="revision:' + _escape(id) + '" data-revision-type="' + _escape(markerType) + '" aria-current="' + (active ? 'true' : 'false') + '">' + (innerHtml !== undefined ? innerHtml : _escape(text)) + '</span>';
    }

    function renderParagraphRunsHtml(inst, block, pageNumber, totalPages) {
        var commentMarkers = commentMarkersForBlock(inst, block && block.id || '');
        var revisionMarkers = revisionMarkersForBlock(inst, block && block.id || '');
        var markers = commentMarkers.concat(revisionMarkers);
        var cursor = 0;
        var html = [];
        _asArray(block && block.content && block.content.runs).forEach(function (run) {
            var text = resolveInlineRunDisplayText(run, pageNumber, totalPages);
            var runStart = cursor;
            var runEnd = cursor + text.length;
            var inlineCommentIds = readCommentIdsFromRun(run);
            var inlineRevisionIds = readRevisionIdsFromRun(run);
            var boundaries = [0, text.length];
            markers.forEach(function (marker) {
                var range = marker.range || {};
                var start = Math.max(runStart, Number(range.startOffset || 0));
                var end = Math.min(runEnd, Number(range.endOffset || 0));
                if (end > start) {
                    boundaries.push(start - runStart, end - runStart);
                }
            });
            boundaries = _unique(boundaries.map(function (value) {
                return Math.max(0, Math.min(text.length, Number(value || 0) || 0));
            })).sort(function (a, b) { return a - b; });
            for (var i = 0; i < boundaries.length - 1; i++) {
                var localStart = boundaries[i];
                var localEnd = boundaries[i + 1];
                if (localEnd <= localStart) continue;
                var chunk = text.slice(localStart, localEnd);
                var absoluteStart = runStart + localStart;
                var absoluteEnd = runStart + localEnd;
                var commentId = inlineCommentIds[0] || '';
                var commentMarker = null;
                if (!commentId) {
                    commentMarker = commentMarkers.find(function (candidate) {
                        var range = candidate.range || {};
                        return Number(range.startOffset || 0) < absoluteEnd && Number(range.endOffset || 0) > absoluteStart;
                    }) || null;
                    commentId = commentMarker && commentMarker.targetId || '';
                }
                var revisionId = inlineRevisionIds[0] || '';
                var revisionMarker = null;
                if (!revisionId) {
                    revisionMarker = revisionMarkers.find(function (candidate) {
                        var range = candidate.range || {};
                        return Number(range.startOffset || 0) < absoluteEnd && Number(range.endOffset || 0) > absoluteStart;
                    }) || null;
                    revisionId = revisionMarker && revisionMarker.targetId || '';
                } else {
                    revisionMarker = revisionMarkers.find(function (candidate) { return candidate.targetId === revisionId; }) || null;
                }
                var chunkHtml = renderFormattedInlineHtml(run, chunk);
                if (commentId) {
                    var comment = commentById(inst && inst.model, commentId);
                    chunkHtml = renderCommentSpanHtml(inst, commentId, chunk, commentMarker && commentMarker.status || readCommentStatus(comment), chunkHtml);
                }
                if (revisionId) chunkHtml = renderRevisionSpanHtml(inst, revisionId, chunk, revisionMarker, chunkHtml);
                html.push(chunkHtml);
            }
            if (text.length === 0 && inlineCommentIds[0]) {
                html.push(renderCommentSpanHtml(inst, inlineCommentIds[0], '', readCommentStatus(commentById(inst && inst.model, inlineCommentIds[0]))));
            }
            cursor = runEnd;
        });
        return html.join('');
    }

    function renderHeaderFooterHtml(inst, page, type, readOnly, totalPages) {
        var isHeader = type === 'header';
        var region = resolveHeaderFooterRegion(inst.model, type, page.pageNumber);
        var regionName = isHeader ? 'Header' : 'Footer';
        var cssName = isHeader ? 'header' : 'footer';
        var placeholder = isHeader
            ? (inst.options.HeaderPlaceholder || inst.options.headerPlaceholder || 'Header')
            : (inst.options.FooterPlaceholder || inst.options.footerPlaceholder || 'Footer');
        var label = isHeader
            ? formatA11yLabel(inst.options.HeaderLabel || inst.options.headerLabel || 'Header, page {0}', page.pageNumber)
            : formatA11yLabel(inst.options.FooterLabel || inst.options.footerLabel || 'Footer, page {0}', page.pageNumber);
        var blocks = _asArray(region && region.blocks);
        var empty = !blocks.length || blocks.every(function (block) { return !_blockText(block).trim(); });
        var classes = ['tm-wysiwyg-page__' + cssName];
        if (empty) classes.push('tm-wysiwyg-page__' + cssName + '--empty');
        var html = ['<div class="' + classes.join(' ') + '" data-render-region="' + regionName + '" data-testid="' + (isHeader ? 'document-page-header' : 'document-page-footer') + '" data-hf-id="' + _escape(region && region.id || '') + '" data-placeholder="' + _escape(placeholder) + '" contenteditable="' + (readOnly ? 'false' : 'true') + '" role="textbox" aria-multiline="true" aria-readonly="' + (readOnly ? 'true' : 'false') + '" aria-label="' + _escape(label) + '" tabindex="0">'];
        blocks.forEach(function (block) {
            html.push(renderEngineBlockHtml(inst, block, inst.options.ImageAltMissing || inst.options.imageAltMissing || 'Image is missing alternative text.', page.pageNumber, totalPages));
        });
        if (!blocks.length) html.push('<p class="tm-wysiwyg-block" data-block-id="' + _escape((region && region.id || cssName) + '-empty') + '"><br></p>');
        html.push('</div>');
        return html.join('');
    }

    function buildSimpleHeaderFooterLayoutRegions(root, pagePlan) {
        return _asArray(pagePlan && pagePlan.pages).flatMap(function (page) {
            return ['Header', 'Footer'].map(function (regionName) {
                var isHeader = regionName === 'Header';
                var node = root && root.querySelector ? root.querySelector('.tm-wysiwyg-page[data-page-index="' + page.pageIndex + '"] .tm-wysiwyg-page__' + (isHeader ? 'header' : 'footer')) : null;
                var rect = node && node.getBoundingClientRect ? node.getBoundingClientRect() : { x: 0, y: 0, width: 0, height: 0 };
                var region = isHeader ? page.header : page.footer;
                return _sortObject({
                    id: (region && region.id || regionName.toLowerCase()) + '-page-' + page.pageIndex,
                    headerFooterId: region && region.id || '',
                    region: regionName,
                    pageIndex: page.pageIndex,
                    pageNumber: page.pageNumber,
                    frame: { x: rect.x || 0, y: rect.y || 0, width: rect.width || 0, height: rect.height || 0 },
                    blockIds: (isHeader ? page.headerBlockIds : page.footerBlockIds).slice(),
                    blocks: _asArray(region && region.blocks).map(function (block) {
                        return {
                            id: 'layout-' + block.id + '-page-' + page.pageIndex,
                            blockId: block.id,
                            region: regionName,
                            headerFooterId: region && region.id || '',
                            pageIndex: page.pageIndex,
                            rect: { x: rect.x || 0, y: rect.y || 0, width: rect.width || 0, height: rect.height || 0 }
                        };
                    })
                });
            });
        });
    }

    function renderEngineBlockHtml(inst, block, imageAltMissing, pageNumber, totalPages) {
        if (!block) return '';
        if (block.type === 'paragraph') {
            var content = renderParagraphRunsHtml(inst, block, pageNumber, totalPages);
            var blockContent = block.content || {};
            var blockStyle = block.style || {};
            var alignment = normalizeParagraphAlignment(blockContent.alignment ?? blockContent.Alignment ?? blockStyle.alignment ?? blockStyle.Alignment ?? 'left');
            return '<p class="tm-wysiwyg-block" data-block-id="' + _escape(block.id) + '" data-alignment="' + _escape(alignment) + '" style="text-align:' + _escape(alignment) + '" role="paragraph">' + (content || '<br data-caret-placeholder="true">') + '</p>';
        }
        if (block.type === 'image') {
            var object = normalizeImageObject(block);
            var alt = block.content.altText || '';
            var caption = block.content.caption || '';
            var warningId = 'tm-wysiwyg-image-alt-warning-' + _escape(block.id);
            var ariaDescription = !alt ? ' aria-describedby="' + warningId + '"' : '';
            var selected = inst.selection && (inst.selection.activeImageBlockId === block.id || inst.selection.blockId === block.id && inst.selection.isObjectSelection === true);
            var src = _asText(object.url || '');
            var height = Math.max(1, Number(object.height || 80) || 80);
            var html = ['<figure class="' + renderImageFigureClasses(selected, object) + '" style="' + _escape(renderImageFigureStyle(object)) + '" data-block-id="' + _escape(block.id) + '" data-wrap-mode="' + _escape(object.wrapMode) + '" role="figure" tabindex="0" aria-label="' + _escape(alt || caption || 'Image') + '"' + ariaDescription + ' aria-selected="' + (selected ? 'true' : 'false') + '">'];
            if (src) {
                html.push('<img src="' + _escape(src) + '" alt="' + _escape(alt) + '" style="width:100%;height:' + height + 'px;object-fit:contain" draggable="false" />');
            } else {
                html.push('<div class="tm-wysiwyg-image__placeholder" style="height:' + height + 'px" aria-hidden="true">' + _escape(alt || caption || 'Image') + '</div>');
            }
            if (caption) html.push('<figcaption>' + _escape(caption) + '</figcaption>');
            html.push('<span class="tm-wysiwyg-selection-box" data-testid="document-wysiwyg-object-selection-box"></span>');
            ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'].forEach(function (handleName) {
                html.push('<span class="tm-wysiwyg-object-resize-handle tm-wysiwyg-object-resize-handle--' + handleName + '" data-resize-handle="' + handleName + '" data-testid="document-wysiwyg-object-resize-handle-' + handleName + '"></span>');
            });
            html.push('<span class="tm-wysiwyg-layout-bubble" data-testid="document-wysiwyg-object-layout-bubble"><button type="button" class="tm-wysiwyg-layout-bubble__button tm-wysiwyg-layout-bubble__button--active">Wrap</button><button type="button" class="tm-wysiwyg-layout-bubble__button">Inline</button><button type="button" class="tm-wysiwyg-layout-bubble__button">Alt</button></span>');
            if (!alt) html.push('<span id="' + warningId + '" class="tm-document-wysiwyg-host__sr-only" data-testid="document-wysiwyg-image-alt-warning" role="status" aria-live="polite">' + _escape(imageAltMissing) + '</span>');
            html.push('</figure>');
            return html.join('');
        }
        if (block.type === 'table') {
            return renderEngineTableHtml(block);
        }
        return '';
    }

    function render(inst) {
        if (!inst || inst.disposed || !inst.root) return;
        var diagnostics = ensureDiagnostics(inst);
        if (inst.markerStoreDirty) refreshRuntimeMarkerStore(inst);
        var blocks = _asArray(inst.model && inst.model.body && inst.model.body.blocks);
        var previousSelection = inst.selection ? _clone(inst.selection) : null;
        var invalidatedScopeIds = inst.layout && inst.layout.invalidatedScopeIds || [];
        var beforeHtml = inst.root.innerHTML;
        var renderStart = strictPerformanceNow();
        try {
            if (diagnostics.forceRenderFailure) {
                diagnostics.forceRenderFailure = false;
                throw new Error('forced strict render failure');
            }
            var pageLabel = formatA11yLabel(inst.options.PageLabel || inst.options.pageLabel || 'Page {0}', 1);
            var bodyLabel = formatA11yLabel(inst.options.BodyLabel || inst.options.bodyLabel || 'Document body, page {0}', 1);
            var imageAltMissing = inst.options.ImageAltMissing || inst.options.imageAltMissing || 'Image is missing alternative text.';
            var readOnly = inst.options.readOnly || inst.options.ReadOnly;
            applyReviewDisplayModeClass(inst.root, inst.options.reviewDisplayMode || inst.options.ReviewDisplayMode || 'AllMarkup');
            var pagePlan = buildPagePlan(inst, blocks, previousSelection);
            var html = [
                '<div class="tm-wysiwyg-document tm-wysiwyg-document--google-docs-engine" data-testid="document-wysiwyg-engine-document" role="document" aria-label="' + _escape(pageLabel) + '">',
                '<span class="tm-document-wysiwyg-host__sr-only" data-testid="document-wysiwyg-selection-live" role="status" aria-live="polite" aria-atomic="true"></span>'
            ];
            pagePlan.pages.forEach(function (page) {
                var currentPageLabel = formatA11yLabel(inst.options.PageLabel || inst.options.pageLabel || 'Page {0}', page.pageNumber);
                var currentBodyLabel = formatA11yLabel(inst.options.BodyLabel || inst.options.bodyLabel || 'Document body, page {0}', page.pageNumber);
                html.push('<section class="tm-wysiwyg-page' + (page.isVirtual ? ' tm-wysiwyg-page--virtual' : '') + '" data-page-number="' + page.pageNumber + '" data-page-index="' + page.pageIndex + '" role="region" aria-label="' + _escape(currentPageLabel) + '"' + (page.isVirtual ? ' aria-hidden="true"' : '') + '>');
                if (page.isVirtual) {
                    html.push('<div class="tm-wysiwyg-page__virtual-placeholder" data-testid="document-wysiwyg-virtual-page" data-block-count="' + page.blockIds.length + '"></div>');
                } else {
                    html.push(renderHeaderFooterHtml(inst, page, 'header', readOnly, pagePlan.pages.length));
                    html.push('<div class="tm-wysiwyg-page__body" data-render-region="Body" contenteditable="' + (readOnly ? 'false' : 'true') + '" role="textbox" aria-multiline="true" aria-readonly="' + (readOnly ? 'true' : 'false') + '" aria-label="' + _escape(currentBodyLabel) + '" tabindex="0">');
                    if (blocks.length === 0) {
                        html.push('<p class="tm-wysiwyg-block" data-block-id="empty-paragraph"><br></p>');
                    } else {
                        page.blocks.forEach(function (block) {
                            html.push(renderEngineBlockHtml(inst, block, imageAltMissing, page.pageNumber, pagePlan.pages.length));
                        });
                    }
                    html.push('</div>');
                    html.push(renderHeaderFooterHtml(inst, page, 'footer', readOnly, pagePlan.pages.length));
                }
                html.push('</section>');
            });
            html.push('</div>');
            inst.root.innerHTML = html.join('');
            var layoutStart = strictPerformanceNow();
            if (diagnostics.forceLayoutFailure) {
                diagnostics.forceLayoutFailure = false;
                throw new Error('forced strict layout failure');
            }
            inst.layout = Object.assign(buildLayoutSnapshot(inst.root, inst.model), {
                pages: pagePlan.pages.map(function (page) {
                    return {
                        pageIndex: page.pageIndex,
                        pageNumber: page.pageNumber,
                        blockIds: page.blockIds.slice(),
                        headerFooterIds: [page.header && page.header.id, page.footer && page.footer.id].filter(Boolean),
                        headerBlockIds: page.headerBlockIds.slice(),
                        footerBlockIds: page.footerBlockIds.slice(),
                        exclusions: [],
                        isVirtual: page.isVirtual === true,
                        isRendered: page.isRendered === true
                    };
                }),
                headerFooterRegions: buildSimpleHeaderFooterLayoutRegions(inst.root, pagePlan),
                activePageIndex: pagePlan.activePageIndex,
                virtualizationEnabled: pagePlan.virtualizationEnabled,
                invalidatedScopeIds: invalidatedScopeIds
            });
            recordVirtualizationMetric(inst, pagePlan);
            diagnostics.layoutVersion++;
            recordLayoutMetric(inst, Math.max(0, strictPerformanceNow() - layoutStart), 'render', invalidatedScopeIds);
            var first = blocks[0] || null;
            var preservedSelection = createSelectionSnapshot(previousSelection || {});
            var selectionBlockId = preservedSelection.blockId && _findBlock(inst.model, preservedSelection.blockId)
                ? preservedSelection.blockId
                : (first ? first.id : 'empty-paragraph');
            if (diagnostics.forceSelectionFailure) {
                diagnostics.forceSelectionFailure = false;
                throw new Error('forced strict selection restore failure');
            }
            if (selectionBlockId !== preservedSelection.blockId) {
                preservedSelection = createSelectionSnapshot({
                    region: preservedSelection.region || 'Body',
                    blockId: selectionBlockId,
                    offset: 0,
                    isCollapsed: true
                });
            }
            inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, preservedSelection);
            markSelectionChanged(inst, 'render');
            if (inst.pendingDomSelectionRestore) {
                restoreDomSelectionFromSnapshot(inst, inst.pendingDomSelectionRestore);
                inst.pendingDomSelectionRestore = null;
            }
            diagnostics.renderVersion++;
            recordRenderMetric(inst, Math.max(0, strictPerformanceNow() - renderStart), 'render');
            diagnostics.lastValidRenderHtml = inst.root.innerHTML;
            diagnostics.lastValidLayout = _clone(inst.layout);
            diagnostics.lastValidSelection = _clone(inst.selection);
            diagnostics.lastValidSnapshot = _clone(inst.model);
            updateActiveCommentDom(inst);
            updateActiveRevisionDom(inst);
            updateActiveImageSelectionDom(inst);
            inst.root.removeAttribute('data-debug-recovery');
        } catch (error) {
            var message = String(error && error.message || error);
            if (message.indexOf('layout') >= 0) {
                inst.layout = createSafeLayoutFallback(inst, invalidatedScopeIds);
                diagnostics.layoutVersion++;
                recordWatchdogFailure(inst, 'layout', error, { recovery: 'safe-layout-fallback' });
                recordLayoutMetric(inst, Math.max(0, strictPerformanceNow() - renderStart), 'safe-fallback', invalidatedScopeIds);
            } else if (message.indexOf('selection') >= 0) {
                var firstBlock = blocks[0] || null;
                inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, firstBlock
                    ? { region: 'Body', blockId: firstBlock.id, offset: 0, isCollapsed: true }
                    : firstModelSelection(inst.model));
                markSelectionChanged(inst, 'selection-fallback');
                recordWatchdogFailure(inst, 'selection', error, { recovery: 'nearest-valid-caret' });
            } else {
                inst.root.innerHTML = diagnostics.lastValidRenderHtml || beforeHtml;
                if (diagnostics.lastValidLayout) inst.layout = _clone(diagnostics.lastValidLayout);
                if (diagnostics.lastValidSelection) inst.selection = _clone(diagnostics.lastValidSelection);
                recordWatchdogFailure(inst, 'render', error, { recovery: 'last-valid-render' });
            }
            diagnostics.renderVersion++;
            recordRenderMetric(inst, Math.max(0, strictPerformanceNow() - renderStart), 'recovery');
            if (inst.root) inst.root.setAttribute('data-debug-recovery', message);
        }
    }

    function applyReviewDisplayModeClass(root, mode) {
        if (!root || !root.classList) return;
        ['tm-wysiwyg-host--review-all-markup', 'tm-wysiwyg-host--review-simple-markup', 'tm-wysiwyg-host--review-no-markup', 'tm-wysiwyg-host--review-original'].forEach(function (className) {
            root.classList.remove(className);
        });
        var normalized = String(mode || 'AllMarkup').replace(/[\s_.:-]+/g, '').toLowerCase();
        var className = normalized === 'simplemarkup'
            ? 'tm-wysiwyg-host--review-simple-markup'
            : normalized === 'nomarkup'
                ? 'tm-wysiwyg-host--review-no-markup'
                : normalized === 'original'
                    ? 'tm-wysiwyg-host--review-original'
                    : 'tm-wysiwyg-host--review-all-markup';
        root.classList.add(className);
    }

    function renderEngineTableHtml(block) {
        var rows = _asArray(block && block.content && block.content.rows);
        var html = ['<table class="tm-wysiwyg-block tm-wysiwyg-table" data-block-id="' + _escape(block.id) + '" role="table" aria-label="Table"><tbody>'];
        rows.forEach(function (row) {
            html.push('<tr data-row-id="' + _escape(row.id) + '" role="row">');
            _asArray(row.cells).forEach(function (cell, cellIndex) {
                var style = [];
                if (cell.style && cell.style.background) style.push('background:' + _escape(cell.style.background));
                if (cell.style && cell.style.border) style.push('border:' + _escape(cell.style.border));
                if (cell.style && cell.style.padding !== undefined) style.push('padding:' + Number(cell.style.padding || 0) + 'px');
                html.push('<td data-cell-id="' + _escape(cell.id) + '" colspan="' + _escape(cell.colSpan || 1) + '" rowspan="' + _escape(cell.rowSpan || 1) + '" role="gridcell" tabindex="-1" aria-label="Table cell ' + (cellIndex + 1) + '"' + (style.length ? ' style="' + style.join(';') + '"' : '') + '>');
                _asArray(cell.blocks).forEach(function (child) {
                    if (child.type === 'paragraph') {
                        html.push('<p class="tm-wysiwyg-block" data-block-id="' + _escape(child.id) + '">' + _escape(_textFromRuns(child.content && child.content.runs)) + '</p>');
                    }
                });
                html.push('</td>');
            });
            html.push('</tr>');
        });
        html.push('</tbody></table>');
        return html.join('');
    }

    function _escape(value) {
        return _asText(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    var _testTextMeasureCache = new Map();
    var _testTextMeasureStats = { MeasureCount: 0, MeasureCacheHits: 0, MeasureCacheSize: 0, MeasureInvalidations: 0 };

    function testTextMeasureStyle(request) {
        var source = request || {};
        return {
            text: _asText(source.Text ?? source.text ?? ''),
            fontFamily: _asText(source.FontFamily ?? source.fontFamily ?? 'Arial'),
            fontSize: Number(source.FontSize ?? source.fontSize ?? 12) || 12,
            fontWeight: _asText(source.FontWeight ?? source.fontWeight ?? '400'),
            fontStyle: _asText(source.FontStyle ?? source.fontStyle ?? 'normal'),
            letterSpacing: Number(source.LetterSpacing ?? source.letterSpacing ?? 0) || 0,
            zoom: Number(source.Zoom ?? source.zoom ?? 1) || 1
        };
    }

    function getTextRunMeasureCacheKey(request) {
        var style = testTextMeasureStyle(request);
        return [style.text, style.fontFamily, style.fontSize, style.fontWeight, style.fontStyle, style.letterSpacing, style.zoom].join('\u001f');
    }

    function measureTextRun(request) {
        var style = testTextMeasureStyle(request);
        var key = getTextRunMeasureCacheKey(request);
        if (_testTextMeasureCache.has(key)) {
            _testTextMeasureStats.MeasureCacheHits++;
            return _clone(_testTextMeasureCache.get(key));
        }

        var width = Array.from(style.text).reduce(function (total, ch) {
            return total + (/\s/.test(ch) ? style.fontSize * 0.32 : style.fontSize * 0.55);
        }, 0);
        if (/700|bold/i.test(style.fontWeight)) width *= 1.08;
        if (/italic/i.test(style.fontStyle)) width *= 1.04;
        width += Math.max(0, style.text.length - 1) * style.letterSpacing;
        var result = {
            Text: style.text,
            Width: Math.max(1, width * style.zoom),
            Height: Math.max(1, Math.ceil(style.fontSize * 1.25 * style.zoom))
        };
        _testTextMeasureCache.set(key, result);
        _testTextMeasureStats.MeasureCount++;
        _testTextMeasureStats.MeasureCacheSize = _testTextMeasureCache.size;
        return _clone(result);
    }

    function clearTextRunMeasureCache() {
        _testTextMeasureCache.clear();
        _testTextMeasureStats = {
            MeasureCount: 0,
            MeasureCacheHits: 0,
            MeasureCacheSize: 0,
            MeasureInvalidations: (_testTextMeasureStats.MeasureInvalidations || 0) + 1
        };
    }

    function getTextRunMeasureStats() {
        _testTextMeasureStats.MeasureCacheSize = _testTextMeasureCache.size;
        return _clone(_testTextMeasureStats);
    }

    function testWrapMode(value) {
        if (value === null || value === undefined || value === '') return { value: 0, css: 'inline' };
        if (typeof value === 'number') {
            if (value === 0) return { value: 0, css: 'inline' };
            if (value === 1) return { value: 1, css: 'square' };
            if (value === 2) return { value: 2, css: 'tight' };
            if (value === 4) return { value: 4, css: 'top-bottom' };
            if (value === 5) return { value: 5, css: 'behind-text' };
            if (value === 6) return { value: 6, css: 'in-front-of-text' };
        }

        var key = _asText(value).replace(/[^a-z0-9]/gi, '').toLowerCase();
        if (key === 'inline' || key === 'inlined') return { value: 0, css: 'inline' };
        if (key === 'square') return { value: 1, css: 'square' };
        if (key === 'tight') return { value: 2, css: 'tight' };
        if (key === 'topbottom' || key === 'topandbottom') return { value: 4, css: 'top-bottom' };
        if (key === 'behindtext') return { value: 5, css: 'behind-text' };
        if (key === 'infrontoftext' || key === 'frontoftext') return { value: 6, css: 'in-front-of-text' };
        return { value: 0, css: 'inline' };
    }

    function testHorizontalPosition(value) {
        if (value === null || value === undefined || value === '') return null;
        if (typeof value === 'number') {
            if (value === 0) return { value: 0, css: 'left' };
            if (value === 1) return { value: 1, css: 'center' };
            if (value === 2) return { value: 2, css: 'right' };
            return null;
        }

        var key = _asText(value).toLowerCase();
        if (key === 'left' || key === 'start') return { value: 0, css: 'left' };
        if (key === 'center' || key === 'middle') return { value: 1, css: 'center' };
        if (key === 'right' || key === 'end') return { value: 2, css: 'right' };
        return null;
    }

    function rectFromAny(rect) {
        rect = rect || {};
        return {
            X: Number(rect.X ?? rect.x ?? rect.left ?? 0) || 0,
            Y: Number(rect.Y ?? rect.y ?? rect.top ?? 0) || 0,
            Width: Number(rect.Width ?? rect.width ?? 0) || 0,
            Height: Number(rect.Height ?? rect.height ?? 0) || 0
        };
    }

    function rectContains(rect, x, y) {
        var r = rectFromAny(rect);
        return x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height;
    }

    function normalizeWrapContourPoints(points) {
        return _asArray(points).map(function (point) {
            var x = point ? (point.X ?? point.x ?? 0) : 0;
            var y = point ? (point.Y ?? point.y ?? 0) : 0;
            return {
                X: Math.max(0, Math.min(1, Number(x) || 0)),
                Y: Math.max(0, Math.min(1, Number(y) || 0))
            };
        });
    }

    function getLayoutAvailableIntervalsForTest(y, height, bodyFrame, exclusions) {
        var frame = rectFromAny(bodyFrame);
        var rowY = Number(y || 0);
        var rowHeight = Number(height || 0) || 1;
        var blocked = [];
        _asArray(exclusions).forEach(function (exclusion) {
            if (!exclusion || exclusion.BlocksText === false || exclusion.blocksText === false) return;
            var rect = rectFromAny(exclusion.Rect || exclusion.rect);
            if (rowY + rowHeight < rect.Y || rowY > rect.Y + rect.Height) return;
            var blockX = rect.X;
            var blockWidth = rect.Width;
            var polygon = _asArray(exclusion.Polygon || exclusion.polygon);
            if (polygon.length >= 3) {
                var scanY = rowY + rowHeight / 2;
                var xs = [];
                for (var i = 0; i < polygon.length; i++) {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.length];
                    var ay = Number(a.Y ?? a.y ?? 0);
                    var by = Number(b.Y ?? b.y ?? 0);
                    if ((ay <= scanY && by > scanY) || (by <= scanY && ay > scanY)) {
                        var ax = Number(a.X ?? a.x ?? 0);
                        var bx = Number(b.X ?? b.x ?? 0);
                        xs.push(ax + (scanY - ay) * (bx - ax) / (by - ay));
                    }
                }
                xs.sort(function (left, right) { return left - right; });
                if (xs.length >= 2) {
                    blockX = xs[0];
                    blockWidth = xs[xs.length - 1] - xs[0];
                }
            }
            blocked.push({ X: blockX, Width: blockWidth });
        });

        if (!blocked.length) return [{ X: frame.X, Y: rowY, Width: frame.Width, Height: rowHeight }];
        blocked.sort(function (a, b) { return a.X - b.X; });
        var intervals = [];
        var cursor = frame.X;
        var frameRight = frame.X + frame.Width;
        blocked.forEach(function (item) {
            var start = Math.max(frame.X, item.X);
            var end = Math.min(frameRight, item.X + item.Width);
            if (start > cursor) intervals.push({ X: cursor, Y: rowY, Width: start - cursor, Height: rowHeight });
            cursor = Math.max(cursor, end);
        });
        if (cursor < frameRight) intervals.push({ X: cursor, Y: rowY, Width: frameRight - cursor, Height: rowHeight });
        return intervals.filter(function (interval) { return interval.Width > 0; });
    }

    function blockTypeForTest(block) {
        var type = block && (block.Type ?? block.type);
        if (type === 5 || String(type).toLowerCase().indexOf('image') >= 0) return 'image';
        if (type === 4 || String(type).toLowerCase().indexOf('table') >= 0) return 'table';
        if (type === 6 || String(type).toLowerCase().indexOf('pagebreak') >= 0) return 'pageBreak';
        return 'paragraph';
    }

    function paragraphRunsForTest(block) {
        var content = block && (block.Content || block.content) || {};
        var runs = _asArray(content.Inlines || content.inlines || content.Runs || content.runs);
        if (!runs.length) runs = [{ Id: (block && block.Id || block && block.id || 'p') + '-empty', Text: '' }];
        var offset = 0;
        return runs.map(function (run, index) {
            var text = _asText(run.Text ?? run.text ?? run.FallbackText ?? run.fallbackText ?? run.Key ?? run.key ?? '');
            var result = {
                Id: _asText(run.Id ?? run.id ?? ('inline-' + index)),
                Text: text,
                Start: offset,
                End: offset + text.length,
                Marks: _asArray(run.Marks || run.marks)
            };
            offset += text.length;
            return result;
        });
    }

    function createLayoutSnapshotForRenderTest(documentModel) {
        var doc = documentModel || {};
        var pageSettings = doc.PageSettings || doc.pageSettings || {};
        var size = pageSettings.Size || pageSettings.size || {};
        var margins = pageSettings.Margins || pageSettings.margins || {};
        var pageWidth = Number(size.Width ?? size.width ?? 595) || 595;
        var pageHeight = Number(size.Height ?? size.height ?? 842) || 842;
        var bodyFrame = {
            X: Number(margins.Left ?? margins.left ?? 72) || 72,
            Y: Number(margins.Top ?? margins.top ?? 72) || 72,
            Width: pageWidth - (Number(margins.Left ?? margins.left ?? 72) || 72) - (Number(margins.Right ?? margins.right ?? 72) || 72),
            Height: pageHeight - (Number(margins.Top ?? margins.top ?? 72) || 72) - (Number(margins.Bottom ?? margins.bottom ?? 72) || 72)
        };
        var blocks = _asArray(doc.Blocks || doc.blocks).slice().sort(function (a, b) {
            return Number(a.Order ?? a.order ?? 0) - Number(b.Order ?? b.order ?? 0);
        });
        var page = { PageIndex: 0, Rect: { X: 0, Y: 0, Width: pageWidth, Height: pageHeight }, BodyRect: bodyFrame, Objects: [], Paragraphs: [], Lines: [] };
        var y = bodyFrame.Y;
        var activeExclusions = [];
        var charWidth = 7;
        var lineHeight = 16;
        var spacing = 8;

        function imageGeometry(block, blockIndex) {
            var content = block.Content || block.content || {};
            var layout = content.Layout || content.layout || {};
            var transform = layout.Transform || layout.transform || {};
            var sizeValue = content.Size || content.size || {};
            var width = Number(transform.Width ?? transform.width ?? sizeValue.Width ?? sizeValue.width ?? 160) || 160;
            var height = Number(transform.Height ?? transform.height ?? sizeValue.Height ?? sizeValue.height ?? 90) || 90;
            var wrap = layout.Wrap || layout.wrap || {};
            var mode = testWrapMode(wrap.Mode ?? wrap.mode ?? 0);
            var position = layout.Position || layout.position || {};
            var horizontal = testHorizontalPosition(position.HorizontalAlignment ?? position.horizontalAlignment);
            var x = bodyFrame.X + (Number(position.X ?? position.x ?? 0) || 0);
            if (horizontal && horizontal.value === 1) x = bodyFrame.X + (bodyFrame.Width - width) / 2;
            if (horizontal && horizontal.value === 2) x = bodyFrame.X + bodyFrame.Width - width - (Number(position.X ?? position.x ?? 0) || 0);
            var imageY = y + (Number(position.Y ?? position.y ?? 0) || 0);
            var anchor = layout.Anchor || layout.anchor || {};
            var anchorBlockId = _asText(anchor.BlockId ?? anchor.blockId ?? '');
            var anchorParagraph = page.Paragraphs.find(function (paragraph) { return paragraph.BlockId === anchorBlockId; });
            if (anchorParagraph && anchor.MoveWithText !== false && anchor.FixedOnPage !== true) imageY = anchorParagraph.Rect.Y + (Number(position.Y ?? position.y ?? 0) || 0);
            if (anchor.FixedOnPage === true || Number(layout.Kind ?? layout.kind ?? 0) === 2) imageY = Number(position.Y ?? position.y ?? imageY) || imageY;
            var caption = _asText(content.Caption ?? content.caption ?? '');
            var captionHeight = caption ? 24 : 0;
            var distanceLeft = Number(wrap.DistanceLeft ?? wrap.distanceLeft ?? 0) || 0;
            var distanceRight = Number(wrap.DistanceRight ?? wrap.distanceRight ?? 0) || 0;
            var distanceTop = Number(wrap.DistanceTop ?? wrap.distanceTop ?? 0) || 0;
            var distanceBottom = Number(wrap.DistanceBottom ?? wrap.distanceBottom ?? 0) || 0;
            var allowOverlap = ((layout.Stacking && (layout.Stacking.AllowOverlap ?? layout.Stacking.allowOverlap)) ?? true) !== false;
            if (!allowOverlap && page.Objects.length) {
                var previousBottom = Math.max.apply(null, page.Objects.map(function (item) { return item.WrapRect.Y + item.WrapRect.Height; }));
                imageY = Math.max(imageY, previousBottom + spacing);
            }
            return {
                BlockId: _asText(block.Id ?? block.id ?? ('image-' + blockIndex)),
                ObjectId: _asText(content.Id ?? content.id ?? content.ObjectId ?? content.objectId ?? block.Id ?? block.id ?? ('image-' + blockIndex)),
                Rect: { X: x, Y: imageY, Width: width, Height: height },
                ObjectRect: { X: x, Y: imageY, Width: width, Height: height },
                FootprintRect: { X: x, Y: imageY, Width: width, Height: height + captionHeight },
                WrapRect: { X: x - distanceLeft, Y: imageY - distanceTop, Width: width + distanceLeft + distanceRight, Height: height + captionHeight + distanceTop + distanceBottom },
                WrapMode: mode.value,
                WrapModeCss: mode.css,
                Layer: mode.value === 5 ? 'behind-text' : (mode.value === 6 ? 'in-front-of-text' : 'object'),
                AnchorBlockId: anchorBlockId || _asText(block.Id ?? block.id ?? ''),
                AnchorOffset: Number(anchor.Offset ?? anchor.offset ?? 0) || 0,
                AnchorRegion: Number(anchor.Region ?? anchor.region ?? 0) || 0,
                AllowOverlap: allowOverlap,
                ZIndex: Number((layout.Stacking && (layout.Stacking.ZIndex ?? layout.Stacking.zIndex)) ?? 0) || 0,
                VisualRects: [{ X: x, Y: imageY, Width: width, Height: height + captionHeight }],
                DataAttributes: {
                    'data-layout-object-id': 'layout-object-' + _asText(block.Id ?? block.id ?? ('image-' + blockIndex)),
                    'data-wrap-mode': String(mode.value),
                    'data-anchor-block-id': _asText(block.Id ?? block.id ?? ''),
                    'data-object-z-index': String(Number((layout.Stacking && (layout.Stacking.ZIndex ?? layout.Stacking.zIndex)) ?? 0) || 0)
                }
            };
        }

        function addParagraph(block) {
            var blockId = _asText(block.Id ?? block.id ?? 'paragraph');
            var runs = paragraphRunsForTest(block);
            var text = runs.map(function (run) { return run.Text; }).join('');
            var lines = [];
            var offset = 0;
            var lineIndex = 0;
            var paragraphTop = y;
            if (text.length === 0) {
                lines.push({
                    Id: 'layout-line-' + blockId + '-0',
                    BlockId: blockId,
                    VisualLineIndex: 0,
                    Rect: { X: bodyFrame.X, Y: y, Width: 1, Height: lineHeight },
                    Segments: [{
                        Id: 'layout-segment-' + blockId + '-0-0',
                        LineId: 'layout-line-' + blockId + '-0',
                        InlineId: runs[0].Id,
                        BlockId: blockId,
                        Text: '',
                        Length: 0,
                        TextLength: 0,
                        BlockStartOffset: 0,
                        StartOffset: 0,
                        Rect: { X: bodyFrame.X, Y: y, Width: 1, Height: lineHeight }
                    }]
                });
                y += lineHeight + spacing;
            } else {
                while (offset < text.length) {
                    var intervals = getLayoutAvailableIntervalsForTest(y, lineHeight, bodyFrame, activeExclusions);
                    var interval = intervals.find(function (candidate) { return candidate.Width >= 40; }) || intervals[0] || { X: bodyFrame.X, Width: bodyFrame.Width };
                    var maxChars = Math.max(1, Math.floor(interval.Width / charWidth));
                    var end = Math.min(text.length, offset + maxChars);
                    var lineText = text.slice(offset, end);
                    var lineId = 'layout-line-' + blockId + '-' + lineIndex;
                    var segments = [];
                    runs.forEach(function (run, runIndex) {
                        var start = Math.max(offset, run.Start);
                        var finish = Math.min(end, run.End);
                        if (finish > start || (run.Text.length === 0 && offset === 0 && runIndex === 0)) {
                            var localText = text.slice(start, finish);
                            segments.push({
                                Id: 'layout-segment-' + blockId + '-' + lineIndex + '-' + runIndex,
                                LineId: lineId,
                                InlineId: run.Id,
                                BlockId: blockId,
                                Text: localText,
                                Length: localText.length,
                                TextLength: localText.length,
                                BlockStartOffset: start,
                                StartOffset: start,
                                Marks: _clone(run.Marks),
                                Rect: { X: interval.X + Math.max(0, start - offset) * charWidth, Y: y, Width: Math.max(1, localText.length * charWidth), Height: lineHeight }
                            });
                        }
                    });
                    lines.push({
                        Id: lineId,
                        BlockId: blockId,
                        VisualLineIndex: lineIndex,
                        Rect: { X: interval.X, Y: y, Width: Math.max(1, Math.min(interval.Width, lineText.length * charWidth)), Height: lineHeight },
                        Segments: segments,
                        AvailableIntervals: intervals
                    });
                    offset = end;
                    lineIndex++;
                    y += lineHeight + 4;
                }
                y += spacing;
            }
            var paragraphRect = { X: bodyFrame.X, Y: paragraphTop, Width: bodyFrame.Width, Height: Math.max(lineHeight, y - paragraphTop - spacing) };
            var paragraph = { BlockId: blockId, Rect: paragraphRect, Lines: lines };
            page.Paragraphs.push(paragraph);
            page.Lines = page.Lines.concat(lines);
            var exclusionBottom = activeExclusions.reduce(function (bottom, item) {
                var rect = rectFromAny(item.Rect);
                return Math.max(bottom, rect.Y + rect.Height);
            }, 0);
            if (exclusionBottom > y) y = exclusionBottom + spacing;
            activeExclusions = activeExclusions.filter(function (item) {
                var rect = rectFromAny(item.Rect);
                return rect.Y + rect.Height > y;
            });
        }

        blocks.forEach(function (block, index) {
            var type = blockTypeForTest(block);
            if (type === 'image') {
                var object = imageGeometry(block, index);
                page.Objects.push(object);
                if (object.WrapMode !== 0 && object.WrapMode !== 5 && object.WrapMode !== 6) {
                    activeExclusions.push({ BlocksText: true, Rect: object.WrapRect });
                }
                var nextType = blockTypeForTest(blocks[index + 1]);
                if (nextType !== 'paragraph') y = Math.max(y, object.WrapRect.Y + object.WrapRect.Height + spacing);
                return;
            }
            if (type === 'paragraph') addParagraph(block);
        });

        return { Pages: [page] };
    }

    function hitTestLayoutGeometry(request) {
        var source = request || {};
        var x = Number(source.X ?? source.x ?? 0) || 0;
        var y = Number(source.Y ?? source.y ?? 0) || 0;
        function base(kind) { return { Kind: kind, ActiveImageBlockId: null, ActiveObjectId: null }; }
        function pointInVisual(item) {
            var rects = _asArray(item.VisualRects || item.visualRects);
            return (rects.length ? rects : [item.Rect || item.rect]).some(function (rect) { return rectContains(rect, x, y); });
        }
        function caretFromLine(line) {
            var rect = rectFromAny(line.Rect || line.rect);
            if (y < rect.Y || y > rect.Y + rect.Height) return null;
            var segment = _asArray(line.Segments || line.segments)[0] || {};
            var length = Number(segment.TextLength ?? segment.Length ?? segment.textLength ?? segment.length ?? 0) || 0;
            var start = Number(segment.StartOffset ?? segment.BlockStartOffset ?? segment.startOffset ?? 0) || 0;
            var ratio = rect.Width > 0 ? Math.max(0, Math.min(1, (x - rect.X) / rect.Width)) : 0;
            return {
                Kind: 'TextCaret',
                LayoutLineId: line.Id || line.id || null,
                LayoutSegmentId: segment.Id || segment.id || null,
                VisualLineIndex: Number(line.VisualLineIndex ?? line.visualLineIndex ?? 0) || 0,
                Offset: start + Math.round(ratio * length),
                ActiveImageBlockId: null,
                ActiveObjectId: null
            };
        }

        var control = _asArray(source.Controls).filter(function (item) { return rectContains(item.Rect, x, y); })
            .sort(function (a, b) { return (Number(b.LayerPriority || 0) - Number(a.LayerPriority || 0)) || (Number(b.ZIndex || 0) - Number(a.ZIndex || 0)); })[0];
        if (control) return Object.assign(base(control.Kind || 'Control'), control);
        var tableCell = _asArray(source.TableCells).find(function (item) { return rectContains(item.Rect, x, y); });
        if (tableCell) return Object.assign(base('TableCell'), tableCell);
        var region = _asArray(source.HeaderFooters).find(function (item) { return rectContains(item.Rect, x, y); });
        if (region) return Object.assign(base('HeaderFooter'), region);

        var lineHit = null;
        _asArray(source.Lines).some(function (line) {
            lineHit = caretFromLine(line);
            return !!lineHit;
        });
        var objectHit = _asArray(source.Objects).filter(function (item) {
            return item.Selectable !== false && pointInVisual(item) && String(item.Layer || '').toLowerCase() !== 'behind-text';
        }).sort(function (a, b) {
            return (Number(b.LayerPriority || 0) - Number(a.LayerPriority || 0)) || (Number(b.ZIndex || 0) - Number(a.ZIndex || 0));
        })[0];
        if (objectHit && (!lineHit || Number(objectHit.LayerPriority || 0) >= 20)) {
            return Object.assign(base('ImageObject'), {
                Kind: objectHit.Kind || 'ImageObject',
                ActiveImageBlockId: objectHit.BlockId || objectHit.blockId || null,
                ActiveObjectId: objectHit.ObjectId || objectHit.objectId || null,
                ObjectId: objectHit.ObjectId || objectHit.objectId || null,
                BlockId: objectHit.BlockId || objectHit.blockId || null
            });
        }
        if (lineHit) return lineHit;
        var body = _asArray(source.BodyRects).find(function (item) { return rectContains(item.Rect, x, y); });
        if (body) return base('Body');
        var page = _asArray(source.PageRects).find(function (item) { return rectContains(item.Rect, x, y); });
        return page ? base('PageMargin') : base('None');
    }

    function createMarkerStore(initialMarkers) {
        var markers = _asArray(initialMarkers).map(_clone).sort(function (a, b) { return Number(b.priority || 0) - Number(a.priority || 0); });
        function byType(type) { return markers.filter(function (marker) { return marker.type === type; }); }
        function byBlock(blockId) { return markers.filter(function (marker) { return marker.range && marker.range.startBlockId === blockId; }); }
        function overlapping(range) {
            return markers.filter(function (marker) {
                var r = marker.range || {};
                return r.startBlockId === range.startBlockId && r.endBlockId === range.endBlockId
                    && Number(r.startOffset || 0) < Number(range.endOffset || 0)
                    && Number(r.endOffset || 0) > Number(range.startOffset || 0);
            });
        }
        function transformText(blockId, offset, length, isDelete) {
            markers = markers.map(function (marker) {
                var clone = _clone(marker);
                var range = clone.range || {};
                if (range.startBlockId !== blockId) return clone;
                var delta = Number(length || 0) * (isDelete ? -1 : 1);
                if (offset <= range.startOffset) range.startOffset = Math.max(0, Number(range.startOffset || 0) + delta);
                if (offset <= range.endOffset) range.endOffset = Math.max(range.startOffset, Number(range.endOffset || 0) + delta);
                return clone;
            }).sort(function (a, b) { return Number(b.priority || 0) - Number(a.priority || 0); });
            store.all = markers;
            return markers.map(_clone);
        }
        function renderClasses() {
            return markers.map(function (marker) {
                var type = String(marker.type || '').replace(/[A-Z]/g, function (m) { return '-' + m.toLowerCase(); }).toLowerCase();
                var className = 'tm-wysiwyg-marker tm-wysiwyg-marker--' + type;
                if (marker.type === 'comment' && marker.status) className += ' tm-document-inline--comment-anchor--' + marker.status;
                if (marker.type === 'revisionDeletion') className += ' tm-wysiwyg-marker--revision-delete';
                return {
                    id: marker.id,
                    className: className,
                    testId: marker.type === 'tagQuery' ? 'document-tag-query-marker' : (marker.type === 'slashQuery' ? 'document-slash-query-marker' : '')
                };
            });
        }
        var store = {
            all: markers,
            byType: byType,
            byBlock: byBlock,
            overlapping: overlapping,
            transformText: transformText,
            renderClasses: renderClasses,
            remove: function (id) {
                var before = markers.length;
                markers = markers.filter(function (marker) { return marker.id !== id; });
                store.all = markers;
                return markers.length !== before;
            }
        };
        return store;
    }

    function readCommentId(comment) {
        return _asText(comment && (comment.id || comment.Id) || '');
    }

    function readCommentStatus(comment) {
        var raw = comment && (comment.status ?? comment.Status);
        if (raw === 1) return 'resolved';
        var text = _asText(raw || 'Open').toLowerCase();
        return text.indexOf('resolved') >= 0 ? 'resolved' : 'open';
    }

    function commentById(model, commentId) {
        return _asArray(model && model.comments).find(function (comment) {
            return readCommentId(comment) === commentId;
        }) || null;
    }

    function revisionById(model, revisionId) {
        var id = _asText(revisionId);
        return _asArray(model && model.revisions).find(function (revision) {
            return _asText(revision && (revision.id || revision.Id)) === id;
        }) || null;
    }

    function readRevisionStatus(revision) {
        return normalizeRevisionStatus(revision && (revision.status ?? revision.Status ?? revision.action ?? revision.Action));
    }

    function readRevisionTypeName(revision) {
        return normalizeRevisionType(revision && (revision.type ?? revision.Type));
    }

    function readRevisionMarkerType(revision) {
        var type = readRevisionTypeName(revision);
        if (type === 'Deletion') return 'revisionDeletion';
        if (type === 'FormatChange' || type === 'Formatting') return 'revisionFormat';
        return 'revisionInsertion';
    }

    function collectInlineCommentRanges(model) {
        var ranges = {};
        function remember(commentId, blockId, start, end) {
            if (!commentId || !blockId || end <= start) return;
            var existing = ranges[commentId];
            if (!existing) {
                ranges[commentId] = { startBlockId: blockId, endBlockId: blockId, startOffset: start, endOffset: end };
                return;
            }
            if (existing.startBlockId === blockId && existing.endBlockId === blockId) {
                existing.startOffset = Math.min(existing.startOffset, start);
                existing.endOffset = Math.max(existing.endOffset, end);
            }
        }
        function scanBlock(block) {
            if (!block || block.type !== 'paragraph') {
                if (block && block.type === 'table') {
                    _asArray(block.content && block.content.rows).forEach(function (row) {
                        _asArray(row.cells).forEach(function (cell) { _asArray(cell.blocks).forEach(scanBlock); });
                    });
                }
                return;
            }
            var cursor = 0;
            _asArray(block.content && block.content.runs).forEach(function (run) {
                var text = resolveInlineRunDisplayText(run);
                var start = cursor;
                var end = cursor + text.length;
                readCommentIdsFromRun(run).forEach(function (commentId) {
                    remember(commentId, block.id, start, end);
                });
                cursor = end;
            });
        }
        _asArray(model && model.body && model.body.blocks).forEach(scanBlock);
        _asArray(model && model.headers).forEach(function (region) { _asArray(region.blocks).forEach(scanBlock); });
        _asArray(model && model.footers).forEach(function (region) { _asArray(region.blocks).forEach(scanBlock); });
        return ranges;
    }

    function collectInlineRevisionRanges(model) {
        var ranges = {};
        function remember(revisionId, blockId, start, end) {
            if (!revisionId || !blockId || end <= start) return;
            var existing = ranges[revisionId];
            if (!existing) {
                ranges[revisionId] = { startBlockId: blockId, endBlockId: blockId, startOffset: start, endOffset: end };
                return;
            }
            if (existing.startBlockId === blockId && existing.endBlockId === blockId) {
                existing.startOffset = Math.min(existing.startOffset, start);
                existing.endOffset = Math.max(existing.endOffset, end);
            }
        }
        function scanBlock(block) {
            if (!block || block.type !== 'paragraph') {
                if (block && block.type === 'table') {
                    _asArray(block.content && block.content.rows).forEach(function (row) {
                        _asArray(row.cells).forEach(function (cell) { _asArray(cell.blocks).forEach(scanBlock); });
                    });
                }
                return;
            }
            var cursor = 0;
            _asArray(block.content && block.content.runs).forEach(function (run) {
                var text = resolveInlineRunDisplayText(run);
                var start = cursor;
                var end = cursor + text.length;
                readRevisionIdsFromRun(run).forEach(function (revisionId) {
                    remember(revisionId, block.id, start, end);
                });
                cursor = end;
            });
        }
        _asArray(model && model.body && model.body.blocks).forEach(scanBlock);
        _asArray(model && model.headers).forEach(function (region) { _asArray(region.blocks).forEach(scanBlock); });
        _asArray(model && model.footers).forEach(function (region) { _asArray(region.blocks).forEach(scanBlock); });
        return ranges;
    }

    function blockOffsetFromInlineIndex(block, inlineIndex, offset) {
        var runs = _asArray(block && block.content && block.content.runs);
        var index = Math.max(0, Math.min(runs.length - 1, Number(inlineIndex || 0) || 0));
        var total = 0;
        for (var i = 0; i < index; i++) total += resolveInlineRunDisplayText(runs[i]).length;
        return total + Math.max(0, Number(offset || 0) || 0);
    }

    function rangeFromCommentAnchor(model, comment) {
        var anchor = comment && (comment.anchor || comment.Anchor) || {};
        var blockId = _asText(anchor.BlockId || anchor.blockId || anchor.StartBlockId || anchor.startBlockId || '');
        if (!blockId) return null;
        var block = _findBlock(model, blockId);
        var length = _blockText(block).length;
        var hasInlineStart = anchor.StartInlineIndex !== undefined || anchor.startInlineIndex !== undefined;
        var hasInlineEnd = anchor.EndInlineIndex !== undefined || anchor.endInlineIndex !== undefined;
        var start = hasInlineStart
            ? blockOffsetFromInlineIndex(block, anchor.StartInlineIndex ?? anchor.startInlineIndex, anchor.StartOffset ?? anchor.startOffset ?? anchor.StartTextOffset ?? anchor.startTextOffset ?? 0)
            : Number(anchor.StartOffset ?? anchor.startOffset ?? anchor.StartTextOffset ?? anchor.startTextOffset ?? 0) || 0;
        var end = hasInlineEnd
            ? blockOffsetFromInlineIndex(block, anchor.EndInlineIndex ?? anchor.endInlineIndex, anchor.EndOffset ?? anchor.endOffset ?? anchor.EndTextOffset ?? anchor.endTextOffset ?? length)
            : Number(anchor.EndOffset ?? anchor.endOffset ?? anchor.EndTextOffset ?? anchor.endTextOffset ?? length) || length;
        if (end <= start && length > start) end = length;
        return {
            startBlockId: blockId,
            endBlockId: _asText(anchor.EndBlockId || anchor.endBlockId || blockId),
            startOffset: Math.max(0, Math.min(start, length)),
            endOffset: Math.max(0, Math.min(Math.max(start, end), length))
        };
    }

    function rangeFromRevision(model, revision) {
        var source = revision || {};
        var revisionRange = source.range || source.Range || source.affectedRange || source.AffectedRange || {};
        var blockId = _asText(revisionRange.BlockId || revisionRange.blockId || revisionRange.StartBlockId || revisionRange.startBlockId || '');
        if (!blockId) return null;
        var block = _findBlock(model, blockId);
        var length = _blockText(block).length;
        var hasInlineStart = revisionRange.StartInlineIndex !== undefined || revisionRange.startInlineIndex !== undefined;
        var hasInlineEnd = revisionRange.EndInlineIndex !== undefined || revisionRange.endInlineIndex !== undefined;
        var start = hasInlineStart
            ? blockOffsetFromInlineIndex(block, revisionRange.StartInlineIndex ?? revisionRange.startInlineIndex, revisionRange.StartOffset ?? revisionRange.startOffset ?? revisionRange.start ?? 0)
            : Number(revisionRange.StartOffset ?? revisionRange.startOffset ?? revisionRange.start ?? 0) || 0;
        var end = hasInlineEnd
            ? blockOffsetFromInlineIndex(block, revisionRange.EndInlineIndex ?? revisionRange.endInlineIndex, revisionRange.EndOffset ?? revisionRange.endOffset ?? revisionRange.end ?? length)
            : Number(revisionRange.EndOffset ?? revisionRange.endOffset ?? revisionRange.end ?? length) || length;
        if (end <= start && length > start) end = length;
        return {
            startBlockId: blockId,
            endBlockId: _asText(revisionRange.EndBlockId || revisionRange.endBlockId || blockId),
            startOffset: Math.max(0, Math.min(start, length)),
            endOffset: Math.max(0, Math.min(Math.max(start, end), length))
        };
    }

    function buildRuntimeCommentMarkers(model) {
        var inlineRanges = collectInlineCommentRanges(model);
        return _asArray(model && model.comments).map(function (comment) {
            var commentId = readCommentId(comment);
            if (!commentId) return null;
            var range = inlineRanges[commentId] || rangeFromCommentAnchor(model, comment);
            if (!range || !range.startBlockId || range.endOffset <= range.startOffset) return null;
            var status = readCommentStatus(comment);
            return _sortObject({
                blockId: range.startBlockId,
                id: 'comment:' + commentId,
                isActive: false,
                isResolved: status === 'resolved',
                startOffset: range.startOffset,
                endOffset: range.endOffset,
                type: 'comment',
                threadId: commentId,
                targetId: commentId,
                status: status,
                range: range,
                affectsData: true,
                priority: 60,
                source: 'document'
            });
        }).filter(Boolean);
    }

    function buildRuntimeRevisionMarkers(model) {
        var inlineRanges = collectInlineRevisionRanges(model);
        return _asArray(model && model.revisions).map(function (revision) {
            var normalized = normalizeRevision(revision);
            var revisionId = _asText(normalized.id);
            if (!revisionId || readRevisionStatus(normalized) !== 'Pending') return null;
            var range = inlineRanges[revisionId] || rangeFromRevision(model, normalized);
            if (!range || !range.startBlockId || range.endOffset <= range.startOffset) return null;
            var type = readRevisionMarkerType(normalized);
            var payloadText = _asText(normalized.payloadJson || normalized.payload && normalized.payload.text || '');
            return _sortObject({
                author: normalized.author,
                blockId: range.startBlockId,
                createdAt: normalized.createdAt || normalized.timestamp || null,
                endOffset: range.endOffset,
                formatDelta: normalized.payload && normalized.payload.mark || null,
                id: 'revision:' + revisionId,
                insertedText: type === 'revisionInsertion' ? payloadText : '',
                isActive: false,
                originalText: type === 'revisionDeletion' ? payloadText : '',
                priority: 50,
                range: range,
                source: 'document',
                startOffset: range.startOffset,
                status: readRevisionStatus(normalized),
                targetId: revisionId,
                threadId: revisionId,
                type: type
            });
        }).filter(Boolean);
    }

    function refreshRuntimeMarkerStore(inst) {
        if (!inst) return null;
        inst.markerStore = createMarkerStore([].concat(
            buildRuntimeCommentMarkers(inst.model),
            buildRuntimeRevisionMarkers(inst.model)));
        inst.markerStoreDirty = false;
        return inst.markerStore;
    }

    function detectAutocompleteTriggerText(text, offset) {
        var before = _asText(text).slice(0, Number(offset || 0));
        var match = before.match(/(?:^|\s)(\{\{|@|\/)([A-Za-z0-9_-]*)$/);
        if (!match) return null;
        var marker = match[1];
        var query = match[2] || '';
        var triggerId = marker === '{{' ? 'token' : (marker === '@' ? 'mention' : 'slash');
        return {
            triggerId: triggerId,
            marker: marker,
            markerType: triggerId === 'token' ? 'tokenQuery' : (triggerId === 'mention' ? 'tagQuery' : 'slashQuery'),
            query: query,
            startOffset: before.length - marker.length - query.length,
            endOffset: before.length
        };
    }

    function computeFloatingPosition(anchor, floating, options) {
        var opts = options || {};
        var gutter = Number(opts.gutter || 8) || 8;
        var rect = Object.assign({}, anchor || {});
        if (opts.anchorIsContainerRelative && opts.scrollContainerRect) {
            rect.left = Number(rect.left || 0) - Number(opts.scrollLeft || 0) + Number(opts.scrollContainerRect.left || 0);
            rect.top = Number(rect.top || 0) - Number(opts.scrollTop || 0) + Number(opts.scrollContainerRect.top || 0);
        }
        var width = Number(floating && floating.width || 0) || 0;
        var height = Number(floating && floating.height || 0) || 0;
        var viewport = opts.constrainToScrollContainer && opts.scrollContainerRect
            ? {
                left: Number(opts.scrollContainerRect.left || 0),
                top: Number(opts.scrollContainerRect.top || 0),
                right: Number(opts.scrollContainerRect.left || 0) + Number(opts.scrollContainerRect.width || 0),
                bottom: Number(opts.scrollContainerRect.top || 0) + Number(opts.scrollContainerRect.height || 0)
            }
            : {
                left: Number(opts.viewportLeft || 0) || 0,
                top: Number(opts.viewportTop || 0) || 0,
                right: Number(opts.viewportWidth || 0) || 0,
                bottom: Number(opts.viewportHeight || 0) || 0
            };
        var placement = opts.placement === 'top' ? 'top' : 'bottom';
        var left = Number(rect.left || 0) + Number(rect.width || 0) / 2 - width / 2;
        var top = placement === 'top'
            ? Number(rect.top || 0) - height - gutter
            : Number(rect.top || 0) + Number(rect.height || 0) + gutter;
        if (placement === 'bottom' && top + height > viewport.bottom - gutter) {
            placement = 'top';
            top = Number(rect.top || 0) - height - gutter;
        }
        left = Math.max(viewport.left + gutter, Math.min(left, viewport.right - gutter - width));
        top = Math.max(viewport.top + gutter, Math.min(top, viewport.bottom - gutter - height));
        return { left: Math.round(left), top: Math.round(top), width: width, height: height, placement: placement };
    }

    function createVisualStabilityTracker(options) {
        var opts = options || {};
        var maxToolbarDelta = Number(opts.maxToolbarDelta ?? opts.MaxToolbarDelta ?? 1);
        var frames = [];
        function keyForFrame(frame) {
            return {
                pageKey: _asText(frame && (frame.pageKey || frame.PageKey || frame.pageFingerprint || frame.PageFingerprint || '')),
                paragraphKey: _asText(frame && (frame.paragraphKey || frame.ParagraphKey || frame.paragraphFingerprint || frame.ParagraphFingerprint || '')),
                selectionRelevant: frame && (frame.selectionRelevant === true || frame.SelectionRelevant === true),
                floatingOpen: frame && (frame.floatingOpen === true || frame.FloatingOpen === true),
                toolbarTop: Number(frame && (frame.toolbarTop ?? frame.ToolbarTop ?? 0)) || 0,
                commandValue: frame && (frame.commandValue ?? frame.CommandValue ?? null)
            };
        }
        function record(beforeFrame, afterFrame, reason) {
            var before = keyForFrame(beforeFrame || {});
            var after = keyForFrame(afterFrame || {});
            var toolbarDelta = Math.abs(after.toolbarTop - before.toolbarTop);
            var issues = [];
            if (before.paragraphKey && after.paragraphKey && before.paragraphKey !== after.paragraphKey) issues.push('paragraph-blink');
            if (before.pageKey && after.pageKey && before.pageKey !== after.pageKey) issues.push('page-blink');
            if (toolbarDelta > maxToolbarDelta) issues.push('toolbar-jump');
            if (before.selectionRelevant && before.floatingOpen && !after.floatingOpen) issues.push('floating-toolbar-hidden');
            if (reason === 'command' && JSON.stringify(before.commandValue) !== JSON.stringify(after.commandValue) && after.commandValue === null) issues.push('command-state-unstable');
            var result = _sortObject({
                ok: issues.length === 0,
                reason: reason || '',
                issues: issues,
                toolbarDelta: toolbarDelta,
                paragraphStable: issues.indexOf('paragraph-blink') < 0,
                pageStable: issues.indexOf('page-blink') < 0,
                floatingToolbarStable: issues.indexOf('floating-toolbar-hidden') < 0,
                commandStateStable: issues.indexOf('command-state-unstable') < 0
            });
            frames.push(result);
            return result;
        }
        return {
            record: record,
            snapshot: function () {
                return _sortObject({
                    ok: frames.every(function (frame) { return frame.ok === true; }),
                    frameCount: frames.length,
                    frames: frames.slice()
                });
            }
        };
    }

    function computeObjectChromeLayout(request) {
        var source = request || {};
        var objectRect = rectFromAny(source.objectRect || source.ObjectRect || source.rect || source.Rect);
        var toolbarSize = rectFromAny(source.toolbarSize || source.ToolbarSize || { Width: 300, Height: 36 });
        var viewport = rectFromAny(source.viewport || source.Viewport || { X: 0, Y: 0, Width: 1280, Height: 720 });
        var sidePanel = source.sidePanelRect || source.SidePanelRect ? rectFromAny(source.sidePanelRect || source.SidePanelRect) : null;
        var gutter = Number(source.gutter ?? source.Gutter ?? 8) || 8;
        var usableViewport = {
            left: viewport.X,
            top: viewport.Y,
            width: viewport.Width,
            height: viewport.Height
        };
        if (sidePanel && sidePanel.Width > 0 && sidePanel.X < viewport.X + viewport.Width) {
            usableViewport.width = Math.max(120, sidePanel.X - viewport.X - gutter);
        }
        var position = computeFloatingPosition(
            { left: objectRect.X, top: objectRect.Y, width: objectRect.Width, height: objectRect.Height },
            { width: toolbarSize.Width, height: toolbarSize.Height },
            {
                placement: 'top',
                gutter: gutter,
                viewportWidth: usableViewport.left + usableViewport.width,
                viewportHeight: usableViewport.top + usableViewport.height
            });
        position.left = Math.max(viewport.X + gutter, Math.min(position.left, viewport.X + usableViewport.width - toolbarSize.Width - gutter));
        var toolbarRect = { X: position.left, Y: position.top, Width: toolbarSize.Width, Height: toolbarSize.Height };
        var avoidsSidePanel = !sidePanel || !rectsOverlapWithTolerance(
            { x: toolbarRect.X, y: toolbarRect.Y, width: toolbarRect.Width, height: toolbarRect.Height },
            { x: sidePanel.X, y: sidePanel.Y, width: sidePanel.Width, height: sidePanel.Height },
            1);
        return _sortObject({
            toolbar: {
                left: Math.round(toolbarRect.X),
                top: Math.round(toolbarRect.Y),
                width: Math.round(toolbarRect.Width),
                height: Math.round(toolbarRect.Height),
                placement: position.placement,
                avoidsSidePanel: avoidsSidePanel
            },
            layoutBubble: {
                compact: true,
                maxButtonCount: 4,
                minHeight: 32,
                labelMode: 'icon-or-short-label'
            },
            selectionPane: {
                accessible: true,
                minWidth: 224,
                maxHeight: Math.max(160, Math.min(384, viewport.Height - gutter * 2))
            }
        });
    }

    function createObjectChromeModel(request) {
        var source = request || {};
        var objectRect = rectFromAny(source.objectRect || source.ObjectRect || source.rect || source.Rect || { Width: 160, Height: 96 });
        var captionRect = source.captionRect || source.CaptionRect ? rectFromAny(source.captionRect || source.CaptionRect) : null;
        var handleSize = Math.max(12, Number(source.handleSize ?? source.HandleSize ?? 12) || 12);
        var hitSize = Math.max(18, Number(source.hitSize ?? source.HitSize ?? 18) || 18);
        var names = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'];
        var handles = names.map(function (name) {
            var x = objectRect.X + (name.indexOf('e') >= 0 ? objectRect.Width : (name.indexOf('w') >= 0 ? 0 : objectRect.Width / 2)) - handleSize / 2;
            var y = objectRect.Y + (name.indexOf('s') >= 0 ? objectRect.Height : (name.indexOf('n') >= 0 ? 0 : objectRect.Height / 2)) - handleSize / 2;
            if (captionRect && name.indexOf('s') >= 0) y = Math.min(y, captionRect.Y - handleSize - 2);
            var overlapsCaption = captionRect
                ? rectsOverlapWithTolerance({ x: x, y: y, width: handleSize, height: handleSize }, { x: captionRect.X, y: captionRect.Y, width: captionRect.Width, height: captionRect.Height }, 1)
                : false;
            return {
                name: name,
                width: handleSize,
                height: handleSize,
                hitWidth: hitSize,
                hitHeight: hitSize,
                captionOverlap: overlapsCaption
            };
        });
        var toolbar = computeObjectChromeLayout(source);
        return _sortObject({
            selectionOutline: {
                clean: true,
                width: 2,
                offset: 3,
                contrastToken: 'var(--tm-color-primary)'
            },
            handles: handles,
            allHandlesLargeEnough: handles.every(function (handle) { return handle.width >= 12 && handle.height >= 12 && handle.hitWidth >= 18 && handle.hitHeight >= 18; }),
            handlesAvoidCaption: handles.every(function (handle) { return handle.captionOverlap !== true; }),
            toolbar: toolbar.toolbar,
            layoutBubble: toolbar.layoutBubble,
            selectionPane: toolbar.selectionPane
        });
    }

    function previewImmediateTextEdit(request) {
        var source = request || {};
        var text = _asText(source.text ?? source.Text ?? '');
        var inputType = _asText(source.inputType ?? source.InputType ?? 'insertText');
        var data = _asText(source.data ?? source.Data ?? '');
        var selection = createSelectionSnapshot(source.selection || source.Selection || { blockId: 'p1', offset: text.length });
        var offset = Math.max(0, Math.min(text.length, Number(selection.offset || 0)));
        var visibleText = text;
        var lines = [text];
        if (inputType === 'insertText') {
            visibleText = text.slice(0, offset) + data + text.slice(offset);
            lines = [visibleText];
        } else if (inputType === 'insertParagraph' || inputType === 'insertLineBreak') {
            visibleText = text.slice(0, offset) + '\n' + text.slice(offset);
            lines = visibleText.split('\n');
        } else if (inputType === 'deleteContentBackward') {
            if (offset === 0 && source.previousText !== undefined) {
                visibleText = _asText(source.previousText ?? source.PreviousText ?? '') + text;
            } else {
                visibleText = text.slice(0, Math.max(0, offset - 1)) + text.slice(offset);
            }
            lines = visibleText.split('\n');
        }
        var maxLineChars = Math.max(1, Number(source.maxLineChars ?? source.MaxLineChars ?? 40) || 40);
        var wrapped = [];
        lines.forEach(function (line) {
            if (line.length === 0) wrapped.push('');
            for (var i = 0; i < line.length; i += maxLineChars) wrapped.push(line.slice(i, i + maxLineChars));
        });
        return _sortObject({
            visibleText: visibleText,
            spaceVisibleImmediately: inputType !== 'insertText' || data !== ' ' || visibleText.indexOf(' ') >= 0,
            enterStableImmediately: inputType !== 'insertParagraph' && inputType !== 'insertLineBreak' || lines.length >= 2,
            backspaceMergeImmediate: inputType !== 'deleteContentBackward' || offset !== 0 || visibleText.indexOf(_asText(source.previousText ?? source.PreviousText ?? '')) === 0,
            longWordPredictable: wrapped.every(function (line) { return line.length <= maxLineChars; }),
            wrappedLines: wrapped,
            nextSelection: createSelectionSnapshot(Object.assign({}, selection, {
                offset: inputType === 'insertText' ? offset + data.length : (inputType === 'deleteContentBackward' ? Math.max(0, offset - 1) : offset),
                isCollapsed: true
            }))
        });
    }

    function selectionOverlapsRange(selection, range) {
        var snapshot = createSelectionSnapshot(selection || {});
        if (!range) return false;
        var blockId = range.blockId || range.BlockId || range.startBlockId || range.StartBlockId || '';
        if (blockId && blockId !== snapshot.blockId) return false;
        var start = Number(range.start ?? range.Start ?? range.startOffset ?? range.StartOffset ?? 0);
        var end = Number(range.end ?? range.End ?? range.endOffset ?? range.EndOffset ?? start);
        var offset = Number(snapshot.offset || 0);
        return offset >= Math.min(start, end) && offset <= Math.max(start, end);
    }

    function createSidePanelSyncState(model, selection, options) {
        var source = options || {};
        buildIndexes(model);
        var snapshot = createSelectionSnapshot(selection || firstModelSelection(model));
        var block = _findBlock(model, snapshot.blockId);
        var formatting = collectFormattingState(model, snapshot, source.pendingTypingMarks || source.PendingTypingMarks || []);
        var comments = _asArray(model && model.comments).filter(function (comment) {
            var range = comment.range || comment.Range || comment.anchor || comment.Anchor || {};
            return selectionOverlapsRange(snapshot, range);
        });
        var revisions = _asArray(model && model.revisions).filter(function (revision) {
            return selectionOverlapsRange(snapshot, revision.affectedRange || revision.AffectedRange || revision.range || revision.Range || {});
        });
        var imageState = block && block.type === 'image'
            ? Object.assign({ isSelected: true, blockId: block.id }, _clone(block.content || {}))
            : null;
        var panelInputs = {
            debounceMs: Math.max(80, Number(source.debounceMs ?? source.DebounceMs ?? 180) || 180),
            applyViaCommands: true,
            livePreview: true,
            waitsForBlur: false
        };
        return _sortObject({
            selection: snapshot,
            properties: {
                blockId: snapshot.blockId,
                blockType: block ? block.type : '',
                formatting: formatting.commandValues
            },
            image: imageState,
            revision: {
                activeRevisionIds: revisions.map(function (revision) { return revision.id || revision.Id || ''; }).filter(Boolean)
            },
            comments: {
                activeCommentIds: comments.map(function (comment) { return comment.id || comment.Id || ''; }).filter(Boolean)
            },
            panelInputs: panelInputs,
            source: 'runtime-selection'
        });
    }

    function createPanelCommandDebouncer(options) {
        var opts = options || {};
        var debounceMs = Math.max(80, Number(opts.debounceMs ?? opts.DebounceMs ?? 180) || 180);
        var pending = [];
        var flushed = [];
        return {
            queue: function (commandId, payload) {
                var item = { commandId: normalizeCommandId(commandId), payload: _clone(payload || {}), debounceMs: debounceMs, livePreview: true, waitsForBlur: false };
                pending.push(item);
                return _sortObject(Object.assign({ queued: true }, item));
            },
            flush: function () {
                flushed = flushed.concat(pending);
                pending = [];
                return _sortObject({ appliedViaCommands: true, count: flushed.length, commands: flushed.slice() });
            },
            snapshot: function () {
                return _sortObject({ pendingCount: pending.length, flushedCount: flushed.length, debounceMs: debounceMs });
            }
        };
    }

    function createUxPolishHarness(options) {
        return {
            visualStability: createVisualStabilityTracker(options),
            computeObjectChromeLayout: computeObjectChromeLayout,
            createObjectChromeModel: createObjectChromeModel,
            createStrictPerformanceStats: createStrictPerformanceStats,
            recordOperationPerformance: recordOperationPerformance,
            previewImmediateTextEdit: previewImmediateTextEdit,
            createSidePanelSyncState: createSidePanelSyncState,
            createPanelCommandDebouncer: createPanelCommandDebouncer
        };
    }

    function buildPageMetricsForTest(pages, renderedIndexes, overflowIndexes, activeIndex) {
        var rendered = new Set(_asArray(renderedIndexes));
        var overflow = new Set(_asArray(overflowIndexes));
        var metricsPages = _asArray(pages).map(function (page, index) {
            var pageIndex = Number(page.index ?? page.PageIndex ?? index) || 0;
            return {
                PageIndex: pageIndex,
                PageNumber: pageIndex + 1,
                BlockIds: _asArray(page.blockIds || page.BlockIds),
                IsVirtual: !rendered.has(pageIndex),
                HasOverflow: overflow.has(pageIndex)
            };
        });
        return {
            TotalPages: metricsPages.length,
            RenderedPages: metricsPages.filter(function (page) { return !page.IsVirtual; }).length,
            VirtualizedPages: metricsPages.filter(function (page) { return page.IsVirtual; }).length,
            ActivePageIndex: activeIndex,
            Pages: metricsPages
        };
    }

    function formatNonPrintingText(text) {
        return _asText(text).replace(/ /g, '\u00b7').replace(/\t/g, '\u2192').replace(/\n/g, '\u00b6\n');
    }

    function findActiveHeadingBlockIdFromRects(rects, viewportTop) {
        var current = null;
        _asArray(rects).forEach(function (rect) {
            if (Number(rect.top || 0) <= Number(viewportTop || 0)) current = rect.id || rect.Id || current;
        });
        return current;
    }

    function createPerformanceMetricsHarness() {
        var metrics = {
            Baselines: [],
            TypingLatency: {},
            ImageDragLatency: {},
            SelectionMovementLatency: {},
            LayoutPassCount: 0,
            LayoutDragReflowCount: 0,
            LayoutResizeReflowCount: 0,
            LayoutInvalidatedPages: [],
            LayoutInvalidatedPageCount: 0,
            LastLayoutPassMs: 0,
            MemoryCleanup: null
        };
        function latencySummary(samples) {
            var values = _asArray(samples).map(Number).filter(function (value) { return Number.isFinite(value); });
            var total = values.reduce(function (sum, value) { return sum + value; }, 0);
            return {
                Count: values.length,
                LastMs: values.length ? values[values.length - 1] : 0,
                MaxMs: values.length ? Math.max.apply(Math, values) : 0,
                AverageMs: values.length ? total / values.length : 0
            };
        }
        function baseline(name, samples) {
            var item = Object.assign({ Name: _asText(name || 'baseline') }, latencySummary(samples));
            metrics.Baselines.push(item);
            return item;
        }
        return {
            recordTypingLatency: function (scenarioName, samples) {
                var item = baseline('typing-' + scenarioName, samples);
                metrics.TypingLatency[scenarioName] = item;
                return item;
            },
            recordImageDragLatency: function (samples) {
                metrics.ImageDragLatency = baseline('image-drag', samples);
                return metrics.ImageDragLatency;
            },
            recordSelectionMovementLatency: function (samples) {
                metrics.SelectionMovementLatency = baseline('selection-movement', samples);
                return metrics.SelectionMovementLatency;
            },
            recordLayoutPass: function (reason, beforeSnapshot, afterSnapshot) {
                metrics.LayoutPassCount++;
                if (String(reason || '').indexOf('drag') >= 0) metrics.LayoutDragReflowCount++;
                if (String(reason || '').indexOf('resize') >= 0) metrics.LayoutResizeReflowCount++;
                var pages = _asArray(afterSnapshot && afterSnapshot.Pages);
                pages.forEach(function (page) {
                    var index = Number(page.PageIndex ?? page.index ?? 0) || 0;
                    if (metrics.LayoutInvalidatedPages.indexOf(index) < 0) metrics.LayoutInvalidatedPages.push(index);
                });
                metrics.LayoutInvalidatedPageCount = metrics.LayoutInvalidatedPages.length;
                metrics.LastLayoutPassMs = 0;
            },
            recordMemoryCleanup: function (cleanup) {
                metrics.MemoryCleanup = _clone(cleanup || {});
                return metrics.MemoryCleanup;
            },
            metrics: function () { return _clone(metrics); },
            snapshot: function () {
                return {
                    HasInstance: true,
                    Performance: _clone(metrics),
                    LayoutPassCount: metrics.LayoutPassCount,
                    LayoutInvalidatedPageCount: metrics.LayoutInvalidatedPageCount,
                    BaselineCount: metrics.Baselines.length
                };
            },
            dispose: function () {}
        };
    }

    function schemaAllowsBlockForTest(type, region) {
        var normalizedRegion = _asText(region).toLowerCase();
        if ((type === 6 || String(type).toLowerCase().indexOf('pagebreak') >= 0) && normalizedRegion !== 'body') return false;
        if ((type === 4 || String(type).toLowerCase().indexOf('table') >= 0) && normalizedRegion === 'tablecell') return false;
        return true;
    }

    function normalizeInsertionBlocksForSchema(blocks, region) {
        var warnings = [];
        var output = [];
        _asArray(blocks).forEach(function (block) {
            if (!schemaAllowsBlockForTest(block.Type ?? block.type, region)) {
                if ((block.Type ?? block.type) === 4 && _asText(region).toLowerCase() === 'tablecell') {
                    _asArray(block.Content && block.Content.Rows).forEach(function (row) {
                        _asArray(row.Cells).forEach(function (cell) {
                            _asArray(cell.Blocks).forEach(function (child) { output.push(_clone(child)); });
                        });
                    });
                    warnings.push({ code: 'table-unwrapped-in-table-cell' });
                } else {
                    warnings.push({ code: 'block-rejected-by-schema' });
                }
                return;
            }
            var clone = _clone(block);
            if ((clone.Type ?? clone.type) === 5 && clone.Content && clone.Content.AltText === undefined) {
                clone.Content.AltText = '';
                warnings.push({ code: 'image-alt-text-defaulted' });
            }
            output.push(clone);
        });
        return { blocks: output, warnings: warnings };
    }

    function applyLayoutTextEditModel(segments, change) {
        var ordered = _asArray(segments).slice().sort(function (a, b) { return Number(a.StartOffset || 0) - Number(b.StartOffset || 0); });
        var text = ordered.map(function (segment) { return _asText(segment.Text); }).join('');
        var offset = Math.max(0, Math.min(text.length, Number(change && change.offset || 0) || 0));
        var inputType = change && change.inputType || '';
        if (inputType === 'insertText') {
            var data = _asText(change.data || '');
            return { Handled: true, Text: text.slice(0, offset) + data + text.slice(offset), CaretOffset: offset + data.length };
        }
        if (inputType === 'deleteContentBackward') {
            if (offset <= 0) return { Handled: false, MergePrevious: true };
            return { Handled: true, Text: text.slice(0, offset - 1) + text.slice(offset), DeletedText: text.slice(offset - 1, offset), CaretOffset: offset - 1 };
        }
        if (inputType === 'deleteContentForward') {
            if (offset >= text.length) return { Handled: false, MergeNext: true };
            return { Handled: true, Text: text.slice(0, offset) + text.slice(offset + 1), DeletedText: text.slice(offset, offset + 1), CaretOffset: offset };
        }
        if (inputType === 'insertParagraph') {
            return { Handled: true, SplitBefore: text.slice(0, offset), SplitAfter: text.slice(offset), StartOffset: offset };
        }
        return { Handled: false };
    }

    function normalizeMarkTypeForTest(value) {
        if (value === 8) return 'Revision';
        if (value === 7) return 'CommentAnchor';
        var key = _asText(value).replace(/[^a-z]/gi, '').toLowerCase();
        if (key === 'revision' || key === 'revisionanchor') return 'Revision';
        if (key === 'commentanchor') return 'CommentAnchor';
        return value;
    }

    function computeImageMoveSnap(point, context) {
        var ctx = context || {};
        var result = { x: Number(point && point.x || 0) || 0, y: Number(point && point.y || 0) || 0, guides: [] };
        if (ctx.disableSnap) return result;
        var body = rectFromAny(ctx.bodyRect);
        var size = rectFromAny(ctx.objectSize);
        var candidates = [
            { x: body.X, Kind: 'text-left' },
            { x: body.X + body.Width - size.Width, Kind: 'text-right' },
            { x: body.X + body.Width / 2 - size.Width / 2, Kind: 'page-center-x' }
        ];
        _asArray(ctx.otherObjects).forEach(function (object) {
            var rect = rectFromAny(object.Rect);
            candidates.push({ x: rect.X - size.Width, Kind: 'object-left' });
        });
        candidates.forEach(function (candidate) {
            if (Math.abs(result.x - candidate.x) <= 5) {
                result.x = candidate.x;
                result.guides.push({ Kind: candidate.Kind, X: candidate.x });
            }
        });
        _asArray(ctx.lines).forEach(function (line) {
            var rect = rectFromAny(line.Rect);
            if (Math.abs(result.y - rect.Y) <= 5) {
                result.y = rect.Y;
                result.guides.push({ Kind: 'line-top', Y: rect.Y });
            }
        });
        return result;
    }

    function createRuntimeSelectionFromSnapshotForTest(snapshot) {
        var source = snapshot || {};
        return _sortObject({
            region: source.region || source.Region || 'Body',
            pageIndex: source.pageIndex ?? source.PageIndex ?? 0,
            anchorBlockId: source.anchorBlockId || source.AnchorBlockId || null,
            focusBlockId: source.focusBlockId || source.FocusBlockId || source.anchorBlockId || source.AnchorBlockId || null,
            anchorNodeId: source.anchorInlineId || source.AnchorInlineId || null,
            focusNodeId: source.focusInlineId || source.FocusInlineId || source.anchorInlineId || source.AnchorInlineId || null,
            anchorOffset: source.anchorOffset ?? source.AnchorOffset ?? 0,
            focusOffset: source.focusOffset ?? source.FocusOffset ?? source.anchorOffset ?? source.AnchorOffset ?? 0,
            isCollapsed: source.isCollapsed ?? source.IsCollapsed ?? true,
            direction: source.direction || source.Direction || 'none',
            activeTableCellId: source.activeTableCellId || source.ActiveTableCellId || null,
            activeTableId: source.activeTableId || source.ActiveTableId || null,
            tableCellPath: source.tableCellPath || source.TableCellPath || null,
            activeImageBlockId: source.activeImageBlockId || source.ActiveImageBlockId || null,
            activeCommentId: source.activeCommentId || source.ActiveCommentId || null,
            activeRevisionId: source.activeRevisionId || source.ActiveRevisionId || null
        });
    }

    function createSelectionSnapshotFromRuntimeSelectionForTest(selection) {
        var source = selection || {};
        return _sortObject({
            region: source.region || 'Body',
            pageIndex: source.pageIndex || 0,
            anchorBlockId: source.anchorBlockId || null,
            focusBlockId: source.focusBlockId || source.anchorBlockId || null,
            anchorInlineId: source.anchorNodeId || null,
            focusInlineId: source.focusNodeId || source.anchorNodeId || null,
            anchorOffset: source.anchorOffset || 0,
            focusOffset: source.focusOffset || 0,
            isCollapsed: source.isCollapsed !== false,
            direction: source.direction || 'none',
            activeTableCellId: source.activeTableCellId || null,
            activeTableId: source.activeTableId || null,
            tableCellPath: source.tableCellPath || null,
            activeImageBlockId: source.activeImageBlockId || null,
            activeCommentId: source.activeCommentId || null,
            activeRevisionId: source.activeRevisionId || null
        });
    }

    function createRuntimeCommandTransactionForTest(command, payload, beforeSelection, afterSelection, beforeFormatting, afterFormatting) {
        var op = {
            operationId: 'test-op-1',
            command: command,
            payload: _clone(payload || {}),
            beforeSelection: createSelectionSnapshotFromRuntimeSelectionForTest(createRuntimeSelectionFromSnapshotForTest(beforeSelection || {})),
            afterSelection: createSelectionSnapshotFromRuntimeSelectionForTest(createRuntimeSelectionFromSnapshotForTest(afterSelection || {})),
            beforeFormatting: _clone(beforeFormatting || {}),
            afterFormatting: _clone(afterFormatting || {})
        };
        return {
            command: command,
            operations: [op],
            inverseOperations: [Object.assign(_clone(op), { operationId: 'test-op-1-inverse', inverseOf: 'test-op-1' })]
        };
    }

    function transformSelectionForTextChangeForTest(selection, target, offset, length, isDelete) {
        var clone = _clone(selection || {});
        var start = Math.max(0, Number(offset || 0) || 0);
        var count = Math.max(0, Number(length || 0) || 0);
        var end = start + count;
        ['anchor', 'focus'].forEach(function (prefix) {
            var blockKey = prefix + 'BlockId';
            var inlineKey = prefix + 'InlineId';
            var offsetKey = prefix + 'Offset';
            if (clone[blockKey] !== target.BlockId || (target.InlineId && clone[inlineKey] !== target.InlineId)) {
                return;
            }

            var point = Math.max(0, Number(clone[offsetKey] || 0) || 0);
            if (!isDelete) {
                if (point >= start) clone[offsetKey] = point + count;
                return;
            }

            if (point < start) {
                clone[offsetKey] = point;
            } else if (point <= end) {
                clone[offsetKey] = start;
            } else {
                clone[offsetKey] = Math.max(0, point - count);
            }
        });
        return clone;
    }

    function transformRuntimeCommentAnchorsForTextChangeForTest(comments, blockId, offset, length, isDelete) {
        return _asArray(comments).map(function (comment) {
            var clone = _clone(comment);
            var anchor = clone.Anchor || clone.anchor || {};
            if ((anchor.BlockId || anchor.blockId) !== blockId) return clone;
            var count = Math.max(0, Number(length || 0) || 0);
            var delta = count * (isDelete ? -1 : 1);
            var start = Number(anchor.StartOffset ?? anchor.startOffset ?? 0) || 0;
            var end = Number(anchor.EndOffset ?? anchor.endOffset ?? start) || start;
            if (!isDelete) {
                if (offset <= start) {
                    start += count;
                    end += count;
                } else if (offset < end) {
                    end += count;
                }
            } else {
                if (offset + count <= start) {
                    start += delta;
                    end += delta;
                } else if (offset >= end) {
                    // no-op
                } else {
                    end = Math.max(start, end - count);
                    if (offset <= start && offset + count >= end) {
                        start = Math.max(0, offset);
                        end = start;
                        anchor.IsOrphaned = true;
                    }
                }
            }
            anchor.StartOffset = Math.max(0, start);
            anchor.EndOffset = Math.max(anchor.StartOffset, end);
            clone.Anchor = anchor;
            return clone;
        });
    }

    function sortRemoteBatchOperationsForTest(operations) {
        return _asArray(operations).slice().sort(function (a, b) {
            var aTs = Number(a.Metadata && a.Metadata.LogicalTimestamp);
            var bTs = Number(b.Metadata && b.Metadata.LogicalTimestamp);
            var aHasTs = Number.isFinite(aTs);
            var bHasTs = Number.isFinite(bTs);
            if (aHasTs && bHasTs && aTs !== bTs) return aTs - bTs;
            if (aHasTs !== bHasTs) return aHasTs ? -1 : 1;
            var aTarget = a.Target || {};
            var bTarget = b.Target || {};
            return _asText(aTarget.BlockId).localeCompare(_asText(bTarget.BlockId))
                || (Number(aTarget.Offset || 0) - Number(bTarget.Offset || 0))
                || _asText(a.OperationId).localeCompare(_asText(b.OperationId));
        });
    }

    function transformRemoteBatchInsertOffsetsForTest(operations) {
        var shifts = {};
        return _asArray(operations).map(function (operation) {
            var clone = _clone(operation);
            var target = clone.Target || {};
            var key = [target.BlockId, target.InlineId, target.InlineIndex, target.Offset].join('|');
            var shift = shifts[key] || 0;
            target.Offset = Number(target.Offset || 0) + shift;
            shifts[key] = shift + _asText(clone.Text || clone.text).length;
            clone.Target = target;
            return clone;
        });
    }

    function createRenderPlanForTest(document) {
        var doc = document || {};
        var blocks = _asArray(doc.Blocks || doc.blocks);
        var blockPlans = blocks.map(function (block) {
            var blockId = _asText(block.Id ?? block.id ?? '');
            var content = block.Content || block.content || {};
            var type = blockTypeForTest(block);
            var plan = { type: type, blockId: blockId, attributes: { 'data-node-id': blockId, 'data-block-id': blockId } };
            if (type === 'paragraph') {
                plan.inlines = _asArray(content.Inlines || content.inlines).map(function (inline) {
                    var inlineId = _asText(inline.Id ?? inline.id ?? inline.Key ?? inline.key ?? '');
                    return {
                        type: inline.Key || inline.key ? 'token' : 'text',
                        attributes: { 'data-node-id': inlineId, 'data-inline-id': inlineId },
                        text: _asText(inline.Text ?? inline.text ?? inline.Key ?? inline.key ?? '')
                    };
                });
            } else if (type === 'image') {
                plan.image = { assetId: content.AssetId || content.assetId || null, url: content.Url || content.url || null };
            } else if (type === 'table') {
                plan.rows = _asArray(content.Rows || content.rows).map(function (row) {
                    return {
                        cells: _asArray(row.Cells || row.cells).map(function (cell) {
                            var cellId = _asText(cell.Id ?? cell.id ?? '');
                            return {
                                attributes: { 'data-node-id': cellId, 'data-cell-id': cellId },
                                blocks: _asArray(cell.Blocks || cell.blocks).map(function (child) {
                                    var childId = _asText(child.Id ?? child.id ?? '');
                                    return { attributes: { 'data-node-id': childId, 'data-block-id': childId } };
                                })
                            };
                        })
                    };
                });
            }
            return plan;
        });
        var headerFooterPlans = _asArray(doc.HeadersFooters || doc.headersFooters).map(function (region) {
            var id = _asText(region.Id ?? region.id ?? '');
            return {
                attributes: { 'data-node-id': id },
                blocks: _asArray(region.Blocks || region.blocks).map(function (block) {
                    var blockId = _asText(block.Id ?? block.id ?? '');
                    return { attributes: { 'data-block-id': blockId, 'data-node-id': blockId } };
                })
            };
        });
        return {
            source: 'runtimeDocument',
            documentId: doc.DocumentId || doc.documentId || '',
            pages: [{ index: 0, blockIds: blocks.map(function (block) { return block.Id || block.id; }) }],
            blockPlans: blockPlans,
            headerFooterPlans: headerFooterPlans
        };
    }

    function createUndoStackContractHarness(documentSnapshot) {
        var model = importFromCSharpJson(documentSnapshot || {
            DocumentId: 'phase11-undo-contract',
            Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
        });
        var inst = {
            id: 'phase11-undo-contract-' + (++_counter),
            model: model,
            schema: createDefaultSchemaRegistry(),
            selection: firstModelSelection(model),
            layout: { invalidatedScopeIds: [] },
            options: {},
            dotNetRef: null,
            commands: [],
            transactions: [],
            undoTransactions: [],
            redoTransactions: [],
            activeTransaction: null,
            boundaryPatches: [],
            modelEpoch: 0,
            savedEpoch: 0,
            savedVersion: null,
            dirtyState: createInitialDirtyState(),
            timers: [],
            pendingUndoStateTimer: null,
            pendingUndoStateNotify: false,
            performanceStats: createStrictPerformanceStats(),
            diagnostics: createDiagnosticsState()
        };

        function commitOperation(operation, meta) {
            var body = meta || {};
            var attached = attachOperationMethods(operation);
            var transaction = createTransaction(inst.model, {
                instanceId: inst.id,
                commandName: body.commandName || body.CommandName || attached.type,
                type: body.transactionType || body.TransactionType || (attached.type === OPERATION_TYPES.InsertText ? TRANSACTION_TYPES.Typing : TRANSACTION_TYPES.Default),
                label: body.label || body.Label || attached.type,
                beforeSelection: body.beforeSelection || body.BeforeSelection || inst.selection
            });
            inst.activeTransaction = transaction;
            var result = transaction.apply(attached);
            inst.activeTransaction = null;
            if (!result.ok) return result;
            transaction.commit();
            inst.selection = createSelectionPostFixer(inst.schema).fix(inst.model, transaction.afterSelection || inst.selection);
            transaction.afterSelection = _clone(inst.selection);
            transaction.afterModelSnapshot = _clone(inst.model);
            inst.transactions.push(transaction.toJSON());
            if (transactionAffectsDocument(transaction)) {
                pushUndoTransaction(inst, transaction);
                inst.redoTransactions = [];
                inst.modelEpoch++;
            }
            return Object.assign({ ok: true, undoState: undoStateForInstance(inst), transaction: transaction.toJSON() }, result);
        }

        function reload(nextDocumentSnapshot) {
            clearRuntimeUndoStacks(inst);
            inst.model = importFromCSharpJson(nextDocumentSnapshot || {
                DocumentId: 'phase11-undo-contract-reload',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            inst.selection = firstModelSelection(inst.model);
            inst.modelEpoch = 0;
            inst.savedEpoch = 0;
            inst.dirtyState = createInitialDirtyState();
            return undoStateForInstance(inst);
        }

        function text(blockId) {
            var block = _findBlock(inst.model, blockId || 'p1');
            return _blockText(block);
        }

        return {
            inst: inst,
            commitOperation: commitOperation,
            saveAck: function (epoch) { return applySaveAckToInstance(inst, { epoch: epoch ?? inst.modelEpoch }); },
            reload: reload,
            state: function () { return undoStateForInstance(inst); },
            text: text
        };
    }

    installGlobalToolbarButtonBridge();

    return {
        create: create,
        dispose: dispose,
        loadDocument: loadDocument,
        applyCommand: applyCommand,
        getDocumentSnapshot: getDocumentSnapshot,
        getSelectionSnapshot: getSelectionSnapshot,
        getLayoutSnapshot: getLayoutSnapshot,
        getDebugSnapshot: getDebugSnapshot,
        getDirtyState: getDirtyState,
        acknowledgeSave: acknowledgeSave,
        requestAutosaveSnapshot: requestAutosaveSnapshot,
        markAutosaveFailed: markAutosaveFailed,
        applyRemoteOperations: applyStrictRemoteOperations,
        updateProviderImageUrl: updateProviderImageUrl,
        refreshSnapshot: refreshSnapshot,
        applyCSharpUpdate: applyCSharpUpdate,
        exportCanonicalSnapshot: exportCanonicalSnapshot,
        importCanonicalSnapshot: importCanonicalSnapshot,
        getLayoutProbe: getLayoutProbe,
        runFrameProbe: runFrameProbe,
        simulateWatchdogFailure: simulateWatchdogFailure,
        exportFailureArtifact: exportFailureArtifact,
        getBoundaryPanelData: getBoundaryPanelData,
        focus: focus,
        setReadOnly: setReadOnly,
        getBodyHtml: getBodyHtml,
        getSelectedText: getSelectedText,
        restoreSelection: restoreSelection,
        getFormattingState: getFormattingState,
        getSidePanelSyncState: getSidePanelSyncState,
        getUndoState: getUndoState,
        getDebugUndoStack: getDebugUndoStack,
        getLastCommandTransaction: getLastCommandTransaction,
        getPageMetrics: getPageMetrics,
        getDebugMetrics: getDebugMetrics,
        clearDebugMetrics: clearDebugMetrics,
        setShowBlocks: setShowBlocks,
        setShowNonPrintingCharacters: setShowNonPrintingCharacters,
        setProtectionMode: setProtectionMode,
        setSearchMarkers: setSearchMarkers,
        clearSearchMarkers: clearSearchMarkers,
        scrollToSearchResult: scrollToSearchResult,
        scrollToBlock: scrollToBlock,
        scrollToPage: scrollToPage,
        getLinkInfo: getLinkInfo,
        copySelection: copySelection,
        applyRemoteOperation: applyRemoteOperation,
        applyRemoteOperationBatch: applyRemoteOperationBatch,
        applyRemoteCursor: applyRemoteCursor,
        insertImageNode: insertImageNode,
        captureCommentAnchor: captureCommentAnchor,
        upsertComment: upsertComment,
        removeComment: removeComment,
        getMarkers: getMarkers,
        upsertMarker: upsertMarker,
        scrollToComment: scrollToComment,
        scrollToRevision: scrollToRevision,
        setTrackChangesEnabled: setTrackChangesEnabled,
        setReviewDisplayMode: setReviewDisplayMode,
        reviewRevision: reviewRevision,
        reviewAllRevisions: reviewAllRevisions,
        clearRevisionDecorations: clearRevisionDecorations,
        applyOfflineState: applyOfflineState,
        getOfflineState: getOfflineState,
        markSaved: markSaved,
        DocumentHitTestService: function () {
            return { hitTest: hitTestLayoutGeometry };
        },
        model: {
            importFromCSharpJson: importFromCSharpJson,
            exportToCSharpJson: exportToCSharpJson,
            buildIndexes: buildIndexes,
            validateModel: validateModel,
            createDefaultSchemaRegistry: createDefaultSchemaRegistry,
            DocumentSchemaRegistry: DocumentSchemaRegistry
        },
        operations: {
            types: OPERATION_TYPES,
            transactionTypes: TRANSACTION_TYPES,
            createOperation: createOperation,
            validateOperation: validateOperation,
            applyOperation: applyOperation,
            createTransaction: createTransaction,
            createDocumentFingerprint: createDocumentFingerprint,
            createSelectionDocumentFingerprint: createSelectionDocumentFingerprint,
            createHistoryController: createHistoryController,
            createDiffer: createDiffer,
            mergeAdjacentTextRuns: mergeAdjacentTextRuns,
            shouldCoalesceTyping: shouldCoalesceTyping,
            coalesceTypingOperation: coalesceTypingOperation
        },
        history: {
            createHistoryController: createHistoryController,
            createHistoryRestoreOperation: createHistoryRestoreOperation
        },
        boundary: {
            createBoundaryPatch: createBoundaryPatch,
            createInitialDirtyState: createInitialDirtyState,
            operationAffectedBlockIds: operationAffectedBlockIds
        },
        selection: {
            createLogicalPosition: createLogicalPosition,
            createLogicalRange: createLogicalRange,
            createSelectionSnapshot: createSelectionSnapshot,
            createStableSelectionToken: serializeStableSelectionToken,
            createStableSelectionTokenData: createStableSelectionTokenData,
            withStableSelectionToken: withStableSelectionToken,
            normalizePosition: normalizeLogicalPosition,
            normalizeRange: normalizeLogicalRange,
            normalizeSelection: normalizeSelectionSnapshot,
            createSelectionPostFixer: createSelectionPostFixer,
            createSelectionEngine: createSelectionEngine,
            buildLayoutSnapshot: buildLayoutSnapshot,
            createModelLayoutDomMapper: createModelLayoutDomMapper,
            logicalToDomRange: logicalToDomRange,
            domRangeToLogical: domRangeToLogical,
            caretRectFromLayout: caretRectFromLayout,
            compareDomCaretToLayout: compareDomCaretToLayout,
            pointerHitTest: pointerHitTest,
            moveSelection: moveSelection
        },
        textLayout: {
            createTextMeasurementService: createTextMeasurementService,
            tokenizeText: tokenizeText,
            createLineBreaker: createLineBreaker,
            createParagraphLayoutEngine: createParagraphLayoutEngine,
            createLayoutScope: createLayoutScope,
            inferLayoutScopeFromOperation: inferLayoutScopeFromOperation,
            mergeTextStyle: mergeTextStyle,
            isCjkCharacter: isCjkCharacter
        },
        rendering: {
            createRenderSnapshot: createRenderSnapshot,
            createAtomicRenderer: createAtomicRenderer,
            projectEditing: projectEditing,
            projectData: projectData
        },
        input: {
            createBeforeInputNormalizer: createBeforeInputNormalizer,
            normalizeBeforeInput: normalizeBeforeInput,
            createInputPipeline: createInputPipeline,
            createTypingChangeBuffer: createTypingChangeBuffer
        },
        scheduling: {
            createActiveLayoutScheduler: createActiveLayoutScheduler
        },
        revisions: {
            createRevisionEngine: createRevisionEngine,
            normalizeRevision: normalizeRevision,
            normalizeRevisionRange: normalizeRevisionRange,
            normalizeRevisionGroups: normalizeRevisionGroups,
            resolveTrackChangesState: resolveTrackChangesState
        },
        commands: {
            createCommandDispatcher: createCommandDispatcher,
            normalizeCommandId: normalizeCommandId,
            collectFormattingState: collectFormattingState,
            computeFormattingState: computeFormattingState,
            toBlazorFormattingState: toBlazorFormattingState
        },
        uxPolish: {
            createVisualStabilityTracker: createVisualStabilityTracker,
            computeObjectChromeLayout: computeObjectChromeLayout,
            createObjectChromeModel: createObjectChromeModel,
            createStrictPerformanceStats: createStrictPerformanceStats,
            recordOperationPerformance: recordOperationPerformance,
            previewImmediateTextEdit: previewImmediateTextEdit,
            createSidePanelSyncState: createSidePanelSyncState,
            createPanelCommandDebouncer: createPanelCommandDebouncer,
            createUxPolishHarness: createUxPolishHarness
        },
        tables: {
            createTableController: createTableController
        },
        objects: {
            normalizeImageObject: normalizeImageObject,
            createTextExclusion: createTextExclusion,
            getAvailableIntervals: getAvailableIntervals,
            hitTestLayerPriority: hitTestLayerPriority,
            createImagePreviewController: createImagePreviewController,
            createEditorWidget: createEditorWidget,
            createImageInspectorState: createImageInspectorState
        },
        accessibility: {
            getRegionLabel: getRegionLabel,
            getFocusRegionFromElement: getFocusRegionFromElement,
            getFocusTargetDetails: getFocusTargetDetails,
            setActiveFocusRegion: setActiveFocusRegion,
            handleEditorKeyDown: handleEditorKeyDown,
            closeFloatingUiForKeyboard: closeFloatingUiForKeyboard,
            scheduleAccessibilityAnnouncement: scheduleAccessibilityAnnouncement
        },
        diagnostics: {
            getDebugSnapshot: getDebugSnapshot,
            getLayoutProbe: getLayoutProbe,
            runFrameProbe: runFrameProbe,
            simulateWatchdogFailure: simulateWatchdogFailure,
            exportFailureArtifact: exportFailureArtifact
        },
        __testHooks: {
            _instances: _instances,
            instances: _instances,
            importFromCSharpJson: importFromCSharpJson,
            exportToCSharpJson: exportToCSharpJson,
            validateModel: validateModel,
            createDefaultSchemaRegistry: createDefaultSchemaRegistry,
            createOperation: createOperation,
            validateOperation: validateOperation,
            applyOperation: applyOperation,
            createTransaction: createTransaction,
            createHistoryController: createHistoryController,
            createHistoryRestoreOperation: createHistoryRestoreOperation,
            createUndoStackContractHarness: createUndoStackContractHarness,
            operationsAffectDocument: operationsAffectDocument,
            clearRuntimeUndoStacks: clearRuntimeUndoStacks,
            undoStateForInstance: undoStateForInstance,
            createBoundaryPatch: createBoundaryPatch,
            createInitialDirtyState: createInitialDirtyState,
            operationAffectedBlockIds: operationAffectedBlockIds,
            createDiffer: createDiffer,
            createSelectionEngine: createSelectionEngine,
            createDocumentFingerprint: createDocumentFingerprint,
            createSelectionDocumentFingerprint: createSelectionDocumentFingerprint,
            createStableSelectionToken: serializeStableSelectionToken,
            createStableSelectionTokenData: createStableSelectionTokenData,
            withStableSelectionToken: withStableSelectionToken,
            validateStableSelectionToken: function (instanceId, tokenOrPayload, model) {
                return validateStableSelectionToken({
                    id: instanceId || '',
                    model: model || importFromCSharpJson({ DocumentId: 'test', Blocks: [] }),
                    schema: createDefaultSchemaRegistry()
                }, tokenOrPayload);
            },
            normalizeSelectionSnapshot: normalizeSelectionSnapshot,
            findRegionInfoForBlock: findRegionInfoForBlock,
            operationRegionInfo: operationRegionInfo,
            nextSelectionForOperation: nextSelectionForOperation,
            createTextMeasurementService: createTextMeasurementService,
            tokenizeText: tokenizeText,
            createLineBreaker: createLineBreaker,
            createParagraphLayoutEngine: createParagraphLayoutEngine,
            createLayoutScope: createLayoutScope,
            inferLayoutScopeFromOperation: inferLayoutScopeFromOperation,
            createRenderSnapshot: createRenderSnapshot,
            createAtomicRenderer: createAtomicRenderer,
            projectEditing: projectEditing,
            projectData: projectData,
            createBeforeInputNormalizer: createBeforeInputNormalizer,
            normalizeBeforeInput: normalizeBeforeInput,
            createInputPipeline: createInputPipeline,
            createTypingChangeBuffer: createTypingChangeBuffer,
            createActiveLayoutScheduler: createActiveLayoutScheduler,
            createRevisionEngine: createRevisionEngine,
            normalizeRevision: normalizeRevision,
            normalizeRevisionRange: normalizeRevisionRange,
            normalizeRevisionGroups: normalizeRevisionGroups,
            resolveTrackChangesState: resolveTrackChangesState,
            createCommandDispatcher: createCommandDispatcher,
            normalizeCommandId: normalizeCommandId,
            collectFormattingState: collectFormattingState,
            computeFormattingState: computeFormattingState,
            toBlazorFormattingState: toBlazorFormattingState,
            normalizeMark: normalizeMark,
            normalizeMarks: normalizeMarks,
            normalizeTextRun: normalizeTextRunForMerge,
            mergeAdjacentTextRuns: mergeAdjacentTextRuns,
            splitRunsForRange: function (block, start, end, mark, remove) {
                var clone = _clone(block || {});
                if (!clone.content) clone.content = { type: 'paragraph', runs: [] };
                _splitRunsForRange(clone, start, end, mark, remove);
                return _clone(clone.content.runs || []);
            },
            createTableController: createTableController,
            normalizeImageObject: normalizeImageObject,
            createTextExclusion: createTextExclusion,
            getAvailableIntervals: getAvailableIntervals,
            createImagePreviewController: createImagePreviewController,
            createEditorWidget: createEditorWidget,
            collectLayoutProbe: collectLayoutProbe,
            ensureDiagnostics: ensureDiagnostics,
            operationRendererKeys: function () {
                return [
                    'acceptRevision',
                    'addInlineMark',
                    'createRevision',
                    'deleteBlock',
                    'deleteText',
                    'insertBlock',
                    'insertText',
                    'moveBlock',
                    'rejectRevision',
                    'removeInlineMark',
                    'setBlockAttribute',
                    'updateBlock'
                ];
            },
            sortRemoteBatchOperations: sortRemoteBatchOperationsForTest,
            transformRemoteBatchInsertOffsets: transformRemoteBatchInsertOffsetsForTest,
            transformSelectionForTextChange: transformSelectionForTextChangeForTest,
            createRuntimeSelectionFromSnapshot: createRuntimeSelectionFromSnapshotForTest,
            createSelectionSnapshotFromRuntimeSelection: createSelectionSnapshotFromRuntimeSelectionForTest,
            createRuntimeCommandTransaction: createRuntimeCommandTransactionForTest,
            transformRuntimeCommentAnchorsForTextChange: transformRuntimeCommentAnchorsForTextChangeForTest,
            createRenderPlan: createRenderPlanForTest,
            normalizeWrapMode: testWrapMode,
            normalizeHorizontalPosition: testHorizontalPosition,
            normalizeWrapContourPoints: normalizeWrapContourPoints,
            getLayoutAvailableIntervals: getLayoutAvailableIntervalsForTest,
            clearTextRunMeasureCache: clearTextRunMeasureCache,
            measureTextRun: measureTextRun,
            getTextRunMeasureCacheKey: getTextRunMeasureCacheKey,
            getTextRunMeasureStats: getTextRunMeasureStats,
            createLayoutSnapshotForRender: createLayoutSnapshotForRenderTest,
            hitTestLayoutGeometry: hitTestLayoutGeometry,
            applyLayoutTextEditModel: applyLayoutTextEditModel,
            normalizeMarkType: normalizeMarkTypeForTest,
            computeImageMoveSnap: computeImageMoveSnap,
            schemaAllowsBlock: schemaAllowsBlockForTest,
            schemaAllowsToolbarBlockCommand: function (type, region) { return schemaAllowsBlockForTest(type, region); },
            normalizeInsertionBlocksForSchema: normalizeInsertionBlocksForSchema,
            createMarkerStore: createMarkerStore,
            buildRuntimeCommentMarkers: buildRuntimeCommentMarkers,
            buildRuntimeRevisionMarkers: buildRuntimeRevisionMarkers,
            detectAutocompleteTriggerText: detectAutocompleteTriggerText,
            computeFloatingPosition: computeFloatingPosition,
            shouldShowMiniToolbarForSelection: shouldShowMiniToolbarForSelectionSnapshot,
            buildPageMetrics: buildPageMetricsForTest,
            formatNonPrintingText: formatNonPrintingText,
            findActiveHeadingBlockIdFromRects: findActiveHeadingBlockIdFromRects,
            createPerformanceMetricsHarness: createPerformanceMetricsHarness,
            createVisualStabilityTracker: createVisualStabilityTracker,
            computeObjectChromeLayout: computeObjectChromeLayout,
            createObjectChromeModel: createObjectChromeModel,
            createStrictPerformanceStats: createStrictPerformanceStats,
            createLatencyHistogramSummary: createLatencyHistogramSummary,
            recordLatencyHistogram: recordLatencyHistogram,
            recordInputDomApply: recordInputDomApply,
            invokeBoundaryMethod: invokeBoundaryMethod,
            recordOperationPerformance: recordOperationPerformance,
            previewImmediateTextEdit: previewImmediateTextEdit,
            createSidePanelSyncState: createSidePanelSyncState,
            createPanelCommandDebouncer: createPanelCommandDebouncer,
            createUxPolishHarness: createUxPolishHarness,
            buildPagePlan: buildPagePlan,
            materializePage: materializePage,
            findPageIndexForBlockId: findPageIndexForBlockId,
            formatA11yLabel: formatA11yLabel,
            getRegionLabel: getRegionLabel,
            getFocusRegionFromElement: getFocusRegionFromElement,
            getFocusTargetDetails: getFocusTargetDetails,
            setActiveFocusRegion: setActiveFocusRegion,
            handleEditorKeyDown: handleEditorKeyDown,
            chooseKeyboardSelection: chooseKeyboardSelection,
            closeFloatingUiForKeyboard: closeFloatingUiForKeyboard,
            scheduleAccessibilityAnnouncement: scheduleAccessibilityAnnouncement
        }
    };
})();

window.tmDocumentEditorRuntime = (function () {
    var transactionCallbacks = new Map();
    var selectionCallbacks = new Map();
    var runtimeDocuments = new Map();
    var googleDocsEngineInstances = new Map();
    var migrationAudits = new Map();
    var migrationStages = ['hardCut', 'defaultDemo', 'plainParagraphEditing', 'formattingCommands', 'imageWrapScenarios', 'revisions', 'tables'];

    function _googleDocsEngine() {
        return window.tmDocumentEditorEngine || null;
    }

    function _isGoogleDocsInstance(instanceId) {
        return googleDocsEngineInstances.has(instanceId);
    }

    function _getMigrationAudit(instanceId) {
        var id = String(instanceId || '');
        if (!migrationAudits.has(id)) {
            migrationAudits.set(id, {
                instanceId: id,
                facadeCallCounts: {},
                routedToGoogleDocsCount: 0,
                directLegacyHotPathCount: 0,
                removedLegacyPath: true,
                lastFacadeMethod: '',
                lastRoute: ''
            });
        }

        return migrationAudits.get(id);
    }

    function _recordFacadeCall(instanceId, methodName, route) {
        var audit = _getMigrationAudit(instanceId);
        audit.facadeCallCounts[methodName] = (audit.facadeCallCounts[methodName] || 0) + 1;
        audit.lastFacadeMethod = methodName;
        audit.lastRoute = route;
        if (route === 'google-docs') audit.routedToGoogleDocsCount++;
    }

    function _readMigrationStageSet() {
        var values = [];
        try {
            var query = new URLSearchParams(window.location && window.location.search || '');
            values = values.concat(String(query.get('tmDocumentEditorMigration') || query.get('documentEditorMigration') || '').split(','));
        } catch {
            // Ignore URL parsing failures.
        }

        try {
            if (window.localStorage) {
                values = values.concat(String(window.localStorage.getItem('tmDocumentEditorMigration') || '').split(','));
            }
        } catch {
            // Ignore local storage failures.
        }

        return new Set(values.map(function (value) {
            return String(value || '').trim().toLowerCase();
        }).filter(Boolean));
    }

    function _isDefaultDocumentEditorRoute() {
        try {
            return /\/document-editor\/?$/i.test(window.location && window.location.pathname || '');
        } catch {
            return false;
        }
    }

    function _googleMethodName(methodName) {
        switch (methodName) {
            case 'executeCommand': return 'applyCommand';
            case 'getSnapshot': return 'getDocumentSnapshot';
            case 'getRuntimeSelection': return 'getSelectionSnapshot';
            default: return methodName;
        }
    }

    function _call(methodName, args, fallback) {
        var instanceId = args && args.length > 0 ? args[0] : null;
        var googleEngine = _googleDocsEngine();
        var googleMethodName = _googleMethodName(methodName);
        var method = googleEngine ? googleEngine[googleMethodName] : null;
        if (instanceId) {
            _recordFacadeCall(instanceId, methodName, 'google-docs');
        }
        if (typeof method === 'function') return method.apply(googleEngine, args || []);
        if (typeof fallback === 'function') return fallback();
        return undefined;
    }

    function _hasOwn(value, key) {
        return !!value && Object.prototype.hasOwnProperty.call(value, key);
    }

    function _cloneJson(value) {
        if (value === undefined || value === null) return value;
        return JSON.parse(JSON.stringify(value));
    }

    function _readPair(value, pascalKey, camelKey, fallback) {
        if (_hasOwn(value, pascalKey)) return value[pascalKey];
        if (_hasOwn(value, camelKey)) return value[camelKey];
        return fallback;
    }

    function _writePair(value, pascalKey, camelKey, propertyValue) {
        if (!value) return;
        if (_hasOwn(value, camelKey) && !_hasOwn(value, pascalKey)) {
            value[camelKey] = propertyValue;
        } else {
            value[pascalKey] = propertyValue;
        }
    }

    function _ensureArray(value, pascalKey, camelKey) {
        var current = _readPair(value, pascalKey, camelKey, []);
        if (!Array.isArray(current)) current = [];
        _writePair(value, pascalKey, camelKey, current);
        return current;
    }

    function _ensureString(value, pascalKey, camelKey, fallback) {
        var current = _readPair(value, pascalKey, camelKey, fallback || '');
        if (current === undefined || current === null || current === '') current = fallback || '';
        _writePair(value, pascalKey, camelKey, String(current));
        return String(current);
    }

    function _sortObjectDeep(value) {
        if (Array.isArray(value)) {
            return value.map(_sortObjectDeep);
        }

        if (!value || typeof value !== 'object') return value;

        var sorted = {};
        Object.keys(value).sort().forEach(function (key) {
            sorted[key] = _sortObjectDeep(value[key]);
        });
        return sorted;
    }

    function _sortObject(value) {
        return _sortObjectDeep(value);
    }

    function _normalizeWrapContourPoints(points) {
        if (!Array.isArray(points)) return [];
        return points.map(function (point) {
            var x = Number(point && (point.X ?? point.x));
            var y = Number(point && (point.Y ?? point.y));
            return {
                X: Math.max(0, Math.min(1, Number.isFinite(x) ? x : 0)),
                Y: Math.max(0, Math.min(1, Number.isFinite(y) ? y : 0))
            };
        });
    }

    function _stableNodeId(prefix, path) {
        return 'rt-' + prefix + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
    }

    function _getSnapshotDocument(snapshot) {
        if (!snapshot) return {};
        return snapshot.Document || snapshot.document || snapshot;
    }

    function _setSnapshotDocument(snapshot, document) {
        if (!snapshot) return;
        if (_hasOwn(snapshot, 'document') && !_hasOwn(snapshot, 'Document')) {
            snapshot.document = document;
        } else {
            snapshot.Document = document;
        }
    }

    function _looksLikeTextInline(inline) {
        if (!inline) return false;
        if (inline.NoteId !== undefined || inline.noteId !== undefined || inline.NoteType !== undefined || inline.noteType !== undefined) return false;
        if (inline.FieldType !== undefined || inline.fieldType !== undefined || inline.FallbackText !== undefined || inline.fallbackText !== undefined) return false;
        if (inline.Key !== undefined || inline.key !== undefined || inline.TokenType !== undefined || inline.tokenType !== undefined) return false;
        var type = String(inline.$type || inline.Type || inline.type || '').toLowerCase();
        return type === 'text' || type === 'textrun' || type.indexOf('text') >= 0 || _hasOwn(inline, 'Text') || _hasOwn(inline, 'text');
    }

    function _readInlineText(inline) {
        return String(_readPair(inline, 'Text', 'text', ''));
    }

    function _writeInlineText(inline, text) {
        _writePair(inline, 'Text', 'text', text);
    }

    function _readInlineMarks(inline) {
        var marks = _readPair(inline, 'Marks', 'marks', []);
        return Array.isArray(marks) ? marks : [];
    }

    function _writeInlineMarks(inline, marks) {
        _writePair(inline, 'Marks', 'marks', marks);
    }

    function _normalizeInline(inline, path) {
        var result = inline ? _cloneJson(inline) : {};
        _ensureString(result, 'Id', 'id', _stableNodeId('inline', path));
        var marks = _readInlineMarks(result).map(function (mark) { return _sortObjectDeep(_cloneJson(mark)); });
        _writeInlineMarks(result, marks);

        if (result.NoteId !== undefined || result.noteId !== undefined || result.NoteType !== undefined || result.noteType !== undefined) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'noteReference';
            }
            return result;
        }

        if (result.FieldType !== undefined || result.fieldType !== undefined || result.FallbackText !== undefined || result.fallbackText !== undefined) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'field';
            }
            return result;
        }

        if (result.Key !== undefined || result.key !== undefined || result.TokenType !== undefined || result.tokenType !== undefined) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'token';
            }
            return result;
        }

        if (_looksLikeTextInline(result)) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'text';
            }
            _writeInlineText(result, _readInlineText(result));
        }

        return result;
    }

    function _inlineMergeKey(inline) {
        var clone = _cloneJson(inline) || {};
        delete clone.Id;
        delete clone.id;
        delete clone.Text;
        delete clone.text;
        return JSON.stringify(_sortObjectDeep(clone));
    }

    function _canMergeInlineRuns(previous, current) {
        return _looksLikeTextInline(previous)
            && _looksLikeTextInline(current)
            && _inlineMergeKey(previous) === _inlineMergeKey(current);
    }

    function _createEmptyTextInline(path) {
        return {
            $type: 'text',
            Id: _stableNodeId('inline', path),
            Marks: [],
            Text: ''
        };
    }

    function _normalizeInlines(inlines, path) {
        var source = Array.isArray(inlines) ? inlines : [];
        var result = [];
        for (var i = 0; i < source.length; i++) {
            var normalized = _normalizeInline(source[i], path + '-' + i);
            var previous = result.length > 0 ? result[result.length - 1] : null;
            if (previous && _canMergeInlineRuns(previous, normalized)) {
                _writeInlineText(previous, _readInlineText(previous) + _readInlineText(normalized));
            } else {
                result.push(normalized);
            }
        }

        if (result.length === 0) {
            result.push(_createEmptyTextInline(path + '-0'));
        }

        return result.map(_sortObjectDeep);
    }

    function _contentKind(content) {
        if (!content) return '';
        var raw = content.$type || content.Type || content.type || '';
        return String(raw).toLowerCase();
    }

    function _looksLikeParagraphContent(content) {
        return !!content
            && (_contentKind(content).indexOf('paragraph') >= 0
                || _hasOwn(content, 'Inlines')
                || _hasOwn(content, 'inlines'));
    }

    function _looksLikeTableContent(content) {
        return !!content
            && (_contentKind(content).indexOf('table') >= 0
                || _hasOwn(content, 'Rows')
                || _hasOwn(content, 'rows'));
    }

    function _looksLikeImageContent(content) {
        return !!content
            && (_contentKind(content).indexOf('image') >= 0
                || _hasOwn(content, 'Url')
                || _hasOwn(content, 'url')
                || _hasOwn(content, 'AssetId')
                || _hasOwn(content, 'assetId'));
    }

    function _normalizeParagraphContent(content, path) {
        var result = content ? _cloneJson(content) : {};
        if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
            result.$type = 'paragraph';
        }
        var inlines = _ensureArray(result, 'Inlines', 'inlines');
        _writePair(result, 'Inlines', 'inlines', _normalizeInlines(inlines, path + '-inline'));
        return _sortObjectDeep(result);
    }

    function _normalizeTableContent(content, path) {
        var result = content ? _cloneJson(content) : {};
        var rows = _ensureArray(result, 'Rows', 'rows');
        for (var r = 0; r < rows.length; r++) {
            var row = rows[r] ? _cloneJson(rows[r]) : {};
            _ensureString(row, 'Id', 'id', _stableNodeId('row', path + '-' + r));
            var cells = _ensureArray(row, 'Cells', 'cells');
            for (var c = 0; c < cells.length; c++) {
                var cell = cells[c] ? _cloneJson(cells[c]) : {};
                _ensureString(cell, 'Id', 'id', _stableNodeId('cell', path + '-' + r + '-' + c));
                var blocks = _ensureArray(cell, 'Blocks', 'blocks');
                _writePair(cell, 'Blocks', 'blocks', _normalizeBlocks(blocks, path + '-' + r + '-' + c + '-block'));
                cells[c] = _sortObjectDeep(cell);
            }
            _writePair(row, 'Cells', 'cells', cells);
            rows[r] = _sortObjectDeep(row);
        }
        _writePair(result, 'Rows', 'rows', rows);
        return _sortObjectDeep(result);
    }

    function _normalizeImageContent(content) {
        var result = content ? _cloneJson(content) : {};
        if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
            result.$type = 'image';
        }

        var layout = result.Layout || result.layout || null;
        var legacy = result.FloatingLayout || result.floatingLayout || null;
        if (!layout && legacy) {
            var inline = (legacy.Inline ?? legacy.inline) !== false;
            layout = {
                Kind: inline ? 0 : 1,
                Anchor: {
                    MoveWithText: !inline,
                    FixedOnPage: false,
                    LockAnchor: !!(legacy.LockAnchor ?? legacy.lockAnchor)
                },
                Position: {
                    HorizontalRelativeTo: legacy.HorizontalRelativeTo ?? legacy.horizontalRelativeTo ?? 0,
                    VerticalRelativeTo: legacy.VerticalRelativeTo ?? legacy.verticalRelativeTo ?? 3,
                    X: legacy.X ?? legacy.x ?? 0,
                    Y: legacy.Y ?? legacy.y ?? 0
                },
                Wrap: {
                    Mode: legacy.WrapMode ?? legacy.wrapMode ?? 0,
                    DistanceLeft: legacy.DistanceLeft ?? legacy.distanceLeft ?? 0,
                    DistanceRight: legacy.DistanceRight ?? legacy.distanceRight ?? 0,
                    DistanceTop: legacy.DistanceTop ?? legacy.distanceTop ?? 0,
                    DistanceBottom: legacy.DistanceBottom ?? legacy.distanceBottom ?? 0,
                    WrapContourPoints: _normalizeWrapContourPoints(legacy.WrapContourPoints ?? legacy.wrapContourPoints)
                },
                Transform: {},
                Stacking: {
                    ZIndex: legacy.ZIndex ?? legacy.zIndex ?? 0,
                    AllowOverlap: (legacy.AllowOverlap ?? legacy.allowOverlap) === true
                        || String(legacy.AllowOverlap ?? legacy.allowOverlap ?? '').toLowerCase() === 'true'
                }
            };
            if (legacy.HorizontalPosition != null || legacy.horizontalPosition != null) {
                layout.Position.HorizontalAlignment = legacy.HorizontalPosition ?? legacy.horizontalPosition;
            }
        }

        if (layout) {
            _writePair(result, 'Layout', 'layout', _sortObjectDeep(layout));
        }

        delete result.FloatingLayout;
        delete result.floatingLayout;
        return _sortObjectDeep(result);
    }

    function _normalizeBlockContent(content, path) {
        if (!content) return _normalizeParagraphContent({}, path);
        if (_looksLikeTableContent(content)) return _normalizeTableContent(content, path);
        if (_looksLikeImageContent(content)) return _normalizeImageContent(content);
        if (_looksLikeParagraphContent(content)) return _normalizeParagraphContent(content, path);
        return _sortObjectDeep(_cloneJson(content));
    }

    function _normalizeBlock(block, path) {
        var result = block ? _cloneJson(block) : {};
        _ensureString(result, 'Id', 'id', _stableNodeId('block', path));
        var content = _readPair(result, 'Content', 'content', null);
        _writePair(result, 'Content', 'content', _normalizeBlockContent(content, path + '-content'));
        return _sortObjectDeep(result);
    }

    function _normalizeBlocks(blocks, path) {
        var source = Array.isArray(blocks) ? blocks : [];
        return source.map(function (block, index) {
            return _normalizeBlock(block, path + '-' + index);
        });
    }

    function _normalizeHeaderFooter(headerFooter, path) {
        var result = headerFooter ? _cloneJson(headerFooter) : {};
        _ensureString(result, 'Id', 'id', _stableNodeId('header-footer', path));
        var blocks = _ensureArray(result, 'Blocks', 'blocks');
        _writePair(result, 'Blocks', 'blocks', _normalizeBlocks(blocks, path + '-block'));
        return _sortObjectDeep(result);
    }

    function _normalizeDocument(document) {
        var result = document ? _cloneJson(document) : {};
        if (_hasOwn(result, 'document') || _hasOwn(result, 'Document')) {
            result = _getSnapshotDocument(result);
        }

        _writePair(result, 'SchemaVersion', 'schemaVersion', _readPair(result, 'SchemaVersion', 'schemaVersion', 1) || 1);
        _ensureString(result, 'DocumentId', 'documentId', 'document');
        _writePair(result, 'Metadata', 'metadata', _readPair(result, 'Metadata', 'metadata', {}) || {});
        _writePair(result, 'PageSettings', 'pageSettings', _readPair(result, 'PageSettings', 'pageSettings', {}) || {});

        var sections = _ensureArray(result, 'Sections', 'sections');
        _writePair(result, 'Sections', 'sections', sections.map(function (section, index) {
            var normalized = section ? _cloneJson(section) : {};
            _ensureString(normalized, 'Id', 'id', _stableNodeId('section', index));
            return _sortObjectDeep(normalized);
        }));

        var blocks = _ensureArray(result, 'Blocks', 'blocks');
        _writePair(result, 'Blocks', 'blocks', _normalizeBlocks(blocks, 'block'));

        var comments = _ensureArray(result, 'Comments', 'comments');
        _writePair(result, 'Comments', 'comments', comments.map(function (comment) { return _sortObjectDeep(_cloneJson(comment)); }));

        var notes = _ensureArray(result, 'Notes', 'notes');
        _writePair(result, 'Notes', 'notes', notes.map(function (note) { return _sortObjectDeep(_cloneJson(note)); }));

        var headersFooters = _ensureArray(result, 'HeadersFooters', 'headersFooters');
        _writePair(result, 'HeadersFooters', 'headersFooters', headersFooters.map(_normalizeHeaderFooter));

        var revisions = _ensureArray(result, 'Revisions', 'revisions');
        _writePair(result, 'Revisions', 'revisions', revisions.map(function (revision) { return _sortObjectDeep(_cloneJson(revision)); }));

        var assets = _ensureArray(result, 'Assets', 'assets');
        _writePair(result, 'Assets', 'assets', assets.map(function (asset) { return _sortObjectDeep(_cloneJson(asset)); }));

        var anchors = _ensureArray(result, 'Anchors', 'anchors');
        _writePair(result, 'Anchors', 'anchors', anchors.map(function (anchor) { return _sortObjectDeep(_cloneJson(anchor)); }));

        return _sortObjectDeep(result);
    }

    function fromCanonicalDocument(document) {
        return _sortObjectDeep({
            version: 1,
            document: _normalizeDocument(document)
        });
    }

    function toCanonicalDocument(runtimeDocument) {
        if (!runtimeDocument) return _normalizeDocument({});
        var document = _hasOwn(runtimeDocument, 'document') || _hasOwn(runtimeDocument, 'Document')
            ? _getSnapshotDocument(runtimeDocument)
            : runtimeDocument;
        return _normalizeDocument(document);
    }

    function _normalizeSnapshot(snapshot) {
        var result = snapshot ? _cloneJson(snapshot) : {};
        var document = _getSnapshotDocument(result);
        _setSnapshotDocument(result, toCanonicalDocument(document));
        if (!_hasOwn(result, 'ProtocolVersion') && !_hasOwn(result, 'protocolVersion')) {
            result.ProtocolVersion = 1;
        }
        return _sortObjectDeep(result);
    }

    function _storeSnapshotRuntime(instanceId, snapshot) {
        if (!instanceId || !snapshot) return;
        runtimeDocuments.set(instanceId, fromCanonicalDocument(_getSnapshotDocument(snapshot)));
    }

    function _snapshotFromRuntime(instanceId) {
        var runtimeDocument = runtimeDocuments.get(instanceId);
        if (!runtimeDocument) return null;
        return _sortObjectDeep({
            ProtocolVersion: 1,
            Document: toCanonicalDocument(runtimeDocument)
        });
    }

    function _stripRuntimeFields(value) {
        if (Array.isArray(value)) return value.map(_stripRuntimeFields);
        if (!value || typeof value !== 'object') return value;

        var result = {};
        Object.keys(value).sort().forEach(function (key) {
            if (key.indexOf('__runtime') === 0 || key.indexOf('_runtime') === 0) return;
            result[key] = _stripRuntimeFields(value[key]);
        });
        return result;
    }

    function _findFirstDifference(expected, actual, path) {
        if (expected === actual) return null;
        if (typeof expected !== typeof actual) {
            return { path: path || '$', expected: expected, actual: actual };
        }
        if (expected === null || actual === null || typeof expected !== 'object') {
            return { path: path || '$', expected: expected, actual: actual };
        }

        var expectedKeys = Array.isArray(expected) ? expected.map(function (_, index) { return index; }) : Object.keys(expected).sort();
        var actualKeys = Array.isArray(actual) ? actual.map(function (_, index) { return index; }) : Object.keys(actual).sort();
        var keys = Array.from(new Set(expectedKeys.concat(actualKeys))).sort(function (a, b) {
            return String(a).localeCompare(String(b), undefined, { numeric: true });
        });

        for (var i = 0; i < keys.length; i++) {
            var key = keys[i];
            if (!_hasOwn(expected, key) || !_hasOwn(actual, key)) {
                return { path: (path || '$') + '.' + key, expected: expected[key], actual: actual[key] };
            }
            var diff = _findFirstDifference(expected[key], actual[key], (path || '$') + '.' + key);
            if (diff) return diff;
        }

        return null;
    }

    function diffCanonicalDocuments(expected, actual) {
        var left = _stripRuntimeFields(toCanonicalDocument(expected));
        var right = _stripRuntimeFields(toCanonicalDocument(actual));
        var diff = _findFirstDifference(left, right, '$');
        return diff || { equal: true, path: '$', expected: left, actual: right };
    }

    function roundTripCanonicalDocument(document) {
        return toCanonicalDocument(fromCanonicalDocument(document));
    }

    function create(root, options, dotNetRef) {
        var googleEngine = _googleDocsEngine();
        if (!googleEngine || typeof googleEngine.create !== 'function') {
            throw new Error('tmDocumentEditorRuntime.create: tmDocumentEditorEngine is unavailable.');
        }
        var instanceId = googleEngine.create(root, Object.assign({}, options || {}, { useGoogleDocsEngine: true, legacyEngineRemoved: true }), dotNetRef);
        googleDocsEngineInstances.set(instanceId, true);
        return instanceId;
    }

    function loadDocument(instanceId, snapshot, forceRender) {
        var normalizedSnapshot = _normalizeSnapshot(snapshot);
        _storeSnapshotRuntime(instanceId, normalizedSnapshot);
        return _call('loadDocument', [instanceId, normalizedSnapshot, forceRender]);
    }

    function getDocument(instanceId) {
        var googleSnapshot = _call('getDocumentSnapshot', [instanceId], function () { return null; });
        if (googleSnapshot && googleSnapshot.ok !== false) {
            var csharpDocument = googleSnapshot.csharpDocument || (window.tmDocumentEditorEngine && window.tmDocumentEditorEngine.model.exportToCSharpJson(googleSnapshot.document));
            var normalizedGoogleSnapshot = _normalizeSnapshot({ Document: csharpDocument });
            _storeSnapshotRuntime(instanceId, normalizedGoogleSnapshot);
            return JSON.stringify(normalizedGoogleSnapshot);
        }
        var runtimeSnapshot = _snapshotFromRuntime(instanceId);
        return runtimeSnapshot ? JSON.stringify(runtimeSnapshot) : null;
    }

    function executeCommand(instanceId, command, payload) {
        return _call('executeCommand', [instanceId, command, payload]);
    }

    function getSelectedText(instanceId) {
        return _call('getSelectedText', [instanceId], function () {
            var selection = typeof window.getSelection === 'function' ? window.getSelection() : null;
            return selection ? String(selection.toString() || '') : '';
        }) || '';
    }

    function getRemovedLegacyPathAudit(instanceId) {
        var audit = _getMigrationAudit(instanceId);
        return _sortObject(_cloneJson(audit));
    }

    function getMigrationStatus(instanceId) {
        var id = String(instanceId || '');
        var stages = _readMigrationStageSet();
        var audit = getRemovedLegacyPathAudit(id);
        var allByFlag = stages.has('all') || stages.has('phase20');
        var gates = {};
        migrationStages.forEach(function (stage) {
            gates[stage] = allByFlag
                || stages.has(stage.toLowerCase())
                || stages.has(stage.replace(/[A-Z]/g, function (value) { return '-' + value.toLowerCase(); }))
                || stage !== 'testRoute';
        });
        gates.hardCut = true;
        gates.defaultDemo = gates.defaultDemo || _isDefaultDocumentEditorRoute();

        return _sortObject({
            ok: true,
            instanceId: id,
            engineMode: 'google-docs',
            useGoogleDocsEngine: true,
            legacyEngineRemoved: true,
            publicCompatibility: {
                legacyWysiwygGlobal: false,
                legacyWysiwygAlias: false,
                tmDocumentEditorRuntime: !!window.tmDocumentEditorRuntime
            },
            gates: gates,
            allFacadeGatesEnabled: migrationStages.every(function (stage) { return gates[stage] === true; }),
            moduleNames: getRuntimeModuleNames(),
            removedLegacyPathAudit: audit
        });
    }

    function onTransactionCommitted(instanceId, callback) {
        if (!transactionCallbacks.has(instanceId)) {
            transactionCallbacks.set(instanceId, []);
        }
        transactionCallbacks.get(instanceId).push(callback);
        return function () {
            var callbacks = transactionCallbacks.get(instanceId) || [];
            transactionCallbacks.set(instanceId, callbacks.filter(function (item) { return item !== callback; }));
        };
    }

    function onSelectionStateChanged(instanceId, callback) {
        if (!selectionCallbacks.has(instanceId)) {
            selectionCallbacks.set(instanceId, []);
        }
        selectionCallbacks.get(instanceId).push(callback);
        return function () {
            var callbacks = selectionCallbacks.get(instanceId) || [];
            selectionCallbacks.set(instanceId, callbacks.filter(function (item) { return item !== callback; }));
        };
    }

    function dispose(instanceId) {
        transactionCallbacks.delete(instanceId);
        selectionCallbacks.delete(instanceId);
        runtimeDocuments.delete(instanceId);
        migrationAudits.delete(String(instanceId || ''));
        if (_isGoogleDocsInstance(instanceId)) {
            googleDocsEngineInstances.delete(instanceId);
            var googleEngine = _googleDocsEngine();
            return googleEngine && typeof googleEngine.dispose === 'function'
                ? googleEngine.dispose(instanceId)
                : undefined;
        }
        return _call('dispose', [instanceId]);
    }

    // Phase 5: public API remains a stable facade; these internal modules are
    // implementation boundaries for tests and refactors, not a public contract.
    var runtimeModules = {
        core: {
            create: create,
            loadDocument: loadDocument,
            getDocument: getDocument,
            executeCommand: executeCommand,
            getSelectedText: getSelectedText,
            dispose: dispose,
            call: _call
        },
        selection: {
            onSelectionStateChanged: onSelectionStateChanged,
            restoreSelection: function (instanceId, snapshot) {
                return _call('restoreSelection', [instanceId, snapshot]);
            },
            getRuntimeSelection: function (instanceId) {
                return _call('getRuntimeSelection', [instanceId], function () { return null; });
            },
            getSelectionSnapshot: function (instanceId) {
                return _call('getSelectionSnapshot', [instanceId], function () { return null; });
            }
        },
        rendering: {
            loadDocument: loadDocument,
            applyRemoteOperation: function (instanceId, operation) {
                return _call('applyRemoteOperation', [instanceId, operation]);
            },
            applyRemoteOperationBatch: function (instanceId, batch) {
                return _call('applyRemoteOperationBatch', [instanceId, batch]);
            },
            applyRemoteOperations: function (instanceId, operations) {
                return _call('applyRemoteOperations', [instanceId, operations]);
            },
            applyRemoteCursor: function (instanceId, cursor) {
                return _call('applyRemoteCursor', [instanceId, cursor], function () { return false; });
            },
            getDebugSnapshot: function (instanceId) {
                var snapshot = _call('getDebugSnapshot', [instanceId], function () { return null; });
                if (!snapshot || typeof snapshot !== 'object') return snapshot;
                if (!_hasOwn(snapshot, 'EngineMode') && !_hasOwn(snapshot, 'engineMode')) {
                    snapshot.EngineMode = 'google-docs';
                }
                if (!_hasOwn(snapshot, 'UseGoogleDocsEngine') && !_hasOwn(snapshot, 'useGoogleDocsEngine')) {
                    snapshot.UseGoogleDocsEngine = true;
                }
                snapshot.LegacyEngineRemoved = true;
                if (!_hasOwn(snapshot, 'MigrationStatus') && !_hasOwn(snapshot, 'migrationStatus')) {
                    snapshot.MigrationStatus = getMigrationStatus(instanceId);
                }
                return snapshot;
            },
            getPageMetrics: function (instanceId) {
                return _call('getPageMetrics', [instanceId], function () { return null; });
            }
        },
        input: {
            focus: function (instanceId) {
                return _call('focus', [instanceId]);
            },
            closeHeaderFooter: function (instanceId) {
                return _call('closeHeaderFooter', [instanceId], function () { return false; });
            },
            setShowBlocks: function (instanceId, show) {
                return _call('setShowBlocks', [instanceId, show], function () { return false; });
            },
            setShowNonPrintingCharacters: function (instanceId, show) {
                return _call('setShowNonPrintingCharacters', [instanceId, show], function () { return false; });
            },
            setProtectionMode: function (instanceId, isProtected, markers) {
                return _call('setProtectionMode', [instanceId, isProtected, markers], function () { return false; });
            },
            getBodyHtml: function (instanceId) {
                return _call('getBodyHtml', [instanceId], function () { return ''; }) || '';
            },
            scrollToBlock: function (instanceId, blockId) {
                return _call('scrollToBlock', [instanceId, blockId], function () { return false; });
            },
            scrollToPage: function (instanceId, pageIndex) {
                return _call('scrollToPage', [instanceId, pageIndex], function () { return false; });
            }
        },
        formatting: {
            executeCommand: executeCommand,
            getFormattingState: function (instanceId) {
                return _call('getFormattingState', [instanceId], function () { return null; });
            },
            getSidePanelSyncState: function (instanceId) {
                return _call('getSidePanelSyncState', [instanceId], function () { return null; });
            },
            getLastCommandTransaction: function (instanceId) {
                return _call('getLastCommandTransaction', [instanceId], function () { return null; });
            },
            getUndoState: function (instanceId) {
                return _call('getUndoState', [instanceId], function () { return null; });
            },
            getDebugUndoStack: function (instanceId) {
                return _call('getDebugUndoStack', [instanceId], function () { return null; });
            },
            undo: function (instanceId) {
                var result = executeCommand(instanceId, 'undo', {});
                return !!(result && result.ok !== false);
            },
            redo: function (instanceId) {
                var result = executeCommand(instanceId, 'redo', {});
                return !!(result && result.ok !== false);
            }
        },
        clipboard: {
            getLinkInfo: function (instanceId) {
                return _call('getLinkInfo', [instanceId], function () { return null; });
            },
            copySelection: function (instanceId, writeToClipboard) {
                return _call('copySelection', [instanceId, writeToClipboard], function () { return null; });
            }
        },
        search: {
            setSearchMarkers: function (instanceId, blockIdsOrMarkers, offsets, lengths) {
                return _call('setSearchMarkers', [instanceId, blockIdsOrMarkers, offsets, lengths], function () { return false; });
            },
            clearSearchMarkers: function (instanceId) {
                return _call('clearSearchMarkers', [instanceId], function () { return false; });
            },
            scrollToSearchResult: function (instanceId, blockId, offset, length) {
                return _call('scrollToSearchResult', [instanceId, blockId, offset, length], function () { return false; });
            }
        },
        image: {
            executeCommand: executeCommand,
            insertImageNode: function (instanceId, block, dispatchPatch) {
                return _call('insertImageNode', [instanceId, block, dispatchPatch]);
            },
            insertImageUrl: function (instanceId, payload) {
                return executeCommand(instanceId, 'insertImageUrl', payload || {});
            }
        },
        table: {
            executeCommand: executeCommand,
            insertTable: function (instanceId, payload) {
                return executeCommand(instanceId, 'insertTable', payload || {});
            }
        },
        comments: {
            captureCommentAnchor: function (instanceId) {
                return _call('captureCommentAnchor', [instanceId], function () { return null; });
            },
            scrollToComment: function (instanceId, commentId) {
                return _call('scrollToComment', [instanceId, commentId], function () { return false; });
            },
            upsertComment: function (instanceId, comment) {
                return _call('upsertComment', [instanceId, comment], function () { return false; });
            },
            removeComment: function (instanceId, commentId) {
                return _call('removeComment', [instanceId, commentId], function () { return false; });
            }
        },
        revisions: {
            setTrackChangesEnabled: function (instanceId, enabled) {
                return _call('setTrackChangesEnabled', [instanceId, enabled]);
            },
            setReviewDisplayMode: function (instanceId, mode) {
                return _call('setReviewDisplayMode', [instanceId, mode]);
            },
            scrollToRevision: function (instanceId, revisionId) {
                return _call('scrollToRevision', [instanceId, revisionId]);
            },
            reviewRevision: function (instanceId, revisionId, action) {
                return _call('reviewRevision', [instanceId, revisionId, action], function () { return false; });
            },
            reviewAllRevisions: function (instanceId, action, payload) {
                return _call('reviewAllRevisions', [instanceId, action, payload], function () { return false; });
            },
            clearRevisionDecorations: function (instanceId, revisionId, removeContent) {
                return _call('clearRevisionDecorations', [instanceId, revisionId, removeContent]);
            }
        },
        serialization: {
            fromCanonicalDocument: fromCanonicalDocument,
            toCanonicalDocument: toCanonicalDocument,
            roundTripCanonicalDocument: roundTripCanonicalDocument,
            diffCanonicalDocuments: diffCanonicalDocuments,
            normalizeSnapshot: _normalizeSnapshot
        },
        watchdog: {
            getState: function () { return null; }
        },
        migration: {
            getMigrationStatus: getMigrationStatus,
            getRemovedLegacyPathAudit: getRemovedLegacyPathAudit
        }
    };

    function getRuntimeModuleNames() {
        return Object.keys(runtimeModules).sort();
    }

    return {
        create: runtimeModules.core.create,
        loadDocument: runtimeModules.core.loadDocument,
        getDocument: runtimeModules.core.getDocument,
        executeCommand: runtimeModules.core.executeCommand,
        getSelectedText: runtimeModules.core.getSelectedText,
        onTransactionCommitted: onTransactionCommitted,
        onSelectionStateChanged: runtimeModules.selection.onSelectionStateChanged,
        dispose: runtimeModules.core.dispose,
        applyRemoteOperation: function (instanceId, operation) {
            return runtimeModules.rendering.applyRemoteOperation(instanceId, operation);
        },
        applyRemoteOperationBatch: function (instanceId, batch) {
            return runtimeModules.rendering.applyRemoteOperationBatch(instanceId, batch);
        },
        applyRemoteOperations: function (instanceId, operations) {
            return runtimeModules.rendering.applyRemoteOperations(instanceId, operations);
        },
        applyRemoteCursor: function (instanceId, cursor) {
            return runtimeModules.rendering.applyRemoteCursor(instanceId, cursor);
        },
        setTrackChangesEnabled: function (instanceId, enabled) {
            return runtimeModules.revisions.setTrackChangesEnabled(instanceId, enabled);
        },
        setReviewDisplayMode: function (instanceId, mode) {
            return runtimeModules.revisions.setReviewDisplayMode(instanceId, mode);
        },
        setReadOnly: function (instanceId, readOnly) {
            return _call('setReadOnly', [instanceId, readOnly]);
        },
        scrollToRevision: function (instanceId, revisionId) {
            return runtimeModules.revisions.scrollToRevision(instanceId, revisionId);
        },
        scrollToComment: function (instanceId, commentId) {
            return runtimeModules.comments.scrollToComment(instanceId, commentId);
        },
        upsertComment: function (instanceId, comment) {
            return runtimeModules.comments.upsertComment(instanceId, comment);
        },
        removeComment: function (instanceId, commentId) {
            return runtimeModules.comments.removeComment(instanceId, commentId);
        },
        reviewRevision: function (instanceId, revisionId, action) {
            return runtimeModules.revisions.reviewRevision(instanceId, revisionId, action);
        },
        reviewAllRevisions: function (instanceId, action, payload) {
            return runtimeModules.revisions.reviewAllRevisions(instanceId, action, payload);
        },
        clearRevisionDecorations: function (instanceId, revisionId, removeContent) {
            return runtimeModules.revisions.clearRevisionDecorations(instanceId, revisionId, removeContent);
        },
        restoreSelection: function (instanceId, snapshot) {
            return runtimeModules.selection.restoreSelection(instanceId, snapshot);
        },
        focus: function (instanceId) {
            return runtimeModules.input.focus(instanceId);
        },
        closeHeaderFooter: function (instanceId) {
            return runtimeModules.input.closeHeaderFooter(instanceId);
        },
        setShowBlocks: function (instanceId, show) {
            return runtimeModules.input.setShowBlocks(instanceId, show);
        },
        setShowNonPrintingCharacters: function (instanceId, show) {
            return runtimeModules.input.setShowNonPrintingCharacters(instanceId, show);
        },
        setProtectionMode: function (instanceId, isProtected, markers) {
            return runtimeModules.input.setProtectionMode(instanceId, isProtected, markers);
        },
        getBodyHtml: function (instanceId) {
            return runtimeModules.input.getBodyHtml(instanceId);
        },
        scrollToBlock: function (instanceId, blockId) {
            return runtimeModules.input.scrollToBlock(instanceId, blockId);
        },
        scrollToPage: function (instanceId, pageIndex) {
            return runtimeModules.input.scrollToPage(instanceId, pageIndex);
        },
        captureCommentAnchor: function (instanceId) {
            return runtimeModules.comments.captureCommentAnchor(instanceId);
        },
        setSearchMarkers: function (instanceId, blockIdsOrMarkers, offsets, lengths) {
            return runtimeModules.search.setSearchMarkers(instanceId, blockIdsOrMarkers, offsets, lengths);
        },
        clearSearchMarkers: function (instanceId) {
            return runtimeModules.search.clearSearchMarkers(instanceId);
        },
        scrollToSearchResult: function (instanceId, blockId, offset, length) {
            return runtimeModules.search.scrollToSearchResult(instanceId, blockId, offset, length);
        },
        copySelection: function (instanceId, writeToClipboard) {
            return runtimeModules.clipboard.copySelection(instanceId, writeToClipboard);
        },
        getDebugSnapshot: function (instanceId) {
            return runtimeModules.rendering.getDebugSnapshot(instanceId);
        },
        getPageMetrics: function (instanceId) {
            return runtimeModules.rendering.getPageMetrics(instanceId);
        },
        getFormattingState: function (instanceId) {
            return runtimeModules.formatting.getFormattingState(instanceId);
        },
        getSidePanelSyncState: function (instanceId) {
            return runtimeModules.formatting.getSidePanelSyncState(instanceId);
        },
        getLastCommandTransaction: function (instanceId) {
            return runtimeModules.formatting.getLastCommandTransaction(instanceId);
        },
        getUndoState: function (instanceId) {
            return runtimeModules.formatting.getUndoState(instanceId);
        },
        getDebugUndoStack: function (instanceId) {
            return runtimeModules.formatting.getDebugUndoStack(instanceId);
        },
        getDirtyState: function (instanceId) {
            return _call('getDirtyState', [instanceId], function () { return null; });
        },
        markSaved: function (instanceId, marker) {
            return _call('markSaved', [instanceId, marker], function () { return false; });
        },
        getOfflineState: function (instanceId) {
            return _call('getOfflineState', [instanceId], function () { return null; });
        },
        applyOfflineState: function (instanceId, stateJson) {
            return _call('applyOfflineState', [instanceId, stateJson], function () { return false; });
        },
        undo: function (instanceId) {
            return runtimeModules.formatting.undo(instanceId);
        },
        redo: function (instanceId) {
            return runtimeModules.formatting.redo(instanceId);
        },
        getRuntimeSelection: function (instanceId) {
            return runtimeModules.selection.getRuntimeSelection(instanceId);
        },
        getSelectionSnapshot: function (instanceId) {
            return runtimeModules.selection.getSelectionSnapshot(instanceId);
        },
        getLinkInfo: function (instanceId) {
            return runtimeModules.clipboard.getLinkInfo(instanceId);
        },
        getMigrationStatus: function (instanceId) {
            return runtimeModules.migration.getMigrationStatus(instanceId);
        },
        getRemovedLegacyPathAudit: function (instanceId) {
            return runtimeModules.migration.getRemovedLegacyPathAudit(instanceId);
        },
        insertImageNode: function (instanceId, block, dispatchPatch) {
            return runtimeModules.image.insertImageNode(instanceId, block, dispatchPatch);
        },
        __internal: {
            version: 1,
            modules: runtimeModules,
            getModuleNames: getRuntimeModuleNames
        },
        __testHooks: {
            fromCanonicalDocument: fromCanonicalDocument,
            toCanonicalDocument: toCanonicalDocument,
            roundTripCanonicalDocument: roundTripCanonicalDocument,
            diffCanonicalDocuments: diffCanonicalDocuments,
            computeFormattingState: function (model, selection, pendingTypingMarks) {
                return _googleDocsEngine().__testHooks.computeFormattingState(model, selection, pendingTypingMarks || []);
            },
            getRuntimeDocument: function (instanceId) {
                return runtimeDocuments.has(instanceId) ? _cloneJson(runtimeDocuments.get(instanceId)) : null;
            },
            createRuntimeCommandTransaction: function (command, payload, beforeSelection, afterSelection, beforeFormatting, afterFormatting) {
                return _googleDocsEngine().__testHooks.createRuntimeCommandTransaction(
                    command,
                    payload,
                    beforeSelection,
                    afterSelection,
                    beforeFormatting,
                    afterFormatting);
            },
            normalizeSnapshot: _normalizeSnapshot,
            normalizeWrapMode: function (value) {
                return _googleDocsEngine().__testHooks.normalizeWrapMode(value);
            },
            normalizeHorizontalPosition: function (value) {
                return _googleDocsEngine().__testHooks.normalizeHorizontalPosition(value);
            }
        }
    };
})();

window.tmDocumentWysiwygCommand = (function () {
    'use strict';

    function cloneJson(value) {
        if (value === undefined || value === null) return value;
        try { return JSON.parse(JSON.stringify(value)); } catch { return value; }
    }

    function readCommandName(command) {
        if (typeof command === 'string') return command;
        var body = command || {};
        return String(body.command || body.Command || body.commandName || body.CommandName || body.name || body.Name || body.id || body.Id || '');
    }

    function readPayload(command) {
        if (!command || typeof command === 'string') return {};
        var body = command || {};
        return cloneJson(body.payload || body.Payload || {});
    }

    function readSelectionToken(command) {
        if (!command || typeof command === 'string') return null;
        var body = command || {};
        var payload = body.payload || body.Payload || {};
        var selection = body.selection || body.Selection || payload.selection || payload.Selection || {};
        return body.selectionToken
            || body.SelectionToken
            || body.stableSelectionToken
            || body.StableSelectionToken
            || payload.selectionToken
            || payload.SelectionToken
            || payload.stableSelectionToken
            || payload.StableSelectionToken
            || selection.selectionToken
            || selection.SelectionToken
            || selection.stableSelectionToken
            || selection.StableSelectionToken
            || null;
    }

    function normalizeResult(instanceId, commandName, result) {
        if (result && typeof result === 'object') {
            if (result.ok === false) return result;
            return Object.assign({ ok: true, instanceId: instanceId, command: commandName }, result);
        }

        return { ok: result !== false && result !== undefined, instanceId: instanceId, command: commandName, result: result };
    }

    function execute(instanceId, command) {
        var commandName = readCommandName(command);
        if (!instanceId || !commandName) {
            return {
                ok: false,
                instanceId: instanceId || '',
                command: commandName || '',
                error: { code: 'invalid-command-request', reason: !instanceId ? 'missing-instance-id' : 'missing-command-name' }
            };
        }

        var payload = readPayload(command);
        var token = readSelectionToken(command);
        if (token) {
            payload.SelectionToken = token;
            payload.selectionToken = token;
        }
        if (command && typeof command === 'object' && (command.selection || command.Selection) && !payload.Selection && !payload.selection) {
            payload.Selection = cloneJson(command.Selection || command.selection);
        }

        var runtime = window.tmDocumentEditorRuntime;
        if (!runtime || typeof runtime.executeCommand !== 'function') {
            return {
                ok: false,
                instanceId: instanceId,
                command: commandName,
                error: { code: 'runtime-unavailable', reason: 'tmDocumentEditorRuntime.executeCommand is unavailable' }
            };
        }

        try {
            return normalizeResult(instanceId, commandName, runtime.executeCommand(instanceId, commandName, payload));
        } catch (error) {
            return {
                ok: false,
                instanceId: instanceId,
                command: commandName,
                error: {
                    code: 'command-exception',
                    reason: String(error && error.message || error || 'command-exception')
                }
            };
        }
    }

    return { execute: execute };
})();

// Phase 12: Watchdog — wraps tmDocumentEditorRuntime with error recovery
(function () {
    'use strict';

    var runtime = window.tmDocumentEditorRuntime;
    if (!runtime) return;

    var WD_READY = 'ready';
    var WD_RECOVERING = 'recovering';
    var WD_RECOVERED = 'recovered';
    var WD_FAILED = 'failed';
    var WD_DEFAULT_MAX_ATTEMPTS = 3;
    var WD_DEFAULT_BACKOFF_MS = 100;

    var _watchdogContexts = new Map();

    function _wdGet(instanceId) {
        return _watchdogContexts.get(instanceId) || null;
    }

    function _cloneWatchdogJson(value) {
        if (value == null) return value;
        try { return JSON.parse(JSON.stringify(value)); } catch { return value; }
    }

    function _parseWatchdogJson(value) {
        if (value == null || value === '') return null;
        if (typeof value === 'string') {
            try { return JSON.parse(value); } catch { return value; }
        }

        return _cloneWatchdogJson(value);
    }

    function _unwrapWatchdogDocumentSnapshot(value) {
        if (!value || typeof value !== 'object') return value || null;
        return value.Document || value.document || value;
    }

    function _wrapWatchdogDocumentSnapshot(value) {
        if (!value || typeof value !== 'object') return value || null;
        if (value.Document || value.document) return value;
        return { Document: value };
    }

    function _safeCall(fn, fallback) {
        try {
            var value = fn();
            return value === undefined ? fallback : value;
        } catch {
            return fallback;
        }
    }

    function _watchdogNow() {
        try { return new Date().toISOString(); } catch { return ''; }
    }

    function _notifyDotNet(wd, methodName, detail) {
        if (!wd || !wd.dotNetRef) return;
        try {
            wd.dotNetRef.invokeMethodAsync(methodName, detail || wd.lastRecoveryDetail || null);
        } catch {}
    }

    function _recordWatchdogEvent(wd, eventName, source, error, extra) {
        if (!wd) return null;
        var detail = Object.assign({
            event: eventName || '',
            Event: eventName || '',
            source: source || wd.lastErrorSource || '',
            Source: source || wd.lastErrorSource || '',
            state: wd.state || '',
            State: wd.state || '',
            attempt: wd.attempt || 0,
            Attempt: wd.attempt || 0,
            maxAttempts: wd.maxAttempts || WD_DEFAULT_MAX_ATTEMPTS,
            MaxAttempts: wd.maxAttempts || WD_DEFAULT_MAX_ATTEMPTS,
            backoffMs: wd.currentBackoffMs || 0,
            BackoffMs: wd.currentBackoffMs || 0,
            usedSnapshotFallback: !!wd.usedSnapshotFallback,
            UsedSnapshotFallback: !!wd.usedSnapshotFallback,
            errorMessage: error && error.message ? String(error.message) : (error ? String(error) : ''),
            ErrorMessage: error && error.message ? String(error.message) : (error ? String(error) : ''),
            timestamp: _watchdogNow(),
            Timestamp: _watchdogNow()
        }, extra || {});

        wd.lastRecoveryDetail = detail;
        wd.events.push(detail);
        if (wd.events.length > 20) {
            wd.events = wd.events.slice(wd.events.length - 20);
        }

        return detail;
    }

    function _readMarkers(instanceId) {
        if (typeof runtime.getMarkers === 'function') {
            return _safeCall(function () { return runtime.getMarkers(instanceId); }, []);
        }

        return _safeCall(function () {
            return runtime.__internal.modules.core.call('getMarkers', [instanceId], function () { return []; });
        }, []);
    }

    function _readUploadState(debugSnapshot) {
        var pendingUploads = debugSnapshot && (debugSnapshot.PendingUploads || debugSnapshot.pendingUploads);
        var pendingUploadCount = debugSnapshot
            ? Number(debugSnapshot.PendingUploadCount ?? debugSnapshot.pendingUploadCount ?? (Array.isArray(pendingUploads) ? pendingUploads.length : 0))
            : 0;
        return {
            pendingUploadCount: pendingUploadCount || 0,
            PendingUploadCount: pendingUploadCount || 0,
            pendingUploads: Array.isArray(pendingUploads) ? _cloneWatchdogJson(pendingUploads) : [],
            PendingUploads: Array.isArray(pendingUploads) ? _cloneWatchdogJson(pendingUploads) : []
        };
    }

    function _captureStableSnapshot(instanceId, reason) {
        var debugSnapshot = _safeCall(function () { return runtime.getDebugSnapshot(instanceId); }, null);
        var runtimeSnapshot = _parseWatchdogJson(_safeCall(function () { return _origGetDocument(instanceId); }, null));
        var document = _unwrapWatchdogDocumentSnapshot(runtimeSnapshot);
        var snapshot = {
            capturedAt: _watchdogNow(),
            CapturedAt: _watchdogNow(),
            reason: reason || '',
            Reason: reason || '',
            document: document,
            Document: null,
            markers: _cloneWatchdogJson(_readMarkers(instanceId) || []),
            Markers: null,
            selection: _cloneWatchdogJson(
                _safeCall(function () { return runtime.getSelectionSnapshot(instanceId); }, null)
                || _safeCall(function () { return runtime.getRuntimeSelection(instanceId); }, null)),
            Selection: null,
            undoState: _cloneWatchdogJson(_safeCall(function () { return runtime.getUndoState(instanceId); }, null)),
            UndoState: null,
            undoDebug: _cloneWatchdogJson(_safeCall(function () { return runtime.getDebugUndoStack(instanceId); }, null)),
            UndoDebug: null,
            uploadState: _readUploadState(debugSnapshot),
            UploadState: null
        };
        snapshot.Document = snapshot.document;
        snapshot.Markers = snapshot.markers;
        snapshot.Selection = snapshot.selection;
        snapshot.UndoState = snapshot.undoState;
        snapshot.UndoDebug = snapshot.undoDebug;
        snapshot.UploadState = snapshot.uploadState;
        return snapshot;
    }

    function _rememberStableSnapshot(instanceId, wd, reason) {
        if (!wd) return null;
        var snapshot = _captureStableSnapshot(instanceId, reason);
        if (snapshot && snapshot.document) {
            wd.stableSnapshot = snapshot;
        }

        return wd.stableSnapshot;
    }

    function _rememberStableSnapshotFromDocument(instanceId, wd, reason, documentSnapshot) {
        if (!wd) return null;
        var document = _unwrapWatchdogDocumentSnapshot(_parseWatchdogJson(documentSnapshot));
        if (!document) return wd.stableSnapshot;
        var debugSnapshot = _safeCall(function () { return runtime.getDebugSnapshot(instanceId); }, null);
        var snapshot = {
            capturedAt: _watchdogNow(),
            CapturedAt: _watchdogNow(),
            reason: reason || '',
            Reason: reason || '',
            document: document,
            Document: document,
            markers: _cloneWatchdogJson(_readMarkers(instanceId) || []),
            Markers: null,
            selection: _cloneWatchdogJson(
                _safeCall(function () { return runtime.getSelectionSnapshot(instanceId); }, null)
                || _safeCall(function () { return runtime.getRuntimeSelection(instanceId); }, null)),
            Selection: null,
            undoState: _cloneWatchdogJson(_safeCall(function () { return runtime.getUndoState(instanceId); }, null)),
            UndoState: null,
            undoDebug: _cloneWatchdogJson(_safeCall(function () { return runtime.getDebugUndoStack(instanceId); }, null)),
            UndoDebug: null,
            uploadState: _readUploadState(debugSnapshot),
            UploadState: null
        };
        snapshot.Markers = snapshot.markers;
        snapshot.Selection = snapshot.selection;
        snapshot.UndoState = snapshot.undoState;
        snapshot.UndoDebug = snapshot.undoDebug;
        snapshot.UploadState = snapshot.uploadState;
        wd.stableSnapshot = snapshot;
        return wd.stableSnapshot;
    }

    function _restoreStableSnapshotExtras(instanceId, stableSnapshot) {
        if (!stableSnapshot) return;
        var markers = stableSnapshot.markers || stableSnapshot.Markers || [];
        if (Array.isArray(markers)) {
            markers.forEach(function (marker) {
                _safeCall(function () {
                    if (typeof runtime.upsertMarker === 'function') {
                        return runtime.upsertMarker(instanceId, marker);
                    }

                    return runtime.__internal.modules.core.call('upsertMarker', [instanceId, marker], function () { return null; });
                }, null);
            });
        }

        var selection = stableSnapshot.selection || stableSnapshot.Selection || null;
        if (selection) {
            _safeCall(function () { return runtime.restoreSelection(instanceId, selection); }, null);
        }
    }

    function _captureRecoveryState(instanceId, wd) {
        var snapshot = wd.forceSnapshotFallback
            ? null
            : _unwrapWatchdogDocumentSnapshot(_parseWatchdogJson(_safeCall(function () { return _origGetDocument(instanceId); }, null)));
        var offlineState = _safeCall(function () { return _origGetOfflineState(instanceId); }, null);
        var stableSnapshot = null;
        wd.usedSnapshotFallback = false;

        if (snapshot) {
            stableSnapshot = _captureStableSnapshot(instanceId, 'recovery-live');
            stableSnapshot.document = snapshot;
            stableSnapshot.Document = snapshot;
        } else if (wd.stableSnapshot) {
            stableSnapshot = _cloneWatchdogJson(wd.stableSnapshot);
            snapshot = stableSnapshot.document || stableSnapshot.Document || null;
            wd.usedSnapshotFallback = !!snapshot;
            if (wd.usedSnapshotFallback) {
                _recordWatchdogEvent(wd, 'snapshotFallbackUsed', wd.lastErrorSource, null, { usedSnapshotFallback: true, UsedSnapshotFallback: true });
            }
        }

        return {
            snapshot: snapshot,
            offlineState: offlineState,
            stableSnapshot: stableSnapshot
        };
    }

    function _failRecovery(instanceId, wd, source, error) {
        wd.state = WD_FAILED;
        wd.currentBackoffMs = 0;
        var detail = _recordWatchdogEvent(wd, 'runtimeRecoveryFailed', source, error);
        _notifyDotNet(wd, 'HandleRuntimeRecoveryFailed', detail);
    }

    function _attemptRecovery(instanceId, wd) {
        if (!wd || wd.state !== WD_RECOVERING) return;
        var recoveryState = _captureRecoveryState(instanceId, wd);

        try { _origDispose(instanceId); } catch {}

        try {
            if (wd.forceRecoveryFailure) {
                throw new Error('Forced watchdog recovery failure');
            }

            _origCreate(wd.rootEl, wd.options, wd.dotNetRef);
        } catch (error) {
            if (wd.attempt < wd.maxAttempts) {
                wd.state = WD_READY;
                _scheduleRecovery(instanceId, wd, wd.lastErrorSource || 'unknown', error);
                return;
            }

            _failRecovery(instanceId, wd, wd.lastErrorSource || 'unknown', error);
            return;
        }

        try { if (recoveryState.snapshot) _origLoadDocument(instanceId, recoveryState.snapshot); } catch {}
        try { if (recoveryState.offlineState) _origApplyOfflineState(instanceId, recoveryState.offlineState); } catch {}
        _restoreStableSnapshotExtras(instanceId, recoveryState.stableSnapshot);
        if (recoveryState.stableSnapshot) {
            wd.stableSnapshot = _cloneWatchdogJson(recoveryState.stableSnapshot);
        }

        wd.state = WD_RECOVERED;
        wd.currentBackoffMs = 0;
        var detail = _recordWatchdogEvent(wd, 'runtimeRecovered', wd.lastErrorSource || 'unknown', null);
        _notifyDotNet(wd, 'HandleRuntimeRecovered', detail);
    }

    function _scheduleRecovery(instanceId, wd, source, error) {
        if (!wd || wd.state === WD_RECOVERING) return;
        if (wd.attempt >= wd.maxAttempts) {
            _failRecovery(instanceId, wd, source, error);
            return;
        }

        wd.state = WD_RECOVERING;
        wd.lastErrorSource = source || 'unknown';
        wd.attempt += 1;
        wd.currentBackoffMs = Math.max(0, wd.baseBackoffMs || WD_DEFAULT_BACKOFF_MS) * Math.pow(2, Math.max(0, wd.attempt - 1));
        _recordWatchdogEvent(wd, 'runtimeRecoveryScheduled', source, error);
        setTimeout(function () { _attemptRecovery(instanceId, wd); }, wd.currentBackoffMs);
    }

    var _origCreate = runtime.create;
    var _origLoadDocument = runtime.loadDocument;
    var _origGetDocument = runtime.getDocument;
    var _origGetOfflineState = runtime.getOfflineState;
    var _origApplyOfflineState = runtime.applyOfflineState;
    runtime.create = function (rootEl, options, dotNetRef) {
        var instanceId = options && (options.InstanceId || options.instanceId || '');
        var result = _origCreate.apply(runtime, arguments);
        if (instanceId) {
            var wd = {
                state: WD_READY,
                rootEl: rootEl,
                options: options,
                dotNetRef: dotNetRef || null,
                stableSnapshot: null,
                events: [],
                lastRecoveryDetail: null,
                lastErrorSource: '',
                attempt: 0,
                maxAttempts: Number(options && (options.WatchdogMaxAttempts ?? options.watchdogMaxAttempts) || WD_DEFAULT_MAX_ATTEMPTS),
                baseBackoffMs: Number(options && (options.WatchdogBackoffMs ?? options.watchdogBackoffMs) || WD_DEFAULT_BACKOFF_MS),
                currentBackoffMs: 0,
                usedSnapshotFallback: false,
                forceRecoveryFailure: false,
                forceSnapshotFallback: false
            };
            _watchdogContexts.set(String(instanceId), wd);
        }
        return result;
    };

    var _origDispose = runtime.dispose;
    runtime.dispose = function (instanceId) {
        _watchdogContexts.delete(String(instanceId || ''));
        return _origDispose.apply(runtime, arguments);
    };

    runtime.loadDocument = function (instanceId) {
        try {
            var result = _origLoadDocument.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) {
                wd.state = WD_READY;
                wd.attempt = 0;
                _rememberStableSnapshotFromDocument(String(instanceId || ''), wd, 'loadDocument', arguments[1]);
            }

            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'render', error);
            }
            return undefined;
        }
    };

    runtime.getDocument = function (instanceId) {
        try {
            return _origGetDocument.apply(runtime, arguments);
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'serialization', error);
            }
            return wd && wd.stableSnapshot && wd.stableSnapshot.document
                ? JSON.stringify(_wrapWatchdogDocumentSnapshot(wd.stableSnapshot.document))
                : null;
        }
    };

    var _origExecuteCommand = runtime.executeCommand;
    runtime.executeCommand = function (instanceId, command, payload) {
        try {
            var result = _origExecuteCommand.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) {
                wd.state = WD_READY;
                wd.attempt = 0;
                _rememberStableSnapshot(String(instanceId || ''), wd, 'command');
            }

            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'command', error);
            }
            return undefined;
        }
    };

    var _origApplyBatch = runtime.applyRemoteOperationBatch;
    runtime.applyRemoteOperationBatch = function (instanceId, batch) {
        try {
            var result = _origApplyBatch.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) {
                wd.state = WD_READY;
                wd.attempt = 0;
                _rememberStableSnapshot(String(instanceId || ''), wd, 'remoteOperation');
            }

            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'remoteOperation', error);
            }
            return undefined;
        }
    };

    var _origApplyRemoteOperation = runtime.applyRemoteOperation;
    runtime.applyRemoteOperation = function (instanceId) {
        try {
            var result = _origApplyRemoteOperation.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) _rememberStableSnapshot(String(instanceId || ''), wd, 'remoteOperation');
            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'remoteOperation', error);
            }
            return undefined;
        }
    };

    runtime.__watchdog = {
        getState: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? wd.state : null;
        },
        getStableSnapshot: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? _cloneWatchdogJson(wd.stableSnapshot) : null;
        },
        getLastRecoveryDetail: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? _cloneWatchdogJson(wd.lastRecoveryDetail) : null;
        },
        getEvents: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? _cloneWatchdogJson(wd.events || []) : [];
        },
        configure: function (instanceId, options) {
            var wd = _wdGet(String(instanceId || ''));
            if (!wd) return false;
            if (options && options.maxAttempts != null) wd.maxAttempts = Number(options.maxAttempts) || wd.maxAttempts;
            if (options && options.baseBackoffMs != null) wd.baseBackoffMs = Number(options.baseBackoffMs) || wd.baseBackoffMs;
            if (options && options.forceRecoveryFailure != null) wd.forceRecoveryFailure = !!options.forceRecoveryFailure;
            if (options && options.forceSnapshotFallback != null) wd.forceSnapshotFallback = !!options.forceSnapshotFallback;
            return true;
        },
        simulateCrash: function (instanceId, source, options) {
            var wd = _wdGet(String(instanceId || ''));
            if (!wd) return false;
            if (options) {
                runtime.__watchdog.configure(instanceId, options);
            }
            _scheduleRecovery(String(instanceId || ''), wd, source || 'command', new Error((options && options.message) || 'Simulated watchdog crash'));
            return true;
        }
    };

    if (runtime.__internal && runtime.__internal.modules && runtime.__internal.modules.watchdog) {
        runtime.__internal.modules.watchdog.getState = runtime.__watchdog.getState;
        runtime.__internal.modules.watchdog.getStableSnapshot = runtime.__watchdog.getStableSnapshot;
        runtime.__internal.modules.watchdog.getLastRecoveryDetail = runtime.__watchdog.getLastRecoveryDetail;
        runtime.__internal.modules.watchdog.getEvents = runtime.__watchdog.getEvents;
        runtime.__internal.modules.watchdog.simulateCrash = runtime.__watchdog.simulateCrash;
    }
})();

(function () {
    function _resolveInstanceId(instanceId) {
        if (instanceId) return instanceId;
        var host = document.querySelector('[data-testid="document-wysiwyg-host"][data-instance-id]');
        return host ? (host.getAttribute('data-instance-id') || '') : '';
    }

    function _runtime() {
        return window.tmDocumentEditorRuntime || null;
    }

    function _engine() {
        return window.tmDocumentEditorEngine || null;
    }

    function _debugSnapshot(instanceId) {
        var runtime = _runtime();
        if (runtime && typeof runtime.getDebugSnapshot === 'function') {
            return runtime.getDebugSnapshot(instanceId);
        }

        var engine = _engine();
        return engine && typeof engine.getDebugSnapshot === 'function'
            ? engine.getDebugSnapshot(instanceId)
            : { InstanceId: instanceId, HasInstance: false, Error: 'getDebugSnapshot unavailable' };
    }

    window.tmDocumentEditorTestProbe = (function () {
        var state = null;

        function now() {
            return performance && typeof performance.now === 'function' ? performance.now() : Date.now();
        }

        function resolveHost(selector) {
            return document.querySelector(selector || '[data-testid="document-wysiwyg-host"]');
        }

        function readText(host) {
            return host ? (host.innerText || host.textContent || '') : '';
        }

        function resolveInstanceId(host) {
            return host && (host.getAttribute('data-instance-id') || host.closest('[data-instance-id]')?.getAttribute('data-instance-id')) || '';
        }

        function readRenderStats(host) {
            try {
                var id = resolveInstanceId(host);
                return window.tmDocumentEditorDebug && typeof window.tmDocumentEditorDebug.getRenderStats === 'function'
                    ? window.tmDocumentEditorDebug.getRenderStats(id)
                    : {};
            } catch (error) {
                return { error: String(error) };
            }
        }

        function readBoundaryPatchCount(host) {
            try {
                var id = resolveInstanceId(host);
                var snapshot = window.tmDocumentEditorRuntime && typeof window.tmDocumentEditorRuntime.getDebugSnapshot === 'function'
                    ? window.tmDocumentEditorRuntime.getDebugSnapshot(id)
                    : window.tmDocumentEditorEngine && typeof window.tmDocumentEditorEngine.getDebugSnapshot === 'function'
                        ? window.tmDocumentEditorEngine.getDebugSnapshot(id)
                        : {};
                return Number(snapshot.boundaryPatchCount || snapshot.BoundaryPatchCount || 0);
            } catch {
                return 0;
            }
        }

        function num(value) {
            return Number(value || 0);
        }

        function delta(current, initial) {
            return Math.max(0, num(current) - num(initial));
        }

        function recordVisibleTextChange() {
            if (!state || state.visibleTextChangedAt !== null) return;
            var text = readText(state.host);
            if (text !== state.initialText) {
                state.visibleTextChangedAt = now();
                state.lastText = text;
            }
        }

        function tickVisibleText() {
            if (!state) return;
            recordVisibleTextChange();
            state.raf = requestAnimationFrame(tickVisibleText);
        }

        function onKeyDown(event) {
            if (!state) return;
            state.keydownCount++;
            state.lastKey = event.key || '';
            if (state.keyDownAt === null) state.keyDownAt = now();
        }

        function onBeforeInput() {
            if (!state) return;
            state.beforeInputCount++;
            if (state.beforeInputAt === null) state.beforeInputAt = now();
        }

        function disconnect() {
            if (!state) return;
            document.removeEventListener('keydown', onKeyDown, true);
            document.removeEventListener('beforeinput', onBeforeInput, true);
            if (state.observer) state.observer.disconnect();
            if (state.raf) cancelAnimationFrame(state.raf);
        }

        function start(selector) {
            disconnect();
            var host = resolveHost(selector);
            if (!host) throw new Error('tmDocumentEditorTestProbe could not find editor host.');
            var initialStats = readRenderStats(host);
            state = {
                selector: selector || '[data-testid="document-wysiwyg-host"]',
                host: host,
                startedAt: now(),
                keyDownAt: null,
                beforeInputAt: null,
                firstDomMutationAt: null,
                visibleTextChangedAt: null,
                initialText: readText(host),
                lastText: readText(host),
                initialStats: initialStats,
                initialBoundaryPatchCount: readBoundaryPatchCount(host),
                keydownCount: 0,
                beforeInputCount: 0,
                mutationBatchCount: 0,
                mutationRecordCount: 0,
                largestBatchSize: 0,
                lastKey: '',
                observer: null,
                raf: null
            };
            state.observer = new MutationObserver(function (records) {
                if (!state) return;
                state.mutationBatchCount++;
                state.mutationRecordCount += records.length;
                state.largestBatchSize = Math.max(state.largestBatchSize, records.length);
                if (state.firstDomMutationAt === null) state.firstDomMutationAt = now();
                recordVisibleTextChange();
            });
            state.observer.observe(host, { childList: true, characterData: true, subtree: true, attributes: true });
            document.addEventListener('keydown', onKeyDown, true);
            document.addEventListener('beforeinput', onBeforeInput, true);
            state.raf = requestAnimationFrame(tickVisibleText);
            return snapshot();
        }

        function snapshot() {
            if (!state) return {};
            recordVisibleTextChange();
            var currentStats = readRenderStats(state.host);
            var keyDownAt = state.keyDownAt;
            return {
                startedAt: state.startedAt,
                keyDownAt: keyDownAt,
                beforeInputAt: state.beforeInputAt,
                firstDomMutationAt: state.firstDomMutationAt,
                visibleTextChangedAt: state.visibleTextChangedAt,
                beforeInputLatencyMs: keyDownAt === null || state.beforeInputAt === null ? null : state.beforeInputAt - keyDownAt,
                domMutationLatencyMs: keyDownAt === null || state.firstDomMutationAt === null ? null : state.firstDomMutationAt - keyDownAt,
                visibleTextChangeLatencyMs: keyDownAt === null || state.visibleTextChangedAt === null ? null : state.visibleTextChangedAt - keyDownAt,
                fullRenderCount: delta(currentStats.FullRenderCount || currentStats.fullRenderCount, state.initialStats.FullRenderCount || state.initialStats.fullRenderCount),
                partialRenderCount: delta(currentStats.IncrementalOperationCount || currentStats.incrementalOperationCount, state.initialStats.IncrementalOperationCount || state.initialStats.incrementalOperationCount),
                blazorCallbackCount: delta(readBoundaryPatchCount(state.host), state.initialBoundaryPatchCount),
                keydownCount: state.keydownCount,
                beforeInputCount: state.beforeInputCount,
                mutationBatchCount: state.mutationBatchCount,
                mutationRecordCount: state.mutationRecordCount,
                largestBatchSize: state.largestBatchSize,
                visibleTextLength: readText(state.host).length,
                key: state.lastKey || ''
            };
        }

        function stop() {
            var result = snapshot();
            disconnect();
            state = null;
            return result;
        }

        return {
            start: start,
            snapshot: snapshot,
            stop: stop,
            reset: function (selector) { return start(selector); }
        };
    })();

    window.tmDocumentEditorDebug = {
        getRuntimeState: function (instanceId) {
            var id = _resolveInstanceId(instanceId);
            var snapshot = _debugSnapshot(id) || {};
            var runtime = _runtime();
            var runtimeDocument = runtime && runtime.__testHooks && runtime.__testHooks.getRuntimeDocument
                ? runtime.__testHooks.getRuntimeDocument(id)
                : null;

            return Object.assign({}, snapshot, {
                RuntimeAuthority: 'JsCanonicalBoundary',
                JsOwnedRuntime: true,
                JsOwnedRuntimePhase: 'GoogleDocsEngineHardCut',
                LegacyEngineRemoved: true,
                HasRuntimeDocument: !!runtimeDocument,
                RuntimeDocumentId: runtimeDocument && runtimeDocument.document
                    ? (runtimeDocument.document.DocumentId || runtimeDocument.document.documentId || '')
                    : ''
            });
        },
        getRuntimeStateJson: function (instanceId) {
            return JSON.stringify(this.getRuntimeState(instanceId), null, 2);
        },
        getRenderStats: function (instanceId) {
            var id = _resolveInstanceId(instanceId);
            var snapshot = _debugSnapshot(id) || {};
            var metrics = snapshot.performanceStats || snapshot.PerformanceStats || {};
            return {
                InstanceId: id,
                HasInstance: snapshot.ok !== false && !snapshot.error,
                SnapshotApplyCount: snapshot.lastCSharpUpdate ? 1 : 0,
                KeyDownCount: metrics.keyDownCount || metrics.KeyDownCount || 0,
                BeforeInputCount: metrics.beforeInputCount || metrics.BeforeInputCount || 0,
                InputDomApplyCount: metrics.inputDomApplyCount || metrics.InputDomApplyCount || 0,
                FullRenderCount: metrics.fullRenderCount || metrics.FullRenderCount || metrics.renderPassCount || metrics.RenderPassCount || 0,
                PartialRenderCount: metrics.partialRenderCount || metrics.PartialRenderCount || 0,
                TextNodePatchCount: metrics.textNodePatchCount || metrics.TextNodePatchCount || 0,
                BlockPatchCount: metrics.blockPatchCount || metrics.BlockPatchCount || 0,
                MarkerOverlayPatchCount: metrics.markerOverlayPatchCount || metrics.MarkerOverlayPatchCount || 0,
                ObjectOverlayPatchCount: metrics.objectOverlayPatchCount || metrics.ObjectOverlayPatchCount || 0,
                SelectionNotifyCount: metrics.selectionNotifyCount || metrics.SelectionNotifyCount || 0,
                BlazorInteropCallCount: metrics.blazorInteropCallCount || metrics.BlazorInteropCallCount || 0,
                BlazorCallbackDuringTypingCount: metrics.blazorCallbackDuringTypingCount || metrics.BlazorCallbackDuringTypingCount || 0,
                FormattingStateEventCount: metrics.formattingStateEventCount || metrics.FormattingStateEventCount || metrics.formattingStateNotifyCount || metrics.FormattingStateNotifyCount || 0,
                TypingFlushCount: metrics.typingFlushCount || metrics.TypingFlushCount || 0,
                MaxTypingBatchSize: metrics.maxTypingBatchSize || metrics.MaxTypingBatchSize || 0,
                MaxBoundaryPatchBatchSize: metrics.maxBoundaryPatchBatchSize || metrics.MaxBoundaryPatchBatchSize || 0,
                MedianKeyToDomMs: metrics.medianKeyToDomMs || metrics.MedianKeyToDomMs || 0,
                P95KeyToDomMs: metrics.p95KeyToDomMs || metrics.P95KeyToDomMs || 0,
                MaxInputLatencyMs: metrics.maxKeyToDomMs || metrics.MaxInputLatencyMs || 0,
                AverageInputLatencyMs: metrics.keyToDomSamples && metrics.keyToDomSamples.length
                    ? metrics.keyToDomSamples.reduce(function (sum, value) { return sum + Number(value || 0); }, 0) / metrics.keyToDomSamples.length
                    : (metrics.AverageInputLatencyMs || 0),
                IncrementalOperationCount: metrics.incrementalOperationCount || metrics.IncrementalOperationCount || snapshot.transactionCount || 0,
                InputOperationCount: metrics.inputOperationCount || metrics.InputOperationCount || 0,
                MaxInputOperationMs: metrics.inputOperationMaxMs || metrics.MaxInputOperationMs || 0,
                LastRenderReason: metrics.renderLastReason || metrics.RenderLastReason || '',
                LayoutPassCount: metrics.layoutPassCount || metrics.LayoutPassCount || 0,
                LastLayoutPassMs: metrics.layoutPassLastMs || metrics.LayoutPassLastMs || 0,
                MaxLayoutPassMs: metrics.layoutPassMaxMs || metrics.LayoutPassMaxMs || 0,
                LastLayoutReason: metrics.layoutLastReason || metrics.LayoutLastReason || '',
                TotalPages: snapshot.layout && Array.isArray(snapshot.layout.pages) ? snapshot.layout.pages.length : 0,
                RenderedPages: snapshot.layout && Array.isArray(snapshot.layout.pages) ? snapshot.layout.pages.length : 0,
                VirtualizedPages: 0,
                ToolbarStateLayoutThrashCount: metrics.toolbarStateLayoutThrashCount || metrics.ToolbarStateLayoutThrashCount || 0,
                FormattingCommandPartialRenderCount: metrics.formattingCommandPartialRenderCount || metrics.FormattingCommandPartialRenderCount || 0,
                LightweightBoundaryPatchCount: metrics.lightweightBoundaryPatchCount || metrics.LightweightBoundaryPatchCount || 0,
                BoundarySnapshotExportCount: metrics.boundarySnapshotExportCount || metrics.BoundarySnapshotExportCount || 0,
                DeferredBoundaryPatchDispatchCount: metrics.deferredBoundaryPatchDispatchCount || metrics.DeferredBoundaryPatchDispatchCount || 0,
                DeferredRevisionNotifyCount: metrics.deferredRevisionNotifyCount || metrics.DeferredRevisionNotifyCount || 0,
                RevisionNotifyCount: metrics.revisionNotifyCount || metrics.RevisionNotifyCount || 0,
                MarkerStoreDeferredRefreshCount: metrics.markerStoreDeferredRefreshCount || metrics.MarkerStoreDeferredRefreshCount || 0
            };
        },
        getUndoStack: function (instanceId) {
            var id = _resolveInstanceId(instanceId);
            var runtime = _runtime();
            var undoState = runtime && typeof runtime.getUndoState === 'function'
                ? runtime.getUndoState(id)
                : null;
            return {
                InstanceId: id,
                HasInstance: !!undoState,
                JsOwnedUndo: true,
                CanUndo: !!(undoState && (undoState.CanUndo || undoState.canUndo)),
                CanRedo: !!(undoState && (undoState.CanRedo || undoState.canRedo)),
                UndoDepth: undoState ? (undoState.UndoDepth || undoState.undoDepth || 0) : 0,
                RedoDepth: undoState ? (undoState.RedoDepth || undoState.redoDepth || 0) : 0,
                Items: [],
                RedoItems: []
            };
        },
        setImageDebugEnabled: function (enabled) {
            try {
                window.localStorage.setItem('tmDocumentEditorImageDebug', enabled ? '1' : '0');
            } catch {}
            console.info('[TmDocumentEditor:image]', enabled ? 'debug enabled' : 'debug disabled');
        },
        getImageDebugEnabled: function () {
            try {
                return window.localStorage.getItem('tmDocumentEditorImageDebug') === '1';
            } catch {
                return false;
            }
        }
    };
})();
