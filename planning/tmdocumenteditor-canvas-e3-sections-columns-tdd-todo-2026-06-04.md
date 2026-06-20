# Canvas engine - E3: Sekce, sloupce, line numbering a page setup (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E3** · Stav: dokončeno včetně P2 newspaper balance · Priorita: P2 (nad rámec legacy)

## Proč

Sekce s vlastním page setupem, vícesloupcový layout a line numbering jsou standard Word/OnlyOffice. Legacy je neměl. Layout/recalc musí znát sekce a sloupce (master architektonický princip „section/column markup je součást layoutu").

## Cílový stav

- Section model + break next-page/continuous/even/odd.
- Per-section page size/margins/orientation; mix portrait/landscape.
- Multi-column: count, width, spacing, separator; text teče mezi sloupci; column break.
- Line numbering: continuous/per-page/per-section, restart, increment.
- Per-page canvas cache respektuje column geometrii.

## Clean-room
- [x] Section/column flow vlastní; ONLYOFFICE `sections/*` jen inspirace.

## Znovupoužití
- [x] Pagination (Faze 6.7), page-metrics; header/footer per section (Faze 16).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/sections.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/column-flow.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/line-numbering.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/sections.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/column-flow.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasSectionsColumnsE2ETests.cs
```

## DoD
- [x] Dvousloupcový text + landscape sekce vypadají profesionálně.
- [x] Save/reload sekce/sloupce.
- [x] Undo gate pro page setup/section commands.

## Faze E3.1: Section model + breaks

### E3.1.1 RED
- [x] `sections.test.mjs`: section break next-page/continuous/even/odd; per-section page size/margins/orientation; mix portrait/landscape.

### E3.1.2 GREEN + screenshot + akceptace
- [x] `sections.mjs`; pagination respektuje sekce; E2E landscape sekce.

## Faze E3.2: Multi-column flow

### E3.2.1 RED
- [x] `column-flow.test.mjs`: count/width/spacing/separator; text teče sloupec po sloupci; column break.
- [x] Newspaper-style balance.

### E3.2.2 GREEN + screenshot + akceptace
- [x] `column-flow.mjs`; cache respektuje column geometrii; E2E dvousloupcový text.

## Faze E3.3: Line numbering + page setup UI

### E3.3.1 RED
- [x] `line-numbering`: continuous/per-page/per-section, restart, increment, render v margin.
- [x] Page setup dialog (margins/size/orientation/columns).

### E3.3.2 GREEN + screenshot + akceptace fáze E3
- [x] Line numbering render; E2E sekce+sloupce+line numbers, save/reload.
- [x] Page setup dialog (Blazor).
- [x] Undo gate.
- [x] Screenshot: profesionální geometrie.

## Poznámky
- DOCX sectPr roundtrip = Faze 19 smoke.
- Newspaper-style balanced columns dokončeno včetně layout regression testu a E2E screenshot ověření.
