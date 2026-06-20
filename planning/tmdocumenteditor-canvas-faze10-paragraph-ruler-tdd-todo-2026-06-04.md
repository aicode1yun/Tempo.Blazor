# Canvas engine - Faze 10: Paragraph commands, styly a ruler (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 10** · Stav: implementováno a ověřeno · Priorita: P0

## Proč

Odstavcové commandy (zarovnání, spacing, indent, seznamy), heading styly se semantickým typem/levelem (ne jen font size) a vizuální stav pravítka. Navazuje na dispatcher (Faze 9) a layout (Faze 6). Plný styl management je E4; tady základ.

## Cílový stav

- Align left/center/right/justify; line spacing; spacing before/after; increase/decrease indent.
- Bullet/numbered list toggle; list nesting Tab/Shift+Tab mimo tabulku.
- Heading/block style apply; `Heading 1`–`Heading 6` mění semantický typ/level (ne jen velikost).
- Style resolver odlišuje name, based-on, outline level a direct formatting.
- Smíšený heading/paragraph výběr publikuje správný mixed state.
- Změna/přesun/smazání nadpisu invaliduje outline/TOC cache.
- Quote style; ruler margin/indent handles; show blocks/non-printing chars.

## Clean-room
- [x] Style resolver vlastní; ONLYOFFICE document-editor UX (name/based-on/outline/direct) jen koncepční inspirace.

## Znovupoužití
- [x] `core-engine/paragraph-styles.mjs`, `list-model.mjs`, `list-layout.mjs`.
- [x] `core-engine/core-editor.mjs` align facade; outline/TOC cache invalidace (R.4.6 layout cache).
- [x] Dispatcher z Faze 9.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/paragraph-commands.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/heading-style.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/ruler.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/__tests__/paragraph-commands.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/__tests__/heading-style.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasParagraphE2ETests.cs
```

## DoD
- [x] Heading round-trip zachová level, style id/name, inline formatting.
- [x] Mixed selection → mixed toolbar state.
- [x] Undo/redo gate; screenshot před/po.

## Faze 10.1: Align, spacing, indent

### 10.1.1 RED
- [x] `paragraph-commands.test.mjs`: align 4 varianty; line spacing; spacing before/after; increase/decrease indent.

### 10.1.2 GREEN + screenshot + akceptace
- [x] Reuse align facade + layout options; E2E alignment/indent před/po.

## Faze 10.2: Bullet/numbered list toggle + nesting

### 10.2.1 RED
- [x] List toggle on/off; Tab/Shift+Tab mění úroveň (mimo tabulku); label layout bez overlapu.

### 10.2.2 GREEN + screenshot + akceptace
- [x] Reuse list-model/list-layout; E2E seznam + nesting. (Plné numbering definitions = E1.)

## Faze 10.3: Heading styly a level

### 10.3.1 RED
- [x] `heading-style.test.mjs`: `Heading 1`–`6` nastaví semantický typ + level + style id/name, ne jen font size; round-trip zachová.

### 10.3.2 GREEN + screenshot + akceptace
- [x] Reuse paragraph-styles; style resolver (name/based-on/outline/direct); E2E aplikovat H1/H2, save/reload, stejný render + model.
- [x] Screenshot: nadpisy mají profesionální hierarchii/spacing.

## Faze 10.4: Style resolver + mixed state + cache invalidace

### 10.4.1 RED
- [x] Smíšený heading/paragraph výběr → mixed; změna textu/smazání nadpisu invaliduje outline/TOC cache.
- [x] Přesun nadpisu invaliduje outline/TOC cache po zavedení move commandu.

### 10.4.2 GREEN + akceptace
- [x] Mixed state v queryCommand; cache invalidace hook (využije Faze 18 TOC).

## Faze 10.5: Quote, ruler, show blocks

### 10.5.1 RED
- [x] Quote style; ruler kreslí margin/indent handles + tab area; show blocks/non-printing chars overlay.

### 10.5.2 GREEN + screenshot + akceptace fáze 10
- [x] `ruler.mjs` vizuál (interakce handles = E2); show-blocks overlay; E2E screenshot ruler + pilcrow/space značky.
- [x] UX review: odstavce mají dokumentovou hustotu a čitelnost.

## Poznámky
- Plné ruler interakce (drag indent/tab) = E2; tady jen vizuální stav.
- Numbering definitions/restart/continue/list styly = E1.
- Heading styly se v E4 napojí na plný style store.
