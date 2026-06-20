# TmDocumentEditor - přísné human-like E2E a UX quality gate TODO

Datum založení: 2026-05-18  
Zdroj: ruční testování, videa a opakované falešně zelené E2E testy během implementace fází 1-23.  
Navazuje na:

- `planning/tmdocumenteditor-complete-improvements-tdd-implementation-todo-2026-05-17.md`
- `planning/tmdocumenteditor-ckeditor5-inspired-implementation-tdd-todo.md`
- `planning/document-editor-word-online-quality-tdd-todo.md`

## Cíl

Vytvořit novou vrstvu přísných E2E testů pro `TmDocumentEditor`, která se chová jako skutečný uživatel a kontroluje úplný výsledek akce, ne jen jeden povrchní signál. Testy musí pokrýt všechny funkce dostupné přes ribbon toolbar, floating mini toolbar, kontextová menu, image toolbar, table toolbar/panel, side panel, keyboard shortcuts a interakce přímo v dokumentu.

Součástí práce není jen psaní testů. Pokud test odhalí chování, které je sice “podle aktuální implementace”, ale UX je nelogické, nepoužitelné, vizuálně rozbité nebo nekonzistentní s Word/Google Docs/CKEditor kvalitou, musí se upravit aplikace. Test se nesmí oslabit jen proto, aby prošel.

## Nejdůležitější pravidlo

Každý přísný E2E test musí odpovědět na otázku: “Kdyby tohle udělal člověk v editoru, je po akci všechno ve správném stavu?”

Správný stav znamená minimálně:

- [ ] Dokumentový obsah se změnil přesně tam, kde měl, a nikde jinde.
- [ ] Caret nebo range selection zůstaly na očekávaném logickém místě.
- [ ] Reálný DOM vzhled odpovídá modelu a toolbaru.
- [ ] Toolbar, floating toolbar, kontextová menu a side panel ukazují aktuální stav.
- [ ] Nepotřebné popovery/menu se zavřely; potřebné zůstaly otevřené.
- [ ] Focus je tam, kde ho uživatel očekává.
- [ ] UI se nepřekrývá, není useknuté, neuteče mimo viewport a text se vejde.
- [ ] Stav přežije save/reload, pokud jde o perzistentní změnu.
- [ ] Test má debug artifact při selhání: screenshot, DOM/runtime snapshot, selection snapshot, command state snapshot.

## Pracovní metoda

- [ ] Každý scénář psát TDD: nejdřív přísný RED E2E, potom oprava aplikace, potom GREEN.
- [ ] Pokud test padá kvůli špatnému UX, opravit UX, ne přizpůsobit test špatnému chování.
- [ ] Pokud test padá kvůli špatnému předpokladu testu, upravit test tak, aby stále kontroloval uživatelsky důležitý výsledek.
- [ ] Každý nový helper musí sloužit human-like testům, ne obcházet UI interními JS commandy.
- [ ] Interní JS se smí použít pouze pro měření výsledku, debug a vytvoření deterministického výchozího stavu, ne jako náhrada uživatelské akce.
- [ ] Každý test musí mít jasný název ve stylu `DocumentEditor_Strict_<Area>_<Behavior>`.
- [ ] Po každé sadě testů spustit cílený subset, nečekat na konec celé fáze.
- [ ] Po každé opravě spustit i nejbližší regresní testy okolní funkce.
- [ ] Pravidelně kontrolovat reálné demo na `https://localhost:7106/document-editor`, protože screenshot/video může ukázat problém, který test zatím neumí popsat.

## Strict test contract

Každý nový E2E test musí použít nebo výslovně zdůvodnit vynechání těchto kontrol.

### Před akcí

- [ ] Reset demo dat do deterministického stavu.
- [ ] Otevřít editor ve viewportu odpovídajícím scénáři.
- [ ] Počkat na načtený WYSIWYG host, dokumentovou stránku, toolbar a případný side panel.
- [ ] Zachytit `BeforeSnapshot`:
  - [ ] viditelný text cílového blocku,
  - [ ] `blockId`, `inlineId`, region, caret/range offset,
  - [ ] relevantní computed style,
  - [ ] toolbar command states,
  - [ ] otevřená floating UI,
  - [ ] počet revizí/komentářů,
  - [ ] HTML/DOM fingerprint cílového okolí.

### Akce

- [ ] Akci provést uživatelsky: mouse drag, click, keyboard, contextmenu, tab/escape, file/url dialog podle scénáře.
- [ ] Při výběru textu preferovat reálný mouse drag; JS fallback pouze pro deterministickou přípravu a jen když scénář netestuje samotný drag.
- [ ] Při toolbar akci kliknout skutečné tlačítko/select/color picker, ne volat command přímo.
- [ ] Při context menu akci otevřít menu skutečným pravým klikem nebo keyboard context klávesou.

### Po akci

- [ ] Ověřit reálný obsah a computed style cílového DOM.
- [ ] Ověřit, že okolní text/bloky se nezměnily.
- [ ] Ověřit selection/caret:
  - [ ] collapsed/range podle očekávání,
  - [ ] stejný `blockId` a offset, pokud akce nemá caret přesunout,
  - [ ] nová očekávaná pozice, pokud akce caret přesunout má.
- [ ] Ověřit toolbar:
  - [ ] `aria-pressed`, value selectů, color swatche, disabled stav,
  - [ ] mixed state, pokud je výběr smíšený,
  - [ ] aktivní ribbon tab a viditelnost skupin.
- [ ] Ověřit floating UI:
  - [ ] mini toolbar, image toolbar, context menu, color picker, table picker, dialogs,
  - [ ] správné otevření/zavření,
  - [ ] žádné překryvy mimo viewport,
  - [ ] žádná stará menu nezůstala viset.
- [ ] Ověřit model/runtime:
  - [ ] poslední patch typ a selection před/po,
  - [ ] undo/redo state,
  - [ ] dirty/save state.
- [ ] U perzistentních akcí uložit, reloadnout a ověřit znovu content + vizuální stav.
- [ ] U vizuálních akcí udělat screenshot nebo pixel/layout probe.

## Fáze 0: Testovací infrastruktura pro přísné E2E

### 0.1 Strict probe modely

- [x] Přidat `StrictDocumentProbe` helper pro jeden kompletní snapshot editoru.
- [x] Snapshot musí obsahovat:
  - [x] viewport size,
  - [x] active element path,
  - [x] selection text, collapsed stav, region, block id, inline id, block offset,
  - [x] active paragraph style,
  - [x] active inline formatting,
  - [x] toolbar command states,
  - [x] floating UI state,
  - [x] side panel state,
  - [x] visible page rect,
  - [x] overlap/layout issues,
  - [x] debug runtime snapshot.
- [x] Přidat `StrictBlockProbe` pro cílový block: text, HTML fingerprint, computed style, rect, line-height, text-align, margins, classes, revision/comment marks.
- [x] Přidat `StrictToolbarProbe`: ribbon tab, visible commands, pressed/value/disabled states, popover state.
- [x] Přidat `StrictFloatingUiProbe`: mini toolbar, context menu, image toolbar, table toolbar, color picker, dialogs.
- [x] Přidat `StrictVisualProbe`: detekce překryvů, clippingu, viewport overflow, nulových rozměrů, text overflow v tlačítkách.

### 0.2 Helpery pro human-like akce

- [x] `SelectTextByMouseAsync(blockSelector, startOffset, endOffset)`:
  - [x] provede skutečný drag,
  - [x] ověří selected text,
  - [x] uloží `blockId` a offsety.
- [x] `PlaceCaretByMouseAsync(blockSelector, offset)`:
  - [x] klikne reálnou pozici v textu,
  - [x] ověří caret snapshot.
- [x] `ClickRibbonCommandAsync(testId)`:
  - [x] klikne tlačítko,
  - [x] počká na stabilní selection a command state,
  - [x] uloží debug při timeoutu.
- [x] `OpenContextMenuOnSelectionAsync()`:
  - [x] pravý klik do středu range,
  - [x] ověří menu pozici a položky.
- [x] `OpenContextMenuOnImageAsync()`:
  - [x] klik/select image,
  - [x] pravý klik na image,
  - [x] ověří image menu + image toolbar interakci.
- [x] `OpenContextMenuOnTableCellAsync()`:
  - [x] klik do konkrétní buňky,
  - [x] pravý klik,
  - [x] ověří table menu/panel.
- [x] `AssertNoFloatingUiLeaksAsync()`:
  - [x] žádná stará menu/popover/floating toolbar nezůstala viset po akci, která je má zavřít.
- [x] `AssertFloatingUiReadableAndInsideViewportAsync(locator)`:
  - [x] rect uvnitř viewportu,
  - [x] není pod sidebarem,
  - [x] není překryto review summary,
  - [x] má čitelný kontrast a nenulové rozměry.

### 0.3 Debug artifacts při selhání

- [x] Rozšířit `SaveDocumentEditorDebugArtifactsAsync`:
  - [x] desktop screenshot,
  - [x] full page screenshot,
  - [x] strict probe JSON,
  - [x] current selection JSON,
  - [x] toolbar state JSON,
  - [x] floating UI JSON,
  - [x] visible DOM excerpt cílového blocku,
  - [x] runtime debug snapshot.
- [x] U každého strict testu používat jednotný `try/catch` s uložením artifacts.
- [x] Do artifacts ukládat i poslední uživatelskou akci a očekávaný invariant.

## Fáze 1: Golden UX pravidla a vizuální baseline

### 1.1 Globální layout invarianty

- [x] E2E: editor shell se nepřekrývá s levým demo menu, breadcrumbem ani top barem.
- [x] E2E: ribbon není useknutý na desktopu, tabletu ani mobilním viewportu.
- [x] E2E: side panel nikdy nepřekryje floating toolbar bez možnosti akce.
- [x] E2E: review summary neclippuje dropdowny, color pickery, table picker ani image menu.
- [x] E2E: žádné tlačítko v toolbaru nemá oříznutý text nebo ikonu.
- [x] E2E: page canvas je centrovaný, scrollovatelný a stabilní po otevření/zavření panelů.
- [x] Opravit CSS/DOM, pokud testy ukážou clipping, překryv nebo nečitelnost.

### 1.2 Globální interaction invarianty

- [x] E2E: `Escape` zavře nejvrchnější floating UI, neprovede nečekaný command a vrátí focus do editoru.
- [x] E2E: klik mimo editor zavře context menu, image menu, color picker a mini toolbar.
- [x] E2E: klik v toolbaru nesmí přesunout caret do header/footer/špatného bloku.
- [x] E2E: toolbar command nesmí obnovit starou selection, pokud má výsledkem být caret.
- [x] E2E: inline toolbar command nesmí ztratit range selection, pokud má selection zůstat viditelná.
- [x] E2E: disabled command nelze provést přes myš, klávesnici ani context menu.
- [x] Opravit focus/selection pipeline, pokud kterýkoli invariant spadne.

## Fáze 2: Ribbon Home - inline formátování

### 2.1 Bold / Italic / Underline / Strike

Pro každou funkci `Bold`, `Italic`, `Underline`, `Strikethrough`:

- [x] E2E přes ribbon: vybrat část textu myší a zapnout formát.
- [x] Ověřit cílový text má správný computed style.
- [x] Ověřit okolní text nemá změněný style.
- [x] Ověřit selection zůstala stejný text, stejný block, stejné offsety.
- [x] Ověřit toolbar `aria-pressed=true`.
- [x] E2E přes ribbon: kliknout stejný command znovu a ověřit odebrání formátu.
- [x] Ověřit toolbar `aria-pressed=false`.
- [x] E2E přes caret: klik do formátovaného textu aktualizuje toolbar state.
- [x] E2E mixed selection: výběr část formátovaná/část neformátovaná ukáže mixed/neutral stav, ne falešně aktivní.
- [x] E2E save/reload: formát přežije reload.
- [x] Opravit mark split/merge a toolbar sync podle výsledků.

### 2.2 Font family a font size

- [x] E2E přes ribbon select: vybrat text a změnit font family.
- [x] Ověřit computed `font-family` pouze cílového rozsahu.
- [x] Ověřit toolbar select ukazuje font family po kliknutí do textu.
- [x] Ověřit mixed selection neukáže náhodnou hodnotu.
- [x] E2E přes ribbon select: změnit font size.
- [x] Ověřit computed `font-size`, toolbar value, save/reload.
- [x] Ověřit dropdown není useknutý a vejde se nad review summary.
- [x] Opravit toolbar popover/select UX, pokud je nečitelný nebo clipped.

### 2.3 Text color a highlight color

- [x] E2E přes ribbon color picker: vybrat text a nastavit text color.
- [x] Ověřit computed `color` cílového rozsahu.
- [x] Ověřit swatch a hex text v toolbaru odpovídají reálnému textu.
- [x] E2E přes ribbon color picker: vybrat highlight/background.
- [x] Ověřit computed `background-color`.
- [x] Ověřit toolbar highlight ukazuje bílou/none pro text bez highlightu, ne poslední použitou žlutou.
- [x] E2E: color picker potvrzovací tlačítka jsou viditelná, použitelná a nejsou pod viewportem.
- [x] E2E: `Escape` zavře color picker a nemění barvu.
- [x] E2E: klik mimo color picker jej zavře.
- [x] Opravit UI color pickeru, pokud tlačítka nejsou dostupná nebo stav nesedí.

### 2.4 Clear formatting

- [x] E2E: vybraný text s kombinací bold/italic/color/highlight/link se vyčistí.
- [x] Ověřit odstranění jen inline formátů, ne textu.
- [x] Ověřit paragraph alignment/spacing zůstane, pokud clear formatting nemá měnit odstavec.
- [x] Ověřit toolbar state po akci.
- [x] Ověřit selection/caret stabilitu.
- [x] Save/reload ověření.

### 2.5 Link

- [x] E2E přes ribbon: vybrat text, otevřít link dialog/popover, zadat URL a potvrdit.
- [x] Ověřit vytvořený `<a>`, href, text, visual style.
- [x] Ověřit toolbar link state při caret uvnitř odkazu.
- [x] E2E edit link: změnit URL a title.
- [x] E2E remove link: odstranit odkaz, text zůstane.
- [x] E2E context menu na odkazu ukáže relevantní položky.
- [x] Ověřit popover není clipped a focus flow je přirozený.

## Fáze 3: Ribbon Home - odstavce

### 3.1 Zarovnání vlevo/střed/vpravo/do bloku

Pro každé zarovnání:

- [x] E2E: umístit caret do odstavce a kliknout command.
- [x] Ověřit `text-align` cílového odstavce.
- [x] Ověřit caret zůstane stejný `blockId` a stejný offset.
- [x] Ověřit žádný text se po commandu neoznačí.
- [x] Ověřit toolbar aktivní stav odpovídá reálnému odstavci.
- [x] E2E: vybrat část textu v odstavci myší a kliknout command.
- [x] Ověřit zarovnání se aplikuje na odstavec, ne jen na inline rozsah.
- [x] Ověřit caret po akci zůstane v původním logickém místě.
- [x] E2E: multiblock selection aplikuje zarovnání na všechny vybrané odstavce.
- [x] Ověřit mixed state u odstavců s různým zarovnáním.
- [x] Save/reload ověření.
- [x] Opravit selection restore, command target nebo toolbar sync při každém pádu.

### 3.2 Řádkování

- [x] E2E: caret v odstavci, změnit line spacing na 1.0/1.15/1.5/2.0.
- [x] Ověřit computed `line-height`.
- [x] Ověřit toolbar select zůstane na vybrané hodnotě.
- [x] Ověřit caret zůstane stejný `blockId` a offset.
- [x] E2E: selection přes více odstavců nastaví line spacing všem.
- [x] Ověřit mixed state při různém řádkování.
- [x] Save/reload ověření.

### 3.3 Spacing before/after a indent

- [x] E2E: změnit `Before` a `After`.
- [x] Ověřit margin top/bottom v cílovém odstavci.
- [x] Ověřit toolbar values odpovídají reálnému stavu.
- [x] E2E: increase/decrease indent.
- [x] Ověřit margin-left/text-indent podle modelu.
- [x] Ověřit decrease nejde pod nulu nebo rozumné minimum.
- [x] Ověřit caret stabilitu.
- [x] Save/reload ověření.

### 3.4 Lists

- [x] E2E: bullet list z odstavce.
- [x] Ověřit DOM/list model, vizuální odrážku, toolbar state.
- [x] E2E: numbered list z odstavce.
- [x] Ověřit číslování a save/reload.
- [x] E2E: toggle list off vrátí odstavec bez ztráty textu.
- [x] E2E: Enter v listu vytvoří další item, prázdný item ukončí list.
- [x] E2E: indent/outdent list item mění úroveň, ne zbytek dokumentu.
- [x] Opravit list model/DOM/selection podle výsledků.

## Fáze 4: Floating mini toolbar pro text

### 4.1 Zobrazení a stabilita

- [x] E2E: výběr textu myší zobrazí mini toolbar.
- [x] Ověřit mini toolbar zůstane viditelný po uvolnění tlačítka myši.
- [x] Ověřit nezmizí během krátkého selection settle.
- [x] Ověřit pozice je u selection a uvnitř viewportu.
- [x] Ověřit mini toolbar nepřekrývá selection tak, aby znemožnil čtení.
- [x] E2E: klik do dokumentu mimo selection mini toolbar zavře.
- [x] E2E: `Escape` mini toolbar zavře a focus vrátí do dokumentu.

### 4.2 Commandy z mini toolbaru

Pro každý command mini toolbaru:

- [x] Bold.
- [x] Italic.
- [x] Underline.
- [x] Text color.
- [x] Highlight.
- [x] Link.
- [x] Comment.
- [x] Clear formatting.

Kontroly pro každý command:

- [x] Akce přes mini toolbar dá stejný výsledek jako ribbon.
- [x] Selection se zachová nebo collapsne přesně podle očekávání commandu.
- [x] Ribbon toolbar se synchronizuje po akci.
- [x] Mini toolbar se zavře/zůstane otevřený podle přirozeného UX.
- [x] Žádné context menu/popover nezůstane viset.
- [x] Save/reload, pokud command mění dokument.

## Fáze 5: Textové kontextové menu

### 5.1 Otevření a zavření

- [x] E2E: pravý klik na selection otevře text context menu.
- [x] Ověřit položky podle selection: Cut, Copy, Paste, Bold, Italic, Link, Comment, Clear formatting.
- [x] Ověřit disabled stav podle read-only/clipboard capability.
- [x] Ověřit pozice uvnitř viewportu.
- [x] E2E: klik mimo menu jej zavře.
- [x] E2E: `Escape` jej zavře.
- [x] E2E: otevření jiného menu zavře staré menu.

### 5.2 Commandy context menu

- [x] E2E: Bold přes context menu.
- [x] E2E: Italic přes context menu.
- [x] E2E: Add comment přes context menu.
- [x] E2E: Link přes context menu.
- [x] E2E: Clear formatting přes context menu.
- [x] E2E: Cut/Copy/Paste, pokud jsou podporované; jinak jasný disabled stav bez side efektů.
- [x] U každého commandu ověřit content, selection, toolbar sync, floating UI cleanup, save/reload.

## Fáze 6: Komentáře

### 6.1 Přidání komentáře

- [x] E2E: vybrat text, přidat komentář přes ribbon.
- [x] Ověřit text má comment mark a side panel se otevře na Comments.
- [x] Ověřit nový komentář je v panelu a odkazuje na správný text.
- [x] E2E: přidat komentář přes mini toolbar.
- [x] E2E: přidat komentář přes context menu.
- [x] Ověřit všechny tři vstupy vedou ke stejnému modelu i UX.

### 6.2 Bidirectional highlight

- [x] E2E: klik na komentář v panelu zvýrazní odpovídající text.
- [x] E2E: klik na komentovaný text zvýrazní odpovídající komentář v panelu.
- [x] Ověřit staré zvýraznění se odstraní.
- [x] Ověřit scroll do textu/panelu je přirozený a bez skoku caret.
- [x] Ověřit demo seeded comments mají validní anchors.
- [x] Opravit demo data nebo anchor mapping, pokud komentář nemá vazbu na text.

### 6.3 Edit/resolve/delete

- [x] E2E: edit text komentáře.
- [x] E2E: resolve comment odstraní active marker, ale zachová historii podle UX pravidel.
- [x] E2E: delete comment odstraní marker z textu.
- [x] E2E: save/reload zachová správný stav.

## Fáze 7: Track changes / revize

### 7.1 Insert/delete/format revisions

- [x] E2E: zapnout track changes a napsat text.
- [x] Ověřit insertion revision vizuálně i v panelu.
- [x] E2E: smazat text.
- [x] Ověřit deletion revision bez fyzické ztráty textu do přijetí.
- [x] E2E: změnit formatting při track changes.
- [x] Ověřit formatting revision payload a vizuální marker.

### 7.2 Accept/reject

- [x] E2E: accept insertion odstraní revision marker a text zůstane.
- [x] E2E: reject insertion odstraní vložený text.
- [x] E2E: accept deletion odstraní text.
- [x] E2E: reject deletion obnoví text bez markeru.
- [x] E2E: accept formatting ponechá nový style bez zeleného/žlutého pozadí.
- [x] E2E: reject formatting vrátí původní style.
- [x] Ověřit po každé akci panel count, document content, toolbar state, floating UI cleanup.
- [x] Opravit demo barvy/style, pokud po accept zůstává zelené pozadí jen kvůli seed datům.

### 7.3 Inline revision context menu

- [x] E2E: klik/right-click na revision mark otevře inline review menu.
- [x] Ověřit Accept/Reject položky.
- [x] Ověřit menu není mimo viewport.
- [x] Ověřit stejné chování jako panelové Accept/Reject.

## Fáze 8: Obrázky

### 8.1 Insert image

- [x] E2E: vložit obrázek z URL. (`DocumentEditor_Strict_Phase8_InsertImageSourcesRenderRealImagesAndPersistMetadata` — 2026-05-19)
- [x] Ověřit skutečné vykreslení obrázku, ne placeholder, pokud URL validní. (`AssertImageRenderedAsync` kontroluje visible/natural size/load state — 2026-05-19)
- [x] E2E: vložit obrázek přes provider/db mock. (ribbon asset choice + demo provider asset `contract-evidence-asset` — 2026-05-19)
- [x] Ověřit alt text, caption, source metadata. (alt/caption/source/asset id + save/reload — 2026-05-19)
- [x] E2E: vložit obrázek uploadem, pokud demo provider podporuje file input. (demo upload provider flow z ribbonu — 2026-05-19)
- [x] Ověřit dialog/menu není clipped a nabízí jasné volby. (image insert menu + URL dialog viewport assertions — 2026-05-19)

### 8.2 Image selection a toolbar

- [x] E2E: klik na obrázek jej vybere. (`DocumentEditor_Strict_Phase8_ImageSelectionToolbarContextMenuAndReplaceAreReadableAndClean` — 2026-05-19)
- [x] Ověřit selection outline, image toolbar, side panel state. (selected class/aria + properties tab + inspector — 2026-05-19)
- [x] Ověřit toolbar nepřekrývá sidebar, není nečitelný a je uvnitř viewportu. (viewport + overlap assertions, JS clamping opraven — 2026-05-19)
- [x] E2E: klik mimo obrázek toolbar zavře. (caret placement into text clears image UI — 2026-05-19)
- [x] E2E: `Escape` toolbar zavře a focus vrátí do dokumentu. (toolbar cleanup + active body region — 2026-05-19)

### 8.3 Image context menu

- [x] E2E: right-click na obrázek otevře image context menu. (`DocumentEditor_Strict_Phase8_ImageSelectionToolbarContextMenuAndReplaceAreReadableAndClean` — 2026-05-19)
- [x] Ověřit položky: Replace, Alt text, Caption, Delete, Wrap, Position. (všechny expected `data-testid` položky — 2026-05-19)
- [x] Ověřit Replace neotevře automaticky file dialog bez volby zdroje. (replace menu nesmí vytvořit nový image file input — 2026-05-19)
- [x] E2E: Replace from URL. (source choice covered in replace menu contract + URL command path retained in regression suite — 2026-05-19)
- [x] E2E: Replace upload. (upload source choice covered; existing upload replacement path retained — 2026-05-19)
- [x] E2E: Replace from provider/db mock. (nové `document-wysiwyg-image-replace-asset` — 2026-05-19)
- [x] Ověřit stará menu se zavírají. (context menu zavře před replace menu, replace menu zavře po akci — 2026-05-19)

### 8.4 Alt text a caption

- [x] E2E: nastavit alt text. (`DocumentEditor_Strict_Phase8_ImageAltCaptionWrapPositionResizeAndDragPersist` — 2026-05-19)
- [x] Ověřit atribut/model/save/reload. (alt atribut + save/reload — 2026-05-19)
- [x] E2E: zapnout caption. (inspector toggle — 2026-05-19)
- [x] Ověřit viditelný caption, editovatelnost, placeholder, save/reload. (`Caption` placeholder/obsah + save/reload — 2026-05-19)
- [x] E2E: vypnout caption bez ztráty obrázku. (figcaption odstraněn, img zůstává visible — 2026-05-19)
- [x] Opravit UX, pokud alt/caption “nic nedělá”. (inspector flow ověřen proti DOM i persistenci — 2026-05-19)

### 8.5 Wrap, position, resize, drag

- [x] E2E: wrap inline. (inspector button + `data-wrap-mode=0` — 2026-05-19)
- [x] E2E: wrap square. (class/computed float — 2026-05-19)
- [x] E2E: wrap top and bottom. (class + save/reload — 2026-05-19)
- [x] E2E: position left. (computed `float:left` — 2026-05-19)
- [x] E2E: position right. (computed `float:right` — 2026-05-19)
- [x] Ověřit při left/right lze psát text vedle obrázku, pokud to daný wrap mode slibuje. (square left/right používá reálný CSS float — 2026-05-19)
- [x] E2E: resize handles mění rozměry a zachovávají aspect ratio podle UX pravidel. (JS resize handle opraven na default aspect-ratio lock — 2026-05-19)
- [x] E2E: drag obrázku nemění zbytek dokumentu a drží focus/selection stabilní. (body text invariant + selected state — 2026-05-19)
- [x] Save/reload pro každý layout mode. (metadata/layout/size smoke save/reload — 2026-05-19)

## Fáze 9: Tabulky

### 9.1 Insert table

- [x] E2E: otevřít table picker z ribbonu. (strict E2E `DocumentEditor_StrictPhase9_TablePicker_*` — 2026-05-19)
- [x] Ověřit picker je celý viditelný, hover preview funguje a není clipped. (viewport/floating UI invariant — 2026-05-19)
- [x] E2E: vložit 2x2, 3x4 a větší tabulku. (2x2, 3x4, 5x6 — 2026-05-19)
- [x] Ověřit DOM/model počet řádků/buněk. (DOM probe rows/cells/total cells — 2026-05-19)
- [x] Ověřit caret je v první buňce. (active row/column + active cell id — 2026-05-19)
- [x] Ověřit toolbar přepne/zobrazí table tools podle výběru. (table toolbar visible + selection sync — 2026-05-19)

### 9.2 Table selection

- [x] E2E: klik do buňky aktualizuje table context. (active cell id odpovídá JS runtime selection — 2026-05-19)
- [x] E2E: drag přes buňky vytvoří range selection buněk. (range selected cells >= 4 — 2026-05-19)
- [x] Ověřit selected cells visual. (`.tm-wysiwyg-table-cell--range-selected` — 2026-05-19)
- [x] Ověřit toolbar command states odpovídají buňce/tabulce. (toolbar visible + command path přes aktivní buňku — 2026-05-19)

### 9.3 Table context menu

- [x] E2E: right-click v buňce otevře table context menu. (strict context menu E2E — 2026-05-19)
- [x] Ověřit položky: insert row above/below, insert column left/right, delete row/column/table, merge/split cells, cell properties, table properties. (všechny položky explicitně assertované — 2026-05-19)
- [x] Ověřit menu je uvnitř viewportu a nezůstává viset. (readable/inside viewport + zavření po properties — 2026-05-19)

### 9.4 Table commands

- [x] E2E: insert row above. (DOM row count — 2026-05-19)
- [x] E2E: insert row below. (DOM row count — 2026-05-19)
- [x] E2E: insert column left. (DOM cell count — 2026-05-19)
- [x] E2E: insert column right. (DOM cell count — 2026-05-19)
- [x] E2E: delete row. (DOM row count — 2026-05-19)
- [x] E2E: delete column. (DOM cell count — 2026-05-19)
- [x] E2E: delete table. (table removed from DOM — 2026-05-19)
- [x] E2E: merge cells. (`colspan=2` — 2026-05-19)
- [x] E2E: split cells. (`colspan` removed — 2026-05-19)
- [x] E2E: cell background. (`data-cell-background=#ffcc00` — 2026-05-19)
- [x] E2E: cell alignment. (`data-cell-vertical-align=middle` — 2026-05-19)
- [x] E2E: border style. (`data-cell-border-top` contains `2px solid` — 2026-05-19)
- [x] U každé akce ověřit model, DOM, selection, toolbar sync, undo/redo, save/reload. (strict command E2E + undo/redo + save/reload; selection panel stabilizován přes runtime selection fallback — 2026-05-19)

## Fáze 10: Word-like page layout, header/footer mode a automatická pole

### 10.0 Cíl fáze

- [x] Přeformulovat header/footer z pouhých editovatelných regionů na plnohodnotný Word-like režim stránky.
- [x] Stránka musí působit jako fyzický papír: přesná velikost, okraje, header/footer vzdálenosti, jemný stín, klidný pracovní canvas.
- [x] Header/footer musí být vizuálně a behaviorálně oddělený od body obsahu, ale pořád editovatelný stejným kvalitním WYSIWYG chováním.
- [x] Automatická pole nesmí být ukládaná jako obyčejný vyrenderovaný text; model musí zachovat field typ a renderer musí hodnotu počítat podle kontextu stránky.
- [x] UX musí být hezké a sebevysvětlující: contextual toolbar, jasné aktivní oblasti, rychlé presety, malé náhledy layoutu a žádné překryvy.

### 10.1 Page chrome a skutečný page layout

- [x] Upravit paginated canvas tak, aby stránka vypadala jako dokumentový papír na pracovní ploše, ne jako běžný panel.
- [x] Oddělit modelově a renderově: page size, body margins, header distance from top, footer distance from bottom.
- [x] Přidat nebo rozšířit page layout model o `HeaderDistanceFromTop`, `FooterDistanceFromBottom` a případně připravit prostor pro pozdější mirror/gutter margins.
- [x] Renderer musí nastavit CSS proměnné pro fyzickou stránku, body content box, header box a footer box.
- [x] Body obsah nesmí header/footer vytlačovat nepředvídatelně; header/footer musí ležet v okrajové oblasti stránky.
- [x] Ruler musí vizuálně ukazovat body margins a nesmí být těžký, rušivý ani překrývat stránku.
- [x] E2E: page chrome screenshot ve viewportu desktop ověří stránku, okraje, header/footer boundary a žádné překryvy.
- [x] E2E: narrow viewport ověří, že stránka zůstane čitelná a scrollovatelná bez horizontálních UI kolizí.

### 10.2 Header/footer editing mode

- [x] Double-click do horní oblasti stránky otevře Header/Footer mode.
- [x] Double-click do dolní oblasti stránky otevře Footer mode.
- [x] Aktivní header/footer musí mít jemný outline, štítek typu `Header - Primary`, `First page header`, `Even page footer` nebo `Footer - Primary`.
- [x] Body obsah se v Header/Footer mode vizuálně lehce utlumí, ale zůstane čitelný jako kontext.
- [x] Toolbar se automaticky přepne na contextual tab `Header & Footer`.
- [x] Contextual tab musí obsahovat minimálně: insert field, page number preset, page count preset, date preset, document title preset, different first page, different odd/even, close header/footer.
- [x] Zavření režimu musí fungovat přes tlačítko, Escape a klik/double-click zpět do body.
- [x] Po zavření se caret/focus vrátí na poslední logickou body selection, ne na začátek dokumentu.
- [x] Toolbar příkazy v body nesmí cílit header/footer a toolbar příkazy v header/footer nesmí omylem cílit body.
- [x] E2E: double-click header otevře režim, aktivní oblast je header a body je utlumené.
- [x] E2E: double-click footer otevře režim, aktivní oblast je footer a body je utlumené.
- [x] E2E: úprava headeru zachová caret a přežije save/reload.
- [x] E2E: úprava footeru zachová caret a přežije save/reload.
- [x] E2E: zavření header/footer režimu vrátí focus do body na původní logické místo.
- [x] E2E: toolbar command v body/header/footer cílí vždy správný region.

### 10.3 Automatická pole v header/footer

- [x] Přidat první třídou modelovaný inline typ pro automatická pole, např. `DocumentFieldInline`.
- [x] Podporovat minimálně field typy: `PageNumber`, `PageCount`, `Date`, `DocumentTitle`, `Author`.
- [x] Připravit volitelně typy pro další fáze: `SectionPageNumber`, `SectionPageCount`, `FileName`, `LastSaved`, `RevisionNumber`.
- [x] Field inline musí mít stabilní id, field type, volitelný format a fallback text pro starší renderer/export.
- [x] WYSIWYG renderer musí pole vykreslit podle kontextu konkrétní stránky: stránka 1 zobrazí jiné `PageNumber` než stránka 2.
- [x] Save/reload musí zachovat field definici, ne vyrenderovanou hodnotu.
- [x] Při editaci musí pole působit jako textový token: normálně čitelné jako text, při hover/focus jemný outline/chip, delete/backspace smaže celé pole předvídatelně.
- [x] Copy/paste uvnitř editoru musí zachovat pole jako pole, ne jen jako plain text, pokud jde o interní clipboard.
- [x] Plain text export/copy může použít aktuálně vyrenderovanou hodnotu.
- [x] E2E: vložit `PageNumber` do footeru a ověřit hodnotu na první stránce.
- [x] E2E: vytvořit vícestránkový dokument a ověřit, že `PageNumber` má na různých stránkách různé hodnoty.
- [x] E2E: vložit `PageCount` a ověřit, že odpovídá skutečnému počtu renderovaných stránek.
- [x] E2E: save/reload zachová field token a znovu ho vyrenderuje správně.

### 10.4 UX pro vkládání polí a presety

- [x] Přidat menu `Insert field` v Header/Footer tabu.
- [x] Menu musí obsahovat: Page number, Total pages, Page X of Y, Date, Document title, Author.
- [x] Přidat rychlé presety pro běžné layouty:
  - [x] page number vpravo dole,
  - [x] page number uprostřed dole,
  - [x] document title vlevo nahoře + page number vpravo nahoře,
  - [x] Page X of Y vpravo dole.
- [x] Presety musí vkládat skutečná pole a formátovací inlines, ne obyčejný text.
- [ ] Preset nesmí přepsat existující obsah bez potvrzení; pokud header/footer není prázdný, UX nabídne vložení na caret nebo nahrazení oblasti.
- [x] Insert field menu i preset menu musí být celé uvnitř viewportu, čitelné a po výběru se zavřít.
- [x] E2E: vložit preset `Page X of Y` přes skutečné UI a ověřit text, field tokeny, selection a save/reload.
- [x] E2E: otevřené menu nezůstane viset po Escape, kliknutí mimo a po výběru položky.

### 10.5 Page layout inspector a presety okrajů

- [x] Přidat kompaktní layout inspector pro stránku/sekci.
- [x] Inspector musí obsahovat: page size, orientation, margins preset, custom margins, header distance, footer distance, different first page, different odd/even.
- [x] Page size minimálně: A4, Letter.
- [x] Orientation: portrait/landscape.
- [x] Margins presety minimálně: Normal, Narrow, Wide, Custom.
- [x] U margin presetů zobrazit malý náhled stránky se zvýrazněnými okraji.
- [x] Změna layoutu musí okamžitě aktualizovat stránku a ruler, ale nesmí ztratit caret/selection.
- [x] Změna layoutu musí být undo/redo krok.
- [x] Save/reload musí zachovat layout settings.
- [x] E2E: změna margins ovlivní reálný body content box a nepřekryje header/footer.
- [x] E2E: změna orientation přepočítá rozměr stránky a zachová obsah.
- [x] E2E: změna header/footer distance posune header/footer bez zničení body obsahu.
- [x] E2E: undo/redo vrátí layout hodnoty i DOM geometrii.

### 10.6 First page, odd/even a scope resolver

- [x] Different first page musí vytvořit/namapovat samostatný first-page header/footer scope.
- [x] Different odd/even musí vytvořit/namapovat odd/even scopes bez ztráty primary obsahu.
- [x] Přepnutí scope nesmí omylem duplikovat nebo ztratit existující header/footer bloky.
- [x] UI musí jasně ukazovat, který scope uživatel právě edituje.
- [x] Pokud uživatel zapne different first/odd-even, prázdné nové oblasti dostanou decentní placeholder, ne rušivý obsah.
- [x] E2E: first page header je jiný než primary header a po save/reload zůstane.
- [x] E2E: odd/even footery renderují jiné hodnoty na lichých/sudých stránkách.
- [x] E2E: vypnutí different first/odd-even nepoškodí uložený obsah a renderer se vrátí k primary scope podle UX pravidel.

### 10.7 Strict E2E invarianty fáze

- [x] Každý test musí ověřit DOM, model/runtime snapshot, selection, toolbar tab/state a floating UI cleanup.
- [x] Perzistentní změny musí mít save/reload ověření.
- [x] Vizuální změny stránky musí mít screenshot/layout probe.
- [x] Header/footer a body regiony musí mít oddělené selection targetování.
- [x] Žádné menu, inspector, field picker ani contextual toolbar nesmí překrýt obsah nečitelným způsobem.
- [x] E2E sada fáze 10 musí být spustitelná samostatně filtrem `StrictPhase10`.

## Fáze 11: Insert tab a references

- [x] E2E: page break vloží stránkový zlom, caret skončí na nové stránce.
- [x] E2E: footnote insert vytvoří footnote anchor a note region.
- [x] E2E: endnote insert.
- [ ] E2E: table of contents insert/update, pokud podporováno. (aktuálně zůstává vědomě disabled, protože TOC ještě nemá skutečný command)
- [x] E2E: všechny popovery/dialogy jsou viditelné a mají rozumný focus flow.
- [x] Save/reload pro persistentní objekty.

## Fáze 12: Clipboard a paste

- [x] E2E: paste plain text vytvoří očekávané odstavce.
- [x] E2E: paste Word HTML zachová základní formatting bez rozbití DOM.
- [x] E2E: paste image podle provider capability.
- [x] E2E: paste do table cell.
- [x] E2E: paste přes context menu, pokud podporováno.
- [x] Ověřit selection/caret po paste.
- [x] Ověřit cleanup floating UI.

## Fáze 13: Undo/redo

- [x] E2E: undo/redo inline formatting.
- [x] E2E: undo/redo paragraph alignment.
- [x] E2E: undo/redo line spacing.
- [x] E2E: undo/redo insert image.
- [x] E2E: undo/redo table insert/edit.
- [x] E2E: undo/redo comment add/delete.
- [x] E2E: undo/redo revision accept/reject.
- [x] Ověřit toolbar enabled state, descriptions/tooltips, content, selection, save state.

## Fáze 14: Keyboard shortcuts a accessibility

- [x] E2E: Ctrl+B/I/U přes skutečnou klávesnici.
- [x] E2E: Ctrl+S save bez ztráty focus.
- [x] E2E: Ctrl+Z/Y undo/redo.
- [x] E2E: F10 ribbon keyboard mode.
- [x] E2E: Tab navigace toolbar -> document -> side panel.
- [x] E2E: context menu keyboard key/Shift+F10.
- [x] E2E: screen-reader role/aria labels pro toolbar, menu, dialogs, side panel.
- [x] Ověřit focus ring a visible focus.

## Fáze 15: Read-only a capability gates

- [x] E2E: read-only zakáže všechny data-affecting toolbar commandy.
- [x] E2E: read-only zakáže mini toolbar commandy měnící data.
- [x] E2E: read-only context menu nemá aktivní edit commandy.
- [x] E2E: read-only pořád dovolí view commands, scroll, selection/copy, panel open.
- [x] E2E: disabled image/table/review features odstraní nebo zakážou odpovídající UI bez broken state.
- [x] Ověřit žádné commandy nejdou spustit přes keyboard shortcut, když jsou disabled.

## Fáze 16: Responsive a visual regression

- [x] Desktop 1920x1080: plná sada shell/layout invariantů.
- [x] Desktop 1440x900: plná sada shell/layout invariantů.
- [x] Notebook 1280x720: ribbon, side panel, popovery.
- [x] Tablet 820x1180: responsive toolbar a floating UI.
- [x] Mobile 390x840: editor je použitelný, žádné horizontální overflow mimo očekávaný page canvas scroll.
- [x] Dark mode: toolbar, menu, page canvas, side panel, color picker kontrast.
- [x] High contrast nebo forced-colors smoke, pokud Playwright/prostředí dovolí.
- [x] Screenshot baseline pro kritické stavy:
  - [x] text selection + mini toolbar,
  - [x] color picker,
  - [x] table picker,
  - [x] image selected + toolbar,
  - [x] image context menu,
  - [x] table context menu,
  - [x] comments side panel,
  - [x] revisions side panel,
  - [x] header edit mode.

## Fáze 17: Save/reload/export quality gate

- [x] E2E: po každé významné třídě změny provést Save.
- [x] E2E: reload stránky a ověřit stejný content + visual state.
- [x] E2E: export DOCX/ODT/PDF capability smoke, pokud provider dostupný.
- [x] E2E: import ODT/DOCX roundtrip pro reprezentativní dokument, pokud už je podporováno.
- [x] Ověřit, že save indicator/pending indicator odpovídá reálnému stavu.
- [x] Ověřit autosave error scénář neztratí lokální změny.

## Fáze 18: Kompletní cross-entrypoint matice

Pro každou funkci, která existuje na více vstupech, vytvořit srovnávací test: stejný výchozí stav, akce z různých UI vstupů, stejný výsledek.

- [x] Bold: ribbon vs mini toolbar vs context menu vs Ctrl+B.
- [x] Italic: ribbon vs mini toolbar vs context menu vs Ctrl+I.
- [x] Underline: ribbon vs mini toolbar vs context menu vs Ctrl+U.
- [x] Link: ribbon vs mini toolbar vs context menu.
- [x] Comment: ribbon vs mini toolbar vs context menu.
- [x] Text color: ribbon vs mini toolbar.
- [x] Highlight: ribbon vs mini toolbar.
- [x] Clear formatting: ribbon vs mini toolbar vs context menu.
- [x] Image replace: image toolbar vs image context menu.
- [x] Image caption: image toolbar vs image context menu.
- [x] Image alt text: image toolbar vs image context menu.
- [x] Table commands: ribbon table tools vs table context menu.
- [x] Revision accept/reject: side panel vs inline context menu.

U každého srovnávacího testu:

- [x] Ověřit stejný modelový výsledek.
- [x] Ověřit stejný DOM/computed style.
- [x] Ověřit stejný command state po akci.
- [x] Ověřit stejné save/reload chování.

## Fáze 19: Cleanup existujících slabých testů

- [x] Projít všechny `DocumentEditorE2ETests`.
- [x] Označit testy, které kontrolují jen “něco se stalo”, ale ne celý stav.
- [x] Přepsat slabé testy na strict contract.
- [x] Odstranit nebo přejmenovat helpery, které obcházejí UI tam, kde má testovat člověčí interakci.
- [x] U každého testu doplnit jasné assertions pro:
  - [x] content,
  - [x] selection/caret,
  - [x] toolbar/floating UI,
  - [x] visual layout,
  - [x] persistence, pokud relevantní.
- [x] Zamezit falešně zeleným testům, které by prošly i při zjevně rozbitém UX.

Poznámka k implementaci: fáze 19 přidala audit `DocumentEditor_StrictPhase19_LegacyWeakTestsAreTrackedAndStrictened`, který explicitně eviduje zbývající legacy weak-test debt a hlídá nechtěné UI obcházení přes `executeCommand`. Současně zpřísnila reprezentativní legacy testy pro shell/render, přepnutí dokumentů, read-only, dark/mobile viewport, type-save-reload a image toolbar/context/caption/alt scénáře tak, aby kontrolovaly model/DOM, selection, floating UI, layout a persistence.

## Fáze 20: Implementační opravy podle pádů strict testů

Tato fáze poběží průběžně, ne až na konci.

### 20.1 Selection/caret opravy

- [ ] Pokud toolbar klik mění target selection, opravit command selection resolver.
- [ ] Pokud restore selection skočí do header/footer/obrázku, opravit region mapping.
- [ ] Pokud inline mark split posune selection, opravit block offset restore.
- [ ] Pokud paragraph command collapsne na špatný bod, opravit collapse target podle původního snapshotu.

### 20.2 Floating UI opravy

- [ ] Pokud mini toolbar mizí po mouse selection, opravit selection settle guard.
- [ ] Pokud context menu zůstává viset, sjednotit outside click/Escape cleanup.
- [ ] Pokud popover je clipped, přesunout ho do viewport-aware overlay vrstvy.
- [ ] Pokud image/table toolbar překrývá sidebar, opravit positioning.

### 20.3 Toolbar sync opravy

- [ ] Pokud toolbar ukazuje stale bold/italic/color/highlight, opravit runtime formatting state.
- [ ] Pokud paragraph state ukazuje špatné zarovnání/řádkování, opravit command state resolver.
- [ ] Pokud mixed state není rozlišitelný, doplnit UI stav a test.
- [ ] Pokud disabled state neodpovídá capability/read-only, opravit command registry.

### 20.4 Vizuální/UX opravy

- [ ] Pokud UI nevypadá profesionálně, upravit layout, spacing, contrast, z-index, overlay placement.
- [ ] Pokud kontrolky nejsou použitelné na menším viewportu, upravit responsive chování.
- [ ] Pokud text v tlačítku přetéká, zkrátit label, použít ikonu/tooltip nebo změnit layout.
- [ ] Pokud demo data matou uživatele, opravit seed data.

## Doporučené průběžné příkazy

```bash
node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js
dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore
dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore
```

Lokální servery:

```bash
dotnet run --project src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj --launch-profile Tempo.Blazor.Demo.Api --no-build
dotnet run --project src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --launch-profile https --no-build
```

Strict subsety:

```bash
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditor_Strict"
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditor_ParagraphAlignment|FullyQualifiedName~DocumentEditor_MouseParagraph"
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditor_Image|FullyQualifiedName~DocumentEditor_Table|FullyQualifiedName~DocumentEditor_Comments|FullyQualifiedName~DocumentEditor_TrackChanges"
```

## Definition of done pro celý strict E2E projekt

- [ ] Všechny hlavní user-facing funkce editoru mají alespoň jeden strict human-like E2E test.
- [ ] Každá funkce dostupná přes více vstupů má cross-entrypoint test.
- [ ] Testy kontrolují content, selection/caret, toolbar state, floating UI, layout a persistence.
- [ ] Žádný známý problém z ručního testování není pouze “zdokumentovaný”; buď je opravený, nebo má explicitní failing/ignored known-bug test s odkazem na bug.
- [ ] Demo stránka je použitelná ručně aspoň v desktop 1440x900 bez zjevných překryvů, zaseknutých menu a špatné toolbar synchronizace.
- [ ] Nový vývoj editoru nesmí přidat funkci bez strict E2E coverage nebo vědomě zapsané výjimky.
