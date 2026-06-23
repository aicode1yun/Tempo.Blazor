using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionMediaUploadDialog : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext? Context { get; set; }

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool   IsOpen    { get; set; }

    /// <summary>"image" | "video" | "audio" | "pdf" | "file"</summary>
    [Parameter] public string MediaType { get; set; } = "file";

    [Parameter] public EventCallback<(string? FileId, string? Url)> OnConfirmed { get; set; }
    [Parameter] public EventCallback                                  OnCancelled { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private string  _activeTab   = "upload";
    private bool    _isDragging;
    private bool    _isUploading;
    private string? _uploadError;
    private string  _embedUrl    = string.Empty;

    private bool _focusTrapInitialized;
    private bool _wasOpen;
    private ElementReference _dialogRef;
    private ElementReference _urlInputRef;

    // ── Library state ─────────────────────────────────────────────────────────

    private List<INotionMediaLibraryItem> _libraryItems     = [];
    private string                        _libraryQuery     = string.Empty;
    private bool                          _isLoadingLibrary;
    private string?                       _libraryError;
    private int                           _librarySkip;
    private bool                          _libraryHasMore;
    private bool                          _libraryLoaded;
    private CancellationTokenSource?      _libraryCts;

    private const int LibraryPageSize = 24;

    // ── Computed ─────────────────────────────────────────────────────────────

    private bool CanUpload         => Context?.FileProvider         is not null;
    private bool CanBrowseLibrary  => Context?.MediaLibraryProvider is not null;

    private string _dialogTitle => MediaType switch
    {
        "image" => Loc["TmNotionMediaUploadDialog_TitleImage"],
        "video" => Loc["TmNotionMediaUploadDialog_TitleVideo"],
        "audio" => Loc["TmNotionMediaUploadDialog_TitleAudio"],
        "pdf"   => Loc["TmNotionMediaUploadDialog_TitlePdf"],
        _       => Loc["TmNotionMediaUploadDialog_TitleFile"]
    };

    private string _acceptTypes => MediaType switch
    {
        "image" => "image/*",
        "video" => "video/*",
        "audio" => "audio/*",
        "pdf"   => "application/pdf",
        _       => "*/*"
    };

    private string? _libraryMediaType => MediaType switch
    {
        "image" => "image",
        "pdf"   => "pdf",
        "file"  => "file",
        _       => null
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            _wasOpen = false;
            _focusTrapInitialized = false;
            ResetLibraryState();
            return;
        }

        if (_wasOpen)
        {
            return;
        }

        _wasOpen      = true;
        _uploadError  = null;
        _embedUrl     = string.Empty;
        _isDragging   = false;
        _activeTab    = CanUpload ? "upload" : CanBrowseLibrary ? "library" : "embed";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen && !_focusTrapInitialized)
        {
            _focusTrapInitialized = true;
            try { await JS.InvokeVoidAsync("tmNotionEditor.initFocusTrap", _dialogRef); }
            catch { }
        }

        if (IsOpen && _activeTab == "embed")
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.focus", _urlInputRef); }
            catch { }
        }

        if (IsOpen && _activeTab == "library" && !_libraryLoaded && !_isLoadingLibrary)
        {
            await LoadLibraryAsync(reset: true);
        }
    }

    private void SetTab(string tab)
    {
        _activeTab = tab;
        if (tab == "library" && !_libraryLoaded && !_isLoadingLibrary)
            _ = LoadLibraryAsync(reset: true);
    }

    private void ResetLibraryState()
    {
        _libraryItems     = [];
        _libraryQuery     = string.Empty;
        _librarySkip      = 0;
        _libraryHasMore   = false;
        _libraryLoaded    = false;
        _libraryError     = null;
        _libraryCts?.Cancel();
        _libraryCts?.Dispose();
        _libraryCts = null;
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    private async Task HandleFileSelectedAsync(InputFileChangeEventArgs e)
    {
        if (Context?.FileProvider is null) return;
        var file = e.File;
        var validationKey = NotionMediaUploadValidation.Validate(MediaType, file.Name, file.ContentType, file.Size);
        if (!string.IsNullOrEmpty(validationKey))
        {
            _uploadError = Loc[validationKey, NotionMediaUploadValidation.FormatMaxFileSize()];
            _isUploading = false;
            StateHasChanged();
            return;
        }

        _isUploading = true;
        _uploadError = null;
        StateHasChanged();
        try
        {
            await using var stream = file.OpenReadStream(maxAllowedSize: NotionMediaUploadValidation.MaxFileSizeBytes);
            var media = await Context.FileProvider.UploadNotionFileAsync(stream, file.Name, file.ContentType);
            await OnConfirmed.InvokeAsync((media.AssetId, media.Url));
        }
        catch
        {
            _uploadError = Loc["TmNotionMediaUploadDialog_UploadError"];
        }
        finally
        {
            _isUploading = false;
            StateHasChanged();
        }
    }

    // ── Embed ─────────────────────────────────────────────────────────────────

    private async Task HandleEmbedConfirmAsync()
    {
        var url = _embedUrl.Trim();
        if (string.IsNullOrWhiteSpace(url)) return;
        await OnConfirmed.InvokeAsync((null, url));
    }

    private async Task HandleUrlKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")  await HandleEmbedConfirmAsync();
        if (e.Key == "Escape") await HandleCancelAsync();
    }

    private async Task HandleDialogKeyDownAsync(KeyboardEventArgs e)
    {
        if (string.Equals(e.Key, "Escape", StringComparison.Ordinal))
            await HandleCancelAsync();
    }

    private async Task HandleCancelAsync() => await OnCancelled.InvokeAsync();

    // ── Library ───────────────────────────────────────────────────────────────

    private async Task OnLibrarySearchChangedAsync(ChangeEventArgs e)
    {
        _libraryQuery = e.Value?.ToString() ?? string.Empty;
        await LoadLibraryAsync(reset: true);
    }

    private async Task LoadLibraryAsync(bool reset)
    {
        if (Context?.MediaLibraryProvider is null) return;

        _libraryCts?.Cancel();
        _libraryCts?.Dispose();
        _libraryCts = new CancellationTokenSource();
        var ct = _libraryCts.Token;

        if (reset)
        {
            _libraryItems   = [];
            _librarySkip    = 0;
            _libraryHasMore = false;
        }

        _isLoadingLibrary = true;
        _libraryError     = null;
        StateHasChanged();

        try
        {
            if (reset)
                await Task.Delay(300, ct); // debounce on search change

            var items = await Context.MediaLibraryProvider.SearchAsync(
                _libraryQuery, _libraryMediaType, _librarySkip, LibraryPageSize, ct);

            var list = items.ToList();
            _libraryItems.AddRange(list);
            _librarySkip   += list.Count;
            _libraryHasMore = list.Count == LibraryPageSize;
            _libraryLoaded  = true;
        }
        catch (OperationCanceledException) { return; }
        catch
        {
            _libraryError = Loc["TmNotionMediaUploadDialog_LibraryError"];
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _isLoadingLibrary = false;
                StateHasChanged();
            }
        }
    }

    private async Task HandleLibraryItemSelectedAsync(INotionMediaLibraryItem item)
        => await OnConfirmed.InvokeAsync((item.Id, item.Url));

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _libraryCts?.Cancel();
        _libraryCts?.Dispose();

        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.destroyFocusTrap", _dialogRef);
        }
        catch
        {
        }
    }
}
