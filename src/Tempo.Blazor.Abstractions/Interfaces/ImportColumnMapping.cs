namespace Tempo.Blazor.Interfaces;

/// <summary>
/// A single detected column mapped to a target field, as produced by the <c>TmImportWizard</c>
/// mapping step.
/// </summary>
/// <param name="ColumnIndex">Zero-based index of the source column (see <see cref="ImportColumn.Index"/>).</param>
/// <param name="ColumnName">Detected name of the source column.</param>
/// <param name="TargetFieldKey">
/// Key of the <see cref="ImportTargetField"/> this column maps to, or <see langword="null"/> when the
/// column is ignored.
/// </param>
public sealed record ImportColumnMapping(int ColumnIndex, string ColumnName, string? TargetFieldKey);
