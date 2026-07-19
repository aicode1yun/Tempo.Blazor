using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentRendererTests : LocalizationTestBase
{
    private const string SafePngDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void BlockRenderer_RendersParagraphBlock()
    {
        var cut = RenderBlock(Paragraph("Hello document"));

        cut.Find(".tm-document-block--paragraph").TextContent.Should().Contain("Hello document");
    }

    [Fact]
    public void BlockRenderer_RendersHeadingBlock()
    {
        var cut = RenderBlock(Heading(2, "Section title"));

        cut.Find("h2.tm-document-block--heading").TextContent.Should().Contain("Section title");
    }

    [Fact]
    public void BlockRenderer_RendersBulletAndNumberedLists()
    {
        var bullet = RenderBlock(ListItem("Bullet item", ordered: false));
        var ordered = RenderBlock(ListItem("Numbered item", ordered: true));

        bullet.Find("ul.tm-document-list").TextContent.Should().Contain("Bullet item");
        ordered.Find("ol.tm-document-list").TextContent.Should().Contain("Numbered item");
    }

    [Fact]
    public void BlockRenderer_RendersQuoteBlock()
    {
        var cut = RenderBlock(Quote("Important clause"));

        cut.Find("blockquote.tm-document-block--quote").TextContent.Should().Contain("Important clause");
    }

    [Fact]
    public void BlockRenderer_RendersTableWithMergedCells()
    {
        var cut = RenderBlock(Table());

        var merged = cut.Find("td[colspan='2']");
        merged.TextContent.Should().Contain("Merged heading");
        cut.FindAll("td").Should().HaveCount(3);
    }

    [Fact]
    public void BlockRenderer_RendersDrawingRunUrlImageWithAltAndCaption()
    {
        var cut = RenderBlock(Paragraph(
            new TextRun { Text = "Before " },
            DrawingUrl("drawing-url", SafePngDataUrl, "Chart preview", "Evidence image"),
            new TextRun { Text = " after." }));

        var drawing = cut.Find("[data-testid='document-drawing-object'][data-object-id='drawing-url']");
        var image = drawing.QuerySelector("img.tm-document-image__media");
        image.GetAttribute("src").Should().Be(SafePngDataUrl);
        image.GetAttribute("alt").Should().Be("Chart preview");
        drawing.TextContent.Should().Contain("Evidence image");
    }

    [Fact]
    public void BlockRenderer_RendersProviderDrawingRun()
    {
        var resolver = new StaticImageResolver(SafePngDataUrl);

        var cut = RenderBlock(Paragraph(DrawingAsset("drawing-asset", "asset-1", "Provider image", "Uploaded image")), resolver);

        cut.WaitForAssertion(() =>
            cut.Find("img.tm-document-image__media").GetAttribute("src").Should().Be(SafePngDataUrl));
        resolver.RequestedAssetIds.Should().Contain("asset-1");
    }

    [Fact]
    public void BlockRenderer_ShowsDrawingRunLoadingStateWhileProviderResolves()
    {
        var cut = RenderBlock(Paragraph(DrawingAsset("drawing-loading", "asset-1", "Provider image", null)), new DelayedImageResolver());

        cut.Find(".tm-document-image__loading").TextContent.Should().Contain("Loading image");
    }

    [Fact]
    public void BlockRenderer_RendersBrokenDrawingStateForUnsafeUrl()
    {
        var cut = RenderBlock(Paragraph(DrawingUrl("drawing-unsafe", "javascript:alert(1)", "Unsafe", null)));

        cut.FindAll("img").Should().BeEmpty();
        cut.Find(".tm-document-image__broken").TextContent.Should().Contain("Image could not be loaded");
    }

    [Fact]
    public void BlockRenderer_RendersPageBreak()
    {
        var cut = RenderBlock(new DocumentBlock
        {
            Type = DocumentBlockType.PageBreak,
            Content = new PageBreakBlockContent()
        });

        cut.Find(".tm-document-page-break").GetAttribute("role").Should().Be("separator");
    }

    [Fact]
    public void InlineRenderer_EncodesTextAndDoesNotRenderMarkup()
    {
        var cut = RenderBlock(Paragraph("<script>alert(1)</script>"));

        cut.Markup.Should().NotContain("<script>");
        cut.Find(".tm-document-block--paragraph").TextContent.Should().Contain("<script>alert(1)</script>");
    }

    [Fact]
    public void InlineRenderer_RendersSafeLinkWithSecurityAttributes()
    {
        var cut = RenderBlock(Paragraph(new TextRun
        {
            Text = "Open link",
            Marks =
            [
                new InlineMark
                {
                    Type = InlineMarkType.Link,
                    Link = new LinkMarkData { Href = "https://example.com", Title = "Example" }
                }
            ]
        }));

        var link = cut.Find("a.tm-document-inline");
        link.GetAttribute("href").Should().Be("https://example.com");
        link.GetAttribute("target").Should().Be("_blank");
        link.GetAttribute("rel").Should().Be("noopener noreferrer");
        link.GetAttribute("title").Should().Be("Example");
    }

    [Fact]
    public void InlineRenderer_DoesNotRenderUnsafeLinkHref()
    {
        var cut = RenderBlock(Paragraph(new TextRun
        {
            Text = "Unsafe link",
            Marks =
            [
                new InlineMark
                {
                    Type = InlineMarkType.Link,
                    Link = new LinkMarkData { Href = "javascript:alert(1)" }
                }
            ]
        }));

        cut.FindAll("a").Should().BeEmpty();
        cut.Find(".tm-document-inline").TextContent.Should().Contain("Unsafe link");
    }

    [Fact]
    public void InlineRenderer_RendersTokenAsChipWithMetadata()
    {
        var cut = RenderBlock(Paragraph(new TokenRun
        {
            Key = "client.name",
            DisplayName = "Client name",
            TokenType = "text",
            TypeLabel = "Text",
            Description = "Client full name"
        }));

        var chip = cut.Find("[data-testid='document-token-chip']");
        chip.ClassList.Should().Contain("tm-document-inline--token");
        chip.GetAttribute("data-token-key").Should().Be("client.name");
        chip.GetAttribute("data-token-type").Should().Be("text");
        chip.GetAttribute("title").Should().Be("Client full name");
        chip.TextContent.Should().Contain("Client name");
    }

    private IRenderedComponent<TmDocumentBlockRenderer> RenderBlock(
        DocumentBlock block,
        IDocumentImageUrlResolver? resolver = null)
    {
        return Render<TmDocumentBlockRenderer>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Block, block)
            .Add(p => p.ImageUrlResolver, resolver));
    }

    private static DocumentBlock Paragraph(string text) => Paragraph(new TextRun { Text = text });

    private static DocumentBlock Paragraph(params InlineContent[] inlines) => new()
    {
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent { Inlines = [.. inlines] }
    };

    private static DocumentBlock Heading(int level, string text) => new()
    {
        Type = DocumentBlockType.Heading,
        Content = new HeadingBlockContent
        {
            Level = level,
            Inlines = [new TextRun { Text = text }]
        }
    };

    private static DocumentBlock ListItem(string text, bool ordered) => new()
    {
        Type = DocumentBlockType.List,
        Content = new ListBlockContent
        {
            Ordered = ordered,
            Inlines = [new TextRun { Text = text }]
        }
    };

    private static DocumentBlock Quote(string text) => new()
    {
        Type = DocumentBlockType.Quote,
        Content = new QuoteBlockContent
        {
            Inlines = [new TextRun { Text = text }]
        }
    };

    private static DocumentBlock Table() => new()
    {
        Type = DocumentBlockType.Table,
        Content = new TableBlockContent
        {
            Rows =
            [
                new TableRowContent
                {
                    Cells =
                    [
                        new TableCellContent
                        {
                            ColumnSpan = 2,
                            Blocks = [Paragraph("Merged heading")]
                        },
                        new TableCellContent
                        {
                            Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "merged" }
                        }
                    ]
                },
                new TableRowContent
                {
                    Cells =
                    [
                        new TableCellContent { Blocks = [Paragraph("A1")] },
                        new TableCellContent { Blocks = [Paragraph("B1")] }
                    ]
                }
            ]
        }
    };

    private static DocumentDrawingRun DrawingUrl(string objectId, string url, string alt, string? caption) => new()
    {
        Id = $"{objectId}-run",
        ObjectId = objectId,
        Source = DocumentImageSource.Url,
        Url = url,
        AltText = alt,
        Caption = caption,
        Size = new DocumentImageSize { Width = 120, Height = 80 },
        Layout = DocumentObjectLayout.Inline()
    };

    private static DocumentDrawingRun DrawingAsset(string objectId, string assetId, string alt, string? caption) => new()
    {
        Id = $"{objectId}-run",
        ObjectId = objectId,
        Source = DocumentImageSource.Asset,
        AssetId = assetId,
        AltText = alt,
        Caption = caption,
        Size = new DocumentImageSize { Width = 120, Height = 80 },
        Layout = new DocumentObjectLayout
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square },
            Position = new DocumentObjectPosition { X = 12, Y = 8 },
            Transform = new DocumentObjectTransform { Width = 120, Height = 80 }
        }
    };

    private sealed class StaticImageResolver(string url) : IDocumentImageUrlResolver
    {
        public List<string> RequestedAssetIds { get; } = [];

        public Task<string> ResolveUrlAsync(string documentId, string assetId, CancellationToken cancellationToken = default)
        {
            RequestedAssetIds.Add(assetId);
            return Task.FromResult(url);
        }
    }

    private sealed class DelayedImageResolver : IDocumentImageUrlResolver
    {
        private readonly TaskCompletionSource<string> _completion = new();

        public Task<string> ResolveUrlAsync(string documentId, string assetId, CancellationToken cancellationToken = default)
            => _completion.Task;
    }
}
