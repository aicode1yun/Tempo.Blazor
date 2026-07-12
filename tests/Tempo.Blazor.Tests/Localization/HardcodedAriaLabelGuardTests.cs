using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Tempo.Blazor.Tests.Localization;

/// <summary>
/// Localization guard (K7): no reusable-library component may hardcode a user-facing
/// <c>aria-label</c> or <c>title</c> as a literal — it must be an <c>@Loc[...]</c> expression.
/// Demo/app projects (Demo.*, ReportServer.Web) are out of scope. This is a source-text sweep,
/// so it also guards against regressions in components this phase did not touch.
/// </summary>
public class HardcodedAriaLabelGuardTests
{
    // aria-label="..." or title="..." whose value does NOT start with '@' (i.e. a literal).
    private static readonly Regex Literal =
        new("(?:aria-label|title)=\"(?<v>[^\"@][^\"]*)\"", RegexOptions.Compiled);

    private static bool ContainsLetter(string s) => s.Any(char.IsLetter);

    private static bool IsLibraryRazor(string path)
    {
        var p = path.Replace('\\', '/');
        if (!p.Contains("/src/")) return false;
        if (p.Contains("/bin/") || p.Contains("/obj/")) return false;
        if (p.Contains(".Demo") || p.Contains("ReportServer")) return false;
        return p.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoReusableLibraryComponent_HardcodesAriaLabelOrTitle()
    {
        var root = FindRepoRoot();
        var srcDir = Path.Combine(root.FullName, "src");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(srcDir, "*.razor", SearchOption.AllDirectories))
        {
            if (!IsLibraryRazor(file)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in Literal.Matches(lines[i]))
                {
                    var value = m.Groups["v"].Value;
                    if (!ContainsLetter(value)) continue; // skip pure glyphs/numbers (e.g. "×")
                    offenders.Add($"{Path.GetRelativePath(root.FullName, file)}:{i + 1}  {m.Value}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "reusable-library aria-label/title must use @Loc[...]; found hardcoded literals:\n" +
            string.Join("\n", offenders));
    }

    private static DirectoryInfo FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx"))) return directory;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
