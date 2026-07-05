using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframeThumbnailRendererTests
{
    [Fact]
    public void Render_DefaultSize_IsDeterministicSmallSvg()
    {
        var document = Document("Orders");
        document.Elements.Add(new WireframeElement
        {
            Id = "button",
            Type = "TmButton",
            X = 40,
            Y = 30,
            W = 120,
            H = 36
        });

        var first = WireframeThumbnailRenderer.Render(document);
        var second = WireframeThumbnailRenderer.Render(document);

        first.Should().Be(second);
        first.Should().StartWith("<svg");
        first.Should().Contain("viewBox=\"0 0 160 120\"");
        first.Should().Contain("width=\"160\"");
        first.Should().Contain("height=\"120\"");
        first.Should().Contain("data-elements=\"1\"");
    }

    [Fact]
    public void Render_CustomSize_UsesConfiguredDimensions()
    {
        var document = Document("Custom");

        var svg = WireframeThumbnailRenderer.Render(document, width: 240, height: 90);

        svg.Should().Contain("viewBox=\"0 0 240 90\"");
        svg.Should().Contain("width=\"240\"");
        svg.Should().Contain("height=\"90\"");
        svg.Should().Contain("<rect width=\"240\" height=\"90\"");
    }

    [Fact]
    public void Render_EmptyPage_RendersZeroElementThumbnail()
    {
        var document = Document("Empty");

        var svg = WireframeThumbnailRenderer.Render(document);

        svg.Should().Contain("data-elements=\"0\"");
        svg.Should().Contain(">Empty - 0</text>");
        CountElementRects(svg).Should().Be(0);
    }

    [Fact]
    public void Render_DensePage_RendersEveryElementDeterministically()
    {
        var document = Document("Dense");
        document.Width = 1000;
        document.Height = 800;
        for (var i = 0; i < 40; i++)
        {
            document.Elements.Add(new WireframeElement
            {
                Id = "e" + i,
                Type = "TmCard",
                X = (i % 10) * 90,
                Y = (i / 10) * 120,
                W = 70,
                H = 80
            });
        }

        var svg = WireframeThumbnailRenderer.Render(document);

        svg.Should().Contain("data-elements=\"40\"");
        CountElementRects(svg).Should().Be(40);
        svg.Should().Be(WireframeThumbnailRenderer.Render(document));
    }

    [Fact]
    public void Render_SanitizesDangerousTitleContent()
    {
        var document = Document(
            "<script>alert(1)</script><foreignObject>bad</foreignObject><b>safe</b>javascript:alert(2)");

        var svg = WireframeThumbnailRenderer.Render(document);

        svg.Contains("script", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        svg.Contains("foreignObject", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        svg.Contains("javascript:", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        svg.Contains("<b>", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        svg.Should().Contain("alert(1)badsafealert(2) - 0");
    }

    private static WireframeDocument Document(string title)
    {
        var document = new WireframeDocument { Title = title };
        document.EnsureActivePage();
        return document;
    }

    private static int CountElementRects(string svg)
        => svg.Split("<rect x=\"", StringSplitOptions.None).Length - 1;
}
