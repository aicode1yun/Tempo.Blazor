// Phase D — runtime/entry.mjs
// Historical module aggregation entry point. The canvas editor imports shared modules
// directly and no longer ships the legacy bundled runtime.

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
import {
    importParagraphContent,
    importImageObject,
    importTable,
    importBlock,
    importRegion,
} from '../core/block-import.mjs';
import { exportBlock, readCommentId } from '../core/block-export.mjs';
import { exportComment, exportRevision } from '../core/comment-revision-export.mjs';
import {
    exportToCSharpJson,
    exportRevisionsToCSharpJson,
    exportCommentsToCSharpJson,
} from '../core/document-export.mjs';
import { validateModel } from '../core/validate-model.mjs';
import {
    fromCanonicalDocument,
    toCanonicalDocument,
    normalizeCanonicalDocument,
    normalizeCanonicalSnapshot,
    roundTripCanonicalDocument,
    diffCanonicalDocuments,
    normalizeInline,
    normalizeInlines,
    normalizeBlock,
    normalizeBlocks,
    normalizeBlockContent,
    normalizeParagraphContent,
    normalizeTableContent,
    normalizeImageContent,
    normalizeHeaderFooter,
} from '../core/canonical-document.mjs';
import { createObjectSelectionSnapshotFactory } from '../core/object-selection-snapshot.mjs';
import { createSelectionNormalizers } from '../core/selection-normalize.mjs';
import { createSelectionPostFixerFactory } from '../core/selection-post-fixer.mjs';
import {
    stableJsonString,
    hashStableString,
    createDocumentFingerprint,
    createSelectionDocumentFingerprint,
} from '../core/fingerprint.mjs';
import {
    createAccessibilityAnnouncer,
    announcementDebounceMs,
} from '../accessibility/announcements.mjs';
import { readOptionalBoolean } from '../core/value-readers.mjs';
import {
    normalizeAnchorRegionName,
    anchorRegionToValue,
    readObjectLayoutInCell,
} from '../objects/anchor-region.mjs';
import {
    normalizeTextExclusionPageIndex,
    createTextExclusionScopeKey,
    readTextExclusionScope,
} from '../layout/text-exclusion.mjs';
import {
    createTextExclusionScopeDescriptor,
    textExclusionMatchesScope,
} from '../layout/text-exclusion-scope.mjs';
import { createTextExclusion } from '../layout/text-exclusion-factory.mjs';
import { createAnchoredDrawingRunCollector } from '../objects/anchored-drawing-collector.mjs';
import {
    resolvePositionReferenceRect,
    resolveAlignedHorizontal,
    resolveAlignedVertical,
    resolveAnchoredDrawingRect,
} from '../objects/anchored-drawing-position.mjs';
import {
    intervalEndGeometry,
    subtractGeometryInterval,
    objectOverlapCollisionRect,
    resolveObjectOverlapGeometry,
} from '../objects/overlap-geometry.mjs';
import { createAnchoredDrawingResolvers } from '../objects/anchored-drawing-layout.mjs';
import { createParagraphLayoutEngineFactory } from '../layout/paragraph-engine.mjs';
import { flattenParagraphRuns } from '../layout/paragraph-runs.mjs';
import { createRenderHost } from '../core-engine/render-host.mjs';
import { createCoreEditor } from '../core-engine/core-editor.mjs';
import { invertOperation, applyToText, applyOps, transformOperation, transformAgainstList } from '../core-engine/operations.mjs';
import { createCollabClient, createRelayCollabClient, transformChange } from '../core-engine/collab-client.mjs';
import { createInputSurface } from '../core-engine/input-surface.mjs';
import { serializeRange as coreSerializeRange, parseClipboard as coreParseClipboard, parseHtml as coreParseHtml, parsePlainText as coreParsePlainText } from '../core-engine/clipboard.mjs';
import {
    applyInsertText,
    applyDeleteBackward,
    applyDeleteForward,
    applyInsertParagraph,
    applyReplaceRange,
} from '../core-engine/edit-model.mjs';
import { hitTestPoint, collectCaretStops, caretStopAt, lineCaretStops } from '../core-engine/hit-test.mjs';
import { moveCaretByKey, caretRect, blockMaxOffset, createCaretElement } from '../core-engine/caret.mjs';
import { selectionRectsForRange, createSelectionRectElement, createCompositionUnderlineElement } from '../core-engine/selection-overlay.mjs';
import { applyBidiToLayout, applyBidiToBlock } from '../core-engine/bidi-line.mjs';
import { applyMarkToBlockRange, blockRangeHasMark, setParagraphProperty, firstMarkValueInRange } from '../core-engine/edit-format.mjs';
import { createObjectElement, objectHitTest, resizeRectByHandle, RESIZE_HANDLES } from '../core-engine/object-overlay.mjs';
import { createUndoStack } from '../core-engine/undo-stack.mjs';
import { applyParagraphStyle, getDocumentOutline, paragraphStyleName, DEFAULT_PARAGRAPH_STYLES } from '../core-engine/paragraph-styles.mjs';
import { createTableModel, insertTableAfterBlock, addTableRow, addTableColumn, findTableContaining } from '../core-engine/edit-table.mjs';
import { findMatches } from '../core-engine/find-replace.mjs';
import { acceptAllRevisions, rejectAllRevisions, listRevisions, hasRevisions } from '../core-engine/track-changes.mjs';
import { addCommentMarkToRange, stripCommentMark, commentAnchorText, commentIdsInRange, collectCommentIds } from '../core-engine/comments.mjs';
import { textRun as hfTextRun, pageNumberField, pageCountField, setRegion as setHeaderFooterRegion, clearRegion as clearHeaderFooterRegion } from '../core-engine/header-footer.mjs';
import { applyEditorAria, headingAriaForBlock, describeCaretContext, createLiveRegion } from '../core-engine/a11y.mjs';
import { bidiClass, baseDirection, resolveLevels, reorderVisual, hasRtl } from '../layout/bidi.mjs';
import { graphemeBoundaries, nextGraphemeBoundary, prevGraphemeBoundary, isGraphemeBoundary, graphemeCount } from '../layout/grapheme.mjs';
import {
    polygonIntervalsAtYGeometry,
    mergeGeometryIntervals,
    polygonBlockedIntervalsForGeometry,
    applyWrapSideToBlockedIntervals,
    blockedIntervalsForExclusionGeometry,
} from '../layout/blocked-intervals.mjs';
import {
    normalizeManagerInterval,
    mergeBlockedIntervalsForLayout,
    subtractBlockedIntervalsFromBody,
} from '../layout/exclusion-intervals.mjs';
import { createTextExclusionManager } from '../layout/text-exclusion-manager.mjs';
import {
    availableIntervalsCacheNumber,
    createAvailableIntervalsCacheKey,
    createAvailableIntervalsCacheStats,
    ensureAvailableIntervalsCacheStats,
    getAvailableIntervalsCacheStats,
    ensureAvailableIntervalsCache,
    resetAvailableIntervalsCache,
    getAvailableIntervals,
} from '../layout/available-intervals-cache.mjs';
import {
    normalizeWrapSnapshotInterval,
    collectBlockedIntervalsForWrapSnapshot,
} from '../layout/wrap-snapshot-intervals.mjs';
import { normalizeParagraphLayoutOptions } from '../layout/paragraph-layout-options.mjs';
import { createScopedLayoutMetadataDecorator } from '../layout/scoped-layout-metadata.mjs';
import {
    createAnchoredDrawingLayoutScope,
    createAnchoredDrawingScopeAggregator,
} from '../layout/anchored-drawing-scope.mjs';
import {
    normalizeLayoutSegmentStyle,
    decorationsFromMarks,
    applySegmentStyleToElement,
} from '../layout/segment-style.mjs';
import {
    paragraphRectFromLines,
    createInlineObjectLayoutFromSegmentFactory,
    firstScopeBlockId,
    findLayoutBlock,
    createLayoutObjectBlockFactory,
} from '../layout/paragraph-layout-tree.mjs';
import {
    flattenLayoutSegments,
    stableChecksum,
    createRenderSnapshot,
} from '../render/render-snapshot.mjs';
import {
    domRectToRect,
    rectsOverlap,
    rectsOverlapWithTolerance,
    hasRevisionRun,
    scopeIncludesBlock,
    markOverlayNonText,
} from '../render/render-helpers.mjs';
import { nowMs, elapsedWithSimulated } from '../core/timing.mjs';
import { createSelectionToRange } from '../core/selection-to-range.mjs';
import { createTypingChangeBufferFactory } from '../input/typing-change-buffer.mjs';
import {
    isInlineBreakNode,
    isCaretPlaceholderNode,
    domLogicalLength,
    domBoundaryLogicalOffset,
    createFindTextNodeFactory,
} from '../render/dom-text-mapping.mjs';
import { createCaretRectFromLayout } from '../layout/caret-rect.mjs';
import { previousWordBoundary, nextWordBoundary } from '../core/word-boundary.mjs';
import { createObjectTextExclusionRectForHitTest } from '../layout/object-exclusion-hit-rect.mjs';
import { createObjectHitCollectors } from '../layout/object-hit-collectors.mjs';
import {
    normalizePreviewIntervalForCompare,
    previewIntervalsSignature,
} from '../objects/preview-intervals-signature.mjs';
import {
    captureObjectPointerPreviewNodeState,
    restoreObjectPointerPreviewNodeState,
    createSerializeImageMoveTrack,
    createReadImageMoveTrackOriginalRect,
} from '../objects/image-move-track.mjs';
import {
    shouldReanchorImageObject,
    createComputeImageResizePreview,
} from '../objects/image-resize-preview.mjs';
import { createComputeImageMoveSnap } from '../objects/image-move-snap.mjs';
import { createReadTextPositionDomContext } from '../render/text-position-dom-context.mjs';
import { createNormalizeNearestTextPositionLineBox } from '../layout/nearest-text-position-line-box.mjs';
import { createBlockTextLineRectsFromDom } from '../render/block-text-line-rects.mjs';
import { findNearestBodyParagraphBlockIdFromPoint } from '../render/find-nearest-body-paragraph.mjs';
import { createGetObjectPointerTarget } from '../render/object-pointer-target.mjs';
import { createResolveImageObjectPointerModelTarget } from '../objects/image-pointer-model-target.mjs';
import { createImageTargetHelpers } from '../objects/image-target-helpers.mjs';
import {
    createFirstDrawingRunFromSourceBlock,
    createImagePayloadFromInsertImageCommand,
} from '../objects/insert-image-payload.mjs';
import { createNormalizeImageInsertPayload } from '../objects/image-insert-payload.mjs';
import {
    readImageInsertDimension,
    splitInlineListForDrawingInsert,
    createInlineDrawingLayoutForInsert,
    createDrawingRunFromImageInsert,
    insertDrawingRunAtTextOffset,
} from '../objects/image-insert.mjs';
import { createTableControllerFactory } from '../objects/table-controller.mjs';
import { createActiveImageTarget } from '../objects/active-image-target.mjs';
import { createSelectionTargetForInsertImageCommand } from '../objects/insert-image-selection-target.mjs';
import { normalizeSearchMarkersForRender } from '../render/search-markers.mjs';
import { overlapArea, createTextOverlapArea } from '../render/text-overlap.mjs';
import {
    createIsWysiwygLayoutElementVisible,
    getWysiwygRectRelativeTo,
    createUnionWysiwygRects,
} from '../render/wysiwyg-layout-geometry.mjs';
import {
    normalizeScopedBlockIdSet,
    createWysiwygParagraphProjectionSignature,
    createObjectEntryPaintLayer,
} from '../render/wysiwyg-render-helpers.mjs';
import {
    normalizeHeaderFooterScope,
    resolveHeaderFooterRegion,
} from '../render/header-footer-region.mjs';
import {
    isSafeInlineCssColor,
    isSafeInlineFontFamily,
    normalizeInlineFontSize,
    createRenderInlineTextHtml,
} from '../render/inline-style-sanitise.mjs';
import { createRenderFormattedInlineHtml } from '../render/inline-formatted-html.mjs';
import {
    createRenderCommentSpanHtml,
    createRenderRevisionSpanHtml,
    createRenderSearchSpanHtml,
} from '../render/marker-span-html.mjs';
import {
    inlineDrawingIsSelected,
    createRenderDrawingObjectTestMarkerHtml,
} from '../render/inline-drawing-helpers.mjs';
import {
    createRenderObjectSelectionDescriptionAttribute,
    createRenderObjectResizeHandleHtml,
    createRenderObjectFocusPolicyAttributes,
    renderObjectRotationHandleHtml,
} from '../render/object-aria-html.mjs';
import {
    createObjectFocusPolicy,
    createRenderSelectionOverlay,
    createRenderRevisionOverlay,
    createRenderCommentMarkers,
    restoreLogicalSelection,
    createApplyObjectFocusPolicyToElement,
} from '../render/atomic-overlays.mjs';
import { createAtomicRendererFactory } from '../render/atomic-renderer.mjs';
import {
    createRenderImageLayoutBubbleButton,
    createRenderImageLayoutBubbleHtml,
} from '../render/image-layout-bubble.mjs';
import {
    createExplicitObjectLayerRect,
    createObjectLayerRectFromObject,
    createRenderDrawingFigureStyle,
    estimateInlineDrawingCaptionReserveHeight,
    createRenderDrawingAnchorReservationStyle,
} from '../render/drawing-figure-style.mjs';
import { createRenderImageFigureClasses } from '../render/image-figure-classes.mjs';
import {
    createFirstTextPointInElement,
    createProjectedDomTextPointAtBlockOffset,
} from '../render/dom-text-point.mjs';
import { findLayoutObjectForRender } from '../render/find-layout-object-for-render.mjs';
import { createCollectWysiwygPageObjectEntries } from '../render/collect-wysiwyg-page-objects.mjs';
import { createWysiwygObjectRenderEntryFactory } from '../render/wysiwyg-object-render-entry.mjs';
import { createSyncWysiwygObjectLayerPositions } from '../render/sync-wysiwyg-object-layer-positions.mjs';
import {
    safeInlineColor,
    buildInlineStyleAttribute,
    escapeAttribute,
} from '../render/inline-style.mjs';
import {
    clamp01,
    describeRevisionColor,
    blendRevisionColor,
    revisionColorForAuthor,
    applyRevisionColorVars,
} from '../render/revision-color.mjs';
import { createRenderEngineTableHtml } from '../render/engine-table-html.mjs';
import { createMarkersForBlock } from '../render/markers-for-block.mjs';
import {
    buildObjectOverlayStyle,
    createRenderWysiwygObjectSelectionOverlayHtml,
    createRenderWysiwygObjectGuidesOverlayHtml,
} from '../render/object-overlay-html.mjs';
import {
    applyWysiwygObjectLayerRect,
    createResolveWysiwygObjectLayerRect,
} from '../render/wysiwyg-object-layer-rect.mjs';
import { createGetWysiwygObjectVisualRectRelativeTo } from '../render/wysiwyg-object-visual-rect.mjs';
import {
    groupProjectedWysiwygSegmentsByLine,
    createProjectedWysiwygLineRenderer,
} from '../render/projected-wysiwyg-line.mjs';
import { createSplitProjectedWysiwygSegmentsForReflow } from '../render/projected-segment-split.mjs';
import { createResolveProjectedWysiwygLineIntervals } from '../render/projected-line-intervals.mjs';
import {
    restoreWysiwygProjectedParagraph,
    createShouldProjectWysiwygParagraph,
} from '../render/projected-paragraph-state.mjs';
import { createReflowProjectedWysiwygSegments } from '../render/projected-segment-reflow.mjs';
import { createProjectWysiwygParagraphAroundExclusions } from '../render/project-paragraph-around-exclusions.mjs';
import { createCollectWysiwygDomTextExclusions } from '../render/wysiwyg-dom-text-exclusions.mjs';
import {
    readCommentId as readMarkerCommentId,
    readCommentStatus as readMarkerCommentStatus,
    commentById as markerCommentById,
    createRevisionReaders,
} from '../core/marker-readers.mjs';
import { createAnchorRanges } from '../core/anchor-ranges.mjs';
import { createInlineMarkerRanges } from '../core/inline-marker-ranges.mjs';
import { createRuntimeMarkerBuilders } from '../core/runtime-markers.mjs';
import { blockTypeForTest, paragraphRunsForTest } from '../core/test-projections.mjs';
import { createMarkerStoreFactory } from '../core/marker-store.mjs';
import {
    normalizeReviewDisplayModeClass,
    applyReviewDisplayModeClass,
} from '../render/review-display-mode.mjs';
import {
    liveTextNodeCanUseFastPatch,
    createTextBlockHasOnlyPlainTextRuns,
} from '../render/live-paragraph-fast-patch.mjs';
import { createSetLiveParagraphText } from '../render/live-paragraph-text.mjs';
import { createTargetIsBehindTextOverlaySurface } from '../render/behind-text-surface.mjs';
import { createNativeCaretRangeFromPoint } from '../render/native-caret-range.mjs';
import { createEditableSurfacePredicates } from '../render/editable-surface.mjs';
import { createInitialDirtyState, getOperationId } from '../history/dirty-state.mjs';
import { createTextEditHandlers } from '../history/handlers-text-edit.mjs';
import { createImageHandlers } from '../history/handlers-image.mjs';
import { createModelProjections } from '../render/model-projections.mjs';
import { createOverlayRenderers } from '../render/overlay-renderers.mjs';
import {
    createEditorWidgetFactory,
    createImageInspectorStateFactory,
} from '../objects/editor-widget.mjs';
import { createImagePreviewControllerFactory } from '../objects/image-preview-controller.mjs';
import {
    createIndexBuilder,
    createBlockIndexContext,
    findBlockByIndex,
} from '../core/indexes.mjs';
import { createTransactionsModule } from '../history/transactions.mjs';
import {
    WD_READY,
    WD_RECOVERING,
    WD_RECOVERED,
    WD_FAILED,
    WD_DEFAULT_MAX_ATTEMPTS,
    WD_DEFAULT_BACKOFF_MS,
    WD_EVENT_HISTORY_LIMIT,
    computeWatchdogBackoff,
    cloneWatchdogJson,
    parseWatchdogJson,
    unwrapWatchdogDocumentSnapshot,
    wrapWatchdogDocumentSnapshot,
    safeCall,
    watchdogNow,
    buildWatchdogEventDetail,
    recordWatchdogEvent,
    createWatchdogContext,
    isWatchdogProcessing,
    lastEventWas,
} from './watchdog-helpers.mjs';
import { InstanceManager, defaultInstanceManager } from './instance-manager.mjs';
import {
    readObjectWrapSide,
    normalizeRelativePositionName,
    relativePositionToValue,
    verticalAlignmentToValue,
    normalizePositionSpec,
    normalizeLayoutKindName,
} from '../objects/layout-helpers.mjs';
import {
    normalizeCommandColorValue,
    commandMark,
    isClearValueCommand,
} from '../input/command-marks.mjs';
import {
    commandSource,
    inlineCommandTypes,
    paragraphCommandTypes,
    markMatchesCommand,
} from '../input/command-classifiers.mjs';
import { normalizeCommandId } from '../input/command-id.mjs';
import { pendingMarkForCommand } from '../input/pending-marks.mjs';
import { findInheritedTextColor } from '../core/inherited-style.mjs';
import {
    normalizeRevisionType,
    normalizeRevisionStatus,
    normalizeRevisionRange,
} from '../core/revision-normalize.mjs';
import { escapeHtml } from '../render/escape.mjs';
import { resolveInlineRunDisplayText, textFromRunsForRender } from '../render/run-text.mjs';
import {
    findRunAtOffset,
    inlineAtOffset,
    resolveTextOffsetToInlineIndex,
} from '../core/run-finders.mjs';
import { runsForRange } from '../core/runs-for-range.mjs';
import { toBlazorFormattingState } from '../core/blazor-formatting-state.mjs';
import { createFormattingStateModule } from '../input/formatting-state.mjs';
import { normalizePasteText } from '../clipboard/paste-text.mjs';
import {
    createLogicalPosition,
    createLogicalRange,
    normalizeSelectionModeValue,
    normalizeTextSelectionPayload,
    normalizeObjectSelectionPayload,
    isObjectSelectionSnapshot,
    createSelectionSnapshot,
} from '../core/selection-snapshot.mjs';
import {
    shouldCoalesceTyping,
    coalesceTypingOperation,
    defaultCoalesceWindowMs,
} from '../input/typing-coalescer.mjs';
import {
    disposedResult,
    missingResult,
    errorResult,
} from './instance-results.mjs';
import { normalizeHorizontalPositionName, horizontalPositionToValue } from '../objects/horizontal-position.mjs';
import { wrapModeToValue, wrapModeToCssName, wrapModeCreatesTextExclusion } from '../objects/wrap-mode-value.mjs';
import {
    rectFromGeometry,
    rectRightGeometry,
    rectBottomGeometry,
    rectIntersectsGeometry,
    rectOverlapsHorizontallyGeometry,
    intersectGeometryRect,
    geometryBoundsOfPoints,
    normalizeWrapContourPointsForGeometry,
    normalizeWrapContourPoints,
    readObjectDistance,
    createObjectFootprintRect,
    createObjectWrapRect,
    projectWrapContourPointsForGeometry,
} from '../objects/geometry.mjs';
import { normalizeImageObject, imageObjectToLayout } from '../objects/image-object.mjs';
import { syncImageLayoutCase, applyImageWrapModeToLayout } from '../objects/sync-image-layout.mjs';
import { createDrawingRunsModule } from '../objects/drawing-runs.mjs';
import {
    BeforeInputCommands,
    normalizeBeforeInput,
    createBeforeInputNormalizer,
} from '../input/before-input.mjs';
import {
    operationTouchesRevisions,
    operationMayChangeRevisions,
    isFormattingVisualOperation,
} from '../history/operation-classifiers.mjs';
import {
    createApplyOperationDispatcher,
    ApplyOperationHandlerNames,
} from '../history/apply-operation-dispatcher.mjs';
import { createImportOrchestrator } from '../core/import-orchestrator.mjs';
import { detectAutocompleteTriggerText } from '../input/autocomplete-trigger.mjs';
import { compactCommandName } from '../input/command-name.mjs';
import { computeFloatingPosition } from '../render/floating-position.mjs';
import { firstTextBlock, firstModelSelection } from '../core/first-block.mjs';
import {
    operationAffectedBlockIds,
    transactionAffectedBlockIds,
} from '../history/operation-affected.mjs';
import { createSimpleHandlers } from '../history/handlers-simple.mjs';
import { createDiffer } from '../history/differ.mjs';
import { createOperationValidator } from '../history/validate-operation.mjs';
import { createReplaceModelContents } from '../core/replace-model.mjs';
import {
    findRegionInfoForBlock,
    operationRegionInfo,
    nextSelectionForOperation,
} from '../core/region-info.mjs';
import { commentIdsAtInsertionOffset } from '../core/comment-resolver.mjs';
import { styleHasValues, resolveTypingStyleAtInsertion } from '../core/typing-style.mjs';
import { insertTextRun } from '../core/insert-text-run.mjs';
import {
    setParagraphText,
    cloneRunSlice,
    deleteTextRange,
    splitParagraphRuns,
    splitRunsForRange,
} from '../core/run-mutators.mjs';
import { splitParagraphRunsAtOffset } from '../core/split-paragraph-runs.mjs';
import { createEmptyTableCellFactory } from '../core/table-cell-factory.mjs';
import { createFindBlock } from '../core/find-block.mjs';
import { createBuildIndexes } from '../core/build-indexes.mjs';
import { createTextHandlers } from '../history/handlers-text.mjs';
import { createSplitHandler } from '../history/handlers-split.mjs';
import { createTrackedHandlers } from '../history/handlers-tracked.mjs';
import { createParagraphAttributeHandler } from '../history/handlers-paragraph-attribute.mjs';
import { createRestoreSnapshotHandler } from '../history/handlers-restore-snapshot.mjs';
import { createRevisionDecisionHandler } from '../history/handlers-revision-decision.mjs';
import { createCommandDispatcherFactory } from '../history/command-dispatcher.mjs';
import { createRevisionEngineFactory } from '../history/revision-engine.mjs';
import { createHistoryControllerFactory } from '../history/history-controller.mjs';
import { createTableHandlers } from '../history/handlers-table.mjs';
import {
    revisionById,
    readRevisionStatus,
    readRevisionTypeName,
    readRevisionMarkerType,
    setRevisionPayloadText,
    createTrackedRevisionPayload,
    createInsertionRevisionPayload,
    createStructureRevisionPayload,
    createDeletionRevisionPayloadFactory,
    createLiveInsertionRevisionPayloadFactory,
    transformRunsInRange,
    createRevisionListHelpers,
    createSetRevisionForRange,
} from '../history/revision-helpers.mjs';
import {
    resolveTrackChangesState,
    isTrackChangesEnabled,
    resolveRevisionUserId,
    revisionPayloadText,
    stableRevisionStringify,
} from '../history/track-changes.mjs';
import {
    revisionAuthorMergeKey,
    revisionRunFormattingMergeKey,
    canMergeAdjacentRevisionRuns,
    replaceRevisionIdOnRun,
} from '../history/revision-merge.mjs';
import { createRevisionList } from '../history/revision-list.mjs';
import { createRevisionRunMutators } from '../history/revision-run-mutators.mjs';
import { normalizeRevision } from '../history/normalize-revision.mjs';
import { createRevisionGroupNormaliser } from '../history/revision-groups.mjs';
import { revisionDecorativeStyle } from '../history/revision-decorative.mjs';
import {
    createTextMeasurementService,
    normalizeMeasureStyle,
    computeMeasureCacheKey,
    measureTextRunPure,
} from '../layout/text-measurement.mjs';
import {
    createFontMetricsService,
    normalizeFontMetricStyle,
    fontStringFromStyle,
    syntheticRunMetrics,
    computeFontMetricKey,
} from '../layout/font-metrics.mjs';
import { createLineBreakerModule } from '../layout/line-breaker.mjs';
import {
    normalizeLineBreakerOptions,
    normalizeLineRanges,
    resolveLineRangesForBreaker,
    isInvalidInterval,
    lineRangesAreInvalid,
    coalesceNonBreakingTokens,
    splitTokenIntoFittingPieces,
    applyJustifyMetadata,
} from '../layout/line-breaker-helpers.mjs';
import {
    createLineDraft,
    materializeLineDraft,
} from '../layout/line-draft.mjs';
import {
    isCjkCharacter,
    isTokenDelimiter,
    cssLengthToPixels,
    mergeTextStyle,
    tokenizeText,
    runForOffset,
    createParagraphTokenizer,
} from '../layout/paragraph-tokenizer.mjs';
import { normalizeParagraphAlignment } from '../layout/paragraph-alignment.mjs';
import { createLineBreakerFallback } from '../layout/line-breaker-fallback.mjs';
import {
    normalizeSelectionTokenRegion,
    readSelectionTokenValue,
    parseSelectionTokenData,
    readSelectionTokenData,
} from '../core/selection-token.mjs';
import { createSelectionTextRange } from '../core/selection-range.mjs';
import { createObjectSelectionRestorer } from '../core/object-selection-restore.mjs';
import { createRangeFormatting } from '../core/range-formatting.mjs';
import {
    IMAGE_RESIZE_MIN_WIDTH,
    IMAGE_RESIZE_MIN_HEIGHT,
    normalizeImageResizeHandleName,
    imageResizeHandleIndex,
    computeImageResizeFixedPoint,
    createImageResizeBounds,
    clampImageResizeSize,
} from '../objects/image-resize.mjs';
import { formatNonPrintingText } from '../render/non-printing.mjs';
import { formatA11yLabel } from '../accessibility/labels.mjs';
import {
    objectAccessibilityIdFragment,
    activeObjectStatusId,
    appendAriaDescribedByToken,
    getImageObjectAccessibleLabel,
    objectResizeHandleDirectionLabel,
    objectResizeHandleAriaLabel,
} from '../accessibility/object-aria.mjs';
import { findActiveHeadingBlockIdFromRects } from '../render/heading-finder.mjs';
import { findLimitForBlock } from '../core/limit-finder.mjs';
import { rectFromAny, rectContains } from '../render/rect-helpers.mjs';
import { createPerformanceMetricsHarness } from './performance-metrics.mjs';
import { createBoundaryPatchModule } from './boundary-patch.mjs';
import { createWatchdogInstaller } from './watchdog.mjs';
import { createPerformanceProbe } from './performance-probe.mjs';
import {
    readCommandName,
    readPayload,
    readSelectionToken,
    normalizeResult,
    createCommandExecutor,
} from './command-execute.mjs';
import {
    schemaAllowsBlockForTest,
    normalizeInsertionBlocksForSchema,
} from '../core/schema-validation.mjs';
import { applyLayoutTextEditModel } from '../input/layout-text-edit-model.mjs';
import { formattingScalarValue } from '../core/formatting-scalar.mjs';
import { median, percentileNearestRank } from '../core/stats.mjs';
import {
    createDefaultLatencyBudgets,
    createLatencyHistogramState,
    ensureLatencyHistogramState,
    latencyBudgetForName,
    createLatencyHistogramSummary,
} from './latency-histograms.mjs';
import { createStrictPerformanceStats } from './strict-performance-stats.mjs';
import {
    PERFORMANCE_HISTOGRAM_LIMIT,
    PARTIAL_RENDER_SCOPE_SAMPLES_LIMIT,
    recordLatencyHistogram,
    recordPartialRenderScope,
} from './strict-performance-recorders.mjs';
import {
    strictPerformanceNow,
    normalizePerformanceRegion,
    activeRegionForSelection,
    activeRegionForInstance,
    ensureStrictPerformanceStats,
} from './strict-performance-helpers.mjs';
import {
    typingHotPathWindowMs,
    isTypingHotPath,
} from './typing-hot-path.mjs';
import {
    DIAGNOSTICS_TIMELINE_LIMIT,
    DIAGNOSTICS_ERROR_LIMIT,
    DIAGNOSTICS_WATCHDOG_FAILURE_LIMIT,
    createDiagnosticsState,
    ensureDiagnostics,
    recordTimeline,
    recordDiagnosticError,
    recordWatchdogFailure,
} from './diagnostics.mjs';
import {
    recordLayoutMetric,
    recordRenderMetric,
} from './layout-render-metrics.mjs';
import { recordOperationPerformance } from './operation-performance.mjs';
import {
    isElementNode,
    getFocusRegionFromElement,
    getFocusTargetDetails,
} from '../render/focus-region.mjs';
import { cssEscape } from '../render/css-escape.mjs';
import {
    findLiveTextBlockElement,
    findLiveTextBlockElements,
    findLiveTextBlockElementForContext,
    liveBlockElementMatchesSelection,
    liveBlockContextFromElement,
} from '../render/live-block-finder.mjs';
import {
    selectionBelongsToEditor,
    selectionTargetsTextSurface,
} from '../render/dom-selection.mjs';
import { selectedDomRect } from '../render/selection-rect.mjs';
import { pageIndexFromPoint } from '../render/page-finder.mjs';
import {
    floatingViewportBoundsAvoidingChrome,
    floatingViewportWidthAvoidingSidePanel,
} from '../render/floating-viewport.mjs';
import { createMiniToolbarPredicate } from '../render/mini-toolbar-predicate.mjs';
import {
    finiteNumber,
    caretOffsetFromInterval,
    nearestOffsetWithinLine,
} from '../layout/caret-math.mjs';
import {
    testTextMeasureStyle,
    getTextRunMeasureCacheKey,
    createTestTextMeasurer,
} from '../layout/test-text-measurer.mjs';
import { hitRectFromAny, hitRectContains } from '../layout/hit-rect.mjs';
import { normalizeCaretInterval } from '../layout/caret-interval.mjs';
import {
    collectLayoutLineIntervals,
    findCaretIntervalHit,
} from '../layout/caret-interval-collector.mjs';
import { inferCaretIntervalAffinity } from '../layout/caret-affinity.mjs';
import {
    findLayoutBlockById,
    findReferenceLineForOffset,
} from '../layout/layout-block-finder.mjs';
import { scoreNearestTextPositionLineBox } from '../layout/line-box-scorer.mjs';
import {
    drawingLayerForWrapMode,
    hitTestLayerPriority,
} from '../objects/layer-priority.mjs';
import { objectHitPriority } from '../objects/hit-priority.mjs';
import { createDrawingObjectSnapshotFactory } from '../objects/drawing-snapshot.mjs';
import { createDrawingIndexHelpers } from '../objects/drawing-index.mjs';
import { createFindDrawingRunByAsset } from '../objects/find-drawing-by-asset.mjs';
import { createAffectedParagraphsAroundObject } from '../objects/affected-paragraphs.mjs';
import {
    normalizeDropRegionName,
    anchorRegionForNearestTextPosition,
    imageAnchorScopeKey,
    imageDropScopeKey,
    canDropImageInNearestTextScope,
} from '../objects/drop-region.mjs';
import {
    testWrapMode,
    testWrapSide,
    testHorizontalPosition,
} from '../objects/wrap-mode-test.mjs';
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
    timing: {
        nowMs,
        elapsedWithSimulated,
    },
    createSelectionToRange,
    markerReaders: {
        readCommentId: readMarkerCommentId,
        readCommentStatus: readMarkerCommentStatus,
        commentById: markerCommentById,
        createRevisionReaders,
    },
    createAnchorRanges,
    createInlineMarkerRanges,
    createRuntimeMarkerBuilders,
    testProjections: {
        blockTypeForTest,
        paragraphRunsForTest,
    },
    createMarkerStoreFactory,
    wordBoundary: {
        previousWordBoundary,
        nextWordBoundary,
    },
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
    blockImport: {
        importParagraphContent,
        importImageObject,
        importTable,
        importBlock,
        importRegion,
    },
    blockExport: {
        exportBlock,
        readCommentId,
        exportComment,
        exportRevision,
    },
    documentExport: {
        exportToCSharpJson,
        exportRevisionsToCSharpJson,
        exportCommentsToCSharpJson,
    },
    validate: {
        validateModel,
    },
    fingerprint: {
        stableJsonString,
        hashStableString,
        createDocumentFingerprint,
        createSelectionDocumentFingerprint,
    },
    valueReaders: {
        readOptionalBoolean,
    },
    indexes: {
        createIndexBuilder,
        createBlockIndexContext,
        findBlockByIndex,
    },
    revisionNormalize: {
        normalizeRevisionType,
        normalizeRevisionStatus,
        normalizeRevisionRange,
    },
    runFinders: {
        findRunAtOffset,
        inlineAtOffset,
        resolveTextOffsetToInlineIndex,
    },
    runsForRange,
    toBlazorFormattingState,
    selectionSnapshot: {
        createLogicalPosition,
        createLogicalRange,
        normalizeSelectionModeValue,
        normalizeTextSelectionPayload,
        normalizeObjectSelectionPayload,
        isObjectSelectionSnapshot,
        createSelectionSnapshot,
    },
    firstBlock: {
        firstTextBlock,
        firstModelSelection,
    },
    createImportOrchestrator,
    createReplaceModelContents,
    regionInfo: {
        findRegionInfoForBlock,
        operationRegionInfo,
        nextSelectionForOperation,
    },
    commentIdsAtInsertionOffset,
    typingStyle: {
        styleHasValues,
        resolveTypingStyleAtInsertion,
    },
    insertTextRun,
    runMutators: {
        setParagraphText,
        cloneRunSlice,
        deleteTextRange,
        splitParagraphRuns,
        splitRunsForRange,
        splitParagraphRunsAtOffset,
    },
    createEmptyTableCellFactory,
    createFindBlock,
    createBuildIndexes,
    selectionToken: {
        normalizeSelectionTokenRegion,
        readSelectionTokenValue,
        parseSelectionTokenData,
        readSelectionTokenData,
    },
    createSelectionTextRange,
    createObjectSelectionRestorer,
    createRangeFormatting,
    findLimitForBlock,
    findInheritedTextColor,
    schemaValidation: {
        schemaAllowsBlockForTest,
        normalizeInsertionBlocksForSchema,
    },
    formattingScalarValue,
    stats: { median, percentileNearestRank },
    canonical: {
        fromCanonicalDocument,
        toCanonicalDocument,
        normalizeCanonicalDocument,
        normalizeCanonicalSnapshot,
        roundTripCanonicalDocument,
        diffCanonicalDocuments,
        normalizeInline,
        normalizeInlines,
        normalizeBlock,
        normalizeBlocks,
        normalizeBlockContent,
        normalizeParagraphContent,
        normalizeTableContent,
        normalizeImageContent,
        normalizeHeaderFooter,
    },
    createObjectSelectionSnapshotFactory,
    createSelectionNormalizers,
    createSelectionPostFixerFactory,
};

export const clipboard = {
    normalizePasteText,
};

export const accessibility = {
    createAccessibilityAnnouncer,
    announcementDebounceMs,
    formatA11yLabel,
    objectAria: {
        objectAccessibilityIdFragment,
        activeObjectStatusId,
        appendAriaDescribedByToken,
        getImageObjectAccessibleLabel,
        objectResizeHandleDirectionLabel,
        objectResizeHandleAriaLabel,
    },
};

export const runtime = {
    InstanceManager,
    defaultInstanceManager,
    createPerformanceMetricsHarness,
    createBoundaryPatchModule,
    createWatchdogInstaller,
    createPerformanceProbe,
    commandExecute: {
        readCommandName,
        readPayload,
        readSelectionToken,
        normalizeResult,
        createCommandExecutor,
    },
    latency: {
        createDefaultLatencyBudgets,
        createLatencyHistogramState,
        ensureLatencyHistogramState,
        latencyBudgetForName,
        createLatencyHistogramSummary,
    },
    createStrictPerformanceStats,
    perfHelpers: {
        strictPerformanceNow,
        normalizePerformanceRegion,
        activeRegionForSelection,
        activeRegionForInstance,
        ensureStrictPerformanceStats,
    },
    typingHotPath: {
        typingHotPathWindowMs,
        isTypingHotPath,
    },
    diagnostics: {
        DIAGNOSTICS_TIMELINE_LIMIT,
        DIAGNOSTICS_ERROR_LIMIT,
        DIAGNOSTICS_WATCHDOG_FAILURE_LIMIT,
        createDiagnosticsState,
        ensureDiagnostics,
        recordTimeline,
        recordDiagnosticError,
        recordWatchdogFailure,
    },
    metrics: {
        recordLayoutMetric,
        recordRenderMetric,
        recordOperationPerformance,
    },
    recorders: {
        PERFORMANCE_HISTOGRAM_LIMIT,
        PARTIAL_RENDER_SCOPE_SAMPLES_LIMIT,
        recordLatencyHistogram,
        recordPartialRenderScope,
    },
    results: {
        disposedResult,
        missingResult,
        errorResult,
    },
    watchdog: {
        WD_READY,
        WD_RECOVERING,
        WD_RECOVERED,
        WD_FAILED,
        WD_DEFAULT_MAX_ATTEMPTS,
        WD_DEFAULT_BACKOFF_MS,
        WD_EVENT_HISTORY_LIMIT,
        computeWatchdogBackoff,
        cloneWatchdogJson,
        parseWatchdogJson,
        unwrapWatchdogDocumentSnapshot,
        wrapWatchdogDocumentSnapshot,
        safeCall,
        watchdogNow,
        buildWatchdogEventDetail,
        recordWatchdogEvent,
        createWatchdogContext,
        isWatchdogProcessing,
        lastEventWas,
    },
};

export const history = {
    OperationTypes,
    TransactionTypes,
    isTypingLikeTransactionType,
    createCommandDispatcherFactory,
    createRevisionEngineFactory,
    createHistoryControllerFactory,
    createInitialDirtyState,
    getOperationId,
    createTextEditHandlers,
    createImageHandlers,
    createIdCounters,
    createOperationsModule,
    isSelectionOnlyOperation,
    operationsAffectDocument,
    transactionAffectsDocument,
    supportsOperationHistory,
    supportsLightweightTransactionSnapshots,
    createTransactionsModule,
    operationTouchesRevisions,
    operationMayChangeRevisions,
    isFormattingVisualOperation,
    createApplyOperationDispatcher,
    ApplyOperationHandlerNames,
    operationAffectedBlockIds,
    transactionAffectedBlockIds,
    createSimpleHandlers,
    createDiffer,
    createOperationValidator,
    createTextHandlers,
    createSplitHandler,
    createTrackedHandlers,
    createParagraphAttributeHandler,
    createRestoreSnapshotHandler,
    createRevisionDecisionHandler,
    createTableHandlers,
    revisionById,
    readRevisionStatus,
    readRevisionTypeName,
    readRevisionMarkerType,
    setRevisionPayloadText,
    createTrackedRevisionPayload,
    createInsertionRevisionPayload,
    createStructureRevisionPayload,
    createDeletionRevisionPayloadFactory,
    createLiveInsertionRevisionPayloadFactory,
    transformRunsInRange,
    createRevisionListHelpers,
    createSetRevisionForRange,
    resolveTrackChangesState,
    isTrackChangesEnabled,
    resolveRevisionUserId,
    revisionPayloadText,
    stableRevisionStringify,
    revisionAuthorMergeKey,
    revisionRunFormattingMergeKey,
    canMergeAdjacentRevisionRuns,
    replaceRevisionIdOnRun,
    createRevisionList,
    createRevisionRunMutators,
    normalizeRevision,
    createRevisionGroupNormaliser,
    revisionDecorativeStyle,
};

export const layout = {
    LayoutScopeKinds,
    createLayoutScope,
    inferLayoutScopeFromOperation,
    createParagraphLayoutEngineFactory,
    paragraphRuns: { flattenParagraphRuns, runForOffset, mergeTextStyle, cssLengthToPixels },
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
    textExclusion: {
        normalizeTextExclusionPageIndex,
        createTextExclusionScopeKey,
        readTextExclusionScope,
        createTextExclusionScopeDescriptor,
        textExclusionMatchesScope,
        createTextExclusion,
    },
    blockedIntervals: {
        polygonIntervalsAtYGeometry,
        mergeGeometryIntervals,
        polygonBlockedIntervalsForGeometry,
        applyWrapSideToBlockedIntervals,
        blockedIntervalsForExclusionGeometry,
    },
    exclusionIntervals: {
        normalizeManagerInterval,
        mergeBlockedIntervalsForLayout,
        subtractBlockedIntervalsFromBody,
    },
    createTextExclusionManager,
    availableIntervalsCache: {
        availableIntervalsCacheNumber,
        createAvailableIntervalsCacheKey,
        createAvailableIntervalsCacheStats,
        ensureAvailableIntervalsCacheStats,
        getAvailableIntervalsCacheStats,
        ensureAvailableIntervalsCache,
        resetAvailableIntervalsCache,
        getAvailableIntervals,
    },
    wrapSnapshotIntervals: {
        normalizeWrapSnapshotInterval,
        collectBlockedIntervalsForWrapSnapshot,
    },
    normalizeParagraphLayoutOptions,
    createScopedLayoutMetadataDecorator,
    createAnchoredDrawingLayoutScope,
    createAnchoredDrawingScopeAggregator,
    segmentStyle: {
        normalizeLayoutSegmentStyle,
        decorationsFromMarks,
        applySegmentStyleToElement,
    },
    paragraphLayoutTree: {
        paragraphRectFromLines,
        createInlineObjectLayoutFromSegmentFactory,
        firstScopeBlockId,
        findLayoutBlock,
        createLayoutObjectBlockFactory,
    },
    textMeasurement: {
        createTextMeasurementService,
        normalizeMeasureStyle,
        computeMeasureCacheKey,
        measureTextRunPure,
    },
    fontMetrics: {
        createFontMetricsService,
        normalizeFontMetricStyle,
        fontStringFromStyle,
        syntheticRunMetrics,
        computeFontMetricKey,
    },
    bidi: {
        bidiClass,
        baseDirection,
        resolveLevels,
        reorderVisual,
        hasRtl,
    },
    grapheme: {
        graphemeBoundaries,
        nextGraphemeBoundary,
        prevGraphemeBoundary,
        isGraphemeBoundary,
        graphemeCount,
    },
    createLineBreakerModule,
    caretMath: {
        finiteNumber,
        caretOffsetFromInterval,
        nearestOffsetWithinLine,
    },
    hitRect: {
        hitRectFromAny,
        hitRectContains,
    },
    testTextMeasurer: {
        testTextMeasureStyle,
        getTextRunMeasureCacheKey,
        createTestTextMeasurer,
    },
    inferCaretIntervalAffinity,
    normalizeCaretInterval,
    collectLayoutLineIntervals,
    findCaretIntervalHit,
    findLayoutBlockById,
    findReferenceLineForOffset,
    scoreNearestTextPositionLineBox,
    lineBreakerHelpers: {
        normalizeLineBreakerOptions,
        normalizeLineRanges,
        resolveLineRangesForBreaker,
        isInvalidInterval,
        lineRangesAreInvalid,
        coalesceNonBreakingTokens,
        splitTokenIntoFittingPieces,
        applyJustifyMetadata,
        createLineDraft,
        materializeLineDraft,
    },
    paragraphTokenizer: {
        isCjkCharacter,
        isTokenDelimiter,
        cssLengthToPixels,
        mergeTextStyle,
        tokenizeText,
        runForOffset,
        createParagraphTokenizer,
    },
    normalizeParagraphAlignment,
    createLineBreakerFallback,
    createCaretRectFromLayout,
    createObjectTextExclusionRectForHitTest,
    createObjectHitCollectors,
    createNormalizeNearestTextPositionLineBox,
};

export const objects = {
    WrapModeNames,
    WrapSideNames,
    normalizeWrapModeName,
    normalizeWrapSideName,
    wrapSideToValue,
    normalizeDrawingKindName,
    exportDrawingKind,
    normalizeAnchorRegionName,
    anchorRegionToValue,
    readObjectLayoutInCell,
    readObjectWrapSide,
    normalizeRelativePositionName,
    relativePositionToValue,
    verticalAlignmentToValue,
    normalizePositionSpec,
    normalizeLayoutKindName,
    normalizeHorizontalPositionName,
    horizontalPositionToValue,
    wrapModeToValue,
    wrapModeToCssName,
    wrapModeCreatesTextExclusion,
    normalizeImageObject,
    imageObjectToLayout,
    syncImageLayoutCase,
    applyImageWrapModeToLayout,
    createDrawingRunsModule,
    geometry: {
        rectFromGeometry,
        rectRightGeometry,
        rectBottomGeometry,
        rectIntersectsGeometry,
        rectOverlapsHorizontallyGeometry,
        intersectGeometryRect,
        geometryBoundsOfPoints,
        normalizeWrapContourPointsForGeometry,
        normalizeWrapContourPoints,
        readObjectDistance,
        createObjectFootprintRect,
        createObjectWrapRect,
        projectWrapContourPointsForGeometry,
    },
    layerPriority: {
        drawingLayerForWrapMode,
        hitTestLayerPriority,
    },
    objectHitPriority,
    previewIntervalsSignature: {
        normalizePreviewIntervalForCompare,
        previewIntervalsSignature,
    },
    imageMoveTrack: {
        captureObjectPointerPreviewNodeState,
        restoreObjectPointerPreviewNodeState,
        createSerializeImageMoveTrack,
        createReadImageMoveTrackOriginalRect,
    },
    imageResizePreview: {
        shouldReanchorImageObject,
        createComputeImageResizePreview,
    },
    createComputeImageMoveSnap,
    createResolveImageObjectPointerModelTarget,
    createImageTargetHelpers,
    insertImagePayload: {
        createFirstDrawingRunFromSourceBlock,
        createImagePayloadFromInsertImageCommand,
    },
    createNormalizeImageInsertPayload,
    imageInsert: {
        readImageInsertDimension,
        splitInlineListForDrawingInsert,
        createInlineDrawingLayoutForInsert,
        createDrawingRunFromImageInsert,
        insertDrawingRunAtTextOffset,
    },
    createTableControllerFactory,
    createActiveImageTarget,
    createSelectionTargetForInsertImageCommand,
    createDrawingObjectSnapshotFactory,
    createDrawingIndexHelpers,
    createFindDrawingRunByAsset,
    createAffectedParagraphsAroundObject,
    createAnchoredDrawingRunCollector,
    anchoredPosition: {
        resolvePositionReferenceRect,
        resolveAlignedHorizontal,
        resolveAlignedVertical,
        resolveAnchoredDrawingRect,
    },
    overlap: {
        intervalEndGeometry,
        subtractGeometryInterval,
        objectOverlapCollisionRect,
        resolveObjectOverlapGeometry,
    },
    createAnchoredDrawingResolvers,
    createEditorWidgetFactory,
    createImageInspectorStateFactory,
    createImagePreviewControllerFactory,
    dropRegion: {
        normalizeDropRegionName,
        anchorRegionForNearestTextPosition,
        imageAnchorScopeKey,
        imageDropScopeKey,
        canDropImageInNearestTextScope,
    },
    wrapModeTest: {
        testWrapMode,
        testWrapSide,
        testHorizontalPosition,
    },
    imageResize: {
        IMAGE_RESIZE_MIN_WIDTH,
        IMAGE_RESIZE_MIN_HEIGHT,
        normalizeImageResizeHandleName,
        imageResizeHandleIndex,
        computeImageResizeFixedPoint,
        createImageResizeBounds,
        clampImageResizeSize,
    },
};

export const input = {
    normalizeCommandColorValue,
    commandMark,
    isClearValueCommand,
    commandSource,
    inlineCommandTypes,
    paragraphCommandTypes,
    markMatchesCommand,
    normalizeCommandId,
    pendingMarkForCommand,
    shouldCoalesceTyping,
    coalesceTypingOperation,
    defaultCoalesceWindowMs,
    BeforeInputCommands,
    normalizeBeforeInput,
    createBeforeInputNormalizer,
    detectAutocompleteTriggerText,
    compactCommandName,
    applyLayoutTextEditModel,
    createTypingChangeBufferFactory,
    createFormattingStateModule,
};

export const render = {
    escapeHtml,
    resolveInlineRunDisplayText,
    textFromRunsForRender,
    computeFloatingPosition,
    formatNonPrintingText,
    findActiveHeadingBlockIdFromRects,
    rectFromAny,
    rectContains,
    focusRegion: {
        isElementNode,
        getFocusRegionFromElement,
        getFocusTargetDetails,
    },
    cssEscape,
    liveBlockFinder: {
        findLiveTextBlockElement,
        findLiveTextBlockElements,
        findLiveTextBlockElementForContext,
        liveBlockElementMatchesSelection,
        liveBlockContextFromElement,
    },
    domSelection: {
        selectionBelongsToEditor,
        selectionTargetsTextSurface,
    },
    selectedDomRect,
    pageIndexFromPoint,
    floatingViewport: {
        floatingViewportBoundsAvoidingChrome,
        floatingViewportWidthAvoidingSidePanel,
    },
    createMiniToolbarPredicate,
    snapshot: {
        flattenLayoutSegments,
        stableChecksum,
        createRenderSnapshot,
    },
    helpers: {
        domRectToRect,
        rectsOverlap,
        rectsOverlapWithTolerance,
        hasRevisionRun,
        scopeIncludesBlock,
        markOverlayNonText,
    },
    createModelProjections,
    createOverlayRenderers,
    liveParagraphFastPatch: {
        liveTextNodeCanUseFastPatch,
        createTextBlockHasOnlyPlainTextRuns,
    },
    createSetLiveParagraphText,
    createTargetIsBehindTextOverlaySurface,
    createNativeCaretRangeFromPoint,
    createEditableSurfacePredicates,
    createReadTextPositionDomContext,
    createBlockTextLineRectsFromDom,
    findNearestBodyParagraphBlockIdFromPoint,
    createGetObjectPointerTarget,
    normalizeSearchMarkersForRender,
    overlapArea,
    createTextOverlapArea,
    wysiwygLayoutGeometry: {
        createIsWysiwygLayoutElementVisible,
        getWysiwygRectRelativeTo,
        createUnionWysiwygRects,
    },
    wysiwygRenderHelpers: {
        normalizeScopedBlockIdSet,
        createWysiwygParagraphProjectionSignature,
        createObjectEntryPaintLayer,
    },
    headerFooter: {
        normalizeHeaderFooterScope,
        resolveHeaderFooterRegion,
    },
    inlineStyleSanitise: {
        isSafeInlineCssColor,
        isSafeInlineFontFamily,
        normalizeInlineFontSize,
        createRenderInlineTextHtml,
    },
    createRenderFormattedInlineHtml,
    markerSpanHtml: {
        createRenderCommentSpanHtml,
        createRenderRevisionSpanHtml,
        createRenderSearchSpanHtml,
    },
    inlineDrawingHelpers: {
        inlineDrawingIsSelected,
        createRenderDrawingObjectTestMarkerHtml,
    },
    objectAriaHtml: {
        createRenderObjectSelectionDescriptionAttribute,
        createRenderObjectResizeHandleHtml,
        createRenderObjectFocusPolicyAttributes,
        renderObjectRotationHandleHtml,
    },
    atomicOverlays: {
        createObjectFocusPolicy,
        createRenderSelectionOverlay,
        createRenderRevisionOverlay,
        createRenderCommentMarkers,
        restoreLogicalSelection,
        createApplyObjectFocusPolicyToElement,
    },
    createAtomicRendererFactory,
    imageLayoutBubble: {
        createRenderImageLayoutBubbleButton,
        createRenderImageLayoutBubbleHtml,
    },
    drawingFigureStyle: {
        createExplicitObjectLayerRect,
        createObjectLayerRectFromObject,
        createRenderDrawingFigureStyle,
        estimateInlineDrawingCaptionReserveHeight,
        createRenderDrawingAnchorReservationStyle,
    },
    createRenderImageFigureClasses,
    domTextPoint: {
        createFirstTextPointInElement,
        createProjectedDomTextPointAtBlockOffset,
    },
    findLayoutObjectForRender,
    createCollectWysiwygPageObjectEntries,
    createWysiwygObjectRenderEntryFactory,
    createSyncWysiwygObjectLayerPositions,
    inlineStyle: {
        isSafeInlineCssColor,
        safeInlineColor,
        buildInlineStyleAttribute,
        escapeAttribute,
    },
    revisionColor: {
        clamp01,
        describeRevisionColor,
        blendRevisionColor,
        revisionColorForAuthor,
        applyRevisionColorVars,
    },
    createRenderEngineTableHtml,
    createMarkersForBlock,
    objectOverlayHtml: {
        buildObjectOverlayStyle,
        createRenderWysiwygObjectSelectionOverlayHtml,
        createRenderWysiwygObjectGuidesOverlayHtml,
    },
    wysiwygObjectLayerRect: {
        applyWysiwygObjectLayerRect,
        createResolveWysiwygObjectLayerRect,
    },
    createGetWysiwygObjectVisualRectRelativeTo,
    groupProjectedWysiwygSegmentsByLine,
    createProjectedWysiwygLineRenderer,
    createSplitProjectedWysiwygSegmentsForReflow,
    createResolveProjectedWysiwygLineIntervals,
    restoreWysiwygProjectedParagraph,
    createShouldProjectWysiwygParagraph,
    createReflowProjectedWysiwygSegments,
    createProjectWysiwygParagraphAroundExclusions,
    createCollectWysiwygDomTextExclusions,
    reviewDisplayMode: {
        normalizeReviewDisplayModeClass,
        applyReviewDisplayModeClass,
    },
    domTextMapping: {
        isInlineBreakNode,
        isCaretPlaceholderNode,
        domLogicalLength,
        domBoundaryLogicalOffset,
        createFindTextNodeFactory,
    },
};

// Phase R — new model-owned core engine (assembly of extracted modules).
export const coreEngine = {
    createRenderHost,
    createCoreEditor,
    createInputSurface,
    clipboard: {
        serializeRange: coreSerializeRange,
        parseClipboard: coreParseClipboard,
        parseHtml: coreParseHtml,
        parsePlainText: coreParsePlainText,
    },
    editModel: {
        applyInsertText,
        applyDeleteBackward,
        applyDeleteForward,
        applyInsertParagraph,
        applyReplaceRange,
    },
    editFormat: {
        applyMarkToBlockRange,
        blockRangeHasMark,
        setParagraphProperty,
        firstMarkValueInRange,
    },
    hitTest: {
        hitTestPoint,
        collectCaretStops,
        caretStopAt,
        lineCaretStops,
    },
    caret: {
        moveCaretByKey,
        caretRect,
        blockMaxOffset,
        createCaretElement,
    },
    selection: {
        selectionRectsForRange,
        createSelectionRectElement,
        createCompositionUnderlineElement,
    },
    bidi: {
        applyBidiToLayout,
        applyBidiToBlock,
    },
    objectOverlay: {
        createObjectElement,
        objectHitTest,
        resizeRectByHandle,
        RESIZE_HANDLES,
    },
    createUndoStack,
    paragraphStyles: {
        applyParagraphStyle,
        getDocumentOutline,
        paragraphStyleName,
        DEFAULT_PARAGRAPH_STYLES,
    },
    editTable: {
        createTableModel,
        insertTableAfterBlock,
        addTableRow,
        addTableColumn,
        findTableContaining,
    },
    findReplace: {
        findMatches,
    },
    trackChanges: {
        acceptAllRevisions,
        rejectAllRevisions,
        listRevisions,
        hasRevisions,
    },
    comments: {
        addCommentMarkToRange,
        stripCommentMark,
        commentAnchorText,
        commentIdsInRange,
        collectCommentIds,
    },
    headerFooter: {
        textRun: hfTextRun,
        pageNumberField,
        pageCountField,
        setRegion: setHeaderFooterRegion,
        clearRegion: clearHeaderFooterRegion,
    },
    a11y: {
        applyEditorAria,
        headingAriaForBlock,
        describeCaretContext,
        createLiveRegion,
    },
    // R.5.18 / R.5.22 — operation algebra (op-log undo + collaboration OT).
    operations: {
        invertOperation,
        applyToText,
        applyOps,
        transformOperation,
        transformAgainstList,
    },
    // R.5.22 — client-side OT control for realtime collaboration over a sequencer/relay server.
    collab: {
        createCollabClient,
        createRelayCollabClient,
        transformChange,
    },
};

// Top-level default — what `window.tmDocumentEditorModules` becomes after the IIFE wrap.
export default Object.freeze({
    core,
    history,
    layout,
    objects,
    input,
    render,
    clipboard,
    accessibility,
    runtime,
    coreEngine,
    version: 'phase-d-skeleton-261',
});
