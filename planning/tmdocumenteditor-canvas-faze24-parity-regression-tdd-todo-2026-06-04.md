# Canvas engine - Faze 24: Parity regression suite (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 24** · Stav: implementováno · Priorita: P0 (gate před cutoverem)

## Proč

Před cutoverem musí existovat suite, která dokáže, že canvas engine pokrývá vše, co legacy + rozšířené E-fáze. Sbírá per-command/provider/interaction testy a přepisuje staré WYSIWYG/core testy na canvas selektory. Definuje dvoustupňovou akceptaci (legacy-parity vs full-quality).

## Cílový stav

- `DocumentEditorCanvasLegacyParityE2ETests` + seed document pokrývající všechny feature groups.
- Každý toolbar command má min. jeden E2E nebo explicitní "shell-only" test.
- Každý provider boundary má save/export/reload test.
- Každá major interaction má screenshot test.
- Staré WYSIWYG/core testy přepsané na canvas selektory nebo označené legacy/core-only.
- Historické bug regression testy zachované, expected behavior na canvas engine.
- Každá E1–E12 fáze má parity řádek.

## Clean-room
- [x] N/A (test gate).

## Znovupoužití
- [x] Všechny per-fáze E2E (5–22 + E1–E12); screenshot helpery (Faze 2).
- [x] Existující legacy/core E2E jako reference (přepsat na canvas).

## Doporučené nové soubory

```text
tests/Tempo.Blazor.E2E/DocumentEditorCanvasLegacyParityE2ETests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasExtendedParityE2ETests.cs
tests/Tempo.Blazor.E2E/Fixtures/canvas-parity-seed.json
tests/Tempo.Blazor.E2E/CanvasEngine/ParityCoverageMatrix.cs
```

## DoD
- [x] Legacy-parity suite zelená bez legacy fallbacku → umožní "legacy-parity preview" cutover.
- [x] Legacy-parity + E1–E12 suite zelená → umožní "kvalita GDocs/Word/OnlyOffice".

## Faze 24.1: Seed document + coverage matrix

### 24.1.1 RED
- [x] `canvas-parity-seed.json` pokrývá všechny feature groups (text/heading/list/table/image/drawing/shape/math/form/header-footer/notes/comment/revision/field/TOC/section/columns).
- [x] `ParityCoverageMatrix` mapuje command/provider/interaction → test; chybějící = RED.

### 24.1.2 GREEN + akceptace
- [x] Seed + matrix; všechny buňky mají test nebo explicitní "shell-only".

## Faze 24.2: Toolbar command parity

### 24.2.1 RED → GREEN + akceptace
- [x] Každý toolbar command (legacy + rozšířený) má E2E nebo shell-only test; `AssertToolbarStateMatchesModelAsync`.

## Faze 24.3: Provider boundary parity

### 24.3.1 RED → GREEN + akceptace
- [x] Každý provider (Image/Font/Token/Mention/PdfExport/Format/Comparison/Suggestion/Collaboration/Offline/Sync/Audit) má save/export/reload test.

## Faze 24.4: Interaction + screenshot parity

### 24.4.1 RED → GREEN + akceptace
- [x] Každá major interaction (typing/selection/drag/table/image/comment/revision/find/TOC/form/math/shape) má screenshot test.

## Faze 24.5: Přepis legacy/core testů + bug regrese

### 24.5.1 RED → GREEN + akceptace fáze 24
- [x] Staré WYSIWYG/core testy přepsané na canvas selektory nebo označené legacy/core-only diagnostiku.
- [x] Historické bug regression testy zachované s canvas expected behavior.
- [x] Legacy-parity suite zelená; E1–E12 parity řádky zelené.

## Poznámky
- Tahle fáze je předpoklad Faze 25 (cutover) a Faze 26 (soak/removal) z master plánu.
- Coverage matrix slouží jako living dokument — nové featury přidávají řádky.
