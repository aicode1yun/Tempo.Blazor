# Document editor: Word/Google Docs quality TDD TODO

Datum založení: 2026-05-14  
Aktuální implementační master TODO: `planning/tmdocumenteditor-complete-improvements-tdd-implementation-todo-2026-05-17.md`  
Zdroj problému: ruční test + video `/home/pavel/2026-05-14 13-51-26.mp4`  
Cíl: posunout `TmDocumentEditor` z prototypového WYSIWYG editoru na použitelný Word Online / Google Docs styl editoru.  
Proces: TDD po co nejmenších krocích, průběžné e2e testy po každé uživatelsky viditelné změně.

## Základní pravidla

- [ ] Každý implementační krok začíná RED unit/component/js testem.
- [ ] Každá fáze s viditelným UX má alespoň jeden e2e test.
- [ ] Po každé fázi odškrtnout hotové položky přímo v tomto souboru.
- [ ] Živou editační surface vlastní JavaScript; Blazor nesmí při psaní, remote patchi ani změně formátování přerenderovat editable subtree.
- [ ] DOM patch musí být minimální a selection-safe; full snapshot refresh je jen initial load, document switch, recovery a read-only preview.
- [ ] Revize, comments, images, headers/footers a formatting musí používat strukturované operace, ne text-only fallback.
- [ ] Všechny nové veřejné kontrakty patří do `Tempo.Blazor.Abstractions`.
- [ ] Všechny nové viditelné texty doplnit do `TmResources.resx`, `TmResources.cs.resx`, `TmResources.fr.resx` a `MockTmLocalizer`.
- [ ] Vizuální změny ověřovat screenshotem desktop + užší viewport.

## Audit z videa

- [ ] Formátování se aplikuje nepředvídatelně a toolbar stav neodpovídá skutečnému výběru.
- [ ] Přijímání revizí nechává dokument v divném stavu a revizní panel není jasně synchronní s obsahem.
- [ ] `Enter` nepokračuje tam, kde byl caret; text začne až na další řádce / jiném vizuálním místě.
- [ ] `Shift+Enter` se chová opačně: pokračuje na předchozí řádce nebo v nesprávném DOM text nodu.
- [ ] Obrázek se nezobrazuje jako skutečný dokumentový objekt; je vidět fallback/placeholder.
- [ ] Není ověřené drag & drop, resize a kontextové menu na obrázku.
- [ ] Chybí výběr font family přes provider boundary.
- [ ] Chybí výběr velikosti písma.
- [ ] Chybí zarovnání odstavce.
- [ ] Pravý panel je trvale zabraný komentáři a revizemi; nejde jej ergonomicky skrýt/obnovit.
- [ ] Po zavření verzí není zřejmá cesta, jak panel znovu vyvolat.
- [ ] Hlavička nejde editovat per stránka.
- [ ] Patička nejde editovat vůbec, natož po jednotlivých stránkách.
- [ ] Vzhled působí jako demo/prototyp, ne jako líbivý dokumentový editor.

## Doporučené průběžné příkazy

```bash
dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor|FullyQualifiedName~TmDocumentWysiwygHostTests" --logger "console;verbosity=minimal"
node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js
dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --logger "console;verbosity:minimal"
```

E2E po nastartování demo API + WASM:

```bash
dotnet run --project src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj
dotnet run --project src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --launch-profile https
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor" --logger "console;verbosity=minimal"
```

## Fáze 0: Reprodukce a safety net z videa

### 0.1 Video-regrese jako e2e scénáře

- [x] **RED E2E:** Scénář `DocumentEditor_Wysiwyg_EnterContinuesAtCaret` reprodukuje problém klasického Enteru.
- [x] **GREEN:** Test zatím padá na známé vadě, bez implementační opravy. (Ulozeno jako ignored known-bug e2e.)
- [x] **RED E2E:** Scénář `DocumentEditor_Wysiwyg_ShiftEnterCreatesSoftBreakAtCaret` reprodukuje problém Shift+Enter.
- [x] **GREEN:** Test zatím padá na známé vadě. (Ulozeno jako ignored known-bug e2e.)
- [x] **RED E2E:** Scénář `DocumentEditor_Wysiwyg_AcceptRevisionKeepsContentAndCaretStable` reprodukuje divné přijetí revize.
- [x] **GREEN:** Test zatím padá na známé vadě. (Ulozeno jako ignored known-bug e2e.)
- [x] **RED E2E:** Scénář `DocumentEditor_Wysiwyg_ImageAssetRendersAsImageObject` reprodukuje placeholder místo obrázku.
- [x] **GREEN:** Test zatím padá na známé vadě. (Ulozeno jako ignored known-bug e2e.)

### 0.2 Telemetrie pro debug editoru

- [x] **RED Unit:** `WysiwygDebugSnapshot` umí popsat selection, active block, active inline, pending transaction a DOM path.
- [x] **GREEN:** Přidat interní debug DTO bez veřejného UI.
- [x] **RED JS/static:** JS engine obsahuje `getDebugSnapshot(instanceId)`.
- [x] **GREEN:** Vrací collapsed/range selection, active element, last input type, last patch id.
- [x] **RED E2E:** Při selhání editor e2e uloží debug snapshot do test outputu.
- [x] **GREEN:** Rozšířit Playwright helper pro editor screenshot + debug JSON.

## Fáze 1: Selection/caret invarianty

### 1.1 Jednotný selection model

- [x] **RED Unit:** `WysiwygSelectionSnapshot` rozliší body/header/footer/image/table region.
- [x] **GREEN:** Přidat `Region`, `PageIndex`, `HeaderFooterId`, `TableCellPath`.
- [x] **RED JS:** `getSelectionSnapshot` vrátí stabilní selection i uvnitř nested spans s marks.
- [x] **GREEN:** Normalizovat text node, inline wrapper a block wrapper.
- [x] **RED JS:** Selection uvnitř revision marku se mapuje na skutečný text run, ne na wrapper.
- [x] **GREEN:** Ignorovat prezentační DOM vrstvy při mapování.

### 1.2 Restore selection bez skoku

- [x] **RED JS:** `restoreSelection` vrátí caret na stejný inline/offset po aktualizaci jednoho text runu.
- [x] **GREEN:** Implementovat offset mapping přes `data-inline-id` a text node walker.
- [x] **RED JS:** Restore po splitu inline najde nejbližší logický offset.
- [x] **GREEN:** Přidat fallback přes block id + character offset.
- [x] **RED E2E:** Po kliknutí do prostředku odstavce a napsání znaku zůstane caret za vloženým znakem.
- [x] **GREEN:** Nevolat snapshot refresh po běžném lokálním inputu.

### 1.3 Transaction guard

- [x] **RED Unit:** Lokální patch nese `TransactionId` a očekávanou selection před/po.
- [x] **GREEN:** Rozšířit patch pipeline o before/after selection.
- [x] **RED JS:** Během composition/input transakce se remote/shell patch queue pouze zařadí do fronty.
- [x] **GREEN:** Flush fronty až po stabilizaci selection.
- [x] **RED E2E:** Rychlé držení klávesy nepřeskakuje po dávkách do špatného místa.
- [x] **GREEN:** Batchovat model commit, ale DOM ponechat browser-owned.

## Fáze 2: Enter a Shift+Enter jako Word

### 2.1 Hard paragraph break

- [x] **RED JS:** `beforeinput insertParagraph` vytvoří patch `SplitBlock` s přesným block/inline/offset.
- [x] **GREEN:** Přidat patch typ `SplitBlock` nebo rozšířit existující `InsertBlock` o split metadata.
- [x] **RED Unit:** Split uprostřed odstavce rozdělí text před/po caret do dvou odstavců.
- [x] **GREEN:** Implementovat split v `WysiwygPatchApplier`.
- [x] **RED Unit:** Split zachová marks před/po splitu podle Word-like chování.
- [x] **GREEN:** Marks pokračují do nového odstavce pro typing style, ale revision rozsahy se nepřelijí nesprávně.
- [x] **RED E2E:** `Enter` uprostřed věty vytvoří nový odstavec a další psaní pokračuje na začátku nového odstavce.
- [x] **GREEN:** JS DOM split + C# model commit + restore selection.

### 2.2 Soft break

- [x] **RED JS:** `beforeinput insertLineBreak` / `Shift+Enter` vytvoří patch `InsertSoftBreak`.
- [x] **GREEN:** Přidat inline typ nebo payload pro hard break v odstavci.
- [x] **RED Unit:** Soft break zachová jeden block a vloží break inline na caret.
- [x] **GREEN:** `WysiwygPatchApplier` podporuje soft break.
- [x] **RED JS:** Render soft breaku používá `<br data-inline-break>` bez rozbití mappingu.
- [x] **GREEN:** Serializer/deserializer podporuje soft break.
- [x] **RED E2E:** `Shift+Enter` vytvoří nový vizuální řádek ve stejném odstavci a další psaní pokračuje na tom řádku.
- [x] **GREEN:** Opravit DOM insertion/selection restore.

### 2.3 Backspace/Delete kolem breaků

- [x] **RED Unit:** Backspace na začátku odstavce spojí s předchozím odstavcem a zachová marks.
- [x] **GREEN:** Přidat command `MergeWithPreviousBlock`.
- [x] **RED Unit:** Delete před soft breakem odstraní soft break, ne celý text.
- [x] **GREEN:** Implementovat delete soft breaku.
- [x] **RED E2E:** Enter, Shift+Enter, Backspace a Delete v kombinaci drží caret na očekávaném místě.
- [x] **GREEN:** Stabilizovat key/input pipeline.

## Fáze 3: Inline formátování, které respektuje selection

### 3.1 Toolbar command contract

- [x] **RED Unit:** Ribbon command `Bold` vyžaduje aktuální selection snapshot, ne aktivní block fallback.
- [x] **GREEN:** Command bridge vždy tahá selection z JS hostu těsně před aplikací.
- [x] **RED Unit:** Pokud není range selection, command nastaví typing style pro budoucí text.
- [x] **GREEN:** Přidat `PendingTypingMarks`.
- [x] **RED JS:** `getFormattingState` vrací mixed/active/inactive pro bold/italic/underline.
- [x] **GREEN:** Toolbar umí tri-state active state.

### 3.2 Přesné mark split/merge

- [x] **RED Unit:** Bold na části text runu rozdělí run na před/výběr/po.
- [x] **GREEN:** Implementovat range mark splitter.
- [x] **RED Unit:** Opakovaný Bold na bold rozsahu mark odebere.
- [x] **GREEN:** Implementovat toggle remove.
- [x] **RED Unit:** Italic + underline kombinace se nemazají navzájem.
- [x] **GREEN:** Merge sousedních text runů pouze při shodných marks.
- [x] **RED E2E:** Vybrané slovo lze ztučnit, kurzívou i podtrhnout bez změny okolního textu.
- [x] **GREEN:** Aktualizovat JS render marks bez full snapshotu.

### 3.3 Formatting state v ribbonu

- [x] **RED Component:** Ribbon ukazuje aktivní `Bold` při caret uvnitř bold textu.
- [x] **GREEN:** Napojit selection changed event na ribbon state.
- [x] **RED Component:** Mixed selection nezobrazuje falešně aktivní formát.
- [x] **GREEN:** Přidat mixed state styling.
- [x] **RED E2E:** Kliknutí do bold textu aktivuje tlačítko Tučné; kliknutí mimo jej vypne.
- [x] **GREEN:** Debounce selection state bez zpoždění při psaní.

## Fáze 4: Track changes/revize jako ve Wordu

### 4.1 Revizní model pro insert/delete/format

- [x] **RED Unit:** Při zapnutém track changes `InsertText` vytvoří pending insertion revision a zelený inline mark.
- [x] **GREEN:** Stabilizovat existující insertion path.
- [x] **RED Unit:** `DeleteRange` při track changes text nemaže fyzicky, ale označí jej deletion revision.
- [x] **GREEN:** Přidat deletion mark a skrytí/přeškrtnutí podle review mode.
- [x] **RED Unit:** Formatting change vytvoří formatting revision s původním a novým mark stavem.
- [x] **GREEN:** Přidat structured formatting revision payload.
- [x] **RED Unit:** Enter/SplitBlock při track changes nezahodí existující pending revisions.
- [x] **GREEN:** Split/merge revizí při strukturálních změnách.

### 4.2 Přijetí a odmítnutí revize

- [x] **RED Unit:** Accept insertion odstraní revision mark a ponechá vložený text jako normální obsah.
- [x] **GREEN:** Implementovat idempotentní accept insertion.
- [x] **RED Unit:** Reject insertion odstraní vložený text a revizi.
- [x] **GREEN:** Implementovat reject insertion.
- [x] **RED Unit:** Accept deletion fyzicky odstraní přeškrtnutý text.
- [x] **GREEN:** Implementovat accept deletion.
- [x] **RED Unit:** Reject deletion ponechá text a odstraní deletion mark.
- [x] **GREEN:** Implementovat reject deletion.
- [x] **RED Unit:** Accept/Reject formatting aplikuje nebo vrátí formatting payload.
- [x] **GREEN:** Implementovat formatting decision.
- [x] **RED E2E:** Uživatel zapne sledování změn, píše, maže, přijme první revizi a obsah/panel zůstane konzistentní.
- [x] **GREEN:** Panel i DOM se patchují bez resetu caret.

### 4.3 Reviewing modes

- [x] **RED Unit:** Review mode `AllMarkup` zobrazuje insertions zeleně a deletions červeně přeškrtnutě.
- [x] **GREEN:** Přidat `DocumentReviewDisplayMode`.
- [x] **RED Unit:** `SimpleMarkup` zobrazí text normálně s margin indikátorem revize.
- [x] **GREEN:** Přidat margin markers.
- [x] **RED Unit:** `NoMarkup` skryje markup bez ztráty revizí.
- [x] **GREEN:** Render mode je view state, ne model mutation.
- [x] **RED Component:** Review ribbon obsahuje Show Markup dropdown.
- [x] **GREEN:** Přepínání All/Simple/No markup.
- [x] **RED E2E:** Přepnutí No Markup nezničí pending revize a návrat na All Markup je znovu ukáže.
- [x] **GREEN:** Persistovat view state jen lokálně.

### 4.4 Revizní panel

- [x] **RED Component:** Revizní panel ukazuje stejný počet pending revizí jako dokument.
- [x] **GREEN:** Panel bere data ze sjednoceného selectoru.
- [x] **RED Component:** Klik na revizi scrolluje a zvýrazní odpovídající text/image/block.
- [x] **GREEN:** JS API `scrollToRevision(revisionId)`.
- [x] **RED Component:** Accept/Reject tlačítka mají disabled/loading stav a neblikají.
- [x] **GREEN:** Idempotentní command + optimistic UI.
- [x] **RED E2E:** Accept/Reject z panelu i inline kontextu funguje stejně.
- [x] **GREEN:** Sjednotit command handler.

## Fáze 5: Obrázky jako skutečné dokumentové objekty

### 5.1 Provider-resolved image rendering

- [x] **RED Unit:** Image block s `AssetId` požádá `IDocumentImageProvider.ResolveAsync`.
- [x] **GREEN:** Host připraví display URL před snapshotem nebo přes JS callback.
- [x] **RED JS:** Render image blocku používá `<figure>` + `<img>` a ne textový placeholder.
- [x] **GREEN:** Přidat loading/error states s retry.
- [x] **RED E2E:** Demo provider image se zobrazí jako skutečný obrázek s nenulovou naturalWidth.
- [x] **GREEN:** Opravit resolver/render pipeline.

### 5.2 Image selection, resize a layout

- [x] **RED JS:** Klik na image nastaví selected image state bez přesunu caret do textu.
- [x] **GREEN:** Selection manager podporuje object selection.
- [x] **RED JS:** Resize handle změní width/height a vytvoří `UpdateBlock` patch.
- [x] **GREEN:** Implementovat resize transaction s preview.
- [x] **RED E2E:** Obrázek lze vybrat, resize provede změnu velikosti a reload ji zachová.
- [x] **GREEN:** Persistovat image size/layout.

### 5.3 Image context menu

- [x] **RED Component:** Pravé tlačítko na obrázku otevře image context menu.
- [x] **GREEN:** Přidat JS contextmenu bridge do Blazor menu.
- [x] **RED Component:** Menu obsahuje Replace image, Alt text, Caption, Wrap text, Position, Delete.
- [x] **GREEN:** Implementovat commandy a lokalizace.
- [x] **RED E2E:** Pravé tlačítko na obrázku otevře menu a Delete odstraní image block.
- [x] **GREEN:** Object delete bez rozbití selection.

### 5.4 Word-like image drag & reposition

- [x] **RED JS:** Drag existujícího obrázku začne image-drag transaction, ne text selection.
- [x] **GREEN:** Implementovat pointer capture pro object drag bez caret jump.
- [x] **RED JS:** Floating image při drag sleduje pointer v page coordinates.
- [x] **GREEN:** Převádět pointer pozici na `ImageBlockContent.Layout.X/Y/PageIndex`.
- [x] **RED JS:** Drop floating image vytvoří structured `UpdateImageLayout`/`UpdateBlock` patch.
- [x] **GREEN:** Persistovat floating position a wrap mode v dokumentovém modelu.
- [x] **RED JS:** Inline image drag ukáže insertion caret mezi bloky/inline pozicemi.
- [x] **GREEN:** Drop inline image přesune image objekt na cílovou pozici.
- [x] **RED Unit:** Move image command podporuje undo/redo bez ztráty image metadat.
- [x] **GREEN:** Sjednotit image move přes command stack a collaboration operation mapper.
- [x] **RED E2E:** Obrázek lze přetáhnout na jiné místo stránky a po save/reload tam zůstane.
- [x] **GREEN:** Stabilizovat DOM patch bez full Blazor rerenderu.
- [x] **RED E2E:** Změna wrap mode + drag nezpůsobí caret jump ani rozpad text flow.
- [x] **GREEN:** Text flow reaguje podle `Wrap text` módu.

### 5.5 Drag & drop upload

- [x] **RED JS:** Drop image file na dokument zavolá `HandleImageUploadRequested`.
- [x] **GREEN:** Přidat drop zone nad page surface s vizuálním targetem.
- [x] **RED Unit:** Upload používá `IDocumentImageProvider` a vytvoří asset-backed image block.
- [x] **GREEN:** Napojit provider.
- [x] **RED E2E:** Drag/drop nebo file chooser vloží image a po save/reload zůstane viditelný.
- [x] **GREEN:** Commit assets při save.

## Fáze 6: Font provider, font size a text styling

### 6.1 Font provider v abstractions

- [x] **RED Unit:** `IDocumentFontProvider` vrací dostupné font families a fallback font.
- [x] **GREEN:** Přidat `IDocumentFontProvider`, `DocumentFontFamily`, `DocumentFontQuery`.
- [x] **RED Unit:** In-memory provider vrací bezpečný default set.
- [x] **GREEN:** Implementovat `InMemoryDocumentFontProvider`.
- [x] **RED Component:** `TmDocumentEditor` přijme `FontProvider` parametr.
- [x] **GREEN:** Přidat parametr a fallback provider.
- [x] **RED Localization:** Texty pro font dropdown jsou lokalizované.
- [x] **GREEN:** Doplnit resources.

### 6.2 Font family command

- [x] **RED Unit:** Apply font family na selection vytvoří `InlineMarkType.FontFamily` nebo ekvivalentní structured mark.
- [x] **GREEN:** Rozšířit inline mark model.
- [x] **RED Unit:** Font family na collapsed selection nastaví pending typing style.
- [x] **GREEN:** Pending style se aplikuje na další typed text.
- [x] **RED JS:** Render font family mark nastaví CSS `font-family` jen na rozsah.
- [x] **GREEN:** Sanitizovat font name přes provider whitelist.
- [x] **RED E2E:** Vybrané slovo lze přepnout na jiný font a po reloadu zůstane.
- [x] **GREEN:** Snapshot/patch/persistence roundtrip.

### 6.3 Font size command

- [x] **RED Unit:** Apply font size na selection vytvoří structured mark se size v pt.
- [x] **GREEN:** Přidat `FontSize` mark.
- [x] **RED Unit:** Nevalidní size mimo povolený rozsah se odmítne.
- [x] **GREEN:** Validace 6-96 pt nebo provider options.
- [x] **RED Component:** Ribbon size dropdown obsahuje běžné velikosti a editable input.
- [x] **GREEN:** Implementovat size selector.
- [x] **RED E2E:** Změna velikosti písma ovlivní jen vybraný text a po reloadu zůstane.
- [x] **GREEN:** Render/persist.

### 6.4 Barvy, highlight a clear formatting

- [x] **RED Unit:** Font color mark se aplikuje na selection.
- [x] **GREEN:** Přidat color mark s token-safe nebo hex-safe validací.
- [x] **RED Unit:** Highlight mark se aplikuje na selection.
- [x] **GREEN:** Přidat highlight mark.
- [x] **RED Unit:** Clear formatting odstraní inline style marks, ale ne link/token/revision bez explicitního rozhodnutí.
- [x] **GREEN:** Implementovat clear formatting rules.
- [x] **RED E2E:** Color/highlight/clear formatting fungují bez resetu caret.
- [x] **GREEN:** Toolbar + JS render.

## Fáze 7: Zarovnání a odstavcové vlastnosti

### 7.1 Paragraph properties model

- [x] **RED Unit:** Paragraph block nese alignment `Left|Center|Right|Justify`.
- [x] **GREEN:** Přidat/rozšířit paragraph properties v modelu.
- [x] **RED Unit:** Alignment roundtrip přes JSON neztratí hodnotu.
- [x] **GREEN:** Aktualizovat serializaci.
- [x] **RED JS:** Render paragraph nastaví `text-align`.
- [x] **GREEN:** CSS/DOM mapping.

### 7.2 Alignment command

- [x] **RED Unit:** Align Center na aktuálním odstavci změní jen aktivní block.
- [x] **GREEN:** Implementovat command.
- [x] **RED Unit:** Range selection přes více odstavců nastaví alignment všem zasaženým odstavcům.
- [x] **GREEN:** Multi-block command.
- [x] **RED Component:** Ribbon má align left/center/right/justify segmented control.
- [x] **GREEN:** Stav tlačítek odpovídá aktivnímu odstavci nebo mixed state.
- [x] **RED E2E:** Uživatel vycentruje odstavec, reload zachová zarovnání.
- [x] **GREEN:** Persistovat.

### 7.3 Line spacing a paragraph spacing

- [x] **RED Unit:** Paragraph nese line spacing a spacing before/after.
- [x] **GREEN:** Rozšířit paragraph properties.
- [x] **RED Component:** Ribbon line spacing menu nastaví 1.0, 1.15, 1.5, 2.0.
- [x] **GREEN:** Command + render.
- [x] **RED E2E:** Line spacing změna je viditelná a nezmění text.
- [x] **GREEN:** CSS mapping.

### 7.4 Indent/outdent

- [x] **RED Unit:** Paragraph indent command nastaví left/right/first-line indent.
- [x] **GREEN:** Model + renderer.
- [x] **RED Component:** Increase/Decrease indent funguje na více odstavců.
- [x] **GREEN:** Multi-block command.
- [x] **RED E2E:** Indent/outdent posune odstavec a zachová caret.
- [x] **GREEN:** Selection-safe patch.

## Fáze 8: Ribbon 2.0 a command UX

### 8.1 Skutečné taby

- [x] **RED Component:** Klik na Home/Insert/Layout/References/Review/View změní viditelné command groups.
- [x] **GREEN:** Ribbon tab state + group rendering.
- [x] **RED Component:** Aktivní tab má jasný visual state a ARIA selected.
- [x] **GREEN:** Přidat keyboard navigation pro taby.
- [x] **RED E2E:** Uživatel přepne na Review a vidí review příkazy, Home příkazy zmizí.
- [x] **GREEN:** Stabilizovat ribbon layout.

### 8.2 Command availability

- [x] **RED Unit:** Read-only režim zakáže editační commandy, ale nechá view/review navigation.
- [x] **GREEN:** Command registry s `CanExecute`.
- [x] **RED Component:** Disabled tlačítka mají tooltip s důvodem.
- [x] **GREEN:** Přidat command tooltip provider.
- [x] **RED E2E:** Read-only editor nedovolí změnu obsahu ani přes keyboard shortcuts.
- [x] **GREEN:** JS input guard.

### 8.3 Keyboard shortcuts

- [x] **RED JS:** Ctrl+B/I/U volá stejné commandy jako ribbon.
- [x] **GREEN:** Keyboard shortcut dispatcher.
- [x] **RED JS:** Ctrl+Z/Y používá editor command history, ne browser undo mimo model.
- [x] **GREEN:** Command history bridge.
- [x] **RED E2E:** Ctrl+B na výběru funguje stejně jako tlačítko.
- [x] **GREEN:** Sjednocení command pipeline.

## Fáze 9: Pravý panel, komentáře, revize, verze

### 9.1 Unified side panel shell

- [x] **RED Component:** Editor má jeden side panel shell s taby `Komentáře`, `Revize`, `Verze`, `Vlastnosti`.
- [x] **GREEN:** Vytvořit `TmDocumentSidePanel`.
- [x] **RED Component:** Panel lze zavřít a znovu otevřít z ribbonu i z pravého edge tlačítka.
- [x] **GREEN:** Přidat view state + toggle button.
- [x] **RED Component:** Zavřený panel uvolní místo dokumentu.
- [x] **GREEN:** Responsive layout.
- [x] **RED E2E:** Zavřít panel, otevřít Revize přes Review ribbon, otevřít Verze přes View ribbon.
- [x] **GREEN:** Commandy pro panel.

### 9.2 Comments UX

- [x] **RED Component:** Komentáře nejsou trvale otevřené, pokud nejsou vybrané.
- [x] **GREEN:** Aktivní panel tab.
- [x] **RED Component:** Komentářová karta má compact/professional layout s autorem, časem, stavem.
- [x] **GREEN:** UI polish bez card-in-card.
- [x] **RED E2E:** Add comment otevře panel komentářů a zvýrazní anchor.
- [x] **GREEN:** Anchor scroll/focus.

### 9.3 Versions UX

- [x] **RED Component:** Zavření verzí neodstraní možnost je znovu vyvolat.
- [x] **GREEN:** View tab obsahuje `Versions` toggle.
- [x] **RED Component:** Versions panel jde otevřít z top/ribbon commandu i keyboard commandu.
- [x] **GREEN:** Command registry entry.
- [x] **RED E2E:** Uživatel otevře verze, zavře panel, znovu otevře verze a vybere diff.
- [x] **GREEN:** Persistovat jen lokální UI state.

## Fáze 10: Hlavičky a patičky jako editovatelné regiony

### 10.1 Region model a selection

- [x] **RED Unit:** Selection snapshot rozliší body/header/footer region.
- [x] **GREEN:** Region metadata v selection a patchi.
- [x] **RED Unit:** Patch z headeru upraví `DocumentHeaderFooter.Blocks`, ne body blocks.
- [x] **GREEN:** Routing patch applier podle regionu.
- [x] **RED JS:** Klik do headeru aktivuje header edit mode.
- [x] **GREEN:** Header region contenteditable se samostatným placeholderem.
- [x] **RED JS:** Klik do footeru aktivuje footer edit mode.
- [x] **GREEN:** Footer region contenteditable.

### 10.2 Per-page/per-section scopes

- [x] **RED Unit:** Document podporuje Primary/FirstPage/EvenPage header/footer pro sekci.
- [x] **GREEN:** Ověřit a doplnit chybějící model helpers.
- [x] **RED Component:** Layout ribbon umí přepnout Different First Page a Different Odd/Even.
- [x] **GREEN:** Section/header-footer commandy.
- [x] **RED Unit:** Editace first page headeru nezmění primary header.
- [x] **GREEN:** Scope resolver.
- [x] **RED E2E:** Uživatel edituje first page header a primary footer; obsah zůstane po reloadu.
- [x] **GREEN:** Persistovat.

### 10.3 Header/footer UX

- [x] **RED Component:** Double click v horním okraji stránky otevře header edit mode.
- [x] **GREEN:** JS double-click bridge.
- [x] **RED Component:** Header/footer mode zobrazí contextual ribbon tab.
- [x] **GREEN:** Contextual tab `Hlavička a patička`.
- [x] **RED Component:** Close Header and Footer vrátí focus do body na předchozí caret.
- [x] **GREEN:** Selection restore mezi regiony.
- [x] **RED E2E:** Double click header, napsat text, close, psát v body bez skoku.
- [x] **GREEN:** Region focus stability.

## Fáze 11: Page layout a polished document canvas

### 11.1 Page look

- [x] **RED Screenshot/E2E:** Editor stránka má A4 poměr, page shadow a neutrální pracovní plochu.
- [x] **GREEN:** Upravit CSS page surface, spacing, shadow, background.
- [x] **RED E2E:** Text ani panely se nepřekrývají při 1280×720.
- [x] **GREEN:** Responsive constraints.
- [x] **RED E2E:** Narrow viewport nezpůsobí horizontální overflow mimo dokumentový canvas.
- [x] **GREEN:** Mobile/tablet layout.

### 11.2 Typography defaults

- [x] **RED Unit:** Nový dokument má default body font, size, line height a paragraph spacing.
- [x] **GREEN:** `DocumentEditorTheme` nebo document defaults.
- [x] **RED Screenshot:** Body text, headings, revision text a comments mají konzistentní typografii.
- [x] **GREEN:** CSS polish.
- [x] **RED E2E:** Dlouhý text nepřetéká mimo stránku.
- [x] **GREEN:** Word wrapping a page width constraints.

### 11.3 Status bar

- [x] **RED Component:** Status bar ukazuje saved state, word count, page count, active region a zoom.
- [x] **GREEN:** Přidat `TmDocumentEditorStatusBar`.
- [x] **RED Component:** Autosave/last saved se přesune ze ribbonu do status baru.
- [x] **GREEN:** Odebrat status noise z ribbonu.
- [x] **RED E2E:** Po psaní se status změní na unsaved/saved bez layout shiftu.
- [x] **GREEN:** Stabilní status layout.

### 11.4 Ruler a zoom

- [x] **RED Component:** View tab obsahuje Ruler toggle a Zoom controls.
- [x] **GREEN:** Přidat view state.
- [x] **RED E2E:** Zoom 100/page width mění měřítko stránky bez ztráty caret.
- [x] **GREEN:** CSS transform/scale + selection mapping.
- [x] **RED Screenshot:** Ruler odpovídá page margins.
- [x] **GREEN:** Jednoduchý ruler render.

## Fáze 12: Kontextové nabídky a inline mini toolbar

### 12.1 Text context menu

- [x] **RED Component:** Pravé tlačítko na textu otevře context menu s Cut/Copy/Paste, Font, Paragraph, Comment.
- [x] **GREEN:** JS contextmenu bridge + Blazor menu.
- [x] **RED JS:** Menu pozice respektuje viewport a scroll.
- [x] **GREEN:** Floating positioning helper.
- [x] **RED E2E:** Pravé tlačítko na vybraném textu otevře menu a Bold/Comment command funguje.
- [x] **GREEN:** Command reuse.

### 12.2 Mini toolbar

- [x] **RED JS:** Po výběru textu se zobrazí mini toolbar u selection rectu.
- [x] **GREEN:** Floating toolbar with bold/italic/comment/link.
- [x] **RED JS:** Mini toolbar nezakrývá selection a mizí při psaní.
- [x] **GREEN:** Positioning + lifecycle.
- [x] **RED E2E:** Vybrat text, kliknout mini toolbar Bold, výběr zůstane použitelný.
- [x] **GREEN:** Selection preservation.

## Fáze 13: Linky, tokeny a existující dokumentové objekty

### 13.1 Links

- [x] **RED Unit:** Link mark má href/title a validaci bezpečné URL.
- [x] **GREEN:** Link mark model.
- [x] **RED Component:** Insert/Edit link dialog.
- [x] **GREEN:** Dialog + command.
- [x] **RED E2E:** Vybraný text lze změnit na odkaz a otevření dialogu jej umí editovat.
- [x] **GREEN:** Render + persistence.

### 13.2 Tokens

- [x] **RED JS:** Token run je atomic inline node a caret jej nepřepíše po znacích.
- [x] **GREEN:** Token renderer + selection mapping kolem atomic node.
- [x] **RED Component:** Token menu jde otevřít z Insert ribbonu.
- [x] **GREEN:** Zachovat token provider a `TmDocumentTokenMenu`.
- [x] **RED E2E:** Vložený token přežije psaní okolo, formatting a reload.
- [x] **GREEN:** Structured token patches.

## Fáze 14: Tables minimum viable Word-like behavior

### 14.1 Table selection/caret

- [x] **RED JS:** Klik do buňky mapuje selection na table cell path.
- [x] **GREEN:** Table cell selection mapping.
- [x] **RED Unit:** Insert text v buňce upraví jen cell content.
- [x] **GREEN:** Patch applier podle cell path.
- [x] **RED E2E:** Psaní v tabulce nemění okolní odstavce a caret zůstává v buňce.
- [x] **GREEN:** Table input pipeline.

### 14.2 Table context actions

- [x] **RED Component:** Table context menu obsahuje insert/delete row/column.
- [x] **GREEN:** Menu + commands.
- [x] **RED Unit:** Insert row/column zachová ostatní buňky.
- [x] **GREEN:** Structured table update.
- [x] **RED E2E:** Uživatel přidá řádek a uloží/reloadne.
- [x] **GREEN:** Persistence.

## Fáze 15: Collaboration a remote ops bez rozbití caret

### 15.1 Remote patch queue

- [x] **RED Unit:** Remote ops během lokální input transakce se zařadí do fronty.
- [x] **GREEN:** Collaboration refresh používá editor transaction state.
- [x] **RED JS:** Remote `UpdateBlock` pro jiný block nehne lokálním caret.
- [x] **GREEN:** Patch only affected DOM node.
- [x] **RED E2E:** Dva kontexty, remote update mimo aktivní odstavec, lokální caret zůstane.
- [x] **GREEN:** Selection-safe remote patch.

### 15.2 Konflikty ve stejném odstavci

- [x] **RED Unit:** Lokální pending insert a remote insert ve stejném inline se deterministicky seřadí.
- [x] **GREEN:** Offset transform podle operation metadata.
- [x] **RED E2E:** Dva uživatelé píší do stejného odstavce bez resetu na začátek.
- [x] **GREEN:** Basic OT transform.

## Fáze 16: Accessibility a keyboard completeness

### 16.1 ARIA regions

- [x] **RED Component:** Editor má ARIA labels pro ribbon, document surface, side panel, status bar.
- [x] **GREEN:** Doplnit labels.
- [x] **RED E2E/axe smoke:** Základní stránka nemá kritické accessibility chyby.
- [x] **GREEN:** Opravit role/labels.

### 16.2 Keyboard navigation

- [x] **RED E2E:** Tab/Shift+Tab se pohybuje mezi ribbonem, dokumentem a panelem bez pasti.
- [x] **GREEN:** Focus management.
- [x] **RED E2E:** Escape zavře menu/dialog/panel a vrátí focus do dokumentu.
- [x] **GREEN:** Escape stack.
- [x] **RED E2E:** Alt nebo F10 aktivuje ribbon keyboard mode.
- [x] **GREEN:** Ribbon key tips základ.

## Fáze 17: Save/load/export consistency

### 17.1 Roundtrip coverage

- [x] **RED Unit:** Dokument s fonty, size, alignment, revizemi, obrázkem, header/footer projde JSON roundtripem.
- [x] **GREEN:** Serializace všech nových vlastností.
- [x] **RED Unit:** Save request obsahuje structured document bez DOM-only dat.
- [x] **GREEN:** Serializer boundaries.
- [x] **RED E2E:** Vytvořit dokument se všemi novými prvky, uložit, reloadnout, vizuálně ověřit.
- [x] **GREEN:** End-to-end persistence.

### 17.2 Export readiness

- [x] **RED Unit:** DOCX/PDF export request dostane font/paragraph/header/footer/image metadata.
- [x] **GREEN:** Rozšířit export DTO mapping.
- [x] **RED E2E:** Export buttons zůstávají enabled/disabled podle provider availability.
- [x] **GREEN:** Command availability.

## Fáze 18: Visual design polish

### 18.1 Líbivý a profesionální vzhled

- [x] **RED Screenshot:** Ribbon, canvas, side panel a status bar projdou vizuálním baseline snapshotem.
- [x] **GREEN:** Přestylovat toolbar na skutečný ribbon s menšími ikonami, skupinami a čistou typografií.
- [x] **RED Screenshot:** Revize a komentáře mají kultivovaný pravý panel bez přeplněných card-in-card prvků.
- [x] **GREEN:** Side panel polish.
- [x] **RED Screenshot:** Page canvas vypadá jako moderní document editor, ne formulář.
- [x] **GREEN:** Page, shadow, margins, typography, empty states.
- [x] **RED E2E:** Screenshot desktop 1440×900 a 1280×720 bez překryvů textů/tlačítek.
- [x] **GREEN:** Responsive polish.

### 18.2 Demo obsah

- [x] **RED Component:** Demo dokument obsahuje validní provider image, revizi, komentář, header/footer a reprezentativní formatting.
- [x] **GREEN:** Aktualizovat demo seed.
- [x] **RED E2E:** Demo stránka po načtení nepůsobí rozbitě: image visible, panels toggleable, no placeholder errors.
- [x] **GREEN:** Demo quality gate.

## Fáze 19: Hardening a regresní sada

### 19.1 Editor quality smoke suite

- [x] **RED E2E:** `DocumentEditorQualitySmokeTests` zahrnuje typing, Enter, Shift+Enter, formatting, revision accept/reject, image, panel toggle, header/footer.
- [x] **GREEN:** Zelená smoke sada.
- [x] **RED Unit:** Všechny nové commandy mají idempotentní aplikaci a undo metadata.
- [x] **GREEN:** Command consistency tests.
- [x] **RED JS:** `document-editor-wysiwyg.js` má static tests pro selection mapping a commands.
- [x] **GREEN:** Doplnit JS test runner nebo minimální node-based assertions.

### 19.2 Performance guard

- [x] **RED E2E/metric:** Držení klávesy 2 sekundy nezpůsobí průměrné zpoždění inputu nad stanovený limit.
- [x] **GREEN:** Input batching, no Blazor rerender per keystroke.
- [x] **RED E2E/metric:** Remote patch mimo aktivní block nevyvolá full snapshot.
- [x] **GREEN:** Instrumentace a assertion přes JS debug counters.
- [x] **RED E2E:** Dokument s obrázkem, panelem a revizemi zůstane interaktivní po 200 rychlých znacích.
- [x] **GREEN:** Debounce, transaction queue, minimal DOM patches.

## Definition of Done pro celý plán

- [ ] Enter a Shift+Enter fungují jako ve Wordu a mají e2e regresi.
- [ ] Inline formatting funguje přes selection, collapsed typing style a toolbar state.
- [ ] Track changes podporuje insertion, deletion, formatting, accept/reject a review modes bez ztráty caret.
- [ ] Obrázky se zobrazují, dají se vybrat, resize/drag/dropnout a ovládat přes context menu.
- [ ] Font family je přes provider boundary v abstractions.
- [ ] Font size, color, highlight a clear formatting fungují per selection.
- [ ] Alignment, spacing a indent fungují per paragraph/multi-paragraph.
- [ ] Pravý panel je ovladatelný a obnovitelný z ribbonu.
- [x] Header/footer jsou editovatelné podle regionu a scope.
- [ ] Vizuální vzhled odpovídá modernímu online document editoru.
- [ ] Relevantní unit/component/js/e2e testy jsou zelené.
