using System.Text;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>How a "text to columns" split divides each source row.</summary>
public enum SpreadsheetTextToColumnsMode
{
    /// <summary>Fields are separated by delimiter characters (tab, comma, …).</summary>
    Delimited,

    /// <summary>Fields are sliced at fixed character positions.</summary>
    FixedWidth
}

/// <summary>The target format applied to a produced column by the text-to-columns command.</summary>
public enum SpreadsheetColumnFormat
{
    /// <summary>Run each value through the value parser (numbers, dates, … are typed).</summary>
    General,

    /// <summary>Keep the value as literal text regardless of its content.</summary>
    Text,

    /// <summary>Do not write this column (it is dropped from the output).</summary>
    Skip
}

/// <summary>
/// Options that drive <see cref="SpreadsheetTextToColumns.Split"/>: the split mode, which delimiters
/// to use, the optional text qualifier (quote) character, whether to collapse consecutive delimiters,
/// and the break positions for fixed-width mode.
/// </summary>
public sealed class SpreadsheetSeparatorOptions
{
    /// <summary>The split mode.</summary>
    public SpreadsheetTextToColumnsMode Mode { get; set; } = SpreadsheetTextToColumnsMode.Delimited;

    /// <summary>Use the tab character as a delimiter.</summary>
    public bool Tab { get; set; }

    /// <summary>Use the semicolon as a delimiter.</summary>
    public bool Semicolon { get; set; }

    /// <summary>Use the comma as a delimiter.</summary>
    public bool Comma { get; set; }

    /// <summary>Use the space as a delimiter.</summary>
    public bool Space { get; set; }

    /// <summary>An additional custom delimiter character, or null.</summary>
    public string? OtherDelimiter { get; set; }

    /// <summary>The text qualifier (quote) character that protects delimiters inside a field. Null disables qualifiers.</summary>
    public char? TextQualifier { get; set; } = '"';

    /// <summary>Collapse runs of consecutive delimiters into a single split (no empty fields between them).</summary>
    public bool TreatConsecutiveAsOne { get; set; }

    /// <summary>Zero-based character break positions for <see cref="SpreadsheetTextToColumnsMode.FixedWidth"/>.</summary>
    public List<int> FixedWidthBreaks { get; set; } = [];

    /// <summary>Builds the effective set of delimiter characters for delimited mode.</summary>
    public IReadOnlyList<char> GetDelimiters()
    {
        var set = new List<char>();
        if (Tab) set.Add('\t');
        if (Semicolon) set.Add(';');
        if (Comma) set.Add(',');
        if (Space) set.Add(' ');
        if (!string.IsNullOrEmpty(OtherDelimiter))
            set.Add(OtherDelimiter![0]);
        return set;
    }
}

/// <summary>
/// Pure engine that splits raw text rows into columns either by delimiters (honouring a text
/// qualifier and optional collapsing of consecutive delimiters) or by fixed character widths. Returns
/// a jagged list of string tokens per row; type detection and target placement are the caller's job.
/// </summary>
public static class SpreadsheetTextToColumns
{
    /// <summary>Splits each row in <paramref name="rows"/> according to <paramref name="options"/>.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Split(IReadOnlyList<string> rows, SpreadsheetSeparatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);

        return options.Mode == SpreadsheetTextToColumnsMode.FixedWidth
            ? rows.Select(r => SplitFixedWidth(r ?? string.Empty, options.FixedWidthBreaks)).ToList()
            : rows.Select(r => SplitDelimited(r ?? string.Empty, options)).ToList();
    }

    private static IReadOnlyList<string> SplitDelimited(string row, SpreadsheetSeparatorOptions options)
    {
        var delimiters = options.GetDelimiters();
        if (delimiters.Count == 0)
            return [row];

        var qualifier = options.TextQualifier;
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < row.Length; i++)
        {
            var ch = row[i];

            if (qualifier is { } q && ch == q)
            {
                // A doubled qualifier inside a quoted field is an escaped literal qualifier.
                if (inQuotes && i + 1 < row.Length && row[i + 1] == q)
                {
                    current.Append(q);
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (!inQuotes && delimiters.Contains(ch))
            {
                fields.Add(current.ToString());
                current.Clear();

                if (options.TreatConsecutiveAsOne)
                    while (i + 1 < row.Length && delimiters.Contains(row[i + 1]))
                        i++;

                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static IReadOnlyList<string> SplitFixedWidth(string row, IReadOnlyList<int> breaks)
    {
        var sorted = breaks.Where(b => b > 0 && b < row.Length).Distinct().OrderBy(b => b).ToList();
        if (sorted.Count == 0)
            return [row.Trim()];

        var fields = new List<string>();
        var start = 0;
        foreach (var b in sorted)
        {
            fields.Add(row[start..b].Trim());
            start = b;
        }
        fields.Add(row[start..].Trim());
        return fields;
    }
}
