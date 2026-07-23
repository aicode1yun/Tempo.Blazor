using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionAtomicAuthoringEngineTests
{
    [Fact]
    public async Task ExecuteAsync_OperationFailsAfterEarlierMutation_DoesNotSaveAnything()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page);
        var block = Block(page.Page.Id, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "draft");
        var compiler = new StaticCompiler(
        [
            new NotionUpsertBlockOperation(0, "created", block),
            new FailingOperation(1, "broken")
        ]);
        var engine = Engine(provider, compiler);

        var result = await engine.ExecuteAsync(Request(
            "mid-batch-failure",
            """[{"op":"create"},{"op":"fail"}]""",
            page.Page.Id,
            page.ConcurrencyToken));

        result.Success.Should().BeFalse();
        result.Applied.Should().Be(0);
        result.Errors.Should().ContainSingle(issue =>
            issue.Path == "$.operations[1]" && issue.Code == "test_failure");
        provider.SaveCount.Should().Be(0);
        provider.GetStored(page.Page.Id).Blocks.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MovesBlockTreeAcrossPages_WithOneAtomicSave()
    {
        var source = Page("11111111-1111-1111-1111-111111111111", "source-token");
        var target = Page("22222222-2222-2222-2222-222222222222", "target-token");
        var parent = Block(source.Page.Id, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "parent", order: 4);
        var child = Block(
            source.Page.Id,
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "child",
            parent.Id,
            order: 7);
        source.Blocks = [parent, child];
        var compiler = new StaticCompiler(
        [
            new NotionMoveBlockOperation(
                0,
                "move-tree",
                parent.Id,
                source.Page.Id,
                target.Page.Id,
                targetParentBlockId: null,
                targetOrder: 0)
        ]);
        var provider = new RecordingAggregateProvider(source, target);
        var engine = Engine(provider, compiler);
        var request = new NotionAtomicAuthoringRequest
        {
            IdempotencyKey = "move-between-pages",
            OperationsJson = """[{"op":"moveBlock","clientRef":"move-tree"}]""",
            Targets =
            [
                new NotionAggregateTarget(NotionAggregateTargetKind.Page, source.Page.Id),
                new NotionAggregateTarget(NotionAggregateTargetKind.Page, target.Page.Id)
            ],
            ExpectedPageVersions =
            [
                new NotionExpectedPageVersion(source.Page.Id, source.ConcurrencyToken),
                new NotionExpectedPageVersion(target.Page.Id, target.ConcurrencyToken)
            ]
        };

        var result = await engine.ExecuteAsync(request);

        result.Success.Should().BeTrue();
        result.Atomic.Should().BeTrue();
        result.Applied.Should().Be(1);
        provider.SaveCount.Should().Be(1);
        provider.LastSaveRequest!.Pages.Should().HaveCount(2);
        provider.LastSaveRequest.Pages
            .Single(item => item.Snapshot.Page.Id == source.Page.Id)
            .BaseConcurrencyToken.Should().Be("source-token");
        provider.LastSaveRequest.Pages
            .Single(item => item.Snapshot.Page.Id == target.Page.Id)
            .BaseConcurrencyToken.Should().Be("target-token");

        provider.GetStored(source.Page.Id).Blocks.Should().BeEmpty();
        var savedTarget = provider.GetStored(target.Page.Id);
        savedTarget.Blocks.Select(block => block.Id).Should().Equal(parent.Id, child.Id);
        savedTarget.Blocks.Should().OnlyContain(block => block.PageId == target.Page.Id);
        savedTarget.Blocks.Single(block => block.Id == parent.Id).ParentBlockId.Should().BeNull();
        savedTarget.Blocks.Single(block => block.Id == parent.Id).Order.Should().Be(0);
        savedTarget.Blocks.Single(block => block.Id == child.Id).ParentBlockId.Should().Be(parent.Id);
        savedTarget.Blocks.Single(block => block.Id == child.Id).Order.Should().Be(0);
        result.Updated.Should().ContainSingle(change =>
            change.Id == parent.Id && change.ClientRef == "move-tree");
        result.Pages.Select(page => page.PageId)
            .Should().BeInAscendingOrder();
        result.Pages.Should().OnlyContain(page =>
            !string.IsNullOrWhiteSpace(page.ConcurrencyToken) &&
            page.Digest.StartsWith("sha256:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ExpectedTokenIsStale_RejectsBeforeCompileAndSave()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "current-token");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([]);
        var engine = Engine(provider, compiler);

        var result = await engine.ExecuteAsync(Request(
            "stale-token",
            "[]",
            page.Page.Id,
            "stale-token"));

        result.Success.Should().BeFalse();
        result.Conflict.Should().BeTrue();
        result.Conflicts.Should().ContainSingle(conflict =>
            conflict.PageId == page.Page.Id &&
            conflict.ExpectedConcurrencyToken == "stale-token" &&
            conflict.CurrentConcurrencyToken == "current-token");
        compiler.CallCount.Should().Be(0);
        provider.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ResponseLossRetry_ReplaysOriginalResultWithoutSecondSave()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var block = Block(page.Page.Id, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "created");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([new NotionUpsertBlockOperation(0, "new-block", block)]);
        var engine = Engine(provider, compiler);
        var request = Request(
            "response-loss",
            """[{"clientRef":"new-block","op":"create"}]""",
            page.Page.Id,
            page.ConcurrencyToken);

        _ = await engine.ExecuteAsync(request);
        var retry = await engine.ExecuteAsync(request);

        retry.Success.Should().BeTrue();
        retry.Replayed.Should().BeTrue();
        retry.Created.Should().ContainSingle(change =>
            change.Id == block.Id && change.ClientRef == "new-block");
        provider.SaveCount.Should().Be(1);
        compiler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_SameKeyWithDifferentCanonicalHash_ReturnsCollision()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([]);
        var engine = Engine(provider, compiler);

        var first = await engine.ExecuteAsync(Request(
            "reused-key",
            """[{"op":"noop","value":1}]""",
            page.Page.Id,
            page.ConcurrencyToken));
        var collision = await engine.ExecuteAsync(Request(
            "reused-key",
            """[{"op":"noop","value":2}]""",
            page.Page.Id,
            page.ConcurrencyToken));

        first.Success.Should().BeTrue();
        collision.Success.Should().BeFalse();
        collision.Errors.Should().ContainSingle(issue =>
            issue.Code == "idempotency_key_reused" &&
            issue.Path == "$.idempotencyKey");
        compiler.CallCount.Should().Be(1);
        provider.SaveCount.Should().Be(0, "no-op compilations do not require persistence");
    }

    [Fact]
    public async Task ExecuteAsync_SemanticallyIdenticalJson_ReplaysDespiteWhitespaceAndPropertyOrder()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([]);
        var engine = Engine(provider, compiler);

        var first = await engine.ExecuteAsync(Request(
            "canonical-json",
            """[{"op":"noop","value":1}]""",
            page.Page.Id,
            page.ConcurrencyToken));
        var retry = await engine.ExecuteAsync(Request(
            "canonical-json",
            """
            [
              { "value": 1.0, "op": "noop" }
            ]
            """,
            page.Page.Id,
            page.ConcurrencyToken));

        first.RequestHash.Should().Be(retry.RequestHash);
        retry.Replayed.Should().BeTrue();
        compiler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedOperations_ReturnsPathAwareErrorWithoutLoading()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([]);
        var engine = Engine(provider, compiler);

        var result = await engine.ExecuteAsync(Request(
            "malformed-json",
            """[{"op":]""",
            page.Page.Id,
            page.ConcurrencyToken));

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(issue =>
            issue.Code == "operations_invalid" &&
            issue.Path == "$.operations");
        compiler.CallCount.Should().Be(0);
        provider.LoadCount.Should().Be(0);
        provider.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedFailure_ReleasesReceiptForRetry()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new FailOnceCompiler();
        var engine = Engine(provider, compiler);
        var request = Request(
            "unexpected-failure",
            """[{"op":"noop"}]""",
            page.Page.Id,
            page.ConcurrencyToken);

        var first = () => engine.ExecuteAsync(request);
        await first.Should().ThrowAsync<ApplicationException>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var retry = await engine.ExecuteAsync(request, timeout.Token);

        retry.Success.Should().BeTrue();
        retry.Replayed.Should().BeFalse();
        compiler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredReceipt_AllowsKeyReuse()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([]);
        var clock = new ManualTimeProvider();
        var receipts = new InMemoryNotionIdempotencyReceiptStore(clock);
        var engine = new NotionAtomicAuthoringEngine(
            provider,
            compiler,
            receipts,
            receiptRetention: TimeSpan.FromMinutes(5));

        var first = await engine.ExecuteAsync(Request(
            "retained-key",
            """[{"op":"noop","value":1}]""",
            page.Page.Id,
            page.ConcurrencyToken));
        clock.Advance(TimeSpan.FromMinutes(6));
        var afterExpiry = await engine.ExecuteAsync(Request(
            "retained-key",
            """[{"op":"noop","value":2}]""",
            page.Page.Id,
            page.ConcurrencyToken));

        first.Success.Should().BeTrue();
        afterExpiry.Success.Should().BeTrue();
        afterExpiry.Replayed.Should().BeFalse();
        compiler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderLoadWarning_IsReturnedOnSuccess()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page)
        {
            LoadIssues =
            [
                new NotionAggregateIssue
                {
                    Code = "snapshot_upgraded",
                    Severity = NotionIssueSeverity.Warning,
                    Message = "The provider upgraded an older snapshot.",
                    Path = "$.pages[0].schemaVersion"
                }
            ]
        };
        var engine = Engine(provider, new StaticCompiler([]));

        var result = await engine.ExecuteAsync(Request(
            "load-warning",
            "[]",
            page.Page.Id,
            page.ConcurrencyToken));

        result.Success.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue =>
            issue.Code == "snapshot_upgraded" &&
            issue.Path == "$.pages[0].schemaVersion");
    }

    [Fact]
    public async Task ExecuteAsync_ProviderResolvesWrongPage_RejectsBeforeCompileAndSave()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new RecordingAggregateProvider(page)
        {
            LoadedSnapshotTransform = snapshot =>
            {
                snapshot.Page.Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
                return snapshot;
            }
        };
        var compiler = new StaticCompiler([]);
        var engine = Engine(provider, compiler);

        var result = await engine.ExecuteAsync(Request(
            "wrong-page-resolution",
            "[]",
            page.Page.Id,
            page.ConcurrencyToken));

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(issue =>
            issue.Code == "page_resolution_mismatch" &&
            issue.Path == "$.targets[0]");
        compiler.CallCount.Should().Be(0);
        provider.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentSameKey_AllowsExactlyOneSave()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var block = Block(page.Page.Id, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "created");
        var provider = new RecordingAggregateProvider(page)
        {
            PauseBeforeCommit = true
        };
        var compiler = new StaticCompiler([new NotionUpsertBlockOperation(0, "new-block", block)]);
        var engine = Engine(provider, compiler);
        var request = Request(
            "concurrent-retry",
            """[{"op":"create","clientRef":"new-block"}]""",
            page.Page.Id,
            page.ConcurrencyToken);

        var first = engine.ExecuteAsync(request);
        await provider.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrent = engine.ExecuteAsync(request);
        provider.AllowCommit.TrySetResult();
        var results = await Task.WhenAll(first, concurrent);

        results.Should().OnlyContain(result => result.Success);
        results.Count(result => result.Replayed).Should().Be(1);
        provider.SaveCount.Should().Be(1);
        compiler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeCompile_HasNoSideEffectsAndDoesNotPoisonKey()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var block = Block(page.Page.Id, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "created");
        var provider = new RecordingAggregateProvider(page);
        var compiler = new StaticCompiler([new NotionUpsertBlockOperation(0, "new-block", block)]);
        var receipts = new InMemoryNotionIdempotencyReceiptStore();
        var engine = new NotionAtomicAuthoringEngine(provider, compiler, receipts);
        var request = Request(
            "cancel-and-retry",
            """[{"op":"create","clientRef":"new-block"}]""",
            page.Page.Id,
            page.ConcurrencyToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => engine.ExecuteAsync(request, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        provider.SaveCount.Should().Be(0);

        var retry = await engine.ExecuteAsync(request);
        retry.Success.Should().BeTrue();
        retry.Replayed.Should().BeFalse();
        provider.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderConflict_IsSortedAndPathAware()
    {
        var pageA = Page("aaaaaaaa-0000-0000-0000-000000000001", "token-a");
        var pageB = Page("bbbbbbbb-0000-0000-0000-000000000002", "token-b");
        var provider = new RecordingAggregateProvider(pageA, pageB)
        {
            ConflictOnSave = true
        };
        var compiler = new StaticCompiler(
        [
            new NotionUpsertBlockOperation(
                0,
                "a",
                Block(pageA.Page.Id, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "a")),
            new NotionUpsertBlockOperation(
                1,
                "b",
                Block(pageB.Page.Id, "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "b"))
        ]);
        var engine = Engine(provider, compiler);
        var request = new NotionAtomicAuthoringRequest
        {
            IdempotencyKey = "provider-conflict",
            OperationsJson = """[{"op":"create"},{"op":"create"}]""",
            Targets =
            [
                new NotionAggregateTarget(NotionAggregateTargetKind.Page, pageA.Page.Id),
                new NotionAggregateTarget(NotionAggregateTargetKind.Page, pageB.Page.Id)
            ],
            ExpectedPageVersions =
            [
                new NotionExpectedPageVersion(pageA.Page.Id, pageA.ConcurrencyToken),
                new NotionExpectedPageVersion(pageB.Page.Id, pageB.ConcurrencyToken)
            ]
        };

        var result = await engine.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.Conflict.Should().BeTrue();
        result.Conflicts.Select(conflict => conflict.PageId).Should().BeInAscendingOrder();
        result.Errors.Should().ContainSingle(issue =>
            issue.Code == "concurrency_conflict" &&
            issue.Path == "$.pages");
        provider.SaveCount.Should().Be(1);
        provider.GetStored(pageA.Page.Id).Blocks.Should().BeEmpty();
        provider.GetStored(pageB.Page.Id).Blocks.Should().BeEmpty();
    }

    private static NotionAtomicAuthoringEngine Engine(
        RecordingAggregateProvider provider,
        INotionAtomicOperationCompiler compiler)
        => new(provider, compiler, new InMemoryNotionIdempotencyReceiptStore());

    private static NotionAtomicAuthoringRequest Request(
        string key,
        string operationsJson,
        Guid pageId,
        string expectedToken)
        => new()
        {
            IdempotencyKey = key,
            OperationsJson = operationsJson,
            Targets = [new NotionAggregateTarget(NotionAggregateTargetKind.Page, pageId)],
            ExpectedPageVersions = [new NotionExpectedPageVersion(pageId, expectedToken)]
        };

    private static NotionPageSnapshot Page(string id, string token)
    {
        var pageId = Guid.Parse(id);
        return new NotionPageSnapshot
        {
            Page = new NotionPageState { Id = pageId, Title = id },
            ConcurrencyToken = token,
            Digest = $"sha256:{pageId:N}"
        };
    }

    private static NotionBlockSnapshot Block(
        Guid pageId,
        string id,
        string html,
        Guid? parentBlockId = null,
        int order = 0)
        => new()
        {
            Id = Guid.Parse(id),
            PageId = pageId,
            ParentBlockId = parentBlockId,
            Type = BlockType.Paragraph,
            Order = order,
            Content = JsonSerializer.SerializeToElement(
                new TextBlockContent { Html = html },
                NotionAggregateJson.Options)
        };

    private sealed class StaticCompiler(IReadOnlyList<NotionCanonicalOperation> operations)
        : INotionAtomicOperationCompiler
    {
        public int CallCount { get; private set; }

        public ValueTask<NotionOperationCompilationResult> CompileAsync(
            JsonArray source,
            NotionAggregateWorkingSet workingSet,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(NotionOperationCompilationResult.Compiled(operations));
        }
    }

    private sealed class FailingOperation(int operationIndex, string? clientRef)
        : NotionCanonicalOperation(operationIndex, clientRef)
    {
        internal override NotionCanonicalApplyResult Apply(NotionAggregateWorkingSet workingSet)
            => NotionCanonicalApplyResult.Failed(new NotionAggregateIssue
            {
                Code = "test_failure",
                Severity = NotionIssueSeverity.Error,
                Message = "Synthetic failure after an earlier in-memory mutation.",
                Path = $"$.operations[{OperationIndex}]"
            });
    }

    private sealed class FailOnceCompiler : INotionAtomicOperationCompiler
    {
        public int CallCount { get; private set; }

        public ValueTask<NotionOperationCompilationResult> CompileAsync(
            JsonArray source,
            NotionAggregateWorkingSet workingSet,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (CallCount == 1)
            {
                throw new ApplicationException("Synthetic unexpected failure.");
            }

            return ValueTask.FromResult(NotionOperationCompilationResult.Compiled([]));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class RecordingAggregateProvider(params NotionPageSnapshot[] pages)
        : INotionAggregateProvider
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, NotionPageSnapshot> _stored =
            pages.ToDictionary(page => page.Page.Id, Clone);

        public int SaveCount { get; private set; }
        public int LoadCount { get; private set; }
        public NotionAggregateSaveRequest? LastSaveRequest { get; private set; }
        public bool ConflictOnSave { get; init; }
        public bool PauseBeforeCommit { get; init; }
        public IReadOnlyList<NotionAggregateIssue> LoadIssues { get; init; } = [];
        public Func<NotionPageSnapshot, NotionPageSnapshot>? LoadedSnapshotTransform { get; init; }
        public TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowCommit { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<NotionAggregateLoadResult> LoadPageAsync(
            Guid pageId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                LoadCount++;
                return Task.FromResult(_stored.TryGetValue(pageId, out var page)
                    ? new NotionAggregateLoadResult
                    {
                        Found = true,
                        Snapshot = Transform(Clone(page)),
                        Issues = LoadIssues
                    }
                    : new NotionAggregateLoadResult { Found = false });
            }
        }

        public Task<NotionAggregateLoadResult> LoadBlockAsync(
            Guid blockId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                LoadCount++;
                var page = _stored.Values.SingleOrDefault(
                    candidate => candidate.Blocks.Any(block => block.Id == blockId));
                return Task.FromResult(page is null
                    ? new NotionAggregateLoadResult { Found = false }
                    : new NotionAggregateLoadResult
                    {
                        Found = true,
                        Snapshot = Transform(Clone(page)),
                        MatchedBlockId = blockId,
                        Issues = LoadIssues
                    });
            }
        }

        public async Task<NotionAggregateSaveResult> SaveAsync(
            NotionAggregateSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                SaveCount++;
                LastSaveRequest = Clone(request);
            }

            SaveStarted.TrySetResult();
            if (PauseBeforeCommit)
            {
                await AllowCommit.Task.WaitAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (ConflictOnSave)
                {
                    return new NotionAggregateSaveResult
                    {
                        Conflict = true,
                        Conflicts =
                        [
                            new NotionPageConflict
                            {
                                PageId = request.Pages[^1].Snapshot.Page.Id,
                                ExpectedConcurrencyToken = request.Pages[^1].BaseConcurrencyToken,
                                CurrentConcurrencyToken = "current-last"
                            },
                            new NotionPageConflict
                            {
                                PageId = request.Pages[0].Snapshot.Page.Id,
                                ExpectedConcurrencyToken = request.Pages[0].BaseConcurrencyToken,
                                CurrentConcurrencyToken = "current-first"
                            }
                        ]
                    };
                }

                foreach (var item in request.Pages)
                {
                    if (!_stored.TryGetValue(item.Snapshot.Page.Id, out var current) ||
                        !string.Equals(
                            current.ConcurrencyToken,
                            item.BaseConcurrencyToken,
                            StringComparison.Ordinal))
                    {
                        return new NotionAggregateSaveResult
                        {
                            Conflict = true,
                            Conflicts =
                            [
                                new NotionPageConflict
                                {
                                    PageId = item.Snapshot.Page.Id,
                                    ExpectedConcurrencyToken = item.BaseConcurrencyToken,
                                    CurrentConcurrencyToken = current?.ConcurrencyToken,
                                    CurrentDigest = current?.Digest
                                }
                            ]
                        };
                    }
                }

                var savedPages = new List<NotionSavedPage>();
                foreach (var item in request.Pages)
                {
                    var saved = Clone(item.Snapshot);
                    saved.ConcurrencyToken = $"saved-{SaveCount}-{saved.Page.Id:N}";
                    _stored[saved.Page.Id] = saved;
                    savedPages.Add(new NotionSavedPage
                    {
                        PageId = saved.Page.Id,
                        ConcurrencyToken = saved.ConcurrencyToken,
                        Digest = saved.Digest,
                        SchemaVersion = saved.SchemaVersion
                    });
                }

                return new NotionAggregateSaveResult
                {
                    Success = true,
                    Pages = savedPages
                };
            }
        }

        public NotionPageSnapshot GetStored(Guid pageId)
        {
            lock (_gate)
            {
                return Clone(_stored[pageId]);
            }
        }

        private static T Clone<T>(T value)
            => JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(value, NotionAggregateJson.Options),
                NotionAggregateJson.Options)!;

        private NotionPageSnapshot Transform(NotionPageSnapshot snapshot)
            => LoadedSnapshotTransform?.Invoke(snapshot) ?? snapshot;
    }
}
