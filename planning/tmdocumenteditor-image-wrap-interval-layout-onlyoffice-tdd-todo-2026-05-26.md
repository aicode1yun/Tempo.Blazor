# TmDocumentEditor - intervalove obtékání obrázků podle principu OnlyOffice/Word TDD TODO

Datum založení: 2026-05-26  
Stav: navrženo, čeká na implementaci  
Priorita: P0 - současné CSS/float obtékání neumí správně obrázek uprostřed ani libovolné přesunutí obrázku  
Navazuje na:

- `planning/tmdocumenteditor-image-handling-onlyoffice-analysis-2026-05-25.md`
- `planning/tmdocumenteditor-image-drawing-onlyoffice-level-tdd-todo-2026-05-25.md`
- aktuální analýzu OnlyOffice `WrapManager` / `Paragraph_Recalculate`

## Proč tento TODO vzniká

Současná implementace se pokusila opravit kolize textu kolem obrázků pomocí skrytého DOM anchoru a CSS `float`. To je slepá ulička. Funguje jen jako aproximace pro obrázek přilepený vlevo nebo vpravo a selhává pro střed, libovolný drag, více objektů, tight/through polygon a přesný caret.

OnlyOffice to řeší jinak:

- floating obrázek je samostatný drawing objekt ukotvený do odstavce,
- objekt má skutečný page/paragraph rect, wrap mode, distance from text, polygon a vrstvu,
- při layoutu každého řádku se spočítají zakázané horizontální intervaly,
- text se sází do doplňku těchto intervalů,
- obrázek uprostřed je přirozený případ: řádek může mít levý i pravý textový interval,
- `TopBottom` je jediný běžný režim, který blokuje celý vodorovný pás,
- drag/resize změní geometrii objektu a invaliduje dotčené odstavce.

Tento dokument rozepisuje náhradu současného hacku po malých TDD krocích.

## Zdrojové principy z OnlyOffice

Tyto soubory jsou inspirace pro architekturu, ne kód ke kopírování:

- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/WrapManager.js`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph_Recalculate.js`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/Paragraph/ParaDrawing.js`
- `/home/pavel/NetProjects/onlyfficeservergit/sdkjs/word/Editor/GraphicObjects/GraphicPage.js`

Mapování principů:

- `CWrapManager.checkRanges` -> náš `TextExclusionManager` / `getAvailableIntervals`.
- `CWrapPolygon.getArrayWrapIntervals` -> výpočet blokovaných intervalů pro `Square`, `Tight`, `Through`, `TopBottom`.
- `Paragraph.private_RecalculateLineFillRanges` -> náš line breaker musí vytvářet více textových range na jednom vizuálním řádku.
- `ParaDrawing.Update_Position` -> náš drag/resize commit musí přepočítat object rect a invalidovat dotčený layout.

Licenční poznámka: OnlyOffice je AGPL. Přebíráme chování a architektonické principy, ne kopírovaný kód.

## Cílový stav

- [ ] `Square` obrázek umístěný vlevo obtéká textem zprava.
- [ ] `Square` obrázek umístěný vpravo obtéká textem zleva.
- [ ] `Square` obrázek umístěný uprostřed obtéká textem z obou stran, pokud mají strany dost místa.
- [ ] `Square` obrázek posunutý na libovolné X/Y souřadnice obtéká podle skutečného objektového obdélníku, ne podle align hodnoty.
- [ ] `Tight` používá skutečný wrap polygon.
- [ ] `Through` používá editovatelný wrap polygon a chová se jako průchozí polygonové obtékání.
- [ ] `TopBottom` jako jediný blokuje celý řádkový pás přes šířku textového rámu.
- [x] `BehindText` a `InFrontOfText` neblokují textový layout.
- [ ] Více obrázků na stejném řádku sloučí blokované intervaly.
- [ ] Příliš úzké boční intervaly se nepoužijí pro text, ale nepřeklopí automaticky celý režim na `TopBottom`.
- [ ] Caret lze umístit do reálného textového intervalu vedle obrázku.
- [ ] V prázdném odstavci vedle floating obrázku lze začít psát na správné vizuální straně.
- [ ] ArrowUp/ArrowDown z textu před/za obrázkem nepřepíná fokus na obrázek.
- [ ] Obrázek se vybere explicitně kliknutím nebo objektovou navigací, ne náhodným pohybem textového caret.
- [ ] Drag a resize mění obtékání okamžitě v preview a po dropu jednou undoable operací.
- [ ] Header, footer a tabulka používají stejnou wrap geometrii jako body.
- [ ] Demo dokument po načtení nemá žádné překrytí obrázku a textu.
- [ ] Všechny E2E testy ověřují skutečné uživatelské chování, ne jen interní JS API.

## Nevyjednatelná pravidla

- [ ] Nepoužívat CSS `float` jako zdroj pravdy pro anchored/floating drawing obtékání.
- [ ] Nepoužívat skrytý DOM anchor jako náhradu za layout engine.
- [ ] `renderDrawingAnchorReservationStyle` nesmí rozhodovat o obtékání textu kolem floating objektu.
- [ ] `Square` na středu se nesmí maskovat jako `TopBottom`.
- [ ] Line breaker musí umět více dostupných intervalů na jednom řádku.
- [ ] Textové segmenty musí být rozdělené mezi intervaly, ne pouze posunuté do prvního intervalu.
- [ ] Každá změna layoutu musí začít RED testem.
- [ ] Test, který dnes potvrzuje špatné chování, se musí přepsat, ne mazat bez náhrady.
- [ ] Drag/resize preview nesmí zapisovat každý pointermove do trvalého dokumentového modelu.
- [ ] Commit drag/resize musí být jeden undo krok.
- [ ] Kód a XML dokumentace jsou anglicky; planning dokumenty jsou česky.

## Dotčené oblasti v našem kódu

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
  - `createTextExclusion`
  - `getAvailableIntervals`
  - `blockedIntervalsForExclusionGeometry`
  - `createLineBreaker`
  - `layoutParagraphInScopedFrame`
  - `layoutParagraphAcrossPages`
  - `createDomCaretIntervalsAroundObjects`
  - `renderDrawingFigureStyle`
  - `renderDrawingAnchorReservationStyle`
  - `renderDrawingAnchorHtml`
  - object layer rendering a hit testing
- `src/Tempo.Blazor.Abstractions/DocumentEditor/*`
  - drawing layout model
  - wrap side / distances / polygon
  - serialization snapshoty
- `src/Tempo.Blazor.DocumentFormats/Docx/*`
  - `wp:wrapSquare`
  - `wp:wrapTight`
  - `wp:wrapThrough`
  - `wp:wrapTopAndBottom`
  - distances and wrap side
- `src/Tempo.Blazor.Demo.Api/Services/DemoDocumentEditorStore.cs`
  - demo seed obrázky
- `tests/Tempo.Blazor.Tests/DocumentEditor/*`
- `tests/Tempo.Blazor.E2E/DocumentEditorE2ETests.cs`
- nové E2E/JS testovací soubory z níže uvedených fází

## Doporučené nové testovací soubory

- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapIntervalsJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapLineBreakerJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapObjectLayerJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapCaretJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapDragResizeJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapHeaderFooterJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.DocumentFormats.Tests/DocxDrawing/DocumentDocxDrawingWrapIntervalParityTests.cs`
- [ ] `tests/Tempo.Blazor.E2E/DocumentEditorImageWrapOnlyOfficeParityE2ETests.cs`
- [ ] `tests/Tempo.Blazor.E2E/DocumentEditorImageWrapDemoE2ETests.cs`

Názvy lze upravit podle lokální organizace, ale testovací oblasti musí zůstat oddělené: geometrie, line breaker, DOM/object layer, caret, drag/resize, formáty, E2E.

## Fáze 0: Zastavit špatné očekávání a zapsat baseline

### 0.1 Přepsat špatný center full-band test

- [x] Najít test přidaný pro středový full-band reservation v `DocumentEditorImageDrawingPhase12ObjectLayerJavaScriptTests.cs`.
- [x] Přepsat ho na RED očekávání: `Square` center vrací blokovaný interval pouze kolem objektu, ne full-width pás.
- [x] Test musí explicitně očekávat dva dostupné intervaly na řádku: levý a pravý.
- [x] Test musí selhat se současným `renderDrawingAnchorReservationStyle` řešením.
- [x] Přidat komentář v testu: středový `Square` není `TopBottom`.

### 0.2 Přepsat špatný center E2E test

- [x] Najít E2E test typu `DemoImageCenterWrapUsesFullBandReservation`.
- [x] Přepsat ho na uživatelský scénář: načíst demo, vybrat center Square obrázek, ověřit text vlevo i vpravo od objektového rectu.
- [x] Ověřit, že žádný textový rect nepřekrývá object rect.
- [x] Ověřit, že řádek se neroztáhne do full-band `TopBottom` chování.
- [x] Test musí být RED na současné implementaci.

### 0.3 Přidat diagnostiku aktuálního layoutu

- [x] Přidat JS test hook `snapshotWrapLayoutForTest`.
- [x] Snapshot musí vracet object rect, wrap rect, blocked intervals, available intervals, line segments, caret stops.
- [x] Snapshot nesmí číst CSS float jako zdroj pravdy.
- [x] Snapshot musí obsahovat page/region/headerFooter/table/cell scope.
- [x] Přidat bUnit/JS unit test, že snapshot pro center Square obsahuje reálné X objektu.

### 0.4 Baseline E2E pro demo po načtení

- [x] E2E RED: otevřít demo document editor.
- [x] Ověřit, že po prvním načtení žádný visible text rect nepřekrývá image rect.
- [x] Ověřit zvlášť left, right, center, top-bottom obrázek.
- [x] Ověřit, že center Square nepoužívá full-width reserved band.
- [x] Uložit failure screenshot do test artifacts.

## Fáze 1: Pojmenování a model zdroje pravdy

### 1.1 Zavést jasné typy wrap geometrie v JS runtime

- [x] Přidat interní objekt `TextExclusion`.
- [x] `TextExclusion` musí mít `objectId`.
- [x] `TextExclusion` musí mít `pageIndex`.
- [x] `TextExclusion` musí mít `region`.
- [x] `TextExclusion` musí mít `scopeKey`.
- [x] `TextExclusion` musí mít `wrapMode`.
- [x] `TextExclusion` musí mít `wrapSide`.
- [x] `TextExclusion` musí mít `sourceRect`.
- [x] `TextExclusion` musí mít `wrapRect`.
- [x] `TextExclusion` musí mít `polygon`.
- [x] `TextExclusion` musí mít `distanceLeft/Right/Top/Bottom`.
- [x] `TextExclusion` musí mít `allowOverlap`.
- [x] `TextExclusion` musí mít `zIndex`.
- [x] Přidat normalizační testy pro každý field.

### 1.2 Sjednotit wrap mode názvy

- [x] RED test: všechny vstupy `Inline`, `Square`, `Tight`, `Through`, `TopBottom`, `BehindText`, `InFrontOfText` se normalizují deterministicky.
- [x] RED test: numerické hodnoty z C#/DOCX mappingu se normalizují stejně jako string hodnoty.
- [x] Implementovat jedinou veřejnou normalizační funkci pro runtime.
- [x] Odstranit duplicitní lokální normalizace, které vrací odlišné názvy.
- [x] Ověřit, že `TopAndBottom` alias mapuje na `TopBottom`.

### 1.3 Sjednotit wrap side

- [x] Přidat enum-like JS normalizaci `BothSides`, `Largest`, `Left`, `Right`.
- [x] RED test: neznámý wrap side se chová jako `BothSides`.
- [x] RED test: `Left` znamená text pouze vlevo od objektu.
- [x] RED test: `Right` znamená text pouze vpravo od objektu.
- [x] RED test: `Largest` zvolí širší stranu.
- [x] Přidat mapování z DOCX `wrapText`.

### 1.4 Zrušit align jako rozhodující údaj pro wrap

- [x] RED test: objekt s `horizontalPosition.align = Center` a `rect.x = 300` blokuje interval podle `rect.x`, ne podle align stringu.
- [x] RED test: objekt s `align = Left`, ale drag posunem na střed, obtéká jako středový objekt.
- [x] Implementovat pravidlo: wrap vychází z `object.rect`, align je jen vstup pro výpočet počáteční pozice.

## Fáze 2: Intervalový manager po vzoru OnlyOffice

### 2.1 Vytvořit `TextExclusionManager`

- [x] RED JS test: manager bez objektů vrátí jeden dostupný interval přes celý body frame.
- [x] RED JS test: manager se Square rectem uprostřed vrátí dva dostupné intervaly.
- [x] RED JS test: manager se Square rectem vlevo vrátí jeden pravý interval.
- [x] RED JS test: manager se Square rectem vpravo vrátí jeden levý interval.
- [x] RED JS test: manager s TopBottom rectem vrátí žádný interval a `movedToY` pod objekt.
- [x] Implementovat manager nad stávajícím `getAvailableIntervals` bez změny UI.
- [x] Zachovat cache, ale zahrnout do cache key `wrapSide`, `wrapMode`, `distances`, `polygon`.

### 2.2 Blokované intervaly pro Square

- [x] RED test: Square používá `wrapRect = object rect + distances`.
- [x] RED test: distance left rozšíří blokovaný interval doleva.
- [x] RED test: distance right rozšíří blokovaný interval doprava.
- [x] RED test: distance top/bottom rozšíří vertikální zásah řádků.
- [x] RED test: objekt mimo vertikální rozsah řádku neblokuje nic.
- [x] RED test: objekt mimo horizontální body frame neblokuje nic.
- [x] Implementovat výpočet blokovaného intervalu z `wrapRect`.

### 2.3 Wrap side pro Square

- [x] RED test: `BothSides` vrací zakázaný interval pouze přes objekt.
- [x] RED test: `Left` zakáže interval od object left do pravého okraje frame.
- [x] RED test: `Right` zakáže interval od levého okraje frame do object right.
- [x] RED test: `Largest` u středového objektu vybere širší stranu pro text.
- [x] RED test: při stejné šířce `Largest` používá deterministický fallback.
- [x] Implementovat wrap side bez návaznosti na CSS.

### 2.4 Sloučení blokovaných intervalů

- [x] RED test: překrývající se objekty sloučí zakázané intervaly.
- [x] RED test: intervaly vzdálené méně než minimální mezera se sloučí.
- [x] RED test: malé dostupné mezery pod `minReadableWidth` se odstraní.
- [x] RED test: odstranění malé mezery nesmí změnit `Square` na `TopBottom`.
- [x] Implementovat merge podobný OnlyOffice principu `checkRanges`.

### 2.5 `movedToY` pouze při nulovém dostupném prostoru

- [x] RED test: pokud existuje levý interval, řádek se neposouvá pod obrázek.
- [x] RED test: pokud existuje pravý interval, řádek se neposouvá pod obrázek.
- [x] RED test: pokud neexistuje žádný čitelný interval, řádek se posune na nejbližší spodní hranu blokujících objektů.
- [x] Implementovat `movedToY` jen pro skutečně prázdný doplněk intervalů.

## Fáze 3: Line breaker přes více intervalů na jednom řádku

### 3.1 Rozšířit line breaker vstup

- [x] RED test: `breakParagraph` přijme pro řádek více intervalů.
- [x] RED test: jeden vizuální řádek může mít více `line.ranges`.
- [x] RED test: každý range má `x`, `width`, `start`, `end`, `segments`.
- [x] RED test: `availableIntervals` nejsou jen metadata pro caret, ale skutečné textové kapacity.
- [x] Implementovat interní strukturu `LineRangeDraft`.

### 3.2 Sázení textu do levého a pravého intervalu

- [x] RED test: text delší než levý interval pokračuje ve stejném vizuálním řádku v pravém intervalu.
- [x] RED test: další slovo se neposune pod obrázek, pokud se vejde do pravého intervalu.
- [x] RED test: segment rect v pravém intervalu začíná za object wrap rectem.
- [x] RED test: line rect pokrývá celý vizuální řádek, ale segmenty mají vlastní recty.
- [x] Implementovat přechod `current range -> next range -> next visual line`.

### 3.3 Zachovat správné word wrapping

- [x] RED test: mezera na konci levého intervalu se neztratí.
- [x] RED test: slovo se nepřilepí k dalšímu slovu při přechodu mezi intervaly.
- [x] RED test: dlouhé slovo se dělí podle existující logiky v rámci aktuálního intervalu.
- [x] RED test: CJK tokeny fungují přes intervaly.
- [x] RED test: non-breaking space nepovolí break přes interval, pokud by to porušilo token.
- [x] Implementovat přechod mezi intervaly bez změny textového obsahu.

### 3.4 Caret stops v multi-range řádku

- [x] RED test: caret stop před textem v levém intervalu má správný rect.
- [x] RED test: caret stop na začátku pravého intervalu má správný rect.
- [x] RED test: klik do prázdné části levého intervalu mapuje na nejbližší offset.
- [x] RED test: klik do prázdné části pravého intervalu mapuje na nejbližší offset.
- [x] RED test: caret se nikdy neumístí dovnitř blokovaného image rectu.
- [x] Implementovat range-aware caret hit testing.

### 3.5 Justify a alignment

- [x] RED test: left align funguje v každém range samostatně.
- [x] RED test: right align v multi-range řádku neodsune text přes objekt.
- [x] RED test: center align v multi-range řádku používá aktivní textový range, ne celý body frame.
- [x] RED test: justify nepřidává mezery přes zakázaný interval.
- [x] Implementovat alignment po range.

## Fáze 4: Integrace do document layoutu

### 4.1 Přepočítat paragraph layout s exkluzemi před sázením textu

- [x] RED test: `layoutParagraphInScopedFrame` předá line breakeru intervaly pro aktuální Y.
- [x] RED test: `layoutParagraphAcrossPages` předá line breakeru intervaly pro aktuální page.
- [x] RED test: line breaker nesmí nejdřív vysázet celý řádek a teprve pak ho zúžit.
- [x] Implementovat iterativní layout: Y -> exclusions -> ranges -> break line -> další Y.

### 4.2 Odstranit post-layout posun jen do prvního intervalu

- [x] RED test: současný vzor `available.intervals[0]` je zakázaný pro multi-range text.
- [x] Upravit `layoutParagraphInScopedFrame`, aby nepřesouval hotový řádek pouze do prvního intervalu.
- [x] Upravit `layoutParagraphAcrossPages`, aby nepřesouval hotový řádek pouze do prvního intervalu.
- [x] Přidat test, že druhý interval obsahuje skutečný textový segment.

### 4.3 Stabilní layout recty

- [x] RED test: paragraph rect zahrnuje všechny segmenty na všech range.
- [x] RED test: line rect height odpovídá nejvyššímu segmentu nebo inline objektu.
- [x] RED test: line rect width není použita k zakrytí image rectu.
- [x] Implementovat `line.visualRect`, `line.textRanges`, `line.availableIntervals`.

### 4.4 Invalidace dotčených odstavců

- [x] RED test: změna object rect invaliduje anchor odstavec.
- [x] RED test: změna object rect invaliduje následující obtékané odstavce přes vertikální zásah objektu.
- [x] RED test: změna object rect v headeru neinvaliduje body.
- [x] RED test: změna object rect v tabulkové buňce neinvaliduje jiné buňky, pokud se geometrie nepřekrývá.
- [x] Implementovat scope-aware invalidation.

## Fáze 5: Object layer jako zdroj vizuální polohy

### 5.1 Oddělit visual object od text reservation anchoru

- [x] RED test: anchored drawing se renderuje v object layeru podle `object.rect`.
- [x] RED test: anchor span má nulový nebo minimální layout footprint pro floating objekty.
- [x] RED test: anchor span nesmí mít `float:left`.
- [x] RED test: anchor span nesmí mít `float:right`.
- [x] RED test: anchor span nesmí mít `shape-outside`.
- [x] Implementovat object layer rendering jako jediný vizuální obraz floating objektu.

### 5.2 Přepsat `renderDrawingAnchorReservationStyle`

- [x] RED test: pro `Square`, `Tight`, `Through` vrací anchor style bez float/shape-outside.
- [x] RED test: pro `TopBottom` anchor také nerozhoduje o flow, rozhoduje layout engine.
- [x] RED test: pro `Inline` anchor stále rezervuje inline objekt.
- [x] Implementovat nový anchor style.
- [x] Odstranit center full-band branch.

### 5.3 Přepsat `renderDrawingFigureStyle`

- [x] RED test: floating figure style používá `position:absolute` nebo object layer transform.
- [x] RED test: floating figure style nepoužívá `float`.
- [x] RED test: object rect odpovídá layout snapshotu.
- [x] Implementovat style z layout object rectu.

### 5.4 CSS cleanup

- [x] Najít CSS pravidla `.tm-wysiwyg-image--float-left`.
- [x] Najít CSS pravidla `.tm-wysiwyg-image--float-right`.
- [x] Přepsat je tak, aby neřídila layout textu.
- [x] Odstranit nebo zneškodnit staré float utility pro WYSIWYG floating drawing.
- [x] Přidat regression test, že computed style floating objektu nemá `float: left/right`.

## Fáze 6: Square center a libovolný drag

### 6.1 Unit testy pro center Square

- [x] RED test: body frame `[0, 600]`, image `[250, 350]` vrátí dostupné intervaly `[0, 250]` a `[350, 600]`.
- [x] RED test: distances posunou intervaly na `[0, left-distance]` a `[right+distance, 600]`.
- [x] RED test: pokud levý interval je malý, text pokračuje v pravém.
- [x] RED test: pokud oba intervaly jsou malé, řádek jde pod objekt.
- [x] GREEN implementace bez CSS.

### 6.2 E2E pro center Square po načtení

- [x] Otevřít demo.
- [x] Najít center Square image.
- [x] Získat image rect.
- [x] Získat text recty na stejných Y řádcích.
- [x] Ověřit, že existuje text vlevo od image.
- [x] Ověřit, že existuje text vpravo od image.
- [x] Ověřit, že text recty nepřekrývají image rect.
- [x] Ověřit, že text pokračuje pod image až po vyčerpání bočních intervalů.

### 6.3 E2E pro drag na libovolné X

- [x] Vybrat Square obrázek vlevo.
- [x] Přetáhnout ho do středu řádku.
- [x] Během preview ověřit, že textové intervaly se mění.
- [x] Po dropu ověřit image rect ve středu.
- [x] Po dropu ověřit text vlevo i vpravo.
- [x] Stisknout undo.
- [x] Ověřit návrat původní pozice i původního obtékání.
- [x] Stisknout redo.
- [x] Ověřit návrat středového obtékání.

## Fáze 7: TopBottom jako samostatný režim

### 7.1 Unit testy TopBottom

- [x] RED test: TopBottom blokuje celý body frame pouze ve vertikálním pásu objektu.
- [x] RED test: TopBottom s objektem mimo horizontální frame neblokuje text.
- [x] RED test: distance top/bottom ovlivní výšku blokovaného pásu.
- [x] RED test: distance left/right nezužují full-width pás.
- [x] Implementovat explicitní TopBottom branch v interval manageru.

### 7.2 E2E TopBottom

- [x] Vybrat obrázek.
- [x] Přepnout Wrap -> Break/TopBottom.
- [x] Ověřit, že řádek vedle obrázku zmizí a pokračuje pod ním.
- [x] Přepnout zpět na Square.
- [x] Ověřit, že text se vrátí vedle objektu podle jeho X.
- [x] Ověřit, že změna wrap mode je jeden undo krok.

## Fáze 8: Tight a Through polygon

### 8.1 Polygonová geometrie

- [x] RED test: rectangle polygon dává stejný interval jako Square bez distances.
- [x] RED test: trojúhelníkový polygon vrací užší interval nahoře než dole.
- [x] RED test: polygon průsečík používá top/mid/bottom sampling řádku.
- [x] RED test: polygon point uvnitř řádku se zahrne jako sample.
- [x] RED test: polygon mimo řádek neblokuje interval.
- [x] Implementovat polygon interval výpočet ve vlastním kódu.

### 8.2 Tight

- [x] RED test: Tight používá `wrapContourPoints`.
- [x] RED test: Tight bez polygonu spadne na rectangular contour.
- [x] RED test: Tight distance left/right rozšiřuje výsledný interval.
- [x] RED test: Tight zachová wrap side.
- [x] Implementovat Tight v `TextExclusionManager`.

### 8.3 Through

- [x] RED test: Through používá polygon stejně jako Tight.
- [x] RED test: Through má samostatný `kind` pro budoucí editaci polygonu.
- [x] RED test: změna polygon pointu invaliduje layout.
- [x] Implementovat Through bez vizuální editace bodů.

### 8.4 E2E polygon smoke

- [x] Přidat demo polygonový obrázek s jednoduchým lichoběžníkem.
- [x] Ověřit, že textové recty sledují polygon přibližně podle Y.
- [x] Ověřit, že žádný text rect nezasahuje do polygonového blocked intervalu.

Poznámka 2026-05-26: Fáze 8 dokončena. `Tight` a `Through` používají polygonové intervaly s top/mid/bottom samplingem řádkového pásu a se samplingem vrcholů uvnitř pásu; obdélníkový fallback odpovídá Square bez distances, distances rozšiřují konturu přes wrap rect, `wrapSide` se aplikuje na výsledné blokované intervaly a cache key reaguje na změnu polygon pointu. Přidán izolovaný E2E smoke s lichoběžníkovou Tight konturou, který kontroluje skutečné line/text recty proti polygonovému blocked intervalu.

## Fáze 9: BehindText a InFrontOfText

### 9.1 Layout pravidla

- [x] RED test: BehindText nevytváří text exclusion.
- [x] RED test: InFrontOfText nevytváří text exclusion.
- [x] RED test: BehindText je v object layeru pod textem.
- [x] RED test: InFrontOfText je v object layeru nad textem.
- [x] RED test: hit testing preferuje InFrontOfText před textem při kliknutí na obrázek.
- [x] RED test: hit testing umožní textový caret přes BehindText.
- [x] Implementovat layer priority.

### 9.2 E2E vrstva před/za textem

- [x] Přepnout obrázek na BehindText.
- [x] Kliknout do textu nad obrázkem.
- [x] Ověřit, že caret jde do textu, ne na obrázek.
- [x] Přepnout obrázek na InFrontOfText.
- [x] Kliknout na obrázek.
- [x] Ověřit object selection.

Poznámka 2026-05-26: Fáze 9 dokončena. Render WYSIWYG stránky má samostatné paint vrstvy `behind-text`, `object` a `in-front-of-text`; `BehindText` se renderuje před textovou vrstvou a nemá pointer hit, `InFrontOfText` se renderuje nad běžnými objekty a vyhrává object hit-test. Synchronizace absolutních rectů nyní prochází všechny paint vrstvy, overlay selection/guides zůstává nad nimi. Klik přes vybraný `BehindText` overlay se přesměruje do textového hit-testu, zatímco handle/bublina zůstávají objektové. Doplněn geometrický návrat `BlockId` pro caret hit-test bez explicitních intervalů a přidán E2E scénář přepnutí `BehindText`/`InFrontOfText`.

## Fáze 10: Caret, psaní a prázdné obtékané prostory

### 10.1 Kliknutí vedle obrázku

- [x] RED E2E: klik do levého intervalu u center Square nastaví caret do textu vlevo.
- [x] RED E2E: klik do pravého intervalu u center Square nastaví caret do textu vpravo.
- [x] RED E2E: klik do blocked rect vybere obrázek, pokud je klik na objekt.
- [x] RED E2E: klik do blocked rect mimo viditelný objekt neudělá textový caret uvnitř objektu.
- [x] Implementovat range-aware hit testing.

### 10.2 Psaní vedle obrázku v prázdném odstavci

- [x] RED E2E: vložit Square floating obrázek do prázdného odstavce.
- [x] Kliknout vpravo od obrázku.
- [x] Psát text.
- [x] Ověřit, že text vzniká v pravém intervalu, ne pod obrázkem.
- [x] Kliknout vlevo od středového obrázku.
- [x] Psát text.
- [x] Ověřit, že text vzniká v levém intervalu.
- [x] Implementovat synthetic insertion affinity pro empty range.

### 10.3 Arrow navigace

- [x] RED E2E: ArrowDown z textu nad obrázkem zůstává v textovém caret modelu.
- [x] RED E2E: ArrowUp z textu pod obrázkem zůstává v textovém caret modelu.
- [x] RED E2E: ArrowLeft/Right přes anchor offset nevybere objekt automaticky.
- [x] RED E2E: explicitní shortcut pro object navigation může vybrat objekt.
- [x] Implementovat oddělení text selection a object selection.

### 10.4 Escape a object selection

- [x] RED E2E: klik na obrázek vybere objekt.
- [x] RED E2E: Escape vrátí caret k anchor offsetu.
- [x] RED E2E: další psaní pokračuje v textu, ne v object toolbaru.
- [x] Implementovat návrat caret podle anchor block/offset/affinity.

Poznámka 2026-05-26: Fáze 10 dokončena. `DocumentHitTestService`/runtime hit-test nyní potlačuje fallback textový caret uvnitř text-exclusion rectu, takže viditelný objekt se vybírá jen přes visual rect a neviditelná wrap mezera nevyrábí caret uvnitř blokovaného pásu. Doplněny E2E scénáře pro center `Square` levý/pravý interval, object/gap hit-test, psaní do prázdného odstavce přes virtual caret affinity, návrat z object selection přes `Escape` a oddělení textové šipkové navigace od explicitní objektové navigace.

## Fáze 11: Drag/resize a reflow

### 11.1 Drag preview bez trvalého zápisu

- [x] RED unit test: pointermove mění preview rect.
- [x] RED unit test: pointermove nevytváří update operation.
- [x] RED unit test: preview rect generuje preview exclusions.
- [x] RED E2E: během drag se text vizuálně reflowuje kolem preview pozice.
- [x] Implementovat preview exclusion overlay.

### 11.2 Drag commit

- [x] RED unit test: pointerup vytvoří jednu `UpdateImageLayout` operaci.
- [x] RED unit test: operace obsahuje nový rect/position, anchor a affected paragraph ids.
- [x] RED unit test: undo vrátí rect i wrap layout.
- [x] RED unit test: redo obnoví rect i wrap layout.
- [x] Implementovat commit.

### 11.3 Resize preview a commit

- [x] RED unit test: resize preview mění object rect a wrap rect.
- [x] RED E2E: text se během resize preview odsouvá podle nové velikosti.
- [x] RED unit test: resize commit je jedna undo operace.
- [x] RED unit test: aspect ratio lock funguje.
- [x] RED unit test: min size nezpůsobí nulový wrap interval.
- [x] Implementovat resize přes stejný preview/commit model.

### 11.4 Anchor přepočet při dropu

- [x] RED test: drop u jiného odstavce přepočítá anchor block.
- [x] RED test: drop u jiného offsetu přepočítá anchor offset.
- [x] RED test: fixedOnPage objekt nepřepočítává anchor podle textu.
- [x] RED test: objekt v headeru se nepřesune do body anchoru.
- [x] Implementovat nearest paragraph/offset resolver.

Poznámka 2026-05-26: Fáze 11 dokončena. Drag/resize preview nově publikuje `previewRect`, `previewWrapRect` a `previewExclusion`, kreslí samostatný wrap-exclusion overlay a zůstává bez zápisu do modelu i bez operace. Drag i resize commit používají jednotnou `UpdateImageLayout` operaci s old/new layoutem, anchorem a affected paragraph ids; undo/redo vrací rect i wrap layout. Reanchor při dropu respektuje nearest paragraph/offset, `fixedOnPage` a ochranu header/footer scope. E2E pokrytí navazuje na existující strict scénáře pro drag/resize live feedback, preview intervaly a reflow po commitu.

## Fáze 12: Header, footer, tabulky

### 12.1 Header/footer scope

- [x] RED unit test: header object vytváří exclusions jen v header scope.
- [x] RED unit test: footer object vytváří exclusions jen ve footer scope.
- [x] RED unit test: body text neobtéká header object, pokud objekt není v body scope.
- [x] RED E2E: psaní v headeru kolem obrázku je plynulé a bez překryvů.
- [x] Implementovat scope key do manageru.

### 12.2 Tabulkové buňky

- [x] RED unit test: image v buňce obtéká jen text ve stejné buňce, pokud `layoutInCell = true`.
- [x] RED unit test: image v buňce s `layoutInCell = false` může obtékat proti stránce.
- [x] RED E2E: vložit obrázek do buňky a psát vedle něj.
- [x] RED E2E: resize obrázku v buňce nepřekryje text jiné buňky.
- [x] Implementovat cell scope.

### 12.3 Vícesloupcový layout připravenost

- [x] RED unit test: exclusion v jednom column frame neovlivní jiný column frame.
- [x] Připravit `columnIndex` ve scope, i když UI zatím vícesloupcový layout plně nepoužívá.
- [x] Ověřit, že cache key zahrnuje frame rect.

Poznámka 2026-05-26: Fáze 12 dokončena. Wrap exclusion manager má scope descriptor/key pro body, header, footer, table cell a připravený `columnIndex`; interval cache zahrnuje scope i frame rect. Header/footer objekty už nevytvářejí body exclusions, tabulková buňka filtruje vlastní lokální exclusions a `layoutInCell = false` promuje table-cell objekt do body/page scope. DOM selection, hit-test, drop target a image update payloady nesou table/cell/column metadata, takže drag/resize/reanchor nepřepíná region omylem. Ověřeno unit balíkem `DocumentEditorImageWrapPhase12ScopedRegionsJavaScriptTests` a navazujícím E2E strict region-scope testem pro header, footer a table-cell image scope.

## Fáze 13: DOCX/DrawingML parita

### 13.1 Import wrap side a distances

- [x] RED DOCX test: `wp:wrapSquare wrapText="bothSides"` importuje `BothSides`.
- [x] RED DOCX test: `wrapText="left"` importuje `Left`.
- [x] RED DOCX test: `wrapText="right"` importuje `Right`.
- [x] RED DOCX test: `wrapText="largest"` importuje `Largest`.
- [x] RED DOCX test: `distL/distR/distT/distB` se importují do layout distances.
- [x] Implementovat nebo opravit importer.

### 13.2 Export wrap side a distances

- [x] RED DOCX test: `Square` exportuje `wp:wrapSquare`.
- [x] RED DOCX test: distances se exportují do správných atributů.
- [x] RED DOCX test: `Tight` exportuje `wp:wrapTight` s polygonem.
- [x] RED DOCX test: `Through` exportuje `wp:wrapThrough` s polygonem.
- [x] RED DOCX test: `TopBottom` exportuje `wp:wrapTopAndBottom`.
- [x] Implementovat nebo opravit exporter.

### 13.3 Roundtrip

- [x] RED roundtrip test: center Square anchor přežije import/export/import.
- [x] RED roundtrip test: drag změněná absolute/relative position přežije roundtrip.
- [x] RED roundtrip test: Tight polygon přežije roundtrip.
- [x] RED roundtrip test: BehindText/InFrontOfText layer přežije roundtrip.
- [x] Implementovat chybějící mappingy.

Poznámka 2026-05-26: Fáze 13 dokončena. Doplněny explicitní DOCX/DrawingML testy pro nativní `wrapText` hodnoty `bothSides`, `left`, `right`, `largest`, export `wrapSquare`, `wrapTight`, `wrapThrough`, `wrapTopAndBottom`, roundtrip center Square anchoru, absolutní drag pozice, tight polygonu a vrstev `BehindText`/`InFrontOfText`. Opraven byl konkrétní výpadek parity: `wp:wrapTopAndBottom` nyní importuje i exportuje nativní `distT/distB`; export navíc zapisuje `tm:wrap-side` jako fallback metadata pro interní roundtripy.

## Fáze 14: Demo dokumenty

### 14.1 Přepsat demo seed

- [x] Upravit `DemoDocumentEditorStore.cs`.
- [x] Demo nesmí používat top-level image block pro cílové wrap scénáře.
- [x] Demo musí mít Square left.
- [x] Demo musí mít Square right.
- [x] Demo musí mít Square center.
- [x] Demo musí mít Square arbitrary offset po drag-like pozici.
- [x] Demo musí mít TopBottom.
- [x] Demo musí mít Tight polygon.
- [x] Demo musí mít BehindText nebo InFrontOfText.
- [x] Demo musí mít obrázek v headeru.
- [x] Demo musí mít obrázek ve footeru.
- [x] Demo musí mít obrázek v tabulkové buňce.

### 14.2 Demo texty

- [x] Každý wrap scénář musí mít dost dlouhý text na více řádků.
- [x] Center Square text musí být dost dlouhý pro levý i pravý interval.
- [x] Text nesmí být náhodná data, aby E2E mohlo hledat stabilní substringy.
- [x] Popisky obrázků nesmí způsobit skryté navýšení wrap rectu bez testu.
- [x] Přidat explicitní test id nebo stabilní object id pro každý obrázek.

### 14.3 Demo reset a save

- [x] E2E: reset demo vrátí všechny obrázky na správná místa.
- [x] E2E: změnit wrap mode, uložit, reload, ověřit stejný layout.
- [x] E2E: změnit drag pozici, uložit, reload, ověřit stejný layout.
- [x] E2E: změnit resize, uložit, reload, ověřit stejný layout.

Poznámka k implementaci 2026-05-26: contract demo seed teď obsahuje stabilní drawing-run scénáře `contract-left-wrap-image`, `contract-right-wrap-image`, `contract-center-wrap-image`, `contract-offset-wrap-image`, `contract-top-bottom-image`, `contract-tight-wrap-image`, `contract-in-front-image`, `contract-header-logo-image`, `contract-footer-logo-image` a `contract-table-cell-image`. Cílené API/E2E testy ověřují absenci top-level image blocků pro cílové scénáře, kanonický reset a save/reload pro wrap mode, drag pozici i resize.

## Fáze 15: Přepsání existujících testů

### 15.1 Testy, které drží CSS float hack

- [x] Najít všechny testy očekávající `float:left`.
- [x] Najít všechny testy očekávající `float:right`.
- [x] Najít všechny testy očekávající `shape-outside`.
- [x] Najít všechny testy očekávající full-band center Square.
- [x] Přepsat je na intervalové layout snapshoty.
- [x] Pokud test pokrývá pouze static HTML fallback, přesunout jej mimo WYSIWYG floating behavior.

### 15.2 E2E testy, které neověřují skutečné chování

- [x] Najít image E2E testy, které používají interní JS command místo myši/klávesnice.
- [x] Zachovat je jen jako diagnostické unit/integration testy.
- [x] Přidat human-like E2E náhradu pro hlavní uživatelské chování.
- [x] Ověřit Playwright trace při failure.

### 15.3 Snapshot testy

- [x] Přidat canonical layout snapshot pro left Square.
- [x] Přidat canonical layout snapshot pro right Square.
- [x] Přidat canonical layout snapshot pro center Square.
- [x] Přidat canonical layout snapshot pro TopBottom.
- [x] Přidat canonical layout snapshot pro Tight.
- [x] Přidat canonical layout snapshot pro BehindText.
- [x] Snapshoty musí obsahovat numeric tolerance, ne křehké přesné pixely tam, kde závisí na font renderingu.

Poznámka 2026-05-26: Fáze 15 je dokončená. Přidaný `DocumentEditorImageWrapPhase15TestRewriteJavaScriptTests` staticky hlídá, že DocumentEditor testy nevyžadují staré browser-flow fallbacky (`float:*`, `shape-outside`, full-band center Square), a zároveň zavádí canonical intervalové snapshoty pro left/right/center Square, TopBottom, Tight a BehindText s explicitní tolerancí. Image parity E2E jsou ověřené jako human-like tok přes myš/klávesnici; interní `executeCommand` image scénáře zůstávají v diagnostickém runtime E2E souboru. `PlaywrightTestBase` ukládá trace.zip jako test artefakt při neúspěchu.

## Fáze 16: Výkon

### 16.1 Cache intervalů

- [x] RED benchmark/test: 100 řádků a 10 floating objektů nevolá polygon výpočet pro každý token.
- [x] Cache key zahrnuje line y, line height, frame, object ids, rects, wrap modes, distances, polygon version.
- [x] Cache se invaliduje při drag/resize/wrap mode/polygon change.
- [x] Cache se neinvaliduje při psaní mimo vertikální rozsah objektu.

### 16.2 Reflow rozsah

- [x] RED test: psaní v odstavci bez floating objektů nepočítá všechny page exclusions znovu.
- [x] RED test: změna obrázku invaliduje jen affected scope.
- [x] RED test: header editace neinvaliduje body layout.
- [x] RED test: footer editace neinvaliduje body layout.
- [x] Implementovat incremental reflow boundary.

### 16.3 E2E performance smoke

- [x] E2E měření: psaní 30 znaků vedle obrázku nemá viditelné zpoždění.
- [x] E2E měření: resize preview drží použitelný frame rate.
- [x] E2E měření: undo po drag/resize netrvá nepřiměřeně dlouho.
- [x] Uložit performance budget do testu nebo diagnostického výstupu.

Poznámka 2026-05-26: Fáze 16 je dokončená. `getAvailableIntervals` má diagnostikovanou cache nad podpisem řádku, rámu, scope a geometrie objektů včetně `polygonVersion`, takže opakované tokenové dotazy na stejný řádek nepočítají znovu polygonové intervaly. `layoutAfterOperation` dostal konzervativní incremental reflow boundary pro psaní/formátování v běžném body odstavci mimo floating objekty; header/footer a image změny zůstávají scope-bound. Přidány JS performance testy fáze 16 a image E2E smoke s budgety pro psaní vedle obtékaného obrázku, resize preview/commit a undo po resize.

## Fáze 17: Accessibility a fokus

### 17.1 Object selection accessibility

- [x] Obrázek má `role="img"` a alt text.
- [x] Vybraný obrázek má čitelný popis selection state.
- [x] Resize handles jsou dostupné jen při object selection.
- [x] Text caret navigace nepřeskakuje do resize handles.
- [x] Escape z object selection vrací fokus/caret do dokumentu.

### 17.2 Keyboard model

- [x] Tab v dokumentu neprochází každý floating obrázek jako běžný formulářový prvek.
- [x] Explicitní klávesová akce pro výběr objektu je zdokumentovaná v testu, ne nutně viditelným UI textem.
- [x] Arrow keys v textu zůstávají textové.
- [x] Delete/Backspace na object selection smaže objekt jednou undo operací.
- [x] Delete/Backspace v textu vedle objektu nemaže objekt omylem.

Poznámka 2026-05-26: Fáze 17 je dokončená. Přidaný `DocumentEditorImageWrapPhase17AccessibilityFocusJavaScriptTests` kryje selection-only focus model pro floating obrázky, `role="img"`/alt label, `aria-describedby` na stav vybraného objektu, dostupnost resize handles jen při object selection, explicitní `Alt+Shift+O`/`Ctrl+Alt+O` navigaci, Tab bez průchodu obrázky, Escape návrat do textu a Delete/Backspace rozdíl mezi object selection a textem. Opraveno mazání objektu přes `RestoreSnapshot`: operace nyní ukládá i klon `previousSnapshot`/`previousSelection`, takže Delete vybraného obrázku je jedna undo operace a undo objekt skutečně vrátí. Legacy top-level image placeholder ve WYSIWYG text layer má také nefocusovatelný `role="img"`/`aria-label`, aby nezmizel z accessibility stromu do doby, než ho cleanup fáze oddělí od wrap flow.

## Fáze 18: Cleanup starého řešení

### 18.1 Odstranit CSS flow reservation hack

- [x] Odstranit branch pro center full-band reservation.
- [x] Odstranit shape-outside z WYSIWYG floating path.
- [x] Odstranit float left/right z WYSIWYG floating path.
- [x] Zachovat inline image flow pro skutečné inline obrázky.
- [x] Přidat grep test nebo unit assertion, že floating anchor HTML neobsahuje `float:`.

### 18.2 Odstranit staré image block závislosti z wrap scénářů

- [x] Najít demo top-level image blocky používané jen pro wrap.
- [x] Přepsat je na drawing runs.
- [x] Odstranit test očekávající top-level image block jako cílový stav.
- [x] Pokud static renderer potřebuje image block pro jiný komponent, oddělit ho od document editor WYSIWYG wrap flow.

### 18.3 Dokumentace interních pravidel

- [x] Přidat krátkou technickou poznámku do planning nebo docs.
- [x] Popsat rozdíl `Inline` vs `Anchored/Floating`.
- [x] Popsat rozdíl `Square` vs `TopBottom`.
- [x] Popsat, že text layout je intervalový a CSS float není zdroj pravdy.

Poznámka 2026-05-26: Fáze 18 je dokončená. Přidaný `DocumentEditorImageWrapPhase18CleanupJavaScriptTests` kryje, že WYSIWYG text anchor pro floating obrázek nemá `data-flow-reservation`, `float:`, `shape-outside`, `display:block` ani `clear:both`, zatímco skutečný `Inline` drawing run si nechává reálnou inline šířku a výšku. Z CSS zmizely staré `.tm-wysiwyg-image--float-left/right` utility a nepoužívaný `.tm-wysiwyg-drawing-anchor--flow`; bundle byl přegenerovaný. Demo seed helpery v API i SharedUI se jmenují `CreateImageDrawingParagraph` a už nespoléhají na průběžné `ConvertImageBlocksToDrawingRuns`. Interní pravidla jsou popsaná v `planning/tmdocumenteditor-image-wrap-layout-internal-rules-2026-05-26.md`.

## Fáze 19: Finální E2E regresní sada

### 19.1 Scénáře po načtení

- [x] Demo load: no text-image overlap.
- [x] Demo load: left Square text right side.
- [x] Demo load: right Square text left side.
- [x] Demo load: center Square text both sides.
- [x] Demo load: TopBottom text below.
- [x] Demo load: Tight text follows polygon.
- [x] Demo load: BehindText does not reserve space.
- [x] Demo load: InFrontOfText does not reserve space.

### 19.2 Scénáře editace

- [x] Psát před obrázkem.
- [x] Psát za obrázkem.
- [x] Psát vlevo od center obrázku.
- [x] Psát vpravo od center obrázku.
- [x] Mazat text vedle obrázku.
- [x] Undo/redo text edit vedle obrázku.
- [x] Změnit wrap mode a undo/redo.
- [x] Drag a undo/redo.
- [x] Resize a undo/redo.

### 19.3 Scénáře scope

- [x] Body image.
- [x] Header image.
- [x] Footer image.
- [x] Table cell image.
- [x] Více obrázků v jednom odstavci.
- [x] Obrázek přes dvě řádkové výšky.

Poznámka 2026-05-26: Fáze 19 je doplněná jako finální E2E sada v `DocumentEditorStrictEnginePhase19E2ETests`. Testy kontrolují načtení demo dokumentu bez překryvů, konkrétní line intervaly pro left/right/center Square, TopBottom bez bočních intervalů, Tight s reálnými intervaly, BehindText/InFrontOfText bez legacy flow reservation a scope body/header/footer/table-cell. Demo seed nově obsahuje i `contract-behind-text-image`. Editační E2E scénář píše a maže text vedle centered Square obrázku a ověřuje undo/redo. Samostatný sandbox scénář drží více drawing runů v jednom odstavci včetně obrázku vyššího než dvě řádkové výšky. Wrap/drag/resize scénář kontroluje transakční undo/redo přes reálný JS engine.

Ověření 2026-05-26: API části fáze 19 procházejí. Browser E2E sada je záměrně ostrá a aktuálně je RED: po rebuildu WASM demo aplikace padá `DocumentEditor_Strict_Engine_DefaultDemoReloadIsReadableAndOverlapFree` na reálné překryvy text/image a text/caption v demo dokumentu. To není oslabené v testu; je to regresní brána pro fázi 20.

## Fáze 20: Definition of Done

- [ ] Žádný WYSIWYG floating/anchored image wrap není řízený CSS floatem.
- [ ] Center Square obrázek se chová jako Word/OnlyOffice: text může být vlevo i vpravo.
- [ ] Libovolně posunutý obrázek obtéká podle skutečné geometrie.
- [ ] TopBottom zůstává samostatný režim.
- [ ] Tight/Through mají polygonový základ.
- [ ] Caret a psaní fungují v bočních intervalech.
- [ ] Object selection je explicitní a nepřebírá fokus při běžné textové navigaci.
- [ ] Drag/resize mají preview reflow a jeden undo commit.
- [ ] Demo dokument po načtení nemá překryv textu a obrázku.
- [ ] DOCX import/export zachová wrap mode, distances, wrap side a polygon.
- [ ] Všechny upravené unit testy procházejí.
- [ ] Všechny relevantní E2E testy procházejí.
- [ ] Staré testy nebyly oslabené na pouhé "něco se zobrazilo".
- [ ] Planning dokument `tmdocumenteditor-image-drawing-onlyoffice-level-tdd-todo-2026-05-25.md` je po implementaci aktualizován o odkaz na tento nový wrap engine plán.

## Doporučené ověřovací příkazy

Průběžně po malých fázích:

```bash
dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocumentEditorImageWrap"
```

Po změnách formátů:

```bash
dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/ --filter "FullyQualifiedName~DocxDrawing"
```

Po E2E fázích:

```bash
dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~DocumentEditorImageWrap"
```

Před uzavřením celku:

```bash
dotnet test
```
