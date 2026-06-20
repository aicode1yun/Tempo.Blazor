# Canvas engine - Faze 7: Hit testing, caret a selection (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 7** · Stav: hotovo · Priorita: P0

## Proč

Selection a caret patří runtime enginu, ne browser contenteditable. Tato fáze mapuje point→pozici, pozici→caret rect, kreslí caret a selection na overlay (nad page cache) a obsluhuje myš/klávesnici pro umístění a rozšíření výběru.

## Cílový stav

- point → text position (z layout caret stops, bez interpolace).
- text position → caret rect; collapsed caret paint na overlay (blikání).
- Range selection: jedna řádka, více řádek, přes bloky.
- Myš: click caret, drag select, double-click slovo, triple-click odstavec, Shift+click rozšíření.
- Klávesnice: šipky, Home/End, PageUp/PageDown, Ctrl/Alt word movement.
- Selection se kreslí jako overlay pass — nepřekresluje content cache.

## Clean-room
- [x] Vlastní hit-test/caret/selection; bez ONLYOFFICE kódu.

## Znovupoužití
- [x] `core-engine/hit-test.mjs` (pointer→pozice z caretStops), `caret.mjs` (moveCaretByKey Arrow/Home/End/Up/Down, grapheme krok, blikání), `selection-overlay.mjs` (per-řádek rects).
- [x] `layout/caret-*.mjs` (caret-math, caret-rect, caret-interval, nearest-text-position-line-box, caret-affinity).
- [x] **mousedown+preventDefault** pattern (udrží fokus na off-screen surface).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/selection-controller.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/pointer-gestures.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/__tests__/hit-test.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/__tests__/caret-move.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/__tests__/selection-range.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasCaretSelectionE2ETests.cs
```

## DoD
- [x] `AssertCaretVisibleAsync`, `AssertSelectionVisibleAsync`.
- [x] Selection overlay nepřekresluje content (ověřit, že content cache se nemění při pohybu caretu).
- [x] Žádné zpoždění/posun při výběru.

## Faze 7.1: point → position a position → caret rect

### 7.1.1 RED
- [x] `hit-test.test.mjs`: klik na souřadnici vrátí přesnou model pozici (caret stops, bez interpolace); pozice→rect vrátí x/y/výšku.

### 7.1.2 GREEN + akceptace
- [x] Reuse hit-test + caret-math; afinita na hranicích řádek.

## Faze 7.2: Collapsed caret paint

### 7.2.1 RED
- [x] Caret se kreslí na overlay v rect pozici; bliká; viditelný v aktivním viewportu.

### 7.2.2 GREEN + screenshot + akceptace
- [x] Reuse caret blikání; E2E `AssertCaretVisibleAsync`.

## Faze 7.3: Range selection (řádka / více řádek / bloky)

### 7.3.1 RED
- [x] `selection-range.test.mjs`: selection rects pro výběr v jedné řádce, přes více řádek, přes bloky; konzistentní highlight.

### 7.3.2 GREEN + screenshot + akceptace
- [x] Reuse selection-overlay per-řádek rects; E2E `AssertSelectionVisibleAsync` (highlight sedí na textu).

## Faze 7.4: Myš — click, drag, double/triple-click, Shift+click

### 7.4.1 RED
- [x] `pointer-gestures`: mousedown→caret; drag→range; double-click→slovo; triple-click→odstavec; Shift+click→rozšíření od anchoru.

### 7.4.2 GREEN + screenshot + akceptace
- [x] mousedown+preventDefault (fokus na surface); gesture state machine; E2E real pointer drag select.

## Faze 7.5: Klávesnice — pohyb a rozšíření

### 7.5.1 RED
- [x] `caret-move.test.mjs`: šipky (grapheme krok), Home/End, PageUp/PageDown (o stránku), Ctrl/Alt word movement; Shift+ varianty rozšiřují selection.

### 7.5.2 GREEN + screenshot + akceptace fáze 7
- [x] Reuse moveCaretByKey + word movement; PageUp/Dn přes layout; E2E real keyboard: klik→ArrowRight→Shift+ArrowRight→selection rect.
- [x] UX review: výběr působí nativně, bez posunu.

## Poznámky
- Drag autoscroll na hraně viewportu zmínit; plné virtualizované scrollování doladí Faze 22.
- Bidi caret (RTL mirror x) reuse z `bidi-line`; arabský caret uvnitř slova přibližný (follow-up).
- Selection uvnitř tabulky/objektů = Faze 14/15.

## Implementační evidence

- Přidáno `selection/selection-controller.mjs` a `selection/pointer-gestures.mjs`; controller používá layout caretStops z Faze 6, `core-engine/hit-test.mjs`, `caret.mjs`, `selection-overlay.mjs` a grapheme/word segmentation.
- `pagination.mjs` předává caretStops do canvas fragmentů; `canvas-stack.mjs` vrací `selectionLayout`; `entry.mjs` napojil selection runtime po renderu bez repaintu content cache.
- Selection/caret overlay se kreslí do `selection-caret` canvas vrstvy a synchronizuje DOM geometry (`document-canvas-caret`, `document-canvas-selection-rect`) pro E2E a budoucí interop.
- Myš: mousedown+preventDefault, click caret, drag range, double-click word, triple-click paragraph, Shift+click extension. Klávesnice: Arrow/Home/End/PageUp/PageDown, Ctrl/Alt word movement, Shift+rozšíření.
- E2E seed `phase-7-canvas-caret-selection` a test `DocumentEditorCanvasCaretSelectionE2ETests` ověřují caret visible, selection visible, real pointer/keyboard a neměnný content canvas cache. Screenshot evidence: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase7-caret-selection/2026-06-04/desktop-1440x1000/`.
