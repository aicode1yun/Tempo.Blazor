using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramStencilOriginTests
{
    [Fact]
    public void DiagramStencil_Can_Be_Marked_As_TempoOriginal()
    {
        var stencil = new DiagramStencil
        {
            Id = "test.original",
            Name = "Original",
            Category = "Test",
            Origin = DiagramStencilOrigin.TempoOriginal
        };

        stencil.Origin.Should().Be(DiagramStencilOrigin.TempoOriginal);
    }

    [Fact]
    public void BuiltInProvider_Marks_All_Stencils_As_TempoOriginal_Without_External_Source()
    {
        var stencils = new BuiltInDiagramStencilProvider()
            .GetStencilSets()
            .SelectMany(set => set.Stencils)
            .ToList();

        stencils.Should().NotBeEmpty();
        stencils.Should().OnlyContain(stencil => stencil.Origin == DiagramStencilOrigin.TempoOriginal);
        stencils.Should().OnlyContain(stencil => string.IsNullOrWhiteSpace(stencil.ExternalAssetSourceId));
    }

    [Fact]
    public void RegisterStencil_Rejects_Stencil_Without_Explicit_Origin()
    {
        var registry = new DiagramStencilRegistry();
        var stencil = new DiagramStencil
        {
            Id = "test.unspecified",
            Name = "Unspecified",
            Category = "Test"
        };

        var act = () => registry.RegisterStencil(stencil);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetTempoOriginal_Returns_Only_TempoOriginal_Stencils()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterStencil(new DiagramStencil
        {
            Id = "test.original",
            Name = "Original",
            Category = "Test",
            Origin = DiagramStencilOrigin.TempoOriginal
        });

        var stencils = registry.GetTempoOriginal().ToList();

        stencils.Should().ContainSingle();
        stencils[0].Id.Should().Be("test.original");
        stencils.Should().OnlyContain(stencil => stencil.Origin == DiagramStencilOrigin.TempoOriginal);
    }
}
