using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class SqlToErDiagramGeneratorTests
{
    [Fact]
    public void Generate_CreatesCorrectNodesAndEdges()
    {
        var tables = new List<SqlTableDefinition>
        {
            new()
            {
                Name = "Users",
                Columns =
                [
                    new() { Name = "Id", DataType = "INT", IsPrimaryKey = true },
                    new() { Name = "Name", DataType = "VARCHAR(255)" }
                ],
                PrimaryKeys = ["Id"]
            },
            new()
            {
                Name = "Posts",
                Columns =
                [
                    new() { Name = "Id", DataType = "INT", IsPrimaryKey = true },
                    new() { Name = "UserId", DataType = "INT", IsForeignKey = true },
                    new() { Name = "Title", DataType = "VARCHAR(255)" }
                ],
                PrimaryKeys = ["Id"],
                ForeignKeys =
                [
                    new() { ColumnName = "UserId", ReferenceTable = "Users", ReferenceColumn = "Id" }
                ]
            }
        };

        var doc = SqlToErDiagramGenerator.Generate(tables);

        doc.Title.Should().Be("ER Diagram");
        doc.Pages.Count.Should().Be(1);

        var page = doc.ActivePage;
        page.Nodes.Count.Should().Be(2);
        page.Nodes.Should().Contain(n => n.StencilId == "erd.entity" && n.Data["name"].ToString() == "Users");
        page.Nodes.Should().Contain(n => n.StencilId == "erd.entity" && n.Data["name"].ToString() == "Posts");

        page.Edges.Count.Should().Be(1);
        var edge = page.Edges[0];
        edge.Routing.Should().Be("straight");
        edge.EndArrow.Should().Be("crow");
        edge.Label.Should().Be("UserId");

        var usersNode = page.Nodes.First(n => n.Data["name"].ToString() == "Users");
        var postsNode = page.Nodes.First(n => n.Data["name"].ToString() == "Posts");
        edge.SourceNodeId.Should().Be(usersNode.Id);
        edge.TargetNodeId.Should().Be(postsNode.Id);
    }

    [Fact]
    public void Generate_HidesJunctionTable_AndCreatesManyToManyEdge()
    {
        var tables = new List<SqlTableDefinition>
        {
            new()
            {
                Name = "Students",
                Columns = [new() { Name = "Id", DataType = "INT", IsPrimaryKey = true }],
                PrimaryKeys = ["Id"]
            },
            new()
            {
                Name = "Courses",
                Columns = [new() { Name = "Id", DataType = "INT", IsPrimaryKey = true }],
                PrimaryKeys = ["Id"]
            },
            new()
            {
                Name = "StudentCourse",
                Columns =
                [
                    new() { Name = "StudentId", DataType = "INT", IsPrimaryKey = true },
                    new() { Name = "CourseId", DataType = "INT", IsPrimaryKey = true }
                ],
                PrimaryKeys = ["StudentId", "CourseId"],
                ForeignKeys =
                [
                    new() { ColumnName = "StudentId", ReferenceTable = "Students", ReferenceColumn = "Id" },
                    new() { ColumnName = "CourseId", ReferenceTable = "Courses", ReferenceColumn = "Id" }
                ],
                IsJunctionTable = true
            }
        };

        var doc = SqlToErDiagramGenerator.Generate(tables);
        var page = doc.ActivePage;

        page.Nodes.Count.Should().Be(2);
        page.Nodes.Should().NotContain(n => n.Data["name"].ToString() == "StudentCourse");

        page.Edges.Count.Should().Be(1);
        var edge = page.Edges[0];
        edge.StartArrow.Should().Be("crow");
        edge.EndArrow.Should().Be("crow");
        edge.Label.Should().Be("StudentCourse");
    }

    [Fact]
    public void Generate_AvoidsDuplicateEdges_WhenMultipleForeignKeysBetweenSameTables()
    {
        var tables = new List<SqlTableDefinition>
        {
            new()
            {
                Name = "A",
                Columns = [new() { Name = "Id", DataType = "INT", IsPrimaryKey = true }],
                PrimaryKeys = ["Id"]
            },
            new()
            {
                Name = "B",
                Columns =
                [
                    new() { Name = "Id", DataType = "INT", IsPrimaryKey = true },
                    new() { Name = "A1", DataType = "INT", IsForeignKey = true },
                    new() { Name = "A2", DataType = "INT", IsForeignKey = true }
                ],
                PrimaryKeys = ["Id"],
                ForeignKeys =
                [
                    new() { ColumnName = "A1", ReferenceTable = "A", ReferenceColumn = "Id" },
                    new() { ColumnName = "A2", ReferenceTable = "A", ReferenceColumn = "Id" }
                ]
            }
        };

        var doc = SqlToErDiagramGenerator.Generate(tables);
        var page = doc.ActivePage;

        page.Nodes.Count.Should().Be(2);
        page.Edges.Count.Should().Be(1);
    }
}
