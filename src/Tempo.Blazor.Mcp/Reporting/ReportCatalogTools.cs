using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>MCP catalog tools for report definitions.</summary>
[McpServerToolType]
public static class ReportCatalogTools
{
    [McpServerTool(Name = "list_reports")]
    [Description("List stored report definitions for a tenant/folder. Returns id, name, description, folderId and latestRevisionId. Use get_report_definition before editing.")]
    public static async Task<string> ListReports(
        IReportDefinitionStore store,
        [Description("Tenant id. Defaults to northwind.")] string? tenantId = null,
        [Description("Folder id. Defaults to root.")] string? folderId = null,
        [Description("Optional free-text search across report id/name/description.")] string? search = null,
        [Description("Pagination offset.")] int skip = 0,
        [Description("Maximum number of reports to return.")] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var context = ReportMcpHelpers.Context(tenantId, null, null, cancellationToken);
        var reports = await store.ListReportsAsync(ReportMcpHelpers.FolderId(folderId), context).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(search))
        {
            reports = reports
                .Where(report =>
                    report.ReportId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    report.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (report.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();
        }

        var page = reports
            .OrderBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .Select(report => new
            {
                reportId = report.ReportId,
                report.Name,
                report.Description,
                report.FolderId,
                report.LatestRevisionId,
            })
            .ToArray();

        return McpToolResults.Success(new
        {
            tenantId = context.TenantId,
            folderId = ReportMcpHelpers.FolderId(folderId),
            totalCount = reports.Count,
            items = page,
        });
    }
}
