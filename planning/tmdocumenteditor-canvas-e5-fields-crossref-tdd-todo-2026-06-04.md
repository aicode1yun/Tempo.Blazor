# Canvas engine - E5: Fields, cross-reference, captions a bibliografie (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E5** · Stav: implementováno · Priorita: P2 (nad rámec legacy)

## Proč

Field engine (instrText + cached result + update), cross-reference, captions se SEQ auto-číslováním, table of figures a bibliografie jsou standard Word/OnlyOffice. Legacy měl jen header fields + TOC. Rozšiřuje pole z Faze 16.

## Cílový stav

- Field engine: instrText + cached result + update; PAGE, NUMPAGES, DATE, TIME, FILENAME, AUTHOR, STYLEREF.
- Cross-reference: REF na heading/bookmark/caption/numbered item; klik → skok; update po změně cíle.
- Caption (figure/table/equation) se SEQ auto-číslováním; vložení/smazání přečísluje.
- Table of figures jako generovaný aktualizovatelný field.
- Bibliography/citations (kde provider podporuje) nebo model + render placeholder.
- Update field / update all; print/export aktualizuje pole.

## Clean-room
- [x] Field engine vlastní; ONLYOFFICE `ComplexField*` jen koncept.

## Znovupoužití
- [x] `commands/fields.mjs` (Faze 16 základní pole) → rozšířit na field engine.
- [x] Bookmarks (Faze 18) jako REF targety.
- [x] TOC generator (Faze 18) pro table of figures pattern.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/fields/field-engine.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/fields/cross-reference.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/fields/captions.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/fields/__tests__/field-engine.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/fields/__tests__/captions-seq.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasFieldsE2ETests.cs
```

## DoD
- [x] SEQ renumber, REF resolve, page-number field po repaginaci.
- [x] Save/reload pole jako aktualizovatelné; undo gate.

## Faze E5.1: Field engine

### E5.1.1 RED
- [x] `field-engine.test.mjs`: field = instrText + cached result; update přepočítá; PAGE/NUMPAGES po repaginaci; DATE/TIME/FILENAME/AUTHOR/STYLEREF.

### E5.1.2 GREEN + akceptace
- [x] `field-engine.mjs`; update field / update all command.

## Faze E5.2: Cross-reference

### E5.2.1 RED
- [x] `cross-reference`: REF na heading/bookmark/caption/numbered item; klik → skok; update po změně cíle (text/page).

### E5.2.2 GREEN + screenshot + akceptace
- [x] `cross-reference.mjs`; E2E vložit cross-ref a update.
- [x] E2E přejmenovat cíl cross-reference a update.

## Faze E5.3: Captions + SEQ

### E5.3.1 RED
- [x] `captions-seq.test.mjs`: caption figure/table/equation se SEQ; vložení/smazání přečísluje; label + číslo + text.

### E5.3.2 GREEN + screenshot + akceptace
- [x] `captions.mjs`; E2E caption + auto-číslo.

## Faze E5.4: Table of figures + bibliografie

### E5.4.1 RED
- [x] Table of figures jako generovaný aktualizovatelný field (z captions); bibliography/citations model + render (provider boundary).

### E5.4.2 GREEN + screenshot + akceptace fáze E5
- [x] Table of figures generator; bibliography placeholder/provider; E2E caption→cross-ref→table of figures→update; save/reload; undo gate.

## Poznámky
- Plný citation style management (APA/MLA) jako provider/follow-up.
- DOCX field roundtrip = Faze 19.
