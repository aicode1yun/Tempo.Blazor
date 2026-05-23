using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase23UxPolishJavaScriptTests
{
    [Fact]
    public async Task Phase23_WysiwygScript_PassesNodeSyntaxCheck()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "--check", scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        process.ExitCode.Should().Be(0, stdout + stderr);
    }

    [Fact]
    public async Task Phase23_VisualStability_KeepsParagraphPageToolbarFloatingAndCommandStateStable()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const tracker = sandbox.window.tmDocumentEditorEngine.__testHooks.createVisualStabilityTracker({ maxToolbarDelta: 1 });
            const typing = tracker.record({
                paragraphKey: 'p1:stable',
                pageKey: 'page:1',
                toolbarTop: 120,
                selectionRelevant: true,
                floatingOpen: true,
                commandValue: true
            }, {
                paragraphKey: 'p1:stable',
                pageKey: 'page:1',
                toolbarTop: 120.5,
                selectionRelevant: true,
                floatingOpen: true,
                commandValue: true
            }, 'typing');
            const command = tracker.record({
                paragraphKey: 'p1:stable',
                pageKey: 'page:1',
                toolbarTop: 120,
                selectionRelevant: true,
                floatingOpen: true,
                commandValue: 'justify'
            }, {
                paragraphKey: 'p1:stable',
                pageKey: 'page:1',
                toolbarTop: 120,
                selectionRelevant: true,
                floatingOpen: true,
                commandValue: 'justify'
            }, 'command');
            const snapshot = tracker.snapshot();

            assert.strictEqual(typing.ok, true);
            assert.strictEqual(typing.paragraphStable, true);
            assert.strictEqual(typing.pageStable, true);
            assert.strictEqual(typing.floatingToolbarStable, true);
            assert.strictEqual(command.commandStateStable, true);
            assert.strictEqual(snapshot.ok, true);
            assert.strictEqual(snapshot.frameCount, 2);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase23_ObjectChrome_ComputesReadableImageUiAwayFromSidePanel()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const chrome = sandbox.window.tmDocumentEditorEngine.__testHooks.createObjectChromeModel({
                objectRect: { X: 600, Y: 160, Width: 220, Height: 124 },
                captionRect: { X: 600, Y: 290, Width: 220, Height: 24 },
                toolbarSize: { Width: 288, Height: 34 },
                viewport: { X: 0, Y: 0, Width: 1024, Height: 720 },
                sidePanelRect: { X: 760, Y: 0, Width: 264, Height: 720 }
            });

            assert.strictEqual(chrome.selectionOutline.clean, true);
            assert.ok(chrome.selectionOutline.width >= 2);
            assert.strictEqual(chrome.handles.length, 8);
            assert.strictEqual(chrome.allHandlesLargeEnough, true);
            assert.strictEqual(chrome.handlesAvoidCaption, true);
            assert.strictEqual(chrome.toolbar.avoidsSidePanel, true);
            assert.strictEqual(chrome.layoutBubble.compact, true);
            assert.strictEqual(chrome.selectionPane.accessible, true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase23_TextFeelAndSidePanelSync_AreImmediateAndSelectionDriven()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const space = hooks.previewImmediateTextEdit({ text: 'HelloWorld', selection: { blockId: 'p1', offset: 5 }, inputType: 'insertText', data: ' ' });
            const enter = hooks.previewImmediateTextEdit({ text: 'HelloWorld', selection: { blockId: 'p1', offset: 5 }, inputType: 'insertParagraph' });
            const merge = hooks.previewImmediateTextEdit({ previousText: 'Alpha ', text: 'Beta', selection: { blockId: 'p2', offset: 0 }, inputType: 'deleteContentBackward' });
            const longWord = hooks.previewImmediateTextEdit({ text: 'abcdefghijabcdefghijabcdefghij', selection: { blockId: 'p1', offset: 30 }, inputType: 'insertText', data: 'k', maxLineChars: 10 });
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase23',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello', Marks: [{ Type: 'Bold' }] }] } },
                    { Id: 'img1', Type: 'Image', Content: { Id: 'img-object', AltText: 'Evidence', Caption: 'Caption', Layout: { Wrap: { Mode: 'Square' } } } }
                ],
                Comments: [{ Id: 'comment1', Range: { BlockId: 'p1', Start: 0, End: 5 } }],
                Revisions: [{ Id: 'rev1', Type: 'Insertion', Status: 'Pending', AffectedRange: { BlockId: 'p1', Start: 0, End: 5 } }]
            });
            const sidePanel = hooks.createSidePanelSyncState(model, { blockId: 'p1', offset: 2, isCollapsed: true });
            const imagePanel = hooks.createSidePanelSyncState(model, { blockId: 'img1', offset: 0, isObjectSelection: true, objectId: 'img-object' });
            const debouncer = hooks.createPanelCommandDebouncer({ debounceMs: 120 });
            const queued = debouncer.queue('ImageAltText', { blockId: 'img1', value: 'New alt' });
            const flushed = debouncer.flush();

            assert.strictEqual(space.spaceVisibleImmediately, true);
            assert.strictEqual(space.visibleText, 'Hello World');
            assert.strictEqual(enter.enterStableImmediately, true);
            assert.strictEqual(enter.visibleText.split('\n').length, 2);
            assert.strictEqual(enter.visibleText.split('\n')[0], 'Hello');
            assert.strictEqual(enter.visibleText.split('\n')[1], 'World');
            assert.strictEqual(merge.backspaceMergeImmediate, true);
            assert.strictEqual(merge.visibleText, 'Alpha Beta');
            assert.strictEqual(longWord.longWordPredictable, true);
            assert.strictEqual(sidePanel.source, 'runtime-selection');
            assert.strictEqual(sidePanel.properties.formatting.bold, true);
            assert.strictEqual(sidePanel.revision.activeRevisionIds[0], 'rev1');
            assert.strictEqual(sidePanel.comments.activeCommentIds[0], 'comment1');
            assert.strictEqual(imagePanel.image.blockId, 'img1');
            assert.strictEqual(queued.livePreview, true);
            assert.strictEqual(queued.waitsForBlur, false);
            assert.strictEqual(flushed.appliedViaCommands, true);
            assert.strictEqual(flushed.count, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase23_RuntimeInstance_RendersSelectedImageChromeAndPanelState()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    contains() { return true; },
                    addEventListener() {},
                    removeEventListener() {},
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, { InstanceId: 'phase23-runtime' }, null);
            engine.loadDocument('phase23-runtime', {
                Document: {
                    DocumentId: 'phase23-runtime-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Text' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Type: 'Image', Id: 'img-object', AltText: 'Runtime image', Caption: 'Runtime caption' } }
                    ]
                }
            });
            engine.restoreSelection('phase23-runtime', { blockId: 'img1', offset: 0, isObjectSelection: true, objectId: 'img-object' });
            const panel = engine.getSidePanelSyncState('phase23-runtime');
            const html = root.innerHTML;
            engine.dispose('phase23-runtime');

            assert.strictEqual(panel.ok, true);
            assert.strictEqual(panel.source, 'runtime-selection');
            assert.strictEqual(panel.image.blockId, 'img1');
            assert.ok(html.includes('tm-wysiwyg-image--selected'));
            assert.ok(html.includes('data-testid="document-wysiwyg-object-layout-bubble"') || html.includes('data-testid="document-wysiwyg-layout-bubble"'));
            assert.ok(html.includes('document-wysiwyg-object-resize-handle-nw'));
            assert.ok(html.includes('document-wysiwyg-object-resize-handle-se'));

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static string GetWysiwygScriptPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
    }

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

        throw new InvalidOperationException("Repository root was not found.");
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
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase23-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
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
            File.Delete(tempFile);
        }
    }
}
