# TmDocumentEditor - analýza obrázků, obtékání textu a Word/Google Docs UX

Datum: 2026-05-21  
Video: `/home/pavel/Videa/Záznamy obrazovky/Záznam obrazovky z 2026-05-21 05-01-52.mp4`  
Stav: analytický návrh bez úprav produkčního kódu

## Cíl

Cílem je navrhnout takové řešení obrázků a obtékání textu, aby se `TmDocumentEditor` nechoval jako webová aproximace s jedním pomocným trikem, ale jako skutečný dokumentový editor ve stylu Wordu a Google Docs.

Konkrétně musí být vyřešené:

- kliknutí do libovolného řádku textu vedle obrázku,
- přirozený Backspace/Delete kolem obrázků a textu obtékajícího obrázek,
- živé přepočítání textu při posunu nebo změně velikosti obrázku,
- úplná sada layout režimů: inline, square, tight, through, top/bottom, behind text, in front of text,
- přesná pozice vůči stránce, okrajům, odstavci/sloupci,
- krásné a srozumitelné UI se stejnou úrovní polish jako Word/Google Docs,
- přísné E2E testy, které kontrolují geometrii, caret, obsah, UI stav, model a save/reload.

## Použité podklady

### Lokální video

Ve videu je vidět několik symptomů jednoho hlubšího problému:

- Text vedle levého obrázku vypadá vizuálně jako obtékající text, ale nechová se jako normální textový tok.
- Kliknutí na druhý řádek textu vedle obrázku neumí spolehlivě přesunout caret do konkrétního řádku/znaku.
- Backspace na začátku textu vedle obrázku nic neudělá nebo se nechová jako běžný editorový Backspace.
- Text nereaguje přirozeně na hýbání obrázkem po stránce. Při přesunu objektu má dokument okamžitě přepočítat řádky, ne čekat na ruční workaround.
- UI výběru obrázku působí jako technický prototyp: jeden resize bod, slabá manipulace s objektem, málo vizuálních vodítek.

### Oficiální chování Wordu a Google Docs

Microsoft Word uvádí pro obrázky layout režimy `In Line with Text`, `Square`, `Tight`, `Through`, `Top and Bottom`, `Behind Text` a `In Front of Text`. Word také rozlišuje inline obrázky a ostatní obrázky ukotvené k odstavci; u ne-inline obrázků pracuje s objektovou kotvou a volbami jako `Move with text`, `Fix position on page`, přesná pozice a `Lock anchor`.

Google Docs nabízí pro obrázky režimy `In line`, `Wrap text`, `Break text`, `Behind text` a `In front of text`; pro `Wrap text`/`Break text` navíc podporuje `Move with text` a `Fix position on page`. Pravý panel má sekce pro `Size & Rotation`, `Text Wrapping`, `Position`, úpravy obrázku a přístupnost.

Zdroje:

- Microsoft Support: https://support.microsoft.com/en-au/office/wrap-text-around-a-picture-in-word-bdbbe1fe-c089-4b5c-b85c-43997da64a12
- Microsoft Support: https://support.microsoft.com/en-gb/office/wrap-text-and-move-pictures-in-word-becff26a-d1b9-4b9d-80f8-7e214557ca9f
- Google Docs Editors Help: https://support.google.com/docs/answer/97447?co=GENIE.Platform%3DDesktop&hl=en-en
- Google Docs accessibility/alt text: https://support.google.com/docs/answer/6199477?hl=en

### Lokální implementace

Relevantní části současného řešení:

- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlocks.cs`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentAnchors.cs`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentImageInspector.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentImageWrapPanel.razor`
- `tests/Tempo.Blazor.E2E/DocumentEditorE2ETests.cs`

## Krátký verdikt

Současné řešení nejde donekonečna opravovat drobnými patchemi. Základní problém není jen bug v hit-testu nebo CSS. Pro Word/Google Docs kvalitu potřebujeme přejít od `CSS float + sidecar paragraph workaround` k vlastnímu dokumentovému layout modelu pro ukotvené objekty, line boxy, wrap zóny, caret mapu a objektové overlay UI.

Jinými slovy: obrázek nesmí vytvářet vedle sebe „speciální pomocný odstavec“, který se tváří jako obtékající text. Obrázek musí být objekt v layout enginu a běžný text musí skutečně reflowovat okolo jeho exclusion zóny.

## Proč současné E2E testy mohly projít

Současné testy kolem obrázků v mnoha případech ověřují, že:

- vznikne `p.tm-wysiwyg-image-sidecar-text[data-wrap-sidecar-for=...]`,
- caret je uvnitř tohoto sidecar odstavce,
- napsaný text je geometricky vpravo/vlevo od obrázku,
- text se nepropsal do nadpisu nebo jiného bloku,
- obrázek má očekávané CSS třídy.

To ale netestuje to nejdůležitější: zda text vedle obrázku je skutečně stejný textový tok, který má správné řádky, správnou caret geometrii, přirozený Backspace/Delete a živé přepočítání při posunu objektu.

Testy tedy umí potvrdit funkčnost workaroundu, ale neumí potvrdit Word/Docs kvalitu. V tom je rozdíl mezi „test zelený“ a „editor použitelný“.

## Současný stav implementace

### Datový model

`ImageBlockContent` už má dobrý základ:

- `Source`: URL, asset, clipboard,
- `Url`, `AssetId`,
- `AltText`, `Caption`,
- `Size`, `NaturalSize`,
- `Alignment`,
- `FloatingLayout`,
- `LinkUrl`.

`DocumentFloatingLayout` už obsahuje:

- `Inline`,
- `HorizontalRelativeTo`,
- `VerticalRelativeTo`,
- `X`, `Y`,
- `WrapMode`,
- `ZIndex`,
- `LockAnchor`,
- `HorizontalPosition`,
- `DistanceLeft`, `DistanceRight`, `DistanceTop`, `DistanceBottom`,
- `PreservedWrapMode`.

To je užitečné a nemělo by se zahodit. Problém je, že tento model není zatím napojený na skutečný page layout engine.

### Renderování a obtékání

Současný CSS používá pro square wrapping hlavně:

- `float: left/right`,
- `shape-outside: inset(...)`,
- marginy okolo obrázku,
- u `top-bottom` clear/block layout,
- u `behind/in-front` absolutní pozici.

To je dobré pro jednoduchý HTML dokument, ale slabé pro editor typu Word:

- browser float není dokumentový layout engine,
- hit-test a caret mapování se pak musí dohánět ručně,
- line boxes nejsou explicitně známé runtime modelu,
- kliknutí na druhý řádek obtékajícího textu nelze spolehlivě namapovat na dokumentový offset,
- přesun obrázku není jedna transakce `object moved -> reflow all affected lines`,
- u vícestránkového layoutu je browser float příliš nepředvídatelný.

### Sidecar text

JS má explicitní funkce pro sidecar:

- `_createWrappedImageSideTextBlockModel`,
- `_isWrappedImageSideTextBlock`,
- `_ensureWrappedImageSideTextBlock`,
- `_findWrappedImageSideTextBlockAtPoint`,
- `_focusWrappedImageSideTextBlock`.

To je hlavní architektonický kompromis. Sidecar je samostatný odstavec vložený vedle/za obrázek, ne skutečná část plynoucího textu okolo objektu. Kvůli tomu:

- Backspace na začátku sidecar textu není obyčejný Backspace v jednom odstavci,
- klikání mezi řádky závisí na našem vlastním hit-testu,
- přesun obrázku nemá přirozený vliv na řádky běžného textu,
- druhý obrázek nebo další bloky vedle prvního obrázku se snadno dostanou do špatné vrstvy/oblasti,
- save/reload může vytvořit stav, který vizuálně vypadá jako Word, ale interakčně jím není.

### UI

Současné UI má:

- image selection toolbar,
- pravý image inspector,
- volby alt text, caption, link u URL obrázku, wrap/align/width/height,
- jeden resize handle.

Slabiny:

- jeden resize handle je málo; Word/Docs standard je výběrový rámeček s více body,
- chybí rotation handle,
- chybí jasná layout bublina přímo u obrázku,
- chybí anchor affordance,
- chybí živé vodicí linky a snap,
- chybí přehledná sada přesných voleb pozice,
- wrap mode UI neukazuje plnou sadu modelu (`Tight`, `Through`, `BehindText`),
- `Position left/right/center` je UXově nejasné vůči `Wrap square/top-bottom/in-front`.

## Co přesně musí editor umět jako Word/Google Docs

### 1. Režimy vložení a obtékání

Editor musí podporovat:

- `Inline with text`: obrázek je atomický inline znak v odstavci.
- `Square / Wrap text`: text obtéká obdélník obrázku s definovanou vzdáleností.
- `Tight`: text obtéká přesnější konturu obrázku.
- `Through`: text může proudit skrz otevřené části kontury podle wrap points.
- `Top and bottom / Break text`: text je pouze nad a pod objektem.
- `Behind text`: obrázek je pod textovou vrstvou, text jde přes něj.
- `In front of text`: obrázek je nad textovou vrstvou, text ho neobtéká.

Google Docs má jednodušší slovník, ale pro náš editor dává smysl cílit na nadmnožinu Wordu, protože `DocumentWrapMode` už ji v modelu má.

### 2. Kotva a vazba na text

Každý ne-inline obrázek musí mít:

- stabilní `objectId`,
- `anchorBlockId`,
- volitelný `anchorInlineId`,
- `anchorOffset`,
- volbu `moveWithText`,
- volbu `fixedOnPage`,
- volbu `lockAnchor`,
- volbu `allowOverlap`.

Důležité rozlišení:

- `Move with text`: objekt zůstává navázaný na odstavec; když se text před ním mění, objekt se posouvá s odstavcem.
- `Fix position on page`: objekt zůstane na souřadnici stránky bez ohledu na změny okolního textu.
- `Lock anchor`: uživatel nemůže nechtěně přepojit objekt na jiný odstavec.

Současné `LockAnchor` nestačí jako náhrada za tyto tři významy. Měli bychom je v modelu oddělit.

### 3. Přesná pozice

Pro každý plovoucí obrázek musí jít nastavit:

- horizontální pozice relativně k stránce, okrajům, sloupci, znaku/odstavci,
- vertikální pozice relativně k stránce, okrajům, odstavci, řádku,
- absolutní X/Y v jednotkách editoru,
- alignment preset: vlevo, střed, vpravo,
- procentuální pozice, pokud chceme kompatibilitu s Word/ODT importem,
- z-index / pořadí objektů,
- povolení překrytí,
- přichycení k okrajům, středu stránky, baseline, objektům a textovým sloupcům.

### 4. Vzdálenost od textu

Pro square/tight/through/top-bottom musí být zvlášť:

- vzdálenost vlevo,
- vzdálenost vpravo,
- vzdálenost nahoře,
- vzdálenost dole.

UI by mělo mít jednoduchý mód pro běžné uživatele: jeden slider/stepper „Mezera od textu“. Pokročilý mód pak čtyři hodnoty.

### 5. Wrap points

Word umí u tight/through upravovat body obtékací kontury. To je pokročilá funkce, ale pokud chceme být „jako Word“, model musí být připraven:

- `WrapContour`: seznam bodů v normalizovaných souřadnicích obrázku,
- editace kontury přes overlay,
- přidání bodu kliknutím/drag,
- smazání bodu,
- reset na obdélník,
- import/export kompatibilita.

Google Docs tento detail běžně nevystavuje tak silně jako Word, takže pro MVP stačí model + read-only kontura. Pro „Word parity“ je to samostatná fáze.

### 6. Výběr, resize, rotate, crop

Výběr obrázku musí mít:

- 8 resize handles: rohy + strany,
- rotation handle nad objektem,
- viditelný selection box oddělený od modrého caret/page focus rámečku,
- badge aktuálního wrap režimu,
- anchor ikonu poblíž odstavce,
- klávesové posuny šipkami,
- jemné posuny `Ctrl`/`Alt` + šipky,
- zachování poměru stran při rohovém resize,
- volitelný resize bez zachování poměru při bočních handlech nebo se speciální klávesou,
- live rozměry při resize,
- crop mód s jiným overlayem,
- reset size na natural size.

Současný jeden handle je UXově nedostatečný. Uživatel z něj nepozná, jestli jde měnit šířku, výšku, poměr stran, nebo jen diagonální resize.

### 7. Drag a live reflow

Při tažení obrázku:

- objekt se musí hýbat plynule,
- text musí průběžně reflowovat nebo se musí ukazovat velmi věrný preview stav,
- vodítka musí ukazovat zarovnání k okrajům stránky, textové oblasti, středu stránky, jiným obrázkům,
- kurzor se nesmí ztratit,
- po uvolnění vznikne jedna undo transakce,
- při Escape během drag se vše vrátí,
- posun nesmí změnit obsah okolního textu.

Pro výkon je možné:

- během drag zobrazit preview overlay a reflow dělat throttlovaně,
- při velmi dlouhém dokumentu přepočítat jen dotčené stránky,
- na `pointerup` udělat finální layout pass.

### 8. Caret a hit-testing

Toto je klíč k problémům z videa.

Editor musí mít vlastní `CaretMap`:

- pro každý řádek znát `lineBox`,
- pro každý text segment znát jeho recty a offset range,
- pro obrázek znát jeho visual rect, wrap rect a selection rect,
- klik na textovou oblast vedle obrázku mapovat na nejbližší text offset v daném řádku,
- klik na prázdnou část řádku vpravo od textu mapovat na konec řádku,
- klik na prázdno uvnitř wrap zóny nemá vybírat obrázek,
- klik na obrázek má vybírat obrázek jen pokud je uvnitř visual/selection rectu nebo handle.

Bez explicitní caret mapy budeme donekonečna opravovat jednotlivé `elementFromPoint` případy.

### 9. Backspace/Delete a textové operace

Musí být definovaná pravidla:

- Backspace uvnitř textu vedle obrázku maže znak vlevo.
- Backspace na začátku vizuálního řádku maže předchozí znak v tomtéž odstavci, ne „nic“.
- Backspace na začátku odstavce sloučí odstavec s předchozím odstavcem.
- Pokud je před caret inline obrázek, Backspace vybere nebo smaže obrázek podle běžného editorového pravidla.
- Delete před obrázkem analogicky pracuje s objektem vpravo.
- Výběr přes text a obrázek musí zahrnout obrázek jako atomický objekt.
- Undo/redo musí vracet text, pozici obrázku i layout ve správném pořadí.

Sidecar odstavec tyto semantiky rozbíjí, protože editor pak neumí poznat, že jde jen o další vizuální řádky stejného textového kontextu.

## Doporučená cílová architektura

### A. Dokumentový model

Doporučený model pro obrázek:

```text
ImageBlockContent
  Source
  Url / AssetId / ClipboardId
  AltText
  Caption
  LinkUrl
  Size
  NaturalSize
  Crop
  Effects
  Layout: DocumentObjectLayout
```

`DocumentObjectLayout` by měl obsahovat:

```text
ObjectLayout
  Kind: Inline | Anchored | Fixed
  WrapMode: Inline | Square | Tight | Through | TopBottom | BehindText | InFrontOfText
  Anchor:
    BlockId
    InlineId
    Offset
    Region
    PageIndex?
    MoveWithText
    LockAnchor
  Position:
    HorizontalRelativeTo: Page | Margin | Column | Character | Paragraph
    VerticalRelativeTo: Page | Margin | Paragraph | Line
    X
    Y
    HorizontalAlignment?: Left | Center | Right | Inside | Outside
    VerticalAlignment?: Top | Center | Bottom
  Wrap:
    DistanceLeft
    DistanceRight
    DistanceTop
    DistanceBottom
    ContourPoints[]
  Transform:
    Width
    Height
    Rotation
    LockAspectRatio
  Stacking:
    ZIndex
    AllowOverlap
```

Současné `DocumentFloatingLayout` lze evolučně rozšířit nebo z něj vytvořit kompatibilní wrapper. Nemusíme nutně všechno zahodit, ale potřebujeme jasně oddělit anchor, position, wrap, transform a stacking.

### B. Layout engine

Potřebujeme explicitní layout pass:

1. Vstup: document blocks + page settings + floating objects.
2. Rozdělit text do odstavců a inline runs.
3. Umístit inline obrázky jako atomické glyphy.
4. Pro anchored/fixed objekty spočítat objektové recty.
5. Z rectů spočítat wrap exclusion zones.
6. Sázet řádky odstavců do dostupných intervalů mezi exclusion zónami.
7. Uložit line boxes a segment boxes pro hit-testing.
8. Rozdělit obsah přes stránky.
9. Vygenerovat render tree/DOM s datovými atributy pro každý line/segment/object.

Klíčový pojem je `exclusion zone`: oblast stránky, kam nesmí vstoupit text. Square obrázek je obdélníková exclusion zone + distance. Tight/Through je polygonová zóna.

### C. Rendering

Doporučené vrstvy stránky:

- page background,
- header,
- body text layer,
- behind-text object layer,
- inline/anchored object layer,
- in-front object layer,
- selection/caret overlay,
- guides/handles overlay,
- floating UI portals.

Text by neměl být renderovaný jako jeden contenteditable proud s CSS floatem, pokud chceme přesnou line/caret kontrolu. Lepší je mít layout-aware DOM, kde textové runs zůstávají editovatelné, ale řádky a objekty mají explicitní souřadnice/recty.

Realistický kompromis pro Blazor/WASM:

- zachovat contenteditable textové bloky pro jednoduché odstavce,
- pro paged WYSIWYG přidat interní layout snapshot a caret mapu,
- postupně přestat používat sidecar pro wrapped images,
- renderovat wrapped text jako normální odstavce s layout metadaty, ne jako sidecar block.

### D. Hit-testing service

Zavést jednotný JS modul:

```text
DocumentHitTestService
  hitTest(x, y):
    returns TextCaret | ImageObject | ImageHandle | TableCell | PageMargin | HeaderFooter | None

  caretFromPoint(x, y):
    uses layout line boxes, not only document.caretRangeFromPoint

  objectFromPoint(x, y):
    checks handles, selection box, visual rect, z-order
```

Tento service musí být jediná autorita pro:

- kliknutí do textu,
- kliknutí na obrázek,
- drag obrázku,
- výběr textu přes obrázek,
- kontextové menu,
- e2e debug snapshot.

### E. Editační engine

Backspace/Delete/Enter musí pracovat nad dokumentovým modelem, ne nad náhodným DOM stavem:

- `DeleteBackwardCommand`,
- `DeleteForwardCommand`,
- `SplitParagraphCommand`,
- `MergeParagraphCommand`,
- `InsertTextCommand`,
- `MoveObjectCommand`,
- `ResizeObjectCommand`,
- `ChangeObjectLayoutCommand`.

Při každém commandu:

- před akcí uložit selection snapshot,
- změnit model,
- spustit layout invalidaci,
- renderovat změnu,
- obnovit caret podle modelového offsetu,
- vytvořit jednu undo transakci.

## UX/UI návrh

### Výběr obrázku

Po kliknutí na obrázek:

- obrázek dostane tenký modrý selection rámeček,
- kolem rámečku se zobrazí 8 resize bodů,
- nad rámečkem se zobrazí rotation handle,
- poblíž levého horního okraje se zobrazí malá anchor ikona, pokud je objekt anchored,
- přímo pod/vedle obrázku se zobrazí kompaktní layout bubble.

Selection rámeček musí být vizuálně odlišný od modrého rámečku textové oblasti stránky, aby uživatel chápal, jestli je vybraný obrázek nebo textový region.

### Layout bubble

Bubble po vzoru Google Docs:

- `Inline`,
- `Wrap`,
- `Break`,
- `Behind`,
- `Front`,
- `Move with text`,
- `Fix position`,
- `More options`.

Ikony musí být srozumitelné: malá stránka, čáry textu a objekt. Textové popisky se zobrazí v tooltipu nebo pod ikonou podle šířky.

Bubble musí:

- nikdy nepřekrývat pravý panel,
- držet se u obrázku,
- při scrollu zůstat poblíž objektu nebo se schovat,
- zavřít se při kliknutí mimo obrázek a UI,
- nezavírat se při práci uvnitř vlastního popoveru.

### Pravý inspector

Pravý panel by měl být strukturovaný do sekcí:

1. `Obrázek`
   - zdroj,
   - alt text,
   - caption,
   - link,
   - replace.
2. `Velikost a otočení`
   - šířka,
   - výška,
   - lock aspect ratio,
   - reset,
   - rotation.
3. `Obtékání textu`
   - wrap mode jako ikony,
   - vzdálenost od textu,
   - pokročilé čtyři vzdálenosti.
4. `Pozice`
   - vlevo/střed/vpravo,
   - move with text / fix position,
   - relativně k stránce/okraji/odstavci,
   - X/Y.
5. `Pořadí`
   - dopředu/dozadu,
   - před text/za text,
   - allow overlap.
6. `Přístupnost`
   - alt text status,
   - upozornění při prázdném alt textu,
   - dekorativní obrázek.

### Kontextové menu

Pravý klik na obrázek:

- Replace image
- Crop image
- Alt text
- Caption
- Link
- Text wrapping submenu
- Position submenu
- Bring forward / Send backward
- Reset size
- Delete

Replace nesmí rovnou otevírat file picker. Má nabídnout zdroje: upload, URL, asset/provider, clipboard.

### Drag/resize polish

Během drag/resize:

- zobrazit průsvitný preview bounding box,
- zobrazit aktuální rozměry,
- zobrazit snap vodítka,
- text reflowovat živě/throttlovaně,
- při kolizi s okraji stránky ukázat jemné varování,
- při `Shift` zachovat poměr stran,
- při `Alt` jemnější krok,
- při `Escape` vrátit změnu.

### Přístupnost a klávesnice

Obrázek musí jít ovládat bez myši:

- Tab/focus na obrázek,
- Enter otevře layout bubble,
- ContextMenu/Shift+F10 otevře menu,
- šipky posouvají objekt,
- Shift+šipky zvětšují/zmenšují podle aktivního handle módu,
- Ctrl+Alt+Y nebo podobná zkratka pro alt text podle platformní konvence,
- screen reader oznámí alt text, wrap mode, velikost a vazbu na text.

## Přesné akceptační scénáře

### Kliknutí na text vedle obrázku

Scénář:

1. Obrázek je `Square`, pozice vlevo.
2. Vedle něj je text na více řádcích.
3. Uživatel klikne na druhý řádek vpravo od obrázku.

Musí platit:

- caret je na druhém řádku na nejbližším znakovém offsetu,
- obrázek není vybraný,
- image toolbar není viditelný,
- textový toolbar odpovídá formátování v daném místě,
- další psaní pokračuje přesně tam,
- Backspace maže znak vlevo od caret.

### Backspace na začátku obtékajícího textu

Scénář:

1. Caret je na začátku textu vizuálně vedle obrázku.
2. Uživatel stiskne Backspace.

Musí platit:

- pokud je to začátek vizuálního řádku, ale ne začátek odstavce, smaže se předchozí znak,
- pokud je to začátek odstavce, odstavec se sloučí s předchozím odstavcem,
- obrázek se nesmaže, pokud není vybraný jako objekt,
- žádný sidecar block nezůstane prázdný jako artefakt.

### Posun obrázku

Scénář:

1. Obrázek je vlevo a obtéká ho několik řádků textu.
2. Uživatel táhne obrázek dolů a doprava.

Musí platit:

- text se průběžně nebo po krátkém throttle reflowuje,
- řádky změní šířku podle nové exclusion zóny,
- žádný text neleze přes obrázek,
- po puštění vznikne jedna undo transakce,
- save/reload zachová pozici a layout.

### Resize obrázku

Scénář:

1. Uživatel táhne pravý dolní roh obrázku.

Musí platit:

- šířka a výška se mění s lock aspect ratio,
- obtékající text okamžitě reaguje,
- pravý panel ukazuje aktuální hodnoty,
- při tažení bočního handle se mění pouze jedna osa, pokud je aspect ratio vypnuté nebo uživatel drží příslušný modifier,
- caption a selection box se nepřekrývají.

### Behind/In front

Scénář:

1. Uživatel nastaví `Behind text`.

Musí platit:

- text zůstane editovatelný nad obrázkem,
- kliknutí do textu vybere text, ne obrázek,
- obrázek lze vybrat přes selection pane nebo přes explicitní objektový hit target,
- z-index je perzistentní.

### Tight/Through

Scénář:

1. Obrázek má polygonovou konturu.
2. Uživatel nastaví `Tight`.

Musí platit:

- text respektuje konturu,
- změna wrap points přepočítá řádky,
- export/import zachová konturu nebo bezpečně degraduje na square s `PreservedWrapMode`.

## Přísné E2E testy, které budou potřeba

Nové testy nesmí volat interní command jako náhradu uživatelské akce. Interní JS je v pořádku pro měření, snapshot a deterministickou přípravu dat, ale ne pro samotnou interakci.

Testovací matice:

- každý wrap mode,
- pozice vlevo/střed/vpravo,
- move with text vs fix position,
- lock anchor on/off,
- resize rohy a strany,
- drag uvnitř stránky a přes okraj,
- kliknutí do každého řádku vedle obrázku,
- Backspace/Delete před/za textem a před/za obrázkem,
- save/reload,
- undo/redo,
- copy/paste,
- export/import, minimálně přes interní model a později ODT/DOCX,
- viewporty desktop/tablet/mobile,
- pravý panel otevřený/zavřený,
- více obrázků vedle sebe a nad sebou,
- obrázek za textem/před textem.

Každý test musí po akci ověřit:

- browser selection/caret,
- model selection,
- aktivní toolbar/inspector state,
- viditelný text,
- line geometry,
- image rect,
- absence překryvů,
- absence stale menu/popover,
- undo stack,
- dirty/save stav,
- screenshot/debug artifact při selhání.

Příklad geometrické kontroly:

- žádný textový rect se nesmí protínat s image exclusion rectem,
- text vedle levého obrázku musí mít `left >= image.right + distanceRight`,
- text vedle pravého obrázku musí mít `right <= image.left - distanceLeft`,
- po posunu obrázku se musí změnit alespoň jeden line box dotčeného odstavce,
- kliknutý bod na druhém řádku musí skončit v caret rectu stejného řádku.

## Doporučený implementační směr

### Fáze 1: Specifikace modelu a compatibility map

- Zmapovat Word/ODT/DOCX/Google Docs pojmy na náš model.
- Rozšířit `DocumentFloatingLayout` nebo vytvořit `DocumentObjectLayout`.
- Oddělit `MoveWithText`, `FixedOnPage`, `LockAnchor`, `AllowOverlap`.
- Přidat `Rotation`, `Crop`, `WrapContour`, `WrapDistance`.
- Připravit migraci starých dokumentů.

### Fáze 2: Layout snapshot a line box model

- Zavést layout pass pro stránku.
- Pro každý odstavec ukládat line boxes.
- Pro každý obrázek ukládat visual rect, wrap rect, selection rect.
- Vytvořit debug overlay a testovací snapshot.

### Fáze 3: Nahradit sidecar pro nové scénáře

- Nové wrapped images nesmí vytvářet sidecar odstavec.
- Text vedle obrázku musí být běžný odstavec.
- Staré sidecar dokumenty migrovat do normálního textového toku.
- Dočasně držet read-only fallback pro staré stavy, ale nevytvářet nové.

### Fáze 4: Hit-testing a caret map

- Jednotný hit-test service.
- Kliknutí do druhého/třetího řádku vedle obrázku.
- Kliknutí do prázdné oblasti řádku.
- Kliknutí na visual rect obrázku.
- Kliknutí na handle.

### Fáze 5: Editing semantics

- Backspace/Delete kolem obrázků.
- Enter v textu obtékajícím obrázek.
- Výběr přes text a obrázek.
- Copy/paste.
- Undo/redo transakce.

### Fáze 6: UI polish

- 8 resize handles.
- Rotation handle.
- Layout bubble.
- Anchor glyph.
- Vodítka, snap, live rozměry.
- Přepsat image inspector do sekcí.

### Fáze 7: Advanced parity

- Tight/Through kontury.
- Edit wrap points.
- Selection pane pro objekty za textem.
- Bring forward/back.
- Grouping více objektů.
- Export/import parity.

## Doporučení k aktuálnímu sidecar řešení

Sidecar bych už dál nerozvíjel jako cílový směr. Může dočasně zůstat jako compatibility fallback, ale nové chování by mělo být postavené jinak.

Konkrétní doporučení:

- Nepřidávat další speciální pravidla typu „když kliknu na druhý řádek sidecaru, udělej X“.
- Nepřidávat další záplaty pro konkrétní kombinace `figureRect`, `visualRect`, `elementFromPoint`.
- Připravit migrační command: sidecar text se převede do normálního odstavce za/okolo obrázku podle anchoru.
- Všechny nové testy psát proti cílovému modelu, ne proti existenci `data-wrap-sidecar-for`.

## UX laťka

Editor bude působit důvěryhodně až ve chvíli, kdy uživatel nebude muset přemýšlet, jestli je text vedle obrázku „speciální“. Musí prostě:

- kliknout kam chce,
- psát kde chce,
- posunout obrázek,
- vidět text okamžitě reagovat,
- mít jasné handles,
- mít jasné volby obtékání,
- nevidět žádné technické artefakty.

Současný stav se snaží správné chování simulovat. Cílový stav ho musí skutečně modelovat.

## Nejbližší praktický výstup

Jako další krok bych nevzal jednu malou opravu aktuálního videa. Vzal bych to jako signál k nové implementační fázi:

1. Napsat malou technickou specifikaci `DocumentObjectLayout`.
2. Napsat RED E2E pro kliknutí na druhý řádek obtékajícího textu bez sidecar předpokladu.
3. Napsat RED E2E pro Backspace na začátku obtékajícího řádku.
4. Napsat RED E2E pro live reflow po drag/resize.
5. Teprve potom začít měnit architekturu.

Tím se vyhneme tomu, že opravíme jen další viditelný symptom a za hodinu narazíme na další variantu stejného problému.
