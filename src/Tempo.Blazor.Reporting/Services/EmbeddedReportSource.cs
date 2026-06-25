using System.Globalization;
using Tempo.Blazor.Reporting.Models;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Export;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Pdf;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Blazor.Reporting.Services;

/// <summary>Runs a report definition directly in the current Blazor render mode.</summary>
public sealed class EmbeddedReportSource : IReportSource
{
    private readonly ReportDefinition _definition;
    private readonly IReportDataProvider _dataProvider;
    private readonly ITextMeasurer _textMeasurer;
    private readonly ReportPdfRenderer _pdfRenderer;
    private readonly ReportSnapshotGeneratorOptions _snapshotOptions;

    /// <summary>Creates an embedded report source.</summary>
    public EmbeddedReportSource(
        ReportDefinition definition,
        IReportDataProvider dataProvider,
        ITextMeasurer? textMeasurer = null,
        ReportPdfRenderer? pdfRenderer = null,
        ReportSnapshotGeneratorOptions? snapshotOptions = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _textMeasurer = textMeasurer ?? new DefaultReportViewerTextMeasurer();
        _pdfRenderer = pdfRenderer ?? new ReportPdfRenderer();
        _snapshotOptions = snapshotOptions ?? new ReportSnapshotGeneratorOptions
        {
            SnapshotId = string.IsNullOrWhiteSpace(definition.Id) ? "embedded-report" : definition.Id,
        };
    }

    /// <inheritdoc />
    public async Task<ReportViewerMetadata> GetMetadataAsync(
        ReportViewerMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executionContext = CreateExecutionContext(request, cancellationToken);
        var parameters = new List<ReportViewerParameterMetadata>();
        foreach (var parameter in _definition.Parameters)
        {
            var options = await ResolveOptionsAsync(
                parameter,
                request.Parameters,
                executionContext,
                cancellationToken).ConfigureAwait(false);
            parameters.Add(new ReportViewerParameterMetadata(parameter, options)
            {
                IsCascading = parameter.AvailableValues?.Kind == ReportParameterAvailableValuesKind.DataSet,
            });
        }

        return new ReportViewerMetadata
        {
            ReportId = _definition.Id,
            Title = string.IsNullOrWhiteSpace(_definition.Name) ? _definition.Id : _definition.Name,
            Parameters = parameters,
        };
    }

    /// <inheritdoc />
    public async Task<ReportViewerRenderResult> RenderAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var processing = await ProcessAsync(request, cancellationToken).ConfigureAwait(false);
        var primaryDataSet = ResolvePrimaryDataSet(processing.Context.DataSets);
        var instance = ReportBandInstantiator.Instantiate(_definition, primaryDataSet, processing.Context);
        var snapshot = ReportSnapshotGenerator.Generate(instance, _textMeasurer, _snapshotOptions);
        var metadata = await GetMetadataAsync(
            new ReportViewerMetadataRequest
            {
                CultureName = request.CultureName,
                TenantId = request.TenantId,
                UserId = request.UserId,
                Parameters = processing.Context.Parameters,
            },
            cancellationToken).ConfigureAwait(false);

        return new ReportViewerRenderResult
        {
            Snapshot = snapshot,
            Metadata = metadata,
            Parameters = processing.Context.Parameters,
            InteractionToken = request.InteractionToken,
        };
    }

    /// <inheritdoc />
    public async Task<ReportViewerExportResult> ExportPdfAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await RenderAsync(request, cancellationToken).ConfigureAwait(false);
        var bytes = _pdfRenderer.Render(result.Snapshot);
        var fileName = string.IsNullOrWhiteSpace(_definition.Id)
            ? "report.pdf"
            : $"{SanitizeFileName(_definition.Id)}.pdf";
        return new ReportViewerExportResult(bytes, fileName, "application/pdf");
    }

    /// <inheritdoc />
    public async Task<ReportViewerExportResult> ExportCsvAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var processing = await ProcessAsync(request, cancellationToken).ConfigureAwait(false);
        var document = ReportTabularExportBuilder.Build(_definition, processing.Context);
        var culture = CreateCulture(request.CultureName);
        var delimiter = ResolveDelimiter(culture);
        var bytes = ReportCsvExporter.Export(
            document,
            new ReportCsvExportOptions
            {
                Culture = culture,
                Delimiter = delimiter,
                IncludeBom = true,
            });
        return new ReportViewerExportResult(bytes, ExportFileName("csv"), "text/csv; charset=utf-8");
    }

    /// <inheritdoc />
    public async Task<ReportViewerExportResult> ExportXlsxAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var processing = await ProcessAsync(request, cancellationToken).ConfigureAwait(false);
        var document = ReportTabularExportBuilder.Build(_definition, processing.Context);
        var bytes = ReportXlsxExporter.Export(
            document,
            new ReportXlsxExportOptions { Culture = CreateCulture(request.CultureName) });
        return new ReportViewerExportResult(
            bytes,
            ExportFileName("xlsx"),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task<ProcessingResult> ProcessAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken)
    {
        var executionContext = CreateExecutionContext(request, cancellationToken);
        var resolution = await ReportParameterProcessor.ResolveAsync(
            _definition,
            _dataProvider,
            request.Parameters,
            executionContext).ConfigureAwait(false);
        var dataSets = await LoadDataSetsAsync(
            resolution.Values,
            executionContext,
            cancellationToken).ConfigureAwait(false);
        return new ProcessingResult(new ReportProcessingContext(executionContext, resolution.Values, dataSets));
    }

    private static ReportExecutionContext CreateExecutionContext(
        ReportViewerMetadataRequest request,
        CancellationToken cancellationToken)
        => new(request.TenantId, request.UserId, request.CultureName, CancellationToken: cancellationToken);

    private static ReportExecutionContext CreateExecutionContext(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken)
        => new(request.TenantId, request.UserId, request.CultureName, CancellationToken: cancellationToken);

    private async Task<IReadOnlyDictionary<string, ProcessedDataSet>> LoadDataSetsAsync(
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var dataSets = new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal);
        foreach (var dataSet in _definition.DataSets)
        {
            var result = await _dataProvider.GetDataAsync(
                dataSet.Name,
                new ReportDataQuery
                {
                    SourceName = dataSet.Source?.Name,
                    Text = dataSet.Query,
                },
                parameters,
                executionContext).ConfigureAwait(false);
            dataSets[dataSet.Name] = await ReportDataSetRuntime.LoadAsync(
                dataSet.Name,
                result,
                cancellationToken).ConfigureAwait(false);
        }

        return dataSets;
    }

    private async Task<IReadOnlyList<ReportViewerParameterOption>> ResolveOptionsAsync(
        ReportParameterDefinition parameter,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        if (parameter.AvailableValues is null)
        {
            return [];
        }

        if (parameter.AvailableValues.Kind == ReportParameterAvailableValuesKind.Static)
        {
            return parameter.AvailableValues.StaticValues
                .Select(value => new ReportViewerParameterOption(value.Value, value.Label ?? value.Value))
                .ToArray();
        }

        if (string.IsNullOrWhiteSpace(parameter.AvailableValues.DataSetName))
        {
            return [];
        }

        var dataSetDefinition = _definition.DataSets.FirstOrDefault(dataSet =>
            string.Equals(dataSet.Name, parameter.AvailableValues.DataSetName, StringComparison.Ordinal));
        var result = await _dataProvider.GetDataAsync(
            parameter.AvailableValues.DataSetName,
            new ReportDataQuery
            {
                SourceName = dataSetDefinition?.Source?.Name,
                Text = dataSetDefinition?.Query,
            },
            parameters,
            executionContext).ConfigureAwait(false);
        var dataSet = await ReportDataSetRuntime.LoadAsync(
            parameter.AvailableValues.DataSetName,
            result,
            cancellationToken).ConfigureAwait(false);
        var culture = CreateCulture(executionContext.CultureName);

        return dataSet.Rows
            .Select(row =>
            {
                var value = ResolveField(row, parameter.AvailableValues.ValueField);
                var label = ResolveField(row, parameter.AvailableValues.LabelField ?? parameter.AvailableValues.ValueField);
                return new ReportViewerParameterOption(
                    Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(label ?? value, culture) ?? string.Empty);
            })
            .ToArray();
    }

    private static object? ResolveField(ProcessedDataRow row, string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        return field.TrimStart().StartsWith("=", StringComparison.Ordinal) ? null : row[field];
    }

    private static ProcessedDataSet ResolvePrimaryDataSet(IReadOnlyDictionary<string, ProcessedDataSet> dataSets)
    {
        var first = dataSets.Values.FirstOrDefault();
        return first ?? new ProcessedDataSet("Empty", [], [new ProcessedDataRow(new Dictionary<string, object?>(StringComparer.Ordinal))]);
    }

    private static CultureInfo CreateCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(cultureName) ? "en-US" : cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "report" : sanitized;
    }

    private string ExportFileName(string extension)
        => string.IsNullOrWhiteSpace(_definition.Id)
            ? $"report.{extension}"
            : $"{SanitizeFileName(_definition.Id)}.{extension}";

    private static char ResolveDelimiter(CultureInfo culture)
    {
        var separator = culture.TextInfo.ListSeparator;
        return string.IsNullOrEmpty(separator) ? ',' : separator[0];
    }

    private sealed record ProcessingResult(ReportProcessingContext Context);
}
