using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class DocumentEditorPhase7MarkerStoreE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase7_FindPanelPublishesSearchMarkersToRuntimeStore()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+f");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-find-input']").FillAsync("the");

        await Assertions.Expect(body.Locator("[data-testid='document-search-marker']")).Not.ToHaveCountAsync(0);
        await Assertions.Expect(body.Locator("[data-testid='document-search-marker-active']")).ToHaveCountAsync(1);

        var state = await ReadMarkerStoreStateAsync(page);
        Assert.IsTrue(state.HasSearchMarker, "Search markers should be stored in the runtime marker store.");
        Assert.IsTrue(state.HasActiveSearchMarker, "The active search result should be a first-class marker.");
        Assert.IsTrue(state.SearchMarkersAreTransient, "Search markers must remain transient and non-persistent.");
    }

    [TestMethod]
    public async Task Phase7_RuntimeBridgeTracksRemoteCursorAndRestrictedRegionMarkers()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        var applied = await page.EvaluateAsync<bool>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const block = host?.querySelector('.tm-wysiwyg-block[data-block-id]');
                const blockId = block?.getAttribute('data-block-id') || '';
                if (!instanceId || !blockId) return false;

                window.tmDocumentWysiwyg?.applyRemoteCursor?.(instanceId, {
                    sessionId: 'phase7-peer',
                    displayName: 'Phase 7 Peer',
                    blockId,
                    inlineIndex: 0,
                    offset: 1,
                    color: '#2563eb'
                });

                window.tmDocumentWysiwyg?.setProtectionMode?.(instanceId, true, [{
                    id: 'phase7-region',
                    startBlockId: blockId,
                    startOffset: 0,
                    endBlockId: blockId,
                    endOffset: 8,
                    label: 'Editable region'
                }]);

                return true;
            }
            """);

        Assert.IsTrue(applied);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-remote-cursor']")).ToHaveCountAsync(1);

        var state = await ReadMarkerStoreStateAsync(page);
        Assert.IsTrue(state.HasRemoteSelectionMarker, "Remote cursor state should be indexed as a marker.");
        Assert.IsTrue(state.HasRestrictedRegionMarker, "Protected editable regions should be indexed as markers.");
    }

    [TestMethod]
    public async Task Phase7_OverlappingSearchCommentAndRevisionMarkersStayIndexed()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        var applied = await page.EvaluateAsync<bool>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const block = host?.querySelector('.tm-wysiwyg-block[data-block-id]');
                const blockId = block?.getAttribute('data-block-id') || '';
                if (!instanceId || !blockId) return false;

                window.tmDocumentWysiwyg?.setSearchMarkers?.(instanceId, [blockId], [0], [4]);
                window.tmDocumentWysiwyg?.upsertMarker?.(instanceId, {
                    id: 'phase7-comment-overlap',
                    type: 'comment',
                    range: { startBlockId: blockId, startOffset: 1, endBlockId: blockId, endOffset: 5 },
                    priority: 60,
                    affectsData: true,
                    targetId: 'phase7-comment-overlap'
                });
                window.tmDocumentWysiwyg?.upsertMarker?.(instanceId, {
                    id: 'phase7-revision-overlap',
                    type: 'revisionDeletion',
                    range: { startBlockId: blockId, startOffset: 2, endBlockId: blockId, endOffset: 6 },
                    priority: 80,
                    affectsData: true,
                    targetId: 'phase7-revision-overlap'
                });

                return true;
            }
            """);

        Assert.IsTrue(applied);

        var state = await ReadMarkerStoreStateAsync(page);
        Assert.IsTrue(state.HasSearchMarker || state.HasActiveSearchMarker, "Search marker should stay indexed.");
        Assert.IsTrue(state.HasCommentMarker, "Comment marker should stay indexed alongside search.");
        Assert.IsTrue(state.HasRevisionMarker, "Revision marker should stay indexed alongside search/comment.");
    }


    private static Task<MarkerStoreState> ReadMarkerStoreStateAsync(IPage page)
        => page.EvaluateAsync<MarkerStoreState>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const markers = window.tmDocumentWysiwyg?.getMarkers?.(instanceId) || [];
                const typeOf = marker => marker.type || marker.Type || '';
                const sourceOf = marker => marker.source || marker.Source || '';
                const affectsDataOf = marker => !!(marker.affectsData ?? marker.AffectsData);
                const searchMarkers = markers.filter(marker => typeOf(marker) === 'search' || typeOf(marker) === 'searchActive');
                return {
                    hasSearchMarker: searchMarkers.some(marker => typeOf(marker) === 'search'),
                    hasActiveSearchMarker: searchMarkers.some(marker => typeOf(marker) === 'searchActive'),
                    searchMarkersAreTransient: searchMarkers.length > 0
                        && searchMarkers.every(marker => sourceOf(marker) === 'transient' && !affectsDataOf(marker)),
                    hasCommentMarker: markers.some(marker => typeOf(marker) === 'comment'),
                    hasRevisionMarker: markers.some(marker => String(typeOf(marker)).indexOf('revision') === 0),
                    hasRemoteSelectionMarker: markers.some(marker => typeOf(marker) === 'remoteSelection'),
                    hasRestrictedRegionMarker: markers.some(marker => typeOf(marker) === 'restrictedRegion')
                };
            }
            """);

    private sealed class MarkerStoreState
    {
        [JsonPropertyName("hasSearchMarker")]
        public bool HasSearchMarker { get; set; }

        [JsonPropertyName("hasActiveSearchMarker")]
        public bool HasActiveSearchMarker { get; set; }

        [JsonPropertyName("searchMarkersAreTransient")]
        public bool SearchMarkersAreTransient { get; set; }

        [JsonPropertyName("hasCommentMarker")]
        public bool HasCommentMarker { get; set; }

        [JsonPropertyName("hasRevisionMarker")]
        public bool HasRevisionMarker { get; set; }

        [JsonPropertyName("hasRemoteSelectionMarker")]
        public bool HasRemoteSelectionMarker { get; set; }

        [JsonPropertyName("hasRestrictedRegionMarker")]
        public bool HasRestrictedRegionMarker { get; set; }
    }
}
