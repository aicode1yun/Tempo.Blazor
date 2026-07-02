using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilFormatSpecInvariantsTests
{
    [Fact]
    public void WireframeDocumentSchema_ValidatesCurrentSerializerOutput()
    {
        var document = new WireframeDocument
        {
            Title = "Spec regression",
            TargetPackIds = ["tempo", "app:11111111-1111-1111-1111-111111111111"],
            TargetTheme = "dark"
        };
        document.Pages.Clear();
        var page = new WireframePage
        {
            Id = "home",
            Name = "Home",
            Width = 800,
            Height = 600,
            TargetPackIds = ["tempo"],
            TargetTheme = "light"
        };
        page.Elements.Add(new WireframeElement
        {
            Id = "cta",
            Type = "TmButton",
            X = 40,
            Y = 48,
            W = 120,
            H = 36
        });
        page.Connectors.Add(new WireframeConnector
        {
            Id = "loop",
            FromId = "cta",
            ToId = "cta",
            Label = "self"
        });
        document.Pages.Add(page);
        document.ActivePageId = page.Id;

        var schema = JsonSchema.FromText(File.ReadAllText(WireframeDocumentSchemaPath()));
        var json = WireframeSerializer.Serialize(document);
        var result = schema.Evaluate(JsonNode.Parse(json), new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        result.IsValid.Should().BeTrue("wireframe-document.schema.json must accept current WireframeSerializer output");
    }

    [Fact]
    public void WireframeDocumentSchema_TracksPagedModelSurface()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(WireframeDocumentSchemaPath()));
        var root = document.RootElement;
        var rootProps = PropertyNames(root.GetProperty("properties"));
        var required = root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        var defs = root.GetProperty("$defs");
        var pageProps = PropertyNames(defs.GetProperty("WireframePage").GetProperty("properties"));

        required.Should().Contain(["version", "title", "pages"]);
        rootProps.Should().Contain(["pages", "activePageId", "targetPacks", "targetTheme"]);
        rootProps.Should().NotContain(["elements", "connectors", "width", "height"]);
        PropertyNames(defs).Should().Contain(["WireframePage", "WireframeElement", "WireframeConnector", "WireframeLayer"]);
        pageProps.Should().Contain(["elements", "connectors", "layers", "targetPacks", "targetTheme"]);
    }

    [Fact]
    public void TempoStencilSchema_RenderNodeKindsMatchRuntimeEnum()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TempoStencilSchemaPath()));
        var schemaKinds = document.RootElement
            .GetProperty("$defs")
            .GetProperty("RenderNode")
            .GetProperty("properties")
            .GetProperty("kind")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var runtimeKinds = Enum.GetNames<RenderNodeKind>()
            .Select(name => JsonNamingPolicy.CamelCase.ConvertName(name))
            .ToArray();

        schemaKinds.Should().BeEquivalentTo(runtimeKinds);
    }

    [Fact]
    public void BuiltInTempoPack_UsesTempoNamespaceAndRuntimeDerivedSchemaCount()
    {
        var pack = StencilPackSerializer.Deserialize(BuiltInStencilPackProvider.ReadPackJson());
        var schemaTypes = new BuiltInComponentSchemas().GetSchemas().Select(schema => schema.Type).ToArray();
        var providerDefinitions = new BuiltInStencilPackProvider().GetDefinitions().ToArray();

        pack.Id.Should().Be("tempo");
        pack.Namespace.Should().Be("tempo");
        pack.IsBuiltIn.Should().BeTrue();
        pack.Components.Select(component => component.Type).Should().BeEquivalentTo(schemaTypes);
        providerDefinitions.Should().HaveCount(schemaTypes.Length);
        providerDefinitions.Should().OnlyContain(definition => definition.IsBuiltIn);
        providerDefinitions.Select(definition => definition.PackId).Should().OnlyContain(packId => packId == "tempo");
    }

    [Fact]
    public void AppStencilPackComponents_AreNamespacedByAppScopeWithoutCollisions()
    {
        var pack = new StencilPack
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = "customer-pack",
            Namespace = "app:customer",
            Components =
            [
                Component("Card"),
                Component("Badge")
            ]
        };

        var definitions = new StencilPackCompiler().Compile(pack).ToArray();

        definitions.Select(definition => definition.Type).Should().BeEquivalentTo("app:customer:Card", "app:customer:Badge");
        definitions.Should().OnlyContain(definition => definition.ScopeAppId == "customer");
        definitions.Should().OnlyContain(definition => definition.LocalType == "Card" || definition.LocalType == "Badge");
    }

    [Theory]
    [InlineData("{eval(\"1+1\")}")]
    [InlineData("{System.Environment.Exit(0)}")]
    [InlineData("token(userInput)")]
    public void SafeBindings_RejectFunctionCallsExceptLiteralTokenLookup(string source)
    {
        var success = StencilExpression.TryParse(source, out var expression, out var error);

        success.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        expression.IsMalformed.Should().BeTrue();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Literal);
        expression.Root.Value.Should().Be(source);
    }

    [Fact]
    public void SafeBindings_AllowTokenFunctionWithLiteralKey()
    {
        var success = StencilExpression.TryParse("token(\"color.primary\", \"#2563eb\")", out var expression, out _);

        success.Should().BeTrue();
        expression.IsMalformed.Should().BeFalse();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Token);
        expression.Root.Name.Should().Be("color.primary");
    }

    [Fact]
    public void TokenResolution_PrecedenceMatchesSpec()
    {
        const string key = "color.primary";

        new StencilTokenResolver(
                elementOverrides: new Dictionary<string, string> { [key] = "element" },
                documentTheme: new Dictionary<string, string> { [key] = "document" },
                packTheme: new Dictionary<string, string> { [key] = "pack-theme" },
                packDefaults: new Dictionary<string, string> { [key] = "pack-default" })
            .Resolve(key, "literal")
            .Should()
            .Be("element");
        new StencilTokenResolver(
                elementOverrides: null,
                documentTheme: new Dictionary<string, string> { [key] = "document" },
                packTheme: new Dictionary<string, string> { [key] = "pack-theme" },
                packDefaults: new Dictionary<string, string> { [key] = "pack-default" })
            .Resolve(key, "literal")
            .Should()
            .Be("document");
        new StencilTokenResolver(
                elementOverrides: null,
                documentTheme: null,
                packTheme: new Dictionary<string, string> { [key] = "pack-theme" },
                packDefaults: new Dictionary<string, string> { [key] = "pack-default" })
            .Resolve(key, "literal")
            .Should()
            .Be("pack-theme");
        new StencilTokenResolver(
                elementOverrides: null,
                documentTheme: null,
                packTheme: null,
                packDefaults: new Dictionary<string, string> { [key] = "pack-default" })
            .Resolve(key, "literal")
            .Should()
            .Be("pack-default");
        new StencilTokenResolver(
                elementOverrides: null,
                documentTheme: null,
                packTheme: null,
                packDefaults: null)
            .Resolve(key, "literal")
            .Should()
            .Be("literal");
    }

    [Fact]
    public void SpecDocuments_CoverRuntimeStencilAndWireframeContracts()
    {
        var spec = File.ReadAllText(RepoPath("docs", "SPEC-stencil-format.md"));
        var guide = File.ReadAllText(RepoPath("docs", "stencil-pack-authoring-guide.md"));

        spec.Should().Contain("tempo-stencil");
        spec.Should().Contain("wireframe-document.schema.json");
        spec.Should().Contain("app:{id}:{localType}");
        spec.Should().Contain("token()");
        spec.Should().Contain("native{}");
        guide.Should().Contain("PromptHelper");
        guide.Should().Contain("render");
        guide.Should().Contain("targetPacks");

        foreach (var kind in Enum.GetNames<RenderNodeKind>().Select(name => JsonNamingPolicy.CamelCase.ConvertName(name)))
        {
            spec.Should().Contain(kind);
            guide.Should().Contain(kind);
        }
    }

    private static StencilComponent Component(string type)
        => new()
        {
            Type = type,
            DisplayName = type,
            Category = "Spec",
            DefaultSize = new StencilSize(120, 72),
            Render = new RenderNode
            {
                Kind = RenderNodeKind.Rect,
                Attributes = new Dictionary<string, object?>
                {
                    ["w"] = "size.w",
                    ["h"] = "size.h"
                }
            }
        };

    private static string TempoStencilSchemaPath()
        => RepoPath("src", "Tempo.Blazor.Wireframe", "wwwroot", "tempo-stencil.schema.json");

    private static string WireframeDocumentSchemaPath()
        => RepoPath("src", "Tempo.Blazor.Wireframe", "wwwroot", "wireframe-document.schema.json");

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the repository root should be discoverable from the test output directory");
        return Path.Combine([directory!.FullName, .. parts]);
    }

    private static string[] PropertyNames(JsonElement obj)
        => obj.EnumerateObject().Select(property => property.Name).ToArray();
}
