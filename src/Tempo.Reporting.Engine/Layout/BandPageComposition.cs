#pragma warning disable MA0048

using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Options for band pagination.</summary>
public sealed record ReportPageCompositionOptions
{
    /// <summary>Minimum remaining body height worth using before the next band is moved to a new page.</summary>
    public double MinimumOrphanHeight { get; init; } = 12;
}

/// <summary>Paginated report band composition.</summary>
public sealed record ReportPageComposition
{
    /// <summary>Creates a page composition.</summary>
    public ReportPageComposition(ReportDefinition definition, IReadOnlyList<ReportComposedPage> pages)
    {
        Definition = definition;
        Pages = pages.ToArray();
    }

    /// <summary>Source report definition.</summary>
    public ReportDefinition Definition { get; }

    /// <summary>Composed pages.</summary>
    public IReadOnlyList<ReportComposedPage> Pages { get; }
}

/// <summary>A single composed page with repeated and body band placements.</summary>
public sealed record ReportComposedPage
{
    /// <summary>Creates a composed page.</summary>
    public ReportComposedPage(
        int pageNumber,
        double width,
        double height,
        ReportLayoutRectangle contentRectangle,
        IReadOnlyList<ReportBandPlacement> placements)
    {
        PageNumber = pageNumber;
        Width = width;
        Height = height;
        ContentRectangle = contentRectangle;
        Placements = placements.ToArray();
    }

    /// <summary>One-based page number.</summary>
    public int PageNumber { get; }

    /// <summary>Page width.</summary>
    public double Width { get; }

    /// <summary>Page height.</summary>
    public double Height { get; }

    /// <summary>Body content rectangle between page header and footer.</summary>
    public ReportLayoutRectangle ContentRectangle { get; }

    /// <summary>Band placements in paint order.</summary>
    public IReadOnlyList<ReportBandPlacement> Placements { get; }
}

/// <summary>Absolute placement of a processed band on a composed page.</summary>
public sealed record ReportBandPlacement
{
    /// <summary>Creates a band placement.</summary>
    public ReportBandPlacement(
        ReportBandInstance band,
        double x,
        double y,
        double width,
        double height,
        bool isRepeatedPageBand,
        ReportTableLayoutPage? tablePage = null,
        string? tableElementId = null)
    {
        Band = band;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        IsRepeatedPageBand = isRepeatedPageBand;
        TablePage = tablePage;
        TableElementId = tableElementId;
    }

    /// <summary>Placed band instance.</summary>
    public ReportBandInstance Band { get; }

    /// <summary>Absolute left coordinate.</summary>
    public double X { get; }

    /// <summary>Absolute top coordinate.</summary>
    public double Y { get; }

    /// <summary>Placement width.</summary>
    public double Width { get; }

    /// <summary>Placement height.</summary>
    public double Height { get; }

    /// <summary>Whether this placement is a repeated page header or footer.</summary>
    public bool IsRepeatedPageBand { get; }

    /// <summary>Table page slice rendered by this placement, when the band is split by a tablix.</summary>
    public ReportTableLayoutPage? TablePage { get; }

    /// <summary>Element id of the table rendered by this placement.</summary>
    public string? TableElementId { get; }
}

/// <summary>Composes processed report bands into fixed pages.</summary>
public static class ReportPageComposer
{
    /// <summary>Composes report bands into pages.</summary>
    public static ReportPageComposition Compose(
        ReportInstance instance,
        ITextMeasurer measurer,
        ReportPageCompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(measurer);

        options ??= new ReportPageCompositionOptions();
        var definition = instance.Definition;
        var pageSize = ApplyOrientation(definition.PageSetup.PageSize, definition.PageSetup.Orientation);
        var pages = new List<MutableComposedPage>();
        var current = CreatePage(definition, pageSize, pages.Count + 1);
        pages.Add(current);
        var cursorY = current.ContentRectangle.Y;

        foreach (var band in instance.Bands)
        {
            if (TryAddTableBand(instance, band, measurer, options, pages, ref current, ref cursorY))
            {
                continue;
            }

            var bandHeight = ReportBandLayoutMeasure.MeasureHeight(band, measurer);
            if (ShouldMoveToNextPage(band, bandHeight, cursorY, current.ContentRectangle, options))
            {
                current = CreatePage(definition, pageSize, pages.Count + 1);
                pages.Add(current);
                cursorY = current.ContentRectangle.Y;
            }

            current.BodyPlacements.Add(new ReportBandPlacement(
                band,
                current.ContentRectangle.X,
                cursorY,
                current.ContentRectangle.Width,
                bandHeight,
                isRepeatedPageBand: false));
            cursorY += bandHeight;
        }

        return new ReportPageComposition(
            definition,
            pages.Select(page => page.ToImmutable()).ToArray());
    }

    private static bool TryAddTableBand(
        ReportInstance instance,
        ReportBandInstance band,
        ITextMeasurer measurer,
        ReportPageCompositionOptions options,
        List<MutableComposedPage> pages,
        ref MutableComposedPage current,
        ref double cursorY)
    {
        var tableElement = band.Elements.FirstOrDefault(element => element.Source is ReportTableElement);
        if (tableElement?.Source is not ReportTableElement table ||
            !TryResolveDataSet(instance, table, out var dataSet))
        {
            return false;
        }

        var context = instance.ProcessingContext ?? new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: instance.DataSets);
        var firstPageHeight = Math.Max(0, current.ContentRectangle.Y + current.ContentRectangle.Height - cursorY - table.Y);
        var continuedPageHeight = Math.Max(0, current.ContentRectangle.Height - table.Y);
        var tableLayout = ReportTableLayouter.Layout(
            new ReportTableLayoutRequest
            {
                Table = table,
                DataSet = dataSet,
                Context = context,
                Styles = instance.Definition.Styles,
                X = table.X,
                Y = 0,
                Width = table.Width,
                FirstPageHeight = firstPageHeight,
                PageHeight = continuedPageHeight,
            },
            measurer);

        for (var index = 0; index < tableLayout.Pages.Count; index++)
        {
            var tablePage = tableLayout.Pages[index];
            var placementHeight = table.Y + tablePage.Height;
            if (index > 0 || ShouldMoveToNextPage(band, placementHeight, cursorY, current.ContentRectangle, options))
            {
                current = CreatePage(instance.Definition, ApplyOrientation(instance.Definition.PageSetup.PageSize, instance.Definition.PageSetup.Orientation), pages.Count + 1);
                pages.Add(current);
                cursorY = current.ContentRectangle.Y;
            }

            current.BodyPlacements.Add(new ReportBandPlacement(
                band,
                current.ContentRectangle.X,
                cursorY,
                current.ContentRectangle.Width,
                placementHeight,
                isRepeatedPageBand: false,
                tablePage,
                tableElement.ElementId));
            cursorY += placementHeight;
        }

        return true;
    }

    private static bool TryResolveDataSet(ReportInstance instance, ReportTableElement table, out ProcessedDataSet dataSet)
    {
        if (!string.IsNullOrWhiteSpace(table.DataSetName) &&
            instance.DataSets.TryGetValue(table.DataSetName, out var namedDataSet))
        {
            dataSet = namedDataSet;
            return true;
        }

        if (string.IsNullOrWhiteSpace(table.DataSetName) && instance.DataSets.Count == 1)
        {
            dataSet = instance.DataSets.Values.Single();
            return true;
        }

        dataSet = new ProcessedDataSet(string.Empty, [], []);
        return false;
    }

    private static bool ShouldMoveToNextPage(
        ReportBandInstance band,
        double bandHeight,
        double cursorY,
        ReportLayoutRectangle contentRectangle,
        ReportPageCompositionOptions options)
    {
        var usedHeight = Math.Max(0, cursorY - contentRectangle.Y);
        if (usedHeight <= 0.0001)
        {
            return false;
        }

        var remainingHeight = contentRectangle.Y + contentRectangle.Height - cursorY;
        if (bandHeight <= remainingHeight + 0.0001)
        {
            return false;
        }

        return band.KeepTogether ||
            remainingHeight < Math.Max(0, options.MinimumOrphanHeight) ||
            bandHeight <= contentRectangle.Height + 0.0001;
    }

    private static MutableComposedPage CreatePage(ReportDefinition definition, ReportPageSize pageSize, int pageNumber)
    {
        var margins = definition.PageSetup.Margins;
        var pageHeaderHeight = Math.Max(0, definition.Bands.PageHeader?.Height ?? 0);
        var pageFooterHeight = Math.Max(0, definition.Bands.PageFooter?.Height ?? 0);
        var bodyX = margins.Left;
        var bodyWidth = Math.Max(0, pageSize.Width - margins.Left - margins.Right);
        var contentY = margins.Top + pageHeaderHeight;
        var footerY = pageSize.Height - margins.Bottom - pageFooterHeight;
        var contentHeight = Math.Max(0, footerY - contentY);
        var contentRectangle = new ReportLayoutRectangle(bodyX, contentY, bodyWidth, contentHeight);
        var repeatedPlacements = new List<ReportBandPlacement>();

        if (definition.Bands.PageHeader is not null)
        {
            repeatedPlacements.Add(new ReportBandPlacement(
                CreateRepeatedBandInstance(definition.Bands.PageHeader),
                bodyX,
                margins.Top,
                bodyWidth,
                pageHeaderHeight,
                isRepeatedPageBand: true));
        }

        if (definition.Bands.PageFooter is not null)
        {
            repeatedPlacements.Add(new ReportBandPlacement(
                CreateRepeatedBandInstance(definition.Bands.PageFooter),
                bodyX,
                footerY,
                bodyWidth,
                pageFooterHeight,
                isRepeatedPageBand: true));
        }

        return new MutableComposedPage(pageNumber, pageSize.Width, pageSize.Height, contentRectangle, repeatedPlacements);
    }

    private static ReportBandInstance CreateRepeatedBandInstance(ReportBand band)
    {
        var elements = band.Elements
            .Select(element => element is ReportTextBoxElement textBox
                ? new ReportTextBoxInstance(textBox, ResolveRepeatedText(textBox), ResolveRepeatedText(textBox))
                : new ReportElementInstance(element, null, null))
            .ToArray();
        return new ReportBandInstance(band.Kind, null, null, elements, sourceBand: band);
    }

    private static string ResolveRepeatedText(ReportTextBoxElement textBox)
    {
        if (textBox.Text is not null)
        {
            return textBox.Text;
        }

        if (string.IsNullOrWhiteSpace(textBox.Expression))
        {
            return string.Empty;
        }

        var text = textBox.Expression.Trim();
        if (text.StartsWith('='))
        {
            text = text[1..];
        }

        text = text
            .Replace("Globals.PageNumber", "PageNumber", StringComparison.Ordinal)
            .Replace("Globals.TotalPages", "TotalPages", StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("+", string.Empty, StringComparison.Ordinal);

        while (text.Contains("  ", StringComparison.Ordinal))
        {
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        }

        return text.Trim();
    }

    private static ReportPageSize ApplyOrientation(ReportPageSize pageSize, ReportPageOrientation orientation)
        => orientation == ReportPageOrientation.Landscape
            ? new ReportPageSize(pageSize.Height, pageSize.Width, pageSize.Unit)
            : pageSize;

    private sealed class MutableComposedPage
    {
        public MutableComposedPage(
            int pageNumber,
            double width,
            double height,
            ReportLayoutRectangle contentRectangle,
            IReadOnlyList<ReportBandPlacement> repeatedPlacements)
        {
            PageNumber = pageNumber;
            Width = width;
            Height = height;
            ContentRectangle = contentRectangle;
            RepeatedPlacements = repeatedPlacements;
        }

        public int PageNumber { get; }

        public double Width { get; }

        public double Height { get; }

        public ReportLayoutRectangle ContentRectangle { get; }

        public IReadOnlyList<ReportBandPlacement> RepeatedPlacements { get; }

        public List<ReportBandPlacement> BodyPlacements { get; } = [];

        public ReportComposedPage ToImmutable()
            => new(
                PageNumber,
                Width,
                Height,
                ContentRectangle,
                RepeatedPlacements.Concat(BodyPlacements).OrderBy(PaintOrder).ThenBy(placement => placement.Y).ToArray());

        private static int PaintOrder(ReportBandPlacement placement)
            => placement.Band.Kind switch
            {
                ReportBandKind.PageHeader => 0,
                ReportBandKind.PageFooter => 2,
                _ => 1,
            };
    }
}

internal static class ReportBandLayoutMeasure
{
    public static double MeasureHeight(ReportBandInstance band, ITextMeasurer measurer)
    {
        var height = Math.Max(0, band.Height);
        foreach (var textBox in band.Elements.OfType<ReportTextBoxInstance>())
        {
            if (textBox.Source is not ReportTextBoxElement source)
            {
                continue;
            }

            var layout = ReportTextBoxLayouter.Layout(
                new ReportTextBoxLayoutRequest
                {
                    Id = textBox.ElementId,
                    X = 0,
                    Y = 0,
                    Width = source.Width,
                    Height = source.Height,
                    Padding = source.Padding ?? new ReportThickness(),
                    Border = source.Border,
                    HorizontalAlignment = source.HorizontalAlignment,
                    VerticalAlignment = source.VerticalAlignment,
                    LineSpacing = source.TextStyle.LineHeight,
                    CanGrow = source.CanGrow,
                    Runs = textBox.Runs.Select(run => new ReportRichTextRun(run.Text, source.TextStyle)).ToArray(),
                },
                measurer);
            height = Math.Max(height, source.Y + layout.ActualHeight);
        }

        return Math.Round(height, 4, MidpointRounding.AwayFromZero);
    }
}

#pragma warning restore MA0048
