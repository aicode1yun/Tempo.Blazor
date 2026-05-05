namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Demo metadata model for <see cref="Tempo.Blazor.Components.Files.TmDocumentManager{TMetadata}"/>.
/// </summary>
public class DocumentMetadata
{
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Owner { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public DateTime? ReviewDate { get; set; }
}
