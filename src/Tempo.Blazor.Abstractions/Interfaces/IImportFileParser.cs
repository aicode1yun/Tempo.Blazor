namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Pluggable hook that parses an uploaded import file into rows and detected columns. Supply an
/// implementation to <c>TmImportWizard</c> to power its parse + column-mapping flow.
/// <para>
/// The core library ships a dependency-free CSV implementation (<c>CsvImportFileParser</c>). This
/// interface is the extension point: a future package (Plán 4 / <c>TmDataImport</c>) can provide an
/// XLSX or JSON parser without any change to the wizard.
/// </para>
/// </summary>
public interface IImportFileParser
{
    /// <summary>Parses <paramref name="stream"/> into <see cref="ImportParseResult"/> rows and columns.</summary>
    /// <param name="stream">Readable stream of the uploaded file's bytes.</param>
    /// <param name="options">Parsing options (header handling, delimiter, …).</param>
    /// <param name="ct">Token used to cancel the operation.</param>
    Task<ImportParseResult> ParseAsync(Stream stream, ImportParseOptions options, CancellationToken ct = default);
}
