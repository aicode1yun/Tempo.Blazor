using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

/// <summary>Filterable audit log panel for Notion workspace actions.</summary>
public partial class TmNotionAuditLogPanel : ComponentBase
{
    private static readonly IReadOnlyList<string> AvailableActions = ["create", "edit", "delete", "move", "restrict"];
    private const int DefaultPageSize = 10;
    private const int ExportLimit = 1000;

    /// <summary>Provider used to query audit entries.</summary>
    [Parameter, EditorRequired] public INotionAuditProvider AuditProvider { get; set; } = default!;

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised when the close button is clicked.</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    /// <summary>Number of audit entries shown per page.</summary>
    [Parameter] public int PageSize { get; set; } = DefaultPageSize;

    private PagedResult<AuditEntryDto> _result = new() { Page = 1, PageSize = DefaultPageSize };
    private string _userFilter = string.Empty;
    private string _actionFilter = string.Empty;
    private string _fromFilter = string.Empty;
    private string _toFilter = string.Empty;
    private int _skip;
    private bool _isLoading;
    private string? _loadError;
    private string? _csvHref;

    private string CsvFileName => $"notion-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;
        _csvHref = null;
        StateHasChanged();

        try
        {
            _result = await AuditProvider.GetEntriesAsync(BuildFilter(), BuildPaging());
        }
        catch
        {
            _loadError = Loc["Notion_Audit_LoadError"];
            _result = new PagedResult<AuditEntryDto> { Page = 1, PageSize = EffectivePageSize };
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task ApplyFiltersAsync()
    {
        _skip = 0;
        await LoadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _userFilter = string.Empty;
        _actionFilter = string.Empty;
        _fromFilter = string.Empty;
        _toFilter = string.Empty;
        _skip = 0;
        await LoadAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (!_result.HasPreviousPage)
            return;

        _skip = Math.Max(0, _skip - EffectivePageSize);
        await LoadAsync();
    }

    private async Task NextPageAsync()
    {
        if (!_result.HasNextPage)
            return;

        _skip += EffectivePageSize;
        await LoadAsync();
    }

    private async Task PrepareCsvExportAsync()
    {
        var exportResult = await AuditProvider.GetEntriesAsync(
            BuildFilter(),
            new NotionAuditQuery { Skip = 0, Take = ExportLimit });

        var csv = BuildCsv(exportResult.Items);
        _csvHref = "data:text/csv;charset=utf-8," + Uri.EscapeDataString(csv);
    }

    private void OnUserFilterInput(ChangeEventArgs args)
        => _userFilter = args.Value?.ToString() ?? string.Empty;

    private void OnActionFilterChanged(ChangeEventArgs args)
        => _actionFilter = args.Value?.ToString() ?? string.Empty;

    private void OnFromFilterChanged(ChangeEventArgs args)
        => _fromFilter = args.Value?.ToString() ?? string.Empty;

    private void OnToFilterChanged(ChangeEventArgs args)
        => _toFilter = args.Value?.ToString() ?? string.Empty;

    private AuditLogFilter BuildFilter()
        => new()
        {
            UserId = string.IsNullOrWhiteSpace(_userFilter) ? null : _userFilter.Trim(),
            Action = string.IsNullOrWhiteSpace(_actionFilter) ? null : _actionFilter.Trim(),
            From = ParseDate(_fromFilter),
            To = ParseDate(_toFilter)
        };

    private NotionAuditQuery BuildPaging()
        => new()
        {
            Skip = _skip,
            Take = EffectivePageSize
        };

    private int EffectivePageSize => Math.Clamp(PageSize, 1, 50);

    private static DateOnly? ParseDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private string BuildCsv(IReadOnlyList<AuditEntryDto> entries)
    {
        var builder = new StringBuilder();
        builder.Append(CsvCell(Loc["Notion_Audit_Timestamp"]));
        builder.Append(',');
        builder.Append(CsvCell(Loc["Notion_Audit_User"]));
        builder.Append(',');
        builder.Append(CsvCell(Loc["Notion_Audit_Action"]));
        builder.Append(',');
        builder.Append(CsvCell(Loc["Notion_Audit_Target"]));
        builder.Append(',');
        builder.Append(CsvCell(Loc["Notion_Audit_Details"]));
        builder.AppendLine();

        foreach (var entry in entries)
        {
            builder.Append(CsvCell(entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(CsvCell(GetUserDisplay(entry)));
            builder.Append(',');
            builder.Append(CsvCell(GetActionLabel(entry.Action)));
            builder.Append(',');
            builder.Append(CsvCell($"{entry.TargetType}:{entry.TargetId}"));
            builder.Append(',');
            builder.Append(CsvCell(FormatDetails(entry)));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string GetActionLabel(string action)
        => action.ToLowerInvariant() switch
        {
            "create" => Loc["Notion_Audit_Action_Create"],
            "edit" => Loc["Notion_Audit_Action_Edit"],
            "delete" => Loc["Notion_Audit_Action_Delete"],
            "move" => Loc["Notion_Audit_Action_Move"],
            "restrict" => Loc["Notion_Audit_Action_Restrict"],
            _ => action
        };

    private static string GetUserDisplay(AuditEntryDto entry)
        => string.IsNullOrWhiteSpace(entry.UserDisplayName) ? entry.UserId : entry.UserDisplayName;

    private static string ShortenTargetId(string targetId)
        => Guid.TryParse(targetId, out var id) ? id.ToString("D")[..8] : targetId;

    private static string FormatTimestamp(DateTime timestamp)
        => timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static string FormatDetails(AuditEntryDto entry)
        => string.Join("; ", entry.Details
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={pair.Value}"));

    private static string NormalizeCssToken(string value)
        => string.Concat(value.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-'));

    private static string CsvCell(string value)
        => '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
