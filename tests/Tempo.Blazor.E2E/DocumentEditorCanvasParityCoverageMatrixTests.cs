using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.E2E.CanvasEngine;

namespace Tempo.Blazor.E2E;

/// <summary>Audits canvas parity coverage rows that are shared by the phase 24 gate.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
public sealed class DocumentEditorCanvasParityCoverageMatrixTests
{
    private static readonly Regex CanvasE2EReferencePattern = new(@"DocumentEditorCanvas[A-Za-z0-9]+E2ETests", RegexOptions.Compiled);

    [TestMethod]
    public void ParityCoverageMatrix_ContainsEveryLegacyAndExtendedPhase()
    {
        var phaseIds = ParityCoverageMatrix.Entries.Select(entry => entry.Phase).ToArray();

        CollectionAssert.IsSubsetOf(ParityCoverageMatrix.LegacyPhases.ToArray(), phaseIds, "Every legacy phase 0-23 must have a canvas parity row.");
        CollectionAssert.IsSubsetOf(ParityCoverageMatrix.ExtendedPhases.ToArray(), phaseIds, "Every extended phase E1-E12 must have a canvas parity row.");
    }

    [TestMethod]
    public void ParityCoverageMatrix_AllRowsHaveCommandProviderInteractionAndScreenshotCoverage()
    {
        foreach (var entry in ParityCoverageMatrix.Entries)
        {
            AssertNonBlank(entry.FeatureGroup, entry.Phase, nameof(entry.FeatureGroup));
            AssertNonBlank(entry.CommandCoverage, entry.Phase, nameof(entry.CommandCoverage));
            AssertNonBlank(entry.ProviderBoundaryCoverage, entry.Phase, nameof(entry.ProviderBoundaryCoverage));
            AssertNonBlank(entry.InteractionCoverage, entry.Phase, nameof(entry.InteractionCoverage));
            AssertNonBlank(entry.ScreenshotCoverage, entry.Phase, nameof(entry.ScreenshotCoverage));
            AssertNonBlank(entry.Notes, entry.Phase, nameof(entry.Notes));
            StringAssert.DoesNotMatch(entry.ProviderBoundaryCoverage, new Regex("legacy|wysiwyg", RegexOptions.IgnoreCase), $"{entry.Phase} provider coverage must not rely on legacy fallback.");
            StringAssert.DoesNotMatch(entry.InteractionCoverage, new Regex("legacy|wysiwyg", RegexOptions.IgnoreCase), $"{entry.Phase} interaction coverage must not rely on legacy fallback.");
        }
    }

    [TestMethod]
    public void ParityCoverageMatrix_ReferencesExistingCanvasE2EFiles()
    {
        var e2eDirectory = FindE2EDirectory();
        var missing = ParityCoverageMatrix.Entries
            .SelectMany(entry => ExtractCanvasE2EReferences(entry))
            .Concat(ParityCoverageMatrix.ToolbarCommands.SelectMany(item => ExtractCanvasE2EReferences(item.Test)))
            .Concat(ParityCoverageMatrix.ProviderBoundaries.SelectMany(item => ExtractCanvasE2EReferences(item.Test)))
            .Concat(ParityCoverageMatrix.MajorInteractions.SelectMany(item => ExtractCanvasE2EReferences(item.Test)))
            .Concat(ParityCoverageMatrix.MajorInteractions.SelectMany(item => ExtractCanvasE2EReferences(item.ScreenshotTest)))
            .Distinct(StringComparer.Ordinal)
            .Where(reference => !File.Exists(Path.Combine(e2eDirectory, reference + ".cs")))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), missing, "Every matrix reference to a DocumentEditorCanvas*E2ETests class must point to an existing E2E file.");
    }

    [TestMethod]
    public void ParityCoverageMatrix_ToolbarCommandsHaveE2EOrShellOnlyCoverage()
    {
        var duplicateCommands = ParityCoverageMatrix.ToolbarCommands
            .GroupBy(command => command.Command, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), duplicateCommands, "Toolbar commands must appear once in the phase 24 coverage matrix.");

        foreach (var command in ParityCoverageMatrix.ToolbarCommands)
        {
            AssertNonBlank(command.Command, command.Command, nameof(command.Command));
            Assert.IsTrue(command.CoverageKind is "E2E" or "ShellOnly", $"{command.Command} must be covered by E2E or explicitly marked shell-only.");
            AssertNonBlank(command.Test, command.Command, nameof(command.Test));
        }

        Assert.IsTrue(
            ParityCoverageMatrix.ToolbarCommands.Any(command => command.Test.Contains("AssertToolbarStateMatchesModelAsync", StringComparison.Ordinal)),
            "Toolbar parity must include the shared AssertToolbarStateMatchesModelAsync gate.");
    }

    [TestMethod]
    public void ParityCoverageMatrix_ProviderBoundariesHaveSaveExportOrReloadCoverage()
    {
        var expectedProviders = new[]
        {
            "Image",
            "Font",
            "Token",
            "Mention",
            "PdfExport",
            "Format",
            "Comparison",
            "Suggestion",
            "Collaboration",
            "Offline",
            "Sync",
            "Audit"
        };
        var actualProviders = ParityCoverageMatrix.ProviderBoundaries.Select(provider => provider.Provider).ToArray();
        CollectionAssert.AreEquivalent(expectedProviders, actualProviders, "Every phase 24 provider boundary must have a coverage row.");

        foreach (var provider in ParityCoverageMatrix.ProviderBoundaries)
        {
            AssertNonBlank(provider.Boundary, provider.Provider, nameof(provider.Boundary));
            AssertNonBlank(provider.Test, provider.Provider, nameof(provider.Test));
            Assert.IsTrue(
                provider.Boundary.Contains("save", StringComparison.OrdinalIgnoreCase)
                || provider.Boundary.Contains("export", StringComparison.OrdinalIgnoreCase)
                || provider.Boundary.Contains("reload", StringComparison.OrdinalIgnoreCase)
                || provider.Boundary.Contains("sync", StringComparison.OrdinalIgnoreCase),
                $"{provider.Provider} must be tied to save/export/reload/sync behavior.");
        }
    }

    [TestMethod]
    public void ParityCoverageMatrix_MajorInteractionsHaveScreenshotCoverage()
    {
        var expectedInteractions = new[]
        {
            "typing",
            "selection",
            "drag",
            "table",
            "image",
            "comment",
            "revision",
            "find",
            "toc",
            "form",
            "math",
            "shape"
        };
        var actualInteractions = ParityCoverageMatrix.MajorInteractions.Select(interaction => interaction.Interaction).ToArray();
        CollectionAssert.AreEquivalent(expectedInteractions, actualInteractions, "Every phase 24 major interaction must have a screenshot row.");

        foreach (var interaction in ParityCoverageMatrix.MajorInteractions)
        {
            AssertNonBlank(interaction.Test, interaction.Interaction, nameof(interaction.Test));
            AssertNonBlank(interaction.ScreenshotTest, interaction.Interaction, nameof(interaction.ScreenshotTest));
            StringAssert.Contains(interaction.ScreenshotTest, "DocumentEditorCanvas");
        }
    }

    [TestMethod]
    public void ParitySeedFixture_CoversAllFeatureGroupsPhasesProvidersAndInteractions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindFixturePath()));
        var root = document.RootElement;

        Assert.AreEqual("Tempo.Blazor.CanvasParitySeed.v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("phase-24-canvas-parity-seed", root.GetProperty("documentId").GetString());

        var featureGroups = root.GetProperty("featureGroups")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray();
        CollectionAssert.AreEquivalent(ParityCoverageMatrix.RequiredSeedFeatureGroups.ToArray(), featureGroups, "The phase 24 seed document must cover every legacy feature group.");

        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.LegacyPhases.ToArray(),
            ReadStringArray(root, "legacyPhases"),
            "The seed fixture must enumerate all legacy phases 0-23.");
        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.ExtendedPhases.ToArray(),
            ReadStringArray(root, "extendedPhases"),
            "The seed fixture must enumerate every extended E phase.");
        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.ProviderBoundaries.Select(provider => provider.Provider).ToArray(),
            ReadStringArray(root, "providerBoundaries"),
            "The seed fixture must enumerate every provider boundary.");
        CollectionAssert.AreEquivalent(
            ParityCoverageMatrix.MajorInteractions.Select(interaction => interaction.Interaction).ToArray(),
            ReadStringArray(root, "majorInteractions"),
            "The seed fixture must enumerate every major interaction.");
    }

    private static IEnumerable<string> ExtractCanvasE2EReferences(ParityCoverageEntry entry)
        => ExtractCanvasE2EReferences(string.Join(';', entry.ProviderBoundaryCoverage, entry.InteractionCoverage, entry.ScreenshotCoverage));

    private static IEnumerable<string> ExtractCanvasE2EReferences(string text)
        => CanvasE2EReferencePattern.Matches(text).Select(match => match.Value);

    private static string[] ReadStringArray(JsonElement root, string propertyName)
        => root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

    private static string FindFixturePath()
        => FindRepoFile(Path.Combine("tests", "Tempo.Blazor.E2E", "Fixtures", "canvas-parity-seed.json"));

    private static string FindE2EDirectory()
    {
        var file = FindRepoFile(Path.Combine("tests", "Tempo.Blazor.E2E", "DocumentEditorE2ETestBase.cs"));
        return Path.GetDirectoryName(file) ?? throw new DirectoryNotFoundException("Could not resolve tests/Tempo.Blazor.E2E.");
    }

    private static string FindRepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from {AppContext.BaseDirectory}.");
    }

    private static void AssertNonBlank(string value, string row, string field)
        => Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"{row} must provide {field}.");
}
