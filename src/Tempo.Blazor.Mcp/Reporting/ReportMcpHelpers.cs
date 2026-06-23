using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.Blazor.Mcp.Reporting;

internal static class ReportMcpHelpers
{
    public const string DefaultTenantId = "northwind";
    public const string DefaultUserId = "mcp-agent";
    public const string DefaultCultureName = "en-US";
    public const string DefaultFolderId = "root";

    public static ReportExecutionContext Context(
        string? tenantId,
        string? userId,
        string? cultureName,
        CancellationToken cancellationToken = default)
        => new(
            string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId.Trim(),
            string.IsNullOrWhiteSpace(userId) ? DefaultUserId : userId.Trim(),
            string.IsNullOrWhiteSpace(cultureName) ? DefaultCultureName : cultureName.Trim(),
            CancellationToken: cancellationToken);

    public static string FolderId(string? folderId)
        => string.IsNullOrWhiteSpace(folderId) ? DefaultFolderId : folderId.Trim();

    public static string ReportId(string? reportId, string name)
        => string.IsNullOrWhiteSpace(reportId) ? Slug(name) : reportId.Trim();

    public static string Slug(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? $"report-{Guid.NewGuid():N}" : slug;
    }

    public static ReportDefinition CreateDefaultDefinition(string reportId, string name, string? description)
        => new()
        {
            Id = reportId,
            Name = name,
            Description = description,
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(760, 540),
                Margins = new ReportThickness(24),
            },
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 56,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "report-title",
                            X = 0,
                            Y = 0,
                            Width = 420,
                            Height = 28,
                            Text = name,
                            TextStyle = new ReportTextStyle { FontSize = 18, Bold = true },
                        },
                        new ReportLineElement
                        {
                            Id = "header-rule",
                            X = 0,
                            Y = 40,
                            Width = 712,
                            Height = 0,
                            Stroke = new ReportBorderLine("#cbd5e1", 1),
                        },
                    ],
                },
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 48,
                },
            },
        };

    public static JsonNode? DefinitionNode(ReportDefinition definition)
        => JsonNode.Parse(ReportDefinitionJsonSerializer.Serialize(definition));

    public static async Task<(ReportDefinitionRecord? Report, ReportDefinitionRevisionRecord? Revision, ReportDefinition? Definition)>
        LoadLatestAsync(IReportDefinitionStore store, string reportId, ReportExecutionContext context)
    {
        var report = await store.LoadReportAsync(reportId, context).ConfigureAwait(false);
        if (report is null)
        {
            return (null, null, null);
        }

        var revisions = await store.ListRevisionsAsync(reportId, context).ConfigureAwait(false);
        var revision = string.IsNullOrWhiteSpace(report.LatestRevisionId)
            ? revisions.OrderByDescending(item => item.RevisionNumber).FirstOrDefault()
            : revisions.FirstOrDefault(item => string.Equals(item.RevisionId, report.LatestRevisionId, StringComparison.Ordinal));
        if (revision is null)
        {
            return (report, null, null);
        }

        var definition = ReportDefinitionJsonSerializer.Deserialize(revision.DefinitionJson);
        return (report, revision, definition);
    }

    public static IReadOnlyDictionary<string, ReportParameterValue> ParseParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);
        }

        var root = JsonNode.Parse(parametersJson) as JsonObject
            ?? throw new JsonException("parametersJson must be a JSON object.");
        var result = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);
        foreach (var (key, value) in root)
        {
            result[key] = ToParameterValue(value);
        }

        return result;
    }

    private static ReportParameterValue ToParameterValue(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj["values"] is JsonArray values)
            {
                return ReportParameterValue.Multiple(values.Select(ToClrValue));
            }

            if (obj.ContainsKey("scalarValue"))
            {
                return ReportParameterValue.Scalar(ToClrValue(obj["scalarValue"]));
            }
        }

        if (node is JsonArray array)
        {
            return ReportParameterValue.Multiple(array.Select(ToClrValue));
        }

        return ReportParameterValue.Scalar(ToClrValue(node));
    }

    private static object? ToClrValue(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        var element = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.Clone(),
        };
    }
}
