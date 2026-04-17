using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramDocumentExtensionsTests
{
    [Fact]
    public void CloneWithNewIds_Generates_New_Document_Id()
    {
        var original = CreateSampleDocument();
        var clone = original.CloneWithNewIds();

        clone.Id.Should().NotBe(original.Id);
    }

    [Fact]
    public void CloneWithNewIds_Generates_New_Page_Id()
    {
        var original = CreateSampleDocument();
        var clone = original.CloneWithNewIds();

        clone.Pages[0].Id.Should().NotBe(original.Pages[0].Id);
    }

    [Fact]
    public void CloneWithNewIds_Generates_New_Node_Ids()
    {
        var original = CreateSampleDocument();
        var originalNodeIds = original.Pages[0].Nodes.Select(n => n.Id).ToHashSet();

        var clone = original.CloneWithNewIds();
        var cloneNodeIds = clone.Pages[0].Nodes.Select(n => n.Id).ToHashSet();

        cloneNodeIds.Should().NotIntersectWith(originalNodeIds);
    }

    [Fact]
    public void CloneWithNewIds_Generates_New_Port_Ids()
    {
        var original = CreateSampleDocument();
        var originalPortIds = original.Pages[0].Nodes.SelectMany(n => n.Ports).Select(p => p.Id).ToHashSet();

        var clone = original.CloneWithNewIds();
        var clonePortIds = clone.Pages[0].Nodes.SelectMany(n => n.Ports).Select(p => p.Id).ToHashSet();

        clonePortIds.Should().NotIntersectWith(originalPortIds);
    }

    [Fact]
    public void CloneWithNewIds_Generates_New_Edge_Ids()
    {
        var original = CreateSampleDocument();
        var originalEdgeIds = original.Pages[0].Edges.Select(e => e.Id).ToHashSet();

        var clone = original.CloneWithNewIds();
        var cloneEdgeIds = clone.Pages[0].Edges.Select(e => e.Id).ToHashSet();

        cloneEdgeIds.Should().NotIntersectWith(originalEdgeIds);
    }

    [Fact]
    public void CloneWithNewIds_Generates_New_Layer_Ids()
    {
        var original = CreateSampleDocument();
        var originalLayerIds = original.Pages[0].Layers.Select(l => l.Id).ToHashSet();

        var clone = original.CloneWithNewIds();
        var cloneLayerIds = clone.Pages[0].Layers.Select(l => l.Id).ToHashSet();

        cloneLayerIds.Should().NotIntersectWith(originalLayerIds);
    }

    [Fact]
    public void CloneWithNewIds_Remaps_Edge_Node_References()
    {
        var original = CreateSampleDocument();
        var clone = original.CloneWithNewIds();

        var nodeIdMap = new Dictionary<string, string>();
        for (int i = 0; i < clone.Pages[0].Nodes.Count; i++)
        {
            nodeIdMap[original.Pages[0].Nodes[i].Id] = clone.Pages[0].Nodes[i].Id;
        }

        foreach (var edge in clone.Pages[0].Edges)
        {
            nodeIdMap.Values.Should().Contain(edge.SourceNodeId);
            nodeIdMap.Values.Should().Contain(edge.TargetNodeId);
        }
    }

    [Fact]
    public void CloneWithNewIds_Remaps_Edge_Port_References()
    {
        var original = CreateSampleDocument();
        var clone = original.CloneWithNewIds();

        var originalPorts = original.Pages[0].Nodes.SelectMany(n => n.Ports).ToList();
        var clonedPorts = clone.Pages[0].Nodes.SelectMany(n => n.Ports).ToList();
        var portIdMap = new Dictionary<string, string>();
        for (int i = 0; i < clonedPorts.Count; i++)
        {
            portIdMap[originalPorts[i].Id] = clonedPorts[i].Id;
        }

        foreach (var edge in clone.Pages[0].Edges)
        {
            if (!string.IsNullOrEmpty(edge.SourcePortId))
                portIdMap.Values.Should().Contain(edge.SourcePortId);
            if (!string.IsNullOrEmpty(edge.TargetPortId))
                portIdMap.Values.Should().Contain(edge.TargetPortId);
        }
    }

    [Fact]
    public void CloneWithNewIds_Remaps_Node_Group_And_Parent_References()
    {
        var original = new DiagramDocument();
        original.EnsurePages();
        var page = original.Pages[0];

        var groupNode = new DiagramNode { StencilId = "general.group" };
        var childNode = new DiagramNode
        {
            StencilId = "general.rectangle",
            ParentNodeId = groupNode.Id,
            ParentGroupId = groupNode.Id,
            GroupId = groupNode.Id
        };
        page.Nodes.Add(groupNode);
        page.Nodes.Add(childNode);

        var clone = original.CloneWithNewIds();
        var clonedGroup = clone.Pages[0].Nodes.First(n => n.StencilId == "general.group");
        var clonedChild = clone.Pages[0].Nodes.First(n => n.StencilId == "general.rectangle");

        clonedChild.ParentNodeId.Should().Be(clonedGroup.Id);
        clonedChild.ParentGroupId.Should().Be(clonedGroup.Id);
        clonedChild.GroupId.Should().Be(clonedGroup.Id);
    }

    [Fact]
    public void CloneWithNewIds_Remaps_Node_Layer_References()
    {
        var original = new DiagramDocument();
        original.EnsurePages();
        var page = original.Pages[0];

        var layer = new DiagramLayer { Name = "Test Layer" };
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            LayerId = layer.Id
        };
        page.Layers.Add(layer);
        page.Nodes.Add(node);

        var clone = original.CloneWithNewIds();
        var clonedLayer = clone.Pages[0].Layers[0];
        var clonedNode = clone.Pages[0].Nodes[0];

        clonedNode.LayerId.Should().Be(clonedLayer.Id);
        clonedNode.LayerId.Should().NotBe(layer.Id);
    }

    [Fact]
    public void CloneWithNewIds_Preserves_Structure_And_Data()
    {
        var original = CreateSampleDocument();
        var clone = original.CloneWithNewIds();

        clone.Pages.Count.Should().Be(original.Pages.Count);
        clone.Pages[0].Nodes.Count.Should().Be(original.Pages[0].Nodes.Count);
        clone.Pages[0].Edges.Count.Should().Be(original.Pages[0].Edges.Count);
        clone.Pages[0].Layers.Count.Should().Be(original.Pages[0].Layers.Count);

        var originalNode = original.Pages[0].Nodes[0];
        var clonedNode = clone.Pages[0].Nodes[0];
        clonedNode.X.Should().Be(originalNode.X);
        clonedNode.Y.Should().Be(originalNode.Y);
        clonedNode.W.Should().Be(originalNode.W);
        clonedNode.H.Should().Be(originalNode.H);
        clonedNode.Data["label"]!.ToString().Should().Be(originalNode.Data["label"]!.ToString());
    }

    [Fact]
    public void CloneWithNewIds_Updates_Timestamps()
    {
        var original = CreateSampleDocument();
        original.CreatedAt = DateTime.UtcNow.AddDays(-1);
        original.ModifiedAt = DateTime.UtcNow.AddDays(-1);

        var clone = original.CloneWithNewIds();

        clone.CreatedAt.Should().BeAfter(original.CreatedAt);
        clone.ModifiedAt.Should().BeAfter(original.ModifiedAt);
    }

    private static DiagramDocument CreateSampleDocument()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        var page = doc.Pages[0];

        var layer = new DiagramLayer { Name = "Default" };
        page.Layers.Add(layer);

        var node1 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 100,
            Y = 100,
            W = 120,
            H = 60,
            Data = new Dictionary<string, object> { ["label"] = "Start" },
            LayerId = layer.Id
        };
        node1.Ports.Add(new DiagramPort { Name = "right", Side = PortSide.Right, Offset = 0.5 });

        var node2 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 300,
            Y = 100,
            W = 120,
            H = 60,
            Data = new Dictionary<string, object> { ["label"] = "End" },
            LayerId = layer.Id
        };
        node2.Ports.Add(new DiagramPort { Name = "left", Side = PortSide.Left, Offset = 0.5 });

        page.Nodes.Add(node1);
        page.Nodes.Add(node2);

        page.Edges.Add(new DiagramEdge
        {
            SourceNodeId = node1.Id,
            TargetNodeId = node2.Id,
            SourcePortId = node1.Ports[0].Id,
            TargetPortId = node2.Ports[0].Id,
            Routing = "straight"
        });

        return doc;
    }
}
