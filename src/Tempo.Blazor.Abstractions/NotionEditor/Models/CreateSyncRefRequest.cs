namespace Tempo.Blazor.NotionEditor.Models;

public sealed record CreateSyncRefRequest(string TargetPageId, string? AfterBlockId);
