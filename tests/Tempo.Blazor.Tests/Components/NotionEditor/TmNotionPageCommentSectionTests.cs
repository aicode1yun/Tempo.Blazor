using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionPageCommentSectionTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("cf100000-0000-0000-0000-000000000001");

    public TmNotionPageCommentSectionTests()
    {
        Services.AddSingleton<INotificationService, NoOpNotificationService>();
        Services.AddScoped<CommentNotificationOrchestrator>();
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["Tm_Delete"] = "Delete",
            ["Tm_Cancel"] = "Cancel",
            ["TmCommentReactions_Label"] = "Reactions",
            ["TmCommentReactions_Add"] = "Add reaction",
            ["TmCommentReactions_You"] = "You",
            ["TmCommentReactions_More"] = "+{0} more",
            ["Notion_PageComments_Title"] = "Page comments",
            ["Notion_PageComments_Add"] = "Comment",
            ["Notion_PageComments_Placeholder"] = "Add a comment to this page",
            ["Notion_PageComments_Empty"] = "No comments yet",
            ["Notion_PageComments_Reply"] = "Reply",
            ["TmNotionPageComment_PanelLabel"] = "Page comments",
            ["TmNotionPageComment_Toggle"] = "Comments",
            ["TmNotionPageComment_BadgeLabel"] = "{0} unresolved comments",
            ["TmNotionPageComment_Loading"] = "Loading comments",
            ["TmNotionPageComment_NoComments"] = "No comments yet",
            ["TmNotionPageComment_Thread"] = "Comment threads",
            ["TmNotionPageComment_Resolve"] = "Resolve",
            ["TmNotionPageComment_Unresolve"] = "Re-open",
            ["TmNotionPageComment_Resolved"] = "Resolved",
            ["TmNotionPageComment_Edited"] = "edited",
            ["TmNotionPageComment_Edit"] = "Edit",
            ["TmNotionPageComment_Delete"] = "Delete",
            ["TmNotionPageComment_EditLabel"] = "Edit comment",
            ["TmNotionPageComment_Save"] = "Save",
            ["TmNotionPageComment_Cancel"] = "Cancel",
            ["TmNotionPageComment_ReplyTrigger"] = "Reply",
            ["TmNotionPageComment_ReplyPlaceholder"] = "Reply to this thread",
            ["TmNotionPageComment_ReplyLabel"] = "Write a reply",
            ["TmNotionPageComment_ReplyHint"] = "Ctrl+Enter to send",
            ["TmNotionPageComment_Reply"] = "Reply",
            ["TmNotionPageComment_ReplyToThis"] = "Reply to this",
            ["TmNotionPageComment_NewPlaceholder"] = "Add a comment to this page",
            ["TmNotionPageComment_NewLabel"] = "New comment",
            ["TmNotionPageComment_Comment"] = "Comment",
            ["TmNotionPageComment_DeleteConfirm"] = "Delete this comment?",
            ["TmNotionPageComment_LoadError"] = "Could not load comments.",
            ["TmNotionPageComment_SendError"] = "Could not send comment.",
            ["TmNotionPageComment_ActionError"] = "Action failed.",
            ["TmNotionPageComment_MarkAllAsRead"] = "Mark all as read",
            ["TmNotionPageComment_Watch"] = "Watch thread",
            ["TmNotionPageComment_Unwatch"] = "Unwatch",
            ["TmNotionPageComment_Time_JustNow"] = "just now",
            ["TmNotionPageComment_Time_MinutesAgo"] = "{0}m ago",
            ["TmNotionPageComment_Time_HoursAgo"] = "{0}h ago",
            ["TmNotionPageComment_Time_DaysAgo"] = "{0}d ago"
        });
    }

    [Fact]
    public async Task PageCommentSection_AddsRepliesReactsAndResolves()
    {
        var provider = new FakeCommentProvider(PageId.ToString("D"));
        var context = new NotionEditorContext
        {
            DataProvider = default!,
            BlockProvider = default!,
            CommentProvider = provider
        };
        var cut = RenderComponent<PageCommentHost>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.PageId, PageId.ToString("D")));

        await cut.Find(".tm-npcp__toggle").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
            cut.Find(".tm-npcp__status").TextContent.Should().Contain("No comments yet"));

        cut.Find(".tm-npcp__new-comment .tm-npcp__reply-input").Input("First page comment");
        cut.WaitForAssertion(() =>
            cut.Find(".tm-npcp__new-comment .tm-npcp__reply-send").HasAttribute("disabled").Should().BeFalse());
        await cut.Find(".tm-npcp__new-comment .tm-npcp__reply-send").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Find(".tm-npcp__entry-text").TextContent.Should().Contain("First page comment"));
        provider.PageComments.Should().ContainSingle();

        await cut.Find(".tm-npcp__reply-trigger").ClickAsync(new MouseEventArgs());
        cut.Find(".tm-npcp__thread-reply .tm-npcp__reply-input").Input("Thread reply");
        cut.WaitForAssertion(() =>
            cut.Find(".tm-npcp__thread-reply .tm-npcp__reply-send").HasAttribute("disabled").Should().BeFalse());
        await cut.Find(".tm-npcp__thread-reply .tm-npcp__reply-send").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Thread reply"));
        provider.PageComments[0].Thread.Should().HaveCount(2);

        await cut.Find(".tm-comment-reaction--add").ClickAsync(new MouseEventArgs());
        await cut.Find(".tm-comment-reaction-picker__item").ClickAsync(new MouseEventArgs());
        provider.PageComments[0].Thread[0].Reactions.Should().ContainSingle(reaction => reaction.Emoji == "👍");

        await cut.Find(".tm-npcp__thread-action").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
            cut.Find(".tm-npcp__thread").ClassList.Should().Contain("tm-npcp__thread--resolved"));
        provider.PageComments[0].IsResolved.Should().BeTrue();
    }

    public sealed class PageCommentHost : ComponentBase
    {
        [Parameter] public NotionEditorContext Context { get; set; } = default!;
        [Parameter] public string PageId { get; set; } = string.Empty;

        private bool _expanded;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<NotionEditorContext>>(0);
            builder.AddAttribute(1, "Value", Context);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<TmNotionPageCommentSection>(0);
                childBuilder.AddAttribute(1, "PageId", PageId);
                childBuilder.AddAttribute(2, "Expanded", _expanded);
                childBuilder.AddAttribute(3, "OnExpandedChanged", EventCallback.Factory.Create<bool>(this, value =>
                {
                    _expanded = value;
                    StateHasChanged();
                }));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private sealed class FakeCommentProvider : INotionCommentProvider
    {
        private readonly string _pageId;
        private readonly Dictionary<Guid, PageComment> _comments = [];

        public FakeCommentProvider(string pageId)
        {
            _pageId = pageId;
        }

        public List<PageComment> PageComments => _comments.Values.ToList();

        public Task<IEnumerable<IBlockComment>> GetBlockCommentsAsync(string blockId)
            => Task.FromResult<IEnumerable<IBlockComment>>([]);

        public Task<IBlockComment> AddBlockCommentAsync(string blockId, string htmlContent)
            => throw new NotSupportedException();

        public Task<INotionCommentEntry> ReplyToCommentAsync(string commentId, string htmlContent, string? parentEntryId = null)
        {
            var comment = _comments[Guid.Parse(commentId)];
            var entry = Entry(htmlContent, parentEntryId is null ? null : Guid.Parse(parentEntryId));
            ((List<INotionCommentEntry>)comment.Thread).Add(entry);
            comment.LastActivityAt = entry.CreatedAt;
            return Task.FromResult<INotionCommentEntry>(entry);
        }

        public Task<INotionCommentEntry> EditCommentAsync(string commentId, string htmlContent)
            => throw new NotSupportedException();

        public Task DeleteCommentAsync(string commentId)
        {
            _comments.Remove(Guid.Parse(commentId));
            return Task.CompletedTask;
        }

        public Task DeleteCommentEntryAsync(string entryId)
            => Task.CompletedTask;

        public Task<IBlockComment> ResolveCommentAsync(string commentId)
        {
            var comment = _comments[Guid.Parse(commentId)];
            comment.IsResolved = true;
            comment.ResolvedAt = DateTime.UtcNow;
            return Task.FromResult<IBlockComment>(comment);
        }

        public Task<IBlockComment> UnresolveCommentAsync(string commentId)
        {
            var comment = _comments[Guid.Parse(commentId)];
            comment.IsResolved = false;
            comment.ResolvedAt = null;
            return Task.FromResult<IBlockComment>(comment);
        }

        public Task<IBlockComment> AddTextAnchorCommentAsync(
            string blockId,
            int startOffset,
            int endOffset,
            string highlightedText,
            string htmlContent,
            string commentId)
            => throw new NotSupportedException();

        public Task<IEnumerable<IPageComment>> GetPageCommentsAsync(string pageId)
            => Task.FromResult<IEnumerable<IPageComment>>(_comments.Values.Where(comment => comment.PageId == pageId));

        public Task<IPageComment> AddPageCommentAsync(string pageId, string htmlContent)
        {
            var entry = Entry(htmlContent);
            var comment = new PageComment
            {
                Id = Guid.NewGuid(),
                BlockId = Guid.Parse(pageId),
                PageId = pageId,
                Thread = new List<INotionCommentEntry> { entry },
                LastActivityAt = entry.CreatedAt,
                SubscribedUserIds = ["demo"]
            };
            _comments[comment.Id] = comment;
            return Task.FromResult<IPageComment>(comment);
        }

        public Task<int> GetUnresolvedCommentsCountAsync(string pageId)
            => Task.FromResult(_comments.Values.Count(comment => comment.PageId == pageId && !comment.IsResolved));

        public Task MarkThreadAsReadAsync(string commentId, string userId) => Task.CompletedTask;

        public Task MarkThreadAsUnreadAsync(string commentId, string userId) => Task.CompletedTask;

        public Task MarkAllThreadsAsReadAsync(string ownerId, string userId) => Task.CompletedTask;

        public Task<IReadOnlyList<ICommentReaction>> GetReactionsAsync(string entryId)
            => Task.FromResult<IReadOnlyList<ICommentReaction>>([]);

        public Task AddReactionAsync(string entryId, string emoji, string userId)
        {
            var entry = _comments.Values
                .SelectMany(comment => comment.Thread)
                .OfType<NotionCommentEntry>()
                .Single(entry => entry.Id.ToString("D") == entryId);
            var reaction = entry.Reactions.OfType<CommentReaction>().SingleOrDefault(reaction => reaction.Emoji == emoji);
            if (reaction is null)
            {
                reaction = new CommentReaction { Emoji = emoji };
                entry.Reactions.Add(reaction);
            }
            if (!reaction.UserIds.Contains(userId))
            {
                reaction.UserIds.Add(userId);
            }
            return Task.CompletedTask;
        }

        public Task RemoveReactionAsync(string entryId, string emoji, string userId) => Task.CompletedTask;

        public Task SubscribeToThreadAsync(string commentId, string userId) => Task.CompletedTask;

        public Task UnsubscribeFromThreadAsync(string commentId, string userId) => Task.CompletedTask;

        private static NotionCommentEntry Entry(string htmlContent, Guid? parentEntryId = null) => new()
        {
            Id = Guid.NewGuid(),
            ParentEntryId = parentEntryId,
            AuthorUserId = "demo",
            AuthorDisplayName = "Demo User",
            HtmlContent = htmlContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CanEdit = true,
            CanDelete = true,
            Reactions = []
        };
    }
}
