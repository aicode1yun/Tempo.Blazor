using System.Reflection;
using System.Text.Json.Serialization;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>Prompt-friendly report definition schema documentation for AI tools.</summary>
public static class ReportDefinitionPromptSchema
{
    /// <summary>All report element types documented for LLM authoring.</summary>
    public static IReadOnlyList<ReportElementSchemaDescriptor> ElementTypes { get; } =
    [
        new(
            "textBox",
            "Static text or expression-backed text. Use text for labels/headings and expression for row values such as =Fields.Total.",
            ["id", "x", "y", "width", "height", "text or expression"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["textStyle"] = "Inline font family, size, weight, decoration and color.",
                ["horizontalAlignment"] = "left, center, right or justify.",
                ["verticalAlignment"] = "top, middle or bottom.",
                ["padding"] = "Optional left/top/right/bottom spacing.",
                ["border"] = "Optional four-sided border.",
                ["canGrow"] = "Allow the element to grow vertically during text layout.",
            }),
        new(
            "image",
            "Image rendered from a URL, embedded data URI or expression result.",
            ["id", "x", "y", "width", "height", "source"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceKind"] = "url, embedded or expression.",
                ["source"] = "URL, data URI or expression source.",
                ["contentType"] = "MIME type for embedded images.",
                ["sizing"] = "stretch, contain, cover or actualSize.",
            }),
        new(
            "shape",
            "Vector rectangle, rounded rectangle or ellipse used for panels, backgrounds and highlights.",
            ["id", "x", "y", "width", "height"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["shape"] = "rectangle, roundedRectangle or ellipse.",
                ["fillColor"] = "Optional fill color.",
                ["border"] = "Optional four-sided border.",
            }),
        new(
            "line",
            "Straight horizontal, vertical or diagonal rule with a stroke.",
            ["id", "x", "y", "width", "height"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stroke"] = "Line color and width.",
            }),
        new(
            "table",
            "Tablix-style table bound to a data set, with columns and row templates.",
            ["id", "x", "y", "width", "height", "columns", "detail"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dataSetName"] = "Name of the data set used by detail rows.",
                ["columns"] = "Column headers and widths.",
                ["header"] = "Optional header row.",
                ["groups"] = "Optional grouped row definitions.",
                ["detail"] = "Detail row template.",
                ["footer"] = "Optional footer row.",
                ["zebraStripeColor"] = "Optional odd-row background.",
            }),
        new(
            "chart",
            "Engine-rendered column, bar, line, area, pie or donut chart bound to a data set.",
            ["id", "x", "y", "width", "height", "series"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["chartType"] = "column, bar, line, area, pie or donut.",
                ["dataSetName"] = "Name of the data set used by chart series.",
                ["series"] = "Each series needs categoryExpression and valueExpression.",
                ["title"] = "Optional chart title.",
                ["categoryAxisTitle"] = "Optional category axis title.",
                ["valueAxisTitle"] = "Optional value axis title.",
                ["showLegend"] = "Whether to render the legend.",
                ["colorPalette"] = "Optional series color palette.",
            }),
        new(
            "subReport",
            "Placeholder for another report definition with parameter mappings.",
            ["id", "x", "y", "width", "height", "reportId"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reportId"] = "Referenced report identifier.",
                ["parameterMappings"] = "Target parameter name plus source expression pairs.",
            }),
    ];

    /// <summary>Returns a compact schema object intended for MCP responses and PromptHelper docs.</summary>
    public static object Build()
        => new
        {
            schemaVersion = ReportDefinition.CurrentSchemaVersion,
            serializer = "ReportDefinitionJsonSerializer with camelCase JSON, string enums and polymorphic element discriminator 'type'.",
            report = new
            {
                required = new[] { "schemaVersion", "name", "pageSetup", "bands.detail" },
                bands = new[] { "reportHeader", "pageHeader", "detail", "reportFooter", "pageFooter" },
                expressionHints = new[]
                {
                    "Use =Fields.ColumnName inside detail/table/chart contexts.",
                    "Use =Parameters.ParameterName for parameter values.",
                    "Use static text without a leading '=' for labels.",
                },
            },
            elementTypes = ElementTypes,
        };

    /// <summary>All JSON discriminators declared on <see cref="ReportElement"/>.</summary>
    public static IReadOnlySet<string> RuntimeElementDiscriminators()
        => typeof(ReportElement)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(attribute => Convert.ToString(attribute.TypeDiscriminator, System.Globalization.CultureInfo.InvariantCulture))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
}

/// <summary>AI-readable description of one report element discriminator.</summary>
public sealed record ReportElementSchemaDescriptor(
    string Type,
    string Description,
    IReadOnlyList<string> Required,
    IReadOnlyDictionary<string, string> Properties);
