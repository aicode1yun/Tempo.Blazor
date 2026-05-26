using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase17AccessibilityFocusJavaScriptTests
{
    [Fact]
    public async Task Phase17_ObjectLayerSelectionOnlyFocusExposesAccessibleImageState()
    {
        var result = await RunScenarioAsync(
            "object-layer-a11y-state",
            """
            const model = hooks.importFromCSharpJson(createWrappedDrawingDocument());
            const textSelection = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 2 });
            const objectSelection = hooks.createObjectSelectionSnapshot(model, {
                objectId: 'phase17-object',
                blockId: 'p1'
            }, textSelection);

            const unselectedHtml = hooks.renderWysiwygBodyLayersHtmlForTest({
                id: 'phase17-a11y',
                model,
                selection: textSelection,
                options: { ImageResizeHandleLabel: 'Resize image' }
            }, model.body.blocks);
            const selectedHtml = hooks.renderWysiwygBodyLayersHtmlForTest({
                id: 'phase17-a11y',
                model,
                selection: objectSelection,
                options: { ImageResizeHandleLabel: 'Resize image' }
            }, model.body.blocks, objectSelection);

            assert.ok(unselectedHtml.includes('data-testid="document-wysiwyg-object-layer-item"'), unselectedHtml);
            assert.ok(unselectedHtml.includes('role="img"'), unselectedHtml);
            assert.ok(unselectedHtml.includes('aria-label="Accessible service diagram"'), unselectedHtml);
            assert.ok(unselectedHtml.includes('data-object-focus-policy="selection-only"'), unselectedHtml);
            assert.ok(unselectedHtml.includes('aria-selected="false"'), unselectedHtml);
            assert.strictEqual(/data-testid="document-wysiwyg-object-layer-item"[^>]*tabindex\s*=/.test(unselectedHtml), false, unselectedHtml);
            assert.strictEqual(unselectedHtml.includes('aria-label="Resize image '), false, unselectedHtml);

            assert.ok(selectedHtml.includes('aria-selected="true"'), selectedHtml);
            assert.ok(selectedHtml.includes('data-object-selected="true"'), selectedHtml);
            assert.ok(selectedHtml.includes('aria-describedby="tm-wysiwyg-active-object-status-phase17-a11y"'), selectedHtml);
            assert.strictEqual((selectedHtml.match(/role="button" tabindex="-1" aria-label="Resize image /g) || []).length, 8, selectedHtml);
            assert.ok(selectedHtml.includes('role="group"'), selectedHtml);
            assert.ok(selectedHtml.includes('aria-label="Selected image controls"'), selectedHtml);
            assert.ok(selectedHtml.includes('role="toolbar"'), selectedHtml);

            const statusInst = { id: 'phase17-a11y', model, selection: objectSelection, options: {} };
            const status = hooks.activeObjectAccessibilityStatus(statusInst);
            assert.ok(status.includes('Selected image: Accessible service diagram.'), status);
            assert.ok(status.includes('Wrap mode Square'), status);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_KeyboardModelKeepsTextNavigationExplicitObjectSelectionAndSingleUndoDelete()
    {
        var result = await RunScenarioAsync(
            "keyboard-model",
            """
            const calls = [];
            const root = createRoot();
            const dotNet = {
                invokeMethodAsync(method, payload) {
                    calls.push({ method, payload });
                    return Promise.resolve(true);
                }
            };

            engine.create(root, {
                InstanceId: 'phase17-keyboard',
                ImageResizeHandleLabel: 'Resize image'
            }, dotNet);
            engine.loadDocument('phase17-keyboard', { Document: createWrappedDrawingDocument() });
            const inst = hooks.instances.get('phase17-keyboard');

            assert.ok(root.attributes['aria-keyshortcuts'].includes('Alt+Shift+O'), root.attributes['aria-keyshortcuts']);
            assert.ok(root.attributes['aria-keyshortcuts'].includes('Control+Alt+O'), root.attributes['aria-keyshortcuts']);

            inst.selection = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 3 });
            const arrow = hooks.handleEditorKeyDown(inst, keyEvent('ArrowDown', { target: editableTarget() }));
            assert.strictEqual(selectedObjectId(inst), '', JSON.stringify(arrow));
            assert.strictEqual(inst.selection.selectionMode, 'Text');

            const tab = keyEvent('Tab', { target: editableTarget() });
            const tabResult = hooks.handleEditorKeyDown(inst, tab);
            assert.strictEqual(tabResult.handled, true);
            assert.strictEqual(tab.prevented, true);
            assert.strictEqual(selectedObjectId(inst), '');

            const next = keyEvent('O', { altKey: true, shiftKey: true, target: editableTarget() });
            const selected = hooks.handleEditorKeyDown(inst, next);
            assert.strictEqual(selected.handled, true);
            assert.strictEqual(next.prevented, true);
            assert.strictEqual(selected.result.objectId, 'phase17-object');
            assert.strictEqual(inst.selection.selectionMode, 'Object');
            assert.strictEqual(selectedObjectId(inst), 'phase17-object');
            assert.strictEqual(root.focused, true, 'object selection focus is owned by the editor surface, not by resize handles');

            const escape = hooks.handleEditorKeyDown(inst, keyEvent('Escape', { target: root }));
            assert.strictEqual(escape.handled, true);
            assert.strictEqual(inst.selection.selectionMode, 'Text');
            assert.strictEqual(selectedObjectId(inst), '');
            assert.strictEqual(inst.selection.blockId, 'p1');

            inst.selection = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 6 });
            const beforeTextDeleteDrawingCount = drawingCount(inst.model);
            const backspace = hooks.handleEditorKeyDown(inst, keyEvent('Backspace', { target: editableTarget() }));
            assert.strictEqual(backspace.handled, true);
            assert.strictEqual(drawingCount(inst.model), beforeTextDeleteDrawingCount, 'Backspace in text beside an image must not delete the object');
            engine.applyCommand('phase17-keyboard', 'undo', {});

            hooks.handleEditorKeyDown(inst, keyEvent('O', { altKey: true, shiftKey: true, target: editableTarget() }));
            const undoDepthBeforeObjectDelete = inst.undoTransactions.length;
            const deleteObject = hooks.handleEditorKeyDown(inst, keyEvent('Delete', { target: root }));
            assert.strictEqual(deleteObject.handled, true);
            assert.strictEqual(drawingCount(inst.model), 0, 'Delete on object selection removes the selected object');
            assert.strictEqual(inst.undoTransactions.length, undoDepthBeforeObjectDelete + 1, 'object delete is one undoable operation');
            assert.strictEqual(inst.selection.selectionMode, 'Text');

            const undoObjectDelete = engine.applyCommand('phase17-keyboard', 'undo', {});
            assert.strictEqual(undoObjectDelete.ok, true, JSON.stringify(undoObjectDelete));
            assert.strictEqual(drawingCount(inst.model), 1, 'undo restores the deleted image object');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    private static async Task<ScenarioResult> RunScenarioAsync(string scenario, string nodeScript)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return new ScenarioResult(0, "OK", "");
        var result = await RunNodeAsync(scriptPath, nodeScript, scenario);
        return new ScenarioResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static string GetWysiwygScriptPath()
        => Path.Combine(FindRepositoryRoot(), "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");

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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript,
        string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase17-{scenario}-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, SharedSandboxScript + nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { tempFile, scriptPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed record ScenarioResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public void ShouldPass()
        {
            ExitCode.Should().Be(0, StandardError);
            StandardOutput.Trim().Should().Be("OK");
        }
    }

    private const string SharedSandboxScript =
        """
        const fs = require('fs');
        const vm = require('vm');
        const assert = require('assert');

        function createSandbox() {
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON,
                Date,
                Math,
                Number,
                String,
                Promise,
                Node: { ELEMENT_NODE: 1 }
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.innerWidth = 1280;
            sandbox.window.innerHeight = 800;
            sandbox.window.performance = { now: () => Date.now() };
            sandbox.window.Node = sandbox.Node;
            sandbox.window.getSelection = function () { return null; };
            return sandbox;
        }

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const engine = sandbox.window.tmDocumentEditorEngine;
        const hooks = engine.__testHooks;

        function createRoot() {
            return {
                innerHTML: '',
                attributes: {},
                focused: false,
                nodeType: 1,
                listeners: {},
                classList: { add() {}, toggle() {}, remove() {} },
                focus() { this.focused = true; },
                setAttribute(name, value) { this.attributes[name] = String(value); },
                removeAttribute(name) { delete this.attributes[name]; },
                contains() { return true; },
                addEventListener(name, handler) { this.listeners[name] = handler; },
                removeEventListener(name) { delete this.listeners[name]; },
                querySelector() { return null; },
                querySelectorAll() { return []; },
                getBoundingClientRect() { return { left: 0, top: 0, width: 960, height: 640 }; }
            };
        }

        function keyEvent(key, extra) {
            return Object.assign({
                key,
                ctrlKey: false,
                metaKey: false,
                altKey: false,
                shiftKey: false,
                isComposing: false,
                clientX: 0,
                clientY: 0,
                target: null,
                prevented: false,
                stopped: false,
                preventDefault() { this.prevented = true; },
                stopPropagation() { this.stopped = true; }
            }, extra || {});
        }

        function editableTarget() {
            return {
                nodeType: 1,
                parentElement: null,
                closest(selector) {
                    if (String(selector).includes('figure') || String(selector).includes('tm-wysiwyg-object-layer-item')) {
                        return null;
                    }
                    if (String(selector).includes('.tm-wysiwyg-page__body[contenteditable]')) {
                        return this;
                    }
                    if (String(selector).includes('.tm-wysiwyg-block[data-block-id]')) {
                        return this;
                    }
                    return null;
                }
            };
        }

        function selectedObjectId(inst) {
            const selection = inst.selection || {};
            return String(selection.activeObjectId
                || selection.objectId
                || selection.objectSelection && selection.objectSelection.objectId
                || '');
        }

        function drawingCount(model) {
            const block = model.body.blocks.find(item => item.id === 'p1');
            return (block.content.runs || []).filter(run => run && (run.kind === 'drawing' || run.objectId)).length;
        }

        function createWrappedDrawingDocument() {
            return {
                DocumentId: 'phase17-image-wrap-a11y',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: { Inlines: [
                        { Id: 'before', Text: 'Before wrapped image ' },
                        {
                            $type: 'drawing',
                            Id: 'draw-phase17',
                            ObjectId: 'phase17-object',
                            Kind: 0,
                            Source: 0,
                            Url: '/phase17.png',
                            AltText: 'Accessible service diagram',
                            Caption: 'Accessible service diagram caption',
                            Size: { Width: 150, Height: 90, LockAspectRatio: true },
                            Layout: {
                                Kind: 1,
                                Anchor: { BlockId: 'p1', Offset: 21, InlineIndex: 1, Region: 'Body', MoveWithText: true },
                                Position: { HorizontalRelativeTo: 2, VerticalRelativeTo: 3, X: 180, Y: 32 },
                                Wrap: { Mode: 1, DistanceLeft: 8, DistanceRight: 8, DistanceTop: 4, DistanceBottom: 4 },
                                Transform: { Width: 150, Height: 90, LockAspectRatio: true },
                                Stacking: { ZIndex: 0, AllowOverlap: false }
                            }
                        },
                        { Id: 'after', Text: ' and after wrapped image text.' }
                    ] }
                }]
            };
        }

        """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
