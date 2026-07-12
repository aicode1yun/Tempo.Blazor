using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Chat;

/// <summary>
/// A chat conversation component with message rendering, typing indicators, attachments, and send input.
/// </summary>
public partial class TmChat : ComponentBase
{
    /// <summary>The currently authenticated user.</summary>
    [Parameter] public ChatUser CurrentUser { get; set; } = new();

    /// <summary>List of messages to display.</summary>
    [Parameter] public IReadOnlyList<ChatMessage> Messages { get; set; } = [];

    /// <summary>Users currently typing.</summary>
    [Parameter] public IReadOnlyList<ChatUser> TypingUsers { get; set; } = [];

    /// <summary>Placeholder for the message input.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Whether the input is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Fired when the user sends a message (text only). Retained for backward compatibility.</summary>
    [Parameter] public EventCallback<string> OnSendMessage { get; set; }

    /// <summary>Fired when the user sends a message, carrying optional thread/reply context.</summary>
    [Parameter] public EventCallback<ChatSendRequest> OnSend { get; set; }

    /// <summary>Fired when an attachment is clicked.</summary>
    [Parameter] public EventCallback<ChatAttachment> OnAttachmentClick { get; set; }

    // ── K6: threads, edit/delete, reactions, receipts ──

    /// <summary>Fired when the user commits an edit to one of their messages.</summary>
    [Parameter] public EventCallback<ChatMessageEdit> OnEditMessage { get; set; }

    /// <summary>Fired when the user deletes one of their messages.</summary>
    [Parameter] public EventCallback<string> OnDeleteMessage { get; set; }

    /// <summary>Fired when the user toggles an emoji reaction on a message.</summary>
    [Parameter] public EventCallback<ChatReactionToggle> OnToggleReaction { get; set; }

    /// <summary>Fired when the user opens a message's reply thread.</summary>
    [Parameter] public EventCallback<ChatMessage> OnReply { get; set; }

    /// <summary>Fired once per incoming message when it first becomes visible, for read receipts.</summary>
    [Parameter] public EventCallback<string> OnMessageRead { get; set; }

    /// <summary>Id of the thread root whose reply panel is open. Two-way bindable.</summary>
    [Parameter] public string? ActiveThreadRootId { get; set; }

    /// <summary>Fires when the open thread changes.</summary>
    [Parameter] public EventCallback<string?> ActiveThreadRootIdChanged { get; set; }

    /// <summary>When true, replies are grouped into a thread panel instead of shown inline. Default <c>true</c>.</summary>
    [Parameter] public bool EnableThreads { get; set; } = true;

    /// <summary>When true, messages show a reaction bar and emoji picker. Default <c>true</c>.</summary>
    [Parameter] public bool EnableReactions { get; set; } = true;

    /// <summary>When true, the current user can edit and delete their own messages. Default <c>true</c>.</summary>
    [Parameter] public bool AllowEditDelete { get; set; } = true;

    /// <summary>When true, own messages display per-user read receipts. Default <c>true</c>.</summary>
    [Parameter] public bool ShowReadReceipts { get; set; } = true;

    /// <summary>When true, incoming messages fire <see cref="OnMessageRead"/> as they render. Default <c>true</c>.</summary>
    [Parameter] public bool AutoReadReceipts { get; set; } = true;

    /// <summary>Emoji offered by the reaction picker.</summary>
    [Parameter] public IReadOnlyList<string> ReactionEmojis { get; set; } = ["👍", "❤️", "😂", "🎉", "😮", "😢"];

    private string _inputText = string.Empty;
    private ElementReference _messagesContainer;
    private bool _isSubmitting;

    // ── K6 interaction state ──
    private string? _activeThread;                       // internal fallback when not bound
    private string? _editingId;
    private string _editText = string.Empty;
    private string? _pickerForId;
    private string _threadInput = string.Empty;
    private readonly HashSet<string> _reportedRead = [];

    private string? EffectiveActiveThread => ActiveThreadRootId ?? _activeThread;

    private string PlaceholderText => Placeholder ?? Loc["TmChat_Placeholder"];

    private bool IsSendDisabled => Disabled || string.IsNullOrWhiteSpace(_inputText) || _isSubmitting;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || Messages.Count > 0)
        {
            await ScrollToBottomAsync();
        }

        await ReportReadReceiptsAsync();
    }

    private async Task ReportReadReceiptsAsync()
    {
        if (!AutoReadReceipts || !OnMessageRead.HasDelegate) return;

        foreach (var message in Messages)
        {
            if (message.Type == ChatMessageType.System || message.IsDeleted) continue;
            if (IsCurrentUser(message.Author)) continue;
            if (string.IsNullOrEmpty(message.Id) || _reportedRead.Contains(message.Id)) continue;
            if (message.IsReadByUser(CurrentUser.Id)) continue;

            _reportedRead.Add(message.Id);
            await OnMessageRead.InvokeAsync(message.Id);
        }
    }

    private async Task ScrollToBottomAsync()
    {
        await Task.CompletedTask;
    }

    private string GetMessageCssClass(ChatMessage message)
    {
        var baseClass = "tm-chat__message";
        var suffix = message.Type switch
        {
            ChatMessageType.System => "system",
            ChatMessageType.Outgoing => "outgoing",
            _ => IsCurrentUser(message.Author) ? "outgoing" : "incoming"
        };
        return $"{baseClass} {baseClass}--{suffix}";
    }

    private bool IsCurrentUser(ChatUser? user)
    {
        if (user is null) return false;
        return user.Id == CurrentUser.Id;
    }

    private string FormatTimestamp(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        return local.ToString("HH:mm");
    }

    private static string FormatFileSize(long? bytes)
    {
        if (!bytes.HasValue) return "";
        var b = bytes.Value;
        return b switch
        {
            < 1024 => $"{b} B",
            < 1024 * 1024 => $"{b / 1024.0:0.0} KB",
            < 1024 * 1024 * 1024 => $"{b / (1024.0 * 1024.0):0.0} MB",
            _ => $"{b / (1024.0 * 1024.0 * 1024.0):0.0} GB"
        };
    }

    private async Task HandleSendAsync()
    {
        if (IsSendDisabled) return;
        var text = _inputText.Trim();
        _inputText = string.Empty;
        _isSubmitting = true;
        await OnSendMessage.InvokeAsync(text);
        await OnSend.InvokeAsync(new ChatSendRequest(text));
        _isSubmitting = false;
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await HandleSendAsync();
        }
    }

    // ── K6: threads ──

    private bool IsThreadReply(ChatMessage m)
        => EnableThreads && !string.IsNullOrEmpty(m.ThreadRootId) && m.ThreadRootId != m.Id;

    private IEnumerable<ChatMessage> RootMessages()
        => EnableThreads ? Messages.Where(m => !IsThreadReply(m)) : Messages;

    private IReadOnlyList<ChatMessage> RepliesOf(string rootId)
        => Messages.Where(m => m.ThreadRootId == rootId && m.Id != rootId)
                   .OrderBy(m => m.Timestamp)
                   .ToList();

    private int ReplyCountOf(ChatMessage root)
    {
        var counted = RepliesOf(root.Id).Count;
        return counted > 0 ? counted : root.ReplyCount;
    }

    private ChatMessage? ActiveThreadRoot
        => EffectiveActiveThread is null ? null : Messages.FirstOrDefault(m => m.Id == EffectiveActiveThread);

    private async Task OpenThreadAsync(ChatMessage root)
    {
        _activeThread = root.Id;
        _pickerForId = null;
        await ActiveThreadRootIdChanged.InvokeAsync(root.Id);
        await OnReply.InvokeAsync(root);
    }

    private async Task CloseThreadAsync()
    {
        _activeThread = null;
        _threadInput = string.Empty;
        await ActiveThreadRootIdChanged.InvokeAsync(null);
    }

    private async Task SendThreadReplyAsync()
    {
        var root = ActiveThreadRoot;
        if (root is null) return;
        var text = _threadInput.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _threadInput = string.Empty;
        await OnSend.InvokeAsync(new ChatSendRequest(text, replyToId: root.Id, threadRootId: root.Id));
    }

    private async Task HandleThreadKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendThreadReplyAsync();
        }
    }

    // ── K6: edit / delete ──

    private bool CanEditDelete(ChatMessage m)
        => AllowEditDelete && !m.IsDeleted && m.Type != ChatMessageType.System && IsCurrentUser(m.Author);

    // Actions are only offered when their handler is actually wired, so a read-only
    // host (e.g. the basic demo) shows no dead buttons.
    private bool CanReact => EnableReactions && OnToggleReaction.HasDelegate;
    private bool CanReplyInThread => EnableThreads && OnSend.HasDelegate;
    private bool CanEditMessage(ChatMessage m) => CanEditDelete(m) && OnEditMessage.HasDelegate;
    private bool CanDeleteMessage(ChatMessage m) => CanEditDelete(m) && OnDeleteMessage.HasDelegate;
    private bool HasMessageActions(ChatMessage m, bool inThread)
        => CanReact || (!inThread && CanReplyInThread) || CanEditMessage(m) || CanDeleteMessage(m);

    private void StartEdit(ChatMessage m)
    {
        _editingId = m.Id;
        _editText = m.Text;
        _pickerForId = null;
    }

    private void CancelEdit()
    {
        _editingId = null;
        _editText = string.Empty;
    }

    private async Task CommitEditAsync()
    {
        if (_editingId is null) return;
        var id = _editingId;
        var text = _editText.Trim();
        _editingId = null;
        if (!string.IsNullOrEmpty(text))
        {
            await OnEditMessage.InvokeAsync(new ChatMessageEdit(id, text));
        }
    }

    private async Task HandleEditKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await CommitEditAsync();
        }
        else if (e.Key == "Escape")
        {
            CancelEdit();
        }
    }

    private async Task DeleteAsync(ChatMessage m)
    {
        await OnDeleteMessage.InvokeAsync(m.Id);
    }

    // ── K6: reactions ──

    private void TogglePicker(string messageId)
        => _pickerForId = _pickerForId == messageId ? null : messageId;

    private async Task ToggleReactionAsync(string messageId, string emoji)
    {
        _pickerForId = null;
        await OnToggleReaction.InvokeAsync(new ChatReactionToggle(messageId, emoji));
    }

    private string GetReactionChipClass(ChatReaction reaction)
    {
        var cls = "tm-chat__reaction";
        if (reaction.ReactedBy(CurrentUser.Id)) cls += " tm-chat__reaction--mine";
        return cls;
    }

    // ── K6: read receipts ──

    private IReadOnlyList<ChatReadReceipt> OtherReaders(ChatMessage m)
        => m.ReadBy.Where(r => r.User is not null && r.User.Id != CurrentUser.Id).ToList();

    private string GetReceiptsText(ChatMessage m)
    {
        var readers = OtherReaders(m);
        if (readers.Count == 0) return string.Empty;
        var names = string.Join(", ", readers.Select(r => r.User!.Name));
        return string.Format(Loc["TmChat_ReadBy"], names);
    }

    private async Task HandleAttachmentClick(ChatAttachment attachment)
    {
        await OnAttachmentClick.InvokeAsync(attachment);
    }

    private string GetTypingText()
    {
        if (TypingUsers.Count == 0) return string.Empty;
        if (TypingUsers.Count == 1)
            return string.Format(Loc["TmChat_TypingSingle"], TypingUsers[0].Name);
        if (TypingUsers.Count == 2)
            return string.Format(Loc["TmChat_TypingTwo"], TypingUsers[0].Name, TypingUsers[1].Name);
        return string.Format(Loc["TmChat_TypingMany"], TypingUsers[0].Name, TypingUsers.Count - 1);
    }
}
