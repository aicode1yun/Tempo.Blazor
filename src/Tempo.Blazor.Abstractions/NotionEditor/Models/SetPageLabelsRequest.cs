namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request body for replacing all labels assigned to a Notion page.</summary>
public sealed record SetPageLabelsRequest(IReadOnlyList<string> Labels);
