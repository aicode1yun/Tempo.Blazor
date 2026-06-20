using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Mcp.Tests.Fixtures;

public sealed class FakeDiagramStencilProvider : IDiagramStencilProvider
{
    public int Priority => 0;

    public IEnumerable<DiagramStencilSet> GetStencilSets()
    {
        yield return new DiagramStencilSet
        {
            Id = "test",
            Name = "Test stencils",
            Stencils =
            [
                new DiagramStencil
                {
                    Id = "test.process",
                    Name = "Process",
                    Category = "Test",
                    SetId = "test",
                    PaletteId = "test.flow",
                    Kind = DiagramStencilKind.Node,
                    DefaultWidth = 160,
                    DefaultHeight = 80,
                    Tags = ["flow"]
                },
                new DiagramStencil
                {
                    Id = "test.database",
                    Name = "Database",
                    Category = "Test",
                    SetId = "test",
                    PaletteId = "test.data",
                    Kind = DiagramStencilKind.Node,
                    DefaultWidth = 140,
                    DefaultHeight = 100,
                    Tags = ["data"]
                }
            ]
        };
    }
}
