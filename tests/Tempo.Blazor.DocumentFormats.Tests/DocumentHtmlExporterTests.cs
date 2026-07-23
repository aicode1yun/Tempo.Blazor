using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Html;

namespace Tempo.Blazor.DocumentFormats.Tests;

public sealed class DocumentHtmlExporterTests
{
    [Fact]
    public void Export_RendersParagraphsHeadingsListsTablesAndTokens()
    {
        var document = DocumentFormatTestData.CreateDocument();
        document.Blocks.Insert(2, new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 1.5,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Client: " },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" }
                ]
            }
        });

        var html = new DocumentHtmlExporter().Export(document);

        html.Should().Contain("<h1>Agreement</h1>");
        html.Should().Contain("<p><strong>Bold</strong> and <a href=\"https://example.test\">link</a></p>");
        html.Should().Contain("data-token-key=\"client.name\"");
        html.Should().Contain("<ol><li>Numbered item</li></ol>");
        html.Should().Contain("<table><tbody>");
        html.Should().Contain("colspan=\"2\"");
        html.Should().Contain("<figcaption>Image caption</figcaption>");
    }

    [Fact]
    public void Export_EscapesTextAttributesAndUnsafeLinks()
    {
        var document = DocumentEditorDocument.Empty("html-test");
        document.Metadata.Title = "<Document>";
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Content = new HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new TextRun { Text = "<script>alert(1)</script>" }]
                }
            },
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Text = "unsafe",
                            Marks =
                            [
                                new InlineMark
                                {
                                    Type = InlineMarkType.Link,
                                    Link = new LinkMarkData { Href = "javascript:alert(1)" }
                                }
                            ]
                        }
                    ]
                }
            }
        ];

        var html = new DocumentHtmlExporter().Export(document, new DocumentHtmlExportOptions { IncludeDocumentWrapper = true });

        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
        html.Should().Contain("<title>&lt;Document&gt;</title>");
        html.Should().NotContain("<script>");
        html.Should().NotContain("javascript:");
    }

    [Theory]
    [InlineData("var(--evil)")]
    [InlineData("url(https://evil.test/x)")]
    [InlineData("red;position:fixed")]
    [InlineData("\" onmouseover=\"alert(1)")]
    public void Export_DropsUnsafeCssColors(string value)
    {
        var document = DocumentEditorDocument.Empty("unsafe-color");
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Text = "safe",
                            Marks =
                            [
                                new InlineMark
                                {
                                    Type = InlineMarkType.TextColor,
                                    Value = value
                                }
                            ]
                        }
                    ]
                }
            }
        ];

        var html = new DocumentHtmlExporter().Export(document);

        html.Should().Contain(">safe<");
        html.Should().NotContain("style=");
        html.Should().NotContain("onmouseover");
    }
}
