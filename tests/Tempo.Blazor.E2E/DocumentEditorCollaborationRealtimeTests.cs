using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorCollaborationRealtimeTests : WasmTestBase
{
    [TestInitialize]
    public Task ResetDocumentEditorDemoAsync()
        => DocumentEditorE2EReset.ResetAsync();

    [TestMethod]
    public async Task RealtimeProvider_DoesNotPollDocumentCollaborationEndpointsWhileIdle()
    {
        var collaborationDocumentRequests = new List<string>();
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/document-editor/collaboration/documents/", StringComparison.Ordinal))
            {
                collaborationDocumentRequests.Add(request.Url);
            }
        };

        await page.GotoAsync($"{BaseUrl}/document-editor?renderEngine=Legacy", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block", new()
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60000
        });

        collaborationDocumentRequests.Clear();
        await page.WaitForTimeoutAsync(1800);

        collaborationDocumentRequests.Should().BeEmpty(
            "SignalR collaboration should push batches and cursors instead of polling document collaboration endpoints while idle");
    }
}
