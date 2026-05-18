using System.Collections.ObjectModel;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Logical editing regions supported by the document editor schema.</summary>
public enum DocumentEditorRegion
{
    /// <summary>Main document body.</summary>
    Body,

    /// <summary>Page header region.</summary>
    Header,

    /// <summary>Page footer region.</summary>
    Footer,

    /// <summary>Nested table cell region.</summary>
    TableCell,

    /// <summary>Footnote text region.</summary>
    Footnote,

    /// <summary>Endnote text region.</summary>
    Endnote,

    /// <summary>Image caption region.</summary>
    Caption,

    /// <summary>Selected image object region.</summary>
    Image
}

/// <summary>Inline content context used when validating marks.</summary>
public enum DocumentInlineContext
{
    /// <summary>Plain text run.</summary>
    Text,

    /// <summary>Token or merge-field run.</summary>
    Token,

    /// <summary>Footnote or endnote reference run.</summary>
    NoteReference
}

/// <summary>Semantic insertion kind used by schema queries that are not one-to-one block types.</summary>
public enum DocumentInsertionKind
{
    /// <summary>Paragraph block insertion.</summary>
    Paragraph,

    /// <summary>Heading block insertion.</summary>
    Heading,

    /// <summary>List block insertion.</summary>
    List,

    /// <summary>Quote block insertion.</summary>
    Quote,

    /// <summary>Table block insertion.</summary>
    Table,

    /// <summary>Image block insertion.</summary>
    Image,

    /// <summary>Page break block insertion.</summary>
    PageBreak,

    /// <summary>Footnote reference insertion.</summary>
    Footnote,

    /// <summary>Endnote reference insertion.</summary>
    Endnote
}

/// <summary>Immutable document editor schema used by paste, import, command, and save validation.</summary>
public sealed class DocumentEditorSchema
{
    private readonly IReadOnlyDictionary<DocumentBlockType, DocumentBlockRule> _blockRules;
    private readonly IReadOnlyDictionary<DocumentInsertionKind, DocumentInsertionRule> _insertionRules;
    private readonly IReadOnlyDictionary<InlineMarkType, DocumentMarkRule> _markRules;

    internal DocumentEditorSchema(
        IReadOnlyDictionary<DocumentBlockType, DocumentBlockRule> blockRules,
        IReadOnlyDictionary<DocumentInsertionKind, DocumentInsertionRule> insertionRules,
        IReadOnlyDictionary<InlineMarkType, DocumentMarkRule> markRules)
    {
        _blockRules = blockRules;
        _insertionRules = insertionRules;
        _markRules = markRules;
    }

    /// <summary>Registered block rules.</summary>
    public IReadOnlyDictionary<DocumentBlockType, DocumentBlockRule> BlockRules => _blockRules;

    /// <summary>Registered semantic insertion rules.</summary>
    public IReadOnlyDictionary<DocumentInsertionKind, DocumentInsertionRule> InsertionRules => _insertionRules;

    /// <summary>Registered inline mark rules.</summary>
    public IReadOnlyDictionary<InlineMarkType, DocumentMarkRule> MarkRules => _markRules;

    /// <summary>Returns whether the block type can be inserted in the region.</summary>
    public bool CanInsert(DocumentBlockType blockType, DocumentEditorRegion region) =>
        _blockRules.TryGetValue(blockType, out var rule) && rule.AllowedRegions.Contains(region);

    /// <summary>Returns whether the semantic insertion kind can be inserted in the region.</summary>
    public bool CanInsert(DocumentInsertionKind insertionKind, DocumentEditorRegion region) =>
        _insertionRules.TryGetValue(insertionKind, out var rule) && rule.AllowedRegions.Contains(region);

    /// <summary>Returns whether the inline mark can be applied to the current inline context.</summary>
    public bool CanApplyMark(InlineMarkType markType, DocumentInlineContext context) =>
        _markRules.TryGetValue(markType, out var rule) && rule.AllowedContexts.Contains(context);

    /// <summary>Returns whether a mark changes review semantics and should participate in tracked changes.</summary>
    public bool MarkAffectsReview(InlineMarkType markType) =>
        _markRules.TryGetValue(markType, out var rule) && rule.AffectsReview;
}

/// <summary>Immutable placement rule for a block type.</summary>
public sealed record DocumentBlockRule(DocumentBlockType Type, IReadOnlySet<DocumentEditorRegion> AllowedRegions);

/// <summary>Immutable placement rule for a semantic insertion kind.</summary>
public sealed record DocumentInsertionRule(DocumentInsertionKind Kind, IReadOnlySet<DocumentEditorRegion> AllowedRegions);

/// <summary>Immutable rule for an inline mark.</summary>
public sealed record DocumentMarkRule(InlineMarkType Type, IReadOnlySet<DocumentInlineContext> AllowedContexts, bool AffectsReview);

/// <summary>Fluent builder for <see cref="DocumentEditorSchema"/>.</summary>
public sealed class DocumentEditorSchemaBuilder
{
    private readonly Dictionary<DocumentBlockType, HashSet<DocumentEditorRegion>> _blockRegions = [];
    private readonly Dictionary<DocumentInsertionKind, HashSet<DocumentEditorRegion>> _insertionRegions = [];
    private readonly Dictionary<InlineMarkType, HashSet<DocumentInlineContext>> _markContexts = [];
    private readonly HashSet<InlineMarkType> _reviewMarks = [];

    /// <summary>Configures a block type by string name, for example "paragraph".</summary>
    public DocumentBlockRuleBuilder Block(string blockType) => Block(ParseBlockType(blockType));

    /// <summary>Configures a block type.</summary>
    public DocumentBlockRuleBuilder Block(DocumentBlockType blockType)
    {
        EnsureBlock(blockType);
        return new DocumentBlockRuleBuilder(this, blockType);
    }

    /// <summary>Configures a semantic insertion kind.</summary>
    public DocumentInsertionRuleBuilder Insertion(DocumentInsertionKind kind)
    {
        EnsureInsertion(kind);
        return new DocumentInsertionRuleBuilder(this, kind);
    }

    /// <summary>Configures an inline mark.</summary>
    public DocumentMarkRuleBuilder Mark(InlineMarkType markType)
    {
        EnsureMark(markType);
        return new DocumentMarkRuleBuilder(this, markType);
    }

    /// <summary>Builds an immutable schema snapshot.</summary>
    public DocumentEditorSchema Build()
    {
        var blockRules = _blockRegions.ToDictionary(
            pair => pair.Key,
            pair => new DocumentBlockRule(pair.Key, ToReadOnlySet(pair.Value)));
        var insertionRules = _insertionRegions.ToDictionary(
            pair => pair.Key,
            pair => new DocumentInsertionRule(pair.Key, ToReadOnlySet(pair.Value)));
        var markRules = _markContexts.ToDictionary(
            pair => pair.Key,
            pair => new DocumentMarkRule(pair.Key, ToReadOnlySet(pair.Value), _reviewMarks.Contains(pair.Key)));

        return new DocumentEditorSchema(
            new ReadOnlyDictionary<DocumentBlockType, DocumentBlockRule>(blockRules),
            new ReadOnlyDictionary<DocumentInsertionKind, DocumentInsertionRule>(insertionRules),
            new ReadOnlyDictionary<InlineMarkType, DocumentMarkRule>(markRules));
    }

    internal DocumentEditorSchemaBuilder Allow(DocumentBlockType blockType, params DocumentEditorRegion[] regions)
    {
        EnsureBlock(blockType).UnionWith(regions);
        return this;
    }

    internal DocumentEditorSchemaBuilder Disallow(DocumentBlockType blockType, params DocumentEditorRegion[] regions)
    {
        foreach (var region in regions)
        {
            EnsureBlock(blockType).Remove(region);
        }

        return this;
    }

    internal DocumentEditorSchemaBuilder Allow(DocumentInsertionKind kind, params DocumentEditorRegion[] regions)
    {
        EnsureInsertion(kind).UnionWith(regions);
        return this;
    }

    internal DocumentEditorSchemaBuilder Disallow(DocumentInsertionKind kind, params DocumentEditorRegion[] regions)
    {
        foreach (var region in regions)
        {
            EnsureInsertion(kind).Remove(region);
        }

        return this;
    }

    internal DocumentEditorSchemaBuilder Allow(InlineMarkType markType, params DocumentInlineContext[] contexts)
    {
        EnsureMark(markType).UnionWith(contexts);
        return this;
    }

    internal DocumentEditorSchemaBuilder Disallow(InlineMarkType markType, params DocumentInlineContext[] contexts)
    {
        foreach (var context in contexts)
        {
            EnsureMark(markType).Remove(context);
        }

        return this;
    }

    internal DocumentEditorSchemaBuilder AffectsReview(InlineMarkType markType, bool affectsReview = true)
    {
        if (affectsReview)
        {
            _reviewMarks.Add(markType);
        }
        else
        {
            _reviewMarks.Remove(markType);
        }

        EnsureMark(markType);
        return this;
    }

    private HashSet<DocumentEditorRegion> EnsureBlock(DocumentBlockType blockType)
    {
        if (!_blockRegions.TryGetValue(blockType, out var regions))
        {
            regions = [];
            _blockRegions[blockType] = regions;
        }

        return regions;
    }

    private HashSet<DocumentEditorRegion> EnsureInsertion(DocumentInsertionKind kind)
    {
        if (!_insertionRegions.TryGetValue(kind, out var regions))
        {
            regions = [];
            _insertionRegions[kind] = regions;
        }

        return regions;
    }

    private HashSet<DocumentInlineContext> EnsureMark(InlineMarkType markType)
    {
        if (!_markContexts.TryGetValue(markType, out var contexts))
        {
            contexts = [];
            _markContexts[markType] = contexts;
        }

        return contexts;
    }

    private static DocumentBlockType ParseBlockType(string blockType)
    {
        if (Enum.TryParse<DocumentBlockType>(blockType, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Unsupported document block type '{blockType}'.", nameof(blockType));
    }

    private static IReadOnlySet<T> ToReadOnlySet<T>(HashSet<T> values)
        where T : struct, Enum =>
        new HashSet<T>(values);
}

/// <summary>Fluent block rule builder.</summary>
public readonly struct DocumentBlockRuleBuilder
{
    private readonly DocumentEditorSchemaBuilder _builder;
    private readonly DocumentBlockType _blockType;

    internal DocumentBlockRuleBuilder(DocumentEditorSchemaBuilder builder, DocumentBlockType blockType)
    {
        _builder = builder;
        _blockType = blockType;
    }

    /// <summary>Allows the block in the provided regions.</summary>
    public DocumentBlockRuleBuilder AllowIn(params DocumentEditorRegion[] regions)
    {
        _builder.Allow(_blockType, regions);
        return this;
    }

    /// <summary>Allows the block in the provided region names.</summary>
    public DocumentBlockRuleBuilder AllowIn(params string[] regions) => AllowIn(ParseRegions(regions));

    /// <summary>Disallows the block in the provided regions.</summary>
    public DocumentBlockRuleBuilder DisallowIn(params DocumentEditorRegion[] regions)
    {
        _builder.Disallow(_blockType, regions);
        return this;
    }

    /// <summary>Disallows the block in the provided region names.</summary>
    public DocumentBlockRuleBuilder DisallowIn(params string[] regions) => DisallowIn(ParseRegions(regions));

    private static DocumentEditorRegion[] ParseRegions(IEnumerable<string> regions) =>
        regions.Select(DocumentEditorSchemaNames.ParseRegion).ToArray();
}

/// <summary>Fluent semantic insertion rule builder.</summary>
public readonly struct DocumentInsertionRuleBuilder
{
    private readonly DocumentEditorSchemaBuilder _builder;
    private readonly DocumentInsertionKind _kind;

    internal DocumentInsertionRuleBuilder(DocumentEditorSchemaBuilder builder, DocumentInsertionKind kind)
    {
        _builder = builder;
        _kind = kind;
    }

    /// <summary>Allows the insertion kind in the provided regions.</summary>
    public DocumentInsertionRuleBuilder AllowIn(params DocumentEditorRegion[] regions)
    {
        _builder.Allow(_kind, regions);
        return this;
    }

    /// <summary>Disallows the insertion kind in the provided regions.</summary>
    public DocumentInsertionRuleBuilder DisallowIn(params DocumentEditorRegion[] regions)
    {
        _builder.Disallow(_kind, regions);
        return this;
    }
}

/// <summary>Fluent inline mark rule builder.</summary>
public readonly struct DocumentMarkRuleBuilder
{
    private readonly DocumentEditorSchemaBuilder _builder;
    private readonly InlineMarkType _markType;

    internal DocumentMarkRuleBuilder(DocumentEditorSchemaBuilder builder, InlineMarkType markType)
    {
        _builder = builder;
        _markType = markType;
    }

    /// <summary>Allows the mark in the provided contexts.</summary>
    public DocumentMarkRuleBuilder AllowIn(params DocumentInlineContext[] contexts)
    {
        _builder.Allow(_markType, contexts);
        return this;
    }

    /// <summary>Disallows the mark in the provided contexts.</summary>
    public DocumentMarkRuleBuilder DisallowIn(params DocumentInlineContext[] contexts)
    {
        _builder.Disallow(_markType, contexts);
        return this;
    }

    /// <summary>Marks this inline mark as relevant for tracked review state.</summary>
    public DocumentMarkRuleBuilder AffectsReview(bool affectsReview = true)
    {
        _builder.AffectsReview(_markType, affectsReview);
        return this;
    }
}

/// <summary>Factory for the editor default schema.</summary>
public static class DocumentEditorDefaultSchema
{
    /// <summary>Creates the default schema used by the built-in document editor features.</summary>
    public static DocumentEditorSchema Create()
    {
        var builder = new DocumentEditorSchemaBuilder();
        var textRegions = new[]
        {
            DocumentEditorRegion.Body,
            DocumentEditorRegion.Header,
            DocumentEditorRegion.Footer,
            DocumentEditorRegion.TableCell,
            DocumentEditorRegion.Footnote,
            DocumentEditorRegion.Endnote
        };

        builder.Block(DocumentBlockType.Paragraph).AllowIn(textRegions);
        builder.Block(DocumentBlockType.Heading).AllowIn(DocumentEditorRegion.Body);
        builder.Block(DocumentBlockType.List).AllowIn(textRegions);
        builder.Block(DocumentBlockType.Quote).AllowIn(textRegions);
        builder.Block(DocumentBlockType.Table).AllowIn(DocumentEditorRegion.Body);
        builder.Block(DocumentBlockType.Image).AllowIn(DocumentEditorRegion.Body, DocumentEditorRegion.TableCell);
        builder.Block(DocumentBlockType.PageBreak).AllowIn(DocumentEditorRegion.Body);

        foreach (DocumentInsertionKind kind in Enum.GetValues<DocumentInsertionKind>())
        {
            var regions = kind switch
            {
                DocumentInsertionKind.Heading => [DocumentEditorRegion.Body],
                DocumentInsertionKind.Table => [DocumentEditorRegion.Body],
                DocumentInsertionKind.Image => [DocumentEditorRegion.Body, DocumentEditorRegion.TableCell],
                DocumentInsertionKind.PageBreak => [DocumentEditorRegion.Body],
                DocumentInsertionKind.Footnote or DocumentInsertionKind.Endnote => [DocumentEditorRegion.Body],
                _ => textRegions
            };
            builder.Insertion(kind).AllowIn(regions);
        }

        foreach (InlineMarkType markType in Enum.GetValues<InlineMarkType>())
        {
            builder.Mark(markType)
                .AllowIn(DocumentInlineContext.Text, DocumentInlineContext.Token, DocumentInlineContext.NoteReference);
        }

        builder.Mark(InlineMarkType.Link).DisallowIn(DocumentInlineContext.Token);
        builder.Mark(InlineMarkType.CommentAnchor).AffectsReview();
        builder.Mark(InlineMarkType.Revision).AffectsReview();

        return builder.Build();
    }
}

/// <summary>Applies schema-aware insertion fallback rules for paste and import flows.</summary>
public sealed class DocumentInsertionPolicy
{
    private readonly DocumentEditorSchema _schema;

    /// <summary>Creates a policy backed by the default schema.</summary>
    public DocumentInsertionPolicy()
        : this(DocumentEditorDefaultSchema.Create())
    {
    }

    /// <summary>Creates a policy backed by a custom schema.</summary>
    public DocumentInsertionPolicy(DocumentEditorSchema schema)
    {
        _schema = schema;
    }

    /// <summary>Applies insertion rules to a sequence of blocks.</summary>
    public DocumentInsertionPolicyResult Apply(IEnumerable<DocumentBlock> blocks, DocumentEditorRegion region)
    {
        var normalized = new List<DocumentBlock>();
        var warnings = new List<DocumentInsertionWarning>();

        foreach (var block in blocks)
        {
            ApplyBlock(CloneBlock(block), region, normalized, warnings);
        }

        return new DocumentInsertionPolicyResult(normalized, warnings);
    }

    private void ApplyBlock(
        DocumentBlock block,
        DocumentEditorRegion region,
        List<DocumentBlock> normalized,
        List<DocumentInsertionWarning> warnings)
    {
        if (!Enum.IsDefined(block.Type))
        {
            normalized.Add(CreateParagraph());
            warnings.Add(DocumentInsertionWarning.UnknownBlockFallback(region));
            return;
        }

        if (!_schema.CanInsert(block.Type, region))
        {
            if (block.Type == DocumentBlockType.Table && region == DocumentEditorRegion.TableCell)
            {
                UnwrapTable(block, normalized, warnings);
                warnings.Add(new DocumentInsertionWarning("table-unwrapped-in-table-cell", "Nested tables are flattened when pasted into a table cell.", region, block.Type));
                return;
            }

            warnings.Add(new DocumentInsertionWarning("block-rejected-by-schema", $"Block '{block.Type}' is not allowed in region '{region}'.", region, block.Type));
            return;
        }

        if (block.Content is ImageBlockContent image && image.AltText is null)
        {
            image.AltText = string.Empty;
            warnings.Add(new DocumentInsertionWarning("image-alt-text-defaulted", "Image alternative text was defaulted to an empty value.", region, block.Type));
        }

        if (block.Content is TableBlockContent table)
        {
            NormalizeTableCells(table, warnings);
        }

        normalized.Add(block);
    }

    private void NormalizeTableCells(TableBlockContent table, List<DocumentInsertionWarning> warnings)
    {
        foreach (var cell in table.Rows.SelectMany(row => row.Cells))
        {
            var cellBlocks = new List<DocumentBlock>();
            foreach (var child in cell.Blocks)
            {
                ApplyBlock(CloneBlock(child), DocumentEditorRegion.TableCell, cellBlocks, warnings);
            }

            cell.Blocks = cellBlocks.Count > 0 ? cellBlocks : [CreateParagraph()];
        }
    }

    private void UnwrapTable(DocumentBlock tableBlock, List<DocumentBlock> normalized, List<DocumentInsertionWarning> warnings)
    {
        if (tableBlock.Content is not TableBlockContent table)
        {
            return;
        }

        var before = normalized.Count;
        foreach (var cell in table.Rows.SelectMany(row => row.Cells))
        {
            foreach (var child in cell.Blocks)
            {
                ApplyBlock(CloneBlock(child), DocumentEditorRegion.TableCell, normalized, warnings);
            }
        }

        if (normalized.Count == before)
        {
            normalized.Add(CreateParagraph());
        }
    }

    private static DocumentBlock CreateParagraph() =>
        new()
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent()
        };

    private static DocumentBlock CloneBlock(DocumentBlock block)
    {
        var json = JsonSerializer.Serialize(block);
        return JsonSerializer.Deserialize<DocumentBlock>(json) ?? CreateParagraph();
    }
}

/// <summary>Result of applying insertion policy rules.</summary>
public sealed record DocumentInsertionPolicyResult(
    IReadOnlyList<DocumentBlock> Blocks,
    IReadOnlyList<DocumentInsertionWarning> Warnings);

/// <summary>Warning emitted while normalizing an insertion.</summary>
public sealed record DocumentInsertionWarning(string Code, string Message, DocumentEditorRegion Region, DocumentBlockType? BlockType = null)
{
    /// <summary>Creates the warning used when an unknown block is converted to a paragraph.</summary>
    public static DocumentInsertionWarning UnknownBlockFallback(DocumentEditorRegion region) =>
        new("unknown-block-fallback", "Unknown block type was converted to a paragraph.", region);
}

/// <summary>Runs structural post-fixers before import, remote sync, and save boundaries.</summary>
public sealed class DocumentEditorPostFixer
{
    /// <summary>Applies built-in post-fixers to the document.</summary>
    public DocumentPostFixerResult Fix(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var warnings = new List<DocumentPostFixerWarning>();
        EnsureHeaderFooterPlaceholders(document, warnings);
        EnsureTableCellPlaceholders(GetAllBlockLists(document), warnings);
        MarkOrphanedCommentAnchors(document, warnings);
        MarkUnusedDraftAssets(document, warnings);
        PreservePendingRevisionRanges(document, warnings);

        return new DocumentPostFixerResult(document, warnings);
    }

    private static void EnsureHeaderFooterPlaceholders(DocumentEditorDocument document, List<DocumentPostFixerWarning> warnings)
    {
        foreach (var headerFooter in document.HeadersFooters.Where(item => item.Blocks.Count == 0))
        {
            headerFooter.Blocks.Add(CreateParagraph());
            warnings.Add(new DocumentPostFixerWarning("empty-header-footer-placeholder", $"Header/footer '{headerFooter.Id}' received a paragraph placeholder."));
        }
    }

    private static void EnsureTableCellPlaceholders(IEnumerable<List<DocumentBlock>> blockLists, List<DocumentPostFixerWarning> warnings)
    {
        foreach (var block in blockLists.SelectMany(blocks => blocks).ToArray())
        {
            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            foreach (var cell in table.Rows.SelectMany(row => row.Cells))
            {
                if (cell.Blocks.Count == 0)
                {
                    cell.Blocks.Add(CreateParagraph());
                    warnings.Add(new DocumentPostFixerWarning("empty-table-cell-placeholder", $"Table cell '{cell.Id}' received a paragraph placeholder."));
                }

                EnsureTableCellPlaceholders([cell.Blocks], warnings);
            }
        }
    }

    private static void MarkOrphanedCommentAnchors(DocumentEditorDocument document, List<DocumentPostFixerWarning> warnings)
    {
        var blockIds = GetAllBlocks(document)
            .Select(block => block.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var comment in document.Comments)
        {
            var blockId = comment.Anchor.BlockId;
            if (!string.IsNullOrWhiteSpace(blockId) && !blockIds.Contains(blockId))
            {
                comment.Anchor.IsOrphaned = true;
                warnings.Add(new DocumentPostFixerWarning("orphaned-comment-anchor", $"Comment '{comment.Id}' points to a missing block."));
            }
        }
    }

    private static void MarkUnusedDraftAssets(DocumentEditorDocument document, List<DocumentPostFixerWarning> warnings)
    {
        var usedAssetIds = GetAllBlocks(document)
            .Select(block => block.Content)
            .OfType<ImageBlockContent>()
            .Select(image => image.AssetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var asset in document.Assets.Where(asset => asset.IsLocalDraft && !usedAssetIds.Contains(asset.Id)))
        {
            asset.IsUnusedDraft = true;
            warnings.Add(new DocumentPostFixerWarning("unused-image-asset-draft", $"Local draft image asset '{asset.Id}' is no longer referenced."));
        }
    }

    private static void PreservePendingRevisionRanges(DocumentEditorDocument document, List<DocumentPostFixerWarning> warnings)
    {
        var blockIds = GetAllBlocks(document)
            .Select(block => block.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var revision in document.Revisions.Where(revision => revision.Action == DocumentRevisionAction.Pending))
        {
            if (!string.IsNullOrWhiteSpace(revision.Range.BlockId) && !blockIds.Contains(revision.Range.BlockId))
            {
                warnings.Add(new DocumentPostFixerWarning("pending-revision-missing-range", $"Pending revision '{revision.Id}' references a missing block and is kept for review."));
            }
        }
    }

    private static IEnumerable<List<DocumentBlock>> GetAllBlockLists(DocumentEditorDocument document)
    {
        yield return document.Blocks;

        foreach (var note in document.Notes)
        {
            yield return note.Blocks;
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            yield return headerFooter.Blocks;
        }
    }

    private static IEnumerable<DocumentBlock> GetAllBlocks(DocumentEditorDocument document)
    {
        foreach (var blocks in GetAllBlockLists(document))
        {
            foreach (var block in WalkBlocks(blocks))
            {
                yield return block;
            }
        }
    }

    private static IEnumerable<DocumentBlock> WalkBlocks(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block.Content is TableBlockContent table)
            {
                foreach (var child in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => WalkBlocks(cell.Blocks)))
                {
                    yield return child;
                }
            }
        }
    }

    private static DocumentBlock CreateParagraph() =>
        new()
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent()
        };
}

/// <summary>Result of running document post-fixers.</summary>
public sealed record DocumentPostFixerResult(DocumentEditorDocument Document, IReadOnlyList<DocumentPostFixerWarning> Warnings);

/// <summary>Post-fixer warning with a stable code.</summary>
public sealed record DocumentPostFixerWarning(string Code, string Message);

/// <summary>Shared parser for schema names coming from JS and host code.</summary>
public static class DocumentEditorSchemaNames
{
    /// <summary>Parses a region name, returning Body for empty values.</summary>
    public static DocumentEditorRegion ParseRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return DocumentEditorRegion.Body;
        }

        return Enum.TryParse<DocumentEditorRegion>(region, ignoreCase: true, out var parsed)
            ? parsed
            : DocumentEditorRegion.Body;
    }
}
