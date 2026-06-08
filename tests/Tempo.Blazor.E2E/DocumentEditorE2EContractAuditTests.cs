using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Audits the legacy DocumentEditor E2E suite taxonomy so diagnostics are not mistaken for canvas UX parity coverage.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:DiagnosticRuntime")]
public sealed class DocumentEditorE2EContractAuditTests
{
    private static readonly DocumentEditorE2EContract[] Contracts =
    [
        new("DocumentEditorE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.DiagnosticRuntime | E2EContractKind.ProviderBoundary | E2EContractKind.LayoutVisual | E2EContractKind.LegacyMixed, "Legacy mixed suite retained for breadth; it includes diagnostic runtime probes, while strict/OnlyOffice suites provide the canonical UX contracts.", "DocumentEditorOnlyOfficeParityE2ETests.cs"),
        new("DocumentEditorImageDocxPhase39E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.ProviderBoundary | E2EContractKind.LayoutVisual, "DOCX import/edit/export DrawingML workflow coverage through real upload/download UI boundaries."),
        new("DocumentEditorImageOnlyOfficeParityE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "ONLYOFFICE-level image wrapping, focus, insertion, drag and resize behavior."),
        new("DocumentEditorOnlyOfficeParityE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Canonical ONLYOFFICE-level workflow coverage for formatting, selection, revisions, undo, side panels and ribbon modes."),
        new("DocumentEditorPhase10ClipboardPipelineE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.ProviderBoundary, "Clipboard paste/import boundaries through user-visible entry points."),
        new("DocumentEditorPhase11ImageUxE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Image insertion and inspector UX."),
        new("DocumentEditorPhase14AutocompleteE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Autocomplete, token, slash and mention workflows."),
        new("DocumentEditorPhase15ImageLayoutStressE2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Image layout stress and visual stability diagnostics."),
        new("DocumentEditorPhase15PageUxE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Page UX, navigator and visible page controls."),
        new("DocumentEditorPhase16AutosaveE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.ProviderBoundary, "Autosave provider boundary and user-visible save state."),
        new("DocumentEditorPhase17WatchdogE2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.ProviderBoundary, "Runtime watchdog and recovery diagnostics."),
        new("DocumentEditorPhase18DebugE2ETests.cs", E2EContractKind.DiagnosticRuntime, "Debug artifact and diagnostic panel coverage."),
        new("DocumentEditorPhase19ImportExportE2ETests.cs", E2EContractKind.ProviderBoundary, "Import/export provider boundary coverage."),
        new("DocumentEditorPhase20PerformanceE2ETests.cs", E2EContractKind.DiagnosticRuntime, "Performance and latency diagnostic coverage."),
        new("DocumentEditorPhase21AccessibilityE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Keyboard and accessibility workflows."),
        new("DocumentEditorPhase22DemoDocsE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.ProviderBoundary, "Demo document reset and scenario boundary coverage."),
        new("DocumentEditorPhaseABCPerformanceE2ETests.cs", E2EContractKind.DiagnosticRuntime, "Phase A/B/C legacy WYSIWYG typing performance diagnostic coverage."),
        new("DocumentEditorPhase3CommandRegistryE2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.ProviderBoundary, "Diagnostic command registry/provider boundary coverage."),
        new("DocumentEditorPhase4ToolbarE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Toolbar and ribbon shell workflows."),
        new("DocumentEditorPhase5RuntimeModularizationE2ETests.cs", E2EContractKind.DiagnosticRuntime, "Runtime modularization diagnostics."),
        new("DocumentEditorPhase6SchemaPolicyE2ETests.cs", E2EContractKind.ProviderBoundary, "Schema policy and document boundary coverage."),
        new("DocumentEditorPhase7MarkerStoreE2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.ProviderBoundary, "Diagnostic marker store/runtime boundary coverage."),
        new("DocumentEditorPhase8FloatingFocusE2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Floating UI focus and viewport workflows."),
        new("DocumentEditorPhase9FindReplaceE2ETests.cs", E2EContractKind.HumanWorkflow, "Find/replace workflow coverage."),
        new("DocumentEditorQualitySmokeTests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Broad smoke only; not a replacement for strict workflow assertions."),
        new("DocumentEditorRegressionRecoveryE2ETests.cs", E2EContractKind.HumanWorkflow, "Regression recovery workflow coverage."),
        new("DocumentEditorRegressionRecoveryPhase2E2ETests.cs", E2EContractKind.HumanWorkflow, "Regression recovery phase 2 workflow coverage."),
        new("DocumentEditorRegressionRecoveryPhase3E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Header/footer recovery workflow and layout coverage."),
        new("DocumentEditorRegressionRecoveryPhase4E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Comment marker recovery workflow and layout coverage."),
        new("DocumentEditorRegressionRecoveryPhase5E2ETests.cs", E2EContractKind.HumanWorkflow, "Revision recovery workflow coverage."),
        new("DocumentEditorRegressionRecoveryPhase6E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Formatting recovery workflow and toolbar sync coverage."),
        new("DocumentEditorRegressionRecoveryPhase7E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Image wrapping recovery workflow and layout coverage."),
        new("DocumentEditorRegressionRecoveryPhase8E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Floating toolbar recovery workflow and layout coverage."),
        new("DocumentEditorRegressionRecoveryPhase9E2ETests.cs", E2EContractKind.HumanWorkflow, "Track changes and revision recovery workflow coverage."),
        new("DocumentEditorRegressionRecoveryPhase10E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Comment boundary recovery workflow and layout coverage."),
        new("DocumentEditorRegressionRecoveryPhase11E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Undo recovery workflow and visible state coverage."),
        new("DocumentEditorRegressionRecoveryPhase12E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Side panel recovery workflow and layout coverage."),
        new("DocumentEditorRegressionRecoveryPhase13E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.LayoutVisual, "Final recovery workflow suite for visible P0/P1 behavior."),
        new("DocumentEditorStrictEnginePhase0E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict frame diagnostics for live typing/layout."),
        new("DocumentEditorStrictEnginePhase1And2E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict engine diagnostics for early runtime phases."),
        new("DocumentEditorStrictEnginePhase3E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict command/runtime diagnostics."),
        new("DocumentEditorStrictEnginePhase4E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict input/runtime diagnostics."),
        new("DocumentEditorStrictEnginePhase5E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict text layout diagnostics."),
        new("DocumentEditorStrictEnginePhase6E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict paragraph layout diagnostics."),
        new("DocumentEditorStrictEnginePhase7E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict marker/layout diagnostics."),
        new("DocumentEditorStrictEnginePhase8E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict floating UI runtime diagnostics."),
        new("DocumentEditorStrictEnginePhase9E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict table runtime diagnostics."),
        new("DocumentEditorStrictEnginePhase10E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict page layout diagnostics."),
        new("DocumentEditorStrictEnginePhase11E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict anchored-object diagnostics."),
        new("DocumentEditorStrictEnginePhase12E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict clipboard/runtime diagnostics."),
        new("DocumentEditorStrictEnginePhase13E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict undo/runtime diagnostics."),
        new("DocumentEditorStrictEnginePhase14E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict table editing diagnostics."),
        new("DocumentEditorStrictEnginePhase15E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict page UX diagnostics."),
        new("DocumentEditorStrictEnginePhase16E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict visual matrix diagnostics."),
        new("DocumentEditorStrictEnginePhase17E2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.ProviderBoundary, "Strict autosave/watchdog diagnostics."),
        new("DocumentEditorStrictEnginePhase18E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict test-harness diagnostics."),
        new("DocumentEditorStrictEnginePhase18ImageRegionScopeE2ETests.cs", E2EContractKind.DiagnosticRuntime | E2EContractKind.LayoutVisual, "Strict image-region diagnostics for header, footer and table-cell drawing scope."),
        new("DocumentEditorStrictEnginePhase19E2ETests.cs", E2EContractKind.HumanWorkflow | E2EContractKind.DiagnosticRuntime, "Strict demo reset and readable reload diagnostics."),
        new("DocumentEditorStrictEnginePhase20E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict performance diagnostics."),
        new("DocumentEditorStrictEnginePhase22E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict demo-doc diagnostics."),
        new("DocumentEditorStrictEnginePhase23E2ETests.cs", E2EContractKind.DiagnosticRuntime, "Strict UX polish diagnostics.")
    ];

    [TestMethod]
    public void DocumentEditorE2EContractRegistry_CoversEveryDocumentEditorE2EFile()
    {
        var e2eDirectory = FindE2EDirectory();
        var files = Directory.EnumerateFiles(e2eDirectory, "DocumentEditor*E2ETests.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Concat(File.Exists(Path.Combine(e2eDirectory, "DocumentEditorQualitySmokeTests.cs"))
                ? ["DocumentEditorQualitySmokeTests.cs"]
                : [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Where(name => !name.StartsWith("DocumentEditorCanvas", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var registered = Contracts.Select(contract => contract.FileName).Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEquivalent(files, registered,
            $"Every legacy DocumentEditor E2E file must be classified. Canvas E2E files are audited by DocumentEditorCanvasParityCoverageMatrixTests. Missing registry entries: {string.Join(", ", files.Except(registered, StringComparer.Ordinal))}. Stale registry entries: {string.Join(", ", registered.Except(files, StringComparer.Ordinal))}.");
    }

    [TestMethod]
    public void DocumentEditorE2EContractRegistry_MarksDiagnosticsLegacyAndObsoleteCoverageExplicitly()
    {
        var duplicateFiles = Contracts
            .GroupBy(contract => contract.FileName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), duplicateFiles, "Each E2E file must appear once in the contract registry.");

        foreach (var contract in Contracts)
        {
            Assert.AreNotEqual(E2EContractKind.None, contract.Kind, $"{contract.FileName} must have at least one E2E contract kind.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.Coverage), $"{contract.FileName} must explain what it covers.");

            if (contract.Kind.HasFlag(E2EContractKind.DiagnosticRuntime))
            {
                Assert.IsTrue(
                    contract.FileName.Contains("Strict", StringComparison.Ordinal)
                    || contract.FileName.Contains("Runtime", StringComparison.Ordinal)
                    || contract.Coverage.Contains("diagnostic", StringComparison.OrdinalIgnoreCase)
                    || contract.Coverage.Contains("performance", StringComparison.OrdinalIgnoreCase),
                    $"{contract.FileName} is diagnostic runtime coverage and must say so by name or coverage text.");
            }

            if (contract.Kind.HasFlag(E2EContractKind.LegacyMixed) || contract.Kind.HasFlag(E2EContractKind.ObsoleteAfterRuntimeChange))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(contract.ReplacementFile),
                    $"{contract.FileName} is legacy/obsolete and must point to a replacement contract.");
                Assert.IsTrue(Contracts.Any(candidate => candidate.FileName == contract.ReplacementFile),
                    $"{contract.FileName} points to unknown replacement file '{contract.ReplacementFile}'.");
            }
        }
    }

    private static string FindE2EDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DocumentEditorE2ETestBase.cs")))
            {
                return directory.FullName;
            }

            var candidate = Path.Combine(directory.FullName, "tests", "Tempo.Blazor.E2E");
            if (File.Exists(Path.Combine(candidate, "DocumentEditorE2ETestBase.cs")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate tests/Tempo.Blazor.E2E from the test bin directory.");
    }

    [Flags]
    private enum E2EContractKind
    {
        None = 0,
        HumanWorkflow = 1,
        DiagnosticRuntime = 2,
        ProviderBoundary = 4,
        LayoutVisual = 8,
        LegacyMixed = 16,
        ObsoleteAfterRuntimeChange = 32
    }

    private sealed record DocumentEditorE2EContract(
        string FileName,
        E2EContractKind Kind,
        string Coverage,
        string? ReplacementFile = null);
}
