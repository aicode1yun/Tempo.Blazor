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
/// <param name="AutoDetectDelimiter">
/// When <see langword="true"/>, delimited parsers sniff the dominant separator
/// (comma, semicolon, tab, or pipe) from the first record instead of using <paramref name="Delimiter"/>.
/// Defaults to <see langword="false"/>.
/// </param>
/// <param name="EncodingName">
/// Text encoding of the source (e.g. "utf-8", "windows-1250"). Null (default) reads UTF-8 with
/// byte-order-mark detection; an unknown name falls back to UTF-8.
/// </param>
public sealed record ImportParseOptions(
    bool HasHeaderRow = true,
    char Delimiter = ',',
    bool AutoDetectDelimiter = false,
    string? EncodingName = null);
