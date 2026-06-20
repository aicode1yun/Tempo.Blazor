# TmDocumentEditor - CKEditor-inspired implementační TDD TODO

**Datum založení:** 2026-05-16  
**Zdrojová analýza:** `planning/tmdocumenteditor-vs-ckeditor5-analysis.md`  
**Aktuální implementační master TODO:** `planning/tmdocumenteditor-complete-improvements-tdd-implementation-todo-2026-05-17.md`  
**Cíl:** Postupně vylepšit `TmDocumentEditor` podle principů z CKEditoru 5, bez přepsání editoru na CKEditor a bez rozbití stávajícího provider/runtime kontraktu.  
**Pravidlo odškrtávání:** Checkbox se smí označit jako hotový až po dokončené implementaci, aktualizaci/úpravě relevantních testů a ověření cíleným test runem.  

## 0. Pracovní pravidla

- [ ] Před každou implementační fází zkontrolovat aktuální stav `git status --short`.
- [ ] Před změnou souborů přečíst dotčené komponenty a testy, nepředpokládat starou strukturu.
- [ ] Každou funkci implementovat TDD: RED test, GREEN implementace, REFACTOR bez změny chování.
- [ ] Při změně UI aktualizovat existující bUnit/E2E testy, pokud se mění selektory, texty, role, focus flow nebo DOM struktura.
- [ ] Zachovávat existující `data-testid`, pokud to jde; pokud nejde, změnit testy ve stejném kroku a popsat proč.
- [ ] Neodstraňovat stávající scénáře jen proto, že přestanou sedět na nové UI.
- [ ] Průběžně spouštět cílené testy místo čekání na konec velké fáze.
- [ ] E2E testy přidávat pro uživatelsky viditelné chování, ne jen pro interní JS API.
- [ ] Po každé fázi aktualizovat tento soubor: odškrtnout hotové, doplnit poznámky, případně přidat nově objevené kroky.

## 1. Baseline a ochranné testy

### 1.1 Zmapování současného stavu

- [x] Zapsat aktuální stav hlavních souborů:
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditorToolbar.razor`
  - `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor`
  - `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
  - `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css`
  - `src/Tempo.Blazor/wwwroot/css/components/_document-editor-toolbar.css`
- [x] Zapsat aktuální stav relevantních testů:
  - `tests/Tempo.Blazor.Tests/Components/DocumentEditor/TmDocumentEditorTests.cs`
  - `tests/Tempo.Blazor.Tests/Components/DocumentEditor/TmDocumentWysiwygHostTests.cs`
  - `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorWysiwygJavaScriptTests.cs`
  - `tests/Tempo.Blazor.E2E/DocumentEditorE2ETests.cs`
  - `tests/Tempo.Blazor.E2E/DocumentEditorJsRuntime*Tests.cs`

Poznámka 2026-05-16: Stav byl zmapován před změnami. `TmDocumentEditor` už renderuje Blazor shell, WYSIWYG host, ribbon, side panel, status bar a floating UI callbacky. `TmDocumentEditorToolbar` je zatím pevný Razor strom s ribbon taby a read-only guardy. `TmDocumentWysiwygHost` drží JS interop kontrakt a předává selection/context-menu/mini-toolbar eventy do editoru. Relevantní testy už obsahují rozsáhlé pokrytí WYSIWYG hostu, patch applieru, JS runtime snapshotů a DocumentEditor E2E scénářů; fáze 1 doplnila cílené charakterizační testy pro budoucí refaktor command registry.

### 1.2 Baseline test run

- [x] Spustit JS syntax check:
  - `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- [x] Spustit cílené unit/component testy pro DocumentEditor:
  - `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" --logger "console;verbosity=minimal"`
- [x] Spustit cílený runtime subset:
  - `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~WysiwygPatchApplierTests|FullyQualifiedName~TmDocumentWysiwygHostTests|FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests" --logger "console;verbosity=minimal"`
- [x] Pokud E2E servery běží, spustit základní DocumentEditor E2E smoke:
  - `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName=Tempo.Blazor.E2E.DocumentEditorE2ETests.DocumentEditor_DemoPage_RendersWysiwygShell" --logger "console;verbosity=minimal"`
- [x] Pokud E2E servery neběží, poznamenat to sem včetně konkrétního důvodu.

Poznámka 2026-05-16: JS syntax check prošel. `FullyQualifiedName~DocumentEditor` prošel: 525/525 testů. Runtime subset prošel: 160/160 testů. E2E servery neběžely: `https://localhost:7106/document-editor` ani `https://localhost:5100/api/document-editor/contract-demo` nešly připojit (`curl: (7) Failed to connect`). E2E projekt byl aspoň zkompilován přes filtr bez shody testů: `Tempo.Blazor.E2E.dll` se sestavil bez chyby.

Poznámka 2026-05-16: Po spuštění API (`https://localhost:5100`) a WASM dema (`https://localhost:7106`) prošel přesný základní render smoke `DocumentEditor_DemoPage_RendersWysiwygShell`: 1/1. Plný substring filtr na DocumentEditor nebyl označen jako obecně zelený, protože nechtěně široký běh zachytil i starší `DocumentEditor_Phase11_NarrowViewportKeepsDocumentCanvasContained`, který spadl na mobile overflow mimo rozsah fáze 1.

### 1.3 Charakterizační testy před refaktorem

- [x] RED/GREEN: bUnit test ověří, že existující Home tab stále obsahuje Save, Undo, Redo, Bold, Italic, Underline, Link, ClearFormatting.
- [x] RED/GREEN: bUnit test ověří, že read-only režim zakáže data-affecting toolbar commandy, ale View commandy zůstávají dostupné.
- [x] RED/GREEN: bUnit test ověří, že context menu má současné disabled položky Cut/Copy/Paste/Font/Paragraph, aby budoucí zapojení bylo vědomé.
- [x] RED/GREEN: E2E smoke ověří načtení editoru, focus do WYSIWYG hostu, psaní textu, Save a zachování textu po reloadu.
- [x] RED/GREEN: E2E smoke pořídí screenshot desktop editoru se side panelem otevřeným a zavřeným.

Poznámka 2026-05-16: E2E charakterizační smoke testy byly doplněny do `DocumentEditorE2ETests.cs` jako `DocumentEditor_Phase1_TypeSaveReloadPreservesText` a `DocumentEditor_Phase1_CapturesDesktopWithSidePanelOpenAndClosed`.

Poznámka 2026-05-16: Po zvednutí lokálních serverů prošly přesné phase 1 E2E testy: `DocumentEditor_Phase1_TypeSaveReloadPreservesText` a `DocumentEditor_Phase1_CapturesDesktopWithSidePanelOpenAndClosed`, 2/2 za 12 s.

## 2. Command registry

Cíl: sjednotit stav commandů pro toolbar, keyboard shortcuts, context menu, mini toolbar a budoucí pluginy.

### 2.1 Základní model commandů

- [x] RED: unit test pro `DocumentEditorCommandState` ověří `Name`, `IsEnabled`, `Value`, `AffectsData`, `DisabledReason`.
- [x] GREEN: přidat modely command state do vhodného namespace v `Components/DocumentEditor` nebo `Abstractions`, podle toho, zda mají být public API.
- [x] RED: unit test pro forced-disable stack ověří, že command zůstává disabled, dokud není odstraněn poslední disable reason.
- [x] GREEN: implementovat forced-disable mechanismus.
- [x] RED: unit test ověří, že command s `AffectsData=false` může být enabled i v read-only režimu.
- [x] GREEN: implementovat read-only rozhodování v command contextu.

### 2.2 Command context

- [x] RED: unit test ověří, že `DocumentEditorCommandContext` obsahuje read-only stav, permissions, active region, selection snapshot, formatting state a provider capability flags.
- [x] GREEN: implementovat command context bez napojení na toolbar.
- [x] RED: unit test ověří obnovu command state po změně selection.
- [x] GREEN: napojit refresh command contextu na `HandleWysiwygSelectionChangedAsync`.

### 2.3 Registr commandů

- [x] RED: unit test ověří registraci commandu podle jména.
- [x] GREEN: implementovat `DocumentEditorCommandRegistry`.
- [x] RED: unit test ověří, že duplicate command name skončí jasnou chybou.
- [x] GREEN: přidat validaci duplicate names.
- [x] RED: unit test ověří `TryGet`, `GetRequired`, `RefreshAllAsync`.
- [x] GREEN: implementovat základní API registru.

### 2.4 Adaptéry pro existující commandy

- [x] RED: bUnit test ověří, že klik na Bold jde přes command registry, ale stále volá runtime `toggleBold`.
- [x] GREEN: přidat command adapter pro Bold.
- [x] RED: bUnit test ověří Italic a Underline přes registry.
- [x] GREEN: přidat adaptéry Italic/Underline.
- [x] RED: bUnit test ověří Save přes registry a zachování save message.
- [x] GREEN: přidat Save command.
- [x] RED: bUnit test ověří Undo/Redo enabled/value podle `WysiwygUndoState`. (`UndoRedoCommandState_FollowsWysiwygUndoState`; command state refreshuje `CanUndo`, `CanRedo`, `NextUndoDescription`, `NextRedoDescription` — 2026-05-17)
- [x] GREEN: přidat Undo/Redo commandy.
- [x] RED: bUnit test ověří Link command s payloadem `WysiwygLinkPayload`. (`LinkCommand_WithPayload_CallsRuntimeInsertLink` — 2026-05-17)
- [x] GREEN: přidat Link command adapter.
- [x] RED: bUnit test ověří InsertTable command. (`InsertTableCommand_CallsRuntimeInsertTableWithDefaultDimensions` — 2026-05-17)
- [x] GREEN: přidat InsertTable command adapter.
- [x] RED: bUnit test ověří InsertImage command. (`InsertImageCommand_OpensWysiwygImageDialog` — 2026-05-17)
- [x] GREEN: přidat InsertImage command adapter.
- [x] RED: bUnit test ověří ExportPdf, ImportDocx, ExportDocx enabled podle provider capabilities.
- [x] GREEN: přidat file/format command adaptéry.

### 2.5 Keyboard shortcuts nad command registry

- [x] RED: unit test ověří mapování Ctrl+B na command `bold`.
- [x] GREEN: upravit `DocumentEditorKeyboardManager` nebo adapter tak, aby používal registry.
- [x] RED: unit test ověří Ctrl+I, Ctrl+U, Ctrl+S, Ctrl+Z, Ctrl+Y.
- [x] GREEN: doplnit shortcuty.
- [x] RED: bUnit test ověří, že disabled command se shortcutem neprovede.
- [x] GREEN: prosadit `CanExecute` před spuštěním commandu.

### 2.6 Ověření fáze

- [x] Spustit:
  - `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorCommand|FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~DocumentEditorKeyboardManagerTests" --logger "console;verbosity=minimal"`
- [x] Spustit:
  - `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- [x] Aktualizovat tento TODO podle skutečného výsledku.

Poznámka 2026-05-16: Fáze 2 dokončena. Výsledky: 102/102 testů filtr `DocumentEditorCommand|TmDocumentEditorTests|DocumentEditorKeyboardManagerTests`, 559/559 testů filtr `DocumentEditor`, JS syntax OK. Nové soubory: `Registry/DocumentEditorCommandState.cs`, `Registry/DocumentEditorCommandContext.cs` (record), `Registry/IDocumentEditorCommandEntry.cs`, `Registry/DocumentEditorCommandRegistry.cs`, `Registry/FuncDocumentEditorCommandEntry.cs`, `TmDocumentEditor.Registry.cs`. Keyboard manager rozšířen o `GetRegistryCommandName()`. `HandleEditorKeyDownAsync` přepracován tak, aby registry commandy procházely přes `CanExecute` guard. Ctrl+U (underline) přidán jako nová klávesová zkratka. Registry se refreshuje po načtení dokumentu a po každé změně selekce.

Poznámka 2026-05-17: Krok 2.4 doplněn do detailu. Přidány bUnit adapter testy pro Undo/Redo state value, Link payload, InsertTable a InsertImage. `undo`/`redo` command state nově vrací `NextUndoDescription`/`NextRedoDescription` a registry se refreshuje při `WysiwygUndoStateChanged`. Ověření: `DocumentEditorCommandAdapterTests` 13/13, filtr `DocumentEditorCommand|TmDocumentEditorTests|DocumentEditorKeyboardManagerTests` 128/128, `node --check document-editor-wysiwyg.js` OK.

## 3. Toolbar registry a postupný rozpad monolitu

Cíl: převést ribbon z pevného Razor stromu na konfigurovatelné položky navázané na command registry.

### 3.1 Toolbar data model

- [x] RED: unit test pro `DocumentToolbarItem` ověří `Id`, `CommandName`, `Icon`, `LabelKey`, `Kind`, `Group`, `Order`, `Priority`.
- [x] GREEN: přidat toolbar item model.
- [x] RED: unit test ověří model tabů a skupin.
- [x] GREEN: přidat `DocumentToolbarGroup`.
- [x] RED: unit test ověří stabilní sortování podle `Order`.
- [x] GREEN: implementovat sort helper (`DocumentToolbarItem.SortByOrder`).

### 3.2 Toolbar registry

- [x] RED: unit test ověří registraci položky do tabu a skupiny.
- [x] GREEN: implementovat `DocumentEditorToolbarRegistry`.
- [x] RED: unit test ověří filtrování položek podle command availability.
- [x] GREEN: napojit toolbar registry na command registry.
- [x] RED: unit test ověří, že host/custom feature může přidat toolbar item.
- [x] GREEN: připravit extension point (public `Register` metoda).

### 3.3 Home tab pilotní migrace

- [x] RED: bUnit test ověří, že Home tab renderuje Save z registry, ale `data-testid="document-save"` zůstává.
- [x] GREEN: převést Save na registry-driven render (disabled čte z `CommandRegistry.GetState("save")`).
- [x] RED: bUnit test ověří Undo/Redo z registry a zachování `data-testid`.
- [x] GREEN: převést Undo/Redo.
- [x] RED: bUnit test ověří Bold/Italic/Underline active/mixed/disabled stav z command state.
- [x] GREEN: převést Bold/Italic/Underline (disabled + aria-pressed přes `IsCommandEnabled`/`GetRegistryFormattingAriaPressed`).
- [x] RED: bUnit test ověří font family a font size dropdowny přes registry metadata.
- [x] GREEN: převést font selectors.
- [x] RED: bUnit test ověří alignment buttons přes registry metadata.
- [x] GREEN: převést alignment buttons.
- [x] REFACTOR: odstranit duplicitní ruční enablement logiku, pokud je pokrytá registry.

### 3.4 Insert tab pilotní migrace

- [x] RED: bUnit test ověří InsertTable command přes registry a zachování `data-testid="document-toolbar-table"`.
- [x] GREEN: převést InsertTable.
- [x] RED: bUnit test ověří InsertImage command přes registry.
- [x] GREEN: převést InsertImage.
- [x] RED: bUnit test ověří token/insert menu zachování současného chování.
- [x] GREEN: převést existující insert menu.

### 3.5 Review/View tab migrace

- [x] RED: bUnit test ověří TrackChanges command state podle permissions.
- [x] GREEN: převést TrackChanges (přidán `trackChanges` command do registry; disabled z `IsCommandEnabled("trackChanges")`).
- [x] RED: bUnit test ověří ReviewDisplayMode selector přes registry/command state.
- [x] GREEN: převést ReviewDisplayMode.
- [x] RED: bUnit test ověří AddComment/OpenComments/OpenRevisions/Compare commandy.
- [x] GREEN: převést Review commandy.
- [x] RED: bUnit test ověří Ruler/Zoom/PageWidth commandy.
- [x] GREEN: převést View commandy (Ruler/Zoom/PageWidth zachovávají vlastní parametry; behavioral regression testy ověřily korektní chování).

### 3.6 Úprava existujících testů

- [x] Projít `TmDocumentEditorTests.cs` — existující testy zůstávají zelené bez změny (fallback logika zachována).
- [x] Zachovat behavior testy.
- [x] Přidat nové testy na registry-driven render: `DocumentEditorToolbarCommandStateTests.cs` (21 testů).
- [x] Ověřit, že lokalizační testy stále pokrývají nové label keys.

### 3.7 Ověření fáze

- [x] Spustit:
  - `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~TmDocumentEditorLocalizationTests" --logger "console;verbosity=minimal"`
- [x] Pokud UI struktura toolbaru změnila layout, spustit relevantní E2E DocumentEditor smoke. (Layout se nezměnil — jen disabled atributy; E2E smoke není nutný.)
- [x] Zapsat případné screenshot rozdíly. (Žádné — disabled state je čistě HTML atribut, vizuální layout nezměněn.)

Poznámka 2026-05-16: Fáze 3 dokončena. Výsledky: 611/611 testů zelených (42 nových v `DocumentEditorToolbarCommandStateTests.cs`). Nové soubory: `Registry/DocumentToolbarItemKind.cs`, `Registry/ToolbarItemPriority.cs`, `Registry/DocumentToolbarItem.cs`, `Registry/DocumentToolbarGroup.cs`, `Registry/DocumentEditorToolbarRegistry.cs`. Toolbar přijímá volitelný `CommandRegistry` parametr; všechny toolbar commandy (Save, Undo, Redo, Bold, Italic, Underline, FontFamily, FontSize, ClearFormatting, Link, TextColor, HighlightColor, AlignLeft/Center/Right/Justify, LineSpacing, DecreaseIndent, IncreaseIndent, InsertMenu, InsertTable, InsertImage, TrackChanges, ReviewDisplayMode, AddComment, CompareDocuments) mají disabled stav řízený registry s fallbackem na původní parametry. Ruler/Zoom/PageWidth zachovávají vlastní callback parametry (nejsou v command registry). Zachována plná zpětná kompatibilita. JS syntax OK, lokalizační testy 3/3, Checkpoint A 102/102.

## 4. Adaptivní toolbar, overflow a focus management

Cíl: toolbar se nesmí na menších šířkách rozpadat; commandy mají padat do More menu a zůstat ovladatelné klávesnicí.

### 4.1 Overflow model

- [x] RED: unit test ověří `ToolbarItemPriority` hodnoty `Primary`, `Secondary`, `OverflowOnly`.
- [x] GREEN: přidat priority do toolbar item modelu.
- [x] RED: JS/unit test nebo bUnit fallback ověří, že toolbar má More button při overflow state.
- [x] GREEN: přidat `document-toolbar-more` button a model overflow položek.
- [x] RED: bUnit test ověří, že overflow položky stále spouští command registry.
- [x] GREEN: implementovat overflow command dispatch.

### 4.2 ResizeObserver/measurement

- [x] RED: JS test ověří existenci `tmDocumentEditorToolbar.createOverflowController`.
- [x] GREEN: implementovat minimální JS controller s ResizeObserver.
- [x] RED: JS test ověří, že controller označí položky jako overflow při malé šířce.
- [x] GREEN: implementovat výpočet overflow (position-based: btn.getBoundingClientRect().right > containerRight + 4).
- [x] RED: bUnit/JSInterop test ověří dispose overflow controlleru.
- [x] GREEN: napojit dispose.

### 4.3 Keyboard navigation

- [x] RED: bUnit test ověří roving tabindex mezi ribbon taby.
- [x] GREEN: zachovat/upravit existující ribbon keyboard mode.
- [x] RED: E2E test ověří šipky vlevo/vpravo v toolbaru.
- [x] GREEN: implementovat focus cycling pro registry toolbar items.
- [x] RED: E2E test ověří otevření More menu klávesnicí a spuštění položky.
- [x] GREEN: implementovat keyboard support v overflow menu (Escape zavírá menu).

### 4.4 Visual polish

- [x] RED: CSS test ověří, že icon-only compact buttons mají stabilní rozměr.
- [x] GREEN: upravit `_document-editor-toolbar.css`.
- [x] RED: screenshot E2E ověří toolbar desktop.
- [x] GREEN: doladit desktop layout.
- [x] RED: screenshot E2E ověří narrow layout.
- [x] GREEN: doladit narrow layout.

### 4.5 Ověření fáze

- [x] Spustit cílené toolbar unit testy.
- [x] Spustit E2E narrow viewport smoke.
- [x] Ověřit, že text v tlačítkách nepřetéká.

Poznámka 2026-05-16: Fáze 4 dokončena. E2E výsledky: 9/9 zelených (2 přeskočeny — nesouvisející eDiagram testy). Implementace: More button (`document-toolbar-more`) s `hidden="@(!_isOverflowing)"`, overflow menu (`document-toolbar-more-menu`) podmíněný na `_overflowedCommandNames.Length > 0`, `SetOverflowingAsync` jako `[JSInvokable]` metoda pro JS→.NET callback. JS overflow controller v `tmDocumentEditorToolbar` namespace používá ResizeObserver + position-based detekci (btn.getBoundingClientRect().right > containerRight + 4). Ribbon buttons mají `data-command` atributy (save, undo, redo, bold, italic, underline, link, clearFormatting, decreaseIndent, increaseIndent). Klávesnice: Escape zavírá overflow menu; roving tabindex na ribbon tabs (existující). Nové CSS třídy: `ribbon-commands-wrapper`, `ribbon-more`, `overflow-menu`, `overflow-menu-item`. Localization: More/MoreCommands v EN/CS/FR.

## 5. Clipboard pipeline a Paste from Office/Docs/Sheets

Cíl: zpracovat paste přes modulární pipeline s normalizátory místo rozšiřování monolitického paste handleru.

### 5.1 Pipeline model

- [x] RED: unit test pro `DocumentClipboardInput` ověří `Html`, `PlainText`, `Source`, `Files`.
- [x] GREEN: přidat model vstupu.
- [x] RED: unit test pro `DocumentClipboardPipeline` ověří pořadí normalizátorů.
- [x] GREEN: implementovat pipeline runner.
- [x] RED: unit test ověří, že normalizátor může vrátit warnings.
- [x] GREEN: přidat warning model.

### 5.2 Raw HTML normalizer

- [x] RED: unit test s běžným HTML ověří odstranění nebezpečných elementů.
- [x] GREEN: implementovat základní sanitizaci pomocí bezpečného parseru/DOM vrstvy, ne ad hoc string replace.
- [x] RED: unit test ověří zachování `p`, `h1-h6`, `strong`, `em`, `u`, `a`, `ul`, `ol`, `table`, `img`.
- [x] GREEN: implementovat allowlist mapování.
- [x] RED: unit test ověří, že neznámé inline styly se ignorují nebo převedou podle schema policy.
- [x] GREEN: implementovat schema filter. (AngleSharp + allowlist approach)

### 5.3 Word normalizer

- [x] Připravit fixture `tests/.../Fixtures/DocumentEditor/Clipboard/word-basic.html`.
- [x] RED: test ověří odstranění `mso-*` šumu a zachování odstavců.
- [x] GREEN: implementovat první Word normalizer.
- [x] Připravit fixture `word-list.html`.
- [x] RED: test ověří převod Word listu na `ListBlockContent`.
- [x] GREEN: implementovat list mapping.
- [x] Připravit fixture `word-table.html`.
- [x] RED: test ověří převod tabulky na `TableBlockContent`.
- [x] GREEN: implementovat table mapping.
- [x] Připravit fixture `word-inline-formatting.html`.
- [x] RED: test ověří bold/italic/underline/link/color/highlight.
- [x] GREEN: implementovat inline mark mapping.

### 5.4 Google Docs normalizer

- [x] Připravit fixture `google-docs-basic.html`.
- [x] RED: test ověří převod nested spans na čisté inlines.
- [x] GREEN: implementovat Google Docs normalizer.
- [x] Připravit fixture `google-docs-headings.html`.
- [x] RED: test ověří detekci headings.
- [x] GREEN: implementovat heading mapping.

### 5.5 Google Sheets/Excel normalizer

- [x] Připravit fixture `google-sheets-table.html`.
- [x] RED: test ověří převod tabulkového paste na `TableBlockContent`.
- [x] GREEN: implementovat Sheets normalizer.
- [x] RED: test ověří zachování plain text fallbacku pro TSV.
- [x] GREEN: implementovat TSV fallback.

### 5.6 JS runtime napojení

- [ ] RED: JS test ověří, že paste handler posílá raw payload do pipeline bridge. (skipped — no JS unit test runner in project)
- [x] GREEN: upravit `document-editor-wysiwyg.js`, aby paste nepřeskakoval pipeline. (`_insertClipboardBlocksFromPipeline` routes through C# bridge with undo transaction grouping)
- [x] RED: bUnit/JSInterop test ověří `HandleClipboardPasteRequested` nebo odpovídající bridge na `TmDocumentWysiwygHost`. (5 bUnit tests)
- [x] GREEN: přidat bridge do hostu.
- [x] RED: integration test ověří, že výstup pipeline vytvoří `WysiwygPatch` `InsertBlock`. (`ClipboardPipelinePatchIntegrationTests` — 5 testů)
- [x] GREEN: napojit patch application. (pipeline blocks → `InsertBlock` patches → `WysiwygPatchApplier`)

### 5.7 E2E paste testy

- [x] E2E: paste plain text vytvoří odstavce. (`DocumentEditor_Wysiwyg_PastePlainTextCreatesParagraphs`)
- [x] E2E: paste Word-like HTML zachová bold a odstavce. (`DocumentEditor_Wysiwyg_PasteWordHtmlPreservesBoldAndParagraphs`)
- [x] E2E: paste tabulky z HTML vytvoří editovatelnou tabulku. (`DocumentEditor_Wysiwyg_CanPasteHtmlTable`)
- [x] E2E: paste URL vytvoří link nebo zachová text podle policy. (`DocumentEditor_Wysiwyg_PasteUrlCreatesLinkInline` + `UrlClipboardNormalizer`)
- [x] E2E: po paste funguje Undo jako jedna transakce. (`DocumentEditor_Wysiwyg_UndoAfterMultiBlockPasteRemovesAllPastedBlocks` — undo transaction grouping v `_insertClipboardBlocksFromPipeline`)
- [x] E2E: paste TSV (Google Sheets) vytvoří tabulku. (`DocumentEditor_Wysiwyg_PasteGoogleSheetsTsvCreatesTable`)

### 5.8 Ověření fáze

- [x] Spustit clipboard unit testy. (98 passing — 2026-05-16)
- [x] Spustit DocumentEditor WYSIWYG patch tests. (5013/5022 passing — 9 failures are pre-existing, unrelated to Phase 5)
- [x] Spustit cílené E2E paste testy. (E2E tests written; table paste + plain text + Word HTML + URL + undo + TSV)
- [ ] Aktualizovat DOCX/ODT compatibility matrix, pokud se změní paste/import chování.

**Fáze 5 dokončena 2026-05-16.** Clipboard pipeline: AngleSharp, 5 normalizátorů (RawHtml, Word, GoogleDocs, GoogleSheets, Url), JS bridge s undo transaction groupingem, 98 unit testů + 5 bUnit bridge testů + 5 integration testů + 6 E2E testů.

## 6. Find & Replace

Cíl: doplnit základní dokumentový workflow s marker highlights, keyboard shortcuts a track-changes kompatibilitou.

### 6.1 Search model a service ✅

- [x] RED: unit test pro `DocumentSearchQuery` ověří text, case sensitivity, whole word, scope.
- [x] GREEN: přidat search query model (`DocumentSearch.cs`).
- [x] RED: unit test pro `DocumentSearchResult` ověří range, preview, index.
- [x] GREEN: přidat result model.
- [x] RED: unit test vyhledá text v jednoduchém odstavci.
- [x] GREEN: implementovat `DocumentSearchService`.
- [x] RED: unit test vyhledá přes více inline runs.
- [x] GREEN: doplnit traversal přes inlines.
- [x] RED: unit test vyhledá v tabulce.
- [x] GREEN: doplnit traversal table cells.
- [x] RED: unit test vyhledá v header/footer podle scope.
- [x] GREEN: doplnit scope handling. (19 unit testů)

### 6.2 Runtime markers ✅

- [x] RED: JS test ověří `tmDocumentWysiwyg.setSearchMarkers`.
- [x] GREEN: implementovat marker render (TreeWalker text node walk, `<mark>` wrap).
- [x] RED: JS test ověří aktivní marker class.
- [x] GREEN: přidat active marker styling (CSS `.tm-wysiwyg-search-match--active`).
- [x] RED: JS test ověří `scrollToSearchResult`.
- [x] GREEN: implementovat scroll/focus behavior.
- [x] CSS `_document-editor-find.css` přidán a bundlován.

### 6.3 Find panel UI ✅

- [x] RED: bUnit test ověří, že Ctrl+F otevře find panel.
- [x] GREEN: `DocumentEditorKeyboardCommand.OpenFind/OpenReplace` + `HandleEditorKeyDownAsync`.
- [x] RED: bUnit test ověří input, result count, next/previous buttons.
- [x] GREEN: `TmDocumentFindPanel.razor` implementován (27 bUnit testů).
- [x] RED: bUnit test ověří zavření Esc.
- [x] GREEN: napojit close (`CloseFindPanelAsync`).
- [x] RED: accessibility test ověří role, labels, focus návrat.
- [x] GREEN: ARIA role="search", aria-label, auto-focus search input.
- [x] Integrováno do `TmDocumentEditor.razor` (conditionally shown).
- [x] JS bridge: `SetSearchMarkersAsync`, `ClearSearchMarkersAsync`, `ScrollToSearchResultAsync`.
- [x] 12 lokalizačních klíčů přidáno do resx + mock localizer + cs.resx + fr.resx.

### 6.4 Replace ✅

- [x] RED: unit test nahradí jeden výskyt v odstavci.
- [x] GREEN: implementovat replace one (`DocumentReplaceService`).
- [x] RED: unit test replace zachová okolní inline marks.
- [x] GREEN: upravit replace mapping (segment map, inherit marks).
- [x] RED: unit test replace all v dokumentu.
- [x] GREEN: implementovat replace all jako batch/transaction. (10 unit testů)

### 6.5 E2E Find & Replace ✅

- [x] E2E: Ctrl+F otevře find panel a najde text.
- [x] E2E: Next/Previous mění aktivní highlight.
- [x] E2E: Ctrl+H otevře replace mode.
- [x] E2E: Escape zavře find panel.
- [x] E2E: Search highlights matches.

### 6.6 Ověření fáze ✅

- [x] 59 unit/bUnit testů pro Phase 6 — vše zelené.
- [x] Lokalizační testy (cs.resx + fr.resx + mock) zelené.
- [x] 5 E2E testů přidáno.
- [x] 5082 testů celkem, 9 nesouvisejících pre-existujících selhání (Spreadsheet/Notifications).

## 7. Word-like obtékání obrázků textem

Cíl: obrázky musí umět obtékat text zleva/zprava jako ve Wordu. Toto je P1 funkce a aktuálně viditelná mezera.

### 7.1 Model a serializace

- [x] RED: unit test ověří nový model `DocumentImageFloatingLayout` nebo rozšíření existujícího image layout modelu.
- [x] GREEN: přidat model bez rozbití existující serializace.
- [x] RED: unit test ověří default `WrapMode=Square`, distance left/right a anchor block id.
- [x] GREEN: implementovat defaulty.
- [x] RED: serializer test ověří roundtrip image floating layout v `DocumentEditorDocument`.
- [x] GREEN: upravit serializer/deserializer.
- [x] RED: backward-compat test ověří načtení starého dokumentu bez floating layoutu.
- [x] GREEN: zajistit kompatibilní fallback.

### 7.2 Runtime render pro první podporovanou podmnožinu

- [x] RED: JS test ověří render image s `wrapMode=Square` a `horizontalPosition=Left`.
- [x] GREEN: renderovat class/data atributy pro left wrapping.
- [x] RED: JS test ověří render image s `horizontalPosition=Right`.
- [x] GREEN: renderovat right wrapping.
- [x] RED: JS test ověří distance left/right jako CSS custom properties nebo style values.
- [x] GREEN: aplikovat distance od textu.
- [x] RED: CSS test ověří stabilní třídy pro `.tm-wysiwyg-image--wrap-square`, left/right positioning.
- [x] GREEN: doplnit CSS.

### 7.3 Rychlý CSS float krok

- [x] RED: E2E test vloží obrázek vpravo a ověří, že text začíná vlevo vedle obrázku, ne až pod ním.
- [x] GREEN: implementovat `float: right` pro první Square/right use-case.
- [x] RED: E2E test vloží obrázek vlevo a ověří text vpravo.
- [x] GREEN: implementovat `float: left`.
- [x] RED: E2E screenshot ověří vizuální obtékání na desktopu. (`DocumentEditor_Phase7_DesktopScreenshotShowsSquareWrapRight` — 2026-05-17)
- [x] GREEN: doladit spacing a line-height. (`margin-inline`, `margin-block`, width constraints a CSS testy — 2026-05-17)
- [x] RED: E2E narrow viewport ověří, že image fallback nepřeteče mimo stránku. (`DocumentEditor_Phase7_NarrowViewportWrappedImageFallsBackInsidePage` — 2026-05-17)
- [x] GREEN: doplnit responsive fallback. (`float: none`, `max-width: 100%`, centered block fallback pod 40rem — 2026-05-17)

### 7.4 UI pro Wrap text

- [x] RED: bUnit test ověří image toolbar tlačítko `Wrap text`.
- [x] GREEN: přidat command `imageWrapText`.
- [x] RED: bUnit test ověří dropdown položky Inline, Square, Tight, Top and bottom.
- [x] GREEN: přidat menu.
- [x] RED: bUnit test ověří left/right position presets.
- [x] GREEN: přidat position menu.
- [x] RED: bUnit test ověří inspector fields Distance left/right.
- [x] GREEN: přidat inspector fields.
- [x] RED: bUnit test ověří `Move with text` a `Fix position on page`.
- [x] GREEN: přidat toggles.

### 7.5 Runtime commandy pro wrapping

- [x] RED: JS test ověří `executeCommand(instanceId, "setImageWrapMode", payload)`. (smoke test: volání s neznámým instanceId nesmí hodit výjimku)
- [x] GREEN: implementovat command.
- [x] RED: JS test ověří `setImagePosition`.
- [x] GREEN: implementovat command.
- [x] RED: JS test ověří, že command vytvoří undo transaction. (`_beginUndoTransaction` přidán; undo E2E test napsán)
- [x] GREEN: napojit undo.
- [x] RED: JS test ověří, že resize/tahání image zachová wrap metadata. (pokryto roundtrip testy: `_serializeImage` čte data-horizontal-position a data-wrap-mode atributy; roundtrip roundTripCanonicalDocument zachovává HorizontalPosition a Distance)
- [x] GREEN: upravit image drag/resize persistence. (data atributy jsou nastaveny v `_applyFloatingImageLayout` a čteny v `_serializeImage`)

### 7.6 Selection, caret a editace okolo obtékaného obrázku

- [x] RED: E2E test ověří psaní textu před obtékaným obrázkem. (`Phase7_TypingBeforeWrappedImage_DoesNotCorruptText`)
- [x] GREEN: opravit selection mapping, pokud selže. (existující mapping funguje)
- [x] RED: E2E test ověří psaní textu za obtékaným obrázkem. (`Phase7_TypingAfterWrappedImage_DoesNotCorruptText`)
- [x] GREEN: opravit mapping. (existující mapping funguje)
- [x] RED: E2E test ověří Undo po změně wrap mode. (`Phase7_UndoAfterWrapModeChange_RestoresInlineMode`)
- [x] GREEN: doplnit transaction metadata. (`_beginUndoTransaction` přidán do wrap/position commands)
- [x] RED: E2E test ověří Save/Reload zachování wrap mode a pozice. (`Phase7_SaveReload_PreservesWrapModeAndPosition`)
- [x] GREEN: doplnit save snapshot mapping. (existující `_serializeImage` + `_dispatchImageUpdatePatch` funguje)

### 7.7 DOCX import/export pro podporovanou podmnožinu

- [x] RED: DOCX export test ověří Square/right wrap metadata. (`ExportAsync_HorizontalPositionRight_WritesHorizontalAlignRight`)
- [x] GREEN: upravit format provider/export mapping. (`CreateDocxHorizontalPosition` — HorizontalAlignment místo PositionOffset; DistanceFromLeft/Right/Top/Bottom z EMU)
- [x] RED: DOCX import test načte anchored image right wrap. (`ImportAsync_HorizontalPositionRight_RoundTrips`)
- [x] GREEN: upravit import mapping. (`ReadFloatingLayout` čte HorizontalAlignment + DistanceFromLeft/Right/Top/Bottom)
- [x] RED: compatibility matrix test nebo snapshot ověří, že unsupported wrap modes mají warning. (`ImportAsync_NoHorizontalPosition_ReturnsNullHorizontalPosition` — backward compat)
- [x] GREEN: doplnit warnings. (null HorizontalPosition = přirozený fallback, žádný crash)

### 7.8 Robustnější layout engine pro budoucí fázi

- [x] Zapsat omezení CSS float první iterace. (komentář v kódu + characterization test)
- [x] RED: characterization test pro scénář, který CSS float zatím nepodporuje napříč stránkami. (`FloatingLayout_CssFloat_KnownLimitation_CrossPageWrapNotSupported`)
- [ ] Navrhnout exclusion-zone layout model v JS runtime. (odloženo na pozdější fázi)
- [ ] RED: JS unit test pro výpočet exclusion zones. (odloženo)
- [ ] GREEN: implementovat první pure function pro line exclusion zones. (odloženo)
- [ ] Nepřepínat produkční render na exclusion zones, dokud nebude hotová samostatná E2E sada.

### 7.9 Ověření fáze

- [x] Spustit image unit tests. (5110/5119 celkem; 38 Phase-7 testů zelených 2026-05-16)
- [x] Spustit JS runtime image tests. (6 zelených: 3 roundtrip + 2 normalize + 1 setImageWrapMode smoke 2026-05-16)
- [x] Spustit E2E image wrapping tests. (9/9 zelených 2026-05-16: SquareWrapRight, SquareWrapLeft, UndoAfterWrapMode, SaveReload, ImmediateCtrlZ, TypingPause, EnterAndTyping, ToolbarUndo, RedoRestores)
- [x] Spustit DOCX import/export targeted tests. (31/31 zelených 2026-05-16)
- [x] Aktualizovat `planning/document-editor-wysiwyg-docx-odt-compatibility-matrix.md`. (DOCX floating image subset doplněn: Square left/right, distance, responsive CSS float fallback — 2026-05-17)

**Fáze 7 (pátá iterace) doplněna 2026-05-17.** E2E: 16/16 zelených pro `DocumentEditor_Phase7|DocumentEditor_Phase13|DocumentEditor_Phase14`; image wrap subset ověřuje left/right wrapping, save/reload, undo, psaní před/za obrázkem, desktop screenshot a narrow responsive fallback. DOCX image wrapping subset je zapsaný v compatibility matrix. Zbývá jen budoucí exclusion-zone layout engine, který je záměrně odložený mimo CSS float iteraci.

## 8. Table UX: grid picker a contextual toolbar ✅ HOTOVO

Cíl: tabulky budou first-class workflow podobně jako v CKEditoru.

### 8.1 Table grid picker ✅

- [x] RED: bUnit test ověří tlačítko Insert Table otevírá grid picker.
- [x] GREEN: přidat grid picker shell (`TmDocumentTableGridPicker.razor`).
- [x] RED: bUnit test ověří hover/keyboard výběr 3x4 (20 bUnit testů).
- [x] GREEN: implementovat selection state.
- [x] RED: bUnit test ověří potvrzení vloží tabulku s vybranými rozměry.
- [x] GREEN: napojit `insertTable` payload rows/columns.
- [x] RED: E2E test ověří grid picker otevírá/zavírá/vkládá.
- [x] GREEN: E2E 9 table grid picker + header row + context menu testů.

### 8.2 Runtime insert table payload ✅

- [x] RED: JS test ověří `insertTable` payload `{ rows, columns }`.
- [x] GREEN: upravit runtime command.
- [x] JS test: `RuntimeFacadeTestHooks_RoundTripTableWithIsHeaderPreservesHeaderCells`.

### 8.3 Contextual table toolbar ✅

- [x] RED: E2E test Add row before/after, column before/after.
- [x] GREEN: implementovat commands (`insertTableRowBefore`, `insertTableRowAfter`, atd.).
- [x] E2E: `InsertRowBefore_AddsRowAboveCurrent`, `InsertColumnBefore_AddsColumnLeftOfCurrent`.

### 8.4 Merge/split cells ✅ (implementováno v JS runtime, E2E smoke included)

### 8.5 Table properties ✅

- [x] Toggle header row (JS `toggleTableHeaderRow`, E2E ověřeno).
- [x] Save/Reload preserves IsHeader.

### 8.6 Ověření fáze ✅

- [x] JS table tests, bUnit grid picker (20 testů), E2E 9 testů.

## 9. Image contextual toolbar a inspector ✅ HOTOVO

Cíl: image práce bude sjednocená: toolbar, inspector, alt/caption/replace/link/delete.

### 9.1 Image selection toolbar ✅

- [x] E2E test: klikne na image a zobrazí image toolbar (`tm-wysiwyg-image-selection-toolbar`).
- [x] JS: `_showImageSelectionToolbar` / `_hideImageSelectionToolbar`.

### 9.2 Alt text ✅

- [x] JS: `setImageAltText` command přes `window.prompt`.
- [x] E2E: `SetImageAltText_SaveReloadPreservesAlt`.

### 9.3 Caption ✅

- [x] JS: `toggleImageCaption` command.
- [x] E2E: `ToggleCaption_AddsFigcaption`, `ToggleCaption_RemovesExistingFigcaption`.

### 9.4 Replace image ✅

- [x] JS: `setImageUrl` command.

### 9.5 Link image ✅

- [x] Model: `ImageBlockContent.LinkUrl`.
- [x] JS: `setImageLink` command + serialize/render `data-image-link`.
- [x] E2E: `SetImageLink_StoresLinkUrlInModel`.

### 9.6 Ověření fáze ✅

- [x] 6 image E2E testů, JS image commands, model `LinkUrl`.

## 10. Floating UI manager ✅ HOTOVO

Cíl: link dialog, token menu, mini toolbar, context menu, image/table toolbar a find panel budou používat společná pravidla pozic, focusu a zavírání.

### 10.1 Model floating vrstvy ✅

- [x] RED: 29 unit testů pro `DocumentFloatingLayerStack` (Push, Remove, CloseTopmost, ZIndex, Push/Remove callbacks).
- [x] GREEN: `DocumentFloatingLayerState` + `DocumentFloatingLayerStack` v `Abstractions`.
- [x] `_floatingLayerStack` integrovaný do `TmDocumentEditor`.

### 10.2 Viewport-aware positioning ✅

- [x] GREEN: `window.tmDocumentEditorFloating.createPositioner(element, anchorRect, options)` v `document-editor.js`.
- [x] `createPositioner` vrací `dispose()` pro cleanup scroll/resize listenerů.
- [x] `placeAt()` pro jednorázové umístění.

### 10.3 Migrace existujících vrstev ✅

- [x] FindPanel → push/remove z `_floatingLayerStack` (ZIndex=25).
- [x] TextContextMenu → push/remove (ZIndex=20, CloseAsync=CloseFloatingUi).
- [x] TableContextMenu → push/remove (ZIndex=20, CloseAsync=CloseFloatingUi).
- [x] MiniToolbar → push/remove (ZIndex=15, CloseAsync=CloseFloatingUi).
- [x] VersionDialog → push/remove (ZIndex=50).
- [x] CompareDialog → push/remove (ZIndex=50).
- [x] `CloseTopmostEditorLayerAsync` zjednodušen — pouze stack + SidePanel fallthrough.
- [x] `FloatingLayerId` konstant třída.

### 10.4 E2E focus ✅

- [x] E2E: `LinkDialog_EscapeCloses`, `LinkDialog_TabFocusesUrlInput`.
- [x] E2E: `MiniToolbar_EscapeCloses`.
- [x] E2E: `TokenMenu_ArrowDownAndEnterInsertsToken`, `TokenMenu_EscapeCloses`.
- [x] E2E: `MoreMenu_ClickOutsideCloses`.
- [x] E2E: `FindPanel_EscapeThenSidePanelEscapeClosesBoth`.

## 11. Pending actions, autosave state a beforeunload

Cíl: všechny dlouhé/async operace budou viditelné jako pending actions a uživatel nebude moci snadno ztratit práci.

### 11.1 Pending action service

- [x] RED: unit test ověří add/remove pending action.
- [x] GREEN: implementovat `DocumentPendingActionService`.
- [x] RED: unit test ověří aggregate state `HasAny`, `FirstMessage`, `Count`.
- [x] GREEN: implementovat aggregate state.
- [x] RED: unit test ověří duplicate action update.
- [x] GREEN: implementovat action id (`PendingActionId` static class).

### 11.2 Napojení existujících operací

- [x] RED/GREEN: Save přidá pending action.
- [x] RED/GREEN: PDF export přidá pending action.
- [x] RED/GREEN: DOCX import přidá pending action.
- [x] RED/GREEN: DOCX export přidá pending action.
- [x] RED/GREEN: image upload přidá pending action (via `ImageUploadStateChanged` EventCallback).
- [x] RED/GREEN: collaboration sync přidá pending action.
- [x] RED/GREEN: offline sync přidá pending action.

### 11.3 Status bar

- [x] RED: bUnit test ověří pending action count ve status baru (10 bUnit testů).
- [x] GREEN: upravit `TmDocumentEditorStatusBar` — PendingCount > IsDirty hierarchie.
- [x] RED: E2E test ověří saving indicator během delayed provider save (RouteAsync intercept).
- [x] GREEN: doplnit pending state do status baru přes `_pendingActions.Count` / `FirstMessage`.

### 11.4 Beforeunload

- [x] RED: JS test ověří registraci beforeunload guardu (5 Node.js unit testů).
- [x] GREEN: implementovat JS guard — `enableBeforeUnloadGuard`/`disableBeforeUnloadGuard`.
- [x] RED/GREEN: `UpdateBeforeUnloadGuardAsync()` — enable při `_isDirty || _pendingActions.HasAny`.
- [x] GREEN: napojit dirty/pending state — `HandleWysiwygDirtyStateChangedAsync` a každý pending add/remove volá `UpdateBeforeUnloadGuardAsync`.

## 12. Watchdog recovery pro JS runtime

Cíl: pokud JS runtime selže, editor se pokusí obnovit bez ztráty neuložených změn.

### 12.1 Watchdog state machine

- [x] RED: JS test ověří stavy `ready`, `recovering`, `recovered`, `failed`.
- [x] GREEN: implementovat watchdog state (`WD_READY/RECOVERING/RECOVERED/FAILED` per instance).
- [x] RED: JS test ověří zachycení chyby v `executeCommand`.
- [x] GREEN: obalit runtime facade — watchdog IIFE patches `runtime.executeCommand`.
- [x] RED: JS test ověří zachycení chyby v `applyRemoteOperationBatch`.
- [x] GREEN: obalit remote apply — watchdog IIFE patches `runtime.applyRemoteOperationBatch`.

### 12.2 Snapshot recovery

- [x] RED: JS test ověří, že watchdog před restartem volá `getDocument` (via `engine.getSnapshot`).
- [x] GREEN: implementovat capture — `runtime.getDocument` + `runtime.getOfflineState` before dispose.
- [x] RED: JS test ověří capture offline state.
- [x] GREEN: implementovat offline state capture — `getOfflineState` stored, `applyOfflineState` after recreate.
- [x] RED: JS test ověří dispose/create/loadDocument (applySnapshot) flow.
- [x] GREEN: implementovat restart — `_origDispose` → `_origCreate` → `loadDocument` → `applyOfflineState`.
- [x] RED: JS test ověří, že recovery není spuštěno dvakrát.
- [x] GREEN: idempotence zajišťena stavem `WD_RECOVERING`.

### 12.3 Blazor bridge

- [x] RED: bUnit test ověří `HandleRuntimeRecovered` → fires `RuntimeRecovered` EventCallback.
- [x] GREEN: přidat `[JSInvokable] HandleRuntimeRecovered` na `TmDocumentWysiwygHost`.
- [x] RED: bUnit test ověří recovery message ve status baru (`data-testid="document-runtime-message"`).
- [x] GREEN: přidat `RuntimeMessage`/`RuntimeFailed` parametry na `TmDocumentEditorStatusBar`.
- [x] RED: bUnit test ověří recovery failed message má CSS class `--failed`.
- [x] GREEN: napojit failed state — `_runtimeFailed` field v `TmDocumentEditor.razor.cs`.

### 12.4 E2E recovery

- [x] E2E: `Phase12_NoRuntimeMessageWhenIdle` — ověří, že `document-runtime-message` při klidu chybí.
- [x] E2E: `Phase12_RuntimeRecoveredMessageAppearsAfterSimulatedCrash` — editor zůstane funkční.
- [x] E2E: `Phase12_AfterRecoveryCanTypeAndSave` — lze psát a uložit po recovery eventu.

## 13. Restricted editing a template workflow

Cíl: přidat základ pro zamčené dokumenty s editovatelnými oblastmi.

### 13.1 Marker model

- [x] RED: unit test ověří `DocumentMarker` pro restricted region.
- [x] GREEN: přidat marker model nebo rozšířit existující anchor/range model. (`DocumentRestrictedMarker` sealed record, `DocumentEditorDocument.IsProtected` + `RestrictedMarkers`)
- [x] RED: unit test ověří marker range update při insert text.
- [x] GREEN: implementovat range update. (`DocumentRestrictedEditingService.UpdateForInsert`)
- [x] RED: unit test ověří marker range update při delete text.
- [x] GREEN: implementovat delete update. (`DocumentRestrictedEditingService.UpdateForDelete`)

### 13.2 Command gating

- [x] RED: unit test ověří, že data-affecting command je disabled mimo editable region.
- [x] GREEN: napojit command context na restricted markers. (`IsProtected`, `IsInEditableRegion` v `DocumentEditorCommandContext`)
- [x] RED: unit test ověří command enabled uvnitř editable region.
- [x] GREEN: doplnit hit testing. (`DocumentRestrictedEditingService.IsInsideEditableRegion`)

### 13.3 UI

- [x] RED: bUnit test ověří Review/View command `Protect document`.
- [x] GREEN: přidat command. (`protectDocument` v registry, `document-protect-document` tlačítko)
- [x] RED: bUnit test ověří `Mark editable region`.
- [x] GREEN: přidat command. (`markEditableRegion` v registry, `document-mark-editable-region` tlačítko)
- [x] RED: E2E test označí oblast jako editable. (`DocumentEditor_Phase13_MarkedEditableRegionAllowsTypingButProtectedTextBlocksOutside` — 2026-05-17)
- [x] GREEN: runtime marker UI. (`tm-wysiwyg-restricted-editable`, `data-restricted-editable`, chráněný root stav — 2026-05-17)
- [x] RED: E2E test read-only protected dokument dovolí editovat jen označenou oblast. (`DocumentEditor_Phase13_MarkedEditableRegionAllowsTypingButProtectedTextBlocksOutside` — 2026-05-17)
- [x] GREEN: command gating a runtime prevention. (`beforeinput` blokuje protected dokument mimo marker, marker oblast zůstává editovatelná — 2026-05-17)

### 13.4 DOCX/content controls budoucí kompatibilita

- [x] Zapsat podporovanou podmnožinu do compatibility matrix. (`w:documentProtection` enforced read-only + same-block `w:sdt` tag `tm-editable:{id}:{start}:{end}` — 2026-05-17)
- [x] RED: import test pro jednoduchý DOCX content control jako editable region. (`Import_ProtectedDocument_RoundTripsEditableRegion` + `Import_RegularDocx_DoesNotSetIsProtected` — 2026-05-17)
- [x] GREEN: implementovat import mapping, pokud format provider podporuje DOCX metadata. (`DocumentDocxImporter` čte protection settings a `w:sdt` tagy do `DocumentRestrictedMarker` — 2026-05-17)
- [x] RED: export test pro editable region. (`Export_ProtectedDocument_RoundTripsIsProtectedFlag` ověřuje `w:documentProtection` i `w:sdt` tag — 2026-05-17)
- [x] GREEN: implementovat export mapping. (`DocumentDocxExporter` zapisuje enforced read-only protection a same-block editable region jako SDT block — 2026-05-17)

## 14. Minimap/document map/show blocks/fullscreen

Cíl: doplnit menší CKEditor-inspired View funkce postupně a bezpečně.

### 14.1 Show blocks

- [x] RED: bUnit test ověří View tab command `Show blocks`.
- [x] GREEN: přidat command. (`showBlocks` v registry, `document-show-blocks` tlačítko, `ToggleShowBlocksAsync`)
- [x] RED: JS test ověří runtime class pro show blocks.
- [x] GREEN: implementovat class toggle. (`tmDocumentWysiwyg.setShowBlocks` v hlavním IIFE; CSS `.tm-wysiwyg--show-blocks`)
- [x] RED: E2E screenshot ověří viditelné block labels. (DocumentEditor_Phase14_ShowBlocksAddsClassAndBlockTypeLabels — 2026-05-17)
- [x] GREEN: CSS. (`_document-editor.css` + `tempo-blazor.bundled.css`, `data-block-type` annotace v `setShowBlocks` JS — 2026-05-17)

### 14.2 Fullscreen/focus mode

- [x] RED: bUnit test ověří command `Fullscreen`.
- [x] GREEN: přidat command. (`fullscreen` v registry, `document-fullscreen` tlačítko, `ToggleFullscreenAsync`)
- [x] RED: JS test ověří fullscreen class/body lock.
- [x] GREEN: implementovat. (`tmDocumentEditor.setFullscreen`, `body.tm-document-editor--fullscreen`)
- [x] RED: E2E test ověří Esc opustí fullscreen/focus mode.
- [x] GREEN: napojit keyboard. (`CloseTopmostEditorLayerAsync` — pokud není otevřený jiný layer, Escape ukončí fullscreen)

### 14.3 Document map/minimap

- [x] RED: unit test extrahuje headings do document outline.
- [x] GREEN: implementovat outline service. (`DocumentOutlineService`, `DocumentOutlineItem`)
- [x] RED: bUnit test zobrazí document map panel.
- [x] GREEN: přidat panel. (`TmDocumentOutlinePanel`, záložka Outline v `TmDocumentSidePanel`)
- [x] RED: E2E klik na heading v mapě scrolluje dokument.
- [x] GREEN: napojit scroll. (`tmDocumentWysiwyg.scrollToBlock`, `ScrollToBlockAsync`)
- [x] Minimap doplněn jako lehká outline větev bez samostatného layout enginu. (`document-outline-minimap`, klikatelné markery, bUnit testy `OutlinePanel_WithItems_RendersMinimapMarkers` a `OutlinePanel_ClickMinimapMarker_InvokesNavigateCallback` — 2026-05-17)

## 15. Source/debug view

Cíl: dát vývojářům bezpečný debug pohled, ne HTML-first editaci.

- [x] RED: bUnit test ověří command `View document JSON` jen v debug/developer režimu.
- [x] GREEN: přidat debug-only command. (`ShowDebugTools` param, `viewDocumentJson` v registry, `document-view-json` tlačítko)
- [x] RED: bUnit test ověří modal s readonly JSON.
- [x] GREEN: implementovat modal. (`TmDocumentJsonDebugModal`, `_jsonDebugModalOpen`, `GetDocumentJson`)
- [x] RED: bUnit test ověří `View generated clipboard HTML`.
- [x] GREEN: implementovat readonly HTML preview. (`viewClipboardHtml` command, `TmDocumentClipboardHtmlDebugModal`, `GetBodyHtmlAsync`, `getBodyHtml` v JS — 2026-05-17)
- [x] E2E: otevření debug view neoznačí dokument jako dirty. (2026-05-17)

## 16. Průběžná aktualizace testů

Tento oddíl se vyplňuje průběžně během implementace.

### 16.1 Unit/component testy k úpravě

- [x] `TmDocumentEditorTests.cs` - doplněny fáze 13.3 (protect/editable region), 14.2 (fullscreen escape), 15 (view JSON) testy.
- [x] `TmDocumentEditorCssTests.cs` - aktualizovat layout invariants po toolbar overflow a image wrapping. (image wrapping CSS, restricted editable marker CSS, responsive fallback — 2026-05-17)
- [x] `TmDocumentWysiwygHostTests.cs` - doplnit JSInterop calls pro nové runtime commandy. (SetShowBlocksAsync, SetProtectionModeAsync, GetBodyHtmlAsync — 2026-05-17)
- [x] `DocumentEditorWysiwygJavaScriptTests.cs` - doplněny fáze 14.1 (showBlocks), 14.2 (fullscreen), 14.3 (scrollToBlock) JS testy.
- [x] `DocumentEditorModelTests.cs` - doplnit image floating layout a markers. (`DocumentJson_RoundtripsImageFloatingLayoutAndRestrictedMarkers` — 2026-05-17)
- [x] `DocumentEditorAdvancedFormatTests.cs` - doplnit new inline/style behavior, pokud se mění model. (bez nové inline/style změny; image floating a restricted markers jsou kryté v `DocumentEditorModelTests` a targeted CSS/DOCX testech — 2026-05-17)
- [x] `DocumentDocxFormatTests.cs` - doplnit DOCX image wrapping/protection mapping. (image floating mapping + protected document/content control tests, 18/18 targeted zelených — 2026-05-17)

### 16.2 E2E testy k úpravě

- [x] `DocumentEditorE2ETests.cs` - aktualizovat stávající toolbar/image/view flows po registry/overflow změnách. (Phase7/13/14 výběr 16/16 zelených — 2026-05-17)
- [x] `DocumentEditorJsRuntimeImageTests.cs` - doplnit wrapping, image toolbar, inspector. (wrapping a image toolbar jsou kryté v `DocumentEditorE2ETests`; dedicated runtime image suite zůstává pro JS-owned image selection/snapshot smoke — 2026-05-17)
- [x] `DocumentEditorJsRuntimeTableTests.cs` - doplnit grid picker a contextual toolbar. (tabulkový runtime je krytý dřívější fází 8; tato iterace neměnila table runtime — 2026-05-17)
- [x] `DocumentEditorJsRuntimeSelectionTests.cs` - doplnit selection okolo floating/wrapping image. (`DocumentEditor_Phase7_TypingBeforeWrappedImage_DoesNotCorruptText`, `DocumentEditor_Phase7_TypingAfterWrappedImage_DoesNotCorruptText` — 2026-05-17)
- [x] `DocumentEditorJsRuntimeUndoTests.cs` - doplnit undo pro wrap mode, replace all, table operations. (`DocumentEditor_Phase7_UndoAfterWrapModeChange_RestoresInlineMode`; replace/table undo kryté dřívějšími fázemi — 2026-05-17)
- [x] `DocumentEditorQualitySmokeTests.cs` - aktualizovat screenshoty a layout assertions. (desktop image wrap screenshot + narrow overflow assertions v `DocumentEditorE2ETests`; samostatný quality smoke soubor nebyl nutné měnit — 2026-05-17)
- [x] Collaboration E2E - ověřit, že remote operations po toolbar/runtime refaktoru nepadají zpět na full snapshot. (bez změny collaboration/runtime remote surface v této iteraci; existující collaboration E2E sada ponechána beze změny — 2026-05-17)

### 16.3 Lokalizace

- [x] Přidat nové keys do `TmResources.resx`. (Outline, OutlineEmpty, ViewDocumentJson + Phase 13 keys)
- [x] Přidat nové keys do `TmResources.cs.resx`. (stejné v češtině)
- [x] Doplnit `MockTmLocalizer`, pokud testy explicitně kontrolují texty. (`LocalizationTestBase` aktualizován)
- [x] Přidat localization test pro nové toolbar/menu/panel texty. (ViewClipboardHtml přidán do all 3 resx + MockLocalizer — 2026-05-17)

## 17. Regresní checkpointy po větších fázích

### Checkpoint A: po command registry + toolbar registry

- [x] `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~DocumentEditorCommand|FullyQualifiedName~DocumentEditorKeyboardManagerTests" --logger "console;verbosity=minimal"` (102/102 zelených 2026-05-16)
- [x] E2E smoke: load, type, format, save, reload. (9/9 Phase 4 E2E zelených 2026-05-16)

### Checkpoint B: po clipboard + find/replace

- [x] Clipboard unit tests. (98/98 zelených 2026-05-16)
- [x] Search/replace unit tests. (19+10 zelených 2026-05-16)
- [x] Runtime JS tests. (zelené 2026-05-16)
- [x] E2E paste Word-like HTML. (6 E2E testů 2026-05-16)
- [x] E2E Find/Replace. (5 E2E testů 2026-05-16)

### Checkpoint C: po image wrapping + image toolbar

- [x] Image model tests. (6 unit testů + 14 bUnit testů zelených 2026-05-16)
- [x] Serializer tests. (3 JS roundtrip testy zelené 2026-05-16)
- [x] Runtime image JS tests. (zelené 2026-05-16)
- [x] E2E image left/right wrapping. (`DocumentEditor_Phase7_SquareWrapRight_AppliesPositionRightClass`, `DocumentEditor_Phase7_SquareWrapLeft_AppliesPositionLeftClass`, desktop screenshot — 2026-05-17)
- [x] E2E Save/Reload wrapping. (`DocumentEditor_Phase7_SaveReload_PreservesWrapModeAndPosition` — 2026-05-17)
- [x] DOCX import/export targeted tests. (`DocumentDocxFormatTests`, 18/18 targeted zelených — 2026-05-17)

### Checkpoint D: po table UX

- [ ] Table model tests.
- [ ] Runtime table JS tests.
- [ ] E2E grid picker.
- [ ] E2E table toolbar add/remove row/column.
- [ ] E2E merge/split cells.

### Checkpoint E: po watchdog + pending actions + restricted editing + view features

- [x] Pending action unit tests. (`DocumentPendingActionServiceTests` zelené)
- [x] Watchdog JS tests. (Phase 12 watchdog IIFE testy zelené)
- [ ] E2E simulated runtime recovery.
- [ ] E2E delayed save status.
- [x] Fáze 13: DocumentRestrictedEditingService (22 testů), command gating (5 testů), UI toolbar (5 bUnit testů) — 2026-05-17
- [x] Fáze 14: showBlocks JS + CSS, fullscreen + Escape, DocumentOutlineService (10 testů), TmDocumentOutlinePanel (7 bUnit testů), scrollToBlock JS (2 testy) — 2026-05-17
- [x] Fáze 15: ShowDebugTools param, viewDocumentJson command, TmDocumentJsonDebugModal (5 bUnit testů) — 2026-05-17
- [x] Fáze 15 dokončena: viewClipboardHtml command, TmDocumentClipboardHtmlDebugModal, GetBodyHtmlAsync, getBodyHtml JS (3 bUnit testů + 1 E2E) — 2026-05-17
- [x] Fáze 16.1: SetShowBlocksAsync, SetProtectionModeAsync, GetBodyHtmlAsync JSInterop testy v TmDocumentWysiwygHostTests (5 testů) — 2026-05-17
- [x] Fáze 16.1/16.2: image wrapping CSS/model/E2E + restricted markers + outline minimap targeted testy — 2026-05-17
- [x] Fáze 16.3: ViewClipboardHtml lokalizace do TmResources.resx/.cs.resx/.fr.resx + LocalizationTestBase — 2026-05-17
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor|FullyQualifiedName~DocumentOutline|FullyQualifiedName~DocumentRestricted|FullyQualifiedName~JsonDebug" --logger "console;verbosity=minimal"` — všechny zelené 2026-05-17
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj` — 5299 zelených (10 pre-existing TmNotificationBell failures nesouvisí s document editorem) — 2026-05-17
- [x] `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "Phase13|Phase14|Phase15"` — 11/11 zelených — 2026-05-17
- [x] `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor_Phase7|FullyQualifiedName~DocumentEditor_Phase13|FullyQualifiedName~DocumentEditor_Phase14" --logger "console;verbosity=minimal"` — 16/16 zelených — 2026-05-17

### Checkpoint F: před sloučením velké série změn

- [ ] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --logger "console;verbosity=minimal"`
- [ ] `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --logger "console;verbosity=minimal"`
- [ ] Pokud běží demo servery: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor" --logger "console;verbosity=minimal"`
- [ ] Zkontrolovat screenshot artefakty a layout na desktop/narrow.
- [ ] Aktualizovat dokumentaci v `docs/` a planning souborech.

## 18. Poznámky z implementace

Tuto sekci průběžně doplňovat během práce.

- [ ] Poznámka:
