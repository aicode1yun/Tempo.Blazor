namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class RollupFieldConfig : IRollupFieldConfig
{
    public Guid RelationFieldId { get; set; }
    public Guid TargetFieldId { get; set; }
    public RollupAggregation Aggregation { get; set; }
}
