using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class NotionTaskProviderContractTests
{
    [Fact]
    public void NotionTaskDto_RoundtripsThroughJson()
    {
        var dto = new NotionTaskDto
        {
            Id = "task-1",
            PageId = "page-1",
            PageTitle = "Project Plan",
            BlockId = "block-1",
            Text = "Ship task view",
            AssigneeId = "alice",
            AssigneeDisplayName = "Alice Johnson",
            DueDate = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
            IsCompleted = false,
            CreatedAt = new DateTime(2026, 6, 1, 9, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(dto);
        var roundtrip = JsonSerializer.Deserialize<NotionTaskDto>(json);

        roundtrip.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task INotionTaskProvider_CanBeImplementedWithoutBlazorDependencies()
    {
        INotionTaskProvider provider = new InMemoryTaskProvider([
            new NotionTaskDto
            {
                Id = "task-1",
                PageId = "page-1",
                PageTitle = "Project Plan",
                BlockId = "block-1",
                Text = "Open task",
                AssigneeId = "alice",
                DueDate = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            }
        ]);

        var result = await provider.GetTasksAsync(new NotionTaskQuery
        {
            AssigneeId = "alice",
            IncludeCompleted = false,
            Skip = 0,
            Take = 10
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().Text.Should().Be("Open task");

        await provider.SetCompletedAsync("task-1", true);
        var completed = await provider.GetTasksAsync(new NotionTaskQuery { IncludeCompleted = true });
        completed.Items.Single().IsCompleted.Should().BeTrue();
    }

    private sealed class InMemoryTaskProvider : INotionTaskProvider
    {
        private readonly Dictionary<string, NotionTaskDto> _tasks;

        public InMemoryTaskProvider(IEnumerable<NotionTaskDto> tasks)
            => _tasks = tasks.ToDictionary(task => task.Id, StringComparer.Ordinal);

        public Task<PagedResult<NotionTaskDto>> GetTasksAsync(NotionTaskQuery query, CancellationToken cancellationToken = default)
        {
            var tasks = _tasks.Values
                .Where(task => query.IncludeCompleted || !task.IsCompleted)
                .Where(task => string.IsNullOrWhiteSpace(query.AssigneeId) || task.AssigneeId == query.AssigneeId)
                .Skip(Math.Max(0, query.Skip))
                .Take(query.Take <= 0 ? 50 : query.Take)
                .ToList();

            return Task.FromResult(new PagedResult<NotionTaskDto>
            {
                Items = tasks,
                TotalCount = tasks.Count,
                Page = 1,
                PageSize = query.Take <= 0 ? 50 : query.Take
            });
        }

        public Task SetCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default)
        {
            _tasks[taskId].IsCompleted = completed;
            return Task.CompletedTask;
        }
    }
}
