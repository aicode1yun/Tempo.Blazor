using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class WorkItemProviderContractTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void WorkItemBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        IBlockContent content = new WorkItemBlockContent
        {
            ProviderKey = "demo",
            ExternalId = "DEMO-101",
            DisplayMode = WorkItemDisplayMode.Card,
            CachedSnapshot = new WorkItemDto
            {
                ProviderKey = "demo",
                ExternalId = "DEMO-101",
                Url = "https://tracker.example/work/DEMO-101",
                Title = "Prepare release checklist",
                Status = "In Progress",
                StatusColor = "#f59e0b",
                TypeLabel = "Story",
                TypeIconUrl = "https://tracker.example/icons/story.svg",
                AssigneeDisplayName = "Ada Lovelace",
                Priority = "High",
                UpdatedAt = new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero),
                Fields = new Dictionary<string, string>
                {
                    ["Sprint"] = "CF5",
                    ["Team"] = "Editor"
                }
            }
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<WorkItemBlockContent>();
        var workItem = (WorkItemBlockContent)restored!;
        workItem.ProviderKey.Should().Be("demo");
        workItem.ExternalId.Should().Be("DEMO-101");
        workItem.DisplayMode.Should().Be(WorkItemDisplayMode.Card);
        workItem.CachedSnapshot.Should().NotBeNull();
        workItem.CachedSnapshot!.Fields.Should().Contain("Sprint", "CF5");
    }

    [Fact]
    public async Task WorkItemProviderRegistry_SeparatesProvidersByProviderKey()
    {
        var demo = new InMemoryWorkItemProvider("demo", "Demo tracker", [
            new WorkItemDto { ProviderKey = "demo", ExternalId = "DEMO-101", Title = "Demo item" }
        ]);
        var ops = new InMemoryWorkItemProvider("ops", "Ops tracker", [
            new WorkItemDto { ProviderKey = "ops", ExternalId = "OPS-7", Title = "Ops item" }
        ]);

        var registry = new WorkItemProviderRegistry([demo, ops], NullLogger<WorkItemProviderRegistry>.Instance);

        registry.Count.Should().Be(2);
        registry.GetProvider("demo").Should().BeSameAs(demo);
        registry.GetProvider("ops").Should().BeSameAs(ops);
        registry.GetAll().Select(provider => provider.ProviderKey).Should().Equal("demo", "ops");

        var demoItem = await registry.GetProvider("demo")!.GetByIdAsync("DEMO-101", CancellationToken.None);
        var opsItem = await registry.GetProvider("ops")!.GetByIdAsync("OPS-7", CancellationToken.None);

        demoItem!.ProviderKey.Should().Be("demo");
        opsItem!.ProviderKey.Should().Be("ops");
    }

    private sealed class InMemoryWorkItemProvider : IWorkItemProvider
    {
        private readonly IReadOnlyList<WorkItemDto> _items;

        public InMemoryWorkItemProvider(string providerKey, string displayName, IReadOnlyList<WorkItemDto> items)
        {
            ProviderKey = providerKey;
            DisplayName = displayName;
            _items = items;
        }

        public string ProviderKey { get; }
        public string DisplayName { get; }

        public Task<WorkItemDto?> GetByIdAsync(string externalId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(item =>
                string.Equals(item.ExternalId, externalId, StringComparison.OrdinalIgnoreCase)));

        public Task<PagedResult<WorkItemDto>> SearchAsync(WorkItemQuery query, CancellationToken cancellationToken)
        {
            var matches = _items
                .Where(item => string.IsNullOrWhiteSpace(query.FreeText)
                    || item.ExternalId.Contains(query.FreeText, StringComparison.OrdinalIgnoreCase)
                    || item.Title.Contains(query.FreeText, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return Task.FromResult(new PagedResult<WorkItemDto>
            {
                Items = matches,
                TotalCount = matches.Length,
                Page = 1,
                PageSize = matches.Length
            });
        }
    }
}
