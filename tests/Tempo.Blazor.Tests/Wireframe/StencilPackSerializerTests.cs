using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilPackSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var pack = CreateGoodPack();

        var json = StencilPackSerializer.Serialize(pack);
        var restored = StencilPackSerializer.Deserialize(json);

        restored.Should().BeEquivalentTo(pack, options => options.ComparingByMembers<JsonElement>());
        StencilPackSerializer.Serialize(restored).Should().Be(json);
        json.Should().Contain("\"kind\": \"stack\"");
        json.Should().Contain("\"resize\": \"nineSlice\"");
        json.Should().Contain("\"x\": \"{x}\"");
        json.Should().NotContain("\"attrs\"");
    }

    [Fact]
    public void RenderNode_RoundTrip_PreservesEmptyStringsButOmitsNullStrings()
    {
        var node = new RenderNode
        {
            Kind = RenderNodeKind.Text,
            Text = string.Empty,
            Value = string.Empty
        };

        var json = JsonSerializer.Serialize(node, StencilJsonOptions.Default);
        var restored = JsonSerializer.Deserialize<RenderNode>(json, StencilJsonOptions.Default);

        json.Should().Contain("\"text\": \"\"");
        json.Should().Contain("\"value\": \"\"");
        json.Should().NotContain("\"when\"");
        restored.Should().NotBeNull();
        restored!.Text.Should().BeEmpty();
        restored.Value.Should().BeEmpty();
        restored.When.Should().BeNull();
    }

    internal static StencilPack CreateGoodPack()
    {
        return new StencilPack
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = "tempo",
            Namespace = "tempo",
            Target = new StencilTarget
            {
                Framework = "blazor",
                Library = "Tempo.Blazor",
                Version = "2.0.5"
            },
            Tokens = new Dictionary<string, string>
            {
                ["palette.primary"] = "#2563eb",
                ["spacing.md"] = "12px"
            },
            Themes = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["dark"] = new Dictionary<string, string>
                {
                    ["palette.primary"] = "#93c5fd",
                    ["surface.default"] = "#111827"
                }
            },
            Icons = new Dictionary<string, string>
            {
                ["check"] = "M4 12l4 4L20 4"
            },
            Parts = new Dictionary<string, RenderNode>
            {
                ["focusRing"] = new RenderNode
                {
                    Kind = RenderNodeKind.Rect,
                    Attributes = new Dictionary<string, object?>
                    {
                        ["x"] = "{x}",
                        ["y"] = "{y}",
                        ["w"] = "{w}",
                        ["h"] = "{h}",
                        ["stroke"] = "token(\"palette.primary\")"
                    }
                }
            },
            Components =
            [
                new StencilComponent
                {
                    Type = "tempo:TmButton",
                    DisplayName = "Button",
                    Category = "Inputs",
                    Icon = "check",
                    DefaultSize = new StencilSize(128, 40),
                    MinSize = new StencilSize(88, 32),
                    MaxSize = new StencilSize(320, 64),
                    SizePresets = new Dictionary<string, StencilSize>
                    {
                        ["sm"] = new(96, 32),
                        ["lg"] = new(160, 48)
                    },
                    Resize = StencilResize.NineSlice,
                    Slice = new StencilSlice { Left = 8, Top = 8, Right = 8, Bottom = 8 },
                    Props =
                    [
                        new PropDef
                        {
                            Name = "label",
                            DisplayName = "Label",
                            Type = PropType.String,
                            Default = "OK",
                            Category = "Content",
                            IsRequired = true
                        },
                        new PropDef
                        {
                            Name = "items",
                            DisplayName = "Items",
                            Type = PropType.StringList,
                            Default = new[] { "One", "Two" },
                            Category = "Content"
                        }
                    ],
                    ContentSlots = ["prefix", "suffix"],
                    Impl = new StencilImpl
                    {
                        Component = "TmButton",
                        Parameters = new Dictionary<string, object?>
                        {
                            ["ChildContent"] = "{label}",
                            ["Disabled"] = "{disabled}"
                        }
                    },
                    Render = new RenderNode
                    {
                        Kind = RenderNodeKind.Stack,
                        Attributes = new Dictionary<string, object?>
                        {
                            ["direction"] = "row",
                            ["gap"] = "token(\"spacing.md\")",
                            ["padding"] = "8 12",
                            ["align"] = "center"
                        },
                        Children =
                        [
                            new RenderNode
                            {
                                Kind = RenderNodeKind.Rect,
                                Attributes = new Dictionary<string, object?>
                                {
                                    ["x"] = "0",
                                    ["y"] = "0",
                                    ["w"] = "size.w",
                                    ["h"] = "size.h",
                                    ["fill"] = "token(\"palette.primary\")"
                                }
                            },
                            new RenderNode
                            {
                                Kind = RenderNodeKind.Text,
                                Text = "{label ?? \"OK\"}",
                                Attributes = new Dictionary<string, object?>
                                {
                                    ["fill"] = "#fff",
                                    ["fontWeight"] = "600"
                                }
                            },
                            new RenderNode
                            {
                                Kind = RenderNodeKind.Component,
                                Attributes = new Dictionary<string, object?>
                                {
                                    ["ref"] = "tempo:TmIcon"
                                },
                                Props = new Dictionary<string, object?>
                                {
                                    ["name"] = "{icon}",
                                    ["size"] = 16
                                }
                            },
                            new RenderNode
                            {
                                Kind = RenderNodeKind.Repeat,
                                Prop = "items",
                                As = "item",
                                Node = new RenderNode
                                {
                                    Kind = RenderNodeKind.Text,
                                    Text = "{item}"
                                }
                            },
                            new RenderNode
                            {
                                Kind = RenderNodeKind.Spinner,
                                When = "{loading}",
                                Attributes = new Dictionary<string, object?>
                                {
                                    ["size"] = 16
                                }
                            }
                        ]
                    }
                },
                new StencilComponent
                {
                    Type = "tempo:NativeDatePicker",
                    DisplayName = "Native date picker",
                    Category = "Inputs",
                    DefaultSize = new StencilSize(180, 36),
                    Resize = StencilResize.Scale,
                    Native = new StencilNative
                    {
                        NativeType = "Tempo.Blazor.Wireframe.NativeDatePicker",
                        Parameters = new Dictionary<string, object?>
                        {
                            ["Kind"] = "date"
                        }
                    }
                }
            ]
        };
    }
}
