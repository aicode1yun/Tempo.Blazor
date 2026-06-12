# Canvas engine - Faze 12: History, dirty state, save a autosave (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 12** · Stav: hotovo · Priorita: P0

## Proč

Transakční historie s lidsky seskupeným undo/redo, dirty state z model verze (ne ze scrollu/renderu), manuální + auto save tahající aktuální canvas model, save conflict/offline a before-unload guard. Sjednocuje undo napříč všemi commandy.

## Cílový stav

- History stack s transakcemi; typing coalescing po slovech/časovém okně.
- Undo/redo text editů, formátování, tabulek/obrázků/komentářů/revizí.
- Dirty state z model version.
- Manual save tahá aktuální canvas model; autosave debounce.
- Save conflict/retry/offline draft; before-unload guard.

## Clean-room
- [x] Vlastní history; bez ONLYOFFICE kódu.

## Znovupoužití
- [x] `core-engine/operations.mjs`, `undo-stack.mjs`.
- [x] R.4.8 `SaveCoreAsync` pattern (RequestDocumentAsync → CreateProviderBoundarySnapshot → provider + MarkSaved).
- [x] `isDirty`/`markSaved` facade; provider boundary; OfflineStore/SyncProvider params.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/history/history-controller.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/history/coalescing.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/history/__tests__/undo-redo.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/history/__tests__/coalescing.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasHistorySaveE2ETests.cs
```

## DoD
- [x] E2E save/reload pro každou kategorii: text, formatting, table, image, comments, revisions.
- [x] Undo/redo gate pro každý editační command (sjednocení).
- [x] Screenshot: po reloadu dokument stejný.

## Faze 12.1: History stack + transakce

### 12.1.1 RED
- [x] `undo-redo.test.mjs`: každý command = transakce; undo/redo vrací přesný stav.

### 12.1.2 GREEN + akceptace
- [x] Reuse operations/undo-stack; jednotné transakční API pro všechny commandy.

## Faze 12.2: Typing coalescing

### 12.2.1 RED
- [x] `coalescing.test.mjs`: psaní se sloučí po slovech/časovém okně (ne 1 undo na znak); Enter/formátování ukončí skupinu.

### 12.2.2 GREEN + akceptace
- [x] Coalescing strategie; hranice (mezera, interpunkce, čas, command typ).

## Faze 12.3: Undo/redo napříč typy

### 12.3.1 RED
- [x] Undo/redo: text, formatting, table op, image op, comment, revision decision.

### 12.3.2 GREEN + screenshot + akceptace
- [x] Všechny editační cesty přes history; E2E undo/redo pro každý typ.

## Faze 12.4: Dirty state + manual save

### 12.4.1 RED
- [x] Dirty z model version; manual save tahá aktuální model; markSaved vynuluje dirty.

### 12.4.2 GREEN + screenshot + akceptace
- [x] Reuse SaveCoreAsync pattern; E2E edit → save → persistovaný model nese edit.

## Faze 12.5: Autosave, conflict, offline, before-unload

### 12.5.1 RED
- [x] Autosave debounce; save conflict → retry; offline draft uložen a obnoven; before-unload guard při dirty.

### 12.5.2 GREEN + akceptace fáze 12
- [x] Autosave + conflict/retry + offline (OfflineStore/SyncProvider/PreferLocalDraft); before-unload; E2E offline draft recovery smoke.

## Poznámky
- Collaboration remote ops mají vlastní transform (Faze 20); coalescing nesmí kolidovat s remote ops.
- Save/reload gates se opakují v každé feature fázi (table/image/comments) — tady jen kategorie smoke.
