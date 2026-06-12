# Canvas engine - E12: Hyphenation, page background a pokročilé tabulky (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E12** · Stav: implementováno · Priorita: P2 (nad rámec legacy)

## Proč

Hyphenation, page background/watermark/borders a pokročilé tabulky (styly, repeat header, split across pages, nested, sort, formula) dorovnávají poslední parity mezeru k Word/OnlyOffice. Rozšiřuje line-breaker (6), page frame (5) a tabulky (14).

## Cílový stav

- Hyphenation: auto/manual, zone, consecutive limit; optional/non-breaking hyphen; integrace s line-breakerem.
- Page background: page color, watermark (text/image, diagonal), page borders (art/line, margin/page).
- Pokročilé tabulky: table styly (banded rows/cols, header/total), repeat header rows na dalších stránkách, split table across pages, nested tables.
- Table extras: convert text↔table, table sort, jednoduchá formula (SUM/AVERAGE), cell margins/spacing, cell borders editor.

## Clean-room
- [x] Hyphenation/table style/formula vlastní; ONLYOFFICE jen koncept.

## Znovupoužití
- [x] `layout/line-breaker.mjs` (Faze 6) pro hyphenation break points; `render/page-frame.mjs` (Faze 5) pro background; tabulky (Faze 14).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/hyphenation.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/page-background.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/table-styles.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/table-pagination.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/hyphenation.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/__tests__/table-styles.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/__tests__/table-pagination.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasAdvancedTablesE2ETests.cs
```

## DoD
- [x] Hyphenation break points; table style banding; header-row repeat na page break.
- [x] Save/reload; undo gate.

## Faze E12.1: Hyphenation

### E12.1.1 RED
- [x] `hyphenation.test.mjs`: auto/manual, zone, consecutive limit; optional/non-breaking hyphen; break points v line-breakeru.

### E12.1.2 GREEN + screenshot + akceptace
- [x] `hyphenation.mjs` (pattern/dictionary nebo heuristika); E2E zalomení s pomlčkami.

## Faze E12.2: Page background, watermark, borders

### E12.2.1 RED
- [x] `page-background`: page color; watermark (text/image, diagonal); page borders (line/dash, margin/page).

### E12.2.2 GREEN + screenshot + akceptace
- [x] `page-background.mjs`; E2E watermark + page border.

## Faze E12.3: Table styly + repeat header + split across pages + nested

### E12.3.1 RED
- [x] `table-styles.test.mjs`: banded rows/cols, header/total styly; `table-layout.test.mjs`: repeat header row na dalších stránkách; split tabulky přes stránky; nested table layout.

### E12.3.2 GREEN + screenshot + akceptace
- [x] `table-styles.mjs` + `table-pagination.mjs`; E2E tabulka přes 2 stránky s opakovanou hlavičkou + style.

## Faze E12.4: Table extras

### E12.4.1 RED
- [x] Convert text↔table; table sort; formula SUM/AVERAGE; cell margins/spacing; cell borders editor.

### E12.4.2 GREEN + screenshot + akceptace fáze E12
- [x] Extras commandy; E2E convert + sort + formula; save/reload; undo gate.
- [x] Screenshot: tabulka přes stránky + style + watermark profesionální.

## Poznámky
- DOCX table style / hyphenation / watermark roundtrip = Faze 19 smoke.
- Formula engine jen základní agregace; plný (vzorce přes buňky) = follow-up.
