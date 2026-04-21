namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IRollupFieldConfig : IFieldConfig
{
    Guid RelationFieldId { get; }
    Guid TargetFieldId { get; }
    RollupAggregation Aggregation { get; }
}
