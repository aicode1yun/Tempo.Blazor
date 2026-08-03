using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Fails the run if it overwrote a COMMITTED screenshot without asking to.
/// <para>
/// Its predicate is the WRITE TARGET, not the name of a class. That distinction is the whole point:
/// the previous guard swept for classes called <c>*BaselineScreenshots</c> and was therefore
/// constitutionally blind to <see cref="NotionE2ETestBase"/>, which wrote 765 committed PNGs — 66%
/// of every tracked PNG in the repository — from 276 call sites, under a name matching no
/// convention and with no category. A single ordinary Notion test rewrote 12 of them. Any guard
/// keyed on how a class is spelled can be defeated by spelling it differently; a guard keyed on
/// what the run actually TOUCHED cannot.
/// </para>
/// <para>
/// Only files git already tracks are measured. A NEW png is a run artefact and not this guard's
/// business; a MODIFIED one is a committed reference that this run destroyed.
/// </para>
/// <para>
/// SCOPE: <c>__baseline__/</c> and nothing else. It first swept all three png roots, and that was
/// too wide — it failed runs whose every test method was green. Measured on
/// <c>ComponentAccessibilityE2ETests</c> at a clean HEAD: 6/6 methods passed, the run died in
/// <c>AssemblyCleanup</c> over 9 pngs under <c>__screenshots__/accessibility/</c>.
/// </para>
/// <para>
/// The two dropped roots are not references, and that is measurable rather than a matter of taste.
/// <c>ToHaveScreenshotAsync</c> appears <b>0 times</b> in this project and nothing reads any png
/// back, so no assertion anywhere consumes one. What separates <c>__baseline__</c> is that it is the
/// only root with a DELIBERATE-CHANGE MECHANISM: <see cref="BaselineOutput.CommittedRoot"/> is
/// hardcoded to it, <see cref="BaselineGeneratorTestBase.WritesAllowed"/> / <c>TM_WRITE_BASELINES</c>
/// gate writes to it, and <see cref="BaselineGeneratorGateTests"/> polices the generator convention
/// that feeds it. <c>__screenshots__/</c> (315 pngs, 57 classes) and <c>screenshots/</c> (7 pngs,
/// 3 classes) have none of that — every one of those classes builds its path with
/// <c>Path.Combine</c> and writes unconditionally, and their own doc comments call the output
/// "for UX review". So the old scope demanded a redirect that does not exist for them:
/// <see cref="BaselineOutput"/> can only ever point at <c>__baseline__</c>, and it has 5 call sites,
/// all Notion. A guard whose remedy cannot be applied is a guard that only teaches people to
/// distrust it.
/// </para>
/// <para>
/// REACHABILITY, so the next reader knows what this can and cannot catch: CI never runs it. Both
/// publish workflows test with <c>--filter "FullyQualifiedName!~Tempo.Blazor.E2E&amp;…"</c>
/// (<c>publish-nuget.yml</c>, <c>publish-nuget-org.yml</c>), which excludes this whole assembly.
/// This guard fires on a developer's machine or not at all.
/// </para>
/// <para>
/// Do NOT read this narrowing as "the E2E suite is now green". What was measured is the scope of
/// this guard, not the health of the suite: ordinary (non-generator, therefore un-skipped) classes
/// still write straight into <c>__baseline__</c> with a bare <c>Path.Combine</c>, bypassing
/// <see cref="BaselineOutput"/> — <c>NotionKrDocxFidelityE2ETests</c>,
/// <c>NotionRestrictionsE2ETests</c>, <c>NotionTableE2ETests</c>, <c>StencilStructurePhase10E2ETests</c>,
/// <c>StencilComplexPhase12E2ETests</c>, <c>StencilFormControlsGalleryE2ETests</c> and
/// <c>WireframeServerPreviewE2ETests</c>. Whether a given run of those actually trips this guard
/// depends on whether their capture comes out byte-identical, which was not measured. If one does
/// trip it, that is the guard working, not a regression in it.
/// </para>
/// <para>
/// It is not a test method, because it has to observe the whole run rather than one test. The
/// snapshot is taken in <c>[AssemblyInitialize]</c> and compared from
/// <see cref="PlaywrightTestBase.AssemblyCleanup"/>, which calls
/// <see cref="AssertNothingWasOverwritten"/> OUTSIDE its best-effort teardown loop — that loop
/// swallows exceptions, so a failure raised inside it would be silent.
/// </para>
/// </summary>
[TestClass]
public static class BaselineWriteSweep
{
    /// <summary>Relative path → sha256 of every tracked png, as it stood before the run.</summary>
    private static Dictionary<string, string>? _before;

    private static string? _repositoryRoot;

    [AssemblyInitialize]
    public static void CaptureSnapshot(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            _repositoryRoot = FindRepositoryRoot();
            _before = HashTrackedScreenshots(_repositoryRoot);
            context.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[baseline-sweep] snapshotted {_before.Count} tracked png files; "
                + $"writes allowed = {BaselineGeneratorTestBase.WritesAllowed}"));
        }
        catch (Exception ex)
        {
            // A missing git or a tarball checkout must not take the whole suite down; the sweep
            // reports it could not measure instead of pretending it measured nothing.
            _before = null;
            context.WriteLine($"[baseline-sweep] DISABLED: {ex.Message}");
        }
    }

    /// <summary>
    /// Throws if any tracked screenshot changed while the opt-in was off. Called from the single
    /// <c>[AssemblyCleanup]</c> the assembly is allowed to have.
    /// </summary>
    public static void AssertNothingWasOverwritten()
    {
        if (_before is null || _repositoryRoot is null)
        {
            return;
        }

        var after = HashTrackedScreenshots(_repositoryRoot);
        var damaged = new List<string>();

        foreach (var (path, hash) in _before)
        {
            if (!after.TryGetValue(path, out var current))
            {
                damaged.Add($"{path} (deleted)");
            }
            else if (!string.Equals(current, hash, StringComparison.Ordinal))
            {
                damaged.Add(path);
            }
        }

        if (damaged.Count == 0)
        {
            return;
        }

        if (BaselineGeneratorTestBase.WritesAllowed)
        {
            // The whole point of the opt-in: with it set, rewriting them is the intent.
            return;
        }

        var report = new StringBuilder()
            .Append(damaged.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" committed screenshot(s) were overwritten by a run that did not set ")
            .Append(BaselineGeneratorTestBase.EnvironmentVariable)
            .Append(". Captures must be redirected to TestResults (see BaselineOutput), not written ")
            .Append("into the working tree:");

        foreach (var path in damaged.Take(25))
        {
            report.Append("\n  ").Append(path);
        }

        if (damaged.Count > 25)
        {
            report.Append(string.Create(CultureInfo.InvariantCulture,
                $"\n  … and {damaged.Count - 25} more"));
        }

        throw new InvalidOperationException(report.ToString());
    }

    /// <summary>
    /// The tracked png files under the COMMITTED BASELINE root, hashed. <c>git ls-files</c> is what
    /// makes "tracked" mean the same thing here as it does in the acceptance criterion.
    /// </summary>
    private static Dictionary<string, string> HashTrackedScreenshots(string repositoryRoot)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var relative in TrackedScreenshotPaths(repositoryRoot))
        {
            var absolute = Path.Combine(repositoryRoot, relative);
            if (!File.Exists(absolute))
            {
                continue;
            }

            using var stream = File.OpenRead(absolute);
            hashes[relative] = Convert.ToHexString(SHA256.HashData(stream));
        }

        return hashes;
    }

    private static List<string> TrackedScreenshotPaths(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("--");
        // __baseline__ ONLY. See the type doc for why __screenshots__/ and screenshots/ are out.
        startInfo.ArgumentList.Add("tests/Tempo.Blazor.E2E/__baseline__/*.png");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("could not start git");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30000);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files exited {process.ExitCode}");
        }

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx.");
    }
}
