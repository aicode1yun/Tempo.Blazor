namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public record NotionDatabaseGrouping(Guid FieldId, bool HideEmptyGroups, SortDirection SortDirection);
