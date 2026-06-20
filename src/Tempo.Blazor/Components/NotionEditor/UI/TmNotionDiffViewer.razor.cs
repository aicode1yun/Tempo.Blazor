using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public enum NotionDiffViewMode
{
    Inline,
    SideBySide
}

public partial class TmNotionDiffViewer : ComponentBase
{
    [Parameter] public IReadOnlyList<BlockDiff> Diffs { get; set; } = [];
    [Parameter] public NotionDiffViewMode ViewMode { get; set; } = NotionDiffViewMode.Inline;
    [Parameter] public bool Loading { get; set; }

    private string ModeClass => ViewMode == NotionDiffViewMode.SideBySide ? "sidebyside" : "inline";

    private IEnumerable<BlockDiff> OrderedDiffs
        => Diffs.OrderBy(diff => diff.AfterOrder ?? diff.BeforeOrder ?? int.MaxValue)
            .ThenBy(diff => diff.BlockId, StringComparer.OrdinalIgnoreCase);

    private static string DiffClass(BlockDiff diff)
        => diff.DiffType.ToString().ToLowerInvariant();

    private string GetBlockHtml(IPageBlock block)
    {
        if (block.Content is ITextBlockContent text)
            return string.IsNullOrWhiteSpace(text.Html)
                ? HtmlEncoder.Default.Encode(Loc["TmNotionDiffViewer_EmptyBlock"])
                : text.Html;

        if (block.Content is ICodeBlockContent code)
            return $"<pre><code>{HtmlEncoder.Default.Encode(code.Code)}</code></pre>";

        if (block.Content is IFileBlockContent file)
            return HtmlEncoder.Default.Encode(string.Join(' ', file.FileName, file.ContentType));

        if (block.Content is IMediaBlockContent media)
            return HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(media.Caption) ? media.Url : media.Caption);

        return HtmlEncoder.Default.Encode(string.Format(Loc["TmNotionDiffViewer_BlockFallback"], block.Type));
    }

    private string GetBlockTypeLabel(IPageBlock? block)
    {
        if (block is null)
            return string.Empty;

        return block.Type switch
        {
            BlockType.Paragraph => Loc["TmNotionPageHistory_BlockParagraph"],
            BlockType.Heading1 => Loc["TmNotionPageHistory_BlockH1"],
            BlockType.Heading2 => Loc["TmNotionPageHistory_BlockH2"],
            BlockType.Heading3 => Loc["TmNotionPageHistory_BlockH3"],
            BlockType.Quote => Loc["TmNotionPageHistory_BlockQuote"],
            BlockType.Callout => Loc["TmNotionPageHistory_BlockCallout"],
            BlockType.Code => Loc["TmNotionPageHistory_BlockCode"],
            BlockType.BulletList => Loc["TmNotionPageHistory_BlockBullet"],
            BlockType.NumberedList => Loc["TmNotionPageHistory_BlockNumbered"],
            BlockType.TodoItem => Loc["TmNotionPageHistory_BlockTodo"],
            BlockType.Toggle => Loc["TmNotionPageHistory_BlockToggle"],
            BlockType.Image => Loc["TmNotionPageHistory_BlockImage"],
            BlockType.Table => Loc["TmNotionPageHistory_BlockTable"],
            BlockType.Divider => Loc["TmNotionPageHistory_BlockDivider"],
            _ => block.Type.ToString()
        };
    }

    private string GetDiffLabel(BlockDiffType type) => type switch
    {
        BlockDiffType.Added => Loc["TmNotionPageHistory_DiffAdded"],
        BlockDiffType.Removed => Loc["TmNotionPageHistory_DiffRemoved"],
        BlockDiffType.Modified => Loc["TmNotionPageHistory_DiffModified"],
        BlockDiffType.Moved => Loc["TmNotionPageHistory_DiffMoved"],
        _ => type.ToString()
    };

    private static string GetDiffSymbol(BlockDiffType type) => type switch
    {
        BlockDiffType.Added => "+",
        BlockDiffType.Removed => "-",
        BlockDiffType.Modified => "~",
        BlockDiffType.Moved => "↕",
        _ => "?"
    };
}
