using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the phase 20 hard cut from the removed DOM-driven editor.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase20E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_DefaultDemoUsesOnlyGoogleDocsEngine()
    {
        var context = await CreateContextAsync();
        await context.AddInitScriptAsync(
            "localStorage.setItem('tmDocumentEditorUseGoogleDocsEngine', '0'); localStorage.setItem('tmDocumentEditorMigration', 'phase20'); window.__tmDocumentEditorUseGoogleDocsEngine = false;");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await page.GotoAsync($"{BaseUrl}/document-editor?tmDocumentEditorEngine=legacy&tmDocumentEditorMigration=phase20", new()
        {
            WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForRuntimeHostReadyAsync(page);
        await WaitForEngineBodyHtmlReadyAsync(page);

        var result = await page.EvaluateAsync<MigrationStatusProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtime = window.tmDocumentEditorRuntime;
                runtime.setShowBlocks(instanceId, true);
                runtime.setShowNonPrintingCharacters(instanceId, true);
                runtime.setSearchMarkers(instanceId, ['contract-intro'], [0], [7]);
                runtime.scrollToSearchResult(instanceId, 'contract-intro', 0, 7);
                runtime.clearSearchMarkers(instanceId);
                runtime.scrollToBlock(instanceId, 'contract-intro');
                runtime.scrollToPage(instanceId, 0);
                const html = runtime.getBodyHtml(instanceId);
                const status = runtime.getMigrationStatus(instanceId);
                const audit = runtime.getRemovedLegacyPathAudit(instanceId);
                const moduleNames = runtime.__internal.getModuleNames();
                return {
                    instanceId,
                    htmlLength: html.length,
                    moduleNames,
                    publicCompat: status.publicCompatibility,
                    gates: status.gates,
                    engineMode: status.engineMode,
                    legacyEngineRemoved: status.legacyEngineRemoved === true,
                    allFacadeGatesEnabled: status.allFacadeGatesEnabled === true,
                    removedLegacyPath: audit.removedLegacyPath === true,
                    facadeMethods: Object.keys(audit.facadeCallCounts || {}).sort(),
                    routedToGoogleDocsCount: audit.routedToGoogleDocsCount || 0,
                    legacyGlobalPresent: typeof window.tmDocumentWysiwyg !== 'undefined',
                    legacyAliasPresent: typeof window.tmDocumentEditorWysiwyg !== 'undefined',
                    oldSidecars: host?.querySelectorAll('[data-wrap-sidecar-for], .tm-wysiwyg-image-sidecar-text').length || 0
                };
            }
            """);

        result.InstanceId.Should().NotBeNullOrWhiteSpace();
        result.HtmlLength.Should().BeGreaterThan(100);
        result.EngineMode.Should().Be("google-docs");
        result.LegacyEngineRemoved.Should().BeTrue();
        result.RemovedLegacyPath.Should().BeTrue();
        result.PublicCompat.LegacyWysiwygGlobal.Should().BeFalse();
        result.PublicCompat.LegacyWysiwygAlias.Should().BeFalse();
        result.PublicCompat.TmDocumentEditorRuntime.Should().BeTrue();
        result.ModuleNames.Should().Contain(["core", "selection", "rendering", "input", "formatting", "clipboard", "search", "image", "table", "comments", "revisions", "serialization", "migration", "watchdog"]);
        result.AllFacadeGatesEnabled.Should().BeTrue();
        result.Gates.HardCut.Should().BeTrue();
        result.Gates.DefaultDemo.Should().BeTrue();
        result.Gates.PlainParagraphEditing.Should().BeTrue();
        result.Gates.FormattingCommands.Should().BeTrue();
        result.Gates.ImageWrapScenarios.Should().BeTrue();
        result.Gates.Revisions.Should().BeTrue();
        result.Gates.Tables.Should().BeTrue();
        result.FacadeMethods.Should().Contain(["setShowBlocks", "setShowNonPrintingCharacters", "setSearchMarkers", "scrollToSearchResult", "clearSearchMarkers", "scrollToBlock", "scrollToPage", "getBodyHtml"]);
        result.RoutedToGoogleDocsCount.Should().BeGreaterThan(0);
        result.LegacyGlobalPresent.Should().BeFalse();
        result.LegacyAliasPresent.Should().BeFalse();
        result.OldSidecars.Should().Be(0);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_RejectsLegacyFlagAndRoutesCommandsToNewEngine()
    {
        var context = await CreateContextAsync();
        await context.AddInitScriptAsync("localStorage.setItem('tmDocumentEditorUseGoogleDocsEngine', '1'); localStorage.setItem('tmDocumentEditorMigration', 'phase20');");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync($"{BaseUrl}/document-editor?tmDocumentEditorEngine=google-docs&tmDocumentEditorMigration=phase20", new()
        {
            WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForRuntimeHostReadyAsync(page);

        var result = await page.EvaluateAsync<GoogleMigrationProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtime = window.tmDocumentEditorRuntime;
                runtime.executeCommand(instanceId, 'InsertText', { target: { blockId: 'contract-heading', offset: 0 }, text: 'X', source: 'phase20' });
                const status = runtime.getMigrationStatus(instanceId);
                const audit = runtime.getRemovedLegacyPathAudit(instanceId);
                return {
                    instanceId,
                    engineMode: status.engineMode,
                    useGoogleDocsEngine: status.useGoogleDocsEngine,
                    hardCut: status.gates.hardCut,
                    legacyEngineRemoved: status.legacyEngineRemoved === true,
                    routedToGoogleDocsCount: audit.routedToGoogleDocsCount || 0,
                    legacyGlobalPresent: typeof window.tmDocumentWysiwyg !== 'undefined',
                    legacyAliasPresent: typeof window.tmDocumentEditorWysiwyg !== 'undefined'
                };
            }
            """);

        result.InstanceId.Should().NotBeNullOrWhiteSpace();
        result.EngineMode.Should().Be("google-docs");
        result.UseGoogleDocsEngine.Should().BeTrue();
        result.HardCut.Should().BeTrue();
        result.LegacyEngineRemoved.Should().BeTrue();
        result.RoutedToGoogleDocsCount.Should().BeGreaterThan(0);
        result.LegacyGlobalPresent.Should().BeFalse();
        result.LegacyAliasPresent.Should().BeFalse();
    }

    private sealed class MigrationStatusProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("htmlLength")] public int HtmlLength { get; set; }
        [JsonPropertyName("moduleNames")] public string[] ModuleNames { get; set; } = [];
        [JsonPropertyName("publicCompat")] public PublicCompatibilityProbe PublicCompat { get; set; } = new();
        [JsonPropertyName("gates")] public MigrationGatesProbe Gates { get; set; } = new();
        [JsonPropertyName("engineMode")] public string EngineMode { get; set; } = string.Empty;
        [JsonPropertyName("legacyEngineRemoved")] public bool LegacyEngineRemoved { get; set; }
        [JsonPropertyName("allFacadeGatesEnabled")] public bool AllFacadeGatesEnabled { get; set; }
        [JsonPropertyName("removedLegacyPath")] public bool RemovedLegacyPath { get; set; }
        [JsonPropertyName("facadeMethods")] public string[] FacadeMethods { get; set; } = [];
        [JsonPropertyName("routedToGoogleDocsCount")] public int RoutedToGoogleDocsCount { get; set; }
        [JsonPropertyName("legacyGlobalPresent")] public bool LegacyGlobalPresent { get; set; }
        [JsonPropertyName("legacyAliasPresent")] public bool LegacyAliasPresent { get; set; }
        [JsonPropertyName("oldSidecars")] public int OldSidecars { get; set; }
    }

    private static async Task WaitForRuntimeHostReadyAsync(Microsoft.Playwright.IPage page)
    {
        await page.WaitForSelectorAsync("[data-testid='document-editor-demo']", new()
        {
            State = Microsoft.Playwright.WaitForSelectorState.Attached,
            Timeout = 60000
        });
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'][data-instance-id]", new()
        {
            State = Microsoft.Playwright.WaitForSelectorState.Attached,
            Timeout = 60000
        });
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return !!instanceId && typeof window.tmDocumentEditorRuntime?.getMigrationStatus === 'function';
            }
            """,
            null,
            new() { Timeout = 60000 });
    }

    private static Task WaitForEngineBodyHtmlReadyAsync(Microsoft.Playwright.IPage page)
    {
        return page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                if (!instanceId || typeof window.tmDocumentEditorRuntime?.getBodyHtml !== 'function') return false;
                try {
                    return String(window.tmDocumentEditorRuntime.getBodyHtml(instanceId) || '').length > 100;
                } catch {
                    return false;
                }
            }
            """,
            null,
            new() { Timeout = 60000 });
    }

    private sealed class PublicCompatibilityProbe
    {
        [JsonPropertyName("legacyWysiwygGlobal")] public bool LegacyWysiwygGlobal { get; set; }
        [JsonPropertyName("legacyWysiwygAlias")] public bool LegacyWysiwygAlias { get; set; }
        [JsonPropertyName("tmDocumentEditorRuntime")] public bool TmDocumentEditorRuntime { get; set; }
    }

    private sealed class MigrationGatesProbe
    {
        [JsonPropertyName("hardCut")] public bool HardCut { get; set; }
        [JsonPropertyName("plainParagraphEditing")] public bool PlainParagraphEditing { get; set; }
        [JsonPropertyName("formattingCommands")] public bool FormattingCommands { get; set; }
        [JsonPropertyName("imageWrapScenarios")] public bool ImageWrapScenarios { get; set; }
        [JsonPropertyName("revisions")] public bool Revisions { get; set; }
        [JsonPropertyName("tables")] public bool Tables { get; set; }
        [JsonPropertyName("defaultDemo")] public bool DefaultDemo { get; set; }
    }

    private sealed class GoogleMigrationProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("engineMode")] public string EngineMode { get; set; } = string.Empty;
        [JsonPropertyName("useGoogleDocsEngine")] public bool UseGoogleDocsEngine { get; set; }
        [JsonPropertyName("hardCut")] public bool HardCut { get; set; }
        [JsonPropertyName("legacyEngineRemoved")] public bool LegacyEngineRemoved { get; set; }
        [JsonPropertyName("routedToGoogleDocsCount")] public int RoutedToGoogleDocsCount { get; set; }
        [JsonPropertyName("legacyGlobalPresent")] public bool LegacyGlobalPresent { get; set; }
        [JsonPropertyName("legacyAliasPresent")] public bool LegacyAliasPresent { get; set; }
    }
}
