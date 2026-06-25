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
