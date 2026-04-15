using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>
/// Registers built-in diagram stencil definitions for Tempo.Blazor.
/// Priority 0 – custom providers with higher priority can override individual stencils.
/// </summary>
public sealed class BuiltInDiagramStencilProvider : IDiagramStencilProvider
{
    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public IEnumerable<DiagramStencilSet> GetStencilSets()
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
                    IconSvg = "<rect x='4' y='8' width='24' height='16' rx='2' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 60,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    IconSvg = "<rect x='4' y='8' width='24' height='16' rx='6' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 60,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    IconSvg = "<ellipse cx='16' cy='16' rx='12' ry='8' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 80,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    IconSvg = "<text x='16' y='20' text-anchor='middle' font-size='12' fill='currentColor'>T</text>",
                    DefaultWidth = 80,
                    DefaultHeight = 30,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 }
                    ],
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
                    IconSvg = "<rect x='4' y='4' width='24' height='24' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><line x1='4' y1='12' x2='28' y2='12' stroke='currentColor' stroke-width='2'/><line x1='4' y1='20' x2='28' y2='20' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 180,
                    DefaultHeight = 140,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.25 },
                        new() { Name = "top-mid", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "top-right", Side = PortSide.Top, Offset = 0.75 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.25 },
                        new() { Name = "bottom-mid", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "bottom-right", Side = PortSide.Bottom, Offset = 0.75 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                            new()
                            {
                                Type = "divider",
                                Padding = 0
                            },
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
                            new()
                            {
                                Type = "divider",
                                Padding = 0
                            },
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
                    IconSvg = "<rect x='4' y='8' width='24' height='20' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><rect x='4' y='4' width='10' height='6' rx='1' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 200,
                    DefaultHeight = 160,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
                    Layout = new()
                    {
                        BackgroundShape = "package",
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
                    DefaultData = new()
                    {
                        ["name"] = "Package"
                    }
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
                    IconSvg = "<rect x='4' y='8' width='24' height='16' rx='4' fill='none' stroke='currentColor' stroke-width='2'/><rect x='9' y='12' width='5' height='5' rx='1' fill='currentColor'/><line x1='18' y1='14' x2='25' y2='14' stroke='currentColor' stroke-width='2'/><line x1='18' y1='18' x2='25' y2='18' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 80,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["icon"] = "⚙",
                        ["label"] = "Task"
                    }
                },
                new()
                {
                    Id = "bpmn.gateway",
                    Name = "Gateway",
                    Category = "BPMN",
                    IconSvg = "<polygon points='16,4 28,16 16,28 4,16' fill='none' stroke='currentColor' stroke-width='2'/><line x1='16' y1='10' x2='16' y2='22' stroke='currentColor' stroke-width='2'/><line x1='10' y1='16' x2='22' y2='16' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 80,
                    DefaultHeight = 80,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["icon"] = "✕"
                    }
                },
                new()
                {
                    Id = "bpmn.event",
                    Name = "Event",
                    Category = "BPMN",
                    IconSvg = "<circle cx='16' cy='16' r='10' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 80,
                    DefaultHeight = 80,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["label"] = "Start"
                    }
                },
                new()
                {
                    Id = "bpmn.pool",
                    Name = "Pool",
                    Category = "BPMN",
                    IconSvg = "<rect x='4' y='4' width='24' height='20' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><line x1='10' y1='4' x2='10' y2='24' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 300,
                    DefaultHeight = 180,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
                    Layout = new()
                    {
                        BackgroundShape = "pool",
                        Sections =
                        [
                            new()
                            {
                                Type = "text",
                                DataKey = "label",
                                DefaultText = "Pool",
                                Padding = 4,
                                TextStyle = new()
                                {
                                    IsBold = true,
                                    TextAlign = StencilTextAlign.Center,
                                    FontSize = 12
                                }
                            },
                            new()
                            {
                                Type = "divider",
                                Padding = 0
                            },
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
                    DefaultData = new()
                    {
                        ["label"] = "Pool",
                        ["lane"] = "Lane 1"
                    }
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
                    IconSvg = "<rect x='4' y='8' width='24' height='16' rx='8' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 50,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["label"] = "Start"
                    }
                },
                new()
                {
                    Id = "flowchart.decision",
                    Name = "Decision",
                    Category = "Flowchart",
                    IconSvg = "<polygon points='16,4 28,16 16,28 4,16' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 80,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["label"] = "Decision?"
                    }
                },
                new()
                {
                    Id = "flowchart.process",
                    Name = "Process",
                    Category = "Flowchart",
                    IconSvg = "<rect x='4' y='8' width='24' height='16' rx='1' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 60,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["label"] = "Process"
                    }
                },
                new()
                {
                    Id = "flowchart.document",
                    Name = "Document",
                    Category = "Flowchart",
                    IconSvg = "<rect x='4' y='4' width='24' height='20' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 20 Q8 24 12 20 Q16 16 20 20 Q24 24 28 20' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 120,
                    DefaultHeight = 70,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["label"] = "Document"
                    }
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
                    IconSvg = "<rect x='4' y='4' width='24' height='10' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><rect x='4' y='14' width='24' height='14' rx='1' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 140,
                    DefaultHeight = 100,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                            new()
                            {
                                Type = "divider",
                                Padding = 0
                            },
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
                    IconSvg = "<polygon points='16,4 28,16 16,28 4,16' fill='none' stroke='currentColor' stroke-width='2'/>",
                    DefaultWidth = 100,
                    DefaultHeight = 80,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                    DefaultData = new()
                    {
                        ["label"] = "Has"
                    }
                },
                new()
                {
                    Id = "erd.weak-entity",
                    Name = "Weak Entity",
                    Category = "ERD",
                    IconSvg = "<rect x='4' y='4' width='24' height='10' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><rect x='4' y='14' width='24' height='14' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><rect x='5' y='5' width='22' height='8' rx='1' fill='none' stroke='currentColor' stroke-width='1.5'/><rect x='5' y='15' width='22' height='12' rx='1' fill='none' stroke='currentColor' stroke-width='1.5'/>",
                    DefaultWidth = 140,
                    DefaultHeight = 100,
                    Ports =
                    [
                        new() { Name = "top", Side = PortSide.Top, Offset = 0.5 },
                        new() { Name = "right", Side = PortSide.Right, Offset = 0.5 },
                        new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5 },
                        new() { Name = "left", Side = PortSide.Left, Offset = 0.5 }
                    ],
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
                            new()
                            {
                                Type = "divider",
                                Padding = 0
                            },
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
