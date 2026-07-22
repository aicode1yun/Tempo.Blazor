using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.Theme;

/// <summary>
/// Guards the <c>Wrap</c> variant of <c>TmCodeEditor</c> at the stylesheet level.
/// <para>
/// The editor is a transparent textarea stacked on a highlighted <c>&lt;pre&gt;</c>: both must share
/// identical metrics, so wrapping has to be switched on for BOTH at once. Turning it on for only one
/// of them drifts the highlight against the caret — a defect bUnit cannot see (the classes are
/// correct, only the rendered geometry is wrong) and a screenshot only shows once text is long
/// enough to wrap.
/// </para>
/// <para>
/// The rules are also asserted in the bundled stylesheet, because consumers reference
/// <c>tempo-blazor.bundled.css</c> — a component file edited without rebundling ships nothing.
/// </para>
/// </summary>
public class CodeEditorWrapStylesheetTests
{
    private const string WrapModifier = ".tm-code-editor--wrap";

    private static DirectoryInfo RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
            {
                return current;
            }

            current = current.Parent!;
        }

        throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx.");
    }

    private static string CssPath(params string[] parts) =>
        Path.Combine(new[] { RepositoryRoot().FullName, "src", "Tempo.Blazor", "wwwroot", "css" }
            .Concat(parts).ToArray());

    private static string StripComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline);

    /// <summary>
    /// One shape for both sources: the component file is hand-formatted, the bundle is minified
    /// (<c>white-space:pre-wrap</c> without the space), so padding around ':' and ',' is dropped.
    /// </summary>
    private static string Normalise(string css)
    {
        var collapsed = Regex.Replace(StripComments(css), @"\s+", " ").Trim();
        return Regex.Replace(collapsed, @"\s*([:;,])\s*", "$1");
    }

    private static string ComponentCss() => Normalise(File.ReadAllText(CssPath("components", "_code-editor.css")));

    private static string BundledCss() => Normalise(File.ReadAllText(CssPath("tempo-blazor.bundled.css")));

    /// <summary>
    /// Declarations of every rule whose selector list contains exactly <paramref name="selector"/>.
    /// Exact match on purpose: the wrap modifier appears in two rules (the shared metrics and the
    /// textarea-only overflow), and a substring match would only ever return the first one.
    /// </summary>
    private static string RuleBody(string css, string selector)
    {
        var bodies = new List<string>();
        foreach (Match match in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selectors = match.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (selectors.Contains(selector, StringComparer.Ordinal))
            {
                bodies.Add(match.Groups[2].Value);
            }
        }

        return string.Join(string.Empty, bodies);
    }

    /// <summary>Selector list of the first rule that declares <paramref name="declaration"/> under the wrap modifier.</summary>
    private static string WrapRuleSelectors(string css, string declaration)
    {
        foreach (Match match in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selectors = match.Groups[1].Value;
            if (selectors.Contains(WrapModifier, StringComparison.Ordinal)
                && match.Groups[2].Value.Contains(declaration, StringComparison.OrdinalIgnoreCase))
            {
                return selectors;
            }
        }

        return string.Empty;
    }

    [Theory]
    [InlineData("component")]
    [InlineData("bundled")]
    public void Wrap_Applies_PreWrap_To_Textarea_And_Overlay_Together(string source)
    {
        var css = source == "component" ? ComponentCss() : BundledCss();

        var selectors = WrapRuleSelectors(css, "white-space:pre-wrap");

        selectors.Should().Contain($"{WrapModifier} .tm-code-editor__textarea",
            "wrapping only the textarea leaves the highlight overlay unwrapped and the caret drifts");
        selectors.Should().Contain($"{WrapModifier} .tm-code-editor__highlight",
            "wrapping only the overlay leaves the real input scrolling horizontally");
    }

    [Theory]
    [InlineData("component")]
    [InlineData("bundled")]
    public void Wrap_Breaks_Long_Words_And_Drops_Horizontal_Scroll(string source)
    {
        var css = source == "component" ? ComponentCss() : BundledCss();
        var body = RuleBody(css, $"{WrapModifier} .tm-code-editor__textarea");

        body.Should().Contain("overflow-wrap:anywhere",
            "a single long token (URL, path) must break instead of forcing a horizontal scrollbar");
        body.Should().Contain("overflow-x:hidden",
            "with wrapping on there is nothing to scroll horizontally and the overlay cannot follow it");
    }

    [Fact]
    public void Default_Editor_Keeps_Pre_Whitespace()
    {
        // Wrapping is opt-in: source code must keep its horizontal scrolling by default.
        var shared = RuleBody(ComponentCss(), ".tm-code-editor__textarea");

        shared.Should().Contain("white-space:pre");
        shared.Should().NotContain("white-space:pre-wrap");
    }
}
