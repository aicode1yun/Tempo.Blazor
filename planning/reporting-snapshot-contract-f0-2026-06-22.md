# Tempo Reporting F0 - analýza snapshot kontraktu

Datum: 2026-06-22

## Kontext

F0 má ověřit největší riziko reportingu: jestli serverový C# engine dokáže připravit textové metriky tak, aby je browser canvas viewer vykreslil ve stejné šířce. Reporting proto nepřebírá layout document editoru. Reuse je jen na úrovni principu: stabilní snapshot/display-list kontrakt a nízkoúrovňový canvas painter.

## Co konzumuje document editor canvas painter

Relevantní moduly:

- `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/display-list.mjs`
- `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/canvas-renderer.mjs`
- `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/layers.mjs`
- sdílené měření/font helpery v `src/Tempo.Blazor/wwwroot/js/document-editor/layout/font-metrics.mjs`

Document editor display-list je objekt se `schemaVersion`, `pages`, globálním polem `commands`, statistikami a layout metadaty. Commandy nesou absolutní souřadnice v CSS px, `pageIndex`, `layer`, `sequence` a typ specifický pro painter. Textové commandy (`textRun`, `field`, `listLabel`, `lineNumber`) používají `x`, `y`, `baseline`, `width`, `height`, `text` a `style`.

Painter vybírá canvas vrstvu podle `command.layer` (`page-background`, `content`, `objects`, `selection-caret`, `annotations`, `diagnostics`). Text kreslí přes `fontStringFromStyle`, `paintAdvancedText`, `letterSpacing` a `characterScale`. Document editor si také drží text rect metadata, která E2E testy používají přes `data-canvas-text-rect` pro kontrolu překryvů.

## Reporting snapshot F0 schéma

F0 reporting snapshot je záměrně menší než document editor display-list:

- `schemaVersion`: aktuálně `1`
- `snapshotId`: stabilní identifikátor renderu
- `pages[]`: stránky s `pageNumber`, `width`, `height`
- `commands[]` na stránce, v pořadí malování
- typy commandů F0: `textRun`, `rectangle`, `line`, `path`, `image`, `clipPush`, `clipPop`

Souřadnice jsou absolutní CSS px při 96 DPI. Engine vlastní layout; JS painter pouze maluje. `textRun` vždy nese `text`, `x`, `baseline`, `width`, `height`, `fontFamily`, `fontSize`, `fontWeight`, `fontStyle`, `letterSpacing` a `fill`. Viewer nesmí text přelamovat ani dopočítávat layout. Pokud browser natural width neodpovídá snapshot width, painter používá run-width scaling (`scaleX = width / measureText(...)`), aby vizuální šířka běhu odpovídala C# snapshotu.

## Rozhodnutí F0

- Reporting má vlastní `Tempo.Reporting.Engine.Snapshot` model a JSON serializer s odmítnutím nepodporované verze.
- F0 painter je izolovaný `reporting-painter.mjs` + bundle `reporting-painter.bundle.js`; nepřebírá document editor layout, ale kopíruje ověřený command/painter styl.
- Harness je statická stránka `src/Tempo.Blazor.Demo/wwwroot/reporting-harness.html`, která přijme snapshot JSON, načte webfonty a vykreslí jednu stránku na canvas.
- Font metriky jsou v engine: TTF reader (`hmtx`, `kern`, `hdmx`), binární serializer v2 a `TableTextMeasurer`.
- E2E fidelity gate doplňuje glyph-level hinted kalibraci z browseru pro použité F0 webfont/size kombinace. Celý běh se pak skládá v C# z hinted advance table a ověřuje proti `canvas.measureText` s tolerancí <= 0,5 %. Tím F0 zachycuje praktický rozdíl mezi čistým `hmtx` a tím, co Chromium pro webfont skutečně měří.

## Otevřené navazující body

- O2 produkční font sada zůstává otevřená pro další fáze. F0 používá lokálně dostupné OFL/free fonty a prokazuje mechanismus tabulek, ne finální dodávaný font balíček.
- Pro post-MVP komplexní shaping/RTL zůstává vhodný HarfBuzzSharp nebo ekvivalentní server-side shaping pipeline. F0 řeší latinku, diakritiku, řečtinu/azbuku a CJK bez RTL.
- F9 může přesunout reporting viewer do samostatného `Tempo.Blazor.Reporting` RCL. F0 zatím drží harness a bundle v existující demo/component infrastruktuře.
