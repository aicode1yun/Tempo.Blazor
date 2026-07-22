# Tempo.Reporting.Abstractions

Contracts and JSON models for Tempo Reporting. This package has no Blazor or ASP.NET dependency,
so API projects, workers, MCP hosts, and shared DTO libraries can reference it without pulling UI
assets.

## What It Contains

- Report definition records: page setup, bands, parameters, data sets, styles, and polymorphic
  report elements.
- Canonical JSON serialization through `ReportDefinitionJsonSerializer`.
- FluentValidation validators for report definitions and parameters.
- Report execution context and data-provider contracts.
- In-memory definition store for demos, tests, and lightweight MCP hosts.
- Report Server DTOs shared by remote clients and server endpoints.

## Report Definition JSON

`ReportDefinitionJsonSerializer` uses camelCase JSON, string enums, and the polymorphic element
discriminator property `type`.

```json
{
  "schemaVersion": 1,
  "id": "sales-summary",
  "name": "Sales summary",
  "pageSetup": {
    "pageSize": { "width": 595.28, "height": 841.89, "unit": "point" },
    "orientation": "portrait",
    "margins": { "left": 36, "top": 36, "right": 36, "bottom": 36 }
  },
  "dataSets": [
    {
      "name": "Orders",
      "source": { "name": "northwind" },
      "query": "orders/recent",
      "fields": [
        { "name": "Customer", "dataType": "string" },
        { "name": "Total", "dataType": "number" }
      ]
    }
  ],
  "bands": {
    "detail": {
      "kind": "detail",
      "height": 28,
      "elements": [
        {
          "type": "textBox",
          "id": "customer",
          "x": 0,
          "y": 0,
          "width": 220,
          "height": 20,
          "expression": "=Fields.Customer"
        }
      ]
    }
  }
}
```

The package includes `docs/report-definition.schema.json` in the NuGet package. Use it for AI
authoring guidance, editor validation, and contract documentation. Runtime validation should still
go through `ReportDefinitionValidator`, because it can apply semantic rules that JSON Schema cannot
express well.

## Element Discriminators

The current schema version (`ReportDefinition.CurrentSchemaVersion == 1`) supports:

- `textBox`
- `image`
- `shape`
- `line`
- `table`
- `chart`
- `subReport`

## RDL Import

`RdlReportImporter` (namespace `Tempo.Reporting.Abstractions.Definitions.Rdl`) maps SSRS / Telerik **RDL**
XML onto `ReportDefinition`. Elements are matched by **local name**, so the SSRS 2008 / 2010 / 2016 schema
namespaces and Telerik-namespaced RDL all parse without a hardcoded namespace.

```csharp
var result = new RdlReportImporter().Import(rdlXml);   // also accepts a Stream
if (result.HasErrors) { /* result.Errors -> block; nothing is persisted */ }
var json = ReportDefinitionJsonSerializer.Serialize(result.Definition);
```

The body is resolved as a direct `Report/Body` (RDL 2008) **or** `Report/ReportSections/ReportSection/Body`
(RDL 2010/2016, the shape Report Builder emits). Multi-section reports import the first section and say so.

Mapped: report name/description, page size and margins (`in`/`cm`/`mm`/`pt`/`pc`/`px` converted to points),
`ReportParameters`, `DataSources`/`DataSets` (query, command text, fields, query-parameter bindings),
and the body report items `Textbox`, `Tablix`/`Table`, `Chart` (including the `Stacked` subtypes),
`Image`, `Rectangle` and `Line`.

**Nothing is dropped silently.** Every construct outside the supported subset produces an
`RdlImportDiagnostic`: subreports, matrices, lists, gauges, maps, custom report items, custom code and
`CodeModules`, embedded image bytes, page headers/footers, report variables, custom properties, additional
report sections, per-item `Visibility`/`Action`, tablix row **and column** grouping, unmapped chart types
(imported as a column chart), data-set `Filters`/`SortExpressions`, calculated-field expressions, and
data-set-backed parameter defaults/valid values.

The importer never invents content: an RDL textbox with no value is **skipped with a diagnostic** rather
than backfilled with its designer-generated name. Where the definition model is stricter than RDL the
importer repairs and reports — a hidden parameter with no default gets a null default and is marked
optional, and a multi-value parameter is coerced to `List`. RDL `Nullable` is not the same as "optional",
so that mapping is diagnosed too.

Malformed XML or a non-`Report` root returns an `Error` diagnostic rather than throwing. Full RDL fidelity
is out of scope — imported reports should be reviewed in the designer.

## Minimal Host Contract

```csharp
public sealed class OrdersDataProvider : IReportDataProvider
{
    public Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context)
    {
        // Return declared fields plus streamed rows for the requested data set.
    }
}
```

Use `ReportExecutionContext` to carry tenant, user, culture, and cancellation information across
data providers, validators, engine processing, and server endpoints.
