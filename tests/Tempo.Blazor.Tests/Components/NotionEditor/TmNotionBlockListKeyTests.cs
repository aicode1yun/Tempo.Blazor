using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// The block list must key its children by block id. Without @key Blazor diffs by index, so
/// inserting a block above a still-unsaved block hands that block's component a different
/// Block and overwrites its dirty DOM.
/// </summary>
public sealed class TmNotionBlockListKeyTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public void InsertingBlockAbove_PreservesTheComponentInstancesOfExistingBlocks()
    {
        var first = Paragraph("first");
        var second = Paragraph("second");

        var cut = RenderList([first, second]);

        var before = cut.FindComponents<TmNotionBlock>()
            .ToDictionary(component => component.Instance.Block.Id, component => component.Instance);
        before.Should().HaveCount(2);

        // Prepend a block: every existing block shifts down by one index.
        var inserted = Paragraph("inserted");
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Blocks, new List<IPageBlock> { inserted, first, second }));

        var after = cut.FindComponents<TmNotionBlock>()
            .ToDictionary(component => component.Instance.Block.Id, component => component.Instance);
        after.Should().HaveCount(3);

        after[first.Id].Should().BeSameAs(before[first.Id],
            "the component that owns the first block must be reused, not handed the inserted block");
        after[second.Id].Should().BeSameAs(before[second.Id],
            "the component that owns the second block must keep its own DOM");
    }

    [Fact]
    public void RemovingBlockAbove_PreservesTheComponentInstanceOfTheBlockBelow()
    {
        var first = Paragraph("first");
        var second = Paragraph("second");

        var cut = RenderList([first, second]);
        var beforeSecond = cut.FindComponents<TmNotionBlock>()
            .Single(component => component.Instance.Block.Id == second.Id)
            .Instance;

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Blocks, new List<IPageBlock> { second }));

        var afterSecond = cut.FindComponents<TmNotionBlock>()
            .Single(component => component.Instance.Block.Id == second.Id)
            .Instance;

        afterSecond.Should().BeSameAs(beforeSecond);
    }

    [Fact]
    public void ReorderingBlocks_PreservesBothComponentInstances()
    {
        var first = Paragraph("first");
        var second = Paragraph("second");

        var cut = RenderList([first, second]);
        var before = cut.FindComponents<TmNotionBlock>()
            .ToDictionary(component => component.Instance.Block.Id, component => component.Instance);

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Blocks, new List<IPageBlock> { second, first }));

        var after = cut.FindComponents<TmNotionBlock>()
            .ToDictionary(component => component.Instance.Block.Id, component => component.Instance);

        after[first.Id].Should().BeSameAs(before[first.Id]);
        after[second.Id].Should().BeSameAs(before[second.Id]);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private IRenderedComponent<TmNotionBlockList> RenderList(IReadOnlyList<IPageBlock> blocks)
    {
        var context = new NotionEditorContext
        {
            BlockProvider = Substitute.For<INotionBlockProvider>()
        };

        return RenderComponent<TmNotionBlockList>(parameters => parameters
            .AddCascadingValue(context)
            .Add(p => p.PageId, PageId)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Blocks, blocks.ToList()));
    }

    private static IPageBlock Paragraph(string html) => new PageBlock
    {
        Id = Guid.NewGuid(),
        PageId = PageId,
        Type = BlockType.Paragraph,
        Order = 0,
        Content = new TextBlockContent { Html = html }
    };
}
