using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionPageCommentSection
{
    /// <summary>Page identifier whose page-level comment threads are displayed.</summary>
    [Parameter] public string PageId { get; set; } = string.Empty;

    /// <summary>Whether the page comment section is expanded.</summary>
    [Parameter] public bool Expanded { get; set; }

    /// <summary>Raised when the expanded state changes.</summary>
    [Parameter] public EventCallback<bool> OnExpandedChanged { get; set; }

    /// <summary>Raised after the comment count may have changed.</summary>
    [Parameter] public EventCallback OnCountChanged { get; set; }

    /// <summary>Raised when a user mention inside a comment is activated.</summary>
    [Parameter] public EventCallback<string> OnMentionClicked { get; set; }
}
