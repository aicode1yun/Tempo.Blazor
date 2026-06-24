# Tempo.Blazor.Signing split + DocumentEditor performance CI TODO

Datum zalozeni: 2026-06-24

## Cil

Opravit falesne selhavajici GitHub Actions job `TmDocumentEditor - Performance baseline`
a vyclenit podpisovou oblast do samostatneho NuGet balicku `Tempo.Blazor.Signing`.

Tento dokument je implementacni checklist. Pri implementaci se jednotlive body
odskrtavaji primo tady, aby bylo videt, co uz je hotove a co jeste zbyva.

## Vychozi stav

- [ ] CI job `TmDocumentEditor - Performance baseline` je cerveny na PR #3.
- [ ] Build v jobu prosel.
- [ ] Performance testy v jobu prosly `9/9`.
- [ ] Selhal az krok `Detect regression vs. checked-in baseline`.
- [ ] Log selhani ukazuje porovnani `perf-e2e-2026-05-26.csv` proti `perf-2026-05-26.csv`.
- [ ] Python parser spadl na `KeyError: 'scenario'`.
- [ ] Pricina je kombinace spatneho vyberu CSV souboru podle `mtime`, michani `perf-*` a `perf-e2e-*` baseline a BOM v CSV hlavicce.
- [ ] Workflow stale obsahuje stare DocumentEditor pathy pod `src/Tempo.Blazor/...`.
- [ ] Workflow stale pouziva Node.js 20.
- [ ] `Tempo.Blazor.Signing` je popsany v `JsonDocumentation/package-split-proposal.json`, ale fyzicky jeste neexistuje.
- [ ] Signing komponenty jsou stale v `src/Tempo.Blazor/Components/Signing/**`.
- [ ] Signing CSS je stale importovane z core `src/Tempo.Blazor/wwwroot/css/tempo-blazor.css`.
- [ ] `pdf-template-designer.js` je stale v core `_content/Tempo.Blazor`.
- [ ] `AddTempoBlazorSigning()` je stale v core `Tempo.Blazor`.
- [ ] Demo projekty pouzivaji Signing pres core package.

## Rozsah vycleneni

Vychozi implementacni varianta:

- [ ] Do `Tempo.Blazor.Signing` presunout `src/Tempo.Blazor/Components/Signing/**`.
- [ ] Do `Tempo.Blazor.Signing` presunout `src/Tempo.Blazor/wwwroot/js/pdf-template-designer.js`.
- [ ] Do `Tempo.Blazor.Signing` presunout signing-specific CSS soubory.
- [ ] Nechat `TmSignature`, `TmSignatureCapture` a `signature-capture.js` v core pro prvni iteraci, pokud se pri implementaci nepotvrdi, ze je lze presunout bez sirsi breaking zmeny.
- [ ] Nechat podpisove modely v `Tempo.Blazor.Abstractions`.
- [ ] Zachovat verejny namespace `Tempo.Blazor.Components.Signing`.
- [ ] Zachovat kompatibilni metabalicek `Tempo.Blazor.All`, ktery bude novy signing balicek referencovat.

Follow-up varianta po stabilizaci:

- [ ] Zvazit presun `TmSignature`, `TmSignatureCapture`, `signature-capture.js` a navazujicich typovych souboru do `Tempo.Blazor.Signing`.
- [ ] Pokud se budou presouvat, pripravit explicitni breaking-change poznamku a migracni navod.

## Faze 0 - CI hotfix pro DocumentEditor performance baseline

### 0.1 Inventura aktualniho workflow

- [x] Zkontrolovat `.github/workflows/document-editor-performance.yml`.
- [x] Potvrdit, ze trigger pathy stale ukazuji na stare core DocumentEditor umisteni.
- [x] Potvrdit, ze build step nebuildi explicitne `Tempo.Blazor.DocumentEditor`.
- [x] Potvrdit, ze `setup-node` je bud zbytecny, nebo ma byt prepnuty na Node.js 24.
- [x] Potvrdit, ze regression step vybira CSV soubory podle filesystem `mtime`.
- [x] Potvrdit, ze regression step porovnava vsechny `perf-*.csv`, vcetne `perf-e2e-*`.

### 0.2 Oprava trigger pathu

- [x] Odebrat stale pathy `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`.
- [x] Odebrat stale pathy `src/Tempo.Blazor/wwwroot/js/document-editor.js`.
- [x] Odebrat stale pathy `src/Tempo.Blazor/Components/DocumentEditor/**`.
- [x] Pridat `src/Tempo.Blazor.DocumentEditor/**`.
- [x] Ponechat `src/Tempo.Blazor.Abstractions/DocumentEditor/**`.
- [x] Ponechat `tests/Tempo.Blazor.Tests/DocumentEditor/Performance/**`.
- [x] Ponechat `planning/baselines/**`.
- [x] Ponechat `.github/workflows/document-editor-performance.yml`.

### 0.3 Oprava build kroku

- [x] Buildit `src/Tempo.Blazor.DocumentEditor/Tempo.Blazor.DocumentEditor.csproj`.
- [x] Buildit `tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj`.
- [x] Nepovysovat job na full solution build, pokud to neni nutne.

### 0.4 Node.js cleanup

- [x] Pokud workflow Node.js realne nepouziva, odebrat `actions/setup-node`.
- [x] Pokud Node.js zustane potreba, nastavit `node-version: '24'` (neni potreba; `setup-node` byl odebran).
- [ ] Overit, ze warning o Node.js 20 zmizel z dalsiho behu. Ceka na dalsi GitHub Actions run.

### 0.5 Robustni regression gate

- [x] Cist CSV s `encoding='utf-8-sig'`, aby BOM nerozbil hlavicku `scenario`.
- [x] Porovnavat jen stejnou rodinu baseline souboru.
- [x] Nenechat `perf-e2e-*` vstoupit do porovnani unit/in-process baseline.
- [x] Nenechat `perf-*` vstoupit do porovnani E2E baseline.
- [x] Nevybirat `current` a `previous` podle filesystem `mtime`.
- [x] Vybrat baseline deterministicky podle nazvu souboru nebo explicitniho manifestu.
- [x] Pokud v jobu nevznikne novy current CSV, regression porovnani preskocit s jasnym vysvetlenim.
- [x] Pokud ma byt PR gate jen informativni, nastavit hard fail jen pro manual/default branch beh.
- [x] Vytisknout do logu oba soubory, pocet scenaru a pocet porovnanych metrik.
- [x] Pri skutecne regresi vypsat konkretni scenar, metriku, predchozi hodnotu, aktualni hodnotu a procento.

### 0.6 Validace CI hotfixu

- [x] Spustit lokalne shell cast regression skriptu nad `planning/baselines`.
- [x] Spustit `dotnet build src/Tempo.Blazor.DocumentEditor/Tempo.Blazor.DocumentEditor.csproj --configuration Release`.
- [x] Spustit `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --configuration Release`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~Tempo.Blazor.Tests.DocumentEditor.Performance"`.
- [ ] Overit, ze job uz neselze na `KeyError: 'scenario'`. Lokalni regression blok overen; finalni potvrzeni ceka na dalsi GitHub Actions run.

## Faze 1 - Zalozeni projektu Tempo.Blazor.Signing

### 1.1 Projekt a metadata

- [x] Vytvorit `src/Tempo.Blazor.Signing/Tempo.Blazor.Signing.csproj`.
- [x] Pouzit `Microsoft.NET.Sdk.Razor`.
- [x] Nastavit `TargetFrameworks` na `net8.0;net9.0;net10.0`.
- [x] Nastavit `RootNamespace` na `Tempo.Blazor`.
- [x] Nastavit `IsPackable` na `true`.
- [x] Nastavit `PackageId` na `Tempo.Blazor.Signing`.
- [x] Nastavit `Title` na `Tempo.Blazor.Signing`.
- [x] Doplnit NuGet description pro signing workflow, PDF template designer a signing runner.
- [x] Doplnit `PackageTags`.
- [x] Doplnit `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`.
- [x] Doplnit `PackageReadmeFile`.
- [x] Doplnit `GenerateDocumentationFile`.
- [x] Pribalit root `README.md` jako package readme.

### 1.2 Package dependencies

- [x] Pridat TFM-specific `Microsoft.AspNetCore.Components.Web` pro net8.0.
- [x] Pridat TFM-specific `Microsoft.AspNetCore.Components.Web` pro net9.0.
- [x] Pridat TFM-specific `Microsoft.AspNetCore.Components.Web` pro net10.0.
- [x] Pridat `ProjectReference` na `Tempo.Blazor`.
- [x] Pridat `ProjectReference` na `Tempo.Blazor.Abstractions`, pokud neni transitivne dostatecne.
- [x] Nepridavat zadne nove externi dependencies, pokud je build nevyzada.

### 1.3 Test visibility

- [x] Pridat `InternalsVisibleTo` pro `Tempo.Blazor.Tests`, pokud signing kod obsahuje internal API testovane z test projektu.
- [x] Overit, zda soucasne signing testy potrebuji internal access. `SigningTextResolver` a `SigningStepListExtensions` jsou internal.

## Faze 2 - Presun komponent a kodu

### 2.1 Components/Signing

- [x] Presunout `src/Tempo.Blazor/Components/Signing/**` do `src/Tempo.Blazor.Signing/Components/Signing/**`.
- [x] Zachovat `@namespace Tempo.Blazor.Components.Signing`.
- [x] Zachovat C# namespace `Tempo.Blazor.Components.Signing`.
- [x] Overit, ze `_Imports.razor` v novem projektu obsahuje potrebne usingy.
- [x] Overit, ze komponenty maji dostupne core komponenty z `Tempo.Blazor`.
- [x] Overit, ze komponenty maji dostupne modely z `Tempo.Blazor.Abstractions`.

### 2.2 JS assety

- [x] Presunout `src/Tempo.Blazor/wwwroot/js/pdf-template-designer.js` do `src/Tempo.Blazor.Signing/wwwroot/js/pdf-template-designer.js`.
- [x] Aktualizovat dynamic import v `TmPdfTemplateDesigner.razor.cs` z `_content/Tempo.Blazor/js/pdf-template-designer.js` na `_content/Tempo.Blazor.Signing/js/pdf-template-designer.js`.
- [x] Overit, zda je potreba presunout nejake dalsi signing-specific JS.
- [x] Ponechat `signature-capture.js` v core, pokud `TmSignatureCapture` zustane v core.

### 2.3 CSS assety

- [x] Presunout `_document-page-viewer.css`.
- [x] Presunout `_document-comments.css`.
- [x] Presunout `_signing-field-overlay.css`.
- [x] Presunout `_condition-builder.css`.
- [x] Presunout `_formula-builder.css`.
- [x] Presunout `_recipient-role-editor.css`.
- [x] Presunout `_signing-field-editor-panel.css`.
- [x] Presunout `_pdf-template-designer.css`.
- [x] Presunout `_signing-step-shell.css`.
- [x] Presunout `_signing-form-runner.css`.
- [x] Presunout `_signing-completion-panel.css`.
- [x] Presunout `_submission-status-timeline.css`.
- [x] Presunout `_share-link-panel.css`.
- [x] Presunout `_pdf-signature-verification.css`.
- [x] Presunout `_audit-trail-viewer.css`.
- [x] Ponechat `_signature-capture.css` v core, pokud `TmSignatureCapture` zustane v core.
- [x] Vytvorit `src/Tempo.Blazor.Signing/wwwroot/css/tempo-blazor-signing.css`.
- [x] Do `tempo-blazor-signing.css` pridat importy presunutych signing CSS souboru.
- [x] Odebrat presunute importy z core `src/Tempo.Blazor/wwwroot/css/tempo-blazor.css`.
- [x] Regenerovat nebo nechat MSBuild regenerovat `tempo-blazor.bundled.css`.

### 2.4 Fonty a shared styling

- [x] Overit, jestli signing komponenty potrebuji font `Dancing Script`.
- [x] Pokud font pouziva jen `TmSignatureCapture`, ponechat font-face v core.
- [x] Pokud se font pouziva v presunutych signing komponentach, pridat odpovidajici font asset nebo ponechat dependency na core CSS. Neni potreba; presunute signing CSS font nepouziva.
- [x] Overit, ze signing CSS nepouziva soubory, ktere nejsou ve vyslednem static web assets manifestu.

## Faze 3 - DI a service boundary

### 3.1 Core cleanup

- [x] Odebrat `AddTempoBlazorSigning()` z `src/Tempo.Blazor/Configuration/ServiceCollectionExtensions.cs`.
- [x] Overit, ze `AddTempoBlazor()` zustava core-only.
- [x] Overit, ze core nema zadny compile-time reference na `Tempo.Blazor.Signing`.

### 3.2 Signing registration

- [x] Vytvorit `src/Tempo.Blazor.Signing/Configuration/SigningServiceCollectionExtensions.cs`.
- [x] Implementovat `AddTempoBlazorSigning(this IServiceCollection services)`.
- [x] Volat uvnitr `services.AddTempoBlazor()`.
- [x] Registrovat budouci signing-specific sluzby jen zde.
- [x] Zachovat namespace `Tempo.Blazor.Configuration`, aby migrace v Program.cs zustala jednoducha.

### 3.3 Tempo.Blazor.All

- [x] Pridat `ProjectReference` na `src/Tempo.Blazor.Signing/Tempo.Blazor.Signing.csproj`.
- [x] Overit, ze `AddTempoBlazorAll()` nadale vola `AddTempoBlazorSigning()`.
- [x] Overit, ze `Tempo.Blazor.All` builduje bez ambiguity extension method konfliktu.

## Faze 4 - Demo aplikace

### 4.1 Project references

- [x] Pridat `Tempo.Blazor.Signing` do `src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj`.
- [x] Pridat `Tempo.Blazor.Signing` do `src/Tempo.Blazor.Demo.Server/Tempo.Blazor.Demo.Server.csproj`.
- [x] Pridat `Tempo.Blazor.Signing` do `src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj`.
- [x] Pridat `Tempo.Blazor.Signing` do InteractiveAuto host projektu.
- [x] Pridat `Tempo.Blazor.Signing` do InteractiveAuto client projektu.
- [x] Zkontrolovat, zda `Tempo.ReportServer.Web` nepotrebuje signing reference. Nepotrebuje; nepouziva demo SharedUI ani signing registraci.

### 4.2 Imports

- [x] Overit `_Imports.razor` ve WASM demo.
- [x] Overit `_Imports.razor` v Server demo.
- [x] Overit `_Imports.razor` v SharedUI.
- [x] Overit `_Imports.razor` v InteractiveAuto host/client.
- [x] Zachovat `@using Tempo.Blazor.Components.Signing` tam, kde jsou signing demo page.

### 4.3 Program.cs

- [x] Overit `builder.Services.AddTempoBlazorSigning()` ve WASM demo.
- [x] Overit `builder.Services.AddTempoBlazorSigning()` v Server demo.
- [x] Overit `builder.Services.AddTempoBlazorSigning()` v InteractiveAuto host/client.
- [x] Pokud extension metoda uz neni dostupna transitivne, doplnit odpovidajici using. Neni potreba; zustava `Tempo.Blazor.Configuration`.

### 4.4 Static asset links

- [x] Pridat signing CSS link do WASM `wwwroot/index.html`.
- [x] Pridat signing CSS link do Server `_Host.cshtml` nebo host layoutu.
- [x] Pridat signing CSS link do InteractiveAuto `App.razor`.
- [x] Aktualizovat script path pro `pdf-template-designer.js`, pokud je explicitne linkovany. Neni explicitne linkovany; pouziva dynamic import z komponenty.
- [x] Nepresouvat `signature-capture.js` path, pokud `TmSignatureCapture` zustava v core.
- [x] Overit, ze Signing demo page renderuje bez 404 na static assets.

### 4.5 Demo smoke

- [x] Spustit WASM demo.
- [x] Otevrit signing components page.
- [x] Overit `TmPdfTemplateDesigner`.
- [x] Overit `TmSigningFormRunner`.
- [x] Overit `TmDocumentPageViewer`.
- [x] Overit `TmAuditTrailViewer`.
- [x] Overit, ze podpisovy capture stale funguje pres core script.
- [x] Spustit Server demo.
- [x] Otevrit signing components page.
- [x] Spustit InteractiveAuto demo, pokud nebude casove prilis drahe.

## Faze 5 - Testy

### 5.1 Test project references

- [x] Pridat `Tempo.Blazor.Signing` do `tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj`.
- [x] Overit, ze testy nenacitaji signing komponenty omylem z core.
- [x] Overit, ze `Tempo.Blazor.All` testy stale kompiluji.

### 5.2 Existing signing tests

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Components.Signing"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~SigningLocalizationTests"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSignatureCapture"`, pokud capture zustava v core.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSignature"`, pokud capture zustava v core.
- [x] Spustit relevantni DocumentEditor signing bridge testy.

### 5.3 DI smoke tests

- [x] Pridat nebo aktualizovat test pro `AddTempoBlazorSigning()`.
- [x] Overit, ze `AddTempoBlazorSigning()` registruje core sluzby.
- [x] Overit, ze `AddTempoBlazorAll()` kompiluje a vola signing registration.
- [x] Overit, ze core `AddTempoBlazor()` nema zavislost na signing balicku.

### 5.4 Asset and packaging tests

- [x] Pridat smoke test nebo build overeni static web assets pro `Tempo.Blazor.Signing`.
- [x] Overit, ze `tempo-blazor-signing.css` je soucasti package.
- [x] Overit, ze `pdf-template-designer.js` je soucasti package.
- [x] Overit, ze core package uz neobsahuje presunute signing CSS/JS.

## Faze 6 - JSON dokumentace

### 6.1 Package registry

- [x] Pridat `Tempo.Blazor.Signing` do `JsonDocumentation/packages.json`.
- [x] Nastavit `sourceProject` na `src/Tempo.Blazor.Signing/Tempo.Blazor.Signing.csproj`.
- [x] Nastavit `outputFile` na `tempo-blazor-signing.json`.
- [x] Nastavit `documentationRoots` na `Packages/Tempo.Blazor.Signing/items`.
- [x] Nastavit `componentRoots` na `src/Tempo.Blazor.Signing/Components`.
- [x] Nastavit `includePublicTypes` na `true`.
- [x] Nastavit `includeAssets` na `true`.

### 6.2 Presun dokumentacnich itemu

- [x] Presunout `JsonDocumentation/Components/Signing/**` pod `JsonDocumentation/Packages/Tempo.Blazor.Signing/items/Components/Signing/**` nebo zvolenou existujici package strukturu. Pouzita struktura `items/Signing` pro komponenty.
- [x] Presunout signing helper typy z `JsonDocumentation/Components/Components/*Signing*`, pokud patri k nove package.
- [x] Ponechat `JsonDocumentation/Abstractions/Models/Signing*` v Abstractions dokumentaci.
- [x] Ponechat `JsonDocumentation/Abstractions/Document-Editor/*Signing*` v Abstractions/DocumentEditor dokumentaci.
- [x] Aktualizovat `sourcePath` v presunutych JSON souborech.
- [x] Aktualizovat asset pathy z `_content/Tempo.Blazor/...` na `_content/Tempo.Blazor.Signing/...`.

### 6.3 Generator a vystupy

- [x] Spustit JSON documentation generator.
- [x] Overit, ze vznikl `tempo-blazor-signing.json`.
- [x] Overit, ze aggregate `tempo-blazor-all.json` stale obsahuje signing dokumentaci.
- [x] Overit, ze core `tempo-blazor.json` uz neobsahuje presunute signing komponenty.
- [x] Overit, ze `JsonDocumentation/package-split-proposal.json` status pro Signing odpovida realite.

### 6.4 Getting started a examples

- [x] Aktualizovat globalni `JsonDocumentation/gettingStarted.json`.
- [x] Doplnit signing CSS link do ukazky.
- [x] Doplnit signing package install do ukazky.
- [x] Doplnit signing script path pro `pdf-template-designer.js`.
- [x] Zachovat core `signature-capture.js` v ukazce, pokud `TmSignatureCapture` zustava v core.

## Faze 7 - NuGet, solution a CI publishing

### 7.1 Solution

- [x] Pridat `src/Tempo.Blazor.Signing/Tempo.Blazor.Signing.csproj` do `TempoBlazor.slnx`.
- [x] Overit, ze solution restore najde novy projekt. `dotnet restore TempoBlazor.slnx` prosel.
- [x] Overit, ze solution build zahrne novy projekt. Debug i Release solution build zahrnul `Tempo.Blazor.Signing`.

### 7.2 CI package manifest

- [x] Pridat `src/Tempo.Blazor.Signing/Tempo.Blazor.Signing.csproj` do `eng/nuget-packages.txt`.
- [x] Spustit `bash eng/verify-nuget-package-manifest.sh`.
- [x] Overit, ze manifest vypisuje o jeden package vic. Manifest hlasi `22` packages.
- [x] Overit, ze publishing workflow automaticky packuje novy package pres manifest.

### 7.3 Pack

- [x] Spustit `dotnet pack src/Tempo.Blazor.Signing/Tempo.Blazor.Signing.csproj -c Release -o ./packages`.
- [x] Spustit `bash eng/pack-nuget-packages.sh` podle potreby. Spusteno pres `PACKAGE_OUTPUT=/tmp/tempo-blazor-phase7-packages`.
- [x] Overit, ze vznikne `Tempo.Blazor.Signing.*.nupkg`.
- [x] Overit, ze package neobsahuje test JS soubory ani nepotrebne artefakty.
- [x] Overit, ze package obsahuje README.
- [x] Overit, ze package obsahuje static web assets.
- [x] Overit, ze package obsahuje XML docs.

### 7.4 GitHub workflows

- [x] Overit `.github/workflows/publish-nuget.yml`.
- [x] Overit `.github/workflows/publish-nuget-org.yml`.
- [x] Overit, ze oba workflow pouzivaji `eng/nuget-packages.txt` pres `eng/pack-nuget-packages.sh`.
- [x] Overit, ze neni potreba hardcoded seznam package id.
- [x] Overit artifact name a output path. Oba workflow uploaduji `./packages/*.nupkg` jako `nuget-packages`.

## Faze 8 - README a migracni dokumentace

### 8.1 README

- [x] Doplnit `Tempo.Blazor.Signing` do seznamu NuGet package.
- [x] Doplnit instalacni priklad.
- [x] Doplnit CSS link.
- [x] Doplnit JS asset path pro `pdf-template-designer.js`.
- [x] Doplnit `builder.Services.AddTempoBlazorSigning()`.
- [x] Popsat, ze `Tempo.Blazor.All` obsahuje signing automaticky.

### 8.2 Migration notes

- [x] Popsat migraci z core na `Tempo.Blazor.Signing`.
- [x] Popsat zmenu `_content/Tempo.Blazor/...` na `_content/Tempo.Blazor.Signing/...`.
- [x] Popsat, co zustava v core v prvni iteraci.
- [x] Popsat pripadny breaking change pro namespace nebo assety, pokud vznikne.

### 8.3 AGENTS nebo interní docs

- [x] Aktualizovat popis NuGet packages v `AGENTS.md`, pokud se soubor ma menit.
- [x] Aktualizovat popis JavaScript interopu, pokud se asset presune.
- [x] Aktualizovat dokumentaci dem, pokud uvadi stare pathy. Demo hosty uz uvadi nove signing CSS pathy; zadna dalsi demo dokumentace se starou cestou nezustala.

## Faze 9 - Full validation

### 9.1 Restore a build

- [x] Spustit `dotnet restore`.
- [x] Spustit `dotnet build TempoBlazor.slnx`. Prosel s `0 Warning(s), 0 Error(s)`.
- [x] Spustit `dotnet build TempoBlazor.slnx -c Release`. Prosel s `0 Warning(s), 0 Error(s)`.

### 9.2 Testy

- [x] Spustit targeted signing testy. `708/708` proslo.
- [x] Spustit targeted DocumentEditor performance testy. `9/9` proslo.
- [x] Spustit targeted DocumentEditor signing bridge testy. `27/27` proslo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj`. `6903/6903` proslo.
- [x] Spustit dalsi test projekty, pokud build nebo zmeny zasahnou sdilene API. Vsechny non-E2E test projekty v Release prosly.

### 9.3 Demo

- [x] Spustit WASM demo. Spusteno pres E2E self-host na `https://localhost:7106`.
- [x] Spustit Server demo. Spusteno manualne na `https://localhost:7107`.
- [x] Overit signing demo stranky v browseru. WASM signing E2E `25/25`; Server headless Chromium render obsahoval `signing-document-viewer`, `pdf-template-designer` a `signing-runner-demo`.
- [x] Overit console bez 404 pro signing assety. Signing CSS a JS assety vratily `200` na WASM i Server; server Chromium log bez realnych 404/network erroru.
- [x] Overit console bez JS module import erroru. WASM PDF designer E2E prosly; server Chromium log bez module import erroru.

### 9.4 Package verification

- [x] Spustit manifest verification.
- [x] Spustit pack skript. `VERSION=1.0.0-phase9 PACKAGE_OUTPUT=/tmp/tempo-blazor-phase9-packages bash eng/pack-nuget-packages.sh`.
- [x] Overit pocet `.nupkg`. Vzniklo `22` balicku.
- [x] Overit, ze `Tempo.Blazor.Signing` je v artifact outputu. `/tmp/tempo-blazor-phase9-packages/Tempo.Blazor.Signing.1.0.0-phase9.nupkg`.

## Faze 10 - Commit a push

- [ ] Zkontrolovat `git status --short`.
- [ ] Zkontrolovat diff pro CI hotfix.
- [ ] Zkontrolovat diff pro Signing split.
- [ ] Zkontrolovat, ze nejsou pridane generovane `bin/obj` artefakty.
- [ ] Zkontrolovat, ze nejsou nechtene zmeny v unrelated souborech.
- [ ] Udelat commit s popisem CI fixu a Signing splitu.
- [ ] Pushnout na aktualni branch.
- [ ] Zkontrolovat GitHub Actions po pushi.

## Rizika a rozhodnuti

- [ ] Rozhodnout, jestli PR performance baseline ma byt hard gate nebo soft informational gate.
- [ ] Rozhodnout, jestli `TmSignatureCapture` zustava v core jen docasne nebo dlouhodobe.
- [ ] Rozhodnout, jestli signing CSS ma mit vlastni bundle task, nebo staci import-only soubor.
- [ ] Rozhodnout, jestli se lokalizacni resx klice nechaji v core.
- [ ] Rozhodnout, jestli bude nekdy potreba `Tempo.Blazor.Signing.Abstractions`.
- [ ] Rozhodnout, zda `Tempo.Blazor.DocumentEditor` ma mit volitelnou vazbu na signing package, nebo zustane jen pres Abstractions modely.

## Definition of done

- [ ] GitHub performance baseline job uz nepada na `KeyError: 'scenario'`.
- [ ] Workflow sleduje aktualni DocumentEditor split pathy.
- [ ] Node.js 20 warning je vyreseny nebo zduvodnene ponechany.
- [ ] Existuje NuGet package projekt `Tempo.Blazor.Signing`.
- [ ] Signing komponenty se builduji z noveho projektu.
- [ ] Demo aplikace pouzivaji novy signing package.
- [ ] JSON dokumentace generuje samostatny `tempo-blazor-signing.json`.
- [ ] NuGet CI manifest obsahuje `Tempo.Blazor.Signing`.
- [ ] `Tempo.Blazor.All` zahrnuje `Tempo.Blazor.Signing`.
- [ ] Targeted testy prosly.
- [ ] Release pack vytvari `Tempo.Blazor.Signing.*.nupkg`.
- [ ] README nebo migracni dokumentace rika, jak novy package pouzit.
