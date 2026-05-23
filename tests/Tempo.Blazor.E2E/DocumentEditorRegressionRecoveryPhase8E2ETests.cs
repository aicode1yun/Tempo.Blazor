using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for the context-aware document editor side panel.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase8E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task RecoverySidePanel_TextSelectionKeepsManualTabUntilObjectContextWins()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await page.GetByTestId("document-side-panel-tab-comments").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-comments"))
            .ToHaveAttributeAsync("aria-selected", "true");

        await SelectRecoveryTextAsync(page);
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-comments"))
            .ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToHaveCountAsync(0);

        await ClickFirstVisibleImageAsync(page);
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-properties"))
            .ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoverySidePanel_TextSelectionKeepsManualTabUntilObjectContextWins));
    }

    [TestMethod]
    public async Task RecoverySidePanel_ImageSelectionShowsImagePropertiesImmediately()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var figure = await ClickFirstVisibleImageAsync(page);
        await Assertions.Expect(figure).ToHaveClassAsync(new Regex("tm-wysiwyg-image--selected"), new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-properties"))
            .ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-properties-panel")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync();

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoverySidePanel_ImageSelectionShowsImagePropertiesImmediately));
    }

    [TestMethod]
    public async Task RecoverySidePanel_TableCellSelectionShowsTableAndCellProperties()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var cell = page.Locator("[data-testid='document-wysiwyg-host'] [data-block-id='recovery-table-under-images'] td[data-cell-id]").First;
        await Assertions.Expect(cell).ToBeVisibleAsync(new() { Timeout = 5000 });
        await cell.ScrollIntoViewIfNeededAsync();
        await cell.ClickAsync();

        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-properties"))
            .ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
        try
        {
            await Assertions.Expect(page.GetByTestId("document-cell-properties-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
        catch (PlaywrightException ex)
        {
            var debug = await page.EvaluateAsync<object>(
                """
                () => {
                    const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                    const instanceId = host?.getAttribute('data-instance-id') || '';
                    const runtimeSelection = window.tmDocumentEditorEngine?.getSelectionSnapshot?.(instanceId) || null;
                    return {
                        hostActiveCell: host?.getAttribute('data-active-table-cell-id') || '',
                        hostTablePropertiesOpen: host?.getAttribute('data-table-properties-open') || '',
                        hostCellPropertiesOpen: host?.getAttribute('data-cell-properties-open') || '',
                        hostActiveCellResolved: host?.getAttribute('data-active-table-cell-resolved') || '',
                        runtimeSelection,
                        sidePanelHtml: document.querySelector('[data-testid="document-side-panel-body"]')?.innerHTML?.slice(0, 1000) || ''
                    };
                }
                """);
            throw new AssertFailedException($"{ex.Message}\nTable side panel debug: {System.Text.Json.JsonSerializer.Serialize(debug)}");
        }
        await Assertions.Expect(page.GetByTestId("document-table-properties-panel")).ToBeVisibleAsync();

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoverySidePanel_TableCellSelectionShowsTableAndCellProperties));
    }

    [TestMethod]
    public async Task RecoverySidePanel_CommentMarkerSwitchesToCommentsAndActivatesThread()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var marker = page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor[data-comment-id='recovery-comment-visible']").First;
        await Assertions.Expect(marker).ToBeVisibleAsync();
        await marker.ClickAsync();

        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-comments"))
            .ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-comment-thread'][data-comment-id='recovery-comment-visible']").First)
            .ToHaveClassAsync(new Regex("tm-document-comment-thread--selected"), new() { Timeout = 5000 });

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoverySidePanel_CommentMarkerSwitchesToCommentsAndActivatesThread));
    }

    [TestMethod]
    public async Task RecoverySidePanel_RevisionMarkerSwitchesToRevisionsAndActivatesRevision()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var marker = page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision[data-revision-id='recovery-revision-insertion']").First;
        await Assertions.Expect(marker).ToBeVisibleAsync();
        await marker.ClickAsync();

        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-revisions"))
            .ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item'][data-revision-id='recovery-revision-insertion']").First)
            .ToHaveClassAsync(new Regex("tm-document-revision-panel__item--selected"), new() { Timeout = 5000 });

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoverySidePanel_RevisionMarkerSwitchesToRevisionsAndActivatesRevision));
    }

    private static async Task<ILocator> ClickFirstVisibleImageAsync(IPage page)
    {
        var figure = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id]").First;
        await Assertions.Expect(figure).ToBeVisibleAsync(new() { Timeout = 5000 });
        await figure.ScrollIntoViewIfNeededAsync();
        await figure.ClickAsync();
        return figure;
    }

    private static Task SelectRecoveryTextAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const block = document.querySelector('[data-testid="document-wysiwyg-host"] [data-block-id="recovery-comment-paragraph"]');
                if (!block) throw new Error('Could not find recovery text paragraph.');
                const textNode = Array.from(block.childNodes).find(node => node.nodeType === Node.TEXT_NODE && (node.textContent || '').trim().length > 8)
                    || block.firstChild;
                const range = document.createRange();
                range.setStart(textNode, 0);
                range.setEnd(textNode, Math.min(8, (textNode.textContent || '').length));
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                block.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, composed: true }));
                document.dispatchEvent(new Event('selectionchange', { bubbles: true }));
            }
            """);
}
