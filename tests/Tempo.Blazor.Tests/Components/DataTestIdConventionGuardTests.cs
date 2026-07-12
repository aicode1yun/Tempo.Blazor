using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Tempo.Blazor.Tests.Components;

/// <summary>
/// data-testid convention guard (K9): no reusable-library component may hardcode a
/// <c>data-testid</c> as a literal — it must be an <c>@</c>-bound expression, and the
/// idiomatic form is <c>@TestId("...")</c> (supplied by <see cref="Tempo.Blazor.Components.TmComponentBase"/>)
/// so that a host can namespace every internal test id via <c>TestIdPrefix</c>.
/// Demo/app projects (Demo.*, ReportServer.Web) are out of scope. This is a source-text
/// sweep, so it also guards against regressions in components this phase did not touch.
/// </summary>
public class DataTestIdConventionGuardTests
{
    // data-testid="..." whose value does NOT start with '@' (i.e. a literal or a
    // mixed literal+expression value like "foo-@bar" that would not be prefixable).
    private static readonly Regex Literal =
        new("data-testid=\"(?<v>[^\"@][^\"]*)\"", RegexOptions.Compiled);

    private static bool IsLibraryRazor(string path)
    {
        var p = path.Replace('\\', '/');
        if (!p.Contains("/src/")) return false;
        if (p.Contains("/bin/") || p.Contains("/obj/")) return false;
        if (p.Contains(".Demo") || p.Contains("ReportServer")) return false;
        return p.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoReusableLibraryComponent_HardcodesDataTestId()
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
                    offenders.Add($"{Path.GetRelativePath(root.FullName, file)}:{i + 1}  {m.Value}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "reusable-library data-testid must be @-bound (idiomatically @TestId(\"...\") from TmComponentBase); " +
            "found hardcoded/mixed literals:\n" + string.Join("\n", offenders));
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
