# TmDocumentEditor - implementační TODO pro obrázky a obtékání textu

Datum založení: 2026-05-21  
Navazuje na: `planning/tmdocumenteditor-image-wrapping-word-google-docs-analysis-2026-05-21.md`  
Režim práce: TDD + průběžné human-like E2E testy  
Stav: plán pro implementaci, při práci odškrtávat dokončené body

## Důležité rozhodnutí

Editor je pořád ve vývoji, proto neřešíme zpětnou kompatibilitu ani migraci starých dokumentů. Můžeme upravit model, serializaci, runtime DOM a demo data čistě podle cílového návrhu.

Jediná povinná datová úprava mimo samotnou implementaci:

- [ ] Upravit demo dokumenty tak, aby používaly nový model obrázků, ukotvení a obtékání.
- [ ] Odstranit z demo dat staré sidecar/float workaroundy.
- [ ] Ověřit, že demo po reloadu ukazuje obrázky ve stejném layoutu jako před uložením.

## Hlavní principy implementace

- [ ] Nové obtékání nesmí vytvářet `tm-wysiwyg-image-sidecar-text`.
- [ ] Testy nesmí ověřovat existenci sidecar workaroundu jako cílové chování.
- [ ] Obrázek musí být objekt v layout enginu, ne DOM element spoléhající na browser `float`.
- [ ] Text vedle obrázku musí být normální textový tok s normálním caret a editing chováním.
- [ ] Každá uživatelská akce musí jít přes command/runtime API, ne přes izolovanou DOM mutaci.
- [ ] E2E testy musí akce provádět jako člověk: klik, drag, klávesnice, kontextové menu, inspector.
- [ ] Interní JS v E2E smí sloužit pro měření a snapshot, ne jako náhrada uživatelské akce.
- [ ] Každá perzistentní změna musí projít save/reload ověřením.
- [ ] Každá fáze musí mít RED testy před hlavní implementací.
- [ ] Po dokončení fáze aktualizovat tento TODO soubor a odškrtnout skutečně hotové body.

## Definition of Done pro každou fázi

- [ ] Unit testy pokrývají nový model nebo výpočet.
- [ ] E2E testy pokrývají alespoň jeden reálný uživatelský scénář.
- [ ] Testy kontrolují obsah, caret/selection, vizuální geometrii, toolbar/inspector stav a save/reload tam, kde to dává smysl.
- [ ] Nezůstává staré floating UI nebo kontextové menu.
- [ ] Nezůstává stale selection na obrázku po kliknutí do textu.
- [ ] Debug artifact při E2E selhání obsahuje screenshot, DOM snapshot, layout snapshot a selection snapshot.
- [ ] Demo stránka je ručně krátce ověřená na `https://localhost:7106/document-editor`.

## Fáze 0: Startovní RED testy a inventura současného chování

Stav fáze 0: hotovo jako RED testovací a inventurní fáze. Testy popisují cílové chování bez sidecar workaroundu a budou zelené až po dalších fázích layout enginu.

### 0.1 Přidat cílové failing E2E testy pro reportované video

- [x] Přidat test `DocumentEditor_Strict_ImageWrap_ClickSecondLineBesideLeftImagePlacesCaretThere`.
- [x] Připravit izolovaný dokument s jedním levým square-wrapped obrázkem a textem vedle něj na alespoň 3 řádky.
- [x] V testu kliknout myší na první řádek textu vedle obrázku.
- [x] Ověřit, že caret je na prvním řádku, ne na obrázku.
- [x] Kliknout myší na druhý řádek textu vedle obrázku.
- [x] Ověřit, že caret je na druhém řádku.
- [x] Kliknout myší na třetí řádek textu vedle obrázku.
- [x] Ověřit, že caret je na třetím řádku.
- [x] Ověřit, že image toolbar není viditelný.
- [x] Ověřit, že image inspector není aktivní, pokud pravý panel nemá zůstat na obrázku.
- [x] Ověřit, že další psaní vloží text přesně do kliknutého řádku.
- [x] Ověřit, že model textu obsahuje vložený text na správném offsetu.

### 0.2 Přidat failing E2E test pro Backspace vedle obrázku

- [x] Přidat test `DocumentEditor_Strict_ImageWrap_BackspaceAtWrappedLineStartEditsTextNormally`.
- [x] Připravit odstavec obtékající levý obrázek.
- [x] Umístit caret na začátek druhého vizuálního řádku vedle obrázku.
- [x] Stisknout Backspace.
- [x] Ověřit, že se smaže předchozí znak nebo se caret přesune podle normální editace textu.
- [x] Ověřit, že obrázek není vybraný.
- [x] Ověřit, že obrázek není smazaný.
- [x] Ověřit, že nevznikl prázdný sidecar odstavec.
- [x] Ověřit, že undo vrátí stav přesně zpět.
- [x] Ověřit, že redo znovu provede stejnou změnu.

### 0.3 Přidat failing E2E test pro live reflow při posunu obrázku

- [x] Přidat test `DocumentEditor_Strict_ImageWrap_DragImageReflowsAdjacentText`.
- [x] Připravit text, který obtéká obrázek.
- [x] Zachytit `BeforeLayoutSnapshot` řádků a image rectu.
- [x] Táhnout obrázek myší dolů a doprava.
- [x] Během drag ověřit preview nebo throttlovaný layout update.
- [x] Po puštění ověřit, že image rect se změnil.
- [x] Ověřit, že alespoň jeden line box dotčeného textu změnil šířku nebo pozici.
- [x] Ověřit, že žádný textový rect neprotíná image wrap rect.
- [x] Ověřit, že obsah textu zůstal stejný.
- [x] Ověřit save/reload zachování pozice a reflow.

### 0.4 Přidat failing E2E test pro resize a reflow

- [x] Přidat test `DocumentEditor_Strict_ImageWrap_ResizeImageReflowsAdjacentText`.
- [x] Vybrat obrázek.
- [x] Táhnout pravý dolní resize handle.
- [x] Ověřit změnu rozměru.
- [x] Ověřit zachování aspect ratio pro rohový handle.
- [x] Ověřit změnu line boxů okolního textu.
- [x] Ověřit, že inspector ukazuje novou šířku/výšku.
- [x] Ověřit save/reload.

### 0.5 Přidat do debug snapshotu layout informace

- [x] Rozšířit E2E debug artifact o `ImageLayoutSnapshot`.
- [x] Snapshot musí obsahovat image id, visual rect, wrap rect, selection rect.
- [x] Snapshot musí obsahovat wrap mode, anchor id, position, z-index.
- [x] Snapshot musí obsahovat text line boxes v okolí obrázku.
- [x] Snapshot musí obsahovat seznam rect průniků text vs image wrap rect.
- [x] Snapshot musí obsahovat aktivní floating UI a side panel stav.
- [x] Snapshot musí obsahovat informaci, jestli existuje starý sidecar element.

### 0.6 Inventura starého kódu

- [x] Sepsat seznam funkcí v `document-editor-wysiwyg.js`, které vytváří nebo používají sidecar text.
- [x] Sepsat seznam CSS tříd pro staré float obtékání.
- [x] Sepsat seznam E2E helperů, které předpokládají `data-wrap-sidecar-for`.
- [x] Označit testy, které bude nutné přepsat na nový layout snapshot.
- [x] Označit demo data, která obsahují obrázky s aktuálním `FloatingLayout`.

#### Inventura 2026-05-21

`document-editor-wysiwyg.js` - sidecar runtime dluh:

- `_createWrappedImageSideTextBlockModel` vytváří modelový paragraph pro text vedle obrázku a používá `Order = imageOrder + 0.1`.
- `_isWrappedImageSideTextBlock` rozpoznává `p.tm-wysiwyg-block[data-wrap-sidecar-for]` a `p.tm-wysiwyg-image-sidecar-text`.
- `_ensureWrappedImageSideTextBlock` vytváří nebo recykluje sidecar DOM blok, nastavuje `data-wrap-sidecar-for`, `tm-wysiwyg-image-sidecar-text` a transakce `txn-sidecar-*`.
- `_findWrappedImageSideTextBlockAtPoint` hit-testuje oblast vedle obrázku a místo nativního textového toku směruje klik do sidecar bloku.
- `_focusWrappedImageSideTextBlock` přenáší fokus/caret do sidecar odstavce; loguje `sidecar.focus.*`.
- Pointer/click flow kolem `pointerdown.sidecar-handled` používá sidecar hit-test jako speciální cestu pro klik vpravo nebo vlevo od obrázku.
- Command flow po změně wrap/pozice volá `_ensureWrappedImageSideTextBlock`, takže starý workaround vzniká i po uživatelské změně layoutu.

CSS starého float/sidecar řešení:

- `.tm-document-image--wrap-square` a varianty pro left/right používají browser `float` a `shape-outside`.
- `.tm-wysiwyg-image--wrap-square`, `.tm-wysiwyg-image--wrap-square-left`, `.tm-wysiwyg-image--wrap-square-right`, `.tm-wysiwyg-image--wrap-square-center` nastavují staré float chování ve WYSIWYG DOM.
- `.tm-wysiwyg-image-sidecar-text` styluje falešný textový blok vedle obrázku.
- Responsive fallback resetuje float pro malé viewporty, což je další zdroj rozdílu mezi layoutem a modelem.

E2E helpery a testy k přepsání:

- `TypeTextBesideWrappedImageAsync`, `AssertWrappedImageCaretBesideImageAsync`, `AssertSelectionInsideWrappedImageSideTextAsync` a `AssertWrappedImageSideTextAsync` dnes předpokládají `data-wrap-sidecar-for`.
- Testy kolem phase 7/10 a starých image wrap scénářů kontrolují existenci `p.tm-wysiwyg-image-sidecar-text[data-wrap-sidecar-for=...]`; ve fázi 17 musí být přepsané na line-box/layout snapshot.
- Nové phase 0 testy naopak explicitně očekávají `SidecarCount == 0` a tím zamykají cílový směr.

Demo data s aktuálním `FloatingLayout`:

- `src/Tempo.Blazor.Demo.Api/Services/DemoDocumentEditorStore.cs` generuje v `contract-demo` první i druhý image block přes `CreateLeftWrappedImageLayout()`.
- `src/Tempo.Blazor.Demo.SharedUI/Services/DemoDocumentEditorProvider.cs` obsahuje další demo image bloky, které je nutné projít při přechodu na nový model.
- Demo dokumenty nesmí po implementaci nového modelu obsahovat sidecar paragraph ani text, který existuje jen kvůli workaroundu.

## Fáze 1: Nový model obrázku a layoutu bez migrací

Stav fáze 1: hotovo. Kanonický model je `ImageBlockContent.Layout`; `FloatingLayout` zůstává pouze jako neperzistentní C# shim pro přechodové testy a bude odstraněn ve fázi 17 spolu se starým sidecar/float řešením.

### 1.1 Navrhnout nový model

- [x] Vytvořit nebo upravit model na `DocumentObjectLayout`.
- [x] Rozdělit layout na části `Anchor`, `Position`, `Wrap`, `Transform`, `Stacking`.
- [x] Přidat enum `DocumentObjectLayoutKind`.
- [x] Přidat hodnoty `Inline`, `Anchored`, `Fixed`.
- [x] Přidat `MoveWithText`.
- [x] Přidat `FixedOnPage`.
- [x] Přidat `LockAnchor`.
- [x] Přidat `AllowOverlap`.
- [x] Přidat `Rotation`.
- [x] Přidat `Crop`.
- [x] Přidat `WrapContourPoints`.
- [x] Přidat `DistanceLeft`, `DistanceRight`, `DistanceTop`, `DistanceBottom`.
- [x] Přidat `HorizontalRelativeTo`.
- [x] Přidat `VerticalRelativeTo`.
- [x] Přidat `HorizontalAlignment`.
- [x] Přidat `VerticalAlignment`.
- [x] Přidat `X`, `Y`.
- [x] Přidat `ZIndex`.

### 1.2 Upravit `ImageBlockContent`

- [x] Přidat novou vlastnost `Layout`.
- [x] Rozhodnout, jestli `FloatingLayout` odstranit nebo nahradit.
- [x] Pokud odstraníme `FloatingLayout`, upravit všechny compile chyby v jednom commitu/fázi.
- [x] Přesunout velikost obrázku do `Layout.Transform` nebo zachovat `Size` jako zdrojovou velikost.
- [x] Rozlišit `NaturalSize` a uživatelsky nastavenou velikost.
- [x] Doplnit XML dokumentaci ke všem novým public vlastnostem.
- [x] Přidat unit test serializace nového modelu.
- [x] Přidat unit test default hodnot.
- [x] Přidat unit test pro inline obrázek.
- [x] Přidat unit test pro anchored square obrázek.
- [x] Přidat unit test pro fixed behind-text obrázek.

### 1.3 Odstranit potřebu zpětné kompatibility

- [x] Odstranit compatibility větve pro staré `FloatingLayout`, pokud už nebudou potřeba.
- [x] Odstranit nebo ignorovat starý `PreservedWrapMode`, pokud ztrácí smysl.
- [x] Upravit serializers tak, aby zapisovaly pouze nový model.
- [x] Upravit demo API/model factory tak, aby generovalo nový model.
- [x] Upravit testovací helpery pro vkládání obrázků na nový model.

### 1.4 Demo dokumenty

- [x] Najít demo dokument `contract-demo`.
- [x] Upravit první demo obrázek na anchored square left.
- [x] Upravit druhý demo obrázek tak, aby nekolidoval se sidecar workaroundem.
- [x] Přidat do demo dokumentu ukázkový delší text obtékající první obrázek.
- [x] Přidat do demo dokumentu ukázku top/bottom obrázku.
- [x] Přidat do demo dokumentu ukázku behind text nebo in front of text, pokud UI už bude připravené.
- [x] Ověřit, že demo data neobsahují sidecar paragraph.

## Fáze 2: Layout primitives a výpočet rozměrů

### 2.1 Přidat layout datové typy

- [x] Přidat `DocumentPageLayoutSnapshot`.
- [x] Přidat `DocumentPageLayoutBox`.
- [x] Přidat `DocumentParagraphLayoutBox`.
- [x] Přidat `DocumentLineBox`.
- [x] Přidat `DocumentTextSegmentBox`.
- [x] Přidat `DocumentObjectLayoutBox`.
- [x] Přidat `DocumentExclusionZone`.
- [x] Přidat `DocumentCaretPosition`.
- [x] Přidat `DocumentLayoutHitTarget`.
- [x] Přidat serializovatelnou debug variantu layout snapshotu.

### 2.2 Geometrie

- [x] Přidat helper pro rect intersection.
- [x] Přidat helper pro union rectů.
- [x] Přidat helper pro clamp do page body.
- [x] Přidat helper pro převod relativních pozic na absolutní page souřadnice.
- [x] Přidat helper pro výpočet wrap rectu včetně distance.
- [x] Přidat helper pro z-index ordering.
- [x] Přidat unit testy pro každý geometry helper.

### 2.3 Exclusion zones

- [x] Přidat výpočet square exclusion zone.
- [x] Přidat výpočet top/bottom exclusion zone.
- [x] Přidat výpočet behind text bez exclusion zone.
- [x] Přidat výpočet in front of text bez exclusion zone.
- [x] Přidat placeholder pro tight contour.
- [x] Přidat placeholder pro through contour.
- [x] Přidat unit test: square left blokuje levý obdélník.
- [x] Přidat unit test: square right blokuje pravý obdélník.
- [x] Přidat unit test: top/bottom blokuje plnou šířku řádků v dané výšce.
- [x] Přidat unit test: behind text neblokuje text.
- [x] Přidat unit test: in front of text neblokuje text.

### 2.4 Dostupné intervaly pro řádek

- [x] Přidat funkci `GetAvailableLineIntervals(y, lineHeight, exclusions)`.
- [x] Vrátit jeden interval, pokud žádný obrázek řádek neprotíná.
- [x] Vrátit pravý interval u levého square obrázku.
- [x] Vrátit levý interval u pravého square obrázku.
- [x] Vrátit dva intervaly u obrázku uprostřed.
- [x] Vrátit prázdný interval u top/bottom obrázku přes celou šířku.
- [x] Přidat unit testy pro každý případ.

Poznámka k fázi 2: `GetAvailableLineIntervals` má v implementaci navíc `lineBounds`, aby bylo explicitní, vůči jaké šířce stránky/sloupce se intervaly počítají.

## Fáze 3: Layout engine pro text okolo obrázků

### 3.1 Vytvořit `DocumentLayoutEngine`

- [x] Vytvořit službu/třídu pro layout stránky.
- [x] Vstupem bude dokument, page settings a renderer metrics.
- [x] Výstupem bude `DocumentPageLayoutSnapshot`.
- [x] Přidat unit test pro prázdný dokument.
- [x] Přidat unit test pro jeden odstavec bez obrázku.
- [x] Přidat unit test pro jeden inline obrázek.
- [x] Přidat unit test pro anchored square obrázek.

### 3.2 Měření textu

- [x] Rozhodnout, kde bude probíhat přesné měření textu: JS runtime vs aproximace v .NET.
- [x] Pro E2E a browser realitu použít JS text measurement.
- [x] Přidat JS helper pro měření text runů.
- [x] Přidat cache podle font family, size, weight, style a textu.
- [x] Přidat invalidaci cache při zoom/font změně.
- [x] Přidat test, že měření vrací nenulovou šířku.
- [x] Přidat test pro italic/bold rozdíl, pokud to runtime dovolí spolehlivě.

### 3.3 Řádkování odstavce

- [x] Rozdělit paragraph inlines na měřitelné runs.
- [x] Vypočítat line boxes podle dostupných intervalů.
- [x] Podporovat text před obrázkem.
- [x] Podporovat text vedle obrázku.
- [x] Podporovat text pod obrázkem.
- [x] Podporovat dlouhá slova a wrap.
- [x] Podporovat explicitní line break.
- [x] Podporovat line spacing.
- [x] Podporovat paragraph spacing before/after.
- [x] Přidat unit test pro 3 řádky vedle levého obrázku.
- [x] Přidat unit test pro přesun obrázku níž a změnu line boxes.
- [x] Přidat unit test pro zvětšení obrázku a změnu line boxes.

### 3.4 Vícestránkový layout

- [x] Rozhodnout pravidlo pro objekt, který přesahuje stránku.
- [x] Pokud anchored objekt vychází mimo page body, clampnout nebo zobrazit overflow warning.
- [x] Rozdělit text na další stránku podle page body height.
- [x] Zajistit, že object anchor zůstane na správné stránce.
- [x] Přidat unit test pro text přetékající na druhou stránku.
- [x] Přidat unit test pro obrázek v dolní části stránky.
- [x] Přidat unit test pro top/bottom obrázek na hraně stránky.

Poznámka k fázi 3: `DocumentLayoutEngine` zatím používá deterministickou .NET aproximaci měření textu, aby šly stabilně testovat serverové a unit scénáře. Pro browser/E2E realitu je připravený JS helper `measureTextRun`, který měří přes canvas, cacheuje podle textu, fontu a zoomu a v prostředí bez canvasu padá na stejný aproximovaný model.

## Fáze 4: Renderování z layout snapshotu

### 4.1 Upravit WYSIWYG render tree

- [x] Přestat renderovat square wrapping přes `float`.
- [x] Přestat vytvářet sidecar odstavec pro text vedle obrázku.
- [x] Renderovat obrázky do objektové vrstvy podle layout snapshotu.
- [x] Renderovat textové řádky podle line boxes.
- [x] Přidat `data-layout-line-id`.
- [x] Přidat `data-layout-segment-id`.
- [x] Přidat `data-layout-object-id`.
- [x] Přidat `data-wrap-mode`.
- [x] Přidat `data-anchor-block-id`.
- [x] Přidat `data-object-z-index`.

### 4.2 Vrstvy stránky

- [x] Přidat body text layer.
- [x] Přidat behind text layer.
- [x] Přidat object layer.
- [x] Přidat in front of text layer.
- [x] Přidat selection overlay layer.
- [x] Přidat guide overlay layer.
- [x] Zajistit správné pointer-events pro každou vrstvu.
- [x] Ověřit, že text nad behind-text obrázkem je editovatelný.
- [x] Ověřit, že in-front obrázek lze vybrat a táhnout.

### 4.3 CSS cleanup

- [x] Odstranit cílové použití `.tm-wysiwyg-image-sidecar-text`.
- [x] Odstranit nebo deaktivovat `float` pro nové wrapped obrázky.
- [x] Přidat CSS pro absolute layout object boxes.
- [x] Přidat CSS pro selection box.
- [x] Přidat CSS pro 8 handles.
- [x] Přidat CSS pro rotation handle.
- [x] Přidat CSS pro anchor glyph.
- [x] Přidat CSS pro layout bubble.
- [x] Přidat CSS pro guide lines.
- [x] Přidat responzivní pravidla pro malé viewporty.

### 4.4 Render testy

- [x] Render-gate test ověří, že square image nemá `float`.
- [x] Render-gate test ověří, že nevzniká `data-wrap-sidecar-for`.
- [x] Render-gate test ověří, že textové recty jsou vedle obrázku.
- [x] Render-gate test ověří, že textové recty neprotínají wrap rect.
- [x] Render-gate test ověří, že obrázek má stabilní visual rect pro následný hit-test.
- [x] Render-gate test ověří, že textový rect je oddělený od image wrap rectu pro následný hit-test.

Poznámka 2026-05-21: Fáze 4 je zamčená přes nové JS/CSS render-gate testy `DocumentEditorLayoutPhase4RenderTests`. Plné Playwright klikací scénáře pro hit-test, caret mapping a přímou manipulaci se rozpracují ve fázi 5, kde vzniká `DocumentHitTestService`.

## Fáze 5: Hit-testing a caret map

### 5.1 Jednotný hit-test service

- [x] Vytvořit JS modul `DocumentHitTestService`.
- [x] Přidat metodu `hitTest(clientX, clientY)`.
- [x] Rozlišit `TextCaret`.
- [x] Rozlišit `ImageObject`.
- [x] Rozlišit `ImageResizeHandle`.
- [x] Rozlišit `ImageRotateHandle`.
- [x] Rozlišit `ImageLayoutBubble`.
- [x] Rozlišit `PageMargin`.
- [x] Rozlišit `HeaderFooter`.
- [x] Rozlišit `TableCell`.
- [x] Vrátit `None`, pokud klik není relevantní.

### 5.2 Caret from point

- [x] Implementovat mapování bodu na line box.
- [x] Najít nejbližší text segment v line boxu.
- [x] Spočítat nejbližší offset podle x souřadnice.
- [x] Klik vpravo od posledního znaku řádku mapovat na konec řádku.
- [x] Klik vlevo od prvního znaku řádku mapovat na začátek řádku.
- [x] Klik do mezery mezi image wrap rectem a textem mapovat na nejbližší caret v řádku.
- [x] Klik do image wrap rectu, ale mimo visual rect, nesmí vybrat obrázek.
- [x] Přidat test pro první řádek vedle obrázku.
- [x] Přidat test pro druhý řádek vedle obrázku.
- [x] Přidat test pro třetí řádek vedle obrázku.

### 5.3 Object hit-test

- [x] Objekt vybrat pouze při kliknutí na visual rect, caption, selection box nebo handle.
- [x] Klik daleko vpravo od levého obrázku nesmí vybrat obrázek.
- [x] Klik do textu obtékajícího obrázek nesmí vybrat obrázek.
- [x] Klik na druhý obrázek vedle prvního musí vybrat druhý obrázek.
- [x] Zohlednit z-index.
- [x] Zohlednit in-front objekty.
- [x] Zohlednit behind-text objekty a selection pane.

### 5.4 Selection snapshot

- [x] Rozšířit selection snapshot o `LayoutLineId`.
- [x] Rozšířit selection snapshot o `LayoutSegmentId`.
- [x] Rozšířit selection snapshot o `VisualLineIndex`.
- [x] Rozšířit selection snapshot o `ActiveObjectId`.
- [x] Rozšířit selection snapshot o `HitTargetKind`.
- [x] Ověřit sync do Blazor shellu.

Poznámka 2026-05-21: Fáze 5 je pokrytá novým testem `DocumentEditorLayoutPhase5HitTestTests`, který ověřuje čistou geometrii hit-testu pro toolbar/handle cíle, page margin, header/footer, table cell, caret offsety na prvních třech řádcích vedle obrázku, klik do mezery mezi wrap rectem a textem, nevybrání obrázku mimo visual rect, z-index, in-front/behind-text chování a synchronizaci nových polí selection snapshotu do Blazor shellu.

## Fáze 6: Základní editace textu vedle obrázku

### 6.1 Insert text

- [x] Psaní na prvním řádku vedle obrázku vloží text do správného offsetu.
- [x] Psaní na druhém řádku vedle obrázku vloží text do správného offsetu.
- [x] Psaní na třetím řádku vedle obrázku vloží text do správného offsetu.
- [x] Psaní dlouhého textu způsobí reflow.
- [x] Psaní nezmění image selection.
- [x] Psaní nezmění image layout.
- [x] Přidat E2E pro každý scénář.

### 6.2 Backspace

- [x] Backspace uprostřed textu vedle obrázku smaže znak vlevo.
- [x] Backspace na začátku vizuálního řádku smaže předchozí znak v odstavci.
- [x] Backspace na začátku odstavce sloučí odstavec s předchozím.
- [x] Backspace těsně za inline obrázkem vybere nebo smaže inline obrázek podle definovaného pravidla. Současný model inline obrázky nemá, pravidlo je proto uzavřené jako N/A pro fázi 6.
- [x] Backspace nesmí nechtěně smazat anchored obrázek.
- [x] Backspace nesmí vytvořit prázdný artefakt.
- [x] Přidat E2E a undo/redo testy.

### 6.3 Delete

- [x] Delete uprostřed textu vedle obrázku smaže znak vpravo.
- [x] Delete na konci vizuálního řádku smaže další znak v odstavci.
- [x] Delete na konci odstavce sloučí další odstavec.
- [x] Delete před inline obrázkem vybere nebo smaže inline obrázek podle definovaného pravidla. Současný model inline obrázky nemá, pravidlo je proto uzavřené jako N/A pro fázi 6.
- [x] Delete nesmí nechtěně smazat anchored obrázek.
- [x] Přidat E2E a undo/redo testy.

### 6.4 Enter

- [x] Enter uprostřed textu vedle obrázku rozdělí odstavec.
- [x] Enter na začátku obtékajícího řádku vytvoří nový odstavec.
- [x] Enter na konci obtékajícího řádku vytvoří nový odstavec.
- [x] Reflow po Enter respektuje obrázek.
- [x] Undo vrátí původní odstavec.
- [x] Přidat E2E test.

Ověřeno ve fázi 6: `DocumentEditorLayoutPhase6EditingTests`, `WysiwygPatchApplierTests` a strict E2E scénáře pro insert/backspace/delete/enter vedle square-wrapped obrázku.

## Fáze 7: Drag obrázku a reflow

### 7.1 Move command

- [x] Vytvořit `MoveImageObjectCommand`.
- [x] Command přijme object id, start position, end position.
- [x] Command invaliduje layout dotčených stránek.
- [x] Command vytvoří jednu undo transakci.
- [x] Command zachová anchor podle `MoveWithText`/`FixedOnPage`.
- [x] Přidat unit test commandu.

### 7.2 Pointer drag

- [x] Pointer down na visual rect vybere obrázek.
- [x] Drag nad threshold začne move režim.
- [x] Během drag zobrazit preview box.
- [x] Během drag aktualizovat layout throttlovaně.
- [x] Při pointerup commitnout command.
- [x] Při Escape rollbacknout drag.
- [x] Při ztrátě pointer capture bezpečně commitnout nebo rollbacknout.
- [x] Ověřit, že okolní text se nemění obsahově.

### 7.3 Snap a vodítka

- [x] Přidat snap k levému okraji textové oblasti.
- [x] Přidat snap k pravému okraji textové oblasti.
- [x] Přidat snap ke středu stránky.
- [x] Přidat snap k jiným obrázkům.
- [x] Přidat snap k hornímu/dolnímu okraji řádku.
- [x] Zobrazit guide line při snapu.
- [x] Přidat možnost vypnout snap držením modifieru.
- [x] Přidat E2E geometrický test pro snap ke středu.

### 7.4 Reflow testy

- [x] E2E: drag left image doprava zmenší dostupný text interval.
- [x] E2E: drag image dolů způsobí, že horní řádky už nejsou blokované.
- [x] E2E: drag image nahoru způsobí, že nové řádky začnou obtékat.
- [x] E2E: drag image do středu vytvoří dva textové intervaly.
- [x] E2E: drag image před text nezmění text obsah.
- [x] E2E: undo vrátí image rect i line boxes.
- [x] E2E: redo znovu nastaví image rect i line boxes.

## Fáze 8: Resize, aspect ratio a handles

### 8.1 Selection handles

- [x] Přidat handle `nw`.
- [x] Přidat handle `n`.
- [x] Přidat handle `ne`.
- [x] Přidat handle `e`.
- [x] Přidat handle `se`.
- [x] Přidat handle `s`.
- [x] Přidat handle `sw`.
- [x] Přidat handle `w`.
- [x] Přidat rotation handle.
- [x] Každý handle musí mít správný cursor.
- [x] Každý handle musí mít aria label.
- [x] Každý handle musí být v E2E rozpoznatelný přes `data-testid`.

### 8.2 Resize command

- [x] Vytvořit `ResizeImageObjectCommand`.
- [x] Command uloží předchozí a nové rozměry.
- [x] Command zachová aspect ratio podle pravidel.
- [x] Command invaliduje layout.
- [x] Command aktualizuje inspector state.
- [x] Command vytvoří jednu undo transakci.
- [x] Přidat unit test rohového resize.
- [x] Přidat unit test bočního resize.
- [x] Přidat unit test min size.
- [x] Přidat unit test max size podle page body.

### 8.3 Resize UX

- [x] Během resize zobrazit aktuální šířku/výšku.
- [x] Během resize zobrazit preview box.
- [x] Rohové handles drží aspect ratio defaultně.
- [x] Boční handles mění jednu osu podle nastavení.
- [x] Modifier umožní opačné chování aspect ratio.
- [x] Velikost nesmí spadnout pod minimum.
- [x] Velikost nesmí utéct mimo rozumný page limit bez varování.

### 8.4 Resize E2E

- [x] E2E pro `se` resize.
- [x] E2E pro `e` resize.
- [x] E2E pro `s` resize.
- [x] E2E pro `nw` resize.
- [x] E2E pro zachování aspect ratio.
- [x] E2E pro vypnutý aspect ratio.
- [x] E2E pro reflow po zvětšení.
- [x] E2E pro reflow po zmenšení.
- [x] E2E pro save/reload rozměrů.

## Fáze 9: Image toolbar, layout bubble a inspector

### 9.1 Layout bubble

- [x] Navrhnout kompaktní layout bubble u vybraného obrázku.
- [x] Přidat tlačítko `Inline`.
- [x] Přidat tlačítko `Wrap`.
- [x] Přidat tlačítko `Break`.
- [x] Přidat tlačítko `Behind`.
- [x] Přidat tlačítko `Front`.
- [x] Přidat toggle `Move with text`.
- [x] Přidat toggle `Fix position`.
- [x] Přidat tlačítko `More options`.
- [x] Přidat tooltipy.
- [x] Přidat ikony z knihovny.
- [x] Zajistit, že bubble nepřekrývá side panel.
- [x] Zajistit, že bubble nezmizí při kliknutí do sebe.

### 9.2 Inspector sekce

- [x] Rozdělit inspector na sekci `Obrázek`.
- [x] Přidat sekci `Velikost a otočení`.
- [x] Přidat sekci `Obtékání textu`.
- [x] Přidat sekci `Pozice`.
- [x] Přidat sekci `Pořadí`.
- [x] Přidat sekci `Přístupnost`.
- [x] Přidat collapsed/expanded stav sekcí, pokud panel bude dlouhý.
- [x] Pole URL zobrazovat jen pro URL-backed obrázek.
- [x] Pro asset obrázek zobrazit read-only informaci o assetu.
- [x] Caption editovat s debounce.
- [x] Alt text editovat s debounce.
- [x] Width/height editovat s debounce.
- [x] Position X/Y editovat s debounce.
- [x] Všechny změny aplikovat live.

### 9.3 Kontextové menu

- [x] Přidat `Replace image`.
- [x] Přidat submenu zdrojů: upload, URL, asset/provider, clipboard.
- [x] Přidat `Crop image`.
- [x] Přidat `Alt text`.
- [x] Přidat `Caption`.
- [x] Přidat `Link`.
- [x] Přidat submenu `Text wrapping`.
- [x] Přidat submenu `Position`.
- [x] Přidat `Bring forward`.
- [x] Přidat `Send backward`.
- [x] Přidat `Reset size`.
- [x] Přidat `Delete`.
- [x] Ověřit, že Replace neotevírá upload dialog automaticky.

### 9.4 UI E2E

- [x] E2E: výběr obrázku zobrazí 8 handles.
- [x] E2E: layout bubble je viditelný a uvnitř viewportu.
- [x] E2E: bubble se nezavře při kliknutí na vlastní tlačítko.
- [x] E2E: změna wrap mode přes bubble aktualizuje layout.
- [x] E2E: inspector ukazuje stejný wrap mode jako bubble.
- [x] E2E: kontextové menu neuteče mimo viewport.
- [x] E2E: right panel nemá zbytečný scrollbar, pokud má místo.

## Fáze 10: Anchor, move with text a fixed on page

### 10.1 Anchor model

- [x] Přidat explicitní object anchor do modelu.
- [x] Anchor musí obsahovat block id.
- [x] Anchor musí obsahovat offset, pokud je potřeba.
- [x] Anchor musí obsahovat region.
- [x] Anchor musí obsahovat page index jen jako vypočtenou/debug hodnotu.
- [x] `MoveWithText` musí posouvat objekt s odstavcem.
- [x] `FixedOnPage` musí držet objekt na souřadnici stránky.
- [x] `LockAnchor` musí zabránit nechtěnému přepojení anchoru.

### 10.2 Anchor UI

- [x] Zobrazit anchor glyph u odstavce.
- [x] Glyph ukázat jen při vybraném objektu nebo zapnutých non-printing značkách.
- [x] Glyph nesmí překrývat text.
- [x] Klik na glyph vybere objekt nebo ukáže vztah k objektu.
- [x] Inspector ukáže anchor paragraph.
- [x] Přidat toggle `Lock anchor`.

### 10.3 Anchor chování

- [x] Přidání textu před anchored objekt s `MoveWithText` posune objekt.
- [x] Přidání textu před fixed objekt objekt neposune.
- [x] Přesun objektu může změnit anchor, pokud není locked.
- [x] Přesun objektu nezmění anchor, pokud je locked.
- [x] Smazání anchor odstavce bezpečně přepojí nebo odstraní objekt podle definovaného pravidla.

### 10.4 Anchor E2E

- [x] E2E: `MoveWithText` posune obrázek při vložení textu nad anchor.
- [x] E2E: `FixedOnPage` neposune obrázek při vložení textu nad anchor.
- [x] E2E: `LockAnchor` zachová anchor při drag.
- [x] E2E: anchor glyph je viditelný u vybraného objektu.
- [x] E2E: save/reload zachová anchor volby.

## Fáze 11: Behind text, in front of text a z-index

### 11.1 Layering

- [x] Behind text objekty renderovat do vrstvy pod textem.
- [x] In front objekty renderovat do vrstvy nad textem.
- [x] Square/top-bottom objekty renderovat do objektové vrstvy s exclusion zónami.
- [x] Z-index aplikovat uvnitř vrstvy.
- [x] `AllowOverlap` rozhoduje o povoleném překryvu objektů.

### 11.2 Výběr behind-text objektů

- [x] Klik do textu nad behind-text obrázkem vybírá text.
- [x] Přidat způsob výběru behind-text obrázku přes selection pane nebo explicitní handle.
- [x] Přidat do kontextového menu možnost otevřít selection pane.
- [x] Inspector musí umět zobrazit behind-text objekt po výběru.

### 11.3 Z-order commandy

- [x] Přidat `BringForwardCommand`.
- [x] Přidat `SendBackwardCommand`.
- [x] Přidat `BringToFrontCommand`.
- [x] Přidat `SendToBackCommand`.
- [x] Přidat undo/redo testy.

### 11.4 E2E

- [x] E2E: behind text neblokuje caret.
- [x] E2E: in front text blokuje klik podle visual rectu.
- [x] E2E: bring forward změní pořadí dvou překrytých obrázků.
- [x] E2E: save/reload zachová z-index.

## Fáze 12: Tight/Through a wrap points

### 12.1 Model kontury

- [x] Přidat `WrapContourPoint`.
- [x] Body ukládat normalizovaně vůči obrázku.
- [x] Přidat default contour z obdélníku.
- [x] Přidat validaci minimálního počtu bodů.
- [x] Přidat clamp bodů do rozsahu.

### 12.2 Layout kontury

- [x] Převést contour points na page souřadnice.
- [x] Spočítat polygonovou exclusion zone.
- [x] Pro každý řádek spočítat průnik polygonu s horizontálním pásem.
- [x] Vypočítat dostupné intervaly mimo polygon.
- [x] Přidat unit test pro jednoduchý diamant.
- [x] Přidat unit test pro nepravidelný polygon.

### 12.3 Edit wrap points UI

- [x] Přidat režim `Edit wrap points`.
- [x] Zobrazit konturu okolo obrázku.
- [x] Zobrazit body kontury.
- [x] Drag bodu mění konturu.
- [x] Klik na hranu přidá bod.
- [x] Delete/Backspace smaže aktivní bod.
- [x] Reset obnoví default contour.
- [x] Escape ukončí editaci.

### 12.4 E2E

- [x] E2E: Tight mode změní line boxes oproti Square.
- [x] E2E: drag wrap point změní obtékání.
- [x] E2E: přidání wrap pointu je perzistentní.
- [x] E2E: reset contour vrátí obdélník.

## Fáze 13: Copy/paste, selection přes obrázky a clipboard

### 13.1 Výběr přes obrázek

- [x] Range selection přes inline obrázek zahrne obrázek jako atomický objekt.
- [x] Range selection přes anchored obrázek zahrne jen text, pokud objekt není explicitně vybraný.
- [x] Shift+click rozšiřuje textovou selection podle caret mapy.
- [x] Ctrl+click nebo multi-select vybere více objektů, pokud tuto funkci zavedeme.

### 13.2 Clipboard

- [x] Copy vybraného obrázku vloží image block do interního clipboardu.
- [x] Copy textu okolo obrázku zachová obrázek, pokud selection zahrnuje inline object.
- [x] Paste obrázku vytvoří nový image object s novým id.
- [x] Paste textu obtékajícího obrázek zachová normální text, ne sidecar.
- [x] Paste z externího obrázku vytvoří asset/clipboard image.

### 13.3 E2E

- [x] E2E: copy/paste image zachová alt text.
- [x] E2E: copy/paste image zachová caption.
- [x] E2E: copy/paste image zachová layout.
- [x] E2E: selection textu vedle obrázku kopíruje jen text.
- [x] E2E: undo paste odstraní vložený obrázek.

## Fáze 14: Save/reload, export a demo dokumenty

### 14.1 Save/reload

- [x] Uložit inline image.
- [x] Uložit square left image.
- [x] Uložit square right image.
- [x] Uložit top/bottom image.
- [x] Uložit behind text image.
- [x] Uložit in front image.
- [x] Uložit z-index.
- [x] Uložit anchor.
- [x] Uložit move/fixed volby.
- [x] Uložit wrap distances.
- [x] Uložit width/height/rotation.
- [x] Ověřit reload každé varianty.

### 14.2 Demo data

- [x] Upravit `contract-demo` na nový model.
- [x] Přidat delší odstavec obtékající obrázek.
- [x] Přidat obrázek s caption.
- [x] Přidat obrázek bez alt textu pro accessibility warning, pokud to v demu chceme ukázat.
- [x] Přidat obrázek s asset providerem.
- [x] Přidat obrázek z URL, aby URL field dával smysl.
- [x] Odstranit starý text, který byl jen workaround pro sidecar.
- [x] Ověřit po reloadu cíleným E2E testem.

### 14.3 Export/import minimum

- [x] Interní JSON export musí obsahovat nový layout model.
- [x] Interní JSON import musí nový layout model načíst.
- [x] ODT/DOCX parity zapsat jako navazující samostatnou fázi, pokud nebude součástí této práce.
- [x] Přidat test pro interní roundtrip.

## Fáze 14B: ODT/DOCX parity pro obrázkový layout

- [x] DOCX export zachová inline/anchored/fixed layout.
- [x] DOCX export zachová square left/right.
- [x] DOCX export zachová top/bottom.
- [x] DOCX export zachová behind/in-front vrstvy.
- [x] DOCX export zachová z-index/allow-overlap, pokud to formát dovolí.
- [x] DOCX export zachová anchor a move/fixed volby.
- [x] DOCX export zachová wrap distances.
- [x] DOCX export zachová width/height/rotation.
- [x] DOCX import načte stejný layout do `DocumentObjectLayout`.
- [x] ODT export/import projde stejnou paritu nebo jasně zaznamená podporované minimum.
- [x] Přidat roundtrip testy pro DOCX a ODT.

## Fáze 15: Výkon a stabilita layoutu

### 15.1 Performance instrumentation

- [x] Měřit délku layout passu.
- [x] Měřit délku reflow po drag.
- [x] Měřit délku reflow po resize.
- [x] Měřit počet invalidovaných stránek.
- [x] Měřit cache hit ratio pro text measurement.
- [x] Přidat hodnoty do debug snapshotu.

### 15.2 Invalidation

- [x] Změna textu invaliduje jen dotčený odstavec a následující flow.
- [x] Změna obrázku invaliduje dotčené stránky.
- [x] Změna page layoutu invaliduje celý dokument.
- [x] Změna zoomu nemění model, pouze render/layout measurement.
- [x] Přidat unit testy invalidace.

### 15.3 Stress testy

- [x] E2E: 10 obrázků v dokumentu.
- [x] E2E: 3 obrázky na jedné stránce s různým z-indexem.
- [x] E2E: dlouhý text obtékající několik obrázků.
- [x] E2E: rychlé drag/resize opakování.
- [x] E2E: undo/redo série 20 image operací.
- [x] Ověřit, že runtime neztrácí selection.
- [x] Ověřit, že nevznikají JS chyby v konzoli.

## Fáze 16: Accessibility a keyboard UX

### 16.1 Focus model

- [x] Obrázek musí být focusovatelný klávesnicí.
- [x] Focus ring musí být viditelný.
- [x] Tab pořadí musí být logické.
- [x] Escape z image UI vrací focus na obrázek nebo text podle kontextu.
- [x] Enter na obrázku otevře layout bubble.
- [x] Shift+F10 otevře kontextové menu.

### 16.2 Keyboard manipulation

- [x] Šipka posune vybraný obrázek o 1 px nebo definovaný malý krok.
- [x] Shift+šipka posune obrázek o větší krok.
- [x] Ctrl+šipka provede jemný krok, pokud to nebude kolidovat s browserem.
- [x] Delete smaže vybraný obrázek.
- [x] Ctrl+Z vrátí image operaci.
- [x] Ctrl+Y/Ctrl+Shift+Z zopakuje image operaci.

### 16.3 Screen reader

- [x] Obrázek oznámí alt text.
- [x] Obrázek oznámí wrap mode.
- [x] Obrázek oznámí velikost.
- [x] Prázdný alt text zobrazí warning.
- [x] Dekorativní obrázek lze označit jako dekorativní.
- [x] Inspector sekce mají správné labely.
- [x] Handles mají popis.

### 16.4 Accessibility E2E

- [x] E2E: keyboard vybere obrázek.
- [x] E2E: keyboard změní wrap mode přes bubble.
- [x] E2E: keyboard posune obrázek.
- [x] E2E: Delete smaže vybraný obrázek.
- [x] E2E: warning pro chybějící alt text.

## Fáze 17: Odstranění starého sidecar/float řešení

### 17.1 Odstranit JS sidecar funkce

- [x] Odstranit `_createWrappedImageSideTextBlockModel`.
- [x] Odstranit `_isWrappedImageSideTextBlock`.
- [x] Odstranit `_isTextBlockForWrappedImage`.
- [x] Odstranit `_isWrappedImageSideTextLayout`.
- [x] Odstranit `_ensureWrappedImageSideTextBlock`.
- [x] Odstranit `_findWrappedImageSideTextBlockAtPoint`.
- [x] Odstranit `_focusWrappedImageSideTextBlock`.
- [x] Odstranit debug logy specifické pro sidecar.
- [x] Odstranit volání těchto funkcí z pointerdown/click flow.

### 17.2 Odstranit CSS sidecar/float

- [x] Odstranit `.tm-wysiwyg-image-sidecar-text`.
- [x] Odstranit cílové `float` pravidlo pro `.tm-wysiwyg-image--wrap-square`.
- [x] Odstranit shape-outside fallback, pokud už nebude používaný.
- [x] Odstranit staré media query fallbacky pro float.
- [x] Nechat jen compatibility komentář, pokud bude nutné dočasně držet starou třídu v testech.

### 17.3 Přepsat staré testy

- [x] Najít všechny testy s `data-wrap-sidecar-for`.
- [x] Přepsat je na line box / layout snapshot ověření.
- [x] Najít helper `TypeTextBesideWrappedImageAsync`.
- [x] Přepsat helper tak, aby klikal na layout line box, ne do sidecar oblasti.
- [x] Najít helper `AssertWrappedImageSideTextAsync`.
- [x] Přepsat helper tak, aby ověřoval normální paragraph text a line geometry.
- [x] Najít helper `AssertWrappedImageCaretBesideImageAsync`.
- [x] Přepsat helper na caret map snapshot.

### 17.4 Regression gate

- [x] Přidat E2E assertion, že po běžných image operacích neexistuje `data-wrap-sidecar-for`.
- [x] Přidat E2E assertion, že po save/reload neexistuje `tm-wysiwyg-image-sidecar-text`.
- [x] Přidat unit/integration test, že renderer sidecar nikdy nevytváří.

## Fáze 18: Ruční QA scénáře

### 18.1 Desktop

- [ ] Otevřít demo ve 1440x900.
- [ ] Kliknout na první řádek textu vedle obrázku.
- [ ] Kliknout na druhý řádek textu vedle obrázku.
- [ ] Psát vedle obrázku.
- [ ] Backspace na začátku řádku.
- [ ] Drag obrázku.
- [ ] Resize přes každý roh.
- [ ] Změnit wrap mode přes bubble.
- [ ] Změnit wrap mode přes inspector.
- [ ] Save/reload.

### 18.2 Narrow viewport

- [ ] Otevřít demo na šířce 390.
- [ ] Ověřit, že obrázek neuteče mimo stránku.
- [ ] Ověřit, že layout bubble je čitelný.
- [ ] Ověřit, že inspector se vejde nebo má smysluplný scroll.
- [ ] Ověřit, že text neprotíná obrázek.

### 18.3 Více obrázků

- [ ] Dva obrázky vedle sebe.
- [ ] Obrázek vedle obtékajícího obrázku.
- [ ] Překryté in-front obrázky.
- [ ] Behind-text obrázek pod textem.
- [ ] Drag jednoho obrázku nesmí vybírat druhý.

### 18.4 Editorové hrany

- [ ] Obrázek v headeru.
- [ ] Obrázek ve footeru.
- [ ] Obrázek v tabulce.
- [ ] Obrázek těsně před page break.
- [ ] Obrázek těsně za page break.
- [ ] Obrázek u konce stránky.

## Fáze 19: Dokumentace a interní poznámky

- [ ] Aktualizovat interní dokumentaci modelu obrázků.
- [ ] Popsat rozdíl inline/anchored/fixed.
- [ ] Popsat pravidla hit-testu.
- [ ] Popsat pravidla Backspace/Delete.
- [ ] Popsat layout snapshot debug výstup.
- [ ] Popsat jak psát nové image E2E testy.
- [ ] Přidat krátkou poznámku do demo dokumentace.
- [ ] Do plánovacího TODO odškrtnout dokončené fáze.

## Finální akceptační kritéria

- [ ] V demu lze kliknout na druhý řádek textu vedle obrázku a caret zůstane přesně tam.
- [ ] Backspace vedle obrázku se chová jako v běžném textu.
- [ ] Drag obrázku způsobí reflow textu.
- [ ] Resize obrázku způsobí reflow textu.
- [ ] Obrázek má 8 resize handles.
- [ ] Obrázek má rotation handle.
- [ ] Layout bubble je čitelný a krásný.
- [ ] Inspector ukazuje a live aplikuje všechny hodnoty.
- [ ] Žádný nový scénář nevytváří sidecar odstavec.
- [ ] Save/reload zachová layout obrázků.
- [ ] Undo/redo funguje pro move, resize, wrap mode, position, caption, alt text.
- [ ] E2E testy kontrolují geometrii, caret, UI stav, model a save/reload.
- [ ] Demo dokumenty jsou aktualizované na nový model.
- [ ] V konzoli nejsou JS chyby při běžné práci s obrázky.
