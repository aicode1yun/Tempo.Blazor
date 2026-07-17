using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Ledger;

/// <summary>
/// Accounting ledger grid: debit/credit/running-balance columns with exact decimal money
/// rendering (TmMoneyDisplay), server-side paging and aggregation through
/// <see cref="ILedgerDataProvider"/>, filter-wide footer totals, and a row-matching
/// workflow (match states, pairing, unpairing) when the provider implements
/// <see cref="ILedgerMatchingProvider"/>.
/// </summary>
public partial class TmLedgerGrid : TmComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Data source of the grid. Required.</summary>
    [Parameter, EditorRequired] public ILedgerDataProvider Provider { get; set; } = default!;

    /// <summary>Rows per page. Default is 50.</summary>
    [Parameter] public int PageSize { get; set; } = 50;

    /// <summary>Whether the search and filter controls are shown. Default is true.</summary>
    [Parameter] public bool ShowFilters { get; set; } = true;

    /// <summary>Whether the footer with filter-wide totals is shown. Default is true.</summary>
    [Parameter] public bool ShowFooter { get; set; } = true;

    /// <summary>Whether the running balance column is shown. Default is true.</summary>
    [Parameter] public bool ShowRunningBalance { get; set; } = true;

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Callback invoked when a row is clicked.</summary>
    [Parameter] public EventCallback<LedgerEntry> OnEntrySelected { get; set; }

    /// <summary>Callback invoked after entries were matched or unmatched.</summary>
    [Parameter] public EventCallback OnMatchingChanged { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ILedgerDataProvider? _loadedProvider;
    private IReadOnlyList<LedgerEntry> _items = [];
    private IReadOnlyList<string> _currencies = [];
    private LedgerAggregates _aggregates = new();
    private decimal _openingBalance;
    private long _totalCount;
    private int _pageIndex;
    private bool _loading;
    private int _loadGeneration;
    private readonly HashSet<string> _selection = new(StringComparer.Ordinal);

    private string _search = string.Empty;
    private LedgerMatchState? _stateFilter;
    private string _currencyFilter = string.Empty;

    private bool SupportsMatching => Provider is ILedgerMatchingProvider;

    private int TotalPages => (int)Math.Max(1, (_totalCount + PageSize - 1) / PageSize);

    private bool CanMatch => _selection.Count >= 2;

    private bool CanUnmatch => _items.Any(e => _selection.Contains(e.Id) && !string.IsNullOrEmpty(e.MatchGroupId));

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(Provider, _loadedProvider))
        {
            _loadedProvider = Provider;
            _search = string.Empty;
            _stateFilter = null;
            _currencyFilter = string.Empty;
            _pageIndex = 0;
            _selection.Clear();

            await LoadCurrenciesAsync();
            await LoadAsync();
        }
    }

    private async Task LoadCurrenciesAsync()
    {
        try
        {
            _currencies = await Provider.GetCurrenciesAsync();
        }
        catch
        {
            _currencies = [];
        }
    }

    private LedgerQuery BuildQuery(int skip, int take)
        => new()
        {
            Skip = skip,
            Take = take,
            SearchText = string.IsNullOrWhiteSpace(_search) ? null : _search,
            MatchState = _stateFilter,
            Currency = string.IsNullOrEmpty(_currencyFilter) ? null : _currencyFilter
        };

    /// <summary>Reloads the current page from the provider.</summary>
    public async Task LoadAsync()
    {
        var generation = ++_loadGeneration;
        _loading = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            var page = await Provider.QueryAsync(BuildQuery(_pageIndex * PageSize, PageSize));
            if (generation != _loadGeneration)
            {
                return;
            }

            _items = page.Items;
            _totalCount = page.TotalCount;
            _aggregates = page.Aggregates;
            _openingBalance = page.OpeningBalance;
        }
        catch
        {
            if (generation == _loadGeneration)
            {
                _items = [];
                _totalCount = 0;
                _aggregates = new LedgerAggregates();
                _openingBalance = 0m;
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _loading = false;
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Filters & paging ─────────────────────────────────────────────────────

    private Task HandleSearchChangedAsync(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? string.Empty;
        _pageIndex = 0;
        // Selected rows may leave the view; matching invisible entries would surprise.
        _selection.Clear();
        return LoadAsync();
    }

    private Task HandleStateFilterChangedAsync(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _stateFilter = Enum.TryParse<LedgerMatchState>(value, out var state) ? state : null;
        _pageIndex = 0;
        _selection.Clear();
        return LoadAsync();
    }

    private Task HandleCurrencyFilterChangedAsync(ChangeEventArgs e)
    {
        _currencyFilter = e.Value?.ToString() ?? string.Empty;
        _pageIndex = 0;
        _selection.Clear();
        return LoadAsync();
    }

    private Task PreviousPageAsync()
    {
        if (_pageIndex <= 0)
        {
            return Task.CompletedTask;
        }

        _pageIndex--;
        _selection.Clear();
        return LoadAsync();
    }

    private Task NextPageAsync()
    {
        if ((_pageIndex + 1L) * PageSize >= _totalCount)
        {
            return Task.CompletedTask;
        }

        _pageIndex++;
        _selection.Clear();
        return LoadAsync();
    }

    // ── Selection & matching ─────────────────────────────────────────────────

    private void ToggleSelection(LedgerEntry entry, ChangeEventArgs e)
    {
        var selected = e.Value is bool b ? b : bool.TryParse(e.Value?.ToString(), out var parsed) && parsed;
        if (selected)
        {
            _selection.Add(entry.Id);
        }
        else
        {
            _selection.Remove(entry.Id);
        }
    }

    private bool IsSelected(LedgerEntry entry) => _selection.Contains(entry.Id);

    private async Task MatchSelectedAsync()
    {
        if (Provider is not ILedgerMatchingProvider matching || !CanMatch)
        {
            return;
        }

        try
        {
            await matching.MatchAsync(_selection.ToList());
            _selection.Clear();
            await LoadAsync();
            await OnMatchingChanged.InvokeAsync();
        }
        catch { }
    }

    private async Task UnmatchSelectedAsync()
    {
        if (Provider is not ILedgerMatchingProvider matching || !CanUnmatch)
        {
            return;
        }

        try
        {
            var groups = _items
                .Where(e => _selection.Contains(e.Id) && !string.IsNullOrEmpty(e.MatchGroupId))
                .Select(e => e.MatchGroupId!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var group in groups)
            {
                await matching.UnmatchAsync(group);
            }

            _selection.Clear();
            await LoadAsync();
            await OnMatchingChanged.InvokeAsync();
        }
        catch { }
    }

    private Task HandleRowClickedAsync(LedgerEntry entry)
        => OnEntrySelected.InvokeAsync(entry);

    // ── Display helpers ──────────────────────────────────────────────────────

    // Cross-currency running sums are meaningless; balances render only when the whole
    // filtered set works in one currency (a currency filter or a single-currency ledger).
    private bool HasSingleCurrency
        => !string.IsNullOrEmpty(_currencyFilter) || _currencies.Count <= 1;

    private IReadOnlyList<(LedgerEntry Entry, decimal? Balance)> RowsWithBalances
    {
        get
        {
            var rows = new List<(LedgerEntry, decimal?)>(_items.Count);
            if (!HasSingleCurrency)
            {
                rows.AddRange(_items.Select(entry => (entry, (decimal?)null)));
                return rows;
            }

            var balance = _openingBalance;
            foreach (var entry in _items)
            {
                balance = MoneyMath.Round(balance + entry.SignedAmount, entry.Currency);
                rows.Add((entry, balance));
            }

            return rows;
        }
    }

    private string FooterCurrency
        => !string.IsNullOrEmpty(_currencyFilter)
            ? _currencyFilter
            : (_currencies.Count == 1 ? _currencies[0] : _items.FirstOrDefault()?.Currency ?? string.Empty);

    private string StateLabel(LedgerMatchState state)
        => state switch
        {
            LedgerMatchState.Matched => Loc["TmLedgerGrid_StateMatched"],
            LedgerMatchState.PartiallyMatched => Loc["TmLedgerGrid_StatePartiallyMatched"],
            LedgerMatchState.Disputed => Loc["TmLedgerGrid_StateDisputed"],
            _ => Loc["TmLedgerGrid_StateUnmatched"]
        };

    private static string StateClass(LedgerMatchState state)
        => "tm-ledger__badge tm-ledger__badge--" + state.ToString().ToLowerInvariant();

    private static string FormatDate(DateOnly date)
        => date.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
}
