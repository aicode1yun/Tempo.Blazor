using System.Net;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed partial class DemoNotionTaskProvider : INotionTaskProvider
{
    private readonly MockNotionDataStore _pageStore;
    private readonly MockNotionBlockStore _blockStore;

    public DemoNotionTaskProvider(MockNotionDataStore pageStore, MockNotionBlockStore blockStore)
    {
        _pageStore = pageStore;
        _blockStore = blockStore;
    }

    public Task<PagedResult<NotionTaskDto>> GetTasksAsync(NotionTaskQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pages = _pageStore.GetAllPages()
            .Where(page => !page.IsDeleted)
            .ToDictionary(page => page.Id.ToString(), page => page.Title, StringComparer.OrdinalIgnoreCase);

        var tasks = _blockStore.GetAllBlocksSnapshot()
            .Where(block => block.Type == BlockType.TodoItem)
            .Where(block => block.Content is ITodoBlockContent)
            .Where(block => pages.ContainsKey(block.PageId.ToString()))
            .Select(block => MapTask(block, (ITodoBlockContent)block.Content, pages))
            .Where(task => query.IncludeCompleted || !task.IsCompleted)
            .Where(task => string.IsNullOrWhiteSpace(query.AssigneeId) || string.Equals(task.AssigneeId, query.AssigneeId, StringComparison.OrdinalIgnoreCase))
            .Where(task => string.IsNullOrWhiteSpace(query.PageId) || string.Equals(task.PageId, query.PageId, StringComparison.OrdinalIgnoreCase))
            .Where(task => query.DueAfter is null || (task.DueDate is not null && task.DueDate.Value.Date >= query.DueAfter.Value.Date))
            .Where(task => query.DueBefore is null || (task.DueDate is not null && task.DueDate.Value.Date <= query.DueBefore.Value.Date))
            .OrderBy(task => task.IsCompleted)
            .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
            .ThenBy(task => task.PageTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        var skip = Math.Max(0, query.Skip);
        var take = query.Take <= 0 ? 50 : query.Take;
        var page = skip / take + 1;

        return Task.FromResult(new PagedResult<NotionTaskDto>
        {
            Items = tasks.Skip(skip).Take(take).ToList(),
            TotalCount = tasks.Count,
            Page = page,
            PageSize = take
        });
    }

    public Task SetCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default)
        => _blockStore.SetTodoCompletedAsync(taskId, completed, cancellationToken);

    private static NotionTaskDto MapTask(PageBlock block, ITodoBlockContent todo, IReadOnlyDictionary<string, string> pageTitles)
    {
        var pageId = block.PageId.ToString();
        return new NotionTaskDto
        {
            Id = block.Id.ToString(),
            PageId = pageId,
            PageTitle = pageTitles.TryGetValue(pageId, out var title) ? title : pageId,
            BlockId = block.Id.ToString(),
            Text = ToPlainText(todo.Html),
            AssigneeId = todo.AssigneeId,
            AssigneeDisplayName = todo.AssigneeDisplayName,
            DueDate = todo.DueDate,
            IsCompleted = todo.IsChecked,
            CreatedAt = block.CreatedAt
        };
    }

    private static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var withoutTags = HtmlTagRegex().Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
