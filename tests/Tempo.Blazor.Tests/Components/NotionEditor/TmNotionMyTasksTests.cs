using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class TmNotionMyTasksTests : LocalizationTestBase
{
    public TmNotionMyTasksTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Close"] = "Close",
            ["Tm_Loading"] = "Loading",
            ["Tm_Retry"] = "Retry",
            ["Notion_Tasks_Title"] = "My Tasks",
            ["Notion_Tasks_Count"] = "{0} tasks",
            ["Notion_Tasks_Filters"] = "Task filters",
            ["Notion_Tasks_Filter_Assignee"] = "Assignee filter",
            ["Notion_Tasks_Filter_My"] = "Mine",
            ["Notion_Tasks_Filter_All"] = "All",
            ["Notion_Tasks_Filter_Status"] = "Status filter",
            ["Notion_Tasks_Filter_Open"] = "Open",
            ["Notion_Tasks_Filter_Completed"] = "Done",
            ["Notion_Tasks_Filter_AllStatuses"] = "All statuses",
            ["Notion_Tasks_Filter_Due"] = "Due date filter",
            ["Notion_Tasks_Filter_DueAll"] = "Any due date",
            ["Notion_Tasks_Filter_Overdue"] = "Overdue",
            ["Notion_Tasks_Filter_Upcoming"] = "Upcoming",
            ["Notion_Tasks_GroupBy"] = "Group tasks by",
            ["Notion_Tasks_GroupBy_DueDate"] = "Due date",
            ["Notion_Tasks_GroupBy_Page"] = "Page",
            ["Notion_Tasks_LoadError"] = "Tasks could not be loaded.",
            ["Notion_Tasks_Empty"] = "No tasks match the current filters.",
            ["Notion_Tasks_ToggleComplete"] = "Toggle task completion",
            ["Notion_Tasks_Group_Overdue"] = "Overdue",
            ["Notion_Tasks_Group_Today"] = "Today",
            ["Notion_Tasks_Group_Tomorrow"] = "Tomorrow",
            ["Notion_Tasks_Group_Later"] = "Later",
            ["Notion_Tasks_Group_NoDueDate"] = "No due date",
            ["Notion_Tasks_Due_Today"] = "Today",
            ["Notion_Tasks_Due_Tomorrow"] = "Tomorrow",
            ["Notion_Tasks_Due_Overdue"] = "Overdue"
        });
    }

    [Fact]
    public void RendersTasksGroupedByDueDate()
    {
        var provider = new FakeTaskProvider(SampleTasks());

        var cut = RenderTasks(provider);

        cut.WaitForAssertion(() =>
        {
            cut.Find(".tm-my-tasks__title").TextContent.Should().Be("My Tasks");
            cut.FindAll(".tm-my-tasks__item").Should().HaveCount(3);
            cut.Markup.Should().Contain("Overdue");
            cut.Markup.Should().Contain("Today");
            cut.Markup.Should().Contain("Workspace Launch");
            cut.Markup.Should().Contain("Alice Johnson");
        });
    }

    [Fact]
    public void RendersEmptyStateWhenNoTasksMatchFilters()
    {
        var provider = new FakeTaskProvider([]);

        var cut = RenderTasks(provider);

        cut.WaitForAssertion(() =>
            cut.Find(".tm-my-tasks__empty").TextContent.Should().Contain("No tasks match"));
    }

    [Fact]
    public async Task CanGroupTasksByPage()
    {
        var provider = new FakeTaskProvider(SampleTasks());
        var cut = RenderTasks(provider);
        cut.WaitForAssertion(() => cut.FindAll(".tm-my-tasks__item").Should().NotBeEmpty());

        await cut.FindAll(".tm-my-tasks__filter")
            .Single(button => button.TextContent.Trim() == "Page")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var groupTitles = cut.FindAll(".tm-my-tasks__group-title")
                .Select(title => title.TextContent)
                .ToArray();
            groupTitles.Should().Contain(title => title.Contains("Release Plan"));
            groupTitles.Should().Contain(title => title.Contains("Workspace Launch"));
        });
    }

    [Fact]
    public async Task ToggleTaskPersistsCompletionThroughProvider()
    {
        var provider = new FakeTaskProvider(SampleTasks());
        var cut = RenderTasks(provider);
        cut.WaitForAssertion(() => cut.Find("[data-task-id='task-overdue']").Should().NotBeNull());

        await cut.Find("[data-task-id='task-overdue'] .tm-my-tasks__check").ClickAsync(new MouseEventArgs());

        provider.CompletedUpdates.Should().Contain(("task-overdue", true));
        cut.WaitForAssertion(() =>
            cut.FindAll(".tm-my-tasks__item").Select(item => item.GetAttribute("data-task-id"))
                .Should().NotContain("task-overdue"));
    }

    [Fact]
    public async Task CompletedFilter_CountMatchesVisibleCompletedTasks()
    {
        var provider = new FakeTaskProvider(SampleTasksWithCompleted());
        var cut = RenderTasks(provider);
        cut.WaitForAssertion(() => cut.FindAll(".tm-my-tasks__item").Should().HaveCount(3));

        await cut.FindAll(".tm-my-tasks__filter")
            .Single(button => button.TextContent.Trim() == "Done")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Find(".tm-my-tasks__count").TextContent.Should().Be("1 tasks");
            cut.FindAll(".tm-my-tasks__item").Should().ContainSingle()
                .Which.GetAttribute("data-task-id").Should().Be("task-completed");
        });
    }

    [Fact]
    public void LoadError_UsesLocalizedMessageInsteadOfProviderException()
    {
        var provider = new ThrowingTaskProvider("database-password-leaked");

        var cut = RenderTasks(provider);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Tasks could not be loaded.");
            cut.Markup.Should().NotContain("database-password-leaked");
        });
    }

    [Fact]
    public async Task ClickingTaskRaisesSourceNavigation()
    {
        var provider = new FakeTaskProvider(SampleTasks());
        (string PageId, string BlockId) selected = default;

        var cut = RenderTasks(provider, EventCallback.Factory.Create<(string, string)>(this, value => selected = value));
        cut.WaitForAssertion(() => cut.Find("[data-task-id='task-today']").Should().NotBeNull());

        await cut.Find("[data-task-id='task-today'] .tm-my-tasks__body").ClickAsync(new MouseEventArgs());

        selected.Should().Be(("page-a", "block-today"));
    }

    private IRenderedComponent<TmNotionMyTasks> RenderTasks(
        INotionTaskProvider provider,
        EventCallback<(string, string)> navigateCallback = default)
        => RenderComponent<TmNotionMyTasks>(parameters => parameters
            .Add(component => component.TaskProvider, provider)
            .Add(component => component.CurrentUserId, "alice")
            .Add(component => component.OnNavigateToBlock, navigateCallback));

    private static IReadOnlyList<NotionTaskDto> SampleTasks()
    {
        var now = DateTime.Today;
        return
        [
            new()
            {
                Id = "task-overdue",
                PageId = "page-a",
                PageTitle = "Workspace Launch",
                BlockId = "block-overdue",
                Text = "Send launch checklist",
                AssigneeId = "alice",
                AssigneeDisplayName = "Alice Johnson",
                DueDate = now.AddDays(-1),
                CreatedAt = now.AddDays(-4)
            },
            new()
            {
                Id = "task-today",
                PageId = "page-a",
                PageTitle = "Workspace Launch",
                BlockId = "block-today",
                Text = "Review onboarding copy",
                AssigneeId = "alice",
                AssigneeDisplayName = "Alice Johnson",
                DueDate = now,
                CreatedAt = now.AddDays(-3)
            },
            new()
            {
                Id = "task-release",
                PageId = "page-b",
                PageTitle = "Release Plan",
                BlockId = "block-release",
                Text = "Confirm rollout owner",
                AssigneeId = "alice",
                AssigneeDisplayName = "Alice Johnson",
                DueDate = now.AddDays(3),
                CreatedAt = now.AddDays(-2)
            },
            new()
            {
                Id = "task-other-user",
                PageId = "page-c",
                PageTitle = "Partner Notes",
                BlockId = "block-other",
                Text = "Not visible in Mine scope",
                AssigneeId = "bob",
                AssigneeDisplayName = "Bob Stone",
                DueDate = now,
                CreatedAt = now.AddDays(-1)
            }
        ];
    }

    private static IReadOnlyList<NotionTaskDto> SampleTasksWithCompleted()
    {
        var tasks = SampleTasks().Select(task => new NotionTaskDto
        {
            Id = task.Id,
            PageId = task.PageId,
            PageTitle = task.PageTitle,
            BlockId = task.BlockId,
            Text = task.Text,
            AssigneeId = task.AssigneeId,
            AssigneeDisplayName = task.AssigneeDisplayName,
            DueDate = task.DueDate,
            IsCompleted = task.IsCompleted,
            CreatedAt = task.CreatedAt
        }).ToList();

        tasks.Add(new NotionTaskDto
        {
            Id = "task-completed",
            PageId = "page-a",
            PageTitle = "Workspace Launch",
            BlockId = "block-completed",
            Text = "Archive launch notes",
            AssigneeId = "alice",
            AssigneeDisplayName = "Alice Johnson",
            DueDate = DateTime.Today.AddDays(-2),
            IsCompleted = true,
            CreatedAt = DateTime.Today.AddDays(-5)
        });

        return tasks;
    }

    private sealed class FakeTaskProvider : INotionTaskProvider
    {
        private readonly List<NotionTaskDto> _tasks;

        public FakeTaskProvider(IEnumerable<NotionTaskDto> tasks)
        {
            _tasks = tasks.Select(Clone).ToList();
        }

        public List<(string TaskId, bool Completed)> CompletedUpdates { get; } = [];

        public Task<PagedResult<NotionTaskDto>> GetTasksAsync(NotionTaskQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<NotionTaskDto> filtered = _tasks;

            if (!string.IsNullOrWhiteSpace(query.AssigneeId))
                filtered = filtered.Where(task => string.Equals(task.AssigneeId, query.AssigneeId, StringComparison.OrdinalIgnoreCase));

            if (!query.IncludeCompleted)
                filtered = filtered.Where(task => !task.IsCompleted);

            if (query.DueBefore is DateTime dueBefore)
                filtered = filtered.Where(task => task.DueDate is not null && task.DueDate.Value.Date <= dueBefore.Date);

            if (query.DueAfter is DateTime dueAfter)
                filtered = filtered.Where(task => task.DueDate is not null && task.DueDate.Value.Date >= dueAfter.Date);

            var items = filtered
                .OrderBy(task => task.DueDate ?? DateTime.MaxValue)
                .ThenBy(task => task.PageTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(new PagedResult<NotionTaskDto>
            {
                Items = items.Skip(query.Skip).Take(query.Take).Select(Clone).ToList(),
                TotalCount = items.Count,
                Page = query.Take <= 0 ? 1 : (query.Skip / query.Take) + 1,
                PageSize = query.Take
            });
        }

        public Task SetCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default)
        {
            CompletedUpdates.Add((taskId, completed));
            var task = _tasks.Single(item => item.Id == taskId);
            task.IsCompleted = completed;
            return Task.CompletedTask;
        }

        private static NotionTaskDto Clone(NotionTaskDto task) => new()
        {
            Id = task.Id,
            PageId = task.PageId,
            PageTitle = task.PageTitle,
            BlockId = task.BlockId,
            Text = task.Text,
            AssigneeId = task.AssigneeId,
            AssigneeDisplayName = task.AssigneeDisplayName,
            DueDate = task.DueDate,
            IsCompleted = task.IsCompleted,
            CreatedAt = task.CreatedAt
        };
    }

    private sealed class ThrowingTaskProvider : INotionTaskProvider
    {
        private readonly string _message;

        public ThrowingTaskProvider(string message)
            => _message = message;

        public Task<PagedResult<NotionTaskDto>> GetTasksAsync(NotionTaskQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(_message);

        public Task SetCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
