using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Properties panel for the wireframe editor.
/// Shows editable fields for the currently selected element(s).
///
/// <para>Single-select: all <see cref="PropDef"/> fields grouped by category, plus
/// always-visible Layout (X/Y/W/H) and Element (Type, ZIndex) sections.</para>
///
/// <para>Multi-select: common props across all selected element types, with "Mixed"
/// indicator when values differ. Changes apply to all selected elements.</para>
///
/// <para>All mutations are routed through the cascaded <see cref="WireframeCommandStack"/>
/// when available.</para>
/// </summary>
public partial class TmWireframePropertiesPanel : ComponentBase, IDisposable
{
    // ── DI ────────────────────────────────────────────────────────────────────

    [Inject] private WireframeComponentRegistry _registry { get; set; } = default!;

    // ── Cascaded command stack ────────────────────────────────────────────────

    /// <summary>Command stack cascaded from the parent <see cref="TmWireframeEditor"/>. When present, all mutations are recorded for undo/redo.</summary>
    [CascadingParameter] public WireframeCommandStack? CommandStack { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Document being edited.</summary>
    [Parameter] public WireframeDocument? Document { get; set; }

    /// <summary>Raised after every property change.</summary>
    [Parameter] public EventCallback<WireframeDocument> DocumentChanged { get; set; }

    /// <summary>Currently selected element ids.</summary>
    [Parameter] public string[] SelectedIds { get; set; } = [];

    /// <summary>Currently selected connector ids.</summary>
    [Parameter] public string[] SelectedConnectorIds { get; set; } = [];

    /// <summary>Raised when the user requests copy style from the selected element.</summary>
    [Parameter] public EventCallback OnCopyStyle { get; set; }

    /// <summary>Raised when the user requests paste style to the selected elements.</summary>
    [Parameter] public EventCallback OnPasteStyle { get; set; }

    /// <summary>Raised when the user requests paste size to the selected elements.</summary>
    [Parameter] public EventCallback OnPasteSize { get; set; }

    /// <summary>Prevent all editing interactions.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Additional CSS class on the panel wrapper.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Whether the wireframe clipboard currently holds a copied style.</summary>
    public bool HasClipboardStyle => WireframeClipboard.HasStyle;

    // ── Derived state (recomputed in OnParametersSet) ─────────────────────────

    private List<WireframeElement>     _elements      = [];
    private List<WireframeConnector>   _connectors    = [];
    private WireframeComponentDef?     _def;           // null in multi-type selection
    private List<PropDef>              _commonProps    = [];
    private List<string>               _propCategories = [];
    private bool                       _isMulti;
    private bool                       _sameType;

    // Layout display values (MixedMarker when multi and values differ)
    private string? _layoutX, _layoutY, _layoutW, _layoutH;

    // Validation state: propName → error message
    private readonly Dictionary<string, string> _validationErrors = [];

    // Per-field debounce tokens — each field gets its own CTS so concurrent edits don't cancel each other
    private readonly Dictionary<string, CancellationTokenSource> _debounceCts = new();
    private const int DebounceMs = 350;

    internal const string MixedMarker = "__mixed__";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        _validationErrors.Clear();
        _connectors = [];
        _elements = [];

        if (Document is null) return;

        if (SelectedConnectorIds.Length > 0)
        {
            _connectors = SelectedConnectorIds
                .Select(id => Document.Connectors.FirstOrDefault(c => c.Id == id))
                .Where(c => c is not null)
                .Select(c => c!)
                .ToList();
            return;
        }

        if (SelectedIds.Length == 0) return;

        _elements = SelectedIds
            .Select(id => Document.Elements.FirstOrDefault(e => e.Id == id))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        if (_elements.Count == 0) return;

        _isMulti  = _elements.Count > 1;
        _sameType = _elements.Select(e => e.Type).Distinct().Count() == 1;

        // Resolve definition(s)
        if (_sameType)
        {
            _def = _registry.GetDef(_elements[0].Type);
            _commonProps = _def?.Props.ToList() ?? [];
        }
        else
        {
            // Multi-type: intersect props by Name across all defs
            _def = null;
            var defLists = _elements
                .Select(e => _registry.GetDef(e.Type)?.Props ?? (IReadOnlyList<PropDef>)[])
                .ToList();
            var commonNames = defLists
                .Skip(1)
                .Aggregate(
                    defLists[0].Select(p => p.Name).ToHashSet(),
                    (acc, pl) => { acc.IntersectWith(pl.Select(p => p.Name)); return acc; });
            _commonProps = defLists[0].Where(p => commonNames.Contains(p.Name)).ToList();
        }

        // Categories (props without a category go into "General")
        _propCategories = _commonProps
            .Select(p => p.Category ?? "General")
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        // Layout values
        _layoutX = MixedOrValue(_elements.Select(e => e.X));
        _layoutY = MixedOrValue(_elements.Select(e => e.Y));
        _layoutW = MixedOrValue(_elements.Select(e => e.W));
        _layoutH = MixedOrValue(_elements.Select(e => e.H));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MixedOrValue(IEnumerable<double> values)
    {
        var list = values.Select(v => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                         .Distinct()
                         .ToList();
        return list.Count == 1 ? list[0] : MixedMarker;
    }

    private IEnumerable<PropDef> GetPropsForCategory(string cat)
        => _commonProps.Where(p => (p.Category ?? "General") == cat);

    private string GetCategoryName(string cat) => cat switch
    {
        "General"   => Loc["TmWireframeProps_SectionGeneral"],
        "Content"   => Loc["TmWireframeProps_CatContent"],
        "Appearance"=> Loc["TmWireframeProps_CatAppearance"],
        "Behavior"  => Loc["TmWireframeProps_CatBehavior"],
        "State"     => Loc["TmWireframeProps_CatState"],
        _           => cat,
    };

    /// <summary>True when the prop has different values across the selected elements.</summary>
    internal bool IsMixedValue(string propName)
        => IsMixedValue(_elements, propName);

    /// <summary>Returns the string display value for a prop (first element's value or default).</summary>
    internal string GetDisplayValue(string propName, PropDef prop)
        => GetDisplayValue(_elements, propName, prop);

    // ── Static helpers (testable without a Blazor host) ───────────────────────

    /// <summary>True when the prop has different values across <paramref name="elements"/>.</summary>
    internal static bool IsMixedValue(IList<WireframeElement> elements, string propName)
    {
        if (elements.Count <= 1) return false;
        var values = elements
            .Select(e => e.Props.TryGetValue(propName, out var v) ? v.ToString() : null)
            .Distinct()
            .ToList();
        return values.Count > 1;
    }

    /// <summary>Returns the string display value for a prop from <paramref name="elements"/>[0].</summary>
    internal static string GetDisplayValue(IList<WireframeElement> elements, string propName, PropDef prop)
    {
        if (elements.Count == 0) return prop.Default?.ToString() ?? "";
        var el = elements[0];
        if (!el.Props.TryGetValue(propName, out var je))
            return prop.Default?.ToString() ?? "";

        return je.ValueKind switch
        {
            JsonValueKind.String => je.GetString() ?? "",
            JsonValueKind.Number => je.ToString(),
            JsonValueKind.True   => "true",
            JsonValueKind.False  => "false",
            JsonValueKind.Array  => string.Join(", ", je.EnumerateArray()
                                        .Select(i => i.GetString() ?? i.ToString())),
            _                    => je.ToString()
        };
    }

    // ── Debounce helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Schedules <paramref name="apply"/> after <see cref="DebounceMs"/> of inactivity on
    /// <paramref name="key"/>. Each key has its own CTS so concurrent field edits don't
    /// cancel each other.
    /// </summary>
    private async Task DebounceApply(string key, Func<Task> apply)
    {
        if (_debounceCts.TryGetValue(key, out var old)) { old.Cancel(); old.Dispose(); }
        var cts = new CancellationTokenSource();
        _debounceCts[key] = cts;
        try
        {
            await Task.Delay(DebounceMs, cts.Token);
            if (!cts.IsCancellationRequested) await apply();
        }
        catch (OperationCanceledException) { /* superseded by newer keystroke on the same field */ }
    }

    // ── Change handlers ───────────────────────────────────────────────────────

    // Layout and ZIndex use @oninput → debounce; silent on partial/invalid input
    private Task OnLayoutChanged(string field, ChangeEventArgs e)
    {
        if (Document is null) return Task.CompletedTask;
        var raw = e.Value?.ToString() ?? "";
        if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
            return Task.CompletedTask;   // user still typing (e.g. "-", "1.")

        return DebounceApply("layout_" + field, async () =>
        {
            foreach (var el in _elements)
            {
                double ax = el.X, ay = el.Y, aw = el.W, ah = el.H;
                switch (field)
                {
                    case "x": ax = val; break;
                    case "y": ay = val; break;
                    case "w": aw = Math.Max(1, val); break;
                    case "h": ah = Math.Max(1, val); break;
                }
                if (CommandStack is not null)
                    CommandStack.Push(new ResizeElementCommand(Document, el.Id, el.X, el.Y, el.W, el.H, ax, ay, aw, ah));
                else
                    { el.X = ax; el.Y = ay; el.W = aw; el.H = ah; }
            }
            await NotifyChanged();
        });
    }

    private Task OnZIndexChanged(ChangeEventArgs e)
    {
        if (Document is null || _elements.Count == 0) return Task.CompletedTask;
        if (!int.TryParse(e.Value?.ToString(), out var z)) return Task.CompletedTask;
        var el = _elements[0];
        var before = el.ZIndex;

        return DebounceApply("zindex", async () =>
        {
            if (CommandStack is not null)
                CommandStack.Push(new ZIndexCommand(Document, el.Id, before, z, Loc["TmWireframeProps_CommandChangeZIndex"]));
            else
                el.ZIndex = z;
            await NotifyChanged();
        });
    }

    // String props: @oninput → validate immediately, debounce commit
    private Task OnStringPropChanged(string propName, ChangeEventArgs e,
        string? validationRegex = null)
    {
        if (Document is null) return Task.CompletedTask;
        var raw = e.Value?.ToString() ?? "";

        _validationErrors.Remove(propName);
        if (!string.IsNullOrEmpty(validationRegex) && !string.IsNullOrEmpty(raw))
        {
            if (!Regex.IsMatch(raw, validationRegex))
            {
                _validationErrors[propName] = Loc["TmWireframeProps_ValidationRegexError", validationRegex];
                return Task.CompletedTask;
            }
        }

        return DebounceApply(propName, async () =>
        {
            await PushPropChange(propName, JsonSerializer.SerializeToElement(raw));
        });
    }

    // Bool checkbox: @onchange → immediate (toggle is atomic)
    private async Task OnBoolPropChanged(string propName, ChangeEventArgs e)
    {
        if (Document is null) return;
        var val = e.Value is bool b ? b : bool.TryParse(e.Value?.ToString(), out var bv) && bv;
        await PushPropChange(propName, JsonSerializer.SerializeToElement(val));
    }

    // Number props: @oninput → debounce; silent on partial input (e.g. "-", "1.")
    private Task OnNumberPropChanged(string propName, PropType type, ChangeEventArgs e)
    {
        if (Document is null) return Task.CompletedTask;
        var raw = e.Value?.ToString() ?? "";
        _validationErrors.Remove(propName);

        if (type == PropType.Int)
        {
            if (!int.TryParse(raw, out var i)) return Task.CompletedTask;
            return DebounceApply(propName, () => PushPropChange(propName, JsonSerializer.SerializeToElement(i)));
        }
        else
        {
            if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                return Task.CompletedTask;
            return DebounceApply(propName, () => PushPropChange(propName, JsonSerializer.SerializeToElement(d)));
        }
    }

    // StringList: @oninput → debounce
    private Task OnStringListPropChanged(string propName, ChangeEventArgs e)
    {
        if (Document is null) return Task.CompletedTask;
        var raw = e.Value?.ToString() ?? "";
        var arr = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return DebounceApply(propName, () => PushPropChange(propName, JsonSerializer.SerializeToElement(arr)));
    }

    // ── Command routing ───────────────────────────────────────────────────────

    private async Task PushPropChange(string propName, JsonElement newValue)
    {
        if (Document is null) return;

        if (_elements.Count == 1)
        {
            var changes = new Dictionary<string, JsonElement?> { [propName] = newValue };
            if (CommandStack is not null)
                CommandStack.Push(new UpdatePropsCommand(Document, _elements[0].Id, changes));
            else
                _elements[0].Props[propName] = newValue;

            // When size changes and the component def has size presets, also resize the element.
            if (propName == "size")
                TryApplySizePreset(_elements[0], newValue.GetString());
        }
        else
        {
            var changes = new Dictionary<string, JsonElement?> { [propName] = newValue };
            if (CommandStack is not null)
                CommandStack.Push(new BulkUpdateCommand(Document, _elements.Select(e => e.Id), changes));
            else
                foreach (var el in _elements) el.Props[propName] = newValue;

            // Apply size presets to each element individually (may differ by type)
            if (propName == "size")
                foreach (var el in _elements)
                    TryApplySizePreset(el, newValue.GetString());
        }

        await NotifyChanged();
    }

    private void TryApplySizePreset(WireframeElement el, string? sizeValue)
    {
        if (Document is null || sizeValue is null) return;
        var def = _registry.GetDef(el.Type);
        if (def?.SizePresets is null) return;
        if (!def.SizePresets.TryGetValue(sizeValue, out var preset)) return;

        if (CommandStack is not null)
            CommandStack.Push(new ResizeElementCommand(
                Document, el.Id, el.X, el.Y, el.W, el.H, el.X, el.Y, preset.W, preset.H));
        else
            { el.W = preset.W; el.H = preset.H; }
    }

    // ── Arrange commands (Bring to Front / Send to Back) ──────────────────────

    private async Task OnBringToFront()
    {
        if (Document is null || _elements.Count == 0) return;
        var ids = _elements.Select(e => e.Id).ToList();
        if (CommandStack is not null)
            CommandStack.Push(new BringToFrontCommand(Document, ids));
        else
        {
            var maxZ = Document.Elements.Max(e => e.ZIndex);
            foreach (var el in _elements)
                el.ZIndex = ++maxZ;
        }
        await NotifyChanged();
    }

    private async Task OnSendToBack()
    {
        if (Document is null || _elements.Count == 0) return;
        var ids = _elements.Select(e => e.Id).ToList();
        if (CommandStack is not null)
            CommandStack.Push(new SendToBackCommand(Document, ids));
        else
        {
            var minZ = Document.Elements.Min(e => e.ZIndex);
            var offset = ids.Count;
            foreach (var el in _elements)
                el.ZIndex = minZ - offset--;
        }
        await NotifyChanged();
    }

    // ── Lock toggle ───────────────────────────────────────────────────────────

    private async Task OnLockChanged(ChangeEventArgs e)
    {
        if (Document is null || _elements.Count == 0) return;
        var val = e.Value is bool b ? b : bool.TryParse(e.Value?.ToString(), out var bv) && bv;
        var ids = _elements.Select(e => e.Id).ToList();

        if (CommandStack is not null)
        {
            if (val)
                CommandStack.Push(new LockElementsCommand(Document, ids));
            else
                CommandStack.Push(new UnlockElementsCommand(Document, ids));
        }
        else
        {
            foreach (var el in _elements)
                el.IsLocked = val;
        }
        await NotifyChanged();
    }

    // ── Rotation change ───────────────────────────────────────────────────────

    private Task OnRotationChanged(ChangeEventArgs e)
    {
        if (Document is null || _elements.Count == 0) return Task.CompletedTask;
        if (!double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rot))
            return Task.CompletedTask;

        return DebounceApply("rotation", async () =>
        {
            if (_elements.Count == 1)
            {
                var el = _elements[0];
                var before = el.Rotation;
                if (CommandStack is not null)
                    CommandStack.Push(new RotateElementCommand(Document, el.Id, before, rot));
                else
                    el.Rotation = rot;
            }
            else
            {
                // Multi-select: apply same rotation to all selected
                if (CommandStack is not null)
                {
                    var ids = _elements.Select(e => e.Id);
                    var beforeMap = _elements.ToDictionary(e => e.Id, e => e.Rotation);
                    CommandStack.Push(new BulkRotateCommand(Document, ids, beforeMap, rot));
                }
                else
                {
                    foreach (var el in _elements)
                        el.Rotation = rot;
                }
            }
            await NotifyChanged();
        });
    }

    private async Task NotifyChanged()
    {
        if (Document is not null)
            await DocumentChanged.InvokeAsync(Document);
    }

    // ── Align / Distribute ────────────────────────────────────────────────────

    private async Task OnAlign(WireframeAlignment alignment)
    {
        if (Document is null || _elements.Count < 2) return;
        var ids = _elements.Select(e => e.Id);
        if (CommandStack is not null)
            CommandStack.Push(new AlignElementsCommand(Document, ids, alignment));
        else
            new AlignElementsCommand(Document, ids, alignment).Execute();
        await NotifyChanged();
    }

    private async Task OnDistribute(WireframeDistribution distribution)
    {
        if (Document is null || _elements.Count < 3) return;
        var ids = _elements.Select(e => e.Id);
        if (CommandStack is not null)
            CommandStack.Push(new DistributeElementsCommand(Document, ids, distribution));
        else
            new DistributeElementsCommand(Document, ids, distribution).Execute();
        await NotifyChanged();
    }

    // ── Connector property handlers ───────────────────────────────────────────

    private async Task OnConnectorRoutingChanged(string connectorId, ChangeEventArgs e)
    {
        if (Document is null) return;
        var c = Document.Connectors.FirstOrDefault(x => x.Id == connectorId);
        if (c is null) return;
        var routing = e.Value?.ToString() ?? "straight";
        if (CommandStack is not null)
            CommandStack.Push(new UpdateConnectorRoutingCommand(Document, connectorId, c.Routing, c.Waypoints.ToList(), routing, c.Waypoints.ToList()));
        else
            c.Routing = routing;
        await NotifyChanged();
    }

    private async Task OnConnectorArrowChanged(string connectorId, bool isStart, ChangeEventArgs e)
    {
        if (Document is null) return;
        var c = Document.Connectors.FirstOrDefault(x => x.Id == connectorId);
        if (c is null) return;
        var arrow = e.Value?.ToString() ?? "none";
        var beforeStart = c.StartArrow;
        var beforeEnd = c.EndArrow;
        var afterStart = isStart ? arrow : c.StartArrow;
        var afterEnd = isStart ? c.EndArrow : arrow;
        if (CommandStack is not null)
            CommandStack.Push(new UpdateConnectorStyleCommand(Document, connectorId,
                c.Stroke, c.StrokeWidth, c.StrokeDasharray, beforeStart, beforeEnd,
                c.Stroke, c.StrokeWidth, c.StrokeDasharray, afterStart, afterEnd));
        else
        {
            if (isStart) c.StartArrow = arrow;
            else c.EndArrow = arrow;
        }
        await NotifyChanged();
    }

    private async Task OnConnectorStrokeChanged(string connectorId, ChangeEventArgs e)
    {
        if (Document is null) return;
        var c = Document.Connectors.FirstOrDefault(x => x.Id == connectorId);
        if (c is null) return;
        var stroke = e.Value?.ToString() ?? "#94a3b8";
        if (CommandStack is not null)
            CommandStack.Push(new UpdateConnectorStyleCommand(Document, connectorId,
                c.Stroke, c.StrokeWidth, c.StrokeDasharray, c.StartArrow, c.EndArrow,
                stroke, c.StrokeWidth, c.StrokeDasharray, c.StartArrow, c.EndArrow));
        else
            c.Stroke = stroke;
        await NotifyChanged();
    }

    private async Task OnConnectorStrokeWidthChanged(string connectorId, ChangeEventArgs e)
    {
        if (Document is null) return;
        var c = Document.Connectors.FirstOrDefault(x => x.Id == connectorId);
        if (c is null) return;
        if (!double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var width))
            return;
        width = Math.Max(0.5, Math.Min(20, width));
        if (CommandStack is not null)
            CommandStack.Push(new UpdateConnectorStyleCommand(Document, connectorId,
                c.Stroke, c.StrokeWidth, c.StrokeDasharray, c.StartArrow, c.EndArrow,
                c.Stroke, width, c.StrokeDasharray, c.StartArrow, c.EndArrow));
        else
            c.StrokeWidth = width;
        await NotifyChanged();
    }

    private async Task OnConnectorLabelChanged(string connectorId, ChangeEventArgs e)
    {
        if (Document is null) return;
        var c = Document.Connectors.FirstOrDefault(x => x.Id == connectorId);
        if (c is null) return;
        var label = e.Value?.ToString();
        if (CommandStack is not null)
            CommandStack.Push(new UpdateConnectorLabelCommand(Document, connectorId, c.Label, label));
        else
            c.Label = label;
        await NotifyChanged();
    }

    private async Task OnLayerChanged(ChangeEventArgs e)
    {
        if (Document is null || _elements.Count == 0) return;
        var layerId = e.Value?.ToString() ?? "";
        var elementIds = _elements.Select(el => el.Id).ToArray();

        if (CommandStack is not null)
            CommandStack.Push(new MoveElementsToLayerCommand(Document, elementIds, layerId));
        else
            foreach (var el in _elements) el.LayerId = layerId;

        await NotifyChanged();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var cts in _debounceCts.Values) { cts.Cancel(); cts.Dispose(); }
        _debounceCts.Clear();
    }

    // ── Group label helper ────────────────────────────────────────────────────

    private string GetGroupLabel(string groupId)
    {
        if (Document is null) return groupId;
        var group = Document.Elements.FirstOrDefault(e => e.Id == groupId && e.Type == "__group__");
        if (group is null) return groupId;
        var label = group.Props.GetString("label");
        return string.IsNullOrEmpty(label) ? "Group" : label;
    }
}

