using System.Text.Json;
using Tempo.Blazor.Mcp;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for the success/error JSON envelope (also the package build smoke test).</summary>
public class McpToolResultsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Success_WithoutData_HasSuccessTrue()
    {
        var root = Parse(McpToolResults.Success());

        root.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Success_MergesDataAtTopLevel()
    {
        var id = Guid.NewGuid();
        var root = Parse(McpToolResults.Success(new { id, count = 3 }));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("id").GetGuid().Should().Be(id);
        root.GetProperty("count").GetInt32().Should().Be(3);
    }

    [Fact]
    public void Failure_HasSuccessFalse_WithErrorAndMessage()
    {
        var root = Parse(McpToolResults.Failure(McpToolResults.NotFound, "no such doc"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
        root.GetProperty("message").GetString().Should().Be("no such doc");
    }

    [Fact]
    public void Failure_IncludesValidationErrors_WhenProvided()
    {
        var root = Parse(McpToolResults.Failure(
            McpToolResults.ValidationFailed, "invalid", ["a", "b"]));

        var errors = root.GetProperty("validationErrors").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        errors.Should().BeEquivalentTo(["a", "b"]);
    }
}
