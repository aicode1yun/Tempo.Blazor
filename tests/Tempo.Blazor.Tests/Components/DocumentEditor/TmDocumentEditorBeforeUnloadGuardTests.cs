using System.Reflection;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Regression tests for the before-unload guard interop. The browser global
/// (window.tmDocumentEditor) was historically provided by a host script tag that
/// was deleted, which silently broke the guard and file downloads: the component
/// swallowed the failed interop in a catch. The component must now install the
/// global itself by importing the browser-globals ES module before invoking it.
/// </summary>
public class TmDocumentEditorBeforeUnloadGuardTests : LocalizationTestBase
{
    private const string BrowserGlobalsModulePath =
        "./_content/Tempo.Blazor.DocumentEditor/js/document-editor/interop/browser-globals.mjs";

    [Fact]
    public void Render_ImportsBrowserGlobalsModule()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        JSInterop.SetupModule(BrowserGlobalsModulePath);

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            JSInterop.Invocations
                .Should().Contain(invocation =>
                    invocation.Identifier == "import"
                    && invocation.Arguments.Count > 0
                    && Equals(invocation.Arguments[0], BrowserGlobalsModulePath),
                    "the component must install window.tmDocumentEditor itself instead of relying on a host script tag"));
    }

    [Fact]
    public async Task DirtyTransition_InvokesEnableBeforeUnloadGuard()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        JSInterop.SetupModule(BrowserGlobalsModulePath);

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        var editor = cut.Instance;
        var editorType = typeof(TmDocumentEditor);
        var isDirtyField = editorType.GetField("_isDirty", BindingFlags.Instance | BindingFlags.NonPublic);
        isDirtyField.Should().NotBeNull();
        var updateGuard = editorType.GetMethod("UpdateBeforeUnloadGuardAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        updateGuard.Should().NotBeNull();

        await cut.InvokeAsync(async () =>
        {
            isDirtyField!.SetValue(editor, true);
            await (Task)updateGuard!.Invoke(editor, null)!;
        });

        JSInterop.Invocations
            .Should().Contain(invocation => invocation.Identifier == "tmDocumentEditor.enableBeforeUnloadGuard",
                "a dirty document must arm the browser before-unload guard");
    }
}
