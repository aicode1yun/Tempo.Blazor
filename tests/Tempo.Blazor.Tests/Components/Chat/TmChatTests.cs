using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Chat;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Chat;

public class TmChatTests : LocalizationTestBase
{
    private static readonly ChatUser _currentUser = new("u1", "Alice", avatar: "A", isOnline: true);
    private static readonly ChatUser _otherUser = new("u2", "Bob", avatar: "B", isOnline: true);

    // ── CHAT-5: render zobrazí messages ─────────────────────────────────────

    [Fact]
    public void Render_WithMessages_DisplaysMessageList()
    {
        var messages = new[]
        {
            new ChatMessage("m1", "Hello", _otherUser, ChatMessageType.Incoming),
            new ChatMessage("m2", "Hi there", _currentUser, ChatMessageType.Outgoing),
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, messages)
                      .Add(p => p.CurrentUser, _currentUser));

        cut.FindAll(".tm-chat__message").Count.Should().Be(2);
    }

    [Fact]
    public void Render_EmptyMessages_ShowsEmptyState()
    {
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser));

        cut.FindAll(".tm-chat__message").Should().BeEmpty();
        cut.Find(".tm-chat__empty").Should().NotBeNull();
    }

    // ── CHAT-6: sender/receiver mají různé styly ────────────────────────────

    [Fact]
    public void Render_IncomingMessage_HasIncomingClass()
    {
        var messages = new[]
        {
            new ChatMessage("m1", "Hello", _otherUser, ChatMessageType.Incoming),
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, messages)
                      .Add(p => p.CurrentUser, _currentUser));

        var msg = cut.Find(".tm-chat__message");
        msg.ClassList.Should().Contain("tm-chat__message--incoming");
        msg.TextContent.Should().Contain("Hello");
        msg.TextContent.Should().Contain("Bob");
    }

    [Fact]
    public void Render_OutgoingMessage_HasOutgoingClass()
    {
        var messages = new[]
        {
            new ChatMessage("m1", "Hi", _currentUser, ChatMessageType.Outgoing),
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, messages)
                      .Add(p => p.CurrentUser, _currentUser));

        var msg = cut.Find(".tm-chat__message");
        msg.ClassList.Should().Contain("tm-chat__message--outgoing");
        msg.TextContent.Should().Contain("Hi");
    }

    [Fact]
    public void Render_SystemMessage_HasSystemClass()
    {
        var messages = new[]
        {
            new ChatMessage("m1", "Bob joined the conversation", type: ChatMessageType.System),
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, messages)
                      .Add(p => p.CurrentUser, _currentUser));

        var msg = cut.Find(".tm-chat__message");
        msg.ClassList.Should().Contain("tm-chat__message--system");
        msg.TextContent.Should().Contain("Bob joined");
    }

    [Fact]
    public void Render_OutgoingMessage_WithoutAuthor_InfersFromCurrentUser()
    {
        var messages = new[]
        {
            new ChatMessage("m1", "Hi", type: ChatMessageType.Outgoing),
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, messages)
                      .Add(p => p.CurrentUser, _currentUser));

        var msg = cut.Find(".tm-chat__message");
        msg.ClassList.Should().Contain("tm-chat__message--outgoing");
        msg.TextContent.Should().Contain("Alice");
    }

    // ── CHAT-7: input + send tlačítko vyvolá OnSendMessage ──────────────────

    [Fact]
    public void TypeMessage_ClickSend_FiresOnSendMessage()
    {
        string? sentText = null;
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser)
                      .Add(p => p.OnSendMessage, EventCallback.Factory.Create<string>(this, t => sentText = t)));

        var input = cut.Find(".tm-chat__input");
        input.Input("Hello world");
        cut.Render();

        var sendBtn = cut.Find(".tm-chat__send-btn");
        sendBtn.Click();

        sentText.Should().Be("Hello world");
    }

    [Fact]
    public void EmptyInput_SendButtonDisabled()
    {
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser));

        var sendBtn = cut.Find(".tm-chat__send-btn");
        sendBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void DisabledInput_CannotSend()
    {
        string? sentText = null;
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser)
                      .Add(p => p.Disabled, true)
                      .Add(p => p.OnSendMessage, EventCallback.Factory.Create<string>(this, t => sentText = t)));

        var input = cut.Find(".tm-chat__input");
        input.HasAttribute("disabled").Should().BeTrue();

        var sendBtn = cut.Find(".tm-chat__send-btn");
        sendBtn.HasAttribute("disabled").Should().BeTrue();
    }

    // ── CHAT-8: typing indicator se zobrazí ─────────────────────────────────

    [Fact]
    public void Render_WithTypingUsers_DisplaysTypingIndicator()
    {
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser)
                      .Add(p => p.TypingUsers, new[] { _otherUser }));

        var indicator = cut.Find(".tm-chat__typing-indicator");
        indicator.Should().NotBeNull();
        indicator.TextContent.Should().Contain("Bob");
    }

    [Fact]
    public void Render_WithoutTypingUsers_HidesIndicator()
    {
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser));

        cut.FindAll(".tm-chat__typing-indicator").Should().BeEmpty();
    }

    [Fact]
    public void Render_MultipleTypingUsers_ShowsAllNames()
    {
        var carol = new ChatUser("u3", "Carol", avatar: "C");
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser)
                      .Add(p => p.TypingUsers, new[] { _otherUser, carol }));

        var indicator = cut.Find(".tm-chat__typing-indicator");
        indicator.TextContent.Should().Contain("Bob");
        indicator.TextContent.Should().Contain("Carol");
    }

    // ── CHAT-9: přílohy se renderují ────────────────────────────────────────

    [Fact]
    public void Render_MessageWithAttachments_DisplaysAttachments()
    {
        var msg = new ChatMessage("m1", "See attached", _otherUser, ChatMessageType.Incoming)
        {
            Attachments = new[]
            {
                new ChatAttachment("a1", "report.pdf", "/files/report.pdf", "application/pdf", 10240),
            }
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, new[] { msg })
                      .Add(p => p.CurrentUser, _currentUser));

        var attachments = cut.FindAll(".tm-chat__attachment");
        attachments.Count.Should().Be(1);
        attachments[0].TextContent.Should().Contain("report.pdf");
    }

    [Fact]
    public void ClickAttachment_FiresOnAttachmentClick()
    {
        ChatAttachment? clicked = null;
        var attachment = new ChatAttachment("a1", "doc.pdf", "/files/doc.pdf", "application/pdf");
        var msg = new ChatMessage("m1", "File", _otherUser, ChatMessageType.Incoming)
        {
            Attachments = new[] { attachment }
        };

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, new[] { msg })
                      .Add(p => p.CurrentUser, _currentUser)
                      .Add(p => p.OnAttachmentClick, EventCallback.Factory.Create<ChatAttachment>(this, a => clicked = a)));

        cut.Find(".tm-chat__attachment").Click();

        clicked.Should().NotBeNull();
        clicked!.Id.Should().Be("a1");
    }

    // ── Extra: timestamp rendering ──────────────────────────────────────────

    [Fact]
    public void Render_Message_DisplaysTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 2, 10, 30, 0, TimeSpan.Zero);
        var msg = new ChatMessage("m1", "Hello", _otherUser, ChatMessageType.Incoming, ts);

        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.Messages, new[] { msg })
                      .Add(p => p.CurrentUser, _currentUser));

        var timeEl = cut.Find(".tm-chat__message-time");
        timeEl.Should().NotBeNull();
    }

    [Fact]
    public void Render_WithCustomClass_AppliesClass()
    {
        var cut = Render<TmChat>(parameters =>
            parameters.Add(p => p.CurrentUser, _currentUser)
                      .Add(p => p.Class, "my-chat"));

        cut.Find(".tm-chat.my-chat").Should().NotBeNull();
    }
}
