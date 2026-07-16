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
    static CsvImportFileParser()
    {
        // Legacy code pages (windows-1250, …) are not available on .NET without the provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    public async Task<ImportParseResult> ParseAsync(
        Stream stream, ImportParseOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        using var reader = new StreamReader(stream, ResolveEncoding(options.EncodingName), detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        return Parse(text, options);
    }

    private static Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>Parses raw CSV <paramref name="text"/> using the given <paramref name="options"/>.</summary>
    public static ImportParseResult Parse(string text, ImportParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalized = text ?? string.Empty;
        if (normalized.Length > 0 && normalized[0] == '﻿')
        {
            normalized = normalized[1..]; // strip a leading BOM on the decoded-text path too
        }

        var delimiter = options.AutoDetectDelimiter ? DetectDelimiter(normalized) : options.Delimiter;
        var records = Tokenize(normalized, delimiter);
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

    /// <summary>
    /// Sniffs the dominant separator from the first record: counts each candidate delimiter
    /// outside quoted sections up to the first unquoted newline and picks the most frequent
    /// (comma wins ties and empty input).
    /// </summary>
    private static char DetectDelimiter(string text)
    {
        char[] candidates = [',', ';', '\t', '|'];
        var counts = new int[candidates.Length];
        var inQuotes = false;

        foreach (var ch in text)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                continue;
            }

            if (ch is '\r' or '\n')
            {
                break;
            }

            var index = Array.IndexOf(candidates, ch);
            if (index >= 0)
            {
                counts[index]++;
            }
        }

        var best = 0;
        for (var i = 1; i < candidates.Length; i++)
        {
            if (counts[i] > counts[best])
            {
                best = i;
            }
        }

        return candidates[best];
    }

    /// <summary>Splits CSV text into records of raw string fields, honouring quotes and newlines.</summary>
    private static List<List<string>> Tokenize(string text, char delimiter)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var sawAny = false; // any structural content on the current line (a field char, a quote, or a delimiter)
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
                sawAny = true;
                i++;
            }
            else if (ch == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
                sawAny = true;
                i++;
            }
            else if (ch == '\r' || ch == '\n')
            {
                // End of line. Emit the record UNLESS the line was completely blank (no fields,
                // no content) so a blank line or an extra trailing newline does not become a
                // phantom all-empty row — while a lone quoted-empty field ("") stays a real
                // one-cell row (sawAny is set when a quote opens).
                if (field.Length > 0 || record.Count > 0 || sawAny)
                {
                    record.Add(field.ToString());
                    records.Add(record);
                    record = [];
                }
                field.Clear();
                sawAny = false;
                i += ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else
            {
                field.Append(ch);
                sawAny = true;
                i++;
            }
        }

        // Flush a final field/record when the text does not end with a newline. sawAny covers a
        // trailing lone/empty quoted field (e.g. `"` or `""`) that leaves field/record empty.
        if (field.Length > 0 || record.Count > 0 || sawAny)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
