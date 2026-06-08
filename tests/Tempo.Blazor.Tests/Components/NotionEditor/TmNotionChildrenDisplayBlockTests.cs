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

public sealed class TmNotionChildrenDisplayBlockTests : LocalizationTestBase
{
    private static readonly Guid RootPageId = Guid.Parse("cf130000-0000-0000-0000-000000000001");
    private static readonly Guid ProductPageId = Guid.Parse("cf130000-0000-0000-0000-000000000002");
    private static readonly Guid EmptyPageId = Guid.Parse("cf130000-0000-0000-0000-000000000006");
    private static readonly Guid DeletedPageId = Guid.Parse("cf130000-0000-0000-0000-000000000007");

    public TmNotionChildrenDisplayBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["Notion_Children_Title"] = "Children display",
            ["Notion_Children_Empty"] = "This page has no child pages.",
            ["Notion_Children_SourceDeleted"] = "The selected source page could not be found.",
            ["Notion_Children_Configure"] = "Configure children display",
            ["Notion_Children_Depth"] = "Depth",
            ["Notion_Children_Root"] = "Root page",
            ["Notion_Children_CurrentPage"] = "Current page",
            ["Notion_Children_DepthAll"] = "All levels",
            ["Notion_Children_DepthLevel"] = "{0} level(s)",
            ["Notion_Children_ShowIcons"] = "Show icons",
            ["Notion_Children_Summary"] = "{0} / {1}"
        });
    }

    [Fact]
    public void ChildrenDisplayBlock_RendersTreeToConfiguredDepth()
    {
        var provider = new ChildrenDisplayDataProvider();
        var cut = RenderBlock(provider, new ChildrenDisplayBlockContent
        {
            RootPageId = ProductPageId,
            Depth = 0,
            ShowIcons = true
        });

        cut.WaitForAssertion(() =>
        {
            var pages = cut.FindAll(".tm-children__page-title");
            pages.Select(page => page.TextContent.Trim()).Should().Contain(["Release Notes", "API Guide", "Deep Troubleshooting"]);
            cut.FindAll(".tm-children__page-icon").Select(icon => icon.TextContent.Trim()).Should().Contain("R");
            cut.Find("[aria-level='2']").TextContent.Should().Contain("Deep Troubleshooting");
        });
    }

    [Fact]
    public void ChildrenDisplayBlock_LimitsDepthOne()
    {
        var provider = new ChildrenDisplayDataProvider();
        var cut = RenderBlock(provider, new ChildrenDisplayBlockContent
        {
            RootPageId = ProductPageId,
            Depth = 1,
            ShowIcons = false
        });

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".tm-children__page-title").Select(page => page.TextContent.Trim())
                .Should().Equal("API Guide", "Release Notes");
            cut.Find(".tm-children__tree-wrap").TextContent.Should().NotContain("Deep Troubleshooting");
            cut.FindAll(".tm-children__page-icon").Should().BeEmpty();
        });
    }

    [Fact]
    public void ChildrenDisplayBlock_ShowsEmptyStateWhenRootHasNoChildren()
    {
        var provider = new ChildrenDisplayDataProvider();
        var cut = RenderBlock(provider, new ChildrenDisplayBlockContent
        {
            RootPageId = EmptyPageId,
            Depth = 0,
            ShowIcons = true
        });

        cut.WaitForAssertion(() =>
            cut.Find(".tm-children__empty").TextContent.Should().Be("This page has no child pages."));
    }

    [Fact]
    public void ChildrenDisplayBlock_ShowsDeletedSourceStateWhenConfiguredRootIsMissing()
    {
        var provider = new ChildrenDisplayDataProvider();
        var cut = RenderBlock(provider, new ChildrenDisplayBlockContent
        {
            RootPageId = DeletedPageId,
            Depth = 0,
            ShowIcons = true
        });

        cut.WaitForAssertion(() =>
            cut.Find(".tm-children__empty").TextContent.Should().Be("The selected source page could not be found."));
    }

    [Fact]
    public void ChildrenDisplayBlock_PersistsDepthChanges()
    {
        var provider = new ChildrenDisplayDataProvider();
        ChildrenDisplayBlockContent? changed = null;
        var cut = RenderBlock(provider, new ChildrenDisplayBlockContent
        {
            RootPageId = ProductPageId,
            Depth = 0,
            ShowIcons = true
        }, content => changed = content);

        cut.Find(".tm-children__depth-select").Change("1");

        cut.WaitForAssertion(() =>
        {
            changed.Should().NotBeNull();
            changed!.RootPageId.Should().Be(ProductPageId);
            changed.Depth.Should().Be(1);
            changed.ShowIcons.Should().BeTrue();
        });
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderBlock(
        ChildrenDisplayDataProvider provider,
        ChildrenDisplayBlockContent content,
        Action<ChildrenDisplayBlockContent>? changed = null)
    {
        var context = new NotionEditorContext
        {
            DataProvider = provider,
            NavigateTo = _ => Task.CompletedTask
        };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionChildrenDisplayBlock>(child => child
                .Add(component => component.Block, MakeBlock(content))
                .Add(component => component.Content, content)
                .Add(component => component.OnContentChanged, EventCallback.Factory.Create<ChildrenDisplayBlockContent>(
                    this,
                    changed ?? (_ => { })))));
    }

    private static PageBlock MakeBlock(IBlockContent content) => new()
    {
        Id = Guid.Parse("cf130000-0000-0000-0000-000000000100"),
        PageId = RootPageId,
        Type = BlockType.ChildrenDisplay,
        Order = 0,
        Content = content,
        CreatedAt = DateTime.UtcNow,
        LastEditedAt = DateTime.UtcNow
    };

    private sealed class ChildrenDisplayDataProvider : INotionDataProvider
    {
        private readonly IReadOnlyList<NotionPage> _pages =
        [
            Page(RootPageId, null, "Root", "H"),
            Page(ProductPageId, RootPageId, "Product Space", "P"),
            Page(Guid.Parse("cf130000-0000-0000-0000-000000000003"), ProductPageId, "Release Notes", "R"),
            Page(Guid.Parse("cf130000-0000-0000-0000-000000000004"), ProductPageId, "API Guide", "A"),
            Page(Guid.Parse("cf130000-0000-0000-0000-000000000005"), Guid.Parse("cf130000-0000-0000-0000-000000000003"), "Deep Troubleshooting", "D"),
            Page(EmptyPageId, RootPageId, "Empty Area", "E"),
            Page(DeletedPageId, RootPageId, "Deleted Area", "X", true)
        ];

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult<INotionPage>(_pages.Single(page => page.Id == Guid.Parse(pageId)));

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
        {
            var parent = Guid.TryParse(parentId, out var id) ? id : (Guid?)null;
            return Task.FromResult(_pages.Where(page => page.ParentId == parent && !page.IsDeleted).Cast<INotionPage>());
        }

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

        private static NotionPage Page(Guid id, Guid? parentId, string title, string icon, bool isDeleted = false) => new()
        {
            Id = id,
            ParentId = parentId,
            Title = title,
            IconEmoji = icon,
            CreatedAt = new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc),
            LastEditedAt = new DateTime(2026, 1, 10, 11, 0, 0, DateTimeKind.Utc),
            IsDeleted = isDeleted
        };
    }
}
