namespace Tempo.Blazor.NotionEditor.Models;

public class CodeBlockContent : ICodeBlockContent
{
    public string Code { get; set; } = string.Empty;
    public string? Language { get; set; }
    public bool ShowLineNumbers { get; set; }
    public string? Caption { get; set; }
    public bool WrapLines { get; set; }
}
