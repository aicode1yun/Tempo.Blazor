using System.Text.Json.Nodes;
using Json.Schema;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilPackSchemaTests
{
    [Fact]
    public void Validates_GoodPack_Passes()
    {
        Validate(GoodPackJson()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_UnknownNodeKind()
    {
        var json = MutateGoodPack(root =>
        {
            var firstComponent = root["components"]!.AsArray()[0]!.AsObject();
            var firstChild = firstComponent["render"]!["children"]!.AsArray()[0]!.AsObject();
            firstChild["kind"] = "blob";
        });

        Validate(json).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_MissingComponentType()
    {
        var json = MutateGoodPack(root =>
        {
            var firstComponent = root["components"]!.AsArray()[0]!.AsObject();
            firstComponent.Remove("type");
        });

        Validate(json).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_BadResizeEnum()
    {
        var json = MutateGoodPack(root =>
        {
            var firstComponent = root["components"]!.AsArray()[0]!.AsObject();
            firstComponent["resize"] = "stretch";
        });

        Validate(json).IsValid.Should().BeFalse();
    }

    private static EvaluationResults Validate(string json)
    {
        var schemaText = File.ReadAllText(SchemaPath());
        var schema = JsonSchema.FromText(schemaText);
        var document = JsonNode.Parse(json);

        return schema.Evaluate(document, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });
    }

    private static string GoodPackJson()
        => StencilPackSerializer.Serialize(StencilPackSerializerTests.CreateGoodPack());

    private static string MutateGoodPack(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(GoodPackJson())!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string SchemaPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the repository root should be discoverable from the test output directory");
        return Path.Combine(directory!.FullName, "src", "Tempo.Blazor.Wireframe", "wwwroot", "tempo-stencil.schema.json");
    }
}
