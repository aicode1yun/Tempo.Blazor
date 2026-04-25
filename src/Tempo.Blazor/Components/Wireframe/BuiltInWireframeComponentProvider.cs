using System.Text;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Icons;
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
        foreach (var d in Avatars()) yield return d;
        foreach (var d in Icons()) yield return d;
        foreach (var d in Inputs()) yield return d;
        foreach (var d in Tags()) yield return d;
        foreach (var d in Pickers()) yield return d;
        foreach (var d in Dropdowns()) yield return d;
        foreach (var d in DataDisplay()) yield return d;
        foreach (var d in DataTable()) yield return d;
        foreach (var d in Feedback()) yield return d;
        foreach (var d in Notifications()) yield return d;
        foreach (var d in Navigation()) yield return d;
        foreach (var d in Layout()) yield return d;
        foreach (var d in Toolbar()) yield return d;
        foreach (var d in Forms()) yield return d;
        foreach (var d in Files()) yield return d;
        foreach (var d in Charts()) yield return d;
        foreach (var d in Workflow()) yield return d;
        foreach (var d in Complex()) yield return d;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly BuiltInComponentSchemas _schemas = new();

    private static WireframeComponentDef DefFromSchema(
        string type, string? icon,
        Action<WireframeElement, RenderTreeBuilder> render)
    {
        var s = _schemas.GetSchemas().FirstOrDefault(x => x.Type == type)
                ?? throw new InvalidOperationException($"No schema found for '{type}'");
        return new WireframeComponentDef
        {
            Type         = s.Type,
            Category     = s.Category,
            DisplayName  = s.DisplayName,
            Icon         = icon,
            DefaultWidth  = s.DefaultWidth,
            DefaultHeight = s.DefaultHeight,
            Props        = [.. s.Props],
            IsBuiltIn    = true,
            RenderSvg    = render,
            SizePresets  = s.SizePresets,
        };
    }

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

    private static IEnumerable<WireframeComponentDef> Buttons()
    {
        yield return DefFromSchema("TmButton", "square",
            (el, b) =>
            {
                var label    = el.Props.GetString("label", "Button");
                var variant  = el.Props.GetString("variant", "primary");
                var disabled = el.Props.GetBool("disabled");
                var loading  = el.Props.GetBool("loading");
                var block    = el.Props.GetBool("block");
                var icon     = el.Props.GetString("icon");
                var iconRight= el.Props.GetBool("iconRight");
                var loadingText = el.Props.GetString("loadingText", "");
                var (font, rx) = SizeScale(el.Props.GetString("size", "md"));
                var w = block ? Math.Max(el.W, 120) : el.W;
                var h = el.H;
                var (fill, border, textColor) = variant switch
                {
                    "primary"   => (FillAccent, "#93c5fd", ColorText),
                    "danger"    => ("#fee2e2",  "#fca5a5", "#dc2626"),
                    "ghost"     => ("none",     Border,    ColorText),
                    "outline"   => ("none",     "#93c5fd", Accent),
                    "link"      => ("none",     "none",    Accent),
                    _           => (Fill,       Border,    ColorText),   // secondary / default
                };
                var sb = new StringBuilder();
                if (disabled || loading) sb.Append("<g opacity='" + (disabled ? "0.45" : "0.7") + "'>");
                sb.Append(Rect(0, 0, w, h, fill, border, rx));
                if (variant == "link")
                    sb.Append($"<line x1='8' y1='{F(h - 4)}' x2='{F(w - 8)}' y2='{F(h - 4)}' stroke='{Accent}' stroke-width='1'/>");
                var displayText = loading && !string.IsNullOrEmpty(loadingText) ? loadingText : label;
                var textX = w / 2;
                var hasIcon = !string.IsNullOrEmpty(icon);
                var iconOffset = hasIcon ? 10 : 0;
                if (loading)
                {
                    sb.Append(Icon("spinner", w / 2 - (displayText.Length * font * 0.3) - 10 - iconOffset, h / 2, 12));
                }
                else if (hasIcon)
                {
                    var icoX = iconRight ? w / 2 + (displayText.Length * font * 0.3) + 10 : w / 2 - (displayText.Length * font * 0.3) - 10;
                    sb.Append(Icon(icon, icoX, h / 2, 12));
                    textX = iconRight ? w / 2 - 10 : w / 2 + 10;
                }
                sb.Append(Text(displayText, textX, h / 2, font, textColor, "middle", "500"));
                if (disabled || loading) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmSplitButton", "layout",
            (el, b) =>
            {
                var label   = el.Props.GetString("label", "Action");
                var variant = el.Props.GetString("variant", "primary");
                var (font, rx) = SizeScale(el.Props.GetString("size", "md"));
                var w = el.W; var h = el.H;
                var (fill, border, textColor) = variant switch
                {
                    "danger"    => ("#fee2e2", "#fca5a5", "#dc2626"),
                    "ghost"     => ("none",    Border,    ColorText),
                    "secondary" => (Fill,      Border,    ColorText),
                    _           => (FillAccent, "#93c5fd", ColorText),  // primary
                };
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, w, h, fill, border, rx));
                sb.Append(TextCentred(label, w - 28, h, font, textColor));
                sb.Append(VLine(w - 28, 0, h, border == "none" ? Border : border));
                sb.Append(Rect(w - 28, 0, 28, h, fill, "none", 0));
                sb.Append(ChevronDown(w - 20, h / 2 - 4));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCopyButton", "copy",
            (el, b) =>
            {
                var (_, rx) = SizeScale(el.Props.GetString("size", "md"));
                var h = el.H;
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, h, Fill, Border, rx));
                sb.Append(Icon("copy", el.W / 2, h / 2, h * 0.45));
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AVATARS
    // ══════════════════════════════════════════════════════════════════════════
    private static IEnumerable<WireframeComponentDef> Avatars()
    {
        yield return DefFromSchema("TmAvatar", "user",
            (el, b) =>
            {
                var size = el.Props.GetString("size", "md");
                var shape = el.Props.GetString("shape", "circle");
                var color = el.Props.GetString("color", "gray");
                var name = el.Props.GetString("name", "AB");
                var initials = string.Join("", name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0])).ToUpperInvariant();
                if (initials.Length > 2) initials = initials.Substring(0, 2);
                if (string.IsNullOrEmpty(initials)) initials = "?";
                var sb = new StringBuilder();
                var dim = size switch { "xs" => 24.0, "sm" => 32.0, "lg" => 48.0, "xl" => 56.0, "xxl" => 64.0, _ => 40.0 };
                var rx = shape == "square" ? 6.0 : dim / 2;
                var fill = color switch
                {
                    "blue" => "#dbeafe",
                    "green" => "#dcfce7",
                    "purple" => "#f3e8ff",
                    "red" => "#fee2e2",
                    "yellow" => "#fef9c3",
                    _ => "#e5e7eb"
                };
                var textColor = color switch
                {
                    "blue" => "#2563eb",
                    "green" => "#16a34a",
                    "purple" => "#9333ea",
                    "red" => "#dc2626",
                    "yellow" => "#ca8a04",
                    _ => "#6b7280"
                };
                sb.Append($"<rect x='0' y='0' width='{F(dim)}' height='{F(dim)}' rx='{F(rx)}' fill='{fill}' stroke='{Border}' stroke-width='1'></rect>");
                sb.Append(Text(initials, dim / 2, dim / 2, dim * 0.35, textColor, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmAvatarGroup", "users",
            (el, b) =>
            {
                var count = el.Props.GetInt("count", 3);
                var max = el.Props.GetInt("max", 3);
                var size = el.Props.GetString("size", "md");
                var dim = size switch { "xs" => 24.0, "sm" => 32.0, "lg" => 48.0, "xl" => 56.0, "xxl" => 64.0, _ => 40.0 };
                var overlap = dim * 0.35;
                var sb = new StringBuilder();
                var visible = Math.Min(count, max);
                for (var i = 0; i < visible; i++)
                {
                    var x = i * (dim - overlap);
                    sb.Append($"<circle cx='{F(x + dim / 2)}' cy='{F(dim / 2)}' r='{F(dim / 2)}' fill='{FillDark}' stroke='white' stroke-width='2'></circle>");
                    sb.Append(Text(((char)('A' + i)).ToString(), x + dim / 2, dim / 2, dim * 0.35, ColorText, "middle"));
                }
                if (count > max)
                {
                    var x = visible * (dim - overlap);
                    sb.Append($"<circle cx='{F(x + dim / 2)}' cy='{F(dim / 2)}' r='{F(dim / 2)}' fill='{ColorMuted}' stroke='white' stroke-width='2'></circle>");
                    sb.Append(Text($"+{count - max}", x + dim / 2, dim / 2, dim * 0.28, "white", "middle"));
                }
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ICONS
    // ══════════════════════════════════════════════════════════════════════════
    private static IEnumerable<WireframeComponentDef> Icons()
    {
        yield return DefFromSchema("TmIcon", "circle",
            (el, b) =>
            {
                var name = el.Props.GetString("name", "circle");
                var customSvg = el.Props.GetString("svg", "");
                var size = el.Props.GetString("size", "md");
                var color = el.Props.GetString("color", "gray");
                var dim = size switch { "sm" => 16.0, "lg" => 32.0, "xl" => 48.0, _ => 24.0 };
                var stroke = color switch
                {
                    "blue" => Accent,
                    "green" => "#22c55e",
                    "red" => "#ef4444",
                    "yellow" => "#eab308",
                    "purple" => "#9333ea",
                    _ => BorderStrong
                };
                var style = el.Props.GetString("style", "");
                var svgStyle = string.IsNullOrWhiteSpace(style) ? "" : $" style='{System.Web.HttpUtility.HtmlAttributeEncode(style)}'";

                string svgContent;
                if (!string.IsNullOrWhiteSpace(customSvg))
                {
                    svgContent = customSvg;
                }
                else
                {
                    var builtIn = TmIcon.GetBuiltInSvg(name);
                    if (!string.IsNullOrEmpty(builtIn))
                    {
                        svgContent = builtIn;
                    }
                    else
                    {
                        var registered = IconRegistry.Resolve(name);
                        svgContent = registered
                            ?? $"<circle cx='12' cy='12' r='10' fill='none' stroke='{BorderStrong}' stroke-width='1.5'></circle>";
                    }
                }

                var svg = $"<svg viewBox='0 0 24 24' width='{F(dim)}' height='{F(dim)}' fill='none' stroke='{stroke}' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'{svgStyle}>{svgContent}</svg>";
                Svg(b, svg);
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INPUTS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Inputs()
    {
        yield return DefFromSchema("TmTextInput", "edit-2",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var ph = el.Props.GetString("placeholder", "Enter text...");
                var req = el.Props.GetBool("required");
                var dis = el.Props.GetBool("disabled");
                var ro  = el.Props.GetBool("readOnly");
                var type = el.Props.GetString("type", "text");
                var maxLength = el.Props.GetInt("maxLength", 0);
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                var rectFill = dis ? FillDark : Fill;
                var rectBorder = ro ? "none" : Border;
                sb.Append(Rect(0, 0, el.W, 36, rectFill, rectBorder));
                if (ro)
                    sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='36' fill='none' stroke='{Border}' stroke-width='1' stroke-dasharray='4 2' rx='3'></rect>");
                var placeholderText = type switch
                {
                    "password" => "••••••",
                    "email" => "name@example.com",
                    "tel" => "+1 234 567 890",
                    "url" => "https://example.com",
                    _ => ph
                };
                var textX = 8.0;
                if (type == "password")
                {
                    sb.Append(Icon("lock", el.W - 18, 18, 14));
                }
                else if (type == "email")
                {
                    sb.Append(Text("@", el.W - 18, 18, 12, ColorMuted, "middle"));
                }
                sb.Append(Text(placeholderText, textX, 18, 10, ColorLight));
                if (maxLength > 0)
                    sb.Append(Text($"0/{maxLength}", el.W - 8, 18, 9, ColorLight, "end"));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmTextArea", "align-left",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var ph = el.Props.GetString("placeholder", "Enter text...");
                var req = el.Props.GetBool("required");
                var dis = el.Props.GetBool("disabled");
                var autoGrow = el.Props.GetBool("autoGrow", false);
                var h = el.H - 16;
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, el.W, h, dis ? FillDark : Fill, Border));
                sb.Append(Text(ph, 8, 16, 10, ColorLight));
                if (autoGrow)
                {
                    sb.Append($"<line x1='{F(el.W - 16)}' y1='{F(h - 4)}' x2='{F(el.W - 4)}' y2='{F(h - 16)}' stroke='{ColorLight}' stroke-width='1.5' stroke-linecap='round'></line>");
                    sb.Append($"<line x1='{F(el.W - 10)}' y1='{F(h - 4)}' x2='{F(el.W - 4)}' y2='{F(h - 10)}' stroke='{ColorLight}' stroke-width='1.5' stroke-linecap='round'></line>");
                }
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmNumberInput", "hash",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                var dis = el.Props.GetBool("disabled");
                var w = el.W; var h = 36.0;
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, w, h, dis ? FillDark : Fill, Border));
                sb.Append(Text("0", 8, h / 2, 11, ColorText));
                sb.Append(VLine(w - 24, 0, h));
                sb.Append(Text("▲", w - 16, h / 2 - 6, 8, ColorMuted, "middle"));
                sb.Append(Text("▼", w - 16, h / 2 + 5, 8, ColorMuted, "middle"));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmSearchInput", "search",
            (el, b) =>
            {
                var ph = el.Props.GetString("placeholder", "Search...");
                var dis = el.Props.GetBool("disabled");
                Svg(b, InputField(el.W, el.H, "", ph, hasIcon: true, disabled: dis));
            });

        yield return DefFromSchema("TmCurrencyInput", "dollar-sign",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Amount");
                var sym = el.Props.GetString("currencySymbol", "Kč");
                var dis = el.Props.GetBool("disabled");
                var h = 36.0;
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, el.W, h, dis ? FillDark : Fill, Border));
                sb.Append(Rect(0, 0, 28, h, FillDark, Border, 0));
                sb.Append(Text(sym, 14, h / 2, 10, ColorMuted, "middle"));
                sb.Append(Text("0.00", 36, h / 2, 11, ColorText));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCheckbox", "check-square",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Checkbox");
                var chk = el.Props.GetBool("checked");
                var indeterminate = el.Props.GetBool("indeterminate");
                var dis = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                var isChecked = chk && !indeterminate;
                sb.Append(Rect(0, 0, 16, 16, isChecked ? FillAccent : Fill, isChecked ? "#93c5fd" : Border, 3));
                if (isChecked) sb.Append(Icon("check", 8, 8, 10));
                else if (indeterminate) sb.Append(HLine(4, 12, 8, Accent));
                sb.Append(Text(lbl, 22, 8, 11));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmRadio", "circle",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Option");
                var chk = el.Props.GetBool("checked");
                var dis = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                sb.Append($"<circle cx='8' cy='8' r='7' fill='{Fill}' stroke='{(chk ? "#93c5fd" : Border)}' stroke-width='1.5'></circle>");
                if (chk) sb.Append($"<circle cx='8' cy='8' r='3.5' fill='{Accent}'></circle>");
                sb.Append(Text(lbl, 22, 8, 11));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmRadioGroup", "list",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Options");
                var opts = el.Props.GetStringList("options").Take(4).ToArray();
                if (opts.Length == 0) opts = ["Option 1", "Option 2", "Option 3"];
                var dis = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                sb.Append(FieldLabel(lbl));
                for (var i = 0; i < opts.Length; i++)
                {
                    var y = i * 18.0;
                    sb.Append($"<circle cx='8' cy='{F(y + 8)}' r='7' fill='{Fill}' stroke='{Border}' stroke-width='1.5'></circle>");
                    if (i == 0) sb.Append($"<circle cx='8' cy='{F(y + 8)}' r='3.5' fill='{Accent}'></circle>");
                    sb.Append(Text(opts[i], 22, y + 8, 11));
                }
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmToggle", "toggle-right",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Toggle");
                var chk = el.Props.GetBool("checked");
                var dis = el.Props.GetBool("disabled");
                var trackFill = chk ? "#93c5fd" : FillDark;
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                sb.Append(Pill(0, 2, 36, 16, trackFill, "none"));
                var cx = chk ? 28.0 : 10.0;
                sb.Append($"<circle cx='{F(cx)}' cy='10' r='7' fill='white' stroke='{Border}' stroke-width='1'></circle>");
                sb.Append(Text(lbl, 42, 9, 11));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmToggleSection", "chevron-down",
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
            });

        yield return DefFromSchema("TmSelect", "chevron-down",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var ph = el.Props.GetString("placeholder", "Select option...");
                var req = el.Props.GetBool("required");
                var dis = el.Props.GetBool("disabled");
                var multiple = el.Props.GetBool("multiple", false);
                var clearable = el.Props.GetBool("clearable", false);
                var h = 36.0;
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, el.W, h, dis ? FillDark : Fill, Border));
                if (multiple)
                {
                    sb.Append(Pill(6, 8, 50, 20, FillAccent, "#93c5fd"));
                    sb.Append(Text("Item 1", 11, 18, 9, Accent));
                    sb.Append(Icon("x", 50, 18, 8));
                    sb.Append(Text("+2", 64, 18, 9, ColorMuted));
                    sb.Append(ChevronDown(el.W - 16, h / 2 - 4));
                }
                else
                {
                    sb.Append(Text(ph, 8, h / 2, 10, ColorLight));
                    if (clearable)
                        sb.Append(Icon("x", el.W - 22, h / 2, 10));
                    sb.Append(ChevronDown(el.W - (clearable ? 34 : 16), h / 2 - 4));
                }
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmMultiSelect", "chevrons-down",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                var dis = el.Props.GetBool("disabled");
                var h = 36.0;
                var sb = new StringBuilder();
                if (dis) sb.Append("<g opacity='0.45'>");
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                sb.Append(Rect(0, 0, el.W, h, dis ? FillDark : Fill, Border));
                sb.Append(Pill(6, 8, 50, 20, FillAccent, "#93c5fd"));
                sb.Append(Text("Item 1", 11, 18, 9, Accent));
                sb.Append(Icon("x", 50, 18, 8));
                sb.Append(ChevronDown(el.W - 16, h / 2 - 4));
                if (dis) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCascadingSelect", "git-branch",
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
            });

        yield return DefFromSchema("TmFilterableDropdown", "filter",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                Svg(b, InputField(el.W, 36, lbl, "Filter & select...", hasIcon: true, hasChevron: true));
            });

        yield return DefFromSchema("TmEntityPicker", "users",
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
            });

        yield return DefFromSchema("TmExpressionEditor", "code",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Expression");
                var h = 36.0;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl));
                sb.Append(Rect(0, 0, el.W, h, FillDark, Border));
                sb.Append(Text("{expression}", 8, h / 2, 10, ColorMuted));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmPasswordStrengthIndicator", "shield",
            (el, b) =>
            {
                var strength = el.Props.GetInt("strength", 3);
                strength = Math.Clamp(strength, 0, 5);
                var sb = new StringBuilder();
                var colors = new[] { "#ef4444", "#f97316", "#eab308", "#84cc16", "#22c55e", "#16a34a" };
                var fill = colors[strength];
                var label = strength switch { 0 or 1 => "Very weak", 2 => "Weak", 3 => "Medium", 4 => "Strong", _ => "Very strong" };
                sb.Append(Rect(0, 0, el.W, 8, FillDark, "none", 4));
                var fillW = el.W * (strength / 5.0);
                sb.Append($"<rect x='0' y='0' width='{F(fillW)}' height='8' rx='4' fill='{fill}'></rect>");
                sb.Append(Text(label, 0, 24, 11, fill, "start", "500"));
                sb.Append(Text("Use 8+ chars with letters and numbers", 0, 38, 10, ColorMuted, "start"));
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TAGS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Tags()
    {
        yield return DefFromSchema("TmTagPicker", "tag",
            (el, b) =>
            {
                var tags = el.Props.GetStringList("tags");
                if (tags.Length == 0) tags = ["Tag 1", "Tag 2"];
                var allowCreate = el.Props.GetBool("allowCreate", false);
                var disabled = el.Props.GetBool("disabled");
                var h = allowCreate ? 70.0 : 40.0;
                var sb = new StringBuilder();
                if (disabled) sb.Append("<g opacity='0.45'>");
                var x = 0.0;
                foreach (var tag in tags.Take(4))
                {
                    var tw = tag.Length * 6.5 + 24;
                    sb.Append(Pill(x, 8, tw, 24, FillAccent, "#93c5fd"));
                    sb.Append(Text(tag, x + 10, 20, 10, Accent));
                    sb.Append(Text("×", x + tw - 10, 20, 10, ColorMuted, "middle"));
                    x += tw + 6;
                }
                if (!disabled)
                {
                    sb.Append(Pill(x, 8, 28, 24, FillDark, Border));
                    sb.Append(Text("+", x + 14, 20, 12, ColorText, "middle"));
                }
                if (allowCreate && !disabled)
                {
                    sb.Append(Rect(0, 36, el.W, 26, Fill, Border));
                    sb.Append(Text("Create new tag...", 8, 49, 10, ColorLight));
                }
                if (disabled) sb.Append("</g>");
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PICKERS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Pickers()
    {
        static WireframeComponentDef DateLike(string type, string display, string icon, string defaultLabel)
            => DefFromSchema(type, icon,
                (el, b) =>
                {
                    var lbl = el.Props.GetString("label", defaultLabel);
                    var req = el.Props.GetBool("required");
                    var format = el.Props.GetString("format", "dd.mm.yyyy");
                    var h = 36.0;
                    var sb = new StringBuilder();
                    if (!string.IsNullOrEmpty(lbl)) sb.Append(FieldLabel(lbl, req));
                    sb.Append(Rect(0, 0, el.W, h));
                    sb.Append(Text(format, 8, h / 2, 10, ColorLight));
                    sb.Append(Icon(icon, el.W - 18, h / 2, h * 0.5));
                    Svg(b, sb.ToString());
                });

        yield return DateLike("TmDatePicker", "Date Picker", "calendar", "Date");
        yield return DateLike("TmDateTimePicker", "Date & Time Picker", "calendar", "Date & Time");

        yield return DefFromSchema("TmTimePicker", "clock",
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
            });

        yield return DefFromSchema("TmDateRangePicker", "calendar",
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
            });

        yield return DefFromSchema("TmTimeRangePicker", "clock",
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
            });

        yield return DefFromSchema("TmDateTimeRangePicker", "calendar",
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
            });

        yield return DefFromSchema("TmTimeInput", "clock",
            (el, b) =>
            {
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H));
                sb.Append(Text("HH", 12, el.H / 2, 12, ColorText, "middle"));
                sb.Append(Text(":", el.W / 2, el.H / 2, 12, ColorMuted, "middle"));
                sb.Append(Text("MM", el.W - 12, el.H / 2, 12, ColorText, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCalendarView", "calendar",
            (el, b) =>
            {
                var month = el.Props.GetString("month", "January");
                var year = el.Props.GetInt("year", 2025);
                var selectedDay = el.Props.GetInt("selectedDay", 15);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8));
                sb.Append(Rect(0, 0, el.W, 36, FillDark, "none", 8));
                sb.Append(HLine(0, el.W, 36));
                sb.Append(Text($"{month} {year}", el.W / 2, 18, 12, ColorText, "middle", "500"));
                sb.Append(Text("‹", 20, 18, 14, ColorMuted, "middle"));
                sb.Append(Text("›", el.W - 20, 18, 14, ColorMuted, "middle"));
                var days = new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
                var cellW = el.W / 7;
                for (var i = 0; i < 7; i++)
                {
                    sb.Append(Text(days[i], i * cellW + cellW / 2, 52, 9, ColorMuted, "middle"));
                }
                for (var r = 0; r < 6; r++)
                {
                    for (var c = 0; c < 7; c++)
                    {
                        var day = r * 7 + c + 1;
                        if (day > 31) break;
                        var cx = c * cellW + cellW / 2;
                        var cy = 68 + r * 30;
                        if (day == selectedDay)
                            sb.Append(Pill(cx - 14, cy - 10, 28, 20, FillAccent, "#93c5fd"));
                        sb.Append(Text(day.ToString(), cx, cy, 10, day == selectedDay ? Accent : ColorText, "middle"));
                    }
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCalendarGrid", "grid",
            (el, b) =>
            {
                var month = el.Props.GetString("month", "January");
                var year = el.Props.GetInt("year", 2025);
                var sb = new StringBuilder();
                sb.Append(Text($"{month} {year}", el.W / 2, 10, 11, ColorText, "middle", "500"));
                var cellW = el.W / 7;
                var days = new[] { "M", "T", "W", "T", "F", "S", "S" };
                for (var i = 0; i < 7; i++)
                    sb.Append(Text(days[i], i * cellW + cellW / 2, 28, 9, ColorMuted, "middle"));
                for (var r = 0; r < 6; r++)
                {
                    sb.Append(HLine(0, el.W, 40 + r * 26));
                    for (var c = 0; c < 7; c++)
                    {
                        var day = r * 7 + c + 1;
                        if (day > 31) break;
                        var cx = c * cellW + cellW / 2;
                        var cy = 52 + r * 26;
                        sb.Append(Text(day.ToString(), cx, cy, 10, ColorText, "middle"));
                    }
                }
                sb.Append(HLine(0, el.W, el.H));
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DROPDOWNS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Dropdowns()
    {
        yield return DefFromSchema("TmDropdown", "chevron-down",
            (el, b) =>
            {
                var text = el.Props.GetString("text", "Options");
                var icon = el.Props.GetString("icon");
                var disabled = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (disabled) sb.Append("<g opacity='0.45'>");
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                var textX = 12.0;
                if (!string.IsNullOrEmpty(icon))
                {
                    sb.Append(Icon(icon, 16, el.H / 2, 14));
                    textX = 32;
                }
                sb.Append(Text(text, textX, el.H / 2, 11, ColorText, "start"));
                sb.Append(ChevronDown(el.W - 18, el.H / 2 - 4));
                if (disabled) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmDropdownItem", "menu",
            (el, b) =>
            {
                var label = el.Props.GetString("label", "Item");
                var icon = el.Props.GetString("icon");
                var disabled = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (disabled) sb.Append("<g opacity='0.45'>");
                sb.Append(Rect(0, 0, el.W, el.H, "none", "none", 0));
                var textX = 12.0;
                if (!string.IsNullOrEmpty(icon))
                {
                    sb.Append(Icon(icon, 16, el.H / 2, 14));
                    textX = 36;
                }
                sb.Append(Text(label, textX, el.H / 2, 11, ColorText, "start"));
                if (disabled) sb.Append("</g>");
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DATA DISPLAY
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> DataDisplay()
    {
        yield return DefFromSchema("TmDivider", "minus",
            (el, b) =>
            {
                var sb = new StringBuilder();
                sb.Append(HLine(0, el.W, el.H / 2, Border));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmText", "type",
            (el, b) =>
            {
                var text = el.Props.GetString("text", "Text");
                var align = el.Props.GetString("align", "left");
                var sb = new StringBuilder();
                var x = align switch { "center" => el.W / 2, "right" => el.W - 4, _ => 4.0 };
                var anchor = align switch { "center" => "middle", "right" => "end", _ => "start" };
                sb.Append(Text(text, x, el.H / 2, Math.Min(el.H * 0.6, 14), ColorText, anchor, "400"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCard", "square",
            (el, b) =>
            {
                var title      = el.Props.GetString("title", "Card Title");
                var showHeader = el.Props.GetBool("showHeader", true);
                var showFooter = el.Props.GetBool("showFooter", false);
                var variant    = el.Props.GetString("variant", "default");
                var headerIcon = el.Props.GetString("headerIcon");
                var sb = new StringBuilder();
                var cardFill = variant == "outlined" ? "none" : Fill;
                var cardBorder = variant == "elevated" ? Border : (variant == "outlined" ? BorderStrong : Border);
                var cardStrokeWidth = variant == "outlined" ? 1.5 : 1;
                sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='{F(el.H)}' rx='8' fill='{cardFill}' stroke='{cardBorder}' stroke-width='{F(cardStrokeWidth)}'></rect>");
                if (variant == "elevated")
                    sb.Append($"<rect x='2' y='2' width='{F(el.W - 4)}' height='{F(el.H - 4)}' rx='7' fill='none' stroke='{FillDark}' stroke-width='0.5' opacity='0.5'></rect>");
                if (showHeader)
                {
                    sb.Append(Rect(0, 0, el.W, 40, FillDark, "none", 8));
                    sb.Append(Rect(0, 32, el.W, 8, FillDark, "none", 0));
                    sb.Append(HLine(0, el.W, 40));
                    var titleX = 12.0;
                    if (!string.IsNullOrEmpty(headerIcon))
                    {
                        sb.Append(Icon(headerIcon, 24, 20, 14));
                        titleX = 38.0;
                    }
                    sb.Append(Text(title, titleX, 20, 12, ColorText, "start", "500"));
                }
                if (showFooter)
                {
                    sb.Append(HLine(0, el.W, el.H - 44));
                    sb.Append(Rect(0, el.H - 44, el.W, 44, FillDark, "none", 0));
                    sb.Append(Rect(0, el.H - 8, el.W, 8, FillDark, "none", 8)); // round bottom corners
                    var primaryLabel   = el.Props.GetString("primaryActionLabel", "Save");
                    var secondaryLabel = el.Props.GetString("secondaryActionLabel", "Cancel");
                    var showPrimary    = el.Props.GetBool("showPrimaryAction", true);
                    var showSecondary  = el.Props.GetBool("showSecondaryAction", true);
                    if (showPrimary)
                    {
                        sb.Append(Rect(el.W - 90, el.H - 36, 78, 26, FillAccent, "#93c5fd", 4));
                        sb.Append(Text(primaryLabel, el.W - 51, el.H - 23, 10, Accent, "middle"));
                    }
                    if (showSecondary)
                    {
                        sb.Append(Rect(el.W - (showPrimary ? 176 : 90), el.H - 36, 78, 26, Fill, Border, 4));
                        sb.Append(Text(secondaryLabel, el.W - (showPrimary ? 137 : 51), el.H - 23, 10, ColorMuted, "middle"));
                    }
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmStatCard", "trending-up",
            (el, b) =>
            {
                var title = el.Props.GetString("title") ?? el.Props.GetString("label", "Total Revenue");
                var value = el.Props.GetString("value", "12 450");
                var subValue = el.Props.GetString("subValue");
                var subValueColor = el.Props.GetString("subValueColor", ColorMuted);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8));
                sb.Append(Text(title, 12, 22, 10, ColorMuted));
                sb.Append(Text(value, 12, 55, 18, ColorText, "start", "600"));
                if (!string.IsNullOrEmpty(subValue))
                    sb.Append(Text(subValue, 12, 78, 10, subValueColor));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmBadge", "tag",
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
            });

        yield return DefFromSchema("TmChip", "tag",
            (el, b) =>
            {
                var lbl      = el.Props.GetString("label", "Chip");
                var removable = el.Props.GetBool("removable", true);
                var variant  = el.Props.GetString("variant", "default");
                var (fill, border, textColor) = variant switch
                {
                    "primary" => (FillAccent, "#93c5fd", Accent),
                    "success" => ("#dcfce7",  "#86efac", "#16a34a"),
                    "danger"  => ("#fee2e2",  "#fca5a5", "#dc2626"),
                    "warning" => ("#fef9c3",  "#fde047", "#ca8a04"),
                    _         => (FillDark,   Border,    ColorText),
                };
                var sb = new StringBuilder();
                sb.Append(Pill(0, 0, el.W, el.H, fill, border));
                sb.Append(Text(lbl, 10, el.H / 2, 10, textColor));
                if (removable) sb.Append(Icon("x", el.W - 12, el.H / 2, 8));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmChipGroup", "tag",
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
            });

        yield return DefFromSchema("TmFilterChip", "filter",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Filter");
                var active = el.Props.GetBool("active", true);
                var removable = el.Props.GetBool("removable", true);
                var sb = new StringBuilder();
                var fill = active ? FillAccent : FillDark;
                var border = active ? "#93c5fd" : Border;
                var textColor = active ? Accent : ColorText;
                sb.Append(Pill(0, 0, el.W, el.H, fill, border));
                sb.Append(Icon("filter", 14, el.H / 2, 10));
                sb.Append(Text(lbl, 28, el.H / 2, 10, textColor));
                if (removable) sb.Append(Icon("x", el.W - 10, el.H / 2, 8));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmAccordion", "chevrons-down",
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
            });

        yield return DefFromSchema("TmAccordionItem", "chevron-down",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Section");
                var expanded = el.Props.GetBool("expanded", false);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
                sb.Append(Text(title, 12, el.H / 2, 11, ColorText, "start", expanded ? "600" : "normal"));
                // chevron pointing up if expanded, down if collapsed
                var cy = el.H / 2 - 3;
                if (expanded)
                    sb.Append($"<polyline points='{F(el.W - 18)},{F(cy + 4)} {F(el.W - 14)},{F(cy)} {F(el.W - 10)},{F(cy + 4)}' fill='none' stroke='{BorderStrong}' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round'></polyline>");
                else
                    sb.Append(ChevronDown(el.W - 18, cy));
                if (expanded)
                {
                    sb.Append(HLine(8, el.W - 8, el.H));
                    sb.Append(Text("Content area...", 12, el.H + 14, 10, ColorMuted));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmEmptyState", "inbox",
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
            });

        yield return DefFromSchema("TmChangeDiff", "git-commit",
            (el, b) =>
            {
                var oldValue = el.Props.GetString("oldValue", "Old value");
                var newValue = el.Props.GetString("newValue", "New value");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                // Old
                sb.Append(Rect(8, 8, el.W / 2 - 12, el.H - 16, "#fee2e2", "#fca5a5", 4));
                sb.Append(Text("- " + oldValue, 16, el.H / 2, 10, "#dc2626"));
                // New
                sb.Append(Rect(el.W / 2 + 4, 8, el.W / 2 - 12, el.H - 16, "#dcfce7", "#86efac", 4));
                sb.Append(Text("+ " + newValue, el.W / 2 + 12, el.H / 2, 10, "#16a34a"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmKanbanBoard", "columns",
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
            });

        yield return DefFromSchema("TmMultiViewList", "list",
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
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DATA TABLE
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> DataTable()
    {
        yield return DefFromSchema("TmDataTable", "table",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "");
                var emptyTitle = el.Props.GetString("emptyTitle", "");
                var cols = el.Props.GetStringList("columns");
                if (cols.Length == 0) cols = ["Column 1", "Column 2", "Column 3", "Column 4"];
                var rows = el.Props.GetInt("rows", 5);
                var showSearch = el.Props.GetBool("showSearch", true);
                var showPagination = el.Props.GetBool("showPagination", true);
                var scrollMode = el.Props.GetString("scrollMode", "pagination");
                var showBulkActions = el.Props.GetBool("showBulkActions", false);
                var bulkActions = el.Props.GetStringList("bulkActions");
                if (bulkActions.Length == 0) bulkActions = ["Delete", "Export"];
                var selectable = el.Props.GetBool("selectable", false);
                var showColumnPicker = el.Props.GetBool("showColumnPicker", false);
                var showGrouping = el.Props.GetBool("showGrouping", false);
                var showFilters = el.Props.GetBool("showFilters", false);
                var isEmpty = rows == 0 && !string.IsNullOrEmpty(emptyTitle);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));

                var top = 0.0;
                if (!string.IsNullOrEmpty(title) || showSearch || showColumnPicker)
                {
                    sb.Append(Rect(0, 0, el.W, 40, FillDark, "none", 4));
                    sb.Append(HLine(0, el.W, 40));
                    if (!string.IsNullOrEmpty(title))
                        sb.Append(Text(title, 12, 20, 12, ColorText, "start", "500"));
                    if (showSearch)
                        sb.Append(InputField(160, 26, "", "Search...", hasIcon: true));
                    if (showColumnPicker)
                    {
                        sb.Append(Rect(el.W - 110, 7, 100, 26, Fill, Border, 4));
                        sb.Append(Text("Columns ▾", el.W - 60, 20, 10, ColorMuted, "middle"));
                    }
                    top = 40;
                }

                if (showBulkActions)
                {
                    var barH = 44.0;
                    sb.Append(Rect(0, top, el.W, barH, FillAccent, "#93c5fd", 4));
                    sb.Append(Text("3 selected", 12, top + barH / 2, 11, Accent));
                    var btnX = el.W - 44;
                    foreach (var action in bulkActions.AsEnumerable().Reverse())
                    {
                        btnX -= 68;
                        sb.Append(Rect(btnX, top + 8, 60, 28, Fill, Border, 4));
                        sb.Append(Text(action, btnX + 30, top + barH / 2, 10, ColorText, "middle"));
                    }
                    sb.Append(Rect(el.W - 44, top + 8, 36, 28, Fill, Border, 4));
                    sb.Append(Icon("x", el.W - 26, top + barH / 2, 12));
                    top += barH;
                }

                var paginH = (showPagination && scrollMode == "pagination") ? 36.0 : 0;
                var tableH = el.H - top - paginH;
                var checkboxW = selectable ? 36.0 : 0;
                var contentW = el.W - checkboxW;
                var colW = contentW / cols.Length;
                var rowH = tableH / (rows + 1 + (showFilters ? 1 : 0));

                // Grouping row
                if (showGrouping)
                {
                    sb.Append(Rect(0, top, el.W, rowH, FillAccent, "none", 0));
                    sb.Append(HLine(0, el.W, top + rowH));
                    sb.Append(Text("▾ Group by: Category", 8, top + rowH / 2, 10, Accent, "start", "500"));
                    top += rowH;
                }

                // Header
                sb.Append(Rect(0, top, el.W, rowH, FillDark, "none", 0));
                sb.Append(HLine(0, el.W, top + rowH));
                var colOffset = selectable ? checkboxW : 0;
                if (selectable)
                    sb.Append(Rect(10, top + rowH / 2 - 6, 12, 12, FillDark, Border, 2));
                for (var c = 0; c < cols.Length; c++)
                    sb.Append(Text(cols[c], colOffset + c * colW + 8, top + rowH / 2, 10, ColorMuted, "start", "500"));

                // Filter row
                if (showFilters)
                {
                    var fr = top + rowH;
                    sb.Append(HLine(0, el.W, fr + rowH));
                    for (var c = 0; c < cols.Length; c++)
                    {
                        if (c > 0 || selectable) sb.Append(VLine(colOffset + c * colW, fr, fr + rowH));
                        sb.Append(Rect(colOffset + c * colW + 8, fr + 6, colW - 16, rowH - 12, Fill, Border, 3));
                    }
                    if (selectable)
                        sb.Append(VLine(checkboxW, fr, fr + rowH));
                    top += rowH;
                }

                // Rows
                for (var r = 0; r < rows; r++)
                {
                    var ry = top + rowH * (r + 1);
                    sb.Append(HLine(0, el.W, ry + rowH));
                    if (selectable)
                        sb.Append(Rect(10, ry + rowH / 2 - 6, 12, 12, FillDark, Border, 2));
                    for (var c = 0; c < cols.Length; c++)
                    {
                        if (c > 0 || selectable) sb.Append(VLine(colOffset + c * colW, ry, ry + rowH));
                        sb.Append(Rect(colOffset + c * colW + 6, ry + rowH / 2 - 4, colW * 0.65, 8, FillDark, "none", 2));
                    }
                }

                if (isEmpty)
                {
                    sb.Append(Text(emptyTitle, el.W / 2, top + tableH / 2 - 8, 12, ColorMuted, "middle", "500"));
                    sb.Append(Text("No data available", el.W / 2, top + tableH / 2 + 10, 10, ColorLight, "middle"));
                }

                // Pagination
                if (showPagination && scrollMode == "pagination")
                {
                    sb.Append(HLine(0, el.W, el.H - paginH));
                    sb.Append(Text("← 1  2  3  4  5  →", el.W / 2, el.H - paginH / 2, 10, ColorMuted, "middle"));
                }
                else if (scrollMode == "virtualized")
                {
                    sb.Append(Text("↕ Virtualized", el.W - 50, el.H - 12, 9, ColorLight, "middle"));
                }

                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmPagination", "more-horizontal",
            (el, b) =>
            {
                var total = el.Props.GetInt("totalPages", 5);
                var current = el.Props.GetInt("currentPage", 1);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
                sb.Append(TextCentred($"← {Enumerable.Range(1, Math.Min(total, 5)).Select(p => p == current ? $"[{p}]" : p.ToString()).Aggregate((a, x) => a + "  " + x)}  →", el.W, el.H, 10, ColorMuted));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmBulkActionBar", "check-square",
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
            });

        yield return DefFromSchema("TmColumnFilter", "filter",
            (el, b) =>
            {
                var columnName = el.Props.GetString("columnName", "Name");
                var filterType = el.Props.GetString("filterType", "text");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Rect(0, 0, el.W, 28, FillDark, "none", 6));
                sb.Append(HLine(0, el.W, 28));
                sb.Append(Text($"Filter: {columnName}", 8, 14, 10, ColorText, "start", "500"));
                sb.Append(Rect(8, 36, el.W - 16, 28, Fill, Border, 4));
                sb.Append(Text(filterType == "select" ? "Select..." : "Contains...", 14, 50, 10, ColorLight));
                sb.Append(Rect(8, el.H - 34, 76, 24, FillAccent, "#93c5fd", 4));
                sb.Append(Text("Apply", 46, el.H - 22, 9, Accent, "middle"));
                sb.Append(Rect(96, el.H - 34, 76, 24, Fill, Border, 4));
                sb.Append(Text("Clear", 134, el.H - 22, 9, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmColumnPicker", "columns",
            (el, b) =>
            {
                var columns = el.Props.GetStringList("columns");
                if (columns.Length == 0) columns = ["ID", "Name", "Email", "Status"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Rect(0, 0, el.W, 26, FillDark, "none", 6));
                sb.Append(HLine(0, el.W, 26));
                sb.Append(Text("Columns", 8, 13, 10, ColorText, "start", "500"));
                for (var i = 0; i < columns.Length && i < 5; i++)
                {
                    var y = 32 + i * 24;
                    sb.Append(Rect(8, y, 12, 12, FillDark, Border, 2));
                    sb.Append(Text(columns[i], 26, y + 6, 10, ColorText));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmViewManager", "save",
            (el, b) =>
            {
                var viewName = el.Props.GetString("viewName", "Default view");
                var showSave = el.Props.GetBool("showSaveButton", true);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, 130, el.H, Fill, Border, 4));
                sb.Append(Text(viewName, 10, el.H / 2, 10, ColorText));
                sb.Append(Text("▾", 118, el.H / 2, 10, ColorMuted, "middle"));
                if (showSave)
                {
                    sb.Append(Rect(138, 4, 60, 32, FillAccent, "#93c5fd", 4));
                    sb.Append(Text("Save", 168, el.H / 2, 10, Accent, "middle"));
                }
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FEEDBACK
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Feedback()
    {
        yield return DefFromSchema("TmAlert", "alert-circle",
            (el, b) =>
            {
                var msg = el.Props.GetString("message", "This is an alert message.");
                var title = el.Props.GetString("title");
                var variant = el.Props.GetString("variant", "info");
                var visualVariant = el.Props.GetString("visualVariant", "soft");
                var iconOverride = el.Props.GetString("icon");
                var h = string.IsNullOrEmpty(title) ? 56.0 : 72.0;
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
                var strong = variant switch
                {
                    "success" => "#16a34a", "warning" => "#ca8a04",
                    "danger" or "error" => "#dc2626", _ => Accent
                };
                var sb = new StringBuilder();
                if (visualVariant == "filled")
                {
                    fill = strong;
                    border = strong;
                }
                else if (visualVariant == "outlined")
                {
                    fill = "none";
                }
                var textColor = visualVariant == "filled" ? "white" : ColorText;
                var iconColor = visualVariant == "filled" ? "white" : strong;
                sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='{F(h)}' rx='6' fill='{fill}' stroke='{border}' stroke-width='{F(visualVariant == "outlined" ? 1.5 : 1)}'></rect>");
                if (visualVariant == "filled")
                    sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='{F(h)}' rx='6' fill='{strong}' opacity='0.15'></rect>");
                var icon = !string.IsNullOrEmpty(iconOverride) ? iconOverride : (variant == "success" ? "check" : (variant == "danger" ? "alert-circle" : "info"));
                sb.Append(Icon(icon, 18, h / 2, 16));
                // Recolor icon by drawing a colored circle behind or overlay? We'll just use generic icon + colored text for simplicity
                if (!string.IsNullOrEmpty(title))
                {
                    sb.Append(Text(title, 38, 22, 12, textColor, "start", "600"));
                    sb.Append(Text(msg, 38, 46, 10, visualVariant == "filled" ? "rgba(255,255,255,0.9)" : ColorMuted));
                }
                else
                {
                    sb.Append(Text(msg, 38, h / 2, 11, textColor));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmModal", "layers",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Modal Title");
                var showFooter = el.Props.GetBool("showFooter", true);
                var showOkButton = el.Props.GetBool("showOkButton", true);
                var showCancelButton = el.Props.GetBool("showCancelButton", true);
                var okButtonText = el.Props.GetString("okButtonText", "OK");
                var cancelButtonText = el.Props.GetString("cancelButtonText", "Cancel");
                var okButtonVariant = el.Props.GetString("okButtonVariant", "primary");
                // Size prop affects inner dialog proportions (ratio of modal area used by the dialog)
                var dialogScale = el.Props.GetString("size", "medium") switch
                {
                    "small"      => 0.55,
                    "large"      => 0.88,
                    "xLarge"     => 0.96,
                    "fullscreen" => 1.00,
                    _            => 0.72   // medium
                };
                var (okFill, okBorder, okText) = okButtonVariant switch
                {
                    "danger"    => ("#fee2e2", "#fca5a5", "#dc2626"),
                    "ghost"     => ("none",     Border,    ColorText),
                    "outline"   => ("none",     "#93c5fd", Accent),
                    "secondary" => (Fill,       Border,    ColorText),
                    _           => (FillAccent, "#93c5fd", ColorText),  // primary
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
                    var btnX = dx + dw - 16;
                    if (showOkButton)
                    {
                        btnX -= 86;
                        sb.Append(Rect(btnX, dy + dh - 40, 78, 28, okFill, okBorder, 4));
                        sb.Append(Text(okButtonText, btnX + 39, dy + dh - 26, 10, okText, "middle"));
                    }
                    if (showCancelButton)
                    {
                        btnX -= 86;
                        sb.Append(Rect(btnX, dy + dh - 40, 78, 28, "none", Border, 4));
                        sb.Append(Text(cancelButtonText, btnX + 39, dy + dh - 26, 10, ColorMuted, "middle"));
                    }
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmDialog", "message-square",
            (el, b) =>
            {
                var title   = el.Props.GetString("title", "Confirm action");
                var msg     = el.Props.GetString("message", "Are you sure you want to proceed?");
                var variant = el.Props.GetString("variant", "info");
                var (headerFill, headerBorder, iconStr) = variant switch
                {
                    "success" => ("#dcfce7", "#86efac", "✓"),
                    "warning" => ("#fef9c3", "#fde047", "⚠"),
                    "error"   => ("#fee2e2", "#fca5a5", "✕"),
                    _         => (FillAccent, "#93c5fd", "ⓘ"),   // info
                };
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8, 1.5));
                sb.Append(Rect(0, 0, el.W, 40, headerFill, "none", 8));
                sb.Append(Rect(0, 32, el.W, 8, headerFill, "none", 0));
                sb.Append(HLine(0, el.W, 40, headerBorder));
                sb.Append(Text(iconStr, 14, 20, 13, headerBorder, "middle"));
                sb.Append(Text(title, 30, 20, 12, ColorText, "start", "600"));
                sb.Append(Text(msg, 14, 72, 11, ColorMuted));
                sb.Append(HLine(0, el.W, el.H - 48));
                sb.Append(Rect(el.W - 86, el.H - 36, 74, 26, FillAccent, "#93c5fd", 4));
                sb.Append(Text("OK", el.W - 49, el.H - 23, 10, Accent, "middle"));
                sb.Append(Rect(el.W - 166, el.H - 36, 74, 26, Fill, Border, 4));
                sb.Append(Text("Cancel", el.W - 129, el.H - 23, 10, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmTooltip", "info",
            (el, b) =>
            {
                var text      = el.Props.GetString("text", "Tooltip text");
                var placement = el.Props.GetString("placement", "top");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, "#1f2937", "#1f2937", 4));
                sb.Append(TextCentred(text, el.W, el.H, 10, "white"));
                // Arrow points away from the tooltip box in the direction of the target
                var arrow = placement switch
                {
                    "bottom" => $"<polygon points='{F(el.W/2-5)},0 {F(el.W/2+5)},0 {F(el.W/2)},{F(-6)}' fill='#1f2937'></polygon>",
                    "left"   => $"<polygon points='{F(el.W)},{F(el.H/2-5)} {F(el.W)},{F(el.H/2+5)} {F(el.W+6)},{F(el.H/2)}' fill='#1f2937'></polygon>",
                    "right"  => $"<polygon points='0,{F(el.H/2-5)} 0,{F(el.H/2+5)} {F(-6)},{F(el.H/2)}' fill='#1f2937'></polygon>",
                    _        => $"<polygon points='{F(el.W/2-5)},{F(el.H)} {F(el.W/2+5)},{F(el.H)} {F(el.W/2)},{F(el.H+6)}' fill='#1f2937'></polygon>",  // top
                };
                sb.Append(arrow);
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmPopover", "message-circle",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Popover");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6, 1.5));
                sb.Append(HLine(0, el.W, 32));
                sb.Append(Text(title, 10, 16, 11, ColorText, "start", "500"));
                sb.Append($"<polygon points='{F(el.W / 2 - 6)},0 {F(el.W / 2 + 6)},0 {F(el.W / 2)},{F(-7)}' fill='{Fill}' stroke='{Border}' stroke-width='1'></polygon>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmToastContainer", "bell",
            (el, b) =>
            {
                var position = el.Props.GetString("position", "topRight");
                var maxVisible = el.Props.GetInt("maxVisible", 3);
                var sb = new StringBuilder();
                var toastH = 36.0;
                var gap = 8.0;
                var visible = Math.Min(maxVisible, 3);
                for (var i = 0; i < visible; i++)
                {
                    var y = i * (toastH + gap);
                    sb.Append(Rect(0, y, el.W, toastH, Fill, Border, 6, 1.5));
                    sb.Append(Icon("info", 16, y + toastH / 2, 14));
                    sb.Append(Text($"Toast message {i + 1}", 32, y + toastH / 2, 11));
                    sb.Append(Icon("x", el.W - 16, y + toastH / 2, 12));
                    sb.Append($"<rect x='0' y='{F(y + toastH - 3)}' width='{F(el.W)}' height='3' rx='0' fill='{FillAccent}'></rect>");
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmProgressBar", "bar-chart-2",
            (el, b) =>
            {
                var value = el.Props.GetDouble("value", 60);
                var max = el.Props.GetDouble("max", 100);
                var size = el.Props.GetString("size", "md");
                var variant = el.Props.GetString("variant", "default");
                var indeterminate = el.Props.GetBool("indeterminate", false);
                var striped = el.Props.GetBool("striped", false);
                var showLabel = el.Props.GetBool("showLabel", false);
                var pct = Math.Clamp(max > 0 ? value / max * 100 : 0, 0, 100);

                var trackH = size switch { "sm" => 8.0, "lg" => 24.0, _ => 16.0 };
                var y = (el.H - trackH) / 2;
                var rx = trackH / 2;

                var fillColor = variant switch
                {
                    "success" => "#22c55e",
                    "warning" => "#f59e0b",
                    "error" => "#ef4444",
                    "gradient" => "url(#tm-pg-grad)",
                    _ => Accent
                };

                double fillW = 0, segW1 = 0, segW2 = 0, segX1 = 0, segX2 = 0;
                if (indeterminate)
                {
                    segW1 = el.W * 0.30;
                    segW2 = el.W * 0.25;
                    segX1 = 0.0;
                    segX2 = el.W * 0.50;
                }
                else
                {
                    fillW = el.W * (pct / 100.0);
                }

                var sb = new StringBuilder();
                if (showLabel)
                    sb.Append(Text($"{pct:F0}%", el.W + 8, el.H / 2, 11, ColorText, "start", "500"));

                sb.Append($"<rect x='0' y='{F(y)}' width='{F(el.W)}' height='{F(trackH)}' rx='{F(rx)}' fill='{FillDark}' stroke='none'></rect>");

                if (variant == "gradient")
                {
                    var gradId = "pg" + Guid.NewGuid().ToString("N")[..8];
                    sb.Append($"<defs><linearGradient id='{gradId}' x1='0%' y1='0%' x2='100%' y2='0%'><stop offset='0%' stop-color='{Accent}'/><stop offset='100%' stop-color='#8b5cf6'/></linearGradient></defs>");
                    fillColor = $"url(#{gradId})";
                }

                if (indeterminate)
                {
                    sb.Append($"<rect x='{F(segX1)}' y='{F(y)}' width='{F(segW1)}' height='{F(trackH)}' rx='{F(rx)}' fill='{fillColor}'></rect>");
                    sb.Append($"<rect x='{F(segX2)}' y='{F(y)}' width='{F(segW2)}' height='{F(trackH)}' rx='{F(rx)}' fill='{fillColor}'></rect>");
                }
                else
                {
                    sb.Append($"<rect x='0' y='{F(y)}' width='{F(fillW)}' height='{F(trackH)}' rx='{F(rx)}' fill='{fillColor}'></rect>");
                }

                if (striped)
                {
                    var patId = "pgst" + Guid.NewGuid().ToString("N")[..8];
                    sb.Append($"<defs><pattern id='{patId}' width='10' height='10' patternUnits='userSpaceOnUse' patternTransform='rotate(45)'><rect width='5' height='10' fill='rgba(255,255,255,0.35)'></rect></pattern></defs>");
                    if (indeterminate)
                    {
                        sb.Append($"<rect x='{F(segX1)}' y='{F(y)}' width='{F(segW1)}' height='{F(trackH)}' rx='{F(rx)}' fill='url(#{patId})'></rect>");
                        sb.Append($"<rect x='{F(segX2)}' y='{F(y)}' width='{F(segW2)}' height='{F(trackH)}' rx='{F(rx)}' fill='url(#{patId})'></rect>");
                    }
                    else
                    {
                        sb.Append($"<rect x='0' y='{F(y)}' width='{F(fillW)}' height='{F(trackH)}' rx='{F(rx)}' fill='url(#{patId})'></rect>");
                    }
                }

                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmSpinner", "loader",
            (el, b) => Svg(b, Icon("spinner", el.W / 2, el.H / 2, Math.Min(el.W, el.H))));

        yield return DefFromSchema("TmSkeleton", "minus",
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
            });

        yield return DefFromSchema("TmAutoSaveIndicator", "save",
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
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NOTIFICATIONS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Notifications()
    {
        yield return DefFromSchema("TmNotificationBell", "bell",
            (el, b) =>
            {
                var unreadCount = el.Props.GetInt("unreadCount", 3);
                var disabled = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (disabled) sb.Append("<g opacity='0.45'>");
                sb.Append(Rect(0, 0, el.W, el.H, "none", "none", 0));
                sb.Append(Icon("bell", el.W / 2, el.H / 2, 20));
                if (unreadCount > 0)
                {
                    var badgeText = unreadCount > 9 ? "9+" : unreadCount.ToString();
                    var badgeW = badgeText.Length == 1 ? 16 : 20;
                    sb.Append(Pill(el.W - badgeW - 4, 4, badgeW, 16, "#ef4444", "none"));
                    sb.Append(Text(badgeText, el.W - badgeW / 2 - 4, 12, 9, "white", "middle", "600"));
                }
                if (disabled) sb.Append("</g>");
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Navigation()
    {
        yield return DefFromSchema("TmTabs", "layout",
            (el, b) =>
            {
                var tabs    = el.Props.GetStringList("tabs");
                if (tabs.Length == 0) tabs = ["Tab 1", "Tab 2", "Tab 3"];
                var active  = el.Props.GetInt("activeTab", 0);
                var variant = el.Props.GetString("variant", "line");
                var sb = new StringBuilder();
                if (variant == "line") sb.Append(HLine(0, el.W, el.H));
                var x = 0.0;
                for (var i = 0; i < tabs.Length; i++)
                {
                    var tw = tabs[i].Length * 7.0 + 24;
                    var isActive = i == active;
                    switch (variant)
                    {
                        case "pill":
                            if (isActive) sb.Append(Pill(x + 2, 4, tw - 4, el.H - 8, FillAccent, "#93c5fd"));
                            break;
                        case "enclosed":
                            sb.Append(Rect(x, 0, tw, el.H, isActive ? Fill : FillDark, Border, 0));
                            if (isActive) // erase bottom border of active tab
                                sb.Append($"<line x1='{F(x + 1)}' y1='{F(el.H)}' x2='{F(x + tw - 1)}' y2='{F(el.H)}' stroke='{Fill}' stroke-width='2'/>");
                            break;
                        default: // line
                            if (isActive)
                            {
                                sb.Append(Rect(x, 0, tw, el.H, FillAccent, "none", 0));
                                sb.Append(HLine(x, x + tw, el.H, Accent));
                            }
                            break;
                    }
                    sb.Append(Text(tabs[i], x + tw / 2, el.H / 2, 11,
                        isActive ? Accent : ColorMuted, "middle", isActive ? "500" : "normal"));
                    x += tw;
                }
                if (variant == "enclosed") sb.Append(HLine(0, el.W, el.H));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmTabPanel", "square",
            (el, b) =>
            {
                var label = el.Props.GetString("label", "Tab content");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Text(label, 12, 18, 12, ColorText, "start", "500"));
                sb.Append(Rect(12, 36, el.W - 24, el.H - 48, FillDark, "none", 4));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmBreadcrumbs", "chevron-right",
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
            });

        yield return DefFromSchema("TmContextMenu", "menu",
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
            });

        yield return DefFromSchema("TmContextMenuItem", "menu",
            (el, b) =>
            {
                var text = el.Props.GetString("text", "Item");
                var disabled = el.Props.GetBool("disabled", false);
                var danger = el.Props.GetBool("danger", false);
                var sb = new StringBuilder();
                if (disabled) sb.Append("<g opacity='0.45'>");
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
                sb.Append(Text(text, 12, el.H / 2, 11, danger ? "#ef4444" : ColorText));
                if (disabled) sb.Append("</g>");
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LAYOUT
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Layout()
    {
        yield return DefFromSchema("TmTopBar", "layout",
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
            });

        yield return DefFromSchema("TmSidebar", "sidebar",
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
            });

        yield return DefFromSchema("TmDrawer", "sidebar",
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
            });

        yield return DefFromSchema("TmSection", "minus",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Section Title");
                var sb = new StringBuilder();
                sb.Append(Text(title, 0, 10, 14, ColorText, "start", "600"));
                sb.Append(HLine(0, el.W, 24));
                sb.Append(DashedRect(el.W, el.H - 32));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmCommandPalette", "command",
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
            });

        yield return DefFromSchema("TmKeyboardShortcutsHelp", "command",
            (el, b) =>
            {
                var shortcuts = el.Props.GetStringList("shortcuts");
                if (shortcuts.Length == 0) shortcuts = ["Ctrl+S → Save", "Ctrl+K → Command Palette", "Esc → Close", "? → Help"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8, 1.5));
                sb.Append(Rect(0, 0, el.W, 40, FillDark, "none", 8));
                sb.Append(HLine(0, el.W, 40));
                sb.Append(Text("Keyboard shortcuts", 14, 20, 12, ColorText, "start", "500"));
                sb.Append(Icon("x", el.W - 16, 20, 12));
                for (var i = 0; i < shortcuts.Length && i < 6; i++)
                {
                    var parts = shortcuts[i].Split(" → ");
                    var key = parts.Length > 0 ? parts[0] : shortcuts[i];
                    var desc = parts.Length > 1 ? parts[1] : "";
                    var y = 56 + i * 34;
                    sb.Append(Rect(14, y, 60, 22, FillDark, Border, 4));
                    sb.Append(Text(key, 44, y + 11, 9, ColorText, "middle", "500"));
                    sb.Append(Text(desc, 84, y + 11, 10, ColorMuted));
                }
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TOOLBAR
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Toolbar()
    {
        yield return DefFromSchema("TmToolbar", "minus",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "");
                var sticky = el.Props.GetBool("sticky", false);
                var sb = new StringBuilder();
                if (sticky) sb.Append(Rect(0, 0, el.W, 2, Accent, "none", 0));
                sb.Append(Rect(0, sticky ? 2 : 0, el.W, el.H - (sticky ? 2 : 0), Fill, Border, 0));
                if (!string.IsNullOrEmpty(title))
                    sb.Append(Text(title, 12, el.H / 2, 13, ColorText, "start", "600"));
                sb.Append(Rect(el.W - 140, 10, 60, 28, FillAccent, "#93c5fd", 4));
                sb.Append(Text("Action", el.W - 110, el.H / 2, 10, Accent, "middle"));
                sb.Append(Rect(el.W - 72, 10, 60, 28, Fill, Border, 4));
                sb.Append(Text("Cancel", el.W - 42, el.H / 2, 10, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmToolbarButton", "square",
            (el, b) =>
            {
                var label = el.Props.GetString("label", "Action");
                var icon = el.Props.GetString("icon");
                var disabled = el.Props.GetBool("disabled");
                var sb = new StringBuilder();
                if (disabled) sb.Append("<g opacity='0.45'>");
                sb.Append(Rect(0, 0, el.W, el.H, "none", "none", 4));
                var textX = el.W / 2;
                if (!string.IsNullOrEmpty(icon))
                {
                    sb.Append(Icon(icon, 12, el.H / 2, 14));
                    textX = el.W / 2 + 8;
                }
                sb.Append(Text(label, textX, el.H / 2, 11, ColorText, "middle"));
                if (disabled) sb.Append("</g>");
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmToolbarDivider", "minus",
            (el, b) =>
            {
                Svg(b, VLine(el.W / 2, 4, el.H - 4, Border));
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FORMS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Forms()
    {
        yield return DefFromSchema("TmFormSection", "layout",
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
            });

        yield return DefFromSchema("TmFormRow", "minus",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Field Label");
                var req = el.Props.GetBool("required");
                var sb = new StringBuilder();
                sb.Append(Text(req ? lbl + " *" : lbl, 0, 10, 11, ColorText, "start", "500"));
                sb.Append(Rect(160, 0, el.W - 160, 36, Fill, Border, 4));
                sb.Append(Text("Value", 168, 18, 10, ColorLight));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmFormField", "edit-3",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                var disabled = el.Props.GetBool("disabled");
                var help = el.Props.GetString("helpText");
                var err = el.Props.GetString("errorMessage");
                var hasError = !string.IsNullOrEmpty(err);
                var h = 36.0;
                var extraY = 0.0;
                var sb = new StringBuilder();
                sb.Append(InputField(el.W, h, lbl, "Enter value...", req, disabled: disabled));
                if (hasError)
                {
                    sb.Append(Text(err, 0, h + 14, 10, "#dc2626"));
                    extraY += 18;
                }
                else if (!string.IsNullOrEmpty(help))
                {
                    sb.Append(Text(help, 0, h + 14, 10, ColorLight));
                    extraY += 18;
                }
                if (hasError)
                {
                    // Red border overlay
                    sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='{F(h)}' rx='4' fill='none' stroke='#fca5a5' stroke-width='1.5'></rect>");
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmInlineEdit", "edit",
            (el, b) =>
            {
                var value = el.Props.GetString("value", "Click to edit");
                var sb = new StringBuilder();
                sb.Append(Text(value, 0, el.H / 2, 11));
                sb.Append(Icon("edit", el.W - 14, el.H / 2, 12));
                sb.Append(HLine(0, el.W - 20, el.H - 2, FillDark));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmValidationSummary", "alert-circle",
            (el, b) =>
            {
                var errors = el.Props.GetStringList("errors");
                if (errors.Length == 0) errors = ["Field is required.", "Invalid email format."];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, "#fee2e2", "#fca5a5", 6));
                for (var i = 0; i < errors.Length && i < 4; i++)
                    sb.Append(Text("• " + errors[i], 10, 16 + i * 18.0, 10, "#dc2626"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmDynamicFormRenderer", "list",
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
            });

        yield return DefFromSchema("TmValidatedField", "check-circle",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Label");
                var req = el.Props.GetBool("required");
                var valid = el.Props.GetBool("valid", true);
                var msg = el.Props.GetString("validationMessage", "");
                var sb = new StringBuilder();
                sb.Append(InputField(el.W - 28, 36, lbl, "Enter value...", req));
                var iconName = valid ? "check" : "x";
                var iconColor = valid ? "#16a34a" : "#dc2626";
                sb.Append(Icon(iconName, el.W - 14, 18, 14));
                // Recolor icon circle by adding a small colored dot behind (simplified)
                if (!string.IsNullOrEmpty(msg))
                    sb.Append(Text(msg, 0, 52, 10, iconColor));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmFormValidationMessage", "alert-circle",
            (el, b) =>
            {
                var msg = el.Props.GetString("message", "Validation message");
                var severity = el.Props.GetString("severity", "error");
                var (color, icon) = severity switch
                {
                    "warning" => ("#ca8a04", "alert-circle"),
                    "info" => (Accent, "info"),
                    _ => ("#dc2626", "x"),
                };
                var sb = new StringBuilder();
                sb.Append(Icon(icon, 10, el.H / 2, 12));
                sb.Append(Text(msg, 26, el.H / 2, 10, color));
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FILES
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Files()
    {
        yield return DefFromSchema("TmFileDropZone", "upload-cloud",
            (el, b) =>
            {
                var lbl = el.Props.GetString("label", "Drop files here or click to upload");
                var sb = new StringBuilder();
                sb.Append(DashedRect(el.W, el.H, 8));
                sb.Append(Icon("upload", el.W / 2, el.H / 2 - 16, 32));
                sb.Append(Text(lbl, el.W / 2, el.H / 2 + 16, 10, ColorMuted, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmAttachmentManager", "paperclip",
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
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHARTS
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Charts()
    {
        yield return DefFromSchema("TmChart", "bar-chart-2",
            (el, b) =>
            {
                var type = el.Props.GetString("type", "bar");
                var title = el.Props.GetString("title", "Chart Title");
                var dataPoints = el.Props.GetInt("dataPoints", 6);
                var showLegend = el.Props.GetBool("showLegend", false);
                var showGrid = el.Props.GetBool("showGrid", false);
                var horizontal = el.Props.GetBool("horizontal", false);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
                sb.Append(Text(title, el.W / 2, 16, 12, ColorText, "middle", "500"));
                var legendH = showLegend ? 24.0 : 0;
                var chartX = 32.0; var chartY = 28.0;
                var chartW = el.W - chartX - 8;
                var chartH = el.H - chartY - 24 - legendH;
                var fills = new[] { FillAccent, FillDark, "#fef9c3", "#dcfce7", "#fee2e2" };

                if (type == "pie" || type == "donut")
                {
                    var cx = el.W / 2; var cy = chartY + chartH / 2;
                    var r = Math.Min(chartW, chartH) / 2 - 8;
                    var angles = new[] { 0.0, 72, 144, 216, 288, 360 };
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
                    if (showGrid)
                    {
                        for (var g = 1; g <= 4; g++)
                            sb.Append(HLine(chartX, chartX + chartW, chartY + chartH * g / 5, FillDark));
                    }
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
                    if (showGrid)
                    {
                        for (var g = 1; g <= 4; g++)
                        {
                            if (horizontal)
                                sb.Append(VLine(chartX + chartW * g / 5, chartY, chartY + chartH, FillDark));
                            else
                                sb.Append(HLine(chartX, chartX + chartW, chartY + chartH * g / 5, FillDark));
                        }
                    }
                    var heights = new[] { 0.6, 0.8, 0.4, 0.9, 0.5, 0.7 };
                    if (horizontal)
                    {
                        var barH = chartH / dataPoints * 0.65;
                        var gap = chartH / dataPoints;
                        for (var i = 0; i < dataPoints; i++)
                        {
                            var bw = heights[i % heights.Length] * chartW;
                            var by = chartY + i * gap + gap * 0.175;
                            sb.Append(Rect(chartX, by, bw, barH, FillAccent, "#93c5fd", 2));
                        }
                    }
                    else
                    {
                        var barW = chartW / dataPoints * 0.65;
                        var gap = chartW / dataPoints;
                        for (var i = 0; i < dataPoints; i++)
                        {
                            var bh = heights[i % heights.Length] * chartH;
                            var bx = chartX + i * gap + gap * 0.175;
                            sb.Append(Rect(bx, chartY + chartH - bh, barW, bh, FillAccent, "#93c5fd", 2));
                        }
                    }
                }
                if (showLegend)
                {
                    var lx = el.W / 2 - (dataPoints * 36) / 2;
                    for (var i = 0; i < Math.Min(dataPoints, 5); i++)
                    {
                        sb.Append(Rect(lx + i * 36, el.H - 20, 10, 10, fills[i % fills.Length], Border, 2));
                        sb.Append(Text($"L{i + 1}", lx + i * 36 + 14, el.H - 15, 8, ColorMuted));
                    }
                }
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // WORKFLOW
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Workflow()
    {
        yield return DefFromSchema("TmWorkflowToolbox", "sidebar",
            (el, b) =>
            {
                var nodes = el.Props.GetStringList("nodes");
                if (nodes.Length == 0) nodes = ["Start", "Task", "Decision", "End"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillDark, Border, 4));
                sb.Append(Text("Toolbox", el.W / 2, 14, 10, ColorText, "middle", "500"));
                sb.Append(HLine(0, el.W, 26));
                for (var i = 0; i < nodes.Length && i < 6; i++)
                {
                    var y = 32 + i * 38;
                    sb.Append(Rect(8, y, el.W - 16, 32, Fill, Border, 4));
                    sb.Append(Text(nodes[i], el.W / 2, y + 16, 10, ColorText, "middle"));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmWorkflowPropertiesPanel", "sliders",
            (el, b) =>
            {
                var title = el.Props.GetString("title", "Properties");
                var nodeType = el.Props.GetString("nodeType", "Task");
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
                sb.Append(Rect(0, 0, el.W, 36, FillDark, "none", 4));
                sb.Append(HLine(0, el.W, 36));
                sb.Append(Text(title, 10, 18, 11, ColorText, "start", "500"));
                sb.Append(Text(nodeType, el.W - 10, 18, 9, ColorMuted, "end"));
                var props = new[] { "Name", "Description", "Assignee", "Due Date" };
                for (var i = 0; i < props.Length; i++)
                {
                    var y = 48 + i * 48;
                    sb.Append(Text(props[i], 10, y + 8, 9, ColorMuted));
                    sb.Append(Rect(10, y + 14, el.W - 20, 28, Fill, Border, 3));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmWorkflowMinimap", "map",
            (el, b) =>
            {
                var scale = el.Props.GetDouble("scale", 0.2);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillDark, Border, 4));
                // Dotted grid background
                for (var gy = 8; gy < el.H; gy += 12)
                    for (var gx = 8; gx < el.W; gx += 12)
                        sb.Append($"<circle cx='{F(gx)}' cy='{F(gy)}' r='1' fill='{Border}'></circle>");
                // Minimap viewport
                var vw = el.W * 0.5;
                var vh = el.H * 0.5;
                sb.Append(Rect(el.W / 2 - vw / 2, el.H / 2 - vh / 2, vw, vh, "none", Accent, 2));
                // Tiny nodes
                sb.Append(Rect(20, 20, 24, 16, FillAccent, Border, 2));
                sb.Append(Rect(el.W - 50, el.H - 40, 24, 16, Fill, Border, 2));
                Svg(b, sb.ToString());
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // COMPLEX
    // ══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<WireframeComponentDef> Complex()
    {
        yield return DefFromSchema("TmTimeline", "git-commit",
            (el, b) =>
            {
                var items = el.Props.GetStringList("items");
                if (items.Length == 0) items = ["Event 1", "Event 2", "Event 3", "Event 4"];
                var orientation = el.Props.GetString("orientation", "vertical");
                var alternate = el.Props.GetBool("alternate", false);
                var isHorizontal = orientation == "horizontal";
                var sb = new StringBuilder();
                if (isHorizontal)
                {
                    var stepW = el.W / items.Length;
                    sb.Append(HLine(0, el.W, el.H / 2, FillDark));
                    for (var i = 0; i < items.Length; i++)
                    {
                        var cx = i * stepW + stepW / 2;
                        sb.Append($"<circle cx='{F(cx)}' cy='{F(el.H / 2)}' r='6' fill='{(i == 0 ? FillAccent : FillDark)}' stroke='{(i == 0 ? "#93c5fd" : Border)}' stroke-width='1.5'></circle>");
                        var textY = alternate && i % 2 == 1 ? el.H / 2 + 20 : el.H / 2 - 10;
                        sb.Append(Text(items[i], cx, textY, 10, ColorText, "middle"));
                        sb.Append(Text($"T{i + 1}", cx, textY + (alternate && i % 2 == 1 ? -24 : 14), 9, ColorLight, "middle"));
                    }
                }
                else
                {
                    var rowH = el.H / items.Length;
                    var lineX = alternate ? el.W / 2 : 20;
                    sb.Append(VLine(lineX, 0, el.H, FillDark));
                    for (var i = 0; i < items.Length; i++)
                    {
                        var cy = i * rowH + rowH / 2;
                        sb.Append($"<circle cx='{F(lineX)}' cy='{F(cy)}' r='6' fill='{(i == 0 ? FillAccent : FillDark)}' stroke='{(i == 0 ? "#93c5fd" : Border)}' stroke-width='1.5'></circle>");
                        if (alternate && i % 2 == 1)
                        {
                            sb.Append(Text(items[i], lineX - 14, cy, 10, ColorText, "end"));
                            sb.Append(Text($"Day {i + 1}", lineX - 14, cy - 14, 9, ColorLight, "end"));
                        }
                        else
                        {
                            sb.Append(Text(items[i], lineX + 14, cy, 10, ColorText));
                            sb.Append(Text($"Day {i + 1}", el.W - 10, cy, 9, ColorLight, "end"));
                        }
                    }
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmStepper", "list",
            (el, b) =>
            {
                var steps = el.Props.GetStringList("steps");
                if (steps.Length == 0) steps = ["Step 1", "Step 2", "Step 3", "Step 4"];
                var activeStep = el.Props.GetInt("activeStep", 1);
                var orientation = el.Props.GetString("orientation", "horizontal");
                var isVertical = orientation == "vertical";
                var sb = new StringBuilder();
                if (isVertical)
                {
                    var stepH = el.H / steps.Length;
                    for (var i = 0; i < steps.Length; i++)
                    {
                        var cy = i * stepH + stepH / 2;
                        var isDone = i < activeStep;
                        var isActive = i == activeStep;
                        var fill = isDone ? FillAccent : isActive ? Fill : FillDark;
                        var border2 = isDone || isActive ? "#93c5fd" : Border;
                        sb.Append($"<circle cx='20' cy='{F(cy)}' r='12' fill='{fill}' stroke='{border2}' stroke-width='{(isActive ? "2" : "1.5")}'></circle>");
                        sb.Append(Text(isDone ? "✓" : (i + 1).ToString(), 20, cy, 10,
                            isDone ? Accent : isActive ? ColorText : ColorLight, "middle"));
                        sb.Append(Text(steps[i], 40, cy, 9, isActive ? ColorText : ColorMuted));
                        if (i < steps.Length - 1)
                            sb.Append(VLine(20, cy + 14, (i + 1) * stepH + stepH / 2 - 14, isDone ? "#93c5fd" : Border));
                    }
                }
                else
                {
                    var stepW = el.W / steps.Length;
                    for (var i = 0; i < steps.Length; i++)
                    {
                        var cx = i * stepW + stepW / 2;
                        var isDone = i < activeStep;
                        var isActive = i == activeStep;
                        var fill = isDone ? FillAccent : isActive ? Fill : FillDark;
                        var border2 = isDone || isActive ? "#93c5fd" : Border;
                        sb.Append($"<circle cx='{F(cx)}' cy='20' r='12' fill='{fill}' stroke='{border2}' stroke-width='{(isActive ? "2" : "1.5")}'></circle>");
                        sb.Append(Text(isDone ? "✓" : (i + 1).ToString(), cx, 20, 10,
                            isDone ? Accent : isActive ? ColorText : ColorLight, "middle"));
                        sb.Append(Text(steps[i], cx, 42, 9, isActive ? ColorText : ColorMuted, "middle"));
                        if (i < steps.Length - 1)
                            sb.Append(HLine(cx + 14, (i + 1) * stepW + stepW / 2 - 14, 20, isDone ? "#93c5fd" : Border));
                    }
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmScheduler", "calendar",
            (el, b) =>
            {
                var view  = el.Props.GetString("view", "week");
                var title = el.Props.GetString("title", "Schedule");
                var sb    = new StringBuilder();
                var W = el.W; var H = el.H;

                // ── Outer card ────────────────────────────────────────────────
                sb.Append(Rect(0, 0, W, H, Fill, Border, 6));

                // ── Toolbar ───────────────────────────────────────────────────
                sb.Append(Rect(0, 0, W, 44, FillDark, "none", 6));
                sb.Append(Rect(0, 36, W, 8, FillDark, "none", 0));
                sb.Append(HLine(0, W, 44));
                sb.Append(Text(title, 12, 22, 13, ColorText, "start", "500"));
                // Nav arrows
                sb.Append(Text("‹", W / 2 - 36, 22, 14, ColorMuted, "middle"));
                sb.Append(Text("Today", W / 2, 22, 9, ColorText, "middle"));
                sb.Append(Text("›", W / 2 + 36, 22, 14, ColorMuted, "middle"));
                // View buttons (Day / Week / Month / Agenda)
                var views = new[] { "Day", "Week", "Month", "Agenda" };
                var btnW  = 44.0; var btnGap = 2.0;
                var btnsX = W - views.Length * (btnW + btnGap) - 8;
                for (var vi = 0; vi < views.Length; vi++)
                {
                    var vx = btnsX + vi * (btnW + btnGap);
                    var isActive = string.Equals(view, views[vi], StringComparison.OrdinalIgnoreCase);
                    sb.Append(Rect(vx, 10, btnW, 24, isActive ? FillAccent : Fill, isActive ? "#93c5fd" : Border, 3));
                    sb.Append(Text(views[vi], vx + btnW / 2, 22, 9, isActive ? Accent : ColorMuted, "middle"));
                }

                var contentY = 44.0;

                switch (view.ToLower())
                {
                    // ── DAY VIEW ─────────────────────────────────────────────
                    case "day":
                    {
                        var timeW   = 44.0;
                        var bodyH   = H - contentY;
                        var hours   = 9;
                        var rowH    = bodyH / hours;
                        var labels  = new[] { "8:00","9:00","10:00","11:00","12:00","13:00","14:00","15:00","16:00" };
                        // Day header
                        sb.Append(Rect(timeW, contentY, W - timeW, 24, FillDark, "none", 0));
                        sb.Append(HLine(0, W, contentY + 24));
                        sb.Append(Text("Monday 13", timeW + (W - timeW) / 2, contentY + 12, 11, ColorText, "middle", "500"));
                        contentY += 24;
                        bodyH    -= 24;
                        rowH      = bodyH / hours;
                        // Time labels + rows
                        for (var r = 0; r < hours; r++)
                        {
                            var ry = contentY + r * rowH;
                            sb.Append(HLine(0, W, ry));
                            sb.Append(Text(labels[r], timeW - 4, ry + 8, 8, ColorMuted, "end"));
                        }
                        sb.Append(HLine(0, W, H));
                        sb.Append(VLine(timeW, contentY, H));
                        // Sample events
                        sb.Append(Rect(timeW + 4, contentY + rowH * 1.5, W - timeW - 8, rowH * 1.5, FillAccent, "#93c5fd", 3));
                        sb.Append(Text("Team standup", timeW + 10, contentY + rowH * 2 + 4, 9, Accent));
                        sb.Append(Rect(timeW + 4, contentY + rowH * 4, W - timeW - 8, rowH * 2, "#dcfce7", "#86efac", 3));
                        sb.Append(Text("Design review", timeW + 10, contentY + rowH * 4 + 12, 9, "#16a34a"));
                        break;
                    }

                    // ── WEEK VIEW ────────────────────────────────────────────
                    case "week":
                    {
                        var timeW  = 44.0;
                        var days   = new[] { "Mon\n13", "Tue\n14", "Wed\n15", "Thu\n16", "Fri\n17", "Sat\n18", "Sun\n19" };
                        var colW   = (W - timeW) / days.Length;
                        var hours  = 8;
                        var headerH = 28.0;
                        // Day headers
                        sb.Append(Rect(timeW, contentY, W - timeW, headerH, FillDark, "none", 0));
                        sb.Append(HLine(0, W, contentY + headerH));
                        for (var d = 0; d < days.Length; d++)
                        {
                            var dx = timeW + d * colW;
                            var isToday = d == 0;
                            if (isToday) sb.Append(Rect(dx, contentY, colW, headerH, FillAccent, "none", 0));
                            sb.Append(Text(days[d].Split('\n')[0], dx + colW / 2, contentY + 9,  8, isToday ? Accent : ColorMuted, "middle"));
                            sb.Append(Text(days[d].Split('\n')[1], dx + colW / 2, contentY + 21, 10, isToday ? Accent : ColorText,  "middle", "500"));
                            sb.Append(VLine(dx, contentY, H));
                        }
                        sb.Append(VLine(timeW + days.Length * colW, contentY, H));
                        contentY += headerH;
                        var bodyH = H - contentY;
                        var rowH  = bodyH / hours;
                        var timeLabels = new[] { "8:00","9:00","10:00","11:00","12:00","13:00","14:00","15:00" };
                        for (var r = 0; r < hours; r++)
                        {
                            var ry = contentY + r * rowH;
                            sb.Append(HLine(0, W, ry));
                            sb.Append(Text(timeLabels[r], timeW - 4, ry + 8, 8, ColorMuted, "end"));
                        }
                        sb.Append(HLine(0, W, H));
                        sb.Append(VLine(timeW, contentY, H));
                        // Sample events
                        sb.Append(Rect(timeW + 2, contentY + rowH * 1, colW - 4, rowH, FillAccent, "#93c5fd", 3));
                        sb.Append(Text("Standup", timeW + colW / 2, contentY + rowH * 1 + rowH / 2, 8, Accent, "middle"));
                        sb.Append(Rect(timeW + colW * 2 + 2, contentY + rowH * 3, colW * 2 - 4, rowH * 1.5, "#dcfce7", "#86efac", 3));
                        sb.Append(Text("Design", timeW + colW * 3, contentY + rowH * 3 + rowH * 0.75, 8, "#16a34a", "middle"));
                        break;
                    }

                    // ── MONTH VIEW ───────────────────────────────────────────
                    case "month":
                    {
                        var days    = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                        var colW    = W / days.Length;
                        var headerH = 24.0;
                        var weeks   = 5;
                        var rowH    = (H - contentY - headerH) / weeks;
                        // Day-of-week header
                        sb.Append(Rect(0, contentY, W, headerH, FillDark, "none", 0));
                        sb.Append(HLine(0, W, contentY + headerH));
                        for (var d = 0; d < days.Length; d++)
                        {
                            sb.Append(VLine(d * colW, contentY, H));
                            sb.Append(Text(days[d], d * colW + colW / 2, contentY + 12, 9, ColorMuted, "middle"));
                        }
                        contentY += headerH;
                        // Week rows with day numbers
                        var dayNum = 1;
                        for (var w = 0; w < weeks; w++)
                        {
                            var wy = contentY + w * rowH;
                            sb.Append(HLine(0, W, wy + rowH));
                            for (var d = 0; d < days.Length; d++)
                            {
                                var cx2 = d * colW;
                                var isToday = w == 0 && d == 0;
                                if (isToday)
                                {
                                    sb.Append($"<circle cx='{F(cx2 + 12)}' cy='{F(wy + 11)}' r='9' fill='{Accent}'></circle>");
                                    sb.Append(Text(dayNum.ToString(), cx2 + 12, wy + 11, 9, "white", "middle", "500"));
                                }
                                else
                                    sb.Append(Text(dayNum.ToString(), cx2 + 8, wy + 11, 9, d >= 5 ? ColorLight : ColorText));
                                dayNum++;
                                // Sample event chips
                                if (w == 0 && d == 1)
                                {
                                    sb.Append(Rect(cx2 + 2, wy + 20, colW - 4, 12, FillAccent, "none", 2));
                                    sb.Append(Text("Meeting", cx2 + colW / 2, wy + 26, 8, Accent, "middle"));
                                }
                                if (w == 1 && d == 3)
                                {
                                    sb.Append(Rect(cx2 + 2, wy + 20, colW - 4, 12, "#dcfce7", "none", 2));
                                    sb.Append(Text("Review", cx2 + colW / 2, wy + 26, 8, "#16a34a", "middle"));
                                }
                            }
                        }
                        break;
                    }

                    // ── AGENDA VIEW ──────────────────────────────────────────
                    default: // agenda
                    {
                        var entries = new[]
                        {
                            ("Monday, 13 April",   new[] { ("9:00 – 9:30",  "Team standup",  FillAccent, "#93c5fd", Accent),
                                                            ("11:00 – 12:00", "Design review", "#dcfce7",  "#86efac", "#16a34a") }),
                            ("Tuesday, 14 April",  new[] { ("10:00 – 11:00", "Sprint planning", "#fef9c3", "#fde047", "#ca8a04") }),
                            ("Wednesday, 15 April", new[] { ("14:00 – 15:00", "1:1 with manager", FillAccent, "#93c5fd", Accent) }),
                        };
                        var y = contentY + 8.0;
                        foreach (var (date, events) in entries)
                        {
                            if (y > H - 16) break;
                            // Date heading
                            sb.Append(Text(date, 12, y + 6, 10, ColorText, "start", "600"));
                            sb.Append(HLine(12, W - 12, y + 14, FillDark));
                            y += 20;
                            foreach (var (time, name, evFill, evBorder, evText) in events)
                            {
                                if (y > H - 16) break;
                                sb.Append(Rect(12, y, W - 24, 28, evFill, evBorder, 4));
                                sb.Append(Text(time, 20, y + 14, 9, evText));
                                sb.Append(Text(name, 90, y + 14, 10, evText, "start", "500"));
                                y += 34;
                            }
                            y += 6;
                        }
                        break;
                    }
                }

                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmDashboard", "grid",
            (el, b) =>
            {
                var cols = el.Props.GetInt("columns", 3);
                var rows = el.Props.GetInt("rows", 2);
                var editable = el.Props.GetBool("editable", false);
                var showAddWidget = el.Props.GetBool("showAddWidget", false);
                var gap = 8.0;
                var cellW = (el.W - gap * (cols + 1)) / cols;
                var cellH = (el.H - gap * (rows + 1)) / rows;
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, FillDark, Border, 6));
                var totalWidgets = rows * cols;
                if (showAddWidget) totalWidgets -= 1;
                for (var i = 0; i < totalWidgets; i++)
                {
                    var r = i / cols;
                    var c = i % cols;
                    var wx = gap + c * (cellW + gap);
                    var wy = gap + r * (cellH + gap);
                    sb.Append(Rect(wx, wy, cellW, cellH, Fill, Border, 6));
                    sb.Append(Text($"Widget {i + 1}", wx + cellW / 2, wy + 16, 10, ColorMuted, "middle"));
                    if (editable)
                    {
                        sb.Append(Rect(wx + cellW - 18, wy + 4, 14, 14, FillDark, Border, 2));
                        sb.Append(Text("×", wx + cellW - 11, wy + 11, 8, ColorMuted, "middle"));
                    }
                }
                if (showAddWidget)
                {
                    var lastR = (totalWidgets) / cols;
                    var lastC = (totalWidgets) % cols;
                    var wx = gap + lastC * (cellW + gap);
                    var wy = gap + lastR * (cellH + gap);
                    sb.Append(DashedRect(cellW, cellH, 6));
                    sb.Append($"<g transform='translate({F(wx)},{F(wy)})'>{DashedRect(cellW, cellH, 6)}</g>");
                    sb.Append(Text("+ Add widget", wx + cellW / 2, wy + cellH / 2, 10, ColorMuted, "middle"));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmMarkdownEditor", "edit",
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
            });

        yield return DefFromSchema("TmRichEditorFull", "edit-3",
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
            });

        yield return DefFromSchema("TmRichEditorSimple", "edit-2",
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
            });

        yield return DefFromSchema("TmImageGallery", "image",
            (el, b) =>
            {
                var cols = el.Props.GetInt("columns", 3);
                var count = el.Props.GetInt("itemCount", 6);
                var layout = el.Props.GetString("layout", "grid");
                var gap = 6.0;
                var cellW = (el.W - gap * (cols + 1)) / cols;
                var sb = new StringBuilder();
                var masonryHeights = new[] { 0.9, 1.3, 0.75, 1.1, 0.85, 1.2 };
                var currentY = new double[cols];
                for (var c = 0; c < cols; c++) currentY[c] = gap;
                for (var i = 0; i < count; i++)
                {
                    var c = layout == "masonry" ? Array.IndexOf(currentY, currentY.Min()) : i % cols;
                    var ix = gap + c * (cellW + gap);
                    double iy;
                    double cellH;
                    if (layout == "masonry")
                    {
                        cellH = cellW * masonryHeights[i % masonryHeights.Length];
                        iy = currentY[c];
                        currentY[c] += cellH + gap;
                    }
                    else
                    {
                        var r = i / cols;
                        cellH = cellW * 0.75;
                        iy = gap + r * (cellH + gap);
                    }
                    if (iy + cellH > el.H) break;
                    sb.Append(Rect(ix, iy, cellW, cellH, FillDark, Border, 4));
                    sb.Append(Icon("image", ix + cellW / 2, iy + cellH / 2, Math.Min(cellW, cellH) * 0.45));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmLightbox", "image",
            (el, b) =>
            {
                var imageCount = el.Props.GetInt("imageCount", 8);
                var currentIndex = el.Props.GetInt("currentIndex", 1);
                var sb = new StringBuilder();
                sb.Append($"<rect width='{F(el.W)}' height='{F(el.H)}' fill='rgba(0,0,0,0.85)'></rect>");
                // Main image placeholder
                sb.Append(Rect(el.W / 2 - 200, el.H / 2 - 140, 400, 280, FillDark, Border, 6));
                sb.Append(Icon("image", el.W / 2, el.H / 2, 64));
                // Nav arrows
                sb.Append(Text("‹", 30, el.H / 2, 28, "white", "middle"));
                sb.Append(Text("›", el.W - 30, el.H / 2, 28, "white", "middle"));
                // Counter
                sb.Append(Text($"{currentIndex} / {imageCount}", el.W / 2, 30, 12, "white", "middle"));
                // Thumbnail strip
                var thumbW = 48.0; var thumbGap = 8.0;
                var visible = Math.Min(imageCount, 7);
                var stripW = visible * thumbW + (visible - 1) * thumbGap;
                var stripX = (el.W - stripW) / 2;
                for (var i = 0; i < visible; i++)
                {
                    var tx = stripX + i * (thumbW + thumbGap);
                    var isCurrent = i + 1 == currentIndex;
                    sb.Append(Rect(tx, el.H - 70, thumbW, 48, isCurrent ? FillAccent : FillDark, isCurrent ? "#93c5fd" : Border, 4));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmImportWizard", "upload",
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
            });

        yield return DefFromSchema("TmExportOptions", "download",
            (el, b) =>
            {
                var formats = el.Props.GetStringList("formats");
                if (formats.Length == 0) formats = ["CSV", "Excel", "JSON"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8, 1.5));
                sb.Append(Text("Export options", 16, 24, 13, ColorText, "start", "500"));
                var rowH = 36.0;
                for (var i = 0; i < formats.Length; i++)
                {
                    var y = 52 + i * rowH;
                    sb.Append(Rect(16, y, 16, 16, Fill, Border, 3));
                    sb.Append(Text(formats[i], 40, y + 8, 11));
                }
                sb.Append(Rect(el.W - 100, el.H - 44, 84, 32, FillAccent, "#93c5fd", 4));
                sb.Append(Text("Export", el.W - 58, el.H - 28, 11, Accent, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmImportPreview", "table",
            (el, b) =>
            {
                var rows = el.Props.GetInt("rows", 4);
                var cols = el.Props.GetInt("cols", 4);
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8, 1.5));
                sb.Append(Text("Preview", 16, 24, 13, ColorText, "start", "500"));
                var tableY = 44.0;
                var tableH = el.H - 88;
                var rowH = tableH / (rows + 1);
                var colW = (el.W - 32) / cols;
                // header
                sb.Append(Rect(16, tableY, el.W - 32, rowH, FillDark, Border, 0));
                for (var c = 1; c < cols; c++) sb.Append(VLine(16 + c * colW, tableY, tableY + tableH));
                // rows
                for (var r = 1; r <= rows; r++) sb.Append(HLine(16, el.W - 16, tableY + r * rowH));
                sb.Append(Rect(16, tableY, el.W - 32, tableH, "none", Border, 0));
                sb.Append(Rect(el.W - 100, el.H - 36, 84, 28, FillAccent, "#93c5fd", 4));
                sb.Append(Text("Confirm", el.W - 58, el.H - 22, 10, Accent, "middle"));
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmFilterBuilder", "filter",
            (el, b) =>
            {
                var conditions    = el.Props.GetInt("conditions", 3);
                var groupOperator = el.Props.GetString("groupOperator", "AND");
                var sb = new StringBuilder();

                // Outer card
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));

                // AND / OR toggle in top-left corner
                var togW = 56.0; var togH = 22.0; var togY = 10.0;
                sb.Append(Rect(12, togY, togW, togH, FillDark, Border, 4));
                var andActive = groupOperator == "AND";
                sb.Append(Rect(12, togY, togW / 2, togH, andActive ? FillAccent : FillDark, andActive ? "#93c5fd" : "none", 4));
                sb.Append(Rect(12 + togW / 2, togY, togW / 2, togH, !andActive ? FillAccent : FillDark, !andActive ? "#93c5fd" : "none", 4));
                sb.Append(Text("AND", 12 + togW / 4,      togY + togH / 2, 9, andActive  ? Accent : ColorMuted, "middle", "500"));
                sb.Append(Text("OR",  12 + togW * 3 / 4,  togY + togH / 2, 9, !andActive ? Accent : ColorMuted, "middle", "500"));

                // Column widths: field (35%), operator (25%), value (rest), gap=6, delete=20, padding=12 each side
                var innerW  = el.W - 24 - 20 - 8;   // total usable minus delete col and right pad
                var colF    = innerW * 0.37;          // field
                var colO    = innerW * 0.25;          // operator
                var colV    = innerW - colF - colO - 12; // value
                var gap     = 6.0;
                var rowH    = 30.0;
                var rowGap  = 22.0;   // gap between rows — must fit the 14px pill with margin
                var startY  = 42.0;

                for (var i = 0; i < conditions && i < 5; i++)
                {
                    var ry = startY + i * (rowH + rowGap);

                    // AND/OR connector pill — centred in the gap between rows
                    if (i > 0)
                    {
                        var pillH = 14.0;
                        var pillY = ry - rowGap / 2 - pillH / 2;   // vertically centred in the gap
                        sb.Append(Pill(12, pillY, togW, pillH, andActive ? FillAccent : FillDark, andActive ? "#93c5fd" : Border));
                        sb.Append(Text(groupOperator, 12 + togW / 2, pillY + pillH / 2, 8,
                            andActive ? Accent : ColorMuted, "middle", "500"));
                    }

                    var x0 = 12.0;
                    // Field selector (dropdown)
                    sb.Append(Rect(x0, ry, colF, rowH, FillDark, Border, 4));
                    sb.Append(Text("Field", x0 + 8, ry + rowH / 2, 10, ColorMuted));
                    sb.Append(ChevronDown(x0 + colF - 14, ry + rowH / 2 - 4));

                    // Operator selector (dropdown)
                    var x1 = x0 + colF + gap;
                    sb.Append(Rect(x1, ry, colO, rowH, FillDark, Border, 4));
                    sb.Append(Text("equals", x1 + 8, ry + rowH / 2, 10, ColorMuted));
                    sb.Append(ChevronDown(x1 + colO - 14, ry + rowH / 2 - 4));

                    // Value input (text field — no chevron)
                    var x2 = x1 + colO + gap;
                    sb.Append(Rect(x2, ry, colV, rowH, Fill, Border, 4));
                    sb.Append(Text("Value…", x2 + 8, ry + rowH / 2, 10, ColorLight));

                    // Delete button
                    sb.Append(Icon("x", el.W - 16, ry + rowH / 2, 10));
                }

                // "+ Add condition" link at bottom
                sb.Append(Text("+ Add condition", 12, el.H - 10, 10, Accent));

                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmActivityLog", "activity",
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
            });

        yield return DefFromSchema("TmActivityComments", "message-circle",
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
            });

        yield return DefFromSchema("TmActivityAttachments", "paperclip",
            (el, b) =>
            {
                var files = el.Props.GetStringList("files");
                if (files.Length == 0) files = ["report.pdf", "image.png", "data.xlsx"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8));
                sb.Append(Text("Attachments", 12, 20, 12, ColorText, "start", "500"));
                var rowH = 28.0;
                for (var i = 0; i < files.Length; i++)
                {
                    var y = 34 + i * rowH;
                    sb.Append(Text("📎", 12, y + 10, 11));
                    sb.Append(Text(files[i], 32, y + 10, 11));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmActivityTimeline", "list-ordered",
            (el, b) =>
            {
                var events = el.Props.GetStringList("events");
                if (events.Length == 0) events = ["Created project", "Added members", "Started sprint"];
                var sb = new StringBuilder();
                sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 8));
                var rowH = 40.0;
                for (var i = 0; i < events.Length; i++)
                {
                    var y = 12 + i * rowH;
                    sb.Append($"<circle cx='{F(12)}' cy='{F(y + 8)}' r='4' fill='{FillAccent}'></circle>");
                    if (i < events.Length - 1) sb.Append(VLine(12, y + 14, y + rowH));
                    sb.Append(Text(events[i], 26, y + 8, 11));
                }
                Svg(b, sb.ToString());
            });

        yield return DefFromSchema("TmTreeView", "git-branch",
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
            });

        yield return DefFromSchema("TmWorkflowDesignerCanvas", "git-branch",
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
            });

        // ── GROUP CONTAINER (internal) ─────────────────────────────────────────
        yield return DefFromSchema("__group__", "folder",
            (el, b) =>
            {
                var label = el.Props.GetString("label", "Group");
                var sb = new StringBuilder();
                // Semi-transparent background with dashed border
                sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='{F(el.H)}' rx='4' fill='{FillAccent}' stroke='{Accent}' stroke-width='1' stroke-dasharray='6 3' opacity='0.25'></rect>");
                // Solid border on top edge (header)
                sb.Append($"<rect x='0' y='0' width='{F(el.W)}' height='22' rx='4' fill='{FillAccent}' stroke='{Accent}' stroke-width='1'></rect>");
                sb.Append($"<rect x='0' y='14' width='{F(el.W)}' height='8' fill='{FillAccent}' stroke='none'></rect>");
                // Label
                sb.Append(Text(label, 8, 11, 10, Accent, "start", "500"));
                // Folder icon
                sb.Append(Icon("folder", el.W - 14, 11, 12));
                Svg(b, sb.ToString());
            });
    }
}
