using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.E2E.CanvasEngine;

namespace Tempo.Blazor.E2E;

/// <summary>Phase 24 extended E1-E12 parity gate for canvas-only DocumentEditor coverage.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[TestCategory("DocumentEditor:CanvasParity")]
public sealed class DocumentEditorCanvasExtendedParityE2ETests
{
    [TestMethod]
    public void Phase24_ExtendedParityMatrix_CoversEveryEPhase()
    {
        var extendedRows = ParityCoverageMatrix.Entries
            .Where(entry => ParityCoverageMatrix.ExtendedPhases.Contains(entry.Phase, StringComparer.Ordinal))
            .ToArray();

        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.ExtendedPhases.ToArray(),
            extendedRows.Select(entry => entry.Phase).ToArray(),
            "Each E1-E12 feature phase must have one parity row.");

        foreach (var row in extendedRows)
        {
            Assert.IsTrue(row.CommandCoverage.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length >= 3, $"{row.Phase} must name its command coverage.");
            StringAssert.Contains(row.ProviderBoundaryCoverage, "DocumentEditorCanvas", $"{row.Phase} must point to canvas provider-boundary coverage.");
            StringAssert.Contains(row.ScreenshotCoverage, "DocumentEditorCanvas", $"{row.Phase} must point to canvas screenshot coverage.");
        }
    }

    [TestMethod]
    public void Phase24_ExtendedToolbarCommands_HaveE2EOrExplicitShellOnlyCoverage()
    {
        var extendedCommands = new[]
        {
            "insertEquation",
            "insertSymbol",
            "insertCaption",
            "insertCrossReference",
            "insertTableOfContents",
            "insertTableOfFigures",
            "insertBibliography",
            "updateAllFields",
            "insertShape",
            "insertTextBox",
            "insertConnector",
            "insertChart",
            "insertTextControl",
            "insertDropdownControl",
            "applyAutocorrect",
            "copyFormat",
            "pasteFormat",
            "showRuler",
            "zoomPageWidth",
            "showBlocks",
            "fullscreen",
            "printDocument"
        };
        var coveredCommands = ParityCoverageMatrix.ToolbarCommands.ToDictionary(command => command.Command, StringComparer.Ordinal);

        foreach (var command in extendedCommands)
        {
            Assert.IsTrue(coveredCommands.TryGetValue(command, out var coverage), $"{command} must be present in extended toolbar parity coverage.");
            Assert.IsTrue(coverage.CoverageKind is "E2E" or "ShellOnly", $"{command} must be E2E covered or explicitly shell-only.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(coverage.Test), $"{command} must point to a concrete test.");
        }
    }

    [TestMethod]
    public void Phase24_ExtendedProviderAndInteractionCoverage_IsComplete()
    {
        foreach (var provider in ParityCoverageMatrix.ProviderBoundaries)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(provider.Test), $"{provider.Provider} must point to a provider-boundary test.");
            Assert.IsTrue(
                provider.Test.Contains("DocumentEditorCanvas", StringComparison.Ordinal)
                || provider.Test.Contains("DocumentDocxFormatTests", StringComparison.Ordinal),
                $"{provider.Provider} must be covered by a canvas E2E or document-format provider test.");
        }

        foreach (var interaction in ParityCoverageMatrix.MajorInteractions)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(interaction.Test), $"{interaction.Interaction} must point to an interaction test.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(interaction.ScreenshotTest), $"{interaction.Interaction} must point to a screenshot test.");
            StringAssert.Contains(interaction.ScreenshotTest, "DocumentEditorCanvas");
        }
    }
}
