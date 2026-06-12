# TmDocumentEditor vs CKEditor 5 - detailni analyza a doporuceni

**Datum:** 2026-05-16  
**Aktuální implementační master TODO:** `planning/tmdocumenteditor-complete-improvements-tdd-implementation-todo-2026-05-17.md`  
**Analyzovane repozitare:**  
- Tempo: `/home/pavel/NetProjects/Tempo.Blazor`  
- CKEditor 5: `/home/pavel/NetProjects/ckeditor5`  

## 1. Shrnutí

`TmDocumentEditor` uz neni jednoduchy blokovy editor. Soucasna implementace je pokrocily document-processing subsystém: Blazor shell, JS-owned WYSIWYG runtime, A4 strankovany povrch, track changes, komentare, side panel, DOCX/PDF provider boundary, offline drafty, audit, collaboration a granularni patch/operation model.

CKEditor 5 je oproti tomu zraly, velmi modularni editorovy framework. Jeho nejvetsi hodnota pro Tempo neni v tom, ze by bylo vhodne prevzit konkretni UI nebo kod, ale v jeho architektonicke discipline:

- pluginy jsou rozdelene na `Editing` a `UI`,
- funkce se registruji pres command registry,
- toolbar se sklada z component factory a umi overflow/grouping,
- clipboard/paste je event pipeline s normalizatory,
- data model ma schema, differ, history, markers a conversion pipelines,
- UI ma jednotny focus tracking, tooltipy, aria-live oznamovani a viewport-aware floating vrstvy,
- watchdog umi editor restartovat po runtime chybe.

Moje hlavni doporuceni: **neprepisovat TmDocumentEditor na CKEditor**, ale prevzit z CKEditoru pet principu:

1. **Plugin/feature registry** pro TmDocumentEditor misto pevne zakodovaneho monolitickeho toolbaru.
2. **Command registry s `CanExecute`, `Value`, `AffectsData` a forced-disable stackem** misto mnoha specializovanych callback parametru.
3. **Clipboard pipeline s normalizatory** pro Word/Google Docs/Google Sheets/HTML/tokeny pred ulozenim do modelu.
4. **Adaptivni toolbar a floating UI system** s overflow groupingem, lazy dropdowny a jednotnym focus managementem.
5. **Watchdog/recovery vrstva** pro JS runtime, ktera umi obnovit editor z posledniho stabilniho snapshotu a offline runtime state.

## 2. Pouzite zdroje v kodu

### Tempo.Blazor

Hlavni soubory:

- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditorToolbar.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css`
- `src/Tempo.Blazor/wwwroot/css/components/_document-editor-toolbar.css`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Interfaces/DocumentEditorProviders.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlocks.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/WysiwygPatch.cs`
- `docs/document-editor-js-owned-runtime.md`

Orientacni velikost:

- `document-editor-wysiwyg.js`: 11 649 radku
- `TmDocumentEditor.razor.cs`: vice nez 5 000 radku
- `TmDocumentEditorToolbar.razor`: 1 385 radku
- `_document-editor.css`: 2 480 radku

### CKEditor 5

Hlavni soubory a balicky:

- `packages/ckeditor5-core/src/plugin.ts`
- `packages/ckeditor5-core/src/command.ts`
- `packages/ckeditor5-core/src/editor/editor.ts`
- `packages/ckeditor5-editor-decoupled/src/decouplededitor.ts`
- `packages/ckeditor5-engine/src/model/document.ts`
- `packages/ckeditor5-engine/src/conversion/downcastdispatcher.ts`
- `packages/ckeditor5-typing/src/input.ts`
- `packages/ckeditor5-clipboard/src/clipboardpipeline.ts`
- `packages/ckeditor5-paste-from-office/src/pastefromoffice.ts`
- `packages/ckeditor5-autosave/src/autosave.ts`
- `packages/ckeditor5-watchdog/src/editorwatchdog.ts`
- `packages/ckeditor5-ui/src/editorui/editorui.ts`
- `packages/ckeditor5-ui/src/toolbar/toolbarview.ts`
- `packages/ckeditor5-ui/src/dropdown/utils.ts`
- balicky: table, image, link, list, mention, find-and-replace, word-count, minimap, fullscreen, show-blocks, source-editing, html-support, restricted-editing, style, special-characters, page-break, bookmark, media-embed.

Poznamka k licenci: CKEditor je dual-license GPL/commercial. Doporuceni nize jsou **konceptualni inspirace**, ne navrh kopirovat zdrojovy kod.

## 3. Aktualni stav TmDocumentEditoru

### 3.1 Silne stranky

**Produktovy rozsah je velmi silny.** `TmDocumentEditor` dnes pokryva veci, ktere jsou v beznych rich-text editorech casto az enterprise/premium vrstva:

- provider boundary pro load/save/verze/komentare,
- PDF export pres `IDocumentPdfExportProvider`,
- DOCX import/export pres `IDocumentFormatProvider`,
- document compare pres `IDocumentComparisonProvider`,
- comments rail a side panel,
- revision/track-changes panel,
- runtime track changes s inline accept/reject,
- offline drafty, conflict banner, local/server conflict actions,
- collaboration provider a remote operation aplikace do JS runtime,
- image provider, clipboard images, asset resolve/commit,
- token provider a template preview,
- audit sink,
- A4-like paginated WYSIWYG surface,
- ruler, zoom, page-width mode,
- status bar s word count/position/save state,
- rozsahla bUnit a Playwright test coverage.

**Architektura live editace jde spravnym smerem.** Dokumentace `docs/document-editor-js-owned-runtime.md` jasne rika, ze JS runtime vlastni zivy DOM, selection, input transakce, formatting commands, image/table interactions, comments/revision decorations. Blazor zustava shell pro provider calls, save/export/import, localization a panely. To je spravne rozhodnuti.

**Provider boundary je silnejsi nez u typickeho embed editoru.** Host aplikace nemusi znat DOM ani HTML. Pracuje se strukturovanym `DocumentEditorDocument`, provider kontrakty a `DocumentOperationBatch`.

**Testovaci zakladna je nadstandardni.** Existuji unit/component/E2E testy pro runtime input, selection, table, image, undo, revision, comment, collaboration, offline, save boundary, PDF/DOCX flows. To je zasadni vyhoda proti integraci ciziho editoru.

### 3.2 Slabsi mista

**Toolbar je monoliticky.** `TmDocumentEditorToolbar.razor` ma pres tisic radku, mnoho parametru a udalosti. Kazda nova funkce znamena dalsi parametr, dalsi callback a dalsi podminku. CKEditor toto resi `componentFactory`, command registry a pluginy.

**UI pusobi misty preplnene a textove.** Ribbon pouziva hodne tlacitek s ikonou i textem. To je vhodne pro discoverability, ale na mensich sirskach vznikne hluk. CKEditor toolbar umi group when full a overflow dropdown. Tempo by melo mit podobny adaptivni model.

**Nektere viditelne commandy jsou jen placeholdery.** V toolbaru jsou tlacitka jako Page Layout, Insert Footnote, Insert Endnote, Table of Contents, ale nejsou napojena na plnohodnotne flows. V context menu jsou Cut/Copy/Paste/Font/Paragraph disabled. UI tim slibuje vic, nez umi dodat.

**Insert flows jsou mene vyzrale nez u CKEditoru.** Table insert je z pohledu uzivatele jednorazove `insertTable`, chybi grid picker 1x1 az napr. 10x10. Image dialog je funkcni, ale produktove jednoduchy: URL/upload/alt, malo properties, malo preview feedbacku.

**Clipboard pipeline neni dost modularni.** Runtime umi paste a sanitizaci, ale CKEditor ma samostatny `ClipboardPipeline` a `PasteFromOffice` normalizatory pro Word, Google Docs a Google Sheets. Pro dokumentovy editor je paste z Wordu/Docs kriticka workflow.

**Chybi Find & Replace.** CKEditor ma samostatny feature package s UI a commands. U dokumentoveho editoru je Ctrl+F/Ctrl+H zakladni ocekavani.

**Chybi watchdog-like recovery.** Tm runtime ma fallback/loading/error states, ale CKEditor ma `EditorWatchdog`, ktery prubezne uklada editor data a pri chybe restartuje editor. Pri 11k radcich vlastniho JS runtime je recovery layer velmi dulezity.

**Focus/accessibility system je roztristeny.** Tempo pouziva ARIA atributy, keyboard manager a lokalizaci, ale CKEditor ma jednotny `FocusTracker`, `FocusCycler`, `TooltipManager`, `AriaLiveAnnouncer`, body collection pro floating UI a viewport offsets. To je pro komplexni editor velky rozdil v kvalite.

## 4. Porovnani architektury

| Oblast | TmDocumentEditor | CKEditor 5 | Doporuceni pro Tempo |
|---|---|---|---|
| Live editace | JS-owned runtime + Blazor shell | Editing engine + model/view/controller | Zachovat vlastni runtime, ale zavest formalni plugin a command vrstvu. |
| Data model | `DocumentEditorDocument`, blocks/inlines, provider contracts | schema-driven model, roots, history, differ, markers | Pridat schema/feature registry nad existujici model, ne bourat provider contract. |
| Commands | C# callbacky + JS `executeCommand` stringy | `editor.commands`, `Command.refresh()`, `isEnabled`, `value`, `affectsData` | Zavest `DocumentEditorCommandRegistry` a command metadata. |
| Toolbar | Pevne Razor markup skupiny | Component factory, toolbar config, grouping | Prevest ribbon na konfiguraci nad commands a adaptivni overflow. |
| Plugins | Funkce jsou rozlezle v shellu/runtime | `Plugin`, `requires`, `init`, `afterInit`, editing/UI split | Zavest `IDocumentEditorFeature` s `Editing`, `Ui`, `Providers`. |
| Clipboard | Vlastni paste handler a sanitize | Clipboard event pipeline, normalizatory | Zavest `DocumentClipboardPipeline` + normalizers. |
| UI floating vrstvy | Link dialog, token popover, mini toolbar, context menus zvlast | Body collection, dropdown utils, viewport offsets | Sjednotit do jednoho floating layer manageru. |
| Undo/dirty | JS runtime undo state + dirty state | model history + undo batches + pending actions | Zachovat runtime undo, doplnit action names/history UI a pending actions. |
| Autosave | `AutoSaveInterval`, save state, offline draft | debounced autosave + pending actions + beforeunload | Doplnit pending action manager a beforeunload warning. |
| Recovery | fallback/error, manual refresh | watchdog restart | Zavest runtime watchdog. |
| Accessibility | ARIA, role, keyboard shortcuts | focus tracker, focus cycler, aria-live | Pridat centralni focus/announce service. |

## 5. Co bych z CKEditoru prevzal

### 5.1 Plugin model: editing/UI split

CKEditor primo doporucuje delit plugin na cast, ktera pracuje s editorem bez UI, a UI cast, ktera pristupuje k `editor.ui`. To je presne vhodne pro Tempo.

Navrh pro Tempo:

```csharp
public interface IDocumentEditorFeature
{
    string Name { get; }
    IReadOnlyList<string> Requires { get; }
    void ConfigureSchema(DocumentEditorSchemaBuilder schema);
    void RegisterCommands(DocumentEditorCommandRegistry commands);
    void RegisterToolbar(DocumentEditorToolbarRegistry toolbar);
    void RegisterSerializers(DocumentEditorSerializationRegistry serializers);
}
```

Priklady feature:

- `DocumentTextFeature`
- `DocumentParagraphFeature`
- `DocumentTableFeature`
- `DocumentImageFeature`
- `DocumentLinkFeature`
- `DocumentReviewFeature`
- `DocumentCommentsFeature`
- `DocumentClipboardFeature`
- `DocumentFormatImportExportFeature`
- `DocumentFindReplaceFeature`
- `DocumentRestrictedEditingFeature`

Prinos:

- mensi soubory,
- snazsi testovani,
- host muze vypnout funkce,
- toolbar se sklada podle dostupnych funkci,
- jednodussi permissions/read-only handling.

### 5.2 Command registry podle CKEditor `Command`

CKEditor `Command` ma `value`, `isEnabled`, `refresh()`, `affectsData` a forced disable stack. To je pro editor zasadni.

Navrh pro Tempo:

```csharp
public abstract class DocumentEditorCommand
{
    public string Name { get; init; } = "";
    public bool AffectsData { get; init; } = true;
    public bool IsEnabled { get; protected set; }
    public object? Value { get; protected set; }
    public abstract ValueTask RefreshAsync(DocumentEditorCommandContext context);
    public abstract ValueTask ExecuteAsync(DocumentEditorCommandContext context, object? payload = null);
}
```

Priklady commands:

- `bold`, `italic`, `underline`
- `fontFamily`, `fontSize`, `fontColor`, `highlight`
- `paragraphAlignment`, `lineSpacing`, `indent`
- `insertTable`, `insertImage`, `insertPageBreak`
- `find`, `replace`, `replaceAll`
- `addComment`, `acceptRevision`, `rejectRevision`
- `save`, `exportPdf`, `exportDocx`, `importDocx`
- `toggleRuler`, `zoomIn`, `zoomOut`, `fullscreen`

Prinos:

- toolbar, keyboard shortcuts, context menu a mini toolbar sdili stejny zdroj pravdy,
- disabled state je konzistentni,
- read-only rezim nemusi mit specialni logiku v kazdem tlacitku,
- command value muze primo ridit aktivni stav toolbaru.

### 5.3 Component factory pro toolbar

CKEditor toolbar neni pevny HTML strom, ale skladacka z registrovanych UI komponent. Tempo by melo mit podobny registry-driven ribbon.

Navrh:

```csharp
public sealed class DocumentToolbarItem
{
    public string Id { get; init; } = "";
    public string CommandName { get; init; } = "";
    public string Icon { get; init; } = "";
    public string LabelKey { get; init; } = "";
    public DocumentToolbarItemKind Kind { get; init; }
    public string Group { get; init; } = "";
    public int Order { get; init; }
}
```

Pak `TmDocumentEditorToolbar` nerenderuje rucne kazde tlacitko, ale iteruje:

- tabs,
- groups,
- items,
- overflow behavior,
- permissions.

Prinos:

- host aplikace muze toolbar upravit,
- vlastni feature muze pridat tlacitko,
- stejne command metadata se pouzije pro menu, context menu i shortcuts.

### 5.4 Adaptivni toolbar / overflow

CKEditor `ToolbarView` umi grouping pri nedostatku sirky a focus cycling. Tempo ribbon ted wrapuje skupiny a na mobile se texty schovavaji CSS, ale chybi skutecny overflow model.

Navrh:

- meritelny toolbar layout v JS nebo ResizeObserver,
- prioritizace itemu: primary, secondary, overflow-only,
- "More" dropdown pro prebytecne commandy,
- compact mode pro narrow widths,
- lazy rendering obsahu dropdownu az po otevreni,
- jednotna navigace sipkami v toolbaru.

UX prinos:

- editor bude pouzitelny v side-pane layoutu i na notebooku,
- mensi vizualni hluk,
- nemusi se obetovat dostupnost funkci.

### 5.5 Clipboard pipeline a Paste from Office

CKEditor ma jasnou pipeline:

1. native paste/drop,
2. ziskani `text/html` / `text/plain`,
3. normalizace raw dat,
4. view fragment,
5. model fragment,
6. content insertion.

Tempo by melo zavest podobnou pipeline:

```text
paste/drop
  -> ClipboardInput
  -> RawHtmlNormalizer
  -> OfficeNormalizer
  -> GoogleDocsNormalizer
  -> GoogleSheetsNormalizer
  -> HtmlToDocumentFragment
  -> SchemaFilter
  -> InsertContentCommand
```

Prioritni normalizatory:

- Word: `mso-*` styly, listy, tabulky, komentarove/track-changes artefakty,
- Google Docs: inline styly, nested spans, pseudo headings,
- Google Sheets/Excel: tabulkova data do `TableBlockContent`,
- plain text: odstavce, seznamy, URL autolink,
- Tempo tokens: zachovat tokeny jako special inline.

Prinos:

- paste z Wordu nebude degradovat dokument,
- import/export DOCX boundary dostane lepsi konzistenci,
- clipboard logika se nebude rozrustat v jednom JS souboru.

### 5.6 Conversion pipelines

CKEditor ma upcast/downcast dispatchery a converters. Tempo ma serializatory a patch appliers, ale chybi formalni extension point pro "jak se muj block/inline prevadi do runtime DOM, provider JSON, clipboard HTML, export HTML".

Navrh registru:

- `DocumentModelToDomConverter`
- `DomToDocumentModelConverter`
- `ClipboardHtmlToModelConverter`
- `ModelToClipboardHtmlConverter`
- `ModelToProviderDocumentConverter`
- `ProviderDocumentToModelConverter`

Pro kazdou feature:

- table converter,
- image converter,
- link converter,
- comment marker converter,
- revision marker converter,
- token converter.

Prinos:

- mensi riziko regresi u tabulek/obrazku/revizi,
- lepsi roundtrip testy,
- moznost pridat nove block typy bez editace runtime monolitu.

### 5.7 Markers pro komentare, revize, search a restricted editing

CKEditor pouziva markers pro rozsahy v modelu. Tempo uz ma komentare/revize/suggestions, ale doporucuji sjednotit je pod explicitni `DocumentMarker` model:

```csharp
public sealed class DocumentMarker
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = ""; // comment, revision, search, restricted, bookmark
    public DocumentRange Range { get; init; } = new();
    public Dictionary<string, string> Data { get; init; } = [];
}
```

Prinos:

- comments, revisions, search highlights, bookmarks a restricted regions maji stejnou range mechaniku,
- lepsi collision handling pri editaci,
- jednodussi rendering dekoraci.

### 5.8 Pending actions + autosave state

CKEditor `Autosave` pouziva stav `synchronized`, `waiting`, `saving`, `error`, pending actions a `beforeunload`. Tempo uz ma dirty/save message a offline drafty, ale pending action model by UX zprehlednil.

Navrh:

- `DocumentPendingActionService`,
- pending actions: Save, Export, Import, ImageUpload, CollaborationSync, OfflineSync,
- status bar ukazuje aktivni akce,
- browser close warning, pokud pending actions nebo dirty runtime,
- retry policy s backoffem pro autosave.

### 5.9 Watchdog recovery

CKEditor `EditorWatchdog` prubezne uklada data a pri chybe restartuje editor. Pro `document-editor-wysiwyg.js` s 11k radky je to vysoka priorita.

Navrh pro Tempo:

- JS runtime watchdog obali create/load/executeCommand/applyRemoteOperationBatch,
- pri nechycenem runtime erroru:
  - ulozi `getDocument()` a `getOfflineState()`,
  - dispose instance,
  - create nova instance,
  - load posledni stabilni snapshot,
  - apply offline runtime state,
  - oznami status v Blazor shellu,
  - zachova dirty flag.

UX:

- uzivatel neuvidi "editor spadl, obnov stranku",
- maximalne kratka hlaska ve status baru: "Editor byl obnoven, neulozene zmeny zustaly zachovane."

### 5.10 Find & Replace

CKEditor ma vlastni package pro find-and-replace s commands `find`, `findNext`, `findPrevious`, `replace`, `replaceAll` a marker highlights.

Tempo by melo pridat:

- Ctrl+F otevira find panel,
- Ctrl+H otevira replace mode,
- vysledky jsou markers v dokumentu,
- navigace next/previous,
- replace respektuje track changes,
- replace all je jedna undo transakce,
- search scope: body/header/footer/comments volitelne.

Tohle je pro dokumentovy editor velmi viditelna mezera.

### 5.11 Table UX z CKEditoru

CKEditor table feature ma commandy pro:

- insert row/column,
- remove row/column,
- merge/split cells,
- header row/column,
- select row/column,
- table/cell properties,
- contextual table toolbar.

Tempo uz ma table model vcetne colspan/rowspan a context menu pro add/delete row/column. Doporuceni:

- grid picker v Insert tab,
- floating table toolbar nad tabulkou,
- cell selection model,
- merge/split cells UI,
- row/column resize handles,
- header row/column toggle,
- table properties panel,
- paste z Excel/Sheets do tabulky.

### 5.12 Image UX z CKEditoru

CKEditor image feature je granularni: insert, upload, caption, alt text, styles, resize, link image, toolbar.

Tempo uz umi upload/provider/clipboard, resizing, floating/wrap mode. Doporuceni:

- image contextual toolbar primo u obrazku,
- skutecne Word-like obtekani textem vlevo i vpravo od obrazku,
- alt text dialog jako rychla accessible akce,
- caption toggle,
- image replace,
- image link,
- style presets: Inline, Centered, Side, Full width,
- wrap presets: In line with text, Square, Tight, Through, Top and bottom, Behind text, In front of text,
- position presets: left/right/center relativne ke strance, marginu, odstavci nebo kotve,
- viditelna kotva obrazku a volby `Move with text` / `Fix position on page`,
- drag handles + size input v properties panelu,
- loading/error overlay s retry.

Cast toho v Tempu uz existuje, ale UI by melo byt sjednocene a dostupne pres toolbar/contextual toolbar, ne jen pres jednoduche dialogy.

#### 5.12.1 Word-like obtekani obrazku textem

Tohle je konkretni mezera proti Wordu: obrazek muze byt vizualne "floating", ale editor dnes neumi spolehlive vysazet okolni odstavcovy text tak, aby obrazek obtikal zleva nebo zprava stejne jako ve Wordu. Pro dokumentovy editor je to dulezitejsi nez obecne image styling presets, protoze jde o beznou smluvni, nabidkovou a reportovou sazbu.

Cilove chovani:

- obrazek muze byt inline soucast textu, nebo floating objekt ukotveny k odstavci,
- text se umi obtacet okolo obrazku z jedne nebo obou stran podle zvoleneho wrap modu,
- pri umisteni obrazku vlevo text tece vpravo od obrazku,
- pri umisteni obrazku vpravo text tece vlevo od obrazku,
- pri Square/Tight wrap se respektuje vzdalenost textu od obrazku,
- pri Top and bottom text netece po stranach, ale az nad/pod obrazkem,
- pri Behind/In front of text se obrazek chova jako vrstva vuci textu,
- pri tazeni/resizu obrazku se wrapping prepocita bez ztraty caret/selection,
- obtekani musi fungovat i pri exportu/importu DOCX pro podporovanou podmnozinu.

Navrh modelu:

```csharp
public sealed class DocumentImageFloatingLayout
{
    public DocumentImageWrapMode WrapMode { get; set; } = DocumentImageWrapMode.Square;
    public DocumentImageHorizontalPosition HorizontalPosition { get; set; } = DocumentImageHorizontalPosition.Right;
    public DocumentImageVerticalPosition VerticalPosition { get; set; } = DocumentImageVerticalPosition.Paragraph;
    public double DistanceLeft { get; set; } = 9;
    public double DistanceRight { get; set; } = 9;
    public double DistanceTop { get; set; }
    public double DistanceBottom { get; set; }
    public string AnchorBlockId { get; set; } = "";
    public bool MoveWithText { get; set; } = true;
    public bool LockAnchor { get; set; }
}
```

Implementacni poznamka:

- Samotne CSS `float: left/right` muze byt rychly prvni krok pro jednoduche Square obtekani v ramci jednoho page body, ale nestaci pro Word-like chovani napric strankovanim, header/footer regiony, anchored images a DOCX roundtrip.
- Robustnejsi cesta je layout vrstva v JS runtime: pri renderu stranky vytvori wrapping exclusion zones pro floating images a podle nich rozdeli textove line boxes. Pro prvni iteraci staci omezit podporu na jeden obrazek v odstavcovem use-casu a Square wrap.
- UI musi byt jednoduche: v image toolbaru tlacitko `Wrap text` s preset menu a v inspectoru presne hodnoty vzdalenosti od textu.

Priorita:

- Toto bych posunul na **P1** vedle image contextual toolbaru. Je to viditelna Word parity funkce a primo resi aktualni nefunkcni chovani editoru.

### 5.13 Link UX z CKEditoru

CKEditor link feature ma contextual toolbar: preview, edit, properties, unlink. Tempo ma link dialog v toolbaru a context/mini actions.

Doporuceni:

- po kliknuti na link zobrazit link balloon: otevrit, editovat, odstranit,
- auto-link pri vlozeni URL,
- link decorators: open in new tab, nofollow, download,
- validace URL s jasnym inline error,
- zachovat title/label v `WysiwygLinkPayload`.

### 5.14 Mentions a tokeny

CKEditor Mention je obecny autocomplete feed. Tempo ma `MentionProvider` pro komentare a `TokenProvider` pro document tokens.

Doporuceni:

- sjednotit tokeny, mentions, bookmarks a cross-references pod jeden autocomplete engine,
- trigger znaky: `@`, `#`, `{`, `/`,
- stejny keyboard behavior,
- virtualizovane vysledky,
- provider-driven sections.

### 5.15 Restricted editing

CKEditor restricted editing umi oznacit editovatelne vyjimky v jinak read-only dokumentu.

Pro Tempo je to velmi vhodne pro:

- smlouvy se zamcenym boilerplate textem,
- sablony s vyplnitelnymi poli,
- podpisove dokumenty,
- legal/enterprise workflow.

Navrh:

- `DocumentMarker` type `restrictedRegion`,
- permission model: `CanEditRestrictedRegions`,
- toolbar: "Protect document", "Mark editable region",
- visual chrome jen pri aktivnim review/protect modu,
- export/import DOCX content controls jako budouci rozsireni.

### 5.16 Source editing / HTML support - jen opatrne

CKEditor ma source editing a general HTML support. Pro Tempo bych to neprebiral jako "edituj libovolne HTML", protoze Tempo provider boundary je strukturovany JSON.

Co prevzit:

- developer/debug "View document JSON",
- "View generated HTML for clipboard/export",
- schema-filtered paste/import HTML.

Co neprebirat:

- obecny HTML model bez kontroly,
- libovolne styly v dokumentu,
- runtime reliance na raw HTML jako canonical data.

### 5.17 Minimap, show blocks, fullscreen, word count

CKEditor ma male, ale UX silne features:

- Minimap: pro dlouhe dokumenty,
- Show blocks: ladeni struktury dokumentu,
- Fullscreen: soustredene psani,
- Word count: metrics.

Tempo uz ma status bar a word count, ale doporucuji:

- View tab: `Document map` / `Thumbnails` / `Minimap`,
- View tab: `Show blocks` debug mode,
- fullscreen/focus mode,
- status bar: words, characters, pages, language, sync state.

## 6. Co bych z CKEditoru neprebiral

### 6.1 Neprebirat HTML-first data model

Tempo ma hodnotu v tom, ze provider kontrakt je strukturovany `DocumentEditorDocument`. Nevracel bych se k tomu, aby canonical data byla HTML. HTML ma zustat import/export/clipboard/view format.

### 6.2 Neprebirat cely CKEditor jako embedded engine

Integrace CKEditoru by rychle dala stabilni rich text, ale ztratil bys:

- vlastni provider boundary,
- Blazor-native komponentovy styl,
- detailni DOCX/PDF/export kontrolu,
- vlastni offline/collab model,
- jednotny Tempo design system,
- nezavislost na CKEditor licensing a plugin ekosystemu.

### 6.3 Neprebirat vsechny features najednou

CKEditor ma velmi siroky feature surface. Pro Tempo je dulezite drzet dokumentovy cil:

- smlouvy,
- sablony,
- komentare,
- revize,
- import/export,
- enterprise workflows.

Neni nutne hned pridat media embed, arbitrary HTML support nebo generic page builder funkce.

## 7. UX/UI doporuceni

### 7.1 Ribbon prepracovat na "quiet professional"

Soucasny ribbon je funkcni, ale prilis textovy. Navrhuji:

- primarni Home toolbar jako compact icon-first,
- text jen u vetsich split buttonu a dropdownu,
- tooltip pro kazdy ikonovy command,
- active/mixed/disabled stavy z command registry,
- overflow dropdown,
- mensi skupiny a jasne popisky skupin,
- save/undo/redo v quick-access oblasti, ne jako bezna Home group.

Navrh struktury:

```text
Quick access: Save | Undo | Redo | Status
Tabs: Home | Insert | Layout | References | Review | View
Home:
  Font: style, font, size, bold, italic, underline, color, highlight
  Paragraph: align, list, indent, line spacing
  Insert quick: link, comment
  Editing: find, replace
```

### 7.2 Contextual toolbar podle selection

CKEditor silne pracuje s contextual UI. Tempo uz ma mini toolbar, ale rozsireni by melo byt systemove:

- text selection mini toolbar: bold, italic, link, comment, highlight,
- table toolbar: add row/column, merge/split, header row, properties,
- image toolbar: caption, alt, wrap, replace, link, delete,
- revision popover: accept/reject + author/time,
- link balloon: open/edit/unlink.

### 7.3 Insert tab zlepsit o pickery

Prioritne:

- Table grid picker,
- image source dropdown: Upload / URL / From clipboard / Asset provider,
- page break,
- bookmark,
- special characters,
- horizontal line,
- token.

### 7.4 Review tab zjednodusit na skutecne workflow

Review tab by mel byt nejvetsi konkurencni vyhoda Tempa:

- Track Changes toggle,
- Display mode: All / Simple / No Markup,
- Accept / Reject split buttons,
- Previous / Next revision,
- Comments: New / Previous / Next / Resolve,
- Compare,
- Word count,
- Restricted editing/protection.

### 7.5 Side panel jako "inspector", ne jen seznamy

Side panel dnes obsahuje verze/komentare/revize/suggestions. Doporucuji doplnit:

- kontextovy Inspector tab pro vybrany objekt:
  - image properties,
  - table properties,
  - paragraph properties,
  - link properties,
  - restricted region properties,
- collapsible sections,
- consistent empty states,
- search/filter v comments/revisions.

### 7.6 Status bar a save state

Status bar by mel prevzit roli pending action dashboardu:

- dirty/saving/saved/error/offline/conflict,
- word/character count,
- page count/current page,
- current region: Body/Header/Footer,
- zoom controls,
- collaboration users,
- last autosave time.

### 7.7 Mobile/narrow layout

Pro narrow viewport:

- ribbon collapse do command palette/searchable menu,
- side panel jako drawer,
- page-width zoom default,
- contextual toolbar minimal,
- insert flows fullscreen sheet.

CKEditor ma dobrou inspiraci v grouping/overflow; Tempo by melo jit jeste vic smerem Blazor app ergonomie.

## 8. Architektonicka roadmapa

### Faze 1: Command registry bez zmeny UI

Cil: ziskat centralni command state.

Kroky:

- pridat `DocumentEditorCommandRegistry`,
- zaregistrovat existujici commands,
- toolbar stale vola stare callbacky, ale pres command adapter,
- testy pro `CanExecute`, `Value`, read-only a permissions.

Vysledek: zadna velka UI zmena, ale odstrani se budouci chaos.

### Faze 2: Toolbar registry a overflow

Cil: rozbit monolit `TmDocumentEditorToolbar.razor`.

Kroky:

- definovat toolbar item/group/tab model,
- prevest Home tab na registry,
- pridat overflow dropdown,
- pridat tooltipy,
- zachovat data-testid pro existujici testy pres adapter.

### Faze 3: Clipboard pipeline

Cil: paste z Wordu/Google Docs prestane byt rizikova oblast.

Kroky:

- `DocumentClipboardPipeline`,
- normalizatory,
- JS paste event posle raw payload pipeline,
- unit testy na Word/Docs/Sheets HTML fixtures,
- E2E paste smoke.

### Faze 4: Find & Replace

Cil: doplnit zakladni document editor workflow.

Kroky:

- `DocumentSearchService`,
- markers pro vysledky,
- floating find panel,
- Ctrl+F/Ctrl+H,
- replace respektuje track changes,
- replace all jako jedna undo transakce.

### Faze 5: Table/Image contextual UX

Cil: prvky typu table/image se budou citit jako first-class objects.

Kroky:

- table grid picker,
- table floating toolbar,
- image floating toolbar,
- Word-like image text wrapping vlevo/vpravo,
- image wrap presets a position presets,
- inspector properties,
- merge/split cells,
- alt/caption/link image workflow.

### Faze 6: Watchdog + pending actions

Cil: odolnost pri dlouhe praci.

Kroky:

- runtime watchdog,
- pending action service,
- beforeunload warning,
- recovery status UI,
- E2E test simulovane JS chyby.

### Faze 7: Restricted editing a template workflows

Cil: enterprise/legal odliseni.

Kroky:

- restricted markers,
- protect/restrict toolbar,
- editable regions,
- template token UX,
- export/import mapping.

## 9. Prioritizace podle dopadu

| Priorita | Funkce | Dopad | Narocnost | Proc |
|---|---|---:|---:|---|
| P0 | Command registry | Velky | Stredni | Odemkne ciste UI, permissions, shortcuts a pluginy. |
| P0 | Toolbar overflow/compact mode | Velky | Stredni | Okamzite zlepsi UX a profesionalni dojem. |
| P0 | Clipboard pipeline + Word/Docs paste | Velky | Vyssi | Kriticke pro realne dokumenty. |
| P1 | Find & Replace | Velky | Stredni | Zakladni ocekavani uzivatele. |
| P1 | Table grid picker + contextual table toolbar | Velky | Stredni | Tabulky jsou dokumentovy core workflow. |
| P1 | Word-like image text wrapping | Velky | Vyssi | Obrazky musi umet obtekat text zleva/zprava jako ve Wordu; dnes je to viditelna mezera. |
| P1 | Image contextual toolbar | Stredni/velky | Stredni | Zlepsi praci s obrazky bez velke zmeny modelu. |
| P1 | Pending actions + beforeunload | Stredni | Nizka/stredni | Chrani uzivatele pred ztratou dat. |
| P2 | Watchdog recovery | Velky | Vyssi | Dulezite pro stabilitu dlouhych dokumentu. |
| P2 | Minimap/document map/thumbnails | Stredni | Vyssi | Hodnota u dlouhych dokumentu. |
| P2 | Restricted editing | Velky | Vyssi | Velka enterprise hodnota, ale potrebuje dobry marker model. |
| P3 | Source/debug view | Nizsi | Nizka | Uzitecne pro vyvojare, ne pro bezne uzivatele. |

## 10. Konkretni navrh noveho UI

### 10.1 Editor shell

```text
┌────────────────────────────────────────────────────────────────┐
│ Quick access: Save Undo Redo | autosave/sync status | users     │
├────────────────────────────────────────────────────────────────┤
│ Tabs: Home Insert Layout References Review View                │
├────────────────────────────────────────────────────────────────┤
│ Active ribbon, compact, overflow-aware                         │
├────────────────────────────────────────────────────────────────┤
│ Workspace                                                      │
│   left optional: document map/thumbnails                       │
│   center: paginated A4 pages                                   │
│   right: comments/revisions/inspector/version panel            │
├────────────────────────────────────────────────────────────────┤
│ Status: page | words | region | pending | zoom                 │
└────────────────────────────────────────────────────────────────┘
```

### 10.2 Home tab

- Font style dropdown: Normal, Heading 1-6, Quote
- Font family
- Font size
- Bold/Italic/Underline/Strikethrough
- Text color/Highlight
- Clear formatting
- Alignment segmented buttons
- Bullets/Numbering/Checklist
- Indent/outdent
- Line spacing
- Link
- Comment
- Find

### 10.3 Insert tab

- Table grid picker
- Image split button
- Link
- Bookmark
- Token
- Page break
- Horizontal line
- Special characters

### 10.4 Review tab

- Track changes
- Display mode
- Previous/Next change
- Accept/Reject
- New comment
- Resolve comment
- Compare
- Restricted editing

### 10.5 View tab

- Ruler
- Show blocks
- Document map
- Thumbnails/minimap
- Fullscreen/focus mode
- Zoom 100/Page width/One page

## 11. Technicke riziko a mitigace

### Riziko: runtime monolit v JS

`document-editor-wysiwyg.js` je velmi velky. Kazda nova feature muze zvysit regresni riziko.

Mitigace:

- rozdelit runtime na moduly podle feature,
- feature registry,
- unit JS tests pro konverze/normalizatory,
- watchdog recovery.

### Riziko: toolbar refactor rozbije testy

Existujici testy maji mnoho `data-testid`.

Mitigace:

- zachovat public test ids,
- zavest adapter komponenty,
- migrovat tab po tabu.

### Riziko: paste from Word je nekonecny problem

Office HTML je slozite.

Mitigace:

- zacit s fixtures pro nejcastejsi scenare,
- pipeline s normalizatory,
- degradace musi byt kontrolovana a viditelna ve warnings.

### Riziko: restricted editing komplikuje selection a collaboration

Editovatelne regiony meni command enablement.

Mitigace:

- postavit nejdriv command registry a markers,
- restricted editing az potom.

## 12. Testovaci strategie

### Unit/component

- command registry: enabled/value/read-only/permissions,
- toolbar registry: items visible/hidden/overflow,
- clipboard normalizers: Word/Docs/Sheets fixtures,
- find/replace service,
- marker range update pri insert/delete,
- watchdog state machine.

### E2E

- paste Word-like HTML do dokumentu,
- Ctrl+F najde text a next/previous naviguje,
- replace one / replace all s undo,
- insert table pres grid picker,
- table context toolbar add/remove/merge/split,
- image toolbar caption/alt/replace,
- watchdog recovery po simulovane JS chybe,
- narrow viewport toolbar overflow,
- keyboard navigation v toolbaru a floating UI.

### Visual regression

- desktop page view,
- narrow page-width mode,
- review markup modes,
- side panel open/closed,
- table selected,
- image selected,
- dark mode.

## 13. Hlavni rozdil v produktove filozofii

CKEditor je obecny rich-text framework. Je silny v modularite, plugin ecosystemu, schema/conversion/clipboard architekture a obecne editaci obsahu.

`TmDocumentEditor` by mel zustat dokumentovy workflow editor pro Tempo:

- dokumenty jako pravni/obchodni artefakty,
- verzovani,
- komentare,
- revize,
- compare,
- import/export,
- offline/collaboration,
- sablony a tokeny,
- provider boundary pro backendy.

Proto bych CKEditor pouzil jako referencni architekturu, ne jako cilovy produktovy tvar.

## 14. Finalni doporuceni

Nejlepsi cesta je **Tempo-native editor s CKEditor-like vnitrni disciplinou**:

1. Zachovat `DocumentEditorDocument` a providery jako verejny contract.
2. Zachovat JS-owned runtime jako live editing authority.
3. Nad runtime a Blazor shell pridat plugin/command/schema/toolbar registries.
4. Prepracovat ribbon na adaptivni command-driven UI.
5. Pridat clipboard pipeline a find/replace jako prvni viditelne features.
6. Sjednotit floating UI pro text/table/image/link/revision.
7. Pridat watchdog a pending actions pro stabilitu.

Kdybych mel vybrat jen tri veci s nejlepsim pomerem dopad/narocnost:

1. **Command registry + toolbar registry** - vycisti architekturu a zlepsi budoucnost komponenty.
2. **Clipboard pipeline + Paste from Office/Docs** - nejvic zvedne realnou pouzitelnost.
3. **Find & Replace + contextual table/image toolbars** - nejrychleji zvedne dojem z editoru na uroven profesionalniho document editoru.
