using System.Globalization;
using System.Text;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.Reporting.Engine.Export;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Pdf;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>Renders report server definitions to snapshots and export payloads.</summary>
public interface IReportServerRenderer
{
    /// <summary>Gets parameter metadata for a report definition.</summary>
    Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(ReportDetailDto report, CancellationToken cancellationToken = default);

    /// <summary>Renders a report.</summary>
    Task<RenderReportResultDto> RenderAsync(
        ReportDetailDto report,
        RenderReportRequestDto request,
        ReportExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Default renderer backed by Tempo.Reporting.Engine.</summary>
public sealed class ReportServerRenderer : IReportServerRenderer
{
    private readonly IReportDataProvider _dataProvider;

    /// <summary>Creates a renderer.</summary>
    public ReportServerRenderer(IReportDataProvider dataProvider)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(ReportDetailDto report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var definition = ReportDefinitionJsonSerializer.Deserialize(report.DefinitionJson);
        return Task.FromResult<IReadOnlyList<ReportParameterMetadataDto>>(definition.Parameters
            .Where(parameter => !parameter.Hidden)
            .Select(ToMetadata)
            .ToArray());
    }

    /// <inheritdoc />
    public async Task<RenderReportResultDto> RenderAsync(
        ReportDetailDto report,
        RenderReportRequestDto request,
        ReportExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var definition = ReportDefinitionJsonSerializer.Deserialize(report.DefinitionJson);
        var parameters = request.Parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => ReportParameterValue.Multiple(parameter.Values),
            StringComparer.Ordinal);
        var executionContext = context with
        {
            TenantId = request.TenantId,
            CultureName = string.IsNullOrWhiteSpace(request.CultureName) ? context.CultureName : request.CultureName,
            CancellationToken = cancellationToken,
        };
        var dataSets = await LoadDataSetsAsync(definition, parameters, executionContext, cancellationToken).ConfigureAwait(false);
        var processingContext = new ReportProcessingContext(executionContext, parameters, dataSets);
        var primary = dataSets.Values.FirstOrDefault() ?? new ProcessedDataSet("__empty", [], [new ProcessedDataRow(new Dictionary<string, object?>())]);
        var instance = ReportBandInstantiator.Instantiate(definition, primary, processingContext);
        var snapshot = ReportSnapshotGenerator.Generate(instance, new BasicReportTextMeasurer());

        return request.Format switch
        {
            ReportRenderFormat.Pdf => BinaryResult(report, request, "application/pdf", "pdf", new ReportPdfRenderer().Render(snapshot), snapshot.Pages.Count),
            ReportRenderFormat.Png => BinaryResult(
                report,
                request,
                "image/png",
                "png",
                snapshot.Pages.Count == 0 ? [] : new ReportPdfRenderer().RenderPagePng(snapshot.Pages[0]),
                snapshot.Pages.Count),
            ReportRenderFormat.Csv => BinaryResult(
                report,
                request,
                "text/csv",
                "csv",
                ReportCsvExporter.Export(ReportTabularExportBuilder.Build(definition, processingContext), new ReportCsvExportOptions { Culture = CultureInfo.InvariantCulture }),
                snapshot.Pages.Count),
            ReportRenderFormat.Xlsx => BinaryResult(
                report,
                request,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xlsx",
                ReportXlsxExporter.Export(ReportTabularExportBuilder.Build(definition, processingContext)),
                snapshot.Pages.Count),
            _ => SnapshotResult(report, request, snapshot),
        };
    }

    private async Task<IReadOnlyDictionary<string, ProcessedDataSet>> LoadDataSetsAsync(
        ReportDefinition definition,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var dataSets = new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal);
        foreach (var dataSet in definition.DataSets)
        {
            var result = await _dataProvider.GetDataAsync(
                dataSet.Name,
                new ReportDataQuery
                {
                    SourceName = dataSet.Source?.Name,
                    Text = dataSet.Query,
                },
                parameters,
                context).ConfigureAwait(false);
            dataSets[dataSet.Name] = await ReportDataSetRuntime.LoadAsync(dataSet.Name, result, cancellationToken).ConfigureAwait(false);
        }

        return dataSets;
    }

    private static ReportParameterMetadataDto ToMetadata(ReportParameterDefinition parameter)
        => new()
        {
            Name = parameter.Name,
            Label = parameter.Label ?? parameter.Name,
            Kind = parameter.DataType switch
            {
                ReportParameterType.Number => ReportParameterMetadataKind.Number,
                ReportParameterType.Date => ReportParameterMetadataKind.Date,
                ReportParameterType.Boolean => ReportParameterMetadataKind.Boolean,
                ReportParameterType.List when parameter.AllowMultipleValues => ReportParameterMetadataKind.MultiSelect,
                ReportParameterType.List => ReportParameterMetadataKind.Select,
                _ => ReportParameterMetadataKind.String,
            },
            IsRequired = parameter.Required,
            AllowMultiple = parameter.AllowMultipleValues,
            DefaultValues = string.IsNullOrWhiteSpace(parameter.DefaultExpression) ? [] : [parameter.DefaultExpression],
            Options = parameter.AvailableValues?.StaticValues
                .Select(option => new ReportParameterOptionDto { Value = option.Value, Label = option.Label ?? option.Value })
                .ToList() ?? [],
        };

    private static RenderReportResultDto SnapshotResult(
        ReportDetailDto report,
        RenderReportRequestDto request,
        ReportSnapshot snapshot)
    {
        var json = ReportSnapshotJsonSerializer.Serialize(snapshot);
        return new RenderReportResultDto
        {
            TenantId = request.TenantId,
            ReportId = request.ReportId,
            Format = ReportRenderFormat.Snapshot,
            ContentType = "application/json",
            FileName = $"{Slug(report.Name)}.snapshot.json",
            Bytes = Encoding.UTF8.GetBytes(json),
            SnapshotJson = json,
            PageCount = snapshot.Pages.Count,
        };
    }

    private static RenderReportResultDto BinaryResult(
        ReportDetailDto report,
        RenderReportRequestDto request,
        string contentType,
        string extension,
        byte[] bytes,
        int pageCount)
        => new()
        {
            TenantId = request.TenantId,
            ReportId = request.ReportId,
            Format = request.Format,
            ContentType = contentType,
            FileName = $"{Slug(report.Name)}.{extension}",
            Bytes = bytes,
            PageCount = pageCount,
        };

    private static string Slug(string value)
    {
        var chars = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "report" : slug;
    }
}

/// <summary>Fallback provider used by API smoke tests and empty reports.</summary>
public sealed class EmptyReportDataProvider : IReportDataProvider
{
    /// <inheritdoc />
    public Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context)
        => Task.FromResult(new ReportDataSetResult([], EmptyRows(context.CancellationToken)));

    private static async IAsyncEnumerable<ReportDataRow> EmptyRows(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return new ReportDataRow(new Dictionary<string, object?>());
    }
}

internal sealed class BasicReportTextMeasurer : ITextMeasurer
{
    public TextMeasurement MeasureRun(TextMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var glyphs = (request.Text ?? string.Empty).EnumerateRunes().Count();
        var width = glyphs * request.FontSize * 0.55d;
        var ascent = request.FontSize * 0.78d;
        var descent = request.FontSize * 0.22d;
        var lineGap = request.FontSize * 0.2d;
        return new TextMeasurement(width, ascent, descent, lineGap, ascent + descent + lineGap, glyphs, 0, 0);
    }
}
