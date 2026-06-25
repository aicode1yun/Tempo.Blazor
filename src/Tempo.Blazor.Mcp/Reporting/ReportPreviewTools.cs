using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Pdf;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>MCP preview rendering tools for report definitions.</summary>
[McpServerToolType]
public static class ReportPreviewTools
{
    [McpServerTool(Name = "render_report_preview")]
    [Description("Render a stored report or supplied definitionJson to a PNG preview. Returns contentType=image/png, base64, page dimensions and pageCount. Use after validate_report succeeds.")]
    public static async Task<string> RenderReportPreview(
        IReportDefinitionStore store,
        IReportDataProvider dataProvider,
        ITextMeasurer textMeasurer,
        ReportPdfRenderer renderer,
        [Description("Stored report id. Required when definitionJson is omitted.")] string? reportId = null,
        [Description("Optional full report definition JSON to render without storing.")] string? definitionJson = null,
        [Description("Optional JSON object of parameter values. Values may be scalars, arrays, or { scalarValue } / { values }.")] string? parametersJson = null,
        [Description("Tenant id. Defaults to northwind.")] string? tenantId = null,
        [Description("User id. Defaults to mcp-agent.")] string? userId = null,
        [Description("Culture name. Defaults to en-US.")] string? cultureName = null,
        [Description("1-based page number to render.")] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        ReportDefinition definition;
        var context = ReportMcpHelpers.Context(tenantId, userId, cultureName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(definitionJson))
        {
            if (!ReportValidationEngine.TryDeserialize(definitionJson, out var parsed, out var parseError) || parsed is null)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, parseError ?? "Report definition JSON could not be parsed.");
            }

            definition = parsed;
        }
        else if (!string.IsNullOrWhiteSpace(reportId))
        {
            var (_, _, loaded) = await ReportMcpHelpers.LoadLatestAsync(store, reportId, context).ConfigureAwait(false);
            if (loaded is null)
            {
                return McpToolResults.Failure(McpToolResults.NotFound, $"Report '{reportId}' not found.");
            }

            definition = loaded;
        }
        else
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "render_report_preview requires either reportId or definitionJson.");
        }

        var validation = ReportValidationEngine.Validate(definition);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The report definition is invalid; preview was not rendered.", validation.Errors);
        }

        IReadOnlyDictionary<string, ReportParameterValue> suppliedParameters;
        try
        {
            suppliedParameters = ReportMcpHelpers.ParseParameters(parametersJson);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"parametersJson could not be parsed: {ex.Message}");
        }

        try
        {
            var snapshot = await RenderSnapshotAsync(
                definition,
                dataProvider,
                textMeasurer,
                suppliedParameters,
                context,
                cancellationToken).ConfigureAwait(false);
            if (snapshot.Pages.Count == 0)
            {
                return McpToolResults.Failure(McpToolResults.Error, "The report rendered no pages.");
            }

            var pageIndex = Math.Clamp(pageNumber, 1, snapshot.Pages.Count) - 1;
            var page = snapshot.Pages[pageIndex];
            var png = renderer.RenderPagePng(page);
            return McpToolResults.Success(new
            {
                reportId = definition.Id,
                pageNumber = page.PageNumber,
                pageCount = snapshot.Pages.Count,
                width = page.Width,
                height = page.Height,
                contentType = "image/png",
                base64 = Convert.ToBase64String(png),
            });
        }
        catch (Exception ex) when (ex is ReportProcessingException or ReportDataProviderException or InvalidOperationException)
        {
            return McpToolResults.Failure(McpToolResults.Error, ex.Message);
        }
    }

    private static async Task<Tempo.Reporting.Engine.Snapshot.ReportSnapshot> RenderSnapshotAsync(
        ReportDefinition definition,
        IReportDataProvider dataProvider,
        ITextMeasurer textMeasurer,
        IReadOnlyDictionary<string, ReportParameterValue> suppliedParameters,
        Tempo.Reporting.Abstractions.ReportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var resolution = await ReportParameterProcessor.ResolveAsync(
            definition,
            dataProvider,
            suppliedParameters,
            context).ConfigureAwait(false);
        var dataSets = new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal);
        foreach (var dataSet in definition.DataSets)
        {
            var result = await dataProvider.GetDataAsync(
                dataSet.Name,
                new ReportDataQuery
                {
                    SourceName = dataSet.Source?.Name,
                    Text = dataSet.Query,
                },
                resolution.Values,
                context).ConfigureAwait(false);
            dataSets[dataSet.Name] = await ReportDataSetRuntime.LoadAsync(
                dataSet.Name,
                result,
                cancellationToken).ConfigureAwait(false);
        }

        var processingContext = new ReportProcessingContext(context, resolution.Values, dataSets);
        var primary = dataSets.Values.FirstOrDefault()
            ?? new ProcessedDataSet(
                "Empty",
                [],
                [new ProcessedDataRow(new Dictionary<string, object?>(StringComparer.Ordinal))]);
        var instance = ReportBandInstantiator.Instantiate(definition, primary, processingContext);
        return ReportSnapshotGenerator.Generate(
            instance,
            textMeasurer,
            new ReportSnapshotGeneratorOptions
            {
                SnapshotId = string.IsNullOrWhiteSpace(definition.Id) ? "mcp-report-preview" : definition.Id,
            });
    }
}
