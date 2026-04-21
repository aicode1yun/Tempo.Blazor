namespace Tempo.Blazor.NotionEditor.Models;

public interface IBoardViewConfig : IDatabaseViewConfig
{
    Guid GroupByFieldId { get; }
    IReadOnlyList<string> HiddenGroupIds { get; }
    Guid? CardCoverFieldId { get; }
    IReadOnlyList<Guid> CardPreviewFieldIds { get; }
}
