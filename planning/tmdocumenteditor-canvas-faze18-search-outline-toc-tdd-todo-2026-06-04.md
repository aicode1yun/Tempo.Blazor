# Canvas engine - Faze 18: Search, replace, outline, bookmarks a TOC navigace (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 18** · Stav: implementováno kromě sdílení core find/outline cache · Priorita: P1

## Proč

Find/replace (vč. regex), highlight matches na canvasu, navigace, bookmarks, heading outline panel a generovaný aktualizovatelný TOC s click-to-jump. Velká navigační subdoména.

## Cílový stav

- Find plain text; replace current/all; regex find/replace s backreferences.
- Highlight matches na canvasu; navigate next/previous; live region announcements.
- Bookmarks define/list/go-to.
- Heading outline extraction; outline panel (level, target block id, page index, y).
- Klik v outline → scroll/caret na nadpis.
- Insert TOC jako semantický field generovaný z outline (level, display text, target block id, page number z layout cache).
- Klik na TOC entry naviguje; Update TOC přepočítá; TOC = jedna transakce; save/reload jako aktualizovatelný objekt.

## Clean-room
- [x] Vlastní; ONLYOFFICE `DocumentSearch`/`DocumentOutline` jen koncept.

## Znovupoužití
- [ ] `core-engine/find-replace.mjs`; outline/TOC cache (R.4.6 + Faze 10.4 invalidace). _(Canvas má vlastní search/outline/TOC runtime; core-engine find-replace nebyl připojen.)_
- [x] C# `TmDocumentFindPanel`, `TmDocumentOutlinePanel`, `TmDocumentPageNavigator`.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/search/search-engine.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/navigation/outline.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/navigation/toc-generator.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/navigation/bookmarks.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/search/__tests__/search.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/navigation/__tests__/outline-toc.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasSearchTocE2ETests.cs
```

## DoD
- [x] Active find result viditelný, neruší selection/caret.
- [x] TOC vypadá jako dokumentový obsah (odsazení, page numbers); navigace funguje.
- [x] Save/reload TOC jako objekt; undo gate.

## Faze 18.1: Find/replace + regex

### 18.1.1 RED
- [x] `search.test.mjs`: find plain; replace current/all; regex s backreferences; case/whole-word options.

### 18.1.2 GREEN + screenshot + akceptace
- [x] `search-engine.mjs`; highlight matches overlay; navigate next/prev; live region; E2E find/replace.

## Faze 18.2: Bookmarks

### 18.2.1 RED
- [x] `bookmarks`: define/list/go-to; přežijí editaci. _(Doplněno v `outline-toc.test.mjs`: define/list/find + editace kolem marked range.)_

### 18.2.2 GREEN + akceptace
- [x] `bookmarks.mjs`; reuse R.4.6 bookmarks; converter round-trip. _(Doplněno `applyBookmarkToSelection`; E2E definuje bookmark přes canvas command runtime, edituje kolem něj, naviguje na něj a ověřuje save/reload přes Demo API.)_

## Faze 18.3: Outline extraction + panel

### 18.3.1 RED
- [x] `outline-toc.test.mjs`: outline extraction ignoruje body text, respektuje Heading 1–6 pořadí; entry má level, target block id, page index, y.

### 18.3.2 GREEN + screenshot + akceptace
- [x] `outline.mjs`; outline panel; klik → scroll/caret na nadpis; E2E navigace. _(E2E kliká `document-outline-item` pro H2 `Delivery Scope` a ověřuje selection focus block.)_

## Faze 18.4: TOC generator + navigace

### 18.4.1 RED
- [x] TOC generator vytvoří zanořené entries (level/display/target/page); po přejmenování nadpisu update; page numbers z layout cache.

### 18.4.2 GREEN + screenshot + akceptace
- [x] `toc-generator.mjs`; insert TOC jako semantický field; klik na entry naviguje; E2E insert TOC + klik.

## Faze 18.5: Update TOC + perzistence

### 18.5.1 RED
- [x] Update TOC přepočítá texty/pořadí/page numbers; TOC = 1 undo transakce; save/reload jako aktualizovatelný objekt (ne zploštěný odstavec).

### 18.5.2 GREEN + screenshot + akceptace fáze 18
- [x] Update TOC; E2E: H1/H2 + obsah + klik entry + přejmenovat H2 + update TOC + save/reload; undo gate. _(E2E přejmenuje H2 na `Delivery Roadmap`, aktualizuje TOC, ukládá přes Demo API, reloaduje a ověřuje TOC texty + bookmark.)_
- [x] Screenshot: TOC s odsazením/page numbers; aktivní find result viditelný.

## Poznámky
- DOCX TOC/outline metadata roundtrip = Faze 19 smoke.
- Navigation pane (kombinace outline + find + pages) reuse panely.
