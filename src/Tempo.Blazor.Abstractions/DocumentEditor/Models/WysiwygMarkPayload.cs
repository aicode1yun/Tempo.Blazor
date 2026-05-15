namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Payload for mark toggle commands from the JS engine.</summary>
public sealed class WysiwygMarkPayload
{
    /// <summary>Mark type: Bold, Italic, Underline, etc.</summary>
    public string MarkType { get; set; } = string.Empty;

    /// <summary>Selection snapshot at the time of the command.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }
}
