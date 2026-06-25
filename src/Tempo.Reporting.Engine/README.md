# Tempo.Reporting.Engine

Processing, layout, rendering, and export primitives for Tempo Reporting. This package consumes
`Tempo.Reporting.Abstractions` definitions and produces stable report snapshots, PDF/PNG output,
CSV, and XLSX files.

## Pipeline

1. Resolve parameter defaults and available values with `ReportParameterProcessor`.
2. Load data sets through an `IReportDataProvider`.
3. Instantiate bands with `ReportBandInstantiator`.
4. Generate pages and drawing commands with `ReportSnapshotGenerator`.
5. Render or export the result with `ReportPdfRenderer`, `ReportCsvExporter`, or
   `ReportXlsxExporter`.

```csharp
var context = new ReportExecutionContext("northwind", "user-1", "en-US");
var parameters = await ReportParameterProcessor.ResolveAsync(
    definition,
    dataProvider,
    suppliedParameters,
    context);

var data = await dataProvider.GetDataAsync(
    "Orders",
    new ReportDataQuery { SourceName = "northwind", Text = "orders/recent" },
    parameters.Values,
    context);

var rows = await ReportDataSetRuntime.LoadAsync("Orders", data, context.CancellationToken);
var processing = new ReportProcessingContext(
    context,
    parameters.Values,
    new Dictionary<string, ProcessedDataSet> { ["Orders"] = rows });

var instance = ReportBandInstantiator.Instantiate(definition, rows, processing);
var snapshot = ReportSnapshotGenerator.Generate(instance, textMeasurer);
var pdf = new ReportPdfRenderer().Render(snapshot);
```

## Rendering Notes

- PDF and PNG rendering use SkiaSharp.
- Text layout is driven by `ITextMeasurer`, so hosts can plug in production font metrics or a
  deterministic test measurer.
- Snapshot JSON can be serialized with `ReportSnapshotJsonSerializer` for audit, caching, and
  regression baselines.
- Tabular exports read table/chart-friendly data from the processed report model and preserve basic
  number/date formatting.

## Expressions

The expression engine supports report-safe access to fields, parameters, and aggregate helpers used
during processing and layout. Expressions use the `=` prefix in definitions, for example
`=Fields.Total` or `=Parameters.Region`.
