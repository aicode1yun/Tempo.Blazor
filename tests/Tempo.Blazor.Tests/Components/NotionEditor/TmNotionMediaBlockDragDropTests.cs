using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks.Media;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// DDI-01..06: Drag-and-drop on media blocks (image, PDF, file).
/// TDD: tests written before implementation — initially expected to FAIL (RED),
/// then pass after UI is implemented (GREEN).
/// </summary>
public class TmNotionMediaBlockDragDropTests : LocalizationTestBase
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static NotionEditorContext BuildContext(INotionFileProvider? fileProvider = null)
        => new()
        {
            DataProvider  = Substitute.For<INotionDataProvider>(),
            BlockProvider = Substitute.For<INotionBlockProvider>(),
            FileProvider  = fileProvider,
        };

    private static INotionFileProvider MockFileProvider() => Substitute.For<INotionFileProvider>();

    private static IImageBlockContent EmptyImageContent() => new TestImageContent();
    private static IPdfBlockContent   EmptyPdfContent()   => new TestPdfContent();
    private static IFileBlockContent  EmptyFileContent()  => new TestFileContent();

    // ── DDI-01: No drop overlay without FileProvider ─────────────────────────

    [Fact]
    public void ImageBlock_WithoutFileProvider_NoDropOverlay()
    {
        var ctx = BuildContext(fileProvider: null);
        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        cut.FindAll("[data-testid='drop-overlay']").Should().BeEmpty();
    }

    // ── DDI-02: Drop overlay present when FileProvider is set ────────────────

    [Fact]
    public void ImageBlock_WithFileProvider_ShowsDropOverlay()
    {
        var ctx = BuildContext(fileProvider: MockFileProvider());
        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        cut.Find("[data-testid='drop-overlay']").Should().NotBeNull();
    }

    // ── DDI-03: Drop overlay gets active class on dragenter ───────────────────

    [Fact]
    public async Task ImageBlock_DragEnter_OverlayBecomesActive()
    {
        var ctx = BuildContext(fileProvider: MockFileProvider());
        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        var zone = cut.Find("[data-testid='drop-zone']");
        await zone.DragEnterAsync(new DragEventArgs());

        cut.Find("[data-testid='drop-overlay']").ClassList
           .Should().Contain("tm-notion-drop-overlay--active");
    }

    // ── DDI-04: Drop overlay loses active class on dragleave ─────────────────

    [Fact]
    public async Task ImageBlock_DragLeave_OverlayBecomesInactive()
    {
        var ctx = BuildContext(fileProvider: MockFileProvider());
        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        var zone = cut.Find("[data-testid='drop-zone']");
        await zone.DragEnterAsync(new DragEventArgs());
        await zone.DragLeaveAsync(new DragEventArgs());

        cut.Find("[data-testid='drop-overlay']").ClassList
           .Should().NotContain("tm-notion-drop-overlay--active");
    }

    // ── DDI-05: PDF block – drop overlay present when FileProvider is set ────

    [Fact]
    public void PdfBlock_WithFileProvider_ShowsDropOverlay()
    {
        var ctx = BuildContext(fileProvider: MockFileProvider());
        var cut = RenderComponent<TmNotionPdfBlock>(p => p
            .Add(x => x.Content,  EmptyPdfContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        cut.Find("[data-testid='drop-overlay']").Should().NotBeNull();
    }

    // ── DDI-06: File block – drop overlay present when FileProvider is set ───

    [Fact]
    public void FileBlock_WithFileProvider_ShowsDropOverlay()
    {
        var ctx = BuildContext(fileProvider: MockFileProvider());
        var cut = RenderComponent<TmNotionFileBlock>(p => p
            .Add(x => x.Content,  EmptyFileContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        cut.Find("[data-testid='drop-overlay']").Should().NotBeNull();
    }

    // ── Stub content implementations ─────────────────────────────────────────

    private class TestImageContent : IImageBlockContent
    {
        public string         Url       { get; set; } = string.Empty;
        public string?        FileId    { get; set; }
        public string?        Caption   { get; set; }
        public int?           Width     { get; set; }
        public string?        AltText   { get; set; }
        public MediaAlignment Alignment { get; set; }
    }

    private class TestPdfContent : IPdfBlockContent
    {
        public string  Url     { get; set; } = string.Empty;
        public string? FileId  { get; set; }
        public string? Caption { get; set; }
        public int?    Width   { get; set; }
    }

    private class TestFileContent : IFileBlockContent
    {
        public string  Url           { get; set; } = string.Empty;
        public string? FileId        { get; set; }
        public string? Caption       { get; set; }
        public int?    Width         { get; set; }
        public string  FileName      { get; set; } = "test.txt";
        public long    FileSizeBytes { get; set; }
        public string  ContentType   { get; set; } = "text/plain";
    }
}
