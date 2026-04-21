namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public record NotionFilterCondition(Guid FieldId, NotionFilterOperator Operator, object? Value);
