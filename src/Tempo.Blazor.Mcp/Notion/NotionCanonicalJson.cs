using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal static class NotionCanonicalJson
{
    public static string ComputeRequestHash(
        NotionAtomicAuthoringRequest request,
        JsonArray operations,
        IReadOnlyList<NotionAggregateTarget>? effectiveTargets = null)
    {
        var envelope = new JsonObject
        {
            ["targets"] = new JsonArray(
                (effectiveTargets ?? request.Targets)
                    .Distinct()
                    .OrderBy(target => target.Kind)
                    .ThenBy(target => target.Id)
                    .Select(target => (JsonNode)new JsonObject
                    {
                        ["kind"] = target.Kind == NotionAggregateTargetKind.Page ? "page" : "block",
                        ["id"] = target.Id.ToString("D")
                    })
                    .ToArray()),
            ["expectedPageVersions"] = new JsonArray(
                request.ExpectedPageVersions
                    .Distinct()
                    .OrderBy(version => version.PageId)
                    .ThenBy(version => version.ConcurrencyToken, StringComparer.Ordinal)
                    .Select(version => (JsonNode)new JsonObject
                    {
                        ["pageId"] = version.PageId.ToString("D"),
                        ["concurrencyToken"] = version.ConcurrencyToken
                    })
                    .ToArray()),
            ["operations"] = operations.DeepClone()
        };

        return Hash(envelope);
    }

    public static string ComputeContentDigest(NotionPageSnapshot snapshot)
    {
        var node = JsonSerializer.SerializeToNode(snapshot, NotionAggregateJson.Options)?.AsObject()
            ?? throw new JsonException("Could not serialize Notion page snapshot.");
        node.Remove("concurrencyToken");
        node.Remove("digest");
        return Hash(node);
    }

    private static string Hash(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            WriteCanonical(writer, document.RootElement);
        }

        return $"sha256:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                WriteCanonicalNumber(writer, element);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new JsonException($"Unsupported JSON token '{element.ValueKind}' in canonical request.");
        }
    }

    private static void WriteCanonicalNumber(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.TryGetDecimal(out var decimalValue))
        {
            if (decimalValue == decimal.Truncate(decimalValue) &&
                decimalValue >= long.MinValue &&
                decimalValue <= long.MaxValue)
            {
                writer.WriteNumberValue(decimal.ToInt64(decimalValue));
                return;
            }

            writer.WriteNumberValue(decimalValue);
            return;
        }

        if (element.TryGetDouble(out var doubleValue) && double.IsFinite(doubleValue))
        {
            writer.WriteNumberValue(doubleValue);
            return;
        }

        throw new JsonException($"JSON number '{element.GetRawText()}' cannot be canonicalized.");
    }
}
