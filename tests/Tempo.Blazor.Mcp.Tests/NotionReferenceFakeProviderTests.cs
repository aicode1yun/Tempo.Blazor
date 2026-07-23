using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Testing;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionReferenceFakeProviderTests
{
    [Fact]
    public async Task SaveAsync_MultiPageConflict_PersistsNone_ThenAdvancesBothTokens()
    {
        var first = Page("11111111-1111-1111-1111-111111111111", "first-token");
        var second = Page("22222222-2222-2222-2222-222222222222", "second-token");
        var provider = new FakeNotionAggregateProvider([first, second]);
        var replacements = new[]
        {
            Replacement(first, "First changed", first.ConcurrencyToken),
            Replacement(second, "Second changed", "stale-token")
        };

        var conflict = await provider.SaveAsync(
            new NotionAggregateSaveRequest { Pages = replacements });

        conflict.Success.Should().BeFalse();
        conflict.Conflict.Should().BeTrue();
        conflict.Conflicts.Should().ContainSingle(item =>
            item.PageId == second.Page.Id &&
            item.ExpectedConcurrencyToken == "stale-token" &&
            item.CurrentConcurrencyToken == second.ConcurrencyToken);
        provider.GetSnapshot(first.Page.Id).Page.Title.Should().Be("First");
        provider.GetSnapshot(second.Page.Id).Page.Title.Should().Be("Second");

        replacements[1] = Replacement(
            second,
            "Second changed",
            second.ConcurrencyToken);
        var saved = await provider.SaveAsync(
            new NotionAggregateSaveRequest { Pages = replacements });

        saved.Success.Should().BeTrue();
        saved.Atomic.Should().BeTrue();
        saved.Pages.Should().HaveCount(2);
        saved.Pages.Should().OnlyContain(page =>
            page.ConcurrencyToken.StartsWith("fake:", StringComparison.Ordinal) &&
            page.Digest.StartsWith("sha256:", StringComparison.Ordinal));
        provider.GetSnapshot(first.Page.Id).Page.Title.Should().Be("First changed");
        provider.GetSnapshot(second.Page.Id).Page.Title.Should().Be("Second changed");
        provider.GetSnapshot(first.Page.Id).ConcurrencyToken
            .Should().NotBe(first.ConcurrencyToken);
        provider.GetSnapshot(second.Page.Id).ConcurrencyToken
            .Should().NotBe(second.ConcurrencyToken);
    }

    [Fact]
    public async Task AtomicEngine_ReplayedRequest_UsesReferenceFakeSaveOnce()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var block = new NotionBlockSnapshot
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            PageId = page.Page.Id,
            Type = BlockType.Paragraph,
            Content = JsonSerializer.SerializeToElement(
                new TextBlockContent { Html = "created" },
                NotionAggregateJson.Options)
        };
        var provider = new FakeNotionAggregateProvider([page]);
        var compiler = new StaticCompiler(
            [new NotionUpsertBlockOperation(0, "created", block)]);
        var engine = new NotionAtomicAuthoringEngine(
            provider,
            compiler,
            new InMemoryNotionIdempotencyReceiptStore());
        var request = new NotionAtomicAuthoringRequest
        {
            IdempotencyKey = "reference-fake-replay",
            OperationsJson = """[{"op":"createBlock","clientRef":"created"}]""",
            Targets =
            [
                new NotionAggregateTarget(
                    NotionAggregateTargetKind.Page,
                    page.Page.Id)
            ],
            ExpectedPageVersions =
            [
                new NotionExpectedPageVersion(
                    page.Page.Id,
                    page.ConcurrencyToken)
            ]
        };

        var first = await engine.ExecuteAsync(request);
        var replay = await engine.ExecuteAsync(request);

        first.Success.Should().BeTrue();
        replay.Success.Should().BeTrue();
        replay.Replayed.Should().BeTrue();
        replay.RequestHash.Should().Be(first.RequestHash);
        provider.SaveCallCount.Should().Be(1);
        compiler.CallCount.Should().Be(1);
        provider.GetSnapshot(page.Page.Id).Blocks
            .Should().ContainSingle(saved => saved.Id == block.Id);
    }

    [Fact]
    public async Task Diagnostics_ReturnDefensiveCopies_ThatCannotMutateProviderState()
    {
        var page = Page("11111111-1111-1111-1111-111111111111", "token-1");
        var provider = new FakeNotionAggregateProvider([page]);
        var replacement = Replacement(page, "Saved", page.ConcurrencyToken);

        var result = await provider.SaveAsync(
            new NotionAggregateSaveRequest { Pages = [replacement] });
        var inspectedRequest = provider.LastSaveRequest!;
        inspectedRequest.Pages[0].Snapshot.Page.Title = "tampered request";
        var inspectedSnapshot = provider.GetSnapshot(page.Page.Id);
        inspectedSnapshot.Page.Title = "tampered snapshot";

        result.Success.Should().BeTrue();
        provider.LastSaveRequest!.Pages[0].Snapshot.Page.Title.Should().Be("Saved");
        provider.GetSnapshot(page.Page.Id).Page.Title.Should().Be("Saved");
    }

    private static NotionPageSnapshot Page(string id, string token)
    {
        var pageId = Guid.Parse(id);
        return new NotionPageSnapshot
        {
            Page = new NotionPageState
            {
                Id = pageId,
                Title = pageId == Guid.Parse(
                    "11111111-1111-1111-1111-111111111111")
                    ? "First"
                    : "Second"
            },
            ConcurrencyToken = token,
            Digest = $"sha256:{pageId:N}"
        };
    }

    private static NotionPageSave Replacement(
        NotionPageSnapshot source,
        string title,
        string token)
    {
        var snapshot = Clone(source);
        snapshot.Page.Title = title;
        return new NotionPageSave
        {
            Snapshot = snapshot,
            BaseConcurrencyToken = token
        };
    }

    private static NotionPageSnapshot Clone(NotionPageSnapshot source)
        => JsonSerializer.Deserialize<NotionPageSnapshot>(
            JsonSerializer.Serialize(source, NotionAggregateJson.Options),
            NotionAggregateJson.Options)!;

    private sealed class StaticCompiler(
        IReadOnlyList<NotionCanonicalOperation> operations)
        : INotionAtomicOperationCompiler
    {
        public int CallCount { get; private set; }

        public ValueTask<NotionOperationCompilationResult> CompileAsync(
            JsonArray source,
            NotionAggregateWorkingSet workingSet,
            NotionOperationCompileContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(
                NotionOperationCompilationResult.Compiled(operations));
        }
    }
}
