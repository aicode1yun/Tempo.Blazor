namespace Tempo.Blazor.E2E.CanvasEngine;

/// <summary>Canvas editor parity coverage entries used by phase 24 as a living command/provider/interaction matrix.</summary>
internal static class ParityCoverageMatrix
{
    public static IReadOnlyList<ParityCoverageEntry> Entries { get; } =
    [
        new("0", "Canvas clean-room bootstrap and route readiness", "openCanvasEngine, assertCanvasHostReady", "DocumentEditorCanvasEngineBaselineE2ETests", "Open the production canvas route and assert first paint readiness.", "DocumentEditorCanvasEngineBaselineE2ETests", "Legacy bootstrap parity is covered by the canvas-only host smoke."),
        new("1", "Canvas document model and conversion", "loadCanvasDocument, convertDocumentModel", "DocumentEditorCanvasEnginePhase1E2ETests", "Load, convert and inspect the canonical canvas model.", "DocumentEditorCanvasEnginePhase1E2ETests", "Model parity is verified before user commands execute."),
        new("2", "Canvas screenshot harness and visual assertions", "assertCanvasNonBlank, assertCaretVisible, assertToolbarStateMatchesModel", "DocumentEditorCanvasHarnessE2ETests", "Harness assertions run against the live canvas page.", "DocumentEditorCanvasHarnessE2ETests", "Screenshot and toolbar-state assertions are the reusable phase 24 gates."),
        new("3", "Canvas host, routing and shell contract", "openCanvasHost, toggleToolbar, inspectShellState", "DocumentEditorCanvasHostE2ETests", "Production host route loads the requested canvas document through the canvas engine.", "DocumentEditorCanvasHostE2ETests", "Host parity is canvas-selector based."),
        new("4", "Provider model save/reload round-trip", "saveDocument, reloadDocument, normalizeDocumentJson", "DocumentEditorCanvasModelRoundtripE2ETests.Phase4_CanvasModelRoundtrip_SaveReloadPreservesStructuredSnapshot", "In-memory provider saves, reloads and normalizes a structured document snapshot.", "DocumentEditorCanvasModelRoundtripE2ETests", "Provider boundary preserves text, table, image, header/footer, note, comment and revision anchors."),
        new("5", "Canvas page rendering", "renderDocument, renderTextRuns, renderPage", "DocumentEditorCanvasRenderE2ETests", "Rendered canvas pages expose model document id and content geometry.", "DocumentEditorCanvasRenderE2ETests", "Render parity is asserted through canvas layers and pixel checks."),
        new("6", "Text layout and wrapping", "layoutParagraph, layoutRuns, measureText", "DocumentEditorCanvasTextLayoutE2ETests", "Text layout survives responsive width changes and visual assertions.", "DocumentEditorCanvasTextLayoutE2ETests", "Legacy WYSIWYG text assertions are replaced by canvas text rect selectors."),
        new("7", "Caret and selection overlay", "moveCaret, selectRange, extendSelection", "DocumentEditorCanvasCaretSelectionE2ETests.Phase7_CaretAndSelection_UseOverlayWithoutRepaintingContent", "Selection is stored in the canvas engine model and reflected in toolbar state.", "DocumentEditorCanvasCaretSelectionE2ETests", "Caret/selection parity uses overlay selectors, not contenteditable DOM ranges."),
        new("8", "Typing, keyboard input and IME", "insertText, deleteBackward, composeText, commitComposition", "DocumentEditorCanvasTypingE2ETests; DocumentEditorCanvasImeE2ETests", "Typed and composed text persists in the current canvas model.", "DocumentEditorCanvasTypingE2ETests; DocumentEditorCanvasImeE2ETests", "Historical typing regressions are covered through the hidden input bridge."),
        new("9", "Inline formatting commands", "bold, italic, underline, fontFamily, fontSize, textColor, highlightColor, clearFormatting, undo, redo", "DocumentEditorCanvasInlineFormatE2ETests", "Inline marks are applied through toolbar commands and round-trip through the model.", "DocumentEditorCanvasInlineFormatE2ETests", "Toolbar pressed states are matched against the canvas model."),
        new("10", "Paragraph formatting and ruler", "alignLeft, alignCenter, alignRight, alignJustify, lineSpacing, spacingBefore, spacingAfter, increaseIndent, decreaseIndent, showRuler", "DocumentEditorCanvasParagraphE2ETests", "Paragraph settings save and reload through the document provider.", "DocumentEditorCanvasParagraphE2ETests", "Paragraph and ruler parity uses canvas paragraph geometry."),
        new("11", "Clipboard pipeline", "copy, cut, paste, pasteHtml, viewClipboardHtml", "DocumentEditorCanvasClipboardE2ETests.Phase11_Clipboard_CopyCutPasteRichHtmlAndDebugSnapshot", "Rich clipboard payloads are inserted into the live canvas model.", "DocumentEditorCanvasClipboardE2ETests", "Clipboard screenshots verify rendered paste result and debug snapshot."),
        new("12", "History, dirty state, manual save and autosave", "undo, redo, saveDocument, retrySave, autosaveDocument", "DocumentEditorCanvasHistorySaveE2ETests.Phase12_HistoryManualSaveReloadAndCategorySmoke_PersistsCanvasModel", "Manual save, autosave, retry and reload provider boundaries are tested.", "DocumentEditorCanvasHistorySaveE2ETests", "History parity keeps before/after screenshots and provider manifests."),
        new("13", "Toolbar, context menu and spellcheck", "bold, openContextMenu, applySpellSuggestion, undo", "DocumentEditorCanvasToolbarSpellcheckE2ETests", "Spell suggestion provider edits the live canvas model and supports undo.", "DocumentEditorCanvasToolbarSpellcheckE2ETests", "Toolbar and context menu parity has screenshot evidence."),
        new("14", "Tables", "insertTable, editTableCell, selectTable, tableProperties, undo, redo", "DocumentEditorCanvasTableE2ETests", "Table edits persist through the canvas document model.", "DocumentEditorCanvasTableE2ETests", "Table major interactions use screenshot and canvas cell selectors. insertTable is REALLY covered since Phase4_InsertTableFromToolbarGrid_RendersTypesAndPersists (command-layer plan phase 4) — earlier tests only exercised seeds that already contained tables, which masked the unregistered insertTable command. tableProperties/cellProperties are NOT engine commands: the ribbon/palette entries open the Properties side panel (which issues setTableProperties/setCellProperties) — covered by DocumentEditorCanvasCommandRegistryE2ETests.Phase10_PropertiesCommands_OpenPropertiesSidePanel (command-layer plan phase 10)."),
        new("15", "Images, drawings and object layout", "insertImage, updateImageLayout, resizeImage, dragImage, insertDrawing, setImageZOrder", "DocumentEditorCanvasImageE2ETests; DocumentEditorCanvasShapesDrawingsE2ETests", "Image and drawing objects save, reload and export through phase 15/19 providers.", "DocumentEditorCanvasImageE2ETests; DocumentEditorCanvasShapesDrawingsE2ETests", "Image/object selection, drag and resize have screenshot coverage. replaceImage/setImageLink are NOT engine commands: the ribbon/palette entries open the Properties side panel image inspector (which issues setImageUrl) — covered by DocumentEditorCanvasCommandRegistryE2ETests.Phase10_PropertiesCommands_OpenPropertiesSidePanel (command-layer plan phase 10)."),
        new("16", "Headers, footers and notes", "editHeader, editFooter, insertFootnote, insertEndnote, insertPageNumber, closeHeaderFooter", "DocumentEditorCanvasHeadersFootersNotesE2ETests", "Header/footer/note data exports through DOCX provider smoke in phase 19.", "DocumentEditorCanvasHeadersFootersNotesE2ETests", "Header/footer and note regions are verified with canvas screenshots."),
        new("17", "Comments, revisions and restricted editing", "addComment, openComments, trackChanges, acceptRevision, rejectRevision, protectDocument, markEditableRegion", "DocumentEditorCanvasCommentsRevisionsE2ETests.Phase17_CommentsRevisionsAndRestrictedEditing_RenderAndReviewFromCanvas", "Comment and revision anchors save and reload as canvas model metadata.", "DocumentEditorCanvasCommentsRevisionsE2ETests", "Review pane, marker and restricted-editing interactions are screenshot covered."),
        new("18", "Search, outline and table of contents", "findText, replaceText, openOutline, insertTableOfContents, updateAllFields", "DocumentEditorCanvasSearchOutlineTocE2ETests", "TOC and outline fields are generated from canvas heading data and saved.", "DocumentEditorCanvasSearchOutlineTocE2ETests", "Search highlights, outline navigation and TOC have screenshot assertions."),
        new("19", "Import/export providers", "importDocx, exportDocx, exportPdf, exportOdt, exportHtml, exportMarkdown, compareDocuments", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane", "DOCX, PDF, ODT, HTML, Markdown and compare provider boundaries use the live canvas snapshot.", "DocumentEditorCanvasImportExportE2ETests", "Imported first paint and compare smoke screenshots are attached."),
        new("20", "Collaboration and offline sync", "connectCollaboration, broadcastOperation, saveOfflineDraft, reconnectSync", "DocumentEditorCanvasCollaborationE2ETests.Phase20_TwoCanvasEditors_ConvergeAndRenderRemoteCaret_OverSignalR; DocumentEditorCanvasCollaborationE2ETests.Phase20_OfflineDraft_ReconnectsAndSyncsCanvasModel", "SignalR collaboration, offline draft and sync provider boundaries converge through real services.", "DocumentEditorCanvasCollaborationE2ETests", "Remote caret and offline banners are screenshot covered."),
        new("21", "Accessibility", "focusCanvas, keyboardNavigate, announceSelection, forcedColors", "DocumentEditorCanvasAccessibilityE2ETests.Phase21_CanvasAccessibilityMirrorKeyboardLiveRegionAndForcedColors_AreProductionReady", "Accessibility mirrors expose the current canvas state without provider fallback.", "DocumentEditorCanvasAccessibilityE2ETests", "Keyboard-only focus, live region and forced-colors screenshots are covered."),
        new("22", "Performance and virtualization", "scrollVirtualPages, measureFrameBudget, assertNoLayoutThrash", "DocumentEditorCanvasPerformanceE2ETests", "Performance diagnostics run against canvas selectors and live rendering.", "DocumentEditorCanvasPerformanceE2ETests", "Performance phase keeps visual sanity screenshots for virtualized pages."),
        new("23", "UX polish and acceptance gallery", "openGallery, switchResponsiveViewport, validatePolishedChrome", "DocumentEditorCanvasUxGalleryE2ETests", "Acceptance gallery uses production demo documents and canvas-only route.", "DocumentEditorCanvasUxGalleryE2ETests", "Desktop, tablet and mobile screenshots are the phase 23 gallery."),
        new("E1", "Numbering and list styles", "toggleNumberedList, toggleBulletList, restartNumbering, setNumberingValue, applyListStyle", "DocumentEditorCanvasNumberingListsE2ETests; DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxExtendedEPhaseSmoke_PreservesAdvancedAnchorsAndScreenshots", "Numbering definitions and list styles save, reload and DOCX round-trip.", "DocumentEditorCanvasNumberingListsE2ETests", "List markers and nested numbering are screenshot covered."),
        new("E2", "Tab stops and ruler markers", "addTabStop, moveTabStop, removeTabStop, setTabLeader, showRuler", "DocumentEditorCanvasTabStopsE2ETests", "Tab stops persist through save/reload provider boundary.", "DocumentEditorCanvasTabStopsE2ETests", "Ruler marker interactions have screenshot coverage."),
        new("E3", "Sections, columns and line numbering", "openPageLayout, setColumnCount, toggleLineNumbering, insertSectionBreak", "DocumentEditorCanvasSectionsColumnsE2ETests; DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxExtendedEPhaseSmoke_PreservesAdvancedAnchorsAndScreenshots", "Section properties save, reload and DOCX round-trip.", "DocumentEditorCanvasSectionsColumnsE2ETests", "Column flow and line numbering screenshots are attached."),
        new("E4", "Styles", "applyBlockStyle, createStyle, modifyStyle, undo, redo", "DocumentEditorCanvasStylesE2ETests; DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxExtendedEPhaseSmoke_PreservesAdvancedAnchorsAndScreenshots", "Style definitions save, reload and DOCX round-trip.", "DocumentEditorCanvasStylesE2ETests", "Style gallery and heading rendering are screenshot covered."),
        new("E5", "Fields and cross references", "insertCaption, insertCrossReference, insertTableOfFigures, insertBibliography, updateAllFields, insertToken", "DocumentEditorCanvasFieldsE2ETests; DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxExtendedEPhaseSmoke_PreservesAdvancedAnchorsAndScreenshots", "Fields and cross-reference targets save and reload.", "DocumentEditorCanvasFieldsE2ETests", "Updated field results and reference navigation have screenshots. insertToken (Blazor token panel → engine token run, command-layer plan phase 9) is covered by DocumentEditorTokenMenuE2ETests.Phase9_InsertMenu_OpensTokenPanelInsertsTokenAndPersists."),
        new("E6", "Advanced character formatting", "setSmallCaps, setAllCaps, setKerning, setCharacterSpacing, setBaselineShift, setLigatures", "DocumentEditorCanvasAdvancedCharE2ETests.PhaseE6_AdvancedCharacterFormattingPersistsThroughToolbarUndoSaveAndReload", "Advanced character properties persist through toolbar undo/save/reload.", "DocumentEditorCanvasAdvancedCharE2ETests", "Advanced character gallery has screenshot coverage."),
        new("E7", "Shapes, text boxes, lines, connectors, charts, and groups", "insertShape, insertTextBox, insertLine, insertConnector, insertChart, activateTextBoxEdit, updateChartData, updateImageLayout, updateConnectorEndpoint, groupObjects, ungroupObjects, alignObjects, distributeObjects, setImageZOrder, deleteObject, undo, redo", "DocumentEditorCanvasShapesDrawingsE2ETests.PhaseE7_CanvasConnectorEndpointClipboardAndAllDrawingTypesPersistWithScreenshotEvidence", "DocumentEditorCanvasShapesDrawingsE2ETests.PhaseE7_CanvasInsertedShapesPointerKeyboardAndDeleteInteractionsHaveScreenshotEvidence", "DocumentEditorCanvasShapesDrawingsE2ETests", "Covers canvas drawing layer UX, save/reload, clipboard, endpoint handles, nested text box editing, keyboard-only object controls, and responsive gallery screenshots."),
        new("E8", "Math equations, templates, symbols, slot editing, accessibility, and provider round-trip", "insertEquation, insertLinearMath, insertMathSymbol, insertFraction, insertRadical, insertSuperscript, insertSubscript, insertNary, insertDelimiter, insertLimit, insertAccent, insertBar, insertBorderBox, insertMatrix, setMathDisplayMode, activateMathSlot, selectMathSlotRange, moveMathSlot, insertMathSlotText, deleteMathSlotBackward, deleteMathSlotForward, addMathMatrixRow, addMathMatrixColumn, deactivateMathSlot, undo, redo", "DocumentEditorCanvasMathEquationsE2ETests.PhaseE8_CanvasMathEquationsRenderInsertAndPersist; DocumentDocxFormatTests math OMML smoke", "DocumentEditorCanvasMathEquationsE2ETests.PhaseE8_MathSlotEditingCommandsUndoRedoLiveRegionAndResponsiveScreenshots", "DocumentEditorCanvasMathEquationsE2ETests", "Covers clean-room math tree/layout/render, toolbar gallery, real keyboard slot editing, live math announcements, save/reload, responsive screenshots, and DOCX provider smoke."),
        new("E9", "Content controls and forms", "insertTextControl, insertDropdownControl, insertDateControl, fillContentControl, lockContentControl, validateForm", "DocumentEditorCanvasContentControlsE2ETests.PhaseE9_ContentControlsFillLockUndoSaveAndReload", "DocumentEditorCanvasContentControlsE2ETests.PhaseE9_AdvancedControlsNavigateRepeatSaveReloadAndScreenshot", "DocumentEditorCanvasContentControlsE2ETests", "Content controls, locks, repeating sections and form navigation have save/reload plus screenshots."),
        new("E10", "Autocorrect, symbols and format painter", "applyAutocorrect, insertSymbol, copyFormat, pasteFormat, undo, redo", "DocumentEditorCanvasAutocorrectE2ETests.PhaseE10_AutocorrectFormatPainterSymbolsUndoSaveAndReload", "DocumentEditorCanvasAutocorrectE2ETests.PhaseE10_AutocorrectFormatPainterSymbolsUndoSaveAndReload", "DocumentEditorCanvasAutocorrectE2ETests", "Autocorrect and format painter are tested through live toolbar interactions."),
        new("E11", "View modes and print", "switchPrintLayout, switchWebLayout, switchFocusMode, openPrintPreview, printDocument, exportPdf", "DocumentEditorCanvasViewModesPrintE2ETests", "Print preview exports PDF from the current canvas model.", "DocumentEditorCanvasViewModesPrintE2ETests", "View mode and print preview screenshots are covered."),
        new("E12", "Hyphenation, page background and advanced tables", "toggleHyphenation, setPageBackground, repeatHeaderRows, splitTable, distributeTableColumns", "DocumentEditorCanvasHyphenationAdvancedTablesE2ETests", "Hyphenation/page background/advanced table state saves and reloads.", "DocumentEditorCanvasHyphenationAdvancedTablesE2ETests", "Advanced table and page-background screenshots are covered.")
    ];

    public static IReadOnlyList<string> LegacyPhases { get; } =
        Enumerable.Range(0, 24).Select(phase => phase.ToString()).ToArray();

    public static IReadOnlyList<string> ExtendedPhases { get; } =
    [
        "E1",
        "E2",
        "E3",
        "E4",
        "E5",
        "E6",
        "E7",
        "E8",
        "E9",
        "E10",
        "E11",
        "E12"
    ];

    public static IReadOnlyList<string> RequiredSeedFeatureGroups { get; } =
    [
        "text",
        "heading",
        "list",
        "table",
        "image",
        "drawing",
        "shape",
        "math",
        "form",
        "header-footer",
        "notes",
        "comment",
        "revision",
        "field",
        "toc",
        "section",
        "columns"
    ];

    public static IReadOnlyList<ParityToolbarCommandCoverage> ToolbarCommands { get; } =
    [
        new("save", "E2E", "DocumentEditorCanvasHistorySaveE2ETests.Phase12_HistoryManualSaveReloadAndCategorySmoke_PersistsCanvasModel"),
        new("undo", "E2E", "DocumentEditorCanvasHistorySaveE2ETests; DocumentEditorCanvasInlineFormatE2ETests"),
        new("redo", "E2E", "DocumentEditorCanvasHistorySaveE2ETests; DocumentEditorCanvasInlineFormatE2ETests"),
        new("bold", "E2E", "DocumentEditorCanvasToolbarSpellcheckE2ETests; DocumentEditorCanvasHarnessE2ETests.AssertToolbarStateMatchesModelAsync"),
        new("italic", "E2E", "DocumentEditorCanvasHarnessE2ETests.AssertToolbarStateMatchesModelAsync"),
        new("underline", "E2E", "DocumentEditorCanvasInlineFormatE2ETests"),
        new("fontFamily", "E2E", "DocumentEditorCanvasInlineFormatE2ETests"),
        new("fontSize", "E2E", "DocumentEditorCanvasInlineFormatE2ETests"),
        new("textColor", "E2E", "DocumentEditorCanvasInlineFormatE2ETests"),
        new("highlightColor", "E2E", "DocumentEditorCanvasInlineFormatE2ETests"),
        new("clearFormatting", "E2E", "DocumentEditorCanvasInlineFormatE2ETests"),
        new("alignLeft", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("alignCenter", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("alignRight", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("alignJustify", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("lineSpacing", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("spacingBefore", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("spacingAfter", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("increaseIndent", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("decreaseIndent", "E2E", "DocumentEditorCanvasParagraphE2ETests"),
        new("insertTable", "E2E", "DocumentEditorCanvasTableE2ETests"),
        new("insertImage", "E2E", "DocumentEditorCanvasImageE2ETests"),
        new("insertEquation", "E2E", "DocumentEditorCanvasMathEquationsE2ETests.PhaseE8_CanvasMathEquationsRenderInsertAndPersist"),
        new("insertSymbol", "E2E", "DocumentEditorCanvasAutocorrectE2ETests.PhaseE10_AutocorrectFormatPainterSymbolsUndoSaveAndReload"),
        new("insertPageBreak", "E2E", "DocumentEditorCanvasSectionsColumnsE2ETests"),
        new("insertFootnote", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("insertEndnote", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("insertCaption", "E2E", "DocumentEditorCanvasFieldsE2ETests"),
        new("insertCrossReference", "E2E", "DocumentEditorCanvasFieldsE2ETests"),
        new("insertTableOfContents", "E2E", "DocumentEditorCanvasSearchOutlineTocE2ETests"),
        new("insertTableOfFigures", "E2E", "DocumentEditorCanvasFieldsE2ETests"),
        new("insertBibliography", "E2E", "DocumentEditorCanvasFieldsE2ETests"),
        new("updateAllFields", "E2E", "DocumentEditorCanvasFieldsE2ETests; DocumentEditorCanvasSearchOutlineTocE2ETests"),
        new("trackChanges", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests.Phase17_CommentsRevisionsAndRestrictedEditing_RenderAndReviewFromCanvas"),
        new("reviewDisplayMode", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("addComment", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("openComments", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("openRevisions", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("compareDocuments", "E2E", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("protectDocument", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("markEditableRegion", "E2E", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("showRuler", "E2E", "DocumentEditorCanvasParagraphE2ETests; DocumentEditorCanvasTabStopsE2ETests"),
        new("zoomPageWidth", "E2E", "DocumentEditorCanvasViewModesPrintE2ETests"),
        new("showBlocks", "E2E", "DocumentEditorCanvasUxGalleryE2ETests"),
        new("fullscreen", "E2E", "DocumentEditorCanvasUxGalleryE2ETests"),
        new("openPrintPreview", "E2E", "DocumentEditorCanvasViewModesPrintE2ETests"),
        new("printDocument", "E2E", "DocumentEditorCanvasViewModesPrintE2ETests"),
        new("viewDocumentJson", "ShellOnly", "DocumentEditorCanvasToolbarSpellcheckE2ETests toolbar shell asserts debug command exposure"),
        new("viewClipboardHtml", "E2E", "DocumentEditorCanvasClipboardE2ETests.Phase11_Clipboard_CopyCutPasteRichHtmlAndDebugSnapshot"),
        new("importDocx", "E2E", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("exportDocx", "E2E", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("exportPdf", "E2E", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("openVersions", "E2E", "DocumentEditorCanvasHistorySaveE2ETests"),
        new("insertPageNumber", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("insertPageCount", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("insertPageXOfY", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("insertDateField", "E2E", "DocumentEditorCanvasFieldsE2ETests"),
        new("insertDocumentTitleField", "E2E", "DocumentEditorCanvasFieldsE2ETests"),
        new("differentFirstPage", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("differentOddEven", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("closeHeaderFooter", "E2E", "DocumentEditorCanvasHeadersFootersNotesE2ETests"),
        new("insertShape", "E2E", "DocumentEditorCanvasShapesDrawingsE2ETests"),
        new("insertTextBox", "E2E", "DocumentEditorCanvasShapesDrawingsE2ETests"),
        new("insertConnector", "E2E", "DocumentEditorCanvasShapesDrawingsE2ETests"),
        new("insertChart", "E2E", "DocumentEditorCanvasShapesDrawingsE2ETests"),
        new("insertTextControl", "E2E", "DocumentEditorCanvasContentControlsE2ETests"),
        new("insertDropdownControl", "E2E", "DocumentEditorCanvasContentControlsE2ETests"),
        new("applyAutocorrect", "E2E", "DocumentEditorCanvasAutocorrectE2ETests"),
        new("copyFormat", "E2E", "DocumentEditorCanvasAutocorrectE2ETests"),
        new("pasteFormat", "E2E", "DocumentEditorCanvasAutocorrectE2ETests")
    ];

    public static IReadOnlyList<ParityProviderBoundaryCoverage> ProviderBoundaries { get; } =
    [
        new("Image", "save/export/reload", "DocumentEditorCanvasImageE2ETests; DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("Font", "save/reload/render", "DocumentEditorCanvasAdvancedCharE2ETests.PhaseE6_AdvancedCharacterFormattingPersistsThroughToolbarUndoSaveAndReload"),
        new("Token", "save/reload/render", "DocumentEditorCanvasFieldsE2ETests; DocumentEditorCanvasContentControlsE2ETests"),
        new("Mention", "save/reload/render", "DocumentEditorCanvasCommentsRevisionsE2ETests; DocumentEditorCanvasFieldsE2ETests"),
        new("PdfExport", "export/reload-smoke", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane; DocumentEditorCanvasViewModesPrintE2ETests"),
        new("Format", "import/export/reload", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("Comparison", "compare/export", "DocumentEditorCanvasImportExportE2ETests.Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane"),
        new("Suggestion", "save/undo/reload", "DocumentEditorCanvasToolbarSpellcheckE2ETests"),
        new("Collaboration", "sync/reload", "DocumentEditorCanvasCollaborationE2ETests.Phase20_TwoCanvasEditors_ConvergeAndRenderRemoteCaret_OverSignalR"),
        new("Offline", "draft/reconnect/reload", "DocumentEditorCanvasHistorySaveE2ETests.Phase12_SaveFailure_CreatesOfflineDraftRetryAndBeforeUnloadState; DocumentEditorCanvasCollaborationE2ETests.Phase20_OfflineDraft_ReconnectsAndSyncsCanvasModel"),
        new("Sync", "remote-operation/reload", "DocumentEditorCanvasCollaborationE2ETests.Phase20_TwoCanvasEditors_ConvergeAndRenderRemoteCaret_OverSignalR"),
        new("Audit", "manifest/reload", "DocumentEditorCanvasHistorySaveE2ETests.Phase12_HistoryManualSaveReloadAndCategorySmoke_PersistsCanvasModel; DocumentEditorCanvasImportExportE2ETests")
    ];

    public static IReadOnlyList<ParityInteractionCoverage> MajorInteractions { get; } =
    [
        new("typing", "DocumentEditorCanvasTypingE2ETests; DocumentEditorCanvasImeE2ETests", "DocumentEditorCanvasTypingE2ETests"),
        new("selection", "DocumentEditorCanvasCaretSelectionE2ETests.Phase7_CaretAndSelection_UseOverlayWithoutRepaintingContent", "DocumentEditorCanvasCaretSelectionE2ETests"),
        new("drag", "DocumentEditorCanvasImageE2ETests; DocumentEditorCanvasShapesDrawingsE2ETests", "DocumentEditorCanvasImageE2ETests; DocumentEditorCanvasShapesDrawingsE2ETests"),
        new("table", "DocumentEditorCanvasTableE2ETests", "DocumentEditorCanvasTableE2ETests"),
        new("image", "DocumentEditorCanvasImageE2ETests", "DocumentEditorCanvasImageE2ETests"),
        new("comment", "DocumentEditorCanvasCommentsRevisionsE2ETests", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("revision", "DocumentEditorCanvasCommentsRevisionsE2ETests", "DocumentEditorCanvasCommentsRevisionsE2ETests"),
        new("find", "DocumentEditorCanvasSearchOutlineTocE2ETests", "DocumentEditorCanvasSearchOutlineTocE2ETests"),
        new("toc", "DocumentEditorCanvasSearchOutlineTocE2ETests", "DocumentEditorCanvasSearchOutlineTocE2ETests"),
        new("form", "DocumentEditorCanvasContentControlsE2ETests", "DocumentEditorCanvasContentControlsE2ETests"),
        new("math", "DocumentEditorCanvasMathEquationsE2ETests.PhaseE8_MathSlotEditingCommandsUndoRedoLiveRegionAndResponsiveScreenshots", "DocumentEditorCanvasMathEquationsE2ETests"),
        new("shape", "DocumentEditorCanvasShapesDrawingsE2ETests.PhaseE7_CanvasInsertedShapesPointerKeyboardAndDeleteInteractionsHaveScreenshotEvidence", "DocumentEditorCanvasShapesDrawingsE2ETests")
    ];

    public static IReadOnlyList<string> LegacyCoreOnlyDiagnosticFiles { get; } =
    [
        "DocumentEditorJsRuntimeImageTests.cs",
        "DocumentEditorStrictEnginePhase0E2ETests.cs",
        "DocumentEditorStrictEnginePhase1And2E2ETests.cs",
        "DocumentEditorStrictEnginePhase3E2ETests.cs",
        "DocumentEditorStrictEnginePhase4E2ETests.cs",
        "DocumentEditorStrictEnginePhase5E2ETests.cs",
        "DocumentEditorStrictEnginePhase6E2ETests.cs",
        "DocumentEditorStrictEnginePhase7E2ETests.cs",
        "DocumentEditorStrictEnginePhase8E2ETests.cs",
        "DocumentEditorStrictEnginePhase9E2ETests.cs",
        "DocumentEditorStrictEnginePhase10E2ETests.cs",
        "DocumentEditorStrictEnginePhase11E2ETests.cs",
        "DocumentEditorStrictEnginePhase12E2ETests.cs",
        "DocumentEditorStrictEnginePhase13E2ETests.cs",
        "DocumentEditorStrictEnginePhase14E2ETests.cs",
        "DocumentEditorStrictEnginePhase15E2ETests.cs",
        "DocumentEditorStrictEnginePhase16E2ETests.cs",
        "DocumentEditorStrictEnginePhase17E2ETests.cs",
        "DocumentEditorStrictEnginePhase18E2ETests.cs",
        "DocumentEditorStrictEnginePhase18ImageRegionScopeE2ETests.cs",
        "DocumentEditorStrictEnginePhase19E2ETests.cs",
        "DocumentEditorStrictEnginePhase20E2ETests.cs",
        "DocumentEditorStrictEnginePhase22E2ETests.cs",
        "DocumentEditorStrictEnginePhase23E2ETests.cs"
    ];
}

internal sealed record ParityCoverageEntry(
    string Phase,
    string FeatureGroup,
    string CommandCoverage,
    string ProviderBoundaryCoverage,
    string InteractionCoverage,
    string ScreenshotCoverage,
    string Notes);

internal sealed record ParityToolbarCommandCoverage(string Command, string CoverageKind, string Test);

internal sealed record ParityProviderBoundaryCoverage(string Provider, string Boundary, string Test);

internal sealed record ParityInteractionCoverage(string Interaction, string Test, string ScreenshotTest);
