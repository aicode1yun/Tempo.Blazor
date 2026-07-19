using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Plan 3 follow-up: operation targets must resolve INSIDE content-control blocks (template
/// sections) the same way they resolve inside table cells, so agents can fine-edit conditional
/// chains and repeating sections without whole-control updateBlock replaces. Mirrors the JS
/// collaboration applier resolution (transform.mjs findBlockLocation descends
/// content.contentControl.blocks). Also pins the deterministic paragraph id
/// `{cellId}-text` created by setBlockAttribute table.cell.text on an EMPTY cell — a random id
/// would diverge across replicas applying the same operation.
/// </summary>
public sealed class DocumentOperationApplierContentControlTests
{
    private static DocumentEditorDocument CreateDocumentWithControl()
    {
        var document = DocumentEditorDocument.Empty("cc-doc");
        var control = DocumentAssemblyMetadata.CreateConditionalBlock("if", "amount > 10", "chain-1");
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "intro",
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Id = "intro-run", Text = "Intro" }] },
            },
            new DocumentBlock
            {
                Id = "cc-1",
                Type = DocumentBlockType.ContentControl,
                Order = 1,
                Content = new ContentControlBlockContent
                {
                    Control = control,
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = "cc-child",
                            Type = DocumentBlockType.Paragraph,
                            Order = 0,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = "cc-child-run", Text = "Podmíněný text" }],
                            },
                        },
                        new DocumentBlock
                        {
                            Id = "cc-child-2",
                            Type = DocumentBlockType.Paragraph,
                            Order = 1,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = "cc-child-2-run", Text = "Druhý" }],
                            },
                        },
                        new DocumentBlock
                        {
                            Id = "cc-table",
                            Type = DocumentBlockType.Table,
                            Order = 2,
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
                                                Id = "cc-cell",
                                                Blocks =
                                                [
                                                    new DocumentBlock
                                                    {
                                                        Id = "cc-cell-paragraph",
                                                        Type = DocumentBlockType.Paragraph,
                                                        Content = new ParagraphBlockContent
                                                        {
                                                            Inlines = [new TextRun { Id = "cc-cell-run", Text = "V buňce" }],
                                                        },
                                                    },
                                                ],
                                            },
                                        ],
                                    },
                                ],
                            },
                        },
                    ],
                },
            },
        ];
        return document;
    }

    private static DocumentOperation Operation(DocumentOperationType type, Action<DocumentOperation> configure)
    {
        var operation = new DocumentOperation
        {
            OperationId = Guid.NewGuid().ToString("N"),
            Type = type,
            Target = new DocumentOperationTarget(),
        };
        configure(operation);
        return operation;
    }

    private static ContentControlBlockContent Control(DocumentEditorDocument document)
        => (ContentControlBlockContent)document.Blocks.Single(block => block.Id == "cc-1").Content;

    private static string ChildText(DocumentEditorDocument document, string childId)
        => string.Concat(((ParagraphBlockContent)Control(document).Blocks.Single(b => b.Id == childId).Content)
            .Inlines.OfType<TextRun>().Select(run => run.Text));

    [Fact]
    public void InsertText_IntoContentControlChild_Applies()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.InsertText, op =>
        {
            op.Target.BlockId = "cc-child";
            op.Target.Offset = 9;
            op.Text = " upravený";
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        ChildText(document, "cc-child").Should().Be("Podmíněný upravený text");
    }

    [Fact]
    public void DeleteText_FromContentControlChild_Applies()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.DeleteText, op =>
        {
            op.Target.BlockId = "cc-child";
            op.Target.Offset = 0;
            op.Target.Length = 10;
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        ChildText(document, "cc-child").Should().Be("text");
    }

    [Fact]
    public void AddInlineMark_RangeOnContentControlChild_SplitsRuns()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.AddInlineMark, op =>
        {
            op.Target.BlockId = "cc-child";
            op.Target.Offset = 0;
            op.Target.Length = 9;
            op.Mark = new InlineMark { Type = InlineMarkType.Bold };
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var inlines = ((ParagraphBlockContent)Control(document).Blocks.Single(b => b.Id == "cc-child").Content).Inlines;
        var bold = inlines.OfType<TextRun>().Single(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        bold.Text.Should().Be("Podmíněný");
    }

    [Fact]
    public void SetBlockAttribute_HeadingLevelOnContentControlChild_Applies()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "cc-child";
            op.AttributeName = "headingLevel";
            op.AttributeValueJson = "2";
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var child = Control(document).Blocks.Single(b => b.Id == "cc-child");
        child.Content.Should().BeOfType<HeadingBlockContent>();
        ((HeadingBlockContent)child.Content).Level.Should().Be(2);
    }

    [Fact]
    public void UpdateBlock_ContentControlChild_ReplacesInPlace()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.UpdateBlock, op =>
        {
            op.Target.BlockId = "cc-child";
            op.Block = new DocumentBlock
            {
                Id = "cc-child",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Nahrazeno" }] },
            };
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        ChildText(document, "cc-child").Should().Be("Nahrazeno");
        Control(document).Blocks[0].Id.Should().Be("cc-child", "the replace must keep the child position");
    }

    [Fact]
    public void DeleteBlock_ContentControlChild_RemovesFromControl()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.DeleteBlock, op =>
        {
            op.Target.BlockId = "cc-child-2";
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        Control(document).Blocks.Select(block => block.Id).Should().NotContain("cc-child-2");
        document.Blocks.Select(block => block.Id).Should().Contain("cc-1", "the control itself must survive");
    }

    [Fact]
    public void MoveBlock_WithinContentControl_UsesIndexSemantics()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.MoveBlock, op =>
        {
            op.Target.BlockId = "cc-child-2";
            op.Target.Order = 0;
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        Control(document).Blocks[0].Id.Should().Be("cc-child-2");
    }

    [Fact]
    public void InsertText_IntoTableCellNestedInContentControl_Applies()
    {
        var document = CreateDocumentWithControl();

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.InsertText, op =>
        {
            op.Target.BlockId = "cc-cell-paragraph";
            op.Target.TableCellId = "cc-cell";
            op.Target.Offset = 7;
            op.Text = " tabulky";
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var table = (TableBlockContent)Control(document).Blocks.Single(b => b.Id == "cc-table").Content;
        var text = string.Concat(((ParagraphBlockContent)table.Rows[0].Cells[0].Blocks[0].Content)
            .Inlines.OfType<TextRun>().Select(run => run.Text));
        text.Should().Be("V buňce tabulky");
    }

    [Fact]
    public void SetTableCellText_OnEmptyCell_CreatesParagraphWithDeterministicId()
    {
        var document = DocumentEditorDocument.Empty("cell-doc");
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "table-1",
                Type = DocumentBlockType.Table,
                Order = 0,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells = [new TableCellContent { Id = "empty-cell", Blocks = [] }],
                        },
                    ],
                },
            },
        ];

        var result = new DocumentOperationApplier().Apply(document, Operation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "table-1";
            op.Target.TableCellId = "empty-cell";
            op.AttributeName = "table.cell.text";
            op.AttributeValueJson = JsonSerializer.Serialize("Nový text", DocumentEditorJson.Options);
        }));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var table = (TableBlockContent)document.Blocks[0].Content;
        var created = table.Rows[0].Cells[0].Blocks.Should().ContainSingle().Subject;
        // Deterministic id: two replicas applying the same operation must create the SAME block.
        created.Id.Should().Be("empty-cell-text");
        ((ParagraphBlockContent)created.Content).Inlines.OfType<TextRun>().Single().Text.Should().Be("Nový text");
    }
}
