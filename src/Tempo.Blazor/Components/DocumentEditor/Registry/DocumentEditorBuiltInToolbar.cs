namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Declarative metadata for the built-in document editor toolbar.</summary>
public static class DocumentEditorBuiltInToolbar
{
    private static readonly DocumentToolbarGroup[] Groups =
    [
        new() { Id = "clipboard", Tab = DocumentToolbarTab.Home, LabelKey = "TmDocumentEditor_GroupClipboard", Order = 10 },
        new() { Id = "formatting", Tab = DocumentToolbarTab.Home, LabelKey = "TmDocumentEditor_GroupFormatting", Order = 20 },
        new() { Id = "paragraph", Tab = DocumentToolbarTab.Home, LabelKey = "TmDocumentEditor_GroupParagraph", Order = 30 },
        new() { Id = "insert", Tab = DocumentToolbarTab.Insert, LabelKey = "TmDocumentEditor_GroupInsert", Order = 10 },
        new() { Id = "layout", Tab = DocumentToolbarTab.Layout, LabelKey = "TmDocumentEditor_GroupLayout", Order = 10 },
        new() { Id = "references", Tab = DocumentToolbarTab.References, LabelKey = "TmDocumentEditor_GroupReferences", Order = 10 },
        new() { Id = "review", Tab = DocumentToolbarTab.Review, LabelKey = "TmDocumentEditor_GroupReview", Order = 10 },
        new() { Id = "view", Tab = DocumentToolbarTab.View, LabelKey = "TmDocumentEditor_TabView", Order = 10 },
        new() { Id = "file", Tab = DocumentToolbarTab.View, LabelKey = "TmDocumentEditor_GroupFile", Order = 20 },
        new() { Id = "headerFooter", Tab = DocumentToolbarTab.HeaderFooter, LabelKey = "TmDocumentEditor_TabHeaderFooter", Order = 10 },
    ];

    private static readonly DocumentToolbarItem[] Items =
    [
        Item("save", "save", "save", "TmDocumentEditor_Save", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "clipboard", 10, ToolbarItemPriority.Primary),
        Item("undo", "undo", "undo-2", "TmDocumentEditor_Undo", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "clipboard", 20, ToolbarItemPriority.Primary),
        Item("redo", "redo", "redo-2", "TmDocumentEditor_Redo", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "clipboard", 30, ToolbarItemPriority.Primary),
        Item("bold", "bold", "bold", "TmDocumentEditor_Bold", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "formatting", 10, ToolbarItemPriority.Primary),
        Item("italic", "italic", "italic", "TmDocumentEditor_Italic", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "formatting", 20, ToolbarItemPriority.Primary),
        Item("underline", "underline", "underline", "TmDocumentEditor_Underline", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "formatting", 30, ToolbarItemPriority.Primary),
        Item("fontFamily", "fontFamily", null, "TmDocumentEditor_FontFamily", DocumentToolbarItemKind.Select, DocumentToolbarTab.Home, "formatting", 40, ToolbarItemPriority.Secondary),
        Item("fontSize", "fontSize", null, "TmDocumentEditor_FontSize", DocumentToolbarItemKind.Select, DocumentToolbarTab.Home, "formatting", 50, ToolbarItemPriority.Secondary),
        Item("textColor", "textColor", "paintbrush", "TmDocumentEditor_TextColor", DocumentToolbarItemKind.ColorPicker, DocumentToolbarTab.Home, "formatting", 60, ToolbarItemPriority.Secondary),
        Item("highlightColor", "highlightColor", "highlighter", "TmDocumentEditor_HighlightColor", DocumentToolbarItemKind.ColorPicker, DocumentToolbarTab.Home, "formatting", 70, ToolbarItemPriority.Secondary),
        Item("clearFormatting", "clearFormatting", "eraser", "TmDocumentEditor_ClearFormatting", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "formatting", 80, ToolbarItemPriority.Secondary),
        Item("link", "link", "link", "TmDocumentEditor_Link", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "formatting", 90, ToolbarItemPriority.Secondary),
        Item("alignLeft", "alignLeft", "align-left", "TmDocumentEditor_AlignLeft", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "paragraph", 10, ToolbarItemPriority.Secondary),
        Item("alignCenter", "alignCenter", "align-center", "TmDocumentEditor_AlignCenter", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "paragraph", 20, ToolbarItemPriority.Secondary),
        Item("alignRight", "alignRight", "align-right", "TmDocumentEditor_AlignRight", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "paragraph", 30, ToolbarItemPriority.Secondary),
        Item("alignJustify", "alignJustify", "align-justify", "TmDocumentEditor_AlignJustify", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Home, "paragraph", 40, ToolbarItemPriority.Secondary),
        Item("lineSpacing", "lineSpacing", "pilcrow", "TmDocumentEditor_LineSpacing", DocumentToolbarItemKind.Select, DocumentToolbarTab.Home, "paragraph", 50, ToolbarItemPriority.Secondary),
        Item("spacingBefore", "lineSpacing", null, "TmDocumentEditor_SpacingBefore", DocumentToolbarItemKind.Select, DocumentToolbarTab.Home, "paragraph", 60, ToolbarItemPriority.Secondary),
        Item("spacingAfter", "lineSpacing", null, "TmDocumentEditor_SpacingAfter", DocumentToolbarItemKind.Select, DocumentToolbarTab.Home, "paragraph", 70, ToolbarItemPriority.Secondary),
        Item("increaseIndent", "increaseIndent", "indent-increase", "TmDocumentEditor_IncreaseIndent", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "paragraph", 80, ToolbarItemPriority.Secondary),
        Item("decreaseIndent", "decreaseIndent", "indent-decrease", "TmDocumentEditor_DecreaseIndent", DocumentToolbarItemKind.Button, DocumentToolbarTab.Home, "paragraph", 90, ToolbarItemPriority.Secondary),
        Item("insertTable", "insertTable", "table", "TmDocumentEditor_InsertTable", DocumentToolbarItemKind.GridPicker, DocumentToolbarTab.Insert, "insert", 10, ToolbarItemPriority.Primary),
        Item("insertImage", "insertImage", "image", "TmDocumentEditor_InsertImage", DocumentToolbarItemKind.Button, DocumentToolbarTab.Insert, "insert", 20, ToolbarItemPriority.Primary),
        Item("insertPageBreak", "insertPageBreak", "between-horizontal-start", "TmDocumentEditor_PageBreak", DocumentToolbarItemKind.Button, DocumentToolbarTab.Insert, "insert", 30, ToolbarItemPriority.Secondary),
        Item("insertFootnote", "insertFootnote", "list-plus", "TmDocumentEditor_InsertFootnote", DocumentToolbarItemKind.Button, DocumentToolbarTab.References, "references", 10, ToolbarItemPriority.Secondary),
        Item("insertEndnote", "insertEndnote", "list-end", "TmDocumentEditor_InsertEndnote", DocumentToolbarItemKind.Button, DocumentToolbarTab.References, "references", 20, ToolbarItemPriority.Secondary),
        Item("trackChanges", "trackChanges", "history", "TmDocumentEditor_TrackChanges", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Review, "review", 10, ToolbarItemPriority.Primary),
        Item("reviewDisplayMode", "reviewDisplayMode", "eye", "TmDocumentEditor_ShowMarkup", DocumentToolbarItemKind.Select, DocumentToolbarTab.Review, "review", 20, ToolbarItemPriority.Secondary),
        Item("addComment", "addComment", "message-square-plus", "TmDocumentEditor_AddComment", DocumentToolbarItemKind.Button, DocumentToolbarTab.Review, "review", 30, ToolbarItemPriority.Primary),
        Item("openComments", "openComments", "message-square", "TmDocumentEditor_Comments", DocumentToolbarItemKind.Button, DocumentToolbarTab.Review, "review", 40, ToolbarItemPriority.Secondary),
        Item("openRevisions", "openRevisions", "git-compare", "TmDocumentEditor_Revisions", DocumentToolbarItemKind.Button, DocumentToolbarTab.Review, "review", 50, ToolbarItemPriority.Secondary),
        Item("compareDocuments", "compareDocuments", "columns-3", "TmDocumentEditor_CompareDocuments", DocumentToolbarItemKind.Button, DocumentToolbarTab.Review, "review", 60, ToolbarItemPriority.Secondary),
        Item("protectDocument", "protectDocument", "shield", "TmDocumentEditor_ProtectDocument", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.Review, "review", 70, ToolbarItemPriority.Secondary),
        Item("markEditableRegion", "markEditableRegion", "scan-text", "TmDocumentEditor_MarkEditableRegion", DocumentToolbarItemKind.Button, DocumentToolbarTab.Review, "review", 80, ToolbarItemPriority.Secondary),
        Item("showRuler", "showRuler", "ruler", "TmDocumentEditor_ShowRuler", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.View, "view", 10, ToolbarItemPriority.Primary),
        Item("zoomPageWidth", "zoomPageWidth", "panel-top", "TmDocumentEditor_PageWidth", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "view", 20, ToolbarItemPriority.Secondary),
        Item("showBlocks", "showBlocks", "pilcrow", "TmDocumentEditor_ShowBlocks", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.View, "view", 30, ToolbarItemPriority.Secondary),
        Item("fullscreen", "fullscreen", "maximize", "TmDocumentEditor_Fullscreen", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.View, "view", 40, ToolbarItemPriority.Secondary),
        Item("viewDocumentJson", "viewDocumentJson", "braces", "TmDocumentEditor_ViewJson", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "file", 10, ToolbarItemPriority.OverflowOnly),
        Item("viewClipboardHtml", "viewClipboardHtml", "clipboard-list", "TmDocumentEditor_ViewClipboardHtml", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "file", 20, ToolbarItemPriority.OverflowOnly),
        Item("exportPdf", "exportPdf", "file-type", "TmDocumentEditor_ExportPdf", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "file", 30, ToolbarItemPriority.Secondary),
        Item("importDocx", "importDocx", "upload", "TmDocumentEditor_ImportDocx", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "file", 40, ToolbarItemPriority.Secondary),
        Item("exportDocx", "exportDocx", "file-text", "TmDocumentEditor_ExportDocx", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "file", 50, ToolbarItemPriority.Secondary),
        Item("openVersions", "openVersions", "archive", "TmDocumentEditor_Versions", DocumentToolbarItemKind.Button, DocumentToolbarTab.View, "file", 60, ToolbarItemPriority.Secondary),
        Item("insertPageNumber", "insertPageNumber", "hash", "TmDocumentEditor_InsertPageNumber", DocumentToolbarItemKind.Button, DocumentToolbarTab.HeaderFooter, "headerFooter", 10, ToolbarItemPriority.Primary, context => context.IsHeaderFooterMode),
        Item("insertPageCount", "insertPageCount", "files", "TmDocumentEditor_InsertPageCount", DocumentToolbarItemKind.Button, DocumentToolbarTab.HeaderFooter, "headerFooter", 20, ToolbarItemPriority.Primary, context => context.IsHeaderFooterMode),
        Item("insertPageXOfY", "insertPageXOfY", "file-stack", "TmDocumentEditor_InsertPageXOfY", DocumentToolbarItemKind.Button, DocumentToolbarTab.HeaderFooter, "headerFooter", 30, ToolbarItemPriority.Primary, context => context.IsHeaderFooterMode),
        Item("insertDateField", "insertDateField", "calendar-days", "TmDocumentEditor_InsertDateField", DocumentToolbarItemKind.Button, DocumentToolbarTab.HeaderFooter, "headerFooter", 40, ToolbarItemPriority.Secondary, context => context.IsHeaderFooterMode),
        Item("insertDocumentTitleField", "insertDocumentTitleField", "file-text", "TmDocumentEditor_InsertDocumentTitleField", DocumentToolbarItemKind.Button, DocumentToolbarTab.HeaderFooter, "headerFooter", 50, ToolbarItemPriority.Secondary, context => context.IsHeaderFooterMode),
        Item("differentFirstPage", "differentFirstPage", "file-stack", "TmDocumentEditor_DifferentFirstPage", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.HeaderFooter, "headerFooter", 60, ToolbarItemPriority.Primary, context => context.IsHeaderFooterMode),
        Item("differentOddEven", "differentOddEven", "layout", "TmDocumentEditor_DifferentOddEven", DocumentToolbarItemKind.Toggle, DocumentToolbarTab.HeaderFooter, "headerFooter", 70, ToolbarItemPriority.Primary, context => context.IsHeaderFooterMode),
        Item("closeHeaderFooter", "closeHeaderFooter", "x", "TmDocumentEditor_CloseHeaderFooter", DocumentToolbarItemKind.Button, DocumentToolbarTab.HeaderFooter, "headerFooter", 80, ToolbarItemPriority.Primary, context => context.IsHeaderFooterMode),
    ];

    /// <summary>Built-in toolbar groups in their default order.</summary>
    public static IReadOnlyList<DocumentToolbarGroup> DefaultGroups => Groups;

    /// <summary>Built-in toolbar items in their default metadata form.</summary>
    public static IReadOnlyList<DocumentToolbarItem> DefaultItems => Items;

    /// <summary>Creates a toolbar registry preloaded with the built-in groups and items.</summary>
    public static DocumentEditorToolbarRegistry CreateRegistry(DocumentEditorCommandRegistry? commandRegistry = null)
    {
        var registry = new DocumentEditorToolbarRegistry(commandRegistry);
        foreach (var group in Groups)
        {
            registry.RegisterGroup(group);
        }

        foreach (var item in Items)
        {
            registry.Register(item);
        }

        return registry;
    }

    /// <summary>Finds built-in metadata for the given command name.</summary>
    public static DocumentToolbarItem? FindItemByCommandName(string commandName) =>
        Items.FirstOrDefault(item => string.Equals(item.CommandName, commandName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds built-in group metadata for the given group id.</summary>
    public static DocumentToolbarGroup? FindGroup(string? groupId) =>
        string.IsNullOrWhiteSpace(groupId)
            ? null
            : Groups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));

    private static DocumentToolbarItem Item(
        string id,
        string? commandName,
        string? icon,
        string labelKey,
        DocumentToolbarItemKind kind,
        DocumentToolbarTab tab,
        string group,
        int order,
        ToolbarItemPriority priority,
        Func<DocumentToolbarVisibilityContext, bool>? visibleWhen = null) =>
        new()
        {
            Id = id,
            CommandName = commandName,
            Icon = icon,
            LabelKey = labelKey,
            Kind = kind,
            Tab = tab,
            Group = group,
            GroupId = group,
            Order = order,
            Priority = priority,
            VisibleWhen = visibleWhen
        };
}
