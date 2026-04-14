using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Provides <see cref="WireframeComponentSchema"/> entries for all built-in Tempo.Blazor
/// components. This is the single source of truth for component metadata (types, categories,
/// default dimensions, and property definitions).
/// <para>
/// In the main <c>Tempo.Blazor</c> package, <c>BuiltInWireframeComponentProvider</c> reads
/// props directly from this class so the two never diverge.
/// </para>
/// <para>
/// API / MCP projects that reference only <c>Tempo.Blazor.Abstractions</c> can use this
/// class directly — no Blazor rendering dependency.
/// </para>
/// </summary>
public sealed class BuiltInComponentSchemas : IWireframeSchemaSource
{
    /// <inheritdoc/>
    public string SourceId => "BuiltIn";

    /// <inheritdoc/>
    public int Priority => 0;

    // ── Size preset dictionaries (shared across button-family) ────────────────

    /// <summary>Size presets for standard buttons.</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> ButtonSizes =
        new Dictionary<string, (double, double)>
        {
            ["xs"] = (80,  24), ["sm"] = (100, 30), ["md"] = (120, 36), ["lg"] = (140, 44),
        };

    /// <summary>Size presets for split buttons.</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> SplitButtonSizes =
        new Dictionary<string, (double, double)>
        {
            ["xs"] = (100, 24), ["sm"] = (120, 30), ["md"] = (140, 36), ["lg"] = (160, 44),
        };

    /// <summary>Size presets for icon-only buttons (square).</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> IconButtonSizes =
        new Dictionary<string, (double, double)>
        {
            ["xs"] = (24, 24), ["sm"] = (28, 28), ["md"] = (36, 36), ["lg"] = (44, 44),
        };

    /// <summary>Size presets for badges.</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> BadgeSizes =
        new Dictionary<string, (double, double)>
        {
            ["sm"] = (48, 18), ["md"] = (60, 22), ["lg"] = (72, 26),
        };

    /// <summary>Size presets for spinners.</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> SpinnerSizes =
        new Dictionary<string, (double, double)>
        {
            ["sm"] = (16, 16), ["md"] = (32, 32), ["lg"] = (48, 48),
        };

    // ── Prop helper ───────────────────────────────────────────────────────────

    private static PropDef P(string name, string display, PropType type,
        object? def = null, string[]? opts = null, string? cat = null, bool req = false)
        => new() { Name = name, DisplayName = display, Type = type,
                   Default = def, Options = opts, Category = cat, IsRequired = req };

    // ── Schema definitions ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IEnumerable<WireframeComponentSchema> GetSchemas()
    {
        foreach (var s in Buttons())       yield return s;
        foreach (var s in Avatars())       yield return s;
        foreach (var s in Icons())         yield return s;
        foreach (var s in Inputs())        yield return s;
        foreach (var s in Tags())          yield return s;
        foreach (var s in Pickers())       yield return s;
        foreach (var s in Dropdowns())     yield return s;
        foreach (var s in DataDisplay())   yield return s;
        foreach (var s in DataTable())     yield return s;
        foreach (var s in Feedback())      yield return s;
        foreach (var s in Notifications()) yield return s;
        foreach (var s in Navigation())    yield return s;
        foreach (var s in Layout())        yield return s;
        foreach (var s in Toolbar())       yield return s;
        foreach (var s in Forms())         yield return s;
        foreach (var s in Files())         yield return s;
        foreach (var s in Charts())        yield return s;
        foreach (var s in Workflow())      yield return s;
        foreach (var s in Complex())       yield return s;
    }

    // ── BUTTONS ───────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Buttons()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmButton", Category = "Buttons", DisplayName = "Button",
            DefaultWidth = 120, DefaultHeight = 36, SizePresets = ButtonSizes,
            Props =
            [
                P("label",    "Label",    PropType.String, "Button",    cat: "Content",    req: true),
                P("variant",  "Variant",  PropType.Enum,   "primary",   cat: "Appearance",
                    opts: ["primary","secondary","ghost","danger","outline","link","default"]),
                P("size",     "Size",     PropType.Enum,   "md",        cat: "Appearance",
                    opts: ["xs","sm","md","lg"]),
                P("icon",     "Icon",     PropType.Icon,   cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,       cat: "Behavior"),
                P("loading",  "Loading",  PropType.Bool,   false,       cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSplitButton", Category = "Buttons", DisplayName = "Split Button",
            DefaultWidth = 140, DefaultHeight = 36, SizePresets = SplitButtonSizes,
            Props =
            [
                P("label",   "Label",   PropType.String, "Action",  cat: "Content"),
                P("variant", "Variant", PropType.Enum,   "primary", cat: "Appearance",
                    opts: ["primary","secondary","ghost","danger"]),
                P("size",    "Size",    PropType.Enum,   "md",      cat: "Appearance",
                    opts: ["xs","sm","md","lg"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCopyButton", Category = "Buttons", DisplayName = "Copy Button",
            DefaultWidth = 36, DefaultHeight = 36, SizePresets = IconButtonSizes,
            Props =
            [
                P("size", "Size", PropType.Enum, "md", cat: "Appearance", opts: ["xs","sm","md","lg"]),
            ]
        };
    }

    // ── AVATARS ───────────────────────────────────────────────────────────────

    /// <summary>Size presets for avatars.</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> AvatarSizes =
        new Dictionary<string, (double, double)>
        {
            ["xs"] = (24, 24), ["sm"] = (32, 32), ["md"] = (40, 40),
            ["lg"] = (48, 48), ["xl"] = (56, 56), ["xxl"] = (64, 64),
        };

    private static IEnumerable<WireframeComponentSchema> Avatars()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmAvatar", Category = "Avatars", DisplayName = "Avatar",
            DefaultWidth = 40, DefaultHeight = 40, SizePresets = AvatarSizes,
            Props =
            [
                P("name",  "Name",  PropType.String, "AB",     cat: "Content"),
                P("size",  "Size",  PropType.Enum,   "md",     cat: "Appearance",
                    opts: ["xs","sm","md","lg","xl","xxl"]),
                P("shape", "Shape", PropType.Enum,   "circle", cat: "Appearance",
                    opts: ["circle","square"]),
                P("color", "Color", PropType.Enum,   "gray",   cat: "Appearance",
                    opts: ["gray","blue","green","purple","red","yellow"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmAvatarGroup", Category = "Avatars", DisplayName = "Avatar Group",
            DefaultWidth = 120, DefaultHeight = 40,
            Props =
            [
                P("count", "Count", PropType.Int,  3,    cat: "State"),
                P("max",   "Max",   PropType.Int,  3,    cat: "Appearance"),
                P("size",  "Size",  PropType.Enum, "md", cat: "Appearance",
                    opts: ["xs","sm","md","lg","xl","xxl"]),
            ]
        };
    }

    // ── ICONS ─────────────────────────────────────────────────────────────────

    /// <summary>Size presets for icons.</summary>
    public static readonly IReadOnlyDictionary<string, (double W, double H)> IconSizes =
        new Dictionary<string, (double, double)>
        {
            ["sm"] = (16, 16), ["md"] = (24, 24), ["lg"] = (32, 32), ["xl"] = (48, 48),
        };

    private static IEnumerable<WireframeComponentSchema> Icons()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmIcon", Category = "Icons", DisplayName = "Icon",
            DefaultWidth = 24, DefaultHeight = 24, SizePresets = IconSizes,
            Props =
            [
                P("name",  "Name",       PropType.String, "circle", cat: "Content"),
                P("svg",   "Custom SVG", PropType.String, "",       cat: "Content"),
                P("size",  "Size",       PropType.Enum,   "md",     cat: "Appearance",
                    opts: ["sm","md","lg","xl"]),
                P("color", "Color",      PropType.Enum,   "gray",   cat: "Appearance",
                    opts: ["gray","blue","green","red","yellow","purple"]),
                P("style", "Style",      PropType.String, "",       cat: "Appearance"),
            ]
        };
    }

    // ── INPUTS ────────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Inputs()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmTextInput", Category = "Inputs", DisplayName = "Text Input",
            DefaultWidth = 240, DefaultHeight = 56,
            Props =
            [
                P("label",       "Label",       PropType.String, "Label",          cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "Enter text...",  cat: "Content"),
                P("type",        "Type",        PropType.Enum,   "text",           cat: "Behavior",
                    opts: ["text","password","email","tel","url"]),
                P("maxLength",   "Max Length",  PropType.Int,    0,                cat: "Behavior"),
                P("required",    "Required",    PropType.Bool,   false,            cat: "Behavior"),
                P("disabled",    "Disabled",    PropType.Bool,   false,            cat: "Behavior"),
                P("readOnly",    "Read Only",   PropType.Bool,   false,            cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTextArea", Category = "Inputs", DisplayName = "Text Area",
            DefaultWidth = 240, DefaultHeight = 100,
            Props =
            [
                P("label",       "Label",       PropType.String, "Label",         cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "Enter text...", cat: "Content"),
                P("rows",        "Rows",        PropType.Int,    3,               cat: "Appearance"),
                P("required",    "Required",    PropType.Bool,   false,           cat: "Behavior"),
                P("disabled",    "Disabled",    PropType.Bool,   false,           cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmNumberInput", Category = "Inputs", DisplayName = "Number Input",
            DefaultWidth = 200, DefaultHeight = 56,
            Props =
            [
                P("label",    "Label",    PropType.String, "Label", cat: "Content"),
                P("min",      "Min",      PropType.Double, cat: "Behavior"),
                P("max",      "Max",      PropType.Double, cat: "Behavior"),
                P("step",     "Step",     PropType.Double, 1.0,     cat: "Behavior"),
                P("required", "Required", PropType.Bool,   false,   cat: "Behavior"),
                P("disabled", "Disabled", PropType.Bool,   false,   cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSearchInput", Category = "Inputs", DisplayName = "Search Input",
            DefaultWidth = 240, DefaultHeight = 36,
            Props =
            [
                P("placeholder", "Placeholder", PropType.String, "Search...", cat: "Content"),
                P("disabled",    "Disabled",    PropType.Bool,   false,       cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCurrencyInput", Category = "Inputs", DisplayName = "Currency Input",
            DefaultWidth = 200, DefaultHeight = 56,
            Props =
            [
                P("label",          "Label",           PropType.String, "Amount", cat: "Content"),
                P("currencySymbol", "Currency Symbol", PropType.String, "Kč",     cat: "Appearance"),
                P("required",       "Required",        PropType.Bool,   false,    cat: "Behavior"),
                P("disabled",       "Disabled",        PropType.Bool,   false,    cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCheckbox", Category = "Inputs", DisplayName = "Checkbox",
            DefaultWidth = 140, DefaultHeight = 20,
            Props =
            [
                P("label",         "Label",         PropType.String, "Checkbox", cat: "Content"),
                P("checked",       "Checked",       PropType.Bool,   false,      cat: "State"),
                P("indeterminate", "Indeterminate", PropType.Bool,   false,      cat: "State"),
                P("disabled",      "Disabled",      PropType.Bool,   false,      cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmRadio", Category = "Inputs", DisplayName = "Radio",
            DefaultWidth = 140, DefaultHeight = 20,
            Props =
            [
                P("label",    "Label",    PropType.String, "Option", cat: "Content"),
                P("checked",  "Checked",  PropType.Bool,   false,    cat: "State"),
                P("disabled", "Disabled", PropType.Bool,   false,    cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmRadioGroup", Category = "Inputs", DisplayName = "Radio Group",
            DefaultWidth = 200, DefaultHeight = 80,
            Props =
            [
                P("label",    "Label",    PropType.String,     "Options", cat: "Content"),
                P("options",  "Options",  PropType.StringList, cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,       false,     cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmToggle", Category = "Inputs", DisplayName = "Toggle",
            DefaultWidth = 100, DefaultHeight = 20,
            Props =
            [
                P("label",    "Label",    PropType.String, "Toggle", cat: "Content"),
                P("checked",  "Checked",  PropType.Bool,   false,    cat: "State"),
                P("disabled", "Disabled", PropType.Bool,   false,    cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmToggleSection", Category = "Inputs", DisplayName = "Toggle Section",
            DefaultWidth = 240, DefaultHeight = 40,
            Props =
            [
                P("label",    "Label",    PropType.String, "Section", cat: "Content"),
                P("expanded", "Expanded", PropType.Bool,   true,      cat: "State"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSelect", Category = "Inputs", DisplayName = "Select",
            DefaultWidth = 200, DefaultHeight = 56,
            Props =
            [
                P("label",       "Label",       PropType.String, "Label",            cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "Select option...", cat: "Content"),
                P("multiple",    "Multiple",    PropType.Bool,   false,              cat: "Behavior"),
                P("clearable",   "Clearable",   PropType.Bool,   false,              cat: "Behavior"),
                P("required",    "Required",    PropType.Bool,   false,              cat: "Behavior"),
                P("disabled",    "Disabled",    PropType.Bool,   false,              cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmMultiSelect", Category = "Inputs", DisplayName = "Multi Select",
            DefaultWidth = 240, DefaultHeight = 56,
            Props =
            [
                P("label",       "Label",       PropType.String, "Label",          cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "Select items...", cat: "Content"),
                P("required",    "Required",    PropType.Bool,   false,            cat: "Behavior"),
                P("disabled",    "Disabled",    PropType.Bool,   false,            cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCascadingSelect", Category = "Inputs", DisplayName = "Cascading Select",
            DefaultWidth = 300, DefaultHeight = 56,
            Props =
            [
                P("label",    "Label",    PropType.String, "Label", cat: "Content"),
                P("levels",   "Levels",   PropType.Int,    2,       cat: "Appearance"),
                P("disabled", "Disabled", PropType.Bool,   false,   cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmFilterableDropdown", Category = "Inputs", DisplayName = "Filterable Dropdown",
            DefaultWidth = 200, DefaultHeight = 56,
            Props =
            [
                P("label",       "Label",       PropType.String, "Label",           cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "Filter...",       cat: "Content"),
                P("disabled",    "Disabled",    PropType.Bool,   false,             cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmEntityPicker", Category = "Inputs", DisplayName = "Entity Picker",
            DefaultWidth = 240, DefaultHeight = 56,
            Props =
            [
                P("label",       "Label",       PropType.String, "Select entity", cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "Choose...",     cat: "Content"),
                P("multiple",    "Multiple",    PropType.Bool,   false,           cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmExpressionEditor", Category = "Inputs", DisplayName = "Expression Editor",
            DefaultWidth = 280, DefaultHeight = 56,
            Props =
            [
                P("label",       "Label",       PropType.String, "Expression",   cat: "Content"),
                P("placeholder", "Placeholder", PropType.String, "{expression}", cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmPasswordStrengthIndicator", Category = "Inputs", DisplayName = "Password Strength",
            DefaultWidth = 240, DefaultHeight = 48,
            Props =
            [
                P("strength", "Strength", PropType.Int, 3, cat: "State"),
            ]
        };
    }

    // ── TAGS ─────────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Tags()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmTagPicker", Category = "Tags", DisplayName = "Tag Picker",
            DefaultWidth = 240, DefaultHeight = 40,
            Props =
            [
                P("tags",        "Tags",         PropType.StringList, cat: "Content"),
                P("allowCreate", "Allow Create", PropType.Bool, false, cat: "Behavior"),
                P("disabled",    "Disabled",     PropType.Bool, false, cat: "Behavior"),
            ]
        };
    }

    // ── PICKERS ───────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Pickers()
    {
        var dateBehavior = new[] {
            P("format",   "Format",   PropType.String, "dd.mm.yyyy", cat: "Appearance"),
            P("min",      "Min",      PropType.String, "",           cat: "Behavior"),
            P("max",      "Max",      PropType.String, "",           cat: "Behavior"),
            P("required", "Required", PropType.Bool,   false,        cat: "Behavior"),
            P("disabled", "Disabled", PropType.Bool,   false,        cat: "Behavior"),
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDatePicker", Category = "Pickers", DisplayName = "Date Picker",
            DefaultWidth = 200, DefaultHeight = 56,
            Props = [P("label", "Label", PropType.String, "Date", cat: "Content"), ..dateBehavior]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDateTimePicker", Category = "Pickers", DisplayName = "Date & Time Picker",
            DefaultWidth = 200, DefaultHeight = 56,
            Props = [P("label", "Label", PropType.String, "Date & Time", cat: "Content"), ..dateBehavior]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTimePicker", Category = "Pickers", DisplayName = "Time Picker",
            DefaultWidth = 160, DefaultHeight = 56,
            Props = [P("label", "Label", PropType.String, "Time", cat: "Content"), ..dateBehavior]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDateRangePicker", Category = "Pickers", DisplayName = "Date Range Picker",
            DefaultWidth = 320, DefaultHeight = 56,
            Props = [P("label", "Label", PropType.String, "Date range", cat: "Content"), ..dateBehavior]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTimeRangePicker", Category = "Pickers", DisplayName = "Time Range Picker",
            DefaultWidth = 280, DefaultHeight = 56,
            Props =
            [
                P("label",    "Label",    PropType.String, "Time range", cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,        cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDateTimeRangePicker", Category = "Pickers", DisplayName = "DateTime Range Picker",
            DefaultWidth = 400, DefaultHeight = 56,
            Props =
            [
                P("label",    "Label",    PropType.String, "DateTime range", cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,            cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTimeInput", Category = "Pickers", DisplayName = "Time Input",
            DefaultWidth = 120, DefaultHeight = 36,
            Props =
            [
                P("disabled", "Disabled", PropType.Bool, false, cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCalendarView", Category = "Pickers", DisplayName = "Calendar View",
            DefaultWidth = 280, DefaultHeight = 260,
            Props =
            [
                P("month",       "Month",        PropType.String, "January", cat: "Content"),
                P("year",        "Year",         PropType.Int,    2025,      cat: "Content"),
                P("selectedDay", "Selected Day", PropType.Int,    15,        cat: "State"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCalendarGrid", Category = "Pickers", DisplayName = "Calendar Grid",
            DefaultWidth = 240, DefaultHeight = 200,
            Props =
            [
                P("month", "Month", PropType.String, "January", cat: "Content"),
                P("year",  "Year",  PropType.Int,    2025,      cat: "Content"),
            ]
        };
    }

    // ── DROPDOWNS ────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Dropdowns()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmDropdown", Category = "Dropdowns", DisplayName = "Dropdown",
            DefaultWidth = 160, DefaultHeight = 36,
            Props =
            [
                P("text",     "Text",     PropType.String, "Options", cat: "Content"),
                P("icon",     "Icon",     PropType.Icon,              cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,     cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDropdownItem", Category = "Dropdowns", DisplayName = "Dropdown Item",
            DefaultWidth = 160, DefaultHeight = 32,
            Props =
            [
                P("label",    "Label",    PropType.String, "Item",  cat: "Content"),
                P("icon",     "Icon",     PropType.Icon,            cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,   cat: "Behavior"),
            ]
        };
    }

    // ── DATA DISPLAY ──────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> DataDisplay()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmDivider", Category = "Data Display", DisplayName = "Divider",
            DefaultWidth = 200, DefaultHeight = 12,
            Props = []
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmText", Category = "Data Display", DisplayName = "Text",
            DefaultWidth = 120, DefaultHeight = 24,
            Props =
            [
                P("text",  "Text",  PropType.String, "Text", cat: "Content"),
                P("align", "Align", PropType.Enum,   "left", cat: "Appearance",
                    opts: ["left","center","right"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCard", Category = "Data Display", DisplayName = "Card",
            DefaultWidth = 280, DefaultHeight = 180,
            Props =
            [
                P("title",               "Title",                PropType.String, "Card Title", cat: "Content"),
                P("showHeader",          "Show Header",          PropType.Bool,   true,         cat: "Appearance"),
                P("showFooter",          "Show Footer",          PropType.Bool,   false,        cat: "Appearance"),
                P("variant",             "Variant",              PropType.Enum,   "default",    cat: "Appearance",
                    opts: ["default","elevated","outlined"]),
                P("headerIcon",          "Header Icon",          PropType.Icon,                 cat: "Content"),
                P("showPrimaryAction",   "Show Primary Action",  PropType.Bool,   true,         cat: "Appearance"),
                P("showSecondaryAction", "Show Secondary Action",PropType.Bool,   true,         cat: "Appearance"),
                P("primaryActionLabel",  "Primary Action Label", PropType.String, "Save",       cat: "Content"),
                P("secondaryActionLabel","Secondary Action Label",PropType.String,"Cancel",     cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmStatCard", Category = "Data Display", DisplayName = "Stat Card",
            DefaultWidth = 160, DefaultHeight = 100,
            Props =
            [
                P("title",         "Title",           PropType.String, "Total Revenue", cat: "Content"),
                P("value",         "Value",           PropType.String, "12 450",        cat: "Content"),
                P("unit",          "Unit",            PropType.String, "Kč",            cat: "Content"),
                P("trend",         "Trend",           PropType.Enum,   "up",            cat: "Appearance",
                    opts: ["up","down","neutral"]),
                P("subValue",      "Sub Value",       PropType.String, "",              cat: "Content"),
                P("subValueColor", "Sub Value Color", PropType.String, "",              cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmBadge", Category = "Data Display", DisplayName = "Badge",
            DefaultWidth = 60, DefaultHeight = 22, SizePresets = BadgeSizes,
            Props =
            [
                P("label",   "Label",   PropType.String, "Badge",   cat: "Content"),
                P("variant", "Variant", PropType.Enum,   "default", cat: "Appearance",
                    opts: ["default","primary","success","danger","warning","info"]),
                P("size",    "Size",    PropType.Enum,   "md",      cat: "Appearance",
                    opts: ["sm","md","lg"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmChip", Category = "Data Display", DisplayName = "Chip",
            DefaultWidth = 80, DefaultHeight = 24,
            Props =
            [
                P("label",     "Label",     PropType.String, "Chip",    cat: "Content"),
                P("removable", "Removable", PropType.Bool,   true,      cat: "Behavior"),
                P("variant",   "Variant",   PropType.Enum,   "default", cat: "Appearance",
                    opts: ["default","primary","success","danger","warning"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmChipGroup", Category = "Data Display", DisplayName = "Chip Group",
            DefaultWidth = 240, DefaultHeight = 32,
            Props = [P("chips", "Chips", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmFilterChip", Category = "Data Display", DisplayName = "Filter Chip",
            DefaultWidth = 120, DefaultHeight = 28,
            Props =
            [
                P("label",     "Label",     PropType.String, "Filter", cat: "Content"),
                P("active",    "Active",    PropType.Bool,   true,     cat: "State"),
                P("removable", "Removable", PropType.Bool,   true,     cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmAccordion", Category = "Data Display", DisplayName = "Accordion",
            DefaultWidth = 280, DefaultHeight = 120,
            Props =
            [
                P("items",    "Items",          PropType.StringList, cat: "Content"),
                P("multiple", "Allow Multiple", PropType.Bool, false, cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmAccordionItem", Category = "Data Display", DisplayName = "Accordion Item",
            DefaultWidth = 280, DefaultHeight = 44,
            Props =
            [
                P("title",    "Title",    PropType.String, "Section", cat: "Content"),
                P("expanded", "Expanded", PropType.Bool,   false,     cat: "State"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmEmptyState", Category = "Data Display", DisplayName = "Empty State",
            DefaultWidth = 280, DefaultHeight = 160,
            Props =
            [
                P("title",       "Title",        PropType.String, "No data",                        cat: "Content"),
                P("description", "Description",  PropType.String, "There is nothing to display here.", cat: "Content"),
                P("actionLabel", "Action Label", PropType.String, "Add item",                       cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmChangeDiff", Category = "Data Display", DisplayName = "Change Diff",
            DefaultWidth = 400, DefaultHeight = 120,
            Props =
            [
                P("oldValue", "Old Value", PropType.String, "Old value", cat: "Content"),
                P("newValue", "New Value", PropType.String, "New value", cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmKanbanBoard", Category = "Data Display", DisplayName = "Kanban Board",
            DefaultWidth = 480, DefaultHeight = 320,
            Props =
            [
                P("columns",      "Columns",       PropType.StringList, cat: "Content"),
                P("showAddCard",  "Show Add Card", PropType.Bool, true, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmMultiViewList", Category = "Data Display", DisplayName = "Multi View List",
            DefaultWidth = 360, DefaultHeight = 280,
            Props =
            [
                P("title",      "Title",       PropType.String, "Items", cat: "Content"),
                P("showSearch", "Show Search", PropType.Bool,   true,    cat: "Appearance"),
            ]
        };
    }

    // ── DATA TABLE ────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> DataTable()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmDataTable", Category = "Data Table", DisplayName = "Data Table",
            DefaultWidth = 600, DefaultHeight = 320,
            Props =
            [
                P("title",           "Title",             PropType.String,     "",    cat: "Content"),
                P("columns",         "Columns",           PropType.StringList, cat: "Content"),
                P("rows",            "Rows",              PropType.Int,        5,     cat: "Appearance"),
                P("showSearch",      "Show Search",       PropType.Bool,       true,  cat: "Appearance"),
                P("showPagination",  "Show Pagination",   PropType.Bool,       true,  cat: "Appearance"),
                P("showBulkActions", "Show Bulk Actions", PropType.Bool,       false, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmPagination", Category = "Data Table", DisplayName = "Pagination",
            DefaultWidth = 240, DefaultHeight = 36,
            Props =
            [
                P("totalPages",  "Total Pages",  PropType.Int, 5, cat: "Content"),
                P("currentPage", "Current Page", PropType.Int, 1, cat: "State"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmBulkActionBar", Category = "Data Table", DisplayName = "Bulk Action Bar",
            DefaultWidth = 400, DefaultHeight = 44,
            Props = [P("selectedCount", "Selected Count", PropType.Int, 3, cat: "State")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmColumnFilter", Category = "Data Table", DisplayName = "Column Filter",
            DefaultWidth = 180, DefaultHeight = 120,
            Props =
            [
                P("columnName", "Column Name", PropType.String, "Name", cat: "Content"),
                P("filterType", "Filter Type", PropType.Enum,   "text", cat: "Appearance",
                    opts: ["text","select","date","number"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmColumnPicker", Category = "Data Table", DisplayName = "Column Picker",
            DefaultWidth = 160, DefaultHeight = 160,
            Props = [P("columns", "Columns", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmViewManager", Category = "Data Table", DisplayName = "View Manager",
            DefaultWidth = 200, DefaultHeight = 40,
            Props =
            [
                P("viewName",       "View Name",        PropType.String, "Default view", cat: "Content"),
                P("showSaveButton", "Show Save Button", PropType.Bool,   true,           cat: "Appearance"),
            ]
        };
    }

    // ── FEEDBACK ──────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Feedback()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmAlert", Category = "Feedback", DisplayName = "Alert",
            DefaultWidth = 400, DefaultHeight = 56,
            Props =
            [
                P("message",       "Message",        PropType.String, "This is an alert message.", cat: "Content"),
                P("title",         "Title",          PropType.String, "",                          cat: "Content"),
                P("variant",       "Variant",        PropType.Enum,   "info",                      cat: "Appearance",
                    opts: ["info","success","warning","danger"]),
                P("visualVariant", "Visual Variant", PropType.Enum,   "soft",                      cat: "Appearance",
                    opts: ["soft","filled","outlined"]),
                P("icon",          "Icon",           PropType.Icon,                                cat: "Content"),
                P("dismissible",   "Dismissible",    PropType.Bool,   true,                        cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmModal", Category = "Feedback", DisplayName = "Modal",
            DefaultWidth = 480, DefaultHeight = 360,
            Props =
            [
                P("title",      "Title",       PropType.String, "Modal Title", cat: "Content"),
                P("showFooter", "Show Footer", PropType.Bool,   true,          cat: "Appearance"),
                P("size",       "Size",        PropType.Enum,   "medium",      cat: "Appearance",
                    opts: ["small","medium","large","xLarge","fullscreen"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDialog", Category = "Feedback", DisplayName = "Dialog",
            DefaultWidth = 320, DefaultHeight = 160,
            Props =
            [
                P("title",   "Title",   PropType.String, "Confirm action", cat: "Content"),
                P("message", "Message", PropType.String, "Are you sure?",  cat: "Content"),
                P("variant", "Variant", PropType.Enum,   "info",           cat: "Appearance",
                    opts: ["info","success","warning","error"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTooltip", Category = "Feedback", DisplayName = "Tooltip",
            DefaultWidth = 120, DefaultHeight = 36,
            Props =
            [
                P("text",      "Text",      PropType.String, "Tooltip text", cat: "Content"),
                P("placement", "Placement", PropType.Enum,   "top",          cat: "Appearance",
                    opts: ["top","bottom","left","right"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmPopover", Category = "Feedback", DisplayName = "Popover",
            DefaultWidth = 200, DefaultHeight = 120,
            Props =
            [
                P("title",     "Title",     PropType.String, "Popover", cat: "Content"),
                P("placement", "Placement", PropType.Enum,   "bottom",  cat: "Appearance",
                    opts: ["top","bottom","left","right"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmProgressBar", Category = "Feedback", DisplayName = "Progress Bar",
            DefaultWidth = 240, DefaultHeight = 16,
            Props =
            [
                P("value",         "Value",         PropType.Double, 60.0,      cat: "State"),
                P("max",           "Max",           PropType.Double, 100.0,     cat: "State"),
                P("size",          "Size",          PropType.Enum,   "md",      cat: "Appearance",
                    opts: ["sm","md","lg"]),
                P("variant",       "Variant",       PropType.Enum,   "default", cat: "Appearance",
                    opts: ["default","success","warning","error","gradient"]),
                P("indeterminate", "Indeterminate", PropType.Bool,   false,     cat: "State"),
                P("striped",       "Striped",       PropType.Bool,   false,     cat: "Appearance"),
                P("showLabel",     "Show Label",    PropType.Bool,   false,     cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSpinner", Category = "Feedback", DisplayName = "Spinner",
            DefaultWidth = 32, DefaultHeight = 32, SizePresets = SpinnerSizes,
            Props = [P("size", "Size", PropType.Enum, "md", cat: "Appearance", opts: ["sm","md","lg"])]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSkeleton", Category = "Feedback", DisplayName = "Skeleton",
            DefaultWidth = 280, DefaultHeight = 80,
            Props =
            [
                P("lines",      "Lines",       PropType.Int,  3,     cat: "Appearance"),
                P("showAvatar", "Show Avatar", PropType.Bool, false, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmToastContainer", Category = "Feedback", DisplayName = "Toast Container",
            DefaultWidth = 320, DefaultHeight = 120,
            Props =
            [
                P("position",   "Position",   PropType.Enum, "topRight", cat: "Appearance",
                    opts: ["topRight","topLeft","bottomRight","bottomLeft"]),
                P("maxVisible", "Max Visible", PropType.Int, 3,          cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmAutoSaveIndicator", Category = "Feedback", DisplayName = "Auto Save Indicator",
            DefaultWidth = 100, DefaultHeight = 24,
            Props =
            [
                P("state", "State", PropType.Enum, "saved", cat: "State",
                    opts: ["saving","saved","error"]),
            ]
        };
    }

    // ── NOTIFICATIONS ─────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Notifications()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmNotificationBell", Category = "Notifications", DisplayName = "Notification Bell",
            DefaultWidth = 48, DefaultHeight = 48,
            Props =
            [
                P("unreadCount", "Unread Count", PropType.Int,  3,     cat: "State"),
                P("disabled",    "Disabled",     PropType.Bool, false, cat: "Behavior"),
            ]
        };
    }

    // ── NAVIGATION ────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Navigation()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmTabs", Category = "Navigation", DisplayName = "Tabs",
            DefaultWidth = 400, DefaultHeight = 40,
            Props =
            [
                P("tabs",      "Tabs",             PropType.StringList, cat: "Content"),
                P("activeTab", "Active Tab Index", PropType.Int,        0,      cat: "State"),
                P("variant",   "Variant",          PropType.Enum,       "line", cat: "Appearance",
                    opts: ["line","pill","enclosed"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmBreadcrumbs", Category = "Navigation", DisplayName = "Breadcrumbs",
            DefaultWidth = 300, DefaultHeight = 24,
            Props = [P("items", "Items", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTabPanel", Category = "Navigation", DisplayName = "Tab Panel",
            DefaultWidth = 400, DefaultHeight = 160,
            Props = [P("label", "Label", PropType.String, "Tab content", cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmContextMenu", Category = "Navigation", DisplayName = "Context Menu",
            DefaultWidth = 160, DefaultHeight = 120,
            Props = [P("items", "Items", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmContextMenuItem", Category = "Navigation", DisplayName = "Context Menu Item",
            DefaultWidth = 160, DefaultHeight = 32,
            Props =
            [
                P("text",     "Text",     PropType.String, "Item",  cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,   cat: "Behavior"),
                P("danger",   "Danger",   PropType.Bool,   false,   cat: "Appearance"),
            ]
        };
    }

    // ── LAYOUT ────────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Layout()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmTopBar", Category = "Layout", DisplayName = "Top Bar",
            DefaultWidth = 800, DefaultHeight = 56,
            Props =
            [
                P("title",             "Title",              PropType.String, "App Name", cat: "Content"),
                P("showSearch",        "Show Search",        PropType.Bool,   true,       cat: "Appearance"),
                P("showNotifications", "Show Notifications", PropType.Bool,   true,       cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSidebar", Category = "Layout", DisplayName = "Sidebar",
            DefaultWidth = 220, DefaultHeight = 400,
            Props =
            [
                P("items",     "Items",     PropType.StringList, cat: "Content"),
                P("collapsed", "Collapsed", PropType.Bool,       false, cat: "State"),
                P("width",     "Width",     PropType.Int,        220,   cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDrawer", Category = "Layout", DisplayName = "Drawer",
            DefaultWidth = 360, DefaultHeight = 400,
            Props =
            [
                P("title",     "Title",     PropType.String, "Drawer", cat: "Content"),
                P("placement", "Placement", PropType.Enum,   "right",  cat: "Appearance",
                    opts: ["left","right"]),
                P("width",     "Width",     PropType.Int,    400,      cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmSection", Category = "Layout", DisplayName = "Section",
            DefaultWidth = 400, DefaultHeight = 160,
            Props = [P("title", "Title", PropType.String, "Section Title", cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmCommandPalette", Category = "Layout", DisplayName = "Command Palette",
            DefaultWidth = 480, DefaultHeight = 320,
            Props = [P("placeholder", "Placeholder", PropType.String, "Type a command...", cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmKeyboardShortcutsHelp", Category = "Layout", DisplayName = "Keyboard Shortcuts",
            DefaultWidth = 360, DefaultHeight = 280,
            Props = [P("shortcuts", "Shortcuts", PropType.StringList, cat: "Content")]
        };
    }

    // ── TOOLBAR ───────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Toolbar()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmToolbar", Category = "Toolbar", DisplayName = "Toolbar",
            DefaultWidth = 600, DefaultHeight = 48,
            Props =
            [
                P("title",  "Title",  PropType.String, "",    cat: "Content"),
                P("sticky", "Sticky", PropType.Bool,   false, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmToolbarButton", Category = "Toolbar", DisplayName = "Toolbar Button",
            DefaultWidth = 80, DefaultHeight = 32,
            Props =
            [
                P("label",    "Label",    PropType.String, "Action", cat: "Content"),
                P("icon",     "Icon",     PropType.Icon,             cat: "Content"),
                P("disabled", "Disabled", PropType.Bool,   false,    cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmToolbarDivider", Category = "Toolbar", DisplayName = "Toolbar Divider",
            DefaultWidth = 1, DefaultHeight = 32,
            Props = []
        };
    }

    // ── FORMS ─────────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Forms()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmFormSection", Category = "Forms", DisplayName = "Form Section",
            DefaultWidth = 500, DefaultHeight = 140,
            Props =
            [
                P("title",       "Title",       PropType.String, "Section",             cat: "Content"),
                P("description", "Description", PropType.String, "Section description.", cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmFormRow", Category = "Forms", DisplayName = "Form Row",
            DefaultWidth = 500, DefaultHeight = 56,
            Props =
            [
                P("label",    "Label",    PropType.String, "Field Label", cat: "Content"),
                P("required", "Required", PropType.Bool,   false,         cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmFormField", Category = "Forms", DisplayName = "Form Field",
            DefaultWidth = 280, DefaultHeight = 64,
            Props =
            [
                P("label",        "Label",        PropType.String, "Label", cat: "Content"),
                P("required",     "Required",     PropType.Bool,   false,   cat: "Behavior"),
                P("disabled",     "Disabled",     PropType.Bool,   false,   cat: "Behavior"),
                P("helpText",     "Help Text",    PropType.String, "",      cat: "Content"),
                P("errorMessage", "Error Message",PropType.String, "",      cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmInlineEdit", Category = "Forms", DisplayName = "Inline Edit",
            DefaultWidth = 200, DefaultHeight = 28,
            Props =
            [
                P("value",       "Value",        PropType.String, "Click to edit", cat: "Content"),
                P("editOnClick", "Edit on Click", PropType.Bool,  true,            cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmValidationSummary", Category = "Forms", DisplayName = "Validation Summary",
            DefaultWidth = 320, DefaultHeight = 100,
            Props = [P("errors", "Errors", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDynamicFormRenderer", Category = "Forms", DisplayName = "Dynamic Form",
            DefaultWidth = 400, DefaultHeight = 240,
            Props = [P("fieldCount", "Field Count", PropType.Int, 4, cat: "Appearance")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmValidatedField", Category = "Forms", DisplayName = "Validated Field",
            DefaultWidth = 280, DefaultHeight = 64,
            Props =
            [
                P("label",             "Label",              PropType.String, "Label", cat: "Content"),
                P("required",          "Required",           PropType.Bool,   false,   cat: "Behavior"),
                P("valid",             "Valid",              PropType.Bool,   true,    cat: "State"),
                P("validationMessage", "Validation Message", PropType.String, "",      cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmFormValidationMessage", Category = "Forms", DisplayName = "Validation Message",
            DefaultWidth = 280, DefaultHeight = 24,
            Props =
            [
                P("message",  "Message",  PropType.String, "Validation message", cat: "Content"),
                P("severity", "Severity", PropType.Enum,   "error",              cat: "Appearance",
                    opts: ["error","warning","info"]),
            ]
        };
    }

    // ── FILES ─────────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Files()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmFileDropZone", Category = "Files", DisplayName = "File Drop Zone",
            DefaultWidth = 300, DefaultHeight = 160,
            Props =
            [
                P("label",    "Label",    PropType.String, "Drop files here or click to upload", cat: "Content"),
                P("accept",   "Accept",   PropType.String, "*/*",  cat: "Behavior"),
                P("multiple", "Multiple", PropType.Bool,   true,   cat: "Behavior"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmAttachmentManager", Category = "Files", DisplayName = "Attachment Manager",
            DefaultWidth = 360, DefaultHeight = 200,
            Props = [P("maxFiles", "Max Files", PropType.Int, 5, cat: "Behavior")]
        };
    }

    // ── CHARTS ────────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Charts()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmChart", Category = "Charts", DisplayName = "Chart",
            DefaultWidth = 400, DefaultHeight = 240,
            Props =
            [
                P("title",      "Title",       PropType.String, "Chart Title", cat: "Content"),
                P("type",       "Type",        PropType.Enum,   "bar",         cat: "Appearance",
                    opts: ["bar","line","pie","donut"]),
                P("dataPoints", "Data Points", PropType.Int,    6,             cat: "Appearance"),
            ]
        };
    }

    // ── WORKFLOW ─────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Workflow()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmWorkflowToolbox", Category = "Workflow", DisplayName = "Workflow Toolbox",
            DefaultWidth = 160, DefaultHeight = 280,
            Props = [P("nodes", "Nodes", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmWorkflowPropertiesPanel", Category = "Workflow", DisplayName = "Properties Panel",
            DefaultWidth = 220, DefaultHeight = 300,
            Props =
            [
                P("title",    "Title",     PropType.String, "Properties", cat: "Content"),
                P("nodeType", "Node Type", PropType.String, "Task",       cat: "Content"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmWorkflowMinimap", Category = "Workflow", DisplayName = "Workflow Minimap",
            DefaultWidth = 160, DefaultHeight = 120,
            Props = [P("scale", "Scale", PropType.Double, 0.2, cat: "Appearance")]
        };
    }

    // ── COMPLEX ───────────────────────────────────────────────────────────────

    private static IEnumerable<WireframeComponentSchema> Complex()
    {
        yield return new WireframeComponentSchema
        {
            Type = "TmTimeline", Category = "Complex", DisplayName = "Timeline",
            DefaultWidth = 300, DefaultHeight = 240,
            Props =
            [
                P("items",       "Items",       PropType.StringList, cat: "Content"),
                P("orientation", "Orientation", PropType.Enum, "vertical", cat: "Appearance",
                    opts: ["vertical","horizontal"]),
                P("alternate",   "Alternate",   PropType.Bool, false, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmStepper", Category = "Complex", DisplayName = "Stepper",
            DefaultWidth = 500, DefaultHeight = 56,
            Props =
            [
                P("steps",       "Steps",            PropType.StringList, cat: "Content"),
                P("activeStep",  "Active Step Index",PropType.Int, 1,    cat: "State"),
                P("orientation", "Orientation",      PropType.Enum, "horizontal", cat: "Appearance",
                    opts: ["horizontal","vertical"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmScheduler", Category = "Complex", DisplayName = "Scheduler",
            DefaultWidth = 700, DefaultHeight = 400,
            Props =
            [
                P("title", "Title", PropType.String, "Schedule", cat: "Content"),
                P("view",  "View",  PropType.Enum,   "week",     cat: "Appearance",
                    opts: ["day","week","month","agenda"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmDashboard", Category = "Complex", DisplayName = "Dashboard",
            DefaultWidth = 800, DefaultHeight = 500,
            Props =
            [
                P("columns",       "Columns",        PropType.Int,  3,     cat: "Appearance"),
                P("rows",          "Rows",           PropType.Int,  2,     cat: "Appearance"),
                P("editable",      "Editable",       PropType.Bool, false, cat: "Behavior"),
                P("showAddWidget", "Show Add Widget",PropType.Bool, false, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmMarkdownEditor", Category = "Complex", DisplayName = "Markdown Editor",
            DefaultWidth = 500, DefaultHeight = 300,
            Props =
            [
                P("placeholder",  "Placeholder",  PropType.String, "Write markdown...", cat: "Content"),
                P("showToolbar",  "Show Toolbar", PropType.Bool,   true,                cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmRichEditorFull", Category = "Complex", DisplayName = "Rich Text Editor (Full)",
            DefaultWidth = 600, DefaultHeight = 320,
            Props = [P("placeholder", "Placeholder", PropType.String, "Start writing...", cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmRichEditorSimple", Category = "Complex", DisplayName = "Rich Text Editor (Simple)",
            DefaultWidth = 400, DefaultHeight = 200,
            Props = [P("placeholder", "Placeholder", PropType.String, "Type here...", cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmImageGallery", Category = "Complex", DisplayName = "Image Gallery",
            DefaultWidth = 400, DefaultHeight = 280,
            Props =
            [
                P("columns",   "Columns",    PropType.Int,  3,      cat: "Appearance"),
                P("itemCount", "Item Count", PropType.Int,  6,      cat: "Appearance"),
                P("layout",    "Layout",     PropType.Enum, "grid", cat: "Appearance",
                    opts: ["grid","masonry","list"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmImportWizard", Category = "Complex", DisplayName = "Import Wizard",
            DefaultWidth = 560, DefaultHeight = 360,
            Props = [P("steps", "Steps", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmFilterBuilder", Category = "Complex", DisplayName = "Filter Builder",
            DefaultWidth = 520, DefaultHeight = 192,
            Props =
            [
                P("conditions",    "Conditions",     PropType.Int,  3,     cat: "Appearance"),
                P("groupOperator", "Group Operator", PropType.Enum, "AND", cat: "Appearance",
                    opts: ["AND","OR"]),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmActivityLog", Category = "Complex", DisplayName = "Activity Log",
            DefaultWidth = 400, DefaultHeight = 280,
            Props = [P("itemCount", "Item Count", PropType.Int, 5, cat: "Appearance")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmActivityComments", Category = "Complex", DisplayName = "Activity Comments",
            DefaultWidth = 400, DefaultHeight = 320,
            Props = [P("commentCount", "Comment Count", PropType.Int, 3, cat: "Appearance")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmActivityAttachments", Category = "Complex", DisplayName = "Activity Attachments",
            DefaultWidth = 320, DefaultHeight = 120,
            Props = [P("files", "Files", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmActivityTimeline", Category = "Complex", DisplayName = "Activity Timeline",
            DefaultWidth = 320, DefaultHeight = 160,
            Props = [P("events", "Events", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmLightbox", Category = "Complex", DisplayName = "Lightbox",
            DefaultWidth = 600, DefaultHeight = 400,
            Props =
            [
                P("imageCount",   "Image Count",   PropType.Int, 8, cat: "Content"),
                P("currentIndex", "Current Index", PropType.Int, 1, cat: "State"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmExportOptions", Category = "Complex", DisplayName = "Export Options",
            DefaultWidth = 360, DefaultHeight = 240,
            Props = [P("formats", "Formats", PropType.StringList, cat: "Content")]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmImportPreview", Category = "Complex", DisplayName = "Import Preview",
            DefaultWidth = 480, DefaultHeight = 260,
            Props =
            [
                P("rows", "Rows",    PropType.Int, 4, cat: "Appearance"),
                P("cols", "Columns", PropType.Int, 4, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmTreeView", Category = "Complex", DisplayName = "Tree View",
            DefaultWidth = 240, DefaultHeight = 200,
            Props =
            [
                P("depth",           "Depth",           PropType.Int,  3,     cat: "Appearance"),
                P("showCheckboxes",  "Show Checkboxes", PropType.Bool, false, cat: "Appearance"),
            ]
        };

        yield return new WireframeComponentSchema
        {
            Type = "TmWorkflowDesignerCanvas", Category = "Complex", DisplayName = "Workflow Designer",
            DefaultWidth = 500, DefaultHeight = 300,
            Props = [P("title", "Title", PropType.String, "Workflow", cat: "Content")]
        };
    }
}
