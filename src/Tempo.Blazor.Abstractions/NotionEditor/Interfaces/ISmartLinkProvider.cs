namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface ISmartLinkProvider
{
    Task<SmartLinkDto?> ResolveAsync(string url, CancellationToken cancellationToken = default);
}
