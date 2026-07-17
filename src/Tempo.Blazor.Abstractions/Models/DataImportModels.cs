using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>One row prepared for import: mapped target-field values keyed by field key.</summary>
public sealed class DataImportRow
{
    /// <summary>1-based row number in the source file (excluding the header row).</summary>
    public int RowNumber { get; set; }

    /// <summary>Cell values keyed by the target field key the source column was mapped to.</summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Creates a deep copy.</summary>
    public DataImportRow Clone()
        => new()
        {
            RowNumber = RowNumber,
            Values = new Dictionary<string, string>(Values, StringComparer.Ordinal)
        };
}

/// <summary>
/// One row-level problem found while validating or importing. <see cref="Message"/> is
/// display text supplied by the import target (data, not component chrome).
/// </summary>
public sealed class DataImportRowError
{
    /// <summary>1-based source row number the error belongs to.</summary>
    public int RowNumber { get; set; }

    /// <summary>Key of the offending target field, or null for a whole-row problem.</summary>
    public string? FieldKey { get; set; }

    /// <summary>Human-readable reason supplied by the target or validator.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Result of importing one batch of rows.</summary>
public sealed class DataImportBatchResult
{
    /// <summary>Number of rows the target accepted from the batch.</summary>
    public int ImportedCount { get; init; }

    /// <summary>Rows the target rejected, with reasons. The import continues past them.</summary>
    public IReadOnlyList<DataImportRowError> Errors { get; init; } = [];
}

/// <summary>Summary of one finished import run, as reported by the import component.</summary>
public sealed class DataImportResult
{
    /// <summary>Identifier of the import session (usable for a later rollback).</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Rows the target accepted.</summary>
    public int ImportedCount { get; init; }

    /// <summary>Rows skipped before import because dry-run validation rejected them.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Rows the target rejected during import.</summary>
    public int FailedCount { get; init; }
}

/// <summary>
/// Destination of a data import: declares the target schema and accepts batches.
/// Validation (dry-run) and import are batched so large files never block the UI thread;
/// <see cref="RollbackAsync"/> undoes everything imported under one session id.
/// </summary>
public interface IDataImportTarget
{
    /// <summary>Fields of the target schema that source columns map onto.</summary>
    IReadOnlyList<ImportTargetField> Fields { get; }

    /// <summary>Validates a batch without importing (dry-run); returns the failures (empty when clean).</summary>
    Task<IReadOnlyList<DataImportRowError>> ValidateBatchAsync(
        IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a batch under <paramref name="sessionId"/>. Invalid rows are reported in the
    /// result and skipped — the batch continues past them (partial import).
    /// </summary>
    Task<DataImportBatchResult> ImportBatchAsync(
        string sessionId, IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default);

    /// <summary>Removes everything imported under <paramref name="sessionId"/>.</summary>
    Task RollbackAsync(string sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory <see cref="IDataImportTarget"/> for demos and tests: a pluggable row validator,
/// per-session storage with rollback, and clone-on-read snapshots so callers exercise real
/// persistence semantics.
/// </summary>
public sealed class InMemoryDataImportTarget : IDataImportTarget
{
    private readonly object _gate = new();
    private readonly List<(string SessionId, DataImportRow Row)> _rows = [];
    private readonly Func<DataImportRow, IEnumerable<DataImportRowError>>? _rowValidator;

    /// <summary>Creates the target with its schema and an optional per-row validator.</summary>
    public InMemoryDataImportTarget(
        IEnumerable<ImportTargetField> fields,
        Func<DataImportRow, IEnumerable<DataImportRowError>>? rowValidator = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = fields.ToList();
        _rowValidator = rowValidator;
    }

    /// <inheritdoc />
    public IReadOnlyList<ImportTargetField> Fields { get; }

    /// <summary>Snapshot of all imported rows across sessions, in import order.</summary>
    public IReadOnlyList<DataImportRow> ImportedRows
    {
        get
        {
            lock (_gate)
            {
                return _rows.Select(entry => entry.Row.Clone()).ToList();
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DataImportRowError>> ValidateBatchAsync(
        IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        IReadOnlyList<DataImportRowError> errors = rows.SelectMany(ValidateRow).ToList();
        return Task.FromResult(errors);
    }

    /// <inheritdoc />
    public Task<DataImportBatchResult> ImportBatchAsync(
        string sessionId, IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var errors = new List<DataImportRowError>();
        var imported = 0;

        lock (_gate)
        {
            foreach (var row in rows)
            {
                var rowErrors = ValidateRow(row).ToList();
                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors);
                    continue;
                }

                _rows.Add((sessionId, row.Clone()));
                imported++;
            }
        }

        return Task.FromResult(new DataImportBatchResult { ImportedCount = imported, Errors = errors });
    }

    /// <inheritdoc />
    public Task RollbackAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _rows.RemoveAll(entry => string.Equals(entry.SessionId, sessionId, StringComparison.Ordinal));
        }

        return Task.CompletedTask;
    }

    private IEnumerable<DataImportRowError> ValidateRow(DataImportRow row)
        => _rowValidator?.Invoke(row) ?? [];
}
