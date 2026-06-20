using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Blocks.Special;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionExcerptIncludeBlockTests : LocalizationTestBase
{
    private static readonly Guid TargetPageId = Guid.Parse("cf140000-0000-0000-0000-000000000001");
    private static readonly Guid SourcePageId = Guid.Parse("cf140000-0000-0000-0000-000000000002");
    private static readonly Guid NoExcerptPageId = Guid.Parse("cf140000-0000-0000-0000-000000000003");
    private static readonly Guid DeletedPageId = Guid.Parse("cf140000-0000-0000-0000-000000000004");

    public TmNotionExcerptIncludeBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["TmNotionEditor_Untitled"] = "Untitled",
            ["Notion_ExcerptInclude_Title"] = "Excerpt include",
            ["Notion_ExcerptInclude_Select"] = "Select source page",
            ["Notion_ExcerptInclude_NoExcerpt"] = "The selected page has no excerpt.",
            ["Notion_ExcerptInclude_NotFound"] = "The excerpt source page could not be found.",
            ["Notion_ExcerptInclude_EditSource"] = "Open source"
        });
    }

    [Fact]
    public void ExcerptIncludeBlock_LoadsSourcePageExcerpt()
    {
        var provider = new ExcerptIncludeProvider();
        var cut = RenderBlock(provider, SourcePageId, true);

        cut.WaitForAssertion(() =>
        {
            provider.BlockLoadCount.Should().Be(1);
            cut.Find(".tm-excerpt-include__content").InnerHtml.Should().Contain("Source <strong>excerpt</strong>");
            cut.Find(".tm-excerpt-include__source").TextContent.Should().Contain("Source page");
            cut.FindAll("[contenteditable]").Should().BeEmpty();
        });
    }

    [Fact]
    public void ExcerptIncludeBlock_ShowsNoExcerptState()
    {
        var provider = new ExcerptIncludeProvider();
        var cut = RenderBlock(provider, NoExcerptPageId, true);

        cut.WaitForAssertion(() =>
            cut.Find(".tm-excerpt-include__state").TextContent.Should().Be("The selected page has no excerpt."));
    }

    [Fact]
    public void ExcerptIncludeBlock_ShowsNotFoundStateForDeletedSource()
    {
        var provider = new ExcerptIncludeProvider();
        var cut = RenderBlock(provider, DeletedPageId, true);

        cut.WaitForAssertion(() =>
            cut.Find(".tm-excerpt-include__state").TextContent.Should().Be("The excerpt source page could not be found."));
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderBlock(
        ExcerptIncludeProvider provider,
        Guid sourcePageId,
        bool readOnly)
    {
        var block = MakeBlock(sourcePageId);
        var context = new NotionEditorContext
        {
            DataProvider = provider,
            BlockProvider = provider,
            NavigateTo = _ => Task.CompletedTask
        };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionExcerptIncludeBlock>(child => child
                .Add(component => component.Block, block)
                .Add(component => component.Content, (IExcerptIncludeBlockContent)block.Content)
                .Add(component => component.ReadOnly, readOnly)));
    }

    private static PageBlock MakeBlock(Guid sourcePageId) => new()
    {
        Id = Guid.Parse("cf140000-0000-0000-0000-000000000020"),
        PageId = TargetPageId,
        Type = BlockType.ExcerptInclude,
        Order = 0,
        Content = new ExcerptIncludeBlockContent { SourcePageId = sourcePageId }
    };

    private sealed class ExcerptIncludeProvider : INotionDataProvider, INotionBlockProvider
    {
        private readonly Dictionary<Guid, INotionPage> _pages = new()
        {
            [TargetPageId] = Page(TargetPageId, "Target page"),
            [SourcePageId] = Page(SourcePageId, "Source page"),
            [NoExcerptPageId] = Page(NoExcerptPageId, "No excerpt page"),
            [DeletedPageId] = Page(DeletedPageId, "Deleted source", isDeleted: true)
        };

        private readonly Dictionary<Guid, IReadOnlyList<IPageBlock>> _blocksByPage = new()
        {
            [SourcePageId] =
            [
                new PageBlock
                {
                    Id = Guid.Parse("cf140000-0000-0000-0000-000000000030"),
                    PageId = SourcePageId,
                    Type = BlockType.Paragraph,
                    Order = 0,
                    Content = new TextBlockContent { Html = "Body paragraph" }
                },
                new PageBlock
                {
                    Id = Guid.Parse("cf140000-0000-0000-0000-000000000031"),
                    PageId = SourcePageId,
                    Type = BlockType.Excerpt,
                    Order = 1,
                    Content = new ExcerptBlockContent { Html = "Source <strong>excerpt</strong>" }
                }
            ],
            [NoExcerptPageId] =
            [
                new PageBlock
                {
                    Id = Guid.Parse("cf140000-0000-0000-0000-000000000032"),
                    PageId = NoExcerptPageId,
                    Type = BlockType.Paragraph,
                    Order = 0,
                    Content = new TextBlockContent { Html = "No macro here" }
                }
            ]
        };

        public int BlockLoadCount { get; private set; }

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult(_pages[Guid.Parse(pageId)]);

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult(_pages.Values.Where(page => !page.IsDeleted));

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => Task.FromResult(Enumerable.Empty<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult(Enumerable.Empty<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => Task.FromResult(Enumerable.Empty<INotionPage>());

        public Task<INotionPage> CreatePageAsync(string? parentId, string title)
            => throw new NotSupportedException();

        public Task UpdatePageAsync(INotionPage page)
            => throw new NotSupportedException();

        public Task DeletePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task RestorePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task PermanentlyDeletePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task ToggleFavoriteAsync(string pageId, bool isFavorite)
            => throw new NotSupportedException();

        public Task MovePageAsync(string pageId, string? newParentId)
            => throw new NotSupportedException();

        public Task<INotionPage> DuplicatePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<INotionPage>>([]);

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
        {
            BlockLoadCount++;
            _blocksByPage.TryGetValue(Guid.Parse(pageId), out var blocks);
            return Task.FromResult<IEnumerable<IPageBlock>>(blocks ?? []);
        }

        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
            => throw new NotSupportedException();

        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
            => throw new NotSupportedException();

        public Task UpdateBlockAsync(IPageBlock block)
            => Task.CompletedTask;

        public Task DeleteBlockAsync(string blockId)
            => throw new NotSupportedException();

        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds)
            => throw new NotSupportedException();

        public Task MoveBlockAsync(MoveNotionBlockRequest request)
            => throw new NotSupportedException();

        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId)
            => throw new NotSupportedException();

        public Task<IPageBlock> DuplicateBlockAsync(string blockId)
            => throw new NotSupportedException();

        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
            => throw new NotSupportedException();

        public Task<string> GetBlockLinkAsync(string blockId)
            => throw new NotSupportedException();

        private static NotionPage Page(Guid id, string title, bool isDeleted = false) => new()
        {
            Id = id,
            Title = title,
            IsDeleted = isDeleted,
            CreatedAt = new DateTime(2026, 1, 14, 10, 0, 0, DateTimeKind.Utc),
            LastEditedAt = new DateTime(2026, 1, 14, 11, 0, 0, DateTimeKind.Utc)
        };
    }
}
