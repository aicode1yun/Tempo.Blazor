namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Result returned by the JS-owned WYSIWYG remote operation batch patcher.</summary>
public sealed class WysiwygRemoteOperationBatchApplyResult
{
    /// <summary>Whether all operations in the batch were applied or skipped as already applied.</summary>
    public bool Success { get; set; }

    /// <summary>Number of operations applied to the live DOM.</summary>
    public int Applied { get; set; }

    /// <summary>Number of operations skipped because their operation id was already applied.</summary>
    public int Skipped { get; set; }

    /// <summary>Number of operations queued because the editor is still inside a local input transaction.</summary>
    public int Queued { get; set; }

    /// <summary>Operation ids that could not be applied.</summary>
    public List<string> FailedOperationIds { get; set; } = [];

    /// <summary>Creates a successful result.</summary>
    public static WysiwygRemoteOperationBatchApplyResult Ok(int applied = 0, int skipped = 0, int queued = 0)
        => new()
        {
            Success = true,
            Applied = applied,
            Skipped = skipped,
            Queued = queued
        };

    /// <summary>Creates a failed result for a specific operation or bridge failure.</summary>
    public static WysiwygRemoteOperationBatchApplyResult Failed(params string[] operationIds)
        => new()
        {
            Success = false,
            FailedOperationIds = operationIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList()
        };
}
