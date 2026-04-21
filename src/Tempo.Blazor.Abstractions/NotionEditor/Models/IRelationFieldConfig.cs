namespace Tempo.Blazor.NotionEditor.Models;

public interface IRelationFieldConfig : IFieldConfig
{
    Guid TargetDatabaseId { get; }
    bool IsBidirectional { get; }
    Guid? InverseFieldId { get; }
}
