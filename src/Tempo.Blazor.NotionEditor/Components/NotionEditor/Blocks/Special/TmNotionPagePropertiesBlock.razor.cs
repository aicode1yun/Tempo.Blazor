using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionPagePropertiesBlock : ComponentBase
{
    /// <summary>Owning page block for persistence context.</summary>
    [Parameter] public IPageBlock Block { get; set; } = default!;

    /// <summary>Saved page properties content.</summary>
    [Parameter] public IPagePropertiesBlockContent? Content { get; set; }

    /// <summary>Whether the properties table is rendered without editing affordances.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised when edited page properties should be persisted.</summary>
    [Parameter] public EventCallback<PagePropertiesBlockContent> OnContentChanged { get; set; }

    /// <summary>Raised when the block receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    private readonly List<EditablePropertyRow> _rows = [];
    private string? _contentSignature;

    private string RowsSummary => _rows.Count == 0
        ? Loc["Notion_PageProps_Empty"]
        : string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}", _rows.Count);

    protected override void OnParametersSet()
    {
        var signature = BuildSignature(Content?.Rows ?? []);
        if (string.Equals(signature, _contentSignature, StringComparison.Ordinal))
        {
            return;
        }

        _contentSignature = signature;
        _rows.Clear();
        foreach (var row in Content?.Rows ?? [])
        {
            _rows.Add(new EditablePropertyRow(row.Key, ToPlainText(row.ValueHtml)));
        }
    }

    private async Task AddRowAsync()
    {
        if (ReadOnly)
        {
            return;
        }

        _rows.Add(new EditablePropertyRow(string.Empty, string.Empty));
        await PersistAsync();
    }

    private async Task RemoveRowAsync(int index)
    {
        if (ReadOnly || index < 0 || index >= _rows.Count)
        {
            return;
        }

        _rows.RemoveAt(index);
        await PersistAsync();
    }

    private async Task ChangeKeyAsync(int index, string value)
    {
        if (ReadOnly || index < 0 || index >= _rows.Count)
        {
            return;
        }

        _rows[index] = _rows[index] with { Key = value.Trim() };
        await PersistAsync();
    }

    private async Task ChangeValueAsync(int index, string value)
    {
        if (ReadOnly || index < 0 || index >= _rows.Count)
        {
            return;
        }

        _rows[index] = _rows[index] with { ValueText = value };
        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        var content = new PagePropertiesBlockContent
        {
            Rows = _rows
                .Select(row => new PagePropertyRow
                {
                    Key = row.Key,
                    ValueHtml = NotionInlineHtmlSanitizer.EncodePlainText(row.ValueText)
                })
                .ToArray()
        };

        _contentSignature = BuildSignature(content.Rows);
        await OnContentChanged.InvokeAsync(content);
    }

    private Task HandleFocusedAsync(MouseEventArgs _)
        => OnFocused.InvokeAsync();

    private static string BuildSignature(IReadOnlyList<PagePropertyRow> rows)
        => string.Join('\u001f', rows.Select(row => $"{row.Key}\u001e{row.ValueHtml}"));

    private static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var normalized = html.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
        var withoutTags = HtmlTagRegex().Replace(normalized, string.Empty);
        return WebUtility.HtmlDecode(withoutTags);
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    private sealed record EditablePropertyRow(string Key, string ValueText);
}
