namespace Tempo.Blazor.Components.Activity;

/// <summary>
/// Internal DTO deserialized from the JS hover event object.
/// Property names match the camelCase keys sent by <c>tmRichEditor.initTokenHoverPreview</c>.
/// </summary>
internal sealed class TokenHoverJsArgs
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? ColorClass { get; set; }
    public string? TypeLabel { get; set; }
    public double RectLeft { get; set; }
    public double RectTop { get; set; }
    public double RectRight { get; set; }
    public double RectBottom { get; set; }
    public double RectWidth { get; set; }
    public double RectHeight { get; set; }
}
