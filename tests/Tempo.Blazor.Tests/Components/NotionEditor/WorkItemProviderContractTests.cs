using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.NotionEditor.Enums;
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
            SourceKey = "demo",
            ExternalId = "DEMO-101",
            DisplayMode = WorkItemDisplayMode.Card,
            CachedSnapshot = new TmWorkItem
            {
                Id = "DEMO-101",
                SourceKey = "demo",
                ExternalId = "DEMO-101",
                Url = "https://tracker.example/work/DEMO-101",
                Title = "Prepare release checklist",
                StatusLabel = "In Progress",
                StatusColor = "#f59e0b",
                TypeLabel = "Story",
                TypeIconUrl = "https://tracker.example/icons/story.svg",
                Assignees = [new TmWorkItemAssignee { Id = "ada", Name = "Ada Lovelace" }],
                PriorityLabel = "High",
                UpdatedAt = new DateTime(2026, 6, 1, 10, 15, 0, DateTimeKind.Utc),
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
        workItem.SourceKey.Should().Be("demo");
        workItem.ExternalId.Should().Be("DEMO-101");
        workItem.DisplayMode.Should().Be(WorkItemDisplayMode.Card);
        workItem.CachedSnapshot.Should().NotBeNull();
        workItem.CachedSnapshot!.Fields.Should().Contain("Sprint", "CF5");
        workItem.CachedSnapshot.Assignees.Should().ContainSingle()
            .Which.Name.Should().Be("Ada Lovelace");
    }
}
