using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Components.NotionEditor.UI;

/// <summary>
/// All available slash-menu block types with their display keys, icons, and search keywords.
/// Name / Description fields are localisation keys resolved by the component via Loc[].
/// </summary>
public static class SlashMenuRegistry
{
    // ── Inline 20×20 SVG icon strings ─────────────────────────────────────────

    private static class Icons
    {
        internal const string Paragraph =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M3 6h14M3 10h14M3 14h9" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string Heading1 =
            """<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"><text x="2" y="15" font-size="12" font-weight="800" fill="currentColor" font-family="system-ui,sans-serif">H1</text></svg>""";

        internal const string Heading2 =
            """<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"><text x="2" y="15" font-size="12" font-weight="700" fill="currentColor" font-family="system-ui,sans-serif">H2</text></svg>""";

        internal const string Heading3 =
            """<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"><text x="2" y="15" font-size="12" font-weight="600" fill="currentColor" font-family="system-ui,sans-serif">H3</text></svg>""";

        internal const string BulletList =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><circle cx="4" cy="5.5" r="1.5" fill="currentColor"/><circle cx="4" cy="10" r="1.5" fill="currentColor"/><circle cx="4" cy="14.5" r="1.5" fill="currentColor"/><path d="M7.5 5.5h9.5M7.5 10h9.5M7.5 14.5h9.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string NumberedList =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M3 4v4M2.5 8h2M3 12v-1.5h1.5v1.5H3v1.5h2" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/><path d="M7.5 5.5h9.5M7.5 10h9.5M7.5 14.5h9.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string TodoItem =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="2.5" y="6.5" width="7" height="7" rx="1.5" stroke="currentColor" stroke-width="1.5"/><path d="M4.5 10l2 2 2.5-3" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/><path d="M12 10h6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string Toggle =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M6 5l5 5-5 5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/><path d="M13 5h5M13 10h5M13 15h5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string Quote =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M3 14.5V9A5 5 0 018 4M11 14.5V9a5 5 0 015-5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string Callout =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="2" y="4" width="16" height="12" rx="2" stroke="currentColor" stroke-width="1.5"/><path d="M10 7v4M10 13h.01" stroke="currentColor" stroke-width="1.75" stroke-linecap="round"/></svg>""";

        internal const string Divider =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M2 10h16" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>""";

        internal const string Code =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M6 7l-4 3 4 3M14 7l4 3-4 3" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/><path d="M11.5 5l-3 10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string Equation =
            """<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"><text x="3" y="15" font-size="14" fill="currentColor" font-family="serif">∑</text></svg>""";

        internal const string Image =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="2" y="4" width="16" height="12" rx="2" stroke="currentColor" stroke-width="1.5"/><circle cx="7.5" cy="8.5" r="1.5" fill="currentColor" opacity=".65"/><path d="M2 14l5-5 3 3 2.5-2.5L17 14" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

        internal const string Video =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><circle cx="10" cy="10" r="8" stroke="currentColor" stroke-width="1.5"/><path d="M8.5 7.5l5 2.5-5 2.5V7.5z" fill="currentColor"/></svg>""";

        internal const string Audio =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M3 8h3l4-4v12l-4-4H3V8z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/><path d="M15.5 5.5a7 7 0 010 9M13 8a4 4 0 010 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string File =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M5 3h7l4 4v10a1 1 0 01-1 1H5a1 1 0 01-1-1V4a1 1 0 011-1z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/><path d="M12 3v5h5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

        internal const string Pdf =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M5 3h7l4 4v10a1 1 0 01-1 1H5a1 1 0 01-1-1V4a1 1 0 011-1z" stroke="currentColor" stroke-width="1.5"/><path d="M12 3v5h5" stroke="currentColor" stroke-width="1.5"/><text x="4.5" y="17" font-size="5" font-weight="700" fill="currentColor" font-family="system-ui,sans-serif">PDF</text></svg>""";

        internal const string Bookmark =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M5 3h10a1 1 0 011 1v13l-6-3.5L4 17V4a1 1 0 011-1z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

        internal const string Embed =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="2" y="3.5" width="16" height="11" rx="1.5" stroke="currentColor" stroke-width="1.5"/><path d="M7 18h6M10 14.5V18" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string ChildPage =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M4 3h9l4 4v10a1 1 0 01-1 1H4a1 1 0 01-1-1V4a1 1 0 011-1z" stroke="currentColor" stroke-width="1.5"/><path d="M13 3v5h4" stroke="currentColor" stroke-width="1.5"/><path d="M6 10h8M6 13h5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string LinkedPage =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M4 3h9l4 4v10a1 1 0 01-1 1H4a1 1 0 01-1-1V4a1 1 0 011-1z" stroke="currentColor" stroke-width="1.5"/><path d="M13 3v5h4" stroke="currentColor" stroke-width="1.5"/><path d="M17 2l-3 3M17 2h-3M17 2v3" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

        internal const string TableOfContents =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M3 5h14M5 9h12M7 13h10M9 17h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string TemplateButton =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="3" y="7" width="14" height="6" rx="3" stroke="currentColor" stroke-width="1.5"/><path d="M7 10h6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>""";

        internal const string Diagram =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="1.5" y="1.5" width="6" height="4" rx="1" stroke="currentColor" stroke-width="1.3"/><rect x="12.5" y="1.5" width="6" height="4" rx="1" stroke="currentColor" stroke-width="1.3"/><rect x="7" y="14" width="6" height="4" rx="1" stroke="currentColor" stroke-width="1.3"/><path d="M4.5 5.5v3h11V5.5M10 8.5v5.5" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

        internal const string Wireframe =
            """<svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true"><rect x="2" y="2" width="16" height="4" rx="1" stroke="currentColor" stroke-width="1.3"/><rect x="2" y="8" width="7" height="10" rx="1" stroke="currentColor" stroke-width="1.3"/><rect x="11" y="8" width="7" height="10" rx="1" stroke="currentColor" stroke-width="1.3"/></svg>""";
    }

    // ── Registry ───────────────────────────────────────────────────────────────

    private static readonly SlashMenuItem[] _all =
    [
        // ── Basic ────────────────────────────────────────────────────────────
        new(BlockType.Paragraph,      "TmNotionSlashMenu_ItemName_Paragraph",       "TmNotionSlashMenu_ItemDesc_Paragraph",
            Icons.Paragraph,      SlashMenuCategory.Basic,
            ["text", "paragraph", "plain", "p"]),

        new(BlockType.Heading1,       "TmNotionSlashMenu_ItemName_Heading1",        "TmNotionSlashMenu_ItemDesc_Heading1",
            Icons.Heading1,       SlashMenuCategory.Basic,
            ["h1", "heading", "heading1", "title", "big"]),

        new(BlockType.Heading2,       "TmNotionSlashMenu_ItemName_Heading2",        "TmNotionSlashMenu_ItemDesc_Heading2",
            Icons.Heading2,       SlashMenuCategory.Basic,
            ["h2", "heading", "heading2", "subtitle", "medium"]),

        new(BlockType.Heading3,       "TmNotionSlashMenu_ItemName_Heading3",        "TmNotionSlashMenu_ItemDesc_Heading3",
            Icons.Heading3,       SlashMenuCategory.Basic,
            ["h3", "heading", "heading3", "subheading", "small"]),

        new(BlockType.BulletList,     "TmNotionSlashMenu_ItemName_BulletList",      "TmNotionSlashMenu_ItemDesc_BulletList",
            Icons.BulletList,     SlashMenuCategory.Basic,
            ["bullet", "list", "unordered", "ul", "-", "*", "bulleted"]),

        new(BlockType.NumberedList,   "TmNotionSlashMenu_ItemName_NumberedList",    "TmNotionSlashMenu_ItemDesc_NumberedList",
            Icons.NumberedList,   SlashMenuCategory.Basic,
            ["numbered", "list", "ordered", "ol", "1.", "number"]),

        new(BlockType.TodoItem,       "TmNotionSlashMenu_ItemName_TodoItem",        "TmNotionSlashMenu_ItemDesc_TodoItem",
            Icons.TodoItem,       SlashMenuCategory.Basic,
            ["todo", "task", "checkbox", "check", "[]", "to-do"]),

        new(BlockType.Toggle,         "TmNotionSlashMenu_ItemName_Toggle",          "TmNotionSlashMenu_ItemDesc_Toggle",
            Icons.Toggle,         SlashMenuCategory.Basic,
            ["toggle", "collapse", "expand", "details", "accordion"]),

        new(BlockType.Quote,          "TmNotionSlashMenu_ItemName_Quote",           "TmNotionSlashMenu_ItemDesc_Quote",
            Icons.Quote,          SlashMenuCategory.Basic,
            ["quote", "blockquote", ">", "citation"]),

        new(BlockType.Callout,        "TmNotionSlashMenu_ItemName_Callout",         "TmNotionSlashMenu_ItemDesc_Callout",
            Icons.Callout,        SlashMenuCategory.Basic,
            ["callout", "note", "info", "warning", "alert", "tip"]),

        new(BlockType.Divider,        "TmNotionSlashMenu_ItemName_Divider",         "TmNotionSlashMenu_ItemDesc_Divider",
            Icons.Divider,        SlashMenuCategory.Basic,
            ["divider", "separator", "line", "hr", "---", "rule"]),

        new(BlockType.Code,           "TmNotionSlashMenu_ItemName_Code",            "TmNotionSlashMenu_ItemDesc_Code",
            Icons.Code,           SlashMenuCategory.Basic,
            ["code", "snippet", "monospace", "```", "programming", "pre"]),

        new(BlockType.Equation,       "TmNotionSlashMenu_ItemName_Equation",        "TmNotionSlashMenu_ItemDesc_Equation",
            Icons.Equation,       SlashMenuCategory.Basic,
            ["equation", "math", "latex", "formula", "$$", "block equation"]),

        // ── Media ────────────────────────────────────────────────────────────
        new(BlockType.Image,          "TmNotionSlashMenu_ItemName_Image",           "TmNotionSlashMenu_ItemDesc_Image",
            Icons.Image,          SlashMenuCategory.Media,
            ["image", "photo", "picture", "img", "upload"]),

        new(BlockType.Video,          "TmNotionSlashMenu_ItemName_Video",           "TmNotionSlashMenu_ItemDesc_Video",
            Icons.Video,          SlashMenuCategory.Media,
            ["video", "youtube", "vimeo", "mp4", "movie", "film"]),

        new(BlockType.Audio,          "TmNotionSlashMenu_ItemName_Audio",           "TmNotionSlashMenu_ItemDesc_Audio",
            Icons.Audio,          SlashMenuCategory.Media,
            ["audio", "sound", "music", "mp3", "podcast"]),

        new(BlockType.File,           "TmNotionSlashMenu_ItemName_File",            "TmNotionSlashMenu_ItemDesc_File",
            Icons.File,           SlashMenuCategory.Media,
            ["file", "attachment", "upload", "download"]),

        new(BlockType.Pdf,            "TmNotionSlashMenu_ItemName_Pdf",             "TmNotionSlashMenu_ItemDesc_Pdf",
            Icons.Pdf,            SlashMenuCategory.Media,
            ["pdf", "document", "acrobat"]),

        // ── Embeds ───────────────────────────────────────────────────────────
        new(BlockType.Bookmark,       "TmNotionSlashMenu_ItemName_Bookmark",        "TmNotionSlashMenu_ItemDesc_Bookmark",
            Icons.Bookmark,       SlashMenuCategory.Embeds,
            ["bookmark", "link", "url", "web", "website", "preview"]),

        new(BlockType.Embed,          "TmNotionSlashMenu_ItemName_Embed",           "TmNotionSlashMenu_ItemDesc_Embed",
            Icons.Embed,          SlashMenuCategory.Embeds,
            ["embed", "iframe", "website", "integration"]),

        // ── Page ─────────────────────────────────────────────────────────────
        new(BlockType.ChildPage,      "TmNotionSlashMenu_ItemName_ChildPage",       "TmNotionSlashMenu_ItemDesc_ChildPage",
            Icons.ChildPage,      SlashMenuCategory.Page,
            ["page", "subpage", "child", "new page"]),

        new(BlockType.LinkedPage,     "TmNotionSlashMenu_ItemName_LinkedPage",      "TmNotionSlashMenu_ItemDesc_LinkedPage",
            Icons.LinkedPage,     SlashMenuCategory.Page,
            ["linked", "link", "page", "reference"]),

        // ── Advanced ─────────────────────────────────────────────────────────
        new(BlockType.TableOfContents, "TmNotionSlashMenu_ItemName_TableOfContents", "TmNotionSlashMenu_ItemDesc_TableOfContents",
            Icons.TableOfContents, SlashMenuCategory.Advanced,
            ["toc", "table", "contents", "outline", "headings", "navigation"]),

        new(BlockType.TemplateButton, "TmNotionSlashMenu_ItemName_TemplateButton",  "TmNotionSlashMenu_ItemDesc_TemplateButton",
            Icons.TemplateButton, SlashMenuCategory.Advanced,
            ["template", "button", "widget", "shortcut"]),

        new(BlockType.Diagram,        "TmNotionSlashMenu_ItemName_Diagram",         "TmNotionSlashMenu_ItemDesc_Diagram",
            Icons.Diagram,        SlashMenuCategory.Advanced,
            ["diagram", "flowchart", "graph", "chart", "flow", "uml"]),

        new(BlockType.Wireframe,      "TmNotionSlashMenu_ItemName_Wireframe",       "TmNotionSlashMenu_ItemDesc_Wireframe",
            Icons.Wireframe,      SlashMenuCategory.Advanced,
            ["wireframe", "mockup", "ui", "design", "prototype", "layout"])
    ];

    // ── Public API ─────────────────────────────────────────────────────────────

    public static IReadOnlyList<SlashMenuItem> All => _all;

    public static SlashMenuItem? FindByType(BlockType type) =>
        Array.Find(_all, i => i.Type == type);

    /// <summary>
    /// Returns filtered, grouped items in display order.
    /// <paramref name="query"/> is matched case-insensitively against Keywords, Name key and Description key raw text.
    /// <paramref name="recentlyUsed"/> drives an optional "Recently used" section at the top.
    /// <paramref name="resolvedNames"/> supplies already-resolved display names for searching (Loc[item.Name] per item).
    /// </summary>
    public static List<(SlashMenuCategory Category, List<SlashMenuItem> Items)> GetGrouped(
        string                        query,
        IReadOnlyList<BlockType>      recentlyUsed,
        Func<SlashMenuItem, string>   resolveName,
        Func<SlashMenuItem, string>   resolveDescription)
    {
        var q = query.Trim().ToLowerInvariant();

        bool Matches(SlashMenuItem item)
        {
            if (q.Length == 0) return true;
            if (item.Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase))) return true;
            if (resolveName(item).Contains(q, StringComparison.OrdinalIgnoreCase))         return true;
            if (resolveDescription(item).Contains(q, StringComparison.OrdinalIgnoreCase))  return true;
            return false;
        }

        var filtered = _all.Where(Matches).ToList();

        var result = new List<(SlashMenuCategory, List<SlashMenuItem>)>();

        // Recently used section (only when no query filter active)
        if (q.Length == 0 && recentlyUsed.Count > 0)
        {
            var recent = recentlyUsed
                .Select(t => filtered.FirstOrDefault(i => i.Type == t))
                .OfType<SlashMenuItem>()
                .ToList();
            if (recent.Count > 0)
                result.Add((SlashMenuCategory.Recent, recent));
        }

        // Remaining categories in defined order
        foreach (SlashMenuCategory cat in Enum.GetValues<SlashMenuCategory>())
        {
            if (cat == SlashMenuCategory.Recent) continue;
            var items = filtered.Where(i => i.Category == cat).ToList();
            if (items.Count > 0)
                result.Add((cat, items));
        }

        return result;
    }
}
