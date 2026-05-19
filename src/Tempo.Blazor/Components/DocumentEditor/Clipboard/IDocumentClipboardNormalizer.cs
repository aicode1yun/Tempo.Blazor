namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Single stage in the clipboard normalization pipeline.</summary>
public interface IDocumentClipboardNormalizer
{
    /// <summary>Priority used when multiple normalizers can process the same input. Higher runs first.</summary>
    int Priority => 0;

    /// <summary>Returns true when this normalizer can process the given clipboard input.</summary>
    bool CanHandle(DocumentClipboardInput input);

    /// <summary>Transforms the clipboard input into normalized document blocks.</summary>
    DocumentClipboardOutput Normalize(DocumentClipboardInput input);
}
