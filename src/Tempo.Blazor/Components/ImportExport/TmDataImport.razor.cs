using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.ImportExport;

/// <summary>
/// Full data-import flow over the <see cref="TmImportWizard"/> shell: upload XLSX/CSV
/// (dialect + encoding aware), preview, column-to-schema mapping, batched dry-run
/// validation with a downloadable error report, and batched import with progress,
/// partial import (skip invalid rows), failed-rows download for continuation, and
/// per-session rollback — all through <see cref="IDataImportTarget"/>.
/// </summary>
public partial class TmDataImport : TmComponentBase
{
    private const int MaxDisplayedErrors = 100;
    private const long MaxFileBytes = 50 * 1024 * 1024;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Destination of the import: target schema, validation, batches, rollback. Required.</summary>
    [Parameter, EditorRequired] public IDataImportTarget Target { get; set; } = default!;

    /// <summary>
    /// Parsers by lower-case file extension (e.g. ".xlsx" → XlsxImportFileParser from
    /// Tempo.Blazor.DataTableXlsx). ".csv"/".txt" fall back to the built-in CSV parser.
    /// </summary>
    [Parameter] public IReadOnlyDictionary<string, IImportFileParser>? Parsers { get; set; }

    /// <summary>Parse options override. Null uses the upload step's dialect and encoding selectors.</summary>
    [Parameter] public ImportParseOptions? ParseOptions { get; set; }

    /// <summary>Rows per provider batch for validation and import. Default is 500.</summary>
    [Parameter] public int BatchSize { get; set; } = 500;

    /// <summary>Rows shown in the upload preview. Default is 10.</summary>
    [Parameter] public int PreviewRowCount { get; set; } = 10;

    /// <summary>Callback invoked when an import run finishes, with the run summary.</summary>
    [Parameter] public EventCallback<DataImportResult> OnCompleted { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private readonly CsvImportFileParser _csvParser = new();
    private int _activeStep;
    private string? _gateMessage;
    private string _fileName = string.Empty;
    private byte[]? _fileBytes;
    private IImportFileParser? _fileParser;
    private ImportParseResult? _parseResult;
    private string _delimiterChoice = "auto";
    private string _encodingChoice = "utf-8";
    private readonly Dictionary<int, string?> _mappings = [];
    private List<DataImportRow> _rows = [];
    private List<DataImportRowError> _validationErrors = [];
    private bool _validationRan;
    private bool _validating;
    private bool _skipInvalid;
    private bool _importing;
    private int _progressPercent;
    private List<DataImportRowError> _importErrors = [];
    private string _sessionId = string.Empty;
    private DataImportResult? _result;
    private bool _rolledBack;
    private int _runGeneration;

    private HashSet<int> InvalidRowNumbers
        => _validationErrors.Select(e => e.RowNumber).ToHashSet();

    private int InvalidRowCount => InvalidRowNumbers.Count;

    private int ValidRowCount => _rows.Count - InvalidRowCount;

    private int SkippedByValidationCount => _skipInvalid ? InvalidRowCount : 0;

    private int RowsToImportCount => _skipInvalid ? ValidRowCount : _rows.Count;

    /// <summary>Row numbers that did not make it into the target: validation-skipped + import-rejected.</summary>
    private HashSet<int> FailedRowNumbers
    {
        get
        {
            var failed = _importErrors.Select(e => e.RowNumber).ToHashSet();
            if (_skipInvalid)
            {
                failed.UnionWith(InvalidRowNumbers);
            }

            return failed;
        }
    }

    // ── Step gating ──────────────────────────────────────────────────────────

    private void HandleActiveStepChangedAsync(int requested)
    {
        _gateMessage = null;

        if (requested < _activeStep)
        {
            _activeStep = requested;
            return;
        }

        switch (_activeStep)
        {
            case 0 when _parseResult is null:
                _gateMessage = Loc["TmDataImport_GateUploadRequired"];
                return;
            case 1:
                var missing = MissingRequiredFields();
                if (missing.Count > 0)
                {
                    _gateMessage = string.Format(
                        Loc["TmDataImport_GateRequiredUnmapped"], string.Join(", ", missing.Select(f => f.Label)));
                    return;
                }

                var duplicates = DuplicateMappedFields();
                if (duplicates.Count > 0)
                {
                    _gateMessage = string.Format(
                        Loc["TmDataImport_GateDuplicateMapping"], string.Join(", ", duplicates.Select(f => f.Label)));
                    return;
                }

                break;
            case 2 when _validationErrors.Count > 0 && !_skipInvalid:
                _gateMessage = Loc["TmDataImport_GateErrorsRemain"];
                return;
        }

        _activeStep = requested;
        if (_activeStep == 2)
        {
            BuildRows();
            // Deliberately not awaited: the step transition (and its click chain) completes
            // immediately, while the batched dry-run reports through InvokeAsync renders.
            _ = RunDryRunAsync();
        }
    }

    private List<ImportTargetField> MissingRequiredFields()
    {
        var mapped = _mappings.Values.Where(v => v is not null).ToHashSet(StringComparer.Ordinal);
        return Target.Fields.Where(f => f.Required && !mapped.Contains(f.Key)).ToList();
    }

    private List<ImportTargetField> DuplicateMappedFields()
    {
        var duplicateKeys = _mappings.Values
            .Where(v => v is not null)
            .GroupBy(v => v!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        return Target.Fields.Where(f => duplicateKeys.Contains(f.Key)).ToList();
    }

    // ── Upload + parse ───────────────────────────────────────────────────────

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        _gateMessage = null;
        _parseResult = null;
        _fileBytes = null;
        _fileName = e.File.Name;

        var extension = Path.GetExtension(_fileName).ToLowerInvariant();
        _fileParser = ResolveParser(extension);
        if (_fileParser is null)
        {
            _gateMessage = string.Format(Loc["TmDataImport_GateUnsupportedFile"], extension);
            return;
        }

        try
        {
            using var buffer = new MemoryStream();
            await using (var stream = e.File.OpenReadStream(MaxFileBytes))
            {
                await stream.CopyToAsync(buffer);
            }

            _fileBytes = buffer.ToArray();
            await ParseBufferedFileAsync();
        }
        catch (Exception exception)
        {
            _fileBytes = null;
            _parseResult = null;
            _gateMessage = string.Format(Loc["TmDataImport_GateParseFailed"], exception.Message);
        }
    }

    private IImportFileParser? ResolveParser(string extension)
    {
        if (Parsers is not null && Parsers.TryGetValue(extension, out var parser))
        {
            return parser;
        }

        return extension is ".csv" or ".txt" ? _csvParser : null;
    }

    private ImportParseOptions EffectiveParseOptions
    {
        get
        {
            if (ParseOptions is not null)
            {
                return ParseOptions;
            }

            var delimiter = _delimiterChoice switch
            {
                "tab" => '\t',
                { Length: 1 } choice => choice[0],
                _ => ','
            };
            return new ImportParseOptions(
                AutoDetectDelimiter: _delimiterChoice == "auto",
                Delimiter: delimiter,
                EncodingName: _encodingChoice == "utf-8" ? null : _encodingChoice);
        }
    }

    private async Task ParseBufferedFileAsync()
    {
        if (_fileBytes is null || _fileParser is null)
        {
            return;
        }

        using var stream = new MemoryStream(_fileBytes, writable: false);
        _parseResult = await _fileParser.ParseAsync(stream, EffectiveParseOptions);
        AutoMapColumns();
        ResetDownstreamState();
    }

    private void AutoMapColumns()
    {
        _mappings.Clear();
        if (_parseResult is null)
        {
            return;
        }

        foreach (var column in _parseResult.Columns)
        {
            var match = Target.Fields.FirstOrDefault(field =>
                string.Equals(field.Key, column.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(field.Label, column.Name, StringComparison.OrdinalIgnoreCase));
            _mappings[column.Index] = match?.Key;
        }
    }

    private async Task OnDelimiterChangedAsync(ChangeEventArgs e)
    {
        _delimiterChoice = e.Value?.ToString() ?? "auto";
        await ParseBufferedFileAsync();
    }

    private async Task OnEncodingChangedAsync(ChangeEventArgs e)
    {
        _encodingChoice = e.Value?.ToString() ?? "utf-8";
        await ParseBufferedFileAsync();
    }

    private void OnMappingChanged(int columnIndex, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _mappings[columnIndex] = string.IsNullOrEmpty(value) ? null : value;
        _gateMessage = null;
        ResetDownstreamState();
    }

    /// <summary>Mapping or source changed: any earlier dry-run or import result is stale.</summary>
    private void ResetDownstreamState()
    {
        // Abandon any dry-run/import loop still running against the previous state.
        _runGeneration++;
        _rows = [];
        _validationErrors = [];
        _validationRan = false;
        _validating = false;
        _importing = false;
        _skipInvalid = false;
        _importErrors = [];
        _result = null;
        _rolledBack = false;
        _progressPercent = 0;
    }

    // ── Rows + dry-run ───────────────────────────────────────────────────────

    private void BuildRows()
    {
        _rows = [];
        if (_parseResult is null)
        {
            return;
        }

        var mappedColumns = _mappings
            .Where(pair => pair.Value is not null)
            .Select(pair => (Index: pair.Key, Key: pair.Value!))
            .ToList();

        for (var i = 0; i < _parseResult.Rows.Count; i++)
        {
            var source = _parseResult.Rows[i];
            var row = new DataImportRow { RowNumber = i + 1 };
            foreach (var (index, key) in mappedColumns)
            {
                row.Values[key] = index < source.Count ? source[index] : string.Empty;
            }

            _rows.Add(row);
        }
    }

    private async Task RunDryRunAsync()
    {
        var generation = _runGeneration;
        var rows = _rows;
        _validating = true;
        _validationRan = false;
        _validationErrors = [];
        _skipInvalid = false;
        await InvokeAsync(StateHasChanged);

        try
        {
            var errors = new List<DataImportRowError>();
            for (var offset = 0; offset < rows.Count; offset += BatchSize)
            {
                if (generation != _runGeneration)
                {
                    return;   // the source or mapping changed under this run — abandon it
                }

                var batch = rows.Skip(offset).Take(BatchSize).ToList();
                errors.AddRange(await Target.ValidateBatchAsync(batch));
                // Yield so large files never freeze the (single) WASM UI thread.
                await Task.Delay(1);
            }

            if (generation != _runGeneration)
            {
                return;
            }

            _validationErrors = errors;
            _validationRan = true;
        }
        catch (Exception exception)
        {
            // Fire-and-forget context: a throwing target must surface, not crash unobserved.
            _gateMessage = string.Format(Loc["TmDataImport_GateTargetFailed"], exception.Message);
        }
        finally
        {
            if (generation == _runGeneration)
            {
                _validating = false;
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Import ───────────────────────────────────────────────────────────────

    private void StartImport()
    {
        if (_importing || _result is not null)
        {
            return;
        }

        var invalid = InvalidRowNumbers;
        var rowsToImport = _skipInvalid
            ? _rows.Where(r => !invalid.Contains(r.RowNumber)).ToList()
            : _rows;
        if (rowsToImport.Count == 0)
        {
            return;
        }

        _importing = true;
        _progressPercent = 0;
        _importErrors = [];
        _sessionId = Guid.NewGuid().ToString("N");

        // Deliberately not awaited: the click completes immediately and the batched
        // import reports progress through InvokeAsync renders.
        _ = RunImportAsync(rowsToImport);
    }

    private async Task RunImportAsync(List<DataImportRow> rowsToImport)
    {
        var generation = _runGeneration;
        var sessionId = _sessionId;
        var imported = 0;

        try
        {
            for (var offset = 0; offset < rowsToImport.Count; offset += BatchSize)
            {
                if (generation != _runGeneration)
                {
                    return;   // reset/cancel while importing — stop feeding the target
                }

                var batch = rowsToImport.Skip(offset).Take(BatchSize).ToList();
                var batchResult = await Target.ImportBatchAsync(sessionId, batch);
                imported += batchResult.ImportedCount;
                _importErrors.AddRange(batchResult.Errors);
                _progressPercent = (int)Math.Round(
                    Math.Min(offset + batch.Count, rowsToImport.Count) * 100d / rowsToImport.Count);
                await InvokeAsync(StateHasChanged);
                await Task.Delay(1);
            }

            if (generation != _runGeneration)
            {
                return;
            }

            _progressPercent = 100;
            _result = new DataImportResult
            {
                SessionId = _sessionId,
                ImportedCount = imported,
                SkippedCount = SkippedByValidationCount,
                FailedCount = _importErrors.Select(e => e.RowNumber).Distinct().Count()
            };
            await OnCompleted.InvokeAsync(_result);
        }
        catch (Exception exception)
        {
            // Fire-and-forget context: a throwing target must surface, not crash unobserved.
            _gateMessage = string.Format(Loc["TmDataImport_GateTargetFailed"], exception.Message);
        }
        finally
        {
            if (generation == _runGeneration)
            {
                _importing = false;
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RollbackAsync()
    {
        if (_result is null || _rolledBack)
        {
            return;
        }

        await Target.RollbackAsync(_sessionId);
        _rolledBack = true;
    }

    private void Reset()
    {
        _activeStep = 0;
        _gateMessage = null;
        _fileName = string.Empty;
        _fileBytes = null;
        _fileParser = null;
        _parseResult = null;
        _mappings.Clear();
        ResetDownstreamState();
    }

    // ── Downloads ────────────────────────────────────────────────────────────

    private async Task DownloadErrorReportAsync()
    {
        var csv = new StringBuilder();
        csv.Append(CsvField(Loc["TmDataImport_ErrorRow"])).Append(',')
           .Append(CsvField(Loc["TmDataImport_ErrorField"])).Append(',')
           .Append(CsvField(Loc["TmDataImport_ErrorReason"])).Append("\r\n");
        foreach (var error in _validationErrors)
        {
            csv.Append(error.RowNumber).Append(',')
               .Append(CsvField(FieldLabel(error.FieldKey))).Append(',')
               .Append(CsvField(error.Message)).Append("\r\n");
        }

        await DownloadCsvAsync("import-errors.csv", csv.ToString());
    }

    private async Task DownloadFailedRowsAsync()
    {
        // Failed rows keep the target-field layout so the file can be corrected and re-imported.
        var fields = Target.Fields
            .Where(f => _mappings.Values.Contains(f.Key, StringComparer.Ordinal))
            .ToList();
        var failed = FailedRowNumbers;

        var csv = new StringBuilder();
        csv.Append(string.Join(",", fields.Select(f => CsvField(f.Label)))).Append("\r\n");
        foreach (var row in _rows.Where(r => failed.Contains(r.RowNumber)))
        {
            csv.Append(string.Join(",", fields.Select(f => CsvField(row.Values.GetValueOrDefault(f.Key) ?? string.Empty))))
               .Append("\r\n");
        }

        await DownloadCsvAsync("import-failed-rows.csv", csv.ToString());
    }

    private async Task DownloadCsvAsync(string fileName, string content)
    {
        try
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
            await JS.InvokeVoidAsync("tmDataTable.downloadFile", fileName, "text/csv", Convert.ToBase64String(bytes));
        }
        catch
        {
            // Download helpers are unavailable in non-browser render contexts (e.g. prerendering).
        }
    }

    private static string CsvField(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    // ── Display helpers ──────────────────────────────────────────────────────

    private string FieldLabel(string? fieldKey)
        => Target.Fields.FirstOrDefault(f => string.Equals(f.Key, fieldKey, StringComparison.Ordinal))?.Label
           ?? fieldKey
           ?? string.Empty;

    private string FieldOptionLabel(ImportTargetField field)
        => field.Required ? string.Format(Loc["TmDataImport_RequiredField"], field.Label) : field.Label;
}
