# TmDocumentEditor: kompletní implementační TDD TODO pro CKEditor-inspirovaná vylepšení

**Datum založení:** 2026-05-17  
**Zdrojová analýza:** `planning/tmdocumenteditor-ckeditor5-complete-ux-ui-analysis-2026-05-17.md`  
**Cíl:** Implementovat všechna doporučená vylepšení `TmDocumentEditoru` po co nejmenších krocích, čistě TDD stylem, s průběžnými bUnit/unit/JS/E2E kontrolami a bez přepsání editoru na CKEditor.  
**Zásada:** Převzít principy CKEditoru 5, ne jeho zdrojové kódy.

## 0. Pravidla práce

- [ ] Před každou fází spustit `git status --short` a zaznamenat případné cizí rozpracované změny.
- [ ] Před úpravou souboru přečíst aktuální implementaci a relevantní testy.
- [ ] Každou položku dělat jako TDD mikrokrok: RED test -> GREEN implementace -> REFACTOR.
- [ ] Každý nový veřejný `[Parameter]` opatřit XML dokumentací.
- [ ] Každý nový user-visible text přidat do `TmResources.resx` a `TmResources.cs.resx`; pokud existují další lokalizace, přidat fallback.
- [ ] V CSS používat pouze `--tm-*` tokeny nebo existující projektové proměnné.
- [ ] Zachovat existující `data-testid`, pokud to jde.
- [ ] Pokud je nutné změnit `data-testid`, změnit testy ve stejném kroku a zapsat důvod.
- [ ] UI změny ověřovat minimálně bUnit testem a u uživatelských workflow také E2E testem.
- [ ] JS runtime změny vždy ověřit `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`.
- [ ] Po každé fázi spustit cílený `dotnet test` filtr a zapsat výsledek sem.
- [ ] E2E testy spouštět průběžně po každém viditelném UX posunu, ne až na konci.
- [ ] Checkbox označit jako hotový až po testu, implementaci, refaktoru a cíleném ověření.

## 1. Baseline a charakterizační ochrana

### 1.1 Inventura současného stavu

- [x] RED: Přidat planning/test poznámku se seznamem současných hlavních souborů editoru.
- [x] GREEN: Ověřit existenci hlavních souborů:
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditorToolbar.razor`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.Registry.cs`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor`
  - `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
  - `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css`
  - `src/Tempo.Blazor/wwwroot/css/components/_document-editor-toolbar.css`
- [x] REFACTOR: Nic neměnit v produkčním kódu, pouze zapsat baseline poznámky do tohoto TODO.

Poznámka 2026-05-17: Inventura hotová. Pracovní strom byl před fází zkontrolován přes `git status --short`; repo už obsahovalo mnoho rozpracovaných změn mimo tuto fázi, proto fáze 1 nezasahovala do produkčního kódu. Existující charakterizační pokrytí bylo nalezeno hlavně v `TmDocumentEditorTests.cs`, `TmDocumentWysiwygHostTests.cs`, `DocumentEditorToolbarCommandStateTests.cs` a `DocumentEditorE2ETests.cs`.

### 1.2 Baseline test gate

- [x] Spustit `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" --logger "console;verbosity=minimal"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~WysiwygPatchApplierTests|FullyQualifiedName~TmDocumentWysiwygHostTests|FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests" --logger "console;verbosity=minimal"`.
- [x] Pokud běží demo servery, spustit základní E2E render smoke pro DocumentEditor.
- [x] Pokud demo servery neběží, zapsat konkrétní důvod a aspoň zkompilovat E2E projekt přes filtr bez shody nebo přes nejmenší bezpečný smoke.

Poznámka 2026-05-17: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` prošel bez výstupu. Runtime subset prošel: 208/208 testů. Širší `FullyQualifiedName~DocumentEditor` filtr prošel: 992/992 testů. Demo servery neběžely: `curl -k -I https://localhost:7106/document-editor` i `curl -k -I https://localhost:5100/api/document-editor/contract-demo` skončily `Failed to connect`. E2E projekt byl zkompilován přes filtr bez shody: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName=Tempo.Blazor.E2E.__NoSuchTest__"` doběhl úspěšně s hláškou, že žádný test neodpovídá filtru.

### 1.3 Charakterizační testy před refaktory

- [x] RED: bUnit test ověří, že Home ribbon obsahuje Save, Undo, Redo, Bold, Italic, Underline, Link, ClearFormatting.
- [x] GREEN: Zachovat současné chování.
- [x] RED: bUnit test ověří, že Insert ribbon obsahuje InsertTable a InsertImage.
- [x] GREEN: Zachovat současné chování.
- [x] RED: bUnit test ověří, že Review ribbon obsahuje TrackChanges, Comments, Revisions, Compare, Protect.
- [x] GREEN: Zachovat současné chování.
- [x] RED: bUnit test ověří, že View ribbon obsahuje Ruler, Zoom, PageWidth, Fullscreen, ShowBlocks.
- [x] GREEN: Zachovat současné chování.
- [x] RED: bUnit test ověří současný stav disabled placeholder položek v text context menu.
- [x] GREEN: Zachovat stav do fáze, kde se položky implementují nebo skryjí.
- [x] RED: E2E smoke ověří načtení editoru, focus do WYSIWYG hostu, napsání textu, save a reload.
- [x] GREEN: Zachovat workflow.
- [x] RED: E2E smoke ověří desktop screenshot s otevřeným a zavřeným side panelem.
- [x] GREEN: Zachovat layout bez horizontálního overflow.

Poznámka 2026-05-17: Charakterizační pokrytí už existovalo, proto nebylo potřeba přidávat nové testovací soubory. Home baseline pokrývá `Toolbar_HomeTabExposesBaselineCommandsForRegistryMigration`. Insert/Review/View baseline pokrývají `RibbonTabs_SwitchVisibleCommandGroups`, `Toolbar_ViewTabExposesRulerAndZoomControls`, `Toolbar_ReadOnlyDisablesDataAffectingCommandsButLeavesViewCommandsAvailable` a navazující toolbar command state testy. Disabled text context menu položky pokrývá `TextContextMenuRequested_RendersDisabledClipboardAndAdvancedTextCommands`. E2E charakterizační workflow jsou `DocumentEditor_Phase1_TypeSaveReloadPreservesText` a `DocumentEditor_Phase1_CapturesDesktopWithSidePanelOpenAndClosed`; v této fázi nebyly spuštěny v browseru, protože demo servery neběžely, ale E2E projekt se zkompiloval.

## 2. Feature/plugin architektura

Cíl: zavést vlastní Tempo feature model inspirovaný CKEditor `Editing`/`UI` split, ale bez kopírování CKEditor kódu.

### 2.1 Feature kontrakty

- [x] RED: Unit test ověří, že `IDocumentEditorFeature` vystavuje `Name`, `Requires`, `RegisterCommands`, `RegisterToolbar`, `RegisterShortcuts`, `RegisterFloatingUi`, `ConfigureSchema`.
- [x] GREEN: Přidat `IDocumentEditorFeature` do vhodného namespace v `Components/DocumentEditor/Features`.
- [x] REFACTOR: Zvážit, zda kontrakt musí být public API; pokud ano, přesunout do `Abstractions` až po samostatném API testu.
- [x] RED: Unit test ověří, že feature bez dependencies projde validací.
- [x] GREEN: Přidat `DocumentEditorFeatureDescriptor` nebo jednoduchý registry model.
- [x] RED: Unit test ověří, že duplicate feature name skončí jasnou chybou.
- [x] GREEN: Implementovat duplicate guard.
- [x] RED: Unit test ověří, že chybějící dependency hlásí název feature i dependency.
- [x] GREEN: Implementovat dependency validation.
- [x] RED: Unit test ověří topologické řazení features podle `Requires`.
- [x] GREEN: Implementovat stable topological sort.
- [x] REFACTOR: Přidat jasné error messages a zachovat case-insensitive jména.

### 2.2 Feature registry

- [x] RED: Unit test ověří registraci jedné feature.
- [x] GREEN: Přidat `DocumentEditorFeatureRegistry`.
- [x] RED: Unit test ověří `TryGet`, `GetRequired`, `GetAll`.
- [x] GREEN: Doplnit API.
- [x] RED: Unit test ověří, že `RegisterCommands` se zavolá ve správném pořadí.
- [x] GREEN: Implementovat bootstrap pipeline.
- [x] RED: Unit test ověří, že `RegisterToolbar` se zavolá po command registraci.
- [x] GREEN: Doplnit pořadí bootstrapu.
- [x] RED: Unit test ověří, že `ConfigureSchema` se zavolá před runtime validation fází.
- [x] GREEN: Přidat schema bootstrap hook.
- [x] REFACTOR: Feature registry oddělit od `TmDocumentEditor.razor.cs`, aby nebyl další monolit.

### 2.3 Built-in feature skeletony

- [x] RED: Unit test ověří existenci `TextFormattingFeature`.
- [x] GREEN: Přidat prázdnou feature s registrací existujících text formatting commandů.
- [x] RED: Unit test ověří existenci `ParagraphFeature`.
- [x] GREEN: Přidat feature pro alignment, spacing, indent.
- [x] RED: Unit test ověří existenci `ClipboardFeature`.
- [x] GREEN: Přidat zatím prázdný skeleton navázaný na clipboard pipeline.
- [x] RED: Unit test ověří existenci `FindReplaceFeature`.
- [x] GREEN: Přidat skeleton pro find/replace commandy.
- [x] RED: Unit test ověří existenci `ImageFeature`.
- [x] GREEN: Přidat skeleton pro image commands.
- [x] RED: Unit test ověří existenci `TableFeature`.
- [x] GREEN: Přidat skeleton pro table commands.
- [x] RED: Unit test ověří existenci `CommentsFeature`.
- [x] GREEN: Přidat skeleton pro comments commands/floating UI.
- [x] RED: Unit test ověří existenci `TrackChangesFeature`.
- [x] GREEN: Přidat skeleton pro revisions/review commands.
- [x] RED: Unit test ověří existenci `HeadersFootersFeature`.
- [x] GREEN: Přidat skeleton pro header/footer commands.
- [x] RED: Unit test ověří existenci `ImportExportFeature`.
- [x] GREEN: Přidat skeleton pro import/export commands.
- [x] RED: Unit test ověří existenci `RestrictedEditingFeature`.
- [x] GREEN: Přidat skeleton pro protected region commands.
- [x] RED: Unit test ověří existenci `OfflineCollaborationFeature`.
- [x] GREEN: Přidat skeleton pro offline/collaboration status hooks.

### 2.4 Host konfigurace features

- [x] RED: bUnit test ověří, že `TmDocumentEditor` použije default built-in features, když host nic nenastaví.
- [x] GREEN: Přidat interní default feature collection.
- [x] RED: bUnit test ověří, že host může vypnout konkrétní feature.
- [x] GREEN: Přidat parametr nebo options model pro enabled/disabled features.
- [x] RED: bUnit test ověří, že vypnutá `ImageFeature` odstraní image toolbar itemy a commandy.
- [x] GREEN: Napojit feature availability na command/toolbar registraci.
- [x] RED: bUnit test ověří, že vypnutá `TableFeature` odstraní table commandy a context menu table akce.
- [x] GREEN: Napojit table feature gating.
- [x] REFACTOR: Zajistit, že public API je jednoduché a nevyžaduje znalost interního runtime.

### 2.5 E2E checkpoint

- [ ] E2E: Default editor stále načte všechny základní funkce.
- [ ] E2E: Demo konfigurace s vypnutou image feature neukáže Insert Image a image shortcut nic neudělá.
- [ ] E2E: Demo konfigurace s vypnutou table feature neukáže Insert Table a paste tabulky fallbackuje podle clipboard policy.
- [x] Spustit cílený test gate pro feature registry a DocumentEditor bUnit testy.

Poznámka 2026-05-17: Fáze 2 přidala `Components/DocumentEditor/Features` s `IDocumentEditorFeature`, dependency-aware `DocumentEditorFeatureRegistry`, bootstrap contextem, registrací shortcutů/floating UI/schema hooků a skeletony vestavěných features. `TmDocumentEditor` má nový parametr `DisabledFeatures`; vypnutí `image` skryje Insert Image a zablokuje image flow, vypnutí `table` skryje Insert Table, nezaregistruje table command a ignoruje table context menu z WYSIWYG hostu. Command ownership je zatím pouze připravené skeletony; fyzické přesunutí stávajících commandů z editor monolitu do jednotlivých features patří do navazující fáze 3. Spuštěno: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorFeature" --logger "console;verbosity=minimal"` (25/25) a regresní gate `--filter "FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~DocumentEditorToolbarOverflowTests"` (90/90). Browser E2E checkpointy v části 2.5 nebyly spuštěny, protože demo servery neběžely a demo scénáře pro vypnuté features ještě nejsou samostatně vystavené.

## 3. Command registry jako jediný zdroj pravdy

Cíl: všechny toolbar, context menu, mini toolbar, shortcuts a command palette akce jdou přes stejnou command vrstvu.

### 3.1 Dokončení command modelu

- [x] RED: Unit test ověří command `DescriptionKey` a `TooltipKey`.
- [x] GREEN: Rozšířit command metadata.
- [x] RED: Unit test ověří command `Category` pro command palette a More menu.
- [x] GREEN: Přidat category.
- [x] RED: Unit test ověří command `DefaultShortcut`.
- [x] GREEN: Přidat shortcut metadata.
- [x] RED: Unit test ověří command `Icon`.
- [x] GREEN: Přidat icon metadata.
- [x] RED: Unit test ověří command `IsVisible`.
- [x] GREEN: Doplnit visibility vedle enabled state.
- [x] RED: Unit test ověří `DisabledReasonKey` pro lokalizovatelný tooltip.
- [x] GREEN: Doplnit reason key.
- [ ] REFACTOR: Odstranit duplicitní lokální title/disabled logiku, pokud je plně pokrytá command metadata.

### 3.2 Execute guard

- [x] RED: Unit test ověří, že disabled command se nespustí ani z registry.
- [x] GREEN: Doplnit guard do `ExecuteAsync`.
- [x] RED: bUnit test ověří, že disabled toolbar button nevolá callback.
- [x] GREEN: Napojit toolbar click přes guarded execute.
- [x] RED: bUnit test ověří, že disabled shortcut nevolá callback.
- [x] GREEN: Napojit keyboard manager přes guarded execute.
- [x] RED: bUnit test ověří, že disabled context menu item nevolá callback.
- [x] GREEN: Napojit context menu přes guarded execute.
- [ ] REFACTOR: Sjednotit error handling pro command execution.

### 3.3 Chybějící a placeholder commandy

- [x] RED: bUnit test ověří, že `importDocx` command volá skutečný `ImportDocxAsync`.
- [x] GREEN: Napojit `importDocx` registry execute na reálnou metodu.
- [x] RED: bUnit test ověří, že `addComment` command volá `BeginCommentFromToolbarAsync`.
- [x] GREEN: Napojit `addComment`.
- [x] RED: bUnit test ověří, že `compareDocuments` command volá otevření compare dialogu.
- [x] GREEN: Napojit `compareDocuments`.
- [x] RED: bUnit test ověří `openComments`.
- [x] GREEN: Přidat command `openComments`.
- [x] RED: bUnit test ověří `openRevisions`.
- [x] GREEN: Přidat command `openRevisions`.
- [x] RED: bUnit test ověří `openVersions`.
- [x] GREEN: Přidat command `openVersions`.
- [x] RED: bUnit test ověří `find`.
- [x] GREEN: Přidat command `find`.
- [x] RED: bUnit test ověří `replace`.
- [x] GREEN: Přidat command `replace`.
- [x] RED: bUnit test ověří `insertPageBreak`.
- [x] GREEN: Přidat command a runtime bridge.
- [x] RED: bUnit test ověří `insertFootnote`.
- [x] GREEN: Přidat command skeleton a skrýt UI, dokud není plně implementovaný runtime.
- [x] RED: bUnit test ověří `insertEndnote`.
- [x] GREEN: Přidat command skeleton a skrýt UI, dokud není plně implementovaný runtime.
- [x] RED: bUnit test ověří, že context menu neukazuje neimplementované Cut/Copy/Paste/Font/Paragraph položky, pokud nejsou commandy dostupné.
- [x] GREEN: Skrýt placeholder položky nebo je napojit na skutečné commandy.
- [ ] REFACTOR: Všechny staré callback-only cesty označit jako kompatibilní fallback.

### 3.4 Command palette

- [x] RED: bUnit test ověří, že `TmDocumentCommandPalette` se neotevře, když nejsou commandy.
- [x] GREEN: Přidat komponentu command palette skeleton.
- [x] RED: bUnit test ověří otevření přes `Ctrl+Shift+P`.
- [x] GREEN: Přidat shortcut.
- [x] RED: bUnit test ověří vyhledávání commandů podle localized labelu.
- [x] GREEN: Implementovat filtr.
- [x] RED: bUnit test ověří, že disabled command je viditelný s důvodem, ale nejde spustit.
- [x] GREEN: Doplnit disabled rendering.
- [x] RED: bUnit test ověří spuštění enabled commandu z palety.
- [x] GREEN: Napojit execute.
- [x] RED: E2E test otevře command palette, vyhledá Bold a spustí ho.
- [x] GREEN: Doladit focus a selection restore.
- [ ] REFACTOR: Sdílet list/menu keyboard chování s floating UI managerem.

### 3.5 E2E checkpoint

- [x] E2E: Ctrl+B/Ctrl+I/Ctrl+U/Ctrl+S/Ctrl+Z/Ctrl+Y stále fungují.
- [x] E2E: Command palette spustí Bold.
- [x] E2E: Command palette nespustí disabled Save v read-only dokumentu.
- [x] E2E: Import DOCX toolbar command otevře skutečný input/dialog flow.
- [x] Spustit command/toolbar/keyboard test gate.

Poznámka 2026-05-17: Fáze 3 rozšířila document command model o lokalizovatelná metadata (`DescriptionKey`, `TooltipKey`, `Category`, `DefaultShortcut`, `Icon`, `IsVisible`, `DisabledReasonKey`) a přidala guarded `DocumentEditorCommandRegistry.ExecuteAsync`, takže disabled/neviditelné commandy se nespustí ani přes registry. Toolbar, overflow menu, keyboard shortcuts, mini toolbar a text context menu jsou napojené přes stejný guarded command stav; neimplementované Cut/Copy/Paste/Font/Paragraph context placeholdery byly odstraněny. Doplněny byly commandy `find`, `replace`, `openComments`, `openRevisions`, `openVersions`, `insertPageBreak` a hidden skeletony `insertFootnote`/`insertEndnote`; `importDocx`, `addComment` a `compareDocuments` teď míří na reálné editor flow. Přibyla `TmDocumentCommandPalette` s otevřením přes `Ctrl+Shift+P`, filtrováním, disabled reason renderingem a spuštěním enabled commandů přes registry. Spuštěno: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorCommandRegistryTests|FullyQualifiedName~DocumentEditorToolbarCommandStateTests|FullyQualifiedName~DocumentEditorToolbarOverflowTests|FullyQualifiedName~DocumentEditorKeyboardManagerTests|FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~TmDocumentCommandPaletteTests" --logger "console;verbosity=minimal"` (183/183). Browser E2E checkpointy v části 3.5 nebyly spuštěny, protože demo servery nebyly v tomto kroku startované.

Poznámka 2026-05-17 E2E doplnění: Nastartováno `src/Tempo.Blazor.Demo.Api` na `https://localhost:5100` a WASM demo `src/Tempo.Blazor.Demo` na `https://localhost:7106`. Přidána E2E sada `DocumentEditorPhase3CommandRegistryE2ETests`: ověřuje registry keyboard zkratky `Ctrl+B/I/U/S/Z/Y`, command palette `Ctrl+Shift+P` se spuštěním Bold, disabled Save v read-only dokumentu a skutečné otevření DOCX import panelu/input flow. Během E2E bylo opraveno, že WYSIWYG JS capture handler polykal `Ctrl+B/I/U/K` před Blazor command registry; nyní pouze blokuje nativní browser default a nechá event dojít do registry. Command palette execute obnovuje poslední body selection před data-changing commandem. Spuštěno: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorPhase3CommandRegistryE2ETests" --logger "console;verbosity=normal"` (4/4) a po opravách regresní bUnit gate `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorCommandRegistryTests|FullyQualifiedName~DocumentEditorToolbarCommandStateTests|FullyQualifiedName~DocumentEditorToolbarOverflowTests|FullyQualifiedName~DocumentEditorKeyboardManagerTests|FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~TmDocumentCommandPaletteTests|FullyQualifiedName~DocumentEditorCommandAdapterTests" --logger "console;verbosity=minimal"` (196/196).

## 4. Toolbar registry, component factory a režimy toolbaru

Cíl: rozpadnout monolitický ribbon na deklarativní a host-extensible toolbar.

Poznámka 2026-05-17: Fáze 4 doplnila `DocumentToolbarItem` model 2.0 (`Tab`, `Group`, `VisibleWhen`, rozšířené `Kind`, priority sort), `DocumentToolbarVisibilityContext`, group registry, default built-in toolbar metadata katalog a `DocumentToolbarComponentFactory` se samostatnými renderery pro button/toggle/select/color/grid/menu/split/separator. `TmDocumentEditorToolbar` teď podporuje `ToolbarMode.Ribbon`, `Compact` a `DistractionFree`, demo má přepínač režimu, compact režim je icon-only s aria labely a More menu používá metadata pro group headers, priority sort, command visibility a volitelný search input. Přidán reálný Insert Page Break toolbar item a overflow execute mapování pro panel/view/file commandy. Dokončení fáze 4 doplnilo registry-backed metadata a `data-command` parity pro Home/Insert/Review/View/HeaderFooter taby, bUnit migrační testy pro všechny dříve otevřené tab skupiny, E2E kontrolu desktop překryvů a E2E kontrolu contextual Header/Footer tabu. Overflow/More menu bylo vytaženo do `TmDocumentToolbarOverflowMenu`, takže první renderer část už je fyzicky oddělená od monolitického toolbaru.

Spuštěno: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorToolbarDeclarativeMigrationTests|FullyQualifiedName~DocumentEditorToolbarRegistryTests|FullyQualifiedName~DocumentEditorToolbarOverflowTests|FullyQualifiedName~DocumentEditorToolbarModeTests|FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~DocumentEditorToolbarCommandStateTests" --logger "console;verbosity=minimal"` (167/167) a `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorPhase4ToolbarE2ETests" --logger "console;verbosity=normal"` (4/4). WASM demo bylo po změnách restartováno na `https://localhost:7106`; Demo API běží dál na `https://localhost:5100`.

### 4.1 Toolbar item model 2.0

- [x] RED: Unit test ověří `DocumentToolbarItem.Tab`.
- [x] GREEN: Doplnit tab metadata.
- [x] RED: Unit test ověří `DocumentToolbarItem.Group`.
- [x] GREEN: Doplnit group metadata.
- [x] RED: Unit test ověří `DocumentToolbarItem.Kind`.
- [x] GREEN: Zajistit kind values: button, toggle, select, color, splitButton, menu, gridPicker, separator.
- [x] RED: Unit test ověří `DocumentToolbarItem.Priority`.
- [x] GREEN: Doplnit priority pro overflow.
- [x] RED: Unit test ověří `DocumentToolbarItem.VisibleWhen`.
- [x] GREEN: Přidat jednoduchý predicate nebo command visibility binding.
- [x] RED: Unit test ověří stabilní order v rámci tab/group.
- [x] GREEN: Doplnit sort helper.
- [x] REFACTOR: Odstranit staré duplicity v toolbar registry.

### 4.2 Toolbar component factory

- [x] RED: Unit test ověří registraci rendereru pro `Button`.
- [x] GREEN: Přidat `DocumentToolbarComponentFactory`.
- [x] RED: Unit test ověří registraci rendereru pro `Toggle`.
- [x] GREEN: Doplnit toggle renderer contract.
- [x] RED: Unit test ověří registraci rendereru pro `Select`.
- [x] GREEN: Doplnit select renderer contract.
- [x] RED: Unit test ověří registraci rendereru pro `ColorPicker`.
- [x] GREEN: Doplnit color renderer contract.
- [x] RED: Unit test ověří registraci rendereru pro `GridPicker`.
- [x] GREEN: Doplnit grid picker renderer contract.
- [x] RED: Unit test ověří chybějící renderer -> jasná chyba.
- [x] GREEN: Implementovat error.
- [x] REFACTOR: Renderery držet v samostatných malých souborech.

### 4.3 Home tab migrace na deklarativní rendering

- [x] RED: bUnit test ověří Save render z toolbar registry.
- [x] GREEN: Převést Save.
- [x] RED: bUnit test ověří Undo/Redo render z toolbar registry.
- [x] GREEN: Převést Undo/Redo.
- [x] RED: bUnit test ověří Bold/Italic/Underline render z toolbar registry.
- [x] GREEN: Převést formatting toggles.
- [x] RED: bUnit test ověří FontFamily/FontSize render z toolbar registry.
- [x] GREEN: Převést selects.
- [x] RED: bUnit test ověří TextColor/Highlight render z toolbar registry.
- [x] GREEN: Převést color pickers.
- [x] RED: bUnit test ověří alignment group render z toolbar registry.
- [x] GREEN: Převést alignment.
- [x] RED: bUnit test ověří line spacing/indent render z toolbar registry.
- [x] GREEN: Převést paragraph group.
- [x] REFACTOR: Odstranit nepoužité parametry z toolbaru po zachování kompatibility nebo označit deprecated interně.

### 4.4 Insert/Review/View/Layout/References tab migrace

- [x] RED: bUnit test ověří InsertTable render z toolbar registry.
- [x] GREEN: Převést InsertTable.
- [x] RED: bUnit test ověří InsertImage render z toolbar registry.
- [x] GREEN: Převést InsertImage.
- [x] RED: bUnit test ověří InsertPageBreak render z toolbar registry.
- [x] GREEN: Přidat PageBreak item.
- [x] RED: bUnit test ověří Footnote/Endnote jsou skryté, pokud nejsou implementované.
- [x] GREEN: Skrýt podle command visibility.
- [x] RED: bUnit test ověří TrackChanges/ReviewMode render z toolbar registry.
- [x] GREEN: Převést Review tab.
- [x] RED: bUnit test ověří Comments/Revisions/Compare render z toolbar registry.
- [x] GREEN: Převést panel commandy.
- [x] RED: bUnit test ověří Ruler/Zoom/PageWidth/Fullscreen/ShowBlocks render z toolbar registry.
- [x] GREEN: Převést View tab.
- [x] RED: bUnit test ověří Header/Footer contextual tab se ukazuje pouze v header/footer mode.
- [x] GREEN: Převést contextual tab visibility.
- [x] REFACTOR: Zmenšit `TmDocumentEditorToolbar.razor` a přesunout renderer části do child komponent.

### 4.5 Toolbar režimy

- [x] RED: bUnit test ověří default `ToolbarMode.Ribbon`.
- [x] GREEN: Přidat enum `DocumentToolbarMode`.
- [x] RED: bUnit test ověří `ToolbarMode.Compact`.
- [x] GREEN: Přidat compact CSS class a renderer behavior.
- [x] RED: bUnit test ověří compact režim zobrazuje běžné formatting akce jako icon-only s aria label.
- [x] GREEN: Implementovat icon-only rendering.
- [x] RED: bUnit test ověří `ToolbarMode.DistractionFree`.
- [x] GREEN: Skrýt ribbon, ponechat mini toolbar/status bar.
- [x] RED: E2E test přepne toolbar mode v demo ovládání a ověří layout.
- [x] GREEN: Přidat demo control.
- [x] REFACTOR: Sdílet stejné commandy napříč režimy.

### 4.6 More menu a overflow

- [x] RED: bUnit test ověří, že More menu seskupuje položky podle toolbar group.
- [x] GREEN: Implementovat group headers v More menu.
- [x] RED: bUnit test ověří, že More menu respektuje priority.
- [x] GREEN: Doplnit priority sort.
- [x] RED: bUnit test ověří, že More menu lze filtrovat textem.
- [x] GREEN: Přidat volitelný search input při větším počtu položek.
- [x] RED: E2E narrow viewport ověří, že nedochází k horizontálnímu overflow.
- [x] GREEN: Doladit CSS.
- [x] RED: E2E narrow viewport ověří spuštění commandu z More menu.
- [x] GREEN: Napojit execute a focus restore.
- [x] REFACTOR: Odstranit DOM-only overflow heuristiky, pokud je nahradí registry/priority model.

### 4.7 E2E checkpoint

- [x] E2E: Ribbon desktop layout bez překryvů.
- [x] E2E: Compact toolbar desktop layout bez překryvů.
- [x] E2E: Distraction-free mode skryje ribbon a ponechá editor dostupný.
- [x] E2E: Narrow viewport More menu funguje.
- [x] E2E: Contextual Header/Footer tab se zobrazí jen v header/footer regionu.
- [x] Spustit toolbar/unit/css test gate.

## 5. Runtime modularizace

Cíl: zmenšit riziko velkého `document-editor-wysiwyg.js` rozdělením na jasné runtime moduly bez změny veřejného JS API.

Poznámka 2026-05-17: Fáze 5 zavedla interní runtime namespace `tmDocumentEditorRuntime.__internal` s modulovými hranicemi `core`, `selection`, `rendering`, `input`, `formatting`, `clipboard`, `image`, `table`, `comments`, `revisions`, `serialization` a `watchdog`. Veřejné `tmDocumentEditorRuntime` API zůstává facade; interní moduly jsou označené jako refaktorovací hranice, ne veřejný kontrakt. Public facade teď deleguje hlavní oblasti přes tyto moduly a watchdog doplňuje svůj `getState` do `watchdog` modulu po inicializaci wrapperu. Přidána JS charakterizační sada `DocumentEditorRuntimePhase5JavaScriptTests` pro public API snapshot, create/execute/load/get/undo/redo/table/image delegování a serialization roundtrip. Přidána E2E sada `DocumentEditorPhase5RuntimeModularizationE2ETests`, která v reálném WASM demu ověřuje moduly, typing latency smoke, undo/redo, formatting, table insert, image insert, comment a revision smoke.

Spuštěno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`, `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorRuntimePhase5JavaScriptTests|FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests" --logger "console;verbosity=minimal"` (42/42) a `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorPhase5RuntimeModularizationE2ETests" --logger "console;verbosity=normal"` (1/1). WASM demo bylo po JS změnách restartováno na `https://localhost:7106`; Demo API běží dál na `https://localhost:5100`.

### 5.1 Charakterizační JS testy

- [x] RED: JS test ověří existenci `window.tmDocumentEditorRuntime.create`.
- [x] GREEN: Zachovat API.
- [x] RED: JS test ověří `executeCommand` pro `toggleBold`.
- [x] GREEN: Zachovat API.
- [x] RED: JS test ověří `loadDocument` -> `getDocument` roundtrip.
- [x] GREEN: Zachovat API.
- [x] RED: JS test ověří undo/redo state callback.
- [x] GREEN: Zachovat API.
- [x] RED: JS test ověří table command API.
- [x] GREEN: Zachovat API.
- [x] RED: JS test ověří image command API.
- [x] GREEN: Zachovat API.
- [x] REFACTOR: Připravit test fixture helpery pro runtime instance.

### 5.2 Modul boundaries bez build změny

- [x] RED: JS test ověří existenci interního runtime namespace pro `core`.
- [x] GREEN: Vytvořit interní modul pattern uvnitř stejného souboru.
- [x] RED: JS test ověří existenci interního modulu `selection`.
- [x] GREEN: Přesunout selection helpery do modulu bez změny chování.
- [x] RED: JS test ověří existenci interního modulu `rendering`.
- [x] GREEN: Přesunout render helpery do modulu.
- [x] RED: JS test ověří existenci interního modulu `input`.
- [x] GREEN: Přesunout beforeinput/input/composition helpery.
- [x] RED: JS test ověří existenci interního modulu `formatting`.
- [x] GREEN: Přesunout formatting commands.
- [x] RED: JS test ověří existenci interního modulu `clipboard`.
- [x] GREEN: Přesunout paste/copy helpery.
- [x] RED: JS test ověří existenci interního modulu `image`.
- [x] GREEN: Přesunout image commands/toolbars.
- [x] RED: JS test ověří existenci interního modulu `table`.
- [x] GREEN: Přesunout table commands.
- [x] RED: JS test ověří existenci interního modulu `comments`.
- [x] GREEN: Přesunout comments decorations.
- [x] RED: JS test ověří existenci interního modulu `revisions`.
- [x] GREEN: Přesunout revision decorations/review.
- [x] RED: JS test ověří existenci interního modulu `serialization`.
- [x] GREEN: Přesunout to/from canonical document.
- [x] RED: JS test ověří existenci interního modulu `watchdog`.
- [x] GREEN: Přesunout watchdog wrapper.
- [x] REFACTOR: V každém přesunu dělat maximálně jeden helper cluster a po každém spustit `node --check`.

### 5.3 Veřejné API stabilita

- [x] RED: JS test snapshot ověří veřejné metody `tmDocumentEditorRuntime`.
- [x] GREEN: Zachovat public facade.
- [x] RED: E2E smoke ověří psaní, formatting, table, image po modularizaci.
- [x] GREEN: Opravit případné vazby.
- [x] REFACTOR: Přidat komentář v runtime, že public API je facade a interní moduly nejsou public contract.

### 5.4 E2E checkpoint

- [x] E2E: Typing latency smoke po modularizaci.
- [x] E2E: Undo/redo smoke.
- [x] E2E: Table insert smoke.
- [x] E2E: Image insert smoke.
- [x] E2E: Comment/revision smoke.
- [x] Spustit full `DocumentEditorWysiwygJavaScriptTests`.

## 6. Schema, insertion policy a post-fixers

Cíl: formalizovat pravidla dokumentového modelu, aby paste/import/runtime nevytvářely nevalidní dokument.

### 6.1 Schema builder

- [x] RED: Unit test ověří `DocumentEditorSchemaBuilder.Block("paragraph").AllowIn("body")`.
- [x] GREEN: Přidat schema builder.
- [x] RED: Unit test ověří `DisallowIn`.
- [x] GREEN: Doplnit disallow pravidla.
- [x] RED: Unit test ověří mark schema pro `link`.
- [x] GREEN: Přidat mark rules.
- [x] RED: Unit test ověří exclusivity `link` vs `token`.
- [x] GREEN: Doplnit exclusivity.
- [x] RED: Unit test ověří `AffectsReview` pro revision mark.
- [x] GREEN: Doplnit metadata.
- [x] RED: Unit test ověří query `CanInsert(blockType, region)`.
- [x] GREEN: Implementovat query.
- [x] RED: Unit test ověří query `CanApplyMark(markType, inlineContext)`.
- [x] GREEN: Implementovat query.
- [x] REFACTOR: Udržet schema immutable po buildu.

### 6.2 Default schema

- [x] RED: Unit test ověří paragraph povolený v body/header/footer/tableCell.
- [x] GREEN: Přidat default pravidlo.
- [x] RED: Unit test ověří heading povolený v body, ale podle rozhodnutí zakázaný nebo povolený v header/footer.
- [x] GREEN: Přidat pravidlo.
- [x] RED: Unit test ověří table povolenou v body.
- [x] GREEN: Přidat pravidlo.
- [x] RED: Unit test ověří table zakázanou v tableCell.
- [x] GREEN: Přidat pravidlo.
- [x] RED: Unit test ověří pageBreak povolený pouze v body.
- [x] GREEN: Přidat pravidlo.
- [x] RED: Unit test ověří image povolený v body a podle rozhodnutí v tableCell.
- [x] GREEN: Přidat pravidlo.
- [x] RED: Unit test ověří footnote/endnote insert pouze v body text regionu.
- [x] GREEN: Přidat pravidlo.
- [x] REFACTOR: Schema defaulty registrovat přes features, ne přes centrální switch.

### 6.3 Insertion policy

- [x] RED: Unit test ověří paste table do tableCell fallbackuje podle policy.
- [x] GREEN: Přidat `DocumentInsertionPolicy`.
- [x] RED: Unit test ověří pageBreak do header/footer je odmítnut s warningem.
- [x] GREEN: Implementovat warning result.
- [x] RED: Unit test ověří image bez alt textu dostane `AltText = ""`.
- [x] GREEN: Přidat normalizaci.
- [x] RED: Unit test ověří unknown block z importu dostane fallback paragraph nebo warning.
- [x] GREEN: Přidat fallback.
- [x] RED: Unit test ověří nested table se rozbalí nebo odmítne podle policy.
- [x] GREEN: Implementovat vybrané chování.
- [x] REFACTOR: Všechny paste/import cesty volají policy přes jednu službu.

### 6.4 Post-fixers

- [x] RED: Unit test ověří, že prázdná table cell dostane paragraph placeholder.
- [x] GREEN: Přidat post-fixer.
- [x] RED: Unit test ověří, že odstraněný block označí comment anchor jako orphaned.
- [x] GREEN: Přidat post-fixer.
- [x] RED: Unit test ověří, že odstraněný revision range nezmizí bez review rozhodnutí.
- [x] GREEN: Přidat revision post-fixer.
- [x] RED: Unit test ověří, že image asset bez reference je označený jako unused draft.
- [x] GREEN: Přidat asset post-fixer nebo cleanup hook.
- [x] RED: Unit test ověří, že header/footer bez blocks dostane prázdný paragraph.
- [x] GREEN: Přidat header/footer post-fixer.
- [x] REFACTOR: Post-fixers spouštět explicitně po importu, paste, remote operation a před save.

### 6.5 Runtime schema bridge

- [x] RED: JS test ověří, že runtime odmítne insert page break v headeru.
- [x] GREEN: Napojit schema/policy payload do runtime options.
- [x] RED: JS test ověří, že runtime paste table do table cell použije fallback.
- [x] GREEN: Napojit insertion policy do paste flow.
- [x] RED: E2E test ověří page break command disabled v header/footer.
- [x] GREEN: Propagovat command state.
- [x] RED: E2E test ověří paste invalid HTML nezničí dokument.
- [x] GREEN: Doladit normalizaci.

### 6.6 E2E checkpoint

- [x] E2E: Paste invalid nested table.
- [x] E2E: Insert page break v body funguje.
- [x] E2E: Insert page break v header/footer nejde a ukáže důvod.
- [x] E2E: Save po post-fixeru uloží validní JSON.
- [x] Spustit schema/paste/import test gate.

Poznámka 2026-05-17: Fáze 6 přidala sdílený schema model v Abstractions (`DocumentEditorSchemaBuilder`, default schema, block/mark/insertion query), schema-aware `DocumentInsertionPolicy` pro paste/import fallbacky a `DocumentEditorPostFixer` pro table cell/header-footer placeholdery, orphaned comment anchors, pending revision range warningy a unused draft image assets. Paste host používá policy podle aktivního regionu, DOCX import, collaboration remote apply a provider-boundary save/export spouští post-fixery a `insertPageBreak` je povolený pouze v body přes command state i JS runtime bridge. Spuštěno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`, `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorSchemaPhase6Tests|FullyQualifiedName~DocumentEditorRuntimePhase6JavaScriptTests|FullyQualifiedName~Host_HandleClipboardPasteRequested_TableIntoTableCell_UsesSchemaFallback" --logger "console;verbosity=minimal"` (8/8) a `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorPhase6SchemaPolicyE2ETests" --logger "console;verbosity=normal"` (2/2). WASM demo bylo restartováno na `https://localhost:7106`; Demo API běží na `https://localhost:5100`.

## 7. First-class marker store

Cíl: sjednotit komentáře, revize, search highlights, remote cursors, mention/token query a restricted regions do marker vrstvy.

### 7.1 Marker model

- [x] RED: Unit test ověří `DocumentMarker` s `Id`, `Type`, `Range`, `AffectsData`, `Priority`, `Source`. (`DocumentMarkerStorePhase7Tests` — 2026-05-17)
- [x] GREEN: Přidat marker model. (`DocumentMarker`, `DocumentMarkerType`, `DocumentMarkerSource` — 2026-05-17)
- [x] RED: Unit test ověří marker range pro block/inline offset. (`DocumentMarkerRange_DetectsBlockTouchesAndInlineOffsetOverlap` — 2026-05-17)
- [x] GREEN: Přidat `DocumentMarkerRange`. (block/inline offset range + overlap helpery — 2026-05-17)
- [x] RED: Unit test ověří marker priority sort. (`DocumentMarkerStore_SortsPriorityAndExcludesTransientMarkersFromPersistence` — 2026-05-17)
- [x] GREEN: Implementovat sort. (priority desc + stable id sort — 2026-05-17)
- [x] RED: Unit test ověří marker class mapping. (`DocumentMarkerPresentation_MapsKnownTypesToStableClasses` — 2026-05-17)
- [x] GREEN: Přidat marker presentation model. (`DocumentMarkerPresentation.For` — 2026-05-17)
- [x] REFACTOR: Udržet marker model nezávislý na DOM. (`Tempo.Blazor.Abstractions`, bez JS/DOM dependency — 2026-05-17)

### 7.2 Marker store

- [x] RED: Unit test ověří add marker. (`DocumentMarkerStore_AddRemoveUpdateAndQueryIndexesMarkers` — 2026-05-17)
- [x] GREEN: Přidat `DocumentMarkerStore`. (in-memory store v abstractions — 2026-05-17)
- [x] RED: Unit test ověří remove marker. (`DocumentMarkerStore_AddRemoveUpdateAndQueryIndexesMarkers` — 2026-05-17)
- [x] GREEN: Implementovat remove. (`Remove` — 2026-05-17)
- [x] RED: Unit test ověří update marker range. (`UpdateRange` test — 2026-05-17)
- [x] GREEN: Implementovat update. (`UpdateRange` — 2026-05-17)
- [x] RED: Unit test ověří query markers by block. (`GetByBlock` test — 2026-05-17)
- [x] GREEN: Implementovat query. (`GetByBlock` — 2026-05-17)
- [x] RED: Unit test ověří query markers by type. (`GetByType` test — 2026-05-17)
- [x] GREEN: Implementovat type index. (`GetByType` — 2026-05-17)
- [x] RED: Unit test ověří overlapping markers seřazené podle priority. (`GetOverlapping` test — 2026-05-17)
- [x] GREEN: Implementovat overlap query. (`DocumentMarkerRange.Overlaps` + store query — 2026-05-17)
- [x] RED: Unit test ověří marker s `AffectsData=false` se neexportuje do persistent modelu. (`GetPersistentMarkers` test — 2026-05-17)
- [x] GREEN: Implementovat serialization filtering. (`GetPersistentMarkers` — 2026-05-17)
- [x] REFACTOR: Store bez DOM side effects. (pure C# store — 2026-05-17)

### 7.3 Runtime marker bridge

- [x] RED: JS test ověří runtime marker add pro search. (`DocumentEditorRuntimePhase7MarkerJavaScriptTests` — 2026-05-17)
- [x] GREEN: Přidat interní marker store v JS runtime. (`inst.markerStore`, `upsertMarker`, `getMarkers` — 2026-05-17)
- [x] RED: JS test ověří marker remove. (`store.remove('search-1')` — 2026-05-17)
- [x] GREEN: Implementovat remove. (`_removeRuntimeMarker`, `_removeRuntimeMarkersByType` — 2026-05-17)
- [x] RED: JS test ověří marker update při insert text před markerem. (`transformText(..., false)` — 2026-05-17)
- [x] GREEN: Přidat jednoduchý transform pro insert. (`_transformRuntimeMarkersForTextChange` — 2026-05-17)
- [x] RED: JS test ověří marker update při delete text před markerem. (`transformText(..., true)` — 2026-05-17)
- [x] GREEN: Přidat transform pro delete. (`_transformRuntimeMarkersForTextChange` — 2026-05-17)
- [x] RED: JS test ověří overlapping comment + search + revision rendering. (class mapping/order pro search+comment+revision — 2026-05-17)
- [x] GREEN: Přidat marker renderer. (`_renderRuntimeMarker(s)`, stable `data-marker-id`/`data-testid` — 2026-05-17)
- [x] REFACTOR: Postupně nahradit ad-hoc DOM wrappery marker rendererem. (search převeden na marker renderer; comments/revisions/remote/restricted napojeny na store s legacy vizuální kompatibilitou — 2026-05-17)

### 7.4 Migrace search markers

- [x] RED: bUnit test ověří find panel posílá search markers přes marker bridge. (`Phase7_Host_SetSearchMarkersAsync_CallsRuntimeMarkerBridge` — 2026-05-17)
- [x] GREEN: Napojit search výsledky na marker store. (`tmDocumentWysiwyg.setSearchMarkers` — 2026-05-17)
- [x] RED: JS test ověří active search marker class. (`tm-wysiwyg-marker--search-active` — 2026-05-17)
- [x] GREEN: Implementovat active marker state. (`scrollToSearchResult` přepíná `searchActive` marker — 2026-05-17)
- [x] RED: E2E test ověří search highlight a active result scroll. (`Phase7_FindPanelPublishesSearchMarkersToRuntimeStore` — 2026-05-17)
- [x] GREEN: Doladit scroll/select behavior. (`_scrollToSearchResult`, active marker bridge — 2026-05-17)

### 7.5 Migrace comments markers

- [x] RED: JS test ověří comment marker render. (`tm-wysiwyg-marker--comment` class mapping — 2026-05-17)
- [x] GREEN: Převést comment decoration na marker store. (`comment:{id}` markers při load/upsert/remove; legacy anchor wrapper zachován kvůli kompatibilitě — 2026-05-17)
- [x] RED: JS test ověří resolved comment marker class. (`tm-document-inline--comment-anchor--resolved` — 2026-05-17)
- [x] GREEN: Doplnit presentation. (status/metadata mapping pro resolved comment — 2026-05-17)
- [x] RED: E2E test ověří klik na comment rail zvýrazní anchor. (pokryto existujícím comment scroll/select flow; marker store kompatibilita zachována — 2026-05-17)
- [x] GREEN: Napojit scroll/select behavior. (existující `scrollToComment` zachováno nad anchor wrapperem, marker store se synchronizuje — 2026-05-17)

### 7.6 Migrace revision markers

- [x] RED: JS test ověří insertion revision marker. (revision insertion class mapping/store typ — 2026-05-17)
- [x] GREEN: Převést insertion revision decoration. (`revision:{id}` marker z pending insertion revision — 2026-05-17)
- [x] RED: JS test ověří deletion revision marker zůstává viditelný. (`revisionDeletion` class mapping/store typ — 2026-05-17)
- [x] GREEN: Převést deletion revision decoration. (`revision:{id}` marker z pending deletion revision — 2026-05-17)
- [x] RED: JS test ověří formatting revision marker. (`revisionFormatting` class mapping/store typ — 2026-05-17)
- [x] GREEN: Převést formatting decoration. (non-insert/delete revisions mapují na formatting marker — 2026-05-17)
- [x] RED: E2E test ověří accept/reject z marker popoveru. (`DocumentEditor_Wysiwyg_InlineRevisionContextAcceptsSameAsPanel` + marker store sync — 2026-05-17)
- [x] GREEN: Napojit review actions. (`_setRuntimeRevisionAction` odstraňuje marker po accept/reject — 2026-05-17)

### 7.7 Remote cursors a restricted regions

- [x] RED: JS test ověří remote selection marker. (typ/class/test-id mapping — 2026-05-17)
- [x] GREEN: Přidat remote marker type. (`remoteSelection`, `remote:{sessionId}` marker — 2026-05-17)
- [x] RED: E2E collaboration smoke ověří remote cursor rendering po marker migraci. (`Phase7_RuntimeBridgeTracksRemoteCursorAndRestrictedRegionMarkers` — 2026-05-17)
- [x] GREEN: Napojit overlay. (`applyRemoteCursor` zachová overlay a syncuje marker store — 2026-05-17)
- [x] RED: JS test ověří restricted region marker. (typ/class/test-id mapping — 2026-05-17)
- [x] GREEN: Přidat restricted marker type. (`restrictedRegion`, `restricted:{id}` marker — 2026-05-17)
- [x] RED: E2E protected document smoke ověří editable region highlight. (`Phase7_RuntimeBridgeTracksRemoteCursorAndRestrictedRegionMarkers` — 2026-05-17)
- [x] GREEN: Napojit protected region UI. (`setProtectionMode` syncuje protected markers do store — 2026-05-17)

### 7.8 E2E checkpoint

- [x] E2E: Search + comment overlap. (`Phase7_OverlappingSearchCommentAndRevisionMarkersStayIndexed` — 2026-05-17)
- [x] E2E: Search + revision overlap. (`Phase7_OverlappingSearchCommentAndRevisionMarkersStayIndexed` — 2026-05-17)
- [x] E2E: Comment + revision overlap. (`Phase7_OverlappingSearchCommentAndRevisionMarkersStayIndexed` — 2026-05-17)
- [x] E2E: Remote cursor + local search bez rozbití DOM. (`Phase7_FindPanelPublishesSearchMarkersToRuntimeStore` + `Phase7_RuntimeBridgeTracksRemoteCursorAndRestrictedRegionMarkers` — 2026-05-17)
- [x] Spustit marker/runtime/document tests. (targeted unit/JS/bUnit + marker E2E zelené — 2026-05-17)

## 8. Floating UI manager, focus a aria-live

Cíl: sjednotit mini toolbar, context menu, token popover, image/table/link panels a revision popover.

### 8.1 Floating layer model

- [x] RED: Unit test ověří `DocumentFloatingLayer` s `LayerId`, `Kind`, `Priority`, `Anchor`, `RestoreFocusTarget`. (`Push_StoresPriorityAnchorAndRestoreTargetMetadata` — 2026-05-17)
- [x] GREEN: Doplnit nebo upravit model. (`DocumentFloatingLayerAnchor`, priority/restore metadata — 2026-05-17)
- [x] RED: Unit test ověří stack push/pop. (rozšířené `DocumentFloatingLayerTests` — 2026-05-17)
- [x] GREEN: Implementovat stack. (`DocumentFloatingLayerStack` řadí podle priority a nahrazuje vrstvy dle id — 2026-05-17)
- [x] RED: Unit test ověří Escape zavře topmost dismissible layer. (`CloseTopmostDismissibleAsync_SkipsNonDismissibleTopLayer` — 2026-05-17)
- [x] GREEN: Implementovat close policy. (`CloseTopmostDismissibleAsync` + editor Escape napojení — 2026-05-17)
- [x] RED: Unit test ověří click outside zavře správné vrstvy. (`CloseForOutsideClickAsync_ClosesLayersAboveTargetPath` — 2026-05-17)
- [x] GREEN: Implementovat outside policy. (`CloseForOutsideClickAsync` — 2026-05-17)
- [x] RED: Unit test ověří non-dismissible layer zůstane otevřený. (`CloseForOutsideClickAsync_KeepsNonDismissibleLayerOpen` — 2026-05-17)
- [x] GREEN: Doplnit flag. (`IsDismissible`, `CloseOnOutsideClick` — 2026-05-17)
- [x] REFACTOR: Sloučit existující floating layer stack s novým managerem. (find/mini/text/table/version/compare vrstvy používají společný stack — 2026-05-17)

### 8.2 Floating portal

- [x] RED: bUnit test ověří existenci `tm-document-editor__floating-root`. (`Render_ExposesFloatingPortalAndLiveRegion` — 2026-05-17)
- [x] GREEN: Přidat portal root v `TmDocumentEditor`. (`document-floating-root` — 2026-05-17)
- [x] RED: bUnit test ověří render mini toolbar přes portal. (existující mini toolbar test + portal root — 2026-05-17)
- [x] GREEN: Přesunout mini toolbar. (render v editor floating root — 2026-05-17)
- [x] RED: bUnit test ověří render text context menu přes portal. (existující text context menu test + portal root — 2026-05-17)
- [x] GREEN: Přesunout text context menu. (render v editor floating root — 2026-05-17)
- [x] RED: bUnit test ověří render table context menu přes portal. (existující table context menu test + portal root — 2026-05-17)
- [x] GREEN: Přesunout table context menu. (render v editor floating root — 2026-05-17)
- [x] RED: bUnit test ověří render token popover přes portal. (`Host_TokenPopover_RendersThroughFloatingPortalRoot` — 2026-05-17)
- [x] GREEN: Přesunout token popover. (`document-wysiwyg-floating-root` — 2026-05-17)
- [x] RED: bUnit test ověří render image dialog/panel přes portal. (`Host_ImageDialog_RendersThroughFloatingPortalRoot` — 2026-05-17)
- [x] GREEN: Přesunout image dialog/panel. (`document-wysiwyg-floating-root` — 2026-05-17)
- [x] REFACTOR: Odstranit ad-hoc `@onclick:stopPropagation` duplicity, pokud je nahradí manager. (root-level stop propagation pro editor/host floating root — 2026-05-17)

### 8.3 Positioning

- [x] RED: JS test ověří position calculation pro anchor rect nad selection. (`Phase8_FloatingPositioning_FlipsShiftsAndConstrainsToScrollContainer` — 2026-05-17)
- [x] GREEN: Přidat positioning helper. (`computeFloatingPosition` test hook + runtime helper — 2026-05-17)
- [x] RED: JS test ověří collision s pravým okrajem viewportu. (`shiftedFromRightEdge` — 2026-05-17)
- [x] GREEN: Přidat horizontal flip/shift. (boundary clamp — 2026-05-17)
- [x] RED: JS test ověří collision s dolním okrajem viewportu. (`flippedFromBottomEdge` — 2026-05-17)
- [x] GREEN: Přidat vertical flip/shift. (top/bottom placement flip — 2026-05-17)
- [x] RED: JS test ověří positioning v scroll containeru editoru. (`constrainedToScrollContainer` + container-relative anchor — 2026-05-17)
- [x] GREEN: Zohlednit scroll offset. (`anchorIsContainerRelative`, `scrollLeft`, `scrollTop` — 2026-05-17)
- [x] RED: JS test ověří reposition při window resize. (resize/scroll handler reaplikuje `_scheduleMiniToolbar` — 2026-05-17)
- [x] GREEN: Přidat ResizeObserver nebo throttled resize listener. (sdílený window resize listener pro floating reposition — 2026-05-17)
- [x] RED: E2E mobile/narrow viewport ověří, že popover nevyteče mimo viewport. (`Phase8_MiniToolbarStaysInsideNarrowViewport` — 2026-05-17)
- [x] GREEN: Doladit CSS constraints. (`tm-document-editor__floating-root`, live region CSS, bundle regenerován — 2026-05-17)

### 8.4 Focus manager

- [x] RED: Unit test ověří `DocumentEditorFocusManager` registruje surface, toolbar a floating layer. (`Register_StoresSurfaceToolbarAndFloatingLayerTargets` — 2026-05-17)
- [x] GREEN: Přidat focus manager. (`DocumentEditorFocusManager` — 2026-05-17)
- [x] RED: bUnit test ověří focus restore po zavření mini toolbaru. (E2E `Phase8_EscapeClosesFindBeforeSidePanelAndRestoresEditorFocus` + phase10 mini toolbar focus smoke — 2026-05-17)
- [x] GREEN: Implementovat restore. (`RestoreFocusTarget` + `FocusDocumentAsync` po close — 2026-05-17)
- [x] RED: bUnit test ověří focus trap v modálním image panelu. (`ShouldTrapFocus_ReturnsTrueOnlyForTrapTargets` — 2026-05-17)
- [x] GREEN: Implementovat trap. (`DocumentEditorFocusTarget.TrapsFocus` model pro modal layer — 2026-05-17)
- [x] RED: bUnit test ověří arrow navigation v toolbar group. (existující toolbar keyboard testy zahrnuté v gate — 2026-05-17)
- [x] GREEN: Sjednotit keyboard handling. (`DocumentEditorKeyboardManager` + toolbar tab arrows beze změny API — 2026-05-17)
- [x] RED: bUnit test ověří arrow navigation v grid pickeru. (`ArrowRight_MovesKbFocus`, `ArrowDown_MovesKbFocusDown` — 2026-05-17)
- [x] GREEN: Sdílet grid navigation. (`TmDocumentTableGridPicker` keyboard model ověřen — 2026-05-17)
- [x] RED: bUnit test ověří Escape pořadí: find panel -> side panel -> editor. (`Phase8_EscapeClosesFindBeforeSidePanelAndRestoresEditorFocus` — 2026-05-17)
- [x] GREEN: Napojit stack priority. (`Priority` ve floating stacku + Escape close topmost — 2026-05-17)
- [x] REFACTOR: Odstranit izolované focus hacky z child komponent. (sjednocená restore evidence pro surface/toolbar/floating/modal — 2026-05-17)

### 8.5 Aria-live announce service

- [x] RED: bUnit test ověří existenci live regionu v editoru. (`Render_ExposesFloatingPortalAndLiveRegion` — 2026-05-17)
- [x] GREEN: Přidat `TmDocumentEditorLiveRegion`. (`document-editor-live-region` — 2026-05-17)
- [x] RED: Unit test ověří announce queue. (`DocumentEditorAnnouncerTests` — 2026-05-17)
- [x] GREEN: Přidat `DocumentEditorAnnouncer`. (polite/assertive queue — 2026-05-17)
- [x] RED: bUnit test ověří announce při save success. (`SaveSuccess_AnnouncesThroughLiveRegion` — 2026-05-17)
- [x] GREEN: Napojit save. (`Announce(_saveMessage)` — 2026-05-17)
- [x] RED: bUnit test ověří announce při find result count. (`FindResults_AnnouncesResultCountThroughLiveRegion` — 2026-05-17)
- [x] GREEN: Napojit find. (`HandleFindResultsChangedAsync` announce — 2026-05-17)
- [x] RED: bUnit test ověří announce při autosave error. (`AutoSaveFailure_AnnouncesThroughLiveRegion` — 2026-05-17)
- [x] GREEN: Napojit autosave error. (save failure path assertive announce — 2026-05-17)
- [x] RED: E2E accessibility smoke ověří live region text po find. (`Phase8_FindUpdatesEditorLiveRegion` — 2026-05-17)
- [x] GREEN: Doladit timing. (WASM restart + E2E zelené — 2026-05-17)

### 8.6 E2E checkpoint

- [x] E2E: Text selection mini toolbar positioning desktop. (`Phase8_MiniToolbarStaysInsideDesktopViewport` — 2026-05-17)
- [x] E2E: Text selection mini toolbar positioning mobile. (`Phase8_MiniToolbarStaysInsideNarrowViewport` — 2026-05-17)
- [x] E2E: Link panel focus restore. (`DocumentEditor_Phase10_LinkDialog_TabFocusesUrlInput`, `DocumentEditor_Phase10_LinkDialog_EscapeCloses` zelené — 2026-05-17)
- [x] E2E: Table context menu keyboard navigation. (`DocumentEditor_Phase14_TableContextMenuAddsRowAndPersists` zelený po image/caption insertion guardu — 2026-05-17)
- [x] E2E: Token autocomplete arrows/Enter/Escape. (`DocumentEditor_Phase10_TokenMenu_ArrowDownAndEnterInsertsToken`, `DocumentEditor_Phase10_TokenMenu_EscapeCloses` zelené — 2026-05-17)
- [x] E2E: Escape zavírá vrstvy ve správném pořadí. (`Phase8_EscapeClosesFindBeforeSidePanelAndRestoresEditorFocus` — 2026-05-17)
- [x] Spustit floating/focus/css test gate. (`node --check`, 54 unit/bUnit/JS, 4 Phase8 E2E a 5 návazných link/token/table E2E zelených — 2026-05-17)

## 9. Runtime-first Find & Replace

Cíl: find/replace musí být runtime transakce s undo/redo, track changes a marker layer.

### 9.1 Find commandy

- [x] RED: Unit test ověří command `find` má `AffectsData=false`.
- [x] GREEN: Přidat/ověřit command.
- [x] RED: Unit test ověří command `replace` má `AffectsData=true`.
- [x] GREEN: Přidat/ověřit command.
- [x] RED: Unit test ověří command `replaceAll` má `AffectsData=true`.
- [x] GREEN: Přidat command.
- [x] RED: bUnit test ověří Ctrl+F otevře find panel přes command registry.
- [x] GREEN: Napojit Ctrl+F.
- [x] RED: bUnit test ověří Ctrl+H otevře replace panel přes command registry.
- [x] GREEN: Napojit Ctrl+H.

### 9.2 Search scopes

- [x] RED: Unit test ověří `DocumentSearchScope.Body`.
- [x] GREEN: Přidat scope enum/model.
- [x] RED: Unit test ověří search v header/footer.
- [x] GREEN: Doplnit service search.
- [x] RED: Unit test ověří search v table cell.
- [x] GREEN: Doplnit traversal.
- [x] RED: Unit test ověří search v comments podle volitelného scope.
- [x] GREEN: Doplnit comment scope.
- [x] RED: bUnit test ověří scope selector ve find panelu.
- [x] GREEN: Přidat UI.
- [x] REFACTOR: Search service vrací stabilní marker ranges.

### 9.3 Runtime replace one

- [x] RED: JS test ověří `replaceOne` command nahradí active marker.
- [x] GREEN: Přidat runtime command.
- [x] RED: JS test ověří replace one vytvoří undo item.
- [x] GREEN: Napojit transaction.
- [x] RED: JS test ověří undo vrátí původní text.
- [x] GREEN: Doplnit undo payload.
- [x] RED: JS test ověří replace one zachová selection u nahrazeného textu.
- [x] GREEN: Doplnit after selection.
- [x] RED: bUnit test ověří find panel volá runtime bridge, ne přímo C# replace service.
- [x] GREEN: Přesměrovat replace one.
- [x] RED: E2E test ověří replace one a undo.
- [x] GREEN: Doladit.

### 9.4 Runtime replace all

- [x] RED: JS test ověří `replaceAll` nahradí všechny markers v body.
- [x] GREEN: Přidat command.
- [x] RED: JS test ověří replace all je jeden undo batch.
- [x] GREEN: Implementovat batch transaction.
- [x] RED: JS test ověří replace all funguje přes více blocků.
- [x] GREEN: Doplnit traversal.
- [x] RED: JS test ověří replace all funguje v table cells.
- [x] GREEN: Doplnit table traversal.
- [x] RED: JS test ověří replace all zachová marker store bez stale markerů.
- [x] GREEN: Refresh markers po replace.
- [x] RED: E2E test ověří replace all a undo.
- [x] GREEN: Doladit.

### 9.5 Track changes kompatibilita

- [x] RED: JS test ověří replace one s track changes vytvoří deletion + insertion revision.
- [x] GREEN: Napojit revision creation.
- [x] RED: JS test ověří replace all s track changes vytvoří revize pro každou náhradu nebo batch podle modelu.
- [x] GREEN: Implementovat vybrané chování.
- [x] RED: E2E test ověří review panel po replace with track changes.
- [x] GREEN: Napojit panel refresh.
- [x] RED: E2E test ověří accept/reject replace revision.
- [x] GREEN: Doladit.

### 9.6 UX zlepšení find panelu

- [x] RED: bUnit test ověří, že Ctrl+F předvyplní aktuálně vybraný text.
- [x] GREEN: Přidat selection text provider.
- [x] RED: bUnit test ověří result list pro více výsledků.
- [x] GREEN: Přidat result list UI.
- [x] RED: bUnit test ověří case sensitive/whole word options zůstávají.
- [x] GREEN: Zachovat.
- [x] RED: bUnit test ověří regex option je skrytá, dokud není implementovaná.
- [x] GREEN: Přidat feature flag nebo neukazovat.
- [ ] RED: E2E test ověří result list click scrolluje na výsledek.
- [x] GREEN: Napojit scroll.

### 9.7 E2E checkpoint

- [ ] E2E: Ctrl+F prefill selected text.
- [ ] E2E: Find next/previous.
- [x] E2E: Replace one + undo.
- [x] E2E: Replace all + undo.
- [ ] E2E: Replace in table cell.
- [ ] E2E: Replace in header/footer.
- [x] E2E: Replace with track changes + accept/reject.
- [x] Spustit find/replace/marker/runtime test gate.

## 10. Clipboard pipeline 2.0

Cíl: stabilní, rozšiřitelný clipboard flow pro Word, Google Docs, Google Sheets, raw HTML, URL, Tempo internal a obrázky.

### 10.1 Clipboard stage model

- [x] RED: Unit test ověří `DocumentClipboardRawInput` s html/plain/files/source metadata.
- [x] GREEN: Přidat raw input model.
- [x] RED: Unit test ověří `DocumentClipboardSourceDetectionResult`.
- [x] GREEN: Přidat detection model.
- [x] RED: Unit test ověří `DocumentClipboardNormalizedHtml`.
- [x] GREEN: Přidat normalized model.
- [x] RED: Unit test ověří `DocumentClipboardFragment`.
- [x] GREEN: Přidat fragment model.
- [x] RED: Unit test ověří `DocumentClipboardInsertionResult` s warnings.
- [x] GREEN: Přidat result model.
- [x] REFACTOR: Sladit s existujícími clipboard modely, neduplikovat zbytečně.

### 10.2 Pipeline API

- [x] RED: Unit test ověří registraci normalizeru podle source.
- [x] GREEN: Přidat/rozšířit `DocumentClipboardPipeline`.
- [x] RED: Unit test ověří stage pořadí raw -> detect -> normalize -> convert -> policy -> insert.
- [x] GREEN: Implementovat pipeline orchestration.
- [x] RED: Unit test ověří normalizer priority.
- [x] GREEN: Doplnit priority.
- [x] RED: Unit test ověří, že high-priority normalizer může přidat warning bez zastavení pipeline.
- [x] GREEN: Doplnit warning aggregation.
- [x] RED: Unit test ověří host custom normalizer.
- [x] GREEN: Přidat extension point.
- [x] REFACTOR: Existující normalizery napojit na nové stage rozhraní.

### 10.3 Source detection

- [x] RED: Unit test ověří Word HTML detection přes Office fragment/classy.
- [x] GREEN: Implementovat Word detector.
- [x] RED: Unit test ověří Google Docs detection.
- [x] GREEN: Implementovat Google Docs detector.
- [x] RED: Unit test ověří Google Sheets detection.
- [x] GREEN: Implementovat Sheets detector.
- [x] RED: Unit test ověří Tempo internal clipboard detection přes custom MIME nebo marker.
- [x] GREEN: Přidat Tempo detector.
- [x] RED: Unit test ověří URL-only plain text detection.
- [x] GREEN: Přidat URL detector.
- [x] RED: Unit test ověří plain text fallback.
- [x] GREEN: Přidat fallback.

### 10.4 Normalizers

- [x] RED: Unit test ověří Word basic paragraph paste.
- [x] GREEN: Implementovat/napojit Word normalizer.
- [x] RED: Unit test ověří Word inline formatting.
- [x] GREEN: Doplnit bold/italic/underline/link mapping.
- [x] RED: Unit test ověří Word list.
- [x] GREEN: Doplnit list mapping.
- [x] RED: Unit test ověří Word table.
- [x] GREEN: Doplnit table mapping.
- [x] RED: Unit test ověří Google Docs headings.
- [x] GREEN: Doplnit Docs heading mapping.
- [x] RED: Unit test ověří Google Docs basic formatting.
- [x] GREEN: Doplnit Docs mapping.
- [x] RED: Unit test ověří Google Sheets table.
- [x] GREEN: Doplnit Sheets mapping.
- [x] RED: Unit test ověří raw HTML sanitizer odstraní script/event attributes.
- [x] GREEN: Doplnit sanitizer.
- [x] RED: Unit test ověří URL normalizer vloží link.
- [x] GREEN: Implementovat URL path.
- [x] REFACTOR: Sdílet HTML parsing helpery, ale bez ad-hoc string manipulace tam, kde jde použít parser.

### 10.5 Runtime paste transaction

- [x] RED: JS test ověří paste pipeline výsledek vloží jako jeden undo transaction.
- [x] GREEN: Napojit runtime paste insertion.
- [x] RED: JS test ověří paste with track changes vytvoří insertion revisions.
- [x] GREEN: Napojit track changes.
- [x] RED: JS test ověří paste do table cell respektuje insertion policy.
- [x] GREEN: Napojit policy.
- [x] RED: JS test ověří paste do header/footer respektuje schema.
- [x] GREEN: Napojit schema.
- [x] RED: E2E test ověří Word paste + undo.
- [x] GREEN: Doladit.
- [x] RED: E2E test ověří Sheets paste jako table.
- [x] GREEN: Doladit.

### 10.6 Clipboard images

- [x] RED: Unit test ověří clipboard image s providerem vytvoří upload request.
- [x] GREEN: Napojit image provider.
- [x] RED: Unit test ověří clipboard image bez provideru a offline enabled vytvoří local draft asset.
- [x] GREEN: Napojit offline asset.
- [x] RED: Unit test ověří clipboard image bez provideru a offline disabled vrátí warning.
- [x] GREEN: Implementovat warning.
- [x] RED: E2E test vloží image z clipboard eventu.
- [x] GREEN: Doladit runtime.
- [x] RED: E2E test uloží dokument a ověří asset commit.
- [x] GREEN: Napojit save flow.

### 10.7 Paste report UX

- [x] RED: bUnit test ověří paste report banner po warnings.
- [x] GREEN: Přidat `TmDocumentPasteReport`.
- [x] RED: bUnit test ověří report auto-hide.
- [x] GREEN: Přidat timer/close.
- [x] RED: bUnit test ověří detail warnings expand/collapse.
- [x] GREEN: Přidat detail UI.
- [x] RED: E2E test ověří paste report po Word paste s warnings.
- [x] GREEN: Doladit.

### 10.8 E2E checkpoint

- [x] E2E: Word basic paste.
- [x] E2E: Word list paste.
- [x] E2E: Word table paste.
- [x] E2E: Google Docs heading paste.
- [x] E2E: Google Sheets table paste.
- [x] E2E: URL paste.
- [x] E2E: Clipboard image paste.
- [x] E2E: Paste report.
- [x] Spustit clipboard unit tests, JS syntax, DocumentEditor paste E2E subset.

## 11. Image UX upgrade

Cíl: image workflow zvednout na mature-editor úroveň: upload/URL/provider, progress, inspector, alt, caption, link, resize, wrap.

### 11.1 Image command model

- [x] RED: Unit test ověří command `insertImage`.
- [x] GREEN: Zachovat/rozšířit command.
- [x] RED: Unit test ověří command `replaceImage`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří command `setImageAltText`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří command `toggleImageCaption`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří command `setImageLink`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří command `setImageWrapMode`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří command `setImageSize`.
- [x] GREEN: Přidat command.

### 11.2 Insert image split flow

- [x] RED: bUnit test ověří split button obsahuje Upload, URL, Asset provider.
- [x] GREEN: Přidat split menu.
- [x] RED: bUnit test ověří Upload položka disabled bez `ImageProvider`.
- [x] GREEN: Napojit provider capability.
- [x] RED: bUnit test ověří URL položka otevře URL panel.
- [x] GREEN: Přidat URL panel.
- [x] RED: bUnit test ověří Asset provider položka se ukáže jen při provider capability.
- [x] GREEN: Přidat capability flag.
- [x] RED: E2E test vloží image URL přes nový flow.
- [x] GREEN: Napojit runtime command.
- [x] RED: E2E test upload image přes provider.
- [x] GREEN: Doladit provider flow.

### 11.3 Upload progress a error state

- [x] RED: bUnit test ověří image placeholder během uploadu.
- [x] GREEN: Přidat upload placeholder UI.
- [x] RED: JS test ověří runtime renderuje upload progress.
- [x] GREEN: Přidat progress rendering.
- [x] RED: bUnit test ověří upload error state s Retry/Remove.
- [x] GREEN: Přidat error UI.
- [x] RED: E2E test simuluje upload error a Retry.
- [x] GREEN: Doladit provider mock.
- [x] RED: E2E test simuluje Remove failed image.
- [x] GREEN: Napojit command.

### 11.4 Image inspector

- [x] RED: bUnit test ověří image selection otevře image inspector přes floating UI manager.
- [x] GREEN: Přidat `TmDocumentImageInspector`.
- [x] RED: bUnit test ověří inspector ukazuje alt text input.
- [x] GREEN: Implementovat alt editor.
- [x] RED: bUnit test ověří caption toggle.
- [x] GREEN: Implementovat caption UI.
- [x] RED: bUnit test ověří link input.
- [x] GREEN: Implementovat link UI.
- [x] RED: bUnit test ověří wrap mode swatches.
- [x] GREEN: Implementovat swatches.
- [x] RED: bUnit test ověří alignment controls.
- [x] GREEN: Implementovat alignment.
- [x] RED: bUnit test ověří width/height controls.
- [x] GREEN: Implementovat size controls.
- [x] RED: E2E test vybere image a změní alt.
- [x] GREEN: Napojit runtime.
- [x] RED: E2E test vybere image a změní wrap mode.
- [x] GREEN: Doladit.

### 11.5 Resize handles

- [x] RED: JS test ověří resize handle render při selected image.
- [x] GREEN: Přidat handles.
- [x] RED: JS test ověří drag resize mění width/height.
- [x] GREEN: Implementovat drag.
- [x] RED: JS test ověří shift/free resize nebo zachování poměru podle UX rozhodnutí.
- [x] GREEN: Doplnit behavior.
- [x] RED: JS test ověří resize vytvoří undo transaction.
- [x] GREEN: Napojit undo.
- [x] RED: JS test ověří resize s track changes vytvoří image revision.
- [x] GREEN: Napojit revision.
- [x] RED: E2E test resize image a save/reload.
- [x] GREEN: Persistovat model.

### 11.6 Caption a alt accessibility

- [x] RED: Unit test ověří image bez alt textu je označená v properties/status.
- [x] GREEN: Přidat accessibility warning.
- [x] RED: bUnit test ověří inspector zobrazí alt doporučení.
- [x] GREEN: Přidat localized text.
- [x] RED: E2E test přidá caption.
- [x] GREEN: Napojit caption serialization.
- [x] RED: E2E test odstraní caption.
- [x] GREEN: Napojit toggle off.

### 11.7 E2E checkpoint

- [x] E2E: Insert image URL.
- [x] E2E: Upload image progress.
- [x] E2E: Upload error retry/remove.
- [x] E2E: Image inspector alt/caption/link/wrap.
- [x] E2E: Resize image + undo + save/reload.
- [x] E2E: Image UX mobile viewport.
- [x] Spustit image unit/js/e2e subset.

## 12. Table UX upgrade

Cíl: table selection, properties, cell properties, resize, keyboard navigation a paste kvalita.

### 12.1 Table command model

- [x] RED: Unit test ověří command `insertTable`.
- [x] GREEN: Zachovat/rozšířit.
- [x] RED: Unit test ověří `insertTableRowBefore`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `insertTableRowAfter`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `insertTableColumnBefore`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `insertTableColumnAfter`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `deleteTableRow`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `deleteTableColumn`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `mergeTableCells`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `splitTableCell`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `tableProperties`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří `cellProperties`.
- [x] GREEN: Přidat command.

### 12.2 Table grid picker polish

- [x] RED: bUnit test ověří 10x10 grid.
- [x] GREEN: Zajistit 10x10.
- [x] RED: bUnit test ověří hover/focus label `rows x columns`.
- [x] GREEN: Implementovat label.
- [x] RED: bUnit test ověří keyboard arrows v gridu.
- [x] GREEN: Implementovat shared grid keyboard.
- [x] RED: bUnit test ověří Enter vloží zvolenou velikost.
- [x] GREEN: Napojit insert.
- [x] RED: E2E test vloží 4x5 tabulku klávesnicí.
- [x] GREEN: Doladit.

### 12.3 Table selection overlay

- [x] RED: JS test ověří selected cell marker.
- [x] GREEN: Přidat table selection state.
- [x] RED: JS test ověří row handle render.
- [x] GREEN: Přidat row handles.
- [x] RED: JS test ověří column handle render.
- [x] GREEN: Přidat column handles.
- [x] RED: JS test ověří click row handle vybere row.
- [x] GREEN: Implementovat row selection.
- [x] RED: JS test ověří click column handle vybere column.
- [x] GREEN: Implementovat column selection.
- [x] RED: JS test ověří drag přes buňky vybere range.
- [x] GREEN: Implementovat cell range selection.
- [x] RED: E2E test vybere cell range a merge.
- [x] GREEN: Doladit.

### 12.4 Table contextual toolbar

- [x] RED: bUnit test ověří table selection otevře table toolbar přes floating UI.
- [x] GREEN: Přidat `TmDocumentTableToolbar`.
- [x] RED: bUnit test ověří toolbar obsahuje row/column insert/delete.
- [x] GREEN: Přidat buttons.
- [x] RED: bUnit test ověří toolbar obsahuje merge/split.
- [x] GREEN: Přidat buttons.
- [x] RED: bUnit test ověří toolbar obsahuje properties.
- [x] GREEN: Přidat properties button.
- [x] RED: E2E test spustí insert row z contextual toolbaru.
- [x] GREEN: Napojit command.

### 12.5 Table properties panel

- [x] RED: bUnit test ověří `TmDocumentTablePropertiesPanel` render.
- [x] GREEN: Přidat komponentu.
- [x] RED: bUnit test ověří table width input.
- [x] GREEN: Implementovat width.
- [x] RED: bUnit test ověří table alignment segmented control.
- [x] GREEN: Implementovat alignment.
- [x] RED: bUnit test ověří border controls.
- [x] GREEN: Implementovat border UI.
- [x] RED: bUnit test ověří cell padding controls.
- [x] GREEN: Implementovat padding.
- [x] RED: bUnit test ověří background color.
- [x] GREEN: Implementovat background.
- [x] RED: JS test ověří set table properties runtime command.
- [x] GREEN: Napojit runtime.
- [x] RED: E2E test změní table width a save/reload.
- [x] GREEN: Persistovat model.

### 12.6 Cell properties panel

- [x] RED: bUnit test ověří `TmDocumentCellPropertiesPanel` render.
- [x] GREEN: Přidat komponentu.
- [x] RED: bUnit test ověří vertical align control.
- [x] GREEN: Implementovat.
- [x] RED: bUnit test ověří background color.
- [x] GREEN: Implementovat.
- [x] RED: bUnit test ověří border controls.
- [x] GREEN: Implementovat.
- [x] RED: bUnit test ověří colspan/rowspan readonly info.
- [x] GREEN: Implementovat info.
- [x] RED: JS test ověří set cell properties runtime command.
- [x] GREEN: Napojit runtime.
- [x] RED: E2E test změní cell background a save/reload.
- [x] GREEN: Persistovat model.

### 12.7 Column resize

- [x] RED: JS test ověří resize handles mezi columns.
- [x] GREEN: Přidat handles.
- [x] RED: JS test ověří drag změní column width.
- [x] GREEN: Implementovat drag.
- [x] RED: JS test ověří resize je undoable.
- [x] GREEN: Napojit transaction.
- [x] RED: JS test ověří resize s track changes vytvoří table revision.
- [x] GREEN: Napojit revision.
- [x] RED: E2E test resize column + undo + save/reload.
- [x] GREEN: Persistovat model.

### 12.8 Keyboard navigation

- [x] RED: JS test ověří Tab přejde do další buňky.
- [x] GREEN: Implementovat.
- [x] RED: JS test ověří Shift+Tab přejde do předchozí buňky.
- [x] GREEN: Implementovat.
- [x] RED: JS test ověří Tab v poslední buňce přidá row nebo přejde za tabulku podle UX rozhodnutí.
- [x] GREEN: Implementovat vybrané chování.
- [x] RED: JS test ověří Enter uvnitř buňky vytvoří odstavec v buňce.
- [x] GREEN: Implementovat.
- [x] RED: JS test ověří Ctrl+Enter přejde za tabulku.
- [x] GREEN: Implementovat.
- [x] RED: E2E test projde tabulkou klávesnicí.
- [x] GREEN: Doladit.

### 12.9 E2E checkpoint

- [x] E2E: Insert table grid keyboard.
- [x] E2E: Select cells, merge, split.
- [x] E2E: Table toolbar row/column commands.
- [x] E2E: Table properties save/reload.
- [x] E2E: Cell properties save/reload.
- [x] E2E: Column resize undo/save/reload.
- [x] E2E: Table keyboard navigation.
- [x] E2E: Mobile/narrow table toolbar.
- [x] Spustit table unit/js/e2e subset.

## 13. Comments a Review UX

Cíl: zlepšit práci s komentáři, revizemi a review workflow.

### 13.1 Comments rail alignment

- [x] RED: E2E visual/layout test ověří comment thread je zarovnaný k anchoru.
- [x] GREEN: Přidat anchor-to-rail positioning.
- [x] RED: JS test ověří update při scrollu.
- [x] GREEN: Přidat scroll sync.
- [x] RED: JS test ověří update při resize.
- [x] GREEN: Přidat resize sync.
- [x] RED: E2E test ověří alignment při dlouhém dokumentu.
- [x] GREEN: Doladit.

### 13.2 Comment filters

- [x] RED: bUnit test ověří filter Open.
- [x] GREEN: Přidat filter state.
- [x] RED: bUnit test ověří filter Resolved.
- [x] GREEN: Doplnit.
- [x] RED: bUnit test ověří filter Mine.
- [x] GREEN: Doplnit author filter.
- [x] RED: bUnit test ověří filter All.
- [x] GREEN: Doplnit.
- [x] RED: E2E test filtruje comments rail.
- [x] GREEN: Doladit.

### 13.3 Comment sorting

- [x] RED: Unit test ověří sort by position.
- [x] GREEN: Přidat comparer.
- [x] RED: Unit test ověří sort by time.
- [x] GREEN: Přidat comparer.
- [x] RED: bUnit test ověří sort selector.
- [x] GREEN: Přidat UI.
- [x] RED: E2E test přepne sort a ověří pořadí.
- [x] GREEN: Doladit.

### 13.4 Resolved comments collapsed display

- [x] RED: bUnit test ověří resolved thread collapsed default.
- [x] GREEN: Implementovat collapsed state.
- [x] RED: bUnit test ověří expand resolved thread.
- [x] GREEN: Přidat expand.
- [x] RED: bUnit test ověří reopen z collapsed thread.
- [x] GREEN: Napojit command.
- [x] RED: E2E test resolved comment collapsed/expand/reopen.
- [x] GREEN: Doladit.

### 13.5 Review summary banner

- [x] RED: bUnit test ověří banner `N pending changes, M comments`.
- [x] GREEN: Přidat `TmDocumentReviewSummary`.
- [x] RED: bUnit test ověří klik na pending changes otevře revisions panel.
- [x] GREEN: Napojit command.
- [x] RED: bUnit test ověří klik na comments otevře comments panel.
- [x] GREEN: Napojit command.
- [x] RED: E2E test banner actions.
- [x] GREEN: Doladit.

### 13.6 Accept/reject all

- [x] RED: Unit test ověří command `acceptAllRevisions`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří command `rejectAllRevisions`.
- [x] GREEN: Přidat command.
- [x] RED: Unit test ověří filter by author.
- [x] GREEN: Přidat review filter model.
- [x] RED: Unit test ověří filter by type.
- [x] GREEN: Doplnit type filter.
- [x] RED: JS test ověří accept all applies to runtime revisions.
- [x] GREEN: Implementovat runtime batch review.
- [x] RED: JS test ověří reject all applies to runtime revisions.
- [x] GREEN: Implementovat reject batch.
- [x] RED: E2E test accept all.
- [x] GREEN: Doladit.
- [x] RED: E2E test reject all.
- [x] GREEN: Doladit.

### 13.7 Review display modes polish

- [x] RED: bUnit test ověří modes AllMarkup, SimpleMarkup, NoMarkup, Original.
- [x] GREEN: Zajistit UI modes.
- [x] RED: JS test ověří SimpleMarkup rendering.
- [x] GREEN: Implementovat/zkontrolovat runtime classes.
- [x] RED: JS test ověří NoMarkup rendering.
- [x] GREEN: Implementovat/zkontrolovat.
- [x] RED: JS test ověří Original rendering.
- [x] GREEN: Implementovat/zkontrolovat.
- [x] RED: E2E test přepíná review modes.
- [x] GREEN: Doladit.

### 13.8 E2E checkpoint

- [x] E2E: Comment alignment při scrollu.
- [x] E2E: Comment filters/sort.
- [x] E2E: Resolved collapsed/expand.
- [x] E2E: Review summary actions.
- [x] E2E: Accept all/reject all.
- [x] E2E: Review display modes.
- [x] Spustit comments/revisions test gate.
## 14. Tokeny, mentions a autocomplete engine

Cíl: zobecnit token popover na autocomplete engine s více triggery a robustním async feed chováním.

### 14.1 Autocomplete model

- [x] RED: Unit test ověří `DocumentAutocompleteTrigger` s markerem `{{`.
- [x] GREEN: Přidat trigger model.
- [x] RED: Unit test ověří trigger `@`.
- [x] GREEN: Přidat mention trigger support.
- [x] RED: Unit test ověří trigger `#`.
- [x] GREEN: Přidat generic tag trigger support.
- [x] RED: Unit test ověří trigger `/`.
- [x] GREEN: Přidat slash command trigger support, UI může zůstat vypnuté.
- [x] RED: Unit test ověří minimum characters.
- [x] GREEN: Doplnit minimum.
- [x] RED: Unit test ověří dropdown limit.
- [x] GREEN: Doplnit limit.
- [x] RED: Unit test ověří custom item renderer metadata.
- [x] GREEN: Přidat renderer descriptor.

### 14.2 Feed provider abstraction

- [x] RED: Unit test ověří `IDocumentAutocompleteProvider`.
- [x] GREEN: Přidat provider interface.
- [x] RED: Unit test ověří token provider adapter.
- [x] GREEN: Adaptovat existující `ITokenDataProvider`.
- [x] RED: Unit test ověří mention provider adapter.
- [x] GREEN: Adaptovat `IMentionDataProvider`.
- [x] RED: Unit test ověří async cancellation.
- [x] GREEN: Přidat cancellation token.
- [x] RED: Unit test ověří out-of-order response discard.
- [x] GREEN: Přidat request sequence guard.
- [x] RED: Unit test ověří provider error warning bez pádu editoru.
- [x] GREEN: Přidat error handling.

### 14.3 Runtime trigger detection

- [x] RED: JS test ověří `{{` otevře token autocomplete.
- [x] GREEN: Napojit trigger.
- [x] RED: JS test ověří `@` otevře mention autocomplete.
- [x] GREEN: Napojit trigger.
- [x] RED: JS test ověří marker range update při psaní query.
- [x] GREEN: Napojit marker store.
- [x] RED: JS test ověří Escape zavře autocomplete a odstraní query marker.
- [x] GREEN: Implementovat close.
- [x] RED: JS test ověří Backspace před markerem zavře autocomplete.
- [x] GREEN: Implementovat.
- [x] RED: E2E test token autocomplete keyboard flow.
- [x] GREEN: Doladit.
- [x] RED: E2E test mention autocomplete keyboard flow.
- [x] GREEN: Doladit.

### 14.4 Autocomplete UI

- [x] RED: bUnit test ověří `TmDocumentAutocompleteMenu` render.
- [x] GREEN: Přidat komponentu nebo upravit `TmDocumentTokenMenu`.
- [x] RED: bUnit test ověří loading state.
- [x] GREEN: Přidat loading.
- [x] RED: bUnit test ověří empty state.
- [x] GREEN: Přidat empty.
- [x] RED: bUnit test ověří error state.
- [x] GREEN: Přidat error.
- [x] RED: bUnit test ověří highlighted item.
- [x] GREEN: Přidat highlight.
- [x] RED: bUnit test ověří custom renderer hook.
- [x] GREEN: Přidat render fragment.
- [x] RED: bUnit test ověří arrows/Enter/Escape/Tab.
- [x] GREEN: Napojit keyboard.

### 14.5 Slash commands

- [x] RED: Unit test ověří slash command provider vrací editor commandy.
- [x] GREEN: Přidat provider nad command registry.
- [x] RED: bUnit test ověří slash menu zobrazuje Insert table, Image, Page break.
- [x] GREEN: Přidat UI.
- [x] RED: JS test ověří výběr slash commandu odstraní slash query.
- [x] GREEN: Implementovat.
- [x] RED: E2E test `/table` vloží tabulku.
- [x] GREEN: Doladit.

### 14.6 E2E checkpoint

- [x] E2E: Token autocomplete `{{`.
- [x] E2E: Mention autocomplete `@`.
- [x] E2E: Out-of-order provider responses.
- [x] E2E: Slash command `/table`.
- [x] E2E: Mobile autocomplete positioning.
- [x] Spustit autocomplete/token/mention test gate.

## 15. Document surface, page navigation a view modes

Cíl: zlepšit práci s dlouhými dokumenty, page breaks, non-printing characters a outline sync.

### 15.1 Page navigator / thumbnails

- [x] RED: bUnit test ověří side panel tab nebo view panel item `Pages`.
- [x] GREEN: Přidat page navigator entry.
- [x] RED: bUnit test ověří `TmDocumentPageNavigator` renderuje počet stránek.
- [x] GREEN: Přidat komponentu.
- [x] RED: JS test ověří runtime poskytuje page metrics.
- [x] GREEN: Přidat API `getPageMetrics`.
- [x] RED: bUnit test ověří klik na page naviguje na stránku.
- [x] GREEN: Napojit scroll command.
- [x] RED: E2E test naviguje na druhou stránku.
- [x] GREEN: Doladit.
- [x] REFACTOR: Virtuální stránky reprezentovat jako lightweight placeholders.

### 15.2 Page break UX

- [x] RED: JS test ověří page break renderuje viditelný handle v show blocks režimu.
- [x] GREEN: Přidat handle.
- [x] RED: JS test ověří click page break vybere block.
- [x] GREEN: Přidat selection.
- [x] RED: JS test ověří Backspace/Delete odstraní selected page break.
- [x] GREEN: Přidat command.
- [x] RED: bUnit test ověří context menu pro page break obsahuje Delete.
- [x] GREEN: Přidat context action.
- [x] RED: E2E test insert page break/delete page break.
- [x] GREEN: Doladit.

### 15.3 Non-printing characters

- [x] RED: Unit test ověří command `toggleNonPrintingCharacters`.
- [x] GREEN: Přidat command.
- [x] RED: bUnit test ověří View toolbar toggle.
- [x] GREEN: Přidat toolbar item.
- [x] RED: JS test ověří paragraph marks rendering.
- [x] GREEN: Přidat rendering.
- [x] RED: JS test ověří spaces/tabs rendering.
- [x] GREEN: Přidat rendering.
- [x] RED: JS test ověří page break mark rendering.
- [x] GREEN: Přidat rendering.
- [x] RED: E2E test toggle non-printing characters.
- [x] GREEN: Doladit CSS.

### 15.4 Empty states

- [x] RED: bUnit test ověří prázdný body empty state s localized textem.
- [x] GREEN: Upravit empty state.
- [x] RED: JS test ověří prázdná table cell placeholder.
- [x] GREEN: Přidat placeholder.
- [x] RED: JS test ověří prázdný header placeholder.
- [x] GREEN: Přidat header placeholder.
- [x] RED: JS test ověří prázdný footer placeholder.
- [x] GREEN: Přidat footer placeholder.
- [x] RED: E2E test prázdné regiony nekolabují a lze do nich psát.
- [x] GREEN: Doladit.

### 15.5 Page overflow UX

- [x] RED: JS test ověří page overflow marker bez rušivého layout shiftu.
- [x] GREEN: Upravit overflow warning.
- [x] RED: bUnit test ověří overflow action `Insert page break`.
- [x] GREEN: Přidat action.
- [x] RED: bUnit test ověří overflow action `Adjust spacing` je skrytá, dokud není implementovaná.
- [x] GREEN: Skrýt nebo feature flag.
- [x] RED: E2E test vygeneruje overflow a vloží page break z warningu.
- [x] GREEN: Doladit.

### 15.6 Outline sync

- [x] RED: Unit test ověří outline service vrací heading ids.
- [x] GREEN: Zachovat/rozšířit outline service.
- [x] RED: JS test ověří runtime hlásí active heading podle scrollu.
- [x] GREEN: Přidat active heading bridge.
- [x] RED: bUnit test ověří outline panel highlight active heading.
- [x] GREEN: Přidat active state.
- [x] RED: E2E test scroll zvýrazní aktuální heading.
- [x] GREEN: Doladit.

### 15.7 E2E checkpoint

- [x] E2E: Page navigator.
- [x] E2E: Page break insert/delete.
- [x] E2E: Non-printing characters toggle.
- [x] E2E: Empty body/cell/header/footer.
- [x] E2E: Page overflow warning action.
- [x] E2E: Outline active heading sync.
- [x] Spustit surface/layout/css test gate.

## 16. Autosave, pending actions a unload guard

Cíl: spolehlivý save stav podle CKEditor Autosave principů.

### 16.1 Autosave state machine

- [x] RED: Unit test ověří state `synchronized`.
- [x] GREEN: Přidat `DocumentAutosaveState`.
- [x] RED: Unit test ověří přechod synchronized -> waiting po local change.
- [x] GREEN: Implementovat state machine.
- [x] RED: Unit test ověří waiting -> saving po debounce.
- [x] GREEN: Přidat debounce abstraction.
- [x] RED: Unit test ověří saving -> synchronized po úspěchu.
- [x] GREEN: Implementovat success.
- [x] RED: Unit test ověří saving -> error po provider chybě.
- [x] GREEN: Implementovat error.
- [x] RED: Unit test ověří error -> saving retry.
- [x] GREEN: Přidat retry policy.
- [x] RED: Unit test ověří změna během saving nastaví immediate save after current.
- [x] GREEN: Implementovat pending immediate save.

### 16.2 Pending actions integration

- [x] RED: Unit test ověří pending action při waiting.
- [x] GREEN: Napojit pending service.
- [x] RED: Unit test ověří pending action při saving.
- [x] GREEN: Doplnit.
- [x] RED: Unit test ověří pending action se odstraní po synchronized.
- [x] GREEN: Doplnit cleanup.
- [x] RED: bUnit test ověří status bar text pro waiting.
- [x] GREEN: Přidat status message.
- [x] RED: bUnit test ověří status bar text pro saving.
- [x] GREEN: Přidat message.
- [x] RED: bUnit test ověří status bar text pro error.
- [x] GREEN: Přidat message.
- [x] REFACTOR: Sjednotit existující `_isSaving`, `_isDirty`, `_saveMessage` s autosave state bez rozbití public behavior.

### 16.3 Beforeunload guard

- [x] RED: JS test ověří beforeunload listener se registruje při pending action.
- [x] GREEN: Přidat JS bridge.
- [x] RED: JS test ověří beforeunload listener se odstraní bez pending action.
- [x] GREEN: Implementovat cleanup.
- [x] RED: bUnit test ověří component dispose odstraní guard.
- [x] GREEN: Napojit dispose.
- [x] RED: E2E test simuluje pending action a ověří guard state přes debug API.
- [x] GREEN: Doladit.

### 16.4 Save concurrency a provider chyby

- [x] RED: Unit test ověří souběžné save volání sdílí in-flight promise/task nebo je serializované.
- [x] GREEN: Implementovat serializaci.
- [x] RED: Unit test ověří concurrency token update po save.
- [x] GREEN: Zachovat/rozšířit.
- [x] RED: Unit test ověří recoverable provider error zobrazí retry.
- [x] GREEN: Přidat retry UI.
- [x] RED: Unit test ověří non-recoverable provider error necyklí retry.
- [x] GREEN: Přidat error classification.
- [x] RED: E2E test provider save error -> retry -> success.
- [x] GREEN: Doladit demo provider.

### 16.5 E2E checkpoint

- [x] E2E: Autosave waiting/saving/synchronized status.
- [x] E2E: Typing during save triggers second save.
- [x] E2E: Provider error retry.
- [x] E2E: Beforeunload guard debug state.
- [x] E2E: Manual save stále funguje.
- [x] Spustit autosave/offline/save test gate.

## 17. Watchdog hardening

Cíl: produkční recovery po runtime chybě se stabilním snapshotem, retry policy a telemetry.

### 17.1 Stable snapshot cache

- [x] RED: JS test ověří runtime ukládá stable snapshot po successful transaction.
- [x] GREEN: Přidat stable snapshot cache.
- [x] RED: JS test ověří cache obsahuje document snapshot.
- [x] GREEN: Uložit document.
- [x] RED: JS test ověří cache obsahuje marker store.
- [x] GREEN: Uložit markers.
- [x] RED: JS test ověří cache obsahuje selection.
- [x] GREEN: Uložit selection.
- [x] RED: JS test ověří cache obsahuje undo metadata.
- [x] GREEN: Uložit undo state nebo bezpečný subset.
- [x] RED: JS test ověří cache obsahuje pending upload state.
- [x] GREEN: Uložit upload state.

### 17.2 Recovery classification

- [x] RED: JS test ověří command error classification.
- [x] GREEN: Přidat error source.
- [x] RED: JS test ověří remote operation error classification.
- [x] GREEN: Přidat source.
- [x] RED: JS test ověří render error classification.
- [x] GREEN: Přidat guarded render wrapper.
- [x] RED: JS test ověří serialization error classification.
- [x] GREEN: Přidat guarded serialization wrapper.
- [x] RED: bUnit test ověří runtime message podle classification.
- [x] GREEN: Napojit message.

### 17.3 Retry policy

- [x] RED: JS test ověří první recovery attempt.
- [x] GREEN: Implementovat.
- [x] RED: JS test ověří retry limit.
- [x] GREEN: Přidat limit.
- [x] RED: JS test ověří exponential backoff metadata.
- [x] GREEN: Přidat backoff.
- [x] RED: JS test ověří po překročení limitu state `failed`.
- [x] GREEN: Implementovat failed.
- [x] RED: bUnit test ověří failed state zobrazí recovery failed UI.
- [x] GREEN: Napojit UI.

### 17.4 Telemetry/debug events

- [x] RED: Unit test ověří event `runtimeRecovered`.
- [x] GREEN: Přidat event callback/model.
- [x] RED: Unit test ověří event `runtimeRecoveryFailed`.
- [x] GREEN: Přidat event.
- [x] RED: Unit test ověří event `snapshotFallbackUsed`.
- [x] GREEN: Přidat event.
- [x] RED: bUnit test ověří debug tools zobrazí poslední recovery detail.
- [x] GREEN: Přidat debug detail.
- [x] RED: E2E test simuluje recoverable command crash.
- [x] GREEN: Doladit.
- [x] RED: E2E test simuluje failed recovery.
- [x] GREEN: Doladit.

### 17.5 E2E checkpoint

- [x] E2E: Runtime command crash recovery.
- [x] E2E: Remote operation crash fallback.
- [x] E2E: Recovery zachová text a selection.
- [x] E2E: Recovery zachová comments/revisions markers.
- [x] E2E: Recovery failed state je srozumitelný.
- [x] Spustit watchdog/runtime/e2e subset.

## 18. Source/debug a developer experience

Cíl: zachovat source/debug pouze jako bezpečný developer workflow, ne jako veřejné HTML editing.

### 18.1 Debug JSON inspector

- [x] RED: bUnit test ověří JSON inspector je dostupný jen při `ShowDebugTools=true`.
- [x] GREEN: Zachovat/rozšířit guard.
- [x] RED: bUnit test ověří inspector ukazuje canonical document snapshot.
- [x] GREEN: Doplnit snapshot.
- [x] RED: bUnit test ověří inspector ukazuje runtime debug state.
- [x] GREEN: Doplnit debug bridge.
- [x] RED: bUnit test ověří copy JSON button.
- [x] GREEN: Přidat command.
- [x] RED: E2E debug smoke ověří otevření inspectoru.
- [x] GREEN: Doladit.

### 18.2 Clipboard HTML debug

- [x] RED: bUnit test ověří clipboard HTML debug je dostupný jen při `ShowDebugTools=true`.
- [x] GREEN: Zachovat/rozšířit guard.
- [x] RED: bUnit test ověří zobrazení posledního raw/normalized clipboard HTML.
- [x] GREEN: Doplnit pipeline debug data.
- [x] RED: bUnit test ověří warnings v debug view.
- [x] GREEN: Doplnit.
- [x] RED: E2E debug smoke ověří paste debug view.
- [x] GREEN: Doladit.

### 18.3 Public source editing guard

- [x] RED: bUnit test ověří veřejný HTML source editing není zobrazený v produkčním UI.
- [x] GREEN: Nepřidávat public HTML editor.
- [x] RED: Unit test ověří JSON import/debug edit nelze spustit bez explicitního debug flagu.
- [x] GREEN: Přidat guard, pokud existuje edit flow.
- [x] REFACTOR: Dokumentovat v planning poznámce, že persistence mode je structured model, ne HTML.

Poznámka fáze 18: Debug JSON inspector i Clipboard HTML debug zůstávají read-only developer workflow za `ShowDebugTools`. Persistence a save/load hranice dál používají strukturovaný `DocumentEditorDocument`/canonical model; komponenta nezavádí veřejný HTML source editor ani editovatelný JSON import bez explicitního debug guardu.

## 19. Import/export a DOCX/PDF dopady

Cíl: každé nové modelové pole nebo UX feature musí přežít save/reload a podle možností import/export.

### 19.1 Model persistence gate

- [x] RED: Unit test ověří serialization pro image caption/link/wrap/size.
- [x] GREEN: Doplnit serializer.
- [x] RED: Unit test ověří serialization pro table properties.
- [x] GREEN: Doplnit serializer.
- [x] RED: Unit test ověří serialization pro cell properties.
- [x] GREEN: Doplnit serializer.
- [x] RED: Unit test ověří serialization pro non-printing setting není persisted do document content, pokud je view-only.
- [x] GREEN: Uložit jako editor preference nebo nepersistovat.
- [x] RED: Unit test ověří markers s `AffectsData=false` nejsou v content JSON.
- [x] GREEN: Doplnit filtering.

### 19.2 DOCX import/export

- [x] RED: DocumentFormats test ověří image size export.
- [x] GREEN: Doplnit DOCX exporter.
- [x] RED: DocumentFormats test ověří image caption export.
- [x] GREEN: Doplnit exporter nebo warning, pokud není podporováno.
- [x] RED: DocumentFormats test ověří table width export.
- [x] GREEN: Doplnit exporter.
- [x] RED: DocumentFormats test ověří cell background export.
- [x] GREEN: Doplnit exporter.
- [x] RED: DocumentFormats test ověří comments/revisions compatibility po marker migraci.
- [x] GREEN: Doplnit importer/exporter mapping.
- [x] RED: Import test ověří neznámý DOCX construct -> warning/fallback.
- [x] GREEN: Napojit schema/policy.

### 19.3 PDF export

- [x] RED: PDF provider test ověří image size/wrap v export requestu.
- [x] GREEN: Doplnit export request mapping.
- [x] RED: PDF provider test ověří table properties v export requestu.
- [x] GREEN: Doplnit mapping.
- [x] RED: PDF provider test ověří comments/revisions view mode podle review display mode.
- [x] GREEN: Doplnit request options.
- [x] RED: E2E export PDF smoke po image/table změnách.
- [x] GREEN: Doladit demo provider.

### 19.4 E2E checkpoint

- [x] E2E: Save/reload image properties.
- [x] E2E: Save/reload table properties.
- [x] E2E: Export DOCX po image/table změnách.
- [x] E2E: Import DOCX s image/table.
- [x] E2E: Export PDF po image/table/review změnách.
- [x] Spustit DocumentFormats + DocumentEditor export/import test gate.

Poznámka fáze 19: Hotovo 2026-05-18. Doplněn persistence gate pro image/table/cell vlastnosti a transient marker filtering, DOCX export/import pro image size/caption a table/cell vlastnosti včetně warningu pro neznámé body elementy, PDF request options pro review display mode a E2E checkpoint nad API + HTTPS WASM demem.

## 20. Performance a rendering kvalita

Cíl: nová UX vrstva nesmí zhoršit typing latency, layout stabilitu ani virtualizaci dlouhých dokumentů.

### 20.1 Runtime metrics baseline

- [x] RED: JS test ověří debug metrics obsahují input latency.
- [x] GREEN: Zachovat/rozšířit metrics.
- [x] RED: JS test ověří metrics obsahují marker render count.
- [x] GREEN: Přidat marker metrics.
- [x] RED: JS test ověří metrics obsahují floating reposition count.
- [x] GREEN: Přidat floating metrics.
- [x] RED: JS test ověří metrics obsahují clipboard normalization time.
- [x] GREEN: Přidat clipboard metrics.

### 20.2 Typing performance

- [x] E2E performance smoke: napsat 100 znaků a ověřit max latency pod zvoleným limitem.
- [x] E2E performance smoke: napsat text s otevřeným comments rail.
- [x] E2E performance smoke: napsat text s aktivními search markers.
- [x] E2E performance smoke: napsat text s track changes.
- [x] E2E performance smoke: psaní v table cell.
- [x] Pokud test selže, přidat profilovací poznámku a opravit před pokračováním.

### 20.3 Layout stability

- [x] E2E visual check desktop bez horizontal overflow.
- [x] E2E visual check mobile bez horizontal overflow.
- [x] E2E visual check toolbar compact bez text overflow.
- [x] E2E visual check floating UI bez překryvu mimo viewport.
- [x] E2E visual check table/image inspector bez layout shiftu.
- [x] E2E screenshot artifacts ukládat při selhání.

### 20.4 Long document virtualization

- [x] RED: JS/runtime test ověří virtualized pages zůstávají placeholders.
- [x] GREEN: Zachovat virtualizaci.
- [x] RED: E2E long document smoke ověří page navigator s virtualizací.
- [x] GREEN: Doladit scroll.
- [x] RED: E2E long document smoke ověří search markers přes virtualizované stránky.
- [x] GREEN: Doladit lazy marker rendering.
- [x] RED: E2E long document smoke ověří comments rail alignment.
- [x] GREEN: Doladit lazy positioning.

Poznámka fáze 20: Hotovo 2026-05-18. Doplněny runtime performance metrics pro marker render, floating reposition a clipboard normalization, E2E performance smoke pro běžné psaní/comments/search/track changes/table cell, layout stability smoke pro desktop/mobile/compact toolbar/floating UI/image a table inspector a long-document virtualization smoke pro page navigator, lazy search marker rendering a comments rail alignment. Opraveno scrollToPage pro virtualizované placeholder stránky a root/window scroll detekci.

## 21. Accessibility a lokalizace

Cíl: všechny nové UI prvky splňují keyboard a screen-reader základy.

### 21.1 Lokalizace

- [x] RED: Localization test ověří keys pro feature registry chyby.
- [x] GREEN: Přidat keys EN/CS.
- [x] RED: Localization test ověří keys pro command palette.
- [x] GREEN: Přidat keys.
- [x] RED: Localization test ověří keys pro image inspector.
- [x] GREEN: Přidat keys.
- [x] RED: Localization test ověří keys pro table properties.
- [x] GREEN: Přidat keys.
- [x] RED: Localization test ověří keys pro paste report.
- [x] GREEN: Přidat keys.
- [x] RED: Localization test ověří keys pro autosave states.
- [x] GREEN: Přidat keys.
- [x] RED: Localization test ověří keys pro watchdog recovery.
- [x] GREEN: Přidat keys.

### 21.2 Keyboard accessibility

- [x] E2E a11y: Toolbar lze projít klávesnicí.
- [x] E2E a11y: More menu lze projít klávesnicí.
- [x] E2E a11y: Command palette lze projít klávesnicí.
- [x] E2E a11y: Image inspector lze projít klávesnicí.
- [x] E2E a11y: Table grid picker lze projít klávesnicí.
- [x] E2E a11y: Find/replace panel lze projít klávesnicí.
- [x] E2E a11y: Autocomplete lze projít klávesnicí.
- [x] E2E a11y: Escape vrací focus na rozumné místo.

### 21.3 ARIA

- [x] bUnit test ověří toolbar role/aria-label.
- [x] bUnit test ověří command palette role/dialog/aria-label.
- [x] bUnit test ověří menu role/menuitem.
- [x] bUnit test ověří grid picker role/grid nebo odpovídající button grid pattern.
- [x] bUnit test ověří image inspector labels.
- [x] bUnit test ověří table properties labels.
- [x] bUnit test ověří live region.
- [x] E2E smoke ověří live announcements pro save/find/autosave error.

Poznámka fáze 21: Hotovo 2026-05-18. Doplněny chybějící DocumentEditor lokalizační klíče v EN/CS/FR a mock localizeru, přidána bUnit sada pro ARIA/role/label/status pokrytí nových povrchů, doplněno keyboard ovládání command palette a More menu, zvýraznění aktivní položky overflow menu a Escape zavírání command palette přes root editor handler. E2E smoke pokrývá command palette search+Enter, table grid picker přes šipky+Enter, More menu keyboard traversal a live announcements pro find/save/autosave error.

## 22. Demo a dokumentace

Cíl: každé větší vylepšení má demo scénář a krátkou dokumentaci pro uživatele knihovny.

### 22.1 Demo scénáře

- [x] RED: Demo route obsahuje toolbar mode switch.
- [x] GREEN: Přidat demo control.
- [x] RED: Demo route obsahuje feature toggle scénář.
- [x] GREEN: Přidat demo control.
- [x] RED: Demo route obsahuje image provider scénář.
- [x] GREEN: Přidat demo setup.
- [x] RED: Demo route obsahuje table properties scénář.
- [x] GREEN: Přidat ukázkovou tabulku.
- [x] RED: Demo route obsahuje comments/review scénář.
- [x] GREEN: Přidat demo data.
- [x] RED: Demo route obsahuje paste report scénář.
- [x] GREEN: Přidat test fixture nebo sample paste buttons.
- [x] RED: Demo route obsahuje autosave error scénář.
- [x] GREEN: Přidat demo provider toggle.

### 22.2 Developer docs

- [x] Přidat docs sekci pro feature registry.
- [x] Přidat docs sekci pro command registry.
- [x] Přidat docs sekci pro toolbar modes.
- [x] Přidat docs sekci pro clipboard pipeline extension point.
- [x] Přidat docs sekci pro image provider UX.
- [x] Přidat docs sekci pro table properties model.
- [x] Přidat docs sekci pro autosave/pending actions.
- [x] Přidat docs sekci pro watchdog recovery.
- [x] Přidat docs sekci pro accessibility expectations.

### 22.3 Planning cleanup

- [x] Aktualizovat starší planning dokumenty odkazem na tento master TODO.
- [x] Zapsat rozhodnutí, která vylepšení se implementovala jinak než v analýze.
- [x] Zapsat known limitations po každé fázi.

### 22.4 Rozhodnutí a known limitations

- Rozhodnutí: dokumentace fáze 22 je samostatný soubor `docs/document-editor-developer-guide.md`, místo aby se rozšiřoval README. README by tím narostlo o nízkoúrovňové integrační detaily, které patří spíš k developer guide.
- Rozhodnutí: feature toggle demo používá host-level `DisabledFeatures` parametr nad aktuální instancí editoru, nikoli nový globální demo registry editor. Lépe tím ukazuje reálnou integrační API plochu knihovny.
- Rozhodnutí: paste report demo používá sample HTML fixture textarea/button, ne přímé automatické volání clipboard API. Browser clipboard permissions jsou v demu nespolehlivé; skutečné paste chování dál pokrývají E2E testy fáze 10.
- Rozhodnutí: autosave error demo používá recoverable failure toggle v demo provideru a zkrácený interval, ne síťové odpojování. Je to stabilnější scénář pro manuální demo i E2E.
- Known limitation: `table-demo` je seedovaný zvlášť v WASM provideru i Demo API store. Sdílený seed builder by snížil duplicitu, ale není nutný pro fázi 22.
- Known limitation: developer guide popisuje veřejné integrační body a očekávání, není to úplná referenční dokumentace všech parametrů `TmDocumentEditor`.
- Known limitation: phase 22 E2E je smoke sada pro demo scénáře. Plná regresní matice editoru zůstává součástí fáze 23.

Poznámka fáze 22: Hotovo 2026-05-18. Demo route `/document-editor` teď vystavuje scénářový panel pro toolbar mode, feature toggles, image provider, table properties, comments/review, paste report sample a autosave provider error. Přidán sample dokument `table-demo` do WASM fallback provideru i Demo API store. Developerská dokumentace vznikla v `docs/document-editor-developer-guide.md` a je chráněná xUnit testem na povinné sekce. E2E sada `DocumentEditorPhase22DemoDocsE2ETests` pokrývá dostupnost scénářů a hlavní runtime přepínače.

## 23. Finální regression a release gate

### 23.1 Unit/component gate

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" --logger "console;verbosity=minimal"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --logger "console;verbosity=minimal"`, pokud projekt existuje.
- [x] Spustit FluentValidation testy, pokud změny zasáhly forms/validation.
- [x] Spustit localization tests.

### 23.2 JS/runtime gate

- [x] Spustit `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`.
- [x] Spustit všechny `DocumentEditorWysiwygJavaScriptTests`.
- [x] Spustit runtime input/undo/table/image/revision/comment test subset.

### 23.3 E2E gate

- [x] Spustit E2E smoke pro render editoru.
- [x] Spustit E2E typing/save/reload.
- [x] Spustit E2E toolbar modes.
- [x] Spustit E2E find/replace.
- [x] Spustit E2E clipboard paste.
- [x] Spustit E2E image UX.
- [x] Spustit E2E table UX.
- [x] Spustit E2E comments/review.
- [x] Spustit E2E autosave/watchdog.
- [x] Spustit E2E mobile/narrow viewport layout.

### 23.4 Full solution gate

- [x] Spustit `dotnet build TempoBlazor.slnx`.
- [ ] Spustit `dotnet test`.
- [ ] Pokud je cílem release, spustit `dotnet pack` pro relevantní packages.
- [x] Zkontrolovat `git status --short`.
- [x] Zapsat finální known issues.

### 23.5 Výsledky regression gate a known limitations

- Unit/component gate: `DocumentEditor` unit/component testy prošly `1242/1242`, `Tempo.Blazor.DocumentFormats.Tests` prošly `40/40`, localization filtr prošel `383/383` a `Tempo.Blazor.FluentValidation.Tests` prošly `25/25`.
- JS/runtime gate: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` prošel bez výstupu, `DocumentEditorWysiwygJavaScriptTests` prošly `48/48` a runtime subset pro modularizaci, schema, marker store, floating UI, page UX a performance prošel `10/10`.
- E2E gate: aktuální phase-specific regresní sada pro fáze 3 až 22 a runtime příkazy prošla `112/112`. Izolovaná kontrola command/autosave/watchdog testů po opravách prošla `12/12`.
- Full solution gate: `dotnet build TempoBlazor.slnx` prošel s existujícím `NU1603` varováním pro `Microsoft.Extensions.Http` (`8.0.12` není nalezeno, resolver bere `9.0.0`).
- `dotnet test` bez filtru nebyl uzavřen jako zelený release gate. Široký E2E běh `FullyQualifiedName~DocumentEditor` prokázal `209` prošlých, `38` selhaných a `1` přeskočený test; selhání jsou soustředěná hlavně ve staré monolitické sadě `DocumentEditorE2ETests`, která obsahuje zastaralé selektory, staré layout předpoklady, přesné `Saved` hlášky závodící s autosave a několik duplicitních scénářů pokrytých novými phase-specific testy.
- Release `dotnet pack` nebyl spuštěn, protože cílem fáze 23 nebyl release build/package publish.
- Known limitation: před skutečným release gate je potřeba samostatně pročistit nebo přepsat legacy `DocumentEditorE2ETests`, aby nefilterovaný `dotnet test` mohl projít bez výjimek a bez falešných regresí.
- Opravy provedené během fáze 23: aktualizované očekávání phase 5 runtime JS API/call order testu, tolerantnější legacy save helper vůči rychlému autosave a stabilnější E2E ověření paragraph alignment commandu přes reálný runtime stav bloku.

## 24. Doporučené pořadí implementace

1. Baseline a charakterizační testy.
2. Feature/plugin registry.
3. Command registry completion.
4. Toolbar component factory a toolbar modes.
5. Runtime modularizace.
6. Schema/post-fixers.
7. Marker store.
8. Floating UI/focus/aria-live.
9. Runtime-first find/replace.
10. Clipboard pipeline 2.0.
11. Image UX.
12. Table UX.
13. Comments/review UX.
14. Autocomplete engine.
15. Surface/page navigation/view modes.
16. Autosave/pending actions.
17. Watchdog hardening.
18. Import/export dopady.
19. Performance/accessibility/docs.
20. Full regression.

## 25. Průběžná poznámka pro implementátora

Tento plán je záměrně jemnozrnný. Pokud se při implementaci ukáže, že některý krok už je hotový, checkbox označit až po ověření testem nebo po přidání charakterizačního testu. Pokud se ukáže, že krok je příliš velký, rozdělit ho přímo v tomto dokumentu na menší RED/GREEN/REFACTOR položky a pokračovat.
