using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class TodoBlockActionItemSerializationTests
{
    [Fact]
    public void TodoBlockContent_ActionItemMetadata_Roundtrips()
    {
        var dueDate = DateTime.Today.AddDays(2);
        var content = new TodoBlockContent
        {
            Html = "Review rollout checklist",
            IsChecked = false,
            AssigneeId = "alice",
            AssigneeDisplayName = "Alice Johnson",
            DueDate = dueDate
        };

        var json = JsonSerializer.Serialize(content);
        var restored = JsonSerializer.Deserialize<TodoBlockContent>(json);

        restored.Should().NotBeNull();
        restored!.AssigneeId.Should().Be("alice");
        restored.AssigneeDisplayName.Should().Be("Alice Johnson");
        restored.DueDate.Should().Be(dueDate);
        json.Should().NotContain(nameof(TodoBlockContent.IsOverdue));
    }

    [Fact]
    public void TodoBlockContent_LegacyPayloadWithoutActionItemMetadata_UsesNullDefaults()
    {
        const string LegacyJson = """
                                  {
                                    "IsChecked": false,
                                    "Html": "Legacy todo"
                                  }
                                  """;

        var restored = JsonSerializer.Deserialize<TodoBlockContent>(LegacyJson);

        restored.Should().NotBeNull();
        restored!.AssigneeId.Should().BeNull();
        restored.AssigneeDisplayName.Should().BeNull();
        restored.DueDate.Should().BeNull();
        restored.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void TodoBlockContent_IsOverdue_RequiresPastDueDateAndUncheckedState()
    {
        new TodoBlockContent
        {
            IsChecked = false,
            DueDate = DateTime.Today.AddDays(-1)
        }.IsOverdue.Should().BeTrue();

        new TodoBlockContent
        {
            IsChecked = true,
            DueDate = DateTime.Today.AddDays(-1)
        }.IsOverdue.Should().BeFalse();

        new TodoBlockContent
        {
            IsChecked = false,
            DueDate = DateTime.Today
        }.IsOverdue.Should().BeFalse();
    }
}
