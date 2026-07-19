using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Blocks.Media;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class TmNotionPdfBlockTests : LocalizationTestBase
{
    private static IPdfBlockContent CreateContent(string? url = null, string? caption = null)
        => new TestPdfBlockContent { Url = url, Caption = caption };

    // ── PDF-35: Notion block renderuje TmPdfViewer ─────────────────────────

    [Fact]
    public void Render_WithUrl_DisplaysPdfViewer()
    {
        var cut = Render<TmNotionPdfBlock>(parameters =>
            parameters.Add(p => p.Content, CreateContent("https://example.com/test.pdf")));

        cut.Find(".tm-pdf-viewer").Should().NotBeNull();
    }

    // ── PDF-36: Notion block předává Url do TmPdfViewer ────────────────────

    [Fact]
    public void Render_PassesUrlToPdfViewer()
    {
        var cut = Render<TmNotionPdfBlock>(parameters =>
            parameters.Add(p => p.Content, CreateContent("https://example.com/doc.pdf")));

        var openLink = cut.Find("a[target='_blank']");
        openLink.GetAttribute("href").Should().Be("https://example.com/doc.pdf");
    }

    // ── PDF-37: Caption editing je zachováno ───────────────────────────────

    [Fact]
    public void Render_WithCaption_DisplaysCaptionArea()
    {
        var cut = Render<TmNotionPdfBlock>(parameters =>
            parameters.Add(p => p.Content, CreateContent("https://example.com/test.pdf", "My caption"))
                      .Add(p => p.ReadOnly, false));

        var caption = cut.Find(".tm-notion-image-block__caption");
        caption.Should().NotBeNull();
        caption.GetAttribute("contenteditable").Should().Be("true");
    }

    // ── PDF-38: Upload dialog je zachován ──────────────────────────────────

    [Fact]
    public void Render_WithoutUrl_DisplaysUploadZone()
    {
        var cut = Render<TmNotionPdfBlock>(parameters =>
            parameters.Add(p => p.Content, CreateContent())
                      .Add(p => p.ReadOnly, false));

        cut.Find(".tm-notion-media-upload-zone").Should().NotBeNull();
    }

    // ── PDF-39: ReadOnly mód skryje upload ─────────────────────────────────

    [Fact]
    public void Render_WithoutUrl_ReadOnly_ShowsEmptyPlaceholder()
    {
        var cut = Render<TmNotionPdfBlock>(parameters =>
            parameters.Add(p => p.Content, CreateContent())
                      .Add(p => p.ReadOnly, true));

        cut.Find(".tm-notion-media-empty-placeholder").Should().NotBeNull();
        cut.FindAll(".tm-notion-media-upload-zone").Should().BeEmpty();
    }

    // ── PDF-40: Focus handling je zachováno ────────────────────────────────

    [Fact]
    public void Render_HasFocusHandler()
    {
        var cut = Render<TmNotionPdfBlock>(parameters =>
            parameters.Add(p => p.Content, CreateContent("https://example.com/test.pdf")));

        var root = cut.Find(".tm-notion-pdf-block");
        root.HasAttribute("blazor:onfocus").Should().BeTrue();
    }

    private class TestPdfBlockContent : IPdfBlockContent
    {
        public string Url { get; set; } = string.Empty;
        public string? FileId { get; set; }
        public string? Caption { get; set; }
        public int? Width { get; set; }
    }
}
