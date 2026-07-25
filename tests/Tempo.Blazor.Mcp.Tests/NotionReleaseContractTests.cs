using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionReleaseContractTests
{
    [Fact]
    public void CanonicalTableRows_ExposeNoLegacyFlatCells()
    {
        typeof(ITableRowBlockContent).GetProperty("Cells")
            .Should().BeNull();
        typeof(TableRowBlockContent).GetProperty("Cells")
            .Should().BeNull();

        var sourceRoot = Path.Combine(
            RepoRoot(),
            "src",
            "Tempo.Blazor.NotionEditor");
        var offenders = Directory.EnumerateFiles(
                sourceRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains(
                           "new TableRowBlockContent { Cells",
                           StringComparison.Ordinal) ||
                       source.Contains(
                           "ITableRowBlockContent row => row.Cells",
                           StringComparison.Ordinal) ||
                       source.Contains(
                           "row!.Cells",
                           StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(RepoRoot(), path))
            .ToList();

        offenders.Should().BeEmpty(
            "2.7.0 has one canonical RichCells table-row representation");
    }

    [Fact]
    public void DemoAggregateAdapter_HasNoLegacyCellFallback()
    {
        var path = Path.Combine(
            RepoRoot(),
            "src",
            "Tempo.Blazor.Demo.Api",
            "Data",
            "DemoNotionAggregateStore.cs");

        File.ReadAllText(path).Should().NotContain(
            "row.Cells",
            "the demo boundary must reject legacy rows instead of upgrading them at runtime");
    }

    [Fact]
    public void NotionAuthoring_HasNoGranularProviderOrBlockEndpoints()
    {
        typeof(INotionAggregateProvider).Assembly.GetType(
                "Tempo.Blazor.NotionEditor.Interfaces.INotionBlockProvider")
            .Should().BeNull();

        var sourceRoot = Path.Combine(RepoRoot(), "src");
        var offenders = Directory.EnumerateFiles(
                sourceRoot,
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                           path.EndsWith(".razor", StringComparison.Ordinal))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("INotionBlockProvider", StringComparison.Ordinal) ||
                       source.Contains("/api/notion/blocks", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(RepoRoot(), path))
            .ToList();

        offenders.Should().BeEmpty(
            "2.7 Notion authoring must persist exclusively through complete aggregate snapshots");
    }

    /// <summary>
    /// The packages ship in lockstep, and the number they ship under is the one the changelog announces.
    /// <para>
    /// This used to name the release it was written for (<c>…AreSynchronizedTo270</c>, asserting the literal
    /// "2.7.0"), which made every release start by editing the test that was supposed to check the release.
    /// A guard that has to be rewritten to stay green is measuring the state it found rather than the rule,
    /// and the rule here is two things neither of which mentions a number: every packable project agrees,
    /// and what they agree on is what the changelog says is being released. The second half is the defect
    /// this release ran into — a changelog entry written under 2.5.6 while the packages stood at 2.7.0,
    /// which would have published a version below the one already on the feed.
    /// </para>
    /// <para>
    /// WHAT IT READS OUT OF THE CHANGELOG, since that shape is now load-bearing: the first <c>##</c>
    /// heading whose text is a semantic version. Headings that are not versions are SKIPPED rather than
    /// failed on, so the common <c>## Unreleased</c> section can be added on top without turning this red
    /// for a reason that has nothing to do with the packages — a guard that cries at a benign edit is a
    /// guard somebody eventually weakens. The document title (a single <c>#</c>) and the subsections
    /// inside a release (<c>###</c>) do not match either. What it therefore assumes, and what would make
    /// it lie, is that no released version is listed ABOVE the one being shipped.
    /// </para>
    /// </summary>
    [Fact]
    public void PackableProjects_AgreeOnOneVersion_AndItIsTheOneTheChangelogAnnounces()
    {
        var projectVersions = Directory.EnumerateFiles(
                Path.Combine(RepoRoot(), "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Select(path => (
                Path: path,
                Version: XDocument.Load(path)
                    .Descendants("Version")
                    .Select(element => element.Value.Trim())
                    .SingleOrDefault()))
            .Where(item => item.Version is not null)
            .ToList();

        projectVersions.Should().NotBeEmpty();

        var announced = Regex.Match(
                File.ReadAllText(Path.Combine(RepoRoot(), "CHANGELOG.md")),
                @"^##\s*(?<version>\d+\.\d+\.\d+)",
                RegexOptions.Multiline)
            .Groups["version"].Value;

        announced.Should().NotBeEmpty(
            "the changelog must open with the version being released, or there is nothing to check against");

        projectVersions.Should().OnlyContain(
            item => item.Version == announced,
            $"every locally packable project must ship under the version the changelog announces ({announced}); "
            + "a package numbered independently of the release notes is how a fix ships to nobody");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new DirectoryNotFoundException(
                "Could not locate the Tempo.Blazor repository root.");
    }
}
