# TmDocumentEditor DOCX DrawingML architektura

Datum: 2026-05-25

Tento dokument shrnuje rozhodnuti po fazich 25-41 pro import/export DOCX obrazku. Cilem je OnlyOffice/Word kompatibilni DrawingML cesta, kde je editacni model `DocumentDrawingRun` a DOCX zapisuje skutecne `w:drawing/wp:inline` nebo `w:drawing/wp:anchor`, ne vizualni placeholder ani top-level legacy image blok.

## Cile a ne-cile

Primarni cil:

- importovat obrazky z Word/OnlyOffice DOCX jako inline obsah odstavce (`DocumentDrawingRun`),
- exportovat `DocumentDrawingRun` jako nativni DrawingML picture,
- zachovat vztah obrazku k vlastnickemu DOCX partu: body, header, footer, table cell, footnote, endnote nebo comments,
- zachovat dostatek typed DOCX metadata pro roundtrip bez zavislosti pouze na `tm:*` atributech,
- bezpecne zpracovat neduveryhodne DOCX balicky.

Ne-cil:

- zpetna kompatibilita se starym `ImageBlockContent` modelem neni cilova editacni architektura,
- `ImageBlockContent` zustava jen jako vstupni legacy/adaptacni tvar na hranici provideru a nekterych nedocx exporteru,
- DOCX `w:drawing` import nesmi vytvaret top-level `ImageBlockContent` blok.

## Kanonicky model

Kanonicky runtime a formatovy model pro obrazky v textu je `DocumentDrawingRun`.

`DocumentDrawingRun` nese:

- `ObjectId` a `Id` pro stabilni identitu objektu a inline runu,
- `Source`, `Url`, `AssetId`, `AltText`, `Caption` a volitelny `LinkUrl`,
- `DocumentObjectLayout` s `Kind`, `Anchor`, `Position`, `Wrap`, `Transform` a `Stacking`,
- volitelny `DocumentDocxDrawingMetadata` pro DOCX-specific hodnoty,
- inline `Marks` pro comment/revision marky stejne jako textove runy.

Importer po nacteni media partu sklada `DocumentDrawingRun` primo. Nepouziva uz mezityp `ImportedDocxImage(ImageBlockContent, Metadata)` ani konverzi `ImageBlockContent -> DocumentDrawingRun` pro `w:drawing`. Exporter pro skutecny drawing run vola `WriteDrawingRunAsync(DocumentDrawingRun, ...)`. Legacy top-level `ImageBlockContent` blok se muze pred exportem izolovane adaptovat pres `DocxDrawingRunAdapter.FromImageBlock`, aby se nezablokovaly stare demo/test dokumenty, ale vnitrni DrawingML writer zustava nad `DocumentDrawingRun`.

## Mapping wp:inline/wp:anchor -> DocumentDrawingRun

| DOCX prvek | Tempo model | Poznamka |
| --- | --- | --- |
| `w:r/w:drawing/wp:inline` | `DocumentDrawingRun` v `ParagraphBlockContent.Inlines` | Inline obrazek se ucastni toku textu. |
| `wp:inline/wp:extent/@cx,@cy` | `Layout.Transform.Width/Height` a `Size.Width/Height` | EMU se prevadi na body. |
| `wp:inline/wp:docPr` | `Docx.DocPrId`, `Docx.DocPrName`, `Docx.DocPrDescription`, `AltText` | `descr` je preferovany alt text. |
| `pic:cNvPr` | `Docx.PictureNonVisualId`, `Docx.PictureName`, `Docx.PictureDescription` | Zachovava picture-level identitu. |
| `a:blip/@r:embed` | `Docx.RelationshipId`, `Docx.Media`, `Url` nebo `AssetId` | Embedded image relationship je vzdy resen z vlastnickeho partu. |
| `a:srcRect` | `Layout.Transform.Crop` | Hodnoty jsou procenta v editor modelu. |
| `a:xfrm/@rot` | `Layout.Transform.Rotation` | DOCX rotation units se prevadi na stupne. |
| `a:xfrm/@flipH/@flipV` | `Layout.Transform.Flip` | Zachovava horizontalni a vertikalni flip. |
| `w:r/w:drawing/wp:anchor` | `DocumentDrawingRun` s `Layout.Kind = Anchored` nebo `Fixed` | Floating obrazek zustava inline runem s anchor metadaty. |
| `wp:anchor/wp:positionH/wp:positionV` | `Layout.Position` | Relativni reference a offset/alignment se mapuji do Tempo layoutu. |
| `wp:anchor/wp:wrapSquare` | `Layout.Wrap.Mode = Square` | Vcetne `wrapText` na `Wrap.Side` a distanci. |
| `wp:anchor/wp:wrapTopAndBottom` | `Layout.Wrap.Mode = TopBottom` | Text obtika pouze nad/pod. |
| `wp:anchor/wp:wrapTight/wp:wrapThrough` | `Layout.Wrap.Mode = Tight/Through` | Polygon se normalizuje na body `0..1` v `WrapContourPoints`. |
| `wp:anchor/wp:wrapNone + behindDoc=true` | `Layout.Wrap.Mode = BehindText` | Objekt je za textem. |
| `wp:anchor/wp:wrapNone + behindDoc=false` | `Layout.Wrap.Mode = InFrontOfText` | Objekt je pred textem. |
| `wp:anchor/@relativeHeight` | `Layout.Stacking.ZIndex` | Pouziva se pro poradi vrstev. |
| `wp:anchor/@locked`, `@layoutInCell`, `@hidden`, `@allowOverlap` | `Layout.Anchor` a `Docx` metadata | Hodnoty se zachovavaji pro roundtrip. |
| `wp14:anchorId`, `wp14:editId` | `Docx.AnchorId`, `Docx.EditId` | Exportuje se jen validni 8 hex hodnota. |
| `tm:*` atributy | fallback pro Tempo roundtrip | Nesmí byt jediny zdroj pravdy, protoze Word/OnlyOffice je nezapisuje. |

Header, footer, footnote, endnote, comments a table cell se mapuji stejnym principem. Rozdil je vlastnicky Open XML part a `DocumentObjectAnchor.Region`, `HeaderFooterId`, `TableId` nebo `CellId`.

## Jednotky a zaokrouhlovani

Tempo model pouziva body (`pt`) pro rozmery a pozice dokumentu. DOCX DrawingML pouziva EMU.

Pravidla:

- `1 inch = 914400 EMU`.
- `1 pt = 12700 EMU`.
- `1 pt = 20 twips` pro WordprocessingML hodnoty mimo DrawingML.
- Pixelove vstupy jsou jen vstupni/preview udaj; prevod na EMU pouziva DPI, default `96`.
- Export `pt -> EMU` pouziva `Math.Round`.
- Import `EMU -> pt` se u rozmeru/pozic uklada zaokrouhleny typicky na 2 desetinna mista, aby roundtrip nekmital kvuli binarni aritmetice.
- Rotation: `1 degree = 60000` DrawingML jednotek, importer uklada stupne zaokrouhlene na 4 desetinna mista.
- Crop: `a:srcRect` pouziva tisiciny procenta. `10000 = 10 %`, `100000 = 100 %`. Tempo model drzi procenta.
- Wrap contour polygon se normalizuje vuci `wp:extent` na souradnice `0..1`.

Centralni zdroj pravdy je `DocxUnitConverter`, `DocxTransformConverter` a `DocxCropConverter`.

## Media part security model

DOCX import/export je navrzeny pro neduveryhodne dokumenty.

Import:

- `DocumentFormatImportOptions.MaxImagePartBytes` omezuje embedded image part; default je 25 MB.
- `DocumentFormatImportOptions.MaxRawDrawingXmlChars` omezuje preserve-only raw DrawingML; default je 128 KB znaku.
- Externi `a:blip/@r:link` se defaultne nestahuje a importuje se jako warning `docx.imageExternalReference`.
- Embedded image relationship se resi pouze pres vlastnicky part (`MainDocumentPart`, `HeaderPart`, `FooterPart`, `FootnotesPart`, `EndnotesPart`, `WordprocessingCommentsPart`).
- Image part path musi byt bezpecny package path. Podezrely path traversal nebo nesmyslny path konci warningem `docx.imageUnsafePartPath`.
- Broken relationship nebo chybejici part nesmi shodit import; vznikne `docx.imageMissingPart`.
- Content type se porovnava s byte signature. Mismatch se odmita jako `docx.imageContentTypeMismatch`.
- Unsupported content type se odmita jako `docx.imageUnsupportedContentType`.
- Warningy pro image chyby nesou `SourcePath` a, kdyz je dostupne, i `ObjectId`.

Export:

- `DocumentFormatExportOptions.MaxImagePartBytes` omezuje image payload; default je 25 MB.
- Provider asset bytes se v jednom exportu cacheuji podle `AssetId`, aby opakovane pouzity obrazek nevolal resolver opakovane.
- Obri data URL se kontroluje pred plnym dekodovanim odhadem base64 velikosti.
- Externi URL se defaultne nestahuji a exportuji se jen jako warning `docx.imageExternalUrlUnsupported` nebo jako placeholder, pokud je explicitne povoleno `AllowImagePlaceholders`.
- Content type/signature mismatch se odmita i pri exportu.

## Unsupported DrawingML preserve/warning policy

Importer rozlisuje picture DrawingML a nepodporovane graficke typy.

Podporovane:

- `pic:pic` s embedded `a:blip`,
- inline i anchored host (`wp:inline`, `wp:anchor`),
- crop, rotation, flip, rect geometry, wrap distances, wrap polygon, effect extent, docPr/cNvPr metadata.

Preserve nebo fallback:

- Neobrazkove DrawingML, chart, SmartArt, diagram nebo canvas group se neimportuje jako obrazek, ale ulozi se do `PreservedParts` s warningem.
- Picture efekty mimo editovatelny model, napriklad shadow/effectDag, se uchovaji v `DocumentDocxDrawingMetadata.RawDrawingXml` v limitu `MaxRawDrawingXmlChars`.
- Export pri preserve-only efektu zapise explicitni fallback a emituje warning, napriklad `docx.drawingUnsupportedEffectExportFallback`.
- Neobvykle horizontal/vertical reference typu inside/outside se aproximuji s fallback warningem.
- Neobdelnikova preset geometry se zachova v metadata, editor ji renderuje jako rect.

Princip: importer nesmi ztratit cely dokument kvuli jednomu nepodporovanemu drawingu. Pokud nelze drawing bezpecne editovat jako obrazek, musi byt zachovan alespon varovny signal a raw/preserved metadata v mezich limitu.

## Fixture sada Word/OnlyOffice

Aktualni fixture sada je generovana v `tests/Tempo.Blazor.DocumentFormats.Tests/DocxDrawing/DocxDrawingFixtureBuilder.cs`. Povinna sada pokryva:

- inline PNG,
- inline JPEG s alt textem,
- anchored Square,
- anchored TopBottom,
- anchored BehindText,
- anchored InFrontOfText,
- anchored Tight/Through s wrap polygonem,
- crop,
- rotation,
- header/footer/table-cell relationships,
- OnlyOffice-like anchor s page-relative pozici, relativeHeight, layoutInCell a allowOverlap.

Obnova nebo rozsireni sady:

1. Vytvorit nebo ziskat referencni DOCX z Wordu nebo OnlyOffice.
2. Rozbalit DOCX jako ZIP a porovnat `word/document.xml`, `word/_rels/document.xml.rels`, header/footer part a `word/media/*`.
3. Do `DocxDrawingFixtureBuilder` pridat minimalni generovany fixture, ktery reprezentuje stejny WordprocessingML/DrawingML tvar.
4. Do phase testu pridat import/export/roundtrip assertion nad konkretni vlastnosti, ne nad cely XML part.
5. Pokud je potreba realny binarni fixture, ulozit ho do `tests/Tempo.Blazor.DocumentFormats.Tests/TestData/DocxDrawing/` a popsat jeho puvod v tamnim `README.md`.
6. Spustit Open XML SDK validator a roundtrip testy.

## Release gate

Automatizovana cast:

```bash
dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocxDrawing"
dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorImageDrawing"
dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --filter "FullyQualifiedName~FormatExportImport"
dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~Phase37_TempoFixture_ExportedDocxPassesOpenXmlValidatorWithoutMajorSchemaErrors"
```

E2E cast vyzaduje bezici Demo API na `https://localhost:5100` a WASM demo na `https://localhost:7106`:

```bash
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorImageOnlyOfficeParityE2ETests|FullyQualifiedName~DocumentEditorImageDocxPhase39E2ETests"
```

Rucni interoperabilita:

- Exportovat demo dokument s inline, Square, TopBottom, BehindText, InFrontOfText, header, footer a table-cell obrazkem.
- Otevrit export v OnlyOffice a zkontrolovat, ze objekty zustaly editovatelne obrazky s odpovidajicim obtikanim.
- Otevrit stejny export ve Wordu nebo Word Online, pokud je k dispozici.
- Importovat export zpet a overit, ze `DocumentDrawingRun` identity, wrap mode, anchor region a media typ zustaly zachovane.

Release lze povazovat za hotovy az tehdy, kdyz projde automatizovana cast, E2E cast a rucni interoperabilita. Automatizovane testy hlidaji schema, roundtrip a runtime chovani; rucni cast hlida kompatibilitu v realnych editorech, kterou unit test sam nevidi.

