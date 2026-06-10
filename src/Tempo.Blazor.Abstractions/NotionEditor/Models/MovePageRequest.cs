namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>
/// Request payload for moving a Notion page under another page or to the root.
/// </summary>
/// <param name="NewParentId">The target parent page identifier, or <see langword="null" /> for a root page.</param>
public sealed record MovePageRequest(string? NewParentId);
