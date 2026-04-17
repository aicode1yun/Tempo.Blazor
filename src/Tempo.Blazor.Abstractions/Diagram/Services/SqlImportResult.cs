using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Result of importing SQL DDL into an ER diagram.</summary>
public sealed class SqlImportResult
{
    public SqlImportResult(DiagramDocument document, string layoutDirection)
    {
        Document = document;
        LayoutDirection = layoutDirection;
    }

    public DiagramDocument Document { get; }
    public string LayoutDirection { get; }
}
