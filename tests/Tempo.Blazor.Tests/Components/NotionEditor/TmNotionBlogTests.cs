using Bunit.Rendering;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionBlogTests : LocalizationTestBase
{
    public TmNotionBlogTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Close"] = "Close",
            ["Tm_Loading"] = "Loading",
            ["Tm_Retry"] = "Retry",
            ["Notion_Blog_Title"] = "Blog",
            ["Notion_Blog_Count"] = "{0} posts",
            ["Notion_Blog_New"] = "New",
            ["Notion_Blog_Publish"] = "Publish",
            ["Notion_Blog_Draft"] = "Draft",
            ["Notion_Blog_Published"] = "Published",
            ["Notion_Blog_PublishedOn"] = "Published {0}",
            ["Notion_Blog_Empty"] = "No blog posts in this space.",
            ["Notion_Blog_LoadError"] = "Blog posts could not be loaded.",
            ["Notion_Blog_Paging"] = "Blog paging",
            ["Notion_Blog_Previous"] = "Previous",
            ["Notion_Blog_Next"] = "Next",
            ["Notion_Blog_Page"] = "Page {0}",
            ["Notion_Blog_NoPostSelected"] = "Select a post to read it.",
            ["Notion_Blog_NewPostTitle"] = "Untitled blog post",
            ["Notion_Blog_NewPostBody"] = "Draft the post body here.",
            ["TmNotionBlock_ParagraphPlaceholder"] = "Paragraph",
            ["TmNotionBlock_Comments"] = "Comments",
            ["TmNotionBlock_ThreadTooltipLabel"] = "Thread"
        });
    }

    [Fact]
    public void BlogListRendersPostsChronologically()
    {
        var provider = new FakeBlogProvider(SamplePosts());
        var cut = RenderBlog(provider);

        cut.WaitForAssertion(() =>
        {
            var titles = cut.FindAll("[data-testid='notion-blog-post-item'] .tm-notion-blog__post-title")
                .Select(title => title.TextContent.Trim())
                .ToArray();
            titles.Should().Equal("Newest post", "Older post", "Draft post");
        });
    }

    [Fact]
    public void BlogPostViewReusesReadOnlyBlockRenderer()
    {
        var provider = new FakeBlogProvider(SamplePosts());
        var cut = RenderBlog(provider);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='notion-blog-post']").TextContent.Should().Contain("Newest body");
            cut.Find(".tm-notion-block-list").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task CanCreateAndPublishDraftPost()
    {
        var provider = new FakeBlogProvider(SamplePosts());
        var cut = RenderBlog(provider);
        cut.WaitForAssertion(() => cut.Find("[data-testid='notion-blog-new']").Should().NotBeNull());

        await cut.Find("[data-testid='notion-blog-new']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Untitled blog post"));

        await cut.Find("[data-testid='notion-blog-publish']").ClickAsync(new MouseEventArgs());

        provider.PublishedPostIds.Should().ContainSingle();
        cut.WaitForAssertion(() => cut.Find("[data-testid='notion-blog-post']").TextContent.Should().Contain("Published"));
    }

    [Fact]
    public void EmptySpaceShowsLocalizedEmptyState()
    {
        var cut = RenderBlog(new FakeBlogProvider([]));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='notion-blog-empty']").TextContent.Should().Contain("No blog posts"));
    }

    private IRenderedComponent<ContainerFragment> RenderBlog(INotionBlogProvider provider)
    {
        var context = new NotionEditorContext
        {
            DataProvider = new FakeDataProvider(),
            BlockService = new FakeBlockService()
        };

        return Render(builder =>
        {
            builder.OpenComponent<CascadingValue<NotionEditorContext>>(0);
            builder.AddAttribute(1, "Value", context);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(child =>
            {
                child.OpenComponent<TmNotionBlogList>(3);
                child.AddAttribute(4, nameof(TmNotionBlogList.BlogProvider), provider);
                child.AddAttribute(5, nameof(TmNotionBlogList.SpaceId), "team");
                child.AddAttribute(6, nameof(TmNotionBlogList.CurrentUserId), "alice");
                child.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static IReadOnlyList<NotionBlogPostDto> SamplePosts()
        =>
        [
            Post("old", "Older post", new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc), "Older body"),
            Post("new", "Newest post", new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc), "Newest body"),
            Post("draft", "Draft post", null, "Draft body")
        ];

    private static NotionBlogPostDto Post(string id, string title, DateTime? publishedAt, string body)
        => new()
        {
            Id = id,
            SpaceId = "team",
            Title = title,
            PublishedAt = publishedAt,
            AuthorId = "alice",
            Blocks =
            [
                new PageBlock
                {
                    Id = Guid.NewGuid(),
                    PageId = Guid.NewGuid(),
                    Type = BlockType.Paragraph,
                    Order = 0,
                    Content = new TextBlockContent { Html = body },
                    CreatedAt = DateTime.UtcNow,
                    LastEditedAt = DateTime.UtcNow
                }
            ]
        };

    private sealed class FakeBlogProvider : INotionBlogProvider
    {
        private readonly List<NotionBlogPostDto> _posts;

        public FakeBlogProvider(IEnumerable<NotionBlogPostDto> posts)
            => _posts = posts.Select(Clone).ToList();

        public List<string> PublishedPostIds { get; } = [];

        public Task<PagedResult<NotionBlogPostDto>> GetPostsAsync(string spaceId, NotionBlogQuery paging, CancellationToken cancellationToken = default)
        {
            var items = _posts
                .Where(post => post.SpaceId == spaceId)
                .Where(post => paging.IncludeDrafts || post.PublishedAt is not null)
                .OrderByDescending(post => post.PublishedAt ?? DateTime.MinValue)
                .ThenBy(post => post.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult(new PagedResult<NotionBlogPostDto>
            {
                Items = items.Skip(paging.Skip).Take(paging.Take).Select(Clone).ToArray(),
                TotalCount = items.Length,
                Page = paging.Skip / paging.Take + 1,
                PageSize = paging.Take
            });
        }

        public Task<NotionBlogPostDto?> GetPostAsync(string postId, CancellationToken cancellationToken = default)
            => Task.FromResult(_posts.FirstOrDefault(post => post.Id == postId) is { } post ? Clone(post) : null);

        public Task<NotionBlogPostDto> CreatePostAsync(CreateNotionBlogPostRequest request, CancellationToken cancellationToken = default)
        {
            var post = new NotionBlogPostDto
            {
                Id = "created",
                SpaceId = request.SpaceId,
                Title = request.Title,
                AuthorId = request.AuthorId,
                Blocks = request.Blocks
            };
            _posts.Add(Clone(post));
            return Task.FromResult(Clone(post));
        }

        public Task<NotionBlogPostDto> PublishAsync(string postId, PublishNotionBlogPostRequest request, CancellationToken cancellationToken = default)
        {
            var post = _posts.Single(item => item.Id == postId);
            post.PublishedAt = request.PublishedAt ?? new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            PublishedPostIds.Add(postId);
            return Task.FromResult(Clone(post));
        }

        private static NotionBlogPostDto Clone(NotionBlogPostDto post) => new()
        {
            Id = post.Id,
            SpaceId = post.SpaceId,
            Title = post.Title,
            PublishedAt = post.PublishedAt,
            AuthorId = post.AuthorId,
            Blocks = post.Blocks
        };
    }

    private sealed class FakeBlockService : INotionEditorBlockService
    {
        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId) => Task.FromResult<IEnumerable<IPageBlock>>([]);
        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId) => Task.FromResult<IEnumerable<IPageBlock>>([]);
        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId) => Task.FromResult(block);
        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId) => Task.FromResult(blocks);
        public Task UpdateBlockAsync(IPageBlock block) => Task.CompletedTask;
        public Task DeleteBlockAsync(string blockId) => Task.CompletedTask;
        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds) => Task.CompletedTask;
        public Task MoveBlockAsync(MoveNotionBlockRequest request) => Task.CompletedTask;
        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId) => Task.CompletedTask;
        public Task<IPageBlock> DuplicateBlockAsync(string blockId) => throw new KeyNotFoundException(blockId);
        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType) => throw new KeyNotFoundException(blockId);
        public Task<string> GetBlockLinkAsync(string blockId) => Task.FromResult($"#{blockId}");
    }

    private sealed class FakeDataProvider : INotionDataProvider
    {
        public Task<INotionPage> GetPageAsync(string pageId) => Task.FromResult<INotionPage>(new NotionPage { Id = Guid.Parse(pageId), Title = pageId });
        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId) => Task.FromResult<IEnumerable<INotionPage>>([]);
        public Task<IEnumerable<INotionPage>> GetFavoritesAsync() => Task.FromResult<IEnumerable<INotionPage>>([]);
        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count) => Task.FromResult<IEnumerable<INotionPage>>([]);
        public Task<IEnumerable<INotionPage>> GetTrashAsync() => Task.FromResult<IEnumerable<INotionPage>>([]);
        public Task<INotionPage> CreatePageAsync(string? parentId, string title) => Task.FromResult<INotionPage>(new NotionPage { Id = Guid.NewGuid(), ParentId = parentId is null ? null : Guid.Parse(parentId), Title = title });
        public Task UpdatePageAsync(INotionPage page) => Task.CompletedTask;
        public Task DeletePageAsync(string pageId) => Task.CompletedTask;
        public Task RestorePageAsync(string pageId) => Task.CompletedTask;
        public Task PermanentlyDeletePageAsync(string pageId) => Task.CompletedTask;
        public Task ToggleFavoriteAsync(string pageId, bool isFavorite) => Task.CompletedTask;
        public Task MovePageAsync(string pageId, string? newParentId) => Task.CompletedTask;
        public Task<INotionPage> DuplicatePageAsync(string pageId) => Task.FromResult<INotionPage>(new NotionPage { Id = Guid.NewGuid(), Title = pageId });
        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<INotionPage>>([]);
        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
