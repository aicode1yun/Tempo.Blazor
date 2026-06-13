using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Unit tests for the canvas page-image export DTO (<see cref="DocumentPageImage"/>) and its mapping
/// into the signing model (<c>SigningDocumentPage</c>). Plan S1.5: the bridge turns one bitmap per
/// editor page into the page list the signing designer/runner already consume.
/// </summary>
public class DocumentPageImageMappingTests
{
    [Fact]
    public void DocumentPageImage_CarriesPageGeometryAndDataUrl()
    {
        var image = new DocumentPageImage
        {
            PageIndex = 2,
            Width = 794,
            Height = 1123,
            Scale = 2,
            DataUrl = "data:image/png;base64,AAAA"
        };

        image.PageIndex.Should().Be(2);
        image.Width.Should().Be(794);
        image.Height.Should().Be(1123);
        image.Scale.Should().Be(2);
        image.DataUrl.Should().Be("data:image/png;base64,AAAA");
    }

    [Fact]
    public void ToSigningDocumentPages_MapsEveryPageInOrderWithAttachmentAndImageUrl()
    {
        var images = new[]
        {
            new DocumentPageImage { PageIndex = 0, Width = 794, Height = 1123, Scale = 2, DataUrl = "data:image/png;base64,PAGE0" },
            new DocumentPageImage { PageIndex = 1, Width = 794, Height = 1123, Scale = 2, DataUrl = "data:image/png;base64,PAGE1" }
        };

        var pages = images.ToSigningDocumentPages("editor-export");

        pages.Should().HaveCount(2);
        pages.Select(page => page.PageIndex).Should().ContainInOrder(0, 1);
        pages.Should().OnlyContain(page => page.AttachmentUuid == "editor-export");
        pages[0].ImageUrl.Should().Be("data:image/png;base64,PAGE0");
        pages[1].ImageUrl.Should().Be("data:image/png;base64,PAGE1");
    }

    [Fact]
    public void ToSigningDocumentPages_UsesLogicalPageDimensionsNotBackingResolution()
    {
        // The descriptor width/height are the logical (CSS) page size; the 2x backing resolution lives
        // only inside the data URL pixels. The designer overlays normalized 0..1 fields over the
        // displayed page, so the signing page must carry the logical size.
        var images = new[] { new DocumentPageImage { PageIndex = 0, Width = 612, Height = 792, Scale = 2, DataUrl = "data:image/png;base64,X" } };

        var page = images.ToSigningDocumentPages("editor-export").Single();

        page.Width.Should().Be(612);
        page.Height.Should().Be(792);
    }

    [Fact]
    public void ToSigningDocumentPages_AppliesLocalizedPageLabelFromFactory()
    {
        var images = new[]
        {
            new DocumentPageImage { PageIndex = 0, Width = 794, Height = 1123, Scale = 2, DataUrl = "data:image/png;base64,A" },
            new DocumentPageImage { PageIndex = 1, Width = 794, Height = 1123, Scale = 2, DataUrl = "data:image/png;base64,B" }
        };

        var pages = images.ToSigningDocumentPages("editor-export", pageNumber => $"Strana {pageNumber}");

        pages[0].Label.Should().Be("Strana 1");
        pages[1].Label.Should().Be("Strana 2");
    }

    [Fact]
    public void ToSigningDocumentPages_WithoutLabelFactory_LeavesLabelUnset()
    {
        var images = new[] { new DocumentPageImage { PageIndex = 0, Width = 794, Height = 1123, Scale = 2, DataUrl = "data:image/png;base64,A" } };

        var page = images.ToSigningDocumentPages("editor-export").Single();

        page.Label.Should().BeNull();
    }
}
