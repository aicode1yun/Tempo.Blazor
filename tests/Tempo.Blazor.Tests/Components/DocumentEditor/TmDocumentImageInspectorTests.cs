using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class TmDocumentImageInspectorTests : LocalizationTestBase
{
    [Fact]
    public void Inspector_RendersAltWarning_WhenAltTextIsEmpty()
    {
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent()));

        cut.Find("[data-testid='document-image-inspector']").Should().NotBeNull();
        cut.Find("[data-testid='document-image-inspector-alt-warning']")
            .TextContent.Should().Contain("alt text");
    }

    [Fact]
    public void Inspector_AltInput_RaisesAltTextChanged()
    {
        string? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { AltText = "Old" })
            .Add(p => p.AltTextChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-alt']").Change("New alt");

        received.Should().Be("New alt");
    }

    [Fact]
    public void Inspector_DecorativeCheckbox_RaisesDecorativeChangedAndSuppressesAltWarning()
    {
        bool? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { IsDecorative = true })
            .Add(p => p.DecorativeChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-decorative']")
            .HasAttribute("checked").Should().BeTrue();
        cut.FindAll("[data-testid='document-image-inspector-alt-warning']").Should().BeEmpty();
        cut.Find("[data-testid='document-image-inspector-alt']")
            .HasAttribute("disabled").Should().BeTrue();

        cut.Find("[data-testid='document-image-inspector-decorative']").Change(false);

        received.Should().BeFalse();
    }

    [Fact]
    public void Inspector_WrapButton_RaisesWrapModeChanged()
    {
        DocumentWrapMode? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent())
            .Add(p => p.WrapModeChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-wrap-square']").Click();

        received.Should().Be(DocumentWrapMode.Square);
    }

    [Fact]
    public void Inspector_CaptionCheckboxReflectsCaption()
    {
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Caption = "Evidence caption" }));

        cut.Find("[data-testid='document-image-inspector-caption-toggle']")
            .HasAttribute("checked").Should().BeTrue();
        cut.Find("[data-testid='document-image-inspector-caption']")
            .GetAttribute("value").Should().Be("Evidence caption");
    }

    [Fact]
    public void Inspector_CaptionInput_RaisesCaptionChanged()
    {
        string? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Caption = "Old caption" })
            .Add(p => p.CaptionChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-caption']").Change("New caption");

        received.Should().Be("New caption");
    }

    [Fact]
    public async Task Inspector_CaptionInput_DebouncesInputChanges()
    {
        string? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Caption = "Old caption" })
            .Add(p => p.CaptionChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-caption']").Input("Live caption");

        await Task.Delay(400);
        received.Should().Be("Live caption");
    }

    [Fact]
    public void Inspector_UncheckingCaption_RaisesEmptyCaption()
    {
        string? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Caption = "Existing caption" })
            .Add(p => p.CaptionChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-caption-toggle']").Change(false);

        received.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DocumentImageHorizontalPosition.Left, "document-image-inspector-align-start")]
    [InlineData(DocumentImageHorizontalPosition.Center, "document-image-inspector-align-center")]
    [InlineData(DocumentImageHorizontalPosition.Right, "document-image-inspector-align-end")]
    public void Inspector_UsesFloatingLayoutHorizontalPosition_ForActiveAlignment(
        DocumentImageHorizontalPosition horizontalPosition,
        string activeTestId)
    {
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent
            {
                Alignment = DocumentImageAlignment.Center,
                FloatingLayout = new DocumentFloatingLayout
                {
                    Inline = false,
                    WrapMode = DocumentWrapMode.Square,
                    HorizontalPosition = horizontalPosition
                }
            }));

        cut.Find($"[data-testid='{activeTestId}']").ClassList
            .Should().Contain("tm-document-image-inspector__swatch--active");
    }

    [Fact]
    public void Inspector_SizeInput_RaisesSizeChanged()
    {
        DocumentImageSize? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Size = new DocumentImageSize { Width = 120, Height = 80 } })
            .Add(p => p.SizeChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-width']").Change("240");

        received.Should().NotBeNull();
        received!.Width.Should().Be(240);
        received.Height.Should().Be(80);
    }

    [Fact]
    public async Task Inspector_SizeInput_DebouncesInputChanges()
    {
        DocumentImageSize? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Size = new DocumentImageSize { Width = 120, Height = 80 } })
            .Add(p => p.SizeChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-width']").Input("260");

        await Task.Delay(400);
        received.Should().NotBeNull();
        received!.Width.Should().Be(260);
        received.Height.Should().Be(80);
    }

    [Fact]
    public async Task Inspector_UrlInput_DebouncesInputChanges_ForUrlImages()
    {
        string? received = null;
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Source = DocumentImageSource.Url, Url = "https://old.example/image.png" })
            .Add(p => p.UrlChanged, value => received = value));

        cut.Find("[data-testid='document-image-inspector-link']")
            .GetAttribute("value").Should().Be("https://old.example/image.png");
        cut.Find("[data-testid='document-image-inspector-link']").Input("https://new.example");

        await Task.Delay(400);
        received.Should().Be("https://new.example");
    }

    [Fact]
    public void Inspector_HidesLinkInput_ForAssetImages()
    {
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Source = DocumentImageSource.Asset, AssetId = "evidence" }));

        cut.FindAll("[data-testid='document-image-inspector-link']").Should().BeEmpty();
    }

    [Fact]
    public void Inspector_HidesUrlInput_ForEmbeddedDataImages()
    {
        var cut = RenderComponent<TmDocumentImageInspector>(parameters => parameters
            .Add(p => p.Image, new ImageBlockContent { Source = DocumentImageSource.Url, Url = "data:image/png;base64,iVBORw0KGgo=" }));

        cut.FindAll("[data-testid='document-image-inspector-link']").Should().BeEmpty();
    }
}
