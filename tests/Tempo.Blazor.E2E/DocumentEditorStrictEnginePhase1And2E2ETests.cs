using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict migration tests for the new document editor engine facade and model layer.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase1And2E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_DefaultsToGoogleDocsRuntimeAfterHardCut()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var debug = await page.EvaluateAsync<EngineDebugProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const debug = window.tmDocumentEditorRuntime?.getDebugSnapshot?.(instanceId) || {};
                return {
                    instanceId,
                    engineMode: String(debug.EngineMode ?? debug.engineMode ?? ''),
                    useGoogleDocsEngine: !!(debug.UseGoogleDocsEngine ?? debug.useGoogleDocsEngine)
                };
            }
            """);

        debug.InstanceId.Should().NotBeNullOrWhiteSpace();
        debug.EngineMode.Should().Be("google-docs");
        debug.UseGoogleDocsEngine.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_GoogleDocsFacadeStillIgnoresDeterministicFlag()
    {
        var context = await CreateContextAsync();
        await context.AddInitScriptAsync("localStorage.setItem('tmDocumentEditorUseGoogleDocsEngine', '1'); window.__tmDocumentEditorUseGoogleDocsEngine = true;");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync($"{BaseUrl}/document-editor?tmDocumentEditorEngine=google-docs", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);

        var debug = await page.EvaluateAsync<EngineDebugProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const debug = window.tmDocumentEditorRuntime?.getDebugSnapshot?.(instanceId) || {};
                return {
                    instanceId,
                    engineMode: String(debug.EngineMode ?? debug.engineMode ?? ''),
                    useGoogleDocsEngine: !!(debug.UseGoogleDocsEngine ?? debug.useGoogleDocsEngine),
                    blockCount: Number(debug.Validation?.counts?.blocks ?? debug.validation?.counts?.blocks ?? 0),
                    hasError: !!(debug.Error ?? debug.error)
                };
            }
            """);

        debug.InstanceId.Should().NotBeNullOrWhiteSpace();
        debug.EngineMode.Should().Be("google-docs");
        debug.UseGoogleDocsEngine.Should().BeTrue();
        debug.HasError.Should().BeFalse();
        debug.BlockCount.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_FacadeReturnsStableDisposedErrors()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<DisposedFacadeProbe>(
            """
            () => {
                const root = document.createElement('div');
                document.body.appendChild(root);
                const id = window.tmDocumentEditorEngine.create(root, { instanceId: 'phase-disposed', useGoogleDocsEngine: true }, null);
                const disposeResult = window.tmDocumentEditorEngine.dispose(id);
                const debug = window.tmDocumentEditorEngine.getDebugSnapshot(id);
                const load = window.tmDocumentEditorEngine.loadDocument(id, { DocumentId: 'x', Blocks: [] });
                root.remove();
                return {
                    instanceId: id,
                    disposeOk: disposeResult?.ok === true,
                    debugErrorCode: String(debug?.error?.code || ''),
                    loadErrorCode: String(load?.error?.code || '')
                };
            }
            """);

        result.InstanceId.Should().Be("phase-disposed");
        result.DisposeOk.Should().BeTrue();
        result.DebugErrorCode.Should().Be("disposed");
        result.LoadErrorCode.Should().Be("disposed");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_ModelImportsIndexesValidatesAndRoundtripsContractDemo()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ModelContractProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const modelApi = window.tmDocumentEditorEngine.model;
                const model = modelApi.importFromCSharpJson(snapshot.Document || snapshot.document);
                const validation = modelApi.validateModel(model);
                const exported = modelApi.exportToCSharpJson(model);
                const roundtrip = modelApi.importFromCSharpJson(exported);
                const roundtripValidation = modelApi.validateModel(roundtrip);
                const schema = modelApi.createDefaultSchemaRegistry();
                return {
                    ok: validation.ok === true && roundtripValidation.ok === true,
                    blockCount: Number(validation.counts.blocks || 0),
                    inlineCount: Number(validation.counts.inlines || 0),
                    objectCount: Number(validation.counts.objects || 0),
                    revisionCount: Number(validation.counts.revisions || 0),
                    commentCount: Number(validation.counts.comments || 0),
                    exportedBlockCount: Array.isArray(exported.Blocks) ? exported.Blocks.length : 0,
                    bodyAllowsParagraph: schema.checkChild('body', 'paragraph') === true,
                    paragraphAllowsText: schema.checkChild('paragraph', 'text') === true,
                    captionAllowsImage: schema.checkChild('caption', 'image') === true,
                    imageIsObject: schema.getDefinition('image')?.isObject === true,
                    tableCellIsLimit: schema.getDefinition('tableCell')?.isLimit === true
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.BlockCount.Should().BeGreaterThan(0);
        result.InlineCount.Should().BeGreaterThan(0);
        result.ObjectCount.Should().BeGreaterThan(0);
        result.ExportedBlockCount.Should().BeGreaterThan(0);
        result.BodyAllowsParagraph.Should().BeTrue();
        result.ParagraphAllowsText.Should().BeTrue();
        result.CaptionAllowsImage.Should().BeFalse("caption is a limit region and must not accept nested image objects yet");
        result.ImageIsObject.Should().BeTrue();
        result.TableCellIsLimit.Should().BeTrue();
    }

    private sealed class EngineDebugProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("engineMode")] public string EngineMode { get; set; } = string.Empty;
        [JsonPropertyName("useGoogleDocsEngine")] public bool UseGoogleDocsEngine { get; set; }
        [JsonPropertyName("blockCount")] public int BlockCount { get; set; }
        [JsonPropertyName("hasError")] public bool HasError { get; set; }
    }

    private sealed class DisposedFacadeProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("disposeOk")] public bool DisposeOk { get; set; }
        [JsonPropertyName("debugErrorCode")] public string DebugErrorCode { get; set; } = string.Empty;
        [JsonPropertyName("loadErrorCode")] public string LoadErrorCode { get; set; } = string.Empty;
    }

    private sealed class ModelContractProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("blockCount")] public int BlockCount { get; set; }
        [JsonPropertyName("inlineCount")] public int InlineCount { get; set; }
        [JsonPropertyName("objectCount")] public int ObjectCount { get; set; }
        [JsonPropertyName("revisionCount")] public int RevisionCount { get; set; }
        [JsonPropertyName("commentCount")] public int CommentCount { get; set; }
        [JsonPropertyName("exportedBlockCount")] public int ExportedBlockCount { get; set; }
        [JsonPropertyName("bodyAllowsParagraph")] public bool BodyAllowsParagraph { get; set; }
        [JsonPropertyName("paragraphAllowsText")] public bool ParagraphAllowsText { get; set; }
        [JsonPropertyName("captionAllowsImage")] public bool CaptionAllowsImage { get; set; }
        [JsonPropertyName("imageIsObject")] public bool ImageIsObject { get; set; }
        [JsonPropertyName("tableCellIsLimit")] public bool TableCellIsLimit { get; set; }
    }
}
