using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>Outcome of applying a granular operation batch to a report definition.</summary>
public sealed record ReportOperationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    int Applied,
    IReadOnlyList<string> CreatedIds,
    ReportDefinition? Definition);

/// <summary>Applies ordered MCP report edit operations to a working copy.</summary>
public static class ReportOperationEngine
{
    private static readonly HashSet<string> ReservedElementKeys = new(StringComparer.Ordinal)
    {
        "op", "band", "id", "element", "patch", "w", "h"
    };

    /// <summary>Applies report edit operations to a copy of <paramref name="definition"/>.</summary>
    public static ReportOperationResult Apply(ReportDefinition definition, string operationsJson)
    {
        if (!McpJsonHelpers.TryParseOperationArray(operationsJson, out var ops, out var errors) || ops is null)
        {
            return new ReportOperationResult(false, errors, 0, [], null);
        }

        var working = ReportDefinitionJsonSerializer.Deserialize(ReportDefinitionJsonSerializer.Serialize(definition));
        var created = new List<string>();
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] is not JsonObject op)
            {
                return Fail(i, "operation must be an object.", created);
            }

            var name = op["op"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Fail(i, "missing 'op' discriminator.", created);
            }

            var error = name switch
            {
                "setName" => SetName(ref working, op),
                "setDescription" => SetDescription(ref working, op),
                "setPageSetup" => SetPageSetup(ref working, op),
                "setBandHeight" => SetBandHeight(ref working, op),
                "clearBand" => ClearBand(ref working, op),
                "addElement" => AddElement(ref working, op, created),
                "updateElement" => UpdateElement(ref working, op),
                "replaceElement" => ReplaceElement(ref working, op),
                "removeElement" => RemoveElement(ref working, op),
                _ => $"unknown op '{name}'."
            };

            if (error is not null)
            {
                return Fail(i, error, created);
            }
        }

        return new ReportOperationResult(true, [], ops.Count, created, working);

        ReportOperationResult Fail(int index, string message, IReadOnlyList<string> createdIds)
            => new(false, [$"operations[{index}]: {message}"], 0, createdIds, null);
    }

    private static string? SetName(ref ReportDefinition definition, JsonObject op)
    {
        var name = op["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "setName requires 'name'.";
        }

        definition = definition with { Name = name.Trim() };
        return null;
    }

    private static string? SetDescription(ref ReportDefinition definition, JsonObject op)
    {
        definition = definition with { Description = op["description"]?.GetValue<string>() };
        return null;
    }

    private static string? SetPageSetup(ref ReportDefinition definition, JsonObject op)
    {
        if (op["pageSetup"] is JsonObject setupObject)
        {
            var setup = setupObject.Deserialize<ReportPageSetup>(ReportDefinitionJsonSerializer.Options);
            if (setup is null)
            {
                return "setPageSetup could not deserialize 'pageSetup'.";
            }

            definition = definition with { PageSetup = setup };
            return null;
        }

        var current = definition.PageSetup;
        var size = current.PageSize;
        if (TryDouble(op, "width", out var width))
        {
            size = size with { Width = width };
        }
        if (TryDouble(op, "height", out var height))
        {
            size = size with { Height = height };
        }

        var margins = current.Margins;
        if (op["margins"] is JsonObject marginsObject)
        {
            margins = marginsObject.Deserialize<ReportThickness>(ReportDefinitionJsonSerializer.Options) ?? margins;
        }

        definition = definition with { PageSetup = current with { PageSize = size, Margins = margins } };
        return null;
    }

    private static string? SetBandHeight(ref ReportDefinition definition, JsonObject op)
    {
        if (!TryDouble(op, "height", out var height))
        {
            return "setBandHeight requires numeric 'height'.";
        }

        var bandName = BandName(op);
        var band = ResolveBand(definition, bandName, create: true, out var error);
        if (band is null)
        {
            return error;
        }

        definition = StoreBand(definition, bandName, band with { Height = height });
        return null;
    }

    private static string? ClearBand(ref ReportDefinition definition, JsonObject op)
    {
        var bandName = BandName(op);
        var band = ResolveBand(definition, bandName, create: true, out var error);
        if (band is null)
        {
            return error;
        }

        definition = StoreBand(definition, bandName, band with { Elements = [] });
        return null;
    }

    private static string? AddElement(ref ReportDefinition definition, JsonObject op, List<string> created)
    {
        var bandName = BandName(op);
        var band = ResolveBand(definition, bandName, create: true, out var error);
        if (band is null)
        {
            return error;
        }

        var elementObject = BuildElementObject(op, requireType: true, out error);
        if (elementObject is null)
        {
            return error;
        }

        var element = DeserializeElement(elementObject, out error);
        if (element is null)
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(element.Id))
        {
            elementObject["id"] = CreateElementId(elementObject["type"]?.GetValue<string>() ?? "element", band);
            element = DeserializeElement(elementObject, out error);
            if (element is null)
            {
                return error;
            }
        }

        if (band.Elements.Any(existing => string.Equals(existing.Id, element.Id, StringComparison.Ordinal)))
        {
            return $"element '{element.Id}' already exists in band '{bandName}'.";
        }

        band.Elements.Add(element);
        created.Add(element.Id);
        definition = StoreBand(definition, bandName, GrowBandToFit(band, element));
        return null;
    }

    private static string? UpdateElement(ref ReportDefinition definition, JsonObject op)
    {
        var bandName = BandName(op);
        var band = ResolveBand(definition, bandName, create: false, out var error);
        if (band is null)
        {
            return error;
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "updateElement requires 'id'.";
        }

        var index = band.Elements.FindIndex(element => string.Equals(element.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return $"element '{id}' not found in band '{bandName}'.";
        }

        var root = JsonNode.Parse(JsonSerializer.Serialize(band.Elements[index], ReportDefinitionJsonSerializer.Options)) as JsonObject;
        if (root is null)
        {
            return $"element '{id}' could not be serialized for patching.";
        }

        var patch = op["patch"] as JsonObject ?? DirectPatch(op);
        ApplyPatch(root, patch);
        NormalizeBounds(root);

        var updated = DeserializeElement(root, out error);
        if (updated is null)
        {
            return error;
        }

        band.Elements[index] = updated;
        definition = StoreBand(definition, bandName, GrowBandToFit(band, updated));
        return null;
    }

    private static string? ReplaceElement(ref ReportDefinition definition, JsonObject op)
    {
        var bandName = BandName(op);
        var band = ResolveBand(definition, bandName, create: false, out var error);
        if (band is null)
        {
            return error;
        }

        var id = op["id"]?.GetValue<string>();
        var elementObject = BuildElementObject(op, requireType: true, out error);
        if (elementObject is null)
        {
            return error;
        }
        if (!string.IsNullOrWhiteSpace(id) && !elementObject.ContainsKey("id"))
        {
            elementObject["id"] = id;
        }

        var element = DeserializeElement(elementObject, out error);
        if (element is null)
        {
            return error;
        }

        var index = band.Elements.FindIndex(existing => string.Equals(existing.Id, element.Id, StringComparison.Ordinal));
        if (index < 0)
        {
            return $"element '{element.Id}' not found in band '{bandName}'.";
        }

        band.Elements[index] = element;
        definition = StoreBand(definition, bandName, GrowBandToFit(band, element));
        return null;
    }

    private static string? RemoveElement(ref ReportDefinition definition, JsonObject op)
    {
        var bandName = BandName(op);
        var band = ResolveBand(definition, bandName, create: false, out var error);
        if (band is null)
        {
            return error;
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "removeElement requires 'id'.";
        }

        var removed = band.Elements.RemoveAll(element => string.Equals(element.Id, id, StringComparison.Ordinal));
        if (removed == 0)
        {
            return $"element '{id}' not found in band '{bandName}'.";
        }

        definition = StoreBand(definition, bandName, band);
        return null;
    }

    private static string BandName(JsonObject op)
        => op["band"]?.GetValue<string>() ?? "detail";

    private static ReportBand? ResolveBand(ReportDefinition definition, string bandName, bool create, out string? error)
    {
        error = null;
        var band = NormalizeBandName(bandName) switch
        {
            "reportHeader" => definition.Bands.ReportHeader,
            "reportFooter" => definition.Bands.ReportFooter,
            "pageHeader" => definition.Bands.PageHeader,
            "pageFooter" => definition.Bands.PageFooter,
            "detail" => definition.Bands.Detail,
            _ => null,
        };

        if (band is not null)
        {
            return band;
        }

        if (!IsSupportedBand(bandName))
        {
            error = $"band '{bandName}' is not supported. Use reportHeader, pageHeader, detail, reportFooter or pageFooter.";
            return null;
        }

        if (!create)
        {
            error = $"band '{bandName}' does not exist.";
            return null;
        }

        return new ReportBand { Kind = KindForBand(bandName), Height = 48 };
    }

    private static ReportDefinition StoreBand(ReportDefinition definition, string bandName, ReportBand band)
    {
        var normalized = NormalizeBandName(bandName);
        var bands = normalized switch
        {
            "reportHeader" => definition.Bands with { ReportHeader = band with { Kind = ReportBandKind.ReportHeader } },
            "reportFooter" => definition.Bands with { ReportFooter = band with { Kind = ReportBandKind.ReportFooter } },
            "pageHeader" => definition.Bands with { PageHeader = band with { Kind = ReportBandKind.PageHeader } },
            "pageFooter" => definition.Bands with { PageFooter = band with { Kind = ReportBandKind.PageFooter } },
            "detail" => definition.Bands with { Detail = band with { Kind = ReportBandKind.Detail } },
            _ => definition.Bands,
        };
        return definition with { Bands = bands };
    }

    private static ReportBand GrowBandToFit(ReportBand band, ReportElement element)
        => band with { Height = Math.Max(band.Height, element.Y + element.Height) };

    private static string NormalizeBandName(string value)
        => value.Trim() switch
        {
            "report-header" or "report_header" or "ReportHeader" => "reportHeader",
            "report-footer" or "report_footer" or "ReportFooter" => "reportFooter",
            "page-header" or "page_header" or "PageHeader" => "pageHeader",
            "page-footer" or "page_footer" or "PageFooter" => "pageFooter",
            "Detail" => "detail",
            var other => other,
        };

    private static bool IsSupportedBand(string value)
        => NormalizeBandName(value) is "reportHeader" or "reportFooter" or "pageHeader" or "pageFooter" or "detail";

    private static ReportBandKind KindForBand(string value)
        => NormalizeBandName(value) switch
        {
            "reportHeader" => ReportBandKind.ReportHeader,
            "reportFooter" => ReportBandKind.ReportFooter,
            "pageHeader" => ReportBandKind.PageHeader,
            "pageFooter" => ReportBandKind.PageFooter,
            _ => ReportBandKind.Detail,
        };

    private static JsonObject? BuildElementObject(JsonObject op, bool requireType, out string? error)
    {
        error = null;
        JsonObject root;
        if (op["element"] is JsonObject elementObject)
        {
            root = (JsonObject)elementObject.DeepClone();
        }
        else
        {
            root = DirectPatch(op);
        }

        NormalizeBounds(root);
        if (requireType && string.IsNullOrWhiteSpace(root["type"]?.GetValue<string>()))
        {
            error = "element payload requires 'type'.";
            return null;
        }

        return root;
    }

    private static JsonObject DirectPatch(JsonObject op)
    {
        var patch = new JsonObject();
        foreach (var (key, value) in op)
        {
            if (!ReservedElementKeys.Contains(key))
            {
                patch[key] = value?.DeepClone();
            }
        }

        return patch;
    }

    private static void ApplyPatch(JsonObject target, JsonObject patch)
    {
        foreach (var (key, value) in patch)
        {
            target[key] = value?.DeepClone();
        }
    }

    private static void NormalizeBounds(JsonObject root)
    {
        if (root.TryGetPropertyValue("w", out var width))
        {
            root["width"] = width?.DeepClone();
            root.Remove("w");
        }
        if (root.TryGetPropertyValue("h", out var height))
        {
            root["height"] = height?.DeepClone();
            root.Remove("h");
        }
    }

    private static ReportElement? DeserializeElement(JsonObject element, out string? error)
    {
        error = null;
        try
        {
            return element.Deserialize<ReportElement>(ReportDefinitionJsonSerializer.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            error = $"element could not be parsed: {ex.Message}";
            return null;
        }
    }

    private static string CreateElementId(string type, ReportBand band)
    {
        var prefix = string.IsNullOrWhiteSpace(type) ? "element" : type;
        prefix = char.ToLowerInvariant(prefix[0]) + prefix[1..];
        var index = band.Elements.Count + 1;
        string id;
        do
        {
            id = $"{prefix}-{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            index++;
        }
        while (band.Elements.Any(element => string.Equals(element.Id, id, StringComparison.Ordinal)));

        return id;
    }

    private static bool TryDouble(JsonObject op, string key, out double value)
    {
        value = 0;
        if (op[key] is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var number))
        {
            value = number;
            return true;
        }

        return false;
    }
}
