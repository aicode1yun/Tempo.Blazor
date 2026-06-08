using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionSpacesContractTests
{
    [Fact]
    public void NotionSpaceDto_RoundtripsThroughJson()
    {
        var space = new NotionSpaceDto
        {
            Id = "space-team",
            Key = "TEAM",
            Name = "Team Space",
            Description = "Shared engineering knowledge.",
            IconEmoji = "T",
            HomePageId = "11111111-1111-1111-1111-111111111111",
            Type = NotionSpaceType.Team
        };

        var json = JsonSerializer.Serialize(space, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<NotionSpaceDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().BeEquivalentTo(space);
    }

    [Fact]
    public void NotionPage_SpaceId_IsOptionalAndJsonBackwardsCompatible()
    {
        var legacyJson = """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "parentId": null,
              "title": "Legacy page"
            }
            """;

        var legacy = JsonSerializer.Deserialize<NotionPage>(legacyJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        legacy.Should().NotBeNull();
        legacy!.SpaceId.Should().BeNull();

        var page = new NotionPage
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Title = "Space-aware page",
            SpaceId = "space-team"
        };

        var json = JsonSerializer.Serialize(page, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<NotionPage>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().NotBeNull();
        restored!.SpaceId.Should().Be("space-team");
    }

    [Fact]
    public async Task INotionSpaceProvider_CreatesListsAndMovesPagesAtomically()
    {
        var page = new NotionPage
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "Architecture",
            SpaceId = "space-team"
        };
        var provider = new InMemorySpaceProvider([page]);

        await provider.CreateSpaceAsync(new NotionSpaceDto
        {
            Id = "space-public",
            Key = "PUBLIC",
            Name = "Public",
            Type = NotionSpaceType.Public
        });

        var spaces = await provider.GetSpacesAsync();
        spaces.Should().Contain(space => space.Id == "space-public");

        await provider.MovePageToSpaceAsync(page.Id.ToString("D"), "space-public");
        var publicPages = await provider.GetPagesInSpaceAsync("space-public");
        publicPages.Should().ContainSingle(p => p.Id == page.Id)
            .Which.SpaceId.Should().Be("space-public");
    }

    private sealed class InMemorySpaceProvider : INotionSpaceProvider
    {
        private readonly Dictionary<string, NotionSpaceDto> _spaces = new(StringComparer.OrdinalIgnoreCase)
        {
            ["space-team"] = new NotionSpaceDto
            {
                Id = "space-team",
                Key = "TEAM",
                Name = "Team",
                Type = NotionSpaceType.Team
            }
        };
        private readonly Dictionary<Guid, NotionPage> _pages;
        private readonly object _gate = new();

        public InMemorySpaceProvider(IEnumerable<NotionPage> pages)
            => _pages = pages.ToDictionary(page => page.Id);

        public Task<IReadOnlyList<NotionSpaceDto>> GetSpacesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotionSpaceDto>>(_spaces.Values.OrderBy(space => space.Name).ToArray());

        public Task<NotionSpaceDto?> GetSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_spaces.GetValueOrDefault(spaceId));

        public Task<NotionSpaceDto> CreateSpaceAsync(NotionSpaceDto space, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(space.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(space.Key);
            ArgumentException.ThrowIfNullOrWhiteSpace(space.Name);

            lock (_gate)
            {
                if (_spaces.ContainsKey(space.Id))
                    throw new InvalidOperationException("Space already exists.");

                _spaces[space.Id] = space;
            }

            return Task.FromResult(space);
        }

        public Task<IReadOnlyList<INotionPage>> GetPagesInSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                var pages = _pages.Values
                    .Where(page => string.Equals(page.SpaceId, spaceId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
                    .Cast<INotionPage>()
                    .ToArray();

                return Task.FromResult<IReadOnlyList<INotionPage>>(pages);
            }
        }

        public Task MovePageToSpaceAsync(string pageId, string spaceId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_spaces.ContainsKey(spaceId))
                    throw new KeyNotFoundException($"Space {spaceId} not found.");

                var parsed = Guid.Parse(pageId);
                if (!_pages.TryGetValue(parsed, out var page))
                    throw new KeyNotFoundException($"Page {pageId} not found.");

                page.SpaceId = spaceId;
                page.ParentId = null;
                page.LastEditedAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }
    }
}
