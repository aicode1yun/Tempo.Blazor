using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 5 of the headless document runtime: C#↔JS operation-applier convergence. Seeded
/// pseudo-random operation batches over a seed document (body paragraphs + a table cell
/// paragraph) are applied by <see cref="DocumentOperationApplier"/>, and the resulting CONTENT
/// SIGNATURE (deep block order, text, heading levels, per-character mark ranges) is committed as
/// a fixture. The Node lane (scripts/operation-convergence.test.mjs) replays the SAME operations
/// through the JS collaboration applier (transform.mjs applyOperation) on the canvas model and
/// must produce a deeply equal signature — convergence is about content, not run identities
/// (both appliers split/merge runs differently).
/// The generated set deliberately stays within the semantics BOTH appliers share; two known
/// divergences are excluded and documented in docs/document-operation-applier-coverage.md:
/// body-level MoveBlock (C# order-value vs JS index) and InsertBlock/UpdateBlock payload shapes
/// (persistence DocumentBlock vs canvas block).
/// Regenerate with TEMPO_REGENERATE_OPERATION_CONVERGENCE_FIXTURE=1.
/// </summary>
public sealed class DocumentOperationConvergenceFixtureTests
{
    private const string FixtureRelativePath = "tests/Tempo.Blazor.Tests/DocumentEditor/TestData/operation-convergence-fixture.json";
    private static readonly int[] Seeds = [11, 23, 47, 71, 97];

    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };

    [Fact]
    public void CommittedFixture_MatchesCSharpApplierResults()
    {
        RegenerateIfRequested();

        var fixturePath = Path.Combine(RepoRoot(), FixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fixturePath).Should().BeTrue(
            "the convergence fixture must be committed — regenerate via TEMPO_REGENERATE_OPERATION_CONVERGENCE_FIXTURE=1");

        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var cases = fixture.RootElement.GetProperty("cases");
        cases.GetArrayLength().Should().Be(Seeds.Length);

        foreach (var (seed, index) in Seeds.Select((seed, index) => (seed, index)))
        {
            var (document, operations) = GenerateCase(seed);
            var applier = new DocumentOperationApplier();
            foreach (var operation in operations)
            {
                var result = applier.Apply(document, operation);
                result.IsValid.Should().BeTrue(
                    $"seed {seed}: {operation.Type} must apply ({string.Join("; ", result.Errors)})");
            }

            var signature = JsonSerializer.Serialize(ComputeSignature(document), WireOptions);
            var committed = cases[index].GetProperty("expectedSignature").GetRawText();
            JsonNode(signature).Should().Be(
                JsonNode(committed),
                $"seed {seed}: the C# applier result must match the committed convergence signature");
        }
    }

    private static string JsonNode(string json)
        => JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));

    // ── Fixture generation ─────────────────────────────────────────────────────────────────────

    private static void RegenerateIfRequested()
    {
        if (Environment.GetEnvironmentVariable("TEMPO_REGENERATE_OPERATION_CONVERGENCE_FIXTURE") != "1")
        {
            return;
        }

        var cases = new List<object>();
        foreach (var seed in Seeds)
        {
            var (document, operations) = GenerateCase(seed);
            var model = CanvasDocumentModelConverter.ToCanvasModel(CloneDocument(document));

            var applier = new DocumentOperationApplier();
            foreach (var operation in operations)
            {
                applier.Apply(document, operation).IsValid.Should().BeTrue($"seed {seed} generation must stay valid");
            }

            cases.Add(new
            {
                seed,
                model,
                operations,
                expectedSignature = ComputeSignature(document),
            });
        }

        var path = Path.Combine(RepoRoot(), FixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 1, cases }, WireOptions));
    }

    private static DocumentEditorDocument CloneDocument(DocumentEditorDocument document)
        => DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(document));

    /// <summary>Deterministic case: a seed document plus operations generated against the
    /// evolving state so offsets always stay valid. The generation phases keep the batch inside
    /// the semantics shared by both appliers: text edits on single-run paragraphs first, then
    /// block-level operations, then mark ranges (marks split runs, so they come last).</summary>
    private static (DocumentEditorDocument Document, List<DocumentOperation> Operations) GenerateCase(int seed)
    {
        var random = new Random(seed);
        var document = DocumentEditorDocument.Empty($"convergence-{seed}");
        document.Blocks = [];
        for (var index = 0; index < 4; index++)
        {
            document.Blocks.Add(new DocumentBlock
            {
                Id = $"p{index}",
                Type = DocumentBlockType.Paragraph,
                Order = index,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Id = $"p{index}-run", Text = $"Odstavec {index} se základním textem." }],
                },
            });
        }

        document.Blocks.Add(new DocumentBlock
        {
            Id = "t1",
            Type = DocumentBlockType.Table,
            Order = 4,
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
                                Id = "t1c1",
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Id = "t1c1p0",
                                        Type = DocumentBlockType.Paragraph,
                                        Order = 0,
                                        Content = new ParagraphBlockContent
                                        {
                                            Inlines = [new TextRun { Id = "t1c1p0-run", Text = "Text v buňce tabulky." }],
                                        },
                                    },
                                ],
                            },
                            // Deliberately EMPTY cell: pins the deterministic paragraph creation
                            // of `setBlockAttribute table.cell.text` on both runtimes.
                            new TableCellContent
                            {
                                Id = "t1c2",
                                Blocks = [],
                            },
                        ],
                    },
                ],
            },
        });

        var working = CloneDocument(document);
        var applier = new DocumentOperationApplier();
        var operations = new List<DocumentOperation>();

        void Emit(DocumentOperation operation)
        {
            applier.Apply(working, operation).IsValid.Should().BeTrue($"seed {seed}: generated {operation.Type} must be valid");
            operations.Add(operation);
        }

        string[] TextBlockIds() => working.Blocks
            .Where(block => block.Content is ParagraphBlockContent or HeadingBlockContent)
            .Select(block => block.Id)
            .Concat(NestedCellBlocks(working).Select(block => block.Id))
            .ToArray();

        int TextLength(string blockId)
        {
            var block = working.Blocks.FirstOrDefault(item => item.Id == blockId)
                ?? NestedCellBlocks(working).First(item => item.Id == blockId);
            return block.Content switch
            {
                ParagraphBlockContent paragraph => paragraph.Inlines.OfType<TextRun>().Sum(run => run.Text.Length),
                HeadingBlockContent heading => heading.Inlines.OfType<TextRun>().Sum(run => run.Text.Length),
                _ => 0,
            };
        }

        bool IsNested(string blockId) => NestedCellBlocks(working).Any(block => block.Id == blockId);

        // Phase 1 — text edits (single-run blocks, offsets bounded by the live text length).
        for (var index = 0; index < 5; index++)
        {
            var blockIds = TextBlockIds();
            var blockId = blockIds[random.Next(blockIds.Length)];
            var length = TextLength(blockId);
            if (random.Next(2) == 0 || length < 4)
            {
                Emit(NewOperation(DocumentOperationType.InsertText, op =>
                {
                    op.Target.BlockId = blockId;
                    op.Target.TableCellId = IsNested(blockId) ? "t1c1" : null;
                    op.Target.Offset = random.Next(length + 1);
                    op.Text = $"+{seed}{index}+";
                }));
            }
            else
            {
                var offset = random.Next(length - 2);
                Emit(NewOperation(DocumentOperationType.DeleteText, op =>
                {
                    op.Target.BlockId = blockId;
                    op.Target.TableCellId = IsNested(blockId) ? "t1c1" : null;
                    op.Target.Offset = offset;
                    op.Target.Length = Math.Min(2, length - offset);
                }));
            }
        }

        // Phase 2 — block-level operations shared by both appliers (incl. the semantics
        // converged in this plan: persistence-shaped insert/update payloads and body-level
        // order-value moves).
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.Order = 1.5 + random.Next(2);
            op.Block = new DocumentBlock
            {
                Id = $"inserted-{seed}",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Id = $"inserted-{seed}-run", Text = $"Vložený odstavec {seed}." }],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.TableCellId = "t1c1";
            op.Target.Order = 1;
            op.Block = new DocumentBlock
            {
                Id = $"inserted-cell-{seed}",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Id = $"inserted-cell-{seed}-run", Text = "Vloženo do buňky." }],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.UpdateBlock, op =>
        {
            op.Target.BlockId = "p0";
            op.Block = new DocumentBlock
            {
                Id = "p0",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Id = "p0-updated-run", Text = $"Aktualizovaný obsah {seed}." }],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.MoveBlock, op =>
        {
            op.Target.BlockId = "p2";
            op.Target.Order = 0.5;
        }));
        Emit(NewOperation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "p1";
            op.AttributeName = "headingLevel";
            op.AttributeValueJson = (1 + random.Next(3)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }));
        Emit(NewOperation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "p2";
            op.AttributeName = "text";
            op.AttributeValueJson = JsonSerializer.Serialize($"Nahrazený text {seed}.");
        }));
        Emit(NewOperation(DocumentOperationType.DeleteBlock, op => op.Target.BlockId = "p3"));

        // Phase 2b — patterns emitted by the semantic MCP block tools (plan 3, Fáze 2):
        // list/quote persistence payloads and index-based moves inside a table cell.
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.Order = 2.25;
            op.Block = new DocumentBlock
            {
                Id = $"inserted-list-{seed}",
                Type = DocumentBlockType.List,
                Content = new ListBlockContent
                {
                    Ordered = seed % 2 == 0,
                    Inlines = [new TextRun { Id = $"inserted-list-{seed}-run", Text = $"Položka seznamu {seed}." }],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.TableCellId = "t1c1";
            op.Target.Order = 0;
            op.Block = new DocumentBlock
            {
                Id = $"inserted-cell-quote-{seed}",
                Type = DocumentBlockType.Quote,
                Content = new QuoteBlockContent
                {
                    Inlines = [new TextRun { Id = $"inserted-cell-quote-{seed}-run", Text = "Citace v buňce." }],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.MoveBlock, op =>
        {
            op.Target.BlockId = $"inserted-cell-{seed}";
            op.Target.TableCellId = "t1c1";
            op.Target.Order = 0;
        }));

        // Phase 2c — template authoring payloads (plan 3, Fáze 5): conditional chains and
        // repeating sections as content-control persistence payloads (insertBlock + updateBlock).
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.Order = 3.25;
            op.Block = new DocumentBlock
            {
                Id = $"cc-if-{seed}",
                Type = DocumentBlockType.ContentControl,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("if", $"contract.amount > {seed}", $"chain-{seed}"),
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = $"cc-if-{seed}-p",
                            Type = DocumentBlockType.Paragraph,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = $"cc-if-{seed}-run", Text = $"Podmíněný obsah {seed}." }],
                            },
                        },
                    ],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.Order = 3.5;
            op.Block = new DocumentBlock
            {
                Id = $"cc-else-{seed}",
                Type = DocumentBlockType.ContentControl,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("else", null, $"chain-{seed}"),
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = $"cc-else-{seed}-p",
                            Type = DocumentBlockType.Paragraph,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = $"cc-else-{seed}-run", Text = $"Jinak {seed}." }],
                            },
                        },
                    ],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.InsertBlock, op =>
        {
            op.Target.Order = 3.75;
            op.Block = new DocumentBlock
            {
                Id = $"cc-repeat-{seed}",
                Type = DocumentBlockType.ContentControl,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateRepeatingSection("items"),
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = $"cc-repeat-{seed}-p",
                            Type = DocumentBlockType.Paragraph,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = $"cc-repeat-{seed}-run", Text = "Položka." }],
                            },
                        },
                    ],
                },
            };
        }));
        Emit(NewOperation(DocumentOperationType.UpdateBlock, op =>
        {
            op.Target.BlockId = $"cc-if-{seed}";
            op.Block = new DocumentBlock
            {
                Id = $"cc-if-{seed}",
                Type = DocumentBlockType.ContentControl,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("if", $"contract.amount >= {seed * 2}", $"chain-{seed}"),
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = $"cc-if-{seed}-p",
                            Type = DocumentBlockType.Paragraph,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = $"cc-if-{seed}-run2", Text = $"Aktualizovaná podmínka {seed}." }],
                            },
                        },
                    ],
                },
            };
        }));

        // Phase 2d — table.cell.text on both paths: replace the existing cell paragraph and the
        // deterministic paragraph creation in the EMPTY cell (plan 3 follow-up).
        Emit(NewOperation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "t1";
            op.Target.TableCellId = "t1c1";
            op.AttributeName = "table.cell.text";
            op.AttributeValueJson = JsonSerializer.Serialize($"Nová hodnota buňky {seed}.");
        }));
        Emit(NewOperation(DocumentOperationType.SetBlockAttribute, op =>
        {
            op.Target.BlockId = "t1";
            op.Target.TableCellId = "t1c2";
            op.AttributeName = "table.cell.text";
            op.AttributeValueJson = JsonSerializer.Serialize($"Vytvořeno v prázdné buňce {seed}.");
        }));

        // Phase 2e — fine edits INSIDE a content control (plan 3 follow-up: content-control
        // children are operation-addressable on both runtimes).
        Emit(NewOperation(DocumentOperationType.InsertText, op =>
        {
            op.Target.BlockId = $"cc-if-{seed}-p";
            op.Target.Offset = 0;
            op.Text = $"[{seed}] ";
        }));
        Emit(NewOperation(DocumentOperationType.DeleteText, op =>
        {
            op.Target.BlockId = $"cc-else-{seed}-p";
            op.Target.Offset = 0;
            op.Target.Length = 5;
        }));
        Emit(NewOperation(DocumentOperationType.AddInlineMark, op =>
        {
            op.Target.BlockId = $"cc-if-{seed}-p";
            op.Target.Offset = 0;
            op.Target.Length = 4;
            op.Mark = new InlineMark { Type = InlineMarkType.Italic };
        }));

        // Phase 3 — mark ranges last (they split runs; both appliers converge on content).
        for (var index = 0; index < 2; index++)
        {
            var blockIds = TextBlockIds();
            var blockId = blockIds[random.Next(blockIds.Length)];
            var length = TextLength(blockId);
            if (length < 4)
            {
                continue;
            }

            var offset = random.Next(length - 3);
            Emit(NewOperation(DocumentOperationType.AddInlineMark, op =>
            {
                op.Target.BlockId = blockId;
                op.Target.TableCellId = IsNested(blockId) ? "t1c1" : null;
                op.Target.Offset = offset;
                op.Target.Length = Math.Min(3, length - offset);
                op.Mark = new InlineMark { Type = random.Next(2) == 0 ? InlineMarkType.Bold : InlineMarkType.Italic };
            }));
        }

        return (document, operations);
    }

    private static IEnumerable<DocumentBlock> NestedCellBlocks(DocumentEditorDocument document)
        => document.Blocks
            .Select(block => block.Content)
            .OfType<TableBlockContent>()
            .SelectMany(table => table.Rows)
            .SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Blocks);

    private static DocumentOperation NewOperation(DocumentOperationType type, Action<DocumentOperation> configure)
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

    // ── Content signature (mirrored in scripts/operation-convergence.test.mjs) ────────────────

    private static object ComputeSignature(DocumentEditorDocument document)
    {
        var entries = new List<object>();
        foreach (var block in document.Blocks)
        {
            entries.Add(SignatureEntry("body", block));
            if (block.Content is TableBlockContent table)
            {
                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        foreach (var nested in cell.Blocks)
                        {
                            entries.Add(SignatureEntry($"cell:{cell.Id}", nested));
                        }
                    }
                }
            }
            else if (block.Content is ContentControlBlockContent control)
            {
                foreach (var nested in control.Blocks)
                {
                    entries.Add(SignatureEntry($"cc:{block.Id}", nested));
                }
            }
        }

        return new { blocks = entries };
    }

    private static object SignatureEntry(string container, DocumentBlock block)
    {
        var inlines = block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null,
        };

        var text = string.Empty;
        var markRanges = new SortedDictionary<string, List<int[]>>(StringComparer.Ordinal);
        if (inlines is not null)
        {
            var cursor = 0;
            foreach (var run in inlines.OfType<TextRun>())
            {
                foreach (var mark in run.Marks)
                {
                    var key = JsonNamingPolicy.CamelCase.ConvertName(mark.Type.ToString());
                    if (!markRanges.TryGetValue(key, out var ranges))
                    {
                        markRanges[key] = ranges = [];
                    }

                    if (ranges.Count > 0 && ranges[^1][1] == cursor)
                    {
                        ranges[^1][1] = cursor + run.Text.Length;
                    }
                    else
                    {
                        ranges.Add([cursor, cursor + run.Text.Length]);
                    }
                }

                text += run.Text;
                cursor += run.Text.Length;
            }
        }

        Dictionary<string, string>? assembly = null;
        if (block.Content is ContentControlBlockContent controlContent)
        {
            assembly = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in new[]
                     {
                         DocumentAssemblyMetadata.BranchKey,
                         DocumentAssemblyMetadata.ExpressionKey,
                         DocumentAssemblyMetadata.GroupKey,
                         DocumentAssemblyMetadata.BindKey,
                     })
            {
                if (controlContent.Control.Metadata.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                {
                    assembly[key] = value!;
                }
            }

            if (assembly.Count == 0)
            {
                assembly = null;
            }
        }

        return new
        {
            container,
            id = block.Id,
            kind = block.Content switch
            {
                HeadingBlockContent => "heading",
                ParagraphBlockContent => "paragraph",
                ListBlockContent => "list",
                QuoteBlockContent => "quote",
                TableBlockContent => "table",
                ContentControlBlockContent => "contentControl",
                _ => "other",
            },
            headingLevel = block.Content is HeadingBlockContent heading2 ? heading2.Level : (int?)null,
            text,
            marks = markRanges.ToDictionary(pair => pair.Key, pair => pair.Value),
            assembly,
        };
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }
}
