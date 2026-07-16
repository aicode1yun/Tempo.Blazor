using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.AuditLog;

/// <summary>
/// Virtualized audit log viewer for large event stores (millions of rows). Provides a
/// timeline histogram, actor/action/entity/period filters, full-text search, an inline
/// detail with property-level change diffs, CSV export of the filtered set, and an
/// optional hash-chain integrity indicator. Data access is pluggable through
/// <see cref="IAuditLogProvider"/>.
/// </summary>
public partial class TmAuditLogViewer : TmComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Data source of the viewer. Required.</summary>
    [Parameter, EditorRequired] public IAuditLogProvider Provider { get; set; } = default!;

    /// <summary>Height of the entry list (CSS value). Default is "480px".</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Whether rows are virtualized (recommended for large logs). Default is true.</summary>
    [Parameter] public bool Virtualized { get; set; } = true;

    /// <summary>Pixel height of one row used by virtualization. Default is 44.</summary>
    [Parameter] public float ItemSize { get; set; } = 44f;

    /// <summary>Maximum rows rendered when <see cref="Virtualized"/> is false. Default is 500.</summary>
    [Parameter] public int NonVirtualizedMaxItems { get; set; } = 500;

    /// <summary>Whether the timeline histogram is shown. Default is true.</summary>
    [Parameter] public bool ShowTimeline { get; set; } = true;

    /// <summary>Number of timeline buckets. Default is 40.</summary>
    [Parameter] public int TimelineBucketCount { get; set; } = 40;

    /// <summary>Whether the filter controls are shown. Default is true.</summary>
    [Parameter] public bool ShowFilters { get; set; } = true;

    /// <summary>Whether the search input is shown. Default is true.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <summary>Whether the CSV export action is offered. Default is true.</summary>
    [Parameter] public bool ShowExport { get; set; } = true;

    /// <summary>File name of the CSV export. Default is "audit-log.csv".</summary>
    [Parameter] public string ExportFileName { get; set; } = "audit-log.csv";

    /// <summary>Maximum number of rows included in the CSV export. Default is 100000.</summary>
    [Parameter] public int ExportMaxRows { get; set; } = 100_000;

    /// <summary>
    /// Whether the hash-chain integrity indicator is shown when the provider implements
    /// <see cref="IAuditLogIntegrityProvider"/>. Default is true.
    /// </summary>
    [Parameter] public bool ShowIntegrity { get; set; } = true;

    /// <summary>Callback invoked when an entry is selected (expanded).</summary>
    [Parameter] public EventCallback<AuditLogEntry> OnEntrySelected { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private Virtualize<AuditLogEntry>? _virtualize;
    private IAuditLogProvider? _loadedProvider;
    private AuditLogFilterOptions _filterOptions = new();
    private IReadOnlyList<AuditLogTimelineBucket> _timeline = [];
    private List<AuditLogEntry> _plainItems = [];
    private long _totalCount;
    private bool _loading;
    private string? _expandedEntryId;
    private AuditLogIntegrityResult? _integrity;
    private bool _integrityLoading;
    private bool _exporting;

    // Filter state
    private string _search = string.Empty;
    private string _actorFilter = string.Empty;
    private string _actionFilter = string.Empty;
    private string _entityTypeFilter = string.Empty;
    private DateTimeOffset? _from;
    private DateTimeOffset? _to;

    private string EffectiveHeight => string.IsNullOrEmpty(Height) ? "480px" : Height!;

    private bool HasActiveFilter
        => !string.IsNullOrEmpty(_search)
           || !string.IsNullOrEmpty(_actorFilter)
           || !string.IsNullOrEmpty(_actionFilter)
           || !string.IsNullOrEmpty(_entityTypeFilter)
           || _from is not null
           || _to is not null;

    private bool SupportsIntegrity => ShowIntegrity && Provider is IAuditLogIntegrityProvider;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(Provider, _loadedProvider))
        {
            _loadedProvider = Provider;
            _search = string.Empty;
            _actorFilter = string.Empty;
            _actionFilter = string.Empty;
            _entityTypeFilter = string.Empty;
            _from = null;
            _to = null;
            _expandedEntryId = null;
            _integrity = null;

            await LoadFilterOptionsAsync();
            await RefreshAsync();
            await VerifyIntegrityAsync();
        }
    }

    private async Task LoadFilterOptionsAsync()
    {
        try
        {
            _filterOptions = await Provider.GetFilterOptionsAsync();
        }
        catch
        {
            _filterOptions = new AuditLogFilterOptions();
        }
    }

    private async Task VerifyIntegrityAsync()
    {
        if (Provider is not IAuditLogIntegrityProvider integrityProvider || !ShowIntegrity)
        {
            return;
        }

        _integrityLoading = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            _integrity = await integrityProvider.VerifyIntegrityAsync();
        }
        catch
        {
            _integrity = null;
        }
        finally
        {
            _integrityLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    private AuditLogQuery BuildQuery(int skip, int take)
        => new()
        {
            Skip = skip,
            Take = take,
            ActorId = string.IsNullOrEmpty(_actorFilter) ? null : _actorFilter,
            Action = string.IsNullOrEmpty(_actionFilter) ? null : _actionFilter,
            EntityType = string.IsNullOrEmpty(_entityTypeFilter) ? null : _entityTypeFilter,
            From = _from,
            To = _to,
            SearchText = string.IsNullOrWhiteSpace(_search) ? null : _search
        };

    private int _refreshGeneration;

    /// <summary>Reloads the entry list, count, and timeline from the provider.</summary>
    public async Task RefreshAsync()
    {
        // Rapid filter changes overlap with async providers; only the newest refresh may
        // publish its results, or a slow stale query would overwrite the fresh state.
        var generation = ++_refreshGeneration;
        _loading = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            var page = await Provider.QueryAsync(BuildQuery(0, Virtualized ? 0 : NonVirtualizedMaxItems));
            var timeline = ShowTimeline
                ? await Provider.GetTimelineAsync(BuildQuery(0, 0), Math.Max(1, TimelineBucketCount))
                : (IReadOnlyList<AuditLogTimelineBucket>)[];

            if (generation != _refreshGeneration)
            {
                return;
            }

            _totalCount = page.TotalCount;
            _plainItems = Virtualized ? [] : [.. page.Items];
            _timeline = timeline;
            _timelineMaxCount = timeline.Count == 0 ? 0 : timeline.Max(b => b.Count);

            if (_virtualize is not null)
            {
                await _virtualize.RefreshDataAsync();
            }
        }
        catch
        {
            if (generation == _refreshGeneration)
            {
                _totalCount = 0;
                _plainItems = [];
                _timeline = [];
                _timelineMaxCount = 0;
            }
        }
        finally
        {
            if (generation == _refreshGeneration)
            {
                _loading = false;
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async ValueTask<ItemsProviderResult<AuditLogEntry>> ProvideItemsAsync(ItemsProviderRequest request)
    {
        try
        {
            var page = await Provider.QueryAsync(BuildQuery(request.StartIndex, request.Count), request.CancellationToken);
            var total = (int)Math.Min(int.MaxValue, page.TotalCount);
            return new ItemsProviderResult<AuditLogEntry>(page.Items, total);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new ItemsProviderResult<AuditLogEntry>([], 0);
        }
    }

    // ── Filter handlers ──────────────────────────────────────────────────────

    private Task HandleSearchChangedAsync(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? string.Empty;
        return RefreshAsync();
    }

    private Task HandleActorChangedAsync(ChangeEventArgs e)
    {
        _actorFilter = e.Value?.ToString() ?? string.Empty;
        return RefreshAsync();
    }

    private Task HandleActionChangedAsync(ChangeEventArgs e)
    {
        _actionFilter = e.Value?.ToString() ?? string.Empty;
        return RefreshAsync();
    }

    private Task HandleEntityTypeChangedAsync(ChangeEventArgs e)
    {
        _entityTypeFilter = e.Value?.ToString() ?? string.Empty;
        return RefreshAsync();
    }

    private Task HandleFromChangedAsync(ChangeEventArgs e)
    {
        _from = ParseDate(e.Value?.ToString());
        return RefreshAsync();
    }

    private Task HandleToChangedAsync(ChangeEventArgs e)
    {
        var date = ParseDate(e.Value?.ToString());
        _to = date?.AddDays(1).AddTicks(-1);
        return RefreshAsync();
    }

    private static DateTimeOffset? ParseDate(string? value)
        => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? new DateTimeOffset(date, TimeSpan.Zero)
            : null;

    private string FromInputValue => _from?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private string ToInputValue => _to?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private Task ClearFiltersAsync()
    {
        _search = string.Empty;
        _actorFilter = string.Empty;
        _actionFilter = string.Empty;
        _entityTypeFilter = string.Empty;
        _from = null;
        _to = null;
        return RefreshAsync();
    }

    private Task HandleBucketClickedAsync(AuditLogTimelineBucket bucket)
    {
        // Bucket ends are exclusive while the query To is inclusive.
        _from = bucket.Start;
        _to = bucket.End.AddTicks(-1);
        return RefreshAsync();
    }

    // ── Detail ───────────────────────────────────────────────────────────────

    private async Task ToggleDetailAsync(AuditLogEntry entry)
    {
        if (string.Equals(_expandedEntryId, entry.Id, StringComparison.Ordinal))
        {
            _expandedEntryId = null;
        }
        else
        {
            _expandedEntryId = entry.Id;
            await OnEntrySelected.InvokeAsync(entry);
        }

        await InvokeAsync(StateHasChanged);
    }

    private bool IsExpanded(AuditLogEntry entry)
        => string.Equals(_expandedEntryId, entry.Id, StringComparison.Ordinal);

    // ── CSV export ───────────────────────────────────────────────────────────

    /// <summary>Exports the filtered result set as CSV and triggers a browser download.</summary>
    public async Task ExportCsvAsync()
    {
        if (_exporting)
        {
            return;
        }

        _exporting = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            var rows = new List<IReadOnlyList<string?>>();
            const int batchSize = 2000;
            var skip = 0;
            while (rows.Count < ExportMaxRows)
            {
                var take = Math.Min(batchSize, ExportMaxRows - rows.Count);
                var page = await Provider.QueryAsync(BuildQuery(skip, take));
                foreach (var entry in page.Items)
                {
                    rows.Add(BuildCsvRow(entry));
                }

                skip += page.Items.Count;

                // A provider may cap its page size below the requested take, so a short
                // page is not the end of the set — only an empty page or the total is.
                if (page.Items.Count == 0 || skip >= page.TotalCount)
                {
                    break;
                }
            }

            var data = new DataTableExportData
            {
                Name = "audit-log",
                Headers =
                [
                    Loc["TmAuditLogViewer_ColumnTimestamp"],
                    Loc["TmAuditLogViewer_ColumnActor"],
                    Loc["TmAuditLogViewer_ColumnActorId"],
                    Loc["TmAuditLogViewer_ColumnAction"],
                    Loc["TmAuditLogViewer_ColumnEntityType"],
                    Loc["TmAuditLogViewer_ColumnEntityId"],
                    Loc["TmAuditLogViewer_ColumnSeverity"],
                    Loc["TmAuditLogViewer_ColumnDescription"],
                    Loc["TmAuditLogViewer_ColumnChanges"],
                    Loc["TmAuditLogViewer_ColumnIpAddress"]
                ],
                Rows = rows
            };

            var bytes = new CsvDataTableExporter().Export(data);
            try
            {
                await JS.InvokeVoidAsync("tmDataTable.downloadFile", ExportFileName, "text/csv", Convert.ToBase64String(bytes));
            }
            catch { /* JS unavailable (e.g. tests) */ }
        }
        finally
        {
            _exporting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static IReadOnlyList<string?> BuildCsvRow(AuditLogEntry entry)
        =>
        [
            entry.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            entry.ActorName,
            entry.ActorId,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.Severity.ToString(),
            entry.Description,
            string.Join("; ", entry.Changes.Select(c => $"{c.Property}: {c.OldValue ?? "—"} → {c.NewValue ?? "—"}")),
            entry.IpAddress
        ];

    // ── Display helpers ──────────────────────────────────────────────────────

    private string ActionDisplay(AuditLogEntry entry)
        => string.IsNullOrEmpty(entry.ActionLabel) ? entry.Action : entry.ActionLabel!;

    private string EntityDisplay(AuditLogEntry entry)
        => string.IsNullOrEmpty(entry.EntityLabel)
            ? (string.IsNullOrEmpty(entry.EntityId) ? entry.EntityType : $"{entry.EntityType} · {entry.EntityId}")
            : entry.EntityLabel!;

    private static string FormatTimestamp(DateTimeOffset value)
        => value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private string SeverityClass(AuditLogEntry entry)
        => entry.Severity switch
        {
            AuditLogSeverity.Critical => "tm-audit-log__severity tm-audit-log__severity--critical",
            AuditLogSeverity.Warning => "tm-audit-log__severity tm-audit-log__severity--warning",
            _ => "tm-audit-log__severity tm-audit-log__severity--info"
        };

    private string SeverityLabel(AuditLogEntry entry)
        => entry.Severity switch
        {
            AuditLogSeverity.Critical => Loc["TmAuditLogViewer_SeverityCritical"],
            AuditLogSeverity.Warning => Loc["TmAuditLogViewer_SeverityWarning"],
            _ => Loc["TmAuditLogViewer_SeverityInfo"]
        };

    private string IntegrityClass()
    {
        var baseClass = "tm-audit-log__integrity";
        if (_integrityLoading)
        {
            return baseClass + " tm-audit-log__integrity--checking";
        }

        return _integrity?.Status switch
        {
            AuditLogIntegrityStatus.Verified => baseClass + " tm-audit-log__integrity--verified",
            AuditLogIntegrityStatus.Failed => baseClass + " tm-audit-log__integrity--failed",
            _ => baseClass + " tm-audit-log__integrity--unknown"
        };
    }

    private string IntegrityText()
    {
        if (_integrityLoading)
        {
            return Loc["TmAuditLogViewer_IntegrityChecking"];
        }

        return _integrity?.Status switch
        {
            AuditLogIntegrityStatus.Verified => Loc["TmAuditLogViewer_IntegrityVerified"],
            AuditLogIntegrityStatus.Failed => Loc["TmAuditLogViewer_IntegrityFailed"],
            _ => Loc["TmAuditLogViewer_IntegrityUnknown"]
        };
    }

    private string IntegrityTitle()
        => _integrity?.Status == AuditLogIntegrityStatus.Failed && !string.IsNullOrEmpty(_integrity.FirstInvalidEntryId)
            ? string.Format(CultureInfo.CurrentCulture, Loc["TmAuditLogViewer_IntegrityFailedAt"], _integrity.FirstInvalidEntryId)
            : IntegrityText();

    private long _timelineMaxCount;

    private double BucketHeightPercent(AuditLogTimelineBucket bucket)
    {
        if (_timelineMaxCount <= 0 || bucket.Count <= 0)
        {
            return 0;
        }

        return Math.Max(4, bucket.Count * 100.0 / _timelineMaxCount);
    }

    private string BucketTitle(AuditLogTimelineBucket bucket)
        => string.Format(
            CultureInfo.CurrentCulture,
            Loc["TmAuditLogViewer_TimelineBucketTitle"],
            bucket.Start.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            bucket.End.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            bucket.Count);
}
