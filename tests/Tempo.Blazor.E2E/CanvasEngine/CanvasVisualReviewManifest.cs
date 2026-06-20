using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.E2E.CanvasEngine;

/// <summary>Structured screenshot evidence for a canvas document editor visual review.</summary>
public sealed class CanvasVisualReviewManifest
{
    /// <summary>Test class that produced the evidence.</summary>
    [JsonPropertyName("testClass")]
    public string TestClass { get; set; } = string.Empty;

    /// <summary>Test method that produced the evidence.</summary>
    [JsonPropertyName("testName")]
    public string TestName { get; set; } = string.Empty;

    /// <summary>Named viewport under test.</summary>
    [JsonPropertyName("viewport")]
    public string Viewport { get; set; } = string.Empty;

    /// <summary>Viewport width in CSS pixels.</summary>
    [JsonPropertyName("viewportWidth")]
    public int ViewportWidth { get; set; }

    /// <summary>Viewport height in CSS pixels.</summary>
    [JsonPropertyName("viewportHeight")]
    public int ViewportHeight { get; set; }

    /// <summary>Seed document identifier requested by the test.</summary>
    [JsonPropertyName("seedId")]
    public string SeedId { get; set; } = string.Empty;

    /// <summary>Human-readable user actions performed by the test.</summary>
    [JsonPropertyName("userActions")]
    public List<string> UserActions { get; } = [];

    /// <summary>Expected visible result for reviewer comparison.</summary>
    [JsonPropertyName("expectedVisibleChanges")]
    public string ExpectedVisibleChanges { get; set; } = string.Empty;

    /// <summary>Expected model result for reviewer comparison.</summary>
    [JsonPropertyName("expectedModelChanges")]
    public string ExpectedModelChanges { get; set; } = string.Empty;

    /// <summary>Relative or absolute screenshot paths produced by the test.</summary>
    [JsonPropertyName("screenshotPaths")]
    public List<string> ScreenshotPaths { get; } = [];

    /// <summary>Canvas metrics captured during the visual gate.</summary>
    [JsonPropertyName("metrics")]
    public Dictionary<string, object> Metrics { get; } = [];

    /// <summary>Reviewer notes from the agent after opening the final screenshot.</summary>
    [JsonPropertyName("uxReviewerNotes")]
    public string UxReviewerNotes { get; set; } = string.Empty;

    /// <summary>Writes the manifest as indented JSON.</summary>
    public async Task WriteAsync(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
