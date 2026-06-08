using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.E2E.CanvasEngine;

namespace Tempo.Blazor.E2E;

/// <summary>Phase 24 legacy parity gate for canvas-only DocumentEditor coverage.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[TestCategory("DocumentEditor:CanvasParity")]
public sealed class DocumentEditorCanvasLegacyParityE2ETests
{
    [TestMethod]
    public void Phase24_LegacyParitySeed_CoversEveryLegacyFeatureGroup()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindFixturePath()));
        var featureGroups = document.RootElement.GetProperty("featureGroups")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.RequiredSeedFeatureGroups.ToArray(),
            featureGroups,
            "The phase 24 seed document must include every legacy feature group before cutover.");
    }

    [TestMethod]
    public void Phase24_LegacyParityMatrix_CoversPhasesZeroThroughTwentyThreeWithoutFallback()
    {
        var legacyRows = ParityCoverageMatrix.Entries
            .Where(entry => ParityCoverageMatrix.LegacyPhases.Contains(entry.Phase, StringComparer.Ordinal))
            .ToArray();

        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.LegacyPhases.ToArray(),
            legacyRows.Select(entry => entry.Phase).ToArray(),
            "Every legacy phase 0-23 must have a parity row.");

        foreach (var row in legacyRows)
        {
            Assert.IsFalse(
                ContainsLegacyFallback(row.ProviderBoundaryCoverage)
                || ContainsLegacyFallback(row.InteractionCoverage)
                || ContainsLegacyFallback(row.ScreenshotCoverage),
                $"{row.Phase} must use canvas coverage and must not point to legacy/WYSIWYG fallback.");
        }
    }

    [TestMethod]
    public void Phase24_LegacyToolbarCommands_HaveE2EOrExplicitShellOnlyCoverage()
    {
        var legacyCommands = new[]
        {
            "save",
            "undo",
            "redo",
            "bold",
            "italic",
            "underline",
            "fontFamily",
            "fontSize",
            "textColor",
            "highlightColor",
            "clearFormatting",
            "alignLeft",
            "alignCenter",
            "alignRight",
            "alignJustify",
            "lineSpacing",
            "spacingBefore",
            "spacingAfter",
            "increaseIndent",
            "decreaseIndent",
            "insertTable",
            "insertImage",
            "insertFootnote",
            "insertEndnote",
            "addComment",
            "trackChanges",
            "openPrintPreview",
            "importDocx",
            "exportDocx",
            "exportPdf"
        };
        var coveredCommands = ParityCoverageMatrix.ToolbarCommands.ToDictionary(command => command.Command, StringComparer.Ordinal);

        foreach (var command in legacyCommands)
        {
            Assert.IsTrue(coveredCommands.TryGetValue(command, out var coverage), $"{command} must be present in toolbar parity coverage.");
            Assert.IsTrue(coverage.CoverageKind is "E2E" or "ShellOnly", $"{command} must be E2E covered or explicitly shell-only.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(coverage.Test), $"{command} must point to a concrete test.");
        }
    }

    [TestMethod]
    public void Phase24_LegacyCoreSuites_AreClassifiedAsDiagnosticsInsteadOfParityFallback()
    {
        var diagnosticFiles = ParityCoverageMatrix.LegacyCoreOnlyDiagnosticFiles.ToArray();
        Assert.IsTrue(diagnosticFiles.Length >= 1, "Legacy/core-only diagnostics must be explicitly listed.");
        Assert.IsTrue(
            diagnosticFiles.Any(file => file.Contains("StrictEngine", StringComparison.Ordinal)),
            "Strict engine diagnostics must be classified outside the canvas parity gate.");
        Assert.IsTrue(
            diagnosticFiles.Any(file => file.Contains("JsRuntimeImage", StringComparison.Ordinal)),
            "Legacy JS runtime diagnostics must be classified outside the canvas parity gate.");
    }

    private static bool ContainsLegacyFallback(string value)
        => value.Contains("legacy", StringComparison.OrdinalIgnoreCase)
            || value.Contains("wysiwyg", StringComparison.OrdinalIgnoreCase);

    private static string FindFixturePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Tempo.Blazor.E2E", "Fixtures", "canvas-parity-seed.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not locate tests/Tempo.Blazor.E2E/Fixtures/canvas-parity-seed.json.");
    }
}
