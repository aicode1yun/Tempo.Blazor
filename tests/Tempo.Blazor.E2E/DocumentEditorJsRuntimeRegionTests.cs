using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for JS-owned page regions.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeRegionTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase13_HeaderEditKeepsBodyUnchangedAndReportsActiveRegion()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var headerMarker = $"phase13-header-{DateTimeOffset.UtcNow:HHmmssfff}";
        var bodyBefore = await ReadVisibleBodyTextAsync(page);

        await ActivateRegionAsync(page, "header");
        var headerSelection = await ReadRuntimeSelectionAsync(page);
        Assert.AreEqual("Header", headerSelection.Region);

        await page.Keyboard.InsertTextAsync(headerMarker);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header").First).ToContainTextAsync(headerMarker);
        Assert.AreEqual(bodyBefore, await ReadVisibleBodyTextAsync(page), "Typing in the header must not mutate body text.");

        await page.GetByTestId("document-close-header-footer").ClickAsync();
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId) || {};
                return String(selection.region || selection.Region || '') === 'Body';
            }
            """);
    }

    [TestMethod]
    public async Task Phase13_FooterEditPersistsThroughSaveReload()
    {
        var original = await LoadDemoDocumentAsync("contract-demo");
        Assert.IsNotNull(original?.Document);

        try
        {
            var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
            var footerMarker = $"phase13-footer-{DateTimeOffset.UtcNow:HHmmssfff}";

            await ActivateRegionAsync(page, "footer");
            var footerSelection = await ReadRuntimeSelectionAsync(page);
            Assert.AreEqual("Footer", footerSelection.Region);

            await page.Keyboard.InsertTextAsync(footerMarker);
            await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__footer").First).ToContainTextAsync(footerMarker);

            await page.GetByTestId("document-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Saved");
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await WaitForDocumentEditorReadyAsync(page);

            await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__footer").First).ToContainTextAsync(footerMarker);
        }
        finally
        {
            if (original?.Document is not null)
            {
                await SaveDemoDocumentAsync(original.Document);
            }
        }
    }

    [TestMethod]
    public async Task Phase13_UndoHeaderEditRestoresHeaderRegionOnly()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var headerMarker = $"phase13-undo-{DateTimeOffset.UtcNow:HHmmssfff}";
        var bodyBefore = await ReadVisibleBodyTextAsync(page);

        await ActivateRegionAsync(page, "header");
        await page.Keyboard.InsertTextAsync(headerMarker);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header").First).ToContainTextAsync(headerMarker);

        await RuntimeUndoAsync(page);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header").First).Not.ToContainTextAsync(headerMarker);
        Assert.AreEqual(bodyBefore, await ReadVisibleBodyTextAsync(page), "Undoing a header edit must not alter body text.");
        var selection = await ReadRuntimeSelectionAsync(page);
        Assert.AreEqual("Header", selection.Region);
    }

    private static Task ActivateRegionAsync(IPage page, string region)
    {
        return page.EvaluateAsync(
            """
            region => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const selector = region === 'footer'
                    ? '.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__footer[contenteditable="true"]'
                    : '.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__header[contenteditable="true"]';
                const target = Array.from(host?.querySelectorAll(selector) || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                if (!target) throw new Error(`${region} region was not found.`);
                target.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
                const inline = target.querySelector('[data-inline-id]');
                const text = firstTextNode(inline || target);
                const range = document.createRange();
                if (text) {
                    range.setStart(text, text.textContent.length);
                    range.collapse(true);
                } else {
                    range.selectNodeContents(target);
                    range.collapse(false);
                }
                target.focus({ preventScroll: true });
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));

                function firstTextNode(root) {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                    return walker.nextNode();
                }
            }
            """,
            region);
    }

    private static Task RuntimeUndoAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.undo?.(instanceId);
            }
            """);
    }

    private static Task<string> ReadVisibleBodyTextAsync(IPage page)
    {
        return page.EvaluateAsync<string>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'))
                .filter(body => {
                    const rect = body.getBoundingClientRect();
                    return rect.width > 0 && rect.height > 0;
                })
                .map(body => body.innerText || body.textContent || '')
                .join('\n')
            """);
    }

    private static Task<RuntimeSelectionSnapshot> ReadRuntimeSelectionAsync(IPage page)
    {
        return page.EvaluateAsync<RuntimeSelectionSnapshot>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId) || {};
            }
            """);
    }

    private static async Task<DocumentEditorLoadResult?> LoadDemoDocumentAsync(string documentId)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        return await http.GetFromJsonAsync<DocumentEditorLoadResult>($"api/document-editor/{Uri.EscapeDataString(documentId)}");
    }

    private static async Task SaveDemoDocumentAsync(DocumentEditorDocument document)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.PutAsJsonAsync(
            $"api/document-editor/{Uri.EscapeDataString(document.DocumentId)}",
            new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            });
        response.EnsureSuccessStatusCode();
    }

    private sealed class RuntimeSelectionSnapshot
    {
        [JsonPropertyName("region")]
        public string? Region { get; set; }
    }
}
