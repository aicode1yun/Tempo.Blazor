namespace Tempo.Blazor.Components.NotionEditor.Services;

using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

/// <summary>
/// Rebuilds a block around new content. Block contents are immutable to their consumers, so any
/// edit produces a fresh <see cref="PageBlock"/> carrying a fresh content instance.
/// </summary>
internal static class NotionBlockContentFactory
{
    /// <summary>Clones the block with a different HTML body, keeping every other typed field.</summary>
    public static PageBlock WithHtml(IPageBlock source, string html)
    {
        var content = source.Content switch
        {
            IHeadingBlockContent hc => (IBlockContent)new HeadingBlockContent
            {
                Html = html, Level = hc.Level, IsToggleable = hc.IsToggleable,
                BackgroundColor = hc.BackgroundColor, TextColor = hc.TextColor, Alignment = hc.Alignment
            },
            ICalloutBlockContent cc => new CalloutBlockContent
            {
                Html = html, IconEmoji = cc.IconEmoji, IconImageUrl = cc.IconImageUrl,
                Variant = cc.Variant,
                BackgroundColor = cc.BackgroundColor, TextColor = cc.TextColor, Alignment = cc.Alignment
            },
            IListBlockContent lc => new ListBlockContent
            {
                Html = html, IndentLevel = lc.IndentLevel,
                BackgroundColor = lc.BackgroundColor, TextColor = lc.TextColor, Alignment = lc.Alignment
            },
            ITodoBlockContent tc => new TodoBlockContent
            {
                Html = html, IsChecked = tc.IsChecked,
                AssigneeId = tc.AssigneeId,
                AssigneeDisplayName = tc.AssigneeDisplayName,
                DueDate = tc.DueDate,
                IsOverdue = IsTodoOverdue(tc.DueDate, tc.IsChecked),
                BackgroundColor = tc.BackgroundColor, TextColor = tc.TextColor, Alignment = tc.Alignment
            },
            IToggleBlockContent tg => new ToggleBlockContent
            {
                Html = html, IsOpen = tg.IsOpen,
                BackgroundColor = tg.BackgroundColor, TextColor = tg.TextColor, Alignment = tg.Alignment
            },
            ITextBlockContent tc => new TextBlockContent
            {
                Html = html,
                BackgroundColor = tc.BackgroundColor, TextColor = tc.TextColor, Alignment = tc.Alignment
            },
            _ => source.Content
        };
        return WithContent(source, content);
    }

    /// <summary>Clones the block with a different content instance and a fresh edit timestamp.</summary>
    public static PageBlock WithContent(IPageBlock source, IBlockContent content) => new()
    {
        Id            = source.Id,
        PageId        = source.PageId,
        ParentBlockId = source.ParentBlockId,
        Type          = source.Type,
        Order         = source.Order,
        Content       = content,
        CreatedAt     = source.CreatedAt,
        LastEditedAt  = DateTime.UtcNow
    };

    /// <summary>Reads the HTML body of any block content that has one.</summary>
    public static string? HtmlOf(IBlockContent? content) => content switch
    {
        ITextBlockContent text => text.Html,
        _                      => null
    };

    private static bool IsTodoOverdue(DateTime? dueDate, bool isChecked) =>
        !isChecked && dueDate is DateTime date && date.Date < DateTime.Today;
}
