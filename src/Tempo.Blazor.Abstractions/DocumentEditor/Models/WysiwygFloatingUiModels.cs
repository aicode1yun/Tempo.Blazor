namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Viewport-relative floating UI position computed by the WYSIWYG JS engine.</summary>
public class WysiwygFloatingUiPosition
{
    /// <summary>Left coordinate in viewport pixels.</summary>
    public double Left { get; set; }

    /// <summary>Top coordinate in viewport pixels.</summary>
    public double Top { get; set; }

    /// <summary>Floating element width used during placement.</summary>
    public double Width { get; set; }

    /// <summary>Floating element height used during placement.</summary>
    public double Height { get; set; }

    /// <summary>Current viewport width in pixels.</summary>
    public double ViewportWidth { get; set; }

    /// <summary>Current viewport height in pixels.</summary>
    public double ViewportHeight { get; set; }
}

/// <summary>Request to show the text context menu for the current editor selection.</summary>
public sealed class WysiwygTextContextMenuRequest : WysiwygFloatingUiPosition
{
    /// <summary>Original pointer X coordinate in viewport pixels.</summary>
    public double ClientX { get; set; }

    /// <summary>Original pointer Y coordinate in viewport pixels.</summary>
    public double ClientY { get; set; }

    /// <summary>Selection snapshot to restore before a context menu command runs.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }

    /// <summary>Optional block id when the menu targets a structural block such as a page break.</summary>
    public string? BlockId { get; set; }

    /// <summary>Optional block type when the menu targets a structural block.</summary>
    public string? BlockType { get; set; }

    /// <summary>Optional spelling diagnostic under the pointer.</summary>
    public WysiwygMisspelling? Misspelling { get; set; }
}

/// <summary>Misspelled word and replacement suggestions shown in a document editor text context menu.</summary>
public sealed class WysiwygMisspelling
{
    /// <summary>Misspelled word.</summary>
    public string? Word { get; set; }

    /// <summary>Start offset inside the target block.</summary>
    public int Start { get; set; }

    /// <summary>End offset inside the target block.</summary>
    public int End { get; set; }

    /// <summary>Target block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Suggested replacements.</summary>
    public List<string> Suggestions { get; set; } = [];

    /// <summary>Whether the editor may apply a replacement.</summary>
    public bool CanApplyFix { get; set; } = true;
}

/// <summary>Request to show the table context menu for the current table cell.</summary>
public sealed class WysiwygTableContextMenuRequest : WysiwygFloatingUiPosition
{
    /// <summary>Original pointer X coordinate in viewport pixels.</summary>
    public double ClientX { get; set; }

    /// <summary>Original pointer Y coordinate in viewport pixels.</summary>
    public double ClientY { get; set; }

    /// <summary>Runtime id of the table cell that opened the menu.</summary>
    public string? CellId { get; set; }

    /// <summary>Selection snapshot to restore before a table command runs.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }
}

/// <summary>Request to show or hide the inline mini toolbar for the current editor selection.</summary>
public sealed class WysiwygMiniToolbarRequest : WysiwygFloatingUiPosition
{
    /// <summary>Whether the mini toolbar should be visible.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Selection snapshot to restore before a mini toolbar command runs.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }

    /// <summary>Optional runtime reason for a visibility change.</summary>
    public string? Reason { get; set; }
}
