namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>A single layout validation finding.</summary>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Key">The localization key describing the finding.</param>
/// <param name="Path">A locator for the offending node (section/block identifier).</param>
public sealed record LayoutValidationMessage(LayoutSeverity Severity, string Key, string Path);
