# Canvas engine - Faze 13: Toolbar shell, mini toolbar, context menu a spellcheck (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 13** · Stav: implementováno a ověřeno · Priorita: P1

## Proč

Kompletní UI shell nad dispatcherem: ribbon/compact/distraction-free, mini toolbar nad selection, context menu pro text/link/table/image/misspelling, keyboard shortcuts manager, command palette. Plus proofing service (spellcheck) jako engine diagnostika s canvas overlay vlnovkami.

## Cílový stav

- Ribbon, compact, distraction-free toolbar — všechny commandy na canvas dispatcher.
- Mini toolbar nad selection; context menu (text/link/table/image/spellcheck).
- Proofing service: engine pošle text spans + jazyk, provider/worker vrátí misspelling diagnostics bez mutace modelu.
- Canvas kreslí červené vlnovky jako overlay (ne inline style).
- Context menu nad misspelling: návrhy, Ignore once/all, Add to dictionary (pokud provider).
- Oprava = transakční replace command (undoable).
- Diagnostics invalidace jen v dotčeném rozsahu; respekt read-only/protected/comments/revisions/language.
- Keyboard shortcuts manager; command palette.
- Toolbar click nesmaže selection.

## Clean-room
- [x] Proofing API vlastní; ONLYOFFICE spellchecker UX jen inspirace.

## Znovupoužití
- [x] C# shell: `TmDocumentEditorToolbar`, `TmDocumentTableToolbar`, `TmDocumentCommandPalette`, `TmDocumentToolbarOverflowMenu`, `DocumentEditorShortcutRegistry`, `DocumentEditorKeyboardManager`.
- [x] Dispatcher (Faze 9), formatting state readback (9.5).
- [x] Existující spellcheck word checker (`buildWordListChecker`), SuggestionProvider boundary.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/diagnostics/proofing-service.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/diagnostics/squiggle-overlay.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/diagnostics/__tests__/proofing.test.mjs
src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasMiniToolbar.razor
src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasContextMenu.razor
tests/Tempo.Blazor.E2E/DocumentEditorCanvasToolbarE2ETests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasSpellcheckE2ETests.cs
```

## DoD
- [x] E2E real pointer: toolbar click nesmaže selection.
- [x] Misspelling squiggle čitelná, nepřekrývá baseline; context menu nepřekrývá caret/selection špatně.
- [x] Oprava undoable.

## Faze 13.1: Toolbar módy na dispatcher

### 13.1.1 RED
- [x] Ribbon/compact/distraction-free commandy volají canvas dispatcher; click zachová selection.

### 13.1.2 GREEN + screenshot + akceptace
- [x] Napojit toolbary; E2E click nesmaže selection; toolbar overflow na mobilu.

## Faze 13.2: Mini toolbar + context menu

### 13.2.1 RED
- [x] Mini toolbar nad selection; context menu text/link/table/spellcheck s relevantními commandy.

### 13.2.2 GREEN + screenshot + akceptace
- [x] Mini toolbar + context menu komponenty; `AssertNoUiOverlapAsync` (nepřekrývá výběr/caret).

## Faze 13.3: Proofing service

### 13.3.1 RED
- [x] `proofing.test.mjs`: engine vyrobí text spans + jazyk; provider vrátí misspelling ranges; model se nemutuje; range→canvas rects přes zalomení.

### 13.3.2 GREEN + akceptace
- [x] `proofing-service.mjs`; respekt read-only/protected/comments/revisions/language; inkrementální invalidace v rozsahu.

## Faze 13.4: Squiggle overlay + context menu opravy

### 13.4.1 RED
- [x] Vlnovky jako overlay pass; context menu nad misspelling: návrhy, Ignore once/all, Add to dictionary; klik na návrh = replace command.

### 13.4.2 GREEN + screenshot + akceptace
- [x] `squiggle-overlay.mjs`; E2E: špatné slovo → squiggle → context menu → oprava → text se změní, squiggle zmizí; undo gate.

## Faze 13.5: Shortcuts + command palette

### 13.5.1 RED
- [x] Keyboard shortcuts manager (Ctrl+B atd.) volá dispatcher; command palette hledá a spouští commandy.

### 13.5.2 GREEN + akceptace fáze 13
- [x] Reuse shortcut/keyboard registry + command palette; E2E shortcut + palette.
- [x] Screenshot: žádné menu/panel nepřekrývá výběr/caret nesmyslně.

## Poznámky
- Proofing worker (off-main-thread) volitelný; default provider sync. Plný jazyk/dictionary management = follow-up.
- Spellcheck diagnostics nejsou persistentní (nebo provider-backed) — nikdy v save modelu jako mutace.
