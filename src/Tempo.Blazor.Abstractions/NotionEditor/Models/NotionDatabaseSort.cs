namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public record NotionDatabaseSort(Guid FieldId, SortDirection Direction);
