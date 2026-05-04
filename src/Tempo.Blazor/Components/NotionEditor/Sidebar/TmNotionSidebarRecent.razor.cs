using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Sidebar;

public partial class TmNotionSidebarRecent : ComponentBase
{
    // ── Cascaded context ──────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    [Parameter, EditorRequired] public IReadOnlyList<INotionPage> Pages        { get; set; } = [];
    [Parameter]                 public string?                    ActivePageId  { get; set; }

    // ── Section expand ────────────────────────────────────────────────────────

    private bool _isExpanded = false;

    // ── Computed ──────────────────────────────────────────────────────────────

    private bool IsActive(INotionPage page) =>
        ActivePageId is not null &&
        page.Id.ToString().Equals(ActivePageId, StringComparison.OrdinalIgnoreCase);

    private string GetTitle(INotionPage page) =>
        string.IsNullOrWhiteSpace(page.Title)
            ? Loc["TmNotionSidebar_Untitled"]
            : page.Title;

    private string GetIcon(INotionPage page) =>
        string.IsNullOrEmpty(page.IconEmoji) ? "📄" : page.IconEmoji;

    private string GetRelativeTime(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;

        if (diff.TotalMinutes < 1)
            return Loc["TmNotionSidebarRecent_JustNow"];

        if (diff.TotalHours < 1)
            return string.Format(Loc["TmNotionSidebarRecent_MinutesAgo"], (int)diff.TotalMinutes);

        if (diff.TotalDays < 1)
            return string.Format(Loc["TmNotionSidebarRecent_HoursAgo"], (int)diff.TotalHours);

        return string.Format(Loc["TmNotionSidebarRecent_DaysAgo"], (int)diff.TotalDays);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async Task NavigateAsync(INotionPage page)
    {
        if (Context.NavigateTo is not null)
            await Context.NavigateTo(page.Id.ToString());
    }
}
