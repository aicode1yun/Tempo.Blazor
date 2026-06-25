namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Identifies the origin application of pasted clipboard content.</summary>
public enum DocumentClipboardSource
{
    /// <summary>Source could not be determined.</summary>
    Unknown,

    /// <summary>Content originated from Microsoft Word or another Office application.</summary>
    Word,

    /// <summary>Content originated from Google Docs.</summary>
    GoogleDocs,

    /// <summary>Content originated from Google Sheets or Microsoft Excel.</summary>
    GoogleSheets,

    /// <summary>Content originated from within the same Tempo editor instance.</summary>
    Internal,

    /// <summary>Plain text contains a single URL.</summary>
    Url,

    /// <summary>Plain text fallback.</summary>
    PlainText,

    /// <summary>Raw HTML fallback.</summary>
    RawHtml
}
