using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionBlogProvider : INotionBlogProvider
{
    private static readonly DateTime SeedNow = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
    private readonly Dictionary<string, NotionBlogPostDto> _posts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public DemoNotionBlogProvider()
    {
        Reset();
    }

    public Task<PagedResult<NotionBlogPostDto>> GetPostsAsync(string spaceId, NotionBlogQuery paging, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spaceId);
        paging ??= new NotionBlogQuery();

        lock (_gate)
        {
            var skip = Math.Max(0, paging.Skip);
            var take = Math.Clamp(paging.Take, 1, 100);
            var filtered = _posts.Values
                .Where(post => string.Equals(post.SpaceId, spaceId, StringComparison.OrdinalIgnoreCase))
                .Where(post => paging.IncludeDrafts || post.PublishedAt is not null)
                .OrderByDescending(post => post.PublishedAt ?? DateTime.MinValue)
                .ThenBy(post => post.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var result = new PagedResult<NotionBlogPostDto>
            {
                Items = filtered.Skip(skip).Take(take).Select(Clone).ToArray(),
                TotalCount = filtered.Length,
                Page = skip / take + 1,
                PageSize = take
            };

            return Task.FromResult(result);
        }
    }

    public Task<NotionBlogPostDto?> GetPostAsync(string postId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);
        lock (_gate)
        {
            return Task.FromResult(_posts.TryGetValue(postId, out var post) ? Clone(post) : null);
        }
    }

    public Task<NotionBlogPostDto> CreatePostAsync(CreateNotionBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AuthorId);

        var id = Guid.NewGuid();
        var blocks = request.Blocks.Count == 0
            ? CreateDefaultBlocks(id, request.Title)
            : request.Blocks.Select(block => CloneBlock(block, id)).ToArray();

        var post = new NotionBlogPostDto
        {
            Id = id.ToString("D"),
            SpaceId = request.SpaceId,
            Title = request.Title.Trim(),
            AuthorId = request.AuthorId.Trim(),
            PublishedAt = null,
            Blocks = blocks
        };

        lock (_gate)
        {
            _posts[post.Id] = Clone(post);
        }

        return Task.FromResult(Clone(post));
    }

    public Task<NotionBlogPostDto> PublishAsync(string postId, PublishNotionBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);
        request ??= new PublishNotionBlogPostRequest();

        lock (_gate)
        {
            if (!_posts.TryGetValue(postId, out var post))
                throw new KeyNotFoundException($"Blog post {postId} not found.");

            post.PublishedAt = request.PublishedAt ?? DateTime.UtcNow;
            _posts[postId] = Clone(post);
            return Task.FromResult(Clone(post));
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _posts.Clear();
            SeedDefaultPosts();
        }
    }

    public void SeedE2EBlog()
    {
        lock (_gate)
        {
            _posts.Clear();
            AddSeedPost(
                "cf300001-0000-0000-0000-000000000001",
                "team",
                "Launch notes for Tempo editor",
                "alice",
                SeedNow.AddDays(-1),
                [
                    Block("cf300001-0000-0000-0000-000000000101", "cf300001-0000-0000-0000-000000000001", BlockType.Heading2, 0, new HeadingBlockContent { Level = 2, Html = "Launch highlights" }),
                    Block("cf300001-0000-0000-0000-000000000102", "cf300001-0000-0000-0000-000000000001", BlockType.Paragraph, 1, new TextBlockContent { Html = "The new blog surface reuses the same read-only Notion block renderer as regular pages." })
                ]);
            AddSeedPost(
                "cf300002-0000-0000-0000-000000000002",
                "team",
                "Knowledge base cleanup",
                "bob",
                SeedNow.AddDays(-5),
                [
                    Block("cf300002-0000-0000-0000-000000000101", "cf300002-0000-0000-0000-000000000002", BlockType.Paragraph, 0, new TextBlockContent { Html = "Archived pages were moved into the public documentation space." })
                ]);
            AddSeedPost(
                "cf300003-0000-0000-0000-000000000003",
                "team",
                "Draft migration checklist",
                "alice",
                null,
                [
                    Block("cf300003-0000-0000-0000-000000000101", "cf300003-0000-0000-0000-000000000003", BlockType.TodoItem, 0, new TodoBlockContent { Html = "Confirm migration date", IsChecked = false })
                ]);
        }
    }

    public void SeedE2EEmptyBlog()
    {
        lock (_gate)
        {
            _posts.Clear();
        }
    }

    public void SeedE2EManyBlogPosts()
    {
        lock (_gate)
        {
            _posts.Clear();
            for (var i = 0; i < 13; i++)
            {
                var id = Guid.Parse($"cf3001{i:00}-0000-0000-0000-000000000001");
                AddSeedPost(
                    id.ToString("D"),
                    "team",
                    $"Blog pagination entry {i + 1:00}",
                    i % 2 == 0 ? "alice" : "bob",
                    SeedNow.AddDays(-i),
                    [
                        Block(Guid.NewGuid().ToString("D"), id.ToString("D"), BlockType.Paragraph, 0, new TextBlockContent { Html = $"Pagination body {i + 1:00}" })
                    ]);
            }
        }
    }

    private void SeedDefaultPosts()
    {
        AddSeedPost(
            "cf30d001-0000-0000-0000-000000000001",
            "team",
            "Tempo release digest",
            "demo",
            SeedNow.AddDays(-2),
            [
                Block("cf30d001-0000-0000-0000-000000000101", "cf30d001-0000-0000-0000-000000000001", BlockType.Paragraph, 0, new TextBlockContent { Html = "A short digest of the latest editor improvements." })
            ]);
    }

    private void AddSeedPost(string id, string spaceId, string title, string authorId, DateTime? publishedAt, IReadOnlyList<PageBlock> blocks)
    {
        _posts[id] = new NotionBlogPostDto
        {
            Id = id,
            SpaceId = spaceId,
            Title = title,
            AuthorId = authorId,
            PublishedAt = publishedAt,
            Blocks = blocks.Select(block => CloneBlock(block, Guid.Parse(id))).ToArray()
        };
    }

    private static IReadOnlyList<PageBlock> CreateDefaultBlocks(Guid postId, string title)
        =>
        [
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = postId,
                Type = BlockType.Paragraph,
                Order = 0,
                Content = new TextBlockContent { Html = title },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            }
        ];

    private static PageBlock Block(string id, string pageId, BlockType type, int order, IBlockContent content)
        => new()
        {
            Id = Guid.Parse(id),
            PageId = Guid.Parse(pageId),
            Type = type,
            Order = order,
            Content = content,
            CreatedAt = SeedNow.AddDays(-10),
            LastEditedAt = SeedNow
        };

    private static NotionBlogPostDto Clone(NotionBlogPostDto post)
        => new()
        {
            Id = post.Id,
            SpaceId = post.SpaceId,
            Title = post.Title,
            PublishedAt = post.PublishedAt,
            AuthorId = post.AuthorId,
            Blocks = post.Blocks.Select(block => CloneBlock(block, Guid.Parse(post.Id))).ToArray()
        };

    private static PageBlock CloneBlock(PageBlock block, Guid pageId)
        => new()
        {
            Id = block.Id == Guid.Empty ? Guid.NewGuid() : block.Id,
            PageId = pageId,
            ParentBlockId = block.ParentBlockId,
            Type = block.Type,
            Order = block.Order,
            Content = CloneContent(block.Content),
            CreatedAt = block.CreatedAt,
            LastEditedAt = block.LastEditedAt
        };

    private static IBlockContent CloneContent(IBlockContent content)
        => content switch
        {
            HeadingBlockContent heading => new HeadingBlockContent
            {
                Level = heading.Level,
                IsToggleable = heading.IsToggleable,
                Html = heading.Html,
                Mentions = heading.Mentions.ToArray(),
                BackgroundColor = heading.BackgroundColor,
                TextColor = heading.TextColor,
                Alignment = heading.Alignment
            },
            ListBlockContent list => new ListBlockContent
            {
                IndentLevel = list.IndentLevel,
                Html = list.Html,
                Mentions = list.Mentions.ToArray(),
                BackgroundColor = list.BackgroundColor,
                TextColor = list.TextColor,
                Alignment = list.Alignment
            },
            TodoBlockContent todo => new TodoBlockContent
            {
                Html = todo.Html,
                Mentions = todo.Mentions.ToArray(),
                IsChecked = todo.IsChecked,
                AssigneeId = todo.AssigneeId,
                AssigneeDisplayName = todo.AssigneeDisplayName,
                DueDate = todo.DueDate,
                BackgroundColor = todo.BackgroundColor,
                TextColor = todo.TextColor,
                Alignment = todo.Alignment
            },
            TextBlockContent text => new TextBlockContent
            {
                Html = text.Html,
                Mentions = text.Mentions.ToArray(),
                BackgroundColor = text.BackgroundColor,
                TextColor = text.TextColor,
                Alignment = text.Alignment
            },
            DividerBlockContent => new DividerBlockContent(),
            _ => content
        };
}
