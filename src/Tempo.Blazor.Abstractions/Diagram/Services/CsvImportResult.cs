using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Result of importing CSV data into a diagram.</summary>
public sealed class CsvImportResult
{
    public CsvImportResult(DiagramDocument document, string layoutAlgorithm)
    {
        Document = document;
        LayoutAlgorithm = layoutAlgorithm;
    }

    public DiagramDocument Document { get; }
    public string LayoutAlgorithm { get; }
}
