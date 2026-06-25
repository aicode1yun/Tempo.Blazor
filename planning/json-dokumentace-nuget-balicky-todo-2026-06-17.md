# JSON dokumentace pro NuGet balíčky - implementační TODO

Datum auditu: 2026-06-17

## Kontext a cíl

Udržet JSON dokumentaci v souladu se skutečným stavem zdrojových kódů a změnit generování tak, aby platilo pravidlo: **co NuGet balíček, to samostatný finální JSON dokumentační soubor**.

Tento checklist je pracovní plán pro implementaci. Při implementaci odškrtávat hotové položky přímo zde.

## Zjištění z detailního průchodu repozitáře

- [x] Zmapována stávající konzolová aplikace `JsonDocumentation/JsonDocumentationGenerator`.
- [x] Zmapovány existující zdrojové JSON soubory v `JsonDocumentation`.
- [x] Zmapovány všechny projekty se skutečným `PackageId`.
- [x] Zmapovány komponenty ve zdrojových kódech `src/Tempo.Blazor/Components`.
- [x] Zmapovány nové komponenty v `src/Tempo.Blazor.EmailTemplates/Components`.
- [x] Zmapovány veřejné API oblasti balíčků bez komponent.
- [x] Ověřeno, že všechny existující JSON zdroje v `JsonDocumentation` jsou syntakticky validní.

### Současný stav generátoru

- [x] `Program.cs` je tvrdě navázaný na jednu složku `Components`, jednu složku `Abstractions` a dva výstupy:
  `tempo-blazor-documentation.json` a `tempo-blazor-abstractions.json`.
- [x] `Program.cs` čte `JsonDocumentation/Components` a `JsonDocumentation/Abstractions` nerekurzivně přes `Directory.GetFiles`.
- [x] Kvůli nerekurzivnímu čtení ignoruje 5 existujících spreadsheet JSONů:
  `SpreadsheetCell`, `SpreadsheetHyperlink`, `SpreadsheetNamedRange`, `SpreadsheetWorkbook`, `TmSpreadsheet`.
- [x] Kvůli nerekurzivnímu čtení ignoruje 18 existujících NotionEditor abstraction JSONů.
- [x] Finální `tempo-blazor-documentation.json` je zastaralý: obsahuje 135 položek, zatímco top-level `JsonDocumentation/Components` má 147 JSON souborů.
- [x] Finální `tempo-blazor-abstractions.json` je zastaralý: obsahuje 31 položek, zatímco top-level `JsonDocumentation/Abstractions` má 34 JSON souborů.
- [x] Ve finálním component JSONu chybí i existující top-level JSONy:
  `TmBarcode`, `TmBottomNavigation`, `TmFloatingActionButton`, `TmMaskedTextBox`, `TmMenu`, `TmNotionEditor`,
  `TmQRCode`, `TmRangeSlider`, `TmRating`, `TmSlider`, `TmSplitter`, `TmStackLayout`.
- [x] Ve finálním abstractions JSONu chybí i existující top-level JSONy:
  `IWireframeSchemaSource`, `WireframeComponentSchema`, `WireframeSchemaRegistry`.
- [x] `ParameterEnricher` neumí kompletní strom komponent; mapuje jen přímé podsložky `src/Tempo.Blazor/Components`.
- [x] `ParameterEnricher.CategoryMap` neobsahuje nové kategorie: `AITools`, `Chat`, `Diagram`, `DocumentEditor`, `Modeling`, `PivotTable`, `Signing`, `Spreadsheet`, `Wireframe` a další nové oblasti.
- [x] `ParameterEnricher` jen obohacuje existující JSONy, negeneruje chybějící JSON skeletony.
- [x] `ParameterEnricher` používá regexy vhodné jen pro jednoduché případy; neřeší robustně multi-line XML summary, `[EditorRequired]`, komplexní `[Parameter]` atributy, generické komponenty a rozsáhlé code-behind soubory.
- [ ] V řešení zatím nejsou testy pro `JsonDocumentationGenerator`.

### NuGet balíčky, které mají mít samostatný JSON

- [x] `Tempo.Blazor` - UI komponenty, služby, CSS/JS interop, core component API.
- [x] `Tempo.Blazor.Abstractions` - rozhraní, modely, document editor, Notion, spreadsheet, diagram, wireframe, pivot, document library a další kontrakty.
- [x] `Tempo.Blazor.FluentValidation` - `FluentValidationValidator`, `EditContext` extension, DI extension.
- [x] `Tempo.Blazor.Collaboration` - SignalR collaboration/change notifier providers.
- [x] `Tempo.Blazor.DocumentFormats` - DOCX/ODT/HTML/Markdown import/export a konverzní kontrakty.
- [x] `Tempo.Blazor.EmailTemplates.Abstractions` - email template model, MJML import/export, Scriban templating, DTOs, validátory, rendering kontrakty.
- [x] `Tempo.Blazor.EmailTemplates` - `TmEmailTemplateEditor` a doprovodné UI komponenty/služby.
- [x] `Tempo.Blazor.Mcp` - MCP wireframe tools, tool contracty, validace a operation engine.

## Návrh cílových výstupů

- [x] Potvrdit finální naming výstupů. Doporučené názvy podle PackageId v kebab/lowercase formátu:
  `tempo-blazor.json`,
  `tempo-blazor-abstractions.json`,
  `tempo-blazor-fluentvalidation.json`,
  `tempo-blazor-collaboration.json`,
  `tempo-blazor-documentformats.json`,
  `tempo-blazor-emailtemplates-abstractions.json`,
  `tempo-blazor-emailtemplates.json`,
  `tempo-blazor-mcp.json`.
- [x] Rozhodnout, zda zachovat `tempo-blazor-documentation.json` jako kompatibilní alias pro `Tempo.Blazor`.
- [x] Rozhodnout, zda bude každý finální JSON obsahovat jednotný root tvar:
  `package`, `gettingStarted`, `items`, `libraryExamples`, případně `assets`/`interop`.
- [x] Přidat do každého finálního JSONu metadata balíčku: `packageId`, `title`, `description`, `targetFrameworks`, `installation`, `dependencies`, `namespaces`.
- [x] Udržet kompatibilní strukturu `items` pro MCP server a existující konzumenty.
- [x] Oddělit dokumentaci komponent od veřejných API položek pomocí `kind` (`Component`, `Interface`, `Class`, `Record`, `Enum`, `Service`, `Tool`, `Function`, `Model`).

## Inventura zdrojových kódů podle balíčků

### Tempo.Blazor

- [x] Nalezeno 425 `.razor` souborů v projektu, z toho 424 komponentových `.razor` souborů v `Components` a jedna `_Imports.razor`.
- [x] Nalezeno 641 C# zdrojových souborů bez `bin/obj`.
- [x] Nalezeno 765 public type matchů.
- [x] Nalezeno 4007 `[Parameter]` atributů v `Tempo.Blazor` + `Tempo.Blazor.EmailTemplates`.
- [x] Nalezeno 128 `[EditorRequired]` parametrů.
- [x] Nalezeno 72 `[CascadingParameter]`.
- [x] Nalezeno 112 `CaptureUnmatchedValues` parametrů.
- [x] Nalezeno 25 `@typeparam` výskytů.
- [x] Nalezeno 233 `.razor.cs` code-behind souborů v `src/Tempo.Blazor/Components`.
- [x] Nalezeno 149 `.razor.css` souborů v `src/Tempo.Blazor/Components`.

#### Tempo.Blazor - komponentové pokrytí

- [x] Doplnit nebo zrevidovat dokumentaci pro 424 skutečných komponent v `src/Tempo.Blazor/Components`.
- [x] Rozhodnout, které vnitřní subkomponenty mají být public-facing dokumentované a které mají být označené jako internal/support, ale stále dohledatelné.
- [x] Aktualizovat existujících 152 komponentových/modelových JSONů pod `JsonDocumentation/Components`.
- [x] Doplnit 276 komponent, které dnes nemají JSON podle názvu komponenty.
- [x] Odstranit drift mezi source JSONy a finálním generated JSONem.

Chybějící komponentové JSONy podle kategorií:

- [x] `AITools`: 1 (`TmAIPrompt`)
- [x] `Activity`: 1 (`MarkdownToolbar`)
- [x] `Charts`: 3 (`TmGauge`, `TmSparkline`, `TmStockChart`)
- [x] `Chat`: 1 (`TmChat`)
- [x] `DataTable`: 2 (`TmTreeList`, `TmTreeListColumn`)
- [x] `Diagram`: 16 (`TmDiagramEditor`, `TmDiagramCanvas`, toolbox, panels, import dialogs, template gallery/card, minimap, ruler, routing/search/table helpers)
- [x] `DocumentEditor`: 37 (`TmDocumentEditor` a kompletní canvas/core/WYSIWYG shell, toolbar, panels, command palette, comments, revisions, versions, diff, paste/debug modals)
- [x] `Files`: 4 (`TmDocumentManager`, `TmDocumentOpenDialog`, `TmFileManager`, `TmPdfViewer`)
- [x] `Inputs`: 7 (`TmColorGradient`, `TmColorPalette`, `TmColorPicker`, `TmFlatColorPicker`, `TmMultiColumnComboBox`, `TmSignature`, `TmSignatureCapture`)
- [x] `Layout`: 3 (`TmDockManager`, `TmDockPane`, `TmSplitterPane`)
- [x] `Modeling`: 7 (`TmModelingEditor`, diagram preview, inspector, issue panel, model tree, source panel, view selector)
- [x] `Notifications`: 1 (`TmNotificationToastContainer`)
- [x] `NotionEditor`: 128 (bloky, database views/cells, page/sidebar/shared UI, collaboration, analytics, blog, comments, media, synced blocks, Tempo embed blocks)
- [x] `Pickers`: 1 (`TmRecurrenceEditor`)
- [x] `PivotTable`: 4 (`TmPivotTable`, `TmPivotFieldPanel`, `TmPivotFieldTree`, `TmPivotFieldChip`)
- [x] `Scheduler`: 10 (`TmGantt` a Gantt import/export/filter/history/portfolio/reports/task/workload komponenty)
- [x] `Signing`: 29 (audit trail, PDF template/signature verification, signing runner/steps, comments/reactions, recipient/conditions/formula/share/status)
- [x] `Spreadsheet`: 17 (canvas grid, toolbar, formula bar, sheet tabs, status bar, filter dropdown a všechny spreadsheet dialogy)
- [x] `Wireframe`: 4 (`TmWireframeContextMenu`, `TmWireframeExportDialog`, `TmWireframeLayersPanel`, `TmWireframeRuler`)

### Tempo.Blazor.Abstractions

- [x] Nalezeno 708 C# zdrojových souborů bez `bin/obj`.
- [x] Nalezeno 1212 public type matchů.
- [x] Hlavní oblasti: `NotionEditor` 285 souborů, `Models` 138, `DocumentEditor` 80, `Diagram` 57, `Spreadsheet` 57, `Interfaces` 30, `PivotTable` 23, `Wireframe` 20, `DocumentLibrary` 14.
- [x] Rozdělit dokumentaci na public contract oblasti: core interfaces/models, document library, document editor, NotionEditor, diagram, spreadsheet, wireframe, pivot table, modeling, localization.
- [x] Zahrnout již existujících 34 top-level abstraction JSONů.
- [x] Zahrnout již existujících 18 `Abstractions/NotionEditor` JSONů, které současný generátor ignoruje.
- [x] Doplnit chybějící public API JSONy podle priorit: provider interfaces, DTO/record modely, enumy, serializační helpers, service contracts.
- [x] U rozhraní dokumentovat metody, callbacky, cancellation tokeny, default implementace a komponenty, které je používají.

### Tempo.Blazor.FluentValidation

- [x] Nalezeny 3 C# zdrojové soubory bez `bin/obj`: `FluentValidationValidator.cs`, `EditContextFluentValidationExtensions.cs`, `ServiceCollectionExtensions.cs`, plus `_Imports.razor`.
- [x] Nalezeny 3 public type matchů.
- [x] Vytvořit samostatný JSON pro balíček.
- [x] Zdokumentovat `FluentValidationValidator`.
- [x] Zdokumentovat `EditContextFluentValidationExtensions`.
- [x] Zdokumentovat `AddTempoFluentValidation`.
- [x] Přidat instalační a usage příklady pro `EditForm`, assembly scanning a programmatic `EditContext`.

### Tempo.Blazor.Collaboration

- [x] Nalezeny 2 reálné zdrojové C# soubory.
- [x] Nalezeny 4 public type matchů.
- [x] Vytvořit samostatný JSON pro balíček.
- [x] Zdokumentovat `SignalRTempoDocumentChangeNotifier`.
- [x] Zdokumentovat `SignalRDocumentCollaborationProvider`.
- [x] Zdokumentovat očekávané SignalR hub metody/kontrakty a vazbu na document editor abstractions.
- [x] Přidat server/client setup příklad.

### Tempo.Blazor.DocumentFormats

- [x] Nalezeno 19 C# zdrojových souborů bez `bin/obj`.
- [x] Nalezeno 40 public type matchů.
- [x] Vytvořit samostatný JSON pro balíček.
- [x] Zdokumentovat import/export kontrakty `IDocumentFormatImporter`, `IDocumentFormatExporter`.
- [x] Zdokumentovat výsledkové modely: import/export result, warnings, preserved parts, image import/export request/result.
- [x] Zdokumentovat DOCX import/export (`DocumentDocxImporter`, `DocumentDocxExporter`) a limity kompatibility.
- [x] Zdokumentovat ODT import/export.
- [x] Zdokumentovat HTML import/export.
- [x] Zdokumentovat Markdown import/export.
- [x] Zdokumentovat Notion konverze `DocumentModelToNotionConverter` a `NotionToDocumentModelConverter`.
- [x] Přidat server-side usage příklady pro API projekt.

### Tempo.Blazor.EmailTemplates.Abstractions

- [x] Nalezeno 101 C# zdrojových souborů bez `bin/obj`.
- [x] Nalezeno 97 public type matchů.
- [x] Vytvořit samostatný JSON pro balíček.
- [x] Zdokumentovat contracts: `IEmailTemplateStore`, `IEmailSender`.
- [x] Zdokumentovat rendering pipeline: `IEmailTemplateRenderer`, `EmailTemplateRenderer`, `IMjmlCompiler`, `MjmlNetCompiler`, `RenderResult`, `RenderError`.
- [x] Zdokumentovat MJML generator/importer: `MjmlGenerator`, `MjmlGeneratorOptions`, `MjmlImporter`, include resolver, import result/messages.
- [x] Zdokumentovat email template document model: document, section, column, styles, fonts, MJ attributes.
- [x] Zdokumentovat všechny block modely a jejich vazbu na MJML.
- [x] Zdokumentovat DTOs a validátory pro create/update/render/send.
- [x] Zdokumentovat Scriban templating a security options.
- [x] Přidat odkazy na `docs/email-templates/TEMPLATE_SYNTAX.md` a `docs/email-templates/MJML_ATTRIBUTE_PARITY.md`.

### Tempo.Blazor.EmailTemplates

- [x] Nalezeno 16 `.razor` souborů v projektu, z toho 15 reálných komponent a jedna `_Imports.razor`.
- [x] Nalezeno 9 C# zdrojových souborů bez `bin/obj`.
- [x] Nalezeno 10 public type matchů.
- [x] Vytvořit samostatný JSON pro balíček.
- [x] Zdokumentovat `TmEmailTemplateEditor`.
- [x] Zdokumentovat `TmEmailTemplateCanvas`.
- [x] Zdokumentovat `TmEmailTemplateToolbox`.
- [x] Zdokumentovat `TmEmailPropertyPanel`.
- [x] Zdokumentovat `TmEmailTemplatePreview`.
- [x] Zdokumentovat import/export dialogy.
- [x] Zdokumentovat validation panel, variable picker, object fields, table/list/key-value/classes/html attributes editory.
- [x] Zdokumentovat `AddTempoEmailTemplates`, `ITmEmailLocalizer`, autosave/history/clipboard služby a JS asset `tm-email-variable-insert.js`.
- [x] Navázat na starší položku `E12.3` v `planning/tm-email-template-editor-todo-2026-06-11.md`.

### Tempo.Blazor.Mcp

- [x] Nalezeno 12 C# zdrojových souborů bez `bin/obj`.
- [x] Nalezeno 20 public type matchů.
- [x] Vytvořit samostatný JSON pro balíček.
- [x] Zdokumentovat `AddTempoWireframeMcpTools`.
- [x] Zdokumentovat MCP tool list a doporučené hostování přes `WithToolsFromAssembly`.
- [x] Zdokumentovat tool contracty: list components, get schema, list/get/create documents, validate, apply operations, replace document, implementation brief.
- [x] Zdokumentovat operation JSON formát pro `wireframe_apply_operations`.
- [x] Zdokumentovat success/failure envelope a concurrency token `expectedModifiedAt`.
- [x] Zdokumentovat vazbu na `ITempoDocumentLibraryProvider` a `IWireframeDocumentProvider`.

## Implementace generátoru

- [x] Navrhnout konfigurační model balíčků, např. `JsonDocumentation/packages.json` nebo C# konfiguraci v generátoru.
- [x] Každý package config musí obsahovat `packageId`, `sourceProject`, `documentationRoots`, `outputFile`, `gettingStartedFile`, `examplesFile`, `includePatterns`, `excludePatterns`.
- [x] Přepsat načítání item JSONů na rekurzivní a deterministicky seřazené.
- [x] Zajistit, aby rekurzivní načítání umělo zachovat category/subcategory z cesty.
- [x] Přidat podporu balíčkových `gettingStarted` souborů místo jednoho globálního `gettingStarted.json`.
- [x] Přidat podporu balíčkových `libraryExamples` souborů.
- [x] Přidat validaci, že každý `PackageId` ze zdrojových `.csproj` má definovaný výstup nebo explicitní opt-out.
- [x] Přidat validaci, že každý output má neprázdné `items`.
- [x] Přidat validaci, že `itemName` je unikátní v rámci jednoho balíčku.
- [x] Přidat validaci, že `kind`, `category`, `description` a `requiredImports`/`namespace` jsou přítomné podle typu položky.
- [x] Přidat drift kontrolu: existuje-li public component/API ve zdroji a nemá JSON, generátor vypíše warning nebo fail podle režimu.
- [x] Přidat režimy CLI:
  `generate`, `enrich`, `validate`, `list-missing`, případně `--package <PackageId>`.
- [x] Zachovat jednoduché spuštění bez argumentů z rootu repozitáře.
- [x] Zachovat možnost explicitního output path, ale rozšířit ji na output directory pro více balíčků.
- [x] Přidat čisté konzolové summary po balíčcích.

## Implementace enricheru a API extrakce

- [x] Přepsat `ParameterEnricher` tak, aby uměl celý strom komponent včetně vnořených složek.
- [x] Přidat category/subcategory mapování pro všechny aktuální složky komponent.
- [x] Robustně číst `.razor`, `.razor.cs` a další partial soubory stejné komponenty.
- [x] Přidat podporu `[Parameter, EditorRequired]`, `[Parameter]` na více řádcích, nullable typů, generik, tuple typů a `RenderFragment<T>`.
- [x] Přidat podporu `@typeparam` a type parameter dokumentace.
- [ ] Přidat podporu multi-line XML summary, `<param>`, `<returns>`, `<remarks>` a `<inheritdoc>`.
- [x] Rozlišit veřejné parametry od `[CascadingParameter]` a `CaptureUnmatchedValues`.
- [x] Generovat `isRequired` primárně z `[EditorRequired]`, až sekundárně heuristikou podle nullable/default hodnot.
- [x] Zachovat ručně psané příklady a popisy v existujících JSONech při obohacování.
- [x] Přidat skeleton generator pro chybějící komponenty.
- [x] Přidat skeleton generator pro public C# typy z XML dokumentace / source metadata.

## Organizace zdrojových JSON dokumentací

- [x] Rozhodnout cílovou adresářovou strukturu.
- [x] Doporučená struktura:
  `JsonDocumentation/Packages/<PackageId>/gettingStarted.json`,
  `JsonDocumentation/Packages/<PackageId>/items/**/*.json`,
  `JsonDocumentation/Packages/<PackageId>/libraryExamples.json`.
- [ ] Připravit migrační krok ze stávající struktury `Components`, `Abstractions`, `gettingStarted.json`, `libraryExamples.json`.
- [x] Zachovat nebo zdokumentovat kompatibilní legacy strukturu, pokud ji používá MCP server.
- [x] Zabránit duplicitám mezi `Tempo.Blazor` a `Tempo.Blazor.Abstractions`; položka patří do balíčku, kde je public API definované.
- [ ] U komponent v `Tempo.Blazor` uvádět závislosti na abstractions modelech pomocí `relatedTypes`, ne kopírováním modelové dokumentace.
- [ ] U `EmailTemplates` uvádět související modely z `EmailTemplates.Abstractions` pomocí `relatedTypes`.

## Aktualizace existující dokumentace

- [x] Aktualizovat `JsonDocumentation/gettingStarted.json`, pokud zůstane jako legacy `Tempo.Blazor` vstup.
- [x] Aktualizovat seznam balíčků v getting started z původních 3 na aktuálních 8.
- [x] Doplnit aktuální JS interop assety: dashboard, workflow, rich editor, scheduler, pdf viewer, diagram editor, dagre, color picker, file manager, gantt, notion editor, signing, spreadsheet, wireframe, email variable insert.
- [x] Aktualizovat CSS asset informace (`tempo-blazor.bundled.css`, tokens, dark mode).
- [x] Zrevidovat `libraryExamples.json` a rozdělit příklady podle balíčků.
- [x] Zrevidovat staré komponentové JSONy proti aktuálním parametrům.
- [x] Doplnit JSON dokumentaci pro nové oblasti: DocumentEditor, Diagram, NotionEditor, Spreadsheet, Signing, Gantt, PivotTable, Modeling, MCP, EmailTemplates.

## Testy a ověření

- [ ] Přidat test projekt nebo test suite pro `JsonDocumentationGenerator`.
- [x] Test: generátor vytvoří samostatný JSON pro všech 8 PackageId.
- [x] Test: rekurzivní JSON načítání zahrne `Components/Spreadsheet` a `Abstractions/NotionEditor`.
- [x] Test: output je deterministicky seřazený.
- [x] Test: validace odhalí chybějící `itemName`, `kind`, `description`.
- [x] Test: drift check odhalí komponentu bez JSONu.
- [x] Test: `ParameterEnricher` zachytí `[EditorRequired]`, generika, code-behind parametry a multi-line XML summary.
- [x] Test: legacy output aliasy jsou vytvořené nebo záměrně vypnuté podle rozhodnutí.
- [ ] Spustit `dotnet test` pro nový generator test scope.
- [x] Spustit `dotnet build TempoBlazor.slnx`.
- [x] Ručně ověřit `jq` validitu všech finálních JSON výstupů.

## Akceptační kritéria

- [x] V rootu repozitáře vznikne jeden finální JSON pro každý NuGet balíček.
- [x] Každý finální JSON obsahuje správná package metadata a neprázdné `items`.
- [x] `Tempo.Blazor` JSON zahrnuje všechny public-facing komponenty nebo explicitně označené internal/support komponenty podle rozhodnutí.
- [x] `Tempo.Blazor.Abstractions` JSON zahrnuje rekurzivně i NotionEditor abstractions.
- [x] `Tempo.Blazor.EmailTemplates` a `Tempo.Blazor.EmailTemplates.Abstractions` mají vlastní oddělené JSONy.
- [x] `Tempo.Blazor.FluentValidation`, `Tempo.Blazor.Collaboration`, `Tempo.Blazor.DocumentFormats` a `Tempo.Blazor.Mcp` mají vlastní JSONy s veřejnými API a usage příklady.
- [x] Generátor umí spustit `validate` a nehlásí drift mezi zdrojem a JSON dokumentací.
- [x] Finální JSONy jsou validní přes `jq empty`.
- [x] Build a relevantní testy projdou.
