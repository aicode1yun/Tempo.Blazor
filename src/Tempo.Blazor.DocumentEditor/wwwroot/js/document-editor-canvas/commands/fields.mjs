import { createCanvasRunText } from '../layout/canvas-text-style.mjs';
import { createCaptionBlock, createTableOfFiguresField, renumberCaptions } from '../fields/captions.mjs';
import { createCrossReferenceField, resolveCrossReferenceNavigation } from '../fields/cross-reference.mjs';
import { FIELD_TYPES, collectReferenceTargets, normalizeFieldType, updateAllFields } from '../fields/field-engine.mjs';

const FIELD_COMMAND_ALIASES = new Map([
    ['insertfield', 'insertField'],
    ['insertpagenumber', 'insertField'],
    ['insertpagecount', 'insertField'],
    ['insertpagexofy', 'insertField'],
    ['insertdatefield', 'insertField'],
    ['insertdocumenttitlefield', 'insertField'],
    ['insertauthorfield', 'insertField'],
    ['inserttimefield', 'insertField'],
    ['insertfilenamefield', 'insertField'],
    ['insertstylereffield', 'insertField'],
    ['insertcrossreference', 'insertCrossReference'],
    ['crossreference', 'insertCrossReference'],
    ['gotoreference', 'goToReference'],
    ['insertcaption', 'insertCaption'],
    ['inserttableoffigures', 'insertTableOfFigures'],
    ['inserttableoffiguresfield', 'insertTableOfFigures'],
    ['insertbibliography', 'insertBibliography'],
    ['insertcitation', 'insertCitation'],
    ['updatefield', 'updateAllFields'],
    ['updatefields', 'updateAllFields'],
    ['updateallfields', 'updateAllFields'],
    ['insertfootnote', 'insertNote'],
    ['insertendnote', 'insertNote'],
    ['insertpagebreak', 'insertPageBreak'],
    ['deletepagebreak', 'deletePageBreak'],
    ['inserttoken', 'insertToken'],
    ['setpagesettings', 'setPageSettings'],
    ['setpagesetup', 'setPageSettings'],
    ['togglefirstpageheaderfooter', 'toggleDifferentFirstPage'],
    ['differentfirstpage', 'toggleDifferentFirstPage'],
    ['toggleoddevenheaderfooter', 'toggleDifferentOddEven'],
    ['differentoddeven', 'toggleDifferentOddEven'],
]);

export function isFieldCommand(commandId) {
    return FIELD_COMMAND_ALIASES.has(compact(commandId));
}

export function canonicalFieldCommandId(commandId) {
    return FIELD_COMMAND_ALIASES.get(compact(commandId)) || '';
}

export function applyFieldCommand(model, selection, commandId, payload = null) {
    const command = canonicalFieldCommandId(commandId);
    const working = clone(model || {});
    ensureModelCollections(working);

    if (command === 'insertField') {
        return insertField(working, selection, commandId, payload);
    }

    if (command === 'insertNote') {
        return insertNote(working, selection, commandId, payload);
    }

    if (command === 'insertPageBreak') {
        return insertPageBreak(working, selection, payload);
    }

    if (command === 'deletePageBreak') {
        return deletePageBreak(working, selection, payload);
    }

    if (command === 'insertToken') {
        return insertToken(working, selection, payload);
    }

    if (command === 'insertCrossReference') {
        return insertCrossReference(working, selection, payload);
    }

    if (command === 'goToReference') {
        return goToReference(working, selection, payload);
    }

    if (command === 'insertCaption') {
        return insertCaption(working, selection, payload);
    }

    if (command === 'insertTableOfFigures') {
        return insertGeneratedFieldBlock(working, selection, createTableOfFiguresField(payload || {}), 'insertTableOfFigures');
    }

    if (command === 'insertBibliography') {
        return insertGeneratedFieldBlock(working, selection, createBibliographyField(payload || {}), 'insertBibliography');
    }

    if (command === 'insertCitation') {
        return insertCitation(working, selection, payload);
    }

    if (command === 'updateAllFields') {
        return updateFieldsCommand(working, selection, payload);
    }

    if (command === 'setPageSettings') {
        return setPageSettings(working, payload);
    }

    if (command === 'toggleDifferentFirstPage') {
        return toggleSectionFlag(working, 'differentFirstPage', payload);
    }

    if (command === 'toggleDifferentOddEven') {
        return toggleSectionFlag(working, 'differentOddAndEvenPages', payload);
    }

    return unchanged(working, selection, command);
}

export function queryFieldCommandState(model, selection) {
    ensureModelCollections(model);
    const hasBodySelection = !!findEditableBlock(model, selection?.focus?.blockId || selection?.anchor?.blockId);
    const hasDocument = !!model;
    const inHeaderFooter = isHeaderFooterSelection(selection);
    return {
        fields: {
            inHeaderFooter,
            pageSettings: normalizePageSettingsPayload(model?.pageSettings || {}),
        },
        commands: {
            insertfield: commandState(hasDocument),
            insertpagenumber: commandState(hasDocument),
            insertpagecount: commandState(hasDocument),
            insertpagexofy: commandState(hasDocument),
            insertdatefield: commandState(hasDocument),
            insertdocumenttitlefield: commandState(hasDocument),
            insertauthorfield: commandState(hasDocument),
            inserttimefield: commandState(hasDocument),
            insertfilenamefield: commandState(hasDocument),
            insertstylereffield: commandState(hasDocument),
            insertcrossreference: commandState(hasDocument && collectReferenceTargets(model).size > 0),
            gotoreference: commandState(hasDocument),
            insertcaption: commandState(hasBodySelection),
            inserttableoffigures: commandState(hasDocument),
            insertbibliography: commandState(hasDocument),
            insertcitation: commandState(hasDocument && (model?.bibliographySources || []).length > 0),
            updatefield: commandState(hasDocument),
            updatefields: commandState(hasDocument),
            updateallfields: commandState(hasDocument),
            insertfootnote: commandState(hasBodySelection),
            insertendnote: commandState(hasBodySelection),
            insertpagebreak: commandState(hasBodySelection),
            deletepagebreak: commandState(hasDocument),
            inserttoken: commandState(hasDocument),
            setpagesettings: commandState(hasDocument),
            setpagesetup: commandState(hasDocument),
            differentfirstpage: commandState(hasDocument, firstSectionFlag(model, 'differentFirstPage')),
            differentoddeven: commandState(hasDocument, firstSectionFlag(model, 'differentOddAndEvenPages')),
        },
    };
}

function insertField(model, selection, commandId, payload) {
    const fieldType = resolveFieldType(commandId, payload);
    const run = {
        id: payload?.id || payload?.Id || createId('field'),
        type: 'field',
        text: '',
        marks: [],
        field: {
            fieldType,
            format: payload?.format ?? payload?.Format ?? null,
            fallbackText: payload?.fallbackText ?? payload?.FallbackText ?? defaultFieldFallback(fieldType, model),
            displayText: payload?.displayText ?? payload?.DisplayText ?? null,
        },
    };

    const target = resolveInsertionTarget(model, selection, payload, true);
    if (!target) {
        return unchanged(model, selection, 'insertField');
    }

    const cow = withClonedBlock(model, target.block);
    insertRunAtOffset(cow.block, run, target.offset);
    cow.model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model: cow.model,
        selection: {
            anchor: { blockId: cow.block.id, offset: target.offset + createCanvasRunText(run).length },
            focus: { blockId: cow.block.id, offset: target.offset + createCanvasRunText(run).length },
        },
        operation: 'insertField',
        dirtyBlockIds: [cow.block.id],
        insertedRunIds: [String(run.id)],
    };
}

function insertNote(model, selection, commandId, payload) {
    const noteType = compact(commandId) === 'insertendnote' || normalizeNoteType(payload?.noteType ?? payload?.NoteType) === 1 ? 1 : 0;
    const target = resolveInsertionTarget(model, selection, payload, false);
    if (!target) {
        return unchanged(model, selection, 'insertNote');
    }

    const marker = payload?.marker ?? payload?.Marker ?? nextNoteMarker(model, noteType);
    const noteId = payload?.noteId ?? payload?.NoteId ?? createId(noteType === 1 ? 'endnote' : 'footnote');
    const referenceId = payload?.referenceId ?? payload?.ReferenceId ?? createId('note-ref');
    const reference = {
        id: referenceId,
        type: 'noteReference',
        text: '',
        marks: [],
        noteReference: {
            noteId,
            noteType,
            displayMarker: String(marker),
        },
    };
    const cow = withClonedBlock(model, target.block);
    insertRunAtOffset(cow.block, reference, target.offset);
    cow.model.notes = [...(model.notes || []), {
        id: noteId,
        type: noteType,
        sectionId: cow.block.sectionId || firstSection(cow.model)?.id || '',
        marker: String(marker),
        referenceIds: [referenceId],
        blocks: [
            {
                id: `${noteId}-body`,
                sectionId: cow.block.sectionId || firstSection(cow.model)?.id || '',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [
                        {
                            id: `${noteId}-text`,
                            type: 'text',
                            text: String(payload?.text ?? payload?.Text ?? defaultNoteText(noteType)),
                            marks: [],
                        },
                    ],
                },
            },
        ],
    }];
    cow.model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model: cow.model,
        selection: {
            anchor: { blockId: cow.block.id, offset: target.offset + String(marker).length },
            focus: { blockId: cow.block.id, offset: target.offset + String(marker).length },
        },
        operation: noteType === 1 ? 'insertEndnote' : 'insertFootnote',
        dirtyBlockIds: [cow.block.id],
        insertedRunIds: [String(referenceId)],
        noteId,
    };
}

function insertPageBreak(model, selection, payload) {
    const target = resolveInsertionTarget(model, selection, payload, false);
    const bodyBlocks = model.body.blocks || [];
    const targetIndex = target?.block
        ? bodyBlocks.findIndex(block => String(block?.id || '') === String(target.block.id || ''))
        : -1;
    const insertIndex = targetIndex >= 0 ? targetIndex + 1 : bodyBlocks.length;
    const previous = bodyBlocks[Math.max(0, insertIndex - 1)] || target?.block || null;
    const next = bodyBlocks[insertIndex] || null;
    const order = next
        ? (Number(previous?.order || 0) + Number(next.order || 0)) / 2
        : Number(previous?.order || 0) + 10;
    const sectionId = payload?.sectionId ?? payload?.SectionId ?? previous?.sectionId ?? firstSection(model)?.id ?? '';
    const block = {
        id: payload?.id ?? payload?.Id ?? createId('page-break'),
        sectionId,
        type: 'pageBreak',
        order,
        content: {
            type: 'pageBreak',
            pageBreak: {
                breakType: payload?.breakType ?? payload?.BreakType ?? 'page',
                nextSectionId: payload?.nextSectionId ?? payload?.NextSectionId ?? null,
            },
        },
    };

    bodyBlocks.splice(insertIndex, 0, block);
    const dirtyBlockIds = syncSectionBlocks(model, new Set([block.id, previous?.id, next?.id].filter(Boolean).map(String)));
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: next?.id ? collapsedSelection(next.id, 0) : (previous?.id ? collapsedSelection(previous.id, clampOffset(previous, Number.MAX_SAFE_INTEGER)) : selection),
        operation: 'insertPageBreak',
        dirtyBlockIds,
        insertedBlockIds: [block.id],
    };
}

/// Phase 9: the Blazor-side token menu inserts the selected catalog token as a first-class
/// token RUN (type 'token') — the model/renderer already support the pill; plain text would
/// lose the token semantics (key, catalog metadata, DOCX round-trip).
function insertToken(model, selection, payload) {
    const key = String(payload?.key ?? payload?.Key ?? '').trim();
    if (!key) {
        return unchanged(model, selection, 'insertToken');
    }

    const displayName = String(payload?.displayName ?? payload?.DisplayName ?? key);
    const run = {
        id: payload?.id ?? payload?.Id ?? createId('token'),
        type: 'token',
        text: '',
        marks: [],
        token: {
            key,
            displayName,
            description: payload?.description ?? payload?.Description ?? null,
            colorClass: payload?.colorClass ?? payload?.ColorClass ?? null,
            typeLabel: payload?.typeLabel ?? payload?.TypeLabel ?? null,
            tokenType: payload?.tokenType ?? payload?.TokenType ?? null,
            fallbackText: displayName,
        },
    };

    const target = resolveInsertionTarget(model, selection, payload, true);
    if (!target) {
        return unchanged(model, selection, 'insertToken');
    }

    const cow = withClonedBlock(model, target.block);
    insertRunAtOffset(cow.block, run, target.offset);
    cow.model.version = Number(model.version || 0) + 1;
    const caretOffset = target.offset + createCanvasRunText(run).length;
    return {
        changed: true,
        model: cow.model,
        selection: {
            anchor: { blockId: cow.block.id, offset: caretOffset },
            focus: { blockId: cow.block.id, offset: caretOffset },
        },
        operation: 'insertToken',
        dirtyBlockIds: [cow.block.id],
        insertedRunIds: [String(run.id)],
    };
}

// Layout memoizes BY OBJECT REFERENCE at every level — the whole layout by model reference
// (entry.render/progressive layout) and block signatures by block reference (pagination.mjs,
// immutable-model-contract tests). A mutator must therefore return a new model object with a
// cloned touched block, sharing every unchanged reference. And because buildSectionFlows lays
// out sections[].blocks when populated (normalization keeps those as SEPARATE objects from
// body.blocks with the same ids), the swap must reach the section lists too — the same reason
// text-editing runs synchronizeSectionsWithBody.
function withClonedBlock(model, targetBlock) {
    const blocks = Array.isArray(model?.body?.blocks) ? model.body.blocks : [];
    const bodyIndex = blocks.indexOf(targetBlock);
    if (bodyIndex >= 0) {
        const copy = cloneBlockShallow(targetBlock);
        const nextBlocks = blocks.slice();
        nextBlocks[bodyIndex] = copy;
        return {
            model: {
                ...model,
                body: { ...model.body, blocks: nextBlocks },
                sections: sectionsWithBlockSwapped(model, targetBlock, copy),
            },
            block: copy,
        };
    }

    const containers = Array.isArray(model?.headersFooters) ? model.headersFooters : [];
    for (let index = 0; index < containers.length; index += 1) {
        const hfBlocks = Array.isArray(containers[index]?.blocks) ? containers[index].blocks : [];
        const hfIndex = hfBlocks.indexOf(targetBlock);
        if (hfIndex >= 0) {
            const copy = cloneBlockShallow(targetBlock);
            const nextHfBlocks = hfBlocks.slice();
            nextHfBlocks[hfIndex] = copy;
            const nextContainers = containers.slice();
            nextContainers[index] = { ...containers[index], blocks: nextHfBlocks };
            return { model: { ...model, headersFooters: nextContainers }, block: copy };
        }
    }

    // Table-cell (or deeper) nesting: clone the whole body, relocate the block by id and point
    // the section lists at the cloned top-level blocks so the paginator sees the change.
    const nextBody = structuredClone(model?.body ?? { blocks: [] });
    const nextModel = { ...model, body: nextBody };
    const relocated = findEditableBlock(nextModel, targetBlock?.id);
    if (!relocated) {
        return { model, block: targetBlock };
    }

    const bodyById = new Map((nextBody.blocks || []).map(block => [String(block?.id || ''), block]));
    if (Array.isArray(model?.sections)) {
        nextModel.sections = model.sections.map(section => ({
            ...section,
            blocks: (Array.isArray(section?.blocks) ? section.blocks : [])
                .map(block => bodyById.get(String(block?.id || '')) || block),
        }));
    }

    return { model: nextModel, block: relocated };
}

function sectionsWithBlockSwapped(model, targetBlock, copy) {
    if (!Array.isArray(model?.sections)) {
        return model?.sections;
    }

    const targetId = String(targetBlock?.id || '');
    return model.sections.map(section => {
        const sectionBlocks = Array.isArray(section?.blocks) ? section.blocks : [];
        const index = sectionBlocks.findIndex(block => block === targetBlock || String(block?.id || '') === targetId);
        if (index < 0) {
            return section;
        }

        const nextBlocks = sectionBlocks.slice();
        nextBlocks[index] = copy;
        return { ...section, blocks: nextBlocks };
    });
}

function cloneBlockShallow(block) {
    return { ...block, content: structuredClone(block?.content ?? { type: 'paragraph', runs: [] }) };
}

function deletePageBreak(model, selection, payload) {
    const blocks = model.body.blocks || [];
    const isPageBreak = block => String(block?.type || block?.content?.type || '').toLowerCase() === 'pagebreak';
    const requestedId = String(payload?.blockId ?? payload?.BlockId ?? '');
    let index = requestedId
        ? blocks.findIndex(block => String(block?.id || '') === requestedId && isPageBreak(block))
        : -1;

    // Fallback: the page break at the caret, or immediately next to the caret's block.
    if (index < 0 && !requestedId) {
        const caretId = String(selection?.focus?.blockId || selection?.anchor?.blockId || '');
        const caretIndex = blocks.findIndex(block => String(block?.id || '') === caretId);
        if (caretIndex >= 0) {
            if (isPageBreak(blocks[caretIndex])) {
                index = caretIndex;
            } else if (blocks[caretIndex + 1] && isPageBreak(blocks[caretIndex + 1])) {
                index = caretIndex + 1;
            } else if (blocks[caretIndex - 1] && isPageBreak(blocks[caretIndex - 1])) {
                index = caretIndex - 1;
            }
        }
    }

    if (index < 0) {
        return unchanged(model, selection, 'deletePageBreak');
    }

    const removed = blocks.splice(index, 1)[0];
    // Caret to the block that followed the break (the content that flows back), else the previous one.
    const target = blocks[index] || blocks[index - 1] || null;
    const dirtyBlockIds = syncSectionBlocks(model, new Set([removed?.id, target?.id].filter(Boolean).map(String)));
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: target ? collapsedSelection(target.id, 0) : selection,
        operation: 'deletePageBreak',
        dirtyBlockIds,
        removedBlockIds: [String(removed?.id || '')].filter(Boolean),
    };
}

function insertCrossReference(model, selection, payload) {
    const targets = collectReferenceTargets(model);
    const requestedId = String(payload?.targetId ?? payload?.TargetId ?? '').trim();
    const target = requestedId ? targets.get(requestedId) : firstReferenceTarget(targets, payload);
    if (!target) {
        return unchanged(model, selection, 'insertCrossReference');
    }

    const targetBlock = target.blockId ? findEditableBlock(model, target.blockId) : null;
    const run = createCrossReferenceField(target, {
        id: payload?.id ?? payload?.Id,
        targetId: target.id,
        referenceKind: payload?.referenceKind ?? payload?.ReferenceKind ?? target.kind,
        referenceFormat: payload?.referenceFormat ?? payload?.ReferenceFormat ?? payload?.format ?? payload?.Format ?? 'full',
    });
    const targetInsertion = resolveInsertionTarget(model, selection, payload, true);
    if (!targetInsertion) {
        return unchanged(model, selection, 'insertCrossReference');
    }

    insertRunAtOffset(targetInsertion.block, run, targetInsertion.offset);
    updateAllFields(model);
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: collapsedSelection(targetInsertion.block.id, targetInsertion.offset + createCanvasRunText(run).length),
        operation: 'insertCrossReference',
        dirtyBlockIds: Array.from(new Set([targetInsertion.block.id, targetBlock?.id].filter(Boolean))),
    };
}

function goToReference(model, selection, payload) {
    const field = payload?.field || payload?.Field || findFieldAtSelection(model, selection);
    const navigation = resolveCrossReferenceNavigation(model, field);
    if (!navigation?.selection) {
        return unchanged(model, selection, 'goToReference');
    }

    return {
        changed: false,
        model,
        selection: navigation.selection,
        operation: 'goToReference',
        dirtyBlockIds: [],
    };
}

function insertCaption(model, selection, payload) {
    const target = resolveInsertionTarget(model, selection, payload, false);
    if (!target) {
        return unchanged(model, selection, 'insertCaption');
    }

    const blocks = model.body.blocks;
    const targetIndex = blocks.findIndex(block => String(block?.id || '') === String(target.block.id || ''));
    const nextBlock = blocks[targetIndex + 1] || null;
    const caption = createCaptionBlock({
        ...(payload || {}),
        sectionId: target.block.sectionId || firstSection(model)?.id || null,
        order: nextBlock ? (Number(target.block.order || 0) + Number(nextBlock.order || 0)) / 2 : Number(target.block.order || 0) + 10,
    });
    blocks.splice(targetIndex < 0 ? blocks.length : targetIndex + 1, 0, caption);
    syncSectionBlocks(model, new Set([caption.id]));
    const renumber = renumberCaptions(model);
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: collapsedSelection(caption.id, captionTextLength(caption)),
        operation: 'insertCaption',
        dirtyBlockIds: Array.from(new Set([caption.id, ...(renumber.dirtyBlockIds || [])])),
        insertedBlockIds: [caption.id],
    };
}

function insertGeneratedFieldBlock(model, selection, fieldRun, operation) {
    const target = resolveInsertionTarget(model, selection, {}, false) || { block: allBodyBlocks(model).at(-1), offset: 0 };
    const order = Number(target?.block?.order || 0) + 10;
    const block = {
        id: createId(`${operation}-block`),
        sectionId: target?.block?.sectionId || firstSection(model)?.id || null,
        type: 'paragraph',
        order,
        paragraphProperties: { spacingBefore: 10, spacingAfter: 10 },
        content: {
            type: 'paragraph',
            runs: [fieldRun],
        },
        preserve: {},
    };
    model.body.blocks.push(block);
    syncSectionBlocks(model, new Set([block.id]));
    updateAllFields(model);
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: collapsedSelection(block.id, createCanvasRunText(fieldRun).length),
        operation,
        dirtyBlockIds: [block.id],
        insertedBlockIds: [block.id],
    };
}

function insertCitation(model, selection, payload) {
    const source = resolveCitationSource(model, payload);
    if (!source) {
        return unchanged(model, selection, 'insertCitation');
    }

    const citationId = payload?.citationId ?? payload?.CitationId ?? createId('citation');
    const run = {
        id: payload?.id || payload?.Id || createId('citation-run'),
        type: 'field',
        text: '',
        marks: [],
        field: {
            fieldType: FIELD_TYPES.citation,
            instrText: `CITATION ${source.id}`,
            targetId: source.id,
            citationId: source.id,
            fallbackText: '',
            displayText: null,
            cachedResult: null,
        },
    };
    model.citations = Array.isArray(model.citations) ? model.citations : [];
    model.citations.push({
        id: citationId,
        sourceId: source.id,
        runId: run.id,
        locator: payload?.locator ?? payload?.Locator ?? null,
        displayText: null,
    });

    const target = resolveInsertionTarget(model, selection, payload, true);
    if (!target) {
        return unchanged(model, selection, 'insertCitation');
    }

    insertRunAtOffset(target.block, run, target.offset);
    updateAllFields(model);
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: collapsedSelection(target.block.id, target.offset + createCanvasRunText(run).length),
        operation: 'insertCitation',
        dirtyBlockIds: [target.block.id],
    };
}

function updateFieldsCommand(model, selection, payload) {
    const renumber = renumberCaptions(model);
    const updated = updateAllFields(model, payload || {});
    return {
        changed: renumber.changed || updated.changed,
        model,
        selection,
        operation: 'updateAllFields',
        dirtyBlockIds: Array.from(new Set([...(renumber.dirtyBlockIds || []), ...(updated.dirtyBlockIds || [])])),
    };
}

function setPageSettings(model, payload) {
    const section = targetSection(model, payload) || firstSection(model);
    const currentPageSettings = normalizePageSettingsPayload(section?.properties?.pageSettings || section?.pageSettings || model.pageSettings || {});
    const currentColumns = normalizeColumnsPayload(section?.properties?.columns || {});
    const currentLineNumbering = normalizeLineNumberingPayload(section?.properties?.lineNumbering || {});
    const currentNoteNumbering = normalizeNoteNumberingPayload(section?.properties?.noteNumbering || {});
    const nextPageSettings = normalizePageSettingsPayload(extractPageSettingsPayload(payload), currentPageSettings);
    const nextColumns = normalizeColumnsPayload(extractColumnsPayload(payload), currentColumns);
    const nextLineNumbering = normalizeLineNumberingPayload(extractLineNumberingPayload(payload), currentLineNumbering);
    const nextNoteNumbering = normalizeNoteNumberingPayload(extractNoteNumberingPayload(payload), currentNoteNumbering);
    const currentSetup = {
        pageSettings: currentPageSettings,
        columns: currentColumns,
        lineNumbering: currentLineNumbering,
        noteNumbering: currentNoteNumbering,
    };
    const nextSetup = {
        pageSettings: nextPageSettings,
        columns: nextColumns,
        lineNumbering: nextLineNumbering,
        noteNumbering: nextNoteNumbering,
    };
    if (JSON.stringify(currentSetup) === JSON.stringify(nextSetup)) {
        return unchanged(model, null, 'setPageSettings');
    }

    model.pageSettings = { ...nextPageSettings };

    if (section) {
        section.pageSettings = { ...nextPageSettings };
        section.properties = {
            ...(section.properties || {}),
            pageSettings: { ...nextPageSettings },
            columns: clone(nextColumns),
            lineNumbering: clone(nextLineNumbering),
            noteNumbering: clone(nextNoteNumbering),
        };
    }

    model.version = Number(model.version || 0) + 1;
    return { changed: true, model, selection: null, operation: 'setPageSettings', dirtyBlockIds: allBodyBlockIds(model) };
}

function toggleSectionFlag(model, key, payload = null) {
    const section = firstSection(model);
    if (!section) {
        return unchanged(model, null, key);
    }

    section.properties = section.properties || {};
    // Set-mode when the payload carries the target state (the C# ribbon checkbox sends
    // {enabled} so the host and engine cannot diverge); blind toggle otherwise (legacy
    // togglefirstpageheaderfooter/toggleoddevenheaderfooter callers).
    const requested = payload?.enabled ?? payload?.Enabled;
    const next = requested == null ? section.properties[key] !== true : requested === true;
    if ((section.properties[key] === true) === next) {
        return unchanged(model, null, key);
    }

    section.properties[key] = next;
    model.version = Number(model.version || 0) + 1;
    return { changed: true, model, selection: null, operation: key, dirtyBlockIds: [] };
}

function resolveInsertionTarget(model, selection, payload, allowHeaderFooter) {
    const blockId = String(payload?.blockId ?? payload?.BlockId ?? selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    let block = findEditableBlock(model, blockId);
    if (!block && allowHeaderFooter) {
        block = findHeaderFooterBlock(model, blockId)
            || firstHeaderFooterBlock(model)
            || firstEditableBlock(model);
    }

    block ??= firstEditableBlock(model);
    if (!block) {
        return null;
    }

    const requestedOffset = payload?.offset ?? payload?.Offset ?? selection?.focus?.offset ?? selection?.anchor?.offset;
    const offset = clampOffset(block, requestedOffset);
    return { block, offset };
}

function insertRunAtOffset(block, run, offset) {
    const runs = runsOrEmpty(block);
    const textLength = runs.reduce((total, item) => total + createCanvasRunText(item).length, 0);
    let targetOffset = Math.max(0, Math.min(textLength, Number(offset || 0) || 0));
    let cursor = 0;
    for (let index = 0; index < runs.length; index += 1) {
        const current = runs[index];
        const text = createCanvasRunText(current);
        const end = cursor + text.length;
        if (targetOffset <= end) {
            if (targetOffset === cursor) {
                runs.splice(index, 0, run);
                return;
            }

            if (targetOffset === end) {
                runs.splice(index + 1, 0, run);
                return;
            }

            const local = targetOffset - cursor;
            const left = { ...current, id: current.id ? `${current.id}-l` : createId('text'), text: text.slice(0, local) };
            const right = { ...current, id: current.id ? `${current.id}-r` : createId('text'), text: text.slice(local) };
            runs.splice(index, 1, left, run, right);
            return;
        }

        cursor = end;
    }

    runs.push(run);
}

function resolveFieldType(commandId, payload) {
    const explicit = payload?.fieldType ?? payload?.FieldType;
    if (explicit != null) {
        if (typeof explicit === 'number') {
            return Math.max(0, Math.trunc(explicit));
        }

        const normalized = compact(explicit);
        const byName = {
            pagenumber: FIELD_TYPES.pageNumber,
            pagecount: FIELD_TYPES.pageCount,
            totalpages: FIELD_TYPES.pageCount,
            pagexofy: FIELD_TYPES.pageXOfY,
            date: FIELD_TYPES.date,
            documenttitle: FIELD_TYPES.documentTitle,
            title: FIELD_TYPES.documentTitle,
            author: FIELD_TYPES.author,
            filename: FIELD_TYPES.fileName,
            file: FIELD_TYPES.fileName,
            time: FIELD_TYPES.time,
            styleref: FIELD_TYPES.styleRef,
            ref: FIELD_TYPES.ref,
            seq: FIELD_TYPES.seq,
            tableoffigures: FIELD_TYPES.tableOfFigures,
            tof: FIELD_TYPES.tableOfFigures,
            bibliography: FIELD_TYPES.bibliography,
            citation: FIELD_TYPES.citation,
        };
        if (normalized in byName) {
            return byName[normalized];
        }
    }

    const command = compact(commandId);
    if (command === 'insertpagecount') {
        return FIELD_TYPES.pageCount;
    }

    if (command === 'insertpagexofy') {
        return FIELD_TYPES.pageXOfY;
    }

    if (command === 'insertdatefield') {
        return FIELD_TYPES.date;
    }

    if (command === 'insertdocumenttitlefield') {
        return FIELD_TYPES.documentTitle;
    }

    if (command === 'insertauthorfield') {
        return FIELD_TYPES.author;
    }

    if (command === 'inserttimefield') {
        return FIELD_TYPES.time;
    }

    if (command === 'insertfilenamefield') {
        return FIELD_TYPES.fileName;
    }

    if (command === 'insertstylereffield') {
        return FIELD_TYPES.styleRef;
    }

    return FIELD_TYPES.pageNumber;
}

function defaultFieldFallback(fieldType, model) {
    if (fieldType === FIELD_TYPES.pageCount) {
        return '1';
    }

    if (fieldType === FIELD_TYPES.pageXOfY) {
        return '1 / 1';
    }

    if (fieldType === FIELD_TYPES.date) {
        return new Date().toLocaleDateString();
    }

    if (fieldType === FIELD_TYPES.documentTitle) {
        return String(model?.metadata?.title || model?.metadata?.Title || '');
    }

    if (fieldType === FIELD_TYPES.author) {
        return String(model?.metadata?.author?.displayName || model?.metadata?.Author?.DisplayName || '');
    }

    if (fieldType === FIELD_TYPES.time) {
        return new Date().toLocaleTimeString();
    }

    if (fieldType === FIELD_TYPES.fileName) {
        return String(model?.metadata?.fileName || model?.metadata?.FileName || model?.documentId || '');
    }

    if (fieldType === FIELD_TYPES.styleRef) {
        return '';
    }

    return '1';
}

function nextNoteMarker(model, noteType) {
    const section = firstSection(model);
    const settings = normalizeNoteNumberingPayload(section?.properties?.noteNumbering || {});
    const count = (model.notes || []).filter(note => {
        if (normalizeNoteType(note?.type ?? note?.Type) !== noteType) {
            return false;
        }

        return settings.restartEachSection !== true
            || !section?.id
            || !note?.sectionId
            || String(note.sectionId) === String(section.id);
    }).length;
    return formatNoteMarker(settings.startAt + count, settings.style);
}

function defaultNoteText(noteType) {
    return noteType === 1 ? 'Endnote' : 'Footnote';
}

function formatNoteMarker(value, style) {
    const number = Math.max(1, Math.trunc(Number(value) || 1));
    const normalized = normalizeNoteNumberingStyle(style);
    if (normalized === 'lowerRoman') {
        return toRoman(number).toLowerCase();
    }

    if (normalized === 'upperRoman') {
        return toRoman(number).toUpperCase();
    }

    if (normalized === 'lowerLetter') {
        return toLetters(number).toLowerCase();
    }

    if (normalized === 'upperLetter') {
        return toLetters(number).toUpperCase();
    }

    return String(number);
}

function toRoman(value) {
    const symbols = [
        [1000, 'M'],
        [900, 'CM'],
        [500, 'D'],
        [400, 'CD'],
        [100, 'C'],
        [90, 'XC'],
        [50, 'L'],
        [40, 'XL'],
        [10, 'X'],
        [9, 'IX'],
        [5, 'V'],
        [4, 'IV'],
        [1, 'I'],
    ];
    let remaining = Math.max(1, Math.min(3999, Math.trunc(Number(value) || 1)));
    let result = '';
    for (const [amount, marker] of symbols) {
        while (remaining >= amount) {
            result += marker;
            remaining -= amount;
        }
    }

    return result;
}

function toLetters(value) {
    let remaining = Math.max(1, Math.trunc(Number(value) || 1));
    let result = '';
    while (remaining > 0) {
        remaining -= 1;
        result = String.fromCharCode(65 + (remaining % 26)) + result;
        remaining = Math.floor(remaining / 26);
    }

    return result;
}

function extractPageSettingsPayload(input) {
    const source = input || {};
    return source.pageSettings || source.PageSettings || source;
}

function extractColumnsPayload(input) {
    const source = input || {};
    return source.columns || source.Columns || null;
}

function extractLineNumberingPayload(input) {
    const source = input || {};
    return source.lineNumbering || source.LineNumbering || null;
}

function extractNoteNumberingPayload(input) {
    const source = input || {};
    return source.noteNumbering || source.NoteNumbering || null;
}

function normalizePageSettingsPayload(input, fallback = null) {
    const source = input || {};
    const size = source.size || source.Size || {};
    const margins = source.margins || source.Margins || {};
    const documentSettingsPayload = !!(source.size || source.Size || source.margins || source.Margins);
    const landscape = source.landscape === true || source.Landscape === true
        || ((source.landscape ?? source.Landscape) == null && fallback?.landscape === true);
    const sizeWidth = positive(size.width ?? size.Width, 0);
    const sizeHeight = positive(size.height ?? size.Height, 0);
    const widthFromSize = sizeWidth > 0
        ? pointsToCssPixelsOrNull(landscape ? Math.max(sizeWidth, sizeHeight || sizeWidth) : sizeWidth)
        : null;
    const heightFromSize = sizeHeight > 0
        ? pointsToCssPixelsOrNull(landscape ? Math.min(sizeWidth || sizeHeight, sizeHeight) : sizeHeight)
        : null;
    return {
        width: positive(source.width ?? source.Width ?? widthFromSize, fallback?.width ?? 794),
        height: positive(source.height ?? source.Height ?? heightFromSize, fallback?.height ?? 1123),
        marginTop: nonNegative(source.marginTop ?? source.MarginTop ?? pointsToCssPixelsOrNull(margins.top ?? margins.Top), fallback?.marginTop ?? 72),
        marginRight: nonNegative(source.marginRight ?? source.MarginRight ?? pointsToCssPixelsOrNull(margins.right ?? margins.Right), fallback?.marginRight ?? 72),
        marginBottom: nonNegative(source.marginBottom ?? source.MarginBottom ?? pointsToCssPixelsOrNull(margins.bottom ?? margins.Bottom), fallback?.marginBottom ?? 72),
        marginLeft: nonNegative(source.marginLeft ?? source.MarginLeft ?? pointsToCssPixelsOrNull(margins.left ?? margins.Left), fallback?.marginLeft ?? 72),
        headerDistanceFromTop: nonNegative(documentSettingsPayload ? pointsToCssPixelsOrNull(source.headerDistanceFromTop ?? source.HeaderDistanceFromTop) : source.headerDistanceFromTop ?? source.HeaderDistanceFromTop, fallback?.headerDistanceFromTop ?? 48),
        footerDistanceFromBottom: nonNegative(documentSettingsPayload ? pointsToCssPixelsOrNull(source.footerDistanceFromBottom ?? source.FooterDistanceFromBottom) : source.footerDistanceFromBottom ?? source.FooterDistanceFromBottom, fallback?.footerDistanceFromBottom ?? 48),
        sizeName: String(source.sizeName ?? source.SizeName ?? size.name ?? size.Name ?? fallback?.sizeName ?? 'Custom'),
        landscape,
    };
}

function normalizeColumnsPayload(input, fallback = null) {
    const source = input || fallback || {};
    const count = Math.max(1, Math.min(3, integer(source.count ?? source.Count, fallback?.count ?? 1)));
    const items = Array.isArray(source.items || source.Items)
        ? (source.items || source.Items).map(item => ({
            width: nullablePositive(item?.width ?? item?.Width),
            spacingAfter: nullableNonNegative(item?.spacingAfter ?? item?.SpacingAfter),
        }))
        : [];
    const balance = booleanWithFallback(
        source.balance ?? source.Balance ?? source.balanced ?? source.Balanced ?? source.newspaperBalance ?? source.NewspaperBalance,
        fallback?.balance ?? fallback?.Balance ?? fallback?.balanced ?? fallback?.Balanced ?? fallback?.newspaperBalance ?? fallback?.NewspaperBalance ?? false);
    return {
        count,
        spacing: nonNegative(source.spacing ?? source.Spacing, fallback?.spacing ?? 36),
        separatorLine: source.separatorLine === true || source.SeparatorLine === true,
        balance,
        preset: String(source.preset ?? source.Preset ?? fallback?.preset ?? presetForColumnCount(count)),
        items,
    };
}

function booleanWithFallback(value, fallback = false) {
    if (value === true || value === false) {
        return value;
    }

    if (value == null) {
        return fallback === true;
    }

    const normalized = String(value).trim().toLowerCase();
    return normalized === 'true' || normalized === '1' || normalized === 'yes' || normalized === 'newspaper';
}

function normalizeLineNumberingPayload(input, fallback = null) {
    const source = input || fallback || {};
    return {
        enabled: source.enabled === true || source.Enabled === true,
        startAt: Math.max(1, integer(source.startAt ?? source.StartAt, fallback?.startAt ?? 1)),
        increment: Math.max(1, integer(source.increment ?? source.Increment, fallback?.increment ?? 1)),
        distanceFromText: nonNegative(source.distanceFromText ?? source.DistanceFromText, fallback?.distanceFromText ?? 18),
        restart: normalizeLineNumberingRestart(source.restart ?? source.Restart ?? fallback?.restart),
    };
}

function normalizeNoteNumberingPayload(input, fallback = null) {
    const source = input || fallback || {};
    return {
        style: normalizeNoteNumberingStyle(source.style ?? source.Style ?? fallback?.style),
        startAt: Math.max(1, integer(source.startAt ?? source.StartAt, fallback?.startAt ?? 1)),
        restartEachSection: booleanWithFallback(
            source.restartEachSection ?? source.RestartEachSection,
            fallback?.restartEachSection ?? fallback?.RestartEachSection ?? true),
    };
}

function ensureModelCollections(model) {
    model.body = model.body || { blocks: [] };
    model.body.blocks = Array.isArray(model.body.blocks) ? model.body.blocks : [];
    model.sections = Array.isArray(model.sections) ? model.sections : [];
    model.headersFooters = Array.isArray(model.headersFooters) ? model.headersFooters : [];
    model.notes = Array.isArray(model.notes) ? model.notes : [];
    model.bibliographySources = Array.isArray(model.bibliographySources) ? model.bibliographySources : [];
    model.citations = Array.isArray(model.citations) ? model.citations : [];
}

function firstReferenceTarget(targets, payload = null) {
    const preferred = compact(payload?.referenceKind ?? payload?.ReferenceKind ?? '');
    const values = Array.from(targets.values());
    return values.find(target => preferred && compact(target.kind) === preferred)
        || values.find(target => target.kind === 'heading')
        || values.find(target => target.kind === 'bookmark')
        || values.find(target => target.kind === 'caption')
        || values[0]
        || null;
}

function findFieldAtSelection(model, selection) {
    const blockId = String(selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const offset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;
    const block = findEditableBlock(model, blockId) || firstEditableBlock(model);
    if (!block) {
        return null;
    }

    let cursor = 0;
    let firstField = null;
    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const end = cursor + text.length;
        if (String(run?.type || '') === 'field') {
            firstField ??= run;
            if (offset >= cursor && offset <= end) {
                return run;
            }
        }

        cursor = end;
    }

    return firstField
        || allBodyBlocks(model).flatMap(candidate => runsOrEmpty(candidate)).find(run => String(run?.type || '') === 'field')
        || null;
}

function createBibliographyField(payload = {}) {
    return {
        id: payload.id || payload.Id || createId('bibliography'),
        type: 'field',
        text: '',
        marks: [],
        field: {
            fieldType: FIELD_TYPES.bibliography,
            instrText: 'BIBLIOGRAPHY',
            fallbackText: '',
            displayText: null,
            cachedResult: null,
        },
    };
}

function resolveCitationSource(model, payload = null) {
    const requested = String(payload?.sourceId ?? payload?.SourceId ?? payload?.targetId ?? payload?.TargetId ?? '').trim();
    if (requested) {
        return (model.bibliographySources || []).find(source => String(source?.id || '') === requested) || null;
    }

    return (model.bibliographySources || [])[0] || null;
}

function captionTextLength(block) {
    return runsOrEmpty(block).reduce((total, run) => total + createCanvasRunText(run).length, 0);
}

function collapsedSelection(blockId, offset) {
    const position = { blockId: String(blockId || ''), offset: Math.max(0, Number(offset || 0) || 0) };
    return { anchor: position, focus: { ...position } };
}

function syncSectionBlocks(model, dirtyBlockIds = new Set()) {
    const bodyBlocks = model.body.blocks || [];
    for (const section of model.sections || []) {
        const sectionId = String(section?.id || '');
        section.blocks = bodyBlocks
            .filter(block => String(block?.sectionId || '') === sectionId)
            .map(block => clone(block));
        for (const block of section.blocks) {
            if (block?.id) {
                dirtyBlockIds.add(String(block.id));
            }
        }
    }

    return Array.from(dirtyBlockIds);
}

function findEditableBlock(model, blockId) {
    const id = String(blockId || '');
    return allBodyBlocks(model).find(block => String(block?.id || '') === id) || null;
}

function firstEditableBlock(model) {
    return allBodyBlocks(model).find(block => Array.isArray(block?.content?.runs)) || null;
}

function allBodyBlocks(model) {
    const stack = Array.isArray(model?.body?.blocks) ? [...model.body.blocks].reverse() : [];
    const result = [];
    while (stack.length > 0) {
        const block = stack.pop();
        if (!block) {
            continue;
        }

        result.push(block);
        const rows = block?.content?.table?.rows;
        if (Array.isArray(rows)) {
            for (let rowIndex = rows.length - 1; rowIndex >= 0; rowIndex -= 1) {
                for (const cell of [...(rows[rowIndex]?.cells || [])].reverse()) {
                    for (const nested of [...(cell?.blocks || [])].reverse()) {
                        stack.push(nested);
                    }
                }
            }
        }
    }

    return result;
}

function findHeaderFooterBlock(model, blockId) {
    const id = String(blockId || '');
    return allHeaderFooterBlocks(model).find(block => String(block?.id || '') === id) || null;
}

function firstHeaderFooterBlock(model) {
    return allHeaderFooterBlocks(model).find(block => Array.isArray(block?.content?.runs)) || null;
}

function allHeaderFooterBlocks(model) {
    return (model?.headersFooters || []).flatMap(item => Array.isArray(item?.blocks) ? item.blocks : []);
}

function runsOrEmpty(block) {
    block.content = block.content || { type: 'paragraph', runs: [] };
    block.content.runs = Array.isArray(block.content.runs) ? block.content.runs : [];
    return block.content.runs;
}

function clampOffset(block, offset) {
    const length = runsOrEmpty(block).reduce((total, run) => total + createCanvasRunText(run).length, 0);
    return Math.max(0, Math.min(length, Number(offset || 0) || 0));
}

function firstSection(model) {
    return (model?.sections || []).slice().sort((left, right) => (Number(left?.order || 0) || 0) - (Number(right?.order || 0) || 0))[0] || null;
}

function targetSection(model, payload) {
    const sectionId = String(payload?.sectionId ?? payload?.SectionId ?? '').trim();
    if (!sectionId) {
        return firstSection(model);
    }

    return (model?.sections || []).find(section => String(section?.id || '') === sectionId) || firstSection(model);
}

function firstSectionFlag(model, key) {
    return firstSection(model)?.properties?.[key] === true;
}

function isHeaderFooterSelection(selection) {
    return ['header', 'footer'].includes(String(selection?.region || selection?.Region || '').toLowerCase());
}

function allBodyBlockIds(model) {
    return allBodyBlocks(model).map(block => String(block?.id || '')).filter(Boolean);
}

function normalizeNoteType(value) {
    if (typeof value === 'number') {
        return value === 1 ? 1 : 0;
    }

    return compact(value) === 'endnote' ? 1 : 0;
}

function normalizeNoteNumberingStyle(value) {
    const normalized = compact(value || 'decimal');
    if (normalized === 'lowerroman') {
        return 'lowerRoman';
    }

    if (normalized === 'upperroman') {
        return 'upperRoman';
    }

    if (normalized === 'lowerletter' || normalized === 'loweralpha') {
        return 'lowerLetter';
    }

    if (normalized === 'upperletter' || normalized === 'upperalpha') {
        return 'upperLetter';
    }

    return 'decimal';
}

function commandState(enabled, active = false) {
    return { disabled: !enabled, active: !!active, mixed: false, value: null, state: active ? 'active' : 'inactive' };
}

function positive(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function nonNegative(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}

function integer(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.trunc(parsed) : fallback;
}

function nullablePositive(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function nullableNonNegative(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

function pointsToCssPixelsOrNull(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed * 96 / 72 : null;
}

function presetForColumnCount(count) {
    if (count === 2) {
        return 'two';
    }

    if (count === 3) {
        return 'three';
    }

    return 'one';
}

function normalizeLineNumberingRestart(value) {
    const normalized = compact(value);
    if (normalized === 'page' || normalized === 'perpage') {
        return 'page';
    }

    if (normalized === 'section' || normalized === 'persection') {
        return 'section';
    }

    return 'continuous';
}

function unchanged(model, selection, operation) {
    return { changed: false, model, selection, operation, dirtyBlockIds: [] };
}

function compact(value) {
    return String(value == null ? '' : value).replace(/[\s_-]/g, '').toLowerCase();
}

function createId(prefix) {
    const random = Math.random().toString(36).slice(2, 10);
    return `${prefix}-${Date.now().toString(36)}-${random}`;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
