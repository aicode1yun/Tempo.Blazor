using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.NotionEditor;
using Tempo.Blazor.Components.NotionEditor.Page;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionRestrictionsTests : LocalizationTestBase
{
    public TmNotionRestrictionsTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Close"] = "Close",
            ["Tm_Cancel"] = "Cancel",
            ["Tm_Save"] = "Save",
            ["Notion_Restrictions_Title"] = "Restricted",
            ["Notion_Restrictions_Mode"] = "Mode",
            ["Notion_Restrictions_ModeOpen"] = "Open",
            ["Notion_Restrictions_ModeReadOnlyForSome"] = "Read-only for some",
            ["Notion_Restrictions_ModeEditForSome"] = "Edit for some",
            ["Notion_Restrictions_AddUser"] = "Add user",
            ["Notion_Restrictions_SubjectType"] = "Subject type",
            ["Notion_Restrictions_User"] = "User",
            ["Notion_Restrictions_Group"] = "Group",
            ["Notion_Restrictions_Subject"] = "Subject",
            ["Notion_Restrictions_SubjectPlaceholder"] = "User or group",
            ["Notion_Restrictions_Permission"] = "Permission",
            ["Notion_Restrictions_View"] = "View",
            ["Notion_Restrictions_Comment"] = "Comment",
            ["Notion_Restrictions_Edit"] = "Edit",
            ["Notion_Restrictions_Empty"] = "No restrictions",
            ["Notion_Restrictions_Explicit"] = "Explicit",
            ["Notion_Restrictions_LoadError"] = "Page restrictions could not be loaded."
        });

        var notifications = new InMemoryNotificationStore();
        Services.AddSingleton<INotificationService>(notifications);
        Services.AddSingleton<INotificationBadgeState>(notifications);
        Services.AddSingleton<CommentNotificationOrchestrator>();
        Services.AddSingleton<PageNotificationOrchestrator>();
        Services.AddSingleton<NavigationManager>(new RestrictionNavigationManager());
    }

    [Fact]
    public async Task RestrictionsDialog_AddsUserAndGroupEntriesWithPermissionLevels()
    {
        var provider = new RestrictionProvider();

        var cut = RenderComponent<TmNotionRestrictionsDialog>(parameters => parameters
            .Add(component => component.Visible, true)
            .Add(component => component.PageId, RestrictionProvider.PageId)
            .Add(component => component.Provider, provider));

        cut.WaitForAssertion(() => cut.Find(".tm-nprd").Should().NotBeNull());

        await cut.Find("#tm-nprd-mode").ChangeAsync(new ChangeEventArgs { Value = PageRestrictionMode.EditForSome.ToString() });
        await cut.Find(".tm-nprd__input").InputAsync(new ChangeEventArgs { Value = "bob" });
        await cut.FindAll(".tm-nprd__select")[2].ChangeAsync(new ChangeEventArgs { Value = PageRestrictionPermission.View.ToString() });
        await cut.Find(".tm-nprd__add-btn").ClickAsync(new MouseEventArgs());

        await cut.FindAll(".tm-nprd__select")[1].ChangeAsync(new ChangeEventArgs { Value = PageRestrictionSubjectType.Group.ToString() });
        await cut.Find(".tm-nprd__input").InputAsync(new ChangeEventArgs { Value = "readers" });
        await cut.FindAll(".tm-nprd__select")[2].ChangeAsync(new ChangeEventArgs { Value = PageRestrictionPermission.Edit.ToString() });
        await cut.Find(".tm-nprd__add-btn").ClickAsync(new MouseEventArgs());

        cut.FindAll(".tm-nprd__source").Should().Contain(element => element.TextContent.Contains("Explicit"));

        await cut.Find(".tm-nprd__primary").ClickAsync(new MouseEventArgs());

        provider.Restrictions.Mode.Should().Be(PageRestrictionMode.EditForSome);
        provider.Restrictions.Entries.Should().Contain(entry =>
            entry.SubjectType == PageRestrictionSubjectType.User &&
            entry.SubjectId == "bob" &&
            entry.Permission == PageRestrictionPermission.View);
        provider.Restrictions.Entries.Should().Contain(entry =>
            entry.SubjectType == PageRestrictionSubjectType.Group &&
            entry.SubjectId == "readers" &&
            entry.Permission == PageRestrictionPermission.Edit);
    }

    [Fact]
    public void Editor_UsesReadOnlyModeWhenUserDoesNotHaveEditPermission()
    {
        var provider = new RestrictionProvider { EffectivePermission = PageRestrictionPermission.View };

        var cut = RenderComponent<TmNotionEditor>(parameters => parameters
            .Add(component => component.DataProvider, provider)
            .Add(component => component.BlockProvider, provider)
            .Add(component => component.PermissionProvider, provider)
            .Add(component => component.CurrentUserId, "bob")
            .Add(component => component.CurrentUserGroupIds, ["readers"])
            .Add(component => component.InitialPageId, RestrictionProvider.PageId.ToString("D"))
            .Add(component => component.ShowSidebar, true));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".tm-notion-editor").ClassList.Should().Contain("tm-notion-editor--locked");
            cut.Find(".tm-notion-page").ClassList.Should().Contain("tm-notion-page--readonly");
            cut.Find(".tm-notion-restricted-badge").TextContent.Should().Contain("Restricted");
        });
    }

    [Fact]
    public void RestrictionsDialog_LoadErrorUsesLocalizedMessage()
    {
        var provider = new ThrowingRestrictionProvider("raw connection string password");

        var cut = RenderComponent<TmNotionRestrictionsDialog>(parameters => parameters
            .Add(component => component.Visible, true)
            .Add(component => component.PageId, RestrictionProvider.PageId)
            .Add(component => component.Provider, provider));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Page restrictions could not be loaded.");
            cut.Markup.Should().NotContain("raw connection string password");
        });
    }

    private sealed class RestrictionProvider : INotionDataProvider, INotionBlockProvider, INotionPermissionProvider
    {
        public static readonly Guid PageId = Guid.Parse("cf200000-0000-0000-0000-000000000001");
        public PageRestrictionPermission EffectivePermission { get; set; } = PageRestrictionPermission.Edit;

        public PageRestrictionDto Restrictions { get; private set; } = new()
        {
            PageId = PageId,
            Mode = PageRestrictionMode.EditForSome
        };

        private readonly NotionPage _page = new()
        {
            Id = PageId,
            Title = "Restricted page",
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };

        private readonly List<IPageBlock> _blocks =
        [
            new PageBlock
            {
                Id = Guid.Parse("cf200000-0000-0000-0000-000000000010"),
                PageId = PageId,
                Type = BlockType.Paragraph,
                Order = 0,
                Content = new TextBlockContent { Html = "Permission protected text." }
            }
        ];

        public Task<PageRestrictionDto> GetRestrictionsAsync(Guid pageId, CancellationToken cancellationToken = default)
            => Task.FromResult(CloneRestrictions(Restrictions));

        public Task SetRestrictionsAsync(PageRestrictionDto restrictions, CancellationToken cancellationToken = default)
        {
            Restrictions = CloneRestrictions(restrictions);
            return Task.CompletedTask;
        }

        public Task<PageEffectivePermissionDto> GetEffectivePermissionAsync(Guid pageId, string userId, IReadOnlyList<string>? groupIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new PageEffectivePermissionDto
            {
                PageId = pageId,
                UserId = userId,
                Permission = EffectivePermission,
                Mode = Restrictions.Mode,
                SourcePageId = pageId
            });

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

        private static PageRestrictionDto CloneRestrictions(PageRestrictionDto restrictions) => new()
        {
            PageId = restrictions.PageId,
            Mode = restrictions.Mode,
            Entries = restrictions.Entries
                .Select(entry => new PageRestrictionEntryDto
                {
                    SubjectType = entry.SubjectType,
                    SubjectId = entry.SubjectId,
                    Permission = entry.Permission
                })
                .ToArray()
        };
    }

    private sealed class ThrowingRestrictionProvider : INotionPermissionProvider
    {
        private readonly string _message;

        public ThrowingRestrictionProvider(string message)
            => _message = message;

        public Task<PageRestrictionDto> GetRestrictionsAsync(Guid pageId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(_message);

        public Task SetRestrictionsAsync(PageRestrictionDto restrictions, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PageEffectivePermissionDto> GetEffectivePermissionAsync(
            Guid pageId,
            string userId,
            IReadOnlyList<string>? groupIds = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PageEffectivePermissionDto
            {
                PageId = pageId,
                UserId = userId,
                Permission = PageRestrictionPermission.Edit
            });
    }

    private sealed class RestrictionNavigationManager : NavigationManager
    {
        public RestrictionNavigationManager()
            => Initialize("https://localhost/", "https://localhost/notion-editor");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }
    }
}
