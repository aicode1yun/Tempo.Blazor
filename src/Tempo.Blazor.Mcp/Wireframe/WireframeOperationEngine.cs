using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>Outcome of applying a granular operation batch to a wireframe document.</summary>
public sealed record WireframeOperationResult(
    bool Success, IReadOnlyList<string> Errors, int Applied, IReadOnlyList<string> CreatedIds);

/// <summary>
/// Applies a granular operation batch (add/update/remove elements, connectors and pages, set title
/// and canvas size) to a wireframe document. Operations are applied in order; the first failure
/// aborts the batch (callers apply to a copy and only persist on success).
/// </summary>
public static class WireframeOperationEngine
{
    public static WireframeOperationResult Apply(WireframeDocument document, string operationsJson)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(operationsJson);
        }
        catch (JsonException ex)
        {
            return new WireframeOperationResult(false, [$"operations: invalid JSON ({ex.Message})."], 0, []);
        }

        if (parsed is not JsonArray ops)
        {
            return new WireframeOperationResult(false, ["operations: expected a JSON array of operations."], 0, []);
        }

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
                "setCanvasSize" => SetCanvasSize(document, op),
                "addElement" => AddElement(document, op, created),
                "updateElement" => UpdateElement(document, op),
                "removeElement" => RemoveElement(document, op),
                "addConnector" => AddConnector(document, op, created),
                "updateConnector" => UpdateConnector(document, op),
                "removeConnector" => RemoveConnector(document, op),
                _ => $"unknown op '{name}'."
            };

            if (error is not null)
            {
                return Fail(i, error, created);
            }
        }

        return new WireframeOperationResult(true, [], ops.Count, created);

        static WireframeOperationResult Fail(int index, string message, List<string> created)
            => new(false, [$"operations[{index}]: {message}"], 0, created);
    }

    // ── Page resolution ──────────────────────────────────────────────────────────

    private static WireframePage? ResolvePage(WireframeDocument document, JsonObject op, out string? error)
    {
        error = null;
        var pageId = op["pageId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(pageId))
        {
            document.EnsureActivePage();
            return document.ActivePage;
        }

        var page = document.Pages.FirstOrDefault(p => p.Id == pageId);
        if (page is null)
        {
            error = $"page '{pageId}' not found.";
        }
        return page;
    }

    // ── Operations ───────────────────────────────────────────────────────────────

    private static string? SetTitle(WireframeDocument document, JsonObject op)
    {
        var title = op["title"]?.GetValue<string>();
        if (title is null)
        {
            return "setTitle requires 'title'.";
        }
        document.Title = title;
        return null;
    }

    private static string? AddPage(WireframeDocument document, JsonObject op, List<string> created)
    {
        var page = new WireframePage();
        if (op["name"]?.GetValue<string>() is { } name) page.Name = name;
        if (TryDouble(op, "width", out var w)) page.Width = w;
        if (TryDouble(op, "height", out var h)) page.Height = h;
        document.Pages.Add(page);
        created.Add(page.Id);
        return null;
    }

    private static string? UpdatePage(WireframeDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        if (op["name"]?.GetValue<string>() is { } name) page.Name = name;
        if (TryDouble(op, "width", out var w)) page.Width = w;
        if (TryDouble(op, "height", out var h)) page.Height = h;
        return null;
    }

    private static string? RemovePage(WireframeDocument document, JsonObject op)
    {
        var pageId = op["pageId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(pageId)) return "removePage requires 'pageId'.";
        var page = document.Pages.FirstOrDefault(p => p.Id == pageId);
        if (page is null) return $"page '{pageId}' not found.";
        document.Pages.Remove(page);
        return null;
    }

    private static string? SetCanvasSize(WireframeDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        if (TryDouble(op, "width", out var w)) page.Width = w;
        if (TryDouble(op, "height", out var h)) page.Height = h;
        return null;
    }

    private static string? AddElement(WireframeDocument document, JsonObject op, List<string> created)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";

        var type = op["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type)) return "addElement requires 'type'.";

        var el = new WireframeElement { Type = type };
        if (op["id"]?.GetValue<string>() is { Length: > 0 } id) el.Id = id;
        if (TryDouble(op, "x", out var x)) el.X = x;
        if (TryDouble(op, "y", out var y)) el.Y = y;
        if (TryDouble(op, "w", out var w)) el.W = w;
        if (TryDouble(op, "h", out var h)) el.H = h;
        ApplyProps(el, op["props"]);

        page.Elements.Add(el);
        created.Add(el.Id);
        return null;
    }

    private static string? UpdateElement(WireframeDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id)) return "updateElement requires 'id'.";
        var el = page.Elements.FirstOrDefault(e => e.Id == id);
        if (el is null) return $"element '{id}' not found.";

        if (op["type"]?.GetValue<string>() is { Length: > 0 } type) el.Type = type;
        if (TryDouble(op, "x", out var x)) el.X = x;
        if (TryDouble(op, "y", out var y)) el.Y = y;
        if (TryDouble(op, "w", out var w)) el.W = w;
        if (TryDouble(op, "h", out var h)) el.H = h;
        ApplyProps(el, op["props"]);
        return null;
    }

    private static string? RemoveElement(WireframeDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id)) return "removeElement requires 'id'.";
        var removed = page.Elements.RemoveAll(e => e.Id == id);
        return removed == 0 ? $"element '{id}' not found." : null;
    }

    private static string? AddConnector(WireframeDocument document, JsonObject op, List<string> created)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        var from = op["fromId"]?.GetValue<string>();
        var to = op["toId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return "addConnector requires 'fromId' and 'toId'.";
        var c = new WireframeConnector { FromId = from, ToId = to };
        if (op["label"]?.GetValue<string>() is { } label) c.Label = label;
        page.Connectors.Add(c);
        created.Add(c.Id);
        return null;
    }

    private static string? UpdateConnector(WireframeDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id)) return "updateConnector requires 'id'.";
        var c = page.Connectors.FirstOrDefault(x => x.Id == id);
        if (c is null) return $"connector '{id}' not found.";
        if (op["fromId"]?.GetValue<string>() is { Length: > 0 } from) c.FromId = from;
        if (op["toId"]?.GetValue<string>() is { Length: > 0 } to) c.ToId = to;
        if (op.ContainsKey("label")) c.Label = op["label"]?.GetValue<string>();
        return null;
    }

    private static string? RemoveConnector(WireframeDocument document, JsonObject op)
    {
        var page = ResolvePage(document, op, out var error);
        if (page is null) return error ?? "page not found.";
        var id = op["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id)) return "removeConnector requires 'id'.";
        var removed = page.Connectors.RemoveAll(x => x.Id == id);
        return removed == 0 ? $"connector '{id}' not found." : null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void ApplyProps(WireframeElement el, JsonNode? props)
    {
        if (props is not JsonObject obj) return;
        foreach (var (key, value) in obj)
        {
            el.Props[key] = value is null
                ? JsonSerializer.SerializeToElement<object?>(null)
                : JsonSerializer.Deserialize<JsonElement>(value.ToJsonString());
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
