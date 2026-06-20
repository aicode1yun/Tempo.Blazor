using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Embed;

public partial class TmNotionWorkItemBlock : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>Work-item block content.</summary>
    [Parameter] public IWorkItemBlockContent? Content { get; set; }

    /// <summary>Registry of available work-item providers.</summary>
    [Parameter] public WorkItemProviderRegistry? WorkItemProviders { get; set; }

    /// <summary>Whether editing controls are disabled.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised when provider data or block configuration changes.</summary>
    [Parameter] public EventCallback<WorkItemBlockContent> OnContentChanged { get; set; }

    /// <summary>Raised when the block receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    [CascadingParameter] private NotionEditorContext? Context { get; set; }

    private WorkItemDto? _liveItem;
    private string? _loadError;
    private string _selectedProviderKey = string.Empty;
    private string _searchText = string.Empty;
    private string? _pickerError;
    private bool _searching;
    private bool _searched;
    private IReadOnlyList<WorkItemDto> _searchResults = [];
    private string? _lastRefreshKey;

    private WorkItemProviderRegistry? Registry => WorkItemProviders ?? Context?.WorkItemProviders;
    private IReadOnlyList<IWorkItemProvider> Providers => Registry?.GetAll().ToArray() ?? [];
    private WorkItemDto? Snapshot => _liveItem ?? Content?.CachedSnapshot;
    private WorkItemDisplayMode CurrentDisplayMode => Content?.DisplayMode ?? WorkItemDisplayMode.Card;
    private bool NeedsPicker => string.IsNullOrWhiteSpace(Content?.ProviderKey) || string.IsNullOrWhiteSpace(Content?.ExternalId);

    protected override async Task OnParametersSetAsync()
    {
        var providers = Providers;
        if (string.IsNullOrWhiteSpace(_selectedProviderKey))
            _selectedProviderKey = Content?.ProviderKey ?? providers.FirstOrDefault()?.ProviderKey ?? string.Empty;

        if (NeedsPicker)
        {
            _liveItem = null;
            _loadError = null;
            _lastRefreshKey = null;
            return;
        }

        var refreshKey = $"{Content!.ProviderKey}\u001f{Content.ExternalId}";
        if (string.Equals(refreshKey, _lastRefreshKey, StringComparison.Ordinal))
            return;

        _lastRefreshKey = refreshKey;
        await RefreshAsync(updateContent: true);
    }

    private async Task RefreshAsync(bool updateContent)
    {
        if (Content is null || string.IsNullOrWhiteSpace(Content.ProviderKey) || string.IsNullOrWhiteSpace(Content.ExternalId))
            return;

        var provider = Registry?.GetProvider(Content.ProviderKey);
        if (provider is null)
        {
            _loadError = Loc["Notion_WorkItem_LoadError"];
            _liveItem = null;
            return;
        }

        try
        {
            var item = await provider.GetByIdAsync(Content.ExternalId, _disposeCts.Token);
            if (item is null)
            {
                _loadError = Loc["Notion_WorkItem_LoadError"];
                _liveItem = null;
                return;
            }

            _loadError = null;
            _liveItem = NormalizeProviderSnapshot(item, Content.ProviderKey);

            if (updateContent && !SnapshotsEqual(Content.CachedSnapshot, _liveItem))
                await OnContentChanged.InvokeAsync(CloneContent(Content.ProviderKey, Content.ExternalId, _liveItem, CurrentDisplayMode));
        }
        catch when (!_disposeCts.IsCancellationRequested)
        {
            _loadError = Loc["Notion_WorkItem_LoadError"];
            _liveItem = null;
        }
    }

    private async Task HandleProviderChangedAsync(ChangeEventArgs args)
    {
        _selectedProviderKey = args.Value?.ToString() ?? string.Empty;
        _searched = false;
        _searchResults = [];
        _pickerError = null;
        if (!string.IsNullOrWhiteSpace(_searchText))
            await SearchAsync();
    }

    private Task HandleSearchInputAsync(ChangeEventArgs args)
    {
        _searchText = args.Value?.ToString() ?? string.Empty;
        _pickerError = null;
        return Task.CompletedTask;
    }

    private async Task HandleSearchKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && !string.IsNullOrWhiteSpace(_searchText))
            await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var provider = Registry?.GetProvider(_selectedProviderKey);
        if (provider is null)
        {
            _pickerError = Loc["Notion_WorkItem_LoadError"];
            return;
        }

        _searching = true;
        _searched = true;
        _pickerError = null;
        _searchResults = [];

        try
        {
            var query = new WorkItemQuery
            {
                ProviderKey = provider.ProviderKey,
                FreeText = _searchText.Trim(),
                Ids = [],
                Take = 12
            };
            var result = await provider.SearchAsync(query, _disposeCts.Token);
            _searchResults = result.Items;
        }
        catch when (!_disposeCts.IsCancellationRequested)
        {
            _pickerError = Loc["Notion_WorkItem_LoadError"];
        }
        finally
        {
            _searching = false;
        }
    }

    private async Task SelectWorkItemAsync(WorkItemDto item)
    {
        var snapshot = NormalizeProviderSnapshot(item, item.ProviderKey);
        _liveItem = snapshot;
        _loadError = null;
        _searchResults = [];
        _searched = false;
        _searchText = snapshot.ExternalId;
        _selectedProviderKey = snapshot.ProviderKey;
        _lastRefreshKey = $"{snapshot.ProviderKey}\u001f{snapshot.ExternalId}";

        await OnContentChanged.InvokeAsync(CloneContent(
            snapshot.ProviderKey,
            snapshot.ExternalId,
            snapshot,
            Content?.DisplayMode ?? WorkItemDisplayMode.Card));
    }

    private async Task SetDisplayModeAsync(WorkItemDisplayMode mode)
    {
        if (Content is null || ReadOnly || mode == CurrentDisplayMode)
            return;

        await OnContentChanged.InvokeAsync(CloneContent(
            Content.ProviderKey,
            Content.ExternalId,
            Snapshot ?? Content.CachedSnapshot,
            mode));
    }

    private async Task HandleRefreshClickedAsync()
    {
        _lastRefreshKey = null;
        await RefreshAsync(updateContent: true);
    }

    private Task HandleFocusAsync() => OnFocused.InvokeAsync();

    private void RenderCard(RenderTreeBuilder builder, WorkItemDto item)
    {
        var seq = 0;
        builder.OpenElement(seq++, "article");
        builder.AddAttribute(seq++, "class", "tm-work-item tm-work-item--card");
        builder.AddAttribute(seq++, "data-work-item-provider", item.ProviderKey);
        builder.AddAttribute(seq++, "data-work-item-id", item.ExternalId);

        builder.OpenElement(seq++, "div");
        builder.AddAttribute(seq++, "class", "tm-work-item__header");
        RenderTypeIcon(builder, ref seq, item);
        builder.OpenElement(seq++, "div");
        builder.AddAttribute(seq++, "class", "tm-work-item__heading");
        RenderLinkTitle(builder, ref seq, item);
        RenderMeta(builder, ref seq, item);
        builder.CloseElement();
        RenderStatus(builder, ref seq, item);
        builder.CloseElement();

        RenderCardDetails(builder, ref seq, item);
        RenderActions(builder, ref seq);

        builder.CloseElement();
    }

    private void RenderList(RenderTreeBuilder builder, WorkItemDto item)
    {
        var seq = 0;
        builder.OpenElement(seq++, "article");
        builder.AddAttribute(seq++, "class", "tm-work-item tm-work-item--list");
        builder.AddAttribute(seq++, "data-work-item-provider", item.ProviderKey);
        builder.AddAttribute(seq++, "data-work-item-id", item.ExternalId);
        RenderTypeIcon(builder, ref seq, item);
        RenderLinkTitle(builder, ref seq, item);
        RenderStatus(builder, ref seq, item);
        RenderActions(builder, ref seq);
        builder.CloseElement();
    }

    private void RenderInline(RenderTreeBuilder builder, WorkItemDto item)
    {
        var seq = 0;
        builder.OpenElement(seq++, "a");
        builder.AddAttribute(seq++, "class", "tm-work-item tm-work-item--inline");
        builder.AddAttribute(seq++, "href", item.Url);
        builder.AddAttribute(seq++, "target", "_blank");
        builder.AddAttribute(seq++, "rel", "noopener noreferrer");
        builder.AddAttribute(seq++, "data-work-item-provider", item.ProviderKey);
        builder.AddAttribute(seq++, "data-work-item-id", item.ExternalId);
        RenderTypeIcon(builder, ref seq, item);
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__chip-id");
        builder.AddContent(seq++, item.ExternalId);
        builder.CloseElement();
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__chip-title");
        builder.AddContent(seq++, item.Title);
        builder.CloseElement();
        RenderStatus(builder, ref seq, item);
        builder.CloseElement();
    }

    private void RenderTypeIcon(RenderTreeBuilder builder, ref int seq, WorkItemDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.TypeIconUrl))
        {
            builder.OpenElement(seq++, "img");
            builder.AddAttribute(seq++, "class", "tm-work-item__type-icon");
            builder.AddAttribute(seq++, "src", item.TypeIconUrl);
            builder.AddAttribute(seq++, "alt", string.Empty);
            builder.CloseElement();
            return;
        }

        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__type-fallback");
        builder.AddAttribute(seq++, "aria-hidden", "true");
        builder.AddContent(seq++, Initial(item.TypeLabel ?? item.ProviderKey));
        builder.CloseElement();
    }

    private void RenderLinkTitle(RenderTreeBuilder builder, ref int seq, WorkItemDto item)
    {
        builder.OpenElement(seq++, "a");
        builder.AddAttribute(seq++, "class", "tm-work-item__link");
        builder.AddAttribute(seq++, "href", item.Url);
        builder.AddAttribute(seq++, "target", "_blank");
        builder.AddAttribute(seq++, "rel", "noopener noreferrer");
        builder.AddAttribute(seq++, "aria-label", Loc["Notion_WorkItem_Open"]);
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__external-id");
        builder.AddContent(seq++, item.ExternalId);
        builder.CloseElement();
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__title");
        builder.AddContent(seq++, item.Title);
        builder.CloseElement();
        builder.CloseElement();
    }

    private void RenderMeta(RenderTreeBuilder builder, ref int seq, WorkItemDto item)
    {
        builder.OpenElement(seq++, "div");
        builder.AddAttribute(seq++, "class", "tm-work-item__meta");
        if (!string.IsNullOrWhiteSpace(item.TypeLabel))
            RenderMetaItem(builder, ref seq, item.TypeLabel);
        if (!string.IsNullOrWhiteSpace(item.AssigneeDisplayName))
            RenderMetaItem(builder, ref seq, item.AssigneeDisplayName);
        if (!string.IsNullOrWhiteSpace(item.Priority))
            RenderMetaItem(builder, ref seq, item.Priority);
        builder.CloseElement();
    }

    private static void RenderMetaItem(RenderTreeBuilder builder, ref int seq, string value)
    {
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__meta-item");
        builder.AddContent(seq++, value);
        builder.CloseElement();
    }

    private void RenderStatus(RenderTreeBuilder builder, ref int seq, WorkItemDto item)
    {
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-work-item__status");
        builder.AddAttribute(seq++, "style", StatusStyle(item.StatusColor));
        builder.AddAttribute(seq++, "aria-label", Loc["Notion_WorkItem_Status"]);
        builder.AddContent(seq++, item.Status);
        builder.CloseElement();
    }

    private void RenderCardDetails(RenderTreeBuilder builder, ref int seq, WorkItemDto item)
    {
        if (item.UpdatedAt is null && item.Fields.Count == 0)
            return;

        builder.OpenElement(seq++, "dl");
        builder.AddAttribute(seq++, "class", "tm-work-item__details");
        if (item.UpdatedAt is { } updatedAt)
        {
            builder.OpenElement(seq++, "div");
            builder.OpenElement(seq++, "dt");
            builder.AddContent(seq++, Loc["Notion_WorkItem_Updated"]);
            builder.CloseElement();
            builder.OpenElement(seq++, "dd");
            builder.AddContent(seq++, updatedAt.ToLocalTime().ToString("g"));
            builder.CloseElement();
            builder.CloseElement();
        }

        foreach (var field in item.Fields.Take(3))
        {
            builder.OpenElement(seq++, "div");
            builder.OpenElement(seq++, "dt");
            builder.AddContent(seq++, field.Key);
            builder.CloseElement();
            builder.OpenElement(seq++, "dd");
            builder.AddContent(seq++, field.Value);
            builder.CloseElement();
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    private void RenderActions(RenderTreeBuilder builder, ref int seq)
    {
        if (ReadOnly)
            return;

        builder.OpenElement(seq++, "div");
        builder.AddAttribute(seq++, "class", "tm-work-item__actions");
        RenderModeButton(builder, ref seq, WorkItemDisplayMode.Card, Loc["Notion_WorkItem_Mode_Card"]);
        RenderModeButton(builder, ref seq, WorkItemDisplayMode.List, Loc["Notion_WorkItem_Mode_List"]);
        RenderModeButton(builder, ref seq, WorkItemDisplayMode.Inline, Loc["Notion_WorkItem_Mode_Inline"]);
        builder.OpenElement(seq++, "button");
        builder.AddAttribute(seq++, "class", "tm-work-item__refresh");
        builder.AddAttribute(seq++, "type", "button");
        builder.AddAttribute(seq++, "title", Loc["Notion_WorkItem_Refresh"]);
        builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, HandleRefreshClickedAsync));
        builder.AddContent(seq++, Loc["Notion_WorkItem_Refresh"]);
        builder.CloseElement();
        builder.CloseElement();
    }

    private void RenderModeButton(RenderTreeBuilder builder, ref int seq, WorkItemDisplayMode mode, string label)
    {
        builder.OpenElement(seq++, "button");
        builder.AddAttribute(seq++, "class", mode == CurrentDisplayMode ? "tm-work-item__mode tm-work-item__mode--active" : "tm-work-item__mode");
        builder.AddAttribute(seq++, "type", "button");
        builder.AddAttribute(seq++, "aria-pressed", mode == CurrentDisplayMode);
        builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => SetDisplayModeAsync(mode)));
        builder.AddContent(seq++, label);
        builder.CloseElement();
    }

    private static WorkItemBlockContent CloneContent(
        string providerKey,
        string externalId,
        WorkItemDto? snapshot,
        WorkItemDisplayMode mode)
        => new()
        {
            ProviderKey = providerKey,
            ExternalId = externalId,
            CachedSnapshot = snapshot,
            DisplayMode = mode
        };

    private static WorkItemDto NormalizeProviderSnapshot(WorkItemDto item, string providerKey)
        => new()
        {
            ProviderKey = string.IsNullOrWhiteSpace(item.ProviderKey) ? providerKey : item.ProviderKey,
            ExternalId = item.ExternalId,
            Url = item.Url,
            Title = item.Title,
            Status = item.Status,
            StatusColor = item.StatusColor,
            TypeLabel = item.TypeLabel,
            TypeIconUrl = item.TypeIconUrl,
            AssigneeDisplayName = item.AssigneeDisplayName,
            Priority = item.Priority,
            UpdatedAt = item.UpdatedAt,
            Fields = new Dictionary<string, string>(item.Fields, StringComparer.OrdinalIgnoreCase)
        };

    private static bool SnapshotsEqual(WorkItemDto? left, WorkItemDto? right)
        => left is not null && right is not null
            && string.Equals(left.ProviderKey, right.ProviderKey, StringComparison.Ordinal)
            && string.Equals(left.ExternalId, right.ExternalId, StringComparison.Ordinal)
            && string.Equals(left.Title, right.Title, StringComparison.Ordinal)
            && string.Equals(left.Status, right.Status, StringComparison.Ordinal)
            && string.Equals(left.StatusColor, right.StatusColor, StringComparison.Ordinal)
            && string.Equals(left.Url, right.Url, StringComparison.Ordinal);

    private static string StatusStyle(string? color)
    {
        var sanitized = SanitizeCssColor(color);
        return string.IsNullOrWhiteSpace(sanitized)
            ? string.Empty
            : $"--tm-work-item-status-color:{sanitized}";
    }

    private static string? SanitizeCssColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;

        var trimmed = color.Trim();
        if (trimmed.StartsWith("var(--tm-", StringComparison.Ordinal) && trimmed.EndsWith(')'))
            return trimmed;

        if (trimmed.Length is 4 or 7 or 9
            && trimmed[0] == '#'
            && trimmed[1..].All(Uri.IsHexDigit))
            return trimmed;

        return null;
    }

    private static string Initial(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? string.Empty : trimmed[..1].ToUpperInvariant();
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
