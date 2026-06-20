namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Payload for block insert commands from the JS engine.</summary>
public sealed class WysiwygBlockPayload
{
    /// <summary>Block type: Paragraph, Heading, List, Table, Image, PageBreak.</summary>
    public string BlockType { get; set; } = string.Empty;

    /// <summary>Selection snapshot at the time of the command.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }

    /// <summary>Optional heading level when BlockType is Heading.</summary>
    public int? HeadingLevel { get; set; }
}
