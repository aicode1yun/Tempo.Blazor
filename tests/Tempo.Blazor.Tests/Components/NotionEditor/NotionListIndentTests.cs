using FluentAssertions;
using Bunit;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Tab may push a list item at most one level below the item above it, and does nothing at all on
/// the first block. Anything else renders a list with a gap in its nesting.
/// </summary>
public sealed class NotionListIndentTests
{
    [Theory]
    [InlineData(0, 0, 1)]   // one level under a top-level item
    [InlineData(1, 1, 2)]
    [InlineData(0, 2, 1)]   // never jumps more than one level at a time
    [InlineData(1, 0, 1)]   // already as deep as allowed
    [InlineData(2, 0, 2)]
    public void Indent_NeverGoesMoreThanOneLevelBelowThePreviousItem(int current, int previous, int expected) =>
        NotionListIndent.Next(current, outdent: false, previousIndentLevel: previous).Should().Be(expected);

    [Fact]
    public void Indent_OnTheFirstBlock_DoesNothing() =>
        NotionListIndent.Next(0, outdent: false, previousIndentLevel: null).Should().Be(0);

    [Fact]
    public void Indent_WhenTheBlockAboveIsNotAListItem_DoesNothing() =>
        NotionListIndent.Next(2, outdent: false, previousIndentLevel: null).Should().Be(2);

    [Fact]
    public void Indent_NeverExceedsTheMaximumLevel() =>
        NotionListIndent.Next(NotionListIndent.MaxLevel, outdent: false, previousIndentLevel: NotionListIndent.MaxLevel)
            .Should().Be(NotionListIndent.MaxLevel);

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void Outdent_StepsBackOneLevelAndStopsAtZero(int current, int expected) =>
        NotionListIndent.Next(current, outdent: true, previousIndentLevel: 0).Should().Be(expected);

    [Fact]
    public void Outdent_OnTheFirstBlock_StillWorks() =>
        NotionListIndent.Next(1, outdent: true, previousIndentLevel: null).Should().Be(0);
}

/// <summary>The block list must tell each block what sits above it, or the indent rule has no input.</summary>
public sealed class TmNotionBlockListIndentWiringTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("12121212-3434-5656-7878-909090909090");

    [Fact]
    public void EachBlockLearnsTheIndentLevelOfTheListItemAboveIt()
    {
        var blocks = new List<IPageBlock>
        {
            Paragraph(),
            ListItem(indent: 0),
            ListItem(indent: 1),
            Paragraph(),
            ListItem(indent: 2)
        };

        var cut = RenderComponent<TmNotionBlockList>(parameters => parameters
            .AddCascadingValue(new NotionEditorContext { BlockProvider = Substitute.For<INotionBlockProvider>() })
            .Add(p => p.PageId, PageId)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Blocks, blocks));

        var levels = cut.FindComponents<TmNotionBlock>()
            .Select(component => component.Instance.PreviousListIndentLevel)
            .ToList();

        levels[0].Should().BeNull("the first block has nothing above it");
        levels[1].Should().BeNull("a paragraph is not a list item");
        levels[2].Should().Be(0);
        levels[3].Should().Be(1);
        levels[4].Should().BeNull("the block above is a paragraph again");
    }

    private static PageBlock Paragraph() => new()
    {
        Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.Paragraph,
        Content = new TextBlockContent { Html = "x" }
    };

    private static PageBlock ListItem(int indent) => new()
    {
        Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.BulletList,
        Content = new ListBlockContent { Html = "x", IndentLevel = indent }
    };
}
