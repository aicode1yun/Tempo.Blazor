using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Provides built-in stencil definitions for server-side diagram export.
/// This is a copy of the client-side definitions so the API can render diagrams
/// without referencing the Blazor UI assembly.</summary>
internal sealed class DemoDiagramStencilRegistry
{
    private readonly Dictionary<string, DiagramStencil> _stencils;

    public DemoDiagramStencilRegistry()
    {
        _stencils = new Dictionary<string, DiagramStencil>(StringComparer.Ordinal);
        foreach (var set in GetSets())
        {
            foreach (var stencil in set.Stencils)
            {
                _stencils[stencil.Id] = stencil;
            }
        }
    }

    public DiagramStencil? GetStencil(string stencilId)
        => _stencils.TryGetValue(stencilId, out var s) ? s : null;

    private static IEnumerable<DiagramStencilSet> GetSets()
    {
        yield return GeneralSet();
        yield return UmlSet();
        yield return BpmnSet();
        yield return FlowchartSet();
        yield return ErdSet();
    }

    private static DiagramStencilSet GeneralSet()
    {
        return new DiagramStencilSet
        {
            Id = "general",
            Name = "General",
            Stencils =
            [
                new()
                {
                    Id = "general.rectangle",
                    Name = "Rectangle",
                    Category = "General",
                    DefaultWidth = 120,
                    DefaultHeight = 60,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Rectangle",
                                TextStyle = new() { TextAlign = StencilTextAlign.Center }
                            }
                        ]
                    }
                },
                new()
                {
                    Id = "general.rounded",
                    Name = "Rounded Rectangle",
                    Category = "General",
                    DefaultWidth = 120,
                    DefaultHeight = 60,
                    Layout = new()
                    {
                        BackgroundShape = "rounded",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Rounded",
                                TextStyle = new() { TextAlign = StencilTextAlign.Center }
                            }
                        ]
                    }
                },
                new()
                {
                    Id = "general.ellipse",
                    Name = "Ellipse",
                    Category = "General",
                    DefaultWidth = 120,
                    DefaultHeight = 80,
                    Layout = new()
                    {
                        BackgroundShape = "ellipse",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Ellipse",
                                TextStyle = new() { TextAlign = StencilTextAlign.Center }
                            }
                        ]
                    }
                },
                new()
                {
                    Id = "general.text",
                    Name = "Text",
                    Category = "General",
                    DefaultWidth = 80,
                    DefaultHeight = 30,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Fill = "transparent",
                        Stroke = "transparent",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Text",
                                TextStyle = new() { TextAlign = StencilTextAlign.Center }
                            }
                        ]
                    }
                }
            ]
        };
    }

    private static DiagramStencilSet UmlSet()
    {
        return new DiagramStencilSet
        {
            Id = "uml",
            Name = "UML",
            Stencils =
            [
                new()
                {
                    Id = "uml.class",
                    Name = "Class",
                    Category = "UML",
                    DefaultWidth = 180,
                    DefaultHeight = 140,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "name",
                                DefaultText = "ClassName",
                                Padding = 8,
                                TextStyle = new()
                                {
                                    IsBold = true,
                                    TextAlign = StencilTextAlign.Center,
                                    FontSize = 13
                                }
                            },
                            new() { Type = "divider", Padding = 0 },
                            new()
                            {
                                Type = "list",
                                DataKey = "attributes",
                                DefaultText = "- attribute: Type",
                                Padding = 8,
                                TextStyle = new()
                                {
                                    TextAlign = StencilTextAlign.Left,
                                    FontSize = 11,
                                    FontFamily = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"
                                }
                            },
                            new() { Type = "divider", Padding = 0 },
                            new()
                            {
                                Type = "list",
                                DataKey = "methods",
                                DefaultText = "+ method(): Type",
                                Padding = 8,
                                TextStyle = new()
                                {
                                    TextAlign = StencilTextAlign.Left,
                                    FontSize = 11,
                                    FontFamily = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"
                                }
                            }
                        ]
                    },
                    DefaultData = new()
                    {
                        ["name"] = "ClassName",
                        ["attributes"] = new[] { "- id: Guid", "- name: string" },
                        ["methods"] = new[] { "+ Save(): void", "+ Load(): void" }
                    }
                },
                new()
                {
                    Id = "uml.package",
                    Name = "Package",
                    Category = "UML",
                    DefaultWidth = 200,
                    DefaultHeight = 160,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "name",
                                DefaultText = "Package",
                                Padding = 6,
                                TextStyle = new()
                                {
                                    IsBold = true,
                                    TextAlign = StencilTextAlign.Left,
                                    FontSize = 12
                                }
                            }
                        ]
                    },
                    DefaultData = new() { ["name"] = "Package" }
                }
            ]
        };
    }

    private static DiagramStencilSet BpmnSet()
    {
        return new DiagramStencilSet
        {
            Id = "bpmn",
            Name = "BPMN",
            Stencils =
            [
                new()
                {
                    Id = "bpmn.task",
                    Name = "Task",
                    Category = "BPMN",
                    DefaultWidth = 120,
                    DefaultHeight = 80,
                    Layout = new()
                    {
                        BackgroundShape = "rounded",
                        Sections =
                        [
                            new()
                            {
                                Type = "icon",
                                DataKey = "icon",
                                DefaultText = "⚙",
                                Padding = 8,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 16 }
                            },
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Task",
                                Padding = 4,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 12 }
                            }
                        ]
                    },
                    DefaultData = new() { ["icon"] = "⚙", ["label"] = "Task" }
                },
                new()
                {
                    Id = "bpmn.gateway",
                    Name = "Gateway",
                    Category = "BPMN",
                    DefaultWidth = 80,
                    DefaultHeight = 80,
                    Layout = new()
                    {
                        BackgroundShape = "diamond",
                        Sections =
                        [
                            new()
                            {
                                Type = "icon",
                                DataKey = "icon",
                                DefaultText = "✕",
                                Padding = 0,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 22 }
                            }
                        ]
                    },
                    DefaultData = new() { ["icon"] = "✕" }
                },
                new()
                {
                    Id = "bpmn.event",
                    Name = "Event",
                    Category = "BPMN",
                    DefaultWidth = 80,
                    DefaultHeight = 80,
                    Layout = new()
                    {
                        BackgroundShape = "ellipse",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Start",
                                Padding = 0,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 11 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Start" }
                },
                new()
                {
                    Id = "bpmn.pool",
                    Name = "Pool",
                    Category = "BPMN",
                    DefaultWidth = 300,
                    DefaultHeight = 180,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Pool",
                                Padding = 8,
                                TextStyle = new()
                                {
                                    IsBold = true,
                                    TextAlign = StencilTextAlign.Center,
                                    FontSize = 12
                                }
                            },
                            new() { Type = "divider", Padding = 0 },
                            new()
                            {
                                Type = "text",
                                DataKey = "lane",
                                DefaultText = "Lane 1",
                                Padding = 8,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 11 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Pool", ["lane"] = "Lane 1" }
                }
            ]
        };
    }

    private static DiagramStencilSet FlowchartSet()
    {
        return new DiagramStencilSet
        {
            Id = "flowchart",
            Name = "Flowchart",
            Stencils =
            [
                new()
                {
                    Id = "flowchart.terminator",
                    Name = "Start / End",
                    Category = "Flowchart",
                    DefaultWidth = 120,
                    DefaultHeight = 50,
                    Layout = new()
                    {
                        BackgroundShape = "ellipse",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Start",
                                Padding = 0,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 12 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Start" }
                },
                new()
                {
                    Id = "flowchart.decision",
                    Name = "Decision",
                    Category = "Flowchart",
                    DefaultWidth = 120,
                    DefaultHeight = 80,
                    Layout = new()
                    {
                        BackgroundShape = "diamond",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Decision?",
                                Padding = 0,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 12 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Decision?" }
                },
                new()
                {
                    Id = "flowchart.process",
                    Name = "Process",
                    Category = "Flowchart",
                    DefaultWidth = 120,
                    DefaultHeight = 60,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Process",
                                Padding = 0,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 12 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Process" }
                },
                new()
                {
                    Id = "flowchart.document",
                    Name = "Document",
                    Category = "Flowchart",
                    DefaultWidth = 120,
                    DefaultHeight = 70,
                    Layout = new()
                    {
                        BackgroundShape = "document",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Document",
                                Padding = 4,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 12 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Document" }
                }
            ]
        };
    }

    private static DiagramStencilSet ErdSet()
    {
        return new DiagramStencilSet
        {
            Id = "erd",
            Name = "ERD",
            Stencils =
            [
                new()
                {
                    Id = "erd.entity",
                    Name = "Entity",
                    Category = "ERD",
                    DefaultWidth = 140,
                    DefaultHeight = 100,
                    Layout = new()
                    {
                        BackgroundShape = "rectangle",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "name",
                                DefaultText = "Entity",
                                Padding = 6,
                                TextStyle = new()
                                {
                                    IsBold = true,
                                    TextAlign = StencilTextAlign.Center,
                                    FontSize = 12
                                }
                            },
                            new() { Type = "divider", Padding = 0 },
                            new()
                            {
                                Type = "list",
                                DataKey = "attributes",
                                DefaultText = "id: PK",
                                Padding = 6,
                                TextStyle = new()
                                {
                                    TextAlign = StencilTextAlign.Left,
                                    FontSize = 11,
                                    FontFamily = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"
                                }
                            }
                        ]
                    },
                    DefaultData = new()
                    {
                        ["name"] = "Entity",
                        ["attributes"] = new[] { "id: PK", "name: string" }
                    }
                },
                new()
                {
                    Id = "erd.relationship",
                    Name = "Relationship",
                    Category = "ERD",
                    DefaultWidth = 100,
                    DefaultHeight = 80,
                    Layout = new()
                    {
                        BackgroundShape = "diamond",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Has",
                                Padding = 0,
                                TextStyle = new() { TextAlign = StencilTextAlign.Center, FontSize = 11 }
                            }
                        ]
                    },
                    DefaultData = new() { ["label"] = "Has" }
                },
                new()
                {
                    Id = "erd.weak-entity",
                    Name = "Weak Entity",
                    Category = "ERD",
                    DefaultWidth = 140,
                    DefaultHeight = 100,
                    Layout = new()
                    {
                        BackgroundShape = "weak-entity",
                        StrokeWidth = 3,
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "name",
                                DefaultText = "Weak Entity",
                                Padding = 6,
                                TextStyle = new()
                                {
                                    IsBold = true,
                                    TextAlign = StencilTextAlign.Center,
                                    FontSize = 12
                                }
                            },
                            new() { Type = "divider", Padding = 0 },
                            new()
                            {
                                Type = "list",
                                DataKey = "attributes",
                                DefaultText = "id: PK",
                                Padding = 6,
                                TextStyle = new()
                                {
                                    TextAlign = StencilTextAlign.Left,
                                    FontSize = 11,
                                    FontFamily = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"
                                }
                            }
                        ]
                    },
                    DefaultData = new()
                    {
                        ["name"] = "Weak Entity",
                        ["attributes"] = new[] { "id: PK", "owner_id: FK" }
                    }
                }
            ]
        };
    }
}
