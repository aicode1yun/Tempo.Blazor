# E2E failures TODO: e2e-full-after-core-editor-ready-helper

Datum běhu: 2026-06-15 01:43:48 (TRX timestamp).

Zdrojový běh:

```bash
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --logger "trx;LogFilePrefix=e2e-full-after-core-editor-ready-helper" --results-directory TestResults/fixed --verbosity minimal
```

TRX: `TestResults/fixed/e2e-full-after-core-editor-ready-helper_net10.0_20260615014348.trx`

Souhrn:

- Failed: 420
- Passed: 1284
- Skipped: 14
- Total: 1718
- Duration: 7 h 56 m

## Implementační TODO

Checkboxy níže jsou pracovní checklist. Při opravách odškrtávat pouze položky, které mají cílený zelený rerun uvedený v poznámce pod položkou.

### DocumentEditor readiness/reset a izolace stavu

- [x] Zachytit diagnostiku při `WaitForDocumentEditorReadyAsync` timeoutu: URL, render engine atributy, console errors, existence hostu, počet `.tm-wysiwyg-block`, aktivní floating UI a `data-focus-owner`.
  - Ověření: helper nově při timeoutu vrací URL, ready state, render engine, engine mode, active region, focus owner, počty bloků, floating UI, error UI a active element; minimální rerun `e2e-documenteditor-readiness-reset-pending-priority` prošel `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-readiness-reset-pending-priority_net10.0_20260615063356.trx`).
- [x] Opravit route bootstrap pro recovery/quality/autosave testy tak, aby zaseknutá navigace nebo prázdný host neblokovaly desítky testů po 60 s.
  - Ověření: recovery helper i Phase16 autosave helper otevírají legacy render engine přes `renderEngine=Legacy`; minimální rerun `e2e-documenteditor-readiness-reset-pending-priority` prošel `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-readiness-reset-pending-priority_net10.0_20260615063356.trx`).
- [x] Před každým legacy/recovery otevřením resetovat selection/object focus/floating toolbars/table toolbar/live-region state, aby starý image/table state neovlivnil další test.
  - Ověření: `DocumentEditorE2EReset` nově instaluje klientskou izolaci pro editorové storage flagy a po readiness resetuje transientní selection/focus/beforeunload/live-region/object-pane stav; minimální rerun `e2e-documenteditor-readiness-reset-client-isolation` prošel `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-readiness-reset-client-isolation_net10.0_20260615063916.trx`).
- [x] Ověřit minimem: `DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_SpaceKey_IsVisibleBeforeNextCharacter` a `DocumentEditorPhase16AutosaveE2ETests.Phase16_Autosave_ShowsWaitingSavingAndSynchronizedStatus`.
  - Ověření: `e2e-documenteditor-readiness-reset-client-isolation` prošlo `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-readiness-reset-client-isolation_net10.0_20260615063916.trx`).

### DocumentEditor image object vrstvy, hit-testing a wrap layout

- [x] Opravit pointer-events/z-index pro `document-wysiwyg-guides-layer`, selected image controls a `in-front-of-text` vrstvu: root overlay nesmí blokovat klik na jiný objekt/text; interaktivní mají být pouze handle/toolbar prvky.
  - Ověření: `e2e-documenteditor-phase9-phase11-behindtext-zorder-suppressed` prošlo `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-phase9-phase11-behindtext-zorder-suppressed_net10.0_20260615060308.trx`).
- [x] Stabilizovat `Phase11_ImageInspectorUpdatesAltAndWrap`: selected object overlay už neblokuje klik a test míří na vložený obrázek přes stabilní `data-object-id`, ne na křehký `figure.Last` přes měnící se render vrstvy.
  - Ověření: `e2e-documenteditor-image-inspector-wrap-objectid` prošlo `Failed: 0, Passed: 1, Skipped: 0, Total: 1`.
- [x] Dořešit sdílený behind-text hit-test/layout root pro `Phase9` a `Phase11` layering: po opravě stale `data-wrap-mode` hodnot a command helper fallbacku oba cílené běhy padají na `No text segment intersects the behind-text image`.
  - Ověření: `e2e-documenteditor-phase9-phase11-behindtext-zorder-suppressed` prošlo `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-phase9-phase11-behindtext-zorder-suppressed_net10.0_20260615060308.trx`).
- [x] Opravit image focus/chrome: keyboard selection, delete/arrow keys, layout bubble, inspector, alt/source metadata, missing-alt warning a persistence.
  - Ověření: `e2e-documenteditor-js-runtime-image-focus-chrome-2` prošlo `Failed: 0, Passed: 10, Skipped: 0, Total: 10` (`TestResults/fixed/e2e-documenteditor-js-runtime-image-focus-chrome-2_net10.0_20260615072138.trx`).
- [x] Opravit text exclusion pro tight/top-bottom/behind/in-front image layout: line intervals nesmí protínat image/caption rect a side text musí zůstat hit-testovatelný.
  - Ověření: `e2e-documenteditor-text-exclusion-wrap-smoke-13` prošlo `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-documenteditor-text-exclusion-wrap-smoke-13_net10.0_20260615075622.trx`).
- [x] Ověřit minimem: `Phase11_ImageInspectorUpdatesAltAndWrap`, `DefaultContractDemo_ReloadAndInspectorDoNotMaskProviderVsUrlImages`, `DocumentEditor_Strict_Engine_Phase19_LoadWrapFootprintsMatchOnlyOfficeContracts`.
  - Ověření: `e2e-documenteditor-image-wrap-minimum-24` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-documenteditor-image-wrap-minimum-24_net10.0_20260615083014.trx`).

### DocumentEditor WYSIWYG selection, commands a floating UI

- [x] Opravit mouse selection a collapsed caret mapping tak, aby typing/space/enter, mini toolbar, context menu a ribbon commandy pracovaly nad stejným selection tokenem.
  - Ověření: `e2e-onlyoffice-selection-toolbar-pointer-next-06` prošlo `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-onlyoffice-selection-toolbar-pointer-next-06_net10.0_20260615112931.trx`).
- [x] Opravit toolbar state sync pro caret/selection formatting včetně mixed state, bold/color/highlight/font/line-height.
  - Ověření: `e2e-onlyoffice-selection-toolbar-pointer-next-06` prošlo `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-onlyoffice-selection-toolbar-pointer-next-06_net10.0_20260615112931.trx`).
- [x] Opravit floating toolbar/context menu/dialog focus: mini toolbar se má otevřít po reálném výběru, zůstat ve viewportu a nezakrývat text; Escape/Tab/F10 mají zavírat správnou vrstvu.
  - Ověření: `e2e-onlyoffice-selection-toolbar-pointer-next-06` prošlo `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-onlyoffice-selection-toolbar-pointer-next-06_net10.0_20260615112931.trx`).
- [x] Dořešit dílčí WYSIWYG selection/command smoke: mezera posouvá caret před dalším znakem, kombinované B/I/U formátování nemění okolní text, text context menu spouští Bold/Comment a highlight picker po návratu na plain text nehlásí starou barvu.
  - Ověření: `e2e-documenteditor-wysiwyg-selection-commands-next-04` prošlo `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-documenteditor-wysiwyg-selection-commands-next-04_net10.0_20260615090759.trx`).
- [x] Dořešit dílčí paragraph/ribbon state: paragraph toolbar commandy po mouse selection cílí na původní selection, vrací caret na focus bod, line-spacing/spacing paragraph payloady se aplikují z runtime commandu a WYSIWYG render propisuje line-height/margins/indent do viditelného bloku.
  - Ověření: `e2e-documenteditor-paragraph-state-next-03` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-documenteditor-paragraph-state-next-03_net10.0_20260615092330.trx`).
- [x] Ověřit minimem: `DocumentEditor_Wysiwyg_Phase1TypingKeepsCaretAfterInsertedCharacter`, `DocumentEditor_Phase12_MiniToolbarBoldPreservesSelection`, `DocumentEditor_ToolbarReflectsCaretFormattingStateFromWysiwygSelection`.
  - Ověření: `e2e-documenteditor-wysiwyg-selection-toolbar-minimum-03` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-documenteditor-wysiwyg-selection-toolbar-minimum-03_net10.0_20260615084834.trx`).

### DocumentEditor revisions/comments/find/replace/save boundary

- [x] Opravit track changes insert/delete/review display: inline markers, panel state, accept/reject, undo/redo a save/reload nesmí vracet reviewed revisions.
  - Ověření: `e2e-review-comments-save-next-04` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-review-comments-save-next-04_net10.0_20260615115323.trx`).
- [x] Opravit comment anchor/composer flow: seed anchors, submit disabled state, panel bidirectional sync a JS runtime comment anchors.
  - Ověření: `e2e-comments-markers-next-03` prošlo `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-comments-markers-next-03_net10.0_20260615123238.trx`).
- [x] Opravit find/replace marker store, remote cursor markers a live region priority tak, aby image selection message nepřepisovala find stav.
  - Dílčí ověření replace-one s track changes: `e2e-review-comments-save-next-04` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-review-comments-save-next-04_net10.0_20260615115323.trx`).
  - Dílčí ověření marker store/search/remote/protection: `e2e-comments-markers-next-03` prošlo `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-comments-markers-next-03_net10.0_20260615123238.trx`).
  - Ověření live-region/find/save/autosave: `e2e-live-region-clean-01` prošlo `Failed: 0, Passed: 1, Skipped: 0, Total: 1` (`TestResults/fixed/e2e-live-region-clean-01_net10.0_20260615130339.trx`).
- [x] Opravit save/autosave boundary: manual save message `Saved`, autosave statusy, dirty state, failed save retry a canonical patch dispatch.
  - Ověření: `e2e-review-comments-save-next-04` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-review-comments-save-next-04_net10.0_20260615115323.trx`).
- [x] Ověřit minimem: `OnlyOfficeParity_ReviewedRevisions_DoNotReturnAfterSaveReload`, `Phase9_ReplaceOne_WithTrackChangesCreatesReviewableRevisions`, `Recovery_HeaderFooter_VisibleEditableAndPersistent`.
  - Ověření: `e2e-review-comments-save-next-04` prošlo `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-review-comments-save-next-04_net10.0_20260615115323.trx`).

### DocumentEditor table, clipboard, autocomplete, page UX a import/export

- [x] Opravit table insert/grid picker/context menu/cell typing/row-column commands/persistence v legacy i JS runtime testech.
  - Ověření: finální legacy+strict table subset prošel `Failed: 0, Passed: 8, Skipped: 0, Total: 8` (`TestResults/fixed/e2e-table-legacy-strict-04_net10.0_20260615163734.trx`).
  - Ověření: finální JS runtime table suite prošla `Failed: 0, Passed: 7, Skipped: 0, Total: 7` (`TestResults/fixed/e2e-table-cluster-clean-05_net10.0_20260615163055.trx`).
- [x] Opravit paste pipeline pro plain text, Word HTML, TSV/Sheets, image paste, paste report a undo batching.
  - Ověření: Phase10 clipboard suite prošla `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-clipboard-phase10-current-03_net10.0_20260615170412.trx`).
  - Ověření: legacy WYSIWYG paste smoke prošel `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-clipboard-legacy-current-01_net10.0_20260615172550.trx`).
  - Ověření: strict Phase12 paste smoke prošel `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-clipboard-strict-current-01_net10.0_20260615172803.trx`).
- [x] Opravit autocomplete/token/slash/mention popovery v editoru včetně mobile viewport clamping.
  - Ověření: Phase14 autocomplete suite prošla `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-autocomplete-phase14-current-03_net10.0_20260615174541.trx`).
- [x] Opravit page break, nonprinting marks, empty body/header/footer placeholders, overflow warning a outline active heading.
  - Ověření: Phase15 Page UX suite prošla `Failed: 0, Passed: 6, Skipped: 0, Total: 6` (`TestResults/fixed/e2e-pageux-phase15-full-11_net10.0_20260615184153.trx`).
- [x] Opravit Phase19 import/export smoke a structured image/table properties persistence.
  - Ověření: Phase19 import/export suite prošla `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-phase19-import-export-current-02_net10.0_20260615185607.trx`).
- [x] Ověřit minimem: `Phase10_WordListPasteShowsReportAndUndoesAsSingleTransaction`, `Phase12_GridPickerKeyboardInsertsFourByFiveTable`, `TokenTrigger_TypedInEditor_InsertsSelectedTokenAndRemovesQuery`, `Phase15_PageNavigator_NavigatesToSecondPageAfterPageBreak`.
  - Ověření: minimální table/clipboard/token/page suite prošla `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-table-clipboard-token-page-minimum-current-01_net10.0_20260615185836.trx`).

### DocumentEditor strict engine internals

- [x] Opravit selection normalization/hit-test mapper/input replacement: range replace nesmí nechat původní text za vložením.
  - Ověření: strict Phase4 selection suite prošla `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-strict-phase4-current-04_net10.0_20260615193558.trx`).
  - Ověření: strict Phase8 input suite prošla `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-strict-phase8-current-03_net10.0_20260615192733.trx`).
- [x] Opravit text layout greedy line breaker, justify metadata, paragraph invalidation/pagination handoff a wrap bubble DOM reflow.
  - Ověření: strict Phase5 text layout suite prošla `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-strict-phase5-current-03_net10.0_20260615192535.trx`).
  - Ověření: strict Phase6 pagination suite prošla `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-strict-phase6-current-02_net10.0_20260615193206.trx`).
  - Ověření: live typing wrap smoke + Phase19 load wrap kontrakt prošly `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-strict-layout-wrap-current-04_net10.0_20260615200455.trx`).
- [x] Dořešit strict Phase3 paragraph formatting: line spacing mixed state/persistence, spacing before/after a indent save/reload používají stejné paragraph hodnoty jako WYSIWYG runtime.
  - Ověření: `e2e-documenteditor-strict-phase3-paragraph-next-02` prošlo `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-strict-phase3-paragraph-next-02_net10.0_20260615093126.trx`).
  - Doplňkové ověření: `e2e-documenteditor-strict-paragraph-alignment-history-next-01` prošlo `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-documenteditor-strict-paragraph-alignment-history-next-01_net10.0_20260615093416.trx`).
  - Doplňkové ověření listů: `e2e-documenteditor-strict-phase3-list-next-03` prošlo `Failed: 0, Passed: 1, Skipped: 0, Total: 1` (`TestResults/fixed/e2e-documenteditor-strict-phase3-list-next-03_net10.0_20260615094306.trx`).
- [x] Opravit history transactions: typing undo/redo source, image drag undo geometry a revision accept undo.
  - Ověření: strict Phase15 history suite prošla `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-strict-phase15-current-03_net10.0_20260615201304.trx`).
- [x] Opravit strict boundary/facade lifecycle: disposed error code, canonical patch after commit, active transaction recovery, watchdog text preservation.
  - Ověření: strict Phase16 boundary/facade suite prošla `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-strict-phase16-current-02_net10.0_20260615201844.trx`).
- [x] Ověřit minimem: strict phase 4/5/8/15/16 cílené testy podle dotčené změny.
  - Ověření: kombinované strict minimum Phase4/5/6/8/15/16 prošlo `Failed: 0, Passed: 27, Skipped: 0, Total: 27` (`TestResults/fixed/e2e-strict-internals-minimum-current-01_net10.0_20260615202526.trx`).

### Spreadsheet

- [x] Opravit spreadsheet demo/grid readiness a `.tm-spreadsheet-grid` indexování pro canvas/dom renderer testy.
  - Ověření: kombinovaný spreadsheet grid readiness subset prošel `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-spreadsheet-grid-readiness-current-01_net10.0_20260615213552.trx`).
- [x] Opravit canvas navigation/scroll: ArrowDown/ArrowRight monotonic scroll, drag autoscroll a editor alignment při scrollu.
  - Ověření: canvas scroll/navigation subset prošel `Failed: 0, Passed: 6, Skipped: 0, Total: 6` (`TestResults/fixed/e2e-spreadsheet-canvas-scroll-current-02_net10.0_20260615214245.trx`).
- [x] Opravit formula bar sessions: drag range replacement, mixed formula token replacement, F2 transfer, long-session scroll replacement a Blazor active-cell sync.
  - Ověření: formula session subset prošel `Failed: 0, Passed: 5, Skipped: 0, Total: 5` (`TestResults/fixed/e2e-spreadsheet-formula-sessions-current-02_net10.0_20260615215340.trx`).
- [x] Opravit formula reference highlight drawing a benchmark page testids/result rows.
  - Ověření: highlight + benchmark subset prošel `Failed: 0, Passed: 7, Skipped: 0, Total: 7` (`TestResults/fixed/e2e-spreadsheet-highlights-benchmark-current-02_net10.0_20260615220015.trx`).
- [x] Ověřit minimem: `ArrowNavigation_ScrollsGridVertically`, `CanvasJsEngine_FormulaBarMixedFormulaDragRangeReplacesOnlyCaretTargetedToken`, `BenchmarkPage_ExposesPasteLatencyForCanvasAndCanvasJsEngine`.
  - Ověření: minimální spreadsheet subset prošel `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-spreadsheet-minimum-current-01_net10.0_20260615220122.trx`).

### Notion editor

- [x] Opravit Notion demo/fixture readiness pro `.tm-notion-editor`, database block `.tm-db`, nav/special/media blocks a seeded pages.
  - Ověření: `NotionMediaBlocksE2ETests`, `NotionNavBlocksE2ETests` a `NotionSpecialBlocksE2ETests` prošly `Failed: 0, Passed: 23, Skipped: 0, Total: 23` (`TestResults/fixed/e2e-notion-media-nav-special-current-02_net10.0_20260616003821.trx`).
- [x] Opravit block type transforms pro heading, bullet, todo a divider.
  - Ověření: celá `NotionBlockEditingE2ETests` prošla `Failed: 0, Passed: 22, Skipped: 0, Total: 22` (`TestResults/fixed/e2e-notion-block-editing-current-03_net10.0_20260615231446.trx`).
- [x] Opravit inline toolbar selection/visibility, link remove unwrap, slash menu default/arrow selection a page search výsledky/navigaci.
  - Ověření: `NotionFormattingE2ETests` prošly `Failed: 0, Passed: 21, Skipped: 0, Total: 21` (`TestResults/fixed/e2e-notion-formatting-current-02_net10.0_20260616015236.trx`), `NotionKeyboardE2ETests` prošly `Failed: 0, Passed: 11, Skipped: 0, Total: 11` (`TestResults/fixed/e2e-notion-keyboard-current-02_net10.0_20260616015503.trx`) a `NotionSlashMenuE2ETests` prošly `Failed: 0, Passed: 17, Skipped: 0, Total: 17` (`TestResults/fixed/e2e-notion-slash-current-02_net10.0_20260616015845.trx`).
- [x] Opravit database block render a až poté filter/sort/group/fields/views/record detail flows.
  - Ověření: celá `NotionDatabaseBasicE2ETests` prošla `Failed: 0, Passed: 14, Skipped: 0, Total: 14` (`TestResults/fixed/e2e-notion-database-basic-current-04_net10.0_20260615233347.trx`).
  - Ověření: celá `NotionDatabaseAdvancedE2ETests` prošla `Failed: 0, Passed: 14, Skipped: 0, Total: 14` (`TestResults/fixed/e2e-notion-database-advanced-current-02_net10.0_20260615234835.trx`).
- [x] Opravit media dialog/image-video-file-audio render, comments notification badge/draft composer a read-only `contenteditable=false` DOM contract.
  - Ověření: `NotionMediaBlocksE2ETests` prošly `Failed: 0, Passed: 13, Skipped: 0, Total: 13` (`TestResults/fixed/e2e-notion-media-current-01_net10.0_20260616022526.trx`), `NotionCommentsE2ETests` prošly `Failed: 0, Passed: 50, Skipped: 0, Total: 50` (`TestResults/fixed/e2e-notion-comments-current-01_net10.0_20260616022042.trx`), `NotionCommentsRecoveryE2ETests` prošly `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-notion-comments-recovery-current-01_net10.0_20260616022211.trx`), `NotionReadOnlyE2ETests` prošly `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-notion-readonly-current-01_net10.0_20260616020606.trx`) a EB12 locked `contenteditable=false` kontrakt prošel `Failed: 0, Passed: 1, Skipped: 0, Total: 1` (`TestResults/fixed/e2e-notion-page-settings-readonly-current-01_net10.0_20260616022551.trx`).
- [x] Ověřit minimem: `Database_TableView_LoadsData`, `Heading1_Type_RendersH1`, `InlineToolbar_Link_Remove_UnwrapsAnchor`, `ImageBlock_EnterUrl_DisplaysImage`.
  - Ověření: minimální Notion subset prošel `Failed: 0, Passed: 4, Skipped: 0, Total: 4` (`TestResults/fixed/e2e-notion-minimum-current-05_net10.0_20260615222906.trx`).

### Signing/PdfTemplate/Email/Modeling/FormulaBuilder

- [x] Opravit PdfTemplateDesigner route readiness a draw field count update.
  - Ověření: celá `PdfTemplateDesignerE2ETests` prošla `Failed: 0, Passed: 17, Skipped: 0, Total: 17` (`TestResults/fixed/e2e-pdf-template-current-02_net10.0_20260616023903.trx`).
- [x] Opravit Signing mobile signature canvas hit target, comment draft composer, field editor localization/rename preview, runner step keyboard order a overlay resize/zoom stability.
  - Ověření: `SigningDocumentCommentsE2ETests` prošly `Failed: 0, Passed: 9, Skipped: 0, Total: 9` (`TestResults/fixed/e2e-signing-comments-current-02_net10.0_20260616024255.trx`), `SigningFieldEditorPanelE2ETests` prošly `Failed: 0, Passed: 46, Skipped: 0, Total: 46` (`TestResults/fixed/e2e-signing-field-editor-current-02_net10.0_20260616030737.trx`), `SigningLocalizationE2ETests` prošly `Failed: 0, Passed: 10, Skipped: 0, Total: 10` (`TestResults/fixed/e2e-signing-localization-current-01_net10.0_20260616025223.trx`), `SigningQualityE2ETests` prošly `Failed: 0, Passed: 8, Skipped: 0, Total: 8` (`TestResults/fixed/e2e-signing-quality-current-03_net10.0_20260616030002.trx`) a `SignatureCaptureE2ETests` prošly `Failed: 0, Passed: 6, Skipped: 0, Total: 6` (`TestResults/fixed/e2e-signature-capture-current-02_net10.0_20260616030309.trx`).
- [x] Opravit EmailEditor drag empty drop target `[data-tm-drop-empty]`.
  - Ověření: `DemoEmailTemplateStoreTests` prošly `Failed: 0, Passed: 8, Skipped: 0, Total: 8` (`TestResults/fixed/api-email-template-store-current-01_net10.0_20260616054359.trx`) a celá `EmailEditorDragDropE2ETests` prošla `Failed: 0, Passed: 7, Skipped: 0, Total: 7` (`TestResults/fixed/e2e-email-dragdrop-current-02_net10.0_20260616054546.trx`).
- [x] Opravit Modeling BPMN unknown AI task fallback a source panel loading indicator.
  - Ověření: BPMN/DI unit subset prošel `Failed: 0, Passed: 10, Skipped: 0, Total: 10` (`TestResults/fixed/unit-modeling-bpmn-di-current-01_net10.0_20260616055425.trx`), cílený modeling E2E subset prošel `Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`TestResults/fixed/e2e-modeling-targeted-current-03_net10.0_20260616055534.trx`) a dotčené E2E třídy prošly `Failed: 0, Passed: 10, Skipped: 0, Total: 10` (`TestResults/fixed/e2e-modeling-classes-current-01_net10.0_20260616055706.trx`).
- [x] Opravit FormulaBuilder token display value (`{{Subtotal}}`) místo interního id (`{{formula-subtotal}}`).
  - Ověření: unit subset FormulaBuilder/helper prošel `Failed: 0, Passed: 22, Skipped: 0, Total: 22` (`TestResults/fixed/unit-formula-builder-current-01_net10.0_20260616060024.trx`), cílený E2E test prošel `Failed: 0, Passed: 1, Skipped: 0, Total: 1` (`TestResults/fixed/e2e-formula-builder-targeted-current-02_net10.0_20260616060130.trx`) a celá `FormulaBuilderE2ETests` prošla `Failed: 0, Passed: 3, Skipped: 0, Total: 3` (`TestResults/fixed/e2e-formula-builder-class-current-01_net10.0_20260616060201.trx`).
- [x] Ověřit minimem: příslušné single-test filtry pro každou komponentu.
  - Ověření: minimální subset pro PdfTemplate/Signing/Email/Modeling/FormulaBuilder prošel `Failed: 0, Passed: 10, Skipped: 0, Total: 10` (`TestResults/fixed/e2e-final-minimum-current-01_net10.0_20260616060355.trx`).

## Failing testy podle tříd

- 161: `Tempo.Blazor.E2E.DocumentEditorE2ETests`
- 23: `Tempo.Blazor.E2E.SpreadsheetE2ETests`
- 14: `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests`
- 14: `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests`
- 9: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests`
- 8: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests`
- 7: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests`
- 7: `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests`
- 6: `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests`
- 6: `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests`
- 6: `Tempo.Blazor.E2E.NotionBlockEditingE2ETests`
- 6: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests`
- 6: `Tempo.Blazor.E2E.NotionNavBlocksE2ETests`
- 6: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests`
- 5: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests`
- 5: `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests`
- 5: `Tempo.Blazor.E2E.NotionFormattingE2ETests`
- 4: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests`
- 4: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase0E2ETests`
- 4: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests`
- 4: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests`
- 4: `Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests`
- 4: `Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests`
- 4: `Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests`
- 3: `Tempo.Blazor.E2E.NotionSlashMenuE2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests`
- 3: `Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests`
- 3: `Tempo.Blazor.E2E.DocumentEditorPhase20PerformanceE2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests`
- 3: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests`
- 3: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests`
- 2: `Tempo.Blazor.E2E.NotionCommentsE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorPhase7MarkerStoreE2ETests`
- 2: `Tempo.Blazor.E2E.EmailEditorDragDropE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase4E2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase15E2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorPhase3CommandRegistryE2ETests`
- 2: `Tempo.Blazor.E2E.PdfTemplateDesignerE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorPhase21AccessibilityE2ETests`
- 2: `Tempo.Blazor.E2E.NotionKeyboardE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorPhase9FindReplaceE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase16E2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase1And2E2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase4E2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase11E2ETests`
- 2: `Tempo.Blazor.E2E.SigningQualityE2ETests`
- 2: `Tempo.Blazor.E2E.SigningFieldEditorPanelE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorPhase19ImportExportE2ETests`
- 2: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase5E2ETests`
- 1: `Tempo.Blazor.E2E.FormulaBuilderE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorPhase22DemoDocsE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase10E2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorPhase11ImageUxE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase8E2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorPhase6SchemaPolicyE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase17E2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase6E2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase23E2ETests`
- 1: `Tempo.Blazor.E2E.NotionCommentsRecoveryE2ETests`
- 1: `Tempo.Blazor.E2E.NotionPageSettingsRecoveryE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeCommentTests`
- 1: `Tempo.Blazor.E2E.SignatureCaptureE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRegionTests`
- 1: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSelectionTests`
- 1: `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase3E2ETests`
- 1: `Tempo.Blazor.E2E.SpreadsheetBlockE2ETests`
- 1: `Tempo.Blazor.E2E.ModelingSourcePanelM9E2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorPhase15ImageLayoutStressE2ETests`
- 1: `Tempo.Blazor.E2E.ModelingBpmnProfileM17E2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorImageOnlyOfficeParityE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorPhase5RuntimeModularizationE2ETests`
- 1: `Tempo.Blazor.E2E.SigningLocalizationE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorPhase18DebugE2ETests`
- 1: `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase20E2ETests`

## Přesný seznam failing testů

### `Tempo.Blazor.E2E.DocumentEditorE2ETests`

1. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_HighlightPickerReflectsActualSelectionBackground` (00:00:30.6124408)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_HighlightPickerReflectsActualSelectionBackground threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text '#ffffff' But was: ' Highlight ' Call log: - - LocatorAssertions.ToConta...
2. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageInspectorStaysInsideEditorViewportAwayFromSidePanel` (00:00:43.8884382)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageInspectorStaysInsideEditorViewportAwayFromSidePanel threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figu...
3. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageReplaceShowsSourceChoicesInsteadOfOpeningUploadImmediately` (00:00:43.6259304)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageReplaceShowsSourceChoicesInsteadOfOpeningUploadImmediately threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locato...
4. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageSelectionDoesNotSurviveTextCaretNavigation` (00:00:43.4159222)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageSelectionDoesNotSurviveTextCaretNavigation threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wys...
5. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageWrapPhase10_ArrowKeysStayTextAndExplicitShortcutSelectsObject` (00:00:19.2827467)
   - Příznak: Expected probe.Issues to be empty because the keyboard navigation scenario needs a stable side caret interval, but found at least one item {"left interval hit must be a text caret in phase10-arrow-text-d5df03393e524e70a71cc34e46bb7567, got {"Kind":"None","ActiveImageBlockId":null...
6. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageWrapPhase10_CenterSquareIntervalsObjectAndBlockedGapHitTesting` (00:00:19.1033933)
   - Příznak: Expected probe.Issues to be empty because center Square must expose left/right text intervals, object body hit and a non-caret blocked wrap gap, but found at least one item {"left interval hit must be a text caret in phase10-hit-text-6128cf85155c4a4ea96c667d169c5b0a, got {"Kind":...
7. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ImageWrapPhase10_EmptyParagraphTypingUsesVirtualCaretAffinityAndEscapeRestoresText` (00:00:18.6599010)
   - Příznak: Expected rightProbe.Issues to be empty because an empty paragraph with a left Square image must publish a virtual right-side caret interval, but found at least one item {"left interval hit must be a text caret in phase10-empty-right-adf3f33ad8c342ec9680551bd8e492a0, got {"Kind":"...
8. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_MouseParagraphCommandsKeepRibbonStateInSync` (00:00:35.3536793)
   - Příznak: Expected styled.LineHeight to be the same string, but they differ at index 0: ↓ (actual) "21.2667px" "1.5" ↑ (expected).
9. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ParagraphAlignmentCommandsCollapseMouseSelection` (00:00:26.9360005)
   - Příznak: Expected selectionAfterJustify.IsCollapsed to be True because paragraph toolbar commands should use the selection as the target and then return to a caret, but found False.
10. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase0_CenterSquareWrapUsesLeftAndRightTextIntervals` (00:00:52.4662435)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase0_CenterSquareWrapUsesLeftAndRightTextIntervals threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.t...
11. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_FindPanel_EscapeThenSidePanelEscapeClosesBoth` (00:00:45.4953152)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_FindPanel_EscapeThenSidePanelEscapeClosesBoth threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-find-close']") - - locator resolv...
12. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_ImageAnchorMoveFixedLockGlyphAndPersist` (00:00:27.7053827)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_ImageAnchorMoveFixedLockGlyphAndPersist threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - wa...
13. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_LinkDialog_EscapeCloses` (00:00:20.3823166)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_LinkDialog_EscapeCloses threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '0' But was: '1' Call log: - - LocatorAssertions.ToHaveCountAsync with timeout 3000ms - - w...
14. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_LinkDialog_TabFocusesUrlInput` (00:00:24.5226633)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_LinkDialog_TabFocusesUrlInput threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be focused Call log: - - LocatorAssertions.ToBeFocusedAsync with timeout 5000ms - - waiting for ...
15. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_TokenMenu_ArrowDownAndEnterInsertsToken` (00:00:21.7069271)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase10_TokenMenu_ArrowDownAndEnterInsertsToken threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - wa...
16. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase11_ImageLayeringSelectionPaneZOrderAndPersist` (00:00:24.1342620)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase11_ImageLayeringSelectionPaneZOrderAndPersist threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'data-wrap-mode' '5' But was: 'BehindText' Call log: - - LocatorAsse...
17. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase11_PendingIndicatorAppearsAndDisappearsDuringSave` (00:00:18.9353074)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase11_PendingIndicatorAppearsAndDisappearsDuringSave threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms...
18. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_AfterRecoveryCanTypeAndSave` (00:00:18.8848375)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_AfterRecoveryCanTypeAndSave threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'recovery-183030029' But was: 'Editable document surface. Use Tab to move between the...
19. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_MiniToolbarBoldPreservesSelection` (00:00:21.7237706)
   - Příznak: Assert.IsTrue failed. Mini-toolbar Bold should format the selected text.
20. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_RuntimeRecoveredMessageAppearsAfterSimulatedCrash` (00:00:19.1318491)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_RuntimeRecoveredMessageAppearsAfterSimulatedCrash threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'watchdog-e2e' But was: 'Editable document surface. Use Tab to ...
21. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_TextContextMenuRunsBoldAndCommentCommands` (00:00:22.5849958)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase12_TextContextMenuRunsBoldAndCommentCommands threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - ...
22. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_MarkEditableRegionButtonEnabledOnlyWhenProtected` (00:00:21.3753957)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_MarkEditableRegionButtonEnabledOnlyWhenProtected threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be enabled Call log: - - LocatorAssertions.ToBeEnabledAsync with timeout 5000...
23. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_MarkedEditableRegionAllowsTypingButProtectedTextBlocksOutside` (00:00:48.9214144)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_MarkedEditableRegionAllowsTypingButProtectedTextBlocksOutside threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-command='markEditableRegion']").Fi...
24. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_ProtectDocumentTogglesProtectionState` (00:00:46.7183873)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_ProtectDocumentTogglesProtectionState threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-command='protectDocument']").First - - locator resolved to...
25. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_TokenRunSurvivesTypingFormattingAndReload` (00:00:22.9275243)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase13_TokenRunSurvivesTypingFormattingAndReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - ...
26. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase14_ShowBlocksAddsClassAndBlockTypeLabels` (00:00:17.9927025)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase14_ShowBlocksAddsClassAndBlockTypeLabels threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg--show-blocks' But was: 'tm-document-wysiwyg-host tm-document-wys...
27. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase14_TableCellTypingStaysInsideCell` (00:00:21.5232660)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase14_TableCellTypingStaysInsideCell threw exception: System.TimeoutException: Timeout 5000ms exceeded.
28. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase14_TableContextMenuAddsRowAndPersists` (00:00:20.9713796)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase14_TableContextMenuAddsRowAndPersists threw exception: System.TimeoutException: Timeout 5000ms exceeded.
29. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase15_OpeningDebugViewDoesNotMarkDocumentDirty` (00:00:45.4528835)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase15_OpeningDebugViewDoesNotMarkDocumentDirty threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-command='viewDocumentJson']").First - - locator resolve...
30. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase17_StructuredDocumentPersistsAndReloadsVisualMetadata` (00:00:10.2698359)
   - Příznak: Expected paragraphStyle.TextAlign to be "right" with a length of 5, but "left" has a length of 4, differs near "lef" (index 0).
31. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase18_DemoQualityGateRendersRepresentativeContent` (00:00:18.0443028)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase18_DemoQualityGateRendersRepresentativeContent threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Confidential - Page 1' But was: 'Confidential · Page 1 of 1InlineWra...
32. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase18_DesktopLayoutsHaveNoCriticalOverlap` (00:00:15.3246044)
   - Příznak: Assert.AreEqual failed. Expected:<0>. Actual:<2>. Viewport 1440x900: document host has horizontal overflow; overflowing ribbon controls: Line spacing 11.151.52, Before 06121824, After 06121824
33. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase4_MoreButton_NotVisibleAtFullDesktopWidth` (00:00:14.0290065)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase4_MoreButton_NotVisibleAtFullDesktopWidth threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be hidden Call log: - - LocatorAssertions.ToBeHiddenAsync with timeout 3000ms - - waiti...
34. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase6_FindPanel_EscapeCloses` (00:00:44.0190863)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase6_FindPanel_EscapeCloses threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-find-close']") - - locator resolved to <button title="Clo...
35. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase6_FindPanel_NextAdvancesActiveHighlight` (00:00:18.7314466)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase6_FindPanel_NextAdvancesActiveHighlight threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '1' But was: '0' Call log: - - LocatorAssertions.ToHaveCountAsync with timeout...
36. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase6_FindPanel_SearchHighlightsMatches` (00:00:18.7165083)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase6_FindPanel_SearchHighlightsMatches threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '1' But was: '0' Call log: - - LocatorAssertions.ToHaveCountAsync with timeout 500...
37. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DemoImageAfterReloadCanAcceptSideText` (00:00:51.5329877)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DemoImageAfterReloadCanAcceptSideText threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wysiwy...
38. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DemoImagePositionLeftAfterReloadEnablesSideText` (00:00:52.9218584)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DemoImagePositionLeftAfterReloadEnablesSideText threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure...
39. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DemoSecondImageBesideWrappedFirstCanBeSelected` (00:00:20.9185631)
   - Příznak: Expected layoutIssues to be empty because the demo regression requires the second image to sit beside the first wrapped image, like the reported video, but found at least one item {"first demo image must be square-wrapped for this regression"}.
40. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DesktopScreenshotShowsSquareWrapRight` (00:00:52.7817339)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_DesktopScreenshotShowsSquareWrapRight threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-drawing-anchor'][data-object-ancho...
41. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_PositionLeftFromTopBottom_EnablesSideTextWrapping` (00:00:27.5777763)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_PositionLeftFromTopBottom_EnablesSideTextWrapping threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg-image--wrap-square' But was: 'tm-wysiwyg-layout-objec...
42. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_TopBottomWrapBlocksFullBandAndUndoRedoReturnsSquare` (00:00:19.5873145)
   - Příznak: Expected beforeLine!.Rect.X to be greater than 607.0, but found 443.0 (difference of -164).
43. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_WrappedImageBeforeHeadingDoesNotUseHeadingAsSideText` (00:00:49.1375754)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase7_WrappedImageBeforeHeadingDoesNotUseHeadingAsSideText threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("h...
44. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_ExtendedContextMenu_HasRowAndColumnCommands` (00:00:21.2997652)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_ExtendedContextMenu_HasRowAndColumnCommands threw exception: System.TimeoutException: Timeout 5000ms exceeded.
45. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_InsertColumnBefore_AddsColumnLeftOfCurrent` (00:00:21.1364868)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_InsertColumnBefore_AddsColumnLeftOfCurrent threw exception: System.TimeoutException: Timeout 5000ms exceeded.
46. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_InsertRowBefore_AddsRowAboveCurrent` (00:00:21.6002261)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_InsertRowBefore_AddsRowAboveCurrent threw exception: System.TimeoutException: Timeout 5000ms exceeded.
47. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_TableGridPicker_InsertsWith3x4Dimensions` (00:00:21.5427970)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_TableGridPicker_InsertsWith3x4Dimensions threw exception: System.TimeoutException: Timeout 5000ms exceeded.
48. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_ToggleHeaderRow_ConvertsFirstRowToTh` (00:00:21.3646984)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_ToggleHeaderRow_ConvertsFirstRowToTh threw exception: System.TimeoutException: Timeout 5000ms exceeded.
49. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_ToggleHeaderRow_SaveReloadPreservesIsHeader` (00:00:21.4717077)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase8_ToggleHeaderRow_SaveReloadPreservesIsHeader threw exception: System.TimeoutException: Timeout 5000ms exceeded.
50. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_BehindTextCaretPassThroughAndInFrontSelectionAfterWrapSwitch` (00:00:24.1890765)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_BehindTextCaretPassThroughAndInFrontSelectionAfterWrapSwitch threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'data-wrap-mode' '1' But was: 'Square' Call log: - ...
51. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ImageSelectionToolbar_AppearsOnImageClick` (00:00:47.7208927)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ImageSelectionToolbar_AppearsOnImageClick threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wy...
52. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ImageSelectionToolbar_HidesAfterBodyClick` (00:00:48.1750548)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ImageSelectionToolbar_HidesAfterBodyClick threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wy...
53. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_SetImageAltText_SaveReloadPreservesAlt` (00:00:47.0467595)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_SetImageAltText_SaveReloadPreservesAlt threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wysiw...
54. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_SetImageLink_StoresLinkUrlInModel` (00:00:47.2197996)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_SetImageLink_StoresLinkUrlInModel threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wysiwyg-im...
55. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ToggleCaption_AddsFigcaption` (00:00:46.9617783)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ToggleCaption_AddsFigcaption threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wysiwyg-image[d...
56. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ToggleCaption_RemovesExistingFigcaption` (00:00:47.5691056)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Phase9_ToggleCaption_RemovesExistingFigcaption threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("figure.tm-wysi...
57. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_FieldMenuPresetPageNumbersAndSaveReload` (00:00:23.7898749)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_FieldMenuPresetPageNumbersAndSaveReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have text '1' But was: '<element(s) not found>' Call log: - - LocatorAssertions....
58. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_FooterToWrappedImageSideTextKeepsBodyFocusAfterRefresh` (00:00:22.1704638)
   - Příznak: Expected line.Rect.Width to be greater than 0.0 because typing beside a wrapped image must target visible text beside the object, not a generated sidecar paragraph, but found 0.0.
59. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_FooterTypingKeepsFocusAndHeaderFooterToolbarAfterTransactionCommit` (00:00:19.7191018)
   - Příznak: Expected focusProbe to be the same string, but they differ at index 9: ↓ (actual) "document-page-footer" "document-wysiwyg-foo…" ↑ (expected).
60. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_HeaderFooterInsertsAutomaticPageFieldAndPersists` (00:00:20.0518202)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_HeaderFooterInsertsAutomaticPageFieldAndPersists threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeou...
61. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_HeaderFooterModeShowsScopeDimsBodyAndClosesCleanly` (00:00:21.2556608)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_HeaderFooterModeShowsScopeDimsBodyAndClosesCleanly threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg-region--active' But was: 'tm-wysiwyg-page__he...
62. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_OddEvenFooterScopesRenderAndDisablingPreservesContent` (00:00:33.0156075)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_OddEvenFooterScopesRenderAndDisablingPreservesContent threw exception: Microsoft.Playwright.PlaywrightException: Locator expected text matching regex 'Odd|Liché' But was: ' footer - Primary' Call log...
63. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_PageCountAndPageNumberFieldsUseRenderedPageContext` (00:00:43.0274194)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_PageCountAndPageNumberFieldsUseRenderedPageContext threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have text '1' But was: '<element(s) not found>' Call log: - - Locator...
64. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase10_PageLayoutInspectorUpdatesGeometryUndoRedoAndPersists` (00:00:21.2220463)
   - Příznak: Expected issues to be empty, but found at least one item {"page layout inspector overflows viewport bottom"}.
65. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase11_FootnotesEndnotesReferencesToolbarAndPersistence` (00:00:27.5434751)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase11_FootnotesEndnotesReferencesToolbarAndPersistence threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeou...
66. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase11_PageBreakCreatesNewPageCaretAndPersists` (00:00:22.3698231)
   - Příznak: Expected selection.PageIndex to be 1 because the caret must be restored into the body on the newly created page, but found 0 (difference of -1).
67. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_ContextMenuPasteTruthfulDisabledStateAndDismissal` (00:00:26.5200487)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_ContextMenuPasteTruthfulDisabledStateAndDismissal threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be disabled Call log: - - LocatorAssertions.ToBeDisabledAsync with tim...
68. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PasteImageUsesProviderCapabilityAndNoUploadLeak` (00:00:24.3287132)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PasteImageUsesProviderCapabilityAndNoUploadLeak threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout...
69. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PasteIntoTableCellStaysInsideCellWithCaretAndCleanUi` (00:00:20.9264747)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PasteIntoTableCellStaysInsideCellWithCaretAndCleanUi threw exception: System.TimeoutException: Timeout 5000ms exceeded.
70. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PastePlainTextCreatesParagraphsCaretAndCleanUi` (00:00:22.3164818)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PastePlainTextCreatesParagraphsCaretAndCleanUi threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout ...
71. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PasteWordHtmlPreservesFormattingAndSanitizesDom` (00:00:22.6087457)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase12_PasteWordHtmlPreservesFormattingAndSanitizesDom threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout...
72. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase13_UndoRedoCommentAddDelete` (00:00:19.5567995)
   - Příznak: Expected selection.IsCollapsed to be False because a human-like mouse text selection should produce a visible range selection, but found True.
73. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase13_UndoRedoImageInsertAndTableEdit` (00:00:32.4980060)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase13_UndoRedoImageInsertAndTableEdit threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '0' But was: '1' Call log: - - LocatorAssertions.ToHaveCountAsync with timeou...
74. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase13_UndoRedoParagraphAlignmentAndLineSpacing` (00:00:22.2677243)
   - Příznak: Expected spaced.LineHeight to be the same string, but they differ at index 0: ↓ (actual) "21.2667px" "1.5" ↑ (expected).
75. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase13_UndoRedoRevisionAcceptReject` (00:00:36.7578455)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase13_UndoRedoRevisionAcceptReject threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - waiting...
76. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase14_F10TabNavigationAndVisibleFocus` (00:01:02.5463865)
   - Příznak: Assert.IsTrue failed. Tab navigation should continue from document content into the side panel.
77. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase14_KeyboardShortcutsFormatSaveUndoRedoAndKeepFocus` (00:00:37.7675925)
   - Příznak: Assert.IsTrue failed. Ctrl+S must save without moving focus out of the document.
78. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase15_ReadOnlyBlocksDataCommandsButKeepsViewSelectionAndPanels` (00:00:31.2154220)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase15_ReadOnlyBlocksDataCommandsButKeepsViewSelectionAndPanels threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg--show-blocks' But was: 'tm-document-wys...
79. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase16_DarkModeAndForcedColorsSmoke` (00:02:23.6179279)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase16_DarkModeAndForcedColorsSmoke threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block")
80. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase16_FloatingPopoversAndCriticalStateScreenshots` (00:00:56.6559706)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase16_FloatingPopoversAndCriticalStateScreenshots threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator("fig...
81. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase16_ResponsiveShellLayoutMatrix` (00:00:47.4951722)
   - Příznak: Expected responsiveIssues to be empty because responsive shell issues in tablet-820x1180, but found at least one item {"unexpected horizontal overflow elements: tm-wysiwyg-page, document-page-header, tm-wysiwyg-block, tm-wysiwyg-page__body tm-wysiwyg-page__body--layout, document-...
82. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase17_AutosaveFailureKeepsLocalChangesUntilSuccessfulSave` (00:00:19.7001708)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase17_AutosaveFailureKeepsLocalChangesUntilSuccessfulSave threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with tim...
83. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase17_ExportAndImportRoundtripSmoke` (00:02:10.4959390)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase17_ExportAndImportRoundtripSmoke threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block")
84. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase17_SaveReloadPersistsRepresentativeChangeClasses` (00:00:39.4810101)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase17_SaveReloadPersistsRepresentativeChangeClasses threw exception: System.TimeoutException: Timeout 5000ms exceeded.
85. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_ImageToolbarAndContextMenuProduceSameImageState` (00:00:47.6584871)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_ImageToolbarAndContextMenuProduceSameImageState threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator(...
86. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_InlineFormattingEntryPointsProduceSameModelDomCommandAndPersistence` (00:02:21.0592120)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_InlineFormattingEntryPointsProduceSameModelDomCommandAndPersistence threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-save'...
87. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_LinkCommentColorHighlightAndClearEntryPointsAreEquivalent` (00:00:43.2103393)
   - Příznak: Expected result.Href to be "https://example.com" with a length of 19, but "" has a length of 0, differs near "" (index 0).
88. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_RevisionPanelAndInlineReviewProduceSameAcceptRejectState` (00:01:31.7721454)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_RevisionPanelAndInlineReviewProduceSameAcceptRejectState threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] ....
89. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_TableToolbarAndContextMenuProduceSameTableState` (00:00:21.2844978)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase18_TableToolbarAndContextMenuProduceSameTableState threw exception: System.TimeoutException: Timeout 5000ms exceeded.
90. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TableCommands_UpdateDomSelectionUndoRedoAndPersist` (00:00:20.9311641)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TableCommands_UpdateDomSelectionUndoRedoAndPersist threw exception: System.TimeoutException: Timeout 5000ms exceeded.
91. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TableContextMenu_ContainsAllCommandsAndOpensPropertyPanels` (00:00:20.8383360)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TableContextMenu_ContainsAllCommandsAndOpensPropertyPanels threw exception: System.TimeoutException: Timeout 5000ms exceeded.
92. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TablePicker_InsertsExpectedShapesAndKeepsCaretInFirstCell` (00:00:21.2333410)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TablePicker_InsertsExpectedShapesAndKeepsCaretInFirstCell threw exception: System.TimeoutException: Timeout 5000ms exceeded.
93. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TableSelection_ClickAndDragSynchronizeContextAndVisualRange` (00:00:21.6398111)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_StrictPhase9_TableSelection_ClickAndDragSynchronizeContextAndVisualRange threw exception: System.TimeoutException: Timeout 5000ms exceeded.
94. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase3_AlignmentCommandsAreStableExactMixedAndPersistent` (00:00:40.8827332)
   - Příznak: Expected afterMouseCommand.IsCollapsed to be True, but found False.
95. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase3_LineSpacingIsStableMixedAndPersistent` (00:00:19.0128550)
   - Příznak: Expected (GetActiveSelectionParagraphStyleAsync(page)).LineHeight to be the same string, but they differ at index 0: ↓ (actual) "21.2667px" "1" ↑ (expected).
96. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase3_ListCommandsCreateToggleIndentEnterAndPersist` (00:00:19.7810018)
   - Příznak: Expected lists not to be empty.
97. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase3_SpacingAndIndentAreStableExactAndPersistent` (00:00:15.6296075)
   - Příznak: Expected styled.MarginTopPt to approximate 12.0 +/- 0.75, but 0.0 differed by 12.0.
98. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase4_MiniToolbarColorHighlightAndClearFormatting` (00:00:24.0259854)
   - Příznak: Expected issues to be empty, but found at least one item {"Tempo color picker dropdown is visually occluded by tm-wysiwyg-block"}.
99. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase4_MiniToolbarInlineCommandsMatchRibbonAndKeepSelection` (00:00:27.0423161)
   - Příznak: Expected InlineMarkIsActive(marked, command.Name) to be True because bold should be applied from the mini toolbar, but found False.
100. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase5_TextContextMenuCommentAndClipboardStates` (00:00:26.1766462)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase5_TextContextMenuCommentAndClipboardStates threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be disabled Call log: - - LocatorAssertions.ToBeDisabledAsync with timeout 5000...
101. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase5_TextContextMenuFormattingLinkClearAndPersistence` (00:00:34.6887917)
   - Příznak: Expected unexpected to be empty because only the expected floating UI should remain visible, but found at least one item { "text-context-menu (document-text-context-menu)" }.
102. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase5_TextContextMenuVisibilityItemsAndDismissal` (00:00:29.4233810)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase5_TextContextMenuVisibilityItemsAndDismissal threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be disabled Call log: - - LocatorAssertions.ToBeDisabledAsync with timeout 50...
103. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase6_AddCommentsFromRibbonMiniToolbarAndContextMenu` (00:00:50.4705393)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase6_AddCommentsFromRibbonMiniToolbarAndContextMenu threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-comment-submit']") - - loc...
104. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase6_CommentBidirectionalHighlightAndSeedAnchors` (00:00:18.2981319)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase6_CommentBidirectionalHighlightAndSeedAnchors threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Client name' But was: 'Acme s.r.o.' Call log: - - LocatorAsser...
105. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase6_CommentEditResolveDeleteAndPersistence` (00:00:19.0797194)
   - Příznak: Expected selection.IsCollapsed to be False because a human-like mouse text selection should produce a visible range selection, but found True.
106. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_AcceptRejectPanelActionsUpdateContentMarkersToolbarAndCleanup` (00:00:51.8141295)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_AcceptRejectPanelActionsUpdateContentMarkersToolbarAndCleanup threw exception: Microsoft.Playwright.PlaywrightException: Locator expected not to contain text 'phase7-reject-insert-171337801' But was:...
107. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_EnterAfterBackspaceMergeKeepsMovedTextOnCaretLine` (00:00:12.7747692)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_EnterAfterBackspaceMergeKeepsMovedTextOnCaretLine threw exception: Microsoft.Playwright.PlaywrightException: Error: Revision text ' Priority support is included during the first thirty days.' was not...
108. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_EnterAfterSeedRevisionKeepsTypingBelowRevision` (00:00:12.5527279)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_EnterAfterSeedRevisionKeepsTypingBelowRevision threw exception: Microsoft.Playwright.PlaywrightException: Error: Revision text ' Priority support is included during the first thirty days.' was not fo...
109. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_InlineRevisionReviewMenuMatchesPanelActionsAndStaysReadable` (00:00:51.2546727)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_InlineRevisionReviewMenuMatchesPanelActionsAndStaysReadable threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'...
110. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_InsertDeleteFormatRevisionsAreVisibleAndPanelSynced` (00:01:00.4426289)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_InsertDeleteFormatRevisionsAreVisibleAndPanelSynced threw exception: System.TimeoutException: Timeout 5000ms exceeded.
111. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_LiveTypingInJustifiedLayoutParagraphDoesNotWrapSegments` (00:00:20.8071535)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_LiveTypingInJustifiedLayoutParagraphDoesNotWrapSegments threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible inline contract-intro-suffix was not found. at eval (eval at evaluate...
112. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_SeedRevisionKeepsLogicalVisualOrderBeforeAccept` (00:00:12.8533765)
   - Příznak: Expected probe.RuntimeMarkerCount to be 0 because the seeded revision is already embedded in the document and must not be wrapped by a second runtime marker, but found 2 (difference of 2).
113. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_SpaceAfterSeedRevisionKeepsLogicalVisualOrder` (00:00:12.8022576)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_SpaceAfterSeedRevisionKeepsLogicalVisualOrder threw exception: Microsoft.Playwright.PlaywrightException: Error: Revision text ' Priority support is included during the first thirty days.' was not fou...
114. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingAfterSeedRevisionDoesNotPaintApprovedTextAsRevision` (00:00:12.1996879)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingAfterSeedRevisionDoesNotPaintApprovedTextAsRevision threw exception: Microsoft.Playwright.PlaywrightException: Error: Revision text ' Priority support is included during the first thirty days.'...
115. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingBesideWrappedImageDefersReflowUntilIdle` (00:00:26.6782010)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingBesideWrappedImageDefersReflowUntilIdle threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg-image--wrap-square' But was: 'tm-wysiwyg-layout-ob...
116. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingCharacterByCharacterAfterRevisionStaysPlain` (00:00:30.3789488)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingCharacterByCharacterAfterRevisionStaysPlain threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeo...
117. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingWithTrackingOffInsideRevisionDoesNotExtendRevision` (00:00:28.8191814)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase7_TypingWithTrackingOffInsideRevisionDoesNotExtendRevision threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync wit...
118. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase8_ImageAltCaptionWrapPositionResizeAndDragPersist` (00:00:47.1365057)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase8_ImageAltCaptionWrapPositionResizeAndDragPersist threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']").Locator(...
119. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase8_ImageSelectionToolbarContextMenuAndReplaceAreReadableAndClean` (00:00:48.0221483)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase8_ImageSelectionToolbarContextMenuAndReplaceAreReadableAndClean threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-hos...
120. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase8_InsertImageSourcesRenderRealImagesAndPersistMetadata` (00:00:21.9301475)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase8_InsertImageSourcesRenderRealImagesAndPersistMetadata threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'data-image-source' '0' But was: 'null' Call log: - ...
121. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase9_ImageLayoutBubbleInspectorAndContextMenuStayInSync` (00:00:27.3508564)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Strict_Phase9_ImageLayoutBubbleInspectorAndContextMenuStayInSync threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '8' But was: '0' Call log: - - LocatorAssertions.ToHaveCou...
122. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ToolbarReflectsCaretFormattingStateFromWysiwygSelection` (00:00:39.6929982)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_ToolbarReflectsCaretFormattingStateFromWysiwygSelection threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'aria-pressed' 'true' But was: 'false' Call log: - - LocatorAss...
123. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CanPasteHtmlTable` (00:00:19.4396365)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CanPasteHtmlTable threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - waiting for Locator("[da...
124. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationClientFormattingMatrixRendersOnPeer` (00:00:41.9076927)
   - Příznak: Assert.IsTrue failed. Italic formatting should render on the peer client.
125. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationClientTrackedChangesRoundTripBetweenPeers` (00:00:29.8646067)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationClientTrackedChangesRoundTripBetweenPeers threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeou...
126. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationOwnTypingIsNotDuplicatedAfterProviderEcho` (00:00:17.5553597)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationOwnTypingIsNotDuplicatedAfterProviderEcho threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'ECHO175511981' But was: 'Editable document surface. Use T...
127. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteBoldMarkKeepsFocusedSurface` (00:00:12.7829891)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteBoldMarkKeepsFocusedSurface threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <anonymous>:42:15...
128. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteImageUpdateRendersWithoutFullReload` (00:00:19.1745449)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteImageUpdateRendersWithoutFullReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'style' matching regex '260px' But was: 'null' Call log:...
129. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteRevisionReviewClearsDecorationsWithoutReload` (00:00:11.1669410)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteRevisionReviewClearsDecorationsWithoutReload threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), ...
130. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTableCellEditDoesNotResetCaret` (00:00:15.6268775)
   - Příznak: Assert.AreEqual failed. Expected:<contract-intro>. Actual:<>. Remote table cell edit must not move caret to another block.
131. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTextDoesNotResetCaretToDocumentStart` (00:00:29.7216347)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTextDoesNotResetCaretToDocumentStart threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'CARET180145854' But was: 'Editable document surface. Use...
132. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTextKeepsFocusedSurface` (00:00:30.0597669)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTextKeepsFocusedSurface threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'REMOTE180116389' But was: 'Editable document surface. Use Tab to move...
133. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTrackedDeletionShowsDeletionSpanAndPanel` (00:00:10.7030542)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTrackedDeletionShowsDeletionSpanAndPanel threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <ano...
134. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTrackedInsertionShowsSpanAndPanelWithoutFocusLoss` (00:00:12.4573078)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteTrackedInsertionShowsSpanAndPanelWithoutFocusLoss threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:...
135. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteUpdateDuringFastTypingDoesNotBatchJump` (00:00:26.0251672)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationRemoteUpdateDuringFastTypingDoesNotBatchJump threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'KFAST17572246121e10d4afabd43dea3f03b7bbb90e6c1' But wa...
136. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationTwoClientsDifferentLinesKeepLocalCaret` (00:00:25.4150102)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationTwoClientsDifferentLinesKeepLocalCaret threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline text node was not found in the requested block. at eval (eval at evalua...
137. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationTwoClientsSameParagraphConvergeDeterministically` (00:00:28.2090944)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_CollaborationTwoClientsSameParagraphConvergeDeterministically threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'A175702404' But was: 'Editable document surface. U...
138. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_DroppedImagePersistsAfterSaveAndReload` (00:00:19.0690712)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_DroppedImagePersistsAfterSaveAndReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - wai...
139. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_FloatingImageDragKeepsTextFlowAndSelectionStable` (00:00:25.4876319)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_FloatingImageDragKeepsTextFlowAndSelectionStable threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg-image--floating' But was: 'tm-wysiwyg-layout-object t...
140. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_ImageAssetRendersAsImageObject` (00:00:23.2326020)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_ImageAssetRendersAsImageObject threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 10000ms - - waiting fo...
141. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_ImageContextMenuDeleteRemovesImageBlock` (00:00:13.2125103)
   - Příznak: Expected before.Blocks Tempo.Blazor.DocumentEditor.Models.DocumentBlock { Content = Tempo.Blazor.DocumentEditor.Models.HeadingBlockContent { Inlines = Tempo.Blazor.DocumentEditor.Models.TextRun { { Id = "contract-heading-text", Marks = { Maximum recursion depth of 5 was reached. ...
142. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_InlineImageDragMovePersistsAfterSaveAndReload` (00:00:19.9929337)
   - Příznak: Assert.IsTrue failed. Dragging should move the image later in the document. Before=-1, after=-1.
143. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_InlineRevisionContextAcceptsSameAsPanel` (00:00:24.5521557)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_InlineRevisionContextAcceptsSameAsPanel threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - wa...
144. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteDeletePreservesAdjacentRevisionSpan` (00:00:11.0084646)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteDeletePreservesAdjacentRevisionSpan threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <anonymous>:42:15) a...
145. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteInsertAfterCaretDoesNotMoveSelection` (00:00:10.7791657)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteInsertAfterCaretDoesNotMoveSelection threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <anonymous>:42:15) ...
146. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteInsertBeforeCaretTransformsSelection` (00:00:10.9049112)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteInsertBeforeCaretTransformsSelection threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <anonymous>:42:15) ...
147. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchAppliesAndPartiallyRemovesFormattingRange` (00:00:11.0802719)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchAppliesAndPartiallyRemovesFormattingRange threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), ...
148. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchAppliesTextInOrderAndIdempotently` (00:00:10.5604254)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchAppliesTextInOrderAndIdempotently threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <anonymo...
149. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchOrdersConcurrentSameOffsetByStableId` (00:00:10.8679391)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchOrdersConcurrentSameOffsetByStableId threw exception: Microsoft.Playwright.PlaywrightException: Error: Editable inline target was not found. at eval (eval at evaluate (:234:30), <anon...
150. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchPatchesBlocksAndImageInDom` (00:00:15.8666146)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_JsRemoteOperationBatchPatchesBlocksAndImageInDom threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Remote paragraph from batch' But was: '<element(s) not found>' ...
151. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_LineSpacingAndIndentAreVisibleAndKeepCaretStable` (00:00:29.3192337)
   - Příznak: Expected styled.LineHeight to be the same string, but they differ at index 0: ↓ (actual) "21.2667px" "1.5" ↑ (expected).
152. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_PasteGoogleSheetsTsvCreatesTable` (00:00:20.8293404)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_PasteGoogleSheetsTsvCreatesTable threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - waiting f...
153. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_PastePlainTextCreatesParagraphs` (00:00:17.5443664)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_PastePlainTextCreatesParagraphs threw exception: Microsoft.Playwright.PlaywrightException: Error: strict mode violation: Locator("[data-testid='document-wysiwyg-host']").Locator(".tm-wysiwyg-page__body p")...
154. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_PasteWordHtmlPreservesBoldAndParagraphs` (00:00:21.1874657)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_PasteWordHtmlPreservesBoldAndParagraphs threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - wa...
155. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_Phase1TypingKeepsCaretAfterInsertedCharacter` (00:00:18.5463248)
   - Příznak: Assert.AreEqual failed. Expected:<contract-intro-prefix>. Actual:<>. Local typing must not move the caret to another inline.
156. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_ReviewNoMarkupDoesNotDestroyPendingRevisions` (00:00:25.0600319)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_ReviewNoMarkupDoesNotDestroyPendingRevisions threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'data-review-display-mode' 'NoMarkup' But was: 'null' Call log: - ...
157. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_SelectedWordCanCombineFormattingWithoutChangingSurroundings` (00:00:32.8648654)
   - Příznak: Assert.IsTrue failed. Selected text should be bold.
158. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_SpaceKeyMovesCaretImmediatelyBeforeNextCharacter` (00:00:18.1441291)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_SpaceKeyMovesCaretImmediatelyBeforeNextCharacter threw exception: Microsoft.Playwright.PlaywrightException: Error: Caret visual probe requires a collapsed selection inside an inline. at eval (eval at evalu...
159. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_TrackChangesEnterKeepsPendingRevisionPanel` (00:00:20.1099845)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_TrackChangesEnterKeepsPendingRevisionPanel threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'ENTER172812519' But was: '<element(s) not found>' Call log: - - Locat...
160. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_TrackChangesShowsInlineRevisionAndAcceptsIt` (00:00:29.2537740)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_TrackChangesShowsInlineRevisionAndAcceptsIt threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '0' But was: '1' Call log: - - LocatorAssertions.ToHaveCountAsync with ...
161. `Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_UndoAfterMultiBlockPasteRemovesAllPastedBlocks` (00:00:21.3505873)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_Wysiwyg_UndoAfterMultiBlockPasteRemovesAllPastedBlocks threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms...

### `Tempo.Blazor.E2E.DocumentEditorImageOnlyOfficeParityE2ETests`

162. `Tempo.Blazor.E2E.DocumentEditorImageOnlyOfficeParityE2ETests.ImageOnlyOfficeParity_InsertImageAtCaretCreatesDrawingRunNotTopLevelBlock` (00:00:26.3534677)
   - Příznak: Assert.AreEqual failed. Expected:<0>. Actual:<1>. Undo must remove the inserted drawing run in one step.

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeCommentTests`

163. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeCommentTests.Phase10_InsertBeforeCommentKeepsHighlightOnOriginalText` (00:00:29.5447779)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeCommentTests.Phase10_InsertBeforeCommentKeepsHighlightOnOriginalText threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 10000ms...

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests`

164. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase11_ClickingImageReportsImageRuntimeSelection` (00:00:46.0171030)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase11_ClickingImageReportsImageRuntimeSelection threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[da...
165. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase11_ImageSnapshotKeepsNaturalAndDisplaySize` (00:00:45.3103958)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase11_ImageSnapshotKeepsNaturalAndDisplaySize threw exception: System.TimeoutException: Timeout 30000ms exceeded.
166. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_ArrowKeysMoveSelectedImageAndUndoRedoRestoresPosition` (00:00:45.9512236)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_ArrowKeysMoveSelectedImageAndUndoRedoRestoresPosition threw exception: System.TimeoutException: Timeout 30000ms exceeded.
167. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_DeleteRemovesKeyboardSelectedImage` (00:00:45.5561125)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_DeleteRemovesKeyboardSelectedImage threw exception: System.TimeoutException: Timeout 30000ms exceeded.
168. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_KeyboardFocusOpensLayoutBubbleAndChangesWrapMode` (00:00:45.5156932)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_KeyboardFocusOpensLayoutBubbleAndChangesWrapMode threw exception: System.TimeoutException: Timeout 30000ms exceeded.
169. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_MissingAltWarningAndDecorativeStateAreExposed` (00:00:19.0250141)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_MissingAltWarningAndDecorativeStateAreExposed threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'data-image-alt-warning' 'true' But was: 'null' Call log: - - Locator...
170. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_ShiftF10OpensImageContextMenuAndEscapeReturnsFocus` (00:00:46.2571651)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeImageTests.Phase16_ShiftF10OpensImageContextMenuAndEscapeReturnsFocus threw exception: System.TimeoutException: Timeout 30000ms exceeded.

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests`

171. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase16_LongTypingRecordsLatencyAndDoesNotRenderThroughBlazor` (00:00:10.7992198)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase16_LongTypingRecordsLatencyAndDoesNotRenderThroughBlazor threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonymous>:10:1...
172. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase4_CompositionPreviewSurvivesRuntimeSelectionRefreshAndCommitsOnce` (00:00:10.8839176)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase4_CompositionPreviewSurvivesRuntimeSelectionRefreshAndCommitsOnce threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonym...
173. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_BackspaceAtParagraphStartMergesWithPreviousParagraph` (00:00:10.8766440)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_BackspaceAtParagraphStartMergesWithPreviousParagraph threw exception: Microsoft.Playwright.PlaywrightException: Error: Mergeable top-level text block was not found. at eval (eval at evaluate (:234:30), <anonymo...
174. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_EnterCreatesVisibleEmptyParagraphImmediately` (00:00:11.1572405)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_EnterCreatesVisibleEmptyParagraphImmediately threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonymous>:10:15) at Util...
175. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_EnterSplitsParagraphAndContinuesInNewBlock` (00:00:10.9531390)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_EnterSplitsParagraphAndContinuesInNewBlock threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonymous>:10:15) at Utilit...
176. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_FastTypingUsesJsOwnedInputWithoutFullRender` (00:00:10.8755273)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_FastTypingUsesJsOwnedInputWithoutFullRender threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonymous>:10:15) at Utili...
177. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_ShiftEnterCreatesSoftBreakAndKeepsCurrentBlock` (00:00:11.2062379)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_ShiftEnterCreatesSoftBreakAndKeepsCurrentBlock threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonymous>:10:15) at Ut...
178. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_ShiftEnterMovesCaretToVisibleSoftBreakLineImmediately` (00:00:10.8675005)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeInputTests.Phase6_ShiftEnterMovesCaretToVisibleSoftBreakLineImmediately threw exception: Microsoft.Playwright.PlaywrightException: Error: Visible text block 0 was not found. at eval (eval at evaluate (:234:30), <anonymous>:10:15...

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRegionTests`

179. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRegionTests.Phase13_FooterEditPersistsThroughSaveReload` (00:00:27.1224454)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeRegionTests.Phase13_FooterEditPersistsThroughSaveReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Saved' But was: 'Autosaved' Call log: - - LocatorAssertions.ToContainTextAsync ...

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests`

180. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests.Phase16_RemoteOperationOnVirtualPageUpdatesModelWithoutRenderingPage` (00:00:11.6208537)
   - Příznak: Assert.IsTrue failed. The setup document must be virtualized.
181. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests.Phase16_SelectionOnVirtualPageRestoresAfterScrollingBack` (00:00:42.6593184)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests.Phase16_SelectionOnVirtualPageRestoresAfterScrollingBack threw exception: System.TimeoutException: Timeout 30000ms exceeded.
182. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests.Phase16_ThirtyPageDocumentUsesVirtualizedVisiblePages` (00:00:41.1549868)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests.Phase16_ThirtyPageDocumentUsesVirtualizedVisiblePages threw exception: System.TimeoutException: Timeout 30000ms exceeded.
183. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRenderLoopTests.Phase3_RuntimeRenderLoop_RendersVisibleDocumentTextAndStableNodeIds` (00:00:11.3415147)
   - Příznak: Assert.IsTrue failed. Runtime render loop must stamp stable node ids for selection and incremental rendering.

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests`

184. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests.Phase9_AcceptInsertionIsUndoableAndRestoresPendingRevision` (00:00:37.5120068)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests.Phase9_AcceptInsertionIsUndoableAndRestoresPendingRevision threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 100...
185. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests.Phase9_RejectInsertionRemovesInsertedText` (00:00:32.7813773)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests.Phase9_RejectInsertionRemovesInsertedText threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '0' But was: '1' Call log: - - LocatorAssertions.ToHaveCountAsync with timeout 500...
186. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests.Phase9_TrackChangesDeleteKeepsDeletedTextAsRedStrike` (00:00:10.4326660)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeRevisionTests.Phase9_TrackChangesDeleteKeepsDeletedTextAsRedStrike threw exception: Microsoft.Playwright.PlaywrightException: Error: No selectable text run found. at eval (eval at evaluate (:234:30), <anonymous>:4:22) at Utility...

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests`

187. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests.Phase8_AutosaveUsesJsCanonicalSnapshotAndMarksRuntimeSaved` (00:01:08.4803985)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests.Phase8_AutosaveUsesJsCanonicalSnapshotAndMarksRuntimeSaved threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwy...
188. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests.Phase8_ExplicitSaveUsesJsCanonicalSnapshotAndMarksRuntimeSaved` (00:00:29.6499713)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests.Phase8_ExplicitSaveUsesJsCanonicalSnapshotAndMarksRuntimeSaved threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Saved' But was: 'Autosaved' Call log: - - LocatorAsser...
189. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSaveBoundaryTests.Phase8_SaveFailureKeepsRuntimeDirty` (00:00:18.7932693)
   - Příznak: Assert.IsTrue failed. Failed save must not acknowledge the JS runtime dirty state.

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSelectionTests`

190. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeSelectionTests.Phase4_SelectingMixedBoldPlainTextReportsMixedToolbarState` (00:00:10.9488405)
   - Příznak: Assert.IsTrue failed. The seeded contract intro inlines must be selectable as one range.

### `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests`

191. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_AddRowBeforeAndUndoAreJsOwned` (00:00:20.2574947)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_AddRowBeforeAndUndoAreJsOwned threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '2' But was: '3' Call log: - - LocatorAssertions.ToHaveCountAsync with timeout 5000ms - -...
192. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_GridPickerKeyboardInsertsFourByFiveTable` (00:00:25.1025129)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_GridPickerKeyboardInsertsFourByFiveTable threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '4' But was: '3' Call log: - - LocatorAssertions.ToHaveCountAsync with timeout...
193. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_InsertTableFocusesFirstCellAndTypingStaysInsideCell` (00:00:17.6281022)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_InsertTableFocusesFirstCellAndTypingStaysInsideCell threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'phase12-cell-185459550' But was: 'Item' Call log: - - LocatorAss...
194. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_SaveReloadKeepsTableContentAndCellMetadata` (00:00:31.2601009)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_SaveReloadKeepsTableContentAndCellMetadata threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'phase12-save-185705015' But was: 'Item' Call log: - - LocatorAssertions.T...
195. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_TabMovesCaretToNextCell` (00:00:18.2132937)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_TabMovesCaretToNextCell threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'A185517017' But was: 'Item' Call log: - - LocatorAssertions.ToContainTextAsync with timeout ...
196. `Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_TableAndCellPropertiesPersistAfterSaveReload` (00:00:31.0813349)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorJsRuntimeTableTests.Phase12_TableAndCellPropertiesPersistAfterSaveReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'phase12-props-185634149' But was: 'Item' Call log: - - LocatorAssertion...

### `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests`

197. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_FloatingToolbar_OnlyAppearsForRealTextSelection` (00:00:21.1156026)
   - Příznak: Human mouse text selection failed. Expected 'exact target phrase' in block 'onlyoffice-formatting-paragraph'. Target: {"blockId":"onlyoffice-formatting-paragraph","start":25,"end":44,"expectedText":"exact target phrase","rect":{"x":512.515625,"y":377.453125,"width":133.234375,"he...
198. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_PerformanceBudget_SpaceEnterFormattingTrackChangesAndMixedMarkupStayMeasured` (00:00:24.8314086)
   - Příznak: Assert.IsTrue failed. selection -> toolbar state exceeded the p95 budget. Histogram=count=6, p50=42.1ms, p95=559.5ms, max=559.5ms, budget=200ms
199. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_ReviewedRevisions_DoNotReturnAfterSaveReload` (00:00:51.5774275)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_ReviewedRevisions_DoNotReturnAfterSaveReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected not to contain text 'phase16-reject-190752370' But was: 'Editable document...
200. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonCollapsedCaretFormatting_AffectsNextTypedText` (00:00:27.4611949)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonCollapsedCaretFormatting_AffectsNextTypedText threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with...
201. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonHighlightClear_RemovesHighlightAndKeepsSelection` (00:00:14.7899732)
   - Příznak: Toolbar pointerdown for 'document-highlight-color-trigger' destroyed the selected text. Before=text='exact target phrase', collapsed=False, start=onlyoffice-formatting-paragraph:25, end=onlyoffice-formatting-paragraph:44, runtimeSelection=True, runtimeToken=True; PointerDown=text...
202. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonHighlight_AppliesToSelectionAndUpdatesSwatch` (00:00:15.3896158)
   - Příznak: Toolbar pointerdown for 'document-highlight-color-trigger' destroyed the selected text. Before=text='exact target phrase', collapsed=False, start=onlyoffice-formatting-paragraph:25, end=onlyoffice-formatting-paragraph:44, runtimeSelection=True, runtimeToken=True; PointerDown=text...
203. `Tempo.Blazor.E2E.DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonTextColor_AppliesToSelectionAndUpdatesSwatch` (00:00:14.9596682)
   - Příznak: Toolbar pointerdown for 'document-font-color-trigger' destroyed the selected text. Before=text='exact target phrase', collapsed=False, start=onlyoffice-formatting-paragraph:25, end=onlyoffice-formatting-paragraph:44, runtimeSelection=True, runtimeToken=True; PointerDown=text='', ...

### `Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests`

204. `Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests.Phase10_ClipboardImagePastePersistsAfterSaveAndReload` (00:00:20.7159154)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests.Phase10_ClipboardImagePastePersistsAfterSaveAndReload threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeou...
205. `Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests.Phase10_GoogleDocsHeadingSheetsTableAndUrlPaste` (00:00:25.8629820)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests.Phase10_GoogleDocsHeadingSheetsTableAndUrlPaste threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 1000...
206. `Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests.Phase10_WordListPasteShowsReportAndUndoesAsSingleTransaction` (00:00:24.6957064)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase10ClipboardPipelineE2ETests.Phase10_WordListPasteShowsReportAndUndoesAsSingleTransaction threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with...

### `Tempo.Blazor.E2E.DocumentEditorPhase11ImageUxE2ETests`

207. `Tempo.Blazor.E2E.DocumentEditorPhase11ImageUxE2ETests.Phase11_ImageInspectorUpdatesAltAndWrap` (00:00:50.7109513)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase11ImageUxE2ETests.Phase11_ImageInspectorUpdatesAltAndWrap threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image").Last - ...

### `Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests`

208. `Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.AutocompleteMenu_OnMobileViewport_StaysInsideViewport` (00:00:23.0792982)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.AutocompleteMenu_OnMobileViewport_StaysInsideViewport threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 100...
209. `Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.MentionTrigger_TypedInEditor_InsertsSelectedMentionAndRemovesQuery` (00:00:22.4662316)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.MentionTrigger_TypedInEditor_InsertsSelectedMentionAndRemovesQuery threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-autocomp...
210. `Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.SlashTableCommand_TypedInEditor_InsertsTableAndRemovesQuery` (00:00:22.8888947)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.SlashTableCommand_TypedInEditor_InsertsTableAndRemovesQuery threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-autocomplete-po...
211. `Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.TokenTrigger_TypedInEditor_InsertsSelectedTokenAndRemovesQuery` (00:00:23.1956474)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase14AutocompleteE2ETests.TokenTrigger_TypedInEditor_InsertsSelectedTokenAndRemovesQuery threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-token-popove...

### `Tempo.Blazor.E2E.DocumentEditorPhase15ImageLayoutStressE2ETests`

212. `Tempo.Blazor.E2E.DocumentEditorPhase15ImageLayoutStressE2ETests.Phase15_ImageLayoutStress_KeepsSelectionUndoRedoAndConsoleClean` (00:01:08.6521673)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15ImageLayoutStressE2ETests.Phase15_ImageLayoutStress_KeepsSelectionUndoRedoAndConsoleClean threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']...

### `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests`

213. `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_EmptyBodyTableCellHeaderAndFooter_DoNotCollapseAndAcceptTyping` (00:00:16.3964287)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_EmptyBodyTableCellHeaderAndFooter_DoNotCollapseAndAcceptTyping threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with ...
214. `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_NonPrintingCharactersToggle_ShowsParagraphAndSpaceMarks` (00:00:21.1228583)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_NonPrintingCharactersToggle_ShowsParagraphAndSpaceMarks threw exception: Microsoft.Playwright.PlaywrightException: Locator expected matching regex 'tm-wysiwyg--show-nonprinting' But was: 'tm-document-wysiwyg...
215. `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_OutlinePanel_HighlightsActiveHeadingAfterScroll` (00:00:19.6337741)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_OutlinePanel_HighlightsActiveHeadingAfterScroll threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'data-active' 'true' But was: 'false' Call log: - - LocatorAssert...
216. `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_PageBreak_CanBeSelectedAndDeletedWithKeyboard` (00:00:17.9082981)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_PageBreak_CanBeSelectedAndDeletedWithKeyboard threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - ...
217. `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_PageNavigator_NavigatesToSecondPageAfterPageBreak` (00:00:17.6760078)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_PageNavigator_NavigatesToSecondPageAfterPageBreak threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '1' But was: '0' Call log: - - LocatorAssertions.ToHaveCountAsync w...
218. `Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_PageOverflowWarningAction_InsertsPageBreak` (00:00:21.6745949)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase15PageUxE2ETests.Phase15_PageOverflowWarningAction_InsertsPageBreak threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 10000ms - - ...

### `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests`

219. `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_Autosave_ShowsWaitingSavingAndSynchronizedStatus` (00:01:08.6611945)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_Autosave_ShowsWaitingSavingAndSynchronizedStatus threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-blo...
220. `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_BeforeUnloadGuard_DebugStateTracksPendingWork` (00:01:08.5947153)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_BeforeUnloadGuard_DebugStateTracksPendingWork threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block"...
221. `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_ManualSave_StillPersistsAndClearsDirtyState` (00:01:08.6210043)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_ManualSave_StillPersistsAndClearsDirtyState threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block")
222. `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_ProviderErrorRetry_ThenSuccess` (00:01:09.4425463)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_ProviderErrorRetry_ThenSuccess threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block")
223. `Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_TypingDuringSave_TriggersSecondSave` (00:01:08.5936540)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase16AutosaveE2ETests.Phase16_TypingDuringSave_TriggersSecondSave threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block")

### `Tempo.Blazor.E2E.DocumentEditorPhase18DebugE2ETests`

224. `Tempo.Blazor.E2E.DocumentEditorPhase18DebugE2ETests.Phase18_ClipboardDebugView_ShowsRawNormalizedAndWarnings` (00:00:46.1976864)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase18DebugE2ETests.Phase18_ClipboardDebugView_ShowsRawNormalizedAndWarnings threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-view-clipboard-html']") - - locat...

### `Tempo.Blazor.E2E.DocumentEditorPhase19ImportExportE2ETests`

225. `Tempo.Blazor.E2E.DocumentEditorPhase19ImportExportE2ETests.Phase19_ExportDocxImportDocxAndExportPdfSmoke` (00:00:00.1693065)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase19ImportExportE2ETests.Phase19_ExportDocxImportDocxAndExportPdfSmoke threw exception: System.InvalidOperationException: Sequence contains no elements
226. `Tempo.Blazor.E2E.DocumentEditorPhase19ImportExportE2ETests.Phase19_SaveReload_ImageAndTablePropertiesPersist` (00:00:17.4939993)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase19ImportExportE2ETests.Phase19_SaveReload_ImageAndTablePropertiesPersist threw exception: System.InvalidOperationException: Sequence contains no elements

### `Tempo.Blazor.E2E.DocumentEditorPhase20PerformanceE2ETests`

227. `Tempo.Blazor.E2E.DocumentEditorPhase20PerformanceE2ETests.Phase20_LayoutStabilitySmoke_CoversDesktopMobileCompactFloatingAndInspectors` (00:00:22.6255416)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase20PerformanceE2ETests.Phase20_LayoutStabilitySmoke_CoversDesktopMobileCompactFloatingAndInspectors threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisible...
228. `Tempo.Blazor.E2E.DocumentEditorPhase20PerformanceE2ETests.Phase20_LongDocumentVirtualizationSmoke_CoversNavigatorSearchAndCommentRail` (00:00:16.9426297)
   - Příznak: Expected metrics.TotalPages to be greater than 20, but found 3 (difference of -17).
229. `Tempo.Blazor.E2E.DocumentEditorPhase20PerformanceE2ETests.Phase20_TypingPerformanceSmoke_CoversCommentsSearchTrackChangesAndTableCell` (00:00:31.6665688)
   - Příznak: Expected metrics.FullRenderCount to be 0 because typing should stay inside the JS-owned surface, but found 1 (difference of 1).

### `Tempo.Blazor.E2E.DocumentEditorPhase21AccessibilityE2ETests`

230. `Tempo.Blazor.E2E.DocumentEditorPhase21AccessibilityE2ETests.Phase21_LiveRegion_AnnouncesFindSaveAndAutosaveError` (00:01:48.5582443)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase21AccessibilityE2ETests.Phase21_LiveRegion_AnnouncesFindSaveAndAutosaveError threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bl...
231. `Tempo.Blazor.E2E.DocumentEditorPhase21AccessibilityE2ETests.Phase21_SelectedImageChrome_HasAccessibleLabelsToolbarAndDeleteCommand` (00:00:18.6042928)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase21AccessibilityE2ETests.Phase21_SelectedImageChrome_HasAccessibleLabelsToolbarAndDeleteCommand threw exception: System.TimeoutException: Timeout 5000ms exceeded.

### `Tempo.Blazor.E2E.DocumentEditorPhase22DemoDocsE2ETests`

232. `Tempo.Blazor.E2E.DocumentEditorPhase22DemoDocsE2ETests.Phase22_TableReviewPasteAndAutosaveScenarios_AreUsable` (00:00:44.6806220)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase22DemoDocsE2ETests.Phase22_TableReviewPasteAndAutosaveScenarios_AreUsable threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for GetByTestId("document-editor-review-scenario") - - locator resol...

### `Tempo.Blazor.E2E.DocumentEditorPhase3CommandRegistryE2ETests`

233. `Tempo.Blazor.E2E.DocumentEditorPhase3CommandRegistryE2ETests.Phase3_CommandPalette_SearchesAndExecutesBold` (00:00:11.2529472)
   - Příznak: Assert.IsTrue failed.
234. `Tempo.Blazor.E2E.DocumentEditorPhase3CommandRegistryE2ETests.Phase3_KeyboardShortcuts_RunThroughCommandRegistry` (00:00:11.1900746)
   - Příznak: Assert.IsTrue failed.

### `Tempo.Blazor.E2E.DocumentEditorPhase5RuntimeModularizationE2ETests`

235. `Tempo.Blazor.E2E.DocumentEditorPhase5RuntimeModularizationE2ETests.Phase5_RuntimeModulesKeepCoreEditingTableImageCommentsAndRevisionsWorking` (00:00:10.9781915)
   - Příznak: CollectionAssert.AreEquivalent failed. The number of elements in the collections do not match. Expected:<12>. Actual:<14>.

### `Tempo.Blazor.E2E.DocumentEditorPhase6SchemaPolicyE2ETests`

236. `Tempo.Blazor.E2E.DocumentEditorPhase6SchemaPolicyE2ETests.Phase6_PageBreakWorksInBodyButIsDisabledInHeader` (00:00:17.7435939)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase6SchemaPolicyE2ETests.Phase6_PageBreakWorksInBodyButIsDisabledInHeader threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '1' But was: '0' Call log: - - LocatorAssertions.ToHaveCountAsync with ...

### `Tempo.Blazor.E2E.DocumentEditorPhase7MarkerStoreE2ETests`

237. `Tempo.Blazor.E2E.DocumentEditorPhase7MarkerStoreE2ETests.Phase7_FindPanelPublishesSearchMarkersToRuntimeStore` (00:00:18.6766335)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase7MarkerStoreE2ETests.Phase7_FindPanelPublishesSearchMarkersToRuntimeStore threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '1' But was: '0' Call log: - - LocatorAssertions.ToHaveCountAsync wi...
238. `Tempo.Blazor.E2E.DocumentEditorPhase7MarkerStoreE2ETests.Phase7_RuntimeBridgeTracksRemoteCursorAndRestrictedRegionMarkers` (00:00:16.2509284)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase7MarkerStoreE2ETests.Phase7_RuntimeBridgeTracksRemoteCursorAndRestrictedRegionMarkers threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have count '1' But was: '0' Call log: - - LocatorAssertions.ToHaveC...

### `Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests`

239. `Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests.Phase8_FindUpdatesEditorLiveRegion` (00:00:18.8033322)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests.Phase8_FindUpdatesEditorLiveRegion threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text '1 of' But was: 'Resize image selected' Call log: - - LocatorAssertions.ToContainT...
240. `Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests.Phase8_MiniToolbarStaysInsideDesktopViewport` (00:00:16.2471461)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests.Phase8_MiniToolbarStaysInsideDesktopViewport threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - w...
241. `Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests.Phase8_MiniToolbarStaysInsideNarrowViewport` (00:00:15.5652380)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase8FloatingFocusE2ETests.Phase8_MiniToolbarStaysInsideNarrowViewport threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - wa...

### `Tempo.Blazor.E2E.DocumentEditorPhase9FindReplaceE2ETests`

242. `Tempo.Blazor.E2E.DocumentEditorPhase9FindReplaceE2ETests.Phase9_ReplaceAll_UsesSingleRuntimeUndoBatchAndClearsSearchMarkers` (00:00:15.1997364)
   - Příznak: Assert.AreEqual failed. Expected:<Replace all>. Actual:<>.
243. `Tempo.Blazor.E2E.DocumentEditorPhase9FindReplaceE2ETests.Phase9_ReplaceOne_WithTrackChangesCreatesReviewableRevisions` (00:00:45.1941915)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorPhase9FindReplaceE2ETests.Phase9_ReplaceOne_WithTrackChangesCreatesReviewableRevisions threw exception: System.TimeoutException: Timeout 30000ms exceeded.

### `Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests`

244. `Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.PerformanceGuard_FastTypingBurstDoesNotTriggerFullRendersOrLargeAverageDelay` (00:01:08.7250159)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.PerformanceGuard_FastTypingBurstDoesNotTriggerFullRendersOrLargeAverageDelay threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] ....
245. `Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.PerformanceGuard_ImagePanelAndRevisionsStayInteractiveAfterQuickTyping` (00:01:08.4896854)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.PerformanceGuard_ImagePanelAndRevisionsStayInteractiveAfterQuickTyping threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wys...
246. `Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.PerformanceGuard_RemotePatchOutsideActiveBlockDoesNotFullRender` (00:01:08.1694179)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.PerformanceGuard_RemotePatchOutsideActiveBlockDoesNotFullRender threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bl...
247. `Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.QualitySmoke_CoversCoreEditingFormattingRevisionsImagesPanelsAndHeaderFooter` (00:01:08.4008157)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorQualitySmokeTests.QualitySmoke_CoversCoreEditingFormattingRevisionsImagesPanelsAndHeaderFooter threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] ....

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryE2ETests`

248. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryE2ETests.Recovery_DocumentShowsHeadersFootersCommentsAndRevisions` (00:01:08.0512324)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryE2ETests.Recovery_DocumentShowsHeadersFootersCommentsAndRevisions threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests`

249. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests.RecoveryTyping_HoldKeyPaintsProgressivelyWhileInteropIsThrottled` (00:01:08.1824865)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests.RecoveryTyping_HoldKeyPaintsProgressivelyWhileInteropIsThrottled threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host...
250. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests.RecoveryTyping_PerformanceStatsProveNoFullRenderPath` (00:01:08.5935233)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests.RecoveryTyping_PerformanceStatsProveNoFullRenderPath threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiw...
251. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests.RecoveryTyping_SpaceEnterAndNextCharacterUsePartialPatches` (00:01:08.2797560)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase10E2ETests.RecoveryTyping_SpaceEnterAndNextCharacterUsePartialPatches threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase11E2ETests`

252. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase11E2ETests.DefaultContractDemo_ImageSourcesAndWrapModesStayHonest` (00:00:00.0148027)
   - Příznak: Expected String(urlImage, "Url") to be "/document-editor-evidence.svg", but found <null>.
253. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase11E2ETests.DefaultContractDemo_ReloadAndInspectorDoNotMaskProviderVsUrlImages` (00:00:51.7250141)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase11E2ETests.DefaultContractDemo_ReloadAndInspectorDoNotMaskProviderVsUrlImages threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-ho...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests`

254. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests.RecoveryCommentsAndRevisions_UseDistinctReadableStates` (00:01:08.2260155)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests.RecoveryCommentsAndRevisions_UseDistinctReadableStates threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wys...
255. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests.RecoveryImageUx_UsesCompactIconSegmentsAndDocumentStyleHandles` (00:01:08.4580057)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests.RecoveryImageUx_UsesCompactIconSegmentsAndDocumentStyleHandles threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']...
256. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests.RecoveryTextSelection_UxHighlightToolbarAndColorPopoverStayReadable` (00:01:07.9038158)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase12E2ETests.RecoveryTextSelection_UxHighlightToolbarAndColorPopoverStayReadable threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-h...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests`

257. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_Comments_MarkersPanelBidirectionalSync` (00:01:08.3964042)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_Comments_MarkersPanelBidirectionalSync threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bl...
258. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_FastTyping_IsNotBatchedIntoLargeChunks` (00:01:08.2038525)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_FastTyping_IsNotBatchedIntoLargeChunks threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bl...
259. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_HeaderFooter_VisibleEditableAndPersistent` (00:00:34.1012446)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_HeaderFooter_VisibleEditableAndPersistent threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Saved' But was: 'Autosaved' Call log: - - LocatorAssertions...
260. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_ImageProperties_AllFieldsApplyWithDebounce` (00:01:07.7923681)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_ImageProperties_AllFieldsApplyWithDebounce threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwy...
261. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_ImageSelection_ShowsToolbarAndPropertiesPanel` (00:01:08.3218817)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_ImageSelection_ShowsToolbarAndPropertiesPanel threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wys...
262. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_P1RegressionSuite_MarkersPopoversSourceUiSideTabsTableAndMobileSmoke` (00:01:08.2465188)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_P1RegressionSuite_MarkersPopoversSourceUiSideTabsTableAndMobileSmoke threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document...
263. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_Revisions_MarkersPanelAcceptRejectSync` (00:01:08.3613910)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_Revisions_MarkersPanelAcceptRejectSync threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bl...
264. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_SpaceAndEnter_AppearImmediately` (00:01:08.5556818)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_SpaceAndEnter_AppearImmediately threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block")
265. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_TextSelection_ShowsFloatingToolbarAndAppliesFormatting` (00:01:08.5437035)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase13E2ETests.Recovery_TextSelection_ShowsFloatingToolbarAndAppliesFormatting threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests`

266. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_EnterKey_SplitsParagraphBeforeNextCharacter` (00:01:08.1883188)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_EnterKey_SplitsParagraphBeforeNextCharacter threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwy...
267. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_FastTyping_IsNotBatchedIntoLargeChunks` (00:01:08.2068119)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_FastTyping_IsNotBatchedIntoLargeChunks threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-blo...
268. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_SpaceKey_IsVisibleBeforeNextCharacter` (00:01:08.0408087)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase2E2ETests.Recovery_SpaceKey_IsVisibleBeforeNextCharacter threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bloc...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase3E2ETests`

269. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase3E2ETests.DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody` (00:00:16.1072209)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase3E2ETests.DefaultAndRecoveryDocuments_RenderHeaderFooterAroundBody threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Confidential - Page 1' But was: 'Confidential · Page 1...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase4E2ETests`

270. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase4E2ETests.RecoveryComments_RenderVisibleMarkerWithoutOpenPanel` (00:01:08.8692098)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase4E2ETests.RecoveryComments_RenderVisibleMarkerWithoutOpenPanel threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwy...
271. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase4E2ETests.RecoveryComments_TextAndPanelSelectionStayBidirectional` (00:01:08.3080300)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase4E2ETests.RecoveryComments_TextAndPanelSelectionStayBidirectional threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wys...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests`

272. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests.RecoveryRevisions_AcceptAndRejectActionsUpdateTextAndMarkers` (00:01:08.4894117)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests.RecoveryRevisions_AcceptAndRejectActionsUpdateTextAndMarkers threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .t...
273. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests.RecoveryRevisions_RenderVisibleInsertionAndDeletionMarkers` (00:01:08.2000641)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests.RecoveryRevisions_RenderVisibleInsertionAndDeletionMarkers threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-...
274. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests.RecoveryRevisions_TextAndPanelSelectionStayBidirectional` (00:01:08.3638004)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase5E2ETests.RecoveryRevisions_TextAndPanelSelectionStayBidirectional threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wy...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests`

275. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryFloatingToolbar_BoldClickKeepsSelectionAndToolbarVisible` (00:01:08.2170589)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryFloatingToolbar_BoldClickKeepsSelectionAndToolbarVisible threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'...
276. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryFloatingToolbar_ColorPopoverStaysOpenAndOutsideClickClosesToolbar` (00:01:08.2606628)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryFloatingToolbar_ColorPopoverStaysOpenAndOutsideClickClosesToolbar threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysi...
277. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryFloatingToolbar_StaysInsideViewportAndAwayFromSidePanel` (00:01:08.2112427)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryFloatingToolbar_StaysInsideViewportAndAwayFromSidePanel threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']...
278. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryTextSelection_MouseShowsFloatingToolbarAndKeepsItAfterMouseUp` (00:01:08.1992514)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase6E2ETests.RecoveryTextSelection_MouseShowsFloatingToolbarAndKeepsItAfterMouseUp threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests`

279. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests.RecoveryImageSelection_ClickShowsOutlineHandlesToolbarAndInspector` (00:01:08.5642554)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests.RecoveryImageSelection_ClickShowsOutlineHandlesToolbarAndInspector threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-hos...
280. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests.RecoveryImageToolbar_CommandsUpdateVisibleStateAndInspector` (00:01:08.2907767)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests.RecoveryImageToolbar_CommandsUpdateVisibleStateAndInspector threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm...
281. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests.RecoveryImageToolbar_GeometryDoesNotOverlapSidePanelAndTextClickHidesImageTools` (00:01:08.4068467)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase7E2ETests.RecoveryImageToolbar_GeometryDoesNotOverlapSidePanelAndTextClickHidesImageTools threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='documen...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests`

282. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_CommentMarkerSwitchesToCommentsAndActivatesThread` (00:01:08.1244210)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_CommentMarkerSwitchesToCommentsAndActivatesThread threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-ho...
283. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_ImageSelectionShowsImagePropertiesImmediately` (00:01:08.4275932)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_ImageSelectionShowsImagePropertiesImmediately threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']...
284. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_RevisionMarkerSwitchesToRevisionsAndActivatesRevision` (00:01:08.1560451)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_RevisionMarkerSwitchesToRevisionsAndActivatesRevision threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwy...
285. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_TableCellSelectionShowsTableAndCellProperties` (00:01:08.0407516)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_TableCellSelectionShowsTableAndCellProperties threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host']...
286. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_TextSelectionKeepsManualTabUntilObjectContextWins` (00:01:08.8241231)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase8E2ETests.RecoverySidePanel_TextSelectionKeepsManualTabUntilObjectContextWins threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-ho...

### `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests`

287. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryCommentMarker_RectStaysInsideTextLine` (00:01:08.4276298)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryCommentMarker_RectStaysInsideTextLine threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block...
288. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryFloatingToolbar_DoesNotCoverSelectedText` (00:01:08.2543259)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryFloatingToolbar_DoesNotCoverSelectedText threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bl...
289. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryImageToolbar_StaysOutsideReadableText` (00:01:08.2497853)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryImageToolbar_StaysOutsideReadableText threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block...
290. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryMarkerDecoration_DoesNotShiftAdjacentText` (00:01:08.4538154)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryMarkerDecoration_DoesNotShiftAdjacentText threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-b...
291. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryOverlayLayering_UsesStableZIndexTokensAndNonTextOverlayNodes` (00:01:08.3195800)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryOverlayLayering_UsesStableZIndexTokensAndNonTextOverlayNodes threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-h...
292. `Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryRevisionMarker_RectStaysInsideTextLine` (00:01:08.4480089)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorRegressionRecoveryPhase9E2ETests.RecoveryRevisionMarker_RectStaysInsideTextLine threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-bloc...

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase0E2ETests`

293. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase0E2ETests.DocumentEditor_Strict_Engine_ActiveParagraphReflowsBeforeNextPaint` (00:00:16.0691898)
   - Příznak: Expected after.Issues to be empty because active paragraph layout must already be valid in the next frame after 'space', but found at least one item {"text/image overlap: contract-tight-wrap-text -> contract-tight-wrap-image"}.
294. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase0E2ETests.DocumentEditor_Strict_Engine_LiveTypingKeepsCaretLogicalPosition` (00:00:16.1336463)
   - Příznak: Expected after.Issues to be empty because visual state must remain valid after 'space', but found at least one item { "text/image overlap: contract-tight-wrap-text -> contract-tight-wrap-image" }.
295. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase0E2ETests.DocumentEditor_Strict_Engine_LiveTypingNeverCreatesTextOverlap` (00:00:12.8060534)
   - Příznak: Expected probe.Issues to be empty because before typing: the strict frame probe must stay clean, but found at least one item {"text/image overlap: contract-tight-wrap-text -> contract-tight-wrap-image"}.
296. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase0E2ETests.DocumentEditor_Strict_Engine_TypingBesideWrappedImageUsesAvailableIntervals` (00:00:11.5361291)
   - Příznak: Expected target.Lines.Length to be greater than or equal to 2 because the reset demo must expose at least two text lines beside a wrapped image, but found 0 (difference of -2).

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase10E2ETests`

297. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase10E2ETests.DocumentEditor_Strict_PageLayout_RendersHeaderFooterFieldsAndRegionInputImmediately` (00:00:10.9483915)
   - Příznak: Expected result.FirstHeaderText "Page /" to contain "Page 1/4".

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase15E2ETests`

298. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase15E2ETests.DocumentEditor_Strict_History_ImageDragAndRevisionAcceptUndoRestoreModelAndLayout` (00:00:10.7218530)
   - Příznak: Expected result.ImageUndoX to be 40, but found 180 (difference of 140).
299. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase15E2ETests.DocumentEditor_Strict_History_TypingUndoRedoCoalescesAndRestoresSelection` (00:00:11.1292128)
   - Příznak: Expected result.RedoAppliedSource to be "redo" with a length of 4, but "typing" has a length of 6, differs near "typ" (index 0).

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase16E2ETests`

300. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase16E2ETests.DocumentEditor_Strict_Boundary_CSharpUpdatesSaveAckRemoteAssetAndRecoveryRespectActiveTransaction` (00:00:11.1674435)
   - Příznak: Expected result.AssetOk to be True, but found False.
301. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase16E2ETests.DocumentEditor_Strict_Boundary_SendsCanonicalPatchAfterCommitAndSurvivesCallbackFailure` (00:00:11.0912046)
   - Příznak: Expected result.PatchSent to be True, but found False.

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase17E2ETests`

302. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase17E2ETests.DocumentEditor_Strict_Diagnostics_WatchdogRecoversFailuresWithoutDroppingUserText` (00:00:10.7355324)
   - Příznak: Expected result.OperationTextPreserved to be True, but found False.

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests`

303. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests.DocumentEditor_Strict_Engine_DefaultDemoReloadIsReadableAndOverlapFree` (00:00:14.3944192)
   - Příznak: Default contract demo reload must be readable without text/image overlap is broken at probe 'Default contract demo reload must be readable without text/image overlap: before'. Issues: text/caption overlap: contract-right-wrap-text -> contract-behind-text-image; text/caption overl...
304. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests.DocumentEditor_Strict_Engine_Phase19_ImageWrapDragResizeUndoRedoStayTransactional` (00:00:43.9526749)
   - Příznak: Phase 19 image wrap drag resize undo redo is broken at probe 'Phase 19 image wrap drag resize undo redo: before'. Issues: text/caption overlap: contract-right-wrap-text -> contract-behind-text-image; text/caption overlap: contract-right-wrap-text -> contract-behind-text-image; te...
305. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests.DocumentEditor_Strict_Engine_Phase19_LoadWrapFootprintsMatchOnlyOfficeContracts` (00:00:12.3386197)
   - Příznak: Expected probe.LineIntervals to contain only items matching Not(HorizontallyIntersects(line, value(Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests+<>c__DisplayClass13_0).probe.ImageRect)) because text intervals must be cut around the actual image rectangle instead of c...
306. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase19E2ETests.DocumentEditor_Strict_Engine_Phase19_TextEditingBesideWrappedImageSupportsUndoRedo` (00:00:35.0234854)
   - Příznak: Phase 19 text editing beside wrapped image is broken at probe 'Phase 19 text editing beside wrapped image: before'. Issues: text/caption overlap: contract-right-wrap-text -> contract-behind-text-image; text/caption overlap: contract-right-wrap-text -> contract-behind-text-image; ...

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase1And2E2ETests`

307. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase1And2E2ETests.DocumentEditor_Strict_Engine_FacadeReturnsStableDisposedErrors` (00:00:10.8536660)
   - Příznak: Expected result.DebugErrorCode to be the same string, but they differ at index 0: ↓ (actual) "missing-instance" "disposed" ↑ (expected).
308. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase1And2E2ETests.DocumentEditor_Strict_Engine_GoogleDocsFacadeStillIgnoresDeterministicFlag` (00:01:08.2779885)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase1And2E2ETests.DocumentEditor_Strict_Engine_GoogleDocsFacadeStillIgnoresDeterministicFlag threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiw...

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase20E2ETests`

309. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase20E2ETests.DocumentEditor_Strict_Engine_RejectsLegacyFlagAndRoutesCommandsToNewEngine` (00:01:08.5349224)
   - Příznak: Test method Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase20E2ETests.DocumentEditor_Strict_Engine_RejectsLegacyFlagAndRoutesCommandsToNewEngine threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid='document-wysiwyg-...

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase23E2ETests`

310. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase23E2ETests.DocumentEditor_Strict_Engine_UxPolishContractsWorkInBrowserDom` (00:00:11.2717746)
   - Příznak: Expected afterWrapFootprint.TextLineFingerprint not to be "[[343,76,38,20],[381,76,169,20],[551,76,545,20],[342,97,597,20],[354,63,32,17],[406,63,30,17],[456,63,29,17],[505,63,32,17],[557,63,40,17],[617,63,30,17],[667,63,31,17],[718,63,16,17],[753,63,30,17]]" because changing wra...

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase4E2ETests`

311. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase4E2ETests.DocumentEditor_Strict_Selection_CaretHitTestMapperAndKeyboardMovement` (00:00:10.6689633)
   - Příznak: Expected result.LeftOffset to be 0, but found -1 (difference of -1).
312. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase4E2ETests.DocumentEditor_Strict_Selection_NormalizesPositionsRangesAndLimitBoundaries` (00:00:10.8088078)
   - Příznak: Expected result.PositionInline to be "r2", but "r1" differs near "1" (index 1).

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase5E2ETests`

313. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase5E2ETests.DocumentEditor_Strict_TextLayout_GreedyLineBreakerRespectsIntervalsHardBreaksAndLongTokens` (00:00:10.5219994)
   - Příznak: Expected result.AllSegmentsInsideIntervals to be True, but found False.
314. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase5E2ETests.DocumentEditor_Strict_TextLayout_JustifyIsLayoutMetadataAndDoesNotMoveLogicalOffsets` (00:00:10.9596410)
   - Příznak: Expected result.ExtraSpacePositive to be True, but found False.

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase6E2ETests`

315. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase6E2ETests.DocumentEditor_Strict_ParagraphLayout_ImmediateRelayoutAndPaginationHandoffStayNonOverlapping` (00:00:10.9486723)
   - Příznak: Expected result.P3Stale to be True, but found False.

### `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase8E2ETests`

316. `Tempo.Blazor.E2E.DocumentEditorStrictEnginePhase8E2ETests.DocumentEditor_Strict_Input_InsertTextUsesLogicalSelectionMarksTrackingLayoutRenderAndBoundaryPatch` (00:00:11.1721234)
   - Příznak: Expected result.Text to be the same string, but they differ at index 11: ↓ (actual) "…Tempoworld" "…Tempo" ↑ (expected).

### `Tempo.Blazor.E2E.EmailEditorDragDropE2ETests`

317. `Tempo.Blazor.E2E.EmailEditorDragDropE2ETests.DragMode_EmptyColumnsExposeLargeStableDropTargets` (00:00:21.7358672)
   - Příznak: Test method Tempo.Blazor.E2E.EmailEditorDragDropE2ETests.DragMode_EmptyColumnsExposeLargeStableDropTargets threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator("[data-tm-drop-empty]") to be visible
318. `Tempo.Blazor.E2E.EmailEditorDragDropE2ETests.Drop_TextBlockFromToolbox_IntoEmptyColumn_AddsBlock` (00:00:21.6142828)
   - Příznak: Test method Tempo.Blazor.E2E.EmailEditorDragDropE2ETests.Drop_TextBlockFromToolbox_IntoEmptyColumn_AddsBlock threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator("[data-tm-drop-empty]") to be visible

### `Tempo.Blazor.E2E.FormulaBuilderE2ETests`

319. `Tempo.Blazor.E2E.FormulaBuilderE2ETests.FormulaBuilder_InsertsTokenAndOperator` (00:00:12.7089048)
   - Příznak: Test method Tempo.Blazor.E2E.FormulaBuilderE2ETests.FormulaBuilder_InsertsTokenAndOperator threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have value matching regex '\{\{Subtotal\}\}\s\+\s\{\{Tax\}\}' But was: '{{formula-subtotal}} + {{formula-tax}}...

### `Tempo.Blazor.E2E.ModelingBpmnProfileM17E2ETests`

320. `Tempo.Blazor.E2E.ModelingBpmnProfileM17E2ETests.BpmnUnknownAiTaskFallsBackWithWarning` (00:00:16.4825447)
   - Příznak: Test method Tempo.Blazor.E2E.ModelingBpmnProfileM17E2ETests.BpmnUnknownAiTaskFallsBackWithWarning threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator("[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-model-element-i...

### `Tempo.Blazor.E2E.ModelingSourcePanelM9E2ETests`

321. `Tempo.Blazor.E2E.ModelingSourcePanelM9E2ETests.SourcePanel_LoadButton_ShowsLoadingAndReturnsLoaded` (00:00:11.3756652)
   - Příznak: Test method Tempo.Blazor.E2E.ModelingSourcePanelM9E2ETests.SourcePanel_LoadButton_ShowsLoadingAndReturnsLoaded threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator("[data-testid='modeling-editor-loading']") to be visible

### `Tempo.Blazor.E2E.NotionBlockEditingE2ETests`

322. `Tempo.Blazor.E2E.NotionBlockEditingE2ETests.BulletList_Enter_ContinuesList` (00:00:19.0704576)
   - Příznak: Test method Tempo.Blazor.E2E.NotionBlockEditingE2ETests.BulletList_Enter_ContinuesList threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator(".tm-notion-bullet__body[contenteditable='true']").First to be visible
323. `Tempo.Blazor.E2E.NotionBlockEditingE2ETests.DividerBlock_Renders` (00:00:19.2619303)
   - Příznak: Test method Tempo.Blazor.E2E.NotionBlockEditingE2ETests.DividerBlock_Renders threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator(".tm-notion-divider").First to be visible
324. `Tempo.Blazor.E2E.NotionBlockEditingE2ETests.Heading1_Type_RendersH1` (00:00:19.0593185)
   - Příznak: Test method Tempo.Blazor.E2E.NotionBlockEditingE2ETests.Heading1_Type_RendersH1 threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator(".tm-notion-heading--h1").First to be visible
325. `Tempo.Blazor.E2E.NotionBlockEditingE2ETests.Heading2_Type_RendersH2` (00:00:18.5801253)
   - Příznak: Test method Tempo.Blazor.E2E.NotionBlockEditingE2ETests.Heading2_Type_RendersH2 threw exception: System.TimeoutException: Timeout 10000ms exceeded. Call log: - - waiting for Locator(".tm-notion-heading--h2").First to be visible
326. `Tempo.Blazor.E2E.NotionBlockEditingE2ETests.TodoItem_Click_TogglesCheckbox` (00:00:42.5450190)
   - Příznak: Test method Tempo.Blazor.E2E.NotionBlockEditingE2ETests.TodoItem_Click_TogglesCheckbox threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("label.tm-notion-todo").Last
327. `Tempo.Blazor.E2E.NotionBlockEditingE2ETests.TodoItem_Click_Twice_UnchecksCheckbox` (00:00:43.5947081)
   - Příznak: Test method Tempo.Blazor.E2E.NotionBlockEditingE2ETests.TodoItem_Click_Twice_UnchecksCheckbox threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("label.tm-notion-todo").Last

### `Tempo.Blazor.E2E.NotionCommentsE2ETests`

328. `Tempo.Blazor.E2E.NotionCommentsE2ETests.BlockComment_Mention_Notification` (00:00:16.6023473)
   - Příznak: Test method Tempo.Blazor.E2E.NotionCommentsE2ETests.BlockComment_Mention_Notification threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notification-bell__badge").First to be visible
329. `Tempo.Blazor.E2E.NotionCommentsE2ETests.Notification_Bell_ShowsCount` (00:00:16.2436352)
   - Příznak: Test method Tempo.Blazor.E2E.NotionCommentsE2ETests.Notification_Bell_ShowsCount threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notification-bell__badge").First to be visible

### `Tempo.Blazor.E2E.NotionCommentsRecoveryE2ETests`

330. `Tempo.Blazor.E2E.NotionCommentsRecoveryE2ETests.EB10_TextAnchorPagePanelAndNoProviderStates_AreCaptured` (00:00:15.8882966)
   - Příznak: Test method Tempo.Blazor.E2E.NotionCommentsRecoveryE2ETests.EB10_TextAnchorPagePanelAndNoProviderStates_AreCaptured threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator("button[title='Comment']").First

### `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests`

331. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_AddField_AppearsInHeader` (00:00:23.8049055)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_AddField_AppearsInHeader threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
332. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_AddView_Tab_Appears` (00:00:23.4245817)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_AddView_Tab_Appears threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
333. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_BoardView_DragCard_MovesColumn` (00:00:24.2074403)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_BoardView_DragCard_MovesColumn threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
334. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Export_DownloadsFile` (00:00:23.6996094)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Export_DownloadsFile threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
335. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_FieldEditor_Opens` (00:00:23.7556447)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_FieldEditor_Opens threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
336. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Fields_HideField_HidesColumn` (00:00:24.1817943)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Fields_HideField_HidesColumn threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
337. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Fields_OpenPanel` (00:00:23.4254535)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Fields_OpenPanel threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
338. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Fields_ShowField_ShowsColumn` (00:00:23.9099363)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Fields_ShowField_ShowsColumn threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
339. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Filter_AddCondition_ReducesRecords` (00:00:24.0684311)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Filter_AddCondition_ReducesRecords threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
340. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Filter_OpenPanel` (00:00:23.3723378)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Filter_OpenPanel threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
341. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Filter_Remove_ShowsAll` (00:00:23.4469231)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Filter_Remove_ShowsAll threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
342. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Group_OpenPanel` (00:00:23.9418966)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Group_OpenPanel threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
343. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Sort_ByName_SortsAlphabetically` (00:00:23.3882781)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Sort_ByName_SortsAlphabetically threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
344. `Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Sort_OpenPanel` (00:00:24.4425181)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseAdvancedE2ETests.Database_Sort_OpenPanel threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible

### `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests`

345. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_AddRecord_AppearsInTable` (00:00:24.2828092)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_AddRecord_AppearsInTable threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
346. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_CellEdit_Checkbox_Toggles` (00:00:23.7026347)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_CellEdit_Checkbox_Toggles threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
347. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_CellEdit_Text_SavesValue` (00:00:23.5332021)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_CellEdit_Text_SavesValue threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
348. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_RecordDetail_Close` (00:00:23.6469400)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_RecordDetail_Close threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
349. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_RecordDetail_Opens` (00:00:23.3836748)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_RecordDetail_Opens threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
350. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_BackToTable` (00:00:23.7390566)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_BackToTable threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
351. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Board_ShowsKanban` (00:00:23.6154863)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Board_ShowsKanban threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
352. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Calendar_ShowsCalendar` (00:00:23.3330215)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Calendar_ShowsCalendar threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
353. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Gallery_ShowsCards` (00:00:23.4317615)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Gallery_ShowsCards threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
354. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_List_ShowsList` (00:00:23.8653541)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_List_ShowsList threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
355. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Timeline_ShowsTimeline` (00:00:24.1426042)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_SwitchView_Timeline_ShowsTimeline threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
356. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_TableView_LoadsData` (00:00:23.7605313)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_TableView_LoadsData threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
357. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_TableView_ShowsFields` (00:00:23.6795892)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_TableView_ShowsFields threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible
358. `Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_TableView_ShowsRecords` (00:00:23.4454306)
   - Příznak: Test method Tempo.Blazor.E2E.NotionDatabaseBasicE2ETests.Database_TableView_ShowsRecords threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-db").First to be visible

### `Tempo.Blazor.E2E.NotionFormattingE2ETests`

359. `Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_BottomEdge_CaptureBaseline` (00:00:19.3330436)
   - Příznak: Test method Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_BottomEdge_CaptureBaseline threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notion-inline-toolbar").First to be visible
360. `Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_ColorPanelViewportEdge_CaptureBaseline` (00:00:19.7700051)
   - Příznak: Test method Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_ColorPanelViewportEdge_CaptureBaseline threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notion-inline-toolbar").First to be visible
361. `Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_MainButtonsAndActiveStates_CaptureBaseline` (00:00:19.0928206)
   - Příznak: Test method Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_MainButtonsAndActiveStates_CaptureBaseline threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notion-inline-toolbar").First to be visible
362. `Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_TurnIntoPanelViewportEdge_CaptureBaseline` (00:00:19.1411312)
   - Příznak: Test method Tempo.Blazor.E2E.NotionFormattingE2ETests.EB4_InlineToolbar_TurnIntoPanelViewportEdge_CaptureBaseline threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notion-inline-toolbar").First to be visible
363. `Tempo.Blazor.E2E.NotionFormattingE2ETests.InlineToolbar_Link_Remove_UnwrapsAnchor` (00:00:17.1271341)
   - Příznak: Test method Tempo.Blazor.E2E.NotionFormattingE2ETests.InlineToolbar_Link_Remove_UnwrapsAnchor threw exception: System.TimeoutException: Timeout 5000ms exceeded.

### `Tempo.Blazor.E2E.NotionKeyboardE2ETests`

364. `Tempo.Blazor.E2E.NotionKeyboardE2ETests.PageSearch_Enter_NavigatesToPage` (00:00:11.6975304)
   - Příznak: Assert.IsFalse failed. Search modal should close after Enter
365. `Tempo.Blazor.E2E.NotionKeyboardE2ETests.PageSearch_Type_FiltersResults` (00:00:10.1237878)
   - Příznak: Assert.IsTrue failed. Search should return at least one result for 'Product'

### `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests`

366. `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.AudioBlock_EnterUrl_ShowsPlayer` (00:00:44.0899142)
   - Příznak: Test method Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.AudioBlock_EnterUrl_ShowsPlayer threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-media-dialog").Locator(".tm-media-dialog__url-input")
367. `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.FileBlock_Shows_DownloadLink` (00:00:44.1108997)
   - Příznak: Test method Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.FileBlock_Shows_DownloadLink threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-media-dialog").Locator(".tm-media-dialog__url-input")
368. `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.ImageBlock_Caption_Editable` (00:00:13.5050497)
   - Příznak: Test method Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.ImageBlock_Caption_Editable threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator("[data-block-type='Image']").First.Locator(".tm-notion-image-block__caption") to be visible
369. `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.ImageBlock_EnterUrl_DisplaysImage` (00:00:43.8643076)
   - Příznak: Test method Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.ImageBlock_EnterUrl_DisplaysImage threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-media-dialog").Locator(".tm-media-dialog__url-input")
370. `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.ImageBlock_Resize_HandleVisible` (00:00:13.5236455)
   - Příznak: Test method Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.ImageBlock_Resize_HandleVisible threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator("[data-block-type='Image']").First.Locator(".tm-notion-image-block__img-wrap") to be visible
371. `Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.VideoBlock_EnterUrl_ShowsEmbed` (00:00:44.0460694)
   - Příznak: Test method Tempo.Blazor.E2E.NotionMediaBlocksE2ETests.VideoBlock_EnterUrl_ShowsEmbed threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-media-dialog").Locator(".tm-media-dialog__url-input")

### `Tempo.Blazor.E2E.NotionNavBlocksE2ETests`

372. `Tempo.Blazor.E2E.NotionNavBlocksE2ETests.BreadcrumbBlock_Click_NavigatesToParent` (00:00:18.7024338)
   - Příznak: Test method Tempo.Blazor.E2E.NotionNavBlocksE2ETests.BreadcrumbBlock_Click_NavigatesToParent threw exception: System.TimeoutException: Timeout 8000ms exceeded. Call log: - - waiting for Locator(".tm-ns-search__result").Filter(new() { HasText = "Architecture Guide" }).First to be ...
373. `Tempo.Blazor.E2E.NotionNavBlocksE2ETests.BreadcrumbBlock_Renders_ShowsPath` (00:00:18.6122391)
   - Příznak: Test method Tempo.Blazor.E2E.NotionNavBlocksE2ETests.BreadcrumbBlock_Renders_ShowsPath threw exception: System.TimeoutException: Timeout 8000ms exceeded. Call log: - - waiting for Locator(".tm-ns-search__result").Filter(new() { HasText = "Architecture Guide" }).First to be visibl...
374. `Tempo.Blazor.E2E.NotionNavBlocksE2ETests.ChildPageBlock_Click_NavigatesToPage` (00:00:23.4358183)
   - Příznak: Test method Tempo.Blazor.E2E.NotionNavBlocksE2ETests.ChildPageBlock_Click_NavigatesToPage threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-child-page").First to be visible
375. `Tempo.Blazor.E2E.NotionNavBlocksE2ETests.ChildPageBlock_Renders_WithTitle` (00:00:23.7671179)
   - Příznak: Test method Tempo.Blazor.E2E.NotionNavBlocksE2ETests.ChildPageBlock_Renders_WithTitle threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-child-page").First to be visible
376. `Tempo.Blazor.E2E.NotionNavBlocksE2ETests.LinkedPageBlock_Click_NavigatesToPage` (00:00:23.2335996)
   - Příznak: Test method Tempo.Blazor.E2E.NotionNavBlocksE2ETests.LinkedPageBlock_Click_NavigatesToPage threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-linked-page").First to be visible
377. `Tempo.Blazor.E2E.NotionNavBlocksE2ETests.LinkedPageBlock_Renders_WithTitle` (00:00:23.5055807)
   - Příznak: Test method Tempo.Blazor.E2E.NotionNavBlocksE2ETests.LinkedPageBlock_Renders_WithTitle threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-linked-page").First to be visible

### `Tempo.Blazor.E2E.NotionPageSettingsRecoveryE2ETests`

378. `Tempo.Blazor.E2E.NotionPageSettingsRecoveryE2ETests.EB12_FullWidthSmallTextLocked_AreCaptured` (00:00:16.9213088)
   - Příznak: Test method Tempo.Blazor.E2E.NotionPageSettingsRecoveryE2ETests.EB12_FullWidthSmallTextLocked_AreCaptured threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to have attribute 'contenteditable' 'false' But was: 'null' Call log: - - LocatorAssertions.ToHave...

### `Tempo.Blazor.E2E.NotionSlashMenuE2ETests`

379. `Tempo.Blazor.E2E.NotionSlashMenuE2ETests.SlashMenu_ArrowDown_NavigatesItems` (00:00:10.2301711)
   - Příznak: Assert.IsTrue failed. First item should be selected by default
380. `Tempo.Blazor.E2E.NotionSlashMenuE2ETests.SlashMenu_ArrowUp_NavigatesItems` (00:00:11.0517532)
   - Příznak: Assert.IsTrue failed. Third item should be selected after two ArrowDown presses
381. `Tempo.Blazor.E2E.NotionSlashMenuE2ETests.SlashMenu_Escape_Closes` (00:00:47.5321402)
   - Příznak: Test method Tempo.Blazor.E2E.NotionSlashMenuE2ETests.SlashMenu_Escape_Closes threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-notion-editor") to be visible

### `Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests`

382. `Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TableOfContents_Click_ScrollsToHeading` (00:00:23.6923517)
   - Příznak: Test method Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TableOfContents_Click_ScrollsToHeading threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-toc").First to be visible
383. `Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TableOfContents_Renders_ListsHeadings` (00:00:24.2466468)
   - Příznak: Test method Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TableOfContents_Renders_ListsHeadings threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-toc").First to be visible
384. `Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TemplateButton_Click_InsertsTemplateBlocks` (00:00:23.5270915)
   - Příznak: Test method Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TemplateButton_Click_InsertsTemplateBlocks threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-template-btn").First to be visible
385. `Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TemplateButton_Label_Editable` (00:00:23.8479921)
   - Příznak: Test method Tempo.Blazor.E2E.NotionSpecialBlocksE2ETests.TemplateButton_Label_Editable threw exception: System.TimeoutException: Timeout 15000ms exceeded. Call log: - - waiting for Locator(".tm-template-btn").First to be visible

### `Tempo.Blazor.E2E.PdfTemplateDesignerE2ETests`

386. `Tempo.Blazor.E2E.PdfTemplateDesignerE2ETests.PdfTemplateDesigner_DrawsTextField` (00:00:13.5715423)
   - Příznak: Test method Tempo.Blazor.E2E.PdfTemplateDesignerE2ETests.PdfTemplateDesigner_DrawsTextField threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text '3 designer fields' But was: '2 designer fields' Call log: - - LocatorAssertions.ToContainTextAs...
387. `Tempo.Blazor.E2E.PdfTemplateDesignerE2ETests.PdfTemplateDesigner_OpensDemo` (00:00:47.5725299)
   - Příznak: Test method Tempo.Blazor.E2E.PdfTemplateDesignerE2ETests.PdfTemplateDesigner_OpensDemo threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='pdf-template-designer']").First

### `Tempo.Blazor.E2E.SignatureCaptureE2ETests`

388. `Tempo.Blazor.E2E.SignatureCaptureE2ETests.SignatureCapture_MobileViewport_CapturesPointerSignature` (00:00:06.8111478)
   - Příznak: Assert.IsTrue failed. Mouse start should hit signature canvas, but hit: <div class="tm-signing-form-runner__step-panel" data-mobile="true"><div class="tm-signing-form-runner__progress" role="status" aria-label="Step 1 of 6">Step 1 of 6</div><label class="tm-signing-form-r

### `Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests`

389. `Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests.DocumentComments_AreaDraftAndEscapeWork` (00:00:13.4945962)
   - Příznak: Test method Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests.DocumentComments_AreaDraftAndEscapeWork threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - waiting for Loca...
390. `Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests.DocumentComments_CreateThreadWithMention` (00:00:13.6084962)
   - Příznak: Test method Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests.DocumentComments_CreateThreadWithMention threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - waiting for Loc...
391. `Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests.DocumentComments_EmptyDraftCannotSubmit` (00:00:13.2747306)
   - Příznak: Test method Tempo.Blazor.E2E.SigningDocumentCommentsE2ETests.DocumentComments_EmptyDraftCannotSubmit threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to be visible Call log: - - LocatorAssertions.ToBeVisibleAsync with timeout 5000ms - - waiting for Loca...

### `Tempo.Blazor.E2E.SigningFieldEditorPanelE2ETests`

392. `Tempo.Blazor.E2E.SigningFieldEditorPanelE2ETests.SigningFieldEditor_RenamesFieldAndUpdatesPreview` (00:00:12.9812459)
   - Příznak: Test method Tempo.Blazor.E2E.SigningFieldEditorPanelE2ETests.SigningFieldEditor_RenamesFieldAndUpdatesPreview threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Residence country' But was: ' ZeměČeská republikaSpojené státy' Call log: - -...
393. `Tempo.Blazor.E2E.SigningFieldEditorPanelE2ETests.SigningFieldEditor_SelectPreview_DoesNotClipOptionLabels` (00:00:11.9796886)
   - Příznak: Test method Tempo.Blazor.E2E.SigningFieldEditorPanelE2ETests.SigningFieldEditor_SelectPreview_DoesNotClipOptionLabels threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Czech Republic' But was: ' ZeměČeská republikaSpojené státy' Call log...

### `Tempo.Blazor.E2E.SigningLocalizationE2ETests`

394. `Tempo.Blazor.E2E.SigningLocalizationE2ETests.LocalizedSigningUi_KeepsFallbackTextAndPanelLayoutStable` (00:00:47.5067966)
   - Příznak: Test method Tempo.Blazor.E2E.SigningLocalizationE2ETests.LocalizedSigningUi_KeepsFallbackTextAndPanelLayoutStable threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator("[data-testid='pdf-template-designer']").First

### `Tempo.Blazor.E2E.SigningQualityE2ETests`

395. `Tempo.Blazor.E2E.SigningQualityE2ETests.SigningFormRunner_KeyboardOnlyNavigatesSteps` (00:00:12.6036907)
   - Příznak: Test method Tempo.Blazor.E2E.SigningQualityE2ETests.SigningFormRunner_KeyboardOnlyNavigatesSteps threw exception: Microsoft.Playwright.PlaywrightException: Locator expected to contain text 'Amount' But was: 'Delivery method' Call log: - - LocatorAssertions.ToContainTextAsync with...
396. `Tempo.Blazor.E2E.SigningQualityE2ETests.SigningOverlays_RemainStableAcrossResizeAndZoom` (00:00:07.3411738)
   - Příznak: Assert.IsTrue failed.

### `Tempo.Blazor.E2E.SpreadsheetBlockE2ETests`

397. `Tempo.Blazor.E2E.SpreadsheetBlockE2ETests.SpreadsheetBlock_SlashMenu_InsertsBlock` (00:00:17.5098518)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetBlockE2ETests.SpreadsheetBlock_SlashMenu_InsertsBlock threw exception: System.TimeoutException: Timeout 5000ms exceeded. Call log: - - waiting for Locator(".tm-notion-slash-item").Filter(new() { HasText = "Spreadsheet" }).First to be visibl...

### `Tempo.Blazor.E2E.SpreadsheetE2ETests`

398. `Tempo.Blazor.E2E.SpreadsheetE2ETests.ArrowNavigation_ScrollsGridHorizontally` (00:00:42.8600229)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.ArrowNavigation_ScrollsGridHorizontally threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-spreadsheet-grid").Nth(2) to be visible
399. `Tempo.Blazor.E2E.SpreadsheetE2ETests.ArrowNavigation_ScrollsGridVertically` (00:00:42.5360406)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.ArrowNavigation_ScrollsGridVertically threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-spreadsheet-grid").Nth(2) to be visible
400. `Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_CanvasKeyboardUsableOnLargeDataset` (00:01:12.8736775)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_CanvasKeyboardUsableOnLargeDataset threw exception: System.TimeoutException: Timeout 60000ms exceeded. Call log: - - waiting for Locator("[data-testid=\"spreadsheet-benchmark-result-row\"][data-renderer=\"CanvasJsEngi...
401. `Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_ExposesPasteLatencyForCanvasAndCanvasJsEngine` (00:00:41.3470373)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_ExposesPasteLatencyForCanvasAndCanvasJsEngine threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for GetByTestId("spreadsheet-benchmark-run-both")
402. `Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_Phase11ReadinessMetricsPass` (00:00:41.5545379)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_Phase11ReadinessMetricsPass threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for GetByTestId("spreadsheet-benchmark-run-both")
403. `Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_Phase12BenchmarkRowExposesReadinessMetrics` (00:00:41.0004552)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_Phase12BenchmarkRowExposesReadinessMetrics threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for GetByTestId("spreadsheet-benchmark-run-both")
404. `Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_ResizeReadinessMetricsPass` (00:01:42.7585199)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.BenchmarkPage_ResizeReadinessMetricsPass threw exception: System.TimeoutException: Timeout 90000ms exceeded. Call log: - - waiting for Locator("[data-testid=\"spreadsheet-benchmark-result-row\"][data-renderer=\"CanvasJsEngine\"]")....
405. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_F2TransfersFormulaSessionFromFormulaBarToInlineEditorWithoutLosingCaret` (00:00:43.5731448)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_F2TransfersFormulaSessionFromFormulaBarToInlineEditorWithoutLosingCaret threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-spreadsheet").Filter(new() { Has = Lo...
406. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_FormulaBarDragRangeReplacesReferenceWithoutChangingActiveCell` (00:00:43.7390435)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_FormulaBarDragRangeReplacesReferenceWithoutChangingActiveCell threw exception: System.TimeoutException: Timeout 30000ms exceeded.
407. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_FormulaBarLongSessionCombinesAutocompleteReferencePickingScrollAndCommit` (00:00:14.2948926)
   - Příznak: Assert.AreEqual failed. Expected:<=SUM(E6,J20:L22>. Actual:<=SUM(E6,>. Expected drag range after viewport scroll to replace the active argument token.
408. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_FormulaBarMixedFormulaDragRangeReplacesOnlyCaretTargetedToken` (00:00:13.2653031)
   - Příznak: Assert.AreEqual failed. Expected:<=SUM(A1:B5)+J8:L10>. Actual:<=SUM(A1:B5)+J8:A1>. Expected drag range reference-picking to replace only the caret-targeted single reference inside a mixed formula.
409. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_MouseClickSyncsBlazorActiveCellImmediately` (00:00:13.6920031)
   - Příznak: Assert.AreEqual failed. Expected:<F5>. Actual:<>. Expected Blazor formula bar ref to update immediately after canvas click. Before click activeRef=G6, formulaBarRef=, commandLogCallbacks=5, cellPointerCallbacks=1. After click activeRef=F5, formulaBarRef=, commandLogCallbacks=6, c...
410. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_PublicApiCellUpdatesReachCanvasStore` (00:00:22.3915480)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasJsEngine_PublicApiCellUpdatesReachCanvasStore threw exception: System.TimeoutException: Timeout 10000ms exceeded.
411. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_DragSelectionAutoscrollsRight` (00:00:42.7453580)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_DragSelectionAutoscrollsRight threw exception: System.TimeoutException: Timeout 30000ms exceeded.
412. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_DrawsFormulaReferenceHighlights` (00:00:13.3497198)
   - Příznak: Assert.IsTrue failed. Expected formula reference highlight pixels. Count: 4.
413. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_JsFormulaReferenceHighlightsRemainVisibleAfterScroll` (00:00:12.5275226)
   - Příznak: Assert.IsTrue failed. Expected formula reference highlight pixels after scroll. Count: 0.
414. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_RapidArrowDownKeepsSelectionMonotonicWhileScrolling` (00:00:23.9526566)
   - Příznak: Assert.IsTrue failed. Expected rapid ArrowDown navigation to reach a later row. Last row: 15.
415. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_RapidArrowDownStaysOnLocalHotPath` (00:00:13.0942188)
   - Příznak: Assert.IsTrue failed. Expected rapid local ArrowDown navigation to advance far down the sheet. Ref: G15.
416. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_RapidArrowRightKeepsSelectionMonotonicWhileScrolling` (00:00:18.9402995)
   - Příznak: Assert.IsTrue failed. Expected ArrowRight navigation to scroll canvas grid. scrollLeft: 0.
417. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_RendersNonBlankCanvasAndScrollsHorizontally` (00:00:48.1150474)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_RendersNonBlankCanvasAndScrollsHorizontally threw exception: System.TimeoutException: Timeout 30000ms exceeded.
418. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_ScrollDuringLocalEditKeepsEditorAlignedAndHidesWhenOutOfView` (00:00:44.8983043)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_ScrollDuringLocalEditKeepsEditorAlignedAndHidesWhenOutOfView threw exception: System.TimeoutException: Timeout 30000ms exceeded.
419. `Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_SmallScrollUsesBitmapShiftAndLargeScrollFallsBack` (00:00:43.3479773)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.CanvasRenderer_SmallScrollUsesBitmapShiftAndLargeScrollFallsBack threw exception: System.TimeoutException: Timeout 30000ms exceeded.
420. `Tempo.Blazor.E2E.SpreadsheetE2ETests.DomRenderer_RemainsFunctionalAsFallback` (00:00:42.2871699)
   - Příznak: Test method Tempo.Blazor.E2E.SpreadsheetE2ETests.DomRenderer_RemainsFunctionalAsFallback threw exception: System.TimeoutException: Timeout 30000ms exceeded. Call log: - - waiting for Locator(".tm-spreadsheet-grid").Nth(2) to be visible
