using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Page;

public partial class TmNotionPageReactions : ComponentBase
{
    /// <summary>Canonical page-like reaction value.</summary>
    public const string LikeReaction = PageReactionDto.LikeReaction;

    private static readonly string[] DefaultAvailableReactions = ["👍", "🎉", "❤️", "👀", "✅"];

    [CascadingParameter] internal NotionEditorContext Context { get; set; } = default!;

    /// <summary>Page identifier for reaction aggregation.</summary>
    [Parameter, EditorRequired]
    public Guid PageId { get; set; }

    /// <summary>Available emoji reactions shown by the picker.</summary>
    [Parameter]
    public IReadOnlyList<string> AvailableReactions { get; set; } = DefaultAvailableReactions;

    private IReadOnlyList<PageReactionDto> _reactions = [];
    private Guid _loadedPageId;
    private bool _busy;
    private bool _pickerOpen;

    private string CurrentUserId => string.IsNullOrWhiteSpace(Context.CurrentUserId) ? "anonymous" : Context.CurrentUserId;

    private int LikeCount => _reactions.FirstOrDefault(item => item.Reaction == LikeReaction)?.Count ?? 0;

    private IEnumerable<PageReactionDto> VisibleEmojiReactions => _reactions
        .Where(item => item.Reaction != LikeReaction && item.Count > 0)
        .OrderByDescending(item => IsActive(item.Reaction))
        .ThenByDescending(item => item.Count)
        .ThenBy(item => item.Reaction, StringComparer.Ordinal);

    private string LikeClass => IsActive(LikeReaction)
        ? "tm-page-reactions__like tm-page-reactions__like--active"
        : "tm-page-reactions__like";

    protected override async Task OnParametersSetAsync()
    {
        if (Context.ReactionProvider is null)
            return;

        if (_loadedPageId == PageId)
            return;

        _loadedPageId = PageId;
        await LoadReactionsAsync();
    }

    private async Task LoadReactionsAsync()
    {
        if (Context.ReactionProvider is null)
            return;

        try
        {
            _reactions = await Context.ReactionProvider.GetReactionsAsync(PageId);
        }
        catch
        {
            _reactions = [];
        }
    }

    private bool IsActive(string reaction)
    {
        var users = _reactions.FirstOrDefault(item => item.Reaction == reaction)?.UserIds;
        return users is not null && users.Contains(CurrentUserId, StringComparer.OrdinalIgnoreCase);
    }

    private string ReactionClass(PageReactionDto reaction)
        => IsActive(reaction.Reaction)
            ? "tm-page-reactions__pill tm-page-reactions__pill--active"
            : "tm-page-reactions__pill";

    private void TogglePicker()
    {
        if (_busy)
            return;

        _pickerOpen = !_pickerOpen;
    }

    private async Task ToggleLikeAsync()
    {
        if (Context.ReactionProvider is null || _busy)
            return;

        _busy = true;
        try
        {
            _reactions = await Context.ReactionProvider.ToggleLikeAsync(PageId, CurrentUserId);
        }
        catch
        {
            await LoadReactionsAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ToggleReactionAsync(string reaction)
    {
        if (Context.ReactionProvider is null || _busy || string.IsNullOrWhiteSpace(reaction))
            return;

        _busy = true;
        _pickerOpen = false;
        try
        {
            _reactions = await Context.ReactionProvider.ToggleReactionAsync(PageId, reaction, CurrentUserId);
        }
        catch
        {
            await LoadReactionsAsync();
        }
        finally
        {
            _busy = false;
        }
    }
}
