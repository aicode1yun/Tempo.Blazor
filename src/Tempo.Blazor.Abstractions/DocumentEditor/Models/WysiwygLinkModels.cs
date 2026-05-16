namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Payload used when applying or editing a WYSIWYG hyperlink.</summary>
public sealed class WysiwygLinkPayload
{
    /// <summary>Target URL.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Optional tooltip/title.</summary>
    public string? Title { get; set; }
}

/// <summary>Current hyperlink metadata returned by the WYSIWYG selection bridge.</summary>
public sealed class WysiwygLinkInfo
{
    /// <summary>Target URL.</summary>
    public string? Href { get; set; }

    /// <summary>Optional tooltip/title.</summary>
    public string? Title { get; set; }
}
