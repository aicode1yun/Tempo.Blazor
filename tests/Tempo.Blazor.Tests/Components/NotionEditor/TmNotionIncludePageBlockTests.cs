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

public sealed class TmNotionIncludePageBlockTests : LocalizationTestBase
{
    private static readonly Guid RootPageId = Guid.Parse("cf120000-0000-0000-0000-000000000001");
    private static readonly Guid SourcePageId = Guid.Parse("cf120000-0000-0000-0000-000000000002");

    public TmNotionIncludePageBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["TmNotionEditor_Untitled"] = "Untitled",
            ["Notion_IncludePage_Title"] = "Include page",
            ["Notion_IncludePage_Select"] = "Select source page",
            ["Notion_IncludePage_NotFound"] = "The included page could not be found.",
            ["Notion_IncludePage_Cyclic"] = "This include would create a page cycle, so it was stopped.",
            ["Notion_IncludePage_TooDeep"] = "This include is nested too deeply, so it was stopped.",
            ["Notion_IncludePage_EditSource"] = "Edit source",
            ["Notion_IncludePage_Empty"] = "The included page has no blocks."
        });
    }

    [Fact]
    public void IncludePageBlock_LazyLoadsSourceBlocksAndRendersReadOnlyContent()
    {
        var provider = new IncludePageProvider();
        var block = IncludeBlock(RootPageId, SourcePageId, 0);

        var cut = RenderBlock(provider, block);

        cut.WaitForAssertion(() =>
        {
            provider.BlockLoadCount.Should().Be(1);
            cut.Markup.Should().Contain("Source heading");
            cut.Find(".tm-include-page__paragraph").TextContent.Should().Be("Included paragraph");
            cut.FindAll("[contenteditable]").Should().BeEmpty();
            cut.Find(".tm-include-page__source-link").TextContent.Should().Contain("Edit source");
        });
    }

    [Fact]
    public void IncludePageBlock_StopsNestedPageCycle()
    {
        var provider = new IncludePageProvider();
        provider.BlocksByPage[SourcePageId] =
        [
            IncludeBlock(SourcePageId, RootPageId, 0)
        ];

        var block = IncludeBlock(RootPageId, SourcePageId, 0);
        var cut = RenderBlock(provider, block);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("This include would create a page cycle");
            provider.BlocksByPage.TryGetValue(RootPageId, out _).Should().BeFalse();
        });
    }

    [Fact]
    public void IncludePageBlock_NumberedListsRestartAfterNonNumberedBlock()
    {
        var provider = new IncludePageProvider();
        provider.BlocksByPage[SourcePageId] =
        [
            NumberedBlock(0, "First list item"),
            new PageBlock
            {
                Id = Guid.Parse("cf120000-0000-0000-0000-000000000031"),
                PageId = SourcePageId,
                Type = BlockType.Paragraph,
                Order = 1,
                Content = new TextBlockContent { Html = "Break paragraph" }
            },
            NumberedBlock(2, "Restarted list item")
        ];

        var cut = RenderBlock(provider, IncludeBlock(RootPageId, SourcePageId, 0));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".tm-include-page__number")
                .Select(number => number.TextContent.Trim())
                .Should().Equal("1", "1");
        });
    }

    [Fact]
    public void IncludePageBlock_StopsDeepAcyclicIncludeChain()
    {
        var provider = new IncludePageProvider();
        var currentPage = SourcePageId;
        for (var i = 0; i < 12; i++)
        {
            var nextPage = Guid.Parse($"cf120001-0000-0000-0000-{(i + 1):000000000000}");
            provider.AddPage(nextPage, $"Nested {i + 1}");
            provider.BlocksByPage[currentPage] = [IncludeBlock(currentPage, nextPage, i)];
            currentPage = nextPage;
        }

        var cut = RenderBlock(provider, IncludeBlock(RootPageId, SourcePageId, 0));

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("This include is nested too deeply"));
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderBlock(
        IncludePageProvider provider,
        IPageBlock block)
    {
        var context = new NotionEditorContext
        {
            DataProvider = provider,
            BlockProvider = provider,
            NavigateTo = _ => Task.CompletedTask
        };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionIncludePageBlock>(child => child
                .Add(component => component.Block, block)
                .Add(component => component.Content, (IIncludePageBlockContent)block.Content)
                .Add(component => component.ReadOnly, true)));
    }

    private static PageBlock IncludeBlock(Guid pageId, Guid sourcePageId, int order) => new()
    {
        Id = Guid.NewGuid(),
        PageId = pageId,
        Type = BlockType.IncludePage,
        Order = order,
        Content = new IncludePageBlockContent { SourcePageId = sourcePageId }
    };

    private static PageBlock NumberedBlock(int order, string text) => new()
    {
        Id = Guid.Parse($"cf120000-0000-0000-0000-{(30 + order):000000000000}"),
        PageId = SourcePageId,
        Type = BlockType.NumberedList,
        Order = order,
        Content = new ListBlockContent { Html = text }
    };

    private sealed class IncludePageProvider : INotionDataProvider, INotionBlockProvider
    {
        private readonly Dictionary<Guid, INotionPage> _pages = new()
        {
            [RootPageId] = new NotionPage
            {
                Id = RootPageId,
                Title = "Root page"
            },
            [SourcePageId] = new NotionPage
            {
                Id = SourcePageId,
                Title = "Source page"
            }
        };

        public Dictionary<Guid, IReadOnlyList<IPageBlock>> BlocksByPage { get; } = new()
        {
            [SourcePageId] =
            [
                new PageBlock
                {
                    Id = Guid.Parse("cf120000-0000-0000-0000-000000000010"),
                    PageId = SourcePageId,
                    Type = BlockType.Heading2,
                    Order = 0,
                    Content = new HeadingBlockContent { Level = 2, Html = "Source heading" }
                },
                new PageBlock
                {
                    Id = Guid.Parse("cf120000-0000-0000-0000-000000000011"),
                    PageId = SourcePageId,
                    Type = BlockType.Paragraph,
                    Order = 1,
                    Content = new TextBlockContent { Html = "Included <strong>paragraph</strong>" }
                }
            ]
        };

        public int BlockLoadCount { get; private set; }

        public void AddPage(Guid pageId, string title)
            => _pages[pageId] = new NotionPage
            {
                Id = pageId,
                Title = title
            };

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult(_pages[Guid.Parse(pageId)]);

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult(_pages.Values.AsEnumerable());

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
            BlocksByPage.TryGetValue(Guid.Parse(pageId), out var blocks);
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
    }
}
