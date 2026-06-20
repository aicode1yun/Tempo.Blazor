# Canvas editor — Fáze B: operation-relay architektura (engine = jediný zdroj pravdy, C# mirror pryč)

**Vytvořeno:** 2026-06-08
**Navazuje na:** `planning/tmdocumenteditor-canvas-performance-and-rendering-fix-todo-2026-06-08.md` (Fáze 8 — perf opravy)
**Rozhodnutí uživatele (2026-06-08):** míříme na Fázi B (operation-relay, průběžný C# mirror pryč). „Vše musí fungovat" v každém kroku. `updateBlock` granularita teď stačí (není třeba jemnozrnné table/image operace). Implementace = **TDD + průběžné E2E + screenshotové testy** (Claude posuzuje, že screenshot ukazuje, co se stát mělo). Při implementaci odškrtávat **jen skutečně hotové** položky.

---

## 0. Cíl a principy

**Cílová architektura** (jako OnlyOffice/Google Docs):
- **Canvas JS engine = jediný zdroj pravdy** pro dokument, editaci, undo/redo, operace.
- **C# NEMÁ průběžně synchronizovaný plný mirror dokumentu.** `_document` se stane *lazy snapshotem* taženým na vyžádání (save/export/load/blur/compare), ne udržovaným per-úhoz.
- **Kolaborace = relay operací**: engine emituje lokální operační batch (z op-logu) → C# je tenká roura → provider → cizí klient → `applyRemoteOperationBatch` do jeho enginu. Žádný C# diff `before/after`.
- **Živá UI data** (formatting-state toolbaru, dirty, undo/redo, word/page count, seznam komentářů, outline, revize) = **malé odvozené hodnoty pushnuté z enginu**, když se změní. Ne celý dokument.
- **Plný dokument** = pull na vyžádání (save/export/compare/print).

**Principy implementace:**
- **Inkrementálně a bezpečně:** `_document` má **310 referencí** v `TmDocumentEditor.razor.cs` → NErušit naráz. Vzorec: *přidat nový mechanismus vedle starého → ověřit paritu testy → přepnout → smazat starý*.
- **TDD:** každá funkční změna nejdřív červený test (Node `*.test.mjs` pro JS, C# unit/E2E), pak implementace, pak zelená.
- **Průběžné E2E + screenshoty:** každá fáze má živý E2E na `/document-editor` (port 7106) + screenshot uložený do `/tmp/canvas-phaseB/<fáze>/`, který Claude otevře a posoudí „stalo se, co se stát mělo".
- **Vše funguje:** po každé fázi běží correctness regrese (InlineFormat, CaretSelection, ToolbarSpellcheck, OverlapPerf, CommentsRevisions, Collaboration, HistorySave, Typing) bez NOVÝCH selhání + Node sada zelená.
- **Perf nesmí regresovat:** engine typing-latence ≤ 50 ms (gate `DocumentEditorCanvasEndToEndTypingE2ETests`), human-cadence bez dávkování (`DocumentEditorCanvasHumanTypingE2ETests`).

**Test infra (k využití):**
- Node moduly: `npm run test:document-editor-modules` (rychlé, `.mjs` se servíruje živě — bez rebuildu).
- E2E: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~<Class>" -- xUnit.parallelizeTestCollections=false` (paralelní = OOM).
- Servery: WASM `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https` (7106), API `dotnet run --project src/Tempo.Blazor.Demo.Api --launch-profile Tempo.Blazor.Demo.Api` (5100). **C# změny vyžadují rebuild+restart WASM; `.mjs` jsou živé.**
- Screenshoty: `DocumentEditorE2ETestBase` (`OpenDocumentEditorAsync`, `CaptureEditorScreenshotAsync`), `tests/Tempo.Blazor.E2E/CanvasEngine/` (`DocumentEditorCanvasVisualAssert`), výstup `/tmp/canvas-phaseB/`.
- **POZN:** server padá při `pkill`+launch v jednom příkazu → kill a launch zvlášť, `setsid … &` přes run_in_background.

**Současný stav (zmapováno 2026-06-08):**
- JS→.NET callbacky: `OnCanvasEngineChanged` (debounced 400 ms), `OnCanvasMiniToolbarChanged` (debounced 400 ms), `OnCanvasEngineReady`, `OnCanvasContextMenuRequested`, `OnCanvas{CommandPalette,RibbonFocus,VersionsPanel}Requested`, `OnCanvasAnnotationSelected`.
- C#→JS API (interop exports): `mount/setOptions/dispose`, `getModelJson/getSnapshotJson/setModel/replaceModel`, `applyRemoteOperationBatch/applyRemoteCursor(s)/getCollaborationStateJson/getOfflineStateJson`, `isDirty/markSaved`, `execCommand/queryCommand`, `getFormattingStateJson/getUndoStateJson/getSelectionStateJson/getNavigationStateJson/getSearchStateJson/getPrintPreviewStateJson`, `setTrackChangesEnabled/setReviewDisplayMode/selectComment/selectRevision/captureCommentAnchorJson`, diagnostiky.
- Op-log (`collaboration/op-log.mjs`): `recordLocalChange` → `diffModels` → operace `insertText/deleteText/insertBlock/deleteBlock/moveBlock/updateBlock`; `pendingLocalBatches` (s `localSequence`); `snapshot()`; `transform.mjs` (OT).
- C# collab: `DocumentCollaborationSync.CreateLocalPatchBatch(before, after|patch)` (REDUNDANTNÍ diff), `JoinAsync/LeaveAsync`, `IDocumentCollaborationProvider` + realtime (`RemoteOperationBatchReceived` → `ApplyRemoteOperationBatchAsync` → JS `applyRemoteOperationBatch`).
- Konzumenti `_document`: collab broadcast (`BroadcastLocalCollaborationChangeAsync`), comments (`SyncCommentsFromRuntimeDocument`), suggestions (`TryCreateSuggestionAsync(before, after)`), word/page count (status bar), save (`SaveCoreAsync` už tahá čerstvý), `OnDocumentLoaded` (jednorázový), compare.
- Per-úhozová cesta dnes: `HandleCanvasEngineChangedAsync` (O(1), debounced toolbar-sync 200 ms + reconcile 2500 ms). Reconcile `ReconcileCanvasDocumentAsync` = `RequestDocumentAsync` (full marshal) + snapshot + comments + collab diff + StateHasChanged. **Tohle je to, co Fáze B zruší.**

---

## Fáze B0 — Charakterizační testy + kontrakty (síť, ať „vše funguje" jde ověřit)

Cíl: zafixovat SOUČASNÉ chování testy, aby šlo prokázat parita po migraci. Definovat nové kontrakty.

- [ ] **B0.1** Charakterizační E2E (zelené na SOUČASNÉM kódu) — nová třída `DocumentEditorCanvasPhaseBBaselineE2ETests`:
  - [ ] collab round-trip (dvě instance enginu / simulovaný remote): text edit v A → objeví se v B; + screenshot B.
  - [ ] table edit (změna buňky) propaguje; image resize propaguje; paragraph-property změna propaguje (updateBlock).
  - [ ] comment add/resolve → comment rail; revision accept/reject; word count update; save→reload zachová obsah.
  - každý s `CaptureEditorScreenshotAsync` do `/tmp/canvas-phaseB/B0/`.
- [ ] **B0.2** Kontrakt „local operation batch" (JS→C#): rozhodnout shape. **Reuse `DocumentCollaborationOperationBatch`** (C# už ho posílá do `applyRemoteOperationBatch`, takže engine ho umí konzumovat → symetrie). Engine emituje TÝŽ tvar, jaký umí aplikovat. Zdokumentovat do tohoto MD.
- [ ] **B0.3** Kontrakt „UI state push" (JS→C#): jeden malý payload `OnCanvasUiStateChanged { formatting, dirty, undo, wordCount, pageCount, selectionSummary }` (rozšiřitelný o comments/revisions v B3). Zdokumentovat.
- [x] **B0.4** Node test `collaboration/__tests__/op-log-coverage.test.mjs` (11 testů, Node sada 301→**312**). Charakterizace `diffModels`: insertText/deleteText (čistý prefix/suffix), mixed replace → `updateBlock`, split → `insertBlock`, delete → `deleteBlock`, paragraph property (align) → `updateBlock`, image resize → `updateBlock`, `recordLocalChange` → batch + pending sequence + ack.
  - ⚠️ **ZJIŠTĚNÍ pro B1:** `diffModels` recursuje DO buněk tabulky → editace buňky emituje REDUNDANTNÍ `updateBlock(tabulka)` (nese už celý nový obsah) + `insertText(buňka)` (totéž znovu) → **dvojí aplikace na remote**; navíc **spurious `moveBlock(buňka)`** (porovnává globální map-index s within-array indexem u vnořených bloků). **B1 musí: editace tabulky = JEDEN `updateBlock(tabulka)`, NErekurzovat do buněk** (user akceptoval updateBlock granularitu). Testy 8/9 zafixovaly současné (chybné) chování jako baseline.
- [x] **B0.2 KONTRAKT — local operation batch (JS op-log):** `recordLocalChange` vrací `{ id, documentId, protocolVersion=1, baseVersionId, clientId, transactionId, localSequence, selectionAfter, operations:[ { operationId, schemaVersion=1, type, target:{blockId, tableCellId, offset, length, order}, metadata:{clientId, source, createdAt}, text|null, block|null } ] }`. Typy: `insertText|deleteText|insertBlock|deleteBlock|moveBlock|updateBlock`.
  - **C# remote-apply tvar** (co dnes konzumuje JS `applyRemoteOperationBatch`): `DocumentCollaborationOperationBatch { Sequence:long, SessionId:string, Batch:DocumentOperationBatch{ …, Operations:[DocumentOperation{ SchemaVersion, Type:enum(InsertText/DeleteText/InsertBlock/DeleteBlock/MoveBlock/UpdateBlock/SetBlockAttribute/AddInlineMark/RemoveInlineMark/MoveDrawingObject/CreateRevision/AcceptRevision), Target:{BlockId,SectionId,InlineIndex,InlineId,…}, Metadata, Text, Block, … }] } }`.
  - **⚠️ Tvary se NEshodují 1:1** — local op-log batch (`{localSequence, operations}`) vs remote (`{Sequence, SessionId, Batch:{Operations}}`), op `type` je camelCase string vs C# PascalCase enum, `target` má `tableCellId/offset/length/order` vs `BlockId/SectionId/InlineIndex/InlineId`. **B1.2 round-trip test je pojistka;** mapování řešit tenkou JS/C# vrstvou (NE C# diffem). Enum (de)serializace string-case ověřit.
- [x] **B0.3 KONTRAKT — UI state push (JS→C#):** `OnCanvasUiStateChanged(json)` s malým payloadem (jen primitiva, ŽÁDNÉ bloky): `{ dirty:bool, canUndo:bool, canRedo:bool, wordCount:int, pageCount:int, formatting:{…stejné jako getFormattingStateJson…}, selection:{ collapsed:bool, blockId, hasComment, … } }`. (B3 rozšíří o `comments[]`+`revisions[]`.) Reuse existující `getFormattingStateJson` payload pro `formatting`.
- [x] **B0.1** Baseline E2E spuštěn na current kódu: `CommentsRevisions` **2/2 zelené** ✓; `Collaboration` **Phase20 (two-editor SignalR konvergence) ČERVENÁ** — `TimeoutException 30s` při čekání na konvergenci vzdáleného editoru.
  - **🔴 ZJIŠTĚNÍ/REGRESE z 8.7c:** collab broadcast běží UVNITŘ `ReconcileCanvasDocumentAsync`, který jsem v 8.7 prodloužil na **2500 ms** debounce (+ change-notify 400 ms) → vzdálený editor konverguje příliš pozdě / test timeoutuje. **B1 to opravuje záměrně** (operation-relay s KRÁTKÝM ~150 ms debounce, oddělený od těžkého dokument-reconcile) → Phase20 musí být zelený jako B1 exit. Do té doby je collab latence zhoršená (cena za typing-smoothness z 8.7; B1 vyřeší obojí).
- [x] **B0.5** Baseline „před B": reconcile (`RequestDocumentAsync` marshal + snapshot + collab diff) ≈ **~700–1000 ms/pauza** na 150 odst.; collab broadcast latence ≈ **~2900 ms** (400 ms change-notify + 2500 ms reconcile). Cíl B6: reconcile = 0 (mirror pryč); cíl B1: collab latence ~150–300 ms.
- **Exit:** kontrakty zapsané (B0.2/3) ✓; op-log charakterizace zelená (B0.4, Node 312) ✓; baseline změřen (B0.5) ✓; **víme co opravit v B1** (collab latence + table redundant/spurious ops). POZN: Phase20 collab červený = vstupní bod B1, NE blocker B0.

---

## Fáze B1 — Kolaborace přes operation-relay (zrušit C# diff)

Cíl: lokální změny se posílají kolaborátorům jako operace **z op-logu enginu**, ne z C# diffu `_document`. Tím odpadne hlavní důvod existence mirroru.

**🔑 ROZHODNUTÍ TRANSPORTU (ověřeno):** canvas bloky NEpřežijí silně typovaný C# `DocumentOperation.Block`/`DocumentBlock` (jiný tvar `content`, `DocumentEditorJson.Options` nemá string-enum). **C# = HLOUPÁ ROURA**: relayuje op-log batch jako **opaque JSON** v novém poli `DocumentOperationBatch.CanvasOperationBatchJson`; remote `ApplyRemoteOperationBatchAsync` ho pošle RAW do JS `applyRemoteOperationBatch` (který canvas bloky konzumuje přímo — B1.2). Žádná změna provider interface.

- [x] **B1.0 (TDD, JS) — fix diffModels u tabulek (z B0.4):** `flattenBlocks` NErekurzuje do buněk → editace tabulky = JEDEN `updateBlock(tabulka)` (žádné redundantní cell ops, žádný spurious moveBlock). Op-log + transform sada **18/18**, Node **312/312**.
- [x] **B1.1 (TDD, JS):** op-log `takeLocalBatches()` (vrátí + vyprázdní `pendingLocalBatches`) + interop export `takeLocalOperationBatchesJson(handle)`. Node test (B1.1) zelený.
- [x] **B1.2 (TDD, JS):** `collaboration/__tests__/op-relay-roundtrip.test.mjs` — engine op-log batch → `applyRemoteOperationBatch` → konverguje pro **insertText/deleteText/insertBlock/deleteBlock/updateBlock(paragraph/table/image)** = **7/7**. Dokázáno: C# nemusí rozumět operacím.
- [x] **B1.3a (C# model + transport test):** přidáno `DocumentOperationBatch.CanvasOperationBatchJson` (opaque payload). C# unit `PhaseBOperationRelayTransportTests` (3) — payload přežije serialize round-trip (i tabulka), `DocumentCollaborationOperationBatch` transport, null vynechán (zpětně kompatibilní).
- [x] **B1.3b (C#):** `TmDocumentCanvasEngineHost.TakeLocalOperationBatchesAsync()` → volá JS `takeLocalOperationBatchesJson`, parsuje JSON array → `IReadOnlyList<string>` (raw JSON každého batche).
- [x] **B1.4 (C#):** `TmDocumentEditor.RelayLocalOperationsAsync()` → drainuje batche + `SubmitLocalBatchAsync(new DocumentOperationBatch{ DocumentId, CanvasOperationBatchJson })` přes `_collaborationSync` — **bez** `CreateLocalEditBatch`/`_document`. Volá se z `HandleCanvasEngineChangedAsync` (change-notify ~400 ms). Reconcile už NEbroadcastuje (collaborationBefore=null když relay on). **Nutná oprava `SubmitLocalBatchAsync`** (Abstractions): broadcastovat i s prázdnými typed Operations když je `CanvasOperationBatchJson` set (jinak no-op skip); C# applier jen pro typed ops.
- [x] **B1.5 (C#):** Feature-flag `_useEngineOperationRelay` (default true). Remote apply: `ApplyRemoteOperationBatchAsync` RAW větev (když `CanvasOperationBatchJson` set → pošli raw do JS); + `HandleRealtimeRemoteOperationBatchAsync` má relay-větev (aplikuje canvas-relay batch rovnou přes engine, obejde C# applier/early-return).
- [x] **B1.6 (E2E):** **Phase20 collaboration ZELENÝ — kolaborace funguje OBĚMA směry přes operation-relay** (`DocumentEditorCanvasCollaborationE2ETests` 3/3). Cestou jsem instrumentovaným 2-tab probe (`data-canvas-input-revision` + `activeElement` + `elementFromPoint` + `data-canvas-pointer-*`) odhalil a OPRAVIL samostatný pre-existing bug:
  - 🎯 **ROOT CAUSE:** **presence overlay (remote caret druhého uživatele) zachytával kliky.** Vzdálený kurzor se vykresluje na KONCI textu — přesně kde lokální uživatel klikne, aby pokračoval v psaní. `elementFromPoint` ukázalo `document-canvas-remote-caret` na místě kliku → klik trefil overlay (NE canvas) → mousedown handler na `root` neproběhl (pointer atributy prázdné) → hidden input se nezafokusoval → uživatel nemohl psát. Vzorec „kdo přijal remote edit nemůže psát" = vzdálený caret na konci jeho textu.
  - ✅ **FIX:** `presence-overlay.mjs` root → `style.pointerEvents = 'none'` (presence je čistě dekorativní). Po opravě: `elementFromPoint` propadne na canvas, mousedown proběhne (`hitBlock='canvas-typing-body'`), focus na `document-canvas-hidden-input`, příjemce napíše (rev 0→3, text konverguje). **Byl to pre-existing bug (8.7-éra), postihoval legacy I relay; teď opraven pro obě.**
  - Regresní test `ReceiverCanTypeAfterRemoteApply_PresenceCaretDoesNotBlockClicks` (hlídá pointer-events:none + příjemce po remote editu klikne+napíše).
- [x] **B1.6b** — vyřešeno v B1.6 (presence pointer-events fix).
- [~] **B1.7 (cleanup):** Legacy `CreateLocalEditBatch`-diff (`BroadcastLocalCollaborationChangeAsync`) PONECHÁN jako dormant fallback za `_useEngineOperationRelay=false`. Plné smazání + odpojení od `_document` = součást **B6** (kde mizí mirror). Aktivní cesta už C# diff NEpoužívá.
- **✅ Exit SPLNĚN:** collab funguje **obousměrně přes operation-relay bez C# diffu** (aktivní cesta); Phase20 **3/3**; Node **320**, collab unit **35/35**, transport **3/3**, psaní/perf E2E **8/8**. Cestou opraven pre-existing presence-overlay pointer-events bug.

### B1 fallout (mirror-staleness) — nalezeno širším E2E sweepem při B2 verifikaci, OPRAVENO (2026-06-09)

> Relay (`_useEngineOperationRelay=true`) přestal udržovat C# mirror `_document` per-edit (reconcile je debounced 2500 ms). Flow, které ČTOU/MUTUJÍ `_document` a pushují ho zpět, pak jely na zastaralém snapshotu. B1 exit (jen Phase20 collab) to nezachytil. „Vše musí fungovat" → opraveno jako bridge, než B3–B6 tyto flow zmigrují pryč od mirroru.

- [x] **B1.8 (FIX, C#):** **Before-unload guard timing** (`Phase12_HistoryManualSaveReloadAndCategorySmoke`). Edit → dirty-status se vyrenderoval (~600 ms toolbar-sync), ale guard se aktivoval až v 2500 ms reconcile → test selhal na ř.40. Fix: `DebouncedCanvasToolbarSyncAsync` po `SyncCanvasEngineStateAsync` volá `SyncAutosavePendingAction()` + `UpdateBeforeUnloadGuardAsync()`; guard je teď idempotentní (`_beforeUnloadGuardActive` cache → přeskočí redundantní interop), takže je levné ho volat z každého toolbar-sync passu.
- [x] **B1.9 (FIX, C#):** **Revision review na stale mirroru** (`Phase17_CommentsRevisionsAndRestrictedEditing`, ř.162). `ReviewRevisionAsync`/`ReviewAllRevisionsAsync` mutovaly stale `_document` a `ReplaceDocumentAsync(_document)` clobbnul canvas → marker po accept nezmizel. Fix: nový helper `EnsureCanvasMirrorCurrentAsync()` (pull engine → mirror bez side-efektů, MarkDocumentMounted, SyncComments) volaný na začátku obou review metod; `ReconcileCanvasDocumentAsync` ho teď taky používá (DRY). **POZN:** `BroadcastLocalCollaborationChangeAsync(before,after)` v revizích je pod relayem potenciálně redundantní (ops by to taky odnesly) — necháno pro **B3** (migrace revizí na push/ops), single-client testy to neřeší.
- [x] **B1.10 (FIX, C#, defenzivně):** `ReviewSuggestionAsync` taky čte/hashuje `_document` (`ComputeSnapshotHash` base-match) → pod relayem by stale mirror falešně hlásil suggestion-conflict. Přidán `EnsureCanvasMirrorCurrentAsync()` na začátek (minimální vložení, bez restrukturalizace). Suggestions-under-canvas nemá dedikovaný E2E → plná migrace = **B4**.
- **Audit push-back (`ReplaceDocumentAsync(_document)`):** sites 1524 (document LOAD — `_document` čerstvý, safe), 5632 (SAVE — Phase12 ověřuje, save pulluje fresh, safe), 6867/6955 (revize — fixnuto B1.9). Žádné další nechráněné.
- **Ověřeno (širší E2E sweep, full build):** Phase9 + Phase13 + Phase12 + Phase17 + EndToEndTyping + HumanTyping + Collaboration = **11/11 zelené**. Batch 2 (ImportExport, ModelRoundtrip, Clipboard, Fields, ContentControls, Cutover) **9/9 zelené**. Batch 1 (CaretSelection, Paragraph, HistorySave, Styles, OverlapPerf) zelené.
- ⚠️ **PRE-EXISTING (NE má regrese):** `DocumentEditorCanvasAutocorrectE2ETests.PhaseE10_…` padá na ř.57 (drag-select na `canvas-e10-painter-source` nezaregistruje výběr). **Ověřeno na čistém HEAD buildu (stash+rebuild) — padá identicky bez jakýchkoli mých změn.** Selection wait je canvas-side DOM, nezávislé na mých změnách. Samostatný pre-existing bug formát-painter selectu — mimo Phase B scope.

---

## Fáze B2 — Push malých UI hodnot (formatting/dirty/undo/word-count) z enginu

Cíl: toolbar pressed-state, dirty, undo/redo, word/page count přicházejí jako **malý push**, rychle a bez plného dokumentu.

> **⚠️ KOREKCE DIAGNÓZY (2026-06-09):** Původní předpoklad „pomalý bold + 2 spadlé testy kvůli zpoždění reconcile" byl **chybný**. Skutečnost zjištěná bisectem:
> - `Phase9_InlineFormatting…` padal na **ř.113 (ctrl+click otevři odkaz)**, NE na bold/formatting toolbaru — bold/font/color/highlight kontroly (ř.40–101) **procházely**.
> - `Phase13_ToolbarContextMenuAndSpellcheck…` padal na **ř.61 (klik na spell-suggestion v kontextovém menu)**, NE na bold.
> - Příčina obou: v 8.7 jsem **debouncoval i `onSelectionChange` (mini-toolbar / `OnCanvasMiniToolbarChanged`)** na 400 ms. To je regrese — DELIBERATE výběr (rozsah/objekt) je nízkofrekvenční, řídí plovoucí mini-toolbar a je kotvou pro navazující pointer-akce (ctrl+click odkaz, pravý klik na překlep). Když přijde do .NET pozdě, ty interakce selžou.
> - Stash-bisect: reverted build (sync selection) → **oba testy zelené**; jakýkoli selection-debounce (i 32 ms) → **oba červené**. Change-notify debounce (psaní) je v pořádku a byl zelený už v HEAD.

- [x] **B2.0 (FIX regrese, TDD, JS):** `interop.mjs notifySelectionChanged` rozděluje kadenci: **deliberate** výběr (`isVisible===true` nebo `selection.isCollapsed===false`) → do .NET **OKAMŽITĚ** (zruší pending collapsed timer); **collapsed** caret (psaní/šipky) → debounce 400 ms (psaní zůstává plynulé). Rozhodovací predikát extrahován do `selection-cadence.mjs` (`isDeliberateSelectionNotification`) + **5 unit testů** `selection-cadence.test.mjs`. **Ověřeno:** Phase9 + Phase13 + EndToEndTyping + HumanTyping + Collaboration (Phase20 3/3 + presence regrese) = **7/7 E2E zelené** na plném buildu; Node **325/325**; nový modul servírován (200) a in-browser import funguje.
- [x] **B2.5 (oprava 2 spadlých testů):** Splněno přes B2.0 — `Phase9_InlineFormatting…` i `Phase13_ToolbarContextMenuAndSpellcheck…` jsou zelené a slouží jako E2E regresní brána (vrátí-li se selection-debounce, spadnou).

> **Zbytek B2 (níže) je po B2.0 už jen OPTIMALIZACE, ne bugfix.** Stav po fixu: plovoucí mini-toolbar = okamžitý; hlavní toolbar pressed-state dohání přes `CanvasToolbarSyncDebounce` (200 ms) + 3 interop pully ve `SyncCanvasEngineStateAsync`. Původní „pomalý bold" (vteřinové O(doc) round-tripy) byl vyřešen už minulou session (O(1) handler + debounce). Push-payload sebere posledních ~200 ms a 3 round-tripy, ale není nutný pro funkčnost. Drag-select navíc střílí víc deliberate notifikací → push (formatting už v payloadu) je čistší než opakované interop pully + render-gate je ochrání.

- [x] **B2.1 (TDD, JS):** `format-state.mjs buildFormattingState(commandState)` — čistá funkce co tvaruje `queryCommandState` výstup do plochého formatting recordu (sdílí ji interop pull `getFormattingStateJson` i push). `interop.mjs buildUiState(state)` = `{ formatting, isDirty, canUndo, canRedo, pageCount(z lastLayout.pages.length), modelVersion }`, vše O(výběr)/O(1). **6 unit testů** `format-state.test.mjs` (default/active/mixed/font+barvy/paragraph-fallback + jen-primitiva). **Pozn:** wordCount NEpočítán v JS (O(model), složitý přes tabulky/runy, a C# už ho má cached/ref-gated) → zůstává C#-side, migrace na engine = **B6**.
- [x] **B2.2 (JS):** Push jede přes existující `OnCanvasMiniToolbarChanged` selection event (ne nový callback) — `payload.uiState = buildUiState(state)`. **Připojeno JEN na deliberate výběr** (`isDeliberateSelectionNotification(payload)`); collapsed caret (psaní/šipky/selection-reset během `ReplaceDocumentAsync`) ho NEnese → žádná per-event marshalling režie pro nic. Důvod viz B2.3 pozn.
- [x] **B2.3 (C#):** Host: `CanvasEngineUiState` (Formatting=`CanvasEngineFormattingState` + IsDirty/CanUndo/CanRedo/PageCount/ModelVersion) + `OnUiStateChanged` Func param; `OnCanvasMiniToolbarChanged` vyzobne `CanvasEngineUiStateEnvelope` a dispatchne. `TmDocumentEditor.HandleCanvasUiStateChangedAsync` → `ApplyCanvasFormattingState(uiState.Formatting)` (extrahováno ze `SyncCanvasEngineStateAsync`, sdíleno) + render-gated `StateHasChanged`. **Aplikuje JEN formatting** (dirty/undo/pageCount zůstávají na stávajících cestách — jinak by push pre-fillnul chrome-signature a rozbil render-gate toolbar-syncu). **Guard:** přeskočí během `_isReviewingRevision/_isReviewingSuggestion/_isSaving` (ty jedou `ReplaceDocumentAsync` co emituje selection event; render uprostřed flow korumpoval canvas update).
  - 🐛 **Cestou nalezena+opravená regrese:** první verze pushe (na VŠECHNY selection eventy + aplikace dirty/undo + vlastní render) shazovala `Phase17` ř.162 **pod zátěží** (revision-accept marker nezmizel do 10 s). Bisect (live `false &&` disable pushe → 6/6 zelené) → příčina = per-selection-event režie (větší payload + dvojí deserialize + buildUiState) na collapsed eventech revision flow. **Fix = deliberate-only attach (B2.2) + formatting-only apply + heavy-flow guard.** Po fixu 4-class load **6/6**, full confirmation **11/11**.
- [~] **B2.4 (C#) — KONZERVATIVNÍ, pully PONECHÁNY:** Push je **přidaná prompt cesta** pro formatting na deliberate výběr (okamžité pressed-state). `SyncCanvasEngineStateAsync` (4 interop pully + navigation/outline + content-control + command-registry) **ponechán** — volá ho ~15 míst (command-driven refresh) a refreshuje i věci mimo uiState (outline, active-heading). Plné odstranění pullů = riskantní bez UX přínosu (redundantní pull 200 ms po pushe je neviditelný) → **odloženo na B6** (kde mizí mirror + zjednoduší se cesty). Formatting pull v toolbar-syncu je teď redundantní-ale-neškodný (render-gate ho zahodí).
- [x] **B2.6 (bold/selection responzivnost):** Výběr tučného textu → toolbar Bold pressed-state přijde **promptně přes push** (ne 200 ms toolbar-sync). Ověřeno Phase9 (`document-bold` aria-pressed po výběru) + 11/11 confirmation. Psaní dál plynulé (push na deliberate-only → collapsed/typing ho nenese; EndToEndTyping/HumanTyping zelené).
- **Exit:** formatting pressed-state z pushe (prompt na deliberate výběr); psaní plynulé; 2 dříve spadlé testy zelené (B2.0); Phase17 zelený pod zátěží. **wordCount + pull-removal = B6.**

---

## Fáze B3 — Komentáře a revize/track-changes přes pushnutá data

Cíl: comment rail + revision panely čtou data pushnutá z enginu, ne z `_document.Comments`/revizí.

- [x] **B3.1 (TDD, JS):** `annotations-state.mjs extractAnnotations(model)` (čistá fce, vytáhne `comments`+`revisions` z engine modelu; tolerantní na camel/PascalCase) + `interop.mjs getAnnotationsJson(handle)` (light pull, NE full-doc marshal). **4 unit testy** `annotations-state.test.mjs`. Node 331→**335**. Pozn: comments/revisions už round-trippují jako kanonické C# `DocumentComment`/`DocumentRevision` (`CanvasDocumentModel.Comments/Revisions`) → deserializují přímo.
- [x] **B3.2 (C#) — revision review plně přes engine + panel decoupled:**
  - Host `GetAnnotationsAsync()` → `CanvasEngineAnnotations{Comments,Revisions}`; `PullCanvasAnnotationsAsync()` (C#) plní `_comments` + nové `_canvasRevisions`.
  - **Revision panel binding decoupled:** `Revisions="@(_canvasRevisions ?? _document?.Revisions ?? [])"`; `PendingRevisionCount`/`HasPendingRevisions` → `DisplayedRevisions`. `_canvasRevisions` plní review (PullCanvasAnnotationsAsync) + `EnsureCanvasMirror` (reconcile) + reset na load.
  - **Accept/reject single I all ROUTOVÁNO DO ENGINU** (`acceptrevision`/`rejectrevision` + `acceptallrevisions`/`rejectallrevisions` command přes `ExecCommandAsync` + `PullCanvasAnnotationsAsync`) → **ZRUŠENY 2× full-doc round-tripy** (`EnsureCanvasMirror` pull + `ReplaceDocumentAsync` push). Rychlé, bez mirroru; collaboration jede přes relay.
  - **Comments:** stále přes `SyncCommentsFromRuntimeDocument` (EnsureCanvasMirror) — plné odpojení = B6.
- [x] **B3.2b (FIX engine bulk-review marker bug, TDD):** `revision-render.mjs buildRevisionMarkers` přidával marker pro KAŽDÝ `revisionAnchor` command i když revize už NENÍ pending (accept-all/reject-all nechalo stale anchory → markery nikdy nezmizely → Phase17 ř.167 padalo). **Fix:** marker reprezentuje JEN pending revizi — `if (!revision) continue;` (přeskoč anchor command pro non-pending revizi). Node test `revisions.test.mjs` „reviewing all revisions renders no markers, even when stale revision anchors remain" (reprodukoval bug: 3 markery → po fixu 0). Node 335→**336**.
- [x] **B3.3 (E2E):** Non-flaky sada (InlineFormat/ToolbarSpellcheck/HistorySave/EndToEnd/HumanTyping/Collaboration) **10/10 zelená** (2×); Node 336. **Revision-review flakita VYŘEŠENA** (full engine-routing + marker fix).
- [x] **B3.4 (FIX Phase17 flaky bod ř.174 — diagnostikováno + opraveno):** Diagnostika (capture `elementFromPoint` + selection-state na timeout) odhalila ROOT CAUSE: klik na `canvas-phase17-protected` ofs.3 trefil **`document-undo` toolbar tlačítko** — blok byl na Y≈61 **ZA sticky toolbarem** (jeho screen-Y kolísá podle scroll/layoutu po předchozích editech), klik nedošel na canvas, caret se nehnul (`focusBlock` zůstal `canvas-phase17-review`). NE logický/engine bug, NE revize — **test-robustness mezera** (klikalo se na spočtenou souřadnici bez záruky že není pod toolbarem). **Fix:** `ClickCanvasBlockAsync` před klikem `scrollIntoView({block:'center',inline:'center',behavior:'instant'})` na blokový text-rect (stejný vzor jako ImageE2ETests/ShapesDrawingsE2ETests). **Stress-test 11× (8+3) → 11/11 PASS.**
- **Exit SPLNĚN:** revize plně přes engine bez mirror round-tripů + panel decoupled; engine bulk marker bug opraven (Node-tested); revision-review flakita pryč; **Phase17 stabilní (11/11)**. **OTEVŘENÉ pro pozdější fáze:** comments plné odpojení od mirroru = **B6** (potvrzeno: comment rail dnes potřebuje reconcile — 30s reconcile diag rozbil comment assert ř.91).

---

## Fáze B4 — Suggestions (návrhový režim) bez C# before/after diffu

Cíl: `TryCreateSuggestionAsync(before, after)` dnes diffuje dva C# dokumenty → potřebuje mirror. Přepnout na engine-emitované „suggested change" události NEBO pull-on-demand snapshot jen pro tvorbu návrhu.

> **⚠️ ZJIŠTĚNÍ (2026-06-09): document-suggestion režim (`IDocumentSuggestionProvider`/`DocumentSuggestion`) je WYSIWYG-ONLY feature, NEW NENÍ zapojen pro canvas engine.** Důkaz:
> - `TryCreateSuggestionAsync(before,after)` (diff) volán JEN z wysiwyg cest: `HandleDocumentChangedAsync`, `HandleWysiwygPatchAsync` (bind `DocumentPatchGenerated` na wysiwyg hostu, .razor:678), `HandleWysiwygSnapshotAsync`. **Canvas change-notify (`HandleCanvasEngineChangedAsync`) suggestion NEVYTVÁŘÍ.**
> - `ReviewSuggestionAsync` modifikuje `_document` přes `DocumentOperationApplier`, ale **NEpushuje na canvas** (žádný `ReplaceDocumentAsync`) → pro canvas nefunkční.
> - Žádný demo page nepředává `SuggestionProvider` do `TmDocumentEditor`; **žádný canvas E2E nevytváří document-suggestion** (jen SPELL suggestions přes proofing).
> - **Canvas engine používá pro „navrhované úpravy" track-changes / revize (DocumentRevision) — vyřešeno v B3.** To je canvas ekvivalent suggestion režimu.
>
> **DŮSLEDEK pro B-sérii:** suggestions NEblokují smazání canvas mirroru (B6) — na canvas cestě se nevytvářejí ani nepushují, tedy průběžný canvas mirror nečtou. `_suggestionSnapshot = Clone(_document)` + `ReviewSuggestionAsync` EnsureCanvasMirror (B1.10 defenzivní) jsou v canvas kontextu mrtvý/wysiwyg kód → B6 je může nechat/odstranit.

**ROZHODNUTÍ usera (2026-06-09): implementovat canvas suggestion režim, backovaný track-changes** (revize = engine ops + inline overlay + accept/reject, hotovo v B3 — to JE canvas-native „propose+review edit" mechanismus, Google Docs model „Suggesting = track changes"). Sjednocuje track-changes a suggestion toggly pro canvas.

- [x] **B4.1 (Slice 1 — engine wiring, HOTOVO + bez regrese):** nová property `CanvasEngineTracksChanges => _trackChangesEnabled || (UsingCanvasEngine && _suggestionsEnabled)`; canvas host param `TrackChangesEnabled="@CanvasEngineTracksChanges"` (místo `EffectiveTrackChangesEnabled`). Když host zapne suggestion režim (`SuggestionsEnabled`+`SuggestionProvider`) na canvas enginu → engine track-changes ON → edity se stávají revizemi (inline overlay + accept/reject přes engine, B3). **No-op když suggestions vypnuté** (== `_trackChangesEnabled`) → existující testy beze změny (matematicky identické). Ověřeno: build OK, Phase17 (track-changes) + EndToEndTyping zelené. Toolbar track-changes tlačítko (`EffectiveTrackChangesEnabled`) ZŮSTÁVÁ oddělené (nesvítí v suggestion režimu).
- [x] **B4.2 (Slice 2 — UI surfacing + accept/reject routing + E2E, HOTOVO):**
  - `DisplayedSuggestions` property: v canvas suggestion režimu (`IsCanvasSuggestionMode = UsingCanvasEngine && _suggestionsEnabled`) mapuje pending `_canvasRevisions` → `DocumentSuggestion` (`MapRevisionToSuggestion`: Id=revision.Id, Type Insertion→InsertText/Deletion→DeleteText/Formatting→Formatting, Range/Author přímo — `DocumentRevisionAuthor : DocumentEditorAuthor`, `DocumentSuggestion.Range` = `DocumentRevisionRange`, Action→Status); jinak `_suggestions`.
  - Suggestion panel `Suggestions="@DisplayedSuggestions"`; v canvas suggestion režimu skryt revision panel (`@if (!IsCanvasSuggestionMode)`) — žádné duplicitní listy; `ShowRevisions` zahrnuje `IsCanvasSuggestionMode` (tab dostupný i bez track-changes feature).
  - Accept/reject routing: `AcceptSuggestionAsync`/`RejectSuggestionAsync` → `ReviewSuggestionRoutedAsync` → v canvas suggestion režimu najde revizi v `_canvasRevisions` dle suggestion.Id a routuje na `ReviewRevisionAsync` (engine accept/reject z B3); jinak provider `ReviewSuggestionAsync`.
  - **E2E harness:** `CanvasEngineHostPage` + `?suggestionMode=true` query-param (inject `DemoDocumentSuggestionProvider`, `SuggestionProvider`+`SuggestionsEnabled` gated). **E2E `DocumentEditorCanvasSuggestionModeE2ETests`**: edit v suggestion režimu (1 znak) → tracked insertion revize (inline) → suggestion panel ji ukáže (`document-suggestion-item`) → Accept → routováno na engine revision review → marker zmizí (count 0). **ZELENÝ.** Screenshoty potvrzují UX = Google Docs „Suggesting" model: vložený znak inline tracked + panel „Suggestions" (Insert / Canvas Demo User / Accept-Reject) + „1 pending changes" banner; po Accept aplikováno + „No pending suggestions".
- **Exit SPLNĚN:** canvas suggestion režim plně funkční přes track-changes (engine ops + inline overlay + panel + accept/reject přes engine), bez průběžného mirroru. **Ověřeno: B4 E2E + regrese (CommentsRevisions/InlineFormat/EndToEndTyping/SuggestionMode = 4/4).** No-op pro existující testy (žádná canvas page nemá SuggestionProvider).

---

## Fáze B5 — Save / export / compare = potvrzeně pull-on-demand

Cíl: ověřit (a doplnit), že persistence/export/compare netáhne z průběžného mirroru, ale čerstvě.

> **ZJIŠTĚNÍ (2026-06-09): všechny tři už JSOU pull-on-demand** (code audit). Save i export/compare táhnou čerstvý model z enginu v okamžiku akce, NE z průběžného mirroru:
> - **Save** `SaveCoreAsync` (ř.1192): `_canvasHost.RequestDocumentAsync()` + `CreateProviderBoundarySnapshot(preserveImageBlocks)`.
> - **Export PDF/DOCX + Compare** vše přes `GetCurrentDocumentForProviderExportAsync()` (ř.1668): `_canvasHost.RequestDocumentAsync()` čerstvě. Compare: `OpenCompareDialogAsync` → `CreateCanvasExportBridge().RequestSnapshotAsync()` → tatáž metoda. Export bridge `ExportPdfAsync`/`ExportFormatAsync` → tatáž metoda.
> - Jediná `_document` závislost = `Can*` gaty (`CanExportPdf`/`CanCompareDocuments`: `_document is not null`) — potřebují jen NON-NULL, ne průběžně-čerstvý. B6 nechá `_document` jako lazy non-null snapshot (z loadu) → gaty fungují.

- [x] **B5.1 (E2E):** edit → Save → reload → obsah zachován = `Phase12_HistoryManualSaveReloadAndCategorySmoke` (HistorySave). Zelený.
- [x] **B5.2 (E2E):** export DOCX/PDF reflektuje NEULOŽENÉ edity (pull-on-demand) = `Phase19_CanvasDocxPdfImportExport_UsesCurrentCanvasModelAndKeepsFirstPaintSane` (napíše marker bez save → ověří že DOCX i PDF export marker obsahují). Zelený.
- [x] **B5.3 (compare):** Phase19 manifest: „compare uses the current canvas snapshot" — `OpenCompareDialogAsync` táhne čerstvě přes export bridge (code-audit potvrzeno). Pokrytí přes Phase19 + audit.
- **Exit SPLNĚN:** save/export/compare = pull-on-demand (audit + Phase12/Phase19 E2E, **5/5 zelené** po B1–B4), žádná závislost na PRŮBĚŽNÉM `_document` (jen non-null gaty → B6-safe). Žádná oprava nebyla potřeba — pull-on-demand byl implementován už v B1/dřív.

---

## Fáze B6 — Smazat per-edit reconcile + průběžný `_document` mirror (PAYOFF)

Cíl: nikdo už nečte průběžný mirror → smazat `ReconcileCanvasDocumentAsync` per-pauza marshal. `_document` = lazy snapshot tažený jen na save/export/load/blur/compare.

- [x] **B6.1 (C#):** ✅ SMAZÁNO `ScheduleCanvasDocumentReconcile`/`DebouncedCanvasReconcileAsync`/`ReconcileCanvasDocumentAsync` + dead field `_canvasReconcileSeq` + const `CanvasDocumentReconcileDebounce` + `_canvasReconcileSeq++` z Dispose. `HandleCanvasEngineChangedAsync` je teď O(1) async: dirty z payloadu, `_canvasModelChangedSinceSync=true`, autosave register, before-unload guard, `StateHasChanged`, relay. ŽÁDNÝ per-edit marshal.
- [x] **B6.2 (C#):** ✅ Místo těžkého reconcile: (a) **save/export/compare** = pull-on-demand přes `RequestDocumentAsync` (už z B5, ověřeno auditem — `_document` závislost jinde = jen non-null gaty); (b) **word count + page count** pushnuté z enginu (`getAnnotationsJson` vrací `wordCount=countModelWords(model)` + `pageCount=lastLayout.pages.length` → `_canvasPushedWordCount`/`_canvasPushedPageCount`, `DocumentWordCount`/`DocumentPageCount` je preferují pod canvasem); (c) **comments + revisions** z lehkého `PullCanvasAnnotationsAsync` (engine `extractAnnotations`); pull běží jen po skutečné editaci přes gated blok v `DebouncedCanvasToolbarSyncAsync` (`_canvasModelChangedSinceSync && !review && !save`).
- [x] **B6.3 (E2E):** ✅ `DocumentEditorCanvasEndToEndTypingE2ETests` + `HumanTypingE2ETests` zelené — psaní bez .NET full-marshalu (reconcile cesta smazána, žádná `RequestDocumentAsync` continuation během psaní; marshal jen na save/export/compare). Žádný ~700ms zásek na pauze.
- [x] **B6.4 (E2E):** ✅ human-cadence typing scénář zelený (HumanTyping + EndToEndTyping), plynulé bez skoků; autosave-pending se renderuje promptně na change-notify (oprava Phase12 — fix přidán u B6).
- **Exit:** ✅ HOTOVO — reconcile pryč; per-úhoz i pauzy bez .NET marshalu; save/collab/comments/revisions/word-count/page-count fungují přes nové cesty (pull-on-demand + engine push + lehký annotation pull). Ověřeno: 5/5 finální E2E (HistorySave+autosave, CommentsRevisions/Phase17, EndToEndTyping) po cleanup buildu; dřív celá sada 13/13 + Node 338. Build čistý (žádné dead-code warningy z reconcile).

---

## Fáze B7 — Render chrome: izolace per-edit renderu do child komponent

**Diagnóza POTVRZENA (2026-06-09, analýza videa):** psaní lidskou kadencí → periodické **freeze 340–565 ms na hranicích slov** (medián mezi snímky 132 ms). Několik snímků s NULOVOU změnou pixelů (vlákno zablokované) + nejdelší zámrz následovaný catch-upem 4 znaků naráz („obca" se objevilo v jednom snímku). Příčina NENÍ překreslení canvasu (ten je JS-owned, Blazor na něj nesahá) — je to **contention o jediné WASM/UI vlákno**: když vyprší ~400 ms JS notify-debounce (= mezislovní pauza), `HandleCanvasEngineChangedAsync` zavolá `StateHasChanged` na **obřím rodiči** `TmDocumentEditor`, který přepočítá render fragment s **~120 parametry toolbaru + ~13 status baru + inline mini-toolbarem + panely** (~200 ms BuildRenderTree). Po tu dobu JS nestihne namalovat úhozy → zamrznutí.

**Cíl:** per-edit aktualizace chrome (pressed-state, dirty, word/page count, undo-state, autosave-pending) **nesmí re-renderovat rodiče**. Volatilní stav přesunout do child komponent, aktualizovat imperativně přes `@ref`.Refresh() (= `StateHasChanged` jen na tom childu). Rodič se re-renderuje JEN na strukturální změny (panel open/close, load dokumentu, přepnutí engine/mode).

**Současný stav (kódem ověřeno):** `TmDocumentEditorToolbar` (`@ref="_toolbar"`, ř.56) + `TmDocumentEditorStatusBar` (ř.879, **bez @ref**) UŽ jsou child komponenty, ALE dostávají volatilní hodnoty jako `[Parameter]` výrazy (`BoldState="@_formattingState.Bold"`, `IsDirty="@_isDirty"`, `WordCount="@DocumentWordCount"`, …) → parent render je musí všechny vyhodnotit. **Mini-toolbar je inline v parent .razor** (ř.893–1000+, čte `_formattingState`) → re-renderuje se vždy s rodičem. `ComputeChromeStateSignature()` (ř.8415) existuje, ale `ShouldRender()` (ř.407) ho NEpoužívá a hot-path render (ř.8326) je negated.

**⚠️ Hlavní Blazor past (proč to dřív rozbíjelo testy — viz B2.3):** když child dostane vlastní mutovatelný stav A ZÁROVEŇ má `[Parameter] BoldState`, tak budoucí parent render přepíše pushnutý stav (možná) zastaralou hodnotou parametru. **Kontrakt:** rodič drží `_formattingState`/`_isDirty`/… dál aktuální (levné přiřazení pole), na hot-path NErenderuje (push přes child Refresh), a na strukturálním parent renderu si child v `OnParametersSet` re-syncne ze stejných (aktuálních) hodnot → nikdy se neperou. Push JEN na deliberate stav (ne na každý collapsed selection event — jinak režie + Phase17-pod-zátěží regrese, viz B2.3 poučení).

---

**🟢 VÝSLEDEK B7 (2026-06-09): freeze VYŘEŠEN samotnou B7.1 (rychlá výhra). Komponentová izolace B7.3–B7.7 NEBYLA POTŘEBA.** Měřením (`DocumentEditorCanvasParentRenderGateE2ETests` — psaní 4 slov s mezislovními pauzami > debounce) se ukázalo, že parent rendery během psaní **NEpocházely z canvas handlerů** (instrumentace render-cause čítači: `cn=1; ts=2; mt=0; ui=0` — change-notify/toolbar-sync/mini-toolbar/uiState dohromady jen ~3×). **Skutečný viník = `@onkeydown="HandleEditorKeyDownAsync"` na root divu: Blazor po KAŽDÉM `@on*` handleru implicitně volá `StateHasChanged`** → běžná psací klávesa (kterou handler ignoruje, text řeší canvas input surface) → 1 plný parent render (~200 ms) PER ÚHOZ → ~26 renderů/burst → zámrz plátna + catch-up. **Fix B7.1d:** `HandleEditorKeyDownAsync` na začátku detekuje nehandled klávesu (`IsHandledEditorKeyDown` — není Escape+floating/palette, není registry command, `GetCommand==None`) a nastaví `_suppressNextChromeRender` → `ShouldRender()` ten jeden implicitní render potlačí (handled zkratky renderují samy, takže nic vizuálního se neztratí). **Výsledek: parentRenders/burst 26 → ≤4** (zbylé = první dirty flip + debounced toolbar sync po pauze, ŽÁDNÝ per-keystroke), engine maluje každý úhoz. **Plný refaktor (child-owned stav + Refresh) by ubral poslední ~3 rendery mimo hot-path za cenu vysokého rizika (param-vs-state past + 768 component testů) → ZAMÍTNUT.** Probe není nový kód — využit existující `data-blazor-render-count` (inkrementuje se per parent render). Diagnostické render-cause čítače byly dočasné, po analýze odstraněny.

---

### B7.0 — Baseline + in-app probe (měření) ✅
- [x] **B7.0a** Diagnóza videa HOTOVÁ (freeze 340–565 ms). Probe = **existující `data-blazor-render-count`** (per-parent-render inkrement) — nový čítač nebyl třeba. Render-cause čítače (cn/ts/mt/ui/kd) dočasně přidány k lokalizaci viníka (= keydown), pak odstraněny.
- [x] **B7.0b** Rozpočet: psací burst → parentRenders ≤ 8 (reálně ≤4; bez fixu ~= počet kláves). Ověřeno `DocumentEditorCanvasParentRenderGateE2ETests`.

### B7.1 — Rychlá výhra: gate hot-path render + collab off render path + keydown render guard ✅ HOTOVO
- [x] **B7.1a** `HandleCanvasEngineChangedAsync`: `await InvokeAsync(StateHasChanged)` obalen signature-gatem (`ComputeChromeStateSignature` before/after). Po prvním znaku dirty true→true → no-op fire se nerenderuje.
- [x] **B7.1b** `RelayLocalOperationsAsync()` puštěn AŽ po paintu — `await Task.Yield()` před relayem, aby canvas repaint nečekal na collab drain/submit interop.
- [x] **B7.1d (HLAVNÍ FIX, doplněno během implementace)** `HandleEditorKeyDownAsync` na začátku přes `IsHandledEditorKeyDown(args)` rozliší editor-zkratku od běžné psací/edit klávesy; pro nehandled klávesu nastaví `_suppressNextChromeRender` a vrátí se → `ShouldRender()` potlačí implicitní post-`@onkeydown` render (Blazor renderuje po každém `@on*` handleru). **Tohle byl skutečný zdroj freezu** (per-keystroke ~200 ms parent render), ne canvas handlery.
- [x] **B7.1c (E2E):** `DocumentEditorCanvasParentRenderGateE2ETests.TypingWithInterWordPauses_DoesNotRebuildParentChrome` — psaní 4 slov s pauzami > debounce → **parentRenders 26 → ≤4** (assert ≤ 8), engine maluje každý úhoz (`inputRevisions >= keys`). Freeze pryč → **B7.3–B7.7 NEPOTŘEBA.**

### B7.2–B7.7 — Komponentová izolace (child-owned stav + Refresh) — ❌ NEPROVEDENO (zbytečné)
B7.1 srazila per-edit parent rendery na ≤4/burst (žádný per-keystroke), čímž freeze zmizel. Plný refaktor (toolbar/status-bar/mini-toolbar vlastní stav + `Refresh()` místo parent `StateHasChanged`) by ubral poslední ~3 rendery, které ale **už nejsou na per-keystroke cestě** (jsou to legitimní chrome updaty po pauze) → marginální přínos za vysoké riziko (Blazor param-vs-state past + 768 component testů). **ZAMÍTNUTO.** Detailní postup viz git historie tohoto plánu, kdyby byl někdy potřeba (např. kdyby přibyly další per-edit chrome změny).

### B7+ — Key-hold zásek (držená klávesa, video 2026-06-10 01-51) ✅ HOTOVO
Po B7 user nahlásil zásek při DRŽENÉ klávese (auto-repeat ~30 ms/klávesa: caret plynule postupuje, pak ~540 ms freeze + catch-up ~10 znaků naráz). Při auto-repeatu se všechny debounce resetují → B7 cesty nejedou; viník = **per-úhozové JS náklady** (CDP profil přes nový diagnostický probe `DocumentEditorCanvasKeyHoldProbeE2ETests` — drží klávesu, sleduje rAF mezery, CDP Profiler + caller-chain agregace). ⚠️ Poučení z profilace: (1) `visitNode`/`pf` v profilu = **Playwright tracing snapshotter artefakt** (PlaywrightTestBase zapíná tracing), NE aplikace; (2) probe MUSÍ resetovat demo dokument (`DocumentEditorE2EReset`) — jinak každý běh přidá ~200 autosaved znaků a profily nejsou srovnatelné; (3) A/B srovnání dělat na stejně zatíženém stroji (Phase12 flake po těžkém probe běhu = zátěž, ne kód).
- [x] **Fix 1 — style-store memoizace:** `ensureStyleStore` re-normalizoval VŠECHNY styly při každém volání (volá se per run per layout přes resolveStyle/findStyle; ~1,5 s/burst). WeakMap memo per model+styles identita + lazy key→styles index pro `findStyle` (invalidace z upsert/rename/delete — jediných in-place mutátorů). +5 Node testů `styles/__tests__/store-memo.test.mjs`.
- [x] **Fix 2 — autocorrect precheck:** `applyAutocorrectAfterTextInput` klonoval CELÝ model 2× (+selection 2×) na KAŽDÝ úhoz před vyhodnocením pravidel (~0,3 s + GC tlak). Nový read-only `couldAutocorrect` zrcadlí triggery pravidel (boundary/whitespace/quote/em-dash kontext/auto-capitalize kontext) → clone jen když může něco matchnout. +2 Node testy (fast-path vrací reference, pravidla stále fungují).
- [x] **Fix 3 — table-layout shallow shift:** `shiftMeasuredBlocks` deep-clonoval blok a vzápětí lines/segments/caretStops stejně nahrazoval mapovanými kopiemi (tabulky nejsou v block-cache → běží per úhoz; ~0,5 s). Nahrazeno shallow copy + new rect; negeometrická data sdílená read-only.
- [x] **Fix 4 — page-placement cache:** `withPagePlacement` (comment-overlay + revision-render) četl `offsetLeft/offsetTop` PER MARKER PER RENDER mezi DOM zápisy overlays = forced reflow ping-pong (~2,0–2,7 s — NEJVĚTŠÍ položka). `canvasStack.getPagePlacements()` = sdílený snapshot; invalidace JEN při změně geometrie: signature z plan dat (mounted pages + rozměry + spacer + zoom, žádné DOM čtení) + ResizeObserver na root (horizontální centrování). Steady typing = **0 forced reflows**. Overlay fallback na přímé čtení zůstal (Node testy).
- **Výsledek (probe, čistý dokument, ~40 s hold):** rAF mezery >100 ms BĚHEM držení: 18 → **0** (zbývá 1× ~0,6 s warm-up na začátku psaní = deferred proofing/a11y analýza, jednorázové — follow-up kandidát; + settle po puštění klávesy). getPagePlacements/withPagePlacement/style-store/table-clone/autocorrect-clone všechny PRYČ z top-25 profilu; GC 1,05→0,79 s.
- **Ověřeno:** Node 347/347 (338+9 nových); E2E s fixy: EndToEnd/Human/ParentRenderGate/InlineFormat/CommentsRevisions 5/5, Table+Styles ✓, Phase12 4/4 (na klidném stroji; pod zátěží flake i na baseline), OverlapPerf ContractDemo ✓ (overlap 0), PhaseE10 = pre-existing fail (stejné místo ř.57 jako před fixy). **Follow-up kandidáti:** history-controller `clone` v recordTextInput (~0,4 s/burst, flush per 180 ms), jednorázový analysis hitch na začátku psaní velkého dokumentu.

### B7.8 — Regrese, perf ✅
- [x] **B7.8a/c (E2E)** `ParentRenderGate` + `HumanTyping` + `EndToEndTyping` zelené; regrese batch1 7/7 (HumanTyping/EndToEndTyping/InlineFormat/CaretSelection/HistorySave) + batch2 6/6 (ToolbarSpellcheck/CommentsRevisions/Collaboration/SuggestionMode) — 0 nových selhání (collab/Phase17 vyžadují běžící API:5100). Build čistý.
- [x] **B7.8d (screenshot)** N/A — B7.1 nemění render výstup (jen potlačuje redundantní rendery), žádná vizuální změna k ověření.
- **Exit:** ✅ HOTOVO — freeze na hranicích slov pryč (parentRenders 26→≤4 ověřeno E2E), engine maluje každý úhoz, žádná regrese. Diagnostiky odstraněny.

---

## Fáze B8 — Plná regrese, perf, cleanup, finalizace

- [ ] **B8.1** Plná Node sada zelená (`npm run test:document-editor-modules`).
- [ ] **B8.2** Broad canvas E2E regrese (Typing, InlineFormat, CaretSelection, ToolbarSpellcheck, OverlapPerf, CommentsRevisions, Collaboration, HistorySave, Accessibility, HyphenationAdvancedTables, Tables, Image, NumberingLists) — 0 NOVÝCH selhání (pre-existing: PDF/HistorySave per paměť).
- [ ] **B8.3** Perf gate: `DocumentEditorCanvasEndToEndTypingE2ETests` (engine ≤ 50 ms) + `DocumentEditorCanvasHumanTypingE2ETests` (žádné dávkování) + nová „no canonical pull during typing" zelené.
- [ ] **B8.4** Screenshot sign-off: golden dokumenty (contract-demo, onlyoffice-parity, table-demo, large-perf-1000) — Claude posoudí layout/UX.
- [ ] **B8.5** Odstranit dočasnou diagnostiku/feature-flagy; smazat mrtvý kód mirroru.
- [ ] **B8.6** Aktualizovat paměť (`project_documenteditor_canvas_perf_rendering_fix.md`) + tento MD finálním stavem.
- **Exit:** vše zelené; mirror pryč; architektura = engine source-of-truth + operation-relay; perf na úrovni GDocs/OnlyOffice (per-úhoz ~5-9 ms, žádné záseky na pauzách).

---

## Rizika a poznámky

- **Tvarová shoda operací:** engine op-log batch vs `DocumentCollaborationOperationBatch` musí být identický tvar (B1.2 round-trip test je pojistka). Pokud se liší, přidat tenkou JS mapovací vrstvu — NE C# diff.
- **310 referencí `_document`:** B6.2 audit je nejrizikovější krok — dělat po malých skupinách, po každé skupině regrese. Feature-flag `_useEngineOperationRelay` umožní rychlý rollback.
- **OT/merge:** remote apply už existuje (`transform.mjs`); `updateBlock` (whole-block) merge je hrubozrnný — při konfliktu na stejném bloku „poslední vyhrává"; uživatel akceptoval. Zdokumentovat v B1.
- **Suggestions (B4)** jsou nejvíc závislé na before/after — pokud (b) on-demand, hlídat, ať se netriggeruje per-úhoz.
- **AGPL OnlyOffice** = jen principy, žádný kód.
- **Pořadí:** B1 (collab) a B2 (UI push) jsou nezávislé a obě odstraňují konzumenty mirroru → lze paralelně, ale B6 (smazání reconcile) až PO B1-B5 (všichni konzumenti migrováni).
