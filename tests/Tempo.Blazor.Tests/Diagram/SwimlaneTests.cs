using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class SwimlaneTests : DiagramTestBase
{
    [Fact]
    public void SwimlaneLayoutService_ComputeCell_ReturnsCorrectCell()
    {
        var swimlane = new DiagramNode
        {
            Id = "swim1",
            StencilId = "swimlane.horizontal",
            X = 100,
            Y = 100,
            W = 400,
            H = 200,
            SwimlaneData = new()
            {
                IsHorizontal = true,
                RowCount = 2,
                ColumnCount = 1,
                HeaderSize = 30,
                RowSizes = [80, 80],
                ColumnSizes = []
            }
        };

        // Inside first row (after header)
        var cell1 = SwimlaneLayoutService.ComputeCell(swimlane, 150, 110);
        cell1.Should().Be((0, 0));

        // Inside second row
        var cell2 = SwimlaneLayoutService.ComputeCell(swimlane, 150, 190);
        cell2.Should().Be((1, 0));

        // Inside header area - should return null
        var header = SwimlaneLayoutService.ComputeCell(swimlane, 110, 150);
        header.Should().BeNull();

        // Outside swimlane
        var outside = SwimlaneLayoutService.ComputeCell(swimlane, 50, 50);
        outside.Should().BeNull();
    }

    [Fact]
    public void SwimlaneLayoutService_ArrangeChild_PositionsInsideCell()
    {
        var swimlane = new DiagramNode
        {
            Id = "swim1",
            StencilId = "swimlane.horizontal",
            X = 100,
            Y = 100,
            W = 400,
            H = 200,
            SwimlaneData = new()
            {
                IsHorizontal = true,
                RowCount = 2,
                ColumnCount = 1,
                HeaderSize = 30,
                RowSizes = [80, 80],
                ColumnSizes = []
            }
        };

        var child = new DiagramNode
        {
            Id = "child1",
            ParentNodeId = "swim1",
            SwimlaneRow = 1,
            SwimlaneColumn = 0,
            W = 50,
            H = 30
        };

        SwimlaneLayoutService.ArrangeChild(swimlane, child);

        child.X.Should().Be(140); // 100 + header(30) + 10 offset
        child.Y.Should().Be(190); // 100 + row0(80) + 10 offset
    }

    [Fact]
    public void AddSwimlaneRowCommand_IncreasesHeightAndRowCount()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var swimlane = new DiagramNode
        {
            Id = "swim1",
            StencilId = "swimlane.horizontal",
            W = 400,
            H = 200,
            SwimlaneData = new()
            {
                IsHorizontal = true,
                RowCount = 2,
                ColumnCount = 1,
                HeaderSize = 30,
                RowSizes = [85, 85],
                ColumnSizes = []
            }
        };
        doc.Nodes.Add(swimlane);

        var cmd = new AddSwimlaneRowCommand(doc, swimlane.Id, 2, 80);
        cmd.Execute();

        swimlane.SwimlaneData!.RowCount.Should().Be(3);
        swimlane.H.Should().Be(280);
        swimlane.SwimlaneData.RowSizes.Should().ContainInOrder(85, 85, 80);

        cmd.Undo();

        swimlane.SwimlaneData.RowCount.Should().Be(2);
        swimlane.H.Should().Be(200);
    }

    [Fact]
    public void RemoveSwimlaneRowCommand_DetachesChildrenInRemovedRow()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var swimlane = new DiagramNode
        {
            Id = "swim1",
            StencilId = "swimlane.horizontal",
            W = 400,
            H = 200,
            SwimlaneData = new()
            {
                IsHorizontal = true,
                RowCount = 2,
                ColumnCount = 1,
                HeaderSize = 30,
                RowSizes = [85, 85],
                ColumnSizes = []
            }
        };
        var child = new DiagramNode
        {
            Id = "child1",
            ParentNodeId = "swim1",
            SwimlaneRow = 1,
            SwimlaneColumn = 0
        };
        doc.Nodes.Add(swimlane);
        doc.Nodes.Add(child);

        var cmd = new RemoveSwimlaneRowCommand(doc, swimlane.Id, 1);
        cmd.Execute();

        child.ParentNodeId.Should().BeNull();
        child.SwimlaneRow.Should().Be(-1);
        swimlane.SwimlaneData!.RowCount.Should().Be(1);
        swimlane.H.Should().Be(115);

        cmd.Undo();

        child.ParentNodeId.Should().Be("swim1");
        child.SwimlaneRow.Should().Be(1);
        swimlane.SwimlaneData.RowCount.Should().Be(2);
        swimlane.H.Should().Be(200);
    }

    [Fact]
    public void MoveNodesCommand_UpdatesSwimlaneParentOnEnterAndLeave()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var swimlane = new DiagramNode
        {
            Id = "swim1",
            StencilId = "swimlane.horizontal",
            X = 100,
            Y = 100,
            W = 400,
            H = 200,
            SwimlaneData = new()
            {
                IsHorizontal = true,
                RowCount = 2,
                ColumnCount = 1,
                HeaderSize = 30,
                RowSizes = [85, 85],
                ColumnSizes = []
            }
        };
        var node = new DiagramNode { Id = "n1", X = 0, Y = 0, W = 50, H = 30 };
        doc.Nodes.Add(swimlane);
        doc.Nodes.Add(node);

        // Move into swimlane
        var before = new Dictionary<string, NodeMoveState> { [node.Id] = new(0, 0, null, -1, -1) };
        var after = new Dictionary<string, NodeMoveState> { [node.Id] = new(130, 110, "swim1", 0, 0) };
        var cmd = new MoveNodesCommand(doc, before, after);
        cmd.Execute();

        node.ParentNodeId.Should().Be("swim1");
        node.SwimlaneRow.Should().Be(0);

        cmd.Undo();

        node.ParentNodeId.Should().BeNull();
        node.SwimlaneRow.Should().Be(-1);
    }

    [Fact]
    public void SwimlaneStencil_RenderedWithHeaders()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Nodes =
            [
                new DiagramNode
                {
                    Id = "swim1",
                    StencilId = "swimlane.horizontal",
                    X = 100,
                    Y = 100,
                    W = 400,
                    H = 200,
                    SwimlaneData = new()
                    {
                        IsHorizontal = true,
                        RowCount = 2,
                        ColumnCount = 1,
                        HeaderSize = 30,
                        RowSizes = [85, 85],
                        ColumnSizes = [],
                        CellLabels = ["Lane A", "Lane B"]
                    }
                }
            ]
        };

        var cut = RenderComponent<TmDiagramCanvas>(parameters => parameters
            .Add(p => p.Document, doc));

        var headers = cut.FindAll(".tm-diagram-node__swimlane-header span");
        headers.Count.Should().Be(2);
        headers[0].TextContent.Should().Be("Lane A");
        headers[1].TextContent.Should().Be("Lane B");
    }

    [Fact]
    public void AddSwimlaneRowCommand_IncreasesSwimlaneHeight()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var swimlane = new DiagramNode
        {
            Id = "swim1",
            StencilId = "swimlane.horizontal",
            W = 400,
            H = 200,
            SwimlaneData = new()
            {
                IsHorizontal = true,
                RowCount = 2,
                ColumnCount = 1,
                HeaderSize = 30,
                RowSizes = [85, 85],
                ColumnSizes = []
            }
        };
        doc.Nodes.Add(swimlane);

        var originalHeight = swimlane.H;
        var cmd = new AddSwimlaneRowCommand(doc, swimlane.Id, 2, 100);
        cmd.Execute();

        swimlane.H.Should().Be(originalHeight + 100);
    }
}
