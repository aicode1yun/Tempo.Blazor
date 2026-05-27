// Phase D — runtime/entry.mjs
// Entry point consumed by tests/Tempo.Blazor.Tests/jsbuild/esbuild.mjs. It re-exports
// every module that has been migrated out of the legacy monolith so that the bundler
// produces a deterministic public surface (window.tmDocumentEditorModules).
//
// As the extraction progresses, more re-exports land here and the IIFE in
// document-editor-wysiwyg.js drops its inline copies.

import helpers, * as helperNamed from '../core/helpers.mjs';
import { DocumentSchemaRegistry, createDefaultSchemaRegistry }
    from '../core/schema.mjs';
import {
    blockText,
    isEditableTextBlock,
    clampTextBoundary,
    clampTextRange,
    tableColumnCount,
} from '../core/text-helpers.mjs';
import {
    findBlockContainer,
    findCell,
    findTableInfo,
    findTableInfoByCellId,
    findTableInfoByBlockId,
    findTableBlockByScan,
} from '../core/model-finders.mjs';
import { OperationTypes, TransactionTypes, isTypingLikeTransactionType }
    from '../history/operation-types.mjs';
import { createIdCounters } from '../history/id-counters.mjs';
import {
    createOperationsModule,
    isSelectionOnlyOperation,
    operationsAffectDocument,
    transactionAffectsDocument,
    supportsOperationHistory,
    supportsLightweightTransactionSnapshots,
} from '../history/operations.mjs';
import {
    normalizeTextExclusionColumnIndex,
    normalizeTarget,
    normalizeRange,
} from '../core/normalize-target.mjs';
import {
    MarkTypeNames,
    markType,
    markValue,
    markOrderValue,
    markKey,
    markSortKey,
    normalizeMark,
    normalizeMarks,
    updateMarks,
    readInlineMarkType,
    readCommentIdFromMark,
    readCommentIdsFromRun,
    readRevisionIdFromMark,
    readRevisionIdFromMarks,
    readRevisionIdsFromRun,
} from '../core/marks.mjs';
import {
    exportBlockType,
    exportHeaderFooterType,
    exportHeaderFooterScope,
    exportFieldType,
    exportCommentAnchorType,
    exportCommentStatus,
    exportCommentVisibility,
    exportRevisionType,
    exportRevisionAction,
    exportRevisionAuthor,
    exportDateTimeOffset,
    exportTextAlignment,
} from '../core/export-types.mjs';
import {
    isDrawingRunSource,
    normalizeDrawingRun,
    importInlineRun,
    exportInlineRun,
    normalizeTextRunForMerge,
    mergeAdjacentTextRuns,
    plainRuns,
} from '../core/inline-runs.mjs';
import { LayoutScopeKinds } from '../layout/scope-kinds.mjs';
import { createLayoutScope, inferLayoutScopeFromOperation } from '../layout/layout-scope.mjs';
import {
    normalizePageBox,
    normalizePageLayoutSettings,
    createPageLayout,
    createPageBreakLayout,
    shiftRectY,
    shiftLayoutLine,
    shiftLayoutSegment,
    shiftCaretStop,
    resolveFieldRunText,
    cloneBlockWithResolvedFields,
} from '../layout/page-metrics.mjs';
import {
    WrapModeNames,
    WrapSideNames,
    normalizeWrapModeName,
    normalizeWrapSideName,
    wrapSideToValue,
} from '../objects/wrap-modes.mjs';
import { normalizeDrawingKindName, exportDrawingKind } from '../objects/drawing-kind.mjs';

// Single namespace export grouped by domain — mirrors the planned module layout from
// planning/tmdocumenteditor-performance-and-features-todo-2026-05-26.md §6.D2.
export const core = {
    helpers,
    helperNamed,
    DocumentSchemaRegistry,
    createDefaultSchemaRegistry,
    text: {
        blockText,
        isEditableTextBlock,
        clampTextBoundary,
        clampTextRange,
        tableColumnCount,
    },
    finders: {
        findBlockContainer,
        findCell,
        findTableInfo,
        findTableInfoByCellId,
        findTableInfoByBlockId,
        findTableBlockByScan,
    },
    coords: {
        normalizeTextExclusionColumnIndex,
        normalizeTarget,
        normalizeRange,
    },
    marks: {
        MarkTypeNames,
        markType,
        markValue,
        markOrderValue,
        markKey,
        markSortKey,
        normalizeMark,
        normalizeMarks,
        updateMarks,
        readInlineMarkType,
        readCommentIdFromMark,
        readCommentIdsFromRun,
        readRevisionIdFromMark,
        readRevisionIdFromMarks,
        readRevisionIdsFromRun,
    },
    exportTypes: {
        exportBlockType,
        exportHeaderFooterType,
        exportHeaderFooterScope,
        exportFieldType,
        exportCommentAnchorType,
        exportCommentStatus,
        exportCommentVisibility,
        exportRevisionType,
        exportRevisionAction,
        exportRevisionAuthor,
        exportDateTimeOffset,
        exportTextAlignment,
    },
    inlineRuns: {
        isDrawingRunSource,
        normalizeDrawingRun,
        importInlineRun,
        exportInlineRun,
        normalizeTextRunForMerge,
        mergeAdjacentTextRuns,
        plainRuns,
    },
};

export const history = {
    OperationTypes,
    TransactionTypes,
    isTypingLikeTransactionType,
    createIdCounters,
    createOperationsModule,
    isSelectionOnlyOperation,
    operationsAffectDocument,
    transactionAffectsDocument,
    supportsOperationHistory,
    supportsLightweightTransactionSnapshots,
};

export const layout = {
    LayoutScopeKinds,
    createLayoutScope,
    inferLayoutScopeFromOperation,
    pageMetrics: {
        normalizePageBox,
        normalizePageLayoutSettings,
        createPageLayout,
        createPageBreakLayout,
        shiftRectY,
        shiftLayoutLine,
        shiftLayoutSegment,
        shiftCaretStop,
        resolveFieldRunText,
        cloneBlockWithResolvedFields,
    },
};

export const objects = {
    WrapModeNames,
    WrapSideNames,
    normalizeWrapModeName,
    normalizeWrapSideName,
    wrapSideToValue,
    normalizeDrawingKindName,
    exportDrawingKind,
};

// Top-level default — what `window.tmDocumentEditorModules` becomes after the IIFE wrap.
export default Object.freeze({
    core,
    history,
    layout,
    objects,
    version: 'phase-d-skeleton-6',
});
