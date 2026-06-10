namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public sealed class NotionSearchResponse
{
    public IReadOnlyList<NotionPage> Pages { get; set; } = [];
    public IReadOnlyList<NotionSearchResult> Blocks { get; set; } = [];
}
