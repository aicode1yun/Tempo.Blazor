using System.Linq;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>
/// R.4.8 / R.5.1 cutover bridge — converts a <see cref="DocumentEditorDocument"/> to/from the JS
/// core-engine model (the <c>{ documentId, body: { blocks: [...] } }</c> shape that
/// <c>coreEngine.createCoreEditor</c> consumes). The hosted component (<c>TmDocumentCoreEngineHost</c>)
/// sends <see cref="ToCoreModel"/> to JS and rebuilds the document from <see cref="FromCoreModel"/>
/// on save.
/// <para>
/// R.5.1 — <b>full round-trip (no data loss on save).</b> Round-trips paragraphs, headings,
/// lists, quotes, text runs + inline marks, paragraph alignment, AND the structural blocks that
/// previously vanished: <b>tables</b> (rows/cells/spans/header + nested blocks, recursively),
/// <b>images</b> (standalone <see cref="ImageBlockContent"/> ↔ a paragraph carrying a drawing run,
/// plus inline <see cref="DocumentDrawingRun"/>), and <b>page breaks</b>. Rich C# content the engine
/// does not model (image source/asset/link/natural-size, token/field/note runs) is carried verbatim
/// in a <c>__docSource</c> preserve channel and restored on the way back, with the engine-managed
/// visible fields (text/url/size/wrap/position/caption/alt) overlaid on top so user edits survive.
/// </para>
/// </summary>
public static class CoreEngineModelConverter
{
    // Opaque preserve channel: the original C# content/run serialized as JSON and stashed on the JS
    // block/run so non-engine-modelled detail is never lost. The engine ignores the extra property.
    private const string PreserveKey = "__docSource";
    private static readonly JsonSerializerOptions PreserveJson = new(JsonSerializerDefaults.Web);

    // =====================================================================================
    //  forward:  DocumentEditorDocument  →  core (JS) model
    // =====================================================================================

    public static Dictionary<string, object?> ToCoreModel(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var blocks = new List<object?>();
        foreach (var block in document.Blocks)
        {
            var converted = ToCoreBlock(block);
            if (converted is not null) blocks.Add(converted);
        }
        return new Dictionary<string, object?>
        {
            ["documentId"] = document.DocumentId,
            ["version"] = document.Version,
            ["body"] = new Dictionary<string, object?> { ["blocks"] = blocks },
        };
    }

    private static Dictionary<string, object?>? ToCoreBlock(DocumentBlock block) => block.Content switch
    {
        TableBlockContent table => ToCoreTable(block, table),
        ImageBlockContent image => ToCoreImageBlock(block, image),
        PageBreakBlockContent pageBreak => ToCorePageBreak(block, pageBreak),
        _ => ToCoreTextBlock(block),
    };

    // ---- text-based blocks (paragraph / heading / list / quote) -------------------------

    private static Dictionary<string, object?>? ToCoreTextBlock(DocumentBlock block)
    {
        var text = ExtractTextContent(block.Content);
        if (text is null) return null; // genuinely unknown content type — skip rather than corrupt

        var (inlines, headingLevel, listType, listLevel, listStart, isQuote) = text.Value;

        var runs = new List<object?>();
        for (var i = 0; i < inlines.Count; i++) runs.Add(ToCoreRun(block.Id, inlines[i], i));
        if (runs.Count == 0) runs.Add(EmptyTextRun(block.Id + "-r0"));

        var content = new Dictionary<string, object?> { ["type"] = "paragraph", ["runs"] = runs };
        if (block.ParagraphProperties is { Alignment: var align } && align != DocumentTextAlignment.Left)
        {
            content["alignment"] = AlignmentName(align);
        }
        if (headingLevel is int level && level >= 1)
        {
            content["headingLevel"] = level;
            content["styleName"] = "Heading" + level;
        }
        if (listType is not null)
        {
            content["listType"] = listType;      // 'bullet' | 'ordered'  (engine: content.listType)
            content["level"] = listLevel;        // engine: content.level (0-based)
            content["listStart"] = listStart;    // preserved for ordered StartNumber
        }
        if (isQuote) content["blockKind"] = "quote"; // engine renders as paragraph; restored on the way back

        return new Dictionary<string, object?> { ["id"] = block.Id, ["type"] = "paragraph", ["content"] = content };
    }

    private static (List<InlineContent> inlines, int? headingLevel, string? listType, int listLevel, int listStart, bool isQuote)?
        ExtractTextContent(DocumentBlockContent content) => content switch
        {
            ParagraphBlockContent p => (p.Inlines, null, null, 0, 1, false),
            HeadingBlockContent h => (h.Inlines, h.Level, null, 0, 1, false),
            ListBlockContent l => (l.Inlines, null, l.Ordered ? "ordered" : "bullet", l.IndentLevel, l.StartNumber, false),
            QuoteBlockContent q => (q.Inlines, null, null, 0, 1, true),
            _ => null,
        };

    // ---- inline runs --------------------------------------------------------------------

    private static Dictionary<string, object?> ToCoreRun(string blockId, InlineContent inline, int index)
    {
        var id = inline.Id ?? (blockId + "-r" + index);

        if (inline is DocumentDrawingRun drawing)
        {
            var run = DrawingToCoreRun(id, drawing.ObjectId, drawing.Url, drawing.AltText, drawing.Caption, drawing.Layout);
            run[PreserveKey] = JsonSerializer.Serialize(drawing, PreserveJson); // keep source/asset/link/docx/metadata
            return run;
        }

        if (inline is not TextRun textRun)
        {
            // token / field / note-reference: the engine has no model for these. Carry them verbatim so
            // a save never drops them; show an empty placeholder while editing in the engine.
            return new Dictionary<string, object?>
            {
                ["id"] = id,
                ["kind"] = "text",
                ["text"] = "",
                ["marks"] = ToCoreMarks(inline.Marks),
                [PreserveKey] = JsonSerializer.Serialize<InlineContent>(inline, PreserveJson),
            };
        }

        var textDict = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["kind"] = "text",
            ["text"] = textRun.Text,
            ["marks"] = ToCoreMarks(inline.Marks),
        };
        // Marks the engine has no model for (superscript/subscript/revision): carry them verbatim so
        // they are restored on save instead of silently dropped.
        var unmapped = inline.Marks.Where(m => CoreMarkType(m.Type) is null).ToList();
        if (unmapped.Count > 0) textDict["__marks"] = JsonSerializer.Serialize(unmapped, PreserveJson);
        return textDict;
    }

    private static List<object?> ToCoreMarks(IEnumerable<InlineMark> marks)
    {
        var result = new List<object?>();
        foreach (var mark in marks)
        {
            var converted = ToCoreMark(mark);
            if (converted is not null) result.Add(converted);
        }
        return result;
    }

    private static Dictionary<string, object?> EmptyTextRun(string id) =>
        new() { ["id"] = id, ["kind"] = "text", ["text"] = "" };

    // ---- images / drawings --------------------------------------------------------------

    // A standalone C# image block has no inline-image equivalent in the engine, which models images
    // only as drawing runs inside a paragraph. We emit a paragraph carrying a single drawing run and
    // tag the block so the reverse pass rebuilds an ImageBlockContent (not a paragraph).
    private static Dictionary<string, object?> ToCoreImageBlock(DocumentBlock block, ImageBlockContent image)
    {
        var run = DrawingToCoreRun(block.Id + "-run", block.Id + "-obj", image.Url, image.AltText, image.Caption, image.Layout);
        run[PreserveKey] = JsonSerializer.Serialize(image, PreserveJson);
        var content = new Dictionary<string, object?>
        {
            ["type"] = "paragraph",
            ["runs"] = new List<object?> { run },
        };
        return new Dictionary<string, object?>
        {
            ["id"] = block.Id,
            ["type"] = "paragraph",
            ["imageBlock"] = true,
            ["content"] = content,
        };
    }

    // Builds the engine drawing-run shape (mirrors render-host.insertImage's layout object).
    private static Dictionary<string, object?> DrawingToCoreRun(
        string runId, string objectId, string? url, string? altText, string? caption, DocumentObjectLayout? layout)
    {
        var l = layout ?? DocumentObjectLayout.Inline();
        return new Dictionary<string, object?>
        {
            ["id"] = runId,
            ["kind"] = "drawing",
            ["objectId"] = objectId,
            ["url"] = url ?? string.Empty,
            ["layout"] = new Dictionary<string, object?>
            {
                ["wrapMode"] = EngineWrapMode(l.Wrap.Mode),
                ["width"] = l.Transform.Width ?? 120d,
                ["height"] = l.Transform.Height ?? 90d,
                ["altText"] = altText ?? string.Empty,
                ["caption"] = caption ?? string.Empty,
                ["zIndex"] = l.Stacking.ZIndex,
                ["horizontalPosition"] = new Dictionary<string, object?>
                {
                    ["align"] = l.Position.HorizontalAlignment?.ToString() ?? "Left",
                    ["offset"] = l.Position.X,
                    ["relativeTo"] = "Page",
                },
                ["verticalPosition"] = new Dictionary<string, object?>
                {
                    ["align"] = "Top",
                    ["offset"] = l.Position.Y,
                    ["relativeTo"] = "Page",
                },
            },
        };
    }

    // ---- tables (recursive) -------------------------------------------------------------

    private static Dictionary<string, object?> ToCoreTable(DocumentBlock block, TableBlockContent table)
    {
        var rows = new List<object?>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var cells = new List<object?>();
            foreach (var cell in table.Rows[r].Cells)
            {
                var cellBlocks = new List<object?>();
                foreach (var nested in cell.Blocks)
                {
                    var converted = ToCoreBlock(nested);
                    if (converted is not null) cellBlocks.Add(converted);
                }
                if (cellBlocks.Count == 0) cellBlocks.Add(EmptyParagraphBlock(cell.Id + "-p"));

                cells.Add(new Dictionary<string, object?>
                {
                    ["id"] = cell.Id,
                    ["type"] = "tableCell",
                    ["rowSpan"] = cell.RowSpan,
                    ["colSpan"] = cell.ColumnSpan,
                    ["isHeader"] = cell.IsHeader,
                    ["width"] = cell.Width,
                    ["backgroundColor"] = cell.BackgroundColor,
                    ["verticalAlign"] = cell.VerticalAlignment.ToString(),
                    ["style"] = new Dictionary<string, object?>(),
                    ["blocks"] = cellBlocks,
                });
            }
            rows.Add(new Dictionary<string, object?> { ["id"] = block.Id + "-row" + r, ["cells"] = cells });
        }

        var content = new Dictionary<string, object?>
        {
            ["type"] = "table",
            ["rows"] = rows,
            ["tableLayout"] = new Dictionary<string, object?>
            {
                ["width"] = table.Layout.Width,
                ["alignment"] = table.Layout.Alignment.ToString(),
                ["backgroundColor"] = table.Layout.BackgroundColor,
                ["cellPadding"] = table.Layout.CellPadding,
            },
        };
        return new Dictionary<string, object?> { ["id"] = block.Id, ["type"] = "table", ["content"] = content };
    }

    private static Dictionary<string, object?> EmptyParagraphBlock(string id) => new()
    {
        ["id"] = id,
        ["type"] = "paragraph",
        ["content"] = new Dictionary<string, object?> { ["type"] = "paragraph", ["runs"] = new List<object?> { EmptyTextRun(id + "-r0") } },
    };

    // ---- page break ---------------------------------------------------------------------

    private static Dictionary<string, object?> ToCorePageBreak(DocumentBlock block, PageBreakBlockContent pageBreak)
    {
        var result = new Dictionary<string, object?> { ["id"] = block.Id, ["type"] = "pageBreak" };
        if (!string.IsNullOrEmpty(pageBreak.NextSectionId)) result["nextSectionId"] = pageBreak.NextSectionId;
        return result;
    }

    // ---- marks --------------------------------------------------------------------------

    private static Dictionary<string, object?>? ToCoreMark(InlineMark mark)
    {
        var type = CoreMarkType(mark.Type);
        if (type is null) return null;
        var value = mark.Type switch
        {
            InlineMarkType.Link => mark.Link?.Href,
            InlineMarkType.CommentAnchor => mark.CommentAnchor?.CommentId,
            _ => mark.Value,
        };
        var result = new Dictionary<string, object?> { ["type"] = type };
        if (!string.IsNullOrEmpty(value)) result["value"] = value;
        return result;
    }

    private static string? CoreMarkType(InlineMarkType type) => type switch
    {
        InlineMarkType.Bold => "bold",
        InlineMarkType.Italic => "italic",
        InlineMarkType.Underline => "underline",
        InlineMarkType.Strikethrough => "strikethrough",
        InlineMarkType.Link => "link",
        InlineMarkType.Highlight => "highlight",
        InlineMarkType.TextColor => "textcolor",
        InlineMarkType.FontFamily => "fontfamily",
        InlineMarkType.FontSize => "fontsize",
        InlineMarkType.CommentAnchor => "comment",
        InlineMarkType.Bookmark => "bookmark",
        _ => null, // superscript/subscript/revision not modelled by the engine (kept via __marks on its run)
    };

    private static string AlignmentName(DocumentTextAlignment alignment) => alignment switch
    {
        DocumentTextAlignment.Center => "center",
        DocumentTextAlignment.Right => "right",
        DocumentTextAlignment.Justify => "justify",
        _ => "left",
    };

    private static string EngineWrapMode(DocumentWrapMode mode) => mode switch
    {
        DocumentWrapMode.Square => "square",
        DocumentWrapMode.Tight => "tight",
        DocumentWrapMode.Through => "through",
        DocumentWrapMode.TopBottom => "topAndBottom",
        DocumentWrapMode.BehindText => "behindText",
        DocumentWrapMode.InFrontOfText => "inFrontOfText",
        _ => "inline",
    };

    // =====================================================================================
    //  reverse:  core (JS) model  →  DocumentEditorDocument
    // =====================================================================================

    public static DocumentEditorDocument FromCoreModel(JsonElement coreModel, DocumentEditorDocument? template = null)
    {
        var document = template ?? new DocumentEditorDocument();
        if (coreModel.ValueKind == JsonValueKind.Object && coreModel.TryGetProperty("documentId", out var idEl) && idEl.ValueKind == JsonValueKind.String)
        {
            document.DocumentId = idEl.GetString() ?? document.DocumentId;
        }
        var blocks = new List<DocumentBlock>();
        if (coreModel.TryGetProperty("body", out var body) && body.TryGetProperty("blocks", out var blockArr) && blockArr.ValueKind == JsonValueKind.Array)
        {
            var order = 0d;
            foreach (var blockEl in blockArr.EnumerateArray())
            {
                var block = FromCoreBlock(blockEl);
                if (block is null) continue;
                block.Order = order++;
                blocks.Add(block);
            }
        }
        document.Blocks = blocks;
        return document;
    }

    private static DocumentBlock? FromCoreBlock(JsonElement blockEl)
    {
        if (blockEl.ValueKind != JsonValueKind.Object) return null;
        var type = blockEl.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "paragraph";
        var id = blockEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString()! : Guid.NewGuid().ToString("N");

        if (string.Equals(type, "pageBreak", StringComparison.OrdinalIgnoreCase)) return FromCorePageBreak(blockEl, id);
        if (string.Equals(type, "table", StringComparison.OrdinalIgnoreCase)) return FromCoreTable(blockEl, id);
        if (IsImageBlock(blockEl)) return FromCoreImageBlock(blockEl, id);
        return FromCoreTextBlock(blockEl, id);
    }

    private static bool IsImageBlock(JsonElement blockEl)
    {
        if (blockEl.TryGetProperty("imageBlock", out var flag) && flag.ValueKind == JsonValueKind.True) return true;
        // Fallback: a paragraph whose only run is a drawing and carries no text is an image block.
        if (!TryGetRuns(blockEl, out var runs)) return false;
        var sawDrawing = false;
        foreach (var run in runs.EnumerateArray())
        {
            var kind = run.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            if (string.Equals(kind, "drawing", StringComparison.OrdinalIgnoreCase)) { sawDrawing = true; continue; }
            var text = run.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String ? txt.GetString() : null;
            if (!string.IsNullOrEmpty(text)) return false; // has real text alongside → inline image, not a block
        }
        return sawDrawing;
    }

    // ---- text-based blocks --------------------------------------------------------------

    private static DocumentBlock FromCoreTextBlock(JsonElement blockEl, string id)
    {
        var content = blockEl.TryGetProperty("content", out var c) ? c : default;
        var inlines = new List<InlineContent>();
        if (TryGetRuns(blockEl, out var runs))
        {
            foreach (var runEl in runs.EnumerateArray())
            {
                var run = FromCoreRun(runEl);
                if (run is not null) inlines.Add(run);
            }
        }

        int? headingLevel = null;
        if (content.ValueKind == JsonValueKind.Object && content.TryGetProperty("headingLevel", out var hl) && hl.TryGetInt32(out var lvl) && lvl >= 1)
        {
            headingLevel = lvl;
        }
        var listType = content.ValueKind == JsonValueKind.Object && content.TryGetProperty("listType", out var ltEl) && ltEl.ValueKind == JsonValueKind.String
            ? ltEl.GetString()
            : null;
        var isQuote = content.ValueKind == JsonValueKind.Object && content.TryGetProperty("blockKind", out var bkEl) && bkEl.ValueKind == JsonValueKind.String
            && string.Equals(bkEl.GetString(), "quote", StringComparison.OrdinalIgnoreCase);

        DocumentBlockContent blockContent;
        DocumentBlockType blockType;
        if (!string.IsNullOrEmpty(listType))
        {
            blockType = DocumentBlockType.List;
            blockContent = new ListBlockContent
            {
                Ordered = string.Equals(listType, "ordered", StringComparison.OrdinalIgnoreCase) || string.Equals(listType, "numbered", StringComparison.OrdinalIgnoreCase),
                IndentLevel = ReadInt(content, "level", 0),
                StartNumber = ReadInt(content, "listStart", 1),
                Inlines = inlines,
            };
        }
        else if (isQuote)
        {
            blockType = DocumentBlockType.Quote;
            blockContent = new QuoteBlockContent { Inlines = inlines };
        }
        else if (headingLevel.HasValue)
        {
            blockType = DocumentBlockType.Heading;
            blockContent = new HeadingBlockContent { Level = headingLevel.Value, Inlines = inlines };
        }
        else
        {
            blockType = DocumentBlockType.Paragraph;
            blockContent = new ParagraphBlockContent { Inlines = inlines };
        }

        var block = new DocumentBlock { Id = id, Type = blockType, Content = blockContent };
        if (content.ValueKind == JsonValueKind.Object && content.TryGetProperty("alignment", out var alEl) && alEl.ValueKind == JsonValueKind.String)
        {
            block.ParagraphProperties.Alignment = AlignmentFromName(alEl.GetString());
        }
        return block;
    }

    private static InlineContent? FromCoreRun(JsonElement runEl)
    {
        if (runEl.ValueKind != JsonValueKind.Object) return null;
        var kind = runEl.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : "text";

        if (string.Equals(kind, "drawing", StringComparison.OrdinalIgnoreCase))
        {
            var drawing = TryDeserializePreserved<DocumentDrawingRun>(runEl) ?? new DocumentDrawingRun();
            if (runEl.TryGetProperty("objectId", out var objEl) && objEl.ValueKind == JsonValueKind.String) drawing.ObjectId = objEl.GetString()!;
            if (runEl.TryGetProperty("id", out var ridEl) && ridEl.ValueKind == JsonValueKind.String) drawing.Id = ridEl.GetString();
            OverlayDrawingFromRun(drawing, runEl);
            return drawing;
        }

        // Preserved token / field / note-reference run.
        var preserved = TryDeserializePreserved<InlineContent>(runEl);
        if (preserved is not null) return preserved;

        var text = runEl.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
        var run = new TextRun
        {
            Id = runEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null,
            Text = text,
        };
        if (runEl.TryGetProperty("marks", out var marks) && marks.ValueKind == JsonValueKind.Array)
        {
            foreach (var markEl in marks.EnumerateArray())
            {
                var mark = FromCoreMark(markEl);
                if (mark is not null) run.Marks.Add(mark);
            }
        }
        // Restore marks the engine could not model (carried in the __marks preserve channel).
        if (runEl.TryGetProperty("__marks", out var preservedMarks) && preservedMarks.ValueKind == JsonValueKind.String)
        {
            try
            {
                var restored = JsonSerializer.Deserialize<List<InlineMark>>(preservedMarks.GetString()!, PreserveJson);
                if (restored is not null) run.Marks.AddRange(restored);
            }
            catch (JsonException) { /* preserve is best-effort */ }
        }
        return run;
    }

    // ---- images -------------------------------------------------------------------------

    private static DocumentBlock FromCoreImageBlock(JsonElement blockEl, string id)
    {
        JsonElement? drawingRun = null;
        if (TryGetRuns(blockEl, out var runs))
        {
            foreach (var run in runs.EnumerateArray())
            {
                var kind = run.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
                if (string.Equals(kind, "drawing", StringComparison.OrdinalIgnoreCase)) { drawingRun = run; break; }
            }
        }

        var image = drawingRun is { } dr ? TryDeserializePreserved<ImageBlockContent>(dr) ?? new ImageBlockContent() : new ImageBlockContent();
        if (drawingRun is { } run2) OverlayImageFromRun(image, run2);

        return new DocumentBlock { Id = id, Type = DocumentBlockType.Image, Content = image };
    }

    private static void OverlayImageFromRun(ImageBlockContent image, JsonElement runEl)
    {
        if (runEl.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String) image.Url = url.GetString();
        if (!runEl.TryGetProperty("layout", out var l) || l.ValueKind != JsonValueKind.Object) return;

        if (l.TryGetProperty("wrapMode", out var wm) && wm.ValueKind == JsonValueKind.String) image.Layout.Wrap.Mode = ParseEngineWrapMode(wm.GetString());
        if (TryGetDouble(l, "width", out var w)) image.Layout.Transform.Width = w;
        if (TryGetDouble(l, "height", out var h)) image.Layout.Transform.Height = h;
        if (l.TryGetProperty("altText", out var alt) && alt.ValueKind == JsonValueKind.String) image.AltText = alt.GetString();
        if (l.TryGetProperty("caption", out var cap) && cap.ValueKind == JsonValueKind.String) image.Caption = cap.GetString();
        if (TryGetInt(l, "zIndex", out var z)) image.Layout.Stacking.ZIndex = z;
        image.Layout.Position.X = ReadPositionOffset(l, "horizontalPosition", image.Layout.Position.X);
        image.Layout.Position.Y = ReadPositionOffset(l, "verticalPosition", image.Layout.Position.Y);
    }

    private static void OverlayDrawingFromRun(DocumentDrawingRun drawing, JsonElement runEl)
    {
        if (runEl.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String) drawing.Url = url.GetString();
        if (!runEl.TryGetProperty("layout", out var l) || l.ValueKind != JsonValueKind.Object) return;

        if (l.TryGetProperty("wrapMode", out var wm) && wm.ValueKind == JsonValueKind.String) drawing.Layout.Wrap.Mode = ParseEngineWrapMode(wm.GetString());
        if (TryGetDouble(l, "width", out var w)) drawing.Layout.Transform.Width = w;
        if (TryGetDouble(l, "height", out var h)) drawing.Layout.Transform.Height = h;
        if (l.TryGetProperty("altText", out var alt) && alt.ValueKind == JsonValueKind.String) drawing.AltText = alt.GetString();
        if (l.TryGetProperty("caption", out var cap) && cap.ValueKind == JsonValueKind.String) drawing.Caption = cap.GetString();
        if (TryGetInt(l, "zIndex", out var z)) drawing.Layout.Stacking.ZIndex = z;
        drawing.Layout.Position.X = ReadPositionOffset(l, "horizontalPosition", drawing.Layout.Position.X);
        drawing.Layout.Position.Y = ReadPositionOffset(l, "verticalPosition", drawing.Layout.Position.Y);
    }

    // ---- tables -------------------------------------------------------------------------

    private static DocumentBlock FromCoreTable(JsonElement blockEl, string id)
    {
        var table = new TableBlockContent();
        if (blockEl.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object)
        {
            if (content.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var rowEl in rowsEl.EnumerateArray())
                {
                    var row = new TableRowContent();
                    if (rowEl.TryGetProperty("cells", out var cellsEl) && cellsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cellEl in cellsEl.EnumerateArray()) row.Cells.Add(FromCoreCell(cellEl));
                    }
                    table.Rows.Add(row);
                }
            }
            if (content.TryGetProperty("tableLayout", out var tl) && tl.ValueKind == JsonValueKind.Object)
            {
                if (TryGetDouble(tl, "width", out var w)) table.Layout.Width = w;
                if (tl.TryGetProperty("alignment", out var al) && al.ValueKind == JsonValueKind.String) table.Layout.Alignment = TableAlignmentFromName(al.GetString());
                if (tl.TryGetProperty("backgroundColor", out var bg) && bg.ValueKind == JsonValueKind.String) table.Layout.BackgroundColor = bg.GetString();
                if (TryGetDouble(tl, "cellPadding", out var cp)) table.Layout.CellPadding = cp;
            }
        }
        return new DocumentBlock { Id = id, Type = DocumentBlockType.Table, Content = table };
    }

    private static TableCellContent FromCoreCell(JsonElement cellEl)
    {
        var cell = new TableCellContent
        {
            Id = cellEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString()! : Guid.NewGuid().ToString("N"),
            RowSpan = ReadInt(cellEl, "rowSpan", 1),
            ColumnSpan = ReadInt(cellEl, "colSpan", 1),
            IsHeader = cellEl.TryGetProperty("isHeader", out var hdr) && hdr.ValueKind == JsonValueKind.True,
        };
        if (TryGetDouble(cellEl, "width", out var w)) cell.Width = w;
        if (cellEl.TryGetProperty("backgroundColor", out var bg) && bg.ValueKind == JsonValueKind.String) cell.BackgroundColor = bg.GetString();
        if (cellEl.TryGetProperty("verticalAlign", out var va) && va.ValueKind == JsonValueKind.String) cell.VerticalAlignment = CellVAlignFromName(va.GetString());

        if (cellEl.TryGetProperty("blocks", out var blocksEl) && blocksEl.ValueKind == JsonValueKind.Array)
        {
            var order = 0d;
            foreach (var nestedEl in blocksEl.EnumerateArray())
            {
                var nested = FromCoreBlock(nestedEl);
                if (nested is null) continue;
                nested.Order = order++;
                cell.Blocks.Add(nested);
            }
        }
        return cell;
    }

    // ---- page break ---------------------------------------------------------------------

    private static DocumentBlock FromCorePageBreak(JsonElement blockEl, string id)
    {
        var content = new PageBreakBlockContent();
        if (blockEl.TryGetProperty("nextSectionId", out var ns) && ns.ValueKind == JsonValueKind.String) content.NextSectionId = ns.GetString();
        return new DocumentBlock { Id = id, Type = DocumentBlockType.PageBreak, Content = content };
    }

    // ---- marks --------------------------------------------------------------------------

    private static InlineMark? FromCoreMark(JsonElement markEl)
    {
        if (markEl.ValueKind != JsonValueKind.Object || !markEl.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) return null;
        var type = MarkTypeFromName(typeEl.GetString());
        if (type is null) return null;
        var value = markEl.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var mark = new InlineMark { Type = type.Value };
        switch (type.Value)
        {
            case InlineMarkType.Link: mark.Link = new LinkMarkData { Href = value ?? "" }; break;
            case InlineMarkType.CommentAnchor: mark.CommentAnchor = new CommentAnchorMarkData { CommentId = value ?? "" }; break;
            default: mark.Value = value; break;
        }
        return mark;
    }

    private static InlineMarkType? MarkTypeFromName(string? name) => (name ?? string.Empty).ToLowerInvariant() switch
    {
        "bold" => InlineMarkType.Bold,
        "italic" => InlineMarkType.Italic,
        "underline" => InlineMarkType.Underline,
        "strikethrough" or "strike" => InlineMarkType.Strikethrough,
        "link" or "hyperlink" => InlineMarkType.Link,
        "highlight" or "backgroundcolor" => InlineMarkType.Highlight,
        "textcolor" or "fontcolor" or "foregroundcolor" => InlineMarkType.TextColor,
        "fontfamily" => InlineMarkType.FontFamily,
        "fontsize" => InlineMarkType.FontSize,
        "comment" => InlineMarkType.CommentAnchor,
        "bookmark" => InlineMarkType.Bookmark,
        _ => null,
    };

    private static DocumentTextAlignment AlignmentFromName(string? name) => (name ?? string.Empty).ToLowerInvariant() switch
    {
        "center" => DocumentTextAlignment.Center,
        "right" => DocumentTextAlignment.Right,
        "justify" => DocumentTextAlignment.Justify,
        _ => DocumentTextAlignment.Left,
    };

    private static DocumentWrapMode ParseEngineWrapMode(string? mode) => (mode ?? "inline").ToLowerInvariant() switch
    {
        "square" => DocumentWrapMode.Square,
        "tight" => DocumentWrapMode.Tight,
        "through" => DocumentWrapMode.Through,
        "topandbottom" => DocumentWrapMode.TopBottom,
        "behindtext" => DocumentWrapMode.BehindText,
        "infrontoftext" => DocumentWrapMode.InFrontOfText,
        _ => DocumentWrapMode.Inline,
    };

    private static TableHorizontalAlignment TableAlignmentFromName(string? name) => (name ?? string.Empty).ToLowerInvariant() switch
    {
        "center" => TableHorizontalAlignment.Center,
        "right" => TableHorizontalAlignment.Right,
        _ => TableHorizontalAlignment.Left,
    };

    private static TableCellVerticalAlignment CellVAlignFromName(string? name) => (name ?? string.Empty).ToLowerInvariant() switch
    {
        "middle" => TableCellVerticalAlignment.Middle,
        "bottom" => TableCellVerticalAlignment.Bottom,
        _ => TableCellVerticalAlignment.Top,
    };

    // ---- low-level JSON helpers ---------------------------------------------------------

    private static bool TryGetRuns(JsonElement blockEl, out JsonElement runs)
    {
        runs = default;
        if (!blockEl.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object) return false;
        if (!content.TryGetProperty("runs", out runs) || runs.ValueKind != JsonValueKind.Array) return false;
        return true;
    }

    private static T? TryDeserializePreserved<T>(JsonElement el) where T : class
    {
        if (!el.TryGetProperty(PreserveKey, out var src) || src.ValueKind != JsonValueKind.String) return null;
        var json = src.GetString();
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, PreserveJson); }
        catch (JsonException) { return null; }
    }

    private static double ReadPositionOffset(JsonElement layout, string key, double fallback)
    {
        if (layout.TryGetProperty(key, out var pos) && pos.ValueKind == JsonValueKind.Object && TryGetDouble(pos, "offset", out var offset)) return offset;
        return fallback;
    }

    private static int ReadInt(JsonElement obj, string key, int fallback) =>
        TryGetInt(obj, key, out var value) ? value : fallback;

    private static bool TryGetInt(JsonElement obj, string key, out int value)
    {
        value = 0;
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value);
    }

    private static bool TryGetDouble(JsonElement obj, string key, out double value)
    {
        value = 0;
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value);
    }
}
