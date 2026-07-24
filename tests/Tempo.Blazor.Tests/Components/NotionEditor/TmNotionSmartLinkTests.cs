using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Blocks.Text;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionSmartLinkTests : LocalizationTestBase
{
    public TmNotionSmartLinkTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmNotionBlock_Comments"] = "Comments",
            ["TmNotionBlock_ParagraphPlaceholder"] = "Write",
            ["Notion_SmartLink_PasteAs_Inline"] = "Paste as inline preview",
            ["Notion_SmartLink_PasteAs_Card"] = "Paste as card",
            ["Notion_SmartLink_PasteAs_Plain"] = "Paste as plain link"
        });
    }

    [Fact]
    public async Task SmartLinkInline_ResolvesMetadataAndInsertsChip()
    {
        string? savedHtml = null;
        JSInterop.Setup<string>("tmNotionEditor.getHtml", _ => true)
            .SetResult("<a class=\"tm-notion-smart-link\" href=\"https://docs.tempo.local/notion/special-blocks\">Tempo Notion special blocks</a>");

        var cut = RenderBlock(new SmartLinkBlockService(), new SmartLinkProvider(), updated =>
        {
            savedHtml = (updated.Content as ITextBlockContent)?.Html;
        });

        var textBlock = cut.FindComponent<TmNotionTextBlock>();
        await cut.InvokeAsync(() => textBlock.Instance.OnSmartLinkPasteRequested("https://docs.tempo.local/notion/special-blocks", "Inline"));

        var invocation = JSInterop.Invocations
            .Single(call => call.Identifier == "tmNotionEditor.insertSmartLinkChip");

        invocation.Arguments[1].Should().Be("https://docs.tempo.local/notion/special-blocks");
        invocation.Arguments[2].Should().Be("Tempo Notion special blocks");
        invocation.Arguments[3].Should().Be("https://docs.tempo.local/favicon.ico");
        invocation.Arguments[4].Should().Be("Tempo Docs");
        savedHtml.Should().Contain("tm-notion-smart-link");
    }

    [Fact]
    public async Task SmartLinkCard_ResolvesMetadataAndCreatesBookmarkBlock()
    {
        var blockService = new SmartLinkBlockService();
        var cut = RenderBlock(blockService, new SmartLinkProvider());

        var textBlock = cut.FindComponent<TmNotionTextBlock>();
        await cut.InvokeAsync(() => textBlock.Instance.OnSmartLinkPasteRequested("docs.tempo.local/notion/special-blocks", "Card"));

        blockService.CreatedBlock.Should().NotBeNull();
        blockService.CreatedBlock!.Type.Should().Be(BlockType.Bookmark);
        blockService.CreatedBlock.Content.Should().BeOfType<BookmarkBlockContent>()
            .Which.Should().BeEquivalentTo(new BookmarkBlockContent
            {
                Url = "https://docs.tempo.local/notion/special-blocks",
                Title = "Tempo Notion special blocks",
                Description = "Production verification notes for smart links.",
                CoverImageUrl = "https://docs.tempo.local/assets/notion-special-blocks.png",
                FaviconUrl = "https://docs.tempo.local/favicon.ico",
                Domain = "Tempo Docs"
            });
    }

    [Fact]
    public async Task SmartLinkProviderFailure_FallsBackToPlainLink()
    {
        string? savedHtml = null;
        JSInterop.Setup<string>("tmNotionEditor.getHtml", _ => true)
            .SetResult("<a href=\"https://unknown.tempo.local/\">https://unknown.tempo.local/</a>");

        var cut = RenderBlock(new SmartLinkBlockService(), new FailingSmartLinkProvider(), updated =>
        {
            savedHtml = (updated.Content as ITextBlockContent)?.Html;
        });

        var textBlock = cut.FindComponent<TmNotionTextBlock>();
        await cut.InvokeAsync(() => textBlock.Instance.OnSmartLinkPasteRequested("https://unknown.tempo.local", "Inline"));

        JSInterop.Invocations.Should().NotContain(call => call.Identifier == "tmNotionEditor.insertSmartLinkChip");
        savedHtml.Should().Contain("https://unknown.tempo.local/");
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderBlock(
        SmartLinkBlockService blockService,
        ISmartLinkProvider smartLinkProvider,
        Action<IPageBlock>? updated = null)
    {
        var block = new PageBlock
        {
            Id = Guid.Parse("cf800000-0000-0000-0000-000000000010"),
            PageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Type = BlockType.Paragraph,
            Order = 10,
            Content = new TextBlockContent { Html = string.Empty }
        };

        var context = new NotionEditorContext
        {
            BlockService = blockService,
            SmartLinkProvider = smartLinkProvider
        };

        return Render<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionBlock>(child => child
                .Add(component => component.Block, block)
                .Add(component => component.OnUpdated, EventCallback.Factory.Create<IPageBlock>(
                    this,
                    updated ?? (_ => { })))));
    }

    private sealed class SmartLinkProvider : ISmartLinkProvider
    {
        public Task<SmartLinkDto?> ResolveAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult<SmartLinkDto?>(new SmartLinkDto(
                "https://docs.tempo.local/notion/special-blocks",
                "Tempo Notion special blocks",
                "https://docs.tempo.local/favicon.ico",
                "Production verification notes for smart links.",
                "https://docs.tempo.local/assets/notion-special-blocks.png",
                "Tempo Docs"));
    }

    private sealed class FailingSmartLinkProvider : ISmartLinkProvider
    {
        public Task<SmartLinkDto?> ResolveAsync(string url, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Resolver unavailable.");
    }

    private sealed class SmartLinkBlockService : INotionEditorBlockService
    {
        public IPageBlock? CreatedBlock { get; private set; }

        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
        {
            CreatedBlock = block;
            return Task.FromResult(block);
        }

        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
            => Task.FromResult(blocks);

        public Task UpdateBlockAsync(IPageBlock block) => Task.CompletedTask;

        public Task DeleteBlockAsync(string blockId) => Task.CompletedTask;

        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds) => Task.CompletedTask;

        public Task MoveBlockAsync(MoveNotionBlockRequest request) => Task.CompletedTask;

        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId) => Task.CompletedTask;

        public Task<IPageBlock> DuplicateBlockAsync(string blockId) => throw new NotSupportedException();

        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType) => throw new NotSupportedException();

        public Task<string> GetBlockLinkAsync(string blockId) => Task.FromResult(string.Empty);
    }
}
