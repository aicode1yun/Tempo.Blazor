import { normalizeMathRun } from '../math/math-model.mjs';
import { normalizeContentControlBlock, normalizeContentControlRun } from '../controls/sdt-model.mjs';
import { normalizeSigningFieldRun } from '../controls/signing-field-model.mjs';

export const CANVAS_MODEL_SCHEMA_VERSION = 1;

export const CANVAS_BLOCK_TYPES = Object.freeze({
    paragraph: 'paragraph',
    heading: 'heading',
    list: 'list',
    quote: 'quote',
    table: 'table',
    image: 'image',
    pageBreak: 'pageBreak',
    contentControl: 'contentControl',
});

export const CANVAS_RUN_TYPES = Object.freeze({
    text: 'text',
    field: 'field',
    token: 'token',
    noteReference: 'noteReference',
    drawing: 'drawing',
    math: 'math',
    contentControl: 'contentControl',
    signingField: 'signingField',
});

export function createCanvasDocumentModel(input = {}) {
    const source = objectOrEmpty(input);
    const bodySource = objectOrEmpty(source.body);
    const bodyBlocks = Array.isArray(bodySource.blocks)
        ? bodySource.blocks
        : Array.isArray(source.blocks)
            ? source.blocks
            : [];
    const sections = Array.isArray(source.sections)
        ? source.sections.map((section, index) => normalizeSection(section, index))
        : [];
    const normalizedBodyBlocks = bodyBlocks.map((block, index) => normalizeBlock(block, index));

    if (sections.length === 0) {
        sections.push(createDefaultSection(normalizedBodyBlocks.slice()));
    }

    if (normalizedBodyBlocks.length === 0 && !sections.some(section => section.blocks.length > 0)) {
        const paragraph = createEmptyParagraphBlock('block-1');
        normalizedBodyBlocks.push(paragraph);
        sections[0].blocks.push(paragraph);
    }

    return {
        schemaVersion: positiveInteger(source.schemaVersion, CANVAS_MODEL_SCHEMA_VERSION),
        documentId: stringValue(source.documentId || source.id, 'canvas-document'),
        version: nonNegativeNumber(source.version, 0),
        metadata: objectOrEmpty(source.metadata),
        pageSettings: normalizePageSettings(source.pageSettings),
        theme: objectOrEmpty(source.theme),
        hyphenation: objectOrEmpty(source.hyphenation || source.Hyphenation),
        pageBackground: objectOrEmpty(source.pageBackground || source.PageBackground),
        sections,
        body: {
            blocks: normalizedBodyBlocks,
        },
        comments: arrayOrEmpty(source.comments),
        notes: arrayOrEmpty(source.notes),
        headersFooters: arrayOrEmpty(source.headersFooters),
        numberingDefinitions: arrayOrEmpty(source.numberingDefinitions),
        listStyles: arrayOrEmpty(source.listStyles),
        styles: arrayOrEmpty(source.styles),
        bibliographySources: arrayOrEmpty(source.bibliographySources),
        citations: arrayOrEmpty(source.citations),
        revisions: arrayOrEmpty(source.revisions),
        assets: arrayOrEmpty(source.assets),
        anchors: arrayOrEmpty(source.anchors),
        isProtected: Boolean(source.isProtected),
        restrictedMarkers: arrayOrEmpty(source.restrictedMarkers),
        outlineRevision: nonNegativeNumber(source.outlineRevision, 0),
        tableOfContentsRevision: nonNegativeNumber(source.tableOfContentsRevision, 0),
        preserve: objectOrEmpty(source.preserve),
    };
}

export function normalizePageSettings(input = {}) {
    const source = objectOrEmpty(input);
    return {
        width: positiveNumber(source.width, 794),
        height: positiveNumber(source.height, 1123),
        marginTop: nonNegativeNumber(source.marginTop, 96),
        marginRight: nonNegativeNumber(source.marginRight, 96),
        marginBottom: nonNegativeNumber(source.marginBottom, 96),
        marginLeft: nonNegativeNumber(source.marginLeft, 96),
        headerDistanceFromTop: nonNegativeNumber(source.headerDistanceFromTop, 48),
        footerDistanceFromBottom: nonNegativeNumber(source.footerDistanceFromBottom, 48),
        sizeName: source.sizeName == null ? null : String(source.sizeName),
        landscape: Boolean(source.landscape),
        preserve: objectOrEmpty(source.preserve),
    };
}

export function normalizeBlock(input = {}, index = 0) {
    const source = objectOrEmpty(input);
    const content = normalizeBlockContent(source.content, source.type);
    return {
        id: stringValue(source.id, `block-${index + 1}`),
        sectionId: source.sectionId == null ? null : String(source.sectionId),
        type: content.type,
        order: finiteNumber(source.order, index),
        paragraphProperties: objectOrEmpty(source.paragraphProperties),
        content,
        preserve: objectOrEmpty(source.preserve),
    };
}

export function normalizeRun(input = {}, index = 0) {
    const source = objectOrEmpty(input);
    const type = knownRunType(source.type);
    const contentControl = type === CANVAS_RUN_TYPES.contentControl || source.contentControl
        ? normalizeContentControlRun(source)
        : null;
    const signingField = type === CANVAS_RUN_TYPES.signingField || source.signingField
        ? normalizeSigningFieldRun(source)
        : null;
    return {
        id: source.id == null ? `run-${index + 1}` : String(source.id),
        type,
        text: source.text == null && contentControl
            ? contentControl.control.displayText
            : source.text == null ? '' : String(source.text),
        marks: arrayOrEmpty(source.marks).map(normalizeMark),
        field: source.field && typeof source.field === 'object' ? source.field : null,
        token: source.token && typeof source.token === 'object' ? source.token : null,
        noteReference: source.noteReference && typeof source.noteReference === 'object' ? source.noteReference : null,
        drawing: source.drawing && typeof source.drawing === 'object' ? source.drawing : null,
        math: type === CANVAS_RUN_TYPES.math || source.math
            ? normalizeMathRun(source)
            : null,
        contentControl,
        signingField,
        preserve: objectOrEmpty(source.preserve),
    };
}

export function normalizeMark(input = {}) {
    const source = objectOrEmpty(input);
    return {
        type: stringValue(source.type, 'unknown'),
        value: source.value == null ? null : String(source.value),
        link: source.link && typeof source.link === 'object' ? source.link : null,
        commentAnchor: source.commentAnchor && typeof source.commentAnchor === 'object' ? source.commentAnchor : null,
        revisionId: source.revisionId == null ? null : String(source.revisionId),
        preserve: objectOrEmpty(source.preserve),
    };
}

function normalizeSection(input = {}, index = 0) {
    const source = objectOrEmpty(input);
    return {
        id: stringValue(source.id, `section-${index + 1}`),
        order: Number.isInteger(source.order) ? source.order : index,
        title: source.title == null ? null : String(source.title),
        properties: objectOrEmpty(source.properties),
        pageSettings: normalizePageSettings(source.pageSettings),
        blocks: arrayOrEmpty(source.blocks).map(normalizeBlock),
        preserve: objectOrEmpty(source.preserve),
    };
}

function normalizeBlockContent(input = {}, fallbackType = CANVAS_BLOCK_TYPES.paragraph) {
    const source = objectOrEmpty(input);
    const type = knownBlockType(source.type || fallbackType);
    return {
        type,
        runs: normalizeRunsForBlock(type, source.runs),
        headingLevel: source.headingLevel == null ? null : Math.max(1, positiveInteger(source.headingLevel, 1)),
        styleId: source.styleId == null ? null : String(source.styleId),
        styleName: source.styleName == null ? null : String(source.styleName),
        outlineLevel: source.outlineLevel == null ? null : Math.max(1, positiveInteger(source.outlineLevel, 1)),
        list: source.list && typeof source.list === 'object'
            ? normalizeListProperties(source.list)
            : null,
        table: source.table && typeof source.table === 'object' ? source.table : null,
        image: source.image && typeof source.image === 'object' ? source.image : null,
        pageBreak: source.pageBreak && typeof source.pageBreak === 'object' ? source.pageBreak : null,
        contentControl: source.contentControl && typeof source.contentControl === 'object'
            ? normalizeContentControlBlock(source)
            : null,
        caption: source.caption && typeof source.caption === 'object' ? normalizeCaption(source.caption) : null,
        tableOfContents: tableOfContentsSource(source)
            ? normalizeTableOfContents(tableOfContentsSource(source))
            : null,
    };
}

function normalizeCaption(input) {
    const source = objectOrEmpty(input);
    return {
        id: stringValue(source.id, 'caption'),
        kind: stringValue(source.kind, 'figure'),
        label: stringValue(source.label, 'Figure'),
        text: source.text == null ? '' : String(source.text),
        number: source.number == null ? null : Math.max(1, positiveInteger(source.number, 1)),
        numberLabel: source.numberLabel == null ? null : String(source.numberLabel),
    };
}

function normalizeTableOfContents(input) {
    const source = objectOrEmpty(input);
    return {
        tocId: stringValue(source.tocId ?? source.TocId, 'toc'),
        isEntry: Boolean(source.isEntry ?? source.IsEntry),
        targetBlockId: (source.targetBlockId ?? source.TargetBlockId) == null ? null : String(source.targetBlockId ?? source.TargetBlockId),
        level: Math.max(1, positiveInteger(source.level ?? source.Level, 1)),
        text: (source.text ?? source.Text) == null ? '' : String(source.text ?? source.Text),
        pageNumber: Math.max(1, positiveInteger(source.pageNumber ?? source.PageNumber, 1)),
        pageIndex: nonNegativeNumber(source.pageIndex ?? source.PageIndex, 0),
        y: finiteNumber(source.y ?? source.Y, 0),
        levels: Math.max(1, positiveInteger(source.levels ?? source.Levels, 3)),
    };
}

function tableOfContentsSource(source) {
    if (source.tableOfContents && typeof source.tableOfContents === 'object') {
        return source.tableOfContents;
    }

    if (source.TableOfContents && typeof source.TableOfContents === 'object') {
        return source.TableOfContents;
    }

    return null;
}

function normalizeListProperties(input) {
    const source = objectOrEmpty(input);
    return {
        ...source,
        ordered: Boolean(source.ordered),
        indentLevel: Math.max(0, positiveInteger(source.indentLevel, 0)),
        startNumber: Math.max(1, positiveInteger(source.startNumber, 1)),
        numberingId: source.numberingId == null ? null : String(source.numberingId),
        abstractNumberingId: source.abstractNumberingId == null ? null : String(source.abstractNumberingId),
        listStyleId: source.listStyleId == null ? null : String(source.listStyleId),
        numberFormat: source.numberFormat == null ? null : String(source.numberFormat),
        levelText: source.levelText == null ? null : String(source.levelText),
        suffix: source.suffix == null ? null : String(source.suffix),
        labelIndent: source.labelIndent == null ? null : nonNegativeNumber(source.labelIndent, 0),
        hangingIndent: source.hangingIndent == null ? null : nonNegativeNumber(source.hangingIndent, 0),
        restartNumbering: Boolean(source.restartNumbering),
        continueNumbering: Boolean(source.continueNumbering),
        numberingValue: source.numberingValue == null ? null : Math.max(1, positiveInteger(source.numberingValue, 1)),
    };
}

function normalizeRunsForBlock(type, runs) {
    if (type === CANVAS_BLOCK_TYPES.table || type === CANVAS_BLOCK_TYPES.image || type === CANVAS_BLOCK_TYPES.pageBreak) {
        return [];
    }

    const normalizedRuns = arrayOrEmpty(runs).map(normalizeRun);
    return normalizedRuns.length > 0 ? normalizedRuns : [createEmptyTextRun('run-1')];
}

function createDefaultSection(blocks) {
    return {
        id: 'section-1',
        order: 0,
        title: null,
        properties: {},
        pageSettings: normalizePageSettings(),
        blocks,
        preserve: {},
    };
}

function createEmptyParagraphBlock(id) {
    return {
        id,
        sectionId: null,
        type: CANVAS_BLOCK_TYPES.paragraph,
        order: 0,
        paragraphProperties: {},
        content: {
            type: CANVAS_BLOCK_TYPES.paragraph,
            runs: [createEmptyTextRun(`${id}-run-1`)],
            headingLevel: null,
            styleId: null,
            styleName: null,
            outlineLevel: null,
            list: null,
            table: null,
            image: null,
            pageBreak: null,
            caption: null,
        },
        preserve: {},
    };
}

function createEmptyTextRun(id) {
    return {
        id,
        type: CANVAS_RUN_TYPES.text,
        text: '',
        marks: [],
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        math: null,
        contentControl: null,
        preserve: {},
    };
}

function knownBlockType(value) {
    const text = String(value || CANVAS_BLOCK_TYPES.paragraph);
    return Object.values(CANVAS_BLOCK_TYPES).includes(text) ? text : CANVAS_BLOCK_TYPES.paragraph;
}

function knownRunType(value) {
    const text = String(value || CANVAS_RUN_TYPES.text);
    return Object.values(CANVAS_RUN_TYPES).includes(text) ? text : CANVAS_RUN_TYPES.text;
}

function objectOrEmpty(value) {
    return value && typeof value === 'object' && !Array.isArray(value) ? value : {};
}

function arrayOrEmpty(value) {
    return Array.isArray(value) ? value : [];
}

function stringValue(value, fallback) {
    return value == null || String(value).trim() === '' ? fallback : String(value);
}

function positiveInteger(value, fallback) {
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function positiveNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function nonNegativeNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}

function finiteNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
}
