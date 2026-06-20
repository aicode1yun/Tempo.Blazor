namespace Tempo.Blazor.NotionEditor.Models;

public sealed record SmartLinkDto(
    string Url,
    string Title,
    string? FaviconUrl = null,
    string? Description = null,
    string? ImageUrl = null,
    string? ProviderName = null);
