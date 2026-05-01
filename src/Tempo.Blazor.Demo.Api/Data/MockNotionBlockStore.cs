using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockNotionBlockStore
{
    private readonly Dictionary<Guid, PageBlock> _blocks = new();

    // ── Fixed IDs for blocks that need stable cross-references ────────────────

    private static readonly Guid _pageId        = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _columnListId  = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _col1Id        = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _col2Id        = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public MockNotionBlockStore()
    {
        InitializeMockBlocks();
    }

    private void InitializeMockBlocks()
    {
        var now = DateTime.UtcNow;

        // ── Phase 1 blocks ────────────────────────────────────────────────────

        Add(BlockType.Heading1,   0, new HeadingBlockContent { Level = 1, Html = "Welcome to Notion Editor" });
        Add(BlockType.Paragraph,  1, new TextBlockContent
        {
            Html = "This is a demo paragraph. You can edit this text, use slash commands to add new blocks, and test various features."
        });
        Add(BlockType.Heading2,   2, new HeadingBlockContent { Level = 2, Html = "Features to Test" });
        Add(BlockType.BulletList, 3, new ListBlockContent { Html = "Press / to open slash commands" });
        Add(BlockType.BulletList, 4, new ListBlockContent { Html = "Press Enter to create a new block" });
        Add(BlockType.BulletList, 5, new ListBlockContent { Html = "Drag blocks to reorder them" });
        Add(BlockType.Heading2,   6, new HeadingBlockContent { Level = 2, Html = "Text Formatting" });
        Add(BlockType.Paragraph,  7, new TextBlockContent
        {
            Html = "Select text to see the inline toolbar. Try making text <strong>bold</strong> or <em>italic</em>."
        });
        Add(BlockType.Divider,    8, new DividerBlockContent());
        Add(BlockType.Paragraph,  9, new TextBlockContent
        {
            Html = "Try pressing <kbd>Ctrl+Z</kbd> to undo or <kbd>Ctrl+Y</kbd> to redo."
        });
        Add(BlockType.Heading2,  10, new HeadingBlockContent { Level = 2, Html = "PDF Viewer" });
        Add(BlockType.Pdf,       11, new PdfBlockContent
        {
            Url     = "https://raw.githubusercontent.com/mozilla/pdf.js/master/web/compressed.tracemonkey-pldi-09.pdf",
            Caption = "TraceMonkey — demo PDF (Mozilla)"
        });

        // ── Phase 2: Media Blocks ─────────────────────────────────────────────

        Add(BlockType.Heading2, 12, new HeadingBlockContent { Level = 2, Html = "Phase 2: Media Blocks" });

        Add(BlockType.Image, 13, new ImageBlockContent
        {
            Url     = "https://images.unsplash.com/photo-1555949963-ff9fe0c870eb?w=1200&q=80",
            AltText = "Software developer at laptop",
            Caption = "Click the image to resize or change alignment",
            Width   = 700
        });

        Add(BlockType.Video, 14, new VideoBlockContent
        {
            Provider = VideoProvider.YouTube,
            Url      = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            Caption  = "YouTube embed demo — click to play",
            Width    = 700
        });

        Add(BlockType.File, 15, new FileBlockContent
        {
            FileName      = "sample-report.pdf",
            FileSizeBytes = 2_457_600,
            ContentType   = "application/pdf",
            Url           = "https://raw.githubusercontent.com/mozilla/pdf.js/master/web/compressed.tracemonkey-pldi-09.pdf",
            Caption       = "Sample PDF report (2.4 MB)"
        });

        // ── Phase 2: Embeds ───────────────────────────────────────────────────

        Add(BlockType.Heading2, 16, new HeadingBlockContent { Level = 2, Html = "Phase 2: Embeds" });

        Add(BlockType.Bookmark, 17, new BookmarkBlockContent
        {
            Url          = "https://github.com/anthropics/anthropic-sdk-python",
            Title        = "anthropics/anthropic-sdk-python",
            Description  = "The official Python library for the Anthropic API. Access Claude and other Anthropic models via a typed Python client.",
            Domain       = "github.com",
            FaviconUrl   = "https://github.com/favicon.ico",
            CoverImageUrl = "https://opengraph.githubassets.com/1/anthropics/anthropic-sdk-python",
            Caption      = "Anthropic Python SDK on GitHub"
        });

        Add(BlockType.Embed, 18, new EmbedBlockContent
        {
            Provider = EmbedProvider.CodePen,
            Url      = "https://codepen.io/alvaromontoro/embed/vYexLGV",
            Width    = 700,
            Height   = 400,
            Caption  = "CodePen embed — interactive CSS demo"
        });

        // ── Phase 2: Layout — 2 Columns ───────────────────────────────────────

        Add(BlockType.Heading2, 19, new HeadingBlockContent { Level = 2, Html = "Phase 2: Layout — 2 Columns" });

        // ColumnList root block (fixed ID so Column children can reference it)
        _blocks[_columnListId] = MakeBlock(
            id:           _columnListId,
            pageId:       _pageId,
            parentBlockId: null,
            type:         BlockType.ColumnList,
            order:        20,
            content:      new ColumnListBlockContent { ColumnCount = 2 }
        );

        // Column 1 — child of ColumnList
        _blocks[_col1Id] = MakeBlock(
            id:           _col1Id,
            pageId:       _pageId,
            parentBlockId: _columnListId,
            type:         BlockType.Column,
            order:        0,
            content:      new ColumnBlockContent { ColumnIndex = 0, WidthPercent = 50 }
        );

        // Content in Column 1
        var col1Para1 = Guid.NewGuid();
        _blocks[col1Para1] = MakeBlock(
            id:           col1Para1,
            pageId:       _pageId,
            parentBlockId: _col1Id,
            type:         BlockType.Heading3,
            order:        0,
            content:      new HeadingBlockContent { Level = 3, Html = "Left Column" }
        );
        var col1Para2 = Guid.NewGuid();
        _blocks[col1Para2] = MakeBlock(
            id:           col1Para2,
            pageId:       _pageId,
            parentBlockId: _col1Id,
            type:         BlockType.Paragraph,
            order:        1,
            content:      new TextBlockContent
            {
                Html = "This content is in the <strong>left column</strong>. Drag the divider between columns to resize. You can add any block type inside a column."
            }
        );
        var col1Todo = Guid.NewGuid();
        _blocks[col1Todo] = MakeBlock(
            id:           col1Todo,
            pageId:       _pageId,
            parentBlockId: _col1Id,
            type:         BlockType.TodoItem,
            order:        2,
            content:      new TodoBlockContent { Html = "Task in left column", IsChecked = true }
        );

        // Column 2 — child of ColumnList
        _blocks[_col2Id] = MakeBlock(
            id:           _col2Id,
            pageId:       _pageId,
            parentBlockId: _columnListId,
            type:         BlockType.Column,
            order:        1,
            content:      new ColumnBlockContent { ColumnIndex = 1, WidthPercent = 50 }
        );

        // Content in Column 2
        var col2Para1 = Guid.NewGuid();
        _blocks[col2Para1] = MakeBlock(
            id:           col2Para1,
            pageId:       _pageId,
            parentBlockId: _col2Id,
            type:         BlockType.Heading3,
            order:        0,
            content:      new HeadingBlockContent { Level = 3, Html = "Right Column" }
        );
        var col2Para2 = Guid.NewGuid();
        _blocks[col2Para2] = MakeBlock(
            id:           col2Para2,
            pageId:       _pageId,
            parentBlockId: _col2Id,
            type:         BlockType.Paragraph,
            order:        1,
            content:      new TextBlockContent
            {
                Html = "This content is in the <strong>right column</strong>. Columns support the full set of block types including nested toggles and lists."
            }
        );
        var col2Bullet1 = Guid.NewGuid();
        _blocks[col2Bullet1] = MakeBlock(
            id:           col2Bullet1,
            pageId:       _pageId,
            parentBlockId: _col2Id,
            type:         BlockType.BulletList,
            order:        2,
            content:      new ListBlockContent { Html = "Feature A" }
        );
        var col2Bullet2 = Guid.NewGuid();
        _blocks[col2Bullet2] = MakeBlock(
            id:           col2Bullet2,
            pageId:       _pageId,
            parentBlockId: _col2Id,
            type:         BlockType.BulletList,
            order:        3,
            content:      new ListBlockContent { Html = "Feature B" }
        );

        // ── Phase 2: Equation (LaTeX) ─────────────────────────────────────────

        Add(BlockType.Heading2, 21, new HeadingBlockContent { Level = 2, Html = "Phase 2: Equation (LaTeX)" });

        Add(BlockType.Equation, 22, new EquationBlockContent
        {
            Expression = @"E = mc^2"
        });

        Add(BlockType.Equation, 23, new EquationBlockContent
        {
            Expression = @"\int_{-\infty}^{\infty} e^{-x^2}\,dx = \sqrt{\pi}"
        });

        Add(BlockType.Equation, 24, new EquationBlockContent
        {
            Expression = @"x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}"
        });

        // ── Phase 2: Callout & Code ───────────────────────────────────────────

        Add(BlockType.Heading2, 25, new HeadingBlockContent { Level = 2, Html = "Phase 2: Code & Callout" });

        Add(BlockType.Callout, 26, new CalloutBlockContent
        {
            IconEmoji       = "💡",
            Html            = "This is a callout block. Use it to highlight important information.",
            BackgroundColor = "blue"
        });

        Add(BlockType.Code, 27, new CodeBlockContent
        {
            Language = "C#",
            Code     = "var equation = new EquationBlockContent\n{\n    Expression = @\"E = mc^2\"\n};\nconsole.WriteLine(equation.Expression);"
        });

        // ── Phase 2: Diagram & Wireframe ──────────────────────────────────────

        Add(BlockType.Heading2, 28, new HeadingBlockContent { Level = 2, Html = "Phase 2: Diagram &amp; Wireframe" });

        Add(BlockType.Paragraph, 29, new TextBlockContent
        {
            Html = "The blocks below show the Diagram and Wireframe editors. Click <strong>Create Diagram</strong> or <strong>Create Wireframe</strong> to open the full-screen editor. After saving, an SVG preview is shown inline."
        });

        Add(BlockType.Diagram, 30, new DiagramBlockContent());

        Add(BlockType.Wireframe, 31, new WireframeBlockContent());

        // ── Phase 3: Inline Database ──────────────────────────────────────────

        Add(BlockType.Heading2, 32, new HeadingBlockContent { Level = 2, Html = "Phase 3: Inline Database" });

        Add(BlockType.Paragraph, 33, new TextBlockContent
        {
            Html = "The database below is fully functional. Switch views using the tabs, filter and sort records, edit fields, open record details, and test import/export."
        });

        Add(BlockType.InlineDatabase, 34, new InlineDatabaseBlockContent
        {
            DatabaseId   = MockNotionDatabaseStore.DbId,
            Title        = "Project Tasks",
            IconEmoji    = "📋",
            ActiveViewId = Guid.Parse("e0000000-0000-0000-0000-000000000001")
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Add(BlockType type, int order, IBlockContent content)
    {
        var id = Guid.NewGuid();
        _blocks[id] = MakeBlock(id, _pageId, null, type, order, content);
    }

    private static PageBlock MakeBlock(Guid id, Guid pageId, Guid? parentBlockId,
        BlockType type, int order, IBlockContent content) => new()
    {
        Id            = id,
        PageId        = pageId,
        ParentBlockId = parentBlockId,
        Type          = type,
        Order         = order,
        Content       = content,
        CreatedAt     = DateTime.UtcNow,
        LastEditedAt  = DateTime.UtcNow
    };

    // ── Public API ────────────────────────────────────────────────────────────

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
            Id            = Guid.NewGuid(),
            PageId        = pageGuid,
            ParentBlockId = block.ParentBlockId,
            Type          = block.Type,
            Order         = GetNextOrder(pageGuid, block.ParentBlockId),
            Content       = block.Content,
            CreatedAt     = DateTime.UtcNow,
            LastEditedAt  = DateTime.UtcNow
        };

        _blocks[newBlock.Id] = newBlock;
        return await Task.FromResult(newBlock);
    }

    public async Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
    {
        var pageGuid = Guid.Parse(pageId);
        var created  = new List<IPageBlock>();

        foreach (var block in blocks)
        {
            var newBlock = new PageBlock
            {
                Id            = Guid.NewGuid(),
                PageId        = pageGuid,
                ParentBlockId = block.ParentBlockId,
                Type          = block.Type,
                Order         = GetNextOrder(pageGuid, block.ParentBlockId),
                Content       = block.Content,
                CreatedAt     = DateTime.UtcNow,
                LastEditedAt  = DateTime.UtcNow
            };

            _blocks[newBlock.Id] = newBlock;
            created.Add(newBlock);
        }

        return await Task.FromResult(created);
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
            _blocks.Remove(id);
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
                block.Order  = GetNextOrder(pid, null);
                _blocks[bid] = block;
            }
        }
        await Task.CompletedTask;
    }

    public async Task<IPageBlock> DuplicateBlockAsync(string blockId)
    {
        if (Guid.TryParse(blockId, out var id) && _blocks.TryGetValue(id, out var src))
        {
            var dup = new PageBlock
            {
                Id            = Guid.NewGuid(),
                PageId        = src.PageId,
                ParentBlockId = src.ParentBlockId,
                Type          = src.Type,
                Order         = src.Order + 1,
                Content       = src.Content,
                CreatedAt     = DateTime.UtcNow,
                LastEditedAt  = DateTime.UtcNow
            };

            _blocks[dup.Id] = dup;
            return await Task.FromResult(dup);
        }

        throw new KeyNotFoundException($"Block {blockId} not found");
    }

    public async Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
    {
        if (Guid.TryParse(blockId, out var id) && _blocks.TryGetValue(id, out var block))
        {
            block.Type          = newType;
            block.Content       = CreateDefaultContent(newType);
            block.LastEditedAt  = DateTime.UtcNow;
            _blocks[id]         = block;
            return await Task.FromResult(block);
        }

        throw new KeyNotFoundException($"Block {blockId} not found");
    }

    public async Task<string> GetBlockLinkAsync(string blockId)
    {
        return await Task.FromResult($"https://notion.demo/block/{blockId}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private int GetNextOrder(Guid pageId, Guid? parentBlockId)
    {
        var max = _blocks.Values
            .Where(b => b.PageId == pageId && b.ParentBlockId == parentBlockId)
            .Max(b => (int?)b.Order) ?? -1;
        return max + 1;
    }

    private static IBlockContent CreateDefaultContent(BlockType type) => type switch
    {
        BlockType.Heading1                                          => new HeadingBlockContent { Level = 1 },
        BlockType.Heading2                                          => new HeadingBlockContent { Level = 2 },
        BlockType.Heading3                                          => new HeadingBlockContent { Level = 3 },
        BlockType.Paragraph                                         => new TextBlockContent(),
        BlockType.BulletList or BlockType.NumberedList              => new ListBlockContent(),
        BlockType.Divider                                           => new DividerBlockContent(),
        BlockType.Code                                              => new CodeBlockContent { Language = "Plain Text" },
        BlockType.Image                                             => new ImageBlockContent(),
        BlockType.Video                                             => new VideoBlockContent(),
        BlockType.Audio                                             => new AudioBlockContent(),
        BlockType.File                                              => new FileBlockContent(),
        BlockType.Pdf                                               => new PdfBlockContent(),
        BlockType.Equation                                          => new EquationBlockContent(),
        BlockType.Bookmark                                          => new BookmarkBlockContent(),
        BlockType.Embed                                             => new EmbedBlockContent(),
        BlockType.ColumnList                                        => new ColumnListBlockContent { ColumnCount = 2 },
        BlockType.Column                                            => new ColumnBlockContent(),
        BlockType.Diagram                                           => new DiagramBlockContent(),
        BlockType.Wireframe                                         => new WireframeBlockContent(),
        _                                                           => new TextBlockContent()
    };
}
