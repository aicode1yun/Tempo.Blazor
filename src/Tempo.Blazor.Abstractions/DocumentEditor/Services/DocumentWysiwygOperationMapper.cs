using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Maps WYSIWYG patch events from the JS engine to document operations.</summary>
public class DocumentWysiwygOperationMapper
{
    /// <summary>Creates an operation batch from a WYSIWYG patch.</summary>
    public DocumentOperationBatch CreateBatch(
        DocumentEditorDocument before,
        WysiwygPatch patch,
        DocumentOperationMetadata metadata)
    {
        var operations = new List<DocumentOperation>();
        MapPatch(before, patch, metadata, operations);

        return new DocumentOperationBatch
        {
            DocumentId = before.DocumentId,
            Operations = operations
        };
    }

    private static void MapPatch(
        DocumentEditorDocument before,
        WysiwygPatch patch,
        DocumentOperationMetadata metadata,
        List<DocumentOperation> operations)
    {
        switch (patch.Type)
        {
            case "InsertText":
            case "InsertInline":
            case "DeleteRange":
            case "SetMarks":
            case "SetParagraphProperties":
            case "InsertBlock":
            case "SplitBlock":
            case "InsertSoftBreak":
            case "UpdateBlock":
            case "RemoveBlock":
            case "Paste":
                var op = new DocumentOperation
                {
                    Type = MapPatchType(patch.Type),
                    Target = MapSelection(patch),
                    Metadata = metadata
                };

                if (patch.Block is not null)
                {
                    op.Block = patch.Block;
                }

                if (patch.Data is not null)
                {
                    op.Text = patch.Data;
                }

                operations.Add(op);
                break;
        }
    }

    private static DocumentOperationType MapPatchType(string patchType) => patchType switch
    {
        "InsertText" => DocumentOperationType.UpdateBlock,
        "InsertInline" => DocumentOperationType.UpdateBlock,
        "DeleteRange" => DocumentOperationType.UpdateBlock,
        "SetMarks" => DocumentOperationType.UpdateBlock,
        "SetParagraphProperties" => DocumentOperationType.UpdateBlock,
        "InsertBlock" => DocumentOperationType.InsertBlock,
        "SplitBlock" => DocumentOperationType.InsertBlock,
        "InsertSoftBreak" => DocumentOperationType.UpdateBlock,
        "UpdateBlock" => DocumentOperationType.UpdateBlock,
        "RemoveBlock" => DocumentOperationType.DeleteBlock,
        "Paste" => DocumentOperationType.UpdateBlock,
        _ => DocumentOperationType.UpdateBlock
    };

    private static DocumentOperationTarget MapSelection(WysiwygPatch patch)
    {
        var sel = patch.Selection;
        if (sel is null)
        {
            return new DocumentOperationTarget();
        }

        return new DocumentOperationTarget
        {
            BlockId = sel.AnchorBlockId,
            Offset = sel.AnchorOffset
        };
    }
}
