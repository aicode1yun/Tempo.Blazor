# Document editor: collaboration, suggestions, server export/import a compare TDD TODO

Stav ověřený v repozitáři k 2026-05-13. Soubor je určený jako živý implementační checklist: při práci vždy odškrtávat hotové kroky a doplnit poznámku, pokud se záměr změní.

Detailní TDD plán pro JavaScript-owned WYSIWYG collaboration engine, provider operation protocol a odstranění text-only fallbacků je v `planning/document-editor-js-collaboration-engine-tdd-todo.md`.

## Pravidla implementace

- [ ] Každý krok začíná RED testem.
- [ ] Ke každé uživatelské funkci vznikne aspoň jeden e2e test.
- [ ] Provider boundary musí zůstat v abstractions nebo v jasně odděleném provider balíčku, ne jako demo-only API.
- [ ] Blazor komponenty nesmí přímo záviset na serverové implementaci.
- [ ] Všechny nové viditelné texty doplnit do `TmResources.resx`, `TmResources.cs.resx`, `TmResources.fr.resx` a `MockTmLocalizer`.
- [ ] Po každé fázi aktualizovat tento checklist.

## Zjištěný aktuální stav

- [x] Existuje `IDocumentCollaborationProvider`.
- [x] Existuje `InMemoryDocumentCollaborationProvider`.
- [x] Existuje `SignalRDocumentCollaborationProvider` wrapper bez přímé SignalR závislosti v abstractions.
- [x] Existuje `DocumentCollaborationSync`.
- [x] Existují unit testy pro provider, operation batches, reconnect a remote cursors.
- [x] `TmDocumentEditor` má veřejný `CollaborationProvider` parametr.
- [x] Collaboration sync je napojený na WYSIWYG i block surface změny editoru.
- [x] Collaboration má e2e test se dvěma page/context instancemi.
- [x] Existuje `DocumentSuggestion`.
- [x] Existuje `IDocumentSuggestionProvider`.
- [x] Existuje lehký `DocumentRevision` model v dokumentu.
- [x] Block surface umí lokálně vytvářet revize při `TrackChangesEnabled`.
- [x] DOCX import/export zachovává podporovanou podmnožinu revizí.
- [x] Provider-backed suggestions jsou napojené do `TmDocumentEditor`.
- [x] Suggestions mají panel, dekorace, accept/reject tok přes provider a e2e.
- [x] Existuje `Tempo.Blazor.DocumentFormats` s `IDocumentFormatImporter` a `IDocumentFormatExporter`.
- [x] Existuje DOCX import/export implementace.
- [x] Existuje ODT import/export implementace.
- [x] Demo API má DOCX/ODT import/export endpointy.
- [x] Demo stránka má DOCX/ODT import/export e2e.
- [ ] Neexistuje reusable serverový document format provider boundary pro komponentu.
- [ ] `TmDocumentEditor` zatím neumí DOCX import/export přes parametr serverového provideru.
- [x] Existuje `IDocumentPdfExportProvider`.
- [x] `TmDocumentEditor` umí zavolat `PdfExportProvider`.
- [x] Existují unit testy pro PDF export provider call a permissions.
- [ ] Demo/API serverový PDF export není hotový.
- [ ] PDF export nemá e2e download test přes server provider.
- [x] Existuje `TmDocumentDiffViewer`.
- [x] Existuje version panel diff mezi dvěma verzemi.
- [x] Existují e2e testy pro version diff.
- [ ] Neexistuje porovnání dvou libovolných dokumentů mimo verze.

## Fáze 0: Baseline a kontrakty plánu

### 0.1 Baseline test run

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" -v:minimal`.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj -v:minimal`.
- [x] Poznamenat do tohoto souboru případné existující failing testy.

Výsledek 2026-05-13:

- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" -v:minimal` prošel: 363 passed, 0 failed, 0 skipped. Během restore/build se objevil existující warning `NU1603` pro `Microsoft.Extensions.Http` v `Tempo.Blazor`.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj -v:minimal` prošel: 0 warnings, 0 errors.
- Nejsou evidované žádné baseline failing testy pro tuto fázi.

### 0.2 Provider boundary inventura

- [x] Sepsat v testu nebo docs, které provider contracts už jsou public API.
- [x] Ověřit, že nové server provider contracts nemají Blazor UI dependency.
- [x] Ověřit XML dokumentaci u každého nového `[Parameter]`.

Public document editor provider contracts:

- `IDocumentEditorProvider`
- `IDocumentVersionProvider`
- `IDocumentCommentProvider`
- `IDocumentTokenValueProvider`
- `IDocumentAuditSink`
- `IDocumentOfflineStore`
- `IDocumentSyncProvider`
- `IDocumentImageProvider`
- `IDocumentImageUrlResolver`
- `IDocumentRenditionProvider`
- `IDocumentPdfExportProvider`
- `IDocumentSuggestionProvider`
- `IDocumentCollaborationProvider`
- `IDocumentFormatImporter`
- `IDocumentFormatExporter`

Boundary poznámky:

- `src/Tempo.Blazor.Abstractions/DocumentEditor` a `src/Tempo.Blazor.DocumentFormats` neobsahují přímé Blazor UI typy jako `Microsoft.AspNetCore.Components`, `IJSRuntime`, `IBrowserFile`, `ElementReference` nebo `RenderFragment`.
- Fáze 0 nepřidala žádný nový `[Parameter]`, takže XML dokumentace nových parametrů je pro tuto fázi splněná jako N/A.

## Fáze 1: Real-time collaboration přes provider boundary

### 1.1 Public editor API

- [x] **RED:** Component test očekává `CollaborationProvider` parametr na `TmDocumentEditor`.
- [x] **GREEN:** Přidat `[Parameter] public IDocumentCollaborationProvider? CollaborationProvider { get; set; }`.
- [x] **RED:** Component test očekává `CollaborationClientId` parametr.
- [x] **GREEN:** Přidat `CollaborationClientId` s defaultem generovaným při inicializaci.
- [x] **RED:** Component test očekává, že editor používá `Author` pro collaboration join.
- [x] **GREEN:** Join request naplnit z `Author`, fallback na lokální anonymní author.
- [x] **RED:** Component test ověří, že bez provideru se collaboration nepouští.
- [x] **GREEN:** Collaboration inicializovat jen při neprázdném dokumentu a provideru.

### 1.2 Join/leave životní cyklus

- [x] **RED:** Test očekává join po úspěšném načtení dokumentu.
- [x] **GREEN:** Po loadu vytvořit `DocumentCollaborationSync` a zavolat `JoinAsync`.
- [x] **RED:** Test očekává leave při dispose editoru.
- [x] **GREEN:** Zavolat `LeaveAsync` v `Dispose`.
- [x] **RED:** Test očekává leave/join při změně `DocumentId`.
- [x] **GREEN:** Při reloadu ukončit starou session a založit novou.
- [x] **RED:** Test očekává chybovou hlášku při join failure bez pádu komponenty.
- [x] **GREEN:** Ošetřit provider exception lokalizovanou zprávou.

### 1.3 Lokální změny do operation batch

- [x] **RED:** Block surface edit vytvoří collaboration batch.
- [x] **GREEN:** Při `HandleDocumentChangedAsync` porovnat předchozí a aktuální dokument přes `CreateLocalEditBatch`.
- [x] **RED:** Prázdný diff se nebroadcastuje.
- [x] **GREEN:** Batch posílat jen při `Operations.Count > 0`.
- [x] **RED:** WYSIWYG patch vytvoří collaboration batch.
- [x] **GREEN:** Po aplikaci `WysiwygPatch` vytvořit batch z before/after snapshotu.
- [x] **RED:** Remote echo ze stejné session se ignoruje.
- [x] **GREEN:** Nepouštět znovu batch, jehož `SessionId` je aktuální session.

### 1.4 Remote catch-up

- [x] **RED:** Test simuluje remote batch a očekává aktualizovaný dokument v editoru.
- [x] **GREEN:** Přidat refresh/poll loop přes `GetOperationBatchesAsync`.
- [x] **RED:** Test očekává, že remote batch obnoví WYSIWYG snapshot.
- [x] **GREEN:** Po remote apply zavolat `ApplySnapshotAsync` ve WYSIWYG hostu.
- [x] **RED:** Test očekává, že dirty lokální stav se remote změnou neztratí.
- [x] **GREEN:** Použít `DocumentCollaborationSync.ApplyRemoteBatch` bez resetu lokální dirty indikace.
- [x] **RED:** Test očekává reconnect catch-up po provider failure.
- [x] **GREEN:** Přidat jednoduchý retry/backoff a `ReconnectAsync`.

### 1.5 Presence a cursory

- [x] **RED:** Component test očekává render `TmDocumentCollaborationCursorOverlay` v editoru při remote cursorech.
- [x] **GREEN:** Vložit overlay do block surface i WYSIWYG mode shellu.
- [x] **RED:** Selection change odešle cursor přes provider.
- [x] **GREEN:** Mapovat `DocumentEditorSelectionState`/`WysiwygSelectionSnapshot` na `DocumentCollaborationCursor`.
- [x] **RED:** Remote cursor bez display name fallbackuje na client id.
- [x] **GREEN:** Zachovat existující fallback z overlay komponenty.
- [x] **RED:** Test očekává, že vlastní cursor se nezobrazuje.
- [x] **GREEN:** Filtrovat aktuální `SessionId`.

### 1.6 Demo provider a API

- [x] **RED:** API test očekává endpoint join collaboration session.
- [x] **GREEN:** Přidat demo endpoints pro join/leave.
- [x] **RED:** API test očekává endpoint pro broadcast operation batch.
- [x] **GREEN:** Přidat demo endpoint pro operation batches.
- [x] **RED:** API test očekává endpoint pro cursors.
- [x] **GREEN:** Přidat demo endpoint pro cursor update/list.
- [x] **RED:** Client provider test očekává HTTP volání.
- [x] **GREEN:** Přidat demo HTTP collaboration provider v `Demo.SharedUI`.

### 1.7 E2E collaboration

- [x] **RED:** E2E otevře dvě browser contexts nad stejným dokumentem.
- [x] **GREEN:** Demo stránka předá editoru collaboration provider.
- [x] **RED:** Text vložený v první stránce se objeví ve druhé.
- [x] **GREEN:** Doladit polling/refresh a snapshot update.
- [x] **RED:** Cursor z první stránky je viditelný ve druhé.
- [x] **GREEN:** Doladit cursor broadcast.
- [x] **RED:** Reconnect test obnoví změny po dočasném odpojení provideru.
- [x] **GREEN:** Doladit reconnect flow.

Výsledek 2026-05-13:

- `TmDocumentEditor` má `CollaborationProvider`, `CollaborationClientId` a `CollaborationSyncInterval`.
- Editor joinuje po loadu, leaveuje při reloadu/dispose, broadcastuje block i WYSIWYG změny, polluje remote batches a renderuje remote cursory.
- Demo API má collaboration endpointy a demo stránky používají HTTP-backed provider přes `DemoApi`.
- Ověření: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~Collaboration" -v:minimal`, `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --filter "FullyQualifiedName~DocumentEditorFormatEndpointTests.CollaborationEndpoints" -v:minimal`, `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor_DemoPage_SyncsCollaborativeEditAndCursor|FullyQualifiedName~DocumentEditor_DemoPage_ReconnectedClientCatchesUpCollaborativeEdit" -v:minimal`.

## Fáze 2: Suggestions / track changes přes provider

### 2.1 Editor API pro suggestions

- [x] **RED:** Component test očekává `SuggestionProvider` parametr.
- [x] **GREEN:** Přidat `[Parameter] public IDocumentSuggestionProvider? SuggestionProvider { get; set; }`.
- [x] **RED:** Component test očekává `SuggestionsEnabled` parametr.
- [x] **GREEN:** Přidat suggestion mode oddělený od legacy `TrackChangesEnabled`.
- [x] **RED:** Test očekává, že bez provideru suggestion mode nejde zapnout.
- [x] **GREEN:** UI akci disabled/hidden podle provideru a permissions.
- [x] **RED:** Test očekává načtení pending suggestions po loadu dokumentu.
- [x] **GREEN:** Volat `GetSuggestionsAsync` s `Status = Pending`.

### 2.2 Suggestion model hardening

- [x] **RED:** Model test očekává reviewer metadata po accept/reject.
- [x] **GREEN:** Doplnit `ReviewedAt` a `Reviewer` do `DocumentSuggestion`.
- [x] **RED:** Model test očekává concurrency/base hash pro suggestion.
- [x] **GREEN:** Doplnit `BaseSnapshotHash` nebo `BaseConcurrencyToken`.
- [x] **RED:** Model test očekává strukturovaný operation payload.
- [x] **GREEN:** Doplnit `Operations` nebo `OperationBatch` k `DocumentSuggestion`.
- [x] **RED:** JSON roundtrip test zachová payload.
- [x] **GREEN:** Upravit serialization kompatibilně.

### 2.3 Provider-backed create suggestion

- [x] **RED:** Block edit v suggestion mode zavolá `CreateSuggestionAsync`.
- [x] **GREEN:** Místo přímé mutace dokumentu vytvořit suggestion z before/after diffu.
- [x] **RED:** WYSIWYG text edit v suggestion mode zavolá `CreateSuggestionAsync`.
- [x] **GREEN:** Zachytit WYSIWYG patch a převést jej na suggestion operation.
- [x] **RED:** Formatting command v suggestion mode vytvoří formatting suggestion.
- [x] **GREEN:** Zachytit mark/style operations.
- [x] **RED:** Delete text v suggestion mode vytvoří delete suggestion s original text.
- [x] **GREEN:** Doplnit text extraction z range.

### 2.4 Rendering suggestions

- [x] **RED:** Inline renderer zvýrazní insert suggestion.
- [x] **GREEN:** Přidat CSS a renderer pro insert dekoraci.
- [x] **RED:** Inline renderer zvýrazní delete suggestion.
- [x] **GREEN:** Přidat CSS a renderer pro delete dekoraci.
- [x] **RED:** WYSIWYG host dostane suggestions v options/snapshotu.
- [x] **GREEN:** Rozšířit `WysiwygEditorOptions`/snapshot payload o suggestions.
- [x] **RED:** WYSIWYG DOM vykreslí suggestion marks s testid.
- [x] **GREEN:** Přidat JS render decorations.
- [x] **RED:** Accessibility test očekává čitelné ARIA popisy změn.
- [x] **GREEN:** Doplnit localized labels.

### 2.5 Suggestions panel

- [x] **RED:** Component test očekává panel s pending suggestions.
- [x] **GREEN:** Přidat `TmDocumentSuggestionPanel`.
- [x] **RED:** Panel zobrazí autora, typ změny a ukázku textu.
- [x] **GREEN:** Doplnit card/list item renderer.
- [x] **RED:** Kliknutí na suggestion fokusuje cílový block/range.
- [x] **GREEN:** Napojit selection/focus helper.
- [x] **RED:** Empty state při nule suggestions.
- [x] **GREEN:** Lokalizovaný empty state.

### 2.6 Accept/reject flow

- [x] **RED:** Accept zavolá `ReviewSuggestionAsync` se statusem Accepted.
- [x] **GREEN:** Implementovat accept handler.
- [x] **RED:** Reject zavolá `ReviewSuggestionAsync` se statusem Rejected.
- [x] **GREEN:** Implementovat reject handler.
- [x] **RED:** Accept aplikuje suggestion operation do dokumentu.
- [x] **GREEN:** Použít `DocumentOperationApplier` nebo WYSIWYG patch applier.
- [x] **RED:** Reject neaplikuje změnu do dokumentu.
- [x] **GREEN:** Jen aktualizovat stav a odstranit dekoraci.
- [x] **RED:** Accept/reject refreshne panel bez reloadu stránky.
- [x] **GREEN:** Aktualizovat lokální suggestions kolekci.
- [x] **RED:** Provider failure ukáže lokalizovanou chybu a ponechá suggestion pending.
- [x] **GREEN:** Ošetřit exception.

### 2.7 E2E suggestions

- [x] **RED:** E2E zapne suggestions mode.
- [x] **GREEN:** Přidat demo provider a UI toggle.
- [x] **RED:** E2E vloží text a vidí pending suggestion.
- [x] **GREEN:** Doladit create flow.
- [x] **RED:** E2E accept aplikuje text do dokumentu.
- [x] **GREEN:** Doladit accept/apply flow.
- [x] **RED:** E2E reject odstraní návrh bez aplikace textu.
- [x] **GREEN:** Doladit reject flow.
- [x] **RED:** E2E ověří WYSIWYG suggestion dekorace.
- [x] **GREEN:** Doladit JS rendering.

### Fáze 2 ověření

- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" -v:minimal` prošel: 382 passed, 0 failed, 0 skipped.
- `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --filter "FullyQualifiedName~DocumentEditorFormatEndpointTests" -v:minimal` prošel: 9 passed, 0 failed, 0 skipped.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj -v:minimal` prošel.
- S běžícím Demo API (`https://localhost:5100`) a WASM demem (`https://localhost:7106`) prošel e2e filtr pro `DocumentEditor_DemoPage_CreatesAndAcceptsProviderBackedSuggestion`, `DocumentEditor_DemoPage_RejectsProviderBackedSuggestionWithoutApplyingIt` a `DocumentEditor_WysiwygMode_RendersProviderBackedSuggestionDecoration`: 3 passed, 0 failed, 0 skipped.

## Fáze 3: DOCX import/export přes serverový provider

### 3.1 Reusable provider contract

- [x] **RED:** Abstractions test očekává `IDocumentFormatProvider`.
- [x] **GREEN:** Přidat provider boundary pro import/export dokumentových formátů.
- [x] **RED:** Contract test očekává import request bez Blazor typů.
- [x] **GREEN:** Přidat `DocumentFormatImportProviderRequest` s file name, content type, bytes/stream abstraction.
- [x] **RED:** Contract test očekává export request s dokumentem a formátem.
- [x] **GREEN:** Přidat `DocumentFormatExportProviderRequest`.
- [x] **RED:** Contract test očekává warnings v import/export výsledku.
- [x] **GREEN:** Přidat společné result modely nebo reuse bezpečné modely z format package.
- [x] **RED:** Contract test očekává podporu `Docx` minimálně.
- [x] **GREEN:** Přidat enum/format capability pro `Docx`.

### 3.2 Editor API

- [x] **RED:** Component test očekává `FormatProvider` parametr.
- [x] **GREEN:** Přidat `[Parameter] public IDocumentFormatProvider? FormatProvider { get; set; }`.
- [x] **RED:** Toolbar test zobrazí DOCX import, když provider podporuje import.
- [x] **GREEN:** Přidat import action.
- [x] **RED:** Toolbar test zobrazí DOCX export, když provider podporuje export.
- [x] **GREEN:** Přidat export action.
- [x] **RED:** Permissions test skryje DOCX export při `CanExport = false`.
- [x] **GREEN:** Napojit na existující permissions.
- [x] **RED:** Read-only test povolí import jen při edit permission.
- [x] **GREEN:** Napojit na `CanEditDocument`.

### 3.3 Import flow

- [x] **RED:** Component test uploadu DOCX zavolá `ImportAsync`.
- [x] **GREEN:** Přidat file input a import handler.
- [x] **RED:** Import result nahradí aktuální dokument a nastaví dirty.
- [x] **GREEN:** Převzít result document do editoru.
- [x] **RED:** Import warnings se zobrazí uživateli.
- [x] **GREEN:** Přidat warnings summary.
- [x] **RED:** Import failure zobrazí lokalizovanou chybu.
- [x] **GREEN:** Ošetřit provider exception.
- [x] **RED:** Import zapíše audit event.
- [x] **GREEN:** Použít `DocumentEditorAuditAction.Import`.

### 3.4 Export flow

- [x] **RED:** Export DOCX zavolá `ExportAsync` s aktuálním dokumentem.
- [x] **GREEN:** Implementovat export handler.
- [x] **RED:** Export result se předá do callbacku/download bridge.
- [x] **GREEN:** Přidat `OnDocumentFormatExported`.
- [x] **RED:** Export warnings se zobrazí bez ztráty downloadu.
- [x] **GREEN:** Uložit poslední export warnings.
- [x] **RED:** Export failure zobrazí lokalizovanou chybu.
- [x] **GREEN:** Ošetřit provider exception.
- [x] **RED:** Export zapíše audit event.
- [x] **GREEN:** Reuse export audit nebo rozšířit details o format.

### 3.5 Server provider implementace v demo

- [x] **RED:** Demo API test očekává provider-style DOCX import endpoint request/response.
- [x] **GREEN:** Upravit/ponechat endpointy za provider třídou.
- [x] **RED:** Demo API test očekává provider-style DOCX export endpoint.
- [x] **GREEN:** Přidat server service implementující format provider logiku.
- [x] **RED:** Client provider test očekává upload multipart nebo bytes request.
- [x] **GREEN:** Přidat `DemoDocumentFormatProvider` v SharedUI.
- [x] **RED:** Demo stránka nepoužívá přímé hardcoded export anchors pro nové flow.
- [x] **GREEN:** Předat provider do `TmDocumentEditor` nebo wrapperu.

### 3.6 E2E DOCX provider

- [x] **RED:** E2E importuje DOCX přes komponentový provider flow.
- [x] **GREEN:** Doladit import UI.
- [x] **RED:** E2E exportuje DOCX přes komponentový provider flow.
- [x] **GREEN:** Doladit download bridge.
- [x] **RED:** E2E ověří warnings UI na fixture s aproximací.
- [x] **GREEN:** Doladit warnings render.
- [x] **RED:** E2E ověří WYSIWYG edit po DOCX importu a následný export.
- [x] **GREEN:** Doladit snapshot refresh.

## Fáze 4: PDF export přes serverový provider

### 4.1 Contract doplnění

- [x] `IDocumentPdfExportProvider` existuje.
- [x] `DocumentPdfExportRequest` existuje.
- [x] `DocumentPdfExportResult` existuje.
- [x] **RED:** Contract test očekává PDF export options.
- [x] **GREEN:** Doplnit `DocumentPdfExportOptions` do requestu.
- [x] **RED:** Contract test očekává include/exclude suggestions/comments volby.
- [x] **GREEN:** Doplnit volby do options.
- [x] **RED:** Contract test očekává page setup volby.
- [x] **GREEN:** Doplnit page size, orientation, margins.

### 4.2 Download bridge

- [x] **RED:** Component test očekává, že PDF export výsledek jde stáhnout bez ručního host callbacku.
- [x] **GREEN:** Přidat JS interop download helper pro byte[]/base64 nebo URL.
- [x] **RED:** Component test ověří fallback `OnPdfExported`.
- [x] **GREEN:** Zachovat existující callback.
- [x] **RED:** Component test ověří disabled stav během exportu.
- [x] **GREEN:** Využít existující `_isExportingPdf` a toolbar state.
- [x] **RED:** Export failure nevyvolá download.
- [x] **GREEN:** Ošetřit error branch.

### 4.3 Server provider implementace v demo/API

- [x] **RED:** API test očekává `/api/document-editor/{id}/export/pdf`.
- [x] **GREEN:** Přidat endpoint.
- [x] **RED:** API test očekává `application/pdf`.
- [x] **GREEN:** Přidat demo PDF renderer/provider.
- [x] **RED:** API test očekává 404 pro neexistující dokument.
- [x] **GREEN:** Ošetřit not found.
- [x] **RED:** Client provider test očekává PDF endpoint call.
- [x] **GREEN:** Přidat `DemoDocumentPdfExportProvider`.
- [x] **RED:** Demo stránka předá provider do `TmDocumentEditor`.
- [x] **GREEN:** Wire provider přes DI.

### 4.4 E2E PDF export

- [x] **RED:** E2E klikne na PDF export a čeká download.
- [x] **GREEN:** Doladit toolbar/download.
- [x] **RED:** E2E ověří název souboru `.pdf`.
- [x] **GREEN:** Doladit filename.
- [x] **RED:** E2E ověří, že PDF export funguje po WYSIWYG editaci.
- [x] **GREEN:** Doladit aktuální snapshot v requestu.
- [x] **RED:** E2E ověří permission hide/disable scénář.
- [x] **GREEN:** Doladit demo variantu nebo test-only route.

## Fáze 5: Porovnání dokumentů mimo verze

### 5.1 Compare service/provider contract

- [x] **RED:** Abstractions test očekává `IDocumentComparisonProvider`.
- [x] **GREEN:** Přidat optional provider boundary.
- [x] **RED:** Model test očekává `DocumentCompareRequest`.
- [x] **GREEN:** Přidat request se dvěma zdroji dokumentu.
- [x] **RED:** Model test očekává zdroj typu current document.
- [x] **GREEN:** Přidat `DocumentCompareSourceKind.Current`.
- [x] **RED:** Model test očekává zdroj typu document id.
- [x] **GREEN:** Přidat `DocumentCompareSourceKind.DocumentId`.
- [x] **RED:** Model test očekává zdroj typu raw snapshot/upload.
- [x] **GREEN:** Přidat `DocumentCompareSourceKind.JsonSnapshot` nebo imported file.
- [x] **RED:** Result test očekává summary added/removed/changed.
- [x] **GREEN:** Přidat `DocumentCompareResult`.

### 5.2 Default local compare

- [x] **RED:** Service test porovná dva dokumenty bez verzí.
- [x] **GREEN:** Přidat `DocumentComparisonService`.
- [x] **RED:** Service test detekuje změněný paragraph text.
- [x] **GREEN:** Reuse `DocumentTextDiffHelper`.
- [x] **RED:** Service test detekuje přidaný block.
- [x] **GREEN:** Přidat block-level diff.
- [x] **RED:** Service test detekuje odebraný block.
- [x] **GREEN:** Přidat removed block diff.
- [x] **RED:** Service test detekuje změnu tabulky.
- [x] **GREEN:** Přidat základní table text extraction.

### 5.3 Compare UI

- [x] **RED:** Component test očekává tlačítko Compare mimo version panel.
- [x] **GREEN:** Přidat toolbar action.
- [x] **RED:** Component test otevře compare dialog.
- [x] **GREEN:** Přidat `TmDocumentCompareDialog`.
- [x] **RED:** Dialog umožní vybrat current vs document id.
- [x] **GREEN:** Přidat source picker.
- [x] **RED:** Dialog umožní nahrát/importovat porovnávaný DOCX přes format provider.
- [x] **GREEN:** Reuse `FormatProvider.ImportAsync`.
- [x] **RED:** Dialog spustí compare a zobrazí `TmDocumentDiffViewer`.
- [x] **GREEN:** Napojit result na diff viewer.
- [x] **RED:** Dialog má loading, empty a error states.
- [x] **GREEN:** Doplnit stavy a lokalizace.

### 5.4 Provider-backed compare

- [x] **RED:** Component test s `ComparisonProvider` volá provider místo local service.
- [x] **GREEN:** Přidat `[Parameter] public IDocumentComparisonProvider? ComparisonProvider { get; set; }`.
- [x] **RED:** Provider error fallbackne na local compare jen když je povoleno.
- [x] **GREEN:** Přidat option `UseLocalComparisonFallback`.
- [x] **RED:** Compare request obsahuje aktuální author/context.
- [x] **GREEN:** Doplnit metadata.
- [x] **RED:** Compare audit event se zapíše.
- [x] **GREEN:** Rozšířit audit action nebo details.

### 5.5 Demo/API compare

- [x] **RED:** API test očekává compare endpoint pro dva document ids.
- [x] **GREEN:** Přidat endpoint.
- [x] **RED:** API test očekává compare current snapshot vs stored document.
- [x] **GREEN:** Přidat request body variantu.
- [x] **RED:** Client provider test očekává compare HTTP call.
- [x] **GREEN:** Přidat `DemoDocumentComparisonProvider`.
- [x] **RED:** Demo stránka předá compare provider do editoru.
- [x] **GREEN:** Wire provider přes DI.

### 5.6 E2E compare mimo verze

- [x] **RED:** E2E otevře compare dialog mimo version panel.
- [x] **GREEN:** Doladit toolbar/dialog.
- [x] **RED:** E2E porovná current document s jiným demo document id.
- [x] **GREEN:** Doladit provider/service.
- [x] **RED:** E2E porovná current document s uploadnutým DOCX.
- [x] **GREEN:** Doladit DOCX import jako compare source.
- [x] **RED:** E2E ověří added/removed summary.
- [x] **GREEN:** Doladit summary.
- [x] **RED:** E2E zavře compare a editor zůstane editovatelný.
- [x] **GREEN:** Doladit modal lifecycle.

## Fáze 6: Cross-feature integrace

### 6.1 Permissions

- [x] **RED:** Collaboration read-only uživatel smí vidět remote cursors, ale neposílá edits.
- [x] **GREEN:** Oddělit presence od edit permission.
- [x] **RED:** Suggestions require comment/review permission podle nové volby.
- [x] **GREEN:** Doplnit `CanSuggest`/`CanReviewSuggestions` nebo mapovat na existující permissions.
- [x] **RED:** DOCX import vyžaduje edit permission.
- [x] **GREEN:** Ošetřit toolbar i handler.
- [x] **RED:** Compare je dostupné pro read permission.
- [x] **GREEN:** Ošetřit toolbar i dialog.

### 6.2 Konflikty a souběhy

- [x] **RED:** Suggestion accept nad změněným dokumentem detekuje base hash mismatch.
- [x] **GREEN:** Vrátit conflict state.
- [x] **RED:** Collaboration remote batch a pending suggestion se nekorumpují.
- [x] **GREEN:** Refresh suggestions po remote apply.
- [x] **RED:** DOCX import během collaboration session vyvolá full-document operation nebo session reset.
- [x] **GREEN:** Zvolit a implementovat bezpečný tok.
- [x] **RED:** Compare dialog pracuje nad stabilním snapshotem i během remote změn.
- [x] **GREEN:** Snapshotovat current document při otevření dialogu.

### 6.3 Accessibility a lokalizace

- [x] **RED:** bUnit test ověří ARIA labels pro collaboration status.
- [x] **GREEN:** Doplnit texty a labels.
- [x] **RED:** bUnit test ověří ARIA labels pro suggestions panel.
- [x] **GREEN:** Doplnit texty a labels.
- [x] **RED:** bUnit test ověří ARIA labels pro format import/export.
- [x] **GREEN:** Doplnit texty a labels.
- [x] **RED:** bUnit test ověří ARIA labels pro compare dialog.
- [x] **GREEN:** Doplnit texty a labels.

### 6.4 Styling

- [x] **RED:** CSS test očekává žádné hardcoded barvy v nových document editor stylech.
- [x] **GREEN:** Použít `--tm-*` tokeny.
- [x] **RED:** Screenshot/e2e ověří suggestions panel na desktopu.
- [x] **GREEN:** Doladit layout.
- [x] **RED:** Screenshot/e2e ověří compare dialog na mobilu.
- [x] **GREEN:** Doladit responsive layout.
- [x] **RED:** Cursor overlay nepřekrývá toolbar.
- [x] **GREEN:** Doladit vrstvení.

## Fáze 7: Dokumentace a examples

- [ ] Doplnit README/API docs pro `IDocumentCollaborationProvider`.
- [ ] Doplnit README/API docs pro `IDocumentSuggestionProvider`.
- [ ] Doplnit README/API docs pro serverový DOCX format provider.
- [ ] Doplnit README/API docs pro `IDocumentPdfExportProvider`.
- [ ] Doplnit README/API docs pro compare mimo verze.
- [ ] Přidat demo popis bez marketingového balastu přímo u relevantních controls.
- [ ] Aktualizovat `AGENTS.md` sekci Document Editor, pokud přibudou nové provider contracts.

## Finální ověření

- [ ] `dotnet build TempoBlazor.slnx -v:minimal`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" -v:minimal`
- [ ] `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj -v:minimal`
- [ ] `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor" -v:minimal`
- [x] `git diff --check`

## Poznámky během implementace

- Zatím nepřidávat full per-character CRDT; aktuální rozhodnutí je operation-log model s deterministickým orderingem.
- `DocumentRevision` ponechat jako lightweight/import/review model v core dokumentu.
- `DocumentSuggestion` držet mimo core snapshot, pokud konkrétní test neprokáže nutnost jiného řešení.
- Detailní navazující plán pro rich collaboration operation stream a JS-renderované remote patche je v `planning/document-editor-js-collaboration-engine-tdd-todo.md`.
- DOCX/ODT lokální format package už existuje; nová práce míří na reusable server provider boundary pro komponentu.
- PDF provider boundary už existuje; nová práce míří na demo server provider, download bridge, options a e2e.
- Compare mimo verze má reuse existujícího `TmDocumentDiffViewer`, ale nesmí být závislé na `DocumentVersion`.
