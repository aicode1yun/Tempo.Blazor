using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

public sealed record NotionBlockTypeSchema(
    BlockType Type,
    string ContentDiscriminator,
    string ContentType,
    string Description);

/// <summary>Maps Notion block types to their polymorphic content payloads.</summary>
public static class NotionBlockCatalog
{
    public static IReadOnlyList<NotionBlockTypeSchema> All { get; } =
    [
        Schema(BlockType.Paragraph, "text", typeof(TextBlockContent), "Paragraph text block."),
        Schema(BlockType.Heading1, "heading", typeof(HeadingBlockContent), "Heading level 1 block."),
        Schema(BlockType.Heading2, "heading", typeof(HeadingBlockContent), "Heading level 2 block."),
        Schema(BlockType.Heading3, "heading", typeof(HeadingBlockContent), "Heading level 3 block."),
        Schema(BlockType.Quote, "text", typeof(TextBlockContent), "Quote text block."),
        Schema(BlockType.Callout, "callout", typeof(CalloutBlockContent), "Callout block."),
        Schema(BlockType.Code, "code", typeof(CodeBlockContent), "Code block."),
        Schema(BlockType.Divider, "divider", typeof(DividerBlockContent), "Divider block."),
        Schema(BlockType.Equation, "equation", typeof(EquationBlockContent), "Equation block."),
        Schema(BlockType.BulletList, "list", typeof(ListBlockContent), "Bullet list item."),
        Schema(BlockType.NumberedList, "list", typeof(ListBlockContent), "Numbered list item."),
        Schema(BlockType.TodoItem, "todo", typeof(TodoBlockContent), "Todo item."),
        Schema(BlockType.Toggle, "toggle", typeof(ToggleBlockContent), "Toggle block."),
        Schema(BlockType.Table, "table", typeof(TableBlockContent), "Table block."),
        Schema(BlockType.TableRow, "tableRow", typeof(TableRowBlockContent), "Table row block."),
        Schema(BlockType.Image, "image", typeof(ImageBlockContent), "Image block."),
        Schema(BlockType.Video, "video", typeof(VideoBlockContent), "Video block."),
        Schema(BlockType.Audio, "audio", typeof(AudioBlockContent), "Audio block."),
        Schema(BlockType.File, "file", typeof(FileBlockContent), "File block."),
        Schema(BlockType.Pdf, "pdf", typeof(PdfBlockContent), "PDF block."),
        Schema(BlockType.Bookmark, "bookmark", typeof(BookmarkBlockContent), "Bookmark block."),
        Schema(BlockType.Embed, "embed", typeof(EmbedBlockContent), "Embed block."),
        Schema(BlockType.ChildPage, "childPage", typeof(ChildPageBlockContent), "Child page block."),
        Schema(BlockType.LinkedPage, "linkedPage", typeof(LinkedPageBlockContent), "Linked page block."),
        Schema(BlockType.Breadcrumb, "breadcrumb", typeof(BreadcrumbBlockContent), "Breadcrumb block."),
        Schema(BlockType.SyncedBlockOrigin, "syncedBlockOrigin", typeof(SyncedBlockOriginContent), "Synced block origin."),
        Schema(BlockType.SyncedBlockRef, "syncedBlockRef", typeof(SyncedBlockRefContent), "Synced block reference."),
        Schema(BlockType.InlineDatabase, "inlineDatabase", typeof(InlineDatabaseBlockContent), "Inline database block."),
        Schema(BlockType.LinkedDatabase, "linkedDatabase", typeof(LinkedDatabaseBlockContent), "Linked database block."),
        Schema(BlockType.ColumnList, "columnList", typeof(ColumnListBlockContent), "Column list block."),
        Schema(BlockType.Column, "column", typeof(ColumnBlockContent), "Column block."),
        Schema(BlockType.TemplateButton, "templateButton", typeof(TemplateButtonBlockContent), "Template button block."),
        Schema(BlockType.TableOfContents, "tableOfContents", typeof(TableOfContentsBlockContent), "Table of contents block."),
        Schema(BlockType.Diagram, "diagram", typeof(DiagramBlockContent), "Embedded diagram document block."),
        Schema(BlockType.Wireframe, "wireframe", typeof(WireframeBlockContent), "Embedded wireframe document block."),
        Schema(BlockType.Spreadsheet, "spreadsheet", typeof(SpreadsheetBlockContent), "Embedded spreadsheet document block."),
        Schema(BlockType.WorkItem, "workItem", typeof(WorkItemBlockContent), "Work item block."),
        Schema(BlockType.ContentByLabel, "contentByLabel", typeof(ContentByLabelBlockContent), "Content-by-label block."),
        Schema(BlockType.IncludePage, "includePage", typeof(IncludePageBlockContent), "Include page block."),
        Schema(BlockType.ChildrenDisplay, "childrenDisplay", typeof(ChildrenDisplayBlockContent), "Children display block."),
        Schema(BlockType.Excerpt, "excerpt", typeof(ExcerptBlockContent), "Excerpt source block."),
        Schema(BlockType.ExcerptInclude, "excerptInclude", typeof(ExcerptIncludeBlockContent), "Excerpt include block."),
        Schema(BlockType.PageProperties, "pageProperties", typeof(PagePropertiesBlockContent), "Page properties block."),
        Schema(BlockType.PagePropertiesReport, "pagePropertiesReport", typeof(PagePropertiesReportBlockContent), "Page properties report block.")
    ];

    public static NotionBlockTypeSchema? Get(BlockType type)
        => All.FirstOrDefault(s => s.Type == type);

    public static bool IsCompatible(BlockType type, IBlockContent content)
        => Get(type) is { } schema && string.Equals(schema.ContentType, content.GetType().Name, StringComparison.Ordinal);

    private static NotionBlockTypeSchema Schema(
        BlockType type,
        string discriminator,
        Type contentType,
        string description)
        => new(type, discriminator, contentType.Name, description);
}
