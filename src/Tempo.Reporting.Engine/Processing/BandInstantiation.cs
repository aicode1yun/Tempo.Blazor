#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Processing options for report instantiation.</summary>
public sealed record ReportProcessingOptions
{
    /// <summary>Maximum allowed nested sub-report depth.</summary>
    public int MaxSubReportDepth { get; init; } = 8;
}

/// <summary>Processed report instance produced before layout.</summary>
public sealed record ReportInstance
{
    /// <summary>Creates a report instance.</summary>
    public ReportInstance(
        ReportDefinition definition,
        IReadOnlyList<ReportBandInstance> bands,
        IReadOnlyDictionary<string, ProcessedDataSet>? dataSets = null,
        ReportProcessingContext? processingContext = null)
    {
        Definition = definition;
        Bands = bands.ToArray();
        DataSets = dataSets is null
            ? new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal)
            : new Dictionary<string, ProcessedDataSet>(dataSets, StringComparer.Ordinal);
        ProcessingContext = processingContext;
    }

    /// <summary>Source report definition.</summary>
    public ReportDefinition Definition { get; }

    /// <summary>Instantiated bands in processing order.</summary>
    public IReadOnlyList<ReportBandInstance> Bands { get; }

    /// <summary>Materialized data sets available to layout-time elements such as tablix.</summary>
    public IReadOnlyDictionary<string, ProcessedDataSet> DataSets { get; }

    /// <summary>Processing context available to layout-time expression evaluation.</summary>
    public ReportProcessingContext? ProcessingContext { get; }
}

/// <summary>Instantiated band with evaluated elements.</summary>
public sealed record ReportBandInstance
{
    /// <summary>Creates a band instance.</summary>
    public ReportBandInstance(
        ReportBandKind kind,
        ProcessedDataRow? row,
        object? groupKey,
        IReadOnlyList<ReportElementInstance> elements,
        IReadOnlyList<ReportBandInstance>? children = null,
        ReportBand? sourceBand = null)
    {
        Kind = kind;
        Row = row;
        GroupKey = groupKey;
        Elements = elements.ToArray();
        Children = children?.ToArray() ?? [];
        SourceBand = sourceBand;
    }

    /// <summary>Band kind.</summary>
    public ReportBandKind Kind { get; }

    /// <summary>Detail row, when the band was instantiated for a row.</summary>
    public ProcessedDataRow? Row { get; }

    /// <summary>Group key, when the band was instantiated for a group.</summary>
    public object? GroupKey { get; }

    /// <summary>Evaluated element instances.</summary>
    public IReadOnlyList<ReportElementInstance> Elements { get; }

    /// <summary>Nested band instances.</summary>
    public IReadOnlyList<ReportBandInstance> Children { get; }

    /// <summary>Source band definition that produced this instance, when available.</summary>
    public ReportBand? SourceBand { get; }

    /// <summary>Nominal band height from the definition or inferred element bounds.</summary>
    public double Height => SourceBand?.Height ?? Elements
        .Select(element => element.Source.Y + element.Source.Height)
        .DefaultIfEmpty(0)
        .Max();

    /// <summary>Whether the band should stay together when paginated.</summary>
    public bool KeepTogether => SourceBand?.KeepTogether ?? false;
}

/// <summary>Base processed element instance.</summary>
public record ReportElementInstance
{
    /// <summary>Creates an element instance.</summary>
    public ReportElementInstance(ReportElement source, object? value, string? text)
    {
        Source = source;
        ElementId = source.Id;
        Value = value;
        Text = text;
    }

    /// <summary>Source element definition.</summary>
    public ReportElement Source { get; }

    /// <summary>Element identifier.</summary>
    public string ElementId { get; }

    /// <summary>Evaluated raw value.</summary>
    public object? Value { get; }

    /// <summary>Evaluated display text.</summary>
    public string? Text { get; }
}

/// <summary>Processed rich-text run placeholder produced from a text box value.</summary>
public sealed record ReportTextRun(string Text);

/// <summary>Processed text box instance.</summary>
public sealed record ReportTextBoxInstance : ReportElementInstance
{
    /// <summary>Creates a text box instance.</summary>
    public ReportTextBoxInstance(ReportTextBoxElement source, object? value, string text)
        : base(source, value, text)
    {
        Runs = [new ReportTextRun(text)];
    }

    /// <summary>Evaluated text runs.</summary>
    public IReadOnlyList<ReportTextRun> Runs { get; }
}

/// <summary>Processed sub-report placeholder with evaluated parameters.</summary>
public sealed record ReportSubReportInstance : ReportElementInstance
{
    /// <summary>Creates a sub-report instance.</summary>
    public ReportSubReportInstance(
        ReportSubReportElement source,
        IReadOnlyDictionary<string, ReportParameterValue> parameterValues,
        int depth)
        : base(source, source.ReportId, source.ReportId)
    {
        ReportId = source.ReportId;
        ParameterValues = new Dictionary<string, ReportParameterValue>(parameterValues, StringComparer.Ordinal);
        Depth = depth;
    }

    /// <summary>Referenced report identifier.</summary>
    public string ReportId { get; }

    /// <summary>Evaluated mapped parameter values.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue> ParameterValues { get; }

    /// <summary>Sub-report nesting depth.</summary>
    public int Depth { get; }
}

/// <summary>Builds a tree of report band instances from definitions and data.</summary>
public static class ReportBandInstantiator
{
    /// <summary>Instantiates report bands over a primary data set.</summary>
    public static ReportInstance Instantiate(
        ReportDefinition definition,
        ProcessedDataSet dataSet,
        ReportProcessingContext context,
        ReportProcessingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(context);

        options ??= new ReportProcessingOptions();
        var bands = new List<ReportBandInstance>();

        AddBandIfVisible(bands, definition.Bands.ReportHeader, null, null, dataSet.Rows, context, options, 0);

        if (definition.Bands.Groups.Count == 0)
        {
            foreach (var row in dataSet.Rows)
            {
                AddBandIfVisible(bands, definition.Bands.Detail, row, null, [row], context, options, 0);
            }
        }
        else
        {
            var levels = definition.Bands.Groups
                .Select(group => new ReportGroupLevel(group.Name, group.Expression))
                .ToArray();
            foreach (var group in ReportGroupingEngine.Group(dataSet, levels, context))
            {
                AddGroupBands(bands, group, definition.Bands.Groups, 0, definition.Bands.Detail, context, options, dataSet.Rows);
            }
        }

        AddBandIfVisible(bands, definition.Bands.ReportFooter, null, null, dataSet.Rows, context, options, 0);
        return new ReportInstance(definition, bands, context.DataSets, context);
    }

    private static void AddGroupBands(
        List<ReportBandInstance> bands,
        ProcessedGroup group,
        IReadOnlyList<ReportGroupDefinition> definitions,
        int level,
        ReportBand? detailBand,
        ReportProcessingContext context,
        ReportProcessingOptions options,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        var definition = definitions[level];
        AddBandIfVisible(bands, definition.GroupHeader, group.Rows.FirstOrDefault(), group.Key, group.Rows, context, options, 0, reportRows);

        if (group.Children.Count == 0)
        {
            foreach (var row in group.Rows)
            {
                AddBandIfVisible(bands, detailBand, row, group.Key, [row], context, options, 0, reportRows);
            }
        }
        else
        {
            foreach (var child in group.Children)
            {
                AddGroupBands(bands, child, definitions, level + 1, detailBand, context, options, reportRows);
            }
        }

        AddBandIfVisible(bands, definition.GroupFooter, group.Rows.LastOrDefault(), group.Key, group.Rows, context, options, 0, reportRows);
    }

    private static void AddBandIfVisible(
        List<ReportBandInstance> bands,
        ReportBand? band,
        ProcessedDataRow? row,
        object? groupKey,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        ReportProcessingOptions options,
        int subReportDepth,
        IReadOnlyList<ProcessedDataRow>? reportRows = null)
    {
        if (band is null || !IsVisible(band.VisibleExpression, row, scopeRows, context, reportRows ?? scopeRows))
        {
            return;
        }

        var elements = band.Elements
            .Where(element => IsVisible(element.VisibleExpression, row, scopeRows, context, reportRows ?? scopeRows))
            .Select(element => InstantiateElement(element, row, scopeRows, context, options, subReportDepth, reportRows ?? scopeRows))
            .ToArray();
        bands.Add(new ReportBandInstance(band.Kind, row, groupKey, elements, sourceBand: band));
    }

    private static ReportElementInstance InstantiateElement(
        ReportElement element,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        ReportProcessingOptions options,
        int subReportDepth,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        return element switch
        {
            ReportTextBoxElement textBox => InstantiateTextBox(textBox, row, scopeRows, context, reportRows),
            ReportSubReportElement subReport => InstantiateSubReport(subReport, row, scopeRows, context, options, subReportDepth, reportRows),
            _ => new ReportElementInstance(element, null, null),
        };
    }

    private static ReportTextBoxInstance InstantiateTextBox(
        ReportTextBoxElement textBox,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        if (!string.IsNullOrWhiteSpace(textBox.Expression))
        {
            var value = ReportAggregateEngine.EvaluateForRow(textBox.Expression, row, scopeRows, context, reportRows);
            return new ReportTextBoxInstance(textBox, value.RawValue, value.AsString());
        }

        return new ReportTextBoxInstance(textBox, textBox.Text, textBox.Text ?? string.Empty);
    }

    private static ReportSubReportInstance InstantiateSubReport(
        ReportSubReportElement subReport,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        ReportProcessingOptions options,
        int subReportDepth,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        if (subReportDepth >= options.MaxSubReportDepth)
        {
            throw new ReportProcessingException(
                "Processing.SubReportDepthExceeded",
                $"Sub-report depth limit {options.MaxSubReportDepth} was exceeded.");
        }

        var parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);
        foreach (var mapping in subReport.ParameterMappings)
        {
            var value = ReportAggregateEngine.EvaluateForRow(mapping.Expression, row, scopeRows, context, reportRows);
            parameters[mapping.ParameterName] = ReportParameterValue.Scalar(value.RawValue);
        }

        return new ReportSubReportInstance(subReport, parameters, subReportDepth + 1);
    }

    private static bool IsVisible(
        string? expression,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
        => string.IsNullOrWhiteSpace(expression) ||
           ReportAggregateEngine.EvaluateForRow(expression, row, scopeRows, context, reportRows).AsBoolean();
}
