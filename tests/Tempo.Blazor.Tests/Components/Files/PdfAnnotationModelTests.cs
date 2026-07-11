using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Abstractions.Shared;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Files;

/// <summary>Model, geometry, bridge, and provider tests for PDF viewer annotations.</summary>
public class PdfAnnotationModelTests
{
    private static DocumentCommentUser Author(string id = "u1") =>
        new() { UserId = id, DisplayName = id.ToUpperInvariant() };

    private static DocumentCommentAnchor SampleTextRange() =>
        DocumentCommentAnchor.TextRange(2,
        [
            DocumentCommentRect.Create(0.10, 0.20, 0.30, 0.02),
            DocumentCommentRect.Create(0.10, 0.23, 0.25, 0.02)
        ],
        "Hello world");

    // ── Model: TextRange factory ──────────────────────────────────────────────

    [Fact]
    public void TextRange_Factory_SetsKindAndKeepsRects()
    {
        var anchor = SampleTextRange();

        anchor.Kind.Should().Be(DocumentCommentAnchorKind.TextRange);
        anchor.PageNumber.Should().Be(2);
        anchor.Rects.Should().HaveCount(2);
        anchor.HighlightedText.Should().Be("Hello world");
    }

    [Fact]
    public void TextRange_Factory_ComputesUnionBoundingBox()
    {
        var anchor = SampleTextRange();

        // Bounding box: x in [0.10, 0.40], y in [0.20, 0.25]
        anchor.X.Should().BeApproximately(0.10, 1e-9);
        anchor.Y.Should().BeApproximately(0.20, 1e-9);
        anchor.Width.Should().BeApproximately(0.30, 1e-9);
        anchor.Height.Should().BeApproximately(0.05, 1e-9);
    }

    [Fact]
    public void TextRange_Factory_DropsInvalidRects()
    {
        var anchor = DocumentCommentAnchor.TextRange(1,
        [
            DocumentCommentRect.Create(0.1, 0.1, 0.2, 0.02),
            DocumentCommentRect.Create(0.1, 0.1, 0, 0) // zero size → invalid
        ]);

        anchor.Rects.Should().HaveCount(1);
    }

    [Fact]
    public void IsValidAnchor_TextRangeWithRects_IsValid()
    {
        DocumentCommentHelper.IsValidAnchor(SampleTextRange()).Should().BeTrue();
    }

    [Fact]
    public void IsValidAnchor_TextRangeWithoutRects_IsInvalid()
    {
        var anchor = new DocumentCommentAnchor
        {
            Kind = DocumentCommentAnchorKind.TextRange,
            PageNumber = 1
        };

        DocumentCommentHelper.IsValidAnchor(anchor).Should().BeFalse();
    }

    // ── Geometry ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToRectStyle_UsesInvariantPercentages()
    {
        var style = DocumentCommentGeometryHelper.ToRectStyle(DocumentCommentRect.Create(0.1, 0.2, 0.3, 0.05));

        style.Should().Contain("left: 10%");
        style.Should().Contain("top: 20%");
        style.Should().Contain("width: 30%");
        style.Should().Contain("height: 5%");
    }

    // ── Bridge round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void Bridge_TextRangeThread_RoundTripsRectsAndText()
    {
        var thread = new DocumentCommentThread
        {
            Id = "t1",
            Anchor = SampleTextRange(),
            Comments = [new DocumentComment { Id = "c1", AuthorId = "u1", AuthorName = "U1", Body = "hi" }]
        };

        var tm = DocumentViewerCommentBridge.ToTmCommentThread(thread, "doc-1");
        tm.Anchor!.Kind.Should().Be(TmCommentAnchorKind.PageArea);
        tm.Anchor.HighlightedText.Should().Be("Hello world");

        var back = DocumentViewerCommentBridge.ToDocumentCommentThread(tm);
        back.Anchor.Kind.Should().Be(DocumentCommentAnchorKind.TextRange);
        back.Anchor.Rects.Should().HaveCount(2);
        back.Anchor.HighlightedText.Should().Be("Hello world");
        back.Anchor.Rects[0].X.Should().BeApproximately(0.10, 1e-6);
    }

    [Fact]
    public void Bridge_EncodeDecodeRects_IsStableAcrossStringRoundTrip()
    {
        var rects = new[]
        {
            DocumentCommentRect.Create(0.123456, 0.2, 0.3, 0.04),
            DocumentCommentRect.Create(0.5, 0.6, 0.1, 0.02)
        };

        var encoded = DocumentViewerCommentBridge.EncodeRects(rects);
        // Simulate JSON provider that stores metadata as boxed string.
        var decoded = DocumentViewerCommentBridge.DecodeRects((object)encoded);

        decoded.Should().HaveCount(2);
        decoded[0].X.Should().BeApproximately(0.123456, 1e-6);
        decoded[1].Y.Should().BeApproximately(0.6, 1e-6);
    }

    // ── In-memory provider ────────────────────────────────────────────────────

    [Fact]
    public async Task InMemoryProvider_CreateThenGet_Persists()
    {
        var provider = new InMemoryPdfAnnotationProvider();
        var created = await provider.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "first" },
            Author());

        var threads = await provider.GetThreadsAsync("doc-1");

        threads.Should().ContainSingle();
        threads[0].Id.Should().Be(created.Id);
        threads[0].Comments.Should().ContainSingle();
        threads[0].Comments[0].Body.Should().Be("first");
        threads[0].Anchor.Kind.Should().Be(DocumentCommentAnchorKind.TextRange);
    }

    [Fact]
    public async Task InMemoryProvider_Reply_AppendsComment()
    {
        var provider = new InMemoryPdfAnnotationProvider();
        var created = await provider.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "first" }, Author());

        await provider.ReplyAsync("doc-1",
            new DocumentCommentReplyRequest { ThreadId = created.Id, Body = "second" }, Author("u2"));

        var threads = await provider.GetThreadsAsync("doc-1");
        threads[0].Comments.Should().HaveCount(2);
        threads[0].Comments[1].Body.Should().Be("second");
    }

    [Fact]
    public async Task InMemoryProvider_ResolveAndReopen_TogglesStatus()
    {
        var provider = new InMemoryPdfAnnotationProvider();
        var created = await provider.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "first" }, Author());

        var resolved = await provider.ResolveAsync("doc-1", created.Id, Author("mod"));
        resolved.Status.Should().Be(DocumentCommentThreadStatus.Resolved);
        resolved.ResolvedByUserId.Should().Be("mod");

        var reopened = await provider.ReopenAsync("doc-1", created.Id);
        reopened.Status.Should().Be(DocumentCommentThreadStatus.Open);
        reopened.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryProvider_DeleteLastComment_RemovesThread()
    {
        var provider = new InMemoryPdfAnnotationProvider();
        var created = await provider.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "first" }, Author());

        await provider.DeleteAsync("doc-1",
            new DocumentCommentDeleteRequest { ThreadId = created.Id, CommentId = created.Comments[0].Id });

        (await provider.GetThreadsAsync("doc-1")).Should().BeEmpty();
    }

    // ── Adapter over a shared comment provider ────────────────────────────────

    [Fact]
    public async Task Adapter_CreateThenGet_RoundTripsTextRangeThroughSharedProvider()
    {
        var adapter = new TmCommentProviderPdfAnnotationAdapter(new FakeTmCommentProvider());

        var created = await adapter.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "hi" }, Author());

        var threads = await adapter.GetThreadsAsync("doc-1");

        threads.Should().ContainSingle();
        threads[0].Id.Should().Be(created.Id);
        threads[0].Anchor.Kind.Should().Be(DocumentCommentAnchorKind.TextRange);
        threads[0].Anchor.Rects.Should().HaveCount(2);
        threads[0].Anchor.HighlightedText.Should().Be("Hello world");
    }

    [Fact]
    public async Task Adapter_Reply_AppendsCommentViaSharedProvider()
    {
        var adapter = new TmCommentProviderPdfAnnotationAdapter(new FakeTmCommentProvider());
        var created = await adapter.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "hi" }, Author());

        var updated = await adapter.ReplyAsync("doc-1",
            new DocumentCommentReplyRequest { ThreadId = created.Id, Body = "reply" }, Author("u2"));

        updated.Comments.Should().HaveCount(2);
        updated.Comments[1].Body.Should().Be("reply");
    }

    [Fact]
    public async Task Adapter_Edit_PreservesAuthorAndCreatedAt()
    {
        var adapter = new TmCommentProviderPdfAnnotationAdapter(new FakeTmCommentProvider());
        var created = await adapter.CreateThreadAsync("doc-1",
            new DocumentCommentThreadCreateRequest { Anchor = SampleTextRange(), Body = "orig" }, Author("author1"));
        var original = created.Comments[0];

        var updated = await adapter.EditAsync("doc-1",
            new DocumentCommentEditRequest { ThreadId = created.Id, CommentId = original.Id, Body = "edited" });

        var edited = updated.Comments.First(c => c.Id == original.Id);
        edited.Body.Should().Be("edited");
        edited.AuthorId.Should().Be("author1");
        edited.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromSeconds(2));
        edited.EditedAt.Should().NotBeNull();
    }

    /// <summary>Minimal in-memory <see cref="ITmCommentProvider"/> for exercising the adapter.</summary>
    private sealed class FakeTmCommentProvider : ITmCommentProvider
    {
        private readonly List<TmCommentThread> _threads = [];

        public TmCommentProviderCapabilities Capabilities =>
            TmCommentProviderCapabilities.Read | TmCommentProviderCapabilities.CreateThread |
            TmCommentProviderCapabilities.Reply | TmCommentProviderCapabilities.Resolve;

        public Task<IReadOnlyList<TmCommentThread>> GetForEntityAsync(TmEntityRef entityRef, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TmCommentThread>>(_threads.Where(t => t.EntityRef.Equals(entityRef)).ToList());

        public Task<TmCommentThread> CreateThreadAsync(TmCommentThread thread, CancellationToken cancellationToken = default)
        {
            _threads.Add(thread);
            return Task.FromResult(thread);
        }

        public Task<TmCommentEntry> ReplyAsync(string threadId, TmCommentEntry entry, CancellationToken cancellationToken = default)
        {
            _threads.First(t => t.Id == threadId).Entries.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<TmCommentEntry> UpdateEntryAsync(string threadId, string entryId, TmCommentEntry entry, CancellationToken cancellationToken = default)
        {
            var thread = _threads.First(t => t.Id == threadId);
            var index = thread.Entries.FindIndex(e => e.Id == entryId);
            entry.Id = entryId;
            thread.Entries[index] = entry;
            return Task.FromResult(entry);
        }

        public Task DeleteThreadAsync(string threadId, CancellationToken cancellationToken = default)
        {
            _threads.RemoveAll(t => t.Id == threadId);
            return Task.CompletedTask;
        }

        public Task DeleteEntryAsync(string threadId, string entryId, CancellationToken cancellationToken = default)
        {
            _threads.First(t => t.Id == threadId).Entries.RemoveAll(e => e.Id == entryId);
            return Task.CompletedTask;
        }

        public Task<TmCommentThread> ResolveAsync(string threadId, TmUserRef? resolvedBy = null, CancellationToken cancellationToken = default)
        {
            var thread = _threads.First(t => t.Id == threadId);
            thread.Status = TmCommentThreadStatus.Resolved;
            thread.ResolvedBy = resolvedBy;
            thread.ResolvedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(thread);
        }

        public Task<TmCommentThread> ReopenAsync(string threadId, TmUserRef? reopenedBy = null, CancellationToken cancellationToken = default)
        {
            var thread = _threads.First(t => t.Id == threadId);
            thread.Status = TmCommentThreadStatus.Open;
            thread.ResolvedBy = null;
            thread.ResolvedAt = null;
            return Task.FromResult(thread);
        }
    }
}
