using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP client for the demo's atomic Notion page aggregate API.</summary>
public sealed class DemoNotionAggregateProvider : INotionAggregateProvider
{
    private readonly HttpClient _http;

    public DemoNotionAggregateProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<NotionAggregateLoadResult> LoadPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default)
        => await ReadLoadAsync(
            $"/api/notion/aggregate/pages/{pageId:D}",
            cancellationToken);

    public async Task<NotionAggregateLoadResult> LoadBlockAsync(
        Guid blockId,
        CancellationToken cancellationToken = default)
        => await ReadLoadAsync(
            $"/api/notion/aggregate/blocks/{blockId:D}",
            cancellationToken);

    public async Task<NotionAggregateSaveResult> SaveAsync(
        NotionAggregateSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            "/api/notion/aggregate/save",
            request,
            NotionAggregateJson.Options,
            cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<NotionAggregateSaveResult>(
            NotionAggregateJson.Options,
            cancellationToken);
        return result ??
            throw new InvalidDataException("The demo aggregate API returned an empty save result.");
    }

    private async Task<NotionAggregateLoadResult> ReadLoadAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync(uri, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<NotionAggregateLoadResult>(
            NotionAggregateJson.Options,
            cancellationToken);
        return result ??
            throw new InvalidDataException("The demo aggregate API returned an empty load result.");
    }
}
