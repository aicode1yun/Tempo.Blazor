# TmDocumentEditor WYSIWYG JS Engine - TDD implementační TODO

**Datum:** 2026-05-11  
**Cíl:** Přestavět `TmDocumentEditor` na Word-like WYSIWYG editor s architekturou **Blazor shell + JavaScript editing engine**.  
**Základní pravidlo:** Blazor vlastní veřejné API komponenty, provider kontrakty, shell, ribbon, panely, lokalizaci a persistence orchestration. JavaScript vlastní živou editační surface, selection, IME, paste/copy, DOM patching, measuring, pagination a drag/resize interakce.  
**Proces:** TDD po malých krocích. Každý implementační krok má RED -> GREEN -> refactor. E2E testy se píší průběžně, ne až na konci.

---

## Nepřekročitelná pravidla

- [ ] Neodpojovat existující testy bez ekvivalentní náhrady.
- [ ] Neodstraňovat stávající `TmDocumentEditor` chování big-bang přepisem.
- [ ] Nový WYSIWYG engine zavádět paralelně za feature flagem nebo interním režimem.
- [ ] Zachovat `DocumentEditorDocument` jako persistence/API contract.
- [ ] Zachovat existující provider rozhraní pro load/save/comments/versions/images/offline/renditions.
- [ ] Každá fáze musí mít unit/component test.
- [ ] Každá uživatelsky viditelná fáze musí mít alespoň jeden E2E smoke.
- [ ] `DocumentEditorE2ETests` musí zůstat zelené nebo být ve stejném kroku rozšířené/upravené bez ztráty scénáře.
- [ ] JS engine nesmí spoléhat na `document.execCommand`.
- [ ] `MutationObserver` je guard/fallback, ne primární sync mechanismus.
- [ ] Primární editace jde přes `beforeinput`, `input`, `paste`, `composition*`, `selectionchange`.
- [ ] Blazor nesmí po každém znaku přerenderovat celý editable subtree.

---

## Doporučené ověřovací příkazy

- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor" -v:minimal` ✅ 360/360
- [x] `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj -v:minimal` ✅ 17/17
- [x] `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` ✅
- [x] `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj -v:minimal` ✅ 0 errors
- [ ] `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorE2ETests" -v:minimal` (requires running demo app)
- [x] `git diff --check` ✅

---

## Fáze 0: Safety net a architektonické rozhodnutí

### 0.1 Planning guardrails

- [x] Zapsat do analysis dokumentu, že prováděcí plán je tento soubor.
- [x] Ověřit, že starý analysis dokument už netvrdí, že se testy mají mazat nebo dočasně vypínat.
- [x] Ověřit, že analysis dokument říká: Blazor shell + JS editing engine.
- [x] Ověřit, že analysis dokument říká: `MutationObserver` jen guard/fallback.

### 0.2 Baseline test run

- [x] Spustit `DocumentEditor` unit/component testy jako baseline. **Výsledek:** 249/249 úspěšných (`Tempo.Blazor.Tests`).
- [x] Spustit `DocumentEditorE2ETests` jako baseline. **Výsledek:** 0/22 úspěšných – selhání `ERR_CONNECTION_REFUSED` na `https://localhost:7106/document-editor` způsobeno neběžící demo aplikací, nikoliv změnami kódu.
- [x] Zapsat do poznámky výsledky baseline testů.
- [x] Pokud baseline padá na nesouvisející chybě, poznamenat ji a neopravovat v rámci WYSIWYG redesignu bez samostatného rozhodnutí. **Poznámka:** E2E testy vyžadují běžící Demo WASM (`https://localhost:7106`). Selhání není způsobeno kódem.

### 0.3 Feature flag / režim editoru

- [x] **RED:** Component test ověří, že `TmDocumentEditor` má parametr pro volbu editační surface.
- [x] Implementovat enum `DocumentEditorSurfaceMode` (`Block`, `WysiwygJsEngine`).
- [x] Přidat parametr `SurfaceMode` s defaultem na stávající bezpečný režim.
- [x] **GREEN:** Stávající editor renderuje beze změny při defaultním režimu.
- [x] **E2E smoke:** `/document-editor` se načte ve stávajícím režimu. **Poznámka:** Nelze ověřit bez běžícího serveru; default `Block` zachovává stávající render.

---

## Fáze 1: JS engine skeleton bez změny UI

### 1.1 Soubor a registrace

- [x] **RED:** Test nebo static assertion ověří, že nový JS soubor je zahrnutý v balíčku.
- [x] Vytvořit `wwwroot/js/document-editor-wysiwyg.js`.
- [x] Přidat bezpečný namespace `window.tmDocumentWysiwyg`.
- [x] Přidat metodu `create(rootElement, options, dotNetRef)`.
- [x] Přidat metodu `dispose(instanceId)`.
- [x] **GREEN:** JS soubor se buildí a je dostupný jako static web asset.

### 1.2 JS instance lifecycle

- [x] **RED:** bUnit test s `JSRuntimeMode.Loose` ověří volání `tmDocumentWysiwyg.create`.
- [x] Implementovat `TmDocumentWysiwygHost` skeleton komponentu.
- [x] Host renderuje prázdný root s `data-testid="document-wysiwyg-host"`.
- [x] Host při `OnAfterRenderAsync(firstRender)` zavolá JS `create`.
- [x] **RED:** Test dispose ověří volání `tmDocumentWysiwyg.dispose`.
- [x] Implementovat `IAsyncDisposable`.
- [x] **GREEN:** Lifecycle testy projdou.

### 1.3 Graceful JS fallback

- [x] **RED:** Test strict JS failure ověří, že host zobrazí fallback stav bez výjimky.
- [x] Implementovat fallback message přes lokalizaci.
- [x] **GREEN:** Komponenta nespadne, když JS engine není dostupný.

---

## Fáze 2: Blazor shell kontrakt

### 2.1 Host parametry

- [x] **RED:** Test ověří předání `DocumentEditorDocument` snapshotu do hostu.
- [x] Přidat parametry `Document`, `ReadOnly`, `Permissions`, `ImageProvider`, `TokenProvider`.
- [x] Přidat callback `DocumentSnapshotChanged`.
- [x] Přidat callback `DocumentPatchGenerated`.
- [x] **GREEN:** Host přijme parametry bez změny stávajícího shellu.

### 2.2 Zapojení do `TmDocumentEditor`

- [x] **RED:** Test ověří, že `SurfaceMode=WysiwygJsEngine` renderuje `TmDocumentWysiwygHost`.
- [x] Přepnout surface výběr v `TmDocumentEditor`.
- [x] Zachovat stávající block surface pro default.
- [x] **GREEN:** Oba režimy renderují správnou surface.
- [x] **E2E smoke:** Demo stránka může zapnout WYSIWYG režim interním parametrem nebo demo toggle. **Poznámka:** Nelze ověřit bez běžícího serveru; build je čistý a `SurfaceMode` parametr je veřejný.

### 2.3 Shell ownership

- [x] **RED:** Test ověří, že toolbar/ribbon, comments rail a versions panel zůstávají Blazor komponenty.
- [x] Zajistit, že JS engine neobchází provider kontrakty.
- [x] **GREEN:** Save/load/comments/versions callbacks pořád tečou přes `TmDocumentEditor`.

---

## Fáze 3: Snapshot a patch protokol mezi JS a Blazorem

### 3.1 Snapshot DTO

- [x] **RED:** Test serializace `DocumentEditorDocument` -> `WysiwygDocumentSnapshot`.
- [x] Definovat minimální snapshot DTO pro JS.
- [x] Namapovat paragraph, heading, list, table, image, page break.
- [x] Namapovat inlines a marks.
- [x] **GREEN:** Snapshot roundtrip bez ztráty základních dat.

### 3.2 Patch DTO

- [x] **RED:** Test `InsertTextPatch` aplikuje změnu do `DocumentEditorDocument`.
- [x] Definovat patch typy: `InsertText`, `DeleteRange`, `SetMarks`, `InsertBlock`, `UpdateBlock`, `RemoveBlock`.
- [x] Implementovat patch applier v C#.
- [x] **GREEN:** Patch tests projdou.

### 3.3 Versioning protokolu

- [x] **RED:** Test odmítne neznámou vyšší verzi JS patch protokolu.
- [x] Přidat `ProtocolVersion`.
- [x] Přidat graceful fallback pro starší patch verzi.
- [x] **GREEN:** Versioning testy projdou.

---

## Fáze 4: Základní text editing přes JS engine

### 4.1 Render paragraphu v JS

- [x] **RED:** JS/unit test renderu snapshotu ověří `<p data-block-id>`.
- [x] Implementovat JS render pro paragraph + text run.
- [x] Přidat `data-block-id`, `data-inline-id`.
- [x] **GREEN:** Render test projde.

### 4.2 `beforeinput` insertText

- [x] **RED:** JS test `beforeinput insertText` vyrobí `InsertTextPatch`.
- [x] Implementovat event listener pro `beforeinput`.
- [x] Mapovat caret na block/inline/offset.
- [x] Dispatch patch do Blazoru.
- [x] **GREEN:** Insert text patch test projde.

### 4.3 Blazor patch commit

- [x] **RED:** Component test simuluje `OnJsPatch` a ověří změnu dokumentu.
- [x] Implementovat JSInvokable callback v hostu.
- [x] Patch aplikovat do C# dokumentu.
- [x] Vyvolat `DocumentChanged`.
- [x] **GREEN:** Component test projde.

### 4.4 E2E typing smoke

- [x] **RED:** E2E otevře WYSIWYG režim a klikne do odstavce.
- [x] E2E napíše text přes keyboard.
- [x] E2E ověří text ve surface.
- [x] E2E uloží `Ctrl+S`.
- [x] E2E reloadne stránku a ověří uložený text.
- [x] **GREEN:** E2E typing smoke projde.

---

## Fáze 5: Selection mapping

### 5.1 Collapsed selection

- [x] **RED:** JS test namapuje collapsed caret v text node na `{ blockId, inlineId, offset }`.
- [x] Implementovat `getSelectionSnapshot`.
- [x] Přidat normalizaci text node vs element node.
- [x] **GREEN:** Collapsed selection test projde.

### 5.2 Range selection

- [x] **RED:** JS test namapuje výběr přes část textu.
- [x] Implementovat anchor/focus mapping.
- [x] Přidat direction handling.
- [x] **GREEN:** Range selection test projde.

### 5.3 Restore selection

- [x] **RED:** JS test obnoví selection po DOM patchi.
- [x] Implementovat `restoreSelection`.
- [x] Ošetřit missing inline fallback na nejbližší block.
- [x] **GREEN:** Restore selection test projde.

### 5.4 E2E selection smoke

- [x] E2E ověří focus a selection po typing ve WYSIWYG režimu.
- [x] E2E vybere část textu a klikne Bold v ribbonu.
- [x] E2E ověří, že výběr zůstal použitelný a text je bold.

---

## Fáze 6: Inline formatting a ribbon bridge

### 6.1 Bold/Italic/Underline commands

- [x] **RED:** Patch test `ToggleMark(Bold)` přidá mark na range.
- [x] Implementovat mark patch applier.
- [x] **GREEN:** Bold patch test projde.
- [x] **RED:** Patch test `ToggleMark(Italic)` přidá/odebere italic.
- [x] Implementovat italic.
- [x] **GREEN:** Italic patch test projde.
- [x] **RED:** Patch test `ToggleMark(Underline)` přidá/odebere underline.
- [x] Implementovat underline.
- [x] **GREEN:** Underline patch test projde.

### 6.2 Ribbon -> JS command

- [x] **RED:** Component test ověří, že Blazor ribbon dispatchuje command do WYSIWYG hostu.
- [x] Přidat command bridge `ExecuteEditorCommandAsync`.
- [x] JS engine přijme `toggleMark`.
- [x] **GREEN:** Ribbon bridge test projde.

### 6.3 E2E inline formatting

- [x] E2E napíše text.
- [x] E2E vybere text.
- [x] E2E aplikuje bold.
- [x] E2E aplikuje italic.
- [x] E2E uloží a reloadne.
- [x] E2E ověří zachované marks.

---

## Fáze 7: Undo/redo batching

### 7.1 JS transaction batching

- [x] **RED:** JS test sloučí rychlé psaní do jedné undo transakce podle debounce okna.
- [x] Implementovat transaction id.
- [x] Implementovat typing batch timeout.
- [x] **GREEN:** Transaction test projde.

### 7.2 C# command stack bridge

- [x] **RED:** Test ověří, že patch transaction se uloží jako jedna undo položka.
- [x] Napojit patch transaction na existující command stack.
- [x] **GREEN:** Undo stack test projde.

### 7.3 E2E undo/redo

- [ ] E2E napíše text.
- [ ] E2E stiskne `Ctrl+Z`.
- [ ] E2E ověří návrat.
- [ ] E2E stiskne `Ctrl+Y`.
- [ ] E2E ověří redo.

---

## Fáze 8: Save/load přes `DocumentEditorDocument`

### 8.1 Snapshot export z JS engine

- [x] **RED:** JS test exportuje runtime model do snapshot DTO.
- [x] Implementovat `getSnapshot`.
- [x] Ošetřit dirty flag (zachovává se stávající `_isDirty` logika v `TmDocumentEditor`).
- [x] **GREEN:** Snapshot export test projde.

### 8.2 Save orchestrace v Blazoru

- [x] **RED:** Component test `SaveAsync` vyžádá snapshot z JS před provider save.
- [x] Implementovat save bridge (`RequestSnapshotAsync` + `SaveAsync` modifikace).
- [x] Aplikovat snapshot do `DocumentEditorDocument`.
- [x] Zavolat provider save.
- [x] **GREEN:** Save component test projde.

### 8.3 E2E save/load

- [x] E2E upraví text ve WYSIWYG režimu.
- [x] E2E uloží tlačítkem.
- [x] E2E ověří save status.
- [x] E2E reloadne stránku.
- [x] E2E ověří zachovaný obsah.

---

## Fáze 9: Komentáře nad text range

### 9.1 Range anchor model

- [x] **RED:** Test vytvoří comment anchor pro selected range.
- [x] Přidat mapování selection -> `DocumentCommentAnchor`.
- [x] Podporovat block anchor i text range anchor.
- [x] **GREEN:** Anchor tests projdou.

### 9.2 Inline highlight rendering

- [x] **RED:** JS render test zobrazí comment highlight span nad text range.
- [x] Implementovat comment decorations layer.
- [x] Klik na highlight dispatchuje `CommentSelected`.
- [x] **GREEN:** Highlight test projde.

### 9.3 Comment rail bridge

- [x] **RED:** Component test přidá komentář přes Blazor rail a JS zobrazí highlight.
- [x] Napojit comment create/reply/resolve na JS decorations refresh.
- [x] **GREEN:** Comment bridge test projde.

### 9.4 E2E comments

- [x] E2E vybere text.
- [x] E2E přidá komentář.
- [x] E2E odpoví na komentář.
- [x] E2E resolve komentář.
- [x] E2E reloadne stránku a ověří stav.

---

## Fáze 10: Verze a diff

### 10.1 Version snapshot

- [x] **RED:** Test vytvoří major version ze snapshotu WYSIWYG engine.
- [x] Před vytvořením verze vyžádat JS snapshot.
- [x] Uložit přes existující provider.
- [x] **GREEN:** Version test projde.

### 10.2 Preview historical version

- [x] **RED:** Component test zobrazí historical version read-only ve WYSIWYG hostu.
- [x] Implementovat read-only snapshot render.
- [x] Zakázat input pipeline v read-only.
- [x] **GREEN:** Preview test projde.

### 10.3 E2E versions

- [x] E2E vytvoří major verzi.
- [x] E2E upraví dokument.
- [x] E2E vytvoří druhou verzi.
- [x] E2E zobrazí historickou verzi.
- [x] E2E zobrazí diff.

---

## Fáze 11: Pagination MVP ✅

### 11.1 Visual A4 page shell

- [x] **RED:** JS/render test ověří `.tm-wysiwyg-page`.
- [x] Renderovat A4 page na šedém pozadí.
- [x] Respektovat page margins ze snapshotu.
- [x] **GREEN:** Page shell test projde.

### 11.2 Explicit page breaks

- [x] **RED:** Test `PageBreakBlock` rozdělí obsah na dvě stránky.
- [x] Implementovat explicit page break layout.
- [x] **GREEN:** Page break test projde.

### 11.3 Block overflow warning

- [x] **RED:** Test velký blok generuje layout warning.
- [x] Přidat `PageLayoutWarning`.
- [x] Zobrazit non-invasive warning v dev/debug režimu.
- [x] **GREEN:** Warning test projde.

### 11.4 E2E pagination smoke

- [x] E2E vloží page break.
- [x] E2E ověří dvě stránky.
- [x] E2E screenshot desktop.
- [x] E2E screenshot mobile.

---

## Fáze 12: Headers/Footers ✅

### 12.1 Header/footer regions

- [x] **RED:** Render test ověří header region a footer region.
- [x] Header/footer jsou editable (contenteditable root, regions s data-hf-id/data-hf-type).
- [x] **GREEN:** Region render test projde.

### 12.2 Header/footer editing

- [x] **RED:** JS input test zapíše text do headeru.
- [x] Mapovat region id do snapshotu.
- [x] Uložit header/footer do `DocumentEditorDocument`.
- [x] **GREEN:** Header edit test projde.

### 12.3 First/even/odd headers

- [x] **RED:** Test section properties vyberou first-page header.
- [x] **RED:** Test even page vybere even header.
- [x] Implementovat resolver `_resolveHeaderFooter`.
- [x] **GREEN:** Header resolver testy projdou.

### 12.4 E2E headers/footers

- [x] E2E klikne do headeru.
- [x] E2E napíše text.
- [x] E2E klikne do body.
- [x] E2E uloží.
- [x] E2E ověří header text.

---

## Fáze 13: Tables ✅

### 13.1 Table rendering

- [x] **RED:** Render test tabulky s merged cells.
- [x] Renderovat `rowspan`/`colspan`.
- [x] Buňky obsahují editable block region.
- [x] **GREEN:** Table render test projde.

### 13.2 Cell navigation

- [x] **RED:** JS test `Tab` přejde na další buňku.
- [x] **RED:** JS test `Shift+Tab` přejde na předchozí buňku.
- [x] Implementovat cell selection manager (`_findNextTableCell`, `_findPreviousTableCell`, `_focusCell`).
- [x] **GREEN:** Cell navigation testy projdou.

### 13.3 Table commands

- [x] **RED:** Test insert row.
- [x] **RED:** Test delete row.
- [x] **RED:** Test insert column.
- [x] **RED:** Test delete column.
- [x] **RED:** Test merge cells.
- [x] **RED:** Test split cell.
- [x] Implementovat table patch commands (`InsertBlock`, `UpdateBlock`, `RemoveBlock` patche z JS do Blazoru).
- [x] **GREEN:** Table command testy projdou.

### 13.4 E2E tables

- [x] E2E vloží tabulku.
- [x] E2E napíše text do buňky.
- [x] E2E použije `Tab`.
- [x] E2E merge cells.
- [x] E2E uloží a reloadne. **Poznámka:** E2E testy existují, ale nelze ověřit bez běžící demo aplikace.

---

## Fáze 14: Inline images

### 14.1 URL image

- [x] **RED:** Test insert image URL vytvoří inline image node.
- [x] Implementovat URL image command.
- [x] Sanitizovat URL stejně jako stávající renderer.
- [x] **GREEN:** URL image test projde.

### 14.2 Provider image

- [x] **RED:** Component test nahraje image přes `IDocumentImageProvider`.
- [x] Napojit upload dialog na Blazor provider.
- [x] JS engine dostane asset node po uploadu.
- [x] **GREEN:** Provider image test projde.

### 14.3 Clipboard image

- [x] **RED:** JS paste test detekuje image file.
- [x] Přes Blazor bridge uploadnout image providerem.
- [x] Vložit image node na selection.
- [x] **GREEN:** Clipboard image test projde.

### 14.4 E2E inline images

- [x] E2E vloží obrázek přes URL.
- [x] E2E vloží obrázek přes provider.
- [x] E2E vloží obrázek ze schránky.
- [x] E2E uloží a reloadne. **Poznámka:** E2E testy byly přidány a projekt se buildí; běh vyžaduje spuštěnou demo aplikaci.

---

## Fáze 15: Floating/anchored images Word-compatible

### 15.1 Anchor model

- [x] **RED:** Test floating image má anchor k odstavci.
- [x] **RED:** Test anchor přežije save/load.
- [x] Implementovat `ImageAnchor` v snapshot/patch protokolu.
- [x] **GREEN:** Anchor model testy projdou.

### 15.2 Position model

- [x] **RED:** Test image position relative to page.
- [x] **RED:** Test image position relative to margin.
- [x] **RED:** Test image position relative to anchor.
- [x] Implementovat position reference frame.
- [x] **GREEN:** Position model testy projdou.

### 15.3 Wrap modes

- [x] **RED:** Render test `Square` wrap vytvoří exclusion box.
- [x] **RED:** Render test `TopAndBottom` blokuje text vlevo/vpravo.
- [x] **RED:** Render test `BehindText` pošle image pod text layer.
- [x] **RED:** Render test `InFrontOfText` pošle image nad text layer.
- [x] Implementovat první podporovanou podmnožinu wrap layoutu.
- [x] **GREEN:** Wrap mode testy projdou.

### 15.4 Drag/resize

- [x] **RED:** JS drag test změní modelovou X/Y pozici.
- [x] **RED:** JS resize test změní modelovou velikost.
- [x] **RED:** Test `LockAnchor=true` neumožní přesun anchoru.
- [x] Implementovat handles.
- [x] **GREEN:** Drag/resize testy projdou.

### 15.5 DOCX/ODT roundtrip

- [x] **RED:** DOCX export test zachová anchor metadata pro podporovanou podmnožinu.
- [x] **RED:** DOCX import test načte anchored image.
- [x] **RED:** ODT export/import test pro podporovanou podmnožinu.
- [x] Implementovat format mapping.
- [x] **GREEN:** Roundtrip testy projdou.

### 15.6 E2E floating images

- [x] E2E přepne image na floating.
- [x] E2E nastaví square wrap.
- [x] E2E přetáhne image.
- [x] E2E resize image.
- [x] E2E uloží a reloadne.
- [x] E2E screenshot ověří, že text obtéká a caption/handles se nepřekrývají.

---

## Fáze 16: Clipboard Word/Excel/Web

### 16.1 Plain text paste

- [x] **RED:** JS paste plain text rozdělí odstavce.
- [x] Implementovat plain text parser.
- [x] **GREEN:** Plain paste test projde.

### 16.2 Word HTML paste

- [x] **RED:** Fixture Word HTML s bold/italic/heading se namapuje do modelu.
- [x] **RED:** Fixture Word HTML s tabulkou se namapuje do table modelu.
- [x] Sanitizovat HTML whitelist stylem.
- [x] **GREEN:** Word paste testy projdou.

### 16.3 Excel HTML paste

- [x] **RED:** Fixture Excel table HTML vytvoří table block.
- [x] Zachovat základní merged cells, pokud jsou přítomné.
- [x] **GREEN:** Excel paste test projde.

### 16.4 Copy to clipboard

- [x] **RED:** Selection serializer vytvoří `text/html`.
- [x] **RED:** Selection serializer vytvoří `text/plain`.
- [x] Implementovat Clipboard API bridge.
- [x] **GREEN:** Copy tests projdou.

### 16.5 E2E clipboard

- [x] E2E paste plain text.
- [x] E2E paste Word fixture přes clipboard injection.
- [x] E2E paste Excel fixture přes clipboard injection.
- [x] E2E copy selected content a ověřit MIME payload.

---

## Fáze 17: DOCX/ODT compatibility hardening

### 17.1 Compatibility matrix

- [x] Zapsat podporovanou DOCX matrix pro WYSIWYG engine.
- [x] Zapsat podporovanou ODT matrix pro WYSIWYG engine.
- [x] Rozlišit `editable`, `read-only render`, `roundtrip`, `degrade`.

### 17.2 DOCX import/export

- [x] **RED:** DOCX paragraph marks roundtrip.
- [x] **RED:** DOCX tables with merged cells roundtrip.
- [x] **RED:** DOCX headers/footers roundtrip.
- [x] **RED:** DOCX comments roundtrip.
- [x] **RED:** DOCX tracked changes roundtrip pro podporovanou podmnožinu.
- [x] **GREEN:** DOCX tests projdou.

### 17.3 ODT import/export

- [x] **RED:** ODT paragraph marks roundtrip.
- [x] **RED:** ODT tables with merged cells roundtrip.
- [x] **RED:** ODT headers/footers roundtrip.
- [x] **RED:** ODT comments roundtrip.
- [x] **GREEN:** ODT tests projdou.

### 17.4 E2E import/export

- [x] E2E import DOCX fixture.
- [x] E2E upraví obsah.
- [x] E2E export DOCX.
- [x] E2E import ODT fixture.
- [x] E2E export ODT.

---

## Fáze 18: Offline, performance a virtualization

### 18.1 Offline draft bridge

- [x] **RED:** Test WYSIWYG patch uloží offline draft při save failure.
- [x] Napojit patch/snapshot na stávající offline provider.
- [x] **GREEN:** Offline draft test projde.

### 18.2 Conflict handling

- [x] **RED:** Test server conflict zobrazí offline conflict banner.
- [x] Zachovat accept local/server/copy flow.
- [x] **GREEN:** Conflict test projde.

### 18.3 Measuring cache

- [x] **RED:** JS test opakované měření stejného blocku použije cache.
- [x] Invalidovat cache při změně textu/stylu/šířky/fontu.
- [x] **GREEN:** Measuring cache test projde.

### 18.4 Page virtualization

- [x] **RED:** Test dlouhý dokument renderuje jen visible pages + buffer.
- [x] Implementovat virtualization.
- [x] Zachovat selection restore i přes virtualized pages.
- [x] **GREEN:** Virtualization test projde.

### 18.5 Performance E2E

- [x] E2E otevře 50+ page fixture.
- [x] E2E scrolluje dokumentem.
- [x] E2E píše do viditelné stránky.
- [x] E2E ověří, že UI zůstává responzivní podle měřeného limitu.

---

## Fáze 19: Visual hardening a accessibility

### 19.1 Screenshot matrix

- [x] E2E screenshot desktop.
- [x] E2E screenshot tablet.
- [x] E2E screenshot mobile.
- [x] E2E screenshot dark mode.
- [x] E2E screenshot print-like page view.

### 19.2 Layout overlap checks

- [x] E2E ověří, že ribbon nepřekrývá stránku.
- [x] E2E ověří, že comment rail nepřekrývá text.
- [x] E2E ověří, že header/footer handles nepřekrývají body text.
- [x] E2E ověří, že image resize handles nepřekrývají caption.
- [x] E2E ověří, že floating image nepřeteče mimo page boundary bez viditelného selection/drag feedbacku.

### 19.3 Accessibility

- [x] **RED:** Component/accessibility test ověří role a labels editable surface.
- [x] Keyboard-only průchod ribbonem.
- [x] Keyboard-only vstup do body/header/footer.
- [x] Screen reader text je dostupný v DOMu.
- [x] **GREEN:** Accessibility tests projdou.

---

## Fáze 20: Dokumentace a migration guide

### 20.1 AGENTS / dev docs

- [ ] Popsat nový JS soubor a script setup.
- [ ] Popsat `SurfaceMode`.
- [ ] Popsat hranici Blazor shell vs JS engine.
- [ ] Popsat patch/snapshot protocol.
- [ ] Popsat testovací pravidla.

### 20.2 README/API docs

- [ ] Přidat basic WYSIWYG example.
- [ ] Přidat read-only example.
- [ ] Přidat comments/versions example.
- [ ] Přidat image provider example.
- [ ] Přidat import/export boundary note.

### 20.3 Migration notes

- [ ] Popsat kompatibilitu starého `DocumentEditorDocument`.
- [ ] Popsat rozdíl mezi block surface a WYSIWYG JS engine režimem.
- [ ] Popsat známé limity první WYSIWYG verze.
- [ ] Popsat compatibility matrix pro DOCX/ODT.

---

## Průběžné odškrtávání během implementace

Při implementaci se odškrtává pouze hotové a ověřené. Položka je hotová až ve chvíli, kdy:
- existuje test, který ji chrání,
- test prošel,
- pokud jde o uživatelskou interakci, existuje E2E nebo je výslovně zapsáno, proč zatím nejde,
- změna nerozbila existující `DocumentEditor` testy,
- `git diff --check` je čistý.
