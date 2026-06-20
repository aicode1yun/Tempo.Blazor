# Canvas engine - Faze 16: Headers, footers, fields, notes a page settings (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 16** · Stav: dokončeno a ověřeno E2E + screenshot testy · Priorita: P1

## Proč

Hlavičky/patičky per stránka s editací, pole (page number/count/X of Y/date/title/author), poznámky pod čarou/koncové a nastavení stránky (margins/size/orientation). Pole se v E5 rozšíří na field engine + cross-ref; tady základní sada.

## Cílový stav

- Header/footer render per page; click-to-edit; different first page; different odd/even.
- Pole: page number, page count, page X of Y, date, document title, author.
- Footnote/endnote vložení a render; numbering settings.
- Page margins, size, orientation; page break behavior.
- Section-like geometrie kde model podporuje (plně E3).

## Clean-room
- [x] Header/footer/notes layout vlastní; ONLYOFFICE jen inspirace.

## Znovupoužití
- [ ] `core-engine/header-footer.mjs`; field runs z model converteru (Faze 4.5).
- [x] Page-metrics (Faze 6) pro geometrii; converter footnotes/endnotes (Faze 4.5).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/header-footer-layout.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/notes-layout.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/fields.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/header-footer.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/notes.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasHeaderFooterE2ETests.cs
```

## DoD
- [x] Header/footer editace jasná; page geometry profesionální.
- [x] Save/reload header/footer/notes/fields; undo gate.

## Faze 16.1: Header/footer render + scopes

### 16.1.1 RED
- [x] `header-footer.test.mjs`: header/footer render v margin area per page; default/first/even scope; correct page placement.

### 16.1.2 GREEN + screenshot + akceptace
- [x] `header-footer-layout.mjs`; E2E render hlavičky/patičky.

## Faze 16.2: Click-to-edit + different first/odd-even

### 16.2.1 RED
- [x] Double-click do header area vstoupí do editace; close header/footer; different first page; different odd/even přepínače.

### 16.2.2 GREEN + screenshot + akceptace
- [x] Edit context pro header/footer (reuse text editing); E2E edit header.

## Faze 16.3: Pole (page number atd.)

### 16.3.1 RED
- [x] `fields`: page number, page count, page X of Y, date, document title, author vloží field run; render aktuální hodnoty po paginaci.

### 16.3.2 GREEN + screenshot + akceptace
- [x] `fields.mjs` (základní); E2E vložit page number do hlavičky.

## Faze 16.4: Footnotes/endnotes

### 16.4.1 RED
- [x] `notes.test.mjs`: footnote insert přidá referenci + note area na stránce; endnote na konci; numbering settings; render.

### 16.4.2 GREEN + screenshot + akceptace
- [x] `notes-layout.mjs`; E2E vložit footnote/endnote.

## Faze 16.5: Page settings

### 16.5.1 RED
- [x] Page margins/size/orientation mění geometrii; page break behavior.

### 16.5.2 GREEN + screenshot + akceptace fáze 16
- [x] Page setup commandy; reflow; save/reload; E2E screenshot „page geometry profesionální".

## Poznámky
- Field engine (instrText/cached result/update), cross-reference, captions, bibliografie, STYLEREF = E5.
- Sekce, sloupce, per-section page setup, line numbering = E3.
- 2026-06-05 ověřeno:
  - `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/header-footer.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/notes.test.mjs` - 4/4 pass.
  - `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - pass.
  - `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasHeadersFootersNotesE2ETests" --logger "console;verbosity=normal"` - pass.
  - Screenshoty: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase16-headers-footers-notes/2026-06-04/desktop-1440x1000/`.
- 2026-06-06 dořešeno a ověřeno:
  - `fields.mjs` doplněn o produkční `insertPageBreak` command, registraci v dispatcheru a section-level `noteNumbering` v `setPageSettings`.
  - `nextNoteMarker` respektuje style/startAt/restartEachSection; ověřeno lower-roman markerem `v` po existující poznámce.
  - `header-footer.test.mjs` ověřuje explicitní page break jako tvrdou paginační hranici a undo page setupu včetně note numbering.
  - `notes.test.mjs` ověřuje numbering settings, vloženou referenci i render markeru/body v note regionu.
  - `DocumentEditorCanvasHeadersFootersNotesE2ETests` ověřuje double-click editaci hlavičky, zavření klikem do body textu, different-first-page a odd/even toggles off/on, vložení page number fieldu, footnote/endnote, page break, landscape page setup, save/reload persistenci a screenshoty.
  - Testy: `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/header-footer.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/notes.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/pagination.test.mjs` - 10/10 pass.
  - Testy: `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - pass.
  - Testy: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore --filter "FullyQualifiedName~DocumentEditorCanvasHeadersFootersNotesE2ETests" --logger "console;verbosity=normal"` - 1/1 pass.
  - Screenshoty: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase16-headers-footers-notes/2026-06-04/desktop-1440x1000/00-phase16-headers-footers-notes-before.png`, `01-phase16-header-editing.png`, `02-phase16-commands-page-setup.png`, `03-phase16-headers-footers-notes-after-save.png`, `04-phase16-headers-footers-notes-after-reload.png`.
