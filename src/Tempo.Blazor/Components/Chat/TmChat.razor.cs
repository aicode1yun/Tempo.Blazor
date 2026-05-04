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

    /// <summary>Fired when the user sends a message.</summary>
    [Parameter] public EventCallback<string> OnSendMessage { get; set; }

    /// <summary>Fired when an attachment is clicked.</summary>
    [Parameter] public EventCallback<ChatAttachment> OnAttachmentClick { get; set; }

    private string _inputText = string.Empty;
    private ElementReference _messagesContainer;
    private bool _isSubmitting;

    private string PlaceholderText => Placeholder ?? Loc["TmChat_Placeholder"];

    private bool IsSendDisabled => Disabled || string.IsNullOrWhiteSpace(_inputText) || _isSubmitting;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || Messages.Count > 0)
        {
            await ScrollToBottomAsync();
        }
    }

    private async Task ScrollToBottomAsync()
    {
        // Defer to ensure DOM is updated before scrolling
        await Task.Delay(1);
        StateHasChanged();
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
        _isSubmitting = false;
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await HandleSendAsync();
        }
    }

    private void HandleAttachmentClick(ChatAttachment attachment)
    {
        OnAttachmentClick.InvokeAsync(attachment);
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
