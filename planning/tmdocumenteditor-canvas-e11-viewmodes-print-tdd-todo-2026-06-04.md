# Canvas engine - E11: View modes, zoom a print/print preview (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E11** · Stav: hotovo · Priorita: P2 (nad rámec legacy)

## Proč

View modes (print/reading/web/outline), zoom presety a print/print preview jsou standard Word/GDocs/OnlyOffice. Legacy měl jen základní zoom. Navazuje na render pipeline (5) a pagination (6).

## Cílový stav

- View modes: print layout (default), reading mode, web layout, outline view; přepínání bez ztráty stavu.
- Zoom presety: fit page, fit width, multiple pages, custom percent; Ctrl+wheel / pinch.
- Print preview render z aktuálního canvas modelu; print dialog + print to PDF přes provider.
- Reading mode: stránky/sloupce optimalizované na čtení, skrytý toolbar, navigace.

## Clean-room
- [x] Vlastní; ONLYOFFICE `Layout/ReadView`/`PrintView` jen koncept.

## Znovupoužití
- [x] Render pipeline (Faze 5) DPR/zoom transform; pagination (Faze 6).
- [x] PdfExportProvider (Faze 19) pro print-to-PDF napojení.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/view/view-modes.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/view/zoom-controller.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/view/print-preview.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/view/__tests__/zoom.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/view/__tests__/view-modes.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasViewModesPrintE2ETests.cs
```

## DoD
- [x] Print layout vs reading mode vs fit-width screenshoty; print preview ne blank.
- [x] Zoom ostrý na ověřených úrovních (100 %, fit-width/custom, Ctrl+wheel; pixel snapping).

## Faze E11.1: Zoom presety

### E11.1.1 RED
- [x] `zoom.test.mjs`: fit page, fit width, multiple pages, custom percent; Ctrl+wheel/pinch; transform + pixel snapping.

### E11.1.2 GREEN + screenshot + akceptace
- [x] `zoom-controller.mjs`; E2E fit-width + custom zoom, ostrost.

## Faze E11.2: View modes

### E11.2.1 RED
- [x] `view-modes.test.mjs`: print layout/reading/web/outline geometrie; přepnutí zachová caret/scroll stav.

### E11.2.2 GREEN + screenshot + akceptace
- [x] `view-modes.mjs`; reading mode (skrytý toolbar, čtecí geometrie); E2E přepnout mody.

## Faze E11.3: Print + print preview

### E11.3.1 RED
- [x] `print-preview`: render z aktuálního modelu; print dialog; ne blank.
- [x] `print-preview`: print to PDF přes provider.

### E11.3.2 GREEN + screenshot + akceptace fáze E11
- [x] `print-preview.mjs`; E2E print preview + fit-width; reading mode; screenshoty.
- [x] UX review: print layout vs reading mode profesionální.

## Implementační poznámky 2026-06-05
- Hotovo: clean-room `view/view-modes.mjs`, `view/zoom-controller.mjs`, `view/print-preview.mjs`, dispatcher view commands, canvas zoom škálování včetně hit metadata, Ctrl+wheel command flow, reading mode skrytí toolbaru v Blazor shellu, print preview snapshot z aktuálního display listu a browser print request.
- Ověřeno: `zoom.test.mjs`, `view-modes.test.mjs`, dispatcher/paragraph regresní JS testy, build E2E projektu a `DocumentEditorCanvasViewModesPrintE2ETests`.

## Implementační poznámky 2026-06-06
- Hotovo: View ribbon má canvas příkazy `openPrintPreview` a `printDocument`, print-preview action bar nabízí browser print i export PDF přes existující `CanvasExportBridge` a `IDocumentPdfExportProvider`.
- Hotovo: doplněná lokalizace EN/CS/FR, scoped action-bar CSS a registry metadata pro View toolbar commandy.
- Opraveno: změna zoom/backing-store velikosti canvasu vynutí repaint stránky, takže fit-width a print preview nemohou skončit s průhledným content layerem po tile-cache hitu.
- Ověřeno: `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore`, `DocumentEditorToolbarDeclarativeMigrationTests`, `entry.test.mjs`, `zoom.test.mjs`, `view-modes.test.mjs` a E2E `DocumentEditorCanvasViewModesPrintE2ETests` včetně screenshotů, browser print stubu a staženého PDF přes provider.

## Poznámky
- Outline view editace (drag headings) jako pokročilejší; basic outline = Faze 18 panel.
- Multiple pages view reuse virtualizace (Faze 22).
