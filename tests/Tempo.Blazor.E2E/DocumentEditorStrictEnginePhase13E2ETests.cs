using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for command dispatching and formatting state snapshots.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase13E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Commands_AllSurfacesNormalizeToSameCommandAndUseRuntimeSelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<CommandSurfaceProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase13-surfaces',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma' }] } }]
                });
                const dispatcher = engine.commands.createCommandDispatcher(model, {
                    selection: { blockId: 'p1', anchor: { blockId: 'p1', offset: 0 }, focus: { blockId: 'p1', offset: 5 } }
                });
                const normalized = [
                    dispatcher.normalizeCommandId({ surface: 'ribbon', commandId: 'bold' }),
                    dispatcher.normalizeCommandId({ surface: 'floating', commandId: 'toggle-bold' }),
                    dispatcher.normalizeCommandId({ surface: 'context', commandId: 'format.bold' }),
                    dispatcher.normalizeCommandId({ surface: 'keyboard', key: 'b', ctrlKey: true })
                ];
                const results = [
                    dispatcher.executeCommand({ surface: 'ribbon', commandId: 'bold' }),
                    dispatcher.executeCommand({ surface: 'floating', commandId: 'toggle-bold' }),
                    dispatcher.executeCommand({ surface: 'context', commandId: 'format.bold' }),
                    dispatcher.executeCommand({ surface: 'keyboard', key: 'b', ctrlKey: true })
                ];
                const state = dispatcher.getState('bold');
                const debugFailure = dispatcher.executeCommand({ surface: 'ribbon', commandId: 'missing-command' });
                return {
                    normalized,
                    allSame: normalized.every(id => id === 'bold'),
                    commandOk: results.every(item => item.ok === true),
                    sources: results.map(item => item.source),
                    usedRuntimeSelection: results.every(item => item.usedRuntimeSelection === true),
                    readDomSelection: results.some(item => item.readDomSelection === true),
                    mutatedDomDirectly: results.some(item => item.mutatedDomDirectly === true),
                    transactionResult: results[0].transaction?.ok === true,
                    stateEnabled: state.isEnabled,
                    stateValueType: typeof state.value,
                    failureLogged: debugFailure.ok === false && dispatcher.getDebugLog().some(entry => entry.code === 'unknown-command')
                };
            }
            """);

        result.AllSame.Should().BeTrue();
        result.CommandOk.Should().BeTrue();
        result.Sources.Should().Contain(["ribbon", "floating", "context", "keyboard"]);
        result.UsedRuntimeSelection.Should().BeTrue();
        result.ReadDomSelection.Should().BeFalse();
        result.MutatedDomDirectly.Should().BeFalse();
        result.TransactionResult.Should().BeTrue();
        result.StateEnabled.Should().BeTrue();
        result.StateValueType.Should().Be("boolean");
        result.FailureLogged.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Commands_InlineFormattingUseOperationsAndClearFormatting()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<InlineCommandProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase13-inline',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma' }] } }]
                });
                const selection = { blockId: 'p1', anchor: { blockId: 'p1', offset: 0 }, focus: { blockId: 'p1', offset: 5 } };
                const dispatcher = engine.commands.createCommandDispatcher(model, { selection });
                const bold = dispatcher.executeCommand('bold');
                const italic = dispatcher.executeCommand('italic');
                const underline = dispatcher.executeCommand('underline');
                const strike = dispatcher.executeCommand('strike');
                const color = dispatcher.executeCommand('textColor', { color: '#123456' });
                const background = dispatcher.executeCommand('backgroundColor', { color: '#fff59d' });
                const link = dispatcher.executeCommand('link', { href: 'https://example.test' });
                const beforeClear = dispatcher.getFormattingSnapshot();
                const clear = dispatcher.executeCommand('clearFormatting');
                const afterClear = dispatcher.getFormattingSnapshot();
                const operations = dispatcher.getCommittedOperations().map(operation => operation.type);
                const marksAfterClear = model.body.blocks[0].content.runs.flatMap(run => run.marks || []);
                return {
                    allOk: [bold, italic, underline, strike, color, background, link, clear].every(item => item.ok === true),
                    operationTypes: operations,
                    usedOperationsOnly: operations.every(type => type === engine.operations.types.ApplyMark || type === engine.operations.types.RemoveMark),
                    beforeBold: beforeClear.commandValues.bold === true,
                    beforeColor: beforeClear.commandValues.textColor,
                    beforeBackground: beforeClear.commandValues.backgroundColor,
                    beforeLink: beforeClear.commandValues.link,
                    afterBold: afterClear.commandValues.bold,
                    afterColor: afterClear.commandValues.textColor,
                    afterBackground: afterClear.commandValues.backgroundColor,
                    marksAfterClear: marksAfterClear.length
                };
            }
            """);

        result.AllOk.Should().BeTrue();
        result.OperationTypes.Should().Contain("ApplyMark");
        result.OperationTypes.Should().Contain("RemoveMark");
        result.UsedOperationsOnly.Should().BeTrue();
        result.BeforeBold.Should().BeTrue();
        result.BeforeColor.Should().Be("#123456");
        result.BeforeBackground.Should().Be("#fff59d");
        result.BeforeLink.Should().Be("https://example.test");
        result.AfterBold.Should().BeFalse();
        result.AfterColor.Should().BeNull();
        result.AfterBackground.Should().BeNull();
        result.MarksAfterClear.Should().Be(0);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Commands_ParagraphFormattingPreservesCaretAndState()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ParagraphCommandProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase13-paragraph',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma' }] } }]
                });
                const dispatcher = engine.commands.createCommandDispatcher(model, {
                    selection: { blockId: 'p1', offset: 7, isCollapsed: true }
                });
                const align = dispatcher.executeCommand('alignment', { value: 'justify' });
                const lineSpacing = dispatcher.executeCommand('lineSpacing', { value: 1.5 });
                const spacingBefore = dispatcher.executeCommand('spacingBefore', { value: 12 });
                const spacingAfter = dispatcher.executeCommand('spacingAfter', { value: 18 });
                const list = dispatcher.executeCommand('list', { value: 'numbered' });
                const indent = dispatcher.executeCommand('indent', { delta: 1 });
                const snapshot = dispatcher.getFormattingSnapshot();
                return {
                    allOk: [align, lineSpacing, spacingBefore, spacingAfter, list, indent].every(item => item.ok === true),
                    caretBlock: dispatcher.getSelection().blockId,
                    caretOffset: dispatcher.getSelection().offset,
                    alignment: model.body.blocks[0].content.alignment,
                    lineSpacing: model.body.blocks[0].content.lineSpacing,
                    spacingBefore: model.body.blocks[0].content.spacingBefore,
                    spacingAfter: model.body.blocks[0].content.spacingAfter,
                    listType: model.body.blocks[0].content.listType,
                    indentLevel: model.body.blocks[0].content.indentLevel,
                    snapshotAlignment: snapshot.paragraph.alignment,
                    snapshotLineSpacing: snapshot.paragraph.lineSpacing,
                    toolbarSelected: snapshot.commandValues.alignment
                };
            }
            """);

        result.AllOk.Should().BeTrue();
        result.CaretBlock.Should().Be("p1");
        result.CaretOffset.Should().Be(7);
        result.Alignment.Should().Be("justify");
        result.LineSpacing.Should().Be(1.5);
        result.SpacingBefore.Should().Be(12);
        result.SpacingAfter.Should().Be(18);
        result.ListType.Should().Be("numbered");
        result.IndentLevel.Should().Be(1);
        result.SnapshotAlignment.Should().Be("justify");
        result.SnapshotLineSpacing.Should().Be(1.5);
        result.ToolbarSelected.Should().Be("justify");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Commands_FormattingSnapshotSupportsMixedObjectsAndBlazorSubscriptions()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<FormattingSnapshotProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase13-state',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Alignment: 'left', Inlines: [
                            { Id: 'r1', Text: 'Alpha ', Marks: [{ Type: 'Bold' }], Style: { color: '#111111' } },
                            { Id: 'r2', Text: 'green', RevisionId: 'rev-ins' },
                            { Id: 'r3', Text: ' plain' }
                        ] } },
                        { Id: 'img1', Type: 'Image', Content: { ObjectId: 'img-o1', AltText: 'Image' } },
                        { Id: 'tbl1', Type: 'Table', Content: { Rows: [{ Id: 'row1', Cells: [{ Id: 'cell1', Blocks: [{ Id: 'cell-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cell-r1', Text: 'Cell' }] } }] }] }] } }
                    ],
                    Revisions: [{
                        Id: 'rev-ins',
                        Type: 'Insertion',
                        Author: 'u1',
                        Timestamp: 1,
                        AffectedRange: { BlockId: 'p1', Start: 6, End: 11 },
                        Payload: { text: 'green', decorativeStyle: { color: '#008000', underline: true } },
                        Status: 'Pending'
                    }]
                });
                const dispatcher = engine.commands.createCommandDispatcher(model, {
                    selection: { blockId: 'p1', anchor: { blockId: 'p1', offset: 0 }, focus: { blockId: 'p1', offset: 17 } }
                });
                const snapshots = [];
                dispatcher.subscribeFormattingState(snapshot => snapshots.push(snapshot));
                const mixed = dispatcher.getFormattingSnapshot();
                dispatcher.setSelection({ blockId: 'img1', objectId: 'img-o1', isObjectSelection: true });
                const image = dispatcher.getFormattingSnapshot();
                dispatcher.setSelection({ blockId: 'tbl1', objectId: 'tbl1', isObjectSelection: true });
                const table = dispatcher.getFormattingSnapshot();
                const tableToolbar = dispatcher.getBlazorToolbarState();
                dispatcher.setSelection({ blockId: 'p1', offset: 7, isCollapsed: true });
                const revisionCursor = dispatcher.getFormattingSnapshot();
                return {
                    mixedBold: mixed.inline.mixed.bold,
                    actualColor: revisionCursor.inline.active.textColor,
                    revisionGreenIgnored: revisionCursor.inline.active.textColor !== '#008000',
                    pendingTypingMarksLength: mixed.pendingTypingMarks.length,
                    imageSelected: image.image.isSelected,
                    tableSelected: table.table.isSelected,
                    commandValueHasBold: Object.prototype.hasOwnProperty.call(mixed.commandValues, 'bold'),
                    disabledReasonForImageBold: image.disabledReasons.bold,
                    blazorSnapshots: snapshots.length,
                    ribbonState: dispatcher.getBlazorToolbarState().ribbon.commandValues.bold,
                    floatingState: dispatcher.getBlazorToolbarState().floating.commandValues.bold,
                    sidePanelImage: tableToolbar.sidePanel.table.isSelected
                };
            }
            """);

        result.MixedBold.Should().BeTrue();
        result.ActualColor.Should().Be("#111111");
        result.RevisionGreenIgnored.Should().BeTrue();
        result.PendingTypingMarksLength.Should().Be(0);
        result.ImageSelected.Should().BeTrue();
        result.TableSelected.Should().BeTrue();
        result.CommandValueHasBold.Should().BeTrue();
        result.DisabledReasonForImageBold.Should().Be("selection-not-text");
        result.BlazorSnapshots.Should().BeGreaterThanOrEqualTo(3);
        result.RibbonState.Should().BeFalse();
        result.FloatingState.Should().BeFalse();
        result.SidePanelImage.Should().BeTrue();
    }

    private sealed class CommandSurfaceProbe
    {
        [JsonPropertyName("normalized")] public string[] Normalized { get; set; } = [];
        [JsonPropertyName("allSame")] public bool AllSame { get; set; }
        [JsonPropertyName("commandOk")] public bool CommandOk { get; set; }
        [JsonPropertyName("sources")] public string[] Sources { get; set; } = [];
        [JsonPropertyName("usedRuntimeSelection")] public bool UsedRuntimeSelection { get; set; }
        [JsonPropertyName("readDomSelection")] public bool ReadDomSelection { get; set; }
        [JsonPropertyName("mutatedDomDirectly")] public bool MutatedDomDirectly { get; set; }
        [JsonPropertyName("transactionResult")] public bool TransactionResult { get; set; }
        [JsonPropertyName("stateEnabled")] public bool StateEnabled { get; set; }
        [JsonPropertyName("stateValueType")] public string StateValueType { get; set; } = string.Empty;
        [JsonPropertyName("failureLogged")] public bool FailureLogged { get; set; }
    }

    private sealed class InlineCommandProbe
    {
        [JsonPropertyName("allOk")] public bool AllOk { get; set; }
        [JsonPropertyName("operationTypes")] public string[] OperationTypes { get; set; } = [];
        [JsonPropertyName("usedOperationsOnly")] public bool UsedOperationsOnly { get; set; }
        [JsonPropertyName("beforeBold")] public bool BeforeBold { get; set; }
        [JsonPropertyName("beforeColor")] public string? BeforeColor { get; set; }
        [JsonPropertyName("beforeBackground")] public string? BeforeBackground { get; set; }
        [JsonPropertyName("beforeLink")] public string? BeforeLink { get; set; }
        [JsonPropertyName("afterBold")] public bool AfterBold { get; set; }
        [JsonPropertyName("afterColor")] public string? AfterColor { get; set; }
        [JsonPropertyName("afterBackground")] public string? AfterBackground { get; set; }
        [JsonPropertyName("marksAfterClear")] public int MarksAfterClear { get; set; }
    }

    private sealed class ParagraphCommandProbe
    {
        [JsonPropertyName("allOk")] public bool AllOk { get; set; }
        [JsonPropertyName("caretBlock")] public string CaretBlock { get; set; } = string.Empty;
        [JsonPropertyName("caretOffset")] public int CaretOffset { get; set; }
        [JsonPropertyName("alignment")] public string Alignment { get; set; } = string.Empty;
        [JsonPropertyName("lineSpacing")] public double LineSpacing { get; set; }
        [JsonPropertyName("spacingBefore")] public int SpacingBefore { get; set; }
        [JsonPropertyName("spacingAfter")] public int SpacingAfter { get; set; }
        [JsonPropertyName("listType")] public string ListType { get; set; } = string.Empty;
        [JsonPropertyName("indentLevel")] public int IndentLevel { get; set; }
        [JsonPropertyName("snapshotAlignment")] public string SnapshotAlignment { get; set; } = string.Empty;
        [JsonPropertyName("snapshotLineSpacing")] public double SnapshotLineSpacing { get; set; }
        [JsonPropertyName("toolbarSelected")] public string ToolbarSelected { get; set; } = string.Empty;
    }

    private sealed class FormattingSnapshotProbe
    {
        [JsonPropertyName("mixedBold")] public bool MixedBold { get; set; }
        [JsonPropertyName("actualColor")] public string? ActualColor { get; set; }
        [JsonPropertyName("revisionGreenIgnored")] public bool RevisionGreenIgnored { get; set; }
        [JsonPropertyName("pendingTypingMarksLength")] public int PendingTypingMarksLength { get; set; }
        [JsonPropertyName("imageSelected")] public bool ImageSelected { get; set; }
        [JsonPropertyName("tableSelected")] public bool TableSelected { get; set; }
        [JsonPropertyName("commandValueHasBold")] public bool CommandValueHasBold { get; set; }
        [JsonPropertyName("disabledReasonForImageBold")] public string DisabledReasonForImageBold { get; set; } = string.Empty;
        [JsonPropertyName("blazorSnapshots")] public int BlazorSnapshots { get; set; }
        [JsonPropertyName("ribbonState")] public bool RibbonState { get; set; }
        [JsonPropertyName("floatingState")] public bool FloatingState { get; set; }
        [JsonPropertyName("sidePanelImage")] public bool SidePanelImage { get; set; }
    }
}
