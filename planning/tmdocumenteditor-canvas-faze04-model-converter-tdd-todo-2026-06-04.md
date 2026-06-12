# Canvas engine - Faze 4: Canonical canvas model a converter (detailni TDD + E2E)

Datum: 2026-06-04 · Nadrazeny: master canvas plan, **Faze 4** · Stav: hotovo · Priorita: P0 (foundational - stavi na ni cely engine)

## Proč

Canvas engine potřebuje interní model optimalizovaný pro layout (block tree, inline runs, marks, objekty, page settings), odvozený z `DocumentEditorDocument`, plus obousměrný converter s preserve channelem, aby žádná modelová vlastnost nezmizela při round-tripu. Tento model je vstup pro layout (Faze 5/6) a cíl pro edit operace (Faze 8/9).

## Cílový stav

- `CanvasDocumentModel`: document → sections → page settings → block tree (paragraph/heading/list/quote/table/image/drawing/pageBreak/toc/footnote/endnote) → inline runs (text/field/drawing) → marks.
- Converter `DocumentEditorDocument -> CanvasDocumentModel` a zpět, ztrátový pouze tam, kde je to explicitně zdokumentováno.
- Preserve channel uchová nemodelované vlastnosti (unknown marks, neznámé atributy) a vrátí je při zpětném převodu.
- Stabilní block id pro TOC/outline/navigaci.

## Clean-room
- [x] Model je odvozen z `DocumentEditorDocument` a vlastních testů, ne z ONLYOFFICE struktur.

## Znovupoužití
- [x] `CoreEngineModelConverter` (C#) jako reference pro preserve channel a round-trip kontrakt; canvas converter je samostatny a ulozeny v Abstractions.
- [x] Stávající JS runtime model-store je napojen na novy canonical `canvas-document-model.mjs`.

## Doporučené nové soubory

```text
src/Tempo.Blazor.Abstractions/DocumentEditor/Models/CanvasDocumentModel.cs
src/Tempo.Blazor.Abstractions/DocumentEditor/Services/CanvasDocumentModelConverter.cs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/model/canvas-document-model.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/model/__tests__/model.test.mjs
tests/Tempo.Blazor.Tests/Components/DocumentEditor/CanvasEngine/Model/CanvasModelConverterTests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasModelRoundtripE2ETests.cs
```

## DoD
- [x] Každý modelový typ má RED converter test před implementací.
- [x] Round-trip neztratí text/strukturu/metadata.
- [x] `dotnet build` zelený.

## Faze 4.1: Model schema a factory

### 4.1.1 RED
- [x] `model.test.mjs`: factory vytvoří document s sekcí, page settings, prázdným odstavcem; každý blok má stabilní `id`, `type`; runs mají `marks`.

### 4.1.2 GREEN + akceptace
- [x] `canvas-document-model.mjs`: konstruktory + validace + id generátor; normalizace (prázdný doc = 1 odstavec).

## Faze 4.2: Converter — paragraph/heading/list/quote

### 4.2.1 RED
- [x] `CanvasModelConverterTests`: paragraph, heading (level/style id/name/outline level + stabilní block id), bullet/numbered list (level), quote round-trip.

### 4.2.2 GREEN + akceptace
- [x] Obousměrný převod; heading metadata zachována; block id stabilní.

## Faze 4.3: Converter — table with spans

### 4.3.1 RED
- [x] Tabulka: rows/cells, colSpan, rowSpan, šířky, pozadí, alignment, vertical alignment round-trip.

### 4.3.2 GREEN + akceptace
- [x] Span model konzistentní (grid normalizace); round-trip bez ztráty buněk.

## Faze 4.4: Converter — image + inline drawing + page break

### 4.4.1 RED
- [x] Standalone image block, inline drawing run, page break round-trip; geometrie/anchor/wrap zachovány (navázat na Faze 15 model).

### 4.4.2 GREEN + akceptace
- [x] Převod drawing runs; preserve geometrie/wrap/z-order.

## Faze 4.5: Converter — header/footer, fields, footnotes/endnotes

### 4.5.1 RED
- [x] Header/footer dokumenty + scopes (default/first/even), field runs (page number atd.), footnote/endnote + numbering settings round-trip.

### 4.5.2 GREEN + akceptace
- [x] Převod hlaviček/patiček a poznámek; reference zachovány.

## Faze 4.6: Converter — comments, revisions, bookmarks, unknown marks

### 4.6.1 RED
- [x] Comment anchor + thread, revision (insert/delete/format) + decision, bookmark, **unknown mark preserve channel** round-trip.

### 4.6.2 GREEN + akceptace
- [x] Marks/anotace zachovány; nemodelovane run/mark detaily projdou preserve channelem beze ztráty.

## Faze 4.7: Save/reload E2E

### 4.7.1 RED
- [x] `DocumentEditorCanvasModelRoundtripE2ETests`: seed doc (text+tabulka+obrázek+hlavička+komentář+revize) → canvas model → zpět → uložit → reload; očekává shodu → RED.

### 4.7.2 GREEN + akceptace fáze 4
- [x] Round-trip přes provider boundary; reload nese vše; zapsat výjimky (co se neround-tripuje) do plánu.
- [x] Screenshot: po reloadu vizuál stejný (předpoklad render z Faze 5; do té doby JSON diff gate).

## Evidence

- `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore` - zeleny build.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - zeleny build.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~CanvasModelConverterTests|FullyQualifiedName~CanvasEngineHostRenderTests" --no-restore --no-build` - 9/9.
- `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/model/__tests__/model.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs` - 6/6.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasModelRoundtripE2ETests" --no-restore --no-build` - 1/1.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasHostE2ETests" --no-restore --no-build` - 1/1 browser smoke po napojeni realneho canvas modelu.

## Implementacni poznamky

Canvas DTO a converter jsou v `Tempo.Blazor.Abstractions`, protoze DTO/provider boundary nesmi zaviset na Blazor UI projektu. Preserve channel nema zadne zname ztratove vyjimky pro pokryty modelovy rozsah; pole, ktera canvas runtime zatim needituje primo, jsou obnovena ze `CanvasPreserveChannel.SourceJson`. Vizuální screenshot pro obsah dokumentu zustava az pro Fazi 5; Faze 4 pouziva JSON diff gate pres realny provider save/reload.

## Poznámky
- Pořadí 4.2→4.6 odpovídá rostoucí složitosti; každý sub-converter mergeovatelný zvlášť.
- Preserve channel je kritický pro NuGet kompatibilitu — žádná tichá ztráta dat.
