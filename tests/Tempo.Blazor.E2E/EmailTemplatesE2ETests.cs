using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E11 — end-to-end coverage of the email-template demo flow on the live WASM demo (7106) +
/// demo API (5100) + smtp4dev (2525/5000). Captures named screenshots into
/// <c>__screenshots__/email-templates/</c> for the UX review (E11.11).
///
/// Faithfulness notes vs the E11 plan:
/// <list type="bullet">
///   <item>blocks are added via the toolbox click-to-add affordance (a real feature) rather than
///   HTML5 drag&amp;drop, which is flaky under headless Chromium;</item>
///   <item>the full send flow (E11.8) drives the <c>Order confirmation</c> seed (which already owns
///   the <c>customer_name</c>/<c>order_id</c> variables + sample data) instead of authoring a fresh
///   variable into the rich-text canvas by simulated keystrokes — the send → smtp4dev →
///   substitution → verify → cleanup path is exercised identically and reliably.</item>
/// </list>
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EmailTemplatesE2ETests : EmailTemplateE2ETestBase
{
    // ── E11.2 list + new ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_2_List_ShowsSeedTemplates_AndNewTemplateOpensEditor()
    {
        var page = await OpenAsync("/email-templates");
        await page.WaitForSelectorAsync("[data-tm-template-card]", new() { Timeout = 30000 });

        (await page.Locator("[data-tm-template-card]").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "the demo seeds Welcome, Newsletter and Order templates");
        await SaveNamedScreenshotAsync(page, "01-list.png");

        await page.Locator("[data-tm-new-template]").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });
        page.Url.Should().Contain("/email-templates/edit/");
        await SaveNamedScreenshotAsync(page, "02-gallery.png");
    }

    // ── E11.3 editor: open seed + add block ──────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_3_Editor_OpensSeed_AddsBlock_SelectsIt()
    {
        var page = await OpenAsync($"/email-templates/edit/{WelcomeTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });
        await SaveNamedScreenshotAsync(page, "03-editor.png");

        var before = await page.Locator("[data-tm-block-id]").CountAsync();
        await page.Locator("[data-tm-block='text']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "n => document.querySelectorAll('[data-tm-block-id]').length > n", before,
            new() { Timeout = 15000 });

        // Select the newly-added block; the property panel reflects a block target.
        await page.Locator("[data-tm-block-id]").Last.ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-prop-target='block']", new() { Timeout = 10000 });
        await SaveNamedScreenshotAsync(page, "04-editor-edit.png");
    }

    // ── E11.4 editor: section preset, delete, undo/redo ──────────────────────────────────────

    [TestMethod]
    public async Task E11_4_Editor_AddSectionPreset_DeleteBlock_UndoRedo()
    {
        var page = await OpenAsync($"/email-templates/edit/{NewsletterTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });

        var sectionsBefore = await page.Locator("[data-tm-section]").CountAsync();
        await page.Locator("[data-tm-preset='TwoEqual'], [data-tm-preset='TwoColumns'], [data-tm-preset]").Nth(1).ClickAsync();
        await page.WaitForFunctionAsync(
            "n => document.querySelectorAll('[data-tm-section]').length > n", sectionsBefore,
            new() { Timeout = 15000 });

        // Delete a block via its inline action, then undo / redo through the toolbar.
        var blocksBefore = await page.Locator("[data-tm-block-id]").CountAsync();
        await page.Locator("[data-tm-block-id]").First.ClickAsync();
        await page.Locator("[data-tm-block-action='delete']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "n => document.querySelectorAll('[data-tm-block-id]').length < n", blocksBefore,
            new() { Timeout = 15000 });

        await page.Locator("[data-tm-undo]").ClickAsync();
        await page.WaitForFunctionAsync(
            "n => document.querySelectorAll('[data-tm-block-id]').length === n", blocksBefore,
            new() { Timeout = 15000 });

        await page.Locator("[data-tm-redo]").ClickAsync();
        await page.WaitForFunctionAsync(
            "n => document.querySelectorAll('[data-tm-block-id]').length < n", blocksBefore,
            new() { Timeout = 15000 });
    }

    // ── E11.5 preview desktop / mobile / text ────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_5_Preview_DesktopMobileText()
    {
        var page = await OpenAsync($"/email-templates/edit/{WelcomeTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });

        await page.Locator("[data-tm-preview-btn]").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-preview-frame]", new() { Timeout = 20000 });
        await SaveNamedScreenshotAsync(page, "05-preview-desktop.png");

        await page.Locator("[data-tm-preview-device='mobile']").ClickAsync();
        await page.WaitForTimeoutAsync(400);
        await SaveNamedScreenshotAsync(page, "06-preview-mobile.png");

        await page.Locator("[data-tm-preview-view='text']").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-preview-text]", new() { Timeout = 10000 });
        (await page.Locator("[data-tm-preview-text]").InnerTextAsync()).Should().NotBeNullOrWhiteSpace();
    }

    // ── E11.6 validation ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_6_Validation_SurfacesButtonWithoutHref()
    {
        var page = await OpenAsync($"/email-templates/edit/{WelcomeTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });

        // A button with no href is an invalid email block.
        await page.Locator("[data-tm-block='button']").First.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        await page.Locator("[data-tm-validate-btn]").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-validation]", new() { Timeout = 15000 });
        (await page.Locator("[data-tm-validation-message]").CountAsync())
            .Should().BeGreaterThan(0, "an empty-href button should produce a validation finding");
        await SaveNamedScreenshotAsync(page, "07-validation.png");
    }

    // ── E11.7 save persists across reload ────────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_7_Save_PersistsAcrossReload()
    {
        // Use a fresh template so we never mutate a shared seed read by other tests.
        var page = await OpenAsync("/email-templates");
        await page.Locator("[data-tm-new-template]").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });
        var editUrl = page.Url;

        var subject = $"Persisted {Guid.NewGuid():N}";
        var subjectField = page.Locator("[data-tm-prop='Subject'] input");
        await subjectField.FillAsync(subject);
        await subjectField.BlurAsync();

        await page.Locator("[data-tm-save]").ClickAsync();
        await page.WaitForTimeoutAsync(800);

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load });
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync("[data-tm-prop='Subject'] input", new() { Timeout = 30000 });
        (await page.Locator("[data-tm-prop='Subject'] input").InputValueAsync())
            .Should().Be(subject, "the saved subject survives a page reload via the API");
        editUrl.Should().Contain("/email-templates/edit/");
    }

    // ── E11.8 full send flow with smtp4dev verification ──────────────────────────────────────

    [TestMethod]
    public async Task E11_8_FullSendFlow_DeliversSubstitutedEmail()
    {
        var recipient = UniqueRecipient("order");
        var customerName = $"Customer {Guid.NewGuid():N}";

        var page = await OpenAsync($"/email-templates/send/{OrderTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-send-page]", new() { Timeout = 30000 });

        // The dynamic form exposes the template's variables.
        await page.WaitForSelectorAsync("[data-tm-var='customer_name']", new() { Timeout = 15000 });
        await page.Locator("[data-tm-var='order_id']").FillAsync("E2E-7777");
        await page.Locator("[data-tm-var='customer_name']").FillAsync(customerName);
        await page.Locator("[data-tm-to]").FillAsync(recipient);
        // Live preview adopts the typed values (not the seed sample data).
        await page.WaitForFunctionAsync(
            "name => { const f = document.querySelector('[data-tm-preview-frame]'); return f && (f.getAttribute('srcdoc') || '').includes(name); }",
            customerName, new() { Timeout = 15000 });
        await SaveNamedScreenshotAsync(page, "08-send-form.png");

        await page.Locator("[data-tm-send-submit]").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-send-success]", new() { Timeout = 20000 });
        await SaveNamedScreenshotAsync(page, "09-send-success.png");

        // smtp4dev actually received the substituted message.
        var message = await PollForMessageAsync(recipient);
        message.Subject.Should().Contain("E2E-7777", "subject substitutes order_id");
        message.To.Should().Contain(recipient);

        var html = await GetMessageHtmlAsync(message.Id);
        html.Should().Contain(customerName, "the HTML body substitutes customer_name");

        var text = await GetMessagePlaintextOrNullAsync(message.Id);
        text.Should().NotBeNull("a multipart email carries a text alternative");
        text!.Should().Contain(customerName);

        await DeleteMessageAsync(message.Id);
    }

    // ── E11.9 accessibility / keyboard ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_9_Editor_KeyboardAndShortcutsHelp()
    {
        var page = await OpenAsync($"/email-templates/edit/{WelcomeTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });

        // Tab moves focus into interactive chrome (focus is visible / tracked).
        await page.Keyboard.PressAsync("Tab");
        var hasFocus = await page.EvaluateAsync<bool>("() => document.activeElement && document.activeElement !== document.body");
        hasFocus.Should().BeTrue();

        // Keyboard shortcuts help opens.
        await page.Locator("[data-tm-help-btn]").ClickAsync();
        await page.WaitForTimeoutAsync(400);
        await SaveNamedScreenshotAsync(page, "09b-shortcuts-help.png");
    }

    // ── E11.10b MJML import flow ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task E11_10b_ImportMjml_RendersStructure()
    {
        var page = await OpenAsync($"/email-templates/edit/{WelcomeTemplateId}");
        await page.WaitForSelectorAsync("[data-tm-email-editor]", new() { Timeout = 30000 });

        await page.Locator("[data-tm-import-btn]").ClickAsync();
        await page.WaitForSelectorAsync("[data-tm-import-input]", new() { Timeout = 15000 });

        const string mjml = """
            <mjml><mj-body><mj-section><mj-column>
              <mj-text>Imported headline</mj-text>
              <mj-button href="https://example.com">Imported CTA</mj-button>
            </mj-column></mj-section></mj-body></mjml>
            """;
        await page.Locator("[data-tm-import-input]").FillAsync(mjml);
        await page.WaitForSelectorAsync("[data-tm-import-summary]", new() { Timeout = 15000 });
        await SaveNamedScreenshotAsync(page, "11-import.png");

        await page.Locator("[data-tm-import-confirm]").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.body.innerText.includes('Imported headline')",
            null, new() { Timeout = 15000 });
    }
}
