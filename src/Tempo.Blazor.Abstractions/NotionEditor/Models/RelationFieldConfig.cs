namespace Tempo.Blazor.NotionEditor.Models;

public class RelationFieldConfig : IRelationFieldConfig
{
    public Guid TargetDatabaseId { get; set; }
    public bool IsBidirectional { get; set; }
    public Guid? InverseFieldId { get; set; }
}
