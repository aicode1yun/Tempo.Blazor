using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Blocks.Lists;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// A toggle owns its children exactly like a column does. Its block list must therefore identify
/// itself with the page and parent ids, and must accept blocks dragged in from another list —
/// without them the drop target is anonymous and the nesting is lost.
/// </summary>
public sealed class TmNotionToggleDragDropTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("aaaa1111-2222-3333-4444-555566667777");

    [Fact]
    public void ToggleBlockList_IdentifiesItsPageAndParent()
    {
        var (cut, toggleId) = RenderToggle();

        var list = cut.FindComponent<TmNotionBlockList>().Instance;

        list.PageId.Should().Be(PageId, "the drop target must name its page");
        list.ParentBlockId.Should().Be(toggleId, "children dropped here belong to the toggle");
    }

    [Fact]
    public void ToggleBlockList_AcceptsBlocksDraggedInFromAnotherList()
    {
        var (cut, _) = RenderToggle();

        var list = cut.FindComponent<TmNotionBlockList>().Instance;

        list.OnExternalBlockDropped.HasDelegate.Should().BeTrue();
        list.OnExternalBlockRemoved.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public async Task DroppingABlockIntoTheToggle_MovesItThroughTheProvider()
    {
        var (cut, toggleId) = RenderToggle();

        var request = new MoveNotionBlockRequest(
            Guid.NewGuid().ToString(), PageId.ToString(), null, toggleId.ToString(), 0);

        var list = cut.FindComponent<TmNotionBlockList>().Instance;
        await cut.InvokeAsync(() => list.OnExternalBlockDropped.InvokeAsync(request));

        await _provider.Received(1).MoveBlockAsync(request);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private INotionBlockProvider _provider = default!;

    private (IRenderedComponent<TmNotionToggleBlock> Cut, Guid ToggleId) RenderToggle()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _provider = Substitute.For<INotionBlockProvider>();
        _provider.GetChildBlocksAsync(Arg.Any<string>()).Returns([]);

        var toggleId = Guid.NewGuid();
        var block = new PageBlock
        {
            Id = toggleId,
            PageId = PageId,
            Type = BlockType.Toggle,
            Content = new ToggleBlockContent { Html = "toggle", IsOpen = true }
        };

        var cut = RenderComponent<TmNotionToggleBlock>(parameters => parameters
            .AddCascadingValue(new NotionEditorContext { BlockProvider = _provider })
            .Add(p => p.Block, block)
            .Add(p => p.Content, (IToggleBlockContent)block.Content));

        return (cut, toggleId);
    }
}
