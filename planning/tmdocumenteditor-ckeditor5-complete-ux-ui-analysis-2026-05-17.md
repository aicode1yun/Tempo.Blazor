# TmDocumentEditor vs CKEditor 5: kompletní analýza, UX/UI doporučení a roadmapa

**Datum:** 2026-05-17  
**Aktuální implementační master TODO:** `planning/tmdocumenteditor-complete-improvements-tdd-implementation-todo-2026-05-17.md`  
**Analyzované zdroje:**

- Tempo.Blazor: `/home/pavel/NetProjects/Tempo.Blazor`
- CKEditor 5: `/home/pavel/NetProjects/ckeditor5`

**Poznámka k licenci:** CKEditor 5 je dual-license GPL/commercial. Doporučení níže jsou konceptuální a architektonická. Nejde o návrh kopírovat zdrojové kódy CKEditoru do Tempo.Blazor.

## 1. Executive Summary

`TmDocumentEditor` už není jen rich-text komponenta. Aktuální stav odpovídá vlastnímu document-processing editoru: Blazor shell, JS-owned WYSIWYG runtime, provider boundary, komentáře, revize, track changes, DOCX/PDF integrace, offline drafty, collaboration, page surface, ruler, zoom, find/replace, toolbar overflow, command registry a watchdog recovery. To je produktově výrazně ambicióznější než běžný embedded HTML editor.

CKEditor 5 je oproti tomu zralý editorový framework s velmi disciplinovanou architekturou: pluginy, commands, schema-driven model, conversion pipelines, clipboard pipeline, toolbar component factory, focus tracking, contextual balloons, pending actions, autosave a watchdog. Největší hodnota CKEditoru pro Tempo není konkrétní UI ani konkrétní kód, ale jeho systémové oddělení zodpovědností.

Hlavní doporučení:

1. **Nepřepisovat `TmDocumentEditor` na CKEditor.** Tempo má vlastní strukturovaný dokumentový model, provider kontrakty, Blazor integraci a enterprise workflow, které jsou pro knihovnu hodnotnější než přímá integrace CKEditoru.
2. **Dokončit pluginizaci kolem existujícího command/toolbar registry.** Registry už existuje, ale toolbar i runtime jsou stále částečně monolitické.
3. **Přenést CKEditor principy do vlastního runtime:** schema, differ, marker store, conversion pipeline, clipboard event pipeline, contextual floating UI a jednotný focus manager.
4. **UX zlepšit přes kontext a progresivní odhalování, ne přes další tlačítka.** Editor už má hodně funkcí; největší UX skok přijde z lepšího uspořádání, contextual toolbars, inspektorů a stabilnějších flows pro paste, image, table, comments a revisions.
5. **Prioritizovat paste/import kvalitu, image/table UX a review workflow.** To jsou místa, kde uživatel nejrychleji pozná rozdíl mezi vlastním editorem a mature editorem typu CKEditor/Word/Google Docs.

## 2. Co jsem kontroloval v Tempo.Blazor

Hlavní soubory:

- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditorToolbar.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.Registry.cs`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Interfaces/DocumentEditorProviders.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlocks.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentEditorDocument.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentNotesHeadersRevisions.cs`
- `docs/document-editor-js-owned-runtime.md`

Orientační velikost aktuálních částí:

- `TmDocumentEditor.razor.cs`: 5 826 řádků
- `TmDocumentEditorToolbar.razor`: 1 708 řádků
- `document-editor-wysiwyg.js`: 12 316 řádků
- `_document-editor.css`: 2 661 řádků
- `_document-editor-toolbar.css`: 471 řádků

To je důležitý signál: největší riziko už není absence funkcí, ale udržitelnost editorového jádra, toolbaru a floating UI.

## 3. Co jsem kontroloval v CKEditor 5

Relevantní části:

- `packages/ckeditor5-core/src/plugin.ts`
- `packages/ckeditor5-core/src/command.ts`
- `packages/ckeditor5-engine/src/model/document.ts`
- `packages/ckeditor5-clipboard/src/clipboardpipeline.ts`
- `packages/ckeditor5-ui/src/componentfactory.ts`
- `packages/ckeditor5-ui/src/toolbar/toolbarview.ts`
- `packages/ckeditor5-ui/src/panel/balloon/contextualballoon.ts`
- `packages/ckeditor5-utils/src/focustracker.ts`
- `packages/ckeditor5-autosave/src/autosave.ts`
- `packages/ckeditor5-watchdog/src/editorwatchdog.ts`
- `packages/ckeditor5-find-and-replace/src/findandreplace.ts`
- `packages/ckeditor5-table/src/ui/inserttableview.ts`
- balíčky pro link, image, table, list, mention, restricted editing, source editing, word count a paste/clipboard.

Nejdůležitější CKEditor principy:

- plugin má `requires`, `init`, `afterInit`, `destroy`;
- funkce se často dělí na `Editing` a `UI`;
- command má `value`, `isEnabled`, `affectsData`, `refresh()` a forced-disable stack;
- toolbar se skládá z component factory a konfigurace, ne z pevně zadrátovaného markup stromu;
- clipboard je event pipeline: DOM paste/drop -> view fragment -> model fragment -> insertion;
- model má roots, history, differ, markers, post-fixers a selection;
- floating UI je sjednocené přes contextual balloon a stacky;
- focus je centrálně sledovaný přes `FocusTracker` a keyboard cyklení;
- autosave používá pending actions a `beforeunload`;
- watchdog si drží poslední obnovitelný stav a umí editor restartovat.

## 4. Současný stav `TmDocumentEditor`

### 4.1 Silné stránky

`TmDocumentEditor` má velmi silnou produktovou šířku:

- strukturovaný `DocumentEditorDocument`, ne jen HTML string;
- provider boundary pro load/save/verze/komentáře;
- DOCX import/export přes `IDocumentFormatProvider`;
- PDF export přes `IDocumentPdfExportProvider`;
- compare přes `IDocumentComparisonProvider`;
- comments rail, revision panel, version panel, properties panel, outline panel;
- track changes s inline accept/reject a review display mode;
- offline drafty a conflict banner;
- collaboration provider a remote cursor overlay;
- image provider, clipboard images, local draft assets;
- token provider a template preview;
- headers/footers, notes, page breaks, page surface, ruler a zoom;
- table operations: insert rows/columns, delete, merge, split, header row;
- command registry s read-only/protected-region gating;
- toolbar overflow menu;
- find/replace panel;
- watchdog wrapper pro runtime recovery;
- rozsáhlé bUnit/E2E testy.

Největší technické plus je rozhodnutí, že živou editaci vlastní JS runtime a Blazor vlastní shell, provider orchestrace, panely, lokalizaci a status UI. To odpovídá směru, kterým by se u takto komplexního editoru mělo jít.

### 4.2 Slabší místa

Největší slabiny nejsou „chybí bold/italic“, ale systémové:

- `TmDocumentEditorToolbar.razor` je pořád velký pevný Razor strom s mnoha parametry;
- `document-editor-wysiwyg.js` je velký runtime modul, který potřebuje vnitřní feature hranice;
- command registry existuje, ale toolbar ho zatím používá spíš jako stavový zdroj než jako plnou command execution vrstvu;
- některé commandy jsou v registry nebo toolbaru jen částečně napojené (`importDocx` v registry vrací `Task.CompletedTask`, některé context menu položky jsou disabled);
- search/replace mění C# model přes `DocumentReplaceService`, ale UX by mělo být runtime-first, aby replacement respektoval selection, undo, track changes a DOM decorations;
- floating UI existuje ve více podobách: mini toolbar, text context menu, table context menu, token popover, image dialog, inline revision popover;
- accessibility je přítomná, ale chybí jednotný focus/floating/aria-live systém na úrovni editoru;
- clipboard pipeline je výrazně lepší než dřív, ale pořád nedosahuje CKEditor modelu „více listenerů na stabilních fázích pipeline“;
- image UX je funkční, ale není ještě produktově srovnatelné s mature editorem;
- table UX má důležité operace, ale chybí hlubší vlastnosti tabulky/buněk, selection handles, resize a inspektor.

## 5. Přímé porovnání

| Oblast | TmDocumentEditor | CKEditor 5 | Doporučení |
|---|---|---|---|
| Produktový cíl | Word-like document editor v Blazoru | obecný rich-text editor framework | Zachovat vlastní směr Tempo, neinvertovat se do CKEditor klonu. |
| Model | strukturovaný document model v C# + JS canonical runtime | schema-driven model s roots/history/differ/markers | Přidat formální schema, differ a marker store do Tempo runtime. |
| Runtime | JS-owned DOM/runtime, Blazor shell | engine model/view/controller | Pokračovat v JS-owned runtime, ale rozdělit ho na features. |
| Pluginy | zatím implicitní moduly a registry | `Plugin`, `requires`, `init`, `afterInit` | Zavést `IDocumentEditorFeature` + JS feature registry. |
| Commands | C# registry + JS string commands | command collection s refresh/value/isEnabled | Registry rozšířit na jediný zdroj pravdy pro toolbar, shortcuts, context menus. |
| Toolbar | ribbon tabs, overflow, hodně ručního markup | component factory, config, grouping | Převést toolbar na deklarativní item config + component factory. |
| Clipboard | paste handler, normalizace, C# pipeline existuje | vícefázová clipboard pipeline | Udělat stabilní fáze: raw -> normalized HTML -> document fragment -> insert transaction. |
| Floating UI | několik samostatných popoverů/menu | contextual balloon se stacky | Sjednotit floating UI do jednoho manageru se stacky, fokusem a pozicováním. |
| Find/Replace | panel existuje | editing/UI split + commands + markers | Přesunout replace do runtime transaction/undo/track-changes vrstvy. |
| Image | upload/URL/alt/replace/wrap částečně | image insert, upload, caption, styles, resize | Přidat image inspector, resize handles, caption/alt/link flow a upload progress. |
| Tables | insert grid, row/column ops, merge/split | table grid, properties, cell properties, resize | Přidat table selection UX, resize, properties panel a keyboard navigation. |
| Autosave | interval, save state, offline drafty | debounced autosave + pending actions + beforeunload | Přidat debounced state machine a browser unload guard. |
| Watchdog | recovery wrapper existuje | editor watchdog s uloženým stavem | Posílit snapshot strategy, retry policy, telemetry a recoverable/non-recoverable chyby. |
| A11y/focus | ARIA a keyboard manager | FocusTracker, FocusCycler, AriaLive | Zavést centrální focus manager a announce service. |

## 6. Co bych z CKEditoru převzal

### 6.1 Skutečný feature/plugin model

Tempo už má registry, ale feature hranice nejsou dotažené. CKEditor striktně odděluje `Editing` a `UI` pluginy. To je přesně vhodné pro Blazor component library, protože některé host aplikace budou chtít headless/provider funkce bez plného UI.

Navržené rozhraní:

```csharp
public interface IDocumentEditorFeature
{
    string Name { get; }
    IReadOnlyList<string> Requires { get; }
    void RegisterCommands(DocumentEditorCommandRegistry commands);
    void RegisterToolbar(DocumentEditorToolbarRegistry toolbar);
    void RegisterShortcuts(DocumentEditorShortcutRegistry shortcuts);
    void RegisterFloatingUi(DocumentFloatingUiRegistry floatingUi);
    void ConfigureSchema(DocumentEditorSchemaBuilder schema);
}
```

První features:

- `TextFormattingFeature`
- `ParagraphFeature`
- `ClipboardFeature`
- `FindReplaceFeature`
- `ImageFeature`
- `TableFeature`
- `CommentsFeature`
- `TrackChangesFeature`
- `HeadersFootersFeature`
- `ImportExportFeature`
- `RestrictedEditingFeature`
- `OfflineCollaborationFeature`

Přínos:

- menší soubory;
- jasné dependency hranice;
- snadné vypínání funkcí host aplikací;
- jednodušší testování;
- méně parametrů na `TmDocumentEditorToolbar`;
- UI se generuje podle dostupných features a permissions.

### 6.2 Command jako jediný zdroj pravdy

Tempo už má `DocumentEditorCommandRegistry`, což je dobrý směr. Doporučuji ho dotáhnout do CKEditor stylu:

- command drží `Name`, `IsEnabled`, `Value`, `AffectsData`, `DisabledReason`;
- command umí `RefreshAsync(context)`;
- command umí `ExecuteAsync(payload)`;
- toolbar, context menu, mini toolbar, keyboard shortcut i command palette volají stejnou command vrstvu;
- read-only, protected editing a provider availability se neřeší ručně v každém buttonu.

Konkrétní zlepšení:

- `addComment`, `compareDocuments`, `importDocx` a podobné commandy napojit na reálné execute metody v registry;
- context menu položky `Cut`, `Copy`, `Paste`, `Font`, `Paragraph` buď implementovat, nebo v dané fázi nezobrazovat;
- commandy pro `find`, `replace`, `replaceAll`, `imageResize`, `tableProperties`, `cellProperties`, `insertFootnote`, `insertEndnote`, `insertPageBreak`;
- `AffectsData = false` důsledně pro view-only akce: fullscreen, ruler, zoom, outline, source/debug, comments panel.

### 6.3 Component factory pro toolbar

CKEditor používá `ComponentFactory`, `ToolbarView.fillFromConfig()` a filtraci nedostupných items. Tempo má `DocumentEditorToolbarRegistry`, ale toolbar je stále ruční markup.

Doporučení:

- každá feature registruje toolbar itemy;
- toolbar renderuje `DocumentToolbarItem` podle `Tab`, `Group`, `Order`, `Kind`, `CommandName`;
- ručně zachovat jen low-level renderery pro item kind: button, toggle, select, color picker, split button, menu, grid picker;
- z toolbaru odstranit většinu specializovaných `[Parameter]`;
- `More` menu řadit podle priority a skupin, ne jen podle přeteklých DOM prvků.

Vizuální dopad:

- méně hluku v ribbonu;
- snadnější kompaktní režim;
- možnost nabídnout `Classic toolbar`, `Ribbon`, `Compact`, `Floating` layout;
- host aplikace si může přidat vlastní command bez forku komponenty.

### 6.4 Clipboard pipeline

CKEditor clipboard pipeline má jasné fáze. Tempo by mělo mít totéž, ale nad vlastním modelem:

1. `RawClipboardInput`: HTML, plain text, files, source metadata.
2. `ClipboardSourceDetection`: Word, Google Docs, Google Sheets, Tempo, URL, plain text.
3. `HtmlNormalization`: odstranění office fragmentů, stylů, nebezpečných atributů.
4. `DocumentFragmentConversion`: převod na `DocumentBlock`/inline fragment.
5. `InsertionPolicy`: co je dovoleno v aktuální selection/region/cell.
6. `RuntimeTransaction`: vložení jako jeden undo krok, volitelně track changes.
7. `PostPasteReport`: nenápadný banner „převedeno z Wordu, 3 styly zjednodušeny“.

Část normalizerů už v repu existuje ve `Components/DocumentEditor/Clipboard/Normalizers`. Doporučení je posunout je z pomocné vrstvy na stabilní veřejnou pipeline, kterou mohou rozšiřovat host aplikace.

### 6.5 Model schema, differ a post-fixers

CKEditor model má schema, history, differ a post-fixers. Tempo model je strukturovaný, ale potřebuje formální pravidla:

- co smí být v body;
- co smí být v header/footer;
- co smí být uvnitř table cell;
- jaké inline marks jsou povolené uvnitř linku/tokenu/revision;
- zda může být table v table;
- jak se opravuje prázdný paragraph/cell;
- jak se zachází s orphan comments/revisions;
- jak se řeší neplatné importy z DOCX/HTML.

Doporučený minimální schema builder:

```csharp
schema.Block("paragraph").AllowIn("body", "header", "footer", "tableCell");
schema.Block("table").AllowIn("body").DisallowIn("tableCell");
schema.Block("pageBreak").AllowIn("body").DisallowIn("header", "footer", "tableCell");
schema.Mark("link").AllowOnText().ExclusiveWith("token");
schema.Mark("revision").AllowOnText().AffectsReview();
```

Post-fixers:

- prázdná buňka musí mít paragraph placeholder;
- odstraněný block odpojí comment anchors;
- image bez alt textu dostane empty alt, ne `null`;
- import nesmí vytvořit neznámý block bez fallbacku;
- revision deletion nesmí zmizet před accept/reject.

### 6.6 First-class markers

CKEditor používá markers pro komentáře, find results, suggestions, mentions a collaboration metadata. Tempo má komentáře/revize/search markers, ale částečně jako DOM dekorace.

Doporučení:

- zavést `DocumentMarkerStore` v runtime;
- marker typy: `comment`, `revision`, `search`, `remoteSelection`, `mentionQuery`, `restrictedRegion`, `bookmark`;
- marker má range, z-index/priority, affectsData, source, class mapping;
- marker update probíhá přes jednu službu při inputu, paste, undo/redo, remote ops.

Přínos:

- méně ručních wrapperů v DOM;
- snazší kombinace komentář + revize + search highlight;
- lepší stabilita při undo/redo a collaboration;
- jednotné scroll/select behavior.

### 6.7 Contextual Balloon / Floating UI Manager

Tempo má mini toolbar, text context menu, table context menu, token popover, image dialog a inline revision review. CKEditor má pro tento problém jeden contextual balloon.

Doporučený `DocumentFloatingUiManager`:

- jeden root portal pro floating UI;
- stacky podle typu: `selection`, `link`, `image`, `table`, `comment`, `revision`, `token`, `find`;
- viewport-aware positioning;
- collision handling;
- focus trap/focus restore;
- Escape close podle stacku;
- click outside rules;
- `aria-live` pro stavové změny;
- stejná keyboard navigace pro menu/grid/list.

UX dopad:

- link dialog může být malý contextual panel u výběru;
- image toolbar může být přímo u obrázku;
- table toolbar může být nad tabulkou;
- revision accept/reject panel nebude ručně pozicovaný ad-hoc;
- token autocomplete bude vypadat stejně jako mention/autocomplete.

### 6.8 Autosave + Pending Actions

Tempo má autosave interval, dirty state, status bar a pending action service. CKEditor autosave ale dělá dvě důležité věci navíc:

- debounced save podle lokálních změn;
- pending action napojenou na `beforeunload`.

Doporučení:

- vytvořit autosave state machine: `synchronized`, `waiting`, `saving`, `error`;
- pokud během save přijde další lokální změna, naplánovat immediate save po dokončení aktuálního;
- status bar zobrazit jasně: „Uloženo“, „Ukládám“, „Čekám na změny“, „Nepodařilo se uložit“;
- při pending changes zapnout browser unload guard;
- provider chyby řadit podle recoverable/non-recoverable.

### 6.9 Watchdog dotáhnout na produkční recovery

Tempo už má watchdog wrapper. CKEditor watchdog ale drží poslední data, roots, markers, comments a track changes data a obnovuje editor řízeně.

Doporučení:

- rozlišit recovery z command chyby, remote operation chyby, render chyby a serialization chyby;
- ukládat poslední stabilní runtime snapshot po transakci, ne až při recovery;
- držet také marker store, undo metadata, active selection a pending upload state;
- nastavit retry limit a exponential backoff;
- telemetry event: `runtimeRecovered`, `runtimeRecoveryFailed`, `snapshotFallbackUsed`;
- status bar: krátká zpráva „Editor byl obnoven po chybě“ s možností zobrazit detail v debug režimu.

## 7. UX/UI doporučení

### 7.1 Ribbon a toolbar

Současný ribbon je funkční, ale hustý. Doporučuji tři režimy:

- **Ribbon:** výchozí pro Word-like editing.
- **Compact toolbar:** pro embedded app scénáře, méně textu, více ikon s tooltipy.
- **Distraction-free:** stránka + mini toolbar + status bar, ribbon skrytý.

Konkrétní změny:

- běžné formatting akce jako Bold/Italic/Underline v compact režimu jen ikona + tooltip;
- text u tlačítek ponechat pro méně známé akce: Compare, Protect, Export, Track Changes;
- oddělit document lifecycle (`Save`, import/export, versions) do top command area, ne mezi formatting;
- aktivovat contextual tabs: `Table`, `Image`, `Header/Footer`, `Review`, když je relevantní selection;
- commandy bez implementace nezobrazovat;
- More menu udělat skupinové a vyhledatelné, ne jen „přeteklá tlačítka“;
- přidat command palette pro power users: hledání příkazů, např. `Ctrl+Shift+P`.

### 7.2 Document surface

Aktuální page surface, ruler a zoom jsou dobrý základ. Doporučení:

- přidat page navigator / mini page thumbnails pro dlouhé dokumenty;
- zlepšit page break UX: viditelný „Page break“ handle, možnost delete přes backspace/context menu;
- non-printing characters jako volitelný view mode: paragraph marks, spaces, page breaks;
- lepší empty states v dokumentu: prázdný body, prázdná buňka, prázdný header/footer;
- při overflow page zobrazit méně rušivou inline indikaci a action „Adjust spacing“ / „Insert page break“;
- outline panel propojit s aktivním headingem a scroll position.

### 7.3 Selection a mini toolbar

Mini toolbar by měl být nejrychlejší cesta k běžné práci:

- text selection: bold, italic, link, comment, highlight;
- link selection: edit link, open, remove;
- image selection: replace, alt, caption, wrap, size;
- table selection: row/column insert, delete, merge, cell background;
- revision selection: accept/reject;
- comment anchor: open thread/reply/resolve.

Důležité je, aby mini toolbar nebyl jen další toolbar. Má být kontextový, malý a stabilně pozicovaný.

### 7.4 Image UX

Z CKEditoru bych převzal hlavně produktové patterny:

- jeden `Insert image` split button: Upload, URL, asset provider;
- drag & drop obrázku přímo do stránky;
- upload progress přímo na placeholderu obrázku;
- image toolbar u vybraného obrázku;
- alt text dialog jako accessibility-first flow;
- caption toggle;
- link image;
- resize handles s poměrem stran;
- wrap/alignment preview jako ikonové swatche;
- error state na obrázku s retry/remove.

Tempo už má image provider a image wrap panel. Doporučení je sjednotit image dialog, context toolbar a wrap panel do jednoho image inspectoru.

### 7.5 Table UX

Tempo má table grid picker a základní table commands. CKEditor table package je silný hlavně v detailech:

- 10x10 insert grid s keyboard navigation;
- table toolbar po výběru tabulky;
- cell properties a table properties;
- resize columns;
- header row/column;
- selection model pro buňky;
- lepší paste z tabulkových zdrojů.

Doporučení pro Tempo:

- table selection overlay: vybraná buňka, row/column handles;
- drag resize columns;
- `Table properties` panel: width, alignment, borders, background, cell padding;
- `Cell properties`: background, border, vertical align, colspan/rowspan info;
- keyboard: Tab/Shift+Tab mezi buňkami, Enter uvnitř buňky, Ctrl+Enter za tabulku;
- paste ze Sheets/Excel převést na table block s warnings;
- oddělit table commands do `TableFeature`.

### 7.6 Find & Replace

Find panel existuje. Největší zlepšení:

- replacement dělat runtime-first transaction;
- každé replace/replace all jako undoable command;
- při track changes replacement vytvoří deletion + insertion revision;
- search results jako first-class markers, ne jen DOM spans;
- options: case sensitive, whole word, regex později;
- result list pro dlouhé dokumenty;
- search scope: body/header/footer/comments;
- `Ctrl+F` předvyplní aktuálně vybraný text.

### 7.7 Comments a Review

Tempo už je tady silné. Doporučení z mature editorů:

- komentářová vlákna zarovnat vizuálně k anchoru;
- filter: open/resolved/mine/all;
- sort: position/time;
- review mode switch: All markup, Simple markup, No markup, Original;
- accept/reject all by author/type/selection;
- summary banner: „12 pending changes, 4 comments“;
- při kliknutí na revision v panelu zvýraznit přesně odpovídající inline range;
- pro resolved comments nabídnout collapsed display, ne jen skrytí.

### 7.8 Tokeny, mentions a autocomplete

CKEditor mention plugin má dobré principy:

- marker trigger;
- debounced feed;
- out-of-order response discard;
- dropdown limit;
- custom renderer;
- marker range pro aktuální query.

Tempo má `TokenProvider` a token popover. Doporučení:

- zobecnit token menu na autocomplete engine;
- podporovat více triggerů: `{{`, `@`, `#`, případně `/`;
- každý feed má vlastní provider, minimum characters, renderer a insert command;
- token query marker patří do marker store;
- výsledky musí být keyboard-first: arrows, Enter, Escape, Tab.

### 7.9 Source/Debug editing

CKEditor má source editing a HTML support. Pro Tempo bych byl opatrný:

- veřejný „HTML source editing“ nedává smysl jako primární funkce, protože Tempo model není HTML;
- debug JSON modal je dobrý pro vývojáře;
- vhodnější veřejná funkce je **Document JSON inspector** jen v debug/dev režimu;
- pro produkci raději `Import/Export` a `Compare`, ne ruční editace source.

## 8. Co bych z CKEditoru nepřebíral

Nepřebíral bych:

- celé CKEditor engine/model/view vrstvy;
- přesnou CKEditor UI estetiku;
- GPL zdrojový kód;
- HTML jako hlavní persistence model;
- plugin API navázané na TypeScript třídy uvnitř CKEditoru;
- premium collaboration/track changes jako černou skříňku.

Tempo má výhodu v Blazor provider kontraktech a strukturovaném dokumentu. Tu by přímá integrace CKEditoru oslabila.

## 9. Prioritní roadmapa

### Fáze 1: Zpevnit command/feature architekturu

Výstupy:

- `IDocumentEditorFeature`;
- registry pro commands, toolbar, shortcuts a floating UI;
- toolbar generovaný z `DocumentToolbarItem`;
- všechny toolbar/context/shortcut akce používají registry;
- odstranit nebo implementovat disabled placeholder položky.

Testy:

- command state pro read-only/protected/editable region;
- toolbar item availability podle features;
- context menu command parity;
- host custom command smoke test.

### Fáze 2: Floating UI a focus manager

Výstupy:

- jednotný floating layer portal;
- stack model pro link/image/table/comment/revision/token;
- focus restore a Escape stack behavior;
- aria-live announce service;
- viewport collision handling.

Testy:

- keyboard navigation v toolbaru/menu/gridu;
- Escape zavírá správnou vrstvu;
- focus se vrací na selection/surface;
- mobile viewport bez překryvů.

### Fáze 3: Runtime-first find/replace a markers

Výstupy:

- search markers v marker store;
- replace one/all jako runtime transaction;
- track changes kompatibilní replacement;
- undo/redo pro replace;
- result list a scope.

Testy:

- replace all je jeden undo krok nebo jasně popsaný batch;
- replace v table cell;
- replace v header/footer;
- replace s track changes;
- search highlight + comments/revisions bez rozbití DOM.

### Fáze 4: Clipboard pipeline 2.0

Výstupy:

- veřejná clipboard pipeline;
- normalizers pro Word, Google Docs, Google Sheets, raw HTML, URL, Tempo internal;
- warnings/report;
- paste jako jedna runtime transaction;
- paste images přes provider/offline fallback;
- schema-aware insertion.

Testy:

- Word basic/inline/list/table;
- Google Docs headings;
- Google Sheets table;
- nested/invalid HTML;
- paste with track changes;
- paste do table cell/header/footer.

### Fáze 5: Image a table UX upgrade

Výstupy:

- image inspector;
- image resize handles;
- upload progress/error/retry;
- table properties/cell properties;
- column resize;
- table/cell selection handles;
- contextual image/table toolbar.

Testy:

- image upload lifecycle;
- image resize persists to model/export;
- table resize persists;
- keyboard navigation in table;
- mobile/narrow layout.

### Fáze 6: Autosave, pending actions, watchdog hardening

Výstupy:

- autosave state machine;
- beforeunload guard;
- pending actions visible in status bar;
- watchdog recovery with stable snapshot cache;
- telemetry/debug events.

Testy:

- save while typing;
- provider error retry;
- unload warning when dirty/pending;
- runtime command crash recovery;
- remote operation crash fallback.

## 10. Největší UX výhra s nejmenším rizikem

Pokud bych měl vybrat jen pět praktických změn:

1. **Contextual image/table/link toolbar** nad jednotným floating managerem.
2. **Schovat nehotové placeholder commandy** a tím zvýšit důvěru v UI.
3. **Runtime-first replace** napojený na undo a track changes.
4. **Clipboard paste report** pro Word/Google Docs/Sheets: uživatel vidí, co se převedlo a co zjednodušilo.
5. **Compact toolbar mode** s čistším ribbonem, skupinovým More menu a command palette.

Tyto změny nezahazují existující práci, ale znatelně zvednou pocit kvality.

## 11. Technická priorita

Největší technický dluh je velikost a centralizace runtime/toolbaru:

- `document-editor-wysiwyg.js` rozdělit minimálně konceptuálně na moduly: core, rendering, selection, input, clipboard, formatting, image, table, comments, revisions, collaboration, serialization, watchdog;
- `TmDocumentEditorToolbar.razor` zmenšit přes registry-driven rendering;
- přesunout command execution do registry;
- z DOM dekorací udělat marker layer;
- udělat schema/post-fixer vrstvu, aby import/paste/runtime nikdy nevytvářely nevalidní dokument.

Tohle není kosmetika. U editoru této velikosti je architektura UX feature: když je runtime stabilní, uživatel cítí méně skoků, méně ztracené selection, méně náhodných disabled stavů a méně rozbitých paste/import scénářů.

## 12. Závěr

`TmDocumentEditor` má velmi dobrý základ a v některých enterprise oblastech už jde dál než běžná konfigurace CKEditoru: provider kontrakty, offline drafty, document compare, Blazor-native panely, strukturovaný dokument a server-side format boundaries. CKEditor 5 je ale výrazně zralejší v systémové disciplíně editoru: pluginy, commands, schema, markers, conversion, clipboard, focus, floating UI a watchdog.

Nejlepší směr není „nahradit Tempo CKEditorem“, ale **udělat z `TmDocumentEditoru` vlastní editorový framework uvnitř Tempo.Blazor**. To znamená převzít CKEditor principy, ale ne jeho kód: rozdělit features, sjednotit commandy, formalizovat model schema, stabilizovat marker/floating/focus vrstvy a posunout UX směrem k contextual editingu.

Priorita pro nejbližší iterace: **feature registry -> floating UI manager -> runtime-first find/replace -> clipboard pipeline -> image/table inspector**.
