using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class BandPageCompositionTests
{
    [Fact]
    public void Compose_RepeatsPageHeaderFooterAndFlowsDetailBandsAcrossPages()
    {
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(200, 220),
                Margins = new ReportThickness(10),
            },
            Bands = new ReportBandCollection
            {
                PageHeader = Band(ReportBandKind.PageHeader, 20, "page-header"),
                PageFooter = Band(ReportBandKind.PageFooter, 20, "page-footer"),
            },
        };
        var reportHeader = Band(ReportBandKind.ReportHeader, 30, "report-header");
        var detail = Band(ReportBandKind.Detail, 40, "detail");
        var reportFooter = Band(ReportBandKind.ReportFooter, 30, "report-footer");
        var instance = new ReportInstance(
            definition,
            [
                Instance(reportHeader),
                Instance(detail),
                Instance(detail),
                Instance(detail),
                Instance(detail),
                Instance(reportFooter),
            ]);

        var composition = ReportPageComposer.Compose(instance, new FixedTextMeasurer());

        composition.Pages.Should().HaveCount(2);
        composition.Pages[0].ContentRectangle.Should().Be(new ReportLayoutRectangle(10, 30, 180, 160));
        composition.Pages[0].Placements.Should().Contain(placement => placement.Band.Kind == ReportBandKind.PageHeader && placement.Y == 10);
        composition.Pages[0].Placements.Should().Contain(placement => placement.Band.Kind == ReportBandKind.PageFooter && placement.Y == 190);
        composition.Pages[0].Placements.Count(placement => placement.Band.Kind == ReportBandKind.Detail).Should().Be(3);
        composition.Pages[1].Placements.Should().Contain(placement => placement.Band.Kind == ReportBandKind.PageHeader);
        composition.Pages[1].Placements.Should().Contain(placement => placement.Band.Kind == ReportBandKind.PageFooter);
        composition.Pages[1].Placements.Count(placement => placement.Band.Kind == ReportBandKind.Detail).Should().Be(1);
        composition.Pages[1].Placements.Should().Contain(placement => placement.Band.Kind == ReportBandKind.ReportFooter);
    }

    [Fact]
    public void Compose_MovesKeepTogetherBandToNextPageWhenItWouldCreateAnOrphan()
    {
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(160, 150),
                Margins = new ReportThickness(10),
            },
        };
        var first = Band(ReportBandKind.Detail, 108, "first");
        var keepTogether = Band(ReportBandKind.Detail, 30, "keep", keepTogether: true);
        var instance = new ReportInstance(definition, [Instance(first), Instance(keepTogether)]);

        var composition = ReportPageComposer.Compose(
            instance,
            new FixedTextMeasurer(),
            new ReportPageCompositionOptions { MinimumOrphanHeight = 24 });

        composition.Pages.Should().HaveCount(2);
        composition.Pages[1].Placements.Single(placement => placement.Band.SourceBand == keepTogether)
            .Y.Should().Be(composition.Pages[1].ContentRectangle.Y);
    }

    private static ReportBand Band(ReportBandKind kind, double height, string text, bool keepTogether = false)
        => new()
        {
            Kind = kind,
            Height = height,
            KeepTogether = keepTogether,
            Elements =
            [
                new ReportTextBoxElement
                {
                    Id = text,
                    Text = text,
                    X = 0,
                    Y = 0,
                    Width = 120,
                    Height = Math.Min(height, 16),
                    TextStyle = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 },
                },
            ],
        };

    private static ReportBandInstance Instance(ReportBand band)
    {
        var elements = band.Elements
            .Select(element => element is ReportTextBoxElement textBox
                ? new ReportTextBoxInstance(textBox, textBox.Text, textBox.Text ?? string.Empty)
                : new ReportElementInstance(element, null, null))
            .ToArray();
        return new ReportBandInstance(band.Kind, null, null, elements, sourceBand: band);
    }
}
