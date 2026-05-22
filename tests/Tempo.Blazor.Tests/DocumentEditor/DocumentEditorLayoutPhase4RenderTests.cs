using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorLayoutPhase4RenderTests
{
    [Fact]
    public async Task Phase4_LayoutSnapshot_RendersObjectBoxesAndTextLineBoxesWithoutSidecar()
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
                Math,
                Number,
                String,
                parseFloat,
                parseInt
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            hooks.clearTextRunMeasureCache();

            const documentModel = {
                DocumentId: 'phase-4-layout-render',
                PageSettings: {
                    Size: { Width: 595, Height: 842 },
                    Margins: { Top: 72, Right: 72, Bottom: 72, Left: 72 }
                },
                Theme: {
                    BodyFontFamily: 'Arial',
                    BodyFontSize: 11,
                    BodyLineHeight: 1.15,
                    ParagraphSpacingAfter: 0
                },
                Blocks: [
                    {
                        Id: 'img-left-square',
                        Type: 5,
                        Order: 10,
                        Content: {
                            $type: 'image',
                            Url: 'data:image/png;base64,iVBORw0KGgo=',
                            Size: { Width: 160, Height: 90 },
                            Layout: {
                                Kind: 1,
                                Position: { HorizontalAlignment: 0, X: 0, Y: 0 },
                                Wrap: { Mode: 1, DistanceRight: 12, DistanceBottom: 8 },
                                Transform: { Width: 160, Height: 90 },
                                Stacking: { ZIndex: 3 }
                            }
                        }
                    },
                    {
                        Id: 'paragraph-after-image',
                        Type: 0,
                        Order: 20,
                        Content: {
                            $type: 'paragraph',
                            Inlines: [{
                                $type: 'text',
                                Id: 'inline-text',
                                Text: 'The first line must be placed beside the left wrapped image. The next lines can continue below it, but none may intersect the image wrap rectangle.'
                            }]
                        }
                    }
                ]
            };

            const snapshot = hooks.createLayoutSnapshotForRender(documentModel);
            const page = snapshot.Pages[0];
            assert.ok(page, 'layout snapshot contains a page');
            assert.strictEqual(page.Objects.length, 1, 'image is rendered as one layout object');

            const object = page.Objects[0];
            assert.strictEqual(object.BlockId, 'img-left-square');
            assert.strictEqual(object.WrapMode, 1);
            assert.strictEqual(object.WrapModeCss, 'square');
            assert.strictEqual(object.Layer, 'object');
            assert.strictEqual(object.DataAttributes['data-layout-object-id'], 'layout-object-img-left-square');
            assert.strictEqual(object.DataAttributes['data-wrap-mode'], '1');
            assert.strictEqual(object.DataAttributes['data-anchor-block-id'], 'img-left-square');
            assert.strictEqual(object.DataAttributes['data-object-z-index'], '3');

            const lines = page.Lines.filter(line => line.BlockId === 'paragraph-after-image');
            assert.ok(lines.length >= 2, 'paragraph is split into layout lines');
            assert.ok(lines.every(line => line.Id && line.Id.startsWith('layout-line-')), 'each line has a stable layout id');
            assert.ok(lines.every(line => line.Segments.length > 0), 'each text line has segments');
            assert.ok(lines.every(line => line.Segments.every(segment => segment.Id && segment.LineId === line.Id && segment.InlineId === 'inline-text')), 'segments carry line and inline ids');

            const firstLine = lines[0];
            assert.ok(firstLine.Rect.X >= object.WrapRect.X + object.WrapRect.Width - 0.1, 'first line starts to the right of the wrapped image');
            const intersects = (a, b) => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
            assert.strictEqual(lines.some(line => intersects(line.Rect, object.WrapRect)), false, 'text line boxes must not intersect the image wrap rect');

            const twoImageDocument = JSON.parse(JSON.stringify(documentModel));
            twoImageDocument.Blocks.splice(1, 0, {
                Id: 'img-left-square-2',
                Type: 5,
                Order: 15,
                Content: {
                    $type: 'image',
                    Url: 'data:image/png;base64,iVBORw0KGgo=',
                    Size: { Width: 140, Height: 80 },
                    Layout: {
                        Kind: 1,
                        Position: { HorizontalAlignment: 0, X: 0, Y: 0 },
                        Wrap: { Mode: 1, DistanceRight: 12, DistanceBottom: 8 },
                        Transform: { Width: 140, Height: 80 },
                        Stacking: { ZIndex: 4 }
                    }
                }
            });
            const twoImageSnapshot = hooks.createLayoutSnapshotForRender(twoImageDocument);
            const twoObjects = twoImageSnapshot.Pages[0].Objects;
            assert.strictEqual(twoObjects.length, 2, 'two adjacent image blocks render as two layout objects');
            assert.ok(twoObjects[1].Rect.Y >= twoObjects[0].WrapRect.Y + twoObjects[0].WrapRect.Height - 0.1, 'second default wrapped image is cascaded below the first instead of overlapping it');

            const contractLikeDocument = JSON.parse(JSON.stringify(documentModel));
            contractLikeDocument.DocumentId = 'phase-4-contract-like-overlap-regression';
            contractLikeDocument.Blocks = [
                {
                    Id: 'contract-title',
                    Type: 0,
                    Order: 1,
                    Content: {
                        $type: 'paragraph',
                        Inlines: [{ $type: 'text', Id: 'title-inline', Text: 'Service agreement' }]
                    }
                },
                {
                    Id: 'contract-wrapped-image',
                    Type: 5,
                    Order: 2,
                    Content: {
                        $type: 'image',
                        Url: 'data:image/png;base64,iVBORw0KGgo=',
                        Caption: 'Image loaded from favicon resolver',
                        Size: { Width: 160, Height: 90 },
                        Layout: {
                            Kind: 1,
                            Position: { HorizontalAlignment: 0, X: 0, Y: 0 },
                            Wrap: { Mode: 1, DistanceRight: 12, DistanceBottom: 8 },
                            Transform: { Width: 160, Height: 90 },
                            Stacking: { ZIndex: 3 }
                        }
                    }
                },
                {
                    Id: 'contract-wrapped-text',
                    Type: 0,
                    Order: 3,
                    Content: {
                        $type: 'paragraph',
                        Inlines: [{
                            $type: 'text',
                            Id: 'wrapped-text-inline',
                            Text: 'This longer clause demonstrates live text wrapping around the evidence image. Users can continue typing beside the image, resize it, and the paragraph must reflow as one editable paragraph.'
                        }]
                    }
                },
                {
                    Id: 'contract-inline-image',
                    Type: 5,
                    Order: 4,
                    Content: {
                        $type: 'image',
                        Url: 'data:image/png;base64,iVBORw0KGgo=',
                        Caption: 'Inline provider image caption',
                        Size: { Width: 180, Height: 100 },
                        Layout: {
                            Kind: 0,
                            Wrap: { Mode: 0 },
                            Transform: { Width: 180, Height: 100 }
                        }
                    }
                }
            ];
            const contractSnapshot = hooks.createLayoutSnapshotForRender(contractLikeDocument);
            const contractPage = contractSnapshot.Pages[0];
            const wrappedObject = contractPage.Objects.find(obj => obj.BlockId === 'contract-wrapped-image');
            const inlineObject = contractPage.Objects.find(obj => obj.BlockId === 'contract-inline-image');
            assert.ok(wrappedObject.FootprintRect.Height > wrappedObject.Rect.Height, 'wrapped image footprint includes caption height');
            assert.ok(wrappedObject.WrapRect.Height > wrappedObject.Rect.Height, 'wrapped image wrap rect includes caption footprint');
            assert.strictEqual(intersects(inlineObject.FootprintRect, wrappedObject.WrapRect), false, 'following inline image footprint must not intersect active wrapped image wrap rect');
            assert.strictEqual(
                contractPage.Lines
                    .filter(line => line.BlockId === 'contract-wrapped-text')
                    .some(line => intersects(line.Rect, wrappedObject.WrapRect)),
                false,
                'wrapped text line boxes must not intersect the wrapped image footprint');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public void Phase4_CssDefinesLayeredRenderingAndDisablesFloatForSnapshotMode()
    {
        var css = File.ReadAllText(GetDocumentEditorCssPath());
        var script = File.ReadAllText(GetWysiwygScriptPath());

        css.Should().Contain(".tm-wysiwyg-page__layer--body-text");
        css.Should().Contain(".tm-wysiwyg-page__layer--behind-text");
        css.Should().Contain(".tm-wysiwyg-page__layer--object");
        css.Should().Contain(".tm-wysiwyg-page__layer--in-front-of-text");
        css.Should().Contain(".tm-wysiwyg-page__layer--selection");
        css.Should().Contain(".tm-wysiwyg-page__layer--guides");
        css.Should().NotContain(".tm-wysiwyg-image-sidecar-text");
        css.Should().NotContain("data-wrap-sidecar-for");
        css.Should().Contain(".tm-wysiwyg-layout-object");
        css.Should().Contain(".tm-wysiwyg-selection-box");
        css.Should().Contain(".tm-wysiwyg-object-resize-handle--nw");
        css.Should().Contain(".tm-wysiwyg-object-resize-handle--se");
        css.Should().Contain(".tm-wysiwyg-object-rotation-handle");
        css.Should().Contain(".tm-wysiwyg-anchor-glyph");
        css.Should().Contain(".tm-wysiwyg-layout-bubble");
        css.Should().Contain(".tm-wysiwyg-guide-line");

        script.Should().Contain("tm-wysiwyg-host--layout-snapshot");
        script.Should().Contain("data-layout-line-id");
        script.Should().Contain("data-layout-segment-id");
        script.Should().Contain("data-layout-object-id");
        script.Should().Contain("data-anchor-block-id");
        script.Should().Contain("data-object-z-index");
    }

    private static string GetWysiwygScriptPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
    }

    private static string GetDocumentEditorCssPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css");
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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
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
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }
}
