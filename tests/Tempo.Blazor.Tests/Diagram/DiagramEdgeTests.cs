using FluentAssertions;
using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramEdgeTests
{
    [Fact]
    public void IsValid_WithDanglingSourceAndConnectedTarget_IsValid()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = "n1",
        };
        edge.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithConnectedSourceAndDanglingTarget_IsValid()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(10, 20),
        };
        edge.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithBothEndsDangling_IsValid()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(30, 40),
        };
        edge.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithBothEndsDisconnected_IsNotValid()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            TargetNodeId = null,
        };
        edge.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithBothEndsConnected_IsValid()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            TargetNodeId = "n2",
        };
        edge.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithEdgeToEdgeConnection_IsValid()
    {
        var edge = new DiagramEdge
        {
            SourceEdgeId = "e1",
            TargetEdgeId = "e2",
        };
        edge.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesConstraint()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            TargetNodeId = "n2",
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0.5, RelativeY = 1.0, Perimeter = true, Dx = 5, Dy = -3 },
            TargetConstraint = new DiagramConnectionConstraint { RelativeX = 0.0, RelativeY = 0.5, Perimeter = false },
        };

        var json = JsonSerializer.Serialize(edge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var deserialized = JsonSerializer.Deserialize<DiagramEdge>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        deserialized.Should().NotBeNull();
        deserialized!.SourceConstraint.Should().NotBeNull();
        deserialized.SourceConstraint!.RelativeX.Should().Be(0.5);
        deserialized.SourceConstraint!.RelativeY.Should().Be(1.0);
        deserialized.SourceConstraint!.Perimeter.Should().BeTrue();
        deserialized.SourceConstraint!.Dx.Should().Be(5);
        deserialized.SourceConstraint!.Dy.Should().Be(-3);

        deserialized.TargetConstraint.Should().NotBeNull();
        deserialized.TargetConstraint!.RelativeX.Should().Be(0.0);
        deserialized.TargetConstraint!.Perimeter.Should().BeFalse();
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesDanglingPoints()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(30, 40),
        };

        var json = JsonSerializer.Serialize(edge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var deserialized = JsonSerializer.Deserialize<DiagramEdge>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        deserialized.Should().NotBeNull();
        deserialized!.SourceNodeId.Should().BeNull();
        deserialized.SourcePoint.Should().NotBeNull();
        deserialized.SourcePoint!.X.Should().Be(10);
        deserialized.SourcePoint!.Y.Should().Be(20);
        deserialized.TargetNodeId.Should().BeNull();
        deserialized.TargetPoint!.X.Should().Be(30);
        deserialized.TargetPoint!.Y.Should().Be(40);
    }

    [Fact]
    public void Serialization_NullableFields_AreOmitted()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            TargetNodeId = "n2",
        };

        var json = JsonSerializer.Serialize(edge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        json.Should().NotContain("sourcePoint");
        json.Should().NotContain("targetPoint");
        json.Should().NotContain("sourceConstraint");
        json.Should().NotContain("targetConstraint");
    }
}
