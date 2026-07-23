namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Resource limits enforced by canonical Notion authoring and rendering.</summary>
public static class NotionAuthoringLimits
{
    /// <summary>Maximum number of logical rows in one table.</summary>
    public const int MaxTableRows = 1_000;

    /// <summary>Maximum number of logical columns in one table.</summary>
    public const int MaxTableColumns = 100;

    /// <summary>Maximum number of physical grid slots in one table.</summary>
    public const int MaxTableSlots = 10_000;

    /// <summary>Maximum number of structured inlines in one cell.</summary>
    public const int MaxCellInlines = 1_000;

    /// <summary>Maximum length of one inline text value.</summary>
    public const int MaxInlineTextLength = 16_384;

    /// <summary>Maximum length of one cell HTML fragment.</summary>
    public const int MaxCellHtmlLength = 65_536;

    /// <summary>Maximum combined HTML and inline text length in one table.</summary>
    public const int MaxTableContentLength = 1_048_576;

    /// <summary>Maximum accepted CSS color literal length.</summary>
    public const int MaxCssColorLength = 128;
}
