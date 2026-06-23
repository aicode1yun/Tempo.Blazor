namespace Tempo.Blazor.Components.NotionEditor.UI;

/// <summary>Describes one keyboard shortcut rendered by the Notion shortcuts panel.</summary>
public sealed record NotionShortcutItem
{
    /// <summary>Stable action identifier used by tests and analytics.</summary>
    public required string Action { get; init; }

    /// <summary>Localization key for the shortcut description.</summary>
    public required string DescriptionKey { get; init; }

    /// <summary>Keyboard chord labels rendered as key caps.</summary>
    public required IReadOnlyList<string> Keys { get; init; }
}

/// <summary>Groups related Notion keyboard shortcuts in the shortcuts panel.</summary>
public sealed record NotionShortcutGroup
{
    /// <summary>Localization key for the group heading.</summary>
    public required string TitleKey { get; init; }

    /// <summary>Shortcuts that belong to the group.</summary>
    public required IReadOnlyList<NotionShortcutItem> Items { get; init; }
}

/// <summary>Default keyboard shortcut catalog for <see cref="TmNotionShortcutsPanel"/>.</summary>
public static class NotionShortcutCatalog
{
    /// <summary>Gets the complete default shortcut catalog.</summary>
    public static IReadOnlyList<NotionShortcutGroup> DefaultGroups { get; } =
    [
        new()
        {
            TitleKey = "Notion_Shortcuts_Group_Navigation",
            Items =
            [
                Item("OpenPageSearch", "Ctrl/⌘", "P"),
                Item("OpenShortcuts", "?"),
                Item("ClosePanel", "Esc"),
                Item("MoveFocusUp", "↑"),
                Item("MoveFocusDown", "↓")
            ]
        },
        new()
        {
            TitleKey = "Notion_Shortcuts_Group_Editing",
            Items =
            [
                Item("CreateBlockBelow", "Enter"),
                Item("InsertLineBreak", "Shift", "Enter"),
                Item("Indent", "Tab"),
                Item("Outdent", "Shift", "Tab"),
                Item("SlashMenu", "/")
            ]
        },
        new()
        {
            TitleKey = "Notion_Shortcuts_Group_Insert",
            Items =
            [
                Item("MentionPerson", "@"),
                Item("LinkPage", "[["),
                Item("InsertToken", "{{")
            ]
        },
        new()
        {
            TitleKey = "Notion_Shortcuts_Group_Formatting",
            Items =
            [
                Item("Bold", "Ctrl/⌘", "B"),
                Item("Italic", "Ctrl/⌘", "I"),
                Item("Underline", "Ctrl/⌘", "U")
            ]
        }
    ];

    private static NotionShortcutItem Item(string action, params string[] keys) => new()
    {
        Action = action,
        DescriptionKey = $"Notion_Shortcut_{action}",
        Keys = keys
    };
}
