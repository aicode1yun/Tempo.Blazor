using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Configuration;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class JsonDiagramStencilProviderTests
{
    private const string RequiredLibraryJson = """
        {
          "setId": "uml25",
          "nameResourceKey": "DiagramStencilLibrary_Uml25",
          "palettes": [
            {
              "paletteId": "uml25.class",
              "nameResourceKey": "DiagramStencilPalette_Uml25_Class",
              "order": 10,
              "stencils": [
                {
                  "id": "uml25.class.class",
                  "nameResourceKey": "DiagramStencil_Uml25_Class",
                  "category": "uml25",
                  "origin": "tempoOriginal",
                  "defaultWidth": 160,
                  "defaultHeight": 96,
                  "tags": ["uml", "classifier"],
                  "keywords": ["class", "object"],
                  "layout": {
                    "shape": "rectangle"
                  }
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void GetStencilSets_Loads_Required_Json_Library()
    {
        var provider = new JsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("required", () => RequiredLibraryJson)]);

        var set = provider.GetStencilSets().Should().ContainSingle().Subject;
        var stencil = set.Stencils.Should().ContainSingle().Subject;

        set.Id.Should().Be("uml25");
        set.NameResourceKey.Should().Be("DiagramStencilLibrary_Uml25");
        stencil.Id.Should().Be("uml25.class.class");
        stencil.NameResourceKey.Should().Be("DiagramStencil_Uml25_Class");
        stencil.Name.Should().Be("DiagramStencil_Uml25_Class");
        stencil.SetId.Should().Be("uml25");
        stencil.PaletteId.Should().Be("uml25.class");
        stencil.Order.Should().Be(10);
        stencil.Origin.Should().Be(DiagramStencilOrigin.TempoOriginal);
        stencil.Tags.Should().BeEquivalentTo(["uml", "classifier"]);
        stencil.Keywords.Should().BeEquivalentTo(["class", "object"]);
    }

    [Fact]
    public void AddJsonDiagramStencilProvider_Registers_IDiagramStencilProvider()
    {
        var services = new ServiceCollection();

        services.AddJsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("required", () => RequiredLibraryJson)]);

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IDiagramStencilProvider>()
            .Should()
            .ContainSingle(stencilProvider => stencilProvider is JsonDiagramStencilProvider);
    }

    [Fact]
    public void Validator_Returns_ErrorCodes_Without_UserFacing_Messages()
    {
        var library = new DiagramStencilLibrary
        {
            NameResourceKey = "DiagramStencilLibrary_Uml25",
            Palettes =
            [
                new DiagramStencilPalette
                {
                    PaletteId = "uml25.class",
                    NameResourceKey = "DiagramStencilPalette_Uml25_Class"
                }
            ]
        };

        var result = DiagramStencilLibraryValidator.Validate(library);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Code == DiagramStencilLibraryValidationErrorCodes.MissingSetId
            && error.Path == "setId");
        result.Errors.Should().AllSatisfy(error => error.Message.Should().BeNull());
    }

    [Fact]
    public void GetStencilSets_Rejects_Library_Without_SetId()
    {
        const string json = """
            {
              "nameResourceKey": "DiagramStencilLibrary_Uml25",
              "palettes": []
            }
            """;
        var provider = new JsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("invalid", () => json)]);

        var act = () => provider.GetStencilSets().ToList();

        act.Should()
            .Throw<DiagramStencilLibraryValidationException>()
            .Which.Errors.Should()
            .ContainSingle(error => error.Code == DiagramStencilLibraryValidationErrorCodes.MissingSetId);
    }

    [Fact]
    public void GetStencilSets_Rejects_Palette_Without_NameResourceKey()
    {
        const string json = """
            {
              "setId": "uml25",
              "nameResourceKey": "DiagramStencilLibrary_Uml25",
              "palettes": [
                {
                  "paletteId": "uml25.class",
                  "stencils": []
                }
              ]
            }
            """;
        var provider = new JsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("invalid", () => json)]);

        var act = () => provider.GetStencilSets().ToList();

        act.Should()
            .Throw<DiagramStencilLibraryValidationException>()
            .Which.Errors.Should()
            .ContainSingle(error => error.Code == DiagramStencilLibraryValidationErrorCodes.MissingPaletteNameResourceKey);
    }

    [Fact]
    public void Optional_Library_Is_Not_Loaded_Until_Requested()
    {
        var optionalLoadCount = 0;
        var provider = new JsonDiagramStencilProvider(
        [
            JsonDiagramStencilLibrarySource.Required("required", () => RequiredLibraryJson),
            JsonDiagramStencilLibrarySource.Optional("optional", () =>
            {
                optionalLoadCount++;
                return """
                    {
                      "setId": "bpmn2",
                      "nameResourceKey": "DiagramStencilLibrary_Bpmn2",
                      "palettes": [
                        {
                          "paletteId": "bpmn2.tasks",
                          "nameResourceKey": "DiagramStencilPalette_Bpmn2_Tasks",
                          "stencils": [
                            {
                              "id": "bpmn2.task.user",
                              "nameResourceKey": "DiagramStencil_Bpmn2_UserTask",
                              "category": "bpmn2",
                              "origin": "tempoOriginal"
                            }
                          ]
                        }
                      ]
                    }
                    """;
            })
        ]);

        provider.GetStencilSets().Select(set => set.Id).Should().Equal("uml25");
        optionalLoadCount.Should().Be(0);

        provider.LoadOptionalLibrary("optional");

        provider.GetStencilSets().Select(set => set.Id).Should().Equal("bpmn2", "uml25");
        optionalLoadCount.Should().Be(1);
    }
}
