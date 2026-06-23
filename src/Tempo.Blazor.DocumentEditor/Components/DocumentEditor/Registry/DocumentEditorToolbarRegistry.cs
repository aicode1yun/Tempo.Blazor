namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Registry for toolbar items. Items are ordered by Order and filtered by command availability.</summary>
public sealed class DocumentEditorToolbarRegistry
{
    private readonly DocumentEditorCommandRegistry? _commandRegistry;
    private readonly List<DocumentToolbarItem> _items = [];
    private readonly List<DocumentToolbarGroup> _groups = [];

    public DocumentEditorToolbarRegistry(DocumentEditorCommandRegistry? commandRegistry = null)
    {
        _commandRegistry = commandRegistry;
    }

    /// <summary>Registers a toolbar item. Can be called by host application to add custom items.</summary>
    public void Register(DocumentToolbarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    /// <summary>Registers a toolbar group used for ordering and overflow grouping.</summary>
    public void RegisterGroup(DocumentToolbarGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _groups.Add(group);
    }

    /// <summary>Returns all registered items sorted by Order.</summary>
    public IEnumerable<DocumentToolbarItem> GetItems() =>
        DocumentToolbarItem.SortByOrder(_items);

    /// <summary>Returns groups sorted by tab and order.</summary>
    public IEnumerable<DocumentToolbarGroup> GetGroups(DocumentToolbarTab? tab = null)
    {
        var groups = tab is null
            ? _groups
            : _groups.Where(group => group.Tab == tab);

        return groups
            .OrderBy(group => group.Tab)
            .ThenBy(group => group.Order);
    }

    /// <summary>Returns items whose commands are available in the command registry.
    /// Items without a CommandName are always included.
    /// When no command registry is attached, all items are returned.</summary>
    public IEnumerable<DocumentToolbarItem> GetAvailableItems(DocumentToolbarVisibilityContext? context = null) =>
        DocumentToolbarItem.SortByOrder(_items.Where(item => IsAvailable(item, context)));

    private bool IsAvailable(DocumentToolbarItem item, DocumentToolbarVisibilityContext? context)
    {
        context ??= new DocumentToolbarVisibilityContext { CommandRegistry = _commandRegistry };
        if (!item.IsVisible(context)) return false;

        if (item.CommandName is null) return true;
        if (_commandRegistry is null) return true;
        if (!_commandRegistry.TryGet(item.CommandName, out _)) return false;

        return _commandRegistry.GetState(item.CommandName)?.IsVisible ?? true;
    }
}
