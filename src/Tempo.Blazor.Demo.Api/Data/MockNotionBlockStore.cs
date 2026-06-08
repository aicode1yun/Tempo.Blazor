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
            Url     = "https://mozilla.github.io/pdf.js/web/compressed.tracemonkey-pldi-09.pdf",
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
            Url           = "https://mozilla.github.io/pdf.js/web/compressed.tracemonkey-pldi-09.pdf",
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

        // ── Phase 2: Spreadsheet ──────────────────────────────────────────────

        Add(BlockType.Heading2, 31_3, new HeadingBlockContent { Level = 2, Html = "Phase 2: Spreadsheet Block" });

        Add(BlockType.Paragraph, 31_4, new TextBlockContent
        {
            Html = "The block below shows the Spreadsheet editor. Click <strong>Create Spreadsheet</strong> to open the full-screen editor. After saving, an embedded live spreadsheet is shown inline."
        });

        Add(BlockType.Spreadsheet, 31_5, new SpreadsheetBlockContent());

        // ── Phase 3: Table ────────────────────────────────────────────────────

        Add(BlockType.Heading2, 31_1, new HeadingBlockContent { Level = 2, Html = "Phase 3: Table" });

        var tableId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        _blocks[tableId] = MakeBlock(
            id:           tableId,
            pageId:       _pageId,
            parentBlockId: null,
            type:         BlockType.Table,
            order:        31_2,
            content:      new TableBlockContent { ColumnCount = 3, HasHeaderRow = true, HasHeaderColumn = false }
        );

        var tableRow1 = Guid.NewGuid();
        _blocks[tableRow1] = MakeBlock(
            id:           tableRow1,
            pageId:       _pageId,
            parentBlockId: tableId,
            type:         BlockType.TableRow,
            order:        0,
            content:      new TableRowBlockContent { Cells = ["Name", "Status", "Priority"] }
        );

        var tableRow2 = Guid.NewGuid();
        _blocks[tableRow2] = MakeBlock(
            id:           tableRow2,
            pageId:       _pageId,
            parentBlockId: tableId,
            type:         BlockType.TableRow,
            order:        1,
            content:      new TableRowBlockContent { Cells = ["Auth refactor", "In Progress", "High"] }
        );

        var tableRow3 = Guid.NewGuid();
        _blocks[tableRow3] = MakeBlock(
            id:           tableRow3,
            pageId:       _pageId,
            parentBlockId: tableId,
            type:         BlockType.TableRow,
            order:        2,
            content:      new TableRowBlockContent { Cells = ["Dark mode", "Done", "Low"] }
        );

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

        // ── Phase 4: Comments, Search, History hints ──────────────────────────

        Add(BlockType.Heading2, 35, new HeadingBlockContent { Level = 2, Html = "Phase 4: Comments &amp; History" });
        Add(BlockType.Callout, 36, new CalloutBlockContent
        {
            IconEmoji = "💬",
            Html = "Try the comment panel: hover a block and click the comment icon. Page history is available via the ⚙️ settings button (top-right). Use <kbd>Ctrl+P</kbd> to search pages.",
            BackgroundColor = "blue"
        });

        // ── Page 2 — Product Roadmap ──────────────────────────────────────────
        AddTo(MockNotionDataStore.Page2Id, BlockType.Heading1, 0,
            new HeadingBlockContent { Level = 1, Html = "Product Roadmap" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.Callout, 1,
            new CalloutBlockContent { IconEmoji = "📌", Html = "Living document — updated every sprint. Last reviewed by <strong>Alice Johnson</strong>.", BackgroundColor = "yellow" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.Heading2, 2,
            new HeadingBlockContent { Level = 2, Html = "Q1 2025 — In Progress" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.TodoItem, 3,
            new TodoBlockContent { Html = "Notion-style block editor — Phase 4 providers", IsChecked = false });
        AddTo(MockNotionDataStore.Page2Id, BlockType.TodoItem, 4,
            new TodoBlockContent { Html = "Collaborative cursors & real-time sync", IsChecked = false });
        AddTo(MockNotionDataStore.Page2Id, BlockType.TodoItem, 5,
            new TodoBlockContent { Html = "Export to PDF via QuestPDF", IsChecked = true });
        AddTo(MockNotionDataStore.Page2Id, BlockType.Heading2, 6,
            new HeadingBlockContent { Level = 2, Html = "Q2 2025 — Planned" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.BulletList, 7,
            new ListBlockContent { Html = "Mobile-responsive layouts" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.BulletList, 8,
            new ListBlockContent { Html = "Notion API integration bridge" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.BulletList, 9,
            new ListBlockContent { Html = "AI-powered block suggestions" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.Divider, 10,
            new DividerBlockContent());
        AddTo(MockNotionDataStore.Page2Id, BlockType.Paragraph, 11,
            new TextBlockContent { Html = "Questions? Ping <strong>Alice Johnson</strong> on Slack." });

        // ── Page 3 — Meeting Notes ────────────────────────────────────────────
        AddTo(MockNotionDataStore.Page3Id, BlockType.Heading1, 0,
            new HeadingBlockContent { Level = 1, Html = "Weekly Team Meeting" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.Paragraph, 1,
            new TextBlockContent { Html = "<em>Date: May 5, 2025 · Attendees: Alice, Bob, Charlie, Diana</em>" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.Heading2, 2,
            new HeadingBlockContent { Level = 2, Html = "Agenda" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.NumberedList, 3,
            new ListBlockContent { Html = "Sprint retrospective" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.NumberedList, 4,
            new ListBlockContent { Html = "Phase 4 demo walkthrough" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.NumberedList, 5,
            new ListBlockContent { Html = "Q2 planning kick-off" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.Heading2, 6,
            new HeadingBlockContent { Level = 2, Html = "Action Items" });
        AddTo(MockNotionDataStore.Page3Id, BlockType.TodoItem, 7,
            new TodoBlockContent { Html = "Bob: merge comment provider PR by EOD", IsChecked = false });
        AddTo(MockNotionDataStore.Page3Id, BlockType.TodoItem, 8,
            new TodoBlockContent { Html = "Alice: update roadmap with Q2 items", IsChecked = true });
        AddTo(MockNotionDataStore.Page3Id, BlockType.TodoItem, 9,
            new TodoBlockContent { Html = "Charlie: write Architecture Guide first draft", IsChecked = false });

        // ── Phase 12: Navigation blocks on Page 1 ────────────────────────────

        Add(BlockType.Heading2, 37, new HeadingBlockContent { Level = 2, Html = "Phase 12: Navigation Blocks" });

        Add(BlockType.ChildPage, 38, new ChildPageBlockContent
        {
            ChildPageId = MockNotionDataStore.Page2Id,
            Title       = "Product Roadmap",
            IconEmoji   = "📌"
        });

        Add(BlockType.LinkedPage, 39, new LinkedPageBlockContent
        {
            LinkedPageId = MockNotionDataStore.Page3Id,
            Title        = "Meeting Notes",
            IconEmoji    = "🗒️"
        });

        Add(BlockType.Breadcrumb, 40, new BreadcrumbBlockContent());

        // ── Phase 13: Special blocks on Page 1 ───────────────────────────────

        Add(BlockType.TableOfContents, 41, new TableOfContentsBlockContent { MaxLevel = 3 });

        Add(BlockType.TemplateButton, 42, new TemplateButtonBlockContent
        {
            Label = "Demo Template",
            TemplateBlocks = new List<PageBlock>
            {
                new PageBlock
                {
                    Id           = Guid.NewGuid(),
                    PageId       = _pageId,
                    Type         = BlockType.Heading2,
                    Order        = 0,
                    Content      = new HeadingBlockContent { Level = 2, Html = "Section Title" },
                    CreatedAt    = DateTime.UtcNow,
                    LastEditedAt = DateTime.UtcNow
                },
                new PageBlock
                {
                    Id           = Guid.NewGuid(),
                    PageId       = _pageId,
                    Type         = BlockType.Paragraph,
                    Order        = 1,
                    Content      = new TextBlockContent { Html = "Write your content here." },
                    CreatedAt    = DateTime.UtcNow,
                    LastEditedAt = DateTime.UtcNow
                }
            }
        });

        // ── Page 4 — Engineering Wiki ─────────────────────────────────────────
        AddTo(MockNotionDataStore.Page4Id, BlockType.Heading1, 0,
            new HeadingBlockContent { Level = 1, Html = "Engineering Wiki" });
        AddTo(MockNotionDataStore.Page4Id, BlockType.Paragraph, 1,
            new TextBlockContent { Html = "Central hub for technical documentation, architecture decisions, and developer guides." });
        AddTo(MockNotionDataStore.Page4Id, BlockType.Heading2, 2,
            new HeadingBlockContent { Level = 2, Html = "Sub-pages" });
        AddTo(MockNotionDataStore.Page4Id, BlockType.BulletList, 3,
            new ListBlockContent { Html = "🏗️ Architecture Guide — system design and patterns" });
        AddTo(MockNotionDataStore.Page4Id, BlockType.BulletList, 4,
            new ListBlockContent { Html = "🔧 Development Setup — local env and tooling" });
        AddTo(MockNotionDataStore.Page4Id, BlockType.Heading2, 5,
            new HeadingBlockContent { Level = 2, Html = "Tech Stack" });
        AddTo(MockNotionDataStore.Page4Id, BlockType.Code, 6,
            new CodeBlockContent { Language = "yaml", Code = "frontend:\n  framework: Blazor (.NET 9)\n  component-lib: Tempo.Blazor\nbackend:\n  api: ASP.NET Core Minimal API\n  db: SQLite (demo) / PostgreSQL (prod)" });

        // ── Page 5 — Architecture Guide ───────────────────────────────────────
        AddTo(MockNotionDataStore.Page5Id, BlockType.Heading1, 0,
            new HeadingBlockContent { Level = 1, Html = "Architecture Guide" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.Callout, 1,
            new CalloutBlockContent { IconEmoji = "🏗️", Html = "This document describes the high-level architecture of the Tempo.Blazor component library.", BackgroundColor = "gray" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.Heading2, 2,
            new HeadingBlockContent { Level = 2, Html = "Layer Overview" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.BulletList, 3,
            new ListBlockContent { Html = "<strong>Abstractions</strong> — interfaces, models, enums (no Blazor dependency)" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.BulletList, 4,
            new ListBlockContent { Html = "<strong>Tempo.Blazor</strong> — Razor components, scoped CSS, JS interop" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.BulletList, 5,
            new ListBlockContent { Html = "<strong>Demo.Api</strong> — minimal API with in-memory mock stores" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.BulletList, 6,
            new ListBlockContent { Html = "<strong>Demo.SharedUI</strong> — HTTP providers + demo pages" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.Heading2, 7,
            new HeadingBlockContent { Level = 2, Html = "Design Principles" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.NumberedList, 8,
            new ListBlockContent { Html = "Interface-first: all providers are defined as interfaces in Abstractions" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.NumberedList, 9,
            new ListBlockContent { Html = "Cascading context: <code>NotionEditorContext</code> propagates providers to all children" });
        AddTo(MockNotionDataStore.Page5Id, BlockType.NumberedList, 10,
            new ListBlockContent { Html = "Scoped CSS: every component has its own <code>.razor.css</code>" });

        AddTo(MockNotionDataStore.Page5Id, BlockType.Breadcrumb, 11, new BreadcrumbBlockContent());

        // ── Page 6 — Development Setup ────────────────────────────────────────
        AddTo(MockNotionDataStore.Page6Id, BlockType.Heading1, 0,
            new HeadingBlockContent { Level = 1, Html = "Development Setup" });
        AddTo(MockNotionDataStore.Page6Id, BlockType.Paragraph, 1,
            new TextBlockContent { Html = "Follow these steps to get the Tempo.Blazor demo running locally." });
        AddTo(MockNotionDataStore.Page6Id, BlockType.Heading2, 2,
            new HeadingBlockContent { Level = 2, Html = "Prerequisites" });
        AddTo(MockNotionDataStore.Page6Id, BlockType.BulletList, 3,
            new ListBlockContent { Html = ".NET 9 SDK or later" });
        AddTo(MockNotionDataStore.Page6Id, BlockType.BulletList, 4,
            new ListBlockContent { Html = "Node.js 20+ (for JS tooling)" });
        AddTo(MockNotionDataStore.Page6Id, BlockType.Heading2, 5,
            new HeadingBlockContent { Level = 2, Html = "Quick Start" });
        AddTo(MockNotionDataStore.Page6Id, BlockType.Code, 6,
            new CodeBlockContent { Language = "bash", Code = "git clone https://github.com/your-org/Tempo.Blazor2\ncd Tempo.Blazor2\n\n# Start API\ndotnet run --project src/Tempo.Blazor.Demo.Api\n\n# Start Server (separate terminal)\ndotnet run --project src/Tempo.Blazor.Demo.Server" });
        AddTo(MockNotionDataStore.Page6Id, BlockType.Callout, 7,
            new CalloutBlockContent { IconEmoji = "💡", Html = "The API runs on <code>https://localhost:5100</code> and the Server on <code>https://localhost:7106</code> by default.", BackgroundColor = "blue" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Add(BlockType type, int order, IBlockContent content)
    {
        var id = Guid.NewGuid();
        _blocks[id] = MakeBlock(id, _pageId, null, type, order, content);
    }

    private void AddTo(Guid pageId, BlockType type, int order, IBlockContent content)
    {
        var id = Guid.NewGuid();
        _blocks[id] = MakeBlock(id, pageId, null, type, order, content);
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

    public void SeedE2ESearchPage()
    {
        RemoveBlocksForPages(MockNotionDataStore.Page1Id, MockNotionDataStore.Page2Id, MockNotionDataStore.Page3Id, MockNotionDataStore.Page4Id);

        AddTo(MockNotionDataStore.Page1Id, BlockType.Paragraph, 0, new TextBlockContent
        {
            Html = "The beacon rollout verifies engineering search filters across author, label, date, content type, and space."
        });
        AddTo(MockNotionDataStore.Page1Id, BlockType.Heading2, 1, new HeadingBlockContent
        {
            Level = 2,
            Html = "Operational notes"
        });

        AddTo(MockNotionDataStore.Page2Id, BlockType.Paragraph, 0, new TextBlockContent
        {
            Html = "Žluťoučký zlutoucky produktový souhrn pokrývá lokalizované dotazy bez ztráty diakritiky."
        });

        AddTo(MockNotionDataStore.Page3Id, BlockType.Paragraph, 0, new TextBlockContent
        {
            Html = "Support space overview for triage knowledge."
        });
        AddTo(MockNotionDataStore.Page4Id, BlockType.Paragraph, 0, new TextBlockContent
        {
            Html = "Customer escalation contains the customer timeline, owner notes, and next response window."
        });

        var searchPageIds = new HashSet<Guid>
        {
            MockNotionDataStore.Page1Id,
            MockNotionDataStore.Page2Id,
            MockNotionDataStore.Page3Id,
            MockNotionDataStore.Page4Id
        };
        var searchBlocks = _blocks.Values
            .Where(block => searchPageIds.Contains(block.PageId))
            .ToArray();
        foreach (var block in searchBlocks)
        {
            block.CreatedAt = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
            block.LastEditedAt = new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc);
        }
    }

    public void SeedE2EBulkPages()
    {
        RemoveBlocksForPages(MockNotionDataStore.Page1Id, MockNotionDataStore.Page2Id, MockNotionDataStore.Page3Id, MockNotionDataStore.Page4Id);

        AddTo(MockNotionDataStore.Page1Id, BlockType.Heading1, 0, new HeadingBlockContent { Level = 1, Html = "CF24 Source Root" });
        AddTo(MockNotionDataStore.Page1Id, BlockType.Paragraph, 1, new TextBlockContent { Html = "Root copy source content." });

        AddTo(MockNotionDataStore.Page2Id, BlockType.Heading2, 0, new HeadingBlockContent { Level = 2, Html = "CF24 Child A" });
        AddTo(MockNotionDataStore.Page2Id, BlockType.TodoItem, 1, new TodoBlockContent { Html = "Child action", IsChecked = false });

        AddTo(MockNotionDataStore.Page3Id, BlockType.Paragraph, 0, new TextBlockContent { Html = "Grandchild content." });
        AddTo(MockNotionDataStore.Page4Id, BlockType.Paragraph, 0, new TextBlockContent { Html = "Target destination." });
    }

    public void CopyBlocksForPages(IReadOnlyDictionary<Guid, Guid> pageIdMap)
    {
        var sourceBlocks = _blocks.Values
            .Where(block => pageIdMap.ContainsKey(block.PageId))
            .OrderBy(block => block.PageId)
            .ThenBy(block => block.ParentBlockId.HasValue)
            .ThenBy(block => block.Order)
            .ToArray();

        var blockIdMap = sourceBlocks.ToDictionary(block => block.Id, _ => Guid.NewGuid());

        foreach (var source in sourceBlocks)
        {
            var clonedParentId = source.ParentBlockId is { } parentId && blockIdMap.TryGetValue(parentId, out var mappedParentId)
                ? mappedParentId
                : source.ParentBlockId;

            var clone = new PageBlock
            {
                Id = blockIdMap[source.Id],
                PageId = pageIdMap[source.PageId],
                ParentBlockId = clonedParentId,
                Type = source.Type,
                Order = source.Order,
                Content = source.Content,
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            };

            _blocks[clone.Id] = clone;
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

    public Task<IReadOnlyList<PageBlock>> CreateImportedBlocksAsync(
        string pageId,
        IEnumerable<IPageBlock> blocks,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pageGuid = Guid.Parse(pageId);
        var imported = new List<PageBlock>();
        var nextOrder = GetNextOrder(pageGuid, null);

        foreach (var source in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
            while (_blocks.ContainsKey(id))
                id = Guid.NewGuid();

            var block = new PageBlock
            {
                Id = id,
                PageId = pageGuid,
                ParentBlockId = source.ParentBlockId,
                Type = source.Type,
                Order = source.ParentBlockId is null ? nextOrder++ : source.Order,
                Content = source.Content,
                CreatedAt = DateTime.UtcNow,
                LastEditedAt = DateTime.UtcNow
            };

            _blocks[block.Id] = block;
            imported.Add(block);
        }

        return Task.FromResult<IReadOnlyList<PageBlock>>(imported);
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

    public void Reset()
    {
        _blocks.Clear();
        InitializeMockBlocks();
    }

    public IReadOnlyList<PageBlock> GetAllBlocksSnapshot()
        => _blocks.Values
            .OrderBy(block => block.PageId)
            .ThenBy(block => block.ParentBlockId.HasValue)
            .ThenBy(block => block.Order)
            .ToArray();

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

            // When converting to Table, create default child rows
            if (newType == BlockType.Table && block.Content is TableBlockContent tbl)
            {
                var colCount = tbl.ColumnCount > 0 ? tbl.ColumnCount : 3;
                if (tbl.ColumnCount == 0)
                {
                    tbl.ColumnCount = colCount;
                    _blocks[id] = block;
                }

                for (var r = 0; r < 2; r++)
                {
                    var rowId = Guid.NewGuid();
                    var rowBlock = new PageBlock
                    {
                        Id            = rowId,
                        PageId        = block.PageId,
                        ParentBlockId = block.Id,
                        Type          = BlockType.TableRow,
                        Order         = r,
                        Content       = new TableRowBlockContent
                        {
                            Cells = Enumerable.Range(0, colCount).Select(_ => string.Empty).ToList()
                        },
                        CreatedAt     = DateTime.UtcNow,
                        LastEditedAt  = DateTime.UtcNow
                    };
                    _blocks[rowId] = rowBlock;
                }
            }

            return await Task.FromResult(block);
        }

        throw new KeyNotFoundException($"Block {blockId} not found");
    }

    public async Task<string> GetBlockLinkAsync(string blockId)
    {
        return await Task.FromResult($"https://notion.demo/block/{blockId}");
    }

    public Task SetTodoCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var id) || !_blocks.TryGetValue(id, out var block))
            throw new KeyNotFoundException($"Task block {taskId} not found");

        if (block.Content is not TodoBlockContent todo)
            throw new InvalidOperationException($"Block {taskId} is not a todo block.");

        todo.IsChecked = completed;
        block.LastEditedAt = DateTime.UtcNow;
        _blocks[id] = block;
        return Task.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private int GetNextOrder(Guid pageId, Guid? parentBlockId)
    {
        var max = _blocks.Values
            .Where(b => b.PageId == pageId && b.ParentBlockId == parentBlockId)
            .Max(b => (int?)b.Order) ?? -1;
        return max + 1;
    }

    private void RemoveBlocksForPages(params Guid[] pageIds)
    {
        var set = pageIds.ToHashSet();
        foreach (var id in _blocks.Values.Where(block => set.Contains(block.PageId)).Select(block => block.Id).ToArray())
            _blocks.Remove(id);
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
        BlockType.Table                                             => new TableBlockContent { ColumnCount = 3 },
        BlockType.ColumnList                                        => new ColumnListBlockContent { ColumnCount = 2 },
        BlockType.Column                                            => new ColumnBlockContent(),
        BlockType.Diagram                                           => new DiagramBlockContent(),
        BlockType.Wireframe                                         => new WireframeBlockContent(),
        BlockType.Spreadsheet                                       => new SpreadsheetBlockContent(),
        _                                                           => new TextBlockContent()
    };
}
