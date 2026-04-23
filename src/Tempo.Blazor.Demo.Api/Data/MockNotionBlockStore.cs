using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockNotionBlockStore
{
    private readonly Dictionary<Guid, PageBlock> _blocks = new();

    public MockNotionBlockStore()
    {
        InitializeMockBlocks();
    }

    private void InitializeMockBlocks()
    {
        var pageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var blocks = new[]
        {
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Heading1,
                Order = 0,
                Content = new HeadingBlockContent { Level = 1, Html = "Welcome to Notion Editor" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Paragraph,
                Order = 1,
                Content = new TextBlockContent { Html = "This is a demo paragraph. You can edit this text, use slash commands to add new blocks, and test various features." },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Heading2,
                Order = 2,
                Content = new HeadingBlockContent { Level = 2, Html = "Features to Test" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.BulletList,
                Order = 3,
                Content = new ListBlockContent { Html = "Press / to open slash commands" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.BulletList,
                Order = 4,
                Content = new ListBlockContent { Html = "Press Enter to create a new block" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.BulletList,
                Order = 5,
                Content = new ListBlockContent { Html = "Drag blocks to reorder them" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Heading2,
                Order = 6,
                Content = new HeadingBlockContent { Level = 2, Html = "Text Formatting" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Paragraph,
                Order = 7,
                Content = new TextBlockContent
                {
                    Html = "Select text to see the inline toolbar. Try making text bold or italic."
                },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Divider,
                Order = 8,
                Content = new DividerBlockContent(),
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Paragraph,
                Order = 9,
                Content = new TextBlockContent
                {
                    Html = "Try pressing Ctrl+Z to undo or Ctrl+Y to redo."
                },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Heading2,
                Order = 10,
                Content = new HeadingBlockContent { Level = 2, Html = "PDF Viewer" },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            },
            new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                ParentBlockId = null,
                Type = BlockType.Pdf,
                Order = 11,
                Content = new PdfBlockContent
                {
                    Url = "https://raw.githubusercontent.com/mozilla/pdf.js/master/web/compressed.tracemonkey-pldi-09.pdf",
                    Caption = "TraceMonkey — demo PDF (Mozilla)"
                },
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            }
        };

        foreach (var block in blocks)
        {
            _blocks[block.Id] = block;
        }
    }

    public async Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
    {
        if (Guid.TryParse(pageId, out var id))
        {
            var blocks = _blocks.Values
                .Where(b => b.PageId == id && b.ParentBlockId == null)
                .OrderBy(b => b.Order)
                .Cast<IPageBlock>();
            return await Task.FromResult(blocks);
        }
        return await Task.FromResult(Array.Empty<IPageBlock>());
    }

    public async Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
    {
        if (Guid.TryParse(parentBlockId, out var id))
        {
            var children = _blocks.Values
                .Where(b => b.ParentBlockId == id)
                .OrderBy(b => b.Order)
                .Cast<IPageBlock>();
            return await Task.FromResult(children);
        }
        return await Task.FromResult(Array.Empty<IPageBlock>());
    }

    public async Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
    {
        var pageGuid = Guid.Parse(pageId);
        var newBlock = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = pageGuid,
            ParentBlockId = block.ParentBlockId,
            Type = block.Type,
            Order = GetNextOrder(pageGuid, block.ParentBlockId),
            Content = block.Content,
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };

        _blocks[newBlock.Id] = newBlock;
        return await Task.FromResult(newBlock);
    }

    public async Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
    {
        var pageGuid = Guid.Parse(pageId);
        var createdBlocks = new List<IPageBlock>();

        foreach (var block in blocks)
        {
            var newBlock = new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = pageGuid,
                ParentBlockId = block.ParentBlockId,
                Type = block.Type,
                Order = GetNextOrder(pageGuid, block.ParentBlockId),
                Content = block.Content,
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            };

            _blocks[newBlock.Id] = newBlock;
            createdBlocks.Add(newBlock);
        }

        return await Task.FromResult(createdBlocks);
    }

    public async Task UpdateBlockAsync(IPageBlock block)
    {
        if (block is PageBlock pageBlock && _blocks.ContainsKey(pageBlock.Id))
        {
            pageBlock.LastEditedAt = DateTime.UtcNow;
            _blocks[pageBlock.Id] = pageBlock;
        }
        await Task.CompletedTask;
    }

    public async Task DeleteBlockAsync(string blockId)
    {
        if (Guid.TryParse(blockId, out var id))
        {
            _blocks.Remove(id);
        }
        await Task.CompletedTask;
    }

    public async Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds)
    {
        var order = 0;
        foreach (var blockId in orderedBlockIds)
        {
            if (Guid.TryParse(blockId, out var id) && _blocks.TryGetValue(id, out var block))
            {
                block.Order = order++;
                _blocks[id] = block;
            }
        }
        await Task.CompletedTask;
    }

    public async Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId)
    {
        if (Guid.TryParse(blockId, out var bid) && Guid.TryParse(targetPageId, out var pid))
        {
            if (_blocks.TryGetValue(bid, out var block))
            {
                block.PageId = pid;
                block.Order = GetNextOrder(pid, null);
                _blocks[bid] = block;
            }
        }
        await Task.CompletedTask;
    }

    public async Task<IPageBlock> DuplicateBlockAsync(string blockId)
    {
        if (Guid.TryParse(blockId, out var id) && _blocks.TryGetValue(id, out var originalBlock))
        {
            var duplicated = new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = originalBlock.PageId,
                ParentBlockId = originalBlock.ParentBlockId,
                Type = originalBlock.Type,
                Order = originalBlock.Order + 1,
                Content = originalBlock.Content,
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            };

            _blocks[duplicated.Id] = duplicated;
            return await Task.FromResult(duplicated);
        }

        throw new KeyNotFoundException($"Block {blockId} not found");
    }

    public async Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
    {
        if (Guid.TryParse(blockId, out var id) && _blocks.TryGetValue(id, out var block))
        {
            block.Type = newType;
            block.Content = CreateDefaultContent(newType);
            block.LastEditedAt = DateTime.UtcNow;
            _blocks[id] = block;
            return await Task.FromResult(block);
        }

        throw new KeyNotFoundException($"Block {blockId} not found");
    }

    public async Task<string> GetBlockLinkAsync(string blockId)
    {
        return await Task.FromResult($"https://notion.demo/block/{blockId}");
    }

    private int GetNextOrder(Guid pageId, Guid? parentBlockId)
    {
        var maxOrder = _blocks.Values
            .Where(b => b.PageId == pageId && b.ParentBlockId == parentBlockId)
            .Max(b => (int?)b.Order) ?? -1;
        return maxOrder + 1;
    }

    private static IBlockContent CreateDefaultContent(BlockType type)
    {
        return type switch
        {
            BlockType.Heading1 => new HeadingBlockContent { Level = 1, Html = "" },
            BlockType.Heading2 => new HeadingBlockContent { Level = 2, Html = "" },
            BlockType.Heading3 => new HeadingBlockContent { Level = 3, Html = "" },
            BlockType.Paragraph => new TextBlockContent { Html = "" },
            BlockType.BulletList or BlockType.NumberedList => new ListBlockContent { Html = "" },
            BlockType.Divider => new DividerBlockContent(),
            BlockType.Code => new CodeBlockContent { Language = "plaintext" },
            BlockType.Image => new ImageBlockContent(),
            BlockType.Video => new VideoBlockContent(),
            BlockType.Audio => new AudioBlockContent(),
            BlockType.File => new FileBlockContent(),
            BlockType.Pdf => new PdfBlockContent(),
            _ => new TextBlockContent { Html = "" }
        };
    }
}
