using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Generates an ER diagram from parsed SQL table definitions.</summary>
public static class SqlToErDiagramGenerator
{
    public static DiagramDocument Generate(List<SqlTableDefinition> tables)
    {
        var doc = new DiagramDocument
        {
            Title = "ER Diagram",
            Pages =
            [
                new DiagramPage
                {
                    Name = "ER Diagram",
                    PageSize = DiagramPageSize.A4,
                    PageOrientation = DiagramPageOrientation.Landscape,
                    Width = 1123,
                    Height = 794,
                    Layers = []
                }
            ],
            ActivePageIndex = 0
        };

        var page = doc.ActivePage;
        var tableIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nonJunctionTables = tables.Where(t => !t.IsJunctionTable).ToList();

        double x = 40;
        double y = 40;
        int col = 0;
        const int maxCols = 4;
        const double nodeW = 180;
        const double nodeHBase = 60;
        const double gapX = 40;
        const double gapY = 40;

        foreach (var table in nonJunctionTables)
        {
            var node = new DiagramNode
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                StencilId = "erd.entity",
                X = x,
                Y = y,
                W = nodeW,
                H = Math.Max(nodeHBase, 40 + table.Columns.Count * 18),
                LayerId = null,
                Data = new Dictionary<string, object>
                {
                    ["name"] = table.Name,
                    ["attributes"] = table.Columns.Select(c =>
                    {
                        var suffix = string.Empty;
                        if (c.IsPrimaryKey) suffix += " PK";
                        if (c.IsForeignKey) suffix += " FK";
                        if (c.IsUnique && !c.IsPrimaryKey) suffix += " UQ";
                        if (!c.IsNullable) suffix += " NN";
                        return $"{c.Name}: {c.DataType}{suffix}";
                    }).ToArray()
                }
            };

            tableIdMap[table.Name] = node.Id;
            page.Nodes.Add(node);

            col++;
            if (col >= maxCols)
            {
                col = 0;
                x = 40;
                y += Math.Max(nodeHBase, 40 + table.Columns.Count * 18) + gapY;
            }
            else
            {
                x += nodeW + gapX;
            }
        }

        var processedRelations = new HashSet<(string Source, string Target)>();

        foreach (var table in nonJunctionTables)
        {
            foreach (var fk in table.ForeignKeys)
            {
                if (!tableIdMap.TryGetValue(table.Name, out var sourceId))
                    continue;
                if (!tableIdMap.TryGetValue(fk.ReferenceTable, out var targetId))
                    continue;

                var key = (Source: targetId, Target: sourceId);
                if (processedRelations.Contains(key))
                    continue;
                processedRelations.Add(key);

                page.Edges.Add(new DiagramEdge
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    SourceNodeId = targetId,
                    TargetNodeId = sourceId,
                    Routing = "straight",
                    ConnectorType = "association",
                    EndArrow = "crow",
                    Label = fk.ColumnName
                });
            }
        }

        foreach (var junction in tables.Where(t => t.IsJunctionTable))
        {
            var fk1 = junction.ForeignKeys[0];
            var fk2 = junction.ForeignKeys[1];

            if (!tableIdMap.TryGetValue(fk1.ReferenceTable, out var leftId))
                continue;
            if (!tableIdMap.TryGetValue(fk2.ReferenceTable, out var rightId))
                continue;

            page.Edges.Add(new DiagramEdge
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                SourceNodeId = leftId,
                TargetNodeId = rightId,
                Routing = "straight",
                ConnectorType = "association",
                StartArrow = "crow",
                EndArrow = "crow",
                Label = junction.Name
            });
        }

        doc.SnapToGrid(8);
        return doc;
    }
}
