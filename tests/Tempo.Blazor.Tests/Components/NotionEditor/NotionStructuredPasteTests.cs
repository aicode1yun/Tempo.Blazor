using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Blocks.Text;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Pasting HTML that carries more than one block — a heading and a paragraph, a list, a table —
/// must produce that many blocks instead of flattening everything into the block under the caret.
/// A snippet with a single block level keeps the old inline behaviour, so pasting a bold word in
/// the middle of a sentence does not split it.
/// </summary>
public sealed class NotionStructuredPasteTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task PastingAHeadingAndAParagraph_InsertsTwoBlocks()
    {
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted("<h1>Title</h1><p>Body</p>"));

        inserted.Should().HaveCount(1);
        var blocks = inserted[0];
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Heading1);
        blocks[1].Type.Should().Be(BlockType.Paragraph);
    }

    [Fact]
    public async Task PastingAList_InsertsOneBlockPerItem()
    {
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted("<ul><li>one</li><li>two</li></ul>"));

        inserted.Should().ContainSingle();
        inserted[0].Should().HaveCount(2);
        inserted[0].Should().OnlyContain(block => block.Type == BlockType.BulletList);
    }

    [Fact]
    public async Task PastingATable_InsertsATableParentWithChildRows()
    {
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted(
            "<table><tr><th>Name</th></tr><tr><td>CF26</td></tr></table>"));

        var blocks = inserted.Should().ContainSingle().Subject;
        var table = blocks.Should().Contain(block => block.Type == BlockType.Table).Subject;
        blocks.Where(block => block.Type == BlockType.TableRow)
            .Should().HaveCount(2)
            .And.OnlyContain(row => row.ParentBlockId == table.Id);
    }

    [Fact]
    public async Task PastingASingleParagraph_DoesNotInsertBlocks()
    {
        // One block level: the caller keeps the old inline paste, so typing is never interrupted.
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted("<p>just text</p>"));

        inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task PastingInlineMarkupOnly_DoesNotInsertBlocks()
    {
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted("<strong>bold</strong> word"));

        inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task PastedScriptPayload_NeverReachesTheInsertedBlocks()
    {
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted(
            """<h1>Title</h1><p>x<img src=q onerror="alert(1)"></p>"""));

        var html = string.Concat(inserted.SelectMany(blocks => blocks)
            .Select(block => (block.Content as ITextBlockContent)?.Html ?? string.Empty));
        html.Should().NotContain("onerror");
        html.Should().NotContain("<img");
    }

    [Fact]
    public async Task PastingEmptyHtml_DoesNothing()
    {
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted("   "));

        inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task PastingALoneCodeBlock_StillBecomesABlock()
    {
        // <pre> has no inline representation; flattening it would lose the code formatting.
        var (cut, inserted) = RenderBlock();

        await cut.InvokeAsync(() => cut.Instance.OnHtmlPasted("<pre><code>var x = 1;</code></pre>"));

        inserted.Should().ContainSingle().Which.Should().ContainSingle()
            .Which.Type.Should().Be(BlockType.Code);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private (IRenderedComponent<TmNotionBlock> Cut, List<IReadOnlyList<IPageBlock>> Inserted) RenderBlock()
    {
        var inserted = new List<IReadOnlyList<IPageBlock>>();
        var context = new NotionEditorContext { BlockProvider = Substitute.For<INotionBlockProvider>() };

        var cut = RenderComponent<TmNotionBlock>(parameters => parameters
            .AddCascadingValue(context)
            .Add(p => p.Block, new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = PageId,
                Type = BlockType.Paragraph,
                Content = new TextBlockContent { Html = "here" }
            })
            .Add(p => p.OnInsertTemplateBlocks, blocks => inserted.Add(blocks)));

        return (cut, inserted);
    }
}
