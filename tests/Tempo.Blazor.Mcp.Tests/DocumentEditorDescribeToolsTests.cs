using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorDescribeToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void DescribeTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorDescribeTools));
    }

    [Fact]
    public async Task DescribeDocument_MissingDocument_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, "missing"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task DescribeDocument_ReturnsBlockAddressesTypesAndText()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-describe");
        doc.Metadata.Title = "Smlouva";
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "h1",
            Type = DocumentBlockType.Heading,
            Order = 0,
            Content = new HeadingBlockContent
            {
                Level = 2,
                Inlines = [new TextRun { Text = "Nájemní smlouva" }]
            }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Pronajímatel pronajímá nájemci byt." }]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("id").GetString().Should().Be("doc-describe");
        root.GetProperty("concurrencyToken").GetString().Should().Be("v1");
        root.GetProperty("metadata").GetProperty("title").GetString().Should().Be("Smlouva");

        var blocks = root.GetProperty("blocks").EnumerateArray().ToList();
        blocks.Should().HaveCount(2);

        var heading = blocks[0];
        heading.GetProperty("blockId").GetString().Should().Be("h1");
        heading.GetProperty("type").GetString().Should().Be("heading");
        heading.GetProperty("level").GetInt32().Should().Be(2);
        heading.GetProperty("text").GetString().Should().Be("Nájemní smlouva");
        heading.GetProperty("textLength").GetInt32().Should().Be("Nájemní smlouva".Length);
        heading.GetProperty("address").GetProperty("container").GetString().Should().Be("body");
        heading.GetProperty("address").GetProperty("operationAddressable").GetBoolean().Should().BeTrue();

        var paragraph = blocks[1];
        paragraph.GetProperty("blockId").GetString().Should().Be("p1");
        paragraph.GetProperty("type").GetString().Should().Be("paragraph");
        paragraph.GetProperty("order").GetDouble().Should().Be(1);
    }

    [Fact]
    public async Task DescribeDocument_LongText_IsTruncatedByMaxTextLength()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-truncate");
        var longText = new string('a', 500);
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = longText }] }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId, maxTextLength: 10));

        var block = root.GetProperty("blocks")[0];
        block.GetProperty("text").GetString().Should().Be(new string('a', 10) + "…");
        block.GetProperty("textLength").GetInt32().Should().Be(500);
        block.GetProperty("textTruncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DescribeDocument_ZeroAndNegativeMaxTextLength_YieldEmptyPreviewButFullLength()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-zero");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Hello" }] }
        });
        provider.Add(doc);

        foreach (var max in new[] { 0, -5 })
        {
            var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId, maxTextLength: max));
            var block = root.GetProperty("blocks")[0];
            block.GetProperty("text").GetString().Should().Be("…");
            block.GetProperty("textLength").GetInt32().Should().Be(5);
            block.GetProperty("textTruncated").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task DescribeDocument_Table_ExposesCellIdsAndNestedBlockAddresses()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-table");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "t1",
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
                                Id = "cell-1",
                                ColumnSpan = 2,
                                IsHeader = true,
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Id = "nested-p",
                                        Type = DocumentBlockType.Paragraph,
                                        Content = new ParagraphBlockContent
                                        {
                                            Inlines = [new TextRun { Text = "Nájemné" }]
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        var table = root.GetProperty("blocks")[0];
        table.GetProperty("type").GetString().Should().Be("table");
        table.GetProperty("rowCount").GetInt32().Should().Be(1);

        var cell = table.GetProperty("rows")[0].GetProperty("cells")[0];
        cell.GetProperty("cellId").GetString().Should().Be("cell-1");
        cell.GetProperty("columnSpan").GetInt32().Should().Be(2);
        cell.GetProperty("isHeader").GetBoolean().Should().BeTrue();

        var nested = cell.GetProperty("blocks")[0];
        nested.GetProperty("blockId").GetString().Should().Be("nested-p");
        nested.GetProperty("text").GetString().Should().Be("Nájemné");
        var address = nested.GetProperty("address");
        address.GetProperty("container").GetString().Should().Be("tableCell");
        address.GetProperty("tableBlockId").GetString().Should().Be("t1");
        address.GetProperty("tableCellId").GetString().Should().Be("cell-1");
        address.GetProperty("operationAddressable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DescribeDocument_TokensAndFields_AreListedWithOccurrences()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-tokens");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Nájemce: " },
                    new TokenRun { Key = "tenant.name", DisplayName = "Jméno nájemce", FallbackText = "—" },
                    new TextRun { Text = ", celkem " },
                    new TokenRun { Key = "total", DisplayName = "Celkem", Expression = "SUM(items,'price')" },
                    new DocumentFieldRun { FieldType = DocumentFieldType.PageNumber, DisplayText = "1" }
                ]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        var tokens = root.GetProperty("tokens").EnumerateArray().ToList();
        tokens.Should().HaveCount(2);
        var tenant = tokens.Single(t => t.GetProperty("key").GetString() == "tenant.name");
        tenant.GetProperty("displayName").GetString().Should().Be("Jméno nájemce");
        tenant.GetProperty("occurrences")[0].GetProperty("blockId").GetString().Should().Be("p1");
        tenant.GetProperty("occurrences")[0].GetProperty("inlineIndex").GetInt32().Should().Be(1);
        var total = tokens.Single(t => t.GetProperty("key").GetString() == "total");
        total.GetProperty("expression").GetString().Should().Be("SUM(items,'price')");

        // Plain text counts ONLY text runs — tokens/fields are separate objects.
        var block = root.GetProperty("blocks")[0];
        block.GetProperty("text").GetString().Should().Be("Nájemce: , celkem ");
        var objects = block.GetProperty("objects").EnumerateArray().ToList();
        objects.Should().HaveCount(3);
        objects[0].GetProperty("kind").GetString().Should().Be("token");
        objects[0].GetProperty("inlineIndex").GetInt32().Should().Be(1);
        objects[2].GetProperty("kind").GetString().Should().Be("field");
    }

    [Fact]
    public async Task DescribeDocument_ContentControls_ListedWithAssemblyMetadata()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-cc");
        var control = DocumentAssemblyMetadata.CreateRepeatingSection("items");
        control.ControlId = "cc-1";
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "cc-block",
            Type = DocumentBlockType.ContentControl,
            Content = new ContentControlBlockContent
            {
                Control = control,
                Blocks =
                [
                    new DocumentBlock
                    {
                        Id = "cc-child",
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent
                        {
                            Inlines = [new TokenRun { Key = "item.price", DisplayName = "Cena" }]
                        }
                    }
                ]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        var controls = root.GetProperty("contentControls").EnumerateArray().ToList();
        controls.Should().HaveCount(1);
        controls[0].GetProperty("controlId").GetString().Should().Be("cc-1");
        controls[0].GetProperty("blockId").GetString().Should().Be("cc-block");
        controls[0].GetProperty("kind").GetString().Should().Be("repeatingSection");
        controls[0].GetProperty("assembly").GetProperty("bind").GetString().Should().Be("items");

        var ccBlock = root.GetProperty("blocks")[0];
        ccBlock.GetProperty("type").GetString().Should().Be("contentControl");
        var child = ccBlock.GetProperty("blocks")[0];
        child.GetProperty("blockId").GetString().Should().Be("cc-child");
        var address = child.GetProperty("address");
        address.GetProperty("container").GetString().Should().Be("contentControl");
        address.GetProperty("contentControlBlockId").GetString().Should().Be("cc-block");
        // Content-control children are operation-addressable (both appliers resolve them).
        address.GetProperty("operationAddressable").GetBoolean().Should().BeTrue();

        // The nested token is still aggregated.
        root.GetProperty("tokens").EnumerateArray()
            .Select(t => t.GetProperty("key").GetString())
            .Should().Contain("item.price");
    }

    [Fact]
    public async Task DescribeDocument_HeadersFooters_AreIncludedWithAddresses()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-hf");
        doc.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "hdr-1",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "hdr-p",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Text = "Strana" }]
                    }
                }
            ]
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        var headersFooters = root.GetProperty("headersFooters").EnumerateArray().ToList();
        headersFooters.Should().HaveCount(1);
        headersFooters[0].GetProperty("id").GetString().Should().Be("hdr-1");
        headersFooters[0].GetProperty("type").GetString().Should().Be("header");

        var block = headersFooters[0].GetProperty("blocks")[0];
        block.GetProperty("blockId").GetString().Should().Be("hdr-p");
        var address = block.GetProperty("address");
        address.GetProperty("container").GetString().Should().Be("headerFooter");
        address.GetProperty("headerFooterId").GetString().Should().Be("hdr-1");
        address.GetProperty("operationAddressable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DescribeDocument_ContentDigest_IsStableAndChangesWithContent()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-digest");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Original" }] }
        });
        provider.Add(doc);

        var first = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));
        var second = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        var digest1 = first.GetProperty("contentDigest").GetString();
        digest1.Should().NotBeNullOrWhiteSpace();
        digest1.Should().MatchRegex("^[0-9a-f]{64}$");
        second.GetProperty("contentDigest").GetString().Should().Be(digest1);

        ((TextRun)((ParagraphBlockContent)doc.Blocks[0].Content).Inlines[0]).Text = "Changed";

        var third = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));
        third.GetProperty("contentDigest").GetString().Should().NotBe(digest1);
    }

    [Fact]
    public async Task DescribeDocument_Statistics_CountBlocksTablesTokensAndHeadersFooters()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-stats");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TokenRun { Key = "k1", DisplayName = "K1" }]
            }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "t1",
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
                                Id = "c1",
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Id = "np",
                                        Type = DocumentBlockType.Paragraph,
                                        Content = new ParagraphBlockContent()
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });
        doc.HeadersFooters.Add(new DocumentHeaderFooter { Id = "hf1", Blocks = [] });
        provider.Add(doc);

        var root = Parse(await DocumentEditorDescribeTools.DescribeDocument(provider, doc.DocumentId));

        var stats = root.GetProperty("statistics");
        stats.GetProperty("bodyBlockCount").GetInt32().Should().Be(2);
        stats.GetProperty("totalBlockCount").GetInt32().Should().Be(3);
        stats.GetProperty("tableCount").GetInt32().Should().Be(1);
        stats.GetProperty("tokenCount").GetInt32().Should().Be(1);
        stats.GetProperty("headerFooterCount").GetInt32().Should().Be(1);
    }
}
