using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.Theme;

/// <summary>
/// Guards WCAG 2.2 SC 1.4.11 (non-text contrast, 3:1) for the three 1.5px selection controls —
/// <c>TmCheckbox</c>, <c>TmRadio</c> and the multiselect option checkbox — in BOTH themes.
/// <para>
/// A selection control carries its whole state in two graphical objects: the outline (nothing is
/// selected) and the glyph on the filled box (something is). Both shipped under 3:1 — the dark
/// glyph at 2.54:1 (white on the lighter primary-400 fill) and the outline at 1.41:1 dark /
/// 1.24:1 light, because all three reached for <c>--tm-border-color</c>, the decorative divider
/// tone. Neither a screenshot review nor bUnit can catch that: the contrast lives in the token
/// graph, not in the DOM.
/// </para>
/// <para>
/// The ratios are computed from the tokens the stylesheets ACTUALLY reference, <c>var()</c> chains
/// resolved, so the guard also fails when the failing colour is reintroduced further up the graph
/// (e.g. by repointing <c>--tm-color-primary</c> in the dark theme).
/// </para>
/// <para>
/// LIMIT, stated on purpose: this runs against Tempo's own two token files. A consumer that
/// repoints the primary scale (EmusCz ships six accents) is NOT covered here — that matrix belongs
/// next to the consumer's theme file.
/// </para>
/// </summary>
public class SelectionControlContrastTests
{
    /// <summary>WCAG 2.2 SC 1.4.11 — user-interface components need 3:1 against adjacent colours.</summary>
    private const double NonTextMinimum = 3.0;

    /// <summary>Light glyph-on-fill measured before the 2.5.4 fix (5.1686); it must never regress.</summary>
    private const double LightGlyphBaseline = 5.16;

    /// <summary>The outline token every selection control must take, with its compatibility fallback.</summary>
    private const string OutlineValue = "var(--tm-border-color-control, var(--tm-border-color))";

    /// <summary>The glyph token every filled selection control must take.</summary>
    private const string GlyphValue = "var(--tm-control-glyph-color, var(--tm-color-white))";

    /// <summary>One selection control: where its stylesheet declares the colours that carry state.</summary>
    public sealed record Control(
        string Name,
        string Stylesheet,
        string BoxSelector,
        string FilledSelectorFragment,
        string FilledFillProperty,
        string? GlyphSelector,
        string BoxFillFallback)
    {
        public override string ToString() => Name;
    }

    private static readonly Control Checkbox = new("checkbox", "_checkbox.css", ".tm-checkbox-custom",
        ":checked ~ .tm-checkbox-custom", "background-color", ".tm-checkbox-check",
        BoxFillFallback: "var(--tm-bg-surface)");

    // A radio marks its state with a filled dot (::after), not with a glyph — hence no glyph selector.
    private static readonly Control Radio = new("radio", "_radio-group.css", ".tm-radio-custom",
        ":checked ~ .tm-radio-custom::after", "background-color", GlyphSelector: null,
        BoxFillFallback: "var(--tm-bg-surface)");

    // The multiselect option box declares no background of its own: it sits on the dropdown
    // surface (_multiselect.css background: var(--tm-bg-surface)), so that is the outline's
    // inner neighbour.
    private static readonly Control MultiselectOption = new("multiselect option", "_multiselect.css",
        ".tm-multiselect__option-checkbox", ".tm-multiselect__option-checkbox--checked", "background",
        ".tm-multiselect__option-checkbox--checked", BoxFillFallback: "var(--tm-bg-surface)");

    /// <summary>Every 1.5px selection control — they all carry state in an outline.</summary>
    public static TheoryData<Control> Controls() => [Checkbox, Radio, MultiselectOption];

    /// <summary>
    /// Only the controls that draw a GLYPH on the filled box. Kept as a separate set on purpose: a
    /// test case that returns early for the radio would be permanently green without measuring
    /// anything, and "green" has to mean "measured".
    /// </summary>
    public static TheoryData<Control> GlyphControls() => [Checkbox, MultiselectOption];

    // ── CSS reading ──────────────────────────────────────────────────────────────────────────────

    private static readonly Regex Declaration =
        new(@"(?<name>--tm-[\w-]+)\s*:\s*(?<value>[^;{}]+);", RegexOptions.Compiled);

    private static readonly Regex RuleBlock =
        new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

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

    /// <summary>Comments would otherwise leak into a selector or swallow a declaration (they contain colons).</summary>
    private static string StripComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline);

    private static string ComponentCss(string file) =>
        StripComments(File.ReadAllText(CssPath("components", file)));

    private static string Normalise(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    /// <summary>All <c>--tm-*</c> declarations of a file, later declarations winning.</summary>
    private static Dictionary<string, string> Declarations(string path)
    {
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Declaration.Matches(StripComments(File.ReadAllText(path))))
        {
            declarations[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
        }

        return declarations;
    }

    /// <summary>The token graph of a theme: the light tokens with the dark overrides layered on top.</summary>
    private static Dictionary<string, string> TokenGraph(bool dark)
    {
        var tokens = Declarations(CssPath("tokens.css"));
        if (dark)
        {
            foreach (var (name, value) in Declarations(CssPath("tokens-dark.css")))
            {
                tokens[name] = value;
            }
        }

        return tokens;
    }

    /// <summary>The first <c>var(…)</c> in a value, with nested fallbacks kept intact.</summary>
    private static string? FirstVar(string value)
    {
        var start = value.IndexOf("var(", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = start + 3; i < value.Length; i++)
        {
            if (value[i] == '(')
            {
                depth++;
            }
            else if (value[i] == ')' && --depth == 0)
            {
                return value[start..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Unbalanced var() in '{value}'.");
    }

    /// <summary>Resolves a CSS value (literal or a <c>var()</c> chain, fallbacks included) to #rrggbb.</summary>
    private static string ResolveColour(string value, Dictionary<string, string> tokens, int depth = 0)
    {
        depth.Should().BeLessThan(16, "a var() chain must not be cyclic");
        value = value.Trim();

        var reference = FirstVar(value);
        if (reference is null)
        {
            return value;
        }

        var inner = reference[4..^1];
        var comma = SplitOnTopLevelComma(inner);
        var name = comma.name.Trim();

        if (tokens.TryGetValue(name, out var referenced))
        {
            return ResolveColour(referenced, tokens, depth + 1);
        }

        comma.fallback.Should().NotBeNull($"token {name} must be declared (or carry a fallback)");
        return ResolveColour(comma.fallback!, tokens, depth + 1);
    }

    private static (string name, string? fallback) SplitOnTopLevelComma(string inner)
    {
        var depth = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '(')
            {
                depth++;
            }
            else if (inner[i] == ')')
            {
                depth--;
            }
            else if (inner[i] == ',' && depth == 0)
            {
                return (inner[..i], inner[(i + 1)..]);
            }
        }

        return (inner, null);
    }

    /// <summary>The value a property has in the first rule whose selector matches the predicate.</summary>
    private static string Property(string stylesheet, Func<string, bool> selectorMatches, string property)
    {
        foreach (Match rule in RuleBlock.Matches(ComponentCss(stylesheet)))
        {
            if (!selectorMatches(Normalise(rule.Groups["selector"].Value)))
            {
                continue;
            }

            foreach (var declaration in rule.Groups["body"].Value.Split(';'))
            {
                var separator = declaration.IndexOf(':', StringComparison.Ordinal);
                if (separator < 0)
                {
                    continue;
                }

                if (string.Equals(declaration[..separator].Trim(), property, StringComparison.Ordinal))
                {
                    return Normalise(declaration[(separator + 1)..]);
                }
            }
        }

        throw new InvalidOperationException(
            $"{stylesheet} has no rule matching the expected selector that declares '{property}'.");
    }

    private static string? TryProperty(string stylesheet, Func<string, bool> selectorMatches, string property)
    {
        try
        {
            return Property(stylesheet, selectorMatches, property);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string Outline(Control control) =>
        FirstVar(Property(control.Stylesheet, s => s == control.BoxSelector, "border"))
        ?? throw new InvalidOperationException($"{control.Name}: the border takes no token at all.");

    private static string BoxFill(Control control) =>
        TryProperty(control.Stylesheet, s => s == control.BoxSelector, "background-color")
        ?? control.BoxFillFallback;

    private static string FilledFill(Control control) =>
        Property(control.Stylesheet,
            s => s.Contains(control.FilledSelectorFragment, StringComparison.Ordinal),
            control.FilledFillProperty);

    /// <summary>The colour the stylesheet gives the glyph. Only defined for <see cref="GlyphControls"/>.</summary>
    private static string Glyph(Control control) =>
        Property(control.Stylesheet,
            s => s.Contains(control.GlyphSelector!, StringComparison.Ordinal), "color");

    private static double Contrast(string foreground, string background)
    {
        static double Channel(double value)
        {
            value /= 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        static double Luminance(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            hex.Length.Should().Be(6, $"'{hex}' must resolve to an opaque #rrggbb colour");
            var channels = Enumerable.Range(0, 3)
                .Select(i => Channel(int.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture)))
                .ToArray();
            return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
        }

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Ratio(string foregroundValue, string backgroundValue, bool dark)
    {
        var tokens = TokenGraph(dark);
        return Contrast(ResolveColour(foregroundValue, tokens), ResolveColour(backgroundValue, tokens));
    }

    // ── Anchors: the stylesheets must keep reaching for the tokens the ratios are computed from ──

    [Theory]
    [MemberData(nameof(Controls))]
    public void SelectionControl_TakesItsOutlineFromTheControlBorderToken(Control control)
        => Outline(control).Should().Be(OutlineValue,
            "--tm-border-color is the DECORATIVE divider tone (1.24:1 light / 1.41:1 dark) and cannot "
            + "carry the boundary of a control whose state that boundary conveys; the fallback keeps "
            + "the shorthand valid against an older token file, where an unresolved var() would drop "
            + "the outline entirely");

    [Theory]
    [MemberData(nameof(GlyphControls))]
    public void SelectionControl_TakesItsGlyphFromTheControlGlyphToken(Control control)
        => Glyph(control).Should().Be(GlyphValue,
            "a hardcoded colour bypasses the token graph, so the dark theme cannot flip it and the "
            + "glyph stays at 2.54:1 on the lighter dark fill");

    [Fact]
    public void DarkTheme_FlipsTheControlTokens_ForBothThemingApis()
    {
        var dark = Normalise(StripComments(File.ReadAllText(CssPath("tokens-dark.css"))));

        dark.Should().Contain("[data-theme=\"dark\"], .tm-dark {",
            "both theming APIs are public — a consumer switching with the class must get the fix too");
        dark.Should().Contain("--tm-control-glyph-color:", "the glyph flip lives in the token file");
        dark.Should().Contain("--tm-border-color-control:", "the outline flip lives in the token file");
    }

    // ── Measurements ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Controls))]
    public void Outline_MeetsNonTextContrast_AgainstItsOwnSurface_InDark(Control control)
        => Ratio(Outline(control), BoxFill(control), dark: true)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "an unselected control is nothing but its outline (was 1.41:1)");

    [Theory]
    [MemberData(nameof(Controls))]
    public void Outline_MeetsNonTextContrast_AgainstItsOwnSurface_InLight(Control control)
        => Ratio(Outline(control), BoxFill(control), dark: false)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "the outline rule is theme-neutral, and light was the WORSE half at 1.24:1");

    [Theory]
    [MemberData(nameof(Controls))]
    public void Outline_MeetsNonTextContrast_OnMutedPanels_InDark(Control control)
        => Ratio(Outline(control), "var(--tm-bg-muted)", dark: true)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "selection controls also sit on muted panels, which in dark ARE the old border tone");

    [Theory]
    [MemberData(nameof(Controls))]
    public void Outline_MeetsNonTextContrast_OnMutedPanels_InLight(Control control)
        => Ratio(Outline(control), "var(--tm-bg-muted)", dark: false)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "selection controls also sit on muted panels in the light theme");

    [Theory]
    [MemberData(nameof(Controls))]
    public void FilledState_StaysDistinguishableFromTheSurface_InDark(Control control)
        => Ratio(FilledFill(control), BoxFill(control), dark: true)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum, "the fill must read as a state change");

    [Theory]
    [MemberData(nameof(Controls))]
    public void FilledState_StaysDistinguishableFromTheSurface_InLight(Control control)
        => Ratio(FilledFill(control), BoxFill(control), dark: false)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum, "the fill must read as a state change");

    [Theory]
    [MemberData(nameof(GlyphControls))]
    public void Glyph_MeetsNonTextContrast_OnItsFill_InDark(Control control)
    {
        Ratio(GlyphValue, FilledFill(control), dark: true).Should().BeGreaterThanOrEqualTo(NonTextMinimum,
            "the glyph is the only thing that distinguishes selected from unselected (was 2.54:1)");
    }

    [Theory]
    [MemberData(nameof(GlyphControls))]
    public void Glyph_DoesNotRegress_OnItsFill_InLight(Control control)
    {
        Ratio(GlyphValue, FilledFill(control), dark: false).Should().BeGreaterThanOrEqualTo(LightGlyphBaseline,
            "light already passed at 5.17:1 and the dark fix must not cost the light theme anything");
    }

    // ── The mixed (indeterminate) state ──────────────────────────────────────────────────────────

    [Fact]
    public void IndeterminateBox_IsKeyedOffTheRenderedClass_NotTheDeadPseudoClass()
    {
        var css = Normalise(ComponentCss("_checkbox.css"));

        css.Should().NotContain(":indeterminate",
            "nothing sets the input's indeterminate DOM property, so that pseudo-class never matched — "
            + "keying the fill off it painted the dash on an unfilled box");
        css.Should().Contain(".tm-checkbox-custom-indeterminate",
            "the fill must key off the class the component actually renders");

        var razor = File.ReadAllText(Path.Combine(RepositoryRoot().FullName,
            "src", "Tempo.Blazor", "Components", "Inputs", "TmCheckbox.razor"));
        razor.Should().Contain("tm-checkbox-custom-indeterminate",
            "a CSS class nobody renders is an unreachable fix");
        razor.Should().Contain("aria-checked", "the mixed state must be exposed to assistive technology");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndeterminateDash_MeetsNonTextContrast_InBothThemes(bool dark)
    {
        var indeterminateFill = Property("_checkbox.css",
            s => s.Contains(".tm-checkbox-custom-indeterminate", StringComparison.Ordinal),
            "background-color");

        Ratio(GlyphValue, indeterminateFill, dark).Should().BeGreaterThanOrEqualTo(NonTextMinimum,
            $"the dash used to be white on an unfilled box — 1.00:1 in light (dark: {dark})");
    }
}
