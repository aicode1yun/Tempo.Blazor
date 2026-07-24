# MCP podpora editorů a DocumentEditor canvas-only cutover

Datum: 2026-06-18

Tento plán vznikl ze zdrojového kódu, ne z README ani z existujících plánovacích markdownů. Účel je rozšířit MCP knihovnu mimo současný wireframe-only rozsah a současně připravit bezpečné odebrání všech DocumentEditor render enginů kromě canvas enginu.

## Shrnutí rozhodnutí

- [x] Projít skutečný zdrojový kód MCP knihovny, editorů, providerů, serializerů, JS interopu a relevantních testů.
- [x] Rozšířit MCP knihovnu z wireframe-only balíku na editor/domain toolkit: Wireframe + Diagram/Draw + DocumentEditor + NotionEditor.
- [x] Zachovat zpětně kompatibilní registraci wireframe toolů a přidat novou umbrella registraci pro všechny domény.
- [x] Brát "draweditor" jako existující `TmDiagramEditor`, dokud nebude výslovně rozhodnuto, že má vzniknout samostatný `TmDrawEditor`.
- [x] Oddělit MCP rozšíření od canvas-only cutoveru tak, aby šly testovat a releasovat po menších dávkách.
- [x] Odebrat z `TmDocumentEditor` legacy/core render enginy až po přesměrování testovacího pokrytí na canvas engine.

Můj závěr: dává smysl přidat podporu NotionEditoru, DocumentEditoru a Draw/Diagram editoru. Diagram/Draw je nejpřipravenější, protože už má `TempoDocumentKind.Diagram`, `IDiagramDocumentProvider` a serializer. DocumentEditor má bohaté modely i provider, ale jeho create/list hranice není stejná jako u document library. NotionEditor má největší API povrch a nejvíc providerů, takže potřebuje nejdřív stabilní MCP kontrakt pro stránky, bloky a best-effort concurrency.

## Zdrojové vstupy použité pro analýzu

MCP knihovna:

- `src/Tempo.Blazor.Mcp/ServiceCollectionExtensions.cs`
- `src/Tempo.Blazor.Mcp/McpJson.cs`
- `src/Tempo.Blazor.Mcp/McpToolResults.cs`
- `src/Tempo.Blazor.Mcp/Wireframe/*.cs`
- `src/Tempo.Blazor.Mcp/Tempo.Blazor.Mcp.csproj`
- `tests/Tempo.Blazor.Mcp.Tests/*.cs`

Document library a sdílené abstrakce:

- `src/Tempo.Blazor.Abstractions/DocumentLibrary/*.cs`
- `src/Tempo.Blazor.Abstractions/Wireframe/*.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/**/*.cs`
- `src/Tempo.Blazor.Abstractions/Diagram/**/*.cs`
- `src/Tempo.Blazor.Abstractions/NotionEditor/**/*.cs`

Editory:

- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasEngineHost.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCoreEngineHost.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor`
- `src/Tempo.Blazor/Components/DiagramEditor/**/*.cs`
- `src/Tempo.Blazor/Components/DiagramEditor/**/*.razor`
- `src/Tempo.Blazor/Components/NotionEditor/**/*.cs`
- `src/Tempo.Blazor/Components/NotionEditor/**/*.razor`

JS interop:

- `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/**/*.mjs`
- `src/Tempo.Blazor/wwwroot/js/document-editor/**/*.js`
- `src/Tempo.Blazor/wwwroot/js/document-editor.js`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- `src/Tempo.Blazor/wwwroot/js/document-editor.dist.js`
- `src/Tempo.Blazor/wwwroot/js/diagram-editor.js`

Testy:

- `tests/Tempo.Blazor.Tests/Components/DocumentEditor/**/*.cs`
- `tests/Tempo.Blazor.Tests/Wireframe/*.cs`
- `tests/Tempo.Blazor.Tests/Components/DiagramEditor/**/*.cs`
- `tests/Tempo.Blazor.Mcp.Tests/*.cs`

## Zjištění ze zdrojového kódu

### MCP je dnes wireframe-only

- [x] Přejmenovat interní architektonický koncept z wireframe-only na více domén, ale ponechat veřejnou wireframe registraci.

Aktuální MCP projekt registruje jen wireframe tool typy v `TempoWireframeMcp.ToolTypes`:

- `WireframeComponentCatalogTools`
- `WireframeDocumentTools`
- `WireframeValidationTools`
- `WireframeOperationTools`
- `WireframeBriefTools`

Aktuální veřejné tool názvy jsou:

- `wireframe_list_components`
- `wireframe_get_component_schema`
- `wireframe_list_documents`
- `wireframe_get_document`
- `wireframe_create_document`
- `wireframe_apply_operations`
- `wireframe_replace_document`
- `wireframe_validate_document`
- `wireframe_get_implementation_brief`

Registrace je přes `AddTempoWireframeMcpTools`, která přidává také `AddWireframeSchemas()`. To je dobrý pattern pro další domény, ale název a package metadata už nebudou odpovídat rozšířenému účelu.

Existující MCP testy pinují přesný seznam názvů toolů. Jakmile přibudou další domény, bude potřeba buď rozšířit očekávaný kontrakt, nebo rozdělit test na wireframe-only a all-tools registraci.

Wireframe operation engine dnes používá JSON array operací s diskriminátorem `op`. Tenhle formát je vhodné zachovat i pro diagram/document/notion, protože je už otestovaný a čitelný pro MCP klienty.

Stávající wireframe operace:

- `setTitle`
- `addPage`
- `updatePage`
- `removePage`
- `setCanvasSize`
- `addElement`
- `updateElement`
- `removeElement`
- `addConnector`
- `updateConnector`
- `removeConnector`

### Editor komponenty ve wireframe katalogu už existují

- [x] Nerozdvojovat wireframe schema podporu pro `TmDiagramEditor`, `TmDocumentEditor` a `TmNotionEditor`, pokud cílem je pouze jejich umístění do wireframe.

Ve zdrojových schematech už existují:

- `TmDiagramEditor`
- `TmDocumentEditor`
- `TmNotionEditor`

Jsou definované v `BuiltInComponentSchemas`, promítnuté v `BuiltInWireframeComponentProvider` a kryté wireframe serializer/provider testy. Pokud tedy "podpora komponent" znamená wireframe catalog support, základ už hotový je. Chybí ale MCP doménové nástroje, které umí číst a upravovat reálný diagram/document/notion obsah.

### Document library dnes nepokrývá všechny cíle stejně

- [x] Rozhodnout, jestli se `TempoDocumentKind` rozšíří o `DocumentEditor` a `NotionPage`, nebo jestli tyto domény zůstanou mimo document library. Rozhodnuto: první MCP fáze nechává DocumentEditor i Notion mimo document library a používá jejich existující providery.

`TempoDocumentKind` obsahuje jen:

- `Wireframe`
- `Diagram`
- `Spreadsheet`

To znamená:

- Diagram/Draw může okamžitě kopírovat wireframe document-library pattern.
- DocumentEditor nemá přímý `TempoDocumentKind`, ale má `IDocumentEditorProvider`.
- NotionEditor nemá document-library kind a přirozeně pracuje přes page/block providery.

### Diagram/Draw je nejrychlejší MCP rozšíření

- [x] Implementovat Diagram/Draw MCP jako první novou doménu.

Diagram má:

- `IDiagramDocumentProvider`
- `DiagramDocument`
- `DiagramPage`
- `DiagramNode`
- `DiagramEdge`
- `DiagramSerializer`
- `TempoDocumentKind.Diagram`

Největší otevřený bod je katalog stencilů. `DiagramStencilRegistry` je dnes v hlavním `Tempo.Blazor` projektu, zatímco MCP projekt závisí jen na `Tempo.Blazor.Abstractions`. MCP by nemělo kvůli registru tahat UI knihovnu, pokud tomu jde zabránit.

### DocumentEditor má tři enginy, ale výchozí je canvas

- [x] Připravit canvas-only cutover jako samostatnou fázi s TDD ochranou.

`DocumentEditorRenderEngine` má hodnoty:

- `Legacy`
- `CoreEnginePreview`
- `CanvasEnginePreview`

`TmDocumentEditor` má výchozí `RenderEngine = DocumentEditorRenderEngine.CanvasEnginePreview`, ale stále obsahuje:

- `TmDocumentWysiwygHost`
- `TmDocumentCoreEngineHost`
- `TmDocumentCanvasEngineHost`
- podmíněné renderování podle `EffectiveRenderEngine`
- stav a metody pro core image dialog/context menu
- stav a metody pro legacy wysiwyg interop
- helpery `UsingCoreEngine`, `UsingCanvasEngine` a mnoho branchí v command/save/export toku

Canvas-only odebrání tedy není jen smazání enum hodnot. Je to zásah do veřejného API, Blazor markup větvení, JS assetů, testů a demo script tagů.

### DocumentEditor provider je vhodný pro MCP, ale nemá stejné operace jako wireframe

- [x] Navrhnout MCP tooly kolem `IDocumentEditorProvider`, ne kolem UI hostů.

`IDocumentEditorProvider` poskytuje:

- `LoadAsync`
- `LoadJsonAsync`
- `SaveAsync`

Save request podporuje:

- `Document`
- `JsonSnapshot`
- concurrency token
- autosave/published/draft verze
- normalizaci JSON
- zachování image blocků

Document model má:

- `DocumentEditorDocument`
- `DocumentBlock`
- polymorfní `DocumentBlockContent`
- `DocumentOperationBatch`
- `DocumentOperation`
- `DocumentOperationApplier`

Chybí ale univerzální create/list API na úrovni provideru. MCP create proto musí buď vyžadovat caller-provided `documentId` a uložit prázdný dokument, nebo zavést volitelné MCP-specific create/list rozhraní.

### NotionEditor má nejširší provider povrch

- [x] Navrhnout Notion MCP jako page API s atomickými aggregate block operacemi.

`TmNotionEditor` vyžaduje:

- `INotionDataProvider`
- `INotionAggregateProvider`

Volitelně umí desítky dalších providerů, včetně diagram/wireframe/spreadsheet/document-library vazeb. Základní MCP kontrakt stojí na stránkách a úplných aggregate snapshotech:

- stránky: list/get/create/update/delete/move/duplicate/favorite/restore
- bloky: list/create/update/delete/reorder/move/duplicate/convert
- validace: parent-child vazby, order, block type/content kompatibilita

Notion concurrency je vynucená přes opaque aggregate concurrency tokeny.

## Architektura MCP rozšíření

### Registrace a názvy

- [x] Přidat novou statickou třídu `TempoBlazorMcp` nebo `TempoMcp` pro registraci všech MCP domén.
- [x] Ponechat `TempoWireframeMcp.AddTempoWireframeMcpTools` jako kompatibilní wireframe-only entrypoint.
- [x] Přidat `AddTempoDiagramMcpTools`.
- [x] Přidat `AddTempoDocumentEditorMcpTools`.
- [x] Přidat `AddTempoNotionMcpTools`.
- [x] Přidat `AddTempoBlazorMcpTools`, které zaregistruje všechny dostupné tool typy.
- [x] Rozdělit tool typy po doménách, ideálně namespace:
  - `Tempo.Blazor.Mcp.Wireframe`
  - `Tempo.Blazor.Mcp.Diagram`
  - `Tempo.Blazor.Mcp.DocumentEditor`
  - `Tempo.Blazor.Mcp.Notion`
- [x] Upravit package description/tags v `Tempo.Blazor.Mcp.csproj`, aby už nepopisovaly jen wireframe.

### Sdílená infrastruktura

- [x] Přidat sdílený helper pro JSON parsing `JsonNode` operací s jednotnými error kódy.
- [x] Přidat sdílený helper pro optimistic concurrency přes `DateTime? expectedModifiedAt`.
- [x] Přidat sdílený helper pro optimistic concurrency přes string `expectedConcurrencyToken`.
- [x] Přidat sdílený helper pro bezpečné clone přes serializer/deserializer dané domény.
- [x] Přidat jednotný envelope pro tool výsledky s `success`, `data`, `validationErrors`, `createdIds`, `modifiedAt`, `concurrencyToken`.
- [x] Rozlišit error kódy:
  - `not_found`
  - `validation_failed`
  - `conflict`
  - `invalid_operation`
  - `unsupported`
  - `error`

### Kompatibilita se současným wireframe MCP

- [x] Neměnit existující wireframe tool názvy.
- [x] Neměnit wireframe JSON schema výstupy bez nutnosti.
- [x] Přidat test, že wireframe-only registrace vrací původní tool set.
- [x] Přidat test, že all-tools registrace obsahuje wireframe + nové domény.
- [x] Upravit `TempoWireframeMcpRegistrationTests.ToolNames_MatchExpectedContract`, aby testoval buď jen wireframe registraci, nebo explicitně nový all-tools kontrakt.
- [x] Rozšířit fake backend v MCP testech o diagram/document/notion providery, místo aby všechny domény předstíraly wireframe storage.

## Diagram/Draw MCP TODO

### Rozhodnutí

- [x] Rozhodnout veřejný prefix toolů: `diagram_*` kvůli kódu, nebo `draw_*` kvůli uživatelskému názvu.
- [x] Nepřidávat `draw_*` aliasy v první fázi; veřejný MCP kontrakt používá `diagram_*` a plán dokumentuje mapování draw/editor na `DiagramDocument`.
- [x] Rozhodnout, jestli MCP projekt smí referencovat `Tempo.Blazor` kvůli `DiagramStencilRegistry`. Preferovaná varianta: ne.
- [x] Přesunout nebo abstrahovat stencil catalog do `Tempo.Blazor.Abstractions`, pokud má být dostupný z MCP bez UI závislosti.

### Tool kontrakt

- [x] Přidat `diagram_list_documents`.
- [x] Přidat `diagram_get_document`.
- [x] Přidat `diagram_create_document`.
- [x] Přidat `diagram_replace_document`.
- [x] Přidat `diagram_apply_operations`.
- [x] Přidat `diagram_validate_document`.
- [x] Přidat `diagram_list_stencils`.
- [x] Přidat `diagram_get_stencil`.
- [x] Přidat `diagram_get_implementation_brief`.

### Operace

- [x] `setTitle`
- [x] `addPage`
- [x] `updatePage`
- [x] `removePage`
- [x] `setActivePage`
- [x] `setCanvasSize`
- [x] `addNode`
- [x] `updateNode`
- [x] `removeNode`
- [x] `addEdge`
- [x] `updateEdge`
- [x] `removeEdge`
- [x] `addLayer`
- [x] `updateLayer`
- [x] `removeLayer`
- [x] `reorderLayers`
- [x] `moveItemsToLayer`

### Validace

- [x] Ověřit, že dokument má alespoň jednu stránku nebo umí bezpečně zavolat `EnsurePages()`.
- [x] Ověřit rozsah `ActivePageIndex`.
- [x] Ověřit unikátní page IDs.
- [x] Ověřit unikátní node IDs v rámci stránky.
- [x] Ověřit unikátní edge IDs v rámci stránky.
- [x] Ověřit unikátní layer IDs v rámci stránky.
- [x] Ověřit kladné page rozměry.
- [x] Ověřit kladné node rozměry.
- [x] Ověřit, že `StencilId` existuje, pokud je k dispozici stencil catalog.
- [x] Ověřit, že edge source/target reference existují.
- [x] Ověřit `DiagramEdge.IsValid()`.
- [x] Ověřit layer reference na nodech a hranách.
- [x] Ověřit parent/group reference u node modelu.

### Testy

- [x] Unit test registrace Diagram MCP tool typů.
- [x] Unit test list/get/create přes fake `ITempoDocumentLibraryProvider` + `IDiagramDocumentProvider`.
- [x] Unit test apply operations happy path.
- [x] Unit test conflict přes `expectedModifiedAt`.
- [x] Unit test validation fail pro edge s neexistujícím node.
- [x] Unit test validation fail pro duplicitní node ID.
- [x] Unit test serializer roundtrip pro MCP replace.
- [x] Unit test stencil catalog compact/full výstupu.

## DocumentEditor MCP TODO

### Rozhodnutí

- [x] Rozhodnout create/list strategii, protože `IDocumentEditorProvider` nemá přímé `CreateAsync` ani `ListAsync`. První MCP kontrakt zůstává provider-only bez list/create toolu.
- [x] Rozhodnout, jestli se přidá `TempoDocumentKind.DocumentEditor`. Rozhodnuto: nepřidávat v první fázi, MCP používá `IDocumentEditorProvider`.
- [x] Rozhodnout, jestli vznikne volitelné `IDocumentEditorMcpLibraryProvider` pro list/create/search.
- [x] Rozhodnout, jestli `document_editor_apply_operations` bude používat `DocumentOperationApplier`, nebo bude zpočátku podporovat jen replace/save snapshot.
- [x] Rozhodnout, jestli MCP bude přijímat `JsonSnapshot`, silně typovaný `DocumentEditorDocument`, nebo obojí.

### Tool kontrakt

- [x] Přidat `document_editor_get_document`.
- [x] Přidat `document_editor_get_json`.
- [x] Přidat `document_editor_save_document`.
- [x] Přidat `document_editor_replace_document`.
- [x] Přidat `document_editor_apply_operations`.
- [x] Přidat `document_editor_validate_document`.
- [x] Přidat `document_editor_get_outline`.
- [x] Přidat `document_editor_search_text`.
- [x] Přidat `document_editor_get_versions`, pokud je dostupný version provider.
- [x] Přidat `document_editor_restore_version`, pokud je dostupný version provider.

### Operace

- [x] `insertBlock`
- [x] `deleteBlock`
- [x] `moveBlock`
- [x] `updateBlock`
- [x] `setBlockAttribute`
- [x] `insertText`
- [x] `deleteText`
- [x] `addInlineMark`
- [x] `removeInlineMark`
- [x] `createRevision`
- [x] `acceptRevision`
- [x] `rejectRevision`
- [x] `applyCanvasOperationBatch` je vědomě odmítnuté jako `unsupported`; MCP nepředstírá uložení canvas-only batch payloadu.

### Validace

- [x] Ověřit `DocumentEditorDocument.SchemaVersion`.
- [x] Ověřit `DocumentId`.
- [x] Ověřit unikátní block IDs.
- [x] Ověřit validní parent/child pořadí bloků, pokud model parent vazby používá. Rozhodnuto: `DocumentEditorDocument` používá nested block lists, ne obecnou parent vazbu; validace rekurzivně prochází nested bloky.
- [x] Ověřit kompatibilitu `DocumentBlockContent` s deklarovaným typem bloku.
- [x] Ověřit page settings rozměry a okraje.
- [x] Ověřit table strukturu.
- [x] Ověřit image asset reference.
- [x] Ověřit comment/revision reference na existující bloky/ranges.
- [x] Spustit dostupný post-fixer/normalizer před uložením, pokud je v modelu bezpečný.

### Testy

- [x] Unit test registrace DocumentEditor MCP tool typů.
- [x] Unit test get typed document přes fake `IDocumentEditorProvider`.
- [x] Unit test get raw JSON přes `LoadJsonAsync`.
- [x] Unit test save s concurrency tokenem.
- [x] Unit test conflict přes `BaseConcurrencyToken`.
- [x] Unit test validation fail pro duplicitní block ID.
- [x] Unit test outline pro heading bloky.
- [x] Unit test search text přes paragraph/heading/list obsah.
- [x] Unit test apply operations přes `DocumentOperationApplier`.

## NotionEditor MCP TODO

### Rozhodnutí

- [x] Rozhodnout, jestli Notion MCP bude minimálně vyžadovat jen `INotionDataProvider` + `INotionAggregateProvider`.
- [x] Rozhodnout, které volitelné providery dostanou samostatné tooly v první fázi.
- [x] Rozhodnout concurrency strategii: zvolený a implementovaný je best-effort přes `LastEditedAt`.
- [x] Rozhodnout, jestli MCP blokové schema vznikne reflexí `IBlockContent` polymorfních typů, nebo ručně udržovaným katalogem.

### Tool kontrakt

- [x] Přidat `notion_list_pages`.
- [x] Přidat `notion_get_page`.
- [x] Přidat `notion_create_page`.
- [x] Přidat `notion_update_page`.
- [x] Přidat `notion_delete_page`.
- [x] Přidat `notion_restore_page`.
- [x] Přidat `notion_move_page`.
- [x] Přidat `notion_duplicate_page`.
- [x] Přidat `notion_list_blocks`.
- [x] Přidat `notion_get_block_tree`.
- [x] Přidat `notion_apply_block_operations`.
- [x] Přidat `notion_replace_blocks`.
- [x] Přidat `notion_validate_page`.
- [x] Přidat `notion_list_block_types`.
- [x] Přidat `notion_get_block_schema`.

### Operace

- [x] `createBlock`
- [x] `createBlocks`
- [x] `updateBlockContent`
- [x] `deleteBlock`
- [x] `reorderBlocks`
- [x] `moveBlock`
- [x] `moveBlockToPage`
- [x] `duplicateBlock`
- [x] `convertBlockType`
- [x] `setPageLabels`
- [x] `toggleFavorite`

### Editor embedding operace

- [x] Přidat helper operaci pro vložení diagram bloku (`BlockType.Diagram`).
- [x] Přidat helper operaci pro vložení wireframe bloku (`BlockType.Wireframe`).
- [x] Přidat helper operaci pro vložení spreadsheet bloku (`BlockType.Spreadsheet`).
- [x] Prověřit, zda existuje nebo má vzniknout Notion block type pro DocumentEditor dokument. Rozhodnuto: v první fázi nevzniká nový DocumentEditor block type.

### Validace

- [x] Ověřit, že stránka existuje.
- [x] Ověřit unikátní block IDs.
- [x] Ověřit, že každý block `PageId` odpovídá stránce nebo cílové child stránce.
- [x] Ověřit parent block reference.
- [x] Ověřit `Order` kolize v rámci stejného parenta.
- [x] Ověřit kompatibilitu `BlockType` a konkrétního `IBlockContent` typu.
- [x] Ověřit embedded diagram/wireframe/spreadsheet reference minimálně jako povinné neprázdné reference.
- [x] Ověřit, že delete/move neporušuje subtree vazby.

### Testy

- [x] Unit test registrace Notion MCP tool typů.
- [x] Unit test list/get page přes fake `INotionDataProvider`.
- [x] Unit test list block tree přes `FakeNotionAggregateProvider`.
- [x] Unit test create/update/delete block operací.
- [x] Unit test reorder/move block operací.
- [x] Unit test validation fail pro parent reference na neexistující blok.
- [x] Unit test validation fail pro špatný content typ vůči `BlockType`.
- [x] Unit test best-effort conflict přes `expectedLastEditedAt`, pokud bude zvolený.

## DocumentEditor canvas-only cutover TODO

### Veřejné API

- [x] Rozhodnout finální kompatibilitu `DocumentEditorRenderEngine`.
- [x] Varianta A: ponechat enum jen kvůli source compatibility, ale ignorovat ho a označit obsolete.
- [x] Varianta B: odložit odebrání enumu i `[Parameter] RenderEngine` do budoucího major/breaking releasu.
- [x] Upravit XML dokumentaci parametrů, aby nezmiňovala legacy/core engine jako aktivní volbu.
- [x] Odebrat nebo upravit `DocumentEditorRenderEngineFlag`.
- [x] Odebrat `CoreEngineHostedInteropReady`, pokud už nebude mít význam.

### Razor markup

- [x] V `TmDocumentEditor.razor` nahradit render-engine switch jediným `TmDocumentCanvasEngineHost`.
- [x] Odebrat markup pro `TmDocumentCoreEngineHost`.
- [x] Odebrat markup pro `TmDocumentWysiwygHost`.
- [x] Odebrat `data-render-engine-requested`, pokud už nebude diagnosticky potřeba.
- [x] Zkontrolovat empty-state chování, protože dnes je pro canvas branch jiné.
- [x] Zachovat veřejné feature plochy toolbaru/dialogů, pokud je canvas engine podporuje.

### Code-behind

- [x] Odebrat pole `_wysiwygHost`.
- [x] Odebrat pole `_coreHost`.
- [x] Ponechat a zjednodušit `_canvasHost`.
- [x] Odebrat `UsingCoreEngine`.
- [x] Odebrat `UsingCanvasEngine` nebo ho nahradit jednoduchým `_canvasHost is not null`.
- [x] Odebrat core-only stav pro context menu.
- [x] Odebrat core-only image dialog stav.
- [x] Odebrat legacy-only interop branch v save/export/focus toku.
- [x] Sloučit command dispatch na canvas host.
- [x] Sloučit image insert/upload workflow na canvas host.
- [x] Sloučit table insert workflow na canvas host.
- [x] Sloučit find/replace workflow na canvas host.
- [x] Sloučit outline/toc workflow na canvas host.
- [x] Sloučit collaboration/revision workflow na canvas host.
- [x] Ověřit, že autosave stále používá `IDocumentEditorProvider.SaveAsync`.

### Komponenty k odstranění nebo archivaci

- [x] Prověřit všechny reference na `TmDocumentWysiwygHost`.
- [x] Odebrat `TmDocumentWysiwygHost`, pokud už nemá žádné reference.
- [x] Prověřit všechny reference na `TmDocumentCoreEngineHost`.
- [x] Odebrat `TmDocumentCoreEngineHost`, pokud už nemá žádné reference.
- [x] Prověřit všechny reference na `CoreEngineModelConverter`.
- [x] Odebrat core/legacy model converter kód, pokud už není potřeba pro import/export.
- [x] Prověřit CSS třídy specifické pro legacy/core hosty.
- [x] Neodebírat zbylé legacy-named CSS v této fázi: canvas dependency tree stále používá část sdílených `document-editor/render` modulů a marker tříd.

### JavaScript assety

- [x] Vytvořit import graph pro `wwwroot/js/document-editor-canvas`.
- [x] Vytvořit import graph pro `wwwroot/js/document-editor`.
- [x] Ověřit, zda canvas engine importuje cokoliv z legacy/core adresáře.
- [x] Odebrat `document-editor-wysiwyg.js`, pokud už ho nepoužívají demo/testy/host.
- [x] Odebrat `document-editor.js`, pokud už ho nepoužívají demo/testy/host.
- [x] Odebrat `document-editor.dist.js`, pokud už není build artefakt používaný hostem.
- [x] Odebrat `document-editor/core-engine-interop.js`, pokud už není host.
- [x] Ponechat sdílené core/render JS moduly, které canvas dependency tree stále importuje.
- [x] Ponechat `document-editor-canvas/interop.mjs` a jeho dependency tree.

### Demo aplikace

- [x] Odebrat legacy/core script tagy z Demo WASM, Server a InteractiveAuto hostů.
- [x] Ověřit, že demo pořád načítá canvas interop přes ES module import z host komponenty.
- [x] Upravit demo stránky, které explicitně nastavují `RenderEngine = Legacy`.
- [x] Upravit demo stránky, které explicitně nastavují `RenderEngine = CoreEnginePreview`.

### Testy

- [x] Najít všechny test helpery typu `RenderDocumentEditorLegacy`.
- [x] Přepsat helpery na canvas render nebo čistě modelové testy.
- [x] Odebrat testy, které pouze pinují existenci legacy hostu.
- [x] Přesunout behavior coverage z legacy JS testů do canvas engine testů.
- [x] Upravit `CanvasEngineHostRenderTests`, aby nekryl rollback na legacy.
- [x] Zachovat/regenerovat coverage pro save/load/import/export.
- [x] Zachovat/regenerovat coverage pro toolbar commands.
- [x] Zachovat/regenerovat coverage pro image/table/search workflows.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocumentEditor"`.
- [ ] Spustit celý `dotnet test`.

Poznámka k ověření: v této implementační dávce prošly cílené MCP testy, cílené DocumentEditor testy a `dotnet build TempoBlazor.slnx`. Plný `dotnet test` zůstává neodškrtnutý kvůli E2E/demo infrastruktuře.

## Doporučené pořadí implementace

### Fáze 1: Bezpečné rozšíření MCP kostry

- [x] Přidat all-tools registraci bez změny wireframe behavioru.
- [x] Přidat testy registrace pro wireframe-only a all-tools.
- [x] Přidat sdílené MCP helpery.
- [x] Nezasahovat ještě do DocumentEditor enginů.

### Fáze 2: Diagram/Draw MCP

- [x] Implementovat diagram document tools.
- [x] Implementovat diagram operation engine.
- [x] Implementovat diagram validation engine.
- [x] Implementovat diagram brief/catalog tools.
- [x] Pokrýt Diagram MCP unit testy.

### Fáze 3: DocumentEditor MCP

- [x] Implementovat read/save/validate/outline/search tooly.
- [x] Implementovat apply operations přes existující document operation model.
- [x] Vyřešit create/list provider gap.
- [x] Pokrýt DocumentEditor MCP unit testy.

### Fáze 4: Notion MCP

- [x] Implementovat page tools.
- [x] Implementovat block tools.
- [x] Implementovat block operation engine.
- [x] Implementovat validation/schema tools.
- [x] Pokrýt Notion MCP unit testy.

### Fáze 5: Canvas-only DocumentEditor

- [x] Přepsat testy pryč z legacy helperů.
- [x] Zjednodušit `TmDocumentEditor` na canvas host.
- [x] Odstranit legacy/core host komponenty.
- [x] Odstranit nepotřebné legacy/core JS assety.
- [x] Odstranit demo script tagy.
- [x] Spustit cílené testy a projektové buildy.

## Definition of Done

- [x] MCP balík umí registrovat původní wireframe-only tooly beze změny názvů.
- [x] MCP balík umí registrovat all-tools s Wireframe + Diagram/Draw + DocumentEditor + NotionEditor.
- [x] Každá nová doména má document/read tool, mutation tool, validation tool a testy conflict/error pathů.
- [x] Diagram/Draw MCP používá `TempoDocumentKind.Diagram` a `IDiagramDocumentProvider`.
- [x] DocumentEditor MCP používá `IDocumentEditorProvider` a nevolá UI hosty.
- [x] Notion MCP používá `INotionDataProvider` a `INotionAggregateProvider` jako minimální kontrakt.
- [x] `TmDocumentEditor` po cutoveru nerenderuje legacy ani core host.
- [x] Legacy/core DocumentEditor JS assety nejsou načítané demo aplikacemi.
- [x] Testy nepoužívají `RenderDocumentEditorLegacy`.
- [x] Projde `dotnet build TempoBlazor.slnx`.
- [ ] Projde `dotnet test`.

## Rizika a otevřené otázky

- [x] Má veřejný název toolů být `draw_*`, i když zdrojový model je `DiagramDocument`? Rozhodnuto: používat `diagram_*`, `draw` zůstává popis/tag.
- [x] Má MCP projekt zůstat bez závislosti na hlavní UI knihovně `Tempo.Blazor`? Ano, zůstává jen na abstractions + MCP package.
- [x] Má se `DiagramStencilRegistry` přesunout do abstractions, nebo stačí MCP schema bez stencil catalogu? Rozhodnuto: MCP má vlastní abstractions-safe stencil catalog.
- [x] Má se přidat `TempoDocumentKind.DocumentEditor`? Ne v první fázi.
- [x] Má se přidat `TempoDocumentKind.NotionPage`, nebo Notion zůstane mimo document library? Notion zůstává mimo document library.
- [x] Jak striktní má být Notion concurrency? Best-effort přes `LastEditedAt`.
- [x] Má se `DocumentEditorRenderEngine` odebrat hned, nebo nejdřív označit obsolete? Nejdřív označit obsolete a ignorovat.
- [x] Které legacy/core DocumentEditor JS moduly jsou build artefakty a které skutečné runtime dependency? Top-level host/bundle assety odstraněné; sdílený dependency tree ponechaný.
- [x] Má MCP umět vkládat DocumentEditor dokument jako Notion block, když dnes existují bloky pro diagram/wireframe/spreadsheet, ale DocumentEditor block není jasně vyčleněný? Ne v první fázi.

## Pravidlo odškrtávání

Při implementaci budu v tomto souboru odškrtávat pouze položky, které jsou skutečně hotové ve zdrojovém kódu a ověřené testem nebo cílenou kompilací. Rozhodovací položku odškrtnu až ve chvíli, kdy se promítne do kontraktu, testu nebo kódu, ne jen po ústní dohodě.
