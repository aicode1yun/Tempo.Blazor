# TmDocumentEditor - TODO opravy prekryvu textu pri obtekani obrazku

Datum zalozeni: 2026-05-22  
Navazuje na:

- `planning/tmdocumenteditor-image-wrapping-word-google-docs-analysis-2026-05-21.md`
- `planning/tmdocumenteditor-image-wrapping-word-google-docs-implementation-todo-2026-05-21.md`

Rezim prace: TDD + prubezne e2e testy + rucni UX kontrola v demu  
Stav: hotovo pro opravu engine/DOM prekryvu, default demo, viewport matrix, legacy-toolbar cleanup a diagnostiku zoomovaneho DOMu; zustavaji uz jen sirsi dlouhodobe UX validace z navazujici Word/Google Docs faze mimo tento bugfix

## Dulezite rozhodnuti

Obtékani textu kolem obrazku z defaultni demo stranky neodstranujeme jen proto, ze odhaluje chybu. Chceme Word-like editor, takze defaultni demo smi a ma obsahovat realisticky obtékany obrazek.

Aktualni chyba ze screenshotu je primarne chyba layout enginu/renderovaci vrstvy:

- text se nesmi prekryvat s jinym textem,
- text se nesmi prekryvat s obrazkem v rezimech `Square`, `Tight`, `Through` a `TopBottom`,
- caption se musi pocitat do footprintu obrazku,
- dalsi bloky nesmi zacit uvnitr aktivni exclusion zony predchoziho plovouciho objektu,
- inline image blok nesmi ignorovat aktivni exclusion zony nad sebou,
- demo data mohou byt narocny scenar, ale nesmi rozbit zakladni layout invarianty.

## Co se nesmi udelat jako "oprava"

- [x] Neodstranit obtekany obrazek z contract demo jen kvuli tomu, aby chyba nebyla videt.
- [x] Neudelat CSS-only hack, ktery pouze posune konkretni demo bloky.
- [x] Neudelat e2e test, ktery kontroluje jen pritomnost elementu, ale ne realne prekryvy rectu.
- [x] Neobnovit sidecar workaround (`tm-wysiwyg-image-sidecar-text`) jako cilove reseni.
- [x] Nepouzivat v e2e JS mutaci dokumentu misto uzivatelske akce, pokud testuje UX.
- [x] Neignorovat captiony pri vypoctu layoutu.
- [x] Neprohlasit demo za opravene bez screenshotu nebo geometricke kontroly po reloadu.

## Cilove invarianty

- [x] `DocumentLayoutEngine` musi umet vratit layout, kde kazdy viditelny textovy line/segment rect neprotina zakazane objekty.
- [x] Pro wrap mody `Square`, `Tight`, `Through`, `TopBottom` musi text respektovat `WrapRect`/`FootprintRect`.
- [x] Pro wrap mode `BehindText` smi text prekryvat obrazek a klik ma preferovat text.
- [x] Pro wrap mode `InFrontOfText` smi obrazek prekryvat text a klik ma preferovat obrazek.
- [x] Caption patri do layout footprintu obrazku, pokud je zobrazeny.
- [x] Inline image block musi respektovat aktivni exclusions, pokud by se jinak kreslil pres plovouci objekt.
- [x] Dalsi paragraph/table/image block musi zacit na Y pozici, kde ma realne dostupny prostor.
- [x] Pokud se na radku nevejde zadny smysluplny interval, engine musi posunout Y pod aktualni blokujici exclusion.
- [x] Bezny text se pri obtekani obrazku ma lamat na hranicich slov, pokud se cele slovo vejde do nektereho dostupneho intervalu.
- [x] Browser DOM render nesmi pridavat vlastni float/shape layout, ktery je v rozporu s engine layoutem.
- [x] Po save/reload musi byt geometrie default demo dokumentu stabilni.

## Definition of Done

- [x] Default demo po `POST /api/document-editor/reset` a reloadu `/document-editor` nema viditelne prekryvy textu, obrazku ani captionu.
- [x] Cileny e2e test zachyti chybu ze screenshotu pred opravou a projde po oprave.
- [x] Unit testy layout enginu pokryvaji presny scenar: plovouci left square obrazek, text vedle nej, nasledujici inline obrazek, caption a provider image.
- [x] E2E testy meri skutecne DOM recty pomoci `getClientRects()`/`getBoundingClientRect()`.
- [x] Testy kontroluji i screenshotovy stav pri `1440x900` se zapnutym pravym panelem.
- [x] Testy kontroluji, ze default demo nelame bezna slova mezi layout lines uprostred slova.
- [x] Testy bezi po restartu demo API a WASM dema, ne proti cache stareho JS.
- [x] Demo zustava UX hezke: jeden jasny wrap scenar, zadne nahodne prekryte favicon obrazky.
- [x] Bezi `dotnet build` pro dotcene projekty.
- [x] Bezi cileny unit + e2e test set.
- [x] Demo servery jsou po overeni vypnute.

## Faze 0: Zmrazit aktualni bug jako RED testy

Stav: hotovo

### 0.1 Default demo e2e repro

- [x] Pridat e2e test `DocumentEditor_DefaultDemo_ImageWrapDoesNotOverlapTextOrImagesOnInitialLoad`.
- [x] Test musi zavolat reset demo API.
- [x] Test musi otevrit `https://localhost:7106/document-editor`.
- [x] Test musi pockat na nacteni WYSIWYG hostu.
- [x] Test musi prepnout na contract demo, pokud neni aktivni.
- [x] Test musi zajistit viewport `1440x900`.
- [x] Test musi nechat otevreny pravy panel `Verze` nebo `Vlastnosti`, protoze screenshot chyby ma pravy panel otevreny.
- [x] Test musi najit prvni viditelnou page body.
- [x] Test musi nasbirat recty vsech viditelnych textovych nodu v page body.
- [x] Test musi nasbirat recty vsech viditelnych `figure.tm-wysiwyg-image`, `img` a `figcaption`.
- [x] Test musi z rectu odfiltrovat prazdne recty, virtual pages a hidden prvky.
- [x] Test musi zmensit recty o 1 px toleranci, aby nepadal na antialiasing.
- [x] Test musi failnout, pokud textovy rect protina image media rect u wrap modu, kde overlap neni povoleny.
- [x] Test musi failnout, pokud textovy rect protina caption rect.
- [x] Test musi failnout, pokud rect textu z jednoho bloku protina rect textu jineho bloku.
- [x] Test musi failnout, pokud inline image block zacne uvnitr vertical rozsahu aktivniho square/tight wrap objektu bez dostatecneho horizontalniho prostoru.
- [x] Pri selhani ulozit screenshot, DOM snapshot, layout snapshot a seznam kolidujicich rectu.

### 0.2 Layout engine unit repro

- [x] Pridat unit test `Layout_DefaultContractLikeWrappedImage_DoesNotOverlapFollowingInlineImages`.
- [x] V testu poskladat minimalni dokument podobny default contract demu:
  - [x] nadpis,
  - [x] uvodni kratky odstavec,
  - [x] pending revision text,
  - [x] left square wrapped image s captionem,
  - [x] text vedle obrazku,
  - [x] nasledujici inline obrazek s captionem,
  - [x] nasledujici provider image.
- [x] Otestovat, ze `page.Objects` obsahuje object box pro plovouci obrazek.
- [x] Otestovat, ze `page.Exclusions` obsahuje zonu pro cely footprint plovouciho obrazku.
- [x] Otestovat, ze radky textoveho odstavce neprotínaji image footprint.
- [x] Otestovat, ze rect nasledujiciho inline image bloku neprotina image footprint.
- [x] Otestovat, ze caption nasledujiciho obrazku neprotina predchozi image footprint.
- [x] Otestovat, ze paragraph recty jsou monotonne v Y, pokud se nejedna o povoleny floating overlay.

### 0.3 DOM vs layout snapshot repro

- [x] Pridat e2e helper `CaptureDocumentOverlapProbeAsync`.
- [x] Probe musi vratit:
  - [x] `TextRects`,
  - [x] `ImageRects`,
  - [x] `CaptionRects`,
  - [x] `FigureRects`,
  - [x] `LayoutObjects`,
  - [x] `ExclusionRects`,
  - [x] `Collisions`.
- [x] Probe musi u kazde kolize vratit:
  - [x] typ kolize,
  - [x] block id A,
  - [x] block id B,
  - [x] text snippet A/B,
  - [x] rect A/B,
  - [x] CSS path A/B.
- [x] Probe musi umet vypsat rozdil mezi layout snapshotem a DOM rectem.
- [x] Probe musi ignorovat podtrzeni spellchecku a dekoracni revizni pozadi, ale ne text samotny.
- [x] Probe musi rozlisovat povoleny `BehindText`/`InFrontOfText` overlap.

## Faze 1: Diagnostika v layout enginu a rendereru

Stav: hotovo

### 1.1 Rozsirit layout snapshot o debug metadata

- [x] Pridat debug-only metadata pro kazdy block layout:
  - `BlockId`,
  - `BlockType`,
  - `Order`,
  - `StartY`,
  - `EndY`,
  - `CurrentYBefore`,
  - `CurrentYAfter`,
  - `PageIndex`.
- [x] Pro kazdy image object ulozit:
  - media rect,
  - caption rect,
  - wrap rect,
  - footprint rect,
  - anchor block id,
  - wrap mode,
  - allow overlap,
  - z-index.
- [x] Pro kazdou text line ulozit:
  - line rect,
  - available intervals,
  - exclusions pouzite pro vypocet,
  - seznam segmentu.
- [x] Pridat diagnostiku, kdyz blok zacina uvnitr aktivni exclusion zony.
- [x] Pridat diagnostiku, kdyz inline image ignoruje aktivni exclusion.
- [x] Pridat diagnostiku, kdyz paragraph line rect protina image footprint.

### 1.2 E2E debug output

- [x] Rozsirit `SaveDocumentEditorDebugArtifactsAsync` o overlap probe JSON.
- [x] Do artifactu pridat `window.tmDocumentEditorWysiwyg.getDebugSnapshot`.
- [x] Do artifactu pridat screenshot celeho viewportu.
- [x] Do artifactu pridat screenshot jen page canvasu.
- [x] Do artifactu pridat seznam aktivnich floating UI prvku.
- [x] Do artifactu pridat informaci o zoomu a side panel stavu.

## Faze 2: Zavest jednotny footprint obrazku

Stav: hotovo pro footprint a bugfix edge-cases

### 2.1 Model layout rectu obrazku

- [x] Najit `DocumentObjectLayoutBox` a souvisejici modely layout snapshotu.
- [x] Navrhnout reprezentaci:
  - [x] `MediaRect`,
  - [x] `CaptionRect`,
  - [x] `ObjectRect`,
  - [x] `WrapRect`,
  - [x] `FootprintRect`.
- [x] Rozhodnout, jestli `ObjectRect` zustane media-only nebo bude znamenat footprint.
- [x] Preferovane: `ObjectRect` ponechat jako media rect kvuli kompatibilite s existujicimi testy a pridat `FootprintRect`.
- [x] Pridat helper `GetObjectFootprintRect`.
- [x] Pridat helper `GetCaptionLayoutRect`.
- [x] Caption rect pocitat jen kdyz caption existuje a neni vypnuta.
- [x] Caption height merit pres stejny text measurer/line height jako dokument.
- [x] Do footprintu zapocitat caption pod obrazkem.
- [x] Do footprintu zapocitat wrap distances.

### 2.2 Unit testy footprintu

- [x] Test: obrazek bez captionu ma footprint shodny s media rect + wrap distances.
- [x] Test: obrazek s jednoradkovym captionem ma footprint vyssi nez media rect.
- [x] Test: dlouhy caption zalomi text a zvetsi footprint.
- [x] Test: `TopBottom` pouziva footprint, ne jen media rect.
- [x] Test: `Square`/`Tight` pouziva footprint pro vylouceni textu.
- [x] Test: `BehindText` nevytvari blokujici footprint pro text.
- [x] Test: `InFrontOfText` nevytvari blokujici footprint pro text, ale zustava v hit-test vrstve nad textem.

## Faze 3: Opravit active exclusions a posun `CurrentY`

Stav: hotovo

### 3.1 Textove odstavce

- [x] Proverit `ParagraphLayoutContext.EnsureWritableLine`.
- [x] Overit, ze `GetAvailableLineIntervals` pouziva vsechny aktivni exclusions pro dany `CurrentY`.
- [x] Pridat unit test, kdy line interval zmizi kvuli obrazku pres celou sirku a engine posune Y pod obrazek.
- [x] Pridat unit test, kdy left square obrazek neblokuje cely radek a text zacne vpravo.
- [x] Pridat unit test, kdy po skonceni obrazku dalsi textovy radek zacne opet na plne sirce.
- [x] Opravit `AdvanceToParagraphBottom`, aby paragraph bottom zahrnoval posledni realny line bottom.
- [x] Opravit pripad, kdy paragraph skonci drive nez aktivni anchor object a nasledujici blok by zacal v kolizi.
- [x] Opravit duplicitni layout pre-anchored obrazku, pokud je image block pred explicitnim anchor odstavcem.
- [x] Zajistit, ze nasledujici textovy blok muze dal obtekat aktivni obrazek, pokud ma dostupny interval.
- [x] Zajistit, ze nasledujici blok se posune pod obrazek, pokud dostupny interval neni vhodny pro typ bloku.

### 3.2 Inline image bloky

- [x] Proverit `LayoutInlineImageBlock`.
- [x] Pred polozenim inline image bloku zjistit, jestli jeho navrzeny rect protina aktivni non-overlap exclusion.
- [x] Pokud protina a existuje dost siroky volny interval, umistit inline image do tohoto intervalu.
- [x] Pokud neexistuje dost siroky interval, posunout `CurrentY` pod nejblizsi blokujici exclusion.
- [x] Po posunu znovu zavolat `EnsureLineFits`.
- [x] Otestovat alignment `Start`, `Center`, `End` uvnitr dostupneho intervalu.
- [x] Otestovat, ze inline image s captionem neprekryje square wrapped obrazek.
- [x] Otestovat, ze provider image po obtekanem odstavci zacne na ciste Y pozici.

### 3.3 TopBottom a blokove objekty

- [x] Proverit, jestli `TopBottom` image block posouva tok dokumentu.
- [x] Pokud `TopBottom` reprezentuje objekt v textovem toku, musi posunout `CurrentY` pod footprint.
- [x] Pokud `TopBottom` reprezentuje floating object, musi alespon vytvorit full-width exclusion pres footprint.
- [x] Rozhodnout a zdokumentovat semantiku:
  - [x] `Inline`: normalni objekt v toku.
  - [x] `TopBottom`: blokuje text nad/pod, ale je ukotveny objekt.
  - [x] `Square/Tight`: text muze byt vlevo/vpravo, ale footprint blokuje kolize.
- [x] Pridat unit testy pro kazdou semantiku.

## Faze 4: Sjednotit DOM renderer s layout enginem

Stav: hotovo pro WYSIWYG; readonly float fallback zustava oddelene mimo engine-driven render

### 4.1 Najit zdroj rozdilu layout vs DOM

- [x] Proverit `TmDocumentBlockRenderer`.
- [x] Proverit CSS tridy:
  - `.tm-wysiwyg-image--wrap-square`,
  - `.tm-wysiwyg-image--wrap-square-left`,
  - `.tm-wysiwyg-image--wrap-square-right`,
  - `.tm-document-image--wrap-square`,
  - `shape-outside`,
  - `float`.
- [x] Zjistit, jestli browser float layout bezi soucasne s engine-driven absolutnim layoutem.
- [x] Pokud ano, odstranit konflikt: ve WYSIWYG pouzivat engine-driven pozice, ne browser float jako druhy layout system.
- [x] Zachovat CSS float pouze tam, kde jde o readonly fallback mimo WYSIWYG, pokud je potreba.
- [x] Otestovat, ze DOM recty odpovidaji layout snapshotu s toleranci.
- [x] Opravit JS mereni fontu: `pt` z dokumentu se nesmi pouzit jako `px`.
- [x] Mereni JS layoutu respektuje inline `FontFamily`, `FontSize`, `Bold`, `Italic`, `Superscript` a `Subscript`.
- [x] Mereni JS layoutu respektuje `LineSpacing`, `SpacingBefore` a `SpacingAfter`.
- [x] DOM layout segmenty renderuji stejny font, velikost, weight, style, letter-spacing a line-height jako layout mereni.
- [x] Vypnout browserove zalamovani uvnitr absolutne pozicovanych layout segmentu, aby DOM nevytvarelo vlastni radky mimo engine.
- [x] Sjednotit C# layout engine a JS layout renderer na slovnim wrapovani beznych textovych tokenu.
- [x] Dlouha slova bez mezery se porad mohou lamat po znacich, aby editor nepretekl z radku.

### 4.2 Caption rendering

- [x] Zjistit, kde se renderuje `figcaption`.
- [x] Napojit caption na layout rect/footprint.
- [x] Zajistit, ze caption nepouziva absolutni nebo negativni pozici mimo footprint.
- [x] Zajistit, ze caption pri resize obrazku zmeni sirku podle media rectu.
- [x] Zajistit, ze caption pri dlouhem textu zalomi a zvysi footprint.
- [x] Otestovat caption po editaci v inspectoru s debounce.
- [x] Otestovat save/reload caption footprint.

### 4.3 Revision styling a layout

- [x] Overit, ze revizni underline/background nemení line height neocekavane.
- [x] Overit, ze accepted/rejected revision nezanecha stale DOM styly.
- [x] E2E overlap probe musi ignorovat dekoracni underline, ale ne text.
- [x] Pridat test s pending revision vedle obtekaneho obrazku.

## Faze 5: Opravit default demo jako realisticky UX scenar

Stav: hotovo pro contract demo seed a shared fallback

Tato faze se dela az po zelene fazi 0-4, ne jako obchazeni chyby.

### 5.1 Contract demo obsah

- [x] Nechat v contract demu jeden jasny square-wrapped obrazek.
- [x] Pouzit vizualne rozumny obrazek, ne dvakrat obri `/favicon.png` nad sebou.
- [x] Udelat text vedle obrazku dost dlouhy na 3-5 radku.
- [x] Udelat caption kratky a citelny.
- [x] Provider image dat az po obtekanem scenari jako samostatny obsah.
- [x] Accessibility warning sample presunout niz nebo do jineho demo scenare, pokud zahlcuje prvni viewport.
- [x] Po reloadu musi prvni viewport pusobit jako dokument, ne jako stress test.

### 5.2 Demo data synchronizace

- [x] Upravit `DemoDocumentEditorStore.cs`.
- [x] Upravit `DemoDocumentEditorProvider.cs`.
- [x] Overit, ze server API demo a shared UI fallback seed maji stejne layout vlastnosti.
- [x] Overit `POST /api/document-editor/reset`.
- [x] Overit, ze e2e izolovane dokumenty nejsou zavisle na konkretni seed estetice.

## Faze 6: E2E UX regrese pro obrazky

Stav: hotovo pro bugfix matrix a hlavni akce obtekani

### 6.1 Default demo matrix

- [x] Pridat test pro viewport `1440x900`.
- [x] Pridat test pro viewport `1920x1080`.
- [x] Pridat test pro viewport `1280x720`.
- [x] Pridat test pro viewport `820x900`.
- [x] Pridat test pro viewport `390x840`.
- [x] V kazdem viewportu overit:
  - [x] bez text/image/caption kolizi,
  - [x] bez rezani beznych slov uprostred slova v default demo scenari,
  - [x] bez horizontalniho overflow mimo page,
  - [x] bez prekryti layout bubble se side panelem,
  - [x] bez duplicitniho image toolbaru.
- [x] Otestovat zoom `Sirka stranky`.
- [x] Otestovat zoom `100 %`, pokud existuje.

### 6.2 User actions

- [x] Kliknout do textu vedle obtekaneho obrazku.
- [x] Otestovat psani v prvnim, druhem a tretim vizualnim radku.
- [x] Otestovat Backspace na zacatku vizualniho radku.
- [x] Otestovat Delete na konci vizualniho radku.
- [x] Otestovat Enter uprostred obtekaneho textu.
- [x] Otestovat drag obrazku a reflow.
- [x] Otestovat resize obrazku a reflow.
- [x] Otestovat zmenu wrap mode `Inline -> Square -> TopBottom -> Square`.
- [x] Otestovat save/reload po kazde perzistentni zmene.

### 6.3 Floating UI

- [x] Pri vyberu obrazku se zobrazi jen jedna primarni layout bubble.
- [x] Stary `document-wysiwyg-image-selection-toolbar` ma count `0`.
- [x] Layout bubble neprekryva side panel.
- [x] `More` otevira image context menu.
- [x] Context menu zustane ve viewportu.
- [x] Klik do textu zavre image UI.
- [x] Escape zavre image UI a necha editor v konzistentnim selection stavu.

## Faze 7: Unit testy pro layout helpery a edge cases

Stav: hotovo

- [x] Test: dva left square objekty pod sebou se neprekryji a text ma spravne intervals.
- [x] Test: left + right square objekt ve stejnem vertical rozsahu nechaji prostredni interval, pokud je dost siroky.
- [x] Test: left + right square objekt bez dostatecneho prostredniho intervalu posunou line pod nizsi objekt.
- [x] Test: inline image nasledujici po left square obrazku se umi umistit do praveho intervalu, pokud se vejde.
- [x] Test: inline image nasledujici po left square obrazku se posune pod obrazek, pokud se nevejde.
- [x] Test: pre-anchored floating image pred explicitnim anchor odstavcem se zalozi jen jednou a nevytvori ghost exclusion.
- [x] Test: left square obrazek nelame normalni text uprostred slov, pokud se slovo vejde do dostupneho intervalu.
- [x] Test: caption zvysuje footprint a posouva nasledujici blok.
- [x] Test: dlouhy caption pres dva radky zvysuje footprint o dva caption radky.
- [x] Test: rotace obrazku ma konzervativni bounding box.
- [x] Test: crop nemeni footprint neocekavane.
- [x] Test: locked aspect ratio po resize zachova footprint a exclusions.
- [x] Test: fixed-on-page object neovlivni move-with-text semantiku jinych objektu.

## Faze 8: Validace proti Word/Google Docs UX

Stav: castecne hotovo v bugfixu; zbytek patri do navazujici Word/Google Docs UX todo

- [x] Sepsat kratkou UX specifikaci pro default chovani `Square`.
- [x] Overit, ze klik vpravo od leveho square obrazku jde do textu, pokud je tam textovy interval.
- [x] Overit, ze klik do prazdne oblasti vedle obrazku nevytvari nahodny sidecar odstavec.
- [x] Overit, ze drag ukazuje citelne handles.
- [x] Overit, ze resize ma vice uchopu a ne jen jeden bod, pokud uz jsou ve fazi hotove.
- [x] Overit, ze layout bubble ma aktivni stav aktualniho wrap modu.
- [x] Overit, ze inspector ukazuje stejne hodnoty jako layout bubble.
- [x] Overit, ze demo vypada jako dokument, ne jako debug playground.

Kratka UX specifikace `Square`: obrazek je normalni objekt ukotveny k odstavci, text kolem nej zustava jeden editovatelny odstavec bez sidecar bloku, klik do textoveho intervalu vzdy preferuje text/caret a klik na media/caption/chrome preferuje obrazek. Layout bubble ukazuje aktualni wrap mode, inspector musi mit stejne hodnoty, drag/resize okamzite prepocte layout lines a zadny dalsi paragraph/table/image blok nesmi zacit uvnitr aktivni wrap exclusion.

## Faze 9: Cleanup a ochrana pred navratem chyby

Stav: hotovo pro bugfix scope

- [x] Odstranit nebo zneaktivnit stare sidecar helpery, pokud uz nejsou potreba.
- [x] Odstranit e2e helpery, ktere toleruji sidecar workaround.
- [x] Odstranit CSS float pravidla, ktera jsou v konfliktu s engine-driven layoutem.
- [x] Zachovat jen nutne CSS pro readonly fallback, pokud je pouzivan.
- [x] Pridat komentar do layout enginu k invariantum obtekani.
- [x] Pridat regresni test do stabilniho smoke setu.
- [x] Zkontrolovat, ze full e2e neobsahuje testy vyzadujici stary image toolbar.
- [x] Aktualizovat tento TODO a oznacit realne hotove body.

## Doporučene prikazy pro overeni

```bash
dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net8.0 --no-restore
dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0 --no-restore
dotnet build src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj --no-restore
dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore
dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentLayoutEngineTests"
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor_DefaultDemo_ImageWrapDoesNotOverlapTextOrImagesOnInitialLoad"
```

Pro rucni overeni:

```bash
dotnet run --project src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj
dotnet run --project src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --launch-profile https
```

Pak otevrit:

```text
https://localhost:7106/document-editor
```

Po overeni demo servery vypnout.

## Poznamky k aktualni hypoteze

Pravdepodobny problem neni jedna jedina vec:

- `LayoutTextBlock` textove radky umi pouzivat `page.Exclusions`, ale navazujici bloky nemusi vzdy respektovat aktivni exclusion zony.
- `LayoutInlineImageBlock` vypada, ze poklada inline image na `CurrentY` a sirku body bez dotazu na `page.Exclusions`.
- Caption pravdepodobne neni zapocitana do footprintu, podle ktereho se vytvari exclusion.
- Pokud plovouci obrazek presahuje pod konec kratkeho odstavce, dalsi image block muze zacit uvnitr jeho vertikalni oblasti.
- DOM/CSS muze do toho pridavat druhy layout system pres float/shape-outside.

Prvni implementacni krok proto musi byt RED test, ktery presne zmeri kolize. Az potom ma smysl opravovat engine, jinak budeme znovu "opravovat" jen to, co je prave videt na screenshotu.
