# Canvas engine - E4: Styly - management a typy stylů (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E4** · Stav: hotovo · Priorita: P2 (nad rámec legacy)

## Proč

Plný style management (paragraph/character/table/list styly, based-on inheritance, galerie, modify/create/delete, update from selection) je jádro Word/GDocs/OnlyOffice produktivity. Legacy měl jen heading picker. Sjednocuje styl resolver z Faze 10.

## Cílový stav

- Style store: paragraph/character/table/list styly; id, name, based-on, next, primary/quick.
- Style resolver: inheritance (based-on chain) + direct formatting override delta.
- Style gallery / quick styles; apply/modify/create/delete/rename; update style from selection; default formatting reset.
- Heading 1–6 / Normal / Quote napojené na reálné styly (sjednotit s Faze 10).
- Změna stylu přepočítá všechny odstavce, které ho používají (recalc-info invalidace).

## Clean-room
- [x] Style store/resolver vlastní; ONLYOFFICE `Styles.js` jen koncept (based-on/next/type).

## Znovupoužití
- [x] `core-engine/paragraph-styles.mjs` (Faze 10 heading styly) → rozšířit na plný store. Hotovo v canvas `commands/heading-style.mjs` + `styles/style-store.mjs`.
- [x] Recalc-info invalidace (Faze 22); converter (Faze 4) pro style id/name.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/styles/style-store.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/styles/style-resolver.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/styles/__tests__/inheritance.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/styles/__tests__/update-propagation.test.mjs
src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasStylePane.razor
tests/Tempo.Blazor.E2E/DocumentEditorCanvasStylesE2ETests.cs
```

## DoD
- [x] Based-on inheritance + update propagation testované.
- [x] Round-trip style id/name; undo gate.
- [x] Modify style změní celý dokument.

## Faze E4.1: Style store + typy

### E4.1.1 RED
- [x] `style-store`: paragraph/character/table/list styly; id/name/based-on/next/type; default styly.

### E4.1.2 GREEN + akceptace
- [x] `style-store.mjs`; converter round-trip.

## Faze E4.2: Resolver (inheritance + direct override)

### E4.2.1 RED
- [x] `inheritance.test.mjs`: based-on chain skládá formátování; direct override delta nad stylem; resolved hodnoty.

### E4.2.2 GREEN + akceptace
- [x] `style-resolver.mjs`; cyklus detection; přístup pro toolbar mixed/active.

## Faze E4.3: Gallery + apply/modify/create/delete

### E4.3.1 RED
- [x] Apply style; modify (změní definici); create from selection; delete/rename; default formatting reset.

### E4.3.2 GREEN + screenshot + akceptace
- [x] Style pane (Blazor); commandy; E2E apply + create. Implementováno jako scoped toolbar style gallery.

## Faze E4.4: Update propagation + heading sjednocení

### E4.4.1 RED
- [x] `update-propagation.test.mjs`: modify "Heading 1" přepočítá všechny H1; Heading 1–6/Normal/Quote napojené na store (sjednotit Faze 10).

### E4.4.2 GREEN + screenshot + akceptace fáze E4
- [x] Recalc-info invalidace stylem; E2E modify Heading 1 → všechny nadpisy se změní; save/reload; undo gate.
- [x] Screenshot: galerie stylů + propagace.

## Poznámky
- Table styly (banded) detailně v E12; tady jen table style jako entita.
- DOCX styles.xml roundtrip = Faze 19.
