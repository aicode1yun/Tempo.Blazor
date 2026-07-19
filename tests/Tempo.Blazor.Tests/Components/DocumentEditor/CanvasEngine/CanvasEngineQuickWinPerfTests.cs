using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.CanvasEngine;

/// <summary>
/// bUnit coverage for the N3 quick-win perf fixes: the debug JSON modal must not serialize the
/// whole document on every editor render (N3.2), and the host's math-run diagnostic attribute
/// must not walk the whole document on every host render (N3.3).
/// </summary>
public sealed class CanvasEngineQuickWinPerfTests : LocalizationTestBase
{
    [Fact]
    public void DebugJsonModal_ClosedModal_DoesNotCarrySerializedDocument()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("debug-json-perf");

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "debug-json-perf")
            .Add(p => p.Provider, provider)
            .Add(p => p.ShowDebugTools, true));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());

        // Perf plan N3.2: while the modal is closed, no document JSON may be computed per render.
        var modal = cut.FindComponent<TmDocumentJsonDebugModal>();
        modal.Instance.Json.Should().BeNullOrEmpty(
            "the closed debug modal must not serialize the whole document on every render");
    }

    [Fact]
    public void DebugJsonModal_OpensWithSnapshotJson_AndClearsOnClose()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("debug-json-open");

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "debug-json-open")
            .Add(p => p.Provider, provider)
            .Add(p => p.ShowDebugTools, true));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());

        var toolbar = cut.FindComponent<TmDocumentEditorToolbar>();
        cut.InvokeAsync(() => toolbar.Instance.OnViewDocumentJson.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            var modal = cut.FindComponent<TmDocumentJsonDebugModal>();
            modal.Instance.IsOpen.Should().BeTrue();
            modal.Instance.Json.Should().Contain("debug-json-open",
                "opening the modal computes the document snapshot once");
        });

        cut.Find("[data-testid='document-json-debug-close']").Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.FindComponent<TmDocumentJsonDebugModal>();
            modal.Instance.IsOpen.Should().BeFalse();
            modal.Instance.Json.Should().BeNullOrEmpty("closing the modal releases the snapshot");
        });
    }

    [Fact]
    public void HostMathCountAttribute_IsCachedByDocumentReference()
    {
        SetupDocumentCanvasModule();
        var document = DocumentEditorDocument.Empty("math-count-host");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "m1",
            Content = new ParagraphBlockContent
            {
                Inlines = [new DocumentMathRun { MathId = "eq-1" }, new TextRun { Text = "x" }]
            }
        });

        var cut = Render<TmDocumentCanvasEngineHost>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.AriaLabel, "Document editor")
            .Add(p => p.InputAriaLabel, "Document editor"));

        var host = cut.Find("[data-testid='document-canvas-engine-host']");
        host.GetAttribute("data-canvas-source-math-count").Should().Be("1");

        // Perf plan N3.3: the count is cached by Document reference — an in-place mutation with the
        // SAME reference must NOT re-walk the document on re-render (the engine owns live edits;
        // the attribute describes the mounted source document).
        document.Blocks.Add(new DocumentBlock
        {
            Id = "m2",
            Content = new ParagraphBlockContent { Inlines = [new DocumentMathRun { MathId = "eq-2" }] }
        });
        cut.Render();
        cut.Find("[data-testid='document-canvas-engine-host']")
            .GetAttribute("data-canvas-source-math-count")
            .Should().Be("1", "same Document reference must serve the cached count");

        // A NEW Document parameter reference recomputes.
        var replacement = DocumentEditorDocument.Empty("math-count-host-2");
        replacement.Blocks.Add(new DocumentBlock
        {
            Id = "m3",
            Content = new ParagraphBlockContent
            {
                Inlines = [new DocumentMathRun { MathId = "eq-a" }, new DocumentMathRun { MathId = "eq-b" }]
            }
        });
        cut.Render(parameters => parameters.Add(p => p.Document, replacement));
        cut.Find("[data-testid='document-canvas-engine-host']")
            .GetAttribute("data-canvas-source-math-count")
            .Should().Be("2", "a new Document reference must recompute the count");
    }
}
