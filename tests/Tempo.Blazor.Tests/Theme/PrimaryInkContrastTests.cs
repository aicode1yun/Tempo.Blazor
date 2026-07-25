using FluentAssertions;

namespace Tempo.Blazor.Tests.Theme;

/// <summary>
/// Guards the ink that sits ON a primary fill — <c>--tm-color-on-primary</c> and its older alias
/// <c>--tm-color-primary-contrast</c> — in BOTH themes.
/// <para>
/// The dark theme repoints <c>--tm-color-primary</c> to the LIGHTER <c>primary-400</c>. White ink on it
/// measures 2.54:1, well under the 4.5:1 that SC 1.4.3 asks of body-sized text. The filled primary button
/// already worked around this with a per-component dark rule
/// (<c>[data-theme="dark"] .tm-btn-primary { color: var(--tm-text-inverse); }</c>), but every other
/// "ink on primary" site — the multiselect confirm button, the chat bubble, the Notion primary buttons,
/// the spreadsheet dialog — reached for a token that was pinned to white in both themes, so no theme could
/// reach it. Making the aliases resolve through <c>--tm-text-inverse</c> flips them once, for both theming
/// APIs, without a per-component dark block; the per-component workaround was then deleted, because a
/// shadowed patch left behind is what the next reader takes for the rule.
/// </para>
/// <para>
/// SCOPE, stated on purpose — this measures the TOKEN GRAPH, not the components. Of the 15 places that
/// consume one of these tokens, only the ones inside <c>Tempo.Blazor</c> itself sit in a project that has a
/// test assembly at all: <c>DocumentEditor</c>, <c>NotionEditor</c>, <c>Signing</c> and <c>Spreadsheet</c>
/// have none. A green run here says the ratios in the token graph are right; it does NOT say those
/// components were exercised.
/// </para>
/// <para>
/// One consumer is deliberately out of reach and has been disconnected instead: the collaborative cursor
/// label paints its ink on <c>--tm-wysiwyg-remote-cursor-color</c>, a per-user RUNTIME colour, so no
/// token-graph guard can cover it. It now takes its own paired knob (see <c>_document-editor.css</c>).
/// </para>
/// <para>
/// SECOND LIMIT, the same one <c>SelectionControlContrastTests</c> states: these ratios are Tempo's own
/// tokens. An application that repoints the primary scale per accent is NOT covered — and this token is the
/// one that does not follow such a repoint by itself, because it resolves through <c>--tm-text-inverse</c>
/// rather than living inside the <c>-50…-900</c> scale. Measured over a six-accent palette, three LIGHT
/// combinations already fall under the text threshold (teal 3.74, orange 3.56, emerald 3.77); dark composes
/// correctly for all six (6.45–9.59). That matrix belongs next to the consumer's theme file.
/// </para>
/// </summary>
public class PrimaryInkContrastTests
{
    /// <summary>WCAG 2.2 SC 1.4.3 — body-sized text needs 4.5:1 against its background.</summary>
    private const double TextMinimum = 4.5;

    /// <summary>WCAG 2.2 SC 1.4.11 — a non-text graphical object needs 3:1.</summary>
    private const double NonTextMinimum = 3.0;

    /// <summary>
    /// One name for "ink on a primary fill", and the threshold that name has to meet. The threshold differs
    /// because the ROLE of the pixels differs, not because the tokens do: a button label is text (4.5:1), a
    /// checkbox tick is a graphical object (3:1). Measuring the glyph against the text threshold would be the
    /// opposite mistake to leaving it out — a guard stricter than the criterion gets "fixed" by loosening it.
    /// </summary>
    public sealed record Ink(string Token, double Minimum, string Role)
    {
        public override string ToString() => Token;
    }

    /// <summary>
    /// Every token of this usage role. All three must resolve to the same colour; each must clear the
    /// threshold its own pixels are held to.
    /// </summary>
    private static readonly Ink[] AllInks =
    [
        new("--tm-color-on-primary", TextMinimum, "button and control labels"),
        new("--tm-color-primary-contrast", TextMinimum, "compatibility alias used by the chat bubble"),
        new("--tm-control-glyph-color", NonTextMinimum, "checkbox tick and multiselect option tick"),
    ];

    public static TheoryData<Ink> InkTokens() => [.. AllInks];

    /// <summary>
    /// The subset whose use sites also have a HOVER fill — the filled buttons. The glyph is excluded because
    /// a checked checkbox has no hover fill to measure against, not because it is less important; a case that
    /// measured it against a background it never sits on would be green without meaning anything.
    /// </summary>
    public static TheoryData<Ink> InksOnHoverableFills() =>
    [
        new("--tm-color-on-primary", TextMinimum, "button and control labels"),
        new("--tm-color-primary-contrast", TextMinimum, "compatibility alias used by the chat bubble"),
    ];

    [Theory]
    [MemberData(nameof(InkTokens))]
    public void Ink_MeetsItsContrastMinimum_OnTheRestingPrimaryFill_InLight(Ink ink)
        => ThemeCss.Ratio($"var({ink.Token})", "var(--tm-color-primary)", dark: false)
            .Should().BeGreaterThanOrEqualTo(ink.Minimum,
                $"light pairs the ink with primary-600 and already passed at 5.17:1 ({ink.Role}) — the dark "
                + "fix must not cost the light theme anything");

    [Theory]
    [MemberData(nameof(InkTokens))]
    public void Ink_MeetsItsContrastMinimum_OnTheRestingPrimaryFill_InDark(Ink ink)
        => ThemeCss.Ratio($"var({ink.Token})", "var(--tm-color-primary)", dark: true)
            .Should().BeGreaterThanOrEqualTo(ink.Minimum,
                $"dark repoints the fill to the LIGHTER primary-400, on which white ink is 2.54:1 ({ink.Role})");

    [Theory]
    [MemberData(nameof(InksOnHoverableFills))]
    public void Ink_MeetsItsContrastMinimum_OnTheHoveredPrimaryFill_InLight(Ink ink)
        => ThemeCss.Ratio($"var({ink.Token})", "var(--tm-color-primary-hover)", dark: false)
            .Should().BeGreaterThanOrEqualTo(ink.Minimum,
                "the hover fill is a second background the same ink has to survive");

    [Theory]
    [MemberData(nameof(InksOnHoverableFills))]
    public void Ink_MeetsItsContrastMinimum_OnTheHoveredPrimaryFill_InDark(Ink ink)
        => ThemeCss.Ratio($"var({ink.Token})", "var(--tm-color-primary-hover)", dark: true)
            .Should().BeGreaterThanOrEqualTo(ink.Minimum,
                "dark moves the hover fill LIGHTER (primary-300), not darker — the ink has to follow");

    /// <summary>
    /// No alias may drift away from the source: they name the same thing, and two independent definitions is
    /// exactly how the light-only white pinning survived a dark theme. Iterating over <see cref="InkTokens"/>
    /// rather than naming the tokens here means adding a fourth name cannot silently leave it unguarded.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllInkAliases_ResolveToTheSameColour(bool dark)
    {
        var tokens = ThemeCss.TokenGraph(dark);
        var source = ThemeCss.ResolveColour("var(--tm-color-on-primary)", tokens);

        AllInks.Should().HaveCountGreaterThan(1, "a single-element sweep would compare the source with itself");

        foreach (var ink in AllInks)
        {
            ThemeCss.ResolveColour($"var({ink.Token})", tokens)
                .Should().Be(source, $"{ink.Token} is a name for the same ink ({ink.Role})");
        }
    }

    // ── The multiselect confirm button: the site that hardcoded `white` ──────────────────────────

    private const string ConfirmButton = ".tm-multiselect__confirm-btn";
    private const string ConfirmButtonHover = ".tm-multiselect__confirm-btn:hover";

    [Fact]
    public void ConfirmButton_TakesItsInkFromTheOnPrimaryToken()
        => ThemeCss.Property("_multiselect.css", ConfirmButton, "color")
            .Should().Be("var(--tm-color-on-primary, var(--tm-color-white))",
                "a hardcoded `white` bypasses the token graph, so the dark theme cannot flip it and the "
                + "label stays at 2.54:1 on the lighter dark fill; the fallback keeps the declaration valid "
                + "against a token file that predates the alias");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmButton_Ink_MeetsTextContrast_InBothThemes(bool dark)
    {
        var ink = ThemeCss.Property("_multiselect.css", ConfirmButton, "color");
        var fill = ThemeCss.Property("_multiselect.css", ConfirmButton, "background");

        ThemeCss.Ratio(ink, fill, dark).Should().BeGreaterThanOrEqualTo(TextMinimum,
            $"the confirm button is body-sized text on a primary fill (dark: {dark})");
    }

    /// <summary>
    /// The hover fill was pinned to <c>primary-600</c>, which IS the resting fill in the light theme: the
    /// button had no hover feedback at all in light, and in dark it moved the wrong way (from the lighter
    /// primary-400 to a darker shade). Taking <c>--tm-color-primary-hover</c> makes both themes move away
    /// from their own resting fill.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmButton_Hover_ActuallyChangesTheFill(bool dark)
    {
        var tokens = ThemeCss.TokenGraph(dark);
        var resting = ThemeCss.ResolveColour(ThemeCss.Property("_multiselect.css", ConfirmButton, "background"), tokens);
        var hovered = ThemeCss.ResolveColour(ThemeCss.Property("_multiselect.css", ConfirmButtonHover, "background"), tokens);

        hovered.Should().NotBe(resting,
            $"a hover rule that repaints the resting colour is a no-op the user cannot see (dark: {dark})");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmButton_Ink_MeetsTextContrast_OnTheHoverFill_InBothThemes(bool dark)
    {
        var ink = ThemeCss.Property("_multiselect.css", ConfirmButton, "color");
        var hoverFill = ThemeCss.Property("_multiselect.css", ConfirmButtonHover, "background");

        ThemeCss.Ratio(ink, hoverFill, dark).Should().BeGreaterThanOrEqualTo(TextMinimum,
            $"the hovered fill is a background the same label has to survive (dark: {dark})");
    }
}
