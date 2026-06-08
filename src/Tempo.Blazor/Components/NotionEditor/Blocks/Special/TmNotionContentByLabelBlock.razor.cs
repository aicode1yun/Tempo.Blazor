using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionContentByLabelBlock : ComponentBase, IDisposable
{
    private static readonly ContentByLabelSortBy[] SortOptions =
    [
        ContentByLabelSortBy.LastEditedDescending,
        ContentByLabelSortBy.LastEditedAscending,
        ContentByLabelSortBy.TitleAscending,
        ContentByLabelSortBy.TitleDescending,
        ContentByLabelSortBy.CreatedDescending,
        ContentByLabelSortBy.CreatedAscending
    ];

    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>Saved Content-by-Label block configuration.</summary>
    [Parameter] public IContentByLabelBlockContent? Content { get; set; }

    /// <summary>Whether editing controls are hidden.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised after the block configuration changes.</summary>
    [Parameter] public EventCallback<ContentByLabelBlockContent> OnContentChanged { get; set; }

    /// <summary>Raised when the block receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    private IReadOnlyList<string> _labels = [];
    private IReadOnlyList<string> _allLabels = [];
    private IReadOnlyList<INotionPage> _pages = [];
    private ContentByLabelSortBy _sortBy = ContentByLabelSortBy.LastEditedDescending;
    private string _selectedLabel = string.Empty;
    private int _maxItems = 10;
    private bool _loading;
    private bool _loadedLabels;
    private int _loadVersion;
    private string? _loadedContentSignature;

    private IEnumerable<string> AvailableLabels => _allLabels
        .Where(label => !_labels.Contains(label, StringComparer.OrdinalIgnoreCase));

    private string SummaryText => _labels.Count == 0
        ? Loc["Notion_ContentByLabel_SelectLabels"]
        : string.Join(", ", _labels);

    protected override async Task OnParametersSetAsync()
    {
        _labels = NormalizeLabels(Content?.Labels);
        _maxItems = NormalizeMaxItems(Content?.MaxItems ?? 10);
        _sortBy = Content?.SortBy ?? ContentByLabelSortBy.LastEditedDescending;

        if (!_loadedLabels && !ReadOnly)
        {
            await LoadAllLabelsAsync();
        }

        var signature = BuildSignature(_labels, _maxItems, _sortBy);
        if (!string.Equals(signature, _loadedContentSignature, StringComparison.Ordinal))
        {
            _loadedContentSignature = signature;
            await LoadPagesAsync();
        }
    }

    private async Task LoadAllLabelsAsync()
    {
        _allLabels = NormalizeLabels(await Context.DataProvider.GetAllLabelsAsync(_disposeCts.Token));
        _loadedLabels = true;
    }

    private async Task LoadPagesAsync()
    {
        var version = ++_loadVersion;
        if (_labels.Count == 0)
        {
            _pages = [];
            return;
        }

        _loading = true;
        try
        {
            var pages = new Dictionary<Guid, INotionPage>();
            foreach (var label in _labels)
            {
                var matches = await Context.DataProvider.GetPagesByLabelAsync(label, _disposeCts.Token);
                foreach (var page in matches.Where(page => !page.IsDeleted))
                {
                    pages[page.Id] = page;
                }
            }

            if (version == _loadVersion)
            {
                _pages = SortPages(pages.Values).Take(_maxItems).ToArray();
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _loading = false;
            }
        }
    }

    private Task OnFocusedAsync(MouseEventArgs _)
        => OnFocused.InvokeAsync();

    private Task HandleSelectedLabelChangedAsync(ChangeEventArgs args)
    {
        _selectedLabel = NormalizeLabel(args.Value?.ToString()) ?? string.Empty;
        return Task.CompletedTask;
    }

    private async Task AddSelectedLabelAsync()
    {
        var label = NormalizeLabel(_selectedLabel);
        if (label is null || _labels.Contains(label, StringComparer.OrdinalIgnoreCase))
        {
            _selectedLabel = string.Empty;
            return;
        }

        _labels = NormalizeLabels(_labels.Concat([label]));
        _selectedLabel = string.Empty;
        await SaveContentAsync();
    }

    private async Task RemoveLabelAsync(string label)
    {
        _labels = _labels
            .Where(existing => !string.Equals(existing, label, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        await SaveContentAsync();
    }

    private async Task HandleMaxItemsChangedAsync(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var maxItems))
        {
            _maxItems = NormalizeMaxItems(maxItems);
            await SaveContentAsync();
        }
    }

    private async Task HandleSortChangedAsync(ChangeEventArgs args)
    {
        if (Enum.TryParse<ContentByLabelSortBy>(args.Value?.ToString(), out var sortBy))
        {
            _sortBy = sortBy;
            await SaveContentAsync();
        }
    }

    private async Task SaveContentAsync()
    {
        var content = new ContentByLabelBlockContent
        {
            Labels = _labels,
            MaxItems = _maxItems,
            SortBy = _sortBy
        };

        _loadedContentSignature = BuildSignature(_labels, _maxItems, _sortBy);
        await OnContentChanged.InvokeAsync(content);
        await LoadPagesAsync();
    }

    private IEnumerable<INotionPage> SortPages(IEnumerable<INotionPage> pages)
    {
        return _sortBy switch
        {
            ContentByLabelSortBy.LastEditedAscending => pages
                .OrderBy(page => page.LastEditedAt)
                .ThenBy(page => PageTitle(page), StringComparer.OrdinalIgnoreCase),
            ContentByLabelSortBy.TitleAscending => pages
                .OrderBy(page => PageTitle(page), StringComparer.OrdinalIgnoreCase),
            ContentByLabelSortBy.TitleDescending => pages
                .OrderByDescending(page => PageTitle(page), StringComparer.OrdinalIgnoreCase),
            ContentByLabelSortBy.CreatedDescending => pages
                .OrderByDescending(page => page.CreatedAt)
                .ThenBy(page => PageTitle(page), StringComparer.OrdinalIgnoreCase),
            ContentByLabelSortBy.CreatedAscending => pages
                .OrderBy(page => page.CreatedAt)
                .ThenBy(page => PageTitle(page), StringComparer.OrdinalIgnoreCase),
            _ => pages
                .OrderByDescending(page => page.LastEditedAt)
                .ThenBy(page => PageTitle(page), StringComparer.OrdinalIgnoreCase)
        };
    }

    private string SortLabel(ContentByLabelSortBy sortBy) => sortBy switch
    {
        ContentByLabelSortBy.LastEditedAscending => Loc["Notion_ContentByLabel_Sort_LastEditedAsc"],
        ContentByLabelSortBy.TitleAscending => Loc["Notion_ContentByLabel_Sort_TitleAsc"],
        ContentByLabelSortBy.TitleDescending => Loc["Notion_ContentByLabel_Sort_TitleDesc"],
        ContentByLabelSortBy.CreatedDescending => Loc["Notion_ContentByLabel_Sort_CreatedDesc"],
        ContentByLabelSortBy.CreatedAscending => Loc["Notion_ContentByLabel_Sort_CreatedAsc"],
        _ => Loc["Notion_ContentByLabel_Sort_LastEditedDesc"]
    };

    private string PageIcon(INotionPage page)
        => !string.IsNullOrWhiteSpace(page.IconEmoji) ? page.IconEmoji! : string.Empty;

    private static string PageTitle(INotionPage page)
        => string.IsNullOrWhiteSpace(page.Title) ? page.Id.ToString("D") : page.Title;

    private static string PageLabels(INotionPage page)
        => string.Join(", ", page.Labels);

    private async Task NavigateToPageAsync(INotionPage page)
    {
        await OnFocused.InvokeAsync();
        if (Context.NavigateTo is not null)
        {
            await Context.NavigateTo(page.Id.ToString("D"));
        }
    }

    private static int NormalizeMaxItems(int value)
        => value is >= 1 and <= 100 ? value : 10;

    private static string BuildSignature(IReadOnlyList<string> labels, int maxItems, ContentByLabelSortBy sortBy)
        => $"{maxItems}|{sortBy}|{string.Join('\u001f', labels)}";

    private static IReadOnlyList<string> NormalizeLabels(IEnumerable<string>? labels)
    {
        if (labels is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var label in labels)
        {
            var normalized = NormalizeLabel(label);
            if (normalized is null || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(normalized);
        }

        return result;
    }

    private static string? NormalizeLabel(string? label)
    {
        var trimmed = label?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public void Dispose()
        => _disposeCts.Dispose();
}
