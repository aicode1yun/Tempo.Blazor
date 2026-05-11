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
    public void Surface_RendersParagraphBlock()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("Hello document"))));

        cut.Find(".tm-document-block--paragraph").TextContent.Should().Contain("Hello document");
    }

    [Fact]
    public void Surface_RendersHeadingBlocks()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(
                Heading(1, "Main title"),
                Heading(2, "Section title"))));

        cut.Find("h1.tm-document-block--heading").TextContent.Should().Contain("Main title");
        cut.Find("h2.tm-document-block--heading").TextContent.Should().Contain("Section title");
    }

    [Fact]
    public void Surface_RendersBulletAndNumberedLists()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(
                ListItem("Bullet item", ordered: false),
                ListItem("Numbered item", ordered: true))));

        cut.Find("ul.tm-document-list").TextContent.Should().Contain("Bullet item");
        cut.Find("ol.tm-document-list").TextContent.Should().Contain("Numbered item");
    }

    [Fact]
    public void Surface_RendersQuoteBlock()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Quote("Important clause"))));

        cut.Find("blockquote.tm-document-block--quote").TextContent.Should().Contain("Important clause");
    }

    [Fact]
    public void Surface_RendersTableWithMergedCells()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Table())));

        var merged = cut.Find("td[colspan='2']");
        merged.TextContent.Should().Contain("Merged heading");
        cut.FindAll("td").Should().HaveCount(3);
    }

    [Fact]
    public void Surface_RendersImageUrlBlockWithAltAndCaption()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(ImageUrl(SafePngDataUrl, "Chart preview", "Evidence image"))));

        var image = cut.Find("img.tm-document-image__media");
        image.GetAttribute("src").Should().Be(SafePngDataUrl);
        image.GetAttribute("alt").Should().Be("Chart preview");
        cut.Find("figcaption").TextContent.Should().Contain("Evidence image");
    }

    [Fact]
    public void Surface_RendersProviderImageBlock()
    {
        var resolver = new StaticImageResolver(SafePngDataUrl);

        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(ImageAsset("asset-1", "Provider image", "Uploaded image")))
                      .Add(p => p.ImageUrlResolver, resolver));

        cut.WaitForAssertion(() =>
            cut.Find("img.tm-document-image__media").GetAttribute("src").Should().Be(SafePngDataUrl));
        resolver.RequestedAssetIds.Should().Contain("asset-1");
    }

    [Fact]
    public void BlockRenderer_ShowsImageLoadingStateWhileProviderResolves()
    {
        var cut = RenderComponent<TmDocumentBlockRenderer>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Block, ImageAsset("asset-1", "Provider image", null))
                      .Add(p => p.ImageUrlResolver, new DelayedImageResolver()));

        cut.Find(".tm-document-image__loading").TextContent.Should().Contain("Loading image");
    }

    [Fact]
    public void Surface_RendersBrokenImageStateForUnsafeUrl()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(ImageUrl("javascript:alert(1)", "Unsafe", null))));

        cut.FindAll("img").Should().BeEmpty();
        cut.Find(".tm-document-image__broken").TextContent.Should().Contain("Image could not be loaded");
    }

    [Fact]
    public void Surface_RendersPageBreak()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Content = new PageBreakBlockContent()
            })));

        cut.Find(".tm-document-page-break").GetAttribute("role").Should().Be("separator");
    }

    [Fact]
    public void InlineRenderer_EncodesTextAndDoesNotRenderMarkup()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("<script>alert(1)</script>"))));

        cut.Markup.Should().NotContain("<script>");
        cut.Find(".tm-document-block--paragraph").TextContent.Should().Contain("<script>alert(1)</script>");
    }

    [Fact]
    public void InlineRenderer_RendersSafeLinkWithSecurityAttributes()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph(new TextRun
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
            }))));

        var link = cut.Find("a.tm-document-inline");
        link.GetAttribute("href").Should().Be("https://example.com");
        link.GetAttribute("target").Should().Be("_blank");
        link.GetAttribute("rel").Should().Be("noopener noreferrer");
        link.GetAttribute("title").Should().Be("Example");
    }

    [Fact]
    public void InlineRenderer_DoesNotRenderUnsafeLinkHref()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph(new TextRun
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
            }))));

        cut.FindAll("a").Should().BeEmpty();
        cut.Find(".tm-document-inline").TextContent.Should().Contain("Unsafe link");
    }

    [Fact]
    public void InlineRenderer_RendersTokenAsChipWithMetadata()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph(new TokenRun
            {
                Key = "client.name",
                DisplayName = "Client name",
                TokenType = "text",
                TypeLabel = "Text",
                Description = "Client full name"
            }))));

        var chip = cut.Find("[data-testid='document-token-chip']");
        chip.ClassList.Should().Contain("tm-document-inline--token");
        chip.GetAttribute("data-token-key").Should().Be("client.name");
        chip.GetAttribute("data-token-type").Should().Be("text");
        chip.GetAttribute("title").Should().Be("Client full name");
        chip.TextContent.Should().Contain("Client name");
    }

    [Fact]
    public void Surface_ReadOnlySurfaceIsNotEditable()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("Read only")))
                      .Add(p => p.ReadOnly, true));

        var surface = cut.Find(".tm-document-surface");
        surface.GetAttribute("contenteditable").Should().Be("false");
        surface.GetAttribute("aria-readonly").Should().Be("true");
    }

    [Fact]
    public void Surface_EditSurfaceHasTextboxAriaAttributes()
    {
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("Editable")))
                      .Add(p => p.ReadOnly, false));

        var surface = cut.Find(".tm-document-surface");
        surface.GetAttribute("role").Should().Be("textbox");
        surface.GetAttribute("aria-multiline").Should().Be("true");
        surface.GetAttribute("contenteditable").Should().Be("true");
    }

    [Fact]
    public void Surface_AttachesPasteHookWithLooseJsInterop()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("Editable")))
                      .Add(p => p.ReadOnly, false));

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditor.attachPaste");
    }

    [Fact]
    public void Surface_GracefullyRendersWhenPasteInteropIsUnavailable()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;

        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("Editable")))
                      .Add(p => p.ReadOnly, false));

        cut.Find(".tm-document-surface").Should().NotBeNull();
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditor.attachPaste");
    }

    [Fact]
    public async Task Surface_DisposeDoesNotThrowWhenPasteDetachInteropFails()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditor.attachPaste", _ => true).SetVoidResult();
        var cut = RenderComponent<TmDocumentSurface>(parameters =>
            parameters.Add(p => p.Document, CreateDocument(Paragraph("Editable")))
                      .Add(p => p.ReadOnly, false));

        var act = async () => await cut.Instance.DisposeAsync();

        await act.Should().NotThrowAsync();
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditor.detachPaste");
    }

    private static DocumentEditorDocument CreateDocument(params DocumentBlock[] blocks)
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Metadata.Title = "Renderer test";
        document.Blocks.AddRange(blocks.Select((block, index) =>
        {
            block.Order = (index + 1) * 10;
            return block;
        }));
        return document;
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

    private static DocumentBlock ImageUrl(string url, string alt, string? caption) => new()
    {
        Type = DocumentBlockType.Image,
        Content = new ImageBlockContent
        {
            Source = DocumentImageSource.Url,
            Url = url,
            AltText = alt,
            Caption = caption
        }
    };

    private static DocumentBlock ImageAsset(string assetId, string alt, string? caption) => new()
    {
        Type = DocumentBlockType.Image,
        Content = new ImageBlockContent
        {
            Source = DocumentImageSource.Asset,
            AssetId = assetId,
            AltText = alt,
            Caption = caption
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
