using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class TmDocumentDiffViewerTests : LocalizationTestBase
{
    [Fact]
    public void DiffViewer_RendersInlineDiffAndSideBySideShell()
    {
        var oldDocument = CreateDocument("Smlouva je platná");
        var newDocument = CreateDocument("Smlouva je dnes platná");

        var cut = Render<TmDocumentDiffViewer>(parameters =>
            parameters.Add(p => p.OldDocument, oldDocument)
                      .Add(p => p.NewDocument, newDocument)
                      .Add(p => p.OldTitle, "Baseline")
                      .Add(p => p.NewTitle, "Current"));

        cut.Find("[data-testid='document-diff-side-by-side']").TextContent.Should().Contain("Baseline");
        cut.Find("[data-testid='document-diff-side-by-side']").TextContent.Should().Contain("Current");
        cut.Find("[data-testid='document-diff-inline']").TextContent.Should().Contain("dnes");
        cut.Find("[data-diff-kind='added']").TextContent.Should().Contain("dnes");
    }

    [Fact]
    public void VersionPanel_SelectsTwoVersionsAndShowsDiffViewer()
    {
        var baseline = CreateVersion("v1", "Baseline", "Smlouva je platná", DateTimeOffset.UtcNow.AddMinutes(-5));
        var current = CreateVersion("v2", "Current", "Smlouva je dnes platná", DateTimeOffset.UtcNow);

        var cut = Render<TmDocumentVersionPanel>(parameters =>
            parameters.Add(p => p.Versions, new[] { baseline, current })
                      .Add(p => p.OnSelectVersion, EventCallback.Factory.Create<DocumentVersion>(this, _ => { })));

        cut.FindAll("[data-testid='document-version-diff-base']")
            .First(button => button.ParentElement?.ParentElement?.TextContent.Contains("Baseline", StringComparison.OrdinalIgnoreCase) == true)
            .Click();
        cut.FindAll("[data-testid='document-version-diff-compare']")
            .First(button => button.ParentElement?.ParentElement?.TextContent.Contains("Current", StringComparison.OrdinalIgnoreCase) == true)
            .Click();

        cut.Find("[data-testid='document-diff-viewer']").Should().NotBeNull();
        cut.Find("[data-diff-kind='added']").TextContent.Should().Contain("dnes");
    }

    private static DocumentEditorDocument CreateDocument(string text)
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = text }]
                }
            }
        ];
        return document;
    }

    private static DocumentVersion CreateVersion(string id, string label, string text, DateTimeOffset createdAt)
    {
        var document = CreateDocument(text);
        var snapshot = new DocumentVersionSnapshot
        {
            DocumentId = document.DocumentId,
            SchemaVersion = document.SchemaVersion,
            Json = DocumentEditorJson.Serialize(document)
        };
        snapshot.Hash = DocumentVersionHashHelper.ComputeSnapshotHash(snapshot);

        return new DocumentVersion
        {
            Id = id,
            DocumentId = document.DocumentId,
            Label = label,
            CreatedAt = createdAt,
            Snapshot = snapshot
        };
    }
}
