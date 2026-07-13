namespace Tempo.Blazor.Interfaces;

/// <summary>
/// A destination field the host declares for <c>TmImportWizard</c> column mapping. Each detected
/// source column can be mapped to one target field (or ignored).
/// </summary>
/// <param name="Key">Stable identifier used as the key in the mapped output rows.</param>
/// <param name="Label">Human-readable label shown in the mapping UI (localized by the host).</param>
/// <param name="Required">Whether the host considers this field mandatory. Informational only.</param>
public sealed record ImportTargetField(string Key, string Label, bool Required = false);
