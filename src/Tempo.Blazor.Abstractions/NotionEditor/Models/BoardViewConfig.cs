namespace Tempo.Blazor.NotionEditor.Models;

public class BoardViewConfig : IBoardViewConfig
{
    public Guid GroupByFieldId { get; set; }
    public IReadOnlyList<string> HiddenGroupIds { get; set; } = new List<string>();
    public Guid? CardCoverFieldId { get; set; }
    public IReadOnlyList<Guid> CardPreviewFieldIds { get; set; } = new List<Guid>();
}
