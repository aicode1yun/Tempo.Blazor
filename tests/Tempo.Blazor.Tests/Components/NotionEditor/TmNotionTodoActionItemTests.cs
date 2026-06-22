using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.NotionEditor.Blocks.Lists;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class TmNotionTodoActionItemTests : LocalizationTestBase
{
    public TmNotionTodoActionItemTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Notion_Todo_Toggle"] = "Toggle action item",
            ["Notion_Todo_Assign"] = "Assign",
            ["Notion_Todo_DueDate"] = "Due date",
            ["Notion_Todo_Overdue"] = "Overdue",
            ["Notion_Todo_Unassign"] = "Unassign",
            ["Notion_Todo_AssigneeSearch"] = "Search people",
            ["Notion_Todo_LoadingUsers"] = "Loading people",
            ["Notion_Todo_NoAssignees"] = "No people found",
            ["Notion_Todo_Today"] = "Today",
            ["Notion_Todo_Tomorrow"] = "Tomorrow",
            ["TmDatePicker_Placeholder"] = "Select date",
            ["TmDatePicker_Clear"] = "Clear date",
            ["TmDatePicker_Today"] = "Today",
            ["TmDatePicker_PreviousMonth"] = "Previous month",
            ["TmDatePicker_NextMonth"] = "Next month"
        });
    }

    [Fact]
    public void TodoWithAssignee_RendersAvatarAndDisplayName()
    {
        var cut = RenderTodo(new TodoBlockContent
        {
            Html = "Assigned task",
            AssigneeId = "alice",
            AssigneeDisplayName = "Alice Johnson"
        });

        cut.Find(".tm-notion-todo__assignee-name").TextContent.Should().Be("Alice Johnson");
        cut.Find(".tm-notion-todo__avatar").TextContent.Should().Be("AJ");
    }

    [Fact]
    public void TodoWithDueDate_RendersCultureFormattedDate()
    {
        // Future date so the todo is never overdue (otherwise the component
        // prefixes the text with "Overdue · "); this test only checks formatting.
        var dueDate = DateTime.Today.AddDays(30);
        var cut = RenderTodo(new TodoBlockContent
        {
            Html = "Scheduled task",
            DueDate = dueDate
        });

        cut.Find(".tm-notion-todo__due").TextContent.Should().Be(dueDate.ToString("d"));
    }

    [Fact]
    public void UncheckedPastDueTodo_RendersOverdueWarningStyle()
    {
        var cut = RenderTodo(new TodoBlockContent
        {
            Html = "Late task",
            IsChecked = false,
            DueDate = DateTime.Today.AddDays(-1)
        });

        cut.Find(".tm-notion-todo").ClassList.Should().Contain("tm-notion-todo--overdue");
        cut.Find(".tm-notion-todo__due").ClassList.Should().Contain("tm-notion-todo__due--overdue");
        cut.Find(".tm-notion-todo__due").TextContent.Should().Contain("Overdue");
    }

    [Fact]
    public void CheckedPastDueTodo_DoesNotRenderOverdueWarningStyle()
    {
        var cut = RenderTodo(new TodoBlockContent
        {
            Html = "Completed late task",
            IsChecked = true,
            DueDate = DateTime.Today.AddDays(-1)
        });

        cut.Find(".tm-notion-todo").ClassList.Should().NotContain("tm-notion-todo--overdue");
        cut.Find(".tm-notion-todo__due").ClassList.Should().NotContain("tm-notion-todo__due--overdue");
    }

    [Fact]
    public async Task AssignButton_UsesMentionProviderAndRaisesSelectedAssignee()
    {
        (string? Id, string? DisplayName) selected = default;
        var cut = RenderTodo(new TodoBlockContent { Html = "Assignable task" },
            new FakeMentionProvider(),
            EventCallback.Factory.Create<(string?, string?)>(this, value => selected = value));

        await cut.Find(".tm-notion-todo__action").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Find(".tm-notion-todo__user-name").TextContent.Should().Be("Alice Johnson"));

        await cut.Find(".tm-notion-todo__user").ClickAsync(new MouseEventArgs());

        selected.Should().Be(("alice", "Alice Johnson"));
    }

    [Fact]
    public void AssignButton_WithoutMentionProvider_IsHidden()
    {
        var cut = RenderTodo(new TodoBlockContent { Html = "Providerless task" }, mentionProvider: null);

        cut.FindAll(".tm-notion-todo__action")
            .Select(button => button.TextContent.Trim())
            .Should().NotContain("Assign");
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderTodo(
        TodoBlockContent content,
        ITmPeopleProvider? mentionProvider = null,
        EventCallback<(string?, string?)> assigneeChanged = default)
    {
        var context = new NotionEditorContext { MentionProvider = mentionProvider };

        return RenderComponent<CascadingValue<NotionEditorContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<TmNotionTodoBlock>(todo => todo
                .Add(x => x.Content, content)
                .Add(x => x.ReadOnly, false)
                .Add(x => x.OnAssigneeChanged, assigneeChanged)));
    }

    private sealed class FakeMentionProvider : TmPeopleProviderBase
    {
        public override Task<IReadOnlyList<TmUser>> SearchAsync(TmPeopleQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TmUser>>([new TmUser
            {
                Id = "alice",
                UserName = "alice",
                DisplayName = "Alice Johnson",
                Email = "alice@example.test"
            }]);

    }
}
