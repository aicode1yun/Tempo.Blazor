using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks.Media;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// CBP-01..03: Clipboard paste on TmNotionImageBlock.
/// TDD: tests written before implementation — initially expected to FAIL (RED),
/// then pass after implementation (GREEN).
/// </summary>
public class TmNotionImageBlockPasteTests : LocalizationTestBase
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static readonly string SampleDataUrl =
        "data:image/jpeg;base64," + Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF });

    private static NotionEditorContext BuildContext(INotionFileProvider? fileProvider = null)
        => new()
        {
            DataProvider  = Substitute.For<INotionDataProvider>(),
            BlockProvider = Substitute.For<INotionBlockProvider>(),
            FileProvider  = fileProvider,
        };

    private static INotionFileProvider ProviderThatUploads(
        string fileId = "file-123",
        string url    = "https://cdn.example.com/image.jpg")
    {
        var p = Substitute.For<INotionFileProvider>();
        p.UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>())
         .Returns(fileId);
        p.GetFileUrlAsync(fileId).Returns(url);
        return p;
    }

    private static IImageBlockContent EmptyImageContent() => new TestImageContent();

    // ── CBP-01: OnImagePasted calls UploadFileAsync ───────────────────────────

    [Fact]
    public async Task OnImagePasted_CallsUploadFileAsync_WithCorrectMimeAndName()
    {
        var provider = ProviderThatUploads();
        var ctx = BuildContext(fileProvider: provider);

        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, false)
            .AddCascadingValue(ctx));

        await cut.InvokeAsync(() =>
            cut.Instance.OnImagePasted(SampleDataUrl, "image/jpeg", "paste.jpg"));

        await provider.Received(1).UploadFileAsync(
            Arg.Any<Stream>(), "paste.jpg", "image/jpeg");
    }

    // ── CBP-02: OnImagePasted fires OnMediaSet with fileId + url ─────────────

    [Fact]
    public async Task OnImagePasted_FiresOnMediaSetWithFileIdAndUrl()
    {
        const string ExpectedFileId = "img-999";
        const string ExpectedUrl    = "https://cdn.example.com/img-999.jpg";

        var provider = ProviderThatUploads(fileId: ExpectedFileId, url: ExpectedUrl);
        var ctx = BuildContext(fileProvider: provider);

        (string? FileId, string? Url) confirmed = default;

        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, false)
            .Add(x => x.OnMediaSet,
                 EventCallback.Factory.Create<(string?, string?)>(
                     this, args => confirmed = args))
            .AddCascadingValue(ctx));

        await cut.InvokeAsync(() =>
            cut.Instance.OnImagePasted(SampleDataUrl, "image/jpeg", "photo.jpg"));

        confirmed.FileId.Should().Be(ExpectedFileId);
        confirmed.Url.Should().Be(ExpectedUrl);
    }

    // ── CBP-03: ReadOnly mode — OnImagePasted does nothing ────────────────────

    [Fact]
    public async Task OnImagePasted_ReadOnly_DoesNotUpload()
    {
        var provider = ProviderThatUploads();
        var ctx = BuildContext(fileProvider: provider);

        var cut = RenderComponent<TmNotionImageBlock>(p => p
            .Add(x => x.Content,  EmptyImageContent())
            .Add(x => x.ReadOnly, true)
            .AddCascadingValue(ctx));

        await cut.InvokeAsync(() =>
            cut.Instance.OnImagePasted(SampleDataUrl, "image/jpeg", "paste.jpg"));

        await provider.DidNotReceive().UploadFileAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Stub ─────────────────────────────────────────────────────────────────

    private class TestImageContent : IImageBlockContent
    {
        public string         Url       { get; set; } = string.Empty;
        public string?        FileId    { get; set; }
        public string?        Caption   { get; set; }
        public int?           Width     { get; set; }
        public string?        AltText   { get; set; }
        public MediaAlignment Alignment { get; set; }
    }
}
