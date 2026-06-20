using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

public sealed class DocumentClipboardPipelineTests
{
    // ─── 5.1 DocumentClipboardInput ───────────────────────────────────────────

    [Fact]
    public void DocumentClipboardInput_HasHtmlPlainTextSourceFiles()
    {
        var input = new DocumentClipboardInput
        {
            Html = "<p>Hello</p>",
            PlainText = "Hello",
            Source = DocumentClipboardSource.Word,
            Files = ["image.png"]
        };

        Assert.Equal("<p>Hello</p>", input.Html);
        Assert.Equal("Hello", input.PlainText);
        Assert.Equal(DocumentClipboardSource.Word, input.Source);
        Assert.Single(input.Files);
        Assert.Equal("image.png", input.Files[0]);
    }

    [Fact]
    public void DocumentClipboardSource_HasExpectedValues()
    {
        var values = Enum.GetValues<DocumentClipboardSource>();
        Assert.Contains(DocumentClipboardSource.Unknown, values);
        Assert.Contains(DocumentClipboardSource.Word, values);
        Assert.Contains(DocumentClipboardSource.GoogleDocs, values);
        Assert.Contains(DocumentClipboardSource.GoogleSheets, values);
        Assert.Contains(DocumentClipboardSource.Internal, values);
        Assert.Contains(DocumentClipboardSource.Url, values);
        Assert.Contains(DocumentClipboardSource.PlainText, values);
        Assert.Contains(DocumentClipboardSource.RawHtml, values);
    }

    [Fact]
    public void DocumentClipboardWarning_HasCodeAndMessage()
    {
        var warning = new DocumentClipboardWarning
        {
            Code = "unsupported-element",
            Message = "The element <span> was stripped."
        };

        Assert.Equal("unsupported-element", warning.Code);
        Assert.Equal("The element <span> was stripped.", warning.Message);
    }

    [Fact]
    public void DocumentClipboardOutput_HasBlocksAndWarnings()
    {
        var block = new DocumentBlock { Type = DocumentBlockType.Paragraph };
        var warning = new DocumentClipboardWarning { Code = "w1", Message = "m1" };

        var output = new DocumentClipboardOutput
        {
            Blocks = [block],
            Warnings = [warning]
        };

        Assert.Single(output.Blocks);
        Assert.Single(output.Warnings);
    }

    [Fact]
    public void StageModels_ExposeRawDetectionNormalizedFragmentAndInsertionData()
    {
        var block = new DocumentBlock { Type = DocumentBlockType.Paragraph };
        var raw = new DocumentClipboardRawInput
        {
            Html = "<p>Hello</p>",
            PlainText = "Hello",
            Files = ["paste.png"],
            MimeTypes = ["text/html"],
            Metadata = new Dictionary<string, string> { ["source"] = "test" }
        };
        var detection = new DocumentClipboardSourceDetectionResult
        {
            Source = DocumentClipboardSource.RawHtml,
            Confidence = 0.5,
            Reason = "html-fallback"
        };
        var normalized = new DocumentClipboardNormalizedHtml
        {
            Html = "<p>Hello</p>",
            Source = DocumentClipboardSource.RawHtml,
            Warnings = [new DocumentClipboardWarning { Code = "w", Message = "m" }]
        };
        var fragment = new DocumentClipboardFragment
        {
            Blocks = [block],
            Source = DocumentClipboardSource.RawHtml,
            Warnings = normalized.Warnings
        };
        var insertion = new DocumentClipboardInsertionResult
        {
            Blocks = fragment.Blocks,
            Source = fragment.Source,
            Warnings = fragment.Warnings
        };

        Assert.Equal("<p>Hello</p>", raw.Html);
        Assert.Equal(DocumentClipboardSource.RawHtml, detection.Source);
        Assert.Equal("<p>Hello</p>", normalized.Html);
        Assert.Single(fragment.Blocks);
        Assert.Single(insertion.Warnings);
    }

    // ─── 5.1 DocumentClipboardPipeline ────────────────────────────────────────

    [Fact]
    public void Pipeline_RunsFirstMatchingNormalizer()
    {
        var firstNormalizer = new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Heading, priority: 0);
        var secondNormalizer = new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Paragraph, priority: -1);
        var pipeline = new DocumentClipboardPipeline([firstNormalizer, secondNormalizer]);

        var output = pipeline.Process(new DocumentClipboardInput { Html = "<h1>Title</h1>" });

        Assert.Single(output.Blocks);
        Assert.Equal(DocumentBlockType.Heading, output.Blocks[0].Type);
    }

    [Fact]
    public void Pipeline_SkipsNormalizerThatCannotHandle()
    {
        var skipNormalizer = new FakeNormalizer(canHandle: false, blockType: DocumentBlockType.Heading);
        var handleNormalizer = new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Paragraph);
        var pipeline = new DocumentClipboardPipeline([skipNormalizer, handleNormalizer]);

        var output = pipeline.Process(new DocumentClipboardInput { Html = "<p>text</p>" });

        Assert.Equal(DocumentBlockType.Paragraph, output.Blocks[0].Type);
    }

    [Fact]
    public void Pipeline_FallsBackToPlainTextWhenNoNormalizerMatches()
    {
        var pipeline = new DocumentClipboardPipeline([]);

        var output = pipeline.Process(new DocumentClipboardInput { PlainText = "Line one\nLine two" });

        Assert.Equal(2, output.Blocks.Count);
        var first = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        Assert.Equal("Line one", Assert.IsType<TextRun>(first.Inlines[0]).Text);
    }

    [Fact]
    public void Pipeline_FallbackWithEmptyInput_ReturnsEmptyBlocks()
    {
        var pipeline = new DocumentClipboardPipeline([]);

        var output = pipeline.Process(new DocumentClipboardInput());

        Assert.Empty(output.Blocks);
    }

    [Fact]
    public void Pipeline_NormalizerCanReturnWarnings()
    {
        var normalizer = new FakeNormalizerWithWarning();
        var pipeline = new DocumentClipboardPipeline([normalizer]);

        var output = pipeline.Process(new DocumentClipboardInput { Html = "<script>bad</script>" });

        Assert.NotEmpty(output.Warnings);
        Assert.Equal("stripped-element", output.Warnings[0].Code);
    }

    [Fact]
    public void Pipeline_HigherPriorityNormalizerRunsFirst()
    {
        var low = new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Paragraph, priority: 1);
        var high = new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Heading, priority: 20);
        var pipeline = new DocumentClipboardPipeline([low, high]);

        var output = pipeline.Process(new DocumentClipboardInput { Html = "<p>text</p>" });

        Assert.Equal(DocumentBlockType.Heading, output.Blocks[0].Type);
    }

    [Fact]
    public void Pipeline_WarningOnlyNormalizerContinuesToNextStage()
    {
        var warning = new WarningOnlyNormalizer();
        var handler = new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Paragraph, priority: 1);
        var pipeline = new DocumentClipboardPipeline([handler, warning]);

        var output = pipeline.Process(new DocumentClipboardInput { Html = "<p>text</p>" });

        Assert.Single(output.Blocks);
        Assert.Contains(output.Warnings, w => w.Code == "host-warning");
    }

    [Fact]
    public void Pipeline_DetectsKnownClipboardSources()
    {
        Assert.Equal(DocumentClipboardSource.Word, DocumentClipboardPipeline.Detect(new DocumentClipboardRawInput
        {
            Html = """<html xmlns:w="urn:schemas-microsoft-com:office:word"><p class="MsoNormal">A</p></html>"""
        }).Source);
        Assert.Equal(DocumentClipboardSource.GoogleDocs, DocumentClipboardPipeline.Detect(new DocumentClipboardRawInput
        {
            Html = """<b id="docs-internal-guid-1">Title</b>"""
        }).Source);
        Assert.Equal(DocumentClipboardSource.GoogleSheets, DocumentClipboardPipeline.Detect(new DocumentClipboardRawInput
        {
            PlainText = "A\tB"
        }).Source);
        Assert.Equal(DocumentClipboardSource.Internal, DocumentClipboardPipeline.Detect(new DocumentClipboardRawInput
        {
            MimeTypes = ["application/x-tempo-document-fragment"]
        }).Source);
        Assert.Equal(DocumentClipboardSource.Url, DocumentClipboardPipeline.Detect(new DocumentClipboardRawInput
        {
            PlainText = "https://example.test"
        }).Source);
        Assert.Equal(DocumentClipboardSource.PlainText, DocumentClipboardPipeline.Detect(new DocumentClipboardRawInput
        {
            PlainText = "hello"
        }).Source);
    }

    [Fact]
    public void Pipeline_ProcessForInsertion_AggregatesPolicyWarnings()
    {
        var pipeline = new DocumentClipboardPipeline([new FakeNormalizer(canHandle: true, blockType: DocumentBlockType.Table)]);

        var output = pipeline.ProcessForInsertion(
            new DocumentClipboardRawInput { Html = "<table><tr><td>A</td></tr></table>" },
            DocumentEditorRegion.TableCell);

        Assert.Single(output.Blocks);
        Assert.Contains(output.Warnings, w => w.Code == "table-unwrapped-in-table-cell");
    }

    // ─── Fake helpers ─────────────────────────────────────────────────────────

    private sealed class FakeNormalizer(bool canHandle, DocumentBlockType blockType, int priority = 0)
        : IDocumentClipboardNormalizer
    {
        public int Priority => priority;

        public bool CanHandle(DocumentClipboardInput input) => canHandle;

        public DocumentClipboardOutput Normalize(DocumentClipboardInput input) =>
            new()
            {
                Blocks =
                [
                    new DocumentBlock
                    {
                        Type = blockType,
                        Content = blockType == DocumentBlockType.Table
                            ? new TableBlockContent
                            {
                                Rows =
                                [
                                    new TableRowContent
                                    {
                                        Cells =
                                        [
                                            new TableCellContent
                                            {
                                                Blocks = [new DocumentBlock { Content = new ParagraphBlockContent() }]
                                            }
                                        ]
                                    }
                                ]
                            }
                            : new ParagraphBlockContent()
                    }
                ]
            };
    }

    private sealed class FakeNormalizerWithWarning : IDocumentClipboardNormalizer
    {
        public bool CanHandle(DocumentClipboardInput input) => true;

        public DocumentClipboardOutput Normalize(DocumentClipboardInput input) =>
            new()
            {
                Blocks = [new DocumentBlock()],
                Warnings = [new DocumentClipboardWarning { Code = "stripped-element", Message = "script removed" }]
            };
    }

    private sealed class WarningOnlyNormalizer : IDocumentClipboardNormalizer
    {
        public int Priority => 100;

        public bool CanHandle(DocumentClipboardInput input) => true;

        public DocumentClipboardOutput Normalize(DocumentClipboardInput input) =>
            new()
            {
                Warnings = [new DocumentClipboardWarning { Code = "host-warning", Message = "Host warning" }]
            };
    }
}
