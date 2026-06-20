using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentFontProviderTests
{
    [Fact]
    public async Task InMemoryProvider_ReturnsDefaultFontsAndFallback()
    {
        IDocumentFontProvider provider = new InMemoryDocumentFontProvider();

        var fonts = await provider.GetFontFamiliesAsync(new DocumentFontQuery { DocumentId = "doc-1" });
        var fallback = await provider.GetFallbackFontAsync();

        fonts.Should().NotBeEmpty();
        fonts.Should().Contain(font => font.Key == "aptos" && font.IsFallback);
        fonts.Should().OnlyContain(font => !string.IsNullOrWhiteSpace(font.CssFamily));
        fallback.Key.Should().Be("aptos");
    }

    [Fact]
    public async Task InMemoryProvider_UsesCustomFallbackWhenAvailable()
    {
        IDocumentFontProvider provider = new InMemoryDocumentFontProvider(
            [
                new DocumentFontFamily { Key = "serif", DisplayName = "Serif", CssFamily = "Georgia, serif" },
                new DocumentFontFamily { Key = "sans", DisplayName = "Sans", CssFamily = "Arial, sans-serif" }
            ],
            fallbackKey: "serif");

        var fallback = await provider.GetFallbackFontAsync();

        fallback.CssFamily.Should().Be("Georgia, serif");
    }
}
