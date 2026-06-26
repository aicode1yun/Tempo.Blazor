using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class JsonWireframeComponentProviderTests
{
    private const string MinimalJson = """
        [
          {
            "type": "MyCard",
            "displayName": "My Card",
            "category": "Custom",
            "defaultWidth": 200,
            "defaultHeight": 120,
            "svgTemplate": "<rect width='{{w}}' height='{{h}}'/>",
            "props": []
          }
        ]
        """;

    private const string FullPropsJson = """
        [
          {
            "type": "Widget",
            "svgTemplate": "<text>{{props.title}}</text>",
            "props": [
              { "name": "title",   "displayName": "Title",   "type": "String",  "default": "Hello",  "category": "Content",    "isRequired": true },
              { "name": "count",   "displayName": "Count",   "type": "Int",     "default": 5                                                       },
              { "name": "visible", "displayName": "Visible", "type": "Bool",    "default": true                                                    },
              { "name": "color",   "displayName": "Color",   "type": "Color",   "default": "#ff0000"                                               },
              { "name": "mode",    "displayName": "Mode",    "type": "Enum",    "default": "A",      "options": ["A","B","C"]                      }
            ]
          }
        ]
        """;

    // ── LoadFromJson ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadFromJson_ParsesMinimalDefinition()
    {
        var provider = new JsonWireframeComponentProvider();
        provider.LoadFromJson(MinimalJson);

        var defs = provider.GetDefinitions().ToList();
        defs.Should().HaveCount(1);

        var d = defs[0];
        d.Type.Should().Be("MyCard");
        d.DisplayName.Should().Be("My Card");
        d.Category.Should().Be("Custom");
        d.DefaultWidth.Should().Be(200);
        d.DefaultHeight.Should().Be(120);
        d.IsBuiltIn.Should().BeFalse();
    }

    [Fact]
    public void LoadFromJson_ParsesAllPropTypes()
    {
        var provider = new JsonWireframeComponentProvider();
        provider.LoadFromJson(FullPropsJson);

        var props = provider.GetDefinitions().First().Props;
        props.Should().HaveCount(5);

        props[0].Type.Should().Be(PropType.String);
        props[0].IsRequired.Should().BeTrue();
        props[0].Category.Should().Be("Content");
        props[0].Default.Should().Be("Hello");

        props[1].Type.Should().Be(PropType.Int);
        props[1].Default.Should().Be(5);

        props[2].Type.Should().Be(PropType.Bool);
        props[2].Default.Should().Be(true);

        props[3].Type.Should().Be(PropType.Color);

        props[4].Type.Should().Be(PropType.Enum);
        props[4].Options.Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public void LoadFromJson_DefaultsAppliedWhenFieldsMissing()
    {
        var json = """[{ "type": "Bare" }]""";
        var provider = new JsonWireframeComponentProvider();
        provider.LoadFromJson(json);

        var d = provider.GetDefinitions().First();
        d.DisplayName.Should().Be("Bare");
        d.Category.Should().Be("Custom");
        d.DefaultWidth.Should().Be(160);
        d.DefaultHeight.Should().Be(40);
        d.Props.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromJson_ParsesScopeMetadata()
    {
        var appId = Guid.NewGuid().ToString("D");
        var json = $$"""
            [
              {
                "type": "InvoiceCard",
                "localType": "InvoiceCard",
                "scopeAppId": "{{appId}}"
              }
            ]
            """;
        var provider = new JsonWireframeComponentProvider();
        provider.LoadFromJson(json);

        var d = provider.GetDefinitions().Single();

        d.Type.Should().Be("InvoiceCard");
        d.LocalType.Should().Be("InvoiceCard");
        d.ScopeAppId.Should().Be(appId);
    }

    [Fact]
    public void LoadFromJson_CanLoadMultipleDefinitions()
    {
        var json = """
            [
              { "type": "Alpha" },
              { "type": "Beta"  },
              { "type": "Gamma" }
            ]
            """;
        var provider = new JsonWireframeComponentProvider();
        provider.LoadFromJson(json);

        provider.GetDefinitions().Should().HaveCount(3);
    }

    [Fact]
    public void LoadFromJson_AccumulatesAcrossMultipleCalls()
    {
        var provider = new JsonWireframeComponentProvider();
        provider.LoadFromJson("""[{ "type": "Alpha" }]""");
        provider.LoadFromJson("""[{ "type": "Beta"  }]""");

        provider.GetDefinitions().Select(d => d.Type)
            .Should().BeEquivalentTo(["Alpha", "Beta"]);
    }

    [Fact]
    public void LoadFromJson_ThrowsOnInvalidJson()
    {
        var provider = new JsonWireframeComponentProvider();
        var act = () => provider.LoadFromJson("not json");
        act.Should().Throw<WireframeDeserializationException>();
    }

    [Fact]
    public void LoadFromJson_ThrowsWhenRootIsNotArray()
    {
        var provider = new JsonWireframeComponentProvider();
        var act = () => provider.LoadFromJson("""{"type":"X"}""");
        act.Should().Throw<WireframeDeserializationException>();
    }

    // ── Template resolver ─────────────────────────────────────────────────────

    [Fact]
    public void ResolvePlaceholders_SubstitutesWidthAndHeight()
    {
        var element = new WireframeElement { Id = "e1", Type = "T", X = 0, Y = 0, W = 320, H = 80 };
        var result = JsonWireframeComponentProvider.ResolvePlaceholders("w={{w}} h={{h}}", element);
        result.Should().Be("w=320 h=80");
    }

    [Fact]
    public void ResolvePlaceholders_SubstitutesPropValue()
    {
        var element = new WireframeElement { Id = "e1", Type = "T", W = 100, H = 40 };
        element.Props["title"] = JsonSerializer.SerializeToElement("Hello World");

        var result = JsonWireframeComponentProvider.ResolvePlaceholders("<text>{{props.title}}</text>", element);
        result.Should().Be("<text>Hello World</text>");
    }

    [Fact]
    public void ResolvePlaceholders_LeavesUnknownPlaceholdersIntact()
    {
        var element = new WireframeElement { Id = "e1", Type = "T", W = 10, H = 10 };
        var result = JsonWireframeComponentProvider.ResolvePlaceholders("{{unknown}}", element);
        result.Should().Be("{{unknown}}");
    }

    [Fact]
    public void ResolvePlaceholders_SubstitutesIdAndType()
    {
        var element = new WireframeElement { Id = "abc123", Type = "MyWidget", W = 10, H = 10 };
        var result = JsonWireframeComponentProvider.ResolvePlaceholders("id={{id}} type={{type}}", element);
        result.Should().Be("id=abc123 type=MyWidget");
    }

    // ── ProviderId / Priority ─────────────────────────────────────────────────

    [Fact]
    public void DefaultProviderIdAndPriority()
    {
        var provider = new JsonWireframeComponentProvider();
        provider.ProviderId.Should().Be("JsonProvider");
        provider.Priority.Should().Be(50);
    }

    [Fact]
    public void CustomProviderIdAndPriority()
    {
        var provider = new JsonWireframeComponentProvider("MyCompany", 100);
        provider.ProviderId.Should().Be("MyCompany");
        provider.Priority.Should().Be(100);
    }
}
