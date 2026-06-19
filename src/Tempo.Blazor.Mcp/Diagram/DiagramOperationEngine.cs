using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>Outcome of applying a granular operation batch to a diagram document.</summary>
public sealed record DiagramOperationResult(
    bool Success, IReadOnlyList<string> Errors, int Applied, IReadOnlyList<string> CreatedIds);

/// <summary>Applies ordered diagram edit operations to a working copy of a document.</summary>
public static class DiagramOperationEngine
{
    public static DiagramOperationResult Apply(DiagramDocument document, string operationsJson)
    {
        if (!McpJsonHelpers.TryParseOperationArray(operationsJson, out var ops, out var errors) || ops is null)
        {
            return new DiagramOperationResult(false, errors, 0, []);
        }

        document.EnsurePages();
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
                "setTitle" => SetTitle(document, op),
                "addPage" => AddPage(document, op, created),
                "updatePage" => UpdatePage(document, op),
                "removePage" => RemovePage(document, op),
                "setActivePage" => SetActivePage(document, op),
                "setCanvasSize" => SetCanvasSize(document, op),
                "addNode" => AddNode(document, op, created),
                "updateNode" => UpdateNode(document, op),
                "removeNode" => RemoveNode(document, op),
                "addEdge" => AddEdge(document, op, created),
                "updateEdge" => UpdateEdge(document, op),
                "removeEdge" => RemoveEdge(document, op),
                "addLayer" => AddLayer(document, op, created),
                "updateLayer" => UpdateLayer(document, op),
                "removeLayer" => RemoveLayer(document, op),
                "reorderLayers" => ReorderLayers(document, op),
                "moveItemsToLayer" => MoveItemsToLayer(document, op),
                _ => $"unknown op '{name}'."
            };

            if (error is not null)
            {
                return Fail(i, error, created);
            }
        }

        document.ModifiedAt = DateTime.UtcNow;
        return new DiagramOperationResult(true, [], ops.Count, created);

        static DiagramOperationResult Fail(int index, string message, List<string> created)
            => new(false, [$"operations[{index}]: {message}"], 0, created);
    }

    private static DiagramPage? ResolvePage(DiagramDocument document, JsonObject op, out string? error)
    {
        error = null;
        document.EnsurePages();

        if (op["pageId"]?.GetValue<string>() is { Length: > 0 } pageId)
        {
            var page = document.Pages.FirstOrDefault(p => p.Id == pageId);
            if (page is null)
            {
                error = $"page '{pageId}' not found.";
            }
            return page;
        }

        if (op["pageIndex"] is JsonValue indexValue && indexValue.TryGetValue<int>(out var index))
        {
            if (index < 0 || index >= document.Pages.Count)
            {
                error = $"pageIndex {index} is outside the page range.";
                return null;
            }
            return document.Pages[index];
        }

        return document.ActivePage;
    }

    private static string? SetTitle(DiagramDocument document, JsonObject op)
    {
        var title = op["title"]?.GetValue<string>();
        if (title is null)
        {
            return "setTitle requires 'title'.";
        }

        document.Title = title;
        return null;
    }

    private static string? AddPage(DiagramDocument document, JsonObject op, List<string> created)
    {
        var page = BuildFromOperation("page", op, new DiagramPage(), ["op", "page"], out var error);
        if (page is null)
        {
            return error;
        }

        document.Pages.Add(page);
        if (op["activate"] is JsonValue activateValue
            && activateValue.TryGetValue<bool>(out var activate)
            && activate)
        {
            document.ActivePageIndex = document.Pages.Count - 1;
        }

        created.Add(page.Id);
        return null;
    }

    private static string? UpdatePage(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var updated = Patch(page, op, ["op", "page", "pageId", "pageIndex"], out error);
        if (updated is null)
        {
            return error;
        }

        var index = document.Pages.IndexOf(page);
        document.Pages[index] = updated;
        return null;
    }

    private static string? RemovePage(DiagramDocument document, JsonObject op)
    {
        var pageId = op["pageId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return "removePage requires 'pageId'.";
        }

        var page = document.Pages.FirstOrDefault(p => p.Id == pageId);
        if (page is null)
        {
            return $"page '{pageId}' not found.";
        }

        document.Pages.Remove(page);
        document.EnsurePages();
        return null;
    }

    private static string? SetActivePage(DiagramDocument document, JsonObject op)
    {
        if (op["pageId"]?.GetValue<string>() is { Length: > 0 } pageId)
        {
            var index = document.Pages.FindIndex(p => p.Id == pageId);
            if (index < 0)
            {
                return $"page '{pageId}' not found.";
            }

            document.ActivePageIndex = index;
            return null;
        }

        if (op["pageIndex"] is JsonValue indexValue && indexValue.TryGetValue<int>(out var pageIndex))
        {
            if (pageIndex < 0 || pageIndex >= document.Pages.Count)
            {
                return $"pageIndex {pageIndex} is outside the page range.";
            }

            document.ActivePageIndex = pageIndex;
            return null;
        }

        return "setActivePage requires 'pageId' or 'pageIndex'.";
    }

    private static string? SetCanvasSize(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        if (TryDouble(op, "width", out var width))
        {
            page.Width = width;
        }
        if (TryDouble(op, "height", out var height))
        {
            page.Height = height;
        }

        return null;
    }

    private static string? AddNode(DiagramDocument document, JsonObject op, List<string> created)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var node = BuildFromOperation("node", op, new DiagramNode(), ["op", "pageId", "pageIndex", "node"], out error);
        if (node is null)
        {
            return error;
        }

        page.Nodes.Add(node);
        created.Add(node.Id);
        return null;
    }

    private static string? UpdateNode(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "updateNode requires 'id'.";
        }

        var index = page.Nodes.FindIndex(n => n.Id == id);
        if (index < 0)
        {
            return $"node '{id}' not found.";
        }

        var node = page.Nodes[index];
        if (op["node"] is JsonObject nested)
        {
            node = Patch(node, nested, [], out error);
            if (node is null)
            {
                return error;
            }
        }

        node = Patch(node, op, ["op", "pageId", "pageIndex", "id", "node"], out error);
        if (node is null)
        {
            return error;
        }

        node.Id = id;
        page.Nodes[index] = node;
        return null;
    }

    private static string? RemoveNode(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "removeNode requires 'id'.";
        }

        var removed = page.Nodes.RemoveAll(n => n.Id == id);
        if (removed == 0)
        {
            return $"node '{id}' not found.";
        }

        page.Edges.RemoveAll(e => e.SourceNodeId == id || e.TargetNodeId == id);
        return null;
    }

    private static string? AddEdge(DiagramDocument document, JsonObject op, List<string> created)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var edge = BuildFromOperation("edge", op, new DiagramEdge(), ["op", "pageId", "pageIndex", "edge"], out error);
        if (edge is null)
        {
            return error;
        }

        page.Edges.Add(edge);
        created.Add(edge.Id);
        return null;
    }

    private static string? UpdateEdge(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "updateEdge requires 'id'.";
        }

        var index = page.Edges.FindIndex(e => e.Id == id);
        if (index < 0)
        {
            return $"edge '{id}' not found.";
        }

        var edge = page.Edges[index];
        if (op["edge"] is JsonObject nested)
        {
            edge = Patch(edge, nested, [], out error);
            if (edge is null)
            {
                return error;
            }
        }

        edge = Patch(edge, op, ["op", "pageId", "pageIndex", "id", "edge"], out error);
        if (edge is null)
        {
            return error;
        }

        edge.Id = id;
        page.Edges[index] = edge;
        return null;
    }

    private static string? RemoveEdge(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "removeEdge requires 'id'.";
        }

        var removed = page.Edges.RemoveAll(e => e.Id == id);
        return removed == 0 ? $"edge '{id}' not found." : null;
    }

    private static string? AddLayer(DiagramDocument document, JsonObject op, List<string> created)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var layer = BuildFromOperation("layer", op, new DiagramLayer(), ["op", "pageId", "pageIndex", "layer"], out error);
        if (layer is null)
        {
            return error;
        }

        page.Layers.Add(layer);
        created.Add(layer.Id);
        return null;
    }

    private static string? UpdateLayer(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "updateLayer requires 'id'.";
        }

        var index = page.Layers.FindIndex(l => l.Id == id);
        if (index < 0)
        {
            return $"layer '{id}' not found.";
        }

        var layer = page.Layers[index];
        if (op["layer"] is JsonObject nested)
        {
            layer = Patch(layer, nested, [], out error);
            if (layer is null)
            {
                return error;
            }
        }

        layer = Patch(layer, op, ["op", "pageId", "pageIndex", "id", "layer"], out error);
        if (layer is null)
        {
            return error;
        }

        layer.Id = id;
        page.Layers[index] = layer;
        return null;
    }

    private static string? RemoveLayer(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "removeLayer requires 'id'.";
        }

        var removed = page.Layers.RemoveAll(l => l.Id == id);
        if (removed == 0)
        {
            return $"layer '{id}' not found.";
        }

        foreach (var node in page.Nodes.Where(n => n.LayerId == id))
        {
            node.LayerId = null;
        }
        foreach (var edge in page.Edges.Where(e => e.LayerId == id))
        {
            edge.LayerId = null;
        }

        return null;
    }

    private static string? ReorderLayers(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        if (op["layerIds"] is not JsonArray ids)
        {
            return "reorderLayers requires 'layerIds' array.";
        }

        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                return $"layerIds[{i}]: layer id is required.";
            }

            var layer = page.Layers.FirstOrDefault(l => l.Id == id);
            if (layer is null)
            {
                return $"layer '{id}' not found.";
            }

            layer.Order = i;
        }

        page.Layers = page.Layers.OrderBy(l => l.Order).ThenBy(l => l.Name, StringComparer.Ordinal).ToList();
        return null;
    }

    private static string? MoveItemsToLayer(DiagramDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null)
        {
            return error ?? "page not found.";
        }

        var layerId = op["layerId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return "moveItemsToLayer requires 'layerId'.";
        }
        if (page.Layers.All(l => l.Id != layerId))
        {
            return $"layer '{layerId}' not found.";
        }

        foreach (var nodeId in ReadStringArray(op, "nodeIds"))
        {
            var node = page.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is null)
            {
                return $"node '{nodeId}' not found.";
            }
            node.LayerId = layerId;
        }

        foreach (var edgeId in ReadStringArray(op, "edgeIds"))
        {
            var edge = page.Edges.FirstOrDefault(e => e.Id == edgeId);
            if (edge is null)
            {
                return $"edge '{edgeId}' not found.";
            }
            edge.LayerId = layerId;
        }

        return null;
    }

    private static T? BuildFromOperation<T>(
        string nestedName,
        JsonObject op,
        T fallback,
        string[] excluded,
        out string? error)
    {
        error = null;
        var value = fallback;
        if (op.TryGetPropertyValue(nestedName, out var nested))
        {
            if (nested is not JsonObject nestedObject)
            {
                error = $"{nestedName} must be an object.";
                return default;
            }

            value = Deserialize<T>(nestedObject, out error);
            if (value is null)
            {
                return default;
            }
        }

        return Patch(value, op, excluded, out error);
    }

    private static T? Patch<T>(T value, JsonObject patch, string[] excluded, out string? error)
    {
        error = null;
        var ignored = excluded.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var node = JsonSerializer.SerializeToNode(value, DiagramJsonOptions.Default) as JsonObject;
        if (node is null)
        {
            error = $"could not serialize {typeof(T).Name}.";
            return default;
        }

        foreach (var (key, patchValue) in patch)
        {
            if (!ignored.Contains(key))
            {
                node[key] = patchValue?.DeepClone();
            }
        }

        return Deserialize<T>(node, out error);
    }

    private static T? Deserialize<T>(JsonObject obj, out string? error)
    {
        try
        {
            error = null;
            return JsonSerializer.Deserialize<T>(obj.ToJsonString(), DiagramJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            error = $"invalid {typeof(T).Name} JSON ({ex.Message}).";
            return default;
        }
    }

    private static IEnumerable<string> ReadStringArray(JsonObject op, string key)
    {
        if (op[key] is not JsonArray array)
        {
            yield break;
        }

        foreach (var item in array)
        {
            if (item is not null)
            {
                yield return item.GetValue<string>();
            }
        }
    }

    private static bool TryDouble(JsonObject op, string key, out double value)
    {
        value = 0;
        if (op[key] is JsonValue v && v.TryGetValue<double>(out var d))
        {
            value = d;
            return true;
        }
        return false;
    }
}
