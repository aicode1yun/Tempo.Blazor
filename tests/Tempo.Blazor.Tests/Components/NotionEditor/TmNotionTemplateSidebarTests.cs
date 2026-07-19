using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.Sidebar;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionTemplateSidebarTests : LocalizationTestBase
{
    [Fact]
    public async Task NewPageButtonAppliesSelectedTemplateThroughProviders()
    {
        var dataProvider = new FakeDataProvider();
        var blockProvider = new FakeBlockProvider();
        var template = new NotionTemplateDto
        {
            Id = "project-plan",
            Name = "Project plan",
            Description = "Plan milestones.",
            IconEmoji = "P",
            Category = "planning",
            Blocks =
            [
                new PageBlock
                {
                    Type = BlockType.Heading1,
                    Order = 0,
                    Content = new HeadingBlockContent { Level = 1, Html = "Project plan" }
                },
                new PageBlock
                {
                    Type = BlockType.TodoItem,
                    Order = 1,
                    Content = new TodoBlockContent { Html = "Launch checklist" }
                }
            ]
        };
        var templateProvider = new FakeTemplateProvider([template]);
        string? navigatedPageId = null;
        var context = new NotionEditorContext
        {
            DataProvider = dataProvider,
            BlockProvider = blockProvider,
            TemplateProvider = templateProvider,
            NavigateTo = pageId =>
            {
                navigatedPageId = pageId;
                return Task.CompletedTask;
            }
        };
        var cut = RenderSidebar(context);

        cut.WaitForAssertion(() => cut.Find(".tm-ns-btn-new").Should().NotBeNull());
        await cut.Find(".tm-ns-btn-new").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Find("[data-template-id='project-plan']").Should().NotBeNull());
        await cut.Find("[data-template-id='project-plan'] .tm-ntg__use").ClickAsync(new MouseEventArgs());

        dataProvider.CreatedPages.Should().ContainSingle()
            .Which.Title.Should().Be("Project plan");
        blockProvider.CreatedBatches.Should().ContainSingle();
        blockProvider.CreatedBatches[0].Blocks.Should().HaveCount(2);
        blockProvider.CreatedBatches[0].Blocks.Select(block => block.PageId)
            .Should().OnlyContain(pageId => pageId == dataProvider.CreatedPages[0].Id);
        navigatedPageId.Should().Be(dataProvider.CreatedPages[0].Id.ToString());
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderSidebar(NotionEditorContext context)
        => Render<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionSidebar>());

    private sealed class FakeTemplateProvider(IReadOnlyList<NotionTemplateDto> templates) : INotionTemplateProvider
    {
        public Task<IReadOnlyList<NotionTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(templates);

        public Task<NotionTemplateDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(templates.FirstOrDefault(template =>
                string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeDataProvider : INotionDataProvider
    {
        private readonly List<INotionPage> _rootPages = [];

        public List<NotionPage> CreatedPages { get; } = [];

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult(_rootPages.Single(page => page.Id.ToString() == pageId));

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult<IEnumerable<INotionPage>>(_rootPages.Where(page => page.ParentId?.ToString() == parentId));

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => Task.FromResult<IEnumerable<INotionPage>>([]);

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult<IEnumerable<INotionPage>>([]);

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => Task.FromResult<IEnumerable<INotionPage>>([]);

        public Task<INotionPage> CreatePageAsync(string? parentId, string title)
        {
            var page = new NotionPage
            {
                Id = Guid.NewGuid(),
                ParentId = parentId is null ? null : Guid.Parse(parentId),
                Title = title,
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            };
            CreatedPages.Add(page);
            _rootPages.Add(page);
            return Task.FromResult<INotionPage>(page);
        }

        public Task UpdatePageAsync(INotionPage page) => Task.CompletedTask;

        public Task DeletePageAsync(string pageId) => Task.CompletedTask;

        public Task RestorePageAsync(string pageId) => Task.CompletedTask;

        public Task PermanentlyDeletePageAsync(string pageId) => Task.CompletedTask;

        public Task ToggleFavoriteAsync(string pageId, bool isFavorite) => Task.CompletedTask;

        public Task MovePageAsync(string pageId, string? newParentId) => Task.CompletedTask;

        public Task<INotionPage> DuplicatePageAsync(string pageId) => GetPageAsync(pageId);

        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<INotionPage>>([]);

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeBlockProvider : INotionBlockProvider
    {
        public List<(string PageId, IReadOnlyList<IPageBlock> Blocks, string? AfterBlockId)> CreatedBatches { get; } = [];

        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
            => Task.FromResult(block);

        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
        {
            var created = blocks.ToList();
            CreatedBatches.Add((pageId, created, afterBlockId));
            return Task.FromResult<IEnumerable<IPageBlock>>(created);
        }

        public Task UpdateBlockAsync(IPageBlock block) => Task.CompletedTask;

        public Task DeleteBlockAsync(string blockId) => Task.CompletedTask;

        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds) => Task.CompletedTask;

        public Task MoveBlockAsync(MoveNotionBlockRequest request) => Task.CompletedTask;

        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId) => Task.CompletedTask;

        public Task<IPageBlock> DuplicateBlockAsync(string blockId)
            => Task.FromResult<IPageBlock>(new PageBlock
            {
                Id = Guid.Parse(blockId),
                Type = BlockType.Paragraph,
                Content = new TextBlockContent()
            });

        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
            => Task.FromResult<IPageBlock>(new PageBlock
            {
                Id = Guid.Parse(blockId),
                Type = newType,
                Content = new TextBlockContent()
            });

        public Task<string> GetBlockLinkAsync(string blockId)
            => Task.FromResult($"https://localhost/notion/block/{Uri.EscapeDataString(blockId)}");
    }
}
