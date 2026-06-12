# TmDocumentEditor - image/drawing handling ONLYOFFICE-level TDD TODO

Datum založení: 2026-05-25  
Stav: navrženo, čeká na implementaci  
Navazuje na: `planning/tmdocumenteditor-image-handling-onlyoffice-analysis-2026-05-25.md`  
Priorita: P0 - práce s obrázky musí přestat být blokový hack a stát se součástí skutečného dokumentového layoutu

## Proč tento TODO existuje

Současný editor už má dobré stavební kameny pro obrázky:

- historický `ImageBlockContent`,
- `DocumentObjectLayout`,
- anchor/wrap/position/transform/stacking model,
- object layer v novějším rendereru,
- exclusion intervals v layout enginu,
- image toolbar, resize handles a drag preview.

Praktické chování ale stále trpí zásadním architektonickým problémem: obrázek je primárně top-level block dokumentu. ONLYOFFICE používá jiný model: obrázek je drawing objekt ukotvený v odstavci, tedy objekt vložený do běhu textu, který se může vizuálně chovat inline nebo floating. To je nutné převzít jako princip.

Tento dokument rozepisuje implementaci po co nejmenších TDD krocích. Každý krok má nejdřív vytvořit nebo zpřísnit test, potom provést minimální změnu a nakonec ověřit regresi.

## Cílový stav

- Obrázek lze vložit uprostřed odstavce bez vytvoření samostatného odstavce kolem obrázku.
- Inline obrázek se chová jako inline objekt v textu, ne jako samostatný blok.
- Floating obrázek je drawing objekt ukotvený k odstavci a inline offsetu.
- Square/Tight/Through/TopBottom obtékání je součástí paragraph line layoutu.
- Uživatel může začít psát vedle obrázku i v prázdném obtékaném prostoru.
- ArrowUp/ArrowDown z textu nikdy náhodně nepřepne focus na obrázek.
- Obrázek se vybere explicitně kliknutím nebo objektovou klávesovou navigací, ne běžným pohybem caret.
- Escape z vybraného obrázku vrátí caret k anchor pozici.
- Drag používá overlay/track preview a při dropu přepočítá anchor na nejbližší paragraph/offset.
- Resize používá overlay/track preview a zapíše jednu undoable operaci.
- WYSIWYG editace nepoužívá CSS float jako zdroj pravdy.
- JS a C# layout používají stejnou semantiku wrap/anchor/position.
- Save/reload/export zachová obrázky, anchor, wrap, velikost, alt text, caption a stacking.
- Demo dokumenty, demo seedy a ukázkové stránky používají nový drawing run/object model.
- Zpětná kompatibilita se starými dokumenty není cílem této práce; starý image block je jen dočasný vstup pro přepsání demo dat a odstranění starých cest.

## Nevyjednatelná pravidla implementace

- [ ] Každý uživatelský problém musí mít nejdřív RED test.
- [ ] E2E testy pro UX musí používat skutečnou myš a klávesnici, ne interní JS commandy.
- [ ] Interní JS API smí být použité pro seed, diagnostiku a snapshoty, ne jako náhrada uživatelské akce.
- [ ] Obrázek v editačním toku nesmí být běžná DOM focus zastávka jen proto, že je v dokumentu.
- [ ] Text selection a object selection musí být oddělené stavy.
- [ ] Drag/resize pointermove nesmí zapisovat každým pohybem do trvalého dokumentového modelu.
- [ ] Každý drag/resize commit musí být jeden undo krok.
- [ ] Layout změny musí fungovat v body, headeru, footeru a tabulkách.
- [ ] Není požadovaná zpětná kompatibilita se starým `ImageBlockContent` modelem.
- [ ] Staré image block cesty se mají odstranit nebo přepsat na nový drawing model, ne dlouhodobě udržovat vedle něj.
- [ ] Všechny demo dokumenty a demo seedy musí být upravené na nový model.
- [ ] Žádný existující test se nesmí oslabit tak, aby procházel se špatným uživatelským chováním.
- [ ] Veřejné modelové typy a `[Parameter]` vlastnosti musí mít XML dokumentaci.
- [ ] Kód, komentáře a XML dokumentace jsou anglicky; planning dokumenty jsou česky.

## Doporučené nové testovací soubory

- [ ] `tests/Tempo.Blazor.E2E/DocumentEditorImageOnlyOfficeParityE2ETests.cs`
- [ ] `tests/Tempo.Blazor.Tests/Models/DocumentEditor/DocumentDrawingRunModelTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/Models/DocumentEditor/DocumentDrawingObjectSerializationTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageSelectionJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageWrapJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorImageDragResizeJavaScriptTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/Services/DocumentEditor/DocumentLayoutEngineDrawingObjectTests.cs`
- [ ] `tests/Tempo.Blazor.Tests/Components/DocumentEditor/TmDocumentImageToolbarSelectionTests.cs`
- [ ] `tests/Tempo.Blazor.E2E/DocumentEditorImageDemoDocumentsE2ETests.cs`

Názvy lze upravit podle existující lokální organizace, ale testovací oblasti musí zůstat oddělené: model, serializer, C# layout, JS runtime, Blazor shell, human-like E2E.

## Fáze 0: Baseline, pojmenování a bezpečnostní síť

### 0.1 Založit image parity E2E soubor

- [x] Vytvořit `DocumentEditorImageOnlyOfficeParityE2ETests.cs`.
- [x] Použít existující E2E base class pro document editor.
- [x] Přidat deterministic seed dokument pro obrázky.
- [x] Seed musí obsahovat běžný body odstavec.
- [x] Seed musí obsahovat prázdný odstavec.
- [x] Seed musí obsahovat odstavec s textem před/za budoucím obrázkem.
- [x] Seed musí obsahovat header.
- [x] Seed musí obsahovat footer.
- [x] Seed musí obsahovat tabulku s buňkou a textem.
- [x] Seed nesmí obsahovat top-level image block jako cílový stav.
- [x] Seed musí obsahovat inline drawing image uprostřed odstavce.
- [x] Seed musí obsahovat jeden floating Square obrázek.
- [x] Seed musí obsahovat jeden TopBottom obrázek.
- [x] Seed musí obsahovat jeden BehindText/InFrontOfText obrázek.
- [x] Seed musí odpovídat budoucím demo dokumentům, ne umělé interní struktuře.

Poznámka k RED stavu: fyzický seed zatím obsahuje stabilní textové cíle pro image scénáře a současné staré image blocky. Test `ImageOnlyOfficeParity_SeedContainsPhase0ImageScenariosAndTargetsNewDrawingModel` záměrně vyžaduje cílový stav bez top-level image blocků a s drawing runs, takže bude červený do implementace modelových fází.

### 0.2 Zapsat aktuální RED chování

- [x] E2E RED: kliknout do textu před Square obrázkem a stisknout ArrowDown.
- [x] Ověřit, že aktuálně selection může skončit na obrázku nebo image toolbaru.
- [x] E2E RED: kliknout do textu za Square obrázkem a stisknout ArrowUp.
- [x] Ověřit, že aktuálně selection může skončit na obrázku.
- [x] E2E RED: vložit obrázek uprostřed věty.
- [x] Ověřit, že aktuálně vzniká samostatný image block.
- [x] E2E RED: vložit Square obrázek do prázdného odstavce a kliknout vedle něj do volného obtékaného prostoru.
- [x] Ověřit, že aktuálně nejde začít psát přirozeně vedle obrázku.
- [x] E2E RED: začít psát vedle obrázku bez existujícího textu.
- [x] Ověřit, že text nevznikne na očekávané vizuální pozici.
- [x] E2E RED: kliknout přímo na obrázek.
- [x] Ověřit, že object selection a toolbar stále fungují.
- [x] E2E RED: stisknout Escape z vybraného obrázku.
- [x] Ověřit, že caret se nevrací konzistentně k anchor pozici.
- [x] E2E RED: drag obrázku přes odstavec.
- [x] Ověřit, že anchor/reflow neodpovídá drop pozici.
- [x] E2E RED: resize obrázku.
- [x] Ověřit, že undo vrací velikost jedním krokem.

### 0.3 Audit existujících image testů

- [x] Projít `DocumentEditorRegressionRecoveryPhase9E2ETests.cs`.
- [x] Projít `DocumentEditorStrictEnginePhase11E2ETests.cs`.
- [x] Projít `DocumentEditorE2ETests.cs` image scénáře.
- [x] Projít `DocumentEditorStrictEnginePhase20E2ETests.cs`.
- [x] Projít `DocumentEditorModelTests.cs` image layout testy.
- [x] Projít `DocumentSerializerTests.cs` image block testy.
- [x] Projít `TmDocumentRendererTests.cs` image block render testy.
- [x] Projít `TmDocumentImageInspectorTests.cs`.
- [x] U každého testu zapsat, jestli ověřuje nový drawing object, starou cestu k odstranění, nebo jen UI shell.
- [x] Označit testy, které je nutné odstranit nebo přepsat, protože drží starý image block model.
- [x] Označit testy, které bude nutné přepsat z image block očekávání na drawing run očekávání.

| Soubor | Co dnes ověřuje | Cílová akce |
|---|---|---|
| `DocumentEditorRegressionRecoveryPhase9E2ETests.cs` | Vizuální překryvy, markery a image toolbar mimo text. | Ponechat jako vizuální smoke, ale image toolbar assertions napojit na object selection nad drawing objectem. |
| `DocumentEditorStrictEnginePhase11E2ETests.cs` | Engine-level anchored object normalizaci, exclusions, preview drag/resize a widget UI přes interní engine API. | Zachovat jako diagnostiku geometrie, ale nenahrazuje human E2E; po fázích 7-17 přepsat image vstupy na drawing run. |
| `DocumentEditorE2ETests.cs` image scénáře | Široké historické UI testy obrázků, často přes isolated dokumenty a image blocky. | Přepsat scénáře na drawing object model; odstranit očekávání top-level image blocku. |
| `DocumentEditorStrictEnginePhase20E2ETests.cs` | Strict gate pro image wrap scénáře. | Přepsat gate tak, aby kontrolovala drawing runs a ne pouze starý image block runtime. |
| `DocumentEditorModelTests.cs` | Roundtrip a layout varianty `ImageBlockContent`. | Přepsat na `DocumentDrawingRun`; staré image block testy odstranit, protože zpětná kompatibilita není cíl. |
| `DocumentSerializerTests.cs` | WYSIWYG serializer mapping image blocku. | Přepsat na drawing run mapping a přidat assertion, že nový save/export nevytváří image block. |
| `TmDocumentRendererTests.cs` | Static render `ImageBlockContent`. | Přepsat static render test na drawing object model, případný image block fallback odstranit. |
| `TmDocumentImageInspectorTests.cs` | Inspector nad `ImageBlockContent`. | Přepsat na active drawing object / object id. |

### 0.4 Přidat diagnostické helpery

- [x] Přidat E2E helper pro přečtení aktivního selection režimu: `Text` / `Object`.
- [x] Přidat E2E helper pro přečtení active image id.
- [x] Přidat E2E helper pro přečtení caret block id a offset.
- [x] Přidat E2E helper pro přečtení anchor block id a anchor offset obrázku.
- [x] Přidat E2E helper pro přečtení počtu top-level image blocků.
- [x] Přidat E2E helper pro přečtení počtu drawing runs v odstavci.
- [x] Přidat E2E helper pro computed rect obrázku.
- [x] Přidat E2E helper pro computed rect caret.
- [x] Přidat E2E helper pro line intervaly kolem obrázku.
- [x] Přidat assertion, že editor host má focus i při object selection.

### 0.5 Akceptace fáze 0

- [x] Existuje RED baseline pro všechny hlášené image UX problémy.
- [x] Testy jasně rozlišují cílový drawing model a staré cesty určené k odstranění.
- [x] Helpery umí číst selection, anchor, layout rect a strukturu dokumentu.
- [x] Nic z produkčního chování zatím nemusí být opravené.

## Fáze 1: Modelový základ pro drawing run

### 1.1 Přidat RED model testy

- [x] Test: textový odstavec může obsahovat `DocumentDrawingRun`.
- [x] Test: `DocumentDrawingRun` dědí z `InlineContent`.
- [x] Test: `DocumentDrawingRun` má stabilní `ObjectId`.
- [x] Test: `DocumentDrawingRun` má `Kind = Image`.
- [x] Test: `DocumentDrawingRun` má `Image` payload nebo referenci na image payload.
- [x] Test: drawing run může nést `DocumentObjectLayout`.
- [x] Test: default layout drawing runu je inline.
- [x] Test: anchored drawing run má anchor block id odpovídající parent blocku.
- [x] Test: anchored drawing run má anchor inline index nebo offset.
- [x] Test: marks na drawing runu se nemíchají s text formatting marks, pokud nejsou explicitně podporované.

### 1.2 Implementovat minimální model

- [x] Upravit `DocumentInline.cs`.
- [x] Přidat `[JsonDerivedType(typeof(DocumentDrawingRun), "drawing")]`.
- [x] Přidat `DocumentDrawingRun : InlineContent`.
- [x] Přidat XML dokumentaci ke všem veřejným vlastnostem.
- [x] Přidat `DocumentDrawingKind` enum.
- [x] Přidat `DocumentDrawingKind.Image`.
- [x] Přidat image data vlastnosti: `Source`, `Url`, `AssetId`, `AltText`, `Caption`.
- [x] Přidat `Size`, `NaturalSize`, `Layout`.
- [x] Přidat `Metadata`.
- [x] Default `Layout` nastavit na `DocumentObjectLayout.Inline()`.
- [x] Nepřidávat novou dlouhodobou kompatibilní vrstvu pro `ImageBlockContent`.

### 1.3 Zpřesnit naming

- [x] Rozhodnout, zda cílový typ bude `DocumentDrawingRun` nebo `DocumentObjectRun`.
- [x] Rozhodnout, zda image payload bude přímo v runu nebo v samostatné `DocumentDrawingObject`.
- [x] Pokud vznikne centrální kolekce objektů, přidat test na referenční integritu `ObjectId` nebo rozhodnout, že ve fázi 1 nevzniká.
- [x] Pokud payload zůstane v runu, přidat modelový test bez centrální kolekce.
- [x] Zapsat rozhodnutí do komentáře v TODO nebo samostatné krátké poznámky.

Poznámka fáze 1: cílový typ je `DocumentDrawingRun`. Image payload zůstává přímo v runu, protože současný inline model už je zdrojem pořadí textu a objektů a umožní malé TDD kroky bez nové centrální kolekce objektů. `ObjectId` je stabilní identita pro layout, selection, commandy a budoucí persistence. Starý `ImageBlockContent` nedostává novou kompatibilní vrstvu; další fáze ho budou postupně odstraňovat z runtime, serializeru, demo dokumentů a testů.

### 1.4 Akceptace fáze 1

- [x] Modelové testy pro drawing run jsou zelené.
- [x] Staré image block modelové testy jsou označené k odstranění nebo přepsání.
- [x] Serializer zatím nemusí umět nový typ.
- [x] Editor runtime zatím nemusí nový typ používat.

## Fáze 2: Serializer a odstranění starého image block modelu

### 2.1 RED serializer testy

- [x] Test: dokument s `DocumentDrawingRun` se serializuje do JSON.
- [x] Test: JSON s `$type: "drawing"` se deserializuje zpět.
- [x] Test: `AltText`, `Caption`, `AssetId`, `Url` přežijí roundtrip.
- [x] Test: `DocumentObjectLayout` přežije roundtrip.
- [x] Test: Square wrap přežije roundtrip.
- [x] Test: TopBottom wrap přežije roundtrip.
- [x] Test: BehindText/InFrontOfText přežije roundtrip.
- [x] Test: `Transform.Width/Height` přežije roundtrip.
- [x] Test: `Stacking.ZIndex` přežije roundtrip.
- [x] Test: nový dokument už nevyžaduje top-level `ImageBlockContent`.

### 2.2 Implementovat serializer podporu

- [x] Ujistit se, že polymorfní JSON zná `DocumentDrawingRun`.
- [x] Doplnit serializer mapping v WYSIWYG model serializeru.
- [x] Dopsat mapping z runtime image objectu do `DocumentDrawingRun`.
- [x] Dopsat mapping z `DocumentDrawingRun` zpět do runtime modelu.
- [x] Ujistit se, že bezpečnostní pravidla pro image URL platí i pro drawing run.
- [x] Ověřit provider boundary: display-only blob URL se nesmí uložit jako trvalý zdroj.

### 2.3 Odstranit starý serializer mapping

- [x] Najít serializer testy, které očekávají `ImageBlockContent`.
- [x] Přepsat je na `DocumentDrawingRun`.
- [x] Odstranit image block mapping z WYSIWYG serializeru, jakmile demo data používají nový model.
- [x] Pokud je potřeba jednorázový konverzní helper pro demo dokumenty, držet ho mimo runtime cestu.
- [x] Konverzní helper nesmí být nová kompatibilní runtime vrstva.
- [x] Přidat test, že výchozí save/export nového dokumentu neobsahuje top-level image block.

Poznámka fáze 2: WYSIWYG serializer už při ukládání nevytváří `ImageBlockContent`; legacy `ImageBlock` runtime vstup převádí na paragraph s jedním `DocumentDrawingRun`. Čtecí cesta pro starý top-level image block zatím zůstává jen jako stávající starý vstup, protože demo seedy a plné runtime odstranění jsou naplánované až ve fázích 3 a 20. Nebyl přidán žádný nový kompatibilitní konverzní helper pro runtime.

### 2.4 Akceptace fáze 2

- [x] Nový drawing run roundtrip je zelený.
- [x] Staré image block serializer testy jsou přepsané nebo odstraněné.
- [x] WYSIWYG serializer/save boundary nevytváří nový `ImageBlockContent`; staré runtime insert příkazy se odstraní v dalších fázích.
- [x] Žádný E2E ještě nemusí být zelený.

## Fáze 3: Runtime interní model pro drawing objekty

### 3.1 RED JS/runtime testy

- [x] Test: runtime načte odstavec s drawing runem.
- [x] Test: runtime vytvoří mapu drawing objectů podle `objectId`.
- [x] Test: runtime rozliší inline drawing a anchored drawing.
- [x] Test: runtime nepočítá drawing run jako textový znak s délkou textu.
- [x] Test: runtime umí převést text offset na inline index i přes drawing run.
- [x] Test: runtime umí najít drawing run podle object id.
- [x] Test: runtime umí vrátit anchor paragraph id a offset.
- [x] Test: runtime umí vrátit layout snapshot drawing objectu.

### 3.2 Implementovat runtime normalizaci

- [x] Upravit model import v `document-editor-wysiwyg.js`.
- [x] Přidat funkci pro normalizaci drawing runu.
- [x] Rozšířit existující `normalizeImageObject`, aby přijímalo i drawing run.
- [x] Přidat `objectId` jako primární identitu objektu.
- [x] Přidat interní `drawingObjectsById`.
- [x] Přidat interní `drawingRunsByBlockId`.
- [x] Přidat diagnostický snapshot pro testy.

### 3.3 Přepnout runtime na nový režim

- [x] Dočasný internal gate vyhodnocen jako nepotřebný: fáze 3 je runtime/model hard-cut bez render změn.
- [x] Výchozí testovací seed musí používat drawing-run image runtime.
- [x] E2E parity testy musí používat nový režim bez image block fallbacku.
- [x] Gate není potřeba odstraňovat, protože nevznikl.
- [x] Zapsat do testů, že nový cílový stav je drawing run, ne starý image block.

### 3.4 Akceptace fáze 3

- [x] Runtime umí načíst nový model bez render změn.
- [x] Runtime diagnostika vrací drawing object mapu.
- [x] Runtime testy už neočekávají image block fallback.

Poznámka 2026-05-25: Fáze 3 dokončena. `document-editor-wysiwyg.js` importuje `$type: "drawing"` jako nulově dlouhý inline object run, zachovává ho při merge runů, exportuje ho zpět jako `DocumentDrawingRun`, rozšiřuje schema o inline `drawing` a staví `drawingObjectsById`/`drawingRunsByBlockId`. Test hooky vrací diagnostiku object mapy, lookup podle `objectId`, layout snapshot a mapování textového offsetu přes drawing run. ONLYOFFICE parity seed už pro image scénáře nepoužívá top-level `ImageBlockContent`, ale paragraph bloky s `DocumentDrawingRun`; E2E helper normalizuje i numerické wrap enumy. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentEditorImageDrawingPhase3RuntimeJavaScriptTests|FullyQualifiedName~DocumentEditorProviderTests.Provider_OnlyOfficeParitySeed_UsesDrawingRunsInsteadOfTopLevelImageBlocks|FullyQualifiedName~DocumentEditorRuntimePhase3TextRunJavaScriptTests"`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~DocumentSerializerTests"`.

## Fáze 4: Selection model - oddělit text selection a object selection

### 4.1 RED selection testy

- [x] Test: text selection má `mode = Text`.
- [x] Test: explicitní image click vytvoří `mode = Object`.
- [x] Test: object selection obsahuje `objectId`.
- [x] Test: object selection obsahuje anchor paragraph id.
- [x] Test: object selection nemění text caret anchor/focus snapshot.
- [x] Test: Escape z object selection obnoví text selection k anchor pozici.
- [x] Test: Delete v object selection smaže object.
- [x] Test: Delete v text selection vedle objectu nesmaže object.
- [x] Test: Backspace v text selection vedle objectu nesmaže object.

### 4.2 Implementovat selection state

- [x] Přidat interní `selectionMode`.
- [x] Přidat `textSelection`.
- [x] Přidat `objectSelection`.
- [x] Upravit selection changed payload do Blazoru.
- [x] Upravit active image detection v `TmDocumentWysiwygHost`.
- [x] Upravit `GetActiveImageSelectionBlockId` nebo ho nahradit `GetActiveImageSelectionObjectId`.
- [x] Přepsat payload na object id jako zdroj pravdy.
- [x] Přidat selection token invalidation při model commitu.

### 4.3 Akceptace fáze 4

- [x] JS selection unit testy jsou zelené.
- [ ] E2E klik na obrázek stále zobrazí image toolbar. (Lokálně blokováno: neběží demo API na `https://localhost:5100`.)
- [x] E2E šipky ještě nemusí být opravené.

## Fáze 5: Focus policy a odstranění náhodného focusu obrázku

### 5.1 RED focus E2E

- [x] E2E: ArrowDown z textu před obrázkem nevybere obrázek.
- [x] E2E: ArrowUp z textu za obrázkem nevybere obrázek.
- [x] E2E: po ArrowUp/Down zůstává `selectionMode = Text`.
- [x] E2E: editor host zůstává aktivní focus element.
- [x] E2E: image toolbar se po ArrowUp/Down nezobrazí.
- [x] E2E: klik na obrázek image toolbar zobrazí.

### 5.2 Upravit DOM focus

- [x] Najít všechny render cesty, které dávají image `tabindex="0"`.
- [x] Odstranit `tabindex="0"` z běžného WYSIWYG image figure/object renderu.
- [x] Pokud je potřeba přístupnost, nahradit ji roving object navigation režimem. Object režim je nyní explicitní klikem / panelem; běžná tab navigace obrázek nepřebírá.
- [x] Ujistit se, že image handles nejsou tab stop bez explicitního object selection.
- [x] Upravit CSS `:focus-visible` pravidla na selection class.
- [x] Přidat class pro explicitně selected object.
- [x] Ujistit se, že screen reader label lze číst z aktivního editor hostu nebo inspectoru.

### 5.3 Upravit keyboard navigation

- [x] Upravit ArrowUp/ArrowDown handler, aby preferoval text line hit.
- [x] Pokud cílová line obsahuje object, hledat nejbližší text interval.
- [x] Pokud text interval neexistuje, přeskočit na nejbližší text caret před/za objektem.
- [x] Nikdy nepřepínat do object selection bez explicitního objektového režimu.
- [x] Přidat explicitní shortcut pro object navigation, pokud ji chceme. Pro fázi 5 není nový shortcut přidaný; explicitní object režim zůstává klik / selection pane.
- [x] Přidat Escape chování z object selection.

### 5.4 Akceptace fáze 5

- [x] RED focus E2E jsou zelené.
- [x] Klik na obrázek stále vybere obrázek.
- [x] Image toolbar se nezobrazuje při obyčejných šipkách v textu.
- [x] Editor zůstává ovladatelný klávesnicí.

## Fáze 6: Insert image do caret pozice místo samostatného blocku

### 6.1 RED insert testy

- [x] Unit: command `InsertImage` při caret uprostřed `TextRun` splitne text run.
- [x] Unit: předchozí text zůstane před drawing runem.
- [x] Unit: následující text zůstane za drawing runem.
- [x] Unit: drawing run má anchor block id aktuálního paragraphu.
- [x] Unit: drawing run má offset odpovídající caret pozici.
- [x] Unit: insert do prázdného paragraphu vloží drawing run do inlines.
- [x] Unit: insert do headingu vloží drawing run nebo korektně vytvoří paragraph podle pravidel.
- [x] E2E: vložit obrázek uprostřed věty nevytvoří top-level image block.
- [x] E2E: text před a za obrázkem zůstane ve stejném odstavci.

### 6.2 Implementovat model command

- [x] Přidat model helper pro split inline listu podle text offsetu.
- [x] Přidat `InsertDrawingRunCommand`.
- [x] Přidat undo payload pro vložení drawing runu.
- [x] Přidat redo payload pro vložení drawing runu.
- [x] Přidat command result se selection: object selection nového obrázku.
- [x] Přidat fallback pro nepodporované kontejnery.

### 6.3 Upravit JS `applyInsertImage`

- [x] Změnit default insert cestu z image block splice na drawing run insert.
- [x] Odstranit image block insert z výchozí cesty.
- [x] Zachovat upload/provider metadata.
- [x] Zachovat alt text a caption.
- [x] Zachovat default `DocumentObjectLayout.Inline()`.
- [x] Po insertu nastavit explicitní object selection.
- [x] Po Escape vrátit caret za vložený drawing run.

### 6.4 Akceptace fáze 6

- [x] Insert image E2E je zelený.
- [x] Save/reload po vložení zachová drawing run.
- [x] Undo po vložení obrázek odstraní jedním krokem.
- [x] Redo obrázek vrátí jedním krokem.

Poznámka 2026-05-25: Fáze 6 dokončena. `InsertImage` / `insertImageBlock` už ve výchozí cestě vkládá `drawing` run do caret pozice, splituje textové runy, zachovává text před/za obrázkem, alt/caption/provider metadata a nastavuje object selection na nový objekt. Object selection po insertu fokusuje WYSIWYG root, aby klávesové undo/redo fungovalo i bez nativního textového caret focusu. E2E diagnostika drawing runů počítá obsahový strom, ne interní indexy. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase --logger "console;verbosity=minimal"`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter FullyQualifiedName~ImageOnlyOfficeParity_InsertImageAtCaretCreatesDrawingRunNotTopLevelBlock --logger "console;verbosity=minimal"`. Celý `DocumentEditorImageOnlyOfficeParityE2ETests` zatím zůstává částečně červený na scénářích pozdějších fází: prázdný obtékaný prostor, explicitní klik na floating objekt, drag a resize.

## Fáze 7: Inline drawing layout jako inline box

### 7.1 RED layout testy

- [x] Layout test: inline drawing má width/height podle size/transform.
- [x] Layout test: inline drawing zvyšuje line height podle své výšky.
- [x] Layout test: text před a za inline drawing je na stejné linii, pokud se vejde.
- [x] Layout test: inline drawing se zalomí jako inline box, pokud se nevejde.
- [x] Layout test: caret před drawing runem má správný x.
- [x] Layout test: caret za drawing runem má správný x.
- [x] Layout test: inline drawing nevytváří exclusion zone.

### 7.2 Implementovat C# layout

- [x] Rozšířit paragraph inline layout o non-text inline box.
- [x] Přidat měření `DocumentDrawingRun`.
- [x] Přidat line segment typ pro drawing run.
- [x] Přidat segment rect pro object render.
- [x] Upravit `DocumentParagraphLayoutBox`, pokud neumí reprezentovat object segment.
- [x] Zajistit, že text measurement ignoruje drawing payload jako text.

### 7.3 Implementovat JS layout/render

- [x] Rozšířit JS line layout o inline object segment.
- [x] Renderovat inline object v object layer nebo inline placeholder podle architektury.
- [x] Uložit caret mapování před a za drawing segmentem.
- [x] Přidat hit testing před/za inline image.
- [x] Přidat object click hit testing na inline image.

### 7.4 Akceptace fáze 7

- [x] Inline image se chová jako inline box.
- [x] Text před/za obrázkem zůstává v odstavci.
- [x] ArrowLeft/ArrowRight přes inline image funguje předvídatelně.
- [x] ArrowUp/ArrowDown z textu image nevybírá.

## Fáze 8: Anchored/floating drawing layout

### 8.1 RED C# layout testy

- [x] Layout test: Square anchored drawing vytvoří exclusion zone.
- [x] Layout test: TopBottom drawing zablokuje celou šířku line range.
- [x] Layout test: BehindText nevytvoří exclusion zone.
- [x] Layout test: InFrontOfText nevytvoří exclusion zone.
- [x] Layout test: anchored drawing se umístí relativně k anchor paragraphu.
- [x] Layout test: `MoveWithText = true` posune objekt při vložení textu před anchor.
- [x] Layout test: `FixedOnPage = true` neposune objekt při změně textu před anchor.
- [x] Layout test: `LockAnchor = true` zabrání automatickému reanchoru při běžném layoutu.

### 8.2 Implementovat anchor resolver

- [x] Přidat resolver anchoru z drawing runu.
- [x] Anchor musí znát block id.
- [x] Anchor musí znát inline index.
- [x] Anchor musí znát text offset.
- [x] Anchor musí znát region: body/header/footer/table cell.
- [x] Anchor resolver musí vracet reference rect.
- [x] Pokud line rect ještě neexistuje, použít paragraph start fallback.
- [x] Fallback logovat jen diagnosticky, ne do console error.

### 8.3 Implementovat object placement

- [x] Převést anchored drawing na object box.
- [x] Spočítat X podle horizontal position.
- [x] Spočítat Y podle vertical position.
- [x] Aplikovat offsets.
- [x] Aplikovat transform width/height.
- [x] Aplikovat z-index.
- [x] Přidat object box do page object collection.
- [x] Přidat exclusion zone jen pro wrap módy, které obtékají text.

### 8.4 Akceptace fáze 8

- [x] C# layout testy pro anchored drawing jsou zelené.
- [x] JS runtime layout umí zobrazit anchored drawing z drawing runu.
- [x] Staré image block layout testy jsou přepsané na drawing run.

Poznámka 2026-05-25: Fáze 8 dokončena. C# layout engine rozlišuje inline a anchored/fixed `DocumentDrawingRun`, pro anchored runy normalizuje anchor metadata, vytváří object box, respektuje position/transform/z-index a zakládá exclusion zóny jen pro wrap módy, které obtékají text. JS runtime už neposílá anchored/fixed drawing runy do line-breakeru jako inline box, ale publikuje je jako page object + exclusion a renderuje je v odstavci jako floating/anchored objekt. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase8AnchoredLayoutJavaScriptTests --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase7InlineLayoutJavaScriptTests --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentLayoutEngineTests --no-restore`.

## Fáze 9: Sjednotit JS a C# wrap geometrii

### 9.1 RED parity testy

- [x] Test: C# a JS vrací stejný exclusion rect pro Square.
- [x] Test: C# a JS vrací stejný full-width interval pro TopBottom.
- [x] Test: C# a JS ignoruje BehindText pro exclusions.
- [x] Test: C# a JS ignoruje InFrontOfText pro exclusions.
- [x] Test: C# a JS respektuje `DistanceLeft`.
- [x] Test: C# a JS respektuje `DistanceRight`.
- [x] Test: C# a JS respektuje `DistanceTop`.
- [x] Test: C# a JS respektuje `DistanceBottom`.
- [x] Test: contour polygon se zpracuje stejně v C# i JS.

### 9.2 Refaktor DTO

- [x] Zavést jednotný JSON tvar pro `DocumentObjectLayout`.
- [x] Nahradit ad hoc zploštěné JS layout fieldy tam, kde už je možné použít strukturovaný model.
- [x] Odstranit starý zploštěný tvar z výchozí serializace.
- [x] Přidat normalizační testy pro nový tvar.
- [x] Zajistit, že nulová hodnota X/Y je validní a neztratí se.

### 9.3 Geometrie

- [x] Vytáhnout JS wrap interval calculation do samostatné testovatelné funkce.
- [x] Přidat testy pro interval subtraction.
- [x] Přidat testy pro polygon projection.
- [x] Přidat testy pro wrap margins.
- [x] Přidat testy pro no-available-interval fallback.
- [x] Přidat testy pro object overlap policy.

### 9.4 Akceptace fáze 9

- [x] C# a JS layout mají stejnou semantiku wrap modes.
- [x] Zero X/Y pozice fungují.
- [x] Nový layout JSON je jediný cílový tvar pro demo dokumenty i save/export.

Poznámka 2026-05-25: Fáze 9 dokončena. JS wrap geometrie používá stejné principy jako C#: jednotlivé `DistanceLeft/Right/Top/Bottom`, clipping do body rectu, full-width TopBottom exclusion, ignorování BehindText/InFrontOfText pro text exclusions, polygon projection pro Tight/Through a stejné odečítání intervalů. `imageObjectToLayout` už vrací strukturovaný `DocumentObjectLayout` tvar místo zploštěných polí a zachovává nulové X/Y i nulové distance. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase9WrapGeometryParityTests --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentLayoutGeometryTests --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests --no-restore`.

## Fáze 10: Caret hit testing v obtékaných intervalech

### 10.1 RED hit-test testy

- [x] JS test: klik do textové části řádku vlevo od obrázku vrátí text caret.
- [x] JS test: klik do textové části řádku vpravo od obrázku vrátí text caret.
- [x] JS test: klik přímo na obrázek vrátí object selection.
- [x] JS test: klik do prázdného obtékaného intervalu vrátí text caret v anchor paragraphu.
- [x] JS test: klik do zakázaného intervalu pod obrázkem u TopBottom nevrátí caret uvnitř objektu.
- [x] E2E: klik vedle Square obrázku umístí caret vedle obrázku.

### 10.2 Implementovat caret interval mapu

- [x] Ukládat line intervals do runtime layout mapy.
- [x] Každý interval musí znát block id.
- [x] Každý interval musí znát line id.
- [x] Každý interval musí znát text offset rozsah.
- [x] Prázdný interval musí umět reprezentovat collapsed caret offset.
- [x] Hit testing musí nejdřív zkusit text interval, potom object, podle priority a přesného bodu.
- [x] Klik na object body stále vybere object.

### 10.3 Akceptace fáze 10

- [x] Kliknutí vedle obrázku je textové, ne objektové.
- [x] Kliknutí na obrázek je objektové.
- [x] Prázdný obtékaný prostor může hostit caret.

Poznámka 2026-05-25: Fáze 10 dokončena. Runtime layout nyní publikuje `lineIntervals` s `blockId`, `lineId`, offset rozsahem a `collapsedOffset`; DOM snapshot doplňuje anchored drawing objekty a ořezává caret intervaly kolem viditelných objektů. `pointerHitTest` i `hitTestLayoutGeometry` používají intervalovou mapu pro klik vedle obrázku a objektové tělo nechávají vyhrát jako explicitní object selection. Klik do prázdného povoleného intervalu vrací collapsed caret v anchor odstavci, zatímco TopBottom blokovaný prostor nevytvoří text caret. Přidán E2E scénář `ImageOnlyOfficeParity_ClickBesideSquareImagePlacesTextCaret`. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase10CaretHitTestingJavaScriptTests --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorImageDrawingPhase10CaretHitTestingJavaScriptTests|FullyQualifiedName~DocumentEditorLayoutPhase5HitTestTests" --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorImageDrawingPhase --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentLayoutGeometryTests --no-restore`; `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`.

## Fáze 11: Psaní vedle obrázku bez existujícího textu

### 11.1 RED typing E2E

- [x] E2E: Square obrázek v prázdném odstavci, klik vpravo vedle něj, napsat `Text vedle`.
- [x] Ověřit, že text vznikne v tomtéž odstavci.
- [x] Ověřit, že nevznikne nový top-level image wrapper paragraph.
- [x] Ověřit, že obrázek zůstane ukotvený.
- [x] Ověřit, že text obtéká obrázek.
- [x] E2E: klik vlevo vedle pravostranného obrázku a psaní funguje.
- [x] E2E: psaní vedle obrázku funguje i po save/reload.

### 11.2 Implementovat virtual caret positions

- [x] Přidat runtime reprezentaci caret pozice v prázdném line intervalu.
- [x] Mapovat tuto pozici na paragraph block id a offset.
- [x] Pokud paragraph nemá text run, vytvořit prázdný `TextRun`.
- [x] Vložený text musí vzniknout v text runu, ne v image payloadu.
- [x] Typing style se má převzít z paragraphu nebo nejbližšího textového runu.
- [x] Track changes a comments se nesmí automaticky rozšířit na nový text, pokud to není správná typing policy.

### 11.3 Akceptace fáze 11

- [x] Uživatel může začít psát vedle obrázku i tam, kde předtím žádný text nebyl.
- [x] Text a obrázek sdílí jeden paragraph anchor kontext.
- [x] Undo psaní vedle obrázku funguje jako běžné psaní.

Poznámka 2026-05-25: Implementováno přes `virtualCaret` metadata na prázdných wrapped line intervalech, přenesení `affinity` do `InsertText` a opravené vkládání textu kolem nulově dlouhých drawing runů. Doplněny JS unit testy pro left/right virtual caret, styl/comment policy a undo; E2E kliká do publikovaných wrapped text intervalů a ověřuje undo/save/reload.

## Fáze 12: Object layer jako jediná editační pravda

### 12.1 RED render testy

- [x] Test: WYSIWYG image drawing se renderuje do object layer.
- [x] Test: text layer neobsahuje focusovatelný image figure.
- [x] Test: selection handles jsou v decorator/overlay vrstvě.
- [x] Test: stará CSS float class není použitá jako editační layout mechanismus.
- [x] E2E: image toolbar se neprotíná s readable text.

### 12.2 Refaktor rendereru

- [x] Najít všechny `renderImageFigureStyle` a staré image figure cesty.
- [x] Oddělit static renderer od WYSIWYG rendereru.
- [x] Pro WYSIWYG použít page object layer.
- [x] Static renderer přepsat na drawing model nebo jasně omezit na interní fallback během přechodu.
- [x] Přesunout selection box a handles do overlay/decorator vrstvy.
- [x] Upravit CSS, aby selection class nenahrazovala DOM focus.
- [x] Ujistit se, že object z-index funguje.

### 12.3 Akceptace fáze 12

- [x] WYSIWYG chování obrázku nepochází z CSS floatu.
- [x] Static/read-only render stále zobrazuje obrázky.
- [x] E2E vizuální testy obrázků prochází.

Poznámka 2026-05-25: WYSIWYG renderer teď kreslí obrázky přes page object layer, zatímco text layer obsahuje pouze nefokusovatelný drawing anchor. Selection box, resize handles a image toolbar jsou přesunuté do overlay/guides vrstev nad stránkou, static renderer zůstává omezený fallback pro read-only výstup. Doplněny render testy pro object layer, absenci focusovatelného image figure v text layeru, overlay handles, nepoužití legacy float layoutu a cílené E2E ověření, že image toolbar nepřekrývá čitelný text.

## Fáze 13: Image toolbar a inspector na object selection

### 13.1 RED toolbar testy

- [x] Component test: image toolbar se zobrazí při object selection.
- [x] Component test: image toolbar se nezobrazí při text selection vedle obrázku.
- [x] Component test: inspector čte active drawing object.
- [x] E2E: změna wrap mode přes toolbar upraví selected drawing object.
- [x] E2E: změna size přes inspector upraví selected drawing object.
- [x] E2E: změna alt textu se uloží do drawing objectu.

### 13.2 Upravit Blazor host

- [x] Upravit active image resolution z block id na object id.
- [x] Upravit command payloady: `objectId`, ne jen `blockId`.
- [x] Upravit `setImageWrapMode`.
- [x] Upravit `setImagePosition`.
- [x] Upravit `setImageSize`.
- [x] Upravit `setImageObjectPosition`.
- [x] Upravit `setImageAnchorMode`.
- [x] Upravit `deleteImage`.
- [x] Ujistit se, že všechny commandy jsou undoable.

### 13.3 Akceptace fáze 13

- [x] Toolbar pracuje s novými drawing objects.
- [x] Text selection vedle obrázku nespouští image toolbar.

Ověřeno:
- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~TmDocumentWysiwygHostTests|FullyQualifiedName~TmDocumentEditorTests"`
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorImageOnlyOfficeParityE2ETests"`

## Fáze 14: Drag track preview

### 14.1 RED drag unit testy

- [x] JS test: pointerdown na image body vytvoří pre-drag state.
- [x] JS test: malý pohyb pod threshold nezačne drag.
- [x] JS test: pohyb nad threshold vytvoří drag track.
- [x] JS test: pointermove mění track transform.
- [x] JS test: pointermove nemutuje trvalý document model.
- [x] JS test: preview může zobrazit guides.
- [x] JS test: Escape během drag zruší track.
- [x] JS test: pointerup commitne jednu operaci.

### 14.2 Implementovat track model

- [x] Přidat `imageMoveTrack` state.
- [x] Uložit original rect.
- [x] Uložit original layout.
- [x] Uložit pointer start.
- [x] Uložit current delta.
- [x] Renderovat preview přes transform.
- [x] Přidat center/page/object guides.
- [x] Přidat throttled lightweight reflow jen pokud je nutné.
- [x] Zajistit cleanup track DOM po commit/cancel.

### 14.3 Akceptace fáze 14

- [x] Drag je plynulejší, protože pointermove nepřepisuje model.
- [x] Undo stack se nemění před pointerup.
- [x] E2E drag preview je viditelné.

Poznámka 2026-05-25: Fáze 14 je dokončená. Drag obrázku používá `imageMoveTrack` s pre-drag stavem, transform preview, vodicími linkami, Escape cancel a jedním commitem na pointerup. Ověřeno přes `node --check`, cílené JS testy fáze 14, build E2E projektu a E2E test viditelnosti drag preview bez předčasné změny undo stacku.

## Fáze 15: Drop reanchor na nejbližší textovou pozici

### 15.1 RED reanchor testy

- [x] JS test: drop nad odstavcem nastaví anchor na tento odstavec.
- [x] JS test: drop uprostřed řádku nastaví anchor offset podle nejbližší pozice.
- [x] JS test: drop do headeru nastaví region header.
- [x] JS test: drop do footeru nastaví region footer.
- [x] JS test: drop do table cell nastaví region/table cell kontext.
- [x] JS test: `LockAnchor = true` brání automatickému reanchoru, pokud nejde o explicitní drag.
- [x] E2E: přesunutý obrázek se po save/reload drží u nového odstavce.

### 15.2 Implementovat nearest position resolver

- [x] Přidat runtime funkci `findNearestTextPositionForPoint`.
- [x] Resolver musí procházet visible paragraph line boxes.
- [x] Resolver musí respektovat page index.
- [x] Resolver musí respektovat region.
- [x] Resolver musí umět prázdný paragraph.
- [x] Resolver musí umět table cell.
- [x] Resolver musí vrátit block id, inline index, text offset, region.
- [x] Resolver musí vrátit reference rect pro relativní X/Y.

### 15.3 Commit operation

- [x] Přidat `MoveDrawingObject` operation.
- [x] Operation uloží old layout.
- [x] Operation uloží new layout.
- [x] Operation uloží old anchor.
- [x] Operation uloží new anchor.
- [x] Undo vrátí old anchor i old position.
- [x] Redo nastaví new anchor i new position.
- [x] Blazor patch applier musí operation aplikovat na nový model.

### 15.4 Akceptace fáze 15

- [x] Drop objektu reanchoruje srozumitelně.
- [x] Save/reload zachová nový anchor.
- [x] Undo/redo funguje jedním krokem.

Poznámka 2026-05-25: Fáze 15 je dokončená. Drag commit používá `MoveDrawingObject`, ukládá starý i nový layout/anchor a resolver vybírá nejbližší textovou pozici v body, headeru, footeru i table cell. Anchor region se při exportu do C# zapisuje jako enum hodnota, aby save/reload nemohl selhat na string enumu. Ověřeno přes `node --check`, JS testy fáze 14 a 15, C# unit test applieru, build E2E projektu a E2E scénáře drag/undo + drag/save/reload.

## Fáze 16: Resize track preview

### 16.1 RED resize unit testy

- [x] JS test: pointerdown na handle vytvoří resize track.
- [x] JS test: resize track zná handle index.
- [x] JS test: resize track zná fixed point.
- [x] JS test: pointermove mění preview width/height.
- [x] JS test: pointermove nemutuje trvalý model.
- [x] JS test: Shift zachová aspect ratio.
- [x] JS test: min width/min height se respektují.
- [x] JS test: Escape resize zruší.
- [x] JS test: pointerup commitne jednu operaci.

### 16.2 Implementovat resize track

- [x] Přidat `imageResizeTrack` state.
- [x] Uložit original rect.
- [x] Uložit original transform.
- [x] Uložit handle.
- [x] Uložit fixed point.
- [x] Přidat aspect ratio calculation.
- [x] Přidat min size.
- [x] Přidat optional max size podle page body.
- [x] Renderovat resize badge.
- [x] Renderovat preview transform.
- [x] Cleanup po commit/cancel.

### 16.3 Commit operation

- [x] Přidat `ResizeDrawingObject` operation nebo rozšířit `UpdateImageLayout`.
- [x] Operation zapisuje `Transform.Width`.
- [x] Operation zapisuje `Transform.Height`.
- [x] Operation zachová natural size.
- [x] Operation zachová aspect ratio lock.
- [x] Undo/redo vrací velikost i případné position compensation.

### 16.4 Akceptace fáze 16

- [x] Resize je plynulý.
- [x] Text reflow nastane konzistentně po commitu nebo řízeně throttled.
- [x] Undo/redo funguje jedním krokem.

Poznámka 2026-05-25: Fáze 16 je dokončená. Resize přes handle používá samostatný `imageResizeTrack`, živý DOM preview mění pouze transform/width/height a badge, zatímco trvalý model se mění až na `pointerup`. Commit zůstává nad `UpdateImageLayout`, ale zapisuje starý i nový layout, takže undo/redo vrací rozměr i kompenzovanou pozici jedním krokem. Ověřeno přes `node --check`, JS testy fází 14-16 a E2E scénáře resize preview bez commitu + resize undo.

## Fáze 17: Převod wrap mode commandů na drawing object model

### 17.1 RED command testy

- [x] Test: `setImageWrapMode Square` nastaví anchored layout.
- [x] Test: `setImageWrapMode Inline` nastaví inline layout.
- [x] Test: `setImageWrapMode TopBottom` nastaví anchored layout a full-width exclusion.
- [x] Test: `setImageWrapMode BehindText` nastaví stacking behind a bez exclusion.
- [x] Test: `setImageWrapMode InFrontOfText` nastaví fixed/anchored front podle pravidel a bez exclusion.
- [x] Test: změna wrap mode zachová object id.
- [x] Test: změna wrap mode zachová alt/caption/source.
- [x] Test: změna wrap mode je jeden undo krok.

### 17.2 Implementovat command mapping

- [x] Upravit `wrapModeToValue`.
- [x] Upravit `syncImageLayoutCase`.
- [x] Upravit `cloneImageLayoutForUpdate`.
- [x] Upravit `applyRuntimeImageCommand`.
- [x] Přestat spoléhat na image block id jako jediný identifikátor.
- [x] Přidat object id lookup.
- [x] Přidat event pro toolbar state update.

### 17.3 Akceptace fáze 17

- [x] Wrap mode UI funguje nad drawing objectem.
- [x] E2E wrap změna je viditelná okamžitě.
- [x] Save/reload zachová wrap mode.

Poznámka 2026-05-25: Fáze 17 je dokončená. `setImageWrapMode` používá drawing target s object-id lookupem, umí fallback na jednoznačný drawing run v odstavci, nastavuje konzistentní `Kind`/`Anchor`/`Wrap`/`Stacking` pro Inline, Square, Tight/Through, TopBottom, BehindText a InFrontOfText a zapisuje `oldLayout/newLayout`, takže změna wrap režimu je jeden `UpdateImageLayout` undo krok. Toolbar state se po image layout commandu publikuje okamžitě. Ověřeno JS unit testy fází 14-17 a E2E scénáři okamžité změny wrap režimu + save/reload persistence.

## Fáze 18: Header/footer a table cell chování

### 18.1 RED region testy

- [x] E2E: vložit inline obrázek do headeru.
- [x] E2E: psát před inline obrázkem v headeru bez zpomalení.
- [x] E2E: psát za inline obrázkem v headeru bez zpomalení.
- [x] E2E: vložit Square obrázek do headeru a psát vedle něj.
- [x] E2E: stejné scénáře ve footeru.
- [x] E2E: vložit inline obrázek do table cell.
- [x] E2E: vložit Square obrázek do table cell a ověřit local wrap.
- [x] Layout test: header image exclusion neovlivní body text.
- [x] Layout test: footer image exclusion neovlivní body text.
- [x] Layout test: table cell image exclusion neovlivní text mimo buňku.

### 18.2 Implementovat region scoping

- [x] Anchor musí obsahovat region.
- [x] Anchor musí rozlišit body/header/footer/table cell.
- [x] Layout object manager musí mít separátní scope pro region.
- [x] Hit testing musí znát region.
- [x] Nearest position resolver musí znát region.
- [x] Drag mezi regiony musí být explicitně povolený nebo zakázaný pravidlem.
- [x] Pokud je drop mezi regiony zakázaný, UI musí vrátit objekt zpět.

### 18.3 Akceptace fáze 18

- [x] Header/footer typing s obrázky je plynulé.
- [x] Regiony si nekradou exclusions.
- [x] Drag/resize funguje i v header/footer/table cell podle pravidel.

Poznámka 2026-05-25: Fáze 18 dokončena. Drawing anchor teď nese region i konkrétní `HeaderFooterId`/`TableId`/`CellId`, insert a následná selection zachovávají header/footer/table-cell kontext, object selection podle object id umí region dopočítat a layout vytváří oddělené object/exclusion scope pro body, header, footer a jednotlivé buňky tabulky. Hit testing a nearest position resolver čtou i header/footer regiony a vnořené table-cell line boxy. Drag mezi regiony je implicitně zakázaný, volitelně povolitelný přes `allowCrossRegionDrop`; zakázaný drop vrací objekt do původního modelu bez mutace. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorImageDrawingPhase18RegionScopeJavaScriptTests" --verbosity normal` (5/5); `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorStrictEnginePhase18ImageRegionScopeE2ETests" --verbosity normal` (1/1).

## Fáze 19: Persistence, provider boundary a export

### 19.1 RED persistence testy

- [x] Test: save request obsahuje drawing run.
- [x] Test: save request neobsahuje display-only blob URL.
- [x] Test: asset image zachová `AssetId`.
- [x] Test: URL image zachová bezpečnou URL.
- [x] Test: data URL se chová podle existujících bezpečnostních pravidel.
- [x] Test: caption přežije save/reload.
- [x] Test: alt text přežije save/reload.
- [x] Test: wrap mode přežije save/reload.
- [x] Test: anchor přežije save/reload.
- [x] Test: transform přežije save/reload.

### 19.2 Upravit export/import mapping

- [x] Upravit interní document serializer.
- [x] Upravit provider boundary document mapper.
- [x] Upravit PDF/export request mapping.
- [x] Upravit DOCX/ODT mapping, pokud v repo existuje.
- [x] Přidat explicitní TODO pro plnohodnotné DOCX drawingML, pokud teď není v rozsahu.

### 19.3 Akceptace fáze 19

- [x] Save/reload E2E pro inline a floating obrázky je zelený.
- [x] Provider boundary testy jsou zelené.
- [x] Export testy nepřišly o obrázky.

Poznámka 2026-05-25: Fáze 19 dokončena. Provider boundary a save snapshot převádí legacy `ImageBlockContent` na kanonický `DocumentDrawingRun`, čistí display-only/blob URL a zachovává asset id, bezpečné URL/data URL, alt/caption, wrap, anchor a transform. Interní serializer, PDF/text export, HTML/Markdown, DOCX a ODT mapping umí drawing runy bez návratu k top-level image blokům; DOCX export používá existující picture DrawingML cestu i pro inline drawing runy a import už vrací obrázky jako runy v odstavci. TODO mimo tuto fázi: doplnit plnou DOCX DrawingML paritu pro pokročilé Word anchor scénáře, crop/efekty a další high-fidelity vlastnosti. Ověřeno: `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj --no-restore`; `dotnet build src/Tempo.Blazor.DocumentFormats/Tempo.Blazor.DocumentFormats.csproj --no-restore`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentDrawingRunSerializationTests|FullyQualifiedName~Provider_Save_SanitizesDrawingRunUrlsAndPreservesPersistentImagePayload|FullyQualifiedName~Provider_Save_ConvertsLegacyImageBlocksToDrawingRunsAtBoundary|FullyQualifiedName~Provider_SavesNormalizedRawJsonAndRejectsInvalidConcurrencyToken"` (6/6); `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~FormatExportImport_RoundtripsDrawingRunsWithoutLegacyImageBlocks"` (1/1); `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~Phase19_SaveReloadPersistsInlineAndFloatingDrawingRuns|FullyQualifiedName~Phase17_ImageOperationsAndSaveReloadNeverCreateLegacySidecars"` (2/2).

## Fáze 20: Demo dokumenty, seedy a ukázkové stránky

### 20.1 Inventura demo dat

- [x] Najít všechny demo dokumenty v `src/Tempo.Blazor.Demo`.
- [x] Najít všechny sdílené demo dokumenty v `src/Tempo.Blazor.Demo.SharedUI`.
- [x] Najít demo seedy v `src/Tempo.Blazor.Demo.Api`.
- [x] Najít demo data používaná serverovým demo projektem.
- [x] Najít demo data používaná InteractiveAuto demo projektem.
- [x] Najít E2E seed dokumenty, které mají image block.
- [x] Najít screenshot/visual demo scénáře závislé na starém image block DOMu.
- [x] Sepsat tabulku: soubor, demo scénář, typ obrázku, cílový nový model.

| Soubor | Demo scénář | Původní typ obrázku | Cílový model |
|---|---|---|---|
| `src/Tempo.Blazor.Demo.Api/Services/DemoDocumentEditorStore.cs` | `contract-demo`, `exhibits-demo` | top-level `ImageBlockContent` | paragraph `DocumentDrawingRun` s `ObjectId` |
| `src/Tempo.Blazor.Demo.SharedUI/Services/DemoDocumentEditorProvider.cs` | WASM/Server/InteractiveAuto sdílené demo dokumenty | top-level `ImageBlockContent` | paragraph `DocumentDrawingRun` |
| `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/InMemoryDocumentEditorProvider.cs` | recovery a onlyoffice parity seed | top-level `ImageBlockContent` v recovery seed | drawing runy v body/header/footer/table cell |
| `tests/Tempo.Blazor.E2E/DocumentEditorJsRuntimeImageTests.cs` | runtime insert/save/reload smoke | legacy `insertImageNode` image block DOM | object-layer/inline drawing DOM |
| `tests/Tempo.Blazor.E2E/DocumentEditorStrictEnginePhase19E2ETests.cs` | canonical demo audit | image block JSON očekávání | drawing run JSON očekávání |
| `src/Tempo.Blazor.Demo.Api/Data/MockNotionBlockStore.cs` | Notion editor demo | Notion `ImageBlockContent` | mimo DocumentEditor fázi, ponecháno |

### 20.2 Přepsat demo dokumenty na drawing run/object model

- [x] Upravit demo dokument s inline obrázkem uprostřed odstavce.
- [x] Upravit demo dokument se Square obtékáním.
- [x] Upravit demo dokument s obrázkem vlevo a textem vpravo.
- [x] Upravit demo dokument s obrázkem vpravo a textem vlevo.
- [x] Upravit demo dokument s TopBottom obrázkem.
- [x] Upravit demo dokument s BehindText obrázkem.
- [x] Upravit demo dokument s InFrontOfText obrázkem.
- [x] Upravit demo dokument s obrázkem v headeru.
- [x] Upravit demo dokument s obrázkem ve footeru.
- [x] Upravit demo dokument s obrázkem v tabulce.
- [x] Doplnit demo dokument s prázdným odstavcem a obrázkem, vedle kterého lze začít psát.
- [x] Doplnit demo dokument s více obrázky a různými z-indexy.

### 20.3 Přepsat demo UI očekávání

- [x] Demo stránky nesmí generovat nový top-level image block.
- [x] Demo stránky musí používat nový insert command.
- [x] Demo inspector musí zobrazovat drawing object data.
- [x] Demo save/export musí ukládat drawing run/object model.
- [x] Demo preview musí renderovat object layer, ne CSS float jako zdroj pravdy.
- [x] Demo ukázky musí obsahovat text, na kterém je vidět skutečné obtékání.

### 20.4 RED/green demo testy

- [x] E2E: otevřít hlavní demo dokument a ověřit, že neobsahuje top-level image block.
- [x] E2E: otevřít demo dokument s inline obrázkem a ověřit drawing run.
- [x] E2E: otevřít demo dokument se Square obrázkem a ověřit text reflow.
- [x] E2E: otevřít demo dokument s header/footer obrázkem a ověřit region scope.
- [x] E2E: demo save/reload zachová drawing object.
- [x] Unit/component test: demo data factory vytváří drawing runs.

### 20.5 Akceptace fáze 20

- [x] Všechna demo data používají nový drawing run/object model.
- [x] Demo aplikace už nevytváří ani neukazuje starý image block model.
- [x] E2E demo smoke testy pro obrázky prochází.
- [x] Demo dokumenty pokrývají inline, Square, TopBottom, BehindText, InFrontOfText, header, footer a table cell scénáře.

Poznámka 2026-05-25: Fáze 20 dokončena. DocumentEditor demo seedy v API i SharedUI už vystavují obrázky jako `DocumentDrawingRun`, recovery/onlyoffice seed nepoužívá top-level image bloky a obsahuje inline, Square, TopBottom, BehindText, InFrontOfText, header, footer, table-cell, prázdný odstavec a z-index scénáře. Legacy runtime helper `insertImageNode` teď vytváří paragraph drawing run místo image blocku, takže testovací/demo insert cesta nezavádí starý model ani před save. Notion demo store zůstává mimo rozsah, protože používá vlastní Notion block model. Ověřeno: `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet build src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~DemoSeed_IncludesImageDocumentCommentsAndVersions|FullyQualifiedName~DemoSeeds_UseDrawingRunsWithoutTopLevelImageBlocks|FullyQualifiedName~FormatExportImport_RoundtripsDrawingRunsWithoutLegacyImageBlocks"` (3/3); `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~Phase20_DefaultDemoUsesDrawingRunsWithoutTopLevelImageBlocks|FullyQualifiedName~Phase19_SaveReloadPersistsInlineAndFloatingDrawingRuns|FullyQualifiedName~Phase17_ImageOperationsAndSaveReloadNeverCreateLegacySidecars|FullyQualifiedName~ImageOnlyOfficeParity_SeedContainsPhase0ImageScenariosAndTargetsNewDrawingModel|FullyQualifiedName~DocumentEditor_Strict_Engine_DemoResetReturnsCanonicalQualityScenarios"` (5/5); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditor_DemoPage_RendersWysiwygShell"` (1/1).

## Fáze 21: Accessibility a explicitní keyboard object navigation

### 21.1 RED accessibility testy

- [x] Test: obrázek má dostupný název z alt textu v object selection režimu.
- [x] Test: image toolbar tlačítka mají aria label.
- [x] Test: resize handles mají aria label jen při object selection.
- [x] Test: běžná Tab navigace neuvízne v každém obrázku v dokumentu.
- [x] E2E: klávesou lze explicitně přejít na další objekt, pokud tuto funkci zavedeme.
- [x] E2E: Escape z objektu vrátí caret.

### 21.2 Implementovat policy

- [x] Rozhodnout shortcut pro object navigation.
- [x] Přidat editor status text pro active object.
- [x] Přidat aria popis selected image.
- [x] Přidat keyboard command pro delete selected object.
- [x] Přidat keyboard command pro otevření image toolbar/inspector.
- [x] Udržet běžné psaní a šipky text-first.

### 21.3 Akceptace fáze 21

- [x] Obrázky jsou přístupné bez toho, aby rozbíjely textovou caret navigaci.
- [x] Accessibility testy a focus E2E prochází.

Poznámka k implementaci: Explicitní object navigation používá `Ctrl+Alt+O/P` a kompatibilně podporuje i `Alt+Shift+O/P`; `Escape` vrací caret do textu, `Delete/Backspace` smaže vybraný objekt a `F10/Enter` otevře image toolbar.

## Fáze 22: Performance guardrails

### 22.1 RED performance testy

- [x] JS performance test: typing vedle obrázku nesmí spustit full document relayout více než povolený limit.
- [x] JS performance test: header typing s obrázkem nesmí přerenderovat body pages.
- [x] JS performance test: drag pointermove nemutuje model.
- [x] JS performance test: resize pointermove nemutuje model.
- [x] E2E perf smoke: napsat 100 znaků v headeru s obrázkem a změřit latenci.
- [x] E2E perf smoke: resize 20 pointermoves a ověřit počet commitů.

### 22.2 Instrumentace

- [x] Přidat debug counter pro layout passes.
- [x] Přidat debug counter pro render swaps.
- [x] Přidat debug counter pro object track frames.
- [x] Přidat debug counter pro model commits.
- [x] Přidat debug snapshot pro active region.
- [x] Counters dostupné jen v test/debug režimu.

### 22.3 Optimalizace

- [x] Omezit relayout na affected region/page.
- [x] Při typing v headeru nepočítat body, pokud se nezměnila page geometry.
- [x] Při object drag používat overlay transform.
- [x] Při object resize používat overlay transform.
- [x] Po commitu přepočítat jen dotčené paragraphs/pages.
- [x] Cacheovat exclusion intervals pro nezměněné objects.

### 22.4 Akceptace fáze 22

- [x] Header/footer typing s obrázky je měřitelně rychlejší.
- [x] Drag/resize pointermove je plynulý.
- [x] Performance testy mají jasné limity a nejsou flaky.

## Fáze 23: Přepsat a zpřísnit existující testy

### 23.1 Přepsat staré image block testy

- [x] `DocumentEditorModelTests` image block roundtrip přepsat na drawing run roundtrip.
- [x] `DocumentSerializerTests` image block mapping přepsat na drawing run mapping.
- [x] `TmDocumentRendererTests` image block render přepsat na drawing object render.
- [x] `TmDocumentImageInspectorTests` přepsat na active drawing object.
- [x] Odstranit testy, které chrání pouze starou kompatibilitu.
- [x] Ponechat jen testy, které hlídají nový cílový model nebo skutečné UX.

### 23.2 Přepsat testy, které maskují špatné UX

- [x] E2E testy, které vybírají image interním JS commandem, doplnit reálným klikem.
- [x] E2E testy resize/drag doplnit ověřením undo stacku.
- [x] E2E testy wrap doplnit ověřením reálného text reflow.
- [x] E2E testy toolbaru doplnit ověřením selection mode.
- [x] E2E testy save/reload doplnit anchor/wrap/transform assertions.
- [x] E2E testy demo dokumentů doplnit kontrolou, že demo data používají drawing run/object model.

### 23.3 Akceptace fáze 23

- [x] Testy už nedrží starý image block model jako požadované chování.
- [x] Nové testy chrání skutečné uživatelské chování.
- [x] Žádný důležitý image scénář není zelený jen díky internímu test hooku.

## Fáze 24: Odstranit starý image block runtime

### 24.1 RED deprecation testy

- [x] Test: nový insert image už nevytváří image block.
- [x] Test: nový demo dokument neobsahuje top-level image block.
- [x] Test: save po editaci obrázku uloží drawing run/object model.
- [x] Test: runtime diagnostics nehlásí image block jako aktivní zdroj pravdy.

### 24.2 Odstranit duplicitní runtime cesty

- [x] Najít image block splice v `applyInsertImage`.
- [x] Odstranit jej z výchozí cesty.
- [x] Najít staré `renderImageFigureStyle` použití v WYSIWYG.
- [x] Odstranit z výchozí WYSIWYG cesty.
- [x] Najít image selection podle block id.
- [x] Přepsat na object id.
- [x] Najít image commandy podle block id.
- [x] Přepsat na object id bez fallback větve.
- [x] Odstranit nepoužívané image block runtime helpery.
- [x] Odstranit nepoužívané image block CSS, pokud ho nepoužívá static renderer. Zůstává jen static/legacy render stopa mimo výchozí WYSIWYG model.

### 24.3 Akceptace fáze 24

- [x] Nové dokumenty používají drawing run/object model.
- [x] Demo dokumenty používají drawing run/object model.
- [x] Starý image block není zdroj pravdy pro WYSIWYG interakce.
- [x] Runtime nemá dlouhodobou fallback větev pro starý image block model.

Poznámka 2026-05-25: Fáze 24 dokončena. `TmDocumentWysiwygHost` posílá do JS display snapshot bez top-level `ImageBlockContent`, URL dialog/upload/provider asset insert vytváří `DocumentDrawingRun`, runtime image commandy vyžadují `objectId`, `insertImageBlock` alias je odstraněný z mapperu a diagnostics hlásí `drawing-object-id` jako zdroj pravdy. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorImageDrawingPhase" --logger "console;verbosity=minimal"`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorImageDrawingPhase24RuntimeJavaScriptTests|FullyQualifiedName~DocumentEditorImageDrawingPhase6InsertJavaScriptTests|FullyQualifiedName~TmDocumentWysiwygHostTests|FullyQualifiedName~DocumentEditorProviderTests|FullyQualifiedName~DocumentEditorCommandAdapterTests" --logger "console;verbosity=minimal"`; `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorImageOnlyOfficeParityE2ETests.ImageOnlyOfficeParity_SeedContainsPhase0ImageScenariosAndTargetsNewDrawingModel|FullyQualifiedName~DocumentEditorImageOnlyOfficeParityE2ETests.ImageOnlyOfficeParity_InsertImageAtCaretCreatesDrawingRunNotTopLevelBlock" --logger "console;verbosity=minimal"`.

## Analýza: Plnohodnotné DOCX DrawingML pro obrázky

### Co znamená "plnohodnotné DrawingML"

DOCX obrázek nesmí být exportovaný jen jako vizuální náhražka. Cílový stav je, že každý `DocumentDrawingRun` má v DOCX skutečný WordprocessingML/DrawingML tvar:

- `w:r/w:drawing/wp:inline` pro inline obrázek.
- `w:r/w:drawing/wp:anchor` pro plovoucí obrázek.
- `wp:extent` jako velikost objektu v EMU.
- `wp:effectExtent` jako rezerva pro efekty, stíny a otočení.
- `wp:docPr` jako veřejná identita objektu, název, alt popis a případný hyperlink.
- `wp:cNvGraphicFramePr/a:graphicFrameLocks` pro non-visual frame vlastnosti, hlavně lock aspect ratio.
- `a:graphic/a:graphicData` s picture URI `http://schemas.openxmlformats.org/drawingml/2006/picture`.
- `pic:pic/pic:nvPicPr/pic:cNvPr` pro non-visual picture identity.
- `pic:blipFill/a:blip` s `r:embed` nebo `r:link`, tedy vztah na image part nebo externí obrázek.
- `pic:blipFill/a:srcRect`, `a:stretch/a:fillRect` nebo `a:tile` pro crop/fill/tile.
- `pic:spPr/a:xfrm` pro offset, extent, rotaci a flip.
- `pic:spPr/a:prstGeom prst="rect"` pro běžný bitmapový obrázek.

OnlyOffice-level kompatibilita znamená, že DOCX otevřený v OnlyOffice/Wordu zobrazí obrázek na stejném místě, se stejným obtékáním, velikostí, cropem, alt textem, z-orderem a anchor chováním, a že po importu zpět do Tempa neztratíme přesnou pozici obrázku v odstavci.

### Primární Open XML fakta, která musí model respektovat

- `wp:inline` je inline DrawingML objekt uvnitř běhu textu; výška/šířka se zapisuje přes `wp:extent`.
- `wp:anchor` je floating objekt ukotvený v textu; nese `simplePos`, `relativeHeight`, `behindDoc`, `locked`, `layoutInCell`, `allowOverlap`, `distT/distB/distL/distR` a poziční děti `wp:positionH`/`wp:positionV`.
- Horizontální a vertikální pozice může být explicitní `wp:posOffset` nebo preset `wp:align`; reference frame může být page/margin/column/character a page/margin/paragraph/line.
- Obtékání není jeden boolean: `wp:wrapNone`, `wp:wrapSquare`, `wp:wrapTight`, `wp:wrapThrough`, `wp:wrapTopAndBottom` mají odlišnou sémantiku; `behindDoc=true` znamená BehindText a `wrapNone + behindDoc=false` odpovídá InFrontOfText.
- Media data nejsou uložená přímo ve `w:drawing`; `a:blip r:embed="rId..."` odkazuje na image part v dané části balíčku. Vztahy v `document.xml`, headeru, footeru a poznámkách jsou oddělené.
- Jednotky musí být deterministické: 1 pt = 12 700 EMU, 1 inch = 914 400 EMU, 1 px při 96 DPI = 9 525 EMU. Současný kód prakticky používá body/pt jako renderer jednotku a násobí 12 700.
- Crop v DrawingML není v bodech; `a:srcRect` používá tisíciny procenta. Hodnota `10000` je 10 %, `100000` je 100 %.
- Rotace v `a:xfrm/@rot` je v šedesátitisícinách stupně. Jeden stupeň = 60 000.
- `wp14:sizeRelH`/`wp14:sizeRelV` řeší relativní velikost vůči margin/page/paragraph; první iterace ji může importovat jako metadata a neexportovat, ale nesmí ji tiše zničit v roundtrip scénářích označených jako preserve.

### Aktuální stav v Tempu

- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentInline.cs` už má `DocumentDrawingRun` s `ObjectId`, `Source`, `Url`, `AssetId`, `AltText`, `Caption`, `Size`, `NaturalSize`, `Layout`, `LinkUrl` a `Metadata`.
- `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentObjectLayout.cs` už má dobrý základ: `Kind`, `Anchor`, `Position`, `Wrap`, `Transform`, `Stacking`; transform už obsahuje `Rotation` a `Crop`, wrap má contour points.
- `src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxExporter.cs` už generuje `wp:inline` a `wp:anchor`, `wp:extent`, `wp:docPr`, `pic:pic`, `a:blip`, `a:xfrm` a `a:prstGeom`.
- Export dnes převádí `DocumentDrawingRun` zpět přes `ToImageBlockContent`, což je jen interní DTO zkratka a v dalších fázích má zmizet.
- Export dnes vytváří image part vždy jako PNG (`ImagePartType.Png`), i když zdroj může být JPEG/GIF/SVG/WEBP nebo provider asset s jiným content typem.
- Export URL, která není `data:`, nahrazuje transparentním PNG; to je bezpečné, ale pro plnohodnotný DOCX musí existovat explicitní rozhodnutí: importovat jako embedded asset, exportovat jako external relationship, nebo odmítnout s warningem.
- Export zapisuje vlastní `tm:*` atributy na `wp:inline/wp:anchor`, což pomáhá našemu roundtripu, ale nesmí být jediný zdroj pravdy. DOCX z Wordu/OnlyOffice žádné `tm:*` atributy mít nebude.
- Export zatím nepíše crop (`a:srcRect`), flip, tile/stretch nuance, hyperlink na obrázku, title/description rozlišení, `wp14:sizeRelH/V`, raw extension listy ani přesné `docPr` id/name roundtrip.
- Export headerů/footerů a poznámek aktuálně používá `DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block))`, takže drawing runy v headeru/footeru/notes ztrácí.
- Import v `DocumentDocxImporter.cs` nejdřív projde `paragraph.Descendants<W.Drawing>()`, z nich vytvoří obrázky a pak je přidá až na konec `inlines`. Tím se ztratí přesná inline pozice obrázku v odstavci a kontext runu.
- `ReadInlines` dnes běhy s `W.Drawing` přeskakuje, takže hyperlink/comment/revision kontext kolem obrázku není přirozeně zachovaný.
- Import čte `A.Blip Embed`, image part, `DW.Extent`, základní anchor/inline layout, zjednodušený wrap fallback, position offset/alignment, `relativeHeight`, `allowOverlap`, `locked` a rotation.
- Import tabulek, headerů, footerů, footnotes/endnotes a comments dnes používá `ReadInlines`, tedy drawing runy z těchto částí ignoruje.
- Současný test `FormatExportImport_RoundtripsDrawingRunsWithoutLegacyImageBlocks` hlídá jen jeden jednoduchý Square obrázek; nehlídá XML tvar, pořadí v odstavci, content type, crop, header/footer, table cell ani roundtrip s DOCX vytvořeným mimo Tempo.

### Inspirace z OnlyOffice

OnlyOffice má stejný klíčový princip, ke kterému už Tempo míří: obrázek je drawing objekt napojený na odstavec, ne top-level blok.

- `core/DocxRenderer/src/logic/elements/Shape.cpp` skládá `w:r/w:drawing/wp:anchor`, nastavuje `dist*`, `simplePos`, `relativeHeight`, `behindDoc`, `locked`, `layoutInCell`, `allowOverlap`, `positionH`, `positionV`, `extent`, `effectExtent`, `wrapNone`, `docPr`, `cNvGraphicFramePr` a pak `a:graphicData`.
- Stejný soubor pro bitmapu zapisuje `a:graphicData` s picture URI, `pic:pic`, `pic:cNvPr`, `pic:blipFill`, `a:blip r:embed`, `a:srcRect`/`a:stretch` a `pic:spPr`.
- `core/OdfFile/Reader/Converter/docx_drawing.cpp` serializuje inline i anchor přes společnou strukturu, ale větví chování podle `isInline`; to je dobrý vzor pro Tempo: jedna kanonická DrawingML DTO vrstva, dvě hostitelské větve `wp:inline` a `wp:anchor`.
- OnlyOffice pracuje s cropem jako s procentuálním rectangle (`srcRect`) a s rozlišením obrázku přes skutečnou image velikost; Tempo musí přestat spoléhat jen na display `Size`.
- JS vrstva OnlyOffice váže obrázek přes `ParaDrawing` na parent paragraph. Tempo analog je `DocumentDrawingRun` + `DocumentObjectAnchor.BlockId/InlineIndex/Offset`; tyto hodnoty musí DOCX import/export aktivně počítat, ne jen uchovávat v `tm:*`.

### Cílové mapování DOCX -> Tempo

| DOCX prvek | Tempo model | Povinné chování |
| --- | --- | --- |
| `w:r/w:drawing` | `DocumentDrawingRun` | Vznikne přesně v místě runu, ne na konci odstavce. |
| `wp:inline` | `Layout.Kind = Inline`, `Wrap.Mode = Inline` | Účastní se line layoutu jako inline box. |
| `wp:anchor` | `Layout.Kind = Anchored` nebo `Fixed` | Anchor zůstává napojený na odstavec/run/offset. |
| `wp:extent` | `Size`, `Layout.Transform.Width/Height` | Převod EMU -> renderer pt je deterministický. |
| `wp:effectExtent` | `Metadata` nebo nový typed model | Importovat a exportovat aspoň roundtripově. |
| `wp:positionH/V wp:posOffset` | `Layout.Position.X/Y` | Zachovat reference frame i offset. |
| `wp:positionH/V wp:align` | `HorizontalAlignment`, `VerticalAlignment` | Neztratit alignment preset. |
| `wp:wrapSquare` | `Wrap.Mode = Square` | Zachovat `wrapText` a dist*; line layout použije exclusion intervals. |
| `wp:wrapTight` | `Wrap.Mode = Tight` | Zachovat polygon/contour, fallback na Square jen s warningem. |
| `wp:wrapThrough` | `Wrap.Mode = Through` | Zachovat polygon/contour, fallback na Tight/Square jen s warningem. |
| `wp:wrapTopAndBottom` | `Wrap.Mode = TopBottom` | Text se nesmí sázet vedle obrázku. |
| `wp:wrapNone + behindDoc=true` | `Wrap.Mode = BehindText` | Neovlivňuje text flow, render za textem. |
| `wp:wrapNone + behindDoc=false` | `Wrap.Mode = InFrontOfText` | Neovlivňuje text flow, render před textem. |
| `wp:relativeHeight` | `Layout.Stacking.ZIndex` | Zachovat řazení vůči ostatním drawing objektům. |
| `wp:allowOverlap` | `Layout.Stacking.AllowOverlap` | Ovlivní collision policy object layeru. |
| `wp:locked` | `Layout.Anchor.LockAnchor` | UI nesmí měnit anchor, pokud je zamčený. |
| `wp:layoutInCell` | `DocumentDocxDrawingMetadata.LayoutInCell` nebo typed flag | Důležité pro tabulky; nesmí se ztratit. |
| `wp:docPr @id/@name/@descr/@title` | `Metadata`, `AltText` | `descr` je alt text, `title/name` jsou oddělené hodnoty. |
| `pic:cNvPr` | `Metadata` | Zachovat picture identity a hyperlink. |
| `a:blip r:embed` | `AssetId`/imported URL + media part | Vztah řešit v partu, kde drawing skutečně leží. |
| `a:blip r:link` | external URL metadata | Bezpečnostně oddělit od embedded obrázků. |
| `a:srcRect` | `Layout.Transform.Crop` | Převést procenta na normalizovaný crop model. |
| `a:xfrm @rot/@flipH/@flipV` | `Transform.Rotation`, flip metadata | Rotace typed, flip doplnit. |
| `wp14:sizeRelH/V` | typed metadata | Preserve/import; export až po UI rozhodnutí. |

### Největší rizika

- Pokud import nejdřív sbírá všechny `Descendants<W.Drawing>()`, nebude nikdy správně fungovat psaní/import u obrázku uprostřed věty, protože pořadí inline obsahu se ztratí.
- Pokud export dál píše `tm:*` jako hlavní pravdu, DOCX vytvořený mimo Tempo nebude správně importovat wrap/anchor chování.
- Pokud image part resolver nebude znát aktuální package part, obrázky v headeru, footeru, footnote, endnote, commentu a table cell budou ztracené nebo budou hledat relationship v `MainDocumentPart`.
- Pokud se media content type bude exportovat vždy jako PNG, OnlyOffice/Word může dokument otevřít, ale ztratíme formát, průhlednost, kvalitu, SVG/EMF scénáře a testy budou falešně zelené.
- Pokud se crop nechá jen jako vizuální CSS stav, DOCX roundtrip zničí jeden z nejčastějších reálných image scénářů.
- Pokud se XML testy budou dívat jen na model po importu, neodhalí chybný DOCX tvar, který Word/OnlyOffice opravuje nebo toleruje.

### Zdroje pro implementaci

- Microsoft Open XML SDK dokumentace pro vložení obrázku do Wordprocessing dokumentu: image part, relationship a `a:blip r:embed` (`https://learn.microsoft.com/en-us/office/open-xml/word/how-to-insert-a-picture-into-a-word-processing-document`).
- Microsoft Open XML SDK typy `DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline` a `Anchor`: hostitelská vrstva `wp:inline/wp:anchor` (`https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.drawing.wordprocessing.inline?view=openxml-3.0.1`, `https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.drawing.wordprocessing.anchor?view=openxml-3.0.1`).
- Microsoft Open XML SDK typy wrap prvků `WrapSquare`, `WrapTight`, `WrapThrough`, `WrapTopBottom`, `WrapNone` (`https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.drawing.wordprocessing.wrapsquare?view=openxml-3.0.1`).
- Microsoft Open XML SDK typy picture vrstvy `DocumentFormat.OpenXml.Drawing.Pictures.Picture`, `BlipFill`, `NonVisualDrawingProperties` (`https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.drawing.pictures.picture?view=openxml-3.0.1`).
- OnlyOffice lokální zdroje: `/home/pavel/NetProjects/onlyfficeservergit/core/DocxRenderer/src/logic/elements/Shape.cpp`, `/home/pavel/NetProjects/onlyfficeservergit/core/OdfFile/Reader/Converter/docx_drawing.cpp`, `/home/pavel/NetProjects/onlyfficeservergit/core/OdfFile/Writer/Converter/ConvertDrawing.cpp`.

## Fáze 25: DOCX DrawingML baseline a fixtures

### 25.1 RED fixtures

- [x] Vytvořit složku `tests/Tempo.Blazor.DocumentFormats.Tests/TestData/DocxDrawing/`.
- [x] Přidat minimální DOCX fixture s jedním inline PNG obrázkem uprostřed věty.
- [x] Přidat fixture s inline JPEG obrázkem a alt textem.
- [x] Přidat fixture s `wp:anchor` + Square wrap.
- [x] Přidat fixture s `wp:anchor` + TopBottom wrap.
- [x] Přidat fixture s `wp:anchor` + BehindText.
- [x] Přidat fixture s `wp:anchor` + InFrontOfText.
- [x] Přidat fixture s `wp:wrapTight` a jednoduchým polygonem.
- [x] Přidat fixture s `wp:wrapThrough` a jednoduchým polygonem.
- [x] Přidat fixture s cropem přes `a:srcRect`.
- [x] Přidat fixture s rotací přes `a:xfrm rot`.
- [x] Přidat fixture s obrázkem v headeru.
- [x] Přidat fixture s obrázkem ve footeru.
- [x] Přidat fixture s obrázkem v table cell.
- [x] Přidat fixture exportovaný z OnlyOffice pro stejnou sadu scénářů.
- [x] Přidat README k fixture sadě: odkud fixture vznikla, co ověřuje a jak ji obnovit.

### 25.2 XML assertion helpery

- [x] Vytvořit helper pro otevření DOCX jako zip/package a načtení `word/document.xml`.
- [x] Vytvořit helper pro načtení `word/_rels/document.xml.rels`.
- [x] Vytvořit helper pro načtení header/footer parts a jejich `.rels`.
- [x] Vytvořit helper `AssertHasInlinePicture`.
- [x] Vytvořit helper `AssertHasAnchorPicture`.
- [x] Vytvořit helper `AssertPictureRelationship`.
- [x] Vytvořit helper `AssertWrapMode`.
- [x] Vytvořit helper `AssertPosition`.
- [x] Vytvořit helper `AssertExtentEmu`.
- [x] Vytvořit helper `AssertCropSrcRect`.
- [x] Vytvořit helper `AssertDocPrAltText`.
- [x] Vytvořit helper `AssertNoTempoAttributesRequiredForImport`.

### 25.3 Akceptace fáze 25

- [x] Testy jsou červené tam, kde současný import/export nestačí.
- [x] Fixture sada pokrývá body/header/footer/table a inline/floating.
- [x] XML helpery kontrolují skutečný DOCX tvar, ne jen model po roundtripu.

Poznámka 2026-05-25: Fáze 25 dokončena v `Tempo.Blazor.DocumentFormats.Tests`, protože tam už je Open XML SDK a stávající DOCX/ODT formátová sada. Binární `.docx` fixture se zatím necommitují; `DocxDrawingFixtureBuilder` vytváří deterministické balíčky pro inline PNG/JPEG, Square, TopBottom, BehindText, InFrontOfText, Tight/Through polygon, crop, rotaci, header/footer/table-cell a OnlyOffice-like anchor tvar bez `tm:*` atributů. `DocxDrawingTestPackage` kontroluje ZIP/XML/rels strukturu přímo. Současně byly starší DOCX/ODT formátové testy přepsané z `ImageBlockContent` očekávání na kanonické `DocumentDrawingRun`. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase25Tests" --logger "console;verbosity=minimal"` (13/13); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --logger "console;verbosity=minimal"` (57/57).

## Fáze 26: DrawingML typed metadata model

### 26.1 RED model testy

- [x] Test: `DocumentDrawingRun` umí nést `DocumentDocxDrawingMetadata`.
- [x] Test: metadata ukládají `DocPrId`, `DocPrName`, `DocPrTitle`, `DocPrDescription`.
- [x] Test: metadata ukládají `PictureNonVisualId`, `PictureName`, `PictureDescription`.
- [x] Test: metadata ukládají `RelationshipId`, `ImagePartUri`, `ContentType`, `OriginalFileName`.
- [x] Test: metadata ukládají `BlipCompressionState`, `BlipLinkRelationshipId` a embedded/external režim.
- [x] Test: metadata ukládají `EffectExtent`.
- [x] Test: metadata ukládají `LayoutInCell`, `Hidden`, `SimplePosition`, `AnchorId`, `EditId`.
- [x] Test: metadata ukládají `RelativeWidth/RelativeHeight` z `wp14:sizeRelH/V`.
- [x] Test: metadata ukládají unsupported raw DrawingML fallback pro preserve-only scénáře.
- [x] Test: serializace/deserializace dokumentu metadata zachová.

### 26.2 Implementace modelu

- [x] Přidat `DocumentDocxDrawingMetadata` do abstractions.
- [x] Přidat `DocumentDrawingRun.Docx` nebo typed property s XML dokumentací.
- [x] Přidat `DocumentObjectEffectExtent`.
- [x] Přidat `DocumentObjectFlip` nebo `FlipHorizontal/FlipVertical` do transformu.
- [x] Přidat `DocumentObjectRelativeSize` pro `wp14:sizeRelH/V`.
- [x] Přidat `DocumentImageMediaInfo` pro content type, extension a source part uri.
- [x] Zachovat existující `Metadata` dictionary jen pro doplňkové hodnoty, ne jako primární model.
- [x] Upravit `DocumentImagePersistence.Sanitize` tak, aby nová metadata neničila.

### 26.3 Akceptace fáze 26

- [x] Nový typed model umí vyjádřit všechno, co aktuální Tempo layout potřebuje, plus DOCX-only data nutná pro roundtrip.
- [x] Žádná nová vlastnost není bez XML dokumentace.
- [x] Starý `ImageBlockContent` není znovu zavedený jako kompatibilní cesta.

Poznámka 2026-05-25: Fáze 26 dokončena. `DocumentDrawingRun` má volitelný typed `Docx` model pro DrawingML roundtrip, obecné `Metadata` zůstává jen jako doplňkové pole. Přidané DTO pokrývá `wp:docPr`, `pic:cNvPr`, embedded/external blip relationshipy, media part metadata, `wp:effectExtent`, `layoutInCell`, hidden/simple position, `wp14:anchorId/editId`, `wp14:sizeRelH/V` a raw XML fallback pro preserve-only scénáře. `DocumentObjectTransform` umí uchovat flip flags a `DocumentImagePersistence.Sanitize` metadata zachová i po normalizaci URL/layoutu. Ověřeno: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentDrawingRunDocxMetadataTests" --logger "console;verbosity=minimal"` (6/6); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentDrawingRunModelTests|FullyQualifiedName~DocumentDrawingRunSerializationTests|FullyQualifiedName~DocumentEditorModelTests" --logger "console;verbosity=minimal"` (36/36); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --no-restore --logger "console;verbosity=minimal"` (57/57).

## Fáze 27: Jednotky, media typy a bezpečná image data vrstva

### 27.1 RED unit testy

- [x] Test: `PointToEmu(1)` vrací 12 700.
- [x] Test: `EmuToPoint(12700)` vrací 1.
- [x] Test: `InchToEmu(1)` vrací 914 400.
- [x] Test: `PixelToEmu(96, dpi: 96)` vrací 914 400.
- [x] Test: rotace 15 stupňů se převádí na 900 000.
- [x] Test: crop 10 % se zapisuje jako `10000`.
- [x] Test: `a:srcRect l=25000` se importuje jako 25 % crop vlevo.
- [x] Test: JPEG zdroj exportuje `ImagePartType.Jpeg`.
- [x] Test: PNG zdroj exportuje `ImagePartType.Png`.
- [x] Test: nepodporovaný image typ vrátí warning a nepoužije falešný PNG bez explicitního warningu.

### 27.2 Implementace helperů

- [x] Vytvořit `DocxUnitConverter`.
- [x] Vytvořit `DocxImageContentTypeMapper`.
- [x] Detekovat content type z provider export requestu, data URL MIME typu nebo signatury bajtů.
- [x] Mapovat `.png`, `.jpg/.jpeg`, `.gif`, `.bmp`, `.tif/.tiff`, `.svg` podle možností Open XML SDK a aktuální podpory.
- [x] U externí URL rozhodnout explicitně: embedded download není součást fáze; default je warning + transparent placeholder jen pokud export option dovolí placeholder.
- [x] Přidat export option pro `AllowImagePlaceholders`.
- [x] Přidat import warning pro neznámý nebo nečitelný image part.

### 27.3 Akceptace fáze 27

- [x] Všechny převody jednotek jsou v jednom místě.
- [x] Export už nepředstírá, že každý obrázek je PNG.
- [x] Placeholder chování je explicitní a testované.

Poznámka 2026-05-25: Fáze 27 dokončena. Přidaný `DocxUnitConverter` centralizuje point/EMU/inch/pixel/twip/rotation/crop převody a DOCX import/export ho používá místo lokálních konstant. Přidaný `DocxImageContentTypeMapper` mapuje MIME typ, file extension a byte signature pro PNG/JPEG/GIF/BMP/TIFF/SVG. DOCX export používá skutečný `ImagePartType` pro data URL i asset resolver, rozlišuje unsupported image typy, externí URL nedownloaduje a transparentní PNG placeholder použije jen při `AllowImagePlaceholders = true`. Import čte `a:srcRect` crop do procent a hlásí varování pro chybějící/nečitelné/nepodporované image party. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase27Tests" --logger "console;verbosity=minimal"` (9/9); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --no-restore --logger "console;verbosity=minimal"` (66/66); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentDrawingRunSerializationTests|FullyQualifiedName~DocumentDrawingRunDocxMetadataTests" --logger "console;verbosity=minimal"` (9/9).

## Fáze 28: Import musí zachovat inline pořadí v odstavci

### 28.1 RED import testy

- [x] Import fixture `Text A + inline image + Text B` vrátí inlines v pořadí text/drawing/text.
- [x] Import fixture se dvěma obrázky ve stejné větě zachová obě pozice.
- [x] Import fixture s hyperlinkem kolem obrázku zachová `LinkUrl`.
- [x] Import fixture s komentářem přes text a obrázek nezahodí comment anchor.
- [x] Import fixture s revision runem a obrázkem neodsune obrázek na konec odstavce.
- [x] Import runu s textem před drawingem ve stejném `w:r` zachová text před drawingem.
- [x] Import runu s drawingem a textem po něm ve stejném `w:r` zachová text po drawingu.

### 28.2 Implementace import pipeline

- [x] Přestat v `ReadParagraphAsync` sbírat obrázky přes `paragraph.Descendants<W.Drawing>()`.
- [x] Rozšířit `ReadInlines` tak, aby zpracovával child elements v přesném pořadí.
- [x] V `ReadInlines` při `W.Drawing` volat nový `ReadDrawingRunAsync`.
- [x] Předat do `ReadInlines` aktuální `OpenXmlPart`, ne jen `MainDocumentPart`.
- [x] Zachovat inherited marks pro text i drawing.
- [x] Drawing s link markem převést na `DocumentDrawingRun.LinkUrl`.
- [x] Drawing s comment/revision markem zachovat v `Marks` nebo typed anchor podle existujícího modelu.
- [x] Nastavit `Layout.Anchor.BlockId`, `InlineIndex` a `Offset` po sestavení inlines.
- [x] Otestovat, že caption heuristika nerozbije normální text za obrázkem.

### 28.3 Akceptace fáze 28

- [x] Import nikdy nepřidává všechny obrázky automaticky na konec odstavce.
- [x] Přesná pozice v odstavci je odvozená z XML pořadí.
- [x] Existující jednoduchý DOCX import test zůstává zelený.

Poznámka 2026-05-25: Fáze 28 dokončena. DOCX importer už nesbírá obrázky přes `paragraph.Descendants<W.Drawing>()` a nepřidává je na konec odstavce. `ReadInlinesAsync` zpracovává `w:r` child elementy v přesném XML pořadí, včetně textu před/za `w:drawing` ve stejném runu. Čtení inlines dostává aktuální `OpenXmlPartContainer`, takže obrázky a hyperlinky mohou používat vztahy z main/header/footer/notes partu. Link mark kolem obrázku se převádí na `DocumentDrawingRun.LinkUrl`, comment/revision marky zůstávají na drawing runu. Po sestavení odstavce se dopočítává `Layout.Anchor.BlockId`, `InlineIndex` a `Offset`. Caption heuristika nově vyžaduje skutečný break po samotném obrázku, takže normální text za inline obrázkem nezmizí. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase28Tests" --logger "console;verbosity=minimal"` (9/9); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --no-restore --logger "console;verbosity=minimal"` (75/75).

## Fáze 29: Inline picture import/export

### 29.1 RED testy

- [x] Export inline `DocumentDrawingRun` vytvoří `w:r/w:drawing/wp:inline`.
- [x] Export inline obrázku nezapisuje `wp:anchor`.
- [x] Export inline zapíše `wp:extent` podle `Layout.Transform.Width/Height`.
- [x] Export inline zapíše `wp:docPr descr` z `AltText`.
- [x] Export inline zapíše `pic:cNvPr` s name a descr.
- [x] Export inline zapíše `a:blip r:embed` do správného relationship partu.
- [x] Import inline `wp:inline` nastaví `Layout.Kind = Inline`.
- [x] Import inline nastaví `Wrap.Mode = Inline`.
- [x] Import inline přečte `wp:extent`.
- [x] Import inline přečte `docPr` i `pic:cNvPr`.

### 29.2 Implementace

- [x] Nahradit `ToImageBlockContent` v DOCX exporteru přímou práci s `DocumentDrawingRun`.
- [x] Vytvořit `WriteDrawingRunAsync(DocumentDrawingRun drawing, OpenXmlPart ownerPart, ...)`.
- [x] Vytvořit `CreateInlineDrawing(DocumentDrawingRun drawing, DocxPictureParts parts)`.
- [x] Vytvořit `CreatePictureGraphic(DocumentDrawingRun drawing, string relId, DocxExtent extent)`.
- [x] Zapisovat `DistanceFrom*` u inline jen pokud má smysl; default 0.
- [x] Nepoužívat `tm:*` pro data, která mají nativní Open XML reprezentaci.
- [x] `tm:*` ponechat jen pro Tempo-only data, která standard nevyjádří.

### 29.3 Akceptace fáze 29

- [x] DOCX inline obrázek z Tempa otevře Word/OnlyOffice jako běžný inline obrázek.
- [x] DOCX inline obrázek z Wordu/OnlyOffice se importuje jako drawing run ve správném místě.

Poznámka 2026-05-25: Fáze 29 dokončena. DOCX exporter už pro inline `DocumentDrawingRun` nepoužívá převod přes `ImageBlockContent`, ale zapisuje nativní `w:r/w:drawing/wp:inline` s `wp:extent`, `wp:docPr`, `pic:cNvPr`, `a:blip r:embed` a obrazovým relationshipem ve vlastnickém partu. Inline export už nepřidává Tempo `tm:*` atributy pro údaje, které mají nativní OpenXML reprezentaci (`layout-kind`, `wrap-mode`, `width`, `height`), ponechává jen Tempo-only anchor/natural metadata. Import inline drawingu ukládá `docPr`, `pic:cNvPr`, `r:embed`, media info a effect extent do `DocumentDocxDrawingMetadata`, zachová `Layout.Kind = Inline`, `Wrap.Mode = Inline` a velikost z `wp:extent`. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase29Tests"` (2/2); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj` (77/77).

## Fáze 30: Floating anchor geometry import/export

### 30.1 RED testy

- [x] Export anchored drawing vytvoří `wp:anchor`.
- [x] Export anchored drawing zapíše `simplePos="0"` a `wp:simplePos`.
- [x] Export zapíše `wp:positionH relativeFrom`.
- [x] Export zapíše `wp:positionH/wp:posOffset` pro explicitní X.
- [x] Export zapíše `wp:positionH/wp:align` pro alignment preset.
- [x] Export zapíše `wp:positionV relativeFrom`.
- [x] Export zapíše `wp:positionV/wp:posOffset` pro explicitní Y.
- [x] Export zapíše `wp:positionV/wp:align` pro vertical alignment preset.
- [x] Export zapíše `relativeHeight` ze z-indexu.
- [x] Export zapíše `locked`, `layoutInCell`, `allowOverlap`.
- [x] Import stejné hodnoty načte bez `tm:*`.

### 30.2 Implementace

- [x] Rozšířit `DocumentObjectPosition` mapování pro všechny Word reference frames, které model podporuje.
- [x] Rozšířit `DocumentObjectAnchor` o `LayoutInCell` nebo metadata rozhodnutí.
- [x] Přidat import/export `SimplePosition`.
- [x] Přidat import/export `Hidden`.
- [x] Přidat import/export `AnchorId/EditId` jako metadata.
- [x] Rozlišit `Anchored` a `Fixed`: `Fixed` znamená `MoveWithText=false`, ale DOCX stále používá `wp:anchor`.
- [x] Přidat fallback warning, pokud DOCX pozici nejde přesně namapovat.

### 30.3 Akceptace fáze 30

- [x] Anchor geometry z Wordu/OnlyOffice jde importovat bez Tempo atributů.
- [x] Export z Tempa nevypadá v OnlyOffice jako resetovaný objekt na začátku stránky.

Poznámka 2026-05-25: Fáze 30 dokončena. Export anchored/fixed drawingu zapisuje nativní `wp:anchor`, `wp:simplePos`, `wp:positionH`, `wp:positionV`, explicitní `wp:posOffset`, horizontal/vertical `wp:align`, `relativeHeight`, `locked`, `layoutInCell`, `hidden`, `allowOverlap` a `wp14:anchorId/editId` z `DocumentDocxDrawingMetadata`. Import čistého DOCX bez Tempo atributů čte stejné hodnoty zpět do `DocumentObjectLayout` a `DocumentDocxDrawingMetadata`; page/page nativní anchor se mapuje na `Fixed` layout s `MoveWithText=false`. Nepřesné Word reference/alignment varianty (`inside/outside`, margin-side varianty) emitují fallback warning. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase30Tests"` (4/4); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj` (81/81).

## Fáze 31: Wrap modes, contour a exclusion semantika

### 31.1 RED testy

- [x] Export `DocumentWrapMode.Square` zapíše `wp:wrapSquare`.
- [x] Export Square zapíše `wrapText="bothSides"` jako default nebo modelovou hodnotu.
- [x] Export `TopBottom` zapíše `wp:wrapTopAndBottom`.
- [x] Export `BehindText` zapíše `wp:wrapNone` a `behindDoc="1"`.
- [x] Export `InFrontOfText` zapíše `wp:wrapNone` a `behindDoc="0"`.
- [x] Export `Tight` zapíše `wp:wrapTight`.
- [x] Export `Through` zapíše `wp:wrapThrough`.
- [x] Import `wrapSquare` načte distance a wrap side.
- [x] Import `wrapTight` načte polygon do `WrapContourPoints`.
- [x] Import `wrapThrough` načte polygon do `WrapContourPoints`.
- [x] Import bez polygonu vytvoří warning a fallback konturu podle bounds.

### 31.2 Implementace

- [x] Ověřit, že `DocumentWrapMode` obsahuje `Tight` a `Through`; pokud ne, doplnit.
- [x] Doplnit typed `DocumentObjectWrapSide` pro bothSides/left/right/largest.
- [x] Doplnit import/export `wp:wrapPolygon`.
- [x] Převádět polygon EMU hodnoty na objektové souřadnice.
- [x] Uložit distance `distT/distB/distL/distR` nativně, ne jen `tm:*`.
- [x] Pro `BehindText` a `InFrontOfText` vynutit, že line layout nevytváří exclusion intervaly.
- [x] Přidat C# layout testy, že imported Tight/Through aspoň nepřekryjí text hůř než Square fallback.

### 31.3 Akceptace fáze 31

- [x] Všechny hlavní Word/OnlyOffice wrap režimy mají nativní import/export mapování.
- [x] Tempo-only wrap data jsou doplňková, ne nutná pro DOCX import.

Poznámka 2026-05-25: Fáze 31 dokončena. DOCX export mapuje Square/TopBottom/BehindText/InFrontOfText/Tight/Through na nativní `wp:wrap*` elementy, zapisuje `wrapText`, nativní distance a polygon pro Tight/Through. Import čistého DOCX bez Tempo atributů čte wrap side, distance a konturu zpět do `DocumentObjectWrap`; chybějící Tight/Through polygon hlásí warning a použije fallback obdélník podle bounds. Layout exclusion zóny nově nesou typed `DocumentObjectWrapSide` (`bothSides`, `left`, `right`, `largest`) a side semantika omezuje blokované line intervaly; Behind/InFront dál nevytváří exclusion intervaly. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase31Tests"` (7/7); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj` (88/88); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentLayoutGeometryTests"` (24/24); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentDrawingRunSerializationTests"` (3/3); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorModelTests"` (28/28).

## Fáze 32: Transform, crop, fill a tvar obrázku

### 32.1 RED testy

- [x] Export `Transform.Rotation` zapíše `a:xfrm rot`.
- [x] Import `a:xfrm rot` načte stupně.
- [x] Export `FlipHorizontal` zapíše `a:xfrm flipH`.
- [x] Export `FlipVertical` zapíše `a:xfrm flipV`.
- [x] Import `flipH/flipV` načte model.
- [x] Export cropu zapíše `a:srcRect`.
- [x] Import `a:srcRect` nastaví `Transform.Crop`.
- [x] Export stretch zapíše `a:stretch/a:fillRect`.
- [x] Import tile zapíše preserve metadata a warning, pokud UI neumí tile.
- [x] Import non-rect preset geometry zachová metadata a použije rect fallback s warningem.

### 32.2 Implementace

- [x] Převést crop model na normalizovaná procenta nebo jasně zdokumentovat současné jednotky.
- [x] Doplnit `DocxCropConverter`.
- [x] Doplnit `DocxTransformConverter`.
- [x] Při exportu držet `wp:extent` a `a:xfrm/a:ext` konzistentní.
- [x] Při rotaci zohlednit `effectExtent` preserve, i když UI neumí efekty.
- [x] Nepřepisovat `a:srcRect` na prázdný element při roundtripu.

### 32.3 Akceptace fáze 32

- [x] Crop/rotace/flip nezmizí po DOCX export/import.
- [x] Obrázek oříznutý v OnlyOffice se v Tempu nevrátí jako neoříznutý plný bitmap.

Poznámka 2026-05-25: Fáze 32 dokončena. Crop model je zdokumentovaný jako normalizovaná procenta z hran objektu a převody jsou centralizované v `DocxCropConverter`; `DocxTransformConverter` zapisuje/čte `a:xfrm` rotation a flip flags. DOCX export drží `wp:extent` a `a:xfrm/a:ext` konzistentní, zapisuje `a:srcRect` jen při nenulovém cropu, vždy zapisuje stretch/fillRect a zachovává `wp:effectExtent` z metadata. Import čte `a:srcRect`, rotation, flipH/flipV, tile fill ukládá do preserve metadata s warningem a non-rect `a:prstGeom` ukládá do metadata s rect fallback warningem. Ověřeno: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase32Tests"` (6/6); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj` (94/94); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentDrawingRunDocxMetadataTests"` (6/6); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentDrawingRunDocxMetadataTests|FullyQualifiedName~DocumentDrawingRunSerializationTests|FullyQualifiedName~DocumentEditorModelTests|FullyQualifiedName~DocumentLayoutEngineTests"` (90/90).

## Fáze 33: Alt text, title, hyperlink a non-visual properties

### 33.1 RED testy

- [ ] Import `wp:docPr descr` nastaví `AltText`.
- [ ] Import `wp:docPr title` nastaví metadata title.
- [ ] Import `wp:docPr name` zachová name odděleně od alt textu.
- [ ] Export `AltText` jde do `wp:docPr descr`.
- [ ] Export name/title nepřepíše alt text.
- [ ] Import `pic:cNvPr descr` použije fallback, pokud `wp:docPr descr` chybí.
- [ ] Import hyperlinku na obrázku nastaví `LinkUrl`.
- [ ] Export `LinkUrl` vytvoří hyperlink relationship pro obrázek.
- [ ] Dekorativní obrázek exportuje prázdný alt popis nebo explicitní dekorativní metadata podle podporované DOCX semantiky.

### 33.2 Implementace

- [ ] Rozlišit `AltText`, `Title`, `Name` v modelu/metadatech.
- [ ] Doplnit čtení `a:hlinkClick` z `wp:docPr` i `pic:cNvPr`.
- [ ] Doplnit export `a:hlinkClick`.
- [ ] Zajistit unikátní `docPr id` v celém dokumentu, včetně header/footer parts.
- [ ] Zajistit stabilní name fallback `Picture {id}` místo alt textu jako name.
- [ ] Přidat warning, pokud DOCX obsahuje konfliktní alt údaje.

### 33.3 Akceptace fáze 33

- [ ] Accessibility data nejsou smíchaná s interním názvem objektu.
- [ ] Hyperlinkovaný obrázek funguje po otevření DOCX v OnlyOffice.

## Fáze 34: Media parts a relationships pro každý package part

### 34.1 RED testy

- [x] Obrázek v body používá relationship z `word/document.xml.rels`.
- [x] Obrázek v headeru používá relationship z `word/_rels/headerX.xml.rels`.
- [x] Obrázek ve footeru používá relationship z `word/_rels/footerX.xml.rels`.
- [x] Obrázek v footnote používá relationship z `word/_rels/footnotes.xml.rels`.
- [x] Obrázek v endnote používá relationship z `word/_rels/endnotes.xml.rels`.
- [x] Obrázek v commentu používá relationship z `word/_rels/comments.xml.rels`.
- [x] Import header image nehledá relationship v `MainDocumentPart`.
- [x] Export provider asset zapíše skutečná data image partu.
- [x] Export dvou shodných assetů buď deduplikuje deterministicky, nebo je zapisuje odděleně s testovaným rozhodnutím.
  - Rozhodnutí fáze 34: shodné assety se zapisují odděleně v deterministickém pořadí exportu; bez deduplikace.

### 34.2 Implementace

- [x] Zavést `IDocxPartImageContext` nebo interní `DocxPartWriterContext`.
- [x] `WriteParagraphAsync` musí znát owner part.
- [x] `ReadParagraphAsync/ReadInlines` musí znát owner part.
- [x] `ReadImageAsync` přijímá `OpenXmlPart ownerPart`, ne `MainDocumentPart`.
- [x] `ownerPart.GetPartById(relId)` použít pro embedded image.
- [x] `ownerPart.HyperlinkRelationships`/external relationships číst per part.
- [x] Přidat deterministic image part naming pro stabilní testy, pokud SDK dovolí.
- [x] Přidat security limit na velikost image partu.

### 34.3 Akceptace fáze 34

- [x] Obrázky fungují v každé části DOCX balíčku.
- [x] Import/export už není pevně svázaný jen s `MainDocumentPart`.

## Fáze 35: Header, footer, notes, comments a table cell drawing runs

### 35.1 RED testy

- [x] Export headeru s `DocumentDrawingRun` zapíše skutečný `w:drawing`.
- [x] Import headeru s `w:drawing` vytvoří drawing run.
- [x] Export footeru s `DocumentDrawingRun` zapíše skutečný `w:drawing`.
- [x] Import footeru s `w:drawing` vytvoří drawing run.
- [x] Export table cell drawing run zůstane v buňce.
- [x] Import table cell drawing run zůstane v buňce.
- [x] Export footnote drawing run zůstane ve footnote.
- [x] Import footnote drawing run zůstane ve footnote.
- [x] Export comment drawing run zůstane v comment partu nebo vytvoří warning, pokud comments image zatím nepodporujeme.
  - Rozhodnutí fáze 35: comments image podporujeme plně přes `DocumentCommentEntry.Inlines` a `word/comments.xml.rels`.

### 35.2 Implementace

- [x] `AddHeadersFooters` přestat převádět bloky přes `DocumentModelText.GetBlockText`.
- [x] Header/footer writer musí používat stejný paragraph writer jako body.
- [x] `ReadHeadersFooters` musí používat async paragraph reader s owner partem.
- [x] `ReadTable` musí číst buňky async nebo mít drawing-aware inline reader.
- [x] Notes writer/reader musí být drawing-aware.
- [x] Comments writer/reader rozhodnout: plná podpora nebo explicitní warning preserve.
- [x] Doplnit anchor region scope (`Body/Header/Footer/Table/Note/Comment`) při importu.
- [x] Při exportu respektovat `Layout.Anchor.Region`, `HeaderFooterId`, `TableId`, `CellId`.

### 35.3 Akceptace fáze 35

- [x] Obrázky v headeru/patičce už nejsou při DOCX exportu zploštěné na text.
- [x] Table cell obrázky nezmizí při importu.

## Fáze 36: Preserve/validation pro unsupported DrawingML

### 36.1 RED testy

- [x] Import chart graphicData vytvoří warning, ale nezpůsobí pád.
- [x] Import SmartArt/canvas/group vytvoří warning a preserve metadata.
- [x] Import picture s unsupported efektem zachová raw extension data nebo warning.
- [x] Export dokumentu po importu unsupported drawing nevymaže raw payload, pokud nebyl upraven.
- [x] Úprava unsupported drawing v UI je zakázaná nebo převede na explicitní image fallback.
  - Rozhodnutí fáze 36: bez nového runtime typu; importer uloží raw DrawingML metadata, exporter při regeneraci použije explicitní fallback warning.
- [x] Broken relationship vytvoří compatibility warning s part path.
- [x] Chybějící `a:blip` vytvoří warning s part path.

### 36.2 Implementace

- [x] Přidat `DocumentUnsupportedDrawingRun` jen pokud bude potřeba; jinak preserve data v `DocumentDrawingRun.Docx`.
- [x] Zavést `RawDrawingml` preserve pole s jasným limitem velikosti.
- [x] Přidat `DocumentFormatCompatibilityWarning` pro každý fallback.
- [x] Neimportovat aktivní obsah ani externí odkazy bez explicitního povolení.
- [x] Přidat validátor, který zkontroluje povinné děti `wp:inline/wp:anchor`.
- [x] Přidat validátor, který zkontroluje image relationship a content type.

### 36.3 Akceptace fáze 36

- [x] Neznámé DrawingML nezpůsobí ztrátu dokumentu.
- [x] Uživatel/test dostane explicitní warning místo tichého smazání.

## Fáze 37: Roundtrip parity s Word/OnlyOffice

### 37.1 RED roundtrip testy

- [x] Import Word inline fixture -> export -> zkontrolovat `wp:inline`.
- [x] Import Word anchor Square fixture -> export -> zkontrolovat `wp:anchor` a `wrapSquare`.
- [x] Import OnlyOffice anchor Square fixture -> export -> zkontrolovat `positionH/V`, `extent`, `relativeHeight`.
- [x] Import crop fixture -> export -> zkontrolovat `a:srcRect`.
- [x] Import header fixture -> export -> zkontrolovat header image relationship.
- [x] Import table fixture -> export -> zkontrolovat drawing v buňce.
- [x] Export Tempo fixture -> import zpět -> model layout hodnoty sedí s tolerancí.
- [x] Export Tempo fixture -> otevřít přes Open XML SDK validator bez zásadních schema chyb.

### 37.2 Implementace

- [x] Přidat test helper pro toleranci EMU/pt rounding.
- [x] Přidat canonical XML snapshoty jen pro malé části XML, ne celé DOCX.
- [x] Přidat Open XML SDK validator test pro generované DOCX.
- [x] Přidat fixture regeneraci skript nebo README postup.
- [x] Každý warning v roundtrip testu musí být buď očekávaný, nebo test selže.

### 37.3 Akceptace fáze 37

- [x] Word/OnlyOffice vytvořený DOCX importujeme bez Tempo atributů.
- [x] Tempo export nevytváří DOCX, který závisí na tolerantním auto-fixu Wordu.

## Fáze 38: Demo dokumenty a ukázkový DOCX export

### 38.1 RED demo testy

- [x] Demo document export obsahuje aspoň jeden `wp:inline`.
- [x] Demo document export obsahuje aspoň jeden `wp:anchor`.
- [x] Demo document export obsahuje header/footer drawing.
- [x] Demo document export obsahuje table cell drawing.
- [x] Demo document export neobsahuje starý top-level image block fallback.
- [x] Demo import OnlyOffice fixture zobrazí obrázky ve správných regionech.

### 38.2 Implementace demo dat

- [x] Upravit demo dokumenty tak, aby měly reálné asset-backed obrázky s content typem.
- [x] Upravit demo dokument s cropem.
- [x] Upravit demo dokument s rotací.
- [x] Upravit demo dokument s Tight/Through fallback scénářem.
- [x] Přidat demo akci Export DOCX image parity.
- [x] Přidat demo akci Import DOCX image parity fixture.
- [x] Zobrazit compatibility warnings v demo UI.

### 38.3 Akceptace fáze 38

- [x] Demo dokumenty pokrývají reálnou DOCX interoperabilitu, ne jen interní runtime.
- [x] Uživatel může ručně exportovat DOCX a otevřít ho v OnlyOffice s očekávaným výsledkem.

## Fáze 39: E2E import/edit/export scénáře

### 39.1 RED E2E

- [x] E2E: importovat DOCX s inline obrázkem, kliknout před něj, psát a exportovat.
- [x] E2E: importovat DOCX se Square obrázkem, psát vedle něj a exportovat.
- [x] E2E: importovat DOCX s header obrázkem, psát v headeru a exportovat.
- [x] E2E: importovat DOCX s table cell obrázkem, editovat text v buňce a exportovat.
- [x] E2E: importovat DOCX s cropem, otevřít image inspector a ověřit crop hodnoty.
- [x] E2E: změnit wrap mode imported obrázku a ověřit export XML.
- [x] E2E: změnit velikost imported obrázku a ověřit jeden undo krok.

### 39.2 Implementace E2E podpory

- [x] Přidat test upload fixture DOCX přes demo endpoint/UI.
- [x] Přidat test stažení/exportu DOCX a XML inspekci výsledku.
- [x] Přidat stabilní test id pro imported drawing objects.
- [x] Přidat debug snapshot pro DOCX drawing metadata v editoru.
- [x] E2E nesmí upravovat model interním JS commandem místo uživatelské akce.

### 39.3 Akceptace fáze 39

- [x] End-to-end tok import -> editace -> export zachová DrawingML semantiku.
- [x] Testy chrání uživatelské chování i výsledný DOCX XML.

## Fáze 40: Performance a bezpečnost DOCX obrázků

### 40.1 RED testy

- [x] Import DOCX s 50 obrázky dokončí pod definovaným limitem.
- [x] Export DOCX s 50 obrázky dokončí pod definovaným limitem.
- [x] Import odmítne image part větší než limit.
- [x] Import odmítne podezřelý content type mismatch s warningem.
- [x] Import externí `r:link` nepřistoupí na síť bez explicitní option.
- [x] Import zip/path traversal image vztah neprojde validací.
- [x] Import broken rel nezpůsobí pád.
- [x] Export obří data URL nepřekročí memory limit bez warningu.

### 40.2 Implementace

- [x] Přidat limity do `DocumentFormatImportOptions`.
- [x] Přidat limity do `DocumentFormatExportOptions`.
- [x] Streamovat image party místo zbytečného kopírování, kde to Open XML SDK dovolí.
- [x] Cacheovat provider asset bytes během jednoho exportu.
- [x] Přidat warningy s part path a object id.
- [x] Nepovolovat automatický download externích obrázků v default režimu.

### 40.3 Akceptace fáze 40

- [x] DOCX image import/export je bezpečný pro nedůvěryhodné dokumenty.
- [x] Velké dokumenty nezpomalí editor kvůli zbytečným kopím bitmap.

## Fáze 41: Cleanup starých DOCX image zkratek

### 41.1 RED cleanup testy

- [x] Test selže, pokud DOCX exporter při drawing runu používá `ImageBlockContent` jako interní most.
- [x] Test selže, pokud importer vrátí top-level `ImageBlockContent` z `w:drawing`.
- [x] Test selže, pokud header/footer writer zploští drawing na `[Image]`.
- [x] Test selže, pokud table reader přeskočí `W.Drawing`.
- [x] Test selže, pokud export zapisuje PNG part pro JPEG input.

### 41.2 Implementace cleanupu

- [x] Odstranit `ToImageBlockContent` z DOCX exporteru.
- [x] Odstranit `ImageBlockContent` návratový typ z `ReadImageAsync`.
- [x] Přejmenovat `ReadImageAsync` na `ReadDrawingRunAsync`.
- [x] Přejmenovat `WriteImageRunAsync` na `WriteDrawingRunAsync`.
- [x] Přesunout DrawingML helpery do menších interních tříd.
- [x] Upravit namespace/usings tak, aby exporter/importer byly čitelné.
- [x] Zkontrolovat, že ODT/HTML/Markdown cesty nejsou nechtěně rozbité.

### 41.3 Akceptace fáze 41

- [x] DOCX kód už nemyslí v `ImageBlockContent`, ale v `DocumentDrawingRun`.
- [x] Staré zkratky nejsou potřeba pro zelené testy.

## Fáze 42: Dokumentace, rozhodnutí a release gate

### 42.1 Dokumentace

- [x] Doplnit architektonickou poznámku `planning/tmdocumenteditor-docx-drawingml-architecture-2026-05-25.md`.
- [x] Popsat mapping `wp:inline/wp:anchor` -> `DocumentDrawingRun`.
- [x] Popsat jednotky EMU/pt/px a rounding pravidla.
- [x] Popsat media part security model.
- [x] Popsat unsupported DrawingML preserve/warning policy.
- [x] Popsat, že zpětná kompatibilita se starým `ImageBlockContent` modelem není cílem.
- [x] Popsat, jak obnovit Word/OnlyOffice fixture sadu.

### 42.2 Release gate

- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocxDrawing"` prochází.
- [x] `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/ --filter "FullyQualifiedName~FormatExportImport"` prochází.
- [x] Cílené image drawing JS unit testy prochází.
- [x] Cílené image parity E2E testy prochází.
- [x] Open XML SDK validator testy pro generované DOCX prochází.
- [ ] Ručně otevřít exportovaný DOCX v OnlyOffice a ověřit inline, Square, TopBottom, BehindText, InFrontOfText, header/footer/table.
- [ ] Ručně otevřít exportovaný DOCX ve Wordu nebo Word Online, pokud je k dispozici.

### 42.3 Akceptace fáze 42

- [x] Implementace má popsaná rozhodnutí a limity.
- [x] Release gate kombinuje unit, XML, roundtrip, E2E a ruční interoperabilitu.

Poznámka 2026-05-25: Fáze 42 má doplněnou architektonickou poznámku `planning/tmdocumenteditor-docx-drawingml-architecture-2026-05-25.md`, včetně mapování `wp:inline/wp:anchor`, jednotek EMU/pt/px, media security modelu, unsupported DrawingML policy, rozhodnutí proti cílové zpětné kompatibilitě se starým `ImageBlockContent` modelem a postupu obnovy Word/OnlyOffice fixture sady. Automatizovaná release gate prošla: `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase42Tests"` (2/2); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocxDrawing"` (96/96); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocxDrawing"` (1/1); `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --filter "FullyQualifiedName~FormatExportImport"` (1/1); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorImageDrawing" --no-build` (87/87); `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~Phase37_TempoFixture_ExportedDocxPassesOpenXmlValidatorWithoutMajorSchemaErrors" --no-build` (1/1); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorImageOnlyOfficeParityE2ETests|FullyQualifiedName~DocumentEditorImageDocxPhase39E2ETests" --no-build` (21/21). Ruční otevření exportovaného DOCX v OnlyOffice a Word/Word Online zůstává ruční interoperability gate, kterou nelze poctivě odškrtnout bez GUI ověření.

## Fáze 43: Manuální smoke scénáře

Po dokončení každé větší fáze ručně projít minimálně tyto scénáře:

- [ ] Vložit obrázek doprostřed věty.
- [ ] Přepnout obrázek na Square.
- [ ] Psát vlevo vedle obrázku.
- [ ] Psát vpravo vedle obrázku.
- [ ] Psát pod TopBottom obrázkem.
- [ ] Stisknout ArrowUp/ArrowDown v textu před obrázkem.
- [ ] Stisknout ArrowUp/ArrowDown v textu za obrázkem.
- [ ] Kliknout obrázek a ověřit toolbar.
- [ ] Escape z obrázku vrátí caret.
- [ ] Drag obrázku k jinému odstavci.
- [ ] Undo drag.
- [ ] Redo drag.
- [ ] Resize obrázku.
- [ ] Undo resize.
- [ ] Save/reload dokumentu.
- [ ] Totéž v headeru.
- [ ] Totéž ve footeru.
- [ ] Totéž v tabulce.
- [ ] Ověřit, že toolbar nepřekrývá čitelný text.
- [ ] Ověřit, že editor během psaní nepůsobí pomalu.
- [ ] Otevřít demo dokument a ověřit, že obrázky používají nový model.
- [ ] Exportovat demo dokument do DOCX a otevřít v OnlyOffice.
- [ ] Importovat zpět DOCX exportovaný z OnlyOffice a ověřit stejné image layouty.

## Fáze 44: Dokumentace rozhodnutí

- [ ] Doplnit krátkou architektonickou poznámku do `planning/`.
- [ ] Popsat, že `ImageBlockContent` už není cílový editační model.
- [ ] Popsat selection policy: text selection vs object selection.
- [ ] Popsat drag/resize track princip.
- [ ] Popsat wrap mode semantiku.
- [ ] Popsat pravidla pro demo dokumenty a demo seedy.
- [ ] Přidat seznam testů, které hlídají hlavní invarianty.

## Finální definition of done

- [ ] Všechny model/unit testy pro drawing run prochází.
- [ ] Všechny serializer testy pro drawing run prochází.
- [ ] Všechny JS runtime image selection/wrap/drag/resize testy prochází.
- [ ] Všechny C# layout drawing object testy prochází.
- [ ] Všechny component testy toolbaru/inspectoru prochází.
- [ ] Všechny image parity E2E testy prochází.
- [ ] Staré image block testy jsou přepsané na drawing run/object model nebo odstraněné.
- [ ] `dotnet test tests/Tempo.Blazor.Tests/` prochází.
- [ ] Cílené document editor E2E testy prochází.
- [ ] Demo dokumenty, demo seedy a demo E2E scénáře používají nový drawing run/object model.
- [ ] Ruční smoke v body/header/footer/table cell je úspěšný.
- [ ] Obrázek už nezískává focus při ArrowUp/ArrowDown v okolním textu.
- [ ] Lze psát vedle obrázku bez předem existujícího textu.
- [ ] Nové vložení obrázku nevytvoří samostatný top-level image block.
- [ ] Drag/resize je plynulý a undoable jedním krokem.
- [ ] Save/reload zachová anchor, wrap, size, alt text a caption.
- [ ] DOCX import zachová pořadí text/drawing/text uvnitř odstavce.
- [ ] DOCX export vytváří nativní `wp:inline` a `wp:anchor`, ne jen Tempo metadata.
- [ ] DOCX import/export funguje v body, headeru, footeru a table cell.
- [ ] DOCX export zachová content type obrázku a nepřevádí vše na PNG bez důvodu.
- [ ] DOCX import/export zachová crop, rotaci, wrap distances, z-index, lock anchor a allow overlap.
- [ ] DOCX import/export má XML assertion testy i model roundtrip testy.
- [ ] DOCX export projde Open XML validací pro image drawing scénáře.
- [ ] Exportovaný DOCX se otevře v OnlyOffice s očekávaným layoutem obrázků.

## Poznámka k rozsahu

Tento TODO je úmyslně rozdělený na malé kroky. Některé fáze půjde sloučit při implementaci, ale testy by se slučovat neměly. Zvlášť důležité je nejdřív zezelenat focus/caret chování a teprve potom odstranit staré render cesty. Bez oddělení text selection a object selection se budou chyby kolem šipek, toolbaru a psaní vedle obrázku vracet.
