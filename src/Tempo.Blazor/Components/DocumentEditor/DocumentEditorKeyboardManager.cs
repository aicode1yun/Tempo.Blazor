using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Maps Word-like document editor keyboard shortcuts to editor commands.</summary>
public sealed class DocumentEditorKeyboardManager
{
    /// <summary>Returns the editor command represented by the keyboard event.</summary>
    public DocumentEditorKeyboardCommand GetCommand(KeyboardEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var key = args.Key?.ToLowerInvariant();
        if (key == "escape")
        {
            return DocumentEditorKeyboardCommand.ClosePanel;
        }

        if (!args.CtrlKey && !args.MetaKey)
        {
            return DocumentEditorKeyboardCommand.None;
        }

        return key switch
        {
            "s" => DocumentEditorKeyboardCommand.Save,
            "z" when args.ShiftKey => DocumentEditorKeyboardCommand.Redo,
            "z" => DocumentEditorKeyboardCommand.Undo,
            "y" => DocumentEditorKeyboardCommand.Redo,
            "b" => DocumentEditorKeyboardCommand.Bold,
            "i" => DocumentEditorKeyboardCommand.Italic,
            "k" => DocumentEditorKeyboardCommand.Link,
            _ => DocumentEditorKeyboardCommand.None
        };
    }
}

/// <summary>Keyboard commands supported by <see cref="TmDocumentEditor"/>.</summary>
public enum DocumentEditorKeyboardCommand
{
    /// <summary>No editor command.</summary>
    None,

    /// <summary>Save the document.</summary>
    Save,

    /// <summary>Undo the latest command.</summary>
    Undo,

    /// <summary>Redo the latest undone command.</summary>
    Redo,

    /// <summary>Toggle bold formatting.</summary>
    Bold,

    /// <summary>Toggle italic formatting.</summary>
    Italic,

    /// <summary>Open the link dialog.</summary>
    Link,

    /// <summary>Close an open panel or dialog.</summary>
    ClosePanel
}
