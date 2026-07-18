using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>
/// Metadata contract for document-assembly constructs. Assembly state rides the content control's
/// <see cref="DocumentContentControl.Metadata"/> dictionary (preserved across runtimes and DOCX
/// round-trips via the SDT JSON payload), so no new model kinds are needed.
/// </summary>
public static class DocumentAssemblyMetadata
{
    /// <summary>Metadata key holding the conditional branch kind: "if", "elseif", or "else".</summary>
    public const string BranchKey = "tmAssembly:branch";

    /// <summary>Metadata key holding the condition expression.</summary>
    public const string ExpressionKey = "tmAssembly:expression";

    /// <summary>Metadata key grouping an IF/ELSEIF/ELSE chain.</summary>
    public const string GroupKey = "tmAssembly:group";

    /// <summary>Metadata key holding the collection token key a repeating section binds to.</summary>
    public const string BindKey = "tmAssembly:bind";

    /// <summary>Creates a block-scope content control representing one branch of a conditional chain.</summary>
    public static DocumentContentControl CreateConditionalBlock(string branch, string? expression, string groupId)
    {
        var control = new DocumentContentControl
        {
            Kind = DocumentContentControlKind.RichText,
            Scope = DocumentContentControlScope.Block,
            Alias = branch switch
            {
                "else" => "ELSE",
                "elseif" => $"ELSE IF {expression}",
                _ => $"IF {expression}",
            },
            LockContent = false,
        };
        control.Metadata[BranchKey] = branch;
        control.Metadata[ExpressionKey] = expression;
        control.Metadata[GroupKey] = groupId;
        return control;
    }

    /// <summary>Creates a repeating-section control bound to a collection token.</summary>
    public static DocumentContentControl CreateRepeatingSection(string bindTokenKey)
    {
        var control = new DocumentContentControl
        {
            Kind = DocumentContentControlKind.RepeatingSection,
            Scope = DocumentContentControlScope.Block,
            Alias = $"REPEAT {bindTokenKey}",
        };
        control.Metadata[BindKey] = bindTokenKey;
        return control;
    }

    /// <summary>Returns the conditional branch kind, or null when the control is not a conditional block.</summary>
    public static string? GetBranch(DocumentContentControl control)
        => control.Metadata.TryGetValue(BranchKey, out var branch) && !string.IsNullOrWhiteSpace(branch)
            ? branch
            : null;

    /// <summary>Returns the condition expression, or null.</summary>
    public static string? GetExpression(DocumentContentControl control)
        => control.Metadata.TryGetValue(ExpressionKey, out var expression) ? expression : null;

    /// <summary>Returns the conditional chain group id, or null.</summary>
    public static string? GetGroup(DocumentContentControl control)
        => control.Metadata.TryGetValue(GroupKey, out var group) ? group : null;

    /// <summary>Returns the repeating-section binding token key, or null.</summary>
    public static string? GetBinding(DocumentContentControl control)
        => control.Kind == DocumentContentControlKind.RepeatingSection
           && control.Metadata.TryGetValue(BindKey, out var bind)
           && !string.IsNullOrWhiteSpace(bind)
            ? bind
            : null;
}

/// <summary>Options for <see cref="DocumentAssemblyService"/>.</summary>
public sealed record DocumentAssemblyOptions
{
    /// <summary>Clock injected for deterministic date functions.</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Assembles a template document with token values: evaluates IF/ELSEIF/ELSE conditional block
/// chains (keeping the winning branch's content, unwrapped), expands repeating sections bound to
/// collection rows (row columns shadow token keys), and replaces token runs — plain keys and
/// computed <see cref="TokenRun.Expression"/>s — with their resolved text. Invalid condition
/// expressions fail closed (the branch is skipped), so a broken template never leaks conditional
/// content.
/// </summary>
public sealed class DocumentAssemblyService
{
    /// <summary>Assembles the template into a plain document.</summary>
    public DocumentEditorDocument Assemble(
        DocumentEditorDocument template,
        IReadOnlyDictionary<string, DocumentTokenValue> tokenValues,
        DocumentAssemblyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(tokenValues);
        options ??= new DocumentAssemblyOptions();
        var context = new DocumentAssemblyContext { TokenValues = tokenValues, Now = options.Now };

        var assembled = Clone(template);
        assembled.Blocks = AssembleBlocks(assembled.Blocks, context);
        for (var i = 0; i < assembled.Blocks.Count; i++)
        {
            assembled.Blocks[i].Order = i + 1;
        }

        return assembled;
    }

    private static List<DocumentBlock> AssembleBlocks(List<DocumentBlock> blocks, DocumentAssemblyContext context)
    {
        var result = new List<DocumentBlock>();
        var index = 0;
        while (index < blocks.Count)
        {
            var block = blocks[index];
            if (block.Content is ContentControlBlockContent control)
            {
                if (DocumentAssemblyMetadata.GetBranch(control.Control) is not null)
                {
                    index = AssembleConditionalChain(blocks, index, context, result);
                    continue;
                }

                if (DocumentAssemblyMetadata.GetBinding(control.Control) is { } binding)
                {
                    result.AddRange(AssembleRepeatingSection(control, binding, context));
                    index++;
                    continue;
                }
            }

            ResolveBlockTokens(block, context);
            result.Add(block);
            index++;
        }

        return result;
    }

    private static int AssembleConditionalChain(
        List<DocumentBlock> blocks,
        int startIndex,
        DocumentAssemblyContext context,
        List<DocumentBlock> result)
    {
        var group = DocumentAssemblyMetadata.GetGroup(((ContentControlBlockContent)blocks[startIndex].Content).Control);
        DocumentBlock? winner = null;
        var index = startIndex;
        while (index < blocks.Count
               && blocks[index].Content is ContentControlBlockContent candidate
               && DocumentAssemblyMetadata.GetBranch(candidate.Control) is { } branch
               && string.Equals(DocumentAssemblyMetadata.GetGroup(candidate.Control), group, StringComparison.Ordinal))
        {
            if (winner is null)
            {
                var expression = DocumentAssemblyMetadata.GetExpression(candidate.Control);
                var isMatch = branch == "else" || EvaluateConditionSafe(expression, context);
                if (isMatch)
                {
                    winner = blocks[index];
                }
            }

            index++;
        }

        if (winner?.Content is ContentControlBlockContent winningControl)
        {
            result.AddRange(AssembleBlocks(winningControl.Blocks, context));
        }

        return index;
    }

    // Fail closed: an invalid or empty expression never shows conditional content.
    private static bool EvaluateConditionSafe(string? expression, DocumentAssemblyContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            return DocumentAssemblyExpression.EvaluateCondition(expression, context);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IEnumerable<DocumentBlock> AssembleRepeatingSection(
        ContentControlBlockContent control,
        string binding,
        DocumentAssemblyContext context)
    {
        if (!context.TokenValues.TryGetValue(binding, out var token) || token.Rows is not { Count: > 0 } rows)
        {
            yield break;
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowContext = new DocumentAssemblyContext
            {
                TokenValues = context.TokenValues,
                RowScope = rows[rowIndex],
                Now = context.Now,
            };
            foreach (var inner in AssembleBlocks(Clone(control.Blocks), rowContext))
            {
                inner.Id = $"{inner.Id}-row{rowIndex + 1}";
                yield return inner;
            }
        }
    }

    private static void ResolveBlockTokens(DocumentBlock block, DocumentAssemblyContext context)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent paragraph:
                ResolveInlineTokens(paragraph.Inlines, context);
                break;
            case HeadingBlockContent heading:
                ResolveInlineTokens(heading.Inlines, context);
                break;
            case ListBlockContent list:
                ResolveInlineTokens(list.Inlines, context);
                break;
            case QuoteBlockContent quote:
                ResolveInlineTokens(quote.Inlines, context);
                break;
            case TableBlockContent table:
                foreach (var cellBlock in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks))
                {
                    ResolveBlockTokens(cellBlock, context);
                }

                break;
        }
    }

    private static void ResolveInlineTokens(List<InlineContent> inlines, DocumentAssemblyContext context)
    {
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is not TokenRun token)
            {
                continue;
            }

            inlines[i] = new TextRun
            {
                Text = ResolveTokenText(token, context),
                Marks = token.Marks.Select(Clone).ToList(),
            };
        }
    }

    private static string ResolveTokenText(TokenRun token, DocumentAssemblyContext context)
    {
        if (!string.IsNullOrWhiteSpace(token.Expression))
        {
            try
            {
                return DocumentAssemblyExpression.Evaluate(token.Expression, context).ToInvariantString();
            }
            catch (FormatException)
            {
                return token.FallbackText ?? $"{{{{{token.Key}}}}}";
            }
        }

        if (context.RowScope is { } row)
        {
            if (row.TryGetValue(token.Key, out var direct))
            {
                return direct ?? string.Empty;
            }

            var dotIndex = token.Key.IndexOf('.');
            if (dotIndex > 0 && row.TryGetValue(token.Key[(dotIndex + 1)..], out var suffix))
            {
                return suffix ?? string.Empty;
            }
        }

        if (context.TokenValues.TryGetValue(token.Key, out var value) && value.HasValue)
        {
            return value.DisplayValue ?? value.Value ?? string.Empty;
        }

        return !string.IsNullOrWhiteSpace(token.FallbackText)
            ? token.FallbackText!
            : $"{{{{{token.Key}}}}}";
    }

    private static T Clone<T>(T value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, DocumentEditorJson.Options), DocumentEditorJson.Options)
           ?? throw new JsonException("Could not clone document for assembly.");
}
