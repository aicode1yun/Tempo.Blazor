namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Tri-state formatting value for the current WYSIWYG selection.</summary>
public enum WysiwygFormattingValue
{
    /// <summary>The formatting is inactive for the selection.</summary>
    Inactive,

    /// <summary>The formatting is active for the whole selection or caret context.</summary>
    Active,

    /// <summary>The selection contains both formatted and unformatted text.</summary>
    Mixed
}

/// <summary>Formatting state near the current WYSIWYG selection.</summary>
public sealed class WysiwygFormattingState
{
    /// <summary>Monotonic runtime formatting state version.</summary>
    public long Version { get; set; }

    /// <summary>Whether formatting commands are disabled for the current selection.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Reason why formatting commands are disabled, if known.</summary>
    public string? DisabledReason { get; set; }

    /// <summary>Bold formatting state.</summary>
    public WysiwygFormattingValue Bold { get; set; }

    /// <summary>Italic formatting state.</summary>
    public WysiwygFormattingValue Italic { get; set; }

    /// <summary>Underline formatting state.</summary>
    public WysiwygFormattingValue Underline { get; set; }

    /// <summary>Strikethrough formatting state.</summary>
    public WysiwygFormattingValue Strikethrough { get; set; }

    /// <summary>Paragraph alignment for the current selection.</summary>
    public DocumentTextAlignment ParagraphAlignment { get; set; } = DocumentTextAlignment.Left;

    /// <summary>Whether the current selection spans multiple paragraph alignments.</summary>
    public bool ParagraphAlignmentMixed { get; set; }

    /// <summary>Font family at the current selection.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Whether the selected range spans multiple font families.</summary>
    public bool FontFamilyMixed { get; set; }

    /// <summary>Font size at the current selection, for example 12pt.</summary>
    public string? FontSize { get; set; }

    /// <summary>Whether the selected range spans multiple font sizes.</summary>
    public bool FontSizeMixed { get; set; }

    /// <summary>Text color at the current selection.</summary>
    public string? TextColor { get; set; }

    /// <summary>Whether the selected range spans multiple text colors.</summary>
    public bool TextColorMixed { get; set; }

    /// <summary>Highlight color at the current selection.</summary>
    public string? HighlightColor { get; set; }

    /// <summary>Whether the selected range spans multiple highlight colors.</summary>
    public bool HighlightColorMixed { get; set; }

    /// <summary>Line spacing at the current selection.</summary>
    public double LineSpacing { get; set; } = 1;

    /// <summary>Whether selected paragraphs have mixed line spacing.</summary>
    public bool LineSpacingMixed { get; set; }

    /// <summary>Spacing before the current paragraph in points.</summary>
    public double SpacingBefore { get; set; }

    /// <summary>Whether selected paragraphs have mixed spacing before values.</summary>
    public bool SpacingBeforeMixed { get; set; }

    /// <summary>Spacing after the current paragraph in points.</summary>
    public double SpacingAfter { get; set; }

    /// <summary>Whether selected paragraphs have mixed spacing after values.</summary>
    public bool SpacingAfterMixed { get; set; }

    /// <summary>Left indent of the current paragraph in points.</summary>
    public double LeftIndent { get; set; }

    /// <summary>Whether selected paragraphs have mixed left indent values.</summary>
    public bool LeftIndentMixed { get; set; }

    /// <summary>Whether the current paragraph is an unordered list item.</summary>
    public bool IsBulletList { get; set; }

    /// <summary>Whether the current paragraph is an ordered list item.</summary>
    public bool IsNumberedList { get; set; }

    /// <summary>Whether selected paragraphs have mixed list states.</summary>
    public bool ListMixed { get; set; }

    /// <summary>Logical region where the selection currently lives.</summary>
    public string ActiveRegion { get; set; } = "Body";

    /// <summary>Selection snapshot used to compute this formatting state.</summary>
    public WysiwygSelectionSnapshot? CurrentSelection { get; set; }
}

/// <summary>Toolbar selection formatting state produced by the JS-owned runtime.</summary>
public sealed class DocumentEditorSelectionFormattingState
{
    /// <summary>Runtime formatting state for the current selection.</summary>
    public WysiwygFormattingState Formatting { get; set; } = new();

    /// <summary>Runtime selection used to compute the formatting state.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }
}
