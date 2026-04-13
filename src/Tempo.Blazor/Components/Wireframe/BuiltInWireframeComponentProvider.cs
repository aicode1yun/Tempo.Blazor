using System.Text;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Wireframe.Models;
using static Tempo.Blazor.Components.Wireframe.WireframeSvg;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Registers wireframe definitions for all built-in Tempo.Blazor components.
/// Priority 0 – custom providers with higher priority can override individual types.
/// </summary>
public sealed class BuiltInWireframeComponentProvider : IWireframeComponentProvider
{
    /// <inheritdoc/>
    public string ProviderId => "BuiltIn";

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public IEnumerable<WireframeComponentDef> GetDefinitions()
    {
        foreach (var d in Buttons()) yield return d;
        foreach (var d in Inputs()) yield return d;
        foreach (var d in Pickers()) yield return d;
        foreach (var d in DataDisplay()) yield return d;
        foreach (var d in DataTable()) yield return d;
        foreach (var d in Feedback()) yield return d;
        foreach (var d in Navigation()) yield return d;
        foreach (var d in Layout()) yield return d;
        foreach (var d in Forms()) yield return d;
        foreach (var d in Files()) yield return d;
        foreach (var d in Charts()) yield return d;
        foreach (var d in Complex()) yield return d;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WireframeComponentDef Def(
        string type, string category, string displayName, string? icon,
        double w, double h,
        Action<WireframeElement, RenderTreeBuilder> render,
        PropDef[]? props = null,
        IReadOnlyDictionary<string, (double W, double H)>? sizePresets = null)
        => new()
        {
            Type = type,
            Category = category,
            DisplayName = displayName,
            Icon = icon,
            DefaultWidth = w,
            DefaultHeight = h,
            Props = props ?? [],
            IsBuiltIn = true,
            RenderSvg = render,
            SizePresets = sizePresets
        };

    // Size preset maps — shared across button-family components
    private static readonly IReadOnlyDictionary<string, (double W, double H)> ButtonSizes = new Dictionary<string, (double, double)>
    {
        ["xs"] = (80,  24),
        ["sm"] = (100, 30),
        ["md"] = (120, 36),
        ["lg"] = (140, 44),
    };

    private static readonly IReadOnlyDictionary<string, (double W, double H)> SplitButtonSizes = new Dictionary<string, (double, double)>
    {
        ["xs"] = (100, 24),
        ["sm"] = (120, 30),
        ["md"] = (140, 36),
        ["lg"] = (160, 44),
    };

    private static readonly IReadOnlyDictionary<string, (double W, double H)> IconButtonSizes = new Dictionary<string, (double, double)>
    {
        ["xs"] = (24, 24),
        ["sm"] = (28, 28),
        ["md"] = (36, 36),
        ["lg"] = (44, 44),
    };

    private static readonly IReadOnlyDictionary<string, (double W, double H)> BadgeSizes = new Dictionary<string, (double, double)>
    {
        ["sm"] = (48, 18),
        ["md"] = (60, 22),
        ["lg"] = (72, 26),
    };

    private static readonly IReadOnlyDictionary<string, (double W, double H)> SpinnerSizes = new Dictionary<string, (double, double)>
    {
        ["sm"] = (16, 16),
        ["md"] = (32, 32),
        ["lg"] = (48, 48),
    };

    private static PropDef Prop(string name, string display, PropType type,
        object? def = null, string[]? opts = null, string? cat = null, bool req = false)
        => new() { Name = name, DisplayName = display, Type = type, Default = def, Options = opts, Category = cat, IsRequired = req };

    private static void Svg(RenderTreeBuilder b, string markup)
        => b.AddMarkupContent(0, markup);

    /// <summary>
    /// Maps xs/sm/md/lg size tokens to a (fontSize, borderRadius) tuple.
    /// Components should scale their visual elements accordingly.
    /// </summary>
    private static (double font, double rx) SizeScale(string size) => size switch
    {
        "xs" => (9,  4),
        "sm" => (10, 5),
        "lg" => (13, 8),
        _    => (11, 6),   // md default
    };

    // ══════════════════════════════════════════════════════════════════════════
    // BUTTONS
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatButtons = "Buttons";

    private static IEnumerable<WireframeComponentDef> Buttons()
    {
        yield return Def("TmButton", CatButtons, "Button", "square", 120, 36,
            (el, b) =>
            {
                var label = el.Props.GetString("label", "Button");
                var variant = el.Props.GetString("variant", "primary");
                var fill = variant == "primary" ? FillAccent : Fill;
                var border = variant == "danger" ? "#fca5a5" : variant == "primary" ? "#93c5fd" : Border;
                var (font, rx) = SizeScale(el.Props.GetString("size", "md"));
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, fill, border, rx));
                sb.Append(TextCentred(label, el.W, el.H, font, ColorText, "500"));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Button", cat: "Content", req: true),
                Prop("variant", "Variant", PropType.Enum, "primary",
                    opts: ["primary","secondary","ghost","danger","outline","link","default"], cat: "Appearance"),
                Prop("size", "Size", PropType.Enum, "md", opts: ["xs","sm","md","lg"], cat: "Appearance"),
                Prop("icon", "Icon", PropType.Icon, cat: "Content"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
                Prop("loading", "Loading", PropType.Bool, false, cat: "Behavior"),
            ],
            sizePresets: ButtonSizes);

        yield return Def("TmSplitButton", CatButtons, "Split Button", "layout", 140, 36,
            (el, b) =>
            {
                var label = el.Props.GetString("label", "Action");
                var (font, rx) = SizeScale(el.Props.GetString("size", "md"));
                var w = el.W; var h = el.H;
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, w, h, FillAccent, "#93c5fd", rx));
                sb.Append(TextCentred(label, w - 28, h, font, ColorText));
                sb.Append(VLine(w - 28, 0, h));
                sb.Append(Rect(w - 28, 0, 28, h, FillAccent, "none", 0));
                sb.Append(ChevronDown(w - 20, h / 2 - 4));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Action", cat: "Content"),
                Prop("variant", "Variant", PropType.Enum, "primary",
                    opts: ["primary","secondary","ghost","danger"], cat: "Appearance"),
                Prop("size", "Size", PropType.Enum, "md", opts: ["xs","sm","md","lg"], cat: "Appearance"),
            ],
            sizePresets: SplitButtonSizes);

        yield return Def("TmCopyButton", CatButtons, "Copy Button", "copy", 36, 36,
            (el, b) =>
            {
                var (_, rx) = SizeScale(el.Props.GetString("size", "md"));
                var h = el.H;
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, h, Fill, Border, rx));
                sb.Append(Icon("copy", el.W / 2, h / 2, h * 0.45));
                Svg(b, sb.ToString());
            },
            [
                Prop("size", "Size", PropType.Enum, "md", opts: ["xs","sm","md","lg"], cat: "Appearance"),
            ],
            sizePresets: IconButtonSizes);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INPUTS
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatInputs = "Inputs";

    private static IEnumerable<WireframeComponentDef> Inputs()
    {
        yield return Def("TmTextInput", CatInputs, "Text Input", "edit-2", 240, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var ph = el.Props.GetString("placeholder", "Enter text...");
                var req = el.Props.GetBool("required");
                Svg(b, InputField(el.W, 36, lbl, ph, req));
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "Enter text...", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
                Prop("readOnly", "Read Only", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmTextArea", CatInputs, "Text Area", "align-left", 240, 100,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var ph = el.Props.GetString("placeholder", "Enter text...");
                var req = el.Props.GetBool("required");
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, el.W, el.H - 16));
                sb.Append(Text(ph, 8, 16, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "Enter text...", cat: "Content"),
                Prop("rows", "Rows", PropType.Int, 3, cat: "Appearance"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmNumberInput", CatInputs, "Number Input", "hash", 200, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                var w = el.W; var h = 36.0;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, w, h));
                sb.Append(Text("0", 8, h / 2, 11, ColorText));
                sb.Append(VLine(w - 24, 0, h));
                sb.Append(Text("▲", w - 16, h / 2 - 6, 8, ColorMuted, "middle"));
                sb.Append(Text("▼", w - 16, h / 2 + 5, 8, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("min", "Min", PropType.Double, cat: "Behavior"),
                Prop("max", "Max", PropType.Double, cat: "Behavior"),
                Prop("step", "Step", PropType.Double, 1.0, cat: "Behavior"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmSearchInput", CatInputs, "Search Input", "search", 240, 36,
            (el, b) =>
            {
                var ph = el.Props.GetString("placeholder", "Search...");
                Svg(b, InputField(el.W, el.H, "", ph, hasIcon: true));
            },
            [
                Prop("placeholder", "Placeholder", PropType.String, "Search...", cat: "Content"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmCurrencyInput", CatInputs, "Currency Input", "dollar-sign", 200, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Amount");
                var sym = el.Props.GetString("currencySymbol", "Kč");
                var h = 36.0;
                var sb = new StringBuilder();
                sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, el.W, h));
                sb.Append(Rect(0, 0, 28, h, FillDark, Border, 0));
                sb.Append(Text(sym, 14, h / 2, 10, ColorMuted, "middle"));
                sb.Append(Text("0.00", 36, h / 2, 11, ColorText));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Amount", cat: "Content"),
                Prop("currencySymbol", "Currency Symbol", PropType.String, "Kč", cat: "Appearance"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmCheckbox", CatInputs, "Checkbox", "check-square", 140, 20,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Checkbox");
                var chk = el.Props.GetBool("checked");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, 16, 16, chk ? FillAccent : Fill, chk ? "#93c5fd" : Border, 3));
                if (chk) sb.Append(Icon("check", 8, 8, 10));
                sb.Append(Text(lbl, 22, 8, 11));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Checkbox", cat: "Content"),
                Prop("checked", "Checked", PropType.Bool, false, cat: "State"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmRadio", CatInputs, "Radio", "circle", 140, 20,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Option");
                var chk = el.Props.GetBool("checked");
                var sb = new StringBuilder();
                sb.Append($"<circle cx='8' cy='8' r='7' fill='{Fill}' stroke='{(chk ? "#93c5fd" : Border)}' stroke-width='1.5'></circle>");
                if (chk) sb.Append($"<circle cx='8' cy='8' r='3.5' fill='{Accent}'></circle>");
                sb.Append(Text(lbl, 22, 8, 11));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Option", cat: "Content"),
                Prop("checked", "Checked", PropType.Bool, false, cat: "State"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmRadioGroup", CatInputs, "Radio Group", "list", 200, 80,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Options");
                var opts = el.Props.GetStringList("options").Take(4).ToArray();
                if (opts.Length == 0) opts = ["Option 1", "Option 2", "Option 3"];
                var sb = new StringBuilder();
                sb.Append(FieldLabel(lbl));
                for (var i = 0; i < opts.Length; i++)
                {
                    var y = i * 18.0;
                    sb.Append($"<circle cx='8' cy='{F(y + 8)}' r='7' fill='{Fill}' stroke='{Border}' stroke-width='1.5'></circle>");
                    if (i == 0) sb.Append($"<circle cx='8' cy='{F(y + 8)}' r='3.5' fill='{Accent}'></circle>");
                    sb.Append(Text(opts[i], 22, y + 8, 11));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Options", cat: "Content"),
                Prop("options", "Options", PropType.StringList, cat: "Content"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmToggle", CatInputs, "Toggle", "toggle-right", 100, 20,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Toggle");
                var chk = el.Props.GetBool("checked");
                var trackFill = chk ? "#93c5fd" : FillDark;
                var sb = new StringBuilder();
                sb.Append(Pill(0, 2, 36, 16, trackFill, "none"));
                var cx = chk ? 28.0 : 10.0;
                sb.Append($"<circle cx='{F(cx)}' cy='10' r='7' fill='white' stroke='{Border}' stroke-width='1'></circle>");
                sb.Append(Text(lbl, 42, 9, 11));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Toggle", cat: "Content"),
                Prop("checked", "Checked", PropType.Bool, false, cat: "State"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmToggleSection", CatInputs, "Toggle Section", "chevron-down", 240, 40,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Section");
                var expanded = el.Props.GetBool("expanded", true);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, 36, FillDark, Border, 4));
                var trackFill = expanded ? "#93c5fd" : FillDark;
                sb.Append(Pill(8, 10, 30, 16, trackFill, "none"));
                sb.Append($"<circle cx='{F(expanded ? 30.0 : 18.0)}' cy='18' r='7' fill='white' stroke='{Border}' stroke-width='1'></circle>");
                sb.Append(Text(lbl, 46, 18, 11, ColorText, "start", "500"));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Section", cat: "Content"),
                Prop("expanded", "Expanded", PropType.Bool, true, cat: "State"),
            ]);

        yield return Def("TmSelect", CatInputs, "Select", "chevron-down", 200, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var ph = el.Props.GetString("placeholder", "Select option...");
                var req = el.Props.GetBool("required");
                Svg(b, InputField(el.W, 36, lbl, ph, req, hasChevron: true));
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "Select option...", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmMultiSelect", CatInputs, "Multi Select", "chevrons-down", 240, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                var h = 36.0;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, el.W, h));
                sb.Append(Pill(6, 8, 50, 20, FillAccent, "#93c5fd"));
                sb.Append(Text("Item 1", 11, 18, 9, Accent));
                sb.Append(Icon("x", 50, 18, 8));
                sb.Append(ChevronDown(el.W - 16, h / 2 - 4));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "Select items...", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmCascadingSelect", CatInputs, "Cascading Select", "git-branch", 300, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var levels = el.Props.GetInt("levels", 2);
                var gap = 8.0;
                var selW = (el.W - gap * (levels - 1)) / levels;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                for (var i = 0; i < levels; i++)
                {
                    var x = i * (selW + gap);
                    sb.Append(Rect(x, 0, selW, 36));
                    sb.Append(ChevronDown(x + selW - 16, 14));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("levels", "Levels", PropType.Int, 2, cat: "Appearance"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmFilterableDropdown", CatInputs, "Filterable Dropdown", "filter", 200, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                Svg(b, InputField(el.W, 36, lbl, "Filter & select...", hasIcon: true, hasChevron: true));
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "Filter...", cat: "Content"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmEntityPicker", CatInputs, "Entity Picker", "users", 240, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Select entity");
                var h = 36.0;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, el.W, h));
                sb.Append(Icon("user", h / 2, h / 2, h * 0.6));
                sb.Append(Text("Choose...", h * 0.9, h / 2, 10, ColorLight));
                sb.Append(ChevronDown(el.W - 16, h / 2 - 4));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Select entity", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "Choose...", cat: "Content"),
                Prop("multiple", "Multiple", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmExpressionEditor", CatInputs, "Expression Editor", "code", 280, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Expression");
                var h = 36.0;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, el.W, h, FillDark, Border));
                sb.Append(Text("{expression}", 8, h / 2, 10, ColorMuted));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Expression", cat: "Content"),
                Prop("placeholder", "Placeholder", PropType.String, "{expression}", cat: "Content"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PICKERS
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatPickers = "Pickers";

    private static IEnumerable<WireframeComponentDef> Pickers()
    {
        static WireframeComponentDef DateLike(string type, string display, string icon, string defaultLabel)
            => Def(type, CatPickers, display, icon, 200, 56,
                (el, b) =>
                {
                    var lbl = el.Props.GetString("label", defaultLabel);
                    var req = el.Props.GetBool("required");
                    var h = 36.0;
                    var sb = new StringBuilder();
                    if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                    sb.Append(Rect(0, 0, el.W, h));
                    sb.Append(Text("dd.mm.yyyy", 8, h / 2, 10, ColorLight));
                    sb.Append(Icon(icon, el.W - 18, h / 2, h * 0.5));
                    Svg(b, sb.ToString());
                },
                [
                    Prop("label", "Label", PropType.String, defaultLabel, cat: "Content"),
                    Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                    Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
                ]);

        yield return DateLike("TmDatePicker", "Date Picker", "calendar", "Date");
        yield return DateLike("TmDateTimePicker", "Date & Time Picker", "calendar", "Date & Time");

        yield return Def("TmTimePicker", CatPickers, "Time Picker", "clock", 160, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Time");
                var h = 36.0;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, el.W, h));
                sb.Append(Text("HH:MM", 8, h / 2, 10, ColorLight));
                sb.Append(Icon("clock", el.W - 18, h / 2, h * 0.5));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Time", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmDateRangePicker", CatPickers, "Date Range Picker", "calendar", 320, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Date range");
                var h = 36.0; var w = el.W;
                var halfW = (w - 24) / 2;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, halfW, h));
                sb.Append(Text("From", 8, h / 2, 10, ColorLight));
                sb.Append(Text("→", halfW + 4, h / 2, 12, ColorMuted, "start"));
                sb.Append(Rect(halfW + 16, 0, halfW, h));
                sb.Append(Text("To", halfW + 24, h / 2, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Date range", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmTimeRangePicker", CatPickers, "Time Range Picker", "clock", 280, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Time range");
                var h = 36.0; var halfW = (el.W - 24) / 2;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, halfW, h)); sb.Append(Text("HH:MM", 8, h / 2, 10, ColorLight));
                sb.Append(Text("–", halfW + 4, h / 2, 12, ColorMuted));
                sb.Append(Rect(halfW + 16, 0, halfW, h)); sb.Append(Text("HH:MM", halfW + 24, h / 2, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Time range", cat: "Content"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmDateTimeRangePicker", CatPickers, "DateTime Range Picker", "calendar", 400, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "DateTime range");
                var h = 36.0; var halfW = (el.W - 24) / 2;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, halfW, h)); sb.Append(Text("From date & time", 8, h / 2, 10, ColorLight));
                sb.Append(Text("→", halfW + 4, h / 2, 12, ColorMuted));
                sb.Append(Rect(halfW + 16, 0, halfW, h)); sb.Append(Text("To date & time", halfW + 24, h / 2, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "DateTime range", cat: "Content"),
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmTimeInput", CatPickers, "Time Input", "clock", 120, 36,
            (el, b) =>
            {
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H));
                sb.Append(Text("HH", 12, el.H / 2, 12, ColorText, "middle"));
                sb.Append(Text(":", el.W / 2, el.H / 2, 12, ColorMuted, "middle"));
                sb.Append(Text("MM", el.W - 12, el.H / 2, 12, ColorText, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DATA DISPLAY
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatDataDisplay = "Data Display";

    private static IEnumerable<WireframeComponentDef> DataDisplay()
    {
        yield return Def("TmCard", CatDataDisplay, "Card", "square", 280, 180,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Card Title");
                var showHeader = el.Props.GetBool("showHeader", true);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8));
                if (showHeader)
                {
                    sb.Append(Rect(0, 0, el.W, 40, FillDark, "none", 8));
                    sb.Append(Rect(0, 32, el.W, 8, FillDark, "none", 0));
                    sb.Append(HLine(0, el.W, 40));
                    sb.Append(Text(title, 12, 20, 12, ColorText, "start", "500"));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Card Title", cat: "Content"),
                Prop("showHeader", "Show Header", PropType.Bool, true, cat: "Appearance"),
                Prop("showFooter", "Show Footer", PropType.Bool, false, cat: "Appearance"),
            ]);

        yield return Def("TmStatCard", CatDataDisplay, "Stat Card", "trending-up", 160, 100,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Total Revenue");
                var value = el.Props.GetString("value", "12 450");
                var unit = el.Props.GetString("unit", "Kč");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8));
                sb.Append(Text(title, 12, 22, 10, ColorMuted));
                sb.Append(Text($"{value} {unit}", 12, 55, 18, ColorText, "start", "600"));
                sb.Append(Text("↑ 12%", 12, 78, 10, "#22c55e"));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Total Revenue", cat: "Content"),
                Prop("value", "Value", PropType.String, "12 450", cat: "Content"),
                Prop("unit", "Unit", PropType.String, "Kč", cat: "Content"),
                Prop("trend", "Trend", PropType.Enum, "up", opts: ["up","down","neutral"], cat: "Content"),
            ]);

        yield return Def("TmBadge", CatDataDisplay, "Badge", "tag", 60, 22,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Badge");
                var variant = el.Props.GetString("variant", "default");
                var font = el.Props.GetString("size", "md") switch { "sm" => 9.0, "lg" => 12.0, _ => 10.5 };
                var fill = variant switch
                {
                    "primary" => FillAccent,
                    "success" => "#dcfce7",
                    "danger" => "#fee2e2",
                    "warning" => "#fef9c3",
                    "info" => FillAccent,
                    _ => FillDark
                };
                var border = variant switch
                {
                    "primary" or "info" => "#93c5fd",
                    "success" => "#86efac",
                    "danger" => "#fca5a5",
                    "warning" => "#fde047",
                    _ => Border
                };
                var sb = new StringBuilder();
                sb.Append(Pill(0, 0, el.W, el.H, fill, border));
                sb.Append(TextCentred(lbl, el.W, el.H, font));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Badge", cat: "Content"),
                Prop("variant", "Variant", PropType.Enum, "default",
                    opts: ["default","primary","success","danger","warning","info"], cat: "Appearance"),
                Prop("size", "Size", PropType.Enum, "md", opts: ["sm","md","lg"], cat: "Appearance"),
            ],
            sizePresets: BadgeSizes);

        yield return Def("TmChip", CatDataDisplay, "Chip", "tag", 80, 24,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Chip");
                var removable = el.Props.GetBool("removable", true);
                var sb = new StringBuilder();
                sb.Append(Pill(0, 0, el.W, el.H, FillDark, Border));
                sb.Append(Text(lbl, 10, el.H / 2, 10));
                if (removable) sb.Append(Icon("x", el.W - 12, el.H / 2, 8));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Chip", cat: "Content"),
                Prop("removable", "Removable", PropType.Bool, true, cat: "Behavior"),
                Prop("variant", "Variant", PropType.Enum, "default",
                    opts: ["default","primary","success","danger","warning"], cat: "Appearance"),
            ]);

        yield return Def("TmChipGroup", CatDataDisplay, "Chip Group", "tag", 240, 32,
            (el, b) =>
            {
                var chips = el.Props.GetStringList("chips");
                if (chips.Length == 0) chips = ["Chip 1", "Chip 2", "Chip 3"];
                var sb = new StringBuilder();
                var x = 0.0;
                foreach (var chip in chips.Take(5))
                {
                    var cw = chip.Length * 6.5 + 20;
                    sb.Append(Pill(x, 4, cw, 24, FillDark, Border));
                    sb.Append(Text(chip, x + 8, 16, 10));
                    x += cw + 6;
                    if (x > el.W - 20) break;
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("chips", "Chips", PropType.StringList, cat: "Content"),
            ]);

        yield return Def("TmAccordion", CatDataDisplay, "Accordion", "chevrons-down", 280, 120,
            (el, b) =>
            {
                var items = el.Props.GetStringList("items");
                if (items.Length == 0) items = ["Section 1", "Section 2", "Section 3"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                var rowH = el.H / items.Length;
                for (var i = 0; i < items.Length; i++)
                {
                    if (i > 0) sb.Append(HLine(0, el.W, i * rowH));
                    sb.Append(Text(items[i], 12, i * rowH + rowH / 2, 11));
                    sb.Append(ChevronDown(el.W - 18, i * rowH + rowH / 2 - 4));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("items", "Items", PropType.StringList, cat: "Content"),
                Prop("multiple", "Allow Multiple", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmEmptyState", CatDataDisplay, "Empty State", "inbox", 280, 160,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "No data");
                var desc = el.Props.GetString("description", "There is nothing to display here.");
                var action = el.Props.GetString("actionLabel", "Add item");
                var sb = new StringBuilder();
                sb.Append(DashedRect(el.W, el.H, 8));
                sb.Append(Icon("inbox", el.W / 2, el.H / 2 - 28, 32));
                sb.Append(Text(title, el.W / 2, el.H / 2 + 8, 13, ColorText, "middle", "600"));
                sb.Append(Text(desc, el.W / 2, el.H / 2 + 26, 10, ColorMuted, "middle"));
                sb.Append(Rect(el.W / 2 - 40, el.H / 2 + 42, 80, 28, FillAccent, "#93c5fd", 4));
                sb.Append(Text(action, el.W / 2, el.H / 2 + 56, 10, Accent, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "No data", cat: "Content"),
                Prop("description", "Description", PropType.String, "There is nothing to display here.", cat: "Content"),
                Prop("actionLabel", "Action Label", PropType.String, "Add item", cat: "Content"),
            ]);

        yield return Def("TmKanbanBoard", CatDataDisplay, "Kanban Board", "columns", 480, 320,
            (el, b) =>
            {
                var cols = el.Props.GetStringList("columns");
                if (cols.Length == 0) cols = ["To Do", "In Progress", "Done"];
                var colW = (el.W - (cols.Length - 1) * 8.0) / cols.Length;
                var sb = new StringBuilder();
                for (var i = 0; i < cols.Length; i++)
                {
                    var x = i * (colW + 8);
                    sb.Append(Rect(x, 0, colW, el.H, FillDark, Border, 6));
                    sb.Append(Rect(x + 4, 4, colW - 8, 24, FillDark, "none", 0));
                    sb.Append(Text(cols[i], x + colW / 2, 16, 11, ColorText, "middle", "500"));
                    // 2 placeholder cards
                    for (var c = 0; c < 2; c++)
                    {
                        var cy = 36 + c * 60.0;
                        sb.Append(Rect(x + 4, cy, colW - 8, 52, Fill, Border, 4));
                        sb.Append(HLine(x + 4, x + colW - 4, cy + 16));
                        sb.Append(Text("Task", x + 12, cy + 8, 10, ColorMuted));
                    }
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("columns", "Columns", PropType.StringList, cat: "Content"),
                Prop("showAddCard", "Show Add Card", PropType.Bool, true, cat: "Appearance"),
            ]);

        yield return Def("TmMultiViewList", CatDataDisplay, "Multi View List", "list", 360, 280,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Items");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(HLine(0, el.W, 40));
                sb.Append(Text(title, 12, 20, 12, ColorText, "start", "500"));
                // View toggle buttons
                sb.Append(Rect(el.W - 62, 10, 24, 22, FillAccent, "#93c5fd", 3));
                sb.Append(Rect(el.W - 36, 10, 24, 22, FillDark, Border, 3));
                // Rows
                for (var r = 0; r < 4; r++)
                {
                    var ry = 48 + r * 52.0;
                    sb.Append(HLine(0, el.W, ry + 52));
                    sb.Append(Rect(8, ry + 8, 36, 36, FillDark, Border, 4));
                    sb.Append(Rect(52, ry + 10, el.W * 0.4, 10, FillDark, "none", 2));
                    sb.Append(Rect(52, ry + 26, el.W * 0.3, 8, FillDark, "none", 2));
                    if (ry + 52 > el.H - 10) break;
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Items", cat: "Content"),
                Prop("showSearch", "Show Search", PropType.Bool, true, cat: "Appearance"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DATA TABLE
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatDataTable = "Data Table";

    private static IEnumerable<WireframeComponentDef> DataTable()
    {
        yield return Def("TmDataTable", CatDataTable, "Data Table", "table", 600, 320,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "");
                var cols = el.Props.GetStringList("columns");
                if (cols.Length == 0) cols = ["Column 1", "Column 2", "Column 3", "Column 4"];
                var rows = el.Props.GetInt("rows", 5);
                var showSearch = el.Props.GetBool("showSearch", true);
                var showPagination = el.Props.GetBool("showPagination", true);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));

                var top = 0.0;
                if (!string.IsNullOrEmpty(title) || showSearch)
                {
                    sb.Append(Rect(0, 0, el.W, 40, FillDark, "none", 4));
                    sb.Append(HLine(0, el.W, 40));
                    if (!string.IsNullOrEmpty(title))
                        sb.Append(Text(title, 12, 20, 12, ColorText, "start", "500"));
                    if (showSearch)
                        sb.Append(InputField(160, 26, "", "Search...", hasIcon: true));
                    top = 40;
                }

                var paginH = showPagination ? 36.0 : 0;
                var tableH = el.H - top - paginH;
                var colW = el.W / cols.Length;
                var rowH = tableH / (rows + 1);

                // Header
                sb.Append(Rect(0, top, el.W, rowH, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, top + rowH));
                for (var c = 0; c < cols.Length; c++)
                    sb.Append(Text(cols[c], c * colW + 8, top + rowH / 2, 10, ColorMuted, "start", "500"));

                // Rows
                for (var r = 0; r < rows; r++)
                {
                    var ry = top + rowH * (r + 1);
                    sb.Append(HLine(0, el.W, ry + rowH));
                    for (var c = 0; c < cols.Length; c++)
                    {
                        if (c > 0) sb.Append(VLine(c * colW, ry, ry + rowH));
                        sb.Append(Rect(c * colW + 6, ry + rowH / 2 - 4, colW * 0.65, 8, FillDark, "none", 2));
                    }
                }

                // Pagination
                if (showPagination)
                {
                    sb.Append(HLine(0, el.W, el.H - paginH));
                    sb.Append(Text("← 1  2  3  4  5  →", el.W / 2, el.H - paginH / 2, 10, ColorMuted, "middle"));
                }

                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "", cat: "Content"),
                Prop("columns", "Columns", PropType.StringList, cat: "Content"),
                Prop("rows", "Rows", PropType.Int, 5, cat: "Appearance"),
                Prop("showSearch", "Show Search", PropType.Bool, true, cat: "Appearance"),
                Prop("showPagination", "Show Pagination", PropType.Bool, true, cat: "Appearance"),
                Prop("showBulkActions", "Show Bulk Actions", PropType.Bool, false, cat: "Appearance"),
            ]);

        yield return Def("TmPagination", CatDataTable, "Pagination", "more-horizontal", 240, 36,
            (el, b) =>
            {
                var total = el.Props.GetInt("totalPages", 5);
                var current = el.Props.GetInt("currentPage", 1);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
                sb.Append(TextCentred($"← {Enumerable.Range(1, Math.Min(total, 5)).Select(p => p == current ? $"[{p}]" : p.ToString()).Aggregate((a, x) => a + "  " + x)}  →", el.W, el.H, 10, ColorMuted));
                Svg(b, sb.ToString());
            },
            [
                Prop("totalPages", "Total Pages", PropType.Int, 5, cat: "Content"),
                Prop("currentPage", "Current Page", PropType.Int, 1, cat: "State"),
            ]);

        yield return Def("TmBulkActionBar", CatDataTable, "Bulk Action Bar", "check-square", 400, 44,
            (el, b) =>
            {
                var count = el.Props.GetInt("selectedCount", 3);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillAccent, "#93c5fd", 4));
                sb.Append(Text($"{count} selected", 12, el.H / 2, 11, Accent));
                sb.Append(Rect(el.W - 180, 8, 60, 28, Fill, Border, 4));
                sb.Append(Text("Delete", el.W - 150, el.H / 2, 10, ColorText, "middle"));
                sb.Append(Rect(el.W - 112, 8, 60, 28, Fill, Border, 4));
                sb.Append(Text("Export", el.W - 82, el.H / 2, 10, ColorText, "middle"));
                sb.Append(Rect(el.W - 44, 8, 36, 28, Fill, Border, 4));
                sb.Append(Icon("x", el.W - 26, el.H / 2, 12));
                Svg(b, sb.ToString());
            },
            [
                Prop("selectedCount", "Selected Count", PropType.Int, 3, cat: "State"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FEEDBACK
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatFeedback = "Feedback";

    private static IEnumerable<WireframeComponentDef> Feedback()
    {
        yield return Def("TmAlert", CatFeedback, "Alert", "alert-circle", 400, 56,
            (el, b) =>
            {
                var msg = el.Props.GetString("message", "This is an alert message.");
                var variant = el.Props.GetString("variant", "info");
                var fill = variant switch
                {
                    "success" => "#dcfce7", "warning" => "#fef9c3",
                    "danger" or "error" => "#fee2e2", _ => FillAccent
                };
                var border = variant switch
                {
                    "success" => "#86efac", "warning" => "#fde047",
                    "danger" or "error" => "#fca5a5", _ => "#93c5fd"
                };
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, fill, border, 6));
                sb.Append(Text("ⓘ", 14, el.H / 2, 14, BorderStrong, "middle"));
                sb.Append(Text(msg, 30, el.H / 2, 11));
                Svg(b, sb.ToString());
            },
            [
                Prop("message", "Message", PropType.String, "This is an alert message.", cat: "Content"),
                Prop("variant", "Variant", PropType.Enum, "info",
                    opts: ["info","success","warning","danger"], cat: "Appearance"),
                Prop("dismissible", "Dismissible", PropType.Bool, true, cat: "Behavior"),
            ]);

        yield return Def("TmModal", CatFeedback, "Modal", "layers", 480, 360,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Modal Title");
                var showFooter = el.Props.GetBool("showFooter", true);
                // Size prop affects inner dialog proportions (ratio of modal area used by the dialog)
                var dialogScale = el.Props.GetString("size", "medium") switch
                {
                    "small"      => 0.55,
                    "large"      => 0.88,
                    "xLarge"     => 0.96,
                    "fullscreen" => 1.00,
                    _            => 0.72   // medium
                };
                var sb = new StringBuilder();
                // Backdrop
                sb.Append($"<rect width='{F(el.W)}' height='{F(el.H)}' fill='rgba(0,0,0,0.08)'></rect>");
                // Dialog
                var dw = el.W * dialogScale;
                var dh = el.H * dialogScale;
                var dx = (el.W - dw) / 2;
                var dy = (el.H - dh) / 2;
                sb.Append(Rect(dx, dy, dw, dh, Fill, Border, 8, 1.5));
                sb.Append(HLine(dx, dx + dw, dy + 44));
                sb.Append(Text(title, dx + 16, dy + 22, 13, ColorText, "start", "600"));
                sb.Append(Icon("x", dx + dw - 20, dy + 22, 14));
                if (showFooter)
                {
                    sb.Append(HLine(dx, dx + dw, dy + dh - 52));
                    sb.Append(Rect(dx + dw - 90, dy + dh - 40, 78, 28, FillAccent, "#93c5fd", 4));
                    sb.Append(Text("Confirm", dx + dw - 51, dy + dh - 26, 10, Accent, "middle"));
                    sb.Append(Rect(dx + dw - 174, dy + dh - 40, 78, 28, Fill, Border, 4));
                    sb.Append(Text("Cancel", dx + dw - 135, dy + dh - 26, 10, ColorMuted, "middle"));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Modal Title", cat: "Content"),
                Prop("showFooter", "Show Footer", PropType.Bool, true, cat: "Appearance"),
                Prop("size", "Size", PropType.Enum, "medium",
                    opts: ["small","medium","large","xLarge","fullscreen"], cat: "Appearance"),
            ]);

        yield return Def("TmDialog", CatFeedback, "Dialog", "message-square", 320, 160,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Confirm action");
                var msg = el.Props.GetString("message", "Are you sure you want to proceed?");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8, 1.5));
                sb.Append(HLine(0, el.W, 40));
                sb.Append(Text(title, 14, 20, 12, ColorText, "start", "600"));
                sb.Append(Text(msg, 14, 72, 11, ColorMuted));
                sb.Append(HLine(0, el.W, el.H - 48));
                sb.Append(Rect(el.W - 86, el.H - 36, 74, 26, FillAccent, "#93c5fd", 4));
                sb.Append(Text("OK", el.W - 49, el.H - 23, 10, Accent, "middle"));
                sb.Append(Rect(el.W - 166, el.H - 36, 74, 26, Fill, Border, 4));
                sb.Append(Text("Cancel", el.W - 129, el.H - 23, 10, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Confirm action", cat: "Content"),
                Prop("message", "Message", PropType.String, "Are you sure?", cat: "Content"),
                Prop("variant", "Variant", PropType.Enum, "info",
                    opts: ["info","success","warning","error"], cat: "Appearance"),
            ]);

        yield return Def("TmTooltip", CatFeedback, "Tooltip", "info", 120, 36,
            (el, b) =>
            {
                var text = el.Props.GetString("text", "Tooltip text");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, "#1f2937", "#1f2937", 4));
                sb.Append(TextCentred(text, el.W, el.H, 10, "white"));
                // Arrow
                sb.Append($"<polygon points='{F(el.W / 2 - 5)},{F(el.H)} {F(el.W / 2 + 5)},{F(el.H)} {F(el.W / 2)},{F(el.H + 6)}' fill='#1f2937'></polygon>");
                Svg(b, sb.ToString());
            },
            [
                Prop("text", "Text", PropType.String, "Tooltip text", cat: "Content"),
                Prop("placement", "Placement", PropType.Enum, "top",
                    opts: ["top","bottom","left","right"], cat: "Appearance"),
            ]);

        yield return Def("TmPopover", CatFeedback, "Popover", "message-circle", 200, 120,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Popover");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6, 1.5));
                sb.Append(HLine(0, el.W, 32));
                sb.Append(Text(title, 10, 16, 11, ColorText, "start", "500"));
                sb.Append($"<polygon points='{F(el.W / 2 - 6)},0 {F(el.W / 2 + 6)},0 {F(el.W / 2)},{F(-7)}' fill='{Fill}' stroke='{Border}' stroke-width='1'></polygon>");
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Popover", cat: "Content"),
                Prop("placement", "Placement", PropType.Enum, "bottom",
                    opts: ["top","bottom","left","right"], cat: "Appearance"),
            ]);

        yield return Def("TmProgressBar", CatFeedback, "Progress Bar", "bar-chart-2", 240, 16,
            (el, b) =>
            {
                var value = el.Props.GetInt("value", 60);
                var max = el.Props.GetInt("max", 100);
                var pct = Math.Clamp(value / (double)max, 0, 1);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillDark, "none", el.H / 2));
                sb.Append(Pill(0, 0, el.W * pct, el.H, FillAccent, "none"));
                Svg(b, sb.ToString());
            },
            [
                Prop("value", "Value", PropType.Int, 60, cat: "State"),
                Prop("max", "Max", PropType.Int, 100, cat: "State"),
                Prop("showLabel", "Show Label", PropType.Bool, false, cat: "Appearance"),
            ]);

        yield return Def("TmSpinner", CatFeedback, "Spinner", "loader", 32, 32,
            (el, b) => Svg(b, Icon("spinner", el.W / 2, el.H / 2, Math.Min(el.W, el.H))),
            [
                Prop("size", "Size", PropType.Enum, "md", opts: ["sm","md","lg"], cat: "Appearance"),
            ],
            sizePresets: SpinnerSizes);

        yield return Def("TmSkeleton", CatFeedback, "Skeleton", "minus", 280, 80,
            (el, b) =>
            {
                var lines = el.Props.GetInt("lines", 3);
                var showAvatar = el.Props.GetBool("showAvatar");
                var sb = new StringBuilder();
                var startX = 0.0;
                if (showAvatar)
                {
                    sb.Append($"<circle cx='20' cy='20' r='18' fill='{FillDark}'></circle>");
                    startX = 48;
                }
                for (var i = 0; i < lines; i++)
                {
                    var lw = i == lines - 1 ? (el.W - startX) * 0.7 : el.W - startX;
                    sb.Append(Rect(startX, i * 20 + (i > 0 ? i * 4 : 0), lw, 12, FillDark, "none", 4));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("lines", "Lines", PropType.Int, 3, cat: "Appearance"),
                Prop("showAvatar", "Show Avatar", PropType.Bool, false, cat: "Appearance"),
            ]);

        yield return Def("TmAutoSaveIndicator", CatFeedback, "Auto Save Indicator", "save", 100, 24,
            (el, b) =>
            {
                var state = el.Props.GetString("state", "saved");
                var (text, fill, border) = state switch
                {
                    "saving" => ("Saving…", FillDark, Border),
                    "error" => ("Error", "#fee2e2", "#fca5a5"),
                    _ => ("Saved ✓", "#dcfce7", "#86efac")
                };
                var sb = new StringBuilder();
                sb.Append(Pill(0, 0, el.W, el.H, fill, border));
                sb.Append(TextCentred(text, el.W, el.H, 9, ColorText));
                Svg(b, sb.ToString());
            },
            [
                Prop("state", "State", PropType.Enum, "saved",
                    opts: ["saving","saved","error"], cat: "State"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatNavigation = "Navigation";

    private static IEnumerable<WireframeComponentDef> Navigation()
    {
        yield return Def("TmTabs", CatNavigation, "Tabs", "layout", 400, 40,
            (el, b) =>
            {
                var tabs = el.Props.GetStringList("tabs");
                if (tabs.Length == 0) tabs = ["Tab 1", "Tab 2", "Tab 3"];
                var active = el.Props.GetInt("activeTab", 0);
                var sb = new StringBuilder();
                sb.Append(HLine(0, el.W, el.H));
                var x = 0.0;
                for (var i = 0; i < tabs.Length; i++)
                {
                    var tw = tabs[i].Length * 7.0 + 24;
                    var isActive = i == active;
                    if (isActive)
                    {
                        sb.Append(Rect(x, 0, tw, el.H, FillAccent, "none", 0));
                        sb.Append(HLine(x, x + tw, el.H, Accent));
                    }
                    sb.Append(Text(tabs[i], x + tw / 2, el.H / 2, 11,
                        isActive ? Accent : ColorMuted, "middle", isActive ? "500" : "normal"));
                    x += tw;
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("tabs", "Tabs", PropType.StringList, cat: "Content"),
                Prop("activeTab", "Active Tab Index", PropType.Int, 0, cat: "State"),
                Prop("variant", "Variant", PropType.Enum, "line",
                    opts: ["line","pill","enclosed"], cat: "Appearance"),
            ]);

        yield return Def("TmBreadcrumbs", CatNavigation, "Breadcrumbs", "chevron-right", 300, 24,
            (el, b) =>
            {
                var items = el.Props.GetStringList("items");
                if (items.Length == 0) items = ["Home", "Section", "Current page"];
                var sb = new StringBuilder();
                var x = 0.0;
                for (var i = 0; i < items.Length; i++)
                {
                    var tw = items[i].Length * 6.5;
                    var isLast = i == items.Length - 1;
                    sb.Append(Text(items[i], x, el.H / 2, 11,
                        isLast ?  ColorText : Accent, "start", isLast ? "500" : "normal"));
                    x += tw;
                    if (!isLast) { sb.Append(Text("/", x + 4, el.H / 2, 10, ColorLight)); x += 14; }
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("items", "Items", PropType.StringList, cat: "Content"),
            ]);

        yield return Def("TmContextMenu", CatNavigation, "Context Menu", "menu", 160, 120,
            (el, b) =>
            {
                var items = el.Props.GetStringList("items");
                if (items.Length == 0) items = ["Edit", "Duplicate", "Delete"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6, 1.5));
                var rowH = el.H / items.Length;
                for (var i = 0; i < items.Length; i++)
                {
                    if (i > 0) sb.Append(HLine(0, el.W, i * rowH));
                    var isDanger = items[i].ToLower().Contains("delete") || items[i].ToLower().Contains("remove");
                    sb.Append(Text(items[i], 14, i * rowH + rowH / 2, 11,
                        isDanger ? "#ef4444" : ColorText));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("items", "Items", PropType.StringList, cat: "Content"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LAYOUT
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatLayout = "Layout";

    private static IEnumerable<WireframeComponentDef> Layout()
    {
        yield return Def("TmTopBar", CatLayout, "Top Bar", "layout", 800, 56,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "App Name");
                var showSearch = el.Props.GetBool("showSearch", true);
                var showNotifications = el.Props.GetBool("showNotifications", true);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillDark, Border, 0));
                sb.Append(Text(title, 16, el.H / 2, 14, ColorText, "start", "600"));
                if (showSearch)
                {
                    var sw = 180.0; var sx = el.W / 2 - sw / 2;
                    sb.Append(Rect(sx, 12, sw, 32, Fill, Border, 4));
                    sb.Append(Icon("search", sx + 16, el.H / 2, 14));
                    sb.Append(Text("Search…", sx + 26, el.H / 2, 10, ColorLight));
                }
                if (showNotifications)
                    sb.Append(Icon("bell", el.W - 40, el.H / 2, 18));
                sb.Append(Icon("user", el.W - 16, el.H / 2, 22));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "App Name", cat: "Content"),
                Prop("showSearch", "Show Search", PropType.Bool, true, cat: "Appearance"),
                Prop("showNotifications", "Show Notifications", PropType.Bool, true, cat: "Appearance"),
            ]);

        yield return Def("TmSidebar", CatLayout, "Sidebar", "sidebar", 220, 400,
            (el, b) =>
            {
                var items = el.Props.GetStringList("items");
                if (items.Length == 0) items = ["Dashboard", "Users", "Reports", "Settings"];
                var collapsed = el.Props.GetBool("collapsed");
                var w = collapsed ? 48.0 : el.W;
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, w, el.H, FillDark, Border, 0));
                for (var i = 0; i < items.Length; i++)
                {
                    var ry = i * 40.0 + 8;
                    if (i == 0)
                        sb.Append(Rect(4, ry, w - 8, 34, FillAccent, "none", 4));
                    sb.Append($"<circle cx='24' cy='{F(ry + 17)}' r='6' fill='{FillDark}' stroke='{Border}'></circle>");
                    if (!collapsed)
                        sb.Append(Text(items[i], 38, ry + 17, 11, i == 0 ? Accent : ColorText));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("items", "Items", PropType.StringList, cat: "Content"),
                Prop("collapsed", "Collapsed", PropType.Bool, false, cat: "State"),
                Prop("width", "Width", PropType.Int, 220, cat: "Appearance"),
            ]);

        yield return Def("TmDrawer", CatLayout, "Drawer", "sidebar", 360, 400,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Drawer");
                var placement = el.Props.GetString("placement", "right");
                var sb = new StringBuilder();
                // Overlay
                sb.Append($"<rect width='{F(el.W)}' height='{F(el.H)}' fill='rgba(0,0,0,0.06)'></rect>");
                // Panel (right side)
                var pw = el.W * 0.65;
                var px = placement == "left" ? 0.0 : el.W - pw;
                sb.Append(Rect(px, 0, pw, el.H, Fill, Border, 0, 1.5));
                sb.Append(HLine(px, px + pw, 44));
                sb.Append(Text(title, px + 16, 22, 13, ColorText, "start", "600"));
                sb.Append(Icon("x", px + pw - 20, 22, 14));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Drawer", cat: "Content"),
                Prop("placement", "Placement", PropType.Enum, "right",
                    opts: ["left","right"], cat: "Appearance"),
                Prop("width", "Width", PropType.Int, 400, cat: "Appearance"),
            ]);

        yield return Def("TmSection", CatLayout, "Section", "minus", 400, 160,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Section Title");
                var sb = new StringBuilder();
                sb.Append(Text(title, 0, 10, 14, ColorText, "start", "600"));
                sb.Append(HLine(0, el.W, 24));
                sb.Append(DashedRect(el.W, el.H - 32));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Section Title", cat: "Content"),
            ]);

        yield return Def("TmCommandPalette", CatLayout, "Command Palette", "command", 480, 320,
            (el, b) =>
            {
                var ph = el.Props.GetString("placeholder", "Type a command...");
                var sb = new StringBuilder();
                sb.Append($"<rect width='{F(el.W)}' height='{F(el.H)}' fill='rgba(0,0,0,0.08)'></rect>");
                var dw = el.W - 40; var dx = 20.0;
                sb.Append(Rect(dx, 20, dw, el.H - 40, Fill, Border, 8, 1.5));
                sb.Append(Rect(dx + 4, 24, dw - 8, 40, FillDark, Border, 4));
                sb.Append(Icon("search", dx + 22, 44, 16));
                sb.Append(Text(ph, dx + 38, 44, 11, ColorLight));
                sb.Append(HLine(dx, dx + dw, 68));
                for (var i = 0; i < 4; i++)
                {
                    var ry = 72 + i * 36.0;
                    if (i == 0) sb.Append(Rect(dx + 4, ry, dw - 8, 32, FillAccent, "none", 4));
                    sb.Append(Rect(dx + 12, ry + 10, 100, 10, FillDark, "none", 3));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("placeholder", "Placeholder", PropType.String, "Type a command...", cat: "Content"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FORMS
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatForms = "Forms";

    private static IEnumerable<WireframeComponentDef> Forms()
    {
        yield return Def("TmFormSection", CatForms, "Form Section", "layout", 500, 140,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Section");
                var desc = el.Props.GetString("description", "Section description text.");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Rect(0, 0, el.W, 48, FillDark, "none", 6));
                sb.Append(Rect(0, 40, el.W, 8, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, 48));
                sb.Append(Text(title, 14, 20, 12, ColorText, "start", "600"));
                sb.Append(Text(desc, 14, 36, 10, ColorMuted));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Section", cat: "Content"),
                Prop("description", "Description", PropType.String, "Section description.", cat: "Content"),
            ]);

        yield return Def("TmFormRow", CatForms, "Form Row", "minus", 500, 56,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Field Label");
                var req = el.Props.GetBool("required");
                var sb = new StringBuilder();
                sb.Append(Text(req ? lbl + " *" : lbl, 0, 10, 11, ColorText, "start", "500"));
                sb.Append(Rect(160, 0, el.W - 160, 36, Fill, Border, 4));
                sb.Append(Text("Value", 168, 18, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Field Label", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmFormField", CatForms, "Form Field", "edit-3", 280, 64,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                Svg(b, InputField(el.W, 36, lbl, "Enter value...", req));
            },
            [
                Prop("label", "Label", PropType.String, "Label", cat: "Content"),
                Prop("required", "Required", PropType.Bool, false, cat: "Behavior"),
            ]);

        yield return Def("TmInlineEdit", CatForms, "Inline Edit", "edit", 200, 28,
            (el, b) =>
            {
                var value = el.Props.GetString("value", "Click to edit");
                var sb = new StringBuilder();
                sb.Append(Text(value, 0, el.H / 2, 11));
                sb.Append(Icon("edit", el.W - 14, el.H / 2, 12));
                sb.Append(HLine(0, el.W - 20, el.H - 2, FillDark));
                Svg(b, sb.ToString());
            },
            [
                Prop("value", "Value", PropType.String, "Click to edit", cat: "Content"),
                Prop("editOnClick", "Edit on Click", PropType.Bool, true, cat: "Behavior"),
            ]);

        yield return Def("TmValidationSummary", CatForms, "Validation Summary", "alert-circle", 320, 100,
            (el, b) =>
            {
                var errors = el.Props.GetStringList("errors");
                if (errors.Length == 0) errors = ["Field is required.", "Invalid email format."];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, "#fee2e2", "#fca5a5", 6));
                for (var i = 0; i < errors.Length && i < 4; i++)
                    sb.Append(Text("• " + errors[i], 10, 16 + i * 18.0, 10, "#dc2626"));
                Svg(b, sb.ToString());
            },
            [
                Prop("errors", "Errors", PropType.StringList, cat: "Content"),
            ]);

        yield return Def("TmDynamicFormRenderer", CatForms, "Dynamic Form", "list", 400, 240,
            (el, b) =>
            {
                var count = el.Props.GetInt("fieldCount", 4);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                for (var i = 0; i < count && i < 6; i++)
                {
                    var fy = 12 + i * 44.0;
                    sb.Append(Rect(el.W * 0.3, fy, el.W * 0.7 - 8, 36, Fill, Border, 4));
                    sb.Append(Rect(8, fy + 8, el.W * 0.28, 10, FillDark, "none", 3));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("fieldCount", "Field Count", PropType.Int, 4, cat: "Appearance"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FILES
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatFiles = "Files";

    private static IEnumerable<WireframeComponentDef> Files()
    {
        yield return Def("TmFileDropZone", CatFiles, "File Drop Zone", "upload-cloud", 300, 160,
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Drop files here or click to upload");
                var sb = new StringBuilder();
                sb.Append(DashedRect(el.W, el.H, 8));
                sb.Append(Icon("upload", el.W / 2, el.H / 2 - 16, 32));
                sb.Append(Text(lbl, el.W / 2, el.H / 2 + 16, 10, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("label", "Label", PropType.String, "Drop files here or click to upload", cat: "Content"),
                Prop("accept", "Accept", PropType.String, "*/*", cat: "Behavior"),
                Prop("multiple", "Multiple", PropType.Bool, true, cat: "Behavior"),
            ]);

        yield return Def("TmAttachmentManager", CatFiles, "Attachment Manager", "paperclip", 360, 200,
            (el, b) =>
            {
                var maxFiles = el.Props.GetInt("maxFiles", 5);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(HLine(0, el.W, 40));
                sb.Append(Text("Attachments", 12, 20, 12, ColorText, "start", "500"));
                sb.Append(Rect(el.W - 80, 10, 68, 24, FillAccent, "#93c5fd", 4));
                sb.Append(Text("+ Upload", el.W - 46, 22, 10, Accent, "middle"));
                for (var i = 0; i < Math.Min(maxFiles, 3); i++)
                {
                    var fy = 48 + i * 44.0;
                    sb.Append(Rect(8, fy, 32, 32, FillDark, Border, 4));
                    sb.Append(Rect(48, fy + 6, el.W * 0.5, 8, FillDark, "none", 3));
                    sb.Append(Rect(48, fy + 20, el.W * 0.3, 6, FillDark, "none", 3));
                    sb.Append(Icon("x", el.W - 16, fy + 16, 10));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("maxFiles", "Max Files", PropType.Int, 5, cat: "Behavior"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHARTS
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatCharts = "Charts";

    private static IEnumerable<WireframeComponentDef> Charts()
    {
        yield return Def("TmChart", CatCharts, "Chart", "bar-chart-2", 400, 240,
            (el, b) =>
            {
                var type = el.Props.GetString("type", "bar");
                var title = el.Props.GetString("title", "Chart Title");
                var dataPoints = el.Props.GetInt("dataPoints", 6);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Text(title, el.W / 2, 16, 12, ColorText, "middle", "500"));
                var chartX = 32.0; var chartY = 28.0;
                var chartW = el.W - chartX - 8;
                var chartH = el.H - chartY - 24;

                if (type == "pie" || type == "donut")
                {
                    var cx = el.W / 2; var cy = chartY + chartH / 2;
                    var r = Math.Min(chartW, chartH) / 2 - 8;
                    var angles = new[] { 0.0, 72, 144, 216, 288, 360 };
                    var fills = new[] { FillAccent, FillDark, "#fef9c3", "#dcfce7", "#fee2e2" };
                    for (var i = 0; i < Math.Min(dataPoints, 5); i++)
                    {
                        var a1 = angles[i] * Math.PI / 180;
                        var a2 = angles[i + 1] * Math.PI / 180;
                        var x1 = cx + r * Math.Cos(a1); var y1 = cy + r * Math.Sin(a1);
                        var x2 = cx + r * Math.Cos(a2); var y2 = cy + r * Math.Sin(a2);
                        var large = (angles[i + 1] - angles[i]) > 180 ? 1 : 0;
                        sb.Append($"<path d='M{F(cx)},{F(cy)} L{F(x1)},{F(y1)} A{F(r)},{F(r)} 0 {large} 1 {F(x2)},{F(y2)} Z' fill='{fills[i % fills.Length]}' stroke='white' stroke-width='1'></path>");
                    }
                    if (type == "donut")
                        sb.Append($"<circle cx='{F(cx)}' cy='{F(cy)}' r='{F(r * 0.5)}' fill='white'></circle>");
                }
                else if (type == "line")
                {
                    // Axes
                    sb.Append(VLine(chartX, chartY, chartY + chartH));
                    sb.Append(HLine(chartX, chartX + chartW, chartY + chartH));
                    var heights = new[] { 0.6, 0.8, 0.4, 0.9, 0.5, 0.7, 0.85 };
                    var pts = new List<string>();
                    for (var i = 0; i < dataPoints; i++)
                    {
                        var px = chartX + (i + 0.5) * chartW / dataPoints;
                        var py = chartY + chartH - heights[i % heights.Length] * chartH;
                        pts.Add($"{F(px)},{F(py)}");
                    }
                    sb.Append($"<polyline points='{string.Join(" ", pts)}' fill='none' stroke='{Accent}' stroke-width='2'></polyline>");
                }
                else // bar
                {
                    sb.Append(VLine(chartX, chartY, chartY + chartH));
                    sb.Append(HLine(chartX, chartX + chartW, chartY + chartH));
                    var heights = new[] { 0.6, 0.8, 0.4, 0.9, 0.5, 0.7 };
                    var barW = chartW / dataPoints * 0.65;
                    var gap = chartW / dataPoints;
                    for (var i = 0; i < dataPoints; i++)
                    {
                        var bh = heights[i % heights.Length] * chartH;
                        var bx = chartX + i * gap + gap * 0.175;
                        sb.Append(Rect(bx, chartY + chartH - bh, barW, bh, FillAccent, "#93c5fd", 2));
                    }
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Chart Title", cat: "Content"),
                Prop("type", "Type", PropType.Enum, "bar",
                    opts: ["bar","line","pie","donut"], cat: "Appearance"),
                Prop("dataPoints", "Data Points", PropType.Int, 6, cat: "Appearance"),
            ]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // COMPLEX
    // ══════════════════════════════════════════════════════════════════════════
    private const string CatComplex = "Complex";

    private static IEnumerable<WireframeComponentDef> Complex()
    {
        yield return Def("TmTimeline", CatComplex, "Timeline", "git-commit", 300, 240,
            (el, b) =>
            {
                var items = el.Props.GetStringList("items");
                if (items.Length == 0) items = ["Event 1", "Event 2", "Event 3", "Event 4"];
                var sb = new StringBuilder();
                var rowH = el.H / items.Length;
                sb.Append(VLine(20, 0, el.H, FillDark));
                for (var i = 0; i < items.Length; i++)
                {
                    var cy = i * rowH + rowH / 2;
                    sb.Append($"<circle cx='20' cy='{F(cy)}' r='6' fill='{(i == 0 ? FillAccent : FillDark)}' stroke='{(i == 0 ? "#93c5fd" : Border)}' stroke-width='1.5'></circle>");
                    sb.Append(Text(items[i], 36, cy, 11));
                    sb.Append(Text($"Day {i + 1}", el.W - 10, cy, 9, ColorLight, "end"));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("items", "Items", PropType.StringList, cat: "Content"),
            ]);

        yield return Def("TmStepper", CatComplex, "Stepper", "list", 500, 56,
            (el, b) =>
            {
                var steps = el.Props.GetStringList("steps");
                if (steps.Length == 0) steps = ["Step 1", "Step 2", "Step 3", "Step 4"];
                var activeStep = el.Props.GetInt("activeStep", 1);
                var stepW = el.W / steps.Length;
                var sb = new StringBuilder();
                for (var i = 0; i < steps.Length; i++)
                {
                    var cx = i * stepW + stepW / 2;
                    var isDone = i < activeStep;
                    var isActive = i == activeStep;
                    var fill = isDone ? FillAccent : isActive ? Fill : FillDark;
                    var border2 = isDone || isActive ? "#93c5fd" : Border;
                    sb.Append($"<circle cx='{F(cx)}' cy='20' r='12' fill='{fill}' stroke='{border2}' stroke-width='{(isActive ? "2" : "1.5")}'></circle>");
                    sb.Append(Text(isDone ? "✓" : (i + 1).ToString(), cx, 20, 10,
                        isDone ? Accent : isActive ?  ColorText : ColorLight, "middle"));
                    sb.Append(Text(steps[i], cx, 42, 9, isActive ?  ColorText : ColorMuted, "middle"));
                    if (i < steps.Length - 1)
                        sb.Append(HLine(cx + 14, (i + 1) * stepW + stepW / 2 - 14, 20, isDone ? "#93c5fd" : Border));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("steps", "Steps", PropType.StringList, cat: "Content"),
                Prop("activeStep", "Active Step Index", PropType.Int, 1, cat: "State"),
            ]);

        yield return Def("TmScheduler", CatComplex, "Scheduler", "calendar", 700, 400,
            (el, b) =>
            {
                var view = el.Props.GetString("view", "week");
                var title = el.Props.GetString("title", "Schedule");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Rect(0, 0, el.W, 44, FillDark, "none", 6));
                sb.Append(Rect(0, 36, el.W, 8, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, 44));
                sb.Append(Text(title, 12, 22, 13, ColorText, "start", "500"));
                // View buttons
                foreach (var (vw, vx) in new[] { ("Day", el.W - 180), ("Week", el.W - 136), ("Month", el.W - 86) })
                {
                    var isActive = view.ToLower() == vw.ToLower();
                    sb.Append(Rect(vx, 10, 40, 24, isActive ? FillAccent : Fill, isActive ? "#93c5fd" : Border, 3));
                    sb.Append(Text(vw, vx + 20, 22, 9, isActive ? Accent : ColorMuted, "middle"));
                }
                // Time column + hour slots
                var cols = view == "month" ? 7 : 7;
                var colW2 = (el.W - 40) / cols;
                for (var c = 0; c <= cols; c++)
                    sb.Append(VLine(40 + c * colW2, 44, el.H));
                var rowH2 = (el.H - 68) / 8;
                for (var r = 0; r <= 8; r++)
                    sb.Append(HLine(0, el.W, 68 + r * rowH2));
                // Sample event
                sb.Append(Rect(40 + colW2 + 2, 68 + rowH2, colW2 * 2 - 4, rowH2 * 2, FillAccent, "#93c5fd", 3));
                sb.Append(Text("Meeting", 40 + colW2 * 2, 68 + rowH2 * 2, 9, Accent, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Schedule", cat: "Content"),
                Prop("view", "View", PropType.Enum, "week",
                    opts: ["day","week","month","agenda"], cat: "Appearance"),
            ]);

        yield return Def("TmDashboard", CatComplex, "Dashboard", "grid", 800, 500,
            (el, b) =>
            {
                var cols = el.Props.GetInt("columns", 3);
                var rows = el.Props.GetInt("rows", 2);
                var gap = 8.0;
                var cellW = (el.W - gap * (cols + 1)) / cols;
                var cellH = (el.H - gap * (rows + 1)) / rows;
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillDark, Border, 6));
                for (var r = 0; r < rows; r++)
                    for (var c = 0; c < cols; c++)
                    {
                        var wx = gap + c * (cellW + gap);
                        var wy = gap + r * (cellH + gap);
                        sb.Append(Rect(wx, wy, cellW, cellH, Fill, Border, 6));
                        sb.Append(Text($"Widget {r * cols + c + 1}", wx + cellW / 2, wy + 16, 10, ColorMuted, "middle"));
                    }
                Svg(b, sb.ToString());
            },
            [
                Prop("columns", "Columns", PropType.Int, 3, cat: "Appearance"),
                Prop("rows", "Rows", PropType.Int, 2, cat: "Appearance"),
            ]);

        yield return Def("TmMarkdownEditor", CatComplex, "Markdown Editor", "edit", 500, 300,
            (el, b) =>
            {
                var ph = el.Props.GetString("placeholder", "Write markdown...");
                var showToolbar = el.Props.GetBool("showToolbar", true);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                var contentY = 0.0;
                if (showToolbar)
                {
                    sb.Append(Rect(0, 0, el.W, 36, FillDark, "none", 6));
                    sb.Append(Rect(0, 28, el.W, 8, FillDark, "none", 0));
                    sb.Append(HLine(0, el.W, 36));
                    foreach (var (tool, tx) in new[] { ("B", 10.0), ("I", 28.0), ("H", 46.0), ("|", 64.0), ("🔗", 72.0), ("📷", 90.0) })
                    {
                        if (tool == "|") sb.Append(VLine(tx, 6, 30));
                        else sb.Append(Text(tool, tx, 18, 11, ColorMuted, "middle", "500"));
                    }
                    contentY = 36;
                }
                sb.Append(Text(ph, 10, contentY + 16, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("placeholder", "Placeholder", PropType.String, "Write markdown...", cat: "Content"),
                Prop("showToolbar", "Show Toolbar", PropType.Bool, true, cat: "Appearance"),
            ]);

        yield return Def("TmRichEditorFull", CatComplex, "Rich Text Editor (Full)", "edit-3", 600, 320,
            (el, b) =>
            {
                var ph = el.Props.GetString("placeholder", "Start writing...");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Rect(0, 0, el.W, 40, FillDark, "none", 6));
                sb.Append(Rect(0, 32, el.W, 8, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, 40));
                var tools = new[] { "B", "I", "U", "|", "H1", "H2", "|", "≡", "⋮", "|", "🔗", "📷", "📋" };
                var tx2 = 8.0;
                foreach (var tool in tools)
                {
                    if (tool == "|") { sb.Append(VLine(tx2, 6, 34)); tx2 += 8; }
                    else { sb.Append(Text(tool, tx2 + 6, 20, 10, ColorMuted, "middle", "500")); tx2 += tool.Length * 7 + 4; }
                }
                sb.Append(Text(ph, 10, 58, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("placeholder", "Placeholder", PropType.String, "Start writing...", cat: "Content"),
            ]);

        yield return Def("TmRichEditorSimple", CatComplex, "Rich Text Editor (Simple)", "edit-2", 400, 200,
            (el, b) =>
            {
                var ph = el.Props.GetString("placeholder", "Type here...");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Rect(0, 0, el.W, 32, FillDark, "none", 6));
                sb.Append(Rect(0, 24, el.W, 8, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, 32));
                var tx2 = 8.0;
                foreach (var tool in new[] { "B", "I", "U", "|", "🔗" })
                {
                    if (tool == "|") { sb.Append(VLine(tx2, 4, 28)); tx2 += 8; }
                    else { sb.Append(Text(tool, tx2 + 6, 16, 10, ColorMuted, "middle", "500")); tx2 += 16; }
                }
                sb.Append(Text(ph, 10, 48, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("placeholder", "Placeholder", PropType.String, "Type here...", cat: "Content"),
            ]);

        yield return Def("TmImageGallery", CatComplex, "Image Gallery", "image", 400, 280,
            (el, b) =>
            {
                var cols = el.Props.GetInt("columns", 3);
                var count = el.Props.GetInt("itemCount", 6);
                var gap = 6.0;
                var cellW = (el.W - gap * (cols + 1)) / cols;
                var cellH = cellW * 0.75;
                var sb = new StringBuilder();
                for (var i = 0; i < count; i++)
                {
                    var c = i % cols; var r = i / cols;
                    var ix = gap + c * (cellW + gap);
                    var iy = gap + r * (cellH + gap);
                    if (iy + cellH > el.H) break;
                    sb.Append(Rect(ix, iy, cellW, cellH, FillDark, Border, 4));
                    sb.Append(Icon("image", ix + cellW / 2, iy + cellH / 2, Math.Min(cellW, cellH) * 0.45));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("columns", "Columns", PropType.Int, 3, cat: "Appearance"),
                Prop("itemCount", "Item Count", PropType.Int, 6, cat: "Appearance"),
            ]);

        yield return Def("TmImportWizard", CatComplex, "Import Wizard", "upload", 560, 360,
            (el, b) =>
            {
                var steps = el.Props.GetStringList("steps");
                if (steps.Length == 0) steps = ["Upload", "Map Fields", "Preview", "Import"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8, 1.5));
                // Stepper header
                sb.Append(Rect(0, 0, el.W, 52, FillDark, "none", 8));
                sb.Append(Rect(0, 44, el.W, 8, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, 52));
                var stepW = el.W / steps.Length;
                for (var i = 0; i < steps.Length; i++)
                {
                    var cx = i * stepW + stepW / 2;
                    sb.Append($"<circle cx='{F(cx)}' cy='20' r='10' fill='{(i == 0 ? FillAccent : FillDark)}' stroke='{(i == 0 ? "#93c5fd" : Border)}' stroke-width='1.5'></circle>");
                    sb.Append(Text((i + 1).ToString(), cx, 20, 9, i == 0 ? Accent : ColorMuted, "middle"));
                    sb.Append(Text(steps[i], cx, 40, 8, i == 0 ?  ColorText : ColorMuted, "middle"));
                    if (i < steps.Length - 1) sb.Append(HLine(cx + 12, (i + 1) * stepW + stepW / 2 - 12, 20, Border));
                }
                // Footer
                sb.Append(HLine(0, el.W, el.H - 48));
                sb.Append(Rect(el.W - 90, el.H - 36, 78, 28, FillAccent, "#93c5fd", 4));
                sb.Append(Text("Next →", el.W - 51, el.H - 22, 10, Accent, "middle"));
                sb.Append(Rect(el.W - 176, el.H - 36, 78, 28, Fill, Border, 4));
                sb.Append(Text("← Back", el.W - 137, el.H - 22, 10, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            },
            [
                Prop("steps", "Steps", PropType.StringList, cat: "Content"),
            ]);

        yield return Def("TmFilterBuilder", CatComplex, "Filter Builder", "filter", 500, 160,
            (el, b) =>
            {
                var conditions = el.Props.GetInt("conditions", 2);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                for (var i = 0; i < conditions && i < 4; i++)
                {
                    var ry = 12 + i * 40.0;
                    if (i > 0) sb.Append(Pill(8, ry - 8, 30, 16, FillAccent, "#93c5fd"));
                    var fw = (el.W - 32) / 3.0 - 4;
                    sb.Append(Rect(8, ry, fw, 28, Fill, Border, 4));
                    sb.Append(ChevronDown(8 + fw - 14, ry + 10));
                    sb.Append(Rect(8 + fw + 4, ry, fw, 28, Fill, Border, 4));
                    sb.Append(ChevronDown(8 + fw * 2 + 8, ry + 10));
                    sb.Append(Rect(8 + fw * 2 + 8, ry, fw, 28, Fill, Border, 4));
                    sb.Append(Icon("x", el.W - 14, ry + 14, 10));
                }
                sb.Append(Text("+ Add condition", 12, el.H - 14, 10, Accent));
                Svg(b, sb.ToString());
            },
            [
                Prop("conditions", "Conditions", PropType.Int, 2, cat: "Appearance"),
            ]);

        yield return Def("TmActivityLog", CatComplex, "Activity Log", "activity", 400, 280,
            (el, b) =>
            {
                var count = el.Props.GetInt("itemCount", 5);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(HLine(0, el.W, 36));
                sb.Append(Text("Activity", 12, 18, 12, ColorText, "start", "500"));
                var rowH2 = (el.H - 36) / count;
                for (var i = 0; i < count; i++)
                {
                    var ry = 36 + i * rowH2;
                    if (i > 0) sb.Append(HLine(0, el.W, ry));
                    sb.Append($"<circle cx='20' cy='{F(ry + rowH2 / 2)}' r='8' fill='{FillDark}' stroke='{Border}'></circle>");
                    sb.Append(Rect(36, ry + rowH2 / 2 - 5, el.W * 0.5, 10, FillDark, "none", 3));
                    sb.Append(Rect(el.W - 64, ry + rowH2 / 2 - 4, 56, 8, FillDark, "none", 3));
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("itemCount", "Item Count", PropType.Int, 5, cat: "Appearance"),
            ]);

        yield return Def("TmActivityComments", CatComplex, "Activity Comments", "message-circle", 400, 320,
            (el, b) =>
            {
                var count = el.Props.GetInt("commentCount", 3);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                var commentH = (el.H - 56) / count;
                for (var i = 0; i < count; i++)
                {
                    var ry = i * commentH + 8;
                    sb.Append($"<circle cx='20' cy='{F(ry + 14)}' r='12' fill='{FillDark}' stroke='{Border}'></circle>");
                    sb.Append(Rect(40, ry, el.W - 48, commentH - 8, FillDark, Border, 6));
                    sb.Append(Rect(48, ry + 8, el.W * 0.35, 8, Fill, "none", 3));
                    sb.Append(Rect(48, ry + 22, el.W * 0.6, 8, Fill, "none", 3));
                }
                sb.Append(HLine(0, el.W, el.H - 48));
                sb.Append(Rect(8, el.H - 40, el.W - 16, 32, FillDark, Border, 4));
                sb.Append(Text("Write a comment...", 16, el.H - 24, 10, ColorLight));
                Svg(b, sb.ToString());
            },
            [
                Prop("commentCount", "Comment Count", PropType.Int, 3, cat: "Appearance"),
            ]);

        yield return Def("TmTreeView", CatComplex, "Tree View", "git-branch", 240, 200,
            (el, b) =>
            {
                var depth = el.Props.GetInt("depth", 3);
                var showCheckboxes = el.Props.GetBool("showCheckboxes");
                var sb = new StringBuilder();
                var items = new (int level, string label)[]
                {
                    (0, "Root"), (1, "Branch A"), (2, "Leaf A1"), (2, "Leaf A2"),
                    (1, "Branch B"), (2, "Leaf B1")
                };
                var y = 0.0;
                foreach (var (level, label) in items.Where(x => x.level < depth))
                {
                    var indent = level * 16.0;
                    if (level > 0)
                        sb.Append(VLine(indent - 8, y - 4, y + 10, FillDark));
                    if (showCheckboxes)
                    {
                        sb.Append(Rect(indent + 4, y, 12, 12, Fill, Border, 2));
                        indent += 16;
                    }
                    sb.Append($"<circle cx='{F(indent + 6)}' cy='{F(y + 6)}' r='4' fill='{(level == 0 ? FillAccent : FillDark)}' stroke='{Border}'></circle>");
                    sb.Append(Text(label, indent + 16, y + 6, 11));
                    y += 28;
                    if (y > el.H) break;
                }
                Svg(b, sb.ToString());
            },
            [
                Prop("depth", "Depth", PropType.Int, 3, cat: "Appearance"),
                Prop("showCheckboxes", "Show Checkboxes", PropType.Bool, false, cat: "Appearance"),
            ]);

        yield return Def("TmWorkflowDesignerCanvas", CatComplex, "Workflow Designer", "git-branch", 500, 300,
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Workflow");
                var sb = new StringBuilder();
                sb.Append(DashedRect(el.W, el.H, 6));
                sb.Append(Text(title, el.W / 2, 18, 12, ColorMuted, "middle", "500"));
                // Nodes
                var nodes = new (double x, double y, string label, string type)[]
                {
                    (60, el.H / 2 - 20, "Start", "initial"),
                    (el.W / 2 - 60, el.H / 2 - 20, "Process", "intermediate"),
                    (el.W - 200, el.H / 2 - 20, "End", "final")
                };
                foreach (var (nx, ny, label, ntype) in nodes)
                {
                    var nfill = ntype == "initial" ? FillAccent : ntype == "final" ? "#dcfce7" : Fill;
                    var rx = ntype == "initial" ? 20.0 : 8.0;
                    sb.Append(Rect(nx, ny, 120, 40, nfill, Border, rx));
                    sb.Append(Text(label, nx + 60, ny + 20, 11, ColorText, "middle"));
                }
                // Arrows
                sb.Append(HLine(180, el.W / 2 - 60, el.H / 2));
                sb.Append(HLine(el.W / 2 + 60, el.W - 200, el.H / 2));
                Svg(b, sb.ToString());
            },
            [
                Prop("title", "Title", PropType.String, "Workflow", cat: "Content"),
            ]);
    }
}
