namespace Tempo.Blazor.EmailTemplates.Abstractions.Import;

/// <summary>A single import diagnostic (localization key plus optional detail and position).</summary>
/// <param name="Key">Localization key describing the diagnostic.</param>
/// <param name="Detail">Optional contextual detail (e.g. the offending element name).</param>
/// <param name="Line">1-based source line, when known.</param>
/// <param name="Column">1-based source column, when known.</param>
public sealed record ImportMessage(string Key, string? Detail = null, int? Line = null, int? Column = null);
