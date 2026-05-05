using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Mention provider for the Notion editor. Users are in-memory; page search
/// calls the Demo API to stay in sync with the live page list.
/// </summary>
public class MockNotionMentionProvider : INotionMentionProvider
{
    private static readonly List<NotionMentionUser> _users = new()
    {
        new("alice",   "Alice Johnson",  "https://i.pravatar.cc/150?u=alice",  "alice@demo.com"),
        new("bob",     "Bob Smith",      "https://i.pravatar.cc/150?u=bob",    "bob@demo.com"),
        new("charlie", "Charlie Brown",  null,                                  "charlie@demo.com"),
        new("diana",   "Diana Prince",   "https://i.pravatar.cc/150?u=diana",  "diana@demo.com"),
        new("demo",    "Demo User",      null,                                  "demo@demo.com"),
    };

    private readonly HttpClient _http;

    public MockNotionMentionProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public Task<IEnumerable<IMentionUser>> SearchUsersAsync(string query)
    {
        IEnumerable<IMentionUser> results = string.IsNullOrWhiteSpace(query)
            ? _users
            : _users.Where(u =>
                u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (u.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        return Task.FromResult(results);
    }

    public async Task<IEnumerable<INotionPage>> SearchPagesAsync(string query)
    {
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>("/api/notion/pages") ?? [];
        return pages
            .Where(p => !p.IsDeleted)
            .Where(p => string.IsNullOrWhiteSpace(query) ||
                        p.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Cast<INotionPage>();
    }
}

public sealed record NotionMentionUser(
    string UserId,
    string DisplayName,
    string? AvatarUrl,
    string? Email) : IMentionUser;
