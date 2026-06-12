# Canvas engine - E10: Autocorrect, autoformat, format painter a symboly (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E10** · Stav: hotovo · Priorita: P2 (nad rámec legacy)

## Proč

Format painter, autocorrect/autoformat-as-you-type a vkládání symbolů/speciálních znaků jsou každodenní Word/GDocs funkce. Legacy je neměl. Navazuje na commandy (9), edit pipeline (8) a clipboard paste options (11).

## Cílový stav

- Format painter: copy formatting (char + para), one-shot i lock; engine-level, undoable.
- Autocorrect: replace-as-you-type tabulka, smart quotes, auto-capitalize, ordinal/fraction.
- Autoformat: auto-bullet/auto-number, auto-hyperlink, horizontal line; autoreplace → undo vrací původní.
- Insert symbol / special character paleta, emoji picker, non-breaking space, optional hyphen, em/en dash.
- Vše přes dispatcher, undoable (automatické náhrady jako 1 undo krok).

## Clean-room
- [x] Vlastní; bez ONLYOFFICE kódu.

## Znovupoužití
- [x] Dispatcher (Faze 9); edit-model (Faze 8); paste options (Faze 11).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/format-painter.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/autocorrect.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/insert-symbol.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/input/__tests__/autocorrect.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/__tests__/format-painter.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasAutocorrectE2ETests.cs
```

## DoD
- [x] Autocorrect náhrada = 1 undo krok (undo vrací původní text).
- [x] Format painter přenese char + para formátování.

## Faze E10.1: Format painter

### E10.1.1 RED
- [x] `format-painter.test.mjs`: copy formatting (char + para) z výběru; apply na jiný rozsah; one-shot i lock; undoable.

### E10.1.2 GREEN + screenshot + akceptace
- [x] `format-painter.mjs` (formatting delta); E2E přenést styl z jednoho slova na jiné.

## Faze E10.2: Autocorrect

### E10.2.1 RED
- [x] `autocorrect.test.mjs`: replace-as-you-type tabulka; smart quotes (kontext); auto-capitalize; ordinal/fraction; undo vrací původní.

### E10.2.2 GREEN + akceptace
- [x] `autocorrect.mjs` v input pipeline; toggle options.

## Faze E10.3: Autoformat-as-you-type

### E10.3.1 RED
- [x] Auto-bullet/auto-number (`1.` + space); auto-hyperlink (URL + space); horizontal line (`---`); každá náhrada undoable jako 1 krok.

### E10.3.2 GREEN + screenshot + akceptace
- [x] Autoformat pravidla; E2E `--` → em dash, `1.` → seznam.

## Faze E10.4: Symbol/special char/emoji

### E10.4.1 RED
- [x] `insert-symbol`: engine command, emoji payload, non-breaking space, optional hyphen, em/en dash na caret.
- [x] Blazor symbol paleta a emoji picker UI.

### E10.4.2 GREEN + screenshot + akceptace fáze E10
- [x] `insert-symbol.mjs`; E2E vložit symbol; format painter; autocorrect; undo gate.
- [x] Paleta (Blazor) pro výběr symbolů.

## Poznámky
- Math autocorrect (linear → struktura) je v E8.7, ne tady.
- Autocorrect options dialog jako Blazor komponenta; default sada zapnutá.
- 2026-06-06: Doplněna lokalizovaná Blazor symbol/emoji paleta v Insert ribbonu, napojení na canvas `insertSymbol` commandy, scoped CSS, registry metadata a E2E screenshot ověření. Při E2E byly zároveň zpevněny undo/redo history snapshoty pro autocorrect, inline/paragraph state a format painter state.
