using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static class NotionCanonicalBlockBridge
{
    public static NotionPageSnapshot InsertBlocks(
        NotionPageSnapshot snapshot,
        IReadOnlyList<IPageBlock> inserted,
        Guid? afterBlockId)
    {
        var insertedIds = inserted.Select(block => block.Id).ToHashSet();
        var roots = inserted
            .Where(block => block.ParentBlockId is null)
            .OrderBy(block => block.Order)
            .ToList();
        var existingRoots = snapshot.Blocks
            .Where(block => block.ParentBlockId is null && !insertedIds.Contains(block.Id))
            .OrderBy(block => block.Order)
            .ThenBy(block => block.Id)
            .ToList();
        var afterIndex = afterBlockId is { } id
            ? existingRoots.FindIndex(block => block.Id == id)
            : -1;
        var insertionIndex = afterIndex >= 0
            ? afterIndex + 1
            : existingRoots.Count;
        insertionIndex = Math.Clamp(insertionIndex, 0, existingRoots.Count);

        var combinedRoots = existingRoots.ToList();
        combinedRoots.InsertRange(
            insertionIndex,
            roots.Select(ToSnapshot));
        for (var index = 0; index < combinedRoots.Count; index++)
        {
            combinedRoots[index].Order = index;
        }

        var retainedChildren = snapshot.Blocks
            .Where(block => block.ParentBlockId is not null && !insertedIds.Contains(block.Id))
            .ToList();
        var insertedChildren = inserted
            .Where(block => block.ParentBlockId is not null)
            .Select(ToSnapshot)
            .ToList();
        var combined = combinedRoots
            .Concat(retainedChildren)
            .Concat(insertedChildren)
            .ToList();
        foreach (var siblings in combined.GroupBy(block => block.ParentBlockId))
        {
            var order = 0;
            foreach (var sibling in siblings
                         .OrderBy(block => block.Order)
                         .ThenBy(block => block.Id))
            {
                sibling.Order = order++;
            }
        }

        snapshot.Blocks = combined
            .OrderBy(block => block.ParentBlockId)
            .ThenBy(block => block.Order)
            .ThenBy(block => block.Id)
            .ToList();
        return snapshot;
    }

    public static NotionBlockSnapshot ToSnapshot(IPageBlock block)
        => new()
        {
            Id = block.Id,
            PageId = block.PageId,
            ParentBlockId = block.ParentBlockId,
            Type = block.Type,
            Order = block.Order,
            CreatedAt = block.CreatedAt,
            LastEditedAt = block.LastEditedAt,
            Content = block.Type switch
            {
                BlockType.Table => JsonSerializer.SerializeToElement(
                    NotionCanonicalTableBridge.ToCanonicalTable(
                        (ITableBlockContent)block.Content),
                    NotionAggregateJson.Options),
                BlockType.TableRow => JsonSerializer.SerializeToElement(
                    NotionCanonicalTableBridge.ToCanonicalRow(
                        (ITableRowBlockContent)block.Content),
                    NotionAggregateJson.Options),
                _ => JsonSerializer.SerializeToElement(
                    block.Content,
                    block.Content.GetType(),
                    NotionAggregateJson.Options)
            }
        };

    public static PageBlock ToViewBlock(
        NotionPageSnapshot snapshot,
        NotionBlockSnapshot block)
    {
        if (block.Type == BlockType.Table)
        {
            return NotionCanonicalTableBridge.ToView(snapshot, block.Id).Table;
        }
        if (block.Type == BlockType.TableRow &&
            block.ParentBlockId is { } tableId &&
            snapshot.Blocks.Any(candidate =>
                candidate.Id == tableId &&
                candidate.Type == BlockType.Table))
        {
            return (PageBlock)NotionCanonicalTableBridge.ToView(snapshot, tableId)
                .Rows.Single(row => row.Id == block.Id);
        }

        return new PageBlock
        {
            Id = block.Id,
            PageId = block.PageId,
            ParentBlockId = block.ParentBlockId,
            Type = block.Type,
            Order = block.Order,
            CreatedAt = block.CreatedAt,
            LastEditedAt = block.LastEditedAt,
            Content = DeserializeContent(block)
        };
    }

    private static IBlockContent DeserializeContent(NotionBlockSnapshot block)
    {
        var contentType = block.Type switch
        {
            BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3 =>
                typeof(HeadingBlockContent),
            BlockType.BulletList or BlockType.NumberedList =>
                typeof(ListBlockContent),
            BlockType.Quote or BlockType.Paragraph =>
                typeof(TextBlockContent),
            BlockType.Callout => typeof(CalloutBlockContent),
            BlockType.Code => typeof(CodeBlockContent),
            BlockType.Divider => typeof(DividerBlockContent),
            BlockType.Equation => typeof(EquationBlockContent),
            BlockType.TodoItem => typeof(TodoBlockContent),
            BlockType.Toggle => typeof(ToggleBlockContent),
            BlockType.Image => typeof(ImageBlockContent),
            BlockType.Video => typeof(VideoBlockContent),
            BlockType.Audio => typeof(AudioBlockContent),
            BlockType.File => typeof(FileBlockContent),
            BlockType.Pdf => typeof(PdfBlockContent),
            BlockType.Bookmark => typeof(BookmarkBlockContent),
            BlockType.Embed => typeof(EmbedBlockContent),
            BlockType.ChildPage => typeof(ChildPageBlockContent),
            BlockType.LinkedPage => typeof(LinkedPageBlockContent),
            BlockType.Breadcrumb => typeof(BreadcrumbBlockContent),
            BlockType.SyncedBlockOrigin => typeof(SyncedBlockOriginContent),
            BlockType.SyncedBlockRef => typeof(SyncedBlockRefContent),
            BlockType.InlineDatabase => typeof(InlineDatabaseBlockContent),
            BlockType.LinkedDatabase => typeof(LinkedDatabaseBlockContent),
            BlockType.ColumnList => typeof(ColumnListBlockContent),
            BlockType.Column => typeof(ColumnBlockContent),
            BlockType.TemplateButton => typeof(TemplateButtonBlockContent),
            BlockType.TableOfContents => typeof(TableOfContentsBlockContent),
            BlockType.Diagram => typeof(DiagramBlockContent),
            BlockType.Wireframe => typeof(WireframeBlockContent),
            BlockType.Spreadsheet => typeof(SpreadsheetBlockContent),
            BlockType.WorkItem => typeof(WorkItemBlockContent),
            BlockType.ContentByLabel => typeof(ContentByLabelBlockContent),
            BlockType.IncludePage => typeof(IncludePageBlockContent),
            BlockType.ChildrenDisplay => typeof(ChildrenDisplayBlockContent),
            BlockType.Excerpt => typeof(ExcerptBlockContent),
            BlockType.ExcerptInclude => typeof(ExcerptIncludeBlockContent),
            BlockType.PageProperties => typeof(PagePropertiesBlockContent),
            BlockType.PagePropertiesReport => typeof(PagePropertiesReportBlockContent),
            _ => typeof(TextBlockContent)
        };

        return (IBlockContent?)JsonSerializer.Deserialize(
                   block.Content.GetRawText(),
                   contentType,
                   NotionAggregateJson.Options)
               ?? throw new InvalidDataException(
                   $"Block '{block.Id}' has no {block.Type} content.");
    }
}
