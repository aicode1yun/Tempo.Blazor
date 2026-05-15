using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentComparisonServiceTests
{
    [Fact]
    public void Compare_DetectsChangedParagraphText()
    {
        var oldDocument = CreateDocument("doc-1", ("p1", "Smlouva je platná"));
        var newDocument = CreateDocument("doc-1", ("p1", "Smlouva je dnes platná"));

        var result = new DocumentComparisonService().Compare(oldDocument, newDocument);

        result.Summary.ChangedBlocks.Should().Be(1);
        result.Changes.Should().ContainSingle(change => change.Kind == DocumentCompareChangeKind.Changed);
        result.TextDiff.Segments.Should().Contain(segment =>
            segment.Kind == DocumentTextDiffSegmentKind.Added && segment.Text == "dnes");
    }

    [Fact]
    public void Compare_DetectsAddedBlock()
    {
        var oldDocument = CreateDocument("doc-1", ("p1", "Base"));
        var newDocument = CreateDocument("doc-1", ("p1", "Base"), ("p2", "Added"));

        var result = new DocumentComparisonService().Compare(oldDocument, newDocument);

        result.Summary.AddedBlocks.Should().Be(1);
        result.Changes.Should().ContainSingle(change =>
            change.Kind == DocumentCompareChangeKind.Added && change.NewText == "Added");
    }

    [Fact]
    public void Compare_DetectsRemovedBlock()
    {
        var oldDocument = CreateDocument("doc-1", ("p1", "Base"), ("p2", "Removed"));
        var newDocument = CreateDocument("doc-1", ("p1", "Base"));

        var result = new DocumentComparisonService().Compare(oldDocument, newDocument);

        result.Summary.RemovedBlocks.Should().Be(1);
        result.Changes.Should().ContainSingle(change =>
            change.Kind == DocumentCompareChangeKind.Removed && change.OldText == "Removed");
    }

    [Fact]
    public void Compare_DetectsTableTextChanges()
    {
        var oldDocument = CreateTableDocument("doc-1", "Price 1000");
        var newDocument = CreateTableDocument("doc-1", "Price 1200");

        var result = new DocumentComparisonService().Compare(oldDocument, newDocument);

        result.Summary.ChangedBlocks.Should().Be(1);
        result.Changes[0].TextDiff.Segments.Should().Contain(segment =>
            segment.Kind == DocumentTextDiffSegmentKind.Added && segment.Text == "1200");
    }

    private static DocumentEditorDocument CreateDocument(string documentId, params (string Id, string Text)[] blocks)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Blocks = blocks.Select((block, index) => new DocumentBlock
        {
            Id = block.Id,
            Type = DocumentBlockType.Paragraph,
            Order = index,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = block.Text }]
            }
        }).ToList();
        return document;
    }

    private static DocumentEditorDocument CreateTableDocument(string documentId, string cellText)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "table-1",
                Type = DocumentBlockType.Table,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent
                                {
                                    Blocks =
                                    [
                                        new DocumentBlock
                                        {
                                            Id = "cell-p1",
                                            Content = new ParagraphBlockContent
                                            {
                                                Inlines = [new TextRun { Text = cellText }]
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        ];
        return document;
    }
}
