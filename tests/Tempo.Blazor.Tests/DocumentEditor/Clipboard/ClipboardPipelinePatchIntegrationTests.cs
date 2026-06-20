using System.Text.Json;
using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

/// <summary>Integration tests verifying that clipboard pipeline output can be applied as WysiwygPatch InsertBlock operations.</summary>
public sealed class ClipboardPipelinePatchIntegrationTests
{
    private static DocumentClipboardPipeline CreatePipeline() => new([
        new WordClipboardNormalizer(),
        new GoogleDocsClipboardNormalizer(),
        new GoogleSheetsClipboardNormalizer(),
        new UrlClipboardNormalizer(),
        new RawHtmlClipboardNormalizer()
    ]);

    private static DocumentEditorDocument CreateEmptyDocument() => new()
    {
        DocumentId = "test-doc",
        Blocks = []
    };

    private static List<WysiwygPatch> BlocksToInsertPatches(IReadOnlyList<DocumentBlock> blocks)
    {
        string? previousBlockId = null;
        return blocks.Select(block =>
        {
            var patch = new WysiwygPatch
            {
                Type = "InsertBlock",
                BlockType = block.Type.ToString(),
                Block = block,
                Selection = new WysiwygSelectionSnapshot { AnchorBlockId = previousBlockId, IsCollapsed = true },
                ProtocolVersion = 1
            };
            previousBlockId = block.Id;
            return patch;
        }).ToList();
    }

    // ─── Plain HTML → document ────────────────────────────────────────────────

    [Fact]
    public void Pipeline_PlainHtml_PatchesApplyTwoBlocks()
    {
        var pipeline = CreatePipeline();
        var output = pipeline.Process(new DocumentClipboardInput { Html = "<p>First</p><p>Second</p>" });
        var document = CreateEmptyDocument();
        var applier = new WysiwygPatchApplier();

        foreach (var patch in BlocksToInsertPatches(output.Blocks))
            applier.ApplyPatch(document, patch);

        Assert.Equal(2, document.Blocks.Count);
        Assert.All(document.Blocks, b => Assert.Equal(DocumentBlockType.Paragraph, b.Type));
    }

    // ─── URL → link mark ─────────────────────────────────────────────────────

    [Fact]
    public void Pipeline_UrlPlainText_PatchCreatesLinkInDocument()
    {
        var pipeline = CreatePipeline();
        var output = pipeline.Process(new DocumentClipboardInput { PlainText = "https://example.com" });
        var document = CreateEmptyDocument();
        var applier = new WysiwygPatchApplier();

        foreach (var patch in BlocksToInsertPatches(output.Blocks))
            applier.ApplyPatch(document, patch);

        Assert.Single(document.Blocks);
        var para = Assert.IsType<ParagraphBlockContent>(document.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines.Single());
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Link && m.Link?.Href == "https://example.com");
    }

    // ─── Google Sheets TSV → table ───────────────────────────────────────────

    [Fact]
    public void Pipeline_TsvPlainText_PatchCreatesTableInDocument()
    {
        var pipeline = CreatePipeline();
        var output = pipeline.Process(new DocumentClipboardInput { PlainText = "A\tB\nC\tD" });
        var document = CreateEmptyDocument();
        var applier = new WysiwygPatchApplier();

        foreach (var patch in BlocksToInsertPatches(output.Blocks))
            applier.ApplyPatch(document, patch);

        Assert.Single(document.Blocks);
        Assert.Equal(DocumentBlockType.Table, document.Blocks[0].Type);
        var table = Assert.IsType<TableBlockContent>(document.Blocks[0].Content);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Rows[0].Cells.Count);
    }

    // ─── JSON round-trip (verifies JS-compatible serialization) ──────────────

    [Fact]
    public void Pipeline_Output_SerializesAndDeserializesBlocks()
    {
        var pipeline = CreatePipeline();
        var output = pipeline.Process(new DocumentClipboardInput { Html = "<p>Hello <strong>world</strong></p>" });

        var json = JsonSerializer.Serialize(output.Blocks);
        var deserialized = JsonSerializer.Deserialize<DocumentBlock[]>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized!);
        Assert.Equal(DocumentBlockType.Paragraph, deserialized![0].Type);
    }

    // ─── Word HTML → document ────────────────────────────────────────────────

    [Fact]
    public void Pipeline_WordHtml_PatchPreservesBoldInDocument()
    {
        const string html = """
            <html xmlns:w="urn:schemas-microsoft-com:office:word">
            <body><p class="MsoNormal"><b>Bold text</b></p></body></html>
            """;
        var pipeline = CreatePipeline();
        var output = pipeline.Process(new DocumentClipboardInput { Html = html });
        var document = CreateEmptyDocument();
        var applier = new WysiwygPatchApplier();

        foreach (var patch in BlocksToInsertPatches(output.Blocks))
            applier.ApplyPatch(document, patch);

        Assert.NotEmpty(document.Blocks);
        var para = Assert.IsType<ParagraphBlockContent>(document.Blocks[0].Content);
        var boldRun = para.Inlines.OfType<TextRun>()
            .FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        Assert.NotNull(boldRun);
    }
}
