# Analýza práce s obrázky: Tempo.Blazor vs OnlyOffice

Datum: 2026-05-25

## Cíl analýzy

Tento dokument porovnává současnou práci s obrázky v `TmDocumentEditor` s architekturou OnlyOffice v lokálním repozitáři `/home/pavel/NetProjects/onlyfficeservergit`. Hlavní důraz je na:

- obtékání textem,
- resize,
- drag & drop / přemisťování,
- focus a caret chování,
- psaní vedle obrázku, když vedle něj ještě není žádný text,
- odstranění nutnosti obalovat obrázek samostatným odstavcem,
- inspiraci pro přiblížení editoru úrovni OnlyOffice.

Krátký závěr: v Tempu už existuje poměrně bohatý model layoutu obrázků, ale praktické chování je pořád zásadně limitované tím, že obrázek je v dokumentu primárně `block`. OnlyOffice obrázek řeší jako drawing objekt vložený do běhu odstavce (`ParaDrawing`) s možností inline nebo ukotveného režimu. To je hlavní architektonický rozdíl, který vysvětluje většinu současných problémů.

## Shrnutí hlavních zjištění

Tempo dnes kombinuje tři různé přístupy:

1. V datovém modelu je obrázek stále samostatný `ImageBlockContent`.
2. V novějším layout enginu existuje objektový layout s anchor/wrap/position/transform informacemi.
3. Ve vykreslení se část chování řeší absolutní objektovou vrstvou, ale část stále přes CSS float a focusovatelný `<figure>`.

OnlyOffice má konzistentnější model:

1. Obrázek je drawing objekt uvnitř odstavce, ne samostatný blok dokumentu.
2. Anchor je vázaný na konkrétní odstavec / pozici v textu.
3. Obtékání je součástí layoutu odstavců, ne dodatečný CSS efekt.
4. Drag a resize běží přes dočasné track objekty v overlay vrstvě a do dokumentu se zapisuje až finální operace.
5. Výběr obrázku je stav editorového controlleru, ne běžný DOM focus prvku uvnitř contenteditable.

Důsledek pro Tempo: pokud chceme kvalitativní skok, nestačí ladit resize handle nebo CSS float. Je potřeba přesunout obrázek z "blokového obsahu" na "drawing objekt ukotvený v textu" a sjednotit kolem toho layout, selection, hit testing, undo a E2E testy.

## Jak dnes pracujeme s obrázky v Tempo.Blazor

### 1. Datový model: obrázek je stále blok

Základní model je v:

- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlocks.cs:315`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentObjectLayout.cs:34`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentAnchors.cs:104`

`ImageBlockContent` dědí z `DocumentBlockContent`, takže obrázek je položka v hlavním seznamu bloků dokumentu. Má `Size`, `NaturalSize`, `Alignment`, `Layout`, metadata, alternativní text a odkaz.

Současně máme modernější layout model:

- `DocumentObjectLayoutKind`: `Inline`, `Anchored`, `Fixed`
- `DocumentObjectAnchor`: `BlockId`, `InlineIndex`, `Offset`, `Region`, `MoveWithText`, `FixedOnPage`, `LockAnchor`
- `DocumentObjectWrap`: `Mode`, `Distances`, `Contour`
- `DocumentObjectTransform`: `Width`, `Height`, `Rotation`, `ScaleX`, `ScaleY`
- `DocumentObjectStacking`: `ZIndex`, `BehindText`, `AllowOverlap`

To je dobrý základ, ale zatím se plně nepropsal do struktury dokumentu. Layout už umí říkat "tento objekt je ukotvený k odstavci", ale samotný obrázek je pořád samostatný blok za odstavcem nebo před ním. To vytváří mezery v chování:

- nejde přirozeně říct "tento obrázek je mezi znakem 8 a 9 v odstavci",
- nejde přirozeně vytvořit caret pozici vedle obrázku uvnitř téhož odstavce,
- šipky v textu logicky narazí na obrázek jako na další blok,
- inline obrázek není opravdu znak v textu, ale samostatný objektový blok.

### 2. Vkládání obrázku: vždy vzniká nový blok

V runtime JS se obrázek vkládá v `applyInsertImage`:

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:1772`

Současné chování:

- najde se aktivní kontejner,
- vytvoří se nový block,
- block se vloží za aktuální block,
- následná selection se nastaví na image block.

To znamená, že i když uživatel stojí uprostřed věty, vložení obrázku nevytvoří drawing objekt v aktuálním odstavci. Místo toho se dokument rozsekne na samostatný image block. Tím se do systému dostává stejný problém, který uživatel popisuje: kolem obrázku jako by musel existovat nový odstavec / samostatný blok.

OnlyOffice se v tomto zásadně liší: obrázek je `ParaDrawing`, tedy run-level element v odstavci. Vložení obrázku přidává drawing do aktuálního paragraph/run kontextu, ne jako samostatný top-level block.

### 3. Normalizace layoutu: dobrý směr, ale nad blokovým základem

JS runtime normalizuje image layout v:

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:5262`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:5341`

`normalizeImageObject` umí získat:

- `anchorBlockId`,
- `anchorOffset`,
- `moveWithText`,
- `fixedOnPage`,
- `horizontalPosition`,
- `verticalPosition`,
- `wrapMode`,
- wrap distance,
- width/height,
- z-index.

To znamená, že Tempo už částečně uvažuje stejně jako Word/OnlyOffice: objekt má anchor, pozici, wrapping a stacking. Slabina je v tom, že anchor není primární identita objektu. Primární identita je pořád image block v toku dokumentu.

Další slabé místo je serializace v `imageObjectToLayout`. Ta dnes vrací zploštěný DTO tvar (`AnchorBlockId`, `AnchorOffset`, `WrapMode`, `Width`, `Height`, ...), zatímco C# model je strukturovanější (`Anchor`, `Position`, `Wrap`, `Transform`, `Stacking`). To zvyšuje riziko, že JS a C# nebudou mít úplně stejnou představu o layoutu.

### 4. Layout engine: obtékání existuje, ale objekt je stále odvozený z bloku

C# layout je hlavně v:

- `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentLayoutEngine.cs:420`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentLayoutEngine.cs:481`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentLayoutEngine.cs:584`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentLayoutGeometryHelper.cs:287`
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentLayoutGeometryHelper.cs:336`

Pozitivní části:

- máme `CreateExclusionZone`,
- `TopBottom` umí zabrat celou šířku body,
- `Square`, `Tight`, `Through` vytváří intervaly pro obtékání,
- existuje polygon sampling pro contour,
- layout odstavce se ptá na dostupné line intervaly.

Slabá místa:

- `BuildPreAnchoredImageMap` mapuje floating obrázky na předchozí text block, pokud chybí anchor. To je praktický hack, ne plnohodnotný anchor model.
- `LayoutInlineImageBlock` pořád layoutuje inline obrázek jako samostatný blok/paragraph-like objekt, ne jako znak uvnitř odstavce.
- Anchor rect se často odvozuje od block rect nebo `currentY`, ne od přesné pozice znaku/line boxu v odstavci.
- Objektové layoutování je stále podřízené tomu, že v hlavním toku existuje image block.

Výsledek: obtékání může vizuálně fungovat pro některé scénáře, ale editor nemá přirozený textový model, ve kterém by caret a psaní vedle obrázku fungovaly stejně jako ve Wordu nebo OnlyOffice.

### 5. JS layout: objektová vrstva existuje, ale paralelně s legacy flow

V JS layoutu:

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:5919`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:6409`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:6585`

Při layoutu image blocku se vytvoří `anchoredObject`. Podle wrap mode se rozhoduje, zda objekt spotřebuje flow (`Inline`, `TopBottom`) nebo je floating (`Square`, `Tight`, `Through`, ...). Floating objekt se přidá do object layer a vytvoří exclusion zónu pro následující text.

To je blízko správnému směru. Problém je v tom, že objekt není "drawing anchored at paragraph offset", ale "block, který se při layoutu tváří jako object". To se pak rozbije v caret navigaci, psaní, hit testingu a undo granularitě.

V atomic rendereru už existuje dobrá separace:

- `textLayer`,
- `objectLayer`.

To bychom měli posílit a udělat z ní hlavní WYSIWYG cestu. Legacy cesta přes CSS float by měla zůstat maximálně jako read-only fallback, ne jako zdroj chování editoru.

### 6. Legacy render: CSS float a focusovatelný figure

Legacy HTML render obrázku:

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:13352`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:13709`
- `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css:3326`
- `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css:3389`

Obrázek se renderuje jako:

- `<figure class="tm-wysiwyg-image ...">`,
- `role="figure"`,
- `tabindex="0"`,
- uvnitř `<img>`,
- selection box,
- resize handles,
- layout bubble.

Problém: focusovatelný DOM `figure` uvnitř editoru se začne chovat jako samostatná zastávka v dokumentu. To vysvětluje uživatelský pocit, že šipka nahoru/dolů v textu před nebo za obrázkem nesmyslně "fokusuje obrázek". Technicky to není jen jeden bug, ale důsledek spojení:

- obrázek je samostatný block,
- selection má region `Image`,
- figure má `tabindex=0`,
- hit testing vrací object selection,
- caret model neumí považovat obrázek za pouhý vizuální objekt ukotvený k textu.

V OnlyOffice není obrázek obyčejný focusovatelný DOM element uprostřed contenteditable. Je to drawing objekt řízený grafickým controllerem a výběr se kreslí v overlay vrstvě.

### 7. Hit testing a selection

Důležité části:

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:7852`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:7929`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:11322`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:12831`

`pointerHitTest` umí prokliknout text i objekt. Pokud bod spadne do image blocku, vrací `type: 'object'`. `imageSelectionForBlock` vytváří selection s:

- `region: 'Image'`,
- `isObjectSelection: true`,
- range `0..1`.

To je pro kliknutí na obrázek v pořádku. Problematičtější je, že objektová selection používá stejný výběrový mechanismus jako textový caret. Jakmile se obrázek ocitne v logické navigaci jako block, šipky a focus obnovování ho mohou brát jako legitimní caret target.

Doporučení: oddělit "text caret selection" a "object selection" podobně jako OnlyOffice:

- textový caret nikdy automaticky nepřeskakuje na image focus při ArrowUp/ArrowDown,
- object selection vzniká explicitně kliknutím na obrázek, Tab/object navigation záměrem, nebo příkazem,
- Escape z object selection vrátí caret k anchor pozici,
- Delete/Backspace smaže obrázek jen při explicitním object selection nebo při caret pozici těsně u inline drawing runu.

### 8. Drag & drop: dnes přepisujeme layout objektu, ale nereanchrujeme jako OnlyOffice

Tempo preview controller:

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:5467`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:5487`
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js:5535`

Současný princip:

- na začátku drag/resize se uloží původní layout,
- při pohybu se mění preview layout,
- na každý preview se znovu volá layout dokumentu,
- při commit se vytvoří jeden `UpdateImageLayout` patch.

Dobré:

- commit je jeden undo krok,
- text se při preview může přelévat,
- máme základ pro live feedback.

Slabé:

- full layout na každý mousemove může být drahý,
- drag primárně mění X/Y offsety, ne vždy hledá nejbližší paragraph/offset anchor,
- neexistuje robustní reanchor podle pozice dropu,
- chybí explicitní "nearest text position" algoritmus ve stylu OnlyOffice,
- zero hodnoty v `setImageObjectPosition` jsou rizikové, protože podmínka typu `if (body.x ?? body.X)` neprojde pro `0`.

OnlyOffice naopak při pohybu pracuje s track objektem, snap vodítky a při dokončení umí přepočítat nejbližší pozici v dokumentu, znovu nastavit anchor a objekt zapsat do dokumentu. To je přesně směr, který bychom měli převzít.

### 9. Resize: máme handle UI, ale chybí plnohodnotný track model

Tempo má:

- osm resize handles,
- rotation handle,
- resize badge,
- částečný lock aspect ratio,
- commit jako `UpdateImageLayout`.

OnlyOffice má v `ResizeTrackShapeImage` oddělený track objekt, který drží:

- původní objekt,
- původní transform,
- handle,
- fixed point,
- rotation,
- flips,
- původní a aktuální extents,
- aspect ratio logiku,
- min extents,
- preview transform.

Relevantní OnlyOffice místa:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/DrawingStates.js:1955`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/DrawingStates.js:1994`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/TrackObjects/ResizeTracks.js:193`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/TrackObjects/ResizeTracks.js:620`

Hlavní rozdíl: OnlyOffice během pointermove primárně kreslí preview v overlay vrstvě a nepracuje s dokumentovým modelem jako s živým zdrojem pravdy pro každý pixel pohybu. Do historie a dokumentu se zapisuje až na konci.

Pro Tempo to znamená:

- pointermove by měl měnit DOM/CSS transform track objektu,
- layout textu během tažení může být buď omezený/throttled, nebo preview-only,
- finální layout a reflow by se měly spustit při pointerup,
- undo krok musí zůstat jeden.

### 10. Panely a floating toolbar nad obrázkem

Tempo má aktivní image toolbar ve:

- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor:68`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor:1586`

Příkazy umí:

- změnit wrap mode,
- pozici,
- wrap distance,
- anchor mode,
- smazat obrázek,
- změnit velikost.

To je uživatelsky užitečné, ale chování musí být navázané na explicitní object selection. Toolbar se nemá aktivovat kvůli běžné caret navigaci šipkami v textu.

## Jak pracuje s obrázky OnlyOffice

### 1. Základní objekt: ParaDrawing jako element odstavce

Klíčový soubor:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:57`

OnlyOffice má `ParaDrawing`, což je drawing objekt ležící přímo v runu odstavce. Umí být:

- `drawing_Inline`,
- `drawing_Anchor`.

Wrapping typy:

- `WRAPPING_TYPE_NONE`,
- `WRAPPING_TYPE_SQUARE`,
- `WRAPPING_TYPE_THROUGH`,
- `WRAPPING_TYPE_TIGHT`,
- `WRAPPING_TYPE_TOP_AND_BOTTOM`.

To je přesně architektura, která nám chybí. Obrázek není top-level block, ale položka uvnitř paragraph/run modelu. Teprve jeho layout rozhoduje, zda se vizuálně chová inline, floating, behind text nebo in front of text.

### 2. Nastavení wrapping stylu

OnlyOffice mapuje veřejné API na interní drawing typy například v:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/apiBuilder.js:19324`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:985`

Veřejné styly:

- inline,
- square,
- tight,
- through,
- topAndBottom,
- behind,
- inFront.

Interně se tím mění:

- drawing type (`Inline` vs `Anchor`),
- wrapping type,
- behindDoc,
- padding/distance,
- horizontální/vertikální position config.

Tempo má podobné názvy wrap modes, ale chybí mu stejně pevné propojení mezi UI příkazem, run-level objektem, anchor pozicí a layout enginem.

### 3. Anchor a pozice

Důležité OnlyOffice části:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:1455`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:1717`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:1754`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:2182`

OnlyOffice při přidání nebo přesunu objektu:

- zná parent paragraph,
- zná pozici v dokumentu,
- přepočítává X/Y vůči paragraph/layout referenci,
- při přetažení umí najít nejbližší pozici v dokumentu,
- objekt znovu vloží nebo přenastaví tak, aby jeho anchor odpovídal textovému kontextu.

To je důležité pro chování "obrázek se pohybuje s textem" a "obrázek zůstává ukotvený v rozumném odstavci". U nás se často jen posune absolutní X/Y vůči stránce nebo body, aniž by se semanticky přehodnotil anchor.

### 4. Obtékání: WrapManager a polygon intervals

Klíčové soubory:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/WrapManager.js:46`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/WrapManager.js:228`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/WrapManager.js:799`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/GraphicObjects.js:4747`

OnlyOffice má `CWrapPolygon`, který drží:

- body polygonu,
- relativní body,
- wrap side,
- bounds,
- historii změn.

Při layoutu textu se pro konkrétní Y rozsah počítají blokované intervaly. Podporuje se:

- square,
- top and bottom,
- tight,
- through,
- vzdálenosti od textu,
- wrap side,
- objekty před textem / za textem,
- hlavička/patička,
- tabulky a buňky.

Tempo má podobnou základní myšlenku v `DocumentLayoutGeometryHelper`, ale OnlyOffice ji má organicky napojenou na drawing object controller a paragraph layout. U nás C# část umí víc než některé JS fallbacky a celý systém je rozdělený mezi block model, atomic renderer a legacy float render.

### 5. Výběr a focus

Relevantní OnlyOffice místa:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/GraphicObjects.js:70`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/GraphicObjects.js:3773`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js:1952`

OnlyOffice drží grafické objekty ve vlastním controlleru:

- `selectedObjects`,
- `selection.textSelection`,
- `wrapPolygonSelection`,
- handle event modes,
- overlay drawing.

Výběr obrázku není obyčejný DOM focus uvnitř contenteditable. Šipky v textu primárně navigují caret v textu. Kliknutí na objekt přepne controller do object selection režimu. To je důvod, proč profesionální editory nepůsobí tak, že obrázek náhodně "chytá focus" při vertikálním pohybu caret pozice.

### 6. Drag & drop: track object a reanchor

Relevantní OnlyOffice místa:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/DrawingStates.js:2042`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/DrawingStates.js:2111`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/TrackObjects/MoveTracks.js:43`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/TrackObjects/MoveTracks.js:160`

Princip:

- po pointerdown existuje pre-move stav,
- až po překročení prahu pohybu vzniká move state,
- pohyb se vykresluje jako track/overlay,
- shift může omezit osu pohybu,
- funguje snapping na okraje, střed, jiné objekty,
- při dokončení se provede jedna dokumentová akce,
- objekt se podle cíle může reanchorovat k nejbližší textové pozici.

Tempo má dobrý začátek, ale pohyb by měl být víc "track first, model commit later" a méně "přepočítej layout dokumentu na každý pohyb".

### 7. Resize: oddělený track s geometrií

OnlyOffice resize track:

- drží původní extents,
- řeší handle index,
- fixed point,
- rotation,
- flips,
- aspect ratio,
- min size,
- transform preview,
- historii až na konci.

Tempo má viditelné handles, ale nemá stejně kompletní geometrický track model. Při základním obrázku bez rotace to nemusí být vidět, ale u profesionálního chování se to projeví:

- resize přes různé rohy,
- zachování poměru stran,
- resize kolem středu,
- otočené obrázky,
- snapping,
- undo/redo,
- text reflow až v konzistentních okamžicích.

## Přímé porovnání

| Oblast | Tempo dnes | OnlyOffice | Dopad |
| --- | --- | --- | --- |
| Dokumentový model | `ImageBlockContent` je top-level block | `ParaDrawing` je element odstavce/runu | U nás obrázek přerušuje textový tok a caret model |
| Inline obrázek | Samostatný image block s inline layoutem | Skutečný inline drawing v textu | U nás inline není opravdu znak v odstavci |
| Floating obrázek | Block s layoutem, object layer nebo CSS float | Anchored drawing object | U nás se obtékání chová jako nadstavba nad blokem |
| Anchor | Částečně `AnchorBlockId`/offset, často fallback na předchozí block | Parent paragraph + pozice + relativní X/Y | OnlyOffice umí přirozenější přesuny s textem |
| Obtékání | Intervaly existují, ale JS/C# nejsou plně sjednocené | WrapManager s polygon intervals | U nás hrozí rozdíly mezi engine/render/runtime |
| Drag | Preview mění layout a přepočítává dokument | Track object v overlay, commit na konci | U nás horší výkon a slabší reanchor |
| Resize | Handles + preview + patch | Resize track s fixed point/rotation/aspect/flips | U nás méně přesné a méně rozšiřitelné |
| Focus | `<figure tabindex="0">`, image selection region | Controller selection, overlay | U nás obrázek chytá focus při běžné navigaci |
| Psaní vedle obrázku | Vyžaduje existující paragraph/text block vedle nebo po obrázku | Caret je v paragraphu obtékaném kolem drawing objektu | U nás nejde pohodlně začít psát v prázdném prostoru vedle obrázku |
| Nový odstavec kolem obrázku | Prakticky ano, protože obrázek je block | Není nutné | Toto je jedna z hlavních architektonických mezer |

## Vysvětlení konkrétních problémů z uživatelského pohledu

### Proč obrázek získává focus při šipce nahoru/dolů

Pravděpodobná kombinace příčin:

- image je samostatný block v logickém pořadí dokumentu,
- renderuje se jako focusovatelný `figure` s `tabindex="0"`,
- selection model má samostatný `Image` region,
- hit testing umí vrátit object selection,
- vertikální caret navigace nemá dost silnou politiku "zůstat v textu, pokud uživatel explicitně nevybral objekt".

Správnější chování:

- ArrowUp/ArrowDown z textu hledá nejbližší textovou caret pozici na předchozí/další vizuální linii.
- Obrázek se vybere jen explicitně kliknutím, Tab/object navigation režimem nebo příkazem.
- Obrázek nemá být běžný DOM focus stop uvnitř textového toku.

### Proč nejde normálně začít psát vedle obrázku, když tam ještě text není

Protože prostor vedle obrázku dnes není samostatná textová pozice. Je to jen vizuálně dostupný interval vypočtený layoutem. Pokud tam není paragraph line/caret anchor, není kam vložit text.

V OnlyOffice je obrázek ukotvený v odstavci a textový layout odstavce se kolem něj láme. I prázdný odstavec s anchorem může vytvořit caret pozici v dostupném intervalu. U nás by se musel vytvořit nebo najít paragraph, do kterého caret patří, a ten paragraph musí znát image exclusion.

### Proč obrázek potřebuje nový odstavec/blok kolem sebe

Protože `applyInsertImage` vkládá samostatný block za aktuální block. I když layout říká `Inline`, modelově nejde o inline run. To je zásadní rozdíl proti Wordu/OnlyOffice.

### Proč resize/drag může působit těžkopádně

Při preview se v JS volá layout dokumentu. To je dražší než track objekt v overlay vrstvě. U jednoduchých dokumentů to může stačit, ale u složitějších dokumentů, hlaviček/patiček, revizí a obrázků s obtékáním je lepší model:

- pointermove = levný overlay transform,
- throttle/volitelný lightweight reflow preview,
- pointerup = jeden semantický commit,
- undo = jeden krok,
- layout = konzistentní po commitu.

## Co si z OnlyOffice vzít

### 1. Obrázek jako drawing run, ne block

Zavést model typu `DocumentDrawingObject` / `DrawingRun`, který může být součástí odstavce:

- `ObjectId`,
- `Kind = Image`,
- `AssetId` / `Source`,
- `AltText`,
- `Caption`,
- `Layout`,
- `Size/NaturalSize`,
- metadata.

Anchored/floating obrázek by stále žil v objektové vrstvě, ale jeho anchor by byl v odstavci na konkrétní inline pozici. Top-level `ImageBlockContent` ponechat jen jako legacy import/fallback a postupně migrovat.

### 2. Jeden objektový layout model pro C# i JS

Sjednotit JS DTO a C# model:

- `Anchor`,
- `Position`,
- `Wrap`,
- `Transform`,
- `Stacking`.

Neudržovat paralelně zploštěné a strukturované varianty, pokud to není jen kompatibilní deserializační vrstva.

### 3. Objektová vrstva jako hlavní WYSIWYG renderer

Atomic renderer s `textLayer` a `objectLayer` je správný směr. Pro editaci by měl být hlavní:

- text se renderuje jako text/caret layer,
- obrázky jako drawing objects v object layer,
- selection handles v overlay/decorator layer,
- legacy CSS float jen pro fallback/statický render.

### 4. Explicitní object selection

Změnit pravidla:

- kliknutí na obrázek vybere obrázek,
- šipky v textu nevybírají obrázek,
- Tab může přepínat objekty jen v explicitním objektovém režimu nebo podle přístupnostní politiky,
- Escape z obrázku vrací caret na anchor,
- toolbar/panel se otevře jen při explicitním object selection.

### 5. Caret v obtékaném odstavci

Textový layout musí znát dostupné intervaly kolem objektů a caret hit testing musí umět:

- najít line interval vedle obrázku,
- vytvořit caret pozici v prázdném odstavci vedle obrázku,
- vložit text do anchor paragraphu,
- nerozbít text na zvláštní block jen proto, že vedle obrázku zatím žádný text není.

### 6. Drag jako track + reanchor

Převzít princip:

- pre-drag threshold,
- overlay track,
- snapping,
- výpočet nearest paragraph/inline offset,
- commit jedné operace,
- přepočet relativního X/Y vůči anchor referenci,
- zachování `MoveWithText`, `FixedOnPage`, `LockAnchor`.

### 7. Resize jako track

Zavést interní resize track:

- original rect,
- handle,
- fixed point,
- aspect ratio,
- min/max,
- rotation-ready design,
- preview transform,
- commit one operation.

### 8. Pokročilejší wrapping

Postupně doplnit:

- wrap side: both / left / right / largest,
- přesnější distances per side,
- Tight/Through podle contour polygonu,
- editace wrap boundary později,
- shodná geometrie v C# i JS.

## Doporučený cílový návrh pro Tempo

### Vrstva modelu

Přidat vedle blocků nový koncept drawing objektu:

- paragraph obsahuje text runs a drawing runs,
- drawing run nese `ObjectId`,
- objektová data mohou být buď přímo v runu, nebo v centrální kolekci `DocumentDrawingObjects`,
- anchor je primárně paragraph/run position, ne fallback na předchozí block.

Legacy:

- `ImageBlockContent` zatím zachovat kvůli kompatibilitě,
- při otevření dokumentu migrovat image blocky na drawing objects, pokud je editor ve WYSIWYG strict režimu,
- export/import musí zachovat starší model, dokud nebude migrace hotová.

### Vrstva layoutu

Layout dokumentu by měl běžet v pořadí:

1. Layout paragraphu zjistí anchored drawing objekty relevantní pro daný page/region.
2. Object manager poskytne exclusion intervals pro line y range.
3. Line breaker vybere dostupný interval.
4. Caret mapování ukládá line/interval informace.
5. Object layer renderuje drawing objekty podle vypočtených rectů.

Inline drawing se chová jako glyph/inline box s vlastní šířkou a výškou. Anchored drawing není součástí line šířky, ale je navázaný na paragraph offset a vytváří exclusions podle wrap mode.

### Vrstva selection/focus

Rozdělit selection state:

- `TextSelection`: anchor/focus caret v textu,
- `ObjectSelection`: explicitně vybrané drawing objekty,
- `SelectionMode`: Text / Object / Mixed později.

DOM focus má zůstat na editor hostu. Obrázky nemají být samostatné `tabindex=0` prvky v běžném toku. Přístupnost se dá řešit roving selection režimem a aria popisem aktivního objektu, ne tím, že obrázek bude náhodný focus stop mezi odstavci.

### Vrstva interakcí

Drag:

- pointerdown na object handle/body,
- threshold,
- track preview,
- guides,
- nearest paragraph/offset,
- commit `MoveDrawingObject`.

Resize:

- pointerdown na handle,
- track preview,
- badge,
- commit `ResizeDrawingObject`.

Typing:

- pokud caret hitne prázdný interval vedle floating obrázku, vytvořit text caret v anchor paragraphu,
- vložení znaku upraví paragraph text, ne image block okolí,
- pokud anchor paragraph neexistuje, vytvořit ho jako textový paragraph v daném regionu, ne nový image-wrapper paragraph.

## TDD/E2E roadmap

### Fáze A: Reprodukční testy současných problémů

1. E2E: Vložit obrázek se Square wrap uprostřed odstavce, stisknout ArrowUp/ArrowDown v textu před/za obrázkem a ověřit, že aktivní selection zůstává textová.
2. E2E: Kliknutí přímo na obrázek aktivuje object selection a zobrazí image toolbar.
3. E2E: Escape z object selection vrátí caret na anchor pozici v textu.
4. E2E: Do prázdného prostoru vedle Square obrázku lze začít psát bez vytvoření samostatného obrázkového odstavce.
5. E2E: Obrázek vložený uprostřed věty nerozseká dokument na tři top-level bloky.
6. E2E: Backspace/Delete v textu vedle obrázku nemaže obrázek, dokud není explicitně selected.
7. E2E: Header/footer s obrázkem a obtékáním udrží caret v header/footer textu a nereaguje pomalu při obyčejném psaní.

### Fáze B: Modelové unit testy

1. Unit: paragraph může obsahovat drawing run.
2. Unit: anchored drawing run má anchor paragraph id a inline offset.
3. Unit: migrace legacy `ImageBlockContent` vytvoří drawing object s odpovídajícím wrap mode.
4. Unit: inline drawing se počítá jako inline box.
5. Unit: anchored drawing vytváří exclusion, ale neodebírá znakový prostor z text runu.

### Fáze C: Layout testy

1. Layout: Square object rozdělí line intervals vlevo/vpravo.
2. Layout: TopBottom zablokuje celou šířku pro daný y rozsah.
3. Layout: Behind/InFront nevytvoří text exclusion.
4. Layout: prázdný paragraph s anchored objectem vytvoří validní caret line interval.
5. Layout: anchor offset se přesouvá s textem při editaci před anchorem.
6. Layout: header/footer exclusions jsou izolované od body flow.

### Fáze D: Selection a focus testy

1. JS unit: ArrowUp/ArrowDown z textové selection nikdy nevrátí object selection bez explicitního objektového režimu.
2. JS unit: pointer hit na image body vrátí object selection.
3. JS unit: pointer hit na text interval vedle obrázku vrátí text caret.
4. JS unit: Escape z object selection vrátí text selection na anchor.
5. E2E: obrázek nemá běžný DOM focus po šipkách z textu.

### Fáze E: Drag/resize track testy

1. JS unit: pointermove při drag nemutuje document model.
2. JS unit: pointerup vytvoří přesně jednu operaci.
3. JS unit: drop spočítá nearest paragraph/offset.
4. JS unit: resize track drží fixed point podle handle.
5. JS unit: resize se Shiftem zachová aspect ratio.
6. E2E: drag obrázku ukáže guides a po undo vrátí původní anchor i pozici.
7. E2E: resize obrázku reflowne text po commitu a undo vrátí text layout.

### Fáze F: Odstranění legacy CSS float jako editační pravdy

1. Přepnout WYSIWYG editaci obrázků na object layer.
2. Zachovat CSS float jen pro statický render/export fallback.
3. Ověřit, že e2e testy nečtou chování z náhodných DOM float efektů.
4. Ověřit, že image toolbar je navázaný na object selection, ne na DOM focus.

## Doporučené pořadí implementace

1. Nejdřív přidat reprodukční E2E testy na focus a psaní vedle obrázku. Bez nich budeme pořád opravovat jen symptomy.
2. Oddělit text selection a object selection v JS runtime.
3. Odstranit běžný `tabindex=0` z obrázků v editačním toku a nahradit explicitním object selection režimem.
4. Přidat drawing run model vedle legacy image blocku.
5. Implementovat migraci image blocku na drawing run/object pro WYSIWYG strict režim.
6. Přepsat insert image tak, aby vkládal drawing do aktuálního paragraphu na caret offset.
7. Upravit line layout a caret hit testing pro prázdné intervaly vedle floating objektů.
8. Přepsat drag na track preview + reanchor při commitu.
9. Přepsat resize na track preview + jeden commit.
10. Sjednotit JS/C# wrap geometry DTO.
11. Teprve potom řešit pokročilé contour editing, wrap side a přesnější DOCX kompatibilitu.

## Rizika a poznámky

Největší riziko je kompatibilita se stávajícími dokumenty a testy. Proto je vhodné neodstraňovat `ImageBlockContent` okamžitě, ale zavést kompatibilní mezivrstvu:

- staré dokumenty načíst,
- image blocky převést do drawing objektů pro editor runtime,
- při serializaci zachovat nový model,
- fallback render nechat dočasně podporovat starý model.

Druhé riziko je rozsah změny. Jedná se o architektonickou změnu selection/layout modelu, ne o malou opravu CSS. Proto je nutné postupovat TDD po malých krocích, ale s jasným cílovým modelem.

Třetí riziko je výkon. Pokud budeme držet současný přístup "relayout na každý pixel pohybu", složitější dokumenty s hlavičkami, patičkami, revizemi a floating objekty budou trpět. OnlyOffice ukazuje vhodnější cestu: track overlay pro interakci, model commit až na konci.

## Verdikt

Ano, z OnlyOffice se tady jednoznačně máme inspirovat. Ne ve smyslu kopírování implementace, ale v architektonických principech:

- obrázek jako drawing objekt ukotvený v odstavci,
- objektová vrstva oddělená od textového caret/focus systému,
- obtékání jako součást line layoutu,
- drag/resize jako overlay track s jedním semantickým commitem,
- explicitní object selection místo náhodného DOM focusu,
- možnost psát vedle obrázku v prázdném obtékaném prostoru.

Současný Tempo model má dost stavebních kamenů, hlavně `DocumentObjectLayout`, object layer a exclusion intervals. Největší práce je přesunout obrázky z blokového chování do skutečného paragraph/drawing modelu a kolem toho narovnat selection, caret a interakce.
