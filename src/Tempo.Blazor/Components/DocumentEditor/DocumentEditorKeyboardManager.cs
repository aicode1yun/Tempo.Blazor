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
        if (key == "f10" || (key == "alt" && args.AltKey && !args.CtrlKey && !args.MetaKey && !args.ShiftKey))
        {
            return DocumentEditorKeyboardCommand.ActivateRibbon;
        }

        if (key == "escape")
        {
            return DocumentEditorKeyboardCommand.ClosePanel;
        }

        if (!args.CtrlKey && !args.MetaKey)
        {
            return DocumentEditorKeyboardCommand.None;
        }

        if (args.AltKey && key == "v")
        {
            return DocumentEditorKeyboardCommand.OpenVersions;
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
            "f" => DocumentEditorKeyboardCommand.OpenFind,
            "h" => DocumentEditorKeyboardCommand.OpenReplace,
            _ => DocumentEditorKeyboardCommand.None
        };
    }

    /// <summary>Returns the registry command name for keyboard shortcuts that are routed through the command registry,
    /// or <c>null</c> for shortcuts that bypass the registry (e.g. ActivateRibbon, ClosePanel).</summary>
    public string? GetRegistryCommandName(KeyboardEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!args.CtrlKey && !args.MetaKey)
        {
            return null;
        }

        if (args.AltKey)
        {
            return null;
        }

        var key = args.Key?.ToLowerInvariant();
        if (args.ShiftKey && key == "p")
        {
            return "commandPalette";
        }

        return key switch
        {
            "s" => "save",
            "z" when args.ShiftKey => "redo",
            "z" => "undo",
            "y" => "redo",
            "b" => "bold",
            "i" => "italic",
            "u" => "underline",
            "k" => "link",
            "f" => "find",
            "h" => "replace",
            _ => null
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

    /// <summary>Open the document versions side panel.</summary>
    OpenVersions,

    /// <summary>Move focus to the active ribbon tab.</summary>
    ActivateRibbon,

    /// <summary>Close an open panel or dialog.</summary>
    ClosePanel,

    /// <summary>Open the Find panel (Ctrl+F).</summary>
    OpenFind,

    /// <summary>Open the Find &amp; Replace panel (Ctrl+H).</summary>
    OpenReplace
}
