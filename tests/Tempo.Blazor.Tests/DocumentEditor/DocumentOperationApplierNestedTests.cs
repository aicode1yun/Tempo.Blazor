using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 5 of the headless document runtime: the C# operation applier must resolve operation
/// targets INSIDE table cells the same way the JS collaboration applier does
/// (transform.mjs findBlockLocation/findContainer — deep search with the table cell id as the
/// container preference). Covers text, formatting, block, attribute and update operations
/// against blocks nested in table cells.
/// </summary>
public sealed class DocumentOperationApplierNestedTests
{
    private static DocumentEditorDocument CreateDocumentWithTable()
    {
        var document = DocumentEditorDocument.Empty("nested-doc");
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
                Id = "table-1",
                Type = DocumentBlockType.Table,
                Order = 1,
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
                                    Id = "cell-1",
                                    Blocks =
                                    [
                                        new DocumentBlock
                                        {
                                            Id = "cell-paragraph",
                                            Type = DocumentBlockType.Paragraph,
                                            Order = 0,
                                            Content = new ParagraphBlockContent
                                            {
                                                Inlines = [new TextRun { Id = "cell-run", Text = "Buňka" }],
                                            },
                                        },
                                    ],
                                },
                            ],
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

    private static string CellParagraphText(DocumentEditorDocument document)
    {
        var table = (TableBlockContent)document.Blocks.Single(block => block.Id == "table-1").Content;
        var cell = table.Rows[0].Cells[0];
        return string.Concat(cell.Blocks
            .SelectMany(block => ((ParagraphBlockContent)block.Content).Inlines)
            .OfType<TextRun>()
            .Select(run => run.Text));
    }

    [Fact]
    public void InsertText_IntoBlockNestedInTableCell_Applies()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.InsertText, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.TableCellId = "cell-1";
            op.Target.Offset = 5;
            op.Text = " textu";
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        CellParagraphText(document).Should().Be("Buňka textu");
    }

    [Fact]
    public void DeleteText_FromBlockNestedInTableCell_Applies()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.DeleteText, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.TableCellId = "cell-1";
            op.Target.Offset = 0;
            op.Target.Length = 3;
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        CellParagraphText(document).Should().Be("ka");
    }

    [Fact]
    public void AddInlineMark_OnBlockNestedInTableCell_Applies()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.AddInlineMark, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.TableCellId = "cell-1";
            op.Target.Offset = 0;
            op.Target.Length = 5;
            op.Mark = new InlineMark { Type = InlineMarkType.Bold };
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var table = (TableBlockContent)document.Blocks.Single(block => block.Id == "table-1").Content;
        var runs = ((ParagraphBlockContent)table.Rows[0].Cells[0].Blocks[0].Content).Inlines.OfType<TextRun>();
        runs.Should().Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
    }

    [Fact]
    public void InsertBlock_IntoTableCellContainer_AddsToTheCell()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.TableCellId = "cell-1";
            op.Target.Order = 1;
            op.Block = new DocumentBlock
            {
                Id = "cell-paragraph-2",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Druhý odstavec" }] },
            };
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var table = (TableBlockContent)document.Blocks.Single(block => block.Id == "table-1").Content;
        table.Rows[0].Cells[0].Blocks.Select(block => block.Id).Should().ContainInOrder("cell-paragraph", "cell-paragraph-2");
        document.Blocks.Should().NotContain(block => block.Id == "cell-paragraph-2", "the block belongs to the cell, not the body");
    }

    [Fact]
    public void DeleteBlock_NestedInTableCell_RemovesItFromTheCell()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.DeleteBlock, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.TableCellId = "cell-1";
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var table = (TableBlockContent)document.Blocks.Single(block => block.Id == "table-1").Content;
        table.Rows[0].Cells[0].Blocks.Should().BeEmpty();
        document.Blocks.Should().Contain(block => block.Id == "table-1", "only the nested block is deleted");
    }

    [Fact]
    public void UpdateBlock_NestedInTableCell_ReplacesThePayloadInPlace()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.UpdateBlock, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.TableCellId = "cell-1";
            op.Block = new DocumentBlock
            {
                Id = "cell-paragraph",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Nahrazený obsah" }] },
            };
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        CellParagraphText(document).Should().Be("Nahrazený obsah");
    }

    [Fact]
    public void SetBlockAttribute_HeadingLevel_OnBlockNestedInTableCell_Applies()
    {
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.TableCellId = "cell-1";
            op.AttributeName = "headingLevel";
            op.AttributeValueJson = "2";
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        var table = (TableBlockContent)document.Blocks.Single(block => block.Id == "table-1").Content;
        var nested = table.Rows[0].Cells[0].Blocks[0];
        nested.Content.Should().BeOfType<HeadingBlockContent>("headingLevel promotes the nested paragraph to a heading");
        ((HeadingBlockContent)nested.Content).Level.Should().Be(2);
    }

    [Fact]
    public void MoveBlock_WithinTableCell_ReordersTheCellBlocks()
    {
        var document = CreateDocumentWithTable();
        var table = (TableBlockContent)document.Blocks.Single(block => block.Id == "table-1").Content;
        table.Rows[0].Cells[0].Blocks.Add(new DocumentBlock
        {
            Id = "cell-paragraph-2",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Druhý" }] },
        });

        var operation = Operation(DocumentOperationType.MoveBlock, op =>
        {
            op.Target.BlockId = "cell-paragraph-2";
            op.Target.TableCellId = "cell-1";
            op.Target.Order = 0;
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        table.Rows[0].Cells[0].Blocks.Select(block => block.Id).Should().ContainInOrder("cell-paragraph-2", "cell-paragraph");
    }

    [Fact]
    public void TextOperations_WithoutTableCellId_StillResolveNestedBlocksByDeepSearch()
    {
        // Parity with JS findBlockLocation: the cell id is a preference, not a requirement.
        var document = CreateDocumentWithTable();
        var operation = Operation(DocumentOperationType.InsertText, op =>
        {
            op.Target.BlockId = "cell-paragraph";
            op.Target.Offset = 0;
            op.Text = "V ";
        });

        var result = new DocumentOperationApplier().Apply(document, operation);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        CellParagraphText(document).Should().Be("V Buňka");
    }
}
