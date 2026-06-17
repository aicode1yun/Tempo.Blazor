namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

/// <summary>A render error projected for transport.</summary>
/// <param name="Message">The error message.</param>
/// <param name="Line">Optional source line.</param>
/// <param name="Column">Optional source column.</param>
public sealed record RenderErrorDto(string Message, int? Line, int? Column);
