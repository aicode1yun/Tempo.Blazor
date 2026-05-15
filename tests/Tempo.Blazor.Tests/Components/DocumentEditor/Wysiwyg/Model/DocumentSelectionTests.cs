using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.Wysiwyg.Model;

public class DocumentSelectionTests
{
    [Fact]
    public void DocumentSelection_Collapsed_WhenAnchorEqualsFocus()
    {
        var pos = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var selection = new DocumentSelection { Anchor = pos, Focus = pos };

        selection.IsCollapsed.Should().BeTrue();
    }

    [Fact]
    public void DocumentSelection_NotCollapsed_WhenAnchorDiffersFromFocus()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.IsCollapsed.Should().BeFalse();
    }

    [Fact]
    public void DocumentSelection_Start_ReturnsAnchorWhenForward()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.Start.Should().BeEquivalentTo(anchor);
    }

    [Fact]
    public void DocumentSelection_Start_ReturnsFocusWhenBackward()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.Start.Should().BeEquivalentTo(focus);
    }

    [Fact]
    public void DocumentSelection_End_ReturnsFocusWhenForward()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.End.Should().BeEquivalentTo(focus);
    }

    [Fact]
    public void DocumentSelection_End_ReturnsAnchorWhenBackward()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.End.Should().BeEquivalentTo(anchor);
    }

    [Fact]
    public void DocumentPosition_HasBlockIdInlineIndexAndTextOffset()
    {
        var pos = new DocumentPosition
        {
            BlockId = "block-1",
            InlineIndex = 2,
            TextOffset = 7
        };

        pos.BlockId.Should().Be("block-1");
        pos.InlineIndex.Should().Be(2);
        pos.TextOffset.Should().Be(7);
    }

    [Fact]
    public void DocumentSelection_IsForward_WhenFocusAfterAnchor()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.IsForward.Should().BeTrue();
    }

    [Fact]
    public void DocumentSelection_IsNotForward_WhenFocusBeforeAnchor()
    {
        var anchor = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 10 };
        var focus = new DocumentPosition { BlockId = "b1", InlineIndex = 0, TextOffset = 5 };
        var selection = new DocumentSelection { Anchor = anchor, Focus = focus };

        selection.IsForward.Should().BeFalse();
    }
}
