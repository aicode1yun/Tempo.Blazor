# Canvas engine - Faze 8: Input pipeline, IME a immediate typing (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 8** · Stav: hotovo · Priorita: P0

## Proč

Psaní musí být okamžité a stabilní bez contenteditable autority. Skrytý input bridge přijímá klávesnici/IME, edit operace mutují model, aktivní řádek se překreslí do ~16 ms. Toto je most mezi vstupem (Faze 7 selection) a commandy (Faze 9).

## Cílový stav

- Skrytý input bridge (off-screen textarea) přijímá text/klávesnici; žádné contenteditable DOM mutace.
- Insert text na collapsed caret; replace selection by typing.
- Enter = split odstavce; Shift+Enter = soft break.
- Backspace/Delete uvnitř runu i přes hranice runů/bloků (merge).
- IME composition preview (žije v modelu) + commit; emoji/grapheme safe offsety.
- Immediate repaint aktivní řádky do 16 ms target.

## Clean-room
- [x] Vlastní input pipeline; bez ONLYOFFICE kódu.

## Znovupoužití
- [x] `core-engine/input-surface.mjs` (skrytá textarea: Enter/Backspace/Delete/šipky v keydown, text/paste v beforeinput, IME commit na compositionend; Apple quirky: keyCode 229 guard, lazy-start, onInput ignoruje composition inputType).
- [x] `core-engine/edit-model.mjs` (insertText/deleteBackward+merge/deleteForward+merge/insertParagraph split; reuse insert-text-run/run-mutators).
- [x] `selection-overlay.createCompositionUnderlineElement` + `edit-model.applyReplaceRange` pro IME.
- [x] Surface mountovaný MIMO canvas (jinak repaint odpojí input).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/input-controller.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/__tests__/insert-delete.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/__tests__/ime.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasTypingE2ETests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasImeE2ETests.cs
```

## DoD
- [x] E2E real keyboard (ne JS API): typing, Enter, Shift+Enter, backspace.
- [x] Text se objeví přesně u caretu; okolní layout se neposkočí nesmyslně.
- [x] Immediate path měřený (aktivní řádek repaint < 16 ms target).

## Faze 8.1: Hidden input bridge

### 8.1.1 RED
- [x] `input-controller`: off-screen surface přijme keydown/beforeinput; contentEditableCount = 0 (žádný contenteditable nikde).

### 8.1.2 GREEN + akceptace
- [x] Reuse input-surface; mount mimo canvas; fokus management (s mousedown+preventDefault z Faze 7).

## Faze 8.2: Insert text + replace selection

### 8.2.1 RED
- [x] `insert-delete.test.mjs`: psaní na collapsed caret vloží text; psaní přes selection nahradí.

### 8.2.2 GREEN + screenshot + akceptace
- [x] Reuse edit-model.insertText + applyReplaceRange; immediate repaint aktivní řádky; E2E napsat "Hello".

## Faze 8.3: Enter / Shift+Enter

### 8.3.1 RED
- [x] Enter rozdělí odstavec na caretu (2 bloky); Shift+Enter vloží soft break v rámci odstavce.

### 8.3.2 GREEN + screenshot + akceptace
- [x] Reuse insertParagraph split + soft break; E2E "Hello"+Enter+"World" → 2 bloky.

## Faze 8.4: Backspace / Delete (run + hranice bloků)

### 8.4.1 RED
- [x] Backspace/Delete uvnitř runu; na hranici runů; na hranici bloků (merge odstavců); grapheme-safe (emoji = 1 krok).

### 8.4.2 GREEN + akceptace
- [x] Reuse deleteBackward/deleteForward + merge; surrogate/grapheme offsety.

## Faze 8.5: IME composition

### 8.5.1 RED
- [x] `ime.test.mjs`: compositionStart/Update/End; preview text žije v modelu (reálný layout), update nahrazuje span, end = 1 edit, prázdná data = zrušení; pre-edit underline.

### 8.5.2 GREEN + screenshot + akceptace
- [x] Reuse composition pipeline; E2E `Hi`→`Hiか`→`Hiかん`→`Hi感`→commit `Hi感じ` + underline.

## Faze 8.6: Immediate typing performance

### 8.6.1 RED
- [x] Měření: aktivní řádek repaint < 16 ms; zbytek dokumentu se nepřekresluje při psaní (jen dirty region).

### 8.6.2 GREEN + akceptace fáze 8
- [x] Immediate path (dirty odstavec) vs idle reconciliation (Faze 22); E2E screenshot: text u caretu, layout bez nesmyslného posunu.

## Implementační evidence

Faze 8 je napojena přes `input/input-controller.mjs`, `input/text-editing.mjs` a existující hidden textarea bridge. Runtime routuje `beforeinput`, `keydown`, `input` fallback a composition events do canvas modelu, zachovává selection/caret, publikuje diagnostiku posledního inputu a nepoužívá contenteditable autoritu. Text editace je čistá modelová vrstva: insert/replace, paragraph split, soft break, grapheme-safe Backspace/Delete, merge přes hranice bloků a IME preview/commit/cancel. Layout nově garantuje caret stop pro prázdný odstavec po Enteru a pro blok končící soft breakem, aby okamžité psaní nespadlo na první blok. Input commit používá dirty-block incremental repaint; strukturální změny překreslují stránky od první dirty stránky dál.

E2E seed `phase-8-canvas-typing-ime` je v demo provideru. Screenshot evidence:

- `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase8-typing/2026-06-04/desktop-1440x1000/`
- `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase8-ime/2026-06-04/desktop-1440x1000/`

Test evidence:

- `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/pagination.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/__tests__/insert-delete.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/__tests__/ime.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/__tests__/hit-test.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/__tests__/caret-move.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/selection/__tests__/selection-range.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/__tests__/display-list.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/__tests__/renderer.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs` - 25/25.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -v:minimal` - zelený build.
- `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore -v:minimal` - zelený build se stávajícím `NU1603` warningem.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasTypingE2ETests|FullyQualifiedName~DocumentEditorCanvasImeE2ETests" --no-restore --no-build -v:minimal` - 2/2.

## Poznámky
- Routování přes edit-model přímé mutace; plné napojení na history/operations transakce = Faze 12 (undo coalescing). Tady minimální undo hook.
- Paste je v beforeinput jen jako plain text fallback; plný clipboard = Faze 11.
- Composition + bidi: ověřit RTL composition (follow-up).
