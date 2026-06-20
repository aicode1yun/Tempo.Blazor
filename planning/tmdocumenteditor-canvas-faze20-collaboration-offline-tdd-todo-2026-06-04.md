# Canvas engine - Faze 20: Collaboration a offline (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 20** · Stav: hotovo · Priorita: P1

## Proč

Realtime kolaborace (serializovatelné operace, remote apply, transform/merge, presence/remote cursors) a offline (draft, resume/sync). Velká subdoména s vlastní konzistenční logikou. Reuse R.4.x collab-client.

## Cílový stav

- Operation model serializovatelný pro realtime.
- Local op log; remote op apply.
- OT/transform nebo deterministický merge strategy.
- Remote cursor/presence canvas overlay.
- Conflict handling; offline draft save; offline resume/sync.

## Clean-room
- [x] Vlastní; ONLYOFFICE `CollaborativeEditing` jen koncept.

## Znovupoužití
- [x] `core-engine/collab-client.mjs`; `operations.mjs` (serializovatelné ops, Faze 12).
- [x] C# `TmDocumentCollaborationCursorOverlay`; CollaborationProvider, SyncProvider, OfflineStore params; Demo API SignalR.
- [x] OT/CRDT rozhodnutí: `planning/document-editor-ot-crdt-decision.md`.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/collaboration/op-log.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/collaboration/transform.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/collaboration/presence-overlay.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/collaboration/__tests__/transform.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/collaboration/__tests__/offline.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasCollaborationE2ETests.cs
```

## DoD
- [x] Two-browser live E2E přes Demo API SignalR.
- [x] Remote caret čitelný, nepřekrývá text agresivně.
- [x] Konvergence: dva klienti skončí se stejným modelem.

## Faze 20.1: Serializovatelné operace + op log

### 20.1.1 RED
- [x] Každá editace produkuje serializovatelnou operaci; local op log; replay konverguje.

### 20.1.2 GREEN + akceptace
- [x] `op-log.mjs`; serialize/deserialize round-trip.
- [x] Přímý reuse `operations.mjs` pro canvas op log.

## Faze 20.2: Remote apply + transform

### 20.2.1 RED
- [x] `transform.test.mjs`: remote op apply; concurrent ops transform/merge; konvergence (dva klienti = stejný model).

### 20.2.2 GREEN + akceptace
- [x] `transform.mjs` (dle OT/CRDT rozhodnutí).
- [x] Přímý reuse `collab-client` transportu na JS vrstvě.

## Faze 20.3: Presence + remote cursors

### 20.3.1 RED
- [x] `presence-overlay`: remote cursor/selection overlay s barvou/jménem; update na remote ops.

### 20.3.2 GREEN + screenshot + akceptace
- [x] `presence-overlay.mjs`; E2E two-browser: edit v jednom → vidět v druhém + remote caret.

## Faze 20.4: Offline draft + resume/sync

### 20.4.1 RED
- [x] `offline.test.mjs`: offline draft save; resume; sync po reconnectu; conflict handling.
- [x] Canvas offline state JSON coverage v canvas entry module testech.

### 20.4.2 GREEN + screenshot + akceptace fáze 20
- [x] Offline (OfflineStore/SyncProvider/PreferLocalDraft) canvas save/resume/sync integrace.
- [x] E2E offline edit → reconnect → sync.
- [x] Two-browser live smoke.
- [x] Screenshot: remote caret čitelný.

## Poznámky
- Coalescing (Faze 12) nesmí kolidovat s remote ops — remote ukončí local skupinu.
- Presence (kdo je online) reuse SignalR; full awareness (jména/avatary) přes CollaborationProvider.
