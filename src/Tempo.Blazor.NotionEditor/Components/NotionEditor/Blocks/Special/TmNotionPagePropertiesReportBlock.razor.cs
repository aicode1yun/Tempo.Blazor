using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionPagePropertiesReportBlock : ComponentBase
{
    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    /// <summary>Owning page block for focus context.</summary>
    [Parameter] public IPageBlock Block { get; set; } = default!;

    /// <summary>Saved report configuration.</summary>
    [Parameter] public IPagePropertiesReportBlockContent? Content { get; set; }

    /// <summary>Whether report configuration controls are hidden.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised when edited report configuration should be persisted.</summary>
    [Parameter] public EventCallback<PagePropertiesReportBlockContent> OnContentChanged { get; set; }

    /// <summary>Raised when the block receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    private readonly List<PagePropertiesReportRow> _rows = [];
    private readonly List<string> _labels = [];
    private readonly List<string> _columns = [];
    private string _labelsText = string.Empty;
    private string _columnsText = string.Empty;
    private string? _contentSignature;
    private string? _loadedSignature;
    private bool _loading;

    private IReadOnlyList<string> VisibleColumns
        => _columns.Count > 0
            ? _columns
            : _rows
                .SelectMany(row => row.Properties.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private string SummaryText
        => _labels.Count == 0
            ? Loc["Notion_PropsReport_Configure"]
            : string.Join(", ", _labels);

    protected override async Task OnParametersSetAsync()
    {
        var signature = BuildSignature(Content?.Labels ?? [], Content?.Columns ?? []);
        if (!string.Equals(signature, _contentSignature, StringComparison.Ordinal))
        {
            _contentSignature = signature;
            SetConfiguration(Content?.Labels ?? [], Content?.Columns ?? []);
        }

        await LoadReportIfNeededAsync();
    }

    private async Task HandleLabelsChangedAsync(ChangeEventArgs args)
    {
        if (ReadOnly)
        {
            return;
        }

        _labelsText = args.Value?.ToString() ?? string.Empty;
        _labels.Clear();
        _labels.AddRange(ParseList(_labelsText));
        await PersistConfigurationAsync();
        await LoadReportAsync();
    }

    private async Task HandleColumnsChangedAsync(ChangeEventArgs args)
    {
        if (ReadOnly)
        {
            return;
        }

        _columnsText = args.Value?.ToString() ?? string.Empty;
        _columns.Clear();
        _columns.AddRange(ParseList(_columnsText));
        await PersistConfigurationAsync();
        await LoadReportAsync();
    }

    private async Task PersistConfigurationAsync()
    {
        var content = new PagePropertiesReportBlockContent
        {
            Labels = _labels.ToArray(),
            Columns = _columns.ToArray()
        };

        _contentSignature = BuildSignature(content.Labels, content.Columns);
        await OnContentChanged.InvokeAsync(content);
    }

    private async Task LoadReportIfNeededAsync()
    {
        var signature = BuildSignature(_labels, _columns);
        if (string.Equals(signature, _loadedSignature, StringComparison.Ordinal))
        {
            return;
        }

        await LoadReportAsync();
    }

    private async Task LoadReportAsync()
    {
        _loadedSignature = BuildSignature(_labels, _columns);
        _rows.Clear();

        if (Context.PagePropertiesProvider is null)
        {
            return;
        }

        _loading = true;
        try
        {
            var rows = await Context.PagePropertiesProvider.QueryPagePropertiesAsync(new PagePropertiesReportQuery
            {
                Labels = _labels.ToArray(),
                Columns = _columns.ToArray()
            });

            _rows.AddRange(rows);
        }
        finally
        {
            _loading = false;
        }
    }

    private void SetConfiguration(IReadOnlyList<string> labels, IReadOnlyList<string> columns)
    {
        _labels.Clear();
        _labels.AddRange(Normalize(labels));
        _columns.Clear();
        _columns.AddRange(Normalize(columns));
        _labelsText = string.Join(", ", _labels);
        _columnsText = string.Join(", ", _columns);
    }

    private Task NavigateToPageAsync(PagePropertiesReportRow row)
        => Context.NavigateTo is null
            ? Task.CompletedTask
            : Context.NavigateTo(row.PageId.ToString("D"));

    private Task HandleFocusedAsync(MouseEventArgs _)
        => OnFocused.InvokeAsync();

    private static bool TryGetProperty(PagePropertiesReportRow row, string column, out string? valueHtml)
    {
        foreach (var property in row.Properties)
        {
            if (!string.Equals(property.Key, column, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            valueHtml = property.Value;
            return !string.IsNullOrWhiteSpace(valueHtml);
        }

        valueHtml = null;
        return false;
    }

    private static string PageIcon(PagePropertiesReportRow row)
        => string.IsNullOrWhiteSpace(row.IconEmoji) ? "#" : row.IconEmoji;

    private string PageTitle(PagePropertiesReportRow row)
        => string.IsNullOrWhiteSpace(row.Title) ? Loc["TmNotionEditor_Untitled"] : row.Title;

    private static string Sanitize(string? html)
        => NotionInlineHtmlSanitizer.SanitizeHtmlFragment(html);

    private static string BuildSignature(IReadOnlyList<string> labels, IReadOnlyList<string> columns)
        => $"{string.Join('\u001f', Normalize(labels))}\u001e{string.Join('\u001f', Normalize(columns))}";

    private static IReadOnlyList<string> ParseList(string value)
        => Normalize(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        => values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
