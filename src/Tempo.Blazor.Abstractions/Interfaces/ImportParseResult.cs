namespace Tempo.Blazor.Interfaces;

/// <summary>
/// The outcome of parsing an import source: the detected <see cref="Columns"/> and the raw string
/// <see cref="Rows"/> (each row is a list of cell values aligned to the columns by index).
/// </summary>
/// <param name="Columns">Columns detected in the source, ordered by <see cref="ImportColumn.Index"/>.</param>
/// <param name="Rows">Data rows as raw string cells; padded so every row spans all columns.</param>
public sealed record ImportParseResult(
    IReadOnlyList<ImportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
