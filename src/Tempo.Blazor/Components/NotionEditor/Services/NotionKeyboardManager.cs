using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Models;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Scoped service that centralises all Notion-editor keyboard shortcuts.
///
/// Usage:
/// <code>
/// // In a Blazor component:
/// [Inject] private NotionKeyboardManager KeyboardManager { get; set; } = default!;
///
/// private Task OnKeyDown(KeyboardEventArgs e)
/// {
///     var action = KeyboardManager.HandleKeyDown(e);
///     if (action is NotionEditorAction.Bold) { ... }
///     return Task.CompletedTask;
/// }
/// </code>
///
/// Subscribers may also listen to <see cref="ActionTriggered"/> for a
/// decoupled, event-driven integration.
/// </summary>
public sealed class NotionKeyboardManager
{
    // ── Binding record ────────────────────────────────────────────────────────

    /// <summary>Associates a key combination with an editor action and a human-readable label.</summary>
    public sealed record NotionKeyboardBinding(
        NotionEditorAction Action,
        string             Key,
        bool               Ctrl  = false,
        bool               Shift = false,
        bool               Alt   = false,
        string             Label = "");

    // ── Registry ──────────────────────────────────────────────────────────────

    private readonly List<NotionKeyboardBinding> _bindings = [];

    // ── Event ─────────────────────────────────────────────────────────────────

    /// <summary>Raised whenever a registered shortcut is detected by <see cref="HandleKeyDown"/>.</summary>
    public event Action<NotionEditorAction>? ActionTriggered;

    // ── Constructor (default shortcuts) ──────────────────────────────────────

    public NotionKeyboardManager()
    {
        RegisterDefaults();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Processes a <see cref="KeyboardEventArgs"/> event, fires <see cref="ActionTriggered"/>
    /// if a matching shortcut is found, and returns the matched action.
    /// Returns <c>null</c> when no binding matches.
    /// </summary>
    public NotionEditorAction? HandleKeyDown(KeyboardEventArgs e)
    {
        foreach (var binding in _bindings)
        {
            if (Matches(e, binding))
            {
                ActionTriggered?.Invoke(binding.Action);
                return binding.Action;
            }
        }
        return null;
    }

    /// <summary>Adds a custom shortcut binding. A binding for the same action is replaced.</summary>
    public void Register(NotionKeyboardBinding binding)
    {
        _bindings.RemoveAll(b => b.Action == binding.Action);
        _bindings.Add(binding);
    }

    /// <summary>Removes all bindings for the specified action.</summary>
    public void Unregister(NotionEditorAction action) =>
        _bindings.RemoveAll(b => b.Action == action);

    /// <summary>Read-only view of all currently registered bindings.</summary>
    public IReadOnlyList<NotionKeyboardBinding> Bindings => _bindings;

    /// <summary>
    /// Returns all shortcuts grouped into <see cref="TmShortcutCategory"/> instances,
    /// ready for display in a help panel.
    /// </summary>
    public IReadOnlyList<TmShortcutCategory> GetShortcutCategories() =>
    [
        new TmShortcutCategory("Text Formatting", _bindings
            .Where(b => b.Action is
                NotionEditorAction.Bold         or
                NotionEditorAction.Italic       or
                NotionEditorAction.Underline    or
                NotionEditorAction.Strikethrough or
                NotionEditorAction.InlineCode   or
                NotionEditorAction.Link)
            .Select(ToShortcut)),

        new TmShortcutCategory("Editor Actions", _bindings
            .Where(b => b.Action is
                NotionEditorAction.SlashMenu      or
                NotionEditorAction.PageSearch     or
                NotionEditorAction.DuplicateBlock or
                NotionEditorAction.Comment)
            .Select(ToShortcut)),

        new TmShortcutCategory("History", _bindings
            .Where(b => b.Action is
                NotionEditorAction.Undo or
                NotionEditorAction.Redo)
            .Select(ToShortcut)),

        new TmShortcutCategory("Page", _bindings
            .Where(b => b.Action is
                NotionEditorAction.ToggleSmallText or
                NotionEditorAction.ToggleFullWidth)
            .Select(ToShortcut)),

        new TmShortcutCategory("Navigation", _bindings
            .Where(b => b.Action is
                NotionEditorAction.Deselect         or
                NotionEditorAction.FocusPreviousBlock or
                NotionEditorAction.FocusNextBlock)
            .Select(ToShortcut)),
    ];

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool Matches(KeyboardEventArgs e, NotionKeyboardBinding binding) =>
        string.Equals(e.Key, binding.Key, StringComparison.OrdinalIgnoreCase)
        && e.CtrlKey  == binding.Ctrl
        && e.ShiftKey == binding.Shift
        && e.AltKey   == binding.Alt;

    private static TmKeyboardShortcut ToShortcut(NotionKeyboardBinding b) =>
        new(BuildKeysString(b), b.Label);

    private static string BuildKeysString(NotionKeyboardBinding b)
    {
        var parts = new List<string>(4);
        if (b.Ctrl)  parts.Add("Ctrl");
        if (b.Shift) parts.Add("Shift");
        if (b.Alt)   parts.Add("Alt");
        parts.Add(b.Key.Length == 1 ? b.Key.ToUpperInvariant() : b.Key);
        return string.Join("+", parts);
    }

    // ── Default shortcuts ─────────────────────────────────────────────────────

    private void RegisterDefaults()
    {
        // Text formatting (handled by the browser / contenteditable natively for most,
        // but registered here so they appear in the help panel and can be intercepted).
        _bindings.AddRange(
        [
            new(NotionEditorAction.Bold,            "b", Ctrl: true,  Label: "Bold"),
            new(NotionEditorAction.Italic,          "i", Ctrl: true,  Label: "Italic"),
            new(NotionEditorAction.Underline,       "u", Ctrl: true,  Label: "Underline"),
            new(NotionEditorAction.Strikethrough,   "s", Ctrl: true, Shift: true, Label: "Strikethrough"),
            new(NotionEditorAction.InlineCode,      "e", Ctrl: true,  Label: "Inline code"),
            new(NotionEditorAction.Link,            "k", Ctrl: true,  Label: "Add link"),

            // Editor actions
            new(NotionEditorAction.SlashMenu,       "/", Ctrl: true,  Label: "Slash command menu"),
            new(NotionEditorAction.PageSearch,      "p", Ctrl: true,  Label: "Search pages"),
            new(NotionEditorAction.DuplicateBlock,  "d", Ctrl: true,  Label: "Duplicate block"),
            new(NotionEditorAction.Comment,         "m", Ctrl: true, Shift: true, Label: "Add comment"),

            // History
            new(NotionEditorAction.Undo,            "z", Ctrl: true,  Label: "Undo"),
            new(NotionEditorAction.Redo,            "y", Ctrl: true,  Label: "Redo"),
            new(NotionEditorAction.Redo,            "z", Ctrl: true, Shift: true, Label: "Redo"),

            // Page toggles
            new(NotionEditorAction.ToggleSmallText, "h", Ctrl: true, Shift: true, Label: "Toggle small text"),
            new(NotionEditorAction.ToggleFullWidth, "f", Ctrl: true, Shift: true, Label: "Toggle full width"),

            // Navigation / UI
            new(NotionEditorAction.Deselect,        "Escape", Label: "Deselect / close menus"),
            new(NotionEditorAction.FocusPreviousBlock, "ArrowUp",   Label: "Focus previous block"),
            new(NotionEditorAction.FocusNextBlock,    "ArrowDown",  Label: "Focus next block"),
        ]);
    }
}
