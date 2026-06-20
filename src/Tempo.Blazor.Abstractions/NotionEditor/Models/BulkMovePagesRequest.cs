namespace Tempo.Blazor.NotionEditor.Models;

public sealed record BulkMovePagesRequest(IReadOnlyList<string> PageIds, string? NewParentId);
