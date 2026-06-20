using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionBlogContractTests
{
    [Fact]
    public void NotionBlogPostDto_RoundtripsThroughJson()
    {
        var post = new NotionBlogPostDto
        {
            Id = "post-1",
            SpaceId = "team",
            Title = "Release notes",
            PublishedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            AuthorId = "alice",
            Blocks =
            [
                new PageBlock
                {
                    Id = Guid.Parse("cf300000-0000-0000-0000-000000000001"),
                    PageId = Guid.Parse("cf300000-0000-0000-0000-000000000010"),
                    Type = BlockType.Paragraph,
                    Order = 0,
                    Content = new TextBlockContent { Html = "Published body" },
                    CreatedAt = new DateTime(2026, 1, 14, 10, 0, 0, DateTimeKind.Utc),
                    LastEditedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var json = JsonSerializer.Serialize(post, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<NotionBlogPostDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().BeEquivalentTo(post);
        restored!.Blocks.Single().Content.Should().BeOfType<TextBlockContent>()
            .Which.Html.Should().Be("Published body");
    }

    [Fact]
    public async Task INotionBlogProvider_CreatesPublishesAndPagesChronologically()
    {
        var provider = new InMemoryBlogProvider();
        var older = await provider.CreatePostAsync(new CreateNotionBlogPostRequest { SpaceId = "team", Title = "Older", AuthorId = "alice" });
        var newer = await provider.CreatePostAsync(new CreateNotionBlogPostRequest { SpaceId = "team", Title = "Newer", AuthorId = "alice" });
        await provider.PublishAsync(older.Id, new PublishNotionBlogPostRequest { PublishedAt = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc) });
        await provider.PublishAsync(newer.Id, new PublishNotionBlogPostRequest { PublishedAt = new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc) });

        var page = await provider.GetPostsAsync("team", new NotionBlogQuery { IncludeDrafts = false, Skip = 0, Take = 1 });

        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle().Which.Title.Should().Be("Newer");
        page.HasNextPage.Should().BeTrue();
    }

    private sealed class InMemoryBlogProvider : INotionBlogProvider
    {
        private readonly Dictionary<string, NotionBlogPostDto> _posts = new(StringComparer.OrdinalIgnoreCase);

        public Task<PagedResult<NotionBlogPostDto>> GetPostsAsync(string spaceId, NotionBlogQuery paging, CancellationToken cancellationToken = default)
        {
            var items = _posts.Values
                .Where(post => post.SpaceId == spaceId)
                .Where(post => paging.IncludeDrafts || post.PublishedAt is not null)
                .OrderByDescending(post => post.PublishedAt ?? DateTime.MinValue)
                .ToArray();

            return Task.FromResult(new PagedResult<NotionBlogPostDto>
            {
                Items = items.Skip(paging.Skip).Take(paging.Take).ToArray(),
                TotalCount = items.Length,
                Page = paging.Skip / paging.Take + 1,
                PageSize = paging.Take
            });
        }

        public Task<NotionBlogPostDto?> GetPostAsync(string postId, CancellationToken cancellationToken = default)
            => Task.FromResult(_posts.GetValueOrDefault(postId));

        public Task<NotionBlogPostDto> CreatePostAsync(CreateNotionBlogPostRequest request, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid().ToString("D");
            var post = new NotionBlogPostDto
            {
                Id = id,
                SpaceId = request.SpaceId,
                Title = request.Title,
                AuthorId = request.AuthorId,
                Blocks = request.Blocks
            };
            _posts[id] = post;
            return Task.FromResult(post);
        }

        public Task<NotionBlogPostDto> PublishAsync(string postId, PublishNotionBlogPostRequest request, CancellationToken cancellationToken = default)
        {
            var post = _posts[postId];
            post.PublishedAt = request.PublishedAt ?? DateTime.UtcNow;
            return Task.FromResult(post);
        }
    }
}
