using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentOutlinePanelTests : LocalizationTestBase
{
    private static IReadOnlyList<DocumentOutlineItem> EmptyOutline => [];

    private static IReadOnlyList<DocumentOutlineItem> SampleOutline =>
    [
        new DocumentOutlineItem("h1", 1, "Chapter 1"),
        new DocumentOutlineItem("h2", 2, "Section 1.1"),
        new DocumentOutlineItem("h3", 1, "Chapter 2"),
    ];

    // ── 14.3.3 – TmDocumentOutlinePanel rendering ─────────────────────────

    [Fact]
    public void OutlinePanel_HasExpectedTestId()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, EmptyOutline));

        cut.Find("[data-testid='document-outline-panel']").Should().NotBeNull();
    }

    [Fact]
    public void OutlinePanel_EmptyOutline_ShowsEmptyMessage()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, EmptyOutline));

        cut.Find("[data-testid='document-outline-empty']").Should().NotBeNull();
        cut.FindAll("[data-testid='document-outline-item']").Should().BeEmpty();
    }

    [Fact]
    public void OutlinePanel_WithItems_RendersAllItems()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline));

        cut.FindAll("[data-testid='document-outline-item']").Should().HaveCount(3);
    }

    [Fact]
    public void OutlinePanel_ItemsShowHeadingText()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline));

        var items = cut.FindAll("[data-testid='document-outline-item']");
        items[0].TextContent.Trim().Should().Be("Chapter 1");
        items[1].TextContent.Trim().Should().Be("Section 1.1");
        items[2].TextContent.Trim().Should().Be("Chapter 2");
    }

    [Fact]
    public void OutlinePanel_ItemsHaveDataLevel()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline));

        var items = cut.FindAll("[data-testid='document-outline-item']");
        items[0].GetAttribute("data-level").Should().Be("1");
        items[1].GetAttribute("data-level").Should().Be("2");
        items[2].GetAttribute("data-level").Should().Be("1");
    }

    [Fact]
    public void OutlinePanel_ClickItem_InvokesNavigateCallback()
    {
        string? navigatedBlockId = null;
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline)
            .Add(p => p.OnNavigateToBlock, EventCallback.Factory.Create<string>(this, id => navigatedBlockId = id)));

        cut.FindAll("[data-testid='document-outline-item']")[1]
           .QuerySelector("button")!.Click();

        navigatedBlockId.Should().Be("h2");
    }

    [Fact]
    public void OutlinePanel_WithItems_RendersMinimapMarkers()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline));

        cut.Find("[data-testid='document-outline-minimap']").Should().NotBeNull();
        cut.FindAll("[data-testid='document-outline-minimap-marker']").Should().HaveCount(3);
    }

    [Fact]
    public void OutlinePanel_ClickMinimapMarker_InvokesNavigateCallback()
    {
        string? navigatedBlockId = null;
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline)
            .Add(p => p.OnNavigateToBlock, EventCallback.Factory.Create<string>(this, id => navigatedBlockId = id)));

        cut.FindAll("[data-testid='document-outline-minimap-marker']")[2].Click();

        navigatedBlockId.Should().Be("h3");
    }

    [Fact]
    public void OutlinePanel_ActiveBlockId_HighlightsMatchingHeading()
    {
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, SampleOutline)
            .Add(p => p.ActiveBlockId, "h2"));

        var item = cut.FindAll("[data-testid='document-outline-item']")[1];
        item.GetAttribute("data-active").Should().Be("true");
        item.QuerySelector("button")!.GetAttribute("aria-current").Should().Be("location");
        cut.FindAll("[data-testid='document-outline-minimap-marker']")[1]
            .GetAttribute("aria-current")
            .Should()
            .Be("location");
    }

    [Fact]
    public void OutlinePanel_NullOutline_ThrowsOrRendersEmpty()
    {
        var act = () => Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, null!));

        act.Should().NotThrow();
        var cut = Render<TmDocumentOutlinePanel>(parameters => parameters
            .Add(p => p.Outline, null!));
        cut.Find("[data-testid='document-outline-empty']").Should().NotBeNull();
    }
}
