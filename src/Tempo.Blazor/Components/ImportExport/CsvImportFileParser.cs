using System.Text;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.ImportExport;

/// <summary>
/// Dependency-free CSV <see cref="IImportFileParser"/>. Implements RFC-4180-ish parsing: quoted
/// fields, embedded delimiters, embedded newlines, escaped quotes (<c>""</c>), and CR/LF/CRLF line
/// endings. Reads UTF-8 and transparently strips a leading BOM.
/// <para>
/// Rows are normalised to a rectangular shape: the detected column count is the widest record, and
/// shorter rows are padded with empty cells so every row aligns to the columns by index.
/// </para>
/// </summary>
public sealed class CsvImportFileParser : IImportFileParser
{
    /// <inheritdoc />
    public async Task<ImportParseResult> ParseAsync(
        Stream stream, ImportParseOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        return Parse(text, options);
    }

    /// <summary>Parses raw CSV <paramref name="text"/> using the given <paramref name="options"/>.</summary>
    public static ImportParseResult Parse(string text, ImportParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var records = Tokenize(text ?? string.Empty, options.Delimiter);
        if (records.Count == 0)
        {
            return new ImportParseResult([], []);
        }

        List<string>? header = null;
        IEnumerable<List<string>> dataRecords;
        if (options.HasHeaderRow)
        {
            header = records[0];
            dataRecords = records.Skip(1);
        }
        else
        {
            dataRecords = records;
        }

        var data = dataRecords.ToList();

        var columnCount = header?.Count ?? 0;
        foreach (var record in data)
        {
            if (record.Count > columnCount)
            {
                columnCount = record.Count;
            }
        }

        if (columnCount == 0)
        {
            return new ImportParseResult([], []);
        }

        var columns = new List<ImportColumn>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            var name = header is not null && i < header.Count && !string.IsNullOrWhiteSpace(header[i])
                ? header[i]
                : $"Column {i + 1}";
            columns.Add(new ImportColumn(i, name));
        }

        var rows = new List<IReadOnlyList<string>>(data.Count);
        foreach (var record in data)
        {
            var row = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                row[i] = i < record.Count ? record[i] : string.Empty;
            }
            rows.Add(row);
        }

        return new ImportParseResult(columns, rows);
    }

    /// <summary>Splits CSV text into records of raw string fields, honouring quotes and newlines.</summary>
    private static List<List<string>> Tokenize(string text, char delimiter)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    field.Append(ch);
                    i++;
                }

                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
                i++;
            }
            else if (ch == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
                i++;
            }
            else if (ch == '\r')
            {
                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = [];
                i += i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else if (ch == '\n')
            {
                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = [];
                i++;
            }
            else
            {
                field.Append(ch);
                i++;
            }
        }

        // Flush a final field/record when the text does not end with a newline.
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
