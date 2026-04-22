namespace Tempo.Blazor.NotionEditor.Enums;

/// <summary>
/// All actions that can be dispatched by <c>NotionKeyboardManager</c>
/// in response to a registered keyboard shortcut.
/// </summary>
public enum NotionEditorAction
{
    // ── Inline text formatting ────────────────────────────────────────────────
    Bold,
    Italic,
    Underline,
    Strikethrough,
    InlineCode,
    Link,

    // ── Block / editor actions ────────────────────────────────────────────────
    SlashMenu,
    PageSearch,
    DuplicateBlock,

    // ── History ───────────────────────────────────────────────────────────────
    Undo,
    Redo,

    // ── Page-level toggles ────────────────────────────────────────────────────
    ToggleSmallText,
    ToggleFullWidth,

    // ── Collaboration ─────────────────────────────────────────────────────────
    Comment,

    // ── Navigation / UI ───────────────────────────────────────────────────────
    Deselect,
    FocusPreviousBlock,
    FocusNextBlock,
}
