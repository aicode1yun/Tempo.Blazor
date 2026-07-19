using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.Sidebar;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionSidebarErrorTests : LocalizationTestBase
{
    public TmNotionSidebarErrorTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Retry"] = "Retry",
            ["TmNotionSidebar_Loading"] = "Loading",
            ["TmNotionSidebar_LoadError"] = "Sidebar could not be loaded.",
            ["TmNotionSidebar_Search"] = "Search",
            ["TmNotionSidebar_SearchPlaceholder"] = "Search pages",
            ["TmNotionSidebar_SearchLoading"] = "Searching",
            ["TmNotionSidebar_SearchNoResults"] = "No pages found",
            ["TmNotionSidebar_SearchError"] = "Search could not be completed.",
            ["TmNotionSidebar_CloseSearch"] = "Close search",
            ["TmNotionSidebar_NewPage"] = "New page",
            ["TmNotionSidebar_Pages"] = "Pages",
            ["TmNotionSidebar_Trash"] = "Trash",
            ["TmNotionSidebar_TrashOpen"] = "Open trash",
            ["TmNotionSidebar_CreatingPage"] = "Creating",
            ["TmNotionEditor_SidebarLabel"] = "Pages",
            ["TmNotionSidebarTrash_Title"] = "Trash",
            ["TmNotionSidebarTrash_Back"] = "Back",
            ["TmNotionSidebarTrash_SearchPlaceholder"] = "Filter deleted pages",
            ["TmNotionSidebarTrash_Loading"] = "Loading",
            ["TmNotionSidebarTrash_LoadError"] = "Trash could not be loaded.",
            ["TmNotionSidebarTrash_Empty"] = "Trash is empty",
            ["TmNotionSidebarTrash_EmptySearch"] = "No matching pages"
        });
    }

    [Fact]
    public void SidebarLoadError_UsesLocalizedMessageInsteadOfProviderException()
    {
        var context = new NotionEditorContext
        {
            DataProvider = new SidebarDataProvider(loadFailure: "connection-string-password")
        };

        var cut = RenderSidebar(context);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Sidebar could not be loaded.");
            cut.Markup.Should().NotContain("connection-string-password");
        });
    }

    [Fact]
    public async Task SidebarSearchError_UsesLocalizedMessageInsteadOfProviderException()
    {
        var context = new NotionEditorContext
        {
            DataProvider = new SidebarDataProvider(),
            SearchProvider = new ThrowingSearchProvider("raw-search-token")
        };

        var cut = RenderSidebar(context);
        cut.WaitForAssertion(() => cut.Find(".tm-ns-btn-search").Should().NotBeNull());

        await cut.Find(".tm-ns-btn-search").ClickAsync(new MouseEventArgs());
        await cut.Find(".tm-ns-search__input").InputAsync(new ChangeEventArgs { Value = "launch" });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Search could not be completed.");
            cut.Markup.Should().NotContain("raw-search-token");
        });
    }

    [Fact]
    public void SidebarTrashLoadError_UsesLocalizedMessageInsteadOfProviderException()
    {
        var context = new NotionEditorContext
        {
            DataProvider = new SidebarDataProvider(trashFailure: "deleted-page-storage-secret")
        };

        var cut = RenderTrash(context);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Trash could not be loaded.");
            cut.Markup.Should().NotContain("deleted-page-storage-secret");
        });
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderSidebar(NotionEditorContext context)
        => Render<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionSidebar>());

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderTrash(NotionEditorContext context)
        => Render<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionSidebarTrash>());

    private sealed class SidebarDataProvider : INotionDataProvider
    {
        private readonly string? _loadFailure;
        private readonly string? _trashFailure;
        private readonly NotionPage _page = new()
        {
            Id = Guid.Parse("f1000000-0000-0000-0000-000000000001"),
            Title = "Launch plan",
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };

        public SidebarDataProvider(string? loadFailure = null, string? trashFailure = null)
        {
            _loadFailure = loadFailure;
            _trashFailure = trashFailure;
        }

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult<INotionPage>(_page);

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult<IEnumerable<INotionPage>>([_page]);

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => _loadFailure is null
                ? Task.FromResult<IEnumerable<INotionPage>>([])
                : Task.FromException<IEnumerable<INotionPage>>(new InvalidOperationException(_loadFailure));

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult<IEnumerable<INotionPage>>([]);

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => _trashFailure is null
                ? Task.FromResult<IEnumerable<INotionPage>>([])
                : Task.FromException<IEnumerable<INotionPage>>(new InvalidOperationException(_trashFailure));

        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<INotionPage>>([]);

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<INotionPage> CreatePageAsync(string? parentId, string title)
            => Task.FromResult<INotionPage>(_page);

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
    }

    private sealed class ThrowingSearchProvider : INotionSearchProvider
    {
        private readonly string _message;

        public ThrowingSearchProvider(string message)
            => _message = message;

        public Task<IEnumerable<INotionPage>> SearchPagesAsync(string query, NotionSearchFilter? filter)
            => Task.FromException<IEnumerable<INotionPage>>(new InvalidOperationException(_message));

        public Task<IEnumerable<NotionSearchResult>> SearchBlocksAsync(string query, NotionSearchFilter? filter)
            => Task.FromException<IEnumerable<NotionSearchResult>>(new InvalidOperationException(_message));

        public Task<(IEnumerable<INotionPage> Pages, IEnumerable<NotionSearchResult> Blocks)> SearchAllAsync(
            string query,
            NotionSearchFilter? filter,
            int maxResults)
            => Task.FromException<(IEnumerable<INotionPage>, IEnumerable<NotionSearchResult>)>(
                new InvalidOperationException(_message));
    }
}
