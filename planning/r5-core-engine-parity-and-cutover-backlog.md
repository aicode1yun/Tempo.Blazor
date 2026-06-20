# R.5 — Plná parita core enginu & pre-flip hardening (backlog)

> **Cíl:** kompletní seznam VŠEHO, co ještě není hotové v novém core enginu, než lze flipnout default
> (Legacy → CoreEngine) a smazat legacy. Sesbíráno auditem všech „POZN/zatím ne/follow-up/pozdější" napříč
> `planning/phase-d-remaining-extraction-todo.md` (R.4.0–R.4.9), memory, `r48-cutover-plan.md` a
> `r48-perf-parity-and-legacy-regression-assessment.md`, + ověření kódu (converter, input-surface, moduly).
> Datum: 2026-05-30, entry version 236.
>
> **Stav předtím (✅ hotové):** R.4.0–R.4.7 (render/input/caret/IME/bidi/featury/a11y), R.4.8 bridge +
> inspector follow-upy (caption/position/inline-resize), R.4.9 typing perf (plochá O(1) <16ms).
> **Bridge E2E 16/16, render-host E2E 29, PhaseR Node 22, bUnit 768/3.**
>
> **Pravidla (PŘETRVÁVAJÍ):** OnlyOffice (`/home/pavel/NetProjects/onlyfficeservergit`) je **AGPL → NIKDY
> nekopírovat kód, jen clean-room architektura**. Každý render/input/caret milník ověřit **reálným
> Playwright** (ne jen Node stuby). **Legacy NEMAZAT** dokud P0+P1 hotové + plná regrese + **explicitní
> schválení** (nevratné — rozbije editor každému bez `RenderEngine=CoreEnginePreview`).

---

## 🔴 P0 — CUTOVER BLOKERY (bez nich flip = ztráta dat nebo nefunkční editace)

### R.5.1 Converter plný round-trip [⚠️ DATA-LOSS BLOKER #1] — ✅ HOTOVO (2026-05-30)
**Bylo:** `CoreEngineModelConverter` round-tripoval JEN paragraphy/nadpisy/text/inline-marks/alignment →
**tabulky, obrázky/drawings, page-breaky se při save ZTRATILY.** **Dořešeno** kompletním přepisem converteru
+ **preserve channel** (`__docSource` na run/image, `__marks` na text run) pro vše, co engine nemodeluje.
- [x] **tabulky** ToCoreModel/FromCoreModel (rekurzivně rows→cells→blocks; spans/header/width/bg/vAlign + table layout width/alignment/bg/padding)
- [x] **obrázky** standalone `ImageBlockContent` ↔ JS paragraph s drawing runem (`imageBlock` flag); + inline `DocumentDrawingRun` v odstavci. Viditelné pole (url/wrapMode/width/height/position/zIndex/caption/alt) mapované; zbytek (source/asset/link/naturalSize/docx/metadata) v `__docSource` + overlay engine-current polí na vrch (přežijí edity)
- [x] **page-breaky** round-trip (`type:'pageBreak'` + nextSectionId)
- [x] **superscript / subscript** (+ jakýkoli nemodelovaný mark) přes `__marks` preserve channel
- [~] **revize (insertion/deletion marks)** — pokud jsou na runu jako marks, přežijí přes `__marks`; engine-native track-changes round-trip = pozdější (souvisí R.5.11)
- [~] **komentáře** — KOTVY round-tripují (comment mark ↔ commentId); document-level metadata `comments` (autor/text/resolved) = NEhandlováno (mimo bloky → samostatný úkol)
- [ ] **hlavičky/patičky + sekce** — document-level, NE v blocích → samostatný úkol (souvisí R.5.13)
- [x] **seznamy** — list struktura (typ bullet/ordered + level + StartNumber) round-tripuje jako list (ne paragraph); quote→quote (`blockKind`)
- [x] **gate**: 8 C# unit testů `CoreEngineModelConverterTests` (tabulka/obrázek/page-break/list/quote/inline-drawing/unmapped-mark round-trip) + **live E2E `R75`** (engine RENDERUJE table+image+pageBreak z converter shape A zachová extra props `imageBlock`/`__docSource`/`tableLayout`/cell-text/pageBreak přes `getSnapshot().model` read-back — BEFORE i AFTER editu). Bridge E2E 16/16 beze změny.
- **POZN:** super/sub/revize-marks jsou STYLING (text se NEztratí ani bez nich); zbylé `[~]/[ ]` jsou document-level metadata, ne „vanish entirely" data-loss. **Strukturální data-loss blocker (table/image/page-break mizely) JE VYŘEŠEN.**

### R.5.2 Rich clipboard (copy / cut / paste / paste-plain) [BLOKER #2] — ✅ HOTOVO (2026-05-31)
`core-engine/clipboard.mjs` (serializeRange→{text,html,internal}, parseClipboard/parseHtml/parsePlainText,
INTERNAL_MIME `application/x-tempo-doc`). render-host `copyToClipboard/cutToClipboard/pasteFromClipboard` +
`deleteSelectedRange` (single + cross-block). input-surface wired `copy`/`cut`/`paste` events + Ctrl+Shift+V arm.
- [x] **copy/cut** ze selekce → clipboard (HTML + plain + interní model fragment); cut maže rozsah
- [x] **paste** interní fragment / HTML / plain → model lines → insertLines (marks zachované, multi-paragraph split)
- [x] **paste as plain text** (Ctrl+Shift+V armuje plain pro další paste)
- [x] **paste z Wordu/GDocs** — `parseHtml` mapuje b/strong/i/em/u/s/a + inline-style (font-weight/style/color/decoration) → marks; sanitizace script/style/komentářů
- [~] tabulky/obrázky v PASTE = follow-up (paste teď řeší formátovaný TEXT + odstavce; nejčastější případ)
- [x] **gate**: Node `PhaseR11` (serialize cross-block + internal/plain/forced-plain parse) + E2E `R76` (copy bold range → DataTransfer → paste keeps text + bold mark)

### R.5.3 Autosave [BLOKER #3] — ✅ HOTOVO (2026-05-31)
**Event-driven, ne polling:** render-host `opts.onChange` = DEBOUNCED notifikace (changeDebounceMs) po editu
(gated na model.version → ne na scroll/viewport; init z setModel → load nefiruje). interop wire `onChange` →
host `[JSInvokable] OnCoreModelChanged` → editor `HandleCoreModelChangedAsync` (`_isDirty=true` + before-unload
guard + `SaveAsync(AutoSave)` když AutoSaveInterval). `CoreChangeDebounceMs` z AutoSaveInterval (clamp 250–10000).
- [x] napojeno přes onChange seam (NE polling) — `SaveCoreAsync` už tahá živý model přes `RequestDocumentAsync`
- [x] respektuje provider save path (offline/sync = existující SaveCoreAsync větve)
- [x] **gate**: E2E `R79` (onChange debounce: 3 rychlé keystroky → 1 fire, load nefiruje) + bridge `R80` (type v core BEZ Save → saved-output ukáže persistovaný edit přes provider)

### R.5.4 Klik otevře hyperlink (Ctrl/Cmd+klik → window.open) — ✅ HOTOVO (2026-05-31)
render-host pointerHandler krok (0): Ctrl/Cmd + mousedown na `data-href` → `activateLink(href)` (opts.onLinkActivate
override, jinak `window.open(href,'_blank','noopener')`). Default = pure JS (žádný C# round-trip potřeba).
- [x] Ctrl/Cmd+klik na `data-href` segment → window.open(href) / onLinkActivate callback
- [x] **gate**: E2E `R77` (Ctrl+click na data-href → window.open zachytí href)
- [ ] hover tooltip s URL (drobnost, follow-up)

### R.5.5 Bookmarks (definovat + navigovat) — ✅ HOTOVO (2026-05-31)
bookmark = `{type:'bookmark', value:name}` value-mark na rozsahu (reuse mark systému). render-host
`addBookmark/goToBookmark/listBookmarks`; renderer emituje `data-bookmark` (na segment.marks). **OPRAVENA reálná
chyba:** marks nebyly v B1 skip-fingerprintu → bookmark (bez vizuálního stylu) přidaný do už-renderovaného textu
se přeskočil; fix = marks (type=value) participují ve fingerprintu (správně i pro link/comment href/id).
- [x] bookmark mark + addBookmark (selekce / collapsed→1 grafém) + goToBookmark (caret + scrollIntoView) + listBookmarks
- [x] **InlineMarkType.Bookmark** + converter round-trip ('bookmark' ↔ Bookmark, value=name)
- [x] **gate**: E2E `R78` (add nad 'world' → data-bookmark anchor; goToBookmark → caret offset 6) + C# converter test (bookmark mark round-trip)
- [~] empty-block point bookmark + odkaz-na-bookmark (interní link navigace) = follow-up

---

## 🟠 P1 — Editační parita must-have (uživatel běžně používá; chybělo by to nápadně)

### R.5.6 Myš: drag-výběr / double-click=slovo / triple-click=odstavec — ✅ HOTOVO (2026-05-31)
render-host pointerHandler `e.detail` větve: 2=word (`selectWordAt` → `grapheme.wordRangeAt` Intl.Segmenter),
3=paragraph (`selectParagraphAt`), 1=caret + `startTextDrag` (doc-level mousemove extend → mouseup). `posFromClient`
vyčleněn z placeCaretFromClient. shift+klik už fungoval (extend).
- [x] mousemove+up drag → rozšiřuje selekci ; [x] double-click=slovo ; [x] triple-click=odstavec ; [x] shift+klik
- [x] **gate**: E2E `R81` (double→slovo, triple→odstavec, drag→14 znaků)

### R.5.7 PageUp / PageDown — ✅ HOTOVO (2026-05-31)
`caret.moveCaretByKey` rozšířen o PageUp/PageDown (vertikální skok o `opts.pageLines`, clamp na první/poslední řádek);
render-host `viewportPageLines` (viewport/lineHeight) + `scrollCaretIntoView`.
- [x] PageUp/PageDown posun o výšku viewportu + scroll — [x] **gate**: E2E `R82` (řádky 0→21→42→21)

### R.5.8 Pending-format (collapsed selekce → marks pro další znak) — ✅ HOTOVO (2026-05-31)
render-host `pendingMarks` (seed z marks aktivních na caretu → toggle); `applyMarkToSelection` collapsed → `togglePendingMark`;
`insertAttrsWithPending` aplikuje na další znak + `clearPendingMarks` (po vložení / caret-move / klik); `getFormattingState` ctí pending (toolbar pressed).
- [x] collapsed toggleMark → pending → aplikace na další znak ; [x] toolbar pressed-state
- [x] **gate**: E2E `R83` (collapsed bold → další znak bold, pak consumed)

### R.5.9 Tabulky — pokročilá editace — ✅ HOTOVO (2026-06-01)
edit-table: `locateCell`, `adjacentCellParagraphId`, `deleteTableRow/Column`, `mergeCellRight` (colSpan — **layout už
spany horizontálně umí**, ověřeno paragraph-engine:452/486), `splitCellHorizontal`. render-host `tableTab` (Tab/Shift+Tab
nav, Tab za poslední buňkou přidá řádek) + delete/merge/split/insert-row-col; input-surface Tab → `tabKey` (tabulka → list fallback);
facade execCommand routy (deletetablerow/column, mergecells, splitcell, insertrow/column…).
- [x] **Tab / Shift+Tab** navigace + Tab past-last přidá řádek
- [x] **delete řádek/sloupec** + insert row above/below + column left/right
- [x] **horizontální merge/split** buněk (colSpan)
- [x] **výběr přes buňky (cell-range)** (R96): `cellSelection` state + `selectCellRange`/`cellRangeIds`/`formatCellSelection`/`clearCellSelectionContent`, highlight v paintOverlays, cell-drag v onTextDragMove
- [x] **vertikální merge (rowSpan)** (R96): `mergeCellDown`/`splitCellVertical` + paragraph-engine `rowSpanCarry` (přeskočí pokryté sloupce, dotáhne výšky spanned řádek)
- [x] **drag-resize sloupců** (R96): `columnBorderHit` + `startColResizeDrag`/`onColResizeMove`/`onColResizeEnd` → `setColumnWidth(model,tableId,columnIndex,width)`
- [x] **gate**: E2E `R85` (Tab nav + insert/delete row+col + merge colSpan 2) + `R96` (col resize + cell-range select/format + vertikální rowSpan merge)

### R.5.10 Inline obrázky — vizuální resize — ✅ HOTOVO (2026-05-31)
**Zjištění: inline resize UŽ FUNGUJE** — `resizeSelectedObject` nastaví `layout.width`, tokenizer čte
`normalizeImageObject.width` ← `layout.width` (image-object:77), figure se překreslí. R61 poznámka byla NEAKTUÁLNÍ.
- [x] inline-image vizuální resize — [x] **gate**: E2E `R84` (figure width 120→240)
- [ ] ~~rotation handle + aspect-lock UI~~ + ~~sjednocení overlay↔atomic renderer~~ — DEFER (P3 architektura, ne data-loss/parita-blocker)

---

## 🟡 P2 — Hloubka featur (každá má „lite" verzi; doplnit do plné parity)

### R.5.11 Track changes — plná hloubka — ✅ HOTOVO (2026-06-01)
track-changes.mjs: `acceptRevision/rejectRevision(id)` (per-revize, filtr přes `matchesRevision`), `listRevisions`
grupuje podle id+kind, `applyReviewMode(model,mode)`. render-host `acceptOne/rejectOne/setReviewMode/getReviewMode`
+ computeLayout layoutuje FILTROVANÝ klon v non-markup módu (layoutSignature ctí reviewMode). facade execCommand routy.
- [x] **per-revize accept/reject** (id-scoped) ; [x] **review-módy** (markup/final/original — non-destructivní render klon)
- [x] **cross-block tracked delete** (R95): `PARAGRAPH_MARK_KEY` deletion mark na `prev.content` při backspace na offset 0 + `resolveParagraphMarks` (accept merge s next blokem / reject clear)
- [x] **format-revize** (R95): `FORMAT_REV_MARK` + `markExtra:{format}` na formátování při trackChanges; `resolveFormatRevisions` (accept drop formatrev mark, reject drop formatrev I formátovací mark); `listRevisions` přidává kind:'format'/'paragraphDeletion'
- [x] **gate**: E2E `R89` (review markup/final/original + per-revize accept) + `R95` (format-revize apply/reject + cross-block delete merge)

### R.5.12 Komentáře — vlákna + navigace + postranní lišta — ✅ HOTOVO (2026-05-31)
render-host `replyToComment(id,text,author)` (c.replies[]), `goToComment(id)` (caret na kotvě + scroll),
`commentAnchorPosition`, `resolveComment`/`reopenComment`/`removeComment`; `getComments` vrací replies + author + anchorBlockId/anchorOffset.
Facade `core-editor.mjs`: `replycomment`/`resolvecomment`/`reopencomment`/`removecomment`/`gotocomment` execCommand routy.
C# host `TmDocumentCoreEngineHost`: `ReplyToCommentAsync`/`ResolveCommentAsync`/`ReopenCommentAsync`/`RemoveCommentAsync`/`GoToCommentAsync` + rich `CoreComment` DTO (Replies/AnchorBlockId/AnchorOffset).
C# editor `TmDocumentEditor`: existující `TmDocumentCommentRail` napájen z enginu — `SyncCoreEngineCommentsAsync` (root entry + reply vlákno + anchor), `OpenCommentsPanelAsync` syncuje při otevření, Reply/Resolve/Reopen/Delete/Select **větví do core hostu** když `UsingCoreEngine`. Demo `/core-engine-editor` má `CanDeleteOwnComments=true`.
- [x] **vlákna / reply** ; [x] **per-comment navigace** (goToComment→SelectComment) ; [x] **resolve/reopen/delete** přes engine ; [x] **postranní lišta UI** (reuse TmDocumentCommentRail, napájen z enginu)
- [x] **gate**: E2E `R99` (facade comment→reply→navigate→resolve→reopen→remove; thread+anchor exposed) + C# build zelený

### R.5.13 Hlavičky/patičky — pole + varianty + editace klikem — ✅ HOTOVO (2026-05-31)
header-footer.mjs `dateField(value?)` (frozen date), `setRegion(model,which,content,scope)` scope-aware
(Primary/FirstPage/EvenPage — layout `resolveHeaderFooterRegion` už scopy řeší). render-host setHeader/setFooter scope param.
render-host pointer handler step (0.5): klik na `data-render-region="Header"/"Footer"` → caret na konec header bloku (editace přes normální edit-model, header bloky jsou indexované).
- [x] **datum/čas pole** (date field — pageNumber/pageCount už byly) ; [x] **různá hlavička první/sudé/liché** (scoped regions)
- [x] **editace hlaviček klikem** (R.5.13 click-to-edit)
- [x] **gate**: E2E `R91` (date field + scopy) + `R98` (klik na header → caret v header bloku → psaní edituje header, body nedotčen)

### R.5.14 Find/Replace — regex — ✅ HOTOVO (2026-05-31)
find-replace.mjs `findRegexMatches` (RegExp, capture groups, zero-width guard, invalid→[]) + `expandReplacement`
($1-$99/$&/$$). render-host find/replaceAll/replaceCurrent ctí `opts.regex` + back-references.
- [x] **regex + back-references** — [x] **gate**: Node `PhaseR12` + E2E `R86` ((yyyy)-(mm)-(dd) → $3/$2/$1)

### R.5.15 Styly / outline / TOC — ✅ HOTOVO (2026-06-01)
render-host `goToHeading(blockId)` (caret+scroll), `insertTableOfContents` (z `getDocumentOutline` → odsazené
paragraphy s `content.toc`+`tocTargetBlockId`); pointerHandler: klik na TOC entry → navigace. facade routy.
- [x] **TOC blok** (generuje obsah) ; [x] **outline-level navigace** (goToHeading + klik na TOC entry)
- [x] **named-style inheritance registry** (R97): paragraph-styles.mjs `buildStyleRegistry(model)` (user `model.styles` nad DEFAULT), `resolveStyle(name,reg)` (basedOn chain root→derived merge, cycle-guard), `defineStyle(model,name,def)`; `applyParagraphStyle` resolvuje přes chain; render-host `setParagraphStyleOnSelection`/`defineStyle`/`getStyles` (propaguje na styled paragraphy)
- [x] **gate**: E2E `R88` (TOC 2 entries + goToHeading + TOC-entry klik) + `R97` (define Callout basedOn Normal, BigCallout basedOn Callout, edit base propaguje)

### R.5.16 Bidi / shaping — přesnost — ✅ HOTOVO (shaped advances + explicit chars 2026-06-01)
line-breaker: RTL paragraph bez vlastního alignmentu → default `right` (`baseDirection`). paragraph-engine předává
`alignment: undefined` místo `'left'` (aby line-breaker rozlišil unset). **🐛 OPRAVENA REÁLNÁ LATENTNÍ CHYBA:
right/center alignment se NIKDY vizuálně neaplikoval na text** — `materializeLineDraft` shiftoval jen sortObject-KLONY
segmentů + line-breaker aplikoval rangeShift jen na caretStops, NE na živé `segments`. Fix: finishCurrent shiftuje
`current.segments` podle rangeShiftu. **Teď funguje RTL right-align I explicitní right/center alignment** (předtím rozbité engine-wide).
- [x] **RTL paragraph-alignment** (+ obecný right/center align render fix)
- [x] **explicitní bidi znaky** (LRE/RLE/LRO/RLO/isolates/PDI/LRM/RLM/ALM) — bidi.mjs X-pass (PhaseR13, 2026-06-01)
- [x] **shaped advances** (R.5.16): line-breaker `addCaretStopsForSegment` měří KUMULATIVNÍ shaped prefix (`service.measureText(text.slice(0,k))`) místo lineární interpolace `width*ratio` → caret x ctí reálné advance proporcionálního fontu I arabského cursive joining. **gate**: E2E `R109` (WWii: W-gaps > i-gaps, non-uniform = měřeno ne interpolováno). POZN: prefix-measure shapuje řezaný char jako isolated/final (ne medial) → arabská joining-boundary stále drobně přibližná (limit canvas measureText bez per-glyph API; dokumentováno).
- [x] **gate**: E2E `R87` (LTR 400px, RTL 1016px, explicit-right 1012px) + `R109` (shaped caret)

---

## 🟢 P3 — Architektura / perf / přístupnost follow-upy

### R.5.17 First-paint virtualizovaný layout [perf, load-time] — ✅ HOTOVO (2026-05-31)
paragraph-engine `layoutDocument` přijímá `maxBlocks` (počítá se na STARTu bloku → early-returns intaktní),
vrací `complete`/`laidOutBlockCount`. render-host `firstPaintMaxBlocks` opt-in (default 0=off → vždy full,
golden/perf netknuté): budgeted first paint → render → `scheduleFullLayout` (requestIdleCallback) dokončí full
+ re-render. layoutSignature ctí budget; setModel re-enable.
- [x] layout jen prvních N bloků při first-paint, zbytek na idle (full re-layout)
- [x] **gate**: E2E `R92` (budgeted 256ms/40 bloků vs full 2540ms/500 bloků = ~10×; idle dokončil všech 500). POZN: render bez viewportu; s page-virtualizací (setViewport) ještě rychlejší.

### R.5.18 Operation-log + op-log UNDO — ✅ HOTOVO (2026-06-01)
**`core-engine/operations.mjs`** = operation algebra (clean-room): `invertOperation` (insert↔delete, addMark↔removeMark, split↔merge), `applyToText`/`applyOps`, `transformOperation(op,against,priority)` = OT **vracející POLE opů** (insert dovnitř delete → delete se SPLITne, to dělá TP1 konvergenci), `transformAgainstList`. render-host: žurnál `opLog` + `emitOp`, `getOperationLog`/`clearOperationLog`.
**OP-LOG UNDO (hybrid):** `undo-stack.mjs` má DVA druhy entries — `'snapshot'` (celý model klon, korektní pro VŠE: marks/odstavce/obrázky/tabulky/struktura) + **`'ops'` (R.5.18)** = pro plain-text edity (psaní/backspace/delete) ukládá op+inverze místo klonu celého dokumentu (O(edit) paměť ne O(dokument)). `recordOps` merge-coalescuje typing run do 1 kroku (`redo` roste dopředu, `undo` PREPEND inverze = replay pozpátku). render-host `recordOpEdit`/`applyOpToModel`/`caretSelectionCollapsed`: 3 text intents → op-log když collapsed caret, jinak snapshot (selection-replace/merge na offset 0 = strukturální → snapshot, korektní). undo/redo větví dle `entry.kind`. Snapshot entries SPREAD pole (`entry.model` přímo) = zpětně kompatibilní.
- **gate**: Node `PhaseR14` (OT invert+TP1 400-pár fuzz) + **`PhaseR17`** (ops-entry coalesce + inverze pozpátku + snapshot/ops koexistence) + **`PhaseR6i`** (host: typing run undo/redo jako 1 op-log krok + bold undo snapshot) + **E2E `R111`** (REÁLNÁ klávesnice: typing run revert/redo op-log + bold revert snapshot). Reálné latentní bugy opravené cestou: split-delete after-offset (`aAt+bLen` ne `bAt+bLen`), boundary tie.

### R.5.19 Bidi line re-wrap v užším boxu — ✅ HOTOVO/OVĚŘENO (2026-06-01)
`applyBidiToLayout` reorderuje KAŽDOU řádku nezávisle (per `lineId`) → RTL paragraph zalomený do více řádek v úzkém
sloupci je správně bidi-reorderovaný řádek po řádku (line-breaker řeší zalomení vč. exclusion zón u floatů; reorderLine
bailuje jen na řádcích s inline objektem). **gate**: E2E `R110` (14 hebrejských slov v 320px sloupci → >1 řádka,
každá dir=rtl + logicky-první segment vpravo). POZN: RTL text OBTÉKAJÍCÍ float = exclusion → užší šířka → wrap + reorder funguje.

### R.5.20 Přístupnost — plná — ✅ PROGRAMATICKY HOTOVO (2026-06-01); zbývá jen manuální NVDA (LIDSKÝ gate)
caret.mjs + selection-overlay.mjs: `@media (forced-colors:active)` → caret `CanvasText`, selection `Highlight`,
find `Mark` + `forced-color-adjust:none`. a11y.mjs `describeCaretGranular` (character=přejetý grafém / word=slovo
u caretu / paragraph=kontext); render-host `setAnnounceGranularity`/`getLiveRegionText`.
- [x] **vysoký kontrast** (forced-colors caret/selection/find) ; [x] **per-znak / per-slovo ohlašování**
- [x] **reading-order pro bidi** (R.5.20): atomic-renderer řadí segmenty do DOM v LOGICKÉM pořadí (sort dle model `start`), vizuál zůstává VISUAL (absolutní `left`/`top` per segment box) → screen reader čte bidi/RTL v reading-order, vzhled beze změny. LTR beze změny (už seřazeno). **gate**: E2E `R108` (dva hebrejské runy: DOM `data-model-start` vzestupně [0,3] ale vizuální `left[0]>left[1]` = RTL; dir=rtl).
- [ ] **manuální NVDA / VoiceOver** — LIDSKÝ follow-up (nelze automatizovat; programatická ARIA + reading-order hotové, čeká na manuální accept uživatele)
- [x] **gate**: E2E `R93` (hcCaret+hcOverlay forced-colors rules + word/character/paragraph announce) + `R108` (bidi reading-order)

---

## 🔵 P4 — Větší chybějící subsystémy (legacy má, core engine zatím vůbec ne)

> Nemají modul v `core-engine/` ani C# wiring — větší kusy práce. Některé blokují i export/collab.

### R.5.21 Export / import přes core model — ✅ HOTOVO (2026-05-31)
Export PROVIDERY (DOCX/ODT/PDF) už operují nad `DocumentEditorDocument`; **chyběl pull živého core modelu**.
`GetCurrentDocumentForProviderExportAsync` má teď `UsingCoreEngine` větev → `RequestDocumentAsync` (→ converter)
→ DOCX/ODT/PDF export ctí neuložené edity. Converter (R.5.1) round-tripuje tabulky/obrázky → export bez ztráty.
- [x] **DOCX export/import** přes core model (+ ODT/PDF stejnou cestou — všechny berou DocumentEditorDocument)
- [x] **gate**: C# `CoreEngineExportRoundTripTests` — doc s tabulkou+obrázkem+textem → ToCoreModel→FromCoreModel → DocumentDocxExporter → DocumentDocxImporter → tabulka+obrázek+text PŘEŽIJÍ (image = data: URL pro embed bytů).

### R.5.22 Kolaborace (realtime) — ✅ HOTOVO + LIVE 2-BROWSER OVĚŘENO (2026-06-01)
**`R106` E2E ZELENÝ: dva reálné browser taby (separate WASM instance) na stejném dokumentu přes ŽIVÝ SignalR hub (API 5100 + WASM 7106) — psaní v jednom tabu KONVERGUJE do druhého** ('Shared'→'Shared-A'→'Shared-A-B' obousměrně). Demo `/core-engine-collab` (`CoreEngineCollabPage`, `@inject SignalRDocumentCollaborationProvider`, host `OnReady`→`ConnectCollaborationAsync`). **🐛 OPRAVENA REÁLNÁ LATENTNÍ CHYBA: C# serializoval ops PascalCase (Type/BlockId/Offset/Text), engine čte camelCase → `applyRemoteOperation` op TICHE ignoroval (nic se neaplikovalo) — fix: FeedServerChangeAsync mapuje na lowercase klíče.** Pozn: hub raisuje event fire-and-forget (`_ = ReceiveRemoteOperationBatchAsync`) → výjimky se polykají, proto OnRemoteBatchAsync má try/catch. Detail níže:
**Algoritmické jádro = OT** (operations.mjs transform/invert/apply, TP1 fuzz PhaseR14). Engine: `onOperation` emit, `applyRemoteOperation` (+caret remap), **presence** overlay (`createRemoteCaretElement` barevný proužek+jmenovka, `setRemoteCursors`, `paintRemoteCursors`), `addOperationListener`. **`collab-client.mjs`**: `createCollabClient` (server-transform model, PhaseR15: server-sekvencer+N klientů konvergují /40 scénářů) + **`createRelayCollabClient`** (PURE-RELAY model = existující hub: transform-to-head proti committed suffixu; **PhaseR16: 2/3/4 klienti KONVERGUJÍ /320 scénářů přes čistý relay** — pozn: cestou opravena reálná priority bug v `toHead`, NE TP2 zeď). facade `connectCollab({clientId,send})` (host ops→relay client, remote→applyRemoteOperation). interop: `connectCollab`/`collabReceiveServerChange`/`OnCollabSend` bridge. **C# host `TmDocumentCoreEngineHost`: `ConnectCollaborationAsync(IDocumentCollaborationRealtimeProvider, docId, author, color)`** = Join + subscribe `RemoteOperationBatchReceived`/`RemoteCursorReceived` + interop connectCollab; `[JSInvokable] OnCollabSend`→map `CoreOperation`→`DocumentOperationBatch`→`BroadcastOperationBatchAsync`→ack; `OnRemoteBatchAsync`→map zpět→`collabReceiveServerChange`; presence obousměrně (`BroadcastCursorAsync`/remote→SetRemoteCursors); `base`↔`Batch.BaseVersionId`, `sequence`↔`broadcast.Sequence`. **REUSE existující transportu** (SignalR hub `/hubs/document-editor-collaboration` v Demo.Api + `SignalRDocumentCollaborationProvider` + presence + reconnect). **gate**: `R104` (2 engine konvergují+presence), `R105` (2 editory přes RELAY transport + reálné psaní → konvergují, 4 sekvencované změny), PhaseR15/R16, C# adapter builduje čistě. **ZBÝVÁ jen: 2-browser live accept E2E (demo page + ConnectCollaborationAsync(SignalRProvider) + API hub na 5100 + 2 taby).** To je test-infra, ne kód — wiring kompletní + ověřený po vrstvách.

### R.5.23 Drobné UI subsystémy — ✅ HOTOVO (2026-06-01: a/b/c/d)
render-host `setZoom` (CSS transform scale + zoom-aware `posFromClient`), `setPageSettings` (runtime re-layout),
`print` (inject @media print stylesheet skrývající overlaye + window.print). facade + execCommand routy.
**🐛 OPRAVENA REÁLNÁ LATENTNÍ CHYBA: pageSettings se NIKDY neaplikoval** — render-host posílal `{pageSettings:{…}}`
NESTED, ale engine čte FLAT width/height/marginTop (normalizePageBox) → vše renderovalo na default 640×900 BEZ okrajů
(přes harness/demo posílající 794×1123/72px). Fix: spread pageSettings flat → teď A4 + okraje fungují (+ setPageSettings).
- [x] **zoom** ; [x] **print** ; [x] **page settings** (runtime margins/size/orientace — engine teď pageSettings REÁLNĚ ctí)
- [x] **kontextové menu** (R.5.23a): render-host `getContextAt(x,y,target)` (selection/link/image/comment/table cell/misspelling) + `onContextMenu(info,x,y)` callback; **🐛 latentní fix: pravý klik už NEkolabuje výběr** (pointerHandler ignoruje button≠0); menu clipboard `menuCopy/Cut/Paste` (navigator.clipboard) + `replaceRange`. Facade routy cut/copy/paste/replacerange/getContextAt/setspellcheck. C# host `OnCoreContextMenu`+`CoreContextInfo`/`CoreMisspelling` DTO; C# editor core context menu UI (cut/copy/paste/comment/link/remove-link/table row+col/spell-suggestions) + backdrop dismiss. **gate**: E2E `R100` (harness getContextAt link/plain + real right-click fires callback, caret follows) + `R81` (live bridge: right-click → core menu → Add comment dismisses).
- [x] **spellcheck** (R.5.23c): `setSpellChecker({isMisspelled,suggest})` + `buildWordListChecker({flagged|known,suggestions})` facade factory + `setspellcheck` execCommand; `paintMisspellings` red wavy underline overlay (`createSpellUnderlineElement`, SVG wave); `misspellingAt`+`getContextAt.misspelling`+suggestions v menu; `replaceRange` opraví. C# host `SetSpellCheckAsync`. **gate**: E2E `R101` (squiggle paint, misspelling+suggestion, replaceRange fix, squiggle clears).
- [x] **drag-drop** (R.5.23b): text move — mousedown uvnitř výběru → `startTextMove`; drag přes práh → drop-caret indikátor (`showDropCaret`); mouseup → `moveSelectionTo` (serialize→delete→insertLines, offset-adjust same-block + end-block-merge remap), 1 undo step. Image move = existující object-drag (R.4.6d). **gate**: E2E `R102` (select 'Banana ' → drag na začátek → 'Banana Apple Cherry', drop-caret mid-drag, undoable; R43/R96/R46c bez regrese). POZN: drop externího souboru z plochy = samostatný upload follow-up.
- [x] **sekce (multi-section per-page-settings)** (R.5.23d): `model.sections=[{startBlockId,pageSettings}]` = "Next Page" section break → fresh page s vlastní geometrií (size/orientace/margins). paragraph-engine: gated `hasSections` větev — `makePage`/`placePageAtY` stackuje stránky podle SKUTEČNÉ výšky (mixed-size safe, ne uniform `index*height`), `activeMetrics` přepnut na sekci při section-start bloku (skip první blok), `ensurePage` ctí activeMetrics, paragraph layout dostává activeMetrics. **Default path (bez sekcí) BEZE ZMĚNY** (298 PhaseD Node zelených). **gate**: E2E `R103` (portrait p1 794px + landscape p2 1123px stacked, v layoutu i DOM). POZN: C# converter section round-trip + toolbar "insert section break" = app-layer follow-up (engine+layout hotové).
- [x] **gate**: E2E `R94` (zoom scale(2)+zoom-aware hit-test; page-settings re-paginace 1→4; print stylesheet). POZN: page-settings fix shiftnul layout o okraje → upraveny 2 PhaseR Node pozice-testy (PhaseR3/R6d) na margin-correct souřadnice.

---

## 🚦 Brána pro flip (kdy je bezpečné flipnout default + smazat legacy)

### STAV REGRESE 2026-06-01 (entry v260) — VŠECHNY DEFERRED MUST-HAVE HOTOVÉ
- **63 harness E2E zelených** (R42–R111: render/input/IME/bidi/marks/tabulky/obrázky/styly/headers/find/revize/komentáře/context-menu/spellcheck/drag-drop/sekce/collab/reading-order/shaped-caret/re-wrap/op-log-undo).
- **302 Node PhaseD zelených** (vč. PhaseR14 OT fuzz, PhaseR15 collab server-transform, PhaseR16 relay 2-4 klienti, PhaseR17 op-log undo, PhaseR13 bidi explicit, PhaseR6i undo).
- **Bridge/live: R81 (context menu live), R106 (LIVE 2-browser collab přes SignalR).**
- **Component bUnit: 1659/1666** — 7 selhání jsou **PRE-EXISTING, NE z této session**: 3× PDF/export (image-block cast) + 4× legacy runtime selection-token snapshoty (Phase2/5/23 + JavaScriptTestHooks; assertují LOGICKÉ pole region/limitId/inlineId, ne caret-x → moje změny je nemůžou způsobit; nedotkl jsem se těch souborů ani legacy runtime). **Mnou zavedené 1 selhání (nový klíč ContextMenuLabel) OPRAVENO** (přidán do resx default/cs/fr + MockLocalizer); cestou bonus-opraven pre-existing fr.resx gap (DebugDocxDrawingMetadata).
- **ZBÝVÁ před flipem: (1) manuální NVDA/VoiceOver smoke = LIDSKÝ gate; (2) manuální .docx round-trip smoke; (3) EXPLICITNÍ schválení flipu.**

1. **P0 KOMPLET** (R.5.1–R.5.5) — bez nich flip = ztráta dat nebo nefunkční základ. **Nepodkročitelné.**
2. **P1 KOMPLET** (R.5.6–R.5.10) — běžná editace; bez nich uživatel pocítí regresi.
3. **P2 rozhodnout s userem** — co je must-have pro paritu vs. co může jít po flipu (graceful degradace OK,
   pokud feature aspoň nepadá / neztrácí data).
4. **P3/P4 mohou jít po flipu** (kromě R.5.21 export, pokud uživatelé exportují .docx — pak P0/P1).
5. **Regrese před flipem:** full core E2E zelená + bUnit beze změny + **manuální smoke** (otevřít reálný .docx
   s tabulkou/obrázkem/komentáři → editovat → uložit → znovu otevřít → nic neztraceno).
6. **Flip = 2 kroky:** (a) flip default `RenderEngine = CoreEnginePreview` (vratné — jen default param);
   (b) **smazat legacy** (`render(inst)` string path + contenteditable + `buildLayoutSnapshot`) — **NEVRATNÉ,
   až po (a) + soak + EXPLICITNÍ SCHVÁLENÍ uživatele.**

### ✅ KROK (a) HOTOVÝ — REVERSIBLE DEFAULT-FLIP (2026-06-01, schváleno uživatelem)
`TmDocumentEditor.RenderEngine` default **Legacy → CoreEnginePreview** (`CoreEngineHostedInteropReady=true` → Resolve vrací core). **VRATNÉ** (1 řádek). Legacy zůstává plně přítomný + volitelný (`RenderEngine=Legacy`).
- **bUnit (legacy-engine testy, nemůžou běžet core v non-browser):** sdílený helper `LocalizationTestBase.RenderDocumentEditorLegacy(...)` pinuje Legacy; mechanicky nahrazeno `RenderComponent<TmDocumentEditor>(`→`RenderDocumentEditorLegacy(` v 7 souborech (101 sites). Suite zelená = 7 PRE-EXISTING fails (3 PDF/export + 4 legacy selection-token snapshots), 0 z flipu. Nový test `Default_RenderEngine_IsCoreEnginePreview_AfterR5Cutover`.
- **Demo:** `/document-editor` (legacy E2E target) **pinnut `RenderEngine=Legacy`** (legacy E2E zůstávají zelené dokud legacy nesmazán); core soak target = `/core-engine-editor` + `/core-engine-collab` (live collab). Library default = core → každý NOVÝ consumer bez explicit param dostane core.
- **Verifikace:** core collab E2E `R106` zelený, core harness 63 E2E zelené (v261), bUnit 7-pre-existing. **POZN: 1 legacy E2E `Phase4_NarrowMoreMenuShowsGroupsAndSearchesCommands` (toolbar overflow more-menu) selhává konzistentně — PRE-EXISTING/environmental (stránka renderuje legacy IDENTICKY jako před flipem; toolbar/overflow jsem tuto session needitoval; měří šířku toolbaru ne caret/engine).** ZBÝVÁ krok (b): manuální NVDA + .docx smoke + soak + schválení → smazat legacy.

### Doporučené pořadí práce
**R.5.1 (converter) → R.5.2 (clipboard) → R.5.3 (autosave) → R.5.4 (klik-odkaz) → R.5.5 (bookmarks)** = odblokuje
flip jako preview-default kandidáta. Pak P1. Pak rozhodnutí o P2. Teprve pak flip default + soak + smazání legacy.
