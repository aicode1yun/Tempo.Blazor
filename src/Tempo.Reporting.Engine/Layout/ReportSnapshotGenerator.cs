#pragma warning disable MA0048

using System.Globalization;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Options for report snapshot generation.</summary>
public sealed record ReportSnapshotGeneratorOptions
{
    /// <summary>Stable snapshot identifier.</summary>
    public string SnapshotId { get; init; } = "report-snapshot";

    /// <summary>Page background fill color.</summary>
    public string PageFillColor { get; init; } = "#ffffff";

    /// <summary>Optional page stroke color.</summary>
    public string? PageStrokeColor { get; init; } = "#e5e7eb";

    /// <summary>Page stroke width.</summary>
    public double PageStrokeWidth { get; init; } = 1;

    /// <summary>Minimum remaining body height worth using before moving a band to a new page.</summary>
    public double MinimumOrphanHeight { get; init; } = 12;
}

/// <summary>Generates fixed-page snapshots from processed report instances.</summary>
public static class ReportSnapshotGenerator
{
    /// <summary>Generates a deterministic snapshot from a processed report instance.</summary>
    public static ReportSnapshot Generate(
        ReportInstance instance,
        ITextMeasurer measurer,
        ReportSnapshotGeneratorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(measurer);

        options ??= new ReportSnapshotGeneratorOptions();
        var composition = ReportPageComposer.Compose(
            instance,
            measurer,
            new ReportPageCompositionOptions { MinimumOrphanHeight = options.MinimumOrphanHeight });
        var totalPages = composition.Pages.Count;
        var processingContext = instance.ProcessingContext ??
            new ReportProcessingContext(new ReportExecutionContext("report", "report", "en-US"), dataSets: instance.DataSets);
        return new ReportSnapshot
        {
            SnapshotId = options.SnapshotId,
            Pages = composition.Pages
                .Select(page => CreateSnapshotPage(composition.Definition, page, totalPages, measurer, options, processingContext))
                .ToList(),
        };
    }

    private static ReportSnapshotPage CreateSnapshotPage(
        ReportDefinition definition,
        ReportComposedPage page,
        int totalPages,
        ITextMeasurer measurer,
        ReportSnapshotGeneratorOptions options,
        ReportProcessingContext processingContext)
    {
        var commands = new List<ReportSnapshotCommand>
        {
            ReportSnapshotCommand.Rectangle(
                $"p{page.PageNumber:000}-page",
                0,
                0,
                page.Width,
                page.Height,
                options.PageFillColor,
                options.PageStrokeColor,
                options.PageStrokeWidth),
        };

        var placementIndex = 0;
        foreach (var placement in page.Placements)
        {
            foreach (var command in CreateBandCommands(definition, placement, page.PageNumber, totalPages, measurer, placementIndex, processingContext))
            {
                commands.Add(command);
            }

            placementIndex++;
        }

        return new ReportSnapshotPage
        {
            PageNumber = page.PageNumber,
            Width = page.Width,
            Height = page.Height,
            Commands = commands,
        };
    }

    private static IEnumerable<ReportSnapshotCommand> CreateBandCommands(
        ReportDefinition definition,
        ReportBandPlacement placement,
        int pageNumber,
        int totalPages,
        ITextMeasurer measurer,
        int placementIndex,
        ReportProcessingContext processingContext)
    {
        if (placement.TablePage is not null)
        {
            var tableId = placement.TableElementId ?? "table";
            foreach (var command in ReportTableLayouter.ToSnapshotCommands(
                placement.TablePage,
                placement.X,
                placement.Y,
                $"p{pageNumber:000}-b{placementIndex:000}-{tableId}",
                measurer))
            {
                yield return command;
            }

            yield break;
        }

        var elementIndex = 0;
        foreach (var element in placement.Band.Elements)
        {
            var idPrefix = $"p{pageNumber:000}-b{placementIndex:000}-e{elementIndex:000}-{element.ElementId}";
            foreach (var command in CreateElementCommands(definition, placement, element, pageNumber, totalPages, measurer, idPrefix, processingContext))
            {
                yield return command;
            }

            elementIndex++;
        }
    }

    private static IEnumerable<ReportSnapshotCommand> CreateElementCommands(
        ReportDefinition definition,
        ReportBandPlacement placement,
        ReportElementInstance element,
        int pageNumber,
        int totalPages,
        ITextMeasurer measurer,
        string idPrefix,
        ReportProcessingContext processingContext)
    {
        var source = element.Source;
        var x = placement.X + source.X;
        var y = placement.Y + source.Y;

        switch (source)
        {
            case ReportTextBoxElement textBox when element is ReportTextBoxInstance textBoxInstance:
                foreach (var command in CreateTextBoxCommands(definition, textBox, textBoxInstance, x, y, pageNumber, totalPages, measurer, idPrefix))
                {
                    yield return command;
                }

                break;

            case ReportImageElement image:
                yield return ReportSnapshotCommand.Image(idPrefix, x, y, image.Width, image.Height, image.Source);
                break;

            case ReportShapeElement shape:
                foreach (var command in CreateShapeCommands(shape, x, y, idPrefix))
                {
                    yield return command;
                }

                break;

            case ReportLineElement line:
                yield return ReportSnapshotCommand.Line(
                    idPrefix,
                    x,
                    y,
                    line.Width,
                    line.Height,
                    line.Stroke.Color,
                    line.Stroke.Width);
                break;

            case ReportChartElement chart:
                foreach (var command in ReportChartLayouter.ToSnapshotCommands(chart, processingContext, x, y, idPrefix, measurer))
                {
                    yield return command;
                }

                break;
        }
    }

    private static IEnumerable<ReportSnapshotCommand> CreateTextBoxCommands(
        ReportDefinition definition,
        ReportTextBoxElement textBox,
        ReportTextBoxInstance instance,
        double x,
        double y,
        int pageNumber,
        int totalPages,
        ITextMeasurer measurer,
        string idPrefix)
    {
        var style = ResolveStyle(definition, textBox.StyleId);
        var textStyle = style?.Text ?? textBox.TextStyle;
        var runs = instance.Runs
            .Select(run => new ReportRichTextRun(ResolvePageText(run.Text, pageNumber, totalPages), textStyle))
            .ToArray();
        var layout = ReportTextBoxLayouter.Layout(
            new ReportTextBoxLayoutRequest
            {
                Id = idPrefix,
                X = x,
                Y = y,
                Width = textBox.Width,
                Height = textBox.Height,
                Padding = textBox.Padding ?? style?.Padding ?? new ReportThickness(),
                Border = textBox.Border ?? style?.Border,
                FillColor = style?.FillColor,
                HorizontalAlignment = textBox.HorizontalAlignment,
                VerticalAlignment = textBox.VerticalAlignment,
                LineSpacing = textStyle.LineHeight,
                CanGrow = textBox.CanGrow,
                Runs = runs,
            },
            measurer);

        foreach (var command in layout.ToSnapshotCommands())
        {
            yield return command;
        }
    }

    private static IEnumerable<ReportSnapshotCommand> CreateShapeCommands(ReportShapeElement shape, double x, double y, string idPrefix)
    {
        var border = FirstBorderLine(shape.Border);
        if (shape.Shape == ReportShapeKind.Ellipse)
        {
            var rx = shape.Width / 2;
            var ry = shape.Height / 2;
            var cx = x + rx;
            var cy = y + ry;
            yield return new ReportSnapshotCommand
            {
                Id = idPrefix,
                Type = ReportSnapshotCommandType.Path,
                X = x,
                Y = y,
                Width = shape.Width,
                Height = shape.Height,
                Fill = shape.FillColor,
                Stroke = border?.Color,
                StrokeWidth = border?.Width ?? 0,
                PathData = string.Create(
                    CultureInfo.InvariantCulture,
                    $"M {cx - rx} {cy} a {rx} {ry} 0 1 0 {shape.Width} 0 a {rx} {ry} 0 1 0 {-shape.Width} 0"),
            };
            yield break;
        }

        yield return ReportSnapshotCommand.Rectangle(
            idPrefix,
            x,
            y,
            shape.Width,
            shape.Height,
            shape.FillColor ?? string.Empty,
            border?.Color,
            border?.Width ?? 0);
    }

    private static ReportStyleDefinition? ResolveStyle(ReportDefinition definition, string? styleId)
        => string.IsNullOrWhiteSpace(styleId)
            ? null
            : definition.Styles.FirstOrDefault(style => string.Equals(style.Id, styleId, StringComparison.Ordinal));

    private static ReportBorderLine? FirstBorderLine(ReportBorder? border)
        => border?.Top ?? border?.Right ?? border?.Bottom ?? border?.Left;

    private static string ResolvePageText(string text, int pageNumber, int totalPages)
        => text
            .Replace("{PageNumber}", pageNumber.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{TotalPages}", totalPages.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("PageNumber", pageNumber.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("TotalPages", totalPages.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}

#pragma warning restore MA0048
