# Canvas engine - Faze 19: Import/export a externí formáty (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 19** · Stav: implementováno pro DOCX/ODT/PDF/HTML/Markdown/compare/debug včetně rozšířeného DOCX E-fáze smoke balíku · Priorita: P1

## Proč

Export/import přes existující provider boundary z aktuálního živého canvas modelu: DOCX, ODT, PDF, Markdown/HTML, compare. Bez toho je editor uzavřený. Sbírá DOCX smoke gates roztroušené v feature fázích (tabulky/obrázky/numbering/styly/fields/sekce/SDT/math).

## Cílový stav

- Export živého canvas modelu přes provider boundary (aktuální neuložené edity).
- DOCX export/import (mapuje do canvas modelu, renderuje okamžitě).
- ODT export/import kde provider podporuje.
- PDF export přes provider z aktuálního modelu.
- Markdown/HTML export/import kde providery exponují.
- Compare documents používá aktuální model; debug JSON reflektuje aktuální model.

## Clean-room
- [x] Mapování formátů vlastní; ONLYOFFICE serializery jen koncept.

## Znovupoužití
- [x] `DocumentSerializer` (C#); PdfExportProvider, FormatProvider, ComparisonProvider; converter (Faze 4).
- [x] C# `TmDocumentCompareDialog`, `TmDocumentDiffViewer`, `TmDocumentJsonDebugModal`, `TmDocumentPasteReport`.

## Doporučené nové soubory

```text
src/Tempo.Blazor/Components/DocumentEditor/CanvasExportBridge.cs
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/Export/CanvasExportTests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasImportExportE2ETests.cs
```

## DoD
- [x] DOCX smoke: text, table, image, header/footer, comment/revision anchors přežijí.
- [x] Imported document má sane first paint, žádné blank pages.

## Faze 19.1: Export živého modelu

### 19.1.1 RED
- [x] Export tahá aktuální canvas model (vč. neuložených editů); debug JSON reflektuje aktuální model.

### 19.1.2 GREEN + akceptace
- [x] `CanvasExportBridge` (RequestDocumentAsync → snapshot → provider); debug JSON modal.

## Faze 19.2: DOCX export/import

### 19.2.1 RED
- [x] DOCX export z aktuálních editů; import mapuje do canvas modelu a renderuje okamžitě; smoke: text/table/image/header-footer/comment/revision.

### 19.2.2 GREEN + screenshot + akceptace
- [x] DOCX bridge; E2E export → import → struktury přežijí; screenshot sane first paint.

## Faze 19.3: PDF + ODT + Markdown/HTML

### 19.3.1 RED
- [x] PDF export přes provider z modelu.
- [x] ODT export/import přes provider. _(Doplněno přes `DocumentFormatProviderKind.Odt`, Demo API provider a reálný `DocumentOdtExporter`/`DocumentOdtImporter`.)_
- [x] Markdown/HTML export/import kde provider podporuje. _(Doplněno přes `DocumentFormatProviderKind.Html`/`Markdown`, Demo API provider, `DocumentHtmlExporter`/`DocumentHtmlImporter`, `DocumentMarkdownExporter` a nový `DocumentMarkdownImporter`.)_

### 19.3.2 GREEN + screenshot + akceptace
- [x] E2E PDF export smoke.
- [x] Provider bridge pro každý formát. _(DOCX/ODT/HTML/Markdown přes `IDocumentFormatProvider`, PDF přes `IDocumentPdfExportProvider`; E2E ověřuje export/import přes reálný Demo API provider.)_

## Faze 19.4: Compare documents

### 19.4.1 RED
- [x] Compare používá aktuální model; diff viewer.

### 19.4.2 GREEN + screenshot + akceptace fáze 19
- [x] ComparisonProvider bridge.
- [x] E2E compare smoke.
- [x] DOCX smoke pro text/table/image/header-footer/comment/revision anchors.
- [x] DOCX smoke vč. numbering/styly/fields/sekce/SDT/math anchors z E-fází.

## Poznámky
- Round-trip přesnost je iterativní; smoke gates definují minimum, plná věrnost = follow-up per formát.
- Tvar `IDocumentFormatProvider` boundary zůstává stejný; rozšířené jsou hodnoty `DocumentFormatProviderKind` pro ODT/HTML/Markdown.
