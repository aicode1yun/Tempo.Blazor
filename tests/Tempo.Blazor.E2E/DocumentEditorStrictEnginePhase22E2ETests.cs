using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict E2E tests for document editor performance and virtualization contracts.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase22E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_VirtualizesLongDocumentAndMaterializesCaretPage()
    {
        var page = await OpenDocumentEditorAsync(1280, 900);

        var result = await page.EvaluateAsync<Phase22VirtualizationProbe>(
            """
            () => {
                const sandbox = document.createElement('div');
                sandbox.setAttribute('data-testid', 'phase22-virtual-host');
                sandbox.style.cssText = 'position:absolute;left:0;top:0;width:760px;height:620px;overflow:auto;background:white;z-index:-1;';
                document.body.appendChild(sandbox);

                const engine = window.tmDocumentEditorEngine;
                const instanceId = engine.create(sandbox, {
                    InstanceId: 'phase22-e2e-virtual',
                    VirtualizationBlocksPerPage: 1,
                    VirtualizationThresholdPages: 10,
                    VirtualizationRenderedPageRadius: 0
                }, null);

                engine.loadDocument(instanceId, {
                    Document: {
                        DocumentId: 'phase22-e2e-doc',
                        Blocks: Array.from({ length: 100 }, (_, index) => ({
                            Id: `p${index}`,
                            Type: 'Paragraph',
                            Content: {
                                Type: 'Paragraph',
                                Inlines: [{ Id: `r${index}`, Text: `Virtualized page paragraph ${index}` }]
                            }
                        }))
                    }
                });

                const before = engine.getPageMetrics(instanceId);
                const renderedBlockCountBefore = sandbox.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id]').length;
                const placeholderCountBefore = sandbox.querySelectorAll('[data-testid="document-wysiwyg-virtual-page"]').length;

                const scrollPage = engine.scrollToPage(instanceId, 50);
                const afterScroll = engine.getPageMetrics(instanceId);
                const pageFiftyRendered = !!sandbox.querySelector('.tm-wysiwyg-page[data-page-index="50"]:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id="p50"]');
                const pageZeroVirtualAfterScroll = sandbox.querySelector('.tm-wysiwyg-page[data-page-index="0"]')?.classList.contains('tm-wysiwyg-page--virtual') === true;

                const selectionResult = engine.restoreSelection(instanceId, {
                    anchor: { region: 'Body', blockId: 'p75', offset: 2 },
                    focus: { region: 'Body', blockId: 'p75', offset: 2 },
                    isCollapsed: true
                });
                const scrollBlock = engine.scrollToBlock(instanceId, 'p75');
                const afterSelection = engine.getPageMetrics(instanceId);
                const selectedBlockRendered = !!sandbox.querySelector('.tm-wysiwyg-page[data-page-index="75"]:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id="p75"]');
                const selection = engine.getSelectionSnapshot(instanceId);
                const debug = engine.getDebugMetrics(instanceId);
                const dispose = engine.dispose(instanceId);
                sandbox.remove();

                return {
                    instanceId,
                    totalPages: before.TotalPages,
                    virtualizationEnabled: before.VirtualizationEnabled === true,
                    activePageBefore: before.ActivePageIndex,
                    virtualizedPagesBefore: before.VirtualizedPages,
                    renderedBlockCountBefore,
                    placeholderCountBefore,
                    scrollPageOk: scrollPage.ok === true,
                    activePageAfterScroll: afterScroll.ActivePageIndex,
                    pageFiftyRendered,
                    pageZeroVirtualAfterScroll,
                    selectionOk: selectionResult.ok === true,
                    scrollBlockOk: scrollBlock.ok === true,
                    activePageAfterSelection: afterSelection.ActivePageIndex,
                    selectedBlockRendered,
                    selectedBlockId: selection.blockId || selection.BlockId || '',
                    selectedOffset: selection.offset ?? selection.Offset ?? -1,
                    maxLiveDomBlockCount: debug.MaxLiveDomBlockCount || 0,
                    disposeOk: dispose.ok === true && dispose.cleanup?.instanceRemoved === true
                };
            }
            """);

        result.InstanceId.Should().Be("phase22-e2e-virtual");
        result.TotalPages.Should().Be(100);
        result.VirtualizationEnabled.Should().BeTrue();
        result.ActivePageBefore.Should().Be(0);
        result.VirtualizedPagesBefore.Should().BeGreaterThan(90);
        result.RenderedBlockCountBefore.Should().Be(1);
        result.PlaceholderCountBefore.Should().BeGreaterThan(90);
        result.ScrollPageOk.Should().BeTrue();
        result.ActivePageAfterScroll.Should().Be(50);
        result.PageFiftyRendered.Should().BeTrue();
        result.PageZeroVirtualAfterScroll.Should().BeTrue();
        result.SelectionOk.Should().BeTrue();
        result.ScrollBlockOk.Should().BeTrue();
        result.ActivePageAfterSelection.Should().Be(75);
        result.SelectedBlockRendered.Should().BeTrue();
        result.SelectedBlockId.Should().Be("p75");
        result.SelectedOffset.Should().Be(2);
        result.MaxLiveDomBlockCount.Should().BeLessThanOrEqualTo(1);
        result.DisposeOk.Should().BeTrue();
    }

    private sealed class Phase22VirtualizationProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("totalPages")] public int TotalPages { get; set; }
        [JsonPropertyName("virtualizationEnabled")] public bool VirtualizationEnabled { get; set; }
        [JsonPropertyName("activePageBefore")] public int ActivePageBefore { get; set; }
        [JsonPropertyName("virtualizedPagesBefore")] public int VirtualizedPagesBefore { get; set; }
        [JsonPropertyName("renderedBlockCountBefore")] public int RenderedBlockCountBefore { get; set; }
        [JsonPropertyName("placeholderCountBefore")] public int PlaceholderCountBefore { get; set; }
        [JsonPropertyName("scrollPageOk")] public bool ScrollPageOk { get; set; }
        [JsonPropertyName("activePageAfterScroll")] public int ActivePageAfterScroll { get; set; }
        [JsonPropertyName("pageFiftyRendered")] public bool PageFiftyRendered { get; set; }
        [JsonPropertyName("pageZeroVirtualAfterScroll")] public bool PageZeroVirtualAfterScroll { get; set; }
        [JsonPropertyName("selectionOk")] public bool SelectionOk { get; set; }
        [JsonPropertyName("scrollBlockOk")] public bool ScrollBlockOk { get; set; }
        [JsonPropertyName("activePageAfterSelection")] public int ActivePageAfterSelection { get; set; }
        [JsonPropertyName("selectedBlockRendered")] public bool SelectedBlockRendered { get; set; }
        [JsonPropertyName("selectedBlockId")] public string SelectedBlockId { get; set; } = string.Empty;
        [JsonPropertyName("selectedOffset")] public int SelectedOffset { get; set; }
        [JsonPropertyName("maxLiveDomBlockCount")] public int MaxLiveDomBlockCount { get; set; }
        [JsonPropertyName("disposeOk")] public bool DisposeOk { get; set; }
    }
}
