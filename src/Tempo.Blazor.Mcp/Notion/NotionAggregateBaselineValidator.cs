using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal static class NotionAggregateBaselineValidator
{
    public static IReadOnlyList<NotionAggregateIssue> Validate(NotionAggregateWorkingSet workingSet)
    {
        var issues = new List<NotionAggregateIssue>();
        var globallySeenBlockIds = new HashSet<Guid>();
        var pages = workingSet.Pages.Values.OrderBy(page => page.Page.Id).ToList();

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = pages[pageIndex];
            var pagePath = $"$.pages[{pageIndex}]";
            if (page.SchemaVersion != NotionPageSnapshot.CurrentSchemaVersion)
            {
                Add(
                    "unsupported_schema_version",
                    $"Snapshot schema version {page.SchemaVersion} is not supported.",
                    $"{pagePath}.schemaVersion");
            }
            if (page.Page.Id == Guid.Empty)
            {
                Add("page_id_required", "Page id must be preallocated.", $"{pagePath}.page.id");
            }

            var byId = new Dictionary<Guid, NotionBlockSnapshot>();
            for (var blockIndex = 0; blockIndex < page.Blocks.Count; blockIndex++)
            {
                var block = page.Blocks[blockIndex];
                var blockPath = $"{pagePath}.blocks[{blockIndex}]";
                if (block.Id == Guid.Empty)
                {
                    Add("block_id_required", "Block id must be preallocated.", $"{blockPath}.id");
                }
                else if (!globallySeenBlockIds.Add(block.Id))
                {
                    Add("duplicate_block_id", $"Block id '{block.Id}' is duplicated.", $"{blockPath}.id");
                }

                if (block.PageId != page.Page.Id)
                {
                    Add(
                        "block_page_mismatch",
                        $"Block '{block.Id}' belongs to page '{block.PageId}', not '{page.Page.Id}'.",
                        $"{blockPath}.pageId");
                }
                if (block.Order < 0)
                {
                    Add("negative_block_order", "Block order cannot be negative.", $"{blockPath}.order");
                }
                if (block.Content.ValueKind is System.Text.Json.JsonValueKind.Undefined or
                    System.Text.Json.JsonValueKind.Null)
                {
                    Add("block_content_required", "Block content is required.", $"{blockPath}.content");
                }

                if (block.Id != Guid.Empty)
                {
                    byId.TryAdd(block.Id, block);
                }
            }

            for (var blockIndex = 0; blockIndex < page.Blocks.Count; blockIndex++)
            {
                var block = page.Blocks[blockIndex];
                var blockPath = $"{pagePath}.blocks[{blockIndex}]";
                if (block.ParentBlockId is { } parentId && !byId.ContainsKey(parentId))
                {
                    Add(
                        "parent_block_not_found",
                        $"Parent block '{parentId}' was not found on page '{page.Page.Id}'.",
                        $"{blockPath}.parentBlockId");
                    continue;
                }

                var visited = new HashSet<Guid> { block.Id };
                var cursor = block.ParentBlockId;
                while (cursor is { } current && byId.TryGetValue(current, out var parent))
                {
                    if (!visited.Add(current))
                    {
                        Add(
                            "block_parent_cycle",
                            $"Block '{block.Id}' participates in a parent cycle.",
                            $"{blockPath}.parentBlockId");
                        break;
                    }

                    cursor = parent.ParentBlockId;
                }
            }

            foreach (var siblings in page.Blocks.GroupBy(block => block.ParentBlockId))
            {
                var orders = siblings.OrderBy(block => block.Order).Select(block => block.Order).ToList();
                if (!orders.SequenceEqual(Enumerable.Range(0, orders.Count)))
                {
                    Add(
                        "non_contiguous_block_order",
                        "Sibling block order must be contiguous and zero-based.",
                        $"{pagePath}.blocks");
                }
            }
        }

        return issues;

        void Add(string code, string message, string path)
            => issues.Add(new NotionAggregateIssue
            {
                Code = code,
                Severity = NotionIssueSeverity.Error,
                Message = message,
                Path = path
            });
    }
}
