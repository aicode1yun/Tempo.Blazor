# TmDocumentEditor - Canvas engine inspirovany ONLYOFFICE TDD TODO

Datum zalozeni: 2026-06-04  
Stav: ve vystavbe; Faze 0-9 hotove  
Priorita: P0 - novy smer pro `TmDocumentEditor`; nechceme se vracet k legacy, ale soucasny core engine neni kvalitativne prijaty

Detailni rozpad: vsechny velke faze (core 2-24 i E1-E12) maji samostatne detailni TDD+E2E plany. Rozcestnik: `planning/tmdocumenteditor-canvas-detailed-plans-index-2026-06-04.md`.

Revize 2026-06-04: plan zkontrolovan proti ONLYOFFICE (`/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word`) a proti soucasnemu core enginu. Pridano: (1) architektonicke principy incremental recalc + per-page canvas cache + section/column markup + reuse, (2) sekce "Znovupouziti stavajiciho core enginu a R.4.x prace", (3) parity checklisty nad ramec legacy (model/toolbar/interakce), (4) faze E1-E12 pro plnou paritu s Word/Google Docs/OnlyOffice (numbering, tab stops, sekce/sloupce, styly, fields/cross-ref/captions, advanced char formatting, shapes/text box/chart, math/rovnice, content controls/formulare, autocorrect/format painter/symboly, view modes/print, hyphenation/page background/advanced tables), (5) dvoustupnova akceptace cutoveru: legacy-parity vs full-quality.

## Proc tento dokument existuje

Soucasny novy core engine je model-owned, ale stale nepusti editor do pocitu stabilniho dokumentoveho editoru. Legacy engine mel take mnoho problemu: DOM/contenteditable drift, nestabilni selection, obtizne toolbar commandy, fragmentaci revizi, riziko prepisu live DOMu Blazorem a nedostatecne pravdive E2E gate.

Cil nove vetve je postavit **uplne novy canvas-based document engine**, ktery je architektonicky inspirovany ONLYOFFICE, ale implementacne je clean-room a vlastni. Engine musi umet vsechny funkce, ktere umel legacy `TmDocumentEditor`, a teprve potom smi nahradit soucasny default.

## Licence a clean-room pravidla

ONLYOFFICE v `/home/pavel/NetProjects/onlyfficeservergit` je AGPL. Tento plan dovoluje pouze architektonickou inspiraci:

- [ ] Nekopirovat zadny zdrojovy kod, nazvy internich trid, algoritmicke implementace ani test fixtures z ONLYOFFICE.
- [ ] Neprekladat ani mechanicky neprepisovat ONLYOFFICE implementaci do Tempo.Blazor.
- [ ] Audit ONLYOFFICE drzet na urovni verejne pozorovatelnych architektonickych principu: canvas/page rendering, oddeleni model-layout-render-command, transakcni historie, UI shell nad runtime enginem.
- [ ] Do kazde PR poznamky pro canvas engine pridat vetu: "ONLYOFFICE byl pouzit pouze jako clean-room architektonicka inspirace; kod nebyl kopirovan."
- [ ] Pokud bude potreba detailni algoritmus, navrhnout ho z vlastnich testu, specifikaci dokumentoveho modelu Tempo a verejnych standardu, ne z ONLYOFFICE kodu.

## Produktovy cil

Editor musi pusobit jako skutecny dokumentovy editor:

- psani je okamzite, caret je stabilni a viditelny,
- text se nikdy neprekryva a po kazdem vstupu je layout validni,
- selection patri runtime enginu, ne browser contenteditable DOMu,
- toolbar, mini toolbar, context menu a keyboard shortcuts volaji jeden command dispatcher,
- canvas vykresluje stranky, text, vybery, caret, objekty, komentare a revize deterministicky,
- DOM je pouze shell, accessibility mirror, input bridge a UI panely,
- save/export/collab vzdy tahaji aktualni model z enginu,
- uzivatelske E2E testy overuji realne kliky, klavesnici, drag a screenshoty, ne jen interni JS API.

## Architektonicke principy inspirovane ONLYOFFICE

- [ ] **Canvas je primarni render surface.** Stranky, text, selection, caret, objekty, tabulky, revize a overlays se kresli do canvas vrstev.
- [ ] **Model je jedina pravda.** DOM ani canvas nejsou persistence model.
- [ ] **Layout je samostatny krok.** Model -> layout tree -> render display list -> canvas paint.
- [ ] **Command pipeline je transakcni.** Kazdy editacni command vytvari operaci nebo transaction, ktera je undoable, redoable, serializovatelna a auditovatelna.
- [ ] **Hit-test je vlastni.** Mouse/touch souradnice se mapuji pres layout tree na logickou pozici v dokumentu.
- [ ] **Selection/caret jsou vlastni.** Browser selection se nepouziva jako autorita.
- [ ] **Blazor je shell.** Toolbar, panely, providery, lokalizace, permission model a persistence orchestrace zustavaji v Blazoru.
- [ ] **Accessibility mirror je povinny.** Canvas sam o sobe neni pristupny; engine musi udrzovat semanticky mirror pro screen readery, focus, live region a clipboard text.
- [ ] **Document structure je first-class.** Nadpisy, outline levels, navigacni osnova a generovany obsah nejsou jen formatovany text, ale semanticke bloky/fieldy s vazbou na layout.
- [ ] **Proofing je engine service.** Kontrola pravopisu bezi jako samostatna diagnosticka vrstva nad text modely, kresli vlastni canvas overlay a opravy prochazeji command dispatcherem.
- [ ] **Rendering nesmi blokovat psani.** Aktivni odstavec/region musi mit immediate path, zbytek dokumentu muze byt virtualizovany nebo renderovany idle.
- [ ] **Incremental recalculation.** Engine drzi recalc-info / dirty tracking: po editaci se layout prepocita od prvniho zmeneneho bloku, ne cely dokument; flow objekty a stranky se reflow-uji jen kdyz je treba. (ONLYOFFICE clean-room: page-based recalc s recalc-info; nekopirovat kod.)
- [ ] **Per-page canvas cache.** Kazda viditelna stranka ma cachovany content layer; selection, caret, search highlight, comments a foreign cursors se kresli jako samostatne overlay pasy nad cache, takze pohyb kurzoru/vyberu neprekresluje text. (ONLYOFFICE clean-room: CPage Draw / DrawSelection / DrawSearch jako oddelene passy.)
- [x] **Section/column markup je soucast layoutu.** Layout zna sekce, vicesloupcovou geometrii a per-section page setup, ne jen jednu A4 plochu.
- [ ] **Znovupouzit, neprepisovat.** R.4.0-R.4.7 a faze D jadra (font-metrics, paragraph-engine, line-breaker, bidi, grapheme, hit-test, caret, selection-overlay, edit-model, operations, undo-stack, collab-client, conventery) jsou hotova a otestovana; canvas engine je stavi jako render/paint vrstvu nad nimi, nikoli green-field rewrite.

## Navrzeny runtime nazev a soubory

Pracovni nazev: `CanvasDocumentEngine`.

Navrzene nove soubory a adresare:

```text
src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasEngineHost.razor
src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasEngineHost.razor.cs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/
  entry.mjs
  interop.mjs
  model/
  layout/
  render/
  input/
  selection/
  commands/
  history/
  clipboard/
  objects/
  tables/
  annotations/
  collaboration/
  a11y/
  diagnostics/
  testing/
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/
tests/Tempo.Blazor.E2E/DocumentEditorCanvasEngine*.cs
```

Feature flag:

- [ ] Pridat `DocumentEditorRenderEngine.CanvasEnginePreview`.
- [ ] `/document-editor` muze pouzit canvas engine az po explicitnim zapnuti nebo po schvalenem cutoveru.
- [ ] Legacy a stavajici core engine zustanou docasne dostupne jen jako referencni/regresni srovnani, ne jako cilova cesta.

## Znovupouziti stavajiciho core enginu a R.4.x prace

Canvas engine NENI green-field. Tyto hotove a otestovane moduly (R.4.0-R.4.7, faze D, ~290 Node + ~19 E2E) se znovupouziji jako vstup, ne prepisuji. Hlavni zmena oproti soucasnemu core enginu je render target: canvas (per-page cache + overlays) misto positioned-DOM; vse ostatni se prevazne recykluje.

- [ ] `layout/font-metrics.mjs` - offscreen-canvas measureText + LRU cache (overeno diff 0.000-0.012px vs getBoundingClientRect).
- [ ] `layout/paragraph-engine.mjs`, `line-breaker.mjs`, `paragraph-tokenizer.mjs` - line breaking, soft wrap, alignment, intervals/exclusions pro obtekani objektu.
- [ ] `layout/bidi.mjs`, `layout/grapheme.mjs` - clean-room UBA bidi + grapheme segmentace.
- [ ] `core-engine/hit-test.mjs`, `caret.mjs`, `selection-overlay.mjs` - point->pozice, caret pohyb po grafemech, selection recty.
- [ ] `core-engine/edit-model.mjs`, `operations.mjs`, `undo-stack.mjs`, `track-changes.mjs` - model mutace, transakce, undo/redo, revize.
- [ ] `core-engine/input-surface.mjs` - skryty input bridge + IME (uz bez contenteditable autority).
- [ ] `core-engine/collab-client.mjs`, `clipboard.mjs`, `find-replace.mjs`, `header-footer.mjs`, `paragraph-styles.mjs`, `list-model.mjs`, `comments.mjs`, `edit-table.mjs`, `object-overlay.mjs`.
- [ ] `CoreEngineModelConverter` (C#) + JS facade `coreEngine.createCoreEditor` - prevod `DocumentEditorDocument` <-> canvas model a command dispatch toolbaru.
- [ ] Kazdy recyklovany modul, ktery se dotkne canvasu, dostane nove RED testy na canvas chovani; existujici Node testy zustavaji zelene a nesmi se oslabit.
- [ ] Co se ZNOVU neresi z nuly: text measurement, bidi, grapheme, line breaking, hit-test, caret math, undo stack, operations, IME, collab transport. Co je NOVE: canvas paint vrstva, per-page cache, overlay passy, recalc-info dirty tracking, a feature subsystemy z faze E1-E12.

## Definice hotovo pro kazdou fazi

Kazda faze musi splnit vsechny body:

- [ ] RED test vznikl pred implementaci.
- [ ] Unit/JS test overuje cisty model/layout/command invariant.
- [ ] bUnit/component test overuje Blazor interop nebo toolbar shell, pokud se ho faze tyka.
- [ ] E2E test pouziva realny browser, realnou klavesnici/mys a demo editor.
- [ ] Kazda viditelna zmena ma screenshot pred/po.
- [ ] Screenshot je automaticky zkontrolovany minimalne na non-blank canvas, viditelny editor, chybne prekryvy UI a ocekavanou zmenu pixelu.
- [ ] Screenshot je rucne/agentem vyhodnoceny z pohledu UX/UI: vypada to jako dokumentovy editor, ne jako rozpadly debug render.
- [ ] Save/reload gate existuje pro kazdou modelovou zmenu, ktera ma persistovat.
- [ ] Undo/redo gate existuje pro kazdy editacni command.
- [ ] `dotnet build` zustava zeleny.
- [ ] Testy se nesmi oslabovat, aby prosly se spatnym UX.

## Screenshot a UX/UI test protocol

Pro canvas engine vznikne sdileny E2E helper, ktery pri kazdem scenari uklada:

```text
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/
  {test-class}/{test-name}/{viewport}/
    00-before-full.png
    01-before-editor.png
    02-after-full.png
    03-after-editor.png
    manifest.json
```

Povinny obsah `manifest.json`:

- test name,
- viewport,
- seed document id,
- user actions,
- expected visible changes,
- expected model changes,
- screenshot paths,
- canvas non-blank metrics,
- overlap checks,
- UX/UI reviewer notes.

Automaticke screenshot gates:

- [ ] `AssertCanvasNonBlankAsync` - canvas nesmi byt prazdny ani jednobarevny.
- [ ] `AssertTextPixelsChangedAsync` - po psani/formattovani se zmeni ocekavana oblast, ne nahodny roh stranky.
- [ ] `AssertCaretVisibleAsync` - caret je viditelny v aktivnim viewportu.
- [ ] `AssertSelectionVisibleAsync` - vyber ma konzistentni highlight recty.
- [ ] `AssertNoTextOverlapAsync` - text/text a text/object recty se neprekryvaji mimo povolene vrstvy.
- [ ] `AssertNoUiOverlapAsync` - toolbar, side panel, context menu, inspector a canvas si nelezi pres sebe neprofesionalne.
- [ ] `AssertToolbarStateMatchesModelAsync` - ribbon/mini toolbar odpovida engine formatting state.
- [ ] `AssertScreenshotLooksIntentionalAsync` - agent otevira screenshoty a zapise kratky UX/UI verdikt.

Minimalni viewporty pro screenshot E2E:

- [ ] desktop 1440x1000,
- [ ] notebook 1280x800,
- [ ] tablet 900x1100,
- [ ] mobil 390x844 pro read/edit smoke a toolbar overflow.

## Legacy feature parity scope

Canvas engine musi pokryt vse, co legacy engine umel verejne nabidnout pres `TmDocumentEditor`, toolbar, providery a demo scenare.

### Public shell a provider boundary

- [ ] `DocumentId`, `Provider`, load/save, save conflict, retry save.
- [ ] `ReadOnly`, `Mode`, `Permissions`.
- [ ] `ShowToolbar`, `ToolbarMode` (`Ribbon`, `Compact`, `DistractionFree`).
- [ ] `ShowComments`, `ShowVersionHistory`, side panel tabs.
- [ ] `DisabledFeatures` a feature registry.
- [ ] `ImageUrlResolver`, `ImageProvider`, `ImageAssetOptions`, image validation.
- [ ] `FontProvider`, `TokenProvider`, `MentionProvider`, `TokenValueProvider`.
- [ ] `PdfExportProvider`, `FormatProvider`, `ComparisonProvider`.
- [ ] `SuggestionProvider`, `CollaborationProvider`, realtime provider path.
- [ ] `OfflineStore`, `SyncProvider`, `PreferLocalDraft`.
- [ ] `AuditSink`, audit failure modes.
- [ ] `OnSaveRequested`, `OnDocumentLoaded`, `OnVersionCreated`, `OnPdfExported`, `OnDocumentFormatExported`, `OnDocumentCompared`.

### Toolbar command parity

- [ ] Save, undo, redo.
- [ ] Bold, italic, underline, strikethrough.
- [ ] Font family, font size.
- [ ] Text color, highlight/background color.
- [ ] Clear formatting.
- [ ] Link apply/remove/open.
- [ ] Align left/center/right/justify.
- [ ] Line spacing, spacing before, spacing after.
- [ ] Increase/decrease indent.
- [ ] Bullet list, numbered list, list nesting.
- [x] Paragraph style / heading style picker (`Normal`, `Heading 1-6`, quote/custom styles).
- [ ] Insert table of contents.
- [ ] Update table of contents.
- [ ] Insert table.
- [ ] Insert image: URL, upload, provider asset.
- [ ] Insert page break.
- [ ] Insert footnote, endnote.
- [ ] Track changes on/off.
- [ ] Review display mode: markup/final/original.
- [ ] Add comment, open comments, open revisions.
- [ ] Compare documents.
- [ ] Protect document, mark editable region.
- [ ] Show ruler, zoom page width, zoom percent, fullscreen.
- [ ] Show blocks, non-printing characters.
- [ ] View document JSON, view clipboard HTML.
- [ ] Export PDF, import DOCX, export DOCX, export ODT where provider supports it.
- [ ] Open versions.
- [ ] Header/footer commands: page number, page count, page X of Y, date, document title, author, different first page, different odd/even, close header/footer.

### Toolbar command parity nad ramec legacy (cil Word / Google Docs / OnlyOffice)

- [ ] Format painter (copy formatting, one-shot i lock).
- [ ] Subscript, superscript, small caps, double strikethrough, change case (UPPER/lower/Sentence/Capitalize/tOGGLE).
- [ ] Character spacing / scale, increase/decrease font size step.
- [ ] Text direction LTR/RTL, paragraph direction toggle.
- [ ] Multilevel list picker, restart/continue numbering, set numbering value, define new list style.
- [ ] Apply/modify/create/delete style, update style from selection, style pane, default formatting reset.
- [ ] Tab stop type picker + ruler tab placement, set/clear tabs dialog.
- [ ] Insert section break (next page/continuous/even/odd), columns (1/2/3/left/right/custom), line numbering.
- [ ] Insert shape, text box, line/arrow, chart, picture-from-shape; group/ungroup, z-order, align/distribute, rotate.
- [ ] Insert equation (gallery + symbol palette), inline/display equation.
- [ ] Insert content control / form field (text, combo, drop-down, date, checkbox, picture), forms mode, clear/highlight fields.
- [ ] Insert symbol / special character, emoji, non-breaking space, optional hyphen, horizontal line.
- [ ] Insert caption, cross-reference, table of figures, bibliography/citation, update fields/all.
- [ ] Page setup: margins, size, orientation, columns; page color, watermark, page borders.
- [ ] Hyphenation auto/manual, language for selection, set proofing language.
- [ ] View modes: print layout, reading/web layout, outline; show ruler/gridlines, navigation pane.
- [ ] Zoom presets: fit page, fit width, multiple pages, custom percent, full screen.
- [ ] Print, print preview, export PDF (already), page setup from print.
- [ ] Word count / document statistics.
- [ ] Autocorrect / autoformat-as-you-type toggle and options.
- [ ] Repeat header rows, convert text-to-table / table-to-text, table sort, insert table formula.

### Document model parity

- [ ] Paragraph, heading, list, quote.
- [ ] Heading metadata: level, outline level, style id/name, stable block id for TOC/navigation.
- [ ] Generated table of contents block/field with entries, target block ids, levels, page numbers and update metadata.
- [ ] Proofing diagnostics as non-persistent or provider-backed annotations: misspelling range, language, suggestions, ignored state.
- [ ] Table with rows/cells, colSpan, rowSpan, widths, backgrounds, alignment, vertical alignment.
- [ ] Standalone image block.
- [ ] Inline drawing/image run.
- [ ] Page break.
- [ ] Text run, field run, drawing run.
- [ ] Inline marks: bold/italic/underline/strike, color, highlight, font, link, comment anchor, revision, bookmark and unknown mark preserve channel.
- [ ] Header/footer documents, scopes and references.
- [ ] Footnotes/endnotes and numbering settings.
- [ ] Page settings, margins, section-like geometry where the model supports it.
- [ ] Comments, replies, resolved state, anchor geometry.
- [ ] Revisions insertion/deletion/formatting and review decisions.
- [ ] Tokens, mentions, autocomplete atomic inline content.

### Document model parity nad ramec legacy (cil Word / Google Docs / OnlyOffice)

Tyto modelove typy legacy `TmDocumentEditor` z velke casti nemel; jsou potreba na deklarovanou kvalitu GDocs/Word/OnlyOffice. Detailni faze viz E1-E12.

- [ ] Numbering definitions: abstract num, level definitions, format (decimal/lower-roman/upper-letter/bullet/legal multilevel), start-at, restart/continue, list style reference.
- [ ] Multilevel list level (0-8) na odstavci s vazbou na numbering definition, ne jen "bullet/numbered".
- [ ] Paragraph/character/table/list styly jako prvotridni objekty: style id, name, based-on, next-style, type, direct override delta.
- [ ] Tab stops na odstavci: pozice, alignment (left/center/right/decimal/bar), leader (none/dot/dash/underline), default tab width.
- [ ] Sekce: section break (next-page/continuous/even/odd), per-section page size/margins/orientation, sloupce (count, width, spacing, separator), line numbering.
- [ ] Pokrocile inline marks: subscript, superscript, small caps, all caps, double strikethrough, character spacing/scale, baseline shift, kerning, vertical-align baseline.
- [x] Field code model: PAGE, NUMPAGES, DATE/TIME, REF/cross-reference, SEQ (caption/figure numbering), STYLEREF, bibliography/citation, instrText + cached result.
- [x] Caption objekty (figure/table/equation) a table of figures jako generovany aktualizovatelny field, ne plochy text.
- [ ] Drawing objekty nad ramec image: shape/auto-shape, text box (s vlastnim odstavcovym obsahem), line/connector, chart, group; fill/stroke/effects, anchor (inline/floating), wrap, z-order, rotace.
- [x] Math/equation objekt: OMML-like strom (fraction, radical, sup/sub, matrix, n-ary, function, accent), inline vs display.
- [ ] Content controls / structured document tags: block i inline, plain text, rich text, combo box, drop-down, date picker, checkbox, picture, repeating; zastupny text, tag/alias, lock.
- [x] Page background: page color, watermark (text/image), page borders.
- [x] Hyphenation settings (auto/manual, zone, consecutive limit) a optional/non-breaking hyphen + non-breaking space jako modelove znaky.
- [ ] Per-run language a proofing override (lang, no-proof), document default language.

### Interaction parity

- [ ] Typing, Enter, Shift+Enter, Backspace, Delete.
- [ ] Mouse click caret placement.
- [ ] Drag selection, double-click word, triple-click paragraph.
- [ ] Shift+click selection extension.
- [ ] Keyboard navigation: arrows, Ctrl/Alt word movement, Home/End, PageUp/PageDown.
- [ ] IME/composition, accents, emoji, surrogate pairs, grapheme clusters.
- [ ] Clipboard: copy, cut, paste, paste plain, rich paste from Word/Google Docs, URL paste, image paste where supported.
- [ ] Context menu for text, link, image, table cell, misspelling.
- [ ] Spellcheck squiggles, suggestions, replace once, replace all, ignore once, ignore all.
- [ ] Heading outline navigation and click-to-jump generated TOC entries.
- [ ] TOC refresh after heading text/order/page-number changes.
- [ ] Drag/drop selected text.
- [ ] Image drag, resize, wrap mode, z-order, alt text, caption, object toolbar/inspector.
- [ ] Table cell navigation with Tab/Shift+Tab, selection ranges, row/column insert/delete, merge/split, resize.
- [ ] Find/replace including regex/backreferences.
- [ ] Autosave, dirty state, before-unload guard.
- [ ] Undo/redo grouped like human editing.
- [ ] Collaboration remote operations and presence.

### Interaction parity nad ramec legacy (cil Word / Google Docs / OnlyOffice)

- [ ] Format painter: vybrat text, kliknout painter, aplikovat formatovani na jiny rozsah; lock pro vice aplikaci.
- [ ] Autocorrect/autoformat behem psani: smart quotes, auto-bullet/auto-number, auto-hyperlink, auto-capitalize, ordinal/fraction, replace-as-you-type.
- [ ] Insert symbol/emoji/special char z palety na caret.
- [ ] Drag/drop a resize tvaru, text boxu a grafu; editace textu uvnitr text boxu/shapeu.
- [ ] Kresleni/uprava equation: klik do equation, navigace mezi sloty, sablony zlomku/odmocnin/matic.
- [ ] Vyplneni content control / form field: tab mezi poli, validace, date picker, checkbox toggle, combo/drop-down.
- [ ] Tab stop drag na pravitku, indent markery drag, decimal tab zarovnani cisel.
- [ ] Vicesloupcove psani: text tece mezi sloupci, column break, sloupcova selection.
- [ ] Reading mode navigace, zoom gestures (Ctrl+wheel, pinch), fit-width/fit-page.
- [ ] Cross-reference klik -> skok, update fields po zmene; caption auto-cislovani po vlozeni/smazani.

## Faze 0: Rozhodnuti, baseline a zakaz iluze

- [x] Zalozit tento plan jako source of truth pro canvas engine.
- [x] Vytvorit `DocumentEditorCanvasEngineDecisionTests`, ktere overi existenci planu a clean-room guardrails.
- [x] Zapsat baseline nespokojenosti se stavajicim core enginem do samostatne sekce: co presne vizualne/UX nevyhovuje.
- [x] Spustit aktualni demo `/document-editor` a ulozit screenshoty soucasneho core enginu jako "before redesign".
- [x] Vytvorit seznam top 20 human scenarios, ktere musi novy canvas engine zvladnout lepe nez core i legacy.
- [x] Zalozit E2E soubor `DocumentEditorCanvasEngineBaselineE2ETests.cs`.
- [x] RED E2E: canvas engine route/flag zatim neexistuje.
- [x] Akceptace: mame pravdivy baseline, screenshoty a explicitni UX problemy.

### Faze 0 source of truth

Tento soubor je od 2026-06-04 source of truth pro novy canvas engine `TmDocumentEditor`. Starsi dokumenty pro legacy, WYSIWYG JS runtime a core engine zustavaji historicky kontext, ale nesmi prepsat smer: canvas primary surface, model jako jedina pravda, vlastni layout, vlastni hit-test, vlastni selection/caret a Blazor pouze jako shell.

Kazde PR nebo lokalni zmena canvas engine musi odkazovat na tento plan a zachovat clean-room vetu: "ONLYOFFICE byl pouzit pouze jako clean-room architektonicka inspirace; kod nebyl kopirovan."

### Faze 0 baseline nespokojenosti se stavajicim core enginem

Aktualni core engine je dulezity mezikrok, ale vizualne a UX neplni cil dokumentoveho editoru na urovni Word/Google Docs/OnlyOffice. Baseline problemy, ktere canvas engine musi zlepsit:

1. Selection stale vypada jako DOM kompromis, ne jako engine-owned dokumentovy vyber s konzistentnimi recty pres radky, bloky, tabulky a objekty.
2. Caret a klik do textu jsou citlive na render vrstvy a positioned-DOM geometrii; uzivatel nesmi mit pocit, ze klika do HTML layoutu, ktery se muze rozjet.
3. Aktivni editace nema jasne oddeleny immediate paint path; pri tlaku na zalamovani radku muze pusobit, ze reflow dohani psani.
4. Page surface neni vykreslovany deterministickym paint stackem; text, selection, objekty, komentare a revize nejsou prirozene oddelene canvas passy.
5. Per-page cache chybi jako primarni koncept, takze pohyb caretu/vyberu neni oddelen od paintu textoveho obsahu.
6. Text flow kolem obrazku a budoucich shape objektu je stale vnimatelny jako slozity DOM/render kompromis misto jednoho layout stromu.
7. Toolbar stav a runtime selection jsou sice lepsi nez legacy, ale porad nejsou navrzene nad jednim canvas command dispatcherem od zacatku.
8. Screenshot gates existuji pro mnoho regresi, ale nejsou zatim povinnou vstupenkou pro kazdou viditelnou canvas zmenu.
9. Accessibility mirror neni samostatny prvotridni kontrakt canvas enginu; musi byt explicitni, testovatelny a synchronizovany s modelem.
10. Soucasny render neposkytuje produktovy pocit "dokumentove plochy" s garantovanym oddelenim page cache, overlays, diagnostics a foreign cursors.

### Faze 0 before redesign screenshot baseline

E2E baseline soubor `tests/Tempo.Blazor.E2E/DocumentEditorCanvasEngineBaselineE2ETests.cs` uklada aktualni stav do:

```text
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/before-redesign/2026-06-04/
  desktop-1440x1000/
    00-current-core-full.png
    01-current-core-editor.png
    manifest.json
  canvas-flag-red/
    manifest.json
```

Baseline evidence musi zachytit realny `/document-editor` v prohlizeci, skutecny render engine z atributu `data-render-engine`, dostupnost hostu `document-core-engine-host`/`document-wysiwyg-host`, nepristupnost `document-canvas-engine-host` a rucni UX poznamky z teto sekce.

### Faze 0 top 20 human scenarios

Canvas engine musi zvladnout lepe nez core i legacy techto 20 lidskych scenaru:

1. Otevrit dokument, okamzite kliknout doprostred odstavce a zacit psat bez posunu caretu.
2. Psat rychle dlouhou vetu na hranici zalomeni radku bez prekryvu textu a bez pozdniho reflow skoku.
3. Vybrat text mysi pres vice radku, pouzit bold a videt stabilni selection i spravny toolbar stav.
4. Pouzit keyboard selection se Shift+Arrow a formatovat bez ztraty logical selection tokenu.
5. Vlozit odstavec Enterem mezi dva bloky a vratit zmenu jednim Undo.
6. Smazat text Backspace/Delete pres hranici runu, bloku a revizi bez roztrzeni modelu.
7. Kliknout vedle obtékaneho obrazku a psat do dostupneho textoveho intervalu, ne vybrat obrazek omylem.
8. Presunout a zmenit velikost obrazku, ulozit, reloadnout a dostat stejnou geometrii.
9. Vlozit tabulku, prejit bunky Tab/Shift+Tab a vybrat rozsah bunek bez selection driftu.
10. Zmenit sirku sloupce tabulky a videt okamzity text reflow uvnitr bunek.
11. Zapnout track changes, psat, mazat, accept/reject a zachovat citelny markup.
12. Pridat komentar k vyberu, kliknout thread v railu a centrovat spravnou anchor oblast.
13. Pouzit find/replace a videt highlighty jako canvas overlay bez rozbiti selection/caret.
14. Vlozit heading, zobrazit outline, kliknout outline item a presunout caret na spravny blok.
15. Vlozit table of contents, prejmenovat heading, update TOC a zachovat navigacni vazbu.
16. Kopirovat/vlozit rich text z Word/Google Docs a dostat cisty model, ne nalepeny DOM chaos.
17. Pouzit IME, diakritiku, emoji a grapheme clustery bez rozbiteho offsetu.
18. Zoomovat na fit width a custom percent bez rozmazaneho textu a bez prekryvu toolbaru.
19. Pracovat v mobilnim viewportu s toolbar overflow bez zakryti caretu, selection a menu.
20. Ulozit, reloadnout, exportovat a porovnat dokument tak, aby vsechny viditelne zmeny odpovidaly modelu.

## Faze 1: Clean-room architektura a technicky spike bez produktu

- [x] Popsat architekturu `CanvasDocumentEngine`: model store, layout service, display list, canvas renderer, input controller, command dispatcher, history, interop.
- [x] Definovat, ktere canvas vrstvy existuji: page background, text/content, selection/caret, objects, annotations, diagnostics.
- [x] Rozhodnout mezi jednim canvasem na viewport vs canvas per visible page.
- [x] Definovat high-DPI scaling, zoom a pixel snapping pravidla.
- [x] Definovat accessibility mirror: semantic text, paragraphs, headings, tables, comments, revisions.
- [x] Definovat hidden input bridge pro keyboard/IME bez contenteditable jako autority.
- [x] RED JS unit testy pro vytvoreni engine instance bez Blazoru.
- [x] GREEN minimalni `createCanvasDocumentEngine({host, model})`.
- [x] Screenshot spike: prazdna A4 stranka na canvasu, non-blank assertion, high-DPI sanity.
- [x] UX review: stranka vypada jako dokumentova plocha, ne jako debug canvas.

### Faze 1 clean-room architektura

Implementacni vstup je `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.mjs`, ktery exportuje `createCanvasDocumentEngine({ host, model })`. Faze 1 je technicky spike bez napojeni na verejny `TmDocumentEditor`; verejny Blazor host a render flag zustavaji az pro Fazi 3.

Architektura `CanvasDocumentEngine` je slozena z techto vrstvenych sluzeb:

1. **Model store** (`model/model-store.mjs`) drzi normalizovany canvas document model a verzi modelu. Model zustava jedina pravda; DOM ani canvas nic neperzistuji.
2. **Layout service** (`layout/page-geometry.mjs`) prevadi model + page settings na page layout. Faze 1 definuje A4-like prazdnou stranku a viewport/visible-page kontrakt; text layout zacina az ve Fazich 5-7.
3. **Display list boundary** je v Fazi 1 kontrakt v pipeline `model-store -> layout-service -> display-list -> canvas-renderer`; plny display-list API vznikne ve Fazi 5.
4. **Canvas renderer** (`render/canvas-stack.mjs`) vytvari canvas-per-visible-page stack a kresli page background, okraj stranky a margin guide jako neblank intentional render.
5. **Input controller** (`input/hidden-input-bridge.mjs`) vytvari skryty `textarea` bridge pro `beforeinput`/IME bez `contenteditable` autority.
6. **Command dispatcher** (`commands/command-dispatcher.mjs`) poskytuje jednotny registrovatelny command boundary pro ribbon, mini toolbar, context menu, shortcuts a interop v dalsich fazich.
7. **History** (`history/history-store.mjs`) definuje undo/redo transaction store jako hranici pro budouci transakcni commandy.
8. **Interop bridge** (`interop.mjs`) poskytuje marshalable ready/snapshot/focus/destroy boundary pro budouci Blazor host.

### Faze 1 canvas vrstvy

Kazda viditelna stranka ma vlastni sadu canvas vrstev v pevnem poradi:

1. `page-background` - papir, hranice stranky, margin guides, page background.
2. `content` - text, tabulky, field resulty a staticky dokumentovy obsah.
3. `objects` - inline/floating obrazky, tvary, grafy a object handles v pozdejsich fazich.
4. `selection-caret` - caret, selection recty a focus ringy bez prekreslovani content layeru.
5. `annotations` - komentare, revize, search highlighty a foreign cursors.
6. `diagnostics` - proofing squiggles, layout diagnostics a volitelne debug overlays.

### Faze 1 rozhodnuti: canvas per visible page

Zvoleno je **canvas per visible page**, ne jeden canvas na cely viewport. Duvody:

- prirozene mapovani na dokumentove stranky, per-page cache a incremental recalc,
- levnejsi invalidace overlay vrstev pri pohybu caretu/selection,
- jednodussi screenshot/pixel gate na konkretni stranku,
- priprava na virtualizaci velkych dokumentu a page-level dirty tracking.

### Faze 1 HiDPI, zoom a pixel snapping

Canvas backing store se nastavuje jako `cssWidth * devicePixelRatio` a `cssHeight * devicePixelRatio`; 2D context dostane `setTransform(dpr, 0, 0, dpr, 0, 0)`. CSS rozmery zustavaji v dokumentovych px. Ostré linky se kresli na `.5` souradnicich pro 1px stroke v CSS prostoru. Zoom bude ve Fazich 5/E11 aplikovan jako layout/render scale nad stejnym pravidlem: CSS page size = logical page size * zoom, backing store = CSS size * DPR.

### Faze 1 accessibility mirror

Canvas neni semanticky dokument. Engine proto vytvari `document-canvas-a11y-mirror` s `role="document"` mimo vizualni tok. Mirror je synchronizovan z modelu a obsahuje blokove semanticke uzly (`p`, pozdeji headings/tables/comments/revisions). Faze 1 overuje paragraph mirror; Faze 21 doplni plny screen-reader kontrakt.

### Faze 1 hidden input bridge

Engine nepouziva `contenteditable`. Klavesnice a IME vstup jdou pres skryty `textarea` s `beforeinput` listenerem. Bridge je samostatna sluzba, umi focus a subscription na input payloady; editacni model mutace se pripoji ve Fazi 8.

### Faze 1 test a screenshot evidence

Unit JS testy jsou v `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs`. Browser screenshot spike je v `tests/Tempo.Blazor.E2E/DocumentEditorCanvasEnginePhase1E2ETests.cs` a staticky harness v `src/Tempo.Blazor.Demo/wwwroot/canvas-engine-harness.html`.

E2E evidence se uklada do:

```text
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase1-spike/2026-06-04/
  desktop-1440x1000/
    00-phase1-empty-a4-full.png
    01-phase1-empty-a4-engine.png
    manifest.json
```

Rucni UX verdikt pro Fazi 1: screenshot ma pusobit jako zamyslena prazdna dokumentova stranka - centrovana, ostra, klidna, bez debug vzhledu a bez UI overlapu.

## Faze 2: Test harness a screenshot evaluator

- [x] Vytvorit E2E helper `OpenCanvasEngineDocumentAsync`.
- [x] Vytvorit screenshot helpery pro full page, editor surface, canvas crop a focused UI control.
- [x] Vytvorit pixel helper pro non-blank canvas.
- [x] Vytvorit rect helpery pro text overlap, UI overlap a viewport clipping.
- [x] Vytvorit `CanvasVisualReviewManifest`.
- [x] Vytvorit `DocumentEditorCanvasVisualAssert`.
- [x] Pridat `view_image` postup do agent workflow: po kazdem screenshot E2E otevrit after screenshot a zapsat UX/UI verdikt do planning notes nebo test outputu.
- [x] RED E2E: blank canvas failne.
- [x] GREEN E2E: minimalni page canvas projde.
- [x] Akceptace: zadna dalsi viditelna faze nesmi byt bez screenshot gate.

### Faze 2 test harness a screenshot evaluator

Sdilena infrastruktura je v `tests/Tempo.Blazor.E2E/CanvasEngine/`:

1. `CanvasEngineTestBase` otevira staticky canvas harness pres `OpenCanvasEngineDocumentAsync(seedId, viewport)`, nastavuje viewport a uklada vystupy do `TestResults/document-editor-canvas/{class}/{test}/{viewport}/`.
2. `CanvasEnginePage` poskytuje page object pro `Host`, `Editor`, `Canvas`, `OverlayCanvas`, `Toolbar`, `A11yMirror`, `HiddenInput` a screenshot metody `CaptureFullAsync`, `CaptureEditorAsync`, `CaptureCanvasCropAsync`, `CaptureControlAsync`.
3. `CanvasPixelMetrics` a `CanvasPixelDelta` drzi deterministicke metriky z canvas backing storu.
4. `DocumentEditorCanvasVisualAssert` poskytuje gates pro non-blank canvas, zmenu pixelu, caret, selection, text/UI overlap, toolbar stav a screenshot review.
5. `CanvasVisualReviewManifest` serializuje test, viewport, seed, akce, ocekavane viditelne/modelove zmeny, screenshot paths, metriky a `uxReviewerNotes`.

Staticky harness `src/Tempo.Blazor.Demo/wwwroot/canvas-engine-harness.html` ma stabilni host `data-testid="document-canvas-engine-host"` a prijima `seedId` z query stringu. Faze 3 na tento kontrakt navaze realnym Blazor hostem.

E2E smoke a RED/GREEN gate kontrakt jsou v `tests/Tempo.Blazor.E2E/DocumentEditorCanvasHarnessE2ETests.cs`. Smoke bezi na matici viewportu:

```text
desktop-1440x1000
notebook-1280x800
tablet-900x1100
mobile-390x844
```

Screenshot evidence se uklada napr. do:

```text
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/DocumentEditorCanvasHarnessE2ETests/
  Phase2_OpenCanvasEngineDocument_CapturesScreenshotsManifestAndPassesSmokeGates/
    desktop-1440x1000/
      00-before-full.png
      01-before-editor.png
      02-after-full.png
      03-after-editor.png
      04-canvas-crop.png
      05-focused-control.png
      manifest.json
```

Agent UX/UI workflow pro dalsi viditelne faze: po screenshot E2E otevrit `03-after-editor.png` nebo ekvivalentni finalni after screenshot pres `view_image`, overit ostrost, cistotu stranky, citelnost, caret/selection viditelnost a absenci text/UI overlapu, potom zapsat verdikt do `uxReviewerNotes` v manifestu nebo do planning notes.

Rucni UX verdikt pro Fazi 2: desktop after screenshot pusobi jako zamyslena prazdna dokumentova plocha - ostra bila stranka, jemne okraje a margin guide, zadny overlap, zadny debug vzhled.

## Faze 3: Blazor host a render flag

- [x] Pridat enum hodnotu `DocumentEditorRenderEngine.CanvasEnginePreview`.
- [x] Vytvorit `TmDocumentCanvasEngineHost`.
- [x] Host renderuje canvas stack, accessibility mirror root a hidden input bridge.
- [x] Host ma `data-testid="document-canvas-engine-host"`.
- [x] Host prijima `DocumentEditorDocument`, `ReadOnly`, permissions, providers potrebne pro object/image flows.
- [x] Host expose interop: ready, changed, formatting state, undo state, selection state, diagnostics.
- [x] `TmDocumentEditor` umi pri `CanvasEnginePreview` renderovat novy host.
- [x] RED bUnit: flag renderuje canvas host, ne legacy/core host.
- [x] GREEN bUnit: flag a dispose lifecycle.
- [x] E2E screenshot: demo route s canvas hostem zobrazi prazdny/seed dokument.

### Faze 3 implementacni poznamky

`CanvasEnginePreview` je explicitni opt-in render engine. Default zustava beze zmeny a preview vetve se aktivuji jen pri primem pozadavku. `TmDocumentCanvasEngineHost` je Blazor shell nad clean-room `CanvasDocumentEngine`: drzi sest canvas vrstev, accessibility mirror, hidden textarea bridge, JS lifecycle a stavove metody pro dirty/formatting/undo/selection/diagnostics. Ready callback je idempotentni, dispose lifecycle uvolnuje JS handle i ES module.

Test evidence:

- `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore` - zeleny build.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - zeleny build.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~CanvasEngineHostRenderTests|FullyQualifiedName~DocumentEditorRenderEngineFlagTests" --no-restore --no-build` - 8/8.
- `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs` - 4/4.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasHostE2ETests" --no-restore --no-build` - 1/1.

Screenshot evidence:

```text
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase3-host/2026-06-04/desktop-1440x1000/
  00-phase3-full.png
  01-phase3-editor.png
  02-phase3-canvas-host.png
  03-phase3-canvas-page.png
  manifest.json
```

Rucni UX verdikt pro Fazi 3: `03-phase3-canvas-page.png` vypada jako zamyslena prazdna dokumentova plocha - ostra bila stranka, jemny margin guide, zadny debug vzhled ani UI overlap.

## Faze 4: Canonical canvas model a converter

- [x] Navrhnout internal canvas model odvozeny z `DocumentEditorDocument`, ale optimalizovany pro layout: document, section, page settings, block tree, inline runs, marks, objects.
- [x] Converter `DocumentEditorDocument -> CanvasDocumentModel`.
- [x] Converter `CanvasDocumentModel -> DocumentEditorDocument`.
- [x] Preserve channel pro nemodelovane vlastnosti.
- [x] Unit RED/GREEN: paragraph, heading, list, quote.
- [x] Unit RED/GREEN: table with spans.
- [x] Unit RED/GREEN: standalone image + inline drawing.
- [x] Unit RED/GREEN: page break.
- [x] Unit RED/GREEN: header/footer, fields, footnotes/endnotes.
- [x] Unit RED/GREEN: comments/revisions/bookmarks/unknown marks.
- [x] E2E save/reload: seed doc projde do canvas modelu a zpet bez ztraty textu, tabulek, obrazku a metadat.

### Faze 4 implementacni poznamky

Canvas DTO a converter jsou v `Tempo.Blazor.Abstractions`, aby provider/API vrstva nemusela referencovat Blazor UI projekt. `CanvasDocumentModel` obsahuje document metadata, CSS-pixel page settings, sections, body block tree, inline runs, marks, table/image/drawing/page-break payloady, header/footer, notes, comments, revisions, assets, anchors a restricted markers. `CanvasPreserveChannel.SourceJson` drzi puvodni zdrojovy JSON na dokumentu, sekcich, page settings, blocich, bunkach, runech a znaceni, aby canvas round-trip neodhodil pole, ktera runtime jeste primo needituje.

`TmDocumentCanvasEngineHost` uz neposila prazdny shell model; mountuje realny `CanvasDocumentModel` pres `CanvasDocumentModelConverter.ToCanvasModel(...)`. JS runtime ma `canvas-document-model.mjs` jako jedno normalizacni misto pravdy a `model-store.mjs` ho pouziva pro vsechny vstupni modely.

Test evidence:

- `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore` - zeleny build.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - zeleny build.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~CanvasModelConverterTests|FullyQualifiedName~CanvasEngineHostRenderTests" --no-restore --no-build` - 9/9.
- `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/model/__tests__/model.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs` - 6/6.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasModelRoundtripE2ETests" --no-restore --no-build` - 1/1.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasHostE2ETests" --no-restore --no-build` - 1/1 browser smoke po napojeni realneho canvas modelu.

Faze 4 nema nove vizualni screenshoty pro obsah dokumentu, protoze text/table/image paint zacina az ve Fazi 5. Akceptacni gate pro 4.7 je JSON diff pres realny provider save/reload.

## Faze 5: Canvas render pipeline

- [x] Display list API: text runs, glyph runs, paragraph boxes, table boxes, image boxes, fields, comments/revision overlays.
- [x] Renderer kresli page background, margins, body area.
- [x] Renderer kresli paragraph text v jedne radce.
- [x] Renderer kresli headings a zakladni inline marks.
- [x] Renderer kresli debug overlay volitelne, nikdy defaultne.
- [x] Unit JS: display list je deterministicky pro stejny model.
- [x] E2E screenshot: jeden odstavec vypada ostre, zarovnane a ne rozmazane na devicePixelRatio 1/2.
- [x] UX review: text ma spravnou baseline, page padding a citelnost.

### Faze 5 implementacni poznamky

Render pipeline je rozdelena na `layers.mjs`, `page-frame.mjs`, `display-list.mjs` a `canvas-renderer.mjs`. Canvas stack sestavuje deterministicky display list a maluje page/background/body, content text, headings, marks, object layer a annotation layer. Debug/diagnostics vrstva zustava defaultne prazdna. Host serializuje `CanvasDocumentModel` jako camelCase JSON, aby JS engine dostal skutecny provider model. E2E evidence a screenshoty jsou v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase5-render/2026-06-04/desktop-1440x1000/`.

## Faze 6: Text measurement, line breaking a pagination

- [x] Font resolver: font family fallback, font size, weight, style.
- [x] Canvas text measurement cache.
- [x] Grapheme segmentation.
- [x] Word breaking, soft wrap, hard break.
- [x] Paragraph layout: line boxes, ascender/descender, line height.
- [x] Alignment left/center/right/justify.
- [x] Paragraph spacing before/after, indent, hanging indent.
- [x] Lists: bullet/number label layout.
- [x] Pagination: page capacity, page breaks, widow/orphan follow-up as P2.
- [x] Unit JS: no text overlap for long paragraphs.
- [x] E2E screenshot: long paragraph wraps across lines/pages.
- [x] UX review: radky pusobi jako dokument, ne jako canvas demo.

### Faze 6 implementacni poznamky

Canvas text layout je pres `layout/pagination.mjs` napojeny na existujici paragraph/line breaker stack (`font-metrics`, tokenizer, grapheme, alignment, page metrics). `display-list.mjs` kresli line/segment layout vcetne justify spacingu, list labelu a page fragmentu. `canvas-stack.mjs` renderuje vice page surfaces a vytvari skrytou text-rect metadata vrstvu pro `AssertNoTextOverlapAsync`. E2E evidence a screenshoty jsou v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase6-text-layout/2026-06-04/desktop-1440x1000/`.

## Faze 7: Hit testing, caret a selection

- [x] Mapovani point -> text position.
- [x] Mapovani text position -> caret rect.
- [x] Collapsed caret paint na canvas overlay.
- [x] Range selection recty pres jeden radek.
- [x] Range selection pres vice radku a bloky.
- [x] Mouse click caret placement.
- [x] Drag selection.
- [x] Double-click word, triple-click paragraph.
- [x] Shift+click extension.
- [x] Keyboard arrows, Home/End, PageUp/PageDown.
- [x] Screenshot E2E: caret je viditelny a selection highlight sedi na textu.
- [x] UX review: selection pusobi nativne, bez zpozdeni a bez posunu.

### Faze 7 implementacni poznamky

Canvas selection runtime je v `selection/selection-controller.mjs`; pouziva caret stops z text layoutu, `core-engine/hit-test.mjs`, `caret.mjs`, `selection-overlay.mjs` a word/grapheme helpery. Overlay kresli caret/selection do `selection-caret` canvas vrstvy a syncuje DOM geometrii pro E2E. Pointer gestures resi click, drag, double-click word, triple-click paragraph a Shift+click extension; keyboard resi Arrow/Home/End/PageUp/PageDown, Ctrl/Alt word movement a Shift+rozsireni. E2E evidence a screenshoty jsou v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase7-caret-selection/2026-06-04/desktop-1440x1000/`.

## Faze 8: Input pipeline, IME a immediate typing

- [x] Hidden input bridge prijima keyboard text.
- [x] `beforeinput`/keyboard abstraction bez contenteditable DOM mutaci.
- [x] Insert text na collapsed caret.
- [x] Replace selection by typing.
- [x] Enter paragraph split.
- [x] Shift+Enter soft break.
- [x] Backspace/Delete within run.
- [x] Backspace/Delete across run/block boundaries.
- [x] IME composition preview a commit.
- [x] Emoji/grapheme safe offsets.
- [x] Immediate active-line repaint do 16 ms target.
- [x] E2E real keyboard: typing, Enter, Shift+Enter, backspace.
- [x] Screenshot E2E: text se objevi presne u caretu, okolni layout se neposkoci nesmyslne.

### Faze 8 implementacni poznamky

Input pipeline je napojena pres `input/input-controller.mjs` a modelovou vrstvu `input/text-editing.mjs`. Hidden textarea prijima `beforeinput`, `keydown`, fallback `input` a composition events, bez `contenteditable`. Text editace podporuje insert/replace, Enter split, Shift+Enter soft break, grapheme-safe Backspace/Delete vcetne merge pres hranice bloku a IME preview/commit/cancel. Selection overlay kresli IME pre-edit underline pres `selection-overlay.createCompositionUnderlineElement`.

Layout garantuje caret stop pro prazdny odstavec po Enteru a terminalni caret stop po trailing soft breaku. Input commit pouziva dirty-block incremental repaint; strukturální zmeny prekresluji stranky od prvni dirty stranky dal. E2E evidence a screenshoty jsou v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase8-typing/2026-06-04/desktop-1440x1000/` a `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase8-ime/2026-06-04/desktop-1440x1000/`.

## Faze 9: Command dispatcher a inline formatting

- [x] Jeden command dispatcher pro ribbon, mini toolbar, context menu, shortcuts a public interop.
- [x] Selection token je zachovan pri toolbar pointerdown.
- [x] Bold/italic/underline/strike range command.
- [x] Collapsed pending formatting pro dalsi znak.
- [x] Font family/size range command.
- [x] Text color/highlight command.
- [x] Clear formatting.
- [x] Link apply/remove/open Ctrl/Cmd+click.
- [x] Formatting state: active, mixed, disabled, value.
- [x] Undo/redo pro kazdy command.
- [x] E2E screenshot: pred/po pro bold, color, highlight a font size.
- [x] UX review: toolbar state a canvas render se shoduji, selection se neztraci.

### Faze 9 implementacni poznamky

Canvas command runtime je v `commands/dispatcher.mjs` a inline mark mutace ve `commands/inline-format.mjs`. `entry.mjs` publikuje `data-canvas-command-*` diagnostics, `interop.mjs` vystavuje `execCommand/queryCommand` a `TmDocumentEditor` routuje inline toolbar commandy do canvas hostu pres `ExecCommandAsync`.

Pokryto: bold/italic/underline/strike, collapsed pending marks pro dalsi insert, fontfamily/fontsize s normalizaci `pt` hodnot, textcolor/highlight, clearFormatting, link/removelink/openlink, active/mixed/value state a undo/redo snapshot transakce. E2E `DocumentEditorCanvasInlineFormatE2ETests` pouziva realny vyber mysi, toolbar kliky, selecty, Tempo color pickery, link dialog a Ctrl+click open. Screenshot evidence je v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase9-inline-format/2026-06-04/desktop-1440x1000/`.

## Faze 10: Paragraph commands, styly a ruler

- [x] Align left/center/right/justify.
- [x] Line spacing.
- [x] Spacing before/after.
- [x] Increase/decrease indent.
- [x] Bullet/numbered list toggle.
- [x] List nesting Tab/Shift+Tab mimo tabulku.
- [x] Heading/block style apply.
- [x] Heading level commandy `Heading 1` az `Heading 6` meni semanticky typ/level, ne pouze font size.
- [x] Style resolver pro nadpisy inspirovany document-editor UX z ONLYOFFICE: jasne odlisit name, based-on, outline level a direct formatting.
- [x] Mixed heading/paragraph selection publikuje spravny toolbar mixed state.
- [x] Zmena textu nadpisu invaliduje outline/TOC cache.
- [ ] Presun nebo smazani nadpisu invaliduje outline/TOC cache. _(Hotovo pro smazani pres text/input pipeline; presun ceka na samostatny move command.)_
- [x] RED/GREEN unit: heading style round-trip zachova level, style id/name a inline formatting.
- [x] RED/GREEN E2E: aplikovat `Heading 1`, `Heading 2`, ulozit/reloadnout a overit stejny canvas render i model.
- [x] Screenshot E2E: nadpisy maji profesionalni hierarchii, spacing a citelny rozdil oproti body textu.
- [x] Quote style.
- [x] Ruler visual state a margin/indent handles.
- [x] Show blocks/non-printing characters.
- [x] Screenshot E2E: alignment/list/indent pred/po.
- [x] UX review: odstavce maji spravnou hustotu a dokumentovou citelnost.

## Faze 11: Clipboard

- [x] Copy selected text as plain/html/internal model.
- [x] Cut with one undo transaction.
- [x] Paste internal fragment.
- [x] Paste plain text.
- [x] Paste rich text from Word/GDocs normalizers.
- [x] Paste URL -> link policy.
- [x] Paste image -> image provider flow, pokud provider povoli.
- [x] Clipboard debug modal stale funguje.
- [x] E2E real clipboard/DataTransfer: copy, cut, paste, paste plain.
- [x] Screenshot E2E: pasted rich text vypada konzistentne, ne jako rozbity HTML import.

## Faze 12: History, dirty state, save a autosave

- [x] History stack s transactions.
- [x] Typing coalescing po slovech/casovem okne, ne po kazdem renderu.
- [x] Undo/redo text edits.
- [x] Undo/redo formatting.
- [x] Undo/redo tables/images/comments/revisions.
- [x] Dirty state z model version, ne ze scroll/renderu.
- [x] Manual save taha aktualni canvas model.
- [x] Autosave debounce.
- [x] Save conflict/retry/offline draft.
- [x] Before-unload guard.
- [x] E2E save/reload pro kazdou hlavni kategorii: text, formatting, table, image, comments, revisions.
- [x] Screenshot E2E: po reloadu dokument vypada stejne jako pred reloadem.

## Faze 13: Toolbar shell, mini toolbar a context menu

- [x] Ribbon vsechny commandy napojene na canvas dispatcher.
- [x] Compact toolbar parity.
- [x] Distraction-free toolbar parity.
- [x] Mini toolbar nad selection.
- [x] Text context menu.
- [x] Link context menu.
- [x] Table context menu.
- [ ] Image context menu.
- [x] Spellcheck suggestions context menu.
- [x] Proofing service API inspirovane ONLYOFFICE UX: engine zada text spans + language, provider/worker vraci misspelling diagnostics bez mutace modelu.
- [x] Canvas render kresli pravopisne cervene vlnovky jako overlay, ne jako inline style v textu.
- [x] Context menu nad misspelling ukazuje navrhy, `Ignore once`, `Ignore all`, `Add to dictionary` pokud provider podporuje.
- [x] Klik na navrh opravy vola transakcni replace command a je undoable.
- [ ] Spellcheck diagnostics se po editaci blizkeho textu invaliduji jen v dotcenem rozsahu.
- [ ] Spellcheck respektuje read-only, protected ranges, comments/revisions visibility a language metadata.
- [x] RED/GREEN unit: misspelling range se mapuje na canvas recty i pres zalomeni radku.
- [x] RED/GREEN E2E: spatne slovo ma squiggle, context menu nabidne opravu, oprava zmeni text a squiggle zmizi.
- [x] Screenshot E2E: vlnovka je citelna, neprekryva baseline a context menu neprekryva caret/selection spatne.
- [x] Keyboard shortcuts manager.
- [x] Command palette integration.
- [x] E2E real pointer: toolbar click nesmaze selection.
- [x] Screenshot E2E: zadny menu/panel neprekryva vyber, caret ani podstatny obsah nesmyslne.

## Faze 14: Tables

- [x] Render table grid na canvasu.
- [x] Text layout uvnitr cells.
- [x] Cell hit-test.
- [x] Cell caret/selection.
- [x] Tab/Shift+Tab navigation.
- [x] Insert row/column.
- [x] Delete row/column.
- [x] Merge/split cells horizontal/vertical.
- [x] Column resize drag.
- [x] Cell range selection.
- [x] Cell formatting background/alignment/vAlign.
- [x] Save/reload table.
- [x] DOCX roundtrip table parity gate.
- [x] Screenshot E2E: tabulka vypada jako editor table, ne jako HTML fallback.

## Faze 15: Images and drawings

- [x] Standalone image render.
- [x] Inline drawing render.
- [x] Image URL resolver.
- [x] Upload/provider asset insert.
- [x] Selection handles.
- [x] Resize with aspect lock.
- [x] Move/drag.
- [x] Wrap modes: inline, square, tight/through where model supports, top-bottom, behind/in-front.
- [x] Z-order.
- [x] Caption.
- [x] Alt text warning.
- [ ] Image inspector and floating toolbar.
- [x] Save/reload image layout.
- [x] Screenshot E2E: object handles jsou ostre, neprekryvaji toolbar a text obtika bez overlapu.

## Faze 16: Headers, footers, fields, notes and page settings

- [x] Header/footer render per page.
- [x] Click-to-edit header/footer.
- [x] Different first page.
- [x] Different odd/even.
- [x] Page number/page count/page X of Y.
- [x] Date/document title/author fields.
- [x] Footnote/endnote insertion and render.
- [x] Page margins, size, orientation.
- [ ] Page break behavior.
- [ ] Section-like page geometry if current model exposes it.
- [x] Screenshot E2E: header/footer editace je jasna a page geometry vypada profesionalne.

## Faze 17: Comments, revisions and restricted editing

- [x] Add comment to selection.
- [x] Comment highlight render on canvas.
- [x] Comment rail sync.
- [x] Reply, resolve, reopen, delete.
- [x] Select comment -> scroll/caret to anchor.
- [x] Track insertions.
- [x] Track deletions including cross-block.
- [x] Track formatting changes.
- [x] Review display modes markup/final/original.
- [x] Accept/reject one revision.
- [x] Accept/reject all.
- [x] Protect document.
- [x] Editable regions.
- [ ] Suggestions provider boundary, pokud je aktivni.
- [x] Screenshot E2E: comments/revisions jsou citelne, barevne rozlisene a nerusi text.

## Faze 18: Search, replace, outline, bookmarks and navigation

- [x] Find plain text.
- [x] Replace current/all.
- [x] Regex find/replace with backreferences.
- [x] Highlight matches on canvas.
- [x] Navigate next/previous.
- [x] Live region announcements.
- [ ] Bookmarks define/list/go-to. _(List/go-to runtime existuje; define UI + edit-survival test nejsou uzavrene.)_
- [x] Heading outline extraction.
- [x] Outline panel model: seznam nadpisu podle outline levelu, stabilni target block id, page index a y souradnice.
- [ ] Klik v outline scrolluje/caret presune na cilovy nadpis. _(Canvas gotoHeading funguje; samostatny outline-panel E2E klik neni hotovy.)_
- [x] Insert table of contents vlozi semanticky TOC block/field generovany z heading outline, ne obycejny text.
- [x] TOC entries obsahuji level, display text, target block id a page number vypocteny z aktualni layout cache.
- [x] Klik na TOC entry naviguje na cilovy nadpis.
- [x] Update TOC prepocita texty, poradi a page numbers po zmene nadpisu/layoutu.
- [x] TOC je undoable jako jedna transaction pri vlozeni i aktualizaci.
- [x] Save/reload zachova TOC jako aktualizovatelny objekt, ne jako zplosteny odstavec.
- [ ] DOCX import/export smoke zachova heading levels a obsah v provider boundary, kde format provider podporuje TOC/outline metadata.
- [x] RED/GREEN unit: outline extraction ignoruje body text a respektuje Heading 1-6 poradi.
- [x] RED/GREEN unit: TOC generator vytvori zanorene entries a po prejmenovani nadpisu je aktualizuje.
- [ ] RED/GREEN E2E: vytvorit H1/H2, vlozit obsah, kliknout entry, prejmenovat H2, update TOC, ulozit/reloadnout. _(E2E pokryva seed H1/H2, vlozeni, klik entry, update, save/reload; prejmenovani H2 neni soucasti scenare.)_
- [x] Screenshot E2E: active find result je viditelny a nerusi selection/caret.
- [x] Screenshot E2E: obsah vypada jako dokumentovy TOC s odsazenim, voditky/page numbers pokud je zavedeme, a navigace je vizualne srozumitelna.

## Faze 19: Import/export and external formats

- [x] Export current live canvas model through existing provider boundary.
- [x] DOCX export uses current unsaved canvas edits.
- [x] DOCX import maps into canvas model and renders immediately.
- [ ] ODT export/import where provider supports it.
- [x] PDF export through provider with current model.
- [ ] Markdown/HTML export/import where existing providers expose it.
- [x] Compare documents uses current model.
- [x] Debug JSON reflects current canvas model.
- [x] E2E DOCX smoke: text, table, image, header/footer, comment/revision anchors survive.
- [x] Screenshot E2E: imported document has sane first paint and no blank pages.

## Faze 20: Collaboration and offline

- [x] Operation model serializovatelny pro realtime.
- [x] Local op log.
- [x] Remote op apply.
- [x] OT/transform nebo deterministicky merge strategy.
- [x] Remote cursor/presence canvas overlay.
- [x] Conflict handling.
- [x] Offline draft save.
- [x] Offline resume/sync.
- [x] Two-browser live E2E pres Demo API SignalR.
- [x] Screenshot E2E: remote caret je citelny a neprekryva text agresivne.

## Faze 21: Accessibility, localization and keyboard quality

- [x] Accessibility mirror DOM pro screen readers.
- [x] Logical reading order vcetne bidi/RTL.
- [x] Live region pro caret granularity, find, comments, save state.
- [x] Forced-colors/high contrast.
- [x] Keyboard-only editing full smoke.
- [ ] Focus management mezi canvas, toolbar, panels, dialogs.
- [x] ARIA labels pres `ITmLocalizer`.
- [x] Czech/English localization keys.
- [ ] Manual NVDA/VoiceOver gate.
- [x] Screenshot E2E forced-colors where possible.

## Faze 22: Performance and large documents

- [x] Virtualized visible pages.
- [x] Tile/canvas cache invalidation.
- [x] Incremental active paragraph layout.
- [x] Idle full-document reconciliation.
- [x] Text measurement cache bounds.
- [x] Large doc first paint target.
- [x] Typing latency p50/p95 metrics.
- [x] Scroll smoothness metrics.
- [x] Memory leak tests over repeated open/close.
- [x] E2E perf screenshot: first paint is not blank and visible pages fill progressively.

## Faze 23: UX/UI polish gate

- [x] Editor surface density, page shadows, margins and toolbar spacing reviewed.
- [x] Canvas text sharpness reviewed on 1x/2x DPR.
- [x] Selection color, caret color and comment/revision colors reviewed in light/dark.
- [x] Image handles, table handles and context menus reviewed for touch/mouse.
- [x] No cards-inside-cards in editor shell.
- [x] Text never overflows buttons/dropdowns.
- [x] Mobile toolbar overflow usable.
- [x] Empty/loading/error states professional.
- [x] Screenshot gallery for top 20 scenarios accepted by agent UX/UI review.
- [ ] Human smoke accepted by user before default cutover.

## Rozsirena feature parita: Word / Google Docs / OnlyOffice uroven (faze E1-E12)

Faze 0-23 + parity (Faze 24) pokryvaji to, co umel **legacy** `TmDocumentEditor`. To staci na bezpecny cutover z legacy, ale NESTACI na uzivateluv cil "stejne kvalitni jako Google Docs / Word Online / OnlyOffice". Nasledujici faze E1-E12 doplnuji funkce, ktere legacy z velke casti nemel, ale ktere maji vsechny tri referencni editory. Vsechny jsou clean-room (ONLYOFFICE jen architektonicka inspirace).

Poradi a gating:

- [ ] Faze E1-E12 pouzivaji stejnou Definici hotovo, screenshot gates a save/reload/undo gates jako faze 0-26.
- [ ] Doporucene poradi implementace: 0-16, pak E1-E6, pak 17-18, pak E7-E12, pak 19-23, pak 24 parity (vcetne E1-E12), pak 25 cutover.
- [ ] Faze 25 (default cutover) se NESMI prohlasit za "kvalita GDocs/Word/OnlyOffice", dokud nejsou E1-E12 hotove; muze ale defaultnout driv jako "legacy-parity preview", pokud to uzivatel explicitne schvali.
- [ ] Kazda E faze ma vlastni radek v parity suite (Faze 24) a vlastni acceptance gallery scenar.

### Faze E1: Numbering, multilevel lists a list styly

- [x] Numbering definition model: abstract num, level 0-8, format (decimal, lower/upper-roman, lower/upper-letter, bullet, none), text template (`%1.%2`), start-at, suffix, indent/hang per level.
- [x] Apply bullet/numbered list, change list level Tab/Shift+Tab, set bullet/number format z pickeru.
- [x] Restart numbering, continue numbering, set numbering value na konkretnim odstavci.
- [x] Legal/multilevel numbering (1, 1.1, 1.1.1) a navaznost mezi odstavci.
- [x] List label layout na canvasu (sirka labelu, zarovnani, hanging indent) bez prekryvu s textem.
- [x] List style reference + define-new-list-style.
- [x] RED/GREEN unit: number sequence po vlozeni/smazani/presunu odstavce; restart/continue.
- [x] RED/GREEN E2E + screenshot: multilevel seznam vypada jako Word, cisla sedi, save/reload zachova numbering. Undo gate.

### Faze E2: Tab stops a pravitko (ruler) interakce

- [ ] Tab stop model na odstavci: pozice, alignment left/center/right/decimal/bar, leader.
- [ ] Default tab width a tab posun na canvasu (znak Tab posune caret na dalsi stop).
- [ ] Decimal tab zarovnava cisla na desetinnou carku.
- [ ] Ruler: tab stop type picker, klik na pravitko vlozi tab, drag tab, double-click otevre Tabs dialog.
- [ ] Indent markery (first-line, hanging, left, right) drag na pravitku meni odstavec.
- [ ] RED/GREEN unit: tab advance + decimal alignment; ruler px <-> document units.
- [ ] RED/GREEN E2E + screenshot: tabulatorem zarovnany sloupec cisel, leader dots, save/reload. Undo gate.

### Faze E3: Sekce, sloupce, line numbering a page setup

- [x] Section model + section break next-page/continuous/even/odd.
- [x] Per-section page size, margins, orientation; mix portrait/landscape v jednom dokumentu.
- [x] Multi-column layout: count, width, spacing, separator line; text tece mezi sloupci; column break.
- [x] Line numbering (continuous/per-page/per-section, restart, increment).
- [x] Layout/recalc zna sekce a sloupce (per-page canvas cache respektuje column geometrii).
- [x] RED/GREEN unit: column flow, section break pagination, line numbering.
- [x] RED/GREEN E2E + screenshot: dvousloupcovy text + landscape sekce vypadaji profesionalne, save/reload.
- [ ] Newspaper-style column balance.
- [x] Page setup dialog + undo gate.

### Faze E4: Styly - management, galerie a typy stylu

- [x] Style store: paragraph, character, table, list styly; id, name, based-on, next, primary/quick flag.
- [x] Style resolver: inheritance (based-on chain) + direct formatting override delta; pristup z toolbaru jako mixed/active state.
- [x] Style gallery / quick styles, apply style, modify style, create style from selection, delete style, rename, default formatting reset.
- [x] Heading 1-6 a Normal/Quote napojene na realne styly (ne jen font size) - sjednoti se s Faze 10.
- [x] Zmena stylu prepocita vsechny odstavce, ktere ho pouzivaji (recalc-info invalidace).
- [x] RED/GREEN unit: based-on inheritance, update style propaguje, round-trip style id/name.
- [x] RED/GREEN E2E + screenshot: galerie stylu, modify style zmeni cely dokument, save/reload. Undo gate.

### Faze E5: Fields, cross-reference, captions a bibliografie

- [x] Field engine: instrText + cached result + update; PAGE, NUMPAGES, DATE, TIME, FILENAME, AUTHOR, STYLEREF.
- [x] Cross-reference: REF na heading/bookmark/caption/numbered item; klik -> skok; update po zmene cile.
- [x] Caption (figure/table/equation) se SEQ auto-cislovanim; vlozeni/smazani precisluje.
- [x] Table of figures jako generovany aktualizovatelny field.
- [x] Bibliography/citations (pokud provider podporuje) nebo aspon model + nahradni render vystup.
- [x] Update field / update all fields command.
- [ ] Print/export aktualizuje fieldy.
- [x] RED/GREEN unit: SEQ renumber, REF resolve, page-number field po repaginaci.
- [x] RED/GREEN E2E + screenshot: caption + cross-reference + table of figures, update, save/reload. Undo gate.
- [ ] E2E prejmenovat cil cross-reference a update.

### Faze E6: Pokrocile znakove formatovani a change case

- [x] Subscript, superscript (s baseline shift a font scale), small caps, all caps, double strikethrough.
- [x] Character spacing (expanded/condensed), character scale, kerning toggle.
- [x] Change case command (UPPER, lower, Sentence, Capitalize Each Word, tOGGLE).
- [x] Increase/decrease font size step, clear character formatting (sjednotit s Faze 9).
- [x] Canvas render: sub/superscript spravna baseline, small caps glyph scaling, spacing mezi glyphy.
- [x] RED/GREEN unit: baseline/scale metriky, spacing advance.
- [x] RED/GREEN E2E + screenshot: H2O s subscript, x^2 superscript, small caps nadpis, save/reload. Undo gate.

### Faze E7: Tvary, textova pole, cary, grafy a drawings nad ramec obrazku

Detailni plan: `planning/tmdocumenteditor-canvas-e7-shapes-textboxes-charts-tdd-todo-2026-06-04.md` (fáze E7.0-E7.13, vlastni RED/E2E/screenshot gates).

- [x] Drawing object model nad ramec image: shape/auto-shape (geometry presets), text box (vlastni odstavcovy obsah), line/arrow/connector, chart, group.
- [x] Render shape fill/stroke/effects na canvasu; text uvnitr shapeu/text boxu prochazi stejnym layout enginem.
- [x] Anchor inline vs floating, wrap modes (sjednotit s Faze 15), z-order, rotace, group/ungroup, align/distribute.
- [x] Insert shape/text box/line/chart z toolbaru; object toolbar/inspector pro tvar.
- [x] Hit-test, selection handles, resize (aspect lock), move, rotate handle pro tvary.
- [x] Chart: aspon render z modelu + data table editor nebo provider boundary; pokud plny chart engine mimo scope, render jako obrazek-fallback s editovatelnymi daty oznacit jako P2.
- [x] RED/GREEN unit: shape geometry bbox, text box layout, group transform.
- [x] RED/GREEN E2E + screenshot: text box s textem + sipka + zakladni graf, resize/move, save/reload. Undo gate.
  - [x] E7 prvni rez: zakladni canvas render shape/textBox/line/chart, undoable insert commandy pres dispatcher, C# converter round-trip, E2E screenshot + save/reload pro seed a vlozeny text box.
  - [x] E7 dodělávka: textbox layout přes sdílený paragraph engine, group/ungroup/align/distribute commandy, group transform child objektů a Playwright screenshot gate pro group wrapper.
  - [x] E7 dodělávka: clean-room stretch guides pro sipky/callouty a group z-order propagace na wrapper i child drawing objekty, overeno JS unit suite + Playwright screenshot `13-phasee7-group-zorder-front.png`.
  - [x] E7 finalizace: nested textbox editace, pointer/keyboard/delete gate, connector endpoint drag, object clipboard, nested group transform/z-order, DOCX DrawingML smoke, parity matrix a DPR 2 screenshot gate.

### Faze E8: Matematika / rovnice (equation editor)

Detailni plan: `planning/tmdocumenteditor-canvas-e8-math-equations-tdd-todo-2026-06-04.md` (fáze E8.0-E8.10, vlastni RED/E2E/screenshot gates).

- [x] Equation model (OMML-like): fraction, radical, superscript/subscript, n-ary (sum/integral), matrix, function, accent, delimiter, bar; inline vs display.
- [x] Equation layout engine na canvasu (math typesetting: baseline, nadsazeni, velikosti zlomku/indexu).
- [x] Equation input: klik do rovnice, navigace mezi sloty (sipky/Tab), sablony z gallery, symbol palette, linear input -> struktura.
- [x] Caret a selection uvnitr rovnice; backspace/delete v math slotech.
- [x] RED/GREEN unit: math layout metriky pro zlomek/odmocninu/matici; slot navigace.
- [x] RED/GREEN E2E + screenshot: vlozit zlomek a integral, upravit, inline i display, save/reload. Undo gate.
- [x] E8 prvni produkcni rez: clean-room math model + Document/Canvas DTO roundtrip, JS model normalizace, deterministicky layout/render pro fraction/radical/sup/sub/nary/matrix, insertEquation/insertFraction/insertRadical/insertSuperscript/insertSubscript/insertMatrix pres undoable dispatcher, linear parser zakladnich vzoru, canvas E2E seed + insert + save/reload + screenshot.
- [x] E8 slot command/runtime prvni rez: math slot path model, path -> caret rect, slot next/previous, insert/delete text ve slotu, add matrix row/column pres undoable dispatcher, live-region hlaseni aktivniho slotu a E2E desktop/tablet/mobil screenshoty.
- [x] E8 dodělávka: klikací hit-test slotů z canvas layout snapshotu, math caret/selection overlay, keyboard/IME routing, slot replacement pro symboly/lineární šablony, rozšířená equation gallery a E2E screenshot suite `DocumentEditorCanvasMathEquationsE2ETests` (full filtr ověřen 3/3 včetně responsive screenshotů).
- [x] E8 dodělávka 2026-06-07: strukturální Backspace/Delete unwrap math parent strukturu s undo/redo, inline nary hit-test preferuje expression slot při překryvu limitů, real-keyboard E2E edituje sumu a responsive screenshoty se pořizují bez side panel překrytí.
- [x] E8 finální dodělávka 2026-06-07: nary/delimiter insert commandy, inline/display přepínání, strukturální cross-slot selection, výstup z math slotu s live-region oznámením, samostatný Math ribbon tab, save/reload pro nové typy, DOCX smoke, parity row a clean-room PR poznámka.
- [ ] Pozn.: math je velka subdomena; rozdelit na E8a struktury, E8b layout, E8c input pokud bude treba.

### Faze E9: Content controls / strukturovane tagy / vyplnitelne formulare

- [x] Structured document tag model: block i inline; plain text, rich text, combo box, drop-down, date picker, checkbox, picture, repeating section.
- [x] Placeholder text, tag/alias, lock (content/delete), required/format mask.
- [ ] Render content control na canvasu (border/highlight v design modu, plain v form modu), forms-fill mode.
  - [x] E9 prvni produkcni rez: inline/block SDT metadata v canvas modelu, form-control display-list/render metadata, text/checkbox/drop-down/date/picture display text a validacni atributy.
- [ ] Interakce: tab mezi poli, edit text, combo/drop-down vyber, date picker, checkbox toggle, picture insert.
  - [x] E9 prvni produkcni rez: undoable commandy pro edit text, checkbox toggle a drop-down vyber; lock enforcement; body/section projekce zustava synchronni.
- [x] RED/GREEN unit: SDT serialize round-trip, value get/set, lock enforcement.
- [x] RED/GREEN E2E + screenshot: formular s text/checkbox/drop-down, vyplnit, save/reload, forms mode. Undo gate.

### Faze E10: Autocorrect, autoformat, format painter a symboly

- [x] Format painter: copy formatting (char + para), one-shot i lock; engine-level, undoable.
- [x] Autocorrect: replace-as-you-type tabulka, smart quotes, auto-capitalize, ordinal/fraction.
- [x] Autoformat-as-you-type: auto-bullet/auto-number, auto-hyperlink, horizontal line, autoreplace -> undo vraci puvodni.
- [x] Insert symbol / special character engine command, emoji payload, non-breaking space, optional hyphen, em/en dash.
- [x] Blazor symbol / special character paleta a emoji picker UI.
- [x] Vse prochazi command dispatcherem a je undoable (vcetne automatickych nahrad jako 1 undo krok).
- [x] RED/GREEN unit: autocorrect tabulka, smart-quote kontext, format painter delta.
- [x] RED/GREEN E2E + screenshot: napsat `--` -> em dash, `1.` + space -> seznam, format painter prenese styl, symbol vlozeni. Undo gate.

### Faze E11: View modes, zoom a print/print preview

- [x] View modes: print layout (default), reading mode, web layout, outline view; prepinani bez ztraty stavu.
- [x] Zoom presety: fit page, fit width, multiple pages, custom percent; Ctrl+wheel / pinch.
- [x] Print preview render z aktualniho canvas modelu; print dialog.
- [x] Print to PDF pres provider.
- [x] Reading mode: stranky/sloupce optimalizovane na cteni, skryty toolbar, navigace.
- [x] RED/GREEN unit: zoom transform + pixel snapping, view mode geometry.
- [x] RED/GREEN E2E + screenshot: print layout vs reading mode vs fit-width, print preview neni blank. UX review.

### Faze E12: Hyphenation, page background a pokrocile tabulky

- [x] Hyphenation: auto/manual, zone, consecutive limit; optional hyphen, non-breaking hyphen; integrace s line-breaker.
- [x] Page background: page color, watermark (text/image, diagonal), page borders (line/dash, margin/page).
- [x] Pokrocile tabulky: table styly (banded rows/cols, header/total), repeat header rows na dalsich strankach, split table across pages, nested tables.
- [x] Table extras: convert text<->table, table sort, jednoducha table formula (SUM/AVERAGE), cell margins/spacing, cell borders editor.
- [x] RED/GREEN unit: hyphenation break points, table style banding, header-row repeat na page break.
- [x] RED/GREEN E2E + screenshot: tabulka pres dve stranky s opakovanou hlavickou + table style + watermark, save/reload. Undo gate.

## Faze 24: Parity regression suite

- [x] Vytvorit `DocumentEditorCanvasLegacyParityE2ETests`.
- [x] Vytvorit seed document covering all legacy feature groups.
- [x] Kazdy toolbar command ma minimalne jeden E2E nebo explicitni "shell-only" test.
- [x] Kazdy provider boundary ma save/export/reload test.
- [x] Kazda major interaction ma screenshot test.
- [x] Prepsat stare WYSIWYG/core testy na canvas selectors nebo je oznacit jako legacy/core-only diagnostiku.
- [x] Zachovat historicke bug regression tests, ale menit expected behavior na canvas engine.
- [x] Kazda faze E1-E12 ma minimalne jeden parity/E2E radek (numbering, tabs, sekce/sloupce, styly, fields/cross-ref, advanced char, shapes/text box/chart, math, content controls/forms, autocorrect/format painter, view modes/print, hyphenation/page background/advanced tables).
- [x] Akceptace (legacy parity): legacy-parity suite (faze 0-23) zelena bez legacy fallbacku -> umoznuje "legacy-parity preview" cutover.
- [x] Akceptace (full quality): legacy-parity + E1-E12 suite zelena -> umoznuje prohlasit "kvalita Google Docs / Word Online / OnlyOffice".

## Faze 25: Cutover plan

- [x] Canvas engine zustava opt-in minimalne po jednu celou parity iteraci.
- [x] `/document-editor` se prepne na canvas jen po explicitnim schvaleni.
- [x] `TmDocumentEditor` default se prepne na `CanvasEnginePreview` az po zelene parity suite, screenshot gallery a manual gates.
- [x] Zachovat rychly rollback parametrem `RenderEngine`.
- [x] Zapsat migracni poznamky do README/docs.
- [ ] Spustit full `dotnet test`.
- [ ] Spustit full relevantni E2E s Demo API + WASM.
- [ ] Manual smoke: typing, toolbar formatting, table, image, comments, revisions, import/export, collaboration.

## Faze 26: Soak and legacy/core removal decision

- [ ] Minimalne nekolik dni pouzivat canvas engine v demo pri realne praci.
- [ ] Sbiranou zpetnou vazbu zapisovat jako nove P0/P1 issues do tohoto planu.
- [ ] Legacy odstranit pouze po explicitnim schvaleni.
- [ ] Soucasny core engine odstranit nebo ponechat jako diagnosticky harness pouze po explicitnim rozhodnuti.
- [ ] Pred mazanim znovu overit provider compatibility a NuGet public API impact.

## Top human scenarios pro acceptance gallery

Prvnich 22 scenaru = legacy-parity gallery (faze 0-23). Zbyvajici = rozsirena kvalita (faze E1-E12).

- [ ] Napsat odstavec, Enter, dalsi odstavec, undo/redo.
- [ ] Vybrat text mysi a dat bold/font size/color z ribbonu.
- [ ] Collapsed caret bold -> napsat dalsi slovo -> bold jen nove slovo.
- [ ] Copy/cut/paste rich text.
- [ ] Paste plain text.
- [ ] Vlozit tabulku, psat do bunek, Tab navigace, resize sloupce.
- [ ] Vlozit obrazek z URL, resize, square wrap.
- [ ] Presunout obrazek a overit obtikani.
- [ ] Pridat komentar, reply, resolve, reopen.
- [ ] Zapnout track changes, psat, mazat, accept/reject.
- [ ] Najit a nahradit text.
- [ ] Vytvorit H1/H2, vlozit obsah, kliknout do obsahu, prejmenovat nadpis a aktualizovat obsah.
- [ ] Napsat slovo s preklepem, otevrit spellcheck context menu, vybrat navrh a undo/redo opravu.
- [ ] Editovat header a vlozit page number.
- [ ] Vlozit footnote/endnote.
- [ ] Ulozit, reloadnout, overit stejny vizual.
- [ ] Export DOCX, zpet import, overit struktury.
- [ ] Export PDF provider smoke.
- [ ] Compare documents smoke.
- [ ] Offline draft recovery smoke.
- [ ] Dva browsery collaboration smoke.
- [ ] Mobile viewport: psani, selection, toolbar overflow.
- [ ] Vytvorit multilevel cislovany seznam, zmenit uroven, restart numbering.
- [ ] Nastavit decimal tab a zarovnat sloupec cisel pres pravitko.
- [ ] Rozdelit dokument na dva sloupce a vlozit section break na landscape sekci.
- [ ] Modify "Heading 1" styl a overit, ze se zmeni vsechny nadpisy.
- [ ] Vlozit obrazek s caption, cross-reference na nej, prejmenovat a update fields.
- [ ] Napsat H2O se subscriptem a x^2 se superscriptem.
- [ ] Vlozit text box a sipku, napsat do text boxu, presunout.
- [ ] Vlozit rovnici (zlomek + integral) a upravit ji.
- [ ] Vlozit formular (text + checkbox + drop-down), prepnout do forms mode a vyplnit.
- [ ] Format painter: prenest formatovani z jednoho slova na jine.
- [ ] Napsat `--` a `1.` a overit autocorrect/autoformat, pak undo.
- [ ] Prepnout do reading mode a print preview, overit fit-width zoom.
- [ ] Tabulka pres dve stranky s opakovanou hlavickou a table style, watermark na strance.

## Prubezne poznamky

- Tento plan zamerne nepouziva legacy DOM jako cil. Legacy muze slouzit jen jako reference feature parity.
- Soucasny core engine muze slouzit jako zdroj testu, converteru a provider wiring, ale ne jako UX cil.
- Kazda faze ma byt mala a mergeovatelna. Pokud faze zacne byt prilis velka, rozdelit ji na `a/b/c` podfaze a pridat screenshot gate pro kazdou viditelnou cast.
