# Analýza rozdělení Tempo.Blazor NuGet balíku

Datum: 2026-06-23
Stav: fáze 0-7 implementovány, další krok je Signing nebo lokalizační resource split

## Implementační stav k 2026-06-23

- Fáze 0 je hotová: core balík už netahá `FluentValidation`, OpenXML verze jsou sjednocené a pack vylučuje testové/interní JS dokumenty.
- Fáze 1 je hotová: `AddTempoBlazor()` je core-only a velké oblasti mají samostatné registrační metody.
- Fáze 2 je hotová: vznikly fyzické RCL balíky `Tempo.Blazor.PdfViewer` a `Tempo.Blazor.Codes`.
- Fáze 3 je hotová: vznikly fyzické RCL balíky `Tempo.Blazor.DiagramEditor`, `Tempo.Blazor.Wireframe` a `Tempo.Blazor.Modeling`.
- Fáze 4 je hotová: vznikl fyzický RCL balík `Tempo.Blazor.Spreadsheet` a optional helper balík `Tempo.Blazor.GanttXlsx`; `DocumentFormat.OpenXml` už není závislost core `Tempo.Blazor`.
- Fáze 5 je hotová: vznikl fyzický RCL balík `Tempo.Blazor.DocumentEditor`; `AngleSharp`, document-editor JS runtime, document-editor-canvas runtime a DocumentEditor CSS už nejsou součást core `Tempo.Blazor`.
- Fáze 6 je hotová: vznikl fyzický RCL balík `Tempo.Blazor.NotionEditor`; `Components/NotionEditor`, `notion-editor.js`, Notion CSS a `AddTempoBlazorNotionEditor()` už nejsou součást core `Tempo.Blazor`.
- Fáze 7 je hotová: vznikl fyzický compat balík `Tempo.Blazor.All`, `JsonDocumentation/packages.json` obsahuje split feature balíky, package-specific JSON výstupy jsou vygenerované a README/release migrační dokumentace je doplněná.
- Demo projekty explicitně referencují a registrují `Tempo.Blazor.PdfViewer`, `Tempo.Blazor.Codes`, `Tempo.Blazor.DiagramEditor`, `Tempo.Blazor.Wireframe`, `Tempo.Blazor.Modeling`, `Tempo.Blazor.Spreadsheet`, `Tempo.Blazor.GanttXlsx`, `Tempo.Blazor.DocumentEditor` a `Tempo.Blazor.NotionEditor`; PDF/Diagram/Wireframe/Spreadsheet/DocumentEditor/NotionEditor assety používají vlastní `_content/{FeaturePackage}` cesty.
- Pack kontrola potvrdila, že core `Tempo.Blazor` už neobsahuje PDF.js assety, `TmPdfViewer`, `TmQRCode`, `TmBarcode`, `QRCoder` ani `ZXing.Net`.
- Build kontrola potvrdila, že nové Diagram/Wireframe/Modeling balíky se samostatně přeloží, demo hosté používají jejich explicitní reference a CSS/JS assety a celé řešení `TempoBlazor.slnx` projde s `0 Error(s)`.
- Pack kontrola fáze 3 potvrdila, že core `Tempo.Blazor.1.0.0.nupkg` má lokálně 8.7 MB a už neobsahuje Diagram/Wireframe JS/CSS/templates/schema assety; nové balíky mají přibližně `Tempo.Blazor.DiagramEditor` 1.2 MB, `Tempo.Blazor.Wireframe` 587 KB a `Tempo.Blazor.Modeling` 226 KB.
- Pack kontrola fáze 4 potvrdila, že core `Tempo.Blazor.1.0.0.nupkg` má lokálně 8.1 MB a už neobsahuje Spreadsheet JS/CSS assety ani `DocumentFormat.OpenXml` dependency; nové balíky mají přibližně `Tempo.Blazor.Spreadsheet` 699 KB a `Tempo.Blazor.GanttXlsx` 33 KB.
- Pack kontrola fáze 5 potvrdila, že core `Tempo.Blazor.1.0.0.nupkg` má lokálně 5.8 MB a už neobsahuje DocumentEditor JS/CSS assety ani `AngleSharp` dependency; nový balík `Tempo.Blazor.DocumentEditor` má přibližně 2.4 MB.
- Pack kontrola fáze 6 potvrdila, že core `Tempo.Blazor.1.0.0.nupkg` má lokálně 3.6 MB a už neobsahuje NotionEditor položky, `notion-editor.js` ani `_notion*.css`; nový balík `Tempo.Blazor.NotionEditor` má přibližně 2.3 MB.
- JSON dokumentace fáze 7 vygenerovala 21 package výstupů a agregát `tempo-blazor-all.json` s 2719 položkami; `JsonDocumentationGenerator validate` prošel.
- Pack kontrola fáze 7 potvrdila, že `Tempo.Blazor.All.1.0.0.nupkg` je malý compat balík (~20 KB) s DLL pro `AddTempoBlazorAll()` a NuGet dependencies na core + split feature balíky.

## Krátký závěr

Rozdělení dává smysl a u Tempo.Blazor je podle aktuální struktury užitečné. Nejde ale jen o přesunutí několika složek s komponentami. Velikost je tvořená kombinací tří věcí:

- velké Razor/C# feature oblasti, hlavně Notion editor, Document editor, Diagram editor, Spreadsheet a Wireframe,
- velké static web assety, hlavně PDF.js, document-editor JS runtime, diagram/spreadsheet JS a společný CSS bundle,
- tranzitivní závislosti, které dnes tahá základní `Tempo.Blazor` balík, i když je běžný konzument nepotřebuje.

Doporučení: pro příští major verzi udělat z `Tempo.Blazor` lehčí core balík a vytvořit samostatné feature balíky. Současný all-in-one způsob zachovat přes nový metabalík `Tempo.Blazor.All`, aby existovala jednoduchá migrační cesta pro aplikace, které chtějí vše.

## Měřený stav

Lokální pack `src/Tempo.Blazor/Tempo.Blazor.csproj` v Release vytvořil balík:

| Metrika | Hodnota | Poznámka |
| --- | ---: | --- |
| `Tempo.Blazor.1.0.0.nupkg` | 12 MB | komprimovaný NuGet balík |
| rozbalený obsah | 46.4 MB | 722 souborů |
| `lib/net8.0/Tempo.Blazor.dll` | 9.6 MB | stejný kód je v nupkg třikrát kvůli net8/net9/net10 |
| `lib/net9.0/Tempo.Blazor.dll` | 9.6 MB | přesun feature kódu se projeví násobeně |
| `lib/net10.0/Tempo.Blazor.dll` | 9.6 MB | největší přímá položka balíku |
| XML dokumentace pro 3 TFM | ~3.8 MB | také se zmenší po přesunu veřejných typů |
| static web assets metadata | ~2.1 MB | roste s počtem assetů |
| scoped CSS bundle | 1.0 MB | `Tempo.Blazor.*.bundle.scp.css` |
| `tempo-blazor.bundled.css` | 696 KB | obsahuje i CSS velkých feature editorů |
| PDF.js worker | 1.0 MB | kandidát na `Tempo.Blazor.PdfViewer` |
| PDF.js runtime | 394 KB | kandidát na `Tempo.Blazor.PdfViewer` |
| `spreadsheet-canvas.js` | 323 KB | kandidát na `Tempo.Blazor.Spreadsheet` |
| `dagre.min.js` | 284 KB | kandidát na `Tempo.Blazor.DiagramEditor` |
| `diagram-editor.js` | 196 KB | kandidát na `Tempo.Blazor.DiagramEditor` |
| `notion-editor.js` | 134 KB | kandidát na `Tempo.Blazor.NotionEditor` |

Největší source oblasti v `src/Tempo.Blazor`:

| Oblast | Přibližná velikost | Poznámka |
| --- | ---: | --- |
| `Components/NotionEditor` | 3.7 MB | největší UI oblast |
| `wwwroot/js/document-editor-canvas` | 2.8 MB | JS engine/runtime, včetně test souborů |
| `wwwroot/js/document-editor` | 2.2 MB | JS runtime pro dokumentový editor |
| `Components/DocumentEditor` | 1.7 MB | velký editor a jeho servisní vrstva |
| `Components/Diagram` | 1.6 MB | editor, příkazy, služby, stencil providery |
| `Components/Spreadsheet` | 844 KB | tabulkový editor + XLSX vrstva |
| `Components/Wireframe` | 712 KB | wireframe editor |
| `Components/Signing` | 504 KB | workflow podpisů a PDF template designer |
| `Components/Scheduler` | 392 KB | včetně Gantt funkcí |
| `Components/Modeling` | 300 KB | modelovací editor nad diagramem |

Důležitý detail: protože balík multi-targetuje `net8.0;net9.0;net10.0`, každý přesunutý veřejný typ a každá přesunutá část assembly zmenšuje `lib/` část typicky třikrát. Static web assety se přesunou jednou, ale zároveň odlehčí metadata static web assets.

## Současné problémové vazby

`Tempo.Blazor.csproj` dnes tahá tyto package reference:

- `AngleSharp` - po fázi 5 už není v core; používá se v `Tempo.Blazor.DocumentEditor` pro clipboard normalizery.
- `CsvHelper` - používá se v diagram CSV importu.
- `DocumentFormat.OpenXml` - po fázi 4 už není v core; používá se v `Tempo.Blazor.Spreadsheet` pro XLSX import/export a v `Tempo.Blazor.GanttXlsx` pro Gantt XLSX import/export.
- `FluentValidation` - v hlavním `Tempo.Blazor` jsem nenašel runtime použití; integrace už existuje jako `Tempo.Blazor.FluentValidation`.
- `QRCoder` - používá `TmQRCode`.
- `ZXing.Net` - používá `TmBarcode`.
- `Microsoft.Extensions.Http` - dnes je potřeba mimo jiné kvůli Diagram/Wireframe/Gantt importům.

To znamená, že split nesmí sledovat jen velikost složek. Pokud chceme core balík opravdu odlehčit, musí se přesunout také feature závislosti. Nejviditelnější rychlá výhra je odstranit `FluentValidation` z `Tempo.Blazor`, protože samostatný integrační balík už existuje.

Další důležitá věc: `AddTempoBlazor()` dnes neregistruje jen základní služby (`ITmLocalizer`, `ThemeService`, `ToastService`, `DragDropService`), ale také Wireframe, Modeling a Diagram registry/providery. Po rozdělení musí být `AddTempoBlazor()` core-only a feature balíky musí mít vlastní registrační metody.

## Cíle návrhu

- Zmenšit default instalaci `Tempo.Blazor` pro aplikace, které potřebují běžné UI komponenty, ale ne velké editory.
- Přesunout těžké static web assety do balíků, které je skutečně používají.
- Přesunout feature závislosti z core balíku do feature balíků.
- Zachovat současné namespace tam, kde to jde, aby migrace byla hlavně o package reference, CSS/JS odkazech a DI registraci.
- Zachovat jednoduchou volbu pro vývojáře, kteří chtějí vše: `Tempo.Blazor.All`.
- Udělat demo tak, aby reálně používalo rozdělené balíky a tím validovalo distribuci.

## Co nedělat v první vlně

- Nepřejmenovávat veřejné namespaces jen kvůli novým package ID. Například `Tempo.Blazor.Components.Diagram` může zůstat i v balíku `Tempo.Blazor.DiagramEditor`.
- Nerozbíjet `Tempo.Blazor.Abstractions` hned v první vlně. Je větší, ale obsahuje integrační modely a backend kontrakty; jeho split by měl výrazně větší migrační dopad.
- Nevyrábět `Tempo.Blazor.Core` jako jediný nový core balík a nenechat `Tempo.Blazor` all-in-one, pokud hlavní cíl je menší default instalace. To by bylo kompatibilnější, ale uživatelé by dál instalovali velký balík pod hlavním názvem.

## Strategie identity balíků

Doporučená strategie pro major verzi:

| Package ID | Role |
| --- | --- |
| `Tempo.Blazor` | lehký core balík s běžnými komponentami |
| `Tempo.Blazor.All` | metabalík, který přitáhne core + všechny feature balíky |
| `Tempo.Blazor.PdfViewer` | PDF viewer a PDF.js assety |
| `Tempo.Blazor.DocumentEditor` | dokumentový editor a jeho JS runtime |
| `Tempo.Blazor.NotionEditor` | Notion-like editor |
| `Tempo.Blazor.DiagramEditor` | diagram editor, stencil registry, diagram importy |
| `Tempo.Blazor.Wireframe` | wireframe editor |
| `Tempo.Blazor.Modeling` | modeling editor nad diagramem |
| `Tempo.Blazor.Spreadsheet` | spreadsheet editor a XLSX podpora |
| `Tempo.Blazor.Signing` | signing workflow a PDF template designer |
| `Tempo.Blazor.Codes` | volitelně QR/barcode komponenty kvůli `QRCoder` a `ZXing.Net` |

Alternativa je vytvořit `Tempo.Blazor.Core` a ponechat `Tempo.Blazor` jako all-in-one metabalík. To je méně breaking, ale neřeší hlavní problém: nejviditelnější NuGet balík by zůstal velký. Proto ji nedoporučuji jako cílový stav, jen jako případnou přechodovou fázi.

## Navržené balíky

### `Tempo.Blazor` core

Obsah:

- design tokens, base CSS, theme service, localization, toast/drag-drop/notification základ,
- běžné komponenty: Buttons, Inputs bez těžkých externích editorů, Forms, Feedback, Layout, Navigation, DataTable, základní DataDisplay, Charts, Pickers, Tags, Timeline, TreeView, Gallery, Files bez `TmPdfViewer`,
- workflow/dashboard/scheduler ponechat v core jen pokud neudrží těžké externí závislosti.

Z core by měly pryč:

- `AngleSharp` - hotovo ve fázi 5 přes `Tempo.Blazor.DocumentEditor`,
- `CsvHelper`, pokud se přesune Diagram CSV import,
- `FluentValidation`, protože je integrační balík,
- `DocumentFormat.OpenXml` - hotovo ve fázi 4 přes `Tempo.Blazor.Spreadsheet` a `Tempo.Blazor.GanttXlsx`,
- `QRCoder` a `ZXing.Net`, pokud se oddělí QR/barcode do `Tempo.Blazor.Codes`.

DI po změně:

```csharp
builder.Services.AddTempoBlazor();
```

Tato metoda má registrovat jen core služby a core notification helpers. Nemá registrovat Diagram, Wireframe ani Modeling providery.

### `Tempo.Blazor.PdfViewer`

Přesunout:

- `TmPdfViewer`,
- `PdfViewMode` a související typy,
- `pdf-viewer.js`,
- `pdf.min.mjs`,
- `pdf.worker.min.mjs`,
- scoped CSS a/nebo feature CSS pro PDF viewer.

DI:

```csharp
builder.Services.AddTempoBlazorPdfViewer();
```

Metoda může být prakticky prázdná, pokud balík nepotřebuje služby, ale je užitečná pro konzistentní setup a budoucí lokalizační resource registraci.

Dopad:

- výrazné odlehčení core static web assets,
- Notion PDF block by měl záviset na tomto balíku místo vlastního PDF loaderu.

### `Tempo.Blazor.DocumentEditor`

Přesunout:

- `Components/DocumentEditor/**`,
- `Tempo.Blazor.DocumentEditor.*` služby/modely, pokud jsou dnes v hlavním projektu,
- `wwwroot/js/document-editor/**`,
- `wwwroot/js/document-editor-canvas/**`,
- document editor CSS soubory,
- `AngleSharp`.

Vyčistit:

- z runtime static web assets vyloučit JS testy, `*.test.mjs`, README a interní test dokumentaci,
- upravit dynamické importy z `./_content/Tempo.Blazor/...` na `./_content/Tempo.Blazor.DocumentEditor/...`.

DI:

```csharp
builder.Services.AddTempoBlazorDocumentEditor();
```

Poznámka k formátům:

- `Tempo.Blazor.DocumentFormats` už existuje a má zůstat samostatný server/client friendly formátový balík.
- Document editor UI by na něj nemusel tvrdě záviset, pokud import/export zůstane přes provider kontrakty. Demo si může přidat oba balíky.

### `Tempo.Blazor.NotionEditor`

Implementováno ve fázi 6:

- `Components/NotionEditor/**`,
- Notion editor services/helpers/interfaces jako runtime UI část,
- `notion-editor.js`,
- Notion CSS soubory,
- vznikl projekt `src/Tempo.Blazor.NotionEditor/Tempo.Blazor.NotionEditor.csproj`,
- veřejné namespace zůstaly pod `Tempo.Blazor.Components.NotionEditor*`, aby migrace byla hlavně o NuGet referenci, DI a assetech.

Rozhodnutí fáze 6:

- Integrované Tempo bloky zůstaly přímo v `Tempo.Blazor.NotionEditor`, protože dnešní Notion editor je s nimi silně propojený.
- Balík proto přímo závisí na `Tempo.Blazor.PdfViewer`, `Tempo.Blazor.DiagramEditor`, `Tempo.Blazor.Wireframe` a `Tempo.Blazor.Spreadsheet`.
- Čistší budoucí varianta zůstává otevřená: základní `Tempo.Blazor.NotionEditor` by časem mohl obsahovat jen text/databáze/page editor a speciální bloky by se přesunuly do `Tempo.Blazor.NotionEditor.TempoBlocks`.

Stav po fázi 6: core `Tempo.Blazor` už neobsahuje Notion editor komponenty, `notion-editor.js`, `_notion*.css`, `AddTempoBlazorNotionEditor()` ani registraci `CommentNotificationOrchestrator`.

Pack kontrola potvrdila:

- core `Tempo.Blazor.1.0.0.nupkg` neobsahuje Notion položky,
- `Tempo.Blazor.NotionEditor.1.0.0.nupkg` obsahuje DLL pro `net8.0`, `net9.0`, `net10.0`, `staticwebassets/js/notion-editor.js` a vlastní Notion CSS vstup.

DI:

```csharp
builder.Services.AddTempoBlazorNotionEditor();
```

Tato metoda volá core `AddTempoBlazor()` a registruje Notion runtime služby. Závislé feature balíky jsou přímé NuGet dependencies Notion balíku; host demo je zároveň referencuje explicitně kvůli čitelnosti assetů a průběžné validaci splitu.

### `Tempo.Blazor.DiagramEditor`

Implementováno ve fázi 3:

- `Components/Diagram/**`,
- diagram služby, stencils, templates,
- embedded `wwwroot/diagram-templates/*.json`,
- `diagram-editor.js`,
- `diagram-arrow-select.js`,
- `dagre.min.js`,
- D3 assety používané diagramem,
- `_diagram-editor.css`,
- `CsvHelper`.
- vznikl projekt `src/Tempo.Blazor.DiagramEditor/Tempo.Blazor.DiagramEditor.csproj`,
- veřejné namespace zůstaly `Tempo.Blazor.Components.Diagram`, aby migrace byla hlavně o NuGet referenci, DI a assetech.

DI:

```csharp
builder.Services.AddTempoBlazorDiagramEditor();
builder.Services.AddDiagramStencilProvider<MyProvider>();
builder.Services.AddJsonDiagramStencilProvider(sources);
```

`AddDiagramStencilProvider<T>()` a `AddJsonDiagramStencilProvider(...)` odešly z core `ServiceCollectionExtensions` do diagram balíku.

Vyřešené vazby:

- Modeling editor referencuje `Tempo.Blazor.DiagramEditor`.
- Notion diagram edit modal v `Tempo.Blazor.NotionEditor` používá komponenty z `Tempo.Blazor.DiagramEditor`; host aplikace je kvůli integrovaným Tempo blokům dostane přes přímou závislost Notion balíku.

### `Tempo.Blazor.Wireframe`

Implementováno ve fázi 3:

- `Components/Wireframe/**`,
- wireframe registry/provider konfiguraci,
- `wireframe-designer.js`,
- `_wireframe-editor.css`.
- `wireframe-document.schema.json`,
- vznikl projekt `src/Tempo.Blazor.Wireframe/Tempo.Blazor.Wireframe.csproj`,
- veřejné namespace zůstaly `Tempo.Blazor.Components.Wireframe`.

DI:

```csharp
builder.Services.AddTempoBlazorWireframe();
builder.Services.AddWireframeComponentProvider<MyProvider>();
```

`AddWireframeComponentProvider<T>()` odešel z core do wireframe balíku.

Poznámka:

- Wireframe modely jsou už z velké části v abstractions, což je dobré pro backend a Notion provider integrace.
- Wireframe může používat diagram modely z `Tempo.Blazor.Abstractions`, aniž by musel záviset na diagram UI balíku.
- Notion wireframe edit modal v `Tempo.Blazor.NotionEditor` používá komponenty z `Tempo.Blazor.Wireframe`; host aplikace je kvůli integrovaným Tempo blokům dostane přes přímou závislost Notion balíku.

### `Tempo.Blazor.Modeling`

Implementováno ve fázi 3:

- `Components/Modeling/**`,
- modeling profiles, relationship/viewpoint rules, mapper a `ModelingDiagramGenerator`,
- modeling CSS isolation.
- vznikl projekt `src/Tempo.Blazor.Modeling/Tempo.Blazor.Modeling.csproj`.

Závislosti:

- `Tempo.Blazor`,
- `Tempo.Blazor.Abstractions`,
- `Tempo.Blazor.DiagramEditor`.

DI:

```csharp
builder.Services.AddTempoBlazorModeling();
```

Tato metoda registruje modeling providery a volá `AddTempoBlazorDiagramEditor()`. Core `AddTempoBlazor()` už modeling neaktivuje.

### `Tempo.Blazor.Spreadsheet`

Přesunout:

- `Components/Spreadsheet/**`,
- spreadsheet JS: `spreadsheet.js`, `spreadsheet-canvas.js`,
- spreadsheet CSS,
- `DocumentFormat.OpenXml` pro XLSX import/export, pokud XLSX zůstane součástí spreadsheet balíku.

DI:

```csharp
builder.Services.AddTempoBlazorSpreadsheet();
```

Důležitá vazba:

- Stav po fázi 4: `DocumentFormat.OpenXml` už není v core; Gantt XLSX import/export je v optional balíku `Tempo.Blazor.GanttXlsx`.

### `Tempo.Blazor.Signing`

Přesunout:

- `Components/Signing/**`,
- `pdf-template-designer.js`,
- signing CSS pro signing workflow.

Nepřesouvat automaticky:

- `TmSignature` a `TmSignatureCapture` jsou dnes v `Components/Inputs` a používají se i jako běžné formulářové inputy. Ty bych v první vlně nechal v core nebo je přesunul až při samostatném rozhodnutí.
- `signature-capture.js` proto pravděpodobně zůstane v core, dokud `TmSignatureCapture` zůstává v core Inputs.

DI:

```csharp
builder.Services.AddTempoBlazorSigning();
```

Riziko:

- `SigningTemplateFromEditorPage` v demu kombinuje Document editor a Signing. Demo musí referencovat oba balíky.

### `Tempo.Blazor.Codes`

Volitelný, ale architektonicky čistý split:

- `TmQRCode`,
- `TmBarcode`,
- QR/barcode CSS,
- `QRCoder`,
- `ZXing.Net`.

Výhoda:

- Core se zbaví dvou specializovaných závislostí.

Nevýhoda:

- QR/barcode jsou malé UI komponenty a uživatelé mohou očekávat, že jsou v základním balíku. Proto bych je dal do první vlny jen pokud je cílem maximálně čistý dependency graph.

### `Tempo.Blazor.All`

Implementováno ve fázi 7:

- vznikl projekt `src/Tempo.Blazor.All/Tempo.Blazor.All.csproj`,
- balík slouží jako kompatibilní all-in cesta pro aplikace, které nechtějí řešit jednotlivé feature reference,
- obsahuje malou registrační extension metodu:

```csharp
builder.Services.AddTempoBlazorAll();
```

Tato metoda volá core a hotové split feature registrace:

- `AddTempoBlazor()`,
- `AddTempoBlazorPdfViewer()`,
- `AddTempoBlazorCodes()`,
- `AddTempoBlazorDiagramEditor()`,
- `AddTempoBlazorWireframe()`,
- `AddTempoBlazorModeling()`,
- `AddTempoBlazorSpreadsheet()`,
- `AddTempoBlazorGanttXlsx()`,
- `AddTempoBlazorDocumentEditor()`,
- `AddTempoBlazorNotionEditor()`,
- `AddTempoBlazorSigning()`; Signing je zatím stále v core.

Pro uživatele, kteří chtějí staré chování, je migrace:

```xml
<PackageReference Include="Tempo.Blazor.All" Version="x.y.z" />
```

```csharp
builder.Services.AddTempoBlazorAll();
```

## CSS a static web assets

Dnes je hlavní CSS vstup `wwwroot/css/tempo-blazor.css`, který importuje i CSS velkých editorů. Po splitu by měl core CSS obsahovat pouze core komponenty a feature balíky by měly mít vlastní CSS.

Doporučené linky pro aplikaci, která používá vše a jejíž feature balíky poskytují samostatný CSS vstup:

```html
<link href="_content/Tempo.Blazor/css/tempo-blazor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.PdfViewer/css/tempo-blazor-pdf-viewer.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.DocumentEditor/css/tempo-blazor-document-editor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.NotionEditor/css/tempo-blazor-notion-editor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.DiagramEditor/css/tempo-blazor-diagram-editor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Wireframe/css/tempo-blazor-wireframe.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Spreadsheet/css/tempo-blazor-spreadsheet.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Signing/css/tempo-blazor-signing.css" rel="stylesheet" />
```

Poznámka k fázi 2 a 3: `Tempo.Blazor.PdfViewer`, `Tempo.Blazor.Codes` a `Tempo.Blazor.Modeling` zatím používají CSS isolation, takže jejich CSS se dostává do host aplikace přes standardní `.styles.css` pipeline, ne přes ručně přidaný stabilní `css/...` soubor. `Tempo.Blazor.DiagramEditor` a `Tempo.Blazor.Wireframe` ve fázi 3 dostaly vlastní stabilní CSS vstupy.

Core CSS musí být první, protože feature CSS má používat core design tokens (`--tm-*`). Feature CSS by nemělo znovu definovat tokeny.

Asset path map:

| Dnes | Po splitu |
| --- | --- |
| `_content/Tempo.Blazor/js/pdf-viewer.js` | `_content/Tempo.Blazor.PdfViewer/js/pdf-viewer.js` |
| `_content/Tempo.Blazor/js/pdf.min.mjs` | `_content/Tempo.Blazor.PdfViewer/js/pdf.min.mjs` |
| `_content/Tempo.Blazor/js/pdf.worker.min.mjs` | `_content/Tempo.Blazor.PdfViewer/js/pdf.worker.min.mjs` |
| `_content/Tempo.Blazor/js/document-editor/**` | `_content/Tempo.Blazor.DocumentEditor/js/document-editor/**` |
| `_content/Tempo.Blazor/js/document-editor-canvas/**` | `_content/Tempo.Blazor.DocumentEditor/js/document-editor-canvas/**` |
| `_content/Tempo.Blazor/js/diagram-editor.js` | `_content/Tempo.Blazor.DiagramEditor/js/diagram-editor.js` |
| `_content/Tempo.Blazor/js/diagram-arrow-select.js` | `_content/Tempo.Blazor.DiagramEditor/js/diagram-arrow-select.js` |
| `_content/Tempo.Blazor/js/dagre.min.js` | `_content/Tempo.Blazor.DiagramEditor/js/dagre.min.js` |
| `_content/Tempo.Blazor/js/d3-*.min.js` | `_content/Tempo.Blazor.DiagramEditor/js/d3-*.min.js` |
| `_content/Tempo.Blazor/js/wireframe-designer.js` | `_content/Tempo.Blazor.Wireframe/js/wireframe-designer.js` |
| `_content/Tempo.Blazor/js/spreadsheet.js` | `_content/Tempo.Blazor.Spreadsheet/js/spreadsheet.js` |
| `_content/Tempo.Blazor/js/spreadsheet-canvas.js` | `_content/Tempo.Blazor.Spreadsheet/js/spreadsheet-canvas.js` |
| `_content/Tempo.Blazor/js/notion-editor.js` | `_content/Tempo.Blazor.NotionEditor/js/notion-editor.js` |
| `_content/Tempo.Blazor/js/pdf-template-designer.js` | `_content/Tempo.Blazor.Signing/js/pdf-template-designer.js` |

Core může dál obsahovat:

- `richEditor.js`,
- `markdownEditor.js`,
- `dashboard.js`,
- `workflow-designer.js`,
- `scheduler.js`, pokud Scheduler/Gantt zůstane core,
- `gantt.js`, pokud Gantt zůstane core,
- `file-manager.js`,
- `color-picker.js`,
- `signature-capture.js`, dokud `TmSignatureCapture` zůstává core input.

## Lokalizace

Dnes jsou resource soubory v `src/Tempo.Blazor/Resources` a obsahují také texty velkých feature oblastí. Resource DLL jsou v nupkg viditelná položka, protože se násobí kulturami a target frameworky.

Možnosti:

1. Přechodová varianta: nechat všechny texty v core resource souboru i po přesunu komponent. Je to nejjednodušší, ale core se nezmenší tolik a feature balíky budou implicitně závislé na core resource klíčích.
2. Doporučená varianta: zavést composite localizer/resource contributor model. Core `DefaultTmLocalizer` zůstane veřejná implementace `ITmLocalizer`, ale feature balíky registrují vlastní resource assembly. `AddTempoBlazorDocumentEditor()` přidá Document editor resources, `AddTempoBlazorDiagramEditor()` diagram resources atd.

Doporučení pro implementaci: pokud chceme split udělat bezpečně, lze resources přesouvat po feature balících postupně, ale už v první vlně připravit API pro registraci resource assembly.

## Dopady do demo aplikací

Demo musí být migrováno tak, aby skutečně používalo rozdělené balíky. Jinak by split neověřil static web assets, DI registrace ani dokumentaci.

### Projekty

`src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj`:

- referencovat core `Tempo.Blazor`,
- přidat reference na feature balíky, protože SharedUI obsahuje demo stránky pro velké komponenty:
  - `Tempo.Blazor.PdfViewer`,
  - `Tempo.Blazor.DocumentEditor`,
  - `Tempo.Blazor.NotionEditor`,
  - `Tempo.Blazor.DiagramEditor`,
  - `Tempo.Blazor.Wireframe`,
  - `Tempo.Blazor.Modeling`,
  - `Tempo.Blazor.Spreadsheet`,
  - `Tempo.Blazor.Signing`, pokud bude splitnuté,
  - `Tempo.Blazor.Codes`, pokud QR/barcode odejdou z core.

Host projekty:

- `src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj`,
- `src/Tempo.Blazor.Demo.Server/Tempo.Blazor.Demo.Server.csproj`,
- `src/Tempo.Blazor.Demo.InteractiveAuto/Tempo.Blazor.Demo.InteractiveAuto.Client/Tempo.Blazor.Demo.InteractiveAuto.Client.csproj`,
- `src/Tempo.Blazor.Demo.InteractiveAuto/Tempo.Blazor.Demo.InteractiveAuto/Tempo.Blazor.Demo.InteractiveAuto.csproj`.

Doporučuji přidat explicitní reference na feature balíky i do host projektů, které načítají CSS/JS assety. Transitivní static web assets přes SharedUI mohou fungovat, ale explicitní reference v hostu jsou čitelnější a snižují riziko překvapení při publish.

Stav po fázi 6: host projekty a SharedUI explicitně referencují `Tempo.Blazor.PdfViewer`, `Tempo.Blazor.Codes`, `Tempo.Blazor.DiagramEditor`, `Tempo.Blazor.Wireframe`, `Tempo.Blazor.Modeling`, `Tempo.Blazor.Spreadsheet`, `Tempo.Blazor.DocumentEditor`, `Tempo.Blazor.NotionEditor` a podle host role také `Tempo.Blazor.GanttXlsx`. Budoucí balík Signing zůstává zatím součástí core podle dalších fází.

`src/Tempo.Blazor.Demo.Api`:

- nemá referencovat UI feature balíky,
- může dál referencovat `Tempo.Blazor.Abstractions`, `Tempo.Blazor.DocumentFormats`, `Tempo.Blazor.Collaboration`, `Tempo.Blazor.Mcp` a backendové kontrakty.

### Demo stránky podle feature balíků

| Balík | Demo stránky / služby |
| --- | --- |
| `Tempo.Blazor.PdfViewer` | `PdfViewerPage.razor`, Notion PDF block |
| `Tempo.Blazor.DocumentEditor` | `DocumentEditorPage.razor`, `CanvasEngineHostPage.razor`, `CoreEngineEditorPage.razor`, `SigningTemplateFromEditorPage.razor`, `DemoDocumentEditorProvider` |
| `Tempo.Blazor.NotionEditor` | `NotionEditorPage.razor`, `PublicNotionPage.razor`, `UnifiedTasksPage.razor`, `DemoNotion*` služby |
| `Tempo.Blazor.DiagramEditor` | `DiagramEditorPage.razor`, `ApiDiagramDocumentProvider`, `MockNotionDiagramDocumentProvider` |
| `Tempo.Blazor.Wireframe` | `WireframeEditorPage.razor`, `ApiWireframeDocumentProvider`, `MockNotionWireframeDocumentProvider` |
| `Tempo.Blazor.Modeling` | `ModelingEditorPage.razor` |
| `Tempo.Blazor.Spreadsheet` | `SpreadsheetPage.razor`, `SpreadsheetBenchmarkPage.razor`, `ApiSpreadsheetDocumentProvider`, `MockNotionSpreadsheetDocumentProvider` |
| `Tempo.Blazor.Signing` | `SigningComponentsPage.razor`, `SigningTemplateFromEditorPage.razor` |
| core `Tempo.Blazor` | zbytek běžných demo stránek, `TmSignature`/`TmSignatureCapture`, pokud zůstanou v Inputs |

### Demo CSS odkazy

Upravit ve všech hostech:

- `src/Tempo.Blazor.Demo/wwwroot/index.html`,
- `src/Tempo.Blazor.Demo.Server/Pages/_Host.cshtml`,
- `src/Tempo.Blazor.Demo.InteractiveAuto/Tempo.Blazor.Demo.InteractiveAuto/Components/App.razor`.

Ponechat:

```html
<link href="_content/Tempo.Blazor/css/tempo-blazor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Reporting/css/tempo-blazor-reporting.css" rel="stylesheet" />
```

Doplnit feature CSS linky pro balíky, které demo používá. Ve fázích 3-6 jsou doplněné:

```html
<link href="_content/Tempo.Blazor.DiagramEditor/css/tempo-blazor-diagram-editor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Wireframe/css/tempo-blazor-wireframe.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Spreadsheet/css/tempo-blazor-spreadsheet.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.DocumentEditor/css/tempo-blazor-document-editor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.NotionEditor/css/tempo-blazor-notion-editor.css" rel="stylesheet" />
```

### Demo script odkazy

Ve všech hostech upravit cesty:

```html
<script src="_content/Tempo.Blazor.Wireframe/js/wireframe-designer.js"></script>
<script src="_content/Tempo.Blazor.Spreadsheet/js/spreadsheet.js"></script>
<script src="_content/Tempo.Blazor.Spreadsheet/js/spreadsheet-canvas.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/dagre.min.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/d3-dispatch.min.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/d3-timer.min.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/d3-quadtree.min.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/d3-hierarchy.min.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/d3-force.min.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/diagram-arrow-select.js"></script>
<script src="_content/Tempo.Blazor.DiagramEditor/js/diagram-editor.js"></script>
<script src="_content/Tempo.Blazor.PdfViewer/js/pdf-viewer.js"></script>
<script src="_content/Tempo.Blazor.NotionEditor/js/notion-editor.js"></script>
```

Pokud vznikne Signing balík:

```html
<script src="_content/Tempo.Blazor.Signing/js/pdf-template-designer.js"></script>
```

`signature-capture.js` zůstane v core, pokud zůstane core `TmSignatureCapture`.

### Demo DI registrace

Před splitem hosté volali jen:

```csharp
builder.Services.AddTempoBlazor();
builder.Services.AddTempoBlazorReporting();
```

Stav po fázi 3: demo hosté už explicitně volají registrace pro hotové feature balíky:

```csharp
builder.Services.AddTempoBlazor();
builder.Services.AddTempoBlazorPdfViewer();
builder.Services.AddTempoBlazorCodes();
builder.Services.AddTempoBlazorDiagramEditor();
builder.Services.AddTempoBlazorWireframe();
builder.Services.AddTempoBlazorModeling();
builder.Services.AddTempoBlazorReporting();
```

Po splitu má demo volat:

```csharp
builder.Services.AddTempoBlazor();
builder.Services.AddTempoBlazorPdfViewer();
builder.Services.AddTempoBlazorDocumentEditor();
builder.Services.AddTempoBlazorDiagramEditor();
builder.Services.AddTempoBlazorWireframe();
builder.Services.AddTempoBlazorModeling();
builder.Services.AddTempoBlazorSpreadsheet();
builder.Services.AddTempoBlazorNotionEditor();
builder.Services.AddTempoBlazorSigning();
builder.Services.AddTempoBlazorReporting();
```

Pokud `Tempo.Blazor.All` bude obsahovat `AddTempoBlazorAll()`, demo by ho nemělo použít jako jedinou cestu. Demo má záměrně používat rozdělené balíky explicitně, aby testovalo jejich setup.

### `_Imports.razor`

`src/Tempo.Blazor.Demo.SharedUI/_Imports.razor` dnes importuje i těžké namespaces globálně. To může zůstat funkční, pokud namespace zůstanou stejné, ale po splitu je lepší:

- core namespaces nechat globálně,
- feature namespaces nechat buď globálně kvůli jednoduchosti dema, nebo je přesunout na konkrétní demo stránky.

Pro demo je přijatelná globální varianta, protože demo má ukazovat celý produkt. Pro knihovní template dokumentaci bych doporučil per-page importy.

### E2E a harness dopady

Přepsat hardcoded importy:

- `/_content/Tempo.Blazor/js/document-editor-canvas/interop.mjs`
- `./_content/Tempo.Blazor/js/document-editor-canvas/entry.mjs`

na:

- `/_content/Tempo.Blazor.DocumentEditor/js/document-editor-canvas/interop.mjs`
- `./_content/Tempo.Blazor.DocumentEditor/js/document-editor-canvas/entry.mjs`

To se týká E2E testů Document editor canvas a `src/Tempo.Blazor.Demo/wwwroot/canvas-engine-harness.html`.

## JSON dokumentace

Současný `JsonDocumentation/packages.json` má pro `Tempo.Blazor` jednu velkou položku:

```json
{
  "packageId": "Tempo.Blazor",
  "sourceProject": "src/Tempo.Blazor/Tempo.Blazor.csproj",
  "outputFile": "tempo-blazor.json",
  "documentationRoots": [ "Components" ],
  "componentRoots": [ "src/Tempo.Blazor/Components" ],
  "includePublicTypes": true,
  "includeAssets": true
}
```

Po fázi 7 je hotovo:

- `Tempo.Blazor` entry dokumentuje core komponenty a core public API,
- `JsonDocumentation/packages.json` obsahuje entries pro fyzicky vyčleněné feature balíky,
- zdrojové JSON itemy vyčleněných feature oblastí jsou přesunuté do `JsonDocumentation/Packages/<PackageId>/items`,
- aggregate `tempo-blazor-all.json` zahrnuje všech 21 konfigurovaných package výstupů,
- `JsonDocumentationGenerator validate` prochází po generování.

Vygenerované nové outputs:

| Package ID | Output |
| --- | --- |
| `Tempo.Blazor.PdfViewer` | `tempo-blazor-pdfviewer.json` |
| `Tempo.Blazor.Codes` | `tempo-blazor-codes.json` |
| `Tempo.Blazor.DocumentEditor` | `tempo-blazor-documenteditor.json` |
| `Tempo.Blazor.NotionEditor` | `tempo-blazor-notioneditor.json` |
| `Tempo.Blazor.DiagramEditor` | `tempo-blazor-diagrameditor.json` |
| `Tempo.Blazor.Wireframe` | `tempo-blazor-wireframe.json` |
| `Tempo.Blazor.Modeling` | `tempo-blazor-modeling.json` |
| `Tempo.Blazor.Spreadsheet` | `tempo-blazor-spreadsheet.json` |
| `Tempo.Blazor.GanttXlsx` | `tempo-blazor-ganttxlsx.json` |
| `Tempo.Blazor.All` | `tempo-blazor-all-package.json` |
| agregát všech balíků | `tempo-blazor-all.json` |

Tento návrh je zapsaný také ve strojově čitelném souboru `JsonDocumentation/package-split-proposal.json`.

## Testovací strategie

První fáze:

- ponechat `tests/Tempo.Blazor.Tests` jako agregovaný test projekt, který referencuje core + feature projekty,
- přidat DI smoke test pro každé `AddTempoBlazorX()`:
  - core neobsahuje diagram/wireframe/modeling služby,
  - feature metoda registruje svoje registry/providery,
  - `AddTempoBlazorAll()` registruje vše.

Druhá fáze:

- postupně rozdělit testy podle balíků:
  - `tests/Tempo.Blazor.DocumentEditor.Tests`,
  - `tests/Tempo.Blazor.DiagramEditor.Tests`,
  - `tests/Tempo.Blazor.Spreadsheet.Tests`,
  - atd.

E2E:

- demo musí běžet se split asset paths,
- přidat smoke E2E pro každou velkou stránku:
  - PDF viewer vykreslí canvas,
  - Document editor načte JS module,
  - Diagram editor najde `window.tmDiagramEditor`,
  - Wireframe editor najde `window.tmWireframeDesigner`,
  - Spreadsheet canvas najde svůj interop,
  - Notion editor umí vložit/otevřít integrovaný blok.

Pack validace:

```bash
dotnet pack src/Tempo.Blazor/Tempo.Blazor.csproj -c Release -o ./packages
dotnet pack src/Tempo.Blazor.PdfViewer/Tempo.Blazor.PdfViewer.csproj -c Release -o ./packages
dotnet pack src/Tempo.Blazor.DocumentEditor/Tempo.Blazor.DocumentEditor.csproj -c Release -o ./packages
dotnet pack src/Tempo.Blazor.DiagramEditor/Tempo.Blazor.DiagramEditor.csproj -c Release -o ./packages
```

Po každém packu kontrolovat:

- velikost nupkg,
- `unzip -l`,
- static web assets path,
- zda balík neobsahuje `*.test.mjs`, README v runtime assetech nebo interní test harness soubory.

## Migrační fáze

### Fáze 0: rychlé odlehčení bez architektonického splitu

- Vyloučit z pack/static web assets:
  - `wwwroot/js/**/*.test.mjs`,
  - `wwwroot/js/**/__tests__/**`,
  - interní README/diagnostic dokumenty, pokud nejsou runtime potřeba.
- Odstranit `FluentValidation` z hlavního `Tempo.Blazor`, pokud build potvrdí, že není používán.
- Sjednotit `DocumentFormat.OpenXml` verzi; po fázi 4 už OpenXML v core není a zůstává jen v balících, které ho reálně potřebují.

### Fáze 1: příprava service boundaries

- Rozdělit `ServiceCollectionExtensions` na core a feature extension metody ještě před fyzickým přesunem projektů.
- Přidat DI testy.
- Připravit asset path konstanty nebo helpery tam, kde komponenty používají dynamický import.

### Fáze 2: nízkorizikové feature balíky

- Hotovo: `Tempo.Blazor.PdfViewer`.
- Hotovo: `Tempo.Blazor.Codes`, aby `QRCoder` a `ZXing.Net` odešly z core.

Tyto oblasti mají jasné assety a relativně omezený dopad.

### Fáze 3: Diagram, Wireframe, Modeling

- Hotovo: `Tempo.Blazor.DiagramEditor`.
- Hotovo: `Tempo.Blazor.Wireframe`.
- Hotovo: `Tempo.Blazor.Modeling`.
- Hotovo: `AddTempoBlazor()` už neregistruje Diagram/Wireframe/Modeling providery.
- Hotovo: demo host scripts používají `_content/Tempo.Blazor.DiagramEditor/...` a `_content/Tempo.Blazor.Wireframe/...`.

### Fáze 4: Spreadsheet a OpenXML rozhodnutí

- Hotovo: `Tempo.Blazor.Spreadsheet` vznikl jako samostatný Razor class library balík.
- Hotovo: Spreadsheet komponenty, scoped CSS, `spreadsheet.js`, `spreadsheet-canvas.js` a `spreadsheet-benchmark.js` se přesunuly z core do `src/Tempo.Blazor.Spreadsheet`.
- Hotovo: demo hosté načítají `_content/Tempo.Blazor.Spreadsheet/css/tempo-blazor-spreadsheet.css` a Spreadsheet skripty z `_content/Tempo.Blazor.Spreadsheet/js/...`.
- Hotovo: `AddTempoBlazorSpreadsheet()` je v `Tempo.Blazor.Spreadsheet` a volá core `AddTempoBlazor()`.
- Hotovo: `GanttExcelImporter` a `GanttXlsxExporter` se přesunuly do `Tempo.Blazor.GanttXlsx`; core Gantt import dialog je vyvolává volitelně přes reflection a při chybějícím balíku zobrazí lokalizovanou chybu.
- Hotovo: `Tempo.Blazor.csproj` už netahá `DocumentFormat.OpenXml`.
- Hotovo: pack kontrola potvrdila, že core nupkg neobsahuje Spreadsheet assety ani OpenXML dependency.

### Fáze 5: Document editor

- Hotovo: `Tempo.Blazor.DocumentEditor` vznikl jako samostatný Razor class library balík.
- Hotovo: DocumentEditor C#/Razor, `IndexedDbDocumentOfflineStore`, document-editor JS runtime, document-editor-canvas runtime a DocumentEditor CSS se přesunuly z core do `src/Tempo.Blazor.DocumentEditor`.
- Hotovo: core `Tempo.Blazor` už netahá `AngleSharp`, neobsahuje `Components/DocumentEditor`, neobsahuje `wwwroot/js/document-editor*` a neimportuje DocumentEditor CSS do `tempo-blazor.css`.
- Hotovo: demo hosté načítají `_content/Tempo.Blazor.DocumentEditor/css/tempo-blazor-document-editor.css`; dynamické importy, E2E odkazy a canvas harness používají `_content/Tempo.Blazor.DocumentEditor/...`.
- Hotovo: `AddTempoBlazorDocumentEditor()` je v `Tempo.Blazor.DocumentEditor` a volá core `AddTempoBlazor()`.
- Hotovo: pack kontrola potvrdila, že core nupkg neobsahuje DocumentEditor assety ani `AngleSharp`; nový DocumentEditor nupkg obsahuje runtime/CSS a neobsahuje test `.mjs`, `__tests__`, JS README ani `.gitkeep`.
- Zůstává vědomý zbytek: `TmDocumentEditor_*` lokalizační resource klíče jsou zatím v core resources. Jejich čisté přesunutí patří do budoucí resource/composite localizer fáze.

### Fáze 6: Notion editor

- Hotovo: `Tempo.Blazor.NotionEditor` vznikl jako samostatný Razor class library balík.
- Hotovo: NotionEditor C#/Razor, `notion-editor.js` a `_notion*.css` se přesunuly z core do `src/Tempo.Blazor.NotionEditor`.
- Hotovo: core `Tempo.Blazor` už neobsahuje Notion komponenty, Notion JS/CSS assety, `AddTempoBlazorNotionEditor()` ani registraci `CommentNotificationOrchestrator`.
- Hotovo: integrované Tempo bloky zůstaly v hlavním Notion balíku a balík má přímé závislosti na `Tempo.Blazor.PdfViewer`, `Tempo.Blazor.DiagramEditor`, `Tempo.Blazor.Wireframe` a `Tempo.Blazor.Spreadsheet`.
- Hotovo: demo hosté načítají `_content/Tempo.Blazor.NotionEditor/css/tempo-blazor-notion-editor.css` a `_content/Tempo.Blazor.NotionEditor/js/notion-editor.js`.
- Hotovo: build/test/pack kontrola potvrdila, že core nupkg neobsahuje Notion položky; nový NotionEditor nupkg obsahuje vlastní DLL, JS, CSS a očekávané feature dependencies.
- Zůstává vědomé budoucí rozhodnutí: pokud bude potřeba menší text/databázový Notion balík, vyříznout integrované Tempo bloky do `Tempo.Blazor.NotionEditor.TempoBlocks`.

### Fáze 7: dokumentace, release a compat

- Hotovo: `Tempo.Blazor.All` vznikl jako fyzický compat balík s `AddTempoBlazorAll()`.
- Hotovo: core `Tempo.Blazor` už neobsahuje core-only `AddTempoBlazorAll()`, aby nevznikla nejednoznačná extension metoda.
- Hotovo: `JsonDocumentation/packages.json` obsahuje package-specific entries pro core, feature balíky, `Tempo.Blazor.All`, reporting a existující doplňkové balíky.
- Hotovo: zdrojové JSON itemy vyčleněných feature oblastí jsou přesunuté do `JsonDocumentation/Packages/<PackageId>/items`.
- Hotovo: vygenerované jsou package-specific výstupy včetně `tempo-blazor-pdfviewer.json`, `tempo-blazor-documenteditor.json`, `tempo-blazor-notioneditor.json`, `tempo-blazor-diagrameditor.json`, `tempo-blazor-wireframe.json`, `tempo-blazor-modeling.json`, `tempo-blazor-spreadsheet.json`, `tempo-blazor-ganttxlsx.json`, `tempo-blazor-codes.json` a `tempo-blazor-all-package.json`.
- Hotovo: agregát `tempo-blazor-all.json` obsahuje 21 package výstupů a 2719 položek.
- Hotovo: `README.md` a `docs/nuget-package-split-migration.md` popisují core vs compat vs explicit feature migraci.
- Hotovo: `JsonDocumentationGenerator validate` prošel.
- Hotovo: `dotnet build TempoBlazor.slnx --no-restore`, `dotnet build src/Tempo.Blazor.All/Tempo.Blazor.All.csproj --no-restore`, registrační testy `ServiceCollectionExtensionsPhase1Tests` a `dotnet pack src/Tempo.Blazor.All/Tempo.Blazor.All.csproj -c Release --no-restore` prošly.
- Hotovo: `.nuspec` nového compat balíku obsahuje dependencies na `Tempo.Blazor`, `Tempo.Blazor.Codes`, `Tempo.Blazor.DiagramEditor`, `Tempo.Blazor.DocumentEditor`, `Tempo.Blazor.GanttXlsx`, `Tempo.Blazor.Modeling`, `Tempo.Blazor.NotionEditor`, `Tempo.Blazor.PdfViewer`, `Tempo.Blazor.Spreadsheet` a `Tempo.Blazor.Wireframe`.

## Rizika a mitigace

| Riziko | Dopad | Mitigace |
| --- | --- | --- |
| Rozbití static asset paths | komponenty se vykreslí bez JS/CSS | centralizovat path konstanty, přidat smoke E2E |
| Chybějící DI registrace | runtime výjimky v editoru | `AddTempoBlazorX()` + DI tests |
| Notion balík zůstane moc velký | split pomůže méně uživatelům Notion editoru | pozdější `NotionEditor.TempoBlocks` |
| Core resources zůstanou velké | core balík se nezmenší maximálně | composite localizer a postupný přesun resources |
| OpenXML zůstane v core kvůli Ganttu | dependency graph core zůstane těžší | vyčlenit Gantt XLSX do optional balíku |
| Namespace přesuny | breaking změny v aplikacích | zachovat namespaces, měnit package references |
| Demo použije metabalík a neověří split | chyby se objeví až u uživatelů | demo explicitně referencuje feature balíky |
| Dokumentace bude duplicitní | JSON output bude matoucí | core exclude patterns + package-specific roots |

## Otevřená rozhodnutí

- Chceme v první major verzi opravdu udělat `Tempo.Blazor` jako core, nebo dát přednost kompatibilnější variantě `Tempo.Blazor.Core`?
- Má `Tempo.Blazor.Signing` vzniknout hned, nebo až po hlavních editorech?
- Má `TmSignatureCapture` zůstat core input, nebo se má časem přesunout do Signing balíku?
- Uzavřeno ve fázi 2: `Tempo.Blazor.Codes` vznikl hned kvůli odstranění `QRCoder`/`ZXing.Net` z core.
- Uzavřeno ve fázi 6: NotionEditor je samostatný feature balík a integrované Tempo bloky zůstaly pro tuto fázi přímo v něm.
- Uzavřeno ve fázi 4: Gantt XLSX import/export je v optional `Tempo.Blazor.GanttXlsx`, takže core nemusí držet OpenXML kvůli Ganttu.
- Uzavřeno ve fázi 5: DocumentEditor je samostatný feature balík; přesun jeho lokalizačních resources zůstává pro composite localizer návrh.
- Otevřené po fázi 6: jestli později vznikne `Tempo.Blazor.NotionEditor.TempoBlocks`, aby mohl být základní Notion balík menší a bez přímých závislostí na PDF/Diagram/Wireframe/Spreadsheet.
- Uzavřeno ve fázi 7: `Tempo.Blazor.All` vznikl jako compat balík a dokumentace se generuje po balíčcích.

## Doporučený další krok

Pokračoval bych jednou ze dvou navazujících větví:

1. Vyčlenit `Tempo.Blazor.Signing`, protože je to poslední větší UI oblast z původního seznamu a stále drží `pdf-template-designer.js` v core.
2. Připravit composite/resource-contributor localizer, aby přesunuté feature balíky mohly časem nést vlastní resource soubory místo core resources.
