using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramSerializerTests
{
    // ── Serialize ─────────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_EmptyDocument_ProducesValidJson()
    {
        var doc = new DiagramDocument();

        var json = DiagramSerializer.Serialize(doc);

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"title\"");
        json.Should().Contain("\"nodes\"");
        json.Should().Contain("\"edges\"");
    }

    [Fact]
    public void Serialize_UsesCamelCase()
    {
        var doc = new DiagramDocument { Title = "Test" };

        var json = DiagramSerializer.Serialize(doc);

        json.Should().Contain("\"title\"");
        json.Should().Contain("\"createdAt\"");
        json.Should().NotContain("\"Title\"");
        json.Should().NotContain("\"CreatedAt\"");
    }

    [Fact]
    public void Serialize_UpdatesModifiedAt()
    {
        var doc = new DiagramDocument();
        var before = DateTime.UtcNow.AddSeconds(-1);

        DiagramSerializer.Serialize(doc);

        doc.ModifiedAt.Should().BeAfter(before);
    }

    [Fact]
    public void Serialize_NullValues_AreOmitted()
    {
        var doc = new DiagramDocument();
        doc.Edges.Add(new DiagramEdge { SourceNodeId = "a", TargetNodeId = "b" /* Label = null */ });

        var json = DiagramSerializer.Serialize(doc);

        json.Should().NotContain("\"label\"");
    }

    // ── Deserialize ───────────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_ValidJson_ReturnsDocument()
    {
        var json = """
            {
              "version": "1.0",
              "title": "UML Diagram",
              "width": 3000,
              "height": 2000,
              "nodes": [],
              "edges": []
            }
            """;

        var doc = DiagramSerializer.Deserialize(json);

        doc.Should().NotBeNull();
        doc.Title.Should().Be("UML Diagram");
        doc.Version.Should().Be("1.0");
        doc.Width.Should().Be(3000);
    }

    [Fact]
    public void Deserialize_WithNodes_MapsCorrectly()
    {
        var json = """
            {
              "version": "1.0",
              "title": "Test",
              "nodes": [
                {
                  "id": "node1",
                  "stencilId": "uml.class",
                  "x": 100,
                  "y": 200,
                  "w": 180,
                  "h": 140,
                  "data": {
                    "name": "Customer"
                  },
                  "ports": [
                    { "id": "p1", "side": 1, "offset": 0.5, "isInput": true, "isOutput": false }
                  ]
                }
              ]
            }
            """;

        var doc = DiagramSerializer.Deserialize(json);

        doc.Nodes.Should().HaveCount(1);
        var node = doc.Nodes[0];
        node.Id.Should().Be("node1");
        node.StencilId.Should().Be("uml.class");
        node.X.Should().Be(100);
        node.Y.Should().Be(200);
        node.W.Should().Be(180);
        node.H.Should().Be(140);
        node.Data.Should().ContainKey("name");
        node.Data["name"].Should().BeOfType<System.Text.Json.JsonElement>().Which.GetString().Should().Be("Customer");
        node.Ports.Should().HaveCount(1);
        node.Ports[0].Id.Should().Be("p1");
    }

    [Fact]
    public void Deserialize_WithEdges_MapsCorrectly()
    {
        var json = """
            {
              "version": "1.0",
              "title": "Test",
              "nodes": [],
              "edges": [
                { "id": "e1", "sourceNodeId": "n1", "targetNodeId": "n2", "label": "1..*", "connectorType": "association" }
              ]
            }
            """;

        var doc = DiagramSerializer.Deserialize(json);

        doc.Edges.Should().HaveCount(1);
        var edge = doc.Edges[0];
        edge.Id.Should().Be("e1");
        edge.SourceNodeId.Should().Be("n1");
        edge.TargetNodeId.Should().Be("n2");
        edge.Label.Should().Be("1..*");
        edge.ConnectorType.Should().Be("association");
    }

    [Fact]
    public void Deserialize_WithWaypoints_MapsCorrectly()
    {
        var json = """
            {
              "version": "1.0",
              "title": "Test",
              "nodes": [],
              "edges": [
                {
                  "id": "e1",
                  "sourceNodeId": "a",
                  "targetNodeId": "b",
                  "waypoints": [
                    { "x": 100, "y": 100 },
                    { "x": 200, "y": 200 }
                  ]
                }
              ]
            }
            """;

        var doc = DiagramSerializer.Deserialize(json);

        doc.Edges[0].Waypoints.Should().HaveCount(2);
        doc.Edges[0].Waypoints[0].X.Should().Be(100);
        doc.Edges[0].Waypoints[0].Y.Should().Be(100);
        doc.Edges[0].Waypoints[1].X.Should().Be(200);
        doc.Edges[0].Waypoints[1].Y.Should().Be(200);
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsDiagramDeserializationException()
    {
        var act = () => DiagramSerializer.Deserialize("{ this is not valid json }");

        act.Should().Throw<DiagramDeserializationException>()
            .WithMessage("Invalid diagram JSON.");
    }

    [Fact]
    public void Deserialize_NullString_ThrowsArgumentNullException()
    {
        var act = () => DiagramSerializer.Deserialize(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── TryDeserialize ────────────────────────────────────────────────────────

    [Fact]
    public void TryDeserialize_ValidJson_ReturnsTrueAndDocument()
    {
        var json = """{ "version": "1.0", "title": "Test", "nodes": [], "edges": [] }""";

        var result = DiagramSerializer.TryDeserialize(json, out var doc);

        result.Should().BeTrue();
        doc.Should().NotBeNull();
        doc!.Title.Should().Be("Test");
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsFalseAndNull()
    {
        var result = DiagramSerializer.TryDeserialize("not json", out var doc);

        result.Should().BeFalse();
        doc.Should().BeNull();
    }

    // ── Roundtrip ─────────────────────────────────────────────────────────────

    [Fact]
    public void Roundtrip_DocumentWithNodesAndEdges_PreservesAllData()
    {
        var original = new DiagramDocument
        {
            Title = "UML Class Diagram",
            Width = 3000,
            Height = 2000
        };
        var node = new DiagramNode
        {
            Id = "class1",
            StencilId = "uml.class",
            X = 200,
            Y = 150,
            W = 180,
            H = 140,
            Data = new() { ["name"] = "Customer" },
            Ports =
            [
                new() { Id = "right", Side = PortSide.Right, Offset = 0.5, IsInput = false, IsOutput = true }
            ]
        };
        original.Nodes.Add(node);
        original.Edges.Add(new DiagramEdge
        {
            Id = "edge1",
            SourceNodeId = "class1",
            TargetNodeId = "class2",
            Label = "1..*",
            Waypoints = [new() { X = 300, Y = 200 }, new() { X = 400, Y = 200 }]
        });

        var json = DiagramSerializer.Serialize(original);
        var restored = DiagramSerializer.Deserialize(json);

        restored.Title.Should().Be("UML Class Diagram");
        restored.Width.Should().Be(3000);
        restored.Height.Should().Be(2000);
        restored.Nodes.Should().HaveCount(1);
        restored.Nodes[0].StencilId.Should().Be("uml.class");
        restored.Nodes[0].Data["name"].Should().BeOfType<System.Text.Json.JsonElement>().Which.GetString().Should().Be("Customer");
        restored.Nodes[0].Ports.Should().HaveCount(1);
        restored.Nodes[0].Ports[0].Side.Should().Be(PortSide.Right);
        restored.Edges.Should().HaveCount(1);
        restored.Edges[0].Waypoints.Should().HaveCount(2);
    }
}
