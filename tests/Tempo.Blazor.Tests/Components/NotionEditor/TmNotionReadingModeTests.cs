using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.NotionEditor;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionReadingModeTests : LocalizationTestBase
{
    public TmNotionReadingModeTests()
    {
        var notifications = new InMemoryNotificationStore();
        Services.AddSingleton<ITmNotificationService>(notifications);
        Services.AddSingleton<CommentNotificationOrchestrator>();
        Services.AddSingleton<NavigationManager>(new ReadingModeNavigationManager());
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Notion_Reading_Enter"] = "Read",
            ["Notion_Reading_Exit"] = "Exit reading",
            ["Notion_Presentation_Enter"] = "Present",
            ["TmNotionEditor_ToggleSidebar"] = "Toggle sidebar",
            ["TmNotionEditor_NavigateBack"] = "Back",
            ["TmNotionEditor_Untitled"] = "Untitled",
            ["TmNotionEditor_Loading"] = "Loading",
            ["TmNotionEditor_LoadError"] = "Load error",
            ["TmNotionEditor_SidebarLabel"] = "Pages",
            ["TmNotionBlock_ParagraphPlaceholder"] = "Write",
            ["TmNotionPage_BlocksLoadError"] = "Blocks error",
            ["Tm_Retry"] = "Retry",
            ["TmNotionPage_WriteHint"] = "Write",
            ["Notion_Shortcuts_Open"] = "Open shortcuts"
        });
    }

    [Fact]
    public async Task ReadingMode_HidesSidebarHandlesAndUsesReadOnlyCenteredSurface()
    {
        var cut = RenderEditor();

        cut.WaitForAssertion(() => cut.Find(".tm-notion-page").Should().NotBeNull());
        cut.Find(".tm-notion-sidebar").Should().NotBeNull();

        await cut.Find(".tm-notion-reading-toggle").ClickAsync(new MouseEventArgs());

        cut.Find(".tm-notion-editor").ClassList.Should().Contain("tm-notion-editor--reading");
        cut.Find(".tm-notion-editor").GetAttribute("data-view-mode").Should().Be("Reading");
        cut.FindAll(".tm-notion-sidebar").Should().BeEmpty();
        cut.Find(".tm-notion-page").ClassList.Should().Contain("tm-notion-page--readonly");
        cut.FindAll(".tm-notion-handle").Should().BeEmpty();
        cut.FindAll(".tm-notion-inline-toolbar").Should().BeEmpty();
    }

    [Fact]
    public async Task PresentationMode_UsesFullscreenClassAndEscapeExits()
    {
        var cut = RenderEditor();

        cut.WaitForAssertion(() => cut.Find(".tm-notion-page").Should().NotBeNull());

        await cut.Find(".tm-notion-presentation-toggle").ClickAsync(new MouseEventArgs());

        cut.Find(".tm-notion-editor").ClassList.Should().Contain("tm-notion-editor--presentation");
        cut.Find(".tm-notion-editor").GetAttribute("data-view-mode").Should().Be("Presentation");
        cut.FindAll(".tm-notion-sidebar").Should().BeEmpty();
        cut.Find(".tm-notion-page").ClassList.Should().Contain("tm-notion-page--readonly");

        await cut.Find(".tm-notion-editor").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.Find(".tm-notion-editor").ClassList.Should().NotContain("tm-notion-editor--presentation");
        cut.Find(".tm-notion-editor").GetAttribute("data-view-mode").Should().Be("Normal");
        cut.Find(".tm-notion-page").ClassList.Should().NotContain("tm-notion-page--readonly");
    }

    private IRenderedComponent<TmNotionEditor> RenderEditor()
    {
        var provider = new ReadingModeProvider();
        return RenderComponent<TmNotionEditor>(parameters => parameters
            .Add(component => component.DataProvider, provider)
            .Add(component => component.BlockProvider, provider)
            .Add(component => component.InitialPageId, ReadingModeProvider.PageId.ToString("D"))
            .Add(component => component.ShowSidebar, true));
    }

    private sealed class ReadingModeProvider : INotionDataProvider, INotionBlockProvider
    {
        public static readonly Guid PageId = Guid.Parse("cf190000-0000-0000-0000-000000000001");

        private readonly NotionPage _page = new()
        {
            Id = PageId,
            Title = "Reading mode page",
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };

        private readonly List<IPageBlock> _blocks =
        [
            new PageBlock
            {
                Id = Guid.Parse("cf190000-0000-0000-0000-000000000010"),
                PageId = PageId,
                Type = BlockType.Paragraph,
                Order = 0,
                Content = new TextBlockContent { Html = "A quiet paragraph for reading mode." }
            }
        ];

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult<INotionPage>(_page);

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult<IEnumerable<INotionPage>>([_page]);

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => Task.FromResult<IEnumerable<INotionPage>>([]);

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult<IEnumerable<INotionPage>>([_page]);

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => Task.FromResult<IEnumerable<INotionPage>>([]);

        public Task<INotionPage> CreatePageAsync(string? parentId, string title)
            => throw new NotSupportedException();

        public Task UpdatePageAsync(INotionPage page)
            => Task.CompletedTask;

        public Task DeletePageAsync(string pageId)
            => Task.CompletedTask;

        public Task RestorePageAsync(string pageId)
            => Task.CompletedTask;

        public Task PermanentlyDeletePageAsync(string pageId)
            => Task.CompletedTask;

        public Task ToggleFavoriteAsync(string pageId, bool isFavorite)
            => Task.CompletedTask;

        public Task MovePageAsync(string pageId, string? newParentId)
            => Task.CompletedTask;

        public Task<INotionPage> DuplicatePageAsync(string pageId)
            => Task.FromResult<INotionPage>(_page);

        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<INotionPage>>([]);

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
            => Task.FromResult<IEnumerable<IPageBlock>>(_blocks);

        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
            => Task.FromResult(block);

        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
            => Task.FromResult(blocks);

        public Task UpdateBlockAsync(IPageBlock block)
            => Task.CompletedTask;

        public Task DeleteBlockAsync(string blockId)
            => Task.CompletedTask;

        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds)
            => Task.CompletedTask;

        public Task MoveBlockAsync(MoveNotionBlockRequest request)
            => Task.CompletedTask;

        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId)
            => Task.CompletedTask;

        public Task<IPageBlock> DuplicateBlockAsync(string blockId)
            => Task.FromResult(_blocks.Single(block => block.Id.ToString("D") == blockId));

        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
            => Task.FromResult(_blocks.Single(block => block.Id.ToString("D") == blockId));

        public Task<string> GetBlockLinkAsync(string blockId)
            => Task.FromResult($"https://localhost/notion/{PageId:D}#{blockId}");
    }

    private sealed class ReadingModeNavigationManager : NavigationManager
    {
        public ReadingModeNavigationManager()
            => Initialize("https://localhost/", "https://localhost/notion-editor");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }
    }
}
