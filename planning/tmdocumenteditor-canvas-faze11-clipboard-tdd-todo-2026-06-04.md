# Canvas engine - Faze 11: Clipboard (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 11** · Stav: hotovo · Priorita: P1

## Proč

Copy/cut/paste s interním fragmentem, plain text a normalizací rich textu z Wordu/Google Docs. Bez kvalitního paste vypadá import rozbitě. Navazuje na edit pipeline (8) a commandy (9).

## Cílový stav

- Copy výběru jako plain/html/interní model fragment.
- Cut = jedna undo transakce.
- Paste interní fragment (plná věrnost); paste plain text; paste rich z Word/GDocs (normalizátory).
- Paste URL → link policy; paste image → image provider flow (kde provider povolí).
- Clipboard debug modal funguje.

## Clean-room
- [x] Normalizátory vlastní; bez ONLYOFFICE kódu.

## Znovupoužití
- [x] Canvas clipboard controller postavený na model-owned edit pipeline a history store.
- [x] Image provider boundary a `TmDocumentClipboardHtmlDebugModal`.
- [x] edit-model replaceRange pro cut a vlastní fragment insert pro paste.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/clipboard/clipboard-controller.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/clipboard/html-normalizer.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/clipboard/__tests__/copy-paste.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/clipboard/__tests__/html-normalizer.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasClipboardE2ETests.cs
```

## DoD
- [x] E2E real clipboard/DataTransfer: copy, cut, paste, paste plain.
- [x] Pasted rich text vypadá konzistentně (ne rozbitý HTML).
- [x] Undo gate pro cut i paste.

## Faze 11.1: Copy/cut interní + plain/html

### 11.1.1 RED
- [x] `copy-paste.test.mjs`: copy výběru produkuje plain text, html i interní fragment; cut = 1 undo transakce.

### 11.1.2 GREEN + akceptace
- [x] Canvas clipboard controller; cut přes jednu undo transakci v history store.

## Faze 11.2: Paste interní fragment + plain

### 11.2.1 RED
- [x] Paste interní fragment zachová marks/strukturu; paste plain vloží čistý text na caret/přes selection.

### 11.2.2 GREEN + screenshot + akceptace
- [x] Fragment paste insert; E2E copy→paste, paste plain.

## Faze 11.3: Rich paste normalizace (Word/GDocs)

### 11.3.1 RED
- [x] `html-normalizer.test.mjs`: Word/GDocs HTML → canvas model (odstavce, nadpisy, seznamy, bold/italic/color, tabulky, odkazy); odstranění junk stylů/span wrapperů.

### 11.3.2 GREEN + screenshot + akceptace
- [x] Normalizátor; E2E paste rich → konzistentní render.

## Faze 11.4: Paste URL a image

### 11.4.1 RED
- [x] Paste URL → link policy (auto-link nebo plain); paste image → image provider flow (kde povoleno).

### 11.4.2 GREEN + akceptace fáze 11
- [x] URL/link policy; image paste přes provider; clipboard debug modal funguje; undo gate.

## Poznámky
- Paste options pill (keep formatting / merge / text only) = E10.
- Image paste závisí na Faze 15 image flow.
- Bezpečnost: sanitizace HTML při normalizaci (žádné script/handler atributy).
