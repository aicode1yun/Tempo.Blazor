using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase21AccessibilityJavaScriptTests
{
    [Fact]
    public async Task Phase21_WysiwygScript_PassesNodeSyntaxCheck()
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
    public async Task Phase21_RenderedSurface_ExposesContentEditableAria()
    {
        var scriptPath = GetWysiwygScriptPath();
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
                JSON,
                Date,
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.innerWidth = 1280;
            sandbox.window.innerHeight = 800;
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
                    querySelectorAll() { return []; },
                    getBoundingClientRect() { return { left: 0, top: 0, width: 800, height: 600 }; }
                };
            }

            (async () => {
            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, {
                InstanceId: 'phase21-render',
                PageLabel: 'Page {0}',
                BodyLabel: 'Document body, page {0}',
                ImageAltMissing: 'Add alt text for accessibility.'
            }, null);
            engine.loadDocument('phase21-render', {
                Document: {
                    DocumentId: 'phase21-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Hello' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Type: 'Image', Caption: 'Evidence preview' } },
                        { Id: 'tbl1', Type: 'Table', Content: { Type: 'Table', Rows: [
                            { Id: 'row1', Cells: [{ Id: 'cell1', Blocks: [{ Id: 'cp1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'cr1', Text: 'Cell' }] } }] }] }
                        ] } }
                    ]
                }
            });

            assert.ok(root.innerHTML.includes('role="document"'));
            assert.ok(root.innerHTML.includes('role="status"'));
            assert.ok(root.innerHTML.includes('aria-label="Page 1"'));
            assert.ok(root.innerHTML.includes('aria-label="Document body, page 1"'));
            assert.ok(root.innerHTML.includes('role="figure"'));
            assert.ok(root.innerHTML.includes('aria-describedby="tm-wysiwyg-image-alt-warning-img1"'));
            assert.ok(root.innerHTML.includes('Add alt text for accessibility.'));
            assert.ok(root.innerHTML.includes('role="table"'));
            assert.ok(root.innerHTML.includes('role="gridcell"'));
            assert.strictEqual(root.attributes['aria-keyshortcuts'].includes('Control+B'), true);

            console.log('OK');
            })().catch(error => {
                console.error(error && error.stack || error);
                process.exit(1);
            });
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase21_FocusAndKeyboardModel_RoutesCommandsThroughAccessibleOwner()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const calls = [];
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON,
                Date,
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.innerWidth = 1280;
            sandbox.window.innerHeight = 800;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    listeners: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    contains() { return true; },
                    addEventListener(name, handler) { this.listeners[name] = handler; },
                    removeEventListener(name) { delete this.listeners[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; },
                    getBoundingClientRect() { return { left: 10, top: 20, width: 800, height: 600 }; }
                };
            }

            function keyEvent(key, extra = {}) {
                return Object.assign({
                    key,
                    ctrlKey: false,
                    metaKey: false,
                    altKey: false,
                    shiftKey: false,
                    clientX: 0,
                    clientY: 0,
                    prevented: false,
                    stopped: false,
                    preventDefault() { this.prevented = true; },
                    stopPropagation() { this.stopped = true; }
                }, extra);
            }

            (async () => {
            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            const dotNet = {
                invokeMethodAsync(method, payload) {
                    calls.push({ method, payload });
                    return Promise.resolve();
                }
            };
            engine.create(root, { InstanceId: 'phase21-keyboard' }, dotNet);
            engine.loadDocument('phase21-keyboard', {
                Document: {
                    DocumentId: 'phase21-keyboard-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Hello' }] } }]
                }
            });

            const hooks = engine.__testHooks;
            const inst = hooks.instances.get('phase21-keyboard');
            inst.model.headersFooters = [{ id: 'h1', type: 'Header' }, { id: 'f1', type: 'Footer' }];

            const cell = { getAttribute: name => name === 'data-cell-id' ? 'cell1' : '' };
            const cellTarget = {
                nodeType: 1,
                parentElement: null,
                closest(selector) {
                    return selector.includes('[data-cell-id]') || selector.includes('td[data-cell-id]') ? cell : null;
                }
            };
            hooks.setActiveFocusRegion(inst, hooks.getFocusRegionFromElement(root, cellTarget), cellTarget, 'test-cell');
            assert.strictEqual(root.attributes['data-active-region'], 'TableCell');
            assert.strictEqual(root.attributes['data-focus-owner'], 'tablecell');

            const tab = keyEvent('Tab');
            const tabResult = hooks.handleEditorKeyDown(inst, tab);
            assert.strictEqual(tabResult.handled, true);
            assert.strictEqual(tab.prevented, true);
            assert.strictEqual(root.attributes['data-active-region'], 'Body');
            const tabToFooter = hooks.handleEditorKeyDown(inst, keyEvent('Tab'));
            assert.strictEqual(tabToFooter.handled, true);
            assert.strictEqual(root.attributes['data-active-region'], 'Footer');

            hooks.handleEditorKeyDown(inst, keyEvent('s', { ctrlKey: true }));
            const boldEvent = keyEvent('b', { ctrlKey: true });
            const boldResult = hooks.handleEditorKeyDown(inst, boldEvent);
            assert.strictEqual(boldResult.handled, true);
            assert.strictEqual(boldEvent.prevented, true);
            assert.ok(inst.commands.some(command => command.command === 'bold' && command.source === 'keyboard'));
            hooks.handleEditorKeyDown(inst, keyEvent('F10', { shiftKey: true }));
            hooks.handleEditorKeyDown(inst, keyEvent('Escape'));
            await new Promise(resolve => setTimeout(resolve, 200));

            assert.ok(calls.some(call => call.method === 'HandleSaveRequested'));
            assert.ok(calls.some(call => call.method === 'HandleTextContextMenuRequested'));
            assert.ok(calls.some(call => call.method === 'HandleMiniToolbarChanged'));
            assert.ok(calls.some(call => call.method === 'HandleAccessibilityAnnouncement'));

            console.log('OK');
            })().catch(error => {
                console.error(error && error.stack || error);
                process.exit(1);
            });
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase21_RevisionPopover_ProvidesAccessibleDialogMetadata()
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

            const engine = sandbox.window.tmDocumentEditorEngine;
            const model = engine.model.importFromCSharpJson({
                DocumentId: 'phase21-revision',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                    { Id: 'r1', Text: 'Alpha ' },
                    { Id: 'r2', Text: 'new', RevisionId: 'rev-ins' }
                ] } }],
                Revisions: [{
                    Id: 'rev-ins',
                    Type: 'Insertion',
                    Author: 'u1',
                    Timestamp: 1,
                    AffectedRange: { BlockId: 'p1', Start: 6, End: 9 },
                    Payload: { text: 'new' },
                    Status: 'Pending'
                }]
            });
            const revisions = engine.revisions.createRevisionEngine(model);
            const popover = revisions.createReviewPopover('rev-ins');

            assert.strictEqual(popover.role, 'dialog');
            assert.strictEqual(popover.ariaModal, false);
            assert.strictEqual(popover.ariaLabel, 'Review Insertion revision');
            assert.ok(popover.actions.some(action => action.id === 'accept' && action.ariaLabel === 'Accept revision'));
            assert.ok(popover.actions.some(action => action.id === 'reject' && action.ariaLabel === 'Reject revision'));

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase21-{Guid.NewGuid():N}.js");
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
