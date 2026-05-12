using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Applies low-level document operations to a document snapshot.</summary>
public class DocumentOperationApplier
{
    /// <summary>Applies a batch to a document.</summary>
    public DocumentOperationValidationResult Apply(DocumentEditorDocument document, DocumentOperationBatch batch)
    {
        var validation = DocumentOperationLog.Validate(batch);
        if (!validation.IsValid)
        {
            return validation;
        }

        if (!string.Equals(document.DocumentId, batch.DocumentId, StringComparison.Ordinal))
        {
            return DocumentOperationValidationResult.Invalid("Batch document id does not match target document.");
        }

        foreach (var operation in batch.Operations)
        {
            var result = Apply(document, operation);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Applies a single operation to a document.</summary>
    public DocumentOperationValidationResult Apply(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.SchemaVersion != DocumentEditorDocument.CurrentSchemaVersion)
        {
            return DocumentOperationValidationResult.Invalid($"Unsupported operation schema version {operation.SchemaVersion}.");
        }

        return operation.Type switch
        {
            DocumentOperationType.InsertText => ApplyInsertText(document, operation),
            DocumentOperationType.DeleteText => ApplyDeleteText(document, operation),
            DocumentOperationType.AddMark => ApplyMark(document, operation, add: true),
            DocumentOperationType.RemoveMark => ApplyMark(document, operation, add: false),
            DocumentOperationType.InsertBlock => ApplyInsertBlock(document, operation),
            DocumentOperationType.DeleteBlock => ApplyDeleteBlock(document, operation),
            DocumentOperationType.MoveBlock => ApplyMoveBlock(document, operation),
            DocumentOperationType.SetBlockAttribute => ApplySetAttribute(document, operation),
            _ => DocumentOperationValidationResult.Invalid("Unsupported operation type.")
        };
    }

    private static DocumentOperationValidationResult ApplyInsertText(DocumentEditorDocument document, DocumentOperation operation)
    {
        var block = FindBlock(document, operation.Target.BlockId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("InsertText target block was not found or is not text-based.");
        }

        var run = EnsureTextRun(inlines, operation.Target.InlineIndex ?? 0);
        var offset = Math.Clamp(operation.Target.Offset ?? run.Text.Length, 0, run.Text.Length);
        run.Text = run.Text.Insert(offset, operation.Text ?? string.Empty);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyDeleteText(DocumentEditorDocument document, DocumentOperation operation)
    {
        var block = FindBlock(document, operation.Target.BlockId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("DeleteText target block was not found or is not text-based.");
        }

        var inlineIndex = operation.Target.InlineIndex ?? 0;
        if (inlineIndex < 0 || inlineIndex >= inlines.Count || inlines[inlineIndex] is not TextRun run)
        {
            return DocumentOperationValidationResult.Invalid("DeleteText target inline was not found.");
        }

        var offset = Math.Clamp(operation.Target.Offset ?? 0, 0, run.Text.Length);
        var length = Math.Min((operation.Text ?? string.Empty).Length, run.Text.Length - offset);
        run.Text = run.Text.Remove(offset, length);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyMark(DocumentEditorDocument document, DocumentOperation operation, bool add)
    {
        var block = FindBlock(document, operation.Target.BlockId);
        var inlines = GetInlineList(block?.Content);
        var inlineIndex = operation.Target.InlineIndex ?? 0;
        if (block is null || inlines is null || operation.Mark is null || inlineIndex < 0 || inlineIndex >= inlines.Count)
        {
            return DocumentOperationValidationResult.Invalid("Mark target was not found.");
        }

        var inline = inlines[inlineIndex];
        if (add)
        {
            if (!inline.Marks.Any(mark => SameMark(mark, operation.Mark)))
            {
                inline.Marks.Add(Clone(operation.Mark));
            }
        }
        else
        {
            inline.Marks.RemoveAll(mark => SameMark(mark, operation.Mark));
        }

        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyInsertBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.Block is null)
        {
            return DocumentOperationValidationResult.Invalid("InsertBlock requires a block payload.");
        }

        if (document.Blocks.Any(block => block.Id == operation.Block.Id))
        {
            return DocumentOperationValidationResult.Valid();
        }

        var block = Clone(operation.Block);
        if (operation.Target.Order is not null)
        {
            block.Order = operation.Target.Order.Value;
        }

        document.Blocks.Add(block);
        document.Blocks = document.Blocks.OrderBy(item => item.Order).ToList();
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyDeleteBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        document.Blocks.RemoveAll(block => block.Id == operation.Target.BlockId);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyMoveBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        var block = FindBlock(document, operation.Target.BlockId);
        if (block is null || operation.Target.Order is null)
        {
            return DocumentOperationValidationResult.Invalid("MoveBlock requires an existing block and target order.");
        }

        block.Order = operation.Target.Order.Value;
        document.Blocks = document.Blocks.OrderBy(item => item.Order).ToList();
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplySetAttribute(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (string.Equals(operation.AttributeName, "metadata.title", StringComparison.OrdinalIgnoreCase))
        {
            document.Metadata.Title = ReadJsonValue<string>(operation.AttributeValueJson) ?? string.Empty;
            return DocumentOperationValidationResult.Valid();
        }

        var block = FindBlock(document, operation.Target.BlockId);
        if (block is null)
        {
            return DocumentOperationValidationResult.Invalid("SetBlockAttribute target block was not found.");
        }

        if (string.Equals(operation.AttributeName, "text", StringComparison.OrdinalIgnoreCase))
        {
            SetBlockText(block, ReadJsonValue<string>(operation.AttributeValueJson) ?? string.Empty);
            return DocumentOperationValidationResult.Valid();
        }

        if (string.Equals(operation.AttributeName, "order", StringComparison.OrdinalIgnoreCase))
        {
            block.Order = ReadJsonValue<double>(operation.AttributeValueJson);
            document.Blocks = document.Blocks.OrderBy(item => item.Order).ToList();
            return DocumentOperationValidationResult.Valid();
        }

        return DocumentOperationValidationResult.Invalid($"Unsupported attribute '{operation.AttributeName}'.");
    }

    private static DocumentBlock? FindBlock(DocumentEditorDocument document, string? blockId)
    {
        return document.Blocks.FirstOrDefault(block => block.Id == blockId);
    }

    private static List<InlineContent>? GetInlineList(DocumentBlockContent? content)
    {
        return content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    private static TextRun EnsureTextRun(List<InlineContent> inlines, int inlineIndex)
    {
        while (inlines.Count <= inlineIndex)
        {
            inlines.Add(new TextRun());
        }

        if (inlines[inlineIndex] is TextRun run)
        {
            return run;
        }

        run = new TextRun { Text = GetInlineText(inlines[inlineIndex]) };
        inlines[inlineIndex] = run;
        return run;
    }

    private static void SetBlockText(DocumentBlock block, string text)
    {
        var inlines = GetInlineList(block.Content);
        if (inlines is null)
        {
            return;
        }

        inlines.Clear();
        inlines.Add(new TextRun { Text = text });
    }

    private static string GetInlineText(InlineContent inline)
    {
        return inline switch
        {
            TextRun run => run.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            _ => string.Empty
        };
    }

    private static T? ReadJsonValue<T>(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options);
    }

    private static bool SameMark(InlineMark left, InlineMark right)
    {
        return left.Type == right.Type
            && left.Link?.Href == right.Link?.Href
            && left.CommentAnchor?.CommentId == right.CommentAnchor?.CommentId
            && left.Value == right.Value;
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
