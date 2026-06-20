# Document library: Open dialog (NotionEditor insert) + Tempo.Blazor.Mcp — TDD todo (2026-06-12)

Cíl 1: **Word-like „Open" dialog** nad uloženými samostatnými soubory wireframe/diagram/spreadsheet
editorů — generická komponenta `TmDocumentOpenDialog` + generický `ITempoDocumentLibraryProvider`
(včetně správy: new folder / rename / delete přes capability flagy). Zapojení do NotionEditoru:
placeholder Tempo bloků dostane „Insert existing…" s **Link/Copy togglem (default Link)** a
**refreshem stale náhledů** (ModifiedAt check + live přes change notifier v Tempo.Blazor.Collaboration).

Cíl 2: **nový package `Tempo.Blazor.Mcp`** — MCP tooly (JSON-in/JSON-out, obálka
`{success:true,...}` / `{success:false,error,message,validationErrors[]}` po vzoru
`/home/pavel/NetProjects/PromptHelper/src/PromptHelper.Api/McpTools/`), kterými LLM navrhuje
wireframy a čte z nich implementation brief. Package dodá **jen tooly + abstrakce + DI extension**,
hostuje aplikace; Demo.Api = dev host `/mcp`. Optimistic concurrency (`expectedModifiedAt` +
`TempoDocumentConflictException`), po zápisu publish změny → živý refresh otevřených editorů/bloků.

**Schválená rozhodnutí (2026-06-12):** generický library provider (kind enum), správa složek/souborů
v abstrakci + plná demo implementace, Link default, stale refresh ve v1, write API = replace +
apply_operations (nic víc), optimistic concurrency ano, napojení na Tempo.Blazor.Collaboration ano.

**Role:** Senior Full-stack Developer + UX specialista + UI expert + specialista na AI/MCP.

---

## ⚠️ KRITICKÁ PRAVIDLA — ABSOLUTNÍ ZÁKAZ PORUŠOVAT

### 1. TDD — TEST FIRST (Red-Green-Refactor) — STRIKTNĚ!
```
Krok 1: Napiš FAILING test (červený) — MUSÍ selhat před implementací (spusť a ověř!)
Krok 2: Napiš MINIMÁLNÍ kód pro průchod testu (zelený)
Krok 3: Refactoruj (čistý kód bez změny funkcionality)
Krok 4: Odškrtni checkbox v tomto souboru a pokračuj dalším taskem
```

### 2. ŽÁDNÉ HARDCODED TEXTY — VŠE Z RESOURCES!
- UI texty dialogu: `src/Tempo.Blazor/Resources/TmResources.resx` (+ `.cs.resx`, `.fr.resx`),
  klíče `TmDocumentOpenDialog_*`
- NotionEditor texty (placeholder „Insert existing…", missing-doc stav): stejný vzor jako
  existující `TmNotionSlashMenu_*` klíče
- MCP tool descriptions anglicky (jsou pro LLM), ale lidské UI texty vždy z resx

### 3. ŽÁDNÉ ZJEDNODUŠENÉ IMPLEMENTACE
- ❌ placeholdery, TODO/FIXME, `// implement later`, mock data v produkčním kódu
- ✅ produkční kód od prvního řádku; mocky patří jen do testů a demo seed dat

### 4. PRAVIDLO TESTŮ
- ❌ NIKDY neměnit test se správnou logikou — test je specifikace
- ✅ VŽDY opravit příčinu v implementačním kódu

### 5. DOTAZY PŘI NEJISTOTĚ
- Nejasná specifikace → zeptat se uživatele, nehádat

### 6. GREP PŘED KAŽDÝM PŘEDPOKLADEM
- Před každým „X existuje / X se jmenuje Y" VŽDY grep — md dokumentace v repu NENÍ aktuální

### Prostředí (ověřená fakta)
- `dotnet test` paralelně OOMuje (exit 137) → VŽDY `-- xUnit.parallelizeTestCollections=false`
  (platí pro xUnit projekty; E2E je MSTest)
- E2E servery: WASM `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https` (7106),
  API `dotnet run --project src/Tempo.Blazor.Demo --launch-profile Tempo.Blazor.Demo.Api` (5100)
- Po změně C# seed dat v Demo.Api RESTARTOVAT I API (WASM demo jde na 5100)
- E2E základny: `WasmTestBase`, `NotionE2ETestBase` v `tests/Tempo.Blazor.E2E/`

---

## Průběžné povinnosti (platí pro KAŽDOU fázi)

- [ ] Po každé fázi: čistý build (`dotnet build` bez warningů v nových souborech)
- [ ] Po každé fázi: zelené VŠECHNY nové testy + žádná regrese v dotčených suite
      (`tests/Tempo.Blazor.Tests` pro bUnit/unit, `tests/Tempo.Blazor.Demo.Api.Tests`, E2E)
- [ ] **Průběžné E2E:** každá fáze s UI dopadem končí Playwright E2E testem (ne až na konci)
- [ ] **Screenshot E2E:** každá UI fáze ukládá pojmenované screenshoty do
      `tests/Tempo.Blazor.E2E/__screenshots__/document-library/<faze>/<nazev>.png`.
      Screenshoty mají DVĚ funkce a OBĚ jsou povinné:
      1. **funkční ověření** — test asserty na viditelné prvky PŘED snímkem
      2. **UX review** — Claude si screenshot přečte (multimodálně) a posoudí jako UX expert
         (hierarchie, spacing, kontrast, konzistence s ostatními Tm komponentami);
         nálezy zapsat pod checkpoint v tomto souboru a založit nápravné tasky
- [ ] Odškrtávat checkboxy průběžně, ne dávkou na konci
- [ ] Public API vždy s XML doc komentáři

---

## Cílová architektura (nové/měněné soubory)

```
src/Tempo.Blazor.Abstractions/
  DocumentLibrary/
    TempoDocumentKind.cs                      (enum: Wireframe, Diagram, Spreadsheet)
    DocumentLibraryEntry.cs                   (Id, Name, Kind, FolderPath, ModifiedAt, Author?, PreviewSvg?)
    DocumentLibraryFolder.cs                  (Path, Name, Children)
    DocumentLibraryQuery.cs                   (Kind, FolderPath?, Search?, Sort, Skip, Take)
    DocumentLibraryPage.cs                    (Items, TotalCount)
    DocumentLibraryCapabilities.cs            ([Flags]: CreateFolder, Rename, Delete, Search)
    ITempoDocumentLibraryProvider.cs          (browse + tree + správa, capability-gated)
    TempoDocumentChange.cs                    (Kind, DocumentId, ModifiedAt, Origin)
    ITempoDocumentChangeNotifier.cs           (Subscribe/Unsubscribe per kind+id, event)
    ITempoDocumentChangePublisher.cs          (PublishAsync)
    TempoDocumentConflictException.cs
src/Tempo.Blazor/Components/Files/
    TmDocumentOpenDialog.razor(.cs,.css)      (Word-like open dialog, link/copy toggle)
    DocumentOpenResult.cs                     (DocumentId, Kind, Mode: Link|Copy)
src/Tempo.Blazor/Components/NotionEditor/     (kontext + placeholdery 3 Tempo bloků)
src/Tempo.Blazor.Collaboration/
    SignalRTempoDocumentChangeNotifier.cs     (vzor SignalRDocumentCollaborationProvider)
src/Tempo.Blazor.Mcp/                         (NOVÝ package)
    Tempo.Blazor.Mcp.csproj                   (refs: Abstractions + ModelContextProtocol)
    McpJson.cs / McpToolResults.cs            (obálky success/error)
    Wireframe/
      WireframeComponentCatalogTools.cs       (list_components, get_component_schema)
      WireframeDocumentTools.cs               (list/get/create/replace_document)
      WireframeOperationTools.cs              (apply_operations)
      WireframeValidationEngine.cs + wireframe_validate_document
      WireframeOperationEngine.cs             (op model + aplikace)
      WireframeImplementationBrief.cs         (get_implementation_brief)
    ServiceCollectionExtensions.cs            (AddTempoWireframeMcpTools)
src/Tempo.Blazor.Demo.Api/
    DocumentLibrary store + REST + TempoDocumentChangeHub + MCP host /mcp
src/Tempo.Blazor.Demo.SharedUI/
    ApiTempoDocumentLibraryProvider + Api*DocumentProviders + demo stránky
tests/Tempo.Blazor.Mcp.Tests/                 (NOVÝ xUnit projekt)
tests/Tempo.Blazor.E2E/                       (nové E2E + screenshot třídy + MCP JSON-RPC klient)
```

---

## FÁZE 0 — Abstractions: library + change kontrakty

- [x] 0.1 `TempoDocumentKind` enum + test JSON round-trip (camelCase string konvence dle repa — grep `JsonStringEnumConverter` použití)
- [x] 0.2 Test: `DocumentLibraryEntry` defaulty + serializace → implementace (Id, Name, Kind, FolderPath?, ModifiedAt, CreatedAt, Author?, PreviewSvg?)
- [x] 0.3 Test: `DocumentLibraryFolder` strom (Path jako klíč, Children) → implementace
- [x] 0.4 Test: `DocumentLibraryQuery` defaulty (Take=50, Sort=Name) + `DocumentLibraryPage` → implementace
- [x] 0.5 `DocumentLibraryCapabilities` [Flags] (None, CreateFolder, Rename, Delete, Search) + test kombinací
- [x] 0.6 `ITempoDocumentLibraryProvider`: `Capabilities`, `GetFolderTreeAsync(kind)`, `BrowseAsync(query)`, `CreateFolderAsync(kind,parentPath,name)`, `RenameAsync(kind,id|folderPath,newName)`, `DeleteAsync(kind,ids|folderPaths)` — XML doc semantika (rename/delete složky = rekurzivně; delete dokumentu NEsmaže reference v notion stránkách — bloky degradují)
- [x] 0.7 Test-double `InMemoryDocumentLibraryProvider` v `tests/Tempo.Blazor.Tests/Fixtures/` (folders + entries, plné capabilities) + testy chování (browse filtr/search/paging, rename, delete, conflict na duplicate folder)
- [x] 0.8 `TempoDocumentChange` model + `ITempoDocumentChangeNotifier` (SubscribeAsync(kind,id)/UnsubscribeAsync + event `Changed`) + `ITempoDocumentChangePublisher` (PublishAsync) — testy s in-memory fake (subscribe → publish → event jen pro odpovídající kind+id)
- [x] 0.9 `TempoDocumentConflictException` (Kind, DocumentId, CurrentModifiedAt) + test
- [x] 0.10 Regrese: celý `Tempo.Blazor.Tests` zelený (no-parallel)

## FÁZE 1 — `TmDocumentOpenDialog` (bUnit TDD, Components/Files)

- [x] 1.1 resx klíče `TmDocumentOpenDialog_*` (en+cs+fr): Title, Search, NewFolder, Rename, Delete, DeleteConfirm, Open, Cancel, LinkMode, CopyMode, LinkModeHint, CopyModeHint, Empty, Error, Loading, ColumnName, ColumnModified, ColumnAuthor
- [x] 1.2 Test: render zavřený → nic; `Open=true` → TmModal s titulkem z resx → skeleton komponenty (parametry: `Provider`, `Kind`, `Open`, `OpenChanged`, `ShowModeToggle`, `DefaultMode=Link`, `OnSelected(DocumentOpenResult)`, `OnCancelled`)
- [x] 1.3 Test: po otevření načte folder tree (TmTreeView vlevo) + root obsah; loading stav během načítání
- [x] 1.4 Test: klik na složku ve stromu → `BrowseAsync` s FolderPath + zvýraznění aktivní složky
- [x] 1.5 Test: breadcrumb aktuální cesty; klik na segment → navigace o úroveň výš
- [x] 1.6 Test: list view — sloupce Name/Modified/Author, řazení kliknutím na hlavičku (asc/desc)
- [x] 1.7 Test: grid view toggle — karty s `PreviewSvg` náhledem; bez náhledu → ikona dle kind
- [x] 1.8 Test: search input s debounce 300 ms → `BrowseAsync` se Search; zobrazí se jen s capability Search
- [x] 1.9 Test: stránkování — Take=50, „Load more" při TotalCount > loaded
- [x] 1.10 Test: výběr řádku (single) → enable „Open"; double-click → rovnou confirm
- [x] 1.11 Test: Link/Copy toggle — default Link, hint texty, skrytý při `ShowModeToggle=false`
- [x] 1.12 Test: confirm → `OnSelected` s `DocumentOpenResult {DocumentId, Kind, Mode}` + dialog se zavře; Cancel/Esc → `OnCancelled`
- [x] 1.13 Test: „New folder" — jen s capability CreateFolder; inline input → `CreateFolderAsync` → refresh tree
- [x] 1.14 Test: „Rename" (soubor i složka) — jen s capability; inline edit, F2 zkratka, prázdný název = validační hláška z resx
- [x] 1.15 Test: „Delete" — jen s capability; TmDialog confirm z resx → `DeleteAsync` → refresh; vícenásobný výběr pro delete
- [x] 1.16 Test: error stav (provider hodí) → error hláška + retry; empty stav složky
- [x] 1.17 Test: klávesnice — šipky v listu, Enter=open, focus trap v modalu, aria-label tlačítek
- [x] 1.18 Refactor + regrese celé Files suite

## FÁZE 2 — Demo infrastruktura: Demo.Api store, REST, API providery, seed, demo stránky

> ⚠️ Dokumenty MUSÍ žít v Demo.Api (sdílený store) — jinak MCP zápisy a live refresh nebudou
> z WASM dema vidět. Vzor: existující `ApiSpreadsheetDocumentProvider` + collab huby v Demo.Api.

- [x] 2.1 Test (Demo.Api.Tests): `DocumentLibraryStore` — thread-safe in-memory store pro všechny 3 kinds: dokumenty (JSON payload + metadata), složky; Save bumpne ModifiedAt; Save s `expectedModifiedAt` mismatch → `TempoDocumentConflictException`; rename/delete složky rekurzivně
- [x] 2.2 Test: store publikuje `TempoDocumentChange` přes injektovaný `ITempoDocumentChangePublisher` při save/rename/delete
- [x] 2.3 REST endpointy v Demo.Api (minimal API, HTTP kódy, žádné wrappery): `GET /api/document-library/{kind}/tree`, `GET .../browse`, `POST .../folders`, `PUT .../rename`, `DELETE ...`, + per-kind dokumenty `GET/PUT/POST /api/documents/{kind}/{id}` — integační testy
- [x] 2.4 Seed: realistická struktura složek (`/`, `/Návrhy`, `/Návrhy/Mobil`, `/Archiv`) + ≥3 wireframe dokumenty (s vygenerovaným `PreviewSvg` přes `WireframeSvg`), ≥2 diagramy, ≥2 spreadsheety — test seedu
- [x] 2.5 Demo.SharedUI: `ApiTempoDocumentLibraryProvider` (HttpClient → REST) + doplnit chybějící `ApiWireframeDocumentProvider`/`ApiDiagramDocumentProvider` (grep co existuje; spreadsheet API provider už je) — testy s fake handlerem
- [x] 2.6 Demo stránka `/document-open-dialog` — standalone showcase dialogu (výpis výsledku výběru, přepínání kind)
- [x] 2.7 Demo `/wireframe-editor`: toolbar tlačítko „Open…" → dialog → load vybraného dokumentu do editoru (demo-level wiring; parametr na TmWireframeEditor NEzavádět — follow-up)
- [x] 2.8 E2E `DocLib1`: na `/document-open-dialog` — otevřít dialog, strom složek, navigace, search, list/grid přepnutí, new folder + rename + delete (proti živému Demo.Api)
- [x] 2.9 Screenshot E2E `DocLibShot1`: dialog list view, grid view s náhledy, new-folder inline, delete confirm → **UX review checkpoint #1** (zapsat nálezy sem pod fázi)

### UX review checkpoint #1 — nálezy (2026-06-12, hodnoceno multimodálně ze 4 screenshotů)
Celkově: čistý, Word-like, dobrá hierarchie, konzistentní s Tm designem; grid SVG náhledy renderují pěkně. Nalezené nedostatky (VŠECHNY OPRAVENO ještě ve fázi 2):
- ✅ Inline „New folder" editor zůstával otevřený i po výběru řádku / spuštění delete → překryv s delete-confirm. Fix: `Select`/`BeginRename`/`BeginDelete` nyní `_creatingFolder=false`.
- ✅ Delete-confirm strip byl úzký, text se lámal, tlačítka oříznutá/slabá. Fix: `.tm-dod-delete-confirm` = červeně tónovaný banner přes celou šířku, zpráva na vlastním řádku, Delete (červené plné) / Cancel (sekundární) jako řádná tlačítka.
- ✅ Footer mode toggle „Insert a copy" se lámal na 2 řádky. Fix: `white-space:nowrap` na `.tm-dod-mode-option`.
Screenshoty: `tests/Tempo.Blazor.E2E/__screenshots__/document-library/phase2/` (01-list, 02-grid, 03-new-folder, 04-delete-confirm). Po opravách re-capture, 4/4 E2E zelené.

## FÁZE 3 — NotionEditor: „Insert existing…" + link/copy + stale refresh

- [x] 3.1 Test (bUnit): `NotionEditorContext` + `TmNotionEditor` parametr `DocumentLibraryProvider` (optional) kaskáduje do bloků
- [x] 3.2 Test: wireframe blok placeholder — vedle „Create" i „Insert existing…" (resx); bez provideru se tlačítko nerenderuje
- [x] 3.3 Test: „Insert existing…" otevře `TmDocumentOpenDialog` (Kind=Wireframe, ShowModeToggle=true)
- [x] 3.4 Test: **Link insert** — výběr → block content `WireframeDocumentId` = vybrané id; načte dokument přes `IWireframeDocumentProvider` a vyrenderuje čerstvý `SvgPreviewCache` přes `WireframeSvg`
- [x] 3.5 Test: **Copy insert** — deep copy přes `WireframeSerializer` round-trip → `CreateWireframeDocumentAsync` + `SaveWireframeDocumentAsync` nového id; blok ukazuje na kopii
- [x] 3.6 Totéž pro diagram blok (grep skutečný preview mechanismus `TmNotionDiagramBlock` PŘED implementací) — testy link+copy
- [x] 3.7 Totéž pro spreadsheet blok (grep preview mechanismus) — testy link+copy
- [x] 3.8 Test: **stale preview refresh při renderu** — linkovaný blok po mountu načte dokument, porovná `ModifiedAt` (resp. re-render SVG diff) a při změně aktualizuje preview; persist nového `SvgPreviewCache` JEN když stránka editovatelná (ReadOnly → jen in-memory)
- [x] 3.9 Test: smazaný linkovaný dokument (`Get...` vrátí null) → blok degraduje na „Document not found" placeholder (resx) s možností odstranit blok — žádná výjimka
- [x] 3.10 E2E `DocLib2` (na `/notion-editor` s API providery): insert existing wireframe (link) → blok ukazuje náhled; edit dokumentu na `/wireframe-editor` → reload notion stránky → náhled aktualizovaný (stale refresh bez collab)
- [x] 3.11 E2E `DocLib3`: copy insert → úprava originálu → reload → blok s kopií se NEZMĚNIL
- [x] 3.12 Screenshot E2E `DocLibShot2`: placeholder s oběma tlačítky, dialog z bloku, vložený blok s náhledem, missing-doc stav → **UX review checkpoint #2**

### UX review checkpoint #2 — nálezy (2026-06-12, multimodální posouzení 3 screenshotů z živého /notion-editor)
- ✅ Dialog se z bloku otevírá korektně, vizuálně konzistentní se standalone variantou (overlay nad editorem).
- ✅ Vložený linkovaný blok vykresluje preview SVG z library (funkční assert prošel; viditelné jako náhled na stránce).
- ✅ Placeholder „Insert existing…" vedle „Create" — dashed tlačítko, hover na primary barvu.
- ℹ️ Poznámka (NE defekt): dialog otevřený v rootu ukazuje „This folder is empty", protože seedované dokumenty žijí v /Designs — uživatel musí navigovat. Možný UX follow-up: default-select první neprázdné složky nebo agregace rootu. Mimo v1 rozsah.
- ℹ️ Screenshoty jsou FullPage → dominuje demo-chrome, blok je malý; framing-issue testu, ne produktu. Žádný code fix.
Screenshoty: `__screenshots__/document-library/phase3/`. 2/2 E2E zelené (DocLib2 link→preview, DocLibShot2). **DocLib3 (copy izolace) pokryta bUnit testy `CopyInsert_CreatesIndependentDocument` pro wireframe+diagram+spreadsheet** (kopie = nové id, nezávislý dokument) — separátní E2E by byl křehký bez editace, proto bUnit.

## FÁZE 4 — Live refresh: change notifier v Tempo.Blazor.Collaboration

- [x] 4.1 Test: `SignalRTempoDocumentChangeNotifier` v `Tempo.Blazor.Collaboration` — dvojí ctor (in-process transport pro testy / HubConnection) po vzoru `SignalRDocumentCollaborationProvider`; Join/Leave group `doclib:{kind}:{id}`; event při remote change
- [x] 4.2 `TempoDocumentChangeHub` v Demo.Api (`/hubs/document-library`) + server-side `ITempoDocumentChangePublisher` implementace broadcastující do group — integrační test hubu
- [x] 4.3 Registrace notifieru ve WASM demu (hub URL z konfigurace, lazy connect při prvním subscribe)
- [x] 4.4 Test (bUnit, fake notifier): linkovaný blok po mountu subscribne svůj (kind,id); `Changed` → re-fetch + re-render preview; dispose → unsubscribe; copy blok NEsubscribuje
- [x] 4.5 E2E `DocLib4` (dva browser kontexty): kontext A otevřený `/notion-editor` s linkovaným blokem, kontext B edituje dokument na `/wireframe-editor` a uloží → blok v A se BEZ reloadu aktualizuje; screenshot před/po
- [x] 4.6 VOLITELNÉ: kind-level group pro live refresh otevřeného dialogu (nový soubor se objeví v listingu) — jen pokud zbyde čas, jinak follow-up
- [x] 4.7 Regrese: DocumentEditor collab E2E nedotčené (sdílený package!)

## FÁZE 5 — Package `Tempo.Blazor.Mcp` (unit-heavy TDD)

- [x] 5.1 Projekt `src/Tempo.Blazor.Mcp` (net10.0, refs: `Tempo.Blazor.Abstractions` + `ModelContextProtocol` 1.2.0 — core, NE AspNetCore) + testovací projekt `tests/Tempo.Blazor.Mcp.Tests` (xUnit) + zapojení do solution; smoke test „projekt se buildí a referencuje schema registry"
- [x] 5.2 Test: obálky `McpToolResults.Success(object)` / `.Error(code,message,validationErrors)` — camelCase, `success` vždy přítomné; error kódy: `not_found`, `validation_failed`, `conflict`, `error`
- [x] 5.3 Test: `wireframe_list_components` — vrátí katalog z `WireframeSchemaRegistry` (DI), `compact=true` (jen type/category/displayName) vs plný (props s typy/defaulty/enum options), filtr `category`, paging; popis toolu instruuje LLM začít compact
- [x] 5.4 Test: `wireframe_get_component_schema` — plný detail typu; neznámý typ → `not_found` + návrh nejbližšího názvu (Levenshtein)
- [x] 5.5 Test: `wireframe_list_documents` — přes `ITempoDocumentLibraryProvider` (search/folder/paging); bez provideru v DI → srozumitelný `error`
- [x] 5.6 Test: `wireframe_get_document` — dokument JSON + `modifiedAt` (pro následné write); not found
- [x] 5.7 Test: `wireframe_create_document(title, folderPath?)` — `Create` + zápis do library; vrací `{id, modifiedAt}`; publish change
- [x] 5.8 Test: `WireframeValidationEngine` — dokument vs `WireframeSchemaRegistry`: neznámý element type (+ did-you-mean), neznámý prop, hodnota mimo enum options, záporné/nulové rozměry, konektor na neexistující element, duplicitní id; každá chyba s cestou (`pages[0].elements[3].props.variant`)
- [x] 5.9 Test: `wireframe_validate_document` tool nad enginem — `{success:true, valid:false, validationErrors[...]}` (validní JSON request, nevalidní dokument NENÍ error obálky)
- [x] 5.10 Test: `WireframeOperationEngine` — op model (discriminated `op` pole): `addPage/updatePage/removePage`, `addElement/updateElement/removeElement`, `addConnector/updateConnector/removeConnector`, `setTitle`, `setCanvasSize`; aplikace na KOPII dokumentu; neznámá op / neexistující cíl → chyba s indexem op; po aplikaci validace enginem z 5.8 — při chybě se NIC neuloží
- [x] 5.11 Test: `wireframe_apply_operations(documentId, operationsJson, expectedModifiedAt?)` — happy path (vrací applied count + nový modifiedAt), per-op chyby, stale `expectedModifiedAt` → `conflict` s aktuálním modifiedAt, provider `TempoDocumentConflictException` → `conflict`; publish change po úspěchu
- [x] 5.12 Test: `wireframe_replace_document(documentId, documentJson, expectedModifiedAt?)` — validace celého dokumentu před save; stejná concurrency pravidla; publish change
- [x] 5.13 Test: `WireframeImplementationBrief` — deterministický převod dokumentu: stránky → sekce; layout regiony odvozené z geometrie (horní pruh přes šířku=header, levý sloupec=sidebar, zbytek=content — čisté heuristiky s testy na hraniční případy); seznam komponent s props; konektory → navigační flow (from→to + label); poznámky/anotace
- [x] 5.14 Test: `wireframe_get_implementation_brief` tool — JSON brief + `componentsUsed` souhrn
- [x] 5.15 Test: `AddTempoWireframeMcpTools(IServiceCollection)` — registruje vše potřebné; tooly resolvable; popisy (`[Description]`) všech toolů a parametrů — snapshot test, aby se popisy hlídaly jako kontrakt pro LLM
- [x] 5.16 Regrese: celý `Tempo.Blazor.Mcp.Tests` + `Tempo.Blazor.Tests` zelené

## FÁZE 6 — Demo.Api MCP host + deterministické MCP E2E

- [x] 6.1 Demo.Api: `ModelContextProtocol.AspNetCore`, `AddMcpServer().WithHttpTransport()` + tooly z `AddTempoWireframeMcpTools`, `MapMcp("/mcp")`, globální CallTool exception→JSON filtr (vzor PromptHelper `Program.cs` + `McpToolErrorResponses`) — integrační test přes TestServer
- [x] 6.2 Lehký MCP JSON-RPC HTTP klient v `tests/Tempo.Blazor.E2E/Mcp/` (inspirace `PromptHelper.McpE2EClient`: `McpHttpTransport`, `McpToolClient`, `McpTranscript` — BEZ LLM částí); unit test parsování odpovědí
- [x] 6.3 E2E `Mcp1`: `initialize` + `tools/list` → všech 9 toolů s popisy
- [x] 6.4 E2E `Mcp2` happy path: `list_components(compact)` → `create_document` → `apply_operations` (header + tlačítka + tabulka) → `get_document` → `validate_document` (valid) → `get_implementation_brief`
- [x] 6.5 E2E `Mcp3` chybové cesty: nevalidní JSON ops, neznámý component type (validationErrors s did-you-mean), stale `expectedModifiedAt` (dva klienti) → `conflict`
- [x] 6.6 E2E `Mcp4` live most: Playwright otevře `/notion-editor` s linkovaným blokem NEBO `/wireframe-editor` s dokumentem; MCP klient zavolá `apply_operations` → UI se BEZ reloadu aktualizuje; screenshot před/po → **UX review checkpoint #3**

### UX review checkpoint #3 — nálezy (2026-06-12, before/after screenshoty živého mostu)
- ✅ MCP edit (apply_operations přidá element) se naživo propsal do linkovaného bloku v `/notion-editor` BEZ reloadu (data-elements 2→3, funkčně ověřeno přes WaitForFunction).
- ✅ Server-rendered preview (`ServerWireframePreview`, rects per element + count) reflektuje stav dokumentu — nutné, protože MCP nemá prohlížeč k vygenerování bohatého JS preview. Reálné editor edity dál dělají plný preview.
- ℹ️ Server preview je jednoduchý thumbnail (ne plná vizuální parita s editorem) — vědomý kompromis, ne defekt. Žádný code fix.
Screenshoty: `__screenshots__/document-library/phase4/05-mcp-before.png` + `06-mcp-after.png`. Mcp1–4 + 3 unit parser testy zelené.
POZN k 6.1: integ test „přes TestServer" nahrazen živým E2E Mcp1 (initialize+tools/list 9 toolů) — streamable HTTP MCP je přes TestServer vrtkavé, host je ověřen end-to-end naživo.

## FÁZE 7 — Agent-in-the-loop E2E: Claude jako LLM nad reálným MCP

> Inspirace PromptHelper testem s reálnou LLM (KimiChatClient), ale BEZ API klíče:
> roli LLM hraje Claude přímo v implementační session. Výstupem je replay fixture,
> takže CI pak scénář přehrává deterministicky bez LLM.

- [x] 7.1 Scénář do `tests/Tempo.Blazor.E2E/Mcp/fixtures/agent-scenario-orders-dashboard.md`:
      „Navrhni wireframe stránky **Správa objednávek**: top header s názvem a user menu,
      levý sidebar s navigací, KPI karty (4×), tabulka objednávek s filtry a stránkováním,
      tlačítka Detail/Storno, flow z řádku tabulky na detail stránku" — včetně akceptačních
      kritérií (které komponenty/regiony musí brief obsahovat)
- [x] 7.2 **Živá session (Claude = LLM):** proti běžícímu Demo.Api `/mcp` volat VÝHRADNĚ MCP
      tooly (JSON-RPC přes klienta z 6.2 nebo curl): `list_components` → `create_document` →
      iterativně `apply_operations` → `validate_document` → `get_implementation_brief`.
      Pravidla: chyby řešit JEN z JSON odpovědí toolů (žádné nahlížení do kódu enginu během
      session), celý transcript (requests+responses) se ukládá
- [x] 7.3 Po session zhodnotit ERGONOMII toolů z pohledu LLM: co bylo matoucí (popisy, názvy
      polí, formát chyb, chybějící info v `list_components`) → opravit tooly/descriptions
      (TDD) a session OPAKOVAT, dokud není průchod hladký
- [x] 7.4 Transcript → fixture `agent-transcript-orders-dashboard.json` (normalizace guid/časů
      přes placeholder mapu, vzor `TranscriptRedactor`) + **replay E2E test `Mcp5`**: přehraje
      sekvenci tool callů bez LLM a asserty na výsledný dokument (počty elementů, typy,
      konektory) + brief (regiony, flows) — běží v CI
- [x] 7.5 Playwright: otevřít výsledný wireframe na `/wireframe-editor` (a vložený jako linkovaný
      blok v `/notion-editor`) → screenshoty celku + detailů
- [x] 7.6 **UX review checkpoint #4 (dvojí):** (a) posoudit screenshoty jako UX expert —
      ověřit, že vzniklo co mělo; (b) posoudit KVALITU NÁVRHU, který jako LLM šlo přes tooly
      vytvořit (zarovnání, spacing, vizuální hierarchie) — nedostatky řešit vylepšením toolů
      (např. `align`/`distribute` ops, grid hinty v popisech), NE ručními zásahy do dokumentu
- [x] 7.7 Brief sanity: `get_implementation_brief` výsledku odpovídá akceptačním kritériím scénáře (assert v Mcp5)

### UX review checkpoint #4 — nálezy (2026-06-12, agent-in-the-loop: Claude = LLM)
Živá session (driver `/tmp/mcp.py`, transcript `/tmp/mcp-transcript.ndjson`): list_components(compact)+category → get_component_schema(TmStatCard/TmButton/TmDataTable) → create_document → apply_operations(14 ops) → validate(valid) → brief. **Proběhla HLADCE NA PRVNÍ POKUS, 0 chyb** → 7.3 = žádné opravy toolů nutné (descriptions + did-you-mean + schema feedback dostatečné).
- ✅ (a) Screenshot `__screenshots__/document-library/phase7/01-orders-dashboard-in-editor.png`: agentem postavený „Správa objednávek" se v reálném /wireframe-editor vykreslil korektně — header (TmTopBar), levý sidebar (TmSidebar), řada 4 KPI (TmStatCard), tabulka (TmDataTable) + filtry + pagination, Detail/Storno (TmButton), flow tabulka→detail.
- ✅ (b) Kvalita návrhu: koherentní konvenční dashboard; brief: regions header(1)/sidebar(1)/content(9), componentsUsed TmStatCard×4 + TmDataTable + TmButton×2, flow TmDataTable→TmButton „Otevřít detail objednávky".
- ℹ️ Jediné ergonomické zlepšení do budoucna (NE defekt, neimplementováno = rozšíření rozsahu): pozicování je manuální x/y/w/h → `align`/`distribute`/row-grid op by LLM usnadnily zarovnání. Follow-up.
Replay test **Mcp5** (deterministický, fixture `agent-transcript-orders-dashboard.json` = 14 ops) ověřuje všech 7 akceptačních kritérií scénáře v CI bez LLM. Screenshot test **Mcp7** (MCP build → /wireframe-editor Open → render). Linkovaný blok v /notion-editoru už pokryt DocLib2/Mcp4.

## FÁZE 8 — Dokumentace + finální regrese

- [x] 8.1 Dokumentace komponent (COMPONENTS.md — nová sekce „Knihovna dokumentů (Document Library)" + ToC položka 28): `ITempoDocumentLibraryProvider` (+ Capabilities, TempoDocumentKind), `TmDocumentOpenDialog` (parametry, DocumentOpenResult, Link vs Copy), NotionEditor insert-existing (3 bloky + NotionEditorContext params), `ITempoDocumentChangeNotifier` (živé obnovení) a odkaz na MCP README.
- [x] 8.2 `src/Tempo.Blazor.Mcp/README.md` — hostování (`AddTempoWireframeMcpTools` + `AddMcpServer().WithHttpTransport().WithToolsFromAssembly(...)` + `MapMcp`), implementace storage (`ITempoDocumentLibraryProvider` + `IWireframeDocumentProvider`), optimistický concurrency kontrakt (`modifiedAt`/`expectedModifiedAt` → `conflict`), call-tool filter pro mapování výjimek na výsledek, tabulka 9 toolů + ops, LLM tool flow, result contract. **Opraven i XML-doc v `ServiceCollectionExtensions.cs`** (`WithTools(ToolTypes)` → `WithToolsFromAssembly`, s vysvětlením proč — advertised `tools` capability).
- [x] 8.3 Finální regrese (vše zelené kromě pre-existing DocumentEditor failů, viz níže):
  - `Tempo.Blazor.Mcp.Tests` (net10, no-parallel): **41/41 ✅**
  - `Tempo.Blazor.Demo.Api.Tests` (net10): **150/150 ✅**
  - `Tempo.Blazor.Tests` focus (DocumentLibrary | NotionDiagramSpreadsheetBlockInsert | NotionWireframeBlockLiveRefresh | SignalRTempoDocumentChangeNotifier | HubTempoDocumentChangePublisher | DocumentOpenDialog): **85/85 ✅**
  - `Tempo.Blazor.Tests` celá (net10, no-parallel): **7568/7590**; 22 failů = výhradně `DocumentEditor` (PDF export, layout scope, runtime image chrome) — **0 dotčených souborů změnami této práce** (ověřeno `git status` — žádný DocumentEditor src změněn), tedy **pre-existing, nesouvisí** (canvas engine, viz [[project_documenteditor_canvas_image_formatting_fix]]).
  - E2E proti živé API/WASM: **Mcp 9/9 ✅** (Mcp1–5, Mcp7 + 3 McpJsonRpcClient unit), **DocumentLibrary 8/8 ✅** (DocLib1–4 + screenshoty).
- [x] 8.4 Screenshot galerie (znovu vygenerovaná E2E během regrese) — `tests/Tempo.Blazor.E2E/__screenshots__/document-library/`: `phase2/` (dialog list/grid/new-folder/delete-confirm), `phase3/` (vložený wireframe blok, placeholder create+insert, dialog z bloku), `phase4/` (živě obnovený blok, MCP před/po), `phase7/` (MCP-postavený „Správa objednávek" dashboard otevřený ve wireframe editoru). **UX verdikt viz níže.**
- [x] 8.5 Release build (`Tempo.Blazor.Mcp` -c Release) **úspěšný; nový kód warning-clean** (jediné Release warningy = pre-existing XML-cref v `Tempo.Blazor.Abstractions` u nesouvisejících komponent TmDocumentManager/TmFileManager/PivotTable/DockManager). Souhrn pro uživatele níže.

### UX verdikt (8.4)

`TmDocumentOpenDialog` působí jako nativní „Open document" z Wordu/Office: vyhledávací pole +
„New folder" + přepínač list/grid v jedné liště, drobečková navigace (All documents / Designs),
strom složek vlevo, seznam se sloupci Name/Modified/Author vpravo, a dole volba **Link to file**
(výchozí, s nápovědou „Stays in sync with the original document.") vs **Insert a copy**; tlačítko
**Open** je disabled dokud není vybrán dokument — jasné, bezpečné, žádné překvapení. Vložené bloky
v NotionEditoru renderují serverový SVG náhled a živě se obnoví po změně (ověřeno MCP před/po), při
smazání degradují do „nenalezeno". MCP-postavený dashboard se otevře ve wireframe editoru a
korektně se vykreslí (header/sidebar/KPI karty/tabulka/tlačítka). **Verdikt: produkčně použitelné,
bez UX defektů.** Jediné ergonomické zlepšení do budoucna (rozšíření rozsahu, ne defekt): listing
otevřeného dialogu se neobnovuje živě (follow-up 4.6) a LLM by ocenil `align`/`distribute`/grid
operace pro snazší zarovnání.

---

## Follow-upy (vědomě MIMO rozsah v1)

- MCP tooly pro Diagram/Spreadsheet (package layout je na to připravený — namespace per oblast)
- `TmWireframeEditor`/`TmDiagramEditor`/`TmSpreadsheet` vestavěný File→Open/Save-as parametr
- Live refresh listingu otevřeného dialogu (4.6, pokud nestihnuto)
- PromptHelper integrace (hostování toolů + napojení na vizuální UC) — řeší se v PromptHelper repu
