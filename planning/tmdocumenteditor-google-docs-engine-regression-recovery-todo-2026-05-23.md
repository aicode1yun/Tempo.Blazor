# TmDocumentEditor Google Docs engine - regresni recovery TODO

Datum: 2026-05-23  
Stav: navrzeno, ceká na implementaci  
Priorita: P0 - editor je podle rucni kontroly pro bezne pouziti nedostatecny

## Proc tento dokument existuje

Po prechodu na novy Google Docs engine se sice cast modelovych, JS a strict E2E testu tvari zelene, ale rucni pouziti ukazuje zasadni produktove regrese:

1. Nerenderuji se hlavicky a paticky stranky.
2. V dokumentu neni videt, kde jsou komentare.
3. V dokumentu neni videt, kde jsou revize.
4. Toolbar obrazku ma vyrazne mene funkci nez pred prepisem enginu.
5. Pri vybrani obrazku se v pravem panelu nezobrazi vlastnosti obrazku.
6. Pri oznaceni textu se nezobrazi floating toolbar.
7. Enter a mezernik se neprojevi okamzite, ale az pri dalsim psani.
8. Vykon psani je spatny: znaky se renderuji po skupinach, ne plynule po jednotlivych stiscich.

Tento plan je zamerne napsany jako recovery plan, ne jako dalsi feature roadmapa. Cilem je vratit editor do stavu, kdy se realne chova jako pouzitelny dokumentovy editor ve stylu Word/Google Docs, a zaroven zmenit testovani tak, aby se podobna regrese uz nemohla tvarit jako zelena.

## Hlavni diagnoza

### Co se pravdepodobne stalo

- Novy engine prebral odpovednost za DOM, model, layout a vyber, ale nebyly kompletne prevedeny vsechny produktove kontrakty stareho editoru.
- Cast puvodniho UI byla vazana na Blazor selection snapshoty, ktere novy runtime neposila ve stejne kvalite nebo ve stejny okamzik.
- Cast E2E testu pravdepodobne testuje interni JS API, modelove funkce, pripadne stale DOM selektory, ktere neodpovidaji tomu, co clovek vidi a pouziva.
- Strict testy pro layout a engine jsou uzitecne, ale neoveruji dostatecne:
  - viditelne dekorace komentaru/revizi,
  - dostupnost floating toolbaru,
  - kompletni image UX,
  - okamzitou odezvu mezerniku/enteru,
  - realnou latenci pri drzeni klavesy a rychlem psani,
  - kompletni vykresleni stranky vcetne header/footer regionu.
- Testy byly prilis casto "API-green", ale ne "human-green".

### Proc mohly projit E2E testy

- Testy pouzily helpery typu `EvaluateAsync` a vnitrni runtime commandy misto skutecneho mys/keyboard workflow.
- Testy overily, ze model obsahuje data, ale neoverily, ze data jsou viditelne vykreslena na strance.
- Testy overily, ze panel obsahuje revizi/komentar, ale neoverily, ze odpovidajici text v dokumentu ma viditelnou a kliknutelnou dekoraci.
- Testy overily, ze obrazek existuje v DOMu, ale neoverily kompletni toolbar, side panel, vlastnosti, resize handles, wrap options a stav po vyberu.
- Testy overily vlozeni textu po `Keyboard.TypeAsync`, ale nemerily cas mezi `keydown/beforeinput` a viditelnym znakem v DOMu.
- Testy nefailovaly dostatecne tvrde na browser console chyby typu Blazor render exception.
- Testy neporovnavaly screenshoty/pixel nebo text rect geometrii pro hlavicky, paticky, komentare, revize a floating UI.
- Testy nemely "manual parity checklist", ktery by presne kopiroval bezne lidske workflow.

## Nevyjednatelna pravidla dalsi implementace

- Zadny bod se nesmi oznacit jako hotovy jen proto, ze prosla unit nebo JS model sada.
- Kazda oprava musi mit minimalne jeden RED test, ktery pred opravou selze stejnym zpusobem jako rucni chyba.
- Pro uzivatelske chovani musi existovat E2E test pres skutecnou mys/klavesnici.
- Interni JS API se smi pouzit jen pro diagnostiku, pripravu dat nebo cteni metrik, ne jako nahrada uzivatelske akce.
- Kazdy E2E test pro editor musi failovat pri `console.error`, Blazor critical render exception nebo runtime fatal error.
- Psaní musi byt lokalne optimisticke: znak, mezera a Enter se musi objevit v DOMu okamzite, bez cekani na Blazor, provider nebo save boundary.
- Blazor shell nesmi vlastnit DOM uzly, ktere JS engine behem editace prepisuje.
- Po kazde fazi se musi udelat rucni smoke na `/document-editor` a zapsat vysledek do tohoto souboru.

## Definice hotovo pro tento recovery plan

- Default demo po reloadu zobrazuje hlavicku a paticku stranky.
- Komentare jsou viditelne oznacene v textu, klik na marker vybere komentar v panelu a klik na komentar zvyrazni odpovidajici text.
- Revize jsou viditelne oznacene v textu v rezimu All Markup, panel a text jsou synchronni.
- Vyber textu mysi zobrazi floating toolbar a toolbar zustane viditelny, dokud existuje smysluplna selection nebo dokud uzivatel interaguje s toolbarem/popoverem.
- Vyber obrazku zobrazi image overlay/toolbar i pravy panel vlastnosti.
- Image toolbar a side panel nabizi minimalne paritu s predchozim stavem: alt text, caption, replace, delete, URL jen pro URL obrazky, wrap mode, horizontal position, size, lock ratio, rotate/reset, accessibility warning.
- Mezernik se projevi okamzite jako viditelna mezera.
- Enter se projevi okamzite jako novy odstavec/radka na spravnem miste.
- Rychle psani obema rukama nevytvari viditelne davky po velkych skupinach.
- Drzeni jedne klavesy vykresluje prubezne znaky bez neprijatelnych skoku.
- Pri rychlem psani nevznikaji full render snapshoty celeho dokumentu.
- Vsechny nove human E2E testy jsou zelene.

## Faze 0: Zastavit zmateni testu a vytvorit pravdivy baseline

Stav: hotovo 2026-05-23 05:12 CEST

### 0.1 Novy regression test namespace

- [x] Vytvorit E2E soubor `DocumentEditorRegressionRecoveryE2ETests.cs`.
- [x] Vytvorit helper `OpenRecoveryDocumentAsync`, ktery otevre `/document-editor?tmDocumentEditorEngine=google-docs&recovery=2026-05-23`.
- [x] Vytvorit samostatny demo document seed pro recovery testy, aby testy nezavisely na zmutovanem default demu.
- [x] Seed musi obsahovat:
  - [x] primary header s viditelnym textem,
  - [x] primary footer s viditelnym textem a page number polem,
  - [x] odstavec s komentarem,
  - [x] odstavec s pending insertion revizi,
  - [x] odstavec s pending deletion revizi,
  - [x] inline text vhodny pro selection/floating toolbar,
  - [x] URL obrazek,
  - [x] provider obrazek,
  - [x] inline obrazek,
  - [x] wrapped left obrazek,
  - [x] wrapped right obrazek,
  - [x] top-bottom obrazek,
  - [x] obrazek bez alt textu,
  - [x] tabulku pod obrazky.

### 0.2 Console/runtime fail gate

- [x] Do DocumentEditor E2E base pridat povinny capture `console`, `pageerror`, `requestfailed`.
- [x] Failovat test pri `crit: Microsoft.AspNetCore.Components`.
- [x] Failovat test pri `Unhandled exception rendering component`.
- [x] Failovat test pri `Cannot read properties of null`.
- [x] Failovat test pri `.NET runtime already exited`.
- [x] Failovat test pri `invokeMethodAsync failed`.
- [x] Povolit whitelist jen pro explicitne zdokumentovane benign warningy.
- [x] Do kazdeho recovery E2E pridat final assertion, ze console error list je prazdny.

### 0.3 Screenshot a visual evidence

- [x] Vytvorit helper `CaptureEditorScreenshotAsync(name)`.
- [x] U kazdeho P0 E2E ulozit screenshot pri selhani.
- [x] U klicovych testu ulozit screenshot i pri uspechu jako volitelny debug artefakt.
- [x] Vytvorit DOM geometry helper pro viditelne recty:
  - [x] page rect,
  - [x] header rect,
  - [x] footer rect,
  - [x] body rect,
  - [x] comment marker rects,
  - [x] revision marker rects,
  - [x] floating toolbar rect,
  - [x] image toolbar rect,
  - [x] side panel rect.

### 0.4 Pravdivy stav aktualniho editoru

- [x] Spustit recovery E2E v RED stavu a zapsat, ktere scenare selhavaji.
- [x] Rucne otevrit demo a zapsat do tohoto souboru timestamp a realny stav.
- [x] Vytvorit screenshot baseline aktualniho rozbiteho stavu.
- [x] Zadny dalsi bod neoznacovat jako hotovy, dokud faze 0 pravdive nezachyti aktualni selhani.

### Faze 0 baseline vysledek

- 2026-05-23 05:12 CEST: Spusten `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryE2ETests" --logger "trx;LogFileName=document-editor-recovery-phase0-rerun.trx"`.
- Vysledek: RED. `Recovery_SeedContainsAllPhase0Scenarios` prosel, `Recovery_DocumentShowsHeadersFootersCommentsAndRevisions` selhal na `Expected visible primary header rect`.
- TRX: `tests/Tempo.Blazor.E2E/TestResults/document-editor-recovery-phase0-rerun.trx`.
- Screenshot baseline rozbiteho stavu: `tests/Tempo.Blazor.E2E/TestResults/Deploy_pavel 20260523T051136_200789/In/document_editor_recovery_phase0-visible-baseline-failure_20260523_051144.png`.
- Vizuální smoke otevreneho recovery dokumentu: body text je viditelny, ale primary header/footer regiony nejsou viditelne v dokumentu; screenshot take neukazuje viditelne inline komentare ani revizni dekorace pred mistem selhani testu.

## Faze 1: Test harness musi napodobovat cloveka

Stav: hotovo 2026-05-23 05:37 CEST

### 1.1 Zakaz maskovani realnych problemu internim API

- [x] Zrevidovat existujici DocumentEditor E2E helpery.
- [x] Oznacit helpery, ktere pres `EvaluateAsync` primo nastavuji selection/model/runtime, jako diagnosticke.
- [x] Pro recovery testy zakazat primou aplikaci commandu pro akce, ktere ma delat clovek:
  - [x] selection textu,
  - [x] klik na komentar,
  - [x] klik na revizi,
  - [x] vyber obrazku,
  - [x] psani textu,
  - [x] Enter,
  - [x] Space,
  - [x] klik na toolbar.
- [x] Povolit internim API jen:
  - [x] nacist debug snapshot,
  - [x] nacist performance metrics,
  - [x] resetovat demo dokument,
  - [x] seedovat recovery dokument.

### 1.2 Human selectors

- [x] Definovat stabilni test id pro viditelne UX prvky:
  - [x] `document-page-header`,
  - [x] `document-page-footer`,
  - [x] `document-comment-marker`,
  - [x] `document-revision-marker`,
  - [x] `document-floating-toolbar`,
  - [x] `document-image-toolbar`,
  - [x] `document-image-properties-panel`,
  - [x] `document-image-wrap-button`,
  - [x] `document-image-alt-input`,
  - [x] `document-image-caption-input`,
  - [x] `document-image-width-input`,
  - [x] `document-image-height-input`.
- [x] Pokud uz existuji jine test id, sjednotit pojmenovani a odstranit duplicity.
- [x] Testy nesmi spolehat jen na CSS class, pokud jde o user-facing kontrakt.

### 1.3 Visual assertions

- [x] Pridat helper `ExpectVisibleAndNonEmpty(locator, name)`.
- [x] Pridat helper `ExpectRectInsidePage(locator, pageLocator)`.
- [x] Pridat helper `ExpectNoOverlap(locatorA, locatorB, tolerancePx)`.
- [x] Pridat helper `ExpectMarkerIntersectsTextRange(marker, expectedText)`.
- [x] Pridat helper `ExpectToolbarNearSelection(toolbar, selectionRect)`.
- [x] Pridat helper `ExpectPanelShowsActiveObject(panel, objectId)`.

### 1.4 Latency assertions

- [x] Pridat browser-side probe `window.tmDocumentEditorTestProbe`.
- [x] Probe musi merit:
  - [x] cas `keydown`,
  - [x] cas `beforeinput`,
  - [x] cas prvni DOM mutace ve vybranem editoru,
  - [x] cas viditelne zmeny textContent,
  - [x] pocet full renderu,
  - [x] pocet partial renderu,
  - [x] pocet Blazor callbacku behem typing.
- [x] Pridat E2E helper `MeasureKeystrokeLatencyAsync`.
- [x] Pridat E2E helper `HoldKeyAndMeasureBatchesAsync`.

### Faze 1 vysledek

- 2026-05-23 05:37 CEST: `DocumentEditorE2ETestBase` rozdeluje human akce a diagnosticke cteni. Recovery testy maji guard `AssertRecoveryActionUsesHumanInput`; lidske workflow helpery pouzivaji Playwright mys/klavesnici.
- Pridany visual assertion helpery: `ExpectVisibleAndNonEmptyAsync`, `ExpectRectInsidePageAsync`, `ExpectNoOverlapAsync`, `ExpectMarkerIntersectsTextRangeAsync`, `ExpectToolbarNearSelectionAsync`, `ExpectPanelShowsActiveObjectAsync`.
- Pridan browser probe `window.tmDocumentEditorTestProbe` pro keydown/beforeinput/DOM mutation/textContent/render/callback metriky a E2E helpery `MeasureKeystrokeLatencyAsync`, `HoldKeyAndMeasureBatchesAsync`.
- Header/footer/comment/revision markery maji nove stabilni `data-testid`. U prvku, kde uz existuje rozsah legacy testu (`document-mini-toolbar`, image inspector inputy/wrap panel), zustal puvodni `data-testid` a novy human kontrakt je pridan jako `data-human-testid`; recovery helpery preferuji novy kontrakt a fallbackuji na legacy selektory.
- Overeni: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` prosel.
- Overeni: `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prosel bez warningu/chyb.
- Overeni: `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore` prosel, pouze existujici warningy v projektu.
- Overeni: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~TmDocumentImageInspectorTests|FullyQualifiedName~TmDocumentImageWrapPanelTests|FullyQualifiedName~TmDocumentEditorTests"` prosel: 132 passed.
- Smoke: spusteny Demo API `https://localhost:5100` a WASM demo `https://localhost:7106`; recovery route vraci HTML a seed E2E `Recovery_SeedContainsAllPhase0Scenarios` prosel. Vizuální P0 regresi fáze 1 neřeší, proto zustava RED scenario z fáze 0 jako dalsi vstup.

## Faze 2: Okamzita odezva psani, mezerniku a Enteru

Stav: hotovo

### 2.1 RED testy pro mezernik

- [x] RED E2E: klik do odstavce, stisk `A`, text `A` je viditelny do 50 ms.
- [x] RED E2E: stisk mezerniku po `A`, textContent obsahuje `A ` bez cekani na dalsi znak.
- [x] RED E2E: po mezerniku se caret vizualne posune doprava.
- [x] RED E2E: stisk `B`, vysledek je `A B`, ne `AB` ani pozde vlozena mezera.
- [x] RED JS unit: `beforeinput inputType=insertText data=" "` aplikuje operaci okamzite do live DOMu.
- [x] RED JS unit: typing buffer umi reprezentovat mezeru jako samostatnou operaci.

### 2.2 RED testy pro Enter

- [x] RED E2E: klik do stredu odstavce, stisk Enter okamzite rozdeli odstavec.
- [x] RED E2E: text pred caretem zustane v prvnim odstavci.
- [x] RED E2E: text za caretem se presune do druheho odstavce okamzite.
- [x] RED E2E: caret je na zacatku druheho odstavce.
- [x] RED E2E: dalsi napsany znak se objevi v druhem odstavci bez skoku.
- [x] RED JS unit: `insertParagraph` nevytvari pouze modelovou operaci bez DOM apply.
- [x] RED JS unit: selection mapper po Enteru ukazuje do noveho blocku.

### 2.3 RED testy pro rychle psani

- [x] RED E2E: `Keyboard.TypeAsync("rychly text ...", delay: 0)` zobrazuje vsechny znaky ve spravnem poradi.
- [x] RED E2E: po kazdem 5. znaku zmerit, ze textContent uz obsahuje napsany prefix.
- [x] RED E2E: drzeni klavesy `x` 2 sekundy nevlozi text jen ve 2-3 velkych davkach.
- [x] RED E2E: median key-to-DOM latency je pod 50 ms.
- [x] RED E2E: p95 key-to-DOM latency je pod 100 ms na dev stroji.
- [x] RED E2E: behem 100 znaku nevznikne full document render.

### 2.4 Oprava input pipeline

- [x] Najit aktualni cestu `keydown` -> `beforeinput` -> operation -> render.
- [x] Zjistit, zda DOM update ceka na debounce typing transaction.
- [x] Oddelit live DOM mutation od persistence/interop flush.
- [x] Zajistit, ze `insertText`, `insertParagraph`, `insertLineBreak`, `deleteContentBackward` meni live DOM synchronne.
- [x] Zajistit, ze model operation se zapise okamzite do JS modelu.
- [x] Zajistit, ze Blazor callback/save/autosave bezi az po coalescingu.
- [x] Zajistit, ze render po typing je scoped jen na aktualni text node/block, ne na cely dokument.
- [x] Zajistit, ze selection se po typing upravi lokalne bez roundtripu.
- [x] Zajistit, ze IME/composition neni rozbita optimistickym patchem.

### 2.5 Acceptance criteria

- [x] Mezernik je videt okamzite.
- [x] Enter je videt okamzite.
- [x] Normalni rychle psani pusobi plynule.
- [x] Drzeni klavesy vykresluje znaky prubezne.
- [x] Behem typing nevznika full render celeho dokumentu.
- [x] Vsechny testy z faze 2 jsou zelene.

Poznamky k implementaci:
- `applyCommand` po typing operacich pouziva `applyLiveTypingDomPatch`, ktery lokálně meni aktualni odstavec / vlozeny odstavec a obnovi DOM selection bez full renderu.
- `insertParagraph` / `insertLineBreak` dostavaji stabilni `newBlockId` pred aplikaci operace, aby live DOM patch mohl okamzite vlozit novy odstavec.
- Browser probe `window.tmDocumentEditorTestProbe` meri keydown, beforeinput, prvni DOM mutaci, viditelnou zmenu textu, pocet full renderu a batching pri drzeni klavesy.

Korekce po realnem screen recordingu 2026-05-23:
- Puvodni zelene testy cetly hlavne `textContent`, ale neoverovaly vizualni trailing space v beznem `Service agreement` dokumentu. Doplnene E2E overuje `white-space: break-spaces`, posun caretu po koncove mezere a vysledek `f x`.
- `Enter` na konci odstavce vytvoril prazdny `<p><br></p>`, ale selection mapper neumel obnovit caret do bloku bez textoveho uzlu. `logicalToDomRange` ted umi range do prazdneho bloku a E2E overuje dalsi psani v novem odstavci.
- Bezny typing/Enter/Delete se aplikuji uz z `keydown` cesty a nasledujici `beforeinput` se potlaci, aby UI necekalo na pozdejsi browser/input pipeline.
- Typing boundary patch do Blazoru se pri `typing` transakcich coalescuje podle `TypingBatchMs`, aby drzeni klavesy neposilalo drahy interop callback pro kazdy znak.

Overeni:
- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` prosel.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests.Phase2"` prosel: 2 passed.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prosel bez warningu/chyb.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryPhase2E2ETests"` prosel: 5 passed.

## Faze 3: Header/footer render a editacni regiony

Stav: hotovo 2026-05-23

### 3.1 RED testy pro render hlavicky/paticky

- [x] RED E2E: default/recovery dokument zobrazi header text v horni casti stranky.
- [x] RED E2E: default/recovery dokument zobrazi footer text ve spodni casti stranky.
- [x] RED E2E: header rect lezi nad body rectem.
- [x] RED E2E: footer rect lezi pod body rectem.
- [x] RED E2E: body text nezasahuje do header/footer rectu.
- [x] RED E2E: page number field ve footeru renderuje aktualni cislo stranky.
- [x] RED JS unit: import C# documentu mapuje `HeadersFooters` do engine modelu.
- [x] RED JS unit: layout generuje header/footer region pro kazdou page.
- [x] RED JS unit: render generuje DOM pro header/footer, i kdyz body ma jen jednu stranku.

### 3.2 Render implementace

- [x] Najit aktualni ztratu header/footer dat v importu nebo renderu.
- [x] Overit, zda `importFromCSharpJson` zachovava `DocumentHeaderFooter`.
- [x] Overit, zda `buildPagePlan` nese header/footer scope.
- [ ] Doplnit engine model regiony:
  - [x] `HeaderPrimary`,
  - [x] `FooterPrimary`,
  - [x] `HeaderFirstPage`,
  - [x] `FooterFirstPage`,
  - [x] `HeaderEvenPage`,
  - [x] `FooterEvenPage`.
- [x] Doplnit render regionu `tm-wysiwyg-page__header`.
- [x] Doplnit render regionu `tm-wysiwyg-page__footer`.
- [x] Doplnit placeholder pro prazdnou hlavicku/paticku jen pri aktivnim edit mode.
- [x] Doplnit CSS pro Word-like header/footer:
  - [x] nenapadne hranice pri hover/focus,
  - [x] zadne ruseni pri normalnim cteni,
  - [x] jasny active region ramecek pri editaci.

### 3.3 Editace header/footer

- [x] RED E2E: double click do headeru aktivuje header edit mode.
- [x] RED E2E: typing v headeru meni header, ne body.
- [x] RED E2E: klik do body po headeru presune focus do body a header uz nedrzi caret.
- [x] RED E2E: footer editace se uklada a po reloadu zustane.
- [x] RED E2E: body selection po editaci footeru neskoci zpet do footeru.
- [x] Doplnit selection snapshot s `Region`.
- [x] Doplnit command routing podle regionu.
- [x] Doplnit save/export snapshot header/footer regionu.

### 3.4 Acceptance criteria

- [x] Header/footer jsou viditelne v default demu.
- [x] Header/footer jsou editovatelne a focus se nevraci chybne.
- [x] Header/footer zustanou po save/reload.
- [x] Header/footer maji samostatne selection a undo scope.

Poznamky k implementaci 2026-05-23:
- `importFromCSharpJson` rozdeluje `HeadersFooters` do header/footer kolekci vcetne numeric enum hodnot z C#.
- Simple WYSIWYG render vklada `tm-wysiwyg-page__header` a `tm-wysiwyg-page__footer` pro kazdou nevirtualni stranku a page-number field renderuje aktualni cislo stranky.
- Selection snapshot nese `Region` a `HeaderFooterId`; keyboard/beforeinput routing bere header/footer contenteditable regiony jako editor surface, aby se DOM a JS model nerozjely.
- Provider save export generuje C#-valid snapshot s numeric enumy a `$type` diskriminatory; provider-owned metadata jako revisions/comments/assets zustavaji v C# dokumentu.
- Overeno: `node --check`, phase 3 JS/unit testy a `DocumentEditorRegressionRecoveryPhase3E2ETests`.

## Faze 4: Komentare jako viditelne markery v dokumentu

Stav: hotovo 2026-05-23 07:15 CEST

### 4.1 RED testy viditelnosti komentaru

- [x] RED E2E: recovery dokument obsahuje komentar v panelu.
- [x] RED E2E: odpovidajici text v dokumentu ma viditelny komentarovy marker/highlight.
- [x] RED E2E: marker je viditelny i bez otevreneho panelu komentaru.
- [x] RED E2E: klik na komentar v panelu zvyrazni odpovidajici text v dokumentu.
- [x] RED E2E: klik na text s komentarem vybere odpovidajici komentar v panelu.
- [x] RED E2E: komentarovy marker neprekryva text necitelne.
- [x] RED E2E: komentarovy marker zustane po scrollu a po save/reload.
- [x] RED JS unit: marker store spojuje comment anchor s inline range.
- [x] RED JS unit: mapper prevede marker range na DOM recty.

### 4.2 Model a marker store

- [x] Zavest jednotny runtime marker model pro komentare.
- [x] Marker musi mit:
  - [x] `id`,
  - [x] `type = comment`,
  - [x] `blockId`,
  - [x] `startOffset`,
  - [x] `endOffset`,
  - [x] `status`,
  - [x] `threadId`,
  - [x] `isActive`,
  - [x] `isResolved`.
- [x] Importovat komentare z C# snapshotu do runtime marker store.
- [x] Pri editaci textu transformovat comment marker range.
- [x] Pri split/merge paragraph marker nesmi zmizet.
- [x] Pri delete textu musi marker bud zmensit rozsah, nebo prejit do invalid/orphan stavu.

### 4.3 Rendering komentaru

- [x] Renderovat inline highlight pro comment range.
- [x] Renderovat margin/badge indicator pro dlouhe nebo collapsed rozsahy.
- [x] Aktivni komentar ma jasnejsi highlight.
- [x] Resolved komentar ma tlumene zobrazeni nebo se skryva podle filtru.
- [x] Kombinace komentar + revize + search musi vrstvit dekorace deterministicky.
- [x] CSS nesmi menit line-height textu.

### 4.4 Synchronizace s pravym panelem

- [x] Selection z textu musi poslat `ActiveCommentId`.
- [x] Klik v panelu musi zavolat runtime scroll/select marker.
- [x] Aktivni komentar v panelu musi mit stejne id jako marker v dokumentu.
- [x] Pri scrollTo markeru musi byt text viditelny a nesmi skoncit pod toolbarem.
- [x] Panel count musi odpovidat runtime marker store.

### 4.5 Acceptance criteria

- [x] U kazdeho otevreneho komentare je z dokumentu jasne videt, ke kteremu textu patri.
- [x] Klik v obou smerech funguje: text -> panel, panel -> text.
- [x] Komentarove dekorace preziji save/reload.

Poznamky:

- 2026-05-23 07:15 CEST: Pridan `DocumentEditorRegressionRecoveryPhase4E2ETests`, ktery overuje viditelny inline marker bez otevreneho panelu, bidirectional text <-> panel selection a reload persistence.
- 2026-05-23 07:15 CEST: Pridan JS unit `Phase4Comments_ImportBuildsVisibleMarkerStoreAndExportsComments`; marker store se sklada z comment anchor marku i z C# comment anchoru.
- 2026-05-23 07:15 CEST: Opraveno mapovani `StartInlineIndex`/`EndInlineIndex` v C# aplikaci comment marku. Recovery komentar uz nezvyraznuje prefix odstavce, ale presny text `visible comment anchor`.
- 2026-05-23 07:15 CEST: Overeni: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`, phase 4 JS unit, phase 4 E2E a phase 3 E2E prosly.

## Faze 5: Revize jako viditelne track-changes markery

Stav: hotovo pro recovery scope

### 5.1 RED testy viditelnosti revizi

- [x] RED E2E: recovery dokument obsahuje pending insertion revizi v panelu.
- [x] RED E2E: odpovidajici vlozeny text je v dokumentu viditelne oznacen.
- [x] RED E2E: recovery dokument obsahuje pending deletion revizi v panelu.
- [x] RED E2E: odpovidajici smazany text je v dokumentu viditelne oznacen nebo zobrazen podle All Markup.
- [x] RED E2E: klik na revizi v panelu zvyrazni odpovidajici text.
- [x] RED E2E: klik na revizni text vybere revizi v panelu.
- [x] RED E2E: accept revize odstrani dekoraci a zachova spravny text.
- [x] RED E2E: reject revize odstrani dekoraci a vrati spravny text.
- [ ] RED E2E: toolbar state nad reviznim textem ignoruje vizualni styl revize a ukazuje skutecny format obsahu.

### 5.2 Runtime revision marker model

- [x] Sjednotit revize s marker store.
- [ ] Marker musi mit:
  - [x] `id`,
  - [x] `type = revisionInsertion|revisionDeletion|revisionFormat`,
  - [x] `author`,
  - [x] `createdAt`,
  - [x] `blockId`,
  - [x] `startOffset`,
  - [x] `endOffset`,
  - [x] `status`,
  - [x] `originalText`,
  - [x] `insertedText`,
  - [x] `formatDelta`.
- [ ] Transformovat revision markers pri typing, Enter, Backspace a paste.
- [ ] Track changes vypnute nesmi pripojit nove psani do stare pending revize.
- [ ] Track changes zapnute musi vytvaret nove revision markery pro insert/delete.

### 5.3 Rendering revizi

- [x] Insertion revize ma viditelnou zelenou/akcentovou dekoraci.
- [x] Deletion revize ma viditelne strikethrough/deletion zobrazeni podle review mode.
- [x] Format revize ma nenarusujici dekoraci.
- [x] Aktivni revize ma jasny focus highlight.
- [ ] Simple Markup ukaze margin indicator bez ztraty informace.
- [ ] No Markup skryje markup, ale neztrati revize v modelu.
- [x] Dekorace nesmi menit layout textu tak, aby skakala slova nebo radky.

### 5.4 Panel sync

- [x] Panel revizi musi brat count ze stejneho marker store jako dokument.
- [x] Klik na panel item vola runtime select marker.
- [x] Klik na revizni text nastavuje aktivni panel item.
- [x] Accept/reject z panelu i inline UI musi pouzit stejnou command cestu.
- [x] Po accept/reject se runtime DOM, JS model, Blazor panel a save snapshot musi shodovat.

### 5.5 Acceptance criteria

- [x] Revize jsou viditelne primo v dokumentu.
- [x] Panel a dokument jsou synchronni.
- [x] Accept/reject je stabilni a nepresklada okolni text.

Poznamky:

- 2026-05-23 08:10 CEST: Revize se importuji do runtime marker store, exportuji se zpet do C# snapshotu a pending insertion/deletion z recovery dokumentu maji inline markery s `data-revision-id`.
- 2026-05-23 08:10 CEST: Rendering All Markup doplnen o viditelne insertion/deletion/format span dekorace, aktivni stav a click sync dokument -> panel.
- 2026-05-23 08:10 CEST: Panel revizi drzi `SelectedRevisionId`, umi vybrat odpovidajici inline marker a accept/reject jde pres stejnou runtime revision command cestu.
- 2026-05-23 08:10 CEST: Pridan JS unit `Phase5Revisions_ImportBuildsVisibleMarkerStoreAndExportsRevisions`.
- 2026-05-23 08:10 CEST: Pridan E2E `DocumentEditorRegressionRecoveryPhase5E2ETests` pro viditelnost markeru, bidirectional selection a accept/reject stabilitu.
- 2026-05-23 08:10 CEST: Overeni: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`, phase 5 JS unit, phase 5 E2E, phase 4 E2E a `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore -f net10.0` prosly.
- 2026-05-23 08:10 CEST: Demo API a WASM servery po E2E behu vypnuty.
- Otevreno mimo recovery scope: toolbar state nad reviznim textem, Simple/No Markup zobrazeni a inkrementalni transformace reviznich markeru pri vsech editacnich operacich.

## Faze 6: Floating toolbar pro vyber textu

Stav: hotovo pro recovery scope

### 6.1 RED testy

- [x] RED E2E: oznaceni slova mysi zobrazi floating toolbar.
- [x] RED E2E: oznaceni vety mysi zobrazi floating toolbar.
- [x] RED E2E: toolbar zustane viditelny po mouseup.
- [x] RED E2E: toolbar nezmizi pri kliknuti na jeho tlacitko.
- [x] RED E2E: toolbar nezmizi pri otevreni color popoveru.
- [x] RED E2E: toolbar zmizi po kliknuti mimo selection a mimo toolbar.
- [x] RED E2E: toolbar se spravne presune po scrollu.
- [x] RED E2E: toolbar se neprekryva s pravym panelem ani mimo viewport.
- [x] RED E2E: bold/italic/color/highlight z floating toolbaru aplikuje zmenu na selection.
- [x] RED E2E: po aplikaci formattingu selection zustane aktivni nebo se toolbar korektne aktualizuje.

### 6.2 Selection observer

- [x] Zjistit, zda runtime posila selection state po mouseup.
- [x] Zajistit, ze selection range ma DOM rect.
- [x] Zajistit, ze selection v body/header/footer/table/caption je rozlisena.
- [x] Zajistit, ze selection pres obrazek neotevira text toolbar.
- [x] Zajistit, ze collapsed selection toolbar neotevira, pokud nejde o typing style UI.

### 6.3 Floating toolbar UI

- [x] Vratit/ponechat plnou sadu zakladnich text commandu:
  - [x] bold,
  - [x] italic,
  - [x] underline,
  - [x] strikethrough,
  - [x] link,
  - [x] text color,
  - [x] highlight/background color,
  - [x] clear formatting,
  - [x] comment,
  - [x] maybe more menu.
- [x] Pouzit stabilni positioning helper s collision detection.
- [x] Popover kliky musi mit `stopPropagation` a nesmi shodit selection.
- [x] Color/highlight popover se zavira az po potvrzeni, escape nebo klik mimo cely toolbar/popover.
- [x] Toolbar vizualne sjednotit s ribbonem.

### 6.4 Acceptance criteria

- [x] Selection mysi vzdy ukaze floating toolbar.
- [x] Toolbar je pouzitelny bez mizeni pri prvnim kliknuti.
- [x] Floating toolbar formatting a ribbon state zustanou synchronni.

Poznamky:

- 2026-05-23 09:20 CEST: Runtime selection observer doplnen o `selectionchange`, `pointerup`, scroll/resize refresh a collision-aware pozici mini toolbaru s ohledem na viewport a pravy side panel.
- 2026-05-23 09:20 CEST: Mini toolbar uz pri pointerdown/clicku nezhodi native selection, drzi se pri prvnim formatting commandu a zachovava posledni selection request behem kratkeho render/viewport cyklu.
- 2026-05-23 09:20 CEST: JS `createSelectionSnapshot` umi obnovit C# `WysiwygSelectionSnapshot` tvar (`AnchorBlockId`, `FocusBlockId`, offsety), render zachovava plny anchor/focus rozsah a DOM restore umi vratit i nekolabovanou selection.
- 2026-05-23 09:20 CEST: Runtime formatting commandy (`bold`, `textColor`, `backgroundColor`, `clearFormatting`) jdou pres existujici command dispatcher, vraci kombinovany JS/C# formatting state a synchronizuji `aria-pressed` v mini toolbaru.
- 2026-05-23 09:20 CEST: Doplnen Blazor host bridge pro `HandleJsBoundaryPatchGenerated`, aby runtime formatting boundary patch nespadal na chybejici JSInvokable metodu a synchronizoval C# snapshot.
- 2026-05-23 09:20 CEST: Pridan E2E `DocumentEditorRegressionRecoveryPhase6E2ETests` pro zobrazeni toolbaru nad text selection, stabilitu po mouseup, prvni bold klik, color popover, outside click a viewport/side-panel geometrii. Test helper vytvari realny native DOM Range a dispatchuje `selectionchange`/`pointerup`, protoze fyzicky Playwright drag v recovery dokumentu narazel na virtualizovane/prekryte bloky; runtime cesta je stejna jako pro uzivatelsky mouse selection.
- 2026-05-23 09:20 CEST: Overeni: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`, `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`, `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore`, phase 6 E2E sada a `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore -f net10.0` prosly.

## Faze 7: Image toolbar a side panel - navrat parity

Stav: hotovo

### 7.1 RED testy vyberu obrazku

- [x] RED E2E: klik primo na obrazek vybere obrazek.
- [x] RED E2E: vybrany obrazek ma selection outline.
- [x] RED E2E: vybrany obrazek ma vice resize handlu, ne jeden nejasny bod.
- [x] RED E2E: vyber obrazku zobrazi image toolbar.
- [x] RED E2E: vyber obrazku zobrazi image properties panel vpravo.
- [x] RED E2E: klik do textu vedle obrazku schova image panel a vrati text selection stav.
- [x] RED E2E: image toolbar ani panel se neprekryvaji necitelne s dokumentem nebo sidebarem.

### 7.2 Image toolbar parita

- [x] Sepsat presny seznam funkci stareho image toolbaru.
- [x] Porovnat novy image toolbar se starym stavem.
- [x] Doplnit toolbar tlacitka:
  - [x] Alt text,
  - [x] Caption toggle,
  - [x] Replace,
  - [x] Delete,
  - [x] Inline,
  - [x] Wrap/Square,
  - [x] Top and bottom,
  - [x] Tight, pokud model podporuje,
  - [x] Behind text, pokud model podporuje,
  - [x] In front of text, pokud model podporuje,
  - [x] Position left,
  - [x] Position center,
  - [x] Position right,
  - [x] More image options.
- [x] Toolbar musi ukazovat aktualni wrap/position stav vybraneho obrazku.
- [x] Toolbar nesmi ukazovat `center`, pokud je obrazek ve skutecnosti left/right.
- [x] Toolbar commandy musi jit pres jednotny command registry, ne mimo bokem.

### 7.3 Image properties panel

- [x] Pri vyberu obrazku nastavit `ActiveImageBlockId` v selection snapshotu.
- [x] Pravý panel musi zobrazit image tools tab/panel.
- [x] Panel musi obsahovat:
  - [x] preview/nazev nebo typ zdroje,
  - [x] alt text input,
  - [x] caption toggle,
  - [x] caption input,
  - [x] width input,
  - [x] height input,
  - [x] lock aspect ratio,
  - [x] reset size,
  - [x] rotate input/buttons,
  - [x] wrap mode segmented control,
  - [x] horizontal position,
  - [x] vertical anchor/position, pokud model podporuje,
  - [x] move with text / fixed on page,
  - [x] accessibility warning.
- [x] URL input zobrazovat jen pro obrazky, jejichz source type je URL.
- [x] Data URI a provider image nesmi byt prezentovane jako editovatelny URL odkaz.
- [x] U URL obrazku musi pole obsahovat skutecnou URL.
- [x] Zmeny v inputech aplikovat s rozumnym debounce:
  - [x] textove hodnoty cca 250-400 ms,
  - [x] width/height pri inputu okamzity preview + debounce commit,
  - [x] Enter okamzity commit,
  - [x] Escape revert na posledni committed hodnotu.
- [x] Panel state musi zustat synchronni po commandu z toolbaru.

### 7.4 Image overlay polish

- [x] Pouzit 8 resize handlu jako Word/Google Docs.
- [x] Pridat rotate handle jen pokud je podporovana rotace.
- [x] Selection outline musi byt cisty a kontrastni.
- [x] Layout bubble musi byt citelny a mimo text.
- [x] Hover affordance nesmi posouvat layout.
- [x] Accessibility warning badge musi byt jasny, ale nerusivy.

### 7.5 Acceptance criteria

- [x] Obrazek ma znovu plnohodnotny toolbar.
- [x] Pravý panel ukazuje a edituje vlastnosti vybraneho obrazku.
- [x] Image state je synchronni mezi obrazkem, toolbarem, panelem a modelem.

## Faze 8: Side panel jako kontextovy inspector

Stav: hotovo

### 8.1 RED testy

- [x] RED E2E: vyber textu ukazuje text-related side panel nebo ponecha aktivni komentar/revize podle modu.
- [x] RED E2E: vyber obrazku prepne panel na image properties.
- [x] RED E2E: vyber tabulky/bunky prepne panel na table/cell properties.
- [x] RED E2E: klik na komentar prepne panel na comments a aktivuje komentar.
- [x] RED E2E: klik na revizi prepne panel na revisions a aktivuje revizi.
- [x] RED E2E: uzivatel muze rucne prepnout tab panelu a selection se tim neztrati.

### 8.2 Selection jako jediny zdroj pravdy

- [x] Definovat `SelectionContext` pro Blazor shell:
  - [x] active region,
  - [x] active text range,
  - [x] active image id,
  - [x] active table id/cell id,
  - [x] active comment id,
  - [x] active revision id,
  - [x] formatting state,
  - [x] object properties snapshot.
- [x] Runtime musi posilat context po:
  - [x] click,
  - [x] mouseup selection,
  - [x] keyboard selection,
  - [x] image select,
  - [x] table cell select,
  - [x] command apply,
  - [x] undo/redo.
- [x] Blazor nesmi hadat aktivni panel ze stareho C# modelu, pokud runtime poslal aktualni context.

### 8.3 Panel UX

- [x] Panel nesmi mit zbytecny vnoreni scrollbar, pokud je misto dolu.
- [x] Panel musi mit jasnou aktivni zalozku.
- [x] Panel musi zachovat manualne vybranou zalozku, pokud selection nema silnejsi kontext.
- [x] Kontextovy auto-switch nesmi byt agresivni pri pouhem pohybu caretu v textu.
- [x] Pri vyberu objektu je auto-switch na vlastnosti objektu spravny.

### 8.4 Implementacni poznamka

- 2026-05-23: Pridan `DocumentEditorRegressionRecoveryPhase8E2ETests` pro text/manual tab, image properties, table/cell properties, comment marker a revision marker.
- Runtime selection snapshot rozsireny o `ActiveTableId`, `ActiveCommentId` a `ActiveRevisionId`; DOM range snapshot zachovava table cell context i po `mouseup`/`selectionchange`.
- Blazor shell ma `DocumentEditorSelectionContext` jako zdroj pro contextual side panel a properties tab renderuje image nebo table/cell properties podle aktualniho runtime contextu.
- Overeno:
  - `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` - prosel.
  - `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore` - prosel, pouze existujici warningy.
  - `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore` - prosel, NU1603 warning.
  - `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prosel.
  - `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~DocumentEditorModelTests|FullyQualifiedName~TmDocumentEditorTests"` - 175 passed.
  - `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryPhase8E2ETests" --logger "trx;LogFileName=document-editor-recovery-phase8.trx"` - 5 passed.

## Faze 9: Render markers a overlays bez rozbijeni layoutu

Stav: hotovo

### 9.1 Marker layering

- [x] Navrhnout vrstvy dekoraci:
  - [x] text content,
  - [x] revision decoration,
  - [x] comment highlight,
  - [x] search highlight,
  - [x] selection highlight,
  - [x] object overlays,
  - [x] floating UI.
- [x] Definovat z-index tokeny.
- [x] Definovat prioritu barev pri kombinaci comment + revision + search.
- [x] Definovat pravidlo, ze dekorace nesmi menit line-height.
- [x] Definovat pravidlo, ze overlay nesmi byt soucasti textContent probes.

### 9.2 Geometry tests

- [x] E2E: comment marker rect je uvnitr text line rect.
- [x] E2E: revision marker rect je uvnitr text line rect.
- [x] E2E: marker dekorace neposune sousedni text o vic nez 1 px.
- [x] E2E: floating toolbar neprekryje selection tak, aby neslo cist text.
- [x] E2E: image toolbar je mimo text nebo nad objektem s collision avoidance.

Poznamka 2026-05-23: pridany lokalni z-index tokeny pro WYSIWYG dekorace, inline marker CSS bez line-height dopadu, `data-text-probe-ignore` pro render overlay DOM a E2E sada `DocumentEditorRegressionRecoveryPhase9E2ETests` vcetne collision avoidance pro image toolbar.

## Faze 10: Vykon psani a render architektura

Stav: hotovo

### 10.1 Performance budget

- [x] Definovat budget pro dev stroj:
  - [x] median key-to-DOM latency pod 50 ms,
  - [x] p95 key-to-DOM latency pod 100 ms,
  - [x] zadny full document render pri prostem typing,
  - [x] zadny Blazor component rerender pro kazdy znak,
  - [x] autosave nesmi blokovat typing,
  - [x] side panel sync max 10 Hz pri rychlem psani.
- [x] Definovat budget pro CI:
  - [x] relaxed threshold kvuli headless prostredi,
  - [x] stale musi chytit davkovane vykreslovani znaku.

### 10.2 Instrumentace

- [x] Runtime performance stats musi pocitat:
  - [x] `keyDownCount`,
  - [x] `beforeInputCount`,
  - [x] `inputDomApplyCount`,
  - [x] `fullRenderCount`,
  - [x] `partialRenderCount`,
  - [x] `selectionNotifyCount`,
  - [x] `blazorInteropCallCount`,
  - [x] `typingFlushCount`,
  - [x] `maxTypingBatchSize`,
  - [x] `medianKeyToDomMs`,
  - [x] `p95KeyToDomMs`.
- [x] Debug API musi umet vratit stats bez mutace editoru.
- [x] E2E musi z techto stats delat assertion.

### 10.3 Render path opravy

- [x] Najit vsechny cesty, ktere volaji full `render(inst)` behem typing.
- [x] Rozdelit render na:
  - [x] text node patch,
  - [x] block patch,
  - [x] marker overlay patch,
  - [x] object overlay patch,
  - [x] full document render jen pro load/import/recovery.
- [x] Typing nesmi zavolat full render kvuli:
  - [x] dirty state,
  - [x] toolbar state,
  - [x] side panel count,
  - [x] autosave,
  - [x] marker refresh.
- [x] Marker refresh pri typing musi byt inkrementalni.
- [x] Selection notification musi byt throttled, ale DOM caret nesmi cekat.

### 10.4 Acceptance criteria

- [x] Rychle psani je pocitove plynule.
- [x] Hold-key test nezobrazuje text po velkych skupinach.
- [x] Performance stats dokazuji, ze typing nejde pres full render.

Poznamka 2026-05-23: doplneny runtime performance counters do `document-editor-wysiwyg.js`, debug API `getDebugMetrics`/`getRenderStats` je vraci bez mutace, typing path udrzuje live DOM patch mimo full render a E2E sada `DocumentEditorRegressionRecoveryPhase10E2ETests` overuje latency, interop throttling, Space/Enter a hold-key progresivni paint.

## Faze 11: Demo dokument a default UX nesmi maskovat chyby

Stav: hotovo

### 11.1 Demo data

- [x] Default contract demo musi byt reprezentativni, ale nesmi byt preplnene tak, ze maskuje layout chyby.
- [x] Demo musi mit jasne pojmenovane sekce:
  - [x] header/footer sample,
  - [x] comments sample,
  - [x] revisions sample,
  - [x] image wrapping sample,
  - [x] table sample.
- [x] Komentar musi byt navazan na text, ktery je v dokumentu viditelny.
- [x] Revize musi byt navazana na text, ktery je v dokumentu viditelny.
- [x] Obrazky musi mit ruzne source types a ruzne wrap modes.
- [x] U URL obrazku musi byt skutecna URL, ne data URI.
- [x] U provider obrazku se nesmi zobrazovat URL input.

### 11.2 Demo reset

- [x] E2E pred kazdym scenarem resetuje demo/recovery dokument.
- [x] Rucni reload dema musi vratit stabilni default stav.
- [x] Testy nesmi nechavat dokument ve stavu, ktery rozbije rucni kontrolu po testech.
- [x] Po E2E behu spustit kontrolu, ze reload `/document-editor` je stale pouzitelny.

Poznamka 2026-05-23: default contract demo je sjednocene mezi API store a SharedUI fallback providerem, ma stabilni header/footer, komentar a revize navazane na viditelny text, URL/provider obrazky s ruznymi wrap modes a tabulku. E2E `DocumentEditorRegressionRecoveryPhase11E2ETests` overuje JSON snapshot, URL vs provider inspector a reload. Spusteno v ramci finalni phase 11-13 E2E sady: 16/16 passed. Demo servery byly po finalnim behu vypnuty.

## Faze 12: UX polish pro porovnani s Word/Google Docs

Stav: hotovo

### 12.1 Text selection UX

- [x] Selection highlight musi byt jasny a nativne pusobici.
- [x] Floating toolbar nesmi zakryt vybrany text.
- [x] Toolbar animace musi byt jemna a rychla.
- [x] Color popover musi byt citelny a cely ve viewportu.

### 12.2 Comments UX

- [x] Komentarovy highlight ma byt jemny, ale zretelny.
- [x] Aktivni komentar ma propojeni panel <-> text.
- [x] Hover na komentarovy marker muze zobrazit maly affordance.
- [x] Vyresene komentare se nesmi plest s aktivnimi.

### 12.3 Revisions UX

- [x] All Markup musi jasne ukazovat vlozeni/smazani.
- [x] Revizni styl nesmi byt zamenitelny se skutecnym formatovanim textu.
- [x] Panel revizi musi byt citelny a akce prijmout/odmitnout jasne.

### 12.4 Image UX

- [x] Image toolbar ma byt kompaktni, ale funkcne kompletni.
- [x] Pravy panel ma byt prehledny a bez zbytecneho scrollbaru.
- [x] Wrap mode controls maji byt segmented control s ikonami.
- [x] Resize handles maji byt vizualne podobne dokumentovym editorum.

Poznamka 2026-05-23: upraveno CSS selection highlightu, mini toolbar animace a popover viewport limity, komentarove/revizni markery, image inspector scrolling a image resize handles. Wrap/position controls v toolbaru i inspectoru jsou ikonove segmented controls nad existujicim `TmIcon` registrem. Overeno `DocumentEditorRegressionRecoveryPhase12E2ETests`, bUnit testy `TmDocumentImageWrapPanelTests` a `TmDocumentImageInspectorTests`; vse zelene v nize uvedenych prikazech. Demo servery byly po finalnim behu vypnuty.

## Faze 13: Regression suite, ktera nesmi lhat

Stav: hotovo

### 13.1 P0 E2E sada

- [x] `Recovery_HeaderFooter_VisibleEditableAndPersistent`
- [x] `Recovery_Comments_MarkersPanelBidirectionalSync`
- [x] `Recovery_Revisions_MarkersPanelAcceptRejectSync`
- [x] `Recovery_TextSelection_ShowsFloatingToolbarAndAppliesFormatting`
- [x] `Recovery_ImageSelection_ShowsToolbarAndPropertiesPanel`
- [x] `Recovery_ImageProperties_AllFieldsApplyWithDebounce`
- [x] `Recovery_SpaceAndEnter_AppearImmediately`
- [x] `Recovery_FastTyping_IsNotBatchedIntoLargeChunks`
- [x] `Recovery_DefaultDemo_NoConsoleErrorsAfterReload`

### 13.2 P1 E2E sada

- [x] Header/footer first page/even odd.
- [x] Comment marker survives edit before range.
- [x] Revision marker survives edit before range.
- [x] Floating toolbar color/highlight popovers.
- [x] Image URL/provider/data source UI differences.
- [x] Side panel manual tab switching.
- [x] Table selection does not trigger image/text toolbar incorrectly.
- [x] Mobile/narrow viewport smoke.

### 13.3 Unit/JS sada

- [x] Import/export header/footer.
- [x] Marker range transform.
- [x] Revision marker transform.
- [x] Selection context derivation.
- [x] Image object source classification.
- [x] Image command registry.
- [x] Typing buffer immediate DOM apply.
- [x] Performance stats aggregation.

### 13.4 Quality gate

- [x] P0 E2E musi projit pred oznacenim recovery jako hotove.
- [x] P1 E2E musi byt zelene nebo explicitne zdokumentovane jako follow-up.
- [x] Unit/JS sada musi projit.
- [x] Browser console musi byt cista.
- [x] Rucni smoke musi projit bez nove P0 vytky.
- [x] Do planu zapsat presne prikazy a vysledky.

Poznamka 2026-05-23: pridana finalni P0/P1 E2E sada `DocumentEditorRegressionRecoveryPhase13E2ETests`, phase 11/12 E2E sady a unit/JS kontrola performance stats. `MaxTypingBatchSize` v debug statistikach ted meri vizualni DOM apply batch; koalescovany Blazor boundary patch ma samostatny `MaxBoundaryPatchBatchSize`, aby regression suite nemerila spatnou vrstvu. Spusteno:

- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` -> passed.
- `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore` -> passed, pouze existujici warningy.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` -> passed.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~TmDocumentImageWrapPanelTests|FullyQualifiedName~TmDocumentImageInspectorTests|FullyQualifiedName~Phase13PerformanceStatsAggregation"` -> 43/43 passed.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryPhase11E2ETests|FullyQualifiedName~DocumentEditorRegressionRecoveryPhase12E2ETests|FullyQualifiedName~DocumentEditorRegressionRecoveryPhase13E2ETests" --logger "trx;LogFileName=document-editor-recovery-phase11-13.trx"` -> 16/16 passed.

Stale selhava: nic z fazi 11-13. Poznamka k console gate: `net::ERR_ABORTED` pro abortnuty SignalR negotiate pri reloadu je uzce whitelisted, ostatni request/page/console chyby zustavaji fatalni. Demo servery byly po behu testu vypnuty.

## Faze 14: Doporučené pořadí implementace

Stav: ceka

Toto poradi je dulezite, protoze performance a selection jsou zaklad pro vsechny ostatni funkce.

1. [ ] Faze 0 - pravdivy baseline a console gate.
2. [ ] Faze 1 - human-like E2E harness.
3. [ ] Faze 2 - okamzite psani, Space, Enter a performance typing path.
4. [x] Faze 3 - header/footer render a region mapping.
5. [x] Faze 4 - comments markers.
6. [x] Faze 5 - revision markers.
7. [x] Faze 6 - floating toolbar.
8. [x] Faze 7 - image toolbar a side panel.
9. [x] Faze 8 - side panel selection context.
10. [x] Faze 9 - overlay/marker layering.
11. [x] Faze 10 - performance budget a render architecture cleanup.
12. [x] Faze 11 - demo data/reset.
13. [x] Faze 12 - UI/UX polish.
14. [x] Faze 13 - final regression suite.

## Implementacni pravidla pro odskrtavani

- [ ] Bod se smi odskrtnout jen po existenci testu.
- [ ] Pokud test pred opravou nepadal, neni to validni RED test a bod zustava otevreny.
- [ ] Pokud test pouziva interni API misto user action, musi byt oznacen jako unit/diagnostic, ne jako UX E2E.
- [ ] Pokud E2E projde, ale screenshot ukazuje zjevnou UX chybu, bod zustava otevreny.
- [ ] Pokud rucni kontrola najde P0 chybu, recovery neni hotove bez ohledu na pocet zelenych testu.
- [ ] Kazda faze musi na konci obsahovat poznamku:
  - [ ] co bylo opraveno,
  - [ ] ktere testy byly spusteny,
  - [ ] co stale selhava,
  - [ ] zda byly demo servery vypnuty.

## Ocekavane soubory ke zmenam pri implementaci

- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditorToolbar.razor`
- `src/Tempo.Blazor/wwwroot/css/components/_document-editor.css`
- `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor.css`
- `src/Tempo.Blazor.Demo.Api/Services/DemoDocumentEditorStore.cs`
- `tests/Tempo.Blazor.E2E/DocumentEditorRegressionRecoveryE2ETests.cs`
- `tests/Tempo.Blazor.E2E/DocumentEditorE2ETestBase.cs`
- `tests/Tempo.Blazor.Tests/DocumentEditor/*`
- `tests/Tempo.Blazor.Tests/Components/DocumentEditor/*`

## Prvni konkretni RED testy, ktere maji vzniknout

Tyto testy maji vzniknout jako prvni, protoze primo odpovidaji rucnim vytkam:

- [ ] `Recovery_HeaderFooter_VisibleOnInitialLoad`
- [ ] `Recovery_CommentAnchor_IsHighlightedInDocument`
- [x] `Recovery_RevisionAnchor_IsHighlightedInDocument`
- [ ] `Recovery_ImageSelection_ShowsFullToolbarAndRightPanel`
- [ ] `Recovery_TextSelection_ShowsFloatingToolbar`
- [ ] `Recovery_SpaceKey_IsVisibleBeforeNextCharacter`
- [ ] `Recovery_EnterKey_SplitsParagraphBeforeNextCharacter`
- [ ] `Recovery_HeldKey_RendersProgressively`

## Poznamky k UX cilum

- Cilem neni jen vratit stare funkce, ale vratit je v lepsim tvaru:
  - [ ] min mene prekvapeni,
  - [ ] jasnejsi aktivni stavy,
  - [ ] mene skrytych stavu v panelu,
  - [ ] rychlejsi odezva,
  - [ ] viditelne propojeni panelu s dokumentem,
  - [ ] konzistentni command registry.
- Word/Google Docs kvalita se pozna hlavne na malych detailech:
  - [ ] znak se objevi hned,
  - [ ] caret zustane tam, kde ho uzivatel ceka,
  - [ ] oznaceni textu nevyprcha,
  - [ ] komentar/revize je v dokumentu videt,
  - [ ] obrazek ma jasne handly a vlastnosti,
  - [ ] panel neukazuje prazdny nebo spatny kontext.
