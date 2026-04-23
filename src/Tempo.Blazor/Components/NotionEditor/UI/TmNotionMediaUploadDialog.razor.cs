using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionMediaUploadDialog : ComponentBase
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

    private ElementReference _urlInputRef;

    // ── Computed ─────────────────────────────────────────────────────────────

    private bool CanUpload => Context?.FileProvider is not null;

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

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (!IsOpen) return;
        _uploadError = null;
        _embedUrl    = string.Empty;
        _isDragging  = false;
        _activeTab   = CanUpload ? "upload" : "embed";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen && _activeTab == "embed")
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.focus", _urlInputRef); }
            catch { }
        }
    }

    private void SetTab(string tab) => _activeTab = tab;

    // ── Upload ────────────────────────────────────────────────────────────────

    private async Task HandleFileSelectedAsync(InputFileChangeEventArgs e)
    {
        if (Context?.FileProvider is null) return;
        _isUploading = true;
        _uploadError = null;
        StateHasChanged();
        try
        {
            var file = e.File;
            await using var stream = file.OpenReadStream(maxAllowedSize: 104_857_600);
            var fileId = await Context.FileProvider.UploadFileAsync(stream, file.Name, file.ContentType);
            var url    = await Context.FileProvider.GetFileUrlAsync(fileId);
            await OnConfirmed.InvokeAsync((fileId, url));
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

    private async Task HandleCancelAsync() => await OnCancelled.InvokeAsync();
}
