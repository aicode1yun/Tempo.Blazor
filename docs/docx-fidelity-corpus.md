# DOCX fidelity — third-party regression corpus (Phase 9)

`tests/Tempo.Blazor.DocumentFormats.Tests/DocumentDocxThirdPartyCorpusTests.cs` holds a corpus of
DOCX packages built with **raw OpenXml** (never our exporter) that mimic what real producers emit.
Each sample asserts (a) what the first import must preserve and (b) import → export → re-import
structural stability (block order, types and text verbatim).

| Sample | Mimics | What it pins |
| --- | --- | --- |
| `BuildWordStyleContract` | Word | Heading style chain (`Heading1` basedOn `Normal` + outline level), bold/italic run properties, merged table header (`gridSpan=2`) + shading + borders, sectPr page size/margins, Czech diacritics. |
| `BuildLibreOfficeStyleMemo` | LibreOffice | `w:jc val="both"` (justified), identically-formatted text fragmented into multiple runs (incl. empty `rPr` and `szCs`-only run properties), `w:tab` characters. |
| `BuildCourtFiling` | Czech court filing (podání) | `w:lnNumType` line numbering (countBy=1, restart=newPage, distance 360 twips → 18 pt), header part with the `č.l.` case-file margin note, justified numbered points, `Sp. zn.` reference. |

## Deviations found (issues per odchylka)

| # | Deviation | Status |
| --- | --- | --- |
| 1 | **Direct paragraph formatting (`w:pPr`) was dropped on import** — `w:jc` (alignment incl. justified legal text), `w:spacing` (before/after/line) and `w:ind` (left/right/firstLine/hanging) never reached `DocumentBlock.ParagraphProperties`, although the exporter writes them. Any Word/LibreOffice document lost its justification and indents when opened in the editor. | **Fixed in Phase 9** — `DocumentDocxImporter.ReadParagraphFormatting` mirrors the exporter's `AppendParagraphFormatting` (jc → Alignment incl. `both`/`distribute` → Justify; spacing twips → points, `line`+`auto` → LineSpacing/240; indents twips → points, hanging → negative FirstLineIndent). Covered by the LibreOffice corpus test; full DocumentFormats suite 312/312. |

No other deviations remain open — the Word contract, LibreOffice memo and court filing samples
import and round-trip cleanly (headings, marks, merged tables, tabs, fragmented runs, line
numbering, headers, diacritics). New deviations discovered by future corpus additions belong in
this table; unresolved ones must also be reported via `plan_report_remaining`.

## Legal filing rendering (line numbering + č.l.)

`layout/line-numbering.mjs` is verified by `layout/__tests__/line-numbering.test.mjs` (per-page /
per-section / continuous restarts, increments — e.g. every 5th line, left-margin placement at
`distanceFromText`, disabled → no labels) and end-to-end by
`DocumentEditorLegalFilingE2ETests` on the Czech filing seed
`phase-9-canvas-legal-filing` (`canvas-engine-host?documentId=phase-9-canvas-legal-filing`):
per-page line numbers in the left margin (`data-canvas-line-number-count`), the `č.l.` header
asserted via the print layout snapshot, and the edge case that ordinary documents paint zero
line-number labels. Screenshot: `__screenshots__/document-editor-legal-filing/`.
