namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Context passed to the <c>NewFolderForm</c> render fragment of <c>TmDocumentManager&lt;TMetadata&gt;</c>.
/// </summary>
public class NewFolderContext<TMetadata> where TMetadata : class
{
    /// <summary>The name of the new folder (two-way bindable).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional metadata for the new folder.</summary>
    public TMetadata? Metadata { get; set; }

    /// <summary>Called when the user confirms creation.</summary>
    public Func<Task>? OnSubmit { get; set; }

    /// <summary>Called when the user cancels.</summary>
    public Func<Task>? OnCancel { get; set; }
}

/// <summary>
/// Context passed to the <c>DeleteForm</c> render fragment of <c>TmDocumentManager&lt;TMetadata&gt;</c>.
/// </summary>
public class DeleteContext<TMetadata> where TMetadata : class
{
    /// <summary>Items selected for deletion.</summary>
    public IReadOnlyList<DocumentManagerItem<TMetadata>> Items { get; set; } = [];

    /// <summary>Called when the user confirms deletion.</summary>
    public Func<Task>? OnConfirm { get; set; }

    /// <summary>Called when the user cancels.</summary>
    public Func<Task>? OnCancel { get; set; }
}

/// <summary>
/// Context passed to the <c>EditForm</c> render fragment of <c>TmDocumentManager&lt;TMetadata&gt;</c>.
/// </summary>
public class EditContext<TMetadata> where TMetadata : class
{
    /// <summary>The item being edited.</summary>
    public DocumentManagerItem<TMetadata> Item { get; set; } = null!;

    /// <summary>Editable metadata (two-way bindable).</summary>
    public TMetadata? Metadata { get; set; }

    /// <summary>Called when the user saves changes.</summary>
    public Func<Task>? OnSubmit { get; set; }

    /// <summary>Called when the user cancels.</summary>
    public Func<Task>? OnCancel { get; set; }
}

/// <summary>
/// Context passed to the <c>DetailPanel</c> render fragment of <c>TmDocumentManager&lt;TMetadata&gt;</c>.
/// </summary>
public class DetailContext<TMetadata> where TMetadata : class
{
    /// <summary>The item whose details are displayed.</summary>
    public DocumentManagerItem<TMetadata> Item { get; set; } = null!;
}

/// <summary>
/// Context passed to the <c>ItemContextMenu</c> render fragment of <c>TmDocumentManager&lt;TMetadata&gt;</c>.
/// </summary>
public class ContextMenuContext<TMetadata> where TMetadata : class
{
    /// <summary>The item that was right-clicked.</summary>
    public DocumentManagerItem<TMetadata> Item { get; set; } = null!;

    /// <summary>Actions available for this item based on permissions.</summary>
    public IReadOnlyList<string> AvailableActions { get; set; } = [];

    /// <summary>Called when the user selects an action from the menu.</summary>
    public Func<string, Task>? OnActionSelected { get; set; }
}
