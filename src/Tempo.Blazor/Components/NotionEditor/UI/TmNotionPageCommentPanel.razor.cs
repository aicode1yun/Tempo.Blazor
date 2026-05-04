using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionPageCommentPanel : ComponentBase
{
    // ── Cascaded ─────────────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public string PageId   { get; set; } = string.Empty;
    [Parameter] public bool   Expanded { get; set; }

    [Parameter] public EventCallback<bool> OnExpandedChanged { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private IReadOnlyList<IBlockComment> _comments        = [];
    private int                          _unresolvedCount;
    private bool                         _loading;
    private bool                         _submitting;
    private string                       _error           = string.Empty;

    private string _newCommentText    = string.Empty;
    private string _replyText         = string.Empty;
    private Guid?  _replyingToCommentId;

    private Guid?  _editingEntryId;
    private string _editText = string.Empty;

    private bool   _initialized;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized && !string.IsNullOrEmpty(PageId))
        {
            _initialized = true;
            await LoadUnresolvedCountAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !string.IsNullOrEmpty(PageId))
        {
            await LoadUnresolvedCountAsync();
            StateHasChanged();
        }
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        if (Context.CommentProvider is null || string.IsNullOrEmpty(PageId)) return;

        _loading = true;
        _error   = string.Empty;
        StateHasChanged();

        try
        {
            var list = await Context.CommentProvider.GetPageCommentsAsync(PageId);
            _comments        = list.ToList();
            _unresolvedCount = _comments.Count(c => !c.IsResolved);
        }
        catch
        {
            _error = Loc["TmNotionPageComment_LoadError"];
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadUnresolvedCountAsync()
    {
        if (Context.CommentProvider is null || string.IsNullOrEmpty(PageId)) return;
        try
        {
            _unresolvedCount = await Context.CommentProvider.GetUnresolvedCommentsCountAsync(PageId);
        }
        catch { /* best-effort */ }
    }

    // ── Expand ────────────────────────────────────────────────────────────────

    private async Task ToggleExpandedAsync()
    {
        var next = !Expanded;
        await OnExpandedChanged.InvokeAsync(next);

        if (next && _comments.Count == 0)
            await LoadAsync();
    }

    // ── New comment ───────────────────────────────────────────────────────────

    private async Task SendNewCommentAsync()
    {
        if (Context.CommentProvider is null || string.IsNullOrWhiteSpace(_newCommentText) || _submitting)
            return;

        _submitting = true;
        _error      = string.Empty;
        StateHasChanged();

        try
        {
            await Context.CommentProvider.AddPageCommentAsync(PageId, _newCommentText.Trim());
            _newCommentText  = string.Empty;
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_SendError"];
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task HandleNewKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && (e.CtrlKey || e.MetaKey))
            await SendNewCommentAsync();
    }

    // ── Reply to thread ───────────────────────────────────────────────────────

    private void StartReply(IBlockComment comment)
    {
        _replyingToCommentId = comment.Id;
        _replyText           = string.Empty;
    }

    private void CancelReply()
    {
        _replyingToCommentId = null;
        _replyText           = string.Empty;
    }

    private async Task SendReplyAsync()
    {
        if (Context.CommentProvider is null || _replyingToCommentId is null ||
            string.IsNullOrWhiteSpace(_replyText) || _submitting)
            return;

        _submitting = true;
        _error      = string.Empty;
        StateHasChanged();

        try
        {
            await Context.CommentProvider.ReplyToCommentAsync(_replyingToCommentId.ToString()!, _replyText.Trim());
            _replyingToCommentId = null;
            _replyText           = string.Empty;
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_SendError"];
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

    // ── Resolve / Unresolve ───────────────────────────────────────────────────

    private async Task ResolveAsync(IBlockComment comment)
    {
        if (Context.CommentProvider is null) return;
        _error = string.Empty;
        try
        {
            await Context.CommentProvider.ResolveCommentAsync(comment.Id.ToString());
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

    private async Task UnresolveAsync(IBlockComment comment)
    {
        if (Context.CommentProvider is null) return;
        _error = string.Empty;
        try
        {
            await Context.CommentProvider.UnresolveCommentAsync(comment.Id.ToString());
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

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
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

    private async Task DeleteEntryAsync(IBlockComment comment, INotionCommentEntry entry)
    {
        if (Context.CommentProvider is null) return;
        _error = string.Empty;
        try
        {
            await Context.CommentProvider.DeleteCommentAsync(entry.Id.ToString());
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

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

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", string.Empty);
    }
}
