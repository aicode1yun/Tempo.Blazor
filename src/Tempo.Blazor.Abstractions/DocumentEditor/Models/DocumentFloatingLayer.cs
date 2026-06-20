namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Identifies the kind of floating UI layer in the document editor.</summary>
public enum DocumentFloatingLayerKind
{
    /// <summary>Find/replace panel.</summary>
    FindPanel,

    /// <summary>Link creation or editing dialog.</summary>
    LinkDialog,

    /// <summary>Token insertion menu.</summary>
    TokenMenu,

    /// <summary>Image URL insertion dialog.</summary>
    ImageDialog,

    /// <summary>Text selection context menu.</summary>
    TextContextMenu,

    /// <summary>Table cell context menu.</summary>
    TableContextMenu,

    /// <summary>Image selection floating toolbar.</summary>
    ImageSelectionToolbar,

    /// <summary>Inline mini formatting toolbar.</summary>
    MiniToolbar,

    /// <summary>Version history dialog.</summary>
    VersionDialog,

    /// <summary>Document comparison dialog.</summary>
    CompareDialog,

    /// <summary>Document side panel.</summary>
    SidePanel,

    /// <summary>Custom application-defined layer.</summary>
    Custom
}

/// <summary>Anchor used to position a floating document editor layer.</summary>
public sealed record DocumentFloatingLayerAnchor
{
    /// <summary>Anchor x-coordinate in viewport pixels.</summary>
    public double X { get; init; }

    /// <summary>Anchor y-coordinate in viewport pixels.</summary>
    public double Y { get; init; }

    /// <summary>Anchor width in pixels.</summary>
    public double Width { get; init; }

    /// <summary>Anchor height in pixels.</summary>
    public double Height { get; init; }

    /// <summary>Optional DOM target id or selector used by the runtime focus bridge.</summary>
    public string? Target { get; init; }
}

/// <summary>Represents a single open floating UI layer.</summary>
public sealed class DocumentFloatingLayerState
{
    /// <summary>Unique identifier for this layer instance.</summary>
    public required string LayerId { get; init; }

    /// <summary>Kind of floating layer.</summary>
    public required DocumentFloatingLayerKind Kind { get; init; }

    /// <summary>Z-index for rendering order. Higher values appear on top.</summary>
    public int ZIndex { get; init; }

    /// <summary>Priority for close ordering. Defaults to <see cref="ZIndex"/> when omitted.</summary>
    public int? Priority { get; init; }

    /// <summary>Effective priority used by stack ordering.</summary>
    public int EffectivePriority => Priority ?? ZIndex;

    /// <summary>Optional anchor for positioning the layer.</summary>
    public DocumentFloatingLayerAnchor? Anchor { get; init; }

    /// <summary>Optional focus target restored after the layer closes.</summary>
    public string? RestoreFocusTarget { get; init; }

    /// <summary>Whether Escape can dismiss this layer.</summary>
    public bool IsDismissible { get; init; } = true;

    /// <summary>Whether pointer interaction outside this layer should dismiss it.</summary>
    public bool CloseOnOutsideClick { get; init; } = true;

    /// <summary>Optional callback to close this specific layer.</summary>
    public Func<Task>? CloseAsync { get; init; }
}

/// <summary>
/// Manages the stack of open floating UI layers in the document editor.
/// Layers are ordered by z-index; the topmost layer is closed first when Esc is pressed.
/// </summary>
public sealed class DocumentFloatingLayerStack
{
    private readonly List<DocumentFloatingLayerState> _layers = [];

    /// <summary>All currently open layers, ordered by ascending z-index.</summary>
    public IReadOnlyList<DocumentFloatingLayerState> Layers => _layers;

    /// <summary>The topmost (highest priority) open layer, or null if the stack is empty.</summary>
    public DocumentFloatingLayerState? Topmost => _layers.Count > 0 ? _layers[^1] : null;

    /// <summary>The topmost dismissible layer, or null if none can be dismissed.</summary>
    public DocumentFloatingLayerState? TopmostDismissible => _layers.LastOrDefault(layer => layer.IsDismissible);

    /// <summary>Whether any layers are currently open.</summary>
    public bool HasOpenLayers => _layers.Count > 0;

    /// <summary>Pushes a new layer onto the stack.</summary>
    public void Push(DocumentFloatingLayerState layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layers.RemoveAll(l => l.LayerId == layer.LayerId);
        _layers.Add(layer);
        _layers.Sort((a, b) =>
        {
            var priorityCompare = a.EffectivePriority.CompareTo(b.EffectivePriority);
            return priorityCompare != 0
                ? priorityCompare
                : string.CompareOrdinal(a.LayerId, b.LayerId);
        });
    }

    /// <summary>Removes the layer with the given identifier.</summary>
    public void Remove(string layerId)
    {
        _layers.RemoveAll(l => l.LayerId == layerId);
    }

    /// <summary>Removes all open layers.</summary>
    public void Clear() => _layers.Clear();

    /// <summary>Invokes the close callback of the topmost dismissible layer and removes it from the stack.</summary>
    public async Task CloseTopmostAsync()
        => await CloseTopmostDismissibleAsync();

    /// <summary>Invokes the close callback of the topmost dismissible layer and removes it from the stack.</summary>
    public async Task<bool> CloseTopmostDismissibleAsync()
    {
        var topmost = TopmostDismissible;
        if (topmost is null) return false;

        await CloseLayerAsync(topmost);
        return true;
    }

    /// <summary>Closes outside-click dismissible layers that are not in the event target path.</summary>
    public async Task<IReadOnlyList<string>> CloseForOutsideClickAsync(IEnumerable<string>? targetLayerIds)
    {
        var targetIds = new HashSet<string>(targetLayerIds ?? [], StringComparer.Ordinal);
        var closed = new List<string>();

        foreach (var layer in _layers.OrderByDescending(layer => layer.EffectivePriority).ToArray())
        {
            if (targetIds.Contains(layer.LayerId))
            {
                break;
            }

            if (!layer.CloseOnOutsideClick || !layer.IsDismissible)
            {
                continue;
            }

            closed.Add(layer.LayerId);
            await CloseLayerAsync(layer);
        }

        return closed;
    }

    private async Task CloseLayerAsync(DocumentFloatingLayerState layer)
    {
        _layers.Remove(layer);
        if (layer.CloseAsync is not null)
        {
            await layer.CloseAsync();
        }
    }
}
