using System.Text.Json.Nodes;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal enum NotionAggregateTargetKind
{
    Page,
    Block
}

internal readonly record struct NotionAggregateTarget(
    NotionAggregateTargetKind Kind,
    Guid Id);

internal readonly record struct NotionExpectedPageVersion(
    Guid PageId,
    string ConcurrencyToken);

internal sealed class NotionAtomicAuthoringRequest
{
    public string IdempotencyKey { get; init; } = string.Empty;
    public string OperationsJson { get; init; } = "[]";
    public IReadOnlyList<NotionAggregateTarget> Targets { get; init; } = [];
    public IReadOnlyList<NotionExpectedPageVersion> ExpectedPageVersions { get; init; } = [];
}

internal sealed record NotionEntityChange(
    int OperationIndex,
    string? ClientRef,
    Guid PageId,
    Guid Id);

internal sealed record NotionAtomicAuthoringResult
{
    public bool Success { get; init; }
    public bool Atomic => true;
    public bool Conflict { get; init; }
    public bool Replayed { get; init; }
    public string RequestHash { get; init; } = string.Empty;
    public int Applied { get; init; }
    public IReadOnlyList<NotionEntityChange> Created { get; init; } = [];
    public IReadOnlyList<NotionEntityChange> Updated { get; init; } = [];
    public IReadOnlyList<NotionEntityChange> Deleted { get; init; } = [];
    public IReadOnlyList<NotionSavedPage> Pages { get; init; } = [];
    public IReadOnlyList<NotionPageConflict> Conflicts { get; init; } = [];
    public IReadOnlyList<NotionAggregateIssue> Warnings { get; init; } = [];
    public IReadOnlyList<NotionAggregateIssue> Errors { get; init; } = [];
}

internal interface INotionAtomicOperationCompiler
{
    ValueTask<NotionOperationCompilationResult> CompileAsync(
        JsonArray source,
        NotionAggregateWorkingSet workingSet,
        CancellationToken cancellationToken);
}

internal sealed record NotionOperationCompilationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<NotionCanonicalOperation> Operations { get; init; } = [];
    public IReadOnlyList<NotionAggregateIssue> Issues { get; init; } = [];

    public static NotionOperationCompilationResult Compiled(
        IReadOnlyList<NotionCanonicalOperation> operations,
        IReadOnlyList<NotionAggregateIssue>? warnings = null)
        => new()
        {
            Success = true,
            Operations = operations,
            Issues = warnings ?? []
        };

    public static NotionOperationCompilationResult Failed(
        params NotionAggregateIssue[] errors)
        => new()
        {
            Issues = errors
        };
}

internal abstract class NotionCanonicalOperation(int operationIndex, string? clientRef)
{
    public int OperationIndex { get; } = operationIndex;
    public string? ClientRef { get; } = clientRef;

    internal abstract NotionCanonicalApplyResult Apply(NotionAggregateWorkingSet workingSet);
}

internal sealed record NotionCanonicalApplyResult
{
    public bool Success { get; init; }
    public IReadOnlyList<NotionEntityChange> Created { get; init; } = [];
    public IReadOnlyList<NotionEntityChange> Updated { get; init; } = [];
    public IReadOnlyList<NotionEntityChange> Deleted { get; init; } = [];
    public IReadOnlyList<NotionAggregateIssue> Issues { get; init; } = [];

    public static NotionCanonicalApplyResult Applied(
        IReadOnlyList<NotionEntityChange>? created = null,
        IReadOnlyList<NotionEntityChange>? updated = null,
        IReadOnlyList<NotionEntityChange>? deleted = null,
        IReadOnlyList<NotionAggregateIssue>? warnings = null)
        => new()
        {
            Success = true,
            Created = created ?? [],
            Updated = updated ?? [],
            Deleted = deleted ?? [],
            Issues = warnings ?? []
        };

    public static NotionCanonicalApplyResult Failed(
        params NotionAggregateIssue[] errors)
        => new()
        {
            Issues = errors
        };
}

internal sealed class NotionUpsertBlockOperation(
    int operationIndex,
    string? clientRef,
    NotionBlockSnapshot block)
    : NotionCanonicalOperation(operationIndex, clientRef)
{
    internal override NotionCanonicalApplyResult Apply(NotionAggregateWorkingSet workingSet)
        => workingSet.UpsertBlock(OperationIndex, ClientRef, block);
}

internal sealed class NotionDeleteBlockOperation(
    int operationIndex,
    string? clientRef,
    Guid blockId)
    : NotionCanonicalOperation(operationIndex, clientRef)
{
    internal override NotionCanonicalApplyResult Apply(NotionAggregateWorkingSet workingSet)
        => workingSet.DeleteBlock(OperationIndex, ClientRef, blockId);
}

internal sealed class NotionMoveBlockOperation(
    int operationIndex,
    string? clientRef,
    Guid blockId,
    Guid sourcePageId,
    Guid targetPageId,
    Guid? targetParentBlockId,
    int targetOrder)
    : NotionCanonicalOperation(operationIndex, clientRef)
{
    internal override NotionCanonicalApplyResult Apply(NotionAggregateWorkingSet workingSet)
        => workingSet.MoveBlock(
            OperationIndex,
            ClientRef,
            blockId,
            sourcePageId,
            targetPageId,
            targetParentBlockId,
            targetOrder);
}

internal sealed class NotionReplacePageStateOperation(
    int operationIndex,
    string? clientRef,
    NotionPageState page)
    : NotionCanonicalOperation(operationIndex, clientRef)
{
    internal override NotionCanonicalApplyResult Apply(NotionAggregateWorkingSet workingSet)
        => workingSet.ReplacePage(OperationIndex, ClientRef, page);
}
