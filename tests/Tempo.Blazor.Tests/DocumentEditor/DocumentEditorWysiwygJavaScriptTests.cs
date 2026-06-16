using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

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

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
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
                'moveDrawingObject',
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

            const anchorDoc = {
                DocumentId: 'anchor-doc',
                PageSettings: {
                    Size: { Width: 400, Height: 500 },
                    Margins: { Top: 50, Right: 50, Bottom: 50, Left: 50 }
                },
                Theme: { BodyFontSize: 11, BodyLineHeight: 1.15, ParagraphSpacingAfter: 0 },
                Blocks: [
                    { Id: 'intro', Type: 0, Order: 1, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'intro-i', Text: 'Intro text before anchor '.repeat(12) }] } },
                    { Id: 'anchor', Type: 0, Order: 2, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'anchor-i', Text: 'Anchor paragraph' }] } },
                    {
                        Id: 'img-move',
                        Type: 5,
                        Order: 3,
                        Content: {
                            $type: 'image',
                            Source: 0,
                            Url: '/favicon.png',
                            Size: { Width: 90, Height: 60 },
                            Layout: {
                                Kind: 1,
                                Anchor: { BlockId: 'anchor', Offset: 0, Region: 0, MoveWithText: true, FixedOnPage: false, LockAnchor: false },
                                Position: { HorizontalRelativeTo: 1, VerticalRelativeTo: 3, X: 0, Y: 0, HorizontalAlignment: 0 },
                                Wrap: { Mode: 1, DistanceRight: 8 },
                                Transform: { Width: 90, Height: 60 }
                            }
                        }
                    },
                    {
                        Id: 'img-fixed',
                        Type: 5,
                        Order: 4,
                        Content: {
                            $type: 'image',
                            Source: 0,
                            Url: '/favicon.png',
                            Size: { Width: 90, Height: 60 },
                            Layout: {
                                Kind: 2,
                                Anchor: { BlockId: 'anchor', Offset: 0, Region: 0, MoveWithText: false, FixedOnPage: true, LockAnchor: true },
                                Position: { HorizontalRelativeTo: 0, VerticalRelativeTo: 0, X: 10, Y: 20 },
                                Wrap: { Mode: 6 },
                                Transform: { Width: 90, Height: 60 }
                            }
                        }
                    }
                ]
            };
            const anchorLayout = hooks.createLayoutSnapshotForRender(anchorDoc);
            const layoutObjects = anchorLayout.Pages[0].Objects;
            const moveObject = layoutObjects.find(item => item.BlockId === 'img-move');
            const fixedObject = layoutObjects.find(item => item.BlockId === 'img-fixed');
            const anchorParagraph = anchorLayout.Pages[0].Paragraphs.find(item => item.BlockId === 'anchor');
            assert.strictEqual(moveObject.AnchorBlockId, 'anchor');
            assert.strictEqual(moveObject.AnchorOffset, 0);
            assert.strictEqual(moveObject.AnchorRegion, 0);
            assert.strictEqual(moveObject.Rect.Y, anchorParagraph.Rect.Y);
            assert.strictEqual(fixedObject.AnchorBlockId, 'anchor');
            assert.strictEqual(fixedObject.Rect.Y, 20);

            const overlapDoc = {
                DocumentId: 'overlap-doc',
                PageSettings: {
                    Size: { Width: 400, Height: 500 },
                    Margins: { Top: 50, Right: 50, Bottom: 50, Left: 50 }
                },
                Theme: { BodyFontSize: 11, BodyLineHeight: 1.15, ParagraphSpacingAfter: 0 },
                Blocks: [
                    {
                        Id: 'img-a',
                        Type: 5,
                        Order: 1,
                        Content: {
                            $type: 'image',
                            Source: 0,
                            Url: '/favicon.png',
                            Size: { Width: 100, Height: 70 },
                            Layout: {
                                Kind: 1,
                                Position: { HorizontalRelativeTo: 1, VerticalRelativeTo: 3, X: 0, Y: 0, HorizontalAlignment: 0 },
                                Wrap: { Mode: 1 },
                                Transform: { Width: 100, Height: 70 },
                                Stacking: { ZIndex: 1, AllowOverlap: false }
                            }
                        }
                    },
                    {
                        Id: 'img-b',
                        Type: 5,
                        Order: 2,
                        Content: {
                            $type: 'image',
                            Source: 0,
                            Url: '/favicon.png',
                            Size: { Width: 100, Height: 70 },
                            Layout: {
                                Kind: 1,
                                Position: { HorizontalRelativeTo: 1, VerticalRelativeTo: 3, X: 0, Y: 0, HorizontalAlignment: 0 },
                                Wrap: { Mode: 1 },
                                Transform: { Width: 100, Height: 70 },
                                Stacking: { ZIndex: 2, AllowOverlap: false }
                            }
                        }
                    }
                ]
            };
            const overlapLayout = hooks.createLayoutSnapshotForRender(overlapDoc);
            const overlapA = overlapLayout.Pages[0].Objects.find(item => item.BlockId === 'img-a');
            const overlapB = overlapLayout.Pages[0].Objects.find(item => item.BlockId === 'img-b');
            assert.strictEqual(overlapA.AllowOverlap, false);
            assert.ok(overlapB.Rect.Y >= overlapA.Rect.Y + overlapA.Rect.Height);

            const clampedContour = hooks.normalizeWrapContourPoints([
                { X: -2, Y: 0.25 },
                { X: 0.5, Y: 2 },
                { X: 1.5, Y: -1 }
            ]);
            assert.deepStrictEqual(JSON.parse(JSON.stringify(clampedContour)), [
                { X: 0, Y: 0.25 },
                { X: 0.5, Y: 1 },
                { X: 1, Y: 0 }
            ]);

            const squareIntervals = hooks.getLayoutAvailableIntervals(
                88,
                12,
                { X: 0, Y: 0, Width: 300, Height: 240 },
                [{ BlocksText: true, Rect: { X: 100, Y: 80, Width: 100, Height: 100 } }]);
            const diamondIntervals = hooks.getLayoutAvailableIntervals(
                88,
                12,
                { X: 0, Y: 0, Width: 300, Height: 240 },
                [{
                    BlocksText: true,
                    Rect: { X: 100, Y: 80, Width: 100, Height: 100 },
                    Polygon: [
                        { X: 150, Y: 80 },
                        { X: 200, Y: 130 },
                        { X: 150, Y: 180 },
                        { X: 100, Y: 130 }
                    ]
                }]);
            assert.strictEqual(squareIntervals[0].Width, 100);
            assert.ok(diamondIntervals[0].Width > squareIntervals[0].Width);
            assert.ok(diamondIntervals[1].X < squareIntervals[1].X);
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
                            Layout: {
                                Kind: 1,
                                Position: { X: 24, Y: 36 },
                                Wrap: { Mode: 1 },
                                Stacking: { ZIndex: 2 }
                            }
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
            assert.strictEqual(normalized.Blocks[1].Content.Layout.Wrap.Mode, 1);
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

            const renderHooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
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

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
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

    [Fact]
    public async Task Phase4Comments_ImportBuildsVisibleMarkerStoreAndExportsComments()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var provider = new InMemoryDocumentEditorProvider();
        var sourceDocument = provider.SeedRecoveryDocument();
        var sourcePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(sourcePath, DocumentEditorJson.Serialize(sourceDocument));

        try
        {
            var nodeScript =
                """
                const fs = require('fs');
                const vm = require('vm');
                const assert = require('assert');

                const code = fs.readFileSync(process.argv[2], 'utf8');
                const source = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
                const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
                sandbox.window.setTimeout = setTimeout;
                sandbox.window.clearTimeout = clearTimeout;
                sandbox.window.console = console;
                vm.createContext(sandbox);
                vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

                const engine = sandbox.window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson(source);
                const marker = engine.__testHooks.buildRuntimeCommentMarkers(model)
                    .find(item => item.targetId === 'recovery-comment-visible');
                assert.ok(marker, 'comment marker must be created from the inline comment anchor');
                assert.strictEqual(marker.type, 'comment');
                assert.strictEqual(marker.range.startBlockId, 'recovery-comment-paragraph');
                assert.ok(marker.range.startOffset > 0, 'inline mark range must include the paragraph prefix');
                assert.strictEqual(marker.range.endOffset - marker.range.startOffset, 'visible comment anchor'.length);

                const exported = engine.model.exportToCSharpJson(model);
                assert.ok(exported.Comments.some(comment => comment.Id === 'recovery-comment-visible'));
                console.log(JSON.stringify({ ProtocolVersion: 1, Document: exported }));
                """;

            var result = await RunNodeAsync(scriptPath, nodeScript, sourcePath);
            result.ExitCode.Should().Be(0, result.StandardError);

            var snapshot = JsonSerializer.Deserialize<WysiwygDocumentSnapshot>(
                result.StandardOutput.Trim(),
                new JsonSerializerOptions(DocumentEditorJson.Options) { PropertyNameCaseInsensitive = true });
            snapshot.Should().NotBeNull();
            snapshot!.Document.Comments.Should().Contain(comment => comment.Id == "recovery-comment-visible");
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task Phase5Revisions_ImportBuildsVisibleMarkerStoreAndExportsRevisions()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var provider = new InMemoryDocumentEditorProvider();
        var sourceDocument = provider.SeedRecoveryDocument();
        var sourcePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(sourcePath, DocumentEditorJson.Serialize(sourceDocument));

        try
        {
            var nodeScript =
                """
                const fs = require('fs');
                const vm = require('vm');
                const assert = require('assert');

                const code = fs.readFileSync(process.argv[2], 'utf8');
                const source = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
                const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
                sandbox.window.setTimeout = setTimeout;
                sandbox.window.clearTimeout = clearTimeout;
                sandbox.window.console = console;
                vm.createContext(sandbox);
                vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

                const engine = sandbox.window.tmDocumentEditorEngine;
                const hooks = engine.__testHooks;
                const model = engine.model.importFromCSharpJson(source);
                const markers = hooks.buildRuntimeRevisionMarkers(model);
                const insertion = markers.find(item => item.targetId === 'recovery-revision-insertion');
                const deletion = markers.find(item => item.targetId === 'recovery-revision-deletion');

                assert.ok(insertion, 'insertion revision marker must be created from the imported model');
                assert.strictEqual(insertion.type, 'revisionInsertion');
                assert.strictEqual(insertion.blockId, 'recovery-insertion-revision-paragraph');
                assert.strictEqual(insertion.range.endOffset - insertion.range.startOffset, 'inserted recovery clause'.length);
                assert.strictEqual(insertion.insertedText, 'inserted recovery clause');

                assert.ok(deletion, 'deletion revision marker must be created from the imported model');
                assert.strictEqual(deletion.type, 'revisionDeletion');
                assert.strictEqual(deletion.blockId, 'recovery-deletion-revision-paragraph');
                assert.strictEqual(deletion.range.endOffset - deletion.range.startOffset, 'deleted recovery clause'.length);
                assert.strictEqual(deletion.originalText, 'deleted recovery clause');

                const exported = engine.model.exportToCSharpJson(model);
                assert.ok(exported.Revisions.some(revision => revision.Id === 'recovery-revision-insertion'));
                assert.ok(exported.Revisions.some(revision => revision.Id === 'recovery-revision-deletion'));
                console.log(JSON.stringify({ ProtocolVersion: 1, Document: exported }));
                """;

            var result = await RunNodeAsync(scriptPath, nodeScript, sourcePath);
            result.ExitCode.Should().Be(0, result.StandardError);

            var snapshot = JsonSerializer.Deserialize<WysiwygDocumentSnapshot>(
                result.StandardOutput.Trim(),
                new JsonSerializerOptions(DocumentEditorJson.Options) { PropertyNameCaseInsensitive = true });
            snapshot.Should().NotBeNull();
            snapshot!.Document.Revisions.Should().Contain(revision => revision.Id == "recovery-revision-insertion");
            snapshot.Document.Revisions.Should().Contain(revision => revision.Id == "recovery-revision-deletion");
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    // ─── 8.x Table serialization ──────────────────────────────────────────────

    [Fact]
    public async Task RuntimeFacadeTestHooks_RoundTripTableWithIsHeaderPreservesHeaderCells()
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
                DocumentId: 'doc-header-table',
                Blocks: [
                    {
                        Id: 'table1',
                        Type: 4,
                        Order: 0,
                        Content: {
                            $type: 'table',
                            Rows: [
                                {
                                    Cells: [
                                        {
                                            Id: 'cell-h1',
                                            IsHeader: true,
                                            ColumnSpan: 1,
                                            RowSpan: 1,
                                            Blocks: [{ Id: 'hp1', Type: 0, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'ht1', Text: 'Header', Marks: [] }] } }]
                                        },
                                        {
                                            Id: 'cell-h2',
                                            IsHeader: true,
                                            ColumnSpan: 1,
                                            RowSpan: 1,
                                            Blocks: [{ Id: 'hp2', Type: 0, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'ht2', Text: 'Col 2', Marks: [] }] } }]
                                        }
                                    ]
                                },
                                {
                                    Cells: [
                                        {
                                            Id: 'cell-d1',
                                            IsHeader: false,
                                            ColumnSpan: 1,
                                            RowSpan: 1,
                                            Blocks: [{ Id: 'dp1', Type: 0, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'dt1', Text: 'Data', Marks: [] }] } }]
                                        },
                                        {
                                            Id: 'cell-d2',
                                            IsHeader: false,
                                            ColumnSpan: 1,
                                            RowSpan: 1,
                                            Blocks: [{ Id: 'dp2', Type: 0, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'dt2', Text: 'Val', Marks: [] }] } }]
                                        }
                                    ]
                                }
                            ]
                        }
                    }
                ]
            };

            const normalized = hooks.roundTripCanonicalDocument(document);
            const tableBlock = normalized.Blocks[0];
            assert.strictEqual(tableBlock.Content.$type, 'table', 'table block type preserved');
            const headerRow = tableBlock.Content.Rows[0];
            const dataRow = tableBlock.Content.Rows[1];
            assert.strictEqual(headerRow.Cells[0].IsHeader, true, 'first row cells IsHeader=true preserved');
            assert.strictEqual(headerRow.Cells[1].IsHeader, true, 'first row second cell IsHeader=true preserved');
            assert.strictEqual(dataRow.Cells[0].IsHeader, false, 'data row cells IsHeader=false preserved');
            assert.strictEqual(dataRow.Cells[1].IsHeader, false, 'data row second cell IsHeader=false preserved');
            assert.strictEqual(headerRow.Cells[0].Blocks[0].Content.Inlines[0].Text, 'Header', 'header cell text preserved');

            const runtimeDoc = hooks.fromCanonicalDocument(document);
            const exported = hooks.toCanonicalDocument(runtimeDoc);
            assert.strictEqual(exported.Blocks[0].Content.Rows[0].Cells[0].IsHeader, true, 'IsHeader survives fromCanonical->toCanonical');
            assert.strictEqual(exported.Blocks[0].Content.Rows[1].Cells[0].IsHeader, false, 'data row IsHeader=false survives fromCanonical->toCanonical');

            const insertTablePayloadRows = 3;
            const insertTablePayloadCols = 5;
            const tRows = { rows: insertTablePayloadRows, columns: insertTablePayloadCols };
            assert.strictEqual((tRows.rows || tRows.Rows || 2), 3, 'insertTable payload rows key works');
            assert.strictEqual((tRows.columns || tRows.Columns || tRows.cols || tRows.Cols || 2), 5, 'insertTable payload columns key works');

            const tRowsAlt = { Rows: 4, Columns: 6 };
            assert.strictEqual((tRowsAlt.rows || tRowsAlt.Rows || 2), 4, 'insertTable payload Rows (capital) key works');
            assert.strictEqual((tRowsAlt.columns || tRowsAlt.Columns || tRowsAlt.cols || tRowsAlt.Cols || 2), 6, 'insertTable payload Columns (capital) key works');
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

    private static async Task<NodeResult> RunNodeAsync(string scriptPath, string nodeScript, params string[] additionalArguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-");
        process.StartInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in additionalArguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

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

    // ─── 4.2 tmDocumentEditorToolbar overflow controller ─────────────────────

    [Fact]
    public async Task ToolbarOverflowController_ExistsAndReturnsExpectedApi()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                WeakMap,
                ResizeObserver: class ResizeObserver {
                    constructor(cb) { this._cb = cb; }
                    observe() {}
                    disconnect() { this._disconnected = true; }
                }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            const toolbar = sandbox.window.tmDocumentEditorToolbar;
            assert.ok(toolbar, 'tmDocumentEditorToolbar must be defined on window');
            assert.strictEqual(typeof toolbar.createOverflowController, 'function',
                'createOverflowController must be a function');
            assert.strictEqual(typeof toolbar.disposeOverflowController, 'function',
                'disposeOverflowController must be a function');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ToolbarOverflowController_CreateWithNullArgs_DoesNotThrow()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                WeakMap,
                ResizeObserver: class ResizeObserver {
                    constructor(cb) {}
                    observe() {}
                    disconnect() {}
                }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            const toolbar = sandbox.window.tmDocumentEditorToolbar;
            // Calling with null/undefined must not throw
            toolbar.createOverflowController(null, null);
            toolbar.createOverflowController(undefined, undefined);
            toolbar.disposeOverflowController(null);
            toolbar.disposeOverflowController(undefined);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ToolbarOverflowController_Dispose_DisconnectsResizeObserver()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            let disconnected = false;

            const sandbox = {
                window: {},
                console,
                WeakMap,
                ResizeObserver: class ResizeObserver {
                    constructor(cb) {}
                    observe() {}
                    disconnect() { disconnected = true; }
                }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            const toolbar = sandbox.window.tmDocumentEditorToolbar;
            const fakeEl = {};
            const fakeDotNet = { invokeMethodAsync() {} };

            toolbar.createOverflowController(fakeEl, fakeDotNet);
            toolbar.disposeOverflowController(fakeEl);

            assert.strictEqual(disconnected, true, 'ResizeObserver.disconnect must be called on dispose');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ─── 4.3 tmDocumentEditor beforeunload guard ─────────────────────────────

    [Fact]
    public async Task BeforeUnloadGuard_EnableRegistersHandler()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            let registered = false;
            const sandbox = {
                window: {
                    addEventListener(type, handler) { if (type === 'beforeunload') registered = true; },
                    removeEventListener() {}
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            assert.strictEqual(registered, true, 'enableBeforeUnloadGuard must register a beforeunload listener');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task BeforeUnloadGuard_EnableIsIdempotent()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            let callCount = 0;
            const sandbox = {
                window: {
                    addEventListener(type, handler) { if (type === 'beforeunload') callCount++; },
                    removeEventListener() {}
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            assert.strictEqual(callCount, 1, 'addEventListener must be called exactly once regardless of repeated enables');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task BeforeUnloadGuard_DisableRemovesHandler()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            let removed = false;
            const sandbox = {
                window: {
                    addEventListener() {},
                    removeEventListener(type, handler) { if (type === 'beforeunload') removed = true; }
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            sandbox.window.tmDocumentEditor.disableBeforeUnloadGuard();
            assert.strictEqual(removed, true, 'disableBeforeUnloadGuard must remove the beforeunload listener');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task BeforeUnloadGuard_DisableWhenNotActive_IsNoOp()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            let removeCount = 0;
            const sandbox = {
                window: {
                    addEventListener() {},
                    removeEventListener(type) { if (type === 'beforeunload') removeCount++; }
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            // disable without ever enabling — must not throw or call removeEventListener
            sandbox.window.tmDocumentEditor.disableBeforeUnloadGuard();
            assert.strictEqual(removeCount, 0, 'disableBeforeUnloadGuard on inactive guard must not call removeEventListener');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task BeforeUnloadGuard_EnableDisableEnable_RegistersAgain()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            let addCount = 0;
            let removeCount = 0;
            const sandbox = {
                window: {
                    addEventListener(type) { if (type === 'beforeunload') addCount++; },
                    removeEventListener(type) { if (type === 'beforeunload') removeCount++; }
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            sandbox.window.tmDocumentEditor.disableBeforeUnloadGuard();
            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();

            assert.strictEqual(addCount, 2, 'second enable after disable must re-register');
            assert.strictEqual(removeCount, 1, 'disable must have removed the first registration');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task BeforeUnloadGuard_DebugStateTracksActiveGuard()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {
                    addEventListener() {},
                    removeEventListener() {}
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            assert.strictEqual(sandbox.window.tmDocumentEditor.getBeforeUnloadGuardState().active, false);
            sandbox.window.tmDocumentEditor.enableBeforeUnloadGuard();
            assert.strictEqual(sandbox.window.tmDocumentEditor.getBeforeUnloadGuardState().active, true);
            sandbox.window.tmDocumentEditor.disableBeforeUnloadGuard();
            assert.strictEqual(sandbox.window.tmDocumentEditor.getBeforeUnloadGuardState().active, false);
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ─── Phase 12: Watchdog recovery ─────────────────────────────────────────

    // Helper inline comment: tests inject a mock runtime engine AFTER loading the
    // file so the runtime facade's _call/_engine() picks up the mock instead of the real engine.
    // A synchronous setTimeout stub is used so recovery callbacks fire inline.

    private static string WatchdogSandboxSetup =>
        """
        const fs = require('fs');
        const vm = require('vm');
        const assert = require('assert');

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const pendingTimers = [];
        const sandbox = {
            window: {},
            console,
            Map,
            WeakMap,
            URL,
            JSON,
            setTimeout: function (cb) { pendingTimers.push(cb); },
            clearTimeout: function () {}
        };
        sandbox.window.setTimeout = sandbox.setTimeout;
        sandbox.window.clearTimeout = sandbox.clearTimeout;
        sandbox.window.addEventListener = function () {};
        sandbox.window.removeEventListener = function () {};

        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

        function flushTimers() { while (pendingTimers.length) pendingTimers.shift()(); }

        function makeMockEngine(overrides) {
            return Object.assign({
                create: function (rootEl, opts) { return opts && (opts.InstanceId || opts.instanceId) || 'inst'; },
                dispose: function () {},
                loadDocument: function () {},
                getDocumentSnapshot: function () {
                    const snapshot = this.getSnapshot();
                    const document = typeof snapshot === 'string'
                        ? JSON.parse(snapshot)
                        : (snapshot || { SchemaVersion: 1, DocumentId: 'doc', Blocks: [] });
                    return { ok: true, csharpDocument: document };
                },
                getDocument: function () { return JSON.stringify({ SchemaVersion: 1, DocumentId: 'doc', Blocks: [] }); },
                getOfflineState: function () { return JSON.stringify({ version: 1, dirtyState: { IsDirty: false } }); },
                applyOfflineState: function () { return true; },
                applyCommand: function () { return this.executeCommand.apply(this, arguments); },
                executeCommand: function () {},
                applyRemoteOperationBatch: function () {},
                applyRemoteOperation: function () {},
                applyRemoteOperations: function () {},
                setTrackChangesEnabled: function () {},
                setReviewDisplayMode: function () {},
                setReadOnly: function () {},
                isAlive: function () { return true; },
                getDirtyState: function () { return { IsDirty: false }; },
                markSaved: function () { return true; },
                getOfflineState: function () { return null; },
                applyOfflineState: function () { return true; },
                undo: function () {},
                redo: function () {},
                focus: function () {},
                getUndoState: function () { return null; },
                getFormattingState: function () { return null; },
                getDebugSnapshot: function () { return {}; },
                getMarkers: function () { return []; },
                upsertMarker: function () { return null; },
                getLastCommandTransaction: function () { return null; },
                getDebugUndoStack: function () { return {}; },
                getSelectionSnapshot: function () { return null; },
                getRuntimeSelection: function () { return null; },
                getLinkInfo: function () { return null; },
                insertImageNode: function () {},
                scrollToRevision: function () {},
                scrollToComment: function () {},
                upsertComment: function () {},
                removeComment: function () {},
                reviewRevision: function () {},
                clearRevisionDecorations: function () {},
                restoreSelection: function () {},
                closeHeaderFooter: function () {},
                captureCommentAnchor: function () { return null; },
                applyRemoteCursor: function () {},
                setSearchMarkers: function () {},
                clearSearchMarkers: function () {},
                scrollToSearchResult: function () {},
                loadDocument: function () { return this.applySnapshot.apply(this, arguments); },
                applySnapshot: function () {},
                getSnapshot: function () { return null; }
            }, overrides || {});
        }
        """;

    [Fact]
    public async Task Watchdog_ExposesGetStateTestHook()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            assert.ok(runtime.__watchdog, '__watchdog must be exposed on the runtime');
            assert.strictEqual(typeof runtime.__watchdog.getState, 'function', 'getState must be a function');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_GetState_ReturnsReadyAfterCreate()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine();
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            const fakeEl = {};
            runtime.create(fakeEl, { InstanceId: 'inst-1' }, null);
            assert.strictEqual(runtime.__watchdog.getState('inst-1'), 'ready', 'state must be ready after create');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_GetState_ReturnsNullForUnknownInstance()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            assert.strictEqual(runtime.__watchdog.getState('no-such-instance'), null, 'unknown instance must return null');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_ErrorTransitionsToRecoveringThenRecovered()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            let executeCalled = 0;
            const mock = makeMockEngine({
                executeCommand: function () {
                    executeCalled++;
                    if (executeCalled === 1) throw new Error('Simulated engine failure');
                }
            });
            sandbox.window.tmDocumentEditorEngine = mock;
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-r' }, null);

            // First call throws — watchdog catches it and schedules recovery
            runtime.executeCommand('inst-r', 'toggleBold', {});
            assert.strictEqual(runtime.__watchdog.getState('inst-r'), 'recovering', 'state must be recovering after error');

            // Flush timers to run recovery callback
            flushTimers();
            assert.strictEqual(runtime.__watchdog.getState('inst-r'), 'recovered', 'state must be recovered after recovery');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_CapturesDocumentBeforeDispose()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            // runtime.getDocument calls engine.getSnapshot; runtime.getOfflineState calls engine.getOfflineState.
            // runtime.dispose (via _origDispose) calls engine.dispose.
            // Track through mock engine — no cross-vm-context spy needed.
            const callOrder = [];
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                executeCommand: function () { throw new Error('fail'); },
                getSnapshot: function () { callOrder.push('getSnapshot'); return null; },
                getOfflineState: function () { callOrder.push('getOfflineState'); return null; },
                dispose: function () { callOrder.push('dispose'); }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-cap' }, null);
            runtime.executeCommand('inst-cap', 'cmd', {});
            flushTimers();

            const getDocIdx = callOrder.indexOf('getSnapshot');
            const getOfflineIdx = callOrder.indexOf('getOfflineState');
            const disposeIdx = callOrder.indexOf('dispose');
            assert.ok(getDocIdx >= 0, 'getSnapshot (via getDocument) must be called');
            assert.ok(getOfflineIdx >= 0, 'getOfflineState must be called');
            assert.ok(disposeIdx >= 0, 'dispose must be called');
            assert.ok(getDocIdx < disposeIdx, 'getSnapshot must run before dispose');
            assert.ok(getOfflineIdx < disposeIdx, 'getOfflineState must run before dispose');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_LoadsDocumentAndOfflineStateAfterCreate()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            // Track recovery flow through mock engine methods.
            // runtime.loadDocument calls engine.applySnapshot; runtime.applyOfflineState calls engine.applyOfflineState.
            // runtime.getDocument calls engine.getSnapshot (must return non-null to trigger loadDocument).
            // runtime.dispose (via _origDispose) calls engine.dispose.
            // engine.create is called by _origCreate during recovery.
            const callOrder = [];
            const fakeOfflineJson = JSON.stringify({ version: 1, dirtyState: { IsDirty: true } });
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                executeCommand: function () { throw new Error('fail'); },
                getSnapshot: function () {
                    return JSON.stringify({ SchemaVersion: 1, DocumentId: 'doc', Sections: [], Blocks: [], Metadata: {}, PageSettings: { Size: 'A4' } });
                },
                getOfflineState: function () { return fakeOfflineJson; },
                dispose: function () { callOrder.push('dispose'); },
                create: function (el, opts) { callOrder.push('create'); return opts && (opts.InstanceId || opts.instanceId) || ''; },
                applySnapshot: function () { callOrder.push('applySnapshot'); },
                applyOfflineState: function () { callOrder.push('applyOfflineState'); return true; }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-ld' }, null);
            runtime.executeCommand('inst-ld', 'cmd', {});
            flushTimers();

            // The initial runtime.create also calls engine.create, so there are two 'create' entries.
            // Use lastIndexOf to find the recovery create (second one).
            const recoveryCreateIdx = callOrder.lastIndexOf('create');
            const disposeIdx = callOrder.indexOf('dispose');
            const snapshotIdx = callOrder.lastIndexOf('applySnapshot');
            const offlineIdx = callOrder.lastIndexOf('applyOfflineState');
            assert.ok(recoveryCreateIdx >= 0, 'recovery create must be called');
            assert.ok(snapshotIdx >= 0, 'applySnapshot (via loadDocument) must be called');
            assert.ok(offlineIdx >= 0, 'applyOfflineState must be called');
            assert.ok(disposeIdx < recoveryCreateIdx, 'dispose must run before recovery create');
            assert.ok(recoveryCreateIdx < snapshotIdx, 'recovery create must run before applySnapshot');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_RecoveryFailedSetsFailedState()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            let createCallCount = 0;
            const mock = makeMockEngine({
                executeCommand: function () { throw new Error('fail'); },
                create: function () {
                    createCallCount++;
                    if (createCallCount > 1) throw new Error('create also failed');
                    return 'inst-fail';
                }
            });
            sandbox.window.tmDocumentEditorEngine = mock;
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-fail' }, null);
            runtime.executeCommand('inst-fail', 'cmd', {});
            flushTimers();
            assert.strictEqual(runtime.__watchdog.getState('inst-fail'), 'failed', 'state must be failed when recovery create throws');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_NotifiesDotNetRecovered()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            const notified = [];
            const fakeDotNet = { invokeMethodAsync: function(method) { notified.push(method); } };
            const mock = makeMockEngine({
                executeCommand: function () { throw new Error('fail'); }
            });
            sandbox.window.tmDocumentEditorEngine = mock;
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-dn' }, fakeDotNet);
            runtime.executeCommand('inst-dn', 'cmd', {});
            flushTimers();
            assert.ok(notified.includes('HandleRuntimeRecovered'), 'HandleRuntimeRecovered must be invoked on dotNetRef');
            assert.ok(!notified.includes('HandleRuntimeRecoveryFailed'), 'HandleRuntimeRecoveryFailed must NOT be invoked on success');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_NotifiesDotNetRecoveryFailed()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            const notified = [];
            const fakeDotNet = { invokeMethodAsync: function(method) { notified.push(method); } };
            let createCount = 0;
            const mock = makeMockEngine({
                executeCommand: function () { throw new Error('fail'); },
                create: function () {
                    createCount++;
                    if (createCount > 1) throw new Error('recovery create failed');
                    return 'inst-df';
                }
            });
            sandbox.window.tmDocumentEditorEngine = mock;
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-df' }, fakeDotNet);
            runtime.executeCommand('inst-df', 'cmd', {});
            flushTimers();
            assert.ok(notified.includes('HandleRuntimeRecoveryFailed'), 'HandleRuntimeRecoveryFailed must be invoked on failure');
            assert.ok(!notified.includes('HandleRuntimeRecovered'), 'HandleRuntimeRecovered must NOT be invoked on failure');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ApplyRemoteOperationBatch_ErrorTriggersRecovery()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            const mock = makeMockEngine({
                applyRemoteOperationBatch: function () { throw new Error('batch fail'); }
            });
            sandbox.window.tmDocumentEditorEngine = mock;
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-batch' }, null);
            runtime.applyRemoteOperationBatch('inst-batch', { operations: [] });
            assert.strictEqual(runtime.__watchdog.getState('inst-batch'), 'recovering', 'state must be recovering after batch error');
            flushTimers();
            assert.strictEqual(runtime.__watchdog.getState('inst-batch'), 'recovered', 'state must be recovered after batch recovery');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_Dispose_ClearsWatchdogState()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine();
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-disp' }, null);
            assert.strictEqual(runtime.__watchdog.getState('inst-disp'), 'ready', 'must be ready before dispose');
            runtime.dispose('inst-disp');
            assert.strictEqual(runtime.__watchdog.getState('inst-disp'), null, 'must be null after dispose');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_ExecuteCommand_NoRecoveryIfAlreadyRecovering()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            let disposeCalls = 0;
            const mock = makeMockEngine({
                executeCommand: function () { throw new Error('fail'); },
                dispose: function () { disposeCalls++; }
            });
            sandbox.window.tmDocumentEditorEngine = mock;
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-nr' }, null);

            // Two failed calls before recovery is flushed
            runtime.executeCommand('inst-nr', 'cmd1', {});
            runtime.executeCommand('inst-nr', 'cmd2', {});
            flushTimers();

            // Only one recovery cycle must have run
            assert.strictEqual(disposeCalls, 1, 'dispose must be called exactly once for double failure');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_StableSnapshot_CapturesDocumentMarkersSelectionUndoAndUploadState()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                getSnapshot: function () {
                    return JSON.stringify({ SchemaVersion: 1, DocumentId: 'doc-stable', Sections: [], Blocks: [{ Id: 'p1' }], Metadata: {}, PageSettings: { Size: 'A4' } });
                },
                getMarkers: function () {
                    return [{ id: 'comment-1', type: 'comment', range: { startBlockId: 'p1', startOffset: 0, endBlockId: 'p1', endOffset: 4 } }];
                },
                getSelectionSnapshot: function () {
                    return { AnchorBlockId: 'p1', AnchorOffset: 2, FocusBlockId: 'p1', FocusOffset: 2, IsCollapsed: true };
                },
                getUndoState: function () {
                    return { CanUndo: true, UndoDepth: 1, Epoch: 7 };
                },
                getDebugUndoStack: function () {
                    return { UndoDepth: 1, RedoDepth: 0 };
                },
                getDebugSnapshot: function () {
                    return { PendingUploadCount: 1, PendingUploads: [{ FileName: 'photo.png' }] };
                }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-stable' }, null);
            runtime.executeCommand('inst-stable', 'bold', {});

            const stable = runtime.__watchdog.getStableSnapshot('inst-stable');
            assert.strictEqual(stable.Document.DocumentId, 'doc-stable');
            assert.strictEqual(stable.Markers[0].id, 'comment-1');
            assert.strictEqual(stable.Selection.AnchorBlockId, 'p1');
            assert.strictEqual(stable.UndoState.Epoch, 7);
            assert.strictEqual(stable.UploadState.PendingUploadCount, 1);
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_CommandError_RecordsClassificationAndRecoveredEvent()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                executeCommand: function () { throw new Error('command exploded'); }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-command' }, null);
            runtime.executeCommand('inst-command', 'bold', {});
            const scheduled = runtime.__watchdog.getLastRecoveryDetail('inst-command');
            assert.strictEqual(scheduled.Source, 'command');
            assert.strictEqual(scheduled.Event, 'runtimeRecoveryScheduled');
            flushTimers();
            const recovered = runtime.__watchdog.getLastRecoveryDetail('inst-command');
            assert.strictEqual(recovered.Event, 'runtimeRecovered');
            assert.strictEqual(recovered.Source, 'command');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_RemoteOperationError_RecordsClassification()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                applyRemoteOperationBatch: function () { throw new Error('remote exploded'); }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-remote' }, null);
            runtime.applyRemoteOperationBatch('inst-remote', { Operations: [] });
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-remote').Source, 'remoteOperation');
            flushTimers();
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-remote').Source, 'remoteOperation');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_RenderError_RecordsClassification()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                applySnapshot: function () { throw new Error('render exploded'); }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-render' }, null);
            runtime.loadDocument('inst-render', { DocumentId: 'doc' });
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-render').Source, 'render');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_SerializationCrash_RecordsClassificationViaDebugHook()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            sandbox.window.tmDocumentEditorEngine = makeMockEngine();
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-serialization' }, null);
            runtime.__watchdog.simulateCrash('inst-serialization', 'serialization', { message: 'serialize exploded' });
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-serialization').Source, 'serialization');
            flushTimers();
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-serialization').Source, 'serialization');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_RetryPolicy_UsesExponentialBackoffAndFailsAfterLimit()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            let createCount = 0;
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                create: function (el, opts) {
                    createCount++;
                    if (createCount > 1) throw new Error('create failed');
                    return opts.InstanceId || 'inst-retry';
                }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-retry', WatchdogMaxAttempts: 3, WatchdogBackoffMs: 50 }, null);
            runtime.__watchdog.simulateCrash('inst-retry', 'command');

            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-retry').Attempt, 1);
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-retry').BackoffMs, 50);
            flushTimers();

            const events = runtime.__watchdog.getEvents('inst-retry');
            const scheduled = events.filter(e => e.Event === 'runtimeRecoveryScheduled');
            assert.deepStrictEqual(scheduled.map(e => e.BackoffMs), [50, 100, 200]);
            assert.strictEqual(runtime.__watchdog.getState('inst-retry'), 'failed');
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-retry').Event, 'runtimeRecoveryFailed');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Watchdog_Recovery_UsesStableSnapshotFallbackWhenLiveSnapshotUnavailable()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript = WatchdogSandboxSetup +
            """
            let snapshotAvailable = true;
            let loadedDocumentId = '';
            sandbox.window.tmDocumentEditorEngine = makeMockEngine({
                getSnapshot: function () {
                    return snapshotAvailable
                        ? JSON.stringify({ SchemaVersion: 1, DocumentId: 'stable-doc', Sections: [], Blocks: [], Metadata: {}, PageSettings: { Size: 'A4' } })
                        : null;
                },
                executeCommand: function (instanceId, command) {
                    if (command === 'boom') throw new Error('command failed');
                },
                applySnapshot: function (instanceId, snapshot) {
                    const doc = typeof snapshot === 'string' ? JSON.parse(snapshot) : snapshot;
                    const document = doc.Document || doc.document || doc;
                    loadedDocumentId = document.DocumentId || document.documentId || '';
                }
            });
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'inst-fallback' }, null);
            runtime.executeCommand('inst-fallback', 'safe', {});
            snapshotAvailable = false;
            runtime.__watchdog.configure('inst-fallback', { forceSnapshotFallback: true });
            runtime.executeCommand('inst-fallback', 'boom', {});
            flushTimers();

            assert.strictEqual(loadedDocumentId, 'stable-doc');
            assert.ok(runtime.__watchdog.getEvents('inst-fallback').some(e => e.Event === 'snapshotFallbackUsed'));
            assert.strictEqual(runtime.__watchdog.getLastRecoveryDetail('inst-fallback').UsedSnapshotFallback, true);
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ─── Phase 7: Image wrap / position ──────────────────────────────────────

    [Fact]
    public async Task ObjectLayout_WithHorizontalAlignmentRight_RoundTripsViaCanonical()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

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
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorRuntime.__testHooks;
            const normalized = hooks.roundTripCanonicalDocument({
                DocumentId: 'doc-1',
                Blocks: [{
                    Id: 'img1', Type: 5, Order: 0,
                    Content: {
                        $type: 'image',
                        Url: '/img/test.png',
                        Size: { Width: 200, Height: 150 },
                        Layout: {
                            Kind: 1,
                            Position: { HorizontalAlignment: 2 },
                            Wrap: { Mode: 1 }
                        }
                    }
                }]
            });
            const layout = normalized.Blocks[0].Content.Layout;
            assert.ok(layout, 'Layout is preserved');
            assert.strictEqual(layout.Wrap.Mode, 1, 'WrapMode=Square preserved');
            assert.strictEqual(layout.Position.HorizontalAlignment, 2, 'HorizontalAlignment=Right(2) preserved');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ObjectLayout_OldFloatingLayoutInput_NormalizesToLayout()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

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
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorRuntime.__testHooks;

            // Old doc: FloatingLayout without HorizontalPosition or Distance props
            const runtimeDoc = hooks.fromCanonicalDocument({
                DocumentId: 'old-doc',
                Blocks: [{
                    Id: 'img1', Type: 5, Order: 0,
                    Content: {
                        $type: 'image',
                        Url: '/img/test.png',
                        Size: { Width: 200, Height: 150 },
                        FloatingLayout: {
                            Inline: false,
                            WrapMode: 1,
                            X: 24,
                            Y: 36
                        }
                    }
                }]
            });
            const exported = hooks.toCanonicalDocument(runtimeDoc);
            const layout = exported.Blocks[0].Content.Layout;
            assert.ok(layout, 'Layout created from old doc');
            assert.strictEqual(layout.Wrap.Mode, 1, 'WrapMode preserved');
            assert.strictEqual(layout.Position.HorizontalAlignment, undefined, 'HorizontalAlignment absent when old doc did not define it');
            assert.strictEqual(exported.Blocks[0].Content.FloatingLayout, undefined, 'old FloatingLayout is not written');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ObjectLayout_Distance_RoundTripsViaCanonical()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

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
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorRuntime.__testHooks;
            const normalized = hooks.roundTripCanonicalDocument({
                DocumentId: 'dist-doc',
                Blocks: [{
                    Id: 'img1', Type: 5, Order: 0,
                    Content: {
                        $type: 'image',
                        Url: '/img/test.png',
                        Size: { Width: 200, Height: 150 },
                        Layout: {
                            Kind: 1,
                            Position: { HorizontalAlignment: 2 },
                            Wrap: {
                                Mode: 1,
                                DistanceLeft: 12,
                                DistanceRight: 0,
                                DistanceTop: 4,
                                DistanceBottom: 4
                            }
                        }
                    }
                }]
            });
            const layout = normalized.Blocks[0].Content.Layout;
            assert.ok(layout, 'Layout roundtripped');
            assert.strictEqual(layout.Position.HorizontalAlignment, 2, 'HorizontalAlignment roundtripped');
            assert.strictEqual(layout.Wrap.DistanceLeft, 12, 'DistanceLeft roundtripped');
            assert.strictEqual(layout.Wrap.DistanceTop, 4, 'DistanceTop roundtripped');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task SetImageWrapMode_CommandIsRouted_DoesNotThrowOnUnknownInstance()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const editor = sandbox.window.tmDocumentEditorEngine;
            // setImageWrapMode must route without throwing even for an unknown instance
            let threw = false;
            try {
                editor.applyCommand('no-such-instance', 'setImageWrapMode', { wrapMode: 'Square' });
                editor.applyCommand('no-such-instance', 'setImageWrapMode', { wrapMode: 'TopBottom' });
                editor.applyCommand('no-such-instance', 'setImageWrapMode', { wrapMode: 'InFrontOfText' });
                editor.applyCommand('no-such-instance', 'setImageSize', { width: 240 });
            } catch (e) {
                threw = true;
            }
            assert.strictEqual(threw, false, 'setImageWrapMode must not throw on unknown instance');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task NormalizeWrapMode_ByName_ReturnsCorrectValueAndCss()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const fn = sandbox.window.tmDocumentEditorRuntime.__testHooks.normalizeWrapMode;

            const cases = [
                ['Inline',        { value: 0, css: 'inline' }],
                ['inline',        { value: 0, css: 'inline' }],
                ['Square',        { value: 1, css: 'square' }],
                ['square',        { value: 1, css: 'square' }],
                ['Through',       { value: 3, css: 'through' }],
                ['TopBottom',     { value: 4, css: 'top-bottom' }],
                ['topandbottom',  { value: 4, css: 'top-bottom' }],
                ['InFrontOfText', { value: 6, css: 'in-front-of-text' }],
                [null,            { value: 0, css: 'inline' }],
                [0,               { value: 0, css: 'inline' }],
                [1,               { value: 1, css: 'square' }],
                [3,               { value: 3, css: 'through' }],
                [4,               { value: 4, css: 'top-bottom' }],
            ];
            for (const [input, expected] of cases) {
                const result = fn(input);
                assert.strictEqual(result.value, expected.value, `value for '${input}'`);
                assert.strictEqual(result.css, expected.css, `css for '${input}'`);
            }
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task NormalizeHorizontalPosition_ByNameAndNumeric_ReturnsCorrectValue()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const fn = sandbox.window.tmDocumentEditorRuntime.__testHooks.normalizeHorizontalPosition;

            function check(input, expValue, expCss) {
                const r = fn(input);
                if (expValue === null) { assert.strictEqual(r, null, `null expected for ${JSON.stringify(input)}`); return; }
                assert.strictEqual(r.value, expValue, `value for ${JSON.stringify(input)}`);
                assert.strictEqual(r.css,   expCss,   `css for ${JSON.stringify(input)}`);
            }

            check(null,      null, null);
            check('Left',    0, 'left');
            check('left',    0, 'left');
            check('Center',  1, 'center');
            check('Right',   2, 'right');
            check('right',   2, 'right');
            check(0,         0, 'left');
            check(1,         1, 'center');
            check(2,         2, 'right');
            check('unknown', null, null);
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ─── Phase 14.1: showBlocks ───────────────────────────────────────────────

    [Fact]
    public async Task ShowBlocks_Enable_AddsClassToRoot()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            // Simulate a minimal instance root element
            const classes = new Set();
            const fakeRoot = {
                classList: {
                    add(c) { classes.add(c); },
                    remove(c) { classes.delete(c); },
                    contains(c) { return classes.has(c); }
                },
                querySelectorAll(selector) { return []; }
            };

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            hooks._instances.set('inst-1', { root: fakeRoot, options: {}, disposed: false });

            sandbox.window.tmDocumentEditorEngine.setShowBlocks('inst-1', true);

            assert.strictEqual(classes.has('tm-wysiwyg--show-blocks'), true, 'class must be added');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ShowBlocks_Disable_RemovesClassFromRoot()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const classes = new Set(['tm-wysiwyg--show-blocks']);
            const fakeRoot = {
                classList: {
                    add(c) { classes.add(c); },
                    remove(c) { classes.delete(c); },
                    contains(c) { return classes.has(c); }
                }
            };

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            hooks._instances.set('inst-2', { root: fakeRoot, options: {}, disposed: false });

            sandbox.window.tmDocumentEditorEngine.setShowBlocks('inst-2', false);

            assert.strictEqual(classes.has('tm-wysiwyg--show-blocks'), false, 'class must be removed');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ─── Phase 14.2: fullscreen ───────────────────────────────────────────────

    [Fact]
    public async Task Fullscreen_Enable_AddsBodyClass()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const bodyClasses = new Set();
            const sandbox = {
                window: {
                    addEventListener() {},
                    removeEventListener() {}
                },
                document: {
                    body: {
                        classList: {
                            add(c) { bodyClasses.add(c); },
                            remove(c) { bodyClasses.delete(c); }
                        },
                        style: {}
                    }
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            sandbox.window.tmDocumentEditor.setFullscreen(true);

            assert.strictEqual(bodyClasses.has('tm-document-editor--fullscreen'), true, 'body class must be added');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Fullscreen_Disable_RemovesBodyClass()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const bodyClasses = new Set(['tm-document-editor--fullscreen']);
            const sandbox = {
                window: {
                    addEventListener() {},
                    removeEventListener() {}
                },
                document: {
                    body: {
                        classList: {
                            add(c) { bodyClasses.add(c); },
                            remove(c) { bodyClasses.delete(c); }
                        },
                        style: { overflow: 'hidden' }
                    }
                },
                console,
                WeakMap,
                ResizeObserver: class { constructor() {} observe() {} disconnect() {} }
            };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor.js' });

            sandbox.window.tmDocumentEditor.setFullscreen(false);

            assert.strictEqual(bodyClasses.has('tm-document-editor--fullscreen'), false, 'body class must be removed');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ── Phase 14.3 – scrollToBlock ────────────────────────────────────────

    [Fact]
    public async Task ScrollToBlock_KnownBlockId_CallsScrollIntoView()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            let scrolledEl = null;
            const fakeBlock = {
                scrollIntoView(opts) { scrolledEl = this; }
            };
            const fakeRoot = {
                classList: { add() {}, remove() {}, contains() { return false; } },
                querySelector(selector) {
                    return selector === '[data-block-id="block-42"]' ? fakeBlock : null;
                }
            };

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            hooks._instances.set('inst-scroll', { root: fakeRoot, options: {}, disposed: false });

            sandbox.window.tmDocumentEditorEngine.scrollToBlock('inst-scroll', 'block-42');

            assert.strictEqual(scrolledEl, fakeBlock, 'scrollIntoView must be called on the block element');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ScrollToBlock_UnknownBlockId_DoesNotThrow()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const fakeRoot = {
                classList: { add() {}, remove() {}, contains() { return false; } },
                querySelector(selector) { return null; }
            };

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            hooks._instances.set('inst-scroll2', { root: fakeRoot, options: {}, disposed: false });

            sandbox.window.tmDocumentEditorEngine.scrollToBlock('inst-scroll2', 'nonexistent-block');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    // ── Phase 13.3 – setProtectionMode ───────────────────────────────────────

    [Fact]
    public async Task ProtectionMode_Enable_SetsIsProtectedFlagOnInstance()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const fakeRoot = {
                classList: { add() {}, remove() {}, contains() { return false; } },
                querySelector() { return null; }
            };
            const inst = { root: fakeRoot, options: {}, disposed: false };
            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            hooks._instances.set('inst-prot1', inst);

            const markers = [{ startBlockId: 'b1', startOffset: 0, endBlockId: 'b1', endOffset: 10 }];
            sandbox.window.tmDocumentEditorEngine.setProtectionMode('inst-prot1', true, markers);

            assert.strictEqual(inst._isProtected, true, '_isProtected must be true');
            assert.strictEqual(inst._protectedMarkers.length, 1, 'markers must be stored');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ProtectionMode_Disable_ClearsIsProtectedFlag()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const fakeRoot = {
                classList: { add() {}, remove() {}, contains() { return false; } },
                querySelector() { return null; }
            };
            const inst = { root: fakeRoot, options: {}, disposed: false, _isProtected: true, _protectedMarkers: [{}] };
            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            hooks._instances.set('inst-prot2', inst);

            sandbox.window.tmDocumentEditorEngine.setProtectionMode('inst-prot2', false, []);

            assert.strictEqual(inst._isProtected, false, '_isProtected must be false after disable');
            assert.strictEqual(inst._protectedMarkers.length, 0, 'markers must be cleared');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task ProtectionMode_UnknownInstance_DoesNotThrow()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            sandbox.window.tmDocumentEditorEngine.setProtectionMode('no-such-instance', true, []);
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public void ReviewUxRuntime_ContainsBatchReviewDisplayModesAndCommentRailSync()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("function reviewAllRevisions");
        script.Should().Contain("function setReviewDisplayMode");
        script.Should().Contain("reviewRevision: reviewRevision");
        script.Should().Contain("reviewAllRevisions: reviewAllRevisions");
        script.Should().Contain("setReviewDisplayMode: setReviewDisplayMode");
        script.Should().Contain("clearRevisionDecorations: clearRevisionDecorations");
    }

    [Fact]
    public async Task Phase2InputPipeline_AppliesSpaceEnterAndTypingBufferImmediately()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const model = engine.model.importFromCSharpJson({
                DocumentId: 'phase2-js',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'AB' }] } }]
            });
            const pipeline = engine.input.createInputPipeline({
                model,
                selection: { blockId: 'p1', offset: 1, isCollapsed: true },
                page: { x: 0, y: 0, width: 320, height: 480 }
            });

            const space = pipeline.handleBeforeInput({ inputType: 'insertText', data: ' ', preventDefault() {} });
            assert.strictEqual(model.indexes.blocks.p1.content.runs.map(run => run.text || '').join(''), 'A B');
            assert.strictEqual(space.selection.offset, 2);
            assert.ok(pipeline.debug().lastVisibleText.includes(' '), 'space must be represented as its own immediate visible text state');

            const enter = pipeline.handleBeforeInput({ inputType: 'insertParagraph', data: null, preventDefault() {} });
            assert.strictEqual(enter.operations[0].type, 'SplitParagraph');
            assert.notStrictEqual(enter.selection.blockId, 'p1');
            assert.strictEqual(enter.selection.offset, 0);
            assert.strictEqual(model.body.blocks[0].content.runs.map(run => run.text || '').join(''), 'A ');
            assert.strictEqual(model.body.blocks[1].content.runs.map(run => run.text || '').join(''), 'B');

            const buffer = engine.input.createTypingChangeBuffer({ timeoutMs: 1000 });
            buffer.push(engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 0 }, text: 'A' }, { source: 'typing', timestamp: 1000 }));
            buffer.push(engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 1 }, text: ' ' }, { source: 'typing', timestamp: 1010 }));
            buffer.push(engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 2 }, text: 'B' }, { source: 'typing', timestamp: 1020 }));
            const snapshot = buffer.snapshot();
            assert.strictEqual(snapshot.operationCount, 1);
            assert.strictEqual(snapshot.operations[0].text, 'A B');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public void Phase2Runtime_ContainsLiveTypingDomPatch()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(scriptPath);

        jsText.Should().Contain("applyLiveTypingDomPatch");
        jsText.Should().Contain("live-typing-dom-patch");
        jsText.Should().Contain("data-live-typing-patch");
        jsText.Should().Contain("renderLiveParagraphHtml");
    }

    [Fact]
    public async Task Phase3HeaderFooter_ImportAndLayoutPreserveRegions()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const model = engine.model.importFromCSharpJson({
                DocumentId: 'phase3-js',
                Blocks: [{ Id: 'body-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-r1', Text: 'Body' }] } }],
                HeadersFooters: [
                    { Id: 'header-primary', Type: 'Header', Scope: 'Primary', Blocks: [{ Id: 'header-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'header-r1', Text: 'Header' }] } }] },
                    { Id: 'footer-primary', Type: 1, Scope: 'Primary', Blocks: [{ Id: 'footer-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'footer-r1', Text: 'Footer ' }, { Id: 'footer-page', Type: 'Field', FieldType: 'PageNumber', FallbackText: '1' }] } }] }
                ]
            });

            assert.strictEqual(model.headers.length, 1);
            assert.strictEqual(model.footers.length, 1);
            assert.strictEqual(model.headers[0].scope, 'Primary');
            assert.strictEqual(model.footers[0].id, 'footer-primary');
            assert.strictEqual(model.indexes.blocks['header-p1'].content.runs[0].text, 'Header');
            assert.strictEqual(model.indexes.blocks['footer-p1'].content.runs[1].kind, 'field');

            const layout = engine.textLayout.createParagraphLayoutEngine().layoutDocument(model, {
                page: { x: 0, y: 0, width: 600, height: 800 },
                margins: { top: 48, right: 48, bottom: 48, left: 48 },
                headerHeight: 32,
                footerHeight: 32
            });
            assert.strictEqual(layout.headerFooterRegions.length, 2);
            assert.ok(layout.headerFooterRegions.some(region => region.region === 'Header' && region.headerFooterId === 'header-primary'));
            assert.ok(layout.headerFooterRegions.some(region => region.region === 'Footer' && region.headerFooterId === 'footer-primary'));
            assert.ok(layout.pages[0].headerFrame.y < layout.pages[0].bodyFrame.y);
            assert.ok(layout.pages[0].footerFrame.y > layout.pages[0].bodyFrame.y);

            const exported = engine.model.exportToCSharpJson(model);
            assert.strictEqual(exported.HeadersFooters.length, 2);
            assert.ok(exported.HeadersFooters.some(region => region.Region === 'Footer' && region.Type === 1 && region.Scope === 0));
            assert.strictEqual(exported.HeadersFooters[0].Blocks[0].Content.$type, 'paragraph');
            assert.strictEqual(exported.HeadersFooters[0].Blocks[0].Content.Inlines[0].$type, 'text');
            console.log(JSON.stringify({ ProtocolVersion: 1, Document: exported }));
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        var snapshot = JsonSerializer.Deserialize<WysiwygDocumentSnapshot>(
            result.StandardOutput.Trim(),
            new JsonSerializerOptions(DocumentEditorJson.Options) { PropertyNameCaseInsensitive = true });
        snapshot.Should().NotBeNull();
        snapshot!.Document.HeadersFooters.Should().Contain(region => region.Type == DocumentHeaderFooterType.Footer);
    }

    [Fact]
    public async Task Phase3HeaderFooter_FullContractExportDeserializesForProviderSave()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var provider = new InMemoryDocumentEditorProvider();
        var sourceDocument = provider.SeedRecoveryDocument();
        var sourcePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(sourcePath, DocumentEditorJson.Serialize(sourceDocument));

        try
        {
            var nodeScript =
                """
                const fs = require('fs');
                const vm = require('vm');

                const code = fs.readFileSync(process.argv[2], 'utf8');
                const source = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
                const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON };
                sandbox.window.setTimeout = setTimeout;
                sandbox.window.clearTimeout = clearTimeout;
                sandbox.window.console = console;
                vm.createContext(sandbox);
                vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

                const engine = sandbox.window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson(source);
                const footer = model.footers[0].blocks[0].content.runs[0];
                footer.text = footer.text + ' provider-boundary';
                const exported = engine.model.exportToCSharpJson(model);
                console.log(JSON.stringify({ ProtocolVersion: 1, Document: exported }));
                """;

            var result = await RunNodeAsync(scriptPath, nodeScript, sourcePath);
            result.ExitCode.Should().Be(0, result.StandardError);
            var snapshot = JsonSerializer.Deserialize<WysiwygDocumentSnapshot>(
                result.StandardOutput.Trim(),
                new JsonSerializerOptions(DocumentEditorJson.Options) { PropertyNameCaseInsensitive = true });
            snapshot.Should().NotBeNull();
            var footerText = snapshot!.Document.HeadersFooters
                .Where(region => region.Type == DocumentHeaderFooterType.Footer)
                .SelectMany(region => region.Blocks)
                .Select(block => block.Content)
                .OfType<ParagraphBlockContent>()
                .SelectMany(content => content.Inlines)
                .OfType<TextRun>()
                .Select(run => run.Text)
                .FirstOrDefault(text => text.Contains("provider-boundary", StringComparison.Ordinal));
            footerText.Should().NotBeNull();
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void Phase3Runtime_ContainsHeaderFooterRendererAndSelectionRouting()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(scriptPath);

        jsText.Should().Contain("renderHeaderFooterHtml");
        jsText.Should().Contain("resolveHeaderFooterRegion");
        jsText.Should().Contain("document-page-header");
        jsText.Should().Contain("document-page-footer");
        jsText.Should().Contain("HeaderFooterId");
        jsText.Should().Contain("flushTypingBoundaryPatchDispatch");
    }

    [Fact]
    public async Task Phase13PerformanceStatsAggregation_SeparatesIncrementalTypingFromFullDocumentLayout()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, performance: { now: () => 100 } };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = sandbox.performance;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const inst = { performanceStats: hooks.createStrictPerformanceStats(), diagnostics: { timeline: [], lastErrors: [], watchdogFailures: [], debugWarnings: [], modelVersion: 0, selectionVersion: 0 } };
            hooks.recordOperationPerformance(inst, [
                { type: 'InsertText' },
                { type: 'SplitParagraph' }
            ], 3.5, ['block:one'], 'typing');
            hooks.recordOperationPerformance(inst, [{ type: 'UpdateImageLayout' }], 7, ['block:image'], 'image');
            hooks.recordOperationPerformance(inst, [{ type: 'InsertText' }], 11, ['document'], 'recovery');

            assert.strictEqual(inst.performanceStats.inputOperationCount, 4);
            assert.strictEqual(inst.performanceStats.incrementalOperationCount, 3);
            assert.strictEqual(inst.performanceStats.fullDocumentLayoutCount, 1);
            assert.strictEqual(inst.performanceStats.typingLatencyCount, 3);
            assert.strictEqual(inst.performanceStats.imageDragLatencyCount, 1);
            assert.strictEqual(inst.performanceStats.inputOperationMaxMs, 11);
            assert.ok(inst.diagnostics.timeline.some(entry => entry.kind === 'operation-performance'));
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
