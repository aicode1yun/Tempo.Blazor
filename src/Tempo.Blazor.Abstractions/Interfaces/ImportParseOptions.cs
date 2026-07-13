namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Options controlling how an <see cref="IImportFileParser"/> reads an import source.
/// Positional parameters carry sensible defaults so <c>new ImportParseOptions()</c> "just works".
/// </summary>
/// <param name="HasHeaderRow">
/// When <see langword="true"/> (default) the first record supplies the column names and is not
/// returned as a data row; when <see langword="false"/> columns are named positionally and every
/// record is treated as data.
/// </param>
/// <param name="Delimiter">Field delimiter for delimited formats such as CSV. Defaults to a comma.</param>
public sealed record ImportParseOptions(bool HasHeaderRow = true, char Delimiter = ',');
