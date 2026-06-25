using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoWorkItemStore
{
    private static readonly DateTime SeedUpdatedAt = new(2026, 6, 1, 10, 15, 0, DateTimeKind.Utc);

    private readonly IReadOnlyList<TmWorkItem> _items =
    [
        Make("demo", "DEMO-101", "https://tracker.demo.local/work/DEMO-101", "Prepare release checklist",
            "To Do", "#3b82f6", "Story", "S", "Ada Lovelace", "High", SeedUpdatedAt.AddDays(-3),
            new() { ["Sprint"] = "CF5", ["Team"] = "Editor" }),
        Make("demo", "DEMO-202", "https://tracker.demo.local/work/DEMO-202", "Wire provider registry into Notion editor",
            "In Progress", "#f59e0b", "Task", "T", "Grace Hopper", "Medium", SeedUpdatedAt.AddDays(-2),
            new() { ["Sprint"] = "CF5", ["Component"] = "Notion" }),
        Make("demo", "DEMO-303", "https://tracker.demo.local/work/DEMO-303", "Polish inline work item chip",
            "Done", "#22c55e", "Feature", "F", "Katherine Johnson", "Low", SeedUpdatedAt.AddDays(-1),
            new() { ["Sprint"] = "CF5", ["UX"] = "Baseline" }),
        Make("demo", "DEMO-404", "https://tracker.demo.local/work/DEMO-404", "Investigate provider outage fallback",
            "Blocked", "#ef4444", "Bug", "B", "Hedy Lamarr", "Critical", SeedUpdatedAt,
            new() { ["Sprint"] = "CF5", ["Risk"] = "Fallback" }),
        Make("ops", "OPS-7", "https://ops.demo.local/work/OPS-7", "Rotate demo API certificate",
            "Scheduled", "#06b6d4", "Change", "C", "Linus Pauling", "Normal", SeedUpdatedAt.AddHours(-6),
            new() { ["Window"] = "Night", ["Environment"] = "Demo" })
    ];

    public TmWorkItem? GetById(string sourceKey, string externalId)
        => _items.FirstOrDefault(item =>
            string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));

    public PagedResult<TmWorkItem> Search(TmWorkItemQuery query)
    {
        var sourceKey = (query.SourceKey ?? string.Empty).Trim();
        IEnumerable<TmWorkItem> filtered = string.IsNullOrWhiteSpace(sourceKey)
            ? _items
            : _items.Where(item => string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));

        if (query.Ids.Count > 0)
        {
            var ids = query.Ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (ids.Count > 0)
                filtered = filtered.Where(item => ids.Contains(item.ExternalId ?? item.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.FreeText))
        {
            var term = query.FreeText.Trim();
            filtered = filtered.Where(item =>
                (item.ExternalId ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.StatusLabel?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.TypeLabel?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || item.Fields.Values.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var skip = Math.Max(0, query.Skip);
        var take = query.Take <= 0 ? 20 : Math.Min(query.Take, 100);
        var matches = filtered
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PagedResult<TmWorkItem>
        {
            Items = matches.Skip(skip).Take(take).ToArray(),
            TotalCount = matches.Length,
            Page = skip / take + 1,
            PageSize = take
        };
    }

    private static TmWorkItem Make(
        string sourceKey, string externalId, string url, string title,
        string status, string statusColor, string typeLabel, string typeLetter,
        string assignee, string priority, DateTime updatedAt, Dictionary<string, string> fields)
        => new()
        {
            Id = externalId,
            SourceKey = sourceKey,
            ExternalId = externalId,
            Url = url,
            Title = title,
            StatusLabel = status,
            StatusColor = statusColor,
            TypeLabel = typeLabel,
            TypeIconUrl = IconDataUrl(statusColor, typeLetter),
            Assignees = [new TmWorkItemAssignee { Id = assignee, Name = assignee }],
            PriorityLabel = priority,
            UpdatedAt = updatedAt,
            Fields = fields
        };

    private static string IconDataUrl(string color, string letter)
    {
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><rect width=\"24\" height=\"24\" rx=\"5\" fill=\"{color}\"/><text x=\"12\" y=\"16\" text-anchor=\"middle\" font-family=\"Arial,sans-serif\" font-size=\"11\" font-weight=\"700\" fill=\"white\">{letter}</text></svg>";
        return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
    }
}
