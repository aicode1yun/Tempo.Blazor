using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class SmartLinkProviderContractTests
{
    [Fact]
    public async Task SmartLinkProvider_ResolvesPreviewMetadata()
    {
        ISmartLinkProvider provider = new ContractSmartLinkProvider();

        var dto = await provider.ResolveAsync("https://docs.tempo.local/smart-links");

        dto.Should().NotBeNull();
        dto!.Url.Should().Be("https://docs.tempo.local/smart-links");
        dto.Title.Should().Be("Tempo Smart Links");
        dto.FaviconUrl.Should().Be("https://docs.tempo.local/favicon.ico");
        dto.Description.Should().Be("Smart link preview metadata.");
        dto.ImageUrl.Should().Be("https://docs.tempo.local/smart-links.png");
        dto.ProviderName.Should().Be("Tempo Docs");
    }

    [Fact]
    public void SmartLinkDisplay_ContainsInlineAndCardModes()
    {
        Enum.GetValues<SmartLinkDisplay>()
            .Should()
            .BeEquivalentTo([SmartLinkDisplay.Inline, SmartLinkDisplay.Card]);
    }

    private sealed class ContractSmartLinkProvider : ISmartLinkProvider
    {
        public Task<SmartLinkDto?> ResolveAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult<SmartLinkDto?>(new SmartLinkDto(
                url,
                "Tempo Smart Links",
                "https://docs.tempo.local/favicon.ico",
                "Smart link preview metadata.",
                "https://docs.tempo.local/smart-links.png",
                "Tempo Docs"));
    }
}
