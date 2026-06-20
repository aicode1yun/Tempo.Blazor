namespace Tempo.Blazor.NotionEditor.Models;

public sealed class PagePropertiesReportRow
{
    private IReadOnlyList<string> _labels = [];
    private IReadOnlyDictionary<string, string?> _properties = new Dictionary<string, string?>();

    public Guid PageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? IconEmoji { get; set; }

    public IReadOnlyList<string> Labels
    {
        get => _labels;
        set => _labels = value ?? [];
    }

    public IReadOnlyDictionary<string, string?> Properties
    {
        get => _properties;
        set => _properties = value ?? new Dictionary<string, string?>();
    }
}
