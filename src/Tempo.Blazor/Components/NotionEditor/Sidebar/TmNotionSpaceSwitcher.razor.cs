using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Sidebar;

public partial class TmNotionSpaceSwitcher : ComponentBase
{
    private readonly string _listId = $"tm-ns-space-list-{Guid.NewGuid():N}";
    private IReadOnlyList<NotionSpaceDto> _spaces = [];
    private readonly Dictionary<string, IReadOnlyList<INotionPage>> _overviewPages = new(StringComparer.OrdinalIgnoreCase);
    private NotionSpaceDto? _selectedSpace;
    private bool _isLoading = true;
    private bool _listOpen;
    private bool _overviewOpen;
    private bool _isMoving;
    private string? _loadError;

    /// <summary>Optional provider that supplies spaces and cross-space page moves.</summary>
    [Parameter] public INotionSpaceProvider? SpaceProvider { get; set; }

    /// <summary>Currently selected space id. Null means the provider default or implicit space.</summary>
    [Parameter] public string? SelectedSpaceId { get; set; }

    /// <summary>Raised when the selected space id changes.</summary>
    [Parameter] public EventCallback<string?> SelectedSpaceIdChanged { get; set; }

    /// <summary>Currently opened page id used by overview move actions.</summary>
    [Parameter] public string? CurrentPageId { get; set; }

    /// <summary>Raised after the provider moves the current page to a different space.</summary>
    [Parameter] public EventCallback<string> OnCurrentPageMoved { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadSpacesAsync();
    }

    protected override void OnParametersSet()
    {
        ResolveSelectedSpace();
    }

    private async Task LoadSpacesAsync()
    {
        _isLoading = true;
        _loadError = null;

        try
        {
            _spaces = SpaceProvider is null
                ? [CreateImplicitSpace()]
                : await SpaceProvider.GetSpacesAsync();

            if (_spaces.Count == 0)
                _spaces = [CreateImplicitSpace()];

            ResolveSelectedSpace();
            if (_overviewOpen)
                await LoadOverviewPagesAsync();
        }
        catch
        {
            _loadError = Loc["Notion_Generic_LoadError"];
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void ToggleList() => _listOpen = !_listOpen;

    private async Task ToggleOverviewAsync()
    {
        _overviewOpen = !_overviewOpen;
        if (_overviewOpen)
            await LoadOverviewPagesAsync();
    }

    private async Task SelectSpaceAsync(NotionSpaceDto space)
    {
        _selectedSpace = space;
        _listOpen = false;
        await SelectedSpaceIdChanged.InvokeAsync(space.Id);
    }

    private async Task MoveCurrentPageAsync(string targetSpaceId)
    {
        if (SpaceProvider is null || string.IsNullOrWhiteSpace(CurrentPageId) || _isMoving)
            return;

        _isMoving = true;
        _loadError = null;

        try
        {
            await SpaceProvider.MovePageToSpaceAsync(CurrentPageId, targetSpaceId);
            SelectedSpaceId = targetSpaceId;
            ResolveSelectedSpace();
            _overviewPages.Clear();
            await LoadOverviewPagesAsync();
            await SelectedSpaceIdChanged.InvokeAsync(targetSpaceId);
            await OnCurrentPageMoved.InvokeAsync(targetSpaceId);
        }
        catch
        {
            _loadError = Loc["Notion_Generic_ActionError"];
        }
        finally
        {
            _isMoving = false;
        }
    }

    private async Task LoadOverviewPagesAsync()
    {
        if (SpaceProvider is null)
            return;

        foreach (var space in _spaces)
        {
            _overviewPages[space.Id] = await SpaceProvider.GetPagesInSpaceAsync(space.Id);
        }
    }

    private void ResolveSelectedSpace()
    {
        _selectedSpace = _spaces.FirstOrDefault(space =>
            string.Equals(space.Id, SelectedSpaceId, StringComparison.OrdinalIgnoreCase))
            ?? _spaces.FirstOrDefault();
    }

    private IReadOnlyList<INotionPage> GetOverviewPages(string spaceId)
        => _overviewPages.TryGetValue(spaceId, out var pages) ? pages : [];

    private bool IsSelected(NotionSpaceDto space)
        => _selectedSpace is not null && string.Equals(_selectedSpace.Id, space.Id, StringComparison.OrdinalIgnoreCase);

    private string GetIcon(NotionSpaceDto? space)
        => string.IsNullOrWhiteSpace(space?.IconEmoji) ? "S" : space.IconEmoji;

    private string GetName(NotionSpaceDto? space)
        => string.IsNullOrWhiteSpace(space?.Name) ? Loc["Notion_Space_Default"] : space.Name;

    private string GetTypeLabel(NotionSpaceType? type)
        => type switch
        {
            NotionSpaceType.Personal => Loc["Notion_Space_Personal"],
            NotionSpaceType.Public => Loc["Notion_Space_Public"],
            _ => Loc["Notion_Space_Team"]
        };

    private string GetPageIcon(INotionPage page)
        => string.IsNullOrWhiteSpace(page.IconEmoji) ? "P" : page.IconEmoji;

    private string GetPageTitle(INotionPage page)
        => string.IsNullOrWhiteSpace(page.Title) ? Loc["TmNotionSidebar_Untitled"] : page.Title;

    private NotionSpaceDto CreateImplicitSpace() => new()
    {
        Id = "default",
        Key = "DEFAULT",
        Name = Loc["Notion_Space_Default"],
        Type = NotionSpaceType.Team
    };
}
