using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoWorkItemStore
{
    private static readonly DateTimeOffset SeedUpdatedAt = new(2026, 6, 1, 10, 15, 0, TimeSpan.Zero);

    private readonly IReadOnlyList<WorkItemDto> _items =
    [
        new()
        {
            ProviderKey = "demo",
            ExternalId = "DEMO-101",
            Url = "https://tracker.demo.local/work/DEMO-101",
            Title = "Prepare release checklist",
            Status = "To Do",
            StatusColor = "#3b82f6",
            TypeLabel = "Story",
            TypeIconUrl = IconDataUrl("#3b82f6", "S"),
            AssigneeDisplayName = "Ada Lovelace",
            Priority = "High",
            UpdatedAt = SeedUpdatedAt.AddDays(-3),
            Fields = new Dictionary<string, string>
            {
                ["Sprint"] = "CF5",
                ["Team"] = "Editor"
            }
        },
        new()
        {
            ProviderKey = "demo",
            ExternalId = "DEMO-202",
            Url = "https://tracker.demo.local/work/DEMO-202",
            Title = "Wire provider registry into Notion editor",
            Status = "In Progress",
            StatusColor = "#f59e0b",
            TypeLabel = "Task",
            TypeIconUrl = IconDataUrl("#f59e0b", "T"),
            AssigneeDisplayName = "Grace Hopper",
            Priority = "Medium",
            UpdatedAt = SeedUpdatedAt.AddDays(-2),
            Fields = new Dictionary<string, string>
            {
                ["Sprint"] = "CF5",
                ["Component"] = "Notion"
            }
        },
        new()
        {
            ProviderKey = "demo",
            ExternalId = "DEMO-303",
            Url = "https://tracker.demo.local/work/DEMO-303",
            Title = "Polish inline work item chip",
            Status = "Done",
            StatusColor = "#22c55e",
            TypeLabel = "Feature",
            TypeIconUrl = IconDataUrl("#22c55e", "F"),
            AssigneeDisplayName = "Katherine Johnson",
            Priority = "Low",
            UpdatedAt = SeedUpdatedAt.AddDays(-1),
            Fields = new Dictionary<string, string>
            {
                ["Sprint"] = "CF5",
                ["UX"] = "Baseline"
            }
        },
        new()
        {
            ProviderKey = "demo",
            ExternalId = "DEMO-404",
            Url = "https://tracker.demo.local/work/DEMO-404",
            Title = "Investigate provider outage fallback",
            Status = "Blocked",
            StatusColor = "#ef4444",
            TypeLabel = "Bug",
            TypeIconUrl = IconDataUrl("#ef4444", "B"),
            AssigneeDisplayName = "Hedy Lamarr",
            Priority = "Critical",
            UpdatedAt = SeedUpdatedAt,
            Fields = new Dictionary<string, string>
            {
                ["Sprint"] = "CF5",
                ["Risk"] = "Fallback"
            }
        },
        new()
        {
            ProviderKey = "ops",
            ExternalId = "OPS-7",
            Url = "https://ops.demo.local/work/OPS-7",
            Title = "Rotate demo API certificate",
            Status = "Scheduled",
            StatusColor = "#06b6d4",
            TypeLabel = "Change",
            TypeIconUrl = IconDataUrl("#06b6d4", "C"),
            AssigneeDisplayName = "Linus Pauling",
            Priority = "Normal",
            UpdatedAt = SeedUpdatedAt.AddHours(-6),
            Fields = new Dictionary<string, string>
            {
                ["Window"] = "Night",
                ["Environment"] = "Demo"
            }
        }
    ];

    public WorkItemDto? GetById(string providerKey, string externalId)
        => _items.FirstOrDefault(item =>
            string.Equals(item.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));

    public PagedResult<WorkItemDto> Search(WorkItemQuery query)
    {
        var providerKey = query.ProviderKey.Trim();
        IEnumerable<WorkItemDto> filtered = _items.Where(item =>
            string.Equals(item.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));

        if (query.Ids.Count > 0)
        {
            var ids = query.Ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (ids.Count > 0)
                filtered = filtered.Where(item => ids.Contains(item.ExternalId));
        }

        if (!string.IsNullOrWhiteSpace(query.FreeText))
        {
            var term = query.FreeText.Trim();
            filtered = filtered.Where(item =>
                item.ExternalId.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Status.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.TypeLabel?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || item.Fields.Values.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var skip = Math.Max(0, query.Skip);
        var take = query.Take <= 0 ? 20 : Math.Min(query.Take, 100);
        var matches = filtered
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PagedResult<WorkItemDto>
        {
            Items = matches.Skip(skip).Take(take).ToArray(),
            TotalCount = matches.Length,
            Page = skip / take + 1,
            PageSize = take
        };
    }

    private static string IconDataUrl(string color, string letter)
    {
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><rect width=\"24\" height=\"24\" rx=\"5\" fill=\"{color}\"/><text x=\"12\" y=\"16\" text-anchor=\"middle\" font-family=\"Arial,sans-serif\" font-size=\"11\" font-weight=\"700\" fill=\"white\">{letter}</text></svg>";
        return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
    }
}
