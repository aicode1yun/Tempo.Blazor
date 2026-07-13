# Placeholder Documentation — Remaining Fill Plan

## What a "placeholder" is

A placeholder doc file is a per-item source-overlay JSON (under `JsonDocumentation/`)
whose top-level `description` is still the generator's auto fallback text rather than a
human sentence. Two fallback shapes exist:

- Components: `"<Name> Blazor component in the <Category> category."`
- Public API types: `"Public <kind> defined by <PackageId>."`

Most of these files already have good `parameters` / `members` — only the top-level
`description` needs a human rewrite. `documentationStatus` stays `"generated"` (siblings use it);
the placeholder is the description text, not the status field.

### Detect them

```bash
# component placeholders
grep -rlE '"description": "[^"]+ Blazor component in the [^"]+ category\.\"' JsonDocumentation --include=*.json
# public-type placeholders
grep -rlE '"description": "Public (class|record|interface|struct|enum|recordstruct) defined by' JsonDocumentation --include=*.json
```

Progress can be re-measured any time with
`dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate --fail-on-drift`
(drift = new public types with **no** overlay at all — a stricter problem than a placeholder
description; keep drift at 0).

## Tier 1 — Component placeholders (primary backlog): 173 remaining

Already done in this pass: the 14 `Tempo.Blazor` (CORE) component placeholders.
Remaining, grouped by package (recommended order: smallest first to lock in the pattern,
NotionEditor last as the bulk):

| Order | Package | Count | Overlay root |
|------:|---------|------:|--------------|
| 1 | Tempo.Blazor.Wireframe | 1 | `Packages/Tempo.Blazor.Wireframe/items` |
| 2 | Tempo.Blazor.Spreadsheet | 2 | `Packages/Tempo.Blazor.Spreadsheet/items` |
| 3 | Tempo.Blazor.DiagramEditor | 7 | `Packages/Tempo.Blazor.DiagramEditor/items` |
| 4 | Tempo.Blazor.Signing | 13 | `Packages/Tempo.Blazor.Signing/items` |
| 5 | Tempo.Blazor.EmailTemplates | 15 | `Packages/Tempo.Blazor.EmailTemplates/items` |
| 6 | Tempo.Blazor.DocumentEditor | 34 | `Packages/Tempo.Blazor.DocumentEditor/items` |
| 7 | Tempo.Blazor.NotionEditor | 101 | `Packages/Tempo.Blazor.NotionEditor/items` |
| | **Total** | **173** | |

### Approach per file

1. Open the overlay's `sourcePath` `.razor` (and any `<Name>*.cs` code-behind) for real intent.
2. Replace **only** the top-level `description` with 1-3 plain sentences: what the component
   renders and its key features. Write prose — do not paste `<see cref>` markup.
3. Leave `parameters`, `members`, `kind`, `namespace`, etc. untouched (already generated/merged).
4. Do not add fabricated parameters; the generator re-extracts `[Parameter]` props from source
   on `generate` and merges them.

## Tier 2 — Public-type placeholders (optional deeper pass): ~251 files

These overlays exist (so they are **not** validation drift) but carry the
`"Public <kind> defined by <PackageId>."` fallback description. They are concentrated in the
abstractions/model packages. Approximate counts by package:

| Package | Count |
|---------|------:|
| Tempo.Blazor.Abstractions | ~224 |
| Tempo.Blazor.NotionEditor | ~10 |
| Tempo.Blazor.DocumentEditor | ~4 |
| Tempo.Blazor.Mcp | ~3 |
| Tempo.Blazor.DiagramEditor | ~2 |
| Tempo.Blazor (CORE) | 6 (4 Enum + 2 Class) |
| others (EmailTemplates.Abstractions, Reporting.Abstractions) | ~2 |

Recommended: do the CORE 6 first (small, high-visibility), then batch
`Tempo.Blazor.Abstractions` by folder/category (Models, Diagram, Document-Editor, …),
reading each type's `.cs` summary. Same rule: rewrite only the `description`
(and add `members` for small DTOs where it clarifies the shape).

## Guardrails

- Never edit component / `.cs` / `.razor` / `.css` / `.resx` source — only `JsonDocumentation/**` overlays.
- After a batch: run `... generate` then `... validate --fail-on-drift`; it must print
  `Validation passed.` The `generate` step rewrites the root aggregate outputs
  (`tempo-blazor*.json`, `tempo-blazor-all.json`) — that is expected.
