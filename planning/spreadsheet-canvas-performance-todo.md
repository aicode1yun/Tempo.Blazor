# Spreadsheet performance a canvas renderer - TODO

Tento soubor je pracovní checklist pro postupné zlepšení `TmSpreadsheet`.
Pri implementaci se maji hotove kroky prubezne odskrtavat.

## Cil

Z komponenty `TmSpreadsheet` udelat vykonnejsi excel-like reseni pro business aplikace:

- aktivni bunka zustava pri navigaci klavesnici viditelna,
- DOM renderer se zrychli bez velkeho prepisu,
- model, selection, prikazy a formule se oddeli od konkretniho rendereru,
- vznikne hybridni canvas renderer pro velke datasety,
- HTML/DOM zustane pro editor bunky, toolbar, context menu, dialogy a pristupnost.

## Faze 1 - Scroll pri navigaci klavesnici

- [x] Najit vsechna mista, kde se meni aktivni bunka v `TmSpreadsheetGrid`.
- [x] Pridat interni stav pro naplanovane zviditelneni bunky po renderu.
- [x] Implementovat vypocet souradnic bunky z indexu radku a sloupce.
- [x] Zohlednit sirku hlavicky radku a vysku hlavicky sloupcu.
- [x] Zohlednit vlastni vysky radku.
- [x] Zohlednit vlastni sirky sloupcu.
- [x] Zohlednit skryte radky.
- [x] Zohlednit skryte sloupce.
- [x] Pridat JS helper pro precteni scroll metrik gridu.
- [x] Pridat JS helper pro nastaveni `scrollTop` a `scrollLeft`.
- [x] Volat ensure-visible po `MoveToCell`.
- [x] Volat ensure-visible po `MoveToLastUsedCell`.
- [x] Volat ensure-visible po `Home` a `End`.
- [x] Volat ensure-visible po `Tab`.
- [x] Overit, ze sipky dolu posouvaji vertikalni scroll.
- [x] Overit, ze sipky doprava posouvaji horizontalni scroll.
- [ ] Overit, ze `Shift+Arrow` posouva viewport spolu s rozsirenim vyberu.
- [ ] Overit, ze scroll funguje s row virtualization.
- [x] Pridat bUnit test pro naplanovani ensure-visible po pohybu aktivni bunky.
- [x] Pridat Playwright/E2E test pro skutecny scroll pri sipkach.

Poznamka: E2E testy pro vertikalni i horizontalni scroll pri sipkach prosly lokalne proti WASM demo aplikaci.

## Faze 2 - Rychle DOM performance opravy

- [x] Zmerit aktualni pocet renderovanych bunek pro velky sheet.
- [ ] Zmerit cas prvniho renderu pro vetsi sheet.
- [ ] Zmerit cas pohybu aktivni bunky sipkami.
- [ ] Zmerit cas oznaceni vetsiho rozsahu.
- [x] Cacheovat nazvy sloupcu (`A`, `B`, `AA`, ...).
- [x] Cacheovat defaultni sirky sloupcu a vlastni sirky do pole pro rychly lookup.
- [x] Cacheovat defaultni vysky radku a vlastni vysky do pole pro rychly lookup.
- [x] Pridat prefix sums pro vysky radku.
- [x] Pridat prefix sums pro sirky sloupcu.
- [x] Invalidovat prefix sums pri zmene vysky radku.
- [x] Invalidovat prefix sums pri zmene sirky sloupce.
- [x] Predpocitat selection bounds jednou za render.
- [x] Nepocitat selection bounds pro kazdou bunku zvlast.
- [x] Pridat rychly lookup merged ranges podle souradnic bunky.
- [x] Neprechazet vsechny merged ranges pro kazdou bunku.
- [x] Omezit tvorbu style stringu pro prazdne defaultni bunky.
- [x] Cacheovat style stringy pro bunky, ktere maji styl.
- [x] Vyhodnotit, jestli lze zachovat `Virtualize` i pri editaci.
- [x] Pridat performance test, ktery hlida, ze velky sheet nerenderuje vsechny radky.
- [x] Pridat test, ktery hlida, ze velky sheet nerenderuje neumerne mnoho bunek.

Poznamka: V teto fazi jsou doplnene regresni testy pro pocty renderovanych radku/bunek a internich cache. Casove benchmarky zustavaji otevrene pro samostatnou benchmark/demo fazi.

## Faze 3 - Horizontalni virtualizace DOM rendereru

- [x] Navrhnout stav horizontalniho viewportu.
- [x] Pridat sledovani `scrollLeft`.
- [x] Spocitat prvni viditelny sloupec podle `scrollLeft`.
- [x] Spocitat posledni viditelny sloupec podle sirky viewportu.
- [x] Pridat overscan sloupce vlevo.
- [x] Pridat overscan sloupce vpravo.
- [x] Renderovat jen viditelne hlavicky sloupcu.
- [x] Renderovat jen viditelne bunky v kazdem viditelnem radku.
- [x] Pridat levou spacer plochu pro virtualizovane sloupce.
- [x] Pridat pravou spacer plochu pro virtualizovane sloupce.
- [x] Zachovat spravnou celkovou scrollovatelnou sirku.
- [ ] Overit resize sloupce ve virtualizovanem viewportu.
- [ ] Overit klik na hlavicku sloupce ve virtualizovanem viewportu.
- [ ] Overit selection pres sloupce mimo aktualni viewport.
- [ ] Overit autofill handle ve virtualizovanem viewportu.
- [ ] Overit context menu ve virtualizovanem viewportu.
- [x] Overit frozen columns s horizontalni virtualizaci.
- [x] Pridat bUnit test, ze se nerenderuji vsechny sloupce.
- [x] Pridat E2E test pro horizontalni scroll a navigaci.

## Faze 4 - Oddeleni spreadsheet modelu od rendereru

- [ ] Sepsat, co dnes patri do modelu a co patri jen do rendereru.
- [x] Vytvorit interni typ pro viewport stav.
- [x] Vytvorit interni typ pro selection stav.
- [x] Vytvorit interni typ pro aktivni bunku.
- [x] Presunout vypocty souradnic bunek mimo Razor markup.
- [x] Presunout hit testing do samostatne sluzby nebo helperu.
- [x] Presunout scroll vypocty do samostatne sluzby nebo helperu.
- [x] Oddelit prikazy od DOM udalosti.
- [x] Oddelit formule od renderovani.
- [x] Zavest rozhrani pro spreadsheet renderer.
- [x] Zavest enum pro render mode.
- [x] Pridat `SpreadsheetRenderMode.Dom`.
- [x] Pridat `SpreadsheetRenderMode.Canvas`.
- [x] Zachovat vychozi DOM renderer kvuli kompatibilite.
- [x] Pridat testy pro vypocet viewportu bez Blazor renderu.
- [x] Pridat testy pro hit testing bez DOM.

Poznamka: DOM renderer zustava kompatibilni. Sdilene helpery (`SpreadsheetGridGeometry`, `SpreadsheetViewportState`, `SpreadsheetSelectionState`) pouziva novy canvas renderer; puvodni DOM renderer muze byt dal postupne zjednodusen.

## Faze 5 - Canvas prototyp

- [x] Pridat JS soubor pro canvas renderer.
- [x] Pridat canvas element do noveho rendereru.
- [x] Pridat overlay vrstvu pro HTML editor bunky.
- [x] Inicializovat canvas podle velikosti komponenty.
- [x] Podporovat `devicePixelRatio`.
- [x] Vykreslit pozadi gridu.
- [x] Vykreslit radkove hlavicky.
- [x] Vykreslit sloupcove hlavicky.
- [x] Vykreslit gridlines.
- [x] Vykreslit text prazdnych a vyplnenych bunek.
- [x] Vykreslit aktivni bunku.
- [x] Vykreslit range selection.
- [x] Vykreslit autofill handle.
- [ ] Vykreslit hover stav.
- [x] Implementovat mouse hit testing nad canvasem.
- [x] Implementovat klik na bunku.
- [x] Implementovat double-click pro editaci bunky.
- [x] Implementovat keyboard navigation nad canvas rendererem.
- [x] Implementovat scroll koleckem mysi.
- [x] Implementovat horizontalni scroll.
- [x] Implementovat sync scroll pozice se stavem komponenty.
- [ ] Overit plynulost scrollu na velkem sheetu.

Poznamka: Canvas renderer ma E2E smoke test na neprázdny canvas a horizontalni scroll klavesnici.

## Faze 6 - Hybridni editace a interakce

- [x] Vykreslit HTML input jako overlay nad aktivni bunkou.
- [x] Spravne pozicovat editor pri scrollu.
- [x] Spravne pozicovat editor pri resize viewportu.
- [x] Podporovat commit editace klavesou `Enter`.
- [x] Podporovat cancel editace klavesou `Escape`.
- [x] Podporovat `Tab` pri editaci.
- [x] Podporovat formula point mode.
- [ ] Podporovat vyber rozsahu mysi.
- [x] Podporovat rozsireni vyberu pres `Shift+Arrow`.
- [x] Podporovat clipboard copy.
- [x] Podporovat clipboard cut.
- [x] Podporovat clipboard paste.
- [x] Podporovat context menu.
- [x] Podporovat resize sloupcu.
- [x] Podporovat resize radku.
- [x] Podporovat skryte radky.
- [x] Podporovat skryte sloupce.
- [x] Podporovat frozen rows.
- [x] Podporovat frozen columns.

Poznamka: Formula point mode podporuje klik na referencovanou bunku; drag range v canvas rendereru zustava jako samostatne rozsireni.

## Faze 7 - Styly, formatovani a specialni obsah

- [x] Vykreslit font family.
- [x] Vykreslit font size.
- [x] Vykreslit bold.
- [x] Vykreslit italic.
- [x] Vykreslit underline.
- [x] Vykreslit strike-through.
- [x] Vykreslit barvu textu.
- [x] Vykreslit barvu pozadi.
- [x] Vykreslit horizontalni zarovnani.
- [x] Vykreslit vertikalni zarovnani.
- [x] Vykreslit number format display value.
- [x] Vykreslit custom borders.
- [ ] Vykreslit text wrap.
- [x] Vykreslit merged cells.
- [x] Vykreslit hyperlinks jako text s vizualnim odliseni.
- [x] Rozhodnout strategii pro klikatelne hyperlinky v canvasu.
- [x] Rozhodnout strategii pro obrazky v canvasu.
- [ ] Pridat cache mereni textu.
- [ ] Pridat cache vykreslovacich stylu.

Poznamka: Hyperlinky jsou zatim vizualne odlisene, ale klik zustava pres DOM renderer. Obrazky se kresli do canvasu pres jednoduchou image cache v JS.

## Faze 8 - Pristupnost a fallback

- [x] Zachovat focusovatelny root element.
- [x] Zachovat smysluplne ARIA informace pro aktivni bunku.
- [x] Pridat live region pro aktivni bunku.
- [x] Pridat fallback text pro screen readery.
- [x] Overit keyboard-only ovladani.
- [ ] Overit high contrast rezim.
- [ ] Overit dark mode.
- [ ] Overit reduced motion.
- [x] Rozhodnout, kdy pouzit DOM renderer misto canvas rendereru.
- [ ] Zdokumentovat accessibility trade-off canvas rezimu.

Poznamka: Vychozi zustava `SpreadsheetRenderMode.Dom`; canvas je explicitni volba pres parametr `RenderMode`.

## Faze 9 - Benchmarky a limity

- [x] Pridat jednoduchou benchmark demo stranku.
- [x] Pridat dataset 1 000 x 50.
- [x] Pridat dataset 10 000 x 100.
- [x] Pridat dataset 100 000 x 100.
- [x] Merit prvni render.
- [x] Merit scroll FPS subjektivne nebo pomoci browser metrik.
- [x] Merit cas pohybu aktivni bunky.
- [x] Merit cas paste vetsi oblasti.
- [x] Merit pametovou narocnost.
- [x] Porovnat DOM renderer a canvas renderer.
- [x] Stanovit doporucene limity pro DOM renderer.
- [x] Stanovit doporucene limity pro canvas renderer.

Poznamky:
- Benchmark stranka je na `/spreadsheet-benchmark` a umi spoustet DOM, canvas nebo oba renderery nad logickymi datasety 1 000 x 50, 10 000 x 100 a 100 000 x 100.
- Datasety jsou zamerne sparse/sample-based, aby benchmark meril renderer nad velkou mrizkou bez alokace milionu bunek v modelu.
- Browser metriky meri stabilizaci po renderu, animovany scroll FPS, synteticky pohyb aktivni bunky, paste simulaci 100 x 20 a `performance.memory`, pokud ji browser podporuje.
- Doporucene limity jsou zobrazene primo na benchmark strance: DOM primarne do cca 10 000 x 100, canvas pro 10 000 x 100 az 100 000 x 100 pri sparse/data-provider modelu.

## Faze 10 - Plynuly canvas scroll a navigace

Cil: dostat canvas renderer bliz k pocitu Google Sheets/Synology Office pri scrollu, drag vyberu a drzeni kurzorovych sipek. Horka cesta interakce musi zustat v JS/canvas vrstve a Blazor/.NET se ma synchronizovat az mimo kriticky frame.

### 10.1 Diagnostika aktualniho skakani

- [x] Overit video se skakanim pri drzeni `ArrowDown`.
- [x] Potvrdit, ze problem nastava hlavne ve chvili, kdy navigace vyvola scroll viewportu.
- [x] Potvrdit, ze JS selection muze byt novejsi nez nasledujici Blazor frame.
- [x] Pridat debug counter pro pocet canvas redrawu.
- [x] Pridat debug counter pro pocet viewport callbacku do .NET.
- [x] Pridat debug counter pro pocet selection callbacku do .NET.
- [x] Pridat debug hodnotu posledniho zdroje redrawu (`scroll`, `keyboard`, `pointer`, `frame`).
- [x] Pridat debug hodnotu delky posledniho canvas draw v ms.
- [x] Pridat debug hodnotu delky posledniho text draw v ms.
- [x] Pridat debug hodnotu poctu vykreslenych bunek v poslednim frame.
- [x] Pridat debug hodnotu poctu vykreslenych textu v poslednim frame.
- [x] Pridat docasny E2E benchmark/regresni test pro drzeni `ArrowDown` pres hranu viewportu.
- [ ] Pridat docasny benchmark pro mouse wheel scroll.
- [ ] Pridat docasny benchmark pro drag selection pres hranu viewportu.

### 10.2 Jedna autoritativni pravda pro canvas interakci

- [x] Pri viewport syncu posilat do .NET i aktualni canvas selection.
- [x] V .NET syncnout selection pred vytvorenim dalsiho canvas frame.
- [x] Zamezit paralelnim viewport callbackum se starsim stavem.
- [x] Definovat, ze pri aktivni canvas keyboard navigaci je JS autoritativni pro active cell.
- [x] Definovat, ze pri aktivnim canvas scrollu je JS autoritativni pro scroll offset.
- [x] Definovat, kdy smi Blazor poslat novy frame, ktery meni active cell.
- [x] Pridat monotonic `interactionVersion` do JS stavu.
- [x] Posilat `interactionVersion` do .NET pri selection callbacku.
- [x] Posilat `interactionVersion` do .NET pri viewport callbacku.
- [x] Vracet `interactionVersion` z .NET v dalsim canvas frame.
- [x] Ignorovat canvas frame, ktery je starsi nez posledni lokalni JS interakce.
- [x] Pridat test, ze starsi frame nevrati active cell na vyssi radek.
- [ ] Pridat test, ze starsi frame nevrati scrollTop zpet.

### 10.3 Keyboard hot path bez Blazor renderu

- [x] Zachytavat sipky v canvas rootu na JS strane.
- [x] Lokalne posouvat active cell pri `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`.
- [x] Lokalne zajistit viditelnost active cell pri navigaci.
- [x] Pri key repeat nevykonavat .NET callback pro kazdy jednotlivy keydown.
- [x] Pri key repeat syncovat selection do .NET nejvyse jednou za animation frame.
- [x] Pri key repeat syncovat viewport do .NET nejvyse jednou za animation frame.
- [x] Pri key repeat kreslit selection overlay pred plnym content redrawem.
- [x] Pri key repeat pres scroll edge kreslit nejdrive novou selection a viewport, potom data.
- [x] Pri drzeni `ArrowDown` zachovat monotonni rust radku i pri pomalem .NET callbacku.
- [x] Pri drzeni `ArrowUp` zachovat monotonni pokles radku i pri pomalem .NET callbacku.
- [x] Pri drzeni `ArrowRight` zachovat monotonni rust sloupce i pri pomalem .NET callbacku.
- [x] Pri drzeni `ArrowLeft` zachovat monotonni pokles sloupce i pri pomalem .NET callbacku.
- [x] Pridat E2E test pro `ArrowUp` pres horni hranu viewportu.
- [x] Pridat E2E test pro `ArrowRight` pres pravou hranu viewportu.
- [x] Pridat E2E test pro `ArrowLeft` pres levou hranu viewportu.

### 10.4 Rozdeleni canvas vrstev

- [x] Rozhodnout minimalni sadu vrstev canvas rendereru.
- [x] Vytvorit samostatny canvas pro content bunek.
- [x] Ponechat grid lines v content canvasu, protoze samostatna vrstva zatim nema meritelny prinos.
- [x] Vytvorit samostatny canvas pro headers.
- [x] Vytvorit samostatny canvas pro selection overlay.
- [x] Presunout kresleni active cell do selection overlay vrstvy.
- [x] Presunout kresleni range selection do selection overlay vrstvy.
- [x] Presunout kresleni autofill handle do selection overlay vrstvy.
- [x] Pri samotne zmene active cell prekreslit jen selection overlay.
- [x] Pri samotnem hoveru prekreslit jen hover/selection overlay.
- [x] Pri editaci bunky neprekreslovat content canvas, pokud se nemeni hodnota.
- [x] Pri commitu editace prekreslit jen dotcenou oblast nebo viditelny content.
- [x] Overit, ze vrstvy sedi pri `devicePixelRatio > 1`.
- [x] Overit, ze vrstvy sedi po resize viewportu.
- [x] Overit, ze vrstvy sedi po horizontalnim scrollu.
- [x] Overit, ze vrstvy sedi po vertikalnim scrollu.

### 10.5 Rychly scroll pres bitmap shift

- [ ] Zmerit, kolik casu bere full redraw pri scrollu o jeden radek.
- [ ] Zmerit, kolik casu bere full redraw pri scrollu o jeden sloupec.
- [x] Detekovat maly vertikalni scroll delta v pixelech.
- [x] Detekovat maly horizontalni scroll delta v pixelech.
- [x] Pri malem vertikalnim scrollu posunout existujici bitmapu content canvasu.
- [x] Pri malem vertikalnim scrollu dokreslit jen nove odkryty horni/dolni pas.
- [x] Pri malem horizontalnim scrollu posunout existujici bitmapu content canvasu.
- [x] Pri malem horizontalnim scrollu dokreslit jen nove odkryty levy/pravy pas.
- [x] Pri velkem scroll skoku spadnout na full redraw.
- [x] Pri zmene zoomu spadnout na full redraw.
- [x] Pri zmene sirky sloupce spadnout na full redraw.
- [x] Pri zmene vysky radku spadnout na full redraw.
- [x] Pri zmene stylu viditelne bunky spadnout na targeted/full redraw podle rozsahu.
- [x] Pridat vizualni test, ze bitmap shift nezanechava artefakty.
- [ ] Pridat benchmark porovnani full redraw vs bitmap shift pri wheel scrollu.

### 10.6 Cache pro text a layout

- [x] Cacheovat namerene sirky textu podle fontu a textu.
- [x] Invalidovat text measurement cache pri zmene fontu.
- [x] Omezit velikost text measurement cache.
- [x] Cacheovat canvas font string pro styl bunky.
- [x] Cacheovat fill/stroke hodnoty pro styl bunky.
- [x] Cacheovat display value pro viditelne bunky.
- [x] Invalidovat display value pri zmene hodnoty bunky.
- [x] Invalidovat display value pri zmene number format.
- [x] Cacheovat viditelny seznam radku pro aktualni viewport.
- [x] Cacheovat viditelny seznam sloupcu pro aktualni viewport.
- [x] Neprepocitavat viditelne radky, pokud se nezmenil `scrollTop` ani vysky radku.
- [x] Neprepocitavat viditelne sloupce, pokud se nezmenil `scrollLeft` ani sirky sloupcu.
- [ ] Pridat benchmark dopadu text measurement cache.
- [ ] Pridat benchmark dopadu visible layout cache.

### 10.7 Mouse scroll a drag selection

- [x] Pri wheel scrollu nevolat .NET callback pro kazdy wheel event.
- [x] Pri wheel scrollu kreslit nejvyse jednou za animation frame.
- [x] Pri wheel scrollu synchronizovat .NET stav az po frame nebo kratkem debounce.
- [x] Pri pointer move pro resize pouzivat jen hit-test throttle pres animation frame.
- [x] Pri pointer drag selection drzet rozsah lokalne v JS.
- [x] Pri pointer drag selection prekreslovat jen selection overlay.
- [x] Pri drag selection u hrany viewportu implementovat autoscroll.
- [x] Pri drag autoscrollu nepouzit Blazor render jako zdroj pohybu.
- [x] Pri ukonceni drag selection poslat finalni selection do .NET.
- [x] Pridat E2E test pro drag selection bez scrollu.
- [x] Pridat E2E test pro drag selection s autoscrollem dolu.
- [x] Pridat E2E test pro drag selection s autoscrollem doprava.

### 10.8 Frame scheduling a backpressure

- [x] Zavest jednu centralni funkci pro planovani canvas redrawu.
- [x] Sloucit vice pozadavku na redraw do jednoho animation frame.
- [x] Rozlisit typ redrawu: `selection`, `headers`, `content`, `full`.
- [x] Pri pending full redraw dovolit preskocit starsi content redraw.
- [x] Pri rychle interakci preferovat posledni stav pred mezistavy.
- [x] Pokud .NET callback bezi dlouho, nesmi blokovat lokalni canvas interakci.
- [x] Pokud .NET callback vrati starsi stav, nesmi prepsat novejsi lokalni interakci.
- [x] Pridat interny warning counter pro zahozene stare framy.
- [x] Pridat interny warning counter pro dlouhe draw framy nad 16 ms.
- [x] Pridat interny warning counter pro dlouhe draw framy nad 33 ms.

### 10.9 Cile mereni pred odskrtnutim faze

- [x] `ArrowDown` pres hranu viewportu nesmi vizualne skakat zpet.
- [x] `ArrowDown` pres hranu viewportu musi udrzet monotonni active row.
- [x] `ArrowRight` pres hranu viewportu musi udrzet monotonni active column.
- [ ] Wheel scroll musi byt plynulejsi nez aktualni canvas baseline.
- [ ] Drag selection musi byt plynulejsi nez aktualni canvas baseline.
- [ ] Canvas nesmi byt pri 1 000 x 50 pomalejsi nez DOM v zakladni keyboard navigaci.
- [ ] Canvas musi mit nizsi nebo srovnatelnou latenci selection pohybu nez DOM.
- [ ] Benchmark stranka musi ukazat oddelene casy pro keyboard, wheel a redraw.
- [x] E2E testy musi pokryt regresi skoku zpet pri keyboard scrollu.
- [x] Poznamka u faze musi popsat, ktere optimalizace mely realny dopad.

Poznamky:
- Canvas renderer ma ted oddeleny content canvas a selection overlay canvas. Pohyb aktivni bunky, range selection, autofill handle a hover se kresli pres overlay bez full redraw obsahu.
- Faze 10.4 je dokoncena jako tri canvas vrstvy: content, headers a selection overlay. Grid lines zustavaji soucasti content canvasu, protoze se prekresluji spolu s viditelnou mrizkou a samostatna vrstva by zatim jen zvysila slozitost.
- Faze 10.5 pridala bitmap shift pro male vertikalni a horizontalni scroll delty. Pri vetsim skoku, zmene velikosti canvasu/DPR, frozen rows/columns nebo strukturalni zmene se pouzije full redraw.
- JS zustava autoritativni pro lokalni keyboard/scroll/drag interakci a .NET frame s nizsi `interactionVersion` nesmi prepsat novejsi lokalni stav.
- E2E regresni testy pokryvaji monotonnost `ArrowDown`, `ArrowUp`, `ArrowRight`, `ArrowLeft`, existenci selection overlay vrstvy, overlay redraw metriky a drag selection bez scrollu.
- Realny dopad zatim prinesly hlavne tri zmeny: `interactionVersion` odstranil prepis novejsi JS selection starsim Blazor framem, selection overlay zlevnil pohyb aktivni bunky bez full content redrawu a viewport/selection callbacky se slucuji pres `requestAnimationFrame` nebo kratky debounce.

## Faze 11 - Hypergrid-inspired optimalizace canvas enginu

Cil: prestat pri rychle interakci bojovat s DOM scroll containerem a Blazor render pipeline. Canvas renderer ma mit vlastni lehky JS scroll/selection engine, jeden paint loop a minimalni mnozstvi prace na bunku.

### 11.1 Baseline pred zmenami

- [x] Zmerit aktualni `ArrowDown` navigaci bez scrollu.
- [x] Zmerit aktualni `ArrowDown` navigaci pres dolni hranu viewportu.
- [x] Zmerit aktualni `ArrowUp` navigaci pres horni hranu viewportu.
- [x] Zmerit aktualni `ArrowRight` navigaci pres pravou hranu viewportu.
- [x] Zmerit aktualni wheel scroll.
- [x] Zmerit aktualni drag selection s autoscrollem.
- [x] Do debug metrik pridat pocet nativnich `scroll` eventu behem keyboard navigace.
- [x] Do debug metrik pridat pocet `root.scrollTo` volani behem keyboard navigace.
- [x] Do debug metrik pridat cas straveny v `ensureCellVisibleLocal`.
- [x] Do debug metrik pridat cas straveny v `drawCells`.
- [x] Do debug metrik pridat cas straveny v `drawCellContent`.
- [x] Do debug metrik pridat pocet `ctx.save/clip/restore` na frame.
- [x] Do benchmark stranky pridat samostatny test `keyboard-scroll-edge`.
- [x] Do benchmark stranky pridat samostatny test `wheel-scroll`.
- [x] Do benchmark stranky pridat samostatny test `drag-autoscroll`.

Poznamky:
- Benchmark stranka ted ukazuje oddelene sloupce pro keyboard bez scrollu, keyboard pres dolni/horni/pravou hranu, wheel scroll a drag autoscroll.
- Edge keyboard baseline nejdrive vybere bunku u prislusne hrany viewportu a meri jen nekolik kroku pres hranu, aby casy nezkresloval dlouhy najezd od `A1`.
- Canvas debug metriky ted pocitaji nativni `scroll` eventy, `root.scrollTo` volani, cas `ensureCellVisibleLocal`, cas `drawCells`, cas `drawCellContent` a pocet `ctx.save/clip/restore` operaci.

### 11.2 Jednotny paint loop po vzoru Hypergridu

- [x] Zavest JS stav `dirtyKind` misto okamziteho planovani vice nezavislych redraw cest.
- [x] Zavest jeden centralni `requestPaint` vstup pro keyboard, wheel, pointer i .NET frame.
- [x] Sloucit `localFrame`, `viewportFrame`, `forcedViewportFrame` a selection redraw planovani do srozumitelneho scheduleru.
- [x] Oddelit `paintRequested` od `syncRequested`.
- [x] Pri vice pozadavcich v jednom frame zachovat pouze nejsilnejsi `dirtyKind`.
- [x] Pri rychle interakci preferovat posledni stav a zahodit mezistavy.
- [x] Pridat debug metriku pro pocet sloucenych paint pozadavku.
- [x] Pridat debug metriku pro pocet zahozenych mezistavu.
- [x] Overit, ze selection overlay se kresli maximalne jednou za animation frame.
- [x] Overit, ze content canvas se pri key repeat nekresli casteji nez jednou za animation frame.

Poznamky:
- Canvas renderer ma centralni `requestPaint`, ktery slucuje keyboard, scroll/wheel, pointer selection i novy .NET frame do jednoho `requestAnimationFrame` paintu.
- Scheduler drzi `dirtyKind` a v jednom frame ponecha nejsilnejsi pozadavek v poradi `selection < headers < content < full`.
- Viewport sync do .NET ma samostatny `requestViewportSync` se stavem `syncRequested`, aby paint scheduler neblokoval callbacky a callbacky neplanovaly vlastni paralelni paint frame.
- Debug metriky `mergedPaintRequestCount`, `discardedIntermediatePaintCount`, `paintFrameCount`, `selectionPaintFrameCount`, `contentPaintFrameCount` a `maxMergedPaintRequestsPerFrame` overuji slucovani pri rychlem key repeat.
- E2E sonda pro 80x `ArrowDown` overuje, ze opakovane keyboard paint pozadavky se slouci a prvni animation frame nekresli selection overlay ani content canvas vice nez jednou.

### 11.3 Logicky scroll model pro keyboard navigaci

- [x] Pridat do JS stavu `logicalScrollLeft` a `logicalScrollTop`.
- [x] Pri inicializaci naplnit logical scroll z nativniho `root.scrollLeft/root.scrollTop`.
- [x] Pri keyboard navigaci menit nejdriv logical scroll, ne nativni scroll.
- [x] Upravit `screenX` a `screenY`, aby pro keyboard hot path pouzivaly logical scroll.
- [x] Upravit hit test tak, aby dokazal pracovat s logical scroll stavem.
- [x] Upravit visible layout cache klic tak, aby pouzival logical scroll.
- [x] Pri `ArrowDown` pres dolni hranu viewportu neposilat okamzite `root.scrollTo`.
- [x] Pri `ArrowUp` pres horni hranu viewportu neposilat okamzite `root.scrollTo`.
- [x] Pri `ArrowRight` pres pravou hranu viewportu neposilat okamzite `root.scrollTo`.
- [x] Pri `ArrowLeft` pres levou hranu viewportu neposilat okamzite `root.scrollTo`.
- [x] Po skonceni key repeat synchronizovat nativni scroll container na logical scroll.
- [x] Pri blur/focus ztracenem mimo grid synchronizovat nativni scroll container.
- [x] Pri wheel scrollu prevzit nativni scroll do logical scrollu.
- [x] Pri programovem `ensureCellVisible` synchronizovat logical i native scroll.
- [x] Pridat guard proti tomu, aby pozdni nativni scroll event vratil logical scroll zpet.
- [x] Pridat E2E test, ze pri drzeni `ArrowDown` neroste pocet `root.scrollTo` volani linearnim tempem.
- [x] Pridat E2E test, ze `ArrowDown` pres hranu viewportu zachova monotonni active row i bez nativniho scroll eventu.

Poznamky:
- Keyboard navigace ted meni `logicalScrollLeft/Top` a kresli podle nej; nativni `root.scrollTo` se neposila pri kazde sipce.
- Nativni scroll container se po key repeatu dosynchronizuje pres kratky debounce, pri `focusout` okamzite.
- User scroll/wheel se bere z nativniho containeru a okamzite prepise logical scroll, aby zustal zachovany bezny scrollbar/touchpad tok.
- Programove `ensureCellVisible` synchronizuje logical i native scroll a vynuti viewport sync.
- Pozdni vlastni nativni scroll eventy se ignoruji, aby nevracely logical scroll na starsi pozici.
- E2E hot-path sonda pro 80x `ArrowDown` overuje `keyboardScrollToCount == 0`, koalescovany `scrollToCount` a rostouci `logicalScrollTop`.

### 11.4 Scrollbar jako reprezentace, ne zdroj pravdy

- [x] Rozhodnout, jestli ponechat soucasny scroll container jako scrollbar shell.
- [x] Pokud ano, pri keyboard hot path odlozit synchronizaci scrollbaru na debounce.
- [x] Pridat debounce pro sync logical scroll -> native scroll.
- [x] Pri uzivatelskem wheel/touchpad scrollu syncnout native scroll -> logical scroll okamzite.
- [x] Pri drag scrollbaru syncnout native scroll -> logical scroll okamzite.
- [x] Pri programovem scrollu oznacit scroll event jako vlastni, aby nespustil dalsi zbytecny redraw.
- [x] Pridat debug metriku pro pocet vlastnich scroll eventu.
- [x] Pridat debug metriku pro pocet uzivatelskych scroll eventu.
- [x] Overit, ze scrollbar pozice zustava vizualne spravna po dlouhem drzeni `ArrowDown`.

Poznamky:
- Soucasny native scroll container zustava jako scrollbar shell; pri keyboard hot path je zdrojem pravdy `logicalScrollLeft/Top`.
- Sync logical -> native bezi pres debounce po key repeatu a okamzite pri `focusout` nebo programovem scrollu.
- User scroll eventy z wheel/touchpadu i scrollbar dragu okamzite prepisi logical scroll a spusti normalni paint cestu.
- Vlastni programove scroll eventy jsou oznacene pres `ownScrollUntil`, zapocitaji se do `ownNativeScrollEventCount` a nespousti dalsi redraw.
- Debug metriky `userNativeScrollEventCount`, `wheelNativeScrollEventCount`, `scrollbarNativeScrollEventCount` a `ownNativeScrollEventCount` rozlisuji zdroj native scrollu.
- E2E overuje, ze po 80x `ArrowDown` zustane `keyboardScrollToCount == 0`, native scrollbar se po debounce dorovna na logical scroll a direct native scroll se pocita jako user scroll.

### 11.5 Rychlejsi kresleni bunek

- [x] Zmerit cenu `ctx.save`.
- [x] Zmerit cenu `ctx.clip`.
- [x] Zmerit cenu `ctx.restore`.
- [x] Pridat context state cache pro `font`.
- [x] Pridat context state cache pro `fillStyle`.
- [x] Pridat context state cache pro `strokeStyle`.
- [x] Pridat context state cache pro `lineWidth`.
- [x] Neprepisovat canvas context property, pokud se hodnota nezmenila.
- [x] Omezit per-cell clipping jen na bunky, kde text realne muze pretekat.
- [x] Pro jednoduche bunky bez overflow kreslit text bez `clip`.
- [x] Pro prazdne bunky nekreslit content cast vubec.
- [x] Pro default background bez selection/hover nevolat `fillRect`, pokud je pozadi uz vyplnene.
- [x] Udelat rychlou cestu pro bezny styl: normal font, default color, bez borderu, bez fillu.
- [x] Udelat pomalou cestu jen pro specialni formatting.
- [x] Pridat debug metriku `fastCellPathCount`.
- [x] Pridat debug metriku `slowCellPathCount`.
- [x] Pridat benchmark dopadu fast path.

Poznamky:
- Canvas content draw ma lehkou context state cache pro `font`, `fillStyle`, `strokeStyle`, `lineWidth`, `textAlign` a `textBaseline`; redundantni zapisy se pocitaji v `contextStateSkipCount`.
- `drawCellContent` uz pouziva `ctx.save/clip/restore` jen kdyz text realne preteka nebo potrebuje pomalou cestu; bezne kratke texty se kresli bez clippingu.
- Cena `ctx.save`, `ctx.clip` a `ctx.restore` se meri oddelene v `contextSaveTotalMs`, `contextClipTotalMs`, `contextRestoreTotalMs` a last-frame metrikach.
- Prazdne bunky preskakuji content draw a pocitaji se v `skippedEmptyCellContentCount`.
- Bezne bunky bez pozadi, borderu, dekoraci, explicitni barvy a bold/italic stylu jdou pres fast path; specialni formatting zustava slow path.
- Benchmark stranka ukazuje `Fast cells` a `Slow cells`, aby byl videt dopad fast path na konkretni dataset.

### 11.6 Partial cell snapshots

- [x] Navrhnout snapshot klic bunky: value, display value, font, foreColor, fill, borders, align, format flags.
- [x] Ulozit snapshot pro viditelne bunky do JS cache podle `row:col`.
- [x] Pri selection-only redrawu nemenit snapshot contentu.
- [x] Pri hover-only redrawu nemenit snapshot contentu.
- [x] Pri content redrawu preskocit bunku, pokud snapshot sedi a pozice se nezmenila.
- [x] Pri logical scrollu nepouzivat snapshot skip pro bunky, kterym se zmenila obrazova pozice.
- [x] Pri edit commit invalidovat snapshot jedne bunky.
- [x] Pri paste invalidovat snapshot dotcene oblasti.
- [x] Pri formatovani invalidovat snapshot dotcene oblasti.
- [x] Pri zmene column width invalidovat snapshot dotcene sloupce.
- [x] Pri zmene row height invalidovat snapshot dotcene radky.
- [x] Pridat debug metriku `cellSnapshotHitCount`.
- [x] Pridat debug metriku `cellSnapshotMissCount`.
- [x] Pridat E2E test, ze samotny pohyb selection nezpusobi content snapshot miss.

Poznamky:
- Canvas uklada offscreen snapshot contentu viditelnych neprazdnych bunek podle `row:col`; klic obsahuje raw hodnotu, display hodnotu, font, barvu textu, fill, zarovnani, dekorace, hyperlink flag a border metadata.
- Snapshot hit se pouzije jen kdyz sedi klic i obrazova pozice/velikost. Pri logical scrollu se zmenena pozice bere jako miss, takze se nepouzije obrazek ze stare souradnice.
- Selection-only a hover-only redraw jdou pres selection vrstvu, content snapshot cache se pri nich nemeni.
- Edit commit invaliduje jednu bunku primo v JS. Paste, formatovani, clear/cut/delete, vlozeni linku/obrazku a formula bar commit invaliduji dotcene bunky pres grid controller.
- Zmena sirky sloupce a vysky radku invaliduje dotcene sloupce/radky; resize viewportu a undo/redo cisti celou render cache.
- Debug metriky obsahuji `cellSnapshotHitCount`, `cellSnapshotMissCount`, `cellSnapshotStoreCount`, `cellSnapshotInvalidationCount` a `cellSnapshotCacheSize`.
- E2E test `CanvasRenderer_SelectionOnlyRedrawDoesNotMissContentSnapshots` overuje, ze opakovany content render pouzije snapshot hit a nasledny pohyb selection neprida content snapshot miss.

### 11.7 Keyboard repeat akcelerace

- [x] Do JS keyboard stavu pridat posledni navigacni klavesu.
- [x] Do JS keyboard stavu pridat cas zacatku repeat sekvence.
- [x] Do JS keyboard stavu pridat pocet repeat eventu.
- [x] Pri pauze delsi nez 500 ms resetovat repeat sekvenci.
- [x] Pri dlouhem drzeni `ArrowDown` volitelne zvetsit krok z 1 na vice radku.
- [x] Pri dlouhem drzeni `ArrowUp` volitelne zvetsit krok z 1 na vice radku.
- [x] Akceleraci zapnout jen pri navigaci bez Shift.
- [x] Akceleraci vypnout pri editaci.
- [x] Akceleraci vypnout pri formula point mode.
- [x] Akceleraci udelat konfigurovatelnou debug flagem.
- [x] Pridat E2E test, ze akcelerace nepreskakuje mimo sheet.
- [x] Pridat E2E test, ze kratke stisky zustanou po jednom radku.

Poznamky:
- JS stav drzi `keyboardRepeatKey`, `keyboardRepeatStartedAt`, `keyboardRepeatLastAt` a `keyboardRepeatCount`; sekvence se resetuje pri zmene klavesy, nerepeat stisku nebo pauze delsi nez 500 ms.
- Akcelerace se pouzije jen pro skutecne `KeyboardEvent.repeat` u `ArrowDown`/`ArrowUp`, bez Shiftu, mimo editor a mimo formula point mode.
- Krok roste postupne na 2/4/8 radku podle delky repeat sekvence; `navigateLocal` stale clampuje cilovy radek do rozsahu sheetu.
- Debug flag `window.tmSpreadsheetCanvas.keyboardRepeatAccelerationEnabled` umoznuje akceleraci vypnout/zapnout bez rebuild. Debug metriky ukazuji repeat eventy, akcelerovane eventy, posledni klavesu, posledni krok, maximalni krok, pocet sekvenci a resetu.
- E2E test `CanvasRenderer_KeyboardRepeatAcceleratesButClampsToSheetEnd` overuje akcelerovane drzeni `ArrowDown` u konce sheetu a clamp na posledni radek.
- E2E test `CanvasRenderer_ShortArrowPressesStaySingleStep` overuje, ze normalni kratke stisky zustavaji presne po jednom radku.

### 11.8 Editor overlay podle Hypergridu

- [x] Rozhodnout, ktere editace muze kompletne obslouzit JS local editor.
- [x] Bezne textove psani otevrit v JS local editoru bez Blazor renderu.
- [x] Dvojklik otevrit v JS local editoru bez Blazor renderu.
- [x] `Enter`/`F2` otevrit editor deterministicky a bez ztraty prvni klavesy.
- [x] `=` nechat napojene na .NET formula mode, dokud nebude formula point mode v JS.
- [x] Pri scrollu jen prepozicovat editor, pokud edited cell zustava viditelna.
- [x] Pri scrollu skryt editor, pokud edited cell opusti viewport.
- [x] Pri nav klavese v editoru commitnout hodnotu a predat navigaci gridu.
- [x] Pri `Escape` v editoru zahodit lokalni hodnotu bez content redrawu.
- [x] Pri nezmenene hodnote zavrit editor bez content redrawu.
- [x] Pri zmenene hodnote aktualizovat JS model okamzite.
- [x] Pri zmenene hodnote poslat commit do .NET mimo kriticky frame.
- [x] Pridat E2E test, ze editace zacata psanim prijme vice znaku.
- [x] Pridat E2E test, ze `Enter` po editaci posune active cell.
- [x] Pridat E2E test, ze scroll behem editace nerozbije pozici editoru.

Poznamky:
- JS local editor obsluhuje bezne textove psani, dvojklik a `Enter`/`F2`; vstup `=` zustava na .NET ceste kvuli formula point mode.
- Editor se pozicuje podle stejneho logical scroll modelu jako canvas. Pri malem scrollu zustava zarovnany s editovanou bunkou, pri opusteni viewportu se vizualne skryje nebo zavre.
- `Enter`, `Tab` a sipky uvnitr editoru commitnou lokalni hodnotu a hned zavolaji lokalni navigaci gridu.
- `Escape` editor zavre bez commitu a bez content redrawu. Nezmenena hodnota se pri blur commitu zavre bez content redrawu.
- Zmenena hodnota se zapise okamzite do JS render modelu, invaliduje snapshot bunky a commit do .NET se posila pres odlozeny callback, aby neblokoval aktualni paint frame.
- .NET `OnCanvasCellEditCommitted` uz nevraci aktivni bunku zpet na editovanou bunku, aby pozdni commit neprebil lokalni JS navigaci po `Enter`.
- E2E pokryva viceznakove psani, dvojklik, nezmeneny blur commit bez redrawu, `Enter` po editaci a scroll/logical scroll behem editace.

### 11.9 Wheel a drag scroll po vzoru logical scrollu

- [x] Prevest wheel delta na logical scroll delta.
- [x] Omezit wheel redraw na jeden paint loop frame.
- [x] Pri wheel scrollu nesynchronizovat .NET drive nez po paintu.
- [x] Pri drag selection autoscrollu menit logical scroll.
- [x] Pri drag selection autoscrollu nevolat nativni `scrollTo` v kazdem kroku.
- [x] Pri drag selection autoscrollu kreslit selection overlay v kazdem frame.
- [x] Pri drag selection autoscrollu kreslit content jen pri skutecne zmene viewportu.
- [x] Pridat E2E test pro drag selection autoscroll bez linearniho rustu .NET callbacku.
- [x] Pridat E2E test pro wheel scroll bez linearniho rustu forced viewport callbacku.

Poznamky:
- Wheel handler bezi jako non-passive listener, prevadi `deltaX/deltaY` vcetne line/page modu na logical scroll a brani nativnimu scrollu v hot path.
- Wheel redraw se slucuje pres jednotny paint loop; viewport sync do .NET se debouncuje az po paintu.
- Drag selection autoscroll uz meni logical scroll, planuje content redraw jen pri skutecnem posunu viewportu a nativni scrollbar synchronizuje odlozene, ne v kazdem frame.
- Selection pri drag autoscrollu zustava lokalne v JS a do .NET se synchronizuje po dokonceni tahu, aby callbacky nerostly s poctem frameu.
- E2E pokryva, ze drag autoscroll a wheel scroll nemaji linearni rust `scrollTo`, viewport callbacku ani selection callbacku.

### 11.10 Mereni pred odskrtnutim

- [x] `ArrowDown` pres hranu viewportu musi byt rychlejsi nez soucasna faze 10 baseline.
- [x] `ArrowUp` pres hranu viewportu musi byt rychlejsi nez soucasna faze 10 baseline.
- [x] `ArrowRight` pres hranu viewportu musi byt rychlejsi nez soucasna faze 10 baseline.
- [x] Wheel scroll musi byt rychlejsi nez soucasna faze 10 baseline.
- [x] Drag autoscroll musi byt rychlejsi nez soucasna faze 10 baseline.
- [x] Pri keyboard scrollu nesmi byt canvas pomalejsi nez DOM u datasetu 1 000 x 50.
- [x] Pri keyboard scrollu musi byt canvas pouzitelny u datasetu 10 000 x 100.
- [x] Pri keyboard scrollu musi zustat active row monotonni.
- [x] Pri keyboard scrollu se nesmi vracet starsi Blazor frame.
- [x] Editace zacata psanim nesmi ztratit zadny znak.
- [x] Formula point mode musi stale kreslit barevne reference.
- [x] Formatting a borders musi zustat viditelne po optimalizaci fast path.

Poznamky:
- Benchmark stranka vystavuje phase-11 metriky jako `data-*` atributy na vysledkovem radku, aby je E2E mohly kontrolovat bez parsovani lokalizovaneho textu.
- Wheel benchmark respektuje `preventDefault`; u canvasu uz po `wheel` eventu rucne neposouva native `scrollTop`, takze meri logical wheel hot path.
- Keyboard benchmark ceka pred merenim na vycisteni canvas in-flight callbacku a timeru, aby se do casu nemichal pripraveny klik/scroll.
- Canvas keyboard scroll odklada viewport sync i selection sync za paint/debounce; behem hot path uz neposila .NET callback v kazdem frame.
- Selection overlay se kresli z aktualnich selection bounds a viditelnych row/column frameu, ne z `Selected` priznaku kazde bunky. Tim se zlevnil pohyb aktivni bunky.
- Barevne formula reference zustaly zachovane pres cache bunek s `FormulaRefColorIndex`, aby normalni selection overlay nemusel prochazet vsechny bunky v kazdem frame.
- E2E `BenchmarkPage_Phase11ReadinessMetricsPass` overuje 1 000 x 50: keyboard edge pod faze 10 baseline, canvas srovnatelny s DOM, wheel/drag logical hot path a koalescovane callbacky.
- E2E `BenchmarkPage_CanvasKeyboardUsableOnLargeDataset` overuje pouzitelnost keyboard scrollu u 10 000 x 100.

Poznamky:
- Hypergrid drzi scroll jako logickou pozici a scrollbar je spis reprezentace stavu. Pro nas je to hlavni smer, protoze nativni `scrollTop` v hot path pravdepodobne stale vytvari cast cukani.
- Hypergrid pouziva dirty paint loop. Pro nas to znamena sjednotit vsechny redraw cesty do jednoho scheduleru a oddelit paint od syncu do .NET.
- Hypergrid setri canvas context property writes a vyhyba se clippingu tam, kde to jde. U nas je per-cell `save/clip/restore` pravdepodobny kandidat na dalsi zrychleni.
- Hypergrid editor je DOM overlay vlastneny gridem. Pro nas dava smysl presunout beznou editaci co nejvic do JS local editoru a do .NET posilat az commit.

## Faze 12 - JS-first spreadsheet engine

Cil: udelat z canvas rendereru samostatny JS grid engine, ktery je autoritativni pro hot path interakce. Blazor ma zustat verejne API, shell, toolbar/persistence vrstva a zdroj externich commandu, ale nema ridit kazdy mezistav pohybu, scrollu, editace nebo formula point mode.

### 12.1 Aktualni stav a hranice odpovednosti

- [x] Sepsat, ktere casti canvas hot path porad volaji .NET.
- [x] Sepsat, ktere .NET callbacky jsou nutne okamzite a ktere mohou byt delayed/commit-only.
- [x] Sepsat, ktere casti `BuildCanvasFrame` jsou potreba jen pri inicializaci a ktere pri kazdem renderu.
- [x] Zmerit latenci jednoho `ArrowDown` bez scrollu ve viewportu.
- [x] Zmerit latenci jednoho `ArrowDown` se scroll hranou viewportu.
- [x] Zmerit latenci kliknuti na bunku v normal mode.
- [x] Zmerit latenci kliknuti na bunku ve formula point mode.
- [x] Zmerit latenci psani do editoru po jednotlivych znacich.
- [x] Zmerit latenci potvrzeni formule.
- [x] Pridat docasny debug overlay/counter pro .NET callbacky v hot path.
- [x] Pridat docasny debug overlay/counter pro Blazor frame prijaty behem hot path.

Realne hotovo:

- Do `spreadsheet-canvas.js` byl pridan jednotny `invokeDotNet` wrapper a countery `dotNetCallbackCount`, `hotPathDotNetCallbackCount`, `dotNetCallbacksByMethod`, `hotPathDotNetCallbacksByMethod`, `blazorFrameCount`, `hotPathBlazorFrameCount` a `lastBlazorFrameAgeMs`. Metriky jsou dostupne pres `window.tmSpreadsheetCanvas.getDebugMetrics(grid)`.
- Do `spreadsheet-benchmark.js` byl pridan `window.tmSpreadsheetBenchmark.runPhase12LatencyProbe(selector)`. Probe meri prvni frame a ustaleni pro `ArrowDown` ve viewportu, `ArrowDown` na scroll hrane, normal click, formula point click, psani znaku a commit formule.
- Aktualni hot path porad vola .NET pro `OnCanvasSelectionChanged` po lokalnim keyboard pohybu, `OnCanvasViewportChanged` pri scroll/viewport synchronizaci, `OnCanvasCellPointer` pri normal/formula pointer vyberu, `OnCanvasPointer` pri pointer fallbacku, `OnCanvasDoubleClick`, `OnCanvasContextMenu`, `OnCanvasColumnResize`/`OnCanvasRowResize`, `OnCanvasKeyCommand` pro prikazy mimo lokalni editor a `OnCanvasCellEditCommitted` pri commitu hodnoty.
- Okamzite nebo skoro okamzite dnes zustavaji hlavne udalosti, ktere meni verejny stav aplikace nebo oteviraji Blazor-only workflow: formula point click, context menu, resize a prikazy mimo lokalni JS editor. Kandidati na delayed/commit-only jsou selection sync, viewport sync, textovy edit commit a batch cell changes.
- `BuildCanvasFrame` dnes porad sklada inicializacni i per-frame data dohromady. Inicializacni nebo strukturou ridka data jsou `RowCount`, `ColumnCount`, rozmery headeru, total size, gridlines, freeze pocty a row/column metriky. Per-render data jsou `ScrollLeft`, `ScrollTop`, viewport size, `ActiveCellRef`, selection, formula/format painter flags, `InteractionVersion`, visible rows, visible columns a visible cells vcetne hodnot/stylu/formula reference metadat.
- E2E test `BenchmarkPage_Phase12LatencyProbeMatchesHotPathCriteria` overuje, ze probe vraci vsechny merene scenare a ze JS hot path drzi cilene faze 12 kriterium.

### 12.2 JS runtime state jako zdroj pravdy

- [x] Vytvorit JS `workbookState`/`sheetState` objekt oddeleny od Blazor frame objektu.
- [x] Presunout `activeCell`, `selection`, `scroll`, `hover`, `drag`, `editor` a `formulaMode` do JS state.
- [x] Pridat monotonne `localRevision` pro JS state.
- [x] Pridat `serverRevision`/`blazorRevision` pro posledni prijaty .NET snapshot.
- [x] Pridat pravidlo, ze starsi Blazor snapshot nikdy neprepise novejsi lokalni JS state.
- [x] Pridat E2E test, ze rychly keyboard pohyb neprepisuje starsi Blazor frame.
- [x] Pridat E2E test, ze rychla editace neprepisuje text starsim Blazor frame.

Realne hotovo:

- Canvas registrace vytvari samostatny `workbookState` a aktivni `sheetState`; Blazor frame zustava renderovaci snapshot, ale lokalni hot path zapisuje nejdriv do JS state.
- V `sheetState` jsou aktualne `activeCell`, `selection`, `scroll`, `hover`, `drag`, `editor`, `formulaMode` a `formatPainterActive`.
- `localRevision` je monotonne zvysovana pro keyboard/pointer/scroll/editor interakce. `serverRevision` drzi posledni prijaty `InteractionVersion` z Blazor snapshotu a `blazorRevision` interni poradi prijatych Blazor framu.
- Stary Blazor frame s nizsim `InteractionVersion` uz neprepisuje JS selection, scroll, formula flags ani rozpracovany editor; misto toho se frame pred vykreslenim prevrstvi lokalnim `sheetState`.
- Debug metriky vraci `localRevision`, `serverRevision`, `blazorRevision` a kopii zjednoduseneho `workbookState`/`sheetState`, aby slo sledovat hranici mezi JS a Blazorem.
- Pridane E2E testy `CanvasRenderer_JsSheetStateRejectsStaleBlazorSelectionFrame` a `CanvasRenderer_JsEditorStateRejectsStaleBlazorFrame` overuji, ze stary Blazor snapshot nevrati selection ani text editoru zpet.

### 12.3 JS cell store a indexy

- [x] Vytvorit JS sparse cell store indexovany podle `row:col`.
- [x] Pri inicializaci naplnit JS store z Blazor sheet snapshotu.
- [x] Pro visible cells pouzivat JS store misto opakovaneho prochazeni C# frame array.
- [x] Pridat index pro formula reference cells.
- [x] Pridat index pro styled/non-empty cells.
- [x] Pridat index pro merged cells, pokud jsou v canvas rendereru podporovane.
- [x] Pridat API `setCells` pro batch update z Blazoru do JS store.
- [x] Pridat API `clearCells` pro batch delete z Blazoru do JS store.
- [x] Pridat E2E test, ze externi batch update z Blazoru se projevi v canvasu.
- [x] Pridat E2E test, ze keyboard selection neprochazi vsechny sampled cells.

Realne hotovo:

- `sheetState.cellStore` je sparse `Map` indexovana podle `row:col`. Store se plni pri kazdem prijatem Blazor frame snapshotu a chova se jako JS cache bunek pro aktualni canvas engine.
- Store ma indexy `formulaRefs`, `styledOrNonEmpty` a `merged`. Merged index se plni podle explicitniho merge flagu, pokud nekdy prijde, nebo podle toho, ze frame cell presahuje beznou sirku/vysku radku/sloupce.
- `findCell` pouziva cell store a pada zpet na pruchod `model.Cells` jen jako fallback; debug metriky meri `cellStoreLookupCount`, `cellStoreHitCount`, `cellStoreMissCount` a `cellStoreFrameScanCount`.
- `drawCells` uz bere visible cells ze store podle aktualniho visible layoutu a `lastVisibleKeys`, takze renderer nepouziva primo C# frame array jako primarni zdroj bunek.
- Formula reference overlay pouziva `formulaRefs` index misto opakovaneho filtrovani vsech cells.
- Pridane JS API `window.tmSpreadsheetCanvas.setCells(root, cells)` a `window.tmSpreadsheetCanvas.clearCells(root, cellsOrRefs)` pro batch update/delete store; update invaliduje cell snapshot a prekresli content.
- Debug metriky vraci velikost store, revision a pocty indexu (`cellStoreSize`, `cellStoreRevision`, `cellStoreFormulaRefCount`, `cellStoreStyledOrNonEmptyCount`, `cellStoreMergedCount`).
- Pridane E2E testy `CanvasRenderer_CellStoreBatchUpdateRepaintsVisibleCell` a `CanvasRenderer_KeyboardSelectionUsesCellStoreWithoutFrameScans`.
- Omezeni: store zatim cerpa geometrii z Blazor frame visible bunek. Plny vypocet visible bunek bez Blazor frame patri do 12.4.

### 12.4 JS layout engine pro viewport

- [x] Presunout vypocet visible rows do JS.
- [x] Presunout vypocet visible columns do JS.
- [x] Presunout row/column size cache do JS.
- [x] Pridat binarni vyhledavani row podle `scrollTop`.
- [x] Pridat binarni vyhledavani column podle `scrollLeft`.
- [x] Pridat overscan konfigurovatelny v JS.
- [x] Pridat invalidaci layout cache pri resize row/column.
- [x] Pridat invalidaci layout cache pri zmene freeze rows/columns.
- [x] Pridat E2E test, ze wheel scroll nezada novy Blazor frame pro visible layout.
- [x] Pridat E2E test, ze keyboard scroll nezada novy Blazor frame pro visible layout.

Realne hotovo:

- `sheetState.layoutState` drzi JS cache velikosti radku/sloupcu, labely sloupcu, freeze pocty, header rozmery a revision countery.
- `getVisibleLayout` uz nepouziva aktualni `model.Rows`/`model.Columns` jako primarni visible layout. Viditelne radky/sloupce sklada v JS z offset cache, `scrollTop`/`scrollLeft`, viewport rozmeru a freeze oblasti.
- Offset cache pro radky/sloupce se stavi v JS z poslednich znamych velikosti a default velikosti odvozene z frame snapshotu.
- Vyhledani prvniho/posledniho viditelneho radku a sloupce pouziva binarni vyhledavani nad offset cache.
- Overscan je konfigurovatelny pres `window.tmSpreadsheetCanvas.layoutOverscanRows` a `window.tmSpreadsheetCanvas.layoutOverscanColumns`.
- Layout cache se invaliduje pri prijeti zmenenych row/column velikosti, pri resize observeru, pri resize radku/sloupce a pri zmene freeze rows/columns.
- `hitCell`, fallback hit-test pro drag selection, resize hit-test, editor positioning, selection overlay a header rendering pouzivaji JS layout frames.
- Debug metriky sleduji `visibleLayoutJsComputeCount`, `visibleLayoutBinarySearchCount`, `layoutRowSizeCacheSize`, `layoutColumnSizeCacheSize`, `layoutRevision`, overscan a invalidace.
- Pridane E2E testy `CanvasRenderer_WheelScrollUsesJsLayoutWithoutBlazorFrame` a `CanvasRenderer_KeyboardScrollUsesJsLayoutWithoutBlazorFrame`.
- Omezeni: JS layout uz umi rozhodnout, ktere souradnice jsou viditelne bez Blazor frame, ale hodnoty/styly dosud nevidenych bunek jsou dostupne jen pokud uz existuji v `cellStore` nebo prijdou patchem.

### 12.5 JS renderer pipeline

- [x] Rozdelit renderer na `contentLayer`, `headerLayer`, `selectionLayer`, `editorLayer`.
- [x] Vytvorit dirty flags pro content/header/selection/editor.
- [x] Vytvorit dirty rect pro selection pohyb bez content redrawu.
- [x] Vytvorit dirty rect pro local cell edit commit.
- [x] Zachovat bitmap shift pro male scroll delty.
- [x] Udelat fallback full redraw jen pri velkem skoku, resize nebo strukturalni zmene.
- [x] Presunout font/text/style cache pod JS engine state.
- [x] Pridat frame budget metriky pro content draw a selection draw.
- [x] Pridat E2E test, ze `ArrowDown` ve viewportu kresli jen selection layer.
- [x] Pridat E2E test, ze `ArrowDown` pres scroll hranu kresli content nejvyse jednou za frame.

Realne hotovo:

- Canvas renderer ma `renderer` state s vrstvami `content`, `header`, `selection` a `editor`. Editor vrstva je zatim DOM `input` layer ridena renderer state, ne dalsi canvas.
- Dirty flags rozlisuji `content`, `header`, `selection`, `editor` a `full`; scheduler je pri vice pozadavcich slucuje podle narocnosti.
- Selection pohyb uklada dirty rect pro predchozi i novy selection stav a `renderSelectionOverlay` umi prekreslit jen dirty oblast bez content redrawu.
- Local edit commit uklada content dirty rect pro editovanou bunku, invaliduje jeji snapshot a content layer umi kreslit jen pres dirty rect.
- Bitmap shift pro male scroll delty zustal zachovany v `tryBitmapScrollRedraw`; velky skok, resize, freeze oblast nebo prazdny viewport padaji na full redraw.
- Font/text/style/display/cell snapshot cache jsou navazane pod `renderer.cache`, stare property na state zustavaji jako kompatibilni aliasy pro existujici testy a debug.
- Pridane metriky: `contentLayerPaintCount`, `headerLayerPaintCount`, `selectionLayerPaintCount`, `editorLayerUpdateCount`, dirty rect pocty/plochy a frame budget casy pro content/selection.
- Pridane E2E testy `CanvasRenderer_ArrowDownInViewportPaintsOnlySelectionLayer` a `CanvasRenderer_ArrowDownAtScrollEdgePaintsContentAtMostOnce`.

### 12.6 JS editor pro text

- [x] Udelat JS editor jako jediny editor pro canvas renderer v normal mode.
- [x] Pri psani prvniho znaku otevrit editor bez Blazor callbacku.
- [x] Pri double click otevrit editor bez Blazor callbacku.
- [x] Pri `F2`/`Enter` otevrit editor bez Blazor callbacku.
- [x] Pri `Escape` zahodit editaci lokalne.
- [x] Pri `Enter` commitnout hodnotu do JS store a posunout selection lokalne.
- [x] Pri `Tab` commitnout hodnotu do JS store a posunout selection lokalne.
- [x] Pri blur commitnout hodnotu do JS store.
- [x] Commit do .NET posilat jako delayed `cellChanged` event.
- [x] Sloucit vice rychlych commit udalosti do jednoho batch eventu.
- [x] Pridat E2E test, ze psani 20 znaku neztrati zadny znak.
- [x] Pridat E2E test, ze rychle `Enter` po editaci nevrati aktivni bunku zpet.

Realne hotovo:

- Normalni textovy canvas editor se otevira v JS pri prvnim znaku, double clicku, `F2` a `Enter`; tyto vstupy nejdou pres `OnCanvasKeyCommand`.
- `Escape` zavre editor lokalne bez commitu.
- `Enter`, `Tab`, sipky v editoru a blur commitnou hodnotu do JS cell store, invaliduji dirty rect bunky a lokalne posunou selection bez cekani na Blazor frame.
- Commit do .NET jde opozdene pres `OnCanvasCellEditCommittedBatch`; rychle commity se slucuji do jedne fronty a u stejne bunky se drzi posledni hodnota.
- Pridane debug metriky pro otevreni editoru, lokalni commity a batch synchronizaci.
- Pridane E2E testy `CanvasRenderer_JsEditorTypingTwentyCharactersKeepsAllCharacters` a `CanvasRenderer_JsEditorFastEnterCommitKeepsNextActiveCell`.

### 12.7 JS formula editor

- [x] Pri napsani `=` zapnout formula edit mode v JS bez Blazor callbacku.
- [x] V JS udrzovat text formule, caret a aktivni reference token.
- [x] Pridat lehky parser A1 referenci pro single cell.
- [x] Pridat lehky parser A1 range referenci.
- [x] Pri kliknuti na bunku vlozit referenci do editoru lokalne.
- [x] Pri drag range vlozit range referenci do editoru lokalne.
- [x] Pri opakovanem kliknuti/drag aktualizovat aktualni formula token lokalne.
- [x] Kreslit barevne formula reference z JS formula state.
- [x] Pri `Enter` commitnout formuli do JS store a delayed .NET eventu.
- [x] Pri `Escape` vratit puvodni hodnotu/formuli lokalne.
- [x] Pri `Backspace`/editaci textu prebarvit reference bez Blazor callbacku.
- [x] Pridat E2E test, ze klik na bunku ve formula editoru vlozi referenci pod 1 frame.
- [x] Pridat E2E test, ze drag range ve formula editoru vlozi range bez Blazor callbacku.
- [x] Pridat E2E test, ze barevne reference zustanou viditelne po scrollu.

Realne hotovo:

- Znak `=` otevre canvas editor a zapne JS formula mode bez `OnCanvasKeyCommand`.
- JS drzi `formulaEditor` state: text, caret, aktivni token, parsed reference, drag anchor/current a debug metriky.
- Parser umi jednoduche A1 reference i A1 range tokeny, vcetne `$` variant, a reference prebarvuje pri `input`/`keyup`/caret zmenach.
- Klik na bunku v formula editoru vlozi nebo nahradi aktivni token lokalne, bez `OnCanvasCellPointer`.
- Drag v formula editoru nahradi aktivni token range referenci lokalne.
- Selection layer kresli formula highlighty primo z JS formula state; synthetic reference zustavaji viditelne i po scrollu.
- `Enter` commitne formuli do JS cell store jako `Value` i `Formula` a .NET dostane delayed batch commit.
- `Escape` zavre editor bez commitu, tedy puvodni hodnota/formule zustava v JS store.
- Pridane E2E testy `CanvasRenderer_JsFormulaEditorClickInsertsReferenceWithinOneFrame`, `CanvasRenderer_JsFormulaEditorDragInsertsRangeLocally`, `CanvasRenderer_JsFormulaEditorEnterCommitsFormulaLocally` a `CanvasRenderer_JsFormulaReferenceHighlightsRemainVisibleAfterScroll`.

### 12.8 JS command log a synchronizace do Blazoru

- [x] Definovat command log udalosti: `cellChanged`, `rangeChanged`, `selectionSettled`, `viewportSettled`, `formulaCommitted`.
- [x] Posilat `cellChanged` do Blazoru az po commit editoru.
- [x] Posilat `selectionSettled` az po debounce nebo blur/focus out.
- [x] Posilat `viewportSettled` az po debounce scrollu.
- [x] Sloucit vice cell edit commitu do batch payloadu.
- [x] Sloucit paste do jednoho batch payloadu.
- [x] Pridat ack/revision z Blazoru pro prijate commandy.
- [x] Pridat retry nebo safe ignore pro zrusene/obsolete commandy.
- [x] Pridat E2E test, ze rychle klavesove pohyby neposilaji .NET callback per key.
- [x] Pridat E2E test, ze rychla editace vice bunek posle batch commit.

### 12.9 Blazor jako shell a verejne API

- [x] Zachovat `TmSpreadsheet` verejne parametry.
- [x] Zachovat DOM renderer beze zmen jako kompatibilni fallback.
- [x] Canvas renderer pri inicializaci vola JS `initEngine` misto opakovaneho frame renderu.
- [x] Blazor toolbar commandy posilat do JS engine jako commandy.
- [x] Style commandy z toolbaru aplikovat nejdriv v JS store.
- [x] Style commandy nasledne poslat do .NET jako batch command.
- [x] Externi zmena workbooku z .NET posila JS engine patch, ne kompletni frame, pokud to jde.
- [x] Pridat `RenderMode=CanvasJsEngine` nebo internu feature flag pro postupnou migraci.
- [x] Pridat E2E test, ze DOM renderer zustal funkcni.
- [x] Pridat E2E test, ze canvas JS engine zustal kompatibilni s verejnym API.

### 12.10 Paste, fill a velke batch operace

- [x] Presunout paste parsing pro canvas hot path do JS.
- [x] Pri paste zapsat hodnoty okamzite do JS store.
- [x] Pri paste prekreslit jen dotcene dirty rects nebo viewport.
- [x] Paste commit do .NET poslat jako batch.
- [x] Autofill drag drzet lokalne v JS.
- [x] Autofill preview kreslit lokalne v selection layer.
- [x] Autofill commit poslat jako batch.
- [x] Pridat E2E test pro paste 100 x 20 bez zablokovani hot path.
- [x] Pridat benchmark paste latency JS engine vs soucasny canvas.

### 12.11 Accessibility a focus strategie

- [x] Zachovat root focus element pro keyboard.
- [x] Udrzovat aria active cell text z JS state.
- [x] Posilat live region update throttle/debounce.
- [x] Editor input musi mit spravny focus a selection/caret.
- [x] Formula editor musi byt citelny pro screen reader jako text input.
- [x] Popsat rozdil pristupnosti DOM rendereru a canvas JS engine.
- [x] Pridat E2E/a11y smoke test pro focus po keyboard navigaci.

Poznamka: DOM renderer zustava pristupnejsi varianta pro ctecky obrazovky, protoze exposeuje realne `gridcell` elementy a nativni per-cell semantiku. Canvas JS engine pro vykon drzi fokus na jednom `grid` root elementu, pouziva synthetic `aria-activedescendant` + live region a pri editaci prepina do realneho text inputu.

### 12.12 Benchmarky a kriterium hotovo

- [x] Pridat benchmark `single-arrow-in-viewport`.
- [x] Pridat benchmark `single-arrow-scroll-edge`.
- [x] Pridat benchmark `formula-cell-click-latency`.
- [x] Pridat benchmark `typing-latency`.
- [x] Pridat benchmark `.NET callbacks per interaction`.
- [x] `ArrowDown` ve viewportu musi byt lokalni JS-only bez .NET callbacku.
- [x] `ArrowDown` ve viewportu musi kreslit jen selection layer.
- [x] Klik na bunku ve formula editoru musi byt JS-only az do commit formule.
- [x] Psani v editoru nesmi vyvolat Blazor render per key.
- [x] Wheel scroll nesmi vyvolat Blazor render per wheel event.
- [x] Drag selection nesmi vyvolat Blazor render per pointer move.
- [ ] Canvas JS engine musi byt subjektivne plynulejsi nez soucasny canvas u 1 000 x 50.
- [x] Canvas JS engine musi byt pouzitelny u 10 000 x 100.
- [x] V planu zaznamenat realne namerene hodnoty pred a po migraci.

Poznamka k overeni 2026-05-07:
- Benchmark `1 000 x 50`, legacy `Canvas`: `single-arrow-in-viewport = 16.70 ms`.
- Benchmark `1 000 x 50`, `CanvasJsEngine`: `single-arrow-in-viewport = 17.00 ms`, `.NET callbacks = 0`, `selection paint frames = 1`, `content paint frames = 0`.
- Benchmark `1 000 x 50`, `CanvasJsEngine`: `single-arrow-scroll-edge = 16.90 ms`, `formula-cell-click-latency = 17.00 ms`, `typing-latency = 17.00 ms`.
- Benchmark `1 000 x 50`, `CanvasJsEngine`: `formula click .NET callbacks = 0`, `typing .NET callbacks = 0`, `typing blazor frames = 0`, `callbacks per interaction = 0.00`.
- Benchmark `1 000 x 50`, `CanvasJsEngine`: `wheel events = 22`, `wheel blazor frames = 0`, `drag frames = 15`, `drag blazor frames = 0`.
- Dataset `10 000 x 100`: E2E `BenchmarkPage_CanvasKeyboardUsableOnLargeDataset` prosel po prepnuti benchmark cesty na `CanvasJsEngine`.

### 12.13 JS-only resize radku a sloupcu

- [x] Zmapovat soucasny hot path pro `column resize` a `row resize` vcetne vsech .NET callbacku behem drag.
- [x] Pridat debug metriky pro resize: `resizePointerMoveCount`, `resizePaintFrameCount`, `resizeDotNetCallbackCount`, `resizeBlazorFrameCount`.
- [x] Pridat benchmark/probe `column-resize-drag-latency`.
- [x] Pridat benchmark/probe `row-resize-drag-latency`.
- [x] Pri `pointerdown` na resize handle prepnout grid do lokalniho JS resize session stavu.
- [x] Pri `pointermove` behem resize nepoustet zadny Blazor callback per move.
- [x] Pri `pointermove` behem resize aktualizovat jen lokalni preview sirky/vysky v JS state.
- [x] Pri `column resize` kreslit behem drag nejdriv jen guide line + preview hodnotu sirky.
- [x] Pri `row resize` kreslit behem drag nejdriv jen guide line + preview hodnotu vysky.
- [x] Omezit resize repaint na `requestAnimationFrame`, aby vice pointer eventu splinulo do jednoho frame.
- [x] Overit, ze `column resize` behem drag nespousti full content redraw per move.
- [x] Overit, ze `row resize` behem drag nespousti full content redraw per move.
- [x] Po `pointerup` propsat finalni sirku sloupce do JS layout cache a prepocitat offsety jen jednou.
- [x] Po `pointerup` propsat finalni vysku radku do JS layout cache a prepocitat offsety jen jednou.
- [x] Poslat do .NET az commit resize po `pointerup`, ne prubezne zmeny.
- [x] Sloucit commit resize do command log/batch payloadu stejne jako ostatni JS hot path akce.
- [x] Pri commit resize invalidovat jen dotcene header/content oblasti, ne bezpodminecne cely grid.
- [x] Po commit resize zachovat korektni pozici editoru, selection overlaye a formula highlightu.
- [x] Overit resize v pritomnosti freeze rows/columns.
- [x] Overit resize na velkem datasetu `10 000 x 100`.
- [x] Pridat E2E test `column resize drag` bez .NET callbacku per move.
- [x] Pridat E2E test `row resize drag` bez .NET callbacku per move.
- [x] Pridat E2E test, ze po commit resize zustane workbook/.NET model synchronizovany.
- [x] Zapsat realne namerene hodnoty pred a po JS-only resize migraci.

Poznamka k overeni 2026-05-07:
- Prime E2E probe `CanvasJsEngine_ColumnResizeDragStaysJsOnlyUntilCommitAndSyncsModel` a `CanvasJsEngine_RowResizeDragStaysJsOnlyUntilCommitAndSyncsModel` overily, ze behem drag zustava `.NET callbacks before commit = 0`, `Blazor frames before commit = 0` a `content paint frames before commit = 0`.
- Benchmark `1 000 x 50`, `CanvasJsEngine`: `column-resize-drag = 946.40 ms`, `pointer moves = 8`, `column resize .NET callbacks = 0`, `column resize blazor frames = 0`.
- Benchmark `1 000 x 50`, `CanvasJsEngine`: `row-resize-drag = 862.90 ms`, `pointer moves = 8`, `row resize .NET callbacks = 1`, `row resize blazor frames = 2`, `row resize paint frames = 8`.
- E2E `BenchmarkPage_ResizeReadinessMetricsPass` prosel pro benchmark row metriky resize hot path.
- E2E `CanvasJsEngine_ResizeCommitKeepsEditorSelectionAndFormulaHighlightAligned` overil, ze `syncLayoutAxes` po resize drzi editor, selection overlay i formula highlight v novem layoutu.
- E2E `CanvasJsEngine_ResizeHotPathWorksWithFrozenAxes` overil lokalni resize hot path i pro frozen prvni radek a sloupec.
- E2E `BenchmarkPage_CanvasResizeUsableOnLargeDataset` prosel pro dataset `10 000 x 100`.
- E2E `CanvasJsEngine_RowResizeSecondDragStartsFromCommittedHeight` a `CanvasJsEngine_ColumnResizeSecondDragStartsFromCommittedWidth` kryji regresi, ze opakovany resize musi startovat z uz commitnute velikosti, ne z puvodni.

### 12.14 Ergonomie formula editoru podle caret a aktivniho tokenu

- [x] Zmapovat aktualni chovani JS formula editoru pro `F4`, klik do aktivni bunky a `ArrowLeft`/`ArrowRight`.
- [x] Pridat debug metriky pro `formulaEditorCaretMoveCount`, `formulaEditorTokenReplaceCount`, `formulaEditorIgnoredSelfClickCount`, `formulaEditorArrowCaretCount`.
- [x] Formalne popsat pravidlo, ze pri editaci formule je autoritou textovy editor a grid slouzi jen jako source/reference surface.
- [x] Dodelat parser tokenu tak, aby umel deterministicky vratit reference token podle `selectionStart`/`selectionEnd`, ne jen posledni token ve formuli.
- [x] Rozlisit `caret token`, `active token` a `selection token`, aby bylo jasne co ma byt zmeneno pri `F4`, kliku nebo drag range.
- [x] U `F4` cyklit absolutni reference token, ve kterem aktualne stoji caret.
- [x] U `F4` podporit i range token `A1:B5` jako jeden logicky token.
- [x] U `F4` nic nedelat, pokud caret neni uvnitr zadne reference.
- [x] Zachovat caret na smysluplne pozici po `F4` transformaci tokenu.
- [x] Pri kliknuti do stejne bunky, ktera je prave editovana, nevkladat self-reference do formule.
- [x] Pri kliknuti do stejne bunky behem formula editace nenechat grid zmenit selection ani active cell.
- [x] Pri kliknuti do stejne bunky jen zachovat fokus editoru a aktualni caret/text state.
- [x] Pri kliknuti do jine bunky vlozit nebo nahradit referenci podle aktivniho tokenu u caret pozice.
- [x] Pri drag range v formula editoru nahradit referenci podle aktivniho tokenu u caret pozice, ne podle posledni reference ve formule.
- [x] `ArrowLeft` a `ArrowRight` v formula editoru routovat do pohybu caret v textu, ne do grid navigation.
- [x] `Home`, `End`, `Ctrl+ArrowLeft`, `Ctrl+ArrowRight` nechat fungovat jako textove pohyby caret uvnitr editoru.
- [x] `ArrowUp` a `ArrowDown` pri editaci formule zatim explicitne rozhodnout: bud caret movement v multiline scenari, nebo zustat bez grid navigation, dokud neni definovano jine chovani.
- [x] Zachovat grid navigation sipkami jen mimo editor nebo po commitu editoru.
- [x] Udrzovat formula highlighty podle tokenu u caret i po jeho posunu vlevo/vpravo.
- [x] Pri posunu caret prebarvit `active formula token` bez plneho content redraw.
- [x] Osetrit pripad, kdy formula obsahuje vic referenci a caret se presouva mezi nimi.
- [x] Osetrit pripad, kdy formula obsahuje kombinaci funkci, literalu a range tokenu, napr. `=SUM(A1:B5)+C7`.
- [x] Osetrit pripad, kdy je caret uvnitr absolutni reference typu `$A$1`, `A$1`, `$A1`.
- [x] Osetrit pripad, kdy je caret tesne pred nebo za tokenem reference.
- [x] Osetrit pripad vyberu textu pres vice tokenu; pro prvni implementaci jasne definovat, zda `F4` ceka na collapse selection nebo ceka na token pod `selectionStart`.
- [x] Zajistit, aby context menu, pointer selection a resize session nemohly behem formula editace rozbit editor focus/caret.
- [x] Zajistit, aby formula bar a canvas editor mely stejna pravidla pro `F4`, caret token a self-click semantiku, nebo zdokumentovat vedomy rozdil.
- [x] Pridat E2E test, ze `F4` meni referenci pod caret u prvni reference ve formuli.
- [x] Pridat E2E test, ze `F4` meni referenci pod caret u posledni reference ve formuli.
- [x] Pridat E2E test, ze `F4` funguje i na range tokenu.
- [x] Pridat E2E test, ze `F4` mimo reference nic nezmeni.
- [x] Pridat E2E test, ze klik do stejne editovane bunky nevlozi self-reference.
- [x] Pridat E2E test, ze klik do jine bunky nahradi token podle caret pozice.
- [x] Pridat E2E test, ze `ArrowLeft`/`ArrowRight` v formula editoru posouvaji caret a nemeni active cell.
- [x] Pridat E2E test, ze highlight aktivni reference sleduje caret pri pohybu mezi tokeny.
- [x] Pridat E2E test pro formuli s vice referencemi a kombinaci range + single ref.
- [x] Zapsat finalni rozhodnuti o chovani `ArrowUp`/`ArrowDown` v formula editoru.
- [x] Zapsat realne overeni, ze chovani odpovida ocekavane ergonomii podobne Excel/Google Sheets.

Poznamka k rozhodnutim 2026-05-07:
- Pri formula editaci je autoritou textovy editor. Grid funguje jako reference surface; klik do jine bunky nebo drag range meni text formule podle aktivniho tokenu u caret pozice.
- `selectionStart != selectionEnd` v prvni implementaci pouziva token pod `selectionStart`. `F4` tedy necili na "posledni token", ale na aktivni token podle caret/selection anchor.
- `ArrowLeft`, `ArrowRight`, `Home`, `End`, `Ctrl+ArrowLeft` a `Ctrl+ArrowRight` zustavaji v editoru a nemeni active cell gridu.
- `ArrowUp` a `ArrowDown` pri formula editaci zamerne nespousti grid navigation; zustava nativni chovani single-line inputu a parity s vice-radkovym editorem se odklada do 12.15.
- Aktivni formula token se pri posunu caret prebarvuje jen v `selectionLayer`; E2E overily, ze pri tom nevznika `content` repaint.
- Formula bar zatim vedome nema stejnou caret/token semantiku jako canvas JS editor; plna parita je presunuta do 12.15.

Poznamka k overeni 2026-05-07:
- E2E prosly: `CanvasJsEngine_F4CyclesAbsoluteReferencesInFormulaEditor`, `CanvasJsEngine_F4CyclesReferenceAtCaretForFirstFormulaToken`, `CanvasJsEngine_F4CyclesReferenceAtCaretForLastFormulaToken`, `CanvasJsEngine_F4CyclesRangeTokenAtCaret`, `CanvasJsEngine_F4OutsideReferenceLeavesFormulaUnchanged`.
- E2E prosly: `CanvasJsEngine_FormulaSelfClickDoesNotInsertSelfReference`, `CanvasJsEngine_FormulaClickReplacesReferenceAtCaret`, `CanvasJsEngine_FormulaClickReplacesCorrectTokenInMixedRangeAndSingleReferenceFormula`.
- E2E prosly: `CanvasJsEngine_FormulaArrowLeftRightMoveCaretWithoutChangingActiveCell`, `CanvasJsEngine_FormulaHighlightFollowsCaretAcrossTokens`.

### 12.15 Excel-like / Google Sheets-like formula UX

- [x] Zmapovat rozdily mezi soucasnym canvas formula editorem, formula barem a ocekavanym UX podobnym Excel/Google Sheets.
- [x] Formalne rozhodnout, zda ma byt formula bar a inline canvas editor jeden sdileny editor state machine.
- [x] Vytvorit sdileny model `formula editing session`, ktery bude drzet text, caret, selection, aktivni token, parsed tokeny, anchor token a reference-picking mode.
- [x] Sjednotit pravidla pro inline editor a formula bar tak, aby `F4`, klikani na bunky, drag range a caret movement fungovaly stejne.
- [x] Rozhodnout a zapsat pravidla pro `selectionStart != selectionEnd` pri formula editaci.
- [x] Podporit nahradu aktivni reference podle caret i pri vyberu casti tokenu.
- [x] Podporit vlozeni reference na prazdne misto ve formuli bez rozbiti syntaxe.
- [x] Podporit bezpecnou nahradu jednoho tokenu uvnitr slozitejsich vyrazu s vice funkcemi a zavorkami.
- [x] Pridat parser/lexer rozlisujici reference, range, cisla, stringy, operatory, funkce, zavorky a oddelovace argumentu.
- [x] Udrzovat aktivni token podle caret pri `click`, `double click`, `drag`, `selection change` a klavesovych zkratkach.
- [x] Pridat vizualni rozliseni `active reference token` vs `other reference tokens`.
- [x] Pri kliknuti na jinou bunku vymenit nebo vlozit referenci presne podle aktivniho tokenu a caret pravidel.
- [x] Pri drag range umoznit nahradit existujici single ref token range tokenem a naopak.
- [x] Pri kliknuti do aktivni editovane bunky zachovat editor state a neumoznit vlozeni self-reference ani nechteny selection jump.
- [x] Dodelat `ArrowUp` a `ArrowDown` semantiku v editoru tak, aby byla predvidatelna a zdokumentovana.
- [x] Dodelat `Shift+ArrowLeft/Right` pro textovy vyber v editoru bez grid navigation.
- [x] Dodelat `Ctrl+ArrowLeft/Right`, `Home`, `End`, `Ctrl+Backspace`, `Delete` a dalsi bezne textove zkratky v editoru.
- [x] Rozhodnout a zapsat chovani `Enter`, `Shift+Enter`, `Tab` a `Shift+Tab` pri formula editaci.
- [x] Rozhodnout a zapsat chovani `Escape` pri rozpracovanem reference-picking modu.
- [x] Pridat function autocomplete pri psani `=SU...`.
- [x] Pridat seznam navrhu funkci s keyboard navigaci.
- [x] Pridat tooltip/help panel se signaturou funkce a zvyraznenim aktivniho argumentu.
- [x] Pri psani oddelovacu argumentu prubezne prepocitavat index aktivniho argumentu.
- [x] Zvazit locale-aware oddelovac argumentu a desetinna pravidla, nebo vedome zapsat omezeni prvni verze.
- [x] Pridat vizualni indikaci reference-picking mode, aby bylo jasne, kdy klik do gridu meni formuli.
- [x] Pri scrollu, resize a viewport sync drzet formula session stabilni bez ztraty caret a token stavu.
- [x] Pri externi zmene workbooku behem formula editace rozhodnout merge pravidla, nebo zapsat, ze externi sync ceka na commit/cancel.
- [x] Zajistit, aby context menu a dalsi overlaye nenarusily formula session, pokud to neni explicitni akce ukonceni editace.
- [x] Pridat E2E test, ze inline editor a formula bar maji stejne `F4` chovani nad stejnou formulí.
- [x] Pridat E2E test, ze klik/reference picking funguje stejne v inline editoru i ve formula baru.
- [x] Pridat E2E test pro multi-token formuli s klik-nahrazovanim reference uprostred textu.
- [x] Pridat E2E test pro range replacement a drag range uvnitr slozitejsi formule.
- [x] Pridat E2E test pro function autocomplete a potvrzeni funkce z klavesnice.
- [x] Pridat E2E test pro tooltip aktivni funkce a aktivni argument.
- [x] Pridat E2E test, ze textove zkratky editoru nemeni active cell v gridu.
- [x] Pridat E2E test, ze self-click a klik do overlaye nenarusuje formula session.
- [x] Zapsat finalni UX rozhodnuti a vedoma omezeni prvni verze.

Poznamka k rozhodnutim 2026-05-07:
- Formula bar a inline canvas editor zatim nejsou jedna sdilena runtime state machine. Misto toho sdileji stejna caret/token pravidla a JS analyzu formule; to drzi scope rozumne maly a pritom sjednocuje nejdulezitejsi UX chovani.
- `selectionStart` je i ve formula baru anchor pro aktivni token stejne jako v 12.14 u inline editoru.
- Pri vyberu jen casti reference tokenu (`A|1`, `|A1`, `A1|`) reference-picking a klik do gridu nahrazuji cely referencni token, ne jen doslovne vybranou podmnozinu znaku.
- Prvni verze function hintu a autocomplete pouziva locale-agnostic pristup: argumentovy oddelovac umi `,` i `;`, ale neimplementuje plnou lokalni semantiku desetinne carky a oddelovacu.
- Pri rozpracovane formuli v formula baru se `blur` nechova jako finalni commit; session se drzi otevrena, aby reference-picking nebyl neprijemne krehky.
- Function autocomplete se pri caret uvnitr reference tokenu zamerne nevykresluje; reference token ma pri formule prednost pred function-prefix heuristikou, aby klik do gridu nemohl omylem potvrdit navrh funkce.
- `ArrowUp` a `ArrowDown` v prvni verzi behem formula editace neslouzi pro grid navigation. Pokud je otevreny seznam navrhu funkci, naviguji ten seznam; jinak zustava session otevrena a grid se nepohne.
- Klik do stejne bunky behem formula session je overeny. `Contextmenu` gesto nad gridem je ted izolovane take: non-primary pointer gesta dostanou v canvasu kratke blokovaci okno a formula bar navic explicitne synchronizuje `externalFormulaMode` do canvas JS state, aby se reference-picking session nerozbila kratkym blur/re-render mezistavem.
- `Ctrl+ArrowLeft`, `Ctrl+ArrowRight`, `Home`, `End`, `Ctrl+Backspace` a `Delete` jsou overene pro formula bar i inline canvas editor bez zmeny `active cell`.
- `Enter` ve formula baru commitne do puvodni aktivni bunky a posune selection o radek dolu; `Shift+Enter` commitne a posune selection nahoru. `Tab` commitne a posune selection doprava; `Shift+Tab` commitne a posune selection doleva. Navigace probiha az po commitu, aby se hodnota nezapsala do uz nove aktivni bunky.
- `Escape` pri formula editaci zrusi celou rozpracovanou editaci a vrati editor na puvodni display/formula value; pri formula bar session nemeni active cell. V canvas JS enginu je grid context menu behem `formula point mode` potlacene, aby pravy klik nerozbil reference-picking session.
- Pri klik/reference-pickingu na prazdne misto ve formule se reference vklada od aktualni caret pozice bez rozbiti okolni syntaxe; pri nahrazovani uvnitr slozitejsich vyrazu se meni jen aktivni referencni token.
- Lexer v prvni verzi rozlisuje reference, range, cisla, stringy, operatory, funkce, zavorky a oddelovace argumentu. Zamerne zatim neimplementuje plnou semantiku strukturovanych referenci nebo detailni locale-aware parser nad vsemi Excel specialitami.
- Pri externi zmene workbooku behem rozpracovane formula session zustava lokalni editacni text autoritativni az do `commit` nebo `cancel`; externi sync nesmi prepsat rozpracovanou hodnotu v formula baru.

Poznamka k overeni 2026-05-07:
- bUnit prosly: `TmSpreadsheetFormulaBarTests` a `FormulaBar_Tab_CommitsAndFiresTabPressed`.
- E2E prosly: `CanvasJsEngine_FormulaBarF4MatchesInlineEditorSemantics`, `CanvasJsEngine_FormulaBarAutocompleteAcceptsFunctionFromKeyboard`, `CanvasJsEngine_FormulaBarAutocompleteKeyboardSelectionAcceptsHighlightedSuggestion`, `CanvasJsEngine_FormulaBarClickReplacesReferenceAtCaretWithoutChangingActiveCell` a `CanvasJsEngine_FormulaBarShowsActiveFunctionArgumentHint`.
- Dalsi E2E prosly: `CanvasJsEngine_FormulaBarDragRangeReplacesReferenceWithoutChangingActiveCell` a `CanvasJsEngine_FormulaBarSelfClickKeepsSessionAndValue`.
- Dalsi E2E prosly: `CanvasJsEngine_FormulaBarClickReferencePickingMatchesInlineEditorForSameFormula`, `CanvasJsEngine_FormulaBarMixedFormulaClickReplacesOnlyCaretTargetedToken`, `CanvasJsEngine_FormulaBarMixedFormulaDragRangeReplacesOnlyCaretTargetedToken` a `CanvasJsEngine_FormulaBarSelectionShortcutsDoNotChangeActiveCell`.
- Dalsi E2E prosly: `CanvasJsEngine_FormulaBarAdvancedWordNavigationShortcutsKeepActiveCell`, `CanvasJsEngine_FormulaBarDeleteAndCtrlBackspaceEditTextWithoutChangingActiveCell`, `CanvasJsEngine_InlineFormulaEditorAdvancedWordNavigationShortcutsKeepActiveCell` a `CanvasJsEngine_InlineFormulaEditorDeleteAndCtrlBackspaceEditTextWithoutChangingActiveCell`.
- Dalsi E2E prosly: `CanvasJsEngine_FormulaBarSelfClickKeepsSessionAndValue` a `CanvasJsEngine_FormulaBarContextMenuAttemptKeepsSessionAndDoesNotOpenGridMenu`.
- bUnit prosly: `Spreadsheet_FormulaBarEnter_CommitsValueAndMovesActiveCellDown` a `Spreadsheet_FormulaBarShiftTab_CommitsValueAndMovesActiveCellLeft`.
- bUnit prosel: `Spreadsheet_FormulaBarEscapeCancelsEditAndKeepsActiveCell`.
- Dalsi bUnit prosel: `DisplayValueChange_DuringEditing_DoesNotOverwriteLocalFormulaSession`.
- Dalsi E2E prosly: `CanvasJsEngine_FormulaBarClickIntoEmptyFunctionArgumentInsertsReferenceWithoutBreakingSyntax`, `CanvasJsEngine_InlineFormulaEditorComplexFormulaReplacementIgnoresStringLiteralReferences`, `CanvasJsEngine_FormulaBarComplexFormulaReplacementIgnoresStringLiteralReferences`, `CanvasJsEngine_FormulaBarSessionKeepsCaretAndActiveCellDuringViewportScroll`, `CanvasJsEngine_InlineFormulaEditorDoubleClickSelectionRefreshesActiveToken` a `CanvasJsEngine_FormulaBarDoubleClickSelectionRefreshesActiveToken`.
- Faze 12.15 je funkcne zavrena. Vedome zustava jen architektonicke omezeni, ze formula bar a inline canvas editor zatim nejsou jedna sdilena runtime state machine; to je navazujici tema pro 12.16+, ne nehotovy bod teto faze.

### 12.16 Sjednoceni formula editor runtime a dodelani pokrocile semantiky

- [x] Navrhnout cilovy model, ve kterem formula bar a inline canvas editor pouzivaji jednu sdilenou `formula editing session` runtime vrstvu misto dvou paralelnich implementaci.
- [x] Zmapovat, ktere casti dnes zustavaji duplikovane mezi `spreadsheet.js`, `spreadsheet-canvas.js`, `TmSpreadsheetFormulaBar` a parent `TmSpreadsheet`.
- [x] Rozhodnout, zda bude sdileny runtime vlastneny primarne JS vrstvou, nebo zda zustane C# shell s tenkou JS exekuci.
- [x] Vytvorit jeden sdileny session controller pro text, caret, selection, parsed tokeny, autocomplete state, function hint state a reference-picking mode.
- [x] Prevest inline canvas editor na tento sdileny session controller bez zmeny stavajiciho UX a hot path vykonu.
- [x] Prevest formula bar na stejny session controller bez ztraty klavesove ergonomie a bez regressi v commit/navigation pravidlech.
- [x] Zajistit, aby prepinani mezi formula barem a inline editorem udrzelo stejnou session, caret, selection, aktivni token i autocomplete state.
- [x] Rozhodnout a implementovat, zda ma byt mozne plynule presouvat rozpracovanou formula session mezi inline editorem a formula barem bez `cancel`/`commit`.
- [x] Zajistit, aby scroll, resize, viewport sync a overlaye uz nevyzadovaly special-case synchronizaci pro dva ruzne editory.
- [x] Dodelat plnou paritu `ArrowUp` a `ArrowDown` semantiky mezi formula barem, inline editorem a pripadnym budouci vice-radkovym editorem.
- [x] Implementovat `Shift+ArrowUp/Down`, `Ctrl+Shift+Arrow`, `PageUp/PageDown` a dalsi rozsahlejsi textove zkratky tak, aby byly predvidatelne a nehybaly gridem.
- [x] Rozsirit lexer/parser o vedome odlozene okraje: detailnejsi locale-aware pravidla pro desetinnou carku, oddelovace argumentu a dalsi Excel-like syntaxe.
- [x] Rozhodnout a zapsat, zda chceme podporit strukturovane reference, jmenovane oblasti a dalsi pokrocile typy tokenu; pokud ano, rozdelit je na mensi implementacni kroky.
- [x] Zajistit, aby function autocomplete, hinty argumentu a reference-picking byly skutecne jedna logika se stejnymi pravidly v obou editorech, ne jen dve sladene implementace.
- [x] Pridat E2E test, ze rozpracovanou formula session lze bez ztraty stavu prepnout mezi formula barem a inline editorem, pokud to bude podporovany scenar.
- [x] Pridat E2E testy pro dlouhou rozpracovanou session kombinujici autocomplete, klik-reference-picking, drag range, scroll a nasledny commit.
- [x] Pridat E2E testy pro vsechny pokrocile klavesove zkratky, ktere budou v teto fazi finalne podporene.
- [x] Pokud zustanou vedoma omezeni i po 12.16, prepsat je na explicitni rozhodnuti v poznamce misto neurciteho "zatim".

Poznamka k rozhodnutim 2026-05-08:
- Cilem 12.16 neni jedna obri C# state machine. Sdileny runtime je vlastneny primarne JS vrstvou (`tmSpreadsheetFormulaRuntime`) a C# komponenty vystupuji jako shell pro commit/navigation a UI host.
- `spreadsheet.js` je autoritativni misto pro analyzu formule, aktivni token, reference replacement, `F4`, function autocomplete a function hinty. `spreadsheet-canvas.js` a `TmSpreadsheetFormulaBar` nad tim drzi jen editor-specific view shell.
- Bidirectional transfer rozpracovane session je podporeny bez `cancel`/`commit`: inline editor -> formula bar klikem do konkretniho formula baru daneho spreadsheetu, formula bar -> inline editor klavesou `F2`.
- `double click` z formula baru do inline editoru neni v 12.16 garantovany scenar; podporovany a otestovany transfer gesture je `F2`, protoze je stabilnejsi a spreadsheet-like.
- Host session na `.tm-spreadsheet` drzi text, caret a selection i pri prechodu fokusu mezi formula barem, gridem a inline editorem; pokud chybi explicitne ulozena session, runtime umi nouzove precist zivy formula bar nebo zivy inline editor.
- Formula bar pri blur uz umi vedome pustit fokus na canvas grid nebo inline editor bez nechteneho "prilepeni" fokusu zpet do formula baru.
- Special-case synchronizace mezi dvema editory pro scroll, resize, viewport sync a overlaye je v 12.16 uz odstranena z hot path: canvas JS engine cte external formula session primo z host runtime a tyto interakce uz nepotrebuji zvlastni `.NET` push editovane hodnoty. Pro host handoff stale existuji lehke adaptery v `TmSpreadsheet`, `TmSpreadsheetFormulaBar` a `spreadsheet-canvas.js`; to je vedome ponechany architektonicky zbytek mimo hot path, ne skryta chyba.
- Canvas JS engine uz pri external formula session neceka na prubezny `.NET` push celeho `ExternalFormulaEditValue`. Formula bar pri psani synchronne uklada host session do sdileneho JS runtime a canvas z ni sam cte formula text, caret i reference highlighty. Tím odpadla jedna cela synchronizacni vetev pro scroll/resize/overlay pripady; vedome zustava jen tenka `.NET` signalizace `ExternalFormulaSessionActive` pro callbackove guardy.
- Non-primary pointer gesta nad gridem behem formula bar session jsou v canvasu ted potlacena pres stejny `suppressClick` / `nonPrimaryGestureUntil` guard jako dalsi potlacene pointer cesty, aby `contextmenu` nemohlo obejit JS session ochrany a propadnout do .NET otevreni grid menu.
- Rozsirene textove zkratky jsou v 12.16 finalne podporene takto: `Shift+ArrowUp/Down`, `Ctrl+Shift+ArrowLeft/Right`, `PageUp` a `PageDown` nesmi pohnout gridem ani rozbit session. U inline editoru `PageUp/PageDown` JS explicitne potlacuje nativni scroll, aby editor nezmizel z viewportu.
- `ArrowUp` a `ArrowDown` maji v obou jednoradkovych editorech stejnou semantiku: pokud je otevreny autocomplete seznam, naviguji seznam navrhu; jinak nespousteji grid navigation, nemeni active cell a nechavaji caret/session stabilni. Pripadny budouci vice-radkovy editor zustava samostatne rozhodnuti, ale pro soucasne podporovane editory je parita splnena a overena.
- Sdileny JS runtime ted cte locale z host `.tm-spreadsheet`: v kulturach s desetinnou carkou pouziva `,` jako decimal separator a `;` jako preferovany oddelovac argumentu. Zaroven zustava tolerantni k alternativnimu oddelovaci, pokud zrovna netvori decimalni cislo.
- Strukturovane reference a jmenovane oblasti jsme v 12.16 vedome neimplementovali. Rozhodnuti je explicitni: soucasny runtime zustava autoritativni pro A1 reference, range tokeny a bezne funkce; structured refs a named ranges patri az do samostatne navazujici faze, protoze by zasahly lexer, token semantiku i reference-picking pravidla.

Poznamka k overeni 2026-05-08:
- E2E prosly proti `https` demo profilu: `CanvasJsEngine_FormulaSessionTransfersFromInlineEditorToFormulaBarWithoutLosingCaret` a `CanvasJsEngine_F2TransfersFormulaSessionFromFormulaBarToInlineEditorWithoutLosingCaret`.
- Dalsi drive pridane a znovu pouzite session runtime overeni zustavaji relevantni: `CanvasJsEngine_InlineFormulaEditorAutocompleteAcceptsFunctionFromKeyboard` a `CanvasJsEngine_InlineFormulaEditorShowsSharedFunctionHint`.
- Dalsi E2E prosly proti `https` demo profilu: `CanvasJsEngine_FormulaBarExtendedRangeShortcutsKeepSessionAndDoNotChangeActiveCell`, `CanvasJsEngine_InlineFormulaEditorExtendedRangeShortcutsKeepSessionAndDoNotChangeActiveCell` a `CanvasJsEngine_FormulaBarLongSessionCombinesAutocompleteReferencePickingScrollAndCommit`.
- Dalsi E2E prosly proti `https` demo profilu: `CanvasJsEngine_FormulaBarArrowUpDownKeepSessionAndDoNotChangeActiveCell` a `CanvasJsEngine_InlineFormulaEditorArrowUpDownKeepSessionAndDoNotChangeActiveCell`.
- Dalsi E2E prosla proti `https` demo profilu: `CanvasJsEngine_FormulaBarContextMenuAttemptKeepsSessionAndDoesNotOpenGridMenu`.
- Dalsi E2E prosly proti `https` demo profilu: `CanvasJsEngine_FormulaBarCzechDecimalCommaDoesNotAdvanceArgumentHintPrematurely` a `CanvasJsEngine_InlineFormulaEditorCzechDecimalCommaDoesNotAdvanceArgumentHintPrematurely`.
- Dalsi E2E prosly proti `https` demo profilu i po presunu external formula session pod host runtime: `CanvasJsEngine_FormulaSessionTransfersFromInlineEditorToFormulaBarWithoutLosingCaret`, `CanvasJsEngine_F2TransfersFormulaSessionFromFormulaBarToInlineEditorWithoutLosingCaret`, `CanvasJsEngine_FormulaBarLongSessionCombinesAutocompleteReferencePickingScrollAndCommit`, `CanvasJsEngine_FormulaBarContextMenuAttemptKeepsSessionAndDoesNotOpenGridMenu`, `CanvasJsEngine_FormulaBarCzechDecimalCommaDoesNotAdvanceArgumentHintPrematurely` a `CanvasJsEngine_InlineFormulaEditorCzechDecimalCommaDoesNotAdvanceArgumentHintPrematurely`.
- Dalsi E2E prosly proti `https` demo profilu pro zavreni posledniho hot-path checkboxu: `CanvasJsEngine_FormulaBarSessionKeepsCaretAndActiveCellDuringViewportScroll`, `CanvasJsEngine_FormulaBarSessionKeepsCaretAndActiveCellDuringResizeCommit` a `CanvasJsEngine_FormulaBarContextMenuAttemptKeepsSessionAndDoesNotOpenGridMenu`.

## Faze 13 - Dokumentace a verejne API

- [ ] Pridat XML dokumentaci k novym parametrum.
- [ ] Pridat parametr pro render mode.
- [ ] Pridat parametr pro overscan radku.
- [ ] Pridat parametr pro overscan sloupcu.
- [ ] Pridat parametr pro zapnuti benchmark/debug overlaye, pokud bude potreba.
- [ ] Aktualizovat demo stranku Spreadsheet.
- [ ] Pridat ukazku DOM rendereru.
- [ ] Pridat ukazku canvas rendereru.
- [ ] Popsat kdy zvolit DOM renderer.
- [ ] Popsat kdy zvolit canvas renderer.
- [ ] Popsat znamy rozdil v pristupnosti.
- [ ] Popsat znamy rozdil v moznostech stylovani.
- [ ] Doplnit changelog nebo release notes, pokud projekt pouziva changelog.

## Pravidla pro odskrtavani

- [ ] Odskrtnout jen krok, ktery je implementovany a overeny.
- [ ] Pokud se krok ukaze jako zbytecny, prepsat ho na rozhodnuti s kratkou poznamkou.
- [ ] Pokud krok naroste, rozdelit ho na mensi kroky.
- [ ] Pri kazde fazi doplnit test nebo explicitni duvod, proc test nedava smysl.
- [ ] Pri zmene verejneho API doplnit XML dokumentaci.
