namespace Tempo.Blazor.Interfaces;

/// <summary>
/// A column detected by an <see cref="IImportFileParser"/> while parsing an import source
/// (e.g. a CSV header cell or a generated positional name when the source has no header).
/// </summary>
/// <param name="Index">Zero-based position of the column in the parsed rows.</param>
/// <param name="Name">Detected header name, or a generated placeholder when no header exists.</param>
public sealed record ImportColumn(int Index, string Name);
