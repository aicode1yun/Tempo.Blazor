namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request for moving an existing Notion block to a top-level page position.</summary>
public sealed record MoveBlockToPageRequest(string TargetPageId, string? AfterBlockId = null);
