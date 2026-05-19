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
}
