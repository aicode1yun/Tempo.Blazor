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
            InlineId = "inline-2",
            InlineIndex = 2,
            TextOffset = 7,
            BlockOffset = 42
        };

        pos.BlockId.Should().Be("block-1");
        pos.InlineId.Should().Be("inline-2");
        pos.InlineIndex.Should().Be(2);
        pos.TextOffset.Should().Be(7);
        pos.BlockOffset.Should().Be(42);
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

    [Fact]
    public void DocumentSelection_CarriesStableSelectionTokenAndBoundaryDiagnostics()
    {
        var tokenData = new
        {
            Anchor = new { BlockId = "b1", InlineId = "i1", LogicalOffset = 2 },
            Focus = new { BlockId = "b1", InlineId = "i3", LogicalOffset = 9 }
        };
        var selection = new DocumentSelection
        {
            Anchor = new DocumentPosition
            {
                BlockId = "b1",
                InlineId = "i1",
                InlineIndex = 0,
                TextOffset = 2,
                BlockOffset = 2
            },
            Focus = new DocumentPosition
            {
                BlockId = "b1",
                InlineId = "i3",
                InlineIndex = 2,
                TextOffset = 1,
                BlockOffset = 9
            },
            SelectionToken = "stable-selection-token",
            StableSelectionToken = "stable-selection-token",
            SelectionTokenData = tokenData
        };

        selection.IsCollapsed.Should().BeFalse();
        selection.Start.BlockOffset.Should().Be(2);
        selection.End.BlockOffset.Should().Be(9);
        selection.SelectionToken.Should().Be("stable-selection-token");
        selection.StableSelectionToken.Should().Be(selection.SelectionToken);
        selection.SelectionTokenData.Should().BeSameAs(tokenData);
    }
}
