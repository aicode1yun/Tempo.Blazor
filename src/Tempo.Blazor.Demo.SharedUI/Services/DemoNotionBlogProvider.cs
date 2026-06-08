using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionBlogProvider : INotionBlogProvider
{
    private readonly HttpClient _http;

    public DemoNotionBlogProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<PagedResult<NotionBlogPostDto>> GetPostsAsync(string spaceId, NotionBlogQuery paging, CancellationToken cancellationToken = default)
    {
        paging ??= new NotionBlogQuery();
        var url = $"/api/notion/blog/spaces/{Uri.EscapeDataString(spaceId)}/posts" +
                  $"?skip={Math.Max(0, paging.Skip)}" +
                  $"&take={Math.Max(1, paging.Take)}" +
                  $"&includeDrafts={paging.IncludeDrafts.ToString().ToLowerInvariant()}";

        return await _http.GetFromJsonAsync<PagedResult<NotionBlogPostDto>>(url, cancellationToken)
               ?? new PagedResult<NotionBlogPostDto>();
    }

    public async Task<NotionBlogPostDto?> GetPostAsync(string postId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/api/notion/blog/posts/{Uri.EscapeDataString(postId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionBlogPostDto>(cancellationToken);
    }

    public async Task<NotionBlogPostDto> CreatePostAsync(CreateNotionBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/blog/posts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionBlogPostDto>(cancellationToken)
               ?? throw new InvalidOperationException("The demo API did not return the created blog post.");
    }

    public async Task<NotionBlogPostDto> PublishAsync(string postId, PublishNotionBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/notion/blog/posts/{Uri.EscapeDataString(postId)}/publish",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionBlogPostDto>(cancellationToken)
               ?? throw new InvalidOperationException("The demo API did not return the published blog post.");
    }
}
