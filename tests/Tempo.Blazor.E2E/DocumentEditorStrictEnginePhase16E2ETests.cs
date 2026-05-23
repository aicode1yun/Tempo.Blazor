using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the JS/C# boundary, dirty state, autosave, and public compatibility APIs.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase16E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Boundary_SendsCanonicalPatchAfterCommitAndSurvivesCallbackFailure()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<BoundaryPatchProbe>(
            """
            async () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const calls = [];
                const failingDotNet = {
                    invokeMethodAsync(method, payload) {
                        calls.push({ method, payload });
                        if (method === 'HandleJsBoundaryPatchGenerated') {
                            return Promise.reject(new Error('phase16 boundary failure'));
                        }
                        return Promise.resolve(true);
                    }
                };
                const id = engine.create(host, { instanceId: 'phase16-boundary' }, failingDotNet);
                engine.loadDocument(id, {
                    DocumentId: 'phase16-boundary-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha' }] } }]
                });
                const commit = engine.applyCommand(id, 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 5 },
                    text: ' beta'
                });
                await new Promise(resolve => setTimeout(resolve, 30));
                const snapshot = engine.getDocumentSnapshot(id);
                const debug = engine.getDebugSnapshot(id);
                const dirty = engine.getDirtyState(id);
                const patchCall = calls.find(call => call.method === 'HandleJsBoundaryPatchGenerated');
                const dirtyCall = calls.find(call => call.method === 'HandleJsDirtyStateChanged' && call.payload?.isDirty === true);
                engine.dispose(id);
                host.remove();
                return {
                    commitOk: commit.ok,
                    text: snapshot.document.body.blocks[0].content.runs.map(run => run.text).join(''),
                    patchSent: !!patchCall,
                    dirtySent: !!dirtyCall,
                    patchTransactionId: patchCall?.payload?.transactionId || '',
                    patchOperationIds: patchCall?.payload?.operationIds || [],
                    affectedBlockIds: patchCall?.payload?.affectedBlockIds || [],
                    hasSnapshot: !!patchCall?.payload?.snapshot,
                    hasCSharpDocument: !!patchCall?.payload?.csharpDocument,
                    selectionBlock: patchCall?.payload?.selection?.blockId || '',
                    dirtyIsStillTrue: dirty.isDirty,
                    failureCaptured: debug.boundaryFailures.some(item => item.code === 'boundary-patch-dispatch-failed'),
                    modelSurvivedFailure: snapshot.document.body.blocks[0].content.runs.map(run => run.text).join('') === 'Alpha beta'
                };
            }
            """);

        result.CommitOk.Should().BeTrue();
        result.Text.Should().Be("Alpha beta");
        result.PatchSent.Should().BeTrue();
        result.DirtySent.Should().BeTrue();
        result.PatchTransactionId.Should().NotBeNullOrWhiteSpace();
        result.PatchOperationIds.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
        result.AffectedBlockIds.Should().Contain("p1");
        result.HasSnapshot.Should().BeTrue();
        result.HasCSharpDocument.Should().BeTrue();
        result.SelectionBlock.Should().Be("p1");
        result.DirtyIsStillTrue.Should().BeTrue();
        result.FailureCaptured.Should().BeTrue();
        result.ModelSurvivedFailure.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Boundary_CSharpUpdatesSaveAckRemoteAssetAndRecoveryRespectActiveTransaction()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<CSharpUpdateProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase16-csharp' });
                const initial = engine.applyCSharpUpdate(id, {
                    type: 'loadDocument',
                    document: {
                        DocumentId: 'phase16-csharp-doc',
                        Blocks: [
                            { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha' }] } },
                            { Id: 'img1', Type: 'Image', Content: { ObjectId: 'img-o1', AssetId: 'asset-1', AltText: 'Asset image' } }
                        ]
                    },
                    version: 'v1',
                    epoch: 3
                });
                const cleanAfterLoad = engine.getDirtyState(id);
                engine.applyCommand(id, 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 5 },
                    text: ' local'
                });
                const dirtyAfterLocal = engine.getDirtyState(id);
                const ack = engine.applyCSharpUpdate(id, { type: 'saveAck', epoch: dirtyAfterLocal.epoch, version: 'v2', marker: 'saved-v2' });
                const remote = engine.applyCSharpUpdate(id, {
                    type: 'remoteOperations',
                    operations: [engine.operations.createOperation(engine.operations.types.InsertText, {
                        target: { blockId: 'p1', offset: 11 },
                        text: ' remote'
                    }, { source: 'remote' })]
                });
                const asset = engine.applyCSharpUpdate(id, {
                    type: 'providerImageUrl',
                    assetId: 'asset-1',
                    url: 'https://cdn.example.test/asset-1.png'
                });
                const hotRefresh = engine.applyCSharpUpdate(id, {
                    type: 'snapshotRefresh',
                    document: { DocumentId: 'bad', Blocks: [{ Id: 'bad', Type: 'Paragraph', Content: { Inlines: [{ Text: 'Bad' }] } }] }
                });
                engine.__testActive = true;
                engine.__testActive = false;
                engine.__testHooks.instances.get(id).activeTransaction = { id: 'manual-active' };
                const blocked = engine.applyCSharpUpdate(id, {
                    type: 'remoteOperations',
                    operations: [engine.operations.createOperation(engine.operations.types.InsertText, {
                        target: { blockId: 'p1', offset: 0 },
                        text: 'blocked '
                    }, { source: 'remote' })]
                });
                engine.__testHooks.instances.get(id).activeTransaction = null;
                const recovery = engine.applyCSharpUpdate(id, {
                    type: 'snapshotRefresh',
                    recovery: true,
                    document: {
                        DocumentId: 'phase16-recovered',
                        Blocks: [{ Id: 'p-recovered', Type: 'Paragraph', Content: { Inlines: [{ Id: 'rr', Text: 'Recovered' }] } }]
                    }
                });
                const snapshot = engine.getDocumentSnapshot(id);
                const debug = engine.getDebugSnapshot(id);
                engine.dispose(id);
                host.remove();
                return {
                    initialOk: initial.ok,
                    cleanAfterLoad: cleanAfterLoad.isDirty,
                    dirtyAfterLocal: dirtyAfterLocal.isDirty,
                    ackClean: ack.dirtyState.isDirty,
                    ackVersion: ack.dirtyState.version,
                    remoteOk: remote.ok,
                    assetOk: asset.ok,
                    assetUrl: asset.url,
                    hotRefreshRejected: hotRefresh.ok === false && hotRefresh.error.code === 'full-snapshot-refresh-requires-recovery',
                    blockedRejected: blocked.ok === false && blocked.error.code === 'active-transaction-conflict',
                    recoveryOk: recovery.ok,
                    recoveredText: snapshot.document.body.blocks[0].content.runs.map(run => run.text).join(''),
                    lastUpdateType: debug.lastCSharpUpdate.type
                };
            }
            """);

        result.InitialOk.Should().BeTrue();
        result.CleanAfterLoad.Should().BeFalse();
        result.DirtyAfterLocal.Should().BeTrue();
        result.AckClean.Should().BeFalse();
        result.AckVersion.Should().Be("v2");
        result.RemoteOk.Should().BeTrue();
        result.AssetOk.Should().BeTrue();
        result.AssetUrl.Should().Be("https://cdn.example.test/asset-1.png");
        result.HotRefreshRejected.Should().BeTrue();
        result.BlockedRejected.Should().BeTrue();
        result.RecoveryOk.Should().BeTrue();
        result.RecoveredText.Should().Be("Recovered");
        result.LastUpdateType.Should().Be("snapshotRefresh");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Boundary_DirtyAutosaveSnapshotAckAndFailureDoNotRollbackModel()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<AutosaveProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase16-autosave' });
                engine.loadDocument(id, {
                    DocumentId: 'phase16-autosave-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha' }] } }]
                });
                engine.applyCommand(id, 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 5 },
                    text: ' autosave'
                });
                const dirty = engine.getDirtyState(id);
                const autosaveSnapshot = engine.requestAutosaveSnapshot(id);
                const failed = engine.markAutosaveFailed(id, { message: 'Save failed', kind: 'network' });
                const afterFailureText = engine.getDocumentSnapshot(id).document.body.blocks[0].content.runs.map(run => run.text).join('');
                const ack = engine.acknowledgeSave(id, { epoch: dirty.epoch, version: 'saved-after-retry', marker: 'saved-after-retry' });
                const afterAck = engine.getDirtyState(id);
                engine.dispose(id);
                host.remove();
                return {
                    dirtyAfterEdit: dirty.isDirty,
                    autosaveText: autosaveSnapshot.csharpDocument.Blocks[0].Content.Inlines.map(run => run.Text).join(''),
                    autosaveEpoch: autosaveSnapshot.epoch,
                    failureDirty: failed.dirtyState.isDirty,
                    failureMessage: failed.dirtyState.lastFailure.message,
                    afterFailureText,
                    ackOk: ack.ok,
                    cleanAfterAck: afterAck.isDirty,
                    savedMarker: afterAck.lastSavedMarker
                };
            }
            """);

        result.DirtyAfterEdit.Should().BeTrue();
        result.AutosaveText.Should().Be("Alpha autosave");
        result.AutosaveEpoch.Should().BeGreaterThan(0);
        result.FailureDirty.Should().BeTrue();
        result.FailureMessage.Should().Be("Save failed");
        result.AfterFailureText.Should().Be("Alpha autosave");
        result.AckOk.Should().BeTrue();
        result.CleanAfterAck.Should().BeFalse();
        result.SavedMarker.Should().Be("saved-after-retry");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Boundary_PublicCompatibilityExportsImportsAndPanelsUseCanonicalSnapshot()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<PublicCompatibilityProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase16-public' });
                engine.loadDocument(id, {
                    DocumentId: 'phase16-public-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Panel text' }] } }],
                    Revisions: [{
                        Id: 'rev1',
                        Type: 'Insertion',
                        Author: 'u1',
                        Timestamp: 1,
                        AffectedRange: { BlockId: 'p1', Start: 0, End: 5 },
                        Payload: { text: 'Panel' },
                        Status: 'Pending'
                    }],
                    Comments: [{ Id: 'c1', BlockId: 'p1', Text: 'Comment text' }]
                });
                const exported = engine.exportCanonicalSnapshot(id);
                const panels = engine.getBoundaryPanelData(id);
                const imported = engine.importCanonicalSnapshot(id, {
                    DocumentId: 'phase16-imported',
                    Blocks: [{ Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Imported ODT snapshot' }] } }],
                    Revisions: [],
                    Comments: []
                });
                const afterImport = engine.getDocumentSnapshot(id);
                engine.dispose(id);
                host.remove();
                return {
                    exportOk: exported.ok,
                    exportDocumentId: exported.csharpDocument.DocumentId,
                    exportText: exported.csharpDocument.Blocks[0].Content.Inlines.map(run => run.Text).join(''),
                    panelCommentCount: panels.comments.length,
                    panelRevisionCount: panels.revisions.length,
                    panelRevisionStatus: panels.revisions[0]?.status || panels.revisions[0]?.Status || '',
                    importOk: imported.ok,
                    importedText: afterImport.csharpDocument.Blocks[0].Content.Inlines.map(run => run.Text).join('')
                };
            }
            """);

        result.ExportOk.Should().BeTrue();
        result.ExportDocumentId.Should().Be("phase16-public-doc");
        result.ExportText.Should().Be("Panel text");
        result.PanelCommentCount.Should().Be(1);
        result.PanelRevisionCount.Should().Be(1);
        result.PanelRevisionStatus.Should().Be("Pending");
        result.ImportOk.Should().BeTrue();
        result.ImportedText.Should().Be("Imported ODT snapshot");
    }

    private sealed class BoundaryPatchProbe
    {
        [JsonPropertyName("commitOk")] public bool CommitOk { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("patchSent")] public bool PatchSent { get; set; }
        [JsonPropertyName("dirtySent")] public bool DirtySent { get; set; }
        [JsonPropertyName("patchTransactionId")] public string PatchTransactionId { get; set; } = "";
        [JsonPropertyName("patchOperationIds")] public string[] PatchOperationIds { get; set; } = [];
        [JsonPropertyName("affectedBlockIds")] public string[] AffectedBlockIds { get; set; } = [];
        [JsonPropertyName("hasSnapshot")] public bool HasSnapshot { get; set; }
        [JsonPropertyName("hasCSharpDocument")] public bool HasCSharpDocument { get; set; }
        [JsonPropertyName("selectionBlock")] public string SelectionBlock { get; set; } = "";
        [JsonPropertyName("dirtyIsStillTrue")] public bool DirtyIsStillTrue { get; set; }
        [JsonPropertyName("failureCaptured")] public bool FailureCaptured { get; set; }
        [JsonPropertyName("modelSurvivedFailure")] public bool ModelSurvivedFailure { get; set; }
    }

    private sealed class CSharpUpdateProbe
    {
        [JsonPropertyName("initialOk")] public bool InitialOk { get; set; }
        [JsonPropertyName("cleanAfterLoad")] public bool CleanAfterLoad { get; set; }
        [JsonPropertyName("dirtyAfterLocal")] public bool DirtyAfterLocal { get; set; }
        [JsonPropertyName("ackClean")] public bool AckClean { get; set; }
        [JsonPropertyName("ackVersion")] public string AckVersion { get; set; } = "";
        [JsonPropertyName("remoteOk")] public bool RemoteOk { get; set; }
        [JsonPropertyName("assetOk")] public bool AssetOk { get; set; }
        [JsonPropertyName("assetUrl")] public string AssetUrl { get; set; } = "";
        [JsonPropertyName("hotRefreshRejected")] public bool HotRefreshRejected { get; set; }
        [JsonPropertyName("blockedRejected")] public bool BlockedRejected { get; set; }
        [JsonPropertyName("recoveryOk")] public bool RecoveryOk { get; set; }
        [JsonPropertyName("recoveredText")] public string RecoveredText { get; set; } = "";
        [JsonPropertyName("lastUpdateType")] public string LastUpdateType { get; set; } = "";
    }

    private sealed class AutosaveProbe
    {
        [JsonPropertyName("dirtyAfterEdit")] public bool DirtyAfterEdit { get; set; }
        [JsonPropertyName("autosaveText")] public string AutosaveText { get; set; } = "";
        [JsonPropertyName("autosaveEpoch")] public int AutosaveEpoch { get; set; }
        [JsonPropertyName("failureDirty")] public bool FailureDirty { get; set; }
        [JsonPropertyName("failureMessage")] public string FailureMessage { get; set; } = "";
        [JsonPropertyName("afterFailureText")] public string AfterFailureText { get; set; } = "";
        [JsonPropertyName("ackOk")] public bool AckOk { get; set; }
        [JsonPropertyName("cleanAfterAck")] public bool CleanAfterAck { get; set; }
        [JsonPropertyName("savedMarker")] public string SavedMarker { get; set; } = "";
    }

    private sealed class PublicCompatibilityProbe
    {
        [JsonPropertyName("exportOk")] public bool ExportOk { get; set; }
        [JsonPropertyName("exportDocumentId")] public string ExportDocumentId { get; set; } = "";
        [JsonPropertyName("exportText")] public string ExportText { get; set; } = "";
        [JsonPropertyName("panelCommentCount")] public int PanelCommentCount { get; set; }
        [JsonPropertyName("panelRevisionCount")] public int PanelRevisionCount { get; set; }
        [JsonPropertyName("panelRevisionStatus")] public string PanelRevisionStatus { get; set; } = "";
        [JsonPropertyName("importOk")] public bool ImportOk { get; set; }
        [JsonPropertyName("importedText")] public string ImportedText { get; set; } = "";
    }
}
