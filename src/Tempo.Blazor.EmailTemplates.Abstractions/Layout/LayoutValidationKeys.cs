namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>Localization keys for layout validation messages (never raw text).</summary>
public static class LayoutValidationKeys
{
    /// <summary>Explicit column widths in a section do not sum to 100%.</summary>
    public const string ColumnWidths = "layout.column_widths";

    /// <summary>A section has no columns.</summary>
    public const string EmptySection = "layout.empty_section";

    /// <summary>Container nesting exceeds the supported depth.</summary>
    public const string MaxNesting = "layout.max_nesting";
}
