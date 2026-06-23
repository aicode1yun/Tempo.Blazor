using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Static clipboard holding copied/cut cells for internal copy-paste operations.</summary>
public static class SpreadsheetClipboard
{
    /// <summary>The copied/cut cells keyed by their relative position within the source range.</summary>
    public static Dictionary<string, SpreadsheetCell>? Cells { get; private set; }

    /// <summary>Whether the clipboard content came from a cut operation.</summary>
    public static bool IsCut { get; private set; }

    /// <summary>The source range reference (e.g. A1:B2) used for the copy/cut.</summary>
    public static string? SourceRangeRef { get; private set; }

    /// <summary>Stores the given cells into the clipboard.</summary>
    public static void Copy(Dictionary<string, SpreadsheetCell> cells, string sourceRangeRef)
    {
        Cells = cells.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
        SourceRangeRef = sourceRangeRef;
        IsCut = false;
    }

    /// <summary>Stores the given cells into the clipboard and marks it as a cut operation.</summary>
    public static void Cut(Dictionary<string, SpreadsheetCell> cells, string sourceRangeRef)
    {
        Copy(cells, sourceRangeRef);
        IsCut = true;
    }

    /// <summary>Clears the clipboard.</summary>
    public static void Clear()
    {
        Cells = null;
        SourceRangeRef = null;
        IsCut = false;
    }
}
