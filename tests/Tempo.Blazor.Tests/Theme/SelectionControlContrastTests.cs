using System.Globalization;
using FluentAssertions;

namespace Tempo.Blazor.Tests.Theme;

/// <summary>
/// Guards WCAG 2.2 SC 1.4.11 (non-text contrast, 3:1) for the three 1.5px selection controls —
/// <c>TmCheckbox</c>, <c>TmRadio</c> and the multiselect option checkbox — in BOTH themes, and the
/// perceptibility of their HOVER state.
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

    /// <summary>
    /// The hover-fill token, with the resting fill as its fallback: on a consumer whose token file predates
    /// the token, the declaration still resolves and the control simply keeps today's appearance, instead of
    /// dropping to <c>transparent</c> and losing its box.
    /// </summary>
    private const string HoverFillValue = "var(--tm-control-hover-fill, var(--tm-bg-surface))";

    /// <summary>One selection control: where its stylesheet declares the colours that carry state.</summary>
    public sealed record Control(
        string Name,
        string Stylesheet,
        string BoxSelector,
        string FilledSelector,
        string FilledFillProperty,
        string? GlyphSelector,
        string BoxFillFallback,
        string? HoverSelector = null,
        string? HoverColourSelector = null)
    {
        public override string ToString() => Name;
    }

    private static readonly Control Checkbox = new("checkbox", "_checkbox.css", ".tm-checkbox-custom",
        ".tm-checkbox-input:checked ~ .tm-checkbox-custom", "background-color", ".tm-checkbox-check",
        BoxFillFallback: "var(--tm-bg-surface)",
        HoverSelector: ".tm-checkbox-label:hover .tm-checkbox-input:not(:checked) ~ .tm-checkbox-custom",
        HoverColourSelector: ".tm-checkbox-label:hover .tm-checkbox-custom");

    // A radio marks its state with a filled dot (::after), not with a glyph — hence no glyph selector.
    private static readonly Control Radio = new("radio", "_radio-group.css", ".tm-radio-custom",
        ".tm-radio-input:checked ~ .tm-radio-custom::after", "background-color", GlyphSelector: null,
        BoxFillFallback: "var(--tm-bg-surface)",
        HoverSelector: ".tm-radio-option:hover .tm-radio-input:not(:checked) ~ .tm-radio-custom",
        HoverColourSelector: ".tm-radio-option:hover .tm-radio-custom");

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

    /// <summary>
    /// Only the controls whose hover state is the control's OWN. The multiselect option is excluded by
    /// measurement, not by omission: its hover is carried by the whole option row
    /// (<c>.tm-multiselect__option:hover</c> → <c>--tm-bg-muted</c>), a far larger graphical object, and a
    /// second tint inside that row measured 1.00:1 against it in the dark theme — the row highlight and
    /// <c>primary-900</c> are isoluminant. Adding one would have been a change that cannot be seen.
    /// </summary>
    public static TheoryData<Control> HoverFillControls() => [Checkbox, Radio];

    // ── Reading the stylesheets ──────────────────────────────────────────────────────────────────

    private static string Outline(Control control) =>
        ThemeCss.FirstVar(ThemeCss.Property(control.Stylesheet, control.BoxSelector, "border"))
        ?? throw new InvalidOperationException($"{control.Name}: the border takes no token at all.");

    private static string BoxFill(Control control) =>
        ThemeCss.TryProperty(control.Stylesheet, control.BoxSelector, "background-color")
        ?? control.BoxFillFallback;

    private static string FilledFill(Control control) =>
        ThemeCss.Property(control.Stylesheet, control.FilledSelector, control.FilledFillProperty);

    /// <summary>The colour the stylesheet gives the glyph. Only defined for <see cref="GlyphControls"/>.</summary>
    private static string Glyph(Control control) =>
        ThemeCss.Property(control.Stylesheet, control.GlyphSelector!, "color");

    /// <summary>The fill the box takes while hovered and unselected — the resting fill when there is none.</summary>
    private static string HoverFill(Control control) =>
        ThemeCss.TryProperty(control.Stylesheet, control.HoverSelector!, "background-color") ?? BoxFill(control);

    /// <summary>
    /// The outline the box takes while hovered and unselected. The colour comes from the label-level hover
    /// rule (which is not scoped to :not(:checked) — a checked box already carries the primary border), the
    /// width from the unchecked-scoped rule.
    /// </summary>
    private static string HoverOutline(Control control) =>
        ThemeCss.TryProperty(control.Stylesheet, control.HoverColourSelector!, "border-color")
        ?? ThemeCss.TryProperty(control.Stylesheet, control.HoverSelector!, "border-color")
        ?? Outline(control);

    /// <summary>Width in px of the <c>border</c> shorthand on the resting box.</summary>
    private static double RestingBorderWidth(Control control) =>
        Pixels(ThemeCss.Property(control.Stylesheet, control.BoxSelector, "border").Split(' ')[0]);

    /// <summary>Width in px the border takes while hovered and unselected.</summary>
    private static double HoverBorderWidth(Control control) =>
        Pixels(ThemeCss.TryProperty(control.Stylesheet, control.HoverSelector!, "border-width")
               ?? ThemeCss.Property(control.Stylesheet, control.BoxSelector, "border").Split(' ')[0]);

    private static double Pixels(string value) =>
        double.Parse(value.Trim().TrimEnd('p', 'x'), CultureInfo.InvariantCulture);

    /// <summary>The properties a control's hover changes, and therefore the ones that must be animated.</summary>
    private static IEnumerable<string> HoverAnimatedProperties(Control control)
    {
        yield return "border-color";
        foreach (var property in new[] { "border-width", "background-color" })
        {
            if (ThemeCss.TryProperty(control.Stylesheet, control.HoverSelector!, property) is not null)
            {
                yield return property;
            }
        }
    }

    private static double Ratio(string foreground, string background, bool dark) =>
        ThemeCss.Ratio(foreground, background, dark);

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

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void SelectionControl_TakesItsHoverFillFromTheControlHoverToken(Control control)
        => ThemeCss.TryProperty(control.Stylesheet, control.HoverSelector!, "background-color")
            .Should().Be(HoverFillValue,
                "the hover state must move onto the box FILL through the token graph; re-colouring only the "
                + "outline leaves a hue-only change the dark theme cannot deepen either");

    [Fact]
    public void DarkTheme_FlipsTheControlTokens_ForBothThemingApis()
    {
        var dark = ThemeCss.Normalise(ThemeCss.StripComments(File.ReadAllText(ThemeCss.CssPath("tokens-dark.css"))));

        dark.Should().Contain("[data-theme=\"dark\"], .tm-dark {",
            "both theming APIs are public — a consumer switching with the class must get the fix too");
        dark.Should().Contain("--tm-border-color-control:",
            "the outline is a DIFFERENT usage role (the boundary itself, not ink on a fill), so it keeps "
            + "its own dark override by right");
        dark.Should().Contain("--tm-control-hover-fill:",
            "a light tint on a dark box would be brighter than the checked fill — the hover fill flips too");
    }

    /// <summary>
    /// The glyph clause used to live in the test above as
    /// <c>dark.Should().Contain("--tm-control-glyph-color:")</c>. That asserted a MECHANISM — "every control
    /// token carries its own dark override" — and the token now deliberately does the opposite: it aliases
    /// <c>--tm-color-on-primary</c>, one source with several names. A mechanism assert reports that
    /// consolidation as a regression BY CONSTRUCTION, which is how a guard ends up re-introducing the second
    /// copy it was meant to prevent. The invariant is what actually matters and it survives any number of
    /// files and lines producing it: the glyph resolves to the same colour as the ink on the same fill.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Glyph_ResolvesToTheSameColourAsTheInkOnAPrimaryFill(bool dark)
    {
        var tokens = ThemeCss.TokenGraph(dark);

        ThemeCss.ResolveColour("var(--tm-control-glyph-color)", tokens)
            .Should().Be(ThemeCss.ResolveColour("var(--tm-color-on-primary)", tokens),
                "the tick on a filled checkbox and the label on a filled button are the same usage role; two "
                + "independent definitions of it is how one of them kept the light-only white");
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

    // ── The hover state ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// THE acceptance criterion for hover, and it is a WCAG one, not a house number: hover is a state, and
    /// SC 1.4.11 asks 3:1 of the visual information that identifies a component's state. So at least one
    /// AREA of the control must change from one colour to another, and the newly-coloured pixels must reach
    /// 3:1 against what they replaced.
    /// <para>
    /// Re-colouring an area is NOT enough on its own: the shipped 2.5.4 hover re-coloured the outline from
    /// <c>gray-500</c> to <c>primary-600</c> — same pixels, near-isoluminant, 1.07:1 light / 1.01:1 dark,
    /// i.e. 1.00 in greyscale. The area that changes here is the ring the outline GAINS when it grows from
    /// 1.5px to 2.5px: those pixels were box fill and become outline colour.
    /// </para>
    /// <para>
    /// Deliberately NOT asserted: the size of the hover TINT (measured 1.22:1 light / 1.41:1 dark). It is
    /// cosmetic, it is below any threshold worth writing down, and pinning the guard to it would be writing
    /// the test against what the implementation happens to produce instead of against the criterion.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void Hover_ChangesAnArea_NotOnlyAHue(Control control)
        => HoverBorderWidth(control).Should().BeGreaterThan(RestingBorderWidth(control),
            "hover must repaint pixels that were something else, not re-tint the same pixels: the outline "
            + "already has to stay above 3:1 against the surface in BOTH states, so no pair of outline "
            + "colours can also differ enough from each other to be seen — the mechanism has to change");

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoverArea_NewlyColouredPixels_MeetNonTextContrast_InLight(Control control)
        => Ratio(HoverOutline(control), BoxFill(control), dark: false)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "the ring the outline gains covers resting box fill, and SC 1.4.11 governs the state change");

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoverArea_NewlyColouredPixels_MeetNonTextContrast_InDark(Control control)
        => Ratio(HoverOutline(control), BoxFill(control), dark: true)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "the ring the outline gains covers resting box fill, and SC 1.4.11 governs the state change");

    /// <summary>
    /// Everything hover changes must be in the resting rule's <c>transition</c>. The radio was missing
    /// <c>background-color</c> while the checkbox had it, so the new tint would have snapped on one control
    /// and faded on the other in the same form — a difference a user reads as the application stalling.
    /// Deriving the list from what the hover rule ACTUALLY declares is what makes this survive the next
    /// property somebody adds.
    /// </summary>
    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void EveryPropertyHoverChanges_IsAnimated(Control control)
    {
        var transition = ThemeCss.Property(control.Stylesheet, control.BoxSelector, "transition");

        foreach (var property in HoverAnimatedProperties(control))
        {
            transition.Should().Contain(property,
                $"{control.Name} hover changes {property}, so it must not jump while the others fade");
        }
    }

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoveredOutline_KeepsNonTextContrast_AgainstTheHoverFill_InLight(Control control)
        => Ratio(HoverOutline(control), HoverFill(control), dark: false)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "SC 1.4.11 does not pause while the pointer is over the control — the outline still has to "
                + "be seen against what it now encloses");

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoveredOutline_KeepsNonTextContrast_AgainstTheHoverFill_InDark(Control control)
        => Ratio(HoverOutline(control), HoverFill(control), dark: true)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "SC 1.4.11 does not pause while the pointer is over the control");

    /// <summary>
    /// (C) The tint gets a DIRECTION, not a number. A numeric floor on it would be a threshold read off the
    /// implementation — the affordance is carried by the ring, and the tint only has to move towards the
    /// accent rather than away from it: darker than the surface in light, lighter in dark. Anything stronger
    /// is not better, it is worse: assertion (B) exists precisely to stop the tint from growing until the
    /// outline drowns in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoverTint_MovesTowardsTheAccent_InBothThemes(Control control)
    {
        ThemeCss.LuminanceOf(HoverFill(control), dark: false)
            .Should().BeLessThan(ThemeCss.LuminanceOf(BoxFill(control), dark: false),
                "on a white surface the accent tint is the darker of the two");

        ThemeCss.LuminanceOf(HoverFill(control), dark: true)
            .Should().BeGreaterThan(ThemeCss.LuminanceOf(BoxFill(control), dark: true),
                "on a dark surface it is the lighter of the two — a light tint copied from the light theme "
                + "would end up brighter than the checked fill");
    }

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoverFill_StaysDistinguishableFromTheCheckedFill_InLight(Control control)
        => Ratio(HoverFill(control), FilledFill(control), dark: false)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "a hover tint that reads as the checked fill would announce a state the user has not chosen");

    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoverFill_StaysDistinguishableFromTheCheckedFill_InDark(Control control)
        => Ratio(HoverFill(control), FilledFill(control), dark: true)
            .Should().BeGreaterThanOrEqualTo(NonTextMinimum,
                "a hover tint that reads as the checked fill would announce a state the user has not chosen");

    /// <summary>
    /// Neither the tint nor the thicker ring may repaint a SELECTED control, so both live in one rule scoped
    /// to the unchecked state. Unscoped it would match a checked box at equal specificity and win on order:
    /// the tint would cover the primary fill and hide the glyph the user just switched on, and the ring would
    /// eat 1px of that fill.
    /// </summary>
    [Theory]
    [MemberData(nameof(HoverFillControls))]
    public void HoverFillAndRing_AreScopedToTheUncheckedState(Control control)
    {
        control.HoverSelector.Should().Contain(":not(:checked)",
            "a hover rule that also matches the checked box overrides the checked fill on pointer-over");

        ThemeCss.TryProperty(control.Stylesheet, control.HoverSelector!, "border-width")
            .Should().NotBeNull("the ring must be declared in the unchecked-scoped rule, not next to the colour");
    }

    // ── The mixed (indeterminate) state ──────────────────────────────────────────────────────────

    [Fact]
    public void IndeterminateBox_IsKeyedOffTheRenderedClass_NotTheDeadPseudoClass()
    {
        var css = ThemeCss.Normalise(ThemeCss.ComponentCss("_checkbox.css"));

        css.Should().NotContain(":indeterminate",
            "nothing sets the input's indeterminate DOM property, so that pseudo-class never matched — "
            + "keying the fill off it painted the dash on an unfilled box");
        css.Should().Contain(".tm-checkbox-custom-indeterminate",
            "the fill must key off the class the component actually renders");

        var razor = File.ReadAllText(Path.Combine(ThemeCss.RepositoryRoot().FullName,
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
        Ratio(GlyphValue, MixedFill(), dark).Should().BeGreaterThanOrEqualTo(NonTextMinimum,
            $"the dash used to be white on an unfilled box — 1.00:1 in light (dark: {dark})");
    }

    private static string MixedFill() =>
        ThemeCss.Property("_checkbox.css", ".tm-checkbox-custom-indeterminate", "background-color");

    /// <summary>
    /// The rule that gives the mixed box its fill back while the pointer is over it. A COMPOUND on purpose:
    /// <c>TmCheckbox</c> puts both classes on one span, and the modifier alone would score (0,5,0) — a TIE
    /// with the generic hover rule, decided by source order instead of by specificity.
    /// </summary>
    private const string MixedHoverSelector = ".tm-checkbox-label:hover .tm-checkbox-input:not(:checked) ~ "
                                             + ".tm-checkbox-custom.tm-checkbox-custom-indeterminate";

    /// <summary>
    /// The fill a MIXED box actually shows while the pointer is over it, as a fall-THROUGH rather than a
    /// lookup: the rule that takes the fill back, else the generic hover tint that would otherwise cover it,
    /// else the resting mixed fill. The order is the point. Ask only for the mixed-hover rule and deleting
    /// it yields <c>null</c>, i.e. a failure about a missing selector; ask only for the resting fill and
    /// deleting it yields the fill that is still declared, i.e. a guard that cannot fail at all. Falling
    /// through to the tint makes the deletion show up as what it IS — the tint landing on the mixed box.
    /// <para>
    /// The last link cannot silently absorb a broken sweep: if the generic hover rule ever stopped being
    /// found, <see cref="SelectionControl_TakesItsHoverFillFromTheControlHoverToken"/> would be red, because
    /// it reads the same selector and asserts the token it declares.
    /// </para>
    /// </summary>
    private static string HoveredMixedFill() =>
        ThemeCss.TryProperty("_checkbox.css", MixedHoverSelector, "background-color")
        ?? ThemeCss.TryProperty("_checkbox.css", Checkbox.HoverSelector!, "background-color")
        ?? MixedFill();

    /// <summary>
    /// THE mixed state has to survive the pointer. Hover feedback may not cost a control the information it
    /// was displaying: SC 1.4.11 asks 3:1 of the visual information identifying a component's STATE, and
    /// mixed is a state — one the user is typically about to act on, since the pointer is already on it.
    /// <para>
    /// It needs a guard of its own, separate from
    /// <see cref="IndeterminateDash_MeetsNonTextContrast_InBothThemes"/>, because the mixed state is NOT
    /// <c>:checked</c>: <c>TmCheckbox</c> renders <c>checked="false"</c> for it and marks the box with a
    /// class, so the hover rule's <c>:not(:checked)</c> — which does protect the checked box — sails
    /// straight past it. Left to that rule the dash measured 1.22:1 in light and 1.72:1 in dark, i.e. a
    /// hovered mixed box was indistinguishable from a hovered EMPTY one.
    /// </para>
    /// <para>
    /// 3:1 is the WCAG threshold, not the measured value: the mixed dash sits at 5.17:1 / 7.02:1 and the
    /// guard deliberately does not pin those, so a future change is free to move the fill as long as the
    /// dash stays legible on it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedDash_KeepsNonTextContrast_WhileHovered_InBothThemes(bool dark)
        => Ratio(GlyphValue, HoveredMixedFill(), dark).Should().BeGreaterThanOrEqualTo(NonTextMinimum,
            $"hover must not repaint the fill the mixed dash is drawn on (dark: {dark})");

    /// <summary>
    /// The unspoken premise of the guard above, said out loud. That guard asks the stylesheet which rule
    /// declares the mixed hover fill and then treats the answer as the colour on screen — "the rule is
    /// there, so the rule won". Reading a stylesheet cannot check that: a rule is found by its selector,
    /// whether or not the cascade lets it paint anything. The premise holds only while the mixed rule
    /// outranks the generic hover rule, and if it stopped holding the guard would not go red — it would go
    /// green while the browser painted the tint, which is the failure this whole phase is about.
    /// <para>
    /// It ranks them the way the cascade does, specificity first and source order only as the tie-break,
    /// so it does not forbid the other correct arrangement (an equally specific rule placed afterwards) —
    /// it forbids the broken one.
    /// </para>
    /// <para>
    /// THE DIVISION OF LABOUR, written down because it is otherwise only true while someone remembers it:
    /// this guard answers "is the rule specific enough to win", and it is deliberately SILENT when the rule
    /// is not there at all — a rank of (6, -1) against a rule that exists still ranks, and the case is not
    /// its business. What covers the absence is
    /// <see cref="MixedDash_KeepsNonTextContrast_WhileHovered_InBothThemes"/>, which falls through to the
    /// tint and goes red on the ratio. The two are a PAIR and each has its own discriminator: delete the
    /// rule and the ratio goes red while this stays green; weaken the selector to the tie and this goes red
    /// while the ratio stays green, because reading the stylesheet cannot see a rule losing the cascade.
    /// Neither may be made conditional on the other's subject, or the absence stops being measured.
    /// </para>
    /// <para>
    /// MUTATING THIS ONE TAKES TWO FILES. The selector it ranks comes from <see cref="MixedHoverSelector"/>,
    /// a constant that mirrors the stylesheet, so weakening the rule in the CSS alone would make the lookup
    /// miss instead of making the rank lose — red from "not found" rather than from "loses the cascade",
    /// which is the wrong quantity and would tell us nothing. Weaken BOTH: the compound in
    /// <c>_checkbox.css</c> and the constant here.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMixedHoverRule_OutranksTheGenericHoverRule_InTheCascade()
    {
        var mixed = CascadeRank(MixedHoverSelector);
        var generic = CascadeRank(Checkbox.HoverSelector!);

        Comparer<(int, int)>.Default.Compare(mixed, generic).Should().BePositive(
            $"the mixed rule must be the one the browser paints, not merely the one the test finds; "
            + $"ranked (classes, position) it is {mixed} against the generic rule's {generic}");
    }

    /// <summary>
    /// Where a rule stands in the cascade against another rule of the same file and origin: its specificity
    /// first, its position in the file only to break a tie. Position is taken from the declaration itself
    /// (the selector followed by its brace) — the generic hover selector is a PREFIX of the mixed one, so
    /// searching for the bare text would find the mixed rule and report the wrong order.
    /// </summary>
    private static (int Classes, int Position) CascadeRank(string selector) =>
        (ClassCount(selector),
         ThemeCss.Normalise(ThemeCss.ComponentCss("_checkbox.css"))
             .IndexOf(selector + " {", StringComparison.Ordinal));

    /// <summary>
    /// The middle column of a selector's specificity — classes, attributes and pseudo-classes — which is the
    /// only column these selectors use. <c>:not(X)</c> counts as X and not as itself (Selectors L4).
    /// <para>
    /// Anything else it meets is thrown on rather than scored as zero. An id or a type selector would be
    /// counted wrongly, and a counter that quietly agrees with whatever it is handed would report the tie it
    /// exists to detect as a win.
    /// </para>
    /// </summary>
    private static int ClassCount(string selector)
    {
        var count = 0;
        for (var i = 0; i < selector.Length;)
        {
            var c = selector[i];
            if (c is ' ' or '>' or '+' or '~')
            {
                i++;
            }
            else if (c is '.' or ':')
            {
                if (c == ':' && i + 1 < selector.Length && selector[i + 1] == ':')
                {
                    throw new NotSupportedException(
                        $"Pseudo-ELEMENT at '{selector[i..]}' scores in the type column, not this one.");
                }

                var start = ++i;
                while (i < selector.Length && (char.IsLetterOrDigit(selector[i]) || selector[i] == '-'))
                {
                    i++;
                }

                var name = selector[start..i];
                if (name.Length == 0)
                {
                    throw new NotSupportedException($"Empty selector name at '{selector[(start - 1)..]}'.");
                }

                if (i < selector.Length && selector[i] == '(')
                {
                    var open = i;
                    var depth = 0;
                    do
                    {
                        depth += selector[i] switch { '(' => 1, ')' => -1, _ => 0 };
                        i++;
                    }
                    while (depth > 0 && i < selector.Length);

                    // :not() takes the specificity of its argument; no other functional pseudo-class is
                    // modelled, because each has its own rule and guessing would score it silently.
                    if (name != "not")
                    {
                        throw new NotSupportedException($"Unmodelled functional pseudo-class ':{name}()'.");
                    }

                    count += ClassCount(selector[(open + 1)..(i - 1)]);
                }
                else
                {
                    count++;
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Unmodelled selector construct at '{selector[i..]}' — ids, types and attribute "
                    + "selectors are not scored here, and silently ignoring one would understate the rank.");
            }
        }

        return count;
    }
}
