namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class CalloutBlockContent : ICalloutBlockContent
{
    public string? IconEmoji { get; set; }
    public string? IconImageUrl { get; set; }
    public string Html { get; set; } = string.Empty;
    public IReadOnlyList<Mention> Mentions { get; set; } = new List<Mention>();
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
}
