using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

public sealed record NotionValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Validates Notion pages and block trees for MCP write tools.</summary>
public static class NotionValidationEngine
{
    public static NotionValidationResult Validate(INotionPage? page, IReadOnlyList<IPageBlock> blocks)
    {
        var errors = new List<string>();
        if (page is not null)
        {
            if (page.Id == Guid.Empty)
            {
                errors.Add("page.id: page id is required.");
            }
            if (string.IsNullOrWhiteSpace(page.Title))
            {
                errors.Add("page.title: title is required.");
            }
        }

        ValidateBlocks(page?.Id, blocks, errors);
        return new NotionValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateBlocks(Guid? pageId, IReadOnlyList<IPageBlock> blocks, List<string> errors)
    {
        var ids = new HashSet<Guid>();
        var parentById = new Dictionary<Guid, Guid?>();
        var byParent = new Dictionary<string, List<IPageBlock>>(StringComparer.Ordinal);

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var path = $"blocks[{i}]";
            if (block.Id == Guid.Empty)
            {
                errors.Add($"{path}.id: block id is required.");
            }
            else if (!ids.Add(block.Id))
            {
                errors.Add($"{path}.id: duplicate block id '{block.Id}'.");
            }
            else
            {
                parentById[block.Id] = block.ParentBlockId;
            }

            if (pageId is not null && block.PageId != pageId.Value)
            {
                errors.Add($"{path}.pageId: expected page id '{pageId}', got '{block.PageId}'.");
            }
            if (block.Content is null)
            {
                errors.Add($"{path}.content: content is required.");
            }
            else if (!NotionBlockCatalog.IsCompatible(block.Type, block.Content))
            {
                errors.Add($"{path}.content: content type '{block.Content.GetType().Name}' is not compatible with block type '{block.Type}'.");
            }
            else
            {
                ValidateEmbeddedReference(block, path, errors);
            }

            var parentKey = ParentKey(block.ParentBlockId);
            if (!byParent.TryGetValue(parentKey, out var siblings))
            {
                siblings = [];
                byParent[parentKey] = siblings;
            }
            siblings.Add(block);
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.ParentBlockId is not null && !ids.Contains(block.ParentBlockId.Value))
            {
                errors.Add($"blocks[{i}].parentBlockId: references missing block '{block.ParentBlockId}'.");
            }
            else if (block.ParentBlockId == block.Id)
            {
                errors.Add($"blocks[{i}].parentBlockId: block cannot be its own parent.");
            }
        }

        ValidateCycles(parentById, errors);

        foreach (var (parentKey, siblings) in byParent)
        {
            var orders = new HashSet<int>();
            foreach (var block in siblings)
            {
                if (!orders.Add(block.Order))
                {
                    errors.Add($"blocks: duplicate order {block.Order} under parent '{parentKey}'.");
                }
            }
        }
    }

    private static string ParentKey(Guid? parentId)
        => parentId?.ToString() ?? "root";

    private static void ValidateEmbeddedReference(IPageBlock block, string path, List<string> errors)
    {
        switch (block.Content)
        {
            case DiagramBlockContent diagram when diagram.DiagramDocumentId == Guid.Empty:
                errors.Add($"{path}.content.diagramDocumentId: embedded diagram reference is required.");
                break;
            case WireframeBlockContent wireframe when wireframe.WireframeDocumentId == Guid.Empty:
                errors.Add($"{path}.content.wireframeDocumentId: embedded wireframe reference is required.");
                break;
            case SpreadsheetBlockContent spreadsheet when spreadsheet.SpreadsheetDocumentId == Guid.Empty:
                errors.Add($"{path}.content.spreadsheetDocumentId: embedded spreadsheet reference is required.");
                break;
        }
    }

    private static void ValidateCycles(Dictionary<Guid, Guid?> parentById, List<string> errors)
    {
        foreach (var id in parentById.Keys)
        {
            var seen = new HashSet<Guid>();
            var current = id;
            while (parentById.TryGetValue(current, out var parent) && parent is not null)
            {
                if (!seen.Add(current))
                {
                    errors.Add($"blocks: parent cycle detected at block '{id}'.");
                    break;
                }

                current = parent.Value;
            }
        }
    }
}
