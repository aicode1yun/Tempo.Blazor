using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tempo.Blazor.E2E.Mcp;

/// <summary>One tool as reported by <c>tools/list</c>.</summary>
public sealed record McpTool(string Name, string? Description);

/// <summary>
/// Minimal MCP streamable-HTTP JSON-RPC client for E2E tests: <c>initialize</c>, <c>tools/list</c>
/// and <c>tools/call</c>, with Server-Sent-Events response parsing. No LLM involved.
/// </summary>
public sealed class McpJsonRpcClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private string? _sessionId;
    private int _nextId;

    public McpJsonRpcClient(HttpClient http, Uri endpoint)
    {
        _http = http;
        _endpoint = endpoint;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await SendAsync("initialize", new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "Tempo.Blazor.E2E", version = "1.0.0" }
        }, ct);

        // Complete the MCP handshake so tools become available on this session.
        await SendNotificationAsync("notifications/initialized", ct);
    }

    private async Task SendNotificationAsync(string method, CancellationToken ct)
    {
        var payload = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload.ToJsonString(Json), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (_sessionId is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        }
        using var response = await _http.SendAsync(request, ct);
        _ = await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await SendAsync("tools/list", new { }, ct);
        var tools = new List<McpTool>();
        if (result.TryGetProperty("tools", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in arr.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var desc = t.TryGetProperty("description", out var d) ? d.GetString() : null;
                tools.Add(new McpTool(name!, desc));
            }
        }
        return tools;
    }

    /// <summary>Calls a tool and returns the parsed JSON envelope the tool produced (its text content).</summary>
    public async Task<JsonElement> CallToolAsync(string name, object? arguments = null, CancellationToken ct = default)
    {
        var result = await SendAsync("tools/call", new { name, arguments = arguments ?? new { } }, ct);
        var text = ReadFirstText(result);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"tools/call '{name}' returned no text content.");
        }
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    private async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId);
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };
        if (parameters is not null)
        {
            payload["params"] = JsonSerializer.SerializeToNode(parameters, Json);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload.ToJsonString(Json), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (_sessionId is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        }

        using var response = await _http.SendAsync(request, ct);
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sid))
        {
            _sessionId = sid.FirstOrDefault();
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"MCP {method} HTTP {(int)response.StatusCode}: {body}");
        }

        var envelope = ParseEnvelope(body, response.Content.Headers.ContentType?.MediaType);
        if (envelope.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP {method} error: {error.GetRawText()}");
        }
        if (!envelope.TryGetProperty("result", out var result))
        {
            throw new InvalidOperationException($"MCP {method} response had no result: {body}");
        }
        return result.Clone();
    }

    internal static JsonElement ParseEnvelope(string body, string? contentType)
    {
        var json = (contentType?.Contains("event-stream", StringComparison.OrdinalIgnoreCase) == true
                    || body.TrimStart().StartsWith("event:", StringComparison.OrdinalIgnoreCase)
                    || body.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            ? ExtractSseData(body)
            : body.Trim();

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string ExtractSseData(string body)
    {
        var sb = new StringBuilder();
        foreach (var raw in body.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                var json = sb.ToString().Trim();
                if (json.StartsWith('{') || json.StartsWith('['))
                {
                    return json;
                }
                sb.Clear();
                continue;
            }
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line["data:".Length..].TrimStart());
            }
        }

        var trailing = sb.ToString().Trim();
        if (trailing.StartsWith('{') || trailing.StartsWith('['))
        {
            return trailing;
        }
        throw new InvalidOperationException("MCP event-stream response contained no JSON data.");
    }

    internal static string? ReadFirstText(JsonElement callResult)
    {
        if (callResult.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                    && block.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }
        return null;
    }
}
