namespace Tempo.Blazor.NotionEditor.Models;

public record StatusGroup(string Name, string Color, IReadOnlyList<SelectFieldOption> Options);
