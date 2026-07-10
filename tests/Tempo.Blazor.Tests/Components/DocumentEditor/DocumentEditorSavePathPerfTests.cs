using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Perf plan N8 — the save path must not re-clone a document the caller already owns (the
/// RequestDocumentAsync output is freshly deserialized), and snapshot hashing/equality must not
/// allocate document-sized strings.
/// </summary>
public class DocumentEditorSavePathPerfTests
{
    private static DocumentEditorDocument CreateDocument()
    {
        var document = DocumentEditorDocument.Empty("save-path-perf");
        // Deterministic metadata: Empty() stamps CreatedAt with UtcNow, which would make two
        // otherwise identical documents compare unequal.
        document.Metadata.CreatedAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        document.Metadata.ModifiedAt = null;
        document.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = "run-1", Text = "Save path snapshot text." }]
            }
        });
        return document;
    }

    [Fact]
    public void Snapshot_WithOwnership_ReusesTheInstanceInPlace()
    {
        var owned = CreateDocument();
        var snapshot = TmDocumentEditor.CreateProviderBoundarySnapshot(owned, preserveImageBlocks: true, assumeOwnership: true);

        snapshot.Should().BeSameAs(owned,
            "a freshly owned document (RequestDocumentAsync output) must be fixed up in place, not re-cloned");
        snapshot.HeadersFooters.Should().NotBeEmpty("EnsurePrimaryHeadersFooters must still run on the owned branch");
    }

    [Fact]
    public void Snapshot_WithoutOwnership_ClonesAndLeavesTheSourceUntouched()
    {
        var shared = CreateDocument();
        var headersBefore = shared.HeadersFooters.Count;
        var snapshot = TmDocumentEditor.CreateProviderBoundarySnapshot(shared, preserveImageBlocks: true);

        snapshot.Should().NotBeSameAs(shared, "shared documents keep the defensive clone");
        shared.HeadersFooters.Count.Should().Be(headersBefore, "the shared source must not be mutated");
        snapshot.HeadersFooters.Should().NotBeEmpty();
        snapshot.Blocks.Should().HaveCount(shared.Blocks.Count);
    }

    [Fact]
    public void Snapshot_BothBranches_ProduceEquivalentDocuments()
    {
        var forOwned = CreateDocument();
        var forShared = CreateDocument();

        var owned = TmDocumentEditor.CreateProviderBoundarySnapshot(forOwned, preserveImageBlocks: true, assumeOwnership: true);
        var cloned = TmDocumentEditor.CreateProviderBoundarySnapshot(forShared, preserveImageBlocks: true);

        // Header/footer fix-ups generate fresh ids per document, so compare the deterministic parts:
        // identical block content and the same fix-up results on both branches.
        JsonSerializer.Serialize(owned.Blocks, DocumentEditorJson.Options)
            .Should().Be(JsonSerializer.Serialize(cloned.Blocks, DocumentEditorJson.Options),
                "the ownership fast path must produce identical block content");
        owned.HeadersFooters.Should().HaveCount(cloned.HeadersFooters.Count,
            "both branches must apply EnsurePrimaryHeadersFooters the same way");
        owned.HeadersFooters.Select(h => (h.Type, h.Scope))
            .Should().BeEquivalentTo(cloned.HeadersFooters.Select(h => (h.Type, h.Scope)));
    }

    [Fact]
    public void ComputeSnapshotHash_MatchesTheStringSerializationBaseline()
    {
        var document = CreateDocument();
        var json = JsonSerializer.Serialize(document, DocumentEditorJson.Options);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        TmDocumentEditor.ComputeSnapshotHash(document).Should().Be(expected,
            "the pooled UTF-8 hash must be byte-compatible with the historical string-based hash");
    }

    [Fact]
    public void DocumentsEqual_ComparesContentWithoutStringAllocation()
    {
        var left = CreateDocument();
        // Deep clone via serialization round-trip -- mirrors the real usage (collaboration snapshot
        // clone vs live document) and avoids construction-time generated ids.
        var right = JsonSerializer.Deserialize<DocumentEditorDocument>(
            JsonSerializer.Serialize(left, DocumentEditorJson.Options), DocumentEditorJson.Options)!;

        TmDocumentEditor.DocumentsEqual(left, left).Should().BeTrue("same reference is trivially equal");
        TmDocumentEditor.DocumentsEqual(left, right).Should().BeTrue("equal content must compare equal");

        right.Blocks.Add(new DocumentBlock
        {
            Id = "p2",
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Changed." }] }
        });
        TmDocumentEditor.DocumentsEqual(left, right).Should().BeFalse("different content must compare unequal");

        TmDocumentEditor.DocumentsEqual(null, null).Should().BeTrue();
        TmDocumentEditor.DocumentsEqual(left, null).Should().BeFalse();
        TmDocumentEditor.DocumentsEqual(null, right).Should().BeFalse();
    }
}
