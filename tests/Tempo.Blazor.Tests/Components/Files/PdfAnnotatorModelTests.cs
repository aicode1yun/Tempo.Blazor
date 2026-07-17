using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Files;

/// <summary>
/// Model tests for the TmPdfAnnotator additive extensions: annotation kinds,
/// ink strokes, per-author/role colors, factory copy semantics, provider
/// round-trips, and the export payload builder.
/// </summary>
public class PdfAnnotatorModelTests
{
    private static DocumentCommentUser Author(string id = "u1", string? role = null) =>
        new() { UserId = id, DisplayName = id.ToUpperInvariant(), Role = role };

    // ── Annotation kind + additive thread fields ─────────────────────────────

    [Fact]
    public void Thread_Defaults_KeepExistingSemantics()
    {
        var thread = new DocumentCommentThread();

        thread.Kind.Should().Be(DocumentAnnotationKind.Comment);
        thread.Color.Should().BeNull();
        thread.StampText.Should().BeNull();
        thread.InkStrokes.Should().BeEmpty();
    }

    [Fact]
    public void InkStroke_Points_AreClampedToPageBounds()
    {
        var stroke = DocumentInkStroke.Create(
        [
            (0.5, 0.5),
            (1.7, -0.3),
            (double.NaN, 0.2)
        ]);

        stroke.Points.Should().HaveCount(3);
        stroke.Points[0].X.Should().Be(0.5);
        stroke.Points[1].X.Should().Be(1);
        stroke.Points[1].Y.Should().Be(0);
        stroke.Points[2].X.Should().Be(0);
    }

    [Fact]
    public void Factory_CreateThread_CopiesAnnotatorFields()
    {
        var request = new DocumentCommentThreadCreateRequest
        {
            Anchor = DocumentCommentAnchor.Point(1, 0.4, 0.6),
            Body = "stamped",
            Kind = DocumentAnnotationKind.Stamp,
            Color = "#b45309",
            StampText = "APPROVED",
            InkStrokes = [DocumentInkStroke.Create([(0.1, 0.1), (0.2, 0.2)])]
        };

        var thread = DocumentCommentFactory.CreateThread(request, Author());

        thread.Kind.Should().Be(DocumentAnnotationKind.Stamp);
        thread.Color.Should().Be("#b45309");
        thread.StampText.Should().Be("APPROVED");
        thread.InkStrokes.Should().HaveCount(1);
    }

    [Fact]
    public async Task InMemoryProvider_RoundTripsAnnotatorFields()
    {
        var provider = new InMemoryPdfAnnotationProvider();
        var request = new DocumentCommentThreadCreateRequest
        {
            Anchor = DocumentCommentAnchor.Point(2, 0.2, 0.3),
            Body = "sketch",
            Kind = DocumentAnnotationKind.Drawing,
            Color = "#0e7490",
            InkStrokes = [DocumentInkStroke.Create([(0.1, 0.2), (0.15, 0.25), (0.2, 0.2)])]
        };

        await provider.CreateThreadAsync("doc", request, Author());
        var threads = await provider.GetThreadsAsync("doc");

        threads.Should().HaveCount(1);
        threads[0].Kind.Should().Be(DocumentAnnotationKind.Drawing);
        threads[0].Color.Should().Be("#0e7490");
        threads[0].InkStrokes.Should().HaveCount(1);
        threads[0].InkStrokes[0].Points.Should().HaveCount(3);
    }

    // ── Color resolution: explicit → author → role → palette ────────────────

    [Fact]
    public void ResolveColor_ExplicitThreadColor_Wins()
    {
        var thread = new DocumentCommentThread
        {
            Color = "#123456",
            Comments = [new DocumentComment { AuthorId = "u1" }]
        };

        var color = PdfAnnotationColorHelper.ResolveColor(
            thread,
            authorColors: new Dictionary<string, string> { ["u1"] = "#aaaaaa" },
            roleColors: null,
            users: null);

        color.Should().Be("#123456");
    }

    [Fact]
    public void ResolveColor_AuthorColor_BeatsRoleColor()
    {
        var thread = new DocumentCommentThread { Comments = [new DocumentComment { AuthorId = "u1" }] };

        var color = PdfAnnotationColorHelper.ResolveColor(
            thread,
            authorColors: new Dictionary<string, string> { ["u1"] = "#aaaaaa" },
            roleColors: new Dictionary<string, string> { ["lawyer"] = "#bbbbbb" },
            users: [Author("u1", role: "lawyer")]);

        color.Should().Be("#aaaaaa");
    }

    [Fact]
    public void ResolveColor_RoleColor_UsedWhenNoAuthorColor()
    {
        var thread = new DocumentCommentThread { Comments = [new DocumentComment { AuthorId = "u1" }] };

        var color = PdfAnnotationColorHelper.ResolveColor(
            thread,
            authorColors: null,
            roleColors: new Dictionary<string, string> { ["lawyer"] = "#bbbbbb" },
            users: [Author("u1", role: "lawyer")]);

        color.Should().Be("#bbbbbb");
    }

    [Fact]
    public void ResolveColor_FallsBackToDeterministicPaletteColor()
    {
        var thread = new DocumentCommentThread { Comments = [new DocumentComment { AuthorId = "someone" }] };

        var first = PdfAnnotationColorHelper.ResolveColor(thread, null, null, null);
        var second = PdfAnnotationColorHelper.ResolveColor(thread, null, null, null);

        first.Should().NotBeNullOrEmpty();
        first.Should().Be(second);
        PdfAnnotationColorHelper.DefaultPalette.Should().Contain(first);
    }

    [Fact]
    public void ResolveColor_DifferentAuthors_CanGetDifferentPaletteColors()
    {
        var colors = new HashSet<string>();
        for (var i = 0; i < 8; i++)
        {
            var thread = new DocumentCommentThread { Comments = [new DocumentComment { AuthorId = $"user-{i}" }] };
            colors.Add(PdfAnnotationColorHelper.ResolveColor(thread, null, null, null));
        }

        colors.Count.Should().BeGreaterThan(1);
    }

    // ── Shared comment bridge round-trip ─────────────────────────────────────

    [Fact]
    public void Bridge_RoundTripsAnnotatorFields()
    {
        var thread = new DocumentCommentThread
        {
            Id = "t1",
            Kind = DocumentAnnotationKind.Drawing,
            Color = "#0e7490",
            StampText = null,
            InkStrokes =
            [
                DocumentInkStroke.Create([(0.1, 0.2), (0.3, 0.4)], thickness: 0.006),
                DocumentInkStroke.Create([(0.5, 0.5), (0.6, 0.6), (0.7, 0.5)])
            ],
            Anchor = DocumentCommentAnchor.Point(2, 0.1, 0.2),
            Comments = [new DocumentComment { Id = "c1", AuthorId = "u1", AuthorName = "U1", Body = "sketch" }]
        };

        var roundTripped = DocumentViewerCommentBridge.ToDocumentCommentThread(
            DocumentViewerCommentBridge.ToTmCommentThread(thread, "doc"));

        roundTripped.Kind.Should().Be(DocumentAnnotationKind.Drawing);
        roundTripped.Color.Should().Be("#0e7490");
        roundTripped.InkStrokes.Should().HaveCount(2);
        roundTripped.InkStrokes[0].Thickness.Should().BeApproximately(0.006, 1e-9);
        roundTripped.InkStrokes[0].Points.Should().HaveCount(2);
        roundTripped.InkStrokes[0].Points[1].Y.Should().BeApproximately(0.4, 1e-6);
        roundTripped.InkStrokes[1].Points.Should().HaveCount(3);
    }

    [Fact]
    public void Bridge_RoundTripsStampFields()
    {
        var thread = new DocumentCommentThread
        {
            Id = "s1",
            Kind = DocumentAnnotationKind.Stamp,
            StampText = "APPROVED",
            Color = "#b45309",
            Anchor = DocumentCommentAnchor.Area(1, 0.6, 0.1, 0.2, 0.06),
            Comments = [new DocumentComment { Id = "c1", AuthorId = "u1", AuthorName = "U1", Body = "APPROVED" }]
        };

        var roundTripped = DocumentViewerCommentBridge.ToDocumentCommentThread(
            DocumentViewerCommentBridge.ToTmCommentThread(thread, "doc"));

        roundTripped.Kind.Should().Be(DocumentAnnotationKind.Stamp);
        roundTripped.StampText.Should().Be("APPROVED");
        roundTripped.Color.Should().Be("#b45309");
        roundTripped.Anchor.Kind.Should().Be(DocumentCommentAnchorKind.Area);
    }

    [Fact]
    public void Bridge_HighlightKind_SurvivesTextRangeRoundTrip()
    {
        var thread = new DocumentCommentThread
        {
            Id = "h1",
            Kind = DocumentAnnotationKind.Highlight,
            Anchor = DocumentCommentAnchor.TextRange(1,
                [DocumentCommentRect.Create(0.1, 0.2, 0.3, 0.02)], "quoted"),
            Comments = [new DocumentComment { Id = "c1", AuthorId = "u1", AuthorName = "U1", Body = "note" }]
        };

        var roundTripped = DocumentViewerCommentBridge.ToDocumentCommentThread(
            DocumentViewerCommentBridge.ToTmCommentThread(thread, "doc"));

        roundTripped.Kind.Should().Be(DocumentAnnotationKind.Highlight);
        roundTripped.Anchor.Kind.Should().Be(DocumentCommentAnchorKind.TextRange);
        roundTripped.Anchor.HighlightedText.Should().Be("quoted");
    }

    // ── Export payload ────────────────────────────────────────────────────────

    [Fact]
    public void ExportPayload_ContainsKindColorRectsAndComments()
    {
        var thread = new DocumentCommentThread
        {
            Id = "t1",
            Kind = DocumentAnnotationKind.Highlight,
            Color = "#f59e0b",
            Anchor = DocumentCommentAnchor.TextRange(3,
                [DocumentCommentRect.Create(0.1, 0.2, 0.3, 0.02)],
                "quoted text"),
            Comments = [new DocumentComment { Id = "c1", AuthorName = "Alice", Body = "note" }]
        };

        var payload = PdfAnnotationExportPayloadBuilder.Build([thread], includeResolved: true);

        payload.Should().Contain("\"kind\":\"highlight\"");
        payload.Should().Contain("\"page\":3");
        payload.Should().Contain("\"color\":\"#f59e0b\"");
        payload.Should().Contain("quoted text");
        payload.Should().Contain("Alice");
    }

    [Fact]
    public void ExportPayload_SkipsResolvedThreadsWhenExcluded()
    {
        var open = new DocumentCommentThread
        {
            Id = "open",
            Anchor = DocumentCommentAnchor.Point(1, 0.1, 0.1),
            Comments = [new DocumentComment { Body = "open note" }]
        };
        var resolved = new DocumentCommentThread
        {
            Id = "resolved",
            Status = DocumentCommentThreadStatus.Resolved,
            Anchor = DocumentCommentAnchor.Point(1, 0.5, 0.5),
            Comments = [new DocumentComment { Body = "resolved note" }]
        };

        var payload = PdfAnnotationExportPayloadBuilder.Build([open, resolved], includeResolved: false);

        payload.Should().Contain("open note");
        payload.Should().NotContain("resolved note");
    }

    [Fact]
    public void ExportPayload_SerializesInkStrokesAndStamps()
    {
        var drawing = new DocumentCommentThread
        {
            Id = "d1",
            Kind = DocumentAnnotationKind.Drawing,
            Anchor = DocumentCommentAnchor.Point(2, 0.1, 0.1),
            InkStrokes = [DocumentInkStroke.Create([(0.1, 0.2), (0.3, 0.4)])],
            Comments = [new DocumentComment { Body = string.Empty }]
        };
        var stamp = new DocumentCommentThread
        {
            Id = "s1",
            Kind = DocumentAnnotationKind.Stamp,
            StampText = "APPROVED",
            Anchor = DocumentCommentAnchor.Area(2, 0.6, 0.1, 0.2, 0.06),
            Comments = [new DocumentComment { Body = "APPROVED" }]
        };

        var payload = PdfAnnotationExportPayloadBuilder.Build([drawing, stamp], includeResolved: true);

        payload.Should().Contain("\"kind\":\"drawing\"");
        payload.Should().Contain("\"strokes\":");
        payload.Should().Contain("\"kind\":\"stamp\"");
        payload.Should().Contain("\"stampText\":\"APPROVED\"");
    }

    [Fact]
    public void ExportPayload_UsesInvariantCultureForNumbers()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("cs-CZ");
            var thread = new DocumentCommentThread
            {
                Id = "t1",
                Anchor = DocumentCommentAnchor.Point(1, 0.25, 0.75),
                Comments = [new DocumentComment { Body = "x" }]
            };

            var payload = PdfAnnotationExportPayloadBuilder.Build([thread], includeResolved: true);

            payload.Should().Contain("0.25");
            payload.Should().NotContain("0,25");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
