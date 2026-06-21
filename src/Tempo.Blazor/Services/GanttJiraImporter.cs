using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Services;

/// <summary>Imports Gantt tasks from a JIRA REST API (v3) project.</summary>
public sealed class GanttJiraImporter : IDisposable
{
    private readonly HttpClient _http;

    public GanttJiraImporter(HttpClient httpClient) => _http = httpClient;

    public async Task<IReadOnlyList<TmWorkItem>> ImportAsync(
        string baseUrl, string token, string projectKey,
        CancellationToken cancellationToken = default)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/search?jql=project%3D{Uri.EscapeDataString(projectKey)}";
        var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new GanttImportAuthException();

        response.EnsureSuccessStatusCode();

        var json   = await response.Content.ReadAsStringAsync(cancellationToken);
        var root   = JsonDocument.Parse(json).RootElement;
        var issues = root.GetProperty("issues");

        var result = new List<TmWorkItem>();
        foreach (var issue in issues.EnumerateArray())
        {
            var fields  = issue.GetProperty("fields");
            var summary = fields.GetProperty("summary").GetString() ?? "";
            var task    = new TmWorkItem { Title = summary };

            if (fields.TryGetProperty("duedate", out var dd) && dd.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParseExact(dd.GetString(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var due))
                    task.End = due;
            }

            if (task.Start == default)
                task.Start = task.End == default ? DateTime.Today : task.End;

            if (fields.TryGetProperty("priority", out var pri) &&
                pri.TryGetProperty("name", out var priName))
                task.Priority = MapPriority(priName.GetString());

            if (fields.TryGetProperty("status", out var status) &&
                status.TryGetProperty("name", out var statusName))
                task.Status = MapStatus(statusName.GetString());

            if (fields.TryGetProperty("assignee", out var assignee) &&
                assignee.ValueKind == JsonValueKind.Object)
            {
                var name = assignee.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                var id   = assignee.TryGetProperty("accountId",   out var ai) ? ai.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(id))
                    task.Assignees.Add(new TmWorkItemAssignee { Id = id, Name = name });
            }

            result.Add(task);
        }
        return result;
    }

    private static TmWorkItemPriority MapPriority(string? name) => name?.ToLowerInvariant() switch
    {
        "highest" => TmWorkItemPriority.Highest,
        "high"    => TmWorkItemPriority.High,
        "medium"  => TmWorkItemPriority.Medium,
        "low"     => TmWorkItemPriority.Low,
        "lowest"  => TmWorkItemPriority.Lowest,
        _         => TmWorkItemPriority.Medium
    };

    private static TmWorkItemStatus MapStatus(string? name) => name?.ToLowerInvariant() switch
    {
        "done"        => TmWorkItemStatus.Done,
        "in progress" => TmWorkItemStatus.InProgress,
        "closed"      => TmWorkItemStatus.Closed,
        _             => TmWorkItemStatus.Open
    };

    public void Dispose() => _http.Dispose();
}
