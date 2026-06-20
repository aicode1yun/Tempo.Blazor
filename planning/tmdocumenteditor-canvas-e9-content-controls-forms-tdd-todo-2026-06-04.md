# Canvas engine - E9: Content controls / strukturované tagy / formuláře (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E9** · Stav: první produkční řez hotový · Priorita: P2 (nad rámec legacy)

## Proč

Content controls (structured document tags) a vyplnitelné formuláře (text/combo/drop-down/date/checkbox/picture) jsou standard Word/OnlyOffice forms. Legacy je neměl (jen tokens/mentions). Velká subdoména.

## Cílový stav

- SDT model: block i inline; plain text, rich text, combo box, drop-down, date picker, checkbox, picture, repeating section.
- Placeholder text, tag/alias, lock (content/delete), required/format mask.
- Render na canvasu (border/highlight v design modu, plain v form modu), forms-fill mode.
- Interakce: tab mezi poli, edit text, combo/drop-down výběr, date picker, checkbox toggle, picture insert.

## Clean-room
- [x] Vlastní; ONLYOFFICE `StructuredDocumentTags/*` jen koncept (SdtPr/typy).

## Znovupoužití
- [x] Inline objekt/atomic content pattern (tokens/mentions); text editing context (Faze 8); image insert (Faze 15) pro picture form.
  - [x] První řez znovupoužil atomic inline run pattern a canvas command/history pipeline; picture control má model/display metadata, bez picture insert UI.
  - [x] Picture form picker používá reálné `ImageAssetOptions` i assety aktuálního dokumentu bez duplicit a zapisuje vybraný asset přes produkční canvas command runtime.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/controls/sdt-model.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/controls/sdt-render.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/controls/forms-mode.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/controls/__tests__/sdt-model.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/controls/__tests__/forms-fill.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasFormsE2ETests.cs
```

## DoD
- [x] SDT serialize round-trip; value get/set; lock enforcement.
- [x] Save/reload formulář; undo gate.

## Faze E9.1: SDT model + typy

### E9.1.1 RED
- [x] `sdt-model.test.mjs`: block/inline SDT; plain/rich text, combo, drop-down, date, checkbox, picture, repeating; placeholder/tag/alias/lock/mask.

### E9.1.2 GREEN + akceptace
- [x] `sdt-model.mjs`; converter round-trip; lock enforcement (content/delete).

## Faze E9.2: Render (design vs form mode)

### E9.2.1 RED
- [x] `sdt-render`: design mode border/highlight + tag; form mode plain; placeholder když prázdné.
  - [x] Display-list test pokrývá form-control metadata, validaci, placeholder/display text a canvas render příkaz.
  - [x] `sdt-render.test.mjs` pokrývá oddělení form fill režimu a design chrome/tag režimu v produkčním render-state modulu.

### E9.2.2 GREEN + screenshot + akceptace
- [x] `sdt-render.mjs`; E2E render formuláře v obou módech.
  - [x] První řez renderuje form-control boxy přímo přes display-list/canvas-renderer a DOM metadata layer; E2E screenshot pokrývá form mode.
  - [x] Canvas render mode je napojený přes `ContentControlRenderMode`/`contentControlMode`; E2E pořizuje form-mode screenshot `00-advanced-controls-before.png` a design-mode screenshot `01-advanced-controls-design-mode.png`.

## Faze E9.3: Forms-fill interakce

### E9.3.1 RED
- [x] `forms-fill.test.mjs`: tab mezi poli; edit text; combo/drop-down výběr; date picker; checkbox toggle; picture insert; required validace.
  - [x] `forms-mode.test.mjs`: edit text, drop-down výběr, checkbox toggle, required validace, lock enforcement, undo/redo a synchronizace `body.blocks`/`sections[].blocks`.
  - [x] `forms-mode.test.mjs`: command navigace mezi poli, date value command, picture asset command, combo text command a repeating-section add/remove včetně undo historie.
  - [x] `forms-fill.test.mjs` pokrývá keyboard field navigation, plain text, checkbox, drop-down, date, combo, picture asset, required/unknown-option validaci a locked edit blokaci.

### E9.3.2 GREEN + screenshot + akceptace fáze E9
- [x] `forms-mode.mjs`; field nav + value set; E2E vyplnit text+checkbox+drop-down ve forms mode; save/reload; undo gate.
  - [x] První řez: `forms-mode.mjs` s undoable příkazy `setContentControlText`, `toggleContentControl`, `selectContentControlOption`, validací a E2E fill/save/reload/undo gate.
  - [x] Field navigace přes canvas command runtime: `focusContentControl`, `nextContentControl`, `previousContentControl` bez undo zápisu.
  - [x] Date value command: `setContentControlDate` + E2E screenshot/save/reload.
  - [x] Picture asset command: `setContentControlPicture` + E2E screenshot/save/reload.
  - [x] Combo text command: `setContentControlComboText` + E2E screenshot/save/reload.
  - [x] Repeating-section runtime operace: `addRepeatingSectionItem`, `removeRepeatingSectionItem`, undo a save/reload.
  - [x] Accessibility mirror renderuje nested repeating-section bloky ve skutečném čtecím pořadí.
  - [x] Fyzická klávesová Tab integrace do hidden-input handleru: `Tab`/`Shift+Tab` routuje přes `nextContentControl`/`previousContentControl`, jen při skutečné změně selection, s fallbackem na tabulky/listy/textový tabulátor.
  - [x] Plné Blazor popover UI pro date picker / picture picker / combo editor.

## Poznámky
- DOCX SDT roundtrip = Faze 19 smoke.
- Repeating section a complex form (více polí) jako pokročilejší pod-část; basic typy stačí na paritu.
- Format mask (telefon/datum) = follow-up detail.
- 2026-06-06: Doplněny advanced content-control runtime příkazy, demo seed pro date/combo/picture/repeating sekci a E2E `PhaseE9_AdvancedControlsNavigateRepeatSaveReloadAndScreenshot` se screenshoty před vyplněním, po vyplnění a po reloadu.
- 2026-06-06: Doplněna produkční keyboard-only Tab navigace přes hidden input bridge; unit test pokrývá prioritu content-control navigace před list indentem a E2E přidává reálný `Keyboard.PressAsync("Tab")`/`Shift+Tab` scénář se screenshotem `01-advanced-controls-keyboard-tab.png`.
- 2026-06-06: Doplněn Blazor popover pro aktivní date/drop-down/combo/picture content control. Popover je lokalizovaný, scoped CSS, používá reálné assety z komponenty i dokumentu a E2E ověřuje `02-advanced-controls-popover.png`, save/reload i vizuální nonblank/overlap kontroly.
- 2026-06-06: Doplněn `sdt-render.mjs`, runtime `ContentControlRenderMode` a `contentControlMode` demo query. Canvas renderer maluje form mode jako plain fill text a design mode s border/highlight/tag chrome; E2E advanced scénář ověřuje oba módy screenshoty.
