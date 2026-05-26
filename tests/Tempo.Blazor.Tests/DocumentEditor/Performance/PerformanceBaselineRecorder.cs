using System.Globalization;
using System.Text;

namespace Tempo.Blazor.Tests.DocumentEditor.Performance;

/// <summary>Appends benchmark scenario rows to a versioned CSV baseline under
/// <c>planning/baselines/</c>. Used by <see cref="DocumentEditorPerformanceBaselineTests"/>.</summary>
internal static class PerformanceBaselineRecorder
{
    private const string BaselineDirRelative = "planning/baselines";
    private const string CsvHeader =
        "scenario,paragraphs,iterations,elapsed_ms,input_operations,model_commits,render_swaps,full_render_swaps,layout_passes,render_passes,typing_latency_total_ms,forced_reflows,js_interop_calls";

    private static readonly object _lock = new();

    internal static string EnsureBaselineDirectory()
    {
        var repoRoot = PerformanceScenarioRunner.FindRepositoryRoot();
        var path = Path.Combine(repoRoot, BaselineDirRelative);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static string EnsureBaselineFile(string date)
    {
        var dir = EnsureBaselineDirectory();
        var path = Path.Combine(dir, $"perf-{date}.csv");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, CsvHeader + Environment.NewLine, Encoding.UTF8);
        }
        return path;
    }

    internal static void AppendRow(string date, BaselineRow row)
    {
        lock (_lock)
        {
            var file = EnsureBaselineFile(date);
            var line = string.Join(',',
                EscapeCsv(row.Scenario),
                row.Paragraphs.ToString(CultureInfo.InvariantCulture),
                row.Iterations.ToString(CultureInfo.InvariantCulture),
                row.ElapsedMs.ToString("0.##", CultureInfo.InvariantCulture),
                row.InputOperations.ToString(CultureInfo.InvariantCulture),
                row.ModelCommits.ToString(CultureInfo.InvariantCulture),
                row.RenderSwaps.ToString(CultureInfo.InvariantCulture),
                row.FullRenderSwaps.ToString(CultureInfo.InvariantCulture),
                row.LayoutPasses.ToString(CultureInfo.InvariantCulture),
                row.RenderPasses.ToString(CultureInfo.InvariantCulture),
                row.TypingLatencyTotalMs.ToString("0.##", CultureInfo.InvariantCulture),
                row.ForcedReflows.ToString(CultureInfo.InvariantCulture),
                row.JsInteropCalls.ToString(CultureInfo.InvariantCulture));
            File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}

internal sealed record BaselineRow(
    string Scenario,
    int Paragraphs,
    int Iterations,
    double ElapsedMs,
    long InputOperations,
    long ModelCommits,
    long RenderSwaps,
    long FullRenderSwaps,
    long LayoutPasses,
    long RenderPasses,
    double TypingLatencyTotalMs,
    long ForcedReflows,
    long JsInteropCalls);
