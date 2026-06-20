namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

/// <summary>Represents an inline Confluence-style status macro stored inside rich text HTML.</summary>
public sealed class InlineStatus
{
    /// <summary>Text displayed inside the status chip.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Visual color of the status chip.</summary>
    public NotionStatusColor Color { get; init; } = NotionStatusColor.Gray;

    public InlineStatus()
    {
    }

    public InlineStatus(string label, NotionStatusColor color)
    {
        Label = label;
        Color = color;
    }
}
