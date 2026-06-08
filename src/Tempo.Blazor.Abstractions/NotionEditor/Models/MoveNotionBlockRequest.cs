namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request for moving an existing Notion block within a page block tree.</summary>
public sealed record MoveNotionBlockRequest(
    string BlockId,
    string TargetPageId,
    string? SourceParentBlockId,
    string? TargetParentBlockId,
    int TargetIndex);
