namespace Tempo.Blazor.NotionEditor.Models;

public sealed record BulkDeletePagesRequest(IReadOnlyList<string> PageIds);
