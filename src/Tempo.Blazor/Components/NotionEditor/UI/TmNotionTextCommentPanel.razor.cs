using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionTextCommentPanel : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded ─────────────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool   Visible         { get; set; }
    [Parameter] public string CommentId       { get; set; } = string.Empty;
    [Parameter] public string BlockId         { get; set; } = string.Empty;
    [Parameter] public int    StartOffset     { get; set; }
    [Parameter] public int    EndOffset       { get; set; }
    [Parameter] public string HighlightedText { get; set; } = string.Empty;
    [Parameter] public double Top             { get; set; }
    [Parameter] public double Left            { get; set; }

    [Parameter] public EventCallback          OnClosed        { get; set; }
    [Parameter] public EventCallback          OnResolved      { get; set; }
    [Parameter] public EventCallback<int>     OnCountChanged  { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private IBlockComment? _comment;
    private bool           _loading;
    private bool           _submitting;
    private string         _replyText  = string.Empty;
    private string         _error      = string.Empty;
    private double         _top;
    private double         _left;
    private bool           _wasVisible;
    private string         _activeCommentId = string.Empty;

    private Guid?          _editingEntryId;
    private string         _editText = string.Empty;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (Visible && !_wasVisible)
        {
            _top              = Top;
            _left             = Left;
            _replyText        = string.Empty;
            _error            = string.Empty;
            _editingEntryId   = null;
            _activeCommentId  = CommentId;
            await LoadAsync();
        }

        if (!Visible && _wasVisible)
            _comment = null;

        _wasVisible = Visible;
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        if (Context.CommentProvider is null) return;

        _loading = true;
        _error   = string.Empty;
        StateHasChanged();

        try
        {
            if (!string.IsNullOrEmpty(_activeCommentId))
            {
                var list = await Context.CommentProvider.GetBlockCommentsAsync(BlockId);
                _comment = list.FirstOrDefault(c => c.Id.ToString() == _activeCommentId);
            }
        }
        catch
        {
            _error = Loc["TmNotionTextComment_LoadError"];
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SendReplyAsync()
    {
        if (Context.CommentProvider is null || string.IsNullOrWhiteSpace(_replyText) || _submitting)
            return;

        _submitting = true;
        _error      = string.Empty;
        StateHasChanged();

        try
        {
            if (_comment is null)
            {
                _comment = await Context.CommentProvider.AddTextAnchorCommentAsync(
                    BlockId, StartOffset, EndOffset, HighlightedText, _replyText.Trim());
                _activeCommentId = _comment.Id.ToString();
            }
            else
            {
                await Context.CommentProvider.ReplyToCommentAsync(_comment.Id.ToString(), _replyText.Trim());
            }

            _replyText = string.Empty;
            await LoadAsync();
            await OnCountChanged.InvokeAsync(_comment?.Thread.Count ?? 0);
        }
        catch
        {
            _error = Loc["TmNotionTextComment_SendError"];
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task HandleReplyKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && (e.CtrlKey || e.MetaKey))
            await SendReplyAsync();
    }

    private async Task ResolveAsync()
    {
        if (Context.CommentProvider is null || _comment is null) return;
        _error = string.Empty;
        try
        {
            _comment = await Context.CommentProvider.ResolveCommentAsync(_comment.Id.ToString());
            await OnResolved.InvokeAsync();
            await OnCountChanged.InvokeAsync(0);
        }
        catch
        {
            _error = Loc["TmNotionTextComment_ActionError"];
        }
    }

    private async Task UnresolveAsync()
    {
        if (Context.CommentProvider is null || _comment is null) return;
        _error = string.Empty;
        try
        {
            _comment = await Context.CommentProvider.UnresolveCommentAsync(_comment.Id.ToString());
            await OnCountChanged.InvokeAsync(_comment.Thread.Count);
        }
        catch
        {
            _error = Loc["TmNotionTextComment_ActionError"];
        }
    }

    private void StartEdit(INotionCommentEntry entry)
    {
        _editingEntryId = entry.Id;
        _editText       = StripHtml(entry.HtmlContent);
        StateHasChanged();
    }

    private void CancelEdit()
    {
        _editingEntryId = null;
        _editText       = string.Empty;
    }

    private async Task SaveEditAsync()
    {
        if (Context.CommentProvider is null || _editingEntryId is null || string.IsNullOrWhiteSpace(_editText))
            return;

        _error = string.Empty;
        try
        {
            await Context.CommentProvider.EditCommentAsync(_editingEntryId.ToString()!, _editText.Trim());
            _editingEntryId = null;
            _editText       = string.Empty;
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionTextComment_ActionError"];
        }
    }

    private async Task DeleteEntryAsync(INotionCommentEntry entry)
    {
        if (Context.CommentProvider is null || _comment is null) return;
        var confirmed = await JS.InvokeAsync<bool>("confirm", Loc["TmNotionTextComment_DeleteConfirm"]);
        if (!confirmed) return;

        _error = string.Empty;
        try
        {
            await Context.CommentProvider.DeleteCommentAsync(_comment.Id.ToString());
            _comment = null;
            _activeCommentId = string.Empty;
            await OnCountChanged.InvokeAsync(0);
        }
        catch
        {
            _error = Loc["TmNotionTextComment_ActionError"];
        }
    }

    private async Task CloseAsync() => await OnClosed.InvokeAsync();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string AvatarInitial(string name)
        => name.Length > 0 ? name[0].ToString().ToUpperInvariant() : "?";

    private static string FormatTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt.ToUniversalTime();
        if (diff.TotalMinutes < 1)  return "just now";
        if (diff.TotalHours   < 1)  return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays    < 1)  return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("MMM d, yyyy");
    }

    private static string TruncateText(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..maxLen] + "…";

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", string.Empty);
    }
}
