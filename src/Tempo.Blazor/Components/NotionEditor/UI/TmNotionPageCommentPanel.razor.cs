using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Models;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionPageCommentPanel : ComponentBase, IDisposable
{
    private const string CurrentUserId = "demo";

    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CommentNotificationOrchestrator? NotificationOrchestrator { get; set; }

    // ── Cascaded ─────────────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public string PageId   { get; set; } = string.Empty;
    [Parameter] public bool   Expanded { get; set; }

    [Parameter] public EventCallback<bool> OnExpandedChanged { get; set; }

    [Parameter] public EventCallback OnCountChanged { get; set; }

    [Parameter] public EventCallback<string> OnMentionClicked { get; set; }

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
    private bool   _showDeleteConfirm;
    private IBlockComment?       _pendingDeleteComment;
    private INotionCommentEntry? _pendingDeleteEntry;

    private Guid?  _replyingToEntryId;
    private string _inlineReplyText = string.Empty;
    private IBlockComment? _inlineReplyComment;

    private bool   _initialized;
    private ElementReference _panelRef;
    private DotNetObjectReference<TmNotionPageCommentPanel>? _dotNetRef;

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
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("tmNotionEditor.initMentionClickHandler", _panelRef, _dotNetRef); } catch { /* best-effort */ }
        }
    }

    [JSInvokable("OnMentionClicked")]
    public void HandleMentionClicked(string userId)
    {
        _ = OnMentionClicked.InvokeAsync(userId);
    }

    public void Dispose()
    {
        _dotNetRef?.Dispose();
        try { JS.InvokeVoidAsync("tmNotionEditor.destroyMentionClickHandler", _panelRef); } catch { }
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
            StateHasChanged();
            await OnCountChanged.InvokeAsync();
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
            var rawText = _newCommentText.Trim();
            var encoded = await CommentMentionHelper.EncodeAsync(rawText, Context.MentionProvider);

            await Context.CommentProvider.AddPageCommentAsync(PageId, encoded);

            // Re-load to get the created entry for notification
            await LoadAsync();
            var comment = _comments.LastOrDefault(c => c.BlockId.ToString() == PageId);
            var entry = comment?.Thread.LastOrDefault();
            if (entry is not null && comment is not null)
                await CommentMentionHelper.NotifyAsync(rawText, entry, comment.Id.ToString(), PageId, Context.MentionProvider, NotificationOrchestrator);

            _newCommentText  = string.Empty;
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
            var rawText = _replyText.Trim();
            var encoded = await CommentMentionHelper.EncodeAsync(rawText, Context.MentionProvider);

            await Context.CommentProvider.ReplyToCommentAsync(_replyingToCommentId.ToString()!, encoded);

            var comment = _comments.FirstOrDefault(c => c.Id == _replyingToCommentId);
            var entry = comment?.Thread.LastOrDefault();
            if (entry is not null && comment is not null)
                await CommentMentionHelper.NotifyAsync(rawText, entry, comment.Id.ToString(), PageId, Context.MentionProvider, NotificationOrchestrator);

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
        else if (e.Key == "Escape")
            CancelInlineReply();
    }

    // ── Inline reply to entry ─────────────────────────────────────────────────

    private void HandleEntryKeyDownAsync(KeyboardEventArgs e, INotionCommentEntry entry, IBlockComment comment)
    {
        if (e.Key == "Enter" && _replyingToEntryId != entry.Id)
        {
            StartReplyToEntry(entry, comment);
        }
        else if (e.Key == "Escape" && _replyingToEntryId == entry.Id)
        {
            CancelInlineReply();
        }
        else if (e.Key == "Escape" && _editingEntryId == entry.Id)
        {
            CancelEdit();
        }
    }

    private void StartReplyToEntry(INotionCommentEntry entry, IBlockComment comment)
    {
        _replyingToEntryId  = entry.Id;
        _inlineReplyComment = comment;
        _inlineReplyText    = QuoteReply(entry);
        StateHasChanged();
    }

    private void CancelInlineReply()
    {
        _replyingToEntryId  = null;
        _inlineReplyComment = null;
        _inlineReplyText    = string.Empty;
    }

    private async Task SendInlineReplyAsync()
    {
        if (Context.CommentProvider is null || _replyingToEntryId is null ||
            _inlineReplyComment is null || string.IsNullOrWhiteSpace(_inlineReplyText) || _submitting)
            return;

        _submitting = true;
        _error      = string.Empty;
        StateHasChanged();

        try
        {
            var rawText = _inlineReplyText.Trim();
            var encoded = await CommentMentionHelper.EncodeAsync(rawText, Context.MentionProvider);

            await Context.CommentProvider.ReplyToCommentAsync(
                _inlineReplyComment.Id.ToString(),
                encoded,
                _replyingToEntryId.ToString());

            var entry = _inlineReplyComment.Thread.LastOrDefault();
            if (entry is not null)
                await CommentMentionHelper.NotifyAsync(rawText, entry, _inlineReplyComment.Id.ToString(), PageId, Context.MentionProvider, NotificationOrchestrator);

            _replyingToEntryId  = null;
            _inlineReplyComment = null;
            _inlineReplyText    = string.Empty;
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

    private async Task HandleInlineReplyKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && (e.CtrlKey || e.MetaKey))
            await SendInlineReplyAsync();
        else if (e.Key == "Escape")
            CancelInlineReply();
    }

    // ── Mark all as read ──────────────────────────────────────────────────────

    private async Task MarkAllAsReadAsync()
    {
        if (Context.CommentProvider is null || string.IsNullOrEmpty(PageId)) return;
        _error = string.Empty;
        try
        {
            await Context.CommentProvider.MarkAllThreadsAsReadAsync(PageId, "demo");
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
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
            var rawText = _editText.Trim();
            var encoded = await CommentMentionHelper.EncodeAsync(rawText, Context.MentionProvider);
            await Context.CommentProvider.EditCommentAsync(_editingEntryId.ToString()!, encoded);
            _editingEntryId = null;
            _editText       = string.Empty;
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

    private void DeleteEntryAsync(IBlockComment comment, INotionCommentEntry entry)
    {
        if (Context.CommentProvider is null) return;
        _pendingDeleteComment = comment;
        _pendingDeleteEntry   = entry;
        _showDeleteConfirm    = true;
        StateHasChanged();
    }

    private async Task HandleDeleteConfirmResult(bool? result)
    {
        _showDeleteConfirm = false;
        if (result != true || _pendingDeleteComment is null)
        {
            _pendingDeleteComment = null;
            _pendingDeleteEntry   = null;
            return;
        }

        _error = string.Empty;
        try
        {
            var isOnlyEntry = _pendingDeleteComment.Thread.Count <= 1;
            if (isOnlyEntry)
            {
                await Context.CommentProvider.DeleteCommentAsync(_pendingDeleteComment.Id.ToString());
            }
            else
            {
                await Context.CommentProvider.DeleteCommentEntryAsync(_pendingDeleteEntry?.Id.ToString()!);
            }
            await LoadAsync();
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
        finally
        {
            _pendingDeleteComment = null;
            _pendingDeleteEntry   = null;
        }
    }

    // ── Subscribe / Unsubscribe ───────────────────────────────────────────────

    private static bool IsSubscribed(IBlockComment comment)
        => comment.SubscribedUserIds.Contains(CurrentUserId);

    private async Task SubscribeAsync(IBlockComment comment)
    {
        if (Context.CommentProvider is null) return;
        _error = string.Empty;
        try
        {
            await Context.CommentProvider.SubscribeToThreadAsync(comment.Id.ToString(), CurrentUserId);
            if (comment is BlockComment bc && !bc.SubscribedUserIds.Contains(CurrentUserId))
                bc.SubscribedUserIds.Add(CurrentUserId);
            else if (comment is Tempo.Blazor.NotionEditor.Models.BlockComment bc2 && !bc2.SubscribedUserIds.Contains(CurrentUserId))
                bc2.SubscribedUserIds.Add(CurrentUserId);
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

    private async Task UnsubscribeAsync(IBlockComment comment)
    {
        if (Context.CommentProvider is null) return;
        _error = string.Empty;
        try
        {
            await Context.CommentProvider.UnsubscribeFromThreadAsync(comment.Id.ToString(), CurrentUserId);
            if (comment is BlockComment bc)
                bc.SubscribedUserIds.Remove(CurrentUserId);
            else if (comment is Tempo.Blazor.NotionEditor.Models.BlockComment bc2)
                bc2.SubscribedUserIds.Remove(CurrentUserId);
        }
        catch
        {
            _error = Loc["TmNotionPageComment_ActionError"];
        }
    }

    private static IEnumerable<CommentThreadNode> FlattenTree(List<CommentThreadNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenTree(node.Children))
                yield return child;
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

    private static string QuoteReply(INotionCommentEntry entry)
    {
        var text = StripHtml(entry.HtmlContent).Trim();
        if (text.Length > 120) text = text[..120] + "…";
        return $"> {entry.AuthorDisplayName}: \"{text}\"\n\n";
    }
}
