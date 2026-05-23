using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict browser tests for document editor UX polish contracts.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase23E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_UxPolishContractsWorkInBrowserDom()
    {
        var page = await OpenDocumentEditorAsync(1280, 900);

        var result = await page.EvaluateAsync<Phase23UxPolishProbe>(
            """
            () => {
                const host = document.createElement('div');
                host.setAttribute('data-testid', 'phase23-ux-host');
                host.style.cssText = 'position:absolute;left:24px;top:24px;width:760px;min-height:520px;background:white;';
                document.body.appendChild(host);

                const engine = window.tmDocumentEditorEngine;
                const instanceId = engine.create(host, { InstanceId: 'phase23-e2e' }, null);
                engine.loadDocument(instanceId, {
                    Document: {
                        DocumentId: 'phase23-e2e-doc',
                        Blocks: [
                            { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Hello', Marks: [{ Type: 'Bold' }] }] } },
                            { Id: 'img1', Type: 'Image', Content: { Type: 'Image', Id: 'img-object', AltText: 'Browser image', Caption: 'Browser caption' } }
                        ],
                        Comments: [{ Id: 'comment1', Range: { BlockId: 'p1', Start: 0, End: 5 } }],
                        Revisions: [{ Id: 'rev1', Type: 'Insertion', Status: 'Pending', AffectedRange: { BlockId: 'p1', Start: 0, End: 5 } }]
                    }
                });

                const tracker = engine.uxPolish.createVisualStabilityTracker({ maxToolbarDelta: 1 });
                const stable = tracker.record(
                    { paragraphKey: 'p1', pageKey: 'page1', toolbarTop: 80, selectionRelevant: true, floatingOpen: true, commandValue: true },
                    { paragraphKey: 'p1', pageKey: 'page1', toolbarTop: 80, selectionRelevant: true, floatingOpen: true, commandValue: true },
                    'typing');
                const text = engine.uxPolish.previewImmediateTextEdit({
                    text: 'HelloWorld',
                    selection: { blockId: 'p1', offset: 5 },
                    inputType: 'insertText',
                    data: ' '
                });
                const chrome = engine.uxPolish.createObjectChromeModel({
                    objectRect: { X: 520, Y: 160, Width: 220, Height: 124 },
                    captionRect: { X: 520, Y: 290, Width: 220, Height: 24 },
                    toolbarSize: { Width: 288, Height: 34 },
                    viewport: { X: 0, Y: 0, Width: 1280, Height: 900 },
                    sidePanelRect: { X: 900, Y: 0, Width: 320, Height: 900 }
                });

                engine.restoreSelection(instanceId, { blockId: 'p1', offset: 2, isCollapsed: true });
                const paragraphPanel = engine.getSidePanelSyncState(instanceId);
                engine.restoreSelection(instanceId, { blockId: 'img1', offset: 0, isObjectSelection: true, objectId: 'img-object' });
                const imagePanel = engine.getSidePanelSyncState(instanceId);
                const selectedImage = host.querySelector('.tm-wysiwyg-image--selected');
                const handleCount = host.querySelectorAll('[data-testid^="document-wysiwyg-object-resize-handle-"]').length;
                const hasBubble = !!host.querySelector('[data-testid="document-wysiwyg-object-layout-bubble"], [data-testid="document-wysiwyg-layout-bubble"]');
                const dispose = engine.dispose(instanceId);
                host.remove();

                return {
                    stableOk: stable.ok === true,
                    spaceVisible: text.spaceVisibleImmediately === true && text.visibleText === 'Hello World',
                    chromeReadable: chrome.selectionOutline.clean === true && chrome.allHandlesLargeEnough === true && chrome.handlesAvoidCaption === true,
                    chromeAvoidsSidePanel: chrome.toolbar.avoidsSidePanel === true,
                    paragraphBold: paragraphPanel.properties?.formatting?.bold === true,
                    activeRevisionId: paragraphPanel.revision?.activeRevisionIds?.[0] || '',
                    activeCommentId: paragraphPanel.comments?.activeCommentIds?.[0] || '',
                    imageBlockId: imagePanel.image?.blockId || '',
                    selectedImageRendered: !!selectedImage,
                    handleCount,
                    hasBubble,
                    disposed: dispose.ok === true
                };
            }
            """);

        result.StableOk.Should().BeTrue();
        result.SpaceVisible.Should().BeTrue();
        result.ChromeReadable.Should().BeTrue();
        result.ChromeAvoidsSidePanel.Should().BeTrue();
        result.ParagraphBold.Should().BeTrue();
        result.ActiveRevisionId.Should().Be("rev1");
        result.ActiveCommentId.Should().Be("comment1");
        result.ImageBlockId.Should().Be("img1");
        result.SelectedImageRendered.Should().BeTrue();
        result.HandleCount.Should().Be(8);
        result.HasBubble.Should().BeTrue();
        result.Disposed.Should().BeTrue();
    }

    private sealed class Phase23UxPolishProbe
    {
        [JsonPropertyName("stableOk")] public bool StableOk { get; set; }
        [JsonPropertyName("spaceVisible")] public bool SpaceVisible { get; set; }
        [JsonPropertyName("chromeReadable")] public bool ChromeReadable { get; set; }
        [JsonPropertyName("chromeAvoidsSidePanel")] public bool ChromeAvoidsSidePanel { get; set; }
        [JsonPropertyName("paragraphBold")] public bool ParagraphBold { get; set; }
        [JsonPropertyName("activeRevisionId")] public string ActiveRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("activeCommentId")] public string ActiveCommentId { get; set; } = string.Empty;
        [JsonPropertyName("imageBlockId")] public string ImageBlockId { get; set; } = string.Empty;
        [JsonPropertyName("selectedImageRendered")] public bool SelectedImageRendered { get; set; }
        [JsonPropertyName("handleCount")] public int HandleCount { get; set; }
        [JsonPropertyName("hasBubble")] public bool HasBubble { get; set; }
        [JsonPropertyName("disposed")] public bool Disposed { get; set; }
    }
}
