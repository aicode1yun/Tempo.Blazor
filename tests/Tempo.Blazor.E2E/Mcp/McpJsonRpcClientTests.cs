using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E.Mcp;

/// <summary>Unit tests for the MCP client's response parsing (no server needed).</summary>
[TestClass]
public class McpJsonRpcClientTests
{
    [TestMethod]
    public void ParseEnvelope_PlainJson_ReturnsRoot()
    {
        var env = McpJsonRpcClient.ParseEnvelope("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"ok\":true}}", "application/json");

        Assert.IsTrue(env.GetProperty("result").GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public void ParseEnvelope_EventStream_ExtractsDataJson()
    {
        var sse = "event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"value\":42}}\n\n";

        var env = McpJsonRpcClient.ParseEnvelope(sse, "text/event-stream");

        Assert.AreEqual(42, env.GetProperty("result").GetProperty("value").GetInt32());
    }

    [TestMethod]
    public void ReadFirstText_ReturnsTextContent()
    {
        var result = JsonDocument.Parse(
            "{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"success\\\":true}\"}]}").RootElement;

        var text = McpJsonRpcClient.ReadFirstText(result);

        Assert.AreEqual("{\"success\":true}", text);
    }
}
