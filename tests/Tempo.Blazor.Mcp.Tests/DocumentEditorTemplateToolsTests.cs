using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorTemplateToolsTests
{
    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void TemplateTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorTemplateTools));
    }

    // ---------------------------------------------------------------- insert_token

    [Fact]
    public async Task InsertToken_SplitsRunAndInsertsTokenAtPlainOffset()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-token", "Vážený , vítejte.");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.InsertToken(
            provider, doc.DocumentId, "p1", 7, "customer.name",
            displayName: "Jméno zákazníka", fallbackText: "zákazníku", expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        ((TextRun)inlines[0]).Text.Should().Be("Vážený ");
        var token = (TokenRun)inlines[1];
        token.Key.Should().Be("customer.name");
        token.DisplayName.Should().Be("Jméno zákazníka");
        token.FallbackText.Should().Be("zákazníku");
        ((TextRun)inlines[2]).Text.Should().Be(", vítejte.");
    }

    [Fact]
    public async Task InsertToken_WithExpression_StoresExpression()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-token-expr", "Celkem: ");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.InsertToken(
            provider, doc.DocumentId, "p1", 8, "total",
            displayName: "Celkem", expression: "CURRENCY(SUM(items,'price'),'cs-CZ','CZK')"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        inlines.OfType<TokenRun>().Single().Expression.Should().Be("CURRENCY(SUM(items,'price'),'cs-CZ','CZK')");
    }

    [Fact]
    public async Task InsertToken_OffsetOutOfRange_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-token-range", "Krátký");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.InsertToken(
            provider, doc.DocumentId, "p1", 99, "k"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task InsertToken_UnknownKeyWithProvider_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-token-validate", "Text ");
        provider.Add(doc);
        var tokenProvider = new FakeTokenValueProvider(knownKeys: ["known.key"]);

        var root = Parse(await DocumentEditorTemplateTools.InsertToken(
            provider, doc.DocumentId, "p1", 5, "unknown.key", tokenProvider: tokenProvider));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        root.GetProperty("message").GetString().Should().Contain("unknown.key");
    }

    [Fact]
    public async Task InsertToken_UnknownKeyWithValidateKeyFalse_Inserts()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-token-novalidate", "Text ");
        provider.Add(doc);
        var tokenProvider = new FakeTokenValueProvider(knownKeys: ["known.key"]);

        var root = Parse(await DocumentEditorTemplateTools.InsertToken(
            provider, doc.DocumentId, "p1", 5, "unknown.key", tokenProvider: tokenProvider, validateKey: false));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- wrap_conditional

    [Fact]
    public async Task WrapConditional_WrapsBlocksIntoIfElseChain()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTwoParagraphDocument("doc-cond");
        provider.Add(doc);
        const string branches = """
            [
              {"branch": "if", "expression": "contract.amount > 10000", "blockIds": ["p1"]},
              {"branch": "else", "blockIds": ["p2"]}
            ]
            """;

        var root = Parse(await DocumentEditorTemplateTools.WrapConditional(
            provider, doc.DocumentId, branches));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var controlIds = root.GetProperty("controlBlockIds").EnumerateArray().Select(e => e.GetString()).ToList();
        controlIds.Should().HaveCount(2);

        var saved = await Load(provider, doc.DocumentId);
        saved.Blocks.Should().HaveCount(2);
        var ifBlock = (ContentControlBlockContent)saved.Blocks[0].Content;
        ifBlock.Control.Metadata[DocumentAssemblyMetadata.BranchKey].Should().Be("if");
        ifBlock.Control.Metadata[DocumentAssemblyMetadata.ExpressionKey].Should().Be("contract.amount > 10000");
        ifBlock.Blocks.Should().ContainSingle().Which.Id.Should().Be("p1");
        var elseBlock = (ContentControlBlockContent)saved.Blocks[1].Content;
        elseBlock.Control.Metadata[DocumentAssemblyMetadata.BranchKey].Should().Be("else");
        elseBlock.Blocks.Should().ContainSingle().Which.Id.Should().Be("p2");
        // Both branches share the chain group id.
        ifBlock.Control.Metadata[DocumentAssemblyMetadata.GroupKey]
            .Should().Be(elseBlock.Control.Metadata[DocumentAssemblyMetadata.GroupKey]);
    }

    [Fact]
    public async Task WrapConditional_FirstBranchNotIf_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTwoParagraphDocument("doc-cond-bad");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.WrapConditional(
            provider, doc.DocumentId, """[{"branch": "else", "blockIds": ["p1"]}]"""));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task WrapConditional_MissingBlock_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTwoParagraphDocument("doc-cond-missing");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.WrapConditional(
            provider, doc.DocumentId, """[{"branch": "if", "expression": "x", "blockIds": ["nope"]}]"""));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task WrapConditional_UpdateExistingBranchExpression_PatchesControl()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTwoParagraphDocument("doc-cond-update");
        provider.Add(doc);
        await DocumentEditorTemplateTools.WrapConditional(
            provider, doc.DocumentId,
            """[{"branch": "if", "expression": "old > 1", "blockIds": ["p1"]}, {"branch": "else", "blockIds": ["p2"]}]""");
        var saved = await Load(provider, doc.DocumentId);
        var controlBlockId = saved.Blocks[0].Id;

        var root = Parse(await DocumentEditorTemplateTools.WrapConditional(
            provider, doc.DocumentId, branchesJson: null,
            existingControlBlockId: controlBlockId, expression: "contract.amount >= 50000"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var updated = (ContentControlBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        updated.Control.Metadata[DocumentAssemblyMetadata.ExpressionKey].Should().Be("contract.amount >= 50000");
        updated.Blocks.Should().ContainSingle().Which.Id.Should().Be("p1");
    }

    // ---------------------------------------------------------------- insert_repeating_section

    [Fact]
    public async Task InsertRepeatingSection_WithRowText_CreatesBoundControl()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-repeat", "Položky:");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.InsertRepeatingSection(
            provider, doc.DocumentId, "items", rowText: "Položka faktury"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var saved = await Load(provider, doc.DocumentId);
        var control = (ContentControlBlockContent)saved.Blocks[^1].Content;
        control.Control.Metadata[DocumentAssemblyMetadata.BindKey].Should().Be("items");
        control.Blocks.Should().ContainSingle();
        ((ParagraphBlockContent)control.Blocks[0].Content).Inlines.OfType<TextRun>().Single().Text.Should().Be("Položka faktury");
    }

    [Fact]
    public async Task InsertRepeatingSection_WithRowBlocksJson_UsesProvidedTemplate()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-repeat-json", "Rozpis:");
        provider.Add(doc);
        var rowBlocks = new List<DocumentBlock>
        {
            new()
            {
                Id = "row-p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TokenRun { Key = "name", DisplayName = "Název" },
                        new TextRun { Text = " — " },
                        new TokenRun { Key = "price", DisplayName = "Cena" }
                    ]
                }
            }
        };

        var root = Parse(await DocumentEditorTemplateTools.InsertRepeatingSection(
            provider, doc.DocumentId, "items",
            rowBlocksJson: JsonSerializer.Serialize(rowBlocks, DocumentEditorJson.Options)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var control = (ContentControlBlockContent)(await Load(provider, doc.DocumentId)).Blocks[^1].Content;
        ((ParagraphBlockContent)control.Blocks[0].Content).Inlines.OfType<TokenRun>().Should().HaveCount(2);
    }

    [Fact]
    public async Task InsertRepeatingSection_NoTemplate_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-repeat-empty", "x");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.InsertRepeatingSection(
            provider, doc.DocumentId, "items"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    // ---------------------------------------------------------------- document_template_describe

    [Fact]
    public async Task TemplateDescribe_ListsTokensConditionalsAndRepeats()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTemplateDocument("doc-template");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.TemplateDescribe(provider, doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var tokens = root.GetProperty("tokens").EnumerateArray().Select(t => t.GetProperty("key").GetString()).ToList();
        tokens.Should().Contain("contract.client").And.Contain("total");

        var chains = root.GetProperty("conditionalChains").EnumerateArray().ToList();
        chains.Should().HaveCount(1);
        var branches = chains[0].GetProperty("branches").EnumerateArray().ToList();
        branches.Should().HaveCount(2);
        branches[0].GetProperty("branch").GetString().Should().Be("if");
        branches[0].GetProperty("expression").GetString().Should().Be("contract.amount > 10000");

        var repeats = root.GetProperty("repeatingSections").EnumerateArray().ToList();
        repeats.Should().ContainSingle();
        repeats[0].GetProperty("bindKey").GetString().Should().Be("items");
    }

    // ---------------------------------------------------------------- document_assemble_render + round-trip

    [Fact]
    public async Task RoundTrip_AuthoredTemplate_AssemblesWithBranchFlipAndSums()
    {
        var provider = new FakeDocumentEditorProvider();
        var renderer = CreateRenderer();
        var (catalog, options) = CreateFontRuntime();

        // 1. Author the template with the semantic tools.
        var doc = DocumentEditorDocument.Empty("doc-roundtrip");
        doc.Theme.BodyFontFamily = "Dancing Script";
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "intro",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Klient: " }] }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "vip",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Schvaluje reditel spolecnosti." }] }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "std",
            Type = DocumentBlockType.Paragraph,
            Order = 2,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Schvaluje bezny manazer." }] }
        });
        provider.Add(doc);

        Parse(await DocumentEditorTemplateTools.InsertToken(
            provider, doc.DocumentId, "intro", 8, "contract.client")).GetProperty("success").GetBoolean().Should().BeTrue();
        Parse(await DocumentEditorTemplateTools.WrapConditional(
            provider, doc.DocumentId,
            """[{"branch": "if", "expression": "contract.amount > 10000", "blockIds": ["vip"]}, {"branch": "else", "blockIds": ["std"]}]"""))
            .GetProperty("success").GetBoolean().Should().BeTrue();
        Parse(await DocumentEditorTemplateTools.InsertRepeatingSection(
            provider, doc.DocumentId, "items", rowText: "Polozka")).GetProperty("success").GetBoolean().Should().BeTrue();

        // 2. template_describe reflects the authored structure.
        var described = Parse(await DocumentEditorTemplateTools.TemplateDescribe(provider, doc.DocumentId));
        described.GetProperty("conditionalChains").EnumerateArray().Should().HaveCount(1);
        described.GetProperty("repeatingSections").EnumerateArray().Should().HaveCount(1);

        // 3. Assemble with two datasets: the branch flips and the repeat expands.
        const string bigDataset = """
            {
              "contract.client": "Acme s.r.o.",
              "contract.amount": "25000",
              "items": {"rows": [{"name": "A", "price": "15000"}, {"name": "B", "price": "10000"}]}
            }
            """;
        var big = Parse(await DocumentEditorTemplateTools.AssembleRender(
            provider, renderer, catalog, options,
            documentId: doc.DocumentId, tokenValuesJson: bigDataset, output: "pdf", includeLayoutText: true));
        big.GetProperty("success").GetBoolean().Should().BeTrue();
        var bigText = big.GetProperty("layoutText").GetString()!;
        bigText.Should().Contain("Acme");
        bigText.Should().Contain("reditel");
        bigText.Should().NotContain("bezny");
        bigText.Should().Contain("Polozka", "repeating section must expand per row");

        const string smallDataset = """
            {"contract.client": "Beta a.s.", "contract.amount": "5000", "items": {"rows": [{"name": "C", "price": "5000"}]}}
            """;
        var small = Parse(await DocumentEditorTemplateTools.AssembleRender(
            provider, renderer, catalog, options,
            documentId: doc.DocumentId, tokenValuesJson: smallDataset, output: "pdf", includeLayoutText: true));
        var smallText = small.GetProperty("layoutText").GetString()!;
        smallText.Should().Contain("bezny");
        smallText.Should().NotContain("reditel");
    }

    [Fact]
    public async Task AssembleRender_PngOutput_ReturnsPages()
    {
        var provider = new FakeDocumentEditorProvider();
        var renderer = CreateRenderer();
        var (catalog, options) = CreateFontRuntime();
        var doc = BuildDocument("doc-assemble-png", "Prosty text.");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.AssembleRender(
            provider, renderer, catalog, options, documentId: doc.DocumentId, output: "png"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("renderedPages").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task AssembleRender_InvalidTokenValuesJson_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var renderer = CreateRenderer();
        var (catalog, options) = CreateFontRuntime();
        var doc = BuildDocument("doc-assemble-bad", "x");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.AssembleRender(
            provider, renderer, catalog, options, documentId: doc.DocumentId, tokenValuesJson: "{broken"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task AssembleRender_UnknownOutput_ReturnsInvalidOperation()
    {
        var provider = new FakeDocumentEditorProvider();
        var renderer = CreateRenderer();
        var (catalog, options) = CreateFontRuntime();
        var doc = BuildDocument("doc-assemble-out", "x");
        provider.Add(doc);

        var root = Parse(await DocumentEditorTemplateTools.AssembleRender(
            provider, renderer, catalog, options, documentId: doc.DocumentId, output: "docx"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
    }

    // ---------------------------------------------------------------- helpers

    private sealed class FakeTokenValueProvider(IReadOnlyList<string> knownKeys) : Tempo.Blazor.DocumentEditor.Interfaces.IDocumentTokenValueProvider
    {
        public Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
            DocumentTokenResolutionContext context,
            IReadOnlyList<TokenRun> tokens,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, DocumentTokenValue> result = tokens
                .Where(token => knownKeys.Contains(token.Key))
                .DistinctBy(token => token.Key)
                .ToDictionary(token => token.Key, token => DocumentTokenValue.Resolved(token.Key, "value"));
            return Task.FromResult(result);
        }
    }

    private static ITempoDocumentService CreateRenderer()
        => new TempoDocumentService(new JintDocumentLayoutEngine(), new FakeTimeProvider());

    private sealed class FakeTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
    }

    private static (ITempoDocumentMcpFontCatalog Catalog, TempoDocumentMcpRenderOptions Options) CreateFontRuntime()
    {
        var options = new TempoDocumentMcpRenderOptions { IncludeSystemFontFallback = false };
        options.Fonts.Add(new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath)));
        return (new TempoDocumentMcpFontCatalog(options), options);
    }

    private static async Task<DocumentEditorDocument> Load(FakeDocumentEditorProvider provider, string documentId)
        => (await provider.LoadAsync(documentId)).Document!;

    private static DocumentEditorDocument BuildDocument(string documentId, string text)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Theme.BodyFontFamily = "Dancing Script";
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] }
        });
        return doc;
    }

    private static DocumentEditorDocument BuildTwoParagraphDocument(string documentId)
    {
        var doc = BuildDocument(documentId, "První odstavec");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p2",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Druhý odstavec" }] }
        });
        return doc;
    }

    private static DocumentEditorDocument BuildTemplateDocument(string documentId)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Klient: " },
                    new TokenRun { Key = "contract.client", DisplayName = "Klient" },
                    new TokenRun { Key = "total", DisplayName = "Celkem", Expression = "SUM(items,'price')" }
                ]
            }
        });

        var ifControl = DocumentAssemblyMetadata.CreateConditionalBlock("if", "contract.amount > 10000", "chain-1");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "cc-if",
            Type = DocumentBlockType.ContentControl,
            Order = 1,
            Content = new ContentControlBlockContent
            {
                Control = ifControl,
                Blocks = [new DocumentBlock { Id = "vip", Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "VIP" }] } }]
            }
        });
        var elseControl = DocumentAssemblyMetadata.CreateConditionalBlock("else", null, "chain-1");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "cc-else",
            Type = DocumentBlockType.ContentControl,
            Order = 2,
            Content = new ContentControlBlockContent
            {
                Control = elseControl,
                Blocks = [new DocumentBlock { Id = "std", Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Standard" }] } }]
            }
        });

        var repeat = DocumentAssemblyMetadata.CreateRepeatingSection("items");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "cc-repeat",
            Type = DocumentBlockType.ContentControl,
            Order = 3,
            Content = new ContentControlBlockContent
            {
                Control = repeat,
                Blocks = [new DocumentBlock { Id = "row", Content = new ParagraphBlockContent { Inlines = [new TokenRun { Key = "name", DisplayName = "Název" }] } }]
            }
        });

        return doc;
    }
}
