namespace Tempo.Blazor.Interfaces;

/// <summary>
/// The result handed back to the host when the user applies a column mapping in <c>TmImportWizard</c>.
/// Carries both the <see cref="Mappings"/> (which source column went to which target field) and the
/// <see cref="Rows"/> re-projected onto the target field keys — mapped values only, ignored columns
/// omitted. This is a mapping payload, not a full import engine.
/// </summary>
/// <param name="Mappings">The chosen column-to-field mappings, one per detected source column.</param>
/// <param name="Rows">
/// Data rows keyed by <see cref="ImportTargetField.Key"/>; a key is present only when a source column
/// was mapped to it.
/// </param>
public sealed record ImportMappingResult(
    IReadOnlyList<ImportColumnMapping> Mappings,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);
