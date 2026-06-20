using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.Sidebar;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionPageTreeBulkTests : LocalizationTestBase
{
    [Fact]
    public void PageTree_RendersMultiSelectToolbarAndDeletesSelectedPages()
    {
        var provider = new BulkPageProvider();
        var changedCount = 0;
        var cut = RenderPageTree(provider, () => changedCount++);

        cut.Find(SelectTestId(BulkPageProvider.SourcePageId)).Change(true);
        cut.Find(SelectTestId(BulkPageProvider.DeletePageId)).Change(true);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='notion-page-bulk-toolbar']").Should().NotBeNull();
            cut.Find("[data-testid='notion-page-bulk-count']").TextContent.Should().Contain("2 selected");
        });

        cut.Find("[data-testid='notion-page-bulk-delete']").Click();
        cut.Find("[data-testid='notion-page-bulk-delete-confirm-button']").Click();

        cut.WaitForAssertion(() =>
        {
            provider.DeletedPageIds.Should().Equal(
                BulkPageProvider.SourcePageId.ToString("D"),
                BulkPageProvider.DeletePageId.ToString("D"));
            changedCount.Should().Be(1);
            cut.FindAll("[data-testid='notion-page-bulk-toolbar']").Should().BeEmpty();
        });
    }

    [Fact]
    public void PageTree_BulkMoveToRootUsesDataProviderBulkEndpoint()
    {
        var provider = new BulkPageProvider();
        var cut = RenderPageTree(provider, () => { });

        cut.Find(SelectTestId(BulkPageProvider.SourcePageId)).Change(true);
        cut.Find("[data-testid='notion-page-bulk-move']").Click();
        cut.Find("[data-testid='notion-page-bulk-target-root']").Click();

        cut.WaitForAssertion(() =>
        {
            provider.MovedPageIds.Should().Equal(BulkPageProvider.SourcePageId.ToString("D"));
            provider.MoveTargetPageId.Should().BeNull();
        });
    }

    [Fact]
    public void PageTree_BulkCopySearchesTargetAndCopiesSelectedTree()
    {
        var provider = new BulkPageProvider();
        var cut = RenderPageTree(provider, () => { });

        cut.Find(SelectTestId(BulkPageProvider.SourcePageId)).Change(true);
        cut.Find("[data-testid='notion-page-bulk-copy']").Click();
        cut.Find("[data-testid='notion-page-bulk-target-search']").Input("Target");

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='notion-page-bulk-target-page']").TextContent.Should().Contain("Target Space"));

        cut.Find("[data-testid='notion-page-bulk-target-page']").Click();

        cut.WaitForAssertion(() =>
        {
            provider.CopiedTrees.Should().ContainSingle(copy =>
                copy.PageId == BulkPageProvider.SourcePageId.ToString("D") &&
                copy.TargetPageId == BulkPageProvider.TargetPageId.ToString("D"));
        });
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderPageTree(BulkPageProvider provider, Action changed)
    {
        var context = new NotionEditorContext
        {
            DataProvider = provider,
            SearchProvider = provider
        };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionPageTree>(child => child
                .Add(component => component.RootPages, provider.RootPages)
                .Add(component => component.OnTreeChanged, EventCallback.Factory.Create(this, changed))));
    }

    private static string SelectTestId(Guid pageId)
        => $"[data-testid='notion-page-select-{pageId:D}']";

    private sealed class BulkPageProvider : INotionDataProvider, INotionSearchProvider
    {
        public static readonly Guid SourcePageId = Guid.Parse("cf240001-0000-0000-0000-000000000001");
        public static readonly Guid DeletePageId = Guid.Parse("cf240005-0000-0000-0000-000000000001");
        public static readonly Guid TargetPageId = Guid.Parse("cf240004-0000-0000-0000-000000000001");

        private readonly List<NotionPage> _pages =
        [
            new() { Id = SourcePageId, Title = "Source Root", IconEmoji = "S" },
            new() { Id = DeletePageId, Title = "Delete Candidate", IconEmoji = "D" },
            new() { Id = TargetPageId, Title = "Target Space", IconEmoji = "T" }
        ];

        public IReadOnlyList<INotionPage> RootPages => _pages.Cast<INotionPage>().ToList();
        public IReadOnlyList<string> MovedPageIds { get; private set; } = [];
        public string? MoveTargetPageId { get; private set; }
        public IReadOnlyList<string> DeletedPageIds { get; private set; } = [];
        public List<(string PageId, string? TargetPageId)> CopiedTrees { get; } = [];

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult<INotionPage>(_pages.Single(page => page.Id == Guid.Parse(pageId)));

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult(_pages.Where(page => page.ParentId?.ToString("D") == parentId).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => Task.FromResult(Enumerable.Empty<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult(_pages.Take(count).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => Task.FromResult(_pages.Where(page => page.IsDeleted).Cast<INotionPage>());

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

        public Task MovePagesAsync(IReadOnlyList<string> pageIds, string? newParentId, CancellationToken cancellationToken = default)
        {
            MovedPageIds = pageIds.ToArray();
            MoveTargetPageId = newParentId;
            return Task.CompletedTask;
        }

        public Task DeletePagesAsync(IReadOnlyList<string> pageIds, CancellationToken cancellationToken = default)
        {
            DeletedPageIds = pageIds.ToArray();
            return Task.CompletedTask;
        }

        public Task<INotionPage> CopyPageTreeAsync(string pageId, string? newParentId, CancellationToken cancellationToken = default)
        {
            CopiedTrees.Add((pageId, newParentId));
            return Task.FromResult<INotionPage>(new NotionPage { Id = Guid.NewGuid(), Title = "Copied page" });
        }

        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<INotionPage>>([]);

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<INotionPage>> SearchPagesAsync(string query, NotionSearchFilter? filter)
        {
            var results = _pages
                .Where(page => page.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Cast<INotionPage>();
            return Task.FromResult(results);
        }

        public Task<IEnumerable<NotionSearchResult>> SearchBlocksAsync(string query, NotionSearchFilter? filter)
            => Task.FromResult(Enumerable.Empty<NotionSearchResult>());

        public Task<(IEnumerable<INotionPage> Pages, IEnumerable<NotionSearchResult> Blocks)> SearchAllAsync(
            string query,
            NotionSearchFilter? filter,
            int maxResults)
            => Task.FromResult<(IEnumerable<INotionPage>, IEnumerable<NotionSearchResult>)>(([], []));
    }
}
