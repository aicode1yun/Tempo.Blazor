using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Xunit.Abstractions;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Perf plan N10 — document editor serialization must resolve through source-generated
/// <see cref="JsonSerializerContext"/> metadata (reflection stays only as a chained fallback),
/// and the JSON output must stay byte-identical to the reflection serializer so persisted
/// snapshots, hashes (N8), and golden fixtures do not shift.
/// </summary>
public class DocumentEditorJsonSourceGenTests
{
    private readonly ITestOutputHelper _output;

    public DocumentEditorJsonSourceGenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------------------------------------------------------------------
    // N10.1/N10.2 — resolver chain wiring
    // ---------------------------------------------------------------------

    [Fact]
    public void Options_ResolveThroughASourceGeneratedContext()
    {
        DocumentEditorJson.Options.TypeInfoResolverChain
            .Should().Contain(resolver => resolver is JsonSerializerContext,
                "DocumentEditorJson.Options must use source-generated metadata (perf plan N10.2)");

        DocumentEditorJson.Options.TypeInfoResolverChain.Last()
            .Should().BeOfType<DefaultJsonTypeInfoResolver>(
                "reflection must remain as the safe fallback at the end of the chain");
    }

    [Fact]
    public void IndentedOptions_InheritTheSourceGeneratedChain()
    {
        DocumentEditorJson.IndentedOptions.TypeInfoResolverChain
            .Should().Contain(resolver => resolver is JsonSerializerContext,
                "the indented debug variant copies Options and must keep the source-gen chain");
    }

    [Fact]
    public void Context_ProvidesMetadataForTheDocumentRootAndContractRoots()
    {
        var context = DocumentEditorJson.Options.TypeInfoResolverChain
            .OfType<JsonSerializerContext>().First();

        foreach (var rootType in new[]
                 {
                     typeof(DocumentEditorDocument),
                     typeof(DocumentEditorSaveRequest),
                     typeof(DocumentEditorSaveResult),
                     typeof(DocumentOfflineDraft),
                     typeof(DocumentComment),
                     typeof(DocumentRevision),
                     typeof(DocumentVersion),
                     typeof(WysiwygPatch),
                     typeof(WysiwygSelectionSnapshot),
                     typeof(DocumentOperationBatch),
                     typeof(DocumentOperation),
                 })
        {
            context.GetTypeInfo(rootType).Should().NotBeNull(
                $"the source-generated context must cover contract root {rootType.Name} (perf plan N10.1)");
        }
    }

    [Fact]
    public void CanvasJsonOptions_ResolveThroughASourceGeneratedContext()
    {
        TmDocumentCanvasEngineHost.CanvasJsonOptions.TypeInfoResolverChain
            .Should().Contain(resolver => resolver is JsonSerializerContext,
                "the canvas interop boundary must use source-generated metadata (perf plan N10.3)");

        TmDocumentCanvasEngineHost.CanvasJsonOptions.TypeInfoResolverChain.Last()
            .Should().BeOfType<DefaultJsonTypeInfoResolver>(
                "reflection must remain as the safe fallback for anonymous option payloads");
    }

    // ---------------------------------------------------------------------
    // N10.5 — golden byte-identity vs the reflection serializer
    // ---------------------------------------------------------------------

    [Fact]
    public void Serialize_IsByteIdenticalToTheReflectionSerializer()
    {
        var document = CreateComprehensiveDocument();

        var reflectionOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        var actual = JsonSerializer.Serialize(document, DocumentEditorJson.Options);
        var expected = JsonSerializer.Serialize(document, reflectionOptions);

        actual.Should().Be(expected,
            "source-generated metadata must not change a single byte of the persisted snapshot format");
    }

    [Fact]
    public void SerializeDeserializeSerialize_IsStable()
    {
        var document = CreateComprehensiveDocument();

        // Compare the second and third generations: the first Deserialize applies normalization
        // (default sections, headers/footers), later round-trips must be idempotent.
        var normalized = DocumentEditorJson.Serialize(DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(document)));
        var again = DocumentEditorJson.Serialize(DocumentEditorJson.Deserialize(normalized));

        again.Should().Be(normalized, "normalized round-trips must stay idempotent under source-gen");
    }

    [Fact]
    public void Deserialize_RoundTripsEveryDerivedBlockAndRunType()
    {
        var document = CreateComprehensiveDocument();
        var roundTripped = DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(document));

        var blockContentTypes = roundTripped.Blocks.Select(b => b.Content.GetType()).ToList();
        blockContentTypes.Should().Contain(
        [
            typeof(ParagraphBlockContent), typeof(HeadingBlockContent), typeof(ListBlockContent),
            typeof(QuoteBlockContent), typeof(TableBlockContent), typeof(ImageBlockContent),
            typeof(PageBreakBlockContent), typeof(ContentControlBlockContent),
        ], "every $type discriminator must survive a source-gen round-trip");

        var paragraph = roundTripped.Blocks
            .Select(b => b.Content).OfType<ParagraphBlockContent>().First();
        paragraph.Inlines.Select(i => i.GetType()).Should().Contain(
        [
            typeof(TextRun), typeof(TokenRun), typeof(DocumentFieldRun),
            typeof(DocumentNoteReferenceRun), typeof(DocumentDrawingRun), typeof(DocumentMathRun),
            typeof(DocumentContentControlRun), typeof(DocumentSigningFieldRun),
        ], "every inline run discriminator must survive a source-gen round-trip");
    }

    [Fact]
    public void CanvasModel_SerializationIsByteIdenticalToTheReflectionSerializer()
    {
        var canvasModel = CanvasDocumentModelConverter.ToCanvasModel(CreateComprehensiveDocument());

        var reflectionOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        var actual = JsonSerializer.Serialize(canvasModel, TmDocumentCanvasEngineHost.CanvasJsonOptions);
        var expected = JsonSerializer.Serialize(canvasModel, reflectionOptions);

        actual.Should().Be(expected,
            "the canvas interop payload (camelCase properties + camelCase string enums) must not shift");
    }

    [Fact]
    public void CanvasSelectionState_RoundTripsThroughCanvasOptions()
    {
        const string json = """
            {"isCollapsed":false,"anchorBlockId":"b1","focusBlockId":"b1","contentControlSelected":true,
             "contentControl":{"controlId":"cc-1","kind":"date","title":"Renewal date"}}
            """;

        var state = JsonSerializer.Deserialize<TmDocumentCanvasEngineHost.CanvasEngineSelectionState>(
            json, TmDocumentCanvasEngineHost.CanvasJsonOptions);

        state.Should().NotBeNull();
        state!.IsCollapsed.Should().BeFalse();
        state.AnchorBlockId.Should().Be("b1");
        state.ContentControlSelected.Should().BeTrue();
        state.ContentControl!.ControlId.Should().Be("cc-1");
        state.ContentControl.Kind.Should().Be("date");
    }

    [Fact]
    public void CanvasProgressiveLayoutPayloads_CarryLayoutComplete()
    {
        // Perf plan N11.5 — annotations and page metrics tell the shell whether the progressive
        // first layout finished; consumers asserting final counts wait for layoutComplete=true.
        var annotations = JsonSerializer.Deserialize<TmDocumentCanvasEngineHost.CanvasEngineAnnotations>(
            """{"comments":[],"revisions":[],"wordCount":12,"pageCount":3,"layoutComplete":false}""",
            TmDocumentCanvasEngineHost.CanvasJsonOptions);
        annotations!.LayoutComplete.Should().BeFalse();
        annotations.PageCount.Should().Be(3);

        var legacyAnnotations = JsonSerializer.Deserialize<TmDocumentCanvasEngineHost.CanvasEngineAnnotations>(
            """{"comments":[],"revisions":[],"wordCount":1,"pageCount":1}""",
            TmDocumentCanvasEngineHost.CanvasJsonOptions);
        legacyAnnotations!.LayoutComplete.Should().BeTrue("engines without progressive layout must default to complete");

        var metrics = JsonSerializer.Deserialize<WysiwygPageMetrics>(
            """{"totalPages":4,"renderedPages":2,"virtualizedPages":2,"activePageIndex":0,"pages":[],"layoutComplete":false}""",
            TmDocumentCanvasEngineHost.CanvasJsonOptions);
        metrics!.LayoutComplete.Should().BeFalse();
        metrics.TotalPages.Should().Be(4);
    }

    // ---------------------------------------------------------------------
    // N10.6 — Clone(document) timing on a 1000-paragraph document
    // ---------------------------------------------------------------------

    [Fact]
    public void CloneTiming_LargeDocument_ReportsBeforeAfter()
    {
        var document = CreateLargePerfDocument(paragraphs: 1000);

        var reflectionOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        static double MeasureClone(DocumentEditorDocument doc, JsonSerializerOptions options, int iterations)
        {
            // Provider Clone == serialize + deserialize round-trip (InMemoryDocumentEditorProvider.Clone).
            _ = JsonSerializer.Deserialize<DocumentEditorDocument>(
                JsonSerializer.Serialize(doc, options), options); // warm-up: JIT + metadata caches
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                _ = JsonSerializer.Deserialize<DocumentEditorDocument>(
                    JsonSerializer.Serialize(doc, options), options);
            }

            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / iterations;
        }

        const int iterations = 10;
        // Alternate rounds and keep the best of each to cancel JIT tiering and GC noise.
        var reflectionMs = double.MaxValue;
        var sourceGenMs = double.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            reflectionMs = Math.Min(reflectionMs, MeasureClone(document, reflectionOptions, iterations));
            sourceGenMs = Math.Min(sourceGenMs, MeasureClone(document, DocumentEditorJson.Options, iterations));
        }

        _output.WriteLine($"CLONE (N10.6, 1000 paragraphs, best-of-3 x {iterations}): reflection={reflectionMs:F1} ms, source-gen chain={sourceGenMs:F1} ms");

        // Functional identity is the hard gate; timing is recorded for the plan (JIT-heavy test
        // hosts are too noisy for a strict faster-than assertion).
        JsonSerializer.Serialize(document, DocumentEditorJson.Options)
            .Should().Be(JsonSerializer.Serialize(document, reflectionOptions));
    }

    // ---------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------

    private static DocumentEditorDocument CreateComprehensiveDocument()
    {
        var document = DocumentEditorDocument.Empty("json-source-gen");
        document.Metadata.CreatedAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        document.Metadata.ModifiedAt = null;
        document.Metadata.Title = "Source-gen fixture";

        List<InlineContent> allRuns =
        [
            new TextRun { Id = "r-text", Text = "plain", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
            new TokenRun { Id = "r-token" },
            new DocumentFieldRun { Id = "r-field" },
            new DocumentNoteReferenceRun { Id = "r-note" },
            new DocumentDrawingRun { Id = "r-drawing", ObjectId = "obj-1", Url = "https://example.test/img.png" },
            new DocumentMathRun { Id = "r-math" },
            new DocumentContentControlRun { Id = "r-cc" },
            new DocumentSigningFieldRun { Id = "r-sign", Uuid = "sig-1", Label = "Sign here" },
        ];

        document.Blocks.AddRange(
        [
            new DocumentBlock { Id = "b-p", Order = 0, Content = new ParagraphBlockContent { Inlines = allRuns } },
            new DocumentBlock { Id = "b-h", Order = 1, Content = new HeadingBlockContent { Level = 2, Inlines = [new TextRun { Text = "Heading" }] } },
            new DocumentBlock { Id = "b-l", Order = 2, Content = new ListBlockContent { Ordered = true, Inlines = [new TextRun { Text = "Item" }] } },
            new DocumentBlock { Id = "b-q", Order = 3, Content = new QuoteBlockContent { Inlines = [new TextRun { Text = "Quote" }] } },
            new DocumentBlock { Id = "b-t", Order = 4, Content = new TableBlockContent() },
            new DocumentBlock { Id = "b-i", Order = 5, Content = new ImageBlockContent() },
            new DocumentBlock { Id = "b-pb", Order = 6, Content = new PageBreakBlockContent() },
            new DocumentBlock { Id = "b-cc", Order = 7, Content = new ContentControlBlockContent() },
        ]);

        document.Comments.Add(new DocumentComment
        {
            Id = "c-1",
            Entries = [new DocumentCommentEntry { Id = "ce-1", Text = "A comment" }],
        });
        document.Revisions.Add(new DocumentRevision { Id = "rev-1" });

        // Style format bags are Dictionary<string, object?> whose runtime values are JsonElement
        // after a deserialize (or primitives when set in code) — the case that breaks source-gen
        // fast-path handlers, so it must stay covered by the byte-identity tests.
        var formatBag = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            """{"fontSize":12.5,"bold":true,"fontFamily":"Georgia"}""")!;
        document.Styles.Add(new DocumentStyleDefinition
        {
            Id = "style-1",
            Name = "Fixture style",
            ParagraphFormat = formatBag,
            CharacterFormat = new Dictionary<string, object?> { ["color"] = "#333333", ["size"] = 11 },
        });
        return document;
    }

    private static DocumentEditorDocument CreateLargePerfDocument(int paragraphs)
    {
        var document = DocumentEditorDocument.Empty("large-perf-clone");
        document.Metadata.CreatedAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        document.Metadata.ModifiedAt = null;
        for (var i = 0; i < paragraphs; i++)
        {
            document.Blocks.Add(new DocumentBlock
            {
                Id = $"p-{i}",
                Order = i,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Id = $"run-{i}", Text = $"Paragraph {i}: the quick brown fox jumps over the lazy dog while measuring serializer throughput." },
                    ],
                },
            });
        }

        return document;
    }
}
