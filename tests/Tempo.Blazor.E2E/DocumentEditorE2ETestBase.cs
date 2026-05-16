using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Shared Playwright helpers for document editor quality and runtime migration tests.
/// </summary>
public abstract class DocumentEditorE2ETestBase : WasmTestBase
{
    /// <summary>Opens the normal document editor demo route and waits until the WYSIWYG surface is ready.</summary>
    protected async Task<IPage> OpenDocumentEditorAsync(int width = 1280, int height = 720)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
        return page;
    }

    /// <summary>Types text into the visible document body.</summary>
    protected static async Task EditorTypeAsync(IPage page, string text)
    {
        var body = await WaitForWysiwygBodyAsync(page);
        var textBlock = body.Locator(".tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)").First;
        await textBlock.ClickAsync(new() { Position = new() { X = 12, Y = 12 } });
        await page.Keyboard.TypeAsync(text);
    }

    /// <summary>Presses the editor undo shortcut.</summary>
    protected static Task EditorPressUndoAsync(IPage page)
    {
        return page.Keyboard.PressAsync("Control+Z");
    }

    /// <summary>Reads visible document body text from non-virtual pages.</summary>
    protected static Task<string> ReadEditorPlainTextAsync(IPage page)
    {
        return page.EvaluateAsync<string>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'))
                .map(body => body.innerText || body.textContent || '')
                .join('\n')
            """);
    }

    /// <summary>Reads current toolbar formatting state from controls and the JS debug bridge.</summary>
    protected static Task<DocumentEditorToolbarState> ReadToolbarStateAsync(IPage page)
    {
        return page.EvaluateAsync<DocumentEditorToolbarState>(
            """
            () => {
                const pressed = selector => {
                    const el = document.querySelector(selector);
                    return el?.getAttribute('aria-pressed') === 'true'
                        || el?.classList?.contains('is-active')
                        || el?.classList?.contains('tm-document-editor__ribbon-button--active')
                        || false;
                };
                const value = selector => {
                    const el = document.querySelector(selector);
                    return el?.value || el?.textContent?.trim() || '';
                };
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const formatting = window.tmDocumentEditorWysiwyg?.getFormattingState?.(instanceId) || {};
                return {
                    bold: !!(formatting.bold ?? formatting.Bold ?? pressed('[data-testid="document-bold"]')),
                    italic: !!(formatting.italic ?? formatting.Italic ?? pressed('[data-testid="document-italic"]')),
                    underline: !!(formatting.underline ?? formatting.Underline ?? pressed('[data-testid="document-underline"]')),
                    fontFamily: String(formatting.fontFamily ?? formatting.FontFamily ?? value('[data-testid="document-font-family"]')),
                    fontSize: String(formatting.fontSize ?? formatting.FontSize ?? value('[data-testid="document-font-size"]')),
                    alignment: String(formatting.alignment ?? formatting.Alignment ?? '')
                };
            }
            """);
    }

    /// <summary>Waits briefly for the editor to settle and captures a full-page screenshot.</summary>
    protected async Task CaptureStableEditorScreenshotAsync(IPage page, string name)
    {
        await page.WaitForTimeoutAsync(250);
        await TakeScreenshotAsync(page, name);
    }

    /// <summary>Waits until the document editor host has rendered at least one WYSIWYG block.</summary>
    protected static async Task WaitForDocumentEditorReadyAsync(IPage page)
    {
        await page.WaitForSelectorAsync("[data-testid='document-editor-demo']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60000
        });
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60000
        });
    }

    /// <summary>Returns the visible contenteditable body from the first non-virtual page.</summary>
    protected static async Task<ILocator> WaitForWysiwygBodyAsync(IPage page)
    {
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await Assertions.Expect(host).ToBeVisibleAsync();
        var body = host.Locator(".tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable]").First;
        await body.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
        return body;
    }
}

/// <summary>Best-effort toolbar state snapshot for document editor E2E tests.</summary>
public sealed record DocumentEditorToolbarState(
    [property: JsonPropertyName("bold")] bool Bold,
    [property: JsonPropertyName("italic")] bool Italic,
    [property: JsonPropertyName("underline")] bool Underline,
    [property: JsonPropertyName("fontFamily")] string FontFamily,
    [property: JsonPropertyName("fontSize")] string FontSize,
    [property: JsonPropertyName("alignment")] string Alignment);
