# TmDocumentEditor Developer Guide

Tento dokument shrnuje integrační body, které vznikly během CKEditor-inspired redesignu `TmDocumentEditor`. Praktické demo je na route `/document-editor`; ukazuje toolbar režimy, feature toggles, image provider, table properties, comments/review, paste report a autosave error scénář.

## Feature Registry

Editor startuje s `DocumentEditorBuiltInFeatures.CreateDefaultRegistry()`. Feature implementuje `IDocumentEditorFeature` a má stabilní `Name`, volitelné `Requires` a registrační metody pro commandy, toolbar, shortcuty, floating UI a schema hooky.

Host aplikace může pro konkrétní instanci vypnout features přes `DisabledFeatures`. Vestavěné názvy jsou v `DocumentEditorFeatureNames`, například `image`, `table`, `comments` a `trackChanges`. Vypnutí feature má skrýt její toolbar položky a zablokovat související editor flow, ne pouze změnit vzhled tlačítka.

## Command Registry

Commandy žijí v `DocumentEditorCommandRegistry` a mají metadata pro label, tooltip, kategorii, shortcut, ikonu, viditelnost a disabled reason. Všechny cesty spuštění, tedy toolbar, command palette, keyboard shortcuty, overflow menu a contextual UI, mají používat stejný registry-backed stav.

Nový command má mít stabilní název, lokalizované texty v resources, guard přes `computeEnabled` nebo force-disable reason a test, který ověří, že disabled command nejde spustit ani mimo viditelné UI.

## Toolbar Modes

`ToolbarMode` podporuje `Ribbon`, `Compact` a `DistractionFree`. Ribbon je výchozí Word-like režim. Compact zachovává command coverage, ale preferuje icon-only controls s aria labely. Distraction-free toolbar skryje a ponechá plochu editoru jako primární UI.

Demo route obsahuje `document-editor-toolbar-mode`, aby host aplikace viděl runtime přepnutí bez reloadu dokumentu.

## Clipboard Pipeline Extension Point

Paste flow prochází přes `DocumentClipboardPipeline` a normalizéry pro Word, Google Docs, Google Sheets, raw HTML a URL. Normalizér vrací bloky, warnings a debug snapshot. Warnings se uživateli zobrazují přes `TmDocumentPasteReport`.

Při přidávání normalizéru preferuj schema-aware výstup, zachovej undo jako jednu transakci a přidej paste fixture do `tests/Tempo.Blazor.Tests/Fixtures/DocumentEditor/Clipboard`.

## Image Provider UX

`ImageProvider` řeší upload a clipboard image flow. `ImageUrlResolver` řeší render provider-managed assetů. Když `ImageProvider` není nastavený, upload volba musí být disabled a paste report má vysvětlit, že obrázek nebylo možné uložit.

Demo route má toggle `document-editor-image-provider-enabled`, který ukazuje rozdíl mezi URL insertion a provider-backed uploadem.

## Table Properties Model

Tabulky používají `TableBlockContent`, `TableLayoutContent`, `TableRowContent` a `TableCellContent`. Table-level nastavení zahrnuje šířku, zarovnání, default padding, background a borders. Cell-level nastavení zahrnuje header flag, spans, merge metadata, vertical alignment, padding, background a borders.

Floating table toolbar otevírá `TmDocumentTablePropertiesPanel` a `TmDocumentCellPropertiesPanel`. Demo dokument `table-demo` obsahuje předpřipravenou tabulku pro manuální kontrolu těchto panelů.

## Autosave And Pending Actions

Autosave řídí `DocumentAutosaveStateMachine` a pending stav se promítá do status baru. UI musí rozlišit waiting, saving, saved, recoverable error a retry. `DocumentPendingActionService` drží další blokující operace, například image upload.

Demo toggle `document-editor-autosave-error` přepne provider do recoverable failure režimu a zkrátí autosave interval, aby šlo chování rychle ověřit.

## Watchdog Recovery

JS-owned WYSIWYG runtime má recovery/watchdog flow pro výpadky runtime bridge. Komponenta má při runtime problému zobrazit fallback místo prázdné plochy a debug modal má obsahovat recovery detail pro diagnostiku.

Při úpravách JS runtime vždy spusť `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` a cílené runtime testy.

## Accessibility Expectations

Toolbar, command palette, grid picker, table/image panels, paste report a live region musí být ovladatelné klávesnicí. Dialogové plochy mají mít role/aria modal tam, kde drží focus. Statusy save/autosave/find/paste mají být oznamované přes live region bez rušivého focus skoku.

Compact toolbar nesmí přijít o aria labely jen proto, že skrývá text. Overflow menu musí mít menu role, aktivní položku a šipkovou navigaci.

## Demo Route Checklist

Route `/document-editor` má pro knihovní uživatele pokrývat:

- toolbar mode switch,
- feature toggles pro images, tables a review,
- image provider enabled/disabled stav,
- table properties sample document,
- comments and review sample data,
- paste report sample HTML,
- autosave error provider toggle.
