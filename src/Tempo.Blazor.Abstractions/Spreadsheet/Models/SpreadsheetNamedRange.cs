using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Defines the scope of a named range within a workbook.
/// </summary>
public enum NamedRangeScope
{
    /// <summary>The named range is available across the entire workbook.</summary>
    Workbook,

    /// <summary>The named range is available only on a specific sheet.</summary>
    Sheet
}

/// <summary>
/// Represents a named range (or named constant) defined in a workbook.
/// </summary>
public sealed partial class SpreadsheetNamedRange
{
    private static readonly Regex CellRefCollisionRegex = CellRefCollisionPattern();

    /// <summary>The name of the named range.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The A1-style reference, constant value, or formula that the name refers to
    /// (e.g. <c>A1:A10</c>, <c>=Sheet2!$A$1</c>, <c>=10</c>).
    /// </summary>
    public string RefersTo { get; set; } = string.Empty;

    /// <summary>The scope of the named range.</summary>
    public NamedRangeScope Scope { get; set; } = NamedRangeScope.Workbook;

    /// <summary>
    /// When <see cref="Scope"/> is <see cref="NamedRangeScope.Sheet"/>, the zero-based
    /// index of the sheet that owns this name. Otherwise <c>null</c>.
    /// </summary>
    public int? SheetIndex { get; set; }

    /// <summary>Optional explanatory comment for the named range.</summary>
    public string? Comment { get; set; }

    /// <summary>Creates a deep copy of this named range.</summary>
    public SpreadsheetNamedRange Clone() => new()
    {
        Name = Name,
        RefersTo = RefersTo,
        Scope = Scope,
        SheetIndex = SheetIndex,
        Comment = Comment
    };

    /// <summary>
    /// Validates that <paramref name="name"/> can be used as a named range identifier.
    /// Rules: must start with a letter or underscore; remaining chars may be letters,
    /// digits, or underscores; must not collide with an A1 cell reference.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (int i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        // Reject names that look like cell references (A1, $A$1, XFD1, etc.)
        if (CellRefCollisionRegex.IsMatch(name))
            return false;

        return true;
    }

    [GeneratedRegex(@"^\$?[A-Za-z]+\$?\d+$", RegexOptions.Compiled)]
    private static partial Regex CellRefCollisionPattern();
}
