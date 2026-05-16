using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorWysiwygJavaScriptTests
{
    [Fact]
    public async Task JavaScriptTestHooks_CoverSelectionMappingAndRemoteCommandOrdering()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable())
        {
            return;
        }

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            assert.ok(hooks, 'test hooks are exposed');
            assert.deepStrictEqual(JSON.parse(JSON.stringify(hooks.operationRendererKeys())), [
                'acceptRevision',
                'addInlineMark',
                'createRevision',
                'deleteBlock',
                'deleteText',
                'insertBlock',
                'insertText',
                'moveBlock',
                'rejectRevision',
                'removeInlineMark',
                'setBlockAttribute',
                'updateBlock'
            ]);

            const sorted = hooks.sortRemoteBatchOperations([
                { OperationId: 'op-b', Type: 0, Target: { BlockId: 'b', InlineIndex: 0, Offset: 1 }, Metadata: { LogicalTimestamp: 2 } },
                { OperationId: 'op-a', Type: 0, Target: { BlockId: 'b', InlineIndex: 0, Offset: 1 }, Metadata: { LogicalTimestamp: 1 } },
                { OperationId: 'op-c', Type: 0, Target: { BlockId: 'a', InlineIndex: 0, Offset: 0 } }
            ]).map(operation => operation.OperationId);
            assert.deepStrictEqual(sorted, ['op-a', 'op-b', 'op-c']);

            const transformedBatch = hooks.transformRemoteBatchInsertOffsets([
                { OperationId: 'op-a', Type: 0, Target: { BlockId: 'b', InlineId: 'i1', InlineIndex: 0, Offset: 2 }, Text: 'A' },
                { OperationId: 'op-b', Type: 0, Target: { BlockId: 'b', InlineId: 'i1', InlineIndex: 0, Offset: 2 }, Text: 'BB' }
            ]);
            assert.strictEqual(transformedBatch[0].Target.Offset, 2);
            assert.strictEqual(transformedBatch[1].Target.Offset, 3);

            const insertedSelection = hooks.transformSelectionForTextChange(
                {
                    anchorBlockId: 'b',
                    anchorInlineId: 'i1',
                    anchorOffset: 5,
                    focusBlockId: 'b',
                    focusInlineId: 'i1',
                    focusOffset: 5,
                    isCollapsed: true
                },
                { BlockId: 'b', InlineId: 'i1' },
                3,
                2,
                false);
            assert.strictEqual(insertedSelection.anchorOffset, 7);
            assert.strictEqual(insertedSelection.focusOffset, 7);

            const deletedSelection = hooks.transformSelectionForTextChange(
                {
                    anchorBlockId: 'b',
                    anchorInlineId: 'i1',
                    anchorOffset: 6,
                    focusBlockId: 'other',
                    focusInlineId: 'i2',
                    focusOffset: 9,
                    isCollapsed: false
                },
                { BlockId: 'b', InlineId: 'i1' },
                4,
                3,
                true);
            assert.strictEqual(deletedSelection.anchorOffset, 4);
            assert.strictEqual(deletedSelection.focusOffset, 9);

            const collapsedRuntimeSelection = hooks.createRuntimeSelectionFromSnapshot({
                region: 'Body',
                pageIndex: 0,
                anchorBlockId: 'block-1',
                anchorInlineId: 'inline-1',
                anchorOffset: 3,
                anchorBlockOffset: 3,
                focusBlockId: 'block-1',
                focusInlineId: 'inline-1',
                focusOffset: 3,
                focusBlockOffset: 3,
                isCollapsed: true,
                direction: 'forward'
            });
            assert.strictEqual(collapsedRuntimeSelection.anchorNodeId, 'inline-1');
            assert.strictEqual(collapsedRuntimeSelection.focusNodeId, 'inline-1');
            assert.strictEqual(collapsedRuntimeSelection.isCollapsed, true);
            assert.strictEqual(collapsedRuntimeSelection.region, 'Body');

            const rangedRuntimeSelection = hooks.createRuntimeSelectionFromSnapshot({
                region: 'TableCell',
                pageIndex: 1,
                activeTableCellId: 'cell-1',
                tableCellPath: 'table-1/row-0/cell-1',
                anchorBlockId: 'block-1',
                anchorInlineId: 'inline-1',
                anchorOffset: 2,
                anchorBlockOffset: 2,
                focusBlockId: 'block-2',
                focusInlineId: 'inline-4',
                focusOffset: 5,
                focusBlockOffset: 12,
                isCollapsed: false,
                direction: 'forward'
            });
            assert.strictEqual(rangedRuntimeSelection.anchorNodeId, 'inline-1');
            assert.strictEqual(rangedRuntimeSelection.focusNodeId, 'inline-4');
            assert.strictEqual(rangedRuntimeSelection.isCollapsed, false);
            assert.strictEqual(rangedRuntimeSelection.activeTableCellId, 'cell-1');

            const restoredSnapshot = hooks.createSelectionSnapshotFromRuntimeSelection(rangedRuntimeSelection);
            assert.strictEqual(restoredSnapshot.anchorInlineId, 'inline-1');
            assert.strictEqual(restoredSnapshot.focusInlineId, 'inline-4');
            assert.strictEqual(restoredSnapshot.tableCellPath, 'table-1/row-0/cell-1');

            const imageRuntimeSelection = hooks.createRuntimeSelectionFromSnapshot({
                region: 'Image',
                anchorBlockId: 'image-1',
                focusBlockId: 'image-1',
                activeImageBlockId: 'image-1',
                isCollapsed: true
            });
            assert.strictEqual(imageRuntimeSelection.region, 'Image');
            assert.strictEqual(imageRuntimeSelection.activeImageBlockId, 'image-1');
            const restoredImageSelection = hooks.createSelectionSnapshotFromRuntimeSelection(imageRuntimeSelection);
            assert.strictEqual(restoredImageSelection.activeImageBlockId, 'image-1');

            const commandTransaction = hooks.createRuntimeCommandTransaction(
                'toggleBold',
                { value: true },
                {
                    anchorBlockId: 'block-1',
                    anchorInlineId: 'inline-1',
                    anchorOffset: 0,
                    focusBlockId: 'block-1',
                    focusInlineId: 'inline-1',
                    focusOffset: 5,
                    isCollapsed: false
                },
                {
                    anchorBlockId: 'block-1',
                    anchorInlineId: 'inline-1',
                    anchorOffset: 0,
                    focusBlockId: 'block-1',
                    focusInlineId: 'inline-1',
                    focusOffset: 5,
                    isCollapsed: false
                },
                { Bold: 0 },
                { Bold: 1 });
            assert.strictEqual(commandTransaction.command, 'toggleBold');
            assert.strictEqual(commandTransaction.operations.length, 1);
            assert.strictEqual(commandTransaction.inverseOperations.length, 1);
            assert.strictEqual(commandTransaction.operations[0].operationId, 'test-op-1');
            assert.strictEqual(commandTransaction.inverseOperations[0].inverseOf, 'test-op-1');
            assert.strictEqual(commandTransaction.inverseOperations[0].command, 'toggleBold');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);

        result.ExitCode.Should().Be(0, result.StandardError);
    }

    [Fact]
    public async Task RuntimeFacadeTestHooks_RoundTripCanonicalDocumentWithoutLosingRichContent()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable())
        {
            return;
        }

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorRuntime.__testHooks;
            assert.ok(hooks, 'runtime test hooks are exposed');

            const document = {
                SchemaVersion: 1,
                DocumentId: 'doc-rich',
                Metadata: { Title: 'Contract' },
                PageSettings: { Size: 'A4' },
                Sections: [{ Id: 'section-1', Order: 0 }],
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 0,
                        Order: 0,
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 't1', Text: 'Hel', Marks: [{ Type: 1 }] },
                                { $type: 'text', Id: 't2', Text: 'lo', Marks: [{ Type: 1 }] },
                                { $type: 'token', Id: 'tok1', Key: 'ClientName', DisplayName: 'Client name', Marks: [{ Type: 2 }] },
                                { $type: 'text', Id: 't3', Text: '!', Marks: [{ Type: 3 }] }
                            ]
                        }
                    },
                    {
                        Id: 'img1',
                        Type: 5,
                        Order: 1,
                        Content: {
                            $type: 'image',
                            AssetId: 'asset-1',
                            Url: '/files/image.png',
                            AltText: 'Diagram',
                            Size: { Width: 240, Height: 120 },
                            NaturalSize: { Width: 640, Height: 320 },
                            FloatingLayout: { Inline: false, WrapMode: 1, X: 24, Y: 36, ZIndex: 2 }
                        }
                    },
                    {
                        Id: 'table1',
                        Type: 4,
                        Order: 2,
                        Content: {
                            $type: 'table',
                            Rows: [
                                {
                                    Id: 'row1',
                                    Cells: [
                                        {
                                            Id: 'cell1',
                                            Width: 180,
                                            BackgroundColor: 'rgb(255, 242, 204)',
                                            Borders: { Top: '2px solid rgb(191, 144, 0)' },
                                            Blocks: [
                                                {
                                                    Id: 'cell-p1',
                                                    Type: 0,
                                                    Content: {
                                                        $type: 'paragraph',
                                                        Inlines: [{ $type: 'text', Id: 'cell-t1', Text: 'Cell', Marks: [] }]
                                                    }
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    }
                ],
                Comments: [{ Id: 'comment-1', AnchorId: 'anchor-1', Text: 'Check this' }],
                Notes: [{ Id: 'note-1', Text: 'Footnote' }],
                HeadersFooters: [
                    {
                        Id: 'hf-1',
                        Kind: 0,
                        Blocks: [
                            {
                                Id: 'header-p1',
                                Type: 0,
                                Content: {
                                    $type: 'paragraph',
                                    Inlines: [{ $type: 'text', Id: 'header-t1', Text: 'Header', Marks: [] }]
                                }
                            }
                        ]
                    }
                ],
                Revisions: [{ Id: 'rev-1', Status: 0, Action: 0, Range: { BlockId: 'p1' } }],
                Assets: [{ Id: 'asset-1', ContentType: 'image/png', Url: '/files/image.png' }],
                Anchors: [{ Id: 'anchor-1', BlockId: 'p1', InlineId: 't1' }]
            };

            const normalized = hooks.roundTripCanonicalDocument(document);
            assert.strictEqual(normalized.DocumentId, 'doc-rich');
            assert.strictEqual(normalized.Sections[0].Id, 'section-1');
            assert.strictEqual(normalized.Blocks[0].Content.Inlines.length, 3, 'adjacent text runs with identical marks are merged');
            assert.strictEqual(normalized.Blocks[0].Content.Inlines[0].Text, 'Hello');
            assert.strictEqual(normalized.Blocks[0].Content.Inlines[1].Key, 'ClientName');
            assert.strictEqual(normalized.Blocks[1].Content.AssetId, 'asset-1');
            assert.strictEqual(normalized.Blocks[1].Content.Size.Width, 240);
            assert.strictEqual(normalized.Blocks[1].Content.NaturalSize.Width, 640);
            assert.strictEqual(normalized.Blocks[1].Content.FloatingLayout.WrapMode, 1);
            assert.strictEqual(normalized.Blocks[2].Content.Rows[0].Cells[0].Blocks[0].Content.Inlines[0].Text, 'Cell');
            assert.strictEqual(normalized.Blocks[2].Content.Rows[0].Cells[0].Width, 180);
            assert.strictEqual(normalized.Blocks[2].Content.Rows[0].Cells[0].BackgroundColor, 'rgb(255, 242, 204)');
            assert.strictEqual(normalized.Blocks[2].Content.Rows[0].Cells[0].Borders.Top, '2px solid rgb(191, 144, 0)');
            assert.strictEqual(normalized.HeadersFooters[0].Blocks[0].Content.Inlines[0].Text, 'Header');
            assert.strictEqual(normalized.Comments[0].AnchorId, 'anchor-1');
            assert.strictEqual(normalized.Revisions[0].Id, 'rev-1');
            assert.strictEqual(normalized.Assets[0].Id, 'asset-1');
            assert.strictEqual(normalized.Anchors[0].Id, 'anchor-1');

            const emptyParagraph = hooks.roundTripCanonicalDocument({
                DocumentId: 'empty-doc',
                Blocks: [{ Content: { $type: 'paragraph', Inlines: [] } }]
            });
            assert.ok(emptyParagraph.Blocks[0].Id, 'missing block id is deterministic');
            assert.strictEqual(emptyParagraph.Blocks[0].Content.Inlines[0].Text, '');
            assert.ok(emptyParagraph.Blocks[0].Content.Inlines[0].Id, 'missing inline id is deterministic');

            const runtimeDocument = hooks.fromCanonicalDocument(document);
            const exportedDocument = hooks.toCanonicalDocument(runtimeDocument);
            const equalDiff = hooks.diffCanonicalDocuments(normalized, exportedDocument);
            assert.strictEqual(equalDiff.equal, true, 'roundtrip documents match after normalization');

            const changed = JSON.parse(JSON.stringify(exportedDocument));
            changed.DocumentId = 'other-doc';
            const mismatch = hooks.diffCanonicalDocuments(exportedDocument, changed);
            assert.strictEqual(mismatch.path, '$.DocumentId');

            const snapshot = hooks.normalizeSnapshot({ ProtocolVersion: 1, Document: document });
            assert.strictEqual(snapshot.Document.DocumentId, 'doc-rich');
            assert.strictEqual(snapshot.Document.Blocks[0].Content.Inlines[0].Text, 'Hello');

            const renderHooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            const renderPlan = renderHooks.createRenderPlan(normalized);
            assert.strictEqual(renderPlan.source, 'runtimeDocument');
            assert.strictEqual(renderPlan.documentId, 'doc-rich');
            assert.strictEqual(renderPlan.pages.length, 1);
            assert.strictEqual(renderPlan.pages[0].blockIds[0], 'p1');
            assert.strictEqual(renderPlan.blockPlans[0].attributes['data-node-id'], 'p1');
            assert.strictEqual(renderPlan.blockPlans[0].attributes['data-block-id'], 'p1');
            assert.strictEqual(renderPlan.blockPlans[0].inlines[0].attributes['data-node-id'], 't1');
            assert.strictEqual(renderPlan.blockPlans[0].inlines[0].attributes['data-inline-id'], 't1');
            assert.strictEqual(renderPlan.blockPlans[0].inlines[1].type, 'token');
            assert.strictEqual(renderPlan.blockPlans[1].type, 'image');
            assert.strictEqual(renderPlan.blockPlans[1].image.assetId, 'asset-1');
            assert.strictEqual(renderPlan.blockPlans[2].rows[0].cells[0].attributes['data-node-id'], 'cell1');
            assert.strictEqual(renderPlan.blockPlans[2].rows[0].cells[0].blocks[0].attributes['data-node-id'], 'cell-p1');
            assert.strictEqual(renderPlan.headerFooterPlans[0].attributes['data-node-id'], 'hf-1');
            assert.strictEqual(renderPlan.headerFooterPlans[0].blocks[0].attributes['data-block-id'], 'header-p1');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);

        result.ExitCode.Should().Be(0, result.StandardError);
    }

    [Fact]
    public async Task JavaScriptTestHooks_TransformCommentAnchorsForTextChanges()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable())
        {
            return;
        }

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            const base = [{
                Id: 'c1',
                Status: 0,
                Anchor: {
                    Type: 1,
                    BlockId: 'b1',
                    StartOffset: 5,
                    EndOffset: 12,
                    IsOrphaned: false
                }
            }];

            const insertedBefore = hooks.transformRuntimeCommentAnchorsForTextChange(base, 'b1', 2, 3, false);
            assert.strictEqual(insertedBefore[0].Anchor.StartOffset, 8);
            assert.strictEqual(insertedBefore[0].Anchor.EndOffset, 15);

            const deletedBefore = hooks.transformRuntimeCommentAnchorsForTextChange(base, 'b1', 1, 2, true);
            assert.strictEqual(deletedBefore[0].Anchor.StartOffset, 3);
            assert.strictEqual(deletedBefore[0].Anchor.EndOffset, 10);

            const deletedThrough = hooks.transformRuntimeCommentAnchorsForTextChange(base, 'b1', 8, 2, true);
            assert.strictEqual(deletedThrough[0].Anchor.StartOffset, 5);
            assert.strictEqual(deletedThrough[0].Anchor.EndOffset, 10);
            assert.strictEqual(deletedThrough[0].Anchor.IsOrphaned, false);

            const deletedWhole = hooks.transformRuntimeCommentAnchorsForTextChange(base, 'b1', 0, 20, true);
            assert.strictEqual(deletedWhole[0].Anchor.StartOffset, 0);
            assert.strictEqual(deletedWhole[0].Anchor.EndOffset, 0);
            assert.strictEqual(deletedWhole[0].Anchor.IsOrphaned, true);
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);

        result.ExitCode.Should().Be(0, result.StandardError);
    }

    private static bool IsNodeAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<NodeResult> RunNodeAsync(string scriptPath, string nodeScript)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "-", scriptPath },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.StandardInput.WriteAsync(nodeScript);
        process.StandardInput.Close();
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new NodeResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed record NodeResult(int ExitCode, string StandardOutput, string StandardError);
}
