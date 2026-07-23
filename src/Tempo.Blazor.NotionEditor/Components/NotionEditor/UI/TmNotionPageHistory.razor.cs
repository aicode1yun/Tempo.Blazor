using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionPageHistory : TmComponentBase, IAsyncDisposable
{
    private enum HistoryView { Empty, Preview, Diff }

    // ── Cascaded ─────────────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool   Visible { get; set; }
    [Parameter] public string PageId  { get; set; } = string.Empty;

    [Parameter] public EventCallback          OnClosed   { get; set; }
    [Parameter] public EventCallback<string>  OnRestored { get; set; }

    // ── Constants ─────────────────────────────────────────────────────────────

    private const int PageSize = 20;

    // ── State ─────────────────────────────────────────────────────────────────

    private HistoryView             _view               = HistoryView.Empty;
    private IReadOnlyList<IPageVersion> _versions       = [];
    private int                     _totalCount;
    private int                     _page               = 1;
    private bool                    _versionsLoading;
    private bool                    _previewLoading;
    private bool                    _diffLoading;
    private bool                    _restoring;

    private IPageVersion?           _selectedVersion;
    private IPageVersion?           _compareFromVersion;
    private IPageVersion?           _compareToVersion;
    private IReadOnlyList<BlockDiff> _diffs             = [];
    private NotionDiffViewMode      _diffViewMode       = NotionDiffViewMode.Inline;

    private bool                    _compareMode;
    private bool                    _showRestoreConfirm;
    private bool                    _wasVisible;

    private string                  _error              = string.Empty;
    private string                  _success            = string.Empty;
    private CancellationTokenSource _toastCts           = new();

    // ── Computed ──────────────────────────────────────────────────────────────

    private int TotalPages => _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PageSize) : 1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (Visible && !_wasVisible)
        {
            _page               = 1;
            _selectedVersion    = null;
            _compareFromVersion = null;
            _compareToVersion   = null;
            _compareMode        = false;
            _diffViewMode       = NotionDiffViewMode.Inline;
            _view               = HistoryView.Empty;
            _error              = string.Empty;
            _success            = string.Empty;
            _diffs              = [];
            await LoadVersionsAsync();
        }

        if (!Visible && _wasVisible)
            Reset();

        _wasVisible = Visible;
    }

    private void Reset()
    {
        _versions           = [];
        _selectedVersion    = null;
        _compareFromVersion = null;
        _compareToVersion   = null;
        _compareMode        = false;
        _view               = HistoryView.Empty;
        _diffs              = [];
    }

    // ── Load versions ──────────────────────────────────────────────────────────

    private async Task LoadVersionsAsync()
    {
        if (Context.HistoryProvider is null || string.IsNullOrEmpty(PageId)) return;

        _versionsLoading = true;
        _error = string.Empty;
        StateHasChanged();

        try
        {
            var result = await Context.HistoryProvider.GetVersionsAsync(PageId, _page, PageSize);
            _versions    = result.Items;
            _totalCount  = result.TotalCount;
        }
        catch
        {
            await ShowErrorAsync(Loc["TmNotionPageHistory_ErrorLoadVersions"]);
        }
        finally
        {
            _versionsLoading = false;
        }
    }

    // ── Select version ────────────────────────────────────────────────────────

    private async Task SelectVersionAsync(IPageVersion version)
    {
        if (_compareMode)
        {
            await SelectForCompareAsync(version);
            return;
        }

        if (_selectedVersion?.Id == version.Id && _view == HistoryView.Preview) return;

        _view            = HistoryView.Preview;
        _selectedVersion = version;

        if (version.BlocksSnapshot.Count == 0)
        {
            _previewLoading = true;
            StateHasChanged();
            try
            {
                var full = await Context.HistoryProvider!.GetVersionAsync(PageId, version.Id.ToString());
                _selectedVersion = full;
            }
            catch
            {
                await ShowErrorAsync(Loc["TmNotionPageHistory_ErrorLoadPreview"]);
            }
            finally
            {
                _previewLoading = false;
            }
        }
    }

    private async Task HandleVersionKeyDownAsync(KeyboardEventArgs e, IPageVersion version)
    {
        if (e.Key is "Enter" or " ")
            await SelectVersionAsync(version);
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    private async Task PreviousPageAsync()
    {
        if (_page <= 1) return;
        _page--;
        _selectedVersion = null;
        _view            = HistoryView.Empty;
        await LoadVersionsAsync();
    }

    private async Task NextPageAsync()
    {
        if (_page >= TotalPages) return;
        _page++;
        _selectedVersion = null;
        _view            = HistoryView.Empty;
        await LoadVersionsAsync();
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    private void RestoreAsync() => _showRestoreConfirm = true;

    private void CancelRestore() => _showRestoreConfirm = false;

    private async Task ConfirmRestoreAsync()
    {
        if (Context.HistoryProvider is null || _selectedVersion is null) return;

        _showRestoreConfirm = false;
        _restoring          = true;
        _error              = string.Empty;
        StateHasChanged();

        try
        {
            await Context.HistoryProvider.RestoreVersionAsync(PageId, _selectedVersion.Id.ToString());
            await ShowSuccessAsync(Loc["TmNotionPageHistory_RestoreSuccess"]);
            await OnRestored.InvokeAsync(PageId);
            await CloseAsync();
        }
        catch
        {
            await ShowErrorAsync(Loc["TmNotionPageHistory_ErrorRestore"]);
        }
        finally
        {
            _restoring = false;
        }
    }

    // ── Compare ───────────────────────────────────────────────────────────────

    private void StartCompare()
    {
        if (_selectedVersion is null) return;
        _compareMode        = true;
        _compareFromVersion = _selectedVersion;
        _compareToVersion   = null;
    }

    private void CancelCompare()
    {
        _compareMode        = false;
        _compareFromVersion = null;
        _compareToVersion   = null;
        _view               = _selectedVersion is not null ? HistoryView.Preview : HistoryView.Empty;
    }

    private async Task SelectForCompareAsync(IPageVersion version)
    {
        if (_compareFromVersion is null)
        {
            _compareFromVersion = version;
            return;
        }

        if (_compareFromVersion.Id == version.Id) return;

        _compareToVersion = version;
        _compareMode      = false;
        _diffViewMode     = NotionDiffViewMode.Inline;
        await LoadDiffAsync();
    }

    private async Task LoadDiffAsync()
    {
        if (Context.HistoryProvider is null || _compareFromVersion is null || _compareToVersion is null)
            return;

        _view       = HistoryView.Diff;
        _diffLoading = true;
        _error      = string.Empty;
        StateHasChanged();

        try
        {
            var diffs = await Context.HistoryProvider.CompareVersionsAsync(
                _compareFromVersion.Id.ToString(),
                _compareToVersion.Id.ToString());
            _diffs = diffs.ToList();
        }
        catch
        {
            await ShowErrorAsync(Loc["TmNotionPageHistory_ErrorLoadDiff"]);
            _view = HistoryView.Preview;
        }
        finally
        {
            _diffLoading = false;
        }
    }

    private void ExitDiff()
    {
        _view               = _selectedVersion is not null ? HistoryView.Preview : HistoryView.Empty;
        _compareFromVersion = null;
        _compareToVersion   = null;
        _diffs              = [];
        _diffViewMode       = NotionDiffViewMode.Inline;
    }

    private void SetDiffViewMode(NotionDiffViewMode viewMode)
    {
        _diffViewMode = viewMode;
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    private async Task CloseAsync() => await OnClosed.InvokeAsync();

    // ── Toast helpers ─────────────────────────────────────────────────────────

    private async Task ShowErrorAsync(string message)
    {
        _error   = message;
        _success = string.Empty;
        StateHasChanged();
        await AutoDismissToastAsync();
    }

    private async Task ShowSuccessAsync(string message)
    {
        _success = message;
        _error   = string.Empty;
        StateHasChanged();
        await AutoDismissToastAsync();
    }

    private async Task AutoDismissToastAsync()
    {
        _toastCts.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;
        try
        {
            await Task.Delay(4000, token);
            _error   = string.Empty;
            _success = string.Empty;
            StateHasChanged();
        }
        catch (OperationCanceledException) { /* replaced by new toast */ }
    }

    // ── Block content helpers ─────────────────────────────────────────────────

    private static string GetBlockHtml(IPageBlock block)
    {
        if (block.Content is ITextBlockContent text)
            return string.IsNullOrEmpty(text.Html)
                ? $"<em>({block.Type})</em>"
                : NotionHtmlSanitizer.SanitizeBlockContent(text.Html);

        return $"<span>[{block.Type}]</span>";
    }

    private string GetBlockTypeLabel(IPageBlock? block)
    {
        if (block is null) return string.Empty;
        return block.Type switch
        {
            BlockType.Paragraph     => Loc["TmNotionPageHistory_BlockParagraph"],
            BlockType.Heading1      => Loc["TmNotionPageHistory_BlockH1"],
            BlockType.Heading2      => Loc["TmNotionPageHistory_BlockH2"],
            BlockType.Heading3      => Loc["TmNotionPageHistory_BlockH3"],
            BlockType.Quote         => Loc["TmNotionPageHistory_BlockQuote"],
            BlockType.Callout       => Loc["TmNotionPageHistory_BlockCallout"],
            BlockType.Code          => Loc["TmNotionPageHistory_BlockCode"],
            BlockType.BulletList    => Loc["TmNotionPageHistory_BlockBullet"],
            BlockType.NumberedList  => Loc["TmNotionPageHistory_BlockNumbered"],
            BlockType.TodoItem      => Loc["TmNotionPageHistory_BlockTodo"],
            BlockType.Toggle        => Loc["TmNotionPageHistory_BlockToggle"],
            BlockType.Image         => Loc["TmNotionPageHistory_BlockImage"],
            BlockType.Table         => Loc["TmNotionPageHistory_BlockTable"],
            BlockType.Divider       => Loc["TmNotionPageHistory_BlockDivider"],
            _                       => block.Type.ToString()
        };
    }

    private string GetDiffLabel(BlockDiffType type) => type switch
    {
        BlockDiffType.Added    => Loc["TmNotionPageHistory_DiffAdded"],
        BlockDiffType.Removed  => Loc["TmNotionPageHistory_DiffRemoved"],
        BlockDiffType.Modified => Loc["TmNotionPageHistory_DiffModified"],
        BlockDiffType.Moved    => Loc["TmNotionPageHistory_DiffMoved"],
        _                      => type.ToString()
    };

    private static string GetDiffSymbol(BlockDiffType type) => type switch
    {
        BlockDiffType.Added    => "+",
        BlockDiffType.Removed  => "−",
        BlockDiffType.Modified => "~",
        BlockDiffType.Moved    => "↕",
        _                      => "?"
    };

    // ── Time formatting ───────────────────────────────────────────────────────

    private static string FormatVersionTime(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var today = DateTime.Today;

        if (local.Date == today)
            return $"Today {local:h:mm tt}";
        if (local.Date == today.AddDays(-1))
            return $"Yesterday {local:h:mm tt}";
        if ((today - local.Date).TotalDays < 7)
            return local.ToString("ddd h:mm tt");
        if (local.Year == today.Year)
            return local.ToString("MMM d, h:mm tt");

        return local.ToString("MMM d yyyy, h:mm tt");
    }

    private static string AvatarInitial(string name)
        => name.Length > 0 ? name[0].ToString().ToUpperInvariant() : "?";

    // ── Dispose ───────────────────────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        _toastCts.Cancel();
        _toastCts.Dispose();
        return ValueTask.CompletedTask;
    }
}
