using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>MCP tools for reading and writing report definitions.</summary>
[McpServerToolType]
public static class ReportDefinitionTools
{
    [McpServerTool(Name = "get_report_definition")]
    [Description("Get one report definition by id. Returns metadata, latest revision and canonical report definition JSON. Optionally pass revisionId for an older revision.")]
    public static async Task<string> GetReportDefinition(
        IReportDefinitionStore store,
        [Description("Report id.")] string reportId,
        [Description("Tenant id. Defaults to northwind.")] string? tenantId = null,
        [Description("Optional revision id. Omit for latest.")] string? revisionId = null,
        CancellationToken cancellationToken = default)
    {
        var context = ReportMcpHelpers.Context(tenantId, null, null, cancellationToken);
        var report = await store.LoadReportAsync(reportId, context).ConfigureAwait(false);
        if (report is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Report '{reportId}' not found.");
        }

        ReportDefinitionRevisionRecord? revision;
        if (string.IsNullOrWhiteSpace(revisionId))
        {
            var revisions = await store.ListRevisionsAsync(reportId, context).ConfigureAwait(false);
            revision = string.IsNullOrWhiteSpace(report.LatestRevisionId)
                ? revisions.OrderByDescending(item => item.RevisionNumber).FirstOrDefault()
                : revisions.FirstOrDefault(item => string.Equals(item.RevisionId, report.LatestRevisionId, StringComparison.Ordinal));
        }
        else
        {
            revision = await store.LoadRevisionAsync(reportId, revisionId, context).ConfigureAwait(false);
        }

        if (revision is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Report '{reportId}' has no matching revision.");
        }

        var definition = ReportDefinitionJsonSerializer.Deserialize(revision.DefinitionJson);
        return McpToolResults.Success(new
        {
            report = new
            {
                report.TenantId,
                report.ReportId,
                report.FolderId,
                report.Name,
                report.Description,
                report.LatestRevisionId,
            },
            revision = new
            {
                revision.RevisionId,
                revision.RevisionNumber,
                revision.CreatedByUserId,
                revision.CreatedAt,
                revision.IsPublished,
            },
            definition = ReportMcpHelpers.DefinitionNode(definition),
        });
    }

    [McpServerTool(Name = "create_report")]
    [Description("Create a report definition. Provide definitionJson for a full definition or omit it to create an AI-friendly starter report with a title band and empty detail band.")]
    public static async Task<string> CreateReport(
        IReportDefinitionStore store,
        [Description("Human-readable report name.")] string name,
        [Description("Optional full report definition JSON.")] string? definitionJson = null,
        [Description("Optional stable report id. Defaults to a slug from name.")] string? reportId = null,
        [Description("Optional report description.")] string? description = null,
        [Description("Tenant id. Defaults to northwind.")] string? tenantId = null,
        [Description("Folder id. Defaults to root.")] string? folderId = null,
        [Description("User id for revision audit. Defaults to mcp-agent.")] string? userId = null,
        [Description("Whether the created revision is published.")] bool publish = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "create_report requires 'name'.");
        }

        var id = ReportMcpHelpers.ReportId(reportId, name);
        var context = ReportMcpHelpers.Context(tenantId, userId, null, cancellationToken);
        var definition = string.IsNullOrWhiteSpace(definitionJson)
            ? ReportMcpHelpers.CreateDefaultDefinition(id, name.Trim(), description)
            : ReportDefinitionJsonSerializer.Deserialize(definitionJson) with
            {
                Id = id,
                Name = name.Trim(),
                Description = description,
            };
        var validation = ReportValidationEngine.Validate(definition);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The report definition is invalid; nothing was saved.", validation.Errors);
        }

        var revision = await store.SaveReportAsync(
            new ReportDefinitionRecord
            {
                TenantId = context.TenantId,
                ReportId = id,
                FolderId = ReportMcpHelpers.FolderId(folderId),
                Name = definition.Name,
                Description = definition.Description,
            },
            ReportDefinitionJsonSerializer.Serialize(definition),
            publish,
            context).ConfigureAwait(false);

        return McpToolResults.Success(new
        {
            reportId = id,
            revisionId = revision.RevisionId,
            revisionNumber = revision.RevisionNumber,
            definition = ReportMcpHelpers.DefinitionNode(definition),
        });
    }

    [McpServerTool(Name = "update_report_elements")]
    [Description("Apply an ordered JSON operation batch to a stored report definition and save a new revision. Supported ops: setName, setDescription, setPageSetup, setBandHeight, clearBand, addElement, updateElement, replaceElement, removeElement. Element payloads use the report JSON discriminator 'type'. The resulting definition is validated before saving.")]
    public static async Task<string> UpdateReportElements(
        IReportDefinitionStore store,
        [Description("Report id.")] string reportId,
        [Description("JSON array of report edit operations.")] string operationsJson,
        [Description("Tenant id. Defaults to northwind.")] string? tenantId = null,
        [Description("User id for revision audit. Defaults to mcp-agent.")] string? userId = null,
        [Description("Optional optimistic token: latest revision id expected by the caller.")] string? expectedRevisionId = null,
        [Description("Whether the new revision is published.")] bool publish = false,
        CancellationToken cancellationToken = default)
    {
        var context = ReportMcpHelpers.Context(tenantId, userId, null, cancellationToken);
        var (report, revision, definition) = await ReportMcpHelpers.LoadLatestAsync(store, reportId, context).ConfigureAwait(false);
        if (report is null || revision is null || definition is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Report '{reportId}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(expectedRevisionId) &&
            !string.Equals(expectedRevisionId, revision.RevisionId, StringComparison.Ordinal))
        {
            return McpToolResults.Failure(
                McpToolResults.Conflict,
                $"Report '{reportId}' is at revision '{revision.RevisionId}'. Re-read with get_report_definition and retry.");
        }

        var result = ReportOperationEngine.Apply(definition, operationsJson);
        if (!result.Success || result.Definition is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "One or more report operations failed.", result.Errors);
        }

        var validation = ReportValidationEngine.Validate(result.Definition);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The resulting report definition is invalid; nothing was saved.", validation.Errors);
        }

        var saved = await store.SaveReportAsync(
            report with
            {
                Name = result.Definition.Name,
                Description = result.Definition.Description,
            },
            ReportDefinitionJsonSerializer.Serialize(result.Definition),
            publish,
            context).ConfigureAwait(false);

        return McpToolResults.Success(new
        {
            reportId,
            applied = result.Applied,
            createdIds = result.CreatedIds,
            revisionId = saved.RevisionId,
            revisionNumber = saved.RevisionNumber,
            definition = ReportMcpHelpers.DefinitionNode(result.Definition),
        });
    }
}
