# Canvas engine - Faze 9: Command dispatcher a inline formatting (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 9** · Stav: hotovo · Priorita: P0

## Proč

Jeden command dispatcher pro ribbon, mini toolbar, context menu, shortcuts a public interop. Zásadní je, aby toolbar pointerdown neztratil selection. Tato fáze dodá inline formatting commandy + formatting state readback (active/mixed/disabled/value).

## Cílový stav

- Jeden dispatcher; všechny vstupy (ribbon/mini/menu/shortcut/interop) volají stejné commandy.
- Selection token zachován při toolbar pointerdown (toolbar nesmaže výběr).
- Bold/italic/underline/strike range command; collapsed pending formatting pro další znak.
- Font family/size; text color/highlight; clear formatting.
- Link apply/remove/open (Ctrl/Cmd+click).
- Formatting state: active, mixed, disabled, value (pro toolbar pressed-state).
- Undo/redo pro každý command.

## Clean-room
- [x] Vlastní dispatcher; mapování command id ↔ host API navazuje na Blazor toolbar/interop command boundary a stávající canvas engine runtime.

## Znovupoužití
- [x] Canvas `commands/dispatcher.mjs` poskytuje `execCommand/queryCommand` facade pro toolbar, interop a selection controller.
- [x] Canvas `commands/inline-format.mjs` implementuje clean-room mark mutace a formatting state readback nad canvas modelem.
- [x] Canvas `history/history-store.mjs` je použitý pro undoable snapshot transakce.
- [x] Toolbar registry/command registry C# shell je použitý přes `TmDocumentEditor` route do canvas hostu.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/dispatcher.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/inline-format.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/__tests__/dispatcher.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/__tests__/inline-format.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasInlineFormatE2ETests.cs
```

## DoD
- [x] E2E real pointer: toolbar click nesmaže selection.
- [x] Toolbar pressed/value state odpovídá canvas command modelu (`aria-pressed` + `data-canvas-command-*` diagnostics).
- [x] Undo/redo gate pro každý inline command je pokrytý v `dispatcher.test.mjs`; E2E ověřuje undo/redo posledního canvas formatting commandu.

## Faze 9.1: Dispatcher a selection token

### 9.1.1 RED
- [x] `dispatcher.test.mjs`: `execCommand(id,arg)` / `queryCommand(id)`; selection token zachován přes simulovaný toolbar pointerdown.

### 9.1.2 GREEN + screenshot + akceptace
- [x] Canvas command runtime facade; selection snapshot před commandem; E2E: výběr → toolbar Bold → selection zůstává.

## Faze 9.2: Bold/italic/underline/strike + collapsed pending

### 9.2.1 RED
- [x] `inline-format.test.mjs`: range command toggluje mark na výběru; collapsed caret nastaví pending formatting pro další napsaný znak.

### 9.2.2 GREEN + screenshot + akceptace
- [x] Clean-room inline mark mutace; pending mark state; E2E bold před/po + unit "collapsed bold → napsat slovo → bold jen nové slovo".

## Faze 9.3: Font family/size, text color/highlight

### 9.3.1 RED
- [x] Range commandy fontfamily/fontsize/textcolor/highlight aplikují mark; value readback.

### 9.3.2 GREEN + screenshot + akceptace
- [x] Canvas facade textcolor/fontsize/highlight; E2E před/po pro color/highlight/font size.

## Faze 9.4: Clear formatting a link

### 9.4.1 RED
- [x] Clear formatting odstraní inline marks; link apply/remove; Ctrl/Cmd+click otevře odkaz.

### 9.4.2 GREEN + screenshot + akceptace
- [x] Canvas removelink/link; clear marks; E2E link apply + open.

## Faze 9.5: Formatting state readback

### 9.5.1 RED
- [x] `queryCommand` vrací active/mixed/disabled/value pro smíšený výběr (část bold, část ne → mixed).

### 9.5.2 GREEN + screenshot + akceptace fáze 9
- [x] Canvas format-state readback; host emituje formattingState snapshot a `data-canvas-command-*` diagnostics; E2E ověřuje toolbar state proti modelu.
- [x] UX review: toolbar state a canvas render se shodují, selection se neztrácí.

## Implementační poznámky 2026-06-04

- Přidáno `commands/dispatcher.mjs` a `commands/inline-format.mjs`; `entry.mjs`, `interop.mjs`, `input-controller.mjs` a `selection-controller.mjs` jsou napojené na jednotný command runtime.
- `TmDocumentCanvasEngineHost` vystavuje `ExecCommandAsync`/`QueryCommandAsync`; `TmDocumentEditor` routuje canvas inline commandy stejně jako core engine.
- E2E seed `phase-9-canvas-inline-format` používá reálný demo provider a toolbar přes `showToolbar=true`.
- Opravena produkční normalizace font size hodnot z toolbaru: canvas ukládá `24` místo `24pt`, aby layout nepřepadl na default velikost.
- Opraven kontrakt `openLinkAtPosition`: runtime vrací stabilní boolean success a Ctrl/Cmd+click ukládá otevřený odkaz do diagnostiky.
- Ověření: JS `node --test` pro dispatcher/inline-format/input/entry zelené; E2E `DocumentEditorCanvasInlineFormatE2ETests` zelené; screenshot evidence je v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase9-inline-format/2026-06-04/desktop-1440x1000/`.

## Poznámky
- Paragraph commandy (align/list/style) = Faze 10; tady jen inline.
- Undo coalescing (psaní po slovech) = Faze 12; tady každý command = vlastní undo krok.
- Routování commandů do canvas hostu v `TmDocumentEditor` analogicky `RouteToCoreEngineAsync` (R.4.8 pattern).
