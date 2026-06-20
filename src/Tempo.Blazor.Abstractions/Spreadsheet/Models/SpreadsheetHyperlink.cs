namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// The kind of hyperlink stored in a spreadsheet cell.
/// </summary>
public enum SpreadsheetHyperlinkKind
{
    /// <summary>External web URL (http/https).</summary>
    Web,

    /// <summary>E-mail hyperlink (mailto).</summary>
    Email,

    /// <summary>Reference to a cell or range inside the workbook.</summary>
    InternalRef,

    /// <summary>Reference to a named range inside the workbook.</summary>
    NamedRange
}

/// <summary>
/// Represents a hyperlink attached to a spreadsheet cell.
/// </summary>
public sealed class SpreadsheetHyperlink
{
    /// <summary>The kind of hyperlink.</summary>
    public SpreadsheetHyperlinkKind Kind { get; set; }

    /// <summary>
    /// The hyperlink target. For <see cref="SpreadsheetHyperlinkKind.Web"/> this is the URL;
    /// for <see cref="SpreadsheetHyperlinkKind.Email"/> the e-mail address (without the
    /// <c>mailto:</c> prefix); for <see cref="SpreadsheetHyperlinkKind.InternalRef"/> an A1
    /// reference or sheet-qualified reference; for <see cref="SpreadsheetHyperlinkKind.NamedRange"/>
    /// the name of the named range.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Optional display text shown in the cell instead of the target.</summary>
    public string? Display { get; set; }

    /// <summary>Optional tooltip shown on hover.</summary>
    public string? Tooltip { get; set; }

    /// <summary>
    /// For <see cref="SpreadsheetHyperlinkKind.Email"/> hyperlinks, an optional subject line
    /// appended to the <c>mailto:</c> URI.
    /// </summary>
    public string? EmailSubject { get; set; }

    /// <summary>
    /// Returns the full navigable URI for this hyperlink (URL, mailto URI, or target reference).
    /// </summary>
    public string GetUri()
    {
        return Kind switch
        {
            SpreadsheetHyperlinkKind.Web => Target,
            SpreadsheetHyperlinkKind.Email => BuildMailto(Target, EmailSubject),
            SpreadsheetHyperlinkKind.InternalRef => Target,
            SpreadsheetHyperlinkKind.NamedRange => Target,
            _ => Target
        };
    }

    /// <summary>Creates a deep copy of this hyperlink.</summary>
    public SpreadsheetHyperlink Clone() => new()
    {
        Kind = Kind,
        Target = Target,
        Display = Display,
        Tooltip = Tooltip,
        EmailSubject = EmailSubject
    };

    private static string BuildMailto(string email, string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return $"mailto:{email}";
        return $"mailto:{email}?subject={Uri.EscapeDataString(subject)}";
    }
}
