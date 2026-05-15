namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Payload for mark toggle commands from the JS engine.</summary>
public sealed class WysiwygMarkPayload
{
    /// <summary>Mark type: Bold, Italic, Underline, etc.</summary>
    public string MarkType { get; set; } = string.Empty;

    /// <summary>Optional mark value, for example a link URL.</summary>
    public string? Data { get; set; }

    /// <summary>Optional structured mark value, for example a CSS font family or font size.</summary>
    public string? Value { get; set; }

    /// <summary>Optional link URL for link marks.</summary>
    public string? Href { get; set; }

    /// <summary>Optional link title for link marks.</summary>
    public string? Title { get; set; }

    /// <summary>Selection snapshot at the time of the command.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }
}
