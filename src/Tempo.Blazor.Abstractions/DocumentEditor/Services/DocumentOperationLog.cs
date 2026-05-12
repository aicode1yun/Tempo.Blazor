using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Append-only idempotent operation log for document collaboration prototypes.</summary>
public class DocumentOperationLog
{
    private readonly List<DocumentOperationBatch> _batches = [];
    private readonly HashSet<string> _operationIds = [];

    /// <summary>Appended operation batches.</summary>
    public IReadOnlyList<DocumentOperationBatch> Batches => _batches;

    /// <summary>Appends a batch after validation and skips operations already present in the log.</summary>
    public DocumentOperationValidationResult Append(DocumentOperationBatch batch)
    {
        var validation = Validate(batch);
        if (!validation.IsValid)
        {
            return validation;
        }

        var uniqueOperations = batch.Operations
            .Where(operation => !_operationIds.Contains(operation.Id))
            .Select(Clone)
            .ToList();

        if (uniqueOperations.Count == 0)
        {
            return DocumentOperationValidationResult.Valid();
        }

        foreach (var operation in uniqueOperations)
        {
            _operationIds.Add(operation.Id);
        }

        _batches.Add(new DocumentOperationBatch
        {
            Id = batch.Id,
            DocumentId = batch.DocumentId,
            BaseVersionId = batch.BaseVersionId,
            Operations = uniqueOperations
        });

        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Replays the log into a document.</summary>
    public DocumentOperationValidationResult Replay(DocumentEditorDocument document, DocumentOperationApplier? applier = null)
    {
        applier ??= new DocumentOperationApplier();
        foreach (var batch in _batches)
        {
            var result = applier.Apply(document, batch);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Validates a batch.</summary>
    public static DocumentOperationValidationResult Validate(DocumentOperationBatch batch)
    {
        if (string.IsNullOrWhiteSpace(batch.DocumentId))
        {
            return DocumentOperationValidationResult.Invalid("Batch document id is required.");
        }

        foreach (var operation in batch.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Id))
            {
                return DocumentOperationValidationResult.Invalid("Operation id is required.");
            }

            if (operation.SchemaVersion != DocumentEditorDocument.CurrentSchemaVersion)
            {
                return DocumentOperationValidationResult.Invalid($"Unsupported operation schema version {operation.SchemaVersion}.");
            }
        }

        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperation Clone(DocumentOperation operation)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(operation, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<DocumentOperation>(json, DocumentEditorJson.Options)!;
    }
}
