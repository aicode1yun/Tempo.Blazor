namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Result of parsing raw CSV text.</summary>
public sealed class CsvParseResult
{
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
    public char DetectedDelimiter { get; init; }
}
